using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Dependencies.Foundation;
using TAOM.Features.CrashReport.Collectors;
using TAOM.Features.CrashReport.Domain;

namespace TAOM.Tests.Features.CrashReport;

[TestClass]
public class CrashSignatureCalculatorTests
{
    [TestMethod]
    public void Compute_SameInputs_ReturnsSameSignature()
    {
        var stack = MakeStack("A", "B", "C");

        var s1 = CrashSignatureCalculator.Compute("System.Exception", "Foo.Bar", stack);
        var s2 = CrashSignatureCalculator.Compute("System.Exception", "Foo.Bar", stack);

        Assert.AreEqual(s1, s2);
    }

    [TestMethod]
    public void Compute_DifferentExceptionType_ReturnsDifferentSignature()
    {
        var stack = MakeStack("A", "B");

        var s1 = CrashSignatureCalculator.Compute("System.Exception", "Foo.Bar", stack);
        var s2 = CrashSignatureCalculator.Compute("System.NullReferenceException", "Foo.Bar", stack);

        Assert.AreNotEqual(s1, s2);
    }

    [TestMethod]
    public void Compute_DifferentOriginatingTarget_ReturnsDifferentSignature()
    {
        var stack = MakeStack("A", "B");

        var s1 = CrashSignatureCalculator.Compute("System.Exception", "Foo.Bar", stack);
        var s2 = CrashSignatureCalculator.Compute("System.Exception", "Other.Method", stack);

        Assert.AreNotEqual(s1, s2);
    }

    [TestMethod]
    public void Compute_OnlyTopFiveFramesMatter_ChangesBelowDepthIgnored()
    {
        var stackA = MakeStack("A", "B", "C", "D", "E", "X-different-but-deep");
        var stackB = MakeStack("A", "B", "C", "D", "E", "Y-different-but-deep");

        var sA = CrashSignatureCalculator.Compute("Ex", "Origin", stackA);
        var sB = CrashSignatureCalculator.Compute("Ex", "Origin", stackB);

        Assert.AreEqual(sA, sB, "frames beyond index 4 should not contribute to signature");
    }

    // ---- The inner chain (issue #552) ------------------------------------------------------

    [TestMethod]
    public void Compute_SameOuterDifferentInner_ReturnsDifferentSignature()
    {
        // THE BUG, from crash bundle 31942985. Every crash dispatched through the Gauntlet UI
        // arrives as TargetInvocationException @ ScreenManager.Update over the same eight frames, so
        // the outer identity is a constant. Two clicks in one broken menu produced a
        // NullReferenceException and then an IndexOutOfRangeException; the throttle suppressed the
        // second as a duplicate, and it was the one that named the state corruption behind the CTD.
        var stack = MakeStack("ViewModel.ExecuteCommand", "GauntletView.OnCommand", "ScreenManager.Update");

        var s1 = CrashSignatureCalculator.Compute(
            new TargetInvocationException(new NullReferenceException()), "ScreenManager.Update", stack);
        var s2 = CrashSignatureCalculator.Compute(
            new TargetInvocationException(new IndexOutOfRangeException()), "ScreenManager.Update", stack);

        Assert.AreNotEqual(s1, s2, "two different inner exceptions must not share a crash signature");
    }

    [TestMethod]
    public void Compute_SameOuterSameInner_StillDeduplicates()
    {
        // The throttle exists to stop a per-frame crash re-zipping an ever-growing debug log. That
        // must keep working: identical crashes still collapse to one bundle.
        var stack = MakeStack("A", "B");

        var s1 = CrashSignatureCalculator.Compute(
            new TargetInvocationException(new NullReferenceException()), "Foo.Bar", stack);
        var s2 = CrashSignatureCalculator.Compute(
            new TargetInvocationException(new NullReferenceException()), "Foo.Bar", stack);

        Assert.AreEqual(s1, s2);
    }

    [TestMethod]
    public void Compute_ExceptionWithNoInner_MatchesThePlainTypeSignature()
    {
        // Signatures already in the wild belong to exceptions with no inner chain. Keeping those
        // stable means an old bundle's id still refers to the same crash.
        var stack = MakeStack("A", "B");

        Assert.AreEqual(
            CrashSignatureCalculator.Compute("System.NullReferenceException", "Foo.Bar", stack),
            CrashSignatureCalculator.Compute(new NullReferenceException(), "Foo.Bar", stack));
    }

    [TestMethod]
    public void Compute_NullException_IsStillDeterministic()
    {
        var stack = MakeStack("A");

        Assert.AreEqual(
            CrashSignatureCalculator.Compute((Exception)null, "Foo.Bar", stack),
            CrashSignatureCalculator.Compute((Exception)null, "Foo.Bar", stack));
    }

