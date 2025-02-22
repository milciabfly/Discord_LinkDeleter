using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using System.Timers;
using DotNetEnv;
using IniParser;
using Newtonsoft.Json;

namespace InvitationLinkDeleter
{
    class ILD
    {
        //トークン
        private string botToken;

        //ギルド
        private ulong guildId;

        //許可ロール
        private ulong roleId;

        //メッセージ
        private string botStatus;
        private string deleteMessage;
        private string customDeleteMessage;

        //タイムアウト
        private ulong maxTimeoutAttempts;
        private ulong timeoutDuration;
        
        //Discordクライアント
        private DiscordSocketClient client;
        private ServiceProvider service;

        //User警告
        private Dictionary<ulong, ulong> usersWarning = new Dictionary<ulong, ulong>();
        //User警告リセット時間
        private DateTime resetWarning = DateTime.Today.AddHours(00).AddMinutes(00);
        private static System.Timers.Timer Timer = new System.Timers.Timer();

        //Discord URL 以外
        private List<string> customUrls = new List<string>();

        //作業ディレクトリ
        //private string directory = AppDomain.CurrentDomain.BaseDirectory;

        //URL保存用ファイル
        private string urlSaveFile = AppDomain.CurrentDomain.BaseDirectory + Path.Combine("Data", "URLs.json");

        private void ReadFile()
        {
            //.envファイル読み込み
            Env.Load($"{AppDomain.CurrentDomain.BaseDirectory}.env");
            botToken = string.IsNullOrEmpty(Env.GetString("DISCORD_TOKEN")) ? "null" : Env.GetString("DISCORD_TOKEN"); // null or 空白を判断
            if (botToken == null) throw new InvalidOperationException("トークンが設定されていません。");

            //.iniファイル読み込み
            var parser = new FileIniDataParser();
            var data = parser.ReadFile($"{AppDomain.CurrentDomain.BaseDirectory}config.ini");
            //ID
            Console.WriteLine(data["IDS"]["GUILDID"]);
            if (!ulong.TryParse(data["IDS"]["GUILDID"] ?? "0", out guildId) || guildId == 0) throw new InvalidOperationException("GUILDIDが設定されていません。");
            roleId = ulong.TryParse(data["IDS"]["ROLEID"] ?? "0", out ulong _r) ? _r : 0;
            //メッセージ
            botStatus = string.IsNullOrEmpty(data["CUSTOM_MESSAGE"]["STATUS"]) ? "招待リンクの送信は禁止されています。" : data["CUSTOM_MESSAGE"]["STATUS"].Replace("\\n", "\n"); ;
            deleteMessage = string.IsNullOrEmpty(data["CUSTOM_MESSAGE"]["DELETE_MESSAGE"]) ? "> Discord招待リンクを検知したため自動で削除しました。" : data["CUSTOM_MESSAGE"]["DELETE_MESSAGE"].Replace("\\n", "\n"); ;
            customDeleteMessage = string.IsNullOrEmpty(data["CUSTOM_MESSAGE"]["CUSTOM_DELETE_MESSAGE"]) ? "> 禁止されているリンクを検知したため自動で削除しました。" : data["CUSTOM_MESSAGE"]["CUSTOM_DELETE_MESSAGE"].Replace("\\n", "\n"); ;
            //タイムアウト
            maxTimeoutAttempts = ulong.TryParse(data["TIMEOUT"]["MAX_TIMEOUT_ATTEMPTS"] ?? "3", out ulong _m) ? _m : 3;
            timeoutDuration = ulong.TryParse(data["TIMEOUT"]["MAX_TIMEOUT_ATTEMPTS"] ?? "2", out ulong _t) ? _t : 2;

            //.jsonファイル読み込み
            //ディレクトリ + ファイルチェック
            if (Directory.Exists(Path.GetDirectoryName(urlSaveFile)))
                if(File.Exists(urlSaveFile))
                {
                    using (var sr = new StreamReader(urlSaveFile, System.Text.Encoding.UTF8))
                    {
                        customUrls = JsonConvert.DeserializeObject<List<string>>(sr.ReadToEnd()) ?? null;
                        sr.Close();
                    }
                }

            Console.WriteLine("正常にファイルを読み込みました。");
        }

