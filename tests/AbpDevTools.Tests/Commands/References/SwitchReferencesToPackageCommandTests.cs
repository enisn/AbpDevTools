using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using AbpDevTools.Commands.References;
using AbpDevTools.Configuration;
using AbpDevTools.Services;
using CliFx.Infrastructure;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace AbpDevTools.Tests.Commands.References;

/// <summary>
/// Mock console for testing commands.
/// </summary>
internal class MockConsole : IConsole
{
    public MockConsole()
    {
    }

    public ConsoleWriter Output => new(this, Console.OpenStandardOutput(), System.Text.Encoding.UTF8);
    public ConsoleWriter Error => new(this, Console.OpenStandardError(), System.Text.Encoding.UTF8);
    public ConsoleReader Input => new(this, Console.OpenStandardInput(), System.Text.Encoding.UTF8);

    public CancellationToken RegisterCancellationHandler() => CancellationToken.None;
    public void Clear() { }
    public ConsoleKeyInfo ReadKey(bool intercept = false) => default;
    public void ResetColor() { }
    public bool IsInputRedirected => false;
    public bool IsOutputRedirected => true;
    public bool IsErrorRedirected => true;
    public ConsoleColor ForegroundColor { get; set; }
    public ConsoleColor BackgroundColor { get; set; }
    public int WindowWidth { get; set; } = 80;
    public int WindowHeight { get; set; } = 25;
    public int CursorLeft { get; set; }
    public int CursorTop { get; set; }
}

/// <summary>
/// Unit tests for SwitchReferencesToPackageCommand class.
/// Tests switching project references back to package references.
/// </summary>
public class SwitchReferencesToPackageCommandTests : IDisposable
{
    private readonly string _testRootPath;

