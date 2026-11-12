using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.SpecialResources.Domain;

namespace TAOM.Features.SpecialResources;

/// <summary>
/// Decides who earns and pays special resources, and how much, for every event the behavior forwards
/// (#967 C1). With no co-op session each path is the code that used to sit in
/// <c>SpecialResourcesBehavior</c>, moved here unchanged and crediting only the local MainHero. During a
/// live session the authority credits each registered remote player instead, and its own MainHero earns,
/// pays and counts as nobody's. A client writes nothing.
///
/// Takes primitives and data objects only; the adapter reads the engine (ADR-007). It holds the
/// campaign's one-tick desertion grace and the log-once latches, so it is registered Transient and
/// SubModule resolves it once per campaign, which gives each campaign a fresh grace.
/// </summary>
public sealed class SpecialResourceEarningService
{
    private static readonly IReadOnlyList<EarningNotice> NoNotices = Array.Empty<EarningNotice>();
    private static readonly IReadOnlyList<EarnerRef> NoEarners = Array.Empty<EarnerRef>();

    private readonly ISpecialResourceService _service;
    private readonly ISpecialResourceStorageService _storage;
    private readonly ICoopSessionProvider _coop;
    private readonly IDedicatedServerProvider _dedicatedServer;
    private readonly IModLogger _logger;

    // The single-player desertion grace (#133 P2): the first daily tick of a campaign never deserts, so a
    // player who saved at balance 0 is not punished before they can earn. Spent by the first tick that
    // reaches the hero, even one with no resource.
    private bool _graceSpent;

    private bool _suppressionLogged;
    private bool _prisonersLogged;
    private bool _desertionOffLogged;
    private bool _recruitSuppressionLogged;
    private bool _recruitScopeLostLogged;

    public SpecialResourceEarningService(
        ISpecialResourceService service,
        ISpecialResourceStorageService storage,
        ICoopSessionProvider coop,
        IDedicatedServerProvider dedicatedServer,
        IModLogger logger)
    {
        _service = service;
        _storage = storage;
        _coop = coop;
        _dedicatedServer = dedicatedServer;
        _logger = logger;
    }

    /// <summary>A co-op session is live, so earning and the daily tick run per player.</summary>
    public bool SessionActive => _coop.IsSessionActive;

    public IReadOnlyList<EarningNotice> OnVictory(VictorySnapshot s)
    {
        var session = _coop.IsSessionActive;
        if (!session && !s.IsPlayerMapEvent) return NoNotices;
        var earners = Eligible(s.LocalHero, s.WinnerCandidates);
        if (earners.Count == 0) return NoNotices;

        _logger.LogDebug($"[SpecRes] MapEventEnded: state={s.State}, isSiege={s.IsSiege}");

        // PARTICIPATION, not command: see SpecialResourceEarnPolicy.IsPlayerVictory. In a session the
        // winning side's own parties name the earners, so only an unresolved winner stops the event.
        if (s.WinningSide == BattleSideEnum.None) return NoNotices;
        if (!session && !SpecialResourceEarnPolicy.IsPlayerVictory(s.PlayerSide, s.WinningSide)) return NoNotices;

        // A raid or extortion event pays only when a real field party was beaten in it (#770).
        if (!SpecialResourceEarnPolicy.PaysBattleCredit(s.EventType, s.BeatAFieldParty))
            return NoNotices;

        return CreditEach(earners, "victory", session, e =>
        {
            if (s.IsSiege)
            {
                _service.EarnFromSiege(e.HeroId, e.KingdomId, e.CultureId);
                return;
            }

            _service.EarnFromBattle(e.HeroId, e.KingdomId, e.CultureId, s.EnemyCount);
        });
    }

