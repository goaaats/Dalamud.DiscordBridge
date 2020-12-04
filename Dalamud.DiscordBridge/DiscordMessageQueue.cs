using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.DiscordBridge.Model;

namespace Dalamud.DiscordBridge
{
    public class DiscordMessageQueue
    {
        private volatile bool runQueue = true;

        private readonly DiscordHandler handler;
        private readonly Thread runnerThread;

        private readonly ConcurrentQueue<QueuedXivMessage> messages = new ConcurrentQueue<QueuedXivMessage>();

        public DiscordMessageQueue(DiscordHandler handler)
        {
            this.handler = handler;
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
            this.runnerThread.Join();
        }

        public void Enqueue(QueuedXivMessage message) => this.messages.Enqueue(message);

        private async void RunMessageQueue()
        {
            while (this.runQueue)
            {
                if (messages.TryDequeue(out var resultMessage))
                {

                }

                Thread.Yield();
            }
        }
    }
}
