using System;
using TaleWorlds.Library;
using TAOM.Features.CompanionTactics.BattleActionBar.Models;

namespace TAOM.Features.CompanionTactics.BattleActionBar.UI;

/// <summary>
/// Single action button on the battle action bar. Bound by BattleActionBar.xml's ItemTemplate.
/// IsActive reflects the underlying TroopStance state for the button's owning formation;
/// it is NOT a local toggle (P3-1 fix, Codex review #36 — buttons used to drift out of
/// sync with TroopStanceManager.ClearStance because each click flipped its own bool).
/// </summary>
public sealed class ActionButtonVM : ViewModel
{
    private readonly Action _executeAction;
    private bool _isActive;

    [DataSourceProperty]
    public string ActionId { get; }

    [DataSourceProperty]
    public string DisplayText { get; }

    [DataSourceProperty]
    public string Hotkey { get; }

    [DataSourceProperty]
    public string CategoryColor { get; }

    /// <summary>The stance this button represents (or <see cref="TroopStance.None"/> for buttons
    /// that don't map to a stance — e.g., display-only Hold Fire / Free Fire / Volley / Shield Wall).</summary>
    public TroopStance MappedStance { get; }

    [DataSourceProperty]
    public bool IsActive
    {
        get => _isActive;
        set
        {
            if (_isActive != value)
            {
                _isActive = value;
                OnPropertyChangedWithValue(value, nameof(IsActive));
            }
        }
    }

    public ActionButtonVM(BattleAction action)
    {
        ActionId = action.Id;
        DisplayText = action.Label;
        Hotkey = action.Hotkey;
        CategoryColor = MapCategoryColor(action.Category);
        MappedStance = MapStance(action.Id);
        _executeAction = action.Execute;
    }

    public void ExecuteAction()
    {
        // Don't self-toggle IsActive. The parent VM re-syncs from ITroopStanceManager via
        // SyncActiveFromStance(formationIndex) immediately after invoking the action callback,
        // and again on the next refresh tick. This keeps the button in sync with stance state
        // even when an external path (Patch35_Formation_SetMovementOrder) clears the stance.
        _executeAction?.Invoke();
    }

    /// <summary>Map an action button's id to the stance the button toggles. Returns
    /// <see cref="TroopStance.None"/> for non-stance buttons (display-only ranged/shield variants).</summary>
    private static TroopStance MapStance(string actionId) => actionId switch
    {
        "action_brace" => TroopStance.BracedForCavalry,
        "action_pike_wall" => TroopStance.PikeWall,
        "action_testudo" => TroopStance.Testudo,
        "action_charge" => TroopStance.LineCharge,
        "action_skirmish" => TroopStance.Skirmish,
        _ => TroopStance.None,
    };

    private static string MapCategoryColor(ActionCategory category) => category switch
    {
        ActionCategory.Ranged => "#88FF88FF",
        ActionCategory.Polearm => "#8888FFFF",
        ActionCategory.Shield => "#FFFF88FF",
        ActionCategory.Cavalry => "#FF8888FF",
        _ => "#FFFFFFFF",
    };
}
