using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.Enlistment;
using TAOM.Features.StoryMissions.Config;
using TAOM.Features.StoryMissions.Domain;
using TAOM.Features.WarChronicle.Events;

namespace TAOM.Features.StoryMissions;

/// <summary>What a story mission's verdict did to the campaign.</summary>
internal enum StoryResult
{
    /// <summary>The window resolved Held.</summary>
    Held,

    /// <summary>The target passed to an attacker; the chronicle resolves the window Fell from the owner change.</summary>
    Fell,

    /// <summary>No verdict: the window stays open and is offered again on the next day.</summary>
    Undecided,

    /// <summary>A win on a window the map had already resolved: nothing changed.</summary>
    Unchanged,

    /// <summary>A loss no attacker could take: nothing changed, and the window is offered again on the next day.</summary>
    Failed,
}

/// <summary>
/// The story offer (#1105 slice 4): picks the chronicle window to offer, refuses an offer where a story mission cannot run (co-op, a
/// dedicated server, enlistment, captivity, a mission the catalog lacks), and turns a mission's verdict into the campaign result. A win
/// resolves the window Held; a loss gives the target to an attacker, and the chronicle's owner-change path resolves it Fell; no verdict
/// snoozes the offer for the rest of the day. The snooze is per session only; a declined offer is the chronicle's saved state.
/// Singleton: <see cref="ResetForNewSession"/> runs from the offer behavior's constructor, which every campaign builds new.
/// </summary>
internal sealed class StoryOfferService
{
    internal const string ShownId = "taom.story-missions.offer.shown";
    internal const string ResultId = "taom.story-missions.offer.result";

    private readonly IChronicleStoryPort _chronicle;
    private readonly StoryMissionCatalog _catalog;
    private readonly ICoopSessionProvider _coop;
    private readonly IDedicatedServerProvider _dedicated;
    private readonly IEnlistmentStateQuery _enlistment;
    private readonly IStoryCampaignAdapter _campaign;
    private readonly IModLogger _logger;
    private readonly IOutcomeLog _outcomes;
    private readonly HashSet<string> _snoozed = new(StringComparer.Ordinal);
    private readonly HashSet<string> _warnedMissing = new(StringComparer.Ordinal);

    public StoryOfferService(IChronicleStoryPort chronicle, StoryMissionCatalog catalog, ICoopSessionProvider coop, IDedicatedServerProvider dedicated,
        IEnlistmentStateQuery enlistment, IStoryCampaignAdapter campaign, IModLogger logger, IOutcomeLog outcomes)
    {
        _chronicle = chronicle;
        _catalog = catalog;
        _coop = coop;
        _dedicated = dedicated;
        _enlistment = enlistment;
        _campaign = campaign;
        _logger = logger;
        _outcomes = outcomes;
    }

    public void ResetForNewSession()
    {
        _snoozed.Clear();
        _warnedMissing.Clear();
    }

    /// <summary>The daily tick: a snoozed offer may show again.</summary>
    public void NewDay() => _snoozed.Clear();

    /// <summary>Hides the window's offer for the rest of the day.</summary>
    public void Snooze(string eventId) => _snoozed.Add(eventId);

    /// <summary>The first window to offer now, or null. The caller has checked that the map is free.</summary>
    public ChronicleStoryOffer? NextOffer()
    {
        // Read live on every use (ICoopSessionProvider's contract).
        if (!CoopSessionPolicy.IsSinglePlayerAuthority(_coop, _dedicated) || _enlistment.IsEnlisted || _campaign.IsMainHeroPrisoner
            || _campaign.IsMainPartyInMapEvent)
            return null;

        var playerFaction = _campaign.PlayerKingdomId;
        foreach (var offer in _chronicle.GetStoryOffers())
        {
            // A loss would hand the fortress to the player's own ruler (or to the player): the defence is offered to the other side only.
            if (_snoozed.Contains(offer.EventId) || (playerFaction != null && offer.Attackers.Contains(playerFaction)))
                continue;
            if (_catalog.TryGet(offer.MissionId, out var mission) && mission.Side == StorySide.Defending)
                return offer;
            if (_warnedMissing.Add(offer.EventId))
                _logger.LogWarning($"[StoryMissions] '{offer.EventId}' offers story mission '{offer.MissionId}', which is not a loaded defence; it is not offered.");
        }

        return null;
    }

