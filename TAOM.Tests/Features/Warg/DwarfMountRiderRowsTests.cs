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
/// Pins the dwarf rider rows in LOTRLOME_Armory's action_sets.xml (the repo's snapshot always, the live install as
/// LiveInstall) to the Armory's human ones. A dwarf whose mount died mounted a free warg and the game crashed (owner
/// report, 2026-11-14, issue #1226): the Armory gave the warg, spider, elephant and chariot rider rows to
/// as_human_warrior only, and as_dwarf_warrior has no base_set. The rows now live in the Armory's as_dwarf_warrior
/// element. as_dwarf_female_warrior (base_set="as_dwarf_warrior") inherits them: v1.5.7 native FUN_180591470 (0x591470)
/// copies the base's slots when it parses the child, and its driver FUN_180590080 (0x590080) parses the sets in
/// document order, so the male set must be declared first. Until the move, TAOM's own action_sets.xml held the rows
/// (PR #1209); it must not declare the dwarf sets again, or the copies drift. The snapshot does not hold the Armory's
/// action_sets.xslt, so the 101 elephant and chariot rows are pinned offline by their family counts and compared with
/// the xslt only in the live test. The live test also serves as the release-channel check
/// (docs/reference/release-process.md): point BANNERLORD_GAME_DIR at a channel folder.
/// </summary>
[TestClass]
public class DwarfMountRiderRowsTests
{
    private const string TaomFile = "Main/_Module/ModuleData/action_sets.xml";
    private const string DwarfSet = "as_dwarf_warrior";
    private const string FemaleSet = "as_dwarf_female_warrior";

    // The creature mount rider action types: the warg, spider, elephant and chariot rows, the chariot mounts and the elephant dismounts.
    private static readonly Regex RiderType = new Regex("^act_(mount_|dismount_)?(warg|spider|elephant|chariot)_", RegexOptions.CultureInvariant);

    [TestMethod]
    public void TaomFile_DeclaresNoDwarfWarriorSet()
    {
        var ids = XDocument.Load(RepoPaths.RepoPath(TaomFile.Split('/'))).Root!.Elements("action_set")
            .Select(s => (string?)s.Attribute("id")).Where(id => id != null && id.StartsWith("as_dwarf_")).ToList();
        Assert.AreEqual(0, ids.Count, "the dwarf rider rows live in LOTRLOME_Armory now (#1226): " + string.Join(", ", ids));
    }

    [TestMethod]
    public void Snapshot_DwarfWarriorSet_BindsEveryHumanWargAndSpiderRow()
    {
        string snapshot = SnapshotActionSets();
        var human = HumanPartialRows(snapshot);
        Assert.AreEqual(131, human.Count, "the snapshot's as_human_warrior partial changed size: refresh the dwarf rows");
        AssertEachBoundToItsClip(human, DwarfRows(snapshot), "snapshot as_dwarf_warrior", "");
    }

    [DataTestMethod]
    [DataRow("^act_warg_", 75)]
    [DataRow("^act_spider_", 56)]
    [DataRow("^act_elephant_", 72)]
    [DataRow("^act_chariot_", 23)]
    [DataRow("^act_mount_chariot_", 4)]
    [DataRow("^act_dismount_elephant_", 2)]
    public void Snapshot_DwarfWarriorSet_BindsTheCreatureFamilyRows(string typePattern, int count)
    {
        // The elephant and chariot rows come from the Armory's xslt, which the snapshot does not hold: this pin keeps them offline.
        var rows = DwarfRows(SnapshotActionSets());
        Assert.AreEqual(count, rows.Count(r => Regex.IsMatch(r.Type, typePattern)),
            $"as_dwarf_warrior binds a different number of {typePattern} rows: refresh the dwarf rows and this pin together");
    }

    [TestMethod]
    public void Snapshot_DwarfWarriorSet_BindsEachCreatureTypeOnce()
    {
        // A repeated type is the state a copy-paste of the human rows leaves behind; native keeps the last row of a type.
        var types = DwarfRows(SnapshotActionSets()).Where(r => RiderType.IsMatch(r.Type)).Select(r => r.Type).ToList();
        Assert.AreEqual(232, types.Count, "the dwarf set must hold 232 creature rider rows");
        var repeated = types.GroupBy(t => t).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        Assert.AreEqual(0, repeated.Count, "as_dwarf_warrior binds a creature type twice: " + string.Join(", ", repeated));
    }

    [DataTestMethod]
    [DataRow("act_warg_forward_walk", "rider_warg_forward_walk")]
    [DataRow("act_warg_attack_running", "rider_warg_attack_running")]
    [DataRow("act_spider_idle", "rider_warg_idle")]
    [DataRow("act_chariot_forward_walk", "chariot_rider_forward_walk_stand")]
    [DataRow("act_elephant_forward_walk", "elephant_rider_walk")]
    public void Snapshot_DwarfWarriorSet_IncludesTheWalkAndAttackRows(string type, string clip)
    {
        CollectionAssert.Contains(DwarfRows(SnapshotActionSets()), (type, clip), $"{DwarfSet} lacks {type}");
    }

