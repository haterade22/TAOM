using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.Core;
using TAOM.Composition;
using TAOM.Features.CreatureSaddles;
using TAOM.Features.CreatureSaddles.Hooks;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.CreatureSaddles;

/// <summary>
/// Creature saddle families (Patch154). A horse saddle with reins on a dire wolf crashed a player's battle spawn
/// (2026-11-15); with a family of its own per rig, the engine's inventory refuses horse saddles on these mounts.
/// The table is pinned by literal, not by looping over the production list.
/// </summary>
[TestClass]
public class CreatureSaddleFamiliesTests
{
    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    [TestMethod]
    [DataRow("warg", 11)]
    [DataRow("fell_warg", 11)]
    [DataRow("dire_wolf", 12)]
    [DataRow("spider", 13)]
    public void EachCreatureRig_HasItsOwnFamily(string monster, int family)
        => Assert.AreEqual(family, CreatureSaddleFamilies.MonsterFamily(monster));

    [TestMethod]
    [DataRow("warg_saddle", 11)]
    [DataRow("dire_wolf_saddle", 12)]
    public void EachCreatureSaddle_TakesItsMountsFamily(string item, int family)
        => Assert.AreEqual(family, CreatureSaddleFamilies.HarnessFamily(item));

    [TestMethod]
    [DataRow("horse")]
    [DataRow("taom_war_ram")]
    [DataRow("taom_elk")]
    [DataRow("taom_war_elephant")]
    [DataRow("chariot")]
    [DataRow(null)]
    public void HorseRigsAndOtherMounts_KeepTheDatasFamily(string? monster)
        => Assert.IsNull(CreatureSaddleFamilies.MonsterFamily(monster));

    [TestMethod]
    [DataRow("saddle_of_aeneas")]
    [DataRow("light_harness")]
    [DataRow("taom_ram_barding_light_a")]
    [DataRow("taom_elk_saddle_a")]
    public void HorseSaddles_KeepTheDatasFamily(string item)
        => Assert.IsNull(CreatureSaddleFamilies.HarnessFamily(item));

    [TestMethod]
    public void TheTable_HoldsExactlyTheseRows()
    {
        CollectionAssert.AreEquivalent(new[] { "warg", "fell_warg", "dire_wolf", "spider" }, CreatureSaddleFamilies.Monsters.Keys.ToList());
        CollectionAssert.AreEquivalent(new[] { "warg_saddle", "dire_wolf_saddle" }, CreatureSaddleFamilies.Harnesses.Keys.ToList());
    }

    [TestMethod]
    public void NewFamilies_AvoidEveryFamilyTheDataAlreadyUses()
    {
        // 0 humans, 1 horse, 2 camel, 4 chariot, 10 elephant (installed data, 2026-11-15).
        var taken = new HashSet<int> { 0, 1, 2, 3, 4, 5, 6, 8, 10 };
        foreach (var f in CreatureSaddleFamilies.Monsters.Values)
            Assert.IsFalse(taken.Contains(f), $"family {f} is already used");
    }

    [TestMethod]
    [DataRow(11, 1, true)]
    [DataRow(12, 11, true)]
    [DataRow(1, 12, true)]
    [DataRow(12, 12, false)]
    [DataRow(1, 1, false)]
    public void MustUnequipHarness_OnlyWhenFamiliesDiffer(int mount, int harness, bool expected)
        => Assert.AreEqual(expected, CreatureSaddleFamilies.MustUnequipHarness(mount, harness));

    [TestCategory("RequiresGame")]   // constructs Monster
    [TestMethod]
    public void Writer_SetsTheFamily_OnARealMonsterAndARealSaddle()
    {
        if (!_gameLoaded) Assert.Inconclusive("Game assemblies unavailable.");
        Assert.IsNotNull(CreatureSaddleFamilyWriter.MonsterFamilySetter, "Monster.FamilyType setter");
        Assert.IsNotNull(CreatureSaddleFamilyWriter.ArmorFamilySetter, "ArmorComponent.FamilyType setter");

        var monster = new Monster { StringId = "dire_wolf" };
        CreatureSaddleFamilyWriter.MonsterFamilySetter!.Invoke(monster, new object[] { 1 });
        CreatureSaddleFamilyWriter.ApplyToMonster(monster);
        Assert.AreEqual(12, monster.FamilyType);

        var horse = new Monster { StringId = "horse" };
        CreatureSaddleFamilyWriter.MonsterFamilySetter.Invoke(horse, new object[] { 1 });
        CreatureSaddleFamilyWriter.ApplyToMonster(horse);
        Assert.AreEqual(1, horse.FamilyType, "a horse keeps its family");

        var saddle = new ItemObject("dire_wolf_saddle");
        var armor = new ArmorComponent(saddle);
        AccessTools.Property(typeof(ItemObject), nameof(ItemObject.ItemComponent)).SetValue(saddle, armor);
        CreatureSaddleFamilyWriter.ArmorFamilySetter!.Invoke(armor, new object[] { 1 });
        CreatureSaddleFamilyWriter.ApplyToItem(saddle);
        Assert.AreEqual(12, armor.FamilyType);
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void PatchCategory_AttachesBothPostfixes_ToTheInstalledEngine()
    {
        if (!_gameLoaded) Assert.Inconclusive("Game assemblies unavailable.");
        const string id = "taom.tests.creaturesaddles";
        var harmony = new Harmony(id);
        try
        {
            PatchCategoryIndex.Build(typeof(CreatureSaddlesModule).Assembly).Apply(harmony, CreatureSaddlesModule.PatchCategory);

            var monster = AccessTools.Method(typeof(Monster), nameof(Monster.Deserialize));
            var item = AccessTools.Method(typeof(ItemObject), nameof(ItemObject.Deserialize));
            Assert.IsTrue(Harmony.GetPatchInfo(monster)!.Postfixes.Any(p => p.owner == id && p.PatchMethod.DeclaringType == typeof(Patch154_MonsterFamily)));
            Assert.IsTrue(Harmony.GetPatchInfo(item)!.Postfixes.Any(p => p.owner == id && p.PatchMethod.DeclaringType == typeof(Patch154_HarnessFamily)));
        }
        finally
        {
            harmony.UnpatchAll(id);
        }
    }

    [TestMethod]
    public void TheModule_IsListedOnce_AppliesPatch154AtProcessLoad_AndAddsTheRepairBehavior()
    {
        Assert.AreEqual(1, FeatureModules.All.OfType<CreatureSaddlesModule>().Count());
        var module = new CreatureSaddlesModule();
        Assert.AreEqual("CreatureSaddles", module.Id);
        Assert.IsNull(module.ParkedReason);
        Assert.IsFalse(module.OwnsSaveData);
        var decls = module.PatchCategories.ToList();
        Assert.AreEqual(1, decls.Count);
        Assert.AreEqual("Patch154_CreatureSaddleFamilies", decls[0].Category);
        Assert.AreEqual(ApplyPhase.ProcessLoad, decls[0].Phase, "before the first game reads its XML");
        Assert.AreEqual(1, module.CampaignBehaviors.Count);
        Assert.AreEqual(typeof(CreatureSaddleSaveRepairBehavior), module.CampaignBehaviors[0].BehaviorType);
    }
}
