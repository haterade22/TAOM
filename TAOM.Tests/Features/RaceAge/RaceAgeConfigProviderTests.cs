using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.RaceAge;

namespace TAOM.Tests.Features.RaceAge;

[TestClass]
public class RaceAgeConfigProviderTests
{
    private IPathService _pathService;
    private IModLogger _logger;
    private RaceAgeConfigProvider _sut;
    private string _tempDir;

    [TestInitialize]
    public void Setup()
    {
        _pathService = Substitute.For<IPathService>();
        _logger = Substitute.For<IModLogger>();

        _tempDir = Path.Combine(Path.GetTempPath(), "taom_test_raceage_" + Path.GetRandomFileName());
        Directory.CreateDirectory(Path.Combine(_tempDir, "raceage"));
        _pathService.ModuleDataPath.Returns(_tempDir);

        _sut = new RaceAgeConfigProvider(_pathService, _logger);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [TestMethod]
    public void LoadConfig_ValidJson_ParsesRaces()
    {
        var json = @"{
            ""defaultRace"": ""human"",
            ""races"": {
                ""human"": { ""maxAge"": 85, ""becomeOld"": 55, ""comesOfAge"": 18, ""middleAge"": 35, ""fertilityEnd"": 45, ""fertilityMod"": 1.0 },
                ""dwarf"": { ""maxAge"": 250, ""becomeOld"": 180, ""comesOfAge"": 30, ""middleAge"": 80, ""fertilityEnd"": 120, ""fertilityMod"": 0.6 }
            }
        }";
        File.WriteAllText(Path.Combine(_tempDir, "raceage", "race_age_config.json"), json);

        var config = _sut.LoadConfig();

        Assert.AreEqual("human", config.DefaultRace);
        Assert.AreEqual(2, config.Races.Count);
        Assert.AreEqual(250, config.Races["dwarf"].MaxAge);
        Assert.AreEqual(0.6f, config.Races["dwarf"].FertilityMod);
    }

    [TestMethod]
    public void LoadConfig_MissingFile_ReturnsEmptyConfig()
    {
        var config = new RaceAgeConfigProvider(
            Substitute.For<IPathService>(),
            _logger
        ).LoadConfig();

        Assert.AreEqual("human", config.DefaultRace);
        Assert.AreEqual(0, config.Races.Count);
    }

    [TestMethod]
    public void LoadConfig_InvalidJson_ReturnsEmptyConfig()
    {
        File.WriteAllText(Path.Combine(_tempDir, "raceage", "race_age_config.json"), "NOT JSON");

        var config = _sut.LoadConfig();

        Assert.AreEqual("human", config.DefaultRace);
        Assert.AreEqual(0, config.Races.Count);
    }

    [TestMethod]
    public void LoadConfig_ImmortalFlag_ParsedCorrectly()
    {
        var json = @"{
            ""defaultRace"": ""human"",
            ""races"": {
                ""nazghul"": { ""maxAge"": 10000, ""becomeOld"": 9999, ""comesOfAge"": 18, ""middleAge"": 100, ""fertilityEnd"": 0, ""fertilityMod"": 0.0, ""immortal"": true }
            }
        }";
        File.WriteAllText(Path.Combine(_tempDir, "raceage", "race_age_config.json"), json);

        var config = _sut.LoadConfig();

        Assert.IsTrue(config.Races["nazghul"].Immortal);
        Assert.AreEqual(0.0f, config.Races["nazghul"].FertilityMod);
    }

    // An immortal race authors `fertilityEnd: 0` to mean "no fertility window at all" — it is a
    // deliberate sentinel, not an inverted range. The ordering check used to read it as an inversion
    // and overwrite the pair with 18/45, silently granting Sauron a human fertility window. Masked
    // downstream by TaomPregnancyModel's IsImmortal short-circuit, but GetFertilityEndAge is public
    // on IRaceAgeService, so any future consumer that reads it without re-checking IsImmortal would
    // have seen 45. (Shipped config: nazghul / saruman / sauron are the only zero-fertilityEnd entries
    // and all three carry immortal:true.)
    [TestMethod]
    public void LoadConfig_ImmortalRaceWithZeroFertilityEnd_PreservesAuthoredValues()
    {
        var json = @"{
            ""defaultRace"": ""human"",
            ""races"": {
                ""sauron"": { ""maxAge"": 10000, ""becomeOld"": 9970, ""comesOfAge"": 18, ""middleAge"": 100, ""fertilityEnd"": 0, ""fertilityMod"": 0.0, ""immortal"": true }
            }
        }";
        File.WriteAllText(Path.Combine(_tempDir, "raceage", "race_age_config.json"), json);

        var config = _sut.LoadConfig();

        Assert.AreEqual(0, config.Races["sauron"].FertilityEnd, "authored 'no fertility window' sentinel must survive validation");
        Assert.AreEqual(18, config.Races["sauron"].ComesOfAge);
    }

    [TestMethod]
    public void LoadConfig_ImmortalRaceWithZeroFertilityEnd_DoesNotWarn()
    {
        var json = @"{
            ""defaultRace"": ""human"",
            ""races"": {
                ""sauron"": { ""maxAge"": 10000, ""becomeOld"": 9970, ""comesOfAge"": 18, ""middleAge"": 100, ""fertilityEnd"": 0, ""fertilityMod"": 0.0, ""immortal"": true }
            }
        }";
        File.WriteAllText(Path.Combine(_tempDir, "raceage", "race_age_config.json"), json);

        _sut.LoadConfig();

        _logger.DidNotReceive().LogWarning(Arg.Is<string>(s => s.Contains("FertilityEnd")));
    }

    // Guard against over-widening the fix: a MORTAL race with a genuinely inverted window must
    // still be corrected and still warn.
    [TestMethod]
    public void LoadConfig_MortalRaceWithInvertedFertilityWindow_ResetsToDefaultsAndWarns()
    {
        var json = @"{
            ""defaultRace"": ""human"",
            ""races"": {
                ""human"": { ""maxAge"": 85, ""becomeOld"": 55, ""comesOfAge"": 60, ""middleAge"": 35, ""fertilityEnd"": 45, ""fertilityMod"": 1.0 }
            }
        }";
        File.WriteAllText(Path.Combine(_tempDir, "raceage", "race_age_config.json"), json);

        var config = _sut.LoadConfig();

        Assert.AreEqual(18, config.Races["human"].ComesOfAge);
        Assert.AreEqual(45, config.Races["human"].FertilityEnd);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("FertilityEnd")));
    }
}
