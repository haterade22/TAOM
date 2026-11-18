using TAOM.Features.CampaignStartOptions;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.Diplomacy;
using TAOM.Features.Diplomacy.Models;

namespace TAOM.Tests.Features.Diplomacy;

/// <summary>
/// War declarations follow the War of the Ring phase: Peace allows none, IsengardWar only the pairs
/// the config lists, FullWar the lore rule (IsWarAllowed), WarEnded everything but a Permanent pair.
/// <c>GetWarVeto</c> is the one predicate the DeclareWarAction prefix, the alliance call-to-war prefix
/// and the kingdom-decision model share; <c>EndWarsForbiddenByPhase</c> ends wars the phase forbids.
/// </summary>
[TestClass]
public class WarOfTheRingWarVetoTests
{
    private IWarOfTheRingConfigProvider _configProvider = null!;
    private IDiplomacyService _diplomacyService = null!;
    private IAllianceAdapter _allianceAdapter = null!;
    private ITaomSettingsProvider _settingsProvider = null!;
    private IModLogger _logger = null!;
    private WarOfTheRingConfig _config = null!;

    [TestInitialize]
    public void Setup()
    {
        _config = new WarOfTheRingConfig
        {
            Enabled = true,
            Phase1 = new PhaseConfig
            {
                TriggerDay = 30,
                Wars = new List<WarDeclaration>
                {
                    new WarDeclaration { Attacker = "isengard", Defender = "rohan" },
                    new WarDeclaration { Attacker = "rivendell", Defender = "lothlorien" },
                }
            },
            Phase2 = new PhaseConfig
            {
                TriggerDay = 45,
                AutoWarBetweenHostileTiers = true,
                BlockPeaceBetweenHostileTiers = true
            },
            TestMode = new TestModeConfig { Enabled = false }
        };
        _configProvider = Substitute.For<IWarOfTheRingConfigProvider>();
        _configProvider.LoadConfig().Returns(_config);
        _diplomacyService = Substitute.For<IDiplomacyService>();
        // NSubstitute answers Permanent (enum value 0) and false for an unstubbed call.
        _diplomacyService.GetRelationshipTier(Arg.Any<string>(), Arg.Any<string>()).Returns(AllianceTier.Neutral);
        _diplomacyService.IsWarAllowed(Arg.Any<string>(), Arg.Any<string>()).Returns(true);
        _allianceAdapter = Substitute.For<IAllianceAdapter>();
        _allianceAdapter.GetAllKingdomIds().Returns(new List<string>
            { "isengard", "rohan", "gondor", "rivendell", "lothlorien" });
        _settingsProvider = Substitute.For<ITaomSettingsProvider>();
        _settingsProvider.IsAvailable.Returns(false);
        _logger = Substitute.For<IModLogger>();
    }

    private WarOfTheRingService SutAt(WarPhase phase)
    {
        var sut = new WarOfTheRingService(
            _configProvider, _diplomacyService, _allianceAdapter, _settingsProvider, Substitute.For<ICampaignStartOptions>(), _logger);
        sut.SetPhaseFromSave(phase);
        return sut;
    }

    private void MakePermanent(string a, string b)
    {
        _diplomacyService.GetRelationshipTier(a, b).Returns(AllianceTier.Permanent);
        _diplomacyService.GetRelationshipTier(b, a).Returns(AllianceTier.Permanent);
        _diplomacyService.IsWarAllowed(a, b).Returns(false);
        _diplomacyService.IsWarAllowed(b, a).Returns(false);
    }

    private void MakeSameAlignment(string a, string b)
    {
        _diplomacyService.IsWarAllowed(a, b).Returns(false);
        _diplomacyService.IsWarAllowed(b, a).Returns(false);
    }

    // --- GetWarVeto ---------------------------------------------------------------------------

    [TestMethod]
    public void GetWarVeto_PeacePhase_ReturnsPhasePeace()
    {
        Assert.AreEqual(WarVeto.PhasePeace, SutAt(WarPhase.Peace).GetWarVeto("isengard", "rohan"));
    }

