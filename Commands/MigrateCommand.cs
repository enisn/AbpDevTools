using CliFx.Infrastructure;
using System.Diagnostics;

namespace AbpDevTools.Commands;

[Command("migrate", Description = "Runs all .DbMigrator projects in folder recursively.")]
public class MigrateCommand : ICommand
{
    [CommandParameter(0, IsRequired = false, Description = "Working directory to run build. Probably project or solution directory path goes here. Default: . (Current Directory)")]
    public string WorkingDirectory { get; set; }

    protected readonly List<RunningProjectItem> runningProjects = new();

    protected IConsole console;

    public async ValueTask ExecuteAsync(IConsole console)
    {
        this.console = console;

        var dbMigrators = Directory.EnumerateFiles(WorkingDirectory, "*.csproj", SearchOption.AllDirectories)
           .Where(x => x.EndsWith("DbMigrator.csproj"))
           .Select(x => new FileInfo(x))
           .ToList();

        var cancellationToken = console.RegisterCancellationHandler();

        await console.Output.WriteLineAsync($"{dbMigrators.Count} db migrator(s) found.");

        foreach (var dbMigrator in dbMigrators)
        {
            runningProjects.Add(new RunningProjectItem
            {
                Name = dbMigrator.Name,
                Process = Process.Start(new ProcessStartInfo("dotnet", $"run --project {dbMigrator.FullName}")
                {
                    WorkingDirectory = Path.GetDirectoryName(dbMigrator.FullName)
                }),
                Status = "Building..."
            });
        }

        await console.Output.WriteAsync("Waiting for db migrators to finish...");

        await Task.WhenAll(runningProjects.Select(x => x.Process.WaitForExitAsync(cancellationToken)));

        await console.Output.WriteLineAsync("Migrations finished.");
    }
}
