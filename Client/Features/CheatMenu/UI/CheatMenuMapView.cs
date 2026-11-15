using SandBox.View.Map;
using TAOM.Core.Logging;

namespace TAOM.Features.CheatMenu.UI;

/// <summary>
/// Reads the cheat menu's map key every map frame (docs/features/cheat-menu.md), attached by <c>CheatMenuCampaignBehavior</c>
/// (the parameterless ctor is AddMapView's new() constraint, so services come from IoC, the sanctioned engine-instantiated
/// boundary). Every gate sits in <see cref="CheatMenuOpener.TryOpenFromHotKey"/>, the same opener the map button uses: with
/// the switch off or in co-op the key is not read. No layer, nothing drawn.
/// </summary>
public class CheatMenuMapView : MapView
{
    private CheatMenuOpener? _opener;
    private CheatMenuKeys? _keys;
    private System.Func<bool>? _pressed;
    private bool _errorLogged;

    protected override void CreateLayout()
    {
        base.CreateLayout();
        _opener = IoC.Resolve<CheatMenuOpener>();
        _keys = new CheatMenuKeys();
        _pressed = () => _keys.OpenPressed;
    }

    protected override void OnMapScreenUpdate(float dt)
    {
        base.OnMapScreenUpdate(dt);
        if (_opener == null || _pressed == null)
            return;
        try
        {
            _opener.TryOpenFromHotKey(_pressed);
        }
        catch (System.Exception ex)
        {
            // Polled every frame: never throw out of the map screen, and log once per view.
            if (_errorLogged)
                return;
            _errorLogged = true;
            try { IoC.Resolve<IModLogger>().LogError($"[CheatMenu] CheatMenuMapView.OnMapScreenUpdate threw: {ex}"); }
            catch { /* swallow-ok: a per-frame poll must never turn into a crash */ }
        }
    }
}
