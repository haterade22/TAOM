using System;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.CampaignSystem;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.CreatureBandits;

namespace TAOM.Tests.Features.CreatureBandits;

/// <summary>
/// #1084: the brood and troll band spawners create parties, which is server work. On a co-op client the daily tick
/// tried to spawn too and threw in <c>WarPartyComponent.OnInitialize</c>. With no campaign in the test host the first
/// world read (<c>Clan.All</c>) throws, and the tick's catch logs it, so an error line proves the tick ran past the gate
/// and silence proves it stopped there.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]   // constructs CampaignBehaviorBase descendants
public class CreatureSpawnCoopGateTests
{
    private static ICoopSessionProvider Coop(bool isAuthority)
    {
        var coop = Substitute.For<ICoopSessionProvider>();
        coop.IsAuthority.Returns(isAuthority);
        return coop;
    }

    private static void RunDailyTick(CampaignBehaviorBase behavior) =>
        behavior.GetType().GetMethod("OnDailyTick", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(behavior, null);

    private static CampaignBehaviorBase Brood(bool isAuthority, IModLogger logger) =>
        new CreatureBroodSpawnBehavior(Coop(isAuthority), logger);

    private static CampaignBehaviorBase Troll(bool isAuthority, IModLogger logger) =>
        new TrollBandSpawnBehavior(Coop(isAuthority), logger);

    [DataTestMethod]
    [DataRow("brood")]
    [DataRow("troll")]
    public void DailyTick_OnACoopClient_TouchesNothing(string spawner)
    {
        var logger = Substitute.For<IModLogger>();
        RunDailyTick(spawner == "brood" ? Brood(false, logger) : Troll(false, logger));
        logger.DidNotReceiveWithAnyArgs().LogError(default!);
        logger.DidNotReceiveWithAnyArgs().LogInfo(default!);
    }

    [DataTestMethod]
    [DataRow("brood")]
    [DataRow("troll")]
    public void DailyTick_WithAuthority_ReadsTheWorld(string spawner)
    {
        var logger = Substitute.For<IModLogger>();
        RunDailyTick(spawner == "brood" ? Brood(true, logger) : Troll(true, logger));
        logger.Received(1).LogError(Arg.Is<string>(s => s.Contains(nameof(NullReferenceException))));
    }

    [DataTestMethod]
    [DataRow("brood")]
    [DataRow("troll")]
    public void DailyTick_ReadsAuthorityOnEveryTick(string spawner)
    {
        // The provider's contract: Coop's host flag can change after load, so a value cached at construction is wrong.
        var coop = Substitute.For<ICoopSessionProvider>();
        coop.IsAuthority.Returns(true, false);
        var logger = Substitute.For<IModLogger>();
        CampaignBehaviorBase behavior = spawner == "brood"
            ? new CreatureBroodSpawnBehavior(coop, logger)
            : new TrollBandSpawnBehavior(coop, logger);
        RunDailyTick(behavior);
        RunDailyTick(behavior);
        logger.Received(1).LogError(Arg.Any<string>());
    }
}
