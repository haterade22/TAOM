using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace TAOM.Features.TrollBruteForce;

/// <summary>
/// The <c>[TrollClips]</c> line: the first time in a mission that a troll Monster enters an action, the clip the
/// troll's action set binds to that action, and whether it is one of TAOM's troll clips or a vanilla one. It proves
/// the troll entered the action and names the bound clip. It cannot prove the clip's keyframes play:
/// <c>MBActionSet.GetActionAnimationName</c> takes no agent and returns the set's static binding
/// (docs/features/troll-race.md "The swing CTD"). One line per Monster and action, so a battle writes a few dozen
/// lines, not one per swing. Pure: <see cref="TrollClipTrace"/> reads the engine.
/// </summary>
public sealed class TrollClipLog
{
    // The release and blocked codes, quick or not, the family the engine keys its melee attack table on
    // (tools/bind_hill_troll_action_set.py MELEE_TABLE). The pattern also matches _balanced and ranged codes
    // (act_release_bow), which vanilla binds to clips with no table row, so the tag says "family", not "table".
    private static readonly Regex MeleeTable = new(@"^act_(quick_)?(release|blocked)_");

    private readonly HashSet<string> _seen = new(StringComparer.Ordinal);

    /// <summary>The log line the first time <paramref name="monster"/> enters <paramref name="action"/> this mission,
    /// else null. <paramref name="clip"/> is the clip the action is bound to in the agent's action set.</summary>
    public string? FirstPlay(string? monster, string? action, string? clip)
    {
        if (string.IsNullOrEmpty(action) || action == "act_none") return null;
        if (!_seen.Add((monster ?? "?") + "|" + action)) return null;

        string kind = string.IsNullOrEmpty(clip) ? "no clip" : IsTrollClip(clip!) ? "troll clip" : "vanilla clip";
        string table = MeleeTable.IsMatch(action) ? ", melee-table family" : "";
        return $"[TrollClips] {monster ?? "?"}: {action} -> {(string.IsNullOrEmpty(clip) ? "-" : clip)} ({kind}{table})";
    }

    public void Clear() => _seen.Clear();

    // TAOM's troll clips only: vanilla also ships clips named anim_* (anim_cutscene_break_chains_short).
    private static bool IsTrollClip(string clip) =>
        clip.StartsWith("anim_hill_troll_", StringComparison.Ordinal) ||
        clip.StartsWith("anim_troll_", StringComparison.Ordinal);
}
