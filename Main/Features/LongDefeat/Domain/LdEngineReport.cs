using System.Collections.Generic;

namespace TAOM.Features.LongDefeat.Domain;

/// <summary>One siege engine as the hammer left it: its kind (<c>ram</c> or <c>tower</c>), the vanilla hit points of its type, and its maximum and current hit points now.</summary>
internal readonly record struct LdEngineReading(string Kind, float Vanilla, float MaxHitPoints, float HitPoints);

/// <summary>
/// What writing the engines' hit points left behind, read after the write: each live ram and tower, and how many visible gates stand or lie destroyed (the hammer does little once the gate
/// has fallen, and nothing repairs it before Phase 3, so the check logs the state beside the proof of the write). The gates come from every mission object, since a destroyed gate leaves the
/// active list (<c>Mission.cs:3060</c>); they show as standing, destroyed or none.
/// </summary>
internal sealed record LdEngineReport(IReadOnlyList<LdEngineReading> Engines, int Gates, int GatesDestroyed)
{
    public static LdEngineReport None { get; } = new(new LdEngineReading[0], 0, 0);
}

/// <summary>
/// One ram or siege tower deployed at the boot, as the field holds it now (#1160). <c>Kind</c> and <c>Index</c> (its place among the boot's engines of that kind) name it across reads. <c>Traveled</c> is the
/// distance along its path since its start, <c>Arrived</c> whether it reached the wall or gate, <c>InLane</c> whether its siege lane lists it, and <c>Done</c> whether its task is already complete (a ram whose
/// gate is down), so the attackers have no use for it. <c>QueuesOff</c> and <c>WaitAtStart</c> hold a tower to its boot state (a ram reads true for both): every ladder queue off, as the boot leaves them
/// until the tower arrives, and its waiting soldiers sent to the lower wait point, the boot's choice.
/// </summary>
internal readonly record struct LdEngineState(string Kind, int Index, bool Destroyed, bool Disabled, float HitPoints, float MaxHitPoints, float Traveled, bool Arrived, bool InLane, bool Done, bool QueuesOff,
    bool WaitAtStart);

/// <summary>What one wave start's restore did: each engine it brought back, as read after the restore, and a problem when it could not act (an engine's reset was not found or threw).</summary>
internal sealed record LdEngineRestore(IReadOnlyList<LdEngineState> Restored, string? Problem = null)
{
    public static LdEngineRestore None { get; } = new(new LdEngineState[0]);
}

/// <summary>A wave's Winged Terror pulses added up: how many, how many defenders lost morale and how much, how many were left below the floor, and the slowest pulse.</summary>
internal sealed class LdTerrorTally
{
    public int Pulses { get; private set; }
    public int Drained { get; private set; }
    public float Lost { get; private set; }
    public int PushedBelow { get; private set; }
    public double MaxMilliseconds { get; private set; }

    public void Add(LdPulseResult pulse)
    {
        Pulses++;
        Drained += pulse.Drained;
        Lost += pulse.Lost;
        PushedBelow += pulse.PushedBelow;
        MaxMilliseconds = System.Math.Max(MaxMilliseconds, pulse.Milliseconds);
    }
}