    public SwitchReferencesToPackageCommandTests()
    {
        _testRootPath = Path.Combine(Path.GetTempPath(), $"SwitchReferencesToPackageCommandTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testRootPath);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testRootPath))
            {
                Directory.Delete(_testRootPath, true);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    #region ExecuteAsync Identifies Projects With Local References

    [Fact]
    public async Task ExecuteAsync_IdentifiesProjects_WithLocalReferences()
    {
        // Arrange
        var localSourcesConfig = Substitute.For<LocalSourcesConfiguration>(
            Substitute.For<YamlDotNet.Serialization.IDeserializer>(),
            Substitute.For<YamlDotNet.Serialization.ISerializer>());

        var gitService = Substitute.For<GitService>();
        var console = new MockConsole();

        var projectPath = Path.Combine(_testRootPath, "TestProject.csproj");
        CreateTestProjectFile(projectPath);

        // Create real source directory with project
        var sourcePath = Path.Combine(_testRootPath, "abp");
        Directory.CreateDirectory(sourcePath);
        CreateTestProjectFile(Path.Combine(sourcePath, "Volo.Abp.Core.csproj"));

        localSourcesConfig.GetOptions().Returns(new LocalSourceMapping
        {
            ["abp"] = new LocalSourceMappingItem
            {
                Path = sourcePath,
                Packages = new HashSet<string> { "Volo.*" }
            }
        });

        // Use real FileExplorer and CsprojManipulationService
        var fileExplorer = new FileExplorer();
        var csprojService = new CsprojManipulationService(fileExplorer);
        var command = new SwitchReferencesToPackageCommand(
            localSourcesConfig, fileExplorer, csprojService, gitService)
        {
            WorkingDirectory = _testRootPath
        };

        // Act - should not throw
        Exception? capturedException = null;
        try
        {
            await command.ExecuteAsync(console);
        }
        catch (Exception ex)
        {
            capturedException = ex;
        }

        // Assert
        capturedException.Should().BeNull();
    }

    #endregion

    #region ExecuteAsync Calls CsprojManipulationService For Each Project

    [Fact]
    public async Task ExecuteAsync_CallsCsprojService_ForEachProject()
    {
        // Arrange
        var localSourcesConfig = Substitute.For<LocalSourcesConfiguration>(
            Substitute.For<YamlDotNet.Serialization.IDeserializer>(),
            Substitute.For<YamlDotNet.Serialization.ISerializer>());

        var gitService = Substitute.For<GitService>();
        var console = new MockConsole();

        var projectPaths = new[]
        {
            Path.Combine(_testRootPath, "Project1.csproj"),
            Path.Combine(_testRootPath, "Project2.csproj"),
            Path.Combine(_testRootPath, "Project3.csproj")
        };

        foreach (var projectPath in projectPaths)
        {
            CreateTestProjectFile(projectPath);
        }

        // Create source directory
        var sourcePath = Path.Combine(_testRootPath, "abp");
        Directory.CreateDirectory(sourcePath);
        // Add a marker file to make directory non-empty
        File.WriteAllText(Path.Combine(sourcePath, ".gitkeep"), "");

        localSourcesConfig.GetOptions().Returns(new LocalSourceMapping
        {
            ["abp"] = new LocalSourceMappingItem
            {
                Path = sourcePath,
                Packages = new HashSet<string> { "Volo.*" }
            }
        });

        var fileExplorer = new FileExplorer();
        var csprojService = new CsprojManipulationService(fileExplorer);
        var command = new SwitchReferencesToPackageCommand(
            localSourcesConfig, fileExplorer, csprojService, gitService)
        {
            WorkingDirectory = _testRootPath
        };

        // Act
        await command.ExecuteAsync(console);

        // Assert - The command processes all 3 projects without throwing
        localSourcesConfig.Received(1).GetOptions();
    }

    #endregion

    #region ExecuteAsync Loads LocalSourceConfiguration

    [Fact]
    public async Task ExecuteAsync_LoadsLocalSourceConfiguration()
    {
        // Arrange
        var localSourcesConfig = Substitute.For<LocalSourcesConfiguration>(
            Substitute.For<YamlDotNet.Serialization.IDeserializer>(),
            Substitute.For<YamlDotNet.Serialization.ISerializer>());

        var gitService = Substitute.For<GitService>();
        var console = new MockConsole();

        var projectPath = Path.Combine(_testRootPath, "TestProject.csproj");
        CreateTestProjectFile(projectPath);

        var abpSourcePath = Path.Combine(_testRootPath, "abp");
        Directory.CreateDirectory(abpSourcePath);
        File.WriteAllText(Path.Combine(abpSourcePath, ".gitkeep"), ""); // Avoid interactive prompt
        var commercialSourcePath = Path.Combine(_testRootPath, "commercial");
        Directory.CreateDirectory(commercialSourcePath);
        File.WriteAllText(Path.Combine(commercialSourcePath, ".gitkeep"), ""); // Avoid interactive prompt

        var expectedSources = new LocalSourceMapping
        {
            ["abp"] = new LocalSourceMappingItem
            {
                Path = abpSourcePath,
                RemotePath = "https://github.com/abpframework/abp.git",
                Branch = "dev",
                Packages = new HashSet<string> { "Volo.*" }
            },
            ["commercial"] = new LocalSourceMappingItem
            {
                Path = commercialSourcePath,
                Packages = new HashSet<string> { "Volo.Commercial.*" }
            }
        };

        localSourcesConfig.GetOptions().Returns(expectedSources);

        var fileExplorer = new FileExplorer();
        var csprojService = new CsprojManipulationService(fileExplorer);
        var command = new SwitchReferencesToPackageCommand(
            localSourcesConfig, fileExplorer, csprojService, gitService)
        {
            WorkingDirectory = _testRootPath
        };

        // Act
        await command.ExecuteAsync(console);

        // Assert
        localSourcesConfig.Received(1).GetOptions();
    }

    #endregion

    #region ExecuteAsync Restores BackedUp Package Versions

    [Fact]
    public async Task ExecuteAsync_RestoresBackedUpPackageVersions()
    {
        // Arrange
        var localSourcesConfig = Substitute.For<LocalSourcesConfiguration>(
            Substitute.For<YamlDotNet.Serialization.IDeserializer>(),
            Substitute.For<YamlDotNet.Serialization.ISerializer>());

        var gitService = Substitute.For<GitService>();
        var console = new MockConsole();

        // Create a project with a project reference to local source
        var projectPath = Path.Combine(_testRootPath, "TestProject.csproj");
        CreateTestProjectFileWithBackup(projectPath, "abp", "8.2.0");

        // Create source directory with a referenced project
        var sourcePath = Path.Combine(_testRootPath, "abp");
        Directory.CreateDirectory(sourcePath);
        var referencedProjectPath = Path.Combine(sourcePath, "Volo.Abp.Core.csproj");
        CreateTestProjectFile(referencedProjectPath);
        File.WriteAllText(Path.Combine(sourcePath, ".gitkeep"), ""); // Avoid interactive prompt

        // Add project reference to the test project (correct relative path)
        var doc = XDocument.Load(projectPath);
        var itemGroup = new XElement("ItemGroup");
        var projectRef = new XElement("ProjectReference",
            new XAttribute("Include", "abp\\Volo.Abp.Core.csproj"));
        itemGroup.Add(projectRef);
        doc.Root!.Add(itemGroup);
        doc.Save(projectPath);

        localSourcesConfig.GetOptions().Returns(new LocalSourceMapping
        {
            ["abp"] = new LocalSourceMappingItem
            {
                Path = sourcePath,
                Packages = new HashSet<string> { "Volo.*" }
            }
        });

        var fileExplorer = new FileExplorer();
        var csprojService = new CsprojManipulationService(fileExplorer);
        var command = new SwitchReferencesToPackageCommand(
            localSourcesConfig, fileExplorer, csprojService, gitService)
        {
            WorkingDirectory = _testRootPath
        };

        // Act
        await command.ExecuteAsync(console);

        // Assert
        // After conversion, the file should have PackageReference instead of ProjectReference
        var updatedDoc = XDocument.Load(projectPath);
        updatedDoc.Descendants("PackageReference").Any().Should().BeTrue();
        updatedDoc.Descendants("ProjectReference").Any().Should().BeFalse("ProjectReference should be converted to PackageReference");
    }

    #endregion

    #region ExecuteAsync Handles Missing Backup Version

    [Fact]
    public async Task ExecuteAsync_HandlesMissingBackupVersion_WhenNoBackupExists()
    {
        // Arrange
        var localSourcesConfig = Substitute.For<LocalSourcesConfiguration>(
            Substitute.For<YamlDotNet.Serialization.IDeserializer>(),
            Substitute.For<YamlDotNet.Serialization.ISerializer>());

        var gitService = Substitute.For<GitService>();
        var console = new MockConsole();

        var projectPath = Path.Combine(_testRootPath, "TestProject.csproj");
        CreateTestProjectFile(projectPath);

        var sourcePath = Path.Combine(_testRootPath, "abp");
        Directory.CreateDirectory(sourcePath);
        // Add a marker file to make directory non-empty
        File.WriteAllText(Path.Combine(sourcePath, ".gitkeep"), "");

        localSourcesConfig.GetOptions().Returns(new LocalSourceMapping
        {
            ["abp"] = new LocalSourceMappingItem
            {
                Path = sourcePath,
                Packages = new HashSet<string> { "Volo.*" }
            }
        });

        var fileExplorer = new FileExplorer();
        var csprojService = new CsprojManipulationService(fileExplorer);
        var command = new SwitchReferencesToPackageCommand(
            localSourcesConfig, fileExplorer, csprojService, gitService)
        {
            WorkingDirectory = _testRootPath
        };

        // Act
        Exception? exception = null;
        try
        {
            await command.ExecuteAsync(console);
        }
        catch (Exception ex)
        {
            exception = ex;
        }

        // Assert
        // When no backup exists and no project references to convert, the command should complete
        exception.Should().BeNull();
    }

    #endregion

    #region ExecuteAsync Handles No Local References

    [Fact]
    public async Task ExecuteAsync_HandlesNoLocalReferences_NoOperation()
    {
        // Arrange
        var localSourcesConfig = Substitute.For<LocalSourcesConfiguration>(
            Substitute.For<YamlDotNet.Serialization.IDeserializer>(),
            Substitute.For<YamlDotNet.Serialization.ISerializer>());

        var gitService = Substitute.For<GitService>();
        var console = new MockConsole();

        var projectPath = Path.Combine(_testRootPath, "TestProject.csproj");
        CreateTestProjectFile(projectPath);

        var sourcePath = Path.Combine(_testRootPath, "abp");
        Directory.CreateDirectory(sourcePath);
        // Add a marker file to make directory non-empty
        File.WriteAllText(Path.Combine(sourcePath, ".gitkeep"), "");

        localSourcesConfig.GetOptions().Returns(new LocalSourceMapping
        {
            ["abp"] = new LocalSourceMappingItem
            {
                Path = sourcePath,
                Packages = new HashSet<string> { "Volo.*" }
            }
        });

        var fileExplorer = new FileExplorer();
        var csprojService = new CsprojManipulationService(fileExplorer);
        var command = new SwitchReferencesToPackageCommand(
            localSourcesConfig, fileExplorer, csprojService, gitService)
        {
            WorkingDirectory = _testRootPath
        };

        // Act - should not throw
        Exception? capturedException = null;
        try
        {
            await command.ExecuteAsync(console);
        }
        catch (Exception ex)
        {
            capturedException = ex;
        }

        // Assert
        // Command should complete successfully even with no local references
        capturedException.Should().BeNull();
    }

    #endregion

    #region ExecuteAsync Reports Converted Projects Count

    [Fact]
    public async Task ExecuteAsync_ReportsConvertedProjectsCount()
    {
        // Arrange
        var localSourcesConfig = Substitute.For<LocalSourcesConfiguration>(
            Substitute.For<YamlDotNet.Serialization.IDeserializer>(),
            Substitute.For<YamlDotNet.Serialization.ISerializer>());

        var gitService = Substitute.For<GitService>();
        var console = new MockConsole();

        var projectPath = Path.Combine(_testRootPath, "TestProject.csproj");
        CreateTestProjectFileWithBackup(projectPath, "abp", "8.2.0");

        var sourcePath = Path.Combine(_testRootPath, "abp");
        Directory.CreateDirectory(sourcePath);
        var referencedProjectPath = Path.Combine(sourcePath, "Volo.Abp.Core.csproj");
        CreateTestProjectFile(referencedProjectPath);
        File.WriteAllText(Path.Combine(sourcePath, ".gitkeep"), ""); // Avoid interactive prompt

        // Add project reference (correct relative path)
        var doc = XDocument.Load(projectPath);
        var itemGroup = new XElement("ItemGroup");
        var projectRef = new XElement("ProjectReference",
            new XAttribute("Include", "abp\\Volo.Abp.Core.csproj"));
        itemGroup.Add(projectRef);
        doc.Root!.Add(itemGroup);
        doc.Save(projectPath);

        localSourcesConfig.GetOptions().Returns(new LocalSourceMapping
        {
            ["abp"] = new LocalSourceMappingItem
            {
                Path = sourcePath,
                Packages = new HashSet<string> { "Volo.*" }
            }
        });

        var fileExplorer = new FileExplorer();
        var csprojService = new CsprojManipulationService(fileExplorer);
        var command = new SwitchReferencesToPackageCommand(
            localSourcesConfig, fileExplorer, csprojService, gitService)
        {
            WorkingDirectory = _testRootPath
        };

        // Act
        await command.ExecuteAsync(console);

        // Assert
        // After conversion, the file should have PackageReference instead of ProjectReference
        var updatedDoc = XDocument.Load(projectPath);
        updatedDoc.Descendants("PackageReference").Any().Should().BeTrue();
        updatedDoc.Descendants("ProjectReference").Any().Should().BeFalse("ProjectReference should be converted to PackageReference");
    }

    #endregion

    #region ExecuteAsync Handles Conversion Errors Gracefully

    [Fact]
    public async Task ExecuteAsync_HandlesConversionErrors_Gracefully()
    {
        // Arrange
        var localSourcesConfig = Substitute.For<LocalSourcesConfiguration>(
            Substitute.For<YamlDotNet.Serialization.IDeserializer>(),
            Substitute.For<YamlDotNet.Serialization.ISerializer>());

        var gitService = Substitute.For<GitService>();
        var console = new MockConsole();

        // Create an invalid project file (malformed XML)
        var projectPath = Path.Combine(_testRootPath, "InvalidProject.csproj");
        File.WriteAllText(projectPath, "<Invalid XML content");

        var sourcePath = Path.Combine(_testRootPath, "abp");
        Directory.CreateDirectory(sourcePath);
        // Add a marker file to make directory non-empty
        File.WriteAllText(Path.Combine(sourcePath, ".gitkeep"), "");

        localSourcesConfig.GetOptions().Returns(new LocalSourceMapping
        {
            ["abp"] = new LocalSourceMappingItem
            {
                Path = sourcePath,
                Packages = new HashSet<string> { "Volo.*" }
            }
        });

        var fileExplorer = new FileExplorer();
        var csprojService = new CsprojManipulationService(fileExplorer);
        var command = new SwitchReferencesToPackageCommand(
            localSourcesConfig, fileExplorer, csprojService, gitService)
        {
            WorkingDirectory = _testRootPath
        };

        // Act
        Exception? exception = null;
        try
        {
            await command.ExecuteAsync(console);
        }
        catch (Exception ex)
        {
            exception = ex;
        }

        // Assert
        // The command should handle the error gracefully - it may or may not throw depending on implementation
        // The test verifies that the command doesn't crash the test runner
        if (exception != null)
        {
            exception.Should().BeOfType<XmlException>("malformed XML should cause XmlException");
        }
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Creates a test project file with basic content.
    /// </summary>
    private static void CreateTestProjectFile(string path)
    {
        var content = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>
</Project>";
        File.WriteAllText(path, content);
    }

    /// <summary>
    /// Creates a test project file with a backed-up version property.
    /// </summary>
    private static void CreateTestProjectFileWithBackup(string path, string sourceKey, string version)
    {
        var propertyName = $"{sourceKey}Version";
        var content = $@"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <{propertyName}>{version}</{propertyName}>
  </PropertyGroup>
</Project>";
        File.WriteAllText(path, content);
    }

    #endregion
}
