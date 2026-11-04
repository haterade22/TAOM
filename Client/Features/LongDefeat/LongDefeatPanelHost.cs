using System;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.InputSystem;
using TaleWorlds.MountAndBlade.View.Screens;
using TaleWorlds.ScreenSystem;

namespace TAOM.Client.Features.LongDefeat;

/// <summary>
/// The panel layer of the Long Defeat HUD: created when the view model wants it, removed when it does not, in the shape of
/// <c>MissionGauntletEscapeMenuBase</c> but without its engine pause, so the wave timer runs. Also reads B and H while no panel is open.
/// </summary>
internal sealed class LongDefeatPanelHost
{
    internal const int PanelLayerOrder = 21;
    internal const string PanelMovieName = "LongDefeatPanel";

    private GauntletLayer? _layer;
    private GauntletMovieIdentifier? _movie;
    private readonly Action<string> _log;
    private string _what = "shop";

    internal LongDefeatPanelHost(Action<string> log) => _log = log;

    /// <summary>B toggles the shop and H sounds the horn. Ctrl or Alt leave every key to the game.</summary>
    internal void ReadKeys(IInputContext input, LongDefeatHudVM vm)
    {
        if (_layer != null || input.IsControlDown() || input.IsAltDown())
            return;

        if (input.IsKeyPressed(InputKey.B))
            vm.ToggleShop();
        if (input.IsKeyPressed(InputKey.H))
            vm.SoundHorn();
    }

    /// <summary>
    /// Loads and releases the panel movie once on a host layer, so a missing prefab shows at mission start and not when the player first
    /// presses B. Returns whether it loaded.
    /// </summary>
    internal static bool ProbeLoads(GauntletLayer host, LongDefeatHudVM vm)
    {
        var probe = host.LoadMovie(PanelMovieName, vm);
        var loaded = probe?.Movie?.IsLoaded == true;
        if (probe != null)
            host.ReleaseMovie(probe);
        return loaded;
    }

    internal void Sync(MissionScreen screen, LongDefeatHudVM vm)
    {
        if (vm.WantsPanel && _layer == null)
        {
            Open(screen, vm);
        }
        else if (!vm.WantsPanel && _layer != null)
        {
            Remove(screen);
        }
        else if (_layer != null && (_layer.Input.IsHotKeyReleased("Exit") || _layer.Input.IsHotKeyReleased("ToggleEscapeMenu")))
        {
            vm.ExecuteClose();
        }
    }

    internal void Remove(MissionScreen? screen)
    {
        if (_layer == null)
            return;

        if (_movie != null)
            _layer.ReleaseMovie(_movie);
        _layer.InputRestrictions.ResetInputRestrictions();
        screen?.RemoveLayer(_layer);
        _layer = null;
        _movie = null;
        Log(_what + " closed");
    }

    private void Open(MissionScreen screen, LongDefeatHudVM vm)
    {
        var layer = new GauntletLayer("GauntletLayer", PanelLayerOrder, false);
        layer.IsFocusLayer = true;
        // The parameterless call is the engine's own shape for a modal focus panel (MissionGauntletEscapeMenuBase.cs:77-82): the cursor shows and every input is restricted.
        // The prefab's root takes events for the same reason: a click on the dim background must not reach the mission (Native's EscapeMenu.xml sets no DoNotAcceptEvents).
        layer.InputRestrictions.SetInputRestrictions();
        layer.Input.RegisterHotKeyCategory(HotKeyManager.GetCategory("GenericPanelGameKeyCategory"));
        var movie = layer.LoadMovie(PanelMovieName, vm);
        if (movie?.Movie?.IsLoaded != true)
        {
            layer.InputRestrictions.ResetInputRestrictions();
            throw new System.InvalidOperationException("the panel movie " + PanelMovieName + " did not load (IsLoaded false)");
        }

        _what = vm.IsShopVisible ? "shop" : "end panel";
        _layer = layer;
        _movie = movie;
        screen.AddLayer(layer);
        ScreenManager.TrySetFocus(layer);
        Log(_what + " open");
    }

    internal const string LogPrefix = "[LongDefeat][Hud] ";

    private void Log(string line) => _log(LogPrefix + line);
}
