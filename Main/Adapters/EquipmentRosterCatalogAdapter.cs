using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;
using TAOM.Core.Logging;

namespace TAOM.Adapters;

public sealed class EquipmentRosterCatalogAdapter : IEquipmentRosterCatalogAdapter
{
    private static readonly IReadOnlyList<string> EmptyItemIds = new string[0];

    private readonly IModLogger _logger;

    public EquipmentRosterCatalogAdapter(IModLogger logger)
    {
        _logger = logger;
    }

    public bool RosterExists(string rosterId)
    {
        if (string.IsNullOrEmpty(rosterId))
            return false;
        try
        {
            return MBObjectManager.Instance?.GetObject<MBEquipmentRoster>(rosterId) != null;
        }
        catch (Exception ex)
        {
            _logger.LogError($"EquipmentRosterCatalogAdapter: RosterExists('{rosterId}') failed: {ex.Message}");
            return false;
        }
    }

    public IReadOnlyList<string> GetBattleSetItemIds(string rosterId)
    {
        if (string.IsNullOrEmpty(rosterId))
            return EmptyItemIds;
        try
        {
            // Lookup mirrors PlayerEquipmentAdapter.ApplyRosterToPlayer: object-manager
            // fetch, then the FIRST battle set from AllEquipments.
            var roster = MBObjectManager.Instance?.GetObject<MBEquipmentRoster>(rosterId);
            var battle = roster?.AllEquipments?.FirstOrDefault(e => e != null && e.IsBattle);
            if (battle == null)
                return EmptyItemIds;

            // Dead-equipment guard (same rationale as PlayerEquipmentAdapter):
            // Campaign.Current.DeadBattleEquipment is a process-wide fallback singleton;
            // if a degenerate roster ever hands it back, reading it as issuable gear
            // would issue the shared dead-hero kit. Treat it as "no battle set".
            if (battle == Campaign.Current?.DeadBattleEquipment)
                return EmptyItemIds;

            var itemIds = new List<string>();
            for (var i = EquipmentIndex.WeaponItemBeginSlot; i < EquipmentIndex.NumEquipmentSetSlots; i++)
            {
                var element = battle[i];
                if (element.IsEmpty)
                    continue;
                var stringId = element.Item?.StringId;
                if (!string.IsNullOrEmpty(stringId))
                    itemIds.Add(stringId);
            }
            return itemIds;
        }
        catch (Exception ex)
        {
            _logger.LogError($"EquipmentRosterCatalogAdapter: GetBattleSetItemIds('{rosterId}') failed: {ex.Message}");
            return EmptyItemIds;
        }
    }

    public int GetItemValue(string itemId)
    {
        if (string.IsNullOrEmpty(itemId))
            return 0;
        try
        {
            return MBObjectManager.Instance?.GetObject<ItemObject>(itemId)?.Value ?? 0;
        }
        catch (Exception ex)
        {
            _logger.LogError($"EquipmentRosterCatalogAdapter: GetItemValue('{itemId}') failed: {ex.Message}");
            return 0;
        }
    }
}
