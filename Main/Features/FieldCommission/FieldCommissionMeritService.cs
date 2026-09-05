using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Adapters;
using TAOM.Core.Domain;
using TAOM.Core.Logging;
using TAOM.Features.FieldCommission.Domain;

namespace TAOM.Features.FieldCommission;

public class FieldCommissionMeritService : IFieldCommissionMeritService
{
    private readonly ITroopRosterQueryAdapter _roster;
    private readonly IRaceManager _raceManager;
    private readonly IFieldCommissionConfigProvider _configProvider;
    private readonly TroopUpgradeGraph _upgradeGraph;
    private readonly IModLogger _logger;

    private readonly Dictionary<string, int> _merits = new Dictionary<string, int>(StringComparer.Ordinal);

    /// <summary>Merit level at which the player last said "not yet" to this troop type. The type has
    /// to earn another full threshold above that mark before it may ask again — otherwise, since
    /// merit only ever grows, the same soldier is re-offered after every won battle forever.</summary>
    private Dictionary<string, int> _declinedAt = new Dictionary<string, int>(StringComparer.Ordinal);

    private readonly Dictionary<string, int> _battleKills = new Dictionary<string, int>(StringComparer.Ordinal);
    private readonly HashSet<string> _preBattleRoster = new HashSet<string>(StringComparer.Ordinal);
    private readonly Queue<PendingPromotionOffer> _pendingOffers = new Queue<PendingPromotionOffer>();
    private readonly List<string> _promotedHeroIds = new List<string>();

    private bool _eligible;

    public FieldCommissionMeritService(
        ITroopRosterQueryAdapter roster,
        IRaceManager raceManager,
        IFieldCommissionConfigProvider configProvider,
        TroopUpgradeGraph upgradeGraph,
        IModLogger logger)
    {
        _roster = roster;
        _raceManager = raceManager;
        _configProvider = configProvider;
        _upgradeGraph = upgradeGraph;
        _logger = logger;
    }

    public float ComputeRatio(int playerHealthyCount, int enemyHealthyCount) =>
        Math.Max(0, playerHealthyCount) / (float)Math.Max(1, enemyHealthyCount);

    public bool IsBattleEligible(float ratio, float ratioThreshold)
    {
        // Positive-requirement polarity: NaN in either argument makes the comparison false, so a
        // degenerate value fails the gate rather than sneaking an owned "eligible" verdict through.
        if (float.IsNaN(ratio) || float.IsNaN(ratioThreshold))
            return false;

        return ratio < ratioThreshold;
    }

    public void BeginBattle(bool eligible)
    {
        _battleKills.Clear();
        _preBattleRoster.Clear();
        _eligible = eligible;

        if (!eligible)
            return;

        foreach (var pair in _roster.GetRosterSnapshot())
        {
            if (pair.Value > 0)
                _preBattleRoster.Add(pair.Key);
        }

        if (Tracing) WriteTrace($"battle started | eligible=true trackedTypes={_preBattleRoster.Count}");
    }

    public void RegisterKill(string troopId)
    {
        if (!_eligible || string.IsNullOrEmpty(troopId) || !_preBattleRoster.Contains(troopId))
            return;

        _battleKills.TryGetValue(troopId, out var current);
        _battleKills[troopId] = current + 1;
    }

