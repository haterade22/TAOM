using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.SkipCampaignIntro.Hooks;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.SkipCampaignIntro;

/// <summary>
/// Drift-guard for the two internal reflection bindings <see cref="Patch58_SkipCampaignIntro"/> resolves
/// at type-load against the installed engine: the private
/// <c>SandBoxGameManager.LaunchSandboxCharacterCreation</c> and the <c>MBGameManager.IsLoaded</c> setter.
/// <para>
/// <c>HarmonyPatchBindingTests</c> already covers the patch's <c>[HarmonyPatch]</c> target
/// (<c>OnLoadFinished</c>). These two <c>AccessTools</c> lookups live inside the patch body and are NOT
/// covered there, so a 1.4.x rename would silently disable the intro-skip (the patch fails safe to the
/// vanilla video) with no other red test. This is that red test.
/// </para>
/// </summary>
[TestClass]
public class Patch58SkipCampaignIntroTests
{
    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void LaunchSandboxCharacterCreation_BindingResolves_AgainstInstalledEngine()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));

        Assert.IsNotNull(
            Patch58_SkipCampaignIntro.LaunchCc,
            "SandBoxGameManager.LaunchSandboxCharacterCreation did not resolve against the installed engine — " +
            "engine drift would silently disable the campaign-intro skip (fail-safe to the vanilla video).");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void IsLoadedSetter_BindingResolves_AgainstInstalledEngine()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));

        Assert.IsNotNull(
            Patch58_SkipCampaignIntro.SetIsLoaded,
            "MBGameManager.IsLoaded setter did not resolve against the installed engine — " +
            "engine drift would silently disable the campaign-intro skip (fail-safe to the vanilla video).");
    }
}
