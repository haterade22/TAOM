using System;
using TaleWorlds.CampaignSystem.Settlements;
using TAOM.Core.Logging;

namespace TAOM.Adapters;

/// <summary>
/// Resolves a settlement's current owner faction for the Elite Emissary. <see cref="Settlement.OwnerClan"/>
/// already hops village→bound-town, so village key-settlements price by their bound town's owner.
/// Owner culture prefers the owner clan's culture, falling back to the settlement's intrinsic culture
/// for unowned/just-conquered transient state. All access is null-conditional (adapters.md — computed
/// getters can throw before a plain null check).
/// </summary>
public sealed class SettlementOwnerAdapter : ISettlementOwnerAdapter
{
    private readonly IModLogger _logger;

    public SettlementOwnerAdapter(IModLogger logger)
    {
        _logger = logger;
    }

    public SettlementOwnerInfo GetOwnerInfo(Settlement settlement)
    {
        if (settlement == null)
            return new SettlementOwnerInfo(null, null, null);

        try
        {
            var settlementId = settlement.StringId;
            var ownerClan = settlement.OwnerClan;
            var kingdomId = ownerClan?.Kingdom?.StringId;
            var cultureId = ownerClan?.Culture?.StringId ?? settlement.Culture?.StringId;
            return new SettlementOwnerInfo(settlementId, kingdomId, cultureId);
        }
        catch (Exception ex)
        {
            _logger.LogError($"[EliteEmissary] SettlementOwnerAdapter.GetOwnerInfo('{settlement.StringId}') failed: {ex.Message}");
            return new SettlementOwnerInfo(settlement.StringId, null, null);
        }
    }
}
