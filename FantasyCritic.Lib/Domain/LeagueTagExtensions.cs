﻿using System;
using System.Collections.Generic;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using FantasyCritic.Lib.Domain.Results;
using FantasyCritic.Lib.Enums;
using NodaTime;

namespace FantasyCritic.Lib.Domain
{
    public static class LeagueTagExtensions
    {
        public static IReadOnlyList<ClaimError> GetRoyaleClaimErrors(IEnumerable<MasterGameTag> allMasterGameTags, MasterGame masterGame, LocalDate dateOfAcquisition)
        {
            var royaleSettings = GetRoyaleEligibilitySettings(allMasterGameTags);
            var claimErrors = GameHasValidTags(royaleSettings, new List<LeagueTagStatus>(), masterGame, masterGame.Tags, dateOfAcquisition);
            return claimErrors;
        }

        public static IReadOnlyList<ClaimError> GameHasValidTags(IEnumerable<LeagueTagStatus> leagueTags, IEnumerable<LeagueTagStatus> slotSpecificTags, 
            MasterGame masterGame, IEnumerable<MasterGameTag> masterGameTags, LocalDate dateOfAcquisition)
        {
            var combinedLeagueTags = CombineTags(leagueTags, slotSpecificTags);

            var masterGameCustomCodeTags = masterGameTags.Where(x => x.HasCustomCode).ToList();
            var masterGameNonCustomCodeTags = masterGameTags.Except(masterGameCustomCodeTags).ToList();
            var leagueCustomCodeTags = combinedLeagueTags.Where(x => x.Tag.HasCustomCode).ToList();
            var leagueNonCustomCodeTags = combinedLeagueTags.Except(leagueCustomCodeTags).ToList();

            //Non custom code tags
            var nonCustomCodeBannedTags = leagueNonCustomCodeTags.Where(x => x.Status.Equals(TagStatus.Banned)).Select(x => x.Tag).ToList();
            var allRequiredTags = combinedLeagueTags.Where(x => x.Status.Equals(TagStatus.Required)).Select(x => x.Tag).ToList();

            var bannedTagsIntersection = masterGameNonCustomCodeTags.Intersect(nonCustomCodeBannedTags);
            var requiredTagsIntersection = masterGameTags.Intersect(allRequiredTags);

            bool hasNoRequiredTags = allRequiredTags.Any() && !requiredTagsIntersection.Any();

            List<ClaimError> claimErrors = bannedTagsIntersection.Select(x => new ClaimError($"That game is not eligible because the {x.ReadableName} tag has been banned.", true)).ToList();
            if (hasNoRequiredTags)
            {
                claimErrors.Add(new ClaimError($"That game is not eligible because it does not have any of the following required tags: ({string.Join(",", allRequiredTags.Select(x => x.ReadableName))})", true));
            }

            if (!leagueCustomCodeTags.Any())
            {
                return claimErrors;
            }

            //Custom code tags
            var masterGameCustomCodeTagsHashSet = masterGameCustomCodeTags.Select(x => x.Name).ToHashSet();
            var leagueCustomCodeTagsDictionary = leagueCustomCodeTags.ToDictionary(x => x.Tag.Name);

            var gameIsPlannedForEarlyAccess = masterGameCustomCodeTagsHashSet.Contains("PlannedForEarlyAccess");
            var gameIsInEarlyAccess = masterGameCustomCodeTagsHashSet.Contains("CurrentlyInEarlyAccess");
            if (leagueCustomCodeTagsDictionary.TryGetValue("PlannedForEarlyAccess", out var plannedEarlyAccessTag))
            {
                if (plannedEarlyAccessTag.Status.Equals(TagStatus.Banned) && gameIsPlannedForEarlyAccess)
                {
                    claimErrors.Add(new ClaimError("That game is not eligible because it has the tag: Planned For Early Access", true));
                }
                else if (plannedEarlyAccessTag.Status.Equals(TagStatus.Required))
                {
                    if (!gameIsPlannedForEarlyAccess && !gameIsInEarlyAccess && hasNoRequiredTags)
                    {
                        claimErrors.Add(new ClaimError("That game is not eligible because it is not planned for or in early access", true));
                    }
                    else if (gameIsPlannedForEarlyAccess || gameIsInEarlyAccess)
                    {
                        hasNoRequiredTags = false;
                    }
                }
            }
            if (leagueCustomCodeTagsDictionary.TryGetValue("CurrentlyInEarlyAccess", out var currentlyInEarlyAccessTags))
            {
                if (currentlyInEarlyAccessTags.Status.Equals(TagStatus.Banned) && gameIsInEarlyAccess)
                {
                    bool acquiredBeforeEarlyAccess = masterGame.EarlyAccessReleaseDate.HasValue && masterGame.EarlyAccessReleaseDate.Value > dateOfAcquisition;
                    if (!acquiredBeforeEarlyAccess)
                    {
                        claimErrors.Add(new ClaimError("That game is not eligible because it has the tag: Currently in Early Access", true));
                    }
                }
                else if (currentlyInEarlyAccessTags.Status.Equals(TagStatus.Required))
                {
                    if (!gameIsInEarlyAccess && hasNoRequiredTags)
                    {
                        claimErrors.Add(new ClaimError("That game is not eligible because it does not have the tag: Currently in Early Access", true));
                    }
                    else if (gameIsInEarlyAccess)
                    {
                        hasNoRequiredTags = false;
                    }
                }
            }

            var gameWillReleaseInternationallyFirst = masterGameCustomCodeTagsHashSet.Contains("WillReleaseInternationallyFirst");
            var gameReleasedInternationallyFirst = masterGameCustomCodeTagsHashSet.Contains("ReleasedInternationally");
            if (leagueCustomCodeTagsDictionary.TryGetValue("WillReleaseInternationallyFirst", out var willReleaseInternationallyFirstTag))
            {
                if (willReleaseInternationallyFirstTag.Status.Equals(TagStatus.Banned) && gameWillReleaseInternationallyFirst)
                {
                    claimErrors.Add(new ClaimError("That game is not eligible because it has the tag: Will Release Internationally First", true));
                }
                else if (willReleaseInternationallyFirstTag.Status.Equals(TagStatus.Required))
                {
                    if (!gameWillReleaseInternationallyFirst && !gameReleasedInternationallyFirst && hasNoRequiredTags)
                    {
                        claimErrors.Add(new ClaimError("That game is not eligible because it will not or has not released internationally first", true));
                    }
                    else if (gameWillReleaseInternationallyFirst || gameReleasedInternationallyFirst)
                    {
                        hasNoRequiredTags = false;
                    }
                }
            }
            if (leagueCustomCodeTagsDictionary.TryGetValue("ReleasedInternationally", out var releasedInternationallyTag))
            {
                if (releasedInternationallyTag.Status.Equals(TagStatus.Banned) && gameReleasedInternationallyFirst)
                {
                    bool acquiredBeforeInternationalRelease = masterGame.InternationalReleaseDate.HasValue && masterGame.InternationalReleaseDate.Value > dateOfAcquisition;
                    if (!acquiredBeforeInternationalRelease)
                    {
                        claimErrors.Add(new ClaimError("That game is not eligible because it has the tag: Released Internationally", true));
                    }
                }
                else if (releasedInternationallyTag.Status.Equals(TagStatus.Required))
                {
                    if (!gameReleasedInternationallyFirst && hasNoRequiredTags)
                    {
                        claimErrors.Add(new ClaimError("That game is not eligible because it does not have the tag: Released Internationally", true));
                    }
                    else if (gameReleasedInternationallyFirst)
                    {
                        hasNoRequiredTags = false;
                    }
                }
            }

            return claimErrors;
        }

