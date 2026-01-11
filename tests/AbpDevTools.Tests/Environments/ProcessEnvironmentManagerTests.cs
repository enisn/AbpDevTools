using AbpDevTools.Configuration;
using AbpDevTools.Environments;
using CliFx.Exceptions;
using FluentAssertions;
using NSubstitute;
using System.Diagnostics;
using Xunit;
using YamlDotNet.Serialization;

namespace AbpDevTools.Tests.Environments;

public class ProcessEnvironmentManagerTests
{
    private readonly ProcessEnvironmentManager _manager;
    private readonly EnvironmentConfiguration _configuration;
    private readonly IDeserializer _deserializer;
    private readonly ISerializer _serializer;

    public ProcessEnvironmentManagerTests()
    {
        _deserializer = Substitute.For<IDeserializer>();
        _serializer = Substitute.For<ISerializer>();
        _configuration = new EnvironmentConfiguration(_deserializer, _serializer);
        _manager = new ProcessEnvironmentManager(_configuration);
    }

    [Fact]
    public void SetEnvironmentVariablesForProcess_should_set_all_variables()
    {
        // Arrange
        var processStartInfo = new ProcessStartInfo();
        var variables = new Dictionary<string, string>
        {
            { "TEST_VAR1", "value1" },
            { "TEST_VAR2", "value2" },
            { "TEST_VAR3", "value3" }
        };

        // Act
        _manager.SetEnvironmentVariablesForProcess(processStartInfo, variables);

        // Assert
        processStartInfo.EnvironmentVariables["TEST_VAR1"].Should().Be("value1");
        processStartInfo.EnvironmentVariables["TEST_VAR2"].Should().Be("value2");
        processStartInfo.EnvironmentVariables["TEST_VAR3"].Should().Be("value3");
    }

    [Fact]
    public void SetEnvironmentVariablesForProcess_should_replace_Today_placeholder()
    {
        // Arrange
        var processStartInfo = new ProcessStartInfo();
        var expectedDate = DateTime.Today.ToString("yyyyMMdd");
        var variables = new Dictionary<string, string>
        {
            { "LOG_FILE", "log_{Today}.txt" }
        };

        // Act
        _manager.SetEnvironmentVariablesForProcess(processStartInfo, variables);

        // Assert
        processStartInfo.EnvironmentVariables["LOG_FILE"].Should().Be($"log_{expectedDate}.txt");
    }

    [Fact]
    public void SetEnvironmentVariablesForProcess_should_replace_AppName_placeholder()
    {
        // Arrange
        var processStartInfo = new ProcessStartInfo
        {
            WorkingDirectory = @"C:\Projects\MyApp.Web"
        };
        var variables = new Dictionary<string, string>
        {
            { "APP_NAME", "{AppName}" }
        };

        // Act
        _manager.SetEnvironmentVariablesForProcess(processStartInfo, variables);

        // Assert
        // MyApp.Web has ".", so it takes "MyApp" and replaces "MyApp.Web" in the path
        // Path becomes C:\Projects\MyApp, normalized to lowercase alphanumeric: cprojectsmyapp
        processStartInfo.EnvironmentVariables["APP_NAME"].Should().Be("cprojectsmyapp");
    }

    [Fact]
    public void SetEnvironmentVariablesForProcess_should_handle_multiple_placeholders()
    {
        // Arrange
        var processStartInfo = new ProcessStartInfo
        {
            WorkingDirectory = @"C:\Projects\Test"
        };
        var expectedDate = DateTime.Today.ToString("yyyyMMdd");
        var variables = new Dictionary<string, string>
        {
            { "COMPLEX_VAR", "{AppName}_{Today}_data" }
        };

        // Act
        _manager.SetEnvironmentVariablesForProcess(processStartInfo, variables);

        // Assert
        processStartInfo.EnvironmentVariables["COMPLEX_VAR"].Should().Be($"cprojectstest_{expectedDate}_data");
    }

    [Fact]
    public void SetEnvironmentVariablesForProcess_should_overwrite_existing_variables()
    {
        // Arrange
        var processStartInfo = new ProcessStartInfo();
        processStartInfo.EnvironmentVariables["EXISTING_VAR"] = "old_value";
        var variables = new Dictionary<string, string>
        {
            { "EXISTING_VAR", "new_value" }
        };

        // Act
        _manager.SetEnvironmentVariablesForProcess(processStartInfo, variables);

        // Assert
        processStartInfo.EnvironmentVariables["EXISTING_VAR"].Should().Be("new_value");
    }

