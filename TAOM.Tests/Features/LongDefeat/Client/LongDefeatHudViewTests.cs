using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Client.Features.LongDefeat;
using TAOM.Features.LongDefeat.Diagnostics;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.LongDefeat.Client;

/// <summary>
/// The view cannot be built without the engine, so its contract is pinned the way <c>FordsClientWiringTests</c> pins Fords': the two
/// check verdicts as plain functions, and the engine hooks the design rests on as source scans.
/// </summary>
[TestClass]
public class LongDefeatHudViewTests
{
    private static string Host() => RepoPaths.ReadSource("Client/Features/LongDefeat/LongDefeatPanelHost.cs", stripComments: true);

    private static string View() => RepoPaths.ReadSource("Client/Features/LongDefeat/LongDefeatHudView.cs", stripComments: true);

    [TestMethod]
    public void TheHudCheck_PassesOnlyWithTheWaveTextSet_AndBothMoviesLoaded()
    {
        var pass = LongDefeatClientChecks.Hud("Preparation", true, true);
        Assert.AreEqual(CheckResult.Pass, pass.Result);
        StringAssert.Contains(pass.Detail, "movie=LongDefeatHud wave_text=set");
        Assert.AreEqual(CheckResult.Fail, LongDefeatClientChecks.Hud("", true, true).Result);
        Assert.AreEqual(CheckResult.Fail, LongDefeatClientChecks.Hud(null, true, true).Result);

        var noHud = LongDefeatClientChecks.Hud("Preparation", false, true);
        Assert.AreEqual(CheckResult.Fail, noHud.Result, "a movie that did not load fails however the view model reads");
        StringAssert.Contains(noHud.Detail, "hud_loaded=False");
        var noPanel = LongDefeatClientChecks.Hud("Preparation", true, false);
        Assert.AreEqual(CheckResult.Fail, noPanel.Result);
        StringAssert.Contains(noPanel.Detail, "panel_loaded=False");
    }

    [TestMethod]
    public void TheView_VetoesPhotoMode_BecauseNoViewCanLeaveIt()
    {
        StringAssert.Contains(View(), "IsPhotoModeAllowed() => false");
    }

    [TestMethod]
    public void TheView_ReadsIsLoaded_ForBothMovies()
    {
        StringAssert.Contains(View(), "Movie?.IsLoaded");
        StringAssert.Contains(View(), "LongDefeatPanelHost.ProbeLoads");
        StringAssert.Contains(Host(), "IsLoaded");
        StringAssert.Contains(Host(), "throw new System.InvalidOperationException", "a panel that did not load latches the view off with a fail line");
    }

    [TestMethod]
    public void ThePanel_LogsWhenItOpensAndCloses_ForRiskNine()
    {
        var host = Host();

        StringAssert.Contains(host, "\"[LongDefeat][Hud] \"");
        StringAssert.Contains(host, "_what + \" open\"");
        StringAssert.Contains(host, "_what + \" closed\"");
        StringAssert.Contains(host, "\"shop\"");
    }

    [TestMethod]
    public void TheClock_IgnoresABadStep_AndRefreshesOncePerInterval()
    {
        var clock = new LongDefeatHudClock();
        Assert.IsTrue(clock.TakeRefresh(), "the first refresh is due at once");
        Assert.IsFalse(clock.TakeRefresh());

        clock.Advance(float.NaN);
        clock.Advance(-1f);
        clock.Advance(float.PositiveInfinity);
        Assert.IsFalse(clock.TakeRefresh());
        Assert.IsFalse(clock.OrdersDelayElapsed);

        clock.Advance(LongDefeatHudClock.RefreshIntervalSec);
        Assert.IsTrue(clock.TakeRefresh(), "a NaN step did not freeze the refresh");
        clock.Advance(LongDefeatHudClock.OrdersDelaySec);
        Assert.IsTrue(clock.OrdersDelayElapsed);
    }

