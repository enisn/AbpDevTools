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

    public static bool CanReadConsoleInput() => CanReadConsoleInput(null);

    public static bool CanReadConsoleInput(Func<string, string?>? getEnvironmentVariable)
    {
        if (IsNonInteractiveEnvironment(getEnvironmentVariable))
        {
            return false;
        }

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

    public static bool SupportsInteractiveConsole(IConsole? console) => SupportsInteractiveConsole(console, null);

    public static bool SupportsInteractiveConsole(IConsole? console, Func<string, string?>? getEnvironmentVariable)
    {
        if (console is null)
        {
            return false;
        }

        if (IsNonInteractiveEnvironment(getEnvironmentVariable))
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

    public static bool IsNonInteractiveEnvironment(Func<string, string?>? getEnvironmentVariable = null)
    {
        getEnvironmentVariable ??= Environment.GetEnvironmentVariable;

        var abpDevNonInteractive = getEnvironmentVariable("ABPDEV_NON_INTERACTIVE");
        if (IsTrueOrOne(abpDevNonInteractive))
        {
            return true;
        }

        var abpDevInteractive = getEnvironmentVariable("ABPDEV_INTERACTIVE");
        if (IsFalseOrZero(abpDevInteractive))
        {
            return true;
        }

        var nonInteractive = getEnvironmentVariable("NONINTERACTIVE");
        if (IsTrueOrOne(nonInteractive))
        {
            return true;
        }

        var nonInteractiveUnderscore = getEnvironmentVariable("NON_INTERACTIVE");
        if (IsTrueOrOne(nonInteractiveUnderscore))
        {
            return true;
        }

        var ci = getEnvironmentVariable("CI");
        if (IsTrueOrOne(ci))
        {
            return true;
        }

        var debianFrontend = getEnvironmentVariable("DEBIAN_FRONTEND");
        if (string.Equals(debianFrontend?.Trim(), "noninteractive", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var term = getEnvironmentVariable("TERM");
        if (string.Equals(term?.Trim(), "dumb", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private static bool IsTrueOrOne(string? value) =>
        string.Equals(value?.Trim(), "true", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(value?.Trim(), "1", StringComparison.OrdinalIgnoreCase);

    private static bool IsFalseOrZero(string? value) =>
        string.Equals(value?.Trim(), "false", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(value?.Trim(), "0", StringComparison.OrdinalIgnoreCase);

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
