using System;

namespace TAOM.Features.MissionDiagnostic;

// Builds the "which save, which hero, which in-game day" line that every crash report is
// correlated by. Pure -- the engine reads are passed in as delegates so the guard semantics are
// testable without a live campaign.
//
// Both halves are guarded INDEPENDENTLY and neither may propagate. Reading CampaignTime before
// Campaign.Models is up throws DivideByZeroException (CampaignTime.GetDayOfSeason divides by the
// static TimeTicksPerDay, which Campaign only assigns once the CampaignTimeModel is built), and a
// session snapshot taken that early must still report the hero rather than losing the whole line.
public static class CampaignContextFormatter
{
    public static string Describe(Func<string> readTime, Func<string> readHero) =>
        $"{Read(readTime, "time", "<time unavailable>")}, {Read(readHero, "hero", "<no hero>")}";

    private static string Read(Func<string> reader, string label, string fallback)
    {
        if (reader == null) return fallback;
        try
        {
            // Invoked ONCE -- these delegates wrap live engine getters, so a second call is both
            // wasted work and a second chance to throw halfway through formatting.
            var value = reader();
            // A getter can hand back null without failing; an empty half reads as a bug in the
            // logger rather than as missing game state, so it gets the same placeholder.
            return string.IsNullOrEmpty(value) ? fallback : value;
        }
        catch (Exception ex)
        {
            return $"<{label} read failed: {ex.GetType().Name}>";
        }
    }
}
