using System.Collections.Generic;
using System.Linq;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Map;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace TAOM.Features.DevConsole.Cheats;

/// <summary>
/// `taom.audit_settlement_entrances`: every settlement whose entrance land parties cannot use, plus the engine's own
/// corrected coordinate for each.
///
/// Why it earns its place: an unreachable entrance does not crash and does not log. It makes the AI
/// parties targeting that settlement fail their path query every tick, which the engine reports only
/// as a repeating "Path finding target is not valid" assert. That is invisible in a normal session; in
/// 2026-08-03 field testing only the reporters' own pathfinding instrumentation named three destinations.
///
/// Cheap checks first. The face's island: `FaceIslandIndex` is the engine's connected-component id, and the main
/// landmass is the island most settlements agree on. The face's terrain: a face group land parties may not use
/// (`PartyNavigationModel.GetInvalidTerrainTypesForNavigationType`). Then paths, only between entrances that pass the
/// cheap checks (the native pathfinder does not bounds-check a face): from the three nearest town or castle entrances,
/// with the excluded terrain, region costs, agent radius and cost limit vanilla's distance cache uses
/// (v1.5.7 SandBoxNavigationCache :23-25, :84, :89). Each path joins two entrances into a group, and only the largest
/// group counts as the main map (SettlementEntranceRules.ConnectedToMain). The island check alone passed all seven
/// entrances Silas He's report named (#1434): their faces are on the main island but cut off by excluded terrain.
///
/// A suggested coordinate comes from `GetAccessiblePointNearPosition`, the engine's own navmesh, and is offered only
/// when it passes the cheap checks and a path from the nearest main-map towns or castles reaches it. It goes into the
/// map editor, never into settlements.xml alone.
/// </summary>
public static class SettlementEntranceCheats
{
    private const string Usage =
        "Format is \"taom.audit_settlement_entrances\".\n"
        + "Checks every settlement's entrance (gate position if it has one, else its map position)\n"
        + "against the campaign navmesh: the island, the terrain land parties may use, and a path from\n"
        + "the nearest towns and castles. Reports each failure with an engine-computed replacement.";

    // Widening probe: the first radius whose point passes every check wins. It starts near enough to keep a corrected
    // gate visually at its settlement. A point the engine finds stays on the input's island, so a wider radius finds
    // more of that island; when it finds none, it returns a random point, which the cheap checks then judge. An
    // off-mesh input comes back unchanged at every radius (native 0x6DB910), so off-mesh entrances are not probed.
    private static readonly float[] SearchRadii = { 0.5f, 1f, 2f, 4f, 8f, 16f, 32f };

    private const int ReferenceCount = 3;
    // The engine returns one random point per call, so each radius takes several and keeps the nearest that passes:
    // one sample per radius missed Barnavon's pocket exit 3.9 units away and moved villages up to 29 units (#1434).
    private const int SamplesPerRadius = 8;
    private const float AgentRadius = 0.3f;     // vanilla's distance cache (SandBoxNavigationCache :89)

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

