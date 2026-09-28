using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Features.ArmourAcquisition;
using TAOM.Features.ArmourAcquisition.Domain;
using TAOM.Features.SpecialResources;

namespace TAOM.Tests.Features.ArmourAcquisition;

/// <summary>
/// KEYforce's upgrade loop: a carried piece becomes its line's next class at a town armoury for gold, a
/// share of the value it gains, two metals and (for lord kit) the player's kingdom special resource. The
/// armoury's level caps the class it can work. The offer and the upgrade judge what the player can pay by
/// one rule; the upgrade never charges for anything it cannot finish, and the upgraded piece keeps its
/// quality modifier.
/// </summary>
[TestClass]
public class ArmouryUpgradeServiceTests
{
    private IArmourGateService _gate = null!;
    private IArmourAcquisitionConfigProvider _config = null!;
    private IArmouryPlayerAdapter _player = null!;
    private ISpecialResourceSpender _spender = null!;
    private ArmouryUpgradeService _service = null!;
    private readonly List<InventoryPiece> _inventory = new();

    private static readonly UpgradeMaterial[] Steel = { new("ironIngot4", 2) };

    private static ArmourAcquisitionConfig Config(params UpgradeRecipe[] recipes)
    {
        var d = ArmourAcquisitionConfig.Default;
        return new ArmourAcquisitionConfig(d.Enabled, 1, 2, 3, recipes.ToDictionary(r => r.Target), d.NamedWeapons,
            d.LordEventChance, d.LordEventCooldownDays, d.LordEventLeaveRelation, d.VisitChancePerDay, d.VisitDurationDays,
            d.VisitLevelBonus, d.Ladder);
    }

    [TestInitialize]
    public void Setup()
    {
        _gate = Substitute.For<IArmourGateService>();
        _config = Substitute.For<IArmourAcquisitionConfigProvider>();
        _config.GetConfig().Returns(Config(
            new UpgradeRecipe(ArmourClass.Heavy, 500, 0.5f, 0f, Steel),
            new UpgradeRecipe(ArmourClass.Lord, 3000, 0f, 150f, new[] { new UpgradeMaterial("ironIngot6", 6) })));
        _player = Substitute.For<IArmouryPlayerAdapter>();
        _player.HeroId.Returns("main_hero");
        _player.KingdomId.Returns("empire");
        _player.CultureId.Returns("gondor");
        _player.ReadInventory().Returns(_ => _inventory.ToList());
        _player.RemovePiece(Arg.Any<string>(), Arg.Any<string?>()).Returns(true);
        _player.RemoveItem(Arg.Any<string>(), Arg.Any<int>()).Returns(true);
        _player.AddPiece(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<int>()).Returns(true);
        _player.ChargeGold(Arg.Any<int>()).Returns(true);
        _spender = Substitute.For<ISpecialResourceSpender>();
        _service = new ArmouryUpgradeService(_gate, _config, _player, _spender);
        Holding();
    }

    private void Piece(string id, ArmourClass cls, string? next, int value)
    {
        _gate.GetClass(id).Returns(cls);
        _gate.GetNext(id).Returns(next);
        _gate.GetRecord(id).Returns(new ArmourItemRecord(id, ArmourSlot.Body, 2, true, "gondor", value));
    }

    /// <summary>The purse, the metals carried and the kingdom resource balance (null: the kingdom has none).</summary>
    private void Holding(int gold = 100000, int steel = 99, int thamaskene = 99, float? resource = 1000f)
    {
        _player.Gold.Returns(gold);
        _inventory.RemoveAll(p => p.ItemId.StartsWith("ironIngot"));
        _inventory.Add(new InventoryPiece("ironIngot4", null, steel));
        _inventory.Add(new InventoryPiece("ironIngot6", null, thamaskene));
        _spender.GetBalance("main_hero", "empire", "gondor")
            .Returns(resource.HasValue ? new SpecialResourceBalance("Castar", resource.Value) : null);
    }

    private void Carrying(params InventoryPiece[] pieces) => _inventory.AddRange(pieces);

