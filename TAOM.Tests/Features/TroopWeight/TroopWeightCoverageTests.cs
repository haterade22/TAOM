using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Core;

namespace TAOM.Tests.Features.TroopWeight;

/// <summary>
/// Coverage of <c>troop_weights.xml</c>: the file is a hand-made per-id list and a troop that was never
/// listed weighs 1.0, so nothing but these tests notices a troop that was forgotten. Pins the gaps found in
/// the 15 Nov 2026 audit (Imladris infantry, Hired copies, the Dual Wield elves, the evil and dwarf level
/// 31 to 40 troops) and the rule that an upgrade never lowers the weight.
/// </summary>
[TestClass]
public class TroopWeightCoverageTests
{
    private sealed record Troop(string Id, int Level, string Culture, string[] Upgrades);

    private static Dictionary<string, float> _weights = null!;
    private static Dictionary<string, Troop> _troops = null!;

    [ClassInitialize]
    public static void Init(TestContext _)
    {
        var moduleData = CultureDataFixture.ModuleDataPath();

        _weights = XDocument.Load(Path.Combine(moduleData, "TroopWeights", "troop_weights.xml"))
            .Descendants("TroopWeight")
            .ToDictionary(
                e => (string)e.Attribute("id")!,
                e => float.Parse((string)e.Attribute("weight")!, CultureInfo.InvariantCulture),
                StringComparer.Ordinal);

        _troops = new Dictionary<string, Troop>(StringComparer.Ordinal);
        foreach (var file in Directory.GetFiles(Path.Combine(moduleData, "troops"), "troops_*.xml"))
        {
            foreach (var npc in XDocument.Load(file).Descendants("NPCCharacter"))
            {
                var id = (string?)npc.Attribute("id");
                var level = (int?)npc.Attribute("level");
                if (id == null || level == null)
                    continue;
                var culture = ((string?)npc.Attribute("culture") ?? "").Replace("Culture.", "");
                var ups = npc.Descendants("upgrade_target")
                    .Select(u => ((string?)u.Attribute("id") ?? "").Replace("NPCCharacter.", ""))
                    .ToArray();
                _troops[id] = new Troop(id, level.Value, culture, ups);
            }
        }
    }

    private static float WeightOf(string id) => _weights.TryGetValue(id, out var w) ? w : 1.0f;

    private static string Show(string id) =>
        $"{id} = {WeightOf(id).ToString(CultureInfo.InvariantCulture)}";

    /// <summary>The Hired copy of a troop is the same body in the same kit, so it fills the same slots.</summary>
    [TestMethod]
    public void EveryHiredCopy_WeighsTheSameAsItsOriginal()
    {
        var hired = _troops.Keys.Where(id => id.EndsWith("_merc", StringComparison.Ordinal)).ToList();
        Assert.IsTrue(hired.Count > 0, "no _merc troop resolved; the sweep is empty");

        var bad = new List<string>();
        foreach (var id in hired.OrderBy(id => id, StringComparer.Ordinal))
        {
            var original = id.Substring(0, id.Length - "_merc".Length);
            if (!_troops.ContainsKey(original))
                bad.Add($"{id}: no original '{original}'");
            else if (WeightOf(id) != WeightOf(original))
                bad.Add($"{Show(id)}, {Show(original)}");
        }

        Assert.AreEqual(0, bad.Count, "Hired copies that weigh differently from their original:\n  " + string.Join("\n  ", bad));
    }

    /// <summary>
    /// Upgrading a troop must not free party slots, and the AI upgrades on its own. Only troops with a
    /// troops_*.xml upgrade edge are checked; the Dual Wield edges come from an XSLT in that module and are
    /// pinned by <see cref="DualWieldElves_WeighByTheLevelRuleAndNeverBelowTheirParent"/>.
    /// </summary>
    [TestMethod]
    public void NoUpgrade_LowersTheWeight()
    {
        var drops = new List<string>();
        foreach (var t in _troops.Values.OrderBy(t => t.Id, StringComparer.Ordinal))
        {
            foreach (var target in t.Upgrades)
            {
                if (_troops.ContainsKey(target) && WeightOf(target) < WeightOf(t.Id))
                    drops.Add($"{Show(t.Id)} -> {Show(target)}");
            }
        }

        Assert.AreEqual(0, drops.Count, "upgrades that lower the weight:\n  " + string.Join("\n  ", drops));
    }

