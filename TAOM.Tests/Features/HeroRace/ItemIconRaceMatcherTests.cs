using System.Linq;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.HeroRace;
using TAOM.Features.HeroRace.Hooks;
using TAOM.Tests.Migration;
using TaleWorlds.Core;

namespace TAOM.Tests.Features.HeroRace;

/// <summary>
/// The matching rules behind Patch132 (dwarven armour icons). An item carries no race, so the patch
/// decides from names alone; a wrong rule here shows up in game only as an icon that is still framed
/// for a human, which a play-test easily misses.
/// </summary>
[TestClass]
public class ItemIconRaceMatcherTests
{
    [DataTestMethod]
    [DataRow("sk_dwarf_erebor_helmet_leather_light_a", null, null)]   // LOTRLOME_Armory Erebor
    [DataRow("sk_dwarf_iron_chest_a", null, null)]                   // Iron Hills
    [DataRow("some_helmet", "erebor_helm_mesh", null)]               // realm only in the mesh
    [DataRow("some_helmet", null, "iron_hills")]                     // no mesh: realm in the culture
    [DataRow("SK_DWARF_DAIN_PAULDRON", null, null)]                  // case-insensitive
    [DataRow("ered_luin_boots", null, null)]
    public void MatchRace_DwarvenItem_IsDwarf(string itemId, string mesh, string culture)
        => Assert.AreEqual(ItemIconRaceMatcher.DwarfRace, ItemIconRaceMatcher.MatchRace(itemId, mesh, culture));

    [DataTestMethod]
    [DataRow("sk_gd_ano_helmet_a", "sk_gd_ano_helmet_a", "gondor")]
    [DataRow("sk_uruk_hai_chest_a", "sk_uruk_hai_chest_a", "isengard")]
    [DataRow(null, null, null)]
    [DataRow("hon_ring_dwarf_1", "leather_gloves", "neutral_culture")] // human mesh, dwarf only in the id
    [DataRow("some_helmet", "some_mesh", "erebor")]                    // human mesh sold by a dwarf realm
    public void MatchRace_NonDwarvenItem_IsNull(string itemId, string mesh, string culture)
        => Assert.IsNull(ItemIconRaceMatcher.MatchRace(itemId, mesh, culture));

    // The tuning console refuses icon rows for any race not in Races; every race MatchRace returns
    // must therefore be in it, or that race could never be tuned.
    [TestMethod]
    public void Races_ContainsEveryRaceTheMatcherReturns()
        => Assert.IsTrue(ItemIconRaceMatcher.IsRace(ItemIconRaceMatcher.MatchRace("sk_dwarf_erebor_helmet", null, null)));

    [TestMethod]
    public void RowKey_JoinsRaceAndSlot()
        => Assert.AreEqual("dwarf_helmet", ItemIconRaceMatcher.RowKey("dwarf", "helmet"));

    [TestMethod]
    public void RowKey_NoRace_IsNull_SoTheLookupMisses()
        => Assert.IsNull(ItemIconRaceMatcher.RowKey(null, "helmet"));

    [TestMethod]
    public void TrySplitRowKey_RaceWithUnderscore_SplitsOnTheLastOne()
    {
        Assert.IsTrue(ItemIconRaceMatcher.TrySplitRowKey("cave_troll_helmet", out var race, out var slot));
        Assert.AreEqual("cave_troll", race);
        Assert.AreEqual("helmet", slot);
    }

    [DataTestMethod]
    [DataRow("dwarf")]
    [DataRow("dwarf_")]
    [DataRow("_helmet")]
    [DataRow("dwarf_shield")]
    [DataRow("")]
    public void TrySplitRowKey_Malformed_Fails(string key)
        => Assert.IsFalse(ItemIconRaceMatcher.TrySplitRowKey(key, out _, out _));

    // Every armour type vanilla gives its own icon camera maps to a slot, and the slot names are the
    // vanilla scene tags. Weapons and shields are not race-shaped and must stay untouched.
    [TestMethod]
    public void SlotFor_ArmourTypes_MapToVanillaCameraSlots()
    {
        Assert.AreEqual("helmet", ItemThumbnailCache_GetItemPoseAndCamera_RacePatch.SlotFor(ItemObject.ItemTypeEnum.HeadArmor));
        Assert.AreEqual("armor", ItemThumbnailCache_GetItemPoseAndCamera_RacePatch.SlotFor(ItemObject.ItemTypeEnum.BodyArmor));
        Assert.AreEqual("boot", ItemThumbnailCache_GetItemPoseAndCamera_RacePatch.SlotFor(ItemObject.ItemTypeEnum.LegArmor));
        Assert.AreEqual("glove", ItemThumbnailCache_GetItemPoseAndCamera_RacePatch.SlotFor(ItemObject.ItemTypeEnum.HandArmor));
        Assert.AreEqual("cape", ItemThumbnailCache_GetItemPoseAndCamera_RacePatch.SlotFor(ItemObject.ItemTypeEnum.Cape));
        Assert.IsNull(ItemThumbnailCache_GetItemPoseAndCamera_RacePatch.SlotFor(ItemObject.ItemTypeEnum.Shield));
        Assert.IsNull(ItemThumbnailCache_GetItemPoseAndCamera_RacePatch.SlotFor(ItemObject.ItemTypeEnum.OneHandedWeapon));

        foreach (var type in new[] { ItemObject.ItemTypeEnum.HeadArmor, ItemObject.ItemTypeEnum.BodyArmor,
                     ItemObject.ItemTypeEnum.LegArmor, ItemObject.ItemTypeEnum.HandArmor, ItemObject.ItemTypeEnum.Cape })
            Assert.IsTrue(ItemIconRaceMatcher.IsSlot(ItemThumbnailCache_GetItemPoseAndCamera_RacePatch.SlotFor(type)));
    }

    // The postfix binds the private target's parameters BY NAME ("item", "camera", "itemFrame"). A
    // rename in a future engine build makes Harmony refuse the category and the icons silently go
    // vanilla.
    [TestMethod]
    [TestCategory("BindingVerification")]
    public void Patch132_TargetParameters_BindAgainstInstalledEngine()
    {
        if (!GameAssemblies.EnsureLoaded())
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));

        var type = AccessTools.TypeByName("TaleWorlds.MountAndBlade.View.Tableaus.Thumbnails.ItemThumbnailCache");
        Assert.IsNotNull(type, "ItemThumbnailCache is gone from the installed engine.");

        var method = AccessTools.Method(type, "GetItemPoseAndCamera");
        Assert.IsNotNull(method, "ItemThumbnailCache.GetItemPoseAndCamera is gone; Patch132 cannot apply.");

        var parameters = method.GetParameters();
        var names = parameters.Select(p => p.Name).ToArray();
        CollectionAssert.Contains(names, "item");
        CollectionAssert.Contains(names, "camera");
        CollectionAssert.Contains(names, "itemFrame");

        // itemFrame must still be by ref, or moving it in the postfix changes nothing AddItem reads.
        var itemFrame = parameters.First(p => p.Name == "itemFrame");
        Assert.IsTrue(itemFrame.ParameterType.IsByRef, "itemFrame is no longer passed by ref.");
        Assert.AreEqual("TaleWorlds.Library.MatrixFrame", itemFrame.ParameterType.GetElementType().FullName);
    }
}
