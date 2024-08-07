using FantasyCritic.Lib.Interfaces;
using FantasyCritic.Lib.Domain.Results;
using FantasyCritic.Lib.Extensions;
using FantasyCritic.Lib.Identity;
using FantasyCritic.Lib.Royale;

namespace FantasyCritic.Lib.Services;

public class RoyaleService
{
    private readonly IRoyaleRepo _royaleRepo;
    private readonly IClock _clock;
    private readonly IMasterGameRepo _masterGameRepo;

    public const int MAX_GAMES = 25;
    public const int FUTURE_RELEASE_LIMIT_DAYS = 5;

    public RoyaleService(IRoyaleRepo royaleRepo, IClock clock, IMasterGameRepo masterGameRepo)
    {
        _royaleRepo = royaleRepo;
        _clock = clock;
        _masterGameRepo = masterGameRepo;
    }

    public Task<IReadOnlyList<RoyaleYearQuarter>> GetYearQuarters()
    {
        return _royaleRepo.GetYearQuarters();
    }

    public async Task<RoyaleYearQuarter> GetActiveYearQuarter()
    {
        IReadOnlyList<RoyaleYearQuarter> supportedQuarters = await GetYearQuarters();
        var activeQuarter = supportedQuarters.Where(x => x.OpenForPlay).WhereMax(x => x.YearQuarter).Single();
        return activeQuarter;
    }

    public async Task<RoyaleYearQuarter?> GetYearQuarter(int year, int quarter)
    {
        IReadOnlyList<RoyaleYearQuarter> supportedQuarters = await GetYearQuarters();
        var requestedQuarter = supportedQuarters.SingleOrDefault(x => x.YearQuarter.Year == year && x.YearQuarter.Quarter == quarter);
        return requestedQuarter;
    }

    public async Task<RoyalePublisher> CreatePublisher(RoyaleYearQuarter yearQuarter, VeryMinimalFantasyCriticUser user, string publisherName)
    {
        RoyalePublisher publisher = new RoyalePublisher(Guid.NewGuid(), yearQuarter, user, publisherName, null, null, new List<RoyalePublisherGame>(), 100m);
        await _royaleRepo.CreatePublisher(publisher);
        return publisher;
    }

    public Task ChangePublisherName(RoyalePublisher publisher, string publisherName)
    {
        return _royaleRepo.ChangePublisherName(publisher, publisherName);
    }

    public Task ChangePublisherIcon(RoyalePublisher publisher, string? publisherIcon)
    {
        return _royaleRepo.ChangePublisherIcon(publisher, publisherIcon);
    }

    public Task ChangePublisherSlogan(RoyalePublisher publisher, string? publisherSlogan)
    {
        return _royaleRepo.ChangePublisherSlogan(publisher, publisherSlogan);
    }

    public Task<RoyalePublisher?> GetPublisher(RoyaleYearQuarter yearQuarter, FantasyCriticUser user)
    {
        return _royaleRepo.GetPublisher(yearQuarter, user);
    }

    public Task<RoyalePublisherData?> GetPublisherData(Guid publisherID)
    {
        return _royaleRepo.GetPublisherData(publisherID);
    }

    public async Task<RoyalePublisher?> GetPublisher(Guid publisherID)
    {
        var publisherData = await _royaleRepo.GetPublisherData(publisherID);
        return publisherData?.RoyalePublisher;
    }

    public Task<IReadOnlyList<RoyalePublisher>> GetAllPublishers(int year, int quarter)
    {
        return _royaleRepo.GetAllPublishers(year, quarter);
    }

    public async Task<IReadOnlyList<RoyalePublisher>> GetAllPublishers(int year)
    {
        var quarters = await GetYearQuarters();
        var quartersInYear = quarters.Where(x => x.YearQuarter.Year == year);

        List<RoyalePublisher> allPublishers = new List<RoyalePublisher>();
        foreach (var quarter in quartersInYear)
        {
            var publishers = await GetAllPublishers(year, quarter.YearQuarter.Quarter);
            allPublishers.AddRange(publishers);
        }

        return allPublishers;
    }

    public Task<RoyaleYearQuarterData?> GetRoyaleYearQuarterData(int year, int quarter)
    {
        return _royaleRepo.GetRoyaleYearQuarterData(year, quarter);
    }

