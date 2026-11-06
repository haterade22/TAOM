using System.Linq;
using SandBox.View.Map;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TAOM.Adapters;
using TAOM.Features.CoopInterop;
using TAOM.Features.FieldCamp.Hooks;

namespace TAOM.Features.FieldCamp.UI;

/// <summary>
/// Persistent on-map camp overlay (MomentumIndicatorMapView is the template). Added/removed via
/// MapScreen.AddMapView by <c>FieldCampOverlayAdapter</c>, which <c>FieldCampCampaignBehavior</c> calls from
/// its per-tick presence check.
/// Parameterless ctor is required by AddMapView's new() constraint, so services resolve via IoC
/// here - the sanctioned engine-instantiated-boundary exception (GauntletCareerScreen precedent).
/// The overlay must NOT steal map input: SetInputRestrictions(false, mouse+keys) and no
/// IsFocusLayer (gui-ui.md); the prefab's root is DoNotAcceptEvents so only the button is
/// hit-testable. This view loads no sprite categories itself: the original prefab uses vanilla
/// brushes, and when the FactionUI theme swaps in TAOMFieldCampOverlay (the Camp icon beside the
/// clock), FrontEndMovieService registers and holds that icon's images for the movie's lifetime.
/// </summary>
public class FieldCampMapView : MapView
{
    /// <summary>4 Hz. The source refreshed the VM every frame, rebuilding status strings each
    /// time; nothing on this panel changes faster than the build bar, which the source itself
    /// throttled to this same interval.</summary>
    private const float RefreshIntervalSeconds = 0.25f;

    private GauntletLayer? _layerAsGauntletLayer;
    private GauntletMovieIdentifier? _movie;
    private FieldCampOverlayVM? _vm;
    private ICampVisualService? _visuals;
    private float _refreshAccumulator;

    protected override void CreateLayout()
    {
        base.CreateLayout();

        _visuals = IoC.Resolve<ICampVisualService>();
        _vm = new FieldCampOverlayVM(
            IoC.Resolve<ICampService>(),
            IoC.Resolve<ICampSettingsProvider>(),
            IoC.Resolve<IGameMenuAdapter>(),
            new MapScreenCampMenuActivationQuery(IoC.Resolve<ICoopSessionProvider>()),
            IoC.ResolveAll<ICampOverlayContributor>().ToList(),
            camp => camp.BuildProgress(),
            // The themed overlay's icon-only Camp button shows the caption on hover. The caption is
            // already localized, so {=!} keeps it from being looked up again.
            caption => new HintViewModel(new TextObject("{=!}" + caption)));
        _vm.Refresh();

        // ScreenManager draws screen and global layers in ascending order. Installed v1.5.6 orders:
        // MapBar 202, MapIncidents 203, MapConversation 205. At 204 the Camp icon beside the clock
        // draws over the map bar (at 95 it was hidden under it), on its own order, and stays under
        // conversations. A literal so FieldCampLayerOrderTests can read it from the IL.
        Layer = new GauntletLayer("TaomFieldCampOverlay", 204);
        _layerAsGauntletLayer = (GauntletLayer)Layer;
        _movie = _layerAsGauntletLayer.LoadMovie("TaomFieldCampOverlay", _vm);
        Layer.InputRestrictions.SetInputRestrictions(false, InputUsageMask.MouseButtons | InputUsageMask.Keyboardkeys);
        MapScreen.AddLayer(Layer);
    }

    protected override void OnMapScreenUpdate(float dt)
    {
        base.OnMapScreenUpdate(dt);

        if (_vm == null)
            return;

        _refreshAccumulator += dt;
        if (_refreshAccumulator < RefreshIntervalSeconds)
            return;
        _refreshAccumulator = 0f;
        _vm.Refresh();
    }

    protected override void OnFinalize()
    {
        if (_layerAsGauntletLayer != null)
        {
            if (_movie != null)
            {
                _layerAsGauntletLayer.ReleaseMovie(_movie);
                _movie = null;
            }

            _layerAsGauntletLayer.InputRestrictions.ResetInputRestrictions();
            MapScreen.Instance?.RemoveLayer(_layerAsGauntletLayer);
            _layerAsGauntletLayer = null;
        }

        _vm?.OnFinalize();
        _vm = null;

        // The MapScreen dying IS campaign-session teardown for map visuals: the scene entities go
        // down with it, but the visual service's bookkeeping and Patch74's per-widget sprite cache
        // would otherwise leak stale handles into the next campaign loaded in this process.
        _visuals?.ClearAll();
        _visuals = null;
        PartyNameplateCampIconPatch.Reset();

        base.OnFinalize();
    }
}
