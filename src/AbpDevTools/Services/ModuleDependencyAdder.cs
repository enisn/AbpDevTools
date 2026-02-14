namespace AbpDevTools.Services;

[RegisterTransient]
public class ModuleDependencyAdder
{
    public void AddDependency(string filePath, string moduleFullName)
    {
        ParseModuleNameAndNamespace(moduleFullName, out var namespaceName, out var moduleName);

        var fileContent = File.ReadAllText(filePath);
        
        fileContent = AddUsingStatement(fileContent, namespaceName);
        
        if (fileContent.Contains(moduleName) && fileContent.Contains("DependsOn"))
        {
            return;
        }

        fileContent = InsertDependsOnAttribute(fileContent, moduleName);

        File.WriteAllText(filePath, fileContent);
    }

    public void AddDependency(string filePath, string moduleFullName, bool checkExisting)
    {
        ParseModuleNameAndNamespace(moduleFullName, out var namespaceName, out var moduleName);

        var fileContent = File.ReadAllText(filePath);
        
        if (checkExisting && fileContent.Contains(moduleName))
        {
            return;
        }

        fileContent = AddUsingStatement(fileContent, namespaceName);
        fileContent = InsertDependsOnAttribute(fileContent, moduleName);

        File.WriteAllText(filePath, fileContent);
    }

    private string AddUsingStatement(string content, string? namespaceName)
    {
        if (string.IsNullOrEmpty(namespaceName))
        {
            return content;
        }

        var usingStatement = $"using {namespaceName};";
        
        if (content.Contains(usingStatement))
        {
            return content;
        }

        var lastUsingIndex = content.LastIndexOf("using ", StringComparison.Ordinal);
        if (lastUsingIndex >= 0)
        {
            var endOfLineIndex = content.IndexOf('\n', lastUsingIndex);
            if (endOfLineIndex >= 0)
            {
                return content.Insert(endOfLineIndex + 1, usingStatement + Environment.NewLine);
            }
        }

        var namespaceIndex = content.IndexOf("namespace ", StringComparison.Ordinal);
        if (namespaceIndex >= 0)
        {
            return content.Insert(namespaceIndex, usingStatement + Environment.NewLine + Environment.NewLine);
        }

        return usingStatement + Environment.NewLine + content;
    }

    private string InsertDependsOnAttribute(string content, string moduleName)
    {
        var indexOfPublicClass = content.IndexOf("public class", StringComparison.Ordinal);
        
        if (indexOfPublicClass < 0)
        {
            throw new InvalidOperationException("Could not find 'public class' declaration in the module file.");
        }

        var existingDependsOn = FindExistingDependsOnAttribute(content, indexOfPublicClass);
        
        if (existingDependsOn.HasValue)
        {
            return AddToExistingDependsOn(content, existingDependsOn.Value, moduleName);
        }

        var dependsOnAttribute = $"[DependsOn(typeof({moduleName}))]{Environment.NewLine}    ";
        
        return content.Insert(indexOfPublicClass, dependsOnAttribute);
    }

    private (int Start, int End)? FindExistingDependsOnAttribute(string content, int beforeIndex)
    {
        var searchStart = Math.Max(0, beforeIndex - 500);
        var searchText = content.Substring(searchStart, beforeIndex - searchStart);
        
        var dependsOnIndex = searchText.LastIndexOf("[DependsOn(", StringComparison.Ordinal);
        
        if (dependsOnIndex < 0)
        {
            return null;
        }

        var actualStart = searchStart + dependsOnIndex;
        var endBracket = content.IndexOf(']', actualStart);
        
        if (endBracket < 0 || endBracket > beforeIndex)
        {
            return null;
        }

        return (actualStart, endBracket);
    }

    private string AddToExistingDependsOn(string content, (int Start, int End) dependsOnRange, string moduleName)
    {
        var dependsOnContent = content.Substring(dependsOnRange.Start, dependsOnRange.End - dependsOnRange.Start + 1);
        
        if (dependsOnContent.Contains($"typeof({moduleName})"))
        {
            return content;
        }

        var closingParenIndex = dependsOnContent.LastIndexOf(')');
        var insertPosition = dependsOnRange.Start + closingParenIndex;
        
        return content.Insert(insertPosition, $", typeof({moduleName})");
    }

    private void ParseModuleNameAndNamespace(string? fullName, out string? namespaceName, out string moduleName)
    {
        if (string.IsNullOrEmpty(fullName))
        {
            namespaceName = null;
            moduleName = fullName ?? string.Empty;
            return;
        }

        var lastDotIndex = fullName.LastIndexOf('.');
        
        if (lastDotIndex < 0)
        {
            namespaceName = null;
            moduleName = fullName;
            return;
        }

        namespaceName = fullName.Substring(0, lastDotIndex);
        moduleName = fullName.Substring(lastDotIndex + 1);
    }
}
