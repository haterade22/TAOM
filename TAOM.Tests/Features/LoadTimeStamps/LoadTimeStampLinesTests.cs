using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.LoadTimeStamps;

namespace TAOM.Tests.Features.LoadTimeStamps;

/// <summary>
/// Pins every load-time stamp line literally (docs/features/load-time-stamps.md "Log lines"): a log
/// reader, human or tool, matches these strings, so a format change must be a deliberate test edit.
/// </summary>
[TestClass]
public class LoadTimeStampLinesTests
{
    [TestMethod]
    public void Ready_DescribesTheAlwaysOnAndTheToggledLines()
    {
        Assert.AreEqual(
            "[LoadStamps] ready: [PatchApply] phase totals are always written; per-category [PatchApply] and per-hook [LoadPhase] lines follow \"Enable Load-Time Stamps\" (Battle Load Diagnostics page, default off): the per-hook lines read it at each game start and game initialization, the per-category lines once, at the first game initialization after launch",
            LoadTimeStampLines.Ready());
    }

    [TestMethod]
    public void DetailUnreadable_NamesTheToggleTheExceptionAndWhatIsStillWritten()
    {
        Assert.AreEqual(
            "[LoadStamps] detail off: \"Enable Load-Time Stamps\" could not be read (InvalidOperationException: mcm); only the always-on load-time totals are written",
            LoadTimeStampLines.DetailUnreadable(new System.InvalidOperationException("mcm")));
    }

    [TestMethod]
    public void Detail_On_SaysTheDetailedLinesAreWritten()
    {
        Assert.AreEqual(
            "[LoadStamps] detail on: the per-category, per-hook and per-handler load-time lines are written",
            LoadTimeStampLines.Detail(true));
    }

    [TestMethod]
    public void Detail_Off_SaysOnlyTheTotalsAreWrittenAndNamesTheToggle()
    {
        Assert.AreEqual(
            "[LoadStamps] detail off: only the always-on load-time totals are written; turn on \"Enable Load-Time Stamps\" for the per-category, per-hook and per-handler lines",
            LoadTimeStampLines.Detail(false));
    }

    [TestMethod]
    public void PatchCategoryLine_Ok_FormatsTwoDecimalsAndResultOk()
    {
        Assert.AreEqual(
            "[PatchApply] phase=GameInit category=Patch11_Diplomacy ms=4.21 result=ok",
            LoadTimeStampLines.PatchCategoryLine("GameInit", "Patch11_Diplomacy", 4.2149, true));
    }

    [TestMethod]
    public void PatchCategoryLine_Failed_PrintsResultFailed()
    {
        Assert.AreEqual(
            "[PatchApply] phase=OnSubModuleLoad category=Patch37_CrashReport ms=0.50 result=failed",
            LoadTimeStampLines.PatchCategoryLine("OnSubModuleLoad", "Patch37_CrashReport", 0.5, false));
    }

