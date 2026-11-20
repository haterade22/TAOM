using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace TAOM.Features.Elephant;

/// <summary>One seat of the watched beast: the arrows its archer has loosed (0 for an empty seat), and the seat's rescue teleports.</summary>
internal readonly record struct CrewSeatReading(int Shots, int Rescues);

/// <summary>One read of the watched beast: whether it lives, its seats in the machine's order, and how far it is from where the trial first saw it.</summary>
internal sealed record CrewReading(bool BeastAlive, IReadOnlyList<CrewSeatReading> Seats, float Moved);

/// <summary>
/// The times and the pass rule of a crew step of the command trial (#1503). The rule is the one of #1482: a seat that
/// needed a rescue teleport did not hold its archer, and only the engine's missile count proves a shot.
/// </summary>
internal static class CrewTrialRules
{
    /// <summary>Real seconds the step waits for a beast with a full crew: the crew is spawned from the mission tick after the rider's build.</summary>
    internal const float CrewWaitSeconds = 20f;

    /// <summary>The shortest watch, so that every picture is taken even when every archer shot at once.</summary>
    internal const float MinWatchSeconds = 30f;

    internal const float WatchLimitSeconds = 150f;

    /// <summary>How far the beast must go from where the trial first saw it. A crew on a beast that stood still does not prove the moving case.</summary>
    internal const float MovedMetres = 10f;

    /// <summary>The camera's distance from the kept archer for the wide pictures. The keep starts at 16 m (<c>SeatOrbit.StartDistance</c>), which shows the archer and not the beast.</summary>
    internal const float WideMetres = 28f;

    /// <summary>Real seconds the step may turn the wheel to reach <see cref="WideMetres"/>; after them it goes on with the distance it has.</summary>
    internal const float ZoomSeconds = 2f;

    /// <summary>
    /// Real seconds after the keep at which a picture is taken. The first is close, the others are after the zoom. The first waits
    /// 8 s: the armies start among trees on <c>battle_terrain_a</c>, and a picture at 3 s showed only a tree (run 20261120-132358).
    /// </summary>
    internal static readonly IReadOnlyList<float> PictureSeconds = new[] { 8f, 16f, 25f };

    /// <summary>The runner turns <c>hud_*.bmp</c> into a PNG (tools/run_fords_autorun.py), so the name starts with it.</summary>
    internal static string PictureName(string step, int number) =>
        "hud_" + step + "_" + number.ToString(CultureInfo.InvariantCulture) + ".bmp";

    internal static bool EveryoneShot(IReadOnlyList<int> shots) => shots.Count > 0 && shots.All(s => s > 0);

    /// <summary>The watch ends when the beast is gone, at the limit, or when every archer shot and the shortest watch has passed. A time that is no number ends it.</summary>
    internal static bool WatchIsOver(float watched, bool everyoneShot, bool beastAlive)
    {
        if (!beastAlive || !(watched < WatchLimitSeconds))
            return true;
        return everyoneShot && watched >= MinWatchSeconds;
    }

    /// <summary>
    /// <paramref name="shots"/> and <paramref name="rescues"/> are the highest values seen for each seat over the watch, so an archer who shot and then died still counts.
    /// The step watches only a beast whose every seat held an archer when it was chosen, so a seat with no shot is an archer who did not shoot.
    /// </summary>
    internal static bool Passes(int seats, IReadOnlyList<int> shots, int rescues, float moved) =>
        seats > 0 && shots.Count == seats && EveryoneShot(shots) && rescues == 0 && moved >= MovedMetres;

    internal static string Facts(int seats, IReadOnlyList<int> shots, int rescues, float moved, bool beastAlive, float watched, int pictures) =>
        "seats=" + Whole(seats)
        + " shots=" + (shots.Count > 0 ? string.Join(",", shots.Select(Whole)) : "none")
        + " rescues=" + Whole(rescues) + " moved=" + Tenth(moved) + " beast=" + (beastAlive ? "alive" : "gone")
        + " watched=" + Tenth(watched) + " pictures=" + Whole(pictures);

    private static string Whole(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static string Tenth(float value) => value.ToString("F1", CultureInfo.InvariantCulture);
}
