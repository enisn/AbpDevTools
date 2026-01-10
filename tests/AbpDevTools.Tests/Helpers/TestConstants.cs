namespace AbpDevTools.Tests.Helpers;

/// <summary>
/// Shared constants used across unit tests.
/// </summary>
public static class TestConstants
{
    /// <summary>
    /// Sample project names used in tests.
    /// </summary>
    public static class ProjectNames
    {
        public const string AcmeBookStore = "Acme.BookStore";
        public const string AcmeBookStoreDbMigrator = "Acme.BookStore.DbMigrator";
        public const string AcmeBookStoreWeb = "Acme.BookStore.Web";
        public const string TestProject = "TestProject";
        public const string SampleProject = "Sample.Project";
    }

    /// <summary>
    /// Sample file paths used in tests.
    /// </summary>
    public static class FilePaths
    {
        public const string TestCsprojPath = "C:\\test\\project\\TestProject.csproj";
        public const string TestSolutionPath = "C:\\test\\TestProject.sln";
        public const string AppSettingsPath = "C:\\test\\project\\appsettings.json";
        public const string TestConfigPath = "C:\\test\\config\\test.yml";
        public const string GitRepositoryPath = "C:\\github\\test-repo";
    }

    /// <summary>
    /// Sample ports used in tests.
    /// </summary>
    public static class Ports
    {
        public const int Http = 5000;
        public const int Https = 5001;
        public const int Database = 5432;
        public const int Redis = 6379;
    }

    /// <summary>
    /// Sample URLs and paths used in tests.
    /// </summary>
    public static class Urls
    {
        public const string GitHubRepository = "https://github.com/test/test-repo.git";
        public const string LocalSourcePath = "C:\\github\\test-repo";
        public const string NuGetPackageSource = "https://api.nuget.org/v3/index.json";
    }

    /// <summary>
    /// Sample configuration file names.
    /// </summary>
    public static class ConfigFiles
    {
        public const string LocalSources = "local-sources.yml";
        public const string RunConfiguration = "run-configuration.yml";
        public const string ReplacementConfiguration = "replacement-configuration.yml";
        public const string EnvironmentConfiguration = "environment-configuration.yml";
    }

    /// <summary>
    /// Sample YAML content for configuration tests.
    /// </summary>
    public static class YamlSamples
    {
        public const string LocalSources = @"
abp:
  remote-path: https://github.com/abpframework/abp.git
  branch: dev
  path: C:\github\abp
  packages:
    - Volo.*
";

        public const string ReplacementConfiguration = @"
replacements:
  - find: ""OldText""
    replace-with: ""NewText""
    file-pattern: ""*.cs""
";

        public const string RunConfiguration = @"
projects:
  - name: Acme.BookStore.Web
    working-directory: C:\Projects\AcmeBookStore\src\Acme.BookStore.Web
    arguments: ""--urls http://localhost:5000""
";

        public const string EnvironmentConfiguration = @"
name: Development
environment-variables:
  ASPNETCORE_ENVIRONMENT: Development
  ASPNETCORE_URLS: http://localhost:5000
";
    }

    /// <summary>
    /// Sample process information for tests.
    /// </summary>
    public static class ProcessData
    {
        public const int SamplePid = 12345;
        public const string SampleProcessName = "dotnet";
        public const string SampleProcessPath = "C:\\Program Files\\dotnet\\dotnet.exe";
    }

    /// <summary>
    /// Test timeout values.
    /// </summary>
    public static class Timeouts
    {
        public const int Short = 100;           // 100ms
        public const int Medium = 1000;         // 1 second
        public const int Long = 5000;           // 5 seconds
        public const int VeryLong = 30000;      // 30 seconds
    }
}
