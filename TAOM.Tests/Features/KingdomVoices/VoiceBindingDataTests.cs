using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Infrastructure;
using static TAOM.Tests.Infrastructure.RepoPaths;

namespace TAOM.Tests.Features.KingdomVoices;

/// <summary>
/// A combat voice definition reaches an agent only through the <c>voice_types</c> pool of its race's adult skin in the
/// Armory's <c>skins.xml</c> (docs/features/kingdom-voices.md, route A). That file is unversioned: a reinstall puts the
/// vanilla voices back with no error, and a definition bound to no skin is silent. The skins are parsed, never
/// regex-matched: the file writes attributes on their own lines and some races write <c>mesh_maturity_type ="adult"</c>.
/// A skin's pools are kept as the blocks the file holds, because which of two blocks the engine reads is not established.
/// </summary>
[TestClass]
public class VoiceBindingDataTests
{
    private const string HumanClass = "human";

    // The adult female dwarf skin holds two voice_types blocks (docs/features/kingdom-voices.md, "Known defects"). Pinned with
    // its block boundaries: any other arrangement, the two merged into one pool included, fails, and so does a fixed skin.
    private const string KnownTwoPoolDwarfWoman = "dwarf/1: [dwarf_01] [female_01, female_02, female_03, female_04, female_05]";

    /// <summary>Adult skin key (<c>race/gender</c>) to its voice_types blocks, in file order.</summary>
    internal static Dictionary<string, string[][]> AdultPools(XDocument skins)
    {
        var pools = new Dictionary<string, string[][]>(StringComparer.Ordinal);
        foreach (var race in skins.Root!.Elements("race"))
        foreach (var skin in race.Elements("skin").Where(s => (string?)s.Attribute("mesh_maturity_type") == "adult"))
        {
            var key = (string?)race.Attribute("id") + "/" + (string?)skin.Attribute("gender");
            Assert.IsFalse(pools.ContainsKey(key), $"two adult skins share {key}; the pool the engine reads is not decided here");
            pools[key] = skin.Elements("voice_types")
                .Select(block => block.Elements("voice_type").Select(v => (string?)v.Attribute("name") ?? "").ToArray())
                .ToArray();
        }

        return pools;
    }

    internal static string Render(string key, string[][] blocks) =>
        key + ": " + (blocks.Length == 0 ? "no pool" : string.Join(" ", blocks.Select(b => "[" + string.Join(", ", b) + "]")));

    internal static List<string> Unbound(IEnumerable<string> definitions, Dictionary<string, string[][]> pools) =>
        definitions.Where(d => !pools.Values.Any(blocks => blocks.Any(b => b.Contains(d)))).ToList();

    /// <summary>Skins with more than one voice_types block: no reader of this repo can say which one applies.</summary>
    internal static List<string> ManyPools(Dictionary<string, string[][]> pools) =>
        pools.Where(p => p.Value.Length > 1).Select(p => Render(p.Key, p.Value)).ToList();

    /// <summary>Single-pool skins whose pool holds a TAOM voice beside another. A skin with several pools is <see cref="ManyPools"/>'s.</summary>
    internal static List<string> Mixed(ICollection<string> definitions, Dictionary<string, string[][]> pools) =>
        pools.Where(p => p.Value.Length == 1 && p.Value[0].Any(definitions.Contains) && p.Value[0].Any(v => !definitions.Contains(v)))
            .Select(p => Render(p.Key, p.Value)).ToList();

    /// <summary>Every skin that one side lacks or holds differently, read both ways.</summary>
    internal static List<string> Differences(Dictionary<string, string[][]> snapshot, Dictionary<string, string[][]> live) =>
        snapshot.Keys.Union(live.Keys).OrderBy(k => k, StringComparer.Ordinal)
            .Select(k => (Key: k, Snapshot: snapshot.TryGetValue(k, out var s) ? Render(k, s) : k + ": no such skin",
                Live: live.TryGetValue(k, out var l) ? Render(k, l) : k + ": no such skin"))
            .Where(d => d.Snapshot != d.Live)
            .Select(d => $"snapshot {d.Snapshot}, live {d.Live}").ToList();

    private static List<string> RegisteredHumanDefinitions()
    {
        var module = RepoPath("Main", "_Module");
        var files = XDocument.Load(Path.Combine(module, "ModuleData", "project.mbproj")).Descendants("file")
            .Where(f => (string?)f.Attribute("type") == "voice_definitions")
            .Select(f => Path.Combine(module, ((string?)f.Attribute("name") ?? "").Replace('/', Path.DirectorySeparatorChar)))
            .ToList();
        var names = files.SelectMany(f => XDocument.Load(f).Descendants("voice_definition"))
            .Where(d => (string?)d.Attribute("sound_and_collision_info_class") == HumanClass)
            .Select(d => (string?)d.Attribute("name") ?? "").ToList();
        Assert.IsTrue(names.Count >= 4, $"only {names.Count} human voice definitions found in {files.Count} registered files; the scan is broken");
        return names;
    }

