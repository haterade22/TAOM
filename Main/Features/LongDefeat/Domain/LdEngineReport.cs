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
