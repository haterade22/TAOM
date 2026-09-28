using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features;
using TAOM.Features.ArmourAcquisition;
using TAOM.Features.ArmourAcquisition.Domain;

namespace TAOM.Tests.Features.ArmourAcquisition;

/// <summary>
/// Pins the MCM-over-config merge. <c>TaomSettings.Instance</c> is null in the MSTest host, which makes the
/// MCM-absent direction testable: the master switch falls back to the config, and the two sub-toggles stay
/// on. They no longer fold in the live master: every consumer runs only while the game-init gate is active,
/// so turning the master off mid-game changes nothing until the next load, as its hint says.
/// </summary>
[TestClass]
public class ArmourAcquisitionSettingsProviderTests
{
    private static ArmourAcquisitionSettingsProvider Build(bool enabled)
    {
        var d = ArmourAcquisitionConfig.Default;
        var config = Substitute.For<IArmourAcquisitionConfigProvider>();
        config.GetConfig().Returns(new ArmourAcquisitionConfig(enabled, d.HeavyLevel, d.EliteLevel, d.LordLevel, d.Recipes,
            d.NamedWeapons, d.LordEventChance, d.LordEventCooldownDays, d.LordEventLeaveRelation, d.VisitChancePerDay,
            d.VisitDurationDays, d.VisitLevelBonus, d.HarnessOfferCooldownDays));
        return new ArmourAcquisitionSettingsProvider(config);
    }

    [TestMethod]
    public void IsEnabled_McmAbsent_FallsBackToTheConfig()
    {
        Assert.IsNull(TaomSettings.Instance, "this test assumes MCM is not loaded in the test host");

        Assert.IsTrue(Build(enabled: true).IsEnabled);
        Assert.IsFalse(Build(enabled: false).IsEnabled);
    }

    [TestMethod]
    public void SubToggles_McmAbsent_AreOnWhateverTheMaster()
    {
        Assert.IsNull(TaomSettings.Instance, "this test assumes MCM is not loaded in the test host");
        var provider = Build(enabled: false);

        Assert.IsTrue(provider.LordEventEnabled);
        Assert.IsTrue(provider.VisitingArmourerEnabled);
    }
}
