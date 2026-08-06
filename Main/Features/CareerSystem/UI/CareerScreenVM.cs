using System;
using System.Collections.Generic;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Features.CareerSystem.UI;

public class CareerScreenVM : ViewModel
{
    private readonly ICareerDataService _dataService;
    private readonly ICareerRegistry _registry;
    private readonly ICareerPassiveService _passiveService;
    private readonly ICareerConfigProvider _configProvider;
    // Optional: when supplied, tier availability becomes hybrid (level OR completed quest). Null in
    // unit tests / legacy callers → falls back to the registry's level-only gate.
    private readonly ICareerQuestService _questService;
    private readonly IModLogger _logger;
    private readonly string _heroStringId;
    private readonly int _heroLevel;
    private readonly Action _onClose;

    private string _screenTitle;
    private string _doneLbl;
    private string _careerName;
    private string _careerDescription;
    private string _careerPortraitSprite;
    private string _abilityName;
    private string _abilitySpriteName;
    private string _abilityLabel;
    private string _freeCareerPointsText;
    private string _tier1Label;
    private string _tier2Label;
    private string _tier3Label;
    private bool _tier2Locked;
    private bool _tier3Locked;
    private string _tier2RequirementText;
    private string _tier3RequirementText;
    private int _freeCareerPoints;
    private bool _hasCareer;
    private bool _hasAbilitySprite;
    private MBBindingList<CareerChoiceGroupObjectVM> _choiceGroupsTier1;
    private MBBindingList<CareerChoiceGroupObjectVM> _choiceGroupsTier2;
    private MBBindingList<CareerChoiceGroupObjectVM> _choiceGroupsTier3;
    private MBBindingList<CareerAbilityEffectVM> _abilityEffects;
    private MBBindingList<CareerAbilityEffectVM> _keystoneEffectLines;
    private MBBindingList<CareerAbilityEffectVM> _passiveEffectLines;

    // Switch-mode wiring -- when isSwitchMode is true the screen renders a list of eligible
    // alternative careers (current career excluded) instead of the normal-mode choice tree.
    // Selecting one invokes _onChooseSwitchTarget, which the screen wires to
    // ICareerSwitchService.SwitchCareer + CloseScreen.
    private readonly bool _isSwitchMode;
    private readonly ICareerHeroAdapter _heroAdapter;
    private readonly Action<string> _onChooseSwitchTarget;
    private MBBindingList<CareerSwitchTargetVM> _eligibleSwitchTargets;
    private string _switchModeSubtitle;
    private string _noTargetsMessage;

    public CareerScreenVM(
        ICareerDataService dataService,
        ICareerRegistry registry,
        ICareerPassiveService passiveService,
        ICareerConfigProvider configProvider,
        IModLogger logger,
        string heroStringId,
        int heroLevel,
        Action onClose,
        ICareerQuestService questService = null,
        bool isSwitchMode = false,
        ICareerHeroAdapter heroAdapter = null,
        Action<string> onChooseSwitchTarget = null)
    {
        _dataService = dataService;
        _registry = registry;
        _passiveService = passiveService;
        _configProvider = configProvider;
        _questService = questService;
        _logger = logger;
        _heroStringId = heroStringId;
        _heroLevel = heroLevel;
        _onClose = onClose;
        _isSwitchMode = isSwitchMode;
        _heroAdapter = heroAdapter;
        _onChooseSwitchTarget = onChooseSwitchTarget;

        _screenTitle = isSwitchMode
            ? new TextObject("{=taom_career_switch_title}Choose Your Path").ToString()
            : new TextObject("{=taom_career_screen_title}Career").ToString();
        _doneLbl = new TextObject("{=taom_career_done}Done").ToString();
        _abilityLabel = new TextObject("{=taom_career_ability_label}Career Ability").ToString();
        _tier1Label = new TextObject("{=taom_career_tier1}Tier 1").ToString();
        _tier2Label = new TextObject("{=taom_career_tier2}Tier 2").ToString();
        _tier3Label = new TextObject("{=taom_career_tier3}Tier 3").ToString();
        _switchModeSubtitle = new TextObject("{=taom_career_switch_subtitle}Eligible careers for your culture and standing.").ToString();
        _noTargetsMessage = new TextObject("{=taom_career_switch_no_targets}No eligible careers available for your culture and standing.").ToString();

        _choiceGroupsTier1 = new MBBindingList<CareerChoiceGroupObjectVM>();
        _choiceGroupsTier2 = new MBBindingList<CareerChoiceGroupObjectVM>();
        _choiceGroupsTier3 = new MBBindingList<CareerChoiceGroupObjectVM>();
        _abilityEffects = new MBBindingList<CareerAbilityEffectVM>();
        _keystoneEffectLines = new MBBindingList<CareerAbilityEffectVM>();
        _passiveEffectLines = new MBBindingList<CareerAbilityEffectVM>();
        _eligibleSwitchTargets = new MBBindingList<CareerSwitchTargetVM>();

        RefreshValues();
    }

