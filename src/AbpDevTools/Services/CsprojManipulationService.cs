using System.Xml.Linq;
using AbpDevTools.Configuration;

namespace AbpDevTools.Services;

[RegisterTransient]
public class CsprojManipulationService
{
    private readonly FileExplorer _fileExplorer;

    public CsprojManipulationService(FileExplorer fileExplorer)
    {
        _fileExplorer = fileExplorer;
    }

    public Dictionary<string, Dictionary<string, string>> BuildProjectLookupCache(
        List<KeyValuePair<string, LocalSourceMappingItem>> sources)
    {
        var cache = new Dictionary<string, Dictionary<string, string>>();

        foreach (var source in sources)
        {
            var sourceKey = source.Key;
            var sourceConfig = source.Value;

            if (!Directory.Exists(sourceConfig.Path))
            {
                cache[sourceKey] = new Dictionary<string, string>();
                continue;
            }

            var projectFiles = _fileExplorer.FindDescendants(sourceConfig.Path, "*.csproj").ToList();
            var sourceProjects = new Dictionary<string, string>();

            foreach (var projectFile in projectFiles)
            {
                var projectName = Path.GetFileNameWithoutExtension(projectFile);
                sourceProjects[projectName] = projectFile;
            }

            cache[sourceKey] = sourceProjects;
        }

        return cache;
    }

    public List<XElement> GetMatchingPackageReferences(XDocument doc, string pattern)
    {
        var packageReferences = doc.Descendants("PackageReference").ToList();
        
        if (pattern.EndsWith("*"))
        {
            var prefix = pattern.Substring(0, pattern.Length - 1);
            return packageReferences.Where(pr => 
                pr.Attribute("Include")?.Value?.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) == true).ToList();
        }
        else
        {
            return packageReferences.Where(pr => 
                string.Equals(pr.Attribute("Include")?.Value, pattern, StringComparison.OrdinalIgnoreCase)).ToList();
        }
    }

    public List<XElement> GetAllProjectReferences(XDocument doc)
    {
        return doc.Descendants("ProjectReference").ToList();
    }

    public string? FindLocalProject(string packageName, string sourceKey, Dictionary<string, Dictionary<string, string>> projectLookupCache)
    {
        if (projectLookupCache.TryGetValue(sourceKey, out var sourceProjects))
        {
            return sourceProjects.TryGetValue(packageName, out var projectPath) ? projectPath : null;
        }
        return null;
    }

    public string? FindSourceForProject(string projectPath, List<KeyValuePair<string, LocalSourceMappingItem>> sources)
    {
        var projectName = Path.GetFileNameWithoutExtension(projectPath);
        
        foreach (var source in sources)
        {
            var sourceConfig = source.Value;
            if (!Directory.Exists(sourceConfig.Path)) continue;

            var projectFiles = _fileExplorer.FindDescendants(sourceConfig.Path, $"{projectName}.csproj");
            if (projectFiles.Any(pf => Path.GetFullPath(pf) == Path.GetFullPath(projectPath)))
            {
                return source.Key;
            }
        }
        return null;
    }

    public string GetRelativePath(string fromPath, string toPath)
    {
        var fromUri = new Uri(fromPath + Path.DirectorySeparatorChar);
        var toUri = new Uri(toPath);
        var relativeUri = fromUri.MakeRelativeUri(toUri);
        return Uri.UnescapeDataString(relativeUri.ToString()).Replace('/', Path.DirectorySeparatorChar);
    }

    public void ConvertToProjectReference(XElement packageRef, string projectPath)
    {
        // Change element name from PackageReference to ProjectReference
        packageRef.Name = "ProjectReference";
        
        // Update Include attribute to point to project file
        packageRef.SetAttributeValue("Include", projectPath);
        
        // Remove Version attribute if present
        packageRef.Attribute("Version")?.Remove();
        
        // Remove other package-specific attributes
        packageRef.Attribute("PrivateAssets")?.Remove();
        packageRef.Attribute("IncludeAssets")?.Remove();
        packageRef.Attribute("ExcludeAssets")?.Remove();
    }

    public void ConvertToPackageReference(XElement projectRef, string packageName, string version)
    {
        // Change element name from ProjectReference to PackageReference
        projectRef.Name = "PackageReference";
        
        // Update Include attribute to package name
        projectRef.SetAttributeValue("Include", packageName);
        
        // Add Version attribute
        projectRef.SetAttributeValue("Version", version);
    }

    public void AddVersionBackupProperties(XDocument doc, Dictionary<string, string> versionsToBackup)
    {
        if (!versionsToBackup.Any()) return;

        var root = doc.Root;
        if (root == null) return;

        // Find or create PropertyGroup for version backup
        var versionPropertyGroup = root.Elements("PropertyGroup")
            .FirstOrDefault(pg => pg.Elements().Any(e => e.Name.LocalName.EndsWith("Version")));

        if (versionPropertyGroup == null)
        {
            versionPropertyGroup = new XElement("PropertyGroup");
            root.Add(versionPropertyGroup);
        }

        foreach (var version in versionsToBackup)
        {
            var propertyName = $"{version.Key}Version";
            var existingProperty = versionPropertyGroup.Element(propertyName);
            
            if (existingProperty == null)
            {
                versionPropertyGroup.Add(new XElement(propertyName, version.Value));
            }
        }
    }

    public string? GetBackedUpVersion(XDocument doc, string sourceKey)
    {
        var propertyName = $"{sourceKey}Version";
        return doc.Descendants("PropertyGroup")
            .SelectMany(pg => pg.Elements(propertyName))
            .FirstOrDefault()?.Value;
    }

    public bool IsProjectUnderSource(string projectPath, string sourcePath)
    {
        try
        {
            var fullProjectPath = Path.GetFullPath(projectPath);
            var fullSourcePath = Path.GetFullPath(sourcePath);
            return fullProjectPath.StartsWith(fullSourcePath, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }
} 