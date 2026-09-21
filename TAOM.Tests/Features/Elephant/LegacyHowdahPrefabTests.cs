using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Features.Elephant;

/// <summary>
/// Six orphaned ADOD_Beasts howdah prefabs were deleted from the live Armory on 2026-09-21
/// (<c>adod_howdah_1_agent</c>, <c>_2_agent</c>, <c>_4_agent</c>, <c>_4_agents</c>, <c>howdah_object</c>,
/// <c>howdah_test</c>; originals under <c>E:\taom-live-backups\2026-09-21\</c>). Nothing referenced them: no
/// TAOM C#, no ModuleData, no scene in any installed module. They loaded anyway, because the Armory loads
/// every file in <c>Prefabs/</c>, costing 45 entities against a queue CLAUDE.md records as having about 921
/// spare, and they logged "Could not find object class(ADODHowdahStandingPoint)" in the Modding Kit for
/// classes no shipped assembly defines.
///
/// This test exists because the Armory is UNVERSIONED. The deletion is invisible to git and a module
/// reinstall silently restores all six, which is the failure mode CLAUDE.md names as "a fix in a dependency
/// module": always land an in-repo gate beside the external edit.
///
/// It deliberately does NOT touch <c>adod_wolf_target.xml</c>, which is out of scope and unexamined.
/// </summary>
[TestClass]
public class LegacyHowdahPrefabTests
{
    private const string DefaultGameDir = @"E:\Steam\steamapps\common\Mount & Blade II Bannerlord";

    private static readonly string[] Removed =
    {
        "adod_howdah_1_agent.xml",
        "adod_howdah_2_agent.xml",
        "adod_howdah_4_agent.xml",
        "adod_howdah_4_agents.xml",
        "howdah_object.xml",
        "howdah_test.xml",
    };

    private static string PrefabDir()
    {
        string? env = Environment.GetEnvironmentVariable("BANNERLORD_GAME_DIR");
        return Path.Combine(string.IsNullOrWhiteSpace(env) ? DefaultGameDir : env,
            "Modules", "LOTRLOME_Armory", "Prefabs");
    }

    [TestMethod]
    public void TheOrphanedAdodHowdahPrefabs_HaveNotComeBack()
    {
        string dir = PrefabDir();
        if (!Directory.Exists(dir))
            Assert.Inconclusive("LOTRLOME_Armory not installed on this machine; the live copy cannot be checked here.");

        string[] back = Removed.Where(f => File.Exists(Path.Combine(dir, f))).ToArray();
        Assert.AreEqual(0, back.Length,
            "An Armory reinstall has restored prefabs deleted on 2026-09-21. They reference script classes no " +
            "shipped assembly defines, and they spend entities from a global queue with little headroom. " +
            "Delete them again: " + string.Join(", ", back));
    }

    [TestMethod]
    public void NoArmoryPrefab_DeclaresAScriptClassNothingShips()
    {
        string dir = PrefabDir();
        if (!Directory.Exists(dir))
            Assert.Inconclusive("LOTRLOME_Armory not installed on this machine; the live copy cannot be checked here.");

        // Catches a restore under any filename, which the name list above cannot. ADOD's beasts port was
        // absorbed into TAOM's own Elephant feature; its script classes are not in any assembly we load, so an
        // entity naming one is dead weight that the Kit reports and the game silently ignores.
        string[] offenders = Directory.GetFiles(dir, "*.xml")
            .Where(f => File.ReadAllText(f).IndexOf("ADODHowdah", StringComparison.Ordinal) >= 0)
            .Select(Path.GetFileName)
            .ToArray()!;

        Assert.AreEqual(0, offenders.Length,
            "These Armory prefabs declare an ADODHowdah* script class that no shipped assembly defines: " +
            string.Join(", ", offenders));
    }
}
