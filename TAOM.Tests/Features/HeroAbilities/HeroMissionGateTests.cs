using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.HeroAbilities.Hooks;

namespace TAOM.Tests.Features.HeroAbilities;

[TestClass]
public class HeroMissionGateTests
{
    [TestMethod]
    public void LongDefeatCombat_Passes() => Assert.IsTrue(HeroMissionGate.IsHeroMission(true, true));

    [TestMethod]
    public void NoMarker_Fails() => Assert.IsFalse(HeroMissionGate.IsHeroMission(true, false));

    [TestMethod]
    public void MarkerButNotCombat_Fails() => Assert.IsFalse(HeroMissionGate.IsHeroMission(false, true));
}