                // GatePosition is the point AI parties path to: gate_posX/gate_posY when the row has them, else
                // posX/posY (v1.5.7 Settlement.cs:961-967).
                var entrance = settlement.GatePosition;
                var position = settlement.Position;
                records.Add(new EntranceRecord
                {
                    Id = settlement.StringId,
                    Name = settlement.Name?.ToString() ?? settlement.StringId,
                    IsFortification = settlement.IsTown || settlement.IsCastle,
                    UsesGatePosition = entrance.X != position.X || entrance.Y != position.Y || settlement.IsTown || settlement.IsCastle,
                    Entrance = entrance,
                    Face = mapScene.GetFaceIndex(entrance),
                });
            }

            if (records.Count == 0) return "No settlements found.";

            // The main landmass is whatever island the most settlements sit on. Deriving it rather
            // than hardcoding an index keeps this correct across map edits and engine bumps.
            var mainIsland = records
                .Where(r => r.Face.IsValid())
                .GroupBy(r => r.Face.FaceIslandIndex)
                .OrderByDescending(g => g.Count())
                .Select(g => (int?)g.Key)
                .FirstOrDefault();

            if (mainIsland == null) return "No settlement resolved to a valid navmesh face. Is the map scene fully loaded?";

            var nav = new NavQuery(mapScene, mainIsland.Value);
            foreach (var r in records)
                r.Verdict = nav.Classify(r.Face, pathReached: null);

            var points = records.Select(r => (r.Entrance.X, r.Entrance.Y)).ToList();
            var candidate = records.Select(r => r.Verdict == SettlementEntranceRules.Verdict.Ok).ToList();
            var fortification = records.Select(r => r.IsFortification).ToList();
            var connected = SettlementEntranceRules.ConnectedToMain(points, candidate, fortification, ReferenceCount,
                (from, to) => nav.Reached(records[from], records[to].Entrance, records[to].Face));

            if (candidate.Count(c => c) > 1 && !connected.Any(c => c))
                return "No path query succeeded between any two entrances, so the audit cannot judge paths. Is the map scene fully loaded?";

            for (int i = 0; i < records.Count; i++)
            {
                if (candidate[i])
                    records[i].Verdict = nav.Classify(records[i].Face, connected[i]);
            }

            // References for the report and the suggestions: the nearest main-map towns and castles, for every record.
            var mainFortification = Enumerable.Range(0, records.Count).Select(i => fortification[i] && connected[i]).ToList();
            for (int i = 0; i < records.Count; i++)
            {
                records[i].References = SettlementEntranceRules.NearestReferences(points, i, mainFortification, ReferenceCount)
                    .Select(k => records[k]).ToList();
            }

            var broken = records.Where(r => r.Verdict != SettlementEntranceRules.Verdict.Ok).ToList();
            foreach (var record in broken)
                record.Suggestion = FindAccessiblePoint(mapScene, nav, record);

            return Format(records.Count, mainIsland.Value, broken);
        });

    private static string FindAccessiblePoint(IMapScene mapScene, NavQuery nav, EntranceRecord record)
    {
        if (record.Verdict == SettlementEntranceRules.Verdict.OffMesh)
            return "none: the entrance is off the navmesh; move it by hand onto nearby land and audit again";

        // Keep the original CampaignVec2 rather than rebuilding one from floats: its land/sea flag
        // is part of the query, and reconstructing it would guess at that.
        foreach (var radius in SearchRadii)
        {
            CampaignVec2? best = null;
            var bestFace = default(PathFaceRecord);
            var bestDistance = float.MaxValue;
            for (int s = 0; s < SamplesPerRadius; s++)
            {
                var candidate = mapScene.GetAccessiblePointNearPosition(record.Entrance, radius);
                var distance = candidate.ToVec2().Distance(record.Entrance.ToVec2());
                if (distance >= bestDistance) continue;
                var face = mapScene.GetFaceIndex(candidate);
                // The cheap checks first: the native path query does not bounds-check a face.
                if (nav.Classify(face, pathReached: null) != SettlementEntranceRules.Verdict.Ok) continue;
                if (nav.ReachedFromAny(record.References, candidate, face) == false) continue;
                best = candidate;
                bestFace = face;
                bestDistance = distance;
            }

            if (best is CampaignVec2 point)
                return $"{point.X:F4}, {point.Y:F4} (face {bestFace.FaceIndex}, radius {radius:F1}, {bestDistance:F2} away, terrain z {TerrainZ(mapScene, point):F3})";
        }

        return "no usable point found within 32 units";
    }

    // A scene entity keeps the z it is saved with (the map does not drop settlement icons onto the terrain), so a move
    // in scene.xscene needs the terrain height at the new point.
    private static float TerrainZ(IMapScene mapScene, CampaignVec2 point)
    {
        mapScene.GetTerrainHeightAndNormal(point.ToVec2(), out var height, out _);
        return height;
    }

    private static string Format(int total, int mainIsland, List<EntranceRecord> broken)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Checked {total} settlement entrance(s). Main landmass = navmesh island {mainIsland}.");

        if (broken.Count == 0)
        {
            sb.AppendLine("OK: every entrance is on the main landmass, on terrain land parties may use, and a path from the main map reaches it.");
            return sb.ToString().TrimEnd();
        }

        sb.AppendLine($"!! {broken.Count} entrance(s) land parties cannot use:");
        sb.AppendLine();
        foreach (var r in broken)
        {
            r.EntranceZ = TerrainZ(Campaign.Current.MapSceneWrapper, r.Entrance);
            var field = r.UsesGatePosition ? "gate_posX/gate_posY" : "posX/posY";
            var refs = r.References.Count == 0 ? "" : $"  near {string.Join(", ", r.References.Select(x => x.Id))}";
            sb.AppendLine($"  {r.Id} ({r.Name})");
            sb.AppendLine($"    {field} = {r.Entrance.X:F4}, {r.Entrance.Y:F4}  terrain z {r.EntranceZ:F3}  face {r.Face.FaceIndex}  island {r.Face.FaceIslandIndex}  group {r.Face.FaceGroupIndex}  {r.Verdict}{refs}");
            sb.AppendLine($"    suggested: {r.Suggestion}");
        }

        sb.AppendLine();
        sb.AppendLine("Move each in the map editor (a town's or castle's main_map_city_gate child, else the entity) and");
        sb.AppendLine("save; never edit settlements.xml alone (docs/modding/settlements.md step 7). Then rebuild the");
        sb.AppendLine("distance cache (Mod Options, TAOM, Map Tools, Rebuild Now).");
        return sb.ToString().TrimEnd();
    }

    /// <summary>The engine calls, with the settings vanilla's distance cache uses.</summary>
    private sealed class NavQuery
    {
        private readonly IMapScene _scene;
        private readonly int _mainIsland;
        private readonly int[] _excluded;
        private readonly int _toSea;
        private readonly int _toLand;

        public NavQuery(IMapScene scene, int mainIsland)
        {
            _scene = scene;
            _mainIsland = mainIsland;
            var models = Campaign.Current.Models;
            _excluded = models.PartyNavigationModel.GetInvalidTerrainTypesForNavigationType(MobileParty.NavigationType.Default) ?? new int[0];
            _toSea = models.MapDistanceModel.RegionSwitchCostFromLandToSea;
            _toLand = models.MapDistanceModel.RegionSwitchCostFromSeaToLand;
        }

        public SettlementEntranceRules.Verdict Classify(PathFaceRecord face, bool? pathReached)
            => SettlementEntranceRules.Classify(face.IsValid(), face.FaceIslandIndex, _mainIsland, face.FaceGroupIndex, _excluded, pathReached);

        // Both faces have passed the cheap checks; the cost limit is vanilla's (Campaign.cs:1120, SandBoxNavigationCache :84).
        // The search starts at the entrance under test: a cut-off one then floods only its own pocket, not the whole main
        // map. This assumes the navmesh is the same both ways, as vanilla's one-way neighbour check does (:137-140).
        public bool Reached(EntranceRecord from, CampaignVec2 target, PathFaceRecord targetFace)
            => _scene.GetPathDistanceBetweenAIFaces(targetFace, from.Face, target.ToVec2(), from.Entrance.ToVec2(),
                AgentRadius, Campaign.PathFindingMaxCostLimit, out _, _excluded, _toSea, _toLand);

        public bool? ReachedFromAny(List<EntranceRecord> references, CampaignVec2 target, PathFaceRecord targetFace)
            => references.Count == 0 ? null : references.Any(from => Reached(from, target, targetFace));
    }

    private sealed class EntranceRecord
    {
        public string Id;
        public string Name;
        public bool IsFortification;
        public bool UsesGatePosition;
        public CampaignVec2 Entrance;
        public float EntranceZ;
        public PathFaceRecord Face;
        public SettlementEntranceRules.Verdict Verdict;
        public List<EntranceRecord> References = new();
        public string Suggestion = "(not computed)";
    }
}