    public override void RefreshValues()
    {
        base.RefreshValues();

        if (_isSwitchMode)
        {
            RebuildEligibleSwitchTargets();
            // HasCareer stays false in switch mode -- the normal-mode panels (portrait, perk
            // tree, ability rollup) gate on @IsNormalMode in the prefab, not @HasCareer.
            HasCareer = false;
            return;
        }

        var careerId = _dataService.GetCareerStringId(_heroStringId);
        if (string.IsNullOrEmpty(careerId))
        {
            HasCareer = false;
            return;
        }

        var career = _registry.GetCareer(careerId);
        if (career == null)
        {
            HasCareer = false;
            return;
        }

        HasCareer = true;
        CareerName = new TextObject(career.DisplayName).ToString();
        CareerDescription = new TextObject(career.Description).ToString();
        CareerPortraitSprite = $"CareerSystem\\Portraits\\{career.PortraitSprite}";

        // Tier headers show the career's per-tier RANK title (e.g. "Captain of Ithilien") when
        // authored, falling back to the generic "Tier N" label.
        Tier1Label = !string.IsNullOrEmpty(career.Rank1Name)
            ? new TextObject(career.Rank1Name).ToString()
            : new TextObject("{=taom_career_tier1}Tier 1").ToString();
        Tier2Label = !string.IsNullOrEmpty(career.Rank2Name)
            ? new TextObject(career.Rank2Name).ToString()
            : new TextObject("{=taom_career_tier2}Tier 2").ToString();
        Tier3Label = !string.IsNullOrEmpty(career.Rank3Name)
            ? new TextObject(career.Rank3Name).ToString()
            : new TextObject("{=taom_career_tier3}Tier 3").ToString();

        var abilityTemplate = _configProvider?.GetAbilityTemplate(career.AbilityTemplateId);
        AbilityName = abilityTemplate != null
            ? new TextObject(abilityTemplate.DisplayName).ToString()
            : new TextObject(career.AbilityTemplateId).ToString();
        AbilitySpriteName = $"CareerSystem\\Abilities\\{career.AbilityTemplateId}";
        HasAbilitySprite = !string.IsNullOrEmpty(career.AbilityTemplateId);

        FreeCareerPoints = _registry.GetUnspentPoints(_heroLevel, _dataService.GetChoiceCount(_heroStringId));
        FreeCareerPointsText = new TextObject("{=taom_career_free_points}Free Points: {COUNT}")
            .SetTextVariable("COUNT", FreeCareerPoints).ToString();

        Tier2Locked = !IsTierAvail(2);
        Tier3Locked = !IsTierAvail(3);

        // Locked tiers show a "Requires Level N" label (level sourced from the registry, the single
        // source of truth) in place of the old stretched gate art.
        Tier2RequirementText = new TextObject("{=taom_career_tier_requirement}Requires Level {LEVEL}")
            .SetTextVariable("LEVEL", _registry.GetTierUnlockLevel(2)).ToString();
        Tier3RequirementText = new TextObject("{=taom_career_tier_requirement}Requires Level {LEVEL}")
            .SetTextVariable("LEVEL", _registry.GetTierUnlockLevel(3)).ToString();

        RebuildAbilityEffects(career);
        RebuildChoiceGroups(career);
    }

