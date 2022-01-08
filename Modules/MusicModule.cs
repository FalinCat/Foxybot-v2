using Discord;
using Discord.Addons.Hosting;
using Discord.Commands;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using Victoria;
using Victoria.Enums;
using Victoria.Responses.Search;

namespace Foxybot.Modules
{
    public class MusicModule : ModuleBase<SocketCommandContext>
    {
        private readonly LavaNode _lavaNode;
        ILogger<DiscordClientService> _logger;
        private static readonly ConcurrentDictionary<ulong, IUserMessage> lastSearchResultMessage = new ConcurrentDictionary<ulong, IUserMessage>();  // textChannel - message

        public MusicModule(LavaNode lavaNode, ILogger<DiscordClientService> logger)
        {
            _lavaNode = lavaNode;
            _logger = logger;
        }


        [Command("help", RunMode = RunMode.Async)]
        public async Task HelpAsync()
        {
            var color = new Color(64, 224, 208);
            EmbedBuilder embed = new EmbedBuilder()
            {
                Color = color,
            };
            var author = new EmbedAuthorBuilder()
            {
                IconUrl = "https://gif-avatars.com/img/90x90/fox.gif",
                Name = "Справка "
            };
            embed.Author = author;

            var configuration = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                    .AddEnvironmentVariables()
                    .Build();
            embed.AddField("Префикс", $"Все команды необходимо начинать со знака \"{configuration["Prefix"]}\"");





            embed.AddField("play", "Поиск видео на Youtube");
            embed.AddField("pl", "Добавить плейлист к воспроизведению");
            embed.AddField("stop", "Остановить воспроизведение");
            embed.AddField("pause", "Поставить паузу");
            embed.AddField("resume", "Продолжить воспроизведение");
            embed.AddField("search", "Поиск на Youtube по названию");
            embed.AddField("seek", "Перемотать на позицию. Задается через :. Например 1:27 ");
            embed.AddField("shuffle", "Перемешать очередь");
            embed.AddField("volume", "Узнать или поставить громкость");
            embed.AddField("stop", "Остановить воспроизведение");
            embed.AddField("next", "Добавить трек в начало очереди");
            embed.AddField("clear", "Очистить очередь");
            embed.AddField("save", "Сохранить себе песню");
            embed.AddField("mix", "Добавить ютубовский микс по треку");



            await ReplyAsync(embed: embed.Build());
        }


        [Command("now", RunMode = RunMode.Async)]
        public async Task PlayNowAsync()
        {
            if (!_lavaNode.TryGetPlayer(Context.Guild, out var player))
                return;

            var track = player.Track;
            var color = new Color(64, 224, 208);
            EmbedBuilder embed = new EmbedBuilder()
            {
                Color = color,
                //Title = "Сейчас играет ♪♪♪",
            };
            var author = new EmbedAuthorBuilder()
            {
                IconUrl = "https://gif-avatars.com/img/90x90/fox.gif",
                Name = "♪♪♪ Сейчас играет ♪♪♪"
            };
            embed.WithAuthor(author);
            var format = "mm:ss";
            if (track.Duration > TimeSpan.FromMinutes(60))
            {
                format = "HH:mm:ss";
            }

            embed.AddField($"{track.Author}", $"[{track.Title}]({track.Url})");
            embed.AddField("Длительность", $"{new DateTime(track.Position.Ticks).ToString(format)} /" +
                $" { new DateTime(track.Duration.Ticks).ToString(format)}", true);
            embed.WithThumbnailUrl($"https://img.youtube.com/vi/{player.Track.Id}/0.jpg");


            await ReplyAsync(null, false, embed.Build());
        }


