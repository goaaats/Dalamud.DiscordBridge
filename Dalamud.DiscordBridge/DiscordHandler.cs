using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Configuration;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.DiscordBridge.Model;
using Dalamud.DiscordBridge.XivApi;
using Dalamud.Game.Chat;
using Dalamud.Plugin;
using Discord;
using Discord.Rest;
using Discord.Webhook;
using Discord.WebSocket;
using Lumina.Excel.GeneratedSheets;

namespace Dalamud.DiscordBridge
{
    public class DiscordHandler : IDisposable
    {
        private readonly DiscordSocketClient socketClient;
        private readonly SpecialCharsHandler specialChars;

        public bool IsConnected => this.socketClient.ConnectionState == ConnectionState.Connected;
        public ulong UserId => this.socketClient.CurrentUser.Id;

        /// <summary>
        /// Defines if the bot has connected and verified that it has the correct permissions
        /// </summary>
        public DiscordState State { get; private set; } = DiscordState.None;

        private readonly Plugin plugin;

        /// <summary>
        /// Chat types that are set when used the "all" setting.
        /// </summary>
        private static readonly XivChatType[] DefaultChatTypes = new[]
        {
            XivChatType.Say,
            XivChatType.Shout,
            XivChatType.Yell,
            XivChatType.Party,
            XivChatType.CrossParty,
            XivChatType.PvPTeam,
            XivChatType.TellIncoming,
            XivChatType.Alliance,
            XivChatType.FreeCompany,
            XivChatType.Ls1,
            XivChatType.Ls2,
            XivChatType.Ls3,
            XivChatType.Ls4,
            XivChatType.Ls5,
            XivChatType.Ls6,
            XivChatType.Ls7,
            XivChatType.Ls8,
            XivChatType.NoviceNetwork,
            XivChatType.CustomEmote,
            XivChatType.StandardEmote,
            XivChatType.CrossLinkShell1,
            XivChatType.CrossLinkShell2,
            XivChatType.CrossLinkShell3,
            XivChatType.CrossLinkShell4,
            XivChatType.CrossLinkShell5,
            XivChatType.CrossLinkShell6,
            XivChatType.CrossLinkShell7,
            XivChatType.CrossLinkShell8,
            XivChatType.Echo,
        };

        /// <summary>
        /// Embed color signalling that everything is fine.
        /// </summary>
        private const int EmbedColorFine = 0x478CFF;

        /// <summary>
        /// Embed color signalling that everything is bad.
        /// </summary>
        private const int EmbedColorError = 0xD10303;

        /// <summary>
        /// The asynchronous message queue that is responsible for sending messages in order.
        /// </summary>
        public readonly DiscordMessageQueue MessageQueue;

        public DiscordHandler(Plugin plugin)
        {
            this.plugin = plugin;

            this.specialChars = new SpecialCharsHandler();

            this.MessageQueue = new DiscordMessageQueue(this.plugin);

            this.socketClient = new DiscordSocketClient();
            this.socketClient.Ready += SocketClientOnReady;
            this.socketClient.MessageReceived += SocketClientOnMessageReceived;
        }

        public async Task Start()
        {
            if (string.IsNullOrEmpty(this.plugin.Config.DiscordToken))
            {
                this.State = DiscordState.TokenInvalid;

                PluginLog.Error("Token empty, cannot start bot.");
                return;
            }

            try
            {
                await this.socketClient.LoginAsync(TokenType.Bot, this.plugin.Config.DiscordToken);
                await this.socketClient.StartAsync();
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, "Token invalid, cannot start bot.");
            }

            this.MessageQueue.Start();

            PluginLog.Verbose("DiscordHandler START!!");
        }

        private async Task SocketClientOnReady()
        {
            this.State = DiscordState.Ready;
            await this.specialChars.TryFindEmote(this.socketClient);

            PluginLog.Verbose("DiscordHandler READY!!");
        }

