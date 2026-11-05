using System.Linq;
using System.Reflection;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.CharacterCreation.Hooks;

namespace TAOM.Tests.Features.CharacterCreation;

/// <summary>
/// Patch20's guard on vanilla <c>CharacterCreationNarrativeStageView.SpawnNonHumanNarrativeMenuCharacter</c>
/// is a prefix: it skips the spawn when vanilla would throw (v1.4.8 :204-215), instead of a finalizer that
/// swallowed every NRE in the method and left a half-built entity in the scene (NRE guard audit 2026-10-27).
/// </summary>
[TestClass]
public class NarrativeMountSpawnGuardTests
{
    // Every value vanilla dereferences without a check (v1.4.8 :204-215, and MBGlobals.GetActionSet's
    // dictionary lookup on the monster's action-set code, Codex 2026-10-27 HIGH).
    private static readonly object MountKey = new object();

    [TestMethod]
    public void CanSpawnMount_EverythingPresent_IsTrue() =>
        Assert.IsTrue(NarrativeHorseGuardPatchHelper.CanSpawnMount(true, "horse", "harness", "as_horse", MountKey));

    [TestMethod]
    public void CanSpawnMount_NoSpawnPoint_IsFalse() =>
        Assert.IsFalse(NarrativeHorseGuardPatchHelper.CanSpawnMount(false, "horse", "harness", "as_horse", MountKey));

    [TestMethod]
    public void CanSpawnMount_NoHorseId_IsFalse() =>
        Assert.IsFalse(NarrativeHorseGuardPatchHelper.CanSpawnMount(true, null, "harness", "as_horse", MountKey));

    [TestMethod]
    public void CanSpawnMount_NoHarnessId_IsFalse() =>
        Assert.IsFalse(NarrativeHorseGuardPatchHelper.CanSpawnMount(true, "horse", null, "as_horse", MountKey));

    [TestMethod]
    public void CanSpawnMount_NoActionSetCode_IsFalse() =>
        Assert.IsFalse(NarrativeHorseGuardPatchHelper.CanSpawnMount(true, "horse", "harness", null, MountKey));

    [TestMethod]
    public void CanSpawnMount_NoMountCreationKey_IsFalse() =>
        Assert.IsFalse(NarrativeHorseGuardPatchHelper.CanSpawnMount(true, "horse", "harness", "as_horse", null));

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void ThePatch_IsAPrefixWithTheNamesHarmonyBinds_AndHasNoFinalizer()
    {
        var type = typeof(CharacterCreationNarrativeStageView_SpawnNonHuman_Patch);
        var methods = type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

        Assert.IsFalse(methods.Any(m => m.GetCustomAttribute<HarmonyFinalizer>() != null || m.Name == "Finalizer"),
            "A finalizer here swallowed foreign NREs; the guard is the prefix.");
        var prefix = methods.Single(m => m.Name == "Prefix");
        Assert.AreEqual(typeof(bool), prefix.ReturnType);
        CollectionAssert.AreEqual(new[] { "character", "____characterScene" },
            prefix.GetParameters().Select(p => p.Name).ToArray());
    }
}
