using AbpDevTools.Configuration;
using AbpDevTools.Tests.Helpers;
using Shouldly;
using Xunit;
using YamlDotNet.Core;

namespace AbpDevTools.Tests.Configuration;

public class RunConfigurationTests : ConfigurationTestBase
{
    [Fact]
    public void Deserialize_ValidYamlWithAllProjects_ShouldReturnCorrectOptions()
    {
        // Arrange
        var yaml = @"
runnable-projects:
  - "".HttpApi.Host""
  - "".HttpApi.HostWithIds""
  - "".AuthServer""
  - "".IdentityServer""
  - "".Web""
  - "".Web.Host""
  - "".Web.Public""
  - "".Mvc""
  - "".Mvc.Host""
  - "".Blazor""
  - "".Blazor.Host""
  - "".Blazor.Server""
  - "".Blazor.Server.Host""
  - "".Blazor.Server.Tiered""
  - "".Unified""
  - "".PublicWeb""
  - "".PublicWebGateway""
  - "".WebGateway""
";

        // Act
        var result = DeserializeYaml<RunOptions>(yaml);

        // Assert
        result.ShouldNotBeNull();
        result.RunnableProjects.ShouldNotBeNull();
        result.RunnableProjects.Length.ShouldBe(18);
        result.RunnableProjects.ShouldContain(".HttpApi.Host");
        result.RunnableProjects.ShouldContain(".Blazor");
        result.RunnableProjects.ShouldContain(".WebGateway");
    }

    [Theory]
    [MemberData(nameof(GetMinimalYamlData))]
    public void Deserialize_MinimalYaml_ShouldReturnCorrectOptions(string yaml, string[] expectedProjects)
    {
        // Arrange
        // yaml and expectedProjects provided by MemberData

        // Act
        var result = DeserializeYaml<RunOptions>(yaml);

        // Assert
        result.ShouldNotBeNull();
        result.RunnableProjects.ShouldNotBeNull();
        result.RunnableProjects.Length.ShouldBe(expectedProjects.Length);
        foreach (var project in expectedProjects)
        {
            result.RunnableProjects.ShouldContain(project);
        }
    }

    [Fact]
    public void Deserialize_YamlWithoutRunnableProjects_ShouldReturnNull()
    {
        // Arrange
        var yaml = @"
# Empty configuration file
";

        // Act
        var result = DeserializeYaml<RunOptions>(yaml);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void Deserialize_YamlWithEmptyRunnableProjectsArray_ShouldReturnEmptyArray()
    {
        // Arrange
        var yaml = @"
runnable-projects: []
";

        // Act
        var result = DeserializeYaml<RunOptions>(yaml);

        // Assert
        result.ShouldNotBeNull();
        result.RunnableProjects.ShouldNotBeNull();
        result.RunnableProjects.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(@"
runnable-projects:
  - "".Web""
", 1)]
    [InlineData(@"
runnable-projects:
  - "".Web""
  - "".Blazor""
", 2)]
    [InlineData(@"
runnable-projects:
  - "".Web""
  - "".Blazor""
  - "".Mvc""
", 3)]
    public void Deserialize_YamlWithMultipleProjects_ShouldReturnCorrectCount(string yaml, int expectedCount)
    {
        // Arrange
        // yaml and expectedCount provided by InlineData

        // Act
        var result = DeserializeYaml<RunOptions>(yaml);

        // Assert
        result.ShouldNotBeNull();
        result.RunnableProjects.ShouldNotBeNull();
        result.RunnableProjects.Length.ShouldBe(expectedCount);
    }

    [Theory]
    [InlineData(@"
runnable-projects:
  - ""   ""
  - ""Test.Project""
", new string[] { "   ", "Test.Project" })]
    [InlineData(@"
runnable-projects:
  - "".Web.Mvc""
  - "".Web""
", new string[] { ".Web.Mvc", ".Web" })]
    public void Deserialize_YamlWithVariousProjectPatterns_ShouldPreserveExactValues(string yaml, string[] expectedProjects)
    {
        // Arrange
        // yaml and expectedProjects provided by InlineData

        // Act
        var result = DeserializeYaml<RunOptions>(yaml);

        // Assert
        result.ShouldNotBeNull();
        result.RunnableProjects.ShouldNotBeNull();
        result.RunnableProjects.Length.ShouldBe(expectedProjects.Length);
        for (int i = 0; i < expectedProjects.Length; i++)
        {
            result.RunnableProjects[i].ShouldBe(expectedProjects[i]);
        }
    }

    [Fact]
    public void Deserialize_InvalidYamlSyntax_ShouldThrowYamlException()
    {
        // Arrange
        var invalidYaml = @"
runnable-projects:
  - "".Web""
    - "".Blazor""
";

        // Act & Assert
        Should.Throw<YamlException>(() => DeserializeYaml<RunOptions>(invalidYaml));
    }

    [Fact]
    public void Deserialize_YamlWithStringInsteadOfArray_ShouldThrowException()
    {
        // Arrange
        var yaml = @"
runnable-projects: ""not-an-array""
";

        // Act & Assert
        // When RunnableProjects is a string instead of an array, deserialization throws
        Should.Throw<Exception>(() => DeserializeYaml<RunOptions>(yaml));
    }

    public static TheoryData<string, string[]> GetMinimalYamlData()
    {
        return new TheoryData<string, string[]>
        {
            {
                @"
runnable-projects:
  - "".Web""
",
                new[] { ".Web" }
            },
            {
                @"
runnable-projects:
  - "".HttpApi.Host""
  - "".Web""
  - "".Blazor""
",
                new[] { ".HttpApi.Host", ".Web", ".Blazor" }
            },
            {
                @"
runnable-projects:
  - "".Mvc.Host""
  - "".Mvc""
",
                new[] { ".Mvc.Host", ".Mvc" }
            }
        };
    }
}