    private void RebuildEligibleSwitchTargets()
    {
        _eligibleSwitchTargets.Clear();
        if (_heroAdapter == null)
        {
            _logger?.LogWarning($"CareerSystem: RebuildEligibleSwitchTargets — heroAdapter is null for hero '{_heroStringId}'; rendering empty picker");
            NotifySwitchTargetsChanged();
            return;
        }

        var currentCareerId = _dataService.GetCareerStringId(_heroStringId);
        var targets = _registry.GetEligibleSwitchTargets(currentCareerId, _heroAdapter);
        foreach (var career in targets)
        {
            var abilityTemplate = _configProvider?.GetAbilityTemplate(career.AbilityTemplateId);
            _eligibleSwitchTargets.Add(new CareerSwitchTargetVM(career, abilityTemplate, OnChooseSwitchTarget));
        }

        if (_eligibleSwitchTargets.Count == 0)
            _logger?.LogWarning($"CareerSystem: RebuildEligibleSwitchTargets — no eligible targets for hero '{_heroStringId}' currentCareer='{currentCareerId}' (dialogue gate should have hidden this; possible static-entry-point or stale eligibility)");

        // IsBrowsingTargets and HasNoSwitchTargets are computed bools that depend on the
        // collection count. Gauntlet doesn't re-evaluate computed properties on collection
        // mutation -- it only listens to OnPropertyChanged events. Without these notifies the
        // prefab's @IsBrowsingTargets / @HasNoSwitchTargets gates stay stale.
        NotifySwitchTargetsChanged();
    }

    private void NotifySwitchTargetsChanged()
    {
        OnPropertyChanged(nameof(IsBrowsingTargets));
        OnPropertyChanged(nameof(HasNoSwitchTargets));
    }

    private void OnChooseSwitchTarget(string careerId)
    {
        _onChooseSwitchTarget?.Invoke(careerId);
    }

    private void RebuildAbilityEffects(CareerDefinition career)
    {
        _abilityEffects.Clear();

        var abilityTemplate = _configProvider?.GetAbilityTemplate(career.AbilityTemplateId);
        if (abilityTemplate != null && !string.IsNullOrEmpty(abilityTemplate.TooltipDescription))
        {
            var description = new TextObject(abilityTemplate.TooltipDescription).ToString();
            _abilityEffects.Add(new CareerAbilityEffectVM(description));
        }
    }

    // Issue #388 — publish the Active Effects panel from the taken choices RebuildChoiceGroups
    // already walked. Keystone lines are the taken keystones' own descriptions (what changes
    // about the ability); passive lines are every taken passive SUMMED per effect type, so two
    // +5% Damage picks read as one "+10% damage" rather than two lines the player adds up.
    //
    // Fed by the existing group walk rather than a second pass of its own: the screen rebuilds
    // on every click, and a duplicate registry walk per rebuild is pure waste (it also broke
    // ClickIncrease_RebuildsGroupsOnce_NotTwice, which counts registry queries to prove exactly
    // one rebuild ran).
    private void PublishActiveEffects(
        List<string> keystoneDescriptions,
        Dictionary<Domain.PassiveEffectType, float> passiveTotals)
    {
        _keystoneEffectLines.Clear();
        _passiveEffectLines.Clear();

        foreach (var description in keystoneDescriptions)
            _keystoneEffectLines.Add(new CareerAbilityEffectVM(new TextObject(description).ToString()));

        foreach (var kvp in passiveTotals)
        {
            var line = CareerEffectDisplayMap.Format(kvp.Key, kvp.Value);
            if (!string.IsNullOrEmpty(line))
                _passiveEffectLines.Add(new CareerAbilityEffectVM(line));
        }
    }

