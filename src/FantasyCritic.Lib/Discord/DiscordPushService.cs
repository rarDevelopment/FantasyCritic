using Discord;
using Discord.WebSocket;
using DiscordDotNetUtilities;
using DiscordDotNetUtilities.Interfaces;
using FantasyCritic.Lib.DependencyInjection;
using FantasyCritic.Lib.Discord.Notifications;
using FantasyCritic.Lib.Discord.UrlBuilders;
using FantasyCritic.Lib.Discord.Utilities;
using FantasyCritic.Lib.Domain.Combinations;
using FantasyCritic.Lib.Domain.LeagueActions;
using FantasyCritic.Lib.Domain.Trades;
using FantasyCritic.Lib.Extensions;
using FantasyCritic.Lib.Interfaces;
using MediatR;

namespace FantasyCritic.Lib.Discord;
public class DiscordPushService
{
    private const int MaxAttempts = 4;
    private const int MaxMessageLength = 2000;
    private readonly string _botToken;
    private readonly IDiscordRepo _discordRepo;
    private readonly IDiscordSupplementalDataRepo _supplementalDataRepo;
    private readonly IDiscordFormatter _discordFormatter;
    private readonly IMediator _mediator;
    private readonly DiscordSocketClient _client;
    private bool _botIsReady;
    private readonly bool _enabled;
    private readonly string _baseAddress;

    public DiscordPushService(
        FantasyCriticDiscordConfiguration configuration,
        IDiscordRepo discordRepo,
        IDiscordSupplementalDataRepo supplementalDataRepo,
        IDiscordFormatter discordFormatter,
        IMediator mediator)
    {
        _enabled = !string.IsNullOrEmpty(configuration.BotToken) && configuration.BotToken != "secret";
        _botToken = configuration.BotToken;
        _baseAddress = configuration.BaseAddress;
        _discordRepo = discordRepo;
        _supplementalDataRepo = supplementalDataRepo;
        _discordFormatter = discordFormatter;
        _mediator = mediator;
        DiscordSocketConfig socketConfig = new()
        {
            GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.GuildMembers,
            AlwaysDownloadUsers = true,
        };
        _client = new DiscordSocketClient(socketConfig);
        _botIsReady = false;
    }

    private async Task<bool> StartBot()
    {
        if (!_enabled)
        {
            return false;
        }

        if (_botIsReady)
        {
            return true;
        }
        _client.Ready += Client_Ready;
        _client.Log += Log;

        await _client.LoginAsync(TokenType.Bot, _botToken);
        await _client.StartAsync();

        var attempts = 0;
        while (!_botIsReady)
        {
            if (attempts > MaxAttempts)
            {
                break;
            }

            await Task.Delay(1000);
            attempts++;
        }

        if (!_botIsReady)
        {
            Serilog.Log.Warning("Discord bot is not ready, cannot send message.");
            return false;
        }

        return true;
    }

    public async Task SendNewMasterGameMessage(MasterGame masterGame, int year)
    {
        //bool shouldRun = await StartBot();
        //if (!shouldRun)
        //{
        //    return;
        //}

        await _mediator.Publish(new NewMasterGameNotification(masterGame, year));

        //var allChannels = await _discordRepo.GetAllLeagueChannels();
        //var newsEnabledChannels = allChannels.Where(x => x.GameNewsSetting != DiscordGameNewsSetting.Off).ToList();

        //var messageTasks = new List<Task>();
        //foreach (var leagueChannel in newsEnabledChannels)
        //{
        //    if (leagueChannel.GameNewsSetting == DiscordGameNewsSetting.Relevant)
        //    {
        //        bool gameIsRelevant = NewGameIsRelevant(masterGame, year);
        //        if (!gameIsRelevant)
        //        {
        //            continue;
        //        }
        //    }

        //    var guild = _client.GetGuild(leagueChannel.GuildID);
        //    var channel = guild?.GetChannel(leagueChannel.ChannelID);
        //    if (channel is not SocketTextChannel textChannel)
        //    {
        //        continue;
        //    }

        //    var tagsString = string.Join(", ", masterGame.Tags.Select(x => x.ReadableName));
        //    messageTasks.Add(textChannel.TrySendMessageAsync($"New Game Added! **{masterGame.GameName}** (Tagged as: **{tagsString}**)"));
        //}

        //await Task.WhenAll(messageTasks);
    }

