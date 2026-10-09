using System;
using System.Collections.Generic;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Core.Validation;
using TAOM.Features.Execution;
using TAOM.Features.WarChronicle.Effects;
using TAOM.Features.WarChronicle.Ledger;

namespace TAOM.Features.WarChronicle.Rally;

/// <summary>
/// The rally (docs/features/war-chronicle.md, Part C): a small, temporary catch-up for AI kingdoms that
/// have lost a large share of their fortification points. Each day, for every kingdom: loss is measured
/// against its baseline, the hysteresis machine moves its tier, and a kingdom that is AI-ruled, at war and
/// not excluded as Neutral gets that tier's volunteer and escape effects as source
/// <c>rally:&lt;kingdom&gt;</c>, ending <c>effectTtlHours</c> from now. Anything else loses its rally
/// source, so a tier drop, the end of a war, a kingdom leaving the map or the switch turned off all clear
/// the effects the same way.
///
/// The tier is tracked for every kingdom with a baseline, switched on or off, eligible or not, so the
/// ledger of a rally-off control run shows what would have fired. A kingdom without a baseline (or with a
/// baseline of zero) has no loss to measure and no tier. The tiers live in <see cref="RallyTierStore"/>
/// (saved and reset with the other campaign state), so this service holds none and needs no reset of its
/// own. Reads the day's snapshots as data, so no engine type reaches it. Host only: the caller has
/// already checked that this peer may run world work. A failure is logged, never thrown.
/// </summary>
public sealed class RallyService
{
    internal const string SourcePrefix = "rally:";

    private readonly WarBaselineService _baselines;
    private readonly RallyTierStore _tiers;
    private readonly IWarEffectService _effects;
    private readonly IRallyConfigProvider _configProvider;
    private readonly IWarChronicleSettingsProvider _settings;
    private readonly IAlignmentService _alignment;
    private readonly WarLedgerService _ledger;
    private readonly IModLogger _logger;

    public RallyService(
        WarBaselineService baselines, RallyTierStore tiers, IWarEffectService effects,
        IRallyConfigProvider configProvider, IWarChronicleSettingsProvider settings,
        IAlignmentService alignment, WarLedgerService ledger, IModLogger logger)
    {
        _baselines = baselines;
        _tiers = tiers;
        _effects = effects;
        _configProvider = configProvider;
        _settings = settings;
        _alignment = alignment;
        _ledger = ledger;
        _logger = logger;
    }

    /// <summary>
    /// The rally writes effects only when both switches are on: <c>enabled</c> in rally.json and the MCM
    /// <c>WarRallyEnabled</c>. The tiers are tracked either way.
    /// </summary>
    public bool IsActive => _configProvider.GetConfig().Enabled && _settings.WarRallyEnabled;

    public void RunDaily(IReadOnlyList<KingdomWarSnapshot> kingdoms, double nowHours)
    {
        try
        {
            Run(kingdoms, nowHours);
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"[WarChronicle] Rally skipped ({ex.GetType().Name}: {ex.Message}).");
        }
    }

    /// <summary>One row per kingdom for the console; reads the stored tier, so it shows what the last tick decided.</summary>
    public IReadOnlyList<RallyStatusRow> Report(IReadOnlyList<KingdomWarSnapshot> kingdoms)
    {
        var rows = new List<RallyStatusRow>();
        if (kingdoms == null)
            return rows;

        var config = _configProvider.GetConfig();
        foreach (var kingdom in kingdoms)
        {
            if (kingdom == null || string.IsNullOrEmpty(kingdom.Id))
                continue;

            var baseline = _baselines.GetBaseline(kingdom.Id);
            rows.Add(new RallyStatusRow
            {
                KingdomId = kingdom.Id,
                Points = kingdom.FortificationPoints,
                Baseline = baseline,
                Loss = WarBaselineService.LossOf(baseline, kingdom.FortificationPoints),
                Tier = _tiers.GetTier(kingdom.Id),
                Eligible = IsEligible(kingdom, config),
            });
        }

        return rows;
    }

    private void Run(IReadOnlyList<KingdomWarSnapshot> kingdoms, double nowHours)
    {
        // An empty read is a glitch, not a map with no kingdoms: acting on it would wipe every tier.
        if (kingdoms == null || kingdoms.Count == 0)
            return;

        if (!FiniteFloatValidator.IsFinite(nowHours))
        {
            _logger.LogWarning("[WarChronicle] Rally skipped: the campaign clock is not a finite number.");
            return;
        }

        var config = _configProvider.GetConfig();
        var active = IsActive;
        var endTime = nowHours + config.EffectTtlHours;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var kept = new HashSet<string>(StringComparer.Ordinal);

        foreach (var kingdom in kingdoms)
        {
            if (kingdom == null || string.IsNullOrEmpty(kingdom.Id) || !seen.Add(kingdom.Id))
                continue;

            var loss = WarBaselineService.LossOf(_baselines.GetBaseline(kingdom.Id), kingdom.FortificationPoints);
            var before = _tiers.GetTier(kingdom.Id);
            var after = loss.HasValue ? RallyTierMachine.Next(before, loss.Value, config) : 0;
            if (after != before)
            {
                _tiers.SetTier(kingdom.Id, after);
                _ledger.LogTierChange(kingdom.Id, before, after, loss ?? 0f);
            }

            if (!active || after == 0 || !IsEligible(kingdom, config))
                continue;

            var source = SourcePrefix + kingdom.Id;
            if (after != before)
                _effects.RemoveSource(source);

            var tier = after == 2 ? config.Tier2 : config.Tier1;
            Apply(source, kingdom.Id, WarEffectKind.VolunteerRate, tier.VolunteerRate, endTime);
            Apply(source, kingdom.Id, WarEffectKind.PrisonerEscape, tier.PrisonerEscape, endTime);
            kept.Add(source);
        }

        DropTiersOfKingdomsThatLeftTheMap(seen);
        RemoveRallySourcesNotIn(kept);
    }

    private void Apply(string source, string kingdomId, WarEffectKind kind, float magnitude, double endTime)
    {
        if (magnitude > 0f)
            _effects.Apply(new WarEffect(source, kingdomId, kind, magnitude, endTime));
    }

    // A destroyed kingdom takes its row with it; its own ev=destroyed line is the ledger's record.
    private void DropTiersOfKingdomsThatLeftTheMap(HashSet<string> seen)
    {
        foreach (var pair in _tiers.Snapshot())
        {
            if (!seen.Contains(pair.Key))
                _tiers.SetTier(pair.Key, 0);
        }
    }

    private void RemoveRallySourcesNotIn(HashSet<string> kept)
    {
        var stale = new HashSet<string>(StringComparer.Ordinal);
        foreach (var effect in _effects.Snapshot())
        {
            if (effect.SourceId.StartsWith(SourcePrefix, StringComparison.Ordinal) && !kept.Contains(effect.SourceId))
                stale.Add(effect.SourceId);
        }

        foreach (var source in stale)
            _effects.RemoveSource(source);
    }

    private bool IsEligible(KingdomWarSnapshot kingdom, RallyConfig config) =>
        !kingdom.IsPlayerRuled
        && kingdom.AtWar
        && (config.IncludeNeutral || _alignment.ResolveSide(kingdom.Id, kingdom.CultureId) != FactionSide.Neutral);
}
