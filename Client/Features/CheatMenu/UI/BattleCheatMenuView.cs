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
using TAOM.Client.Features.FordsOfWar;
using TAOM.Client.Features.LongDefeat;

namespace TAOM.Client.Features.CheatMenu.UI;

/// <summary>
/// The battle cheat menu's key (docs/features/cheat-menu.md, "Battle"): a <c>[DefaultView]</c> (a view TAOM adds in
/// OnMissionBehaviorInitialize never ticks, #970) that reads the bound "Open Battle Cheat Menu (TAOM)" key once per real frame and
/// asks <see cref="BattleCheatOpener.TryOpenOnKey"/>, which reads no key at all while the switch is off or outside single player.
/// The press does not count with Ctrl or Alt held (vanilla's Ctrl+H cheat), nor while the Esc menu, the order panel, the radial
/// menu, photo mode or another pop-up is open. Never throws out of the mission tick: a failure is logged once.
/// The same tick keeps god mode on the player and the horse they ride (<see cref="BattleCheatService.TickGodMode"/>), whether it
/// was turned on from the list or the console; that has its own guard, so a key failure does not stop it.
/// </summary>
[DefaultView]
public sealed class BattleCheatMenuView : MissionView
{
    private BattleCheatOpener? _opener;
    private GameKey? _key;
    private bool _resolved;
    private bool _failed;
    private Func<bool>? _pressed;
    private BattleCheatService? _godMode;
    private bool _godModeFailed;

    public override void OnMissionScreenTick(float dt)
    {
        base.OnMissionScreenTick(dt);
        TickGodMode();
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

    private void TickGodMode()
    {
        if (_godModeFailed || Mission == null)
            return;
        try
        {
            _godMode ??= IoC.Resolve<BattleCheatService>();
            _godMode?.TickGodMode();
        }
        catch (Exception ex)
        {
            _godModeFailed = true;
            try { IoC.Resolve<IModLogger>()?.LogError($"[CheatMenu] BattleCheatMenuView stopped keeping god mode: {ex.GetType().Name} 0x{ex.HResult:X8}"); }
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
        // Fords of War (field a hero) and The Long Defeat (horn) read raw H in these views; the Esc menu item still opens
        // the list there. A story mission carries LongDefeatMissionLogic but not the HUD view, so its key stays on.
        if (mission.GetMissionBehavior<FordsCommandView>() != null
            || mission.GetMissionBehavior<LongDefeatHudView>() != null) return true;
        var escape = mission.GetMissionBehavior<MissionGauntletSingleplayerEscapeMenu>();
        return escape != null && escape.IsActive;
    }
}
