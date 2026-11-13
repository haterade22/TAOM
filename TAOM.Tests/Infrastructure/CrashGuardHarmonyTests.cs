using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Dependencies.Foundation;
using TAOM.Features.MapEventGuard.Hooks;

namespace TAOM.Tests.Infrastructure;

/// <summary>
/// PatchShield strips every unprotected owner's prefixes, postfixes and transpilers on a method after a
/// missing-API exception there, and "com.taom.mod" is not protected. A crash guard that shares its method with
/// another TAOM patch would go with it when that other patch breaks after a game update: on
/// PlayerEncounter.DoMeeting a broken Refuge or Supply Lines patch would also remove the #1099 empty-meeting
/// guard (Patch143). Crash guards are therefore applied under their own "TAOM.*" Harmony id, which
/// PatchShieldPolicy protects.
/// </summary>
[TestClass]
public class CrashGuardHarmonyTests
{
    [TestMethod]
    public void HarmonyIdFor_EmptyMeetingGuard_IsTheCrashGuardId()
    {
        Assert.AreEqual(CrashGuardHarmony.Id, CrashGuardHarmony.HarmonyIdFor(Patch143_EmptyMeetingGuard.Category));
    }

    [TestMethod]
    public void HarmonyIdFor_OrdinaryCategory_IsTheMainId()
    {
        Assert.AreEqual(CrashGuardHarmony.MainId, CrashGuardHarmony.HarmonyIdFor("Patch75_Refuge"));
    }

    [TestMethod]
    public void CrashGuardId_IsProtectedFromThePatchShieldStrip()
    {
        Assert.IsTrue(PatchShieldPolicy.IsProtectedOwner(
            CrashGuardHarmony.Id, PatchShieldPolicy.CompiledProtectedOwnerPrefixes));
    }

    [TestMethod]
    public void MainId_IsNotProtected_SoABrokenFeaturePatchCanStillBeStripped()
    {
        Assert.IsFalse(PatchShieldPolicy.IsProtectedOwner(
            CrashGuardHarmony.MainId, PatchShieldPolicy.CompiledProtectedOwnerPrefixes));
    }

    [TestMethod]
    public void SubModule_AppliesCrashGuardsUnderTheirOwnHarmonyId()
    {
        var source = File.ReadAllText(RepoPaths.RepoPath("Main", "SubModule.cs"));

        StringAssert.Contains(source, "new Harmony(CrashGuardHarmony.Id)");
        StringAssert.Contains(source, "CrashGuardHarmony.IsCrashGuard(category) ? _guardHarmony : _harmony");
        StringAssert.Contains(source, "_guardHarmony?.UnpatchAll(CrashGuardHarmony.Id)");
    }
}
