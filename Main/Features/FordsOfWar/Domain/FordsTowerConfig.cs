namespace TAOM.Features.FordsOfWar.Domain;

/// <summary>Tower and keep durability, the damage factors per attacker kind, and the garrison's fire.</summary>
internal sealed record FordsTowerConfig(int TowerHitPoints, int InnerTowerHitPoints, int KeepHitPoints,
    float RangedDamageFactor, float HeroDamageFactor, float SiegeDamageFactor, float RegenPerSecond,
    float RegenDelaySeconds, int GunnersPerTower, float VolleyIntervalSeconds, int VolleyMissiles,
    float FireRange, float AttackRange);
