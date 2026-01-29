using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using System;
using TAOM.Adapters;
using TAOM.Core.Domain;
using TAOM.Core.Logging;

namespace TAOM.Tests.Core.Domain;

[TestClass]
public class RaceManagerTests
{
    private RaceManager _sut;
    private IModLogger _logger;
    private IFaceGenAdapter _faceGenAdapter;

    [TestInitialize]
    public void Setup()
    {
        _logger = Substitute.For<IModLogger>();
        _faceGenAdapter = Substitute.For<IFaceGenAdapter>();
        _faceGenAdapter.GetRaceNames().Returns(new[] { "human", "dwarf", "elf", "orc" });
        _sut = new RaceManager(_logger, _faceGenAdapter);
    }

    [TestMethod]
    public void GetRaceNameFromId_ValidId_ReturnsCorrectName()
    {
        var result = _sut.GetRaceNameFromId(0);

        Assert.AreEqual("human", result);
    }

    [TestMethod]
    public void GetRaceNameFromId_DwarfId_ReturnsDwarf()
    {
        var result = _sut.GetRaceNameFromId(1);

        Assert.AreEqual("dwarf", result);
    }

    [TestMethod]
    public void GetRaceNameFromId_UnknownId_FallsBackToHuman()
    {
        var result = _sut.GetRaceNameFromId(999);

        Assert.AreEqual("human", result);
    }

    [TestMethod]
    public void GetRaceNameFromId_CachesResult()
    {
        var result1 = _sut.GetRaceNameFromId(0);
        var result2 = _sut.GetRaceNameFromId(0);

        Assert.AreEqual(result1, result2);
        Assert.AreEqual("human", result1);
    }

    [TestMethod]
    public void GetRaceIdFromName_Human_ReturnsZero()
    {
        var result = _sut.GetRaceIdFromName("human");

        Assert.AreEqual(0, result);
    }

    [TestMethod]
    public void GetRaceIdFromName_Dwarf_ReturnsOne()
    {
        var result = _sut.GetRaceIdFromName("dwarf");

        Assert.AreEqual(1, result);
    }

    [TestMethod]
    public void GetRaceIdFromName_UnknownName_ReturnsZeroAndLogs()
    {
        var result = _sut.GetRaceIdFromName("unknown_race");

        Assert.AreEqual(0, result);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("unknown_race")));
    }

    [TestMethod]
    public void GetRaceIdFromName_CaseInsensitive_ReturnsCorrectId()
    {
        var result = _sut.GetRaceIdFromName("HUMAN");

        Assert.AreEqual(0, result);
    }

    [TestMethod]
    public void IsValidRaceName_Human_ReturnsTrue()
    {
        var result = _sut.IsValidRaceName("human");

        Assert.IsTrue(result);
    }

    [TestMethod]
    public void IsValidRaceName_NullOrEmpty_ReturnsFalse()
    {
        Assert.IsFalse(_sut.IsValidRaceName(null));
        Assert.IsFalse(_sut.IsValidRaceName(""));
    }

    [TestMethod]
    public void IsValidRaceName_Unknown_ReturnsFalse()
    {
        Assert.IsFalse(_sut.IsValidRaceName("goblin"));
    }

    [TestMethod]
    public void IsValidRaceId_Zero_ReturnsTrue()
    {
        Assert.IsTrue(_sut.IsValidRaceId(0));
    }

    [TestMethod]
    public void IsValidRaceId_NegativeId_ReturnsFalse()
    {
        Assert.IsFalse(_sut.IsValidRaceId(-1));
    }

    [TestMethod]
    public void IsValidRaceId_UnknownId_ReturnsFalse()
    {
        Assert.IsFalse(_sut.IsValidRaceId(999));
    }

    [TestMethod]
    public void GetAllRaceIds_ReturnsAllRegisteredIds()
    {
        var result = _sut.GetAllRaceIds();

        Assert.AreEqual(4, result.Count);
        Assert.IsTrue(result.Contains(0));
        Assert.IsTrue(result.Contains(1));
        Assert.IsTrue(result.Contains(2));
        Assert.IsTrue(result.Contains(3));
    }

    [TestMethod]
    public void GetAllRaceNames_ReturnsAllRegisteredNames()
    {
        var result = _sut.GetAllRaceNames();

        Assert.AreEqual(4, result.Count);
        Assert.IsTrue(result.Contains("human"));
        Assert.IsTrue(result.Contains("dwarf"));
        Assert.IsTrue(result.Contains("elf"));
        Assert.IsTrue(result.Contains("orc"));
    }

    [TestMethod]
    public void InitializeRaceMappings_WhenAdapterThrows_LogsErrorAndFallsBackToHuman()
    {
        _faceGenAdapter.GetRaceNames().Throws(new InvalidOperationException("FaceGen not ready"));
        var sut = new RaceManager(_logger, _faceGenAdapter);

        var result = sut.GetRaceNameFromId(0);

        Assert.AreEqual("human", result);
        _logger.Received().LogError(Arg.Is<string>(s => s.Contains("Error initializing race mappings")));
    }

    [TestMethod]
    public void InitializeRaceMappings_WhenAdapterReturnsNull_FallsBackToHuman()
    {
        _faceGenAdapter.GetRaceNames().Returns((string[])null);
        var sut = new RaceManager(_logger, _faceGenAdapter);

        var result = sut.GetRaceNameFromId(0);

        Assert.AreEqual("human", result);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("returned null")));
    }
}
