using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.CharacterCreation;
using TAOM.Features.CharacterCreation.Models;

namespace TAOM.Tests.Features.CharacterCreation;

[TestClass]
public class CultureRaceFilterServiceTests
{
    private ICultureCreationDataProvider _provider;
    private IModLogger _logger;
    private CultureRaceFilterService _sut;

    [TestInitialize]
    public void Setup()
    {
        _provider = Substitute.For<ICultureCreationDataProvider>();
        _logger = Substitute.For<IModLogger>();
        _sut = new CultureRaceFilterService(_provider, _logger);
    }

    private void Stub(string cultureId, params string[] races)
    {
        _provider.GetCultureData(cultureId).Returns(new CultureCreationData
        {
            CultureId = cultureId,
            Races = races
        });
    }

    [TestMethod]
    public void GetAllowedRaces_Erebor_DwarfOnly()
    {
        Stub("erebor", "dwarf");

        CollectionAssert.AreEqual(new[] { "dwarf" }, (System.Collections.ICollection)_sut.GetAllowedRaces("erebor"));
    }

    [TestMethod]
    public void GetAllowedRaces_Mordor_UrukOrcHuman()
    {
        Stub("mordor", "uruk", "orc", "human");

        CollectionAssert.AreEqual(new[] { "uruk", "orc", "human" }, (System.Collections.ICollection)_sut.GetAllowedRaces("mordor"));
    }

    [TestMethod]
    public void GetAllowedRaces_Isengard_UrukHaiBerserkerHuman()
    {
        Stub("isengard", "uruk_hai", "berserker", "human");

        CollectionAssert.AreEqual(new[] { "uruk_hai", "berserker", "human" }, (System.Collections.ICollection)_sut.GetAllowedRaces("isengard"));
    }

    [TestMethod]
    public void GetAllowedRaces_Gundabad_PaleUrukOrcGoblinHuman()
    {
        Stub("gundabad", "pale_uruk", "goblin", "orc", "human");

        CollectionAssert.AreEqual(new[] { "pale_uruk", "goblin", "orc", "human" }, (System.Collections.ICollection)_sut.GetAllowedRaces("gundabad"));
    }

    [TestMethod]
    public void GetAllowedRaces_DolGuldur_DgUrukGoblinOrcHuman()
    {
        Stub("dolguldur", "dg_uruk", "goblin", "orc", "human");

        CollectionAssert.AreEqual(new[] { "dg_uruk", "goblin", "orc", "human" }, (System.Collections.ICollection)_sut.GetAllowedRaces("dolguldur"));
    }

    [TestMethod]
    [DataRow("mirkwood")]
    [DataRow("lothlorien")]
    [DataRow("rivendell")]
    public void GetAllowedRaces_ElvishCulture_ElfAndHuman(string cultureId)
    {
        Stub(cultureId, "elf", "human");

        CollectionAssert.AreEqual(new[] { "elf", "human" }, (System.Collections.ICollection)_sut.GetAllowedRaces(cultureId));
    }

    [TestMethod]
    [DataRow("gondor")]
    [DataRow("umbar")]
    [DataRow("empire")]
    [DataRow("vlandia")]
    public void GetAllowedRaces_HumanOnlyCulture_OnlyHuman(string cultureId)
    {
        Stub(cultureId, "human");

        CollectionAssert.AreEqual(new[] { "human" }, (System.Collections.ICollection)_sut.GetAllowedRaces(cultureId));
    }

    [TestMethod]
    public void GetAllowedRaces_UnknownCulture_EmptyAndWarnsOnce()
    {
        _provider.GetCultureData("nonexistent").Returns((CultureCreationData)null);

        var first = _sut.GetAllowedRaces("nonexistent");
        var second = _sut.GetAllowedRaces("nonexistent");

        Assert.AreEqual(0, first.Count);
        Assert.AreEqual(0, second.Count);
        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("nonexistent")));
    }

    [TestMethod]
    public void GetAllowedRaces_NullOrEmptyCultureId_Empty()
    {
        Assert.AreEqual(0, _sut.GetAllowedRaces(null).Count);
        Assert.AreEqual(0, _sut.GetAllowedRaces("").Count);
    }

    [TestMethod]
    public void GetAllowedRaces_CultureWithEmptyRacesArray_EmptyAndWarns()
    {
        _provider.GetCultureData("broken").Returns(new CultureCreationData
        {
            CultureId = "broken",
            Races = System.Array.Empty<string>()
        });

        var result = _sut.GetAllowedRaces("broken");

        Assert.AreEqual(0, result.Count);
        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("broken")));
    }

    [TestMethod]
    public void HasFilter_KnownCulture_True()
    {
        Stub("erebor", "dwarf");

        Assert.IsTrue(_sut.HasFilter("erebor"));
    }

    [TestMethod]
    public void HasFilter_UnknownCulture_False()
    {
        _provider.GetCultureData("nope").Returns((CultureCreationData)null);

        Assert.IsFalse(_sut.HasFilter("nope"));
    }

    [TestMethod]
    public void HasFilter_NullCultureId_False()
    {
        Assert.IsFalse(_sut.HasFilter(null));
        Assert.IsFalse(_sut.HasFilter(""));
    }

    [TestMethod]
    public void HasFilter_EmptyRacesArray_False()
    {
        _provider.GetCultureData("broken").Returns(new CultureCreationData
        {
            CultureId = "broken",
            Races = System.Array.Empty<string>()
        });

        Assert.IsFalse(_sut.HasFilter("broken"));
    }

    [TestMethod]
    public void IsRaceAllowed_AllowedRace_True()
    {
        Stub("mordor", "uruk", "orc", "human");

        Assert.IsTrue(_sut.IsRaceAllowed("mordor", "uruk"));
        Assert.IsTrue(_sut.IsRaceAllowed("mordor", "orc"));
        Assert.IsTrue(_sut.IsRaceAllowed("mordor", "human"));
    }

    [TestMethod]
    public void IsRaceAllowed_DisallowedRace_False()
    {
        Stub("mordor", "uruk", "orc", "human");

        Assert.IsFalse(_sut.IsRaceAllowed("mordor", "dwarf"));
        Assert.IsFalse(_sut.IsRaceAllowed("mordor", "elf"));
        Assert.IsFalse(_sut.IsRaceAllowed("mordor", "saruman"));
    }

    [TestMethod]
    public void IsRaceAllowed_CaseInsensitive()
    {
        Stub("mordor", "uruk", "orc", "human");

        Assert.IsTrue(_sut.IsRaceAllowed("mordor", "URUK"));
        Assert.IsTrue(_sut.IsRaceAllowed("MORDOR", "uruk"));
    }

    [TestMethod]
    public void IsRaceAllowed_UnknownCulture_True()
    {
        _provider.GetCultureData("unknown").Returns((CultureCreationData)null);

        Assert.IsTrue(_sut.IsRaceAllowed("unknown", "human"));
    }

    [TestMethod]
    public void IsRaceAllowed_NullRaceName_False()
    {
        Stub("mordor", "uruk", "orc", "human");

        Assert.IsFalse(_sut.IsRaceAllowed("mordor", null));
        Assert.IsFalse(_sut.IsRaceAllowed("mordor", ""));
    }
}
