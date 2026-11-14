using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TAOM.Adapters;
using TAOM.Features.StoryMissions.Domain;
using TAOM.Features.WarChronicle.Events;

namespace TAOM.Features.StoryMissions.Hooks;

/// <summary>
/// The story offer's entry point (#1105 slice 4): on a free map it shows a two-button popup for the first open story window, then the menu
/// <c>taom_story_offer</c>, whose "Defend" option opens the mission (a <c>LeaveType.Mission</c> option, as the dev menu's). The engine returns
/// to this menu after the mission and runs its init again (<c>MapState.OnActivate</c>, <c>MenuContext.Refresh</c>); that init applies the
/// verdict through <see cref="StoryOfferService"/> once, before the player can save, and "Continue" only leaves. Built new for every
/// campaign, so its pending offer and verdict start clean, and its constructor resets the service's session state.
/// </summary>
public sealed class StoryOfferBehavior : CampaignBehaviorBase
{
    internal const string MenuId = "taom_story_offer";
    private const string BodyVariable = "TAOM_STORY_OFFER";
    private const int TicksBetweenChecks = 30;

    private readonly StoryOfferService _service;
    private readonly StoryMissionLauncher _launcher;
    private readonly IGameMenuAdapter _menus;
    private ChronicleStoryOffer? _offer;
    private StoryOutcome? _verdict;
    private StoryResult? _result;
    private int _ticks;

    internal StoryOfferBehavior(StoryOfferService service, StoryMissionLauncher launcher, IGameMenuAdapter menus)
    {
        _service = service;
        _launcher = launcher;
        _menus = menus;
        _service.ResetForNewSession();
    }

    public override void RegisterEvents()
    {
        CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, AddMenu);
        CampaignEvents.TickEvent.AddNonSerializedListener(this, OnTick);
        CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, _service.NewDay);
    }

    public override void SyncData(IDataStore dataStore)
    {
        // Nothing: a declined offer is the chronicle's saved state.
    }

    private void OnTick(float dt)
    {
        if (++_ticks < TicksBetweenChecks)
            return;

        _ticks = 0;
        if (!MapPopupGate.IsMapFree())
            return;

        // A free map with an offer pending: its popup closed with no answer (HideInquiry), or "Ride" found the map busy. Ask again tomorrow.
        if (_offer != null)
        {
            Later();
            return;
        }

        if (!(_service.NextOffer() is { } offer))
            return;

        _offer = offer;
        _service.NoteShown(offer);
        InformationManager.ShowInquiry(new InquiryData(
            Text(offer.Title, offer).ToString(),
            Text("{=taom_story_offer_body}The hour of {TARGET} has come. Will you ride to its walls and fight the battle yourself, or let fate decide?", offer).ToString(),
            true, true,
            Text("{=taom_story_offer_ride}Ride to {TARGET}", offer).ToString(),
            new TextObject("{=taom_story_offer_later}Not now").ToString(),
            () => { if (MapPopupGate.IsMapFreeUnderPopup()) _menus.EnsureMenuOpen(MenuId); }, // the rest of this tick can open an encounter
            Later), true, false);
    }

    private void AddMenu(CampaignGameStarter starter)
    {
        starter.AddGameMenu(MenuId, "{=!}{" + BodyVariable + "}", OnInit, GameMenu.MenuOverlayType.None);
        starter.AddGameMenuOption(MenuId, "taom_story_offer_defend", "{=taom_story_offer_defend}Defend the walls", DefendCondition, Defend, isLeave: false, index: 0);
        starter.AddGameMenuOption(MenuId, "taom_story_offer_decline", "{=taom_story_offer_fate}Let fate decide", OfferCondition, _ => { Decline(); _menus.ExitToLast(); }, isLeave: false, index: 1);
        starter.AddGameMenuOption(MenuId, "taom_story_offer_continue", "{=taom_story_offer_continue}Continue", ContinueCondition, Continue, isLeave: true, index: 2);
    }

    private void OnInit(MenuCallbackArgs args)
    {
        // A behavior saves nothing, so a verdict still pending at a save would be lost: apply it on the first init after the mission.
        if (_offer != null && _verdict is { } verdict && _result == null)
            _result = _service.Apply(_offer, verdict);
        MBTextManager.SetTextVariable(BodyVariable, Text(StoryOfferService.MenuText(_offer != null, _result), _offer), false);
    }

    private bool OfferCondition(MenuCallbackArgs args)
    {
        args.optionLeaveType = GameMenuOption.LeaveType.Leave;
        return _offer != null && _verdict == null;
    }

    private bool DefendCondition(MenuCallbackArgs args)
    {
        var shown = OfferCondition(args);
        args.optionLeaveType = GameMenuOption.LeaveType.Mission;
        return shown;
    }

    private bool ContinueCondition(MenuCallbackArgs args)
    {
        args.optionLeaveType = GameMenuOption.LeaveType.Continue;
        return _offer == null || _verdict != null;
    }

    private void Defend(MenuCallbackArgs args)
    {
        if (_offer != null && !_launcher.Launch(_offer.MissionId, false, verdict => _verdict = verdict))
        {
            InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=taom_story_offer_failed}The story battle could not open. The taom log says why.").ToString(), Colors.Red));
            _service.Snooze(_offer.EventId);
            Clear();
            _menus.ExitToLast();
        }
    }

    private void Continue(MenuCallbackArgs args)
    {
        Clear();
        _menus.ExitToLast();
    }

    private void Clear()
    {
        _offer = null;
        _verdict = null;
        _result = null;
    }

    // "Not now", or Escape on the popup: a snooze, never the saved decline.
    private void Later()
    {
        if (_offer != null)
            _service.Snooze(_offer.EventId);
        Clear();
    }

    private void Decline()
    {
        if (_offer != null)
            _service.Decline(_offer);
        Clear();
    }

    private static TextObject Text(string text, ChronicleStoryOffer? offer)
    {
        var result = new TextObject(text);
        var target = offer?.TargetSettlementId == null ? null : Settlement.Find(offer.TargetSettlementId);
        result.SetTextVariable("TARGET", target?.Name ?? TextObject.GetEmpty());
        return result;
    }
}
