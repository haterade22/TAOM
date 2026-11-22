using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.LongDefeat;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Tests.Features.LongDefeat.Config;

/// <summary>The <c>rules.hard</c> block of waves.json: a bad value keeps its default with one warning naming the file and the key.</summary>
[TestClass]
public class LongDefeatHardRulesReaderTests
{
    private IModLogger _logger = null!;

    [TestInitialize]
    public void Setup() => _logger = Substitute.For<IModLogger>();

    private HardRules Read(string rules) =>
        WavesReader.ReadHard(JObject.Parse(rules), new LongDefeatConfigScope(_logger, "waves.json"));

    private void AssertOneWarningNaming(string key) =>
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("waves.json") && m.Contains(key)));

    [TestMethod]
    public void NoHardBlock_IsTheDefaultsWithNoWarning()
    {
        Assert.AreEqual(HardRules.Default, Read("{'liveCap':150}"));
        Assert.AreEqual(HardRules.Default, WavesReader.ReadHard(null, new LongDefeatConfigScope(_logger, "waves.json")));
        _logger.DidNotReceiveWithAnyArgs().LogWarning(default!);
    }

    [TestMethod]
    public void BothKeys_ReadTheirValues()
    {
        Assert.AreEqual(new HardRules(1.5f, 4), Read("{'hard':{'factor':1.5,'affixWavesEarlier':4}}"));
        _logger.DidNotReceiveWithAnyArgs().LogWarning(default!);
    }

    [TestMethod]
    public void TheEdgesOfBothRanges_AreAccepted()
    {
        Assert.AreEqual(new HardRules(1.0f, 0), Read("{'hard':{'factor':1.0,'affixWavesEarlier':0}}"));
        Assert.AreEqual(new HardRules(2.0f, 10), Read("{'hard':{'factor':2.0,'affixWavesEarlier':10}}"));
        _logger.DidNotReceiveWithAnyArgs().LogWarning(default!);
    }

    [TestMethod]
    public void OneKeyMissing_KeepsThatDefaultWithNoWarning()
    {
        Assert.AreEqual(new HardRules(1.5f, 2), Read("{'hard':{'factor':1.5}}"));
        Assert.AreEqual(new HardRules(1.25f, 3), Read("{'hard':{'affixWavesEarlier':3}}"));
        _logger.DidNotReceiveWithAnyArgs().LogWarning(default!);
    }

    [DataTestMethod]
    [DataRow("factor", "0.9")]
    [DataRow("factor", "2.1")]
    [DataRow("factor", "NaN")]
    [DataRow("factor", "'1.5'")]
    public void BadFactor_KeepsTheDefaultWithOneWarning(string key, string value)
    {
        Assert.AreEqual(HardRules.Default, Read($"{{'hard':{{'{key}':{value}}}}}"));
        AssertOneWarningNaming("hard." + key);
    }

    [DataTestMethod]
    [DataRow("affixWavesEarlier", "-1")]
    [DataRow("affixWavesEarlier", "11")]
    [DataRow("affixWavesEarlier", "2.5")]
    [DataRow("affixWavesEarlier", "'2'")]
    public void BadAffixWavesEarlier_KeepsTheDefaultWithOneWarning(string key, string value)
    {
        Assert.AreEqual(HardRules.Default, Read($"{{'hard':{{'{key}':{value}}}}}"));
        AssertOneWarningNaming("hard." + key);
    }

    [TestMethod]
    public void AHardThatIsNotAnObject_IsTheDefaultsWithOneWarning()
    {
        Assert.AreEqual(HardRules.Default, Read("{'hard':5}"));
        AssertOneWarningNaming("rules.hard");
    }
}
