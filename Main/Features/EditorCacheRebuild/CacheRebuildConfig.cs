namespace TAOM.Features.EditorCacheRebuild;

/// <summary>
/// Strongly-typed deserialization target for <c>Main/_Module/ModuleData/configs/cache_rebuild_config.json</c>.
///
/// <para>
/// Some fields here are <strong>not</strong> exposed in the shipped JSON because they correspond
/// to scaffolding for unreleased phases (path-reuse v2, spatial-indexed Phase 2, multi-pass
/// DEBUG quality check, in-editor UI overlay). They retain sensible defaults so the deserializer
/// is happy and so future phases can wire them without an API change — but the JSON file is kept
/// clean to avoid misleading users with knobs that silently do nothing. Codex review on 2026-05-12
/// confirmed this user-facing debt; we strip the JSON, keep the class shape.
/// </para>
/// </summary>
public class CacheRebuildConfig
{
    // ── Live knobs (exposed in shipped JSON) ────────────────────────────────────

    public bool Enabled { get; set; } = true;
    public bool ForceVanilla { get; set; } = false;

    public int Parallelism { get; set; } = 4;

    public bool EnableIncremental { get; set; } = true;
    public int IncrementalMaxChanged { get; set; } = 30;

    public int SmokeTestPairs { get; set; } = 10;
    public float SmokeTestDistanceTolerance { get; set; } = 1e-4f;

    public string ValidationReportRelativePath { get; set; } = "TAOM_Map/ModuleData/DistanceCaches/last_rebuild_report.json";

    public bool EnableCheckpoint { get; set; } = true;
    public string CheckpointRelativeDirectory { get; set; } = "TAOM_Map/ModuleData/DistanceCaches";

    public string SettlementSnapshotRelativePath { get; set; } = "TAOM_Map/ModuleData/DistanceCaches/settlements_snapshot.json";

    // ── Reserved fields (NOT in shipped JSON — wired in future phases) ──────────

    /// <summary>Reserved. Checkpoint cadence in Phase-1-completed-indices between writes. Currently hardcoded.</summary>
    public int CheckpointEvery { get; set; } = 20;

    /// <summary>Reserved for path-reuse v2 (Phase 12 of original design). PathReuseCache scaffolding exists in `Caching/`.</summary>
    public bool EnablePathReuse { get; set; } = true;

    /// <summary>Reserved for persistent path-cache sidecar (.paths.bin). PersistentPathCache scaffolding exists in `Caching/`.</summary>
    public bool EnablePersistentPathCache { get; set; } = true;

    /// <summary>Reserved for spatial-index-driven incremental Phase 2 (Phase 9 of original design).</summary>
    public float IncrementalSpatialRadius { get; set; } = 5.0f;

    /// <summary>Reserved for multi-pass quality check (Phase 13 of original design — run full build twice with different scheduling, byte-compare).</summary>
    public bool EnableDebugQualityCheck { get; set; } = false;

    /// <summary>Reserved for in-editor UIExtenderEx progress overlay (Phase 12 of original design — dropped when feature pivoted to in-game MCM trigger).</summary>
    public bool EnableUiOverlay { get; set; } = true;

    /// <summary>Reserved for the Layer 3 symmetry optimization (skip the reverse-direction pathfind for Default nav type after smoke-test confirms symmetry within tolerance).</summary>
    public bool Phase1SkipReversePathfind { get; set; } = false;

    /// <summary>Reserved. Mod-wide log filtering isn't this feature's concern; field stays for future use if log levels become per-feature configurable.</summary>
    public string LogVerbosity { get; set; } = "info";
}
