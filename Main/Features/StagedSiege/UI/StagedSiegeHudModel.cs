using System;
using System.Text;
using TAOM.Features.StagedSiege.Domain;

namespace TAOM.Features.StagedSiege.UI;

/// <summary>What the player is asked to do at the current point.</summary>
public enum HudObjectiveKind
{
    /// <summary>The player attacks and the current point is not yet taken.</summary>
    Take,

    /// <summary>The player defends and the current point is not yet lost.</summary>
    Hold,

    /// <summary>The player attacked, D fell, and the defenders have gone back to the lord's hall.</summary>
    Hall,

    /// <summary>The player defended and D fell.</summary>
    Fallen,
}

/// <summary>The HUD's wording-free reading of one <see cref="StageSnapshot"/>.</summary>
public readonly struct StagedSiegeHudModel
{
    private const char Hall = 'E';

    private StagedSiegeHudModel(HudObjectiveKind kind, char letter, int percent, CaptureStatus status, string strip)
    {
        ObjectiveKind = kind;
        PointLetter = letter;
        ProgressPercent = percent;
        CaptureStatus = status;
        Strip = strip;
    }

    public HudObjectiveKind ObjectiveKind { get; }

    /// <summary>The current point's letter, A to D.</summary>
    public char PointLetter { get; }

    /// <summary>0 to 100; a non-finite progress reads as 0.</summary>
    public int ProgressPercent { get; }

    public CaptureStatus CaptureStatus { get; }

    /// <summary>
    /// The points in order: a captured one carries a plus, the active one is bracketed, for example <c>A+ B+ [C] D E</c>. The
    /// hall, E, appears only when the player attacks, and is bracketed once D has fallen.
    /// </summary>
    public string Strip { get; }

    public static StagedSiegeHudModel From(StageSnapshot s)
    {
        var attacker = s.PlayerSide == StagedSide.Attacker;
        var kind = s.Finished
            ? (attacker ? HudObjectiveKind.Hall : HudObjectiveKind.Fallen)
            : (attacker ? HudObjectiveKind.Take : HudObjectiveKind.Hold);
        var letter = (char)('A' + (int)s.Stage);
        var percent = float.IsNaN(s.Progress) || float.IsInfinity(s.Progress)
            ? 0
            : (int)Math.Round(Math.Max(0f, Math.Min(1f, s.Progress)) * 100f);
        return new StagedSiegeHudModel(kind, letter, percent, s.Status, BuildStrip(s, attacker));
    }

    private static string BuildStrip(StageSnapshot s, bool attacker)
    {
        var sb = new StringBuilder();
        for (var i = 0; i <= (int)StageId.D; i++)
        {
            if (i > 0)
                sb.Append(' ');
            var letter = (char)('A' + i);
            if ((s.CapturedMask & (1 << i)) != 0)
                sb.Append(letter).Append('+');
            else if (!s.Finished && (int)s.Stage == i)
                sb.Append('[').Append(letter).Append(']');
            else
                sb.Append(letter);
        }

        if (attacker)
            sb.Append(' ').Append(s.Finished ? "[" + Hall + "]" : Hall.ToString());
        return sb.ToString();
    }
}
