using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Features.ArmourAcquisition;
using TAOM.Features.ArmourAcquisition.Domain;
using TAOM.Features.CultureMarketplace;

namespace TAOM.Tests.Features.ArmourAcquisition;

/// <summary>
/// The lord's gear ladder (#693; Mike, 2026-09-28): one rung per slot, climbed in order. A rung's quest is
/// done by the hero's own deeds, or the rung by handing an armourer the culture's lord's materials; either
/// way the rung then waits to be claimed at an armoury of the lord level. An armour rung awards the
/// culture's lord piece for the slot, else its best elite piece there; the weapon rung a weapon from the
/// config. Materials drop after battles won, the chance rising with the enemies the hero struck down.
/// </summary>
[TestClass]
public class LordsLadderServiceTests
{
    private const string Hero = "main_hero";

    private ArmourAcquisitionState _state = null!;
    private IArmourGateService _gate = null!;
    private IArmouryPlayerAdapter _player = null!;
    private ICultureMarketplaceConfigProvider _marketplace = null!;
    private LordsLadderService _service = null!;
    private readonly List<InventoryPiece> _carried = new();

    private static readonly LadderStep Hands = new(LadderSlot.Hands, "q_hands", 10);
    private static readonly LadderStep Head = new(LadderSlot.Head, "q_head", 30);
    private static readonly LadderStep Weapon = new(LadderSlot.Weapon, "q_weapon", 60);

    private sealed class FixedRandom : Random
    {
        private readonly double _roll;
        public FixedRandom(double roll) { _roll = roll; }
        public override double NextDouble() => _roll;
        public override int Next(int minValue, int maxValue) => maxValue - 1;
    }

    private static ArmourAcquisitionConfig Config()
    {
        var d = ArmourAcquisitionConfig.Default;
        var ladder = new LordsLadderConfig(new[] { Hands, Head, Weapon }, countsKnockouts: true,
            new Dictionary<string, string> { ["gondor"] = "m_gondor", ["rivendell"] = "m_riv" },
            new MaterialDrop(0.1f, 0.01f, 0.6f, 1, 3),
            new Dictionary<string, IReadOnlyList<string>>
            {
                ["gondor"] = new[] { "anduril", "not_loaded_sword" },
                ["rivendell"] = new[] { "riv_sword" },
            },
            new Dictionary<(string Culture, LadderSlot Slot), IReadOnlyList<string>>
            {
                [LordsLadderConfig.PieceKey("arthedain", LadderSlot.Head)] = new[] { "ar_crown_king", "not_loaded_crown" },
            });
        return new ArmourAcquisitionConfig(d.Enabled, d.HeavyLevel, d.EliteLevel, d.LordLevel, d.Recipes, d.NamedWeapons,
            d.LordEventChance, d.LordEventCooldownDays, d.LordEventLeaveRelation,
            d.VisitChancePerDay, d.VisitDurationDays, d.VisitLevelBonus, ladder);
    }

    [TestInitialize]
    public void Setup()
    {
        _state = new ArmourAcquisitionState();
        var config = Substitute.For<IArmourAcquisitionConfigProvider>();
        config.GetConfig().Returns(Config());
        _gate = Substitute.For<IArmourGateService>();
        _gate.GetPieces(Arg.Any<ArmourClass>(), Arg.Any<string?>(), Arg.Any<ArmourSlot?>()).Returns(Array.Empty<string>());
        foreach (var id in new[] { "anduril", "riv_sword" })
            _gate.GetRecord(id).Returns(new ArmourItemRecord(id, ArmourSlot.None, 5, true, "gondor", 5000));
        _gate.GetRecord("ar_crown_king").Returns(new ArmourItemRecord("ar_crown_king", ArmourSlot.Head, 5, false, "arthedain", 9000));
        _player = Substitute.For<IArmouryPlayerAdapter>();
        _player.HeroId.Returns(Hero);
        _player.CultureId.Returns("gondor");
        _player.ReadInventory().Returns(_ => _carried.ToArray());
        _player.RemoveItem(Arg.Any<string>(), Arg.Any<int>()).Returns(true);
        _player.AddPiece(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<int>()).Returns(true);
        _marketplace = Substitute.For<ICultureMarketplaceConfigProvider>();
        _service = new LordsLadderService(_state, config, _gate, _player, _marketplace);
    }

