using System;

namespace TAOM.Features.FordsOfWar.Domain;

/// <summary>
/// Cavalry and horses take <see cref="FordsMatchConfig.PierceVsMountedFactor"/> times a pierce blow (Mike, 2026-11-07), so
/// spearmen, pikemen, bowmen and crossbowmen answer riders: a spear or pike thrust, an arrow, a bolt, a javelin, a tower's
/// arrow, a couched lance, and in a match the scripted pierce blows too (a warg or spider bite, a signature strike's splash).
/// A cut or blunt blow, a blow the engine stops with a shield (it damages the shield, not the soldier), or a victim on foot
/// is unchanged. A scripted blow that a shield only reduces still lands on the soldier, so the rule applies to it.
/// </summary>
internal static class MountedPierceRule
{
    public static float Factor(float factor, bool pierce, bool victimMounted) => pierce && victimMounted ? factor : 1f;

    /// <summary>A scripted blow's whole damage after the rule, rounded as the engine rounds a modelled blow (MathF.Round, half to even).</summary>
    public static int Damage(float damage, float factor, bool pierce, bool victimMounted) =>
        (int)Math.Round(damage * Factor(factor, pierce, victimMounted));
}
