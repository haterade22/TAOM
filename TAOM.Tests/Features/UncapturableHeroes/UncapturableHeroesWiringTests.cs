using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Features.UncapturableHeroes;

/// <summary>
/// Wiring regression guard, in the RefugeWiringTests shape. Every seam here fails SILENTLY when
/// dropped: an unregistered service resolves nothing, a dropped patch Initialize leaves a
/// null-guarded no-op patch that always defers to vanilla, a missing PatchCategory line makes
/// Harmony apply nothing and report nothing, and a missing ResetForUnload leaks a stale service
/// into the next module load. Each is pinned against the ACTUAL source block that carries the
/// claim, so the test can fail by construction.
/// </summary>
[TestClass]
public class UncapturableHeroesWiringTests
{
    private static string RepoRoot => Path.GetFullPath(
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\.."));

    private static string ReadSource(params string[] parts)
    {
        var path = Path.Combine(new[] { RepoRoot }.Concat(parts).ToArray());
        Assert.IsTrue(File.Exists(path), $"Expected source file not found: {path}");
        return File.ReadAllText(path);
    }

    // ---- Feature IoC --------------------------------------------------------

    [TestMethod]
    public void FeatureIoC_RegistersEveryServiceTheHooksNeed()
    {
        var src = ReadSource("Main", "Features", "UncapturableHeroes", "UncapturableHeroesIoC.cs");

        StringAssert.Contains(src, "IUncapturableHeroService, UncapturableHeroService");
        StringAssert.Contains(src, "IUncapturableRegistry, UncapturableRegistry");
        StringAssert.Contains(src, "IUncapturableHeroesConfigProvider, UncapturableHeroesConfigProvider");
        StringAssert.Contains(src, "IUncapturableHeroesSettingsProvider, UncapturableHeroesSettingsProvider");
        StringAssert.Contains(src, "IHeroCaptivityAdapter, HeroCaptivityAdapter",
            "Without the captivity adapter the direct-capture seam can never free anyone and "
            + "silently defers every capture to vanilla.");
    }

    [TestMethod]
    public void FeatureIoC_InitializesBothHooks()
    {
        var src = ReadSource("Main", "Features", "UncapturableHeroes", "UncapturableHeroesIoC.cs");

        StringAssert.Contains(src, "Hero_CanBecomePrisoner_Patch.Initialize");
        StringAssert.Contains(src, "TakePrisonerAction_Apply_Patch.Initialize",
            "An uninitialised hook null-guards itself into a permanent no-op with no error.");
    }

    // ---- Container wiring ---------------------------------------------------

    [TestMethod]
    public void RootIoC_RegistersTheFeature_AfterEnlistment()
    {
        // IInquiryAdapter is registered in exactly one place (EnlistmentIoC) with no
        // IfAlreadyRegistered. Registering this feature before it would resolve a different
        // instance or fail outright.
        var src = ReadSource("Main", "IoC.cs");

        var enlistment = src.IndexOf("EnlistmentIoC.RegisterEnlistmentFeature", StringComparison.Ordinal);
        var ours = src.IndexOf(
            "UncapturableHeroesIoC.RegisterUncapturableHeroesFeature", StringComparison.Ordinal);

        Assert.IsTrue(enlistment >= 0, "EnlistmentIoC registration not found in IoC.cs.");
        Assert.IsTrue(ours >= 0, "UncapturableHeroes is not registered in IoC.cs; the whole feature is inert.");
        Assert.IsTrue(ours > enlistment,
            "UncapturableHeroes must be registered AFTER Enlistment, which owns the single "
            + "IInquiryAdapter registration.");
    }

    [TestMethod]
    public void RootIoC_InitializesThePatchStatics()
    {
        var src = ReadSource("Main", "IoC.cs");

        StringAssert.Contains(src, "UncapturableHeroesIoC.InitializePatchStatics",
            "Without this the hooks hold a null service and defer every capture to vanilla.");
    }

    // ---- Harmony application ------------------------------------------------

    [TestMethod]
    public void SubModule_AppliesThePatchCategory()
    {
        var src = ReadSource("Main", "SubModule.cs");

        StringAssert.Contains(src, "PatchCategory(\"Patch76_UncapturableHeroes\")",
            "The category is never applied, so Harmony patches nothing and reports nothing.");
    }

    [TestMethod]
    public void SubModule_ResetsBothHooksOnUnload()
    {
        var src = ReadSource("Main", "SubModule.cs");

        StringAssert.Contains(src, "Hero_CanBecomePrisoner_Patch.ResetForUnload()");
        StringAssert.Contains(src, "TakePrisonerAction_Apply_Patch.ResetForUnload()");
    }

    // ---- MCM ----------------------------------------------------------------

    [TestMethod]
    public void TaomSettings_ExposesTheToggle()
    {
        var src = ReadSource("Main", "Features", "TaomSettings.cs");

        StringAssert.Contains(src, "EnableUncapturableHeroes",
            "The settings provider reads this property; without it the feature has no in-game toggle.");
        StringAssert.Contains(src, "World/Uncapturable Heroes");
    }

    // ---- Shipped data -------------------------------------------------------

    [TestMethod]
    public void ConfigFile_ShipsInsideTheModule()
    {
        var path = Path.Combine(
            RepoRoot, "Main", "_Module", "ModuleData", "uncapturable_heroes", "uncapturable_heroes_config.json");

        Assert.IsTrue(File.Exists(path),
            "The config is missing from the shipped module. The provider falls back to compiled "
            + "defaults, so the feature still works, but nothing an author edits has any effect.");
    }
}
