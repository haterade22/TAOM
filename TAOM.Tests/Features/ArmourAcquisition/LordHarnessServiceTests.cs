using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Features.ArmourAcquisition;
using TAOM.Features.ArmourAcquisition.Domain;
using TAOM.Features.CultureMarketplace;

namespace TAOM.Tests.Features.ArmourAcquisition;

/// <summary>
/// The quest and event routes to lord kit (Mike, 2026-09-27: forge, quest and event, all three). "The Lord's
/// Harness" is one quest offered at a lord-level armoury of the player's own culture to a player who owns an
/// elite piece; when it succeeds, the hero who owns the quest may claim one piece of lord kit at such an
/// armoury. The event rolls a rare find after a battle won against lords. Both hand out the right culture's
/// lord kit, a culture with none of its own drawing on its armour donor.
/// </summary>
[TestClass]
public class LordHarnessServiceTests
{
    private const string Hero = "main_hero";

    private ArmourAcquisitionState _state = null!;
    private IArmourGateService _gate = null!;
    private IArmouryPlayerAdapter _player = null!;
    private ICultureMarketplaceConfigProvider _marketplace = null!;
    private LordHarnessService _service = null!;
    private readonly List<InventoryPiece> _carried = new();
    private readonly List<string> _worn = new();

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
        config.GetConfig().Returns(ArmourAcquisitionConfig.Default);   // lord level 3, offer cooldown 30, event 8% / 90 days
        _gate = Substitute.For<IArmourGateService>();
        _gate.GetPieces(Arg.Any<ArmourClass>(), Arg.Any<string?>()).Returns(Array.Empty<string>());
        _gate.GetClass("elite_chest").Returns(ArmourClass.Elite);
        _player = Substitute.For<IArmouryPlayerAdapter>();
        _player.HeroId.Returns(Hero);
        _player.CultureId.Returns("gondor");
        _player.ReadInventory().Returns(_ => _carried.ToArray());
        _player.ReadEquippedItemIds().Returns(_ => _worn.ToArray());
        _player.AddPiece(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<int>()).Returns(true);
        _marketplace = Substitute.For<ICultureMarketplaceConfigProvider>();
        _service = new LordHarnessService(_state, config, _gate, _player, _marketplace);
        _carried.Add(new InventoryPiece("elite_chest", null, 1));
    }

    private bool Offer(int townLevel = 3, bool ownCulture = true, bool running = false, int today = 10) =>
        _service.ShouldOffer(Hero, today, townLevel, ownCulture, running);

    // --- Offer ---

    [TestMethod]
    public void ShouldOffer_LordLevelArmouryOfTheHerosCultureAndAnElitePieceCarried_IsTrue()
    {
        Assert.IsTrue(Offer());
    }

    [TestMethod]
    public void ShouldOffer_AnElitePieceWorn_Counts()
    {
        _carried.Clear();
        _worn.Add("elite_chest");

        Assert.IsTrue(Offer());
    }

    [DataTestMethod]
    [DataRow(2, true)]
    [DataRow(3, false)]
    public void ShouldOffer_ArmouryBelowTheLordLevelOrAnotherCulture_IsFalse(int townLevel, bool ownCulture)
    {
        Assert.IsFalse(Offer(townLevel, ownCulture));
    }

    [TestMethod]
    public void ShouldOffer_NoElitePiece_IsFalse()
    {
        _carried.Clear();

        Assert.IsFalse(Offer());
    }

    [TestMethod]
    public void ShouldOffer_TheQuestAlreadyRunning_IsFalse()
    {
        Assert.IsFalse(Offer(running: true));
    }

    [DataTestMethod]
    [DataRow(ArmourAcquisitionState.HarnessReady)]
    [DataRow(ArmourAcquisitionState.HarnessClaimed)]
    public void ShouldOffer_HarnessEarnedAlready_IsFalse(int stage)
    {
        _state.HarnessStage[Hero] = stage;

        Assert.IsFalse(Offer());
    }

    [TestMethod]
    public void ShouldOffer_DeclinedRecently_WaitsOutTheCooldown()
    {
        _service.Decline(Hero, today: 10);

        Assert.IsFalse(Offer(today: 39));
        Assert.IsTrue(Offer(today: 40));
    }

    [TestMethod]
    public void OnAccepted_ForgetsTheRefusal()
    {
        _service.Decline(Hero, today: 10);

        _service.OnAccepted(Hero);

        Assert.IsTrue(Offer(today: 11));
    }

    // --- Completion and claim ---

    [TestMethod]
    public void OnQuestEnded_Success_ReadiesTheOwner_EvenAfterAPlayerSwitch()
    {
        // The player switched from hero_a to main_hero while hero_a's quest ran: the harness is hero_a's.
        Assert.IsTrue(_service.OnQuestEnded("hero_a", success: true));

        Assert.IsTrue(_service.IsReadyToClaim("hero_a"));
        Assert.IsFalse(_service.IsReadyToClaim(Hero));
    }

    [TestMethod]
    public void OnQuestEnded_Failure_LeavesTheQuestToBeOfferedAgain()
    {
        Assert.IsFalse(_service.OnQuestEnded(Hero, success: false));

        Assert.AreEqual(0, _service.Stage(Hero));
        Assert.IsTrue(Offer());
    }

    [TestMethod]
    public void OnQuestEnded_AlreadyClaimed_ChangesNothing()
    {
        _state.HarnessStage[Hero] = ArmourAcquisitionState.HarnessClaimed;

        Assert.IsFalse(_service.OnQuestEnded(Hero, success: true));
        Assert.AreEqual(ArmourAcquisitionState.HarnessClaimed, _service.Stage(Hero));
    }

    [DataTestMethod]
    [DataRow(2, false)]
    [DataRow(3, true)]
    public void CanClaimAt_NeedsTheLordLevel(int townLevel, bool expected)
    {
        Assert.AreEqual(expected, _service.CanClaimAt(townLevel));
    }

    [TestMethod]
    public void Claim_ReadyAndOneOfTheChoices_GivesThePieceAndMarksItClaimed()
    {
        _state.HarnessStage[Hero] = ArmourAcquisitionState.HarnessReady;
        _gate.GetPieces(ArmourClass.Lord, "gondor").Returns(new[] { "gd_lord_chest" });

        Assert.IsTrue(_service.Claim("gd_lord_chest"));
        _player.Received(1).AddPiece("gd_lord_chest", null, 1);
        Assert.AreEqual(ArmourAcquisitionState.HarnessClaimed, _service.Stage(Hero));
    }

    [TestMethod]
    public void Claim_ThePieceCannotBeGiven_StaysClaimable()
    {
        _state.HarnessStage[Hero] = ArmourAcquisitionState.HarnessReady;
        _gate.GetPieces(ArmourClass.Lord, "gondor").Returns(new[] { "gd_lord_chest" });
        _player.AddPiece("gd_lord_chest", null, 1).Returns(false);

        Assert.IsFalse(_service.Claim("gd_lord_chest"));
        Assert.IsTrue(_service.IsReadyToClaim(Hero));
    }

    [TestMethod]
    public void Claim_NotReady_GivesNothing()
    {
        _gate.GetPieces(ArmourClass.Lord, "gondor").Returns(new[] { "gd_lord_chest" });

        Assert.IsFalse(_service.Claim("gd_lord_chest"));
        _player.DidNotReceive().AddPiece(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<int>());
    }

    [TestMethod]
    public void Claim_APieceNotAmongTheChoices_IsRefused()
    {
        _state.HarnessStage[Hero] = ArmourAcquisitionState.HarnessReady;
        _gate.GetPieces(ArmourClass.Lord, "gondor").Returns(new[] { "gd_lord_chest" });

        Assert.IsFalse(_service.Claim("mordor_lord_chest"));
        _player.DidNotReceive().AddPiece(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<int>());
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
