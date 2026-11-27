using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.SeatOfSeeing;

/// <summary>
/// Where the command mouse reads and writes, pinned in the source (the engine is not up in a test): the screen tick captures and the mission tick acts;
/// the keyboard is never read by the command path; Marshal's Word arms by the same flag the Seat sets; the shield covers the screen only while the mouse is live.
/// </summary>
[TestClass]
public class SeatCommandWiringTests
{
    private static string Source(string path) => Regex.Replace(RepoPaths.ReadSource(path, stripComments: true), @"\s+", " ");

    [TestMethod]
    public void TheSession_CapturesInTheScreenTickPath_AndActsInTheMissionTickOnly()
    {
        var session = Source("Client/Features/SeatOfSeeing/SeatSession.cs");

        StringAssert.Contains(session, "_command?.Capture(_screen.ReadPointer());");
        StringAssert.Contains(session, "if (_commandLive && State == SeatState.Seated) _command?.OnMissionTick(SeatPresence.TakeTransfer());");
        Assert.AreEqual(1, Regex.Matches(session, @"_command\?\.OnMissionTick\(").Count, "one acting call");
        Assert.AreEqual(1, Regex.Matches(session, @"ReadPointer\(\)").Count, "the pointer is read in one place");
    }

    [TestMethod]
    public void TheControl_NeverTouchesTheEngine_ExceptThroughItsAdapter()
    {
        var control = Source("Client/Features/SeatOfSeeing/SeatCommandControl.cs");

        foreach (var forbidden in new[] { "TaleWorlds", "Input.", "Mission.", "OrderController", "Agent" })
            Assert.IsFalse(control.Contains(forbidden), "the control holds no engine type: " + forbidden);
    }

    [TestMethod]
    public void TheCommandAdapter_ReadsNoKeyAndNoMouseState_ItActsOnTheFrameItIsGiven()
    {
        var adapter = Source("Client/Adapters/SeatCommandAdapter.cs");

        Assert.IsFalse(adapter.Contains("Input.Is"), "no key is read: the keyboard stays vanilla");
        Assert.IsFalse(adapter.Contains("MousePosition"), "the pointer is the captured frame's");
        StringAssert.Contains(adapter, "controller.SelectFormation(formation)");
        StringAssert.Contains(adapter, "controller.DeselectFormation(formation)");
        StringAssert.Contains(adapter, "controller.ClearSelectedFormations()");
        StringAssert.Contains(adapter, "Controller?.SetOrderWithFormation(OrderType.Charge, _enemy)");
        StringAssert.Contains(adapter, "controller.SetOrderWithOrderableObject(_orderable)");
        StringAssert.Contains(adapter, "SimulateNewOrderWithPositionAndDirection(_lineBegin, end, out var frames, !horizontal)");
    }

    [TestMethod]
    public void TheSelection_IsGuardedAgainstTheAssertsOfTheEngine_ThatFireOnADoubleSelectOrAnUnselectableOne()
    {
        var adapter = Source("Client/Adapters/SeatCommandAdapter.cs");

        // OrderController.SelectFormation and DeselectFormation raise FailedAssert on a formation already selected, not selectable or not selected.
        StringAssert.Contains(adapter, "if (!controller.SelectedFormations.Contains(formation) && controller.IsFormationSelectable(formation))");
        StringAssert.Contains(adapter, "if (controller.SelectedFormations.Contains(formation)) controller.DeselectFormation(formation);");
        StringAssert.Contains(adapter, "if (controller.IsFormationSelectable(formation)) controller.SelectFormation(formation);");
    }

    [TestMethod]
    public void TheScreenAdapter_ReadsTheEdgesFromTheShieldAndTheModifiersRaw_AndLeavesAPanelPressToTheRelay()
    {
        var adapter = Source("Client/Adapters/SeatScreenAdapter.cs");

        StringAssert.Contains(adapter, "var shield = _cursorLayer?.Input;");
        StringAssert.Contains(adapter, "var left = shield.IsKeyPressed(InputKey.LeftMouseButton);");
        StringAssert.Contains(adapter, "var right = shield.IsKeyPressed(InputKey.RightMouseButton);");
        StringAssert.Contains(adapter, "_edges.Take(left,");
        StringAssert.Contains(adapter, "relay.TransferActive()");
        StringAssert.Contains(adapter, "Input.IsKeyDown(InputKey.LeftControl) || Input.IsKeyDown(InputKey.RightControl)");
    }

    [TestMethod]
    public void TheShield_CoversTheScreenOnlyWhileTheCommandMouseIsLive_AndYieldsToOtherOverlays()
    {
        var relay = Source("Client/Adapters/SeatPanelRelay.cs");

        StringAssert.Contains(relay, "return holdsPanel || (_commandLive && CommandHolds());");
        StringAssert.Contains(relay, "SeatCommandRule.ShieldHolds(_cursorOn, _commandLive, LookHeld(), AnotherLayerClaims(), TransferActive(), ButtonTaken())");
    }

    [TestMethod]
    public void MarshalsWord_ArmsItsHeldKeysAndOutlines_ByTheSameFlagTheSeatSets()
    {
        var view = Source("Client/Features/MarshalsWord/MarshalView.cs");

        StringAssert.Contains(view, "var open = Mission.IsOrderMenuOpen || SeatPresence.CommandMouse;");
    }

    [TestMethod]
    public void TheView_GivesTheSessionTheCommandAdapterAndTheNetworkTest_NoNewLocatorCall()
    {
        var view = Source("Client/Features/SeatOfSeeing/SeatView.cs");

        StringAssert.Contains(view, "new SeatCommandAdapter(MissionScreen, mission, logger, screen.Box), () => SeatMissionClassifier.NetworkSessionNow(coop));");
    }

    [TestMethod]
    public void TheFlagPool_IsOneClass_UsedByTheSeatsPreviewAndMarshalsRoadAhead()
    {
        Assert.IsTrue(Source("Client/Adapters/SeatCommandAdapter.cs").Contains("new OrderFlagPool(mission)"));
        Assert.IsTrue(Source("Client/Adapters/MarshalScreenAdapter.cs").Contains("new OrderFlagPool(mission)"));
        Assert.IsFalse(Source("Client/Adapters/MarshalScreenAdapter.cs").Contains("GameEntity.CreateEmpty"), "the pool is not built twice");
    }
}
