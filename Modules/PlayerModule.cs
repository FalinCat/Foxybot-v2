using Discord;
using Discord.Addons.Hosting;
using Discord.Commands;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using Victoria;
using Victoria.Enums;
using Victoria.EventArgs;

namespace Foxybot.Modules
{
    public class PlayerModule : ModuleBase<SocketCommandContext>
    {
        private readonly LavaNode _lavaNode;
        ILogger<DiscordClientService> _logger;
        private readonly ConcurrentDictionary<ulong, IUserMessage> nowPlayLastMessage = new ConcurrentDictionary<ulong, IUserMessage>();  // textChannel - message

        public PlayerModule(LavaNode lavaNode, ILogger<DiscordClientService> logger)
        {
            _lavaNode = lavaNode;
            _logger = logger;

            _lavaNode.OnTrackEnded += _lavaNode_OnTrackEnded;
            _lavaNode.OnTrackStarted += _lavaNode_OnTrackStarted;
            //_lavaNode.OnTrackStuck += _lavaNode_OnTrackStuck;
        }

        private async Task _lavaNode_OnTrackEnded(TrackEndedEventArgs arg)
        {
            var guild = arg.Player.VoiceChannel.Guild.Id;
            var player = arg.Player;

            if (player.Track == null && player.Queue.Count >= 1)
            {
                player.Queue.TryDequeue(out var track);
                await player.PlayAsync(track);
            }

            if (player.Queue.Count == 0 && player.PlayerState != PlayerState.Playing)
            {
                EmbedBuilder builder = new EmbedBuilder();
                var color = new Color(64, 224, 208);
                builder
                    .WithTitle("Конец очереди :confused: ")
                    .WithDescription("Пойду я...")
                    .WithColor(color);
                if (nowPlayLastMessage.ContainsKey(arg.Player.TextChannel.Id))
                {
                    var msg = nowPlayLastMessage[arg.Player.TextChannel.Id];
                    await msg.DeleteAsync();
                }
                await arg.Player.TextChannel.SendMessageAsync("", false, builder.Build());
                await _lavaNode.LeaveAsync(player.VoiceChannel);
            }

        }

        private async Task _lavaNode_OnTrackStarted(TrackStartEventArgs arg)
        {
            var track = arg.Player.Track;
            EmbedBuilder embed = new EmbedBuilder();
            var color = new Color(64, 224, 208);
            var format = "mm:ss";
            if (track.Duration > TimeSpan.FromMinutes(60))
            {
                format = "HH:mm:ss";
            }
            embed
                .AddField($"{track.Author}", $"[{track.Title}]({track.Url})", true)
                .AddField("Длительность", $"{new DateTime(track.Duration.Ticks).ToString(format)}", true)

                .WithCurrentTimestamp()
                .WithColor(color)
                .WithThumbnailUrl($"https://img.youtube.com/vi/{track.Id}/0.jpg");

            var author = new EmbedAuthorBuilder()
            {
                IconUrl = "https://gif-avatars.com/img/90x90/fox.gif",
                Name = "♪♪♪ Сейчас играет ♪♪♪"
            };
            embed.Author = author;

            nowPlayLastMessage[arg.Player.TextChannel.Id] = await arg.Player.TextChannel.SendMessageAsync("", false, embed.Build());
        }


    }
}
