using System;
using TaleWorlds.Core;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade.GauntletUI.Mission.Singleplayer;
using TaleWorlds.MountAndBlade.View;
using TaleWorlds.MountAndBlade.View.MissionViews;
using TAOM.Core.Logging;
using TAOM.Features.CheatMenu;
using TAOM.Features.CheatMenu.Battle;

namespace TAOM.Client.Features.CheatMenu.UI;

/// <summary>
/// The battle cheat menu's key (docs/features/cheat-menu.md, "Battle"): a <c>[DefaultView]</c> (a view TAOM adds in
/// OnMissionBehaviorInitialize never ticks, #970) that reads the bound "Open Battle Cheat Menu (TAOM)" key once per real frame and
/// asks <see cref="BattleCheatOpener.TryOpenOnKey"/>, which reads no key at all while the switch is off or outside single player.
/// The press does not count with Ctrl or Alt held (vanilla's Ctrl+H cheat), nor while the Esc menu, the order panel, the radial
/// menu, photo mode or another pop-up is open. Never throws out of the mission tick: a failure is logged once.
/// </summary>
[DefaultView]
public sealed class BattleCheatMenuView : MissionView
{
    private BattleCheatOpener? _opener;
    private GameKey? _key;
    private bool _resolved;
    private bool _failed;
    private Func<bool>? _pressed;

    public override void OnMissionScreenTick(float dt)
    {
        base.OnMissionScreenTick(dt);
        if (_failed || MissionScreen == null || Mission == null)
            return;
        try
        {
            if (!_resolved)
                Resolve();
            _opener?.TryOpenOnKey(_pressed!);
        }
        catch (Exception ex)
        {
            _failed = true;
            try { IoC.Resolve<IModLogger>()?.LogError($"[CheatMenu] BattleCheatMenuView stopped reading its key: {ex}"); }
            catch { /* swallow-ok: never throw out of the mission tick */ }
        }
    }

    private void Resolve()
    {
        _resolved = true; // latched even on a miss: the category registers at process load, before any mission
        _opener = IoC.Resolve<BattleCheatOpener>();
        foreach (var category in HotKeyManager.GetAllCategories())
        {
            if (category.GameKeyCategoryId == TaomCheatMenuHotKeyCategory.CategoryId)
                _key = category.GetGameKey(TaomCheatMenuHotKeyCategory.OpenBattleCheatMenuKeyId);
        }
        _pressed = Pressed;
    }

    private bool Pressed()
    {
        var input = Input;
        var key = _key?.KeyboardKey?.InputKey ?? InputKey.Invalid;
        if (input == null || key == InputKey.Invalid || !input.IsKeyPressed(key))
            return false;
        if (input.IsKeyDown(InputKey.LeftControl) || input.IsKeyDown(InputKey.RightControl)
            || input.IsKeyDown(InputKey.LeftAlt) || input.IsKeyDown(InputKey.RightAlt))
            return false;
        return !Busy();
    }

    private bool Busy()
    {
        var mission = Mission;
        var screen = MissionScreen;
        if (mission == null || screen == null) return true;
        if (mission.IsOrderMenuOpen || screen.IsPhotoModeEnabled || screen.IsRadialMenuActive) return true;
        if (InformationManager.IsAnyInquiryActive()) return true;
        var escape = mission.GetMissionBehavior<MissionGauntletSingleplayerEscapeMenu>();
        return escape != null && escape.IsActive;
    }
}
