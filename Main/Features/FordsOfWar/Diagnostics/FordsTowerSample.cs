namespace TAOM.Features.FordsOfWar.Diagnostics;

/// <summary>One standing tower in a 1 Hz look: the enemy melee units within the attack range.</summary>
internal sealed record FordsTowerSample(string TowerId, int EnemyMeleeInRange);
