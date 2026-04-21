using System.Text.RegularExpressions;
using YamlDotNet.Serialization;

namespace AbpDevTools.Configuration;

[RegisterTransient]
public class ReplacementConfiguration : ConfigurationBase<Dictionary<string, ReplacementOption>>
{
    public ReplacementConfiguration(IDeserializer yamlDeserializer, ISerializer yamlSerializer) : base(yamlDeserializer, yamlSerializer)
    {
    }

    public override string FileName => "replacements";

    protected override string LegacyJsonFilePath => Path.Combine(FolderPath, "replacements.json");

    public override Dictionary<string, ReplacementOption> GetOptions()
    {
        NormalizeWildcardFilePatternsInConfig();
        return base.GetOptions()!;
    }

    protected virtual string NormalizeWildcardFilePatterns(string yamlContent)
    {
        return Regex.Replace(
            yamlContent,
            "(?m)^(\\s*file-pattern\\s*:\\s*)(?!['\"])(\\*[^#\\r\\n]*)(\\s*(?:#.*)?)$",
            match => $"{match.Groups[1].Value}\"{match.Groups[2].Value.TrimEnd()}\"{match.Groups[3].Value}"
        );
    }

    protected override Dictionary<string, ReplacementOption> GetDefaults()
    {
        return new Dictionary<string, ReplacementOption>
        {
            {
                "ConnectionStrings", new ReplacementOption
                {
                    FilePattern = "appsettings.json",
                    Find = "Trusted_Connection=True;",
                    Replace = "User ID=SA;Password=12345678Aa;"
                }
            },
            {
                "LocalDb", new ReplacementOption
                {
                    FilePattern = "appsettings.json",
                    Find = "Server=(LocalDb)\\\\MSSQLLocalDB;",
                    Replace = "Server=localhost;"
                }
            }
        };
    }

    private void NormalizeWildcardFilePatternsInConfig()
    {
        if (!File.Exists(FilePath))
        {
            return;
        }

        var yamlContent = File.ReadAllText(FilePath);
        var normalizedYamlContent = NormalizeWildcardFilePatterns(yamlContent);

        if (!string.Equals(yamlContent, normalizedYamlContent, StringComparison.Ordinal))
        {
            File.WriteAllText(FilePath, normalizedYamlContent);
        }
    }
}
