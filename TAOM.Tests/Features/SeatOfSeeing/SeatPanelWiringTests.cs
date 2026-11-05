using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.SeatOfSeeing;

/// <summary>
/// The wiring of stages S2 and S3 (build-spec 6.0 to 6.4, section 7) as source-text pins: the view, the key reader and the adapter are
/// engine entry points that need a live mission, so what must exist is pinned by its text.
/// </summary>
[TestClass]
public class SeatPanelWiringTests
{
    private static string Source(string path) => RepoPaths.ReadSource(path, stripComments: true);

    private static string Flat(string text) => Regex.Replace(text, @"\s+", " ");

    private static string Session() => Flat(Source("Client/Features/SeatOfSeeing/SeatSession.cs"));

    // ---- The view and the key reader ----

    [TestMethod]
    public void TheView_TestsTheMoreKeysOncePerScreenTick_BeforeTheSession()
    {
        var view = Flat(Source("Client/Features/SeatOfSeeing/SeatView.cs"));

        Assert.AreEqual(1, Regex.Matches(view, @"_moreKeys\?\.Tick\(Input\);").Count);
        Assert.IsTrue(view.IndexOf("_moreKeys?.Tick(Input);", System.StringComparison.Ordinal) < view.IndexOf("_session.OnScreenTick(pressed);", System.StringComparison.Ordinal),
            "a press stored by the key reader is seen by the same tick's session");
        StringAssert.Contains(view, "_moreKeys = new SeatKeys(_keys, session);");
        StringAssert.Contains(view, "_moreKeys = null;");
    }

    [TestMethod]
    public void TheSession_GetsTheTrialsSettingsWhenArmed_AndThePlainOnesOtherwise()
    {
        var view = Flat(Source("Client/Features/SeatOfSeeing/SeatView.cs"));

        StringAssert.Contains(view, "CommandTrialRun.Settings(IoC.Resolve<ISeatSettingsProvider>())");
        Assert.AreEqual(1, Regex.Matches(view, @"IoC\.Resolve<ISeatSettingsProvider>\(\)").Count, "the session is the only reader of the provider");
    }

    [TestMethod]
    public void TheKeyReader_TestsTheStaticInputFirst_AndAsksTheViewsContextOnlyOnARealPress()
    {
        var keys = Flat(Source("Client/Features/SeatOfSeeing/SeatKeys.cs"));

        StringAssert.Contains(keys, "key != InputKey.Invalid && TaleWorlds.InputSystem.Input.IsKeyPressed(key) && input.IsKeyPressed(key)");
        // Key 532 is tested only while the state is not Off; keys 531 and 536 only while Seated (build-spec 6.0).
        StringAssert.Contains(keys, "if (state == SeatState.Off) return; if (Pressed(_keys.TakeThisSoldier, input)) _session.PressTake(); if (state != SeatState.Seated) return; if (Pressed(_keys.HideTheInterface, input))");
        StringAssert.Contains(keys, "if (Pressed(_keys.KeepTo, input))");
        StringAssert.Contains(keys, "_session.PressHide();");
        StringAssert.Contains(keys, "_session.PressKeep();");
        Assert.IsFalse(Regex.IsMatch(keys, @"\bnew\b"), "a key test a frame in every mission allocates nothing");
    }

    // ---- The statics the patches read ----

    [TestMethod]
    public void SeatPresence_LeavingClearsTheCursorAndTheFocusMark_NotJustTheSeatedFlag()
    {
        TAOM.Features.SeatOfSeeing.SeatPresence.Set(true);
        TAOM.Features.SeatOfSeeing.SeatPresence.SetCursor(true);
        TAOM.Features.SeatOfSeeing.SeatPresence.MarkFocusSet();

        TAOM.Features.SeatOfSeeing.SeatPresence.Set(false);

        Assert.IsFalse(TAOM.Features.SeatOfSeeing.SeatPresence.IsSeated);
        Assert.IsFalse(TAOM.Features.SeatOfSeeing.SeatPresence.CursorShown, "a patch must be a no-op in the next mission");
        Assert.IsFalse(TAOM.Features.SeatOfSeeing.SeatPresence.TakeFocusSet());
    }

