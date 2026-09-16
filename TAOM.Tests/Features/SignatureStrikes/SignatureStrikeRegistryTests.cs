using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Domain;
using TAOM.Core.Logging;
using TAOM.Features.SignatureStrikes;
using TAOM.Features.SignatureStrikes.Domain;

namespace TAOM.Tests.Features.SignatureStrikes;

/// <summary>
/// Identity for "does this agent have signature strikes", on the DreadRegistry's two axes: hero
/// StringId and FaceGen race. Race names resolve to ids ONCE behind <c>IsValidRaceName</c>; the
/// hot path never calls <c>GetRaceNameFromId</c>, whose "human" coercion of an unknown id is the
/// csharp-architecture.md "Lookup Functions With Fallbacks" trap.
/// </summary>
[TestClass]
public class SignatureStrikeRegistryTests
{
    private static readonly Dictionary<int, string> RaceTable = new Dictionary<int, string>
    {
        [0] = "human",
        [1] = "elf",
        [3] = "orc",
        [14] = "sauron",
    };

    private SignatureStrikesConfig _config = null!;
    private ISignatureStrikesConfigProvider _configProvider = null!;
    private IRaceManager _raceManager = null!;
    private IModLogger _logger = null!;
    private SignatureStrikeRegistry _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _config = new SignatureStrikesConfig();

        _configProvider = Substitute.For<ISignatureStrikesConfigProvider>();
        _configProvider.GetConfig().Returns(_ => _config);

        _raceManager = Substitute.For<IRaceManager>();
        _raceManager.IsValidRaceId(Arg.Any<int>()).Returns(c => RaceTable.ContainsKey(c.Arg<int>()));
        _raceManager.IsValidRaceName(Arg.Any<string>()).Returns(c => RaceTable.ContainsValue(c.Arg<string>()));
        _raceManager.GetRaceIdFromName(Arg.Any<string>()).Returns(c =>
            RaceTable.FirstOrDefault(p => p.Value == c.Arg<string>()).Key);
        _raceManager.GetRaceNameFromId(Arg.Any<int>()).Returns(c =>
            RaceTable.TryGetValue(c.Arg<int>(), out var name) ? name : "human");

        _logger = Substitute.For<IModLogger>();

        _sut = new SignatureStrikeRegistry(_configProvider, _raceManager, _logger);
    }

    [TestMethod]
    public void IsSignatureAgent_ShippedHeroId_IsSignature()
    {
        Assert.IsTrue(_sut.IsSignatureAgent("lord_1_17", raceId: 0));
    }

    [TestMethod]
    public void IsSignatureAgent_ShippedRace_IsSignatureWithoutAHeroId()
    {
        // Custom battle: the agent carries a BasicCharacterObject and no HeroObject.
        Assert.IsTrue(_sut.IsSignatureAgent(null, raceId: 14));
    }

    [TestMethod]
    public void IsSignatureAgent_PlainOrc_IsNotSignature()
    {
        Assert.IsFalse(_sut.IsSignatureAgent("lord_2_1", raceId: 3));
    }

    [TestMethod]
    public void IsSignatureAgent_NoIdentityAtAll_IsNotSignature()
    {
        Assert.IsFalse(_sut.IsSignatureAgent(null, null));
    }

    [TestMethod]
    public void IsSignatureAgent_UnknownRaceId_IsNotSignatureAndNeverCoerces()
    {
        Assert.IsFalse(_sut.IsSignatureAgent(null, raceId: 999));

        _raceManager.DidNotReceive().GetRaceNameFromId(Arg.Any<int>());
    }

    [TestMethod]
    public void IsSignatureAgent_HeroIdMatchIsCaseInsensitive()
    {
        Assert.IsTrue(_sut.IsSignatureAgent("LORD_1_17", raceId: 0));
    }

    [TestMethod]
    public void IsSignatureAgent_InvalidRaceNameInConfig_IsSkippedAndWarned()
    {
        _config.Races = new List<string> { "nazgul" };

        Assert.IsFalse(_sut.IsSignatureAgent(null, raceId: 14));
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("nazgul")));
    }

    [TestMethod]
    public void IsSignatureAgent_EmptyLists_NobodyIsSignature()
    {
        _config.HeroIds = new List<string>();
        _config.Races = new List<string>();

        Assert.IsFalse(_sut.IsSignatureAgent("lord_1_17", raceId: 14));
    }

    [TestMethod]
    public void IsSignatureAgent_TablesAreBuiltOnce()
    {
        _sut.IsSignatureAgent("lord_1_17", 0);
        _sut.IsSignatureAgent("lord_1_17", 0);
        _sut.IsSignatureAgent(null, 14);

        _configProvider.Received(1).GetConfig();
    }
}
