using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.MapEvents;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.MapEventGuard;
using TAOM.Features.MapEventGuard.Hooks;
using TAOM.Tests.Infrastructure;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.MapEventGuard;

/// <summary>Patch170: the repair rule, its order, and the prefix around it.</summary>
[TestClass]
public class HalfFinishedEncounterServiceTests
{
    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(Microsoft.VisualStudio.TestTools.UnitTesting.TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    private IHalfFinishedEncounterAdapter _encounter = null!;
    private ICoopSessionProvider _coop = null!;
    private IModLogger _logger = null!;
    private HalfFinishedEncounterService _service = null!;

    [TestInitialize]
    public void Setup()
    {
        _encounter = Substitute.For<IHalfFinishedEncounterAdapter>();
        _coop = Substitute.For<ICoopSessionProvider>();
        _logger = Substitute.For<IModLogger>();
        _service = new HalfFinishedEncounterService(_encounter, _coop, _logger);
    }

    [TestCleanup]
    public void Cleanup() => Patch170_HalfFinishedEncounterRepair.Initialize(null, null);

    private static HalfFinishedEncounterSnapshot Snap(
        bool hasBattle = true, bool finalized = true, bool missing = false, int defenders = 0, int attackers = 0) =>
        new(hasBattle, finalized, missing, defenders, attackers);

    [TestMethod]
    public void Decide_HealthyLiveBattle_None()
    {
        Assert.AreEqual(HalfFinishedEncounterService.Action.None,
            HalfFinishedEncounterService.Decide(Snap(finalized: false, defenders: 2, attackers: 3)));
    }

    [TestMethod]
    public void Decide_NoBattle_None()
    {
        Assert.AreEqual(HalfFinishedEncounterService.Action.None,
            HalfFinishedEncounterService.Decide(Snap(hasBattle: false, finalized: false)));
    }

    [TestMethod]
    public void Decide_CleanlyFinalized_None()
    {
        Assert.AreEqual(HalfFinishedEncounterService.Action.None, HalfFinishedEncounterService.Decide(Snap()));
    }

    [TestMethod]
    public void Decide_FinalizedWithNullBackReference_RestoresIt()
    {
        Assert.AreEqual(HalfFinishedEncounterService.Action.RestoreBackReferenceOnly,
            HalfFinishedEncounterService.Decide(Snap(missing: true)));
    }

    [TestMethod]
    public void Decide_FinalizedWithAttachedParties_CompletesFinalize()
    {
        Assert.AreEqual(HalfFinishedEncounterService.Action.CompleteFinalize,
            HalfFinishedEncounterService.Decide(Snap(attackers: 1)));
        Assert.AreEqual(HalfFinishedEncounterService.Action.CompleteFinalize,
            HalfFinishedEncounterService.Decide(Snap(missing: true, defenders: 1)));
    }

    [TestMethod]
    public void RepairIfNeeded_Healthy_TouchesNothing()
    {
        _encounter.Read().Returns(Snap(finalized: false, defenders: 1, attackers: 1));

        _service.RepairIfNeeded();

        _encounter.DidNotReceive().RestoreBackReference();
        _encounter.DidNotReceive().EndSide(Arg.Any<int>());
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void RepairIfNeeded_NullBackReferenceAndParties_RestoresThenEndsDefenderThenAttacker()
    {
        _encounter.Read().Returns(Snap(missing: true, defenders: 1, attackers: 3));
        _encounter.EndSide(0).Returns(1);
        _encounter.EndSide(1).Returns(3);
        _encounter.CountMarkedAliveHeroes().Returns(2);

        _service.RepairIfNeeded();

        Received.InOrder(() =>
        {
            _encounter.RestoreBackReference();
            _encounter.EndSide(0);
            _encounter.EndSide(1);
        });
        _logger.Received(1).LogWarning(Arg.Is<string>(m =>
            m.Contains("[MapEventGuard]") && m.Contains("attackerParties=3") && m.Contains("heroesAliveMarkedDiedInBattle=2")));
    }

    [TestMethod]
    public void RepairIfNeeded_PartiesWithBackReference_DoesNotRestoreIt()
    {
        _encounter.Read().Returns(Snap(attackers: 2));

        _service.RepairIfNeeded();

        _encounter.DidNotReceive().RestoreBackReference();
        _encounter.Received(1).EndSide(0);
        _encounter.Received(1).EndSide(1);
    }

    [TestMethod]
    public void RepairIfNeeded_BackReferenceOnly_DoesNotEndSides()
    {
        _encounter.Read().Returns(Snap(missing: true));

        _service.RepairIfNeeded();

        _encounter.Received(1).RestoreBackReference();
        _encounter.DidNotReceive().EndSide(Arg.Any<int>());
    }

    [TestMethod]
    public void RepairIfNeeded_SecondCall_DoesNothing()
    {
        _encounter.Read().Returns(Snap(missing: true, attackers: 2), Snap());

        _service.RepairIfNeeded();
        _service.RepairIfNeeded();

        _encounter.Received(1).RestoreBackReference();
        _encounter.Received(2).EndSide(Arg.Any<int>());
        _logger.Received(1).LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void RepairIfNeeded_WhenHostDeferred_StandsDown()
    {
        _coop.ShouldDeferToHost.Returns(true);
        _encounter.Read().Returns(Snap(missing: true, attackers: 2));

        _service.RepairIfNeeded();

        _encounter.DidNotReceive().Read();
        _encounter.DidNotReceive().EndSide(Arg.Any<int>());
    }

    [TestMethod]
    public void Prefix_NotInitialized_RunsVanilla()
    {
        Patch170_HalfFinishedEncounterRepair.Initialize(null, null);

        Assert.IsTrue(Patch170_HalfFinishedEncounterRepair.Prefix());
    }

    [TestMethod]
    public void Prefix_RepairRuns_ThenRunsVanilla()
    {
        _encounter.Read().Returns(Snap(attackers: 1));
        Patch170_HalfFinishedEncounterRepair.Initialize(_service, _logger);

        Assert.IsTrue(Patch170_HalfFinishedEncounterRepair.Prefix());

        _encounter.Received(1).EndSide(1);
    }

    [TestMethod]
    public void Prefix_RepairThrows_LogsOnceAndRunsVanilla()
    {
        _encounter.Read().Throws(new InvalidOperationException("boom"));
        Patch170_HalfFinishedEncounterRepair.Initialize(_service, _logger);

        Assert.IsTrue(Patch170_HalfFinishedEncounterRepair.Prefix());
        Assert.IsTrue(Patch170_HalfFinishedEncounterRepair.Prefix());

        _logger.Received(1).LogError(Arg.Is<string>(m => m.Contains("[MapEventGuard]") && m.Contains("boom")));
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void EngineMembers_ResolveWithTheShapeTheRepairUses()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));

        var finalize = AccessTools.Method(typeof(PlayerEncounter), "FinalizeBattle");
        Assert.IsNotNull(finalize, "PlayerEncounter.FinalizeBattle is gone.");
        Assert.IsTrue(finalize.IsPublic && !finalize.IsStatic && finalize.GetParameters().Length == 0, "FinalizeBattle changed shape.");

        var afterLoad = AccessTools.Method(typeof(MapEventComponent), "AfterLoad", new[] { typeof(MapEvent) });
        Assert.IsNotNull(afterLoad, "MapEventComponent.AfterLoad(MapEvent) is gone: re-derive the repair from MapEvent.OnAfterLoad.");
        Assert.IsTrue(afterLoad.IsAssembly && !afterLoad.IsStatic, "AfterLoad changed visibility (was internal instance).");

        var end = typeof(MapEventSide).GetMethod("HandleMapEventEnd", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
        Assert.IsNotNull(end, "MapEventSide.HandleMapEventEnd() stopped being public.");
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void Prefix_PatchesTheRealTargetThroughHarmony()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));

        const string id = "taom.tests.patch170";
        var harmony = new Harmony(id);
        try
        {
            harmony.CreateClassProcessor(typeof(Patch170_HalfFinishedEncounterRepair)).Patch();
            var target = AccessTools.Method(typeof(PlayerEncounter), "FinalizeBattle");
            Assert.IsNotNull(Harmony.GetPatchInfo(target)?.Prefixes.FirstOrDefault(p => p.owner == id), "Harmony did not patch FinalizeBattle.");
        }
        finally
        {
            harmony.UnpatchAll(id);
        }
    }
}
