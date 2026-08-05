using System;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;
using TAOM.Core.Logging;

namespace TAOM.Adapters;

/// <summary>
/// See IPartyItemRosterAdapter for the deliberate (ItemObject, int)-overload
/// reasoning (unmodified-stack targeting keeps player-modified variants safe).
/// All roster access goes through <c>new EquipmentElement(item)</c> semantics:
/// AddToCounts(ItemObject, int) is exactly that wrapper (ItemRoster.cs:185),
/// and count/remove use FindIndexOfElement so they touch only the plain stack.
/// </summary>
public sealed class PartyItemRosterAdapter : IPartyItemRosterAdapter
{
    private readonly IModLogger _logger;

    public PartyItemRosterAdapter(IModLogger logger)
    {
        _logger = logger;
    }

    public bool IsMainPartyAvailable()
    {
        try
        {
            return PartyBase.MainParty?.ItemRoster != null;
        }
        catch (Exception ex)
        {
            _logger.LogError($"PartyItemRosterAdapter: IsMainPartyAvailable failed: {ex.Message}");
            return false;
        }
    }

    public bool AddItem(string itemId, int count)
    {
        if (string.IsNullOrEmpty(itemId) || count <= 0)
            return false;
        try
        {
            var roster = PartyBase.MainParty?.ItemRoster;
            var item = MBObjectManager.Instance?.GetObject<ItemObject>(itemId);
            if (roster == null || item == null)
                return false;
            // (ItemObject, int) on purpose: issued gear is modifier-less, so this
            // lands on the unmodified stack — see the interface doc.
            roster.AddToCounts(item, count);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError($"PartyItemRosterAdapter: AddItem('{itemId}', {count}) failed: {ex.Message}");
            return false;
        }
    }

    public int GetItemCount(string itemId)
    {
        if (string.IsNullOrEmpty(itemId))
            return 0;
        try
        {
            var roster = PartyBase.MainParty?.ItemRoster;
            var item = MBObjectManager.Instance?.GetObject<ItemObject>(itemId);
            if (roster == null || item == null)
                return 0;
            // FindIndexOfElement matches item+modifier(null) — the UNMODIFIED stack
            // only. GetItemNumber would return whichever modifier-stack it finds first.
            var index = roster.FindIndexOfElement(new EquipmentElement(item));
            return index >= 0 ? roster.GetElementNumber(index) : 0;
        }
        catch (Exception ex)
        {
            _logger.LogError($"PartyItemRosterAdapter: GetItemCount('{itemId}') failed: {ex.Message}");
            return 0;
        }
    }

    public int RemoveItem(string itemId, int count)
    {
        if (string.IsNullOrEmpty(itemId) || count <= 0)
            return 0;
        try
        {
            var roster = PartyBase.MainParty?.ItemRoster;
            var item = MBObjectManager.Instance?.GetObject<ItemObject>(itemId);
            if (roster == null || item == null)
                return 0;
            // Clamp to the unmodified stack: a negative AddToCounts on a missing
            // element trips the engine's FailedAssert and removes nothing
            // (ItemRoster.cs:200-207) — never hand it more than we hold.
            var held = GetItemCount(itemId);
            var toRemove = Math.Min(count, held);
            if (toRemove <= 0)
                return 0;
            roster.AddToCounts(item, -toRemove);
            return toRemove;
        }
        catch (Exception ex)
        {
            _logger.LogError($"PartyItemRosterAdapter: RemoveItem('{itemId}', {count}) failed: {ex.Message}");
            return 0;
        }
    }
}
