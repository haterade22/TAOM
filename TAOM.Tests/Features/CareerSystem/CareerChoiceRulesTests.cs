using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.CareerSystem;
using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Tests.Features.CareerSystem;

// #769 E4/E5 — the one take/refund rule the career screen asks for every diamond. Order of
// checks is part of the contract: the reason shown to the player is the FIRST hit.
[TestClass]
public class CareerChoiceRulesTests
{
    private ICareerRegistry _registry;
    private HeroCareerData _heroData;
    private int _maxTierAvailable;

    private static readonly CareerChoiceDefinition ForkA = Passive("fork_a", "t1_fork");
    private static readonly CareerChoiceDefinition ForkB = Passive("fork_b", "t1_fork");
    private static readonly CareerChoiceDefinition ForkC = Passive("fork_c", "t1_fork");
    private static readonly CareerChoiceDefinition ForkKeyA = Keystone("fork_key_a", "t1_forkkeys");
    private static readonly CareerChoiceDefinition ForkKeyB = Keystone("fork_key_b", "t1_forkkeys");
    private static readonly CareerChoiceDefinition FreeKeyA = Keystone("free_key_a", "t1_free_a");
    private static readonly CareerChoiceDefinition FreeKeyB = Keystone("free_key_b", "t1_free_b");
    private static readonly CareerChoiceDefinition FreePassive = Passive("free_p1", "t1_free_a");
    private static readonly CareerChoiceDefinition T2Passive = Passive("t2_p1", "t2_fork");
    private static readonly CareerChoiceDefinition T2Passive2 = Passive("t2_p2", "t2_fork");
    private static readonly CareerChoiceDefinition Stray = Passive("stray", "elsewhere");
    private static readonly CareerChoiceDefinition Root = new CareerChoiceDefinition(
        id: "root", groupId: "", type: ChoiceType.Passive,
        description: "", iconSprite: "", passives: null, mutations: null);

    private static readonly CareerDefinition Career = new CareerDefinition(
        id: "warboss", displayName: "Warboss", description: "", portraitSprite: "",
        abilityTemplateId: "rally", minClanTier: 0, rootChoiceId: "root",
        eligibleCultureIds: new List<string>(),
        choiceGroupIds: new List<string> { "t1_fork", "t1_forkkeys", "t1_free_a", "t1_free_b", "t2_fork" });

    [TestInitialize]
    public void Setup()
    {
        _registry = Substitute.For<ICareerRegistry>();
        _heroData = new HeroCareerData("hero1");
        _maxTierAvailable = 1;

        StubGroup("t1_fork", 1, maxPicks: 1, ForkA, ForkB, ForkC);
        StubGroup("t1_forkkeys", 1, maxPicks: 1, ForkKeyA, ForkKeyB);
        StubGroup("t1_free_a", 1, maxPicks: 0, FreeKeyA, FreePassive);
        StubGroup("t1_free_b", 1, maxPicks: 0, FreeKeyB);
        StubGroup("t2_fork", 2, maxPicks: 2, T2Passive, T2Passive2);
        StubGroup("elsewhere", 1, maxPicks: 0, Stray);
    }

    private bool TierAvailable(int tier) => tier <= _maxTierAvailable;

    private ChoiceLockReason CanTake(CareerChoiceDefinition choice, int freePoints = 5)
        => CareerChoiceRules.CanTake(Career, choice, _registry, _heroData, freePoints, TierAvailable);

    private ChoiceLockReason CanRefund(CareerChoiceDefinition choice, params string[] takenThisVisit)
        => CareerChoiceRules.CanRefund(Career, choice, _registry, _heroData, new List<string>(takenThisVisit));

    // ── CanTake ──

