using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.BattleGearLoot.Hooks;

namespace TAOM.Tests.Features.BattleGearLoot;

[TestClass]
public class Patch172BattleGearLootTests
{
    [TestCleanup]
    public void Cleanup() => Patch172_BattleGearLoot.Initialize(null, null);

    [TestMethod]
    public void Prefix_ServiceNull_ReturnsTrue()
    {
        Patch172_BattleGearLoot.Initialize(null, Substitute.For<IModLogger>());
        Assert.IsTrue(Patch172_BattleGearLoot.Prefix(null!, null!, null!, 1, null!));
    }

    [TestCategory("RequiresGame")]   // constructs ItemRoster
    [TestMethod]
    public void Prefix_SwitchOff_ReturnsTrue_AndCallsNothing()
    {
        var settings = Substitute.For<TAOM.Features.BattleGearLoot.IBattleGearLootSettingsProvider>();
        settings.Get().Returns(new TAOM.Features.BattleGearLoot.BattleGearLootSettings(false, 100));
        var random = Substitute.For<TAOM.Features.TroopProgression.IRandomProvider>();
        var armourConfig = Substitute.For<TAOM.Features.ArmourAcquisition.IArmourAcquisitionConfigProvider>();
        var coop = Substitute.For<TAOM.Features.CoopInterop.ICoopSessionProvider>();
        var dedicated = Substitute.For<TAOM.Features.CoopInterop.IDedicatedServerProvider>();
        var service = new TAOM.Features.BattleGearLoot.BattleGearLootService(
            settings, random, armourConfig, coop, dedicated, Substitute.For<IOutcomeLog>());
        Patch172_BattleGearLoot.Initialize(service, Substitute.For<IModLogger>());

        Assert.IsTrue(Patch172_BattleGearLoot.Prefix(null!, null!, null!, 1, new TaleWorlds.CampaignSystem.Roster.ItemRoster()));

        settings.Received().Get();
        random.DidNotReceiveWithAnyArgs().Next(0);
        armourConfig.DidNotReceive().GetConfig();
    }

    [TestCategory("RequiresGame")]   // constructs ItemRoster
    [TestMethod]
    public void Prefix_NoBodyShareForThePlayer_LeavesVanillaWithoutReadingTheSettings()
    {
        var settings = Substitute.For<TAOM.Features.BattleGearLoot.IBattleGearLootSettingsProvider>();
        settings.Get().Returns(new TAOM.Features.BattleGearLoot.BattleGearLootSettings(true, 100));
        var service = new TAOM.Features.BattleGearLoot.BattleGearLootService(settings,
            Substitute.For<TAOM.Features.TroopProgression.IRandomProvider>(),
            Substitute.For<TAOM.Features.ArmourAcquisition.IArmourAcquisitionConfigProvider>(),
            Substitute.For<TAOM.Features.CoopInterop.ICoopSessionProvider>(),
            Substitute.For<TAOM.Features.CoopInterop.IDedicatedServerProvider>(), Substitute.For<IOutcomeLog>());
        Patch172_BattleGearLoot.Initialize(service, Substitute.For<IModLogger>());

        Assert.IsTrue(Patch172_BattleGearLoot.Prefix(null!, null!, null!, 0, new TaleWorlds.CampaignSystem.Roster.ItemRoster()));

        settings.DidNotReceive().Get();
    }
}
