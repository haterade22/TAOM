using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.SeatOfSeeing;

/// <summary>
/// The wiring of stages S6 and S7 (build-spec sections 10 and 11) as source-text pins. The screen adapter and the view are engine entry points that need a
/// live mission, so the writes they make are pinned by their text, and the tests of the session cover everything the fake can show.
/// </summary>
[TestClass]
public class SeatTimeRiseWiringTests
{
    private static string Source(string path) => RepoPaths.ReadSource(path, stripComments: true);

    private static string Flat(string text) => Regex.Replace(text, @"\s+", " ");

    private static string Adapter() => Flat(Source("Client/Adapters/SeatScreenAdapter.cs"));

    private static string Session() => Flat(Source("Client/Features/SeatOfSeeing/SeatSession.cs"));

    private static string Time() => Flat(Source("Client/Features/SeatOfSeeing/SeatTimeControl.cs"));

    // ---- The writes ----

    [TestMethod]
    public void TheAdapter_CallsFordsMissionSpeedWithANullLogger_SoABattleWritesNoFordsOfWarSpeedLines()
    {
        var adapter = Adapter();

        StringAssert.Contains(adapter, "private readonly FordsMissionSpeed _clock = new(null);");
        StringAssert.Contains(adapter, "_clock.Apply(_mission, _clockLevel, realDt);");
        StringAssert.Contains(adapter, "FordsMissionSpeed.Stop(_mission);");
    }

    [TestMethod]
    public void TheSlowRequest_UsesTheSeatsOneIdOnEveryCall()
    {
        var adapter = Adapter();

        StringAssert.Contains(adapter, "new Mission.TimeSpeedRequest(factor, SeatTimeLadder.RequestId)");
        StringAssert.Contains(adapter, "_mission.RemoveTimeSpeedRequest(SeatTimeLadder.RequestId);");
        StringAssert.Contains(adapter, "_mission.GetRequestedTimeSpeed(SeatTimeLadder.RequestId, out _)");
    }

    [TestMethod]
    public void TheRemoveOfTheRequest_IsCalledOnlyAfterAReadThatFoundIt()
    {
        var time = Time();

        Assert.AreEqual(1, Regex.Matches(time, @"RemoveSlowRequest\(\)").Count, "one call site");
        StringAssert.Contains(time, "if (_screen.ReadTime().SlowRequest) _screen.RemoveSlowRequest();");
        Assert.AreEqual(1, Regex.Matches(time, @"AddSlowRequest\(").Count);
        Assert.IsTrue(time.IndexOf("ClearRequest(); Index = next;", System.StringComparison.Ordinal) < time.IndexOf("_screen.AddSlowRequest(", System.StringComparison.Ordinal),
            "the request is cleared before the new one is added, because adding does not replace");
    }

    [TestMethod]
    public void NoOtherTaomCode_WritesATimeSpeedRequestTheFlagsOrThePause()
    {
        var offenders = RepoPaths.CSharpFiles("Main", "Client", "Dependencies")
            .Where(f => !f.EndsWith("SeatScreenAdapter.cs") && !f.EndsWith("FordsMissionSpeed.cs") && !f.EndsWith("LongDefeatMissionSpeed.cs") && !f.EndsWith("CommandTrialHost.Seat.cs"))
            .Where(f => Regex.IsMatch(RepoPaths.StripComments(RepoPaths.Text(f)),
                @"AddTimeSpeedRequest|RemoveTimeSpeedRequest|SetFastForwardingFromUI|\.FixedDeltaTimeMode\s*=|\.FixedDeltaTime\s*=|MissionState\.Current[^;]*\.Paused\s*="))
            .Select(System.IO.Path.GetFileName)
            .ToList();

        CollectionAssert.AreEqual(new string[0], offenders, "the Seat's adapter, the Fords speed writer and the Long Defeat one are the only writers; a fourth needs a conflict read");
    }

    [TestMethod]
    public void ThePause_IsWrittenOnTheCurrentMissionStateOnly()
    {
        StringAssert.Contains(Adapter(), "var state = MissionState.Current; if (state != null) state.Paused = paused;");
    }

    [TestMethod]
    public void TheClockOfAnotherView_IsTheLongDefeatMissionLogicsPresence()
    {
        StringAssert.Contains(Adapter(), "using TAOM.Features.LongDefeat.Hooks;");
        StringAssert.Contains(Adapter(), "public bool ClockOwnedElsewhere() => _mission.GetMissionBehavior<LongDefeatMissionLogic>() != null;");
    }

    [TestMethod]
    public void EveryNativeWriteOfTimeAndTheLift_CallsTheThreadGuard()
    {
        var adapter = Adapter();

        StringAssert.Contains(adapter, "_report = logger.LogError;");
        foreach (var site in new[] { "ApplyFastTime", "StopFastTime", "AddSlowRequest", "RemoveSlowRequest", "SetPanelLift" })
            StringAssert.Contains(adapter, "MissionThreadGuard.NoteCall(\"SeatScreenAdapter." + site + "\", _report);");
    }

