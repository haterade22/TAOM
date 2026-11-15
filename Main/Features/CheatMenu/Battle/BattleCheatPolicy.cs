using System;
using System.Collections.Generic;
using System.Linq;

namespace TAOM.Features.CheatMenu.Battle;

/// <summary>The battle cheat menu's entries, in list order (docs/features/cheat-menu.md, "Battle").</summary>
public enum BattleCheatAction
{
    HealSelf,
    HealSide,
    GodMode,
    Rout,
    Kill,
    SpawnAllies,
    SpawnEnemies,
}

/// <summary>The mission a battle cheat is judged on, read by the battle adapter in one go.</summary>
/// <param name="MissionRunning">A mission exists.</param>
/// <param name="Ending">The mission has ended or is ending, or a side has retreated.</param>
/// <param name="HasPlayerTeam">The mission has a player team (the player's side to heal, copy or fight).</param>
/// <param name="MissionKey">Tells one mission from the next (0 with no mission), so god mode does not outlive its mission. It is the mission's identity hash, which .NET does not guarantee unique.</param>
public sealed record BattleCheatState(bool MissionRunning, bool Ending, bool HasPlayerTeam, int MissionKey = 0);

/// <summary>One active soldier (no mount) of a side: its character's id, and whether that character is a hero.</summary>
public sealed record BattleTroop(string Id, bool IsHero);

/// <summary>An agent's mortality, as the engine's <c>Agent.MortalityState</c> (Mortal, Invulnerable, Immortal).</summary>
public enum AgentMortality
{
    Mortal,
    Invulnerable,
    Immortal,
}

/// <summary>
/// One active agent god mode looks at: its mission index, its mortality now, and an identity stamp of the agent object. The
/// engine gives a deleted agent's index to a new agent within the same mission (#592), so a held agent is read back only while
/// its index still holds the same object (the battle adapter compares the stamp).
/// </summary>
public sealed record GodModeAgent(int Index, AgentMortality Mortality, int Identity = 0);

/// <summary>What one god mode pass does: agents to make immortal, agents to set back to mortal, and the agents it holds after.</summary>
public sealed record GodModePlan(IReadOnlyList<GodModeAgent> Protect, IReadOnlyList<GodModeAgent> Release, IReadOnlyList<GodModeAgent> Held);

/// <summary>The pure rules of the battle cheat menu.</summary>
public static class BattleCheatPolicy
{
    /// <summary>A cheat acts only on a running battle that has a player team and has not ended or seen a retreat.</summary>
    public static bool IsBattleOn(BattleCheatState s) => s.MissionRunning && !s.Ending && s.HasPlayerTeam;

    /// <summary>
    /// How many copies of which troop make <paramref name="count"/> new soldiers: round-robin over the distinct non-hero
    /// characters of the side, in the order first seen. Heroes are never copied (that also keeps the custom dev and supporter
    /// characters out). Empty when the side has no non-hero soldier or the count is not positive.
    /// </summary>
    public static IReadOnlyList<(string Id, int Count)> PlanSpawn(IEnumerable<BattleTroop> troops, int count)
    {
        var ids = troops
            .Where(t => t != null && !t.IsHero && !string.IsNullOrEmpty(t.Id))
            .Select(t => t.Id)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (ids.Count == 0 || count < 1) return Array.Empty<(string, int)>();

        var counts = new int[ids.Count];
        for (var i = 0; i < count; i++) counts[i % ids.Count]++;
        return ids.Select((id, i) => (id, counts[i])).Where(p => p.Item2 > 0).ToList();
    }

    /// <summary>
    /// One god mode pass. <paramref name="targets"/> are the main agent and the mount it rides now; <paramref name="held"/> are
    /// the agents god mode made immortal earlier, with their mortality now (an agent no longer active, or whose index now holds
    /// another agent, is left out). With god
    /// mode on, every Mortal target is protected. A held agent still Immortal stays held while it is a target and is released
    /// (back to Mortal) when it is not: a horse the player left, or every held agent once god mode is off. A held agent whose
    /// mortality something else changed is dropped and not touched, and a target the game itself made Invulnerable or Immortal
    /// is never taken over, so the game's own flags are never undone.
    /// </summary>
    public static GodModePlan PlanGodMode(bool on, IEnumerable<GodModeAgent> targets, IEnumerable<GodModeAgent> held)
    {
        var targetList = on ? targets.Where(t => t != null).ToList() : new List<GodModeAgent>();
        var targetIds = new HashSet<int>(targetList.Select(t => t.Index));
        var protect = targetList.Where(t => t.Mortality == AgentMortality.Mortal).GroupBy(t => t.Index).Select(g => g.First()).ToList();
        var keep = new List<GodModeAgent>();
        var release = new List<GodModeAgent>();
        foreach (var h in held.Where(h => h != null && h.Mortality == AgentMortality.Immortal))
        {
            if (targetIds.Contains(h.Index)) keep.Add(h);
            else release.Add(h);
        }
        var heldAfter = keep.Concat(protect).GroupBy(a => a.Index).Select(g => g.First()).ToList();
        return new GodModePlan(protect, release, heldAfter);
    }
}