    [TestMethod]
    public void SeatPresence_TheFocusMarkIsTakenOnce_AndAHiddenCursorClearsIt()
    {
        TAOM.Features.SeatOfSeeing.SeatPresence.SetCursor(true);
        TAOM.Features.SeatOfSeeing.SeatPresence.MarkFocusSet();

        Assert.IsTrue(TAOM.Features.SeatOfSeeing.SeatPresence.TakeFocusSet());
        Assert.IsFalse(TAOM.Features.SeatOfSeeing.SeatPresence.TakeFocusSet(), "once for each mark");

        TAOM.Features.SeatOfSeeing.SeatPresence.MarkFocusSet();
        TAOM.Features.SeatOfSeeing.SeatPresence.SetCursor(false);

        Assert.IsFalse(TAOM.Features.SeatOfSeeing.SeatPresence.TakeFocusSet(), "hiding the cursor drops a mark nobody took");
        TAOM.Features.SeatOfSeeing.SeatPresence.Set(false);
    }

    [TestMethod]
    public void TheFocusPatch_MarksTheFocusAfterItWritesTheHandlersPoint()
    {
        var patch = Flat(Source("Client/Features/SeatOfSeeing/Hooks/Patch140_FormationFocusPoint.cs"));

        var write = patch.IndexOf("____centerOfScreen = new Vec2(x, y);", System.StringComparison.Ordinal);
        var mark = patch.IndexOf("SeatPresence.MarkFocusSet();", System.StringComparison.Ordinal);
        Assert.IsTrue(write >= 0 && mark > write, "the check line reads NOT REACHED if the mark is lost or comes before the write");
        Assert.AreEqual(1, Regex.Matches(patch, @"MarkFocusSet\(\)").Count);
    }

    // ---- The adapter ----

    [TestMethod]
    public void TheCursorLayer_AsksForAVisibleCursor_AndTakesNoInput()
    {
        var adapter = Flat(Source("Client/Adapters/SeatScreenAdapter.cs"));

        StringAssert.Contains(adapter, "layer.InputRestrictions.SetMouseVisibility(true);");
        Assert.IsFalse(adapter.Contains("SetInputRestrictions"), "the mask stays Invalid, so the layer takes no click or key");
        StringAssert.Contains(adapter, "MBWindowManager.DontChangeCursorPos()");
    }

    [TestMethod]
    public void TheCursorLayer_IsBuiltOnceAndToggled_NeverRemoved()
    {
        var adapter = Flat(Source("Client/Adapters/SeatScreenAdapter.cs"));

        // Vanilla keeps one layer and toggles it (GauntletBannerBuilderScreen, BodyGeneratorView). RemoveLayer finalizes a layer, and
        // AddLayer refuses a finalized one (ScreenBase.cs:299-301, 327), so any removal would force a rebuild on the next show.
        Assert.AreEqual(1, Regex.Matches(adapter, @"new GauntletLayer\(").Count, "one layer, built in one place");
        Assert.IsFalse(adapter.Contains("RemoveLayer"), "the screen finalizes its layers at teardown (ScreenBase.cs:60-63)");
        Assert.IsFalse(adapter.Contains("ResetInputRestrictions"));
        StringAssert.Contains(adapter, "if (_cursorLayer == null) { if (!show) return;");
        StringAssert.Contains(adapter, "_cursorLayer.InputRestrictions.SetMouseVisibility(show);");
        Assert.IsFalse(Regex.IsMatch(adapter, @"_cursorLayer = null"), "the reference is never dropped, so the layer is never rebuilt");
    }

