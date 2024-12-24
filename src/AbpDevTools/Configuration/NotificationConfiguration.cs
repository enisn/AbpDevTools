﻿using System.Text.Json;
using YamlDotNet.Serialization;

namespace AbpDevTools.Configuration;

[RegisterTransient]
public class NotificationConfiguration : ConfigurationBase<NotificationOption>
{
    public NotificationConfiguration(IDeserializer yamlDeserializer, ISerializer yamlSerializer) 
        : base(yamlDeserializer, yamlSerializer)
    {
    }

    public override string FileName => "notifications";

    public void SetOptions(NotificationOption options)
    {
        File.WriteAllText(FilePath, JsonSerializer.Serialize(options, new JsonSerializerOptions
        {
            WriteIndented = true
        }));
    }

    protected override NotificationOption GetDefaults() => new();
}

public class NotificationOption
{
    public bool Enabled { get; set; }
}