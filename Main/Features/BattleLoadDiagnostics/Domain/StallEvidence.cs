using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace TAOM.Features.BattleLoadDiagnostics.Domain;

public enum StallEvidenceKind { None, Compiling, Stuck, RenderWaitUnknown }

/// <summary>
/// What the previous session's load was doing when the game closed, read from the prior log's tail.
/// Scope: the lines after the last <c>MissionInitialize</c>. The last <c>[BattleLoad] seq=</c> line gives
/// the phase and the shader count; anchored on that prefix because a [Freeze] line's status tail also
/// carries <c>phase=</c>. A [Freeze] line counts only when no phase line follows it, so a stall earlier in
/// the same load cannot speak for the wait the load ended in. Pure: no engine types.
/// </summary>
public sealed class StallEvidence
{
    private const string PhasePrefix = "[BattleLoad] seq=";
    private const int StuckAfterSeconds = 60;

    private static readonly Regex PhaseRe = new Regex(@"\bphase=(\S+)", RegexOptions.CultureInvariant);
    private static readonly Regex ShadersRe = new Regex(@"\bshaders=(\d+)", RegexOptions.CultureInvariant);
    private static readonly Regex FreezeRe = new Regex(@"\[Freeze\] main thread silent (\d+)s sample#", RegexOptions.CultureInvariant);
    private static readonly Regex RglRe = new Regex(@"\brglCompiles=(\d+)", RegexOptions.CultureInvariant);

    public string LastPhase { get; private set; } = string.Empty;
    public int? LastShaders { get; private set; }
    public int? FreezeSilentSeconds { get; private set; }
    public int? FreezeRglCompiles { get; private set; }
    public bool FreezeRglUnavailable { get; private set; }

    public static StallEvidence? Parse(string tailText)
    {
        StallEvidence? evidence = null;
        foreach (string line in (tailText ?? string.Empty).Split('\n'))
        {
            if (line.IndexOf(PhasePrefix, StringComparison.Ordinal) >= 0)
            {
                var phase = PhaseRe.Match(line);
                if (!phase.Success) continue;
                string name = phase.Groups[1].Value;
                var shaders = ShadersRe.Match(line);
                evidence = new StallEvidence
                {
                    LastPhase = name,
                    LastShaders = shaders.Success ? ToInt(shaders.Groups[1].Value) : (int?)null,
                };
                continue;
            }

            if (evidence != null) evidence.TakeFreezeLine(line);
        }

        return evidence;
    }

    public StallEvidenceKind Classify()
    {
        if (LastPhase != "WaitingForRender") return StallEvidenceKind.None;
        if (FreezeRglCompiles > 0) return StallEvidenceKind.Compiling;
        if (FreezeRglCompiles == 0 && FreezeSilentSeconds >= StuckAfterSeconds && !FreezeRglUnavailable)
            return StallEvidenceKind.Stuck;
        return StallEvidenceKind.RenderWaitUnknown;
    }

    // The sample with the largest silence speaks for the episode. Tokens come only from the text
    // before " last ": the status tail repeats shaders= and phase=.
    private void TakeFreezeLine(string line)
    {
        var freeze = FreezeRe.Match(line);
        if (!freeze.Success) return;
        int silent = ToInt(freeze.Groups[1].Value);
        if (FreezeSilentSeconds.HasValue && silent <= FreezeSilentSeconds.Value) return;

        int tail = line.IndexOf(" last ", StringComparison.Ordinal);
        string head = tail >= 0 ? line.Substring(0, tail) : line;
        var rgl = RglRe.Match(head);
        FreezeSilentSeconds = silent;
        FreezeRglCompiles = rgl.Success ? ToInt(rgl.Groups[1].Value) : (int?)null;
        FreezeRglUnavailable = head.IndexOf("rgl=unavailable", StringComparison.Ordinal) >= 0;
    }

    private static int ToInt(string digits)
        => int.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : 0;
}
