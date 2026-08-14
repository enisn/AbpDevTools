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

    private static readonly (string Variable, Func<string, bool> IsMatch)[] NonInteractiveRules =
    [
        ("ABPDEV_NON_INTERACTIVE", IsTrueOrOne),
        ("ABPDEV_INTERACTIVE", IsFalseOrZero),
        ("NONINTERACTIVE", IsTrueOrOne),
        ("NON_INTERACTIVE", IsTrueOrOne),
        ("CI", IsTrueOrOne),
        ("DEBIAN_FRONTEND", v => v.Equals("noninteractive", StringComparison.OrdinalIgnoreCase)),
        ("TERM", v => v.Equals("dumb", StringComparison.OrdinalIgnoreCase)),
    ];

    public static bool IsNonInteractiveEnvironment(Func<string, string?>? getEnvironmentVariable = null)
    {
        getEnvironmentVariable ??= Environment.GetEnvironmentVariable;

        var abpDevInteractive = getEnvironmentVariable("ABPDEV_INTERACTIVE")?.Trim();
        if (IsTrueOrOne(abpDevInteractive))
        {
            return false;
        }

        var abpDevNonInteractive = getEnvironmentVariable("ABPDEV_NON_INTERACTIVE")?.Trim();
        if (IsFalseOrZero(abpDevNonInteractive))
        {
            return false;
        }

        return NonInteractiveRules.Any(rule =>
        {
            var value = getEnvironmentVariable(rule.Variable)?.Trim();
            return !string.IsNullOrEmpty(value) && rule.IsMatch(value);
        });
    }

    private static bool IsTrueOrOne(string? value) =>
        !string.IsNullOrEmpty(value) && (value.Equals("true", StringComparison.OrdinalIgnoreCase) || value == "1");

    private static bool IsFalseOrZero(string? value) =>
        !string.IsNullOrEmpty(value) && (value.Equals("false", StringComparison.OrdinalIgnoreCase) || value == "0");

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