    // Hybrid tier gate: a tier is available when the hero meets the level threshold OR has completed
    // that tier's career quest. Falls back to the registry's level-only gate when no quest service
    // was supplied (unit tests / legacy callers).
    private bool IsTierAvail(int tier)
        => _questService != null
            ? _questService.IsTierUnlocked(_heroLevel, tier, _heroStringId)
            : _registry.IsTierAvailable(_heroLevel, tier);

    private void RebuildChoiceGroups(CareerDefinition career)
    {
        // Selecting or deselecting a choice refreshes the screen, which replaces every group VM.
        // Fresh VMs start with ButtonsVisible=false and Gauntlet fires no new HoverBegin for a
        // widget recreated under a stationary cursor, so without carrying the open state over,
        // the +/- strip collapsed on every click.
        var openGroupIds = new HashSet<string>();
        CollectOpenGroupIds(_choiceGroupsTier1, openGroupIds);
        CollectOpenGroupIds(_choiceGroupsTier2, openGroupIds);
        CollectOpenGroupIds(_choiceGroupsTier3, openGroupIds);

        // Issue #388 — filled by the choice walk below, published once at the end.
        var takenKeystoneDescriptions = new List<string>();
        var takenPassiveTotals = new Dictionary<Domain.PassiveEffectType, float>();

        _choiceGroupsTier1.Clear();
        _choiceGroupsTier2.Clear();
        _choiceGroupsTier3.Clear();

        foreach (var groupId in career.ChoiceGroupIds)
        {
            var group = _registry.GetGroup(groupId);
            if (group == null) continue;

            var isLocked = !IsTierAvail(group.Tier);
            var groupVM = new CareerChoiceGroupObjectVM(group, isLocked)
            {
                ButtonsVisible = openGroupIds.Contains(group.Id)
            };

            var choices = _registry.GetChoicesForGroup(groupId);
            foreach (var choice in choices)
            {
                var heroData = _dataService.GetOrCreateData(_heroStringId);
                var isTaken = heroData.HasChoice(choice.Id);

                // Issue #388 — accumulate the Active Effects panel from this same walk.
                if (isTaken)
                {
                    if (choice.Type == Domain.ChoiceType.Keystone)
                    {
                        if (!string.IsNullOrEmpty(choice.Description))
                            takenKeystoneDescriptions.Add(choice.Description);
                    }
                    else if (choice.Passive != null)
                    {
                        takenPassiveTotals.TryGetValue(choice.Passive.EffectType, out var running);
                        takenPassiveTotals[choice.Passive.EffectType] = running + choice.Passive.Magnitude;
                    }
                }
                // Issue #381 — a keystone closed by exclusivity dims (pip + no + button)
                // instead of silently rejecting the click at select time.
                var isFreeToTake = FreeCareerPoints > 0 && !isLocked
                    && !KeystoneExclusivityRule.IsLocked(career, choice, _registry, heroData);
                groupVM.Choices.Add(new CareerChoiceObjectVM(choice, isTaken, isFreeToTake, TrySelectChoice, TryDeselectChoice, career.KeystoneIcon));
            }

            switch (group.Tier)
            {
                case 1: _choiceGroupsTier1.Add(groupVM); break;
                case 2: _choiceGroupsTier2.Add(groupVM); break;
                case 3: _choiceGroupsTier3.Add(groupVM); break;
            }
        }

        PublishActiveEffects(takenKeystoneDescriptions, takenPassiveTotals);
    }

    private static void CollectOpenGroupIds(
        MBBindingList<CareerChoiceGroupObjectVM> groups, HashSet<string> openGroupIds)
    {
        foreach (var groupVM in groups)
        {
            if (groupVM.ButtonsVisible)
                openGroupIds.Add(groupVM.GroupId);
        }
    }

