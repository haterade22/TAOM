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
    private readonly IMessengerSettingsProvider _settings;

    // Fixed hints built once. The page refreshes when it opens and after a click, never per frame.
    private readonly HintViewModel _hintTargetUnavailable;
    private readonly HintViewModel _hintMessengersDisabled;
    private readonly HintViewModel _hintSystemUnavailable;

    private bool _isMessengerAvailable;
    private HintViewModel _sendMessengerHint;
    private string _sendMessengerActionName = "";

    public MessengerEncyclopediaMixin(EncyclopediaHeroPageVM viewModel) : base(viewModel)
    {
        _settings = TAOM.IoC.Resolve<IMessengerSettingsProvider>();
        _hintTargetUnavailable = new HintViewModel(new TextObject("{=taom_messenger_target_unavailable}This person cannot be reached by messenger at this time."));
        _hintMessengersDisabled = new HintViewModel(new TextObject("{=taom_messenger_disabled}The messenger system is disabled in settings."));
        _hintSystemUnavailable = new HintViewModel(new TextObject("{=taom_messenger_not_available}Messenger system not available."));
        _sendMessengerHint = new HintViewModel();
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
            // never let UI refresh throw; the disabled button's hint now shows (#944), so give it a reason
            IsMessengerAvailable = false;
            SendMessengerHint = _hintSystemUnavailable;
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

        // The compact icon keeps the quote and travel time or refusal together in its hover text.
        var offer = behavior.GetOffer(hero, inPerson: false);
        IsMessengerAvailable = offer.CanSend;

        var name = new TextObject("{=taom_send_messenger_cost}Send Messenger ({COST} denars)");
        name.SetTextVariable("COST", offer.Cost);
        SendMessengerActionName = name.ToString();

        if (offer.CanSend)
        {
            var days = new TextObject("{=taom_messenger_trip_days}The messenger will take about {DAYS} {?IS_PLURAL}days{?}day{\\?}.");
            days.SetTextVariable("DAYS", offer.Days);
            days.SetTextVariable("IS_PLURAL", offer.Days > 1 ? 1 : 0);
            SendMessengerHint = new HintViewModel(new TextObject(SendMessengerActionName + "\n" + days));
        }
        else
        {
            SendMessengerHint = new HintViewModel(new TextObject(SendMessengerActionName + "\n" + behavior.GetReason(offer, hero)));
        }
    }

    [DataSourceMethod]
    public void ExecuteSendMessenger()
    {
        var hero = ResolveHero();
        if (hero == null) return;

        var behavior = Campaign.Current?.GetCampaignBehavior<MessengerCampaignBehavior>();
        behavior?.SendMessenger(hero, inPerson: false);
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

    private Hero? ResolveHero()
    {
        if (ViewModel == null) return null;
        return ViewModel.Obj as Hero;
    }
}
