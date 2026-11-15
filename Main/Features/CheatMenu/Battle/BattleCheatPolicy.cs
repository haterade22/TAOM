using System;
using System.Collections.Generic;
using System.Linq;

namespace TAOM.Features.CheatMenu.Battle;

/// <summary>The battle cheat menu's entries, in list order (docs/features/cheat-menu.md, "Battle").</summary>
public enum BattleCheatAction
{
    HealSelf,
    HealSide,
    Rout,
    Kill,
    SpawnAllies,
    SpawnEnemies,
}

/// <summary>The mission a battle cheat is judged on, read by the battle adapter in one go.</summary>
/// <param name="MissionRunning">A mission exists.</param>
/// <param name="Ending">The mission has ended or is ending, or a side has retreated.</param>
/// <param name="HasPlayerTeam">The mission has a player team (the player's side to heal, copy or fight).</param>
public sealed record BattleCheatState(bool MissionRunning, bool Ending, bool HasPlayerTeam);

/// <summary>One active soldier (no mount) of a side: its character's id, and whether that character is a hero.</summary>
public sealed record BattleTroop(string Id, bool IsHero);

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
}
