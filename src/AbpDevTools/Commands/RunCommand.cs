using AbpDevTools.Configuration;
using AbpDevTools.Environments;
using AbpDevTools.LocalConfigurations;
using AbpDevTools.Notifications;
using AbpDevTools.Services;
using CliFx.Infrastructure;
using Spectre.Console;
using System.Diagnostics;

namespace AbpDevTools.Commands;

[Command("run", Description = "Run all the required applications")]
public partial class RunCommand : ICommand
{
    [CommandParameter(0, IsRequired = false, Description = "Working directory to run build. Probably project or solution directory path goes here. Default: . (Current Directory)")]
    public string? WorkingDirectory { get; set; }

    [CommandOption("watch", 'w', Description = "Watch mode")]
    public bool Watch { get; set; }

    [CommandOption("skip-migrate", Description = "Skips migration and runs projects directly.")]
    public bool SkipMigration { get; set; }

    [CommandOption("all", 'a', Description = "Projects to run will not be asked as prompt. All of them will run.")]
    public bool RunAll { get; set; }

    [CommandOption("no-build", Description = "Skips build before running. Passes '--no-build' parameter to dotnet run.")]
    public bool NoBuild { get; set; }

    [CommandOption("install-libs", 'i', Description = "Runs 'abp install-libs' command while running the project simultaneously.")]
    public bool InstallLibs { get; set; }

    [CommandOption("skip-check-libs", Description = "Skips library installation check before running.")]
    public bool SkipCheckLibs { get; set; }

    [CommandOption("graphBuild", 'g', Description = "Uses /graphBuild while running the applications. So no need building before running. But it may cause some performance.")]
    public bool GraphBuild { get; set; }

    [CommandOption("projects", 'p', Description = "(Array) Names or part of names of projects will be ran.")]
    public string[] Projects { get; set; } = Array.Empty<string>();

    [CommandOption("configuration", 'c')]
    public string? Configuration { get; set; }

    [CommandOption("env", 'e', Description = "Uses the virtual environment for this process. Use 'abpdev env config' command to see/manage environments.")]
    public string? EnvironmentName { get; set; }

    [CommandOption("retry", 'r', Description = "Retries running again when application exits.")]
    public bool Retry { get; set; }

    [CommandOption("verbose", 'v', Description = "Shows verbose output from the projects.")]
    public bool Verbose { get; set; }

    [CommandOption("yml", Description = "Path to the yml file to be used for running the project.")]
    public string? YmlPath { get; set; }

    protected IConsole? console;

    protected readonly List<RunningProjectItem> runningProjects = new();
    private volatile bool _processesKilled = false;
    private readonly object _killLock = new();

    protected readonly INotificationManager notificationManager;
    protected readonly MigrateCommand migrateCommand;
    protected readonly IProcessEnvironmentManager environmentManager;
    protected readonly UpdateCheckCommand updateCheckCommand;
    protected readonly RunnableProjectsProvider runnableProjectsProvider;
    protected readonly ToolsConfiguration toolsConfiguration;
    protected readonly FileExplorer fileExplorer;
    private readonly LocalConfigurationManager localConfigurationManager;
    private readonly IKeyInputManager keyInputManager;
    private int lastWindowWidth = 0;

    public RunCommand(
        INotificationManager notificationManager,
        MigrateCommand migrateCommand,
        IProcessEnvironmentManager environmentManager,
        UpdateCheckCommand updateCheckCommand,
        RunnableProjectsProvider runnableProjectsProvider,
        ToolsConfiguration toolsConfiguration,
        FileExplorer fileExplorer,
        LocalConfigurationManager localConfigurationManager,
        IKeyInputManager keyInputManager)
    {
        this.notificationManager = notificationManager;
        this.migrateCommand = migrateCommand;
        this.environmentManager = environmentManager;
        this.updateCheckCommand = updateCheckCommand;
        this.runnableProjectsProvider = runnableProjectsProvider;
        this.toolsConfiguration = toolsConfiguration;
        this.fileExplorer = fileExplorer;
        this.localConfigurationManager = localConfigurationManager;
        this.keyInputManager = keyInputManager;
    }

