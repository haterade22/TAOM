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

    /// <summary>D fell and no hall follows: the player defended, or attacked in a Custom Battle siege and won.</summary>
    Fallen,

    /// <summary>The lord's hall fight: the attackers hold the player's side and point E is not yet taken.</summary>
    TakeHall,

    /// <summary>The lord's hall fight: point E has been taken.</summary>
    HallTaken,
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
    /// hall, E, appears only when it follows (a campaign siege, player attacking), and is bracketed once D has fallen.
    /// </summary>
    public string Strip { get; }

    public static StagedSiegeHudModel From(StageSnapshot s)
    {
        var attacker = s.PlayerSide == StagedSide.Attacker;
        var kind = s.Finished
            ? (s.HallFollows ? HudObjectiveKind.Hall : HudObjectiveKind.Fallen)
            : (attacker ? HudObjectiveKind.Take : HudObjectiveKind.Hold);
        var letter = (char)('A' + (int)s.Stage);
        return new StagedSiegeHudModel(kind, letter, ToPercent(s.Progress), s.Status, BuildStrip(s, s.HallFollows));
    }

    /// <summary>The lord's hall reading: every wall point is behind the player, E is the objective, and the strip says so.</summary>
    public static StagedSiegeHudModel FromHall(HallSnapshot s) =>
        new StagedSiegeHudModel(s.Finished ? HudObjectiveKind.HallTaken : HudObjectiveKind.TakeHall, Hall, ToPercent(s.Progress), s.Status,
            s.Finished ? "A+ B+ C+ D+ " + Hall + "+" : "A+ B+ C+ D+ [" + Hall + "]");

    private static int ToPercent(float progress) =>
        float.IsNaN(progress) || float.IsInfinity(progress) ? 0 : (int)Math.Round(Math.Max(0f, Math.Min(1f, progress)) * 100f);

    private static string BuildStrip(StageSnapshot s, bool hall)
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

        if (hall)
            sb.Append(' ').Append(s.Finished ? "[" + Hall + "]" : Hall.ToString());
        return sb.ToString();
    }
}
