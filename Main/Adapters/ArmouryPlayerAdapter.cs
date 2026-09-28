using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;
using TAOM.Features.ArmourAcquisition.Domain;

namespace TAOM.Adapters;

/// <summary>
/// Boundary implementation of <see cref="IArmouryPlayerAdapter"/>: the main hero and the main party's item
/// roster. Every removal goes stack by stack with the stack's own EquipmentElement, so a modifier never
/// strands a stack at a negative amount (the TownRosterAdapter.RemoveItem lesson, crash report 2026-06-17).
/// </summary>
public class ArmouryPlayerAdapter : IArmouryPlayerAdapter
{
    public string HeroId => Hero.MainHero?.StringId ?? string.Empty;

    public string? KingdomId => Hero.MainHero?.Clan?.Kingdom?.StringId;

    public string? CultureId => Hero.MainHero?.Culture?.StringId;

    public int Gold => Hero.MainHero?.Gold ?? 0;

    private static ItemRoster? Roster => MobileParty.MainParty?.ItemRoster;

    public IReadOnlyList<InventoryPiece> ReadInventory()
    {
        var pieces = new List<InventoryPiece>();
        var roster = Roster;
        if (roster == null)
            return pieces;
        for (var i = 0; i < roster.Count; i++)
        {
            var element = roster.GetElementCopyAtIndex(i);
            var item = element.EquipmentElement.Item;
            if (item == null || element.Amount <= 0)
                continue;
            pieces.Add(new InventoryPiece(item.StringId, element.EquipmentElement.ItemModifier?.StringId, element.Amount));
        }
        return pieces;
    }

    private static int CountItem(string itemId)
    {
        var roster = Roster;
        if (roster == null || string.IsNullOrEmpty(itemId))
            return 0;
        var total = 0;
        for (var i = 0; i < roster.Count; i++)
            if (roster.GetItemAtIndex(i)?.StringId == itemId)
                total += roster.GetElementNumber(i);
        return total;
    }

    public bool RemoveItem(string itemId, int count)
    {
        var roster = Roster;
        if (roster == null || string.IsNullOrEmpty(itemId) || count <= 0 || CountItem(itemId) < count)
            return false;
        // Snapshot the stacks first: AddToCounts reindexes the roster as a stack empties.
        var stacks = new List<(EquipmentElement element, int amount)>();
        for (var i = 0; i < roster.Count; i++)
        {
            var element = roster.GetElementCopyAtIndex(i);
            if (element.EquipmentElement.Item?.StringId == itemId && element.Amount > 0)
                stacks.Add((element.EquipmentElement, element.Amount));
        }
        var remaining = count;
        foreach (var (element, amount) in stacks)
        {
            if (remaining <= 0)
                break;
            var take = Math.Min(remaining, amount);
            roster.AddToCounts(element, -take);
            remaining -= take;
        }
        return remaining == 0;
    }

    public bool RemovePiece(string itemId, string? modifierId)
    {
        var roster = Roster;
        if (roster == null || string.IsNullOrEmpty(itemId))
            return false;
        for (var i = 0; i < roster.Count; i++)
        {
            var element = roster.GetElementCopyAtIndex(i);
            if (element.Amount <= 0 || element.EquipmentElement.Item?.StringId != itemId)
                continue;
            if (!string.Equals(element.EquipmentElement.ItemModifier?.StringId, modifierId, StringComparison.Ordinal))
                continue;
            roster.AddToCounts(element.EquipmentElement, -1);
            return true;
        }
        return false;
    }

    public bool AddPiece(string itemId, string? modifierId, int count)
    {
        var roster = Roster;
        var item = string.IsNullOrEmpty(itemId) ? null : MBObjectManager.Instance?.GetObject<ItemObject>(itemId);
        if (roster == null || item == null || count <= 0)
            return false;
        var modifier = string.IsNullOrEmpty(modifierId) ? null : MBObjectManager.Instance?.GetObject<ItemModifier>(modifierId);
        roster.AddToCounts(new EquipmentElement(item, modifier), count);
        return true;
    }

    public bool ChargeGold(int amount)
    {
        var hero = Hero.MainHero;
        if (hero == null || amount < 0 || hero.Gold < amount)
            return false;
        if (amount > 0)
            GiveGoldAction.ApplyBetweenCharacters(hero, null, amount);
        return true;
    }

    public bool ChangeRelationWith(string heroId, int delta)
    {
        // Hero.Find dereferences Campaign.Current unguarded (v1.5.3 Hero.cs:2212-2215): go through the
        // object manager only when there is one.
        var manager = Campaign.Current?.CampaignObjectManager;
        var hero = string.IsNullOrEmpty(heroId) || manager == null ? null : manager.Find<Hero>(heroId);
        if (hero == null)
            return false;
        ChangeRelationAction.ApplyPlayerRelation(hero, delta);
        return true;
    }
}
