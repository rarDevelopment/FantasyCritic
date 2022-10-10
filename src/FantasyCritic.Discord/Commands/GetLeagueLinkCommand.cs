using Discord;
using Discord.WebSocket;
using FantasyCritic.Discord.Interfaces;
using FantasyCritic.Discord.Models;
using FantasyCritic.Discord.UrlBuilders;
using FantasyCritic.Lib.Extensions;
using FantasyCritic.Lib.Interfaces;
using NodaTime;

namespace FantasyCritic.Discord.Commands;
public class GetLeagueLinkCommand : ICommand
{
    public string Name => "link";
    public string Description => "Get a link to the league.";
    public SlashCommandOptionBuilder[] Options => new SlashCommandOptionBuilder[]
    {
        new()
        {
            Name = "year",
            Description = "The year for the league (if not entered, defaults to the current year).",
            Type = ApplicationCommandOptionType.Integer,
            IsRequired = false
        }
    };

    private readonly IDiscordRepo _discordRepo;
    private readonly IClock _clock;
    private readonly IDiscordParameterParser _parameterParser;
    private readonly IDiscordFormatter _discordFormatter;
    private readonly DiscordSettings _discordSettings;
    private readonly string _baseAddress;

    public GetLeagueLinkCommand(IDiscordRepo discordRepo,
        IClock clock,
        IDiscordParameterParser parameterParser,
        IDiscordFormatter discordFormatter,
        DiscordSettings discordSettings,
        string baseAddress)
    {
        _discordRepo = discordRepo;
        _clock = clock;
        _parameterParser = parameterParser;
        _discordFormatter = discordFormatter;
        _discordSettings = discordSettings;
        _baseAddress = baseAddress;
    }

    public async Task HandleCommand(SocketSlashCommand command)
    {
        try
        {
            var providedYear = command.Data.Options.FirstOrDefault(o => o.Name == "year");
            var dateToCheck = _parameterParser.GetDateFromProvidedYear(providedYear) ?? _clock.GetToday();

            var leagueChannel = await _discordRepo.GetLeagueChannel(command.Channel.Id.ToString(), dateToCheck.Year);
            if (leagueChannel == null)
            {
                await command.RespondAsync($"Error: No league configuration found for this channel in {dateToCheck.Year}.");
                return;
            }

            var leagueUrlBuilder = new LeagueUrlBuilder(_baseAddress, leagueChannel.LeagueYear.League.LeagueID, leagueChannel.LeagueYear.Year);
            var leagueUrl = leagueUrlBuilder.BuildUrl();

            var embedBuilder = new EmbedBuilder()
                .WithTitle(
                    $"Click here to visit the site for the league {leagueChannel.LeagueYear.League.LeagueName} ({leagueChannel.LeagueYear.Year})")
                .WithUrl(leagueUrl)
                .WithFooter(_discordFormatter.BuildEmbedFooter(command.User))
                .WithColor(_discordSettings.EmbedColors.Regular)
                .WithCurrentTimestamp();

            await command.RespondAsync(embed: embedBuilder.Build());
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error retrieving LeagueChannel {ex.Message}");
            await command.RespondAsync("There was an error executing this command. Please try again.");
        }
    }
}
