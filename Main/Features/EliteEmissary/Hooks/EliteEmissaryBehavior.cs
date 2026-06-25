using System.Collections.Generic;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Localization;
using TAOM.Adapters;
using TAOM.Core.Logging;

namespace TAOM.Features.EliteEmissary.Hooks;

/// <summary>
/// Thin boundary (ADR-002) for the Settlement Elite Emissary: registers a "Speak with the faction
/// emissary" option on the town/castle/village menus at key settlements, opens a short conversation
/// with a settlement notable, and on the "purchase elite units" line hands off to the
/// <see cref="EliteEmissaryInquiryPresenter"/>. All decisions live in <see cref="IEliteEmissaryService"/>.
/// Stateless across saves — no SyncData (balances persist via SpecialResources; troops via the engine roster).
/// </summary>
public sealed class EliteEmissaryBehavior : CampaignBehaviorBase
{
    private const string MenuOptionId = "taom_emissary_speak";

    private readonly IEliteEmissaryService _service;
    private readonly IEliteEmissarySettingsProvider _settings;
    private readonly IEliteEmissaryConfigProvider _config;
    private readonly ISettlementOwnerAdapter _ownerAdapter;
    private readonly IModLogger _logger;
    private readonly EliteEmissaryInquiryPresenter _presenter;

    // Set in the menu consequence right before OpenConversation so the emissary greeting only fires
    // for the conversation WE launched (never hijacks a normal notable chat). Cleared once greeted.
    private string _pendingEmissaryHeroId;
    private bool _keySettlementsValidated;

    public EliteEmissaryBehavior(
        IEliteEmissaryService service,
        IEliteEmissarySettingsProvider settings,
        IEliteEmissaryConfigProvider config,
        ISettlementOwnerAdapter ownerAdapter,
        IModLogger logger)
    {
        _service = service;
        _settings = settings;
        _config = config;
        _ownerAdapter = ownerAdapter;
        _logger = logger;
        _presenter = new EliteEmissaryInquiryPresenter(service, ownerAdapter, logger);
    }

    public override void RegisterEvents()
    {
        CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
        // Belt to GreetConsequence's clear (deep-review 2026-06-25): guarantees the greeting flag never
        // survives a conversation, so a stale flag can't make the emissary greeting appear in a later
        // normal chat with the same notable (the rare case where a higher-priority vanilla "start" line —
        // e.g. an active counter-offer issue with that notable — wins and our greet consequence never fires).
        CampaignEvents.ConversationEnded.AddNonSerializedListener(this, OnConversationEnded);
    }

    public override void SyncData(IDataStore dataStore)
    {
        // Stateless: resource balances persist via SpecialResources' store; granted troops via the
        // engine roster; config is immutable; owner faction is re-resolved every menu open.
    }

    private void OnSessionLaunched(CampaignGameStarter starter)
    {
        // Registered unconditionally so the MCM master toggle takes effect at runtime (the condition
        // re-checks IsEnabled each open). Key settlements can be any type → all three menus.
        var menuText = new TextObject("{=taom_emissary_menu}Speak with the faction emissary");
        starter.AddGameMenuOption("town", MenuOptionId, menuText.ToString(), MenuCondition, MenuConsequence, isLeave: false, index: 5);
        starter.AddGameMenuOption("castle", MenuOptionId, menuText.ToString(), MenuCondition, MenuConsequence, isLeave: false, index: 5);
        starter.AddGameMenuOption("village", MenuOptionId, menuText.ToString(), MenuCondition, MenuConsequence, isLeave: false, index: 4);

        RegisterDialog(starter);
        ValidateKeySettlements();
    }

    // --- Menu ---

    private bool MenuCondition(MenuCallbackArgs args)
    {
        if (!_service.IsEnabled) return false;
        var settlement = Settlement.CurrentSettlement;
        if (settlement == null || !_service.IsKeySettlement(settlement.StringId)) return false;
        if (IsEmissaryAccessBlocked(settlement)) return false;

        var owner = _ownerAdapter.GetOwnerInfo(settlement);
        bool hasOffers = _service.HasPurchasableOffers(owner.OwnerKingdomId, owner.OwnerCultureId);
        if (!hasOffers && _settings.HideWhenNoResource)
            return false;

        args.optionLeaveType = GameMenuOption.LeaveType.Trade;
        return MenuHelper.SetOptionProperties(args, hasOffers, !hasOffers,
            new TextObject("{=taom_emissary_no_trade}There is no elite trade for this faction here."));
    }