    public async ValueTask ExecuteAsync(IConsole console)
    {
        this.console = console;
        var canUseInteractiveConsole = global::AbpDevTools.ConsoleSupport.SupportsInteractiveConsole(console);

        if (string.IsNullOrEmpty(WorkingDirectory))
        {
            WorkingDirectory = Directory.GetCurrentDirectory();
        }

        if (string.IsNullOrEmpty(YmlPath))
        {
            YmlPath = Path.Combine(WorkingDirectory, "abpdev.yml");
        }

        var cancellationToken = console.RegisterCancellationHandler();

        if (localConfigurationManager.TryLoad(YmlPath!, out var localRootConfig, FileSearchDirection.OnlyCurrent))
        {
            console.Output.WriteLine($"Loaded YAML configuration from '{YmlPath}' with environment '{localRootConfig?.Environment?.Name ?? "Default"}'.");
        }

        RunnableAppInfo[] runnableApps = await AnsiConsole.Status()
            .StartAsync("Looking for runnable applications", async ctx =>
            {
                ctx.Spinner(Spinner.Known.SimpleDotsScrolling);

                await Task.Yield();

                return runnableProjectsProvider.GetRunnableApplications(WorkingDirectory, localRootConfig?.Run?.Npm?.Scripts);
            });

        var dotnetAppCount = runnableApps.Count(x => x.Type == RunnableAppType.DotNet);
        var npmAppCount = runnableApps.Count(x => x.Type == RunnableAppType.Npm);

        await console.Output.WriteLineAsync($"{dotnetAppCount} csproj file(s), {npmAppCount} npm app(s) found.");

        if (runnableApps.Length == 0)
        {
            await console.Output.WriteLineAsync("No runnable application found.");
            return;
        }

        if (dotnetAppCount > 0 && !SkipMigration && localRootConfig?.Run?.SkipMigrate != true)
        {
            migrateCommand.WorkingDirectory = this.WorkingDirectory;
            migrateCommand.NoBuild = this.NoBuild;
            migrateCommand.EnvironmentName = this.EnvironmentName;
            migrateCommand.RunAll = this.RunAll;
            migrateCommand.Projects = this.Projects;

            await migrateCommand.ExecuteAsync(console);
        }

        await console.Output.WriteLineAsync("Starting projects...");

        var runnableTargets = runnableApps
            .Where(x => x.Type != RunnableAppType.DotNet || !x.Name.Contains(".DbMigrator"))
            .ToArray();

        ApplyLocalProjects(localRootConfig);

        if (Projects.Length > 0)
        {
            runnableTargets = runnableTargets
                .Where(x => Projects.Any(project => MatchesProjectFilter(x, project)))
                .ToArray();
        }
        else if (RunAll)
        {
            runnableTargets = FilterAutomaticallyRunnableTargets(runnableTargets);
        }
        else if (runnableTargets.Length > 1 || runnableTargets.Any(x => !x.IsRunByDefault))
        {
            await console.Output.WriteLineAsync($"\n");

            if (canUseInteractiveConsole)
            {
                var selectedProjects = AnsiConsole.Prompt(
                    new MultiSelectionPrompt<RunnableAppInfo>()
                        .Title("Choose [mediumpurple2]projects[/] to run.")
                        .Required(true)
                        .PageSize(12)
                        .HighlightStyle(new Style(foreground: Color.MediumPurple2))
                        .MoreChoicesText("[grey](Move up and down to reveal more projects)[/]")
                        .InstructionsText(
                            "[grey](Press [mediumpurple2]<space>[/] to toggle a project, " +
                            "[green]<enter>[/] to accept)[/]")
                        .UseConverter(GetRunnableAppDisplayName)
                        .AddChoices(runnableTargets));

                runnableTargets = selectedProjects.ToArray();
            }
            else
            {
                await console.Output.WriteLineAsync("Interactive project selection is unavailable; running automatically detected projects. Use '--projects' to limit the selection or include ambiguous npm scripts.");
                runnableTargets = FilterAutomaticallyRunnableTargets(runnableTargets);
            }
        }

        if (runnableTargets.Length == 0)
        {
            await console.Output.WriteLineAsync("No runnable application selected.");
            return;
        }

        var projectFiles = runnableTargets
            .Where(x => x.Type == RunnableAppType.DotNet)
            .Select(x => new FileInfo(x.FullName))
            .ToArray();

        // Check for missing libs before starting projects
        bool shouldInstallLibs = InstallLibs; // Start with explicit flag

        if (!SkipCheckLibs && localRootConfig?.Run?.SkipCheckLibs != true)
        {
            var projectsNeedingLibs = new List<FileInfo>();

            foreach (var csproj in projectFiles)
            {
                var projectDir = Path.GetDirectoryName(csproj.FullName)!;

                if (!File.Exists(Path.Combine(projectDir, "package.json")))
                {
                    continue;
                }

                var wwwRootLibs = Path.Combine(projectDir, "wwwroot", "libs");

                if (!Directory.Exists(wwwRootLibs) || !Directory.EnumerateFileSystemEntries(wwwRootLibs).Any())
                {
                    projectsNeedingLibs.Add(csproj);
                }
            }

            if (projectsNeedingLibs.Count > 0)
            {
                var projectList = string.Join("\n  - ", projectsNeedingLibs.Select(p => p.Name));
                await console.Output.WriteLineAsync($"\n[yellow]Warning: The following projects are missing wwwroot/libs:[/]");
                await console.Output.WriteLineAsync($"  - {projectList}");

                if (!shouldInstallLibs)
                {
                    shouldInstallLibs = global::AbpDevTools.ConsoleSupport.ConfirmOrDefault(
                        console,
                        "\n[yellow]Would you like to install libs for these projects?[/]",
                        defaultValue: false,
                        fallbackMessage: "Interactive confirmation is unavailable; skipping 'abp install-libs'. Pass '--install-libs' to run it automatically.");
                }
            }
        }

        // Register cleanup handlers for all exit scenarios
        void ProcessExitHandler(object? sender, EventArgs e) => KillRunningProcesses();
        AppDomain.CurrentDomain.ProcessExit += ProcessExitHandler;

        try
        {
            foreach (var runnableTarget in runnableTargets)
            {
                localConfigurationManager.TryLoad(runnableTarget.FullName, out var localConfiguration);

                if (runnableTarget.Type == RunnableAppType.DotNet)
                {
                    StartDotNetProject(runnableTarget, localConfiguration);

                    var projectDir = runnableTarget.WorkingDirectory;
                    var wwwRootLibs = Path.Combine(projectDir, "wwwroot", "libs");

                    if (shouldInstallLibs && File.Exists(Path.Combine(projectDir, "package.json")))
                    {
                        if (!Directory.Exists(wwwRootLibs))
                        {
                            Directory.CreateDirectory(wwwRootLibs);
                        }

                        if (!Directory.EnumerateFiles(wwwRootLibs).Any())
                        {
                            File.WriteAllText(Path.Combine(wwwRootLibs, "abplibs.installing"), string.Empty);
                        }

                        var tools = toolsConfiguration.GetOptions();
                        var installLibsStartInfo = new ProcessStartInfo(tools["abp"], "install-libs")
                        {
                            WorkingDirectory = projectDir,
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                        };

                        var installLibsRunninItem = new RunningInstallLibsItem(
                            runnableTarget.Name.Replace(".csproj", " install-libs"),
                            Process.Start(installLibsStartInfo)!,
                            installLibsStartInfo
                        );

                        runningProjects.Add(installLibsRunninItem);
                    }
                }
                else if (runnableTarget.Type == RunnableAppType.Npm)
                {
                    StartNpmProject(runnableTarget, localConfiguration);
                }
            }

            cancellationToken.Register(KillRunningProcesses);

            await RenderProcesses(cancellationToken, canUseInteractiveConsole);

            await updateCheckCommand.SoftCheckAsync(console);
        }
        finally
        {
            // Always kill processes on exit, regardless of how we exit
            // Reset flag to ensure cleanup runs even if called before
            _processesKilled = false;
            KillRunningProcesses();
            AppDomain.CurrentDomain.ProcessExit -= ProcessExitHandler;
        }
    }

