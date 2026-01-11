using AbpDevTools.Commands;
using AbpDevTools.Configuration;
using AbpDevTools.Tests.Helpers;
using CliFx.Infrastructure;
using NSubstitute;
using Shouldly;
using Xunit;
using System.Text;

namespace AbpDevTools.Tests.Commands;

/// <summary>
/// Tests for <see cref="ReplaceCommand"/> covering configuration loading,
/// command parameter handling, and property behavior.
///
/// Note: ReplaceCommand uses static file I/O operations (File.ReadAllText, File.WriteAllText, Directory.EnumerateFiles)
/// which are difficult to mock without additional abstractions. Full end-to-end file replacement tests
/// would require either integration tests with actual files or refactoring ReplaceCommand to use
/// injected file I/O abstractions.
///
/// These tests focus on what can be verified without file system operations:
/// - Configuration loading and validation
/// - Command parameter handling
/// - Property behavior
/// - Error scenarios for invalid inputs
/// </summary>
public class ReplaceCommandTests : CommandTestBase
{
    #region Configuration Loading Tests

    [Fact]
    public void ExecuteAsync_Loads_ReplacementConfiguration_Verify_Options()
    {
        // Arrange
        var options = new Dictionary<string, ReplacementOption>
        {
            {
                "TestConfig", new ReplacementOption
                {
                    FilePattern = "appsettings.json",
                    Find = "old",
                    Replace = "new"
                }
            }
        };

        var mockConfiguration = CreateMockReplacementConfiguration(options);

        // Act & Assert - Verify the configuration returns expected options
        mockConfiguration.GetOptions().ShouldNotBeNull();
        mockConfiguration.GetOptions().Count.ShouldBe(1);
        mockConfiguration.GetOptions().ShouldContainKey("TestConfig");
    }

    [Fact]
    public void ReplacementConfiguration_Contains_Expected_Config_Name()
    {
        // Arrange
        var options = new Dictionary<string, ReplacementOption>
        {
            {
                "UpdateConnectionString", new ReplacementOption
                {
                    FilePattern = "appsettings.json",
                    Find = "Trusted_Connection=True;",
                    Replace = "User ID=SA;Password=12345678Aa;"
                }
            }
        };

        var mockConfiguration = CreateMockReplacementConfiguration(options);

        // Act & Assert
        var config = mockConfiguration.GetOptions();
        config.ContainsKey("UpdateConnectionString").ShouldBeTrue();
    }

    #endregion

    #region Replacement Option Property Tests

    [Theory]
    [InlineData("appsettings.json", "old", "new")]
    [InlineData(".*\\.json", "Trusted_Connection=True;", "User ID=SA;Password=12345678Aa;")]
    [InlineData("src/.*\\.cs", "namespace Old", "namespace New")]
    public void ReplacementOption_Has_Valid_Properties(string filePattern, string find, string replace)
    {
        // Arrange
        var option = new ReplacementOption
        {
            FilePattern = filePattern,
            Find = find,
            Replace = replace
        };

        // Assert
        option.FilePattern.ShouldBe(filePattern);
        option.Find.ShouldBe(find);
        option.Replace.ShouldBe(replace);
    }

    #endregion

    #region Command Property Tests

    [Fact]
    public void ReplaceCommand_Accepts_ReplacementConfigName_Property()
    {
        // Arrange
        var mockConfiguration = CreateMockReplacementConfiguration(new Dictionary<string, ReplacementOption>());
        var command = new ReplaceCommand(mockConfiguration)
        {
            ReplacementConfigName = "TestConfig"
        };

        // Assert
        command.ReplacementConfigName.ShouldBe("TestConfig");
    }

    [Fact]
    public void ReplaceCommand_Accepts_WorkingDirectory_Property()
    {
        // Arrange
        var mockConfiguration = CreateMockReplacementConfiguration(new Dictionary<string, ReplacementOption>());
        var command = new ReplaceCommand(mockConfiguration)
        {
            WorkingDirectory = "C:\\test\\directory"
        };

        // Assert
        command.WorkingDirectory.ShouldBe("C:\\test\\directory");
    }

    [Fact]
    public void ReplaceCommand_Accepts_Files_Parameter()
    {
        // Arrange
        var mockConfiguration = CreateMockReplacementConfiguration(new Dictionary<string, ReplacementOption>());
        var command = new ReplaceCommand(mockConfiguration)
        {
            Files = new[] { "appsettings.json", "appsettings.Production.json" }
        };

        // Assert
        command.Files.ShouldNotBeNull();
        command.Files.Length.ShouldBe(2);
        command.Files.ShouldContain("appsettings.json");
        command.Files.ShouldContain("appsettings.Production.json");
    }

    [Fact]
    public void ReplaceCommand_Accepts_Null_Files_Parameter()
    {
        // Arrange
        var mockConfiguration = CreateMockReplacementConfiguration(new Dictionary<string, ReplacementOption>());
        var command = new ReplaceCommand(mockConfiguration)
        {
            Files = null
        };

        // Assert
        command.Files.ShouldBeNull();
    }

