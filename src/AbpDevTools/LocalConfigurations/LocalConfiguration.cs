using AbpDevTools.Configuration;

namespace AbpDevTools.LocalConfigurations;
public class LocalConfiguration
{
    public LocalRunOption? Run { get; set; }
    public LocalEnvironmentOption? Environment { get; set; }

    public class LocalEnvironmentOption : EnvironmentOption
    {
        public string? Name { get; set; }
    }

    public class LocalRunOption
    {
        public bool Watch { get; set; }
        public bool NoBuild { get; set; }
        public bool GraphBuild { get; set; }
        public string? Configuration { get; set; }
        public bool SkipMigrate { get; set; }
        public string[] Projects { get; set; } = Array.Empty<string>();
    }
}
