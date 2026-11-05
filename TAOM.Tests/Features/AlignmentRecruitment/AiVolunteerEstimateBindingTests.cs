using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Composition;
using TAOM.Features.AlignmentRecruitment;
using TAOM.Features.AlignmentRecruitment.Hooks;
using TAOM.Tests.Migration;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors.AiBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace TAOM.Tests.Features.AlignmentRecruitment;

/// <summary>
/// Pins the vanilla seam the AI volunteer estimate postfix (#887) rides: the private estimate still takes (Hero, Settlement) and
/// returns (int, float), the category applies exactly this postfix and the replacement JIT-compiles, and vanilla's
/// <c>AiHourlyTick</c> still calls the estimate, so the postfix is not inert. Reads the installed engine, no campaign.
/// </summary>
[TestClass]
public class AiVolunteerEstimateBindingTests
{
    private const string EstimateName = "GetApproximateVolunteersCanBeRecruitedDataFromSettlement";

    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(Microsoft.VisualStudio.TestTools.UnitTesting.TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    [TestMethod]
    [TestCategory("RequiresGameIL")]
    [TestCategory("BindingVerification")]
    public void Target_ResolvesWithTheTupleReturnAndHeroSettlementParameters_InInstalledEngine()
    {
        if (!_gameLoaded) Assert.Inconclusive("Game assemblies unavailable.");

        var target = Patch114_AiVolunteerEstimate_Postfix.TargetMethod();

        Assert.IsNotNull(target, "AiVisitSettlementBehavior." + EstimateName + "(Hero, Settlement) did not resolve.");
        Assert.AreEqual(typeof(ValueTuple<int, float>), ((MethodInfo)target).ReturnType);
        CollectionAssert.AreEqual(new[] { typeof(Hero), typeof(Settlement) }, target.GetParameters().Select(p => p.ParameterType).ToArray());
    }

    [TestMethod]
    [TestCategory("RequiresGameIL")]
    [TestCategory("BindingVerification")]
    public void PatchCategory_AppliedToTheInstalledEngine_AttachesThisPostfixAndTheJitAccepts()
    {
        if (!_gameLoaded) Assert.Inconclusive("Game assemblies unavailable.");
        const string id = "taom.tests.alignmentrecruitment.jit";
        var target = Patch114_AiVolunteerEstimate_Postfix.TargetMethod();
        Assert.IsNotNull(target, EstimateName + " did not resolve.");
        var harmony = new Harmony(id);
        try
        {
            PatchCategoryIndex.Build(typeof(AlignmentRecruitmentModule).Assembly).Apply(harmony, AlignmentRecruitmentModule.PatchCategory);

            var info = Harmony.GetPatchInfo(target);
            Assert.IsNotNull(info, "applying the category patched nothing: the class is not in it");
            var postfixes = info!.Postfixes.Where(p => p.owner == id).ToList();
            Assert.AreEqual(1, postfixes.Count, "exactly one postfix is attached");
            Assert.AreEqual(typeof(Patch114_AiVolunteerEstimate_Postfix), postfixes[0].PatchMethod.DeclaringType);
            Assert.AreEqual(0, info.Prefixes.Count(p => p.owner == id), "no prefix is attached");
            RuntimeHelpers.PrepareMethod(target.MethodHandle);
        }
        finally
        {
            harmony.UnpatchAll(id);
        }
    }

    [TestMethod]
    [TestCategory("RequiresGameIL")]
    [TestCategory("BindingVerification")]
    public void AiHourlyTick_StillCallsTheVolunteerEstimate_InInstalledEngine()
    {
        if (!_gameLoaded) Assert.Inconclusive("Game assemblies unavailable.");

        var tick = AccessTools.Method(typeof(AiVisitSettlementBehavior), "AiHourlyTick", new[] { typeof(MobileParty), typeof(PartyThinkParams) });
        Assert.IsNotNull(tick, "AiVisitSettlementBehavior.AiHourlyTick(MobileParty, PartyThinkParams) did not resolve.");

        var il = PatchProcessor.GetOriginalInstructions(tick!).ToList();

        Assert.IsTrue(
            il.Any(ci => ci.operand is MethodInfo mi && mi.Name == EstimateName),
            "AiHourlyTick no longer calls " + EstimateName + ". If vanilla stops calling the estimate from AiHourlyTick, the #887 postfix is inert.");
    }
}
