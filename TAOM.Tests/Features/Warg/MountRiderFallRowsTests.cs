using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.Warg;

/// <summary>
/// Every rider set holds every fall action of every mount usage (#1101). A rider who dies or falls on a mount plays
/// the usage's fall action from his own set; issue #1101's native Agent.Die fault and two v2.0.29.5 player crashes
/// (Discord, 4 and 13 Oct 2026, last line "as_dwarf_warrior does not contain act_warg_fall_roll") point at a rider set
/// without them. Before TAOM's dwarf rows the dwarf sets had none of the warg or spider falls.
/// The sets are resolved statically: base_set chain plus every same-id declaration (Native, LOTRLOME_Armory, the
/// Armory's action_sets.xslt rows for as_human_warrior, TAOM). The engine copies a base when a set is defined, so this
/// can pass where load order would still leave a gap; it cannot fail where the data is complete. LiveInstall: Native
/// and the Armory's xslt are not in the repo snapshot.
/// </summary>
[TestClass]
[TestCategory("LiveInstall")]
public class MountRiderFallRowsTests
{
    private static readonly string[] RiderSets =
    {
        "as_human_warrior", "as_human_female_warrior", "as_dwarf_warrior", "as_dwarf_female_warrior",
        "as_orc_warrior", "as_goblin_warrior", "as_goblin_female_warrior", "as_uruk_warrior", "as_uruk_female_warrior",
        "as_uruk_hai_warrior", "as_dg_uruk_warrior", "as_dg_uruk_female_warrior", "as_pale_uruk_warrior",
        "as_pale_uruk_female_warrior", "as_berserker_warrior", "as_nazghul_warrior", "as_saruman_warrior",
        "as_saruman_female_warrior",
    };

    private static readonly string[] MountUsages = { "horse", "warg", "spider", "elephant", "chariot" };

    [TestMethod]
    public void EveryRiderSet_HoldsEveryFallActionOfEveryMountUsage()
    {
        var sets = LoadSets();
        var falls = LoadFalls();
        var gaps = new List<string>();
        foreach (var usage in MountUsages)
        {
            Assert.IsTrue(falls.TryGetValue(usage, out var actions) && actions.Count > 0, $"usage {usage} has no fall rows");
            foreach (var set in RiderSets)
            {
                var have = Resolve(sets, set);
                Assert.IsTrue(have.Count > 0, $"{set} not found");
                gaps.AddRange(actions.Distinct().Where(a => !have.Contains(a)).Select(a => $"{set} lacks {a} ({usage})"));
            }
        }
        Assert.AreEqual(0, gaps.Count, string.Join("\n", gaps));
    }

    [TestMethod]
    public void DwarfSets_HoldTheWargFallsThePlayerLogsNamed()
    {
        var sets = LoadSets();
        foreach (var set in new[] { "as_dwarf_warrior", "as_dwarf_female_warrior" })
            CollectionAssert.IsSubsetOf(new[] { "act_warg_fall_roll", "act_warg_fall_left", "act_warg_fall_right" },
                Resolve(sets, set).ToList(), set);
    }

    private sealed class SetData
    {
        public string? Base;
        public HashSet<string> Types = new HashSet<string>();
    }

    private static Dictionary<string, SetData> LoadSets()
    {
        string modules = Path.Combine(GameDir(), "Modules");
        var sets = new Dictionary<string, SetData>();
        void Add(string? id, string? baseSet, IEnumerable<XElement> actions)
        {
            if (id == null) return;
            if (!sets.TryGetValue(id, out var data)) sets[id] = data = new SetData();
            data.Base ??= baseSet;
            data.Types.UnionWith(actions.Select(a => (string?)a.Attribute("type")).Where(t => t != null)!);
        }
        foreach (var file in new[]
                 {
                     Path.Combine(modules, "Native", "ModuleData", "action_sets.xml"),
                     Path.Combine(modules, "LOTRLOME_Armory", "ModuleData", "action_sets.xml"),
                     RepoPaths.RepoPath("Main", "_Module", "ModuleData", "action_sets.xml"),
                 })
            foreach (var s in XDocument.Load(file).Root!.Elements("action_set"))
                Add((string?)s.Attribute("id"), (string?)s.Attribute("base_set"), s.Elements("action"));

        XNamespace xsl = "http://www.w3.org/1999/XSL/Transform";
        var xslt = XDocument.Load(Path.Combine(modules, "LOTRLOME_Armory", "ModuleData", "action_sets.xslt"));
        foreach (var t in xslt.Descendants(xsl + "template"))
        {
            var m = Regex.Match((string?)t.Attribute("match") ?? "", @"^action_set\[@id='([^']+)'\]$");
            if (m.Success) Add(m.Groups[1].Value, null, t.Descendants("action"));
        }
        return sets;
    }

    private static HashSet<string> Resolve(Dictionary<string, SetData> sets, string id)
    {
        var all = new HashSet<string>();
        var seen = new HashSet<string>();
        for (string? cur = id; cur != null && seen.Add(cur) && sets.TryGetValue(cur, out var data); cur = data.Base)
            all.UnionWith(data.Types);
        return all;
    }

    private static Dictionary<string, List<string>> LoadFalls()
    {
        string modules = Path.Combine(GameDir(), "Modules");
        var falls = new Dictionary<string, List<string>>();
        foreach (var file in new[]
                 {
                     Path.Combine(modules, "Native", "ModuleData", "monster_usage_sets.xml"),
                     Path.Combine(modules, "LOTRLOME_Armory", "ModuleData", "monster_usage_sets.xml"),
                 })
            foreach (var u in XDocument.Load(file).Descendants("monster_usage_set"))
            {
                string id = (string?)u.Attribute("id") ?? "";
                if (!falls.TryGetValue(id, out var list)) falls[id] = list = new List<string>();
                list.AddRange(u.Descendants("monster_usage_fall").Select(f => (string?)f.Attribute("action")).Where(a => a != null)!);
            }
        return falls;
    }

    private static string GameDir()
    {
        string? env = Environment.GetEnvironmentVariable("BANNERLORD_GAME_DIR");
        string game = string.IsNullOrWhiteSpace(env) ? @"E:\Steam\steamapps\common\Mount & Blade II Bannerlord" : env!;
        if (!File.Exists(Path.Combine(game, "Modules", "LOTRLOME_Armory", "ModuleData", "action_sets.xml"))
            || !File.Exists(Path.Combine(game, "Modules", "Native", "ModuleData", "action_sets.xml")))
            Assert.Inconclusive("Native or LOTRLOME_Armory not installed on this machine; the live data cannot be checked here.");
        return game;
    }
}
