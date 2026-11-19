using System.Globalization;
using TAOM.Core.Logging;

namespace TAOM.Features.Elephant;

/// <summary>
/// The [Check] lines a crew seat writes when its archer leaves it (#1482), read by tools/ingame_verdict.py through
/// tools/ingame_checks/1482.toml. Two facts, because the first fix proved one and broke the other:
///
/// Held: the seat holds its archer with the engine's position lock and attachment. The rescue teleport is the sign
/// that the hold failed, so a seat passes when it needed none.
///
/// Shot: the archer loosed at least one arrow. A seat with no shot writes a skip, never a fail: an archer with no
/// enemy in range has none either. Only this count proves shooting; the draw-progress fields of the status line
/// count an aborted draw the same as a shot.
///
/// One id for each beast: a battle with a mumakil and no elephant must not read as proof of the howdah.
/// </summary>
internal static class CrewSeatChecks
{
    public const string MumakilHeldId = "taom.crew-seats.mumakil-held";
    public const string HowdahHeldId = "taom.crew-seats.howdah-held";
    public const string MumakilShotId = "taom.crew-seats.mumakil-shot";
    public const string HowdahShotId = "taom.crew-seats.howdah-shot";

    public static bool Held(int rescues) => rescues == 0;

    public static bool Shot(int shots) => shots > 0;

    public static string HeldDetail(string seatTag, int rescues) =>
        string.Format(CultureInfo.InvariantCulture, "seat={0} rescues={1}", Key(seatTag), rescues);

    public static string ShotDetail(string seatTag, int shots) =>
        string.Format(CultureInfo.InvariantCulture, "seat={0} shots={1}", Key(seatTag), shots);

    /// <summary>Writes both lines. Main thread only (IOutcomeLog): every release path of a seat is on it.</summary>
    public static void Report(string heldId, string shotId, string seatTag, int rescues, int shots)
    {
        string key = Key(seatTag);
        if (Held(rescues)) Outcomes.Pass(heldId, key, HeldDetail(seatTag, rescues));
        else Outcomes.Fail(heldId, key, HeldDetail(seatTag, rescues));

        if (Shot(shots)) Outcomes.Pass(shotId, key, ShotDetail(seatTag, shots));
        else Outcomes.Skip(shotId, key, ShotDetail(seatTag, shots));
    }

    private static string Key(string seatTag) =>
        string.IsNullOrWhiteSpace(seatTag) ? "unknown" : seatTag.Replace(' ', '_');
}
