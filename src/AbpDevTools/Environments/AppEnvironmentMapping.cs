using System;
using AbpDevTools.Configuration;

namespace AbpDevTools.Environments;

public class AppEnvironmentMapping
{
    public string EnvironmentName { get; set; } = string.Empty;
    public string AppName { get; set; } = string.Empty;

    public static Dictionary<string, AppEnvironmentMapping> Default { get; } = new()
    {
        {
            "Volo.Abp.EntityFrameworkCore.SqlServer",
            new AppEnvironmentMapping
            {
                AppName = EnvironmentAppConfiguration.SqlServerEdge,
                EnvironmentName = EnvironmentConfiguration.SqlServer
            }
        },
        {
            "Volo.Abp.EntityFrameworkCore.MySQL",
            new AppEnvironmentMapping
            {
                AppName = EnvironmentAppConfiguration.MySql,
                EnvironmentName = EnvironmentConfiguration.MySql
            }
        },
        {
            "Volo.Abp.EntityFrameworkCore.PostgreSql",
            new AppEnvironmentMapping
            {
                AppName = EnvironmentAppConfiguration.PostgreSql,
                EnvironmentName = EnvironmentConfiguration.PostgreSql
            }
        },
        {
            "Volo.Abp.Caching.StackExchangeRedis",
            new AppEnvironmentMapping
            {
                AppName = EnvironmentAppConfiguration.Redis
            }
        },
        {
            "Volo.Abp.EventBus.RabbitMQ",
            new AppEnvironmentMapping
            {
                AppName = EnvironmentAppConfiguration.RabbitMq
            }
        }
    };
}