    [TestMethod]
    public void GetWarVeto_JsonDisabled_FallsBackToIsWarAllowed()
    {
        _config.Enabled = false;
        var sut = SutAt(WarPhase.Peace);

        Assert.AreEqual(WarVeto.None, sut.GetWarVeto("gondor", "isengard"));

        MakeSameAlignment("gondor", "rohan");
        Assert.AreEqual(WarVeto.SameAlignment, sut.GetWarVeto("gondor", "rohan"));

        MakePermanent("rivendell", "lothlorien");
        Assert.AreEqual(WarVeto.Permanent, sut.GetWarVeto("rivendell", "lothlorien"));
    }

    [TestMethod]
    public void GetWarVeto_McmDisabled_FallsBackToIsWarAllowed()
    {
        _settingsProvider.IsAvailable.Returns(true);
        _settingsProvider.WarOfTheRingEnabled.Returns(false);
        var sut = SutAt(WarPhase.Peace);

        Assert.AreEqual(WarVeto.None, sut.GetWarVeto("gondor", "isengard"));

        MakeSameAlignment("gondor", "rohan");
        Assert.AreEqual(WarVeto.SameAlignment, sut.GetWarVeto("gondor", "rohan"));
    }

    [TestMethod]
    public void GetWarVeto_IsengardWarListedPair_ReturnsNone()
    {
        Assert.AreEqual(WarVeto.None, SutAt(WarPhase.IsengardWar).GetWarVeto("isengard", "rohan"));
    }

    [TestMethod]
    public void GetWarVeto_IsengardWarListedPairReversed_ReturnsNone()
    {
        Assert.AreEqual(WarVeto.None, SutAt(WarPhase.IsengardWar).GetWarVeto("rohan", "isengard"));
    }

    [TestMethod]
    public void GetWarVeto_IsengardWarUnlistedPair_ReturnsPhaseNotScripted()
    {
        Assert.AreEqual(WarVeto.PhaseNotScripted, SutAt(WarPhase.IsengardWar).GetWarVeto("isengard", "gondor"));
    }

    [TestMethod]
    public void GetWarVeto_IsengardWarListedPermanentPair_ReturnsPermanent()
    {
        MakePermanent("rivendell", "lothlorien");

        Assert.AreEqual(WarVeto.Permanent, SutAt(WarPhase.IsengardWar).GetWarVeto("rivendell", "lothlorien"));
    }

    [TestMethod]
    public void GetWarVeto_IsengardWarListedSameAlignmentPair_ReturnsSameAlignment()
    {
        MakeSameAlignment("isengard", "rohan");

        Assert.AreEqual(WarVeto.SameAlignment, SutAt(WarPhase.IsengardWar).GetWarVeto("isengard", "rohan"));
    }

    [TestMethod]
    public void GetWarVeto_FullWarOpposedPair_ReturnsNone()
    {
        Assert.AreEqual(WarVeto.None, SutAt(WarPhase.FullWar).GetWarVeto("gondor", "isengard"));
    }

    [TestMethod]
    public void GetWarVeto_FullWarSameAlignment_ReturnsSameAlignment()
    {
        MakeSameAlignment("gondor", "rohan");

        Assert.AreEqual(WarVeto.SameAlignment, SutAt(WarPhase.FullWar).GetWarVeto("gondor", "rohan"));
    }

    [TestMethod]
    public void GetWarVeto_FullWarPermanent_ReturnsPermanent()
    {
        MakePermanent("rivendell", "lothlorien");

        Assert.AreEqual(WarVeto.Permanent, SutAt(WarPhase.FullWar).GetWarVeto("rivendell", "lothlorien"));
    }

    [TestMethod]
    public void GetWarVeto_WarEndedSameAlignment_ReturnsNone()
    {
        MakeSameAlignment("gondor", "rohan");

        Assert.AreEqual(WarVeto.None, SutAt(WarPhase.WarEnded).GetWarVeto("gondor", "rohan"));
    }

