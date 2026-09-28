using System;
using TAOM.Adapters;

namespace TAOM.Features.Spider;

public interface ISpiderAttackService
{
    /// <summary>True when the given Monster StringId is the giant spider (drives BT attach + mount-lock).</summary>
    bool IsSpiderMonster(string? monsterId);

    int CalculateSpiderBiteDamage(IAgentAdapter target, float velocity, float armorEffectivenessPercent, float critRoll);
    /// <summary>One strike's hit on a target in its arc, under the strike's rules (damage multiplier, knockdown); the
    /// ridden spider's are <see cref="SpiderStrikeProfile.Ridden"/>.</summary>
    bool HandleSpiderTargetHit(IAgentAdapter attacker, IAgentAdapter target, sbyte boneId, SpiderStrikeProfile strike);

    /// <summary>Fires the resolved directional attack (pounce or left/right swipe): plays the clip + deals radial
    /// damage in the kind's front arc (reliable, replacing the unreliable bone-collision), under the strike rules for
    /// each clip (a creature bandit's caps and damage, #692; the ridden spider's set is
    /// <see cref="SpiderStrikeSet.Ridden"/>). Returns what the strike did, or default for an inactive spider.</summary>
    SpiderStrikeOutcome SpiderAttack(IAgentAdapter spider, SpiderAttackKind kind, float bearing, SpiderStrikeSet strikes);

    // --- Pure decision helpers (no TaleWorlds types — unit-tested; elephant-parity) ---

    /// <summary>True when the attack has never fired or its cooldown window has fully elapsed (inclusive ≥).</summary>
    bool IsOffCooldown(DateTime? lastFired, DateTime now, double cooldownSeconds);

    /// <summary>Resolves the clip name for a (kind, velocity, bearing): pounce → front/charge by speed; side → left/right by bearing (≥0 = LEFT).</summary>
    string SelectActionName(SpiderAttackKind kind, float velocityY, float bearing);

    /// <summary>Resolves the radial-damage arc for a (kind, bearing): pounce → forward cone; side → forward-left
    /// (bearing ≥ 0) or forward-right flank. Returns (arc center offset deg, half-angle deg); + center = LEFT.</summary>
    (float centerDeg, float halfAngleDeg) SelectArc(SpiderAttackKind kind, float bearing);
}
