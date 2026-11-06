using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.FactionUI.Campaign;

namespace TAOM.Tests.Features.FactionUI;

/// <summary>
/// The encyclopedia troop-name patches (TAOM.EncyclopediaTroopNames) apply with the GameInit batch but stay inert until
/// the first themed encyclopedia movie binds, which is when the category used to be applied (ADR-013 move).
/// </summary>
[TestClass]
public class EncyclopediaTroopNamesTests
{
    [TestCleanup]
    public void Cleanup() => EncyclopediaTroopNames.Active = false;

    [TestMethod]
    public void ShownName_BeforeTheThemedEncyclopediaBinds_KeepsTheBrackets()
    {
        EncyclopediaTroopNames.Active = false;

        Assert.AreEqual("[Gondor] Tower Guard", EncyclopediaTroopNames.ShownName("[Gondor] Tower Guard"));
    }

    [TestMethod]
    public void ShownName_AfterTheThemedEncyclopediaBinds_DropsTheBrackets()
    {
        EncyclopediaTroopNames.Active = true;

        Assert.AreEqual("Tower Guard", EncyclopediaTroopNames.ShownName("[Gondor] Tower Guard"));
    }

    [TestMethod]
    public void DisplayName_NoBrackets_ReturnsTheNameUnchanged()
    {
        Assert.AreEqual("Tower Guard", EncyclopediaTroopNames.DisplayName("Tower Guard"));
        Assert.AreEqual("", EncyclopediaTroopNames.DisplayName(""));
    }
}