    [TestMethod]
    public void CanTake_AnyNullInput_NotInCareer()
    {
        Assert.AreEqual(ChoiceLockReason.NotInCareer,
            CareerChoiceRules.CanTake(null, ForkA, _registry, _heroData, 5, TierAvailable));
        Assert.AreEqual(ChoiceLockReason.NotInCareer,
            CareerChoiceRules.CanTake(Career, null, _registry, _heroData, 5, TierAvailable));
        Assert.AreEqual(ChoiceLockReason.NotInCareer,
            CareerChoiceRules.CanTake(Career, ForkA, null, _heroData, 5, TierAvailable));
        Assert.AreEqual(ChoiceLockReason.NotInCareer,
            CareerChoiceRules.CanTake(Career, ForkA, _registry, null, 5, TierAvailable));
        Assert.AreEqual(ChoiceLockReason.NotInCareer,
            CareerChoiceRules.CanTake(Career, ForkA, _registry, _heroData, 5, null));
    }

    [TestMethod]
    public void CanTake_AlreadyTaken_AlreadyTaken()
    {
        _heroData.AddChoice(ForkA.Id);

        Assert.AreEqual(ChoiceLockReason.AlreadyTaken, CanTake(ForkA));
    }

    [TestMethod]
    public void CanTake_GroupNotInCareer_NotInCareer()
    {
        Assert.AreEqual(ChoiceLockReason.NotInCareer, CanTake(Stray));
    }

    [TestMethod]
    public void CanTake_GroupUnknownToRegistry_NotInCareer()
    {
        var orphan = Passive("orphan", "no_such_group");

        Assert.AreEqual(ChoiceLockReason.NotInCareer, CanTake(orphan));
    }

    [TestMethod]
    public void CanTake_TierNotAvailable_TierLocked()
    {
        Assert.AreEqual(ChoiceLockReason.TierLocked, CanTake(T2Passive));
    }

    [TestMethod]
    public void CanTake_TierAvailable_None()
    {
        _maxTierAvailable = 2;

        Assert.AreEqual(ChoiceLockReason.None, CanTake(T2Passive));
    }

    [TestMethod]
    public void CanTake_MaxPicks1AndOnePickTaken_ForkFull()
    {
        _heroData.AddChoice(ForkA.Id);

        Assert.AreEqual(ChoiceLockReason.ForkFull, CanTake(ForkB));
    }

    [TestMethod]
    public void CanTake_MaxPicks1AndNothingTaken_None()
    {
        Assert.AreEqual(ChoiceLockReason.None, CanTake(ForkB));
    }

    [TestMethod]
    public void CanTake_MaxPicks2AndOnePickTaken_StillOpen()
    {
        _maxTierAvailable = 2;
        _heroData.AddChoice(T2Passive.Id);

        Assert.AreEqual(ChoiceLockReason.None, CanTake(T2Passive2));
    }

    [TestMethod]
    public void CanTake_MaxPicks2AndTwoPicksTaken_ForkFullForAThirdChoice()
    {
        _maxTierAvailable = 2;
        var third = Passive("t2_p3", "t2_fork");
        StubGroup("t2_fork", 2, maxPicks: 2, T2Passive, T2Passive2, third);
        _heroData.AddChoice(T2Passive.Id);
        _heroData.AddChoice(T2Passive2.Id);

        Assert.AreEqual(ChoiceLockReason.ForkFull, CanTake(third));
    }

    [TestMethod]
    public void CanTake_PicksTakenInAnotherGroup_DoNotFillThisFork()
    {
        _heroData.AddChoice(FreePassive.Id);
        _heroData.AddChoice(FreeKeyA.Id);

        Assert.AreEqual(ChoiceLockReason.None, CanTake(ForkA));
    }

    [TestMethod]
    public void CanTake_UnlimitedGroupWithOtherKeystoneTaken_KeystoneLocked()
    {
        _heroData.AddChoice(FreeKeyA.Id);

        Assert.AreEqual(ChoiceLockReason.KeystoneLocked, CanTake(FreeKeyB));
    }

    [TestMethod]
    public void CanTake_UnlimitedGroupPassiveBesideATakenKeystone_None()
    {
        _heroData.AddChoice(FreeKeyA.Id);

        Assert.AreEqual(ChoiceLockReason.None, CanTake(FreePassive));
    }

    [TestMethod]
    public void CanTake_ForkGroupKeystone_IgnoresTheKeystoneRule()
    {
        // MaxPicks governs a fork; the one-keystone-per-tier rule is for unlimited groups only.
        _heroData.AddChoice(FreeKeyA.Id);

        Assert.AreEqual(ChoiceLockReason.None, CanTake(ForkKeyA));
    }

