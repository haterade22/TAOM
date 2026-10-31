namespace TAOM.Features.FordsOfWar.Domain;

/// <summary>Hit points a tower regained this tick; the engine layer mirrors them onto the entity.</summary>
internal sealed record TowerHeal(string TowerId, int Amount);
