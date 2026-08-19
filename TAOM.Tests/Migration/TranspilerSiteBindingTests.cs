using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.BannerColorPersistence;
using TAOM.Features.BannerColorPersistence.Hooks;
using TAOM.Features.PartyIconScale;
using TAOM.Features.PartyIconScale.Hooks;

namespace TAOM.Tests.Migration;

/// <summary>
/// Proves TAOM's transpilers still FIND their IL sites in the installed engine.
///
/// <para>
/// <see cref="HarmonyPatchBindingTests"/> verifies that a patch's TARGET resolves. It cannot see
/// whether the transpiler that runs against that target still matches anything inside it, and every
/// TAOM transpiler is deliberately fail-SAFE: when its anchor is missing it logs a warning and
/// returns the instruction stream unmodified. That is the right runtime behaviour (a re-apply must
/// never crash) and the worst possible test behaviour, because the feature silently stops working
/// while every existing gate stays green.
/// </para>
///
/// <para>
/// The check feeds the REAL engine IL, read with Harmony's own
/// <c>PatchProcessor.GetOriginalInstructions</c>, through the production transpiler and asserts the
/// logger received no warning. Every fail-safe path announces itself through <see cref="IModLogger"/>,
/// so "no warning" is the proof the site resolved. This complements the existing transpiler unit
/// tests, which build synthetic <see cref="CodeInstruction"/> lists and therefore validate the
/// matcher rather than the engine.
/// </para>
///
/// Added at the v1.5.0 bump, which broke exactly this class of binding without turning anything red.
/// </summary>
[TestClass]
public class TranspilerSiteBindingTests
{
    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void BannerColorTranspiler_FindsBothFactionColourSites_InInstalledEngine()
    {
        if (!_gameLoaded) Assert.Inconclusive("Game assemblies unavailable.");

        var target = MobilePartyVisualHelper_GetHumanAgentPartyVisual_Patch.TargetMethod();
        Assert.IsNotNull(target,
            "SandBox.View.SandBoxViewHelpers+MobilePartyVisualHelper.GetHumanAgentPartyVisual did not resolve. " +
            "Party-icon clan colours are silently disabled.");

        var il = PatchProcessor.GetOriginalInstructions((MethodBase)target!).ToList();
        var logger = Substitute.For<IModLogger>();

        var rewritten = BannerColorTranspiler.Rewrite(
            il,
            AccessTools.Method(typeof(MobilePartyVisualHelper_GetHumanAgentPartyVisual_Patch),
                nameof(MobilePartyVisualHelper_GetHumanAgentPartyVisual_Patch.ResolveColor1)),
            AccessTools.Method(typeof(MobilePartyVisualHelper_GetHumanAgentPartyVisual_Patch),
                nameof(MobilePartyVisualHelper_GetHumanAgentPartyVisual_Patch.ResolveColor2)),
            logger);

        logger.DidNotReceive().LogWarning(Arg.Any<string>());

        // Two sites, two instructions inserted each.
        Assert.AreEqual(il.Count + 4, rewritten.Count,
            "Expected exactly two colour sites to be rewritten (ldarg.2 + call per site).");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void PartyIconScale_IconSites_StillResolve_InInstalledEngine()
    {
        if (!_gameLoaded) Assert.Inconclusive("Game assemblies unavailable.");

        var target = Patch53_PartyIconScale.TargetMethod();
        Assert.IsNotNull(target, "MobilePartyVisual.AddCharacterToPartyIcon did not resolve.");

        var il = PatchProcessor.GetOriginalInstructions((MethodBase)target!).ToList();
        var logger = Substitute.For<IModLogger>();
        var getScale = AccessTools.Method(typeof(PartyIconScaleConfig), nameof(PartyIconScaleConfig.GetScale));

        PartyIconScaleTranspiler.RewriteIconSites(il, getScale, logger);

        // Covers the mount site AND v1.5.0's new hardcoded human world-frame ApplyScaleLocal(0.3f).
        logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void PartyIconScale_RelocatedPeopleSite_StillResolves_InInstalledEngine()
    {
        if (!_gameLoaded) Assert.Inconclusive("Game assemblies unavailable.");

        var target = Patch53_PartyIconScaleHumanVisual.TargetMethod();
        Assert.IsNotNull(target,
            "MobilePartyVisualHelper.GetHumanAgentPartyVisual did not resolve. The leader-figure "
            + "scale slider is silently inert.");

        var il = PatchProcessor.GetOriginalInstructions((MethodBase)target!).ToList();
        var logger = Substitute.For<IModLogger>();
        var getScale = AccessTools.Method(typeof(PartyIconScaleConfig), nameof(PartyIconScaleConfig.GetScale));

        PartyIconScaleTranspiler.RewriteHumanVisualSite(il, getScale, logger);

        logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }
}