        [Command("next", RunMode = RunMode.Async)]
        public async Task PlayNextAsync([Remainder] string query)
        {
            if (string.IsNullOrWhiteSpace(query)) // На случай непонятных происшествий
            {
                await SimpleAnswer("Совершенно непонятный запрос", Color.Red);
                return;
            }

            if (!await CheckUserInChannel()) return;
            if (!await CheckBotInAnotherChannel()) return;

            await TryConnectToVoiceAsync();
            if (!_lavaNode.TryGetPlayer(Context.Guild, out var player))
                return;

            if (player.PlayerState == PlayerState.Playing || player.PlayerState == PlayerState.Paused)
            {
                LavaTrack? track = null;
                if (Uri.TryCreate(query, UriKind.Absolute, out Uri? uri) && uri.Scheme == Uri.UriSchemeHttps)
                {
                    var trackParameters = await ParseLinkAsync(query);
                    if (trackParameters == null) return; // Если не получилось выяснить ID видео

                    if (trackParameters.ContainsKey("list"))
                    {
                        await SimpleAnswer("Я смотрю тут ссылка на плейлист. Для добавления всего плейлиста в очередь используйте команду \"pl\"", Color.LightOrange);
                    }

                    var result = await _lavaNode.SearchAsync(SearchType.Direct, trackParameters["id"]);
                    track = result.Tracks.Where(track => track.Id == trackParameters["id"]).FirstOrDefault();

                }
                else  // Если запрос не ссылка - тип поиска Youtube (не по ID)
                {
                    track = _lavaNode.SearchAsync(SearchType.YouTube, query).Result.Tracks.FirstOrDefault();
                }

                if (track == null)
                {
                    await SimpleAnswer("Не получилось найти нужное", Color.Red);
                    return;
                }

                var tmpQueue = player.Queue.ToList();
                tmpQueue.Insert(0, track);
                player.Queue.Clear();
                foreach (var item in tmpQueue)
                {
                    player.Queue.Enqueue(item);
                }

                var color = new Color(64, 224, 208);
                EmbedBuilder embed = new EmbedBuilder()
                {
                    Color = color,
                };

                var author = new EmbedAuthorBuilder()
                {
                    IconUrl = "https://gif-avatars.com/img/90x90/fox.gif",
                    Name = "Добавил трек следующим для воспроизведения"
                };
                embed.Author = author;
                embed.AddField($"{track.Author}", $"[{track.Title}]({track.Url})", true);
                var duration = track.Duration.Ticks;
                if (duration > TimeSpan.FromHours(1).Ticks)
                {
                    embed.AddField($"Длительность", $"{new DateTime(track.Duration.Ticks).ToString("HH:mm:ss")}", true);
                }
                else
                {
                    embed.AddField($"Длительность", $"{new DateTime(track.Duration.Ticks).ToString("mm:ss")}", true);
                }

                await ReplyAsync(null, false, embed.Build());
            }
            else
            {
                await PlayAsync(query);
            }

        }


        [Command("q", RunMode = RunMode.Async)]
        public async Task GetQueueAsync()
        {
            if (!_lavaNode.TryGetPlayer(Context.Guild, out var player))
                return;

            var color = new Color(64, 224, 208);
            EmbedBuilder embed = new EmbedBuilder()
            {
                Color = color,
            };
            var format = "mm:ss";

            //embed.WithTitle("Очередь");
            var author = new EmbedAuthorBuilder()
            {
                IconUrl = "https://gif-avatars.com/img/90x90/fox.gif",
                Name = "Очередь воспроизведения"
            };

            embed.Author = author;
            var url = $"https://img.youtube.com/vi/{player.Track.Id}/0.jpg";
            embed.WithThumbnailUrl(url);
            embed.Author = author;



            var currentPositionString = new DateTime(player.Track.Position.Ticks).ToString("mm::ss");
            var totalDurationString = new DateTime(player.Track.Duration.Ticks).ToString("mm:ss");
            embed.AddField("♪♪♪ Сейчас играет ♪♪♪", $"[{player.Track.Title}]({player.Track.Url}) | {currentPositionString} / {totalDurationString}", false);


            int position = 1;
            foreach (LavaTrack track in player.Queue)
            {
                var duration = new DateTime(track.Duration.Ticks).ToString(format);
                embed.AddField($"{position}. {track.Author}", $"[{track.Title}]({track.Url}) | {duration}");
                position++;

                if (position > 10)
                {
                    embed.AddField("Слишком длинная очередь", $"Не отображено еще {player.Queue.Count - 10} треков");
                    break;
                }
            }

            var totalDurationOfQueue = new DateTime(player.Queue.Sum(track => track.Duration.Ticks) + (player.Track.Duration - player.Track.Position).Ticks);

            if (totalDurationOfQueue.Ticks > TimeSpan.FromHours(1).Ticks)
            {
                format = "HH:mm:ss";
            }

            embed.WithFooter($"Общая продолжительность: {totalDurationOfQueue.ToString(format)}");
            await ReplyAsync(null, false, embed.Build());
        }