    [TestMethod]
    public void PatchCategoryLine_UnderAGermanCulture_StillUsesADot()
    {
        var previous = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");
            StringAssert.Contains(LoadTimeStampLines.PatchCategoryLine("GameInit", "Patch11_Diplomacy", 4.2149, true), "ms=4.21");
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = previous;
        }
    }

    [TestMethod]
    public void PatchPhaseTotal_NamesCountsTotalAndTheSlowestCategory()
    {
        Assert.AreEqual(
            "[PatchApply] phase=GameInit scope=total categories=74 failed=1 ms=312.40 max_ms=41.07 max_category=Patch2_RefreshTableau",
            LoadTimeStampLines.PatchPhaseTotal("GameInit", 74, 1, 312.4, 41.07, "Patch2_RefreshTableau"));
    }

    [TestMethod]
    public void PatchPhaseTotal_NoCategories_PrintsZerosAndNone()
    {
        Assert.AreEqual(
            "[PatchApply] phase=Mission scope=total categories=0 failed=0 ms=0.00 max_ms=0.00 max_category=none",
            LoadTimeStampLines.PatchPhaseTotal("Mission", 0, 0, 0, 0, null));
    }

    [TestMethod]
    public void HookStep_NamesTheHookTheStepAndItsTime()
    {
        Assert.AreEqual(
            "[LoadPhase] hook=OnGameStart step=hand_wired ms=120.50",
            LoadTimeStampLines.HookStep("OnGameStart", "hand_wired", 120.5));
    }

    [TestMethod]
    public void HookTotal_NamesTheGameTheTotalAndTheStepCount()
    {
        Assert.AreEqual(
            "[LoadPhase] hook=OnGameStart game=Campaign scope=total ms=133.25 steps=3",
            LoadTimeStampLines.HookTotal("OnGameStart", "Campaign", 133.25, 3));
    }

    [TestMethod]
    public void HookTotal_NullGame_PrintsNone()
    {
        Assert.AreEqual(
            "[LoadPhase] hook=GameInitOnce game=none scope=total ms=1.00 steps=2",
            LoadTimeStampLines.HookTotal("GameInitOnce", null, 1.0, 2));
    }

    [TestMethod]
    public void ToMs_ConvertsTicksAtTheGivenFrequency()
    {
        Assert.AreEqual(1500.0, LoadTimeStampLines.ToMs(1500, 1000));
    }

    [TestMethod]
    public void ToMs_ZeroFrequency_IsZero()
    {
        Assert.AreEqual(0.0, LoadTimeStampLines.ToMs(5, 0));
    }

    [TestMethod]
    public void LoadXmlReady_SaysTheLinesAreAlwaysWritten()
    {
        Assert.AreEqual(
            "[LoadXml] ready: one line per MBObjectManager.LoadXML call and a summary at every game initialization, always written",
            LoadTimeStampLines.LoadXmlReady());
    }

    [TestMethod]
    public void LoadXml_WithAMerge_SplitsMergeAndObjects()
    {
        Assert.AreEqual(
            "[LoadXml] id=NPCCharacters files=56 ms=13302.00 xslt=2 merge_ms=13001.50 objects_ms=300.50 result=ok",
            LoadTimeStampLines.LoadXml("NPCCharacters", 56, 13302, 2, 13001.5, 300.5, "ok"));
    }

    [TestMethod]
    public void LoadXml_WithoutAMerge_PrintsNoneAndTheExceptionType()
    {
        Assert.AreEqual(
            "[LoadXml] id=Items files=0 ms=3.25 xslt=0 merge_ms=none objects_ms=none result=XmlException",
            LoadTimeStampLines.LoadXml("Items", 0, 3.25, 0, null, null, "XmlException"));
    }

    [TestMethod]
    public void LoadXmlSummary_NamesTheGameTotalsAndTheSlowestType()
    {
        Assert.AreEqual(
            "[LoadXml] summary game=Campaign calls=26 files=329 xslt=6 ms=28000.00 merge_ms=26500.00 objects_ms=1500.00 max_ms=13302.00 max_id=NPCCharacters failed=0",
            LoadTimeStampLines.LoadXmlSummary("Campaign", 26, 329, 6, 28000, 26500, 1500, 13302, "NPCCharacters", 0));
    }

    [TestMethod]
    public void LoadXmlSummary_NoCalls_PrintsZerosAndNone()
    {
        Assert.AreEqual(
            "[LoadXml] summary game=none calls=0 files=0 xslt=0 ms=0.00 merge_ms=0.00 objects_ms=0.00 max_ms=0.00 max_id=none failed=0",
            LoadTimeStampLines.LoadXmlSummary(null, 0, 0, 0, 0, 0, 0, 0, null, 0));
    }

    [TestMethod]
    public void LifecycleReady_BindingOk_SaysTheDispatchLineIsAlwaysWrittenAndEveryHandlerIsTimedWithTheToggle()
    {
        Assert.AreEqual(
            "[Lifecycle] ready: listener binding ok; a dispatch line is always written for each new-game, game-loaded and session-start dispatch; with \"Enable Load-Time Stamps\" on, every handler of them is timed too, with a line for each at or over 10.00 ms",
            LoadTimeStampLines.LifecycleReady(null));
    }

    [TestMethod]
    public void LifecycleReady_BindingMissing_NamesTheProblemAndTheFallback()
    {
        Assert.AreEqual(
            "[Lifecycle] ready: listener binding missing (MbEvent<CampaignGameStarter>._nonSerializedListenerList not found); only a dispatch line for each new-game, game-loaded and session-start dispatch is written, whatever \"Enable Load-Time Stamps\" says",
            LoadTimeStampLines.LifecycleReady("MbEvent<CampaignGameStarter>._nonSerializedListenerList not found"));
    }

    [TestMethod]
    public void Handler_WithAnArgument_PrintsTheSlowestCallsIndex()
    {
        Assert.AreEqual(
            "[Lifecycle] event=OnNewGameCreatedPartialFollowUp handler=CultureMarketplaceBehavior.OnNewGameCreatedPartialFollowUp asm=TAOM calls=100 ms=3021.50 max_ms=2990.25 max_index=1",
            LoadTimeStampLines.Handler("OnNewGameCreatedPartialFollowUp", "CultureMarketplaceBehavior.OnNewGameCreatedPartialFollowUp", "TAOM", 100, 3021.5, 2990.25, 1));
    }

    [TestMethod]
    public void Handler_WithoutAnArgument_PrintsNone()
    {
        Assert.AreEqual(
            "[Lifecycle] event=OnSessionLaunched handler=X.OnSessionLaunched asm=SandBox calls=1 ms=12.00 max_ms=12.00 max_index=none",
            LoadTimeStampLines.Handler("OnSessionLaunched", "X.OnSessionLaunched", "SandBox", 1, 12, 12, -1));
    }

    [TestMethod]
    public void EventTotal_SplitsTaomAndOtherTime_AndNamesTheSlowestHandler()
    {
        Assert.AreEqual(
            "[Lifecycle] event=OnNewGameCreated scope=total listeners=84 taom_listeners=20 ms=3400.00 taom_ms=120.00 other_ms=3280.00 over_threshold=3 max_ms=2100.00 max_handler=HeroSpawnCampaignBehavior.OnNewGameCreated",
            LoadTimeStampLines.EventTotal("OnNewGameCreated", 84, 20, 3400, 120, 3280, 3, 2100, "HeroSpawnCampaignBehavior.OnNewGameCreated"));
    }

    [TestMethod]
    public void EventTotal_NoListeners_PrintsZerosAndNone()
    {
        Assert.AreEqual(
            "[Lifecycle] event=OnGameEarlyLoaded scope=total listeners=0 taom_listeners=0 ms=0.00 taom_ms=0.00 other_ms=0.00 over_threshold=0 max_ms=0.00 max_handler=none",
            LoadTimeStampLines.EventTotal("OnGameEarlyLoaded", 0, 0, 0, 0, 0, 0, 0, null));
    }

    [TestMethod]
    public void Dispatch_WithListeners_PrintsBothTimes()
    {
        Assert.AreEqual(
            "[Lifecycle] dispatch=OnNewGameCreated ms=6650.00 listeners_ms=6600.00 result=ok",
            LoadTimeStampLines.Dispatch("OnNewGameCreated", 6650, 6600, "ok"));
    }

    [TestMethod]
    public void Dispatch_WithoutListeners_PrintsNoneAndTheExceptionType()
    {
        Assert.AreEqual(
            "[Lifecycle] dispatch=OnSessionStart ms=40.00 listeners_ms=none result=NullReferenceException",
            LoadTimeStampLines.Dispatch("OnSessionStart", 40, null, "NullReferenceException"));
    }

    [TestMethod]
    public void LifecycleOff_NamesTheProblemAndWhatIsStillWritten()
    {
        Assert.AreEqual(
            "[Lifecycle] per-handler timing off for this session: boom; dispatch totals are still written",
            LoadTimeStampLines.LifecycleOff("boom"));
    }

    [TestMethod]
    public void LifecycleFault_NamesTheExceptionAndTheConsequence()
    {
        Assert.AreEqual(
            "[Lifecycle] stamp fault, some [Lifecycle] lines may be missing this session: InvalidOperationException: x",
            LoadTimeStampLines.LifecycleFault(new System.InvalidOperationException("x")));
    }

    [TestMethod]
    public void LifecycleRestoreFault_NamesTheExceptionAndTheConsequence()
    {
        Assert.AreEqual(
            "[Lifecycle] restore fault, a campaign handler may keep its timing wrapper this session (it still runs once per call): InvalidOperationException: x",
            LoadTimeStampLines.LifecycleRestoreFault(new System.InvalidOperationException("x")));
    }

    [TestMethod]
    public void LoadXmlFault_NamesTheExceptionAndTheConsequence()
    {
        Assert.AreEqual(
            "[LoadXml] stamp fault, some [LoadXml] lines may be missing this session: InvalidOperationException: x",
            LoadTimeStampLines.LoadXmlFault(new System.InvalidOperationException("x")));
    }

    [TestMethod]
    public void TotalLines_CarryScopeTotalAsAKeyValuePair_AndNoBareTotalToken()
    {
        var totals = new[]
        {
            LoadTimeStampLines.PatchPhaseTotal("GameInit", 74, 1, 312.4, 41.07, "Patch2_RefreshTableau"),
            LoadTimeStampLines.HookTotal("OnGameStart", "Campaign", 133.25, 3),
            LoadTimeStampLines.EventTotal("OnNewGameCreated", 84, 20, 3400, 120, 3280, 3, 2100, "HeroSpawnCampaignBehavior.OnNewGameCreated"),
        };

        foreach (var line in totals)
        {
            var tokens = line.Split(' ');
            CollectionAssert.Contains(tokens, "scope=total", line);
            CollectionAssert.DoesNotContain(tokens, "total", line);
        }
    }

    // Plan 029's log parser joins a bare word after a value to that value, so "phase=GameInit total"
    // misparses. A line with key=value pairs therefore holds nothing but pairs from its first one on.
    // A bare word straight after the [Tag], like the "summary" of a [LoadXml] summary, is not after a
    // value and stays.
    [TestMethod]
    public void EveryKeyValueLine_HasNoBareWordAfterAValue()
    {
        var lines = new[]
        {
            LoadTimeStampLines.PatchCategoryLine("GameInit", "Patch11_Diplomacy", 4.2149, true),
            LoadTimeStampLines.PatchPhaseTotal("GameInit", 74, 1, 312.4, 41.07, "Patch2_RefreshTableau"),
            LoadTimeStampLines.HookStep("OnGameStart", "hand_wired", 120.5),
            LoadTimeStampLines.HookTotal("OnGameStart", "Campaign", 133.25, 3),
            LoadTimeStampLines.LoadXml("NPCCharacters", 56, 13302, 2, 13001.5, 300.5, "ok"),
            LoadTimeStampLines.LoadXmlSummary("Campaign", 26, 329, 6, 28000, 26500, 1500, 13302, "NPCCharacters", 0),
            LoadTimeStampLines.Handler("OnNewGameCreatedPartialFollowUp", "CultureMarketplaceBehavior.OnNewGameCreatedPartialFollowUp", "TAOM", 100, 3021.5, 2990.25, 1),
            LoadTimeStampLines.EventTotal("OnNewGameCreated", 84, 20, 3400, 120, 3280, 3, 2100, "HeroSpawnCampaignBehavior.OnNewGameCreated"),
            LoadTimeStampLines.Dispatch("OnNewGameCreated", 6650, 6600, "ok"),
        };

        foreach (var line in lines)
        {
            var body = line.Substring(line.IndexOf(' ') + 1).Split(' ');
            var firstPair = Array.FindIndex(body, token => token.IndexOf('=') >= 0);
            Assert.IsTrue(firstPair >= 0, "no key=value pair: " + line);
            var bare = body.Skip(firstPair).Where(token => token.IndexOf('=') < 0).ToArray();
            CollectionAssert.AreEqual(new string[0], bare, "a bare word after a value in: " + line);
        }
    }
}
