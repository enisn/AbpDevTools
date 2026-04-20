using System.Collections.Concurrent;

namespace AbpDevTools.Services;

public class KeyInputManager : IKeyInputManager, IDisposable
{
    public event EventHandler<KeyPressEventArgs>? KeyPressed;
    
    private readonly ConcurrentQueue<KeyPressEventArgs> _keyQueue = new();
    private readonly Func<bool> _canReadConsoleInput;
    private readonly Func<bool> _isKeyAvailable;
    private readonly Func<ConsoleKeyInfo> _readKey;
    private CancellationTokenSource _cancellationTokenSource = new();
    private Task? _listeningTask;
    private bool _disposed;

    public bool IsListening => _listeningTask?.Status == TaskStatus.Running;

    public KeyInputManager()
        : this(
            global::AbpDevTools.ConsoleSupport.CanReadConsoleInput,
            () => Console.KeyAvailable,
            () => Console.ReadKey(true))
    {
    }

    internal KeyInputManager(
        Func<bool> canReadConsoleInput,
        Func<bool> isKeyAvailable,
        Func<ConsoleKeyInfo> readKey)
    {
        _canReadConsoleInput = canReadConsoleInput;
        _isKeyAvailable = isKeyAvailable;
        _readKey = readKey;
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
        
        _listeningTask = Task.Run(ListenForKeyPressesAsync, _cancellationTokenSource.Token);
    }

    public void StopListening()
    {
        if (!IsListening)
            return;

        _cancellationTokenSource.Cancel();
        _listeningTask?.Wait(1_000, _cancellationTokenSource.Token);
    }

    private async Task ListenForKeyPressesAsync()
    {
        try
        {
            while (!_cancellationTokenSource.Token.IsCancellationRequested)
            {
                if (!TryGetKeyAvailable(out var keyAvailable))
                {
                    return;
                }

                if (keyAvailable)
                {
                    if (!TryReadKey(out var keyInfo))
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
                    KeyPressed?.Invoke(this, keyEventArgs);
                }
                
                await Task.Delay(50, _cancellationTokenSource.Token); // Small delay to prevent high CPU usage
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
