using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.BattleLoadDiagnostics;
using TAOM.Features.BattleLoadDiagnostics.Domain;

namespace TAOM.Tests.Features.BattleLoadDiagnostics;

[TestClass]
public class EquipmentDumpFormatterTests
{
    private EquipmentDumpFormatter _sut;

    [TestInitialize]
    public void Setup() => _sut = new EquipmentDumpFormatter();

    private static EquipmentSnapshot Snapshot(params EquipmentSlotSnapshot[] slots) =>
        new EquipmentSnapshot(0, "Test", "char", "culture", slots);

    [TestMethod]
    public void Format_NullSnapshot_ReturnsEmpty()
    {
        var lines = _sut.Format(null);
        Assert.AreEqual(0, lines.Count);
    }

    [TestMethod]
    public void Format_EmptyEquipment_EmitsNoSlotLines()
    {
        var lines = _sut.Format(Snapshot());
        Assert.AreEqual(0, lines.Count);
    }

    [TestMethod]
    public void Format_SlotWithNullCollisionMesh_EmitsShieldBoNullToken()
    {
        var slot = new EquipmentSlotSnapshot("Weapon1", "lome_shield", null, null, null, "shieldmesh", "Shield");
        var lines = _sut.Format(Snapshot(slot));

        Assert.AreEqual(1, lines.Count);
        StringAssert.Contains(lines[0], "shieldBo=<null>");
        StringAssert.Contains(lines[0], "bo=<null>");
    }

    [TestMethod]
    public void Format_ShieldSlot_IncludesCollisionBodyName()
    {
        var slot = new EquipmentSlotSnapshot("Weapon1", "lome_shield", "bo_cap_x", "bo_x", null, "shieldmesh", "Shield");
        var lines = _sut.Format(Snapshot(slot));

        StringAssert.Contains(lines[0], "shieldBo=bo_x");
        StringAssert.Contains(lines[0], "bo=bo_cap_x");
    }

    [TestMethod]
    public void Format_IncludesItemIdAndKind()
    {
        var slot = new EquipmentSlotSnapshot("Body", "sk_gd_body", null, null, null, "mesh", "BodyArmor");
        var lines = _sut.Format(Snapshot(slot));

        StringAssert.Contains(lines[0], "id=sk_gd_body");
        StringAssert.Contains(lines[0], "kind=BodyArmor");
    }

    [TestMethod]
    public void Format_MultipleSlots_EmitsOneLinePerSlot()
    {
        var lines = _sut.Format(Snapshot(
            new EquipmentSlotSnapshot("Head", "helm", null, null, null, "m", "HeadArmor"),
            new EquipmentSlotSnapshot("Body", "chest", null, null, null, "m", "BodyArmor")));

        Assert.AreEqual(2, lines.Count);
    }
}
