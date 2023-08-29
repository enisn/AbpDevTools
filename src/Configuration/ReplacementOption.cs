namespace AbpDevTools.Configuration;
public class ReplacementOption
{
    public string FilePattern { get; set; }
    public string Find { get; set; }
    public string Replace { get; set; }

    public static Dictionary<string, ReplacementOption> GetDefaults()
        => new Dictionary<string, ReplacementOption>
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