    public async Task SendGameCriticScoreUpdateMessage(MasterGame game, decimal? oldCriticScore, decimal? newCriticScore, int year)
    {
        bool shouldRun = await StartBot();
        if (!shouldRun)
        {
            return;
        }

        var allChannels = await _discordRepo.GetAllLeagueChannels();
        var leaguesWithGame = await _supplementalDataRepo.GetLeaguesWithOrFormerlyWithGame(game, year);
        var newsEnabledChannels = allChannels.Where(x => x.GameNewsSetting != DiscordGameNewsSetting.Off).ToList();

        var messageToSend = "";

        var newCriticScoreRounded = newCriticScore != null ? (decimal?)Math.Round(newCriticScore.Value, 1) : null;
        var oldCriticScoreRounded = oldCriticScore != null ? (decimal?)Math.Round(oldCriticScore.Value, 1) : null;

        if (newCriticScoreRounded == null)
        {
            return;
        }
        if (oldCriticScoreRounded == null)
        {
            messageToSend = $"**{game.GameName}** now has a critic score of **{newCriticScoreRounded}**";
        }
        else
        {
            var scoreDiff = oldCriticScoreRounded.Value - newCriticScoreRounded.Value;
            if (scoreDiff != 0 && Math.Abs(scoreDiff) >= 1)
            {
                var direction = scoreDiff < 0 ? "UP" : "DOWN";
                messageToSend =
                    $"The critic score for **{game.GameName}** has gone **{direction}** from **{oldCriticScoreRounded}** to **{newCriticScoreRounded}**";
            }
        }

        if (string.IsNullOrEmpty(messageToSend))
        {
            return;
        }

        var messageTasks = new List<Task>();
        foreach (var leagueChannel in newsEnabledChannels)
        {
            if (leagueChannel.GameNewsSetting == DiscordGameNewsSetting.Relevant &&
                !LeagueHasGame(leaguesWithGame, leagueChannel))
            {
                continue;
            }

            var guild = _client.GetGuild(leagueChannel.GuildID);
            var channel = guild?.GetChannel(leagueChannel.ChannelID);
            if (channel is not SocketTextChannel textChannel)
            {
                continue;
            }

            messageTasks.Add(textChannel.TrySendMessageAsync(messageToSend));
        }

        await Task.WhenAll(messageTasks);
    }

    public async Task SendMasterGameEditMessage(MasterGameYear existingGame, MasterGameYear editedGame, IReadOnlyList<string> changes)
    {
        bool shouldRun = await StartBot();
        if (!shouldRun)
        {
            return;
        }

        bool releaseStatusChanged = existingGame.WillRelease() != editedGame.WillRelease();

        var leaguesWithGame = await _supplementalDataRepo.GetLeaguesWithOrFormerlyWithGame(editedGame.MasterGame, existingGame.Year);
        var allChannels = await _discordRepo.GetAllLeagueChannels();
        var newsEnabledChannels = allChannels.Where(x => x.GameNewsSetting != DiscordGameNewsSetting.Off).ToList();

        var messageTasks = new List<Task>();
        foreach (var leagueChannel in newsEnabledChannels)
        {
            if (leagueChannel.GameNewsSetting == DiscordGameNewsSetting.Relevant)
            {
                var gameIsRelevant = GameIsRelevant(existingGame, releaseStatusChanged, leaguesWithGame, leagueChannel);
                if (!gameIsRelevant)
                {
                    continue;
                }
            }

            var guild = _client.GetGuild(leagueChannel.GuildID);
            var channel = guild?.GetChannel(leagueChannel.ChannelID);
            if (channel is not SocketTextChannel textChannel)
            {
                continue;
            }

            var editableChanges = changes.ToList();
            if (releaseStatusChanged)
            {
                var willReleaseStatus = editedGame.WillRelease();
                editableChanges.Add($"**{editedGame.MasterGame.GameName}** {willReleaseStatus.DiscordText} this year!");
            }

            var changesMessage = string.Join('\n', editableChanges.Select(x => $"> {x}"));
            messageTasks.Add(textChannel.TrySendMessageAsync($"> Game Update: **{editedGame.MasterGame.GameName}**\n {changesMessage}"));
        }

        await Task.WhenAll(messageTasks);
    }

