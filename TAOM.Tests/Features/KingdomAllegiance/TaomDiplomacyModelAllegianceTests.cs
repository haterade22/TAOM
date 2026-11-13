using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.CampaignSystem;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.Diplomacy;
using TAOM.Features.Diplomacy.Hooks;
using TAOM.Features.Diplomacy.Models;
using TAOM.Features.Execution;
using TAOM.Features.KingdomAllegiance;
using TAOM.Features.KingdomAllegiance.Models;
using TAOM.Features.Relations;

namespace TAOM.Tests.Features.KingdomAllegiance;

/// <summary>
/// The two join scores of <see cref="TaomDiplomacyModel"/> ask the allegiance service first. A veto never reaches vanilla's
/// formula, so the clan and realm can stay null here; a pass would run vanilla's, which needs a campaign.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class TaomDiplomacyModelAllegianceTests
{
    private AllegianceFactsReader _facts = null!;
    private TaomDiplomacyModel _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        var settings = Substitute.For<IKingdomAllegianceSettingsProvider>();
        settings.Enabled.Returns(true);
        var alignment = Substitute.For<IAlignmentService>();
        alignment.ResolveSide("free", Arg.Any<string>()).Returns(FactionSide.Free);
        alignment.ResolveSide("evil", Arg.Any<string>()).Returns(FactionSide.Evil);
        var service = new KingdomAllegianceService(settings, alignment, Substitute.For<ICoopSessionProvider>(),
            new OutcomeLog(Substitute.For<IModLogger>()));
        _facts = Substitute.For<AllegianceFactsReader>(Substitute.For<IPlayerControlQuery>(), Substitute.For<IOutcomeLog>());
        _facts.Read(Arg.Any<Clan?>(), Arg.Any<Kingdom?>()).Returns(new AllegianceFacts(
            new SideIds("evil", ""), new SideIds("free", ""), PlayerControl.NotPlayer, PlayerControl.NotPlayer, AlreadyServes: false));
        _sut = new TaomDiplomacyModel(Substitute.For<IWarOfTheRingService>(), Substitute.For<ICoopSessionProvider>(),
            Substitute.For<IOnAllianceAction>(), Substitute.For<IPersonalRelationService>(), service, _facts);
    }

    [TestMethod]
    public void GetScoreOfClanToJoinKingdom_AnOpposedAiClan_GetsTheVeto()
    {
        Assert.AreEqual(KingdomAllegianceService.VetoScore, _sut.GetScoreOfClanToJoinKingdom(null!, null!));
    }

    [TestMethod]
    public void GetScoreOfMercenaryToJoinKingdom_AnOpposedAiClan_GetsTheVeto()
    {
        Assert.AreEqual(KingdomAllegianceService.VetoScore, _sut.GetScoreOfMercenaryToJoinKingdom(null!, null!));
    }
}