        private static IReadOnlyList<LeagueTagStatus> CombineTags(IEnumerable<LeagueTagStatus> leagueTags, IEnumerable<LeagueTagStatus> slotTags)
        {
            Dictionary<MasterGameTag, LeagueTagStatus> combinedLeagueTags = slotTags.ToDictionary(x => x.Tag);
            foreach (var leagueTag in leagueTags)
            {
                bool isSlotTag = combinedLeagueTags.ContainsKey(leagueTag.Tag);
                if (!isSlotTag)
                {
                    combinedLeagueTags.Add(leagueTag.Tag, leagueTag);
                }
            }

            return combinedLeagueTags.Values.ToList();
        }

        public static IReadOnlyList<LeagueTagStatus> GetRoyaleEligibilitySettings(IEnumerable<MasterGameTag> allMasterGameTags)
        {
            var bannedTagNames = new List<string>()
            {
                "CurrentlyInEarlyAccess",
                "DirectorsCut",
                "Port",
                "ReleasedInternationally",
                "Remaster",
                "YearlyInstallment"
            };

            var bannedTags = allMasterGameTags.Where(x => bannedTagNames.Contains(x.Name));
            return bannedTags.Select(x => new LeagueTagStatus(x, TagStatus.Banned)).ToList();
        }
    }
}
