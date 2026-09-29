using System;
using TAOM.Features.CultureMarketplace;

namespace TAOM.Features.ArmourAcquisition;

/// <summary>
/// CultureMarketplace's stock gate (<see cref="IMarketplaceStockGate"/>): the town's armoury level from
/// <see cref="ArmouryLevelService"/>, then the per-item answer from <see cref="IArmourGateService"/>.
/// </summary>
public sealed class ArmourMarketplaceGate : IMarketplaceStockGate
{
    private static readonly Func<string, bool> AnyItem = _ => true;

    private readonly IArmourGateService _gate;
    private readonly ArmouryLevelService _levels;

    public ArmourMarketplaceGate(IArmourGateService gate, ArmouryLevelService levels)
    {
        _gate = gate;
        _levels = levels;
    }

    public Func<string, bool> ForTown(string townId)
    {
        if (!_gate.IsActive)
            return AnyItem;
        var level = _levels.GetTownLevel(townId);
        return itemId => _gate.IsEligibleForMarket(itemId, level);
    }
}
