namespace TAOM.Features.HeroAbilities.Ranged;

/// <summary>The rules of the Legolas arrow kill that need no engine type (#1361).</summary>
public static class HeroRangedService
{
    /// <summary>
    /// Chance that a landed arrow kills. An extra arrow, a hit a shield or a weapon blocked, a hit that did no damage
    /// and a victim the arrow already killed give 0, and so does a chance that is not a finite number. The caller rolls
    /// it in SignatureInstantKill.
    /// </summary>
    public static float ArrowKillChance(bool headshot, bool extraArrow, bool blockedByShield, bool blockedByWeapon,
        float inflictedDamage, float headshotChance, float bodyChance, bool victimAlive)
    {
        if (!victimAlive || extraArrow || blockedByShield || blockedByWeapon || !(inflictedDamage > 0f) || float.IsInfinity(inflictedDamage))
            return 0f;
        var chance = headshot ? headshotChance : bodyChance;
        if (float.IsNaN(chance) || float.IsInfinity(chance) || chance < 0f)
            return 0f;
        return chance > 1f ? 1f : chance;
    }
}
