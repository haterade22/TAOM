using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.CreatureBandits;

namespace TAOM.Tests.Features.CreatureBandits;

/// <summary>
/// Creature Bandits against the live, unversioned modules (#694). The brood anchors are hand-listed ids of TAOM_Map
/// settlements, and the troll band's map icon is drawn from the troll races' <c>_map</c> action sets in the Armory; a
/// rename or a new settlement in either module changes nothing in this repo, so only a live read can catch the drift.
/// Inconclusive on a machine without the modules.
/// </summary>
[TestClass]
[TestCategory("LiveInstall")]
public class CreatureBanditLiveDataTests
{
    private static string GameDir()
    {
        string? env = Environment.GetEnvironmentVariable("BANNERLORD_GAME_DIR");
        return string.IsNullOrWhiteSpace(env) ? @"E:\Steam\steamapps\common\Mount & Blade II Bannerlord" : env;
    }

    private static string LiveFile(string module, string relative)
    {
        string path = Path.Combine(GameDir(), "Modules", module, "ModuleData", relative);
        if (!File.Exists(path))
            Assert.Inconclusive($"{module} not installed on this machine; the live data cannot be checked here.");
        return path;
    }

    [TestMethod]
    public void BroodAnchors_AreEveryMirkwoodAndDolGuldurTownCastleAndVillage()
    {
        // The authored culture attribute, not Settlement.Culture at runtime: culture conversion rewrites that on
        // conquest. Hideouts are excluded: a brood patrols settlements a player passes.
        var expected = XDocument.Load(LiveFile("TAOM_Map", "settlements.xml")).Root!.Elements("Settlement")
            .Where(s => (string?)s.Attribute("culture") is "Culture.mirkwood" or "Culture.dolguldur")
            .Where(s => s.Descendants().Any(c => c.Name.LocalName is "Town" or "Village"))
            .Select(s => (string)s.Attribute("id")!)
            .ToList();
        Assert.IsTrue(expected.Count > 0, "no Mirkwood or Dol Guldur settlement found: the culture ids changed");
        CollectionAssert.AreEquivalent(expected, CreatureBanditsConfig.BroodAnchorSettlementIds.ToList(),
            "the brood anchors must be every Mirkwood and Dol Guldur town, castle and village on the live map");
    }

    [TestMethod]
    public void TrollMapActionSets_Exist()
    {
        // A troll band's map icon is its leader drawn with "as_" + race + "_map" (SandBoxViewHelpers
        // GetHumanAgentPartyVisual, ActionSetCode.GenerateActionSetNameWithSuffix), a hill troll's once the cave trolls
        // fall; MBGlobals.GetActionSet throws on a missing set, and nothing catches it on the map.
        var ids = XDocument.Load(LiveFile("LOTRLOME_Armory", "action_sets.xml")).Descendants("action_set")
            .Select(a => (string?)a.Attribute("id")).ToHashSet();
        foreach (var set in new[] { "as_cave_troll_map", "as_hill_troll_map" })
            Assert.IsTrue(ids.Contains(set), $"{set} is missing from the live Armory action_sets.xml");
    }
}