    public IReadOnlyList<EarningNotice> OnRaid(RaidOrHideoutSnapshot s)
    {
        if (s.Side != BattleSideEnum.Attacker) return NoNotices;
        var session = _coop.IsSessionActive;
        if (!session && !s.IsPlayerMapEvent) return NoNotices;
        // RaidCompleted also reports the attacker as winner when only the militia was beaten (#770);
        // per_raid is for the loot. The engine sets the village to Looted just before raising the event.
        if (!s.VillageLooted) return NoNotices;
        if (!session && !SpecialResourceEarnPolicy.IsPlayerVictory(s.PlayerSide, s.Side)) return NoNotices;

        return CreditEach(Eligible(s.LocalHero, s.AttackerLeaders), "raid", session,
            e => _service.EarnFromRaid(e.HeroId, e.KingdomId, e.CultureId));
    }

    public IReadOnlyList<EarningNotice> OnHideout(RaidOrHideoutSnapshot s)
    {
        if (s.Side != BattleSideEnum.Attacker) return NoNotices;
        var session = _coop.IsSessionActive;
        if (!session && !s.IsPlayerMapEvent) return NoNotices;
        // A "send troops" clear raises the hideout event twice for one event (#790): first from its
        // success menu, before the event is finalized, then from OnBeforeMapEventFinalize. Paying only a
        // finalized event pays once, and a save made in the success menu still pays once after load.
        if (!s.IsFinalized) return NoNotices;

        return CreditEach(Eligible(s.LocalHero, s.AttackerLeaders), "hideout", session,
            e => _service.EarnFromHideout(e.HeroId, e.KingdomId, e.CultureId));
    }

    /// <param name="winner">The winning hero: the local MainHero in single-player, any hero in a session. Null when the winner has no hero.</param>
    public IReadOnlyList<EarningNotice> OnTournament(EarnerRef winner)
    {
        if (winner == null) return NoNotices;
        return CreditEach(Eligible(winner, new[] { winner }), "tournament", _coop.IsSessionActive,
            e => _service.EarnFromTournament(e.HeroId, e.KingdomId, e.CultureId));
    }

    /// <summary>
    /// <c>OnPrisonerTakenEvent</c> carries only the roster, no actor, so a session cannot say whose
    /// capture it was: nobody is paid until the capturing party is patched in (#967 C2).
    /// </summary>
    public IReadOnlyList<EarningNotice> OnPrisoners(EarnerRef local, int count)
    {
        if (_coop.IsSessionActive)
        {
            if (!_prisonersLogged)
            {
                _prisonersLogged = true;
                _logger.LogInfo(
                    "[SpecRes] Prisoner income is not credited during a co-op session: the capture event names " +
                    "no capturing party. Further occurrences are not logged.");
            }
            return NoNotices;
        }

        var earners = Eligible(local, null);
        if (earners.Count == 0 || count <= 0) return NoNotices;
        return CreditEach(earners, "prisoners", false,
            e => _service.EarnFromPrisoners(e.HeroId, e.KingdomId, e.CultureId, count));
    }

    /// <summary>
    /// Gives a hero never seen before their resource's starting amount, once. Never overwrites an
    /// earned or spent-to-zero balance: only a never-written (hero, resource) pair is touched (#133 P2).
    /// </summary>
    /// <param name="source">What the log says seeded it, for example <c>legacy save (Name)</c>.</param>
    public void SeedIfUntracked(string heroId, string kingdomId, string cultureId, string source)
    {
        var resource = _service.ResolveResource(kingdomId, cultureId);
        if (resource == null) return;

        // Gate on storage.Contains (true iff the (hero, resource) pair was ever written) — never
        // re-seed a spent-to-0 or kingdom-changed balance, only a never-seen pair.
        var alreadyTracked = _storage.Contains(heroId, resource.Id);
        if (!alreadyTracked && resource.StartingAmount > 0f)
        {
            _storage.Set(heroId, resource.Id, resource.StartingAmount);
            _logger.LogInfo($"SpecialResources: Seeded {resource.DisplayName} = {resource.StartingAmount} for {source}");
        }
        else if (!alreadyTracked)
        {
            // StartingAmount==0 — still record the pair so it isn't re-evaluated as "never seen."
            _storage.Set(heroId, resource.Id, 0f);
        }
    }

