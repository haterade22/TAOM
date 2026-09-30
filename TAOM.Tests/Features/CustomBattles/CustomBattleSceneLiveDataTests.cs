using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Features.CustomBattles;

/// <summary>
/// Every scene the Custom Battle picker offers (<c>custom_battle_scenes.xml</c>) must exist in an installed module's
/// <c>SceneObj</c>, and a siege row's scene must carry a <c>siege</c> level, the level a Custom Battle siege loads. The
/// scenes live in the unversioned TAOM_Map, so a picker row can point at a scene only one machine has (the Edoras row
/// of 2026-09-29 names a scene created that day); the #603 check was a one-off. Inconclusive without the install.
/// </summary>
[TestClass]
[TestCategory("LiveInstall")]
public class CustomBattleSceneLiveDataTests
{
    private const string DefaultGameDir = @"E:\Steam\steamapps\common\Mount & Blade II Bannerlord";

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
    public void EveryPickerScene_ExistsAndEverySiegeRowHasASiegeLevel()
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
            if ((string?)row.Attribute("is_siege_map") == "true" && !File.ReadAllText(scene).Contains("<level name=\"siege\""))
                failures.Add($"{id}: a siege row whose scene has no siege level");
        }

        Assert.AreEqual(0, failures.Count, string.Join("\n", failures));
    }
}
