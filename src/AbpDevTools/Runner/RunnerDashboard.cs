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
    private const int DashboardLogLimit = 5_000;
    private const int MinimumLogPanelHeight = 3;
    private const int PanelHorizontalChrome = 4;
    private const string LogViewerHelp =
        "↑/↓ or J/K Scroll | PgUp/PgDn Page | Home/End Oldest/Newest | F Follow | L/Q/Esc Back";
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
        var showLogViewer = false;
        var logScrollOffset = 0;
        var logs = new List<RunnerLogEntry>();
        string? logApplicationId = null;
        long latestLogSequence = 0;
        var consecutiveConnectionFailures = 0;

        ClearInteractiveSurface(console);
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
                            logScrollOffset = 0;
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
                            if (showLogViewer && logScrollOffset > 0)
                            {
                                logScrollOffset += logsResponse.Logs.Length;
                            }

                            logs.AddRange(logsResponse.Logs);
                            latestLogSequence = logsResponse.Logs[^1].Sequence;
                            if (logs.Count > DashboardLogLimit)
                            {
                                logs.RemoveRange(0, logs.Count - DashboardLogLimit);
                            }
                        }

                        logScrollOffset = ClampLogScrollOffset(logScrollOffset, logs, console);
                        liveContext.UpdateTarget(
                            showLogViewer
                                ? BuildLogView(
                                    context,
                                    selectedIndex,
                                    showAllLogs,
                                    logs,
                                    logScrollOffset,
                                    console)
                                : BuildView(context, selectedIndex, showAllLogs, logs, console));
                        liveContext.Refresh();

                        var key = _keyInputManager.TryGetNextKey();
                        if (key is not null)
                        {
                            if (showLogViewer)
                            {
                                if (key.Key is ConsoleKey.L or ConsoleKey.Q or ConsoleKey.Escape)
                                {
                                    showLogViewer = false;
                                    logScrollOffset = 0;
                                }
                                else
                                {
                                    logScrollOffset = AdjustLogScrollOffset(
                                        key.Key,
                                        logScrollOffset,
                                        logs,
                                        console);
                                }
                            }
                            else if (key.Key is ConsoleKey.Q or ConsoleKey.Escape)
                            {
                                result = RunnerDashboardResult.Detached;
                                break;
                            }
                            else if (key.Key is ConsoleKey.UpArrow or ConsoleKey.K)
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
                                showLogViewer = true;
                                logScrollOffset = 0;
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
            "↑/↓ or J/K Select | A All logs | L Full logs | R Restart | S Stop selected | Ctrl+S Stop context | Q/Esc Detach");

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
        var logRows = BuildVisibleLogPage(
            logs,
            logContentHeight,
            logContentWidth,
            renderOptions,
            scrollOffset: 0).Rows;

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

    internal static IRenderable BuildLogView(
        RunnerContextSnapshot context,
        int selectedIndex,
        bool showAllLogs,
        IReadOnlyList<RunnerLogEntry> logs,
        int scrollOffset,
        IConsole console)
    {
        var dimensions = CalculateLogViewDimensions(console);
        var maximumOffset = GetMaximumLogScrollOffset(logs, dimensions);
        var boundedOffset = Math.Clamp(scrollOffset, 0, maximumOffset);
        var page = BuildVisibleLogPage(
            logs,
            dimensions.ContentHeight,
            dimensions.ContentWidth,
            dimensions.RenderOptions,
            boundedOffset);
        var scope = showAllLogs
            ? "all applications"
            : context.Applications[selectedIndex].DisplayName;
        var mode = boundedOffset == 0
            ? "LIVE"
            : $"PAUSED • {boundedOffset} newer entr{(boundedOffset == 1 ? "y" : "ies")}";
        var panel = new Panel(new Rows(page.Rows))
        {
            Header = new PanelHeader(Markup.Escape($"Logs: {scope} • {mode}")),
            Border = BoxBorder.Rounded,
            Expand = true,
            Height = dimensions.PanelHeight
        };

        var regions = new List<Layout>
        {
            new(panel) { Size = dimensions.PanelHeight }
        };
        if (dimensions.HelpHeight > 0)
        {
            regions.Add(new Layout(new Text(LogViewerHelp)) { Size = dimensions.HelpHeight });
        }

        return new Layout().SplitRows(regions.ToArray());
    }

    internal static int AdjustLogScrollOffset(
        ConsoleKey key,
        int currentOffset,
        IReadOnlyList<RunnerLogEntry> logs,
        IConsole console)
    {
        var dimensions = CalculateLogViewDimensions(console);
        var maximumOffset = GetMaximumLogScrollOffset(logs, dimensions);
        var boundedOffset = Math.Clamp(currentOffset, 0, maximumOffset);
        var pageSize = Math.Max(
            1,
            BuildVisibleLogPage(
                logs,
                dimensions.ContentHeight,
                dimensions.ContentWidth,
                dimensions.RenderOptions,
                boundedOffset).EntryCount);

        return key switch
        {
            ConsoleKey.UpArrow or ConsoleKey.K => Math.Min(maximumOffset, boundedOffset + 1),
            ConsoleKey.DownArrow or ConsoleKey.J => Math.Max(0, boundedOffset - 1),
            ConsoleKey.PageUp => Math.Min(maximumOffset, boundedOffset + pageSize),
            ConsoleKey.PageDown => Math.Max(0, boundedOffset - pageSize),
            ConsoleKey.Home => maximumOffset,
            ConsoleKey.End or ConsoleKey.F => 0,
            _ => boundedOffset
        };
    }

    private static int ClampLogScrollOffset(
        int scrollOffset,
        IReadOnlyList<RunnerLogEntry> logs,
        IConsole console)
    {
        var dimensions = CalculateLogViewDimensions(console);
        return Math.Clamp(scrollOffset, 0, GetMaximumLogScrollOffset(logs, dimensions));
    }

    private static VisibleLogPage BuildVisibleLogPage(
        IReadOnlyList<RunnerLogEntry> logs,
        int availableHeight,
        int availableWidth,
        RenderOptions renderOptions,
        int scrollOffset)
    {
        if (logs.Count == 0)
        {
            return new VisibleLogPage(
                new IRenderable[] { new Text("No captured output yet.") },
                EntryCount: 0);
        }

        var rows = new List<IRenderable>();
        var remainingHeight = availableHeight;
        var lastIndex = Math.Clamp(logs.Count - 1 - scrollOffset, 0, logs.Count - 1);
        for (var index = lastIndex; index >= 0 && remainingHeight > 0; index--)
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
        return new VisibleLogPage(rows.ToArray(), rows.Count);
    }

    private static int GetMaximumLogScrollOffset(
        IReadOnlyList<RunnerLogEntry> logs,
        LogViewDimensions dimensions)
    {
        if (logs.Count == 0)
        {
            return 0;
        }

        var usedHeight = 0;
        var lastVisibleIndex = 0;
        for (var index = 0; index < logs.Count; index++)
        {
            var log = logs[index];
            var row = new Text(
                $"{log.Timestamp.ToLocalTime():HH:mm:ss} {log.ApplicationName} [{log.Stream}] {log.Message}");
            var rowHeight = MeasureRenderableHeight(
                row,
                dimensions.RenderOptions,
                dimensions.ContentWidth);
            if (usedHeight + rowHeight > dimensions.ContentHeight && index > 0)
            {
                break;
            }

            usedHeight += Math.Min(rowHeight, dimensions.ContentHeight);
            lastVisibleIndex = index;
            if (usedHeight >= dimensions.ContentHeight)
            {
                break;
            }
        }

        return Math.Max(0, logs.Count - 1 - lastVisibleIndex);
    }

    private static LogViewDimensions CalculateLogViewDimensions(IConsole console)
    {
        var windowWidth = Math.Max(1, GetWindowWidth(console));
        var windowHeight = Math.Max(1, GetWindowHeight(console));
        var renderOptions = CreateRenderOptions(windowWidth, windowHeight);
        var help = new Text(LogViewerHelp);
        var measuredHelpHeight = MeasureRenderableHeight(help, renderOptions, windowWidth);
        var helpHeight = Math.Min(
            measuredHelpHeight,
            Math.Max(0, windowHeight - MinimumLogPanelHeight));
        var panelHeight = Math.Max(1, windowHeight - helpHeight);
        return new LogViewDimensions(
            panelHeight,
            Math.Max(1, panelHeight - 2),
            Math.Max(1, windowWidth - PanelHorizontalChrome),
            helpHeight,
            renderOptions);
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

    private readonly record struct VisibleLogPage(IRenderable[] Rows, int EntryCount);

    private readonly record struct LogViewDimensions(
        int PanelHeight,
        int ContentHeight,
        int ContentWidth,
        int HelpHeight,
        RenderOptions RenderOptions);

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
