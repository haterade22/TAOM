using System.IO;
using System.Linq;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.SettlementGuards;
using TAOM.Features.TroopProgression;

namespace TAOM.Tests.Features.SettlementGuards;

// Wiring-regression test for SettlementGuards. Unlike most TAOM features, SettlementGuards uses
// MANUAL Harmony patching (no [HarmonyPatchCategory]) because both target methods
// (GuardsCampaignBehavior.TakeGuardAgentDataFromGarrisonTroopList + GetSuitableSpear) are private
// instance methods that AccessTools cannot resolve via attribute-based discovery. The patches are
// applied in Main/ManualPatchApplicator.cs (extracted from SubModule.OnGameInitializationFinished,
// which calls ManualPatchApplicator.ApplyAll(_harmony)) via harmony.Patch(...) plus an
// Initialize(_service) call that wires the static IoC reference each patch's Prefix reads.
//
// The audit-motivating regression class (#121, Messengers): a manual-patch feature is uniquely
// vulnerable to "wired in code but never plugged into SubModule" because there is no Harmony
// category to forget — the patch literally requires a hand-written call site. This test class
// asserts the wiring catalog so a future refactor that drops either patch site, drops either
// Initialize call, or drops the IoC registration goes red.
[TestClass]
public class SettlementGuardsWiringTests
{
    // --- Wiring catalog regression tests ---

    [TestMethod]
    public void MainIoCConfigure_IncludesSettlementGuardsFeatureRegistration()
    {
        var iocSource = ReadProjectSource("Main", "IoC.cs");
        if (iocSource == null)
            Assert.Inconclusive("Main/IoC.cs not found — run from repo root or check working directory");

        StringAssert.Contains(iocSource, "SettlementGuardsIoC.RegisterSettlementGuardsFeature(container);",
            "Main/IoC.cs::Configure must call SettlementGuardsIoC.RegisterSettlementGuardsFeature(container). " +
            "Without it, the service cannot be resolved when SubModule.cs calls IoC.Resolve<ISettlementGuardService>() " +
            "for the patch Initialize calls.");
    }

    [TestMethod]
    public void MainSubModule_InvokesManualPatchApplicator()
    {
        var subModuleSource = ReadProjectSource("Main", "SubModule.cs");
        if (subModuleSource == null)
            Assert.Inconclusive("Main/SubModule.cs not found — run from repo root or check working directory");

        // The manual-patch block was extracted to ManualPatchApplicator (ADR-002); the entry point
        // must still invoke it or NO manual patch (SettlementGuards, BannerColor visuals,
        // CompanionTactics tooltip) applies.
        StringAssert.Contains(subModuleSource, "ManualPatchApplicator.ApplyAll(_harmony);",
            "Main/SubModule.cs::OnGameInitializationFinished must call ManualPatchApplicator.ApplyAll(_harmony). " +
            "Without it, none of the manual (private-target) Harmony patches apply.");
    }

    [TestMethod]
    public void ManualPatchApplicator_AppliesManualHarmonyPatches()
    {
        var applicatorSource = ReadProjectSource("Main", "ManualPatchApplicator.cs");
        if (applicatorSource == null)
            Assert.Inconclusive("Main/ManualPatchApplicator.cs not found — run from repo root or check working directory");

        // Both manual patch sites must be present. The pattern is:
        //   var target = <PatchClass>.TargetMethod();
        //   if (target != null) harmony.Patch(target, prefix: new HarmonyMethod(...));
        StringAssert.Contains(applicatorSource, "GuardsCampaignBehavior_TakeGuardAgentData_Patch.TargetMethod()",
            "ManualPatchApplicator must call GuardsCampaignBehavior_TakeGuardAgentData_Patch.TargetMethod() and then " +
            "harmony.Patch(...) on the result. Without it, custom settlement guards do not apply.");

        StringAssert.Contains(applicatorSource, "GuardsCampaignBehavior_GetSuitableSpear_Patch.TargetMethod()",
            "ManualPatchApplicator must call GuardsCampaignBehavior_GetSuitableSpear_Patch.TargetMethod() and then " +
            "harmony.Patch(...) on the result. Without it, per-culture spear assignment does not apply.");
    }

    [TestMethod]
    public void ManualPatchApplicator_InitializesBothPatchClassesWithService()
    {
        var applicatorSource = ReadProjectSource("Main", "ManualPatchApplicator.cs");
        if (applicatorSource == null)
            Assert.Inconclusive("Main/ManualPatchApplicator.cs not found — run from repo root or check working directory");

        // The patches' Prefix bodies read static _service set by Initialize(). Without the
        // Initialize call, _service stays null and the Prefix early-exits (identical failure
        // mode to the #122 BannerColor regression).
        StringAssert.Contains(applicatorSource, "GuardsCampaignBehavior_TakeGuardAgentData_Patch.Initialize(",
            "ManualPatchApplicator must call GuardsCampaignBehavior_TakeGuardAgentData_Patch.Initialize(...) " +
            "before any guard spawn fires. Without it the Prefix's _service field stays null.");

        StringAssert.Contains(applicatorSource, "GuardsCampaignBehavior_GetSuitableSpear_Patch.Initialize(",
            "ManualPatchApplicator must call GuardsCampaignBehavior_GetSuitableSpear_Patch.Initialize(...) " +
            "before any guard spawn fires. Without it the Prefix's _service field stays null.");
    }

    // --- DryIoc registration smoke test ---

    [TestMethod]
    public void RegisterSettlementGuardsFeature_RegistersService()
    {
        var container = new Container();
        container.RegisterInstance<IModLogger>(Substitute.For<IModLogger>());
        container.RegisterInstance<IPathService>(Substitute.For<IPathService>());
        // SettlementGuardService ctor pulls IRandomProvider (cross-feature dep from TroopProgression)
        // — mirror what Main/IoC.cs guarantees by registering TroopProgressionIoC before SettlementGuardsIoC.
        container.RegisterInstance<IRandomProvider>(Substitute.For<IRandomProvider>());

        SettlementGuardsIoC.RegisterSettlementGuardsFeature(container);

        Assert.IsNotNull(container.Resolve<ISettlementGuardService>(),
            "ISettlementGuardService must be resolvable after RegisterSettlementGuardsFeature. " +
            "SubModule.cs's IoC.Resolve<ISettlementGuardService>() call would throw at startup otherwise.");
        Assert.IsNotNull(container.Resolve<ISettlementGuardConfigProvider>(),
            "ISettlementGuardConfigProvider must be resolvable after RegisterSettlementGuardsFeature.");
    }

    // --- Helpers ---

    private static string ReadProjectSource(params string[] relativeParts)
    {
        var dir = Directory.GetCurrentDirectory();
        while (dir != null)
        {
            var candidate = Path.Combine(new[] { dir }.Concat(relativeParts).ToArray());
            if (File.Exists(candidate))
                return File.ReadAllText(candidate);
            dir = Directory.GetParent(dir)?.FullName;
        }
        return null;
    }
}
