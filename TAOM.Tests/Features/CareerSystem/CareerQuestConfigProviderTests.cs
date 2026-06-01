using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.CareerSystem;
using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Tests.Features.CareerSystem;

[TestClass]
public class CareerQuestConfigProviderTests
{
    private CareerQuestConfigProvider _sut;

    [TestInitialize]
    public void Setup()
    {
        _sut = new CareerQuestConfigProvider(Substitute.For<IPathService>(), Substitute.For<IModLogger>());
    }

    private System.Collections.Generic.List<CareerQuestDefinition> Parse(string xml)
        => _sut.ParseQuests(XDocument.Parse(xml));

    private const string ValidQuest = @"
<CareerQuests>
  <CareerQuest id='gondor_capt_t2' career_id='captain_of_osgiliath' tier='2' title_key='{=k}t' description_key='{=k}d'>
    <Objectives>
      <Objective type='WinBattles' target='20' task_key='{=k}win' />
      <Objective type='SkillThreshold' target='100' param='OneHanded' task_key='{=k}skill' />
    </Objectives>
    <Rewards>
      <Reward type='UnlockTier' amount='2' />
      <Reward type='GrantRenown' amount='50' />
      <Reward type='GrantAttributeFlag' param='captain_proven' />
    </Rewards>
  </CareerQuest>
</CareerQuests>";

    [TestMethod]
    public void ParseQuests_ValidQuest_ParsesAllFields()
    {
        var quests = Parse(ValidQuest);
        Assert.AreEqual(1, quests.Count);
        var q = quests[0];
        Assert.AreEqual("gondor_capt_t2", q.Id);
        Assert.AreEqual("captain_of_osgiliath", q.CareerId);
        Assert.AreEqual(2, q.Tier);
        Assert.AreEqual(2, q.Objectives.Count);
        Assert.AreEqual(CareerQuestObjectiveType.WinBattles, q.Objectives[0].Type);
        Assert.AreEqual(20, q.Objectives[0].Target);
        Assert.AreEqual("OneHanded", q.Objectives[1].Param);
        Assert.AreEqual(3, q.Rewards.Count);
        Assert.AreEqual(CareerQuestRewardType.UnlockTier, q.Rewards[0].Type);
        Assert.AreEqual(2, q.Rewards[0].Amount);
    }

    [TestMethod]
    public void ParseQuests_MissingId_Skipped()
        => Assert.AreEqual(0, Parse("<R><CareerQuest career_id='c' tier='1'><Objectives><Objective type='WinBattles' target='5'/></Objectives></CareerQuest></R>").Count);

    [TestMethod]
    public void ParseQuests_MissingCareerId_Skipped()
        => Assert.AreEqual(0, Parse("<R><CareerQuest id='q' tier='1'><Objectives><Objective type='WinBattles' target='5'/></Objectives></CareerQuest></R>").Count);

    [DataTestMethod]
    [DataRow(0)]
    [DataRow(4)]
    public void ParseQuests_TierOutOfRange_Skipped(int tier)
        => Assert.AreEqual(0, Parse($"<R><CareerQuest id='q' career_id='c' tier='{tier}'><Objectives><Objective type='WinBattles' target='5'/></Objectives></CareerQuest></R>").Count);

    [TestMethod]
    public void ParseQuests_NoObjectives_Skipped()
        => Assert.AreEqual(0, Parse("<R><CareerQuest id='q' career_id='c' tier='1'><Objectives></Objectives></CareerQuest></R>").Count);

    [TestMethod]
    public void ParseQuests_ObjectiveUnknownType_ObjectiveSkipped_AndQuestDroppedWhenNoneLeft()
        => Assert.AreEqual(0, Parse("<R><CareerQuest id='q' career_id='c' tier='1'><Objectives><Objective type='Bogus' target='5'/></Objectives></CareerQuest></R>").Count);

    [TestMethod]
    public void ParseQuests_ObjectiveTargetZero_Skipped()
        => Assert.AreEqual(0, Parse("<R><CareerQuest id='q' career_id='c' tier='1'><Objectives><Objective type='WinBattles' target='0'/></Objectives></CareerQuest></R>").Count);

    [TestMethod]
    public void ParseQuests_SkillThresholdMissingParam_ObjectiveSkipped()
        => Assert.AreEqual(0, Parse("<R><CareerQuest id='q' career_id='c' tier='1'><Objectives><Objective type='SkillThreshold' target='100'/></Objectives></CareerQuest></R>").Count);

