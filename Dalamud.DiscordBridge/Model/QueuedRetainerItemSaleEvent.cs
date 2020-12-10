using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dalamud.DiscordBridge.XivApi;
using Dalamud.Game.Chat;
using Dalamud.Game.Chat.SeStringHandling;

namespace Dalamud.DiscordBridge.Model
{
    public class QueuedRetainerItemSaleEvent : QueuedXivEvent
    {
        public SeString Message { get; set; }
        public SeString Sender { get; set; }
        public XivChatType ChatType { get; set; }
    }
}