    public DailyOutcome OnDailyTick(DailyTickInput input)
    {
        var hero = input.Hero;
        var session = _coop.IsSessionActive;
        // The policy applies with or without a session: a dedicated server's placeholder MainHero earns nothing.
        if (!SpecialResourceEarnPolicy.MayEarn(session, _coop.IsAuthority, _dedicatedServer.IsDedicatedServer, hero.Control))
            return DailyOutcome.NotProcessed;
        if (session)
            SeedPlayer(hero);

        var kingdomId = hero.KingdomId;
        var cultureId = hero.CultureId;
        var outcome = new DailyOutcome { Processed = true, IsLocal = hero.IsLocalMainHero && !session };

        // Phase 9b deferred #133 P2 — desertion grace. On the FIRST daily tick of a campaign (a save load
        // or a new game), suppress desertion. This protects a player who saved at balance=0 from
        // immediate troop loss before they have any chance to earn income. The grace is one tick only,
        // spent here even when the hero has no resource, so the SECOND daily tick (one in-game day later)
        // applies desertion as normal. A session has no grace of its own yet: see the desertion branch.
        var inGracePeriod = !session && !_graceSpent;
        _graceSpent = true;

        var resource = _service.ResolveResource(kingdomId, cultureId);
        if (resource == null)
        {
            _logger.LogDebug($"[SpecRes] DailyTick: no resource for hero (kingdom='{kingdomId}', culture='{cultureId}')");
            return outcome;
        }
        outcome.ResourceName = resource.DisplayName;

        var ownedTowns = input.OwnedTowns;
        var troopUpkeep = input.TroopUpkeep;

        // The same breakdown the tick applies is what the player is told about, so the message and
        // the deduction cannot disagree (#558). Balance is read before AND after, the idiom
        // OnMapEventEnded already uses, because Add floors at 0: a bill larger than the balance is
        // absorbed silently unless the caller compares the two.
        var breakdown = _service.GetDailyBreakdown(hero.HeroId, kingdomId, cultureId, ownedTowns, troopUpkeep);
        var before = _service.GetCurrentAmount(hero.HeroId, kingdomId, cultureId);

        _service.ApplyDailyTick(hero.HeroId, kingdomId, cultureId, ownedTowns, troopUpkeep);

        var balance = _service.GetCurrentAmount(hero.HeroId, kingdomId, cultureId);
        if (session)
            _logger.LogInfo($"[SpecRes] DailyTick (co-op): {hero.HeroId} {before:F0}→{balance:F0} {resource.DisplayName}");

        // Only troops whose row carries a daily_upkeep count: a merchant-only row is not an upkeep
        // troop and must neither desert nor trigger the warnings (#558 finding 5). A troop whose
        // career-modified upkeep is zero is not in arrears either (#767).
        var hasUpkeepDue = breakdown.HasUpkeepDue;

        // The daily line and the overdraft line show on the grace tick too: the deduction happened,
        // only the desertion below is deferred by one day, and a line that goes quiet on the tick the
        // player loads into would hide the one deduction they most want explained.
        if (hasUpkeepDue)
        {
            outcome.DailyLine = SpecialResourceMessages.DailyUpkeep(resource.DisplayName, breakdown.Earning, breakdown.Upkeep, balance);
            outcome.DailyLineYellow = breakdown.Net < 0f;

            if (before + breakdown.Net < 0f)
            {
                outcome.Overdraft = SpecialResourceMessages.UpkeepOverdraft(resource.DisplayName, breakdown.Upkeep, before);
            }
        }

        if (balance <= 0f && hasUpkeepDue && !inGracePeriod)
        {
            // Desertion: the behavior removes the troops from the roster. Remote desertion stays off
            // during a session until the balance mirror and the notices land (#967 C4, C6); when C4 and
            // C6 turn remote desertion on, give each player the first-tick grace here.
            if (session)
                LogDesertionOff();
            else
                outcome.Desertions = _service.CalculateDesertion(hero.HeroId, kingdomId, cultureId, troopUpkeep);
        }
        else if (balance <= 0f && hasUpkeepDue && inGracePeriod)
        {
            _logger.LogInfo($"[SpecRes] DailyTick: desertion grace active (first tick after load) — {breakdown.UpkeepLines.Count} upkeep troop types spared this tick");
            outcome.GraceLogged = true;
        }
        else if (balance > 0f && hasUpkeepDue)
        {
            // Warn only when heading into a deficit: the next daily tick (steady-state — same
            // towns/party as this tick) would push the balance to zero or below, which is exactly the
            // threshold that triggers troop desertion. A low-but-stable balance (income covers
            // upkeep) needs no warning, and neither does a party with no upkeep troops: nothing can
            // desert, and the map-bar flag (SpecialResourceMapBarMixin) keys on the same three terms.
            if (balance + breakdown.Net <= 0f)
            {
                outcome.Warning = $"{resource.DisplayName} running out: {balance:F0} left, losing {-breakdown.Net:F0}/day";
            }
        }

        return outcome;
    }