    public async Task SendGameReleaseUpdates(IEnumerable<MasterGameYear> masterGamesReleasingToday, int year)
    {
        bool shouldRun = await StartBot();
        if (!shouldRun)
        {
            return;
        }

        var leagueHasGameLookup = await _supplementalDataRepo.GetLeaguesWithOrFormerlyWithGames(masterGamesReleasingToday.Select(x => x.MasterGame), year);
        var allChannels = await _discordRepo.GetAllLeagueChannels();
        var newsEnabledChannels = allChannels.Where(x => x.GameNewsSetting != DiscordGameNewsSetting.Off).ToList();

        var messageTasks = new List<Task>();
        foreach (var leagueChannel in newsEnabledChannels)
        {
            var guild = _client.GetGuild(leagueChannel.GuildID);
            var channel = guild?.GetChannel(leagueChannel.ChannelID);
            if (channel is not SocketTextChannel textChannel)
            {
                continue;
            }

            IReadOnlyList<MasterGameYear> relevantGamesForLeague;
            if (leagueChannel.GameNewsSetting == DiscordGameNewsSetting.Relevant)
            {
                relevantGamesForLeague = masterGamesReleasingToday.Where(x => GameIsInLeague(x.MasterGame, leagueHasGameLookup, leagueChannel)).ToList();
            }
            else
            {
                relevantGamesForLeague = masterGamesReleasingToday.ToList();
            }

            if (!relevantGamesForLeague.Any())
            {
                continue;
            }

            var releaseMessages = relevantGamesForLeague.Select(x => $"**{x.MasterGame.GameName}** has released!");
            var releaseMessage = string.Join("\n", releaseMessages);
            messageTasks.Add(textChannel.TrySendMessageAsync(releaseMessage));
        }

        await Task.WhenAll(messageTasks);
    }



    private static bool GameIsRelevant(MasterGameYear masterGameYear, bool releaseStatusChanged, IReadOnlySet<Guid> leaguesWithGame, MinimalLeagueChannel channel)
    {
        if (releaseStatusChanged)
        {
            return true;
        }

        return masterGameYear.IsRelevantInYear(true) || LeagueHasGame(leaguesWithGame, channel);
    }

    private static bool LeagueHasGame(IReadOnlySet<Guid> leaguesWithGame, MinimalLeagueChannel channel)
    {
        return leaguesWithGame.Contains(channel.LeagueID);
    }

    private static bool GameIsInLeague(MasterGame masterGame, ILookup<Guid, Guid> leagueHasGameLookup, MinimalLeagueChannel leagueChannel)
    {
        return leagueHasGameLookup[leagueChannel.LeagueID].Contains(masterGame.MasterGameID);
    }

    public async Task SendLeagueActionMessage(LeagueAction action)
    {
        bool shouldRun = await StartBot();
        if (!shouldRun)
        {
            return;
        }

        var leagueId = action.Publisher.LeagueYearKey.LeagueID;
        var leagueChannels = await _discordRepo.GetLeagueChannels(leagueId);

        if (leagueChannels is null)
        {
            return;
        }

        var messageTasks = new List<Task>();
        foreach (var leagueChannel in leagueChannels)
        {
            var guild = _client.GetGuild(leagueChannel.GuildID);
            SocketTextChannel? channel = guild?.GetTextChannel(leagueChannel.ChannelID);
            if (channel is null)
            {
                continue;
            }

            messageTasks.Add(channel.TrySendMessageAsync($"**{action.Publisher.GetPublisherAndUserDisplayName()}** {action.Description} (at {action.Timestamp.ToEasternDate()})"));
        }

        await Task.WhenAll(messageTasks);
    }

    public async Task SendLeagueYearScoreUpdateMessage(LeagueYearScoreChanges scoreChanges)
    {
        bool shouldRun = await StartBot();
        if (!shouldRun)
        {
            return;
        }

        var changeList = scoreChanges.GetScoreChanges();
        if (!changeList.AnyChanges)
        {
            return;
        }

        var channels = await GetChannelsForLeague(scoreChanges.LeagueYear.League.LeagueID);
        if (!channels.Any())
        {
            return;
        }

        var embedFieldBuilders = new List<EmbedFieldBuilder>();
        foreach (var change in changeList.Changes.Where(c => c.RankChanged || c.ScoreChanged))
        {
            var changeText = "";
            if (change.ScoreChanged)
            {
                changeText = $"> Score has gone **{change.Direction}** from **{change.RoundedOldScore}** to **{change.RoundedNewScore}**";
            }
            if (change.RankChanged)
            {
                changeText += $"\n> Moved from **{change.FormattedOldRank}** place to **{change.FormattedNewRank}** place";
            }

            if (!string.IsNullOrEmpty(changeText))
            {
                embedFieldBuilders.Add(new EmbedFieldBuilder
                {
                    Name = change.Publisher.GetPublisherAndUserDisplayName(),
                    Value = changeText,
                    IsInline = false
                });
            }
        }

        if (embedFieldBuilders.Any())
        {
            var tasks = channels.Select(channel => channel.TrySendMessageAsync(embed: _discordFormatter.BuildRegularEmbed(
                "Publisher Score Updates",
                "",
                null,
                embedFieldBuilders)));
            await Task.WhenAll(tasks);
        }
    }

