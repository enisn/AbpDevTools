using AbpDevTools.Processes;
using NSubstitute;

namespace AbpDevTools.Tests.Helpers;

/// <summary>
/// Builder helper for creating mocked process-related objects.
/// Provides fluent interface for setting up process operations.
/// </summary>
public class ProcessMockBuilder
{
    private readonly List<ProcessInfo> _processes = new();
    private readonly Dictionary<int, ProcessInfo> _processesByPid = new();
    private readonly Dictionary<int, List<ProcessInfo>> _processesByPort = new();
    private int _nextPid = 1000;

    /// <summary>
    /// Creates a new ProcessMockBuilder instance.
    /// </summary>
    public static ProcessMockBuilder Create() => new();

    /// <summary>
    /// Adds a process to the mocked system.
    /// </summary>
    public ProcessMockBuilder WithProcess(int pid, string processName, string path)
    {
        var processInfo = new ProcessInfo
        {
            Pid = pid,
            ProcessName = processName,
            Path = path
        };
        _processes.Add(processInfo);
        _processesByPid[pid] = processInfo;
        return this;
    }

    /// <summary>
    /// Adds a process to the mocked system with auto-generated PID.
    /// </summary>
    public ProcessMockBuilder WithProcess(string processName, string path)
    {
        return WithProcess(_nextPid++, processName, path);
    }

    /// <summary>
    /// Adds processes listening on a specific port.
    /// </summary>
    public ProcessMockBuilder WithProcessesOnPort(int port, params ProcessInfo[] processes)
    {
        _processesByPort[port] = processes.ToList();
        foreach (var process in processes)
        {
            if (!_processesByPid.ContainsKey(process.Pid))
            {
                _processes.Add(process);
                _processesByPid[process.Pid] = process;
            }
        }
        return this;
    }

    /// <summary>
    /// Adds a process listening on a specific port with auto-generated PID.
    /// </summary>
    public ProcessMockBuilder WithProcessOnPort(int port, string processName, string path)
    {
        var processInfo = new ProcessInfo
        {
            Pid = _nextPid++,
            ProcessName = processName,
            Path = path
        };
        return WithProcessesOnPort(port, processInfo);
    }

    /// <summary>
    /// Builds the process state for test assertions.
    /// </summary>
    public ProcessState Build()
    {
        return new ProcessState(
            new List<ProcessInfo>(_processes),
            new Dictionary<int, ProcessInfo>(_processesByPid),
            new Dictionary<int, List<ProcessInfo>>(_processesByPort)
        );
    }

    /// <summary>
    /// Creates a mocked IProcessFinder with the configured processes.
    /// </summary>
    public IProcessFinder BuildProcessFinder()
    {
        var mock = Substitute.For<IProcessFinder>();
        var state = Build();

        mock.FindProcessesByPortAsync(Arg.Any<int>())
            .Returns(callInfo =>
            {
                var port = callInfo.Arg<int>();
                return state.GetProcessesByPort(port);
            });

        return mock;
    }
}

/// <summary>
/// Represents the state of mocked processes for test assertions.
/// </summary>
public class ProcessState
{
    public List<ProcessInfo> Processes { get; }
    public Dictionary<int, ProcessInfo> ProcessesByPid { get; }
    public Dictionary<int, List<ProcessInfo>> ProcessesByPort { get; }

    public ProcessState(
        List<ProcessInfo> processes,
        Dictionary<int, ProcessInfo> processesByPid,
        Dictionary<int, List<ProcessInfo>> processesByPort)
    {
        Processes = processes;
        ProcessesByPid = processesByPid;
        ProcessesByPort = processesByPort;
    }

    /// <summary>
    /// Gets all processes listening on a specific port.
    /// </summary>
    public List<ProcessInfo> GetProcessesByPort(int port) =>
        ProcessesByPort.TryGetValue(port, out var processes) ? processes : new List<ProcessInfo>();

    /// <summary>
    /// Gets a process by its PID.
    /// </summary>
    public ProcessInfo? GetProcessByPid(int pid) =>
        ProcessesByPid.TryGetValue(pid, out var process) ? process : null;

    /// <summary>
    /// Checks if any process is listening on the specified port.
    /// </summary>
    public bool IsPortInUse(int port) =>
        ProcessesByPort.ContainsKey(port) && ProcessesByPort[port].Count > 0;

    /// <summary>
    /// Gets the number of processes on a specific port.
    /// </summary>
    public int GetProcessCountOnPort(int port) =>
        ProcessesByPort.TryGetValue(port, out var processes) ? processes.Count : 0;
}

/// <summary>
/// Helper methods for creating sample ProcessInfo objects for tests.
/// </summary>
public static class TestProcessInfo
{
    /// <summary>
    /// Creates a sample dotnet process.
    /// </summary>
    public static ProcessInfo Dotnet(int pid = TestConstants.ProcessData.SamplePid, string path = "C:\\Projects\\Test\\bin\\Debug\\net9.0\\Test.exe")
    {
        return new ProcessInfo
        {
            Pid = pid,
            ProcessName = TestConstants.ProcessData.SampleProcessName,
            Path = path
        };
    }

    /// <summary>
    /// Creates a sample IIS Express process.
    /// </summary>
    public static ProcessInfo IisExpress(int pid = 54321, string path = "C:\\Program Files\\IIS Express\\iisexpress.exe")
    {
        return new ProcessInfo
        {
            Pid = pid,
            ProcessName = "iisexpress",
            Path = path
        };
    }

    /// <summary>
    /// Creates a sample Node.js process.
    /// </summary>
    public static ProcessInfo Node(int pid = 22222, string path = "C:\\Program Files\\nodejs\\node.exe")
    {
        return new ProcessInfo
        {
            Pid = pid,
            ProcessName = "node",
            Path = path
        };
    }

    /// <summary>
    /// Creates a sample database process (PostgreSQL).
    /// </summary>
    public static ProcessInfo Postgres(int pid = 33333, string path = "C:\\Program Files\\PostgreSQL\\15\\bin\\postgres.exe")
    {
        return new ProcessInfo
        {
            Pid = pid,
            ProcessName = "postgres",
            Path = path
        };
    }

    /// <summary>
    /// Creates a sample Redis process.
    /// </summary>
    public static ProcessInfo Redis(int pid = 44444, string path = "C:\\Program Files\\Redis\\redis-server.exe")
    {
        return new ProcessInfo
        {
            Pid = pid,
            ProcessName = "redis-server",
            Path = path
        };
    }
}
