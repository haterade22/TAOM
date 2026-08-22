using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade.View.Screens;
using TaleWorlds.ScreenSystem;
using TAOM.Core.Logging;

namespace TAOM.Features.SupplyLines.UI;

/// <summary>
/// Full-screen supply order UI, reached through <see cref="SupplyOrderGameState"/> via the
/// [GameStateScreen] attribute (the source module patched GameStateScreenManager.CreateScreen
/// instead; the attribute path needs no Harmony and cannot collide with TAOM's Patch36).
/// Engine-instantiated boundary: services are resolved here and injected into the VM.
/// </summary>
[GameStateScreen(typeof(SupplyOrderGameState))]
public class GauntletSupplyOrderScreen : ScreenBase, IGameStateListener
{
    private readonly ISupplySourceService _sourceService;
    private readonly ISupplyPricingService _pricingService;
    private readonly ISupplyOrderService _orderService;
    private readonly ISupplyLinesSettingsProvider _settings;
    private readonly IModLogger _logger;

    private GauntletLayer? _gauntletLayer;
    private GauntletMovieIdentifier? _movie;
    private SupplyOrderScreenVM? _viewModel;
    private bool _closed;

    // The state argument is what GameStateScreenManager's Activator.CreateInstance passes; the
    // screen needs nothing from it, but the single-arg ctor shape is what the attribute path
    // requires (CareerScreenGameState precedent).
    public GauntletSupplyOrderScreen(SupplyOrderGameState state)
    {
        _sourceService = IoC.Resolve<ISupplySourceService>();
        _pricingService = IoC.Resolve<ISupplyPricingService>();
        _orderService = IoC.Resolve<ISupplyOrderService>();
        _settings = IoC.Resolve<ISupplyLinesSettingsProvider>();
        _logger = IoC.Resolve<IModLogger>();
    }

    protected override void OnInitialize()
    {
        base.OnInitialize();
        try
        {
            _viewModel = new SupplyOrderScreenVM(
                _sourceService, _pricingService, _orderService, _settings,
                playerGold: () => Hero.MainHero?.Gold ?? 0,
                closeAction: CloseScreen);

            _gauntletLayer = new GauntletLayer("SupplyOrderLayer", 15)
            {
                IsFocusLayer = true,
            };
            _gauntletLayer.InputRestrictions.SetInputRestrictions();

            // Both categories so Exit (panel) and the campaign panel keys resolve; null-guarded
            // because a missing category must degrade to mouse-only, not crash the open.
            var panelCategory = HotKeyManager.GetCategory("GenericPanelGameKeyCategory");
            if (panelCategory != null)
                _gauntletLayer.Input.RegisterHotKeyCategory(panelCategory);
            var campaignCategory = HotKeyManager.GetCategory("GenericCampaignPanelsGameKeyCategory");
            if (campaignCategory != null)
                _gauntletLayer.Input.RegisterHotKeyCategory(campaignCategory);

            _movie = _gauntletLayer.LoadMovie("TaomSupplyOrderScreen", _viewModel);
            AddLayer(_gauntletLayer);
            ScreenManager.TrySetFocus(_gauntletLayer);
        }
        catch (Exception ex)
        {
            _logger?.LogError($"SupplyLines: supply order screen failed to open: {ex}");
            InformationManager.DisplayMessage(new InformationMessage(
                new TextObject("{=taom_sl_screen_open_failed}The supply screen failed to open (see log). Returning to the map.").ToString(),
                Colors.Red));
            CloseScreen();
        }
    }

    protected override void OnFrameTick(float dt)
    {
        base.OnFrameTick(dt);
        if (!_closed && _gauntletLayer != null && _gauntletLayer.Input.IsHotKeyReleased("Exit"))
            CloseScreen();
    }

    protected override void OnActivate()
    {
        base.OnActivate();
        if (_gauntletLayer != null)
        {
            ScreenManager.SetSuspendLayer(_gauntletLayer, false);
            ScreenManager.TrySetFocus(_gauntletLayer);
        }
    }

    protected override void OnDeactivate()
    {
        base.OnDeactivate();
        if (_gauntletLayer != null)
            ScreenManager.SetSuspendLayer(_gauntletLayer, true);
    }

    private void CloseScreen()
    {
        // Latch: the Exit hotkey, the VM's cancel and a failed open can all race into here; the
        // second entry must not pop the state underneath (the map) as well.
        if (_closed)
            return;
        _closed = true;

        _gauntletLayer?.InputRestrictions.ResetInputRestrictions();
        if (_gauntletLayer != null && _movie != null)
            _gauntletLayer.ReleaseMovie(_movie);
        _movie = null;
        _viewModel?.OnFinalize();
        Game.Current?.GameStateManager?.PopState();
    }

    protected override void OnFinalize()
    {
        base.OnFinalize();
        if (!_closed)
        {
            // The state was popped from outside CloseScreen (session teardown): release what we
            // hold, but never PopState again.
            _closed = true;
            _gauntletLayer?.InputRestrictions.ResetInputRestrictions();
            if (_gauntletLayer != null && _movie != null)
                _gauntletLayer.ReleaseMovie(_movie);
            _viewModel?.OnFinalize();
        }
        _movie = null;
        _viewModel = null;
        _gauntletLayer = null;
    }

    // IGameStateListener: GameStateScreenManager.OnCreateState registers null if the screen
    // does not implement this interface (CareerScreen precedent).
    void IGameStateListener.OnInitialize() { }
    void IGameStateListener.OnFinalize() { }
    void IGameStateListener.OnActivate() { }
    void IGameStateListener.OnDeactivate() { }
}
