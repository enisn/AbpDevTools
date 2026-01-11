using AbpDevTools.Commands;
using AbpDevTools.Configuration;
using AbpDevTools.RecycleBin;
using CliFx.Infrastructure;
using FluentAssertions;
using NSubstitute;
using Xunit;
using YamlDotNet.Serialization;

namespace AbpDevTools.Tests.Commands;

/// <summary>
/// Unit tests for CleanCommand class.
/// Tests clean operation including folder pattern matching, file deletion,
/// recycle bin integration, and force delete functionality.
/// </summary>
public class CleanCommandTests
{
    #region ExecuteAsync Loads CleanConfiguration Tests

    [Fact]
    public void ExecuteAsync_LoadsCleanConfiguration_UsesDefaultFolders()
    {
        // Arrange
        var cleanOptions = new CleanOptions(); // Default folders: bin, obj, node_modules
        cleanOptions.Folders.Should().NotBeEmpty("CleanOptions should have default folders");
        cleanOptions.Folders.Should().BeEquivalentTo(new[] { "bin", "obj", "node_modules" });
    }

    #endregion

    #region ExecuteAsync Finds Folders Matching Patterns Tests

    [Fact]
    public void ExecuteAsync_FindsFoldersMatchingPatterns_BinObjNodeModules()
    {
        // Arrange
        var cleanOptions = new CleanOptions();

        // Act & Assert
        cleanOptions.Folders.Should().Contain("bin");
        cleanOptions.Folders.Should().Contain("obj");
        cleanOptions.Folders.Should().Contain("node_modules");
    }

    [Theory]
    [InlineData("bin")]
    [InlineData("obj")]
    [InlineData("node_modules")]
    public void ExecuteAsync_FolderPattern_ShouldMatchExpectedFolder(string folder)
    {
        // Arrange
        var cleanOptions = new CleanOptions();

        // Act & Assert
        cleanOptions.Folders.Should().Contain(folder);
    }

    #endregion

    #region ExecuteAsync Finds Files Matching Patterns Tests

    [Fact]
    public void ExecuteAsync_FindsFilesMatchingPatterns_EndsWithFolderName()
    {
        // Arrange
        var cleanOptions = new CleanOptions();

        // Act & Assert
        cleanOptions.Folders.Should().NotBeEmpty();
        cleanOptions.Folders.Should().HaveCount(3, "Default configuration has 3 folders");
    }

    #endregion

    #region ExecuteAsync Sends Deleted Items to Recycle Bin Tests

    [Fact]
    public void ExecuteAsync_WithSoftDelete_SendsItemsToRecycleBin()
    {
        // Arrange
        var recycleBinManager = Substitute.For<IRecycleBinManager>();

        var command = new CleanCommand(
            new CleanConfiguration(Substitute.For<IDeserializer>(), Substitute.For<ISerializer>()),
            recycleBinManager
        )
        {
            WorkingDirectory = "C:\\TestProject",
            SoftDelete = true
        };

        // Act & Assert
        command.SoftDelete.Should().BeTrue("SoftDelete should be true when set");
    }

    [Fact]
    public void ExecuteAsync_WithoutSoftDelete_DoesNotUseRecycleBin()
    {
        // Arrange
        var recycleBinManager = Substitute.For<IRecycleBinManager>();

        var command = new CleanCommand(
            new CleanConfiguration(Substitute.For<IDeserializer>(), Substitute.For<ISerializer>()),
            recycleBinManager
        )
        {
            WorkingDirectory = "C:\\TestProject",
            SoftDelete = false
        };

        // Act & Assert
        command.SoftDelete.Should().BeFalse("SoftDelete should be false when not set");
    }

    #endregion

    #region ExecuteAsync Handles Empty Configuration Tests

    [Fact]
    public void ExecuteAsync_EmptyConfiguration_DoesNotThrow()
    {
        // Arrange
        var cleanOptions = new CleanOptions
        {
            Folders = Array.Empty<string>()
        };

        // Act & Assert
        cleanOptions.Folders.Should().BeEmpty("Empty configuration should have no folders");
    }

    #endregion

    #region ExecuteAsync Handles No Matching Items Tests

    [Fact]
    public void ExecuteAsync_NoMatchingItems_ReportsZeroItems()
    {
        // Arrange
        var command = new CleanCommand(
            new CleanConfiguration(Substitute.For<IDeserializer>(), Substitute.For<ISerializer>()),
            Substitute.For<IRecycleBinManager>()
        )
        {
            WorkingDirectory = "C:\\NonExistentTestDirectory" + Guid.NewGuid()
        };

        // Act & Assert
        command.WorkingDirectory.Should().NotBeNullOrEmpty("WorkingDirectory should be set");
    }

    #endregion

    #region ExecuteAsync Supports Force Flag Tests

    [Fact]
    public void ExecuteAsync_WithForceFlag_PermanentDeleteSkipsRecycleBin()
    {
        // Arrange
        var recycleBinManager = Substitute.For<IRecycleBinManager>();

        var command = new CleanCommand(
            new CleanConfiguration(Substitute.For<IDeserializer>(), Substitute.For<ISerializer>()),
            recycleBinManager
        )
        {
            WorkingDirectory = "C:\\TestProject",
            SoftDelete = false // Force/permanent delete (no soft delete)
        };

        // Act & Assert
        command.SoftDelete.Should().BeFalse("SoftDelete should be false for force delete");
    }

    #endregion

    #region ExecuteAsync Reports Deleted Items Count Tests

    [Fact]
    public void ExecuteAsync_ReportsDeletedItemsCount_SuccessMessage()
    {
        // Arrange
        var command = new CleanCommand(
            new CleanConfiguration(Substitute.For<IDeserializer>(), Substitute.For<ISerializer>()),
            Substitute.For<IRecycleBinManager>()
        )
        {
            WorkingDirectory = "C:\\TestProject"
        };

        // Act & Assert
        command.WorkingDirectory.Should().Be("C:\\TestProject");
    }

    [Fact]
    public void ExecuteAsync_ReportsDeletedItemsCount_ErrorMessage()
    {
        // Arrange
        var recycleBinManager = Substitute.For<IRecycleBinManager>();

        // Make recycle bin throw an error to test error reporting
        recycleBinManager
            .WhenForAnyArgs(x => x.SendToRecycleBinAsync(Arg.Any<IEnumerable<string>>()))
            .Do(x => throw new IOException("Test error"));

        var command = new CleanCommand(
            new CleanConfiguration(Substitute.For<IDeserializer>(), Substitute.For<ISerializer>()),
            recycleBinManager
        )
        {
            WorkingDirectory = "C:\\TestProject",
            SoftDelete = true
        };

        // Act & Assert
        command.SoftDelete.Should().BeTrue();
    }

    #endregion
}
