using System;
using TaleWorlds.MountAndBlade;

namespace TAOM.Adapters;

/// <summary><see cref="IVoiceDefinitionCatalogAdapter"/> over <see cref="SkinVoiceManager"/>.</summary>
public sealed class VoiceDefinitionCatalogAdapter : IVoiceDefinitionCatalogAdapter
{
    // The same two calls vanilla makes for a mount's voice (v1.5.7 MountVisualCreator.SetVoiceDefinition):
    // the count, then the list filled into an array of that size.
    public int[] GetDefinitionIndices(string soundClass)
    {
        int count = SkinVoiceManager.GetVoiceDefinitionCountWithMonsterSoundAndCollisionInfoClassName(soundClass);
        if (count <= 0) return Array.Empty<int>();
        var indices = new int[count];
        SkinVoiceManager.GetVoiceDefinitionListWithMonsterSoundAndCollisionInfoClassName(soundClass, indices);
        return indices;
    }
}
