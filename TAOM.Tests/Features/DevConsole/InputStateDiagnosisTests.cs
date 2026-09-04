using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.DevConsole;

namespace TAOM.Tests.Features.DevConsole;

/// <summary>
/// Pins the interpretation half of <c>taom.print_input_state</c>. The command reads engine statics
/// which need a live game, but the reading of that state is where the diagnostic value is.
///
/// The verdicts rest ONLY on persistent facts (is the layer active, what the input mask permits, who
/// holds focus). The per-frame flags are cursor-derived and a dump taken with the console open has
/// the cursor off whatever you were hovering, so scoring a verdict from them declares healthy
/// widgets broken. An earlier revision did exactly that; <see cref="Build_MapBarNotHitThisFrameButMaskPermitsMouse_IsNotReportedBroken"/>
/// is the regression guard.
/// </summary>
[TestClass]
public class InputStateDiagnosisTests
{
    private static LayerInputState Layer(
        string name,
        bool isActive = true,
        bool isFocusCandidate = false,
        bool keysAllowed = false,
        bool isHitThisFrame = false,
        bool mouseButtonAllowed = true,
        string mask = "All",
        string typeName = "GauntletLayer",
        int order = 0) =>
        new LayerInputState
        {
            Name = name,
            TypeName = typeName,
            Order = order,
            IsActive = isActive,
            IsFocusCandidate = isFocusCandidate,
            IsHitThisFrame = isHitThisFrame,
            KeysAllowed = keysAllowed,
            MouseButtonAllowed = mouseButtonAllowed,
            InputUsageMask = mask,
        };

    private static LayerInputState SceneLayer() =>
        Layer("SceneLayer", isFocusCandidate: true, keysAllowed: true, typeName: "SceneLayer", order: -100);

    private static LayerInputState MapBar(string mask = "All", bool isActive = true, bool hit = false) =>
        Layer("MapBar", isActive: isActive, isHitThisFrame: hit, mask: mask, order: 202);

    private static string Joined(IReadOnlyList<string> lines) => string.Join("\n", lines);

    // ---------- ALT path ----------

    [TestMethod]
    public void Build_NoLayerHoldsFocus_ReportsAltPathBroken()
    {
        var snapshot = new InputStateSnapshot
        {
            TopScreenTypeName = "MapScreen",
            FocusedLayerName = null,
            Layers = new List<LayerInputState> { SceneLayer() },
        };

        var lines = InputStateDiagnosis.Build(snapshot);

        StringAssert.Contains(Joined(lines), "ALT PATH BROKEN");
        StringAssert.Contains(Joined(lines), "no layer holds focus");
    }

    [TestMethod]
    public void Build_FocusPointsAtALayerNotInTheList_ReportsAltPathBrokenAsStale()
    {
        var snapshot = new InputStateSnapshot
        {
            TopScreenTypeName = "MapScreen",
            FocusedLayerName = "CareerScreen",
            Layers = new List<LayerInputState> { SceneLayer() },
        };

        var lines = InputStateDiagnosis.Build(snapshot);

        StringAssert.Contains(Joined(lines), "ALT PATH BROKEN");
        StringAssert.Contains(Joined(lines), "stale pointer");
    }

    [TestMethod]
    public void Build_FocusHeldByInactiveLayer_ReportsAltPathBrokenAndNamesTheLayer()
    {
        var snapshot = new InputStateSnapshot
        {
            TopScreenTypeName = "MapScreen",
            FocusedLayerName = "CareerScreen",
            Layers = new List<LayerInputState>
            {
                Layer("CareerScreen", isActive: false, isFocusCandidate: true, order: 1),
                SceneLayer(),
            },
        };

        var lines = InputStateDiagnosis.Build(snapshot);

        StringAssert.Contains(Joined(lines), "ALT PATH BROKEN");
        StringAssert.Contains(Joined(lines), "CareerScreen");
        StringAssert.Contains(Joined(lines), "not active");
    }

    /// <summary>
    /// The failure the command exists to catch. An earlier revision scored this OK merely because
    /// SOMETHING active held focus, which is the state a leaked full-screen overlay produces.
    /// </summary>
    [TestMethod]
    public void Build_OnTheMapWithFocusOnANonSceneLayer_ReportsAltPathBroken()
    {
        var snapshot = new InputStateSnapshot
        {
            TopScreenTypeName = "MapScreen",
            FocusedLayerName = "SupplyOrderLayer",
            Layers = new List<LayerInputState>
            {
                SceneLayer(),
                Layer("SupplyOrderLayer", isActive: true, isFocusCandidate: true, keysAllowed: true, order: 15),
            },
        };

        var lines = InputStateDiagnosis.Build(snapshot);

        StringAssert.Contains(Joined(lines), "ALT PATH BROKEN");
        StringAssert.Contains(Joined(lines), "SceneLayer");
    }

