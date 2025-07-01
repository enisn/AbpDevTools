using AbpDevTools.Configuration;

namespace AbpDevTools.Services;

[RegisterSingleton]
public class RunnableProjectsProvider
{
    public RunnableProjectsProvider(RunConfiguration runConfiguration)
    {
        runConfiguration.CleanObsolete();
    }

    public FileInfo[] GetRunnableProjects(string path)
    {
        return Directory.EnumerateFiles(path, "*.csproj", SearchOption.AllDirectories)
            .Where(DoesHaveProgramClass)
            .Select(x => new FileInfo(x))
            .ToArray();
    }

    public FileInfo[] GetRunnableProjectsWithMigrateDatabaseParameter(string path)
    {
        return Directory.EnumerateFiles(path, "*.csproj", SearchOption.AllDirectories)
            .Where(DoesHaveProgramClass)
            .Where(DoesHaveMigrateDatabaseParameter)
            .Select(x => new FileInfo(x))
            .ToArray();
    }

    private static bool DoesHaveProgramClass(string projectPath)
    {
        return File.Exists(
            Path.Combine(
                Path.GetDirectoryName(projectPath)!,
                "Program.cs"
            ));
    }

    public bool DoesHaveMigrateDatabaseParameter(string projectPath)
    {
        if (!File.Exists(projectPath))
        {
            return false;
        }
        
        var projectDirectory = Path.GetDirectoryName(projectPath)!;
        
        // Check Program.cs first
        var programCsFilePath = Path.Combine(projectDirectory, "Program.cs");
        if (File.Exists(programCsFilePath) && ContainsMigrateDatabaseText(programCsFilePath))
        {
            return true;
        }
        
        // Check all *Module.cs files in the project directory
        var moduleFiles = Directory.EnumerateFiles(projectDirectory, "*Module.cs", SearchOption.TopDirectoryOnly);
        foreach (var moduleFile in moduleFiles)
        {
            if (ContainsMigrateDatabaseText(moduleFile))
            {
                return true;
            }
        }
        
        return false;
    }
    
    private static bool ContainsMigrateDatabaseText(string filePath)
    {
        try
        {
            using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var reader = new StreamReader(fileStream);
            
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                if (line.Contains("--migrate-database", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            
            return false;
        }
        catch (IOException)
        {
            // If we can't read the file, assume it doesn't contain the text
            return false;
        }
    }
}
