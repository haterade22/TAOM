using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.SeatOfSeeing;

/// <summary>
/// The field trial's wiring (build-spec section 12), as source-text gates: the view and the client submodule are engine entry points that
/// need a live game. What they pin is that an unarmed session pays one null check a tick, that the real key and the trial's press take
/// one path, and that the environment variable is read once.
/// </summary>
[TestClass]
public class CommandTrialWiringTests
{
    private const string TrialFolder = "Client/Features/SeatOfSeeing/Trial/";

    private static string View() => RepoPaths.ReadSource("Client/Features/SeatOfSeeing/SeatView.cs", stripComments: true);

    private static string Member(string signature)
    {
        var code = View();
        var start = code.IndexOf(signature, StringComparison.Ordinal);
        Assert.IsTrue(start >= 0, "SeatView has no " + signature);
        var next = code.IndexOf("public override", start + signature.Length, StringComparison.Ordinal);
        return Regex.Replace(next < 0 ? code.Substring(start) : code.Substring(start, next - start), @"\s+", " ");
    }

    private static string[] TrialFiles() =>
        Directory.GetFiles(RepoPaths.RepoPath("Client", "Features", "SeatOfSeeing", "Trial"), "*.cs");

    private static string TrialSource(string pattern) =>
        string.Join("\n", TrialFiles()
            .Where(f => Regex.IsMatch(Path.GetFileName(f), pattern))
            .Select(f => RepoPaths.ReadSource(TrialFolder + Path.GetFileName(f), stripComments: true)));

    // ---- The view ----

    [TestMethod]
    public void TheRealKey_AndTheTrial_TakeOnePath_ThroughPressSeatKey()
    {
        var tick = Member("public override void OnMissionScreenTick(float dt)");

        // The real key test calls PressSeatKey, and the session is driven by the flag it sets: nothing else feeds OnScreenTick.
        StringAssert.Contains(tick, "key != InputKey.Invalid && TaleWorlds.InputSystem.Input.IsKeyPressed(key) && Input.IsKeyPressed(key)");
        StringAssert.Contains(tick, "PressSeatKey();");
        StringAssert.Contains(tick, "_session.OnScreenTick(pressed);");
        Assert.AreEqual(1, Regex.Matches(View(), @"_session\.OnScreenTick\(").Count, "the session has one caller");
    }

    [TestMethod]
    public void ThePressFlag_IsClearedInTheScreenTick_SoOneRealPressIsOneToggle()
    {
        var tick = Member("public override void OnMissionScreenTick(float dt)");

        StringAssert.Contains(tick, "var pressed = _seatKeyPressed; _seatKeyPressed = false; _session.OnScreenTick(pressed);");
    }

    [TestMethod]
    public void TheView_FliesOnTheTrialInput_WhenItIsSet()
    {
        StringAssert.Contains(Member("public override bool UpdateOverridenCamera(float dt)"), "_session != null && _session.OnFrame(dt, TrialInput)");
    }

    [TestMethod]
    public void AnUnarmedSession_PaysOneNullCheckPerTickInTheView()
    {
        StringAssert.Contains(Member("public override void OnMissionTick(float dt)"), "base.OnMissionTick(dt); _session?.OnMissionTick(); _trial?.OnMissionTick();");
        StringAssert.Contains(Member("public override void OnMissionScreenTick(float dt)"), "_trial?.OnScreenTick();");
        Assert.AreEqual(1, Regex.Matches(View(), @"private CommandTrialScript\? _trial;").Count, "one nullable field");
    }

    [TestMethod]
    public void TheScript_IsBuiltOnlyWhenArmed_AndBothTeardownPathsDropIt()
    {
        var init = Member("public override void OnMissionScreenInitialize()");
        StringAssert.Contains(init, "_trial = CommandTrialRun.Script(");
        StringAssert.Contains(Member("private void EndSession()"), "_trial = null;");
    }

    [TestMethod]
    public void TheHost_GetsTheSessionAndTheViewsOneScreenAdapter_NotASecondAdapter()
    {
        var init = Member("public override void OnMissionScreenInitialize()");

        Assert.AreEqual(1, Regex.Matches(View(), @"new SeatScreenAdapter\(").Count, "one adapter per mission");
        StringAssert.Contains(init, "new SeatSession(screen,");
        StringAssert.Contains(init, "new CommandTrialHost(this, session, screen, mission, MissionScreen)");
        Assert.AreEqual(0, Regex.Matches(TrialSource("^CommandTrialHost"), @"new SeatScreenAdapter\(").Count, "the host builds none");
    }

    [TestMethod]
    public void AnArmedRun_AnswersNoToTheEscapeMenuOnFocusChange_AndAnUnarmedOneAsksTheBase()
    {
        StringAssert.Contains(View(), "IsOpeningEscapeMenuOnFocusChangeAllowed() => !CommandTrialRun.IsArmed && base.IsOpeningEscapeMenuOnFocusChangeAllowed();");
    }

    // ---- The submodule ----

