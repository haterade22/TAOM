using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Migration;

/// <summary>
/// The binding gate's own logic, with no game. Each fixture is a patch class Lib.Harmony 2.4.2 refuses (or
/// accepts) when it applies it. Nothing is patched: Harmony only reads the fixtures' attributes and metadata.
/// </summary>
[TestClass]
public class HarmonyTargetResolverTests
{
    private class FixtureBase
    {
        public void InheritedOnly() { }
    }

    private class FixtureTarget : FixtureBase
    {
        private int _count;

        public int Count(int amount) => amount + _count;

        public void Act(int amount) => _count += amount;

        public void Over() { }

        public void Over(int value) => _count = value;
    }

    private abstract class FixtureAbstract
    {
        public abstract void Run();
    }

    [HarmonyPatch(typeof(FixtureTarget), nameof(FixtureBase.InheritedOnly))]
    private static class InheritedOnlyPatch { public static void Prefix() { } }

    [HarmonyPatch(typeof(FixtureTarget), nameof(FixtureTarget.Over))]
    private static class AmbiguousPatch { public static void Prefix() { } }

    [HarmonyPatch(typeof(FixtureTarget))]
    private static class PerMethodPatch
    {
        [HarmonyPatch(nameof(FixtureTarget.Act))]
        public static void Prefix() { }

        [HarmonyPatch("Missing")]
        public static void Postfix() { }
    }

    [HarmonyPatch(typeof(FixtureTarget), nameof(FixtureTarget.Act))]
    private static class WrongNamePatch { public static void Prefix(int notThere) { } }

    [HarmonyPatch(typeof(FixtureTarget), nameof(FixtureTarget.Act))]
    private static class MissingFieldPatch { public static void Prefix(int ___nope) { } }

    [HarmonyPatch(typeof(FixtureTarget), nameof(FixtureTarget.Act))]
    private static class ResultOnVoidPatch { public static void Postfix(object __result) { } }

    [HarmonyPatch(typeof(FixtureTarget), nameof(FixtureTarget.Count))]
    private static class ResultTypePatch { public static void Postfix(string __result) { } }

    [HarmonyPatch(typeof(FixtureTarget), nameof(FixtureTarget.Act))]
    private static class BadIndexPatch { public static void Prefix(object __3) { } }

    [HarmonyPatch(typeof(FixtureTarget), nameof(FixtureTarget.Count))]
    private static class GoodPatch
    {
        public static void Prefix(object __instance, int amount, ref bool __state, int ____count, object __0,
            bool __runOriginal, MethodBase __originalMethod) { }

        public static void Postfix(ref int __result, bool __state) { }

        public static Exception? Finalizer(Exception? __exception) => __exception;
    }

    [HarmonyPatch(typeof(FixtureTarget), nameof(FixtureTarget.Count))]
    private static class PassThroughPostfixPatch { public static int Postfix(int result) => result; }

    [HarmonyPatch(typeof(FixtureAbstract), nameof(FixtureAbstract.Run))]
    private static class AbstractTargetPatch { public static void Prefix() { } }

    private static class TargetMethodNullPatch
    {
        public static MethodBase? TargetMethod() => null;

        public static void Prefix() { }
    }

    private static class TargetMethodsWithNullPatch
    {
        public static IEnumerable<MethodBase?> TargetMethods()
        {
            yield return AccessTools.Method(typeof(FixtureTarget), nameof(FixtureTarget.Act));
            yield return null;
        }

        public static void Prefix() { }
    }

    private static class BulkWithAnnotationPatch
    {
        public static MethodBase TargetMethod() => AccessTools.Method(typeof(FixtureTarget), nameof(FixtureTarget.Act));

        [HarmonyPatch(nameof(FixtureTarget.Act))]
        public static void Prefix() { }
    }

    [DllImport("kernel32.dll")]
    private static extern uint GetTickCount();

    private static void AssertOneProblem(Type patchClass, string expected)
    {
        var problems = HarmonyTargetResolver.Check(patchClass);
        Assert.AreEqual(1, problems.Count, string.Join("\n", problems));
        StringAssert.Contains(problems[0], expected);
    }

    [TestMethod]
    public void Check_TargetDeclaredOnlyOnABaseType_IsUndefined_ThoughAccessToolsFindsIt()
    {
        Assert.IsNotNull(AccessTools.Method(typeof(FixtureTarget), nameof(FixtureBase.InheritedOnly)), "the old gate's lookup");
        AssertOneProblem(typeof(InheritedOnlyPatch), "Undefined target method");
    }

    [TestMethod]
    public void Check_NameOnlyTargetWithTwoDeclaredOverloads_IsAmbiguous_ThoughAccessToolsPicksOne()
    {
        Assert.AreEqual(0, AccessTools.Method(typeof(FixtureTarget), nameof(FixtureTarget.Over)).GetParameters().Length, "the old gate's lookup");
        AssertOneProblem(typeof(AmbiguousPatch), "Ambiguous match");
    }

