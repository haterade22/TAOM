using System;
using System.Collections.Generic;
using System.Text;

namespace TAOM.Features.BattleLoadDiagnostics;

/// <summary>
/// Compacts a captured managed call stack into one log token naming which engine method built an
/// agent. Pure — the <c>StackTrace</c> capture itself lives at the patch boundary.
///
/// It exists because location characters turn up in a <c>TournamentFight</c> mission that has no
/// code path to spawn them: the mission's 13 behaviors contain no <c>MissionAgentHandler</c>, and
/// <c>FightTournamentGame.GetParticipantCharacters</c> selects only heroes, tier 3-5 garrison
/// troops and <c>BasicTroop</c> upgrade targets. The loadout dump cannot say who spawned an agent;
/// the caller's name can.
///
/// Input frames must be FULLY QUALIFIED (<c>Namespace.Type.Method</c>). The first cut of this class
/// filtered on namespaces while the caller passed short type names, so nothing was ever skipped and
/// every slot went to our own prefix plus Harmony's wrappers — the token shipped useless.
/// </summary>
public static class SpawnOriginFormatter
{
    private const string Separator = " <- ";

    /// <summary>
    /// Frames that sit between the engine and us on every stack. Left in, they push the frame that
    /// actually matters past <paramref name="maxFrames"/>. Matched against the FULL name.
    /// </summary>
    private static readonly string[] NoiseMarkers =
    {
        "TAOM.Features.BattleLoadDiagnostics",
        "HarmonyLib",
    };

    /// <summary>
    /// Joins the first <paramref name="maxFrames"/> meaningful frames, outermost-last, each
    /// shortened to <c>Type.Method</c>. Returns an empty string when nothing usable remains, so the
    /// caller omits the token rather than emitting a bare <c>from=</c>.
    /// </summary>
    public static string Format(IReadOnlyList<string?>? frames, int maxFrames)
    {
        if (frames == null || maxFrames <= 0) return string.Empty;

        var sb = new StringBuilder();
        string? previous = null;
        int taken = 0;

        for (int i = 0; i < frames.Count && taken < maxFrames; i++)
        {
            var frame = frames[i];
            if (string.IsNullOrWhiteSpace(frame)) continue;
            if (IsNoise(frame!)) continue;

            var shortened = Shorten(StripHarmonyWrapperSuffix(frame!.Trim()));
            if (shortened.Length == 0) continue;

            // A Harmony wrapper and the method it stands in for both appear on the stack; once
            // normalised they are the same frame. Log it once.
            if (string.Equals(shortened, previous, StringComparison.Ordinal)) continue;

            if (taken > 0) sb.Append(Separator);
            sb.Append(shortened);
            previous = shortened;
            taken++;
        }

        return sb.ToString();
    }

    private static bool IsNoise(string frame)
    {
        foreach (var marker in NoiseMarkers)
        {
            if (frame.IndexOf(marker, StringComparison.Ordinal) >= 0) return true;
        }
        return false;
    }

    /// <summary>
    /// Harmony's generated wrapper REPLACES the original frame on the stack and is named
    /// <c>&lt;original&gt;_Patch&lt;n&gt;</c>. Dropping such a frame would lose the very method we
    /// are trying to name, so the suffix is stripped and the frame kept.
    /// </summary>
    private static string StripHarmonyWrapperSuffix(string frame)
    {
        int marker = frame.LastIndexOf("_Patch", StringComparison.Ordinal);
        if (marker <= 0) return frame;

        for (int i = marker + "_Patch".Length; i < frame.Length; i++)
        {
            if (!char.IsDigit(frame[i])) return frame;
        }
        // Nothing but digits after the marker — and at least one of them.
        return marker + "_Patch".Length < frame.Length ? frame.Substring(0, marker) : frame;
    }

    /// <summary>Last two dot-separated segments: <c>Type.Method</c>. Unqualified names pass through.</summary>
    private static string Shorten(string frame)
    {
        int last = frame.LastIndexOf('.');
        if (last <= 0) return frame;

        int penultimate = frame.LastIndexOf('.', last - 1);
        return penultimate < 0 ? frame : frame.Substring(penultimate + 1);
    }
}
