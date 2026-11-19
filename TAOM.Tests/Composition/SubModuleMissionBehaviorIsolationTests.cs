using System;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Composition;

/// <summary>
/// #785: every hand-wired registration in <c>SubModule.AddTaomMissionBehaviors</c> builds its behavior inside a
/// factory that <c>AddTaomBehavior</c> runs through <c>MissionBehaviorBuild.Isolated</c>, so a throw while building
/// one behavior drops only that behavior and is reported to the mission-start guard. Wiring pins only: the isolation
/// itself is MissionBehaviorBuildTests.
/// </summary>
[TestClass]
public class SubModuleMissionBehaviorIsolationTests
{
    private const string Registrations = "private void AddTaomMissionBehaviors(Mission mission)";
    private const string FactoryAdd = "void AddTaomBehavior<T>(System.Func<T?> create) where T : MissionBehavior";
    private static readonly Regex Call = new(@"\bAddTaomBehavior\s*\(");

    private static string Source() => RepoPaths.ReadSource("Main/SubModule.cs", stripComments: true);

    private static string Body(string src, string signature)
    {
        var start = src.IndexOf(signature, StringComparison.Ordinal);
        Assert.IsTrue(start >= 0, signature + " moved or was renamed.");
        var next = Regex.Match(src.Substring(start + signature.Length), @"\n    (?:public|private|protected|internal) ");
        Assert.IsTrue(next.Success, "The member after " + signature + " moved.");
        return src.Substring(start, signature.Length + next.Index);
    }

    // The index of the bracket that closes the one at openAt. Comments are already blanked, and no string literal
    // in the pinned code holds a bracket.
    private static int Close(string text, int openAt, char open, char close)
    {
        var depth = 0;
        for (var i = openAt; i < text.Length; i++)
        {
            if (text[i] == open) depth++;
            else if (text[i] == close && --depth == 0) return i;
        }

        Assert.Fail("Unbalanced " + open + close + " after index " + openAt + ".");
        return -1;
    }

    private static void AssertEveryCallPassesAFactory(string src)
    {
        var body = Body(src, Registrations);
        var calls = Call.Matches(body);
        Assert.IsTrue(calls.Count > 0, "No AddTaomBehavior call found: the pin inspected nothing.");
        foreach (Match call in calls)
            Assert.IsTrue(Regex.IsMatch(body.Substring(call.Index + call.Length), @"^\s*\(\)\s*=>"),
                "Every AddTaomBehavior call passes a factory lambda; found: "
                + body.Substring(call.Index, Math.Min(90, body.Length - call.Index)));
    }

    private static void AssertNothingIsBuiltOutsideAFactory(string src)
    {
        var body = Body(src, Registrations);
        var calls = Call.Matches(body);
        Assert.IsTrue(calls.Count > 0, "No AddTaomBehavior call found: the pin inspected nothing.");
        var end = body.IndexOf("LogTaomBehaviorsDone", StringComparison.Ordinal);
        Assert.IsTrue(end > calls[calls.Count - 1].Index, "LogTaomBehaviorsDone moved before the last registration.");

        // Everything from the first registration to the closing stamp, minus the registrations themselves.
        var residue = new StringBuilder();
        var at = calls[0].Index;
        foreach (Match call in calls)
        {
            if (call.Index < at) continue;
            residue.Append(body, at, call.Index - at);
            at = Close(body, call.Index + call.Length - 1, '(', ')') + 1;
        }

        residue.Append(body, at, end - at);
        var text = residue.ToString();
        Assert.IsFalse(text.Contains("IoC.Resolve<"), "A service is resolved between registrations, outside a factory: " + text);
        Assert.IsFalse(Regex.IsMatch(text, @"\bnew\s"), "Something is built between registrations, outside a factory: " + text);
    }

    private static void AssertTheBuildIsIsolatedAndTheAddIsNot(string src)
    {
        var body = Body(src, Registrations);
        var start = body.IndexOf(FactoryAdd, StringComparison.Ordinal);
        Assert.IsTrue(start >= 0, "The factory form of AddTaomBehavior is gone: " + FactoryAdd);
        var open = body.IndexOf('{', start);
        var local = body.Substring(open, Close(body, open, '{', '}') - open + 1);

        var isolated = local.IndexOf("MissionBehaviorBuild.Isolated(create,", StringComparison.Ordinal);
        Assert.IsTrue(isolated >= 0, "AddTaomBehavior builds through MissionBehaviorBuild.Isolated.");
        var isolatedEnd = Close(local, local.IndexOf('(', isolated), '(', ')');

        var report = local.IndexOf("IMissionStartGuardService>()?.Report(", StringComparison.Ordinal);
        Assert.IsTrue(report > isolated && report < isolatedEnd,
            "A failed build is reported to the mission-start guard from inside the isolated call, so the player still sees its notice.");
        Assert.IsTrue(local.IndexOf("StartCall.OnMissionBehaviorInitialize", StringComparison.Ordinal) > report,
            "The guard report names the OnMissionBehaviorInitialize start call, as the guard's own catch does.");

        var add = local.IndexOf("AddBuiltTaomBehavior(behavior)", StringComparison.Ordinal);
        Assert.IsTrue(add > isolatedEnd,
            "The engine add runs after the isolated build, outside it: a throw from Mission.AddMissionBehavior must still reach the mission-start guard.");
    }

    [TestMethod]
    public void AddTaomMissionBehaviors_EveryAddTaomBehaviorCall_PassesAFactory()
    {
        AssertEveryCallPassesAFactory(Source());
    }

    [TestMethod]
    public void AddTaomMissionBehaviors_BetweenRegistrations_NothingIsResolvedOrBuilt()
    {
        AssertNothingIsBuiltOutsideAFactory(Source());
    }

    [TestMethod]
    public void AddTaomBehavior_BuildsInIsolation_ReportsToTheGuard_AndAddsToTheMissionOutsideIt()
    {
        AssertTheBuildIsIsolatedAndTheAddIsNot(Source());
    }

    [TestMethod]
    public void Pin_FailsWhenACallPassesABuiltBehavior()
    {
        var src = Source();
        var mutated = src.Replace("AddTaomBehavior(() => new BehaviorTreeMissionLogic());", "AddTaomBehavior(new BehaviorTreeMissionLogic());");
        Assert.AreNotEqual(src, mutated, "The mutation found nothing to change: the registration's shape changed.");
        Assert.ThrowsException<AssertFailedException>(() => AssertEveryCallPassesAFactory(mutated));
    }

    [TestMethod]
    public void Pin_FailsWhenAServiceIsResolvedBetweenRegistrations()
    {
        var src = Source();
        const string anchor = "AddTaomBehavior(() => new AdvancedCombatBehavior());";
        var mutated = src.Replace(anchor, "var early = IoC.Resolve<IModLogger>(); " + anchor);
        Assert.AreNotEqual(src, mutated, "The mutation found nothing to change: the registration's shape changed.");
        Assert.ThrowsException<AssertFailedException>(() => AssertNothingIsBuiltOutsideAFactory(mutated));
    }
}
