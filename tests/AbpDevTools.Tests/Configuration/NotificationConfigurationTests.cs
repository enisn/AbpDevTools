using AbpDevTools.Configuration;
using AbpDevTools.Tests.Helpers;
using FluentAssertions;
using Xunit;

namespace AbpDevTools.Tests.Configuration;

/// <summary>
/// Unit tests for <see cref="NotificationConfiguration"/> class.
/// Tests YAML deserialization, enabled/disabled state, and default values.
/// </summary>
public class NotificationConfigurationTests : ConfigurationTestBase
{
    #region Valid YAML Deserialization Tests

    [Fact]
    public void Deserialize_ValidYaml_WithNotificationSettings_ShouldSucceed()
    {
        // Arrange
        var yaml = @"
enabled: true
";

        // Act
        var result = DeserializeYaml<NotificationOption>(yaml);

        // Assert
        result.Should().NotBeNull();
        result.Enabled.Should().BeTrue();
    }

    [Fact]
    public void Deserialize_ValidYaml_WithEnabledFalse_ShouldDeserializeCorrectly()
    {
        // Arrange
        var yaml = @"
enabled: false
";

        // Act
        var result = DeserializeYaml<NotificationOption>(yaml);

        // Assert
        result.Should().NotBeNull();
        result.Enabled.Should().BeFalse();
    }

    #endregion

    #region Enabled/Disabled State Tests

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Deserialize_ValidYaml_WithEnabledState_ShouldParseCorrectly(bool enabledState)
    {
        // Arrange
        var yaml = $@"
enabled: {enabledState.ToString().ToLower()}
";

        // Act
        var result = DeserializeYaml<NotificationOption>(yaml);

        // Assert
        result.Should().NotBeNull();
        result.Enabled.Should().Be(enabledState);
    }

    [Fact]
    public void Deserialize_ValidYaml_WithEnabledTrueVariations_ShouldAllParseToTrue()
    {
        // Arrange
        var yamlTrue = @"
enabled: true
";
        var yamlYes = @"
enabled: yes
";
        var yamlOn = @"
enabled: on
";

        // Act
        var resultTrue = DeserializeYaml<NotificationOption>(yamlTrue);
        var resultYes = DeserializeYaml<NotificationOption>(yamlYes);
        var resultOn = DeserializeYaml<NotificationOption>(yamlOn);

        // Assert
        resultTrue.Enabled.Should().BeTrue();
        resultYes.Enabled.Should().BeTrue();
        resultOn.Enabled.Should().BeTrue();
    }

    [Fact]
    public void Deserialize_ValidYaml_WithEnabledFalseVariations_ShouldAllParseToFalse()
    {
        // Arrange
        var yamlFalse = @"
enabled: false
";
        var yamlNo = @"
enabled: no
";
        var yamlOff = @"
enabled: off
";

        // Act
        var resultFalse = DeserializeYaml<NotificationOption>(yamlFalse);
        var resultNo = DeserializeYaml<NotificationOption>(yamlNo);
        var resultOff = DeserializeYaml<NotificationOption>(yamlOff);

        // Assert
        resultFalse.Enabled.Should().BeFalse();
        resultNo.Enabled.Should().BeFalse();
        resultOff.Enabled.Should().BeFalse();
    }

    #endregion

    #region Empty Configuration and Defaults Tests

    [Fact]
    public void Deserialize_EmptyYaml_ShouldReturnNull()
    {
        // Arrange
        var yaml = @"# Empty notification configuration
# No settings defined
";

        // Act
        var result = DeserializeYaml<NotificationOption>(yaml);

        // Assert
        // YAML deserialization returns null for empty documents
        result.Should().BeNull();
    }

    [Fact]
    public void Deserialize_ValidYaml_WithOnlyEnabledField_ShouldDeserializeCorrectly()
    {
        // Arrange
        var yaml = @"
enabled: true
";

        // Act
        var result = DeserializeYaml<NotificationOption>(yaml);

        // Assert
        result.Should().NotBeNull();
        result.Enabled.Should().BeTrue();
    }

    [Fact]
    public void NotificationOption_GetDefaults_ShouldReturnInstanceWithDefaultValues()
    {
        // Arrange & Act
        var option = new NotificationOption();

        // Assert
        option.Should().NotBeNull();
        option.Enabled.Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("# Comment only")]
    public void Deserialize_InvalidOrEmptyYaml_ShouldHandleGracefully(string yamlContent)
    {
        // Act
        var result = DeserializeYaml<NotificationOption>(yamlContent);

        // Assert
        // Empty or invalid YAML should return null or a default instance
        if (string.IsNullOrWhiteSpace(yamlContent) || yamlContent.TrimStart().StartsWith("#"))
        {
            result.Should().BeNull();
        }
    }

    #endregion

    #region Configuration Class Properties Tests

    [Fact]
    public void NotificationConfiguration_FileName_ShouldBeNotifications()
    {
        // Arrange
        var configuration = new NotificationConfiguration(YamlDeserializer, YamlSerializer);

        // Act
        var fileName = configuration.FileName;

        // Assert
        fileName.Should().Be("notifications");
    }

    #endregion

    #region Special Characters and Formatting Tests

    [Fact]
    public void Deserialize_ValidYaml_WithExtraWhitespace_ShouldParseCorrectly()
    {
        // Arrange
        var yaml = @"
enabled:     true
";

        // Act
        var result = DeserializeYaml<NotificationOption>(yaml);

        // Assert
        result.Should().NotBeNull();
        result.Enabled.Should().BeTrue();
    }

    [Fact]
    public void Deserialize_ValidYaml_WithComments_ShouldIgnoreComments()
    {
        // Arrange
        var yaml = @"
# Enable desktop notifications
enabled: true
# Notifications will show for build events
";

        // Act
        var result = DeserializeYaml<NotificationOption>(yaml);

        // Assert
        result.Should().NotBeNull();
        result.Enabled.Should().BeTrue();
    }

    #endregion

    #region Serialization Tests

    [Fact]
    public void Serialize_NotificationOption_WithEnabledTrue_ShouldProduceValidYaml()
    {
        // Arrange
        var option = new NotificationOption { Enabled = true };

        // Act
        var yaml = SerializeYaml(option);

        // Assert
        yaml.Should().NotBeNullOrEmpty();
        yaml.Should().Contain("enabled");
        yaml.Should().Contain("true");
    }

    [Fact]
    public void Serialize_NotificationOption_WithEnabledFalse_ShouldProduceValidYaml()
    {
        // Arrange
        var option = new NotificationOption { Enabled = false };

        // Act
        var yaml = SerializeYaml(option);

        // Assert
        yaml.Should().NotBeNullOrEmpty();
        yaml.Should().Contain("enabled");
        yaml.Should().Contain("false");
    }

    #endregion

    #region Round-trip Tests

    [Fact]
    public void SerializeThenDeserialize_NotificationOption_ShouldPreserveValues()
    {
        // Arrange
        var original = new NotificationOption { Enabled = true };

        // Act
        var yaml = SerializeYaml(original);
        var deserialized = DeserializeYaml<NotificationOption>(yaml);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized.Enabled.Should().Be(original.Enabled);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void RoundTrip_NotificationOption_WithVariousEnabledStates_ShouldPreserveCorrectly(bool enabledState)
    {
        // Arrange
        var original = new NotificationOption { Enabled = enabledState };

        // Act
        var yaml = SerializeYaml(original);
        var deserialized = DeserializeYaml<NotificationOption>(yaml);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized.Enabled.Should().Be(enabledState);
    }

    #endregion
}
