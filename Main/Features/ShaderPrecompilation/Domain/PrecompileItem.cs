namespace TAOM.Features.ShaderPrecompilation.Domain;

// One unit of work in the precompile walk. Either the big all-characters battle (compiles
// character/equipment material shaders) or a single scene pass (loads one battle scene so its
// terrain + forced-atmosphere shaders compile — the class of shader that AV'd d3dcompiler in #287).
public enum PrecompileItemKind
{
    CharacterBattle,
    ScenePass,
}

public sealed class PrecompileItem
{
    public PrecompileItem(PrecompileItemKind kind, string sceneId, string description)
    {
        Kind = kind;
        SceneId = sceneId;
        Description = description;
    }

    public PrecompileItemKind Kind { get; }
    public string SceneId { get; }
    public string Description { get; }
}
