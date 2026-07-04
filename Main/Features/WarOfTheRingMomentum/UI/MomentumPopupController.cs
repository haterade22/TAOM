using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.GauntletUI.Data;
using TaleWorlds.InputSystem;
using TaleWorlds.ScreenSystem;

namespace TAOM.Features.WarOfTheRingMomentum.UI;

/// <summary>
/// Pushes the MomentumView popup as a GauntletLayer onto the map screen (LOTRAOM
/// parity source: MomentumInterface + GenericInterface, minus the AccessTools2
/// reflection — LoadMovie/ReleaseMovie are public on 1.4.6). Owned by the indicator
/// MapView, which also drives Escape-close via IsEscapeRequested.
/// </summary>
public class MomentumPopupController
{
    private readonly IMomentumQueryService _query;

    private GauntletLayer _layer;
    private GauntletMovieIdentifier _movie;
    private MomentumPopupVM _vm;
    private ScreenBase _screen;

    public MomentumPopupController(IMomentumQueryService query)
    {
        _query = query;
    }

    public bool IsOpen => _layer != null;

    /// <summary>True when the open popup's Exit hotkey (Escape) was released this frame.</summary>
    public bool IsEscapeRequested => _layer != null && _layer.Input.IsHotKeyReleased("Exit");

    public void Show(ScreenBase screen)
    {
        if (IsOpen || screen == null)
            return;

        _screen = screen;
        _layer = new GauntletLayer("MomentumPopup", 209);
        _layer.InputRestrictions.SetInputRestrictions();
        // "Exit" (Escape) lives in GenericPanelGameKeyCategory, NOT the ...CampaignPanels...
        // category (which has only window-toggle keys) — using the wrong one silently
        // disables Escape-close. TAOM precedent: GauntletFiefManagementScreen.
        _layer.Input.RegisterHotKeyCategory(HotKeyManager.GetCategory("GenericPanelGameKeyCategory"));
        _layer.IsFocusLayer = true;
        _vm = new MomentumPopupVM(_query, Close);
        _movie = _layer.LoadMovie("MomentumView", _vm);
        screen.AddLayer(_layer);
        ScreenManager.TrySetFocus(_layer);
    }

    public void Close()
    {
        if (!IsOpen)
            return;

        _layer.InputRestrictions.ResetInputRestrictions();
        ScreenManager.TryLoseFocus(_layer);
        _layer.ReleaseMovie(_movie);
        _screen.RemoveLayer(_layer);
        _vm.OnFinalize();

        _movie = null;
        _layer = null;
        _vm = null;
        _screen = null;
    }
}
