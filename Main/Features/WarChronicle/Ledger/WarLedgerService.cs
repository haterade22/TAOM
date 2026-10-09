using System.Collections.Generic;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.Diplomacy;
using TAOM.Features.Execution;
using TAOM.Features.WarChronicle.Effects;
using TAOM.Features.WarChronicle.Rally;

namespace TAOM.Features.WarChronicle.Ledger;

/// <summary>
/// The daily war ledger (docs/features/war-chronicle.md, Part D): one <c>[WarLedger]</c> line per
/// kingdom per day, three side lines, a Free-share line, and one-off event lines. The lines go to the
/// TAOM debug log through <see cref="IModLogger.LogInfo"/> (INFO reaches the file and is flushed at
/// once, <c>FileLogger.Enqueue</c>), where <c>tools/analyze_war_ledger.py</c> reads them. Stateless:
/// every number comes from the snapshots, the baselines, the tier store, the effect registry and the
/// phase machine, so a reload re-emits a day and the analyzer keeps the last.
/// </summary>
public sealed class WarLedgerService
{
    private static readonly FactionSide[] SideOrder = { FactionSide.Free, FactionSide.Evil, FactionSide.Neutral };

    private readonly IKingdomWarSnapshotAdapter _snapshots;
    private readonly WarBaselineService _baselines;
    private readonly RallyTierStore _tiers;
    private readonly IWarEffectService _effects;
    private readonly IWarOfTheRingService _wotr;
    private readonly IAlignmentService _alignment;
    private readonly IModLogger _logger;

    public WarLedgerService(
        IKingdomWarSnapshotAdapter snapshots, WarBaselineService baselines, RallyTierStore tiers,
        IWarEffectService effects, IWarOfTheRingService wotr, IAlignmentService alignment, IModLogger logger)
    {
        _snapshots = snapshots;
        _baselines = baselines;
        _tiers = tiers;
        _effects = effects;
        _wotr = wotr;
        _alignment = alignment;
        _logger = logger;
    }

    /// <summary>Today's lines: a kingdom line each, then the free, evil and neutral side lines, then the share. Empty without kingdoms.</summary>
    public IReadOnlyList<string> BuildDailyLines(IReadOnlyList<KingdomWarSnapshot> kingdoms)
    {
        var lines = new List<string>();
        if (kingdoms == null || kingdoms.Count == 0)
            return lines;

        var campaignId = _snapshots.GetCampaignId();
        var day = _snapshots.GetElapsedDay();
        var phase = _wotr.CurrentPhase;

        var points = new int[SideOrder.Length];
        var baselineTotals = new int[SideOrder.Length];
        var hasBaseline = new bool[SideOrder.Length];
        var alive = new int[SideOrder.Length];

        foreach (var kingdom in kingdoms)
        {
            if (kingdom == null)
                continue;

            var side = _alignment.ResolveSide(kingdom.Id, kingdom.CultureId);
            var baseline = _baselines.GetBaseline(kingdom.Id);
            lines.Add(WarLedgerFormatter.Kingdom(
                campaignId, day, phase, kingdom, side, baseline, _tiers.GetTier(kingdom.Id),
                _effects.GetMultiplier(kingdom.Id, WarEffectKind.VolunteerRate),
                _effects.GetMultiplier(kingdom.Id, WarEffectKind.PrisonerEscape)));

            var slot = SlotOf(side);
            points[slot] += kingdom.FortificationPoints;
            alive[slot]++;
            if (baseline.HasValue)
            {
                baselineTotals[slot] += baseline.Value;
                hasBaseline[slot] = true;
            }
        }

        for (var slot = 0; slot < SideOrder.Length; slot++)
        {
            lines.Add(WarLedgerFormatter.Side(
                campaignId, day, SideOrder[slot], points[slot], hasBaseline[slot] ? baselineTotals[slot] : (int?)null, alive[slot]));
        }

        var contested = points[SlotOf(FactionSide.Free)] + points[SlotOf(FactionSide.Evil)];
        double? share = contested > 0 ? points[SlotOf(FactionSide.Free)] / (double)contested : (double?)null;
        lines.Add(WarLedgerFormatter.Share(campaignId, day, share));
        return lines;
    }

    public void WriteDaily(IReadOnlyList<KingdomWarSnapshot> kingdoms)
    {
        foreach (var line in BuildDailyLines(kingdoms))
            _logger.LogInfo(line);
    }

    public void LogDestroyed(string? kingdomId)
    {
        if (kingdomId is null || kingdomId.Length == 0)
            return;
        _logger.LogInfo(WarLedgerFormatter.DestroyedEvent(_snapshots.GetCampaignId(), _snapshots.GetElapsedDay(), kingdomId));
    }

    /// <summary>For the rally: one line when a kingdom's tier changes. <paramref name="loss"/> is the share of the baseline lost.</summary>
    public void LogTierChange(string kingdomId, int from, int to, float loss) =>
        _logger.LogInfo(WarLedgerFormatter.TierEvent(
            _snapshots.GetCampaignId(), _snapshots.GetElapsedDay(), kingdomId, from, to, loss));

    /// <summary>For the chronicle: one line when an event resolves (Held, Fell, Lapsed, Occurred or Skipped).</summary>
    public void LogChronicleResolution(string eventId, string outcome) =>
        _logger.LogInfo(WarLedgerFormatter.ChronicleEvent(
            _snapshots.GetCampaignId(), _snapshots.GetElapsedDay(), eventId, outcome));

    private static int SlotOf(FactionSide side)
    {
        switch (side)
        {
            case FactionSide.Free: return 0;
            case FactionSide.Evil: return 1;
            default: return 2;
        }
    }
}