    /// <summary>The hands rung has a piece to award, so a hand-in there can be claimed.</summary>
    private void ClaimableGauntlets() => Piece("gd_lord_gloves", ArmourClass.Lord, ArmourSlot.Hand, 900);

    /// <summary>Marks the hero's claimed rungs, as the save keeps them: one bit per slot.</summary>
    private void Claimed(params LadderSlot[] slots) =>
        _state.LadderClaimed[Hero] = slots.Aggregate(0, (mask, slot) => mask | LadderSlotRules.Bit(slot));

    /// <summary>Marks the hero's done rungs, waiting to be claimed, as the save keeps them: one bit per slot.</summary>
    private void Ready(params LadderSlot[] slots) =>
        _state.LadderReady[Hero] = slots.Aggregate(0, (mask, slot) => mask | LadderSlotRules.Bit(slot));

    /// <summary>The service on a ladder of only these rungs, as after a config edit mid-campaign.</summary>
    private LordsLadderService ServiceWith(params LadderStep[] steps)
    {
        var d = Config();
        var config = Substitute.For<IArmourAcquisitionConfigProvider>();
        config.GetConfig().Returns(new ArmourAcquisitionConfig(d.Enabled, d.HeavyLevel, d.EliteLevel, d.LordLevel, d.Recipes,
            d.NamedWeapons, d.LordEventChance, d.LordEventCooldownDays, d.LordEventLeaveRelation, d.VisitChancePerDay,
            d.VisitDurationDays, d.VisitLevelBonus,
            new LordsLadderConfig(steps, d.Ladder.CountsKnockouts, d.Ladder.Materials, d.Ladder.Drop, d.Ladder.Weapons)));
        return new LordsLadderService(_state, config, _gate, _player, _marketplace);
    }

    private void Piece(string id, ArmourClass cls, ArmourSlot slot, int value, string culture = "gondor")
    {
        _gate.GetRecord(id).Returns(new ArmourItemRecord(id, slot, 5, false, culture, value));
        var existing = _gate.GetPieces(cls, culture, slot).ToList();
        existing.Add(id);
        _gate.GetPieces(cls, culture, slot).Returns(existing.ToArray());
    }

    // --- Where the hero stands ---

    [TestMethod]
    public void CurrentStep_NothingClaimed_IsTheFirstRung()
    {
        Assert.AreSame(Hands, _service.CurrentStep(Hero));
    }

    [TestMethod]
    public void CurrentStep_TwoClaimed_IsTheThirdRung_AndAllClaimed_IsNull()
    {
        Claimed(LadderSlot.Hands, LadderSlot.Head);
        Assert.AreSame(Weapon, _service.CurrentStep(Hero));

        Claimed(LadderSlot.Hands, LadderSlot.Head, LadderSlot.Weapon);
        Assert.IsNull(_service.CurrentStep(Hero));
    }

    [TestMethod]
    public void CurrentStep_TheConfigDropsARung_TheHeroResumesAtTheirFirstUnclaimedSlot()
    {
        // The save keeps which slots are claimed, not how many: a rung removed from the config moves nobody
        // onto a rung they never reached (the Design lens, RCA 2026-09-28).
        var shipped = ArmourAcquisitionConfig.Default;
        var withoutShoulders = new LordsLadderConfig(shipped.Ladder.Steps.Where(s => s.Slot != LadderSlot.Shoulders).ToList(),
            shipped.Ladder.CountsKnockouts, shipped.Ladder.Materials, shipped.Ladder.Drop, shipped.Ladder.Weapons);
        var config = Substitute.For<IArmourAcquisitionConfigProvider>();
        config.GetConfig().Returns(new ArmourAcquisitionConfig(shipped.Enabled, shipped.HeavyLevel, shipped.EliteLevel,
            shipped.LordLevel, shipped.Recipes, shipped.NamedWeapons, shipped.LordEventChance, shipped.LordEventCooldownDays,
            shipped.LordEventLeaveRelation, shipped.VisitChancePerDay, shipped.VisitDurationDays, shipped.VisitLevelBonus,
            withoutShoulders));
        Claimed(LadderSlot.Hands, LadderSlot.Legs, LadderSlot.Shoulders);

        var step = new LordsLadderService(_state, config, _gate, _player, _marketplace).CurrentStep(Hero);

        Assert.AreEqual(LadderSlot.Head, step?.Slot);
    }

