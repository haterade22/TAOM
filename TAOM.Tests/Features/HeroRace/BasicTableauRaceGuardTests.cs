using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.HeroRace;

namespace TAOM.Tests.Features.HeroRace;

[TestClass]
public class BasicTableauRaceGuardTests
{
    private BasicTableauRaceGuard _sut;

    [TestInitialize]
    public void Setup() => _sut = new BasicTableauRaceGuard();

    [TestMethod]
    public void ResolveSafeRace_HumanBaseRace_ReturnsHumanUnchanged()
    {
        Assert.AreEqual(BasicTableauRaceGuard.HumanBaseRace, _sut.ResolveSafeRace(BasicTableauRaceGuard.HumanBaseRace));
    }

    [TestMethod]
    public void ResolveSafeRace_DwarfRace_CoercesToHuman()
    {
        // race 1 (dwarf) heads lack the agentless-tableau morph data (issue #295) -> must coerce.
        // Fails if the guard ever returns the input race for a non-allow-listed race.
        Assert.AreEqual(BasicTableauRaceGuard.HumanBaseRace, _sut.ResolveSafeRace(1));
    }

    [TestMethod]
    public void ResolveSafeRace_ElfRace_CoercesToHuman()
    {
        Assert.AreEqual(BasicTableauRaceGuard.HumanBaseRace, _sut.ResolveSafeRace(2));
    }

    [TestMethod]
    public void ResolveSafeRace_NegativeInvalidRace_CoercesToHuman()
    {
        Assert.AreEqual(BasicTableauRaceGuard.HumanBaseRace, _sut.ResolveSafeRace(-1));
    }

    [TestMethod]
    public void ResolveSafeRace_UnknownLargeRace_CoercesToHuman()
    {
        Assert.AreEqual(BasicTableauRaceGuard.HumanBaseRace, _sut.ResolveSafeRace(int.MaxValue));
    }
}
