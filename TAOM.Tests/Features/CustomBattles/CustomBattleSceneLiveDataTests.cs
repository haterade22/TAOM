using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Features.CustomBattles;

/// <summary>
/// Every scene TAOM adds to the Custom Battle picker (<c>custom_battle_scenes.xml</c>) must exist in an installed module's
/// <c>SceneObj</c>, and a siege row's scene must declare the levels a Custom Battle siege loads. The scenes live in the
/// unversioned TAOM_Map, so a picker row can point at a scene only one machine has (the Edoras row of 2026-09-29 names a
/// scene created that day); the #603 check was a one-off. The live check is inconclusive without the install.
/// </summary>
[TestClass]
public class CustomBattleSceneLiveDataTests
{
    private const string DefaultGameDir = @"E:\Steam\steamapps\common\Mount & Blade II Bannerlord";

    // What a Custom Battle siege asks its scene for (v1.5.3): BannerlordMissions.OpenSiegeMissionWithDeployment loads
    // "level_N siege", N being the picker's scene level, and CustomBattleData.SceneLevels offers 1, 2 and 3.
    private static readonly string[] SiegeLevels = { "siege", "level_1", "level_2", "level_3" };

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "TAOM.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new FileNotFoundException("TAOM.sln not found walking upward from cwd");
    }

    private static string ModulesRoot()
    {
        string? env = Environment.GetEnvironmentVariable("BANNERLORD_GAME_DIR");
        string modules = Path.Combine(string.IsNullOrWhiteSpace(env) ? DefaultGameDir : env, "Modules");
        if (!Directory.Exists(Path.Combine(modules, "TAOM_Map")))
            Assert.Inconclusive("TAOM_Map not installed on this machine; the picker's scenes cannot be checked here.");
        return modules;
    }

    [TestMethod]
    [TestCategory("LiveInstall")]
    public void EveryPickerScene_ExistsAndEverySiegeRowDeclaresTheSiegeLevels()
    {
        string modules = ModulesRoot();
        var rows = XDocument.Load(Path.Combine(RepoRoot(), "Main", "_Module", "ModuleData", "custom_battle_scenes.xml"))
            .Descendants("Scene").ToList();
        Assert.IsTrue(rows.Count > 0, "custom_battle_scenes.xml lists no Scene; the parse or the layout moved");

        var failures = new List<string>();
        foreach (XElement row in rows)
        {
            string id = (string?)row.Attribute("id") ?? "";
            string? scene = Directory.EnumerateDirectories(modules)
                .Select(m => Path.Combine(m, "SceneObj", id, "scene.xscene"))
                .FirstOrDefault(File.Exists);
            if (scene == null)
            {
                failures.Add($"{id}: no SceneObj/{id}/scene.xscene in any installed module");
                continue;
            }
            // CustomGame.LoadCustomBattleScenes reads the flag with bool.TryParse.
            if (!bool.TryParse((string?)row.Attribute("is_siege_map"), out bool isSiege) || !isSiege)
                continue;
            HashSet<string> declared;
            try
            {
                using TextReader text = File.OpenText(scene);
                declared = DeclaredLevels(text);
            }
            catch (XmlException e)
            {
                failures.Add($"{id}: its level declarations do not parse ({e.Message})");
                continue;
            }
            string[] missing = SiegeLevels.Except(declared).ToArray();
            if (missing.Length > 0)
                failures.Add($"{id}: a siege row whose scene declares no {string.Join(", ", missing)} level");
        }

        Assert.AreEqual(0, failures.Count, string.Join("\n", failures));
    }

    [TestMethod]
    public void DeclaredLevels_CommentedOutReorderedAndEntityLevels_ReadsOnlyTheSceneDeclarations()
    {
        const string scene =
            "<?xml version=\"1.0\"?>\n<scene name=\"s\">\n\t<levels>\n\t\t<!-- <level name=\"level_3\" mask=\"8\"/> -->\n" +
            "\t\t<level mask='16' name='siege'/>\n\t\t<level name=\"level_1\" mask=\"2\"/>\n\t</levels>\n" +
            "\t<entities>\n\t\t<game_entity name=\"e\">\n\t\t\t<levels>\n\t\t\t\t<level name=\"level_2\"/>\n\t\t\t</levels>\n" +
            "\t\t</game_entity>\n\t</entities>\n</scene>\n";

        HashSet<string> levels = DeclaredLevels(new StringReader(scene));

        CollectionAssert.AreEquivalent(new[] { "siege", "level_1" }, levels.ToList());
    }

    [TestMethod]
    public void DeclaredLevels_NoSceneLevelsBlock_IgnoresTheEntitiesLevelLists()
    {
        const string scene =
            "<?xml version=\"1.0\"?>\n<scene name=\"s\">\n\t<entities>\n\t\t<game_entity name=\"e\">\n\t\t\t<levels>\n" +
            "\t\t\t\t<level name=\"level_1\"/>\n\t\t\t\t<level name=\"level_2\"/>\n\t\t\t\t<level name=\"level_3\"/>\n" +
            "\t\t\t\t<level name=\"siege\"/>\n\t\t\t</levels>\n\t\t</game_entity>\n\t</entities>\n</scene>\n";

        HashSet<string> levels = DeclaredLevels(new StringReader(scene));

        Assert.AreEqual(0, levels.Count, "an entity's level list is an assignment, not a scene declaration");
    }

    /// <summary>
    /// The level names a scene declares, <c>scene/levels/level/@name</c>. Parsed rather than text-matched, so a
    /// commented-out declaration does not count and attribute order and quoting do not matter
    /// (<c>docs/reviews/rca-keyforce-art-wiring-2026-09-29.md</c>, Codex pass C2). An entity's own <c>levels</c> list
    /// sits deeper and is not a declaration. Stops after the block, since a scene can run past 16 MB.
    /// </summary>
    private static HashSet<string> DeclaredLevels(TextReader scene)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        using XmlReader reader = XmlReader.Create(scene);
        while (reader.ReadToFollowing("levels"))
        {
            if (reader.Depth != 1)
                continue;
            using XmlReader block = reader.ReadSubtree();
            while (block.ReadToFollowing("level"))
                names.Add(block.GetAttribute("name") ?? "");
            break;
        }
        return names;
    }
}
