using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.Diplomacy;
using TAOM.Features.Diplomacy.Models;
using TAOM.Features.Execution;
using TAOM.Features.WarChronicle.Ledger;
using TAOM.Features.WarOfTheRingMomentum;
using Culture = System.Globalization.CultureInfo;

namespace TAOM.Features.WarPledge;

/// <summary>
/// War Pledge (#1113, docs/features/war-pledge.md): a Neutral realm joins the War of the Ring for good.
/// <para>
/// <b>The join</b> writes the pledge first, so that <c>AlignmentService</c> and <c>DiplomacyService</c> give
/// the realm its new side and a Hostile tier against the other side before anything else runs. It then makes
/// peace with the realms of the new side and declares war on each realm of the other side that the War of the
/// Ring holds at war with it (<c>ShouldBlockPeace</c>, the test the Full War reconcile uses). The three
/// peace-block layers lock those wars because they read the same tier. Writing the pledge before the
/// declarations follows Full War's own order (#772): vanilla's call-to-war pass then sees every realm of the
/// other side as already at war with the pledged realm, so it asks none of the target's allies on that side
/// to join against it.
/// </para>
/// <para>
/// <b>The pledged realm declares.</b> Vanilla charges a broken alliance or trade agreement to the ruler of the
/// kingdom that declares (v1.5.7 <c>AllianceCampaignBehavior.ApplyBrokenAlliancePenalty</c>: -100 relation;
/// <c>TradeAgreementsCampaignBehavior.ApplyBrokenTradeAgreementPenalty</c>: -50; a trait hit each when that ruler
/// is the player). The Full War script declares in kingdom-list order, which would charge whichever realm
/// sorts first. The realm that pledged broke them, so it is the declarer. The War of the Ring's reconcile at
/// session launch retries a declaration this join could not make, in list order: that retry is not charged
/// to the pledged realm.
/// </para>
/// <para>
/// <b>The AI's courting</b> runs once a day in Full War, on the authority (the behavior gates that). The side
/// that is behind on the momentum balance by <see cref="WarPledgeConfig.LosingBalance"/> takes the first realm
/// of the lean table that leans its way and can join. It never takes a true neutral, a realm that leans to
/// the other side, or a realm a player rules, and it waits <see cref="WarPledgeConfig.DaysBetweenAiJoins"/>
/// days after the first Full War day and after each join.
/// </para>
/// Both run from a tick or a console command, never from a war or peace event
/// (lessons/campaign-mechanics.md, #832).
/// </summary>
public sealed class WarPledgeService
{
    /// <summary>The in-game check line of a join (tools/ingame_checks/1113.toml); its key is the kingdom id.</summary>
    public const string JoinedCheckId = "taom.war-pledge.joined";

    private readonly WarPledgeStore _store;
    private readonly WarPledgeConfig _config;
    private readonly IWarPledgeSettings _settings;
    private readonly IMomentumSettingsProvider _momentumSettings;
    private readonly IMomentumQueryService _momentum;
    private readonly IAlignmentService _alignment;
    private readonly IDiplomacyService _diplomacy;
    private readonly IWarOfTheRingService _wotr;
    private readonly IAllianceAdapter _alliance;
    private readonly IWarPledgeWorldAdapter _world;
    private readonly WarLedgerService _ledger;
    private readonly IOutcomeLog _outcomes;
    private readonly IModLogger _logger;
    private readonly HashSet<string> _warnedLeans = new HashSet<string>(StringComparer.Ordinal);

    public WarPledgeService(
        WarPledgeStore store,
        IWarPledgeConfigProvider configProvider,
        IWarPledgeSettings settings,
        IMomentumSettingsProvider momentumSettings,
        IMomentumQueryService momentum,
        IAlignmentService alignment,
        IDiplomacyService diplomacy,
        IWarOfTheRingService wotr,
        IAllianceAdapter alliance,
        IWarPledgeWorldAdapter world,
        WarLedgerService ledger,
        IOutcomeLog outcomes,
        IModLogger logger)
    {
        _store = store;
        _config = configProvider.LoadConfig();
        _settings = settings;
        _momentumSettings = momentumSettings;
        _momentum = momentum;
        _alignment = alignment;
        _diplomacy = diplomacy;
        _wotr = wotr;
        _alliance = alliance;
        _world = world;
        _ledger = ledger;
        _outcomes = outcomes;
        _logger = logger;
    }

