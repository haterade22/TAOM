using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.MountAndBlade;
using TAOM.Adapters;
using TAOM.Client.Features.MarshalsWord;
using TAOM.Core.Logging;
using TAOM.Features;
using TAOM.Features.MarshalsWord;
using TAOM.Features.MarshalsWord.Domain;
using TAOM.Tests.Features.LoadTimeStamps;
using TAOM.Tests.Features.SeatOfSeeing;
using TAOM.Tests.Infrastructure;
using TAOM.Tests.Migration;
using static TAOM.Tests.Infrastructure.RepoPaths;

namespace TAOM.Tests.Features.MarshalsWord;

/// <summary>
/// Throwers on an advance (build-spec 9.4) end to end: the session's forwards, the statics the patches read, the adapter's reads, the shot count, and the lines each
/// check writes against the manifest's rows and the fixtures that stand for them.
/// </summary>
[TestClass]
public class MarshalThrowerWiringTests
{
    private IModLogger _logger = null!;
    private OutcomeLog _log = null!;

    [TestInitialize]
    public void Setup()
    {
        Reset();
        _logger = Substitute.For<IModLogger>();
        _log = new OutcomeLog(_logger, new FakeStampClock(), () => 0);
    }

    [TestCleanup]
    public void Cleanup() => Reset();

    private static void Reset()
    {
        MarshalHooks.Begin(Substitute.For<IMarshalHookSink>());
        MarshalHooks.Clear();
        MarshalFireTable.Begin();
        MarshalThrowerCost.Reset();
    }

    private static string Code(string path) => Regex.Replace(ReadSource(path, stripComments: true), @"\s+", " ");

    private static string Member(string code, string signature)
    {
        var start = code.IndexOf(signature, StringComparison.Ordinal);
        Assert.IsTrue(start >= 0, "no " + signature);
        var next = Regex.Match(code.Substring(start + signature.Length), @" (public|private|internal) (override |static |void |bool |string |int )");
        return next.Success ? code.Substring(start, next.Index + signature.Length) : code.Substring(start);
    }

    // ---- the session ----

    private const string SessionPath = "Client/Features/MarshalsWord/MarshalSession.cs";

    [TestMethod]
    public void TheSession_BuildsTheDriver_FromTheAdapter_AndRunsItEachMissionTickAfterTheMarch()
    {
        var code = Code(SessionPath);

        StringAssert.Contains(code, "_throwers = new MarshalThrowerDriver(throwers, outcomes, logger, missionKey);");
        StringAssert.Contains(Member(code, "internal void OnMissionTick()"), "_march.Pass(MarshalHooks.ArriveTogether); _throwers.Pass(MarshalHooks.ThrowersFirst); _fire.Pass();");
    }

    [TestMethod]
    public void TheSession_HearsANewAdvance_AndTeamChanges_AndLetsGoOfThemOnBothTeardownPaths()
    {
        var code = Code(SessionPath);

        StringAssert.Contains(code, "_orders.AdvanceGiven += _throwers.OnAdvanceGiven;");
        StringAssert.Contains(code, "_orders.AdvanceGiven -= _throwers.OnAdvanceGiven;");
        Assert.IsFalse(code.Contains("_throwers.OnDirectOrder"), "a facing, a transfer or a cohesion change does not end a thrower run");
        StringAssert.Contains(Member(code, "private void OnPlayerTeamChanged()"), "_throwers.OnPlayerTeamChanged();");
        StringAssert.Contains(Member(code, "private void Teardown()"), "if (owner) { Swallow(_fire.Summarize); Swallow(_throwers.Summarize); MarshalHooks.Clear(); }");
    }

    [TestMethod]
    public void TheSession_CopiesTheSetting_WithTheTrialSwitchOredAgainstIt_WhenItGoesLiveAndEveryFrame()
    {
        var code = Code(SessionPath);

        Assert.AreEqual(2, Regex.Matches(code, Regex.Escape("_settings.ThrowersFirst && !MarshalHooks.TrialThrowersOff")).Count);
        StringAssert.Contains(Member(code, "internal void OnScreenTick(MarshalScreenInput input)"), "_settings.ThrowersFirst && !MarshalHooks.TrialThrowersOff");
        StringAssert.Contains(Member(code, "private void Begin()"), "throwersFirst: _settings.ThrowersFirst && !MarshalHooks.TrialThrowersOff");
    }

