using System.Collections.Generic;
using System.Linq;
using TAOM.Features.SeatOfSeeing.Domain;

namespace TAOM.Features.Spider;

/// <summary>
/// The spider row of the command trial's step table (#1541): one custom battle step that fields spider riders, holds them, charges
/// them, holds them again and reports their speeds. It is known by name and is not in the list a run with no step list arms,
/// because it puts spiders on the field and the other rows were proved in a battle without them.
/// </summary>
internal static class SpiderTrialRows
{
    internal const string Gaits = "spider_gaits";

    /// <summary>The budget covers the wait for the riders, the three phases, the keep and the verdict, in real seconds.</summary>
    internal static readonly IReadOnlyList<SeatTrialStep> Rows = new[]
    {
        new SeatTrialStep(Gaits, new[] { "enter" }, 240f),
    };

    /// <summary>The riders the step adds to the player's army (<c>fords_of_war/fords_troops.xml</c>). The spider is each rider's mount.</summary>
    internal static readonly IReadOnlyList<SeatTrialGroup> Riders = new[]
    {
        new SeatTrialGroup("fow_goblin_spider_rider", 12),
    };

    /// <summary>
    /// The first chosen step that is not <c>enter</c>, when the list also holds the spider step; null when the list may run. The spider
    /// battle has twelve riders in the cavalry company and the crew battle's enemy, and the other rows were proved without them.
    /// </summary>
    internal static string? StepThatCannotShareASpiderBattle(IReadOnlyList<string> chosen)
    {
        if (!chosen.Any(IsSpiderStep))
            return null;
        return chosen.FirstOrDefault(name => !IsSpiderStep(name) && name != "enter");
    }

    private static bool IsSpiderStep(string name) => Rows.Any(row => row.Name == name);

    /// <summary>True when the step list names the spider step.</summary>
    internal static bool NamedIn(string? steps) =>
        (steps ?? "").Split(',').Select(SeatTrialSteps.NameOf).Any(IsSpiderStep);
}