        [Command("P", RunMode = RunMode.Async)]
        public async Task PlayShortcutAsync([Remainder] string query)
        {
            await PlayAsync(query);
        }


        [Command("Play", RunMode = RunMode.Async)]
        public async Task PlayAsync([Remainder] string query)
        {
            if (string.IsNullOrWhiteSpace(query)) // На случай непонятных происшествий
            {
                await SimpleAnswer("Совершенно непонятный запрос", Color.Red);
                return;
            }

            if (!await CheckUserInChannel()) return;
            if (!await CheckBotInAnotherChannel()) return;


            // Проверка запроса для выбора трека из выдачи
            if (int.TryParse(query, out int number))
            {
                await PlayTrackNumberAsync(number);
                return;
            }


            // Если передали ссылку
            LavaTrack? track = null;
            if (Uri.TryCreate(query, UriKind.Absolute, out Uri? uri) && uri.Scheme == Uri.UriSchemeHttps)
            {
                var trackParameters = await ParseLinkAsync(query);
                if (trackParameters == null) return; // Если не получилось выяснить ID видео

                if (trackParameters.ContainsKey("list"))
                {
                    await SimpleAnswer("Я смотрю тут ссылка на плейлист. Для добавления всего плейлиста в очередь используйте команду \"pl\"", Color.LightOrange);
                }

                //var searchString = $"http://youtube.com/watch?v={trackParameters["id"]}";
                var result = await _lavaNode.SearchAsync(SearchType.Direct, trackParameters["id"]);
                track = result.Tracks.Where(track => track.Id == trackParameters["id"]).FirstOrDefault();

            }
            else  // Если запрос не ссылка - тип поиска Youtube (не по ID)
            {
                track = _lavaNode.SearchAsync(SearchType.YouTube, query).Result.Tracks.FirstOrDefault();

            }

            if (track == null)
            {
                await SimpleAnswer("Не получилось найти нужное", Color.Red);
                return;
            }

            await SendToQueue(track);
        }


        [Command("PL", RunMode = RunMode.Async)]
        public async Task PlaylistAsync([Remainder] string query)
        {
            if (string.IsNullOrWhiteSpace(query)) // На случай непонятных происшествий
            {
                await SimpleAnswer("Совершенно непонятный запрос", Color.Red);
                return;
            }

            if (!await CheckUserInChannel()) return;
            if (!await CheckBotInAnotherChannel()) return;

            // Если передали ссылку
            if (Uri.TryCreate(query, UriKind.Absolute, out Uri? uri) && uri.Scheme == Uri.UriSchemeHttps)
            {
                var track = await ParseLinkAsync(query);
                //if (trackParameters == null) return; // Если не получилось выяснить ID видео

                if (!track.ContainsKey("list"))
                {
                    await SimpleAnswer("Не нахожу ID плейлиста", Color.Red);
                }

                var searchString = $"https://www.youtube.com/playlist?list={track["list"]}";
                var result = await _lavaNode.SearchAsync(SearchType.Direct, searchString);
                await SendToQueue(result.Tracks.ToList());
            }
            else
            {
                await SimpleAnswer("Данная команда понимает только ссылки на плейлисты" + Environment.NewLine +
                    "Можно использовать команду \"mix\" для генерации плейлиста по треку", Color.Red);
            }
        }


