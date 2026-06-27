using System;
using System.Collections.Generic;
using TAOM.Features.Execution;

namespace TAOM.Features.AlignmentDesertion;

/// <summary>
/// Pure desertion decision (ADR-002/007). For a single party or garrison, returns the per-troop-type
/// desertion when a troop's culture alignment is opposed to the owner's kingdom alignment (Free vs
/// Evil). Neutral on either side never deserts; named heroes never desert. The per-type count uses the
/// same <c>max(1, (int)(count * rate))</c> floor + cap as <c>SpecialResourceService.CalculateDesertion</c>.
/// </summary>
public class AlignmentDesertionService : IAlignmentDesertionService
{
    private readonly IAlignmentService _alignment;
    private readonly IAlignmentDesertionSettingsProvider _settings;

    public AlignmentDesertionService(IAlignmentService alignment, IAlignmentDesertionSettingsProvider settings)
    {
        _alignment = alignment;
        _settings = settings;
    }

    public bool IsEnabled => _settings.IsEnabled;

    public IReadOnlyList<TroopDesertionResult> CalculateDesertion(
        string ownerKingdomId, bool isPlayerOwned, bool isGarrison, IReadOnlyList<DesertionTroopInfo> troops)
    {
        var result = new List<TroopDesertionResult>();

        if (!_settings.IsEnabled || troops == null || troops.Count == 0)
            return result;

        // Owner gate (player / AI).
        if (isPlayerOwned && !_settings.ApplyToPlayer) return result;
        if (!isPlayerOwned && !_settings.ApplyToAi) return result;

        // Location gate (parties / garrisons).
        if (isGarrison && !_settings.ApplyToGarrisons) return result;
        if (!isGarrison && !_settings.ApplyToParties) return result;

        var ownerSide = _alignment.GetKingdomSide(ownerKingdomId);
        // Neutral-aligned (Umbar/Shaghana/Abanissa), independent, or kingdomless owners never purge.
        // NOTE: a mercenary clan keeps Clan.Kingdom set to its employer, so it resolves to the
        // employer's side and DOES purge opposed troops -- it is not exempt here. (Codex #2.)
        if (ownerSide == FactionSide.Neutral) return result;

        var rate = _settings.Rate;
        // Rate 0 = no desertion. The master toggle is the off switch, but a 0% slider must also mean
        // "none" -- without this the min-1 floor below would still shed 1 per opposed type. (Codex #3.)
        if (rate <= 0f) return result;

        foreach (var troop in troops)
        {
            if (troop == null || troop.IsHero || troop.Count <= 0)
                continue;

            var troopSide = _alignment.GetCultureSide(troop.CultureId);
            if (troopSide == FactionSide.Neutral || troopSide == ownerSide)
                continue; // loyal (same side) or mercenary-culture (Neutral) -- stays

            var desertCount = Math.Max(1, (int)(troop.Count * rate));
            desertCount = Math.Min(desertCount, troop.Count);
            result.Add(new TroopDesertionResult(troop.TroopId, desertCount));
        }

        return result;
    }
}