    private UpgradeOffer Offer(float resource = 0f) =>
        new("med_a", "sturdy", "heavy_a", ArmourClass.Heavy, 800, Steel, resource, 1, UpgradeBlock.None);

    // --- BuildOffers ---

    [TestMethod]
    public void BuildOffers_MediumWithAHeavyNext_IsPricedFromTheRecipe()
    {
        Piece("med_a", ArmourClass.Medium, "heavy_a", 1000);
        Piece("heavy_a", ArmourClass.Heavy, null, 3000);
        Carrying(new InventoryPiece("med_a", "sturdy", 1));

        var offer = _service.BuildOffers(1).Single();

        Assert.AreEqual("heavy_a", offer.TargetItemId);
        Assert.AreEqual(ArmourClass.Heavy, offer.TargetClass);
        Assert.AreEqual(500 + 1000, offer.Gold, "flat gold plus half the value gained (3000 - 1000)");
        Assert.AreEqual("sturdy", offer.ModifierId);
        Assert.IsTrue(offer.CanUpgrade);
    }

    [TestMethod]
    public void BuildOffers_ValueLost_AddsNoValueShare()
    {
        Piece("med_a", ArmourClass.Medium, "heavy_a", 5000);
        Piece("heavy_a", ArmourClass.Heavy, null, 3000);
        Carrying(new InventoryPiece("med_a", null, 1));

        Assert.AreEqual(500, _service.BuildOffers(1).Single().Gold);
    }

    [TestMethod]
    public void BuildOffers_ArmouryBelowTheTargetLevel_IsOfferedButBlocked()
    {
        Piece("med_a", ArmourClass.Medium, "heavy_a", 1000);
        Piece("heavy_a", ArmourClass.Heavy, null, 3000);
        Carrying(new InventoryPiece("med_a", null, 1));

        var offer = _service.BuildOffers(0).Single();

        Assert.AreEqual(UpgradeBlock.ArmouryLevelTooLow, offer.Block);
        Assert.AreEqual(1, offer.RequiredLevel);
    }

    [TestMethod]
    public void BuildOffers_ShortOfGold_IsBlocked()
    {
        Piece("med_a", ArmourClass.Medium, "heavy_a", 1000);
        Piece("heavy_a", ArmourClass.Heavy, null, 1000);
        Carrying(new InventoryPiece("med_a", null, 1));
        Holding(gold: 499);

        Assert.AreEqual(UpgradeBlock.NotEnoughGold, _service.BuildOffers(1).Single().Block);
    }

    [TestMethod]
    public void BuildOffers_ShortOfAMetal_IsBlocked()
    {
        Piece("med_a", ArmourClass.Medium, "heavy_a", 1000);
        Piece("heavy_a", ArmourClass.Heavy, null, 1000);
        Carrying(new InventoryPiece("med_a", null, 1));
        Holding(steel: 1);

        Assert.AreEqual(UpgradeBlock.MissingMaterials, _service.BuildOffers(1).Single().Block);
    }

    [TestMethod]
    public void BuildOffers_MetalsAcrossStacksWithDifferentModifiers_AreCountedTogether()
    {
        Piece("med_a", ArmourClass.Medium, "heavy_a", 1000);
        Piece("heavy_a", ArmourClass.Heavy, null, 1000);
        Carrying(new InventoryPiece("med_a", null, 1));
        Holding(steel: 1);
        Carrying(new InventoryPiece("ironIngot4", "fine", 1));

        Assert.IsTrue(_service.BuildOffers(1).Single().CanUpgrade);
    }

    [TestMethod]
    public void BuildOffers_LordShortOfTheResource_IsBlocked()
    {
        Piece("elite_a", ArmourClass.Elite, "lord_a", 1000);
        Piece("lord_a", ArmourClass.Lord, null, 1000);
        Carrying(new InventoryPiece("elite_a", null, 1));
        Holding(resource: 149f);

        Assert.AreEqual(UpgradeBlock.NotEnoughResource, _service.BuildOffers(3).Single().Block);
    }

