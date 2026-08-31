using AbpDevTools.Services;
using CliFx.Infrastructure;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace AbpDevTools.Runner;

public enum RunnerDashboardResult
{
    Completed,
    Detached,
    Cancelled,
    Unavailable
}

public interface IRunnerDashboard
{
    Task<RunnerDashboardResult> RunAsync(
        string contextKey,
        IConsole console,
        CancellationToken cancellationToken);
}

[RegisterTransient]
public sealed class RunnerDashboard : IRunnerDashboard
{
    private const int DashboardLogLimit = 500;
    private readonly IRunnerClient _runnerClient;
    private readonly IKeyInputManager _keyInputManager;

    public RunnerDashboard(IRunnerClient runnerClient, IKeyInputManager keyInputManager)
    {
        _runnerClient = runnerClient;
        _keyInputManager = keyInputManager;
    }

    public Task<RunnerDashboardResult> RunAsync(
        string contextKey,
        IConsole console,
        CancellationToken cancellationToken)
    {
        return ConsoleSupport.SupportsInteractiveConsole(console)
            ? RunInteractiveAsync(contextKey, console, cancellationToken)
            : RunPlainAsync(contextKey, console, cancellationToken);
    }

    private async Task<RunnerDashboardResult> RunInteractiveAsync(
        string contextKey,
        IConsole console,
        CancellationToken cancellationToken)
    {
        var result = RunnerDashboardResult.Completed;
        var selectedIndex = 0;
        var showAllLogs = false;
        var logs = new List<RunnerLogEntry>();
        string? logApplicationId = null;
        long latestLogSequence = 0;
        var consecutiveConnectionFailures = 0;

        _keyInputManager.StartListening();
        try
        {
            await AnsiConsole.Live(new Text("Connecting to the centralized runner..."))
                .StartAsync(async liveContext =>
                {
                    while (true)
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            result = RunnerDashboardResult.Cancelled;
                            break;
                        }

                        var listResponse = await _runnerClient.ListAsync(
                            contextKey,
                            includeInactive: true,
                            cancellationToken);
                        var context = listResponse?.Success == true
                            ? listResponse.Contexts.FirstOrDefault()
                            : null;

                        if (context is null)
                        {
                            consecutiveConnectionFailures++;
                            liveContext.UpdateTarget(BuildUnavailableView(consecutiveConnectionFailures));
                            liveContext.Refresh();

                            if (consecutiveConnectionFailures >= 5)
                            {
                                result = RunnerDashboardResult.Unavailable;
                                break;
                            }

                            await DelayAsync(cancellationToken);
                            continue;
                        }

                        consecutiveConnectionFailures = 0;
                        var applications = context.Applications;
                        if (applications.Length == 0)
                        {
                            liveContext.UpdateTarget(BuildEmptyView(context));
                            liveContext.Refresh();
                            result = RunnerDashboardResult.Completed;
                            break;
                        }

                        selectedIndex = Math.Clamp(selectedIndex, 0, applications.Length - 1);
                        var selectedApplication = applications[selectedIndex];
                        var requestedLogApplicationId = showAllLogs ? null : selectedApplication.Id;
                        if (!string.Equals(logApplicationId, requestedLogApplicationId, StringComparison.Ordinal))
                        {
                            logApplicationId = requestedLogApplicationId;
                            latestLogSequence = 0;
                            logs.Clear();
                        }

                        var logsResponse = await _runnerClient.GetLogsAsync(
                            contextKey,
                            logApplicationId,
                            latestLogSequence,
                            DashboardLogLimit,
                            cancellationToken);
                        if (logsResponse?.Success == true && logsResponse.Logs.Length > 0)
                        {
                            logs.AddRange(logsResponse.Logs);
                            latestLogSequence = logsResponse.Logs[^1].Sequence;
                            if (logs.Count > DashboardLogLimit)
                            {
                                logs.RemoveRange(0, logs.Count - DashboardLogLimit);
                            }
                        }

                        liveContext.UpdateTarget(BuildView(context, selectedIndex, showAllLogs, logs, console));
                        liveContext.Refresh();

                        var key = _keyInputManager.TryGetNextKey();
                        if (key is not null)
                        {
                            if (key.Key is ConsoleKey.Q or ConsoleKey.Escape)
                            {
                                result = RunnerDashboardResult.Detached;
                                break;
                            }

                            if (key.Key is ConsoleKey.UpArrow or ConsoleKey.K)
                            {
                                selectedIndex = selectedIndex == 0 ? applications.Length - 1 : selectedIndex - 1;
                            }
                            else if (key.Key is ConsoleKey.DownArrow or ConsoleKey.J)
                            {
                                selectedIndex = (selectedIndex + 1) % applications.Length;
                            }
                            else if (key.Key == ConsoleKey.A)
                            {
                                showAllLogs = !showAllLogs;
                            }
                            else if (key.Key == ConsoleKey.R)
                            {
                                await _runnerClient.RestartAsync(
                                    contextKey,
                                    selectedApplication.Id,
                                    cancellationToken: cancellationToken);
                            }
                            else if (key.Key == ConsoleKey.S && key.CtrlPressed)
                            {
                                await _runnerClient.StopAsync(
                                    contextKey,
                                    Array.Empty<string>(),
                                    cancellationToken);
                            }
                            else if (key.Key == ConsoleKey.S)
                            {
                                await _runnerClient.StopAsync(
                                    contextKey,
                                    new[] { selectedApplication.Id },
                                    cancellationToken);
                            }
                        }

                        if (!applications.Any(x => x.IsActive))
                        {
                            result = RunnerDashboardResult.Completed;
                            break;
                        }

                        await DelayAsync(cancellationToken);
                    }
                });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            result = RunnerDashboardResult.Cancelled;
        }
        finally
        {
            _keyInputManager.StopListening();
        }

        return result;
    }

    private async Task<RunnerDashboardResult> RunPlainAsync(
        string contextKey,
        IConsole console,
        CancellationToken cancellationToken)
    {
        await console.Output.WriteLineAsync(
            "Interactive dashboard unavailable; printing status changes. Press Ctrl+C to leave this view.");

        var previousStatuses = new Dictionary<string, string>(StringComparer.Ordinal);
        while (!cancellationToken.IsCancellationRequested)
        {
            var response = await _runnerClient.ListAsync(
                contextKey,
                includeInactive: true,
                cancellationToken);
            var context = response?.Success == true ? response.Contexts.FirstOrDefault() : null;
            if (context is null)
            {
                return RunnerDashboardResult.Unavailable;
            }

            foreach (var application in context.Applications)
            {
                var status = $"{application.State}; readiness={application.Readiness}; {application.Status}";
                if (!previousStatuses.TryGetValue(application.Id, out var previous) ||
                    !string.Equals(previous, status, StringComparison.Ordinal))
                {
                    await console.Output.WriteLineAsync($"- {application.DisplayName}: {status}");
                    previousStatuses[application.Id] = status;
                }
            }

            if (!context.Applications.Any(x => x.IsActive))
            {
                return RunnerDashboardResult.Completed;
            }

            try
            {
                await Task.Delay(500, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        return RunnerDashboardResult.Cancelled;
    }

    internal static IRenderable BuildView(
        RunnerContextSnapshot context,
        int selectedIndex,
        bool showAllLogs,
        IReadOnlyList<RunnerLogEntry> logs,
        IConsole console)
    {
        var compact = GetWindowWidth(console) < 100;
        var table = new Table()
            .Border(TableBorder.Rounded)
            .Title($"Managed context: {Markup.Escape(context.DisplayName)}")
            .AddColumn(string.Empty)
            .AddColumn("Application")
            .AddColumn("State");

        if (!compact)
        {
            table.AddColumn("Ready");
            table.AddColumn("PID");
            table.AddColumn("Uptime");
        }

        table.AddColumn("Details");

        for (var index = 0; index < context.Applications.Length; index++)
        {
            var application = context.Applications[index];
            var cells = new List<string>
            {
                index == selectedIndex ? ">" : string.Empty,
                Markup.Escape(application.DisplayName),
                application.State.ToString()
            };

            if (!compact)
            {
                cells.Add(application.Readiness.ToString());
                cells.Add(application.ProcessId?.ToString() ?? "-");
                cells.Add(FormatUptime(application.StartedAt));
            }

            cells.Add(Markup.Escape(application.Url ?? application.Status));
            table.AddRow(cells.ToArray());
        }

        var availableLogLines = Math.Max(5, GetWindowHeight(console) - context.Applications.Length - 12);
        var logRows = logs
            .TakeLast(availableLogLines)
            .Select(log => (IRenderable)new Text(
                $"{log.Timestamp.ToLocalTime():HH:mm:ss} {log.ApplicationName} [{log.Stream}] {log.Message}"))
            .ToArray();
        if (logRows.Length == 0)
        {
            logRows = new IRenderable[] { new Text("No captured output yet.") };
        }

        var logTitle = showAllLogs
            ? "Logs: all applications"
            : $"Logs: {context.Applications[selectedIndex].DisplayName}";
        var panel = new Panel(new Rows(logRows))
        {
            Header = new PanelHeader(Markup.Escape(logTitle)),
            Border = BoxBorder.Rounded,
            Expand = true
        };

        var help = new Text(
            "↑/↓ or J/K Select | A All logs | R Restart | S Stop selected | Ctrl+S Stop context | Q/Esc Detach");
        return new Rows(table, panel, help);
    }

    private static IRenderable BuildUnavailableView(int attempt)
    {
        return new Panel(new Text($"Runner unavailable; reconnecting ({attempt}/5)..."))
        {
            Header = new PanelHeader("Centralized runner"),
            Border = BoxBorder.Rounded
        };
    }

    private static IRenderable BuildEmptyView(RunnerContextSnapshot context)
    {
        return new Panel(new Text("This context contains no managed applications."))
        {
            Header = new PanelHeader(Markup.Escape(context.DisplayName)),
            Border = BoxBorder.Rounded
        };
    }

    private static async Task DelayAsync(CancellationToken cancellationToken)
    {
#if DEBUG
        await Task.Delay(100, cancellationToken);
#else
        await Task.Delay(400, cancellationToken);
#endif
    }

    private static string FormatUptime(DateTimeOffset? startedAt)
    {
        if (startedAt is null)
        {
            return "-";
        }

        var uptime = DateTimeOffset.UtcNow - startedAt.Value;
        return uptime.TotalHours >= 1
            ? $"{(int)uptime.TotalHours}h {uptime.Minutes}m"
            : $"{uptime.Minutes}m {uptime.Seconds}s";
    }

    private static int GetWindowWidth(IConsole console)
    {
        try
        {
            return console.WindowWidth;
        }
        catch
        {
            return 120;
        }
    }

    private static int GetWindowHeight(IConsole console)
    {
        try
        {
            return console.WindowHeight;
        }
        catch
        {
            return 30;
        }
    }
}