    private static Dictionary<string, string[][]> SnapshotPools()
    {
        var pools = AdultPools(XDocument.Load(RepoPath("docs", "reference", "lotrlome-armory-snapshot", "skins.xml")));
        Assert.IsTrue(pools.Count >= 20, $"only {pools.Count} adult skins found in the snapshot; the scan is broken");
        return pools;
    }

    [TestMethod]
    public void Snapshot_EveryRegisteredHumanVoiceDefinition_IsInAnAdultSkinPool()
    {
        var unbound = Unbound(RegisteredHumanDefinitions(), SnapshotPools());

        Assert.AreEqual(0, unbound.Count,
            "Voice definitions no adult skin names, so no agent uses them: " + string.Join(", ", unbound));
    }

    [TestMethod]
    public void Snapshot_NoAdultSkinPool_MixesATaomVoiceWithAnother()
    {
        var mixed = Mixed(RegisteredHumanDefinitions(), SnapshotPools());

        Assert.AreEqual(0, mixed.Count,
            "Adult skins whose pool holds a TAOM voice beside another, so only part of that race uses it: " + string.Join("; ", mixed));
    }

    [TestMethod]
    public void Snapshot_OnlyTheKnownDwarfWoman_HasMoreThanOneVoicePool()
    {
        var many = ManyPools(SnapshotPools());

        CollectionAssert.AreEqual(new[] { KnownTwoPoolDwarfWoman }, many,
            "An adult skin with several voice_types blocks is a defect: which block the engine reads is not established. The one "
            + "known case is pinned whole; when it is fixed, or changes, update the pin and kingdom-voices.md \"Known defects\". Found: "
            + string.Join("; ", many));
    }

    [TestMethod]
    public void Snapshot_ElfAdultMale_IsElf01Alone()
    {
        Assert.AreEqual("elf/0: [elf_01]", Render("elf/0", SnapshotPools()["elf/0"]));
    }

    [TestMethod]
    [TestCategory("LiveInstall")]
    public void LiveArmory_AdultSkinPools_EqualTheSnapshot()
    {
        var live = Path.Combine(PrefabExtensionBrushTests.GameDir(), "Modules", "LOTRLOME_Armory", "ModuleData", "skins.xml");
        if (!File.Exists(live))
            Assert.Inconclusive("LOTRLOME_Armory is not installed here; its skins.xml cannot be checked.");

        var differing = Differences(SnapshotPools(), AdultPools(XDocument.Load(live)));

        Assert.AreEqual(0, differing.Count,
            "The live Armory's adult voice pools differ from the snapshot. A reinstall reverts them, and a binding is applied only "
            + "once the deployed TAOM defines the voice (docs/reference/lotrlome-armory-snapshot/README.md, the elf combat voice): "
            + "deploy this build, then python tools/oneoff/bind_elf_voice.py --apply. " + string.Join("; ", differing));
    }

    private static XDocument Skins(string races) => XDocument.Parse("<skins>" + races + "</skins>");

    private static string Pool(params string[] voices) =>
        "<voice_types>" + string.Concat(voices.Select(v => $"<voice_type\n name=\"{v}\" />")) + "</voice_types>";

    private static string Skin(string maturity, string gender, params string[] pools) =>
        $"<skin gender=\"{gender}\" mesh_maturity_type =\"{maturity}\">" + string.Concat(pools) + "</skin>";

    private static Dictionary<string, string[][]> Read(string races) => AdultPools(Skins(races));

    [TestMethod]
    public void AdultPools_AdultSkin_ReadsThePoolInOrder()
    {
        var pools = Read("<race\n id=\"elf\">" + Skin("adult", "0", Pool("male_02", "elf_01")) + "</race>");

        Assert.AreEqual("elf/0: [male_02, elf_01]", Render("elf/0", pools["elf/0"]));
    }

    [TestMethod]
    public void AdultPools_TeenagerSkin_IsNotRead()
    {
        Assert.AreEqual(0, Read("<race id=\"elf\">" + Skin("teenager", "0", Pool("elf_01")) + "</race>").Count);
    }

