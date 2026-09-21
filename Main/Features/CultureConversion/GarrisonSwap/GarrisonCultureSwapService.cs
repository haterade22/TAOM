using System;
using System.Collections.Generic;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CultureConversion.GarrisonSwap.Domain;

namespace TAOM.Features.CultureConversion.GarrisonSwap;

/// <summary>
/// Orchestration only: read the two rosters, ask <see cref="ITroopCultureMapper"/> what should
/// replace what, hand the answer back to the adapter, report. All matching logic lives in the
/// mapper; all engine access lives in <see cref="IGarrisonCultureSwapAdapter"/>.
/// </summary>
public sealed class GarrisonCultureSwapService : IGarrisonCultureSwapService
{
    private readonly IGarrisonCultureSwapAdapter _adapter;
    private readonly ITroopCultureMapper _mapper;
    private readonly ICultureConversionSettingsProvider _settings;
    private readonly IModLogger _logger;

    public GarrisonCultureSwapService(
        IGarrisonCultureSwapAdapter adapter,
        ITroopCultureMapper mapper,
        ICultureConversionSettingsProvider settings,
        IModLogger logger)
    {
        _adapter = adapter;
        _mapper = mapper;
        _settings = settings;
        _logger = logger;
    }

    public void SwapSettlementTroops(string settlementId, string fromCultureId, string toCultureId, bool isPlayerOwned)
    {
        if (string.IsNullOrEmpty(settlementId) || string.IsNullOrEmpty(toCultureId))
            return;

        var wantGarrison = _settings.ReplaceGarrisonOnConversion;
        var wantMilitia = _settings.ReplaceMilitiaOnConversion;
        if (!wantGarrison && !wantMilitia)
            return;

        // The player's own fiefs get their own toggle, because a swap there throws away a garrison
        // the player stacked by hand — a much bigger deal than the same change on an AI fief.
        if (isPlayerOwned && !_settings.ReplaceGarrisonInPlayerFiefs)
            return;

        var targetIndex = _adapter.GetCultureTroopIndex(toCultureId);
        var toMilitia = _adapter.GetCultureMilitiaTroops(toCultureId);
        var fromMilitia = string.IsNullOrEmpty(fromCultureId) ? null : _adapter.GetCultureMilitiaTroops(fromCultureId);

        // toMilitia can be non-null and still useless: the eight minor factions resolve to a real
        // CultureMilitiaTroops with four empty slots, so a bare null test would let them past this
        // guard and produce a per-stack warning instead of this one clear line.
        var hasRegulars = targetIndex != null && !targetIndex.IsEmpty;
        var hasMilitia = toMilitia != null && !toMilitia.IsEmpty;
        if (!hasRegulars && !hasMilitia)
        {
            // Nothing to map onto at all. The GarrisonCultureCoverage test exists to make this
            // unreachable for any real conversion target; if it fires, the data moved.
            _logger.LogWarning($"GarrisonSwap: culture '{toCultureId}' has no regular troops and no militia troops — {settlementId} keeps its existing troops");
            return;
        }

        var replaced = 0;

        if (wantGarrison && hasRegulars)
        {
            var roster = _adapter.GetGarrisonRoster(settlementId);
            var plan = _mapper.MapGarrison(settlementId, toCultureId, roster, targetIndex!);
            replaced += Apply(settlementId, "garrison", plan, _adapter.ApplyGarrisonSwaps);
        }

        if (wantGarrison && !hasRegulars)
        {
            _logger.LogDebug($"GarrisonSwap: {settlementId} garrison left as-is, culture '{toCultureId}' has no regular troops");
        }

        if (wantMilitia)
        {
            var roster = _adapter.GetMilitiaRoster(settlementId);
            var plan = _mapper.MapMilitia(settlementId, toCultureId, roster, fromMilitia, toMilitia, targetIndex);
            replaced += Apply(settlementId, "militia", plan, _adapter.ApplyMilitiaSwaps);
        }

        if (replaced <= 0)
            return;

        _logger.LogInfo($"GarrisonSwap: {settlementId} re-manned {replaced} troops as {toCultureId}");
        if (isPlayerOwned)
            _adapter.NotifyPlayerTroopsSwapped(settlementId, replaced);
    }

    private int Apply(
        string settlementId,
        string rosterName,
        TroopSwapPlan plan,
        Func<string, IReadOnlyList<TroopSwap>, int> applySwaps)
    {
        if (plan.UnmappedTroopIds.Count > 0)
        {
            // Not a failure: the stacks below are untouched, which is the fail-safe. It is worth a
            // warning because the cause is always a hole in a culture's authored roster.
            _logger.LogWarning(
                $"GarrisonSwap: {settlementId} {rosterName} kept {plan.UnmappedTroopIds.Count} stack(s) with no match in the new culture — {string.Join(", ", plan.UnmappedTroopIds)}");
        }

        return plan.Swaps.Count == 0 ? 0 : applySwaps(settlementId, plan.Swaps);
    }
}
