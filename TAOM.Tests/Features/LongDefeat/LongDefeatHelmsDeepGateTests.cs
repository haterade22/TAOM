using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Features.LongDefeat;

/// <summary>
/// The live Helm's Deep scene must keep one visible outer gate and at least one inner gate. The engine takes each gate from the <c>CastleGate</c> objects by tag
/// (TeamAISiegeComponent.cs:54-57) and reads <c>InnerGate.IsGateOpen</c> with no null check (BehaviorAssaultWalls.cs:122-174 once the outer gate is open,
/// MovementOrder.cs:902 on a defenders' charge). The scene is unversioned (TAOM_Map) and reworked in the Kit: the 2026-11-06 rework dropped the hidden inner gate,
/// and Mike put it back. A gate entity's own tags replace its prefab's (vanilla sturgia_castle_siege_001 tags an outer-gate prefab <c>inner_gate</c>), and only
/// an entity whose prefab root or own scripts hold <c>CastleGate</c> counts.
/// </summary>
[TestClass]
public class LongDefeatHelmsDeepGateTests
{
    [TestMethod]
    [TestCategory("LiveInstall")]
    public void HelmsDeep_HasOneVisibleOuterGate_AndAnInnerGate_SoTheSiegeAiFindsBoth()
    {
        var env = Environment.GetEnvironmentVariable("BANNERLORD_GAME_DIR");
        var game = string.IsNullOrWhiteSpace(env) ? @"E:\Steam\steamapps\common\Mount & Blade II Bannerlord" : env;
        var modules = Path.Combine(game, "Modules");
        var scene = Path.Combine(modules, "TAOM_Map", "SceneObj", "taom_rohan_castle_helms_deep_forceatmo", "scene.xscene");
        if (!File.Exists(scene))
            Assert.Inconclusive("the TAOM_Map Helm's Deep scene is not installed here; the gates cannot be read.");

        var prefabs = GatePrefabs(modules);
        var gates = new List<(List<string> Tags, bool Visible)>();
        foreach (var entity in XDocument.Load(scene).Root!.Element("entities")!.Elements("game_entity"))
        {
            prefabs.TryGetValue((string?)entity.Attribute("prefab") ?? "", out var prefab);
            var ownScript = entity.Elements("scripts").Elements("script").Any(s => (string?)s.Attribute("name") == "CastleGate");
            if (!ownScript && !prefab.CastleGate)
                continue;
            var tags = entity.Element("tags") != null ? Tags(entity).ToList() : prefab.Tags ?? new List<string>();
            gates.Add((tags, (string?)entity.Attribute("visible") != "false"));
        }

        Assert.AreEqual(1, gates.Count(g => g.Tags.Contains("outer_gate")), "Helm's Deep has one outer gate; the Long Defeat orders its gates by this tag");
        Assert.IsTrue(gates.Single(g => g.Tags.Contains("outer_gate")).Visible, "the outer gate is hidden: the Long Defeat keeps only visible gates and would break it as a hidden gate");
        Assert.IsTrue(gates.Any(g => g.Tags.Contains("inner_gate")), "no inner_gate CastleGate in the scene: the siege AI reads InnerGate with no null check; put the hidden aserai_castle_gate_inner_l1 back in the Kit");
    }

    private static IEnumerable<string> Tags(XElement entity) =>
        entity.Elements("tags").Elements("tag").Select(t => (string?)t.Attribute("name") ?? "");

    // Only a prefab file that names a gate tag is parsed; the rest are read as text once.
    private static Dictionary<string, (List<string>? Tags, bool CastleGate)> GatePrefabs(string modules)
    {
        var map = new Dictionary<string, (List<string>? Tags, bool CastleGate)>(StringComparer.Ordinal);
        foreach (var file in Directory.EnumerateFiles(modules, "*.xml", SearchOption.AllDirectories)
                     .Where(f => f.IndexOf(Path.DirectorySeparatorChar + "Prefabs" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) >= 0))
        {
            var text = File.ReadAllText(file);
            if (text.IndexOf("\"inner_gate\"", StringComparison.Ordinal) < 0 && text.IndexOf("\"outer_gate\"", StringComparison.Ordinal) < 0)
                continue;
            foreach (var prefab in XDocument.Parse(text).Root!.Elements("game_entity"))
                map[(string?)prefab.Attribute("name") ?? ""] = (Tags(prefab).ToList(),
                    prefab.Elements("scripts").Elements("script").Any(s => (string?)s.Attribute("name") == "CastleGate"));
        }

        return map;
    }
}
