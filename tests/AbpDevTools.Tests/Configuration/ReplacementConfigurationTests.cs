using AbpDevTools.Configuration;
using AbpDevTools.Tests.Helpers;
using Shouldly;
using Xunit;
using System.Text.RegularExpressions;

namespace AbpDevTools.Tests.Configuration;

/// <summary>
/// Tests for <see cref="ReplacementConfiguration"/> covering YAML deserialization,
/// regex patterns, file patterns, and edge cases for text replacement rules.
/// </summary>
public class ReplacementConfigurationTests : ConfigurationTestBase
{
    #region Valid YAML Deserialization

    [Fact]
    public void Should_Deserialize_Valid_Yaml_With_Replacement_Rules()
    {
        // Arrange
        var yaml = @"
ConnectionString:
  file-pattern: appsettings.json
  find: Trusted_Connection=True;
  replace: User ID=SA;Password=12345678Aa;

LocalDb:
  file-pattern: appsettings.json
  find: Server=(LocalDb)\\MSSQLLocalDB;
  replace: Server=localhost;
";

        // Act
        var result = DeserializeYaml<Dictionary<string, ReplacementOption>>(yaml);

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(2);

        result.ContainsKey("ConnectionString").ShouldBeTrue();
        var connectionStringRule = result["ConnectionString"];
        connectionStringRule.FilePattern.ShouldBe("appsettings.json");
        connectionStringRule.Find.ShouldBe("Trusted_Connection=True;");
        connectionStringRule.Replace.ShouldBe("User ID=SA;Password=12345678Aa;");

        result.ContainsKey("LocalDb").ShouldBeTrue();
        var localDbRule = result["LocalDb"];
        localDbRule.FilePattern.ShouldBe("appsettings.json");
        // In YAML with unquoted values, backslashes are preserved literally
        localDbRule.Find.ShouldBe("Server=(LocalDb)\\\\MSSQLLocalDB;");
        localDbRule.Replace.ShouldBe("Server=localhost;");
    }

    #endregion

    #region Regex Patterns

    [Theory]
    [InlineData(@"Version: \d+\.\d+\.\d+\.\d+", "*.cs")]
    [InlineData(@"\b[A-Z]{2,}\b", "*.log")]
    [InlineData(@"\$\{(\w+)\}", "*.yml")]
    public void Should_Handle_Regex_Patterns_In_Find_Replace(string find, string filePattern)
    {
        // Arrange
        var replacement = "replacement_value";
        // Use single quotes in YAML to preserve escape sequences literally
        var yaml = $@"
RegexTest:
  file-pattern: '{filePattern}'
  find: '{find}'
  replace: '{replacement}'
";

        // Act
        var result = DeserializeYaml<Dictionary<string, ReplacementOption>>(yaml);

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(1);

        var rule = result["RegexTest"];
        rule.Find.ShouldBe(find);
        rule.Replace.ShouldBe(replacement);
        rule.FilePattern.ShouldBe(filePattern);

        // Verify the find pattern is a valid regex
        var exception = Record.Exception(() => new Regex(find));
        exception.ShouldBeNull($"The find pattern '{find}' should be a valid regex");
    }

    [Fact]
    public void Should_Handle_Capture_Group_Replacement_Pattern()
    {
        // Arrange
        var yaml = @"
CaptureGroupReplacement:
  file-pattern: '*.cs'
  find: '(\d{4})-(\d{2})-(\d{2})'
  replace: '$3/$2/$1'
";

        // Act
        var result = DeserializeYaml<Dictionary<string, ReplacementOption>>(yaml);

        // Assert
        result.ShouldNotBeNull();
        result["CaptureGroupReplacement"].Find.ShouldBe(@"(\d{4})-(\d{2})-(\d{2})");
        result["CaptureGroupReplacement"].Replace.ShouldBe("$3/$2/$1");
    }

    [Fact]
    public void Should_Handle_Email_Regex_Pattern()
    {
        // Arrange
        var emailRegex = @"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b";
        var yaml = $@"
EmailReplacement:
  file-pattern: '*.txt'
  find: '{emailRegex}'
  replace: 'REDACTED_EMAIL'
";

        // Act
        var result = DeserializeYaml<Dictionary<string, ReplacementOption>>(yaml);

        // Assert
        result.ShouldNotBeNull();
        result["EmailReplacement"].Find.ShouldBe(emailRegex);
        result["EmailReplacement"].Replace.ShouldBe("REDACTED_EMAIL");
    }

    #endregion

    #region File Patterns (Globs)

    [Theory]
    [InlineData("*.cs")]
    [InlineData("appsettings*.json")]
    [InlineData("src/**/*.csproj")]
    public void Should_Handle_File_Pattern_Globs(string filePattern)
    {
        // Arrange
        // Quote the filePattern value to prevent YAML from interpreting * as an anchor
        var yaml = $@"
GlobPattern:
  file-pattern: ""{filePattern}""
  find: test
  replace: production
";

        // Act
        var result = DeserializeYaml<Dictionary<string, ReplacementOption>>(yaml);

        // Assert
        result.ShouldNotBeNull();
        result["GlobPattern"].FilePattern.ShouldBe(filePattern);
    }