    [TestMethod]
    public void GetWarVeto_WarEndedPermanent_ReturnsPermanent()
    {
        MakePermanent("rivendell", "lothlorien");

        Assert.AreEqual(WarVeto.Permanent, SutAt(WarPhase.WarEnded).GetWarVeto("rivendell", "lothlorien"));
    }

    [TestMethod]
    public void GetWarVeto_PeacePhaseSameAlignment_ReturnsSameAlignment()
    {
        MakeSameAlignment("gondor", "rohan");

        Assert.AreEqual(WarVeto.SameAlignment, SutAt(WarPhase.Peace).GetWarVeto("gondor", "rohan"));
    }

    [TestMethod]
    public void GetWarVeto_IsengardWarUnlistedSameAlignment_ReturnsSameAlignment()
    {
        MakeSameAlignment("gondor", "rohan");

        Assert.AreEqual(WarVeto.SameAlignment, SutAt(WarPhase.IsengardWar).GetWarVeto("gondor", "rohan"));
    }

    // --- Player-caused declarations lift only the phase vetoes ----------------------------------

    [DataTestMethod]
    [DataRow(WarPhase.Peace)]
    [DataRow(WarPhase.IsengardWar)]
    public void GetWarVeto_PlayerCausedPhaseVeto_ReturnsNone(WarPhase phase)
    {
        Assert.AreEqual(WarVeto.None, SutAt(phase).GetWarVeto("gondor", "isengard", causedByPlayer: true));
    }

    [DataTestMethod]
    [DataRow(WarPhase.Peace)]
    [DataRow(WarPhase.IsengardWar)]
    [DataRow(WarPhase.FullWar)]
    [DataRow(WarPhase.WarEnded)]
    public void GetWarVeto_PlayerCausedPermanent_StillReturnsPermanent(WarPhase phase)
    {
        MakePermanent("rivendell", "lothlorien");

        Assert.AreEqual(WarVeto.Permanent,
            SutAt(phase).GetWarVeto("rivendell", "lothlorien", causedByPlayer: true));
    }

    [DataTestMethod]
    [DataRow(WarPhase.Peace)]
    [DataRow(WarPhase.IsengardWar)]
    [DataRow(WarPhase.FullWar)]
    public void GetWarVeto_PlayerCausedSameAlignment_StillReturnsSameAlignment(WarPhase phase)
    {
        MakeSameAlignment("gondor", "rohan");

        Assert.AreEqual(WarVeto.SameAlignment,
            SutAt(phase).GetWarVeto("gondor", "rohan", causedByPlayer: true));
    }

    [TestMethod]
    public void GetWarVeto_PlayerCausedSameAlignmentInWarEnded_ReturnsNone()
    {
        MakeSameAlignment("gondor", "rohan");

        Assert.AreEqual(WarVeto.None,
            SutAt(WarPhase.WarEnded).GetWarVeto("gondor", "rohan", causedByPlayer: true));
    }

    [TestMethod]
    public void GetWarVeto_DisabledWarEndedSameAlignment_ReturnsSameAlignment()
    {
        _config.Enabled = false;
        MakeSameAlignment("gondor", "rohan");

        Assert.AreEqual(WarVeto.SameAlignment, SutAt(WarPhase.WarEnded).GetWarVeto("gondor", "rohan"));
    }

    // --- GetDeclareWarScorePenalty ----------------------------------------------------------------

    [TestMethod]
    public void GetDeclareWarScorePenalty_NoVeto_ReturnsZero()
    {
        Assert.AreEqual(0f, SutAt(WarPhase.FullWar).GetDeclareWarScorePenalty("gondor", "isengard"));
    }

    [DataTestMethod]
    [DataRow(WarPhase.Peace)]
    [DataRow(WarPhase.IsengardWar)]
    public void GetDeclareWarScorePenalty_PhaseVeto_ReturnsPhasePenalty(WarPhase phase)
    {
        Assert.AreEqual(WarOfTheRingService.PhaseVetoScorePenalty,
            SutAt(phase).GetDeclareWarScorePenalty("gondor", "isengard"));
    }

