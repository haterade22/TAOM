using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.ViewModelCollection.GameMenu.TownManagement;
using TaleWorlds.Core;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade.View;
using TaleWorlds.ScreenSystem;
using TaleWorlds.TwoDimension;
using TAOM.Adapters;
using TAOM.Features.FiefManagement.Models;

namespace TAOM.Features.FiefManagement.UI;

public class GauntletFiefManagementScreen : ScreenBase, IGameStateListener
{
    private readonly FiefManagementGameState _state;
    private readonly IRemoteFiefSettlementSwapper _swapper;

    private TownManagementVM _dataSource;
    private GauntletLayer _layer;
    private SpriteCategory _spriteCategory;

    private Settlement _originalSettlement;
    private bool _swapActive;

    public GauntletFiefManagementScreen(FiefManagementGameState state, IRemoteFiefSettlementSwapper swapper)
    {
        _state = state;
        _swapper = swapper;
    }

    void IGameStateListener.OnActivate() { }
    void IGameStateListener.OnDeactivate() { }
    void IGameStateListener.OnInitialize() { }
    void IGameStateListener.OnFinalize() { }

    protected override void OnInitialize()
    {
        base.OnInitialize();

        var fief = _state.Fief;

        // Vanilla TownManagementVM reads Settlement.CurrentSettlement (which falls through to
        // MobileParty.MainParty.CurrentSettlement) in its constructor AND vanilla child VMs
        // (TownManagementReserveControlVM, SettlementGovernorSelectionItemVM) read it at runtime
        // when the user interacts with reserve / governor. We keep the swap active for the
        // lifetime of the screen and restore in OnFinalize. Safe because IsMenuState=true stops
        // campaign time, so no other code runs while the swap is active.
        try
        {
            _originalSettlement = _swapper.Swap(fief);
            _swapActive = true;
            _dataSource = new TownManagementVM();
        }
        catch
        {
            // If construction throws, restore immediately so we never leave the global field swapped.
            if (_swapActive)
            {
                _swapper.Restore(_originalSettlement);
                _swapActive = false;
            }
            throw;
        }

        _spriteCategory = UIResourceManager.LoadSpriteCategory("ui_town_management");

        _layer = new GauntletLayer("FiefManagement", 206, true);
        _layer.InputRestrictions.SetInputRestrictions();
        AddLayer(_layer);
        _layer.Input.RegisterHotKeyCategory(HotKeyManager.GetCategory("GenericPanelGameKeyCategory"));
        _dataSource.SetDoneInputKey(HotKeyManager.GetCategory("GenericPanelGameKeyCategory").GetHotKey("Confirm"));
        _layer.LoadMovie("TownManagement", _dataSource);
        _layer.IsFocusLayer = true;
        ScreenManager.TrySetFocus(_layer);
        _dataSource.Show = true;
    }

    protected override void OnFinalize()
    {
        if (_layer != null)
        {
            _layer.IsFocusLayer = false;
            ScreenManager.TryLoseFocus(_layer);
            RemoveLayer(_layer);
            _layer = null;
        }
        _spriteCategory?.Unload();
        _spriteCategory = null;
        _dataSource?.OnFinalize();
        _dataSource = null;

        if (_swapActive)
        {
            _swapper.Restore(_originalSettlement);
            _swapActive = false;
        }

        base.OnFinalize();
    }

    protected override void OnFrameTick(float dt)
    {
        base.OnFrameTick(dt);
        if (_layer == null || _dataSource == null) return;

        if (_layer.Input.IsHotKeyReleased("Confirm"))
        {
            UISoundsHelper.PlayUISound("event:/ui/default");
            if (_dataSource.ReserveControl?.IsEnabled == true)
            {
                _dataSource.ReserveControl.ExecuteConfirm();
            }
            else
            {
                _dataSource.ExecuteDone();
                PopState();
            }
        }
        else if (_layer.Input.IsHotKeyReleased("Exit"))
        {
            if (_dataSource.IsSelectingGovernor)
            {
                _dataSource.IsSelectingGovernor = false;
            }
            else if (_dataSource.ReserveControl?.IsEnabled == true)
            {
                _dataSource.ReserveControl.ExecuteCancel();
            }
            else
            {
                _dataSource.ExecuteDone();
                PopState();
            }
        }
    }

    private static void PopState()
    {
        Game.Current?.GameStateManager?.PopState();
    }
}
