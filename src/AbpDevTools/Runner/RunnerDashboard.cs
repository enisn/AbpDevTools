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
    private const int PlainLogInitialLimit = 1_000;
    private const int PlainLogFollowBatchLimit = 500;
    private const int MinimumLogPanelHeight = 3;
    private const int PanelHorizontalChrome = 4;
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
        var openPlainLogViewer = false;
        string? plainLogApplicationId = null;
        var plainLogDisplayName = string.Empty;

        ClearInteractiveSurface(console);
        _keyInputManager.StartListening();
        try
        {
            do
            {
                openPlainLogViewer = false;
                plainLogApplicationId = null;
                plainLogDisplayName = string.Empty;

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
                                else if (key.Key == ConsoleKey.L)
                                {
                                    plainLogApplicationId = requestedLogApplicationId;
                                    plainLogDisplayName = showAllLogs
                                        ? $"{context.DisplayName} (all applications)"
                                        : selectedApplication.DisplayName;
                                    openPlainLogViewer = true;
                                    break;
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

                if (cancellationToken.IsCancellationRequested)
                {
                    result = RunnerDashboardResult.Cancelled;
                    break;
                }

                if (openPlainLogViewer)
                {
                    var returnToDashboard = await RunPlainLogViewerAsync(
                        contextKey,
                        plainLogApplicationId,
                        plainLogDisplayName,
                        console,
                        cancellationToken);
                    if (!returnToDashboard)
                    {
                        result = RunnerDashboardResult.Cancelled;
                        break;
                    }

                    logs.Clear();
                    latestLogSequence = 0;
                    ClearInteractiveSurface(console);
                }
            } while (openPlainLogViewer);
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

    internal async Task<bool> RunPlainLogViewerAsync(
        string contextKey,
        string? applicationId,
        string displayName,
        IConsole console,
        CancellationToken cancellationToken)
    {
        ClearInteractiveSurface(console);
        await console.Output.WriteLineAsync(
            $"Logs: {displayName} (showing at most the latest {PlainLogInitialLimit} entries; press Esc to return)");
        await console.Output.WriteLineAsync();

        var afterSequence = 0L;
        var initialRequest = true;
        while (!cancellationToken.IsCancellationRequested)
        {
            var key = _keyInputManager.TryGetNextKey();
            if (key?.Key == ConsoleKey.Escape)
            {
                return true;
            }

            RunnerResponse? response;
            try
            {
                response = await _runnerClient.GetLogsAsync(
                    contextKey,
                    applicationId,
                    afterSequence,
                    initialRequest ? PlainLogInitialLimit : PlainLogFollowBatchLimit,
                    cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return false;
            }

            if (response?.Success != true)
            {
                await console.Output.WriteLineAsync(
                    response?.Error ?? "The centralized runner became unavailable. Returning to the dashboard.");
                return true;
            }

            if (response.Logs.Length == 0 && initialRequest)
            {
                await console.Output.WriteLineAsync("No captured output yet. Waiting for new output...");
            }

            foreach (var entry in response.Logs)
            {
                await console.Output.WriteLineAsync(
                    FormatPlainLog(entry, includeApplicationName: applicationId is null));
            }

            if (response.Logs.Length > 0)
            {
                afterSequence = response.Logs[^1].Sequence;
            }

            initialRequest = false;
            await DelayAsync(cancellationToken);
        }

        return false;
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
        var windowWidth = Math.Max(1, GetWindowWidth(console));
        var windowHeight = Math.Max(1, GetWindowHeight(console));
        var compact = windowWidth < 100;
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

        var help = new Text(
            "↑/↓ or J/K Select | A All logs | L Logs | R Restart | S Stop selected | Ctrl+S Stop context | Q/Esc Detach");

        if (windowHeight <= MinimumLogPanelHeight)
        {
            return new Rows(table, help);
        }

        var renderOptions = CreateRenderOptions(windowWidth, windowHeight);
        var measuredTableHeight = MeasureRenderableHeight(table, renderOptions, windowWidth);
        var measuredHelpHeight = MeasureRenderableHeight(help, renderOptions, windowWidth);
        var fixedRegionBudget = windowHeight - MinimumLogPanelHeight;
        var tableHeight = Math.Min(measuredTableHeight, Math.Max(1, fixedRegionBudget));
        var helpHeight = Math.Min(
            measuredHelpHeight,
            Math.Max(0, fixedRegionBudget - tableHeight));
        var logPanelHeight = windowHeight - tableHeight - helpHeight;
        var logContentHeight = Math.Max(1, logPanelHeight - 2);
        var logContentWidth = Math.Max(1, windowWidth - PanelHorizontalChrome);
        var logRows = BuildVisibleLogRows(
            logs,
            logContentHeight,
            logContentWidth,
            renderOptions);

        var logTitle = showAllLogs
            ? "Logs: all applications"
            : $"Logs: {context.Applications[selectedIndex].DisplayName}";
        var panel = new Panel(new Rows(logRows))
        {
            Header = new PanelHeader(Markup.Escape(logTitle)),
            Border = BoxBorder.Rounded,
            Expand = true,
            Height = logPanelHeight
        };

        var regions = new List<Layout>
        {
            new(table) { Size = tableHeight },
            new(panel) { Size = logPanelHeight }
        };
        if (helpHeight > 0)
        {
            regions.Add(new Layout(help) { Size = helpHeight });
        }

        return new Layout().SplitRows(regions.ToArray());
    }

    private static IRenderable[] BuildVisibleLogRows(
        IReadOnlyList<RunnerLogEntry> logs,
        int availableHeight,
        int availableWidth,
        RenderOptions renderOptions)
    {
        if (logs.Count == 0)
        {
            return new IRenderable[] { new Text("No captured output yet.") };
        }

        var rows = new List<IRenderable>();
        var remainingHeight = availableHeight;
        for (var index = logs.Count - 1; index >= 0 && remainingHeight > 0; index--)
        {
            var log = logs[index];
            var row = new Text(
                $"{log.Timestamp.ToLocalTime():HH:mm:ss} {log.ApplicationName} [{log.Stream}] {log.Message}");
            var rowHeight = MeasureRenderableHeight(row, renderOptions, availableWidth);
            if (rowHeight > remainingHeight && rows.Count > 0)
            {
                break;
            }

            rows.Add(row);
            remainingHeight -= Math.Min(rowHeight, remainingHeight);
        }

        rows.Reverse();
        return rows.ToArray();
    }

    private static RenderOptions CreateRenderOptions(int width, int height)
    {
        return new RenderOptions(
            AnsiConsole.Console.Profile.Capabilities,
            new Size(width, height));
    }

    private static int MeasureRenderableHeight(
        IRenderable renderable,
        RenderOptions renderOptions,
        int width)
    {
        return Math.Max(
            1,
            Segment.SplitLines(renderable.Render(renderOptions, width), width).Count);
    }

    internal static void ClearInteractiveSurface(IConsole console)
    {
        try
        {
            console.Clear();
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException)
        {
        }
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

    private static string FormatPlainLog(
        RunnerLogEntry entry,
        bool includeApplicationName)
    {
        return includeApplicationName
            ? $"[{entry.ApplicationName}] {entry.Message}"
            : entry.Message;
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
