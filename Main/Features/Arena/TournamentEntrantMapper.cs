using TAOM.Core.Domain;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace TAOM.Features.Arena;

/// <summary>
/// Boundary conversion for the tournament roster guard: sealed engine types in, primitives out.
///
/// Split out of <c>Patch69_TournamentRosterGuard</c> so the Harmony class stays a thin entry point
/// (ADR-002) — the patch binds the target and delegates; this maps; the service decides. Sealed
/// TaleWorlds types are legitimate here because this IS the boundary (ADR-007), the same role
/// <see cref="TAOM.Features.BattleLoadDiagnostics.SpawnOriginFormatter"/> plays for its patch.
/// </summary>
public static class TournamentEntrantMapper
{
    /// <summary>
    /// Reduce a sealed <c>CharacterObject</c> to the primitives the winner panel actually reads.
    /// A null character maps to <c>default</c>, which the guard service classifies as
    /// <see cref="TournamentEntrantVerdict.UnsafeNullCharacter"/>.
    /// </summary>
    public static TournamentEntrant Describe(CharacterObject character, IRaceManager raceManager)
    {
        if (character == null) return default;

        var hero = character.IsHero ? character.HeroObject : null;

        // Validate-before-lookup: GetRaceNameFromId coerces an unknown id to "human", so an
        // unvalidated call would put a plausible-but-wrong race in the diagnostic line that names
        // the offender (.claude/rules/csharp-architecture.md "Validate Before Lookup").
        var raceName = raceManager != null && raceManager.IsValidRaceId(character.Race)
            ? raceManager.GetRaceNameFromId(character.Race)
            : null;

        return new TournamentEntrant(
            characterId: character.StringId,
            name: character.Name?.ToString(),
            isHero: character.IsHero,
            isFemale: character.IsFemale,
            raceName: raceName,
            clanId: hero?.Clan?.StringId,
            hasMapFaction: hero?.MapFaction != null,
            hasCulture: character.Culture != null);
    }

    /// <summary>
    /// The substitute troop for an unsafe entrant. Mirrors vanilla's own padding choice in the tail
    /// of <c>FightTournamentGame.GetParticipantCharacters</c> — elite basic troop first, then basic
    /// troop. Both are culture-owned so <c>Culture</c> is non-null by construction, and both are
    /// non-heroes so the winner panel's hero branch never runs for them.
    ///
    /// Returns null when no filler resolves; the caller then leaves the roster untouched rather
    /// than shrinking it.
    /// </summary>
    public static CharacterObject ResolveFiller(Settlement settlement)
    {
        var culture = settlement?.Culture;
        if (culture == null) return null;

        var filler = culture.EliteBasicTroop ?? culture.BasicTroop;
        // Guard the guard: a filler whose own Culture is null would reintroduce the exact crash
        // this feature exists to prevent.
        return filler?.Culture != null ? filler : null;
    }
}
