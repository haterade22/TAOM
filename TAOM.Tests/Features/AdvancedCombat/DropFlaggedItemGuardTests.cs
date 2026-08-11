using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.AdvancedCombat;

namespace TAOM.Tests.Features.AdvancedCombat;

/// <summary>
/// Tests for the pure predicate behind <c>Patch50_DropFlaggedItemGuard</c>'s Prefix.
///
/// Vanilla <c>Agent.CheckToDropFlaggedItem</c> (Agent.cs:3595, v1.4.8) reads
/// <c>Equipment[equipmentIndex].Item.ItemFlags</c> at Agent.cs:3604 behind a guard that only
/// tests the INDEX (<c>!= EquipmentIndex.None</c>) — never <c>Equipment</c> itself nor the
/// resolved <c>Item</c>. Two shapes therefore NRE, and TAOM's synthetic creature bites reach
/// both because <c>Mission.OnAgentHit</c> calls the method on the victim as its last statement
/// (Mission.cs:5621):
///
///   1. A not-yet-built agent whose <c>Equipment</c> (a plain nullable auto-property assigned once
///      by <c>InitializeMissionEquipment</c> from <c>Agent.Build</c>) is still null — the indexer
///      itself throws. This is a SPAWN-time window: <c>Agent.Clear</c> does not null
///      <c>Equipment</c>, so teardown does not produce it. Observed live 2026-08-10 in a warg
///      battle: victim <c>IsHuman=true</c>, <c>IsMount=false</c>, <c>State=Active</c>,
///      <c>Health=13</c>, flags carrying <c>CanWieldWeapon</c>, and <c>Character == null</c>.
///   2. A wielded slot whose <c>MissionWeapon.Item</c> is null — the shape the original
///      2026-06-17 warg-vs-warg report was attributed to.
///
/// The predicate is deliberately parameterised on plain bools so it carries no engine
/// dependency and the Harmony patch stays a thin caller (ADR-002/ADR-008 — a patch body is not
/// unit-testable, so the decision must not live inside it).
/// </summary>
[TestClass]
public class DropFlaggedItemGuardTests
{
    [TestMethod]
    public void WouldVanillaDereferenceNull_NoEquipment_ReturnsTrue()
    {
        // The half-built-agent case: Equipment[index] throws before any Item is read.
        Assert.IsTrue(DropFlaggedItemGuard.WouldVanillaDereferenceNull(
            hasEquipment: false,
            primaryWielded: true, primaryItemPresent: true,
            offhandWielded: true, offhandItemPresent: true));
    }

    [TestMethod]
    public void WouldVanillaDereferenceNull_NoEquipmentAndNothingWielded_ReturnsTrue()
    {
        // Missing Equipment dominates: vanilla still indexes it if either slot reports wielded,
        // and a null Equipment is never a state we want to hand back to the engine.
        Assert.IsTrue(DropFlaggedItemGuard.WouldVanillaDereferenceNull(
            hasEquipment: false,
            primaryWielded: false, primaryItemPresent: false,
            offhandWielded: false, offhandItemPresent: false));
    }

    [TestMethod]
    public void WouldVanillaDereferenceNull_NothingWielded_ReturnsFalse()
    {
        // Both indices are EquipmentIndex.None — vanilla's loop body never runs. Let it run.
        Assert.IsFalse(DropFlaggedItemGuard.WouldVanillaDereferenceNull(
            hasEquipment: true,
            primaryWielded: false, primaryItemPresent: false,
            offhandWielded: false, offhandItemPresent: false));
    }

    [TestMethod]
    public void WouldVanillaDereferenceNull_PrimaryWieldedWithItem_ReturnsFalse()
    {
        // The ordinary armed human. This is the hot path — it must stay vanilla.
        Assert.IsFalse(DropFlaggedItemGuard.WouldVanillaDereferenceNull(
            hasEquipment: true,
            primaryWielded: true, primaryItemPresent: true,
            offhandWielded: false, offhandItemPresent: false));
    }

    [TestMethod]
    public void WouldVanillaDereferenceNull_BothSlotsWieldedWithItems_ReturnsFalse()
    {
        // Sword + shield. Still vanilla.
        Assert.IsFalse(DropFlaggedItemGuard.WouldVanillaDereferenceNull(
            hasEquipment: true,
            primaryWielded: true, primaryItemPresent: true,
            offhandWielded: true, offhandItemPresent: true));
    }

    [TestMethod]
    public void WouldVanillaDereferenceNull_PrimaryWieldedNullItem_ReturnsTrue()
    {
        Assert.IsTrue(DropFlaggedItemGuard.WouldVanillaDereferenceNull(
            hasEquipment: true,
            primaryWielded: true, primaryItemPresent: false,
            offhandWielded: false, offhandItemPresent: false));
    }

    [TestMethod]
    public void WouldVanillaDereferenceNull_OffhandWieldedNullItem_ReturnsTrue()
    {
        // Vanilla runs the offhand on the loop's second iteration, so a phantom offhand throws
        // even when the primary slot is perfectly healthy.
        Assert.IsTrue(DropFlaggedItemGuard.WouldVanillaDereferenceNull(
            hasEquipment: true,
            primaryWielded: true, primaryItemPresent: true,
            offhandWielded: true, offhandItemPresent: false));
    }

    [TestMethod]
    public void WouldVanillaDereferenceNull_OffhandOnlyWieldedWithItem_ReturnsFalse()
    {
        Assert.IsFalse(DropFlaggedItemGuard.WouldVanillaDereferenceNull(
            hasEquipment: true,
            primaryWielded: false, primaryItemPresent: false,
            offhandWielded: true, offhandItemPresent: true));
    }

    [TestMethod]
    public void WouldVanillaDereferenceNull_UnwieldedSlotWithNullItem_ReturnsFalse()
    {
        // A null Item only matters when the slot is actually wielded — vanilla's index guard
        // short-circuits first, so an empty non-wielded slot is not a throw and not our business.
        Assert.IsFalse(DropFlaggedItemGuard.WouldVanillaDereferenceNull(
            hasEquipment: true,
            primaryWielded: false, primaryItemPresent: false,
            offhandWielded: false, offhandItemPresent: false));
    }
}
