using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.BannerColorPersistence.Hooks;
using TAOM.Features.CastleRecruitment.Hooks;
using TAOM.Features.CharacterSelection.Patches;
using TAOM.Features.Enlistment.Hooks;
using TAOM.Features.MarriageAlignment.Hooks;
using TAOM.Features.PartyIconScale;
using TAOM.Features.PartyIconScale.Hooks;
using TAOM.Features.RaceAge.Hooks;

namespace TAOM.Tests.Migration;

/// <summary>
/// Proves TAOM's transpilers still find their IL sites in the installed engine (the 1.5.x pattern of 8b9f0a233,
/// rewritten for the 1.4.x transpilers). HarmonyPatchBindingTests proves a transpiler's target resolves; it cannot
/// see whether the rewrite still matches anything inside it. Every TAOM transpiler is fail-safe: with its anchor gone
/// it returns the instructions unchanged, so the feature stops while every other gate stays green. Each test reads
/// the real installed IL with PatchProcessor.GetOriginalInstructions, runs the production transpiler, and counts what
/// it changed. The expected counts are the v1.4.8 sites (taom-src, cited per test); after an engine bump a moved count
/// is a finding: read the new IL before touching a number. Patch103 (MissionStartGuard) is pinned the same way by its
/// own MissionStartGuardBindingTests. A new transpiler gets a test here, or a feature binding test that runs it over the
/// installed IL.
/// </summary>
[TestClass]
public class TranspilerSiteBindingTests
{
    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void Patch15_BannerLayerCap_IsStillAbsent_SoTheDisabledTranspilerStaysCorrect()
    {
        RequireGame();
        var il = InstalledIl(Banner_TryGetBannerDataFromCode_Transpiler.TargetMethod(), "Banner.TryGetBannerDataFromCode");

        // v1.4.7 made banner layers unlimited; v1.4.8 Banner.TryGetBannerDataFromCode (Banner.cs:576) has no RemoveRange.
        Assert.AreEqual(0, il.Count(ci => ci.operand is MethodInfo mi && mi.Name == "RemoveRange"),
            "The engine trims banner layers again. Patch15_BannerLayerLimit is off by default and self-bails: enable "
            + "BannerColorConfig.EnableLayerLimitTranspiler only after reading the new IL, and give it a site test.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void Patch23_NotificationCharacter_SwapsBothMapFactionReads()
    {
        RequireGame();
        var il = InstalledIl(SingleOriginal(typeof(CampaignSceneNotificationHelper_CreateNotificationCharacter_Transpiler)),
            "CampaignSceneNotificationHelper.CreateNotificationCharacterFromHero");

        var output = CampaignSceneNotificationHelper_CreateNotificationCharacter_Transpiler.Transpiler(il).ToList();

        // v1.4.8 CampaignSceneNotificationHelper.cs:172 and :176 read hero.MapFaction for the two colours.
        Assert.AreEqual(2, Calls(output, nameof(CampaignSceneNotificationHelper_CreateNotificationCharacter_Transpiler),
            nameof(CampaignSceneNotificationHelper_CreateNotificationCharacter_Transpiler.GetFactionOrClanFaction)));
        Assert.AreEqual(0, Calls(output, "Hero", "get_MapFaction"));
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void Patch42_AiHourlyTick_SwapsTheRecruitmentIsCastleGate()
    {
        RequireGame();
        var il = InstalledIl(Patch42_AiHourlyTick_Transpiler.TargetMethod(), "AiVisitSettlementBehavior.AiHourlyTick");

        var output = Patch42_AiHourlyTick_Transpiler.Transpiler(il).ToList();

        // v1.4.8 AiVisitSettlementBehavior.cs:279: !settlement.IsCastle && item < 1f && GetAvailableWageBudget() > 0.
        Assert.AreEqual(1, Calls(output, nameof(CastleAiToggle), nameof(CastleAiToggle.IsExcludedFromRecruitScoring)));
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void LateTranspiler_CharacterCustomization_InjectsTheRaceActionSet()
    {
        RequireGame();
        var il = InstalledIl(SingleOriginal(typeof(RefreshCharacterEntityAuxPatch)), "BodyGeneratorView.RefreshCharacterEntityAux");
        var before = il.Count;

        var output = RunPrivateTranspiler(typeof(RefreshCharacterEntityAuxPatch), il, (object?)null);

        // v1.4.8 BodyGeneratorView.cs:672: the method's one new AgentVisualsData(); three instructions go in after it.
        Assert.AreEqual(before + 3, output.Count, "the face-generator action-set injection degraded to a no-op");
        Assert.AreEqual(1, Calls(output, nameof(RefreshCharacterEntityAuxPatch), nameof(RefreshCharacterEntityAuxPatch.GetActionSet)));
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void Patch66_OrderBanner_SwapsTheOneToStringCall()
    {
        RequireGame();
        var il = InstalledIl(BehaviorComponent_OnBehaviorActivated_Transpiler.TargetMethod(), "BehaviorComponent.OnBehaviorActivated");

        var output = BehaviorComponent_OnBehaviorActivated_Transpiler.Transpiler(il).ToList();

        // v1.4.8 BehaviorComponent.cs:107 builds the banner from ToString(), the method's only ToString call.
        Assert.AreEqual(1, Calls(output, nameof(BehaviorComponent_OnBehaviorActivated_Transpiler),
            nameof(BehaviorComponent_OnBehaviorActivated_Transpiler.DescribeBehavior)));
        Assert.AreEqual(0, output.Count(ci => ci.operand is MethodInfo mi && mi.Name == "ToString" && mi.GetParameters().Length == 0));
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void Patch81_PartnerClanDraw_SwapsBothClanAllCalls()
    {
        RequireGame();
        var il = InstalledIl(SingleOriginal(typeof(Patch81_MarriageClanDraw)), "RomanceCampaignBehavior.CheckNpcMarriages");

        var output = RunPrivateTranspiler(typeof(Patch81_MarriageClanDraw), il);

        // v1.4.8 RomanceCampaignBehavior.cs:122: Clan.All[MBRandom.RandomInt(Clan.All.Count)], two get_All calls.
        Assert.AreEqual(2, Calls(output, nameof(Patch81_MarriageClanDraw), nameof(Patch81_MarriageClanDraw.CandidateClansFor)));
        Assert.AreEqual(0, Calls(output, "Clan", "get_All"));
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void Patch53_PartyIcon_SwapsBothScaleLiterals()
    {
        RequireGame();
        var il = InstalledIl(Patch53_PartyIconScale.TargetMethod(), "MobilePartyVisual.AddCharacterToPartyIcon");
        var logger = Substitute.For<IModLogger>();
        var getScale = AccessTools.Method(typeof(PartyIconScaleConfig), nameof(PartyIconScaleConfig.GetScale));

        var output = PartyIconScaleTranspiler.Rewrite(il, getScale, logger);

        // v1.4.8 MobilePartyVisual.cs:1137 .Scale(0.3f) (people) and :1197 ScaleFactor * 0.3f (mount).
        logger.DidNotReceive().LogWarning(Arg.Any<string>());
        Assert.AreEqual(2, Calls(output, nameof(PartyIconScaleConfig), nameof(PartyIconScaleConfig.GetScale)));
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void Patch13_DeliverOffSpring_NopsTheRaceAssert()
    {
        RequireGame();
        var il = InstalledIl(SingleOriginal(typeof(DeliverOffSpring_RaceAssert_Patch)), "HeroCreator.DeliverOffSpring");
        var before = il.Count;

        var output = RunPrivateTranspiler(typeof(DeliverOffSpring_RaceAssert_Patch), il);

        // v1.4.8 HeroCreator.cs:260: the method's one Debug.SilentAssert (mother.Race == father.Race), NOPped in place.
        Assert.AreEqual(before, output.Count);
        Assert.AreEqual(0, Calls(output, "Debug", "SilentAssert"));
    }

    private static void RequireGame()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));
    }

    private static List<CodeInstruction> InstalledIl(MethodBase? target, string what)
    {
        Assert.IsNotNull(target, what + " did not resolve against the installed engine.");
        return PatchProcessor.GetOriginalInstructions(target!);
    }

    // The one method Harmony would patch for an attribute-targeted class, resolved as Harmony resolves it.
    private static MethodBase SingleOriginal(Type patchClass)
    {
        var problems = new List<string>();
        var originals = HarmonyTargetResolver.ResolveOriginals(patchClass, problems);
        Assert.AreEqual(0, problems.Count, patchClass.Name + ": " + string.Join("; ", problems));
        Assert.AreEqual(1, originals.Count, patchClass.Name + " should patch exactly one method.");
        return originals[0];
    }

    private static List<CodeInstruction> RunPrivateTranspiler(Type patchClass, List<CodeInstruction> il, params object?[] extra)
    {
        var transpiler = AccessTools.Method(patchClass, "Transpiler");
        Assert.IsNotNull(transpiler, patchClass.Name + ".Transpiler not found.");
        var arguments = new object?[] { il }.Concat(extra).ToArray();
        return ((IEnumerable<CodeInstruction>)transpiler!.Invoke(null, arguments)!).ToList();
    }

    private static int Calls(IEnumerable<CodeInstruction> il, string declaringType, string method) =>
        il.Count(ci => (ci.opcode == OpCodes.Call || ci.opcode == OpCodes.Callvirt)
            && ci.operand is MethodInfo mi && mi.Name == method && mi.DeclaringType?.Name == declaringType);
}
