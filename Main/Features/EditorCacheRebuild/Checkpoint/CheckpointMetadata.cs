using System;

namespace TAOM.Features.EditorCacheRebuild.Checkpoint;

public class CheckpointMetadata
{
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public uint SceneCrc { get; set; }
    public uint NavMeshCrc { get; set; }
    public int PhaseCompleted { get; set; }
    public string NavigationType { get; set; } = "";
}
