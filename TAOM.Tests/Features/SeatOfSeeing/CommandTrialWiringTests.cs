using System;
using System.Collections.Generic;
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
    private const string MarshalTrialFolder = "Client/Features/MarshalsWord/Trial/";

    private static string View() => RepoPaths.ReadSource("Client/Features/SeatOfSeeing/SeatView.cs", stripComments: true);

    private static string Member(string signature)
    {
        var code = View();
        var start = code.IndexOf(signature, StringComparison.Ordinal);
        Assert.IsTrue(start >= 0, "SeatView has no " + signature);
        var next = code.IndexOf("public override", start + signature.Length, StringComparison.Ordinal);
        return Regex.Replace(next < 0 ? code.Substring(start) : code.Substring(start, next - start), @"\s+", " ");
    }

    // Each feature keeps its rows, handlers and host members in its own files of one partial class (build-spec section 12), so a gate reads both folders.
    private static string[] TrialFiles() =>
        Directory.GetFiles(RepoPaths.RepoPath("Client", "Features", "SeatOfSeeing", "Trial"), "*.cs")
            .Concat(Directory.GetFiles(RepoPaths.RepoPath("Client", "Features", "MarshalsWord", "Trial"), "*.cs")).ToArray();

    private static string FolderOf(string file) => file.Replace('\\', '/').Contains("/MarshalsWord/") ? MarshalTrialFolder : TrialFolder;

    private static string TrialSource(string pattern) =>
        string.Join("\n", TrialFiles()
            .Where(f => Regex.IsMatch(Path.GetFileName(f), pattern))
            .Select(f => RepoPaths.ReadSource(FolderOf(f) + Path.GetFileName(f), stripComments: true)));

    // ---- The view ----

    [TestMethod]
    public void TheRealKey_AndTheTrial_TakeOnePath_ThroughPressSeatKey()
    {
        var tick = Member("public override void OnMissionScreenTick(float dt)");

        // The real key test calls PressSeatKey, and the session is driven by the flag it sets: nothing else feeds OnScreenTick.
        StringAssert.Contains(tick, "if (SeatKeys.Pressed(_keys.TakeTheSeat, Input)) PressSeatKey();");
        StringAssert.Contains(tick, "_session.OnScreenTick(pressed);");
        Assert.AreEqual(1, Regex.Matches(View(), @"_session\.OnScreenTick\(").Count, "the session has one caller");
    }

    [TestMethod]
    public void ThePressFlag_IsClearedInTheScreenTick_SoOneRealPressIsOneToggle()
    {
        var tick = Member("public override void OnMissionScreenTick(float dt)");

        StringAssert.Contains(tick, "var pressed = _seatKeyPressed; _seatKeyPressed = false; _moreKeys?.Tick(Input); _session.OnScreenTick(pressed);");
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
        StringAssert.Contains(Member("public override void OnMissionScreenTick(float dt)"), "_trial?.OnScreenTick(dt);");
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
        StringAssert.Contains(init, "new CommandTrialHost(this, session, screen, mission, MissionScreen, logger)");
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
            var code = RepoPaths.ReadSource(FolderOf(file) + Path.GetFileName(file), stripComments: true);
            Assert.IsFalse(Regex.IsMatch(code, @"\brecord\b"), file);
            Assert.IsFalse(Regex.IsMatch(code, @"\binit\s*;"), file);
            Assert.IsFalse(code.Contains("[DefaultView"), file);
            Assert.IsFalse(Regex.IsMatch(code, @"\bclass\s+\w+\s*:\s*MissionView\b"), file);
        }
    }

    [TestMethod]
    public void TheTrial_NeverRemovesABehaviourOrWritesAnAgent_ExceptAfterTheMissionTickMarker()
    {
        var host = TrialSource("^CommandTrialHost");
        var script = TrialSource("^CommandTrialScript");

        StringAssert.Contains(host, "FinishDeployment()");
        Assert.IsTrue(script.Length > 1000 && Regex.IsMatch(script, @"IEnumerable<Verdict> Enter\("), "the script's files were read, the Seat's steps included");
        Assert.IsFalse(Regex.IsMatch(host + script, @"RemoveMissionBehavior|MovementFlags|MovementInputVector"), "no behaviour is removed and no hero field is written here");
        Assert.IsFalse(BodyOf(script, "internal void OnMissionTick").Contains("FinishDeployment"), "FinishDeployment runs from the screen tick, never the mission tick");
        Assert.IsTrue(BodyOf(script, "internal void OnMissionTick").Length > 20, "the body was found");

        CollectionAssert.AreEqual(Array.Empty<string>(), WritesBeforeTheMarker(host, script).ToArray(),
            "an agent, formation or mission flag write runs in a step's code only after yield return Verdict.InMissionTick");
    }

    [TestMethod]
    public void TheMarkerGate_FindsTheHostsRealWriters_SoAnEmptyResultIsNotAScanThatSawNothing()
    {
        var writers = HostWriters(TrialSource("^CommandTrialHost"));

        foreach (var known in new[] { "KillChosenSoldier", "KillChosenCompany", "SelectChosenCompany", "OpenPanel", "ClosePanel", "ChargeHeroFormation", "StopHeroFormation", "WriteHeroMorale", "KillHero",
            "MarshalSelect", "MarshalMoveTo", "MarshalDrag", "MarshalStandGround", "MarshalChargeEnemy" })
            CollectionAssert.Contains(writers, known);
        CollectionAssert.DoesNotContain(writers, "PressSeatKey", "a key press is not an agent write");
        foreach (var held in new[] { "MarshalHoldQueue", "MarshalHoldMark", "MarshalHoldWidths", "MarshalPressFire", "MarshalPressWord" })
            CollectionAssert.DoesNotContain(writers, held, "a held or pressed key sets a view flag the screen tick reads, and writes no agent, formation or mission flag");
        CollectionAssert.DoesNotContain(writers, "ProbeFocus");
    }

    // The members of the host that a person has read and found to write no agent, formation or mission flag from the mission tick. A new member is
    // a write until a person adds it here: the writer list above is found from the engine verbs it names, and a verb nobody listed (a morale
    // write, a health write) would pass it. FinishDeployment removes behaviours and PressHide writes the interface flag; both run from the screen
    // tick by design (section 12), as a real key press or the engine's own deployment call would.
    private static readonly string[] HostMembersThatWriteNothing =
    {
        "CommandTrialHost", "MissionTime", "InDeployment", "InBattle", "HeroAlive", "HeroIsPlayerControlled", "HeroPosition", "ViewPosition",
        "GroundHeight", "FinishDeployment", "Screenshot", "PressSeatKey", "PressPending", "SetInput", "State", "Frames", "GlideBegan", "Faulted",
        "PressHide", "PanelOpen", "MouseVisible", "UiHidden", "WindowFocused", "MoveCursorTo", "ProbeFocus", "Said", "LastSaid", "PressKeep",
        "Keeping", "KeepsChosen", "DistanceToKept", "ChooseSoldier", "AimAtChosen", "ViewForward", "ChooseCompany", "NoteWrite", "WatchFocus",
        // The Marshal's Word members below read the chosen companies, the session's counters or its own tables, or store a ground point: read in CommandTrialHost.Marshal.cs, none writes an agent, a formation or a mission flag.
        "MarshalViewOfMission", "MarshalSessionOfMission", "PlayerController", "MarshalLive", "MarshalFaulted", "MarshalChooseCompanies", "MarshalChooseArchers",
        "MarshalHoldQueue", "MarshalHoldMark", "MarshalHoldWidths", "MarshalQueueArmed", "MarshalMarkArmed", "MarshalWidthsArmed", "MarshalPlanPoint",
        "MarshalPlanFrontDrag", "MarshalPlanArrivalDrag", "MarshalDistanceToPoint", "MarshalOffset", "MarshalInLine", "MarshalSpeed",
        "MarshalQueuedSteps", "MarshalStepsFinished", "MarshalStepsByPatience", "MarshalArraysSolved", "MarshalMarchConclusion", "MarshalMarchesConcluded",
        "MarshalChooseEnemy", "MarshalMarkedEnemy", "MarshalOrderName", "MarshalMarkRead", "MarshalPressFire", "MarshalPressWord", "MarshalFireMode",
        "MarshalHolding", "MarshalHoldVerdict", "MarshalReleaseVerdict", "MarshalOutlinesPainted", "ArrayCompaniesOfChosen", "GroundPosition",
        "MarshalReleaseKeys", "MarshalSettled", "MarshalAhead", "MarshalOrderedOffset", "MarshalStepsCaptured", "MarshalStepsByArrival",
        "MarshalOrderPoint", "MarshalEnemyDistance", "MarshalMarkJudged", "MarshalChooseNearestEnemy", "MarshalPlanApproach", "MarshalWatchBegin",
        "MarshalWatchEnd", "MarshalWatchShots", "MarshalWatchShooters", "MarshalAside", "MarshalIdle", "CensusOfChosen",
        // Stage S4 and S5: reads, the in-memory setting of the trial, and the take key, which stores a flag the mission tick acts on.
        "SetFightOn", "FightGateUp", "HeroOnAi", "HeroRetreating", "HeroPanicked", "HeroMorale", "HeroMortal", "FormationsOwnedByHero", "PressTake",
        "TookFromKeptCompany", "ReadHandOver",
    };

    private static string[] Unclassified(string host, IEnumerable<string> writeNothing) =>
        HostMembers(host).Keys.Except(HostWriters(host)).Except(writeNothing).OrderBy(n => n).ToArray();

    [TestMethod]
    public void TheHost_EveryMemberIsAWriterOrOneAPersonSaidWritesNothing_AndNoPropertyWrites()
    {
        var host = TrialSource("^CommandTrialHost");

        CollectionAssert.AreEqual(Array.Empty<string>(), Unclassified(host, HostMembersThatWriteNothing),
            "a new host member is a write until a person says otherwise: add it to HostMembersThatWriteNothing after reading it");
        Assert.IsFalse(Regex.IsMatch(host, @"\bset\s*[{=;]"), "no host property writes");
        CollectionAssert.IsSubsetOf(HostMembersThatWriteNothing, HostMembers(host).Keys.ToList(),
            "a name on the list that no longer exists is a stale entry: " + string.Join(", ", HostMembersThatWriteNothing.Except(HostMembers(host).Keys)));
    }

    [TestMethod]
    public void TheMarkerGate_FailsAHostMemberNobodyClassified()
    {
        const string host = FakeHost + @"
        public void SetHealth(Agent agent, float value) { agent.Health = value; }
        void Helper() { }
        public T Pick<T>(T value) => value;
        public float Speed { get; set; }";

        CollectionAssert.AreEqual(new[] { "Helper", "Pick", "SetHealth", "Speed" }, Unclassified(host, new[] { "Look" }),
            "a verb the writer list does not know, a member with no access modifier, a generic one and a property are all unclassified");
        CollectionAssert.AreEqual(new[] { "Helper", "Pick", "SetHealth", "Speed" }, Unclassified(host, new[] { "Look", "KillIt", "Kill" }),
            "a writer needs no entry");
        CollectionAssert.AreEqual(Array.Empty<string>(), Unclassified(host, new[] { "Look", "SetHealth", "Helper", "Pick", "Speed" }));
        Assert.IsTrue(Regex.IsMatch(host, @"\bset\s*[{=;]"), "and the property's setter is what the set check finds");
    }

    [TestMethod]
    public void TheMarkerGate_FindsAnIteratorThatTakesAParameter()
    {
        const string script = @"
            private IEnumerable<Verdict> Step(bool largest)
            {
                yield return Verdict.Waiting(""x"");
                _host.KillIt();
                yield return Verdict.Passed("""");
            }";

        CollectionAssert.AreEqual(new[] { "Step: KillIt" }, WritesBeforeTheMarker(FakeHost, script).ToArray());
    }

    private const string FakeHost = @"
        public void KillIt() => Kill(_agent);
        public bool Look() => _agent.IsActive();
        private void Kill(Agent agent) { _mission.KillAgentCheat(agent); }";

    [TestMethod]
    public void TheMarkerGate_FailsAWriteWithNoMarker()
    {
        const string script = @"
            private IEnumerable<Verdict> Step()
            {
                yield return Verdict.Waiting(""x"");
                _host.KillIt();
                yield return Verdict.Passed("""");
            }";

        CollectionAssert.AreEqual(new[] { "Step: KillIt" }, WritesBeforeTheMarker(FakeHost, script).ToArray());
    }

    [TestMethod]
    public void TheMarkerGate_PassesAWriteRightAfterTheMarker_AndAFailedVerdictBetweenDoesNotCount()
    {
        const string script = @"
            private IEnumerable<Verdict> Step()
            {
                yield return Verdict.Waiting(""x"");
                yield return Verdict.InMissionTick;
                if (_host.Look()) yield return Verdict.Failed(""stop"");
                _host.KillIt();
                yield return Verdict.Passed("""");
            }";

        Assert.AreEqual(0, WritesBeforeTheMarker(FakeHost, script).Count());
    }

    [TestMethod]
    public void TheMarkerGate_FailsAWriteAfterAWaitThatFollowedTheMarker()
    {
        const string script = @"
            private IEnumerable<Verdict> Step()
            {
                yield return Verdict.InMissionTick;
                _host.KillIt();
                while (_host.Look()) yield return Verdict.Waiting(""x"");
                _host.KillIt();
                yield return Verdict.Passed("""");
            }";

        CollectionAssert.AreEqual(new[] { "Step: KillIt" }, WritesBeforeTheMarker(FakeHost, script).ToArray(), "the second write runs in a screen tick");
    }

    [TestMethod]
    public void TheMarkerGate_FailsAMarkerInAnotherMethod()
    {
        const string script = @"
            private IEnumerable<Verdict> Other() { yield return Verdict.InMissionTick; }
            private IEnumerable<Verdict> Step()
            {
                _host.KillIt();
                yield return Verdict.Passed("""");
            }";

        CollectionAssert.AreEqual(new[] { "Step: KillIt" }, WritesBeforeTheMarker(FakeHost, script).ToArray());
    }

    [TestMethod]
    public void TheMarkerGate_ReadsTheHostFileForAWriteThatHidesBehindAHelper()
    {
        // KillIt has no .KillAgentCheat( of its own: it reaches one through a private helper, and still counts as a writer.
        CollectionAssert.Contains(HostWriters(FakeHost), "KillIt");
        CollectionAssert.DoesNotContain(HostWriters(FakeHost), "Look");
    }

    // Every member of the host, with or without an access modifier, generic or not, a method, a constructor or a property: name to body.
    private static readonly Regex MemberPattern = new Regex(
        @"^[ \t]*(?:(?:public|private|internal|protected|static|async|override|virtual|sealed|readonly)\s+)*[\w<>?\[\],.]+(?:\s*<[^>]*>)?\s+"
        + @"(?!(?:if|while|for|foreach|switch|using|lock|catch|return|yield|new|throw|await)\b)(\w+)\s*(?:<[^>]*>)?\s*(?:\([^)]*\)\s*(?:=>|\{)|=>|\{)",
        RegexOptions.Multiline);

    private static Dictionary<string, string> HostMembers(string host)
    {
        var members = new Dictionary<string, string>();
        foreach (Match m in MemberPattern.Matches(host))
        {
            members[m.Groups[1].Value] = m.Value.TrimEnd().EndsWith("=>", StringComparison.Ordinal)
                ? host.Substring(m.Index, host.IndexOf(';', m.Index) - m.Index)
                : BodyOf(host.Substring(m.Index + m.Length - 1), "{");
        }

        return members;
    }

    // The members of the host that write an agent, a formation or a mission flag: their body names an engine write, or a helper that does.
    private static List<string> HostWriters(string host)
    {
        var members = HostMembers(host);

        const string Write = @"\.KillAgentCheat\(|ClearSelectedFormations|\.SelectFormation\(|_adapter\.OpenPanel\(|_adapter\.ClosePanel\(|IsOrderMenuOpen\s*=|\.SetOrder\w*\(|\.SetTargetFormation\(|\.SetMovementOrder\(|\.SetMorale\(";
        var writers = members.Where(kv => Regex.IsMatch(kv.Value, Write)).Select(kv => kv.Key).ToList();
        for (var changed = true; changed;)
        {
            changed = false;
            foreach (var kv in members.Where(kv => !writers.Contains(kv.Key)).ToList())
            {
                if (writers.Any(w => Regex.IsMatch(kv.Value, @"\b" + w + @"\(")))
                {
                    writers.Add(kv.Key);
                    changed = true;
                }
            }
        }

        return writers;
    }

    // "Step: Write" for each call of a host writer in a step iterator whose nearest earlier continuing yield is not the marker. A Failed or
    // Passed verdict ends the step, so the code after it never runs and does not count as a yield.
    private static IEnumerable<string> WritesBeforeTheMarker(string host, string script)
    {
        var writers = HostWriters(host);
        foreach (Match step in Regex.Matches(script, @"IEnumerable<Verdict> (\w+)\([^)]*\)\s*\{"))
        {
            var body = BodyOf(script.Substring(step.Index), "{");
            foreach (var writer in writers)
            {
                foreach (Match call in Regex.Matches(body, @"_host\." + writer + @"\("))
                {
                    var yields = Regex.Matches(body.Substring(0, call.Index), @"yield return (?!Verdict\.(?:Failed|Passed)\()([^;]*);");
                    if (yields.Count == 0 || yields[yields.Count - 1].Groups[1].Value.Trim() != "Verdict.InMissionTick")
                        yield return step.Groups[1].Value + ": " + writer;
                }
            }
        }
    }

    [TestMethod]
    public void TheTrial_KeysNothingOnAgentIndex()
    {
        foreach (var file in TrialFiles())
            Assert.IsFalse(Regex.IsMatch(File.ReadAllText(file), @"\.Index\b"), file);
    }

    [TestMethod]
    public void TheKill_GoesThroughTheEnginesOwnCheat_SoItBuildsNoBlowOfItsOwn()
    {
        var host = TrialSource("^CommandTrialHost");

        StringAssert.Contains(Regex.Replace(host, @"\s+", " "), "NoteWrite(\"CommandTrialHost.Kill\"); _mission.KillAgentCheat(agent);");
        Assert.IsFalse(Regex.IsMatch(host, @"new Blow\(|\.Die\("), "the engine's cheat builds the blow");
    }

    [TestMethod]
    public void TheKillOfTheChosenSoldier_HasTheOccupantGateItsSiblingHas()
    {
        var host = Regex.Replace(TrialSource("^CommandTrialHost"), @"\s+", " ");
        var kill = host.Substring(host.IndexOf("public void KillChosenSoldier()", StringComparison.Ordinal));
        kill = kill.Substring(0, kill.IndexOf("public void KillChosenCompany()", StringComparison.Ordinal));

        StringAssert.Contains(kill, "AgentSlotIdentity.IsCurrentOccupant(agent) && agent.IsActive()");
        StringAssert.Contains(host, "!AgentSlotIdentity.IsCurrentOccupant(agent) || !agent.IsActive()", "the sibling the gate is copied from");
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
