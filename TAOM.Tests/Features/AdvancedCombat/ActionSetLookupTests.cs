using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.AdvancedCombat;

// #1065: on v1.5.6 MBActionSet.GetActionSet returns set 0 (as_human_warrior, a VALID set) for an id it cannot
// find, so IsValid alone never fires. The drift guards compare the looked-up name with the requested id.
// See docs/reference/bannerlord-animation-system-map.md section 7.

namespace TAOM.Tests.Features.AdvancedCombat;

[TestClass]
public class ActionSetLookupTests
{
    [TestMethod]
    public void Found_ValidSetWithRequestedName_IsTrue()
        => Assert.IsTrue(ActionSetLookup.Found("as_war_ram", true, "as_war_ram"));

    [TestMethod]
    public void Found_MissingIdFallsBackToSetZero_IsFalse()
        => Assert.IsFalse(ActionSetLookup.Found("as_war_ram", true, "as_human_warrior"));

    [TestMethod]
    public void Found_InvalidSet_IsFalse()
        => Assert.IsFalse(ActionSetLookup.Found("as_war_ram", false, null));

    [TestMethod]
    public void Found_InvalidSetEvenWithMatchingName_IsFalse()
        => Assert.IsFalse(ActionSetLookup.Found("as_war_ram", false, "as_war_ram"));

    // MBActionSet.GetName() returns "Invalid" for an invalid set (v1.5.6), which the call sites pass as is.
    [TestMethod]
    public void Found_InvalidSetReportsTheEnginesInvalidName_IsFalse()
        => Assert.IsFalse(ActionSetLookup.Found("as_war_ram", false, "Invalid"));

    [TestMethod]
    public void Found_NullName_IsFalse()
        => Assert.IsFalse(ActionSetLookup.Found("as_war_ram", true, null));

    [TestMethod]
    public void Found_NullRequestedId_IsFalse()
        => Assert.IsFalse(ActionSetLookup.Found(null, true, null));

    [TestMethod]
    public void Found_DifferentCase_IsFalse()
        => Assert.IsFalse(ActionSetLookup.Found("as_war_ram", true, "AS_WAR_RAM"));
}
