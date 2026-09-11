using System;
using System.Collections.Generic;

namespace TAOM.Features.ShaderPrecompilation.Domain;

// One unit of work in the precompile walk. Either one CHARACTER BATCH (a custom battle whose enemy
// roster is a slice of every loaded character, so their equipment shaders compile at preload, #560)
// or a single scene pass (loads one battle scene so its terrain + forced-atmosphere shaders compile,
// the class of shader that AV'd d3dcompiler in #287).
public enum PrecompileItemKind
{
    CharacterBattle,
    ScenePass,
}

public sealed class PrecompileItem
{
    public PrecompileItem(PrecompileItemKind kind, string sceneId, string description,
        IReadOnlyList<string> characterIds = null, int batchIndex = 0, int batchCount = 0)
    {
        Kind = kind;
        SceneId = sceneId;
        Description = description;
        CharacterIds = characterIds ?? Array.Empty<string>();
        BatchIndex = batchIndex;
        BatchCount = batchCount;
    }

    public PrecompileItemKind Kind { get; }
    public string SceneId { get; }
    public string Description { get; }

    // CharacterBattle only: the exact character ids this batch loads. Empty for a scene pass and for
    // the bootstrap batch (the roster is not readable at the main menu, so the first battle discovers
    // it and slices its own share, see ShaderPrecompilePlanner.BuildBootstrapPlan).
    public IReadOnlyList<string> CharacterIds { get; }

    // 0-based batch position; 0 for scene passes.
    public int BatchIndex { get; }

    // Total character batches in the plan; 0 until the roster is known (the bootstrap item).
    public int BatchCount { get; }
}
