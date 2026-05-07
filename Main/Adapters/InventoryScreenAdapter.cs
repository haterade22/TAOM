using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Inventory;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.ViewModelCollection.Inventory;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;
using TAOM.Core.Logging;
using TAOM.Features.EquipPresets.Models;

namespace TAOM.Adapters;

/// <summary>
/// Concrete <see cref="IInventoryScreenAdapter"/>. Captures the active <c>SPInventoryVM</c> via
/// <see cref="SetActive"/> from <c>Patch33_SPInventoryVMRefresh</c>; reads the private
/// <c>_currentCharacter</c> and <c>_inventoryLogic</c> fields via cached reflection.
///
/// Load path is the Codex review #2026-05-07 critical fix: instead of direct
/// <c>equipment[index] = element</c> mutation, build <c>TransferCommand</c>s and submit via
/// <c>InventoryLogic.AddTransferCommands</c>. Vanilla auto-deposits the displaced equipped
/// item to inventory and consumes the inventory item being equipped, plus fires
/// <c>AfterTransfer</c> to refresh slot VMs.
/// </summary>
public sealed class InventoryScreenAdapter : IInventoryScreenAdapter
{
    private static readonly FieldInfo? CurrentCharacterField =
        typeof(SPInventoryVM).GetField("_currentCharacter", BindingFlags.NonPublic | BindingFlags.Instance);

    private static readonly FieldInfo? InventoryLogicField =
        typeof(SPInventoryVM).GetField("_inventoryLogic", BindingFlags.NonPublic | BindingFlags.Instance);

    private readonly IModLogger _logger;
    private SPInventoryVM? _active;

    public InventoryScreenAdapter(IModLogger logger)
    {
        _logger = logger;
    }

    public void SetActive(SPInventoryVM? vm) => _active = vm;
    public void Clear() => _active = null;

    public bool IsAvailable => _active != null;

    public string? ActiveHeroStringId
    {
        get
        {
            var character = ActiveCharacter();
            return character?.HeroObject?.StringId;
        }
    }

    public bool IsViewingCivilianEquipment => _active?.EquipmentMode == 0;
    public bool IsViewingBattleEquipment => _active?.EquipmentMode == 1;

    public void RefreshActiveVM()
    {
        if (_active == null) return;
        try { _active.RefreshValues(); }
        catch (Exception ex) { _logger.LogError($"[EquipPresets] RefreshActiveVM failed: {ex.Message}"); }
    }