        private async Task AddSlashComand()
        {
            var addBannedUrl = new SlashCommandBuilder().WithName("add-banned-url").WithDescription("禁止するURLを追加する - 管理者のみ")
            .AddOption(name: "url", type: ApplicationCommandOptionType.String, description: "追加するURL", isRequired: true)
            .Build();
            await client.CreateGlobalApplicationCommandAsync(addBannedUrl);
            Console.WriteLine("グローバルスラッシュコマンド [add-banned-url] を登録しました。");

            var removeBannedUrl = new SlashCommandBuilder().WithName("remove-banned-url").WithDescription("禁止するURLを削除する - 管理者のみ")
            .AddOption(name: "url", type: ApplicationCommandOptionType.String, description: "削除するURL", isRequired: true)
            .Build();
            await client.CreateGlobalApplicationCommandAsync(removeBannedUrl);
            Console.WriteLine("グローバルスラッシュコマンド [remove-banned-url] を登録しました。");

            var checkRegisteredUrlList = new SlashCommandBuilder().WithName("check-registered-url-list").WithDescription("登録済みのURLを確認する").Build();
            await client.CreateGlobalApplicationCommandAsync(checkRegisteredUrlList);
            Console.WriteLine("グローバルスラッシュコマンド [check-registered-url-list] を登録しました。");
        }

        public async Task MainProg()
        {
            ReadFile(); //ファイルを読み込む

            var config = new DiscordSocketConfig
            {
                LogLevel = LogSeverity.Info,
                GatewayIntents = GatewayIntents.All
            };

            client = new DiscordSocketClient(config);
            client.Log += Log;
            client.Ready += onReady;
            client.MessageReceived += GetMessage;
            client.SlashCommandExecuted += SlashCommand;

            await client.LoginAsync(TokenType.Bot, botToken);
            await client.StartAsync();

            //1分毎に時間をチェック
            Timer.Interval = TimeSpan.FromMinutes(1).TotalMilliseconds;
            Timer.Elapsed += ResetTimer;
            Timer.Start();

            await Task.Delay(-1);
        }

        private async void ResetTimer(object? sender, ElapsedEventArgs e)
        {
            //今の時間
            var nowTime = DateTime.Now;

            //00:00の時に実行 これぐらいしか思いつかなかった;
            if(resetWarning.Hour == nowTime.Hour && resetWarning.Minute == nowTime.Minute)
            {
                usersWarning.Clear();
                return;
            }
        }


        public async Task onReady()
        {
            AddSlashComand(); //スラッシュコマンドの登録
            Console.WriteLine("ボットが起動しました。");
        }

        private async Task Log(LogMessage message)
        {
            Console.WriteLine(message.ToString());
            await client.SetCustomStatusAsync($"{botStatus}");
        }

        public async Task SlashCommand(SocketSlashCommand command)
        {
            if(command.Data.Name == "add-banned-url")
            {
                if (!client.GetGuild(guildId).GetUser(command.User.Id).GuildPermissions.Administrator)
                {
                    await command.RespondAsync(text: "このコマンドはサーバー管理者のみ使用できます。", ephemeral: true);
                    return;
                }

                string _url = (string)command.Data.Options.FirstOrDefault(opt => opt.Name == "url").Value;
                customUrls.Add(_url);

                await command.RespondAsync(text: $"{_url}を追加しました。", ephemeral: true);

                //ディレクトリの新規作成
                if (!Directory.Exists(Path.GetDirectoryName(urlSaveFile)))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(urlSaveFile));
                    Console.WriteLine("ディレクトリが存在しなかったため、新規作成しました。");
                }

                string json = JsonConvert.SerializeObject(customUrls);
                using (var sw = new StreamWriter(urlSaveFile, false, System.Text.Encoding.UTF8))
                {
                    sw.WriteLine(json);
                    sw.Close();
                }
                await command.FollowupAsync(text: "保存完了", ephemeral: true);
                return;
            }

            if(command.Data.Name == "remove-banned-url")
            {
                if (!client.GetGuild(guildId).GetUser(command.User.Id).GuildPermissions.Administrator)
                {
                    await command.RespondAsync(text: "このコマンドはサーバー管理者のみ使用できます。", ephemeral: true);
                    return;
                }

                if (!Directory.Exists(Path.GetDirectoryName(urlSaveFile)))
                {
                    await command.RespondAsync(text: $"ディレクトリが存在しません。", ephemeral: true);
                    return;
                }
                else if (!File.Exists(urlSaveFile))
                {
                    await command.RespondAsync(text: $"ファイルが存在しません。", ephemeral: true);
                    return;
                }

                string _url = (string)command.Data.Options.FirstOrDefault(opt => opt.Name == "url").Value;

                if(customUrls.RemoveAll(url => url == _url) != 0)
                {
                    await command.RespondAsync(text: $"{_url}をURLを削除しました。", ephemeral: true);
                }
                else
                {
                    await command.RespondAsync(text: $"{_url}は存在しません。URLを確認してから再度お試しください。\n※Discordの仕様により確認コマンドの際、末尾に\"/\"が入っている可能性があります。\n修正例:`{_url.TrimEnd('/')}/` ⇒ `{_url.TrimEnd('/')}`", ephemeral: true);
                    return;
                }

                string json = JsonConvert.SerializeObject(customUrls);
                using (var sw = new StreamWriter(urlSaveFile, false, System.Text.Encoding.UTF8))
                {
                    sw.WriteLine(json);
                    sw.Close();
                }
                await command.FollowupAsync(text: "保存完了", ephemeral: true);
                return;
            }

