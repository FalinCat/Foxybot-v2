using Discord;
using Discord.Addons.Hosting;
using Discord.Commands;
using Discord.WebSocket;
using Foxybot.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Victoria;

namespace FoxyBot
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var host = BuildHost();
            using (host)
            {
                try
                {
                    await host.RunAsync();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }
        }

        private static IHost BuildHost()
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();

            var builder = new HostBuilder()
                .ConfigureAppConfiguration(x =>
                {
                    x.AddConfiguration(configuration);
                })
                .ConfigureLogging(x =>
                {
                    x.AddConsole();
                    x.SetMinimumLevel(LogLevel.Debug);
                })
                .ConfigureDiscordHost((context, config) =>
                {
                    config.SocketConfig = new DiscordSocketConfig
                    {
                        LogLevel = LogSeverity.Debug,
                        AlwaysDownloadUsers = false,
                        MessageCacheSize = 200,
                        DefaultRetryMode = RetryMode.AlwaysRetry,
                    };

                    config.Token = context.Configuration["Token"];
                })
                .UseCommandService((context, config) =>
                {
                    config.CaseSensitiveCommands = false;
                    config.LogLevel = LogSeverity.Debug;
                    config.DefaultRunMode = RunMode.Async;
                })
                .ConfigureServices((context, services) =>
                {
                    var lConf = new LavaConfig();
                    lConf.Hostname = configuration["Lava_Hostname"];
                    lConf.Port = Convert.ToUInt16(configuration["Lava_Port"]);
                    lConf.Authorization = configuration["Lava_Authorization"];
                    lConf.IsSsl = Convert.ToBoolean(configuration["Lava_IsSsl"]);

                    services
                    .AddHostedService<CommandHandler>()
                    .AddLavaNode(x =>
                    {
                        x.SelfDeaf = true;
                    })
                    .AddSingleton<LavaNode>()
                    .AddSingleton<LavaConfig>(lConf);
                })
                .UseConsoleLifetime();

            return builder.Build();
        }
    }
}