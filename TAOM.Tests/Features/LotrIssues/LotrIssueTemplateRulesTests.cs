using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.CampaignSystem.Issues;
using TAOM.Features.LotrIssues;
using TAOM.Features.LotrIssues.Domain;

namespace TAOM.Tests.Features.LotrIssues;

/// <summary>
/// The rules the three issue templates share: the frequency map (LotrIssuesCampaignBehavior uses it too
/// when it offers an issue) and the relation floor a player must reach to take the quest. Expected values
/// are the templates' code before the fold.
/// </summary>
[TestClass]
public class LotrIssueTemplateRulesTests
{
    private static LotrIssueDefinition Def(IssueFrequencyTier frequency = IssueFrequencyTier.Common, int relationMin = -10)
        => new LotrIssueDefinition("lotr_x", LotrIssueTemplate.DeliverGoods, IssueGiverOccupation.Headman, frequency,
            new List<string>(), 1, 0f, "", "", 0, 0f, 0, "", "", relationMin,
            new LotrIssueText("", "", "", "", "", "", "", "", ""));

    [DataTestMethod]
    [DataRow(IssueFrequencyTier.VeryCommon, IssueBase.IssueFrequency.VeryCommon)]
    [DataRow(IssueFrequencyTier.Common, IssueBase.IssueFrequency.Common)]
    [DataRow(IssueFrequencyTier.Rare, IssueBase.IssueFrequency.Rare)]
    public void MapFrequency_EachTier_MapsToTheEngineFrequency(IssueFrequencyTier tier, IssueBase.IssueFrequency expected)
        => Assert.AreEqual(expected, LotrIssueTemplateRules.MapFrequency(tier));

    [TestMethod]
    public void MapFrequency_UndefinedTierValue_FallsBackToCommon()
        => Assert.AreEqual(IssueBase.IssueFrequency.Common, LotrIssueTemplateRules.MapFrequency((IssueFrequencyTier)99));

    [TestMethod]
    public void FrequencyOf_NoDefinition_IsCommon()
        => Assert.AreEqual(IssueBase.IssueFrequency.Common, LotrIssueTemplateRules.FrequencyOf(null));

    [TestMethod]
    public void FrequencyOf_Definition_MapsItsTier()
        => Assert.AreEqual(IssueBase.IssueFrequency.Rare, LotrIssueTemplateRules.FrequencyOf(Def(IssueFrequencyTier.Rare)));

    [DataTestMethod]
    [DataRow(4, true)]
    [DataRow(5, false)]
    [DataRow(6, false)]
    public void IsBelowRelationMin_DefinitionFloorOfFive_IsStrictlyBelow(int relation, bool expected)
        => Assert.AreEqual(expected, LotrIssueTemplateRules.IsBelowRelationMin(relation, Def(relationMin: 5)));

    [DataTestMethod]
    [DataRow(-11, true)]
    [DataRow(-10, false)]
    public void IsBelowRelationMin_NoDefinition_UsesMinusTen(int relation, bool expected)
        => Assert.AreEqual(expected, LotrIssueTemplateRules.IsBelowRelationMin(relation, null));
}