    public void ExecuteSelectChoice(string choiceId)
    {
        _logger?.LogInfo($"CareerSystem: ExecuteSelectChoice — choiceId='{choiceId}' freePoints={FreeCareerPoints}");
        if (FreeCareerPoints <= 0) return;

        var choice = _registry.GetChoice(choiceId);
        if (choice == null) return;

        if (!string.IsNullOrEmpty(choice.GroupId))
        {
            var group = _registry.GetGroup(choice.GroupId);
            if (group != null && !IsTierAvail(group.Tier))
                return;

            // Issue #381 — one keystone per tier, tier-3-completion exemption, grandfathered.
            if (KeystoneExclusivityRule.IsLocked(CurrentCareer(), choice, _registry,
                    _dataService.GetOrCreateData(_heroStringId)))
                return;
        }

        var maxChoices = _registry.GetMaxChoicesForHero(_heroLevel);
        if (_dataService.TryAddChoice(_heroStringId, choiceId, maxChoices))
        {
            _passiveService.RefreshCache(_dataService, _registry);
            RefreshValues();
        }
    }

    private Domain.CareerDefinition CurrentCareer()
    {
        var careerId = _dataService.GetCareerStringId(_heroStringId);
        return careerId != null ? _registry.GetCareer(careerId) : null;
    }

    // Invoked by CareerChoiceObjectVM.SelectChoice via callback (+ button in UI)
    private bool TrySelectChoice(string choiceId)
    {
        if (FreeCareerPoints <= 0) return false;

        var choice = _registry.GetChoice(choiceId);
        if (choice == null) return false;

        if (!string.IsNullOrEmpty(choice.GroupId))
        {
            var group = _registry.GetGroup(choice.GroupId);
            if (group != null && !IsTierAvail(group.Tier))
                return false;

            // Issue #381 — one keystone per tier, tier-3-completion exemption, grandfathered.
            if (KeystoneExclusivityRule.IsLocked(CurrentCareer(), choice, _registry,
                    _dataService.GetOrCreateData(_heroStringId)))
                return false;
        }

        var maxChoices = _registry.GetMaxChoicesForHero(_heroLevel);
        if (_dataService.TryAddChoice(_heroStringId, choiceId, maxChoices))
        {
            _passiveService.RefreshCache(_dataService, _registry);
            RefreshValues();
            return true;
        }
        return false;
    }

    // Invoked by CareerChoiceObjectVM.DeSelectChoice via callback (- button in UI)
    private bool TryDeselectChoice(string choiceId)
    {
        var heroData = _dataService.GetOrCreateData(_heroStringId);
        if (!heroData.HasChoice(choiceId)) return false;

        _dataService.RemoveChoice(_heroStringId, choiceId);
        _passiveService.RefreshCache(_dataService, _registry);
        RefreshValues();
        return true;
    }

    public void ExecuteClose()
    {
        _onClose?.Invoke();
    }

    // ── DataSource Properties ──

    [DataSourceProperty]
    public string ScreenTitle
    {
        get => _screenTitle;
        set { if (_screenTitle != value) { _screenTitle = value; OnPropertyChangedWithValue(value, nameof(ScreenTitle)); } }
    }

    [DataSourceProperty]
    public string DoneLbl
    {
        get => _doneLbl;
        set { if (_doneLbl != value) { _doneLbl = value; OnPropertyChangedWithValue(value, nameof(DoneLbl)); } }
    }

    [DataSourceProperty]
    public bool HasCareer
    {
        get => _hasCareer;
        set { if (_hasCareer != value) { _hasCareer = value; OnPropertyChangedWithValue(value, nameof(HasCareer)); } }
    }

    [DataSourceProperty]
    public string CareerName
    {
        get => _careerName;
        set { if (_careerName != value) { _careerName = value; OnPropertyChangedWithValue(value, nameof(CareerName)); } }
    }

    [DataSourceProperty]
    public string CareerDescription
    {
        get => _careerDescription;
        set { if (_careerDescription != value) { _careerDescription = value; OnPropertyChangedWithValue(value, nameof(CareerDescription)); } }
    }

    [DataSourceProperty]
    public string CareerPortraitSprite
    {
        get => _careerPortraitSprite;
        set { if (_careerPortraitSprite != value) { _careerPortraitSprite = value; OnPropertyChangedWithValue(value, nameof(CareerPortraitSprite)); } }
    }

