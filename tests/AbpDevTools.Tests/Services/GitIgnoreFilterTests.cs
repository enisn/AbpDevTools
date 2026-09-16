using AbpDevTools.Tests.Helpers;
using FluentAssertions;
using Xunit;

namespace AbpDevTools.Tests.Services;

public class GitIgnoreFilterTests : IDisposable
{
    private readonly string _testRootPath;

    public GitIgnoreFilterTests()
    {
        _testRootPath = Path.Combine(Path.GetTempPath(), $"GitIgnoreFilterTests_{Guid.NewGuid()}");
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
    public void ExcludeIgnoredFiles_InGitRepository_KeepsTrackedAndUntrackedFilesButSkipsIgnoredOnes()
    {
        // Arrange
        var trackedProject = CreateFile("src", "Foo", "Foo.csproj");
        var gitIgnoredProject = CreateFile("samples", "Bar", "Bar.csproj");
        var excludedProject = CreateFile("local", "Baz", "Baz.csproj");
        File.WriteAllText(Path.Combine(_testRootPath, ".gitignore"), "samples/" + Environment.NewLine);

        GitTestHelper.InitRepository(_testRootPath);
        GitTestHelper.AddExcludePattern(_testRootPath, "local/");
        GitTestHelper.CommitAll(_testRootPath);
        var untrackedProject = CreateFile("src", "Qux", "Qux.csproj");

        // Act
        var result = GitIgnoreFilter.ExcludeIgnoredFiles(
            _testRootPath,
            new[] { trackedProject, gitIgnoredProject, excludedProject, untrackedProject });

        // Assert
        result.Should().BeEquivalentTo(new[] { trackedProject, untrackedProject });
    }

    [Fact]
    public void ExcludeIgnoredFiles_InGitRepository_SkipsFilesInNestedWorktrees()
    {
        // Arrange
        var project = CreateFile("src", "Foo", "Foo.csproj");
        GitTestHelper.InitRepository(_testRootPath);
        GitTestHelper.CommitAll(_testRootPath);
        GitTestHelper.Run(_testRootPath, "worktree", "add", "-q", "worktrees/bar-feature", "-b", "bar-feature");

        var worktreeProject = Path.Combine(_testRootPath, "worktrees", "bar-feature", "src", "Foo", "Foo.csproj");
        File.Exists(worktreeProject).Should().BeTrue();

        // Act
        var result = GitIgnoreFilter.ExcludeIgnoredFiles(_testRootPath, new[] { project, worktreeProject });

        // Assert
        result.Should().ContainSingle()
            .Which.Should().Be(project, "a worktree is a separate checkout even when it isn't ignored");
    }

    [Fact]
    public void ExcludeIgnoredFiles_InGitRepository_KeepsFilesInSubmodules()
    {
        // Arrange
        var submoduleSourcePath = Path.Combine(_testRootPath, "bar-source");
        CreateFile("bar-source", "Bar.csproj");
        GitTestHelper.InitRepository(submoduleSourcePath);
        GitTestHelper.CommitAll(submoduleSourcePath);

        var repositoryPath = Path.Combine(_testRootPath, "foo");
        var project = CreateFile("foo", "Foo.csproj");
        GitTestHelper.InitRepository(repositoryPath);
        GitTestHelper.Run(repositoryPath, "submodule", "add", "-q", submoduleSourcePath, "external/bar");
        GitTestHelper.CommitAll(repositoryPath);

        var submoduleProject = Path.Combine(repositoryPath, "external", "bar", "Bar.csproj");

        // Act
        var result = GitIgnoreFilter.ExcludeIgnoredFiles(repositoryPath, new[] { project, submoduleProject });

        // Assert
        result.Should().BeEquivalentTo(new[] { project, submoduleProject });
    }

    [Fact]
    public void ExcludeIgnoredFiles_WhenGitListsNoFiles_KeepsAllFiles()
    {
        // Arrange
        var project = CreateFile("samples", "Foo", "Foo.csproj");
        File.WriteAllText(Path.Combine(_testRootPath, ".gitignore"), "samples/" + Environment.NewLine);
        GitTestHelper.InitRepository(_testRootPath);
        GitTestHelper.CommitAll(_testRootPath);

        // Act
        var result = GitIgnoreFilter.ExcludeIgnoredFiles(Path.Combine(_testRootPath, "samples"), new[] { project });

        // Assert
        result.Should().ContainSingle()
            .Which.Should().Be(project, "running inside an ignored folder should still find its projects");
    }

    [Fact]
    public void ExcludeIgnoredFiles_OutsideGitRepository_KeepsAllFiles()
    {
        // Arrange
        var project = CreateFile("samples", "Foo", "Foo.csproj");
        File.WriteAllText(Path.Combine(_testRootPath, ".gitignore"), "samples/" + Environment.NewLine);

        // Act
        var result = GitIgnoreFilter.ExcludeIgnoredFiles(_testRootPath, new[] { project });

        // Assert
        result.Should().ContainSingle()
            .Which.Should().Be(project, "ignore files only apply inside a git repository");
    }

    private string CreateFile(params string[] pathSegments)
    {
        var filePath = Path.Combine(new[] { _testRootPath }.Concat(pathSegments).ToArray());
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        File.WriteAllText(filePath, "<Project />");
        return filePath;
    }
}
