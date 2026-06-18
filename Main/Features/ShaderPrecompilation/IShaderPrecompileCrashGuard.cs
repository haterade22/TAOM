using System.Collections.Generic;

namespace TAOM.Features.ShaderPrecompilation;

// Makes the shader walk self-healing against a scene that hard-CRASHES the process during load — a
// GPU/driver-specific native AV (e.g. `taom_rohan_battle_fords_of_isen_forceatmo` AV'd one user's GPU
// on the `pbr_terrain` input-layout-9 compile while loading fine on others). The runner writes the
// scene it is about to load to a marker file that survives a hard crash; if that marker is still there
// at the next walk's start, the scene crashed and is recorded to a persistent skip list so subsequent
// walks skip it and can complete. Mirrors the BattleLoadStallMarker inflight-marker pattern.
public interface IShaderPrecompileCrashGuard
{
    // At walk start: if the previous walk's inflight marker survived (the process crashed mid-load),
    // record that scene as crashed; return the full set of scene ids to SKIP this walk.
    IReadOnlyCollection<string> ConsumeAndGetSkipSet();

    // Before loading a scene item — written to disk so it survives a hard process crash.
    void MarkLoading(string sceneId);

    // When an item finishes without crashing the process (still alive) — clears the inflight marker.
    void ClearLoading();
}