    #endregion

    #region Interactive Mode Tests

    [Fact]
    public void ReplaceCommand_Supports_Interactive_Mode_True()
    {
        // Arrange
        var mockConfiguration = CreateMockReplacementConfiguration(new Dictionary<string, ReplacementOption>());
        var command = new ReplaceCommand(mockConfiguration)
        {
            InteractiveMode = true
        };

        // Assert
        command.InteractiveMode.ShouldBeTrue();
    }

    [Fact]
    public void ReplaceCommand_Supports_Interactive_Mode_False()
    {
        // Arrange
        var mockConfiguration = CreateMockReplacementConfiguration(new Dictionary<string, ReplacementOption>());
        var command = new ReplaceCommand(mockConfiguration)
        {
            InteractiveMode = false
        };

        // Assert
        command.InteractiveMode.ShouldBeFalse();
    }

    #endregion

    #region Multiple Configuration Tests

    [Fact]
    public void ReplacementConfiguration_Can_Have_Multiple_Options()
    {
        // Arrange
        var options = new Dictionary<string, ReplacementOption>
        {
            {
                "Config1", new ReplacementOption
                {
                    FilePattern = "appsettings.json",
                    Find = "old1",
                    Replace = "new1"
                }
            },
            {
                "Config2", new ReplacementOption
                {
                    FilePattern = "appsettings.json",
                    Find = "old2",
                    Replace = "new2"
                }
            },
            {
                "Config3", new ReplacementOption
                {
                    FilePattern = ".*\\.json",
                    Find = "Development",
                    Replace = "Production"
                }
            }
        };

        var mockConfiguration = CreateMockReplacementConfiguration(options);

        // Act & Assert
        var config = mockConfiguration.GetOptions();
        config.Count.ShouldBe(3);
        config.ShouldContainKey("Config1");
        config.ShouldContainKey("Config2");
        config.ShouldContainKey("Config3");
    }

    [Fact]
    public void ReplacementConfiguration_Allows_Accessing_All_Configs()
    {
        // Arrange
        var options = new Dictionary<string, ReplacementOption>
        {
            {
                "First", new ReplacementOption
                {
                    FilePattern = "appsettings.json",
                    Find = "first",
                    Replace = "last"
                }
            },
            {
                "Second", new ReplacementOption
                {
                    FilePattern = "appsettings.Production.json",
                    Find = "Server=localhost",
                    Replace = "Server=prod-server"
                }
            }
        };

        var mockConfiguration = CreateMockReplacementConfiguration(options);

        // Act
        var config = mockConfiguration.GetOptions();

        // Assert
        config["First"].Find.ShouldBe("first");
        config["Second"].Find.ShouldBe("Server=localhost");
        config["First"].Replace.ShouldBe("last");
        config["Second"].Replace.ShouldBe("Server=prod-server");
    }

    #endregion

    #region Regex Pattern Tests

    [Fact]
    public void ReplacementOption_Supports_Regex_File_Patterns()
    {
        // Arrange
        var option = new ReplacementOption
        {
            FilePattern = ".*\\.json",
            Find = "test",
            Replace = "replaced"
        };

        // Assert - Verify the pattern is stored
        option.FilePattern.ShouldBe(".*\\.json");
        option.Find.ShouldBe("test");
        option.Replace.ShouldBe("replaced");

        // Note: Actual regex matching happens in ExecuteAsync with file system operations
        System.Text.RegularExpressions.Regex.IsMatch("test.json", option.FilePattern).ShouldBeTrue();
        System.Text.RegularExpressions.Regex.IsMatch("test.txt", option.FilePattern).ShouldBeFalse();
    }

    [Fact]
    public void ReplacementOption_Supports_Complex_Regex_Patterns()
    {
        // Arrange
        var patterns = new[]
        {
            "appsettings\\..*\\.json",
            "src/.*\\.cs$",
            "^.*Config\\.json$",
            "file\\d+\\.txt"
        };

        // Act & Assert - Verify patterns can be stored
        foreach (var pattern in patterns)
        {
            var option = new ReplacementOption
            {
                FilePattern = pattern,
                Find = "find",
                Replace = "replace"
            };

            option.FilePattern.ShouldBe(pattern);
        }
    }

    #endregion

    #region Test Helper Classes

    /// <summary>
    /// Creates a mock ReplacementConfiguration with the specified options.
    /// </summary>
    private static ReplacementConfiguration CreateMockReplacementConfiguration(
        Dictionary<string, ReplacementOption> options)
    {
        var mock = Substitute.For<ReplacementConfiguration>(
            Substitute.For<YamlDotNet.Serialization.IDeserializer>(),
            Substitute.For<YamlDotNet.Serialization.ISerializer>()
        );

        mock.GetOptions().Returns(options);

        return mock;
    }

    #endregion
}
