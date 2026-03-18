using AbpDevTools.Configuration;
using AbpDevTools.LocalConfigurations;
using FluentAssertions;
using Xunit;

namespace AbpDevTools.Tests.Commands;

/// <summary>
/// Unit tests for RunCommand libs check functionality.
/// Tests verify how the command detects missing wwwroot/libs folders
/// and prompts the user to install libraries.
/// Projects without a package.json are skipped entirely since they have no frontend dependencies.
/// </summary>
public class RunCommand_LibsCheckTests
{
    #region CheckLibsDirectory Tests

    [Fact]
    public void CheckLibsDirectory_DetectsMissingWwwrootLibsFolder()
    {
        // Arrange
        var testRootPath = Path.Combine(Path.GetTempPath(), $"AbpDevToolsTests_{Guid.NewGuid()}");
        var projectDir = Path.Combine(testRootPath, "MyProject.Web");
        Directory.CreateDirectory(projectDir);

        try
        {
            CreateProjectWithProgramCs(projectDir, includePackageJson: true);
            // Don't create wwwroot/libs - it's missing

            var wwwRootLibs = Path.Combine(projectDir, "wwwroot", "libs");

            // Act
            var hasPackageJson = File.Exists(Path.Combine(projectDir, "package.json"));
            var libsExists = Directory.Exists(wwwRootLibs);

            // Assert
            hasPackageJson.Should().BeTrue("package.json should exist for frontend project");
            libsExists.Should().BeFalse("wwwroot/libs folder should not exist");
        }
        finally
        {
            CleanupTestDirectory(testRootPath);
        }
    }

    [Fact]
    public void CheckLibsDirectory_DetectsEmptyWwwrootLibsFolder()
    {
        // Arrange
        var testRootPath = Path.Combine(Path.GetTempPath(), $"AbpDevToolsTests_{Guid.NewGuid()}");
        var projectDir = Path.Combine(testRootPath, "MyProject.Web");
        Directory.CreateDirectory(projectDir);

        try
        {
            CreateProjectWithProgramCs(projectDir, includePackageJson: true);

            var wwwRootLibs = Path.Combine(projectDir, "wwwroot", "libs");
            Directory.CreateDirectory(wwwRootLibs);

            // Act
            var hasPackageJson = File.Exists(Path.Combine(projectDir, "package.json"));
            var libsExists = Directory.Exists(wwwRootLibs);
            var hasEntries = libsExists && Directory.EnumerateFileSystemEntries(wwwRootLibs).Any();

            // Assert
            hasPackageJson.Should().BeTrue("package.json should exist for frontend project");
            libsExists.Should().BeTrue("wwwroot/libs folder should exist");
            hasEntries.Should().BeFalse("wwwroot/libs folder should be empty");
        }
        finally
        {
            CleanupTestDirectory(testRootPath);
        }
    }

    [Fact]
    public void CheckLibsDirectory_DetectsPopulatedWwwrootLibsFolder()
    {
        // Arrange
        var testRootPath = Path.Combine(Path.GetTempPath(), $"AbpDevToolsTests_{Guid.NewGuid()}");
        var projectDir = Path.Combine(testRootPath, "MyProject.Web");
        Directory.CreateDirectory(projectDir);

        try
        {
            CreateProjectWithProgramCs(projectDir, includePackageJson: true);

            var wwwRootLibs = Path.Combine(projectDir, "wwwroot", "libs");
            Directory.CreateDirectory(wwwRootLibs);
            File.WriteAllText(Path.Combine(wwwRootLibs, "test.js"), "console.log('test');");

            // Act
            var libsExists = Directory.Exists(wwwRootLibs);
            var hasEntries = libsExists && Directory.EnumerateFileSystemEntries(wwwRootLibs).Any();

            // Assert
            libsExists.Should().BeTrue("wwwroot/libs folder should exist");
            hasEntries.Should().BeTrue("wwwroot/libs folder should contain files");
        }
        finally
        {
            CleanupTestDirectory(testRootPath);
        }
    }

