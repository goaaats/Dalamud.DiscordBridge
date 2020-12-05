using System;
using Dalamud.DiscordBridge.Attributes;
using Dalamud.DiscordBridge.Model;
using Dalamud.Game.Chat;
using Dalamud.Game.Chat.SeStringHandling;
using Dalamud.Game.Chat.SeStringHandling.Payloads;
using Dalamud.Plugin;

namespace Dalamud.DiscordBridge
{
    public class Plugin : IDalamudPlugin
    {
        private PluginCommandManager<Plugin> commandManager;
        private PluginUI ui;

        public DalamudPluginInterface Interface;
        public DiscordHandler Discord;
        public Configuration Config;

        public string Name => "Dalamud.DiscordBridge";

        public void Initialize(DalamudPluginInterface pluginInterface)
        {
            this.Interface = pluginInterface;

            this.Config = (Configuration)this.Interface.GetPluginConfig() ?? new Configuration();
            this.Config.Initialize(this.Interface);

            this.Discord = new DiscordHandler(this);
            this.Discord.Start();

            this.ui = new PluginUI(this);
            this.Interface.UiBuilder.OnBuildUi += this.ui.Draw;

            this.Interface.Framework.Gui.Chat.OnChatMessage += ChatOnOnChatMessage;

            this.commandManager = new PluginCommandManager<Plugin>(this, this.Interface);

            if (string.IsNullOrEmpty(this.Config.DiscordToken))
            {
                this.Interface.Framework.Gui.Chat.PrintError("The Discord Bridge plugin was installed successfully." +
                                                              "Please use the \"/pdiscord\" command to set it up.");
            }
        }

        private void ChatOnOnChatMessage(XivChatType type, uint senderid, ref SeString sender, ref SeString message, ref bool ishandled)
        {
            this.Discord.MessageQueue.Enqueue(new QueuedChatEvent
            {
                ChatType = type,
                Message = message,
                Sender = sender
            });
        }

        [Command("/pdiscord")]
        [HelpMessage("Show settings for the discord bridge plugin.")]
        public void OpenSettingsCommand(string command, string args)
        {
            this.ui.Show();
        }

        [Command("/ddebug")]
        [HelpMessage("Show settings for the discord bridge plugin.")]
        public void DebugCommand(string command, string args)
        {
            this.Discord.MessageQueue.Enqueue(new QueuedChatEvent
            {
                ChatType = XivChatType.Say,
                Message = new SeString(new Payload[]{new TextPayload("Test Message"), }),
                Sender = new SeString(new Payload[]{new TextPayload("Test Sender"), })
            });
        }

        #region IDisposable Support
        protected virtual void Dispose(bool disposing)
        {
            if (!disposing) return;

            this.Discord.Dispose();

            this.commandManager.Dispose();

            this.Interface.SavePluginConfig(this.Config);

            this.Interface.UiBuilder.OnBuildUi -= this.ui.Draw;

            this.Interface.Dispose();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
