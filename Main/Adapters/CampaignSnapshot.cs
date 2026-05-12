namespace TAOM.Adapters;

/// <summary>
/// Snapshot of campaign-session state at trigger time. Pure data — used for diagnostic
/// log lines in <see cref="TAOM.Features.EditorCacheRebuild.RuntimeCacheRebuildService"/>.
/// Counts are <c>-1</c> when the underlying collection is unavailable.
/// </summary>
public sealed class CampaignSnapshot
{
    public string GameId { get; set; } = string.Empty;
    public string StartTime { get; set; } = string.Empty;
    public string CurrentTime { get; set; } = string.Empty;
    public int SettlementCount { get; set; } = -1;
    public int FortificationCount { get; set; } = -1;
    public int TownCount { get; set; } = -1;
    public int CastleCount { get; set; } = -1;
    public int VillageCount { get; set; } = -1;
    public string MapSceneWrapperType { get; set; } = string.Empty;
}
