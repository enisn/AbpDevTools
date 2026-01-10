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
runnableProjects:
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
runnableProjects: []
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
runnableProjects:
  - "".Web""
", 1)]
    [InlineData(@"
runnableProjects:
  - "".Web""
  - "".Blazor""
", 2)]
    [InlineData(@"
runnableProjects:
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
runnableProjects:
  - ""   ""
  - ""Test.Project""
", new string[] { "   ", "Test.Project" })]
    [InlineData(@"
runnableProjects:
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
runnableProjects:
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
runnableProjects: ""not-an-array""
";

        // Act & Assert
        // When runnableProjects is a string instead of an array, deserialization throws
        Should.Throw<Exception>(() => DeserializeYaml<RunOptions>(yaml));
    }

    public static TheoryData<string, string[]> GetMinimalYamlData()
    {
        return new TheoryData<string, string[]>
        {
            {
                @"
runnableProjects:
  - "".Web""
",
                new[] { ".Web" }
            },
            {
                @"
runnableProjects:
  - "".HttpApi.Host""
  - "".Web""
  - "".Blazor""
",
                new[] { ".HttpApi.Host", ".Web", ".Blazor" }
            },
            {
                @"
runnableProjects:
  - "".Mvc.Host""
  - "".Mvc""
",
                new[] { ".Mvc.Host", ".Mvc" }
            }
        };
    }
}