    [TestMethod]
    public void Snapshot_FemaleDwarfSet_InheritsFromTheMaleSetDeclaredBeforeIt()
    {
        var sets = XDocument.Load(SnapshotActionSets()).Root!.Elements("action_set").ToList();
        Assert.AreEqual(1, sets.Count(s => (string?)s.Attribute("id") == FemaleSet), $"{FemaleSet} must be declared once");
        int female = sets.FindIndex(s => (string?)s.Attribute("id") == FemaleSet);
        int male = sets.FindIndex(s => (string?)s.Attribute("id") == DwarfSet);
        Assert.AreEqual(DwarfSet, (string?)sets[female].Attribute("base_set"), $"{FemaleSet} must inherit the rider rows through base_set");
        Assert.IsTrue(male >= 0 && male < female,
            "native copies a base into its child when it parses the child, in document order: the male set must come first");
    }

    [TestMethod]
    [TestCategory("LiveInstall")]
    public void LiveArmory_DwarfWarriorSet_BindsTheHumanCreatureRiderRows()
    {
        string live = ArmoryModuleData();
        var partial = HumanPartialRows(Path.Combine(live, "action_sets.xml"));
        var template = XsltCreatureRows(Path.Combine(live, "action_sets.xslt"));
        Assert.IsTrue(partial.Count > 0 && template.Count > 0, "the Armory's human creature rider rows were not found: nothing to compare");
        AssertEachBoundToItsClip(partial.Concat(template).ToList(), DwarfRows(Path.Combine(live, "action_sets.xml")), "live as_dwarf_warrior",
            "Copy the creature rider rows of as_human_warrior into the dwarf set, without act_horse_jump_* and act_howdah_* " +
            "(the horse jumps would replace the dwarf's own jump clips).");
    }

    [TestMethod]
    [TestCategory("LiveInstall")]
    public void LiveArmory_DwarfWarriorRows_EqualTheSnapshot()
    {
        // The snapshot is the only tracked copy; a live edit that skips it reverts on reinstall. Only the dwarf rows are compared:
        // other sessions edit the rest of the 4.5 MB file, and the README allows the snapshot to lag there.
        var live = DwarfRows(Path.Combine(ArmoryModuleData(), "action_sets.xml"));
        var snapshot = DwarfRows(SnapshotActionSets());
        var onlyLive = live.Except(snapshot).ToList();
        var onlySnapshot = snapshot.Except(live).ToList();
        Assert.IsTrue(onlyLive.Count == 0 && onlySnapshot.Count == 0,
            $"as_dwarf_warrior rows differ: {onlyLive.Count} only in live (first: {string.Join(", ", onlyLive.Take(3))}), " +
            $"{onlySnapshot.Count} only in the snapshot (first: {string.Join(", ", onlySnapshot.Take(3))}). " +
            "If live is newer, refresh docs/reference/lotrlome-armory-snapshot/action_sets.xml; after a reinstall, restore live from it (the snapshot README, \"How to restore\").");
    }

    private static string SnapshotActionSets() =>
        Path.Combine(RepoPaths.RepoPath("docs", "reference", "lotrlome-armory-snapshot"), "action_sets.xml");

    // Every as_dwarf_warrior declaration in the file, merged the way the engine merges same-id sets.
    private static List<(string Type, string Clip)> DwarfRows(string actionSetsPath)
    {
        var sets = XDocument.Load(actionSetsPath).Root!.Elements("action_set")
            .Where(s => (string?)s.Attribute("id") == DwarfSet).ToList();
        Assert.IsTrue(sets.Count > 0, $"{DwarfSet} is not declared in {actionSetsPath}");
        return sets.SelectMany(Rows).ToList();
    }

    // Each expected type is bound once to its clip. A conflicting second row would replace it in native, so it fails here.
    private static void AssertEachBoundToItsClip(IReadOnlyCollection<(string Type, string Clip)> expected,
        List<(string Type, string Clip)> actual, string what, string hint)
    {
        Assert.IsTrue(expected.Count > 0, $"{what}: no expected rows to compare");
        var bad = new List<string>();
        foreach (var (type, clip) in expected)
        {
            var clips = actual.Where(r => r.Type == type).Select(r => r.Clip).ToList();
            if (clips.Count != 1 || clips[0] != clip)
                bad.Add($"{type}: wants {clip}, has [{string.Join(", ", clips)}]");
        }
        Assert.AreEqual(0, bad.Count, $"{what}: {bad.Count} of {expected.Count} rows missing or conflicting (#1226). {hint} " +
            string.Join("; ", bad.Take(8)));
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
