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
    private ITaomSettingsProvider _settingsProvider;
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
        _settingsProvider = Substitute.For<ITaomSettingsProvider>();
        _logger = Substitute.For<IModLogger>();

        _settingsProvider.IsAvailable.Returns(false);
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
        _sut = new WarOfTheRingService(_configProvider, _diplomacyService, _allianceAdapter, _settingsProvider, _logger);
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
        _allianceAdapter.HasDeclaredWar("isengard", "vlandia").Returns(true);
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
        _allianceAdapter.HasDeclaredWar("isengard", "vlandia").Returns(false, true);
        _allianceAdapter.HasDeclaredWar("empire", "vlandia").Returns(false, true);
        CreateSut();

        _sut.CheckPhaseTransition(30f);
        _sut.CheckPhaseTransition(31f);

        _allianceAdapter.Received(1).DeclareWar("isengard", "vlandia");
        _allianceAdapter.Received(1).DeclareWar("empire", "vlandia");
    }

    // ---- WotR Momentum #327: EndWar / WarEnded terminal state ----

    [TestMethod]
    public void Outcome_AtStart_IsNone()
    {
        CreateSut();
        Assert.AreEqual(WarOutcome.None, _sut.Outcome);
    }

    [TestMethod]
    public void EndWar_FromFullWar_SetsWarEndedPhaseAndOutcome()
    {
        CreateSut();
        _sut.CheckPhaseTransition(45f);

        _sut.EndWar(WarOutcome.FreeVictory);

        Assert.AreEqual(WarPhase.WarEnded, _sut.CurrentPhase);
        Assert.AreEqual(WarOutcome.FreeVictory, _sut.Outcome);
    }

    [TestMethod]
    public void EndWar_SetsIsWarOfTheRingActiveFalse()
    {
        CreateSut();
        _sut.CheckPhaseTransition(45f);
        Assert.IsTrue(_sut.IsWarOfTheRingActive);

        _sut.EndWar(WarOutcome.EvilVictory);

        Assert.IsFalse(_sut.IsWarOfTheRingActive);
    }

    [TestMethod]
    public void EndWar_LiftsShouldBlockPeaceForHostilePairs()
    {
        _diplomacyService.GetRelationshipTier("empire_w", "empire_s").Returns(AllianceTier.Hostile);
        CreateSut();
        _sut.CheckPhaseTransition(45f);
        Assert.IsTrue(_sut.ShouldBlockPeace("empire_w", "empire_s"));

        _sut.EndWar(WarOutcome.FreeVictory);

        Assert.IsFalse(_sut.ShouldBlockPeace("empire_w", "empire_s"));
    }

    [TestMethod]
    public void EndWar_CalledTwice_KeepsFirstOutcome()
    {
        CreateSut();
        _sut.CheckPhaseTransition(45f);

        _sut.EndWar(WarOutcome.FreeVictory);
        _sut.EndWar(WarOutcome.EvilVictory);

        Assert.AreEqual(WarOutcome.FreeVictory, _sut.Outcome);
        Assert.AreEqual(WarPhase.WarEnded, _sut.CurrentPhase);
    }

    [TestMethod]
    public void EndWar_NoneOutcome_DoesNothing()
    {
        CreateSut();
        _sut.CheckPhaseTransition(45f);

        _sut.EndWar(WarOutcome.None);

        Assert.AreEqual(WarPhase.FullWar, _sut.CurrentPhase);
        Assert.AreEqual(WarOutcome.None, _sut.Outcome);
    }

    [TestMethod]
    public void CheckPhaseTransition_AfterWarEnded_StaysWarEnded()
    {
        CreateSut();
        _sut.CheckPhaseTransition(45f);
        _sut.EndWar(WarOutcome.FreeVictory);

        _sut.CheckPhaseTransition(1000f);

        Assert.AreEqual(WarPhase.WarEnded, _sut.CurrentPhase);
    }

    [TestMethod]
    public void SetPhaseFromSave_WarEnded_RoundTrips()
    {
        CreateSut();
        _sut.SetPhaseFromSave(WarPhase.WarEnded);

        Assert.AreEqual(WarPhase.WarEnded, _sut.CurrentPhase);
        Assert.IsFalse(_sut.IsWarOfTheRingActive);
    }

    [TestMethod]
    public void SetOutcomeFromSave_RestoresOutcome()
    {
        CreateSut();
        _sut.SetOutcomeFromSave(WarOutcome.EvilVictory);

        Assert.AreEqual(WarOutcome.EvilVictory, _sut.Outcome);
    }

    [TestMethod]
    public void EndWar_AfterLoadedWarEndedPhaseWithNoOutcome_SetsOutcome()
    {
        // Load-reconcile path: an older/interrupted save can carry phase WarEnded
        // with no persisted outcome — Momentum's reconcile calls EndWar(store.Victor).
        CreateSut();
        _sut.SetPhaseFromSave(WarPhase.WarEnded);

        _sut.EndWar(WarOutcome.FreeVictory);

        Assert.AreEqual(WarOutcome.FreeVictory, _sut.Outcome);
        Assert.AreEqual(WarPhase.WarEnded, _sut.CurrentPhase);
    }

    // ---- #764: process-singleton session reset ----

    [TestMethod]
    public void ResetForNewSession_FromFullWar_ReturnsPeaceAndNone()
    {
        CreateSut();
        _sut.CheckPhaseTransition(45f);
        Assert.AreEqual(WarPhase.FullWar, _sut.CurrentPhase);

        _sut.ResetForNewSession();

        Assert.AreEqual(WarPhase.Peace, _sut.CurrentPhase);
        Assert.AreEqual(WarOutcome.None, _sut.Outcome);
    }

    [TestMethod]
    public void ResetForNewSession_AfterEndWar_EndWarFiresAgain()
    {
        CreateSut();
        _sut.CheckPhaseTransition(45f);
        _sut.EndWar(WarOutcome.FreeVictory);

        _sut.ResetForNewSession();
        _sut.EndWar(WarOutcome.EvilVictory);

        Assert.AreEqual(WarOutcome.EvilVictory, _sut.Outcome);
        Assert.AreEqual(WarPhase.WarEnded, _sut.CurrentPhase);
    }

    [TestMethod]
    public void ResetForNewSession_ThenPhase1Day_DeclaresPhase1Wars()
    {
        CreateSut();
        _sut.CheckPhaseTransition(45f);
        _allianceAdapter.ClearReceivedCalls();

        _sut.ResetForNewSession();
        _sut.CheckPhaseTransition(30f);

        Assert.AreEqual(WarPhase.IsengardWar, _sut.CurrentPhase);
        _allianceAdapter.Received(1).DeclareWar("isengard", "vlandia");
        _allianceAdapter.Received(1).DeclareWar("empire", "vlandia");
    }

    // ---- MCM-sourced phase days: ordering invariant ----
    // CheckPhaseTransition's two guards are sequential ifs and TransitionToPhase mutates
    // CurrentPhase in place, so an equal or inverted day pair runs BOTH transitions in one
    // call and IsengardWar is never observable. The MCM sliders accept any pair inside
    // [1,365] with no cross-field validation, so the service must clamp.

    private void UseMcmPhaseDays(int phase1Day, int phase2Day)
    {
        _settingsProvider.IsAvailable.Returns(true);
        _settingsProvider.WarOfTheRingEnabled.Returns(true);
        _settingsProvider.TestMode.Returns(false);
        _settingsProvider.Phase1TriggerDay.Returns(phase1Day);
        _settingsProvider.Phase2TriggerDay.Returns(phase2Day);
    }

    [TestMethod]
    public void CheckPhaseTransition_McmNormalPhaseDays_UsesThemUnchanged()
    {
        UseMcmPhaseDays(30, 44);
        CreateSut();

        _sut.CheckPhaseTransition(29f);
        Assert.AreEqual(WarPhase.Peace, _sut.CurrentPhase);

        _sut.CheckPhaseTransition(30f);
        Assert.AreEqual(WarPhase.IsengardWar, _sut.CurrentPhase);

        _sut.CheckPhaseTransition(43f);
        Assert.AreEqual(WarPhase.IsengardWar, _sut.CurrentPhase);

        _sut.CheckPhaseTransition(44f);
        Assert.AreEqual(WarPhase.FullWar, _sut.CurrentPhase);
    }

    [TestMethod]
    public void CheckPhaseTransition_McmEqualPhaseDays_DoesNotSkipIsengardWar()
    {
        UseMcmPhaseDays(30, 30);
        CreateSut();

        _sut.CheckPhaseTransition(30f);
        Assert.AreEqual(WarPhase.IsengardWar, _sut.CurrentPhase);

        _sut.CheckPhaseTransition(31f);
        Assert.AreEqual(WarPhase.FullWar, _sut.CurrentPhase);
    }

    [TestMethod]
    public void CheckPhaseTransition_McmPhase1AfterPhase2_DoesNotSkipIsengardWar()
    {
        UseMcmPhaseDays(100, 44);
        CreateSut();

        _sut.CheckPhaseTransition(99f);
        Assert.AreEqual(WarPhase.Peace, _sut.CurrentPhase);

        _sut.CheckPhaseTransition(100f);
        Assert.AreEqual(WarPhase.IsengardWar, _sut.CurrentPhase);

        _sut.CheckPhaseTransition(101f);
        Assert.AreEqual(WarPhase.FullWar, _sut.CurrentPhase);
    }

    [TestMethod]
    public void CheckPhaseTransition_McmPhase1BelowOne_ClampsToDayOne()
    {
        UseMcmPhaseDays(0, 44);
        CreateSut();

        _sut.CheckPhaseTransition(0f);
        Assert.AreEqual(WarPhase.Peace, _sut.CurrentPhase);

        _sut.CheckPhaseTransition(1f);
        Assert.AreEqual(WarPhase.IsengardWar, _sut.CurrentPhase);
    }

    [TestMethod]
    public void CheckPhaseTransition_JsonEqualPhaseDays_DoesNotSkipIsengardWar()
    {
        var config = CreateDefaultConfig();
        config.Phase1.TriggerDay = 30;
        config.Phase2.TriggerDay = 30;
        _configProvider.LoadConfig().Returns(config);
        CreateSut();

        _sut.CheckPhaseTransition(30f);
        Assert.AreEqual(WarPhase.IsengardWar, _sut.CurrentPhase);

        _sut.CheckPhaseTransition(31f);
        Assert.AreEqual(WarPhase.FullWar, _sut.CurrentPhase);
    }

    [TestMethod]
    public void CheckPhaseTransition_TestModeEqualDays_DoesNotSkipIsengardWar()
    {
        var config = CreateDefaultConfig();
        config.TestMode.Enabled = true;
        config.TestMode.Phase1Day = 3;
        config.TestMode.Phase2Day = 3;
        _configProvider.LoadConfig().Returns(config);
        CreateSut();

        _sut.CheckPhaseTransition(3f);
        Assert.AreEqual(WarPhase.IsengardWar, _sut.CurrentPhase);

        _sut.CheckPhaseTransition(4f);
        Assert.AreEqual(WarPhase.FullWar, _sut.CurrentPhase);
    }

    // ---- #772: Phase 2 never declared the Hostile-pair wars ----
    // The real AreAtWar answers through the diplomacy model: once the phase is FullWar, every Hostile
    // pair reads "at war" before any stance exists. The guards must read the real stance instead
    // (HasDeclaredWar), and the phase turns first so vanilla's OnWarDeclared listeners already treat
    // every Hostile pair as constantly at war. The HasDeclaredWar fakes stand for an existing neutral
    // link; a pair with no link at all would also read true in the game (see IAllianceAdapter).

    private void UseHostilePairs(params (string a, string b)[] hostile)
    {
        _diplomacyService.GetRelationshipTier(Arg.Any<string>(), Arg.Any<string>()).Returns(AllianceTier.Neutral);
        foreach (var (a, b) in hostile)
            _diplomacyService.GetRelationshipTier(a, b).Returns(AllianceTier.Hostile);
        _allianceAdapter.GetAllKingdomIds().Returns(new List<string> { "empire_w", "empire_s", "aserai", "gundabad", "erebor" });
        MakeAreAtWarAnswerLikeTheGame();
    }

    private void MakeAreAtWarAnswerLikeTheGame()
    {
        _allianceAdapter.AreAtWar(Arg.Any<string>(), Arg.Any<string>()).Returns(ci =>
            _sut.CurrentPhase == WarPhase.FullWar
            && _diplomacyService.GetRelationshipTier(ci.ArgAt<string>(0), ci.ArgAt<string>(1)) == AllianceTier.Hostile);
    }

    [TestMethod]
    public void CheckPhaseTransition_Phase2WhenModelReportsHostilePairsAtWar_StillDeclaresThem()
    {
        UseHostilePairs(("empire_w", "empire_s"));
        CreateSut();

        _sut.CheckPhaseTransition(45f);

        _allianceAdapter.Received(1).DeclareWar("empire_w", "empire_s");
    }

    [TestMethod]
    public void CheckPhaseTransition_Phase2_DeclaresAfterThePhaseTurnsToFullWar()
    {
        UseHostilePairs(("empire_w", "empire_s"));
        CreateSut();
        var phaseAtDeclaration = WarPhase.WarEnded;
        _allianceAdapter.When(a => a.DeclareWar("empire_w", "empire_s")).Do(_ => phaseAtDeclaration = _sut.CurrentPhase);

        _sut.CheckPhaseTransition(45f);

        Assert.AreEqual(WarPhase.FullWar, phaseAtDeclaration);
        Assert.AreEqual(WarPhase.FullWar, _sut.CurrentPhase);
    }

    [TestMethod]
    public void CheckPhaseTransition_Phase2BlockPeaceOff_StillDeclaresHostileAndListedWars()
    {
        var config = CreateDefaultConfig();
        config.Phase2.BlockPeaceBetweenHostileTiers = false;
        config.Phase2.Wars = new List<WarDeclaration> { new WarDeclaration { Attacker = "gundabad", Defender = "erebor" } };
        _configProvider.LoadConfig().Returns(config);
        UseHostilePairs(("empire_w", "empire_s"));
        CreateSut();

        _sut.CheckPhaseTransition(45f);

        _allianceAdapter.Received(1).DeclareWar("empire_w", "empire_s");
        _allianceAdapter.Received(1).DeclareWar("gundabad", "erebor");
    }

    [TestMethod]
    public void CheckPhaseTransition_Phase2_SkipsPairAlreadyDeclared()
    {
        UseHostilePairs(("empire_w", "empire_s"));
        _allianceAdapter.HasDeclaredWar("empire_w", "empire_s").Returns(true);
        CreateSut();

        _sut.CheckPhaseTransition(45f);

        _allianceAdapter.DidNotReceive().DeclareWar("empire_w", "empire_s");
    }

    // ---- #772: ReconcileDeclaredWars repairs saves already at FullWar ----

    private void CreateSutAtFullWar()
    {
        CreateSut();
        _sut.SetPhaseFromSave(WarPhase.FullWar);
    }

    [TestMethod]
    public void ReconcileDeclaredWars_FullWarUndeclaredHostilePair_DeclaresIt()
    {
        UseHostilePairs(("empire_w", "empire_s"));
        CreateSutAtFullWar();

        _sut.ReconcileDeclaredWars();

        _allianceAdapter.Received(1).DeclareWar("empire_w", "empire_s");
    }

    [TestMethod]
    public void ReconcileDeclaredWars_FullWarAlreadyDeclaredPair_SkipsIt()
    {
        UseHostilePairs(("empire_w", "empire_s"));
        _allianceAdapter.HasDeclaredWar("empire_w", "empire_s").Returns(true);
        CreateSutAtFullWar();

        _sut.ReconcileDeclaredWars();

        _allianceAdapter.DidNotReceive().DeclareWar(Arg.Any<string>(), Arg.Any<string>());
    }

    [TestMethod]
    public void ReconcileDeclaredWars_FullWarNonHostilePair_SkipsIt()
    {
        UseHostilePairs(("empire_w", "empire_s"));
        CreateSutAtFullWar();

        _sut.ReconcileDeclaredWars();

        _allianceAdapter.DidNotReceive().DeclareWar("empire_w", "aserai");
        _allianceAdapter.DidNotReceive().DeclareWar("empire_s", "aserai");
    }

    [TestMethod]
    public void ReconcileDeclaredWars_FullWarAutoWarOff_DeclaresOnlyConfiguredPhase2Wars()
    {
        var config = CreateDefaultConfig();
        config.Phase2.AutoWarBetweenHostileTiers = false;
        config.Phase2.Wars = new List<WarDeclaration> { new WarDeclaration { Attacker = "gundabad", Defender = "erebor" } };
        _configProvider.LoadConfig().Returns(config);
        UseHostilePairs(("empire_w", "empire_s"), ("gundabad", "erebor"));
        CreateSutAtFullWar();

        _sut.ReconcileDeclaredWars();

        _allianceAdapter.Received(1).DeclareWar("gundabad", "erebor");
        _allianceAdapter.DidNotReceive().DeclareWar("empire_w", "empire_s");
    }

    [TestMethod]
    public void ReconcileDeclaredWars_BlockPeaceOff_HostilePairAtPeace_DoesNotDeclare()
    {
        var config = CreateDefaultConfig();
        config.Phase2.BlockPeaceBetweenHostileTiers = false;
        _configProvider.LoadConfig().Returns(config);
        UseHostilePairs(("empire_w", "empire_s"));
        CreateSutAtFullWar();

        _sut.ReconcileDeclaredWars();

        _allianceAdapter.DidNotReceive().DeclareWar(Arg.Any<string>(), Arg.Any<string>());
    }

    [TestMethod]
    public void ReconcileDeclaredWars_NonHostilePhase2WarAtPeace_DoesNotDeclare()
    {
        var config = CreateDefaultConfig();
        config.Phase2.AutoWarBetweenHostileTiers = false;
        config.Phase2.Wars = new List<WarDeclaration> { new WarDeclaration { Attacker = "gundabad", Defender = "erebor" } };
        _configProvider.LoadConfig().Returns(config);
        UseHostilePairs(("empire_w", "empire_s"));
        CreateSutAtFullWar();

        _sut.ReconcileDeclaredWars();

        _allianceAdapter.DidNotReceive().DeclareWar(Arg.Any<string>(), Arg.Any<string>());
    }

    [TestMethod]
    public void ReconcileDeclaredWars_Phase2WarNamesEliminatedKingdom_DoesNotDeclareOrLog()
    {
        var config = CreateDefaultConfig();
        config.Phase2.AutoWarBetweenHostileTiers = false;
        config.Phase2.Wars = new List<WarDeclaration> { new WarDeclaration { Attacker = "gundabad", Defender = "erebor" } };
        _configProvider.LoadConfig().Returns(config);
        UseHostilePairs(("gundabad", "erebor"));
        _allianceAdapter.GetAllKingdomIds().Returns(new List<string> { "gundabad", "aserai" });
        CreateSutAtFullWar();

        _sut.ReconcileDeclaredWars();

        _allianceAdapter.DidNotReceive().DeclareWar(Arg.Any<string>(), Arg.Any<string>());
        _logger.DidNotReceive().LogInfo(Arg.Is<string>(m => m.Contains("#772")));
    }

    [TestMethod]
    public void ReconcileDeclaredWars_Phase2WarNamesUnknownKingdom_DoesNotDeclareOrLog()
    {
        var config = CreateDefaultConfig();
        config.Phase2.AutoWarBetweenHostileTiers = false;
        config.Phase2.Wars = new List<WarDeclaration> { new WarDeclaration { Attacker = "gundabad", Defender = "nowhere" } };
        _configProvider.LoadConfig().Returns(config);
        UseHostilePairs(("gundabad", "nowhere"));
        CreateSutAtFullWar();

        _sut.ReconcileDeclaredWars();

        _allianceAdapter.DidNotReceive().DeclareWar(Arg.Any<string>(), Arg.Any<string>());
        _logger.DidNotReceive().LogInfo(Arg.Is<string>(m => m.Contains("#772")));
    }

    // #772 Codex R3: a misspelt kingdom id ("rohan" for vlandia) passes the provider, which cannot
    // see the live kingdoms, so the service names it, once per process rather than on every load.
    [TestMethod]
    public void ReconcileDeclaredWars_Phase2WarNamesUnknownKingdom_WarnsOncePerProcess()
    {
        var config = CreateDefaultConfig();
        config.Phase2.AutoWarBetweenHostileTiers = false;
        config.Phase2.Wars = new List<WarDeclaration> { new WarDeclaration { Attacker = "gundabad", Defender = "rohan" } };
        _configProvider.LoadConfig().Returns(config);
        UseHostilePairs(("gundabad", "rohan"));
        CreateSutAtFullWar();

        _sut.ReconcileDeclaredWars();
        _sut.ReconcileDeclaredWars();

        _logger.Received(1).LogWarning(Arg.Is<string>(m =>
            m.Contains("gundabad -> rohan") && m.Contains("war_of_the_ring.json")));
    }

    [TestMethod]
    public void CheckPhaseTransition_Phase1WarNamesUnknownKingdom_WarnsAndStillDeclaresTheOthers()
    {
        var config = CreateDefaultConfig();
        config.Phase1.Wars.Add(new WarDeclaration { Attacker = "isengard", Defender = "rohan" });
        _configProvider.LoadConfig().Returns(config);
        _allianceAdapter.GetAllKingdomIds().Returns(new List<string> { "isengard", "empire", "vlandia" });
        CreateSut();

        _sut.CheckPhaseTransition(30f);

        _allianceAdapter.Received(1).DeclareWar("isengard", "vlandia");
        _allianceAdapter.Received(1).DeclareWar("empire", "vlandia");
        _allianceAdapter.DidNotReceive().DeclareWar("isengard", "rohan");
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("isengard -> rohan")));
    }

    [TestMethod]
    public void ReconcileDeclaredWars_DeclarationHasNoEffect_LogsNoCountAndWarns()
    {
        UseHostilePairs(("empire_w", "empire_s"));
        _allianceAdapter.HasDeclaredWar("empire_w", "empire_s").Returns(false);
        CreateSutAtFullWar();

        _sut.ReconcileDeclaredWars();

        _logger.DidNotReceive().LogInfo(Arg.Is<string>(m => m.Contains("#772")));
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("empire_w") && m.Contains("empire_s")));
    }

    [TestMethod]
    public void ReconcileDeclaredWars_FullWarMcmToggleOff_DoesNothing()
    {
        _settingsProvider.IsAvailable.Returns(true);
        _settingsProvider.WarOfTheRingEnabled.Returns(false);
        UseHostilePairs(("empire_w", "empire_s"));
        CreateSutAtFullWar();

        _sut.ReconcileDeclaredWars();

        _allianceAdapter.DidNotReceive().DeclareWar(Arg.Any<string>(), Arg.Any<string>());
    }

    [TestMethod]
    public void ReconcileDeclaredWars_FullWarMcmToggleOnJsonOff_DeclaresMissingWars()
    {
        var config = CreateDefaultConfig();
        config.Enabled = false;
        _configProvider.LoadConfig().Returns(config);
        _settingsProvider.IsAvailable.Returns(true);
        _settingsProvider.WarOfTheRingEnabled.Returns(true);
        UseHostilePairs(("empire_w", "empire_s"));
        CreateSutAtFullWar();

        _sut.ReconcileDeclaredWars();

        _allianceAdapter.Received(1).DeclareWar("empire_w", "empire_s");
    }

    [TestMethod]
    public void ReconcileDeclaredWars_FullWarDisabled_DoesNothing()
    {
        var config = CreateDefaultConfig();
        config.Enabled = false;
        _configProvider.LoadConfig().Returns(config);
        UseHostilePairs(("empire_w", "empire_s"));
        CreateSutAtFullWar();

        _sut.ReconcileDeclaredWars();

        _allianceAdapter.DidNotReceive().DeclareWar(Arg.Any<string>(), Arg.Any<string>());
    }

    [TestMethod]
    public void ReconcileDeclaredWars_Peace_DoesNothing()
    {
        UseHostilePairs(("empire_w", "empire_s"));
        CreateSut();

        _sut.ReconcileDeclaredWars();

        _allianceAdapter.DidNotReceive().DeclareWar(Arg.Any<string>(), Arg.Any<string>());
    }

    [TestMethod]
    public void ReconcileDeclaredWars_IsengardWar_DoesNothing()
    {
        UseHostilePairs(("empire_w", "empire_s"));
        CreateSut();
        _sut.SetPhaseFromSave(WarPhase.IsengardWar);

        _sut.ReconcileDeclaredWars();

        _allianceAdapter.DidNotReceive().DeclareWar(Arg.Any<string>(), Arg.Any<string>());
    }

    [TestMethod]
    public void ReconcileDeclaredWars_WarEnded_DoesNothing()
    {
        UseHostilePairs(("empire_w", "empire_s"));
        CreateSut();
        _sut.SetPhaseFromSave(WarPhase.WarEnded);

        _sut.ReconcileDeclaredWars();

        _allianceAdapter.DidNotReceive().DeclareWar(Arg.Any<string>(), Arg.Any<string>());
    }

    [TestMethod]
    public void ReconcileDeclaredWars_CalledTwice_DeclaresOnce()
    {
        UseHostilePairs(("empire_w", "empire_s"));
        var declared = false;
        _allianceAdapter.HasDeclaredWar("empire_w", "empire_s").Returns(_ => declared);
        _allianceAdapter.When(a => a.DeclareWar("empire_w", "empire_s")).Do(_ => declared = true);
        CreateSutAtFullWar();

        _sut.ReconcileDeclaredWars();
        _sut.ReconcileDeclaredWars();

        _allianceAdapter.Received(1).DeclareWar("empire_w", "empire_s");
    }

    [TestMethod]
    public void ReconcileDeclaredWars_DeclaredSome_LogsTheCount()
    {
        UseHostilePairs(("empire_w", "empire_s"));
        _allianceAdapter.HasDeclaredWar("empire_w", "empire_s").Returns(false, true);
        CreateSutAtFullWar();

        _sut.ReconcileDeclaredWars();

        _logger.Received().LogInfo(Arg.Is<string>(m => m.Contains("declared 1 missing Full War wars (#772)")));
    }

    [TestMethod]
    public void ReconcileDeclaredWars_NothingMissing_LogsNoCount()
    {
        UseHostilePairs(("empire_w", "empire_s"));
        _allianceAdapter.HasDeclaredWar("empire_w", "empire_s").Returns(true);
        CreateSutAtFullWar();

        _sut.ReconcileDeclaredWars();

        _logger.DidNotReceive().LogInfo(Arg.Is<string>(m => m.Contains("#772")));
    }
}
