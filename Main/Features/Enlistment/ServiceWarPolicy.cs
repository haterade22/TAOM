using System.Collections.Generic;

namespace TAOM.Features.Enlistment;

/// <summary>
/// Whose wars does an enlisted soldier fight, and what does he owe when he goes home?
/// (Field report 5: "you don't officially join the kingdom, your clan individually declares war on
/// anyone you fight".)
///
/// THE REPORT DESCRIBES SOMETHING TAOM ALREADY DOES, BY ACCIDENT. Joining a commander's battle goes
/// through the vanilla <c>encounter</c> menu, whose <c>MenuHelper.EncounterAttackConsequence</c>
/// runs <c>BeHostileAction.ApplyEncounterHostileAction</c> — and that fires
/// <c>DeclareWarAction.ApplyByPlayerHostility</c> whenever the player's clan is not already at war
/// with the defender's faction (since Bannerlord v1.5.7, not when the opposing leader party is a
/// caravan or villager party). So the player's clan accrues its own private war with every enemy
/// his lord happens to meet, one battle at a time, with no announcement and no way back. The
/// question was never whether wars happen; it is whether they are deliberate and reversible.
///
/// SO: mirror the commander's wars on enlistment, and unwind exactly that mirror on discharge.
///
/// TWO FIXES TO SERVEASSOLDIER'S VERSION, both of which it still ships:
///
/// 1. SAS peaces out of EVERY war on discharge (<c>Test.cs:6537-6579</c>), including wars the player
///    started himself before ever enlisting. That is a free universal peace button: declare on
///    everyone, enlist, discharge, walk away clean. <see cref="FactionsToPeaceOnDischarge"/> only
///    ever returns factions that are in the mirror and were NOT already enemies at oath, so a war
///    the player brought with him survives his service untouched.
/// 2. SAS ignores minor factions — its own changelog admits it — so mercenary and bandit-clan wars
///    leak through the mirror and stay behind after discharge. This policy is faction-agnostic: it
///    operates on whatever ids it is handed, and the adapter decides what counts.
///
/// Pure set arithmetic so both directions are testable without a campaign.
/// </summary>
public static class ServiceWarPolicy
{
    /// <summary>
    /// Factions to declare war on when the mirror is applied: the commander's enemies that the
    /// player is not already fighting.
    /// </summary>
    public static List<string> FactionsToDeclareWarOn(
        IEnumerable<string> commanderEnemies, IEnumerable<string> playerCurrentEnemies)
    {
        var already = ToSet(playerCurrentEnemies);
        var result = new List<string>();
        var seen = new HashSet<string>();

        foreach (var id in commanderEnemies ?? new List<string>())
        {
            if (string.IsNullOrEmpty(id) || already.Contains(id) || !seen.Add(id))
                continue;
            result.Add(id);
        }

        return result;
    }

    /// <summary>
    /// Factions to make peace with on discharge: the ones this service put us at war with, minus
    /// anything the player was already at war with when he swore the oath.
    ///
    /// <paramref name="enemiesAtOath"/> is the whole safeguard. Without it this becomes SAS's
    /// universal peace button — and note it subtracts rather than intersects, so a faction the
    /// player independently declared on DURING service is also left alone: the mirror only unwinds
    /// what the mirror created.
    /// </summary>
    public static List<string> FactionsToPeaceOnDischarge(
        IEnumerable<string> mirroredWars, IEnumerable<string> enemiesAtOath)
    {
        var preExisting = ToSet(enemiesAtOath);
        var result = new List<string>();
        var seen = new HashSet<string>();

        foreach (var id in mirroredWars ?? new List<string>())
        {
            if (string.IsNullOrEmpty(id) || preExisting.Contains(id) || !seen.Add(id))
                continue;
            result.Add(id);
        }

        return result;
    }

    /// <summary>
    /// Mid-term reconciliation (#832): declare on the commander's enemies the player neither brought
    /// with him nor already mirrors, and make peace with mirrored wars the commander no longer
    /// fights. A faction the player was at war with at the oath is never declared on and never
    /// peaced, for the same reason <see cref="FactionsToPeaceOnDischarge"/> subtracts it.
    /// Deduped, null-safe, ordinal.
    /// </summary>
    public static ServiceWarDiff Diff(
        IEnumerable<string> commanderEnemies, IEnumerable<string> enemiesAtOath, IEnumerable<string> mirrored)
    {
        var commander = ToSet(commanderEnemies);
        var atOath = ToSet(enemiesAtOath);
        var mirror = ToSet(mirrored);

        var toDeclare = new List<string>();
        foreach (var id in Ordered(commanderEnemies))
        {
            if (!atOath.Contains(id) && !mirror.Contains(id) && !toDeclare.Contains(id))
                toDeclare.Add(id);
        }

        var toPeace = new List<string>();
        foreach (var id in Ordered(mirrored))
        {
            if (!atOath.Contains(id) && !commander.Contains(id) && !toPeace.Contains(id))
                toPeace.Add(id);
        }

        return new ServiceWarDiff(toDeclare, toPeace);
    }

    /// <summary>
    /// Mid-term mirroring is for a player whose OWN CLAN is the oath identity (Mike, 2026-10-20). The
    /// pin must be set, still be the player's current faction, and be his clan: a vassal's faction is
    /// his kingdom, and a mid-term mirror would move that whole kingdom's diplomacy. A vassal keeps the
    /// oath-and-discharge behaviour only.
    /// </summary>
    public static bool ShouldMirrorMidTerm(string oathFactionId, string currentFactionId, string playerClanId)
        => !string.IsNullOrEmpty(oathFactionId)
           && string.Equals(oathFactionId, currentFactionId, System.StringComparison.Ordinal)
           && string.Equals(currentFactionId, playerClanId, System.StringComparison.Ordinal);

    private static IEnumerable<string> Ordered(IEnumerable<string> ids)
    {
        foreach (var id in ids ?? new List<string>())
        {
            if (!string.IsNullOrEmpty(id))
                yield return id;
        }
    }

    private static HashSet<string> ToSet(IEnumerable<string> ids)
    {
        var set = new HashSet<string>();
        foreach (var id in ids ?? new List<string>())
        {
            if (!string.IsNullOrEmpty(id))
                set.Add(id);
        }
        return set;
    }
}

/// <summary>What <see cref="ServiceWarPolicy.Diff"/> says to do, as two ordered id lists.</summary>
public sealed class ServiceWarDiff
{
    public IReadOnlyList<string> ToDeclare { get; }
    public IReadOnlyList<string> ToPeace { get; }

    public ServiceWarDiff(IReadOnlyList<string> toDeclare, IReadOnlyList<string> toPeace)
    {
        ToDeclare = toDeclare;
        ToPeace = toPeace;
    }
}
