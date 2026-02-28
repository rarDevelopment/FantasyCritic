using Discord;
using Discord.Interactions;
using DiscordDotNetUtilities.Interfaces;
using FantasyCritic.Lib.BusinessLogicFunctions;
using FantasyCritic.Lib.Extensions;
using FantasyCritic.Lib.Interfaces;
using FantasyCritic.Lib.Discord.Models;
using FantasyCritic.Lib.Domain.Combinations;
using FantasyCritic.Lib.Discord.Utilities;
using FantasyCritic.Lib.Services;
using JetBrains.Annotations;

namespace FantasyCritic.Lib.Discord.Commands;
public class GameNewsCommand : InteractionModuleBase<SocketInteractionContext>
{
    private readonly IDiscordRepo _discordRepo;
    private readonly InterLeagueService _interLeagueService;
    private readonly PublisherService _publisherService;
    private readonly IReadOnlyFantasyCriticUserStore _fantasyCriticUserStore;
    private readonly IClock _clock;
    private readonly IDiscordFormatter _discordFormatter;
    private readonly string _baseAddress;
    private const string UpcomingValue = "Upcoming";
    private const string RecentValue = "Recent";

    public GameNewsCommand(IDiscordRepo discordRepo,
        InterLeagueService interLeagueService,
        PublisherService publisherService,
        IReadOnlyFantasyCriticUserStore fantasyCriticUserStore,
        IClock clock,
        IDiscordFormatter discordFormatter,
        FantasyCriticSettings fantasyCriticSettings)
    {
        _discordRepo = discordRepo;
        _interLeagueService = interLeagueService;
        _publisherService = publisherService;
        _fantasyCriticUserStore = fantasyCriticUserStore;
        _clock = clock;
        _discordFormatter = discordFormatter;
        _baseAddress = fantasyCriticSettings.BaseAddress;
    }

    [UsedImplicitly]
    [SlashCommand("game-news",
        "Get upcoming or recent releases for publishers in the league (or for yourself in a DM).")]
    public async Task GetGameNews(
        [Summary("upcoming_or_recent", "Whether you want upcoming or recent releases")]
        [Choice("Upcoming Releases", UpcomingValue)]
        [Choice("Recent Releases", RecentValue)]
        string upcomingOrRecent = UpcomingValue)
    {
        await DeferAsync();
        var dateToCheck = _clock.GetGameEffectiveDate();
        var supportedYears = await _interLeagueService.GetSupportedYears();
        var isRecentReleases = upcomingOrRecent == RecentValue;

        if (Context.Channel is IDMChannel)
        {
            var user = await _fantasyCriticUserStore.GetFantasyCriticUserForDiscordUser(Context.User.Id);
            if (user == null)
            {
                await FollowupAsync(embed: _discordFormatter.BuildErrorEmbedWithUserFooter(
                    "No User Found",
                    "You must link your Discord account to your Fantasy Critic account on the Fantasy Critic website to use this bot via DM.",
                    Context.User));
                return;
            }

            var myGameNews = await _publisherService.GetMyGameNews(user);
            var myGameNewsSet = MyGameNewsSet.BuildMyGameNews(myGameNews, dateToCheck, 10);
            var gameNewsToUse = isRecentReleases ? myGameNewsSet.RecentGames : myGameNewsSet.UpcomingGames;

            if (gameNewsToUse.Count == 0)
            {
                await FollowupAsync(embed: _discordFormatter.BuildErrorEmbedWithUserFooter(
                    "No Releases Found",
                    "No data found.",
                    Context.User));
                return;
            }

            var formattedMessage = DiscordSharedMessageUtilities.BuildDateGroupedGameMessagesForUser(gameNewsToUse, _baseAddress);

            await FollowupAsync(embed: _discordFormatter.BuildRegularEmbedWithUserFooter(
                $"Your {upcomingOrRecent} Releases ({user.UserName})",
                formattedMessage,
                Context.User));
        }
        else
        {
            var leagueChannel = await _discordRepo.GetLeagueChannel(Context.Guild.Id, Context.Channel.Id, supportedYears);
            if (leagueChannel == null)
            {
                await FollowupAsync(embed: _discordFormatter.BuildErrorEmbedWithUserFooter(
                    "Error Getting Game News",
                    "No league configuration found for this channel.",
                    Context.User));
                return;
            }

            var leagueYear = leagueChannel.LeagueYear;
            var leagueYearPublisherPairs =
                leagueYear.Publishers.Select(publisher => new LeagueYearPublisherPair(leagueYear, publisher));

            if (leagueYear.Publishers.Count == 0 || leagueYear.Publishers.Sum(p => p.PublisherGames.Count) == 0)
            {
                await FollowupAsync(embed: _discordFormatter.BuildErrorEmbedWithUserFooter(
                    "Error Getting Game News",
                    "No publisher games found, have you done your draft yet?",
                    Context.User));
                return;
            }

            var gameNewsData = GameNewsFunctions.GetGameNews(leagueYearPublisherPairs, dateToCheck, isRecentReleases, 10);
            if (gameNewsData.Count == 0)
            {
                await FollowupAsync(embed: _discordFormatter.BuildErrorEmbedWithUserFooter(
                    "Error Getting Game News",
                    "No data found.",
                    Context.User));
                return;
            }

            var matchedGames = DiscordSharedMessageUtilities.ConvertToMatchedGameDisplays(gameNewsData, leagueYear);
            if (matchedGames.Count == 0)
            {
                await FollowupAsync(embed: _discordFormatter.BuildErrorEmbedWithUserFooter(
                    "Error Getting Game News",
                    "No data found.",
                    Context.User));
                return;
            }

            var formattedMessage = DiscordSharedMessageUtilities.BuildDateGroupedGameMessages(matchedGames, _baseAddress);

            if (formattedMessage.Length > 4096)
            {
                formattedMessage = "There are too many games to list in a Discord Message.";
            }

            await FollowupAsync(embed: _discordFormatter.BuildRegularEmbedWithUserFooter(
                $"{upcomingOrRecent} Publisher Releases",
                formattedMessage,
                Context.User));
        }
    }
}