    /// <summary>The offer was shown: one check line per campaign and window.</summary>
    public void NoteShown(ChronicleStoryOffer offer) => _outcomes.Pass(ShownId, Key(offer.EventId), $"mission={offer.MissionId} target={offer.TargetSettlementId}");

    // OutcomeLog writes one line per key for the whole process, so a key names the campaign: a second campaign, or a reload that plays
    // the other result, still writes its line.
    private string Key(string eventId) => _campaign.CampaignId + "/" + eventId;

    /// <summary>
    /// The story offer menu's body for its state: the open offer before the mission, then the applied result. Held and Fell report the
    /// battle; Undecided and Failed say the call may come again (both snooze); Unchanged means the window closed meanwhile.
    /// </summary>
    public static string MenuText(bool hasOffer, StoryResult? result)
    {
        if (!hasOffer || result == StoryResult.Unchanged)
            return "{=taom_story_offer_none}The moment has passed.";

        return result switch
        {
            StoryResult.Held => "{=taom_story_offer_won}The walls of {TARGET} held, and the host breaks against them.",
            StoryResult.Fell => "{=taom_story_offer_lost}The walls of {TARGET} are lost.",
            null => "{=taom_story_offer_menu}The host of the enemy gathers before {TARGET}. Time stands until you choose.",
            _ => "{=taom_story_offer_undecided}The battle for {TARGET} is not decided. The call may come again.",
        };
    }

    /// <summary>"Let fate decide".</summary>
    public bool Decline(ChronicleStoryOffer offer) => _chronicle.DeclineStory(offer.EventId);

    /// <summary>Applies a story mission's verdict to its window. Main thread, from a menu click on the map.</summary>
    public StoryResult Apply(ChronicleStoryOffer offer, StoryOutcome verdict)
    {
        switch (verdict)
        {
            case StoryOutcome.Won:
                if (!_chronicle.ResolveStoryWon(offer.EventId))
                {
                    _logger.LogInfo($"[StoryMissions] '{offer.EventId}' was won, but its window is no longer open: nothing changed.");
                    return StoryResult.Unchanged;
                }

                _outcomes.Pass(ResultId, Key(offer.EventId) + "/Won", "verdict=Won result=Held");
                return StoryResult.Held;

            case StoryOutcome.Lost:
                // The transfer bypasses the chronicle, so the chronicle first reconciles the live owner and confirms the window is open: an
                // owner change that raised no event (a clan that leaves its kingdom keeps its fiefs) must not be overwritten.
                if (!_chronicle.ConfirmStoryWindow(offer.EventId))
                {
                    _logger.LogInfo($"[StoryMissions] '{offer.EventId}' was lost, but its window is no longer open: nothing changed.");
                    return StoryResult.Unchanged;
                }

                var taker = "the window has no target";
                if (offer.TargetSettlementId != null && _campaign.GiveSettlement(offer.TargetSettlementId, offer.Attackers, out taker))
                {
                    _outcomes.Pass(ResultId, Key(offer.EventId) + "/Lost", $"verdict=Lost result=Fell taker={taker}");
                    return StoryResult.Fell;
                }

                _logger.LogError($"[StoryMissions] '{offer.EventId}' was lost, but {offer.TargetSettlementId} could not pass to an attacker ({taker}).");
                _outcomes.Fail(ResultId, Key(offer.EventId) + "/Lost", "verdict=Lost result=Failed");
                Snooze(offer.EventId);
                return StoryResult.Failed;

            default:
                Snooze(offer.EventId);
                _logger.LogInfo($"[StoryMissions] '{offer.EventId}' ended with no verdict; offered again tomorrow.");
                return StoryResult.Undecided;
        }
    }
}
