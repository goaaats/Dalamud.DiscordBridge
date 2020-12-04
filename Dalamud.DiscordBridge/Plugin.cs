using System;
using Dalamud.DiscordBridge.Attributes;
using Dalamud.Plugin;

namespace Dalamud.DiscordBridge
{
    public class Plugin : IDalamudPlugin
    {
        private DalamudPluginInterface pluginInterface;
        private PluginCommandManager<Plugin> commandManager;
        private PluginUI ui;

        public DiscordHandler Discord;
        public Configuration Config;

        public string Name => "Dalamud.DiscordBridge";

        public void Initialize(DalamudPluginInterface pluginInterface)
        {
            this.pluginInterface = pluginInterface;

            this.Config = (Configuration)this.pluginInterface.GetPluginConfig() ?? new Configuration();
            this.Config.Initialize(this.pluginInterface);

            this.ui = new PluginUI(this);
            this.pluginInterface.UiBuilder.OnBuildUi += this.ui.Draw;

            this.commandManager = new PluginCommandManager<Plugin>(this, this.pluginInterface);

            this.Discord = new DiscordHandler(this);
            this.Discord.Start();

            if (string.IsNullOrEmpty(this.Config.DiscordToken))
            {
                this.pluginInterface.Framework.Gui.Chat.PrintError("The Discord Bridge plugin was installed successfully." +
                                                              "Please use the \"/pdiscord\" command to set it up.");
            }
        }

        [Command("/pdiscord")]
        [HelpMessage("Show settings for the discord bridge plugin.")]
        public void OpenSettingsCommand(string command, string args)
        {
            this.ui.Show();
        }

        #region IDisposable Support
        protected virtual void Dispose(bool disposing)
        {
            if (!disposing) return;

            this.commandManager.Dispose();

            this.pluginInterface.SavePluginConfig(this.Config);

            this.pluginInterface.UiBuilder.OnBuildUi -= this.ui.Draw;

            this.pluginInterface.Dispose();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