    public async Task<ClaimResult> PurchaseGame(RoyalePublisher publisher, MasterGameYear masterGame)
    {
        if (publisher.PublisherGames.Count >= MAX_GAMES)
        {
            return new ClaimResult("Roster is full.");
        }
        if (publisher.PublisherGames.Select(x => x.MasterGame).Contains(masterGame))
        {
            return new ClaimResult("Publisher already has that game.");
        }
        if (!masterGame.CouldReleaseInQuarter(publisher.YearQuarter.YearQuarter))
        {
            return new ClaimResult("Game will not release this quarter.");
        }

        var now = _clock.GetCurrentInstant();
        var currentDate = now.ToEasternDate();
        if (masterGame.MasterGame.IsReleased(currentDate))
        {
            return new ClaimResult("Game has been released.");
        }

        if (publisher.YearQuarter.HasReleaseDateLimit)
        {
            var fiveDaysFuture = currentDate.PlusDays(FUTURE_RELEASE_LIMIT_DAYS);
            if (masterGame.MasterGame.IsReleased(fiveDaysFuture))
            {
                return new ClaimResult($"Game will release within {FUTURE_RELEASE_LIMIT_DAYS} days.");
            }
        }

        if (masterGame.MasterGame.CriticScore.HasValue)
        {
            return new ClaimResult("Game has a score.");
        }

        if (masterGame.MasterGame.HasAnyReviews)
        {
            return new ClaimResult("That game already has reviews.");
        }

        var masterGameTags = await _masterGameRepo.GetMasterGameTags();
        var eligibilityErrors = LeagueTagExtensions.GetRoyaleClaimErrors(masterGameTags, masterGame.MasterGame, currentDate);
        if (eligibilityErrors.Any())
        {
            return new ClaimResult("Game is not eligible under Royale rules.");
        }

        var currentBudget = publisher.Budget;
        var gameCost = masterGame.GetRoyaleGameCost();
        if (currentBudget < gameCost)
        {
            return new ClaimResult("Not enough budget.");
        }

        RoyalePublisherGame game = new RoyalePublisherGame(publisher.PublisherID, publisher.YearQuarter.YearQuarter, masterGame, now, gameCost, 0m, null);
        await _royaleRepo.PurchaseGame(game);
        var nextSlot = publisher.PublisherGames.Count;
        return new ClaimResult(nextSlot);
    }

    public async Task<Result> SellGame(RoyalePublisher publisher, RoyalePublisherGame publisherGame)
    {
        var masterGameTags = await _masterGameRepo.GetMasterGameTags();
        var currentlyInEligible = publisherGame.CalculateIsCurrentlyIneligible(masterGameTags);
        if (!currentlyInEligible)
        {
            var currentDate = _clock.GetToday();
            if (publisherGame.MasterGame.MasterGame.IsReleased(currentDate))
            {
                return Result.Failure("That game has already been released.");
            }
            if (publisherGame.MasterGame.MasterGame.CriticScore.HasValue)
            {
                return Result.Failure("That game already has a score.");
            }

            if (!publisher.PublisherGames.Contains(publisherGame))
            {
                return Result.Failure("You don't have that game.");
            }
        }
        
        await _royaleRepo.SellGame(publisherGame, currentlyInEligible);
        return Result.Success();
    }

    public async Task<Result> SetAdvertisingMoney(RoyalePublisher publisher, RoyalePublisherGame publisherGame, decimal advertisingMoney)
    {
        var currentDate = _clock.GetToday();
        if (publisherGame.MasterGame.MasterGame.IsReleased(currentDate))
        {
            return Result.Failure("That game has already been released.");
        }

        if (publisherGame.MasterGame.MasterGame.CriticScore.HasValue)
        {
            return Result.Failure("That game already has a score.");
        }

        if (!publisher.PublisherGames.Contains(publisherGame))
        {
            return Result.Failure("You don't have that game.");
        }

        decimal newDollarsToSpend = advertisingMoney - publisherGame.AdvertisingMoney;
        if (publisher.Budget < newDollarsToSpend)
        {
            return Result.Failure("You don't have enough money.");
        }

        if (advertisingMoney < 0m)
        {
            return Result.Failure("You can't allocate negative dollars in advertising money.");
        }

        if (advertisingMoney > 10m)
        {
            return Result.Failure("You can't allocate more than 10 dollars in advertising money.");
        }

        await _royaleRepo.SetAdvertisingMoney(publisherGame, advertisingMoney);
        return Result.Success();
    }

    public async Task UpdateFantasyPoints(YearQuarter yearQuarter)
    {
        Dictionary<(Guid, Guid), decimal?> publisherGameScores = new Dictionary<(Guid, Guid), decimal?>();
        var allPublishersForQuarter = await _royaleRepo.GetAllPublishers(yearQuarter.Year, yearQuarter.Quarter);

        var allMasterGameTags = await _masterGameRepo.GetMasterGameTags();
        var currentDate = _clock.GetToday();
        foreach (var publisher in allPublishersForQuarter)
        {
            foreach (var publisherGame in publisher.PublisherGames)
            {
                decimal? fantasyPoints = publisherGame.CalculateFantasyPoints(currentDate, allMasterGameTags);
                publisherGameScores.Add((publisherGame.PublisherID, publisherGame.MasterGame.MasterGame.MasterGameID), fantasyPoints);
            }
        }

        await _royaleRepo.UpdateFantasyPoints(publisherGameScores);
    }

    public async Task<IReadOnlyList<RoyaleYearQuarter>> GetQuartersWonByUser(IVeryMinimalFantasyCriticUser user)
    {
        var quarters = await _royaleRepo.GetYearQuarters();
        return quarters.Where(x => x.WinningUser is not null && x.WinningUser.UserID == user.UserID).ToList();
    }

    public Task StartNewQuarter(YearQuarter nextQuarter)
    {
        return _royaleRepo.StartNewQuarter(nextQuarter);
    }

    public Task FinishQuarter(RoyaleYearQuarter supportedQuarter)
    {
        return _royaleRepo.FinishQuarter(supportedQuarter);
    }

    public Task CalculateRoyaleWinnerForQuarter(RoyaleYearQuarter supportedQuarter)
    {
        return _royaleRepo.CalculateRoyaleWinnerForQuarter(supportedQuarter.YearQuarter.Year, supportedQuarter.YearQuarter.Quarter);
    }
}