    [Fact]
    public void SetEnvironment_should_throw_for_nonexistent_environment()
    {
        // Arrange
        _deserializer.Deserialize<Dictionary<string, EnvironmentOption>>(Arg.Any<string>())
            .Returns(new Dictionary<string, EnvironmentOption>());

        // Act
        var action = () => _manager.SetEnvironment("nonexistent", @"C:\Test");

        // Assert
        action.Should().Throw<CommandException>()
            .WithMessage("*Environment not found*");
    }

    [Fact]
    public void SetEnvironmentForProcess_should_throw_for_nonexistent_environment()
    {
        // Arrange
        _deserializer.Deserialize<Dictionary<string, EnvironmentOption>>(Arg.Any<string>())
            .Returns(new Dictionary<string, EnvironmentOption>());
        var processStartInfo = new ProcessStartInfo();

        // Act
        var action = () => _manager.SetEnvironmentForProcess("nonexistent", processStartInfo);

        // Assert
        action.Should().Throw<CommandException>()
            .WithMessage("*Environment not found*");
    }

    [Fact]
    public void SetEnvironmentForProcess_should_set_variables_from_configured_environment()
    {
        // Arrange
        var environmentName = "test-env";
        var options = new Dictionary<string, EnvironmentOption>
        {
            {
                environmentName, new EnvironmentOption
                {
                    Variables = new Dictionary<string, string>
                    {
                        { "CONFIG_VAR1", "config_value1" },
                        { "CONFIG_VAR2", "config_value2" }
                    }
                }
            }
        };

        _deserializer.Deserialize<Dictionary<string, EnvironmentOption>>(Arg.Any<string>())
            .Returns(options);

        var processStartInfo = new ProcessStartInfo();

        // Act
        _manager.SetEnvironmentForProcess(environmentName, processStartInfo);

        // Assert
        processStartInfo.EnvironmentVariables["CONFIG_VAR1"].Should().Be("config_value1");
        processStartInfo.EnvironmentVariables["CONFIG_VAR2"].Should().Be("config_value2");
    }

    [Theory]
    [InlineData(@"C:\Projects\MyApp.Web", "cprojectsmyapp")]
    [InlineData(@"/Users/john/projects/Test.Api", "usersjohnprojectstest")]
    [InlineData(@"C:\dev\simple-name", "cdevsimplename")]
    [InlineData(@"C:\very\long\path\that\exceeds\maximum\length\and\needs\to\be\truncated\from\the\start", "cverylongpaththatexceedsmaximumlengthandneedstobetruncatedfromthestart")]
    public void SetEnvironmentVariablesForProcess_should_generate_correct_AppName(string directory, string expectedAppName)
    {
        // Arrange
        var processStartInfo = new ProcessStartInfo
        {
            WorkingDirectory = directory
        };
        var variables = new Dictionary<string, string>
        {
            { "APP_NAME", "{AppName}" }
        };

        // Act
        _manager.SetEnvironmentVariablesForProcess(processStartInfo, variables);

        // Assert
        var result = processStartInfo.EnvironmentVariables["APP_NAME"];
        if (expectedAppName.Length > 116)
        {
            result.Should().HaveLength(116);
            result.Should().EndWith(expectedAppName[^116..]);
        }
        else
        {
            result.Should().Be(expectedAppName);
        }
    }

    [Fact]
    public void SetEnvironmentVariablesForProcess_should_handle_empty_directory()
    {
        // Arrange
        var processStartInfo = new ProcessStartInfo
        {
            WorkingDirectory = string.Empty
        };
        var variables = new Dictionary<string, string>
        {
            { "APP_NAME", "{AppName}" }
        };

        // Act
        _manager.SetEnvironmentVariablesForProcess(processStartInfo, variables);

        // Assert
        processStartInfo.EnvironmentVariables["APP_NAME"].Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void SetEnvironmentVariablesForProcess_should_use_current_directory_when_null()
    {
        // Arrange
        var processStartInfo = new ProcessStartInfo
        {
            WorkingDirectory = null
        };
        var variables = new Dictionary<string, string>
        {
            { "APP_NAME", "{AppName}" }
        };

        // Act
        _manager.SetEnvironmentVariablesForProcess(processStartInfo, variables);

        // Assert
        processStartInfo.EnvironmentVariables["APP_NAME"].Should().NotBeNullOrEmpty();
    }
}
