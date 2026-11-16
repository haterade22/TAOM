namespace TAOM.Features.HeroAbilities.Domain;

/// <summary>
/// A running extra-arrow window (#1361): until the mission time <see cref="Until"/>, each of the hero's own shots adds
/// <see cref="ExtraArrows"/> arrows at enemies near his target. Set by the cast, read by the ranged mission logic.
/// </summary>
public readonly record struct HeroBurst(float Until, int ExtraArrows);
