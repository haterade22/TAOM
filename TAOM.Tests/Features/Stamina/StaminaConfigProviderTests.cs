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