    public IReadOnlyList<PendingPromotionOffer> EndBattle(bool won)
    {
        try
        {
            var killedTypes = _battleKills.Count;
            if (!_eligible || !won || killedTypes == 0)
            {
                if (Tracing) WriteTrace($"battle ended | eligible={_eligible} won={won} killedTypes={killedTypes} — nothing scored");
                return Array.Empty<PendingPromotionOffer>();
            }

            // A stack that upgraded tiers between battles carries its merit under the OLD troop
            // id, orphaned. Move it onto a present descendant BEFORE this battle's kills are
            // scored, so the troops that killed fastest (and levelled fastest) aren't the ones
            // whose merit gets silently stranded.
            ConsolidateOrphanedMerits();

            var config = _configProvider.GetConfig();
            var threshold = Math.Max(1, config.MeritThreshold);
            // Every offer is a separate modal inquiry that pauses the game, and the pump shows the
            // next one on the following tick — so an uncapped scan turns one big won battle into a
            // wall of dialogs the player cannot dismiss in bulk. Cap the BATTLE, not the troop:
            // merit above the cap stays banked and re-queues after the next won battle.
            var offerBudget = Math.Max(1, config.MaxOffersPerBattle);
            var queued = new List<PendingPromotionOffer>();

            foreach (var pair in _battleKills.OrderByDescending(p => p.Value).ToList())
            {
                var troopId = pair.Key;
                var kills = pair.Value;
                var currentCount = _roster.GetTroopCount(troopId);

                if (currentCount <= 0)
                {
                    // The type isn't in the roster anymore (fully upgraded or wiped this battle).
                    // Always bank the merit first, then try to pass it (this battle's kills
                    // included) to a present upgraded descendant before dropping it.
                    var heir = _upgradeGraph.FindDescendantInRoster(troopId, GetUpgradeTargetIds, IsCountedInRoster);
                    AwardMerit(troopId, kills);
                    if (heir != null)
                        TransferMerit(troopId, heir);
                    else
                        ForgetTroop(troopId);
                    continue;
                }

                // Always bank this troop's merit — an early continue here would permanently
                // discard kills for every troop type processed after a promotable one (bug class
                // avoided, not present in TAOM's version, but the ordering is deliberate).
                AwardMerit(troopId, kills);

                if (!CanPromote(troopId))
                    continue; // merit still banks; just no offer while this troop isn't promotable

                // Deliberately NOT an early exit once the budget is spent: the loop must keep running
                // so the remaining troop types still bank their kills above. The donor mod learned
                // this the hard way — its early `return` discarded the merit of every troop processed
                // after the first promotable one.
                var remainingBudget = offerBudget - queued.Count;
                if (remainingBudget <= 0)
                    continue;

                if (IsDeclineSuppressed(troopId, threshold))
                    continue;

                var info = _roster.GetTroopInfo(troopId);

                // Subtract merit already committed to offers the player has not answered yet.
                // Without this the same bank backs several offers: two won battles inside one
                // uninterrupted encounter (siege sally-outs do exactly this) each queue from the raw
                // total, and accepting them all deducts less than was promised because CompleteOffer
                // clamps at zero.
                var outstanding = CountQueuedOffers(troopId);
                var available = GetMerit(troopId) - (outstanding * threshold);
                var possibleOffers = Math.Min(Math.Min(currentCount, Math.Max(0, available) / threshold), remainingBudget);

                for (var i = 0; i < possibleOffers; i++)
                {
                    var offer = new PendingPromotionOffer(troopId, string.IsNullOrEmpty(info.Name) ? troopId : info.Name);
                    _pendingOffers.Enqueue(offer);
                    queued.Add(offer);
                }

                if (possibleOffers > 0)
                    if (Tracing) WriteTrace($"offer queued | troop={troopId} count={possibleOffers} merit={GetMerit(troopId)} threshold={threshold}");
            }

            if (Tracing) WriteTrace($"battle scored | won=true kills={killedTypes} offersQueued={queued.Count} budget={offerBudget}");
            return queued;
        }
        finally
        {
            _battleKills.Clear();
            _preBattleRoster.Clear();
            _eligible = false;
        }
    }

    public bool HasPendingOffers => _pendingOffers.Count > 0;

    public void ClearPendingOffers() => _pendingOffers.Clear();

    public bool TryDequeueOffer(out PendingPromotionOffer offer)
    {
        if (_pendingOffers.Count == 0)
        {
            offer = null;
            return false;
        }

        offer = _pendingOffers.Dequeue();
        return true;
    }

    public void CompleteOffer(string troopId)
    {
        if (string.IsNullOrEmpty(troopId))
            return;

        var threshold = Math.Max(1, _configProvider.GetConfig().MeritThreshold);
        _merits[troopId] = Math.Max(0, GetMerit(troopId) - threshold);

        // Accepting clears any previous refusal — the player has changed their mind about this troop
        // type, so the next one they distinguish should be offered on its own merits.
        _declinedAt.Remove(troopId);
        if (Tracing) WriteTrace($"promotion completed | troop={troopId} meritLeft={GetMerit(troopId)}");
    }

    public void RecordDeclinedOffer(string troopId)
    {
        if (string.IsNullOrEmpty(troopId))
            return;

        _declinedAt[troopId] = GetMerit(troopId);
        if (Tracing) WriteTrace($"offer declined | troop={troopId} markedAt={GetMerit(troopId)}");
    }

    /// <summary>
    /// True while this troop type has not earned a further full threshold since the player last
    /// declined it. Merit keeps accruing throughout — the refusal delays the next ask, it never
    /// costs the soldiers anything.
    /// </summary>
    private bool IsDeclineSuppressed(string troopId, int threshold) =>
        _declinedAt.TryGetValue(troopId, out var mark) && GetMerit(troopId) < mark + threshold;

    private int CountQueuedOffers(string troopId)
    {
        var count = 0;
        foreach (var offer in _pendingOffers)
        {
            if (offer != null && string.Equals(offer.TroopId, troopId, StringComparison.Ordinal))
                count++;
        }
        return count;
    }

    /// <summary>
    /// Checked at every call site BEFORE building the message. An interpolated string argument is
    /// evaluated before the callee runs, so a gate inside <see cref="WriteTrace"/> would still pay
    /// for the string on every call with diagnostics off — including once per troop type per battle.
    /// </summary>
    private bool Tracing => _configProvider.GetConfig().Diagnostics;

    private void WriteTrace(string message) => _logger?.LogInfo("[FieldCommission] " + message);

    public int GetMerit(string troopId) =>
        !string.IsNullOrEmpty(troopId) && _merits.TryGetValue(troopId, out var value) ? value : 0;

    public void GrantMerit(string troopId, int amount)
    {
        if (string.IsNullOrEmpty(troopId) || amount <= 0)
            return;

        _merits[troopId] = GetMerit(troopId) + amount;
    }

