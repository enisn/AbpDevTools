using AbpDevTools.Configuration;

namespace AbpDevTools.LocalConfigurations;
public class LocalConfiguration
{
    public LocalEnvironmentOption? Environment { get; set; }

    public class LocalEnvironmentOption : EnvironmentOption
    {
        public string? Name { get; set; }
    }
}
