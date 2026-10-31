using System.Collections.Generic;

namespace TAOM.Features.FordsOfWar.Domain;

/// <summary>The enemies the engine layer found near one tower this tick.</summary>
internal sealed record TowerTargets(string TowerId, IReadOnlyList<TargetCandidate> EnemiesInRange);
