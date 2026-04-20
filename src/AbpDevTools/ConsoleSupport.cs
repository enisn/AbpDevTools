using CliFx.Infrastructure;
using Spectre.Console;

namespace AbpDevTools;

internal static class ConsoleSupport
{
    public static bool ConfirmOrDefault(IConsole? console, string prompt, bool defaultValue = true, string? fallbackMessage = null)
    {
        return ConfirmOrDefault(
            console,
            prompt,
            defaultValue,
            fallbackMessage,
            (text, value) => AnsiConsole.Prompt(new ConfirmationPrompt(text) { DefaultValue = value }));
    }

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

    internal static bool ConfirmOrDefault(
        IConsole? console,
        string prompt,
        bool defaultValue,
        string? fallbackMessage,
        Func<string, bool, bool> confirm)
    {
        if (!SupportsInteractiveConsole(console))
        {
            if (!string.IsNullOrWhiteSpace(fallbackMessage) && console != null)
            {
                try
                {
                    console.Output.WriteLine(fallbackMessage);
                }
                catch (InvalidOperationException)
                {
                }
                catch (IOException)
                {
                }
            }

            return defaultValue;
        }

        return confirm(prompt, defaultValue);
    }
}
