using Discord;
using Discord.Addons.Hosting;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using Victoria;

namespace Foxybot.Modules
{
    public class IteractionModule : ModuleBase<SocketCommandContext>
    {
        private readonly LavaNode _lavaNode;
        ILogger<DiscordClientService> _logger;

        private readonly DiscordSocketClient _client;

        public IteractionModule(DiscordSocketClient client, LavaNode lavaNode, ILogger<DiscordClientService> logger)
        {
            _client = client;
            _lavaNode = lavaNode;
            _logger = logger;

            _logger.LogDebug("Iteraction constructor");


            _client.ButtonExecuted += ButtonHandler;

        }



        [Command("menu")]
        public async Task Spawn()
        {
            var menuBuilder = new SelectMenuBuilder()
                .WithPlaceholder("Select an option")
                .WithCustomId("menu-1")
                .WithMinValues(1)
                .WithMaxValues(1)
                .AddOption("Option A", "opt-a", "Option B is lying!")
                .AddOption("Option B", "opt-b", "Option A is telling the truth!");

            var builder = new ComponentBuilder()
                .WithSelectMenu(menuBuilder);

            await ReplyAsync("Whos really lying?", components: builder.Build());
        }

        [Command("embed")]
        public async Task Embed()
        {
            EmbedBuilder builder = new EmbedBuilder();


            builder
                //.WithTitle("♪♪♪ Сейчас играет ♪♪♪")
                .AddField("Автор", "[Text for link](https://google.com/)", true)
                .AddField("Длительность", "123:157", true)


                .AddField("Автор", "[Text for link](https://google.com/)", true)
                .AddField("Длительность", "123:157", true)
                .AddField("Длительность", "123:157", true)

                .WithUrl("https://google.com");


            //builder.AddField("Cost", "3", false);    // true - for inline

            //builder.AddField("DPS", "42", false);
            //builder.AddField("Hit Speed", "1.5sec", false);
            //builder.AddField("SlowDown", "35%", false);
            //builder.AddField("AOE", "63", false);
            //builder.WithThumbnailUrl("https://via.placeholder.com/100x100"); // Картинка справа вверху
            //builder.ImageUrl = "https://via.placeholder.com/200x100"; // Большая картинка снизу
            //builder.WithUrl("https://via.placeholder.com/200x100"); // Делает Title ссылкой

            var color = new Color(64, 224, 208);
            builder.WithColor(color);
            await Context.Channel.SendMessageAsync("", false, builder.Build());
        }


        [Command("button")]
        public async Task SpawnButton()
        {
            var builder = new ComponentBuilder()
                .WithButton("label", "custom-id");

            await ReplyAsync("Here is a button!", components: builder.Build());
        }


        public async Task ButtonHandler(SocketMessageComponent component)
        {
            // We can now check for our custom id
            switch (component.Data.CustomId)
            {
                // Since we set our buttons custom id as 'custom-id', we can check for it like this:
                case "custom-id":
                    // Lets respond by sending a message saying they clicked the button
                    await component.RespondAsync($"{component.User.Mention} has clicked the button!");
                    break;
            }
        }

    }
}
