namespace AbpDevTools.Services;

public interface IKeyInputManager
{
    event EventHandler<KeyPressEventArgs> KeyPressed;
    void StartListening();
    void StopListening();
    bool IsListening { get; }
    KeyPressEventArgs? TryGetNextKey();
}

public class KeyPressEventArgs : EventArgs
{
    public ConsoleKey Key { get; set; }
    public bool CtrlPressed { get; set; }
    public bool ShiftPressed { get; set; }
    public bool AltPressed { get; set; }
}
