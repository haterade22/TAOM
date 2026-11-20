using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features;
using TAOM.Features.Stamina;
using TAOM.Features.Stamina.Domain;

namespace TAOM.Tests.Features.Stamina;

[TestClass]
public class StaminaConfigProviderTests
{
    private const float Delta = 0.0001f;

    private string _tempDir = null!;
    private string _configDir = null!;
    private IModLogger _logger = null!;
    private StaminaConfigProvider _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "TAOM_Stamina_" + Path.GetRandomFileName());
        _configDir = Path.Combine(_tempDir, "configs");
        Directory.CreateDirectory(_configDir);
        var pathService = Substitute.For<IPathService>();
        pathService.ModuleDataPath.Returns(_tempDir);
        _logger = Substitute.For<IModLogger>();
        _sut = new StaminaConfigProvider(pathService, _logger);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    private void WriteConfig(string json) => File.WriteAllText(Path.Combine(_configDir, "stamina.json"), json);

    private static void AssertDefaults(StaminaTuning tuning)
    {
        Assert.AreEqual(StaminaTuning.DefaultTired.Swing, tuning.EffectsOf(StaminaBand.Tired).Swing, Delta);
        Assert.AreEqual(StaminaTuning.DefaultExhausted.Move, tuning.EffectsOf(StaminaBand.Exhausted).Move, Delta);
        Assert.AreEqual(StaminaTuning.DefaultSweepSeconds, tuning.SweepSeconds, Delta);
        Assert.AreEqual(StaminaTuning.DefaultMaxRefreshesPerFrame, tuning.MaxRefreshesPerFrame);
        Assert.AreEqual(StaminaTuning.DefaultMinRefreshAgeSeconds, tuning.MinRefreshAgeSeconds, Delta);
    }

    [TestMethod]
    public void GetTuning_MissingFile_GivesTheDefaultsAndWarns()
    {
        var tuning = _sut.GetTuning();

        AssertDefaults(tuning);
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("not found")));
    }

    [TestMethod]
    public void GetTuning_FileThatDoesNotParse_GivesTheDefaultsAndLogsAnError()
    {
        WriteConfig("{ \"effects\": ");

        var tuning = _sut.GetTuning();

        AssertDefaults(tuning);
        _logger.Received().LogError(Arg.Is<string>(m => m.Contains("failed to parse")));
    }

    [TestMethod]
    public void GetTuning_EmptyObject_GivesTheDefaultsWithNoWarning()
    {
        WriteConfig("{}");

        var tuning = _sut.GetTuning();

        AssertDefaults(tuning);
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void GetTuning_ValidValues_AreUsed()
    {
        WriteConfig("{ \"effects\": { \"winded\": { \"swing\": 0.9, \"handling\": 0.95, \"move\": 0.9, \"aiAttack\": 0.7 },"
                    + " \"tired\": { \"swing\": 0.8, \"handling\": 0.85, \"move\": 0.8, \"aiAttack\": 0.4 },"
                    + " \"exhausted\": { \"swing\": 0.7, \"handling\": 0.75, \"move\": 0.3, \"aiAttack\": 0.2 } },"
                    + " \"tick\": { \"sweepSeconds\": 0.5, \"maxRefreshesPerFrame\": 4, \"minRefreshAgeSeconds\": 2.0 } }");

        var tuning = _sut.GetTuning();

        var winded = tuning.EffectsOf(StaminaBand.Winded);
        Assert.AreEqual(0.9f, winded.Swing, Delta);
        Assert.AreEqual(0.95f, winded.Handling, Delta);
        Assert.AreEqual(0.9f, winded.Move, Delta);
        Assert.AreEqual(0.7f, winded.AiAttack, Delta);
        Assert.AreEqual(0.8f, tuning.EffectsOf(StaminaBand.Tired).Swing, Delta);
        Assert.AreEqual(0.3f, tuning.EffectsOf(StaminaBand.Exhausted).Move, Delta);
        Assert.AreEqual(0.2f, tuning.EffectsOf(StaminaBand.Exhausted).AiAttack, Delta);
        Assert.AreEqual(0.5f, tuning.SweepSeconds, Delta);
        Assert.AreEqual(4, tuning.MaxRefreshesPerFrame);
        Assert.AreEqual(2f, tuning.MinRefreshAgeSeconds, Delta);
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [DataTestMethod]
    [DataRow("swing", "0.2", DisplayName = "swing below its range")]
    [DataRow("swing", "1.5", DisplayName = "a swing multiplier above 1 would speed a tired soldier up")]
    [DataRow("handling", "-1", DisplayName = "handling negative")]
    [DataRow("move", "0.0", DisplayName = "a move multiplier of 0 would root the soldier")]
    [DataRow("aiAttack", "0.0", DisplayName = "an attack multiplier below the engine floor")]
    public void GetTuning_BandValueOutOfRange_GivesThatFieldItsDefaultAndWarns(string field, string value)
    {
        WriteConfig("{ \"effects\": { \"tired\": { \"" + field + "\": " + value + " } } }");

        var tuning = _sut.GetTuning();

        AssertDefaults(tuning);
        var tired = tuning.EffectsOf(StaminaBand.Tired);
        Assert.AreEqual(StaminaTuning.DefaultTired.Swing, tired.Swing, Delta);
        Assert.AreEqual(StaminaTuning.DefaultTired.Handling, tired.Handling, Delta);
        Assert.AreEqual(StaminaTuning.DefaultTired.Move, tired.Move, Delta);
        Assert.AreEqual(StaminaTuning.DefaultTired.AiAttack, tired.AiAttack, Delta);
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("effects.tired." + field)));
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("one or more")));
    }

    [DataTestMethod]
    [DataRow("NaN")]
    [DataRow("Infinity")]
    [DataRow("-Infinity")]
    public void GetTuning_BandValueNotAFiniteNumber_GivesTheDefault(string literal)
    {
        // Newtonsoft reads these literals as float.NaN and the infinities; a bare range check would let NaN through.
        WriteConfig("{ \"effects\": { \"exhausted\": { \"move\": " + literal + " } } }");

        var tuning = _sut.GetTuning();

        Assert.AreEqual(StaminaTuning.DefaultExhausted.Move, tuning.EffectsOf(StaminaBand.Exhausted).Move, Delta);
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("effects.exhausted.move")));
    }

    [TestMethod]
    public void GetTuning_TiredMilderThanWinded_PutsThatMultiplierBackInAllBands()
    {
        // Each value is in its range, but a tired soldier would swing faster than a winded one.
        WriteConfig("{ \"effects\": { \"winded\": { \"swing\": 0.7, \"move\": 0.9 }, \"tired\": { \"swing\": 0.9, \"move\": 0.8 } } }");

        var tuning = _sut.GetTuning();

        Assert.AreEqual(StaminaTuning.DefaultWinded.Swing, tuning.EffectsOf(StaminaBand.Winded).Swing, Delta);
        Assert.AreEqual(StaminaTuning.DefaultTired.Swing, tuning.EffectsOf(StaminaBand.Tired).Swing, Delta);
        Assert.AreEqual(StaminaTuning.DefaultExhausted.Swing, tuning.EffectsOf(StaminaBand.Exhausted).Swing, Delta);
        Assert.AreEqual(0.9f, tuning.EffectsOf(StaminaBand.Winded).Move, Delta, "the other multipliers keep their valid values");
        Assert.AreEqual(0.8f, tuning.EffectsOf(StaminaBand.Tired).Move, Delta);
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("must not rise")));
    }

    [TestMethod]
    public void GetTuning_ExhaustedMilderThanTired_PutsThatMultiplierBackInAllBands()
    {
        WriteConfig("{ \"effects\": { \"tired\": { \"move\": 0.5 }, \"exhausted\": { \"move\": 0.9 } } }");

        var tuning = _sut.GetTuning();

        Assert.AreEqual(StaminaTuning.DefaultTired.Move, tuning.EffectsOf(StaminaBand.Tired).Move, Delta);
        Assert.AreEqual(StaminaTuning.DefaultExhausted.Move, tuning.EffectsOf(StaminaBand.Exhausted).Move, Delta);
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("must not rise")));
    }

    [DataTestMethod]
    [DataRow("sweepSeconds", "0.01")]
    [DataRow("sweepSeconds", "5")]
    [DataRow("sweepSeconds", "NaN")]
    [DataRow("minRefreshAgeSeconds", "-1")]
    [DataRow("minRefreshAgeSeconds", "60")]
    [DataRow("maxRefreshesPerFrame", "0")]
    [DataRow("maxRefreshesPerFrame", "1000")]
    public void GetTuning_TickValueOutOfRange_GivesTheDefaultAndWarns(string field, string value)
    {
        WriteConfig("{ \"tick\": { \"" + field + "\": " + value + " } }");

        var tuning = _sut.GetTuning();

        AssertDefaults(tuning);
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("tick." + field)));
    }

    [TestMethod]
    public void GetTuning_CalledTwice_ReadsTheFileOnce()
    {
        WriteConfig("{}");
        var first = _sut.GetTuning();
        File.Delete(Path.Combine(_configDir, "stamina.json"));

        Assert.AreSame(first, _sut.GetTuning());
    }

    [TestMethod]
    public void ShippedConfig_LoadsWithoutWarningOrError_AndMatchesTheCompiledDefaults()
    {
        // The provider is fail-soft, so a bad shipped value would load quietly as its default.
        var repoRoot = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\.."));
        var pathService = Substitute.For<IPathService>();
        pathService.ModuleDataPath.Returns(Path.Combine(repoRoot, @"Main\_Module\ModuleData"));
        var logger = Substitute.For<IModLogger>();

        var tuning = new StaminaConfigProvider(pathService, logger).GetTuning();

        logger.DidNotReceive().LogWarning(Arg.Any<string>());
        logger.DidNotReceive().LogError(Arg.Any<string>());
        foreach (var band in new[] { StaminaBand.Winded, StaminaBand.Tired, StaminaBand.Exhausted })
        {
            var shipped = tuning.EffectsOf(band);
            var compiled = StaminaTuning.Default.EffectsOf(band);
            Assert.AreEqual(compiled.Swing, shipped.Swing, Delta, band + " swing");
            Assert.AreEqual(compiled.Handling, shipped.Handling, Delta, band + " handling");
            Assert.AreEqual(compiled.Move, shipped.Move, Delta, band + " move");
            Assert.AreEqual(compiled.AiAttack, shipped.AiAttack, Delta, band + " aiAttack");
        }
        AssertDefaults(tuning);
    }

    private static void AssertEconomyEquals(StaminaEconomy expected, StaminaEconomy actual)
    {
        foreach (var property in typeof(StaminaEconomy).GetProperties())
            Assert.AreEqual((float)property.GetValue(expected), (float)property.GetValue(actual), Delta, property.Name);
    }

    [TestMethod]
    public void GetTuning_EmptyObject_GivesTheDefaultEconomy()
    {
        WriteConfig("{}");

        AssertEconomyEquals(StaminaEconomy.Default, _sut.GetTuning().Economy);
    }

    [TestMethod]
    public void GetTuning_ValidEconomyValues_AreUsed()
    {
        WriteConfig("{ \"pool\": { \"base\": 80, \"perTier\": 12, \"heroBonus\": 50 },"
                    + " \"costs\": { \"swingBase\": 3, \"swingPerKg\": 1.5, \"swingMin\": 4, \"swingMax\": 12, \"parry\": 5, \"jump\": 9 },"
                    + " \"run\": { \"drainPerSecond\": 0.03, \"floor\": 0.4, \"speedThreshold\": 2.5 },"
                    + " \"regen\": { \"perSecond\": 0.08, \"pauseSeconds\": 1.0, \"blocking\": 0.25, \"exhausted\": 2.0 },"
                    + " \"bands\": { \"windedBelow\": 0.5, \"tiredBelow\": 0.25, \"hysteresis\": 0.04, \"recoverAt\": 0.25 } }");

        var e = _sut.GetTuning().Economy;

        Assert.AreEqual(80f, e.PoolBase, Delta);
        Assert.AreEqual(12f, e.PoolPerTier, Delta);
        Assert.AreEqual(50f, e.HeroBonus, Delta);
        Assert.AreEqual(3f, e.SwingBase, Delta);
        Assert.AreEqual(1.5f, e.SwingPerKg, Delta);
        Assert.AreEqual(4f, e.SwingMin, Delta);
        Assert.AreEqual(12f, e.SwingMax, Delta);
        Assert.AreEqual(5f, e.Parry, Delta);
        Assert.AreEqual(9f, e.Jump, Delta);
        Assert.AreEqual(StaminaEconomy.Default.Kick, e.Kick, Delta, "a value the file leaves out keeps its default");
        Assert.AreEqual(0.03f, e.RunDrainPerSecond, Delta);
        Assert.AreEqual(0.4f, e.RunFloor, Delta);
        Assert.AreEqual(2.5f, e.RunSpeedThreshold, Delta);
        Assert.AreEqual(0.08f, e.RegenPerSecond, Delta);
        Assert.AreEqual(1f, e.RegenPauseSeconds, Delta);
        Assert.AreEqual(0.25f, e.BlockingRegen, Delta);
        Assert.AreEqual(2f, e.ExhaustedRegen, Delta);
        Assert.AreEqual(0.5f, e.WindedBelow, Delta);
        Assert.AreEqual(0.25f, e.TiredBelow, Delta);
        Assert.AreEqual(0.04f, e.Hysteresis, Delta);
        Assert.AreEqual(0.25f, e.RecoverAt, Delta);
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [DataTestMethod]
    [DataRow("pool", "base", "0", DisplayName = "a pool of 0 would exhaust every soldier at once")]
    [DataRow("pool", "base", "NaN")]
    [DataRow("pool", "perTier", "-5")]
    [DataRow("costs", "swingBase", "-1", DisplayName = "a negative cost would give stamina back")]
    [DataRow("costs", "parry", "Infinity")]
    [DataRow("costs", "shieldPerDamage", "3")]
    [DataRow("run", "drainPerSecond", "-0.1")]
    [DataRow("run", "floor", "1.5")]
    [DataRow("run", "speedThreshold", "0")]
    [DataRow("regen", "perSecond", "0", DisplayName = "no regeneration would lock an exhausted soldier for good")]
    [DataRow("regen", "perSecond", "NaN")]
    [DataRow("regen", "exhausted", "0")]
    [DataRow("regen", "pauseSeconds", "-2")]
    [DataRow("bands", "windedBelow", "1.2")]
    [DataRow("bands", "recoverAt", "0")]
    [DataRow("bands", "hysteresis", "0.9")]
    public void GetTuning_EconomyValueOutOfRange_GivesItsDefaultAndWarns(string section, string key, string value)
    {
        WriteConfig("{ \"" + section + "\": { \"" + key + "\": " + value + " } }");

        var tuning = _sut.GetTuning();

        AssertEconomyEquals(StaminaEconomy.Default, tuning.Economy);
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains(section + "." + key)));
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("one or more")));
    }

    [TestMethod]
    public void GetTuning_MisspelledEconomyName_Warns_AndTheRealValueKeepsItsDefault()
    {
        // Without the warning a typo loads the default and the log says the file loaded.
        WriteConfig("{ \"costs\": { \"swingbase\": 9 } }");

        var tuning = _sut.GetTuning();

        Assert.AreEqual(StaminaEconomy.Default.SwingBase, tuning.Economy.SwingBase, Delta);
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("costs.swingbase") && m.Contains("not a known setting")));
    }

    [TestMethod]
    public void GetTuning_SwingMinimumAboveItsMaximum_PutsBothBack()
    {
        WriteConfig("{ \"costs\": { \"swingMin\": 20, \"swingMax\": 10 } }");

        var e = _sut.GetTuning().Economy;

        Assert.AreEqual(StaminaEconomy.Default.SwingMin, e.SwingMin, Delta);
        Assert.AreEqual(StaminaEconomy.Default.SwingMax, e.SwingMax, Delta);
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("costs.swingMin") && m.Contains("above")));
    }

    [TestMethod]
    public void GetTuning_ShieldBaseAboveItsMaximum_PutsBothBack()
    {
        WriteConfig("{ \"costs\": { \"shieldBase\": 30, \"shieldMax\": 10 } }");

        var e = _sut.GetTuning().Economy;

        Assert.AreEqual(StaminaEconomy.Default.ShieldBase, e.ShieldBase, Delta);
        Assert.AreEqual(StaminaEconomy.Default.ShieldMax, e.ShieldMax, Delta);
    }

    [DataTestMethod]
    [DataRow("\"windedBelow\": 0.3, \"tiredBelow\": 0.5", DisplayName = "Tired above Winded")]
    [DataRow("\"tiredBelow\": 0.3, \"hysteresis\": 0.05, \"recoverAt\": 0.5", DisplayName = "a recovered soldier would land above Tired and flap")]
    public void GetTuning_BandEdgesOutOfOrder_PutsAllFourBack(string bands)
    {
        WriteConfig("{ \"bands\": { " + bands + " } }");

        var e = _sut.GetTuning().Economy;

        Assert.AreEqual(StaminaEconomy.Default.WindedBelow, e.WindedBelow, Delta);
        Assert.AreEqual(StaminaEconomy.Default.TiredBelow, e.TiredBelow, Delta);
        Assert.AreEqual(StaminaEconomy.Default.Hysteresis, e.Hysteresis, Delta);
        Assert.AreEqual(StaminaEconomy.Default.RecoverAt, e.RecoverAt, Delta);
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("bands must keep")));
    }

    [TestMethod]
    public void ShippedConfig_EconomyMatchesTheCompiledDefaults()
    {
        // The simulated fight (StaminaEconomySimulationTests) runs on the compiled defaults: the shipped file must
        // be the same numbers, or the game would run an economy no test has seen.
        var repoRoot = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\.."));
        var pathService = Substitute.For<IPathService>();
        pathService.ModuleDataPath.Returns(Path.Combine(repoRoot, @"Main\_Module\ModuleData"));
        var logger = Substitute.For<IModLogger>();

        var tuning = new StaminaConfigProvider(pathService, logger).GetTuning();

        logger.DidNotReceive().LogWarning(Arg.Any<string>());
        AssertEconomyEquals(StaminaEconomy.Default, tuning.Economy);
    }

    [TestMethod]
    public void ShippedConfig_TypedSections_HoldOnlyNamesTheCodeReads()
    {
        // The file itself is read here (lessons/testing-qa.md, the parity test must read the FILE): the typed sections
        // drop a name they do not know without a word, so a mistyped shipped key would load as its default and the
        // parity tests above would still pass.
        var repoRoot = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\.."));
        var root = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(Path.Combine(repoRoot, @"Main\_Module\ModuleData\configs\stamina.json")));

        AssertNamesKnown(root, typeof(StaminaConfig), "stamina.json");
        var effects = (Newtonsoft.Json.Linq.JObject)root["effects"]!;
        AssertNamesKnown(effects, typeof(StaminaEffectsConfig), "effects");
        foreach (var band in effects.Properties())
            AssertNamesKnown((Newtonsoft.Json.Linq.JObject)band.Value, typeof(StaminaBandConfig), "effects." + band.Name);
        AssertNamesKnown((Newtonsoft.Json.Linq.JObject)root["tick"]!, typeof(StaminaTickConfig), "tick");
        var peoples = (Newtonsoft.Json.Linq.JObject)root["peoples"]!;
        AssertNamesKnown(peoples, typeof(StaminaPeoplesConfig), "peoples");
        foreach (var table in new[] { "races", "cultures" })
            foreach (var row in ((Newtonsoft.Json.Linq.JObject)peoples[table]!).Properties())
                AssertNamesKnown((Newtonsoft.Json.Linq.JObject)row.Value, typeof(StaminaPeopleConfig), "peoples." + table + "." + row.Name);
    }

    // A name that starts with "_" is a comment key.
    private static void AssertNamesKnown(Newtonsoft.Json.Linq.JObject section, Type shape, string where)
    {
        var known = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in shape.GetProperties())
            known.Add(property.Name);
        foreach (var property in section.Properties())
            Assert.IsTrue(property.Name.StartsWith("_", StringComparison.Ordinal) || known.Contains(property.Name),
                where + " holds '" + property.Name + "', which " + shape.Name + " does not read");
    }

    [TestMethod]
    public void Enabled_NoMcm_IsOffAsTheCompiledDefault()
    {
        Assert.AreEqual(new TaomSettings().EnableStamina, new StaminaSettingsProvider().Enabled);
        Assert.IsFalse(new StaminaSettingsProvider().Enabled);
    }

    [TestMethod]
    public void Enabled_ReadsTheSwitchThroughTheCachedObject()
    {
        var mcm = new TaomSettings { EnableStamina = true };
        var sut = new StaminaSettingsProvider(mcm);
        Assert.IsTrue(sut.Enabled);

        mcm.EnableStamina = false;

        Assert.IsFalse(sut.Enabled, "the switch is read live, not copied at construction");
    }
}