    [TestMethod]
    public void BuildOffers_LordWithNoKingdomResource_WaivesTheResource()
    {
        Piece("elite_a", ArmourClass.Elite, "lord_a", 1000);
        Piece("lord_a", ArmourClass.Lord, null, 1000);
        Carrying(new InventoryPiece("elite_a", null, 1));
        Holding(resource: null);

        Assert.IsTrue(_service.BuildOffers(3).Single().CanUpgrade);
    }

    [TestMethod]
    public void BuildOffers_SkipsPiecesWithNoNextNoRecipeOrNoClass()
    {
        Piece("heavy_top", ArmourClass.Heavy, null, 1000);
        Piece("heavy_a", ArmourClass.Heavy, "elite_a", 1000);
        Piece("elite_a", ArmourClass.Elite, null, 1000);   // no elite recipe in this config
        _gate.GetClass("grain").Returns((ArmourClass?)null);
        Carrying(new InventoryPiece("heavy_top", null, 1), new InventoryPiece("heavy_a", null, 1), new InventoryPiece("grain", null, 30));

        Assert.AreEqual(0, _service.BuildOffers(3).Count);
    }

    [TestMethod]
    public void BuildOffers_NextPieceWithoutAClass_IsSkipped()
    {
        Piece("med_a", ArmourClass.Medium, "mystery", 1000);
        _gate.GetClass("mystery").Returns((ArmourClass?)null);
        Carrying(new InventoryPiece("med_a", null, 1));

        Assert.AreEqual(0, _service.BuildOffers(3).Count);
    }

    [TestMethod]
    public void BuildOffers_AnEmptyStack_IsSkipped()
    {
        Piece("med_a", ArmourClass.Medium, "heavy_a", 1000);
        Piece("heavy_a", ArmourClass.Heavy, null, 1000);
        Carrying(new InventoryPiece("med_a", null, 0));

        Assert.AreEqual(0, _service.BuildOffers(1).Count);
    }

    [TestMethod]
    public void BuildOffers_NamedAndLordPieces_AreNeverSources()
    {
        Piece("lord_a", ArmourClass.Lord, "x", 1000);
        Piece("named_a", ArmourClass.Named, "y", 1000);
        Piece("x", ArmourClass.Lord, null, 1000);
        Piece("y", ArmourClass.Lord, null, 1000);
        Carrying(new InventoryPiece("lord_a", null, 1), new InventoryPiece("named_a", null, 1));

        Assert.AreEqual(0, _service.BuildOffers(3).Count);
    }

    [TestMethod]
    public void BuildOffers_DoableOffersComeFirst_ThenById()
    {
        Piece("z_med", ArmourClass.Medium, "z_heavy", 1000);
        Piece("z_heavy", ArmourClass.Heavy, null, 1000);
        Piece("b_elite", ArmourClass.Elite, "b_lord", 1000);
        Piece("b_lord", ArmourClass.Lord, null, 1000);
        Carrying(new InventoryPiece("b_elite", null, 1), new InventoryPiece("z_med", null, 1));

        // At level 1 the lord forge is blocked, so the heavy upgrade leads although its id sorts later.
        var offers = _service.BuildOffers(1);

        CollectionAssert.AreEqual(new[] { "z_med", "b_elite" }, offers.Select(o => o.SourceItemId).ToArray());
    }

    [TestMethod]
    public void BuildOffers_TwoStacksOfOnePieceWithDifferentModifiers_AreSeparateOffers()
    {
        Piece("med_a", ArmourClass.Medium, "heavy_a", 1000);
        Piece("heavy_a", ArmourClass.Heavy, null, 1000);
        Carrying(new InventoryPiece("med_a", null, 1), new InventoryPiece("med_a", "rusty", 2));

        var offers = _service.BuildOffers(1);

        CollectionAssert.AreEquivalent(new[] { null, "rusty" }, offers.Select(o => o.ModifierId).ToArray());
    }

    // --- Execute ---

    [TestMethod]
    public void Execute_Affordable_TakesPiecePaysAndGivesTheUpgradeWithTheSameModifier()
    {
        Carrying(new InventoryPiece("med_a", "sturdy", 1));

        Assert.AreEqual(UpgradeBlock.None, _service.Execute(Offer()));
        Received.InOrder(() =>
        {
            _player.RemovePiece("med_a", "sturdy");
            _player.RemoveItem("ironIngot4", 2);
            _player.ChargeGold(800);
            _player.AddPiece("heavy_a", "sturdy", 1);
        });
    }

