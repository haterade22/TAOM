namespace TAOM.Features.FordsOfWar.Domain;

/// <summary>An order for a tower to fire <paramref name="Missiles"/> at one target.</summary>
internal sealed record TowerVolley(string TowerId, AgentKey Target, int Missiles);
