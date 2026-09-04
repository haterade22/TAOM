using System;
using System.Collections.Generic;
using System.Linq;

namespace TAOM.Features.DevConsole;

/// <summary>One layer's input-relevant state, captured from the engine at a single instant.</summary>
internal sealed class LayerInputState
{
    internal string Name { get; set; } = "";
    internal string TypeName { get; set; } = "";

    /// <summary>From <c>InputRestrictions.Order</c>: the layer's input priority, not its z-order.</summary>
    internal int Order { get; set; }

    internal bool IsActive { get; set; }

    /// <summary>
    /// <c>ScreenLayer.IsFocusLayer</c>, which means "may hold focus", NOT "holds focus now".
    /// The layer actually holding it is <see cref="InputStateSnapshot.FocusedLayerName"/>.
    /// </summary>
    internal bool IsFocusCandidate { get; set; }

    /// <summary>Cursor-dependent, recomputed every frame. See the class remarks on volatility.</summary>
    internal bool IsHitThisFrame { get; set; }

    /// <summary>Cursor-dependent, recomputed every frame.</summary>
    internal bool KeysAllowed { get; set; }

    /// <summary>Cursor-dependent, recomputed every frame.</summary>
    internal bool MouseButtonAllowed { get; set; }

    /// <summary>Persistent: set by <c>SetInputRestrictions</c>, not recomputed per frame.</summary>
    internal string InputUsageMask { get; set; } = "";
}

/// <summary>The whole input picture at one instant: what is on top, what holds focus, every layer.</summary>
internal sealed class InputStateSnapshot
{
    internal string TopScreenTypeName { get; set; } = "";

    /// <summary>Name of the layer holding <c>ScreenManager.FocusedLayer</c>; null when nothing holds it.</summary>
    internal string FocusedLayerName { get; set; }

    internal List<LayerInputState> Layers { get; set; } = new List<LayerInputState>();
}

/// <summary>
/// Reads an <see cref="InputStateSnapshot"/> and says which of the two hover paths is broken.
///
/// The paths are scored separately because the engine gates them through unrelated mechanisms.
/// Alt-hover nameplates go through <c>InputContext.IsKeysAllowed</c>, which the engine grants only
/// to the single layer equal to <c>ScreenManager.FocusedLayer</c>. The campaign-map resource bar's
/// hover hints go through mouse hit-testing and never consult focus at all. One can be dead while
/// the other is fine.
///
/// VOLATILITY, and it is the whole reason this class is careful: <c>IsKeysAllowed</c>,
/// <c>IsMouseButtonAllowed</c> and <c>IsHitThisFrame</c> are recomputed every frame from where the
/// CURSOR is. <c>ScreenManager.EarlyUpdate</c> grants mouse input only to a layer whose
/// <c>HitTest()</c> passes, and only to the first such layer. So a perfectly healthy map bar reports
/// mouse-not-allowed on any dump taken with the cursor elsewhere, which is every dump taken with the
/// console open. Verdicts are therefore built on the PERSISTENT facts (is the layer active, what
/// does its input mask permit, who holds focus) and the volatile flags are reported as observations
/// only. An earlier revision scored the bar from the volatile flag and would have declared a healthy
/// bar BROKEN.
///
/// Pure and engine-free on purpose, so the reading is unit-tested directly.
/// </summary>
internal static class InputStateDiagnosis
{
    private const string MapBarMarker = "mapbar";
    private const string MapScreenMarker = "MapScreen";
    private const string SceneLayerMarker = "SceneLayer";

    internal static IReadOnlyList<string> Build(InputStateSnapshot snapshot)
    {
        var layers = snapshot?.Layers ?? new List<LayerInputState>();
        var onMap = !string.IsNullOrEmpty(snapshot?.TopScreenTypeName)
                    && Contains(snapshot.TopScreenTypeName, MapScreenMarker);

        var lines = new List<string>
        {
            BuildAltVerdict(snapshot?.FocusedLayerName, layers, onMap),
            BuildBarVerdict(layers),
        };

        var topScreen = snapshot?.TopScreenTypeName;
        if (!string.IsNullOrEmpty(topScreen) && !onMap)
        {
            lines.Add($"NOTE: TopScreen is {topScreen}, not MapScreen. The map bar re-derives its own "
                    + "enabled state and input restrictions from that every tick, so it disables itself "
                    + "while another screen sits on top. Expected while a screen is open; a problem if "
                    + "this is what you see with only the map showing.");
        }

        lines.Add("NOTE: keys-allowed, mouse-allowed and hit-this-frame are per-frame flags derived "
                + "from the cursor position, and opening the console moves the cursor off whatever you "
                + "were hovering. Park the cursor on the widget you care about before dumping if you "
                + "want those columns to mean anything; the verdicts above deliberately do not rest on "
                + "them.");

        return lines;
    }

