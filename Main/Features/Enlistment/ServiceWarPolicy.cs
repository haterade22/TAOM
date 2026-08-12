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
/// with the defender's faction. So the player's clan accrues its own private war with every enemy
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
