using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Client.Features.SeatOfSeeing;
using TAOM.Core.Logging;
using TAOM.Features.SeatOfSeeing;
using TAOM.Features.SeatOfSeeing.Domain;
using TAOM.Tests.Features.LoadTimeStamps;
using static TAOM.Tests.Infrastructure.RepoPaths;

namespace TAOM.Tests.Features.SeatOfSeeing;

/// <summary>
/// The [Check] lines the Seat's session writes, read by tools/ingame_checks/0.toml. The session is driven against the scripted
/// engine and a real OutcomeLog, so the lines are the ones the game writes; the Python fixtures that
/// <c>SeatOfSeeingManifestTests</c> reads must not drift from them.
/// </summary>
[TestClass]
public class SeatOfSeeingChecksRenderTests
{
    private const string Fixtures = "ingame_verdict";
    private static readonly SeatPermissions Allowed = new(Camera: true, FightOn: true, Orders: true, Time: true, TakeSoldier: true);

    private IModLogger _logger = null!;
    private FakeSeatScreen _screen = null!;
    private FakeSeatHero _hero = null!;
    private FakeSeatSettings _settings = null!;
    private SeatPermissions _permissions = Allowed;
    private SeatSession _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        SeatPresence.Set(false);
        _logger = Substitute.For<IModLogger>();
        _screen = new FakeSeatScreen();
        _hero = new FakeSeatHero();
        _settings = new FakeSeatSettings();
        _sut = new SeatSession(_screen, _hero, _settings, () => _permissions, new OutcomeLog(_logger, new FakeStampClock(), () => 0), _logger, "1");
    }

    [TestCleanup]
    public void Cleanup()
    {
        SeatPresence.Set(false);
        SeatPresence.SetHeroFightsOn(false);
    }

    private void Key(bool enabled = true, SeatPermissions? permissions = null)
    {
        _permissions = permissions ?? Allowed;
        _settings.Enabled = enabled;
        _sut.OnScreenTick(true);
    }

    private string[] CheckLines() => Lines(nameof(IModLogger.LogInfo)).Where(l => l.StartsWith("[Check] ", StringComparison.Ordinal)).ToArray();

    private string[] Lines(string method) =>
        _logger.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == method)
            .Select(c => (string)c.GetArguments()[0])
            .ToArray();

    private static void AssertInFixture(string fixture, params string[] lines)
    {
        var path = RepoPath("tools", "tests", "fixtures", Fixtures, fixture);
        Assert.IsTrue(File.Exists(path), path);
        var all = File.ReadAllLines(path);
        foreach (var line in lines)
            Assert.IsTrue(all.Any(l => l.EndsWith("] [INFO] " + line, StringComparison.Ordinal)), fixture + " lacks the rendered line: " + line);
    }

    [TestMethod]
    public void CheckIds_AreValid()
    {
        foreach (var id in new[] { SeatChecks.EnterId, SeatChecks.LeaveId, SeatChecks.FramesId, SeatChecks.GlideId, SeatChecks.FaultId, SeatChecks.FrameCounterId,
                     SeatChecks.CursorId, SeatChecks.FocusId, SeatChecks.PanelReopenedId, SeatChecks.HideId, SeatChecks.KeepId, SeatChecks.LetGoId,
                     SeatChecks.FightOnId, SeatChecks.TakeId })
            Assert.IsTrue(OutcomeLog.IsValidId(id), id);
    }

    [TestMethod]
    public void RenderedLines_OfAVisitThroughStagesTwoAndThree_AreTheOnesTheManifestAndFixturesRead()
    {
        _settings.KeepPanelOpen = true;
        Key();
        while (_sut.State != SeatState.Seated)
            _sut.OnFrame(0.1f);

        _screen.PanelOpen = true;
        _sut.OnScreenTick(false);
        SeatPresence.MarkFocusSet();
        _sut.OnScreenTick(false);
        _screen.PanelOpen = false;
        _sut.OnScreenTick(false);
        _sut.PressHide();
        _screen.PanelOpen = true;
        _sut.OnScreenTick(false);
        _screen.PanelOpen = false;
        _sut.OnScreenTick(false);
        _screen.KeepResult = SeatKeepKind.Soldier;
        _sut.PressKeep();
        _sut.OnMissionTick();
        _screen.Kept = new SeatTargetRead(true, 0f, 0f, 0f);
        _sut.OnFrame(0.02f);
        while (_sut.Frames < 100)
            _sut.OnFrame(0.02f);
        Key();

        var visit = new[]
        {
            "[Check] id=taom.seat-of-seeing.enter result=pass mission=1 blend=1",
            "[Check] id=taom.seat-of-seeing.cursor result=pass mission=1 cursor=shown",
            "[Check] id=taom.seat-of-seeing.focus result=pass mission=1 focus=cursor",
            "[Check] id=taom.seat-of-seeing.panel-reopened result=pass mission=1 panel=reopened",
            "[Check] id=taom.seat-of-seeing.hide result=pass mission=1 interface=hidden",
            "[Check] id=taom.seat-of-seeing.hide result=pass mission=1 interface=restored reason=panel",
            "[Check] id=taom.seat-of-seeing.keep result=pass mission=1 kind=Soldier",
            "[Check] id=taom.seat-of-seeing.let-go result=pass mission=1 reason=gone kind=Soldier",
            "[Check] id=taom.seat-of-seeing.frames result=pass mission=1 frames=60",
            "[Check] id=taom.seat-of-seeing.leave result=pass mission=1 frames=100 glide=1 reason=key",
            "[Check] id=taom.seat-of-seeing.glide result=pass mission=1 elevation=set",
        };
        CollectionAssert.AreEqual(visit, CheckLines());
        AssertInFixture("seat_panel_visit_pass.txt", visit);
        AssertInFixture("seat_panel_reopen_site_missing.txt", visit[0]);
        AssertInFixture("seat_hide_stuck.txt", visit[0], visit[4]);
        AssertInFixture("seat_keep_and_flight.txt", visit[0]);
    }

    [TestMethod]
    public void RenderedLines_OfTheHandOffTheFallAndATake_AreTheOnesTheManifestAndFixturesRead()
    {
        SeatPresence.SetHeroFightsOn(false);
        _settings.HeroFightsOn = true;
        Key();
        while (_sut.State != SeatState.Seated)
            _sut.OnFrame(0.1f);
        _sut.OnMissionTick();
        _hero.RetreatPending = true;
        _sut.OnMissionTick();
        Key();
        _sut.OnMissionTick();
        Key();
        while (_sut.State != SeatState.Seated)
            _sut.OnFrame(0.1f);
        _sut.OnMissionTick();
        _hero.Active = false;
        _sut.OnMissionTick();
        _screen.TakeResult = SeatTakeResult.Taken(SeatTakeKind.Soldier, 3);
        _sut.PressTake();
        _sut.OnMissionTick();

        var lines = CheckLines().Where(l => l.Contains("fight-on") || l.Contains(".take ")).ToArray();
        var expected = new[]
        {
            "[Check] id=taom.seat-of-seeing.fight-on result=pass mission=1 event=begin",
            "[Check] id=taom.seat-of-seeing.fight-on result=pass mission=1 event=retreat-stopped",
            "[Check] id=taom.seat-of-seeing.fight-on result=pass mission=1 event=end",
            "[Check] id=taom.seat-of-seeing.take result=pass mission=1 kind=Soldier formations=3",
        };
        CollectionAssert.AreEqual(expected, lines.Where(l => !l.Contains("result=skip")).ToArray());
        CollectionAssert.Contains(lines, "[Check] id=taom.seat-of-seeing.fight-on result=skip mission=1 reason=fell");
        AssertInFixture("seat_take_visit_pass.txt", expected[0], expected[1], expected[2], expected[3], "[Check] id=taom.seat-of-seeing.fight-on result=skip mission=1 reason=fell");
        AssertInFixture("seat_panel_visit_pass.txt", expected[0], expected[2], expected[3]);
        AssertInFixture("seat_fight_visit_pass.txt", expected[0], expected[1], expected[2]);
        AssertInFixture("seat_fight_no_end.txt", expected[0]);
    }

    [TestMethod]
    public void RenderedLines_OfARefusedHandOffATakeThatFindsNobodyAndAThrow_AreTheOnesTheFixturesRead()
    {
        _settings.HeroFightsOn = true;
        _hero.BeginResult = SeatFightRefusal.NotPlayerControlled;
        Key();
        while (_sut.State != SeatState.Seated)
            _sut.OnFrame(0.1f);
        _sut.OnMissionTick();
        _screen.TakeResult = SeatTakeResult.Nobody();
        _sut.PressTake();
        _sut.OnMissionTick();
        _screen.TakeResult = SeatTakeResult.Refused(SeatTakeRefusal.Wounded);
        _sut.PressTake();
        _sut.OnMissionTick();
        _screen.TakeResult = SeatTakeResult.Taken(SeatTakeKind.Hero, 0);
        _sut.PressTake();
        _sut.OnMissionTick();

        var refused = "[Check] id=taom.seat-of-seeing.fight-on result=skip mission=1 reason=refused why=not-player";
        var nobody = "[Check] id=taom.seat-of-seeing.take result=skip mission=1 reason=nobody";
        var wounded = "[Check] id=taom.seat-of-seeing.take result=skip mission=1 reason=wounded";
        var noCommand = "[Check] id=taom.seat-of-seeing.take result=pass mission=1 kind=Hero formations=0";
        CollectionAssert.IsSubsetOf(new[] { refused, nobody, wounded, noCommand }, CheckLines());
        AssertInFixture("seat_fight_refused.txt", refused);
        AssertInFixture("seat_take_skips.txt", nobody, wounded);
        AssertInFixture("seat_take_no_command.txt", noCommand);

        // A throw from the hand-off writes the fail line.
        SeatPresence.Set(false);
        SeatPresence.SetHeroFightsOn(false);
        Setup();
        _settings.HeroFightsOn = true;
        _hero.BeginThrows = new InvalidOperationException("boom");
        Key();
        while (_sut.State != SeatState.Seated)
            _sut.OnFrame(0.1f);
        _sut.OnMissionTick();
        var thrown = "[Check] id=taom.seat-of-seeing.fight-on result=fail mission=1 error=InvalidOperationException";
        CollectionAssert.Contains(CheckLines(), thrown);
        AssertInFixture("seat_fight_throw.txt", thrown);
    }

    [TestMethod]
    public void RenderedLines_ForAReopenThatCannotReachTheViewModel_AndForAKeepThatFindsNobody()
    {
        _settings.KeepPanelOpen = true;
        _screen.ReopenWorks = false;
        Key();
        while (_sut.State != SeatState.Seated)
            _sut.OnFrame(0.1f);
        _screen.PanelOpen = true;
        _sut.OnScreenTick(false);
        _screen.PanelOpen = false;
        _sut.OnScreenTick(false);
        _screen.KeepResult = SeatKeepKind.None;
        _sut.PressKeep();
        _sut.OnMissionTick();

        var lines = CheckLines();
        CollectionAssert.Contains(lines, "[Check] id=taom.seat-of-seeing.panel-reopened result=fail mission=1 reason=site");
        CollectionAssert.Contains(lines, "[Check] id=taom.seat-of-seeing.keep result=skip mission=1 reason=nobody");
        AssertInFixture("seat_panel_reopen_site_missing.txt", "[Check] id=taom.seat-of-seeing.panel-reopened result=fail mission=1 reason=site");
        AssertInFixture("seat_keep_nobody.txt", "[Check] id=taom.seat-of-seeing.keep result=skip mission=1 reason=nobody");
    }

    [TestMethod]
    public void RenderedLines_AreTheOnesTheSeatManifestAndFixturesRead()
    {
        // A full visit: in, 420 owned frames, out with the glide armed.
        Key();
        for (var i = 0; i < 420; i++)
            _sut.OnFrame(0.02f);
        Key();

        var visit = new[]
        {
            "[Check] id=taom.seat-of-seeing.enter result=pass mission=1 blend=1",
            "[Check] id=taom.seat-of-seeing.frames result=pass mission=1 frames=60",
            "[Check] id=taom.seat-of-seeing.leave result=pass mission=1 frames=420 glide=1 reason=key",
            "[Check] id=taom.seat-of-seeing.glide result=pass mission=1 elevation=set",
        };
        CollectionAssert.AreEqual(visit, CheckLines());
        AssertInFixture("seat_visit_pass.txt", visit);
        AssertInFixture("seat_elevation_missing.txt", visit[0], visit[1], visit[2]);
        AssertInFixture("seat_enter_only.txt", visit[0]);
    }

    [TestMethod]
    public void RenderedLines_ForAFlightLetGo_IsTheLineTheFixtureReads()
    {
        Key();
        while (_sut.State != SeatState.Seated)
            _sut.OnFrame(0.1f);
        _sut.OnFrame(0.02f);
        _screen.KeepResult = SeatKeepKind.Company;
        _sut.PressKeep();
        _sut.OnMissionTick();
        _screen.Input = new SeatInput(0f, 1f, 0f, 0f, 0f, 0f, false);
        _sut.OnFrame(0.02f);

        var lines = CheckLines();
        CollectionAssert.Contains(lines, "[Check] id=taom.seat-of-seeing.keep result=pass mission=1 kind=Company");
        CollectionAssert.Contains(lines, "[Check] id=taom.seat-of-seeing.let-go result=pass mission=1 reason=flight kind=Company");
        AssertInFixture("seat_keep_and_flight.txt", "[Check] id=taom.seat-of-seeing.keep result=pass mission=1 kind=Company",
            "[Check] id=taom.seat-of-seeing.let-go result=pass mission=1 reason=flight kind=Company");
    }

    [TestMethod]
    public void RenderedLines_ForAReopenTheEngineRefused_IsTheSkipLineTheFixtureReads()
    {
        _settings.KeepPanelOpen = true;
        _screen.EngineRefusesReopen = true;
        Key();
        while (_sut.State != SeatState.Seated)
            _sut.OnFrame(0.1f);
        _screen.PanelOpen = true;
        _sut.OnScreenTick(false);
        _screen.PanelOpen = false;
        _sut.OnScreenTick(false);

        var refused = "[Check] id=taom.seat-of-seeing.panel-reopened result=skip mission=1 reason=refused";
        CollectionAssert.Contains(CheckLines(), refused);
        CollectionAssert.DoesNotContain(CheckLines(), "[Check] id=taom.seat-of-seeing.panel-reopened result=pass mission=1 panel=reopened");
        AssertInFixture("seat_panel_reopen_refused.txt", refused);
    }

    [TestMethod]
    public void RenderedLines_ForAKeepThenALeave_AreTheOnesTheFixtureReads()
    {
        Key();
        while (_sut.State != SeatState.Seated)
            _sut.OnFrame(0.1f);
        _sut.OnFrame(0.02f);
        _screen.KeepResult = SeatKeepKind.Company;
        _sut.PressKeep();
        _sut.OnMissionTick();
        Key();

        var kept = "[Check] id=taom.seat-of-seeing.keep result=pass mission=1 kind=Company";
        var left = "[Check] id=taom.seat-of-seeing.let-go result=pass mission=1 reason=leave kind=Company";
        CollectionAssert.Contains(CheckLines(), kept);
        CollectionAssert.Contains(CheckLines(), left);
        AssertInFixture("seat_keep_then_leave.txt", kept, left);
    }

    [TestMethod]
    public void RenderedLines_ForAMissingElevationSetter_AndForNoAgentToFollow()
    {
        Setup();
        _screen.LookWritten = false;
        Key();
        _sut.OnFrame(0.1f);
        Key();

        var missing = "[Check] id=taom.seat-of-seeing.glide result=fail mission=1 elevation=missing";
        CollectionAssert.Contains(CheckLines(), missing);
        AssertInFixture("seat_elevation_missing.txt", missing);

        Setup();
        _screen.GlideBegins = false;
        Key();
        _sut.OnFrame(0.1f);
        Key();

        var noAgent = new[]
        {
            "[Check] id=taom.seat-of-seeing.leave result=pass mission=1 frames=1 glide=0 reason=key",
            "[Check] id=taom.seat-of-seeing.glide result=skip mission=1 reason=no-agent",
        };
        CollectionAssert.IsSubsetOf(noAgent, CheckLines());
        AssertInFixture("seat_visit_no_agent.txt", noAgent[1]);
        // The fixture's visit lasted 420 frames; this one lasted 1, so only the shape of the leave line is compared.
        StringAssert.Matches(File.ReadAllText(RepoPath("tools", "tests", "fixtures", Fixtures, "seat_visit_no_agent.txt")),
            new System.Text.RegularExpressions.Regex(@"leave result=pass mission=1 frames=[0-9]+ glide=0 reason=key"));
    }

    [TestMethod]
    public void RenderedLines_ForTheFaultLatch()
    {
        Key();
        _screen.ApplyThrows = new InvalidOperationException("boom");

        _sut.OnFrame(0.1f);

        var fault = "[Check] id=taom.seat-of-seeing.fault result=fail mission=1 error=InvalidOperationException";
        CollectionAssert.Contains(CheckLines(), fault);
        AssertInFixture("seat_fault.txt", fault);

        var error = Lines(nameof(IModLogger.LogError)).Single();
        const string prefix = "[SeatOfSeeing] the Seat failed and is off for this mission, the hero's own camera restored: System.InvalidOperationException: boom";
        StringAssert.StartsWith(error, prefix);
        var all = File.ReadAllLines(RepoPath("tools", "tests", "fixtures", Fixtures, "seat_fault.txt"));
        Assert.IsTrue(all.Any(l => l.EndsWith("] [ERROR] " + prefix, StringComparison.Ordinal)));
    }

    [TestMethod]
    public void RenderedLines_ForARefusedKeyPress_OneSkipLinePerReason()
    {
        Key(enabled: false);
        Key(permissions: default(SeatPermissions));
        _screen.Facts = _screen.Facts with { RageHoldsHero = true };
        Key();
        _screen.Facts = _screen.Facts with { RageHoldsHero = false, CustomCamera = true };
        Key();

        var skips = new[]
        {
            "[Check] id=taom.seat-of-seeing.enter result=skip mission=1 reason=setting-off",
            "[Check] id=taom.seat-of-seeing.enter result=skip mission=1 reason=denied",
            "[Check] id=taom.seat-of-seeing.enter result=skip mission=1 reason=rage",
            "[Check] id=taom.seat-of-seeing.enter result=skip mission=1 reason=custom-camera",
        };
        CollectionAssert.AreEqual(skips, CheckLines());
        AssertInFixture("seat_skips_only.txt", skips);
    }

    [TestMethod]
    public void RenderedLines_ForAnEntryThatCannotRead()
    {
        _screen.Ground = float.NaN;

        Key();

        var failed = "[Check] id=taom.seat-of-seeing.enter result=fail mission=1 reason=no-ground";
        CollectionAssert.AreEqual(new[] { failed }, CheckLines());
        AssertInFixture("seat_enter_failed.txt", failed);
    }

    [DataTestMethod]
    [DataRow("seat_could_not_start.txt", "Client/Features/SeatOfSeeing/SeatView.cs", "[SeatOfSeeing] the Seat could not start")]
    [DataRow("seat_hotkey_failed.txt", "Main/Features/SeatOfSeeing/SeatOfSeeingModule.cs", "[SeatOfSeeing] hotkey registration failed")]
    [DataRow("seat_order_ray_failed.txt", "Client/Features/SeatOfSeeing/Hooks/Patch140_OrderFlagGetCollidedEntity.cs", "[SeatOfSeeing] order ray failed")]
    [DataRow("seat_order_panel_anchor.txt", "Client/Features/SeatOfSeeing/Hooks/SeatOrderPanelGate.cs", "[SeatOfSeeing] expected exactly one Agent.IsPlayerControlled read")]
    public void TheFeaturesOwnFailureLines_AreInTheCode_AndInTheFixtures(string fixture, string source, string prefix)
    {
        var code = ReadSource(source, stripComments: true);
        StringAssert.Contains(code, prefix, source);
        Assert.IsTrue(System.Text.RegularExpressions.Regex.IsMatch(code, @"LogError\(\s*\$""" + System.Text.RegularExpressions.Regex.Escape(prefix)),
            source + " must write " + prefix + " at LogError");

        var path = RepoPath("tools", "tests", "fixtures", Fixtures, fixture);
        Assert.IsTrue(File.ReadAllLines(path).Any(l => l.Contains("] [ERROR] " + prefix)), fixture);
    }
}
