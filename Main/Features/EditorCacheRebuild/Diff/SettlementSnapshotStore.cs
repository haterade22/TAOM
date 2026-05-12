using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using TAOM.Adapters;
using TAOM.Core.Logging;

namespace TAOM.Features.EditorCacheRebuild.Diff;

public class SettlementSnapshotStore : ISettlementSnapshotStore
{
    private readonly IModLogger _logger;

    public SettlementSnapshotStore(IModLogger logger)
    {
        _logger = logger;
    }

    public SettlementSnapshotFile? TryLoad(string filePath)
    {
        if (!File.Exists(filePath))
            return null;

        try
        {
            var json = File.ReadAllText(filePath);
            return JsonConvert.DeserializeObject<SettlementSnapshotFile>(json);
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"SettlementSnapshotStore: failed to load {filePath}: {ex.Message}");
            return null;
        }
    }

    public void Save(string filePath, INavigationCacheAdapter adapter)
    {
        try
        {
            var settlements = adapter.GetAllRegisteredSettlements();
            var (sceneCrc, navMeshCrc) = adapter.GetSceneCrcValues();

            var snapshots = settlements.Select(s => new SettlementSnapshot
            {
                Id = s.StringId,
                // ToVec2() reads only the cached position scalars — does NOT touch
                // Campaign.Current.MapSceneWrapper (which can be null in editor mode). Codex Finding 4.
                GateX = s.GatePosition.ToVec2().x,
                GateY = s.GatePosition.ToVec2().y,
                PortX = s.PortPosition.ToVec2().x,
                PortY = s.PortPosition.ToVec2().y,
                HasPort = s.HasPort,
                IsFortification = s.IsFortification,
            }).ToArray();

            var file = new SettlementSnapshotFile
            {
                SceneCrc = sceneCrc,
                NavMeshCrc = navMeshCrc,
                NavigationType = adapter.NavigationType.ToString(),
                Timestamp = DateTime.UtcNow,
                Settlements = snapshots,
            };

            var dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllText(filePath, JsonConvert.SerializeObject(file, Formatting.Indented));
            _logger.LogInfo($"SettlementSnapshotStore: saved {snapshots.Length} settlements to {filePath}");
        }
        catch (Exception ex)
        {
            _logger.LogError($"SettlementSnapshotStore: save failed: {ex.Message}");
        }
    }
}
