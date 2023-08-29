using CliFx.Infrastructure;

namespace AbpDevTools.Commands;

[Command("env", Description = "Virtual Environment. You can create virtual envionments and run your applications on pre-built virtual environments without changing any files on your computer.")]
public class EnvironmentCommand : ICommand
{
    public async ValueTask ExecuteAsync(IConsole console)
    {
        await console.Output.WriteLineAsync("-----------------------------------------------------------");
        await console.Output.WriteLineAsync("AbpDev Environment provides you to build virtual environemnts managed by \n\t'abpdev env config'");
        await console.Output.WriteLineAsync("\n\nUsage:\nIt's not used standalone. You should use created environments with other commands like:");
        await console.Output.WriteLineAsync("\t'abpdev run --env sqlserver'");
        await console.Output.WriteLineAsync("-----------------------------------------------------------");
    }
}
