using System;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.BattleLoadDiagnostics;

/// <summary>
/// Source pin for the BattleLoad loading window's only closer in <c>SubModule.OnMissionBehaviorInitialize</c>. Since the
/// mission-start guard (Patch103) wraps that method, a throw anywhere in TAOM's mission wiring (the first mission's patch
/// application included) is survived and the mission starts, so the closer is registered in a <c>finally</c> around all of
/// that work: it always exists, it is registered last, so it ticks first, and a TAOM behaviour that throws in every tick
/// cannot keep the window open (2026-10-08 reviews). A try/finally, not a catch: the throw must still reach the guard.
/// </summary>
[TestClass]
public class BattleLoadCloserWiringTests
{
    private const string Override = "public override void OnMissionBehaviorInitialize(Mission mission)";
    private const string Registrations = "private void AddTaomMissionBehaviors(Mission mission)";
    private const string RegistrationsCall = "AddTaomMissionBehaviors(mission);";
    private const string Prelude = "if (!_missionTimePatchesApplied)";
    private const string FirstFeatureRegistration = "ShaderPrecompileRunner.TryClaimMission(mission)";
    private const string Closer = "new Features.BattleLoadDiagnostics.Hooks.BattleLoadPhaseBehavior(";
    private const string OneCloserCheck = "GetMissionBehavior<Features.BattleLoadDiagnostics.Hooks.BattleLoadPhaseBehavior>() == null";

    private static string Source() => RepoPaths.ReadSource("Main/SubModule.cs", stripComments: true);

    private static string Body(string src, string signature)
    {
        var start = src.IndexOf(signature, StringComparison.Ordinal);
        Assert.IsTrue(start >= 0, signature + " moved or was renamed.");
        var next = Regex.Match(src.Substring(start + signature.Length), @"\n    (?:public|private|protected|internal) ");
        Assert.IsTrue(next.Success, "The member after " + signature + " moved.");
        return src.Substring(start, signature.Length + next.Index);
    }

    private static int Count(string text, string what)
    {
        var n = 0;
        for (var i = text.IndexOf(what, StringComparison.Ordinal); i >= 0; i = text.IndexOf(what, i + 1, StringComparison.Ordinal))
            n++;
        return n;
    }

    private static void AssertCloserIsInAFinallyAroundAllTheWiring(string src)
    {
        Assert.AreEqual(1, Count(src, Closer), "The closer is registered exactly once in SubModule.cs.");

        var wrapper = Body(src, Override);
        var finallies = Regex.Matches(wrapper, @"\bfinally\s*\{");
        Assert.AreEqual(1, finallies.Count, "OnMissionBehaviorInitialize holds exactly one finally.");
        var fin = finallies[0].Index;
        var tryAt = wrapper.LastIndexOf("try", fin, StringComparison.Ordinal);
        var call = wrapper.IndexOf(RegistrationsCall, StringComparison.Ordinal);
        var closer = wrapper.IndexOf(Closer, StringComparison.Ordinal);
        Assert.IsTrue(tryAt >= 0 && tryAt < call && call < fin && fin < closer,
            "Order must be: try, the call to AddTaomMissionBehaviors, finally, the closer.");
        Assert.IsFalse(Regex.IsMatch(wrapper.Substring(tryAt, fin - tryAt), @"\}\s*catch\b"),
            "No catch between the try and the finally: the throw must still reach the mission-start guard.");
        var check = wrapper.IndexOf(OneCloserCheck, StringComparison.Ordinal);
        Assert.IsTrue(fin < check && check < closer,
            "The finally adds the closer only when the mission has none: a reload after a throw reuses the Mission.");

        var registrations = Body(src, Registrations);
        Assert.IsTrue(registrations.Contains(Prelude) && registrations.Contains(FirstFeatureRegistration),
            "The first mission's patch application and the feature registrations run inside AddTaomMissionBehaviors, so inside the try.");
        Assert.IsFalse(wrapper.Contains(Prelude), "No fallible TAOM work may stay outside the try.");
    }

    [TestMethod]
    public void OnMissionBehaviorInitialize_RegistersTheLoadingWindowCloser_InAFinallyAroundAllTheWiring()
    {
        AssertCloserIsInAFinallyAroundAllTheWiring(Source());
    }

    [TestMethod]
    public void Pin_FailsWhenTheFinallyIsGone()
    {
        var src = Source();
        var mutated = Regex.Replace(src, @"\bfinally(\s*\{)", "if (true)$1");
        Assert.AreNotEqual(src, mutated, "The mutation found nothing to change: the finally's shape changed.");
        Assert.ThrowsException<AssertFailedException>(() => AssertCloserIsInAFinallyAroundAllTheWiring(mutated));
    }

    [TestMethod]
    public void Pin_FailsWhenTheOneCloserCheckIsGone()
    {
        var src = Source();
        var mutated = src.Replace(OneCloserCheck, "GetHashCode() != 0");
        Assert.AreNotEqual(src, mutated, "The mutation found nothing to change: the check's shape changed.");
        Assert.ThrowsException<AssertFailedException>(() => AssertCloserIsInAFinallyAroundAllTheWiring(mutated));
    }

    [TestMethod]
    public void Pin_FailsWhenACatchSwallowsTheThrowBeforeTheFinally()
    {
        var src = Source();
        var mutated = Regex.Replace(src, @"\}(\s*)finally(\s*\{)", "}$1catch { }$1finally$2");
        Assert.AreNotEqual(src, mutated, "The mutation found nothing to change: the finally's shape changed.");
        Assert.ThrowsException<AssertFailedException>(() => AssertCloserIsInAFinallyAroundAllTheWiring(mutated));
    }
}
