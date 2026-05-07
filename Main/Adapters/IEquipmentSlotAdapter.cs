using System.Collections.Generic;
using TAOM.Features.EquipPresets.Models;

namespace TAOM.Adapters;

/// <summary>
/// Read-only access to a hero's <c>BattleEquipment</c> / <c>CivilianEquipment</c> for preset
/// capture. Services see only StringIds and primitives — full <c>EquipmentElement</c> stays
/// inside the adapter (per <c>.claude/rules/adapters.md</c>).
///
/// Writes go through <see cref="IInventoryScreenAdapter.LoadEquipment"/> so vanilla
/// <c>InventoryLogic.AddTransferCommands</c> handles roster consumption + displaced-equipment
/// deposit. Direct slot mutation was removed in the Codex review #2026-05-07 fix pass —
/// see CHANGELOG entry "Load via InventoryLogic".
/// </summary>
public interface IEquipmentSlotAdapter
{
    /// <summary>True when a hero with the given StringId is alive and active.</summary>
    bool HeroExists(string heroStringId);

    /// <summary>
    /// Returns one snapshot per slot 0..11 — non-empty slots carry the item's StringId and
    /// modifier StringId; empty slots return an <see cref="EquippedSlotSnapshot"/> with empty
    /// <c>ItemStringId</c> so Load can clear that slot. Empty list when the hero is missing
    /// or backing <c>Equipment</c> is the shared dead-equipment fallback.
    /// </summary>
    IReadOnlyList<EquippedSlotSnapshot> Capture(string heroStringId, bool civilian);
}
