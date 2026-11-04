using System.Collections.Generic;

namespace TAOM.Features.LongDefeat.Domain;

internal enum FormationSlot
{
    Infantry,
    Ranged,
}

/// <summary>One row of a wave's mix: the troop, the formation it joins, its share of the wave.</summary>
internal sealed record WaveTroop(string TroopId, FormationSlot Slot, int Weight);

/// <summary>A wave: how many soldiers, split by weight across the mix.</summary>
internal sealed record WaveSpec(int Count, IReadOnlyList<WaveTroop> Mix);

/// <summary>How waves past the authored list grow: the last roster, with this many more soldiers each wave up to the cap.</summary>
internal sealed record WaveScaling(int CountPerWave, int MaxCount);

/// <summary>One soldier of an expanded wave.</summary>
internal sealed record WaveSoldier(string TroopId, FormationSlot Slot);
