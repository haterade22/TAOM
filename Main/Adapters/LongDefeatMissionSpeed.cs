using TaleWorlds.MountAndBlade;
using TAOM.Core.Logging;
using TAOM.Features.LongDefeat;

namespace TAOM.Adapters;

/// <summary>
/// Gives the Long Defeat mission this frame's clock, by <see cref="LongDefeatSpeedFrame"/> (the Fords rule, but a fixed step at 10x while a person drives), and logs each change of speed. Fast forward stops once
/// the mission has ended, as the engine itself does, or the two would undo each other every frame. The fixed step is read only by MissionState.TickMission, and a paused
/// mission ignores it. Stop is called when the view finalizes, so no speed outlives the mission.
/// </summary>
internal sealed class LongDefeatMissionSpeed
{
    private readonly IModLogger? _logger;
    private int _logged = 1;

    internal LongDefeatMissionSpeed(IModLogger? logger) => _logger = logger;

    internal void Apply(Mission mission, int speed, float realDt)
    {
        var agent = mission.MainAgent;
        var personDriving = agent != null && agent.IsActive() && agent.IsPlayerControlled;
        Set(mission, LongDefeatSpeedFrame.For(speed, realDt, personDriving));
        if (speed == _logged)
            return;

        _logged = speed;
        _logger?.LogInfo($"[LongDefeat][Speed] speed={_logged} mt={mission.CurrentTime:0.0}");
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