    [TestMethod]
    public void CanTake_NoFreePoints_NoPoints()
    {
        Assert.AreEqual(ChoiceLockReason.NoPoints, CanTake(ForkA, freePoints: 0));
        Assert.AreEqual(ChoiceLockReason.NoPoints, CanTake(ForkA, freePoints: -1));
    }

    [TestMethod]
    public void CanTake_OnePoint_None()
    {
        Assert.AreEqual(ChoiceLockReason.None, CanTake(ForkA, freePoints: 1));
    }

    [TestMethod]
    public void CanTake_TierLockedAndForkFullAndNoPoints_TierLockedWins()
    {
        _heroData.AddChoice(T2Passive.Id);
        _heroData.AddChoice(T2Passive2.Id);
        var third = Passive("t2_p3", "t2_fork");
        StubGroup("t2_fork", 2, maxPicks: 2, T2Passive, T2Passive2, third);

        Assert.AreEqual(ChoiceLockReason.TierLocked, CanTake(third, freePoints: 0));
    }

    [TestMethod]
    public void CanTake_ForkFullAndNoPoints_ForkFullWins()
    {
        _heroData.AddChoice(ForkA.Id);

        Assert.AreEqual(ChoiceLockReason.ForkFull, CanTake(ForkB, freePoints: 0));
    }

    [TestMethod]
    public void CanTake_KeystoneLockedAndNoPoints_KeystoneLockedWins()
    {
        _heroData.AddChoice(FreeKeyA.Id);

        Assert.AreEqual(ChoiceLockReason.KeystoneLocked, CanTake(FreeKeyB, freePoints: 0));
    }

    [TestMethod]
    public void CanTake_AlreadyTakenAndTierLocked_AlreadyTakenWins()
    {
        _heroData.AddChoice(T2Passive.Id);

        Assert.AreEqual(ChoiceLockReason.AlreadyTaken, CanTake(T2Passive));
    }

    // ── CanRefund ──

    [TestMethod]
    public void CanRefund_AnyNullInput_NotInCareer()
    {
        _heroData.AddChoice(ForkA.Id);
        var visit = new List<string>();

        Assert.AreEqual(ChoiceLockReason.NotInCareer,
            CareerChoiceRules.CanRefund(null, ForkA, _registry, _heroData, visit));
        Assert.AreEqual(ChoiceLockReason.NotInCareer,
            CareerChoiceRules.CanRefund(Career, null, _registry, _heroData, visit));
        Assert.AreEqual(ChoiceLockReason.NotInCareer,
            CareerChoiceRules.CanRefund(Career, ForkA, null, _heroData, visit));
        Assert.AreEqual(ChoiceLockReason.NotInCareer,
            CareerChoiceRules.CanRefund(Career, ForkA, _registry, null, visit));
        Assert.AreEqual(ChoiceLockReason.NotInCareer,
            CareerChoiceRules.CanRefund(Career, ForkA, _registry, _heroData, null));
    }

    [TestMethod]
    public void CanRefund_NotTaken_NotTaken()
    {
        Assert.AreEqual(ChoiceLockReason.NotTaken, CanRefund(ForkA));
    }

    [TestMethod]
    public void CanRefund_RootChoice_NeverRefundable()
    {
        _heroData.AddChoice(Root.Id);

        Assert.AreEqual(ChoiceLockReason.NotInCareer, CanRefund(Root));
    }

    [TestMethod]
    public void CanRefund_GroupNotInCareer_NotInCareer()
    {
        _heroData.AddChoice(Stray.Id);

        Assert.AreEqual(ChoiceLockReason.NotInCareer, CanRefund(Stray));
    }

    [TestMethod]
    public void CanRefund_GroupUnknownToRegistry_NotInCareer()
    {
        var orphan = Passive("orphan", "no_such_group");
        _heroData.AddChoice(orphan.Id);

        Assert.AreEqual(ChoiceLockReason.NotInCareer, CanRefund(orphan));
    }

