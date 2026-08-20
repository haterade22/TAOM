using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.FieldCommission.Domain;

namespace TAOM.Tests.Features.FieldCommission;

/// <summary>
/// The decision half of Patch71_HeroResetEquipmentsGuard (#486). Deliberately engine-free: the
/// patch does every <c>Equipment</c> dereference at the boundary and passes the answers in as
/// booleans, so the branch that decides whether a fired companion keeps their kit is testable
/// without a running campaign.
/// </summary>
[TestClass]
public class EquipmentResetPlanTests
{
    /// <summary>
    /// All eight combinations of the three slots, exhaustively. Deferring is the ONLY branch that
    /// hands control back to an engine method with three unguarded dereferences, so the truth table
    /// is pinned in full rather than sampled: a single wrong row here is a crash in the game.
    /// Row 2 is the reported #486 case (no civilian roster on the troop) and row 3 is the
    /// bandit-culture case (no <c>DefaultStealthEquipmentRoster</c>).
    /// </summary>
    [DataTestMethod]
    [DataRow(true, true, true, true)]
    [DataRow(true, false, true, false)]
    [DataRow(true, true, false, false)]
    [DataRow(false, true, true, false)]
    [DataRow(true, false, false, false)]
    [DataRow(false, true, false, false)]
    [DataRow(false, false, true, false)]
    [DataRow(false, false, false, false)]
    public void CanDeferToEngine_OnlyDefersWhenTheTemplateSuppliesAllThreeSlots(
        bool hasBattle, bool hasCivilian, bool hasStealth, bool expected)
    {
        Assert.AreEqual(expected, EquipmentResetPlan.CanDeferToEngine(hasBattle, hasCivilian, hasStealth));
    }

    [TestMethod]
    public void ForSlot_TemplateSuppliesTheSlot_UsesTheTemplate()
    {
        Assert.AreEqual(
            EquipmentResetSource.Template,
            EquipmentResetPlan.ForSlot(hasSlotEquipment: true, hasBattleEquipment: true));
    }

    [TestMethod]
    public void ForSlot_SlotMissingButBattleAvailable_FallsBackToBattleGear()
    {
        Assert.AreEqual(
            EquipmentResetSource.BattleFallback,
            EquipmentResetPlan.ForSlot(hasSlotEquipment: false, hasBattleEquipment: true));
    }

    [TestMethod]
    public void ForSlot_SlotMissingAndNoBattleGear_LeavesTheSlotAlone()
    {
        // Nothing sane to reset from, so the hero keeps what they are wearing rather than
        // being handed the campaign-wide dead-equipment default.
        Assert.AreEqual(
            EquipmentResetSource.None,
            EquipmentResetPlan.ForSlot(hasSlotEquipment: false, hasBattleEquipment: false));
    }

    [TestMethod]
    public void ForSlot_SlotPresentWithoutBattleGear_StillUsesTheTemplate()
    {
        // Pins the precedence: the slot's own equipment wins, the battle fallback is only a
        // fallback. A missing battle roster must not suppress a civilian one that exists.
        Assert.AreEqual(
            EquipmentResetSource.Template,
            EquipmentResetPlan.ForSlot(hasSlotEquipment: true, hasBattleEquipment: false));
    }

    [TestMethod]
    public void KeepsSourceEquipmentType_TemplateSource_CopiesTheType()
    {
        Assert.IsTrue(EquipmentResetPlan.KeepsSourceEquipmentType(EquipmentResetSource.Template));
    }

    [TestMethod]
    public void KeepsSourceEquipmentType_BattleFallback_KeepsTheTargetType()
    {
        // Passing the source type here is what retypes a hero's civilian kit EquipmentType.Battle,
        // which is the defect this pins (same one HeroCommissionAdapter had at creation time).
        Assert.IsFalse(EquipmentResetPlan.KeepsSourceEquipmentType(EquipmentResetSource.BattleFallback));
    }

    [TestMethod]
    public void KeepsSourceEquipmentType_NoSource_KeepsTheTargetType()
    {
        Assert.IsFalse(EquipmentResetPlan.KeepsSourceEquipmentType(EquipmentResetSource.None));
    }
}
