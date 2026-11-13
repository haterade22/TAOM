using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Core.Logging;
using TAOM.Features.LongDefeat.Diagnostics;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Tests.Features.LongDefeat.Diagnostics;

/// <summary>The two verdicts of the engines a wave start brings back (#1160): <c>engine-restore</c> reads each one back at its start, and <c>engine-use</c> asks at the clear whether the attackers used it.</summary>
[TestClass]
public class LongDefeatEngineChecksTests
{
    private static LdEngineState Ok(string kind = "tower", int index = 0) =>
        new(kind, index, Destroyed: false, Disabled: false, HitPoints: 5000f, MaxHitPoints: 5000f, Traveled: 0f, Arrived: false, InLane: true, Done: false, QueuesOff: true, WaitAtStart: true);

    private static LdEngineRestore Restore(params LdEngineState[] engines) => new(engines);

    // ---- engine-restore ----

    [TestMethod]
    public void Restored_EveryEngineBackAtItsStart_Passes_AndNamesEachOne()
    {
        var v = LongDefeatEngineChecks.Restored(2, Restore(Ok("tower", 0), Ok("ram", 0)));

        Assert.AreEqual(CheckResult.Pass, v.Result, v.Detail);
        Assert.AreEqual("w=2 restored=2 tower0=ok ram0=ok", v.Detail);
    }

    [DataTestMethod]
    [DataRow("destroyed")]
    [DataRow("disabled")]
    [DataRow("hp")]
    [DataRow("moved")]
    [DataRow("lane")]
    [DataRow("ladders")]
    [DataRow("wait")]
    public void Restored_AnEngineNotBackAtItsStart_Fails_AndNamesWhy(string fault)
    {
        var bad = fault switch
        {
            "destroyed" => Ok() with { Destroyed = true, HitPoints = 0f },
            "disabled" => Ok() with { Disabled = true },
            "hp" => Ok() with { HitPoints = 2500f },
            "moved" => Ok() with { Traveled = 40f },
            "lane" => Ok() with { InLane = false },
            "ladders" => Ok() with { QueuesOff = false },
            _ => Ok() with { WaitAtStart = false },
        };

        var v = LongDefeatEngineChecks.Restored(3, Restore(bad, Ok("ram", 0)));

        Assert.AreEqual(CheckResult.Fail, v.Result, v.Detail);
        StringAssert.Contains(v.Detail, "tower0=" + fault);
        StringAssert.Contains(v.Detail, "ram0=ok");
    }

    [TestMethod]
    public void Restored_ADestroyedEngine_NamesEveryFaultItHas()
    {
        var v = LongDefeatEngineChecks.Restored(2, Restore(Ok() with { Destroyed = true, Disabled = true, HitPoints = 0f }));

        StringAssert.Contains(v.Detail, "tower0=destroyed,disabled,hp");
    }

    [DataTestMethod]
    [DataRow(float.NaN, 5000f, 0f)]
    [DataRow(5000f, float.NaN, 0f)]
    [DataRow(0f, 0f, 0f)]
    [DataRow(5000f, 5000f, float.NaN)]
    public void Restored_ANumberThatIsNotAReading_Fails(float hitPoints, float max, float traveled)
    {
        var v = LongDefeatEngineChecks.Restored(2, Restore(Ok() with { HitPoints = hitPoints, MaxHitPoints = max, Traveled = traveled }));

        Assert.AreEqual(CheckResult.Fail, v.Result, v.Detail);
    }

    [TestMethod]
    public void Restored_AProblem_FailsEvenWithNoEngine_AndKeepsItOneWord()
    {
        var v = LongDefeatEngineChecks.Restored(2, new LdEngineRestore(Array.Empty<LdEngineState>(), "SiegeTower.OnMissionReset was not found"));

        Assert.AreEqual(CheckResult.Fail, v.Result);
        Assert.AreEqual("w=2 restored=0 problem=SiegeTower.OnMissionReset_was_not_found", v.Detail);
    }

    // ---- engine-use ----

    [TestMethod]
    public void Used_EachRestoredEngineMovedArrivedOrFellAgain_Passes()
    {
        var restored = new[] { Ok("tower", 0), Ok("tower", 1), Ok("ram", 0) };
        var now = new[]
        {
            Ok("tower", 0) with { Traveled = 31.24f },
            Ok("tower", 1) with { Traveled = 2f, Arrived = true },
            Ok("ram", 0) with { Destroyed = true, HitPoints = 0f, Traveled = 12f },
        };

        var v = LongDefeatEngineChecks.Used(2, restored, now, runEnded: false);

        Assert.AreEqual(CheckResult.Pass, v.Result, v.Detail);
        Assert.AreEqual("w=2 restored=3 tower0=used,31.2 tower1=arrived,2.0 ram0=fell,12.0", v.Detail);
    }

