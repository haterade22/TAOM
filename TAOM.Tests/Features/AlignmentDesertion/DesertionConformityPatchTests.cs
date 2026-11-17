using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using TAOM.Composition;
using TAOM.Core.Logging;
using TAOM.Features.AlignmentDesertion;
using TAOM.Features.AlignmentDesertion.Hooks;
using TAOM.Features.CoopInterop;
using TAOM.Tests.Migration;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;

namespace TAOM.Tests.Features.AlignmentDesertion;

/// <summary>
/// #1189: the prisoner conformity prefix (Patch150). A stack the recruit guard blocks gets no conformity and the
/// loop moves on (false); every other stack runs vanilla. Also pins the vanilla seam on the installed engine:
/// the private method's signature and parameter names, that it returns true for every stack (the reason a blocked
/// stack used to end the loop), that both prisoner ticks still call it, and that the category attaches exactly this prefix.
/// </summary>
[TestClass]
public class DesertionConformityPatchTests
{
    private const string TargetName = "GenerateConformityForTroop";

    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    [TestCleanup]
    public void Cleanup() => Patch150_ConformityTick_Prefix.Initialize(null!, null!, null!);

    private static MobileParty AnyParty() => (MobileParty)FormatterServices.GetUninitializedObject(typeof(MobileParty));

    private static IDesertionRecruitGuard Guard(bool wouldDesert)
    {
        var guard = Substitute.For<IDesertionRecruitGuard>();
        guard.WouldDesertFromParty(Arg.Any<PartyBase?>(), Arg.Any<CharacterObject?>()).Returns(wouldDesert);
        return guard;
    }

    private static ICoopSessionProvider Solo(bool defer = false)
    {
        var coop = Substitute.For<ICoopSessionProvider>();
        coop.ShouldDeferToHost.Returns(defer);
        return coop;
    }