    /// <summary>
    /// Imladris Infantry and Mithlond Infantry (level 26) sat at 1.0 while the Bowman beside them, same
    /// parent and level, paid 2.0 (Mineshaft, Discord, 15 Nov 2026). They follow the "level 21 to 36 (2.0)"
    /// reading of the Rivendell block; the recruit (level 21) stays 1.0.
    /// </summary>
    [TestMethod]
    public void ImladrisInfantry_WeighsTheSameAsItsBowman()
    {
        Assert.AreEqual(WeightOf("imladris_bowman"), WeightOf("imladris_infantry"), "Rivendell");
        Assert.AreEqual(WeightOf("lindon_imladris_bowman"), WeightOf("lindon_imladris_infantry"), "Lindon");
        Assert.AreEqual(2.0f, WeightOf("imladris_infantry"));
    }

    /// <summary>
    /// The Dual Wield elves come from the separate DualWield module, so no troops_*.xml here knows them;
    /// their rows sit in this file because the weight service reads only this one. Level 31 pays 2.0 and
    /// level 41 and above 3.0, and a row never sits below the troop it upgrades from. Literal list on purpose.
    /// Rows for ids of a module that is not loaded are never looked up, so they cost nothing.
    /// </summary>
    [TestMethod]
    public void DualWieldElves_WeighByTheLevelRuleAndNeverBelowTheirParent()
    {
        var expected = new (string Id, string Parent, float Weight)[]
        {
            ("dw_imladris_bladedancer", "imladris_infantry", 2.0f),
            ("dw_lindon_bladedancer", "lindon_imladris_infantry", 2.0f),
            ("dw_imladris_blademaiden", "imladris_blademaster", 3.0f),
            ("dw_mirkwood_twinblade_warden", "mirkwood_guards", 3.0f),
            ("dw_mirkwood_twinblade_huntress", "mirkwood_wardens", 3.0f),
        };

        foreach (var (id, parent, weight) in expected)
        {
            Assert.IsTrue(_weights.ContainsKey(id), $"{id} has no row");
            Assert.AreEqual(weight, WeightOf(id), id);
            Assert.IsTrue(WeightOf(id) >= WeightOf(parent), $"{Show(id)} is below its parent {Show(parent)}");
        }
    }

    /// <summary>The Dual Wield evil twin-blades are level 31 troops of cultures that weigh level 31 at 2.0.</summary>
    [TestMethod]
    public void DualWieldEvilTwinBlades_WeighTwo()
    {
        foreach (var id in new[]
                 {
                     "dw_goblintown_twinblade", "dw_gundabad_pale_twinaxe",
                     "dw_isengard_urukhai_twinblade", "dw_mordor_uruk_twinblade",
                 })
            Assert.AreEqual(2.0f, WeightOf(id), id);
    }

    /// <summary>
    /// Pale Uruk Fang Rider (level 36) upgraded from the weight-2 Warg Tamer and dropped to 1.0, and Azog's
    /// Defiler (level 41) was the one level 41+ troop of a weighted culture with no row.
    /// </summary>
    [TestMethod]
    public void GundabadTrackerAndDreadRider_Weigh2And3()
    {
        Assert.AreEqual(2.0f, WeightOf("gundabad_tracker"));
        Assert.AreEqual(3.0f, WeightOf("gundabad_dread_rider_of_the_tower"));
    }

    /// <summary>
    /// The evil and dwarf trees weighed their level 31 to 40 troops one at a time and skipped many, so a
    /// level 36 Uruk sat at 2.0 above a level 31 Uruk at 1.0. Humans (Gondor, Rohan, Rhun, Harad, Dunland, ...)
    /// carry no weights by design and are not swept; this is haterade's call and is recorded in the PR.
    /// The disabled cave troll is named because its row is commented out on purpose.
    /// </summary>
    [TestMethod]
    public void EvilAndDwarfTroopsAtLevel31To40_AreWeighted()
    {
        var cultures = new HashSet<string>(StringComparer.Ordinal)
            { "erebor", "eredluin", "dolguldur", "isengard", "mordor", "gundabad", "goblin", "bluecraig", "mistymountainorcs" };
        var disabled = new HashSet<string>(StringComparer.Ordinal) { "cave_troll" };

        var missing = _troops.Values
            .Where(t => cultures.Contains(t.Culture) && t.Level >= 31 && t.Level < 41)
            .Where(t => !disabled.Contains(t.Id))
            .Where(t => WeightOf(t.Id) < 2.0f)
            .OrderBy(t => t.Culture, StringComparer.Ordinal).ThenBy(t => t.Id, StringComparer.Ordinal)
            .Select(t => $"L{t.Level} {t.Culture} {Show(t.Id)}")
            .ToList();

        Assert.AreEqual(0, missing.Count,
            $"{missing.Count} level 31 to 40 troops of weighted cultures still weigh under 2.0:\n  " + string.Join("\n  ", missing));
    }
}
