using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.CampaignSystem;
using TAOM.Features.LotrIssues;

namespace TAOM.Tests.Features.LotrIssues;

// NOTE: SandBox.dll is not on the unit-test host's probe path, so the 7 SandBox issue types resolve
// only in-game. These tests therefore pin the compile-checked 36 CampaignSystem types + the 7 declared
// SandBox names + the intended total of 43 — without forcing a SandBox.dll load.
[TestClass]
public class LotrIssueSuppressionTests
{
    [TestMethod]
    public void ExpectedVanillaIssueCount_Is43()
        => Assert.AreEqual(43, LotrIssueSuppression.ExpectedVanillaIssueCount);

    [TestMethod]
    public void CampaignSystemIssueTypes_AreExactly36()
        => Assert.AreEqual(36, LotrIssueSuppression.CampaignSystemIssueTypes.Count);

    [TestMethod]
    public void SandBoxIssueTypeNames_AreExactly7()
        => Assert.AreEqual(7, LotrIssueSuppression.SandBoxIssueTypeNames.Length);

    [TestMethod]
    public void CampaignSystemIssueTypes_AllDistinct()
    {
        var set = new HashSet<Type>(LotrIssueSuppression.CampaignSystemIssueTypes);
        Assert.AreEqual(LotrIssueSuppression.CampaignSystemIssueTypes.Count, set.Count);
    }

    [TestMethod]
    public void CampaignSystemIssueTypes_AllAreCampaignBehaviors()
    {
        foreach (var t in LotrIssueSuppression.CampaignSystemIssueTypes)
            Assert.IsTrue(typeof(CampaignBehaviorBase).IsAssignableFrom(t), $"{t.Name} is not a CampaignBehaviorBase");
    }

    [TestMethod]
    public void CampaignSystemIssueTypes_AllInIssuesNamespace()
    {
        foreach (var t in LotrIssueSuppression.CampaignSystemIssueTypes)
            Assert.AreEqual("TaleWorlds.CampaignSystem.Issues", t.Namespace, $"{t.Name} is in an unexpected namespace");
    }

    [TestMethod]
    public void DoesNotIncludeHostSpawner()
    {
        Assert.IsFalse(LotrIssueSuppression.CampaignSystemIssueTypes.Any(t => t.Name == "IssuesCampaignBehavior"));
        Assert.IsFalse(LotrIssueSuppression.SandBoxIssueTypeNames.Any(n => n.Contains("IssuesCampaignBehavior")));
    }

    [TestMethod]
    public void SandBoxIssueTypeNames_AllAssemblyQualifiedToSandBox()
    {
        foreach (var n in LotrIssueSuppression.SandBoxIssueTypeNames)
        {
            Assert.IsTrue(n.StartsWith("SandBox.Issues."), $"'{n}' not in SandBox.Issues");
            Assert.IsTrue(n.EndsWith(", SandBox"), $"'{n}' not assembly-qualified to SandBox");
        }
    }
}
