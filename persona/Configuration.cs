using Dalamud.Configuration;
using System;
using System.Collections.Generic;

namespace persona;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;

    public bool ShowSettingMessages { get; set; } = true;

    public HashSet<string> WhitelistedMods { get; set; } = new();
    public Dictionary<string, HashSet<string>> WhitelistedModItems { get; set; } = new();
    public HashSet<string> BlacklistedDesigns { get; set; } = new();

    public void Save()
    {
        Plugin.PluginInterface.SavePluginConfig(this);
    }
}