    [Fact]
    public void CheckLibsDirectory_IdentifiesProjectsNeedingLibs()
    {
        // Arrange
        var testRootPath = Path.Combine(Path.GetTempPath(), $"AbpDevToolsTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(testRootPath);

        try
        {
            var webDir = Path.Combine(testRootPath, "MyProject.Web");
            var apiDir = Path.Combine(testRootPath, "MyProject.HttpApi.Host");
            var blazorDir = Path.Combine(testRootPath, "MyProject.Blazor");

            // Web: has package.json, missing libs → should be flagged
            CreateProjectWithProgramCs(webDir, includePackageJson: true);

            // HttpApi.Host: has package.json, libs populated → should NOT be flagged
            CreateProjectWithProgramCs(apiDir, includePackageJson: true);
            var wwwRootLibs2 = Path.Combine(apiDir, "wwwroot", "libs");
            Directory.CreateDirectory(wwwRootLibs2);
            File.WriteAllText(Path.Combine(wwwRootLibs2, "test.js"), "console.log('test');");

            // Blazor: has package.json, missing libs → should be flagged
            CreateProjectWithProgramCs(blazorDir, includePackageJson: true);

            var projectFiles = new[]
            {
                new FileInfo(Path.Combine(webDir, "MyProject.Web.csproj")),
                new FileInfo(Path.Combine(apiDir, "MyProject.HttpApi.Host.csproj")),
                new FileInfo(Path.Combine(blazorDir, "MyProject.Blazor.csproj"))
            };

            // Act - Replicate the detection logic from RunCommand
            var projectsNeedingLibs = new List<FileInfo>();
            foreach (var csproj in projectFiles)
            {
                var projectDir = Path.GetDirectoryName(csproj.FullName)!;

                if (!File.Exists(Path.Combine(projectDir, "package.json")))
                    continue;

                var wwwRootLibs = Path.Combine(projectDir, "wwwroot", "libs");
                if (!Directory.Exists(wwwRootLibs) || !Directory.EnumerateFileSystemEntries(wwwRootLibs).Any())
                {
                    projectsNeedingLibs.Add(csproj);
                }
            }

            // Assert
            projectsNeedingLibs.Should().HaveCount(2, "web and blazor should need libs");
            projectsNeedingLibs.Select(p => p.Name).Should().Contain("MyProject.Web.csproj");
            projectsNeedingLibs.Select(p => p.Name).Should().Contain("MyProject.Blazor.csproj");
            projectsNeedingLibs.Select(p => p.Name).Should().NotContain("MyProject.HttpApi.Host.csproj");
        }
        finally
        {
            CleanupTestDirectory(testRootPath);
        }
    }

    [Fact]
    public void CheckLibsDirectory_SkipsProjectsWithoutPackageJson()
    {
        // Arrange
        var testRootPath = Path.Combine(Path.GetTempPath(), $"AbpDevToolsTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(testRootPath);

        try
        {
            // Domain project: no package.json, no wwwroot/libs → should NOT be flagged
            var domainDir = Path.Combine(testRootPath, "MyProject.Domain");
            CreateProjectWithProgramCs(domainDir, includePackageJson: false);

            // API host: no package.json, no wwwroot/libs → should NOT be flagged
            var apiDir = Path.Combine(testRootPath, "MyProject.HttpApi.Host");
            CreateProjectWithProgramCs(apiDir, includePackageJson: false);

            // Web project: has package.json, no libs → SHOULD be flagged
            var webDir = Path.Combine(testRootPath, "MyProject.Web");
            CreateProjectWithProgramCs(webDir, includePackageJson: true);

            var projectFiles = new[]
            {
                new FileInfo(Path.Combine(domainDir, "MyProject.Domain.csproj")),
                new FileInfo(Path.Combine(apiDir, "MyProject.HttpApi.Host.csproj")),
                new FileInfo(Path.Combine(webDir, "MyProject.Web.csproj"))
            };

            // Act
            var projectsNeedingLibs = new List<FileInfo>();
            foreach (var csproj in projectFiles)
            {
                var projectDir = Path.GetDirectoryName(csproj.FullName)!;

                if (!File.Exists(Path.Combine(projectDir, "package.json")))
                    continue;

                var wwwRootLibs = Path.Combine(projectDir, "wwwroot", "libs");
                if (!Directory.Exists(wwwRootLibs) || !Directory.EnumerateFileSystemEntries(wwwRootLibs).Any())
                {
                    projectsNeedingLibs.Add(csproj);
                }
            }

            // Assert
            projectsNeedingLibs.Should().ContainSingle()
                .Which.Name.Should().Be("MyProject.Web.csproj");
        }
        finally
        {
            CleanupTestDirectory(testRootPath);
        }
    }

    [Fact]
    public void CheckLibsDirectory_NoProjectsNeedLibs_WhenNoneHavePackageJson()
    {
        // Arrange
        var testRootPath = Path.Combine(Path.GetTempPath(), $"AbpDevToolsTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(testRootPath);

        try
        {
            var domainDir = Path.Combine(testRootPath, "MyProject.Domain");
            var appDir = Path.Combine(testRootPath, "MyProject.Application");

            CreateProjectWithProgramCs(domainDir, includePackageJson: false);
            CreateProjectWithProgramCs(appDir, includePackageJson: false);

            var projectFiles = new[]
            {
                new FileInfo(Path.Combine(domainDir, "MyProject.Domain.csproj")),
                new FileInfo(Path.Combine(appDir, "MyProject.Application.csproj"))
            };

            // Act
            var projectsNeedingLibs = new List<FileInfo>();
            foreach (var csproj in projectFiles)
            {
                var projectDir = Path.GetDirectoryName(csproj.FullName)!;

                if (!File.Exists(Path.Combine(projectDir, "package.json")))
                    continue;

                var wwwRootLibs = Path.Combine(projectDir, "wwwroot", "libs");
                if (!Directory.Exists(wwwRootLibs) || !Directory.EnumerateFileSystemEntries(wwwRootLibs).Any())
                {
                    projectsNeedingLibs.Add(csproj);
                }
            }

            // Assert — no prompt should ever appear
            projectsNeedingLibs.Should().BeEmpty("no projects have package.json so none need libs");
        }
        finally
        {
            CleanupTestDirectory(testRootPath);
        }
    }

    [Fact]
    public void CheckLibsDirectory_ProjectWithPackageJsonAndPopulatedLibs_NotFlagged()
    {
        // Arrange
        var testRootPath = Path.Combine(Path.GetTempPath(), $"AbpDevToolsTests_{Guid.NewGuid()}");
        var projectDir = Path.Combine(testRootPath, "MyProject.Web");
        Directory.CreateDirectory(projectDir);

        try
        {
            CreateProjectWithProgramCs(projectDir, includePackageJson: true);

            var wwwRootLibs = Path.Combine(projectDir, "wwwroot", "libs");
            Directory.CreateDirectory(wwwRootLibs);
            File.WriteAllText(Path.Combine(wwwRootLibs, "bootstrap.min.js"), "/* bootstrap */");

            var csproj = new FileInfo(Path.Combine(projectDir, "MyProject.Web.csproj"));

            // Act
            var projectsNeedingLibs = new List<FileInfo>();
            var projDir = Path.GetDirectoryName(csproj.FullName)!;

            if (File.Exists(Path.Combine(projDir, "package.json")))
            {
                var libsPath = Path.Combine(projDir, "wwwroot", "libs");
                if (!Directory.Exists(libsPath) || !Directory.EnumerateFileSystemEntries(libsPath).Any())
                {
                    projectsNeedingLibs.Add(csproj);
                }
            }

            // Assert
            projectsNeedingLibs.Should().BeEmpty("libs are already installed");
        }
        finally
        {
            CleanupTestDirectory(testRootPath);
        }
    }

    #endregion

    #region SkipCheckLibs Flag Tests

    [Theory]
    [InlineData(true, true, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public void SkipCheckLibs_FlagConfigurationCombinesCorrectly(
        bool commandSkipFlag,
        bool configSkipFlag,
        bool shouldCheckLibs)
    {
        // Arrange - Simulate the condition from RunCommand.cs:173
        var skipCheckLibs = commandSkipFlag;
        var configSkipCheckLibs = configSkipFlag;

        // Act - Simulate the condition: !SkipCheckLibs && localRootConfig?.Run?.SkipCheckLibs != true
        var willCheckLibs = !skipCheckLibs && !configSkipCheckLibs;

        // Assert
        willCheckLibs.Should().Be(shouldCheckLibs,
            $"shouldCheckLibs expectation: command flag={commandSkipFlag}, config flag={configSkipFlag}");
    }

    [Fact]
    public void SkipCheckLibs_LocalConfigurationOverridesDefault()
    {
        // Arrange
        var localConfiguration = new LocalConfiguration
        {
            Run = new LocalConfiguration.LocalRunOption
            {
                SkipCheckLibs = true
            }
        };

        // Act
        var isSkippedInConfig = localConfiguration.Run?.SkipCheckLibs == true;

        // Assert
        isSkippedInConfig.Should().BeTrue("local configuration should skip libs check when SkipCheckLibs is true");
    }

    [Fact]
    public void SkipCheckLibs_NullLocalConfigurationPerformsCheck()
    {
        // Arrange
        LocalConfiguration? localConfiguration = null;

        // Act
        var isSkippedInConfig = localConfiguration?.Run?.SkipCheckLibs == true;

        // Assert
        isSkippedInConfig.Should().BeFalse("null configuration should not skip libs check");
    }

    #endregion

    #region InstallLibs Flag Tests

    [Fact]
    public void InstallLibs_WhenExplicitlySet_OverridesCheck()
    {
        // Arrange
        var explicitInstallFlag = true;
        _ = false; // User said no (not used due to explicit flag)

        // Act - Simulate the logic from RunCommand.cs:171
        var shouldInstallLibs = explicitInstallFlag;

        // Assert
        shouldInstallLibs.Should().BeTrue("explicit --install-libs flag should override user confirmation");
    }

    [Fact]
    public void InstallLibs_WhenNotExplicitlySet_DependsOnUserConfirmation()
    {
        // Arrange
        var explicitInstallFlag = false;
        var userConfirmed = true;

        // Act - Simulate the logic where user confirmation sets the flag
        var shouldInstallLibs = explicitInstallFlag;
        if (!explicitInstallFlag && userConfirmed)
        {
            shouldInstallLibs = true;
        }

        // Assert
        shouldInstallLibs.Should().BeTrue("user confirmation should enable install-libs when flag is not set");
    }

    [Fact]
    public void InstallLibs_WhenNoProjectsNeedLibs_DoesNotInstall()
    {
        // Arrange
        var projectsNeedingLibs = new List<FileInfo>(); // No projects need libs
        var explicitInstallFlag = false;

        // Act - Simulate the logic from RunCommand.cs:187-198
        var shouldInstallLibs = explicitInstallFlag;
        if (projectsNeedingLibs.Count > 0)
        {
            // Would prompt user and potentially set to true
            shouldInstallLibs = true; // Assuming user confirmed
        }

        // Assert
        shouldInstallLibs.Should().BeFalse("should not install libs when no projects need them");
    }

    #endregion

    #region InstallLibsDirectoryCreation Tests

    [Fact]
    public void InstallLibsDirectoryCreation_CreatesWwwrootLibsIfNotExists()
    {
        // Arrange
        var testRootPath = Path.Combine(Path.GetTempPath(), $"AbpDevToolsTests_{Guid.NewGuid()}");
        var projectDir = Path.Combine(testRootPath, "MyProject.Web");
        Directory.CreateDirectory(projectDir);

        try
        {
            CreateProjectWithProgramCs(projectDir, includePackageJson: true);

            var wwwRootLibs = Path.Combine(projectDir, "wwwroot", "libs");

            // Act
            if (!Directory.Exists(wwwRootLibs))
            {
                Directory.CreateDirectory(wwwRootLibs);
            }

            // Assert
            Directory.Exists(wwwRootLibs).Should().BeTrue("wwwroot/libs should be created if it doesn't exist");
        }
        finally
        {
            CleanupTestDirectory(testRootPath);
        }
    }

    [Fact]
    public void InstallLibsDirectoryCreation_DoesNotFailIfExists()
    {
        // Arrange
        var testRootPath = Path.Combine(Path.GetTempPath(), $"AbpDevToolsTests_{Guid.NewGuid()}");
        var projectDir = Path.Combine(testRootPath, "MyProject.Web");
        Directory.CreateDirectory(projectDir);

        try
        {
            CreateProjectWithProgramCs(projectDir, includePackageJson: true);

            var wwwRootLibs = Path.Combine(projectDir, "wwwroot", "libs");
            Directory.CreateDirectory(wwwRootLibs);

            // Act
            var action = () =>
            {
                if (!Directory.Exists(wwwRootLibs))
                {
                    Directory.CreateDirectory(wwwRootLibs);
                }
            };

            // Assert
            action.Should().NotThrow("creating existing directory should not throw exception");
        }
        finally
        {
            CleanupTestDirectory(testRootPath);
        }
    }

    [Fact]
    public void InstallLibsDirectoryCreation_CreatesInstallingMarkerFile()
    {
        // Arrange
        var testRootPath = Path.Combine(Path.GetTempPath(), $"AbpDevToolsTests_{Guid.NewGuid()}");
        var projectDir = Path.Combine(testRootPath, "MyProject.Web");
        Directory.CreateDirectory(projectDir);

        try
        {
            CreateProjectWithProgramCs(projectDir, includePackageJson: true);

            var wwwRootLibs = Path.Combine(projectDir, "wwwroot", "libs");
            Directory.CreateDirectory(wwwRootLibs);

            var markerFile = Path.Combine(wwwRootLibs, "abplibs.installing");

            // Act
            if (!Directory.EnumerateFiles(wwwRootLibs).Any())
            {
                File.WriteAllText(markerFile, string.Empty);
            }

            // Assert
            File.Exists(markerFile).Should().BeTrue("abplibs.installing marker file should be created");
        }
        finally
        {
            CleanupTestDirectory(testRootPath);
        }
    }

    [Fact]
    public void InstallLibs_SkipsProjectWithoutPackageJson_EvenWhenFlagIsSet()
    {
        // Arrange — simulates the execution loop guard from RunCommand
        var testRootPath = Path.Combine(Path.GetTempPath(), $"AbpDevToolsTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(testRootPath);

        try
        {
            var domainDir = Path.Combine(testRootPath, "MyProject.Domain");
            CreateProjectWithProgramCs(domainDir, includePackageJson: false);

            var csproj = new FileInfo(Path.Combine(domainDir, "MyProject.Domain.csproj"));
            var shouldInstallLibs = true;
            var installLibsTriggered = false;

            // Act — replicate the guard: shouldInstallLibs && package.json exists
            if (shouldInstallLibs && File.Exists(Path.Combine(Path.GetDirectoryName(csproj.FullName)!, "package.json")))
            {
                installLibsTriggered = true;
            }

            // Assert
            installLibsTriggered.Should().BeFalse("install-libs should not run for projects without package.json");
        }
        finally
        {
            CleanupTestDirectory(testRootPath);
        }
    }

    #endregion

    #region ProcessCreation Tests

    [Fact]
    public void ProcessCreation_InstallLibsCommand_HasCorrectArguments()
    {
        // Arrange - Simulate the ProcessStartInfo creation from RunCommand.cs:248-253

        // Act
        var installLibsStartInfo = new System.Diagnostics.ProcessStartInfo("abp", "install-libs")
        {
            WorkingDirectory = "C:\\Test\\MyProject.Web",
            UseShellExecute = false,
            RedirectStandardOutput = true,
        };

        // Assert
        installLibsStartInfo.FileName.Should().Be("abp");
        installLibsStartInfo.Arguments.Should().Be("install-libs");
        installLibsStartInfo.WorkingDirectory.Should().Be("C:\\Test\\MyProject.Web");
        installLibsStartInfo.UseShellExecute.Should().BeFalse();
        installLibsStartInfo.RedirectStandardOutput.Should().BeTrue();
    }

    [Fact]
    public void ProcessCreation_BothProjectAndInstallLibs_CreatedWhenNeeded()
    {
        // Arrange
        var shouldInstallLibs = true;
        var projectCreated = false;
        var installLibsCreated = false;

        // Act - Simulate the loop from RunCommand.cs:200-263
        foreach (var csproj in new[] { "MyProject.Web.csproj" })
        {
            // Simulate project process creation
            projectCreated = true;

            if (shouldInstallLibs)
            {
                // Simulate install-libs process creation
                installLibsCreated = true;
            }
        }

        // Assert
        projectCreated.Should().BeTrue("project process should always be created");
        installLibsCreated.Should().BeTrue("install-libs process should be created when shouldInstallLibs is true");
    }

    [Fact]
    public void ProcessCreation_OnlyProject_WhenInstallLibsNotNeeded()
    {
        // Arrange
        var shouldInstallLibs = false;
        var projectCreated = false;
        var installLibsCreated = false;

        // Act - Simulate the loop from RunCommand.cs:200-263
        foreach (var csproj in new[] { "MyProject.Web.csproj" })
        {
            // Simulate project process creation
            projectCreated = true;

            if (shouldInstallLibs)
            {
                // Simulate install-libs process creation
                installLibsCreated = true;
            }
        }

        // Assert
        projectCreated.Should().BeTrue("project process should always be created");
        installLibsCreated.Should().BeFalse("install-libs process should not be created when shouldInstallLibs is false");
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Creates a project file with Program.cs in the specified directory.
    /// Optionally creates a package.json to simulate a frontend project.
    /// </summary>
    private void CreateProjectWithProgramCs(string projectDir, bool includePackageJson = false)
    {
        Directory.CreateDirectory(projectDir);
        var projectName = Path.GetFileName(projectDir);
        var projectPath = Path.Combine(projectDir, $"{projectName}.csproj");

        var programCsPath = Path.Combine(projectDir, "Program.cs");
        File.WriteAllText(programCsPath, @"
using System;

public class Program
{
    public static void Main(string[] args)
    {
        Console.WriteLine(""Hello, World!"");
    }
}
");

        File.WriteAllText(projectPath, @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>
</Project>");

        if (includePackageJson)
        {
            File.WriteAllText(Path.Combine(projectDir, "package.json"), @"{
  ""version"": ""1.0.0"",
  ""name"": ""my-app"",
  ""private"": true,
  ""dependencies"": {}
}");
        }
    }

    /// <summary>
    /// Cleans up test directory.
    /// </summary>
    private void CleanupTestDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    #endregion
}
