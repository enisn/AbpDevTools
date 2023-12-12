namespace AbpDevTools.Configuration;

[RegisterTransient]
public class ReplacementConfiguration : ConfigurationBase<Dictionary<string, ReplacementOption>>
{
    public override string FilePath => Path.Combine(FolderPath, "replacements.json");

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