    private void ApplyLocalProjects(LocalConfiguration? localConfiguration)
    {
        if(localConfiguration is not null)
        {
            if (Projects.Length == 0 && localConfiguration?.Run?.Projects.Length > 0)
            {
                Projects = localConfiguration.Run.Projects;
            }
        }
    }

    private void StartDotNetProject(RunnableAppInfo runnableTarget, LocalConfiguration? localConfiguration)
    {
        var commandPrefix = BuildCommandPrefix(localConfiguration?.Run?.Watch);
        var commandSuffix = BuildCommandSuffix(
            localConfiguration?.Run?.NoBuild,
            localConfiguration?.Run?.GraphBuild,
            localConfiguration?.Run?.Configuration);

        var tools = toolsConfiguration.GetOptions();
        var startInfo = new ProcessStartInfo(tools["dotnet"], commandPrefix + $"run --project \"{runnableTarget.FullName}\"" + commandSuffix)
        {
            WorkingDirectory = runnableTarget.WorkingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        localConfigurationManager.ApplyLocalEnvironmentForProcess(runnableTarget.FullName, startInfo, localConfiguration);

        if (!string.IsNullOrEmpty(EnvironmentName))
        {
            environmentManager.SetEnvironmentForProcess(EnvironmentName, startInfo);
        }

        runningProjects.Add(
            new RunningCsProjItem(
                runnableTarget.Name,
                Process.Start(startInfo)!,
                startInfo,
                verbose: Verbose
            )
        );
    }

    private void StartNpmProject(RunnableAppInfo runnableTarget, LocalConfiguration? localConfiguration)
    {
        var packageManager = runnableTarget.PackageManager ?? "npm";
        var script = runnableTarget.Script ?? throw new InvalidOperationException("Runnable npm target does not define a script.");
        var tools = toolsConfiguration.GetOptions();
        var executable = tools.TryGetValue(packageManager, out var configuredExecutable)
            ? configuredExecutable
            : packageManager;
        executable = ResolveExecutablePath(executable);

        var startInfo = new ProcessStartInfo(executable, $"run {QuoteArgument(script)}")
        {
            WorkingDirectory = runnableTarget.WorkingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        localConfigurationManager.ApplyLocalEnvironmentForProcess(runnableTarget.FullName, startInfo, localConfiguration);

        if (!string.IsNullOrEmpty(EnvironmentName))
        {
            environmentManager.SetEnvironmentForProcess(EnvironmentName, startInfo);
        }

        EnsureNpmProcessDoesNotStealDashboardShortcuts(startInfo);

        runningProjects.Add(
            new RunningNpmProjectItem(
                GetRunnableAppDisplayName(runnableTarget),
                Process.Start(startInfo)!,
                startInfo,
                verbose: Verbose
            )
        );
    }

    private RunnableAppInfo[] FilterAutomaticallyRunnableTargets(RunnableAppInfo[] runnableTargets)
    {
        var skippedTargets = runnableTargets.Where(x => !x.IsRunByDefault).ToArray();
        if (skippedTargets.Length > 0)
        {
            console?.Output.WriteLine("Skipping ambiguous npm script(s): " + string.Join(", ", skippedTargets.Select(GetRunnableAppDisplayName)) + ". Use '--projects' or abpdev.yml run:npm:scripts to include them.");
        }

        return runnableTargets.Where(x => x.IsRunByDefault).ToArray();
    }

    private bool MatchesProjectFilter(RunnableAppInfo runnableTarget, string filter)
    {
        return ContainsFilter(runnableTarget.Name, filter) ||
               ContainsFilter(runnableTarget.FullName, filter) ||
               ContainsFilter(runnableTarget.WorkingDirectory, filter) ||
               ContainsFilter(GetRunnableAppDisplayName(runnableTarget), filter) ||
               ContainsFilter(runnableTarget.Script, filter);
    }

    private string GetRunnableAppDisplayName(RunnableAppInfo runnableTarget)
    {
        if (runnableTarget.Type == RunnableAppType.DotNet)
        {
            return runnableTarget.Name;
        }

        var relativePath = Path.GetRelativePath(WorkingDirectory!, runnableTarget.WorkingDirectory);
        if (relativePath == ".")
        {
            relativePath = Path.GetFileName(runnableTarget.WorkingDirectory);
        }

        return $"{NormalizePath(relativePath)}:{runnableTarget.Script}";
    }

    private static bool ContainsFilter(string? value, string filter)
    {
        return value?.Contains(filter, StringComparison.InvariantCultureIgnoreCase) == true;
    }

    private static string NormalizePath(string path)
    {
        return path.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');
    }

    private static string QuoteArgument(string value)
    {
        return "\"" + value.Replace("\"", "\\\"") + "\"";
    }

    internal static string ResolveExecutablePath(string executable, string? pathValue = null)
    {
        if (string.IsNullOrWhiteSpace(executable))
        {
            return executable;
        }

        executable = executable.Trim('"');

        if (Path.IsPathRooted(executable) ||
            executable.Contains(Path.DirectorySeparatorChar) ||
            executable.Contains(Path.AltDirectorySeparatorChar))
        {
            return executable;
        }

        foreach (var path in (pathValue ?? Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
                     .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            foreach (var candidateName in GetExecutableCandidateNames(executable))
            {
                var candidatePath = Path.Combine(path.Trim('"'), candidateName);
                if (File.Exists(candidatePath))
                {
                    return candidatePath;
                }
            }
        }

        return executable;
    }

    private static IEnumerable<string> GetExecutableCandidateNames(string executable)
    {
        yield return executable;

        if (!OperatingSystem.IsWindows() || Path.HasExtension(executable))
        {
            yield break;
        }

        foreach (var extension in (Environment.GetEnvironmentVariable("PATHEXT") ?? ".COM;.EXE;.BAT;.CMD")
                     .Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            yield return executable + extension;
        }
    }

    internal static void EnsureNpmProcessDoesNotStealDashboardShortcuts(ProcessStartInfo startInfo)
    {
        // Vite and similar dev servers bind stdin shortcuts when CI is not set.
        // Keep abpdev's dashboard shortcuts responsive without redirecting stdin.
        if (!startInfo.Environment.ContainsKey("CI"))
        {
            startInfo.Environment["CI"] = "true";
        }
    }

    private string BuildCommandSuffix(bool? noBuild = null, bool? graphBuild = null, string? configuration = null)
    {
        var commandSuffix = (NoBuild || noBuild == true) ? " --no-build" : string.Empty;

        if (GraphBuild || graphBuild == true)
        {
            commandSuffix += " /graphBuild";
        }

        if (configuration != null)
        {
            commandSuffix += $" --configuration {configuration}";
        }
        else if (!string.IsNullOrEmpty(Configuration))
        {
            commandSuffix += $" --configuration {Configuration}";
        }

        return commandSuffix;
    }

    private string BuildCommandPrefix(bool? watchOverride)
    {
        if (watchOverride is not null)
        {
            return watchOverride.Value ? "watch " : string.Empty;
        }
        return Watch ? "watch " : string.Empty;
    }

    private async Task RenderProcesses(CancellationToken cancellationToken, bool canUseInteractiveConsole)
    {
        if (!canUseInteractiveConsole)
        {
            await RenderProcessesWithoutInteractiveConsole(cancellationToken);
            return;
        }

        var keyCommandHandler = new KeyCommandHandler(runningProjects, console!, cancellationToken);

        // Start key input listening
        keyInputManager.StartListening();

        var restartLive = false;
        KeyPressEventArgs? pendingAction = null;
        var exitRequested = false;

        do
        {
            restartLive = false;
            ClearConsoleIfNeeded();

            var table = new Table().Border(TableBorder.Rounded);

            await AnsiConsole.Live(table)
              .StartAsync(async ctx =>
              {
                  table.AddColumn("Project").AddColumn("Status");

                  foreach (var project in runningProjects)
                  {
                      table.AddRow(project.Name!, project.Status!);
                  }
                  
                  // Add help section
                  table.AddRow("", "");
                  table.AddRow(BuildHelpSection(keyCommandHandler), "");
                  
                  ctx.Refresh();

                  while (!cancellationToken.IsCancellationRequested)
                  {
                      if (keyCommandHandler.IsInnerCommandInProgress)
                      {
                          continue;
                      }

                      // Check for key input
                      var keyEvent = keyInputManager.TryGetNextKey();
                      if (keyEvent != null)
                      {
                          var requiresLiveRestart = keyCommandHandler.RequiresLiveRestart(keyEvent);

                          if (requiresLiveRestart)
                          {
                              // Defer handling until after Live ends to avoid interleaving
                              pendingAction = keyEvent;
                              restartLive = true;
                              break;
                          }

                          var shouldContinue = await keyCommandHandler.HandleKeyPress(keyEvent);
                          if (!shouldContinue)
                          {
                              exitRequested = true;
                              break; // Exit requested by handler
                          }
                      }

#if DEBUG
                      await Task.Delay(100);
#else
                      await Task.Delay(500);
#endif
                      table.Rows.Clear();
                      ClearConsoleIfNeeded();

                      foreach (var project in runningProjects)
                      {
                          if (project.IsCompleted)
                          {
                              table.AddRow(project.Name!, $"[green]*[/] {project.Status}");
                          }
                          else
                          {
                              if (project.Process!.HasExited && !project.Queued)
                              {
                                  project.Status = $"[red]*[/] Exited({project.Process.ExitCode})";

                                  if (Retry)
                                  {
                                      project.Status = $"[orange1]*[/] Exited({project.Process.ExitCode})";

                                      _ = RestartProject(project, cancellationToken); // fire and forget
                                  }
                              }
                              table.AddRow(project.Name!, project.Status!);
                          }
                      }
                      
                      // Re-add help section
                      table.AddRow("", "");
                      table.AddRow(BuildHelpSection(keyCommandHandler), "");

                      ctx.Refresh();
                  }
              });

            if (exitRequested)
            {
                break;
            }

            if (restartLive && pendingAction is not null)
            {
                AnsiConsole.Clear();
                await keyCommandHandler.HandleKeyPress(pendingAction);
                pendingAction = null;
            }

        } while (restartLive && !cancellationToken.IsCancellationRequested);

        // Stop key input listening
        keyInputManager.StopListening();
    }

    protected virtual async Task RenderProcessesWithoutInteractiveConsole(CancellationToken cancellationToken)
    {
        var lastStatuses = new Dictionary<RunningProjectItem, string?>();

        await console!.Output.WriteLineAsync("Interactive console features are unavailable; live dashboard and keybindings are disabled.");

        while (!cancellationToken.IsCancellationRequested)
        {
            var hasActiveProcesses = false;

            foreach (var project in runningProjects)
            {
                UpdateProjectStatusForNonInteractiveMode(project, cancellationToken);

                if (IsProjectActive(project))
                {
                    hasActiveProcesses = true;
                }

                if (!lastStatuses.TryGetValue(project, out var lastStatus) || !string.Equals(lastStatus, project.Status, StringComparison.Ordinal))
                {
                    await console.Output.WriteLineAsync($"- {project.Name}: {project.Status}");
                    lastStatuses[project] = project.Status;
                }
            }

            if (!hasActiveProcesses)
            {
                break;
            }

            try
            {
#if DEBUG
                await Task.Delay(100, cancellationToken);
#else
                await Task.Delay(500, cancellationToken);
#endif
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private static async Task RestartProject(RunningProjectItem project, CancellationToken cancellationToken = default)
    {
        project.Queued = true;

        try
        {
            await Task.Delay(3100, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            project.Queued = false;
            return;
        }

        if (cancellationToken.IsCancellationRequested)
        {
            project.Queued = false;
            return;
        }

        project.Status = $"[orange1]*[/] Exited({project.Process!.ExitCode}) (Retrying...)";

        try
        {
            project.Process = Process.Start(project.Process!.StartInfo)!;

            // Double-check cancellation after starting - if cancelled, kill immediately
            if (cancellationToken.IsCancellationRequested)
            {
                try
                {
                    project.Process?.Kill(entireProcessTree: true);
                }
                catch { }
                return;
            }

            project.StartReadingOutput();
        }
        catch (Exception)
        {
            project.Status = "[red]*[/] Failed to restart";
        }
    }

    private string BuildHelpSection(KeyCommandHandler keyCommandHandler)
    {
        return string.Join(" | ", keyCommandHandler.KeyCommandMappings.Select(kcm => $"[grey]{kcm.GetKeyDisplay()}[/] - {kcm.Name}"));
    }

    private void UpdateProjectStatusForNonInteractiveMode(RunningProjectItem project, CancellationToken cancellationToken)
    {
        if (project.Process is null || !project.Process.HasExited || project.Queued)
        {
            return;
        }

        if (project is RunningInstallLibsItem && project.IsCompleted)
        {
            return;
        }

        project.IsCompleted = false;
        project.Status = Retry
            ? $"[orange1]*[/] Exited({project.Process.ExitCode})"
            : $"[red]*[/] Exited({project.Process.ExitCode})";

        if (Retry)
        {
            _ = RestartProject(project, cancellationToken);
        }
    }

    private static bool IsProjectActive(RunningProjectItem project)
    {
        return project.Queued || project.Process?.HasExited == false;
    }

    protected void KillRunningProcesses()
    {
        // Ensure this is only executed once, even if called from multiple handlers
        lock (_killLock)
        {
            if (_processesKilled)
            {
                return;
            }
            _processesKilled = true;
        }

        try
        {
            console?.Output.WriteLine($"- Killing running {runningProjects.Count} processes...");
        }
        catch
        {
            // Console may not be available during shutdown
        }

        foreach (var project in runningProjects)
        {
            try
            {
                if (project.Process != null && !project.Process.HasExited)
                {
                    project.Process.Kill(entireProcessTree: true);
                    project.Process.WaitForExit(5000); // Wait up to 5 seconds
                }
            }
            catch (Exception)
            {
                // Process may have already exited or may not be accessible
                // Continue to next project
            }
        }
    }

    protected virtual void ClearConsoleIfNeeded()
    {
        if (!global::AbpDevTools.ConsoleSupport.TryGetWindowWidth(console, out var currentWindowWidth))
        {
            return;
        }

        if (lastWindowWidth != currentWindowWidth)
        {
            AnsiConsole.Clear();
            lastWindowWidth = currentWindowWidth;
        }
    }
}
