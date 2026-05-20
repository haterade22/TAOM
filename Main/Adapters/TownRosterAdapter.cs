using System;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;
using TAOM.Core.Logging;

namespace TAOM.Adapters;

public class TownRosterAdapter : ITownRosterAdapter
{
    private readonly IModLogger _logger;

    public TownRosterAdapter(IModLogger logger)
    {
        _logger = logger;
    }

    public string GetCurrentCultureId(Settlement settlement)
    {
        return settlement?.OwnerClan?.Culture?.StringId;
    }

    public string GetSettlementId(Settlement settlement)
    {
        return settlement?.StringId;
    }

    public int GetRosterDistinctItemCount(Settlement settlement)
    {
        var roster = settlement?.ItemRoster;
        return roster?.Count ?? 0;
    }

    public bool AddItem(Settlement settlement, string itemId, int count)
    {
        if (settlement == null || string.IsNullOrEmpty(itemId) || count <= 0)
            return false;

        try
        {
            var itemObject = MBObjectManager.Instance?.GetObject<ItemObject>(itemId);
            if (itemObject == null)
            {
                _logger.LogDebug($"[CultureMarketplace] AddItem: '{itemId}' not in MBObjectManager — skipped");
                return false;
            }

            settlement.ItemRoster.AddToCounts(new EquipmentElement(itemObject), count);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError($"[CultureMarketplace] AddItem('{itemId}' -> {settlement.StringId}) failed: {ex.Message}");
            return false;
        }
    }
}
