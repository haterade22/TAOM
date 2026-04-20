using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Siege;
using TAOM.Features.Siege.Models;

namespace TAOM.Tests.Features.Siege;

[TestClass]
public class SiegeEngineAvailabilityServiceTests
{
    private SiegeEngineAvailabilityService _sut;

    [TestInitialize]
    public void Setup()
    {
        _sut = new SiegeEngineAvailabilityService();
    }

    [TestMethod]
    public void GetDefenderEngines_NoFirePerks_YieldsBallistaCatapultTrebuchet()
    {
        var engines = _sut.GetDefenderEngines(hasFirePerks: false).ToList();

        CollectionAssert.AreEqual(
            new[]
            {
                DefenderSiegeEngineKind.Ballista,
                DefenderSiegeEngineKind.Catapult,
                DefenderSiegeEngineKind.Trebuchet
            },
            engines);
    }

    [TestMethod]
    public void GetDefenderEngines_HasFirePerks_YieldsAllFiveIncludingFireVariants()
    {
        var engines = _sut.GetDefenderEngines(hasFirePerks: true).ToList();

        CollectionAssert.AreEqual(
            new[]
            {
                DefenderSiegeEngineKind.Ballista,
                DefenderSiegeEngineKind.FireBallista,
                DefenderSiegeEngineKind.Catapult,
                DefenderSiegeEngineKind.FireCatapult,
                DefenderSiegeEngineKind.Trebuchet
            },
            engines);
    }

    [TestMethod]
    public void GetDefenderEngines_AnyPerkState_AlwaysIncludesTrebuchet()
    {
        var withoutPerks = _sut.GetDefenderEngines(hasFirePerks: false).ToList();
        var withPerks    = _sut.GetDefenderEngines(hasFirePerks: true).ToList();

        CollectionAssert.Contains(withoutPerks, DefenderSiegeEngineKind.Trebuchet);
        CollectionAssert.Contains(withPerks,    DefenderSiegeEngineKind.Trebuchet);
    }

    [TestMethod]
    public void GetDefenderEngines_AnyPerkState_NeverIncludesRamOrSiegeTower()
    {
        var engines = _sut.GetDefenderEngines(hasFirePerks: true).ToList();

        Assert.AreEqual(5, engines.Count,
            "Defender list must be exactly 5 engines with perks; any more implies an attacker-only engine leaked in.");
    }
}
