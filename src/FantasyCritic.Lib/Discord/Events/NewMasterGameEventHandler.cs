using Discord.WebSocket;
using FantasyCritic.Lib.Discord.Notifications;
using FantasyCritic.Lib.Discord.Utilities;
using FantasyCritic.Lib.Interfaces;
using MediatR;

namespace FantasyCritic.Lib.Discord.Events;
public class NewMasterGameEventHandler : INotificationHandler<NewMasterGameNotification>
{
    private readonly IDiscordRepo _discordRepo;
    private readonly DiscordSocketClient _client;

    public NewMasterGameEventHandler(IDiscordRepo discordRepo, DiscordSocketClient client)
    {
        _discordRepo = discordRepo;
        _client = client;
    }
    public Task Handle(NewMasterGameNotification notification, CancellationToken cancellationToken)
    {
        _ = Task.Run(async () =>
        {
            var masterGame = notification.MasterGame;
            var year = notification.Year;

            var allChannels = await _discordRepo.GetAllLeagueChannels();
            var newsEnabledChannels = allChannels.Where(x => x.GameNewsSetting != DiscordGameNewsSetting.Off).ToList();

            var messageTasks = new List<Task>();
            foreach (var leagueChannel in newsEnabledChannels)
            {
                if (leagueChannel.GameNewsSetting == DiscordGameNewsSetting.Relevant)
                {
                    bool gameIsRelevant = NewGameIsRelevant(masterGame, year);
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

                var tagsString = string.Join(", ", masterGame.Tags.Select(x => x.ReadableName));
                messageTasks.Add(textChannel.TrySendMessageAsync($"New Game Added! **{masterGame.GameName}** (Tagged as: **{tagsString}**)"));
            }

            await Task.WhenAll(messageTasks);
        }, cancellationToken);
        return Task.CompletedTask;
    }
    private static bool NewGameIsRelevant(MasterGame masterGame, int year)
    {
        return masterGame.CouldReleaseInYear(year);
    }
}
