using System;
using System.IO;
using System.Reflection;
using TaleWorlds.Library;
using TAOM.Core.Logging;

namespace TAOM.Features.EditorCacheRebuild.Caching;

public class PersistentPathCache : IPersistentPathCache
{
    private const uint Magic = 0x43504154; // "TAPC" little-endian
    private const int FormatVersion = 1;
    private const int MaxWaypoints = 128;

    private readonly IModLogger _logger;

    public PersistentPathCache(IModLogger logger)
    {
        _logger = logger;
    }

    public bool TryLoad(string filePath, uint expectedSceneCrc, uint expectedNavMeshCrc, IPathReuseCache target)
    {
        if (!File.Exists(filePath))
        {
            _logger.LogDebug($"PersistentPathCache: {filePath} not found");
            return false;
        }

        try
        {
            using var stream = File.OpenRead(filePath);
            using var reader = new System.IO.BinaryReader(stream);

            var magic = reader.ReadUInt32();
            if (magic != Magic)
            {
                _logger.LogWarning($"PersistentPathCache: {filePath} has wrong magic 0x{magic:X8} (expected 0x{Magic:X8})");
                return false;
            }

            var version = reader.ReadInt32();
            if (version != FormatVersion)
            {
                _logger.LogWarning($"PersistentPathCache: {filePath} has format version {version} (expected {FormatVersion})");
                return false;
            }

            var sceneCrc = reader.ReadUInt32();
            var navMeshCrc = reader.ReadUInt32();
            if (sceneCrc != expectedSceneCrc || navMeshCrc != expectedNavMeshCrc)
            {
                _logger.LogInfo($"PersistentPathCache: {filePath} CRC mismatch (scene {sceneCrc:X8} vs {expectedSceneCrc:X8}, navmesh {navMeshCrc:X8} vs {expectedNavMeshCrc:X8}) — invalidating");
                return false;
            }

            var pairCount = reader.ReadInt32();
            for (int i = 0; i < pairCount; i++)
            {
                var id1 = reader.ReadString();
                var isPort1 = reader.ReadBoolean();
                var id2 = reader.ReadString();
                var isPort2 = reader.ReadBoolean();
                var size = reader.ReadInt32();

                if (size < 0 || size > MaxWaypoints)
                {
                    _logger.LogWarning($"PersistentPathCache: {filePath} pair {i} has invalid waypoint count {size}");
                    return false;
                }

                var path = new NavigationPath();
                for (int p = 0; p < size; p++)
                {
                    var x = reader.ReadSingle();
                    var y = reader.ReadSingle();
                    path.PathPoints[p] = new Vec2(x, y);
                }
                path.Size = size;
                target.Store(id1, isPort1, id2, isPort2, path);
            }

            _logger.LogInfo($"PersistentPathCache: loaded {pairCount} paths from {filePath}");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError($"PersistentPathCache: failed to load {filePath}: {ex.Message}");
            return false;
        }
    }

    public void Save(string filePath, uint sceneCrc, uint navMeshCrc, IPathReuseCache source)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        var tempPath = filePath + ".tmp";

        try
        {
            var entries = ExtractEntries(source);

            using (var stream = File.Open(tempPath, FileMode.Create, FileAccess.Write))
            using (var writer = new System.IO.BinaryWriter(stream))
            {
                writer.Write(Magic);
                writer.Write(FormatVersion);
                writer.Write(sceneCrc);
                writer.Write(navMeshCrc);
                writer.Write(entries.Length);

                foreach (var entry in entries)
                {
                    writer.Write(entry.Key.Id1);
                    writer.Write(entry.Key.IsPort1);
                    writer.Write(entry.Key.Id2);
                    writer.Write(entry.Key.IsPort2);
                    writer.Write(entry.Path.Size);
                    for (int p = 0; p < entry.Path.Size; p++)
                    {
                        writer.Write(entry.Path.PathPoints[p].x);
                        writer.Write(entry.Path.PathPoints[p].y);
                    }
                }
            }

            if (File.Exists(filePath))
                File.Delete(filePath);
            File.Move(tempPath, filePath);

            _logger.LogInfo($"PersistentPathCache: saved {entries.Length} paths to {filePath}");
        }
        catch (Exception ex)
        {
            _logger.LogError($"PersistentPathCache: failed to save {filePath}: {ex.Message}");
            if (File.Exists(tempPath))
            {
                try { File.Delete(tempPath); } catch { /* best-effort cleanup */ }
            }
        }
    }

    private static (SortedPathKey Key, NavigationPath Path)[] ExtractEntries(IPathReuseCache source)
    {
        // PathReuseCache uses ConcurrentDictionary internally; we access via reflection on the
        // private _store field to avoid expanding IPathReuseCache's surface with enumeration.
        var storeField = typeof(PathReuseCache).GetField("_store", BindingFlags.NonPublic | BindingFlags.Instance);
        if (storeField == null || source is not PathReuseCache concrete)
            return Array.Empty<(SortedPathKey, NavigationPath)>();

        var dict = (System.Collections.Concurrent.ConcurrentDictionary<SortedPathKey, NavigationPath>)storeField.GetValue(concrete)!;
        var result = new (SortedPathKey, NavigationPath)[dict.Count];
        int i = 0;
        foreach (var kvp in dict)
            result[i++] = (kvp.Key, kvp.Value);
        return result;
    }
}