    public LoadEquipmentResult LoadEquipment(IReadOnlyList<PresetSlotRequest> requests, bool civilian)
    {
        if (_active == null || requests == null || requests.Count == 0)
            return LoadEquipmentResult.Empty;

        var character = ActiveCharacter();
        if (character == null) return LoadEquipmentResult.Empty;
        var inventoryLogic = ActiveInventoryLogic();
        if (inventoryLogic == null)
        {
            _logger.LogWarning("[EquipPresets] LoadEquipment: SPInventoryVM._inventoryLogic not accessible — Load skipped");
            return LoadEquipmentResult.Empty;
        }

        var targetSide = civilian
            ? InventoryLogic.InventorySide.CivilianEquipment
            : InventoryLogic.InventorySide.BattleEquipment;

        var targetEquipment = civilian ? character.FirstCivilianEquipment : character.FirstBattleEquipment;
        if (targetEquipment == null) return LoadEquipmentResult.Empty;

        var commands = new List<TransferCommand>();
        var missingItems = new List<string>();
        var missingModifiers = new List<string>();
        var invalidSlots = new List<int>();
        var equipped = 0;

        foreach (var req in requests)
        {
            var slotIdx = req.SlotIndex;
            if (slotIdx < 0 || slotIdx >= 12)
            {
                invalidSlots.Add(slotIdx);
                continue;
            }
            var slotEnum = (EquipmentIndex)slotIdx;

            if (string.IsNullOrEmpty(req.ItemStringId))
            {
                // Clear request: unequip current slot occupant (if any) back to PlayerInventory.
                var current = targetEquipment[slotEnum];
                if (!current.IsEmpty)
                {
                    commands.Add(TransferCommand.Transfer(
                        amount: 1,
                        fromSide: targetSide,
                        toSide: InventoryLogic.InventorySide.PlayerInventory,
                        elementToTransfer: new ItemRosterElement(current, 1),
                        fromEquipmentIndex: slotEnum,
                        toEquipmentIndex: EquipmentIndex.None,
                        character: character));
                }
                continue;
            }

            // Apply request: look up item + modifier.
            var item = MBObjectManager.Instance.GetObject<ItemObject>(req.ItemStringId);
            if (item == null)
            {
                missingItems.Add(req.ItemStringId);
                continue;
            }

            ItemModifier? modifier = null;
            if (!string.IsNullOrEmpty(req.ItemModifierStringId))
            {
                modifier = MBObjectManager.Instance.GetObject<ItemModifier>(req.ItemModifierStringId);
                if (modifier == null) missingModifiers.Add(req.ItemModifierStringId);
            }

            // Slot-fit gate (M7 Codex fix). Vanilla SPInventoryVM.IsItemEquipmentPossible uses this.
            // The Equipment indexer setter calls IsItemFitsToSlot but ignores the return — we must
            // check explicitly so a tampered save / item-XML drift doesn't put a helmet in a weapon slot.
            if (!Equipment.IsItemFitsToSlot(slotEnum, item))
            {
                invalidSlots.Add(slotIdx);
                continue;
            }

            // If the slot already holds the same EquipmentElement, no-op.
            var existing = targetEquipment[slotEnum];
            if (!existing.IsEmpty
                && existing.Item == item
                && ReferenceEquals(existing.ItemModifier, modifier))
            {
                equipped++;
                continue;
            }

            // Find the item in PlayerInventory (preferring exact modifier match).
            var probe = new EquipmentElement(item, modifier);
            var rosterElement = inventoryLogic.FindItemFromSide(InventoryLogic.InventorySide.PlayerInventory, probe);
            if (rosterElement == null)
            {
                // Fallback: bare-item match (modifier mismatch). Use the inventory's actual element so
                // the player's existing modifier on the spare item is preserved through transfer.
                var bareProbe = new EquipmentElement(item);
                rosterElement = inventoryLogic.FindItemFromSide(InventoryLogic.InventorySide.PlayerInventory, bareProbe);
            }
            if (rosterElement == null)
            {
                missingItems.Add(req.ItemStringId);
                continue;
            }

            commands.Add(TransferCommand.Transfer(
                amount: 1,
                fromSide: InventoryLogic.InventorySide.PlayerInventory,
                toSide: targetSide,
                elementToTransfer: rosterElement.Value,
                fromEquipmentIndex: EquipmentIndex.None,
                toEquipmentIndex: slotEnum,
                character: character));
            equipped++;
        }

        if (commands.Count > 0)
        {
            try
            {
                inventoryLogic.AddTransferCommands(commands);
            }
            catch (Exception ex)
            {
                _logger.LogError($"[EquipPresets] AddTransferCommands failed: {ex.Message}");
            }
        }

        return new LoadEquipmentResult(equipped, missingItems, missingModifiers, invalidSlots);
    }

    private CharacterObject? ActiveCharacter()
    {
        if (_active == null || CurrentCharacterField == null) return null;
        try { return CurrentCharacterField.GetValue(_active) as CharacterObject; }
        catch (Exception ex)
        {
            _logger.LogError($"[EquipPresets] _currentCharacter reflection failed: {ex.Message}");
            return null;
        }
    }

    private InventoryLogic? ActiveInventoryLogic()
    {
        if (_active == null || InventoryLogicField == null) return null;
        try { return InventoryLogicField.GetValue(_active) as InventoryLogic; }
        catch (Exception ex)
        {
            _logger.LogError($"[EquipPresets] _inventoryLogic reflection failed: {ex.Message}");
            return null;
        }
    }
}
