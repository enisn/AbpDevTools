using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Xml;
using System.Xml.Linq;

namespace AbpDevTools.Services;

public enum EnvironmentSupport { Supported, Missing, Unresolved }
public record EnvironmentSupportResult(EnvironmentSupport Support, string Context);

/// <summary>Bounded syntax inspection only; does not prove effective runtime configuration.</summary>
[RegisterTransient]
public class DesignTimeEnvironmentGuard
{
    public virtual EnvironmentSupportResult Inspect(FileInfo project)
    {
        try
        {
            // Only assume default SDK compile items when the project itself gives no
            // indication that membership is customized. Do not evaluate MSBuild.
            var definition = XDocument.Load(project.FullName);
            if (definition.Root?.Name.LocalName != "Project" ||
                definition.Root.Attribute("Sdk")?.Value != "Microsoft.NET.Sdk" ||
                definition.Descendants().Any(e => e.Name.LocalName is
                    "Compile" or "EnableDefaultCompileItems" or "EnableDefaultItems" or
                    "DefaultItemExcludes" or "DefaultItemExcludesInProjectFolder" or
                    "DefaultExcludesInProjectFolder" or "OverrideDefaultCompileItems" or
                    "Import" or "ImportGroup" or "Target" or "Sdk"))
                return new(EnvironmentSupport.Unresolved, project.FullName);
            var sources = Directory.EnumerateFiles(project.DirectoryName!, "*.cs", SearchOption.AllDirectories)
                .Where(p => !Path.GetRelativePath(project.DirectoryName!, p).Split(Path.DirectorySeparatorChar)
                    .Any(s => s is "bin" or "obj"))
                .Select(p => (Path: p, Source: File.ReadAllText(p))).ToArray();
            var factories = sources.Where(s => CSharpSyntaxTree.ParseText(s.Source).GetRoot().DescendantNodes()
                .OfType<ClassDeclarationSyntax>().Any(IsFactory)).ToArray();
            if (factories.Length != 1) return new(EnvironmentSupport.Unresolved, project.FullName);
            var factoryName = CSharpSyntaxTree.ParseText(factories[0].Source).GetRoot().DescendantNodes()
                .OfType<ClassDeclarationSyntax>().First(IsFactory).Identifier.ValueText;
            if (sources.Any(s => HasDerivedFactory(CSharpSyntaxTree.ParseText(s.Source).GetRoot(), factoryName)))
                return new(EnvironmentSupport.Unresolved, project.FullName);
            return new(InspectSource(factories[0].Source), $"{project.FullName}; {factories[0].Path}; factory {factoryName}");
        }
        catch (IOException) { return new(EnvironmentSupport.Unresolved, project.FullName); }
        catch (UnauthorizedAccessException) { return new(EnvironmentSupport.Unresolved, project.FullName); }
        catch (XmlException) { return new(EnvironmentSupport.Unresolved, project.FullName); }
    }

    private static bool IsFactory(ClassDeclarationSyntax c) => c.BaseList?.Types.Any(t =>
        t.Type.DescendantNodesAndSelf().OfType<GenericNameSyntax>().Any(n => n.Identifier.ValueText == "IDesignTimeDbContextFactory")) == true;

    private static bool HasDerivedFactory(SyntaxNode root, string name) => root.DescendantNodes()
        .OfType<ClassDeclarationSyntax>().Any(c => c.BaseList?.Types.Any(t =>
            t.Type.DescendantNodesAndSelf().OfType<SimpleNameSyntax>().Any(n => n.Identifier.ValueText == name)) == true);

