using FantasyCritic.Lib.Domain.Combinations;
using FantasyCritic.Lib.Identity;

namespace FantasyCritic.Lib.Interfaces;

public interface ICombinedDataRepo
{
    Task<BasicData> GetBasicData();
    Task<HomePageData> GetHomePageData(FantasyCriticUser currentUser);
    Task<LeagueYear?> GetLeagueYear(Guid leagueID, int year);
    Task<LeagueYearWithSupplementalDataFromRepo?> GetLeagueYearWithSupplementalData(Guid leagueID, int year, FantasyCriticUser? currentUser);
}