    [Fact]
    public void Should_Handle_Complex_Glob_Patterns()
    {
        // Arrange
        var yaml = @"
ConfigFiles:
  file-pattern: '*.{json,yml,yaml}'
  find: localhost
  replace: production-server
";

        // Act
        var result = DeserializeYaml<Dictionary<string, ReplacementOption>>(yaml);

        // Assert
        result.ShouldNotBeNull();
        result["ConfigFiles"].FilePattern.ShouldBe("*.{json,yml,yaml}");
    }

    [Fact]
    public void Should_Handle_Double_Asterisk_Glob_Pattern()
    {
        // Arrange
        var yaml = @"
RecursivePattern:
  file-pattern: ""**/*.config""
  find: old_value
  replace: new_value
";

        // Act
        var result = DeserializeYaml<Dictionary<string, ReplacementOption>>(yaml);

        // Assert
        result.ShouldNotBeNull();
        result["RecursivePattern"].FilePattern.ShouldBe("**/*.config");
    }

    #endregion

    #region Empty Replacements

    [Fact]
    public void Should_Handle_Empty_Replacements_List()
    {
        // Arrange
        var yaml = "{}";

        // Act
        var result = DeserializeYaml<Dictionary<string, ReplacementOption>>(yaml);

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(0);
    }

    [Fact]
    public void Should_Handle_Partial_Replacement_Option()
    {
        // Arrange
        var yaml = @"
MinimalRule:
  file-pattern: '*.txt'
  find: 'search_text'
";

        // Act
        var result = DeserializeYaml<Dictionary<string, ReplacementOption>>(yaml);

        // Assert
        result.ShouldNotBeNull();
        result["MinimalRule"].FilePattern.ShouldBe("*.txt");
        result["MinimalRule"].Find.ShouldBe("search_text");
        result["MinimalRule"].Replace.ShouldBeNull();
    }

    [Fact]
    public void Should_Handle_Rule_With_Only_FilePattern()
    {
        // Arrange
        var yaml = @"
FilePatternOnly:
  file-pattern: '*.log'
";

        // Act
        var result = DeserializeYaml<Dictionary<string, ReplacementOption>>(yaml);

        // Assert
        result.ShouldNotBeNull();
        result["FilePatternOnly"].FilePattern.ShouldBe("*.log");
        result["FilePatternOnly"].Find.ShouldBeNull();
        result["FilePatternOnly"].Replace.ShouldBeNull();
    }

    #endregion

    #region Multiple Replacement Rules

    [Fact]
    public void Should_Handle_Multiple_Replacement_Rules_In_Order()
    {
        // Arrange
        var yaml = @"
FirstReplacement:
  file-pattern: appsettings.json
  find: 'Development'
  replace: 'Staging'

SecondReplacement:
  file-pattern: appsettings.json
  find: 'localhost'
  replace: 'staging-server'

ThirdReplacement:
  file-pattern: '*.cs'
  find: 'namespace OldCompany'
  replace: 'namespace NewCompany'

FourthReplacement:
  file-pattern: '*.csproj'
  find: '<Version>1.0.0</Version>'
  replace: '<Version>2.0.0</Version>'
";

        // Act
        var result = DeserializeYaml<Dictionary<string, ReplacementOption>>(yaml);

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(4);

        // Verify each rule's properties
        result["FirstReplacement"].Find.ShouldBe("Development");
        result["FirstReplacement"].Replace.ShouldBe("Staging");
        result["FirstReplacement"].FilePattern.ShouldBe("appsettings.json");

        result["SecondReplacement"].Find.ShouldBe("localhost");
        result["SecondReplacement"].Replace.ShouldBe("staging-server");
        result["SecondReplacement"].FilePattern.ShouldBe("appsettings.json");

        result["ThirdReplacement"].Find.ShouldBe("namespace OldCompany");
        result["ThirdReplacement"].Replace.ShouldBe("namespace NewCompany");
        result["ThirdReplacement"].FilePattern.ShouldBe("*.cs");

        result["FourthReplacement"].Find.ShouldBe("<Version>1.0.0</Version>");
        result["FourthReplacement"].Replace.ShouldBe("<Version>2.0.0</Version>");
        result["FourthReplacement"].FilePattern.ShouldBe("*.csproj");
    }

    #endregion

    #region Special Characters Escaping

    [Fact]
    public void Should_Handle_Backslash_Escaping_In_Find_Pattern()
    {
        // Arrange
        var yaml = @"
BackslashTest:
  file-pattern: '*.json'
  find: 'Server=(LocalDb)\\\\MSSQLLocalDB'
  replace: 'Server=localhost'
";

        // Act
        var result = DeserializeYaml<Dictionary<string, ReplacementOption>>(yaml);

        // Assert
        result.ShouldNotBeNull();
        // In YAML single quotes, all backslashes are preserved literally
        // The C# @"\\\\\\ becomes YAML \\\\ which is preserved as 4 backslashes
        result["BackslashTest"].Find.ShouldBe("Server=(LocalDb)\\\\\\\\MSSQLLocalDB");
        result["BackslashTest"].Replace.ShouldBe("Server=localhost");
    }

