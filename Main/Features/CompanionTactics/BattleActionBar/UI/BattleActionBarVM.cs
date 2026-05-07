using System.Collections.Generic;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TAOM.Adapters;
using TAOM.Features.CompanionTactics.BattleActionBar.Models;

namespace TAOM.Features.CompanionTactics.BattleActionBar.UI;

/// <summary>
/// View model for the battle action bar. Bound by BattleActionBar.xml. Holds a reference
/// to <see cref="ITroopStanceManager"/> so each refresh derives button IsActive state
/// from authoritative stance state instead of letting buttons own a local toggle.
/// (P3-1 fix, Codex review #36.)
/// </summary>
public sealed class BattleActionBarVM : ViewModel
{
    private readonly IBattleActionBarService _service;
    private readonly ITroopStanceManager _stances;

    private bool _isVisible;
    private string _formationName = string.Empty;
    private int _currentFormationIndex = -1;
    private MBBindingList<ActionButtonVM> _actionButtons;

    [DataSourceProperty]
    public bool IsVisible
    {
        get => _isVisible;
        set
        {
            if (_isVisible != value)
            {
                _isVisible = value;
                OnPropertyChangedWithValue(value, nameof(IsVisible));
            }
        }
    }

    [DataSourceProperty]
    public string FormationName
    {
        get => _formationName;
        set
        {
            if (_formationName != value)
            {
                _formationName = value;
                OnPropertyChangedWithValue(value, nameof(FormationName));
            }
        }
    }

    [DataSourceProperty]
    public MBBindingList<ActionButtonVM> ActionButtons
    {
        get => _actionButtons;
        set
        {
            if (_actionButtons != value)
            {
                _actionButtons = value;
                OnPropertyChangedWithValue(value, nameof(ActionButtons));
            }
        }
    }

    public BattleActionBarVM(IBattleActionBarService service, ITroopStanceManager stances)
    {
        _service = service;
        _stances = stances;
        _actionButtons = new MBBindingList<ActionButtonVM>();
    }

    /// <summary>
    /// Refresh button list to match the supplied formation. Pass null to hide the bar.
    /// Always rebuilds (no formation-equality short-circuit) so external stance changes
    /// (Patch35_Formation_SetMovementOrder) and composition changes (casualties) are reflected.
    /// </summary>
    public void UpdateForFormation(Formation formation, IFormationAdapter adapter)
    {
        _actionButtons.Clear();

        if (formation == null || adapter == null)
        {
            IsVisible = false;
            FormationName = string.Empty;
            _currentFormationIndex = -1;
            return;
        }

        IsVisible = true;
        FormationName = formation.RepresentativeClass.ToString();
        _currentFormationIndex = adapter.FormationIndex;

        var actions = _service.GetActionsForFormation(adapter);
        foreach (var action in actions)
        {
            _actionButtons.Add(new ActionButtonVM(action));
        }
        SyncActiveFromStance();
    }

    /// <summary>
    /// Re-derive each button's <see cref="ActionButtonVM.IsActive"/> from the authoritative
    /// stance state held by <see cref="ITroopStanceManager"/>. Called after every action
    /// invocation and at the start of each refresh tick. Buttons whose <see cref="ActionButtonVM.MappedStance"/>
    /// is <see cref="TroopStance.None"/> (display-only Hold Fire / Free Fire / Volley / Shield Wall)
    /// always show inactive — they don't track stance state.
    /// </summary>
    public void SyncActiveFromStance()
    {
        if (_stances == null || _currentFormationIndex < 0) return;
        var current = _stances.GetStance(_currentFormationIndex);
        foreach (var button in _actionButtons)
        {
            button.IsActive = button.MappedStance != TroopStance.None && button.MappedStance == current;
        }
    }

    public ActionButtonVM TryGetActionByHotkeyIndex(int oneBasedIndex)
    {
        var zeroBased = oneBasedIndex - 1;
        if (zeroBased < 0 || zeroBased >= _actionButtons.Count) return null;
        return _actionButtons[zeroBased];
    }
}
