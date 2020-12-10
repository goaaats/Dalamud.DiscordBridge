using System;
using Dalamud.DiscordBridge.Attributes;
using Dalamud.DiscordBridge.Model;
using Dalamud.Game.Chat;
using Dalamud.Game.Chat.SeStringHandling;
using Dalamud.Game.Chat.SeStringHandling.Payloads;
using Dalamud.Plugin;
using Lumina.Excel.GeneratedSheets;

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
            this.Interface.ClientState.CfPop += ClientStateOnCfPop;

            this.commandManager = new PluginCommandManager<Plugin>(this, this.Interface);

            if (string.IsNullOrEmpty(this.Config.DiscordToken))
            {
                this.Interface.Framework.Gui.Chat.PrintError("The Discord Bridge plugin was installed successfully." +
                                                              "Please use the \"/pdiscord\" command to set it up.");
            }
        }

        private void ClientStateOnCfPop(object sender, ContentFinderCondition e)
        {
            this.Discord.MessageQueue.Enqueue(new QueuedContentFinderEvent
            {
                ContentFinderCondition = e
            });
        }

        private void ChatOnOnChatMessage(XivChatType type, uint senderid, ref SeString sender, ref SeString message, ref bool ishandled)
        {
            if (type == XivChatType.RetainerSale)
            {
                this.Discord.MessageQueue.Enqueue(new QueuedRetainerItemSaleEvent 
                {
                    ChatType = type,
                    Message = message,
                    Sender = sender
                });
            }
            else
            {
                this.Discord.MessageQueue.Enqueue(new QueuedChatEvent
                {
                    ChatType = type,
                    Message = message,
                    Sender = sender
                });
            }
            
        }

        [Command("/pdiscord")]
        [HelpMessage("Show settings for the discord bridge plugin.")]
        public void OpenSettingsCommand(string command, string args)
        {
            this.ui.Show();
        }

        [Command("/ddebug")]
        [HelpMessage("Show settings for the discord bridge plugin.")]
        [DoNotShowInHelp]
        public void DebugCommand(string command, string args)
        {
            this.Discord.MessageQueue.Enqueue(new QueuedChatEvent
            {
                ChatType = XivChatType.Say,
                Message = new SeString(new Payload[]{new TextPayload("Test Message"), }),
                Sender = new SeString(new Payload[]{new TextPayload("Test Sender"), })
            });
        }

        [Command("/dsaledebug")]
        [HelpMessage("Show settings for the discord bridge plugin.")]
        [DoNotShowInHelp]
        public void SaleDebugCommand(string command, string args)
        {
            // make a sample sale message. This is using Titanium Ore for an item
            Item sampleitem = Interface.Data.GetExcelSheet<Item>().GetRow(12537);
            SeString sameplesale = new SeString(new Payload[] {new TextPayload("The "), new ItemPayload(Interface.Data, sampleitem.RowId, true), new TextPayload(sampleitem.Name) ,new TextPayload(" you put up for sale in the Crystarium markets has sold for 777 gil (after fees).") });

            // PluginLog.Information($"Trying to make a fake sale: {sameplesale.TextValue}");

            this.Discord.MessageQueue.Enqueue(new QueuedRetainerItemSaleEvent
            {
                ChatType = XivChatType.RetainerSale,
                Message = sameplesale,
                Sender = new SeString(new Payload[] { new TextPayload("Test Sender"), })
            });

            Interface.Framework.Gui.Chat.PrintChat(new XivChatEntry
            {
                MessageBytes = sameplesale.Encode()
            });
        }

        [Command("/dprintlist")]
        [HelpMessage("Show settings for the discord bridge plugin.")]
        [DoNotShowInHelp]
        public void ListCommand(string command, string args)
        {
            foreach (var keyValuePair in XivChatTypeExtensions.TypeInfoDict)
            {
                this.Interface.Framework.Gui.Chat.Print($"{keyValuePair.Key.GetSlug()} - {keyValuePair.Key.GetFancyName()}");
            }
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
