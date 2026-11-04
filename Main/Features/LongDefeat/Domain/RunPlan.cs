using System.Collections.Generic;

namespace TAOM.Features.LongDefeat.Domain;

/// <summary>A garrison row: <c>Count</c> soldiers of one troop in one formation at the start of a run.</summary>
internal sealed record GarrisonRow(string TroopId, FormationSlot Slot, int Count);

/// <summary>What the <c>SurvivalRun</c> constructor takes: one fortress's garrison, its waves and shop, and the rules and options.</summary>
internal sealed record RunPlan(
    string FortressId,
    IReadOnlyList<GarrisonRow> Garrison,
    WavePlan Waves,
    ShopPlan Shop,
    RunRules Rules,
    RunOptions Options)
{
    /// <summary>Gold paid for each attacker troop that dies, by troop id. A troop missing from it pays nothing.</summary>
    public IReadOnlyDictionary<string, int> Bounties { get; init; } = new Dictionary<string, int>();

    /// <summary>The recorded keep point of the fortress, or null: the director then uses the scene's reinforcement frame and <see cref="RunRules.KeepRadius"/>.</summary>
    public KeepPoint? Keep { get; init; }
}