    [TestMethod]
    public void Build_OnTheMapWithFocusOnTheSceneLayer_ReportsAltPathGateOk()
    {
        var snapshot = new InputStateSnapshot
        {
            TopScreenTypeName = "MapScreen",
            FocusedLayerName = "SceneLayer",
            Layers = new List<LayerInputState> { SceneLayer() },
        };

        var lines = InputStateDiagnosis.Build(snapshot);

        StringAssert.Contains(Joined(lines), "ALT PATH GATE OK");
    }

    [TestMethod]
    public void Build_OffTheMapWithFocusOnAScreenLayer_DoesNotDemandTheSceneLayer()
    {
        // Inside a full screen the map's SceneLayer SHOULD NOT hold focus; demanding it there would
        // report every open screen as broken.
        var snapshot = new InputStateSnapshot
        {
            TopScreenTypeName = "GauntletCareerScreen",
            FocusedLayerName = "CareerScreen",
            Layers = new List<LayerInputState>
            {
                Layer("CareerScreen", isActive: true, isFocusCandidate: true, keysAllowed: true, order: 1),
            },
        };

        var lines = InputStateDiagnosis.Build(snapshot);

        StringAssert.Contains(Joined(lines), "ALT PATH GATE OK");
    }

    [TestMethod]
    public void Build_SeveralLayersShareTheFocusedName_WarnsThatTheMatchIsAmbiguous()
    {
        // Layer names are not unique in v1.4.8 and the engine matches by reference, so a
        // name-matched reading has to admit when it cannot tell which layer it looked at.
        var snapshot = new InputStateSnapshot
        {
            TopScreenTypeName = "MapScreen",
            FocusedLayerName = "GauntletLayer",
            Layers = new List<LayerInputState>
            {
                Layer("GauntletLayer", isFocusCandidate: true, keysAllowed: true, typeName: "SceneLayer"),
                Layer("GauntletLayer", isFocusCandidate: true, order: 90),
            },
        };

        var lines = InputStateDiagnosis.Build(snapshot);

        StringAssert.Contains(Joined(lines), "share the name");
    }

    // ---------- BAR path ----------

    [TestMethod]
    public void Build_NoMapBarLayerPresent_ReportsBarLayerAbsent()
    {
        var snapshot = new InputStateSnapshot
        {
            TopScreenTypeName = "MapScreen",
            FocusedLayerName = "SceneLayer",
            Layers = new List<LayerInputState> { SceneLayer() },
        };

        var lines = InputStateDiagnosis.Build(snapshot);

        StringAssert.Contains(Joined(lines), "BAR PATH");
        StringAssert.Contains(Joined(lines), "no map bar layer");
    }

    [TestMethod]
    public void Build_MapBarLayerInactive_ReportsBarPathBroken()
    {
        var snapshot = new InputStateSnapshot
        {
            TopScreenTypeName = "MapScreen",
            FocusedLayerName = "SceneLayer",
            Layers = new List<LayerInputState> { SceneLayer(), MapBar(isActive: false) },
        };

        var lines = InputStateDiagnosis.Build(snapshot);

        StringAssert.Contains(Joined(lines), "BAR PATH BROKEN");
        StringAssert.Contains(Joined(lines), "inactive");
    }

    [TestMethod]
    public void Build_MapBarMaskForbidsMouse_ReportsBarPathBroken()
    {
        // The mask is persistent, written by SetInputRestrictions, so it IS verdict-worthy.
        // "Invalid" is the reset state and permits nothing.
        var snapshot = new InputStateSnapshot
        {
            TopScreenTypeName = "MapScreen",
            FocusedLayerName = "SceneLayer",
            Layers = new List<LayerInputState> { SceneLayer(), MapBar(mask: "Invalid") },
        };

        var lines = InputStateDiagnosis.Build(snapshot);

        StringAssert.Contains(Joined(lines), "BAR PATH BROKEN");
        StringAssert.Contains(Joined(lines), "Invalid");
    }