    public async Task SendPublisherNameUpdateMessage(Publisher publisher, string oldPublisherName, string newPublisherName)
    {
        bool shouldRun = await StartBot();
        if (!shouldRun)
        {
            return;
        }

        var channels = await GetChannelsForLeague(publisher.LeagueYearKey.LeagueID);
        if (!channels.Any())
        {
            return;
        }

        var messageToSend = $"Publisher **{oldPublisherName}** ({publisher.User.UserName}) is now known as **{newPublisherName}**";


        var messageTasks = channels.Select(channel => channel.TrySendMessageAsync(messageToSend));
        await Task.WhenAll(messageTasks);
    }

    public async Task SendNewPublisherMessage(Publisher publisher)
    {
        bool shouldRun = await StartBot();
        if (!shouldRun)
        {
            return;
        }

        var channels = await GetChannelsForLeague(publisher.LeagueYearKey.LeagueID);
        if (!channels.Any())
        {
            return;
        }

        var messageToSend = $"Publisher **{publisher.GetPublisherAndUserDisplayName()}** has joined the league!";

        var messageTasks = channels.Select(channel => channel.TrySendMessageAsync(messageToSend));
        await Task.WhenAll(messageTasks);
    }

    public async Task SendPublicBiddingSummary(IEnumerable<LeagueYearPublicBiddingSet> publicBiddingSets)
    {
        bool shouldRun = await StartBot();
        if (!shouldRun)
        {
            return;
        }

        var allChannels = await _discordRepo.GetAllLeagueChannels();
        var channelLookup = allChannels.ToLookup(c => c.LeagueID);

        List<Task> messageTasks = new List<Task>();
        foreach (var publicBiddingSet in publicBiddingSets)
        {
            if (!publicBiddingSet.MasterGames.Any())
            {
                continue;
            }

            var leagueChannels = channelLookup[publicBiddingSet.LeagueYear.League.LeagueID].ToList();
            if (!leagueChannels.Any())
            {
                continue;
            }

            var gameMessages = new List<string>();
            foreach (var publicBid in publicBiddingSet.MasterGames)
            {
                var gameMessage = "";
                var releaseDate = publicBid.MasterGameYear.MasterGame.EstimatedReleaseDate;
                gameMessage += $"**{publicBid.MasterGameYear.MasterGame.GameName}**";

                if (publicBid.CounterPick)
                {
                    gameMessage += " (🎯 Counter Pick Bid)";
                }

                gameMessage += $"\n> Release Date: {releaseDate}";

                var roundedHypeFactor = Math.Round(publicBid.MasterGameYear.HypeFactor, 1);
                gameMessage += $"\n> Hype Factor: {roundedHypeFactor}\n";
                gameMessages.Add(gameMessage);
            }

            var leagueLink = new LeagueUrlBuilder(_baseAddress, publicBiddingSet.LeagueYear.League.LeagueID, publicBiddingSet.LeagueYear.Year).BuildUrl();
            var finalMessage = string.Join("\n", gameMessages);
            var lastSunday = GetLastSunday();
            var header = $"Public Bids (Week of {lastSunday:MMMM dd, yyyy})";

            foreach (var leagueChannel in leagueChannels)
            {
                var guild = _client.GetGuild(leagueChannel.GuildID);
                var channel = guild?.GetTextChannel(leagueChannel.ChannelID);
                if (channel is null)
                {
                    continue;
                }

                messageTasks.Add(channel.TrySendMessageAsync(embed: _discordFormatter.BuildRegularEmbed(
                    header,
                    finalMessage,
                    url: leagueLink)));
            }
        }

        await Task.WhenAll(messageTasks);
    }

    private static DateTime GetLastSunday()
    {
        var currentDate = DateTime.Now;
        var currentDayOfWeek = (int)currentDate.DayOfWeek;
        var lastSundayDate = currentDate.AddDays(-currentDayOfWeek);
        return lastSundayDate;
    }