    /// <summary>
    /// Pledges the realm to the side, or says in <paramref name="reason"/> why it cannot. A dry run goes
    /// through every check and changes nothing. The MCM switch, the lean table and the player-ruled rule do
    /// not bind this method: they bind the AI's courting, and the console may join any realm that passes
    /// the checks.
    /// </summary>
    public bool TryJoin(string? kingdomId, FactionSide side, bool byAi, bool dryRun, out string reason)
    {
        if (!CanJoin(kingdomId, side, out var live, out reason))
            return false;

        var id = kingdomId!;
        if (dryRun)
        {
            reason = $"{id} would pledge to the {side} side.";
            return true;
        }

        if (!_store.Pledge(id, side))
        {
            reason = $"the pledge of {id} could not be stored.";
            return false;
        }

        // Sides here are by kingdom id, as in DiplomacyService.IsWarAllowed: a kingdom that alignment.json
        // does not list (a player-founded realm) is on no side for the wars, so the join makes no peace and
        // no war with it. The player's wars stay the player's own, as under the Full War rules.
        int peace = 0, stuck = 0, expected = 0, wars = 0;
        var undeclared = new List<string>();
        foreach (var other in live)
        {
            if (other == id || _alignment.GetKingdomSide(other) != side || !_alliance.AreAtWar(id, other))
                continue;
            _alliance.MakePeace(id, other);
            if (_alliance.AreAtWar(id, other))
                stuck++;
            else
                peace++;
        }

        foreach (var other in live)
        {
            if (other == id || !FactionSideRules.AreOpposed(side, _alignment.GetKingdomSide(other)))
                continue;
            // The pairs the War of the Ring holds at war. A Permanent row, or blockPeaceBetweenHostileTiers
            // switched off in war_of_the_ring.json, takes a pair out, and then no war is owed there. This
            // does not read autoWarBetweenHostileTiers: the model already holds such a pair at war, and only
            // a real declaration stores the War stance and fires WarDeclared, which rebuilds both realms'
            // at-war lists and ends an alliance or trade agreement with the target.
            if (!_wotr.ShouldBlockPeace(id, other))
                continue;
            expected++;
            if (!_alliance.HasDeclaredWar(id, other))
                _alliance.DeclareWar(id, other);
            if (_alliance.HasDeclaredWar(id, other))
                wars++;
            else
                undeclared.Add(other);
        }

        _ledger.LogPledge(id, side, byAi);

        var by = byAi ? "ai" : "console";
        var detail = string.Format(
            Culture.InvariantCulture, "side={0} by={1} wars={2} expected={3} peace={4} stuck={5}",
            WarLedgerFormatter.SideName(side), by, wars, expected, peace, stuck);
        if (wars == expected && stuck == 0)
            _outcomes.Pass(JoinedCheckId, id, detail);
        else
            _outcomes.Fail(JoinedCheckId, id, detail);

        try
        {
            _world.AnnouncePledge(id, side);
        }
        catch (Exception ex)
        {
            _outcomes.Swallowed("WarPledge", "announce", ex);
        }

        reason = $"{id} pledged to the {side} side ({by}): {wars} of {expected} wars declared, "
                 + $"peace made with {peace} realm(s) of its side"
                 + (stuck > 0 ? $", {stuck} war(s) with its side did not end." : ".")
                 + (undeclared.Count > 0 ? $" Not declared: {string.Join(", ", undeclared)}." : string.Empty);
        _logger.LogInfo("[WarPledge] " + reason);
        return true;
    }

