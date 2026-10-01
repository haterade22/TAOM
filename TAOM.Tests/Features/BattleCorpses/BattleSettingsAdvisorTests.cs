using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Features.BattleCorpses;

namespace TAOM.Tests.Features.BattleCorpses;

/// <summary>
/// The ragdoll and corpse advisor (#701). The player's options are only ever lowered, never
/// raised, and only when the player asks: there is no per-battle ragdoll override in the engine,
/// so writing the saved option is the one lever, and it is the player's to pull.
/// </summary>
[TestClass]
public class BattleSettingsAdvisorTests
{
    private IGraphicsOptionsAdapter _options = null!;
    private IBattleCorpseSettingsProvider _settings = null!;
    private BattleSettingsAdvisor _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _options = Substitute.For<IGraphicsOptionsAdapter>();
        _settings = Substitute.For<IBattleCorpseSettingsProvider>();
        _settings.IsAdviceEnabled.Returns(true);
        _options.SaveOptions(Arg.Any<int>(), Arg.Any<int>()).Returns(true);
        _sut = new BattleSettingsAdvisor(_options, _settings);
    }

    // -------- Classification --------

    [DataTestMethod]
    [DataRow(0, 0, false)]
    [DataRow(3, 1, false)] // exactly the recommendation: 5 ragdolls, 25 corpses
    [DataRow(4, 1, true)]  // 10 ragdolls
    [DataRow(5, 1, true)]  // unlimited ragdolls
    [DataRow(3, 2, true)]  // 75 corpses
    [DataRow(3, 5, true)]  // unlimited corpses
    [DataRow(5, 5, true)]
    [DataRow(1, 0, false)]
    public void IsAboveRecommended_ClassifiesEveryOption(int ragdoll, int corpse, bool expected)
    {
        Assert.AreEqual(expected, BattleSettingsAdvisor.IsAboveRecommended(ragdoll, corpse));
    }

    [DataTestMethod]
    [DataRow(-1, 0)]
    [DataRow(6, 0)]
    [DataRow(0, -1)]
    [DataRow(0, 6)]
    public void IsAboveRecommended_UnknownOption_DoesNotNag(int ragdoll, int corpse)
    {
        Assert.IsFalse(BattleSettingsAdvisor.IsAboveRecommended(ragdoll, corpse));
    }

    [DataTestMethod]
    [DataRow(-1, 5)]
    [DataRow(5, -1)]
    public void IsAboveRecommended_OneUnknownOneAbove_Offers(int ragdoll, int corpse)
    {
        // Each option is judged on its own, as Apply lowers each on its own: an unreadable ragdoll
        // value must not hide a corpse option on Unlimited.
        Assert.IsTrue(BattleSettingsAdvisor.IsAboveRecommended(ragdoll, corpse));
    }

    // -------- ShouldOffer --------

    [TestMethod]
    public void ShouldOffer_RiskyAndEnabled_IsTrue()
    {
        _options.RagdollOption.Returns(5);
        _options.CorpseOption.Returns(5);

        Assert.IsTrue(_sut.ShouldOffer());
    }

    [TestMethod]
    public void ShouldOffer_AdviceTurnedOff_IsFalse()
    {
        _options.RagdollOption.Returns(5);
        _options.CorpseOption.Returns(5);
        _settings.IsAdviceEnabled.Returns(false);

        Assert.IsFalse(_sut.ShouldOffer());
    }

    [TestMethod]
    public void ShouldOffer_AlreadyAtRecommendation_IsFalse()
    {
        _options.RagdollOption.Returns(3);
        _options.CorpseOption.Returns(1);

        Assert.IsFalse(_sut.ShouldOffer());
    }

    // -------- ApplyRecommended --------

    [TestMethod]
    public void ApplyRecommended_FromUnlimited_WritesRecommendation()
    {
        _options.RagdollOption.Returns(5);
        _options.CorpseOption.Returns(5);

        Assert.IsTrue(_sut.ApplyRecommended());

        _options.Received(1).SaveOptions(BattleSettingsAdvisor.RecommendedRagdollOption, BattleSettingsAdvisor.RecommendedCorpseOption);
    }

    [TestMethod]
    public void ApplyRecommended_NeverRaisesALowerOption()
    {
        // A player already on 0 ragdolls and no corpses keeps both.
        _options.RagdollOption.Returns(0);
        _options.CorpseOption.Returns(0);

        _sut.ApplyRecommended();

        _options.Received(1).SaveOptions(0, 0);
    }

    [TestMethod]
    public void ApplyRecommended_LowersOnlyTheRiskyOne()
    {
        _options.RagdollOption.Returns(1);
        _options.CorpseOption.Returns(4);

        _sut.ApplyRecommended();

        _options.Received(1).SaveOptions(1, BattleSettingsAdvisor.RecommendedCorpseOption);
    }

    [TestMethod]
    public void ApplyRecommended_UnknownOption_WritesRecommendation()
    {
        _options.RagdollOption.Returns(-1);
        _options.CorpseOption.Returns(9);

        _sut.ApplyRecommended();

        _options.Received(1).SaveOptions(BattleSettingsAdvisor.RecommendedRagdollOption, BattleSettingsAdvisor.RecommendedCorpseOption);
    }

    [TestMethod]
    public void ApplyRecommended_SaveFails_ReportsFailure()
    {
        _options.RagdollOption.Returns(5);
        _options.CorpseOption.Returns(5);
        _options.SaveOptions(Arg.Any<int>(), Arg.Any<int>()).Returns(false);

        Assert.IsFalse(_sut.ApplyRecommended());
    }

    [TestMethod]
    public void RecommendedValues_AreFiveRagdollsAndLowCorpses()
    {
        // Mike, 2026-10-01: ragdolls one below 10 (option 3 = 5), corpses the lowest non-zero (option 1 = 25).
        Assert.AreEqual(3, BattleSettingsAdvisor.RecommendedRagdollOption);
        Assert.AreEqual(1, BattleSettingsAdvisor.RecommendedCorpseOption);
    }
}