    [TestMethod]
    public void CanRefund_UnlimitedGroup_AlwaysFree()
    {
        _heroData.AddChoice(FreeKeyA.Id);
        _heroData.AddChoice(FreePassive.Id);

        Assert.AreEqual(ChoiceLockReason.None, CanRefund(FreeKeyA));
        Assert.AreEqual(ChoiceLockReason.None, CanRefund(FreePassive, FreePassive.Id));
    }

    [TestMethod]
    public void CanRefund_ForkPickFromAnEarlierVisit_LockedIn()
    {
        _heroData.AddChoice(ForkA.Id);

        Assert.AreEqual(ChoiceLockReason.LockedIn, CanRefund(ForkA));
    }

    [TestMethod]
    public void CanRefund_ForkPickTakenThisVisit_Free()
    {
        _heroData.AddChoice(ForkA.Id);

        Assert.AreEqual(ChoiceLockReason.None, CanRefund(ForkA, ForkA.Id));
    }

    [TestMethod]
    public void CanRefund_ForkPickTakenThisVisitDoesNotUnlockASibling()
    {
        _heroData.AddChoice(ForkA.Id);
        _heroData.AddChoice(ForkB.Id);

        Assert.AreEqual(ChoiceLockReason.LockedIn, CanRefund(ForkA, ForkB.Id));
    }

    // ── helpers ──

    private void StubGroup(string groupId, int tier, int maxPicks, params CareerChoiceDefinition[] choices)
    {
        var ids = new List<string>();
        foreach (var c in choices)
        {
            ids.Add(c.Id);
            _registry.GetChoice(c.Id).Returns(c);
        }
        _registry.GetGroup(groupId).Returns(new CareerChoiceGroupDefinition(
            groupId, "warboss", tier, ids, maxPicks: maxPicks));
        _registry.GetChoicesForGroup(groupId).Returns(new List<CareerChoiceDefinition>(choices));
    }

    private static CareerChoiceDefinition Keystone(string id, string groupId)
        => new CareerChoiceDefinition(id, groupId, ChoiceType.Keystone, "", "", null, null);

    private static CareerChoiceDefinition Passive(string id, string groupId)
        => new CareerChoiceDefinition(id, groupId, ChoiceType.Passive, "", "", null, null);

    // ── #769 E7: the retrain helpers ──

    private bool HoldsForkPick() => CareerChoiceRules.HoldsForkPick(Career, _registry, _heroData);

    [TestMethod]
    public void HoldsForkPick_NoPicks_False()
    {
        Assert.IsFalse(HoldsForkPick());
    }

    [TestMethod]
    public void HoldsForkPick_PickInAForkGroup_True()
    {
        _heroData.AddChoice(ForkB.Id);

        Assert.IsTrue(HoldsForkPick());
    }

    [TestMethod]
    public void HoldsForkPick_PicksOnlyInUnlimitedGroups_False()
    {
        _heroData.AddChoice(FreeKeyA.Id);
        _heroData.AddChoice(FreePassive.Id);

        Assert.IsFalse(HoldsForkPick());
    }

    [TestMethod]
    public void HoldsForkPick_PickInAForkGroupOutsideTheCareer_False()
    {
        var outside = new CareerDefinition(
            id: "other", displayName: "", description: "", portraitSprite: "", abilityTemplateId: "",
            minClanTier: 0, rootChoiceId: "root", eligibleCultureIds: new List<string>(),
            choiceGroupIds: new List<string> { "t1_free_a" });
        _heroData.AddChoice(ForkA.Id);

        Assert.IsFalse(CareerChoiceRules.HoldsForkPick(outside, _registry, _heroData));
    }

    [TestMethod]
    public void HoldsForkPick_NullInput_False()
    {
        _heroData.AddChoice(ForkA.Id);

        Assert.IsFalse(CareerChoiceRules.HoldsForkPick(null, _registry, _heroData));
        Assert.IsFalse(CareerChoiceRules.HoldsForkPick(Career, null, _heroData));
        Assert.IsFalse(CareerChoiceRules.HoldsForkPick(Career, _registry, null));
    }
}