    [Fact]
    public void Should_Handle_Special_Characters_In_Replace_Pattern()
    {
        // Arrange
        var yaml = @"
SpecialChars:
  file-pattern: '*.cs'
  find: 'TODO'
  replace: 'FIXME: High priority - $1'
";

        // Act
        var result = DeserializeYaml<Dictionary<string, ReplacementOption>>(yaml);

        // Assert
        result.ShouldNotBeNull();
        result["SpecialChars"].Find.ShouldBe("TODO");
        result["SpecialChars"].Replace.ShouldBe("FIXME: High priority - $1");
    }

    [Fact]
    public void Should_Handle_Quotes_In_Replacement_Patterns()
    {
        // Arrange
        var yaml = @"
QuotesTest:
  file-pattern: '*.json'
  find: '""value""'
  replace: '''new_value'''
";

        // Act
        var result = DeserializeYaml<Dictionary<string, ReplacementOption>>(yaml);

        // Assert
        result.ShouldNotBeNull();
        result["QuotesTest"].Find.ShouldBe("\"value\"");
        result["QuotesTest"].Replace.ShouldBe("'new_value'");
    }

    [Fact]
    public void Should_Handle_Connection_String_Escaping()
    {
        // Arrange
        var yaml = @"
ConnectionString:
  file-pattern: appsettings.json
  find: 'Server=(LocalDb)\\\\MSSQLLocalDB;Trusted_Connection=True;'
  replace: 'Server=localhost;User ID=sa;Password=P@ssw0rd;'
";

        // Act
        var result = DeserializeYaml<Dictionary<string, ReplacementOption>>(yaml);

        // Assert
        result.ShouldNotBeNull();
        var rule = result["ConnectionString"];
        // In YAML single quotes, all backslashes are preserved literally
        rule.Find.ShouldBe("Server=(LocalDb)\\\\\\\\MSSQLLocalDB;Trusted_Connection=True;");
        rule.Replace.ShouldBe("Server=localhost;User ID=sa;Password=P@ssw0rd;");
    }

    [Fact]
    public void Should_Handle_Regex_Capture_Groups_With_Dollar_Sign()
    {
        // Arrange
        var yaml = @"
DollarSignReplacement:
  file-pattern: '*.txt'
  find: '(\d+)'
  replace: '$$1.00'
";

        // Act
        var result = DeserializeYaml<Dictionary<string, ReplacementOption>>(yaml);

        // Assert
        result.ShouldNotBeNull();
        result["DollarSignReplacement"].Find.ShouldBe(@"(\d+)");
        result["DollarSignReplacement"].Replace.ShouldBe("$$1.00");
    }

    #endregion

    #region Serialization Round-Trip

    [Fact]
    public void Should_Serialize_And_Deserialize_Replacement_Rules_Correctly()
    {
        // Arrange
        var originalRules = new Dictionary<string, ReplacementOption>
        {
            {
                "TestRule1", new ReplacementOption
                {
                    FilePattern = "*.json",
                    Find = "old-value",
                    Replace = "new-value"
                }
            },
            {
                "TestRule2", new ReplacementOption
                {
                    FilePattern = "**/*.cs",
                    Find = @"namespace Old",
                    Replace = @"namespace New"
                }
            }
        };

        // Act
        var yaml = SerializeYaml(originalRules);
        var deserializedRules = DeserializeYaml<Dictionary<string, ReplacementOption>>(yaml);

        // Assert
        deserializedRules.ShouldNotBeNull();
        deserializedRules.Count.ShouldBe(2);

        deserializedRules["TestRule1"].FilePattern.ShouldBe("*.json");
        deserializedRules["TestRule1"].Find.ShouldBe("old-value");
        deserializedRules["TestRule1"].Replace.ShouldBe("new-value");

        deserializedRules["TestRule2"].FilePattern.ShouldBe("**/*.cs");
        deserializedRules["TestRule2"].Find.ShouldBe(@"namespace Old");
        deserializedRules["TestRule2"].Replace.ShouldBe(@"namespace New");
    }

    [Fact]
    public void Should_Preserve_All_Properties_During_Round_Trip()
    {
        // Arrange
        var originalRule = new Dictionary<string, ReplacementOption>
        {
            {
                "ComplexRule", new ReplacementOption
                {
                    FilePattern = "src/**/*.cs",
                    Find = @"(\w+)\.Obsolete",
                    Replace = "$1.Deprecated"
                }
            }
        };

        // Act
        var yaml = SerializeYaml(originalRule);
        var deserializedRule = DeserializeYaml<Dictionary<string, ReplacementOption>>(yaml);

        // Assert
        deserializedRule.ShouldNotBeNull();
        deserializedRule["ComplexRule"].FilePattern.ShouldBe("src/**/*.cs");
        deserializedRule["ComplexRule"].Find.ShouldBe(@"(\w+)\.Obsolete");
        deserializedRule["ComplexRule"].Replace.ShouldBe("$1.Deprecated");
    }

    #endregion
}
