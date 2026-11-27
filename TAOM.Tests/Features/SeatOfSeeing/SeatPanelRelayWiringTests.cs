using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Infrastructure;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.SeatOfSeeing;

/// <summary>
/// The wiring of the order panel clicks (Marshal's Word stage M7, build-spec 6.1) as source-text pins, plus the one engine string the
/// relay depends on: the name of the order layer. The layer, the relay and the adapter run only in a live mission, so the pins say which
/// object asks which question; the rules they call have behaviour tests.
/// </summary>
[TestClass]
public class SeatPanelRelayWiringTests
{
    private static string Source(string path) => RepoPaths.ReadSource(path, stripComments: true);

    private static string Flat(string text) => Regex.Replace(text, @"\s+", " ");

    [TestMethod]
    public void TheCursorLayer_AnswersBothHitTests_FromTheShieldRule_AndNothingElse()
    {
        var layer = Flat(Source("Client/Adapters/SeatCursorLayer.cs"));

        StringAssert.Contains(layer, "internal sealed class SeatCursorLayer : GauntletLayer");
        StringAssert.Contains(layer, "public override bool HitTest() => _holds();");
        StringAssert.Contains(layer, "public override bool HitTest(Vector2 position) => _holdsAt(position.X, position.Y);");
        Assert.AreEqual(2, Regex.Matches(layer, @"override bool HitTest").Count, "no other override: the layer has no movie and takes no key");
    }

    [TestMethod]
    public void TheAdapter_BuildsTheLayerWithTheRelaysShield_AndTheRelayReadsTheEdgesFromThatLayer()
    {
        var adapter = Flat(Source("Client/Adapters/SeatScreenAdapter.cs"));
        var relay = Flat(Source("Client/Adapters/SeatPanelRelay.cs"));

        StringAssert.Contains(adapter, "new SeatCursorLayer(\"SeatCursor\", CursorLayerOrder, relay.ShieldHolds, relay.ShieldHoldsAt)");
        StringAssert.Contains(adapter, "relay.Attach(layer);");
        StringAssert.Contains(adapter, "public SeatRelayResult RelayPanelClicks() => Relay().Tick();");
        StringAssert.Contains(relay, "var input = _shield?.Input;");
        StringAssert.Contains(relay, "input.IsKeyPressed(InputKey.LeftMouseButton)");
        StringAssert.Contains(relay, "input.IsKeyReleased(InputKey.LeftMouseButton)");
        StringAssert.Contains(relay, "input.IsKeyReleased(InputKey.RightMouseButton)");
    }

    [TestMethod]
    public void TheCloseSignals_ReadTheRightButtonRaw_SoAReleaseTheShieldTookStillCountsAsThePlayersClose()
    {
        var adapter = Flat(Source("Client/Adapters/SeatScreenAdapter.cs"));

        StringAssert.Contains(adapter, "Input.IsKeyReleased(InputKey.RightMouseButton)");
        Assert.IsFalse(adapter.Contains("input.IsKeyReleased(InputKey.RightMouseButton)"), "the scene layer reads a mouse button only on a frame it holds the mouse buttons");
    }

    [TestMethod]
    public void TheShield_YieldsToOtherLayers_ByTheRule_AndNeverCountsTheSceneTheOrderLayerOrItself()
    {
        var relay = Flat(Source("Client/Adapters/SeatPanelRelay.cs"));

        StringAssert.Contains(relay, "SeatPanelYieldRule.LayerMayClaim(");
        StringAssert.Contains(relay, "layer == _screen.SceneLayer, layer == _orderLayer, layer == _shield)");
        StringAssert.Contains(relay, "SeatPanelYieldRule.Holds(");
        StringAssert.Contains(relay, "panel.TroopController.IsTransferActive");
    }

    [TestMethod]
    public void TheRelay_CallsTheVanillaMethodsTheNumberKeysCall_AndNoOther()
    {
        var relay = Flat(Source("Client/Adapters/SeatPanelRelay.cs"));

        foreach (var call in new[] { "card.ExecuteAction();", "panel.TrySelectOrderSet(set)", "item.ExecuteAction(parameters.Value);",
                     "panel.TryCloseToggleOrder();", "panel.SelectedOrderSet?.ExecuteDeSelect();", "panel.OnEscape();" })
            StringAssert.Contains(relay, call);
        Assert.IsFalse(Regex.IsMatch(relay, @"\.(SetOrder\w*|SelectFormation|ClearSelectedFormations)\("), "the relay writes no formation by itself");
    }

