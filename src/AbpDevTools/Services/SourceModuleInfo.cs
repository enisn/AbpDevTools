using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AbpDevTools.Services;

public class SourceModuleInfo
{
    public string Name { get; set; } = string.Empty;
    public string Namespace { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public ClassDeclarationSyntax? ClassDeclaration { get; set; }
}
