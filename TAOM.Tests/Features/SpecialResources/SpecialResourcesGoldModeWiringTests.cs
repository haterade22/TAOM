using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static TAOM.Tests.Infrastructure.RepoPaths;

namespace TAOM.Tests.Features.SpecialResources;

/// <summary>
/// #1109 source pins for the engine-coupled gates (behavior, map bar, the two GameModels). The
/// behavior and the models cannot be constructed without a running game, so these read the code
/// that decides the order of the gold-mode return against the work it must stand down.
/// </summary>
[TestClass]
public class SpecialResourcesGoldModeWiringTests
{
    private const string Behavior = "Main/Features/SpecialResources/SpecialResourcesBehavior.cs";

    /// <summary>The text of one method, from its signature to the next member at the same indent.</summary>
    internal static string MethodBody(string path, string signature)
    {
        var text = ReadSource(path, stripComments: true);
        var start = text.IndexOf(signature, StringComparison.Ordinal);
        Assert.IsTrue(start >= 0, $"{signature} not found in {path}: update this pin.");
        var open = text.IndexOf('{', start);
        int depth = 0;
        for (var i = open; i < text.Length; i++)
        {
            if (text[i] == '{') depth++;
            else if (text[i] == '}' && --depth == 0) return text.Substring(start, i - start + 1);
        }
        Assert.Fail($"The body of {signature} did not close.");
        return "";
    }

    private static void AssertBefore(string body, string first, string second, string why)
    {
        var a = body.IndexOf(first, StringComparison.Ordinal);
        var b = body.IndexOf(second, StringComparison.Ordinal);
        Assert.IsTrue(a >= 0, $"missing: {first}");
        Assert.IsTrue(b >= 0, $"missing: {second}");
        Assert.IsTrue(a < b, why);
    }

    // ── SpecialResourcesBehavior ──

    [TestMethod]
    public void OnDailyTickHero_GoldModeReturnBeforeApplyDailyTick()
    {
        var body = MethodBody(Behavior, "private void OnDailyTickHero(Hero hero)");

        StringAssert.Contains(body, "if (_service.IsGoldMode) { _isFirstTickAfterLoad = true; return; }");
        AssertBefore(body, "_service.IsGoldMode", "_service.ResolveResource(", "gold mode must stop the tick before it resolves a resource");
        AssertBefore(body, "_service.IsGoldMode", "ApplyDailyTick", "gold mode must stop the tick before it charges upkeep");
        AssertBefore(body, "_service.IsGoldMode", "CalculateDesertion", "gold mode must stop the tick before desertion");
        AssertBefore(body, "if (hero != Hero.MainHero) return;", "_service.IsGoldMode", "only the main hero's tick is gated");
    }

    [TestMethod]
    public void CanEarn_GoldModeCheckPrecedesDedicatedServerLog()
    {
        var body = MethodBody(Behavior, "private bool CanEarn()");

        StringAssert.Contains(body, "if (_service.IsGoldMode) return false;");
        AssertBefore(body, "_service.IsGoldMode", "MayCreditMainHero", "gold mode must not reach the dedicated-server branch or its log line");
    }

    [TestMethod]
    public void OnUnitRecruited_GoldModeCheckPrecedesChargeRecruitCost()
    {
        var body = MethodBody(Behavior, "private void OnUnitRecruited(CharacterObject character, int count)");

        StringAssert.Contains(body, "_service.IsGoldMode");
        AssertBefore(body, "_service.IsGoldMode", "ChargeRecruitCost", "a gold-mode recruit must never charge the resource");
    }

    // ── Prisoner recruits ──
    // The party screen's prisoner recruit never prices through GetTroopRecruitmentCost, and in gold
    // mode OnUnitRecruited stands down, so a separate behavior charges the gold. It must listen to the
    // prisoner event only: OnUnitRecruited also fires for volunteers and mercenaries, whose price
    // already carries the surcharge, so charging there would bill them twice.

    private const string PrisonerBehavior = "Main/Features/SpecialResources/SpecialResourceGoldPrisonerBehavior.cs";

    [TestMethod]
    public void PrisonerBehavior_ListensToThePrisonerEventOnly()
    {
        var text = ReadSource(PrisonerBehavior, stripComments: true);

        StringAssert.Contains(text, "CampaignEvents.OnMainPartyPrisonerRecruitedEvent.AddNonSerializedListener(");
        Assert.IsFalse(text.Contains("OnUnitRecruitedEvent"), "the gold prisoner charge must not listen to OnUnitRecruited (volunteers would pay twice)");
    }

    [TestMethod]
    public void PrisonerBehavior_GoldModeCheckPrecedesTheDebit()
    {
        var body = MethodBody(PrisonerBehavior, "private void OnPrisonersRecruited(FlattenedTroopRoster roster)");

        AssertBefore(body, "_service.IsGoldMode", "GetGoldPrisonerRecruitCharge", "resource mode must not price prisoners in gold");
        AssertBefore(body, "GetGoldPrisonerRecruitCharge", "GiveGoldAction.ApplyBetweenCharacters(", "the debit is the priced charge");
    }

    [TestMethod]
    public void SubModule_RegistersThePrisonerBehavior()
    {
        var text = ReadSource("Main/SubModule.cs", stripComments: true);

        StringAssert.Contains(text, "campaignStarter.AddBehavior(new SpecialResourceGoldPrisonerBehavior(specialResourceService, specialResourceLogger));");
    }

    // ── Map bar ──

    [TestMethod]
    public void GoldMode_FoldsIntoNoResourceBranch()
    {
        var text = ReadSource("Main/Features/SpecialResources/UI/SpecialResourceMapBarMixin.cs", stripComments: true);

        StringAssert.Contains(text, "var resource = _service.IsGoldMode ? null : _lastResource;");
    }
}