    private (MarshalSession Session, FakeMarshalThrowers Engine, FakeMarshalSettings Settings) LiveSession(bool throwersFirst = true, bool trialOff = false)
    {
        var engine = new FakeMarshalThrowers { Now = 1.0 };
        engine.Standard(1);
        var settings = new FakeMarshalSettings { ThrowersFirst = throwersFirst };
        MarshalHooks.TrialThrowersOff = trialOff;
        var session = new MarshalSession(new FakeMarshalOrders(), new FakeMarshalFire(), engine, new FakeMarshalOutlines(), new FakeMarshalScreen(), settings, () => true,
            _log, _logger, "1");
        session.OnMissionTick();
        Assert.IsTrue(session.IsLive);
        return (session, engine, settings);
    }

    private static MarshalScreenInput Frame() => new(false, false, false, 1f);

    [TestMethod]
    public void TheSession_WithTheSettingOn_PublishesTheStandPointOfACompanyOnAnAdvance()
    {
        var (session, engine, _) = LiveSession();
        try
        {
            session.OnScreenTick(Frame());
            engine.Now = 2.0;
            session.OnMissionTick();

            CollectionAssert.AreEqual(new[] { new ThrowerPoint(1, 0f, 65f) }, engine.Latest);
        }
        finally
        {
            session.Finish();
        }
    }

    [TestMethod]
    public void TheSession_WithTheSettingOff_OrTheTrialSwitchOn_PublishesNothing()
    {
        foreach (var trial in new[] { false, true })
        {
            Reset();
            var (session, engine, _) = LiveSession(throwersFirst: trial, trialOff: trial);
            try
            {
                session.OnScreenTick(Frame());
                engine.Now = 2.0;
                session.OnMissionTick();

                Assert.IsFalse(MarshalHooks.ThrowersFirst, "trial switch " + trial);
                Assert.AreEqual(0, engine.Tables.Count, "trial switch " + trial);
            }
            finally
            {
                session.Finish();
            }
        }
    }

    [TestMethod]
    public void TheSessionsFinish_ClearsTheTableAndTheTrialSwitch_AndWritesTheOpenEpochsCostCheck()
    {
        var (session, engine, _) = LiveSession();
        session.OnScreenTick(Frame());
        engine.Now = 2.0;
        session.OnMissionTick();
        MarshalHooks.Throwers = new MarshalThrowerTable(Array.Empty<Formation>(), Array.Empty<float>(), Array.Empty<float>());
        MarshalHooks.TrialThrowersOff = true;

        session.Finish();
        session.Finish();

        Assert.IsNull(MarshalHooks.Throwers, "both teardown paths clear the published table");
        Assert.IsFalse(MarshalHooks.TrialThrowersOff);
        Assert.AreEqual(1, CheckLines().Count(l => l.Contains("throwers-cost")), "the finish is safe to repeat");
    }

    private string[] CheckLines() =>
        _logger.ReceivedCalls().Where(c => c.GetMethodInfo().Name == nameof(IModLogger.LogInfo)).Select(c => c.GetArguments()[0]?.ToString() ?? "")
            .Where(l => l.StartsWith("[Check] ", StringComparison.Ordinal)).ToArray();

    // ---- the view, the module and the statics ----

    [TestMethod]
    public void TheView_ResolvesTheThrowerAdapter_AndTheHostSetsTheTrialSwitchOnTheHooksDirectly()
    {
        var code = Code("Client/Features/MarshalsWord/MarshalView.cs");

        StringAssert.Contains(code, "IoC.Resolve<IMarshalFireAdapter>(), IoC.Resolve<IMarshalThrowerAdapter>(), outlines,");
        Assert.IsFalse(code.Contains("TrialThrowersOff"), "the view keeps no forwarder of its own");
        StringAssert.Contains(Code("Client/Features/MarshalsWord/Trial/CommandTrialHost.Marshal.cs"), "public void MarshalThrowersOff(bool off) => MarshalHooks.TrialThrowersOff = off;");
    }

