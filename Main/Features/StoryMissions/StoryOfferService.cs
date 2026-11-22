using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.Enlistment;
using TAOM.Features.Execution;
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

    /// <summary>A result on a window the map had already resolved: nothing changed.</summary>
    Unchanged,

    /// <summary>A fallen fortress no attacker could take: nothing changed, and the window is offered again on the next day.</summary>
    Failed,

    /// <summary>A hero story (#1105 slice 3): the hero lives, and the event resolved Averted.</summary>
    HeroLives,

    /// <summary>A hero story: the hero died in battle, and the event resolved Occurred.</summary>
    HeroDies,
}

/// <summary>
/// The story offer (#1105 slice 4): picks the chronicle window to offer, refuses an offer where a story mission cannot run (co-op, a
/// dedicated server, enlistment, captivity, a mission the catalog lacks), and turns a mission's verdict into the campaign result. The
/// sides on offer follow the player's side of the war (#1326, <see cref="StorySideGate"/>): a window with no side he may take is not
/// offered at all, because its "Let fate decide" is a saved decline. Held walls (a won defence, a lost attack) resolve the window Held;
/// fallen walls give the target to an attacker, and the chronicle's owner-change path resolves it Fell; no verdict snoozes the offer for
/// the rest of the day. The snooze is per session only; a declined offer is the chronicle's saved state.
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
    private readonly IAlignmentService _alignment;
    private readonly IModLogger _logger;
    private readonly IOutcomeLog _outcomes;
    private readonly HashSet<string> _snoozed = new(StringComparer.Ordinal);
    private readonly HashSet<string> _warnedMissing = new(StringComparer.Ordinal);

    public StoryOfferService(IChronicleStoryPort chronicle, StoryMissionCatalog catalog, ICoopSessionProvider coop, IDedicatedServerProvider dedicated,
        IEnlistmentStateQuery enlistment, IStoryCampaignAdapter campaign, IAlignmentService alignment, IModLogger logger, IOutcomeLog outcomes)
    {
        _chronicle = chronicle;
        _catalog = catalog;
        _coop = coop;
        _dedicated = dedicated;
        _enlistment = enlistment;
        _campaign = campaign;
        _alignment = alignment;
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

    /// <summary>The first window with a side this player may take, or null. The caller has checked that the map is free.</summary>
    public StoryOffer? NextOffer()
    {
        // Read live on every use (ICoopSessionProvider's contract).
        if (!CoopSessionPolicy.IsSinglePlayerAuthority(_coop, _dedicated) || _enlistment.IsEnlisted || _campaign.IsMainHeroPrisoner
            || _campaign.IsMainPartyInMapEvent)
            return null;

        // The player's clan map faction (his kingdom, or his clan outside one) with its culture: one pair gives his side and his tie.
        var player = _campaign.PlayerSide;
        var side = _alignment.ResolveSide(player);
        foreach (var window in _chronicle.GetStoryOffers())
        {
            if (_snoozed.Contains(window.EventId))
                continue;

            var defend = Loads(window, window.MissionId, StorySide.Defending);
            var attack = window.AttackMissionId != null && Loads(window, window.AttackMissionId, StorySide.Attacking);
            // A window's kingdoms are config ids, each listed in the alignment table (ShippedStoryOffersTests), so the id alone sides
            // them and a war pledge counts. The first attacker leads the host.
            var lead = window.Attackers.Count > 0 ? window.Attackers[0] : string.Empty;
            var choices = StorySideGate.Choose(side, _alignment.GetKingdomSide(window.Defender), _alignment.GetKingdomSide(lead),
                TieOf(player.FactionId, window), defend, attack);
            if (choices.Any)
                return new StoryOffer(window, choices, side);
        }

        return null;
    }

    private bool Loads(ChronicleStoryOffer window, string missionId, StorySide side)
    {
        if (_catalog.TryGet(missionId, out var mission) && mission.Side == side)
            return true;
        if (_warnedMissing.Add(window.EventId + "/" + missionId))
            _logger.LogWarning($"[StoryMissions] '{window.EventId}' offers story mission '{missionId}', which is not a loaded "
                + $"{(side == StorySide.Defending ? "defence" : "attack")}; it is not offered.");
        return false;
    }

    // The player's map faction against the window's kingdoms: he never fights his own realm.
    private static StoryTie TieOf(string factionId, ChronicleStoryOffer window)
    {
        if (string.IsNullOrEmpty(factionId))
            return StoryTie.None;
        if (string.Equals(factionId, window.Defender, StringComparison.Ordinal))
            return StoryTie.Defender;
        return window.Attackers.Contains(factionId) ? StoryTie.Attacker : StoryTie.None;
    }

    /// <summary>The offer was shown: one check line per campaign and window.</summary>
    public void NoteShown(StoryOffer offer) => _outcomes.Pass(ShownId, Key(offer.EventId),
        $"mission={offer.Window.MissionId} target={offer.Window.TargetSettlementId} choices={offer.Choices} side={offer.PlayerSide}");

    // OutcomeLog writes one line per key for the whole process, so a key names the campaign: a second campaign, or a reload that plays
    // the other result, still writes its line.
    private string Key(string eventId) => _campaign.CampaignId + "/" + eventId;

    /// <summary>"Let fate decide".</summary>
    public bool Decline(ChronicleStoryOffer offer) => _chronicle.DeclineStory(offer.EventId);

    /// <summary>
    /// Applies a story mission's verdict, for the side the player took, to its window. Main thread, from a menu click on the map.
    /// </summary>
    public StoryResult Apply(ChronicleStoryOffer offer, StorySide side, StoryOutcome verdict)
    {
        if (verdict != StoryOutcome.Won && verdict != StoryOutcome.Lost)
        {
            Snooze(offer.EventId);
            _logger.LogInfo($"[StoryMissions] '{offer.EventId}' ended with no verdict; offered again tomorrow.");
            return StoryResult.Undecided;
        }

        var attacked = side == StorySide.Attacking;
        var key = Key(offer.EventId) + "/" + (attacked ? "Attack" : string.Empty) + verdict;
        var detail = (attacked ? "side=Attacking " : string.Empty) + $"verdict={verdict}";
        if (offer.HeroId != null)
            return ApplyHero(offer, (verdict == StoryOutcome.Won) != attacked, key, detail);

        // The walls held: the defender won, or the attacker lost.
        if ((verdict == StoryOutcome.Won) != attacked)
        {
            if (!_chronicle.ResolveStoryHeld(offer.EventId, attacked ? "the player's assault was thrown back" : "the player won the story mission"))
            {
                _logger.LogInfo($"[StoryMissions] the walls of '{offer.EventId}' held, but its window is no longer open: nothing changed.");
                return StoryResult.Unchanged;
            }

            _outcomes.Pass(ResultId, key, detail + " result=Held");
            return StoryResult.Held;
        }

        // The walls fell. The transfer bypasses the chronicle, so the chronicle first reconciles the live owner and confirms the window
        // is open: an owner change that raised no event (a clan that leaves its kingdom keeps its fiefs) must not be overwritten.
        if (!_chronicle.ConfirmStoryWindow(offer.EventId))
        {
            _logger.LogInfo($"[StoryMissions] the walls of '{offer.EventId}' fell, but its window is no longer open: nothing changed.");
            return StoryResult.Unchanged;
        }

        var taker = "the window has no target";
        if (offer.TargetSettlementId != null && _campaign.GiveSettlement(offer.TargetSettlementId, offer.Attackers, out taker))
        {
            _outcomes.Pass(ResultId, key, detail + $" result=Fell taker={taker}");
            return StoryResult.Fell;
        }

        _logger.LogError($"[StoryMissions] the walls of '{offer.EventId}' fell, but {offer.TargetSettlementId} could not pass to an attacker ({taker}).");
        _outcomes.Fail(ResultId, key, detail + " result=Failed");
        Snooze(offer.EventId);
        return StoryResult.Failed;
    }

    /// <summary>A hero story (#1105 slice 3). The hero lives when his side won, whichever side the player took; the chronicle resolves the event and kills him itself.</summary>
    private StoryResult ApplyHero(ChronicleStoryOffer offer, bool heroLives, string key, string detail)
    {
        if (!_chronicle.ResolveHeroStory(offer.EventId, heroLives, heroLives ? "the hero's side won the story mission" : "the hero's side lost the story mission"))
        {
            _logger.LogInfo($"[StoryMissions] the story of '{offer.EventId}' was fought, but its window is no longer open: nothing changed.");
            return StoryResult.Unchanged;
        }

        _outcomes.Pass(ResultId, key, detail + (heroLives ? " result=Averted" : " result=Occurred") + " hero=" + offer.HeroId);
        return heroLives ? StoryResult.HeroLives : StoryResult.HeroDies;
    }
}
