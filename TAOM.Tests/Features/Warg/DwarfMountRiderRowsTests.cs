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
/// Pins TAOM's dwarf rider rows (Main/_Module/ModuleData/action_sets.xml) to the Armory's human ones. A dwarf whose
/// mount died mounted a free warg and the game crashed (owner report, 2026-11-14): LOTRLOME_Armory gives the warg,
/// spider, elephant and chariot rider rows to as_human_warrior only, and as_dwarf_warrior has no base_set. TAOM merges
/// the same rows into both dwarf warrior sets. The female copy is a guard, not a need: the managed loader
/// (Module.CreateProcessedActionSetsXMLForNative) merges TAOM's rows into the Armory's as_dwarf_warrior element before
/// native parses the document (the Armory loads before TAOM in the normal list), so as_dwarf_female_warrior
/// (base_set="as_dwarf_warrior") inherits them anyway (native side per the review's native decompile). It can go once
/// in-game step 2 of issue #1226 passes. The repo's Armory snapshot checks the warg and spider rows always; the
/// elephant and chariot rows come from the live action_sets.xslt, which the snapshot does not hold (LiveInstall).
/// </summary>
[TestClass]
public class DwarfMountRiderRowsTests
{
    private const string TaomFile = "Main/_Module/ModuleData/action_sets.xml";
    private static readonly string[] DwarfSets = { "as_dwarf_warrior", "as_dwarf_female_warrior" };

    [TestMethod]
    public void TaomFile_GivesBothDwarfWarriorSetsTheSameRows()
    {
        var male = TaomDwarfRows("as_dwarf_warrior");
        var female = TaomDwarfRows("as_dwarf_female_warrior");
        Assert.IsTrue(male.Count > 0, "as_dwarf_warrior has no rider rows in TAOM's action_sets.xml");
        CollectionAssert.AreEquivalent(male, female, "the female guard block must hold the same rows as the male block (#1226)");
    }

    [TestMethod]
    public void TaomFile_DwarfRows_CarryOnlyCreatureMountActions()
    {
        // The xslt's act_horse_jump_* rows would replace the dwarf's horse jump clips with an elephant clip.
        foreach (var set in DwarfSets)
            foreach (var row in TaomDwarfRows(set))
                StringAssert.Matches(row.Type, new Regex("^(?!act_horse_|act_howdah_|act_ride)act_"),
                    $"{set} carries {row.Type}");
    }

    [DataTestMethod]
    [DataRow("act_warg_forward_walk", "rider_warg_forward_walk")]
    [DataRow("act_warg_attack_running", "rider_warg_attack_running")]
    [DataRow("act_spider_idle", "rider_warg_idle")]
    [DataRow("act_chariot_forward_walk", "chariot_rider_forward_walk_stand")]
    [DataRow("act_elephant_forward_walk", "elephant_rider_walk")]
    public void TaomFile_DwarfRows_IncludeTheWalkAndAttackRows(string type, string clip)
    {
        foreach (var set in DwarfSets)
            CollectionAssert.Contains(TaomDwarfRows(set), (type, clip), $"{set} lacks {type}");
    }

    [TestMethod]
    public void Snapshot_EveryHumanWargAndSpiderRow_IsInTheDwarfRows()
    {
        var human = HumanPartialRows(Path.Combine(RepoPaths.RepoPath("docs", "reference", "lotrlome-armory-snapshot"), "action_sets.xml"));
        Assert.AreEqual(122, human.Count, "the snapshot's as_human_warrior partial changed size: refresh TAOM's dwarf rows");
        CollectionAssert.IsSubsetOf(human, TaomDwarfRows("as_dwarf_warrior"));
    }

    [TestMethod]
    [TestCategory("LiveInstall")]
    public void LiveArmory_DwarfRows_EqualTheHumanCreatureRiderRows()
    {
        string live = ArmoryModuleData();
        var expected = HumanPartialRows(Path.Combine(live, "action_sets.xml"))
            .Concat(XsltCreatureRows(Path.Combine(live, "action_sets.xslt"))).ToList();
        const string refresh = "TAOM's dwarf rider rows drifted from the Armory's as_human_warrior rows. Refresh: copy the " +
            "Armory as_human_warrior partial rows (action_sets.xml) and its action_sets.xslt creature rows, without " +
            "act_horse_jump_* and act_howdah_*, into TAOM's dwarf block in Main/_Module/ModuleData/action_sets.xml (both sets).";
        foreach (var set in DwarfSets)
            CollectionAssert.AreEquivalent(expected, TaomDwarfRows(set), $"{set}: {refresh}");
    }

