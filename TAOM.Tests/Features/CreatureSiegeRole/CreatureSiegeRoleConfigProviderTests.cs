using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.CreatureSiegeRole;

namespace TAOM.Tests.Features.CreatureSiegeRole;

/// <summary>
/// One test per validation rule (csharp-architecture.md "Config Providers MUST Validate"). The file holds one number, the
/// gate damage multiplier a creature's melee blow on a gate is scaled by, and every way it can be wrong reverts to the
/// compiled 2.0 with a warning that names the key: a missing file, text that is not JSON, JSON that is not an object, a
/// missing key (a typo'd key is a missing key), a value that is not a JSON number, a NaN or an infinity (rejected before the
/// range check, because every NaN comparison is false), and a finite value outside [1, 10]. The provider reads once per
/// process, so an edit needs a restart of the game.
/// </summary>
[TestClass]
public class CreatureSiegeRoleConfigProviderTests
{
    private string _tempDir = null!;
    private string _siegeDir = null!;
    private IModLogger _logger = null!;
    private CreatureSiegeRoleConfigProvider _sut = null!;

    private string ConfigPath => Path.Combine(_siegeDir, "creature_siege_role.json");

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "TAOM_CreatureSiegeRole_" + Path.GetRandomFileName());
        _siegeDir = Path.Combine(_tempDir, "siege");
        Directory.CreateDirectory(_siegeDir);
        var pathService = Substitute.For<IPathService>();
        pathService.ModuleDataPath.Returns(_tempDir);
        _logger = Substitute.For<IModLogger>();
        _sut = new CreatureSiegeRoleConfigProvider(pathService, _logger);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    private void Write(string json) => File.WriteAllText(ConfigPath, json);

    private void AssertWarnedAbout(string fragment) =>
        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains(fragment)));

    private void AssertWarnedOnce() => _logger.Received(1).LogWarning(Arg.Any<string>());

    [TestMethod]
    public void TheCompiledDefault_IsTwo_AndInsideTheRange()
    {
        Assert.AreEqual(2f, CreatureSiegeRoleConfigProvider.DefaultGateDamageMultiplier);
        Assert.AreEqual(1f, CreatureSiegeRoleConfigProvider.MinGateDamageMultiplier);
        Assert.AreEqual(10f, CreatureSiegeRoleConfigProvider.MaxGateDamageMultiplier);
    }

    [TestMethod]
    public void GateDamageMultiplier_FileMissing_IsTheDefault_AndWarns()
    {
        Assert.AreEqual(2f, _sut.GateDamageMultiplier);
        AssertWarnedAbout("not found");
    }

    [DataTestMethod]
    [DataRow("{ not json")]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow("{\"gateDamageMultiplier\": 2.")]
    public void GateDamageMultiplier_NotJson_IsTheDefault_AndWarns(string text)
    {
        Write(text);

        Assert.AreEqual(2f, _sut.GateDamageMultiplier);
        AssertWarnedAbout("could not be read");
    }

    [DataTestMethod]
    [DataRow("[]")]
    [DataRow("[3]")]
    [DataRow("true")]
    [DataRow("5")]
    [DataRow("null")]
    [DataRow("\"gateDamageMultiplier\"")]
    public void GateDamageMultiplier_JsonThatIsNotAnObject_IsTheDefault_AndWarns(string text)
    {
        Write(text);

        Assert.AreEqual(2f, _sut.GateDamageMultiplier);
        AssertWarnedAbout("not a JSON object");
    }

    [DataTestMethod]
    [DataRow("{}")]
    [DataRow("{ \"gateDamageMultipler\": 3 }")]
    [DataRow("{ \"GateDamageMultiplier\": 3 }")]
    [DataRow("{ \"somethingElse\": 3 }")]
    public void GateDamageMultiplier_KeyMissing_IsTheDefault_AndWarnsNamingTheKey(string text)
    {
        // A typo'd key is a missing key: the default holds, and the warning names the key a player should have typed.
        Write(text);

        Assert.AreEqual(2f, _sut.GateDamageMultiplier);
        AssertWarnedAbout("gateDamageMultiplier");
    }

    [DataTestMethod]
    [DataRow("{ \"gateDamageMultiplier\": \"3\" }")]
    [DataRow("{ \"gateDamageMultiplier\": \"2.5\" }")]
    [DataRow("{ \"gateDamageMultiplier\": true }")]
    [DataRow("{ \"gateDamageMultiplier\": null }")]
    [DataRow("{ \"gateDamageMultiplier\": [] }")]
    [DataRow("{ \"gateDamageMultiplier\": [3] }")]
    [DataRow("{ \"gateDamageMultiplier\": {} }")]
    public void GateDamageMultiplier_ValueNotANumber_IsTheDefault_AndWarns(string text)
    {
        // Newtonsoft would coerce "3" and true: a hand edit that quotes the value must not slip through.
        Write(text);

        Assert.AreEqual(2f, _sut.GateDamageMultiplier);
        AssertWarnedAbout("not a number");
    }

    [DataTestMethod]
    [DataRow("NaN")]
    [DataRow("Infinity")]
    [DataRow("-Infinity")]
    public void GateDamageMultiplier_NaNOrInfinity_IsRejectedBeforeTheRangeCheck(string literal)
    {
        // Json.NET reads these literals as numbers. Every NaN comparison is false, so `v < 1 || v > 10` would pass a NaN.
        Write("{ \"gateDamageMultiplier\": " + literal + " }");

        Assert.AreEqual(2f, _sut.GateDamageMultiplier);
        AssertWarnedAbout("finite");
    }

    [DataTestMethod]
    [DataRow("0.5")]
    [DataRow("0.999")]
    [DataRow("0")]
    [DataRow("-1")]
    [DataRow("-0.0")]
    public void GateDamageMultiplier_BelowOne_IsTheDefault_AndWarns(string number)
    {
        // Below 1 the "break the gate" multiplier would slow a gate break down.
        Write("{ \"gateDamageMultiplier\": " + number + " }");

        Assert.AreEqual(2f, _sut.GateDamageMultiplier);
        AssertWarnedAbout("between");
    }

    [DataTestMethod]
    [DataRow("10.01")]
    [DataRow("11")]
    [DataRow("1000")]
    [DataRow("1e300")]
    [DataRow("3.5e38")]
    public void GateDamageMultiplier_AboveTen_IsTheDefault_AndWarns(string number)
    {
        // 1e300 is finite as a double and an infinity as a float: the check runs on the double.
        Write("{ \"gateDamageMultiplier\": " + number + " }");

        Assert.AreEqual(2f, _sut.GateDamageMultiplier);
        AssertWarnedAbout("between");
    }

    [TestMethod]
    public void GateDamageMultiplier_ANumberTooBigForADouble_IsTheDefault_AndWarnsOnce()
    {
        Write("{ \"gateDamageMultiplier\": 1e400 }");

        Assert.AreEqual(2f, _sut.GateDamageMultiplier);
        AssertWarnedOnce();
    }

    [DataTestMethod]
    [DataRow("1", 1f)]
    [DataRow("1.0", 1f)]
    [DataRow("10", 10f)]
    [DataRow("10.0", 10f)]
    [DataRow("2.5", 2.5f)]
    [DataRow("3", 3f)]
    [DataRow("4.5", 4.5f)]
    [DataRow("1.000001", 1.000001f)]
    public void GateDamageMultiplier_ANumberInRange_IsAccepted_WithNoWarning(string number, float expected)
    {
        Write("{ \"gateDamageMultiplier\": " + number + " }");

        Assert.AreEqual(expected, _sut.GateDamageMultiplier);
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
        _logger.DidNotReceive().LogError(Arg.Any<string>());
    }

    [TestMethod]
    public void GateDamageMultiplier_OtherKeysInTheFile_AreIgnored()
    {
        Write("{ \"gateDamageMultiplier\": 3, \"comment\": \"tuned in the spike\" }");

        Assert.AreEqual(3f, _sut.GateDamageMultiplier);
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void GateDamageMultiplier_IsReadOnce_SoAnEditNeedsARestart_AndAWarningIsNotRepeated()
    {
        Write("{ \"gateDamageMultiplier\": 3 }");
        Assert.AreEqual(3f, _sut.GateDamageMultiplier);

        Write("{ \"gateDamageMultiplier\": 6 }");

        Assert.AreEqual(3f, _sut.GateDamageMultiplier);
    }

    [TestMethod]
    public void GateDamageMultiplier_ABadFileReadTwice_WarnsOnce()
    {
        Write("{ \"gateDamageMultiplier\": 99 }");

        Assert.AreEqual(2f, _sut.GateDamageMultiplier);
        Assert.AreEqual(2f, _sut.GateDamageMultiplier);

        AssertWarnedOnce();
    }

    [TestMethod]
    public void GateDamageMultiplier_AValidFile_LogsTheValueOnce()
    {
        Write("{ \"gateDamageMultiplier\": 3 }");

        _ = _sut.GateDamageMultiplier;
        _ = _sut.GateDamageMultiplier;

        _logger.Received(1).LogInfo(Arg.Is<string>(s => s.Contains("gateDamageMultiplier") && s.Contains("3")));
    }

    [TestMethod]
    public void TheWarning_NamesTheValueItRejected_AndTheDefaultItUses()
    {
        Write("{ \"gateDamageMultiplier\": 99 }");

        _ = _sut.GateDamageMultiplier;

        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("99") && s.Contains("2")));
    }

    [TestMethod]
    public void TheProvider_NeverThrows_WhateverTheFileHolds()
    {
        foreach (var text in new[] { "\0\0\0", "{\"gateDamageMultiplier\": 3}\u0000", "{\"gateDamageMultiplier\": 0x10}", "{\"a\":" })
        {
            Write(text);
            var pathService = Substitute.For<IPathService>();
            pathService.ModuleDataPath.Returns(_tempDir);
            var provider = new CreatureSiegeRoleConfigProvider(pathService, _logger);

            var value = provider.GateDamageMultiplier;

            Assert.IsTrue(value >= 1f && value <= 10f, $"{text}: {value}");
        }
    }
}
