using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.SupplyLines.Hooks;

namespace TAOM.Tests.Features.SupplyLines;

/// <summary>
/// #1086 follow-up: a co-op guest's caravans are the host's, so the meeting line must not call the caravan theirs.
/// </summary>
[TestClass]
public class SupplyCaravanEncounterPatchTests
{
    [TestMethod]
    public void TheOwnersLine_CallsTheCaravanTheirs()
    {
        StringAssert.StartsWith(SupplyCaravanEncounterPatch.MeetingLine(defersToHost: false), "{=taom_sl_caravan_meet}Your ");
    }

    [TestMethod]
    public void AGuestsLine_HasItsOwnKey_AndDoesNotCallTheCaravanTheirs()
    {
        string line = SupplyCaravanEncounterPatch.MeetingLine(defersToHost: true);

        StringAssert.StartsWith(line, "{=taom_sl_caravan_meet_other}");
        Assert.IsFalse(line.Contains("Your") || line.Contains("your"), line);
    }
}