    [TestMethod]
    public void IsReady_TheReadyRungDroppedFromTheConfig_ReadiesNoOtherRung()
    {
        // Readiness belongs to the slot whose deeds were done: a config that drops that rung gives the next one no
        // free claim (convergence review D-5, RCA 2026-09-28).
        Piece("gd_elite_helm", ArmourClass.Elite, ArmourSlot.Head, 700);
        Ready(LadderSlot.Hands);
        var service = ServiceWith(Head, Weapon);

        Assert.AreSame(Head, service.CurrentStep(Hero));
        Assert.IsFalse(service.IsReady(Hero));
        Assert.IsFalse(service.Claim("gd_elite_helm"));
        Assert.AreEqual("q_head", service.QuestToCredit(Hero));
    }

    [TestMethod]
    public void IsReady_TheLadderReordered_FollowsTheSlotThatWasDone()
    {
        Ready(LadderSlot.Hands);
        var service = ServiceWith(Head, Hands, Weapon);

        Assert.AreSame(Head, service.CurrentStep(Hero));
        Assert.IsFalse(service.IsReady(Hero), "the head rung's deeds are not done");

        Claimed(LadderSlot.Head);
        Assert.AreSame(Hands, service.CurrentStep(Hero));
        Assert.IsTrue(service.IsReady(Hero), "the hands rung kept its readiness");
    }

    [TestMethod]
    public void CanStart_ARungNotYetDoneWhoseQuestIsNotRunning_IsTrue()
    {
        Assert.IsTrue(_service.CanStart(Hero, questRunning: false));
    }

    [TestMethod]
    public void CanStart_TheQuestRunning_TheRungDone_OrTheLadderClimbed_IsFalse()
    {
        Assert.IsFalse(_service.CanStart(Hero, questRunning: true));

        Ready(LadderSlot.Hands);
        Assert.IsFalse(_service.CanStart(Hero, questRunning: false), "a done rung waits for its claim");

        _state.LadderReady.Clear();
        Claimed(LadderSlot.Hands, LadderSlot.Head, LadderSlot.Weapon);
        Assert.IsFalse(_service.CanStart(Hero, questRunning: false));
    }

    [TestMethod]
    public void QuestToCredit_TheCurrentRungsQuest_UntilItIsDone()
    {
        Assert.AreEqual("q_hands", _service.QuestToCredit(Hero));

        Ready(LadderSlot.Hands);
        Assert.IsNull(_service.QuestToCredit(Hero));
    }

    // --- The deeds route ---

    [TestMethod]
    public void OnQuestEnded_SuccessOnTheCurrentRung_ReadiesItForTheQuestsOwner()
    {
        // The owner, not the current main hero: a Player Switcher change must not strand the rung.
        var step = _service.OnQuestEnded("other_hero", "q_hands", success: true);

        Assert.AreSame(Hands, step);
        Assert.IsTrue(_service.IsReady("other_hero"));
        Assert.IsFalse(_service.IsReady(Hero));
    }

    [TestMethod]
    public void OnQuestEnded_AFailureOrAnotherRungsQuest_ChangesNothing()
    {
        Assert.IsNull(_service.OnQuestEnded(Hero, "q_hands", success: false));
        Assert.IsNull(_service.OnQuestEnded(Hero, "q_head", success: true), "the hero is on the hands rung");
        Assert.IsNull(_service.OnQuestEnded(Hero, "taom_cq_warrior_1", success: true), "a career quest, not a rung's");
        Assert.IsNull(_service.OnQuestEnded(null, "q_hands", success: true));
        Assert.IsFalse(_service.IsReady(Hero));
    }

