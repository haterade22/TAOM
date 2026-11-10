using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.HeroDefence;
using TAOM.Features.HeroDefence.Domain;

namespace TAOM.Tests.Features.HeroDefence;

[TestClass]
public class HeroDefenceConfigCatalogTests
{
    private const string GoodCultures = @"{
      ""signatures"": { ""wall"": { ""order"": [""Infantry"",""Ranged"",""Cavalry"",""HorseArcher""], ""countFactor"": 0.5, ""firstPower"": ""hide"" } },
      ""cultures"": { ""a"": { ""signature"": ""wall"", ""mix"": { ""infantry"": 70, ""ranged"": 30 }, ""leaders"": [""x"",""y"",""z""] } }
    }";

    private const string GoodRosters = @"{ ""a"": [ { ""id"": ""t1"", ""level"": 6, ""class"": ""Infantry"" }, { ""id"": ""t2"", ""level"": 11, ""class"": ""Ranged"" } ] }";

    /// <summary>A waves file with an empty opening. With no key, the shipped opening names mordor and erebor, which these test cultures lack.</summary>
    private const string NoOpening = @"{ ""opening"": [] }";

    private IModLogger _logger = null!;

    [TestInitialize]
    public void SetUp() => _logger = Substitute.For<IModLogger>();

    private HeroDefenceConfigCatalog Catalog(string? waves = NoOpening, string? cultures = GoodCultures, string? rosters = GoodRosters) =>
        HeroDefenceConfigCatalog.FromTexts(waves, cultures, rosters, _logger);

    /// <summary>The number values of a wave catalog in one line, so a test can compare them with the shipped defaults.</summary>
    private static string Numbers(WaveCatalog w) =>
        string.Join(" ", new object[]
        {
            w.Scale.HealthPerWave, w.Scale.DamagePerWave, w.Scale.MaxFactor, w.CountStart, w.CountPerWave, w.CountMax, w.PackSize, w.TroopsPerLevel,
            w.NearestLowerLevels, w.HighLevelShare, w.ArmourPercentPerWave, w.ArmourPercentMax, w.SwingPercentPerWave, w.SwingPercentMax,
            w.GapHealthPerLevel, w.GapDamagePerLevel, w.LeaderEveryWaves, string.Join(",", w.LeaderHealth),
        });

    private static string DefaultNumbers => Numbers(HeroDefenceWavesReader.DefaultCatalog);

    private void AssertWarned(string part) =>
        _logger.Received().LogWarning(Arg.Is<string>(message => message.Contains(part)));

    private void AssertNoWarning() =>
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());

    [TestMethod]
    public void Rules_EmptyFile_AreTheDefaultsWithNoWarning()
    {
        Assert.AreEqual(new DefenceRules(), Catalog("{}").Rules);
        AssertNoWarning();
    }

    [TestMethod]
    public void Json_ADuplicateKey_RevertsTheFileWithAWarning()
    {
        var rules = Catalog(@"{ ""rules"": { ""startingGold"": 5, ""startingGold"": 6 } }").Rules;

        Assert.AreEqual(new DefenceRules(), rules);
        AssertWarned("waves.json");
    }

    [TestMethod]
    public void Files_ZeroBytes_WarnAndLoadNothing()
    {
        var catalog = Catalog("", "", "");

        Assert.AreEqual(new DefenceRules(), catalog.Rules);
        Assert.AreEqual(0, catalog.Waves.Cultures.Count);
        AssertWarned("waves.json");
        AssertWarned("cultures.json");
        AssertWarned("rosters.json");
    }

    [TestMethod]
    public void Rules_ValidValues_AreRead()
    {
        var rules = Catalog(@"{ ""rules"": { ""startingGold"": 777, ""pauseSeconds"": 33.5, ""maxPlaced"": 4 } }").Rules;

        Assert.AreEqual(777, rules.StartingGold);
        Assert.AreEqual(33.5f, rules.PauseSeconds, 0.001f);
        Assert.AreEqual(4, rules.MaxPlaced);
        AssertNoWarning();
    }

    [DataTestMethod]
    [DataRow("startingGold", "-1")]
    [DataRow("startingLives", "0")]
    [DataRow("firstPauseSeconds", "0")]
    [DataRow("pauseSeconds", "1e30")]
    [DataRow("maxPlaced", "7")]
    [DataRow("maxPlaced", "0")]
    [DataRow("earlyGoldPerSecond", "-1")]
    [DataRow("earlyGoldCap", "-1")]
    [DataRow("waveBonusBase", "-1")]
    [DataRow("waveBonusPerWave", "-1")]
    [DataRow("bountyBase", "-1")]
    [DataRow("bountyPerLevel", "-1")]
    [DataRow("bountyMountedPercent", "-1")]
    [DataRow("packGapSeconds", "-1")]
    [DataRow("exitRadiusMetres", "0")]
    [DataRow("waveLimitBaseSeconds", "1")]
    [DataRow("waveLimitPerCreepSeconds", "-1")]
    [DataRow("minPostGapMetres", "-1")]
    [DataRow("roadEndClearMetres", "-1")]
    public void Rules_OutOfRange_RevertToTheDefaultWithAWarning(string key, string value)
    {
        var rules = Catalog($@"{{ ""rules"": {{ ""{key}"": {value} }} }}").Rules;

        Assert.AreEqual(new DefenceRules(), rules);
        AssertWarned("rules." + key);
    }

    [TestMethod]
    public void Rules_ANumberAsText_Reverts()
    {
        Assert.AreEqual(new DefenceRules(), Catalog(@"{ ""rules"": { ""pauseSeconds"": ""NaN"" } }").Rules);
        AssertWarned("rules.pauseSeconds");
    }

    [DataTestMethod]
    [DataRow("rules", "firstPauseSeconds")]
    [DataRow("rules", "pauseSeconds")]
    [DataRow("rules", "earlyGoldPerSecond")]
    [DataRow("rules", "bountyBase")]
    [DataRow("rules", "bountyPerLevel")]
    [DataRow("rules", "bountyMountedPercent")]
    [DataRow("rules", "packGapSeconds")]
    [DataRow("rules", "exitRadiusMetres")]
    [DataRow("rules", "waveLimitBaseSeconds")]
    [DataRow("rules", "waveLimitPerCreepSeconds")]
    [DataRow("rules", "minPostGapMetres")]
    [DataRow("rules", "roadEndClearMetres")]
    [DataRow("troops", "highLevelPercent")]
    [DataRow("growth", "healthPerWave")]
    [DataRow("growth", "damagePerWave")]
    [DataRow("growth", "maxFactor")]
    [DataRow("growth", "armourPercentPerWave")]
    [DataRow("growth", "armourPercentMax")]
    [DataRow("growth", "swingPercentPerWave")]
    [DataRow("growth", "swingPercentMax")]
    [DataRow("growth", "gapHealthPerLevel")]
    [DataRow("growth", "gapDamagePerLevel")]
    public void Floats_NotFinite_WarnAndKeepTheDefault(string group, string key)
    {
        foreach (var literal in new[] { "NaN", "Infinity", "-Infinity" })
        {
            var logger = Substitute.For<IModLogger>();

            var catalog = HeroDefenceConfigCatalog.FromTexts($@"{{ ""opening"": [], ""{group}"": {{ ""{key}"": {literal} }} }}", GoodCultures, GoodRosters, logger);

            Assert.AreEqual(new DefenceRules(), catalog.Rules, literal);
            Assert.AreEqual(DefaultNumbers, Numbers(catalog.Waves), literal);
            logger.Received().LogWarning(Arg.Is<string>(message => message.Contains($"{group}.{key}")));
        }
    }

    [DataTestMethod]
    [DataRow("rules")]
    [DataRow("ranks")]
    [DataRow("count")]
    [DataRow("troops")]
    [DataRow("growth")]
    [DataRow("leaders")]
    [DataRow("powers")]
    public void Groups_NotAnObject_WarnOnce(string group)
    {
        Catalog($@"{{ ""{group}"": 3 }}").Waves.ToString();

        _logger.Received(1).LogWarning(Arg.Is<string>(message => message.Contains($"'{group}' is not an object")));
    }

    [TestMethod]
    public void Ranks_ARankNotAnObject_WarnsOnce()
    {
        Catalog(@"{ ""ranks"": { ""captain"": 3 } }").Waves.ToString();

        _logger.Received(1).LogWarning(Arg.Is<string>(message => message.Contains("'ranks.captain' is not an object")));
    }

    [TestMethod]
    public void Ranks_ValidValues_AreRead()
    {
        var catalog = Catalog(@"{ ""ranks"": { ""general"": { ""lives"": 30, ""bounty"": 500, ""health"": 5 } } }");

        Assert.AreEqual(30, catalog.Rules.GeneralLifeCost);
        Assert.AreEqual(500, catalog.Rules.GeneralBounty);
        Assert.AreEqual(5f, catalog.Waves.LeaderHealth[2], 0.001f);
    }

    [DataTestMethod]
    [DataRow("soldier", "lives", "0")]
    [DataRow("captain", "lives", "-1")]
    [DataRow("lieutenant", "bounty", "-1")]
    [DataRow("general", "health", "0")]
    [DataRow("general", "health", "NaN")]
    [DataRow("captain", "health", "Infinity")]
    public void Ranks_OutOfRange_RevertWithAWarning(string rank, string key, string value)
    {
        var catalog = Catalog($@"{{ ""ranks"": {{ ""{rank}"": {{ ""{key}"": {value} }} }} }}");

        Assert.AreEqual(new DefenceRules(), catalog.Rules);
        CollectionAssert.AreEqual(new[] { 2f, 3f, 4f }, catalog.Waves.LeaderHealth.ToArray());
        AssertWarned($"ranks.{rank}.{key}");
    }

    [TestMethod]
    public void Waves_CountAndTroops_AreRead()
    {
        var waves = Catalog(@"{ ""count"": { ""start"": 10, ""perWave"": 2, ""max"": 50, ""packSize"": 5 }, ""troops"": { ""perLevel"": 3, ""nearestLowerLevels"": 5, ""highLevelPercent"": 75 } }").Waves;

        Assert.AreEqual(10, waves.CountStart);
        Assert.AreEqual(2, waves.CountPerWave);
        Assert.AreEqual(50, waves.CountMax);
        Assert.AreEqual(5, waves.PackSize);
        Assert.AreEqual(3, waves.TroopsPerLevel);
        Assert.AreEqual(5, waves.NearestLowerLevels);
        Assert.AreEqual(0.75d, waves.HighLevelShare, 0.001d);
    }

    [DataTestMethod]
    [DataRow("count", "start", "0")]
    [DataRow("count", "perWave", "-1")]
    [DataRow("count", "max", "0")]
    [DataRow("count", "max", "301")]
    [DataRow("count", "packSize", "0")]
    [DataRow("troops", "perLevel", "0")]
    [DataRow("troops", "nearestLowerLevels", "-1")]
    [DataRow("troops", "highLevelPercent", "101")]
    [DataRow("growth", "healthPerWave", "-1")]
    [DataRow("growth", "damagePerWave", "-1")]
    [DataRow("growth", "maxFactor", "0.5")]
    [DataRow("growth", "armourPercentPerWave", "-1")]
    [DataRow("growth", "armourPercentMax", "101")]
    [DataRow("growth", "swingPercentPerWave", "-1")]
    [DataRow("growth", "swingPercentMax", "101")]
    [DataRow("growth", "gapHealthPerLevel", "-1")]
    [DataRow("growth", "gapDamagePerLevel", "-1")]
    [DataRow("leaders", "everyWaves", "0")]
    public void Waves_OutOfRange_WarnsAndKeepsTheDefault(string group, string key, string value)
    {
        var waves = Catalog($@"{{ ""opening"": [], ""{group}"": {{ ""{key}"": {value} }} }}").Waves;

        Assert.AreEqual(DefaultNumbers, Numbers(waves));
        AssertWarned($"{group}.{key}");
    }

    [TestMethod]
    public void Waves_Growth_IsRead()
    {
        var waves = Catalog(@"{ ""growth"": { ""healthPerWave"": 0.1, ""damagePerWave"": 0.2, ""maxFactor"": 3, ""armourPercentMax"": 10 } }").Waves;

        Assert.AreEqual(1.5f, waves.Scale.HealthFactor(5), 0.001f);
        Assert.AreEqual(2f, waves.Scale.DamageFactor(5), 0.001f);
        Assert.AreEqual(3f, waves.Scale.HealthFactor(100), 0.001f);
        Assert.AreEqual(10f, waves.ArmourPercentMax, 0.001f);
    }

    [TestMethod]
    public void Waves_Bands_AreRead()
    {
        var bands = Catalog(@"{ ""bands"": [ { ""fromWave"": 1, ""low"": 2, ""high"": 3 }, { ""fromWave"": 4, ""low"": 5, ""high"": 6 } ] }").Waves.Bands;

        Assert.AreEqual(2, bands.Count);
        Assert.AreEqual(new LevelBand(4, 5, 6), bands[1]);
    }

    [DataTestMethod]
    [DataRow(@"[ { ""fromWave"": 2, ""low"": 1, ""high"": 6 } ]")]
    [DataRow(@"[ { ""fromWave"": 1, ""low"": 7, ""high"": 6 } ]")]
    [DataRow(@"[ { ""fromWave"": 1, ""low"": 0, ""high"": 6 } ]")]
    [DataRow(@"[ { ""fromWave"": 1, ""low"": 1, ""high"": 6 }, { ""fromWave"": 1, ""low"": 1, ""high"": 6 } ]")]
    [DataRow(@"[ { ""fromWave"": 1, ""low"": ""a"", ""high"": 6 } ]")]
    [DataRow("[]")]
    [DataRow("3")]
    public void Waves_BadBands_UseTheShippedListWithAWarning(string bands)
    {
        var read = Catalog($@"{{ ""bands"": {bands} }}").Waves.Bands;

        Assert.AreEqual(10, read.Count);
        Assert.AreEqual(1, read[0].FromWave);
        AssertWarned("bands");
    }

    [TestMethod]
    public void Waves_NoBandsKey_UsesTheShippedListWithNoWarning()
    {
        Assert.AreEqual(10, Catalog().Waves.Bands.Count);
        AssertNoWarning();
    }

    [TestMethod]
    public void Waves_PowerSchedule_IsRead()
    {
        var waves = Catalog(@"{ ""powers"": { ""schedule"": [ { ""fromWave"": 3, ""count"": 2 } ], ""pool"": [ ""a"", ""b"" ] } }").Waves;

        Assert.AreEqual(new PowerStep(3, 2), waves.PowerSchedule.Single());
        CollectionAssert.AreEqual(new[] { "a", "b" }, waves.PowerPool.ToArray());
    }

    [DataTestMethod]
    [DataRow(@"[ { ""fromWave"": 0, ""count"": 1 } ]")]
    [DataRow(@"[ { ""fromWave"": 5, ""count"": 0 } ]")]
    [DataRow(@"[ { ""fromWave"": 5, ""count"": 1 }, { ""fromWave"": 5, ""count"": 2 } ]")]
    [DataRow(@"[ { ""fromWave"": 5, ""count"": 2 }, { ""fromWave"": 10, ""count"": 1 } ]")]
    [DataRow("3")]
    public void Waves_BadPowerSchedule_UsesTheShippedListWithAWarning(string schedule)
    {
        var read = Catalog($@"{{ ""powers"": {{ ""schedule"": {schedule} }} }}").Waves.PowerSchedule;

        Assert.AreEqual(5, read.Count);
        AssertWarned("powers.schedule");
    }

    [DataTestMethod]
    [DataRow(@"[ ""lash"", 3 ]")]
    [DataRow(@"[ ""lash"", ""lash"" ]")]
    [DataRow("[]")]
    [DataRow("3")]
    public void Waves_BadPool_UsesTheShippedListWithAWarning(string pool)
    {
        var read = Catalog($@"{{ ""powers"": {{ ""pool"": {pool} }} }}").Waves.PowerPool;

        Assert.AreEqual(5, read.Count);
        AssertWarned("powers.pool");
    }

    [TestMethod]
    public void Cultures_ValidFile_IsReadWithSignatureValues()
    {
        var culture = Catalog().Waves.Cultures.Single();

        Assert.AreEqual("a", culture.Id);
        Assert.AreEqual("wall", culture.Signature);
        Assert.AreEqual(70, culture.Mix[CreepClass.Infantry]);
        Assert.AreEqual(30, culture.Mix[CreepClass.Ranged]);
        Assert.AreEqual(0.5f, culture.CountFactor, 0.001f);
        Assert.AreEqual("hide", culture.FirstPower);
        CollectionAssert.AreEqual(new[] { "x", "y", "z" }, culture.Leaders.ToArray());
        Assert.AreEqual(2, culture.Roster.Count);
        Assert.AreEqual(new RosterRow("t2", 11, CreepClass.Ranged), culture.Roster[1]);
        AssertNoWarning();
    }

    [TestMethod]
    public void Cultures_ShuffledSignature_IsRead()
    {
        var text = GoodCultures.Replace(@"""countFactor"": 0.5", @"""shuffled"": true, ""countFactor"": 0.5");

        Assert.IsTrue(Catalog(cultures: text).Waves.Signatures["wall"].Shuffled);
    }

    [TestMethod]
    public void Cultures_MissingFile_GivesNoCulturesAndAWarning()
    {
        Assert.AreEqual(0, Catalog(cultures: null).Waves.Cultures.Count);
        AssertWarned("cultures.json");
    }

    [TestMethod]
    public void Cultures_BrokenJson_GivesNoCulturesAndAWarning()
    {
        Assert.AreEqual(0, Catalog(cultures: "{ not json").Waves.Cultures.Count);
        AssertWarned("cultures.json");
    }

    [TestMethod]
    public void Cultures_NoValidCulture_WarnsThatTheModeCannotStart()
    {
        Assert.AreEqual(0, Catalog(cultures: "{}").Waves.Cultures.Count);
        AssertWarned("no valid culture");
    }

    [DataTestMethod]
    [DataRow(@"[""Infantry"",""Ranged"",""Cavalry""]")]
    [DataRow(@"[""Infantry"",""Ranged"",""Cavalry"",""Infantry""]")]
    [DataRow(@"[""Infantry"",""Ranged"",""Cavalry"",""Dragon""]")]
    public void Cultures_ASignatureWithABadOrder_IsSkippedAndItsCulturesWithIt(string order)
    {
        var text = GoodCultures.Replace(@"[""Infantry"",""Ranged"",""Cavalry"",""HorseArcher""]", order);

        Assert.AreEqual(0, Catalog(cultures: text).Waves.Cultures.Count);
        AssertWarned("signatures.wall");
    }

    [DataTestMethod]
    [DataRow("0.1")]
    [DataRow("3")]
    [DataRow("NaN")]
    [DataRow("Infinity")]
    public void Cultures_ASignatureWithABadCountFactor_IsSkipped(string factor)
    {
        var text = GoodCultures.Replace("0.5", factor);

        Assert.AreEqual(0, Catalog(cultures: text).Waves.Cultures.Count);
        AssertWarned("signatures.wall.countFactor");
    }

    [TestMethod]
    public void Cultures_ASignatureWithAPowerOutsideThePool_IsSkipped()
    {
        var text = GoodCultures.Replace(@"""firstPower"": ""hide""", @"""firstPower"": ""dragonfire""");

        Assert.AreEqual(0, Catalog(cultures: text).Waves.Cultures.Count);
        AssertWarned("signatures.wall.firstPower");
    }

    [TestMethod]
    public void Cultures_ASignatureWithAMistypedShuffled_IsSkipped()
    {
        var text = GoodCultures.Replace(@"""countFactor"": 0.5", @"""shuffled"": ""yes"", ""countFactor"": 0.5");

        Assert.AreEqual(0, Catalog(cultures: text).Waves.Cultures.Count);
        AssertWarned("signatures.wall.shuffled");
    }

    [TestMethod]
    public void Cultures_ASignatureNotAnObject_IsSkipped()
    {
        var text = @"{ ""signatures"": { ""wall"": 3 }, ""cultures"": { } }";

        Assert.AreEqual(0, Catalog(cultures: text).Waves.Signatures.Count);
        AssertWarned("signatures.wall");
    }

    [TestMethod]
    public void Cultures_ACultureNotAnObject_IsSkipped()
    {
        var text = GoodCultures.Replace(@"""a"": { ""signature"": ""wall"", ""mix"": { ""infantry"": 70, ""ranged"": 30 }, ""leaders"": [""x"",""y"",""z""] }", @"""a"": 3");

        Assert.AreEqual(0, Catalog(cultures: text).Waves.Cultures.Count);
        AssertWarned("cultures.a");
    }

    [TestMethod]
    public void Cultures_ACultureMissingFromTheRosters_IsSkippedAndTheOrphanRosterWarns()
    {
        var text = @"{ ""b"": [ { ""id"": ""t1"", ""level"": 6, ""class"": ""Infantry"" } ] }";

        Assert.AreEqual(0, Catalog(rosters: text).Waves.Cultures.Count);
        AssertWarned("cultures.a");
        AssertWarned("'b'");
    }

    [TestMethod]
    public void Cultures_AnUnknownSignature_SkipsTheCulture()
    {
        var text = GoodCultures.Replace(@"""signature"": ""wall""", @"""signature"": ""wal""");

        Assert.AreEqual(0, Catalog(cultures: text).Waves.Cultures.Count);
        AssertWarned("cultures.a");
    }

    [DataTestMethod]
    [DataRow(@"""infantry"": 0")]
    [DataRow(@"""infantry"": -5")]
    [DataRow(@"""infantry"": 101")]
    [DataRow(@"""dragons"": 50")]
    [DataRow(@"""infantry"": 40, ""ranged"": 30")]
    [DataRow(@"""ranged"": 100")]
    public void Cultures_ABadMix_SkipsTheCulture(string mix)
    {
        var text = GoodCultures.Replace(@"""infantry"": 70, ""ranged"": 30", mix);

        Assert.AreEqual(0, Catalog(cultures: text).Waves.Cultures.Count);
        AssertWarned("cultures.a.mix");
    }

    [DataTestMethod]
    [DataRow(@"[""x"","" "",""z""]")]
    [DataRow(@"[""x"",""y""]")]
    [DataRow(@"[""x"",""x"",""z""]")]
    [DataRow(@"[""x"",""y"",""z"",""w""]")]
    [DataRow("3")]
    public void Cultures_BadLeaders_SkipTheCulture(string leaders)
    {
        var text = GoodCultures.Replace(@"[""x"",""y"",""z""]", leaders);

        Assert.AreEqual(0, Catalog(cultures: text).Waves.Cultures.Count);
        AssertWarned("cultures.a.leaders");
    }

    [TestMethod]
    public void Rosters_MissingFile_LeavesNoCulturesAndWarns()
    {
        Assert.AreEqual(0, Catalog(rosters: null).Waves.Cultures.Count);
        AssertWarned("rosters.json");
    }

    [DataTestMethod]
    [DataRow(@"{ ""id"": """", ""level"": 6, ""class"": ""Infantry"" }")]
    [DataRow(@"{ ""id"": ""b"", ""level"": 0, ""class"": ""Infantry"" }")]
    [DataRow(@"{ ""id"": ""b"", ""level"": 64, ""class"": ""Infantry"" }")]
    [DataRow(@"{ ""id"": ""b"", ""level"": 6, ""class"": ""Dragon"" }")]
    [DataRow("3")]
    public void Rosters_ABadRow_IsSkippedAndTheOthersStay(string row)
    {
        var text = GoodRosters.Replace(@"{ ""id"": ""t2"", ""level"": 11, ""class"": ""Ranged"" }", row);

        Assert.AreEqual("t1", Catalog(rosters: text).Waves.Cultures.Single().Roster.Single().TroopId);
        AssertWarned("rosters.json");
    }

    [TestMethod]
    public void Rosters_ADuplicateId_IsSkippedWithAWarning()
    {
        var text = @"{ ""a"": [ { ""id"": ""t1"", ""level"": 6, ""class"": ""Infantry"" }, { ""id"": ""t1"", ""level"": 11, ""class"": ""Ranged"" } ] }";

        Assert.AreEqual(new RosterRow("t1", 6, CreepClass.Infantry), Catalog(rosters: text).Waves.Cultures.Single().Roster.Single());
        AssertWarned("'t1'");
    }

    [TestMethod]
    public void Rosters_ACultureWithNoInfantry_LeavesTheRotation()
    {
        var text = @"{ ""a"": [ { ""id"": ""t2"", ""level"": 11, ""class"": ""Ranged"" } ] }";

        Assert.AreEqual(0, Catalog(rosters: text).Waves.Cultures.Count);
        AssertWarned("no infantry");
    }

    [TestMethod]
    public void Opening_KnownIds_AreKept()
    {
        CollectionAssert.AreEqual(new[] { "a" }, Catalog(@"{ ""opening"": [ ""a"" ] }").Waves.Opening.ToArray());
        AssertNoWarning();
    }

    [TestMethod]
    public void Opening_AnUnknownId_IsDroppedWithAWarning()
    {
        CollectionAssert.AreEqual(new[] { "a" }, Catalog(@"{ ""opening"": [ ""nobody"", ""a"" ] }").Waves.Opening.ToArray());
        AssertWarned("opening");
    }

    [TestMethod]
    public void Opening_ARepeatOfTheWaveBefore_IsDroppedWithAWarning()
    {
        CollectionAssert.AreEqual(new[] { "a" }, Catalog(@"{ ""opening"": [ ""a"", ""a"" ] }").Waves.Opening.ToArray());
        AssertWarned("twice in a row");
    }

    [TestMethod]
    public void Opening_ACultureThatHasNotEnteredAtItsWave_IsDroppedWithAWarning()
    {
        var rosters = @"{ ""a"": [ { ""id"": ""t1"", ""level"": 11, ""class"": ""Infantry"" } ] }";

        var opening = Catalog(@"{ ""opening"": [ ""a"" ] }", rosters: rosters).Waves.Opening;

        Assert.AreEqual(0, opening.Count, "wave 1 has the band 1-6, and the lowest infantry of 'a' is level 11");
        AssertWarned("has not entered");
    }

    [TestMethod]
    public void Opening_NoKey_IsTheShippedOpening()
    {
        Catalog("{}").Waves.ToString();

        AssertWarned("'mordor'");
        AssertWarned("'erebor'");
    }

    [TestMethod]
    public void Catalog_BrokenWavesFile_UsesTheDefaultsAndWarns()
    {
        var catalog = Catalog("not json at all");

        Assert.AreEqual(new DefenceRules(), catalog.Rules);
        Assert.AreEqual(10, catalog.Waves.Bands.Count);
        AssertWarned("waves.json");
    }

    [TestMethod]
    public void Warnings_NameTheHeroDefenceFolder()
    {
        Catalog(@"{ ""rules"": { ""maxPlaced"": 99 } }").Waves.ToString();

        AssertWarned("hero_defence/waves.json");
    }
}
