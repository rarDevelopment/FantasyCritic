using MediatR;

namespace FantasyCritic.Lib.Discord.Notifications;
public class NewMasterGameNotification : INotification
{
    public MasterGame MasterGame { get; }
    public int Year { get; }

    public NewMasterGameNotification(MasterGame masterGame, int year)
    {
        MasterGame = masterGame;
        Year = year;
    }
}