    /// <summary>
    /// Charges the one-time recruit_cost to the right hero (no-op unless the troop carries one) and
    /// returns a notice only when the charged hero is this process's own player. RecruitChargePolicy owns
    /// the who-pays table.
    /// </summary>
    public RecruitChargeNotice ChargeRecruit(RecruitContext context, string troopId, int count)
    {
        var session = _coop.IsSessionActive;
        var payer = RecruitChargePolicy.ChoosePayer(
            isAuthority: _coop.IsAuthority, isDedicated: _dedicatedServer.IsDedicatedServer,
            sessionActive: session,
            hasScope: context.ScopedPartyId != null, context.ScopedLeader, context.Local);
        if (payer == null)
        {
            LogRecruitNotCharged(context.ScopedPartyId);
            return null;
        }

        // A guest's first charge must not run against an unseeded zero, or the starting amount would
        // never be granted.
        if (session)
            SeedPlayer(payer);

        var charged = _service.ChargeRecruitCost(payer.HeroId, payer.KingdomId, payer.CultureId, troopId, count);
        // The toast is for the player at this machine: a guest's charge is applied on the server silently,
        // including while a scope has swapped that guest in as MainHero.
        if (charged > 0f && payer.IsLocalMainHero && !session)
        {
            var resource = _service.ResolveResource(payer.KingdomId, payer.CultureId);
            return new RecruitChargeNotice(resource?.DisplayName, charged);
        }
        return null;
    }

    /// <summary>
    /// #967 C2: charges <c>upgrade_cost</c> for the upgrades Coop's server applied from one player's party
    /// screen and returns what left that player's balance. The client's queue never commits, because Coop's
    /// Done resets the screen first. Patch144 names the player and reads the lines.
    /// </summary>
    public float ChargeCoopUpgrades(EarnerRef upgrader, IReadOnlyList<CoopUpgradeLine> upgrades)
    {
        var payer = RecruitChargePolicy.ChoosePayer(
            isAuthority: _coop.IsAuthority, isDedicated: _dedicatedServer.IsDedicatedServer,
            sessionActive: _coop.IsSessionActive,
            hasScope: true, upgrader, mainHero: null);
        if (payer == null) return 0f;

        SeedPlayer(payer);
        var before = _service.GetCurrentAmount(payer.HeroId, payer.KingdomId, payer.CultureId);
        foreach (var upgrade in upgrades)
            _service.SpendForUpgrade(payer.HeroId, payer.KingdomId, payer.CultureId, upgrade.TargetTroopId, upgrade.Count);
        return before - _service.GetCurrentAmount(payer.HeroId, payer.KingdomId, payer.CultureId);
    }

    // Nobody was charged. A scoped recruit whose party or leader is gone is worth one warning; a
    // dedicated server's unscoped recruit is expected and gets one info line.
    private void LogRecruitNotCharged(string scopedPartyId)
    {
        if (scopedPartyId != null)
        {
            if (_recruitScopeLostLogged) return;
            _recruitScopeLostLogged = true;
            _logger.LogWarning(
                $"[SpecRes] Recruit not charged: party '{scopedPartyId}' was not found or has no leader hero. " +
                "Further occurrences are not logged.");
            return;
        }

        if (_recruitSuppressionLogged || !_dedicatedServer.IsDedicatedServer) return;
        _recruitSuppressionLogged = true;
        _logger.LogInfo(
            "[SpecRes] Dedicated server: a recruit with no scoped guest party is not charged to the " +
            "world-gen hero. Further occurrences are not logged.");
    }

