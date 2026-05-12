namespace TAOM.Features.EditorCacheRebuild;

public class CacheRebuildConfig
{
    public bool Enabled { get; set; } = true;
    public bool ForceVanilla { get; set; } = false;

    public int Parallelism { get; set; } = 4;
    public int CheckpointEvery { get; set; } = 20;

    public bool EnablePathReuse { get; set; } = true;
    public bool EnablePersistentPathCache { get; set; } = true;
    public bool EnableIncremental { get; set; } = true;

    public int IncrementalMaxChanged { get; set; } = 30;
    public float IncrementalSpatialRadius { get; set; } = 5.0f;

    public bool EnableDebugQualityCheck { get; set; } = false;
    public bool EnableUiOverlay { get; set; } = true;

    public int SmokeTestPairs { get; set; } = 10;
    public float SmokeTestDistanceTolerance { get; set; } = 1e-4f;

    public bool Phase1SkipReversePathfind { get; set; } = false;

    public string ValidationReportRelativePath { get; set; } = "TAOM_Map/ModuleData/DistanceCaches/last_rebuild_report.json";

    public bool EnableCheckpoint { get; set; } = true;
    public string CheckpointRelativeDirectory { get; set; } = "TAOM_Map/ModuleData/DistanceCaches";

    public string SettlementSnapshotRelativePath { get; set; } = "TAOM_Map/ModuleData/DistanceCaches/settlements_snapshot.json";

    public string LogVerbosity { get; set; } = "info";
}
