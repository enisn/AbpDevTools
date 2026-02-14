using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AbpDevTools.Services;

[RegisterTransient]
public class SourceCodeModuleFinder
{
    public List<SourceModuleInfo> FindAbpModuleClasses(string csprojFilePath)
    {
        var modules = new List<SourceModuleInfo>();
        var csprojDirectory = Path.GetDirectoryName(csprojFilePath);
        
        if (string.IsNullOrEmpty(csprojDirectory))
        {
            return modules;
        }

        var binPath = Path.Combine(csprojDirectory, "bin");
        var objPath = Path.Combine(csprojDirectory, "obj");

        var csFiles = Directory.GetFiles(csprojDirectory, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.StartsWith(binPath, StringComparison.OrdinalIgnoreCase) &&
                        !f.StartsWith(objPath, StringComparison.OrdinalIgnoreCase));

        foreach (var csFile in csFiles)
        {
            try
            {
                var moduleInfo = FindAbpModuleClassInFile(csFile);
                if (moduleInfo != null)
                {
                    modules.Add(moduleInfo);
                }
            }
            catch
            {
            }
        }

        return modules;
    }

    public SourceModuleInfo? FindAbpModuleClassInFile(string csFilePath)
    {
        var sourceText = File.ReadAllText(csFilePath);
        
        if (!sourceText.Contains("class"))
        {
            return null;
        }

        var tree = CSharpSyntaxTree.ParseText(sourceText);
        var root = tree.GetRoot();

        var classDeclarations = root.DescendantNodes()
            .OfType<ClassDeclarationSyntax>();

        foreach (var classDecl in classDeclarations)
        {
            if (InheritsFromAbpModule(classDecl))
            {
                var namespaceDecl = classDecl.Ancestors()
                    .OfType<BaseNamespaceDeclarationSyntax>()
                    .FirstOrDefault();

                var namespaceName = namespaceDecl?.Name.ToString() ?? string.Empty;
                var className = classDecl.Identifier.Text;

                return new SourceModuleInfo
                {
                    Name = className,
                    Namespace = namespaceName,
                    FullName = string.IsNullOrEmpty(namespaceName) ? className : $"{namespaceName}.{className}",
                    FilePath = csFilePath,
                    ClassDeclaration = classDecl
                };
            }
        }

        return null;
    }

    private bool InheritsFromAbpModule(ClassDeclarationSyntax classDecl)
    {
        if (classDecl.BaseList == null)
        {
            return false;
        }

        foreach (var baseType in classDecl.BaseList.Types)
        {
            var typeName = baseType.Type.ToString();
            
            if (typeName.EndsWith("AbpModule") || typeName == "AbpModule" ||
                (typeName.EndsWith("Module") && typeName.Contains("Abp")))
            {
                return true;
            }

            if (typeName.Contains("<"))
            {
                var genericName = typeName.Substring(0, typeName.IndexOf('<'));
                if (genericName.EndsWith("AbpModule") || genericName == "AbpModule")
                {
                    return true;
                }
            }
        }

        return false;
    }
}

public class SourceModuleInfo
{
    public string Name { get; set; } = string.Empty;
    public string Namespace { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public ClassDeclarationSyntax? ClassDeclaration { get; set; }
}