    private bool CanJoin(string? kingdomId, FactionSide side, out IReadOnlyList<string> live, out string reason)
    {
        live = Array.Empty<string>();
        if (string.IsNullOrWhiteSpace(kingdomId))
        {
            reason = "no kingdom id was given.";
            return false;
        }

        var id = kingdomId!;
        if (side == FactionSide.Neutral)
        {
            reason = "a realm pledges to the Free side or to the Evil side.";
            return false;
        }

        if (!_wotr.IsEnabled || _wotr.CurrentPhase != WarPhase.FullWar)
        {
            reason = "a realm can pledge only while the War of the Ring is on and in Full War "
                     + $"(now: {(_wotr.IsEnabled ? "on" : "off")}, {_wotr.CurrentPhase}).";
            return false;
        }

        live = _alliance.GetAllKingdomIds();
        if (!live.Contains(id))
        {
            reason = $"no live kingdom has the id '{id}'.";
            return false;
        }

        if (_store.TryGetSide(id, out var pledged))
        {
            reason = $"{id} already pledged to the {pledged} side.";
            return false;
        }

        var current = _alignment.GetKingdomSide(id);
        if (current != FactionSide.Neutral)
        {
            reason = $"{id} is a member of the {current} side.";
            return false;
        }

        // A kingdom that alignment.json does not list has a side through its culture for momentum, the
        // ledger and the map (ResolveSide). It is not a Neutral realm, so it cannot pledge either way.
        var byCulture = _alignment.ResolveSide(id, _alliance.GetKingdomCultureId(id));
        if (byCulture != FactionSide.Neutral)
        {
            reason = $"{id} is on the {byCulture} side through its culture.";
            return false;
        }

        // A realm cannot stand beside a realm it has a Hostile row with (Umbar and Gondor). The realm has no
        // pledge yet, so the tier read here is the table's own, also against a realm that holds a pledge.
        foreach (var other in live)
        {
            if (other == id || _alignment.GetKingdomSide(other) != side)
                continue;
            if (_diplomacy.GetRelationshipTier(id, other) == AllianceTier.Hostile)
            {
                reason = $"{id} is Hostile to {other}, a realm of the {side} side.";
                return false;
            }
        }

        reason = string.Empty;
        return true;
    }

    /// <summary>The AI's daily step. <paramref name="day"/> is the whole campaign day; the caller is the authority.</summary>
    public void RunDailyCourting(int day)
    {
        if (day < 0 || !_settings.Enabled || !_momentumSettings.MomentumEnabled)
            return;
        if (!_wotr.IsEnabled || _wotr.CurrentPhase != WarPhase.FullWar)
            return;

        if (_store.NextCourtingDay == WarPledgeStore.NoCourtingDay)
        {
            _store.NextCourtingDay = DayAfterTheWait(day);
            _logger.LogInfo($"[WarPledge] Full War on day {day}: the AI courts no realm before day {_store.NextCourtingDay}.");
            return;
        }

        if (day < _store.NextCourtingDay)
            return;

        var losing = LosingSide(_momentum.SliderValue);
        if (losing == FactionSide.Neutral)
            return;

        var live = _alliance.GetAllKingdomIds();
        foreach (var lean in _config.Leans)
        {
            if (lean.Lean != losing || _store.IsPledged(lean.KingdomId))
                continue;
            if (!live.Contains(lean.KingdomId))
            {
                WarnLeanOnce(lean.KingdomId);
                continue;
            }
            if (_world.IsPlayerRuled(lean.KingdomId))
                continue;

            // The wait starts when the pledge is stored, also when the peace or a declaration after it
            // throws (a campaign listener inside the action): "one realm per wait" holds on that path too.
            // The realm had no pledge before the call, so a pledge after it is this call's.
            var joined = false;
            try
            {
                joined = TryJoin(lean.KingdomId, losing, byAi: true, dryRun: false, out _);
            }
            finally
            {
                if (_store.IsPledged(lean.KingdomId))
                    _store.NextCourtingDay = DayAfterTheWait(day);
            }

            if (joined)
                return;
        }
    }

    /// <summary>What the console's status command prints: the switches, the balance, and one row per realm.</summary>
    public WarPledgeStatus Status()
    {
        var live = _alliance.GetAllKingdomIds();
        var rows = new List<WarPledgeStatusRow>();
        var listed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var lean in _config.Leans)
        {
            listed.Add(lean.KingdomId);
            rows.Add(Row(lean.KingdomId, lean.Lean, live));
        }
        foreach (var pledge in _store.Snapshot())
        {
            if (!listed.Contains(pledge.Key))
                rows.Add(Row(pledge.Key, null, live));
        }

