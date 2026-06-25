namespace TAOM.Features.HeroRace;

/// <summary>
/// Decides which race a <c>BasicCharacterTableau</c> (the Save/Load hero preview) may safely render.
/// That tableau builds the body through the agentless native
/// <c>MBAgentVisuals.FillEntityWithBodyMeshesWithoutAgentVisuals</c> static-morph path, which
/// access-violates on races whose head meshes lack the per-face-component morph data vanilla heads
/// carry (issue #295). The guard returns a render-safe race (the human base) for any race that would
/// AV, so the cold main-menu save-load preview cannot crash to desktop.
/// </summary>
public interface IBasicTableauRaceGuard
{
    /// <summary>
    /// Returns <paramref name="race"/> when its head meshes are safe in the agentless tableau build,
    /// otherwise the human base race. Unknown / invalid ids are treated as unsafe and coerced.
    /// </summary>
    int ResolveSafeRace(int race);
}
