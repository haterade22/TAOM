using System.Collections.Generic;
using System.Linq;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace TAOM.Features.DevConsole.Cheats;

/// <summary>
/// `taom.audit_settlement_entrances` — every settlement whose entrance sits on a navmesh island the
/// rest of the map cannot path to, plus the engine's own corrected coordinate for each.
///
/// Why it earns its place: an unreachable entrance does not crash and does not log. It makes the AI
/// parties targeting that settlement fail their path query every tick, which the engine reports only
/// as a repeating "Path finding target is not valid" assert — invisible in a normal session, and
/// visible to us in 2026-08-03 field testing only because the reporters had written their own
/// pathfinding instrumentation. Three destinations were named that way. This finds all of them.
///
/// The faces involved are NOT off-mesh: `PathFaceRecord.IsValid()` returns true for every one, so
/// nothing cheaper than an island comparison detects them. `FaceIslandIndex` is the engine's own
/// connected-component id — two faces with different island indices have no path between them at
/// any cost — so the main landmass is simply the island index most settlements agree on, and any
/// settlement disagreeing with it is unreachable by land from almost everywhere.
///
/// Coordinates come from `GetAccessiblePointNearPosition`, i.e. the engine's own navmesh, so the
/// output is a value to paste into settlements.xml rather than a guess to try.
/// </summary>
public static class SettlementEntranceCheats
{
    private const string Usage =
        "Format is \"taom.audit_settlement_entrances\".\n"
        + "Checks every settlement's entrance (gate position if it has one, else its map position)\n"
        + "against the campaign navmesh and reports any that sit on a disconnected island, with an\n"
        + "engine-computed replacement coordinate for each.";

    // Widening probe: the first radius that lands on the main island wins. Starts near enough to
    // keep a corrected gate visually at its settlement, ends wide enough to escape a small island.
    private static readonly float[] SearchRadii = { 1f, 2f, 4f, 8f, 16f, 32f };

    [CommandLineFunctionality.CommandLineArgumentFunction("audit_settlement_entrances", "taom")]
    public static string AuditSettlementEntrances(List<string> strings) =>
        TaomConsole.RunInCampaign(strings, Usage, args =>
        {
            var mapScene = Campaign.Current?.MapSceneWrapper;
            if (mapScene == null) return "The campaign map scene is not loaded.";

            var records = new List<EntranceRecord>();
            foreach (var settlement in Settlement.All)
            {
                if (settlement == null) continue;

                // A settlement with an explicit gate is entered AT THE GATE — that is the position
                // AI parties path to, and the one the field report's three failures all named.
                var hasGate = settlement.IsTown || settlement.IsCastle;
                var entrance = hasGate ? settlement.GatePosition : settlement.Position;
                var face = mapScene.GetFaceIndex(entrance);

                records.Add(new EntranceRecord
                {
                    Id = settlement.StringId,
                    Name = settlement.Name?.ToString() ?? settlement.StringId,
                    UsesGatePosition = hasGate,
                    Entrance = entrance,
                    FaceIndex = face.FaceIndex,
                    IslandIndex = face.FaceIslandIndex,
                    IsOnMesh = face.IsValid(),
                });
            }

            if (records.Count == 0) return "No settlements found.";

            // The main landmass is whatever island the most settlements sit on. Deriving it rather
            // than hardcoding an index keeps this correct across map edits and engine bumps.
            var mainIsland = records
                .Where(r => r.IsOnMesh)
                .GroupBy(r => r.IslandIndex)
                .OrderByDescending(g => g.Count())
                .Select(g => (int?)g.Key)
                .FirstOrDefault();

            if (mainIsland == null) return "No settlement resolved to a valid navmesh face — is the map scene fully loaded?";

            var broken = records
                .Where(r => !r.IsOnMesh || r.IslandIndex != mainIsland.Value)
                .ToList();

            foreach (var record in broken)
                record.Suggestion = FindAccessiblePoint(mapScene, record, mainIsland.Value);

            return Format(records.Count, mainIsland.Value, broken);
        });

    private static string FindAccessiblePoint(
        TaleWorlds.CampaignSystem.Map.IMapScene mapScene, EntranceRecord record, int mainIsland)
    {
        // Keep the original CampaignVec2 rather than rebuilding one from floats: its land/sea flag
        // is part of the query, and reconstructing it would guess at that.
        foreach (var radius in SearchRadii)
        {
            var candidate = mapScene.GetAccessiblePointNearPosition(record.Entrance, radius);
            var face = mapScene.GetFaceIndex(candidate);
            if (!face.IsValid() || face.FaceIslandIndex != mainIsland) continue;

            return $"{candidate.X:F4}, {candidate.Y:F4} (face {face.FaceIndex}, radius {radius:F0})";
        }

        return "no reachable point found within 32 units — needs the map editor";
    }

    private static string Format(int total, int mainIsland, List<EntranceRecord> broken)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Checked {total} settlement entrance(s). Main landmass = navmesh island {mainIsland}.");

        if (broken.Count == 0)
        {
            sb.AppendLine("OK: every entrance resolves to a face on the main landmass.");
            return sb.ToString().TrimEnd();
        }

        sb.AppendLine($"!! {broken.Count} entrance(s) are unreachable from the main landmass:");
        sb.AppendLine();
        foreach (var r in broken)
        {
            var field = r.UsesGatePosition ? "gate_posX/gate_posY" : "posX/posY";
            var where = r.IsOnMesh ? $"island {r.IslandIndex}" : "OFF-MESH";
            sb.AppendLine($"  {r.Id} ({r.Name})");
            sb.AppendLine($"    {field} = {r.Entrance.X:F4}, {r.Entrance.Y:F4}  face {r.FaceIndex}  {where}");
            sb.AppendLine($"    suggested: {r.Suggestion}");
        }

        sb.AppendLine();
        sb.AppendLine("Apply these to the LIVE TAOM_Map\\ModuleData\\settlements.xml — the copy under");
        sb.AppendLine("Main/_Module/ModuleData/ is a stale shadow and edits there never reach the game.");
        return sb.ToString().TrimEnd();
    }

    private sealed class EntranceRecord
    {
        public string Id;
        public string Name;
        public bool UsesGatePosition;
        public CampaignVec2 Entrance;
        public int FaceIndex;
        public int IslandIndex;
        public bool IsOnMesh;
        public string Suggestion = "(not computed)";
    }
}
