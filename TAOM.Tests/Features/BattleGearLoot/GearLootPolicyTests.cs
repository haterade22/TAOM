using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.BattleGearLoot.Domain;

namespace TAOM.Tests.Features.BattleGearLoot;

[TestClass]
public class GearLootPolicyTests
{
    private static GearPiece Piece(string id = "sword", bool merchandise = true, bool banner = false, bool quest = false, string? mount = null) =>
        new(0, id, merchandise, banner, quest, mount);

    private static bool NoNamed(string _) => false;

    [TestMethod]
    public void Ordinary_IsEligible() => Assert.IsTrue(GearLootPolicy.IsEligible(Piece(), NoNamed));

    [TestMethod]
    public void EmptyItemId_Excluded() => Assert.IsFalse(GearLootPolicy.IsEligible(Piece(id: ""), NoNamed));

    [TestMethod]
    public void NonMerchandise_Excluded() => Assert.IsFalse(GearLootPolicy.IsEligible(Piece(merchandise: false), NoNamed));

    [TestMethod]
    public void NamedWeaponThatIsMerchandise_Excluded() =>
        Assert.IsFalse(GearLootPolicy.IsEligible(Piece(id: "named_blade"), id => id == "named_blade"));

    [TestMethod]
    public void Banner_Excluded() => Assert.IsFalse(GearLootPolicy.IsEligible(Piece(banner: true), NoNamed));

    [TestMethod]
    public void QuestItem_Excluded() => Assert.IsFalse(GearLootPolicy.IsEligible(Piece(quest: true), NoNamed));

    [DataTestMethod]
    [DataRow("spider")]
    [DataRow("elephant")]
    [DataRow("mumakil")]
    public void LockedCreatureMount_Excluded(string monster)
    {
        Assert.IsFalse(GearLootPolicy.IsEligible(Piece(mount: Monster(monster)), NoNamed));
    }

    [TestMethod]
    public void OrdinaryHorse_Eligible() => Assert.IsTrue(GearLootPolicy.IsEligible(Piece(mount: "horse"), NoNamed));

    [TestMethod]
    public void HeavyPieceThatIsMerchandise_Eligible() => Assert.IsTrue(GearLootPolicy.IsEligible(Piece(id: "heavy_helm"), NoNamed));

    private static string Monster(string key) => key switch
    {
        "spider" => TAOM.Features.Spider.SpiderConfig.SpiderMonsterId,
        "elephant" => TAOM.Features.Elephant.ElephantConfig.ElephantMonsterId,
        _ => TAOM.Features.Mumakil.MumakilConfig.MumakilMonsterId,
    };
}
