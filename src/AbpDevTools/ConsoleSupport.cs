using CliFx.Infrastructure;

namespace AbpDevTools;

internal static class ConsoleSupport
{
    public static bool CanReadConsoleInput()
    {
        try
        {
            return !Console.IsInputRedirected;
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

    public static bool SupportsInteractiveConsole(IConsole? console)
    {
        if (console is null)
        {
            return false;
        }

        try
        {
            return !console.IsInputRedirected && !console.IsOutputRedirected && !console.IsErrorRedirected;
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

    public static bool TryGetWindowWidth(IConsole? console, out int windowWidth)
    {
        windowWidth = 0;

        if (!SupportsInteractiveConsole(console))
        {
            return false;
        }

        try
        {
            windowWidth = console!.WindowWidth;
            return windowWidth > 0;
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
}
