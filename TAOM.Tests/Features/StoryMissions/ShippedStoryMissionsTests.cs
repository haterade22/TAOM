using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.LongDefeat.Domain;
using TAOM.Features.StoryMissions.Config;
using TAOM.Features.StoryMissions.Domain;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.StoryMissions;

/// <summary>
/// The REAL <c>story_missions/story_missions.json</c>, read the way the game reads it. A story mission runs in a campaign, so every troop it
/// names must come from an XML file the campaign loads: <c>fords_troops</c> and <c>long_defeat_items</c> load under Custom Game only (SubModule.xml).
/// </summary>
[TestClass]
public class ShippedStoryMissionsTests
{
    private const string MissionId = "helms_deep_defend";

    // Every row the file ships, in file order. Each row other than Helm's Deep is a stand-in until its scene gets a recorded muster and its own track.
    private static readonly string[] ShippedIds =
    {
        MissionId, "minas_tirith_defend", "osgiliath_defend", "pelargir_defend", "dale_defend", "lorien_defend", "north_defend",
    };

    private static string ModuleData => RepoPaths.RepoPath("Main", "_Module", "ModuleData");

    private static (StoryMissionCatalog Catalog, IModLogger Logger) Load()
    {
        var logger = Substitute.For<IModLogger>();
        var paths = Substitute.For<IPathService>();
        paths.ModuleDataPath.Returns(ModuleData);
        return (new StoryMissionCatalog(paths, logger), logger);
    }

    private static StoryMissionDefinition Helms()
    {
        var (catalog, _) = Load();
        Assert.IsTrue(catalog.TryGet(MissionId, out var mission), MissionId + " is not in story_missions.json");
        return mission;
    }

    // The NPCCharacter rows of every XML file SubModule.xml loads for a Campaign: a node with no IncludedGameTypes loads under every game type.
    private static Dictionary<string, XElement> CampaignTroops()
    {
        var sub = XDocument.Load(RepoPaths.RepoPath("Main", "_Module", "SubModule.xml"));
        var troops = new Dictionary<string, XElement>(StringComparer.Ordinal);
        foreach (var node in sub.Descendants("XmlNode").Where(n => (string?)n.Element("XmlName")?.Attribute("id") == "NPCCharacters"))
        {
            var types = node.Descendants("GameType").Select(g => (string?)g.Attribute("value")).ToList();
            if (types.Count > 0 && !types.Contains("Campaign"))
                continue;

            var file = Path.Combine(ModuleData, ((string)node.Element("XmlName")!.Attribute("path")!).Replace('/', Path.DirectorySeparatorChar) + ".xml");
            if (!File.Exists(file))
                continue;

            foreach (var npc in XDocument.Load(file).Descendants("NPCCharacter"))
                if (npc.Attribute("id") != null && npc.Attribute("default_group") != null)
                    troops[(string)npc.Attribute("id")!] = npc;
        }

        return troops;
    }

    private static IEnumerable<(string Troop, FormationSlot Slot, string Culture, string Where)> Rows(StoryMissionDefinition m)
    {
        foreach (var g in m.Garrison)
            yield return (g.TroopId, g.Slot, m.DefenderCulture, "garrison");
        for (var w = 0; w < m.Waves.Authored.Count; w++)
            foreach (var t in m.Waves.Authored[w].Mix)
                yield return (t.TroopId, t.Slot, m.AttackerCulture, "wave " + (w + 1));
    }

    private static IReadOnlyList<StoryMissionDefinition> All() => Load().Catalog.Missions;

    [TestMethod]
    public void TheShippedFile_LoadsEveryRowWithNoWarning_AsSiegeDefences()
    {
        var (catalog, logger) = Load();

        CollectionAssert.AreEqual(ShippedIds, catalog.Missions.Select(m => m.Id).ToArray());
        foreach (var m in catalog.Missions)
            Assert.AreEqual(StorySide.Defending, m.Side, m.Id + ": the host plays a siege defence only");
        logger.DidNotReceiveWithAnyArgs().LogWarning(default!);
    }

