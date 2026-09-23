using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Domain;
using TAOM.Core.Logging;
using TAOM.Features.NazgulFamily;
using TAOM.Features.SignatureStrikes;
using TAOM.Features.SignatureStrikes.Domain;

namespace TAOM.Tests.Features.SignatureStrikes;

/// <summary>
/// Which signature an agent carries, on three identity axes (the DreadRegistry pattern): hero
/// StringId, named hero set (<c>nazgul_nine</c> through <see cref="INazgulRegistry"/>) and FaceGen
/// race. The hero axes win over race, then the first listed signature wins (#645). Race names
/// resolve to ids ONCE behind <c>IsValidRaceName</c>; the hot path never calls
/// <c>GetRaceNameFromId</c>, whose "human" coercion of an unknown id is the csharp-architecture.md
/// "Lookup Functions With Fallbacks" trap.
/// </summary>
[TestClass]
public class SignatureStrikeRegistryTests
{
    private const int Sauron = 0;
    private const int Nazgul = 1;

    private static readonly Dictionary<int, string> RaceTable = new Dictionary<int, string>
    {
        [0] = "human",
        [1] = "elf",
        [3] = "orc",
        [14] = "sauron",
        [15] = "nazghul",
    };

    private SignatureStrikesConfig _config = null!;
    private ISignatureStrikesConfigProvider _configProvider = null!;
    private IRaceManager _raceManager = null!;
    private INazgulRegistry _nazgul = null!;
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

        _nazgul = Substitute.For<INazgulRegistry>();
        _nazgul.IsWraith("lord_1_15").Returns(true);

        _logger = Substitute.For<IModLogger>();

