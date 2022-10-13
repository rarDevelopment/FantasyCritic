using Discord.Interactions;
using FantasyCritic.Discord.Interfaces;
using FantasyCritic.Lib.Extensions;
using FantasyCritic.Lib.Interfaces;
using NodaTime;

namespace FantasyCritic.Discord.Commands;
public class RemoveLeagueCommand : InteractionModuleBase<SocketInteractionContext>
{
    private readonly IDiscordRepo _discordRepo;
    private readonly IClock _clock;
    private readonly IDiscordFormatter _discordFormatter;

    public RemoveLeagueCommand(IDiscordRepo discordRepo,
        IClock clock,
        IDiscordFormatter discordFormatter)
    {
        _discordRepo = discordRepo;
        _clock = clock;
        _discordFormatter = discordFormatter;
    }

    [SlashCommand("remove-league", "Removes the configuration for the league associated with the current channel.")]
    public async Task RemoveLeague()
    {
        var dateToCheck = _clock.GetToday();

        var leagueChannel = await _discordRepo.GetLeagueChannel(Context.Channel.Id.ToString(), dateToCheck.Year);
        if (leagueChannel == null)
        {
            await RespondAsync(embed: _discordFormatter.BuildErrorEmbed(
                "Error Removing League Configuration",
                "No league configuration found for this channel.",
                Context.User));
            return;
        }

        await _discordRepo.DeleteLeagueChannel(Context.Channel.Id.ToString());

        await RespondAsync(embed: _discordFormatter.BuildRegularEmbed(
            "Removed League Configuration",
            "Channel configuration removed.",
            Context.User));
    }
}
