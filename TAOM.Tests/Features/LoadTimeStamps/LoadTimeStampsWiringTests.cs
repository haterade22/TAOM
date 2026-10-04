using System;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.LoadTimeStamps;

/// <summary>
/// Source gate for the load-time stamps in SubModule.cs (single owner, so the wiring is text): each
/// patch phase ends right after its runner call, the per-category lines are written where the
/// toggle can be read, and the hook stamps sit on the right side of the once-per-process guard.
/// </summary>
[TestClass]
public class LoadTimeStampsWiringTests
{
    private const string DetailLine =
        @"\s*_patches\.WriteHeldCategoryLines\(Features\.LoadTimeStamps\.LoadTimeStampsHooks\.DetailEnabled\);";

    private static string Code => RepoPaths.ReadSource("Main/SubModule.cs", stripComments: true);

    [TestMethod]
    public void SubModule_EndsEachPatchPhase_RightAfterItsRunnerCall()
    {
        var code = Code;

        StringAssert.Matches(code, new Regex(
            @"RunPhase\(ApplyPhase\.ProcessLoad, TryPatchCategory\);\s*_patches\.EndPhase\(""OnSubModuleLoad""\);"));
        StringAssert.Matches(code, new Regex(
            @"RunPhase\(ApplyPhase\.MainMenu, TryPatchCategory\);\s*_patches\.EndPhase\(""MainMenu""\);"));
        StringAssert.Matches(code, new Regex(
            @"RunPhase\(ApplyPhase\.GameInit, TryPatchCategory\);\s*_patches\.EndPhase\(""GameInit""\);" + DetailLine));
        StringAssert.Matches(code, new Regex(
            @"RunPhase\(ApplyPhase\.FirstMission, TryPatchCategory\);\s*_patches\.EndPhase\(""Mission""\);" + DetailLine));
    }

    [TestMethod]
    public void SubModule_OnGameStart_StampsItsSteps()
    {
        var body = SubModuleMethodBody("protected override void OnGameStart(Game game, IGameStarter gameStarterObject)");

        var start = IndexOf(body, "LoadTimeStampsHooks.StartHook(\"OnGameStart\"");
        var snapshot = IndexOf(body, "hookStamp?.Mark(\"session_snapshot\");");
        var handWired = IndexOf(body, "hookStamp?.Mark(\"hand_wired\");");
        var modules = IndexOf(body, "hookStamp?.Mark(\"feature_modules\");");
        var end = IndexOf(body, "hookStamp?.End();");

        Assert.IsTrue(start < snapshot && snapshot < handWired && handWired < modules && modules < end,
            "OnGameStart's stamps are out of order");
        Assert.IsTrue(IndexOf(body, "RegisterCampaignLifeBehaviors(campaignStarter);") < handWired,
            "hand_wired must follow the hand-wired CampaignGameStarter block");
        Assert.IsTrue(IndexOf(body, "FeatureModuleHooks.AddGameStartContent(gameStarterObject);") < modules,
            "feature_modules must follow the feature-module content");
    }

    [TestMethod]
    public void SubModule_OnGameInitializationFinished_StampsTheEveryGamePartBeforeTheGuard()
    {
        var body = SubModuleMethodBody("public override void OnGameInitializationFinished(Game game)");

        var guard = IndexOf(body, "if (_gameInitPatchesApplied) return;");
        var guardSet = IndexOf(body, "_gameInitPatchesApplied = true;");

        Assert.IsTrue(IndexOf(body, "StartHook(\"OnGameInitializationFinished\"") < guard);
        Assert.IsTrue(IndexOf(body, "hookStamp?.End();") < guard);
        Assert.IsTrue(IndexOf(body, "StartHook(\"GameInitOnce\"") > guardSet);

        var trimmed = body.TrimEnd().TrimEnd('}').TrimEnd();
        StringAssert.EndsWith(trimmed, "onceStamp?.End();", "onceStamp?.End(); must be the method's last statement");
    }

    [TestMethod]
    public void SubModule_OnGameInitializationFinished_LogsTheLoadXmlSummaryBeforeTheGuard()
    {
        var body = SubModuleMethodBody("public override void OnGameInitializationFinished(Game game)");

        var summary = IndexOf(body, "Features.LoadTimeStamps.LoadTimeStampsHooks.LogLoadXmlSummary();");

        Assert.IsTrue(IndexOf(body, "hookStamp?.Mark(\"armour_gate\");") < summary, "the summary must follow the armour gate step");
        Assert.IsTrue(summary < IndexOf(body, "hookStamp?.End();"), "the summary must come before the hook's total");
        Assert.IsTrue(summary < IndexOf(body, "if (_gameInitPatchesApplied) return;"), "the summary must come before the guard");
    }

    private static int IndexOf(string body, string text)
    {
        var at = body.IndexOf(text, StringComparison.Ordinal);
        Assert.IsTrue(at >= 0, "missing: " + text);
        return at;
    }

    private static string SubModuleMethodBody(string signature)
    {
        var code = RepoPaths.ReadSource("Main/SubModule.cs", stripComments: true);
        var start = code.IndexOf(signature, StringComparison.Ordinal);
        Assert.IsTrue(start >= 0, "SubModule.cs no longer declares " + signature);

        var open = code.IndexOf('{', start);
        var depth = 0;
        for (var i = open; i < code.Length; i++)
        {
            if (code[i] == '{') depth++;
            else if (code[i] == '}' && --depth == 0)
                return code.Substring(open, i - open + 1);
        }

        Assert.Fail("Unbalanced braces after " + signature);
        return string.Empty;
    }
}
