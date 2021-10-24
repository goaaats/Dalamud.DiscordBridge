using System;
using Dalamud.DiscordBridge.Model;
using Dalamud.Game.Text;

namespace Dalamud.DiscordBridge.API
{
    public class DiscordBridgeAPI : IDiscordBridgeAPI
    {
        private readonly bool initialized;
        private readonly Plugin plugin;
        
        public DiscordBridgeAPI(Plugin plugin)
        {
            this.plugin = plugin;
            this.initialized = true;
        }

        public int APIVersion => 1;
        
        public void SendMessage(string pluginName, string avatarUrl, string message)
        {
            this.CheckInitialized();
            this.plugin.Discord.MessageQueue.Enqueue(new QueuedChatEvent
            {
                Sender = pluginName,
                AvatarUrl = avatarUrl,
                Message = message,
                ChatType = XivChatTypeExtensions.IpcChatType,
            });
        }

        private void CheckInitialized()
        {
            if (!this.initialized)
            {
                throw new Exception("API is not initialized.");
            }
        }
    }
}