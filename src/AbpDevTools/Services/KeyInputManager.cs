using System.Collections.Concurrent;

namespace AbpDevTools.Services;

public class KeyInputManager : IKeyInputManager, IDisposable
{
    public event EventHandler<KeyPressEventArgs>? KeyPressed;
    
    private readonly ConcurrentQueue<KeyPressEventArgs> _keyQueue = new();
    private readonly Func<bool> _canReadConsoleInput;
    private readonly Func<bool> _isKeyAvailable;
    private readonly Func<ConsoleKeyInfo> _readKey;
    private readonly bool _pollBeforeRead;
    private CancellationTokenSource _cancellationTokenSource = new();
    private Task? _listeningTask;
    private volatile bool _isNotifyingKeyPress;
    private bool _disposed;

    public bool IsListening => _listeningTask is { IsCompleted: false } && !_cancellationTokenSource.IsCancellationRequested;

    public KeyInputManager()
        : this(
            global::AbpDevTools.ConsoleSupport.CanReadConsoleInput,
            () => Console.KeyAvailable,
            () => Console.ReadKey(true),
            pollBeforeRead: false)
    {
    }

    internal KeyInputManager(
        Func<bool> canReadConsoleInput,
        Func<bool> isKeyAvailable,
        Func<ConsoleKeyInfo> readKey,
        bool pollBeforeRead = true)
    {
        _canReadConsoleInput = canReadConsoleInput;
        _isKeyAvailable = isKeyAvailable;
        _readKey = readKey;
        _pollBeforeRead = pollBeforeRead;
    }

    public void StartListening()
    {
        if (IsListening)
            return;

        if (!_canReadConsoleInput())
            return;

        // Ensure any previous listening task is fully stopped before starting a new one
        if (_listeningTask != null && !_listeningTask.IsCompleted)
        {
            _cancellationTokenSource.Cancel();
            _listeningTask.Wait(TimeSpan.FromSeconds(1));
        }
        _cancellationTokenSource.Dispose();
        _cancellationTokenSource = new CancellationTokenSource();
        
        var cancellationToken = _cancellationTokenSource.Token;
        _listeningTask = Task.Run(() => ListenForKeyPressesAsync(cancellationToken), cancellationToken);
    }

    public void StopListening()
    {
        if (!IsListening)
            return;

        _cancellationTokenSource.Cancel();
        // Key handlers may stop the listener before it re-enters a blocking ReadKey call.
        if (_isNotifyingKeyPress)
        {
            return;
        }

        try
        {
            _listeningTask?.Wait(1_000);
        }
        catch (AggregateException ex) when (ex.InnerExceptions.All(inner => inner is TaskCanceledException or OperationCanceledException))
        {
            // Expected when cancellation wins the wait race.
        }
    }

    private async Task ListenForKeyPressesAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (_pollBeforeRead)
                {
                    if (!TryGetKeyAvailable(out var keyAvailable))
                    {
                        return;
                    }

                    if (!keyAvailable)
                    {
                        await Task.Delay(50, cancellationToken);
                        continue;
                    }
                }

                if (!TryReadKey(out var keyInfo) || cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                var keyEventArgs = new KeyPressEventArgs
                {
                    Key = keyInfo.Key,
                    CtrlPressed = (keyInfo.Modifiers & ConsoleModifiers.Control) != 0,
                    ShiftPressed = (keyInfo.Modifiers & ConsoleModifiers.Shift) != 0,
                    AltPressed = (keyInfo.Modifiers & ConsoleModifiers.Alt) != 0
                };

                _keyQueue.Enqueue(keyEventArgs);

                try
                {
                    _isNotifyingKeyPress = true;
                    KeyPressed?.Invoke(this, keyEventArgs);
                }
                finally
                {
                    _isNotifyingKeyPress = false;
                }

                await Task.Delay(50, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when cancellation is requested
        }
        catch (Exception ex)
        {
            // Log unexpected exceptions to aid debugging
            Console.Error.WriteLine($"[KeyInputManager] Unexpected exception: {ex}");
        }
    }

    private bool TryGetKeyAvailable(out bool keyAvailable)
    {
        keyAvailable = false;

        try
        {
            keyAvailable = _isKeyAvailable();
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
    }

    private bool TryReadKey(out ConsoleKeyInfo keyInfo)
    {
        keyInfo = default;

        try
        {
            keyInfo = _readKey();
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
    }

    public KeyPressEventArgs? TryGetNextKey()
    {
        return _keyQueue.TryDequeue(out var keyEvent) ? keyEvent : null;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        StopListening();
        _cancellationTokenSource?.Dispose();
        _disposed = true;
    }
}
