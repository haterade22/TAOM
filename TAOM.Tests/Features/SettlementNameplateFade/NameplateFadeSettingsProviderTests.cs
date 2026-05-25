using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.SettlementNameplateFade;

namespace TAOM.Tests.Features.SettlementNameplateFade;

[TestClass]
public class NameplateFadeSettingsProviderTests
{
    // TaomSettings.Instance is null in test environments (MCM v5 isn't loaded), so the
    // provider falls back to its compiled defaults. These tests verify the fallbacks
    // match the MCM slider defaults declared in TaomSettings.cs — drift between the two
    // would silently disable the feature in any path that touches the provider before
    // MCM finishes initializing.

    [TestMethod]
    public void Enabled_NoMcmInstance_DefaultsToTrue()
    {
        var sut = new NameplateFadeSettingsProvider();
        Assert.IsTrue(sut.Enabled);
    }

    [TestMethod]
    public void NearDistance_NoMcmInstance_Defaults80()
    {
        var sut = new NameplateFadeSettingsProvider();
        Assert.AreEqual(80f, sut.NearDistance);
    }

    [TestMethod]
    public void FarDistance_NoMcmInstance_Defaults200()
    {
        var sut = new NameplateFadeSettingsProvider();
        Assert.AreEqual(200f, sut.FarDistance);
    }
}