    [TestMethod]
    public void TheCloseSignals_IncludeGameKey77AsAFifthRoute_OnTheViewOrdersBit()
    {
        var adapter = Flat(Source("Client/Adapters/SeatScreenAdapter.cs"));

        // GauntletOrderUIHandler.cs:449-452, 484-492: key 77 closes the panel at the root of a layout that has a Return order.
        StringAssert.Contains(adapter, "input.IsGameKeyPressed((int)GameKeyDefinition.ViewOrders) || input.IsGameKeyPressed((int)GameKeyDefinition.SelectOrderReturn) || byTrial");
        Assert.AreEqual(77, (int)TaleWorlds.MountAndBlade.GameKeyDefinition.SelectOrderReturn, "the engine's game key 77");
    }

    [TestMethod]
    public void TheTransferFlag_IsTheMissionsOwn()
    {
        var adapter = Flat(Source("Client/Adapters/SeatScreenAdapter.cs"));

        StringAssert.Contains(adapter, "public bool TransferMenuOpen() => _mission.IsTransferMenuOpen;");
    }

    [TestMethod]
    public void TheLook_IsOneReadOfTheMiddleButton_ForTheCursorAndForTheInput()
    {
        var adapter = Flat(Source("Client/Adapters/SeatScreenAdapter.cs"));

        Assert.AreEqual(1, Regex.Matches(adapter, @"InputKey\.MiddleMouseButton").Count);
        StringAssert.Contains(adapter, "var looksWithMouse = !_screen.MouseVisible || LookHeld();");
    }

    [TestMethod]
    public void NoOtherTaomCode_WritesTheEnginesHideTheInterfaceFlag()
    {
        // MBDebug.DisableAllUI backs toggle_ui. The Seat restores only its own hide, which is sound only while nothing else writes it.
        var hits = RepoPaths.CSharpFiles("Main", "Client")
            .Where(f => Regex.IsMatch(RepoPaths.StripComments(File.ReadAllText(f)), @"\bDisableAllUI\b"))
            .Select(Path.GetFileName)
            .ToList();

        CollectionAssert.AreEqual(new[] { "SeatScreenAdapter.cs" }, hits, "searched Main and Client; found " + string.Join(",", hits));
        var adapter = Flat(Source("Client/Adapters/SeatScreenAdapter.cs"));
        Assert.AreEqual(1, Regex.Matches(adapter, @"MBDebug\.DisableAllUI = ").Count, "one write");
    }

    [TestMethod]
    public void TheKeepTarget_IsHeldByTheAdapter_AndTestedOccupantFirst()
    {
        var adapter = Flat(Source("Client/Adapters/SeatScreenAdapter.cs"));

        StringAssert.Contains(adapter, "!AgentSlotIdentity.IsCurrentOccupant(_keptAgent) || !_keptAgent.IsActive()");
        StringAssert.Contains(adapter, "_keptFormation.CountOfUnits <= 0");
        StringAssert.Contains(adapter, "SeatKeepRule.KeepsCompany(_mission.IsOrderMenuOpen, count, first?.CountOfUnits ?? 0)");
        Assert.IsFalse(Regex.IsMatch(adapter, @"\.Index\b"), "nothing is keyed on an agent index");
        StringAssert.Contains(adapter, "SeatOrderRay.RayFlags");
        StringAssert.Contains(Flat(Source("Client/Features/SeatOfSeeing/Hooks/SeatOrderRay.cs")), "RayFlags = BodyFlags.CommonFocusRayCastExcludeFlags | BodyFlags.BodyOwnerFlora;");
        StringAssert.Contains(adapter, "SeatKeepRule.AgentCounts(terrainHit, terrainDistance, agentDistance)");
    }

    [TestMethod]
    public void TheEntryCheck_ReadsTheRadialMenuFact_FromTheScreen()
    {
        StringAssert.Contains(Flat(Source("Client/Adapters/SeatScreenAdapter.cs")), "_screen.IsRadialMenuActive");
        StringAssert.Contains(Session(), "facts.MissionEnded || facts.RadialMenuActive) return;");
    }
}
