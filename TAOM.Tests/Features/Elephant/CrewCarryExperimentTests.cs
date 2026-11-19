using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Elephant;

namespace TAOM.Tests.Features.Elephant;

/// <summary>
/// TEMPORARY (#1482): the seat-to-arm map of the one measuring build. Deleted with CrewCarryExperiment once the
/// run has picked how a crew is carried. It is tested because a wrong map costs an in-game round to notice.
/// </summary>
[TestClass]
public class CrewCarryExperimentTests
{
    private static readonly string[] MumakilSeats =
    {
        "mumakil_crew_main_0", "mumakil_crew_main_1", "mumakil_crew_main_2", "mumakil_crew_main_3",
        "mumakil_crew_main_4", "mumakil_crew_upper_0", "mumakil_crew_upper_1", "mumakil_crew_nest_0",
    };

    [TestMethod]
    public void TheMumakilSeats_CoverEveryArm()
    {
        var arms = MumakilSeats.Select(CrewCarryExperiment.ForMumakilSeat).Distinct().ToList();

        foreach (CrewCarryArm arm in Enum.GetValues(typeof(CrewCarryArm)))
            Assert.IsTrue(arms.Contains(arm), $"no mumakil seat tests {arm}");
    }

    [TestMethod]
    public void TheNest_StaysTheControl_AndTheMirrorPairSplitsAttachAgainstBaseline()
    {
        Assert.AreEqual(CrewCarryArm.Baseline, CrewCarryExperiment.ForMumakilSeat("mumakil_crew_nest_0"));
        // main_1 and main_4 are mirror images across the beast, so they see the same lever arm and sightline.
        Assert.AreEqual(CrewCarryArm.ForceAttach, CrewCarryExperiment.ForMumakilSeat("mumakil_crew_main_1"));
        Assert.AreEqual(CrewCarryArm.Baseline, CrewCarryExperiment.ForMumakilSeat("mumakil_crew_main_4"));
    }

    [TestMethod]
    public void AnUnknownSeat_KeepsTodaysBehaviour()
    {
        Assert.AreEqual(CrewCarryArm.Baseline, CrewCarryExperiment.ForMumakilSeat("some_new_seat"));
        Assert.AreEqual(CrewCarryArm.Baseline, CrewCarryExperiment.ForMumakilSeat(null));
        Assert.AreEqual(CrewCarryArm.Baseline, CrewCarryExperiment.ForHowdahSeat("some_new_seat"));
        Assert.AreEqual(CrewCarryArm.Baseline, CrewCarryExperiment.ForHowdahSeat(null));
    }

    [TestMethod]
    public void TheHowdah_TestsAttachAgainstBaseline()
    {
        Assert.AreEqual(CrewCarryArm.Baseline, CrewCarryExperiment.ForHowdahSeat("howdah_crew_front"));
        Assert.AreEqual(CrewCarryArm.ForceAttach, CrewCarryExperiment.ForHowdahSeat("howdah_crew_back"));
    }

    [TestMethod]
    public void TheNonTeleportBlock_AlternatesEveryFourStatusPeriods()
    {
        // Four 5 s status periods is 20 s, and a block edge always falls on a status line.
        for (int period = 0; period < 4; period++)
            Assert.IsFalse(CrewCarryExperiment.NonTeleportBlock(period), $"period {period}");
        for (int period = 4; period < 8; period++)
            Assert.IsTrue(CrewCarryExperiment.NonTeleportBlock(period), $"period {period}");
        Assert.IsFalse(CrewCarryExperiment.NonTeleportBlock(8));
        Assert.IsFalse(CrewCarryExperiment.NonTeleportBlock(-1));
    }
}
