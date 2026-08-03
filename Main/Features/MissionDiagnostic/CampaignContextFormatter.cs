using System;

namespace TAOM.Features.MissionDiagnostic;

// Builds the "which save, which hero, which in-game day" line that every crash report is
// correlated by. Pure -- the engine reads are passed in as delegates so the guard semantics are
// testable without a live campaign.
//
// Both halves are guarded INDEPENDENTLY and neither may propagate. Reading CampaignTime before
// Campaign.Models is up throws DivideByZeroException: CampaignTime.ToString() evaluates GetYear
// first, which integer-divides by the static TimeTicksPerYear, and Campaign only assigns the tick
// constants (from CampaignTimeModel) in CampaignTime.Initialize(). A session snapshot taken that
// early must still report the hero rather than losing the whole line.
//
// The window is structural, not a race: Campaign.OnInitialize calls GameManager.OnGameStart --
// which is what invokes TAOM's SubModule.OnGameStart -- three lines BEFORE CampaignTime.Initialize()
// (verified against installed v1.4.7). And on a save-game load Campaign.GameStarted is already true
// by then (SetLoadingParameters sets it), so the GameStarted guard does not close this window --
// which is exactly why the failure shows up on save-load and not on a new campaign.
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
