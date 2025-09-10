using System.Collections.Concurrent;

namespace AbpDevTools.Services;

public class KeyInputManager : IKeyInputManager, IDisposable
{
    public event EventHandler<KeyPressEventArgs>? KeyPressed;
    
    private readonly ConcurrentQueue<KeyPressEventArgs> _keyQueue = new();
    private CancellationTokenSource _cancellationTokenSource = new();
    private Task? _listeningTask;
    private bool _disposed;

    public bool IsListening => _listeningTask?.Status == TaskStatus.Running;

    public void StartListening()
    {
        if (IsListening)
            return;

        _cancellationTokenSource.CancelAfter(TimeSpan.FromMilliseconds(100));
        _cancellationTokenSource.Token.WaitHandle.WaitOne();
        
        _cancellationTokenSource.Dispose();
        _cancellationTokenSource = new CancellationTokenSource();
        
        _listeningTask = Task.Run(ListenForKeyPresses, _cancellationTokenSource.Token);
    }

    public void StopListening()
    {
        if (!IsListening)
            return;

        _cancellationTokenSource.Cancel();
        _listeningTask?.Wait(TimeSpan.FromSeconds(1));
    }

    private void ListenForKeyPresses()
    {
        try
        {
            while (!_cancellationTokenSource.Token.IsCancellationRequested)
            {
                if (Console.KeyAvailable)
                {
                    var keyInfo = Console.ReadKey(true);
                    
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
                
                Thread.Sleep(50); // Small delay to prevent high CPU usage
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when cancellation is requested
        }
        catch (Exception)
        {
            // Handle any other exceptions gracefully
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