    [TestMethod]
    public void OnQuestEnded_TheRungAlreadyDone_ReturnsNull()
    {
        // Handing in materials completes the running quest too; the rung is readied once.
        Ready(LadderSlot.Hands);

        Assert.IsNull(_service.OnQuestEnded(Hero, "q_hands", success: true));
    }

    // --- The materials route ---

    [TestMethod]
    public void MaterialFor_TheCulturesOwn_ElseItsArmourDonors_ElseNone()
    {
        _marketplace.GetArmourDonor("lindon").Returns("rivendell");

        Assert.AreEqual("m_gondor", _service.MaterialFor("gondor"));
        Assert.AreEqual("m_riv", _service.MaterialFor("lindon"));
        Assert.IsNull(_service.MaterialFor("nowhere"));
        Assert.IsNull(_service.MaterialFor(null));
    }

    [TestMethod]
    public void HandInStatus_CountsEveryStackOfTheMaterial()
    {
        ClaimableGauntlets();
        _carried.Add(new InventoryPiece("m_gondor", null, 4));
        _carried.Add(new InventoryPiece("m_gondor", "fine", 3));
        _carried.Add(new InventoryPiece("m_riv", null, 50));

        var status = _service.HandInStatus();

        Assert.IsNotNull(status);
        Assert.AreEqual(("m_gondor", 10, 7), (status!.MaterialId, status.Needed, status.Carried));
        Assert.IsFalse(status.IsEnough);
    }

    [TestMethod]
    public void HandInStatus_TheRungDoneTheLadderClimbedOrNoMaterial_IsNull()
    {
        Ready(LadderSlot.Hands);
        Assert.IsNull(_service.HandInStatus());

        _state.LadderReady.Clear();
        Claimed(LadderSlot.Hands, LadderSlot.Head, LadderSlot.Weapon);
        Assert.IsNull(_service.HandInStatus());

        _state.LadderClaimed.Clear();
        _player.CultureId.Returns("nowhere");
        Assert.IsNull(_service.HandInStatus());
    }

    [TestMethod]
    public void HandIn_Enough_TakesTheMaterialsAndReadiesTheRung()
    {
        ClaimableGauntlets();
        _carried.Add(new InventoryPiece("m_gondor", null, 12));

        Assert.IsTrue(_service.HandIn());

        _player.Received(1).RemoveItem("m_gondor", 10);
        Assert.IsTrue(_service.IsReady(Hero));
    }

    [TestMethod]
    public void HandIn_Short_TakesNothing()
    {
        ClaimableGauntlets();
        _carried.Add(new InventoryPiece("m_gondor", null, 9));

        Assert.IsFalse(_service.HandIn());

        _player.DidNotReceive().RemoveItem(Arg.Any<string>(), Arg.Any<int>());
        Assert.IsFalse(_service.IsReady(Hero));
    }

    [TestMethod]
    public void HandIn_TheRemovalFails_TheRungIsNotDone()
    {
        ClaimableGauntlets();
        _carried.Add(new InventoryPiece("m_gondor", null, 12));
        _player.RemoveItem("m_gondor", 10).Returns(false);

        Assert.IsFalse(_service.HandIn());
        Assert.IsFalse(_service.IsReady(Hero));
    }

    // --- What a rung awards ---

    [TestMethod]
    public void RewardChoices_ArmourRung_EveryLordPieceOfTheCultureForThatSlot()
    {
        Piece("gd_lord_gloves_a", ArmourClass.Lord, ArmourSlot.Hand, 900);
        Piece("gd_lord_gloves_b", ArmourClass.Lord, ArmourSlot.Hand, 800);
        Piece("gd_elite_gloves", ArmourClass.Elite, ArmourSlot.Hand, 5000);

        CollectionAssert.AreEquivalent(new[] { "gd_lord_gloves_a", "gd_lord_gloves_b" },
            _service.RewardChoices(Hands, "gondor").ToArray());
    }

    [TestMethod]
    public void RewardChoices_NoLordPieceInTheSlot_TheSingleBestElitePiece()
    {
        Piece("gd_elite_helm_a", ArmourClass.Elite, ArmourSlot.Head, 700);
        Piece("gd_elite_helm_b", ArmourClass.Elite, ArmourSlot.Head, 1200);
        Piece("gd_lord_gloves", ArmourClass.Lord, ArmourSlot.Hand, 900);

        CollectionAssert.AreEqual(new[] { "gd_elite_helm_b" }, _service.RewardChoices(Head, "gondor").ToArray());
    }

