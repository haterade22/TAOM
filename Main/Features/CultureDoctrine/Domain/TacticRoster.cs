using System.Collections.Generic;

namespace TAOM.Features.CultureDoctrine.Domain;

/// <summary>One tactic the team will be given, with the weight multiplier its wrapper applies.</summary>
public readonly struct RosterEntry
{
    public RosterEntry(DoctrineTactic tactic, float multiplier)
    {
        Tactic = tactic;
        Multiplier = multiplier;
    }

    public DoctrineTactic Tactic { get; }
    public float Multiplier { get; }
}

/// <summary>
/// The list a team actually registers: the doctrine's rows filtered by side and by the side's
/// Tactics skill, one instance per tactic (first row wins), with <c>Charge</c> guaranteed.
///
/// <c>Charge</c> is load-bearing in the engine. <c>TeamAIComponent.MakeDecision</c> searches the
/// list for a <c>TacticCharge</c> when no enemy formation remains and otherwise falls back to the
/// FIRST entry (`TeamAIComponent.cs:276-298`), and <c>MaxBy</c> over an empty list has nothing to
/// return; so a missing Charge is prepended at neutral weight in vanilla's own position. One
/// instance per tactic because the engine's list is unlocked and read every 5 s on the AI thread:
/// two instances of one tactic would race each other's formation fields.
///
/// <paramref name="ensure"/> names tactics the team must carry at neutral weight whatever the
/// doctrine says: vanilla's <c>MissionCaravanOrVillagerTacticsHandler</c> gives a caravan or
/// villager side a <c>TacticDefensiveLine</c> regardless of skill, and the swap must not lose it.
/// </summary>
public static class TacticRoster
{
    public static IReadOnlyList<RosterEntry> Build(Doctrine doctrine, int tacticsSkill, DoctrineSide side) =>
        Build(doctrine, tacticsSkill, side, null);

    public static IReadOnlyList<RosterEntry> Build(Doctrine doctrine, int tacticsSkill, DoctrineSide side, IReadOnlyList<DoctrineTactic>? ensure)
    {
        var roster = new List<RosterEntry>(doctrine.Tactics.Count + 1);
        var seen = new HashSet<DoctrineTactic>();
        foreach (var entry in doctrine.Tactics)
        {
            if (entry.Side != DoctrineSide.Any && entry.Side != side)
                continue;
            if (tacticsSkill < entry.MinTactics)
                continue;
            if (!seen.Add(entry.Tactic))
                continue;
            roster.Add(new RosterEntry(entry.Tactic, entry.Multiplier));
        }
        if (ensure != null)
            for (var i = 0; i < ensure.Count; i++)
                if (seen.Add(ensure[i]))
                    roster.Add(new RosterEntry(ensure[i], 1f));
        if (!seen.Contains(DoctrineTactic.Charge))
            roster.Insert(0, new RosterEntry(DoctrineTactic.Charge, 1f));
        return roster;
    }
}
