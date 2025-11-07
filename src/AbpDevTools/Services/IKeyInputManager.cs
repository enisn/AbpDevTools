using System.Text;

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

public class KeyCommandMapping
{
    public KeyPressEventArgs KeyPressEvent { get; }
    public Action Action { get; }
    public string Name { get; }
    public string? Description { get; }

    public KeyCommandMapping(KeyPressEventArgs keyPressEvent, Action action, string name, string? description = null)
    {
        KeyPressEvent = keyPressEvent;
        Action = action;
        Name = name;
        Description = description;
    }

    public string GetKeyDisplay()
    {
        StringBuilder modifiers = new StringBuilder();
        if (KeyPressEvent.CtrlPressed)
        {
            modifiers.Append("Ctrl+");
        }
        if (KeyPressEvent.ShiftPressed)
        {
            modifiers.Append("Shift+");
        }
        if (KeyPressEvent.AltPressed)
        {
            modifiers.Append("Alt+");
        }
        return modifiers.ToString() + KeyPressEvent.Key;
    }
}