using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Enlistment;

namespace TAOM.Tests.Features.Enlistment;

/// <summary>
/// #831: a commander raiding a village (aggressor in a raid, forced supplies or forced volunteers)
/// is in a map event the soldier does not join. Everything else in a map event still joins.
/// </summary>
[TestClass]
public class VillageActionJoinPolicyTests
{
    [DataTestMethod]
    [DataRow(false, false, false, DisplayName = "no map event -> not joinable")]
    [DataRow(false, true, false, DisplayName = "no map event, stale raiding flag -> not joinable")]
    [DataRow(true, false, true, DisplayName = "map event, not raiding -> joinable")]
    [DataRow(true, true, false, DisplayName = "map event, raiding -> not joinable")]
    public void IsJoinableBattle_NeedsAMapEventAndNoRaid(bool inMapEvent, bool raiding, bool expected)
    {
        Assert.AreEqual(expected, VillageActionJoinPolicy.IsJoinableBattle(inMapEvent, raiding));
    }
}