            if(command.Data.Name == "check-registered-url-list")
            {
                string _ul = "## [禁止されているURL一覧]\n";
                //Listが空の場合は登録なしを出力、それ以外はURL一覧を出力
                if (customUrls == null || !customUrls.Any()) _ul = "- URLは登録されていません。";
                else foreach(var url in customUrls) _ul += $"- {url}\n";
                await command.RespondAsync(_ul, ephemeral: true);
                return;
            }

        }

        public async Task GetMessage(SocketMessage message)
        {
            if (message.Author.IsBot)
            {
                //Botの場合は無視
                return;
            }

            //小文字変換
            string messageToLower = message.Content.ToLower();

            //Dircordの招待リンク
            if (messageToLower.Contains("discord.gg/"))
            {
                var user = client.GetGuild(guildId).GetUser(message.Author.Id);
                if (user.GuildPermissions.Administrator)
                {
                    //管理者権限所持者は無視
                    return;
                }

                // URL許可ロールのチェック
                bool permRole = roleId == 0 ? false : user.Roles.Any(role => role.Id == roleId);
                if (!permRole)
                {
                    //URL送信権限が無い人は削除
                    await message.DeleteAsync();
                    if (!usersWarning.ContainsKey(message.Author.Id))
                    {
                        usersWarning.Add(message.Author.Id, 1);
                        await message.Channel.SendMessageAsync($"{message.Author.Mention}\n{deleteMessage}\n> \n> ⏰タイムアウト カウント[1/{maxTimeoutAttempts}]");
                        return;
                    }
                    else
                    {
                        usersWarning[message.Author.Id]++;
                        if (usersWarning[message.Author.Id] == maxTimeoutAttempts)
                        {
                            await message.Channel.SendMessageAsync($"{message.Author.Mention}\n{deleteMessage}\n> \n> ⏰タイムアウトカウントが[3]に到達したため、ユーザをタイムアウトします。");
                            await user.SetTimeOutAsync(TimeSpan.FromHours(timeoutDuration));
                            usersWarning[message.Author.Id] = 0;
                            return;
                        }
                        await message.Channel.SendMessageAsync($"{message.Author.Mention}\n{deleteMessage}\n> \n> ⏰タイムアウト カウント[{usersWarning[message.Author.Id]}/{maxTimeoutAttempts}]");
                        return;
                    }
                }
            }

            //Discordの招待リンク以外
            //customUrlsが空でない時
            if(customUrls != null && customUrls.Any())
            {
                foreach(var url in customUrls)
                {
                    if (messageToLower.Contains(url.ToLower()))
                    {
                        var user = client.GetGuild(guildId).GetUser(message.Author.Id);
                        if (user.GuildPermissions.Administrator)
                        {
                            //管理者権限所持者は無視
                            return;
                        }

                        // URL許可ロールのチェック
                        bool permRole = roleId == 0 ? false : user.Roles.Any(role => role.Id == roleId);
                        if (!permRole)
                        {
                            //URL送信権限が無い人は削除
                            await message.DeleteAsync();
                            if (!usersWarning.ContainsKey(message.Author.Id))
                            {
                                usersWarning.Add(message.Author.Id, 1);
                                await message.Channel.SendMessageAsync($"{message.Author.Mention}\n{customDeleteMessage}\n> \n> ⏰タイムアウト カウント[1/{maxTimeoutAttempts}]");
                                return;
                            }
                            else
                            {
                                usersWarning[message.Author.Id]++;
                                if (usersWarning[message.Author.Id] == maxTimeoutAttempts)
                                {
                                    await message.Channel.SendMessageAsync($"{message.Author.Mention}\n{customDeleteMessage}\n> \n> ⏰タイムアウトカウントが[3]に到達したため、ユーザをタイムアウトします。");
                                    await user.SetTimeOutAsync(TimeSpan.FromHours(timeoutDuration));
                                    usersWarning[message.Author.Id] = 0;
                                    return;
                                }
                                await message.Channel.SendMessageAsync($"{message.Author.Mention}\n{customDeleteMessage}\n> \n> ⏰タイムアウト カウント[{usersWarning[message.Author.Id]}/{maxTimeoutAttempts}]");
                                return;
                            }
                        }
                    }
                }
            }
        }


        //Main
        static void Main(string[] args) => new ILD().MainProg().GetAwaiter().GetResult();

    }
}