    [TestMethod]
    public void Check_EachPatchMethodIsResolvedAgainstItsOwnTarget() => AssertOneProblem(typeof(PerMethodPatch), "Postfix");

    [TestMethod]
    public void Check_ParameterNamedLikeNoOriginalParameter_IsReported() => AssertOneProblem(typeof(WrongNamePatch), "\"notThere\" not found");

    [TestMethod]
    public void Check_FieldInjectionOfAMissingField_IsReported() => AssertOneProblem(typeof(MissingFieldPatch), "'nope'");

    [TestMethod]
    public void Check_ResultOnAVoidOriginal_IsReported() => AssertOneProblem(typeof(ResultOnVoidPatch), "Cannot get result from void method");

    [TestMethod]
    public void Check_ResultOfAnUnassignableType_IsReported() => AssertOneProblem(typeof(ResultTypePatch), "cannot assign");

    [TestMethod]
    public void Check_IndexPastTheOriginalsParameters_IsReported() => AssertOneProblem(typeof(BadIndexPatch), "no parameter at index 3");

    [TestMethod]
    public void Check_InjectedNamesMatchingNamesFieldsAndIndexes_Bind()
    {
        var problems = HarmonyTargetResolver.Check(typeof(GoodPatch));
        Assert.AreEqual(0, problems.Count, string.Join("\n", problems));
    }

    [TestMethod]
    public void Check_PassThroughPostfixFirstParameter_IsNotReadAsAName()
    {
        var problems = HarmonyTargetResolver.Check(typeof(PassThroughPostfixPatch));
        Assert.AreEqual(0, problems.Count, string.Join("\n", problems));
    }

    [TestMethod]
    public void Check_AbstractTarget_HasNoBody() => AssertOneProblem(typeof(AbstractTargetPatch), "has no body");

    [TestMethod]
    public void HasBody_ConcreteYes_AbstractAndExternNo()
    {
        Assert.IsTrue(HarmonyTargetResolver.HasBody(AccessTools.Method(typeof(FixtureTarget), nameof(FixtureTarget.Act))));
        Assert.IsFalse(HarmonyTargetResolver.HasBody(AccessTools.Method(typeof(FixtureAbstract), nameof(FixtureAbstract.Run))));
        Assert.IsFalse(HarmonyTargetResolver.HasBody(AccessTools.Method(typeof(HarmonyTargetResolverTests), nameof(GetTickCount))));
    }

    [TestMethod]
    public void Check_TargetMethodReturningNull_IsReported() => AssertOneProblem(typeof(TargetMethodNullPatch), "TargetMethod() returned null");

    [TestMethod]
    public void Check_TargetMethodsWithANullEntry_IsReported() => AssertOneProblem(typeof(TargetMethodsWithNullPatch), "null entry");

    [TestMethod]
    public void Check_TargetMethodWithATargetAnnotation_IsRefused() => AssertOneProblem(typeof(BulkWithAnnotationPatch), "refuses");

    [TestMethod]
    public void ResolveOriginals_ReturnsOnlyTheTargetsThatResolve()
    {
        var problems = new List<string>();
        var originals = HarmonyTargetResolver.ResolveOriginals(typeof(PerMethodPatch), problems);
        Assert.AreEqual(1, originals.Count);
        Assert.AreEqual(nameof(FixtureTarget.Act), originals[0].Name);
        Assert.AreEqual(1, problems.Count, string.Join("\n", problems));
    }

    [TestMethod]
    public void HarmonyInternals_StillWhereTheResolverReadsThem()
    {
        Assert.AreEqual(new Version(2, 4, 2, 0), typeof(Harmony).Assembly.GetName().Version,
            "Lib.Harmony moved off 2.4.2: re-read PatchClassProcessor, AttributePatch, PatchTools.GetOriginalMethod and "
            + "MethodCreatorTools.EmitCallParameter in the new version, update HarmonyTargetResolver, then this pin.");
        Assert.IsNotNull(HarmonyTargetResolver.PatchMethodsField, "PatchClassProcessor.patchMethods");
        Assert.IsNotNull(HarmonyTargetResolver.AuxilaryMethodsField, "PatchClassProcessor.auxilaryMethods");
        Assert.IsNotNull(HarmonyTargetResolver.AttributePatchType, "HarmonyLib.AttributePatch");
        Assert.AreEqual(typeof(HarmonyMethod), HarmonyTargetResolver.AttributePatchInfoField?.FieldType, "AttributePatch.info");
        Assert.AreEqual(typeof(HarmonyPatchType?), HarmonyTargetResolver.AttributePatchKindField?.FieldType, "AttributePatch.type");
        Assert.AreEqual(typeof(MethodBase), HarmonyTargetResolver.GetOriginalMethod?.ReturnType, "PatchTools.GetOriginalMethod");
    }
}