    [TestMethod]
    public void TheClientSubModule_HoldsOneField_ArmsItOnTheFirstTick_AndTicksItOnce()
    {
        var code = RepoPaths.ReadSource("Client/ClientSubModule.cs", stripComments: true);
        var tick = BodyOf(code, "protected override void OnApplicationTick");

        Assert.AreEqual(1, Regex.Matches(code, @"CommandTrialAutostart\? _commandTrial;").Count);
        Assert.AreEqual(1, Regex.Matches(tick, @"_commandTrial = CommandTrialAutostart\.Armed\(").Count);
        Assert.AreEqual(1, Regex.Matches(tick, @"_commandTrial\?\.Tick\(dt\);").Count);
        StringAssert.Contains(BodyOf(tick, "if (!_autorunResolved)"), "_commandTrial = CommandTrialAutostart.Armed(", "armed in the same first-tick block as the other drivers");
    }

    // ---- The environment variable ----

    [TestMethod]
    public void TheEnvironmentVariable_IsReadInOnePlaceOnly()
    {
        var reads = RepoPaths.CSharpFiles("Main", "Client")
            .Where(f => File.ReadAllText(f).Contains("TAOM_COMMAND_TRIAL") || File.ReadAllText(f).Contains("CommandTrialOptions.EnvVar"))
            .Select(f => Path.GetFileName(f))
            .OrderBy(n => n)
            .ToList();

        CollectionAssert.AreEqual(new[] { "CommandTrialOptions.cs" }, reads, string.Join(",", reads));
        var trial = string.Join("\n", TrialFiles().Select(File.ReadAllText));
        Assert.AreEqual(1, Regex.Matches(trial, @"GetEnvironmentVariable\(").Count, "read once, by the options");
        Assert.IsFalse(View().Contains("GetEnvironmentVariable"), "the view asks the run, not the environment");
    }

    // ---- The trial folder ----

    [TestMethod]
    public void TheTrial_DeclaresNoRecordNoInitAccessorAndNoDefaultView()
    {
        // TAOM.Client declares no records or init accessors (its csproj), and the trial adds no view of its own.
        foreach (var file in TrialFiles())
        {
            var code = RepoPaths.ReadSource(TrialFolder + Path.GetFileName(file), stripComments: true);
            Assert.IsFalse(Regex.IsMatch(code, @"\brecord\b"), file);
            Assert.IsFalse(Regex.IsMatch(code, @"\binit\s*;"), file);
            Assert.IsFalse(code.Contains("[DefaultView"), file);
            Assert.IsFalse(Regex.IsMatch(code, @"\bclass\s+\w+\s*:\s*MissionView\b"), file);
        }
    }

    [TestMethod]
    public void TheTrial_NeverRemovesABehaviourOrWritesAnAgent_ExceptFinishingTheDeploymentFromTheScreenTick()
    {
        var host = TrialSource("^CommandTrialHost");
        var script = TrialSource("^CommandTrialScript");

        StringAssert.Contains(host, "FinishDeployment()");
        Assert.IsTrue(script.Length > 1000 && Regex.IsMatch(script, @"IEnumerable<Verdict> Enter\("), "the script's files were read, the Seat's steps included");
        Assert.IsFalse(Regex.IsMatch(host + script, @"RemoveMissionBehavior|\.Die\(|MovementFlags|MovementInputVector"), "S1 writes no agent");
        Assert.IsFalse(BodyOf(script, "internal void OnMissionTick").Contains("FinishDeployment"), "FinishDeployment runs from the screen tick, never the mission tick");
        Assert.IsTrue(BodyOf(script, "internal void OnMissionTick").Length > 20, "the body was found");
    }

    [TestMethod]
    public void TheTrial_KeysNothingOnAgentIndex()
    {
        foreach (var file in TrialFiles())
            Assert.IsFalse(Regex.IsMatch(File.ReadAllText(file), @"\.Index\b"), file);
    }

    [TestMethod]
    public void TheAutostart_NamesNoTaleWorldsType_SoItsExitsRunAgainstAFake()
    {
        var autostart = RepoPaths.ReadSource(TrialFolder + "CommandTrialAutostart.cs", stripComments: true);

        Assert.IsFalse(autostart.Contains("TaleWorlds"), "every engine call goes through ICommandTrialGameHost");
        Assert.IsFalse(Regex.IsMatch(autostart, @"\b(GameStateManager|MBGameManager|InformationManager|Mission\.Current|Utilities)\b"));
    }

    // The text from a method's opening brace to its closing one.
    private static string BodyOf(string code, string signature)
    {
        var start = code.IndexOf(signature, StringComparison.Ordinal);
        Assert.IsTrue(start >= 0, "no " + signature);
        var open = code.IndexOf('{', start);
        var depth = 0;
        for (var i = open; i < code.Length; i++)
        {
            if (code[i] == '{')
                depth++;
            else if (code[i] == '}' && --depth == 0)
                return code.Substring(open, i - open + 1);
        }

        Assert.Fail("unbalanced braces after " + signature);
        return "";
    }
}
