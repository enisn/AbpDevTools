using AbpDevTools;
using FluentAssertions;
using Xunit;

namespace AbpDevTools.Tests.Services;

public class FileExplorerTests : IDisposable
{
    private readonly string _testRootPath;

    public FileExplorerTests()
    {
        _testRootPath = Path.Combine(Path.GetTempPath(), $"FileExplorerTests_{Guid.NewGuid()}");
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

    [Fact]
    public void FindDescendants_ShouldSkipGitDirectory()
    {
        // Arrange
        var validProjectDirectory = Path.Combine(_testRootPath, "src", "ValidProject");
        Directory.CreateDirectory(validProjectDirectory);
        var validProjectPath = Path.Combine(validProjectDirectory, "ValidProject.csproj");
        File.WriteAllText(validProjectPath, "<Project />");

        var gitLogDirectory = Path.Combine(_testRootPath, ".git", "logs", "refs", "remotes", "origin");
        Directory.CreateDirectory(gitLogDirectory);
        File.WriteAllText(Path.Combine(gitLogDirectory, "ValidProject.csproj"), string.Empty);

        var fileExplorer = new FileExplorer();

        // Act
        var projectFiles = fileExplorer.FindDescendants(_testRootPath, "*.csproj").ToList();

        // Assert
        projectFiles.Should().ContainSingle();
        projectFiles[0].Should().Be(validProjectPath);
    }
}
