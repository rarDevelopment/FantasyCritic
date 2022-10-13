using System.Globalization;
using Discord;
using Discord.Interactions;
using FantasyCritic.Discord.Interfaces;
using FantasyCritic.Discord.Models;
using FantasyCritic.Discord.UrlBuilders;
using FantasyCritic.Lib.Extensions;
using FantasyCritic.Lib.Interfaces;
using NodaTime;
using FantasyCritic.Lib.Domain;

namespace FantasyCritic.Discord.Commands;
public class GetPublisherCommand : InteractionModuleBase<SocketInteractionContext>
{
    private readonly IDiscordRepo _discordRepo;
    private readonly IClock _clock;
    private readonly IDiscordParameterParser _parameterParser;
    private readonly IDiscordFormatter _discordFormatter;
    private readonly string _baseAddress;

    public GetPublisherCommand(IDiscordRepo discordRepo,
        IClock clock,
        IDiscordParameterParser parameterParser,
        IDiscordFormatter discordFormatter,
        FantasyCriticSettings fantasyCriticSettings)
    {
        _discordRepo = discordRepo;
        _clock = clock;
        _parameterParser = parameterParser;
        _discordFormatter = discordFormatter;
        _baseAddress = fantasyCriticSettings.BaseAddress;
    }

    [SlashCommand("publisher", "Get publisher information. You can search with just a portion of the name.")]
    public async Task GetPublisher(
        [Summary("publisher_or_player_name", "The name of the publisher or the name of the player. You can input only a portion of the name.")] string publisherOrPlayerName,
        [Summary("year", "The year for the league (if not entered, defaults to the current year).")] int? year = null
        )
    {
        var dateToCheck = _parameterParser.GetDateFromProvidedYear(year) ?? _clock.GetToday();

        var leagueChannel = await _discordRepo.GetLeagueChannel(Context.Channel.Id.ToString(), dateToCheck.Year);
        if (leagueChannel == null)
        {
            await RespondAsync(embed: _discordFormatter.BuildErrorEmbed(
                "Error Getting Publisher",
                "No league configuration found for this channel.",
                Context.User));
            return;
        }

        var termToSearch = publisherOrPlayerName.ToLower().Trim();

        if (termToSearch.Length < 2)
        {
            await RespondAsync(embed: _discordFormatter.BuildErrorEmbed(
                "Error Getting Publisher",
                "Please provide at least 3 characters to search with.",
                Context.User));
            return;
        }

        var foundByPlayerName =
            leagueChannel.LeagueYear.Publishers.Where(p => p.User.UserName.ToLower().Contains(termToSearch)).ToList();

        var foundByPublisherName =
            leagueChannel.LeagueYear.Publishers.Where(p => p.PublisherName.ToLower().Contains(termToSearch)).ToList();

        if (!foundByPlayerName.Any() && !foundByPublisherName.Any())
        {
            await RespondAsync(embed: _discordFormatter.BuildRegularEmbed(
                "No Matches Found",
                "No matches were found for your query.",
                Context.User));
            return;
        }

        if (foundByPlayerName.Count > 1 || foundByPublisherName.Count > 1)
        {
            var message = "";

            if (foundByPlayerName.Any())
            {
                message +=
                    $"Match by player name: {string.Join(", ", foundByPlayerName.Select(p => p.User.UserName))}";
            }
            if (foundByPublisherName.Any())
            {
                message +=
                    $"Match by publisher name: {string.Join(", ", foundByPublisherName.Select(p => p.PublisherName))}";
            }

            await RespondAsync(embed: _discordFormatter.BuildRegularEmbed(
                "Multiple Matches Found",
                message,
                Context.User));
            return;
        }
        else if (foundByPlayerName.Any() && foundByPublisherName.Any())
        {
            var inBothLists = foundByPlayerName
                .Select(publisher => foundByPublisherName.FirstOrDefault(p => p.PublisherID == publisher.PublisherID))
                .Where(inOtherList => inOtherList != null)
                .ToList();

            if (inBothLists.Count != foundByPlayerName.Count)
            {
                var message =
                    $"Match by player name: {string.Join(", ", foundByPlayerName.Select(p => p.User.UserName))}\n";
                message += $"Match by publisher name: {string.Join(", ", foundByPublisherName.Select(p => p.PublisherName))}\n";
                await RespondAsync(embed: _discordFormatter.BuildRegularEmbed(
                    "Multiple Matches Found",
                    message,
                    Context.User));
                return;
            }
        }

        //after all that, we should have just one publisher found

        Publisher? publisherFound = null;

        if (foundByPlayerName.Count == 1)
        {
            publisherFound = foundByPlayerName[0];
        }
        else if (foundByPublisherName.Count == 1)
        {
            publisherFound = foundByPublisherName[0];
        }

        if (publisherFound == null)
        {
            await RespondAsync(embed: _discordFormatter.BuildErrorEmbed(
                "Error Getting Publisher",
                "Something went wrong.",
                Context.User));
            return;
        }

        var pickedGames = publisherFound.PublisherGames.Where(g => !g.CounterPick).OrderBy(g => g.SlotNumber);
        var counterPickedGames = publisherFound.PublisherGames.Where(g => g.CounterPick).OrderBy(g => g.SlotNumber);

        var gamesMessage = string.Join("\n", pickedGames.Select(MakeGameMessage));
        var counterPickMessage = string.Join("\n", counterPickedGames.Select(MakeGameMessage));

        var leagueOptionWillReleaseDroppableGames = leagueChannel.LeagueYear.Options.WillReleaseDroppableGames;
        var leagueOptionWillNotReleaseDroppableGames = leagueChannel.LeagueYear.Options.WillNotReleaseDroppableGames;
        var leagueOptionsFreeDroppableGames = leagueChannel.LeagueYear.Options.FreeDroppableGames;

        var remainingWillReleaseDrops = leagueOptionWillReleaseDroppableGames == -1
            ? "♾️"
            : (leagueOptionWillReleaseDroppableGames - publisherFound.WillReleaseGamesDropped).ToString();

        var remainingWillNotReleaseDrops = leagueOptionWillNotReleaseDroppableGames == -1
            ? "♾️"
            : (leagueOptionWillNotReleaseDroppableGames - publisherFound.WillNotReleaseGamesDropped).ToString();

        var remainingFreeDroppableGames = leagueOptionsFreeDroppableGames == -1
            ? "️♾️"
            : (leagueOptionsFreeDroppableGames - publisherFound.FreeGamesDropped).ToString();

        var publisherUrlBuilder = new PublisherUrlBuilder(_baseAddress, publisherFound.PublisherID);

        var embedFieldBuilders = new List<EmbedFieldBuilder>
        {
            new()
            {
                Name = "Picks",
                Value = gamesMessage,
                IsInline = false
            },
            new()
            {
                Name = "Counter Picks",
                Value =  counterPickMessage,
                IsInline = false
            },
            new()
            {
                Name = "Current Score",
                Value = Math.Round(publisherFound.GetTotalFantasyPoints(leagueChannel.LeagueYear.SupportedYear, leagueChannel.LeagueYear.Options), 1),
                IsInline = false
            },
            new()
            {
                Name = "Remaining Budget",
                Value = publisherFound.Budget,
                IsInline = false
            },
            new()
            {
                Name = "'Will Release' Drops Remaining",
                Value = MakeDropDisplay(remainingWillReleaseDrops, leagueOptionWillReleaseDroppableGames)
            },
            new()
            {
                Name = "'Will Not Release' Drops Remaining",
                Value = MakeDropDisplay(remainingWillNotReleaseDrops, leagueOptionWillNotReleaseDroppableGames)
            },
            new()
            {
                Name = "'Unrestricted' Drops Remaining",
                Value = MakeDropDisplay(remainingFreeDroppableGames, leagueOptionsFreeDroppableGames)
            }
        };

        await RespondAsync(embed: _discordFormatter.BuildRegularEmbed(
            $"{publisherFound.PublisherName} (Player: {publisherFound.User.UserName})",
            publisherUrlBuilder.BuildUrl("View Publisher"),
            Context.User,
            embedFieldBuilders));
    }

    private string MakeDropDisplay(string remaining, int total)
    {
        if (total == 0)
        {
            return "N/A";
        }
        if (total == -1)
        {
            return "♾️";
        }
        return $"{remaining}/{total}";
    }
    private string MakeGameMessage(PublisherGame g)
    {
        var gameMessage = g.GameName;
        if (g.FantasyPoints != null)
        {
            // TODO: get manual critic score here if needed
            var criticScore = g.MasterGame?.MasterGame.CriticScore != null
                ? Math.Round(g.MasterGame.MasterGame.CriticScore.Value, 1).ToString(CultureInfo.InvariantCulture)
                : "N/A";

            gameMessage += $" - Score: {criticScore} - Points: {Math.Round(g.FantasyPoints.Value, 1)}";
        }
        else
        {
            if (g.MasterGame != null)
            {
                if (g.MasterGame.MasterGame.ReleaseDate != null)
                {
                    gameMessage += $" - {g.MasterGame.MasterGame.ReleaseDate?.ToString()[10..]}"; // TODO: why was I using substring here?
                }
                else
                {
                    gameMessage += $" - {g.MasterGame.MasterGame.EstimatedReleaseDate} (est)";
                }
            }
        }

        return gameMessage;
    }
}