    [TestMethod]
    public void TheRelay_RunsAnOrderWhateverItsLook_AndWithTheHandlersOwnParameters()
    {
        var relay = Flat(Source("Client/Adapters/SeatPanelRelay.cs"));
        var adapter = Flat(Source("Client/Adapters/SeatScreenAdapter.cs"));

        StringAssert.Contains(relay, "case SeatPanelAction.ExecuteOrderItem when target is OrderItemVM item:");
        Assert.IsFalse(Regex.IsMatch(relay, @"IsActive: true \} item"), "OrderItemVM.IsActive is the state already in force, not a greyed button (OrderItemVM.cs:30)");
        Assert.IsFalse(relay.Contains("new VisualOrderExecutionParameters("), "the key path's own parameters, with its focused formation, never a null formation");
        StringAssert.Contains(adapter, "AccessTools.Method(typeof(GauntletOrderUIHandler), \"GetVisualOrderExecutionParameters\")");
        StringAssert.Contains(adapter, "new SeatPanelRelay(_screen, _mission, OrderPanel, OrderParameters)");
    }

    [TestMethod]
    public void TheShield_TakesTheWheelWithTheMouseButtons_AndTheZoomReadsTheRawWheelThroughTheRule()
    {
        var adapter = Flat(Source("Client/Adapters/SeatScreenAdapter.cs"));

        Assert.AreEqual(2, Regex.Matches(adapter, @"InputUsageMask\.Mouse\b").Count, "created with Mouse, toggled between Mouse and Invalid");
        Assert.IsFalse(adapter.Contains("InputUsageMask.MouseButtons"), "a mask without the wheel leaves the scene layer hit, and vanilla's release branch fires on a shielded click");
        StringAssert.Contains(adapter, "SeatWheelRule.Read(sceneInput.GetDeltaMouseScroll(), Input.DeltaMouseScroll,");
        StringAssert.Contains(adapter, "ReadWheel(input) / ScrollNotch");
    }

    [TestMethod]
    public void TheTrialHost_UnsubscribesTheStaticOrderEvent_WhenTheViewEnds()
    {
        var host = Flat(Source("Client/Features/SeatOfSeeing/Trial/CommandTrialHost.Seat.cs"));
        var view = Flat(Source("Client/Features/SeatOfSeeing/SeatView.cs"));

        Assert.AreEqual(1, Regex.Matches(host, @"OrderItemVM\.OnExecuteOrder \+= CountOrder;").Count);
        Assert.AreEqual(1, Regex.Matches(host, @"OrderItemVM\.OnExecuteOrder -= CountOrder;").Count);
        StringAssert.Contains(view, "_trialHost?.StopWatchingOrders();");
    }

    [TestMethod]
    public void TheSession_RelaysOnceAScreenTick_OnlyWhileThePanelIsOpenAndTheCursorShows()
    {
        var session = Flat(Source("Client/Features/SeatOfSeeing/SeatSession.cs"));

        Assert.AreEqual(1, Regex.Matches(session, @"_screen\.RelayPanelClicks\(\)").Count);
        StringAssert.Contains(session, "if (_cursorShown && open) NoteRelay(_screen.RelayPanelClicks());");
    }

    // ---- The one engine string: the order layer's name ----

    [TestMethod]
    public void TheOrderLayerName_IsTheOneTheRelaySearchesFor()
    {
        var relay = Source("Client/Adapters/SeatPanelRelay.cs");

        var match = Regex.Match(relay, "const string OrderLayerName = \"([^\"]+)\";");
        Assert.IsTrue(match.Success, "the literal is one named constant");
        Assert.AreEqual("MissionOrder", match.Groups[1].Value);
        Assert.AreEqual(1, Regex.Matches(relay, @"gauntlet\.Name == OrderLayerName").Count);
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void TheInstalledSingleplayerOrderHandler_BuildsALayerNamedMissionOrder()
    {
        if (!GameAssemblies.EnsureLoaded())
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));

        var type = AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType("TaleWorlds.MountAndBlade.GauntletUI.Mission.Singleplayer.MissionGauntletSingleplayerOrderUIHandler", throwOnError: false))
            .FirstOrDefault(t => t != null);
        Assert.IsNotNull(type, "the handler moved or was renamed: the relay would never find the order layer");

        var literals = LiteralsOf(type!);
        CollectionAssert.Contains(literals, "MissionOrder", "the handler no longer names its layer MissionOrder (v1.5.7 MissionGauntletSingleplayerOrderUIHandler.cs:127)");
    }

    private static string[] LiteralsOf(Type type)
    {
        const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        var found = new System.Collections.Generic.List<string>();
        foreach (var method in type.GetMethods(All).Cast<MethodBase>().Concat(type.GetConstructors(All)))
        {
            var il = method.GetMethodBody()?.GetILAsByteArray();
            if (il == null)
                continue;
            for (var i = 0; i + 4 < il.Length; i++)
            {
                if (il[i] != OpCodes.Ldstr.Value)
                    continue;
                try
                {
                    found.Add(type.Module.ResolveString(BitConverter.ToInt32(il, i + 1)));
                }
                catch (ArgumentException)
                {
                    // Not a string token: the byte was part of another instruction.
                }
            }
        }

        return found.ToArray();
    }
}
