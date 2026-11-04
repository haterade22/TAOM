using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TAOM.Core.Logging;

namespace TAOM.Features.LongDefeat.Hooks;

/// <summary>
/// Holds the attackers of a Long Defeat mission at the morale floor so a wave fights to the last soldier. The morale
/// model asks <see cref="HoldsAttacker"/> from the engine's agent threads, so it reads one volatile reference and
/// allocates nothing. Armed from the mission logic's <c>AfterStart</c>, disarmed on both exits.
/// </summary>
public static class LongDefeatMorale
{
    internal const string FiredId = "taom.long-defeat.morale-hold";

    private static volatile Mission? _armed;

    // The morale model asks only below morale 0.01, from agent threads: a counter is the any-thread-safe way to show the clause fired.
    internal static readonly OutcomeCounter Fired = Outcomes.Counter(FiredId);

    public static void Arm(Mission? mission) => _armed = mission;

    public static void Disarm(Mission? mission)
    {
        if (mission != null && ReferenceEquals(_armed, mission))
            _armed = null;
    }

    public static bool HoldsAttacker(Agent? agent)
    {
        var armed = _armed;
        if (armed == null || agent == null || !ReferenceEquals(agent.Mission, armed))
            return false;
        var team = agent.Team;
        if (team == null || team.Side != BattleSideEnum.Attacker)
            return false;

        Fired.Pass();
        return true;
    }
}