        [Command("search", RunMode = RunMode.Async)]
        public async Task SearchAsync([Remainder] string query)
        {
            if (string.IsNullOrWhiteSpace(query)) // На случай непонятных происшествий
            {
                await SimpleAnswer("Совершенно непонятный запрос", Color.Red);
                return;
            }

            if (Uri.TryCreate(query, UriKind.Absolute, out Uri? uri))
            {
                await SimpleAnswer("Пожалуйста, не надо давать для поиска ссылки");
                return;
            }

            var result = await _lavaNode.SearchAsync(SearchType.YouTube, query);


            var color = new Color(64, 224, 208);
            EmbedBuilder embed = new EmbedBuilder()
            {
                Color = color,
            };

            var author = new EmbedAuthorBuilder()
            {
                IconUrl = "https://gif-avatars.com/img/90x90/fox.gif",
                Name = $"Вот что я нашел по запросу {query}:"
            };
            embed.Author = author;

            ushort count = 1;
            foreach (var track in result.Tracks)
            {
                string format = "mm:ss";
                if (track.Duration > TimeSpan.FromHours(1))
                {
                    format = "HH:mm:ss";
                }
                var duration = new DateTime(track.Duration.Ticks).ToString(format);
                embed.AddField($"{track.Author}", $"{count}. [{track.Title}]({track.Url}) | {duration}", false);
                count++;
                if (count > 10)
                {
                    break;
                }
            }

            embed.WithFooter("Для выбора трека используйте команду \"p {номер трека из выдачи}\"");
            var message = await ReplyAsync(embed: embed.Build());
            lastSearchResultMessage[Context.Channel.Id] = message;
        }


        public async Task PlayTrackNumberAsync(int number)
        {
            var message = lastSearchResultMessage[Context.Channel.Id];

            var value = message.Embeds.First().Fields[number - 1].Value;

            Regex regx = new Regex("https://([\\w+?\\.\\w+])+([a-zA-Z0-9\\~\\!\\@\\#\\$\\%\\^\\&amp;\\*\\(\\)_\\-\\=\\+\\\\\\/\\?\\.\\:\\;\\'\\,]*)?", RegexOptions.IgnoreCase);
            MatchCollection matches = regx.Matches(value);

            if (matches.Count < 1)
            {
                await SimpleAnswer("Что-то пошло не так во время поиска трека", Color.Red);
                return;
            }

            if (Uri.TryCreate(matches.First().ToString().TrimEnd(')'), UriKind.Absolute, out Uri? uri))
            {
                var trackParameters = await ParseLinkAsync(uri.ToString());

                var result = await _lavaNode.SearchAsync(SearchType.Direct, trackParameters["id"]);
                var track = result.Tracks.Where(track => track.Id == trackParameters["id"]).FirstOrDefault();
                if (track == null) {
                    await SimpleAnswer("Не получилось скачать инфо о треке для его воспроизведения", Color.Red);
                    return;
                }
                await SendToQueue(track);
            }
        }


        [Command("skip", RunMode = RunMode.Async)]
        public async Task SkipAsync()
        {
            if (!await CheckUserInChannel()) return;
            if (!await CheckBotInAnotherChannel()) return;

            if (!_lavaNode.TryGetPlayer(Context.Guild, out var player))
                return;

            if (player.Queue.Count >= 1)
            {
                await player.SkipAsync();
                await SimpleAnswer("Пропускаем...", Color.LightOrange);
            }
            else
            {
                await player.StopAsync();
            }
        }


        [Command("pause", RunMode = RunMode.Async)]
        public async Task PauseAsync()
        {
            if (!await CheckUserInChannel()) return;
            if (!await CheckBotInAnotherChannel()) return;

            if (!_lavaNode.TryGetPlayer(Context.Guild, out var player))
                return;

            await player.PauseAsync();

            await SimpleAnswer("Ставим паузу...", Color.LightOrange);
        }


        [Command("resume", RunMode = RunMode.Async)]
        public async Task ResumeAsync()
        {
            if (!await CheckUserInChannel()) return;
            if (!await CheckBotInAnotherChannel()) return;

            if (!_lavaNode.TryGetPlayer(Context.Guild, out var player))
                return;

            await SimpleAnswer("Продолжаем играть!", Color.Green);
            await player.ResumeAsync();
        }


        [Command("stop", RunMode = RunMode.Async)]
        public async Task StopAsync()
        {
            if (!await CheckUserInChannel()) return;
            if (!await CheckBotInAnotherChannel()) return;

            if (!_lavaNode.TryGetPlayer(Context.Guild, out var player))
                return;

            await player.StopAsync();
        }


