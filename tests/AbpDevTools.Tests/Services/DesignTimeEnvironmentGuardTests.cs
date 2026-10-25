using AbpDevTools.Services;
using Xunit;

namespace AbpDevTools.Tests.Services;

public class DesignTimeEnvironmentGuardTests
{
    [Theory]
    [InlineData("", EnvironmentSupport.Missing)]
    [InlineData(".AddEnvironmentVariables()", EnvironmentSupport.Supported)]
    public void CommonEfFactoryFollowsConnectionStringToLocalHelper(string setup, EnvironmentSupport expected)
    {
        var source = $$"""
            class Factory : IDesignTimeDbContextFactory<C>
            {
                public C CreateDbContext(string[] args)
                {
                    var configuration = BuildConfiguration();
                    var options = new DbContextOptionsBuilder<C>();
                    options.UseSqlServer(configuration.GetConnectionString("Default"));
                    return new C(options.Options);
                }
                private static IConfigurationRoot BuildConfiguration()
                {
                    var builder = new ConfigurationBuilder()
                        .SetBasePath(".")
                        .AddJsonFile("appsettings.json", optional: false){{setup}};
                    return builder.Build();
                }
            }
            """;
        Assert.Equal(expected, DesignTimeEnvironmentGuard.InspectSource(source));
    }

    [Theory]
    [InlineData("", EnvironmentSupport.Missing)]
    [InlineData(".AddEnvironmentVariables()", EnvironmentSupport.Supported)]
    public void ProjectInspectionReportsFactoryContext(string setup, EnvironmentSupport expected)
    {
        var directory = Path.Combine(Path.GetTempPath(), "guard-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        try
        {
            var project = new FileInfo(Path.Combine(directory, "Sample.csproj"));
            File.WriteAllText(project.FullName, "<Project />");
            File.WriteAllText(Path.Combine(directory, "Factory.cs"),
                "class Factory : IDesignTimeDbContextFactory<C> { public C CreateDbContext(string[] a) { return new C(new ConfigurationBuilder()" + setup + ".Build()); } }");
            Directory.CreateDirectory(Path.Combine(directory, "obj"));
            File.WriteAllText(Path.Combine(directory, "obj", "Broken.cs"), "invalid source");
            var result = new DesignTimeEnvironmentGuard().Inspect(project);
            Assert.Equal(expected, result.Support);
            Assert.Contains("Factory.cs; factory Factory", result.Context);
        }
        finally { Directory.Delete(directory, true); }
    }

    [Theory]
    [InlineData("", EnvironmentSupport.Missing)]
    [InlineData(".AddEnvironmentVariables()", EnvironmentSupport.Supported)]
    [InlineData("/* .AddEnvironmentVariables() */", EnvironmentSupport.Missing)]
    [InlineData(".AddJsonFile(\"AddEnvironmentVariables()\")", EnvironmentSupport.Missing)]
    [InlineData(".ExternalSetup()", EnvironmentSupport.Unresolved)]
    public void InspectsOnlyRelevantBuilder(string setup, EnvironmentSupport expected)
    {
        var source = "class Factory : IDesignTimeDbContextFactory<Context> { public Context CreateDbContext(string[] args) { var config = BuildConfiguration(); return new Context(config); } private static IConfigurationRoot BuildConfiguration() { var builder = new ConfigurationBuilder().AddJsonFile(\"appsettings.json\")" + setup + "; return builder.Build(); } void Unrelated() { new ConfigurationBuilder().AddEnvironmentVariables().Build(); } }";
        Assert.Equal(expected, DesignTimeEnvironmentGuard.InspectSource(source));
    }

    [Theory]
    [InlineData("class Factory : IDesignTimeDbContextFactory<C> { public C CreateDbContext(string[] a) { return new C(new ConfigurationBuilder().AddEnvironmentVariables().Build()); } }", EnvironmentSupport.Supported)]
    [InlineData("class Factory : IDesignTimeDbContextFactory<C> { public C CreateDbContext(string[] a) { var other = new ConfigurationBuilder().AddEnvironmentVariables(); return new C(new ConfigurationBuilder().AddJsonFile(\"appsettings.json\").Build()); } }", EnvironmentSupport.Missing)]
    public void DirectFactoryUsesOnlyReturnedConfiguration(string source, EnvironmentSupport expected)
        => Assert.Equal(expected, DesignTimeEnvironmentGuard.InspectSource(source));

    [Theory]
    [InlineData("class Factory : BaseFactory {}")]
    [InlineData("class BaseFactory : IDesignTimeDbContextFactory<C> { public C CreateDbContext(string[] a) { return new C(new ConfigurationBuilder().Build()); } } class Factory : BaseFactory {}")]
    [InlineData("abstract class Factory : IDesignTimeDbContextFactory<C> { public C CreateDbContext(string[] a) { return new C(new ConfigurationBuilder().Build()); } }")]
    [InlineData("class Factory : IDesignTimeDbContextFactory<C> { public C CreateDbContext(string[] a) { var b = new ConfigurationBuilder(); var ignored = Helpers.Configure(b); return new C(b.Build()); } }")]
    [InlineData("class Factory : IDesignTimeDbContextFactory<C> { public C CreateDbContext(string[] a) { return new C(new ConfigurationBuilder().AddEnvironmentVariables(\"PREFIX_\").Build()); } }")]
    [InlineData("class Factory : IDesignTimeDbContextFactory<C> { public C CreateDbContext(string[] a) { return Helper.Create(); } }")]
    [InlineData("class Factory : IDesignTimeDbContextFactory<C> { public C CreateDbContext(string[] a) { var b = new ConfigurationBuilder(); if (flag) b.AddEnvironmentVariables(); return new C(b.Build()); } }")]
    [InlineData("class Factory : IDesignTimeDbContextFactory<C> {")]
    public void UnsupportedSourceIsUnresolved(string source)
        => Assert.Equal(EnvironmentSupport.Unresolved, DesignTimeEnvironmentGuard.InspectSource(source));
}