    [TestMethod]
    public void RewardChoices_NeitherLordNorEliteInTheSlot_TheBestHeavyPiece()
    {
        // The Haradrim own no elite head or body piece.
        Piece("hr_heavy_helm_a", ArmourClass.Heavy, ArmourSlot.Head, 300, culture: "aserai");
        Piece("hr_heavy_helm_b", ArmourClass.Heavy, ArmourSlot.Head, 450, culture: "aserai");

        CollectionAssert.AreEqual(new[] { "hr_heavy_helm_b" }, _service.RewardChoices(Head, "aserai").ToArray());
    }

    [TestMethod]
    public void RewardChoices_NothingInTheCultureKitForTheSlot_AnyCulturesLordPiecesThere()
    {
        _gate.GetPieces(ArmourClass.Lord, null, ArmourSlot.Hand).Returns(new[] { "gd_lord_gloves" });

        CollectionAssert.AreEqual(new[] { "gd_lord_gloves" }, _service.RewardChoices(Hands, "nowhere").ToArray());
    }

    [TestMethod]
    public void RewardChoices_ACultureWithNoArmour_UsesItsArmourDonor()
    {
        _marketplace.GetArmourDonor("lindon").Returns("rivendell");
        Piece("riv_lord_helm", ArmourClass.Lord, ArmourSlot.Head, 900, culture: "rivendell");

        CollectionAssert.AreEqual(new[] { "riv_lord_helm" }, _service.RewardChoices(Head, "lindon").ToArray());
    }

    [TestMethod]
    public void RewardChoices_WeaponRung_TheCulturesConfiguredWeaponsThatAreLoaded()
    {
        // not_loaded_sword has no record: the Armory no longer ships it.
        CollectionAssert.AreEqual(new[] { "anduril" }, _service.RewardChoices(Weapon, "gondor").ToArray());
    }

    [TestMethod]
    public void RewardChoices_WeaponRung_ACultureWithoutPicks_UsesItsArmourDonors()
    {
        _marketplace.GetArmourDonor("lindon").Returns("rivendell");

        CollectionAssert.AreEqual(new[] { "riv_sword" }, _service.RewardChoices(Weapon, "lindon").ToArray());
    }

    [TestMethod]
    public void RewardChoices_AConfiguredPieceForTheCultureAndSlot_IsOfferedFirst()
    {
        Piece("ar_lord_helm", ArmourClass.Lord, ArmourSlot.Head, 900, culture: "arthedain");

        CollectionAssert.AreEqual(new[] { "ar_crown_king", "ar_lord_helm" }, _service.RewardChoices(Head, "arthedain").ToArray());
    }

    [TestMethod]
    public void RewardChoices_AConfiguredPiece_IsNotRepeatedWhenTheKitAlsoListsIt()
    {
        Piece("ar_lord_helm", ArmourClass.Lord, ArmourSlot.Head, 900, culture: "arthedain");
        Piece("ar_crown_king", ArmourClass.Lord, ArmourSlot.Head, 9000, culture: "arthedain");

        CollectionAssert.AreEqual(new[] { "ar_crown_king", "ar_lord_helm" }, _service.RewardChoices(Head, "arthedain").ToArray());
    }

    [TestMethod]
    public void RewardChoices_AConfiguredPiece_CultureIdIsComparedWithoutCase()
    {
        CollectionAssert.AreEqual(new[] { "ar_crown_king" }, _service.RewardChoices(Head, "Arthedain").ToArray());
    }

    [TestMethod]
    public void RewardChoices_AConfiguredPieceOfAnotherCulture_IsNotOffered()
    {
        Piece("gd_lord_helm", ArmourClass.Lord, ArmourSlot.Head, 900);

        CollectionAssert.AreEqual(new[] { "gd_lord_helm" }, _service.RewardChoices(Head, "gondor").ToArray());
    }