    [TestMethod]
    public void TheModule_RegistersTheThrowerAdapter_PerResolve_AndAddsNoPatchCategory()
    {
        using var container = new Container();
        new MarshalsWordModule().RegisterServices(container);

        Assert.IsInstanceOfType(container.Resolve<IMarshalThrowerAdapter>(), typeof(MarshalThrowerAdapter));
        Assert.AreNotSame(container.Resolve<IMarshalThrowerAdapter>(), container.Resolve<IMarshalThrowerAdapter>(), "it caches the mission's field battle fact");
        Assert.AreEqual(1, new MarshalsWordModule().PatchCategories.Count, "the two thrower patches join the shared deferred category the SubModule already applies");
    }

    [TestMethod]
    public void Hooks_BeginDropsTheTableAndTheFault_ClearDropsTheTableTheSettingAndTheTrialSwitch()
    {
        MarshalHooks.Begin(Substitute.For<IMarshalHookSink>());
        MarshalHooks.Throwers = new MarshalThrowerTable(Array.Empty<Formation>(), Array.Empty<float>(), Array.Empty<float>());
        MarshalHooks.LatchThrowerFault(new InvalidOperationException("x"));
        MarshalHooks.Arm(false, false, true, true, throwersFirst: true);
        MarshalHooks.TrialThrowersOff = true;
        Assert.IsTrue(MarshalHooks.ThrowersFirst);

        MarshalHooks.Clear();
        Assert.IsNull(MarshalHooks.Throwers);
        Assert.IsFalse(MarshalHooks.ThrowersFirst);
        Assert.IsFalse(MarshalHooks.TrialThrowersOff);
        Assert.IsNotNull(MarshalHooks.ThrowerFault, "a fault is read after the finish by the pass that was owed it, and a new mission clears it");

        MarshalHooks.Throwers = new MarshalThrowerTable(Array.Empty<Formation>(), Array.Empty<float>(), Array.Empty<float>());
        MarshalHooks.Begin(Substitute.For<IMarshalHookSink>());
        Assert.IsNull(MarshalHooks.Throwers);
        Assert.IsNull(MarshalHooks.ThrowerFault);
    }