    [TestMethod]
    public void TheShippedFile_GivesEveryRowItsOwnScene()
    {
        var scenes = All().Select(m => m.Scene).ToList();

        Assert.AreEqual(scenes.Count, scenes.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [TestMethod]
    public void TheShippedFile_LoadsTheHelmsDeepDefenceWithNoWarning()
    {
        var (catalog, logger) = Load();

        Assert.IsTrue(catalog.TryGet(MissionId, out var m));
        Assert.AreEqual("taom_rohan_castle_helms_deep_forceatmo", m.Scene);
        Assert.AreEqual(StorySide.Defending, m.Side);
        Assert.AreEqual(1, m.Waves.Authored.Count, "slice 1 proves the host with one wave");
        Assert.IsNotNull(m.Muster?.Foot, "new foot must not fall back to the terrace");
        Assert.IsNotNull(m.Muster?.Archers, "new archers must not fall back to the terrace");
        logger.DidNotReceiveWithAnyArgs().LogWarning(default!);
    }

    [TestMethod]
    public void TheShippedFile_NamesNoCustomGameContent()
    {
        var text = File.ReadAllText(Path.Combine(ModuleData, "story_missions", "story_missions.json"));

        Assert.IsFalse(text.Contains("\"fow_"), "fords_troops loads under Custom Game only");
        Assert.IsFalse(text.Contains("\"ld_"), "long_defeat_items loads under Custom Game only");
    }

    [TestMethod]
    public void EveryTroopTheShippedMissionsField_IsALoadedCampaignTroop()
    {
        var troops = CampaignTroops();

        foreach (var m in All())
        {
            var ids = RunPlanTroops.AllTroopIds(StoryPlanBuilder.Build(m, RunOptions.Default));
            Assert.IsTrue(ids.Count > 0, m.Id);
            CollectionAssert.AreEqual(Array.Empty<string>(), ids.Where(id => !troops.ContainsKey(id)).ToArray(), m.Id + ": named but not loaded in a campaign");
        }
    }

    [TestMethod]
    public void EveryTroop_FillsTheFormationItsRowSays_AndBelongsToTheSideThatFieldsIt()
    {
        var troops = CampaignTroops();

        foreach (var m in All())
        {
            foreach (var (troop, slot, culture, where) in Rows(m))
            {
                var npc = troops[troop];
                Assert.AreEqual(slot == FormationSlot.Ranged ? "Ranged" : "Infantry", (string?)npc.Attribute("default_group"), $"{m.Id}: {troop} in the {where}");
                Assert.AreEqual("Culture." + culture, (string?)npc.Attribute("culture"), $"{m.Id}: {troop} in the {where}");
            }
        }
    }

    [TestMethod]
    public void EveryTaomCultureARowNames_IsLoadedInACampaign()
    {
        // The launcher looks both cultures up in the running campaign, so a TAOM culture must load there; vanilla ids are read in the LiveInstall test.
        var sub = XDocument.Load(RepoPaths.RepoPath("Main", "_Module", "SubModule.xml"));
        var node = sub.Descendants("XmlNode").Single(n => (string?)n.Element("XmlName")?.Attribute("path") == "taom_spcultures");
        var taom = XDocument.Load(Path.Combine(ModuleData, "taom_spcultures.xml")).Descendants("Culture").Select(c => (string?)c.Attribute("id")).ToList();

        CollectionAssert.Contains(node.Descendants("GameType").Select(g => (string?)g.Attribute("value")).ToList(), "Campaign");
        CollectionAssert.Contains(taom, Helms().AttackerCulture);
        foreach (var m in All())
            foreach (var culture in new[] { m.DefenderCulture, m.AttackerCulture })
                Assert.IsTrue(taom.Contains(culture) || VanillaCultureIds.Contains(culture), $"{m.Id}: culture '{culture}' is neither a TAOM nor a vanilla culture");
    }

    // The main SandBox culture ids, which TAOM reuses for its own peoples (vlandia is Rohan, sturgia Dale, khuzait Rhun).
    private static readonly HashSet<string> VanillaCultureIds = new(StringComparer.Ordinal)
    {
        "empire", "aserai", "sturgia", "vlandia", "battania", "khuzait", "nord",
    };

    [TestMethod]
    [TestCategory("LiveInstall")]
    public void EveryVanillaCultureARowNames_IsInTheInstalledSandBox()
    {
        var env = Environment.GetEnvironmentVariable("BANNERLORD_GAME_DIR");
        var game = string.IsNullOrWhiteSpace(env) ? @"E:\Steam\steamapps\common\Mount & Blade II Bannerlord" : env;
        var file = Path.Combine(game, "Modules", "SandBox", "ModuleData", "spcultures.xml");
        if (!File.Exists(file))
            Assert.Inconclusive("the SandBox cultures are not installed here.");

        var cultures = XDocument.Load(file).Descendants("Culture").Select(c => (string?)c.Attribute("id")).ToList();

        CollectionAssert.Contains(cultures, Helms().DefenderCulture);
        foreach (var m in All())
            foreach (var culture in new[] { m.DefenderCulture, m.AttackerCulture }.Where(VanillaCultureIds.Contains))
                CollectionAssert.Contains(cultures, culture, m.Id);
    }

    [TestMethod]
    public void EveryShippedGarrison_IsAsLargeAsTheLongDefeatsAndEveryWaveAboveTheLiveFloor()
    {
        foreach (var m in All())
        {
            Assert.AreEqual(150, m.Garrison.Sum(g => g.Count), m.Id);
            Assert.IsTrue(m.Garrison.Any(g => g.Slot == FormationSlot.Ranged), m.Id + ": the archer muster point needs archers");
            foreach (var wave in m.Waves.Authored)
                Assert.IsTrue(wave.Count > 100, m.Id + ": at 100 or fewer live attackers the siege AI stops its machines");
        }
    }

    [TestMethod]
    public void EveryShippedRow_KeepsAStragglerGroupFromTakingTheKeep()
    {
        foreach (var m in All())
        {
            var floor = (int)(m.Rules.StragglerShare * m.Waves.Authored.Min(w => w.Count)) + 1;
            Assert.IsTrue(m.Rules.KeepMinAttackers >= floor, m.Id);
        }
    }

    [TestMethod]
    public void OnlyHelmsDeep_NamesATrack()
    {
        // Each other row is a stand-in: with no track, the campaign's music plays and nothing is silenced.
        CollectionAssert.AreEqual(new[] { MissionId }, All().Where(m => m.Music != null).Select(m => m.Id).ToArray());
    }

    [TestMethod]
    public void TheShippedMusic_IsAStereoOggVorbisFile_InItsOwnModuleSoundsFolder()
    {
        // The engine opens the file only inside its FMOD callback, after Play has answered, so a file that does not decode would silence the mission
        // with a passing log line. A bad encode fails here instead. StoryMusic/ keeps the track out of the LOTR/OST folder's provenance row.
        var music = Helms().Music;
        Assert.IsNotNull(music, MissionId + " names no music");
        StringAssert.StartsWith(music, "StoryMusic/");
        var path = RepoPaths.RepoPath("Main", "_Module", "ModuleSounds", music!.Replace('/', Path.DirectorySeparatorChar));
        Assert.IsTrue(File.Exists(path), path + " is missing");

        var head = new byte[64];
        using (var file = File.OpenRead(path))
            Assert.AreEqual(head.Length, file.Read(head, 0, head.Length));
        Assert.AreEqual("OggS", System.Text.Encoding.ASCII.GetString(head, 0, 4), "not an Ogg container");
        var packet = 27 + head[26];
        Assert.AreEqual(1, head[packet], "the first packet is not a Vorbis identification header");
        Assert.AreEqual("vorbis", System.Text.Encoding.ASCII.GetString(head, packet + 1, 6));
        Assert.AreEqual(2, head[packet + 11], "channels");
        Assert.AreEqual(48000, BitConverter.ToInt32(head, packet + 12), "sample rate");
    }

    [TestMethod]
    public void TheShippedFile_IsInsideTheModuleFolderTheBuildCopiesWhole()
    {
        // CopyModule deploys Main/_Module verbatim and the csproj lists no ModuleData file by name, so the folder is the shipping rule.
        var path = Path.Combine(ModuleData, "story_missions", "story_missions.json");

        Assert.IsTrue(File.Exists(path));
        var csproj = File.ReadAllText(RepoPaths.RepoPath("Main", "TAOM.csproj"));
        Assert.IsFalse(csproj.Contains("long_defeat\\"), "Long Defeat's json ships by folder, with no csproj line");
        Assert.IsFalse(csproj.Contains("story_missions"), "so the story json needs no line either");
    }
}
