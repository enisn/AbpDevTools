using System.Reflection;

namespace AbpDevTools.Running;
public class RunningConfiguration
{
    public string Version { get; set; } = Assembly.GetExecutingAssembly().GetName().Version.ToString();

    public bool SkipMigration { get; set; }

    public string EnvironmentName { get; set; }

    public string Configuration { get; set; }

    public bool Watch { get; set; }

    public bool NoBuild { get; set; }

    public bool InstallLibs { get; set; }

    public bool GraphBuild { get; set; }

    public bool Retry { get; set; }

    public Dictionary<string, RunningConfigurationApp> Projects { get; set; } = new();
}

public class RunningConfigurationApp
{
    public string Path { get; set; }

    public string EnvironmentName { get; set; }

    public string? Configuration { get; set; }

    public bool? Watch { get; set; }

    public bool? NoBuild { get; set; }

    public bool? InstallLibs { get; set; }

    public bool? GraphBuild { get; set; }

    public Dictionary<string, string> EnvironmentVariables { get; set; } = new();
}