    [TestMethod]
    public void ParseQuests_DuplicateId_SecondSkipped()
    {
        var xml = "<R>" +
            "<CareerQuest id='dup' career_id='c' tier='1'><Objectives><Objective type='WinBattles' target='5'/></Objectives></CareerQuest>" +
            "<CareerQuest id='dup' career_id='c' tier='2'><Objectives><Objective type='WinBattles' target='9'/></Objectives></CareerQuest>" +
            "</R>";
        var quests = Parse(xml);
        Assert.AreEqual(1, quests.Count);
        Assert.AreEqual(1, quests[0].Tier);
    }

    [TestMethod]
    public void ParseQuests_RewardUnknownType_RewardSkipped_QuestStillLoads()
    {
        var quests = Parse("<R><CareerQuest id='q' career_id='c' tier='1'><Objectives><Objective type='WinBattles' target='5'/></Objectives><Rewards><Reward type='Nonsense' amount='1'/></Rewards></CareerQuest></R>");
        Assert.AreEqual(1, quests.Count);
        Assert.AreEqual(0, quests[0].Rewards.Count);
    }

    [TestMethod]
    public void ParseQuests_UnlockTierRewardOutOfRange_RewardSkipped()
    {
        var quests = Parse("<R><CareerQuest id='q' career_id='c' tier='1'><Objectives><Objective type='WinBattles' target='5'/></Objectives><Rewards><Reward type='UnlockTier' amount='9'/></Rewards></CareerQuest></R>");
        Assert.AreEqual(0, quests[0].Rewards.Count);
    }

    [TestMethod]
    public void ParseQuests_GrantItemMissingParam_RewardSkipped()
    {
        var quests = Parse("<R><CareerQuest id='q' career_id='c' tier='1'><Objectives><Objective type='WinBattles' target='5'/></Objectives><Rewards><Reward type='GrantItem' amount='1'/></Rewards></CareerQuest></R>");
        Assert.AreEqual(0, quests[0].Rewards.Count);
    }

    [TestMethod]
    public void ParseQuests_GrantRenownNonPositive_RewardSkipped()
    {
        var quests = Parse("<R><CareerQuest id='q' career_id='c' tier='1'><Objectives><Objective type='WinBattles' target='5'/></Objectives><Rewards><Reward type='GrantRenown' amount='0'/></Rewards></CareerQuest></R>");
        Assert.AreEqual(0, quests[0].Rewards.Count);
    }

    [TestMethod]
    public void ParseQuests_GrantAttributeFlagMissingParam_RewardSkipped()
    {
        var quests = Parse("<R><CareerQuest id='q' career_id='c' tier='1'><Objectives><Objective type='WinBattles' target='5'/></Objectives><Rewards><Reward type='GrantAttributeFlag'/></Rewards></CareerQuest></R>");
        Assert.AreEqual(0, quests[0].Rewards.Count);
    }

    [TestMethod]
    public void ParseQuests_EmptyRoot_ReturnsEmpty()
        => Assert.AreEqual(0, Parse("<CareerQuests></CareerQuests>").Count);

    // ── Codex review follow-ups ──

    [TestMethod]
    public void ParseQuests_VisitSettlementTypeInvalidParam_ObjectiveSkipped()
        => Assert.AreEqual(0, Parse("<R><CareerQuest id='q' career_id='c' tier='1'><Objectives><Objective type='VisitSettlementType' target='3' param='Hideout'/></Objectives></CareerQuest></R>").Count);

    [TestMethod]
    public void ParseQuests_VisitSettlementTypeValidParam_Loads()
    {
        var quests = Parse("<R><CareerQuest id='q' career_id='c' tier='1'><Objectives><Objective type='VisitSettlementType' target='3' param='Town'/></Objectives></CareerQuest></R>");
        Assert.AreEqual(1, quests.Count);
        Assert.AreEqual("Town", quests[0].Objectives[0].Param);
    }

    [TestMethod]
    public void ParseQuests_UnlockTierAmountMismatch_CoercedToQuestTier()
    {
        var quests = Parse("<R><CareerQuest id='q' career_id='c' tier='2'><Objectives><Objective type='WinBattles' target='5'/></Objectives><Rewards><Reward type='UnlockTier' amount='3'/></Rewards></CareerQuest></R>");
        Assert.AreEqual(1, quests.Count);
        Assert.AreEqual(1, quests[0].Rewards.Count);
        Assert.AreEqual(2, quests[0].Rewards[0].Amount);   // coerced from 3 → quest tier 2
    }
}
