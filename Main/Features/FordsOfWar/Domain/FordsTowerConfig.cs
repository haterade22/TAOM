namespace TAOM.Features.FordsOfWar.Domain;

/// <summary>Tower and keep durability, the damage factors per attacker kind, the garrison's fire, and how much damage ordinary blows may do to a tower each second (0 for no limit).</summary>
internal sealed record FordsTowerConfig(int TowerHitPoints, int InnerTowerHitPoints, int KeepHitPoints,
    float RangedDamageFactor, float HeroDamageFactor, float SiegeDamageFactor, float RegenPerSecond,
    float RegenDelaySeconds, int GunnersPerTower, float VolleyIntervalSeconds, int VolleyMissiles,
    float FireRange, float AttackRange, float ThrustFloorFactor, float AssaultDamagePerSecond);
