using Discord;
using Discord.WebSocket;
using FantasyCritic.Lib.Extensions;
using FantasyCritic.Lib.Interfaces;
using NodaTime;

namespace FantasyCritic.Discord.Commands;
public class GetLeagueLinkCommand : ICommand
{
    public string Name { get; set; }
    public string Description { get; set; }
    public SlashCommandOptionBuilder[] Options { get; set; }

    private readonly IDiscordRepo _discordRepo;
    private readonly IClock _clock;
    private readonly IParameterParser _parameterParser;

    public GetLeagueLinkCommand(IDiscordRepo discordRepo, IClock clock, IParameterParser parameterParser)
    {
        Name = "link";
        Description = "Get a link to the league.";
        Options = new SlashCommandOptionBuilder[]
        {
            new()
            {
                Name = "year",
                Description = "The year for the league (if not entered, defaults to the current year).",
                Type = ApplicationCommandOptionType.Integer,
                IsRequired = false
            }
        };
        _discordRepo = discordRepo;
        _clock = clock;
        _parameterParser = parameterParser;
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

            var leagueUrl =
                $"https://www.fantasycritic.games/league/{leagueChannel.LeagueYear.League.LeagueID}/{leagueChannel.LeagueYear.Year}";

            var embedBuilder = new EmbedBuilder()
                .WithTitle(
                    $"Click here to visit the site for the league {leagueChannel.LeagueYear.League.LeagueName} ({leagueChannel.LeagueYear.Year})")
                .WithUrl(leagueUrl)
                .WithFooter($"Requested by {command.User.Username}",
                    command.User.GetAvatarUrl() ?? command.User.GetDefaultAvatarUrl())
                .WithColor(16777215)
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