    [TestMethod]
    public void RewardChoices_AConfiguredPiece_IsNotOfferedThroughTheArmourDonor()
    {
        // Arthedain's donor is Gondor; a Gondor hero must not see the crown, and a hero whose donor is Arthedain
        // would not either: the pieces are looked up for the hero's own culture only.
        _marketplace.GetArmourDonor("arthedain").Returns("gondor");
        _marketplace.GetArmourDonor("lindon").Returns("arthedain");
        Piece("gd_lord_helm", ArmourClass.Lord, ArmourSlot.Head, 900);

        CollectionAssert.AreEqual(new[] { "ar_crown_king", "gd_lord_helm" }, _service.RewardChoices(Head, "arthedain").ToArray());
        CollectionAssert.AreEqual(new[] { "gd_lord_helm" }, _service.RewardChoices(Head, "gondor").ToArray());
        Assert.IsFalse(_service.RewardChoices(Head, "lindon").Contains("ar_crown_king"));
    }

    [TestMethod]
    public void RewardChoices_AConfiguredPiece_IsNotOfferedOnAnotherSlot()
    {
        ClaimableGauntlets();
        Piece("ar_lord_gloves", ArmourClass.Lord, ArmourSlot.Hand, 900, culture: "arthedain");

        CollectionAssert.AreEqual(new[] { "ar_lord_gloves" }, _service.RewardChoices(Hands, "arthedain").ToArray());
    }

    [TestMethod]
    public void RewardChoices_AConfiguredPieceThatIsNotLoaded_IsSkipped()
    {
        // not_loaded_crown has no record: the Armory no longer ships it.
        Assert.IsFalse(_service.RewardChoices(Head, "arthedain").Contains("not_loaded_crown"));
    }

    [TestMethod]
    public void RewardChoices_AConfiguredPieceOfAnotherSlot_IsNotOffered()
    {
        // A row naming a chest on the head rung would let the chest settle the head slot.
        _gate.GetRecord("ar_crown_king").Returns(new ArmourItemRecord("ar_crown_king", ArmourSlot.Body, 5, false, "arthedain", 9000));

        Assert.IsFalse(_service.RewardChoices(Head, "arthedain").Contains("ar_crown_king"));
    }

    [TestMethod]
    public void RewardChoices_AConfiguredPieceAndNothingElseInTheKit_FallsBackToAnyLordPieces()
    {
        _gate.GetRecord("ar_crown_king").Returns((ArmourItemRecord?)null);
        _gate.GetPieces(ArmourClass.Lord, null, ArmourSlot.Head).Returns(new[] { "any_lord_helm" });

        CollectionAssert.AreEqual(new[] { "any_lord_helm" }, _service.RewardChoices(Head, "arthedain").ToArray());
    }

    // --- The claim ---

    [TestMethod]
    [DataRow(2, false)]
    [DataRow(3, true)]
    public void CanClaimAt_NeedsTheLordLevel(int townLevel, bool expected)
    {
        Assert.AreEqual(expected, _service.CanClaimAt(townLevel));
    }

    [TestMethod]
    public void Claim_ReadyAndOneOfTheChoices_GivesThePieceAndClimbsARung()
    {
        Piece("gd_lord_gloves", ArmourClass.Lord, ArmourSlot.Hand, 900);
        Ready(LadderSlot.Hands);

        Assert.IsTrue(_service.Claim("gd_lord_gloves"));

        _player.Received(1).AddPiece("gd_lord_gloves", null, 1);
        Assert.AreEqual(LadderSlotRules.Bit(LadderSlot.Hands), _state.LadderClaimed[Hero], "the hands slot is claimed");
        Assert.IsFalse(_service.IsReady(Hero));
        Assert.IsFalse(_state.LadderReady.ContainsKey(Hero), "no rung is left waiting");
        Assert.AreSame(Head, _service.CurrentStep(Hero));
    }

    [TestMethod]
    public void Claim_TheConfiguredNamedPiece_GivesItToTheCulturesHero()
    {
        // The crown is classed named in armour_classes.xml; Claim checks only the rung's choices, not a class.
        _player.CultureId.Returns("arthedain");
        Claimed(LadderSlot.Hands);
        Ready(LadderSlot.Head);

        Assert.IsTrue(_service.Claim("ar_crown_king"));

        _player.Received(1).AddPiece("ar_crown_king", null, 1);
        Assert.AreEqual(LadderSlotRules.Bit(LadderSlot.Hands) | LadderSlotRules.Bit(LadderSlot.Head), _state.LadderClaimed[Hero]);
    }

