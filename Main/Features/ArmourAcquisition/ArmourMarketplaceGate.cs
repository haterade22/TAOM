using System;
using TAOM.Features.CultureMarketplace;

namespace TAOM.Features.ArmourAcquisition;

/// <summary>
/// CultureMarketplace's stock gate (<see cref="IMarketplaceStockGate"/>): the town's armoury level from
/// <see cref="ArmouryLevelService"/>, then the per-item answer from <see cref="IArmourGateService"/>. With
/// gating off, the item's XML <c>is_merchandise</c> still holds: CultureMarketplace's pool ignores
/// NotMerchandise, so this is what keeps the troll gear and other hero-only kit off its stalls.
/// </summary>
public sealed class ArmourMarketplaceGate : IMarketplaceStockGate
{
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
            return itemId => _gate.GetRecord(itemId)?.IsMerchandise ?? true;
        var level = _levels.GetTownLevel(townId);
        return itemId => _gate.IsEligibleForMarket(itemId, level);
    }
}
