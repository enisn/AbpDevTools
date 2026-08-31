using System.Text;

namespace AbpDevTools.Runner;

internal sealed class RunnerLogWriter : IDisposable
{
    private const long MaxFileSize = 5 * 1024 * 1024;
    private readonly object _sync = new();
    private readonly string _filePath;
    private StreamWriter? _writer;
    private bool _disposed;

    public RunnerLogWriter(string logsDirectory, string contextKey, string applicationId)
    {
        var directory = Path.Combine(logsDirectory, contextKey);
        Directory.CreateDirectory(directory);
        _filePath = Path.Combine(directory, applicationId + ".log");
    }

    public string FilePath => _filePath;

    public void Write(RunnerLogEntry entry)
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            EnsureWriter();
            if (_writer!.BaseStream.Length >= MaxFileSize)
            {
                Rotate();
                EnsureWriter();
            }

            _writer!.WriteLine($"{entry.Timestamp:O} [{entry.Stream}] {entry.Message}");
            _writer.Flush();
        }
    }

    private void EnsureWriter()
    {
        if (_writer is not null)
        {
            return;
        }

        var stream = new FileStream(
            _filePath,
            FileMode.Append,
            FileAccess.Write,
            FileShare.ReadWrite | FileShare.Delete);
        _writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private void Rotate()
    {
        _writer?.Dispose();
        _writer = null;

        var backupPath = _filePath + ".1";
        File.Move(_filePath, backupPath, overwrite: true);
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _writer?.Dispose();
            _writer = null;
        }
    }
}
