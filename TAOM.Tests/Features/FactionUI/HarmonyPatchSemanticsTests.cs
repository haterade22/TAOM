using System;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Features.FactionUI;

/// <summary>
/// The two Harmony facts the themed movie fallback under BannerlordCoop rests on (#1545):
/// <c>GauntletMovieBuildWatchPatch</c>'s postfix is skipped when the engine's build throws, even when another
/// mod's finalizer swallows the exception; and <c>GauntletLayerLoadMoviePatch</c>'s finalizer catches an
/// exception its own postfix throws and can replace the result.
/// </summary>
[TestClass]
public class HarmonyPatchSemanticsTests
{
    private static Exception? _seen;
    private static bool _postfixRan;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int Target() => 1;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowingTarget() => throw new InvalidOperationException("prefab failed");

    private static void ThrowingPostfix() => throw new InvalidOperationException("built no root");

    private static void RecordingPostfix() => _postfixRan = true;

    private static Exception? Finalizer(Exception? __exception, ref int __result)
    {
        _seen = __exception;
        __result = 2;
        return null;
    }

    private static Exception? SwallowingFinalizer() => null;

    [TestMethod]
    public void Finalizer_SeesAnExceptionThrownByAPostfix_AndCanReplaceTheResult()
    {
        var harmony = new Harmony("taom.tests.finalizer-scope");
        try
        {
            harmony.Patch(
                AccessTools.Method(typeof(HarmonyPatchSemanticsTests), nameof(Target)),
                postfix: new HarmonyMethod(typeof(HarmonyPatchSemanticsTests), nameof(ThrowingPostfix)),
                finalizer: new HarmonyMethod(typeof(HarmonyPatchSemanticsTests), nameof(Finalizer)));

            Assert.AreEqual(2, Target());
            Assert.IsInstanceOfType(_seen, typeof(InvalidOperationException));
        }
        finally
        {
            harmony.UnpatchAll("taom.tests.finalizer-scope");
        }
    }

    [TestMethod]
    public void Postfix_IsSkippedWhenTheOriginalThrows_EvenWhenAnotherModsFinalizerSwallowsIt()
    {
        var ours = new Harmony("taom.tests.build-watch");
        var theirs = new Harmony("taom.tests.swallowing-mod");
        var original = AccessTools.Method(typeof(HarmonyPatchSemanticsTests), nameof(ThrowingTarget));
        _postfixRan = false;
        try
        {
            theirs.Patch(original, finalizer: new HarmonyMethod(typeof(HarmonyPatchSemanticsTests), nameof(SwallowingFinalizer)));
            ours.Patch(original, postfix: new HarmonyMethod(typeof(HarmonyPatchSemanticsTests), nameof(RecordingPostfix)));

            ThrowingTarget();

            Assert.IsFalse(_postfixRan);
        }
        finally
        {
            ours.UnpatchAll("taom.tests.build-watch");
            theirs.UnpatchAll("taom.tests.swallowing-mod");
        }
    }
}
