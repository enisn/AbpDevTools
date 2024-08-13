using AbpDevTools.Commands;
using System.Net.Http.Json;
using System.Text.Json;

namespace AbpDevTools.Services;

[RegisterTransient]
public class UpdateChecker
{
    public static string FolderPath => Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "abpdev");

    public static string FilePath => Path.Combine(FolderPath, "updates.json");

    public async Task<UpdateCheckResult> CheckAsync(bool force = false)
    {
        var data = await GetDataAsync();


        if (force || DateTime.Now >= data.NextCheck)
        {
            using var httpClient = new HttpClient();

            var nugetResponse = await httpClient

            var latestVersion = nugetResponse["versions"].Where(x => !x.Contains("-")).Last();

            data.LastChechLatestVersion = latestVersion;
            data.LastCheck = DateTime.Now;
            data.NextCheck = DateTime.Now.AddDays(1);
            await SaveDataAsync(data);

            return new UpdateCheckResult(
                currentVersion < Version.Parse(latestVersion),
                currentVersion.ToString(),
                latestVersion,
                DateTime.Now);
        }

        return new UpdateCheckResult(
            currentVersion < Version.Parse(data.LastChechLatestVersion),
            currentVersion.ToString(),
            data.LastChechLatestVersion,
            data.LastCheck);
    }

    private async Task<UpdatesData> GetDataAsync()
    {
        if (!File.Exists(FilePath))
        {
            await SaveDataAsync(new UpdatesData());
        }

        var json = await File.ReadAllTextAsync(FilePath);
        return JsonSerializer.Deserialize<UpdatesData>(json);
    }

    private async Task SaveDataAsync(UpdatesData data)
    {
        if (!Directory.Exists(FolderPath))
        {
            Directory.CreateDirectory(FolderPath);
        }
        await File.WriteAllTextAsync(FilePath, JsonSerializer.Serialize(data));
    }

    public class UpdatesData
    {
        public DateTime LastCheck { get; set; } = DateTime.Now;
        public DateTime NextCheck { get; set; } = DateTime.Now;
        public string LastChechLatestVersion { get; set; } = "1.0.0";
    }
}
