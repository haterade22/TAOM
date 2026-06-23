using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Domain;
using TAOM.Features.FaceMorphCompat;

namespace TAOM.Tests.Features.FaceMorphCompat;

[TestClass]
public class FaceMorphCompatServiceTests
{
    private IRaceManager _raceManager = null!;
    private FaceMorphCompatService _service = null!;

    [TestInitialize]
    public void Setup()
    {
        _raceManager = Substitute.For<IRaceManager>();
        _service = new FaceMorphCompatService(_raceManager);
    }

    private void StubRace(int id, string name)
    {
        _raceManager.IsValidRaceId(id).Returns(true);
        _raceManager.GetRaceNameFromId(id).Returns(name);
    }

    [TestMethod]
    public void ShouldForceGpuMorph_HumanRace_ReturnsFalse()
    {
        StubRace(0, "human");
        Assert.IsFalse(_service.ShouldForceGpuMorph(0));
    }

    [TestMethod]
    public void ShouldForceGpuMorph_HumanRace_CaseInsensitive_ReturnsFalse()
    {
        StubRace(0, "Human");
        Assert.IsFalse(_service.ShouldForceGpuMorph(0));
    }

    // Every custom LOTR race (skins.xml) uses a custom head mesh that can lack static-morph data ->
    // must be forced onto the GPU path. One row per concrete race the mod ships.
    [DataTestMethod]
    [DataRow(1, "dwarf")]
    [DataRow(2, "uruk")]
    [DataRow(3, "uruk_hai")]
    [DataRow(4, "dg_uruk")]
    [DataRow(5, "pale_uruk")]
    [DataRow(6, "goblin")]
    [DataRow(7, "orc")]
    [DataRow(8, "nazghul")]
    [DataRow(9, "saruman")]
    [DataRow(10, "berserker")]
    [DataRow(11, "cave_troll")]
    [DataRow(12, "hill_troll")]
    public void ShouldForceGpuMorph_CustomRace_ReturnsTrue(int id, string name)
    {
        StubRace(id, name);
        Assert.IsTrue(_service.ShouldForceGpuMorph(id));
    }

    [TestMethod]
    public void ShouldForceGpuMorph_InvalidRaceId_ReturnsTrue_FailSafe()
    {
        // The real RaceManager.GetRaceNameFromId coerces an unknown id to the "human" fallback.
        // The service must validate first and force GPU morph rather than be masked into the
        // not-forced human branch.
        _raceManager.IsValidRaceId(99).Returns(false);
        _raceManager.GetRaceNameFromId(99).Returns("human");
        Assert.IsTrue(_service.ShouldForceGpuMorph(99));
    }

    [TestMethod]
    public void ShouldForceGpuMorph_InvalidRaceId_DoesNotConsultFallbackName()
    {
        _raceManager.IsValidRaceId(99).Returns(false);
        _service.ShouldForceGpuMorph(99);
        _raceManager.DidNotReceive().GetRaceNameFromId(Arg.Any<int>());
    }
}