    [TestMethod]
    public void Hooks_OnlyTheFirstFaultIsKept()
    {
        var first = new InvalidOperationException("first");

        MarshalHooks.LatchThrowerFault(first);
        MarshalHooks.LatchThrowerFault(new InvalidOperationException("second"));

        Assert.AreSame(first, MarshalHooks.ThrowerFault);
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void TheTable_FindsAFormationByReference_AndNeverByHash()
    {
        if (!GameAssemblies.EnsureLoaded())
            Assert.Inconclusive("Game assemblies not loaded.");
        var a = (Formation)FormatterServices.GetUninitializedObject(typeof(Formation));
        var b = (Formation)FormatterServices.GetUninitializedObject(typeof(Formation));
        var table = new MarshalThrowerTable(new[] { a, b }, new[] { 1f, 2f }, new[] { 3f, 4f });

        Assert.AreEqual(0, table.IndexOf(a));
        Assert.AreEqual(1, table.IndexOf(b));
        Assert.AreEqual(-1, table.IndexOf((Formation)FormatterServices.GetUninitializedObject(typeof(Formation))));
        Assert.AreEqual(-1, table.IndexOf(null));
        var code = ReadSource("Main/Features/MarshalsWord/MarshalThrowerTable.cs", stripComments: true);
        StringAssert.Contains(code, "ReferenceEquals(Formations[i], formation)");
        foreach (var banned in new[] { "Dictionary", "HashSet", ".Equals(", "lock" })
            Assert.IsFalse(code.Contains(banned), banned);
    }

    // ---- the shot count and the reader ----

    [TestMethod]
    public void TheShotCount_CountsEveryCompanyWhateverItsFireMode_ZeroesOnBeginAndClear_AndReadsAnOutOfRangeCompanyAsZero()
    {
        MarshalFireTable.SetMode(2, FireMode.OnWord);

        MarshalFireTable.CountAnyShot(1);
        MarshalFireTable.CountAnyShot(2);
        MarshalFireTable.CountAnyShot(2);
        MarshalFireTable.CountAnyShot(-1);
        MarshalFireTable.CountAnyShot(99);

        Assert.AreEqual(1, MarshalFireTable.ShotsOf(1));
        Assert.AreEqual(2, MarshalFireTable.ShotsOf(2));
        Assert.AreEqual(0, MarshalFireTable.ShotsOf(99));
        MarshalFireTable.Begin();
        Assert.AreEqual(0, MarshalFireTable.ShotsOf(2));
        MarshalFireTable.CountAnyShot(2);
        MarshalFireTable.Clear();
        Assert.AreEqual(0, MarshalFireTable.ShotsOf(2));
    }

    [TestMethod]
    public void TheShotReader_CountsEveryPlayerTeamShot_BeforeItTestsTheMode_AndBeforeAnyLockOrLog()
    {
        var reader = Code("Main/Adapters/MarshalCombatReader.cs");
        var shot = Member(reader, "internal static void OnShot(Agent shooter)");

        StringAssert.Contains(shot, "if (formation == null || shooter.Team != Mission.Current?.PlayerTeam) return; var company = formation.Index; MarshalFireTable.NoteShooter(company, shooter); MarshalFireTable.CountAnyShot(company); if (MarshalFireTable.ModeOf(company) == FireMode.Free) return;");
        StringAssert.Contains(Code("Main/Features/MarshalsWord/MarshalFireTable.cs"), "Interlocked.Increment(ref ShotsAny[company])");
    }

    // ---- the adapter ----

    private const string AdapterPath = "Main/Adapters/MarshalThrowerAdapter.cs";

    [TestMethod]
    public void TheAdapter_CountsASoldierByTheEquipmentTheContractNames_AndNeverByTheCachedFlags()
    {
        var code = Code(AdapterPath);
        var soldier = Member(code, "private void CountSoldier(Agent agent)");

        StringAssert.Contains(soldier, "if (agent.HasMount) return;");
        StringAssert.Contains(soldier, "for (var slot = EquipmentIndex.WeaponItemBeginSlot; slot < EquipmentIndex.ExtraWeaponSlot; slot++)");
        StringAssert.Contains(soldier, "var usage = weapon.GetRangedUsageIndex(); if (usage < 0) continue; if (!weapon.GetWeaponComponentDataForUsage(usage).IsConsumable) return; if (weapon.Amount <= 0) continue;");
        foreach (var banned in new[] { "HasThrownCached", "IsRangedCached", "ContainsNonConsumable" })
            Assert.IsFalse(code.Contains(banned), banned + " takes a lock or ignores the ammunition (MissionEquipment.cs:761-819)");
    }

    [TestMethod]
    public void TheAdapter_ReadsTheRangeFromItemData_AtTheMedianOfTheArmed()
    {
        var code = Code(AdapterPath);

        StringAssert.Contains(code, "speed = weapon.GetModifiedMissileSpeedForUsage(usage) * agent.AgentDrivenProperties.MissileSpeedMultiplier;");
        StringAssert.Contains(code, "_speeds.Sort(); return Mission.GetMissileRange(_speeds[_speeds.Count / 2], 0f);");
        Assert.IsFalse(code.Contains("MaximumMissileRange"), "the engine's own range read answers 0 for a soldier holding his sword (native IMBAgent.GetMissileRange)");
    }

    [TestMethod]
    public void TheAdapter_ReadsTheTargetAsVanillaDoes_ReadsTheFieldBattleFactOnce_AndWritesOnlyTheTable()
    {
        var code = Code(AdapterPath);

        StringAssert.Contains(code, "formation.TargetFormation?.QuerySystem ?? query.ClosestSignificantlyLargeEnemyFormation;");
        StringAssert.Contains(code, "FieldBattle: _fieldBattle ??= mission.IsFieldBattle,");
        StringAssert.Contains(code, "RangedBranch: query.IsRangedFormation || query.IsRangedCavalryFormation,");
        StringAssert.Contains(code, "Held: MarshalFireTable.HoldOf(formation.Index), OnFireMode: MarshalFireTable.ModeOf(formation.Index) != FireMode.Free,");
        foreach (var banned in new[] { "SetOrder", "SelectFormation", "SetMovementOrder", "SetFiringOrder", "SetAgentFlags", "NoteCall", "SetTargetFormation" })
            Assert.IsFalse(code.Contains(banned), banned + ": the adapter writes no agent and no formation, so it has no MissionThreadGuard site");
        Assert.AreEqual(2, Regex.Matches(code, @"MarshalHooks\.Throwers = ").Count, "the null and the table, and nothing else is written");
    }

    [DataTestMethod]
    [TestCategory("RequiresGame")]
    [DataRow(ArrangementOrder.ArrangementOrderEnum.Line, true)]
    [DataRow(ArrangementOrder.ArrangementOrderEnum.Loose, true)]
    [DataRow(ArrangementOrder.ArrangementOrderEnum.Scatter, true)]
    [DataRow(ArrangementOrder.ArrangementOrderEnum.Skein, true)]
    [DataRow(ArrangementOrder.ArrangementOrderEnum.Column, false)]
    [DataRow(ArrangementOrder.ArrangementOrderEnum.Circle, false)]
    [DataRow(ArrangementOrder.ArrangementOrderEnum.ShieldWall, false)]
    [DataRow(ArrangementOrder.ArrangementOrderEnum.Square, false)]
    public void ThrowsInArrangement_EveryArrangementOnDefaultMove_AndNoOther(ArrangementOrder.ArrangementOrderEnum arrangement, bool throws)
    {
        Assert.AreEqual(throws, MarshalThrowerAdapter.ThrowsInArrangement(arrangement), arrangement.ToString());
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void TheArrangements_AreTheEightTheGateWasDecidedOver()
    {
        Assert.AreEqual(8, Enum.GetValues(typeof(ArrangementOrder.ArrangementOrderEnum)).Length, "a new arrangement needs its own look at HumanAIComponent.cs:796-808");
    }

    [TestMethod]
    public void TheOrderAdapter_RaisesAdvanceGivenForAnAdvanceOnly_AndStillRaisesDirectOrderForTheOrdersThatClearTheQueue()
    {
        var issued = Member(Code("Main/Adapters/MarshalOrderAdapter.cs"), "private void OnOrderIssued(");

        StringAssert.Contains(issued, "var direct = MarshalOrderPayload.ClearsQueue(orderType) ? DirectOrder : null;");
        StringAssert.Contains(issued, "var advance = orderType == OrderType.Advance ? AdvanceGiven : null;");
        StringAssert.Contains(issued, "direct?.Invoke(companies); advance?.Invoke(companies);");
    }

    [TestMethod]
    public void ThePublish_BindsTheEngagePointOnTheMainThread_AfterTheEmptyTableAndBeforeAnyTableExists()
    {
        var publish = Member(Code(AdapterPath), "public void Publish(");

        StringAssert.Contains(publish, "MarshalEngagePoint.Bind(); var formations = new List<Formation>(points.Count);");
    }

    // ---- the lines against the manifest's rows ----

    private static string[] Toml() => File.ReadAllLines(RepoPath("tools", "ingame_checks", "0.toml"));

    private static string[] Fixture(string name)
    {
        var path = RepoPath("tools", "tests", "fixtures", "ingame_verdict", name);
        Assert.IsTrue(File.Exists(path), path);
        return File.ReadAllLines(path);
    }

    private static string PassPatternOf(string id)
    {
        var rows = Toml();
        var first = Array.FindIndex(rows, l => l.Trim() == "id = \"" + id + "\"");
        Assert.IsTrue(first >= 0, id);
        var pass = rows.Skip(first).First(l => l.StartsWith("pass = ", StringComparison.Ordinal));
        var match = Regex.Match(pass, "^pass = \\['(.*)'\\]\\s*$");
        Assert.IsTrue(match.Success, "one literal pattern in: " + pass);
        return match.Groups[1].Value;
    }

    private static string Normal(string line) => Regex.Replace(line, @"(mean_us|max_us)=[0-9.]+", "$1=N");

    private static void AssertInFixture(string fixture, string line) =>
        Assert.IsTrue(Fixture(fixture).Any(l => Normal(l).EndsWith("] [INFO] " + Normal(line), StringComparison.Ordinal)), fixture + " lacks the rendered line: " + line);

    private MarshalThrowerDriver Driver(FakeMarshalThrowers engine) => new(engine, _log, _logger, "1");

    private static void PassAt(MarshalThrowerDriver driver, FakeMarshalThrowers engine, double now)
    {
        engine.Now = now;
        driver.Pass(true);
    }

    [TestMethod]
    public void RenderedLines_ForARunThatStoodThrewAndWentIn_ArePassLinesTheFixturesHold()
    {
        var engine = new FakeMarshalThrowers();
        engine.Standard(1);
        var driver = Driver(engine);
        PassAt(driver, engine, 0.0);
        engine.MoveTo(1, 65f);
        PassAt(driver, engine, 0.75);
        PassAt(driver, engine, 1.0);
        for (var i = 0; i < 12; i++)
            MarshalFireTable.CountAnyShot(1);
        engine.Censuses[1] = new ThrowerCensus(40, 12, 12);
        PassAt(driver, engine, 3.0);
        engine.MoveTo(1, 69f);
        MarshalFireTable.CountBlow(1);
        PassAt(driver, engine, 8.0);

        var lines = CheckLines();
        foreach (var (line, fixture) in new[]
                 {
                     ("[Check] id=taom.marshals-word.throwers-stand result=pass mission=1 company=1 gap_at_stand=32.0 stand_gap=32.0 overshoot=0.0", "marshal_throwers_stand_pass.txt"),
                     ("[Check] id=taom.marshals-word.throwers-throw result=pass mission=1 company=1 shots=12 stood_s=2.0", "marshal_throwers_throw_pass.txt"),
                     ("[Check] id=taom.marshals-word.throwers-melee result=pass mission=1 company=1 blows=1 melee_s=5.0 advanced_m=4.0", "marshal_throwers_melee_pass.txt"),
                 })
        {
            CollectionAssert.Contains(lines, line);
            AssertInFixture(fixture, line);
        }
    }

    [TestMethod]
    public void RenderedLines_ForAStandThatWalkedInAndShotsThatWereFew_AreTheFailLinesTheFixturesHold()
    {
        var engine = new FakeMarshalThrowers();
        engine.Standard(1);
        var driver = Driver(engine);
        PassAt(driver, engine, 0.0);
        engine.MoveTo(1, 65f);
        PassAt(driver, engine, 0.75);
        PassAt(driver, engine, 1.0);
        engine.MoveTo(1, 73f);
        PassAt(driver, engine, 1.5);
        for (var i = 0; i < 4; i++)
            MarshalFireTable.CountAnyShot(1);
        engine.Censuses[1] = new ThrowerCensus(40, 12, 12);
        PassAt(driver, engine, 13.0);
        PassAt(driver, engine, 58.0);

        var lines = CheckLines();
        foreach (var (line, fixture) in new[]
                 {
                     ("[Check] id=taom.marshals-word.throwers-stand result=fail mission=1 company=1 reason=walked-in gap_at_stand=32.0 stand_gap=32.0 overshoot=8.0", "marshal_throwers_stand_walked_in.txt"),
                     ("[Check] id=taom.marshals-word.throwers-throw result=fail mission=1 company=1 reason=few-throws shots=4 stood_s=12.0", "marshal_throwers_throw_fail.txt"),
                     ("[Check] id=taom.marshals-word.throwers-melee result=fail mission=1 company=1 reason=no-melee blows=0 melee_s=45.0 advanced_m=0.0", "marshal_throwers_melee_fail.txt"),
                 })
        {
            CollectionAssert.Contains(lines, line);
            AssertInFixture(fixture, line);
        }
    }

    [TestMethod]
    public void RenderedLines_ForARunThatNeverStoodAndAnOrderThatChanged_AreTheSkipLinesTheFixturesHold()
    {
        var engine = new FakeMarshalThrowers();
        engine.Standard(1);
        var driver = Driver(engine);
        PassAt(driver, engine, 0.0);
        engine.GatesOf[1] = engine.GatesOf[1] with { Held = true };
        PassAt(driver, engine, 0.5);

        var lines = CheckLines();
        foreach (var (line, fixture) in new[]
                 {
                     ("[Check] id=taom.marshals-word.throwers-stand result=skip mission=1 company=1 reason=released-before-stand why=held", "marshal_throwers_stand_skip.txt"),
                     ("[Check] id=taom.marshals-word.throwers-throw result=skip mission=1 company=1 reason=never-stood", "marshal_throwers_throw_skip.txt"),
                 })
        {
            CollectionAssert.Contains(lines, line);
            AssertInFixture(fixture, line);
        }
    }

    [TestMethod]
    public void RenderedLines_ForTheCostOfAnEpoch_ArePassFailAndSkipLines_WhoseNumbersMoveWithTheClock()
    {
        foreach (var (calls, ticksPerCall, line, fixture) in new[]
                 {
                     (400, 5L, "[Check] id=taom.marshals-word.throwers-cost result=pass mission=1 epoch=1 calls=400 mean_us=5.0 max_us=5.0", "marshal_throwers_cost_pass.txt"),
                     (400, 80L, "[Check] id=taom.marshals-word.throwers-cost result=fail mission=1 epoch=1 reason=mean calls=400 mean_us=80.0 max_us=80.0", "marshal_throwers_cost_fail_mean.txt"),
                     (12, 3L, "[Check] id=taom.marshals-word.throwers-cost result=skip mission=1 epoch=1 reason=too-few-calls calls=12 mean_us=3.0 max_us=3.0", "marshal_throwers_cost_skip.txt"),
                 })
        {
            Setup();
            var engine = new FakeMarshalThrowers();
            engine.Standard(1);
            var driver = Driver(engine);
            PassAt(driver, engine, 0.0);
            for (var i = 0; i < calls; i++)
                MarshalThrowerCost.AddPublished(ticksPerCall * System.Diagnostics.Stopwatch.Frequency / 1_000_000L, replaced: false);
            driver.Summarize();

            Assert.IsTrue(CheckLines().Any(l => Normal(l) == Normal(line)), string.Join("\n", CheckLines()));
            AssertInFixture(fixture, line);
        }
    }

    [TestMethod]
    public void EveryThrowerCheck_HasARowInTheManifest_WhosePassPatternMatchesTheLineItsFixtureHolds()
    {
        var toml = string.Join("\n", Toml());
        foreach (var (id, fixture) in new[]
                 {
                     (MarshalChecks.ThrowersStandId, "marshal_throwers_stand_pass.txt"), (MarshalChecks.ThrowersThrowId, "marshal_throwers_throw_pass.txt"),
                     (MarshalChecks.ThrowersMeleeId, "marshal_throwers_melee_pass.txt"), (MarshalChecks.ThrowersCostId, "marshal_throwers_cost_pass.txt"),
                 })
        {
            Assert.IsTrue(OutcomeLog.IsValidId(id), id);
            StringAssert.Contains(toml, "id = \"" + id + "\"", id);
            var pattern = new Regex(PassPatternOf(id));
            var line = Fixture(fixture).Single(l => l.Contains("id=" + id + " "));
            Assert.IsTrue(pattern.IsMatch(line.Substring(line.IndexOf("[INFO] ", StringComparison.Ordinal) + "[INFO] ".Length)), id + " pass pattern against " + fixture + ": " + line);
        }

        Assert.IsTrue(OutcomeLog.IsValidId(MarshalChecks.ThrowersPostfixCounterId));
        Assert.IsTrue(toml.Contains("[[human]]") && toml.Contains("the company stops short of the enemy, the javelins fly"));
    }

    [TestMethod]
    public void TheDriversFaultLine_IsInTheCodeAtTheErrorLevel_TheFixture_AndTheCapturedChecksFailList()
    {
        StringAssert.Contains(ReadSource("Main/Features/MarshalsWord/MarshalThrowerDriver.cs", stripComments: true), "_logger.LogError(\"[MarshalsWord] the thrower stand failed: \"");
        Assert.IsTrue(Fixture("marshal_throwers_stand_fault.txt").Any(l => l.Contains("] [ERROR] [MarshalsWord] the thrower stand failed")));
        Assert.IsTrue(Toml().Any(l => l.Contains(@"\[MarshalsWord\] the thrower stand failed")));
    }
}
