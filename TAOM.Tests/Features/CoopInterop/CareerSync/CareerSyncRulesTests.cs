using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Core.Infrastructure;
using TAOM.Features.CareerSystem;
using TAOM.Features.CareerSystem.Domain;
using TAOM.Features.CoopInterop.CareerSync;

namespace TAOM.Tests.Features.CoopInterop.CareerSync;

// The co-op server's check of a career a player reports (docs/features/coop-interop.md, "Career sync"). The take
// rule is TAOM's own CareerChoiceRules.CanTake, replayed in the order the player took the choices.
[TestClass]
public class CareerSyncRulesTests
{
    private const string Hero = "Player1";
    private ICareerRegistry _registry = null!;
    private readonly HashSet<string> _questFlags = new(StringComparer.Ordinal) { "proven_at_osgiliath" };
    private int _budget;

    private static readonly CareerChoiceDefinition Root = new("root", "", ChoiceType.Passive, "", "", null, null);
    private static readonly CareerChoiceDefinition ForkA = Passive("fork_a", "t1_fork");
    private static readonly CareerChoiceDefinition ForkB = Passive("fork_b", "t1_fork");
    private static readonly CareerChoiceDefinition KeyA = Keystone("key_a", "t1_free_a");
    private static readonly CareerChoiceDefinition KeyB = Keystone("key_b", "t1_free_b");
    private static readonly CareerChoiceDefinition T2 = Passive("t2_p", "t2_free");
    private static readonly CareerChoiceDefinition Stray = Passive("stray", "elsewhere");

    private static readonly CareerDefinition Career = new(
        id: "ranger", displayName: "Ranger", description: "", portraitSprite: "", abilityTemplateId: "volley",
        minClanTier: 0, rootChoiceId: "root", eligibleCultureIds: new List<string>(),
        choiceGroupIds: new List<string> { "t1_fork", "t1_free_a", "t1_free_b", "t2_free" }, treeVersion: 2);

    [TestInitialize]
    public void Setup()
    {
        _registry = Substitute.For<ICareerRegistry>();
        _budget = 10;
        _registry.GetCareer("ranger").Returns(Career);
        _registry.GetMaxChoicesForHero(Career, Arg.Any<int>()).Returns(_ => _budget);
        _registry.IsTierAvailable(Arg.Any<int>(), Arg.Any<int>()).Returns(x => (int)x[1] == 1);
        _registry.GetChoice("root").Returns(Root);
        StubGroup("t1_fork", 1, 1, ForkA, ForkB);
        StubGroup("t1_free_a", 1, 0, KeyA);
        StubGroup("t1_free_b", 1, 0, KeyB);
        StubGroup("t2_free", 2, 0, T2);
        StubGroup("elsewhere", 1, 0, Stray);
    }

    private CareerSyncRefusal Check(CareerSyncSnapshot sent, int level = 10) =>
        CareerSyncRules.Check(sent, _registry, level, _questFlags);

    private static CareerSyncSnapshot Snap(string? career, string[]? choices = null, int[]? tiers = null, string[]? flags = null) =>
        new(Hero, career, choices ?? Array.Empty<string>(), tiers ?? Array.Empty<int>(), flags ?? Array.Empty<string>());

    [TestMethod]
    public void Check_ALegalCareer_IsAccepted()
    {
        Assert.AreEqual(CareerSyncRefusal.None, Check(Snap("ranger", new[] { "root", "fork_a", "key_a" }, flags: new[] { "career_tree_v2" })));
    }