    private static bool Run(MobileParty party, out bool result)
    {
        result = true;
        return Patch150_ConformityTick_Prefix.Prefix(party, null!, ref result);
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void BlockedStack_SkipsVanillaAndReturnsFalse()
    {
        if (!_gameLoaded) Assert.Inconclusive("Game assemblies unavailable.");
        Patch150_ConformityTick_Prefix.Initialize(Guard(wouldDesert: true), Solo(), Substitute.For<IModLogger>());

        Assert.IsFalse(Run(AnyParty(), out var result), "vanilla must not add conformity to a blocked stack");
        Assert.IsFalse(result, "false keeps the loop going to the next stack");
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void AllowedStack_RunsVanilla()
    {
        if (!_gameLoaded) Assert.Inconclusive("Game assemblies unavailable.");
        Patch150_ConformityTick_Prefix.Initialize(Guard(wouldDesert: false), Solo(), Substitute.For<IModLogger>());

        Assert.IsTrue(Run(AnyParty(), out var result));
        Assert.IsTrue(result, "the prefix leaves the result to vanilla");
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void CoopClient_RunsVanillaEvenForABlockedStack()
    {
        if (!_gameLoaded) Assert.Inconclusive("Game assemblies unavailable.");
        Patch150_ConformityTick_Prefix.Initialize(Guard(wouldDesert: true), Solo(defer: true), Substitute.For<IModLogger>());

        Assert.IsTrue(Run(AnyParty(), out _), "the guard reads per-peer settings, so a deferring peer keeps vanilla");
    }

    [TestMethod]
    public void NoGuardOrNoParty_RunsVanilla()
    {
        Patch150_ConformityTick_Prefix.Initialize(null!, null!, null!);
        Assert.IsTrue(Run(null!, out _), "no guard");
        Patch150_ConformityTick_Prefix.Initialize(Guard(wouldDesert: true), Solo(), Substitute.For<IModLogger>());
        Assert.IsTrue(Run(null!, out _), "no party");
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void GuardThrows_RunsVanillaAndLogsOnce()
    {
        if (!_gameLoaded) Assert.Inconclusive("Game assemblies unavailable.");
        var guard = Substitute.For<IDesertionRecruitGuard>();
        guard.WouldDesertFromParty(Arg.Any<PartyBase?>(), Arg.Any<CharacterObject?>()).Throws(new InvalidOperationException("x"));
        var logger = Substitute.For<IModLogger>();
        Patch150_ConformityTick_Prefix.Initialize(guard, Solo(), logger);
        typeof(Patch150_ConformityTick_Prefix).GetField("_logged", BindingFlags.NonPublic | BindingFlags.Static)!.SetValue(null, false);

        Assert.IsTrue(Run(AnyParty(), out _));
        Assert.IsTrue(Run(AnyParty(), out _));
        logger.Received(1).LogError(Arg.Any<string>());
    }

    [TestMethod]
    [TestCategory("RequiresGameIL")]
    [TestCategory("BindingVerification")]
    public void Target_ResolvesWithTheBoolReturnAndParameterNames_InInstalledEngine()
    {
        if (!_gameLoaded) Assert.Inconclusive("Game assemblies unavailable.");

        var target = Patch150_ConformityTick_Prefix.TargetMethod() as MethodInfo;

        Assert.IsNotNull(target, "RecruitPrisonersCampaignBehavior." + TargetName + "(MobileParty, CharacterObject, int) did not resolve.");
        Assert.AreEqual(typeof(bool), target!.ReturnType);
        // Harmony binds prefix parameters by name.
        CollectionAssert.AreEqual(new[] { "mobileParty", "troop", "hours" }, target.GetParameters().Select(p => p.Name).ToArray());
    }

    [TestMethod]
    [TestCategory("RequiresGameIL")]
    [TestCategory("BindingVerification")]
    public void Target_AlwaysReturnsTrue_InInstalledEngine()
    {
        if (!_gameLoaded) Assert.Inconclusive("Game assemblies unavailable.");

        var il = PatchProcessor.GetOriginalInstructions(Patch150_ConformityTick_Prefix.TargetMethod()).ToList();
        var ret = il.FindIndex(ci => ci.opcode == System.Reflection.Emit.OpCodes.Ret);

        Assert.AreEqual(1, il.Count(ci => ci.opcode == System.Reflection.Emit.OpCodes.Ret), "one return");
        Assert.AreEqual(System.Reflection.Emit.OpCodes.Ldc_I4_1, il[ret - 1].opcode,
            "vanilla no longer returns a constant true; recheck whether a blocked stack still ends the loop");
    }

    [TestMethod]
    [TestCategory("RequiresGameIL")]
    [TestCategory("BindingVerification")]
    public void BothPrisonerTicks_StillCallTheTarget_InInstalledEngine()
    {
        if (!_gameLoaded) Assert.Inconclusive("Game assemblies unavailable.");

        foreach (var (name, args) in new[]
                 {
                     ("HourlyTickMainParty", Type.EmptyTypes),
                     ("DailyTickAIMobileParty", new[] { typeof(MobileParty) }),
                 })
        {
            var tick = AccessTools.Method(typeof(RecruitPrisonersCampaignBehavior), name, args);
            Assert.IsNotNull(tick, "RecruitPrisonersCampaignBehavior." + name + " did not resolve.");
            var calls = PatchProcessor.GetOriginalInstructions(tick!).Count(ci => ci.operand is MethodInfo mi && mi.Name == TargetName);
            Assert.AreEqual(1, calls, name + " no longer calls " + TargetName + " once; the #1189 prefix may be inert.");
        }
    }

    [TestMethod]
    [TestCategory("RequiresGameIL")]
    [TestCategory("BindingVerification")]
    public void PatchCategory_AppliedToTheInstalledEngine_AttachesThisPrefixAndTheJitAccepts()
    {
        if (!_gameLoaded) Assert.Inconclusive("Game assemblies unavailable.");
        const string id = "taom.tests.alignmentdesertion.jit";
        var target = Patch150_ConformityTick_Prefix.TargetMethod();
        var harmony = new Harmony(id);
        try
        {
            PatchCategoryIndex.Build(typeof(AlignmentDesertionModule).Assembly).Apply(harmony, AlignmentDesertionModule.PatchCategory);

            var info = Harmony.GetPatchInfo(target);
            Assert.IsNotNull(info, "applying the category patched nothing");
            var prefixes = info!.Prefixes.Where(p => p.owner == id).ToList();
            Assert.AreEqual(1, prefixes.Count, "exactly one prefix is attached");
            Assert.AreEqual(typeof(Patch150_ConformityTick_Prefix), prefixes[0].PatchMethod.DeclaringType);
            Assert.AreEqual(0, info.Postfixes.Count(p => p.owner == id) + info.Finalizers.Count(p => p.owner == id));
            RuntimeHelpers.PrepareMethod(target.MethodHandle);
        }
        finally
        {
            harmony.UnpatchAll(id);
        }
    }

    [TestMethod]
    public void FeatureModules_ListTheModuleOnce_AtGameInit_WithNoBehaviorModelOrSaveData()
    {
        Assert.AreEqual(1, FeatureModules.All.OfType<AlignmentDesertionModule>().Count());
        var module = new AlignmentDesertionModule();
        Assert.AreEqual("AlignmentDesertion", module.Id);
        Assert.IsNull(module.ParkedReason);
        Assert.IsFalse(module.OwnsSaveData);
        CollectionAssert.AreEqual(
            new[] { AlignmentDesertionModule.PatchCategory, AlignmentDesertionModule.HintPatchCategory, AlignmentDesertionModule.MercenaryPatchCategory },
            module.PatchCategories.Select(c => c.Category).ToArray());
        Assert.IsTrue(module.PatchCategories.All(c => c.Phase == ApplyPhase.GameInit));
        Assert.AreEqual(0, module.CampaignBehaviors.Count + module.GameModels.Count + module.MissionBehaviors.Count);
    }
}
