using System;
using TaleWorlds.CampaignSystem.ViewModelCollection.Encyclopedia;
using TaleWorlds.Core;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.ScreenSystem;

namespace TAOM.Client.Features.Realms.Popups;

/// <summary>
/// A Realms popup (the title tree or the claimants) as a layer of the screen that was on top when it opened, built the
/// way TAOM's Lines of Descent popup is. Every way out ends in <see cref="Close"/>, which is safe to call twice: the close
/// button and Escape, the host deactivating (a screen pushed over it), the encyclopedia changing page (a card click, or the
/// encyclopedia closing) and the host finalizing under the layer. The button, deactivation and page change only set a flag;
/// <see cref="Tick"/> acts on it in the same frame, after the movie updated, so the movie is never released from inside its
/// own update. A layer is never reused.
/// </summary>
internal sealed class RealmsPopupLayer : GauntletLayer
{
    // Over the encyclopedia bar (310) and TAOM's family tree uses the same order; only one popup is open at a time.
    private const int LayerOrder = 320;

    private readonly ScreenBase _host;
    private readonly Action<RealmsPopupLayer> _onClosed;
    private readonly bool _pausesGame;
    private GauntletMovieIdentifier? _movie;
    private ViewModel? _vm;
    private bool _subscribed;
    private bool _paused;
    private bool _tornDown;
    private bool _closeRequested;

    /// <param name="pausesGame">
    /// Holds the active game state (the campaign map's time) while open, the way an inquiry shown with
    /// <c>pauseGameActiveState</c> does: an event window. The title popups open over the encyclopedia, which already pauses.
    /// </param>
    public RealmsPopupLayer(ScreenBase host, Action<RealmsPopupLayer> onClosed, bool pausesGame = false)
        : base("TaomRealmsPopup", LayerOrder, false)
    {
        _host = host;
        _onClosed = onClosed;
        _pausesGame = pausesGame;
    }

    /// <summary>Adds the layer, builds the whole view model (given the close action), loads the movie and takes focus.</summary>
    public void Show(string movieName, Func<Action, ViewModel> buildViewModel)
    {
        // Card frames, tree lines and the masked portraits live here; a screen other than the encyclopedia may not have it.
        UIResourceManager.LoadSpriteCategory("ui_encyclopedia");
        InputRestrictions.SetInputRestrictions();
        // "Exit" (Escape) lives in GenericPanelGameKeyCategory.
        Input.RegisterHotKeyCategory(HotKeyManager.GetCategory("GenericPanelGameKeyCategory"));
        IsFocusLayer = true;
        // Added first: whatever throws below, Close can then remove the layer and nothing is left on the host.
        _host.AddLayer(this);
        if (_pausesGame && GameStateManager.Current != null)
        {
            GameStateManager.Current.RegisterActiveStateDisableRequest(this);
            _paused = true;
        }

        _vm = buildViewModel(RequestClose);
        _movie = LoadMovie(movieName, _vm);
        Game.Current.EventManager.RegisterEvent<EncyclopediaPageChangedEvent>(OnEncyclopediaPageChanged);
        _subscribed = true;
        ScreenManager.TrySetFocus(this);
    }

    public void Close()
    {
        Teardown();
        if (!IsFinalized && _host.HasLayer(this))
            _host.RemoveLayer(this);
    }

    private void RequestClose() => _closeRequested = true;

    private void OnEncyclopediaPageChanged(EncyclopediaPageChangedEvent e) => _closeRequested = true;

    protected override void Tick(float dt)
    {
        if (_tornDown)
            return;
        base.Tick(dt);
        if (_closeRequested || Input.IsHotKeyReleased("Exit"))
            Close();
    }

    protected override void OnDeactivate()
    {
        // A deactivated layer is not ticked: this closes it on the first tick after the host is active again.
        _closeRequested = true;
        base.OnDeactivate();
    }

    protected override void OnFinalize()
    {
        // The host finalizing walks its own layer list, so this path must not remove the layer again.
        Teardown();
        base.OnFinalize();
    }

    private void Teardown()
    {
        if (_tornDown)
            return;
        _tornDown = true;
        try
        {
            if (_subscribed)
            {
                Game.Current?.EventManager.UnregisterEvent<EncyclopediaPageChangedEvent>(OnEncyclopediaPageChanged);
                _subscribed = false;
            }

            if (_paused)
            {
                _paused = false;
                GameStateManager.Current?.UnregisterActiveStateDisableRequest(this);
            }

            InputRestrictions.ResetInputRestrictions();
            ScreenManager.TryLoseFocus(this);
            if (_movie != null)
            {
                ReleaseMovie(_movie);
                _movie = null;
            }

            _vm?.OnFinalize();
            _vm = null;
        }
        finally
        {
            _onClosed(this);
        }
    }
}
