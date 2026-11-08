using System.Globalization;
using TaleWorlds.Core;

namespace TAOM.Features.DeadTargetReset;

/// <summary>
/// When to reset a soldier's target, and the mission-end count (#1096). The engine deletes a faded corpse 30 s after its fade
/// starts and sets every soldier's target on it to -1, but leaves the soldier's "target is valid" flag, so the next AI tick reads
/// agent slot -1 and the game crashes (TaleWorlds.Native 0x6B716E, docs/features/dead-target-reset.md). A death resets nobody's
/// target, so the sweep resets each target that is dead or unconscious, once a second, long before that delete.
/// </summary>
public sealed class DeadTargetSweep
{
    public const float IntervalSeconds = 1f;

    private float _sinceSweep = IntervalSeconds;
    private int _sweeps;
    private int _reset;

    /// <summary>
    /// A dead or unconscious target is stale. A routed one is not: a routed agent has left the battle, the engine's rout already
    /// reset every soldier that targeted it, and target selection never picks an agent that is not active. A deleted one has no
    /// target index left to read.
    /// </summary>
    public static bool IsStale(AgentState targetState) => targetState == AgentState.Killed || targetState == AgentState.Unconscious;

    /// <summary>True once a second of mission time; the first call sweeps at once. A NaN, infinite or non-positive frame adds nothing.</summary>
    public bool Due(float dt)
    {
        if (!(dt > 0f) || float.IsInfinity(dt))
            return false;

        _sinceSweep += dt;
        if (_sinceSweep < IntervalSeconds)
            return false;

        _sinceSweep = 0f;
        _sweeps++;
        return true;
    }

    public void Record(int reset) => _reset += reset;

    public string Summary() => string.Format(CultureInfo.InvariantCulture, "[DeadTarget] mission end: sweeps={0} reset={1}", _sweeps, _reset);
}
