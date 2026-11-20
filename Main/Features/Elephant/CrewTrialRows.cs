using System.Collections.Generic;
using System.Linq;
using TAOM.Features.SeatOfSeeing.Domain;

namespace TAOM.Features.Elephant;

/// <summary>Which beast a crew step of the command trial watches.</summary>
internal enum CrewBeast
{
    Mumakil,
    Howdah,
}

/// <summary>
/// The crew rows of the command trial's step table (#1503): one custom battle step for the mumakil's tower crew and one for
/// the war elephant's howdah crew. They are known by name and are not in the list a run with no step list arms, because
/// they put beasts on the field, and the other rows were proved in a battle without them.
/// </summary>
internal static class CrewTrialRows
{
    internal const string Mumakil = "crew_mumakil";
    internal const string Howdah = "crew_howdah";

    /// <summary>The budget covers the wait for the crew, the longest watch and the two key presses, in real seconds.</summary>
    internal static readonly IReadOnlyList<SeatTrialStep> Rows = new[]
    {
        new SeatTrialStep(Mumakil, new[] { "enter" }, 240f),
        new SeatTrialStep(Howdah, new[] { "enter" }, 240f),
    };

    /// <summary>The riders a crew step adds to the player's army (<c>fords_of_war/fords_troops.xml</c>). The beast is each rider's mount, and the crew is spawned on it.</summary>
    internal static readonly IReadOnlyList<SeatTrialGroup> Beasts = new[]
    {
        new SeatTrialGroup("fow_harad_mumakil_rider", 1),
        new SeatTrialGroup("fow_harad_elephant_rider", 2),
    };

    /// <summary>
    /// The enemy of a battle with a crew step: melee soldiers only, twice the trial's usual number. In the first run (2026-11-20,
    /// <c>E:\Temp\command-trial-runs\20261120-132358</c>) the usual 30 orc archers killed the mumakil 38 s after the charge and the
    /// battle was decided after 61 s, before the second crew step had its last picture.
    /// </summary>
    internal static readonly IReadOnlyList<SeatTrialGroup> Enemy = new[]
    {
        new SeatTrialGroup("fow_mordor_orc_fighter", 300),
    };

    /// <summary>
    /// The first chosen step that is neither a crew step nor <c>enter</c>, when the list also holds a crew step; null when the list
    /// may run. A crew battle has the beast riders in the cavalry company and another enemy, and the other steps were proved
    /// without them: <c>let_go_on_death</c> kills the smallest company that holds no hero, which here holds the three riders.
    /// </summary>
    internal static string? StepThatCannotShareACrewBattle(IReadOnlyList<string> chosen)
    {
        if (!chosen.Any(IsCrewStep))
            return null;
        return chosen.FirstOrDefault(name => !IsCrewStep(name) && name != "enter");
    }

    private static bool IsCrewStep(string name) => Rows.Any(row => row.Name == name);

    /// <summary>True when the step list names a crew step.</summary>
    internal static bool NamedIn(string? steps) =>
        (steps ?? "").Split(',').Select(SeatTrialSteps.NameOf).Any(IsCrewStep);
}