    [TestMethod]
    public void Execute_PieceNoLongerCarried_ChargesNothing()
    {
        Assert.AreEqual(UpgradeBlock.NoLongerCarried, _service.Execute(Offer()));
        _player.DidNotReceive().ChargeGold(Arg.Any<int>());
        _player.DidNotReceive().RemoveItem(Arg.Any<string>(), Arg.Any<int>());
    }

    [TestMethod]
    public void Execute_ThePieceRemovalRefused_ChargesNothing()
    {
        Carrying(new InventoryPiece("med_a", "sturdy", 1));
        _player.RemovePiece("med_a", "sturdy").Returns(false);

        Assert.AreEqual(UpgradeBlock.NoLongerCarried, _service.Execute(Offer()));
        _player.DidNotReceive().ChargeGold(Arg.Any<int>());
        _player.DidNotReceive().RemoveItem(Arg.Any<string>(), Arg.Any<int>());
        _player.DidNotReceive().AddPiece(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<int>());
    }

    [TestMethod]
    public void Execute_GoldSpentSinceTheOffer_ChargesNothing()
    {
        Carrying(new InventoryPiece("med_a", "sturdy", 1));
        Holding(gold: 10);

        Assert.AreEqual(UpgradeBlock.NotEnoughGold, _service.Execute(Offer()));
        _player.DidNotReceive().RemovePiece(Arg.Any<string>(), Arg.Any<string?>());
    }

    [TestMethod]
    public void Execute_MetalSoldSinceTheOffer_ChargesNothing()
    {
        Carrying(new InventoryPiece("med_a", "sturdy", 1));
        Holding(steel: 1);

        Assert.AreEqual(UpgradeBlock.MissingMaterials, _service.Execute(Offer()));
        _player.DidNotReceive().RemovePiece(Arg.Any<string>(), Arg.Any<string?>());
    }

    [TestMethod]
    public void Execute_ResourceBalanceShortSinceTheOffer_ChargesNothing()
    {
        Carrying(new InventoryPiece("med_a", "sturdy", 1));
        Holding(resource: 149f);

        Assert.AreEqual(UpgradeBlock.NotEnoughResource, _service.Execute(Offer(resource: 150f)));
        _player.DidNotReceive().RemovePiece(Arg.Any<string>(), Arg.Any<string?>());
        _spender.DidNotReceive().TrySpend(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<float>());
    }

    [TestMethod]
    public void Execute_ResourceSpendRefused_GivesThePieceBackAndChargesNothingElse()
    {
        Carrying(new InventoryPiece("med_a", "sturdy", 1));
        Holding(resource: 500f);
        _spender.TrySpend("main_hero", "empire", "gondor", 150f).Returns(false);

        Assert.AreEqual(UpgradeBlock.NotEnoughResource, _service.Execute(Offer(resource: 150f)));
        _player.Received(1).AddPiece("med_a", "sturdy", 1);
        _player.DidNotReceive().ChargeGold(Arg.Any<int>());
        _player.DidNotReceive().RemoveItem(Arg.Any<string>(), Arg.Any<int>());
    }

    [TestMethod]
    public void Execute_ResourceWithNoKingdomResource_IsWaived()
    {
        Carrying(new InventoryPiece("med_a", "sturdy", 1));
        Holding(resource: null);

        Assert.AreEqual(UpgradeBlock.None, _service.Execute(Offer(resource: 150f)));
        _spender.DidNotReceive().TrySpend(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<float>());
    }

    [TestMethod]
    public void Execute_ResourceSpent_WhenTheBalanceCoversIt()
    {
        Carrying(new InventoryPiece("med_a", "sturdy", 1));
        Holding(resource: 500f);
        _spender.TrySpend("main_hero", "empire", "gondor", 150f).Returns(true);

        Assert.AreEqual(UpgradeBlock.None, _service.Execute(Offer(resource: 150f)));
        _spender.Received(1).TrySpend("main_hero", "empire", "gondor", 150f);
    }
}