    [TestMethod]
    public void Used_AnEngineThatFellBeforeItMoved_ShowsNoUse_AndCountsForNothing()
    {
        var restored = new[] { Ok("tower", 0), Ok("ram", 0) };
        var now = new[] { Ok("tower", 0) with { Traveled = 30f }, Ok("ram", 0) with { Destroyed = true, HitPoints = 0f, Traveled = 4.9f } };

        var v = LongDefeatEngineChecks.Used(2, restored, now, runEnded: false);
        var alone = LongDefeatEngineChecks.Used(2, new[] { Ok("ram", 0) }, new[] { Ok("ram", 0) with { Destroyed = true, HitPoints = 0f, Traveled = 0f } }, runEnded: false);

        Assert.AreEqual(CheckResult.Pass, v.Result, v.Detail);
        StringAssert.Contains(v.Detail, "ram0=fell-early,4.9");
        Assert.AreEqual(CheckResult.Skip, alone.Result, "an engine destroyed before it moved shows nothing about the attackers' use");
        Assert.AreEqual("w=2 restored=1 ram0=fell-early,0.0 reason=unjudged", alone.Detail);
    }

    [TestMethod]
    public void Used_AnEngineThatStayedAtItsStart_Fails()
    {
        var v = LongDefeatEngineChecks.Used(2, new[] { Ok("tower", 0), Ok("ram", 0) }, new[] { Ok("tower", 0) with { Traveled = 4.9f }, Ok("ram", 0) with { Traveled = 12f } }, runEnded: false);

        Assert.AreEqual(CheckResult.Fail, v.Result, v.Detail);
        StringAssert.Contains(v.Detail, "tower0=idle,4.9");
        StringAssert.Contains(v.Detail, "ram0=used,12.0");
    }

    [TestMethod]
    public void Used_ANaNDistance_IsIdle()
    {
        var v = LongDefeatEngineChecks.Used(2, new[] { Ok() }, new[] { Ok() with { Traveled = float.NaN } }, runEnded: false);

        Assert.AreEqual(CheckResult.Fail, v.Result, v.Detail);
        StringAssert.Contains(v.Detail, "tower0=idle,NaN");
    }

    [TestMethod]
    public void Used_AnEngineTheFieldNoLongerHolds_Fails_AsGone()
    {
        var v = LongDefeatEngineChecks.Used(2, new[] { Ok("ram", 0) }, Array.Empty<LdEngineState>(), runEnded: false);

        Assert.AreEqual(CheckResult.Fail, v.Result);
        StringAssert.Contains(v.Detail, "ram0=gone");
    }

    [TestMethod]
    public void Used_ARamWhoseGateWasDownAtTheRestore_IsDone_AndCountsForNothing()
    {
        var restored = new[] { Ok("tower", 0), Ok("ram", 0) with { Done = true } };
        var now = new[] { Ok("tower", 0) with { Arrived = true, Traveled = 48f }, Ok("ram", 0) };

        var v = LongDefeatEngineChecks.Used(2, restored, now, runEnded: false);

        Assert.AreEqual(CheckResult.Pass, v.Result, v.Detail);
        StringAssert.Contains(v.Detail, "ram0=done");
    }

    [TestMethod]
    public void Used_EveryEngineDone_IsASkip()
    {
        var v = LongDefeatEngineChecks.Used(2, new[] { Ok("ram", 0) with { Done = true } }, new[] { Ok("ram", 0) }, runEnded: false);

        Assert.AreEqual(CheckResult.Skip, v.Result);
        StringAssert.EndsWith(v.Detail, " reason=unjudged");
    }

    [TestMethod]
    public void Used_TheRunEndedInsideTheWave_AnIdleEngineIsASkip_AndAUsedOneStillPasses()
    {
        var idle = LongDefeatEngineChecks.Used(2, new[] { Ok() }, new[] { Ok() }, runEnded: true);
        var used = LongDefeatEngineChecks.Used(2, new[] { Ok() }, new[] { Ok() with { Traveled = 9f } }, runEnded: true);

        Assert.AreEqual(CheckResult.Skip, idle.Result);
        StringAssert.EndsWith(idle.Detail, " reason=run-ended");
        Assert.AreEqual(CheckResult.Pass, used.Result, used.Detail);
    }
}
