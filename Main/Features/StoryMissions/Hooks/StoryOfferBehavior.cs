using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TAOM.Adapters;
using TAOM.Features.StoryMissions.Domain;

namespace TAOM.Features.StoryMissions.Hooks;

/// <summary>
/// The story offer's entry point (#1105 slice 4): on a free map it shows a two-button popup for the first open story window, then the menu
/// <c>taom_story_offer</c>, whose "Defend" and "Storm" options open the mission for that side (<c>LeaveType.Mission</c> options, as the
/// dev menu's); each shows only when the player may take that side (#1326). The engine returns to this menu after the mission and runs
/// its init again (<c>MapState.OnActivate</c>, <c>MenuContext.Refresh</c>); that init applies the verdict through
/// <see cref="StoryOfferService"/> once, before the player can save, and "Continue" only leaves. Built new for every campaign, so its
/// pending offer and verdict start clean, and its constructor resets the service's session state. It saves nothing: a declined offer is the
/// chronicle's saved state. A tick that finds an offer still pending on a free map snoozes it until tomorrow (its popup closed with no answer,
/// or "Ride" found the map busy). The accept button rechecks the map, because the rest of the tick that raised the popup can open an encounter.
/// The unattended scenario run (<c>ScenarioAutorunDriver</c>, kind story-offer) reads <see cref="Pending"/> and <see cref="Verdict"/> and sets
/// <see cref="BotPlaysNextMission"/> before it clicks a side; a person's game never sets it.
/// </summary>
public sealed class StoryOfferBehavior : CampaignBehaviorBase
{
    internal const string MenuId = "taom_story_offer";
    private const string BodyVariable = "TAOM_STORY_OFFER";
    private const int TicksBetweenChecks = 30;

    private readonly StoryOfferService _service;
    private readonly StoryMissionLauncher _launcher;
    private readonly IGameMenuAdapter _menus;
    private StoryOffer? _offer;
    private StorySide _side;
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

    internal bool BotPlaysNextMission { get; set; }
    internal StoryOffer? Pending => _offer;
    internal StoryOutcome? Verdict => _verdict;

    public override void RegisterEvents()
    {
        CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, AddMenu);
        CampaignEvents.TickEvent.AddNonSerializedListener(this, OnTick);
        CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, _service.NewDay);
    }

    public override void SyncData(IDataStore dataStore) { }

    private void OnTick(float dt)
    {
        if (++_ticks < TicksBetweenChecks)
            return;

        _ticks = 0;
        if (!MapPopupGate.IsMapFree())
            return;

        if (_offer != null)
        {
            Later();
            return;
        }

        if (!(_service.NextOffer() is { } offer))
            return;

        _offer = offer;
        _service.NoteShown(offer);
        StoryOfferPopup.Show(offer, () => { if (MapPopupGate.IsMapFreeUnderPopup()) _menus.EnsureMenuOpen(MenuId); }, Later);
    }

    private void AddMenu(CampaignGameStarter starter)
    {
        starter.AddGameMenu(MenuId, "{=!}{" + BodyVariable + "}", OnInit, GameMenu.MenuOverlayType.None);
        starter.AddGameMenuOption(MenuId, "taom_story_offer_defend", "{=taom_story_offer_defend}Defend the walls",
            a => SideCondition(a, StorySide.Defending, hero: false), _ => Launch(StorySide.Defending), isLeave: false, index: 0);
        starter.AddGameMenuOption(MenuId, "taom_story_offer_attack", "{=taom_story_offer_attack}Storm the walls",
            a => SideCondition(a, StorySide.Attacking, hero: false), _ => Launch(StorySide.Attacking), isLeave: false, index: 1);
        starter.AddGameMenuOption(MenuId, "taom_story_offer_stand", "{=taom_story_breaking_stand}Stand with the Fellowship",
            a => SideCondition(a, StorySide.Defending, hero: true), _ => Launch(StorySide.Defending), isLeave: false, index: 2);
        starter.AddGameMenuOption(MenuId, "taom_story_offer_hunt", "{=taom_story_breaking_hunt}Lead the Uruk-hai",
            a => SideCondition(a, StorySide.Attacking, hero: true), _ => Launch(StorySide.Attacking), isLeave: false, index: 3);
        starter.AddGameMenuOption(MenuId, "taom_story_offer_decline", "{=taom_story_offer_fate}Let fate decide", OfferCondition, _ => { Decline(); _menus.ExitToLast(); }, isLeave: false, index: 4);
        starter.AddGameMenuOption(MenuId, "taom_story_offer_continue", "{=taom_story_offer_continue}Continue", ContinueCondition, Continue, isLeave: true, index: 5);
    }

    private void OnInit(MenuCallbackArgs args)
    {
        // A behavior saves nothing, so a verdict still pending at a save would be lost: apply it on the first init after the mission.
        if (_offer != null && _verdict is { } verdict && _result == null)
            _result = _service.Apply(_offer.Window, _side, verdict);
        MBTextManager.SetTextVariable(BodyVariable,
            StoryOfferPopup.Line(StoryOfferText.Menu(_offer?.Choices, _side, _result, _offer?.Window.HeroId != null), _offer?.Window), false);
    }

    private bool OfferCondition(MenuCallbackArgs args)
    {
        args.optionLeaveType = GameMenuOption.LeaveType.Leave;
        return _offer != null && _verdict == null;
    }

    // A side shows only while the offer is open and the player may take it (StorySideGate). A fortress window and a hero story each have their own two options.
    private bool SideCondition(MenuCallbackArgs args, StorySide side, bool hero)
    {
        var shown = OfferCondition(args) && (_offer!.Window.HeroId != null) == hero && (side == StorySide.Attacking ? _offer.Choices.Attack : _offer.Choices.Defend);
        args.optionLeaveType = GameMenuOption.LeaveType.Mission;
        return shown;
    }

    private bool ContinueCondition(MenuCallbackArgs args)
    {
        args.optionLeaveType = GameMenuOption.LeaveType.Continue;
        return _offer == null || _verdict != null;
    }

    private void Launch(StorySide side)
    {
        if (_offer == null)
            return;

        _side = side;
        var missionId = side == StorySide.Attacking ? _offer.Window.AttackMissionId : _offer.Window.MissionId;
        if (missionId == null || !_launcher.Launch(missionId, BotPlaysNextMission, verdict => _verdict = verdict))
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
        _side = StorySide.Defending;
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
            _service.Decline(_offer.Window);
        Clear();
    }
}
