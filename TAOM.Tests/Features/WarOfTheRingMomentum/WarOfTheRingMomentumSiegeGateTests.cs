using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.Diplomacy;
using TAOM.Features.WarOfTheRingMomentum;
using TAOM.Features.WarOfTheRingMomentum.UI;
using TaleWorlds.CampaignSystem.MapEvents;

namespace TAOM.Tests.Features.WarOfTheRingMomentum;

/// <summary>
/// KingdomManager.SiegeCompleted transfers the settlement only for Siege, SallyOut and
/// BlockadeSallyOutBattle. SiegeOutside is also reported with a win (a defender joining from outside
/// the walls) and captures nothing, so it must not score a capture.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class WarOfTheRingMomentumSiegeGateTests
{
    [DataTestMethod]
    [DataRow(MapEvent.BattleTypes.Siege, true)]
    [DataRow(MapEvent.BattleTypes.SallyOut, true)]
    [DataRow(MapEvent.BattleTypes.BlockadeSallyOutBattle, true)]
    [DataRow(MapEvent.BattleTypes.SiegeOutside, false)]
    [DataRow(MapEvent.BattleTypes.FieldBattle, false)]
    public void OnSiegeCompleted_ScoresOnlyBattleTypesThatCapture(MapEvent.BattleTypes battleType, bool scored)
    {
        var adapter = Substitute.For<IWarEventSnapshotAdapter>();
        var settings = Substitute.For<IMomentumSettingsProvider>();
        settings.MomentumEnabled.Returns(true);
        var coop = Substitute.For<ICoopSessionProvider>();
        coop.IsAuthority.Returns(true);
        var sut = new WarOfTheRingMomentumBehavior(
            Substitute.For<IMomentumStateStore>(), Substitute.For<IMomentumEventService>(),
            Substitute.For<IMomentumEnrollmentService>(), Substitute.For<IMomentumVictoryService>(), adapter,
            settings, Substitute.For<IWarOfTheRingService>(), Substitute.For<IMomentumUIService>(),
            Substitute.For<IModLogger>(), coop);
        var handler = typeof(WarOfTheRingMomentumBehavior).GetMethod(
            "OnSiegeCompleted", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(handler, "OnSiegeCompleted moved");

        handler!.Invoke(sut, new object?[] { null, null, true, battleType });

        if (scored)
            adapter.ReceivedWithAnyArgs(1).FromSiege(default!, default!);
        else
            adapter.DidNotReceiveWithAnyArgs().FromSiege(default!, default!);
    }
}
