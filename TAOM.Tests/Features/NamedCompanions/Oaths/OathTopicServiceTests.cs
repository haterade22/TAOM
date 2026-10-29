using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.NamedCompanions;
using TAOM.Features.NamedCompanions.Domain;
using TAOM.Features.NamedCompanions.Oaths;

namespace TAOM.Tests.Features.NamedCompanions.Oaths;

[TestClass]
public class OathTopicServiceTests
{
    private const string Id = "named_companion_aragorn";
    private const int Today = 100;

    private INamedCompanionConfigProvider _config = null!;
    private ICoopSessionProvider _coop = null!;
    private OathLedger _ledger = null!;
    private INamedCompanionAdapter _adapter = null!;
    private ICareerPurseAdapter _purse = null!;
    private IPartyItemRosterAdapter _roster = null!;
    private IModLogger _logger = null!;
    private NamedCompanionDefinition _companion = null!;
    private OathTopicService _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _config = Substitute.For<INamedCompanionConfigProvider>();
        _coop = Substitute.For<ICoopSessionProvider>();
        _ledger = new OathLedger(_coop);
        _adapter = Substitute.For<INamedCompanionAdapter>();
        _purse = Substitute.For<ICareerPurseAdapter>();
        _roster = Substitute.For<IPartyItemRosterAdapter>();
        _logger = Substitute.For<IModLogger>();
        _companion = new NamedCompanionDefinition { CharacterId = Id, Enabled = true, Topics = new List<TopicDefinition>() };
        _config.GetCompanions().Returns(new List<NamedCompanionDefinition> { _companion });
        _purse.GrantGold(Arg.Any<int>()).Returns(true);
        _purse.Gold.Returns(1000);
        _purse.ChargeGold(Arg.Any<int>()).Returns(true);
        _adapter.GetRelationWithPlayer(Id).Returns(0);
        _sut = new OathTopicService(_config, _ledger, _adapter, _purse, _roster, _logger);
    }

    private TopicDefinition Add(string id, TopicWhen when = TopicWhen.Any, int minRelation = -100,
        bool once = false, int cooldown = 0, int cost = 0, int reward = 0, int gain = 0, int maxRelation = 100, int chance = 100)
    {
        var topic = new TopicDefinition
        {
            Id = id, PromptStringId = "p_" + id, ReplyStringId = "r_" + id, WhenKind = when,
            MinRelation = minRelation, Once = once, CooldownDays = cooldown, GoldCost = cost,
            GoldReward = reward, RelationGain = gain, MaxRelation = maxRelation, RandomChance = chance,
            GiveItems = new List<ItemCostDefinition>(), TakeItems = new List<ItemCostDefinition>(),
            RequiresFlags = new List<string>(), SetsFlags = new List<string>(),
        };
        _companion.Topics.Add(topic);
        return topic;
    }

    [TestMethod]
    public void Visible_AnyKind_ShowsRecruitedAndNot()
    {
        Add("t");

        Assert.AreEqual(1, _sut.Visible(Id, recruited: true, Today).Count);
        Assert.AreEqual(1, _sut.Visible(Id, recruited: false, Today).Count);
    }

    [TestMethod]
    public void Visible_RecruitedKind_OnlyWhenRecruited()
    {
        Add("t", TopicWhen.Recruited);

        Assert.AreEqual(1, _sut.Visible(Id, recruited: true, Today).Count);
        Assert.AreEqual(0, _sut.Visible(Id, recruited: false, Today).Count);
    }

    [TestMethod]
    public void Visible_UnrecruitedKind_OnlyWhenNotRecruited()
    {
        Add("t", TopicWhen.Unrecruited);

        Assert.AreEqual(0, _sut.Visible(Id, recruited: true, Today).Count);
        Assert.AreEqual(1, _sut.Visible(Id, recruited: false, Today).Count);
    }

    [TestMethod]
    public void Visible_RelationBelowMinimum_Hidden()
    {
        Add("t", minRelation: 10);
        _adapter.GetRelationWithPlayer(Id).Returns(9);

        Assert.AreEqual(0, _sut.Visible(Id, true, Today).Count);
    }

    [TestMethod]
    public void Visible_RelationAtMinimum_Shown()
    {
        Add("t", minRelation: 10);
        _adapter.GetRelationWithPlayer(Id).Returns(10);

        Assert.AreEqual(1, _sut.Visible(Id, true, Today).Count);
    }

    [TestMethod]
    public void Visible_RequiredFlagUnset_Hidden_ThenSet_Shown()
    {
        Add("t").RequiresFlags.Add("gimli.erebor");

        Assert.AreEqual(0, _sut.Visible(Id, true, Today).Count);

        _ledger.Set("gimli.erebor");
        Assert.AreEqual(1, _sut.Visible(Id, true, Today).Count);
    }

    [TestMethod]
    public void Visible_OnceTopicDone_Hidden()
    {
        Add("t", once: true);
        _ledger.SetTopicLastDay(Id, "t", 4);

        Assert.AreEqual(0, _sut.Visible(Id, true, Today).Count);
    }

    [TestMethod]
    public void Visible_NonOnceTopicUsed_StillShown()
    {
        Add("t", once: false, cooldown: 3);
        _ledger.SetTopicLastDay(Id, "t", 4);

        Assert.AreEqual(1, _sut.Visible(Id, true, Today).Count);
    }

    [TestMethod]
    public void Visible_UnknownOrDisabledCompanion_Empty()
    {
        Add("t");

        Assert.AreEqual(0, _sut.Visible("nobody", true, Today).Count);
        _companion.Enabled = false;
        Assert.AreEqual(0, _sut.Visible(Id, true, Today).Count);
    }

    [TestMethod]
    public void Blocker_Free_IsNull()
    {
        var t = Add("t");

        Assert.AreEqual(TopicBlocker.None, _sut.Blocker(Id, t, 10, false));
    }

    [TestMethod]
    public void Blocker_CoopClient_IsCoop()
    {
        var t = Add("t");

        Assert.AreEqual(TopicBlocker.Coop, _sut.Blocker(Id, t, 10, true));
    }

    [DataTestMethod]
    [DataRow(12, TopicBlocker.Cooldown)]   // last 10 + cd 3 = 13 > 12
    [DataRow(13, TopicBlocker.None)]       // 13 > 13 is false: allowed again on day N + cd
    [DataRow(14, TopicBlocker.None)]
    public void Blocker_CooldownBoundary(int today, TopicBlocker expected)
    {
        var t = Add("t", cooldown: 3);
        _ledger.SetTopicLastDay(Id, "t", 10);

        Assert.AreEqual(expected, _sut.Blocker(Id, t, today, false));
    }

    [TestMethod]
    public void Blocker_CooldownNeverUsed_IsNull()
    {
        var t = Add("t", cooldown: 3);

        Assert.AreEqual(TopicBlocker.None, _sut.Blocker(Id, t, 0, false));
    }

    [TestMethod]
    public void Blocker_PurseShort_IsPurse()
    {
        var t = Add("t", cost: 500);
        _purse.Gold.Returns(499);

        Assert.AreEqual(TopicBlocker.Purse, _sut.Blocker(Id, t, 1, false));
    }

    [TestMethod]
    public void Blocker_PurseExact_IsNull()
    {
        var t = Add("t", cost: 500);
        _purse.Gold.Returns(500);

        Assert.AreEqual(TopicBlocker.None, _sut.Blocker(Id, t, 1, false));
    }

    [TestMethod]
    public void Blocker_NullTopic_IsUnknown()
    {
        Assert.AreEqual(TopicBlocker.Unknown, _sut.Blocker(Id, null, 1, false));
    }

    [TestMethod]
    public void Complete_PaysCharge_Reward_Relation_Flags_Once_Cooldown()
    {
        var t = Add("t", once: true, cooldown: 5, cost: 200, gain: 3);
        t.SetsFlags.AddRange(new[] { "a.one", "a.two" });

        Assert.IsTrue(_sut.Complete(Id, "t", 20, false));

        _purse.Received(1).ChargeGold(200);
        _purse.DidNotReceive().GrantGold(Arg.Any<int>());
        _adapter.Received(1).ChangeRelationWithPlayer(Id, 3);
        Assert.IsTrue(_ledger.Has("a.one"));
        Assert.IsTrue(_ledger.Has("a.two"));
        Assert.IsTrue(_ledger.TryGetTopicLastDay(Id, "t", out var day));
        Assert.AreEqual(20, day);
    }

    [TestMethod]
    public void Complete_Reward_IsGrantedThroughThePurse()
    {
        Add("t", once: true, reward: 150);

        Assert.IsTrue(_sut.Complete(Id, "t", 1, false));

        _purse.Received(1).GrantGold(150);
        _purse.DidNotReceive().ChargeGold(Arg.Any<int>());
    }

    [TestMethod]
    public void Complete_NoRelationGain_LeavesRelationAlone()
    {
        Add("t");

        Assert.IsTrue(_sut.Complete(Id, "t", 1, false));

        _adapter.DidNotReceive().ChangeRelationWithPlayer(Arg.Any<string>(), Arg.Any<int>());
    }

    [TestMethod]
    public void Complete_AlwaysRecordsTheDay_AndANonOnceTopicStaysVisible()
    {
        Add("t");

        Assert.IsTrue(_sut.Complete(Id, "t", 7, false));

        Assert.IsTrue(_ledger.TryGetTopicLastDay(Id, "t", out var day));
        Assert.AreEqual(7, day);
        Assert.AreEqual(1, _sut.Visible(Id, true, Today).Count);
    }

    [TestMethod]
    public void Complete_OnceTopic_DisappearsAfterwards()
    {
        Add("t", once: true);

        Assert.IsTrue(_sut.Complete(Id, "t", 7, false));

        Assert.AreEqual(0, _sut.Visible(Id, true, Today).Count);
    }

    [TestMethod]
    public void Complete_RewardRefusedByThePurse_StillFinishesAndLogs()
    {
        Add("t", once: true, reward: 150);
        _purse.GrantGold(150).Returns(false);

        Assert.IsTrue(_sut.Complete(Id, "t", 1, false));

        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("grant")));
    }

    [TestMethod]
    public void Visible_ReadsTheRelationOnce()
    {
        Add("a");
        Add("b");

        _sut.Visible(Id, true, Today);

        _adapter.Received(1).GetRelationWithPlayer(Id);
    }

    [TestMethod]
    public void Complete_OnCoopClient_ChangesNothing()
    {
        Add("t", once: true, cost: 10, reward: 0, gain: 2).SetsFlags.Add("x");
        _coop.IsCoopClient.Returns(true);

        Assert.IsFalse(_sut.Complete(Id, "t", 1, true));

        AssertUntouched();
    }

    [TestMethod]
    public void Complete_PurseShort_ChangesNothing()
    {
        Add("t", once: true, cost: 5000, gain: 2).SetsFlags.Add("x");

        Assert.IsFalse(_sut.Complete(Id, "t", 1, false));

        AssertUntouched();
    }

    [TestMethod]
    public void Complete_OnCooldown_ChangesNothing()
    {
        Add("t", cooldown: 5, gain: 2).SetsFlags.Add("x");
        _ledger.SetTopicLastDay(Id, "t", 10);

        Assert.IsFalse(_sut.Complete(Id, "t", 12, false));

        _adapter.DidNotReceive().ChangeRelationWithPlayer(Arg.Any<string>(), Arg.Any<int>());
        Assert.IsFalse(_ledger.Has("x"));
    }

    [TestMethod]
    public void Complete_ChargeRefusedByThePurse_ChangesNothing()
    {
        Add("t", once: true, cost: 100, gain: 2).SetsFlags.Add("x");
        _purse.ChargeGold(100).Returns(false);

        Assert.IsFalse(_sut.Complete(Id, "t", 1, false));

        AssertUntouched();
    }

    [TestMethod]
    public void Complete_UnknownTopic_IsFalse()
    {
        Add("t");

        Assert.IsFalse(_sut.Complete(Id, "missing", 1, false));
        Assert.IsFalse(_sut.Complete("nobody", "t", 1, false));
    }

    [TestMethod]
    public void Visible_RelationAboveMaximum_Hidden()
    {
        Add("t", maxRelation: 20);
        _adapter.GetRelationWithPlayer(Id).Returns(21);

        Assert.AreEqual(0, _sut.Visible(Id, true, Today).Count);
    }

    [TestMethod]
    public void Visible_RelationAtMaximum_Shown()
    {
        Add("t", maxRelation: 20);
        _adapter.GetRelationWithPlayer(Id).Returns(20);

        Assert.AreEqual(1, _sut.Visible(Id, true, Today).Count);
    }

    [TestMethod]
    public void Visible_ChanceBelow100_FollowsTheDailyRoll()
    {
        Add("t", chance: 50);
        var day = Enumerable.Range(0, 400).First(d => OathDailyRoll.Percent(Id, "t", d) < 50);
        var otherDay = Enumerable.Range(0, 400).First(d => OathDailyRoll.Percent(Id, "t", d) >= 50);

        Assert.AreEqual(1, _sut.Visible(Id, true, day).Count);
        Assert.AreEqual(0, _sut.Visible(Id, true, otherDay).Count);
    }

    [TestMethod]
    public void Visible_ChanceBelow100_IsStableForTheWholeDay()
    {
        Add("t", chance: 50);

        var first = _sut.Visible(Id, true, 77).Count;

        for (var i = 0; i < 20; i++)
            Assert.AreEqual(first, _sut.Visible(Id, true, 77).Count);
    }

    [TestMethod]
    public void Visible_Chance100_AlwaysShown()
    {
        Add("t", chance: 100);

        for (var day = 0; day < 300; day++)
            Assert.AreEqual(1, _sut.Visible(Id, true, day).Count);
    }

    [TestMethod]
    public void Blocker_TakeItemsMissing_IsItems()
    {
        var t = Add("t", once: true);
        t.TakeItems.Add(new ItemCostDefinition { ItemId = "ore", Count = 3 });
        _roster.GetItemCount("ore").Returns(2);

        Assert.AreEqual(TopicBlocker.Items, _sut.Blocker(Id, t, 1, false));
    }

    [TestMethod]
    public void Blocker_TakeItemsHeld_IsNone()
    {
        var t = Add("t", once: true);
        t.TakeItems.Add(new ItemCostDefinition { ItemId = "ore", Count = 3 });
        _roster.GetItemCount("ore").Returns(3);

        Assert.AreEqual(TopicBlocker.None, _sut.Blocker(Id, t, 1, false));
    }

    [TestMethod]
    public void Blocker_OrderIsCoopUnknownCooldownPurseItems()
    {
        var t = Add("t", cooldown: 5, cost: 5000);
        t.TakeItems.Add(new ItemCostDefinition { ItemId = "ore", Count = 3 });
        _ledger.SetTopicLastDay(Id, "t", 10);

        Assert.AreEqual(TopicBlocker.Coop, _sut.Blocker(Id, t, 11, true));
        Assert.AreEqual(TopicBlocker.Cooldown, _sut.Blocker(Id, t, 11, false));
        Assert.AreEqual(TopicBlocker.Purse, _sut.Blocker(Id, t, 15, false));
        _purse.Gold.Returns(10_000);
        Assert.AreEqual(TopicBlocker.Items, _sut.Blocker(Id, t, 15, false));
    }

    [TestMethod]
    public void MissingTakeItems_ListsTheShortfallOnly()
    {
        var t = Add("t", once: true);
        t.TakeItems.Add(new ItemCostDefinition { ItemId = "ore", Count = 5 });
        t.TakeItems.Add(new ItemCostDefinition { ItemId = "wine", Count = 2 });
        _roster.GetItemCount("ore").Returns(2);
        _roster.GetItemCount("wine").Returns(2);

        var missing = _sut.MissingTakeItems(Id, t);

        Assert.AreEqual(1, missing.Count);
        Assert.AreEqual(MissingTermKind.Item, missing[0].Kind);
        Assert.AreEqual("ore", missing[0].ItemId);
        Assert.AreEqual(3, missing[0].Count);
    }

    [TestMethod]
    public void MissingTakeItems_NullTopicOrNoItems_Empty()
    {
        var t = Add("t");

        Assert.AreEqual(0, _sut.MissingTakeItems(Id, null).Count);
        Assert.AreEqual(0, _sut.MissingTakeItems(Id, t).Count);
    }

    [TestMethod]
    public void Complete_NegativeGain_PassesTheSignedDelta()
    {
        Add("t", once: true, gain: -4);

        Assert.IsTrue(_sut.Complete(Id, "t", 1, false));

        _adapter.Received(1).ChangeRelationWithPlayer(Id, -4);
    }

    [TestMethod]
    public void Complete_TakeAndGiveItems_MoveBothWays()
    {
        var t = Add("t", once: true, reward: 50);
        t.TakeItems.Add(new ItemCostDefinition { ItemId = "ore", Count = 3 });
        t.GiveItems.Add(new ItemCostDefinition { ItemId = "blade", Count = 1 });
        _roster.GetItemCount("ore").Returns(3);
        _roster.RemoveItem("ore", 3).Returns(3);
        _roster.AddItem("blade", 1).Returns(true);

        Assert.IsTrue(_sut.Complete(Id, "t", 5, false));

        _roster.Received(1).RemoveItem("ore", 3);
        _roster.Received(1).AddItem("blade", 1);
        _purse.Received(1).GrantGold(50);
        Assert.IsTrue(_ledger.TryGetTopicLastDay(Id, "t", out _));
    }

    [TestMethod]
    public void Complete_TakeItemFallsShort_GivesBackWhatWasTakenAndChangesNothing()
    {
        var t = Add("t", once: true, cost: 100, gain: 2);
        t.SetsFlags.Add("x");
        t.TakeItems.Add(new ItemCostDefinition { ItemId = "ore", Count = 3 });
        t.TakeItems.Add(new ItemCostDefinition { ItemId = "wine", Count = 2 });
        t.GiveItems.Add(new ItemCostDefinition { ItemId = "blade", Count = 1 });
        _roster.GetItemCount(Arg.Any<string>()).Returns(10);
        _roster.RemoveItem("ore", 3).Returns(3);
        _roster.RemoveItem("wine", 2).Returns(1);

        Assert.IsFalse(_sut.Complete(Id, "t", 1, false));

        _roster.Received(1).AddItem("ore", 3);
        _roster.Received(1).AddItem("wine", 1);
        _roster.DidNotReceive().AddItem("blade", Arg.Any<int>());
        _purse.DidNotReceive().ChargeGold(Arg.Any<int>());
        AssertUntouched();
    }

    [TestMethod]
    public void Complete_ChargeFailsAfterTakingItems_GivesThemBack()
    {
        var t = Add("t", once: true, cost: 100, gain: 2);
        t.SetsFlags.Add("x");
        t.TakeItems.Add(new ItemCostDefinition { ItemId = "ore", Count = 3 });
        t.GiveItems.Add(new ItemCostDefinition { ItemId = "blade", Count = 1 });
        _roster.GetItemCount("ore").Returns(3);
        _roster.RemoveItem("ore", 3).Returns(3);
        _purse.ChargeGold(100).Returns(false);

        Assert.IsFalse(_sut.Complete(Id, "t", 1, false));

        _roster.Received(1).AddItem("ore", 3);
        _roster.DidNotReceive().AddItem("blade", Arg.Any<int>());
        AssertUntouched();
    }

    [TestMethod]
    public void Complete_ItemsMissing_ChangesNothing()
    {
        var t = Add("t", once: true, gain: 2);
        t.SetsFlags.Add("x");
        t.TakeItems.Add(new ItemCostDefinition { ItemId = "ore", Count = 3 });
        _roster.GetItemCount("ore").Returns(1);

        Assert.IsFalse(_sut.Complete(Id, "t", 1, false));

        _roster.DidNotReceive().RemoveItem(Arg.Any<string>(), Arg.Any<int>());
        AssertUntouched();
    }

    [TestMethod]
    public void Complete_GiveItemRefused_StillFinishesAndLogs()
    {
        var t = Add("t", once: true);
        t.GiveItems.Add(new ItemCostDefinition { ItemId = "blade", Count = 1 });
        _roster.AddItem("blade", 1).Returns(false);

        Assert.IsTrue(_sut.Complete(Id, "t", 1, false));

        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("blade")));
    }

    private void AssertUntouched()
    {
        _purse.DidNotReceive().GrantGold(Arg.Any<int>());
        _adapter.DidNotReceive().ChangeRelationWithPlayer(Arg.Any<string>(), Arg.Any<int>());
        Assert.IsFalse(_ledger.Has("x"));
        Assert.IsFalse(_ledger.TryGetTopicLastDay(Id, "t", out _));
    }

    [TestMethod]
    public void IntroDue_ConfiguredUnrecruitedCompanion_True()
    {
        Assert.IsTrue(_sut.IntroDue(Id, recruited: false, isCoopClient: false, hireAllowed: true));
    }

    [TestMethod]
    public void IntroDue_Recruited_False()
    {
        Assert.IsFalse(_sut.IntroDue(Id, recruited: true, isCoopClient: false, hireAllowed: true));
    }

    [TestMethod]
    public void IntroDue_UnknownOrDisabledCompanion_False()
    {
        Assert.IsFalse(_sut.IntroDue("not_configured", false, false, hireAllowed: true));
        Assert.IsFalse(_sut.IntroDue(null, false, false, hireAllowed: true));
        _companion.Enabled = false;
        Assert.IsFalse(_sut.IntroDue(Id, false, false, hireAllowed: true));
    }

    [TestMethod]
    public void IntroDue_CoopClient_False()
    {
        Assert.IsFalse(_sut.IntroDue(Id, recruited: false, isCoopClient: true, hireAllowed: true));
    }

    [TestMethod]
    public void IntroDue_HireRefusedByWandererAllegiance_False()
    {
        // Every backstory ends with an offer to join; a companion who would refuse the hire (#575) keeps it.
        Assert.IsFalse(_sut.IntroDue(Id, recruited: false, isCoopClient: false, hireAllowed: false));
    }

    [TestMethod]
    public void IntroDue_AfterRecordIntro_False()
    {
        Assert.IsTrue(_sut.RecordIntro(Id, Today, isCoopClient: false));

        Assert.IsFalse(_sut.IntroDue(Id, recruited: false, isCoopClient: false, hireAllowed: true));
    }

    [TestMethod]
    public void RecordIntro_CoopClient_ReturnsFalseAndRecordsNothing()
    {
        Assert.IsFalse(_sut.RecordIntro(Id, Today, isCoopClient: true));

        Assert.IsFalse(_ledger.TryGetTopicLastDay(Id, OathTopicService.IntroTopicId, out _));
        Assert.IsTrue(_sut.IntroDue(Id, false, false, hireAllowed: true));
    }

    [TestMethod]
    public void IntroRecord_DoesNotHideAConfigTopic()
    {
        var once = Add("t_once", once: true);
        var plain = Add("t_plain");

        _sut.RecordIntro(Id, Today, isCoopClient: false);

        CollectionAssert.AreEquivalent(new[] { once, plain }, _sut.Visible(Id, recruited: false, Today).ToArray());
        Assert.AreEqual(TopicBlocker.None, _sut.Blocker(Id, once, Today, isCoopClient: false));
        Assert.IsFalse(_ledger.TryGetTopicLastDay(Id, "t_once", out _));
    }
}
