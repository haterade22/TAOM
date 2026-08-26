namespace TAOM.Features.UncapturableHeroes;

/// <summary>
/// Merges the MCM live toggle over the validated JSON default (DreadAuraSettingsProvider pattern).
/// <c>TaomSettings.Instance</c> can be null very early in startup or when MCM fails to load, so the
/// read falls back to JSON.
///
/// One property is enough to justify the interface: it is the seam that keeps
/// <see cref="IUncapturableHeroService"/> free of a static MCM read, which is what makes
/// "toggle off means the registry is never asked" testable at all (ADR-008).
/// </summary>
public interface IUncapturableHeroesSettingsProvider
{
    bool IsEnabled { get; }
}
