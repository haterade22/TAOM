using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.Diplomacy;
using TAOM.Features.Diplomacy.Models;

namespace TAOM.Tests.Features.Diplomacy;

[TestClass]
public class WarOfTheRingServiceTests
{
    private IWarOfTheRingConfigProvider _configProvider;
    private IDiplomacyService _diplomacyService;
    private IAllianceAdapter _allianceAdapter;
    private IModLogger _logger;
    private WarOfTheRingService _sut;

    private WarOfTheRingConfig CreateDefaultConfig()
    {
        return new WarOfTheRingConfig
        {
            Enabled = true,
            Phase1 = new PhaseConfig
            {
                TriggerDay = 30,
                Wars = new List<WarDeclaration>
                {
                    new WarDeclaration { Attacker = "isengard", Defender = "vlandia" },
                    new WarDeclaration { Attacker = "empire", Defender = "vlandia" }
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
    }

    [TestInitialize]
    public void Setup()
    {
        _configProvider = Substitute.For<IWarOfTheRingConfigProvider>();
        _diplomacyService = Substitute.For<IDiplomacyService>();
        _allianceAdapter = Substitute.For<IAllianceAdapter>();
        _logger = Substitute.For<IModLogger>();

        _configProvider.LoadConfig().Returns(CreateDefaultConfig());

        _allianceAdapter.GetAllKingdomIds().Returns(new List<string>
        {
            "empire_w", "vlandia", "empire_s", "isengard", "gundabad",
            "dolguldur", "erebor", "sturgia", "rivendell", "lothlorien",
            "mirkwood", "empire", "aserai", "khuzait", "umbar"
        });
    }

    private void CreateSut()
    {
        _sut = new WarOfTheRingService(_configProvider, _diplomacyService, _allianceAdapter, _logger);
    }

    [TestMethod]
    public void CurrentPhase_AtStart_IsPeace()
    {
        CreateSut();
        Assert.AreEqual(WarPhase.Peace, _sut.CurrentPhase);
    }

    [TestMethod]
    public void IsWarOfTheRingActive_AtStart_IsFalse()
    {
        CreateSut();
        Assert.IsFalse(_sut.IsWarOfTheRingActive);
    }

    [TestMethod]
    public void CheckPhaseTransition_BeforePhase1Day_StaysPeace()
    {
        CreateSut();
        _sut.CheckPhaseTransition(29f);
        Assert.AreEqual(WarPhase.Peace, _sut.CurrentPhase);
    }

    [TestMethod]
    public void CheckPhaseTransition_AtPhase1Day_TransitionsToIsengardWar()
    {
        CreateSut();
        _sut.CheckPhaseTransition(30f);
        Assert.AreEqual(WarPhase.IsengardWar, _sut.CurrentPhase);
    }

    [TestMethod]
    public void CheckPhaseTransition_Phase1_DeclaresConfiguredWars()
    {
        CreateSut();
        _sut.CheckPhaseTransition(30f);

        _allianceAdapter.Received(1).DeclareWar("isengard", "vlandia");
        _allianceAdapter.Received(1).DeclareWar("empire", "vlandia");
    }

    [TestMethod]
    public void CheckPhaseTransition_Phase1_SkipsAlreadyAtWar()
    {
        _allianceAdapter.AreAtWar("isengard", "vlandia").Returns(true);
        CreateSut();
        _sut.CheckPhaseTransition(30f);

        _allianceAdapter.DidNotReceive().DeclareWar("isengard", "vlandia");
        _allianceAdapter.Received(1).DeclareWar("empire", "vlandia");
    }

    [TestMethod]
    public void CheckPhaseTransition_AtPhase2Day_TransitionsToFullWar()
    {
        CreateSut();
        _sut.CheckPhaseTransition(45f);
        Assert.AreEqual(WarPhase.FullWar, _sut.CurrentPhase);
    }

    [TestMethod]
    public void CheckPhaseTransition_Phase2_DeclaresWarBetweenHostilePairs()
    {
        _diplomacyService.GetRelationshipTier("empire_w", "empire_s").Returns(AllianceTier.Hostile);
        _diplomacyService.GetRelationshipTier(Arg.Any<string>(), Arg.Any<string>()).Returns(AllianceTier.Neutral);
        _diplomacyService.GetRelationshipTier("empire_w", "empire_s").Returns(AllianceTier.Hostile);

        _allianceAdapter.GetAllKingdomIds().Returns(new List<string> { "empire_w", "empire_s" });

        CreateSut();
        _sut.CheckPhaseTransition(45f);

        _allianceAdapter.Received(1).DeclareWar("empire_w", "empire_s");
    }

    [TestMethod]
    public void IsWarOfTheRingActive_Phase2_IsTrue()
    {
        CreateSut();
        _sut.CheckPhaseTransition(45f);
        Assert.IsTrue(_sut.IsWarOfTheRingActive);
    }

    [TestMethod]
    public void ShouldBlockPeace_Phase2_HostilePair_ReturnsTrue()
    {
        _diplomacyService.GetRelationshipTier("empire_w", "empire_s").Returns(AllianceTier.Hostile);
        CreateSut();
        _sut.CheckPhaseTransition(45f);

        Assert.IsTrue(_sut.ShouldBlockPeace("empire_w", "empire_s"));
    }

    [TestMethod]
    public void ShouldBlockPeace_Phase2_NeutralPair_ReturnsFalse()
    {
        _diplomacyService.GetRelationshipTier("aserai", "umbar").Returns(AllianceTier.Neutral);
        CreateSut();
        _sut.CheckPhaseTransition(45f);

        Assert.IsFalse(_sut.ShouldBlockPeace("aserai", "umbar"));
    }

    [TestMethod]
    public void ShouldBlockPeace_PeacePhase_ReturnsFalse()
    {
        _diplomacyService.GetRelationshipTier("empire_w", "empire_s").Returns(AllianceTier.Hostile);
        CreateSut();

        Assert.IsFalse(_sut.ShouldBlockPeace("empire_w", "empire_s"));
    }

    [TestMethod]
    public void CheckPhaseTransition_Disabled_DoesNothing()
    {
        var config = CreateDefaultConfig();
        config.Enabled = false;
        _configProvider.LoadConfig().Returns(config);

        CreateSut();
        _sut.CheckPhaseTransition(100f);

        Assert.AreEqual(WarPhase.Peace, _sut.CurrentPhase);
    }

    [TestMethod]
    public void CheckPhaseTransition_TestMode_UsesShortDays()
    {
        var config = CreateDefaultConfig();
        config.TestMode.Enabled = true;
        config.TestMode.Phase1Day = 2;
        config.TestMode.Phase2Day = 5;
        _configProvider.LoadConfig().Returns(config);

        CreateSut();
        _sut.CheckPhaseTransition(2f);
        Assert.AreEqual(WarPhase.IsengardWar, _sut.CurrentPhase);

        _sut.CheckPhaseTransition(5f);
        Assert.AreEqual(WarPhase.FullWar, _sut.CurrentPhase);
    }

    [TestMethod]
    public void CheckPhaseTransition_CalledMultipleTimes_DoesNotRedeclareWar()
    {
        _allianceAdapter.AreAtWar("isengard", "vlandia").Returns(false, true);
        _allianceAdapter.AreAtWar("empire", "vlandia").Returns(false, true);
        CreateSut();

        _sut.CheckPhaseTransition(30f);
        _sut.CheckPhaseTransition(31f);

        _allianceAdapter.Received(1).DeclareWar("isengard", "vlandia");
        _allianceAdapter.Received(1).DeclareWar("empire", "vlandia");
    }
}
