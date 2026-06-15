using System;
using System.Collections.Generic;
using TAOM.Adapters;

namespace TAOM.Features.Spider;

public interface ISpiderAttackService
{
    /// <summary>True when the given Monster StringId is the giant spider (drives BT attach + mount-lock).</summary>
    bool IsSpiderMonster(string? monsterId);

    int CalculateSpiderBiteDamage(IAgentAdapter target, float velocity, float armorEffectivenessPercent, float critRoll);
    void HandleSpiderTargetHit(IAgentAdapter attacker, IAgentAdapter target, sbyte boneId);

    /// <summary>Fires the resolved directional attack (pounce or left/right swipe) via bone-collision CustomAttack.</summary>
    void SpiderAttack(IAgentAdapter spider, SpiderAttackKind kind, float bearing);

    // --- Pure decision helpers (no TaleWorlds types — unit-tested; elephant-parity) ---

    /// <summary>True when the attack has never fired or its cooldown window has fully elapsed (inclusive ≥).</summary>
    bool IsOffCooldown(DateTime? lastFired, DateTime now, double cooldownSeconds);

    /// <summary>Resolves the clip name for a (kind, velocity, bearing): pounce → front/charge by speed; side → left/right by bearing (≥0 = LEFT).</summary>
    string SelectActionName(SpiderAttackKind kind, float velocityY, float bearing);

    /// <summary>Resolves the bone-collision set matching <see cref="SelectActionName"/>.</summary>
    List<sbyte> SelectBones(SpiderAttackKind kind, float velocityY, float bearing);
}
