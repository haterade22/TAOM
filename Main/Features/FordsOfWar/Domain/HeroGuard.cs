namespace TAOM.Features.FordsOfWar.Domain;

/// <summary>The elite troops of a faction that take the field with each of its heroes, at no extra price (Mike, 2026-11-05).</summary>
internal sealed record HeroGuard(string TroopId, int Count, UnitRole Role);