    private void LogDesertionOff()
    {
        if (_desertionOffLogged) return;
        _desertionOffLogged = true;
        _logger.LogInfo(
            "[SpecRes] Co-op session: upkeep desertion is off for remote players until the balance mirror " +
            "and desertion notices land. Further occurrences are not logged.");
    }

    // A registered player's first server balance is the starting amount; the balance mirror reconciles
    // it later (#967 C4). Used by every session path before it touches the player's balance.
    private void SeedPlayer(EarnerRef player)
        => SeedIfUntracked(player.HeroId, player.KingdomId, player.CultureId, $"co-op player ({player.HeroId})");

    // Who may be credited for one event. With no session this is the local MainHero, refused on a
    // dedicated server with one log line. In a session it is the candidates the policy admits
    // (registered players), and a client admits nobody.
    private IReadOnlyList<EarnerRef> Eligible(EarnerRef local, IReadOnlyList<EarnerRef> candidates)
    {
        var session = _coop.IsSessionActive;
        var authority = _coop.IsAuthority;
        var dedicated = _dedicatedServer.IsDedicatedServer;

        if (!session)
        {
            if (local != null && SpecialResourceEarnPolicy.MayEarn(false, authority, dedicated, local.Control))
                return new[] { local };

            if (dedicated && !_suppressionLogged)
            {
                _suppressionLogged = true;
                _logger.LogInfo(
                    "[SpecRes] Dedicated server — earnings are not credited to the world-gen hero. " +
                    "Players earn on their own clients. Further occurrences are not logged.");
            }
            return NoEarners;
        }

        if (candidates == null || candidates.Count == 0) return NoEarners;

        List<EarnerRef> admitted = null;
        foreach (var candidate in candidates)
        {
            if (candidate == null) continue;
            if (!SpecialResourceEarnPolicy.MayEarn(true, authority, dedicated, candidate.Control))
                continue;
            (admitted ??= new List<EarnerRef>()).Add(candidate);
        }
        return admitted ?? NoEarners;
    }

    // Reads each earner's balance before and after the earn call and records a notice for a positive delta.
    private IReadOnlyList<EarningNotice> CreditEach(
        IReadOnlyList<EarnerRef> earners, string source, bool session, Action<EarnerRef> earn)
    {
        if (earners.Count == 0) return NoNotices;

        var notices = new List<EarningNotice>();
        foreach (var earner in earners)
        {
            if (session)
                SeedPlayer(earner);

            var before = _service.GetCurrentAmount(earner.HeroId, earner.KingdomId, earner.CultureId);
            earn(earner);
            if (session)
                _logger.LogInfo($"[SpecRes] Co-op {source}: credited hero {earner.HeroId}");

            // Deliberate display-only wording change (round-4 O1): "+N X from victory"
            // (was "+N X earned from victory") — unified with the other delta toasts.
            var notice = NoticeFor(earner, source, before, session);
            if (notice != null) notices.Add(notice);
        }
        return notices;
    }

    // Unified earning notice (round-4 O1): resolves the hero's resource, then reports the positive
    // delta earned across the earn call; a zero or negative delta stays silent. A session notice is
    // never local: nobody plays on the authority, and MainHero can be a guest there.
    private EarningNotice NoticeFor(EarnerRef earner, string source, float before, bool session)
    {
        var resource = _service.ResolveResource(earner.KingdomId, earner.CultureId);
        if (resource == null) return null;

        var earned = _service.GetCurrentAmount(earner.HeroId, earner.KingdomId, earner.CultureId) - before;
        if (earned > 0f)
            return new EarningNotice(earner.HeroId, resource.DisplayName, earned, source, earner.IsLocalMainHero && !session);
        return null;
    }
}