    [DataSourceProperty]
    public string AbilityName
    {
        get => _abilityName;
        set { if (_abilityName != value) { _abilityName = value; OnPropertyChangedWithValue(value, nameof(AbilityName)); } }
    }

    [DataSourceProperty]
    public string AbilitySpriteName
    {
        get => _abilitySpriteName;
        set { if (_abilitySpriteName != value) { _abilitySpriteName = value; OnPropertyChangedWithValue(value, nameof(AbilitySpriteName)); } }
    }

    [DataSourceProperty]
    public string AbilityLabel
    {
        get => _abilityLabel;
        set { if (_abilityLabel != value) { _abilityLabel = value; OnPropertyChangedWithValue(value, nameof(AbilityLabel)); } }
    }

    [DataSourceProperty]
    public bool HasAbilitySprite
    {
        get => _hasAbilitySprite;
        set { if (_hasAbilitySprite != value) { _hasAbilitySprite = value; OnPropertyChangedWithValue(value, nameof(HasAbilitySprite)); } }
    }

    [DataSourceProperty]
    public int FreeCareerPoints
    {
        get => _freeCareerPoints;
        set { if (_freeCareerPoints != value) { _freeCareerPoints = value; OnPropertyChangedWithValue(value, nameof(FreeCareerPoints)); } }
    }

    [DataSourceProperty]
    public string FreeCareerPointsText
    {
        get => _freeCareerPointsText;
        set { if (_freeCareerPointsText != value) { _freeCareerPointsText = value; OnPropertyChangedWithValue(value, nameof(FreeCareerPointsText)); } }
    }

    [DataSourceProperty]
    public string Tier1Label
    {
        get => _tier1Label;
        set { if (_tier1Label != value) { _tier1Label = value; OnPropertyChangedWithValue(value, nameof(Tier1Label)); } }
    }

    [DataSourceProperty]
    public string Tier2Label
    {
        get => _tier2Label;
        set { if (_tier2Label != value) { _tier2Label = value; OnPropertyChangedWithValue(value, nameof(Tier2Label)); } }
    }

    [DataSourceProperty]
    public string Tier3Label
    {
        get => _tier3Label;
        set { if (_tier3Label != value) { _tier3Label = value; OnPropertyChangedWithValue(value, nameof(Tier3Label)); } }
    }

    [DataSourceProperty]
    public bool Tier2Locked
    {
        get => _tier2Locked;
        set { if (_tier2Locked != value) { _tier2Locked = value; OnPropertyChangedWithValue(value, nameof(Tier2Locked)); } }
    }

    [DataSourceProperty]
    public bool Tier3Locked
    {
        get => _tier3Locked;
        set { if (_tier3Locked != value) { _tier3Locked = value; OnPropertyChangedWithValue(value, nameof(Tier3Locked)); } }
    }

    [DataSourceProperty]
    public string Tier2RequirementText
    {
        get => _tier2RequirementText;
        set { if (_tier2RequirementText != value) { _tier2RequirementText = value; OnPropertyChangedWithValue(value, nameof(Tier2RequirementText)); } }
    }

    [DataSourceProperty]
    public string Tier3RequirementText
    {
        get => _tier3RequirementText;
        set { if (_tier3RequirementText != value) { _tier3RequirementText = value; OnPropertyChangedWithValue(value, nameof(Tier3RequirementText)); } }
    }

    [DataSourceProperty]
    public MBBindingList<CareerChoiceGroupObjectVM> ChoiceGroupsTier1
    {
        get => _choiceGroupsTier1;
        set { if (_choiceGroupsTier1 != value) { _choiceGroupsTier1 = value; OnPropertyChangedWithValue(value, nameof(ChoiceGroupsTier1)); } }
    }

    [DataSourceProperty]
    public MBBindingList<CareerChoiceGroupObjectVM> ChoiceGroupsTier2
    {
        get => _choiceGroupsTier2;
        set { if (_choiceGroupsTier2 != value) { _choiceGroupsTier2 = value; OnPropertyChangedWithValue(value, nameof(ChoiceGroupsTier2)); } }
    }

