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

/// <summary>What an attacker is to the wave (phase2-plan.md section 2): one of its roster, a soldier a commander summoned, or the commander himself. Only roster soldiers count toward the wave's roster.</summary>
internal enum AttackerRole
{
    Roster,
    Summoned,
    Commander,
}

/// <summary>One soldier of an expanded wave. A summoned soldier carries the number of the summon (<c>Use</c>, from 1) that queued him, so a batch never mixes two summons; every other soldier has 0.</summary>
internal sealed record WaveSoldier(string TroopId, FormationSlot Slot, AttackerRole Role = AttackerRole.Roster, int Use = 0);
