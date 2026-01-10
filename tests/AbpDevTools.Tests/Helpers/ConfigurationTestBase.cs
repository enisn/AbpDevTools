using NSubstitute;
using Shouldly;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace AbpDevTools.Tests.Helpers;

/// <summary>
/// Base class for configuration tests following AAA (Arrange-Act-Assert) pattern.
/// Provides common setup and sample YAML data for testing configuration classes.
/// </summary>
public abstract class ConfigurationTestBase
{
    protected IDeserializer YamlDeserializer { get; }
    protected ISerializer YamlSerializer { get; }

    protected ConfigurationTestBase()
    {
        YamlDeserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        YamlSerializer = new SerializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();
    }

    /// <summary>
    /// Creates a temporary configuration file with the specified content for testing.
    /// Returns the file path. Remember to clean up the file after the test.
    /// </summary>
    protected string CreateTempConfigFile(string content, string fileName = "test-config.yml")
    {
        var tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempPath);
        var filePath = Path.Combine(tempPath, fileName);
        File.WriteAllText(filePath, content);
        return filePath;
    }

    /// <summary>
    /// Cleans up a temporary configuration file created by CreateTempConfigFile.
    /// </summary>
    protected void CleanupTempConfigFile(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
            var directory = Path.GetDirectoryName(filePath);
            if (directory != null && Directory.Exists(directory) && !Directory.EnumerateFileSystemEntries(directory).Any())
            {
                Directory.Delete(directory);
            }
        }
        catch
        {
            // Ignore cleanup errors in tests
        }
    }

    /// <summary>
    /// Deserializes YAML content to the specified type.
    /// </summary>
    protected T DeserializeYaml<T>(string yaml) where T : class
    {
        return YamlDeserializer.Deserialize<T>(yaml);
    }

    /// <summary>
    /// Serializes an object to YAML format.
    /// </summary>
    protected string SerializeYaml<T>(T obj) where T : class
    {
        return YamlSerializer.Serialize(obj);
    }

    // Sample YAML data for common configuration types

    /// <summary>
    /// Gets sample local-sources.yml content.
    /// </summary>
    protected string GetSampleLocalSourcesYaml() => TestConstants.YamlSamples.LocalSources;

    /// <summary>
    /// Gets sample replacement-configuration.yml content.
    /// </summary>
    protected string GetSampleReplacementYaml() => TestConstants.YamlSamples.ReplacementConfiguration;

    /// <summary>
    /// Gets sample run-configuration.yml content.
    /// </summary>
    protected string GetSampleRunConfigurationYaml() => TestConstants.YamlSamples.RunConfiguration;

    /// <summary>
    /// Gets sample environment-configuration.yml content.
    /// </summary>
    protected string GetSampleEnvironmentYaml() => TestConstants.YamlSamples.EnvironmentConfiguration;

    /// <summary>
    /// Creates a mock configuration directory with the specified configuration files.
    /// Returns the directory path. Remember to clean up after the test.
    /// </summary>
    protected string CreateMockConfigurationDirectory(Dictionary<string, string> configFiles)
    {
        var configPath = Path.Combine(Path.GetTempPath(), $"abpdev-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(configPath);

        foreach (var configFile in configFiles)
        {
            var filePath = Path.Combine(configPath, configFile.Key);
            File.WriteAllText(filePath, configFile.Value);
        }

        return configPath;
    }

    /// <summary>
    /// Cleans up a mock configuration directory created by CreateMockConfigurationDirectory.
    /// </summary>
    protected void CleanupMockConfigurationDirectory(string directoryPath)
    {
        try
        {
            if (Directory.Exists(directoryPath))
            {
                Directory.Delete(directoryPath, true);
            }
        }
        catch
        {
            // Ignore cleanup errors in tests
        }
    }

    /// <summary>
    /// Assert helper methods for common configuration assertions.
    /// </summary>
    protected static class ConfigAssertions
    {
        /// <summary>
        /// Asserts that a configuration file exists at the specified path.
        /// </summary>
        public static void FileShouldExist(string filePath)
        {
            File.Exists(filePath).ShouldBeTrue($"Configuration file should exist at: {filePath}");
        }

        /// <summary>
        /// Asserts that a configuration file does not exist at the specified path.
        /// </summary>
        public static void FileShouldNotExist(string filePath)
        {
            File.Exists(filePath).ShouldBeFalse($"Configuration file should not exist at: {filePath}");
        }

        /// <summary>
        /// Asserts that a configuration file contains the specified content.
        /// </summary>
        public static void FileShouldContain(string filePath, string expectedContent)
        {
            var content = File.ReadAllText(filePath);
            content.ShouldContain(expectedContent);
        }

        /// <summary>
        /// Asserts that a configuration value is equal to the expected value.
        /// </summary>
        public static void ValueShouldEqual<T>(T? actual, T? expected, string? message = null)
        {
            actual.ShouldBe(expected, message ?? $"Expected {expected} but got {actual}");
        }

        /// <summary>
        /// Asserts that a configuration value is not null.
        /// </summary>
        public static void ValueShouldNotBeNull<T>(T? value, string? message = null) where T : class
        {
            value.ShouldNotBeNull(message ?? "Value should not be null");
        }

        /// <summary>
        /// Asserts that a string value is not null or empty.
        /// </summary>
        public static void StringShouldNotBeNullOrWhiteSpace(string? value, string? message = null)
        {
            value.ShouldNotBeNullOrWhiteSpace(message ?? "String should not be null or whitespace");
        }
    }
}
