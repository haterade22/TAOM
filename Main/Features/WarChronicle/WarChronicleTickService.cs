using System;
using System.Collections.Generic;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.WarChronicle.Effects;
using TAOM.Features.WarChronicle.Ledger;
using TAOM.Features.WarChronicle.Rally;

namespace TAOM.Features.WarChronicle;

/// <summary>
/// What the War Chronicle does on each daily tick, in order: expire the timed effects (which re-bakes
/// them with the live strength), take the baselines of kingdoms not seen before, run the rally, roll the
/// extra prisoner escapes the effects allow, then write the day's ledger. The escape pass runs after the
/// rally, so a boost the rally grants, refreshes or removes reaches the roll the same day, with no lag in
/// either direction, and the ledger's <c>pe</c> is the multiplier that roll used. The expiry cannot fail,
/// and the escape pass and the rally catch their own failures; the rest reads engine state through the
/// snapshot adapter, and a throwing engine getter must never fail the campaign's daily tick, so it is
/// logged and the rest of the day (the baselines, the rally, the escape pass and the ledger) is skipped.
/// Baselines taken after day 1 with none held (a save from before this feature, a corrupt section or a
/// skipped first tick) get one warning. The chronicle events (a later milestone) slot in between the rally
/// and the ledger. The caller has already checked that this peer may run world work.
/// </summary>
public sealed class WarChronicleTickService
{
    private readonly IKingdomWarSnapshotAdapter _snapshots;
    private readonly IWarEffectService _effects;
    private readonly WarBaselineService _baselines;
    private readonly WarLedgerService _ledger;
    private readonly WarEscapeDailyPass _escapes;
    private readonly RallyService _rally;
    private readonly IModLogger _logger;

    public WarChronicleTickService(
        IKingdomWarSnapshotAdapter snapshots, IWarEffectService effects, WarBaselineService baselines,
        WarLedgerService ledger, WarEscapeDailyPass escapes, RallyService rally, IModLogger logger)
    {
        _snapshots = snapshots;
        _effects = effects;
        _baselines = baselines;
        _ledger = ledger;
        _escapes = escapes;
        _rally = rally;
        _logger = logger;
    }

    public void RunDaily()
    {
        try
        {
            var nowHours = _snapshots.GetNowHours();
            _effects.Expire(nowHours);

            var kingdoms = _snapshots.GetKingdoms();
            TakeBaselines(kingdoms);
            _rally.RunDaily(kingdoms, nowHours);
            _escapes.Run();
            _ledger.WriteDaily(kingdoms);
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"[WarChronicle] Daily tick skipped ({ex.GetType().Name}: {ex.Message}).");
        }
    }

    // A new campaign's first daily tick is day 1 (a one-day initial wait from CampaignStartTime), so an
    // empty registry after it means the baselines are measured from today's map, not from the war's start.
    private void TakeBaselines(IReadOnlyList<KingdomWarSnapshot> kingdoms)
    {
        var day = _snapshots.GetElapsedDay();
        var late = _baselines.Count == 0 && day > 1;
        var taken = _baselines.EnsureBaselines(kingdoms);
        if (taken > 0 && late)
        {
            _logger.LogWarning(
                $"[WarChronicle] Baselines taken on day {day} with none restored, for {taken} kingdom(s): " +
                "loss is measured from today's map, not from the start of the war.");
        }
    }
}
