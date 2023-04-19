namespace AbpDevTools.Configuration;
public class ReplacementOption
{
    public string FilePattern { get; set; }
    public string Find { get; set; }
    public string Replace { get; set; }

    public static List<ReplacementOption> GetDefaults()
        => new List<ReplacementOption>
        {
            new ReplacementOption
            {
                FilePattern = "appsettings.json",
                Find = "Trusted_Connection=True;",
                Replace = "User  ID=SA;Password=12345678Aa;"
            }
        };
}
