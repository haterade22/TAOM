using System;
using System.Reflection;
using System.Runtime.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
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
}
