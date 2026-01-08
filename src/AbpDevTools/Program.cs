using AbpDevTools.Commands;
using AbpDevTools.Commands.Migrations;
using AbpDevTools.Commands.References;
using AbpDevTools.Notifications;
using AbpDevTools.RecycleBin;
using AbpDevTools.Services;
using Microsoft.Extensions.DependencyInjection;
using System.Runtime.InteropServices;
using System.Text;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace AbpDevTools;

public class Program
{
    public static async Task<int> Main() =>
        await new CliApplicationBuilder()
            .SetExecutableName("abpdev")
            .SetDescription("A set of tools to make development with ABP easier.")
            .SetTitle("Abp Dev Tools")
            .BuildServices()
            .Build()
            .RunAsync();
}

public static class Startup
{
    public static CliApplicationBuilder BuildServices(this CliApplicationBuilder builder)
    {
        Console.OutputEncoding = Encoding.UTF8;

        var services = new ServiceCollection();

        var commands = new Type[] // Keep this instead reflection for performance
        {
            typeof(AbpStudioSwitchCommand),
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
            typeof(LocalSourcesConfigurationCommand),
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
            typeof(FindPortCommand),
            typeof(MigrationsCommand),
            typeof(AddMigrationCommand),
            typeof(ClearMigrationsCommand),
            typeof(RecreateMigrationsCommand),
            typeof(PrepareCommand),
            typeof(SwitchReferencesToLocalCommand),
            typeof(SwitchReferencesToPackageCommand),
            typeof(LocalSourcesCommand),
            typeof(ReferencesCommand),
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
        
        services.AddSingleton<IKeyInputManager, KeyInputManager>();

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
            services.AddTransient<IRecycleBinManager, WindowsRecycleBinManager>();
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            services.AddTransient<INotificationManager, MacCatalystNotificationManager>();
            services.AddTransient<IRecycleBinManager, MacRecycleBinManager>();
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            services.AddTransient<INotificationManager, DefaultNotificationManager>();
            services.AddTransient<IRecycleBinManager, LinuxRecycleBinManager>();
        }
        else
        {
            services.AddTransient<INotificationManager, DefaultNotificationManager>();
            services.AddTransient<IRecycleBinManager, DefaultRecycleBinManager>();
        }

        var serviceProvider = services.BuildServiceProvider();

        return builder.UseTypeActivator(serviceProvider);
    }
}
