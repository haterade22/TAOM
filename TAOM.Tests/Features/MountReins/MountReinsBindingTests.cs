using System.Linq;
using System.Reflection;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.Core;
using TAOM.Core.Diagnostics;
using TAOM.Core.Logging;
using TAOM.Features.MountReins;
using TAOM.Features.MountReins.Hooks;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.MountReins;

/// <summary>
/// Proves the reins guard still finds its one site in the installed engine (Patch153). The transpiler is fail-safe, so
/// a vanilla rewrite would otherwise turn the guard off silently and the mounted spawn would crash again with every
/// other gate green.
/// </summary>
[TestClass]
public class MountReinsBindingTests
{
    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    // TAOM.Tests does not reference TaleWorlds.MountAndBlade.View; the engine assembly is loaded at run time.
    private static MethodBase Target() =>
        AccessTools.Method(AccessTools.TypeByName("TaleWorlds.MountAndBlade.View.MountVisualCreator"), "AddMountMesh");

    private static bool Calls(CodeInstruction ci, MethodInfo? m) => m != null && ci.operand is MethodInfo mi && mi == m;

    [TestMethod]
    [TestCategory("RequiresGameIL")]
    [TestCategory("BindingVerification")]
    public void TheTarget_IsStatic_TakesTheMountItemAsArgument1_AndHasOneClothCall()
    {
        if (!_gameLoaded) Assert.Inconclusive("Game assemblies unavailable.");
        var target = Target();
        Assert.IsNotNull(target, "MountVisualCreator.AddMountMesh did not resolve.");
        Assert.IsTrue(target.IsStatic, "ldarg.1 is the mount item only while the method is static");
        var p = target.GetParameters();
        Assert.AreEqual(typeof(ItemObject), p[1].ParameterType);
        Assert.AreEqual("mountItem", p[1].Name);

        var il = PatchProcessor.GetOriginalInstructions(target).ToList();
        Assert.AreEqual(1, il.Count(ci => Calls(ci, MountReinsTranspiler.VanillaCall)),
            "expected exactly one AddHorseReinsClothMesh call in AddMountMesh");
    }

    [TestMethod]
    [TestCategory("RequiresGameIL")]
    [TestCategory("BindingVerification")]
    public void TheTranspiler_SwapsThatCallForTheHook_InTheInstalledEngine()
    {
        if (!_gameLoaded) Assert.Inconclusive("Game assemblies unavailable.");
        var il = PatchProcessor.GetOriginalInstructions(Target()).ToList();
        var logger = Substitute.For<IModLogger>();

        var rewritten = MountReinsTranspiler.SwapReinsCloth(il, logger).ToList();

        logger.DidNotReceive().LogWarning(Arg.Any<string>());
        Assert.AreEqual(1, rewritten.Count(ci => Calls(ci, MountReinsTranspiler.HookCall)));
        Assert.AreEqual(0, rewritten.Count(ci => Calls(ci, MountReinsTranspiler.VanillaCall)));
        Assert.AreEqual(il.Count + 1, rewritten.Count, "one ldarg.1 inserted");
    }

    [TestMethod]
    public void TheHook_ReadsTheReinSkeleton_AndStillCallsTheEngineCloth()
    {
        // Call presence only: it proves neither call was deleted, not the branch between them (rule tests cover that).
        if (!_gameLoaded) Assert.Inconclusive("Game assemblies unavailable.");
        var il = PatchProcessor.GetOriginalInstructions(MountReinsTranspiler.HookCall).ToList();
        var reinSkeleton = AccessTools.PropertyGetter(typeof(Monster), nameof(Monster.ReinSkeleton));
        var rule = AccessTools.Method(typeof(MountReinsRule), nameof(MountReinsRule.CanBuildReinsCloth));

        Assert.AreEqual(1, il.Count(ci => Calls(ci, reinSkeleton)), "reads Monster.ReinSkeleton");
        Assert.AreEqual(1, il.Count(ci => Calls(ci, rule)), "asks the rule");
        Assert.AreEqual(1, il.Count(ci => Calls(ci, MountReinsTranspiler.VanillaCall)), "builds the cloth when allowed");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void PatchClass_AppliedToTheInstalledEngine_BuildsAReplacementTheJitAccepts()
    {
        if (!_gameLoaded) Assert.Inconclusive("Game assemblies unavailable.");
        const string id = "taom.tests.mountreins.jit";
        var target = Target();
        var harmony = new Harmony(id);
        try
        {
            PatchCategoryIndex.Build(typeof(MountReinsModule).Assembly).Apply(harmony, MountReinsModule.PatchCategory);

            var info = Harmony.GetPatchInfo(target);
            Assert.IsNotNull(info, "applying the category patched nothing: the class is not in it");
            Assert.IsTrue(info!.Transpilers.Any(p => p.owner == id), "the transpiler is attached");
            System.Runtime.CompilerServices.RuntimeHelpers.PrepareMethod(target.MethodHandle);
        }
        finally
        {
            harmony.UnpatchAll(id);
        }
    }
}
