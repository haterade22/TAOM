using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Client.Features.CustomBattles.Muster;

namespace TAOM.Tests.Features.CustomBattles;

/// <summary>
/// The muster captain's troop-tree read (#1297). It reads only the files a module registers as NPCCharacters, one
/// stream per file, instead of a DOM load of every ModuleData XML of every module. The last module to define a troop
/// owns it, as in the game; upgrades from every definition are joined, as the engine merges them.
/// </summary>
[TestClass]
public class CaptainResolverReadTests
{
    private readonly Dictionary<string, string> _moduleOf = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<string>> _upgrades = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _inTree = new(StringComparer.OrdinalIgnoreCase);

    private void Read(string module, string xml) =>
        CaptainResolver.ReadCharacters(new StringReader(xml), module, _moduleOf, _upgrades, _inTree);

    [TestMethod]
    public void ReadCharacters_TakesEachTroopsModuleAndUpgrades()
    {
        Read("TAOM", @"<NPCCharacters>
  <NPCCharacter id=""gondor_recruit""><upgrade_targets><upgrade_target id=""NPCCharacter.gondor_soldier"" />
    <upgrade_target id=""NPCCharacter.gondor_archer"" /></upgrade_targets></NPCCharacter>
  <NPCCharacter id=""gondor_soldier""><equipmentSet /></NPCCharacter>
</NPCCharacters>");

        Assert.AreEqual("TAOM", _moduleOf["gondor_recruit"]);
        Assert.AreEqual("TAOM", _moduleOf["gondor_soldier"]);
        CollectionAssert.AreEqual(new[] { "gondor_soldier", "gondor_archer" }, _upgrades["gondor_recruit"]);
        Assert.IsFalse(_upgrades.ContainsKey("gondor_soldier"));
        CollectionAssert.AreEquivalent(new[] { "gondor_recruit", "gondor_soldier", "gondor_archer" }, new List<string>(_inTree));
    }

    [TestMethod]
    public void ReadCharacters_ALaterModuleOwnsTheTroop_AndKeepsTheEarlierUpgradesWhenItHasNone()
    {
        Read("Native", @"<NPCCharacters><NPCCharacter id=""a""><upgrade_targets><upgrade_target id=""NPCCharacter.b"" /></upgrade_targets></NPCCharacter></NPCCharacters>");
        Read("TAOM", @"<NPCCharacters><NPCCharacter id=""a"" /></NPCCharacters>");

        Assert.AreEqual("TAOM", _moduleOf["a"]);
        CollectionAssert.AreEqual(new[] { "b" }, _upgrades["a"]);
    }

    [TestMethod]
    public void ReadCharacters_ALaterModulesUpgradesJoinTheEarlierOnes_AsTheEngineMerges()
    {
        // upgrade_targets is AlwaysPreferMerge and upgrade_target unique on @id (NPCCharacters.xsd), so
        // MBObjectManager.MergeElements unions the two definitions' targets.
        Read("Native", @"<NPCCharacters><NPCCharacter id=""a""><upgrade_targets><upgrade_target id=""NPCCharacter.b"" /></upgrade_targets></NPCCharacter></NPCCharacters>");
        Read("TAOM", @"<NPCCharacters><NPCCharacter id=""a""><upgrade_targets><upgrade_target id=""NPCCharacter.c"" /><upgrade_target id=""NPCCharacter.b"" /></upgrade_targets></NPCCharacter></NPCCharacters>");

        CollectionAssert.AreEqual(new[] { "b", "c" }, _upgrades["a"]);
    }
}