    public static EnvironmentSupport InspectSource(string source)
    {
        var tree = CSharpSyntaxTree.ParseText(source);
        var root = tree.GetRoot();
        if (tree.GetDiagnostics().Any(d => d.Severity == DiagnosticSeverity.Error) ||
            root.DescendantTrivia(descendIntoTrivia: true).Any(t => t.IsDirective)) return EnvironmentSupport.Unresolved;
        var factories = root.DescendantNodes().OfType<ClassDeclarationSyntax>().Where(IsFactory).ToArray();
        if (factories.Length != 1) return EnvironmentSupport.Unresolved;
        var factory = factories[0];
        // Other base types, partial declarations and overloads require semantic information.
        if (factory.BaseList!.Types.Count != 1 || factory.Modifiers.Any(SyntaxKind.PartialKeyword) ||
            factory.Modifiers.Any(SyntaxKind.AbstractKeyword) || HasDerivedFactory(root, factory.Identifier.ValueText)) return EnvironmentSupport.Unresolved;
        var methods = factory.Members.OfType<MethodDeclarationSyntax>().ToArray();
        var entry = methods.Where(m => m.Identifier.ValueText == "CreateDbContext").ToArray();
        if (entry.Length != 1) return EnvironmentSupport.Unresolved;
        var results = new List<EnvironmentSupport>();
        var visiting = new HashSet<string>();
        void Visit(MethodDeclarationSyntax method)
        {
            if (visiting.Count >= 32 || !visiting.Add(method.Identifier.ValueText)) { results.Add(EnvironmentSupport.Unresolved); return; }
            var statements = method.Body?.Statements;
            if (statements == null || statements.Value.Any(s => s is not LocalDeclarationStatementSyntax and not ReturnStatementSyntax and not ExpressionStatementSyntax))
            { results.Add(EnvironmentSupport.Unresolved); return; }
            var locals = statements.Value.OfType<LocalDeclarationStatementSyntax>().SelectMany(s => s.Declaration.Variables).ToArray();
            var returns = statements.Value.OfType<ReturnStatementSyntax>().ToArray();
            if (returns.Length != 1) { results.Add(EnvironmentSupport.Unresolved); return; }
            var seen = new HashSet<string>();
            var setupCalls = statements.Value.OfType<ExpressionStatementSyntax>().ToArray();
            var consumedSetup = new HashSet<ExpressionStatementSyntax>();
            void Follow(ExpressionSyntax? expression, int depth = 0)
            {
                if (depth >= 64) { results.Add(EnvironmentSupport.Unresolved); return; }
                if (expression is IdentifierNameSyntax id)
                {
                    var matches = locals.Where(v => v.Identifier.ValueText == id.Identifier.ValueText).ToArray();
                    if (matches.Length == 1 && seen.Add(id.Identifier.ValueText)) Follow(matches[0].Initializer?.Value, depth + 1);
                    else results.Add(EnvironmentSupport.Unresolved);
                }
                else if (expression is MemberAccessExpressionSyntax options && options.Name.Identifier.ValueText == "Options" && options.Expression is IdentifierNameSyntax optionsId)
                {
                    var declarations = locals.Where(v => v.Identifier.ValueText == optionsId.Identifier.ValueText).ToArray();
                    var declaration = declarations.Length == 1 ? declarations[0] : null;
                    seen.Add(optionsId.Identifier.ValueText);
                    if (declaration?.Initializer?.Value is not ObjectCreationExpressionSyntax creation ||
                        !creation.Type.DescendantNodesAndSelf().OfType<SimpleNameSyntax>().Any(n => n.Identifier.ValueText == "DbContextOptionsBuilder"))
                    { results.Add(EnvironmentSupport.Unresolved); return; }
                    var calls = setupCalls.Where(s => s.Expression is InvocationExpressionSyntax c &&
                        c.Expression is MemberAccessExpressionSyntax m && m.Expression is IdentifierNameSyntax receiver &&
                        receiver.Identifier.ValueText == optionsId.Identifier.ValueText &&
                        m.Name.Identifier.ValueText is "UseSqlServer" or "UseNpgsql" or "UseMySql" or "UseSqlite").ToArray();
                    if (calls.Length != 1) { results.Add(EnvironmentSupport.Unresolved); return; }
                    consumedSetup.Add(calls[0]);
                    var arguments = ((InvocationExpressionSyntax)calls[0].Expression).ArgumentList.Arguments;
                    if (arguments.Count != 1) { results.Add(EnvironmentSupport.Unresolved); return; }
                    Follow(arguments[0].Expression, depth + 1);
                }
                else if (expression is InvocationExpressionSyntax connection && connection.Expression is MemberAccessExpressionSyntax get &&
                    get.Name.Identifier.ValueText == "GetConnectionString") Follow(get.Expression, depth + 1);
                else if (expression is InvocationExpressionSyntax call && call.Expression is MemberAccessExpressionSyntax member && member.Name.Identifier.ValueText == "Build")
                {
                    results.Add(Builder(member.Expression, new HashSet<string>()));
                }
                else if (expression is InvocationExpressionSyntax helper && helper.Expression is IdentifierNameSyntax name)
                {
                    if (locals.Any(v => v.Identifier.ValueText == name.Identifier.ValueText) ||
                        method.ParameterList.Parameters.Any(p => p.Identifier.ValueText == name.Identifier.ValueText))
                    { results.Add(EnvironmentSupport.Unresolved); return; }
                    var matches = methods.Where(m => m.Identifier.ValueText == name.Identifier.ValueText && m.ParameterList.Parameters.Count == 0).ToArray();
                    if (matches.Length == 1 && helper.ArgumentList.Arguments.Count == 0) Visit(matches[0]);
                    else results.Add(EnvironmentSupport.Unresolved);
                }
                else if (expression is ObjectCreationExpressionSyntax context)
                {
                    foreach (var argument in context.ArgumentList?.Arguments ?? default) Follow(argument.Expression, depth + 1);
                }
                else results.Add(EnvironmentSupport.Unresolved);
            }
            EnvironmentSupport Builder(ExpressionSyntax expression, HashSet<string> visited, int depth = 0)
            {
                if (depth >= 64) return EnvironmentSupport.Unresolved;
                if (expression is IdentifierNameSyntax id)
                {
                    var matches = locals.Where(v => v.Identifier.ValueText == id.Identifier.ValueText).ToArray();
                    seen.Add(id.Identifier.ValueText);
                    return matches.Length == 1 && visited.Add(id.Identifier.ValueText) && matches[0].Initializer != null
                        ? Builder(matches[0].Initializer!.Value, visited, depth + 1) : EnvironmentSupport.Unresolved;
                }
                if (expression is ObjectCreationExpressionSyntax creation && creation.Type.ToString() is "ConfigurationBuilder" or "Microsoft.Extensions.Configuration.ConfigurationBuilder")
                    return creation.Initializer == null ? EnvironmentSupport.Missing : EnvironmentSupport.Unresolved;
                if (expression is InvocationExpressionSyntax invocation && invocation.Expression is MemberAccessExpressionSyntax access)
                {
                    var name = access.Name.Identifier.ValueText;
                    if (name == "AddEnvironmentVariables" && invocation.ArgumentList.Arguments.Count != 0) return EnvironmentSupport.Unresolved;
                    if (name is not ("AddEnvironmentVariables" or "SetBasePath" or "AddJsonFile")) return EnvironmentSupport.Unresolved;
                    var prior = Builder(access.Expression, visited, depth + 1);
                    // Arguments can pass the traced builder to code that mutates it.
                    if (invocation.ArgumentList.Arguments.Any(a => a.Expression.DescendantNodesAndSelf()
                        .OfType<IdentifierNameSyntax>().Any(id => visited.Contains(id.Identifier.ValueText))))
                        return EnvironmentSupport.Unresolved;
                    return prior == EnvironmentSupport.Unresolved ? prior : name == "AddEnvironmentVariables" ? EnvironmentSupport.Supported : prior;
                }
                return EnvironmentSupport.Unresolved;
            }
            Follow(returns[0].Expression);
            if (setupCalls.Any(s => !consumedSetup.Contains(s)) || locals.Any(v => !seen.Contains(v.Identifier.ValueText) &&
                v.Initializer?.Value.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>().Any(id => seen.Contains(id.Identifier.ValueText)) == true))
                results.Add(EnvironmentSupport.Unresolved);
        }
        Visit(entry[0]);
        return results.Count == 1 ? results[0] : EnvironmentSupport.Unresolved;
    }
}