    [TestMethod]
    [TestCategory("LiveInstall")]
    public void LiveArmory_DwarfWarriorSetsHaveNoRiderRowsOfTheirOwn()
    {
        // If the Armory ever gives the dwarves these rows, TAOM's copy is redundant and should go.
        var sets = XDocument.Load(Path.Combine(ArmoryModuleData(), "action_sets.xml")).Descendants("action_set");
        foreach (var set in sets.Where(s => DwarfSets.Contains((string?)s.Attribute("id"))))
            Assert.IsFalse(set.Elements("action").Any(a => ((string?)a.Attribute("type"))?.StartsWith("act_warg_") == true),
                $"{(string?)set.Attribute("id")} now binds warg rows in the Armory");
    }

    [TestMethod]
    [TestCategory("LiveInstall")]
    public void LiveArmory_DwarfWarriorSet_BindsNoTypeThatTaomsRowsAlsoBind()
    {
        // A shared type would be replaced by TAOM's row, changing a clip the dwarf set already plays.
        var armory = XDocument.Load(Path.Combine(ArmoryModuleData(), "action_sets.xml")).Root!.Elements("action_set")
            .Where(s => (string?)s.Attribute("id") == "as_dwarf_warrior").SelectMany(s => Rows(s).Select(r => r.Type)).ToHashSet();
        var shared = TaomDwarfRows("as_dwarf_warrior").Select(r => r.Type).Where(armory.Contains).ToList();
        Assert.AreEqual(0, shared.Count, "TAOM's dwarf rows replace Armory bindings: " + string.Join(", ", shared));
    }

    private static List<(string Type, string Clip)> TaomDwarfRows(string setId)
    {
        var sets = XDocument.Load(RepoPaths.RepoPath(TaomFile.Split('/'))).Root!.Elements("action_set")
            .Where(s => (string?)s.Attribute("id") == setId).ToList();
        Assert.AreEqual(1, sets.Count, $"{setId} must be declared once in {TaomFile}");
        Assert.IsNull(sets[0].Attribute("base_set"), $"{setId} in {TaomFile} is a merge, not a new set");
        return Rows(sets[0]);
    }

    // The partial as_human_warrior at the top of the Armory's action_sets.xml: its warg and spider rider rows.
    private static List<(string Type, string Clip)> HumanPartialRows(string actionSetsPath)
    {
        var partial = XDocument.Load(actionSetsPath).Root!.Elements("action_set")
            .Single(s => (string?)s.Attribute("id") == "as_human_warrior");
        return Rows(partial);
    }

    private static List<(string Type, string Clip)> XsltCreatureRows(string xsltPath)
    {
        XNamespace xsl = "http://www.w3.org/1999/XSL/Transform";
        var template = XDocument.Load(xsltPath).Descendants(xsl + "template")
            .Single(t => (string?)t.Attribute("match") == "action_set[@id='as_human_warrior']");
        // By exclusion, so a creature family added to the xslt is seen: the horse jumps would replace the dwarf's own
        // jump clips, and act_howdah_stand_bow is played by nothing (TaomHowdahStandingPoint.cs sets no action channel).
        return template.Descendants("action").Select(Row)
            .Where(r => !Regex.IsMatch(r.Type, "^act_(horse|howdah)_")).ToList();
    }

    private static List<(string Type, string Clip)> Rows(XElement set) => set.Elements("action").Select(Row).ToList();

    private static (string Type, string Clip) Row(XElement a) => ((string?)a.Attribute("type") ?? "", (string?)a.Attribute("animation") ?? "");

    private static string ArmoryModuleData()
    {
        string? env = Environment.GetEnvironmentVariable("BANNERLORD_GAME_DIR");
        string game = string.IsNullOrWhiteSpace(env) ? @"E:\Steam\steamapps\common\Mount & Blade II Bannerlord" : env!;
        string dir = Path.Combine(game, "Modules", "LOTRLOME_Armory", "ModuleData");
        if (!Directory.Exists(dir))
            Assert.Inconclusive("LOTRLOME_Armory not installed on this machine; the live data cannot be checked here.");
        return dir;
    }
}