        [Command("remove", RunMode = RunMode.Async)]
        public async Task RemoveAsync([Remainder] string query)
        {
            if (!await CheckUserInChannel()) return;
            if (!await CheckBotInAnotherChannel()) return;

            if (!_lavaNode.TryGetPlayer(Context.Guild, out var player))
                return;


            if (int.TryParse(query, out int n))
            {
                if (n > player.Queue.Count || n <= 0)
                {
                    await SimpleAnswer($"Номер трека должен быть в пределах от 1 до {player.Queue.Count}", Color.Red);
                    return;
                }
                var track = player.Queue.RemoveAt(n - 1);
                await SimpleAnswer($"Трек **{track.Title}** удален из очереди", Color.Green);
            }
            else
            {
                await SimpleAnswer($"Не распознал номер трека", Color.Red);
            }
        }


        [Command("shuffle", RunMode = RunMode.Async)]
        public async Task ShuffleAsync([Remainder] string query)
        {
            if (!await CheckUserInChannel()) return;
            if (!await CheckBotInAnotherChannel()) return;

            if (!_lavaNode.TryGetPlayer(Context.Guild, out var player))
                return;

            if (player.Queue.Count > 1)
            {
                player.Queue.Shuffle();
                await SimpleAnswer("Я перемешал очередь в случайном порядке", Color.Green);
            }
            else
            {
                await SimpleAnswer("Очередь пустая, там нечего перемешивать", Color.Red);
            }
        }


        [Command("clear", RunMode = RunMode.Async)]
        public async Task ClearAsync()
        {
            if (!await CheckUserInChannel()) return;
            if (!await CheckBotInAnotherChannel()) return;

            if (!_lavaNode.TryGetPlayer(Context.Guild, out var player))
                return;

            player.Queue.Clear();
            await SimpleAnswer("Очередь очищенна", Color.Green);
        }


        [Command("save", RunMode = RunMode.Async)]
        public async Task GrabAsync()
        {
            if (!_lavaNode.TryGetPlayer(Context.Guild, out var player))
                return;

            var track = player.Track;
            if (track == null) {
                await SimpleAnswer("Трека нет", Color.Red);
            }
            var color = new Color(64, 224, 208);
            EmbedBuilder embed = new EmbedBuilder()
            {
                Color = color,
            };
            var author = new EmbedAuthorBuilder()
            {
                IconUrl = "https://gif-avatars.com/img/90x90/fox.gif",
                Name = "♪♪♪ Сохраненный трек ♪♪♪"
            };
            embed.WithAuthor(author);
            var format = "mm:ss";
            if (track?.Duration > TimeSpan.FromMinutes(60))
            {
                format = "HH:mm:ss";
            }

            embed.AddField($"{track?.Author}", $"[{track?.Title}]({track?.Url})");
            embed.AddField("Длительность", $"{new DateTime(track.Duration.Ticks).ToString(format)}");
            embed.WithThumbnailUrl($"https://img.youtube.com/vi/{player.Track.Id}/0.jpg");

            await Context.User.SendMessageAsync(embed: embed.Build());
        }


        [Command("volume", RunMode = RunMode.Async)]
        public async Task GetVolumeAsync()
        {
            if (!_lavaNode.TryGetPlayer(Context.Guild, out var player))
            {
                await SimpleAnswer("Бот находится не в голосовом канале", Color.Red);
                return;
            }
            await SimpleAnswer($"Текущая громкость: {player.Volume}%");
        }


        [Command("volume", RunMode = RunMode.Async)]
        public async Task SetVolumeAsync([Remainder] string query)
        {
            if (!_lavaNode.TryGetPlayer(Context.Guild, out var player))
            {
                await SimpleAnswer("Бот находится в не подключенном состоянии", Color.Red);
                return;
            }
            if (ushort.TryParse(query, out ushort value)) {
                if (value > 100 || value < 2)
                {
                    await SimpleAnswer("Громкость надо ставить в пределах от 2 до 100");
                    return;
                }

                await player.UpdateVolumeAsync(value);
                await SimpleAnswer($"Громкость установлена на {value}", Color.Green);
            }
        }


