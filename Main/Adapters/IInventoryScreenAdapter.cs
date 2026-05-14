using System.Collections.Generic;
using TaleWorlds.CampaignSystem.ViewModelCollection.Inventory;
using TAOM.Features.EquipPresets.Models;

namespace TAOM.Adapters;

/// <summary>
/// EquipPresets-scoped wrapper around the active <c>SPInventoryVM</c>. Distinct from
/// <see cref="IInventoryVMAdapter"/> (QuickActions): that one exposes Sell/Unequip operations
/// for the bulk-action menu; this one routes preset Load through vanilla
/// <c>InventoryLogic.TransferCommand</c> + <c>AddTransferCommands</c> so the displaced
/// equipment is deposited and inventory items are consumed (Codex review 2026-05-07 critical
/// finding: direct slot mutation duplicated/lost items).
/// Both adapters can coexist; the SubModule wires each feature's patch to its own.
/// </summary>
public interface IInventoryScreenAdapter
{
    /// <summary>
    /// Phase 9b #141 P1 — expose VM capture on the interface. Pre-fix
    /// Patch33_SPInventoryVMRefresh did a concrete cast (`as InventoryScreenAdapter`) because
    /// the method was missing from the interface. Cast succeeded today but would silently fail
    /// (returning null + skipping the call) if a future mock/alternative impl was registered,
    /// leading to the user-visible "presets overlay shows no hero, can't load" symptom with
    /// no log signal.
    /// </summary>
    void SetActive(SPInventoryVM? vm);

    /// <summary>True when an inventory screen is open and a VM has been captured.</summary>
    bool IsAvailable { get; }

    /// <summary>StringId of the <c>Hero</c> whose equipment the inventory screen is currently
    /// showing. Null when no VM is active or the current character has no associated Hero.</summary>
    string? ActiveHeroStringId { get; }

    /// <summary>True when the inventory screen is in <c>EquipmentModes.Civilian</c>.</summary>
    bool IsViewingCivilianEquipment { get; }

    /// <summary>True when the inventory screen is in <c>EquipmentModes.Battle</c>.</summary>
    bool IsViewingBattleEquipment { get; }

    /// <summary>Triggers <c>SPInventoryVM.RefreshValues</c> after equipment mutation so the UI
    /// reflects the change. Vanilla <c>AfterTransfer</c> path also refreshes; this is a backup
    /// for code paths that bypass <see cref="LoadEquipment"/>.</summary>
    void RefreshActiveVM();

    /// <summary>Releases the captured VM. Called from the inventory screen's OnFinalize hook
    /// so the next inventory open repopulates via <c>Patch33_SPInventoryVMRefresh</c> rather
    /// than seeing a stale reference.</summary>
    void Clear();

    /// <summary>
    /// Applies a preset by building <c>TransferCommand</c>s and submitting them via vanilla
    /// <c>InventoryLogic.AddTransferCommands</c>. Vanilla auto-deposits the displaced equipment
    /// and consumes the inventory items, plus fires <c>AfterTransfer</c> to update slot VMs.
    /// Returns counts so the service can build a user-facing report.
    ///
    /// <para>For each slot in <paramref name="requests"/> with a non-empty <c>ItemStringId</c>:
    /// look up the matching <c>ItemRosterElement</c> in <c>PlayerInventory</c> (preferring the
    /// preset's <c>ItemModifierStringId</c> match). Missing items are reported, never conjured.
    /// For each slot in <paramref name="requests"/> with an empty <c>ItemStringId</c>:
    /// transfer the currently-equipped item back to inventory (clears the slot).</para>
    /// </summary>
    LoadEquipmentResult LoadEquipment(IReadOnlyList<PresetSlotRequest> requests, bool civilian);
}

/// <summary>
/// Outcome of <see cref="IInventoryScreenAdapter.LoadEquipment"/>. POCO — service consumes
/// counts only; the adapter handled all TaleWorlds-typed work internally.
/// </summary>
public sealed class LoadEquipmentResult
{
    public LoadEquipmentResult(int equippedCount, IReadOnlyList<string> missingItems,
                               IReadOnlyList<string> missingModifiers, IReadOnlyList<int> invalidSlots)
    {
        EquippedCount = equippedCount;
        MissingItems = missingItems;
        MissingModifiers = missingModifiers;
        InvalidSlots = invalidSlots;
    }

    public int EquippedCount { get; }
    public IReadOnlyList<string> MissingItems { get; }
    public IReadOnlyList<string> MissingModifiers { get; }
    /// <summary>Slot indexes where <c>Equipment.IsItemFitsToSlot</c> returned false
    /// (item type incompatible with slot — e.g., helmet in weapon slot from a tampered save).</summary>
    public IReadOnlyList<int> InvalidSlots { get; }

    public static LoadEquipmentResult Empty { get; } =
        new(0, System.Array.Empty<string>(), System.Array.Empty<string>(), System.Array.Empty<int>());
}