    private void MenuConsequence(MenuCallbackArgs args)
    {
        var settlement = Settlement.CurrentSettlement;
        if (settlement == null) return;

        var notable = FindEmissaryNotable(settlement);
        if (notable != null)
        {
            _pendingEmissaryHeroId = notable.StringId;
            _logger.LogInfo($"[EliteEmissary] Opening emissary conversation with {notable.Name} at {settlement.StringId}");
            CampaignMapConversation.OpenConversation(
                new ConversationCharacterData(CharacterObject.PlayerCharacter),
                new ConversationCharacterData(notable.CharacterObject));
        }
        else
        {
            // No notable to embody the emissary (rare): open the purchase list directly.
            _logger.LogWarning($"[EliteEmissary] No living notable at {settlement.StringId} — opening purchase list directly");
            _presenter.OpenTroopList(settlement);
        }
    }

    private static Hero FindEmissaryNotable(Settlement settlement)
    {
        foreach (var notable in settlement.Notables)
        {
            if (notable != null && notable.IsAlive)
                return notable;
        }
        return null;
    }

    // --- Dialog ---

    private void RegisterDialog(CampaignGameStarter starter)
    {
        starter.AddDialogLine("taom_emissary_greet", "start", "taom_emissary_hub",
            "{=taom_emissary_greet}You seek the finest warriors our people can offer? Name them, and they are yours — for the right price.",
            GreetCondition, GreetConsequence, 200);

        starter.AddPlayerLine("taom_emissary_buy", "taom_emissary_hub", "close_window",
            "{=taom_emissary_buy}I wish to purchase elite units.",
            BuyCondition, BuyConsequence, 200);

        starter.AddPlayerLine("taom_emissary_leave", "taom_emissary_hub", "close_window",
            "{=taom_emissary_leave}Not today.", null, null, 100);
    }

    private bool GreetCondition()
    {
        return _pendingEmissaryHeroId != null
            && Hero.OneToOneConversationHero?.StringId == _pendingEmissaryHeroId;
    }

    private void GreetConsequence()
    {
        // Clear so a later NORMAL conversation with the same notable doesn't show the emissary greeting.
        _pendingEmissaryHeroId = null;
    }

    private void OnConversationEnded(IEnumerable<CharacterObject> characters)
    {
        // Bulletproof clear: after ANY conversation ends, the greeting flag is reset. Covers the case
        // where our greet line never fired (a higher-priority vanilla start line won), which would
        // otherwise leave _pendingEmissaryHeroId set and leak the emissary greeting into the next
        // normal conversation with that notable.
        _pendingEmissaryHeroId = null;
    }

    private bool BuyCondition()
    {
        var settlement = Settlement.CurrentSettlement;
        if (settlement == null) return false;
        if (IsEmissaryAccessBlocked(settlement)) return false;
        var owner = _ownerAdapter.GetOwnerInfo(settlement);
        return _service.HasPurchasableOffers(owner.OwnerKingdomId, owner.OwnerCultureId);
    }

    // The emissary is a faction-official interaction: it must NOT be reachable when the player sneaked
    // into a hostile settlement via vanilla DISGUISE (the LimitedAccessSolution.Disguise path puts a
    // disguised at-war player on the normal "town" menu, where this option would otherwise appear and
    // let them buy the enemy faction's elites). Mirrors how vanilla CanMainHeroTrade gates disguised
    // trade. Codex review 2026-06-25 [HIGH]. The MenuCondition check hides the option; BuyCondition is
    // the belt — the conversation/purchase flow is only reachable through one of these two gates.
    private static bool IsEmissaryAccessBlocked(Settlement settlement)
    {
        if (Campaign.Current != null && Campaign.Current.IsMainHeroDisguised)
            return true;
        var ownerFaction = settlement.MapFaction;
        var playerFaction = Hero.MainHero?.MapFaction;
        return ownerFaction != null && playerFaction != null && ownerFaction.IsAtWarWith(playerFaction);
    }

    private void BuyConsequence()
    {
        var settlement = Settlement.CurrentSettlement;
        if (settlement != null)
            _presenter.OpenTroopList(settlement);
    }

    // --- Key-settlement id validation (settlements exist by session launch; MBObjectManager populated) ---

    private void ValidateKeySettlements()
    {
        if (_keySettlementsValidated) return;
        _keySettlementsValidated = true;

        int ok = 0, missing = 0;
        foreach (var id in _config.GetConfig().KeySettlementIds)
        {
            if (Settlement.Find(id) == null)
            {
                _logger.LogWarning($"[EliteEmissary] key settlement '{id}' not found in this campaign — emissary will never appear there (check elite_emissary_config.xml against settlements.xml)");
                missing++;
            }
            else
            {
                ok++;
            }
        }
        _logger.LogInfo($"[EliteEmissary] key-settlement validation: {ok} resolved, {missing} missing");
    }
}
