using System.Collections.Generic;
using Dalamud.Configuration;
using Dalamud.DiscordBridge.Model;
using Dalamud.Plugin;
using Newtonsoft.Json;

namespace Dalamud.DiscordBridge
{
    public class Configuration : IPluginConfiguration
    {
        public int Version { get; set; }

        // Add any other properties or methods here.
        [JsonIgnore] private DalamudPluginInterface pluginInterface;

        public string DiscordToken { get; set; } = string.Empty;
        public string DiscordOwnerName { get; set; } = string.Empty;
        public string DiscordBotPrefix { get; set; } = "!";

        public Dictionary<ulong, DiscordChannelConfig> ChannelConfigs = new Dictionary<ulong, DiscordChannelConfig>();

        public void Initialize(DalamudPluginInterface pluginInterface)
        {
            this.pluginInterface = pluginInterface;
        }

        public void Save()
        {
            this.pluginInterface.SavePluginConfig(this);
        }
    }
}
