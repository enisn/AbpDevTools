namespace AbpDevTools.Services;
public class UpdateCheckResult
{
    public UpdateCheckResult(
        bool updateAvailable,
        string currentVersion,
        string latestVersion,
        DateTime lastCheckDate)
    {
        UpdateAvailable = updateAvailable;
        CurrentVersion = currentVersion;
        LatestVersion = latestVersion;
        LastCheckDate = lastCheckDate;
    }

    public bool UpdateAvailable { get; }
    public string CurrentVersion { get; }
    public string LatestVersion { get; }
    public DateTime LastCheckDate { get; }
}
