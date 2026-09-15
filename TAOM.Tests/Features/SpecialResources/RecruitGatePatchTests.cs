using System.Linq;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.SpecialResources.Hooks;

namespace TAOM.Tests.Features.SpecialResources;

/// <summary>
/// The recruit-volunteers screen has two ways to commit the cart: the Done button, which Patch51's
/// postfix on <c>RefreshPartyProperties</c> greys when the cart is unaffordable in the player's
/// special resource, and the Confirm hotkey, which v1.5.3's
/// <c>GauntletMenuRecruitVolunteersView.OnFrameTick</c> routes straight to
/// <c>RecruitmentVM.ExecuteDone</c> with no look at <c>IsDoneEnabled</c>; <c>OnDone</c> then rechecks
/// gold only. Codex review of #600 (M1): with 40 Castar and a 45 Ranger in the cart the hotkey
/// recruited the Ranger and debited 40. The prefix on <c>ExecuteDone</c> closes that; these tests pin
/// the pure cart grouping both patches share and the category the prefix must carry.
/// </summary>
[TestClass]
public class RecruitGatePatchTests
{
    [TestMethod]
    public void Group_DuplicateIds_CountsEachOnce()
    {
        var entries = RecruitCartGrouping.Group(new[] { "a", "b", "a", "a", "b" });

        Assert.AreEqual(2, entries.Count);
        Assert.AreEqual("a", entries[0].TroopId);
        Assert.AreEqual(3, entries[0].Count);
        Assert.AreEqual("b", entries[1].TroopId);
        Assert.AreEqual(2, entries[1].Count);
    }

    [TestMethod]
    public void Group_NullIds_AreSkipped()
    {
        var entries = RecruitCartGrouping.Group(new[] { null, "a", null });

        Assert.AreEqual(1, entries.Count);
        Assert.AreEqual("a", entries[0].TroopId);
        Assert.AreEqual(1, entries[0].Count);
    }

    [TestMethod]
    public void Group_EmptyOrAllNull_ReturnsEmpty()
    {
        Assert.AreEqual(0, RecruitCartGrouping.Group(new string[0]).Count);
        Assert.AreEqual(0, RecruitCartGrouping.Group(new string[] { null, null }).Count);
    }

    [TestMethod]
    public void ExecuteDonePrefix_CarriesTheGateCategory_SoOnePatchCategoryCallAppliesBoth()
    {
        var expected = typeof(RecruitmentVM_RecruitGate_Patch)
            .GetCustomAttributes(typeof(HarmonyPatchCategory), inherit: false)
            .Cast<HarmonyPatchCategory>().Select(c => c.info.category).Single();
        Assert.AreEqual("Patch51_RecruitmentResourceGate", expected, "the gate category literal moved: update SubModule.cs and the registry");

        var prefixCategories = typeof(RecruitmentVM_ExecuteDone_Patch)
            .GetCustomAttributes(typeof(HarmonyPatchCategory), inherit: false)
            .Cast<HarmonyPatchCategory>().Select(c => c.info.category).ToList();
        CollectionAssert.Contains(prefixCategories, expected,
            "RecruitmentVM_ExecuteDone_Patch must share Patch51's category, or SubModule's single PatchCategory call leaves the hotkey bypass open");

        var target = typeof(RecruitmentVM_ExecuteDone_Patch)
            .GetCustomAttributes(typeof(HarmonyPatch), inherit: false)
            .Cast<HarmonyPatch>().Single();
        Assert.AreEqual("ExecuteDone", target.info.methodName, "the prefix must sit on ExecuteDone: the only public entry both the hotkey and the button reach");
    }
}
