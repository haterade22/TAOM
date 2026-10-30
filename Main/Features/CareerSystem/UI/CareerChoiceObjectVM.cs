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
    private readonly string _keystoneIconSprite;
    private readonly ChoiceLockReason _lockReason;
    private readonly string _lockReasonText;
    private bool _isTaken;
    private bool _isFreeToTake;

    public CareerChoiceObjectVM(
        CareerChoiceDefinition choice,
        bool isTaken,
        bool isFreeToTake,
        Func<string, bool> selectChoice = null,
        Func<string, bool> deselectChoice = null,
        string keystoneIconSprite = "",
        ChoiceLockReason lockReason = ChoiceLockReason.None,
        int tierUnlockLevel = 0)
    {
        _choice = choice;
        _isTaken = isTaken;
        _isFreeToTake = isFreeToTake && !isTaken;
        _selectChoice = selectChoice;
        _deselectChoice = deselectChoice;
        _keystoneIconSprite = keystoneIconSprite ?? "";
        _lockReason = lockReason;
        _lockReasonText = BuildLockReasonText(lockReason, tierUnlockLevel);
    }

    // #769: why the diamond is shut. None and the not-a-lock reasons (a taken choice, a choice
    // from outside the career) show nothing.
    private static string BuildLockReasonText(ChoiceLockReason reason, int tierUnlockLevel)
    {
        switch (reason)
        {
            case ChoiceLockReason.TierLocked:
                return new TextObject("{=taom_career_tier_requirement}Requires Level {LEVEL}")
                    .SetTextVariable("LEVEL", tierUnlockLevel).ToString();
            case ChoiceLockReason.ForkFull:
                return new TextObject("{=taom_career_lock_fork}You took another choice on this path.").ToString();
            case ChoiceLockReason.KeystoneLocked:
                return new TextObject("{=taom_career_lock_keystone}You took another keystone in this tier.").ToString();
            case ChoiceLockReason.NoPoints:
                return new TextObject("{=taom_career_lock_points}No free career points.").ToString();
            case ChoiceLockReason.LockedIn:
                return new TextObject("{=taom_career_lock_locked_in}Locked in. Retrain to change this choice.").ToString();
            default:
                return string.Empty;
        }
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

    /// <summary>
    /// Issue #388 — the diamond is the whole click target, so one command has to serve both
    /// directions: take an untaken choice, refund a taken one. (The old screen had a separate
    /// +/- button column per group; the diamond grid has no room for it and image-3 parity
    /// has none.) Both halves already gate themselves by asking CareerChoiceRules: TrySelectChoice
    /// enforces free points, tier locks, fork limits and keystone exclusivity; TryDeselectChoice
    /// no-ops on an untaken id and refuses a fork pick taken on an earlier visit.
    /// </summary>
    public void ExecuteToggleChoice()
    {
        if (IsTaken) DeSelectChoice();
        else SelectChoice();
    }

    [DataSourceProperty]
    public string Name => new TextObject(_choice.Id).ToString();

    [DataSourceProperty]
    public string Description => new TextObject(_choice.Description).ToString();

    // Issue #388 — the diamond icon. The per-choice sprites authored in taom_career_choices.xml
    // (career_choice_*) were never drawn (zero PNGs, zero atlas entries), which is why this
    // property was dead data bound by no prefab. It now resolves to an already-baked banner
    // icon: keystones show their career's own sigil (#380's keystone_icon), passives show an
    // icon for their effect type.
    [DataSourceProperty]
    public string IconSprite
    {
        get
        {
            if (IsKeystone && !string.IsNullOrEmpty(_keystoneIconSprite))
                return _keystoneIconSprite;
            if (_choice.Passives.Count > 0)
                return CareerEffectDisplayMap.IconFor(_choice.Passives[0].EffectType);
            return _keystoneIconSprite ?? "";
        }
    }

    [DataSourceProperty]
    public bool IsKeystone => _choice.Type == ChoiceType.Keystone;

    // Issue #380 — the career's keystone medallion glyph (a banner-icon id, which doubles
    // as its bare-number sprite name). Career-level, so every keystone node of one career
    // shows the same glyph. Empty when the career has no keystone_icon authored — the
    // medallion simply doesn't render (no fallback by design).
    [DataSourceProperty]
    public string KeystoneIconSprite => _keystoneIconSprite;

    [DataSourceProperty]
    public bool HasKeystoneIcon => IsKeystone && !string.IsNullOrEmpty(_keystoneIconSprite);

    // Effect-scope badge shown next to each keystone bullet so the player can distinguish
    // always-active passives from effects that fire only while the career ability is active.
    //
    // PassiveEffect choices flow through CareerPassiveService and are read by GameModel overrides
    // on every relevant calculation -- always active. Mutation (Keystone) choices are applied to
    // a cloned AbilityTemplateData inside AbilityEffectExecutor.Execute on V-press; the clone is discarded
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
    // The prefab renders tinted copies of the diamond gated on IsTaken / IsFreeToTake /
    // IsUnavailable, plus the IsForkLocked ring over the dim one, so every slot always shows a
    // diamond (gold / brown / dim, red-rimmed when its fork is full) instead of a blank gap.
    [DataSourceProperty]
    public bool IsUnavailable => !_isTaken && !_isFreeToTake;

    // The fork-full ring: a sibling the player can no longer take. Only an untaken diamond is
    // refused; the taken pick of the same fork reads LockedIn through LockReasonText instead.
    [DataSourceProperty]
    public bool IsForkLocked => !_isTaken && _lockReason == ChoiceLockReason.ForkFull;

    [DataSourceProperty]
    public string LockReasonText => _lockReasonText;

    [DataSourceProperty]
    public bool HasLockReason => _lockReasonText.Length > 0;

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
