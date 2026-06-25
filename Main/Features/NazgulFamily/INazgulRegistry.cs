namespace TAOM.Features.NazgulFamily;

/// <summary>
/// Identifies the Ringwraiths (the Witch-King + the Nazgûl) by their lord StringId.
/// As undead servants of Sauron they take no spouse, parents, or children.
/// </summary>
public interface INazgulRegistry
{
    /// <summary>True if <paramref name="heroStringId"/> is one of the eight Ringwraith lords.</summary>
    bool IsWraith(string heroStringId);
}