    [TestMethod]
    public void GetDeclareWarScorePenalty_PermanentAndSameAlignment_ReturnLorePenalty()
    {
        MakePermanent("rivendell", "lothlorien");
        MakeSameAlignment("gondor", "rohan");
        var sut = SutAt(WarPhase.FullWar);

        Assert.AreEqual(WarOfTheRingService.LoreVetoScorePenalty,
            sut.GetDeclareWarScorePenalty("rivendell", "lothlorien"));
        Assert.AreEqual(WarOfTheRingService.LoreVetoScorePenalty,
            sut.GetDeclareWarScorePenalty("gondor", "rohan"));
    }

    [TestMethod]
    public void GetDeclareWarScorePenalty_PeacePhaseSameAlignment_ReturnsLorePenalty()
    {
        MakeSameAlignment("gondor", "rohan");

        Assert.AreEqual(WarOfTheRingService.LoreVetoScorePenalty,
            SutAt(WarPhase.Peace).GetDeclareWarScorePenalty("gondor", "rohan"));
    }

    [TestMethod]
    public void ScorePenaltyConstants_LoreAndPhase_LoreIsLarger()
    {
        Assert.IsTrue(WarOfTheRingService.LoreVetoScorePenalty > WarOfTheRingService.PhaseVetoScorePenalty);
        Assert.IsTrue(WarOfTheRingService.PhaseVetoScorePenalty > 0f);
    }

    // --- The script's own declarations need no exemption --------------------------------------

    [TestMethod]
    public void CheckPhaseTransition_Phase1Day_PhaseIsIsengardWarWhenDeclareWarRuns()
    {
        var sut = new WarOfTheRingService(
            _configProvider, _diplomacyService, _allianceAdapter, _settingsProvider, Substitute.For<ICampaignStartOptions>(), _logger);
        var phasesSeen = new List<WarPhase>();
        var vetoesSeen = new List<WarVeto>();
        _allianceAdapter.When(a => a.DeclareWar(Arg.Any<string>(), Arg.Any<string>())).Do(call =>
        {
            phasesSeen.Add(sut.CurrentPhase);
            vetoesSeen.Add(sut.GetWarVeto(call.ArgAt<string>(0), call.ArgAt<string>(1)));
        });

        sut.CheckPhaseTransition(30f);

        Assert.AreEqual(2, phasesSeen.Count);
        CollectionAssert.AreEqual(new[] { WarPhase.IsengardWar, WarPhase.IsengardWar }, phasesSeen);
        CollectionAssert.AreEqual(new[] { WarVeto.None, WarVeto.None }, vetoesSeen);
    }

    [TestMethod]
    public void CheckPhaseTransition_Phase2Day_DeclaresHostileTierWarsThroughGate()
    {
        _config.Phase1.Wars.Clear();
        _diplomacyService.GetRelationshipTier("gondor", "isengard").Returns(AllianceTier.Hostile);
        _allianceAdapter.GetAllKingdomIds().Returns(new List<string> { "gondor", "isengard" });
        var sut = SutAt(WarPhase.IsengardWar);
        var vetoesSeen = new List<WarVeto>();
        _allianceAdapter.When(a => a.DeclareWar(Arg.Any<string>(), Arg.Any<string>())).Do(call =>
            vetoesSeen.Add(sut.GetWarVeto(call.ArgAt<string>(0), call.ArgAt<string>(1))));

        sut.CheckPhaseTransition(45f);

        Assert.AreEqual(WarPhase.FullWar, sut.CurrentPhase);
        CollectionAssert.AreEqual(new[] { WarVeto.None }, vetoesSeen);
    }

    // --- IsEnabled -------------------------------------------------------------------------------