    private static string BuildAltVerdict(string focusedLayerName, IReadOnlyList<LayerInputState> layers, bool onMap)
    {
        if (string.IsNullOrEmpty(focusedLayerName))
        {
            return "ALT PATH BROKEN: no layer holds focus (ScreenManager.FocusedLayer is null), so "
                 + "IsKeysAllowed is false everywhere and Alt-hover nameplates cannot fire.";
        }

        var matches = layers.Where(l => l.Name == focusedLayerName).ToList();
        if (matches.Count == 0)
        {
            return $"ALT PATH BROKEN: focus is held by '{focusedLayerName}', which is not in the live "
                 + "layer list at all. That is a stale pointer to a torn-down layer, and no live layer "
                 + "can win the focus test while it stands.";
        }

        // Layer names are not unique in v1.4.8 (several vanilla layers are literally "GauntletLayer"),
        // and the engine compares by REFERENCE. Matching by name is the best a flattened snapshot can
        // do, so say when the match was ambiguous rather than quietly picking the first.
        var ambiguity = matches.Count > 1
            ? $" (WARNING: {matches.Count} layers share the name '{focusedLayerName}'; the engine matches"
              + " by reference, so this reading cannot tell which one actually holds focus)"
            : "";

        var focused = matches[0];

        if (!focused.IsActive)
        {
            return $"ALT PATH BROKEN: focus is held by '{focusedLayerName}' (order {focused.Order}), which "
                 + $"is not active. A dead layer holding focus locks every other layer out of keyboard input.{ambiguity}";
        }

        // The map's Alt read is issued against the SceneLayer specifically
        // (GauntletMapPartyNameplateView.OnMapScreenUpdate), and the engine answers a key query only
        // for the focused layer. Focus sitting on any other layer is the failure this command exists
        // to catch, and scoring it OK merely because SOMETHING holds focus was the earlier bug here.
        if (onMap && !Contains(focused.TypeName, SceneLayerMarker) && !Contains(focused.Name, SceneLayerMarker))
        {
            return $"ALT PATH BROKEN: on the campaign map, focus is held by '{focusedLayerName}' "
                 + $"({focused.TypeName}, order {focused.Order}) rather than the map's SceneLayer. The Alt "
                 + "game key is read against the SceneLayer, and the engine answers a key query only for "
                 + $"the layer holding focus, so Alt-hover nameplates cannot fire.{ambiguity}";
        }

        return $"ALT PATH GATE OK: focus is held by active layer '{focusedLayerName}' "
             + $"({focused.TypeName}, order {focused.Order})"
             + (onMap ? ", which is the map's SceneLayer as expected" : "")
             + $". If Alt still shows nothing, the fault is past the input gate.{ambiguity}";
    }

    private static string BuildBarVerdict(IReadOnlyList<LayerInputState> layers)
    {
        var bar = layers.FirstOrDefault(l =>
            Contains(l.Name, MapBarMarker) || Contains(l.TypeName, MapBarMarker));

        if (bar == null)
        {
            return "BAR PATH: no map bar layer present (nothing named or typed *MapBar* in the layer "
                 + "list). If the bar is visibly on screen while this says otherwise, it is drawing "
                 + "from a layer this dump did not match, and the table below is the thing to read.";
        }

        if (!bar.IsActive)
        {
            return $"BAR PATH BROKEN: map bar layer '{bar.Name}' is inactive, so its hover hints cannot fire.";
        }

        // The MASK is persistent (SetInputRestrictions writes it and nothing recomputes it per frame),
        // unlike the IsMouseButtonAllowed flag, so it is the only mouse fact worth a verdict.
        if (!MaskPermitsMouse(bar.InputUsageMask))
        {
            return $"BAR PATH BROKEN: map bar layer '{bar.Name}' has input mask '{bar.InputUsageMask}', "
                 + "which does not permit mouse input, so HoverBegin never reaches the hint widgets.";
        }

        return $"BAR PATH GATE OK: map bar layer '{bar.Name}' is active and its mask "
             + $"('{bar.InputUsageMask}') permits mouse input. Observed this frame, cursor-dependent: "
             + $"hit={bar.IsHitThisFrame}, mouseAllowed={bar.MouseButtonAllowed}. If tooltips are dead "
             + "with this line showing, the fault is inside the widget tree or the hint data, not the "
             + "input gate.";
    }

    /// <summary>
    /// The mask is a flags enum rendered as text: "All", "MouseButtons, Keyboardkeys", "Invalid".
    /// "Invalid" is the reset state and permits nothing.
    /// </summary>
    private static bool MaskPermitsMouse(string mask) =>
        Contains(mask, "All") || Contains(mask, "MouseButton");

    private static bool Contains(string value, string marker) =>
        !string.IsNullOrEmpty(value) && value.IndexOf(marker, StringComparison.OrdinalIgnoreCase) >= 0;
}
