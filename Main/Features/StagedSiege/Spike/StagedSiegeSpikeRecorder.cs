using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TaleWorlds.Library;

namespace TAOM.Features.StagedSiege.Spike;

/// <summary>One recorded point of a stage (JSONL, one object per line).</summary>
internal sealed class SpikeRecord
{
    public string Scene;
    public char Stage;
    public string Kind;
    public Vec3 Position;
    public Vec2 Facing;
}

/// <summary>
/// Reads and appends <c>staged_siege_records.jsonl</c> in the player's Documents (Documents\Mount and Blade II Bannerlord\TAOM),
/// never in the game install. Numbers are written by Newtonsoft, which is culture-invariant.
/// </summary>
internal static class StagedSiegeSpikeRecorder
{
    internal static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "Mount and Blade II Bannerlord", "TAOM", "staged_siege_records.jsonl");

    internal static string Append(string scene, char stage, string kind, Vec3 position, Vec2 facing, float radius)
    {
        var line = new JObject
        {
            ["scene"] = scene,
            ["stage"] = stage.ToString(),
            ["kind"] = kind,
            ["x"] = position.x,
            ["y"] = position.y,
            ["z"] = position.z,
            ["fx"] = facing.x,
            ["fy"] = facing.y,
            ["radius"] = radius,
            ["utc"] = DateTime.UtcNow.ToString("o", System.Globalization.CultureInfo.InvariantCulture),
        }.ToString(Formatting.None);
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
        File.AppendAllText(FilePath, line + "\n", new UTF8Encoding(false));
        return line;
    }

    /// <summary>The newest <c>point</c> record per stage letter A to D for <paramref name="scene"/>; unreadable lines are counted.</summary>
    internal static Dictionary<char, SpikeRecord> LatestPoints(string scene, out int badLines)
    {
        badLines = 0;
        var result = new Dictionary<char, SpikeRecord>();
        if (!File.Exists(FilePath)) return result;
        foreach (var text in File.ReadAllLines(FilePath))
        {
            if (string.IsNullOrWhiteSpace(text)) continue;
            try
            {
                var o = JObject.Parse(text);
                var stage = ((string)o["stage"])?.Trim().ToUpperInvariant();
                if ((string)o["kind"] != "point" || stage == null || stage.Length != 1 || stage[0] < 'A' || stage[0] > 'D') continue;
                if (!string.Equals((string)o["scene"], scene, StringComparison.OrdinalIgnoreCase)) continue;
                result[stage[0]] = new SpikeRecord
                {
                    Scene = scene, Stage = stage[0], Kind = "point",
                    Position = new Vec3((float)o["x"], (float)o["y"], (float)o["z"]),
                    Facing = new Vec2((float)o["fx"], (float)o["fy"]),
                };
            }
            catch (Exception) { badLines++; }
        }
        return result;
    }
}