    public async Task SendActionProcessingSummary(IEnumerable<LeagueActionProcessingSet> leagueActionSets)
    {
        bool shouldRun = await StartBot();
        if (!shouldRun)
        {
            return;
        }

        var allChannels = await _discordRepo.GetAllLeagueChannels();
        var channelLookup = allChannels.ToLookup(c => c.LeagueID);

        List<Task> dropTasks = new List<Task>();
        foreach (var leagueAction in leagueActionSets)
        {
            var leagueChannels = channelLookup[leagueAction.LeagueYear.League.LeagueID].ToList();
            if (!leagueChannels.Any())
            {
                continue;
            }

            dropTasks.Add(SendAllDropMessages(leagueAction, leagueChannels));
        }

        await Task.WhenAll(dropTasks);

        List<Task> bidTasks = new List<Task>();
        foreach (var leagueAction in leagueActionSets)
        {
            var leagueChannels = channelLookup[leagueAction.LeagueYear.League.LeagueID].ToList();
            if (!leagueChannels.Any())
            {
                continue;
            }

            bidTasks.Add(SendAllBidMessages(leagueAction, leagueChannels));
        }

        await Task.WhenAll(bidTasks);
    }

    private async Task SendAllBidMessages(LeagueActionProcessingSet leagueAction, List<MinimalLeagueChannel> leagueChannels)
    {
        var bidMessages = new List<string>();
        var leagueActionDictionaryByGame = new Dictionary<string, List<PickupBid>>();

        foreach (var leagueActionBid in leagueAction.Bids)
        {
            if (leagueActionDictionaryByGame.ContainsKey(leagueActionBid.MasterGame.GameName))
            {
                leagueActionDictionaryByGame[leagueActionBid.MasterGame.GameName].Add(leagueActionBid);
            }
            else
            {
                leagueActionDictionaryByGame.Add(leagueActionBid.MasterGame.GameName,
                    new List<PickupBid> { leagueActionBid });
            }
        }

        foreach (var bidGameAction in leagueActionDictionaryByGame)
        {
            var winningBidAmount = bidGameAction.Value.OrderByDescending(b => b.BidAmount).First().BidAmount;

            var messageToAdd = $"**{bidGameAction.Key}**\n";
            foreach (var bid in bidGameAction.Value)
            {
                if (!bid.Successful.HasValue)
                {
                    throw new Exception($"Bid {bid.BidID} Successful property is null");
                }

                if (bid.Successful.Value)
                {
                    var counterPickMessage = bid.CounterPick ? "(🎯 Counter Pick)" : "";
                    messageToAdd += $"- Won by {bid.Publisher.GetPublisherAndUserDisplayName()} with a bid of ${bid.BidAmount} {counterPickMessage}\n";
                }
                else
                {
                    var lossReason = bid.BidAmount == winningBidAmount
                        ? "lost on tiebreakers"
                        : "was outbid";
                    messageToAdd += $"- {bid.Publisher.GetPublisherAndUserDisplayName()}'s bid of ${bid.BidAmount} {lossReason}\n";
                }
            }

            bidMessages.Add($"{messageToAdd}");
        }

        if (bidMessages.Any())
        {
            var messageListToSend = new MessageListBuilder(bidMessages,
                MaxMessageLength)
            .WithTitle("Bids", new[] { TextStyleOption.Bold, TextStyleOption.Underline }, "\n", 1)
            .WithDivider("\n")
            .Build();
            await SendAllMessagesToAllLeagueChannels(leagueChannels, messageListToSend);
        }
    }

    private async Task SendAllDropMessages(LeagueActionProcessingSet leagueAction, IEnumerable<MinimalLeagueChannel> leagueChannels)
    {
        var dropMessages = new List<string>();
        foreach (var drop in leagueAction.Drops)
        {
            if (!drop.Successful.HasValue)
            {
                throw new Exception($"Drop {drop.DropRequestID} Successful property is null");
            }

            var statusMessage = drop.Successful.Value ? "Successful" : "Failed";
            var messageToAdd = $"**{drop.Publisher.GetPublisherAndUserDisplayName()}**: {drop.MasterGame.GameName} (Drop {statusMessage})";
            dropMessages.Add(messageToAdd);
        }

        if (dropMessages.Any())
        {
            var dropMessageListToSend = new MessageListBuilder(dropMessages,
                MaxMessageLength)
            .WithTitle("Drops", new[] { TextStyleOption.Bold, TextStyleOption.Underline }, "\n", 1)
            .WithDivider("\n")
            .Build();
            await SendAllMessagesToAllLeagueChannels(leagueChannels, dropMessageListToSend);
        }
    }

