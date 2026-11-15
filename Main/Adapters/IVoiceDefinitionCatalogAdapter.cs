namespace TAOM.Adapters;

/// <summary>The engine's voice definitions by monster sound class (<c>SkinVoiceManager</c>, ADR-007).</summary>
public interface IVoiceDefinitionCatalogAdapter
{
    /// <summary>
    /// The native indices of every voice definition whose <c>sound_and_collision_info_class</c> is
    /// <paramref name="soundClass"/>. The engine gives no names with them, and no name-to-index lookup exists.
    /// </summary>
    int[] GetDefinitionIndices(string soundClass);
}
