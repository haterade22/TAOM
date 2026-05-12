using TAOM.Adapters;

namespace TAOM.Features.EditorCacheRebuild.Checkpoint;

public interface ICheckpointSerializer
{
    /// <summary>
    /// Try to load a valid checkpoint. Returns the loaded metadata if CRCs match the live scene,
    /// or null if no usable checkpoint exists.
    /// </summary>
    CheckpointMetadata? TryLoad(string baseDirectory, string navigationType, INavigationCacheAdapter adapter);

    void Save(string baseDirectory, string navigationType, INavigationCacheAdapter adapter, int phaseCompleted);

    void Delete(string baseDirectory, string navigationType);
}