        private async Task SocketClientOnMessageReceived(SocketMessage message)
        {
            if (message.Author.IsBot || message.Author.IsWebhook)
                return;

            var args = message.Content.Split();

            foreach (var s in args)
            {
                PluginLog.Verbose(s);
            }

            PluginLog.Verbose("Received command: {0}", args[0]);

            try
            {
                if (args[0] == this.plugin.Config.DiscordBotPrefix + "setchannel" &&
                    await EnsureOwner(message.Author, message.Channel))
                {
                    // Are there parameters?
                    if (args.Length == 1)
                    {
                        await SendGenericEmbed(message.Channel,
                            $"You need to specify some chat kinds to use.\nCheck the ``{this.plugin.Config.DiscordBotPrefix}help`` command for more information.",
                            "Error", EmbedColorError);

                        return;
                    }

                    var kinds = args[1].Split(',');

                    // Is there any chat type that's not recognized?
                    if (kinds
                        .Any(x =>
                        XivChatTypeExtensions.TypeInfoDict.All(y => y.Value.Slug != x) && x != "any"))
                    {
                        PluginLog.Verbose("Could not find kinds");
                        await SendGenericEmbed(message.Channel,
                            $"One or more of the chat kinds you specified could not be found.\nCheck the ``{this.plugin.Config.DiscordBotPrefix}help`` command for more information.",
                            "Error", EmbedColorError);

                        return;
                    }

                    if (!this.plugin.Config.ChannelConfigs.TryGetValue(message.Channel.Id, out var config))
                        config = new DiscordChannelConfig();

                    foreach (var selectedKind in kinds)
                    {
                        PluginLog.Verbose(selectedKind);

                        if (selectedKind == "any")
                        {
                            config.SetUnique(DefaultChatTypes);
                        }
                        else
                        {
                            var chatType = XivChatTypeExtensions.GetBySlug(selectedKind);
                            config.SetUnique(chatType);
                        }
                    }

                    this.plugin.Config.ChannelConfigs[message.Channel.Id] = config;
                    this.plugin.Config.Save();

                    await SendGenericEmbed(message.Channel,
                        $"OK! This channel has been set to receive the following chat kinds:\n\n```{config.ChatTypes.Select(x => $"{x.GetFancyName()}").Aggregate((x, y) => x + "\n" + y)}```",
                        "Chat kinds set", EmbedColorFine);

                    return;
                }

                if (args[0] == this.plugin.Config.DiscordBotPrefix + "unsetchannel" &&
                    await EnsureOwner(message.Author, message.Channel))
                {
                    // Are there parameters?
                    if (args.Length == 1)
                    {
                        await SendGenericEmbed(message.Channel,
                            $"You need to specify some chat kinds to use.\nCheck the ``{this.plugin.Config.DiscordBotPrefix}help`` command for more information.",
                            "Error", EmbedColorError);

                        return;
                    }

                    var kinds = args[1].Split(',');

                    // Is there any chat type that's not recognized?
                    if (kinds.Any(x =>
                        XivChatTypeExtensions.TypeInfoDict.All(y => y.Value.Slug != x) && x != "any"))
                    {
                        await SendGenericEmbed(message.Channel,
                            $"One or more of the chat kinds you specified could not be found.\nCheck the ``{this.plugin.Config.DiscordBotPrefix}help`` command for more information.",
                            "Error", EmbedColorError);

                        return;
                    }

                    if (!this.plugin.Config.ChannelConfigs.TryGetValue(message.Channel.Id, out var config))
                        config = new DiscordChannelConfig();

                    foreach (var selectedKind in kinds)
                    {
                        if (selectedKind == "any")
                        {
                            config.UnsetUnique(DefaultChatTypes);
                        }
                        else
                        {
                            var chatType = XivChatTypeExtensions.GetBySlug(selectedKind);
                            config.UnsetUnique(chatType);
                        }
                    }

                    this.plugin.Config.ChannelConfigs[message.Channel.Id] = config;
                    this.plugin.Config.Save();

                    await SendGenericEmbed(message.Channel,
                        $"OK! This channel has been set to receive the following chat kinds:\n\n```{config.ChatTypes.Select(x => $"{x.GetSlug()} - {x.GetFancyName()}").Aggregate((x, y) => x + "\n" + y)}```",
                        "Chat kinds set", EmbedColorFine);

                    return;
                }

                if (args[0] == this.plugin.Config.DiscordBotPrefix + "setprefix" &&
                    await EnsureOwner(message.Author, message.Channel))
                {
                    // Are there parameters?
                    if (args.Length < 3)
                    {
                        await SendGenericEmbed(message.Channel,
                            $"You need to specify some chat kinds and a prefix to use.\nCheck the ``{this.plugin.Config.DiscordBotPrefix}help`` command for more information.",
                            "Error", EmbedColorError);

                        return;
                    }

                    var kinds = args[1].Split(',');

                    // Is there any chat type that's not recognized?
                    if (kinds.Any(x =>
                        XivChatTypeExtensions.TypeInfoDict.All(y => y.Value.Slug != x) && x != "any"))
                    {
                        await SendGenericEmbed(message.Channel,
                            $"One or more of the chat kinds you specified could not be found.\nCheck the ``{this.plugin.Config.DiscordBotPrefix}help`` command for more information.",
                            "Error", EmbedColorError);

                        return;
                    }

                    if (args[2] == "none")
                        args[2] = string.Empty;

                    foreach (var selectedKind in kinds)
                    {
                        var type = XivChatTypeExtensions.GetBySlug(selectedKind);
                        this.plugin.Config.PrefixConfigs[type] = args[2];
                    }

                    this.plugin.Config.Save();

                    await SendGenericEmbed(message.Channel,
                        $"OK! The following prefixes are set:\n\n```{this.plugin.Config.PrefixConfigs.Select(x => $"{x.Key.GetFancyName()} - {x.Value}").Aggregate((x, y) => x + "\n" + y)}```",
                        "Prefix set", EmbedColorFine);

                    return;
                }

                if (args[0] == this.plugin.Config.DiscordBotPrefix + "toggledf" &&
                    await EnsureOwner(message.Author, message.Channel))
                {
                    if (!this.plugin.Config.ChannelConfigs.TryGetValue(message.Channel.Id, out var config))
                        config = new DiscordChannelConfig();

                    config.IsContentFinder = !config.IsContentFinder;

                    this.plugin.Config.ChannelConfigs[message.Channel.Id] = config;
                    this.plugin.Config.Save();

                    await SendGenericEmbed(message.Channel,
                        $"OK! This channel has been {(config.IsContentFinder ? "enabled" : "disabled")} from receiving Duty Finder notifications.",
                        "Duty Finder set", EmbedColorFine);

                    return;
                }

                if (args[0] == this.plugin.Config.DiscordBotPrefix + "listchannel" &&
                    await EnsureOwner(message.Author, message.Channel))
                {
                    if (!this.plugin.Config.ChannelConfigs.TryGetValue(message.Channel.Id, out var config))
                    {
                        await SendGenericEmbed(message.Channel,
                            $"You didn't set up any channel kinds for this channel yet.\nPlease use the ``{this.plugin.Config.DiscordBotPrefix}setchannel`` command to do this.",
                            "Error", EmbedColorError);
                        return;
                    }

                    await SendGenericEmbed(message.Channel,
                        $"OK! This channel has been set to receive the following chat kinds:\n\n```{config.ChatTypes.Select(x => $"{x.GetFancyName()}").Aggregate((x, y) => x + "\n" + y)}```",
                        "Chat kinds set", EmbedColorFine);

                    return;
                }

                if (args[0] == this.plugin.Config.DiscordBotPrefix + "help")
                {
                    PluginLog.Verbose("Help time");

                    var builder = new EmbedBuilder()
                        .WithTitle("Discord Bridge Help")
                        .WithDescription("You can use the following commands to set up the Discord bridge.")
                        .WithColor(new Color(EmbedColorFine))
                        .AddField("!setchannel", "Select, which kinds of chat should arrive in this channel.\n" +
                                                 "Format: ``!setchannel <kind1,kind2,...>``\n\n" +
                                                 $"See [this link for a list of all available chat kinds]({Constant.KindListLink}) or type ``any`` to enable it for all regular chat messages.")
                        //$"The following chat kinds are available:\n```all - All regular chat\n{XivChatTypeExtensions.TypeInfoDict.Select(x => $"{x.Value.Slug} - {x.Value.FancyName}").Aggregate((x, y) => x + "\n" + y)}```")
                        .AddField("!unsetchannel", "Works like the previous command, but removes kinds of chat from the list of kinds that are sent to this channel.")
                        .AddField("!listchannel", "List all chat kinds that are sent to this channel.")
                        .AddField("!toggledf", "Enable or disable sending duty finder updates to this channel.")
                        .AddField("!setprefix", "Set a prefix for chat kinds. This can be an emoji or a string that will be prepended to every chat message that will arrive with this chat kind.\n" +
                                                "Format: ``!setchannel <kind1,kind2,...> <prefix>``")
                        .AddField("Need more help?",
                            $"You can [read the full step-by-step guide]({Constant.HelpLink}) or [join our Discord server]({Constant.DiscordJoinLink}) to ask for help.")
                        .WithFooter(footer =>
                        {
                            footer
                                .WithText("Dalamud Discord Bridge")
                                .WithIconUrl(Constant.LogoLink);
                        })
                        .WithThumbnailUrl(Constant.LogoLink);
                    var embed = builder.Build();

                    var m = await message.Channel.SendMessageAsync(
                            null,
                            embed: embed)
                        .ConfigureAwait(false);
                    ;
                    PluginLog.Verbose(m.Id.ToString());

                    return;
                }
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, "Could not handle incoming Discord message.");
            }
        }

        private async Task SendGenericEmbed(ISocketMessageChannel channel, string message, string title, uint color)
        {
            var builder = new EmbedBuilder()
                .WithTitle(title)
                .WithDescription(message)
                .WithColor(new Color(color))
                .WithFooter(footer => {
                    footer
                        .WithText("Dalamud Discord Bridge")
                        .WithIconUrl(Constant.LogoLink);
                })
                .WithThumbnailUrl(Constant.LogoLink);
                
            var embed = builder.Build();
            await channel.SendMessageAsync(
                    null,
                    embed: embed)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Check if the sender of this message is set as the owner of this plugin, and send an error message to the specified channel if not null.
        /// </summary>
        /// <param name="user">User in question.</param>
        /// <param name="errorMessageChannel">Channel for error message.</param>
        /// <returns>True if the user is the owner of this plugin.</returns>
        private async Task<bool> EnsureOwner(IUser user, ISocketMessageChannel errorMessageChannel = null)
        {
            PluginLog.Verbose("EnsureOwner: " + user.Username + "#" + user.Discriminator);
            if (user.Username + "#" + user.Discriminator == this.plugin.Config.DiscordOwnerName) 
                return true;

            if (errorMessageChannel == null) 
                return false;

            await SendGenericEmbed(errorMessageChannel, "You are not allowed to run commands for this bot.\n\nIf this is your bot, please use the \"/pdiscord\" command in-game to enter your username.", "Error", EmbedColorError);

            return false;
        }

        public async Task SendChatEvent(string message, string senderName, string senderWorld, XivChatType chatType)
        {
            // Special case for outgoing tells, these should be sent under Incoming tells
            if (chatType == XivChatType.TellOutgoing) {
                chatType = XivChatType.TellIncoming;
            }

            var applicableChannels =
                this.plugin.Config.ChannelConfigs.Where(x => x.Value.ChatTypes.Contains(chatType));

            if (!applicableChannels.Any())
                return;

            message = this.specialChars.TransformToUnicode(message);

            var avatarUrl = Constant.LogoLink;
            try
            {
                avatarUrl = (await XivApiClient.GetCharacterSearch(senderName, senderWorld)).AvatarUrl;
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, "Cannot fetch XIVAPI character search.");
            }

            var displayName = senderName + (string.IsNullOrEmpty(senderWorld) || string.IsNullOrEmpty(senderName)
                ? ""
                : $"@{senderWorld}");

            this.plugin.Config.PrefixConfigs.TryGetValue(chatType, out var prefix);

            foreach (var channelConfig in applicableChannels)
            {
                var socketChannel = this.socketClient.GetChannel(channelConfig.Key);

                if (socketChannel == null)
                {
                    PluginLog.Error("Could not find channel {0} for {1}", channelConfig.Key, chatType);
                    continue;
                }

                var webhookClient = await GetOrCreateWebhookClient(socketChannel);
                await webhookClient.SendMessageAsync($"{prefix}**[{chatType.GetSlug()}]** {message}",
                    username: displayName, avatarUrl: avatarUrl);
            }
        }

        public async Task SendContentFinderEvent(QueuedContentFinderEvent cfEvent)
        {
            var applicableChannels =
                this.plugin.Config.ChannelConfigs.Where(x => x.Value.IsContentFinder);

            if (!applicableChannels.Any())
                return;

            var iconFolder = cfEvent.ContentFinderCondition.Image / 1000 * 1000;

            var embedBuilder = new EmbedBuilder()
                .WithCurrentTimestamp()
                .WithColor(0x297c00)
                .WithTitle("Duty is ready: " + cfEvent.ContentFinderCondition.Name)
                .WithImageUrl("https://xivapi.com" + $"/i/{iconFolder}/{cfEvent.ContentFinderCondition.Image}.png")
                .WithFooter(footer =>
                {
                    footer
                        .WithText("For: " + this.plugin.Interface.ClientState.LocalPlayer.Name)
                        .WithIconUrl(Constant.LogoLink);
                });

            foreach (var channelConfig in applicableChannels)
            {
                var socketChannel = this.socketClient.GetChannel(channelConfig.Key);

                if (socketChannel == null)
                {
                    PluginLog.Error("Could not find channel {0} for cfc", channelConfig.Key);
                    continue;
                }

                var webhookClient = await GetOrCreateWebhookClient(socketChannel);
                await webhookClient.SendMessageAsync(embeds: new[] {embedBuilder.Build()},
                    username: "Dalamud Discord Bridge", avatarUrl: Constant.LogoLink);
            }
        }

        /// <summary>
        /// Get the webhook for the respective channel, or create one if it doesn't exist.
        /// </summary>
        /// <param name="channel">The channel to get the webhook for</param>
        /// <returns><see cref="IWebhook"/> for the respective channel.</returns>
        private async Task<DiscordWebhookClient> GetOrCreateWebhookClient(SocketChannel channel)
        {
            if (!(channel is SocketTextChannel textChannel))
                throw new ArgumentNullException(nameof(textChannel));

            if (!this.plugin.Config.ChannelConfigs.TryGetValue(channel.Id, out var channelConfig))
                throw new ArgumentException("No configuration for channel.", nameof(channel));

            IWebhook hook;
            if (channelConfig.WebhookId != 0)
                hook = await textChannel.GetWebhookAsync(channelConfig.WebhookId) ?? await textChannel.CreateWebhookAsync("FFXIV Bridge Worker");
            else
                hook = await textChannel.CreateWebhookAsync("FFXIV Bridge Worker");
            
            this.plugin.Config.ChannelConfigs[channel.Id].WebhookId = hook.Id;
            this.plugin.Config.Save();

            PluginLog.Verbose("Webhook for {0} OK!! {1}", channel.Id, hook.Id);

            return new DiscordWebhookClient(hook);
        }

        public void Dispose()
        {
            PluginLog.Verbose("Discord DISPOSE!!");
            this.MessageQueue?.Stop();
            this.socketClient?.LogoutAsync().GetAwaiter().GetResult();
            this.socketClient?.Dispose();
        }
    }
}
