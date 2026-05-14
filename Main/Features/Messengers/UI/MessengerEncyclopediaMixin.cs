using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.ViewModels;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.Encyclopedia.Pages;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace TAOM.Features.Messengers.UI;

[ViewModelMixin("RefreshValues")]
internal sealed class MessengerEncyclopediaMixin : BaseViewModelMixin<EncyclopediaHeroPageVM>
{
    private readonly IMessengerService _service;
    private readonly IMessengerSettingsProvider _settings;

    // Cached hint instances — avoids HintViewModel + TextObject allocation on every encyclopedia refresh.
    private readonly HintViewModel _emptyHint = new HintViewModel();
    private readonly HintViewModel _hintTargetUnavailable;
    private readonly HintViewModel _hintMessengersDisabled;
    private readonly HintViewModel _hintSystemUnavailable;

    // Last-state tracker keys the rejection-reason hint by validation result so we don't allocate a fresh
    // HintViewModel + TextObject on every refresh when the same reason still applies.
    private Domain.MessengerValidationResult _lastValidationResult = Domain.MessengerValidationResult.Ok;
    private HintViewModel _lastReasonHint;
    private bool _isMessengerAvailable;
    private HintViewModel _sendMessengerHint;
    private string _sendMessengerActionName = "";

    public MessengerEncyclopediaMixin(EncyclopediaHeroPageVM viewModel) : base(viewModel)
    {
        _service = TAOM.IoC.Resolve<IMessengerService>();
        _settings = TAOM.IoC.Resolve<IMessengerSettingsProvider>();
        _hintTargetUnavailable = new HintViewModel(new TextObject("{=taom_messenger_target_unavailable}This person cannot be reached by messenger at this time."));
        _hintMessengersDisabled = new HintViewModel(new TextObject("{=taom_messenger_disabled}The messenger system is disabled in settings."));
        _hintSystemUnavailable = new HintViewModel(new TextObject("{=taom_messenger_not_available}Messenger system not available."));
        _sendMessengerHint = _emptyHint;
        _sendMessengerActionName = new TextObject("{=taom_send_messenger}Send Messenger").ToString();
    }

    public override void OnRefresh()
    {
        try
        {
            UpdateAvailability();
        }
        catch
        {
            // never let UI refresh throw
            IsMessengerAvailable = false;
        }
    }

    private void UpdateAvailability()
    {
        var hero = ResolveHero();
        if (hero == null)
        {
            IsMessengerAvailable = false;
            SendMessengerHint = _hintTargetUnavailable;
            return;
        }

        if (!_settings.EnableMessengers)
        {
            IsMessengerAvailable = false;
            SendMessengerHint = _hintMessengersDisabled;
            return;
        }

        var behavior = Campaign.Current?.GetCampaignBehavior<MessengerCampaignBehavior>();
        if (behavior == null)
        {
            IsMessengerAvailable = false;
            SendMessengerHint = _hintSystemUnavailable;
            return;
        }

        // Use the service result enum (not the TextObject form) to key the hint cache — same enum value
        // means same reason text, so we can reuse the previous HintViewModel.
        var validation = _service.CanSendMessenger(SnapshotForMixin(hero), Hero.MainHero?.Gold ?? 0);
        var canSend = validation == Domain.MessengerValidationResult.Ok;
        IsMessengerAvailable = canSend;

        if (canSend)
        {
            SendMessengerHint = _emptyHint;
            _lastValidationResult = Domain.MessengerValidationResult.Ok;
            return;
        }

        if (validation != _lastValidationResult || _lastReasonHint == null)
        {
            _lastValidationResult = validation;
            // Re-fetch the rich reason (with HERO_NAME / COST variables) only when the rejection class changes.
            behavior.CanSendMessenger(hero, out var reason);
            _lastReasonHint = new HintViewModel(reason);
        }
        SendMessengerHint = _lastReasonHint;
    }

    private static Domain.HeroSnapshot SnapshotForMixin(Hero hero)
    {
        if (hero == null) return null;
        var inPlayerParty = hero.PartyBelongedTo != null
                            && Hero.MainHero?.PartyBelongedTo != null
                            && hero.PartyBelongedTo == Hero.MainHero.PartyBelongedTo;
        var inMapEvent = hero.PartyBelongedTo?.MapEvent != null;
        return new Domain.HeroSnapshot(
            heroId: hero.StringId,
            isAlive: hero.IsAlive,
            isPrisoner: hero.IsPrisoner,
            isChild: hero.IsChild,
            isFugitive: hero.IsFugitive,
            isActive: hero.IsActive,
            isWanderer: hero.IsWanderer,
            isHumanPlayerCharacter: hero.IsHumanPlayerCharacter,
            isInPlayerParty: inPlayerParty,
            isInMapEvent: inMapEvent);
    }

    [DataSourceMethod]
    public void ExecuteSendMessenger()
    {
        var hero = ResolveHero();
        if (hero == null) return;

        var behavior = Campaign.Current?.GetCampaignBehavior<MessengerCampaignBehavior>();
        behavior?.SendMessenger(hero);
        OnRefresh();
    }

    [DataSourceProperty]
    public string SendMessengerActionName
    {
        get => _sendMessengerActionName;
        set
        {
            if (_sendMessengerActionName != value)
            {
                _sendMessengerActionName = value;
                // Phase 9b #166 P1 — was ViewModel?.OnPropertyChangedWithValue(...). That fires
                // the notification on the host EncyclopediaHeroPageVM, NOT on the mixin where
                // the [DataSourceProperty] bindings actually resolve. Result: bindings froze at
                // first construction; re-opening encyclopedia for different heroes never
                // refreshed the button state. gui-ui.md confirms: notifications must fire on `this`.
                OnPropertyChangedWithValue(value, nameof(SendMessengerActionName));
            }
        }
    }

    [DataSourceProperty]
    public bool IsMessengerAvailable
    {
        get => _isMessengerAvailable;
        set
        {
            if (_isMessengerAvailable != value)
            {
                _isMessengerAvailable = value;
                // Phase 9b #166 P1 — see SendMessengerActionName.
                OnPropertyChangedWithValue(value, nameof(IsMessengerAvailable));
            }
        }
    }

    [DataSourceProperty]
    public HintViewModel SendMessengerHint
    {
        get => _sendMessengerHint;
        set
        {
            if (_sendMessengerHint != value)
            {
                _sendMessengerHint = value;
                // Phase 9b #166 P1 — see SendMessengerActionName.
                OnPropertyChangedWithValue(value, nameof(SendMessengerHint));
            }
        }
    }

    private Hero ResolveHero()
    {
        if (ViewModel == null) return null;
        return ViewModel.Obj as Hero;
    }
}
