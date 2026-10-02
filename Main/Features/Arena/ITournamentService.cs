using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.TournamentGames;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace TAOM.Features.Arena;

/// <summary>
/// Phase 9b #137 — service layer for TaomTournamentModel. Extracted to eliminate rule-4 inline
/// branching from the model overrides. Some methods still accept sealed TaleWorlds types (Town,
/// TournamentGame, CharacterObject) where the model boundary itself receives them; per ADR-007
/// the service does NOT depend on sealed types for its decisions (it could be substituted in
/// tests via an adapter), but pragmatically the existing surface keeps the boundary type for
/// the few cases where no adapter exists yet.
/// </summary>
public interface ITournamentService
{
    /// <summary>
    /// Compute tournament-start chance. Pre-fix this was an inline switch + LINQ count in the
    /// model body. Service variant accepts the count directly (computed at the boundary by the
    /// model) so the decision logic is unit-testable without Campaign.Current.
    /// </summary>
    float CalculateStartChance(int lordCount);

    /// <summary>Compute tournament-end chance from elapsed days.</summary>
    float CalculateEndChance(float elapsedDays);

    /// <summary>
    /// The prize list for one band (<see cref="TournamentPrizeRules"/>): the culture's own items, or every
    /// culture's when it has none, so the list is empty only if no loaded item fits the band at all.
    /// </summary>
    MBList<ItemObject> BuildPrizePool(string? cultureId, PrizeBand band);

    /// <summary>Resolve the dummy-character ID for participant armor selection.</summary>
    string ResolveDummyId(string participantCultureId, string settlementCultureId);

    /// <summary>
    /// True if a tournament participant of this FaceGen race id must fight on foot — i.e. their
    /// custom skeleton clips inside the mount when given a mounted loadout. Currently: dwarves.
    /// Keyed on race (not culture) so a dwarf competing in any town's tournament is caught.
    /// </summary>
    bool ShouldDismountInTournament(int raceId);
}
