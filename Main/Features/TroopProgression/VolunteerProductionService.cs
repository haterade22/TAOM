using System;
using TaleWorlds.CampaignSystem;
using TAOM.Core.Validation;
using TAOM.Features.CulturalFeats;
using TAOM.Features.WarChronicle.Effects;

namespace TAOM.Features.TroopProgression;

/// <summary>
/// The daily per-slot volunteer probability of a notable, after TAOM's adjustments. Pure maths, moved out
/// of <c>TaomVolunteerModel</c> so the model body holds no branch (gamemodels.md rule 4); the castle fill
/// (<c>CastleRecruitmentService</c>) calls it too.
///
/// With a culture, the respawn feats apply as the model did before (an <see cref="ExplainedNumber"/>,
/// <see cref="ICulturalFeatsService.ApplyVolunteerRespawnFeats"/>, clamp to 0..1), with the same result for
/// every finite value; with none, vanilla's value passes through untouched. A non-finite base or feats
/// result deliberately returns vanilla's value unchanged and skips the war step: the old body returned NaN
/// there (or 1 for an infinite base under a positive feat), which <c>RecruitmentCampaignBehavior</c> read
/// as "never produce a volunteer". The War Chronicle multiplier (0.5 to 1.5, baked, one dictionary read)
/// then scales the value and the product is clamped to 0..1, except for a settlement of the player's own
/// clan (decision D4), which keeps its feats and gets no war step. A multiplier of exactly one returns the
/// value above unchanged, so with no active effect the result is today's result.
/// </summary>
public sealed class VolunteerProductionService
{
    private readonly ICulturalFeatsService _feats;
    private readonly IWarEffectService _war;

    public VolunteerProductionService(ICulturalFeatsService feats, IWarEffectService war)
    {
        _feats = feats;
        _war = war;
    }

    /// <param name="baseProbability">The vanilla probability (or the castle curve).</param>
    /// <param name="culture">The settlement owner's culture, or null for no culture feats.</param>
    /// <param name="kingdomKey">The owner clan's kingdom id; null for a clan without a kingdom.</param>
    /// <param name="ownerIsPlayerClan">The settlement belongs to the player's own clan: no war multiplier.</param>
    public float Compute(float baseProbability, ICultureFeatAdapter? culture, string? kingdomKey, bool ownerIsPlayerClan)
    {
        if (!FiniteFloatValidator.IsFinite(baseProbability))
            return baseProbability;

        var value = baseProbability;
        if (culture != null)
        {
            var result = new ExplainedNumber(baseProbability);
            _feats.ApplyVolunteerRespawnFeats(culture, ref result);
            value = Math.Max(0f, Math.Min(1f, result.ResultNumber));
            if (!FiniteFloatValidator.IsFinite(value))
                return baseProbability;
        }

        if (ownerIsPlayerClan)
            return value;

        var multiplier = _war.GetMultiplier(kingdomKey, WarEffectKind.VolunteerRate);
        if (!FiniteFloatValidator.IsFinite(multiplier) || multiplier == 1f)
            return value;

        var scaled = value * multiplier;
        return FiniteFloatValidator.IsFinite(scaled) ? Math.Max(0f, Math.Min(1f, scaled)) : value;
    }
}
