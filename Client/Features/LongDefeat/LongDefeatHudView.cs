using System;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.MissionViews;
using TaleWorlds.MountAndBlade.ViewModelCollection.Order.Visual;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.LongDefeat;
using TAOM.Features.LongDefeat.Autorun;
using TAOM.Features.LongDefeat.Diagnostics;

namespace TAOM.Client.Features.LongDefeat;

/// <summary>
/// The Long Defeat HUD (#1079), built by the mission's <c>[ViewMethod]</c> list. Two layers. The HUD layer (<c>LongDefeatHud</c>) takes
/// no input restriction, because the player has an agent and the cursor must stay off. The panel layer (<c>LongDefeatPanel</c>) is
/// created when the view model wants it (the shop on B, the end summary when the run ends) and removed when it does not, in the shape
/// of <c>MissionGauntletEscapeMenuBase</c> but without its engine pause, so the wave timer runs. B toggles the shop and H sounds the
/// horn; both are read only while no panel is open, and Ctrl or Alt leave every key to the game. Any fault latches the view off, logs
/// once and writes a failing <c>hud</c> check.
/// </summary>
public sealed class LongDefeatHudView : MissionView
{
    // Above the vanilla mission HUD layers (13 to 17) and below the options handler (49) and the escape menu (50).
    internal const int HudLayerOrder = 20;

    private static int _serial;

    private readonly string _key = "m" + System.Threading.Interlocked.Increment(ref _serial);
    private LongDefeatSession? _session;
    private LongDefeatHudVM? _vm;
    private GauntletLayer? _hudLayer;
    private GauntletMovieIdentifier? _hudMovie;
    private readonly LongDefeatPanelHost _panel = new(line => IoC.Resolve<IModLogger>()?.LogInfo(line));
    private readonly LongDefeatHudClock _clock = new();
    private LongDefeatMissionSpeed? _speed;
    private bool _ordersChecked;
    private bool _failed;

    /// <summary>A lost focus opens the Escape menu, which pauses the mission until someone closes it. An armed run says no, as the Fords panel does.</summary>
    public override bool IsOpeningEscapeMenuOnFocusChangeAllowed() =>
        !LongDefeatAutorunPlan.ArmedInThisProcess && base.IsOpeningEscapeMenuOnFocusChangeAllowed();

    // The escape menu offers Photo Mode and no Long Defeat view can close it again, so the run would soft-lock; one "no" disables the item.
    public override bool IsPhotoModeAllowed() => false;

    public override void OnMissionScreenTick(float dt)
    {
        base.OnMissionScreenTick(dt);
        if (_failed || Mission == null)
            return;

        try
        {
            _session ??= IoC.Resolve<LongDefeatSession>();
            if (_session == null)
                return;

            if (_vm == null && _session.IsActive)
                Attach();
            if (_vm == null)
                return;

            _vm.Tick(dt);
            _clock.Advance(dt);
            _panel.ReadKeys(Input, _vm);
            _speed?.Apply(Mission, _session.Speed, dt);
            if (_clock.TakeRefresh())
                _vm.Refresh();

            _panel.Sync(MissionScreen, _vm);
            CheckOrders();
        }
        catch (Exception ex)
        {
            _failed = true;
            IoC.Resolve<IModLogger>()?.LogError($"[LongDefeat] HUD failed, layers removed: {ex}");
            IoC.Resolve<IOutcomeLog>()?.Fail(LongDefeatClientChecks.HudId, _key, "reason=fault error=" + ex.GetType().Name);
            TryDetach();
        }
    }

    public override void OnMissionScreenFinalize()
    {
        TryDetach();
        base.OnMissionScreenFinalize();
    }

    private void Attach()
    {
        _vm = new LongDefeatHudVM(_session!, Leave);
        _hudLayer = new GauntletLayer("GauntletLayer", HudLayerOrder, false);
        _hudMovie = _hudLayer.LoadMovie("LongDefeatHud", _vm);
        MissionScreen.AddLayer(_hudLayer);
        _speed = new LongDefeatMissionSpeed(IoC.Resolve<IModLogger>());
        if (LongDefeatAutorunPlan.Resolve() is { Armed: true } plan)
            _session!.SetSpeed(plan.Speed);
        _vm.Refresh();
        var panelLoaded = LongDefeatPanelHost.ProbeLoads(_hudLayer, _vm);
        var hudLoaded = _hudMovie.Movie?.IsLoaded == true;
        LongDefeatChecks.Record(IoC.Resolve<IOutcomeLog>(), LongDefeatClientChecks.HudId, _key,
            LongDefeatClientChecks.Hud(_vm.WaveText, hudLoaded, panelLoaded));
    }

    private void CheckOrders()
    {
        if (_ordersChecked || !_clock.OrdersDelayElapsed || Mission.MainAgent == null)
            return;

        _ordersChecked = true;
        LongDefeatChecks.Record(IoC.Resolve<IOutcomeLog>(), LongDefeatClientChecks.OrdersId, _key,
            LongDefeatClientChecks.Orders(VisualOrderFactory.GetOrders().Count));
    }

    private void Leave() => LongDefeatGameManager.ReturnToMainMenu(IoC.Resolve<IModLogger>());

    private void TryDetach()
    {
        try
        {
            if (Mission != null)
                LongDefeatMissionSpeed.Stop(Mission);
            _panel.Remove(MissionScreen);
            if (_hudLayer != null)
            {
                if (_hudMovie != null)
                    _hudLayer.ReleaseMovie(_hudMovie);
                MissionScreen?.RemoveLayer(_hudLayer);
            }

            _vm?.OnFinalize();
        }
        catch (Exception ex)
        {
            IoC.Resolve<IModLogger>()?.LogError($"[LongDefeat] HUD detach failed: {ex.Message}");
        }

        _vm = null;
        _hudLayer = null;
        _hudMovie = null;
    }
}
