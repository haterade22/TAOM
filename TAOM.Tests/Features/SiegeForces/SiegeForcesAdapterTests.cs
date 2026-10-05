using System;
using System.Collections.Generic;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.MountAndBlade;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.SiegeForces.Domain;
using TAOM.Features.SiegeForces.Hooks;

namespace TAOM.Tests.Features.SiegeForces;

/// <summary>
/// The adapter's contract where there is no campaign, which is every Custom Battle and every Custom Siege: it answers
/// null or false and never throws or logs an error. <c>Campaign.Current</c> is null in this host, exactly as in Custom
/// Battle, where <c>MobileParty.MainParty</c> and <c>MapEvent.PlayerMapEvent</c> throw a NullReferenceException; the
/// totals prefix reaches <see cref="ISiegeForcesAdapter.ReadMapEventToken"/> for every battle that has a pending record,
/// and a stale record can outlive its campaign (the design review's required fix R1). What the adapter reads from a live campaign
/// needs a live campaign: those members are pinned by SiegeForcesBindingTests and checked in game.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class SiegeForcesAdapterTests
{
    private IModLogger _logger = null!;
    private SiegeForcesAdapter _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _logger = Substitute.For<IModLogger>();
        _sut = new SiegeForcesAdapter(_logger);
    }

    [TestMethod]
    public void ReadMapEventToken_WithoutACampaign_IsNull_AndReportsNoError()
    {
        Assert.IsNull(_sut.ReadMapEventToken());
        _logger.DidNotReceiveWithAnyArgs().LogError(default!);
        _logger.DidNotReceiveWithAnyArgs().LogWarning(default!);
    }

    [TestMethod]
    public void IsSpawnTotalsFitAttached_NothingPatched_IsFalse_AndReportsNoError()
    {
        Assert.IsFalse(_sut.IsSpawnTotalsFitAttached());
        _logger.DidNotReceiveWithAnyArgs().LogError(default!);
    }

    [TestMethod]
    public void IsSpawnTotalsFitAttached_TheFitPrefixIsApplied_IsTrue()
    {
        var harmony = new Harmony("taom.tests.siegeforces.fit");
        try
        {
            harmony.CreateClassProcessor(typeof(Patch102_SpawnTotalsFit)).Patch();

            Assert.IsTrue(_sut.IsSpawnTotalsFitAttached());
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
        }
    }

    [TestMethod]
    public void IsSpawnTotalsFitAttached_OnlyAForeignPrefixIsOnTheTarget_IsFalse()
    {
        var harmony = new Harmony("taom.tests.siegeforces.foreign");
        var target = AccessTools.Method(typeof(DefaultBattleMissionAgentSpawnLogic), nameof(DefaultBattleMissionAgentSpawnLogic.InitWithSinglePhase));
        try
        {
            harmony.Patch(target, prefix: new HarmonyMethod(typeof(SiegeForcesAdapterTests), nameof(ForeignPrefix)));

            Assert.IsFalse(_sut.IsSpawnTotalsFitAttached(), "another mod's prefix on the same target is not TAOM's fit");
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
        }
    }

    private static void ForeignPrefix()
    {
    }

    [TestMethod]
    public void CaptureWallBattle_WithoutACampaign_IsNull_AndReportsNoError()
    {
        Assert.IsNull(_sut.CaptureWallBattle());
        _logger.DidNotReceiveWithAnyArgs().LogError(default!);
        _logger.DidNotReceiveWithAnyArgs().LogWarning(default!);
    }

    [TestMethod]
    public void TryOpenPicker_WithoutACampaign_OpensNothing_AndNeverCallsBack()
    {
        var request = new PickerRequest(new[] { new PickerRow("a troop", "uruk_hai", 5, 0, 5) }, 5, 1);
        var calledBack = false;

        var opened = _sut.TryOpenPicker(request, _ => calledBack = true);

        Assert.IsFalse(opened);
        Assert.IsFalse(calledBack);
        _logger.DidNotReceiveWithAnyArgs().LogError(default!);
    }
}