    [TestMethod]
    public void Compute_InnerChain_IsDepthCapped()
    {
        // The walk runs inside the crash handler, so it is bounded rather than trusting the chain to
        // be short. Two chains that differ only BELOW the cap hash the same, which is what proves
        // the cap is doing something; the shallow difference above still separates them.
        var stack = MakeStack("A");
        var depth = CrashSignatureCalculator.InnerChainDepth;

        var deepA = Nest(depth + 3, leaf: new NullReferenceException());
        var deepB = Nest(depth + 3, leaf: new IndexOutOfRangeException());

        Assert.AreEqual(
            CrashSignatureCalculator.Compute(deepA, "Foo.Bar", stack),
            CrashSignatureCalculator.Compute(deepB, "Foo.Bar", stack),
            "a difference deeper than the cap should not change the signature");
    }

    // ---- The preserved throw site (PatchShield rethrow, bundle 2d446100) ---------------------

    [TestMethod]
    public void Compute_SameTypeThroughSameFrameDifferentThrowSites_ReturnsDifferentSignatures()
    {
        // THE BUG: frames come from `new StackTrace(ex)`, which only sees the segment after the last
        // rethrow. Every NullReferenceException crossing the shielded MapState.OnTick therefore had
        // the same five frames, and the throttle suppressed occurrences #2 and #3 of 40de8e64
        // without anyone being able to tell whether they were the same bug.
        var stack = MakeStack("MapState.OnTick_Patch2", "GameStateManager.OnTick", "Game.OnTick");

        var s1 = CrashSignatureCalculator.Compute(
            WithThrowSite(new NullReferenceException(), "DefaultHeroCreationModel.GetBornSettlement"), "Module.OnApplicationTick", stack);
        var s2 = CrashSignatureCalculator.Compute(
            WithThrowSite(new NullReferenceException(), "EquipmentHelper.AssignHeroEquipmentFromEquipment"), "Module.OnApplicationTick", stack);

        Assert.AreNotEqual(s1, s2, "two different throw sites must not share a crash signature");
    }

    [TestMethod]
    public void Compute_SameThrowSite_StillDeduplicates()
    {
        var stack = MakeStack("MapState.OnTick_Patch2");

        Assert.AreEqual(
            CrashSignatureCalculator.Compute(WithThrowSite(new NullReferenceException(), "A.B"), "Origin", stack),
            CrashSignatureCalculator.Compute(WithThrowSite(new NullReferenceException(), "A.B"), "Origin", stack));
    }

    [TestMethod]
    public void Compute_TieWrappingInnersWithDifferentThrowSites_ReturnsDifferentSignatures()
    {
        // Every UI crash arrives as a TargetInvocationException over the same frames (#552). When the
        // invoked method is shielded, the inner's TargetSite is the shielded wrapper (constant) and the
        // TIE's own recorded site is the reflection path (constant), so only the INNER's recorded site
        // can tell two real throw sites apart. Deep-review data-flow trace 4.
        var stack = MakeStack("ViewModel.ExecuteCommand_Patch3", "GauntletView.OnCommand", "ScreenManager.Update");

        var s1 = CrashSignatureCalculator.Compute(
            WithThrowSite(new TargetInvocationException(WithThrowSite(new NullReferenceException(), "A.X")), "RuntimeMethodHandle.InvokeMethod"),
            "ScreenManager.Update", stack);
        var s2 = CrashSignatureCalculator.Compute(
            WithThrowSite(new TargetInvocationException(WithThrowSite(new NullReferenceException(), "B.Y")), "RuntimeMethodHandle.InvokeMethod"),
            "ScreenManager.Update", stack);

        Assert.AreNotEqual(s1, s2, "an inner exception's preserved throw site must reach the signature");
    }

    [TestMethod]
    public void DescribeIdentity_ThrowSiteRecorded_IncludesIt()
    {
        StringAssert.Contains(
            CrashSignatureCalculator.DescribeIdentity(WithThrowSite(new NullReferenceException(), "A.B <- C.D")),
            "A.B <- C.D");
    }

    [TestMethod]
    public void DescribeIdentity_ThrowSiteNotAString_IsIgnored()
    {
        // Data is writable by anyone; only the string the preserver writes counts.
        var ex = new NullReferenceException();
        ex.Data[RethrowStackPreserver.ThrowSiteDataKey] = 42;

        Assert.AreEqual(typeof(NullReferenceException).FullName, CrashSignatureCalculator.DescribeIdentity(ex));
    }

    private static Exception WithThrowSite(Exception ex, string site)
    {
        ex.Data[RethrowStackPreserver.ThrowSiteDataKey] = site;
        return ex;
    }

    /// <summary>TargetInvocationException nested <paramref name="levels"/> deep around a leaf.</summary>
    private static Exception Nest(int levels, Exception leaf)
    {
        var current = leaf;
        for (var i = 0; i < levels; i++)
            current = new TargetInvocationException(current);
        return current;
    }

    [TestMethod]
    public void Short_PrefixesTo8Chars()
    {
        var full = "abcdef0123456789";
        Assert.AreEqual("abcdef01", CrashSignatureCalculator.Short(full));
    }

    private static IReadOnlyList<StackFrameSnapshot> MakeStack(params string[] methodNames)
    {
        var list = new List<StackFrameSnapshot>(methodNames.Length);
        for (int i = 0; i < methodNames.Length; i++)
            list.Add(new StackFrameSnapshot(i, methodNames[i], null, null, null, 0, 0));
        return list;
    }
}
