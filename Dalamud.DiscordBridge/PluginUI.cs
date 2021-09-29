using System;
using System.Diagnostics;
using System.Numerics;
using Dalamud.Plugin;
using ImGuiNET;

namespace Dalamud.DiscordBridge
{
    public class PluginUI
    {
        private readonly Plugin plugin;

        public PluginUI(Plugin plugin)
        {
            this.plugin = plugin;
        }

        private bool isVisible;

        private string token;
        private string username;

        private static Vector4 errorColor = new Vector4(1f, 0f, 0f, 1f);
        private static Vector4 fineColor = new Vector4(0.337f, 1f, 0.019f, 1f);

        public void Show()
        {
            this.token = this.plugin.Config.DiscordToken;
            this.username = this.plugin.Config.DiscordOwnerName;

            this.isVisible = true;
        }

        public void Draw()
        {
            if (!this.isVisible)
                return;

            ImGui.Begin("Discord Bridge Setup", ref this.isVisible);

            ImGui.Text("In this window, you can set up the XIVLauncher Discord Bridge.\n\n" +
                       "To begin, enter your discord bot token and username below, then click \"Save\".\n" +
                       "As soon as the red text says \"connected\", click the \"Join my server\" button and add the bot to one of your personal servers.\n" +
                       $"You can then use the {this.plugin.Config.DiscordBotPrefix}help command in your discord server to specify channels.");

            ImGui.Dummy(new Vector2(10, 10));

            ImGui.InputText("Enter your bot token", ref this.token, 100);
            ImGui.InputText("Enter your Username(e.g. user#0000)", ref this.username, 50);

            ImGui.Dummy(new Vector2(10, 10));

            ImGui.Text("Status: ");
            ImGui.SameLine();

            var message = this.plugin.Discord.State switch
            {
                DiscordState.None => "Not started",
                DiscordState.Ready => "Connected!",
                DiscordState.TokenInvalid => "Token empty or invalid.",
                _ => "Unknown"
            };

            ImGui.TextColored(this.plugin.Discord.State == DiscordState.Ready ? fineColor : errorColor, message);
            if (this.plugin.Discord.State == DiscordState.Ready && ImGui.Button("Join my server"))
            {
                Process.Start(
                    // $"https://discordapp.com/oauth2/authorize?client_id={this.plugin.Discord.UserId}&scope=bot&permissions=537258064");
                    $"https://discordapp.com/oauth2/authorize?client_id={this.plugin.Discord.UserId}&scope=bot&permissions=2684742720");
            }

            ImGui.Dummy(new Vector2(10, 10));

            if (ImGui.Button("How does this work?"))
            {
                Process.Start(Constant.HelpLink);
            }

            ImGui.SameLine();

            if (ImGui.Button("Save"))
            {
                PluginLog.Verbose("Reloading Discord...");

                this.plugin.Config.DiscordToken = this.token;
                this.plugin.Config.DiscordOwnerName = this.username;
                this.plugin.Config.Save();

                this.plugin.Discord.Dispose();
                this.plugin.Discord = new DiscordHandler(this.plugin);
                this.plugin.Discord.Start();
            }
        }
    }
}