    [TestMethod]
    public void Check_AClearedCareer_IsAccepted_OnlyWhenEverythingIsEmpty()
    {
        // A free career switch clears the book first (CareerSwitchService), so a cleared career is a real state.
        Assert.AreEqual(CareerSyncRefusal.None, Check(Snap(null)));
        Assert.AreEqual(CareerSyncRefusal.None, Check(Snap("")));
        Assert.AreEqual(CareerSyncRefusal.NotCleared, Check(Snap(null, new[] { "root" })));
        Assert.AreEqual(CareerSyncRefusal.NotCleared, Check(Snap(null, tiers: new[] { 2 })));
        Assert.AreEqual(CareerSyncRefusal.NotCleared, Check(Snap(null, flags: new[] { "proven_at_osgiliath" })));
    }

    [TestMethod]
    public void Check_OversizedFields_AreRefused()
    {
        var longId = new string('x', CareerSyncRules.MaxIdLength + 1);
        var tooMany = Enumerable.Range(0, CareerSyncRules.MaxEntries + 1).Select(i => "c" + i).ToArray();

        Assert.AreEqual(CareerSyncRefusal.Oversized, Check(new CareerSyncSnapshot(longId, "ranger", new[] { "root" }, Array.Empty<int>(), Array.Empty<string>())));
        Assert.AreEqual(CareerSyncRefusal.Oversized, Check(new CareerSyncSnapshot("", "ranger", new[] { "root" }, Array.Empty<int>(), Array.Empty<string>())));
        Assert.AreEqual(CareerSyncRefusal.Oversized, Check(Snap(longId)));
        Assert.AreEqual(CareerSyncRefusal.Oversized, Check(Snap("ranger", tooMany)));
        Assert.AreEqual(CareerSyncRefusal.Oversized, Check(Snap("ranger", new[] { "root", longId })));
        Assert.AreEqual(CareerSyncRefusal.Oversized, Check(Snap("ranger", new[] { "root" }, flags: tooMany)));
        Assert.AreEqual(CareerSyncRefusal.Oversized, Check(Snap("ranger", new[] { "root" }, tiers: Enumerable.Repeat(2, CareerSyncRules.MaxEntries + 1).ToArray())));
        Assert.AreEqual(CareerSyncRefusal.Oversized, Check(new CareerSyncSnapshot(Hero, "ranger", null!, Array.Empty<int>(), Array.Empty<string>())));
    }

    [TestMethod]
    public void Check_AnUnknownCareer_IsRefused()
    {
        Assert.AreEqual(CareerSyncRefusal.UnknownCareer, Check(Snap("no_such_career", new[] { "root" })));
    }

    [TestMethod]
    public void Check_TheRootMustComeFirst()
    {
        Assert.AreEqual(CareerSyncRefusal.RootNotFirst, Check(Snap("ranger", new[] { "fork_a", "root" })));
        Assert.AreEqual(CareerSyncRefusal.RootNotFirst, Check(Snap("ranger", new[] { "fork_a" })));
        Assert.AreEqual(CareerSyncRefusal.None, Check(Snap("ranger")), "a career with no choice yet is legal");
    }

    [TestMethod]
    public void Check_AChoiceOfAnotherCareer_IsRefused()
    {
        Assert.AreEqual(CareerSyncRefusal.ChoiceNotAllowed, Check(Snap("ranger", new[] { "root", "stray" })));
        Assert.AreEqual(CareerSyncRefusal.ChoiceNotAllowed, Check(Snap("ranger", new[] { "root", "unknown_choice" })));
    }

    [TestMethod]
    public void Check_AFullFork_AKeystoneLock_AndADuplicate_AreRefused()
    {
        Assert.AreEqual(CareerSyncRefusal.ChoiceNotAllowed, Check(Snap("ranger", new[] { "root", "fork_a", "fork_b" })));
        Assert.AreEqual(CareerSyncRefusal.ChoiceNotAllowed, Check(Snap("ranger", new[] { "root", "key_a", "key_b" })));
        Assert.AreEqual(CareerSyncRefusal.ChoiceNotAllowed, Check(Snap("ranger", new[] { "root", "fork_a", "fork_a" })));
        Assert.AreEqual(CareerSyncRefusal.ChoiceNotAllowed, Check(Snap("ranger", new[] { "root", "root" })));
    }

