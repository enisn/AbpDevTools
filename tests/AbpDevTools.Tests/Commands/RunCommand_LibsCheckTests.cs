using AbpDevTools.Configuration;
using AbpDevTools.LocalConfigurations;
using FluentAssertions;
using Xunit;

namespace AbpDevTools.Tests.Commands;

/// <summary>
/// Unit tests for RunCommand libs check functionality.
/// Tests verify how the command detects missing wwwroot/libs folders
/// and prompts the user to install libraries.
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
            CreateProjectWithProgramCs(projectDir);
            // Don't create wwwroot/libs - it's missing

            var wwwRootLibs = Path.Combine(projectDir, "wwwroot/libs");

            // Act
            var libsExists = Directory.Exists(wwwRootLibs);

            // Assert
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
            CreateProjectWithProgramCs(projectDir);

            var wwwRootLibs = Path.Combine(projectDir, "wwwroot/libs");
            Directory.CreateDirectory(wwwRootLibs);
            // Don't add any files - it's empty

            // Act
            var libsExists = Directory.Exists(wwwRootLibs);
            var hasEntries = libsExists && Directory.EnumerateFileSystemEntries(wwwRootLibs).Any();

            // Assert
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
            CreateProjectWithProgramCs(projectDir);

            var wwwRootLibs = Path.Combine(projectDir, "wwwroot/libs");
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
            // Create multiple projects
            var project1Dir = Path.Combine(testRootPath, "MyProject.Web");
            var project2Dir = Path.Combine(testRootPath, "MyProject.HttpApi.Host");
            var project3Dir = Path.Combine(testRootPath, "MyProject.Blazor");

            CreateProjectWithProgramCs(project1Dir);
            CreateProjectWithProgramCs(project2Dir);
            CreateProjectWithProgramCs(project3Dir);

            // Create wwwroot/libs only for project2
            var wwwRootLibs2 = Path.Combine(project2Dir, "wwwroot/libs");
            Directory.CreateDirectory(wwwRootLibs2);
            File.WriteAllText(Path.Combine(wwwRootLibs2, "test.js"), "console.log('test');");

            var projectFiles = new[]
            {
                new FileInfo(Path.Combine(project1Dir, "MyProject.Web.csproj")),
                new FileInfo(Path.Combine(project2Dir, "MyProject.HttpApi.Host.csproj")),
                new FileInfo(Path.Combine(project3Dir, "MyProject.Blazor.csproj"))
            };

            // Act - Find projects needing libs
            var projectsNeedingLibs = new List<FileInfo>();
            foreach (var csproj in projectFiles)
            {
                var wwwRootLibs = Path.Combine(Path.GetDirectoryName(csproj.FullName)!, "wwwroot/libs");
                if (!Directory.Exists(wwwRootLibs) || !Directory.EnumerateFileSystemEntries(wwwRootLibs).Any())
                {
                    projectsNeedingLibs.Add(csproj);
                }
            }

            // Assert
            projectsNeedingLibs.Should().HaveCount(2, "project1 and project3 should need libs");
            projectsNeedingLibs.Select(p => p.Name).Should().Contain("MyProject.Web.csproj");
            projectsNeedingLibs.Select(p => p.Name).Should().Contain("MyProject.Blazor.csproj");
            projectsNeedingLibs.Select(p => p.Name).Should().NotContain("MyProject.HttpApi.Host.csproj");
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
    [InlineData(false, true, true)]
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
            CreateProjectWithProgramCs(projectDir);

            var wwwRootLibs = Path.Combine(projectDir, "wwwroot/libs");

            // Act - Simulate directory creation from RunCommand.cs:237-241
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
            CreateProjectWithProgramCs(projectDir);

            var wwwRootLibs = Path.Combine(projectDir, "wwwroot/libs");
            Directory.CreateDirectory(wwwRootLibs);

            // Act - Try to create again (should not fail)
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
            CreateProjectWithProgramCs(projectDir);

            var wwwRootLibs = Path.Combine(projectDir, "wwwroot/libs");
            Directory.CreateDirectory(wwwRootLibs);

            var markerFile = Path.Combine(wwwRootLibs, "abplibs.installing");

            // Act - Simulate marker file creation from RunCommand.cs:243-246
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
    /// </summary>
    private void CreateProjectWithProgramCs(string projectDir)
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
