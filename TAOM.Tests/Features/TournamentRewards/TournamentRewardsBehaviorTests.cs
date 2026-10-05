using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.TournamentRewards;

namespace TAOM.Tests.Features.TournamentRewards;

/// <summary>
/// The behavior's TournamentFinished listener runs before vanilla's handler (the order is explained on the
/// behavior), and an MbEvent invokes its listeners with no try. A fault in the listener must therefore be caught and
/// logged, never allowed to skip vanilla's renown and influence award behind it.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class TournamentRewardsBehaviorTests
{
    [TestMethod]
    public void OnTournamentFinished_AFaultInTheHandler_IsLoggedAndNeverEscapes()
    {
        // Building a CampaignBehaviorBase runs engine code, hence the category. The fault is the engine's own: with
        // no Game, CharacterObject.PlayerCharacter (Game.Current.PlayerTroop) throws inside the handler.
        var logger = Substitute.For<IModLogger>();
        var rewards = new TournamentRewardsService(
            Substitute.For<ITournamentRewardsSettingsProvider>(),
            Substitute.For<ITournamentRewardsConfigProvider>(),
            Substitute.For<IHeroSkillXpAdapter>());
        var award = new TournamentSkillAwardService(rewards, Substitute.For<ITournamentChoicePresenter>(),
            Substitute.For<IDedicatedServerProvider>());
        var behavior = new TournamentRewardsBehavior(rewards, award, logger);

        behavior.OnTournamentFinished(null!, null!, null!, null!);

        logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("tournament-finished handler failed")));
    }
}