    [TestMethod]
    public void Check_MoreChoicesThanTheLevelGives_IsRefused()
    {
        _budget = 2;

        Assert.AreEqual(CareerSyncRefusal.None, Check(Snap("ranger", new[] { "root", "fork_a" })));
        Assert.AreEqual(CareerSyncRefusal.OverBudget, Check(Snap("ranger", new[] { "root", "fork_a", "key_a" })));
        _budget = 0;
        Assert.AreEqual(CareerSyncRefusal.OverBudget, Check(Snap("ranger", new[] { "root" })));
    }

    [TestMethod]
    public void Check_ALockedTier_IsRefused_UnlessTheCareerHoldsItsUnlock()
    {
        Assert.AreEqual(CareerSyncRefusal.ChoiceNotAllowed, Check(Snap("ranger", new[] { "root", "t2_p" })));
        Assert.AreEqual(CareerSyncRefusal.None, Check(Snap("ranger", new[] { "root", "t2_p" }, tiers: new[] { 2 })));
    }

    [TestMethod]
    public void Check_TiersOutsideOneToThree_AreRefused()
    {
        Assert.AreEqual(CareerSyncRefusal.BadTier, Check(Snap("ranger", new[] { "root" }, tiers: new[] { 0 })));
        Assert.AreEqual(CareerSyncRefusal.BadTier, Check(Snap("ranger", new[] { "root" }, tiers: new[] { 4 })));
        Assert.AreEqual(CareerSyncRefusal.None, Check(Snap("ranger", new[] { "root" }, tiers: new[] { 1, 2, 3 })));
    }

    [TestMethod]
    public void Check_Flags_OnlyTheCareersTreeVersionAndQuestRewards()
    {
        Assert.AreEqual(CareerSyncRefusal.None, Check(Snap("ranger", new[] { "root" }, flags: new[] { "career_tree_v2", "proven_at_osgiliath" })));
        Assert.AreEqual(CareerSyncRefusal.UnknownFlag, Check(Snap("ranger", new[] { "root" }, flags: new[] { "career_tree_v3" })));
        Assert.AreEqual(CareerSyncRefusal.UnknownFlag, Check(Snap("ranger", new[] { "root" }, flags: new[] { "made_up" })));
    }

    [TestMethod]
    public void Check_ATierOrFlagListedTwice_IsRefused()
    {
        // The book never holds one twice (UnlockTier and HeroCareerData.AddFlag skip a repeat), so a report that does is not one.
        Assert.AreEqual(CareerSyncRefusal.BadTier, Check(Snap("ranger", new[] { "root" }, tiers: new[] { 2, 2 })));
        Assert.AreEqual(CareerSyncRefusal.UnknownFlag, Check(Snap("ranger", new[] { "root" }, flags: new[] { "career_tree_v2", "career_tree_v2" })));
        Assert.AreEqual(CareerSyncRefusal.UnknownFlag, Check(Snap("ranger", new[] { "root" }, flags: new[] { "proven_at_osgiliath", "proven_at_osgiliath" })));
    }

    [TestMethod]
    public void QuestFlags_AreTheGrantAttributeFlagParams()
    {
        var quests = new[]
        {
            new CareerQuestDefinition("q1", "ranger", 2, "", "", null!, new[]
            {
                new CareerQuestRewardDefinition(CareerQuestRewardType.GrantAttributeFlag, 0, "proven"),
                new CareerQuestRewardDefinition(CareerQuestRewardType.UnlockTier, 2, "ignored"),
                new CareerQuestRewardDefinition(CareerQuestRewardType.GrantAttributeFlag, 0, ""),
            }),
        };

        CollectionAssert.AreEquivalent(new[] { "proven" }, CareerSyncRules.QuestFlags(quests).ToList());
        Assert.AreEqual(0, CareerSyncRules.QuestFlags(null).Count);
    }

