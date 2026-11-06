using System.Threading;
using TAOM.Features.LongDefeat.Domain;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.LongDefeat.Hooks;

/// <summary>
/// The one armed mission and the one published table that the Long Defeat's engine hooks read (<see cref="LongDefeatStatHooks"/> for stats and armour,
/// <see cref="LongDefeatCombatHooks"/> for blows). Both hooks used to copy the arm, disarm and publish lifecycle and kept the attackers' damage factor twice; they now read
/// <see cref="Armed"/> and <see cref="Table"/> from here, so a publish reaches both at once and the factor lives in <see cref="LdStatTable.AttackerDamage"/> alone. The engine's agent threads read
/// both, so each is a volatile reference and the blow count an <see cref="Interlocked"/> long. Armed from the mission logic's <c>AfterStart</c> and disarmed on both exits, like
/// <see cref="LongDefeatMorale"/>. A published table must never be changed afterwards.
/// </summary>
internal static class LongDefeatHookState
{
    private static volatile Mission? _armed;
    private static volatile LdStatTable? _table;
    private static long _raised;

    /// <summary>The mission the hooks act in, or null when none is armed.</summary>
    internal static Mission? Armed => _armed;

    /// <summary>What the wave last published, or null before the first publish.</summary>
    internal static LdStatTable? Table => _table;

    /// <summary>Attackers' blows the wave's damage factor raised since the table was last published, so a log line can show the seam ran in this wave.</summary>
    internal static long Raised => Interlocked.Read(ref _raised);

    internal static void CountRaised() => Interlocked.Increment(ref _raised);

    internal static void Arm(Mission? mission)
    {
        _table = null;
        Interlocked.Exchange(ref _raised, 0);
        _armed = mission;
    }

    internal static void Disarm(Mission? mission)
    {
        if (mission == null || !ReferenceEquals(_armed, mission))
            return;

        _armed = null;
        _table = null;
    }

    /// <summary>Takes the wave's table for both hooks; ignored from any mission but the armed one. Restarts the blow count.</summary>
    internal static void Publish(Mission? mission, LdStatTable table)
    {
        if (mission == null || !ReferenceEquals(_armed, mission))
            return;

        Interlocked.Exchange(ref _raised, 0);
        _table = table;
    }
}