    [TestMethod]
    public void Claim_TheConfiguredPiece_IsRefusedToAnotherCulturesHero()
    {
        Claimed(LadderSlot.Hands);
        Ready(LadderSlot.Head);

        Assert.IsFalse(_service.Claim("ar_crown_king"));
        _player.DidNotReceive().AddPiece(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<int>());
    }

    [TestMethod]
    public void Claim_NotReady_GivesNothing()
    {
        Piece("gd_lord_gloves", ArmourClass.Lord, ArmourSlot.Hand, 900);

        Assert.IsFalse(_service.Claim("gd_lord_gloves"));
        _player.DidNotReceive().AddPiece(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<int>());
    }

    [TestMethod]
    public void Claim_APieceNotAmongTheChoices_IsRefused()
    {
        Piece("gd_lord_gloves", ArmourClass.Lord, ArmourSlot.Hand, 900);
        Ready(LadderSlot.Hands);

        Assert.IsFalse(_service.Claim("md_lord_gloves"));
        Assert.IsTrue(_service.IsReady(Hero));
    }

    [TestMethod]
    public void Claim_ThePieceCannotBeGiven_StaysReady()
    {
        Piece("gd_lord_gloves", ArmourClass.Lord, ArmourSlot.Hand, 900);
        Ready(LadderSlot.Hands);
        _player.AddPiece("gd_lord_gloves", null, 1).Returns(false);

        Assert.IsFalse(_service.Claim("gd_lord_gloves"));
        Assert.IsTrue(_service.IsReady(Hero));
        Assert.IsFalse(_state.LadderClaimed.ContainsKey(Hero));
    }

    // --- Material drops ---

    [TestMethod]
    public void RollMaterialDrop_AWonBattle_LuckyRoll_TheCulturesMaterial()
    {
        var drop = _service.RollMaterialDrop(Hero, won: true, heroKills: 0, "gondor", new FixedRandom(0.05));

        Assert.IsNotNull(drop);
        Assert.AreEqual(("m_gondor", 3), drop!.Value);
    }

    [TestMethod]
    public void RollMaterialDrop_ALostBattle_OrAnUnluckyRoll_Nothing()
    {
        Assert.IsNull(_service.RollMaterialDrop(Hero, won: false, heroKills: 500, "gondor", new FixedRandom(0.0)));
        Assert.IsNull(_service.RollMaterialDrop(Hero, won: true, heroKills: 0, "gondor", new FixedRandom(0.2)));
    }

    [TestMethod]
    public void RollMaterialDrop_MoreKills_RaiseTheChance()
    {
        // 0.1 + 0.01 per ten: 50 kills make 0.15 (a 0.155 roll misses), 60 make 0.16 (it hits).
        Assert.IsNull(_service.RollMaterialDrop(Hero, won: true, heroKills: 50, "gondor", new FixedRandom(0.155)));
        Assert.IsNotNull(_service.RollMaterialDrop(Hero, won: true, heroKills: 60, "gondor", new FixedRandom(0.155)));
    }

    [TestMethod]
    public void RollMaterialDrop_ACultureWithNoMaterial_UsesItsDonors_ElseNothing()
    {
        _marketplace.GetArmourDonor("lindon").Returns("rivendell");

        Assert.AreEqual("m_riv", _service.RollMaterialDrop(Hero, won: true, heroKills: 0, "lindon", new FixedRandom(0.0))!.Value.ItemId);
        Assert.IsNull(_service.RollMaterialDrop(Hero, won: true, heroKills: 0, "nowhere", new FixedRandom(0.0)));
    }

    [TestMethod]
    public void RollMaterialDrop_EveryRungClaimed_FindsNothing()
    {
        // Nothing spends a lord's material once the ladder is climbed (RCA 2026-09-28 row 3).
        Claimed(LadderSlot.Hands, LadderSlot.Head, LadderSlot.Weapon);

        Assert.IsNull(_service.RollMaterialDrop(Hero, won: true, heroKills: 500, "gondor", new FixedRandom(0.0)));
    }

