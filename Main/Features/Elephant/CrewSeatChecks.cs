using System.Globalization;
using TAOM.Core.Logging;

namespace TAOM.Features.Elephant;

/// <summary>
/// The [Check] line a crew seat writes when its archer leaves it (#1482), read by tools/ingame_verdict.py through
/// tools/ingame_checks/1482.toml. A seat holds its archer with the engine's position lock and attachment; the
/// rescue teleport is the sign that the hold failed, so a seat passes when it needed none. One id for each beast:
/// a battle with a mumakil and no elephant must not read as proof of the howdah, which has no navmesh under it.
/// </summary>
internal static class CrewSeatChecks
{
    public const string MumakilHeldId = "taom.crew-seats.mumakil-held";
    public const string HowdahHeldId = "taom.crew-seats.howdah-held";

    public static bool Held(int rescues) => rescues == 0;

    public static string Detail(string seatTag, int rescues) =>
        string.Format(CultureInfo.InvariantCulture, "seat={0} rescues={1}", Key(seatTag), rescues);

    /// <summary>Writes the line. Main thread only (IOutcomeLog): every release path of a seat is on it.</summary>
    public static void Report(string id, string seatTag, int rescues)
    {
        if (Held(rescues)) Outcomes.Pass(id, Key(seatTag), Detail(seatTag, rescues));
        else Outcomes.Fail(id, Key(seatTag), Detail(seatTag, rescues));
    }

    private static string Key(string seatTag) =>
        string.IsNullOrWhiteSpace(seatTag) ? "unknown" : seatTag.Replace(' ', '_');
}