        var balance = _momentum.SliderValue;
        return new WarPledgeStatus(
            _settings.Enabled, _momentumSettings.MomentumEnabled, _wotr.IsEnabled, _wotr.CurrentPhase,
            balance, LosingSide(balance), _config.LosingBalance, _config.DaysBetweenAiJoins, _store.NextCourtingDay, rows);
    }

    private WarPledgeStatusRow Row(string kingdomId, FactionSide? lean, IReadOnlyList<string> live)
    {
        _store.TryGetSide(kingdomId, out var pledged);
        var isLive = live.Contains(kingdomId);
        return new WarPledgeStatusRow(kingdomId, lean, pledged, isLive, isLive && _world.IsPlayerRuled(kingdomId));
    }

    // The balance is the map meter: -100 to +100, positive when the Free Peoples lead. Neutral: no side courts.
    private FactionSide LosingSide(int balance)
    {
        if (balance <= -_config.LosingBalance)
            return FactionSide.Free;
        return balance >= _config.LosingBalance ? FactionSide.Evil : FactionSide.Neutral;
    }

    private int DayAfterTheWait(int day) =>
        day > int.MaxValue - _config.DaysBetweenAiJoins ? int.MaxValue : day + _config.DaysBetweenAiJoins;

    // Once per process: the id is mistyped in war_pledge.json, or the realm was destroyed.
    private void WarnLeanOnce(string kingdomId)
    {
        if (_warnedLeans.Add(kingdomId))
            _logger.LogWarning($"[WarPledge] lean row '{kingdomId}' skipped: no live kingdom has that id (check war_pledge.json, or the realm was destroyed).");
    }
}

/// <summary>The state the console's status command prints.</summary>
public sealed class WarPledgeStatus
{
    public WarPledgeStatus(
        bool enabled, bool momentumEnabled, bool warOfTheRingEnabled, WarPhase phase, int balance,
        FactionSide courtingSide, int losingBalance, int daysBetweenAiJoins, int nextCourtingDay,
        IReadOnlyList<WarPledgeStatusRow> rows)
    {
        Enabled = enabled;
        MomentumEnabled = momentumEnabled;
        WarOfTheRingEnabled = warOfTheRingEnabled;
        Phase = phase;
        Balance = balance;
        CourtingSide = courtingSide;
        LosingBalance = losingBalance;
        DaysBetweenAiJoins = daysBetweenAiJoins;
        NextCourtingDay = nextCourtingDay;
        Rows = rows;
    }

    public bool Enabled { get; }

    public bool MomentumEnabled { get; }

    public bool WarOfTheRingEnabled { get; }

    public WarPhase Phase { get; }

    public int Balance { get; }

    /// <summary>The side the balance stands far enough against to court; Neutral when neither does.</summary>
    public FactionSide CourtingSide { get; }

    public int LosingBalance { get; }

    public int DaysBetweenAiJoins { get; }

    public int NextCourtingDay { get; }

    public IReadOnlyList<WarPledgeStatusRow> Rows { get; }
}

/// <summary>One realm of the status: a lean row, or a realm the console pledged that has none.</summary>
public sealed class WarPledgeStatusRow
{
    public WarPledgeStatusRow(string kingdomId, FactionSide? lean, FactionSide pledgedSide, bool isLive, bool isPlayerRuled)
    {
        KingdomId = kingdomId;
        Lean = lean;
        PledgedSide = pledgedSide;
        IsLive = isLive;
        IsPlayerRuled = isPlayerRuled;
    }

    public string KingdomId { get; }

    /// <summary>Null for a realm with no lean row.</summary>
    public FactionSide? Lean { get; }

    /// <summary>Neutral while the realm has not pledged.</summary>
    public FactionSide PledgedSide { get; }

    public bool IsLive { get; }

    public bool IsPlayerRuled { get; }
}
