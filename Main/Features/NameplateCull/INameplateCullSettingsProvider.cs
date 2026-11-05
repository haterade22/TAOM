// Adapted from yotthani's VanillaTuning (MIT, (c) 2026 yotthani), nameplate-cull.
namespace TAOM.Features.NameplateCull;

/// <summary>The MCM toggle of the nameplate cull (Battle Load Diagnostics page, group "Map Performance").</summary>
public interface INameplateCullSettingsProvider
{
    /// <summary>"Cull Hidden Settlement Nameplates". Read on every map frame, so it must stay a cached read.</summary>
    bool CullEnabled { get; }
}
