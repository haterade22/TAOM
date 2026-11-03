using TaleWorlds.MountAndBlade;
using TAOM.Core.Logging;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Adapters;

/// <summary>
/// Gives the mission this frame's clock, and logs each change of speed. Fast forward stops once the mission has ended, as the engine itself does, or the two would undo each other
/// every frame. The fixed step is read only by MissionState.TickMission, and a paused mission ignores it.
/// </summary>
internal sealed class FordsMissionSpeed
{
    private readonly IModLogger? _logger;
    private int _logged = 1;

    internal FordsMissionSpeed(IModLogger? logger) => _logger = logger;

    internal void Apply(Mission mission, FordsGameSpeed speed, float realDt)
    {
        Set(mission, speed.Frame(realDt));
        if (speed.Current == _logged)
            return;

        _logged = speed.Current;
        _logger?.LogInfo($"[FordsOfWar][Speed] speed={_logged} mt={mission.CurrentTime:0.0}");
    }

    internal static void Stop(Mission mission) => Set(mission, (false, 0f));

    private static void Set(Mission mission, (bool FastForward, float FixedStep) frame)
    {
        var fastForward = frame.FastForward && !mission.MissionEnded;
        if (mission.IsFastForward != fastForward)
            mission.SetFastForwardingFromUI(fastForward);

        mission.FixedDeltaTimeMode = frame.FixedStep > 0f;
        mission.FixedDeltaTime = frame.FixedStep;
    }
}
