using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.CampaignStartOptions;
using TAOM.Features.TroopWeight;

namespace TAOM.Tests.Features.CampaignStartOptions;

/// <summary>
/// Troop Weight follows "campaign, then MCM, then default". MCM is not loaded in a unit
/// test, so <c>TaomSettings.Instance</c> is null and the last step answers: on for Troop Weight.
/// </summary>
[TestClass]
public class SettingsProvidersCampaignPrecedenceTests
{
    [DataTestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void TroopWeight_PinnedChoice_Wins(bool pinned)
    {
        var campaign = Substitute.For<ICampaignStartOptions>();
        campaign.TroopWeight.Returns(pinned);

        Assert.AreEqual(pinned, new TroopWeightSettingsProvider(campaign).IsEnabled);
    }

    [TestMethod]
    public void TroopWeight_NothingPinned_FallsBackToTheMcmDefault()
    {
        var sut = new TroopWeightSettingsProvider(Substitute.For<ICampaignStartOptions>());

        Assert.IsTrue(sut.IsEnabled);
        Assert.AreEqual(sut.NewCampaignDefault, sut.IsEnabled);
    }

    [TestMethod]
    public void TroopWeight_PinnedChoice_DoesNotChangeTheNewCampaignDefault()
    {
        var campaign = Substitute.For<ICampaignStartOptions>();
        campaign.TroopWeight.Returns(false);

        Assert.IsTrue(new TroopWeightSettingsProvider(campaign).NewCampaignDefault);
    }
}
