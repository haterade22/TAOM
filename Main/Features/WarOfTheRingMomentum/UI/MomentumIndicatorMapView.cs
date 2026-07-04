using SandBox.View.Map;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.Library;
using TaleWorlds.ScreenSystem;

namespace TAOM.Features.WarOfTheRingMomentum.UI;

/// <summary>
/// Persistent on-map "War of the Ring" slider (LOTRAOM parity source: MomentumIndicator).
/// Added/removed via MapScreen.AddMapView (IMomentumUIService). Parameterless ctor is
/// required by AddMapView's new() constraint, so services resolve via IoC here — the
/// sanctioned engine-instantiated-boundary exception (GauntletCareerScreen precedent).
/// The overlay must NOT steal map input: SetInputRestrictions(false, mouse+keys) and
/// no IsFocusLayer (gui-ui.md). Owns the popup, including Escape-close and a light
/// visibility poll for the MCM toggle.
/// </summary>
public class MomentumIndicatorMapView : MapView
{
    private const float VisibilityPollSeconds = 1.0f;

    private MomentumIndicatorVM _dataSource;
    private MomentumIndicatorItemVM _itemVm;
    private GauntletLayer _layerAsGauntletLayer;
    private MomentumPopupController _popup;
    private float _pollAccumulator;

    protected override void CreateLayout()
    {
        base.CreateLayout();

        UIResourceManager.LoadSpriteCategory("ui_encyclopedia");
        UIResourceManager.LoadSpriteCategory("ui_kingdom");

        var query = IoC.Resolve<IMomentumQueryService>();
        var settings = IoC.Resolve<IMomentumSettingsProvider>();
        _popup = new MomentumPopupController(query);
        _itemVm = new MomentumIndicatorItemVM(query, settings, OpenPopup);
        _dataSource = new MomentumIndicatorVM(_itemVm);

        Layer = new GauntletLayer("MomentumMapIndicator", 100);
        _layerAsGauntletLayer = (GauntletLayer)Layer;
        _layerAsGauntletLayer.LoadMovie("MomentumMapIndicator", _dataSource);
        Layer.InputRestrictions.SetInputRestrictions(false, InputUsageMask.MouseButtons | InputUsageMask.Keyboardkeys);
        MapScreen.AddLayer(Layer);
    }

    protected override void OnResume()
    {
        // SpriteCategory.Unload is not ref-counted — reload on resume (LOTRAOM pattern).
        UIResourceManager.LoadSpriteCategory("ui_encyclopedia");
        UIResourceManager.LoadSpriteCategory("ui_kingdom");
        base.OnResume();
    }

    protected override void OnMapScreenUpdate(float dt)
    {
        base.OnMapScreenUpdate(dt);

        if (_popup != null && _popup.IsEscapeRequested)
            _popup.Close();

        // Cheap poll so an MCM "Show Map Meter" toggle takes effect without waiting
        // for the next momentum event; notifies only on change.
        _pollAccumulator += dt;
        if (_pollAccumulator >= VisibilityPollSeconds)
        {
            _pollAccumulator = 0f;
            _itemVm?.OnMomentumChanged();
        }
    }

    private void OpenPopup()
    {
        _popup?.Show(ScreenManager.TopScreen);
    }

    protected override void OnFinalize()
    {
        _popup?.Close();
        _popup = null;

        _itemVm?.Unsubscribe();
        _itemVm = null;

        if (_layerAsGauntletLayer != null)
        {
            MapScreen.Instance?.RemoveLayer(_layerAsGauntletLayer);
            _layerAsGauntletLayer = null;
        }

        _dataSource?.OnFinalize();
        _dataSource = null;

        UIResourceManager.SpriteData.SpriteCategories["ui_encyclopedia"].Unload();
        UIResourceManager.SpriteData.SpriteCategories["ui_kingdom"].Unload();
        base.OnFinalize();
    }
}