        _sut = new SignatureStrikeRegistry(_configProvider, _raceManager, _nazgul, _logger);
    }

    private static SignatureConfig Signature(string id, string[]? heroIds = null, string[]? races = null) => new SignatureConfig
    {
        Id = id,
        HeroIds = (heroIds ?? new string[0]).ToList(),
        Races = (races ?? new string[0]).ToList(),
    };

    // ---- The shipped pair ------------------------------------------------------------------

    [TestMethod]
    public void Resolve_SauronHeroId_IsSaurons()
        => Assert.AreEqual(Sauron, _sut.ResolveSignatureIndex("lord_1_17", raceId: 0));

    [TestMethod]
    public void Resolve_SauronRaceWithoutAHeroId_IsSaurons()
        => Assert.AreEqual(Sauron, _sut.ResolveSignatureIndex(null, raceId: 14));

    [TestMethod]
    public void Resolve_WraithThroughTheNazgulHeroSet_IsTheNines()
        => Assert.AreEqual(Nazgul, _sut.ResolveSignatureIndex("lord_1_15", raceId: 15));

    [TestMethod]
    public void Resolve_WraithOnAnOldRace_IsStillTheNines()
    {
        // The hero set finds the Nine whatever race their data says (a save from before #644).
        Assert.AreEqual(Nazgul, _sut.ResolveSignatureIndex("lord_1_15", raceId: 0));
    }

    [TestMethod]
    public void Resolve_NazghulRaceWithoutAHeroId_IsTheNines()
        => Assert.AreEqual(Nazgul, _sut.ResolveSignatureIndex(null, raceId: 15));

    [TestMethod]
    public void GetSignatureId_NamesEachSignature()
    {
        Assert.AreEqual("sauron", _sut.GetSignatureId(Sauron));
        Assert.AreEqual("nazgul", _sut.GetSignatureId(Nazgul));
    }

    [TestMethod]
    public void GetSignatureId_OutOfRange_IsEmpty()
    {
        Assert.AreEqual("", _sut.GetSignatureId(2));
        Assert.AreEqual("", _sut.GetSignatureId(-1));
    }

    // ---- Nobody else -----------------------------------------------------------------------

    [TestMethod]
    public void Resolve_PlainOrc_IsNull()
        => Assert.IsNull(_sut.ResolveSignatureIndex("lord_2_1", raceId: 3));

    [TestMethod]
    public void Resolve_NoIdentityAtAll_IsNull()
        => Assert.IsNull(_sut.ResolveSignatureIndex(null, null));

    [TestMethod]
    public void Resolve_UnknownRaceId_IsNullAndNeverCoerces()
    {
        Assert.IsNull(_sut.ResolveSignatureIndex(null, raceId: 999));

        _raceManager.DidNotReceive().GetRaceNameFromId(Arg.Any<int>());
    }

    [TestMethod]
    public void Resolve_HeroIdMatchIsCaseInsensitive()
        => Assert.AreEqual(Sauron, _sut.ResolveSignatureIndex("LORD_1_17", raceId: 0));

    [TestMethod]
    public void Resolve_EmptyLists_NobodyMatches()
    {
        _config.Signatures = new List<SignatureConfig> { Signature("empty") };

        Assert.IsNull(_sut.ResolveSignatureIndex("lord_1_17", raceId: 14));
    }

    // ---- Precedence --------------------------------------------------------------------------

    [TestMethod]
    public void Resolve_HeroAxisBeatsTheRaceAxis_EvenForALaterSignature()
    {
        _config.Signatures = new List<SignatureConfig>
        {
            Signature("by_race", races: new[] { "sauron" }),
            Signature("by_hero", heroIds: new[] { "lord_x" }),
        };

        Assert.AreEqual(1, _sut.ResolveSignatureIndex("lord_x", raceId: 14));
    }

    [TestMethod]
    public void Resolve_SameHeroInTwoSignatures_FirstWinsAndWarns()
    {
        _config.Signatures = new List<SignatureConfig>
        {
            Signature("first", heroIds: new[] { "lord_1_17" }),
            Signature("second", heroIds: new[] { "lord_1_17" }),
        };

        Assert.AreEqual(0, _sut.ResolveSignatureIndex("lord_1_17", raceId: null));
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("lord_1_17") && m.Contains("second")));
    }

    [TestMethod]
    public void Resolve_SameRaceInTwoSignatures_FirstWinsAndWarns()
    {
        _config.Signatures = new List<SignatureConfig>
        {
            Signature("first", races: new[] { "sauron" }),
            Signature("second", races: new[] { "sauron" }),
        };

        Assert.AreEqual(0, _sut.ResolveSignatureIndex(null, raceId: 14));
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("sauron") && m.Contains("second")));
    }

    // ---- Config the registry cannot resolve --------------------------------------------------

    [TestMethod]
    public void Resolve_InvalidRaceNameInConfig_IsSkippedAndWarned()
    {
        _config.Signatures = new List<SignatureConfig> { Signature("typo", races: new[] { "nazgul" }) };

        Assert.IsNull(_sut.ResolveSignatureIndex(null, raceId: 15));
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("'nazgul'")));
    }

    [TestMethod]
    public void Resolve_UnknownHeroSet_IsSkippedAndWarned()
    {
        var signature = Signature("typo");
        signature.HeroSets = new List<string> { "ringwraiths" };
        _config.Signatures = new List<SignatureConfig> { signature };

        Assert.IsNull(_sut.ResolveSignatureIndex("lord_1_15", raceId: null));
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("ringwraiths")));
    }

    [TestMethod]
    public void Resolve_WithoutTheNazgulHeroSet_AWraithIsNotMatchedById()
    {
        _config.Signatures = new List<SignatureConfig> { Signature("sauron_only", heroIds: new[] { "lord_1_17" }) };

        Assert.IsNull(_sut.ResolveSignatureIndex("lord_1_15", raceId: null));
        _nazgul.DidNotReceive().IsWraith(Arg.Any<string>());
    }

    [TestMethod]
    public void Resolve_TablesAreBuiltOnce()
    {
        _sut.ResolveSignatureIndex("lord_1_17", 0);
        _sut.ResolveSignatureIndex("lord_1_15", 15);
        _sut.ResolveSignatureIndex(null, 14);
        _sut.GetSignatureId(0);

        _configProvider.Received(1).GetConfig();
    }
}
