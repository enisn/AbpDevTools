using CliFx;

namespace AbpDevTools;

public class Program
{
    public static async Task<int> Main() =>
        await new CliApplicationBuilder()
            .SetExecutableName("abpdev")
            .AddCommandsFromThisAssembly()
            .Build()
            .RunAsync();
}