    [TestMethod]
    public void Snapshot_OfAndToData_KeepEveryListInOrder_AndCompareByValue()
    {
        var data = new HeroCareerData(Hero) { CareerStringId = "ranger" };
        data.ChoiceIds.AddRange(new[] { "root", "fork_a" });
        data.TierUnlocks.Add(2);
        data.Flags.Add("career_tree_v2");

        var snap = CareerSyncSnapshot.Of(data);
        var back = snap.ToData();

        Assert.AreEqual(Hero, back.HeroStringId);
        Assert.AreEqual("ranger", back.CareerStringId);
        CollectionAssert.AreEqual(new[] { "root", "fork_a" }, back.ChoiceIds);
        CollectionAssert.AreEqual(new[] { 2 }, back.TierUnlocks);
        CollectionAssert.AreEqual(new[] { "career_tree_v2" }, back.Flags);
        Assert.IsTrue(snap.SameAs(CareerSyncSnapshot.Of(back)));
        Assert.IsFalse(snap.SameAs(Snap("ranger", new[] { "fork_a", "root" }, new[] { 2 }, new[] { "career_tree_v2" })));
        Assert.IsTrue(Snap(null).SameAs(Snap("")), "no career and an empty career id are the same cleared state");
    }

    [TestMethod]
    public void Check_ARealCareerTakenToItsLimit_IsAccepted()
    {
        // The shipped career data, so a schema change that the rule cannot read shows here.
        var moduleData = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\..\Main\_Module\ModuleData"));
        var paths = Substitute.For<IPathService>();
        paths.ModuleDataPath.Returns(moduleData);
        var logger = Substitute.For<IModLogger>();
        var registry = new CareerRegistry(new CareerConfigProvider(paths, logger), logger);
        var career = registry.GetCareer("ranger_of_ithilien");
        Assert.IsNotNull(career, "ranger_of_ithilien is in the shipped careers");

        const int level = 30;
        var held = new HeroCareerData(Hero) { CareerStringId = career.Id };
        held.ChoiceIds.Add(career.RootChoiceId);
        var tiers = new[] { 1, 2, 3 };
        foreach (var choice in career.ChoiceGroupIds.SelectMany(registry.GetChoicesForGroup))
        {
            var free = registry.GetMaxChoicesForHero(career, level) - held.ChoiceIds.Count;
            if (CareerChoiceRules.CanTake(career, choice, registry, held, free, _ => true) == ChoiceLockReason.None)
                held.ChoiceIds.Add(choice.Id);
        }
        if (career.TreeVersion >= 2) held.Flags.Add(CareerTreeVersion.FlagFor(career.TreeVersion));
        held.TierUnlocks.AddRange(tiers);

        Assert.IsTrue(held.ChoiceIds.Count > 3, "the walk took more than the root and a fork: " + held.ChoiceIds.Count);
        Assert.AreEqual(CareerSyncRefusal.None, CareerSyncRules.Check(CareerSyncSnapshot.Of(held), registry, level, new HashSet<string>()));
    }

    private void StubGroup(string groupId, int tier, int maxPicks, params CareerChoiceDefinition[] choices)
    {
        foreach (var c in choices) _registry.GetChoice(c.Id).Returns(c);
        _registry.GetChoicesForGroup(groupId).Returns(choices.ToList());
        _registry.GetGroup(groupId).Returns(new CareerChoiceGroupDefinition(groupId, groupId == "elsewhere" ? "other" : "ranger", tier,
            choices.Select(c => c.Id).ToList(), maxPicks: maxPicks));
    }

    private static CareerChoiceDefinition Keystone(string id, string groupId) => new(id, groupId, ChoiceType.Keystone, "", "", null, null);

    private static CareerChoiceDefinition Passive(string id, string groupId) => new(id, groupId, ChoiceType.Passive, "", "", null, null);
}