    [TestMethod]
    public void RollMaterialDrop_ARungWaitingToBeClaimed_StillFinds()
    {
        // A later rung will want materials too.
        Ready(LadderSlot.Hands);

        Assert.IsNotNull(_service.RollMaterialDrop(Hero, won: true, heroKills: 0, "gondor", new FixedRandom(0.0)));
    }

    [TestMethod]
    public void RollMaterialDrop_TheLastRungWaitingToBeClaimed_FindsNothing()
    {
        // With only the done last rung left, no rung will take a material (convergence review D-4).
        Claimed(LadderSlot.Hands, LadderSlot.Head);
        Ready(LadderSlot.Weapon);

        Assert.IsNull(_service.RollMaterialDrop(Hero, won: true, heroKills: 500, "gondor", new FixedRandom(0.0)));
    }

    // --- Edge cases ---

    [TestMethod]
    public void HandInStatus_ARungWithNoRewardChoices_IsNull()
    {
        // A hand-in must never ready a rung that cannot be claimed (RCA 2026-09-28 row 9).
        _player.CultureId.Returns("nowhere_culture");
        var config = Substitute.For<IArmourAcquisitionConfigProvider>();
        var d = Config();
        var ladder = new LordsLadderConfig(d.Ladder.Steps, true,
            new Dictionary<string, string> { ["nowhere_culture"] = "m_nowhere" }, d.Ladder.Drop,
            new Dictionary<string, IReadOnlyList<string>>());
        config.GetConfig().Returns(new ArmourAcquisitionConfig(d.Enabled, d.HeavyLevel, d.EliteLevel, d.LordLevel, d.Recipes,
            d.NamedWeapons, d.LordEventChance, d.LordEventCooldownDays, d.LordEventLeaveRelation, d.VisitChancePerDay,
            d.VisitDurationDays, d.VisitLevelBonus, ladder));
        var service = new LordsLadderService(_state, config, _gate, _player, _marketplace);
        Claimed(LadderSlot.Hands, LadderSlot.Head);   // the weapon rung: no picks for this culture, no donor
        _carried.Add(new InventoryPiece("m_nowhere", null, 99));

        Assert.IsNull(service.HandInStatus());
        Assert.IsFalse(service.HandIn());
        _player.DidNotReceive().RemoveItem(Arg.Any<string>(), Arg.Any<int>());
    }

    [TestMethod]
    public void RewardChoices_WeaponRung_NoPicksAndNoDonor_IsEmpty()
    {
        Assert.AreEqual(0, _service.RewardChoices(Weapon, "nowhere").Count);
    }

    [TestMethod]
    public void Claim_TheLastRung_ClimbsOffTheLadder()
    {
        Claimed(LadderSlot.Hands, LadderSlot.Head);
        Ready(LadderSlot.Weapon);

        Assert.IsTrue(_service.Claim("anduril"));

        Assert.AreEqual(LadderSlotRules.Bit(LadderSlot.Hands) | LadderSlotRules.Bit(LadderSlot.Head)
                        | LadderSlotRules.Bit(LadderSlot.Weapon), _state.LadderClaimed[Hero]);
        Assert.IsNull(_service.CurrentStep(Hero));
        Assert.IsFalse(_service.CanStart(Hero, questRunning: false));
        Assert.IsNull(_service.HandInStatus());
    }

    [TestMethod]
    public void CurrentStep_EverySlotClaimed_IsNull_EvenWithSlotsTheConfigNoLongerLists()
    {
        _state.LadderClaimed[Hero] = LadderSlotRules.AllBits;

        Assert.IsNull(_service.CurrentStep(Hero));
        Assert.IsNull(_service.QuestToCredit(Hero));
    }

    [TestMethod]
    public void OnQuestEnded_AnEmptyOwner_ChangesNothing()
    {
        Assert.IsNull(_service.OnQuestEnded("", "q_hands", success: true));
        Assert.AreEqual(0, _state.LadderReady.Count);
    }
}