        [Command("seek", RunMode = RunMode.Async)]
        public async Task SeekAsync([Remainder] string query)
        {
            if (string.IsNullOrWhiteSpace(query)) // На случай непонятных происшествий
            {
                await SimpleAnswer("Совершенно непонятный запрос", Color.Red);
                return;
            }

            if (!await CheckUserInChannel()) return;
            if (!await CheckBotInAnotherChannel()) return;

            if (!_lavaNode.TryGetPlayer(Context.Guild, out var player))
            {
                await SimpleAnswer("Бот находится в не подключенном состоянии", Color.Red);
                return;
            }

            if (player.PlayerState == PlayerState.Playing || player.PlayerState == PlayerState.Paused)
            {
                var times = query.Split(':');
                int hours = 0, minutes = 0, sec = 0;
                switch (times.Length)
                {
                    case 3:
                        if (!int.TryParse(times[0], out hours) &
                            !int.TryParse(times[1], out minutes) &
                            !int.TryParse(times[2], out sec))
                        {
                            await SimpleAnswer("Не понимаю на какой момент надо перемотать :worried: ", Color.Red);
                        }

                        break;
                    case 2:
                        if (!int.TryParse(times[0], out minutes) &
                            !int.TryParse(times[1], out sec))
                            await SimpleAnswer("Не понимаю на какой момент надо перемотать :worried: ", Color.Red);

                        break;
                    case 1:
                        if (!int.TryParse(times[0], out sec))
                            await SimpleAnswer("Не понимаю на какой момент надо перемотать :worried: ", Color.Red);
                        break;
                    default:
                        break;
                }
                if (minutes > 59 || sec > 59)
                {
                    await SimpleAnswer("Какой то странный формат времени... Ты часом не Меддо?", Color.Red);
                    return;
                }

                var ts = new TimeSpan(hours, minutes, sec);
                var format = "mm:ss";
                if (ts >= player.Track.Duration)
                {
                    
                    if (player.Track.Duration > TimeSpan.FromHours(1))
                    {
                        format = "HH:mm:ss";
                    }
                    await SimpleAnswer($"Нельзя перемотать дальше, чем длительность трека ({new DateTime(player.Track.Duration.Ticks).ToString(format)})", Color.Red);
                    return;
                }

                await player.SeekAsync(ts);

                await SimpleAnswer($"Перемотал на отметку -> {new DateTime(ts.Ticks).ToString(format)}", Color.Green);
            }
        }


        [Command("mix", RunMode = RunMode.Async)]
        public async Task YoutubeMixAsync([Remainder] string query)
        {
            if (!await CheckUserInChannel()) return;
            if (!await CheckBotInAnotherChannel()) return;

            await TryConnectToVoiceAsync();
            if (!_lavaNode.TryGetPlayer(Context.Guild, out var player))
                return;

            var track = await ParseLinkAsync(query);
            //var searchQuery = $"https://youtu.be/{track["id"]}&list=RDMM&&start_radio=1";
            var searchQuery = $"https://youtu.be/{track["id"]}?list=RDMM";
            var result = await _lavaNode.SearchAsync(SearchType.Direct, searchQuery);

            if (result.Status != SearchStatus.PlaylistLoaded) {
                await SimpleAnswer("Не получилось сгенерировать плейлист");
                return;
            }

            await SendToQueue(result.Tracks.ToList());
        }

        public async Task<Dictionary<string, string>> ParseLinkAsync(string link)
        {
            var dict = new Dictionary<string, string>();

            if (Uri.TryCreate(link, UriKind.Absolute, out Uri? uri)/* && uri.Scheme == Uri.UriSchemeHttps*/)
            {
                string id = "";
                if (uri.Host == "youtu.be")
                {
                    id = uri.LocalPath.TrimStart('/');
                }
                else
                {
                    id = HttpUtility.ParseQueryString(uri?.Query).Get("v");
                }

                if (id != null)
                {
                    dict["id"] = id;
                }


                var list = HttpUtility.ParseQueryString(uri.Query).Get("list");
                if (list != null)
                {
                    dict["list"] = list;
                }

            }

            return dict;
        }


