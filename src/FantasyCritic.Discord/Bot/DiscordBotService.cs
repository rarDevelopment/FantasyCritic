using Discord.WebSocket;
using Discord;
using FantasyCritic.Discord.Commands;
using NodaTime;
using Discord.Net;
using FantasyCritic.Discord.Interfaces;
using FantasyCritic.Discord.Utilities;
using Newtonsoft.Json;
using FantasyCritic.Lib.Interfaces;
using FantasyCritic.Lib.Services;

namespace FantasyCritic.Discord.Bot;
public class DiscordBotService
{
    private readonly string _botToken;
    private readonly DiscordFormatter _discordFormatter;
    private readonly DiscordSocketClient _client;
    private readonly List<ICommand> _commandsList;

    public DiscordBotService(string botToken,
        IFantasyCriticRepo mySQLFantasyCriticRepo,
        IDiscordRepo mySQLDiscordRepo,
        SystemClock systemClock,
        GameSearchingService gameSearchingService,
        IDiscordParameterParser parameterParser,
        DiscordFormatter discordFormatter,
        string baseAddress)
    {
        _botToken = botToken;
        _discordFormatter = discordFormatter;
        _commandsList = new List<ICommand>
        {
            new GetLeagueCommand(mySQLDiscordRepo, mySQLFantasyCriticRepo, systemClock, parameterParser, discordFormatter, baseAddress),
            new GetLeagueLinkCommand(mySQLDiscordRepo, systemClock, parameterParser, discordFormatter, baseAddress),
            new GetPublisherCommand(mySQLDiscordRepo, systemClock, parameterParser, discordFormatter, baseAddress),
            new GetGameCommand(mySQLDiscordRepo, systemClock, parameterParser, gameSearchingService, discordFormatter, baseAddress),
            new GetUpcomingGamesCommand(mySQLDiscordRepo, systemClock, discordFormatter, baseAddress),
            new GetRecentGamesCommand(mySQLDiscordRepo, systemClock, discordFormatter, baseAddress),
            new SetLeagueChannelCommand(mySQLDiscordRepo, systemClock, discordFormatter, mySQLFantasyCriticRepo),
            new RemoveLeagueCommand(mySQLDiscordRepo, systemClock, discordFormatter)
        };

        _client = new DiscordSocketClient();
        _client.Log += Log;
        _client.Ready += Client_Ready;
        _client.SlashCommandExecuted += SlashCommandHandler;
    }

    public async Task InitializeBot()
    {
        await _client.LoginAsync(TokenType.Bot, _botToken);
        await _client.StartAsync();
    }

    private static Task Log(LogMessage msg)
    {
        Serilog.Log.Information(msg.ToString());
        return Task.CompletedTask;
    }

    public async Task Client_Ready()
    {
        foreach (var command in _commandsList)
        {
            var globalCommand = new SlashCommandBuilder()
                .WithName(command.Name)
                .WithDescription(command.Description);

            if (command.Options.Any())
            {
                foreach (var option in command.Options)
                {
                    globalCommand.AddOption(option);
                }
            }

            try
            {
                await _client.CreateGlobalApplicationCommandAsync(globalCommand.Build());
            }
            catch (HttpException exception)
            {
                var json = JsonConvert.SerializeObject(exception.Errors, Formatting.Indented);
                Serilog.Log.Error(json);
            }
        }
    }
    private async Task SlashCommandHandler(SocketSlashCommand command)
    {
        var commandToHandle = _commandsList.FirstOrDefault(c => c.Name == command.Data.Name);
        if (commandToHandle != null)
        {
            try
            {
                await commandToHandle.HandleCommand(command);
            }
            catch (Exception ex)
            {
                Serilog.Log.Fatal(ex, ex.Message);
                await command.RespondAsync(embed: _discordFormatter.BuildErrorEmbed(
                    "Error Executing Command",
                    "There was an error executing this command. Please try again.",
                    command.User));
            }
        }
    }
}