    [TestMethod]
    public void TheOrdersCheck_PassesOnlyWithAtLeastOneOrderSet_AndAZeroNamesTheFriendlyFlag()
    {
        var pass = LongDefeatClientChecks.Orders(6);
        Assert.AreEqual(CheckResult.Pass, pass.Result);
        Assert.AreEqual("sets=6", pass.Detail);

        var fail = LongDefeatClientChecks.Orders(0);
        Assert.AreEqual(CheckResult.Fail, fail.Result);
        StringAssert.Contains(fail.Detail, "sets=0");
        Assert.AreEqual(CheckResult.Fail, LongDefeatClientChecks.Orders(-1).Result);
    }

    [TestMethod]
    public void TheHudLayer_TakesNoInputRestriction_BecauseThePlayerHasAnAgent()
    {
        var source = View();
        var start = source.IndexOf("new GauntletLayer(\"GauntletLayer\", HudLayerOrder, false)", System.StringComparison.Ordinal);

        Assert.IsTrue(start >= 0, "the HUD layer is a plain layer");
        var attach = source.Substring(start, source.IndexOf("LoadMovie(\"LongDefeatHud\"", start, System.StringComparison.Ordinal) - start);
        Assert.IsFalse(attach.Contains("InputRestrictions"), "Fords sets a whole-mission cursor because it has no agent; this mode must not");
    }

    [TestMethod]
    public void ThePanelLayer_IsAFocusLayerInTheEscapeMenusShape_AndNeverPausesTheEngine()
    {
        var host = Host();
        var source = View() + host;

        StringAssert.Contains(host, "IsFocusLayer = true");
        StringAssert.Contains(host, "InputRestrictions.SetInputRestrictions()");
        StringAssert.Contains(host, "RegisterHotKeyCategory(HotKeyManager.GetCategory(\"GenericPanelGameKeyCategory\"))");
        StringAssert.Contains(host, "ScreenManager.TrySetFocus");
        StringAssert.Contains(host, "IsHotKeyReleased(\"Exit\")");
        Assert.IsFalse(source.Contains("PauseGameEngine"), "the wave timer runs while the panel is open");
    }

    [TestMethod]
    public void TheKeys_AreBAndH_WithCtrlAndAltLeftToTheGame()
    {
        var source = Host();

        StringAssert.Contains(source, "InputKey.B");
        StringAssert.Contains(source, "InputKey.H");
        StringAssert.Contains(source, "IsControlDown()");
        StringAssert.Contains(source, "IsAltDown()");
    }

    [TestMethod]
    public void TheSpeedKey_IsM_NeverTheChatKeyT_AndIsReadWithAPanelOpenToo()
    {
        var host = Host();

        StringAssert.Contains(host, "InputKey.M");
        StringAssert.Contains(host, "CycleSpeed()");
        Assert.IsFalse(host.Contains("InputKey.T"), "GauntletChatLogView reads game key 6 (T) from the mission's scene layer, and a press opens the chat box");
        Assert.IsTrue(host.IndexOf("InputKey.M", System.StringComparison.Ordinal) < host.IndexOf("InputKey.B", System.StringComparison.Ordinal),
            "M is read before the early return that B and H sit behind while a panel is open");
    }

    [TestMethod]
    public void TheView_AppliesTheSpeedEachTick_StopsItOnDetach_AndAnArmedRunStartsAtItsSpeed()
    {
        var view = View();

        StringAssert.Contains(view, "LongDefeatMissionSpeed");
        StringAssert.Contains(view, "_speed?.Apply(Mission, _session.Speed, dt)");
        StringAssert.Contains(view, "LongDefeatMissionSpeed.Stop(Mission)");
        StringAssert.Contains(view, "SetSpeed(plan.Speed)");
    }

