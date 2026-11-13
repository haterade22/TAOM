using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.CharacterCreation;
using TAOM.Features.PlayerPossession;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.PlayerPossession;

/// <summary>Pins the player-switch listener and its guarantee that no exception leaves it.</summary>
[TestClass]
public class PlayerPossessionSwitchTests
{
    [TestMethod]
    public void RegisterEvents_ChecksForTheHandOff_AtThePlayerSwitch()
    {
        // CampaignEvents needs a running campaign, so the registrations are pinned from the source,
        // inside the body of RegisterEvents.
        var src = RepoPaths.ReadSource("Main/Features/PlayerPossession/PlayerPossessionBehavior.cs", stripComments: true);
        var start = src.IndexOf("public override void RegisterEvents()", System.StringComparison.Ordinal);
        Assert.IsTrue(start >= 0, "RegisterEvents is gone.");
        var body = src.Substring(start, src.IndexOf("\n    }", start, System.StringComparison.Ordinal) - start);

        StringAssert.Contains(body, "CampaignEvents.OnPlayerCharacterChangedEvent.AddNonSerializedListener(this, OnPlayerCharacterChanged)");
        StringAssert.Contains(body, "CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, CheckForPossession)");
        StringAssert.Contains(body, "CampaignEvents.OnCharacterCreationInitializedEvent.AddNonSerializedListener(this, _ => _possession.BeginCharacterCreation())");
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void OnPlayerCharacterChanged_WhenTheCheckThrows_LogsTheErrorAndReturns()
    {
        var logger = Substitute.For<IModLogger>();
        var sut = new PlayerPossessionBehavior(
            Substitute.For<IPlayerPossessionService>(),
            Substitute.For<IJoinReconciliationService>(),
            Substitute.For<ICareerMenuService>(),
            logger);

        // No game runs here, so Hero.MainHero throws inside the check.
        sut.OnPlayerCharacterChanged(null!, null!, null!, false);

        logger.Received(1).LogError(Arg.Is<string>(m => m.StartsWith("[Possession] Checking for a join hand-off at the player switch failed")));
    }
}