        public async Task SendToQueue(List<LavaTrack> trackList)
        {
            await TryConnectToVoiceAsync();
            if (!_lavaNode.TryGetPlayer(Context.Guild, out var player))
                return;

            if (player.PlayerState == PlayerState.Playing || player.PlayerState == PlayerState.Paused)
            {
                player.Queue.Enqueue(trackList);
                await SimpleAnswer($"В очередь добавлено {trackList.Count} треков", Color.Green);
            }
            else
            {
                await SendToQueue(trackList.First());
                trackList.RemoveAt(0);
                player.Queue.Enqueue(trackList);
                await SimpleAnswer($"В очередь добавлено {trackList.Count} треков", Color.Green);
            }
        }


        public async Task SendToQueue(LavaTrack track)
        {
            await TryConnectToVoiceAsync();
            if (!_lavaNode.TryGetPlayer(Context.Guild, out var player))
                return;

            if (player.PlayerState == PlayerState.Playing || player.PlayerState == PlayerState.Paused)
            {
                player.Queue.Enqueue(track);
                var color = new Color(64, 224, 208);
                var format = "mm:ss";
                if (track.Duration > TimeSpan.FromMinutes(60))
                {
                    format = "HH:mm:ss";
                }
                EmbedBuilder embed = new EmbedBuilder()
                {
                    Color = color,
                    ImageUrl = $"https://img.youtube.com/vi/{track.Id}/0.jpg"
                //Title = "Трек добавлен в очередь",
            };
                var author = new EmbedAuthorBuilder()
                {
                    IconUrl = "https://gif-avatars.com/img/90x90/fox.gif",
                    Name = "Трек добавлен в очередь"
                };
                embed.Author = author;

                embed.AddField($"{track.Author}", $"[{track.Title}]({track.Url})");
                embed.AddField("Длительность", $"{new DateTime(track.Duration.Ticks).ToString(format)}", true);


                await ReplyAsync(null, false, embed.Build());
            }
            else
            {
                await player.PlayAsync(track);
            }
        }


        public async Task TryConnectToVoiceAsync()
        {
            if (await CheckUserInChannel())
            {
                try
                {
                    if (Context.User is not IVoiceState voiceState) return;

                    _lavaNode.TryGetPlayer(Context.Guild, out var player);

                    if (player != null && player.VoiceChannel.Id == voiceState?.VoiceChannel.Id)
                        return;


                    await _lavaNode.JoinAsync(voiceState?.VoiceChannel, Context.Channel as ITextChannel);
                }
                catch (Exception exception)
                {
                    await SimpleAnswer(exception.Message, Color.Red);
                    return;
                }
            }
            else
            {
                return;
            }
        }


        public async Task<bool> CheckUserInChannel()
        {
            var voiceState = Context.User as IVoiceState;
            _lavaNode.TryGetPlayer(Context.Guild, out var player);
            if (voiceState?.VoiceChannel == null)
            {
                await SimpleAnswer("Необходимо находиться в голосовом канале!", Color.Red);
                return false;
            }

            return true;
        }


        public async Task<bool> CheckBotInAnotherChannel()
        {
            var voiceState = Context.User as IVoiceState;
            if (_lavaNode.TryGetPlayer(Context.Guild, out var player))
            {
                if (voiceState?.VoiceChannel.Id != player.VoiceChannel.Id)
                {
                    await SimpleAnswer("Необходимо находиться в одном голосовом канале с ботом!", Color.Red);
                    return false;
                }
            }

            return true;
        }

        public async Task SimpleAnswer(string from = "", Color? color = null, string text = "")
        {
            if (color == null)
                color = new Color(64, 224, 208);
            EmbedBuilder embed = new EmbedBuilder()
            {
                Color = color,
                Title = text,
            };
            var author = new EmbedAuthorBuilder()
            {
                IconUrl = "https://gif-avatars.com/img/90x90/fox.gif",
                Name = from
            };

            embed.Author = author;

            await ReplyAsync(null, false, embed.Build());
        }
    }
}
