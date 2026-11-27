using System;
using System.Reflection;
using System.Runtime.Serialization;
using TAOM.Tests.Infrastructure;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.LotrIssues;
using TAOM.Features.LotrIssues.Domain;
using TAOM.Features.LotrIssues.Templates;
using TaleWorlds.CampaignSystem.Issues;

namespace TAOM.Tests.Features.LotrIssues;

/// <summary>
/// Engine-contract invariants for the generic issue templates that unit tests of the pure service can't
/// cover. These pin behaviors whose default would silently break the "one engine type, many configs"
/// design — so the fixes cannot regress unnoticed.
/// </summary>
[TestClass]
public class LotrIssueTemplateInvariantsTests
{
    // Codex review #61 (2026-06-20), HIGH: every config of a template instantiates the SAME engine type, so
    // IssueBase.CheckPreconditions' same-GetType() accept gate (IssueBase.cs:907-918) would cap the player at
    // ONE active quest per template across all its configs unless IssueQuestCanBeDuplicated is overridden to
    // true. The 5-agent deep-review found the spawn-side type throttle but missed this accept-side gate.
    // Reflection over an uninitialized instance avoids needing a live Hero/campaign — the getter is a constant.
    [DataTestMethod]
    [DataRow(typeof(CombatLotrIssue))]
    [DataRow(typeof(DeliverGoodsLotrIssue))]
    [DataRow(typeof(DeliverPersonnelLotrIssue))]
    [DataRow(typeof(HuntBandLotrIssue))]
    public void Template_OverridesIssueQuestCanBeDuplicated_True(Type templateType)
    {
        var prop = typeof(IssueBase).GetProperty(
            "IssueQuestCanBeDuplicated", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(prop, "IssueBase.IssueQuestCanBeDuplicated not found — engine API drift, re-verify.");

        var instance = FormatterServices.GetUninitializedObject(templateType);
        var value = (bool)prop.GetGetMethod(nonPublic: true).Invoke(instance, null);

        Assert.IsTrue(value,
            $"{templateType.Name} must override `IssueQuestCanBeDuplicated => true`. Without it, every config " +
            "sharing this template's type is capped at one concurrent active quest by IssueBase.CheckPreconditions " +
            "(Codex review #61 HIGH). Do not remove the override.");
    }

    [TestMethod]
    public void TrainRetainers_DoesNotAllowDuplicateQuests()
    {
        var prop = typeof(IssueBase).GetProperty(
            "IssueQuestCanBeDuplicated", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(prop, "IssueBase.IssueQuestCanBeDuplicated not found: engine API drift, re-verify.");
        var instance = FormatterServices.GetUninitializedObject(typeof(TrainRetainersLotrIssue));
        var value = (bool)prop.GetGetMethod(nonPublic: true).Invoke(instance, null);
        Assert.IsFalse(value,
            "two loans of one culture line would count each other's troops, so TrainRetainers must stay non-duplicable.");
    }

    [TestMethod]
    public void SaveDefiner_RegistersTheNewPairsAt107To110()
    {
        var src = RepoPaths.ReadSource("Main/Features/LotrIssues/LotrIssueSaveableTypeDefiner.cs", stripComments: true);
        StringAssert.Contains(src, "AddClassDefinition(typeof(TrainRetainersLotrIssue), 107);");
        StringAssert.Contains(src, "AddClassDefinition(typeof(TrainRetainersLotrIssueQuest), 108);");
        StringAssert.Contains(src, "AddClassDefinition(typeof(HuntBandLotrIssue), 109);");
        StringAssert.Contains(src, "AddClassDefinition(typeof(HuntBandLotrIssueQuest), 110);");
        StringAssert.Contains(src, "AddClassDefinition(typeof(CombatLotrIssueQuest), 106);");
    }

    [TestMethod]
    public void CombatIssue_DropsAnOfferWhoseDefinitionLeftCombat()
        => StringAssert.Contains(
            RepoPaths.ReadSource("Main/Features/LotrIssues/Templates/CombatLotrIssue.cs", stripComments: true),
            "StillUses(_def, LotrIssueTemplate.Combat)");

    [TestMethod]
    public void HuntBandQuest_ClearsItsBandBeforeCompleting()
    {
        var src = RepoPaths.ReadSource("Main/Features/LotrIssues/Templates/HuntBandLotrIssue.cs", stripComments: true);
        int handler = src.IndexOf("private void OnMobilePartyDestroyed", StringComparison.Ordinal);
        Assert.IsTrue(handler >= 0, "destroyed handler not found");
        int clear = src.IndexOf("_band = null;", handler, StringComparison.Ordinal);
        int complete = src.IndexOf("CompleteQuestWith", handler, StringComparison.Ordinal);
        Assert.IsTrue(clear > 0 && complete > 0 && clear < complete,
            "the engine raises MobilePartyDestroyed before it removes the party, so the quest must null _band first or OnFinalize destroys it twice.");
        Assert.IsTrue(src.IndexOf("Success();", handler, StringComparison.Ordinal) > clear,
            "the success path must run after _band is cleared");
    }

    [DataTestMethod]
    [DataRow("Main/Features/LotrIssues/Templates/TrainRetainersLotrIssue.cs")]
    [DataRow("Main/Features/LotrIssues/Templates/HuntBandLotrIssue.cs")]
    public void NewTemplates_GateWorldWritesOnShouldDeferToHost(string file)
        => StringAssert.Contains(RepoPaths.ReadSource(file, stripComments: true), "Coop.ShouldDeferToHost");

    [DataTestMethod]
    [DataRow("Main/Features/LotrIssues/Templates/TrainRetainersLotrIssue.cs")]
    [DataRow("Main/Features/LotrIssues/Templates/HuntBandLotrIssue.cs")]
    public void NewTemplates_NeverReadTheThrowingPartyBaseMainParty(string file)
        => Assert.IsFalse(RepoPaths.ReadSource(file, stripComments: true).Contains("PartyBase.MainParty"),
            "PartyBase.MainParty throws when there is no main party; read Campaign.Current?.MainParty.");

    [TestMethod]
    public void TemplateType_MapsTrainRetainersAndHuntBand()
    {
        var method = typeof(LotrIssuesCampaignBehavior).GetMethod("TemplateType", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.IsNotNull(method, "LotrIssuesCampaignBehavior.TemplateType not found");
        Assert.AreEqual(typeof(TrainRetainersLotrIssue), method.Invoke(null, new object[] { LotrIssueTemplate.TrainRetainers }));
        Assert.AreEqual(typeof(HuntBandLotrIssue), method.Invoke(null, new object[] { LotrIssueTemplate.HuntBand }));
        Assert.AreEqual(typeof(CombatLotrIssue), method.Invoke(null, new object[] { LotrIssueTemplate.Combat }));
    }
}
