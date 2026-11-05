using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Features.Execution;
using TAOM.Features.NamedCompanions;
using TAOM.Features.NamedCompanions.Domain;

namespace TAOM.Features.WandererAllegiance;

/// <summary>
/// Pure decision for whether a wanderer takes the player's coin (#575). Reuses the
/// <see cref="IAlignmentService"/> table every other alignment feature reads. It keys on CULTURE
/// StringIds (the keys of <c>execution/alignment.json</c>) for the wanderer, because a tavern wanderer
/// has no clan and no kingdom. The player's side is the player clan's map faction (the kingdom served,
/// or the clan itself when it serves none) with that faction's own culture as the fallback
/// (<see cref="IAlignmentService.ResolveSide"/>, pair from <c>FactionSides.OfPlayer</c>), so a
/// Gondor-born mercenary of Mordor reads Evil and is refused by a Gondor wanderer, and a Neutral realm's
/// player is Neutral whatever the player's own culture (#957).
/// </summary>
/// <remarks>
/// Deliberately does NOT call <see cref="IAlignmentService.AreEnemyAlignments"/>, whose Neutral
/// semantics are inverted for a pairing question: it returns true when either side is Neutral, and
/// would bar every Umbar, Khand, Shaghana and Abanissa wanderer from serving anybody. Here Neutral
/// serves anyone and a Neutral player is refused by nobody, the same reading
/// <c>MarriageAlignmentService</c> and <c>RecruitmentAlignmentService</c> give the table.
/// <para>
/// The named-companion id set is process-lifetime config read once, lazily, because a dialogue
/// condition runs on every render of the line.
/// </para>
/// </remarks>
public class WandererAllegianceService : IWandererAllegianceService
{
    private readonly IAlignmentService _alignment;
    private readonly IWandererAllegianceSettingsProvider _settings;
    private readonly Lazy<HashSet<string>> _namedCompanionIds;

    public WandererAllegianceService(
        IAlignmentService alignment,
        IWandererAllegianceSettingsProvider settings,
        INamedCompanionConfigProvider namedCompanions)
    {
        _alignment = alignment;
        _settings = settings;
        _namedCompanionIds = new Lazy<HashSet<string>>(() => new HashSet<string>(
            (namedCompanions.GetCompanions() ?? Array.Empty<NamedCompanionDefinition>())
                .Select(c => c.CharacterId)
                .Where(id => !string.IsNullOrEmpty(id)),
            StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Decides whether the wanderer refuses. Any null or unknown culture resolves to Neutral, and
    /// Neutral on either side allows the hire, so the rule fails open to vanilla.
    /// </summary>
    /// <param name="wandererHeroId">The wanderer's <c>Hero.StringId</c>; consulted only by the named-companions-only scope.</param>
    /// <param name="wandererCultureId">The wanderer's culture StringId.</param>
    /// <param name="player">The player clan's map faction id with that faction's culture.</param>
    public WandererHireVerdict Evaluate(string? wandererHeroId, string? wandererCultureId, SideIds player)
    {
        if (!_settings.IsEnabled)
            return WandererHireVerdict.Allowed;

        if (_settings.Scope == WandererAllegianceScope.NamedCompanionsOnly && !IsNamedCompanion(wandererHeroId))
            return WandererHireVerdict.Allowed;

        // A hero with no culture is nobody's enemy: fail open to vanilla rather than guess a side.
        if (string.IsNullOrEmpty(wandererCultureId))
            return WandererHireVerdict.Allowed;

        var wandererSide = _alignment.GetCultureSide(wandererCultureId!);
        var playerSide = _alignment.ResolveSide(player);

        if (wandererSide == FactionSide.Neutral || playerSide == FactionSide.Neutral || wandererSide == playerSide)
            return WandererHireVerdict.Allowed;

        // Both sides are non-Neutral and differ, so this is a Free/Evil opposition either way round.
        return wandererSide == FactionSide.Free
            ? WandererHireVerdict.RefusedByFreeWanderer
            : WandererHireVerdict.RefusedByEvilWanderer;
    }

    // Hero.StringId equals the NPCCharacter id for an XML hero (Hero.Deserialize resolves the
    // CharacterObject by base.StringId), which is also how NamedCompanionAdapter finds them.
    private bool IsNamedCompanion(string? heroId) =>
        !string.IsNullOrEmpty(heroId) && _namedCompanionIds.Value.Contains(heroId!);
}