    [DataSourceProperty]
    public MBBindingList<CareerChoiceGroupObjectVM> ChoiceGroupsTier3
    {
        get => _choiceGroupsTier3;
        set { if (_choiceGroupsTier3 != value) { _choiceGroupsTier3 = value; OnPropertyChangedWithValue(value, nameof(ChoiceGroupsTier3)); } }
    }

    /// <summary>Issue #388 — header above the Active Effects panel.</summary>
    [DataSourceProperty]
    public string ActiveEffectsLabel =>
        new TextObject("{=taom_career_active_effects}Active Effects").ToString();

    /// <summary>Issue #388 — taken keystones' descriptions, shown gold in the Active Effects panel.</summary>
    [DataSourceProperty]
    public MBBindingList<CareerAbilityEffectVM> KeystoneEffectLines
    {
        get => _keystoneEffectLines;
        set
        {
            if (_keystoneEffectLines != value)
            {
                _keystoneEffectLines = value;
                OnPropertyChangedWithValue(value, nameof(KeystoneEffectLines));
            }
        }
    }

    /// <summary>Issue #388 — taken passives summed per effect type, e.g. "+20% damage".</summary>
    [DataSourceProperty]
    public MBBindingList<CareerAbilityEffectVM> PassiveEffectLines
    {
        get => _passiveEffectLines;
        set
        {
            if (_passiveEffectLines != value)
            {
                _passiveEffectLines = value;
                OnPropertyChangedWithValue(value, nameof(PassiveEffectLines));
            }
        }
    }

    [DataSourceProperty]
    public MBBindingList<CareerAbilityEffectVM> AbilityEffects
    {
        get => _abilityEffects;
        set { if (_abilityEffects != value) { _abilityEffects = value; OnPropertyChangedWithValue(value, nameof(AbilityEffects)); } }
    }

    // ── Switch-mode bindings ──
    //
    // The prefab gates the normal-mode (perk tree, ability rollup, portrait) panels with
    // @IsNormalMode and the picker panel with @IsBrowsingTargets. The two are derived from
    // _isSwitchMode so it stays a single source of truth.

    [DataSourceProperty]
    public bool IsSwitchMode => _isSwitchMode;

    [DataSourceProperty]
    public bool IsNormalMode => !_isSwitchMode;

    [DataSourceProperty]
    public bool IsBrowsingTargets => _isSwitchMode && _eligibleSwitchTargets != null && _eligibleSwitchTargets.Count > 0;

    // Inverse of IsBrowsingTargets while in switch mode -- drives the empty-state TextWidget
    // in the prefab (shown when the dialogue gate is bypassed, the heroAdapter is null, or
    // GetEligibleSwitchTargets returns no rows). Codex Review #32.
    [DataSourceProperty]
    public bool HasNoSwitchTargets => _isSwitchMode && (_eligibleSwitchTargets == null || _eligibleSwitchTargets.Count == 0);

    [DataSourceProperty]
    public MBBindingList<CareerSwitchTargetVM> EligibleSwitchTargets
    {
        get => _eligibleSwitchTargets;
        set { if (_eligibleSwitchTargets != value) { _eligibleSwitchTargets = value; OnPropertyChangedWithValue(value, nameof(EligibleSwitchTargets)); } }
    }

    [DataSourceProperty]
    public string SwitchModeSubtitle
    {
        get => _switchModeSubtitle;
        set { if (_switchModeSubtitle != value) { _switchModeSubtitle = value; OnPropertyChangedWithValue(value, nameof(SwitchModeSubtitle)); } }
    }

    [DataSourceProperty]
    public string NoTargetsMessage
    {
        get => _noTargetsMessage;
        set { if (_noTargetsMessage != value) { _noTargetsMessage = value; OnPropertyChangedWithValue(value, nameof(NoTargetsMessage)); } }
    }
}
