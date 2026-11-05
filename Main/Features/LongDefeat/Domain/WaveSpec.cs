using System.Collections.Generic;

namespace TAOM.Features.LongDefeat.Domain;

internal enum FormationSlot
{
    Infantry,
    Ranged,
}

/// <summary>One row of a wave's mix: the troop, the formation it joins, its share of the wave.</summary>
internal sealed record WaveTroop(string TroopId, FormationSlot Slot, int Weight);

/// <summary>
/// A wave: how many soldiers, split by weight across the mix. Past the authored list the wave also carries the health and damage
/// factors every attacker of it takes, and how many tier steps its mix has moved (an authored wave: 1, 1 and 0).
/// </summary>
internal sealed record WaveSpec(int Count, IReadOnlyList<WaveTroop> Mix, float HealthFactor = 1f, float DamageFactor = 1f, int TierSteps = 0);

/// <summary>How waves past the authored list grow: the last roster, with this many more soldiers each wave up to the cap.</summary>
internal sealed record WaveScaling(int CountPerWave, int MaxCount);

/// <summary>One soldier of an expanded wave.</summary>
internal sealed record WaveSoldier(string TroopId, FormationSlot Slot);
