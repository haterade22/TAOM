using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CharacterCreation;
using TAOM.Features.PlayerSwitcher;
using TAOM.Features.PlayerSwitcher.Domain;

namespace TAOM.Tests.Features.PlayerSwitcher;

/// <summary>
/// What the last handover did is read after character creation by StartupResources and the treasury
/// restore. It is forgotten when every new character creation starts, before the handler registers, so a
/// registration that throws cannot leave an earlier campaign's record (and its lord's id and gold) for
/// those readers in a new campaign.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class PlayerSwitchRegistrationBehaviorTests
{
    [TestMethod]
    public void OnCharacterCreationInitialized_EvenWhenRegistrationThrows_ForgetsTheLastHandover()
    {
        var session = new PlayerSwitchSessionStore();
        session.RecordOutcome(SwitchOutcome.Switched, SwitchPath.AssumeIdentity, "lord_1_75", 18500);
        var sut = new PlayerSwitchRegistrationBehavior(
            Substitute.For<IHeroSwitchService>(), Substitute.For<ISwitchPlanner>(), session, session,
            Substitute.For<IPlayerSwitchPolicyProvider>(), Substitute.For<ICareerMenuService>(),
            Substitute.For<IInquiryAdapter>(), Substitute.For<IPlayerIdentityAdapter>(), Substitute.For<IModLogger>());

        // A null manager makes the registration throw, the path the catch turns into "unavailable".
        sut.OnCharacterCreationInitialized(null!);

        Assert.AreEqual(SwitchOutcome.NotAttempted, session.LastOutcome);
        Assert.AreEqual(-1, session.LastHeroGold);
        Assert.IsFalse(session.LordTakenOver);
    }
}
