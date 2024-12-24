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
}
