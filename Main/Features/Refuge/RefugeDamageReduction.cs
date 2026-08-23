using TaleWorlds.CampaignSystem;

namespace TAOM.Features.Refuge;

/// <summary>
/// THE composition contract for the refuge defender damage reduction, shared by its two consult
/// sites (TaomCombatMechanicsModel.ApplyDamageReductions, real-time; TaomCombatSimulationModel
/// .SimulateHit, auto-resolve): <b>a reduction r scales the FINAL damage by (1 - r).</b>
///
/// <para>The float overload is that contract literally. The ExplainedNumber overload needs the
/// factor translated, because ExplainedNumber composes factors against the BASE
/// (result = base + base * sumOfFactors, 1.4.8 ExplainedNumber.cs), so a naive AddFactor(-r)
/// subtracts r of the BASE, not r of the final number: with vanilla factors already at +50% and
/// r = 0.20, AddFactor(-0.20) yields 1.30x base while the real-time path yields 1.20x base.
/// Scaling the factor by result/base makes both paths exact: base * (sum - r * result/base)
/// = result - r * result = result * (1 - r).</para>
///
/// <para>PRECONDITION (round-C finding): the exactness above holds only while the number is
/// UNCLAMPED at call time. ResultNumber is the clamped view; if a LimitMin/LimitMax already
/// binds, the derived scale under-  or over-shoots relative to the unclamped composition. Both
/// consult sites call this before any clamp is applied on their paths (verified against the two
/// model files); a future third consumer must keep that ordering.
/// RefugeDamageReductionTests pins the pre-clamped behaviour so a change here is loud.</para>
///
/// <para>Gates are positive requirements so NaN fails them (engine-float rule): a NaN or
/// out-of-range reduction applies nothing, a zero/NaN base or non-finite ratio leaves the number
/// untouched (base 0 means the result is 0 anyway).</para>
/// </summary>
public static class RefugeDamageReduction
{
    /// <summary>Real-time contract: final damage scaled by (1 - reduction).</summary>
    public static float Apply(float damage, float reduction)
        => IsApplicable(reduction) ? damage * (1f - reduction) : damage;

    /// <summary>Auto-resolve contract: the same (1 - reduction) on the FINAL number, expressed
    /// as the base-relative factor ExplainedNumber composes with.</summary>
    public static void Apply(ref ExplainedNumber result, float reduction)
    {
        if (!IsApplicable(reduction))
            return;
        float baseNumber = result.BaseNumber;
        if (!(baseNumber > 0f) && !(baseNumber < 0f))
            return;
        float scale = result.ResultNumber / baseNumber;
        if (float.IsNaN(scale) || float.IsInfinity(scale))
            return;
        result.AddFactor(-reduction * scale);
    }

    private static bool IsApplicable(float reduction) => reduction > 0f && reduction < 1f;
}
