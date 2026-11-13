using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features;
using TAOM.Features.SpecialResources;

namespace TAOM.Tests.Features.SpecialResources;

/// <summary>#1108: the gold-only option. The hot-path read-through rule is pinned in CampaignHotPathSettingsProvidersTests.</summary>
[TestClass]
public class SpecialResourceSettingsProviderTests
{
    private IModLogger _logger;

    [TestInitialize]
    public void Setup() => _logger = Substitute.For<IModLogger>();

    private SpecialResourceSettingsProvider WithRate(int rate) =>
        new(new TaomSettings { EmissaryGoldPerResource = rate }, _logger);

    [TestMethod]
    public void EliteTroopsCostGoldOnly_CompiledDefault_IsOff() =>
        Assert.IsFalse(new SpecialResourceSettingsProvider(new TaomSettings(), _logger).EliteTroopsCostGoldOnly);

    [TestMethod]
    public void EliteTroopsCostGoldOnly_FollowsTheMcmSetting()
    {
        var mcm = new TaomSettings { EliteTroopsCostGoldOnly = true };
        Assert.IsTrue(new SpecialResourceSettingsProvider(mcm, _logger).EliteTroopsCostGoldOnly);
    }

    // MCM edits its one TaomSettings in place, so an edit after the first read must reach the getters.
    [TestMethod]
    public void Getters_ReadThroughTheCachedSettings()
    {
        var mcm = new TaomSettings();
        var sut = new SpecialResourceSettingsProvider(mcm, _logger);
        Assert.IsFalse(sut.EliteTroopsCostGoldOnly);
        Assert.AreEqual(200, sut.EmissaryGoldPerResource);

        mcm.EliteTroopsCostGoldOnly = true;
        mcm.EmissaryGoldPerResource = 137;

        Assert.IsTrue(sut.EliteTroopsCostGoldOnly);
        Assert.AreEqual(137, sut.EmissaryGoldPerResource);
    }

    [TestMethod]
    public void EliteTroopsCostGoldOnly_NoMcm_FallsBackToOff() =>
        Assert.IsFalse(new SpecialResourceSettingsProvider(_logger).EliteTroopsCostGoldOnly);

    [TestMethod]
    public void EmissaryGoldPerResource_NoMcm_UsesTheDefault() =>
        Assert.AreEqual(200, new SpecialResourceSettingsProvider(_logger).EmissaryGoldPerResource);

    [TestMethod]
    public void EmissaryGoldPerResource_CompiledDefault_Is200() =>
        Assert.AreEqual(200, new SpecialResourceSettingsProvider(new TaomSettings(), _logger).EmissaryGoldPerResource);

    [DataTestMethod]
    [DataRow(50)]
    [DataRow(137)]
    [DataRow(1000)]
    public void EmissaryGoldPerResource_InRange_IsUsedAsSet(int rate)
    {
        Assert.AreEqual(rate, WithRate(rate).EmissaryGoldPerResource);
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [DataTestMethod]
    [DataRow(49)]
    [DataRow(1001)]
    [DataRow(0)]
    [DataRow(-5)]
    [DataRow(int.MaxValue)]
    public void EmissaryGoldPerResource_OutOfRange_UsesTheDefaultAndWarns(int rate)
    {
        Assert.AreEqual(200, WithRate(rate).EmissaryGoldPerResource);
        _logger.Received(1).LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void EmissaryGoldPerResource_OutOfRange_WarnsOnlyOnce()
    {
        var provider = WithRate(0);

        _ = provider.EmissaryGoldPerResource;
        _ = provider.EmissaryGoldPerResource;

        _logger.Received(1).LogWarning(Arg.Any<string>());
    }

    [DataTestMethod]
    [DataRow(nameof(TaomSettings.EliteTroopsCostGoldOnly))]
    [DataRow(nameof(TaomSettings.EmissaryGoldPerResource))]
    public void Setting_NeedsNoRestart_AndLivesUnderSpecialResources(string name)
    {
        var text = string.Join("|", typeof(TaomSettings).GetProperty(name)!.GetCustomAttributesData().Select(a => a.ToString()));

        StringAssert.Contains(text, "Special Resources");
        StringAssert.Contains(text, "RequireRestart = False");
    }
}
