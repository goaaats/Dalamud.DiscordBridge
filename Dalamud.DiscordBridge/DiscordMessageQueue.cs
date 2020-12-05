using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.DiscordBridge.Model;
using Dalamud.DiscordBridge.XivApi;
using Dalamud.Game.Chat;
using Dalamud.Game.Chat.SeStringHandling;
using Dalamud.Game.Chat.SeStringHandling.Payloads;
using Dalamud.Plugin;

namespace Dalamud.DiscordBridge
{
    public class DiscordMessageQueue
    {
        private volatile bool runQueue = true;

        private readonly Plugin plugin;
        private readonly Thread runnerThread;

        private readonly ConcurrentQueue<QueuedXivEvent> eventQueue = new ConcurrentQueue<QueuedXivEvent>();

        public DiscordMessageQueue(Plugin plugin)
        {
            this.plugin = plugin;
            this.runnerThread = new Thread(RunMessageQueue);
        }

        public void Start()
        {
            this.runQueue = true;
            this.runnerThread.Start();
        }

        public void Stop()
        {
            this.runQueue = false;

            if(this.runnerThread.IsAlive)
                this.runnerThread.Join();
        }

        public void Enqueue(QueuedXivEvent @event) => this.eventQueue.Enqueue(@event);

        private async void RunMessageQueue()
        {
            while (this.runQueue)
            {
                if (this.eventQueue.TryDequeue(out var resultEvent))
                {
                    if (resultEvent is QueuedChatEvent chatEvent)
                    {
                        var playerLink = chatEvent.Sender.Payloads.FirstOrDefault(x => x.Type == PayloadType.Player) as PlayerPayload;

                        string senderName;
                        string senderWorld;

                        if (this.plugin.Interface.ClientState.LocalPlayer != null) {
                            if (playerLink == null)
                            {
                                // chat messages from the local player do not include a player link, and are just the raw name
                                // but we should still track other instances to know if this is ever an issue otherwise

                                // Special case 2 - When the local player talks in party/alliance, the name comes through as raw text,
                                // but prefixed by their position number in the party (which for local player may always be 1)
                                if (chatEvent.Sender.TextValue.EndsWith(this.plugin.Interface.ClientState.LocalPlayer.Name))
                                {
                                    senderName = this.plugin.Interface.ClientState.LocalPlayer.Name;
                                }
                                else
                                {
                                    PluginLog.Error("playerLink was null. Sender: {0}", BitConverter.ToString(chatEvent.Sender.Encode()));

                                    senderName = chatEvent.ChatType == XivChatType.TellOutgoing ? this.plugin.Interface.ClientState.LocalPlayer.Name : chatEvent.Sender.TextValue;
                                }

                                senderWorld = this.plugin.Interface.ClientState.LocalPlayer.HomeWorld.GameData.Name;
                            }
                            else
                            {
                                senderName = chatEvent.ChatType == XivChatType.TellOutgoing ? this.plugin.Interface.ClientState.LocalPlayer.Name : playerLink.PlayerName;
                                senderWorld = playerLink.World.Name;
                            }
                        } else {
                            senderName = string.Empty;
                            senderWorld = string.Empty;
                        }

                        try
                        {
                            await this.plugin.Discord.SendChatEvent(chatEvent.Message.TextValue, senderName, senderWorld, chatEvent.ChatType);
                        }
                        catch (Exception e)
                        {
                            PluginLog.Error(e, "Could not send discord message.");
                        }
                    }
                }

                Thread.Yield();
            }
        }
    }
}
