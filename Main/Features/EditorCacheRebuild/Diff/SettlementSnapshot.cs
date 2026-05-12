namespace TAOM.Features.EditorCacheRebuild.Diff;

public class SettlementSnapshot
{
    public string Id { get; set; } = "";
    // Position-only snapshot. We do NOT serialize face indices because reading them via
    // CampaignVec2.Face dereferences Campaign.Current.MapSceneWrapper, which can NRE in editor
    // mode where Campaign.Current is null (Codex Finding 4 / P2).
    // Position scalars are sufficient for the differ to detect moves — face index is derivable
    // from position via the scene if ever needed.
    public float GateX { get; set; }
    public float GateY { get; set; }
    public float PortX { get; set; }
    public float PortY { get; set; }
    public bool HasPort { get; set; }
    public bool IsFortification { get; set; }
}

public class SettlementSnapshotFile
{
    public uint SceneCrc { get; set; }
    public uint NavMeshCrc { get; set; }
    public string NavigationType { get; set; } = "";
    public System.DateTime Timestamp { get; set; }
    public SettlementSnapshot[] Settlements { get; set; } = System.Array.Empty<SettlementSnapshot>();
}
