using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Locations;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using TAOM.Core.Logging;
using TAOM.Features.Messengers.Domain;

namespace TAOM.Features.Messengers;

// TAOM boundary class (ADR-002 / ADR-007). Touches sealed TaleWorlds types directly because that is what
// the boundary is for — engine-coupled concerns (mission listener, encounter routing, dialog tree, settlement
// vs field handling, position restore) cannot move into a non-boundary class without inventing a redundant
// abstraction layer. ALL pure-logic decisions delegate to IMessengerService (validation, accident roll,
// position math, speed calc). Save data flows through IMessengerStateStore (store owns serialization format).
// Codex review #34 flagged this as "fat boundary"; deep-review's standards agent independently confirmed
// the structure is compliant — the line count comes from genuine engine-coupled orchestration, not from
// hidden business logic.
public class MessengerCampaignBehavior : CampaignBehaviorBase, IMissionListener
{
    private readonly IMessengerService _service;
    private readonly IMessengerStateStore _store;
    private readonly IMessengerSettingsProvider _settings;
    private readonly IModLogger _logger;

    private static readonly MissionMode[] AllowedMissionModes = { MissionMode.Conversation, MissionMode.Barter };

    private bool _dialogsRegistered;
    private bool _processingArrivedMessenger;
    private PendingMessenger _activeMessenger;
    private Mission _currentMission;
    private Vec2 _originalPosition = Vec2.Invalid;
    private readonly List<string> _toRemoveScratch = new List<string>();
    // Behavior is registered Reuse.Singleton, so a single instance survives across campaigns within the same
    // Bannerlord process. The fields below are RESET when OnSessionLaunched sees a new starter (Codex review #34
    // 2026-05-06: prior session's _dialogsRegistered=true would have suppressed dialog registration in campaign 2).
    private CampaignGameStarter _lastSessionStarter;
    private bool _justLoadedFromSave;

    public MessengerCampaignBehavior(
        IMessengerService service,
        IMessengerStateStore store,
        IMessengerSettingsProvider settings,
        IModLogger logger)
    {
        _service = service;
        _store = store;
        _settings = settings;
        _logger = logger;
    }

    // --- CampaignBehaviorBase ---

