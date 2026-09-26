using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace TAOM.Features.TrollBruteForce;

/// <summary>
/// The <c>[TrollClips]</c> line: the first time in a mission that a troll Monster plays an action, which clip that
/// action played and whether it is one of TAOM's troll clips or a vanilla one. It shows in game what a crash-free
/// battle cannot: whether the hill troll's self-keyed swing clips (bound to the release and blocked codes on
/// 2026-09-26; docs/features/troll-race.md "The swing CTD") actually play. One line per Monster and action, so a
/// battle writes a few dozen lines, not one per swing. Pure: <see cref="TrollClipTrace"/> reads the engine.
/// </summary>
public sealed class TrollClipLog
{
    // The codes the engine looks up in its melee attack table (tools/bind_hill_troll_action_set.py MELEE_TABLE).
    private static readonly Regex MeleeTable = new(@"^act_(quick_)?(release|blocked)_", RegexOptions.Compiled);

    private readonly HashSet<string> _seen = new(StringComparer.Ordinal);

    /// <summary>The log line the first time <paramref name="monster"/> plays <paramref name="action"/> this mission,
    /// else null. <paramref name="clip"/> is the clip the action is bound to in the agent's action set.</summary>
    public string? FirstPlay(string? monster, string? action, string? clip)
    {
        if (string.IsNullOrEmpty(action) || action == "act_none") return null;
        if (!_seen.Add((monster ?? "?") + "|" + action)) return null;

        string kind = string.IsNullOrEmpty(clip) ? "no clip" : IsTrollClip(clip!) ? "troll clip" : "vanilla clip";
        string table = MeleeTable.IsMatch(action) ? ", melee table" : "";
        return $"[TrollClips] {monster ?? "?"}: {action} -> {(string.IsNullOrEmpty(clip) ? "-" : clip)} ({kind}{table})";
    }

    public void Clear() => _seen.Clear();

    // TAOM's troll clips only: vanilla also ships clips named anim_* (anim_cutscene_break_chains_short).
    private static bool IsTrollClip(string clip) =>
        clip.StartsWith("anim_hill_troll_", StringComparison.Ordinal) ||
        clip.StartsWith("anim_troll_", StringComparison.Ordinal);
}
