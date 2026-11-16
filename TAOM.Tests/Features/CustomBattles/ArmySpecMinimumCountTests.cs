using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.CustomBattles.Muster;

namespace TAOM.Tests.Features.CustomBattles;

/// <summary>
/// The fewest men a muster unit can have for its roles (#1297). The banner bearer and the horn blower are soldiers of the
/// unit; the captain is another troop that takes one of the unit's number, so a unit with a captain needs one more man,
/// or MusterArmyBuilder adds no soldier of the unit at all.
/// </summary>
[TestClass]
public class ArmySpecMinimumCountTests
{
    [TestMethod]
    [DataRow(false, false, false, 1)]
    [DataRow(true, false, false, 1)]
    [DataRow(true, false, true, 2)]
    [DataRow(false, true, false, 2)]
    [DataRow(true, true, false, 2)]
    [DataRow(true, true, true, 3)]
    public void MinimumCount_LeavesAtLeastOneSoldierOfTheUnit(bool banner, bool captain, bool horn, int expected) =>
        Assert.AreEqual(expected, ArmySpec.MinimumCount(banner, captain, horn));
}
