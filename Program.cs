using Discord;
using Discord.Net;
using Discord.WebSocket;
using Newtonsoft.Json;
using System.Net;


namespace pmt_discord
{
    internal class Program
    {
        private static string _configPath = "config.json";

        private static DiscordSocketClient _client;
        private static ConfigFile _config;
        private static UptimeService _uptimeService;

        public static async Task Main()
        {
            // Create config file if not exists and exit letting the user know to configure it
            if (!File.Exists(_configPath))
            {
                File.WriteAllText(_configPath, JsonConvert.SerializeObject(new ConfigFile()));
                Console.WriteLine("Config file not found. A new one has been created, please update Token/Guild within it. The program will now exit.");
                Console.ReadKey();
                return;
            }

            // Load config from file
            _config = JsonConvert.DeserializeObject<ConfigFile>(File.ReadAllText(_configPath));

            _client = new DiscordSocketClient();  // init discord client

            // Init event handlers
            _client.Log += Log;
            _client.Ready += Client_Ready;
            _client.SlashCommandExecuted += SlashCommandHandler;            

            // Login to discord and start the bot
            await _client.LoginAsync(TokenType.Bot, _config.Token);
            await _client.StartAsync();

            // Block this task until the program is closed
            await Task.Delay(-1);
        }

        private static async Task Client_Ready()
        {
            var guild = _client.GetGuild(_config.Guild);

            // Create ip-Command
            var guildCommand = new Discord.SlashCommandBuilder()
                .WithName("ip")
                .WithDescription("Update ip to check uptime for")
                .AddOption("address", ApplicationCommandOptionType.String, "The address to ping", isRequired: true);

            try
            {
                // This should only be called once per guild, but it doesn't matter much for now
                await guild.CreateApplicationCommandAsync(guildCommand.Build());
                Console.WriteLine("Command ip registered.");
            }
            catch (ApplicationCommandException exception)
            {
                var json = JsonConvert.SerializeObject(exception.Errors, Formatting.Indented);
                Console.WriteLine(json);
            }

            // Create new uptime service, register eventhandlers and run its task
            _uptimeService = new UptimeService(_config.Ip);
            _uptimeService.ClientDown += HandleClientDown;
            _uptimeService.ClientUp += HandleClientUp;

            await _uptimeService.Run();
        }

        private static async Task SlashCommandHandler(SocketSlashCommand command)
        {
            switch (command.Data.Name)
            {
                case "ip":
                    await HandleIpCommand(command);
                    break;
            }
        }

        private static async Task HandleIpCommand(SocketSlashCommand command)
        {
            var ip = (string)command.Data.Options.First().Value;

            // Check if given ip is valid and write new config to json file
            if (IsValidIpAddress(ip))
            {
                // Update config, uptimeService and write to disk
                _config.Ip = ip;
                _uptimeService.ip = ip;
                File.WriteAllText(_configPath, JsonConvert.SerializeObject(_config));

                await command.RespondAsync($"Ip address updated to {ip}", ephemeral: true);
            }
            else
            {
                await command.RespondAsync($"Ip address {ip} not valid!", ephemeral: true);
            }
        }

        private static async void HandleClientUp(object sender, ClientUpDownEventArgs e)
        {
            await _client.SetStatusAsync(UserStatus.Online);
            await _client.SetGameAsync("Server Up");
            Console.WriteLine($"Server is up: {e.Ip} ({e.Ping} ms)");
        }

        private static async void HandleClientDown(object sender, ClientUpDownEventArgs e)
        {
            // Notify admin in dms
            var admin = await _client.GetUserAsync(_config.Admin);
            await admin.SendMessageAsync($"Server is down: {e.Ip}");

            await _client.SetStatusAsync(UserStatus.DoNotDisturb);
            await _client.SetGameAsync("Server Down");
            Console.WriteLine($"Server is down: {e.Ip}");
        }

        public static bool IsValidIpAddress(string ip)
        {
            IPAddress address;
            if (IPAddress.TryParse(ip, out address))
            {
                switch (address.AddressFamily)
                {
                    case System.Net.Sockets.AddressFamily.InterNetwork:
                        return true;
                    default:
                        return false;
                }
            }

            return false;
        }

        private static Task Log(LogMessage msg)
        {
            Console.WriteLine(msg.ToString());
            return Task.CompletedTask;
        }
    }
}