    [TestMethod]
    public void TheFordsSpeedWriter_WritesTheFlagOnlyWhenItDiffers_AndTheFixedStepEveryCall()
    {
        var writer = Flat(Source("Main/Adapters/FordsMissionSpeed.cs"));

        StringAssert.Contains(writer, "if (mission.IsFastForward != fastForward) mission.SetFastForwardingFromUI(fastForward);",
            "level 10 asks for the flag already up, so another writer's flag is left alone and their raise is not written back");
        StringAssert.Contains(writer, "mission.FixedDeltaTimeMode = frame.FixedStep > 0f;",
            "and the fixed step is written on every call, so level 10 turns the step off whatever the flag did");
        StringAssert.Contains(writer, "mission.FixedDeltaTime = frame.FixedStep;");
    }

    [TestMethod]
    public void TheTrialHost_WritesTheEnginesFlagInOnePlaceOnly()
    {
        var host = Flat(Source("Client/Features/SeatOfSeeing/Trial/CommandTrialHost.Seat.cs"));

        Assert.AreEqual(1, Regex.Matches(host, @"SetFastForwardingFromUI\(").Count);
        StringAssert.Contains(host, "public void SetOtherFastForward(bool raised) { NoteWrite(\"CommandTrialHost.SetOtherFastForward\"); _mission.SetFastForwardingFromUI(raised); }");
    }

    // ---- The lift ----

    [TestMethod]
    public void TheLift_WritesVec3ZeroForZero_AndTheOffsetIsWrittenByNoOtherTaomCode()
    {
        StringAssert.Contains(Adapter(), "_mission.SetCustomCameraTargetLocalOffset(metres == 0f ? Vec3.Zero : new Vec3(0f, 0f, metres, -1f));");

        var writers = RepoPaths.CSharpFiles("Main", "Client", "Dependencies")
            .Where(f => RepoPaths.StripComments(RepoPaths.Text(f)).Contains("SetCustomCameraTargetLocalOffset"))
            .Select(System.IO.Path.GetFileName)
            .ToList();
        CollectionAssert.AreEqual(new[] { "SeatScreenAdapter.cs" }, writers, "the property has one writer in the repo, which is why the sink needs no saved value");
    }

    [TestMethod]
    public void TheRise_IsNotBehindTheSessionsFaultReturn_AndFinishWritesNoLift()
    {
        var session = Session();

        StringAssert.Contains(session, "internal void OnFrameTick(float dt) { try { _time.OnFrame(dt); }");
        Assert.AreEqual(1, Regex.Matches(session, @"_rise\.OnTick\(").Count);
        StringAssert.Contains(session, "Swallow(_rise.Zero);");
        StringAssert.Contains(session, "_rise.Zero(); _controllerShut = true;");

        var finish = session.Substring(session.IndexOf("internal void Finish()", System.StringComparison.Ordinal), 700);
        Assert.IsFalse(finish.Contains("_rise"), "Finish writes nothing: the offset lives on the Mission instance and a new mission builds a new one");
    }

    [TestMethod]
    public void TheFinish_CancelsTimeOutsideTheStateTestAndAfterItsTry()
    {
        var session = Session();
        var finish = session.Substring(session.IndexOf("internal void Finish()", System.StringComparison.Ordinal), 700);

        Assert.IsTrue(finish.IndexOf("catch (Exception ex) { Fault(ex); } _time.Cancel();", System.StringComparison.Ordinal) > 0, "the cancel follows the try, so a throw in the leave cannot skip it");
        Assert.IsFalse(Regex.IsMatch(finish, @"if \(State != SeatState\.Off\) [^}]*_time\.Cancel"), "and sits outside the state test");
        StringAssert.Contains(session, "Swallow(_time.Cancel);");
    }

    [TestMethod]
    public void TheLeave_StepsTenDownExceptOnFinish_AndTheTakeStopsTheFixedStep()
    {
        var session = Session();

        StringAssert.Contains(session, "if (reason != \"finish\") _time.OnLeave();");
        StringAssert.Contains(session, "case SeatTakeOutcome.Taken: _fallSaid = false; _time.OnTake();");
    }

    // ---- The keys ----

    [TestMethod]
    public void ATimeKey_ReadsTheModeBeforeAnySetting_SoAnInterludeStaysSilent()
    {
        var time = Time();
        var press = time.Substring(time.IndexOf("internal void Press(", System.StringComparison.Ordinal), 500);

        Assert.IsTrue(press.IndexOf("ReadFacts().BattleMode", System.StringComparison.Ordinal) < press.IndexOf("_settings.Enabled", System.StringComparison.Ordinal));
        Assert.IsTrue(press.IndexOf("_settings.Enabled", System.StringComparison.Ordinal) < press.IndexOf("_permissions().Time", System.StringComparison.Ordinal));
    }

    [TestMethod]
    public void TheTimeKeys_ActFromTheScreenTickOnly()
    {
        var view = Flat(Source("Client/Features/SeatOfSeeing/SeatView.cs"));
        var keys = Flat(Source("Client/Features/SeatOfSeeing/SeatKeys.cs"));

        Assert.IsFalse(view.Contains("PressTime"), "the view forwards nothing of time: SeatKeys does, once a screen tick");
        Assert.AreEqual(3, Regex.Matches(keys, @"_session\.PressTime\(").Count);
        var missionTick = view.Substring(view.IndexOf("public override void OnMissionTick", System.StringComparison.Ordinal), 260);
        Assert.IsFalse(missionTick.Contains("Time"), "fast time repeats the mission tick, and a key read in each could act more than once");
    }
}