    /// <summary>
    /// Regression guard for the false alarm. IsMouseButtonAllowed and IsHitThisFrame are granted per
    /// frame only to the layer the cursor is over, and opening the console moves the cursor away, so
    /// a healthy bar reports both false on essentially every dump.
    /// </summary>
    [TestMethod]
    public void Build_MapBarNotHitThisFrameButMaskPermitsMouse_IsNotReportedBroken()
    {
        var snapshot = new InputStateSnapshot
        {
            TopScreenTypeName = "MapScreen",
            FocusedLayerName = "SceneLayer",
            Layers = new List<LayerInputState>
            {
                SceneLayer(),
                Layer("MapBar", isHitThisFrame: false, mouseButtonAllowed: false, mask: "All", order: 202),
            },
        };

        var lines = InputStateDiagnosis.Build(snapshot);

        Assert.IsFalse(Joined(lines).Contains("BAR PATH BROKEN"),
            "A cursor-dependent flag must never drive the BAR verdict:\n" + Joined(lines));
        StringAssert.Contains(Joined(lines), "BAR PATH GATE OK");
    }

    [TestMethod]
    public void Build_MapBarHealthy_ReportsGateOkAndSaysWhereToLookNext()
    {
        var snapshot = new InputStateSnapshot
        {
            TopScreenTypeName = "MapScreen",
            FocusedLayerName = "SceneLayer",
            Layers = new List<LayerInputState> { SceneLayer(), MapBar(hit: true) },
        };

        var lines = InputStateDiagnosis.Build(snapshot);

        StringAssert.Contains(Joined(lines), "BAR PATH GATE OK");
        StringAssert.Contains(Joined(lines), "widget tree");
    }

    [TestMethod]
    public void Build_MapBarIdentifiedByLayerNameAlone_StillFound()
    {
        var snapshot = new InputStateSnapshot
        {
            TopScreenTypeName = "MapScreen",
            FocusedLayerName = "SceneLayer",
            Layers = new List<LayerInputState> { SceneLayer(), Layer("MapBarLayer") },
        };

        var lines = InputStateDiagnosis.Build(snapshot);

        Assert.IsFalse(Joined(lines).Contains("no map bar layer"),
            "A layer whose Name carries the map-bar marker must be matched even when its type is generic.");
    }

    // ---------- Framing ----------

    [TestMethod]
    public void Build_TopScreenIsNotMapScreen_NotesTheMapBarSelfDisable()
    {
        var snapshot = new InputStateSnapshot
        {
            TopScreenTypeName = "GauntletCareerScreen",
            FocusedLayerName = "CareerScreen",
            Layers = new List<LayerInputState> { Layer("CareerScreen", isFocusCandidate: true, keysAllowed: true) },
        };

        var lines = InputStateDiagnosis.Build(snapshot);

        StringAssert.Contains(Joined(lines), "TopScreen is GauntletCareerScreen");
        StringAssert.Contains(Joined(lines), "not MapScreen");
    }

    [TestMethod]
    public void Build_TopScreenIsMapScreen_DoesNotNoteTheMapBarSelfDisable()
    {
        var snapshot = new InputStateSnapshot
        {
            TopScreenTypeName = "MapScreen",
            FocusedLayerName = "SceneLayer",
            Layers = new List<LayerInputState> { SceneLayer() },
        };

        var lines = InputStateDiagnosis.Build(snapshot);

        Assert.IsFalse(Joined(lines).Contains("not MapScreen"),
            "The map-bar self-disable note must fire only when the map is not the top screen.");
    }

    [TestMethod]
    public void Build_Always_WarnsThatThePerFrameColumnsAreCursorDependent()
    {
        var lines = InputStateDiagnosis.Build(new InputStateSnapshot
        {
            TopScreenTypeName = "MapScreen",
            FocusedLayerName = "SceneLayer",
            Layers = new List<LayerInputState> { SceneLayer(), MapBar() },
        });

        StringAssert.Contains(Joined(lines), "cursor");
    }

    [TestMethod]
    public void Build_EmptySnapshot_DoesNotThrowAndStillReportsBothPaths()
    {
        var lines = InputStateDiagnosis.Build(new InputStateSnapshot());

        Assert.IsTrue(lines.Any(l => l.Contains("ALT PATH")), "Expected an ALT PATH verdict even with no layers.");
        Assert.IsTrue(lines.Any(l => l.Contains("BAR PATH")), "Expected a BAR PATH verdict even with no layers.");
    }
}
