using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.ArmourAcquisition;
using TAOM.Features.ArmourAcquisition.Domain;
using TAOM.Features.CultureMarketplace;

namespace TAOM.Tests.Features.ArmourAcquisition;

/// <summary>
/// "A Lord's Harness Unclaimed", the event route to lord kit: a rare find after a battle won against lords,
/// with a long cooldown. It hands out the right culture's lord kit, a culture with none of its own drawing
/// on its armour donor. (The quest route is the lord's gear ladder, <see cref="LordsLadderService"/>.)
/// </summary>
[TestClass]
public class LordHarnessServiceTests
{
    private const string Hero = "main_hero";

    private ArmourAcquisitionState _state = null!;
    private IArmourGateService _gate = null!;
    private ICultureMarketplaceConfigProvider _marketplace = null!;
    private LordHarnessService _service = null!;

    private sealed class FixedRandom : Random
    {
        private readonly double _value;
        public FixedRandom(double value) { _value = value; }
        public override double NextDouble() => _value;
        public override int Next(int maxValue) => 0;
    }

    [TestInitialize]
    public void Setup()
    {
        _state = new ArmourAcquisitionState();
        var config = Substitute.For<IArmourAcquisitionConfigProvider>();
        config.GetConfig().Returns(ArmourAcquisitionConfig.Default);   // event 8% / 90 days
        _gate = Substitute.For<IArmourGateService>();
        _gate.GetPieces(Arg.Any<ArmourClass>(), Arg.Any<string?>(), Arg.Any<ArmourSlot?>()).Returns(Array.Empty<string>());
        _marketplace = Substitute.For<ICultureMarketplaceConfigProvider>();
        _service = new LordHarnessService(_state, config, _gate, _marketplace);
    }

    // --- Which pieces ---

    [TestMethod]
    public void LordPieceChoices_TheCulturesLordPiecesFirst()
    {
        _gate.GetPieces(ArmourClass.Lord, "gondor").Returns(new[] { "gd_lord_chest" });
        _gate.GetPieces(ArmourClass.Elite, "gondor").Returns(new[] { "gd_elite_chest" });

        CollectionAssert.AreEqual(new[] { "gd_lord_chest" }, (System.Collections.ICollection)_service.LordPieceChoices("gondor"));
    }

    [TestMethod]
    public void LordPieceChoices_ACultureWithoutLordPieces_OffersItsElitePieces()
    {
        _gate.GetPieces(ArmourClass.Elite, "isengard").Returns(new[] { "uruk_elite_chest" });

        CollectionAssert.AreEqual(new[] { "uruk_elite_chest" }, (System.Collections.ICollection)_service.LordPieceChoices("isengard"));
    }

    [TestMethod]
    public void LordPieceChoices_ACultureWithNoArmour_UsesItsArmourDonor()
    {
        // Mike, 2026-09-27: the Armourer's Commission mapping fills the lord kit of a culture with none.
        _marketplace.GetArmourDonor("lindon").Returns("rivendell");
        _gate.GetPieces(ArmourClass.Lord, "rivendell").Returns(new[] { "riv_lord_chest" });
        _gate.GetPieces(ArmourClass.Lord, null).Returns(new[] { "gd_lord_chest", "riv_lord_chest" });

        CollectionAssert.AreEqual(new[] { "riv_lord_chest" }, (System.Collections.ICollection)_service.LordPieceChoices("lindon"));
    }

    [TestMethod]
    public void LordPieceChoices_NeitherLordNorElite_AnyCulturesLordPieces()
    {
        _gate.GetPieces(ArmourClass.Lord, null).Returns(new[] { "gd_lord_chest" });

        CollectionAssert.AreEqual(new[] { "gd_lord_chest" }, (System.Collections.ICollection)_service.LordPieceChoices("nowhere"));
    }

    // --- The event ---

    [TestMethod]
    public void RollHarnessFind_Triggered_PicksTheLordsKitAndStampsTheCooldown()
    {
        _gate.GetPieces(ArmourClass.Lord, "mordor").Returns(new[] { "md_lord_chest" });

        var find = _service.RollHarnessFind(Hero, today: 50, won: true, new[] { "mordor" }, new FixedRandom(0.0));

        Assert.IsNotNull(find);
        Assert.AreEqual(0, find!.Value.LordIndex);
        Assert.AreEqual("md_lord_chest", find.Value.ItemId);
        Assert.AreEqual(50, _state.LordEventLastDay[Hero]);
    }

    [TestMethod]
    public void RollHarnessFind_OnCooldown_FindsNothingAndKeepsTheStamp()
    {
        _gate.GetPieces(ArmourClass.Lord, "mordor").Returns(new[] { "md_lord_chest" });
        _state.LordEventLastDay[Hero] = 10;

        Assert.IsNull(_service.RollHarnessFind(Hero, today: 50, won: true, new[] { "mordor" }, new FixedRandom(0.0)));
        Assert.AreEqual(10, _state.LordEventLastDay[Hero]);
    }

    [TestMethod]
    public void RollHarnessFind_NoPieceForTheCulture_DoesNotStamp()
    {
        Assert.IsNull(_service.RollHarnessFind(Hero, today: 50, won: true, new[] { "nowhere" }, new FixedRandom(0.0)));
        Assert.IsFalse(_state.LordEventLastDay.ContainsKey(Hero));
    }

    [TestMethod]
    public void RollHarnessFind_ABattleLost_FindsNothing()
    {
        _gate.GetPieces(ArmourClass.Lord, "mordor").Returns(new[] { "md_lord_chest" });

        Assert.IsNull(_service.RollHarnessFind(Hero, today: 50, won: false, new[] { "mordor" }, new FixedRandom(0.0)));
    }
}
