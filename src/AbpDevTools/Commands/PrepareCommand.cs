using CliFx.Exceptions;
using CliFx.Infrastructure;
using Spectre.Console;
using System.Diagnostics;
using System.Threading;

namespace AbpDevTools.Commands;

[Command("prepare", Description = "Prepare the project for the first running on this machine. Creates database, redis, event bus containers.")]
public class PrepareCommand : ICommand
{
    protected IConsole? console;

    [CommandParameter(0, IsRequired = false, Description = "Working directory to run build. Probably project or solution directory path goes here. Default: . (Current Directory)")]
    public string? WorkingDirectory { get; set; }

    protected EnvironmentAppStartCommand EnvironmentAppStartCommand { get; }

    protected AbpBundleCommand AbpBundleCommand { get; }

    public PrepareCommand(EnvironmentAppStartCommand environmentAppStartCommand, AbpBundleCommand abpBundleCommand)
    {
        EnvironmentAppStartCommand = environmentAppStartCommand;
        AbpBundleCommand = abpBundleCommand;
    }

    public async ValueTask ExecuteAsync(IConsole console)
    {
        this.console = console;
        if (string.IsNullOrEmpty(WorkingDirectory))
        {
            WorkingDirectory = Directory.GetCurrentDirectory();
        }

        AbpBundleCommand.WorkingDirectory = WorkingDirectory;

        var environmentApps = new List<string>();
        var installLibsFolders = new List<string>();
        var bundleFolders = new List<string>();

        foreach (var csproj in GetProjects())
        {
            var csprojContent = await File.ReadAllTextAsync(csproj.FullName);

            environmentApps.AddRange(CheckEnvironmentApps(csprojContent));
        }

        if (environmentApps.Count > 0)
        {
            EnvironmentAppStartCommand.AppNames = environmentApps.Distinct().ToArray();
            await EnvironmentAppStartCommand.ExecuteAsync(console);
        }

        var process = Process.Start(new ProcessStartInfo
        {
            FileName = "abp",
            Arguments = "install-libs",
            WorkingDirectory = WorkingDirectory,
            RedirectStandardOutput = true
        }) ?? throw new CommandException("Failed to start 'abp install-libs' process");

        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        console.RegisterCancellationHandler().Register(() =>
        {
            console.Output.WriteLine("Abp install-libs cancelled.");
            process.Kill();
            cts.Cancel();
        });

        try 
        {
            await process.WaitForExitAsync(cts.Token);
            
            if (process.ExitCode != 0)
            {
                throw new CommandException($"'abp install-libs' failed with exit code: {process.ExitCode}");
            }
        }
        catch (OperationCanceledException)
        {
            throw new CommandException("'abp install-libs' operation timed out or was cancelled.");
        }

        await AbpBundleCommand.ExecuteAsync(console);
    }

    private IEnumerable<string> CheckEnvironmentApps(string csprojContent)
    {
        if (csprojContent.Contains("PackageReference Include=\"Volo.Abp.EntityFrameworkCore.SqlServer\""))
        {
            yield return "sqlserver-edge";
        }

        if (csprojContent.Contains("PackageReference Include=\"Volo.Abp.EntityFrameworkCore.MySQL\""))
        {
            yield return "mysql";
        }

        if (csprojContent.Contains("PackageReference Include=\"Volo.Abp.EntityFrameworkCore.PostgreSql\""))
        {
            yield return "postgreSql";
        }

        if (csprojContent.Contains("PackageReference Include=\"Volo.Abp.Caching.StackExchangeRedis\""))
        {
            yield return "redis";
        }
    }

    private IEnumerable<FileInfo> GetProjects()
    {
        try 
        {
            return Directory.EnumerateFiles(WorkingDirectory!, "*.csproj", SearchOption.AllDirectories)
                        .Select(x => new FileInfo(x))
                        .ToArray();
        }
        catch (Exception ex) when (ex is DirectoryNotFoundException || ex is UnauthorizedAccessException)
        {
            throw new CommandException($"Failed to enumerate project files: {ex.Message}");
        }
    }
}