    [TestMethod]
    public void TryDetach_StopsTheSpeedFirst_BeforeAnyPanelWorkThatCouldThrow()
    {
        var view = View();
        var detach = view.Substring(view.IndexOf("private void TryDetach", System.StringComparison.Ordinal));

        Assert.IsTrue(detach.IndexOf("LongDefeatMissionSpeed.Stop(Mission)", System.StringComparison.Ordinal)
            < detach.IndexOf("_panel.Remove(MissionScreen)", System.StringComparison.Ordinal));
    }

    [TestMethod]
    public void TheSpeedAdapter_AsksWhetherAPersonDrivesALiveMainAgent_AndUsesThePureFrameRule()
    {
        var adapter = RepoPaths.ReadSource("Main/Adapters/LongDefeatMissionSpeed.cs", stripComments: true);

        StringAssert.Contains(adapter, "LongDefeatSpeedFrame.For(");
        StringAssert.Contains(adapter, "MainAgent");
        StringAssert.Contains(adapter, "IsPlayerControlled");
    }

    [TestMethod]
    public void TheSpeedAdapter_ReusesTheFordsRule_LogsEachChange_AndStopsWhenTheMissionEnds()
    {
        var adapter = RepoPaths.ReadSource("Main/Adapters/LongDefeatMissionSpeed.cs", stripComments: true);

        StringAssert.Contains(RepoPaths.ReadSource("Main/Features/LongDefeat/LongDefeatSpeedFrame.cs", stripComments: true), "FordsGameSpeed");
        StringAssert.Contains(adapter, "[LongDefeat][Speed] speed=");
        StringAssert.Contains(adapter, "!mission.MissionEnded");
        StringAssert.Contains(adapter, "SetFastForwardingFromUI");
        StringAssert.Contains(adapter, "FixedDeltaTimeMode");
    }

    [TestMethod]
    public void AnArmedRun_RefusesTheFocusLostEscapeMenu()
    {
        var source = View();

        StringAssert.Contains(source, "IsOpeningEscapeMenuOnFocusChangeAllowed");
        StringAssert.Contains(source, "LongDefeatAutorunPlan.ArmedInThisProcess");
    }

    [TestMethod]
    public void TheView_WritesBothClientCheckIds_AsLiterals()
    {
        var ids = RepoPaths.ReadSource("Client/Features/LongDefeat/LongDefeatClientChecks.cs", stripComments: true);

        StringAssert.Contains(ids, "\"taom.long-defeat.hud\"");
        StringAssert.Contains(ids, "\"taom.long-defeat.orders\"");
        StringAssert.Contains(View(), "VisualOrderFactory.GetOrders()");
        StringAssert.Contains(View(), "LongDefeatClientChecks.HudId");
        StringAssert.Contains(View(), "LongDefeatClientChecks.OrdersId");
    }

    [TestMethod]
    public void TheMissionViewList_HasTheHudView_AndNoLongerTheUnattendedOne()
    {
        var list = RepoPaths.ReadSource("Client/Features/LongDefeat/LongDefeatMissionViews.cs", stripComments: true);

        StringAssert.Contains(list, "new LongDefeatHudView()");
        Assert.IsFalse(list.Contains("LongDefeatUnattendedView"));
        Assert.IsFalse(System.IO.File.Exists(RepoPaths.RepoPath("Client", "Features", "LongDefeat", "LongDefeatUnattendedView.cs")),
            "the HUD view carries the focus guard now (plan section 1, Deleted)");
    }

    // Players asked for the kill feed (#1159): every Custom Battle view list adds vanilla's, and this hand-built list had left it out.
    [TestMethod]
    public void TheMissionViewList_HasVanillasKillFeed()
    {
        var list = RepoPaths.ReadSource("Client/Features/LongDefeat/LongDefeatMissionViews.cs", stripComments: true);

        StringAssert.Contains(list, "ViewCreator.CreateSingleplayerMissionKillNotificationUIHandler()");
    }
}