    [DataTestMethod]
    [DataRow(true, true)]
    [DataRow(false, false)]
    public void IsEnabled_McmAvailable_ReadsMcmNotJson(bool mcm, bool expected)
    {
        _config.Enabled = !mcm;
        _settingsProvider.IsAvailable.Returns(true);
        _settingsProvider.WarOfTheRingEnabled.Returns(mcm);

        Assert.AreEqual(expected, SutAt(WarPhase.Peace).IsEnabled);
    }

    [DataTestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void IsEnabled_McmUnavailable_FallsBackToJson(bool json)
    {
        _config.Enabled = json;
        _settingsProvider.IsAvailable.Returns(false);
        _settingsProvider.WarOfTheRingEnabled.Returns(!json);

        Assert.AreEqual(json, SutAt(WarPhase.Peace).IsEnabled);
    }

    // --- EndWarsForbiddenByPhase ----------------------------------------------------------------

    private void AtWar(params (string, string)[] pairs)
    {
        foreach (var (a, b) in pairs)
        {
            _allianceAdapter.AreAtWar(a, b).Returns(true);
            _allianceAdapter.AreAtWar(b, a).Returns(true);
        }
        _allianceAdapter.When(x => x.MakePeace(Arg.Any<string>(), Arg.Any<string>())).Do(call =>
        {
            var x = call.ArgAt<string>(0);
            var y = call.ArgAt<string>(1);
            _allianceAdapter.AreAtWar(x, y).Returns(false);
            _allianceAdapter.AreAtWar(y, x).Returns(false);
        });
    }

    [TestMethod]
    public void EndWarsForbiddenByPhase_PeaceAtWar_MakesPeace()
    {
        AtWar(("isengard", "rohan"));

        var ended = SutAt(WarPhase.Peace).EndWarsForbiddenByPhase();

        Assert.AreEqual(1, ended);
        _allianceAdapter.Received(1).MakePeace("isengard", "rohan");
    }

    [TestMethod]
    public void EndWarsForbiddenByPhase_IsengardWarListedWar_KeepsWar()
    {
        AtWar(("isengard", "rohan"));

        var ended = SutAt(WarPhase.IsengardWar).EndWarsForbiddenByPhase();

        Assert.AreEqual(0, ended);
        _allianceAdapter.DidNotReceive().MakePeace(Arg.Any<string>(), Arg.Any<string>());
    }

    [TestMethod]
    public void EndWarsForbiddenByPhase_IsengardWarUnlistedWar_MakesPeace()
    {
        AtWar(("isengard", "rohan"), ("gondor", "isengard"));

        var ended = SutAt(WarPhase.IsengardWar).EndWarsForbiddenByPhase();

        Assert.AreEqual(1, ended);
        _allianceAdapter.Received(1).MakePeace("isengard", "gondor");
        _allianceAdapter.DidNotReceive().MakePeace("isengard", "rohan");
    }

    [TestMethod]
    public void EndWarsForbiddenByPhase_FullWar_DoesNothing()
    {
        AtWar(("gondor", "isengard"));

        Assert.AreEqual(0, SutAt(WarPhase.FullWar).EndWarsForbiddenByPhase());
        _allianceAdapter.DidNotReceive().MakePeace(Arg.Any<string>(), Arg.Any<string>());
    }

    [TestMethod]
    public void EndWarsForbiddenByPhase_WarEnded_DoesNothing()
    {
        AtWar(("gondor", "isengard"));

        Assert.AreEqual(0, SutAt(WarPhase.WarEnded).EndWarsForbiddenByPhase());
        _allianceAdapter.DidNotReceive().MakePeace(Arg.Any<string>(), Arg.Any<string>());
    }

    [TestMethod]
    public void EndWarsForbiddenByPhase_WotrDisabled_DoesNothing()
    {
        _config.Enabled = false;
        AtWar(("gondor", "isengard"));

        Assert.AreEqual(0, SutAt(WarPhase.Peace).EndWarsForbiddenByPhase());
        _allianceAdapter.DidNotReceive().MakePeace(Arg.Any<string>(), Arg.Any<string>());
    }

    [TestMethod]
    public void EndWarsForbiddenByPhase_McmDisabled_DoesNothing()
    {
        _settingsProvider.IsAvailable.Returns(true);
        _settingsProvider.WarOfTheRingEnabled.Returns(false);
        AtWar(("gondor", "isengard"));

        Assert.AreEqual(0, SutAt(WarPhase.Peace).EndWarsForbiddenByPhase());
        _allianceAdapter.DidNotReceive().MakePeace(Arg.Any<string>(), Arg.Any<string>());
    }

    [TestMethod]
    public void EndWarsForbiddenByPhase_NoWars_ReturnsZero()
    {
        Assert.AreEqual(0, SutAt(WarPhase.Peace).EndWarsForbiddenByPhase());
        _allianceAdapter.DidNotReceive().MakePeace(Arg.Any<string>(), Arg.Any<string>());
    }

    [TestMethod]
    public void EndWarsForbiddenByPhase_EndedSome_LogsTheCount()
    {
        AtWar(("isengard", "rohan"), ("gondor", "isengard"));

        SutAt(WarPhase.Peace).EndWarsForbiddenByPhase();

        _logger.Received().LogInfo(Arg.Is<string>(m => m.Contains("ended 2 wars")));
    }

    [TestMethod]
    public void EndWarsForbiddenByPhase_NothingToEnd_LogsNothing()
    {
        SutAt(WarPhase.Peace).EndWarsForbiddenByPhase();

        _logger.DidNotReceive().LogInfo(Arg.Is<string>(m => m.Contains("ended")));
    }

    [DataTestMethod]
    [DataRow(WarPhase.Peace)]
    [DataRow(WarPhase.IsengardWar)]
    public void EndWarsForbiddenByPhase_PlayerKingdomPhaseVetoedWar_KeepsWar(WarPhase phase)
    {
        _allianceAdapter.GetPlayerKingdomId().Returns("gondor");
        AtWar(("gondor", "isengard"), ("rohan", "rivendell"));

        var ended = SutAt(phase).EndWarsForbiddenByPhase();

        Assert.AreEqual(1, ended);
        _allianceAdapter.DidNotReceive().MakePeace("gondor", "isengard");
        _allianceAdapter.Received(1).MakePeace("rohan", "rivendell");
    }

    [TestMethod]
    public void EndWarsForbiddenByPhase_PlayerKingdomSameAlignmentWar_MakesPeace()
    {
        _allianceAdapter.GetPlayerKingdomId().Returns("gondor");
        MakeSameAlignment("gondor", "rohan");
        AtWar(("gondor", "rohan"));

        var ended = SutAt(WarPhase.Peace).EndWarsForbiddenByPhase();

        Assert.AreEqual(1, ended);
        _allianceAdapter.Received(1).MakePeace("rohan", "gondor");
    }

    [TestMethod]
    public void EndWarsForbiddenByPhase_PlayerKingdomPermanentWar_MakesPeace()
    {
        _allianceAdapter.GetPlayerKingdomId().Returns("rivendell");
        MakePermanent("rivendell", "lothlorien");
        AtWar(("rivendell", "lothlorien"));

        var ended = SutAt(WarPhase.IsengardWar).EndWarsForbiddenByPhase();

        Assert.AreEqual(1, ended);
        _allianceAdapter.Received(1).MakePeace("rivendell", "lothlorien");
    }

    [TestMethod]
    public void EndWarsForbiddenByPhase_PeaceHasNoEffect_DoesNotCountTheWar()
    {
        // MakePeace is a no-op here (a veto on the peace side), so the pair stays at war.
        _allianceAdapter.AreAtWar("isengard", "rohan").Returns(true);

        var ended = SutAt(WarPhase.Peace).EndWarsForbiddenByPhase();

        Assert.AreEqual(0, ended);
        _logger.DidNotReceive().LogInfo(Arg.Is<string>(m => m.Contains("ended")));
    }
}