    private async Task SendAllMessagesToAllLeagueChannels(IEnumerable<MinimalLeagueChannel> leagueChannels, IReadOnlyList<string> messageListToSend)
    {
        var messageTasks = new List<Task>();
        foreach (var leagueChannel in leagueChannels)
        {
            foreach (var messageToSend in messageListToSend)
            {
                var guild = _client.GetGuild(leagueChannel.GuildID);
                var channel = guild?.GetTextChannel(leagueChannel.ChannelID);
                if (channel is null)
                {
                    continue;
                }

                messageTasks.Add(channel.TrySendMessageAsync(messageToSend));
            }
        }

        await Task.WhenAll(messageTasks);
    }

    public async Task SendTradeUpdateMessage(Trade trade)
    {
        bool shouldRun = await StartBot();
        if (!shouldRun)
        {
            return;
        }

        var channels = await GetChannelsForLeague(trade.LeagueYear.League.LeagueID);
        if (!channels.Any())
        {
            return;
        }

        var header = $"The following trade has been **{trade.Status.Value.ToUpper()}**";

        var embedFieldBuilder = new EmbedFieldBuilder
        {
            Name = "Trade Details",
            Value = BuildTradeMessage(trade, trade.Status.Equals(TradeStatus.Proposed)),
            IsInline = false
        };
        var embedFieldBuilders = new List<EmbedFieldBuilder>
        {
            embedFieldBuilder
        };

        var messageTasks = channels.Select(channel => channel.TrySendMessageAsync(embed: _discordFormatter.BuildRegularEmbed(
            "Trade Update",
            header,
            null,
            embedFieldBuilders)));

        await Task.WhenAll(messageTasks);
    }

    private static string BuildTradeMessage(Trade trade, bool includeMessage)
    {
        var message = $"**{trade.Proposer.GetPublisherAndUserDisplayName()}** will receive: ";

        var counterPartySendGames = BuildGameListText(trade.CounterPartyMasterGames);
        var hasCounterPartySendGames = !string.IsNullOrEmpty(counterPartySendGames);

        if (hasCounterPartySendGames)
        {
            message += counterPartySendGames;
        }

        if (trade.CounterPartyBudgetSendAmount != default)
        {
            if (hasCounterPartySendGames)
            {
                message += " and ";
            }
            message += $"**${trade.CounterPartyBudgetSendAmount}** of budget**";
        }

        message += $"\n**{trade.CounterParty.GetPublisherAndUserDisplayName()}** will receive: ";

        var proposerSendGames = BuildGameListText(trade.ProposerMasterGames);
        var hasProposerSendGames = !string.IsNullOrEmpty(proposerSendGames);
        if (hasProposerSendGames)
        {
            message += proposerSendGames;
        }

        if (trade.ProposerBudgetSendAmount != default)
        {
            if (hasProposerSendGames)
            {
                message += " and ";
            }

            message += $"**${trade.ProposerBudgetSendAmount}** of budget";
        }

        if (includeMessage)
        {
            message += $"\nMessage from **{trade.Proposer.GetPublisherAndUserDisplayName()}**: **{trade.Message}**";
        }

        return message;
    }

    private async Task<IReadOnlyList<SocketTextChannel>> GetChannelsForLeague(Guid leagueID)
    {
        var leagueChannels = await _discordRepo.GetLeagueChannels(leagueID);

        List<SocketTextChannel> channels = new List<SocketTextChannel>();
        foreach (var leagueChannel in leagueChannels)
        {
            var guild = _client.GetGuild(leagueChannel.GuildID);
            var channel = guild?.GetTextChannel(leagueChannel.ChannelID);
            if (channel is not null)
            {
                channels.Add(channel);
            }
        }

        return channels;
    }

    private static string BuildGameListText(IEnumerable<MasterGameYearWithCounterPick> games)
    {
        var gameNames = games.Select(g => g.MasterGameYear.MasterGame.GameName);
        var gameNameString = string.Join(" and ", gameNames);
        return gameNameString;
    }

    private Task Client_Ready()
    {
        _botIsReady = true;
        return Task.CompletedTask;
    }

    private static Task Log(LogMessage msg)
    {
        Serilog.Log.Information(msg.ToString());
        return Task.CompletedTask;
    }
}