    public bool CanPromote(string troopId)
    {
        if (string.IsNullOrEmpty(troopId))
            return false;

        var info = _roster.GetTroopInfo(troopId);
        if (info.IsMissing || info.IsHero || info.IsPrisonGuard)
            return false;

        // Validate-before-lookup: an unresolvable race id must fail closed, never fall through to
        // whatever default name the lookup happens to return.
        if (!_raceManager.IsValidRaceId(info.RaceId))
            return false;

        var raceName = _raceManager.GetRaceNameFromId(info.RaceId);
        var allowed = _configProvider.GetConfig().AllowedRaceNames;
        return allowed != null && allowed.Any(name => string.Equals(name, raceName, StringComparison.OrdinalIgnoreCase));
    }

    public void RecordPromotedHero(string heroId)
    {
        if (!string.IsNullOrEmpty(heroId) && !_promotedHeroIds.Contains(heroId))
            _promotedHeroIds.Add(heroId);
    }

    public IReadOnlyList<string> GetPromotedHeroIds() => _promotedHeroIds;

    public IReadOnlyList<string> PruneDeadPromotedHeroes(Func<string, bool> isAliveAndValid)
    {
        if (isAliveAndValid == null)
            return _promotedHeroIds;

        var dropped = _promotedHeroIds.Where(id => !isAliveAndValid(id)).ToList();
        _promotedHeroIds.RemoveAll(id => !isAliveAndValid(id));

        if (dropped.Count > 0)
            _logger?.LogInfo($"[FieldCommission] pruned {dropped.Count} dead/invalid promoted-hero id(s) on load");

        return _promotedHeroIds;
    }

    public void ForgetPromotedHero(string heroId)
    {
        if (!string.IsNullOrEmpty(heroId))
            _promotedHeroIds.Remove(heroId);
    }

    public Dictionary<string, int> ExportMerits() => new Dictionary<string, int>(_merits, StringComparer.Ordinal);

    public void ImportMerits(Dictionary<string, int> merits)
    {
        _merits.Clear();
        if (merits == null)
            return;

        foreach (var pair in merits)
            _merits[pair.Key] = pair.Value;
    }

    public Dictionary<string, int> ExportDeclinedMarks() => new Dictionary<string, int>(_declinedAt, StringComparer.Ordinal);

    public void ImportDeclinedMarks(Dictionary<string, int> marks)
    {
        _declinedAt = new Dictionary<string, int>(StringComparer.Ordinal);
        if (marks == null)
            return;

        foreach (var pair in marks)
        {
            if (!string.IsNullOrEmpty(pair.Key))
                _declinedAt[pair.Key] = pair.Value;
        }
    }

    public List<string> ExportPromotedHeroIds() => new List<string>(_promotedHeroIds);

    public void ImportPromotedHeroIds(List<string> ids)
    {
        _promotedHeroIds.Clear();
        if (ids == null)
            return;

        foreach (var id in ids)
        {
            if (!string.IsNullOrEmpty(id) && !_promotedHeroIds.Contains(id))
                _promotedHeroIds.Add(id);
        }
    }

    private void ConsolidateOrphanedMerits()
    {
        if (_merits.Count == 0)
            return;

        foreach (var troopId in _merits.Keys.ToList())
        {
            var info = _roster.GetTroopInfo(troopId);
            if (info.IsMissing)
            {
                ForgetTroop(troopId);
                continue;
            }

            if (_roster.GetTroopCount(troopId) > 0)
                continue; // still present — nothing to consolidate

            var heir = _upgradeGraph.FindDescendantInRoster(troopId, GetUpgradeTargetIds, IsCountedInRoster);
            if (heir != null)
                TransferMerit(troopId, heir);
            // else: leave parked — the player may still hold this type in a garrison and re-add it.
        }
    }

    private void TransferMerit(string fromTroopId, string toTroopId)
    {
        var merit = GetMerit(fromTroopId);
        ForgetTroop(fromTroopId);
        if (merit > 0)
            _merits[toTroopId] = GetMerit(toTroopId) + merit;
    }

    /// <summary>
    /// Drops everything keyed on this troop type. The decline mark MUST go with the merit: it is an
    /// absolute merit level, so leaving it behind after the bank is emptied or moved to an heir
    /// measures the next offer against a total the type no longer has — and since a mark is only
    /// cleared by a completed promotion, the type could never be offered again.
    /// </summary>
    private void ForgetTroop(string troopId)
    {
        _merits.Remove(troopId);
        _declinedAt.Remove(troopId);
    }

    private void AwardMerit(string troopId, int kills)
    {
        if (kills <= 0)
            return;

        var perKill = Math.Max(1, _configProvider.GetConfig().MeritPerKill);
        _merits[troopId] = GetMerit(troopId) + (kills * perKill);
        if (Tracing) WriteTrace($"merit banked | troop={troopId} kills={kills} perKill={perKill} total={_merits[troopId]}");
    }

    private IReadOnlyList<string> GetUpgradeTargetIds(string troopId) => _roster.GetUpgradeTargetIds(troopId);

    private bool IsCountedInRoster(string troopId) => _roster.GetTroopCount(troopId) > 0;
}
