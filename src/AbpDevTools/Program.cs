using AbpDevTools.Commands;
using AbpDevTools.Commands.Migrations;
using AbpDevTools.Notifications;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace AbpDevTools;

public class Program
{
    public static async Task<int> Main(string[] args) =>
        await new CliApplicationBuilder()
            .SetExecutableName("abpdev")
            .SetDescription("A set of tools to make development with ABP easier.")
            .SetTitle("Abp Dev Tools")
            .BuildServices(args)
            .Build()
            .RunAsync();
}

public static class Startup
{
    internal static string SentryDsn { get; set; } = "{{SENTRY_DSN}}";
    public static CliApplicationBuilder BuildServices(this CliApplicationBuilder builder, string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;

        var services = new ServiceCollection();

        var commands = new Type[] // Keep this instead reflection for performance
        {
            typeof(BuildCommand),
            typeof(ConfigurationClearCommand),
            typeof(ReplacementConfigClearCommand),
            typeof(EnvironmentAppConfigClearCommand),
            typeof(RunConfigClearCommand),
            typeof(CleanConfigClearCommand),
            typeof(ToolsConfigClearCommand),
            typeof(ReplaceConfigurationCommand),
            typeof(EnvironmentAppConfigurationCommand),
            typeof(RunConfigurationCommand),
            typeof(CleanConfigurationCommand),
            typeof(ToolsConfigurationCommand),
            typeof(ToolsCommand),
            typeof(ConfigCommand),
            typeof(DisableNotificationsCommand),
            typeof(EnableNotificationsCommand),
            typeof(EnvironmentAppCommand),
            typeof(EnvironmentAppStartCommand),
            typeof(EnvironmentAppStopCommand),
            typeof(LogsClearCommand),
            typeof(LogsCommand),
            typeof(MigrateCommand),
            typeof(ReplaceCommand),
            typeof(RunCommand),
            typeof(EnvironmentCommand),
            typeof(EnvironmentConfigurationCommand),
            typeof(AbpBundleCommand),
            typeof(AbpBundleListCommand),
            typeof(TestCommand),
            typeof(UpdateCheckCommand),
            typeof(CleanCommand),
            typeof(DatabaseDropCommand),
            typeof(SwitchToEnvironmentCommand),
            typeof(FindFileCommand),
            typeof(MigrationsCommand),
            typeof(AddMigrationCommand),
            typeof(ClearMigrationsCommand),
            typeof(PrepareCommand),
        };

        foreach (var commandType in commands)
        {
            if (!commandType.IsAbstract && typeof(ICommand).IsAssignableFrom(commandType))
            {
                builder.AddCommand(commandType);
                services.AddSingleton(commandType);
            }
        }

        services.AutoRegisterFromAbpDevTools();

        var yamlSerializer = new SerializerBuilder()
            .WithNamingConvention(HyphenatedNamingConvention.Instance)
            .Build();

        services.AddSingleton(yamlSerializer);

        services.AddSingleton(
            new DeserializerBuilder()
                .WithNamingConvention(HyphenatedNamingConvention.Instance)
                .Build());

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            services.AddTransient<INotificationManager, WindowsNotificationManager>();
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            services.AddTransient<INotificationManager, MacCatalystNotificationManager>();
        }
        else
        {
            services.AddTransient<INotificationManager, DefaultNotificationManager>();
        }

        if (!string.IsNullOrEmpty(SentryDsn) && Uri.TryCreate(SentryDsn, UriKind.Absolute, out var sentryUri))
        {
            SentrySdk.Init(options =>
            {
                options.Dsn = SentryDsn;
                options.DefaultTags["os"] = RuntimeInformation.OSDescription;
                options.DefaultTags["runtime"] = RuntimeInformation.FrameworkDescription;
                options.DefaultTags["version"] = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown";
                options.DefaultTags["command"] = args.FirstOrDefault() ?? string.Empty;

                options.SetBeforeSend((@event, hint) =>
                {
                    @event.Contexts["args"] = args;
                    return @event;
                });
            });
        }

        throw new Exception("Hello world!");

        var serviceProvider = services.BuildServiceProvider();

        return builder.UseTypeActivator(serviceProvider);
    }
}
