using System;
using System.Collections.Generic;
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

    public int GetItemCount(Settlement settlement, string itemId)
    {
        if (settlement == null || string.IsNullOrEmpty(itemId)) return 0;
        try
        {
            var itemObject = MBObjectManager.Instance?.GetObject<ItemObject>(itemId);
            if (itemObject == null) return 0;
            // Deep-review 2026-05-21 (Data Flow #9): vanilla `FindIndexOfItem` only finds
            // the FIRST stack matching the ItemObject — different ItemModifiers create
            // separate stacks, so a town with "Sharp warg_brown ×3" + "Damaged warg_brown ×2"
            // would have reported 3 (or 2, whichever stack happens to be first in storage),
            // not the total 5. Sum across ALL stacks for the same ItemObject to make the
            // guaranteed-stock floor work correctly even when modifiers split inventory.
            var roster = settlement.ItemRoster;
            var total = 0;
            for (var i = 0; i < roster.Count; i++)
            {
                if (roster.GetItemAtIndex(i) == itemObject)
                    total += roster.GetElementNumber(i);
            }
            return total;
        }
        catch (Exception ex)
        {
            _logger.LogError($"[CultureMarketplace] GetItemCount('{itemId}' @ {settlement.StringId}) failed: {ex.Message}");
            return 0;
        }
    }

    public bool RemoveItem(Settlement settlement, string itemId, int count)
    {
        if (settlement == null || string.IsNullOrEmpty(itemId) || count <= 0) return false;
        try
        {
            var itemObject = MBObjectManager.Instance?.GetObject<ItemObject>(itemId);
            if (itemObject == null) return false;
            var roster = settlement.ItemRoster;

            // A single ItemObject can occupy MULTIPLE roster stacks when stacks carry different
            // ItemModifiers (e.g. "Sharp assassin_armor" + plain "assassin_armor"). The old code
            // sized `toRemove` from the FIRST stack (FindIndexOfItem is modifier-agnostic) but
            // applied it to `new EquipmentElement(itemObject)` (null modifier), which AddToCounts
            // matches by EXACT element — so the removal landed on a DIFFERENT, smaller stack and
            // drove its Amount negative → MBUnderFlowException("ItemRosterElement::Amount") spam
            // (crash report 2026-06-17: thousands of CultureMarketplace RemoveItem failures).
            // Mirror GetItemCount above: collect every matching stack with its OWN EquipmentElement
            // (modifier-preserving), then remove per-stack clamped to that stack's amount. Snapshot
            // first — AddToCounts mutates/reindexes the roster as stacks empty.
            var stacks = new List<(EquipmentElement element, int amount)>();
            for (var i = 0; i < roster.Count; i++)
            {
                if (roster.GetItemAtIndex(i) != itemObject) continue;
                var amt = roster.GetElementNumber(i);
                if (amt > 0)
                    stacks.Add((roster.GetElementCopyAtIndex(i).EquipmentElement, amt));
            }
            if (stacks.Count == 0) return false;

            var remaining = count;
            foreach (var (element, amount) in stacks)
            {
                if (remaining <= 0) break;
                var take = Math.Min(remaining, amount);
                // Vanilla AddToCounts accepts negative counts; this triggers OnInventoryUpdated
                // → MarketData price recalculation per Town.OnInventoryUpdated (decompiled v1.3.15).
                roster.AddToCounts(element, -take);
                remaining -= take;
            }
            return remaining < count; // removed at least one unit
        }
        catch (Exception ex)
        {
            _logger.LogError($"[CultureMarketplace] RemoveItem('{itemId}' @ {settlement.StringId}) failed: {ex.Message}");
            return false;
        }
    }

    public IReadOnlyList<RosterItemSnapshot> EnumerateRoster(Settlement settlement)
    {
        if (settlement?.ItemRoster == null) return Array.Empty<RosterItemSnapshot>();
        var roster = settlement.ItemRoster;
        var result = new List<RosterItemSnapshot>(roster.Count);
        try
        {
            for (var i = 0; i < roster.Count; i++)
            {
                var item = roster.GetItemAtIndex(i);
                if (item == null) continue;
                var n = roster.GetElementNumber(i);
                if (n <= 0) continue;
                result.Add(new RosterItemSnapshot(item.StringId, item.Culture?.StringId, n));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"[CultureMarketplace] EnumerateRoster({settlement.StringId}) failed: {ex.Message}");
        }
        return result;
    }
}
