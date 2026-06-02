using System;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Features.CareerSystem.UI;

public class CareerChoiceObjectVM : ViewModel
{
    private readonly CareerChoiceDefinition _choice;
    private readonly Func<string, bool> _selectChoice;
    private readonly Func<string, bool> _deselectChoice;
    private bool _isTaken;
    private bool _isFreeToTake;

    public CareerChoiceObjectVM(
        CareerChoiceDefinition choice,
        bool isTaken,
        bool isFreeToTake,
        Func<string, bool> selectChoice = null,
        Func<string, bool> deselectChoice = null)
    {
        _choice = choice;
        _isTaken = isTaken;
        _isFreeToTake = isFreeToTake && !isTaken;
        _selectChoice = selectChoice;
        _deselectChoice = deselectChoice;
    }

    public void SelectChoice()
    {
        if (_selectChoice != null && _selectChoice(_choice.Id))
            IsTaken = true;
    }

    public void DeSelectChoice()
    {
        if (_deselectChoice != null && _deselectChoice(_choice.Id))
            IsTaken = false;
    }

    [DataSourceProperty]
    public string Name => new TextObject(_choice.Id).ToString();

    [DataSourceProperty]
    public string Description => new TextObject(_choice.Description).ToString();

    [DataSourceProperty]
    public string IconSprite => _choice.IconSprite;

    [DataSourceProperty]
    public bool IsKeystone => _choice.Type == ChoiceType.Keystone;

    // Effect-scope badge shown next to each keystone bullet so the player can distinguish
    // always-active passives from effects that fire only while the career ability is active.
    //
    // PassiveEffect choices flow through CareerPassiveService and are read by GameModel overrides
    // on every relevant calculation -- always active. Mutation (Keystone) choices are applied to
    // a cloned AbilityTemplateData inside ExecuteAbilityEffect on V-press; the clone is discarded
    // once the buff window expires. See docs/features/career-system.md.
    //
    // The companion EffectScopeTooltip property was removed (Codex Review #32 + deep-review):
    // it was authored but never bound by the prefab. The "While active" badge alone is enough
    // UX signal -- passives need no explicit label.
    [DataSourceProperty]
    public string EffectScopeBadge => IsKeystone
        ? new TextObject("{=taom_career_choice_while_active}While active").ToString()
        : string.Empty;

    // Empty/locked pip state: shown dim when a slot is neither taken nor currently takeable.
    // The prefab renders three tinted copies of the point-pip gated on IsTaken / IsFreeToTake /
    // IsUnavailable so every slot always shows a pip (gold / brown / dim) instead of a blank gap.
    [DataSourceProperty]
    public bool IsUnavailable => !_isTaken && !_isFreeToTake;

    [DataSourceProperty]
    public bool IsTaken
    {
        get => _isTaken;
        set
        {
            if (_isTaken != value)
            {
                _isTaken = value;
                OnPropertyChangedWithValue(value, nameof(IsTaken));
                OnPropertyChanged(nameof(IsUnavailable));
            }
        }
    }

    [DataSourceProperty]
    public bool IsFreeToTake
    {
        get => _isFreeToTake;
        set
        {
            if (_isFreeToTake != value)
            {
                _isFreeToTake = value;
                OnPropertyChangedWithValue(value, nameof(IsFreeToTake));
                OnPropertyChanged(nameof(IsUnavailable));
            }
        }
    }

    public string ChoiceId => _choice.Id;
}
