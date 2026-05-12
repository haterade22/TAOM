using System;
using System.IO;
using Newtonsoft.Json;
using TAOM.Adapters;
using TAOM.Core.Logging;

namespace TAOM.Features.EditorCacheRebuild.Checkpoint;

public class CheckpointSerializer : ICheckpointSerializer
{
    private readonly IModLogger _logger;

    public CheckpointSerializer(IModLogger logger)
    {
        _logger = logger;
    }

    public CheckpointMetadata? TryLoad(string baseDirectory, string navigationType, INavigationCacheAdapter adapter)
    {
        var metaPath = MetaPath(baseDirectory, navigationType);
        var binPath = BinPath(baseDirectory, navigationType);

        if (!File.Exists(metaPath) || !File.Exists(binPath))
            return null;

        CheckpointMetadata? metadata;
        try
        {
            var json = File.ReadAllText(metaPath);
            metadata = JsonConvert.DeserializeObject<CheckpointMetadata>(json);
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"CheckpointSerializer: failed to parse {metaPath}: {ex.Message}");
            return null;
        }

        if (metadata == null)
            return null;

        var (sceneCrc, navMeshCrc) = adapter.GetSceneCrcValues();
        if (metadata.SceneCrc != sceneCrc || metadata.NavMeshCrc != navMeshCrc)
        {
            _logger.LogInfo(
                $"CheckpointSerializer: CRC mismatch (scene {metadata.SceneCrc:X8}/{sceneCrc:X8}, " +
                $"navmesh {metadata.NavMeshCrc:X8}/{navMeshCrc:X8}) — invalidating checkpoint");
            return null;
        }

        if (metadata.NavigationType != navigationType)
        {
            _logger.LogInfo($"CheckpointSerializer: nav type mismatch ({metadata.NavigationType} vs {navigationType})");
            return null;
        }

        try
        {
            adapter.DeserializeCache(binPath);
            _logger.LogInfo($"CheckpointSerializer: loaded checkpoint phase={metadata.PhaseCompleted} from {binPath}");
            return metadata;
        }
        catch (Exception ex)
        {
            _logger.LogError($"CheckpointSerializer: failed to deserialize {binPath}: {ex.Message}");
            return null;
        }
    }

    public void Save(string baseDirectory, string navigationType, INavigationCacheAdapter adapter, int phaseCompleted)
    {
        try
        {
            if (!Directory.Exists(baseDirectory))
                Directory.CreateDirectory(baseDirectory);

            var binPath = BinPath(baseDirectory, navigationType);
            var metaPath = MetaPath(baseDirectory, navigationType);

            adapter.SerializeCache(binPath);

            var (sceneCrc, navMeshCrc) = adapter.GetSceneCrcValues();
            var metadata = new CheckpointMetadata
            {
                Timestamp = DateTime.UtcNow,
                SceneCrc = sceneCrc,
                NavMeshCrc = navMeshCrc,
                PhaseCompleted = phaseCompleted,
                NavigationType = navigationType,
            };
            File.WriteAllText(metaPath, JsonConvert.SerializeObject(metadata, Formatting.Indented));

            _logger.LogInfo($"CheckpointSerializer: saved checkpoint phase={phaseCompleted} to {binPath}");
        }
        catch (Exception ex)
        {
            _logger.LogError($"CheckpointSerializer: save failed: {ex.Message}");
        }
    }

    public void Delete(string baseDirectory, string navigationType)
    {
        try
        {
            var binPath = BinPath(baseDirectory, navigationType);
            var metaPath = MetaPath(baseDirectory, navigationType);
            if (File.Exists(binPath)) File.Delete(binPath);
            if (File.Exists(metaPath)) File.Delete(metaPath);
            _logger.LogDebug($"CheckpointSerializer: deleted checkpoint files for {navigationType}");
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"CheckpointSerializer: delete failed: {ex.Message}");
        }
    }

    private static string BinPath(string baseDirectory, string navigationType) =>
        Path.Combine(baseDirectory, $"settlements_distance_cache_{navigationType}.ckpt.bin");

    private static string MetaPath(string baseDirectory, string navigationType) =>
        Path.Combine(baseDirectory, $"settlements_distance_cache_{navigationType}.ckpt.meta");
}
