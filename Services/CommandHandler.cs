using Discord.Addons.Hosting;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Reflection;
using Victoria;

namespace Foxybot.Services
{
    public class CommandHandler : DiscordClientService
    {
        private readonly IServiceProvider _provider;
        private readonly DiscordSocketClient _client;
        private readonly CommandService _service;
        private readonly IConfiguration _configuration;
        private readonly LavaNode _lavaNode;
        ILogger<DiscordClientService> _logger;

        public CommandHandler(DiscordSocketClient client, ILogger<DiscordClientService> logger, LavaNode lavaNode, IServiceProvider provider,
            CommandService service, IConfiguration configuration) : base(client, logger)
        {
            _client = client;
            _logger = logger;
            _lavaNode = lavaNode;
            _service = service;
            _provider = provider;
            _configuration = configuration;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _client.Ready += Client_Ready;
            _client.MessageReceived += OnMessageReceived;
            await _client.SetGameAsync(" хорошем настроении");
            await _service.AddModulesAsync(Assembly.GetEntryAssembly(), _provider);
        }

        private async Task OnMessageReceived(SocketMessage socketMessage)
        {
            if (socketMessage is not SocketUserMessage message) return;
            if (message.Source != Discord.MessageSource.User) return;

            var argPos = 0;
            if (!message.HasStringPrefix(_configuration["Prefix"], ref argPos) && !message.HasMentionPrefix(_client.CurrentUser, ref argPos)) return;

            var context = new SocketCommandContext(_client, message);
            await _service.ExecuteAsync(context, argPos, _provider);

        }

        private async Task Client_Ready()
        {
            if (!_lavaNode.IsConnected)
            {
                await _lavaNode.ConnectAsync();
            }
        }
    }
}