    public override void RegisterEvents()
    {
        CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, OnHourlyTick);
        CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
    }

    public override void SyncData(IDataStore dataStore)
    {
        if (dataStore.IsSaving)
        {
            var snapshot = _store.Serialize();
            dataStore.SyncData("_taom_messengers", ref snapshot);
        }
        else
        {
            Dictionary<string, string> snapshot = null;
            dataStore.SyncData("_taom_messengers", ref snapshot);
            _store.Deserialize(snapshot);
            _processingArrivedMessenger = false;
            _activeMessenger = null;
            _currentMission = null;
            _originalPosition = Vec2.Invalid;
            _justLoadedFromSave = true;   // signals OnSessionLaunched to NOT clear the freshly-loaded store
        }
    }

    // --- Public API (callable by other features) ---

    public void SendMessenger(Hero targetHero)
    {
        // Codex review #34: runtime MCM toggle (Settings.Messengers.EnableMessengers OFF mid-game)
        // must short-circuit dispatch in addition to behavior registration.
        if (!_settings.EnableMessengers) return;

        var snapshot = SnapshotHero(targetHero);
        var playerGold = Hero.MainHero?.Gold ?? 0;
        var validation = _service.CanSendMessenger(snapshot, playerGold);
        if (validation != MessengerValidationResult.Ok)
        {
            ShowInquiry(
                new TextObject("{=taom_messenger_cannot_send}Cannot Send Messenger").ToString(),
                BuildValidationReason(validation, targetHero).ToString(),
                affirmative: GameTexts.FindText("str_ok").ToString());
            return;
        }

        GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, null, _settings.MessengerGoldCost, false);

        var startVec = Hero.MainHero?.GetMapPoint()?.Position.ToVec2() ?? Vec2.Zero;
        var messenger = new PendingMessenger(
            targetHeroId: targetHero.StringId,
            dispatchTimeDays: CampaignTime.Now.ToDays,
            position: ToMapCoord(startVec),
            arrived: false);
        _store.Add(messenger);

        var sentText = new TextObject("{=taom_messenger_sent}A messenger has been dispatched to {HERO_NAME} and will arrive within {DAYS} days.");
        sentText.SetTextVariable("HERO_NAME", targetHero.Name);
        sentText.SetTextVariable("DAYS", _settings.MessengerTravelDays);
        ShowInquiry(
            new TextObject("{=taom_messenger_sent_title}Messenger Sent").ToString(),
            sentText.ToString(),
            affirmative: GameTexts.FindText("str_ok").ToString());
    }

    public bool CanSendMessenger(Hero targetHero, out TextObject reason)
    {
        if (!_settings.EnableMessengers)
        {
            reason = new TextObject("{=taom_messenger_disabled}The messenger system is disabled in settings.");
            return false;
        }

        var validation = _service.CanSendMessenger(SnapshotHero(targetHero), Hero.MainHero?.Gold ?? 0);
        if (validation == MessengerValidationResult.Ok)
        {
            reason = TextObject.GetEmpty();
            return true;
        }
        reason = BuildValidationReason(validation, targetHero);
        return false;
    }

    // --- Lifecycle ---

    private void OnSessionLaunched(CampaignGameStarter starter)
    {
        // Singleton behavior + cross-campaign-in-same-process safety:
        //   * A new starter ⇒ reset all per-campaign instance state.
        //   * Clear the store only when this is a NEW campaign — `_justLoadedFromSave` flips true
        //     in SyncData(IsLoading=true), which fires before OnSessionLaunched. If it's set, the
        //     store has freshly-deserialized state we must keep.
        if (_lastSessionStarter != starter)
        {
            _lastSessionStarter = starter;
            _dialogsRegistered = false;
            _processingArrivedMessenger = false;
            _activeMessenger = null;
            _currentMission = null;
            _originalPosition = Vec2.Invalid;
            if (!_justLoadedFromSave)
                _store.Clear();
        }

        // Phase 9b #123 — `_justLoadedFromSave` MUST clear unconditionally, not inside the
        // `if (starter != _lastSessionStarter)` gate. Same-process save → load → save → load gives
        // the SAME starter on the 2nd load, so the gate is false and the flag stayed stuck-on,
        // misleading any future consumer of the flag.
        _justLoadedFromSave = false;

        if (!_dialogsRegistered)
        {
            AddDialogOptions(starter);
            _dialogsRegistered = true;
        }
    }

    private void OnHourlyTick()
    {
        // Runtime MCM disable freezes in-flight messengers. Re-enabling resumes processing on the next tick.
        if (!_settings.EnableMessengers) return;
        if (_store.Count == 0)
            return;

        var mapDiagonal = Campaign.MapDiagonal;
        var travelDays = _settings.MessengerTravelDays;
        var speed = _service.CalculateMessengerSpeed(mapDiagonal, travelDays);

        _toRemoveScratch.Clear();

        foreach (var messenger in _store.GetAll())
        {
            if (messenger.Arrived)
            {
                if (!_processingArrivedMessenger)
                {
                    var outcome = HandleArrivedMessenger(messenger);
                    if (outcome == ArrivedHandleOutcome.RemoveFromList)
                        _toRemoveScratch.Add(messenger.TargetHeroId);
                }
                continue;
            }

            UpdateMessengerPosition(messenger, travelDays, speed);

            if (!messenger.Arrived && _service.RollAccident())
            {
                NotifyAccident(messenger);
                _toRemoveScratch.Add(messenger.TargetHeroId);
            }
        }

        for (var i = 0; i < _toRemoveScratch.Count; i++)
            _store.Remove(_toRemoveScratch[i]);
        _toRemoveScratch.Clear();
    }

    private void UpdateMessengerPosition(PendingMessenger messenger, int travelDays, float speed)
    {
        var elapsedDays = CampaignTime.Now.ToDays - messenger.DispatchTimeDays;
        if (elapsedDays >= travelDays)
        {
            messenger.Arrived = true;
            return;
        }

        var target = ResolveHero(messenger.TargetHeroId);
        var targetVec = target?.GetMapPoint()?.Position.ToVec2() ?? Vec2.Invalid;
        if (!targetVec.IsValid)
            return;

        var update = _service.AdvancePosition(messenger.Position, ToMapCoord(targetVec), speed);
        messenger.Position = update.NewPosition;
        if (update.Arrived)
            messenger.Arrived = true;
    }

    // Boundary conversion: TaleWorlds.Library.Vec2 → TAOM-owned MapCoord. Vec2 stays at the boundary;
    // service / domain code uses MapCoord (Codex review #34, ADR-007).
    private static MapCoord ToMapCoord(Vec2 v) => v.IsValid ? new MapCoord(v.X, v.Y) : MapCoord.Invalid;

    private enum ArrivedHandleOutcome
    {
        WaitForNextTick,
        AwaitingUserInput,
        RemoveFromList,
    }

    private ArrivedHandleOutcome HandleArrivedMessenger(PendingMessenger messenger)
    {
        var target = ResolveHero(messenger.TargetHeroId);
        if (target == null)
            return ArrivedHandleOutcome.RemoveFromList;

        // Permanent unavailability classes — drop with a "messenger lost" notification.
        // Codex review #34 caught: send-time validation rejects fugitive / dead / disabled targets,
        // but the original arrival path only screened dead/disabled. A target that became fugitive
        // mid-flight could pass through the temporary-wait path and trigger StartMessengerConversation
        // with no settlement / no party, falling back to MainParty as the conversation partner.
        if (!target.IsAlive
            || target.HeroState == Hero.CharacterStates.Disabled
            || target.IsFugitive
            || (!target.IsActive && !target.IsWanderer)
            || (!target.IsActive && target.IsWanderer && target.HeroState != Hero.CharacterStates.NotSpawned))
        {
            NotifyMessengerLost(messenger, target);
            return ArrivedHandleOutcome.RemoveFromList;
        }

        if (target.PartyBelongedTo == Hero.MainHero?.PartyBelongedTo)
        {
            var text = new TextObject("{=taom_messenger_confused}The messenger seems confused — they were trying to reach someone in your own party!");
            text.SetTextVariable("HERO_NAME", target.Name);
            ShowInquiry(
                new TextObject("{=taom_messenger_error}Messenger Error").ToString(),
                text.ToString(),
                affirmative: GameTexts.FindText("str_ok").ToString());
            return ArrivedHandleOutcome.RemoveFromList;
        }

        // Temporary unavailability — defer to next tick. IsTargetAvailableNow gates on
        // prisoner/child/MapEvent (transient), all of which the player may resolve.
        if (!IsPlayerAvailable() || !IsTargetAvailableNow(target))
            return ArrivedHandleOutcome.WaitForNextTick;

        _processingArrivedMessenger = true;

        var arrivalText = new TextObject("{=taom_messenger_arrived}A messenger from {HERO_NAME} has arrived. Do you wish to speak with them?");
        arrivalText.SetTextVariable("HERO_NAME", target.Name);

        InformationManager.ShowInquiry(new InquiryData(
                new TextObject("{=taom_messenger_arrived_title}Messenger Arrived").ToString(),
                arrivalText.ToString(),
                isAffirmativeOptionShown: true,
                isNegativeOptionShown: true,
                affirmativeText: new TextObject("{=taom_messenger_speak}Speak").ToString(),
                negativeText: new TextObject("{=taom_messenger_dismiss}Dismiss").ToString(),
                affirmativeAction: () => StartMessengerConversation(messenger),
                negativeAction: () => DismissMessenger(messenger)),
            pauseGameActiveState: true,
            prioritize: false);

        return ArrivedHandleOutcome.AwaitingUserInput;
    }

    private void DismissMessenger(PendingMessenger messenger)
    {
        _store.Remove(messenger.TargetHeroId);
        _processingArrivedMessenger = false;
    }

    private void NotifyAccident(PendingMessenger messenger)
    {
        var target = ResolveHero(messenger.TargetHeroId);
        var targetName = target?.Name ?? new TextObject(messenger.TargetHeroId);

        var text = new TextObject("{=taom_messenger_lost}Your messenger to {HERO_NAME} was ambushed by bandits and never arrived.");
        text.SetTextVariable("HERO_NAME", targetName);
        ShowInquiry(
            new TextObject("{=taom_messenger_lost_title}Messenger Lost").ToString(),
            text.ToString(),
            affirmative: GameTexts.FindText("str_ok").ToString());
    }

    private void NotifyMessengerLost(PendingMessenger messenger, Hero deadTarget)
    {
        var targetName = deadTarget?.Name ?? new TextObject(messenger.TargetHeroId);

        var text = new TextObject("{=taom_messenger_recipient_gone}Your messenger to {HERO_NAME} returned without delivering — the recipient is no longer reachable.");
        text.SetTextVariable("HERO_NAME", targetName);
        ShowInquiry(
            new TextObject("{=taom_messenger_lost_title}Messenger Lost").ToString(),
            text.ToString(),
            affirmative: GameTexts.FindText("str_ok").ToString());
    }

    private void StartMessengerConversation(PendingMessenger messenger)
    {
        if (_activeMessenger != null || _currentMission != null)
            return;

        var target = ResolveHero(messenger.TargetHeroId);
        if (target == null)
        {
            _store.Remove(messenger.TargetHeroId);
            _processingArrivedMessenger = false;
            return;
        }

        _activeMessenger = messenger;
        var mainParty = PartyBase.MainParty;

        // Spawn unspawned wanderer at born settlement
        if (target.IsWanderer && target.HeroState == Hero.CharacterStates.NotSpawned && target.BornSettlement != null)
        {
            target.ChangeState(Hero.CharacterStates.Active);
            EnterSettlementAction.ApplyForCharacterOnly(target, target.BornSettlement);
        }

        // Resolve target party — settlement → travelling party → born settlement → main (last-resort)
        var currentSettlement = target.CurrentSettlement;
        PartyBase targetParty;
        if (currentSettlement != null)
            targetParty = currentSettlement.Party ?? target.BornSettlement?.Party;
        else
            targetParty = target.PartyBelongedTo?.Party ?? target.BornSettlement?.Party;

        PlayerEncounter.Start();
        PlayerEncounter.Current.SetupFields(mainParty, targetParty ?? mainParty);
        Campaign.Current.CurrentConversationContext = ConversationContext.Default;

        if (currentSettlement != null)
        {
            _originalPosition = Hero.MainHero.GetMapPoint().Position.ToVec2();
            PlayerEncounter.EnterSettlement();

            var targetLocation = LocationComplex.Current.GetLocationOfCharacter(target);
            var playerLocation = LocationComplex.Current.GetLocationOfCharacter(Hero.MainHero);

            CampaignEventDispatcher.Instance.OnPlayerStartTalkFromMenu(target);
            _currentMission = (Mission)PlayerEncounter.LocationEncounter.CreateAndOpenMissionController(
                targetLocation, playerLocation, target.CharacterObject, null);
        }
        else
        {
            _originalPosition = Vec2.Invalid;
            _currentMission = (Mission)Campaign.Current.CampaignMissionManager.OpenConversationMission(
                new ConversationCharacterData(Hero.MainHero.CharacterObject, mainParty, true, false, false, false, false, false),
                new ConversationCharacterData(target.CharacterObject, targetParty, true, false, false, false, false, false),
                "", "");
        }

        // Phase 9b #123 P1 — null-guard `_currentMission`. If OpenConversationMission returns null,
        // AddListener no-ops → OnEndMission never fires → `_processingArrivedMessenger` stays stuck
        // true forever → all future arrived-messenger processing silently blocked for the session.
        if (_currentMission != null)
        {
            _currentMission.AddListener(this);
        }
        else
        {
            _logger.LogWarning($"Messengers: OpenConversationMission returned null for target '{target?.StringId}'. " +
                "Resetting processing flag + dropping messenger from store to recover.");
            if (_activeMessenger != null)
                _store.Remove(_activeMessenger.TargetHeroId);
            _activeMessenger = null;
            _processingArrivedMessenger = false;
            _originalPosition = Vec2.Invalid;
        }
    }

    // --- IMissionListener ---

    public void OnEndMission()
    {
        if (_activeMessenger != null)
        {
            _store.Remove(_activeMessenger.TargetHeroId);
            _activeMessenger = null;
        }

        _currentMission?.RemoveListener(this);
        _currentMission = null;
        _processingArrivedMessenger = false;

        // Defer settlement cleanup by one tick — PlayerEncounter.Finish must not run inside OnEndMission
        CampaignEvents.TickEvent.AddNonSerializedListener(this, CleanUpSettlementEncounter);
    }

    public void OnMissionModeChange(MissionMode oldMissionMode, bool atStart)
    {
        if (_currentMission == null) return;
        var allowedNow = false;
        for (var i = 0; i < AllowedMissionModes.Length; i++)
            if (AllowedMissionModes[i] == _currentMission.Mode) { allowedNow = true; break; }
        var allowedBefore = false;
        for (var i = 0; i < AllowedMissionModes.Length; i++)
            if (AllowedMissionModes[i] == oldMissionMode) { allowedBefore = true; break; }
        if (!allowedNow && allowedBefore)
            _currentMission.EndMission();
    }

    public void OnEquipItemsFromSpawnEquipmentBegin(Agent agent, Agent.CreationType creationType) { }
    public void OnEquipItemsFromSpawnEquipment(Agent agent, Agent.CreationType creationType) { }
    public void OnConversationCharacterChanged() { }
    public void OnResetMission() { }
    public void OnDeploymentPlanMade(Team team, bool isFirstPlan) { }

    private void CleanUpSettlementEncounter(float dt)
    {
        try
        {
            PlayerEncounter.Finish(true);

            if (_originalPosition.IsValid && MobileParty.MainParty != null)
                MobileParty.MainParty.Position = new CampaignVec2(_originalPosition, isOnLand: true);
        }
        catch (System.Exception ex)
        {
            _logger.LogWarning($"Messengers: settlement-cleanup tick handler threw: {ex.Message}");
        }
        finally
        {
            _originalPosition = Vec2.Invalid;
            // Phase 9b #123 P2 — audit suggested `RemoveNonSerializedListener` to avoid clearing
            // unrelated listeners, but v1.3.15 `IMbEvent<T>` / generic `MbEvent<T>` only expose
            // `AddNonSerializedListener` + `ClearListeners` (verified via ilspycmd). No public
            // Remove-one exists. `ClearListeners(this)` IS the API. If a future author adds a 2nd
            // TickEvent listener on this object, they must use a separate owner proxy.
            CampaignEvents.TickEvent.ClearListeners(this);
        }
    }

    // --- Dialog tree ---

    private void AddDialogOptions(CampaignGameStarter starter)
    {
        starter.AddPlayerLine(
            "taom_messenger_send_init",
            "hero_main_options",
            "taom_messenger_send_confirm",
            "{=taom_messenger_dialog_init}I need to send you a message later. Can I dispatch a messenger to you?",
            DialogCondition_CanSend,
            null);

        starter.AddDialogLine(
            "taom_messenger_send_npc_accept",
            "taom_messenger_send_confirm",
            "taom_messenger_send_choice",
            "{=taom_messenger_dialog_npc_accept}Of course. I'll make sure to receive your messenger when they arrive.",
            null, null);

        starter.AddPlayerLine(
            "taom_messenger_send_choice_yes",
            "taom_messenger_send_choice",
            "taom_messenger_dialog_sent",
            "{=taom_messenger_dialog_send}Send the messenger ({COST} denars)",
            DialogCondition_HasGold,
            DialogConsequence_DispatchMessenger);

        starter.AddPlayerLine(
            "taom_messenger_send_choice_no",
            "taom_messenger_send_choice",
            "taom_messenger_dialog_decline_ack",
            "{=taom_messenger_dialog_decline}On second thought, never mind.",
            null, null);

        starter.AddDialogLine(
            "taom_messenger_dialog_sent",
            "taom_messenger_dialog_sent",
            "close_window",
            "{=taom_messenger_dialog_sent}I'll be expecting your messenger then.",
            null, null);

        starter.AddDialogLine(
            "taom_messenger_dialog_decline_ack",
            "taom_messenger_dialog_decline_ack",
            "close_window",
            "{=taom_messenger_dialog_decline_ack}As you wish.",
            null, null);
    }

    private bool DialogCondition_CanSend()
    {
        if (!_settings.EnableMessengers) return false;
        var hero = Hero.OneToOneConversationHero;
        if (hero == null) return false;
        return _service.CanSendMessenger(SnapshotHero(hero), Hero.MainHero?.Gold ?? 0) == MessengerValidationResult.Ok;
    }

    private bool DialogCondition_HasGold()
    {
        // Codex review #34 round 2: the initial dialog line gates on EnableMessengers via DialogCondition_CanSend,
        // but the MCM toggle can flip OFF between accept and the cost line. If we don't re-check here, the player
        // can pick "Send the messenger" → SendMessenger silently no-ops → dialog still advances to the success
        // line "I'll be expecting your messenger then." Re-validating here keeps the gate honest.
        if (!_settings.EnableMessengers) return false;
        MBTextManager.SetTextVariable("COST", _settings.MessengerGoldCost.ToString());
        return (Hero.MainHero?.Gold ?? 0) >= _settings.MessengerGoldCost;
    }

    private void DialogConsequence_DispatchMessenger()
    {
        var hero = Hero.OneToOneConversationHero;
        if (hero == null) return;
        SendMessenger(hero);
    }

    // --- Helpers ---

    private static Hero ResolveHero(string heroId)
    {
        if (string.IsNullOrEmpty(heroId)) return null;
        return Hero.FindFirst(h => h != null && h.StringId == heroId);
    }

    private static HeroSnapshot SnapshotHero(Hero hero)
    {
        if (hero == null) return null;
        var inPlayerParty = hero.PartyBelongedTo != null
                            && Hero.MainHero?.PartyBelongedTo != null
                            && hero.PartyBelongedTo == Hero.MainHero.PartyBelongedTo;
        var inMapEvent = hero.PartyBelongedTo?.MapEvent != null;
        return new HeroSnapshot(
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

    private static bool IsTargetAvailableNow(Hero hero)
    {
        if (hero == null || !hero.IsAlive || hero.IsPrisoner || hero.IsChild) return false;
        return hero.PartyBelongedTo?.MapEvent == null;
    }

    private static bool IsPlayerAvailable()
    {
        if (PartyBase.MainParty == null) return false;
        if (PlayerEncounter.Current != null) return false;
        if (GameStateManager.Current?.ActiveState is MapState mapState && !mapState.AtMenu) return true;
        return false;
    }

    private TextObject BuildValidationReason(MessengerValidationResult result, Hero target)
    {
        TextObject text;
        switch (result)
        {
            case MessengerValidationResult.NullTarget:
            case MessengerValidationResult.HumanPlayerCharacter:
                return new TextObject("{=taom_messenger_invalid_target}Invalid target.");
            case MessengerValidationResult.InsufficientGold:
                text = new TextObject("{=taom_messenger_no_gold}Not enough gold! You need {COST} denars.");
                text.SetTextVariable("COST", _settings.MessengerGoldCost);
                return text;
            case MessengerValidationResult.AlreadyPending:
                return new TextObject("{=taom_messenger_already_sent}A messenger has already been dispatched to this person.");
            case MessengerValidationResult.HeroDead:
                text = new TextObject("{=taom_messenger_hero_dead}{HERO_NAME} is dead.");
                text.SetTextVariable("HERO_NAME", target?.Name ?? new TextObject(""));
                return text;
            case MessengerValidationResult.HeroPrisoner:
                text = new TextObject("{=taom_messenger_hero_prisoner}{HERO_NAME} is imprisoned and cannot receive messengers.");
                text.SetTextVariable("HERO_NAME", target?.Name ?? new TextObject(""));
                return text;
            case MessengerValidationResult.HeroFugitive:
                text = new TextObject("{=taom_messenger_hero_fugitive}{HERO_NAME} is fugitive and cannot be found.");
                text.SetTextVariable("HERO_NAME", target?.Name ?? new TextObject(""));
                return text;
            case MessengerValidationResult.HeroChild:
                text = new TextObject("{=taom_messenger_hero_child}{HERO_NAME} is too young to receive messengers.");
                text.SetTextVariable("HERO_NAME", target?.Name ?? new TextObject(""));
                return text;
            case MessengerValidationResult.TargetInPlayerParty:
                text = new TextObject("{=taom_messenger_confused}The messenger seems confused — they were trying to reach someone in your own party!");
                text.SetTextVariable("HERO_NAME", target?.Name ?? new TextObject(""));
                return text;
            default:
                return new TextObject("{=taom_messenger_target_unavailable}This person cannot be reached by messenger at this time.");
        }
    }

    private static void ShowInquiry(string title, string body, string affirmative)
    {
        InformationManager.ShowInquiry(new InquiryData(
            title, body,
            isAffirmativeOptionShown: true,
            isNegativeOptionShown: false,
            affirmativeText: affirmative,
            negativeText: string.Empty,
            affirmativeAction: null,
            negativeAction: null),
            pauseGameActiveState: false,
            prioritize: false);
    }
}
