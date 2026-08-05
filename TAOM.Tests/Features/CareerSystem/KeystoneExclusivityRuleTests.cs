using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.CareerSystem;
using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Tests.Features.CareerSystem;

// Issue #381 — keystone branch exclusivity as an extracted, testable rule.
// Design (adopted 2026-08-05): a tier's ordinary stones stay open on both paths, but its
// KEYSTONES are mutually exclusive — taking one closes the others in that tier. Completing
// any full tier-3 group is the endgame reward that reopens every keystone passed over.
// Already-taken stones are never locked (grandfathered + refundable).
[TestClass]
public class KeystoneExclusivityRuleTests
{
    private ICareerRegistry _registry;
    private HeroCareerData _heroData;

    private static readonly CareerChoiceDefinition KeystoneA = Keystone("t1a_key", "t1_a");
    private static readonly CareerChoiceDefinition KeystoneB = Keystone("t1b_key", "t1_b");
    private static readonly CareerChoiceDefinition KeystoneT2 = Keystone("t2a_key", "t2_a");
    private static readonly CareerChoiceDefinition PassiveA = Passive("t1a_p1", "t1_a");
    private static readonly CareerChoiceDefinition T3Key = Keystone("t3a_key", "t3_a");
    private static readonly CareerChoiceDefinition T3P1 = Passive("t3a_p1", "t3_a");

    private static readonly CareerDefinition Career = new CareerDefinition(
        id: "warboss", displayName: "Warboss", description: "", portraitSprite: "",
        abilityTemplateId: "rally", minClanTier: 0, rootChoiceId: "root",
        eligibleCultureIds: new List<string>(),
        choiceGroupIds: new List<string> { "t1_a", "t1_b", "t2_a", "t3_a" });

    [TestInitialize]
    public void Setup()
    {
        _registry = Substitute.For<ICareerRegistry>();
        _heroData = new HeroCareerData("hero1");

        StubGroup("t1_a", 1, KeystoneA, PassiveA);
        StubGroup("t1_b", 1, KeystoneB);
        StubGroup("t2_a", 2, KeystoneT2);
        StubGroup("t3_a", 3, T3Key, T3P1);
    }

    [TestMethod]
    public void IsLocked_PassiveChoice_NeverLocked()
    {
        _heroData.AddChoice(KeystoneA.Id);

        Assert.IsFalse(KeystoneExclusivityRule.IsLocked(Career, PassiveA, _registry, _heroData));
    }

    [TestMethod]
    public void IsLocked_NoKeystoneTakenInTier_NotLocked()
    {
        Assert.IsFalse(KeystoneExclusivityRule.IsLocked(Career, KeystoneA, _registry, _heroData));
    }

    [TestMethod]
    public void IsLocked_OtherKeystoneTakenSameTier_Locked()
    {
        _heroData.AddChoice(KeystoneA.Id);

        Assert.IsTrue(KeystoneExclusivityRule.IsLocked(Career, KeystoneB, _registry, _heroData));
    }

    [TestMethod]
    public void IsLocked_KeystoneTakenDifferentTier_NotLocked()
    {
        _heroData.AddChoice(KeystoneA.Id);

        Assert.IsFalse(KeystoneExclusivityRule.IsLocked(Career, KeystoneT2, _registry, _heroData));
    }

    [TestMethod]
    public void IsLocked_ChoiceAlreadyTaken_NotLocked()
    {
        // Grandfathering: a save that already holds both same-tier keystones (pre-rule, or
        // pre-rebalance) keeps them taken and refundable — the rule gates future takes only.
        _heroData.AddChoice(KeystoneA.Id);
        _heroData.AddChoice(KeystoneB.Id);

        Assert.IsFalse(KeystoneExclusivityRule.IsLocked(Career, KeystoneB, _registry, _heroData));
    }

    [TestMethod]
    public void IsLocked_CompleteTier3Group_ReopensKeystones()
    {
        // Endgame exemption: any tier-3 group with EVERY choice taken reopens all keystones.
        _heroData.AddChoice(KeystoneA.Id);
        _heroData.AddChoice(T3Key.Id);
        _heroData.AddChoice(T3P1.Id);

        Assert.IsFalse(KeystoneExclusivityRule.IsLocked(Career, KeystoneB, _registry, _heroData));
    }

    [TestMethod]
    public void IsLocked_IncompleteTier3Group_StillLocked()
    {
        _heroData.AddChoice(KeystoneA.Id);
        _heroData.AddChoice(T3Key.Id); // t3a_p1 NOT taken — path incomplete

        Assert.IsTrue(KeystoneExclusivityRule.IsLocked(Career, KeystoneB, _registry, _heroData));
    }

    [TestMethod]
    public void IsLocked_UnknownGroup_NotLocked()
    {
        var orphan = Keystone("orphan_key", "no_such_group");
        _heroData.AddChoice(KeystoneA.Id);

        Assert.IsFalse(KeystoneExclusivityRule.IsLocked(Career, orphan, _registry, _heroData));
    }

    private void StubGroup(string groupId, int tier, params CareerChoiceDefinition[] choices)
    {
        var ids = new List<string>();
        foreach (var c in choices) ids.Add(c.Id);
        _registry.GetGroup(groupId).Returns(new CareerChoiceGroupDefinition(groupId, "warboss", tier, ids));
        _registry.GetChoicesForGroup(groupId).Returns(new List<CareerChoiceDefinition>(choices));
    }

    private static CareerChoiceDefinition Keystone(string id, string groupId) =>
        new CareerChoiceDefinition(id: id, groupId: groupId, type: ChoiceType.Keystone,
            description: "", iconSprite: "", passive: null, mutations: null);

    private static CareerChoiceDefinition Passive(string id, string groupId) =>
        new CareerChoiceDefinition(id: id, groupId: groupId, type: ChoiceType.Passive,
            description: "", iconSprite: "",
            passive: new PassiveEffect(PassiveEffectType.Damage, 0.1f), mutations: null);
}
