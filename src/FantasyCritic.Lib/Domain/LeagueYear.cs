using FantasyCritic.Lib.BusinessLogicFunctions;
using FantasyCritic.Lib.Domain.Calculations;
using FantasyCritic.Lib.Extensions;
using FantasyCritic.Lib.Identity;

namespace FantasyCritic.Lib.Domain;

public class LeagueYear : IEquatable<LeagueYear>
{
    private readonly IReadOnlyDictionary<MasterGame, EligibilityOverride> _eligibilityOverridesDictionary;
    private readonly IReadOnlyDictionary<MasterGame, TagOverride> _tagOverridesDictionary;
    private readonly IReadOnlyDictionary<Guid, Publisher> _publisherDictionary;

    public LeagueYear(League league, SupportedYear year, LeagueOptions options, PlayStatus playStatus,
        bool draftOrderSet, IEnumerable<EligibilityOverride> eligibilityOverrides, IEnumerable<TagOverride> tagOverrides,
        Instant? draftStartedTimestamp, FantasyCriticUser? winningUser, IEnumerable<Publisher> publishers, bool? conferenceLocked)
    {
        League = league;
        SupportedYear = year;
        Options = options;
        PlayStatus = playStatus;
        DraftOrderSet = draftOrderSet;
        EligibilityOverrides = eligibilityOverrides.ToList();
        _eligibilityOverridesDictionary = EligibilityOverrides.ToDictionary(x => x.MasterGame);
        TagOverrides = tagOverrides.ToList();
        _tagOverridesDictionary = TagOverrides.ToDictionary(x => x.MasterGame);
        DraftStartedTimestamp = draftStartedTimestamp;
        WinningUser = winningUser;
        ConferenceLocked = conferenceLocked;

        _publisherDictionary = publishers.ToDictionary(x => x.PublisherID);

        StandardGamesTaken = _publisherDictionary.Values.SelectMany(x => x.PublisherGames).Count(x => !x.CounterPick);
    }

    public League League { get; }
    public SupportedYear SupportedYear { get; }
    public int Year => SupportedYear.Year;
    public LeagueOptions Options { get; }
    public PlayStatus PlayStatus { get; }
    public bool DraftOrderSet { get; }
    public IReadOnlyList<EligibilityOverride> EligibilityOverrides { get; }
    public IReadOnlyList<TagOverride> TagOverrides { get; }
    public Instant? DraftStartedTimestamp { get; }
    public FantasyCriticUser? WinningUser { get; }
    public bool? ConferenceLocked { get; }
    public IReadOnlyList<Publisher> Publishers => _publisherDictionary.Values.ToList();
    public int StandardGamesTaken { get; }
    public int TotalNumberOfStandardGames => Options.StandardGames * Publishers.Count;

    public LocalDate CounterPickDeadline => Options.CounterPickDeadline.InYear(Year);
    public LocalDate? MightReleaseDroppableDate => Options.MightReleaseDroppableDate?.InYear(Year);

    public LeagueYearKey Key => new LeagueYearKey(League.LeagueID, Year);

    public string GetGroupName => $"{League.LeagueID}|{Year}";

    public MasterGameWithEligibilityFactors GetEligibilityFactorsForMasterGame(MasterGame masterGame, LocalDate dateOfPotentialAcquisition)
    {
        bool? eligibilityOverride = GetOverriddenEligibility(masterGame);
        IReadOnlyList<MasterGameTag> tagOverrides = GetOverriddenTags(masterGame);
        return new MasterGameWithEligibilityFactors(masterGame, Options, eligibilityOverride, tagOverrides, dateOfPotentialAcquisition);
    }

    public MasterGameWithEligibilityFactors? GetEligibilityFactorsForSlot(PublisherSlot publisherSlot)
    {
        if (publisherSlot.PublisherGame?.MasterGame is null)
        {
            return null;
        }

        var masterGame = publisherSlot.PublisherGame.MasterGame.MasterGame;
        bool? eligibilityOverride = GetOverriddenEligibility(masterGame);
        IReadOnlyList<MasterGameTag> tagOverrides = GetOverriddenTags(masterGame);
        var acquisitionDate = publisherSlot.PublisherGame.Timestamp.ToEasternDate();
        return new MasterGameWithEligibilityFactors(publisherSlot.PublisherGame.MasterGame.MasterGame, Options, eligibilityOverride, tagOverrides, acquisitionDate);
    }

    public bool GameIsEligibleInAnySlot(MasterGame masterGame, LocalDate dateOfPotentialAcquisition)
    {
        var eligibilityFactors = GetEligibilityFactorsForMasterGame(masterGame, dateOfPotentialAcquisition);
        return SlotEligibilityFunctions.GameIsEligibleInLeagueYear(eligibilityFactors);
    }

    private bool? GetOverriddenEligibility(MasterGame masterGame)
    {
        var eligibilityOverride = _eligibilityOverridesDictionary.GetValueOrDefault(masterGame);
        return eligibilityOverride?.Eligible;
    }

    private IReadOnlyList<MasterGameTag> GetOverriddenTags(MasterGame masterGame)
    {
        var tagOverride = _tagOverridesDictionary.GetValueOrDefault(masterGame);
        if (tagOverride is null)
        {
            return new List<MasterGameTag>();
        }

        return tagOverride.Tags;
    }

    public Publisher? GetUserPublisher(IMinimalFantasyCriticUser? user)
    {
        if (user is null)
        {
            return null;
        }

        return Publishers.SingleOrDefault(x => x.User.Id == user.UserID);
    }

    public IReadOnlyList<Publisher> GetAllPublishersExcept(Publisher publisher)
    {
        return Publishers.Where(x => x.PublisherID != publisher.PublisherID).ToList();
    }

    public Publisher? GetPublisherByID(Guid publisherID) => _publisherDictionary.GetValueOrDefault(publisherID);

    public Publisher GetPublisherByIDOrThrow(Guid publisherID)
    {
        var publisher = GetPublisherByID(publisherID);
        if (publisher is null)
        {
            throw new Exception($"League: {League.LeagueID} has no publisher with ID: {publisherID}");
        }

        return publisher;
    }

    public Publisher GetPublisherByIDOrFakePublisher(Guid? publisherID)
    {
        if (publisherID is null)
        {
            return Publisher.GetFakePublisher(Key);
        }

        var publisher = _publisherDictionary.GetValueOrDefault(publisherID.Value);
        return publisher ?? Publisher.GetFakePublisher(Key);
    }

    public LeagueYear GetUpdatedLeagueYearWithNewScores(IReadOnlyDictionary<Guid, PublisherGameCalculatedStats> calculatedStats)
    {
        var newPublishers = Publishers.Select(x => x.GetUpdatedPublisherWithNewScores(calculatedStats)).ToList();
        return new LeagueYear(League, SupportedYear, Options, PlayStatus, DraftOrderSet, EligibilityOverrides, TagOverrides, DraftStartedTimestamp, WinningUser, newPublishers, ConferenceLocked);
    }

    public override string ToString() => $"{League}|{Year}";

    public bool Equals(LeagueYear? other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        return League.Equals(other.League) && Year == other.Year;
    }

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != this.GetType()) return false;
        return Equals((LeagueYear) obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(League, Year);
    }

    public IReadOnlySet<MasterGameYear> GetGamesInLeague()
    {
        var publisherGames = Publishers.SelectMany(x => x.PublisherGames).Where(x => x.MasterGame is not null);
        return publisherGames.Select(x => x.MasterGame!).ToHashSet();
    }
}
