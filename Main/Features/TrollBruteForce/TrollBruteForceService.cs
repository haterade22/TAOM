using System;
using TAOM.Core.Validation;
using TAOM.Features.SignatureStrikes;
using static TAOM.Features.TrollBruteForce.TrollBruteForceConfig;

namespace TAOM.Features.TrollBruteForce;

/// <inheritdoc cref="ITrollBruteForceService"/>
public sealed class TrollBruteForceService : ITrollBruteForceService
{
    public bool IsBruteForceTroll(string? monsterId) =>
        monsterId is not null && ActionSetsByMonster.ContainsKey(monsterId);

    public bool IsOffCooldown(float? lastFired, float now)
    {
        if (lastFired is not float last) return true;
        if (!FiniteFloatValidator.IsFinite(last) || !FiniteFloatValidator.IsFinite(now)) return false;
        return now - last >= CooldownSeconds;
    }

    public bool ShouldEngage(float enemyDistance, float facingDot, float bodyScale, bool busy)
    {
        if (busy) return false;
        if (!FiniteFloatValidator.IsFinite(enemyDistance) || !FiniteFloatValidator.IsFinite(facingDot)) return false;
        return enemyDistance >= 0f
            && enemyDistance <= TriggerRange * Scale(bodyScale)
            && facingDot > FacingDot;
    }

    public float TrollWidth(string? monsterId, float agentScale)
    {
        if (monsterId is null || !ShoulderWidthByMonster.TryGetValue(monsterId, out float shoulders)) return 0f;
        if (!FiniteFloatValidator.IsFinite(agentScale) || !(agentScale > 0f)) return 0f;
        return shoulders * agentScale;
    }

    public float? FormationUnitDiameter(float vanillaDiameter, int unitCount, int trollCount, float widestTroll)
    {
        if (unitCount <= 0 || trollCount <= 0 || !((float)trollCount / unitCount >= FormationShare)) return null;
        if (!FiniteFloatValidator.IsFinite(vanillaDiameter) || !(vanillaDiameter > 0f)) return null;
        if (!FiniteFloatValidator.IsFinite(widestTroll) || !(widestTroll > vanillaDiameter)) return null;
        return Math.Min(widestTroll, MaxFormationUnitWidth);
    }

    public float BodySize(float agentScale, float standingEyeHeight) =>
        FiniteFloatValidator.IsFinite(standingEyeHeight) && standingEyeHeight > 0f
            ? agentScale * (standingEyeHeight / ReferenceEyeHeight)
            : agentScale;

    public bool TryGetImpactCentre(float x, float y, float lookX, float lookY, float bodyScale, out float centreX, out float centreY)
    {
        centreX = 0f;
        centreY = 0f;
        if (!FiniteFloatValidator.IsFinite(x) || !FiniteFloatValidator.IsFinite(y)
            || !FiniteFloatValidator.IsFinite(lookX) || !FiniteFloatValidator.IsFinite(lookY))
            return false;

        var length = (float)Math.Sqrt(lookX * lookX + lookY * lookY);
        if (!(length > 1e-4f) || !FiniteFloatValidator.IsFinite(length)) return false;

        var reach = ImpactForward * Scale(bodyScale);
        centreX = x + lookX / length * reach;
        centreY = y + lookY / length * reach;
        return FiniteFloatValidator.IsFinite(centreX) && FiniteFloatValidator.IsFinite(centreY);
    }

    public bool HasReachedImpact(float progress) =>
        FiniteFloatValidator.IsFinite(progress) && progress >= ImpactFraction;

    public BruteForceBlow? DecideRingBlow(float distance, float bodyScale, bool shieldBlocked)
    {
        var scale = Scale(bodyScale);
        var falloff = SignatureStrikeFalloff.Compute(distance, InnerRadius * scale, TrollBruteForceConfig.OuterRadius * scale);
        if (!(falloff > 0f)) return null;

        var damage = CentreDamage * falloff * (shieldBlocked ? ShieldBlockedMultiplier : 1f);
        return new BruteForceBlow((int)Math.Round(damage), KnockDown: !shieldBlocked);
    }

    public float OuterRadius(float bodyScale) => TrollBruteForceConfig.OuterRadius * Scale(bodyScale);

    // A NaN, infinite, zero or negative scale reads as 1; a huge one is capped so a bad value cannot make the ring
    // swallow the battlefield.
    private static float Scale(float bodyScale) =>
        FiniteFloatValidator.IsFinite(bodyScale) && bodyScale > 0f ? Math.Min(bodyScale, MaxBodyScale) : 1f;
}
