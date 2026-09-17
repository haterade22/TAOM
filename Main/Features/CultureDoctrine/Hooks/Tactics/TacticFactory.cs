using System;
using System.Collections.Generic;
using TAOM.Features.CultureDoctrine.Domain;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.CultureDoctrine.Hooks.Tactics;

/// <summary>
/// The one switch from a doctrine id to an engine object. A new tactic is one enum member, one
/// case here and its class; nothing else in the feature names a concrete tactic type.
/// Construction happens on the main thread in <c>EarlyStart</c>; the objects then live on the
/// team-AI tick (usually the async AI thread), which is why they take only immutable values.
/// </summary>
public static class TacticFactory
{
    /// <summary>Throws for an enum member with no class: the caller builds the whole list before
    /// touching the team, so the throw leaves vanilla's list intact and lands in the mission
    /// log, and <c>DoctrineSwitchInvariantTests</c> pins the switch against the enum.</summary>
    public static TacticComponent Create(RosterEntry entry, Team team)
    {
        var m = entry.Multiplier;
        switch (entry.Tactic)
        {
            case DoctrineTactic.Charge: return new TacticCharge(team, m);
            case DoctrineTactic.FullScaleAttack: return new TacticFullScaleAttack(team, m);
            case DoctrineTactic.DefensiveEngagement: return new TacticDefensiveEngagement(team, m);
            case DoctrineTactic.DefensiveLine: return new TacticDefensiveLine(team, m);
            case DoctrineTactic.DefensiveRing: return new TacticDefensiveRing(team, m);
            case DoctrineTactic.FrontalCavalryCharge: return new TacticFrontalCavalryCharge(team, m);
            case DoctrineTactic.RangedHarrassmentOffensive: return new TacticRangedHarrassmentOffensive(team, m);
            case DoctrineTactic.HoldChokePoint: return new TacticHoldChokePoint(team, m);
            case DoctrineTactic.CoordinatedRetreat: return new TacticCoordinatedRetreat(team, m);
            case DoctrineTactic.ShieldWall: return new TaomTacticShieldWall(team, m);
            case DoctrineTactic.InfantryMass: return new TaomTacticInfantryMass(team, m);
            case DoctrineTactic.CavalryDominance: return new TaomTacticCavalryDominance(team, m);
            case DoctrineTactic.ArcherRing: return new TaomTacticArcherRing(team, m);
            default: throw new ArgumentOutOfRangeException(nameof(entry), entry.Tactic, "no tactic class for this doctrine id");
        }
    }

    /// <summary>Every tactic the roster names, in roster order. Charge is first in every roster,
    /// so the list is never empty and its first entry is the engine's fallback.</summary>
    public static List<TacticComponent> CreateAll(IReadOnlyList<RosterEntry> roster, Team team)
    {
        var tactics = new List<TacticComponent>(roster.Count);
        for (var i = 0; i < roster.Count; i++)
            tactics.Add(Create(roster[i], team));
        return tactics;
    }
}