    [TestMethod]
    public void AdultPools_VoiceInsideAComment_IsNotRead()
    {
        var pools = Read("<race id=\"elf\"><skin gender=\"0\" mesh_maturity_type=\"adult\"><voice_types>"
            + "<!-- <voice_type name=\"elf_01\" /> --><voice_type name=\"male_02\" /></voice_types></skin></race>");

        Assert.AreEqual("elf/0: [male_02]", Render("elf/0", pools["elf/0"]));
    }

    [TestMethod]
    public void AdultPools_TwoBlocksInOneSkin_AreKeptApart()
    {
        var pools = Read("<race id=\"dwarf\">" + Skin("adult", "1", Pool("dwarf_01"), Pool("female_01")) + "</race>");

        Assert.AreEqual("dwarf/1: [dwarf_01] [female_01]", Render("dwarf/1", pools["dwarf/1"]));
    }

    [TestMethod]
    public void Unbound_DefinitionOnlyOnATeenagerSkinOrMisspelled_IsReported()
    {
        var pools = Read("<race id=\"elf\">" + Skin("adult", "0", Pool("elf_1")) + Skin("teenager", "0", Pool("elf_01")) + "</race>");

        CollectionAssert.AreEqual(new[] { "elf_01" }, Unbound(new[] { "elf_01" }, pools));
    }

    [TestMethod]
    public void Unbound_DefinitionInAnAdultPool_IsNotReported()
    {
        var pools = Read("<race id=\"elf\">" + Skin("adult", "0", Pool("elf_01")) + "</race>");

        Assert.AreEqual(0, Unbound(new[] { "elf_01" }, pools).Count);
    }

    [TestMethod]
    public void Mixed_TaomVoiceBesideAVanillaOne_IsReported()
    {
        var pools = Read("<race id=\"elf\">" + Skin("adult", "0", Pool("male_02", "elf_01")) + Skin("adult", "1", Pool("female_01")) + "</race>");

        CollectionAssert.AreEqual(new[] { "elf/0: [male_02, elf_01]" }, Mixed(new[] { "elf_01" }, pools));
    }

    [TestMethod]
    public void Mixed_TwoTaomVoicesInOnePool_IsNotReported()
    {
        var pools = Read("<race id=\"uruk\">" + Skin("adult", "0", Pool("uruk_01", "uruk_hai_01")) + "</race>");

        Assert.AreEqual(0, Mixed(new[] { "uruk_01", "uruk_hai_01" }, pools).Count);
    }

    [TestMethod]
    public void ManyPools_TheDwarfWomansBlocksMergedIntoOne_NoLongerMatchThePin_AndReadAsMixed()
    {
        var merged = Read("<race id=\"dwarf\">"
            + Skin("adult", "1", Pool("dwarf_01", "female_01", "female_02", "female_03", "female_04", "female_05")) + "</race>");

        Assert.AreEqual(0, ManyPools(merged).Count, "one block: the pin of the two-block state no longer matches, so its test fails");
        Assert.AreEqual(1, Mixed(new[] { "dwarf_01" }, merged).Count);
    }

    [TestMethod]
    public void ManyPools_AnEmptyBlockBeforeTheRealOne_IsReported()
    {
        var pools = Read("<race id=\"elf\">" + Skin("adult", "0", Pool(), Pool("elf_01")) + "</race>");

        CollectionAssert.AreEqual(new[] { "elf/0: [] [elf_01]" }, ManyPools(pools));
    }

    [TestMethod]
    public void Differences_ASkinOnlyOneSideHolds_IsReportedBothWays()
    {
        var a = Read("<race id=\"elf\">" + Skin("adult", "0", Pool("elf_01")) + "</race>");
        var b = Read("<race id=\"elf\">" + Skin("adult", "0", Pool("elf_01")) + Skin("adult", "1", Pool("female_01")) + "</race>");

        CollectionAssert.AreEqual(new[] { "snapshot elf/1: no such skin, live elf/1: [female_01]" }, Differences(a, b));
        CollectionAssert.AreEqual(new[] { "snapshot elf/1: [female_01], live elf/1: no such skin" }, Differences(b, a));
    }

    [TestMethod]
    public void Differences_TheSameNamesInOtherBlocks_AreReported()
    {
        var two = Read("<race id=\"dwarf\">" + Skin("adult", "1", Pool("dwarf_01"), Pool("female_01")) + "</race>");
        var one = Read("<race id=\"dwarf\">" + Skin("adult", "1", Pool("dwarf_01", "female_01")) + "</race>");

        Assert.AreEqual(1, Differences(two, one).Count);
        Assert.AreEqual(0, Differences(two, two).Count);
    }
}
