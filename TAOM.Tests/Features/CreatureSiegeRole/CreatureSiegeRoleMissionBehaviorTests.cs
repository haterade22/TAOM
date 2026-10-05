using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.AdvancedCombat;
using TAOM.Features.CreatureSiegeRole;
using TAOM.Features.CreatureSiegeRole.Domain;
using TAOM.Features.CreatureSiegeRole.Hooks;
using TAOM.Tests.Features.SiegeForces;
using TAOM.Tests.Migration;
using TaleWorlds.MountAndBlade;

namespace TAOM.Tests.Features.CreatureSiegeRole;

/// <summary>
/// The mission boundary of the creature siege role: what it does with no mission (the unit-test shape of <c>AfterStart</c>'s
/// fault path, which in the game would otherwise restart the mission load every frame, #699), and the teardown, which clears a
/// snapshot by token only. A <c>Mission</c> cannot be built outside the game, so the activation itself is covered by the
/// service tests and the in-game checklist; this class pins the lines around it.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class CreatureSiegeRoleMissionBehaviorTests
{
    private static readonly object OtherMission = new();
    private IModLogger _logger = null!;
    private CreatureSiegeRoleMissionBehavior _behavior = null!;

    [ClassInitialize]
    public static void Init(TestContext _) => GameAssemblies.EnsureLoaded();

    [TestInitialize]
    public void Setup()
    {
        CreatureSiegeSnapshot.Clear();
        MissionThreadGuard.ResetForTests();
        _logger = Substitute.For<IModLogger>();
        var config = new CreatureSiegeRoleConfigProvider(Substitute.For<IPathService>(), _logger);
        _behavior = new CreatureSiegeRoleMissionBehavior(new CreatureSiegeRoleSettingsProvider(), config, FakeRaceManager.WithTrolls(), _logger);
    }

    [TestCleanup]
    public void Cleanup()
    {
        CreatureSiegeSnapshot.Clear();
        MissionThreadGuard.ResetForTests();
    }

    private static void PublishAnotherMissionsSnapshot() =>
        CreatureSiegeSnapshot.Publish(new CreatureSiegeSnapshot(OtherMission, new[] { false, true }, 2f, new object[] { new object() }));

    [TestMethod]
    public void TheBehavior_IsAMissionLogic()
    {
        // MissionBehavior.BehaviorType must be Logic only for a MissionLogic: the engine adds `behavior as MissionLogic` to its
        // logic list, and a null in it throws ten seconds into every battle (lessons/gamemodels-services.md).
        Assert.IsTrue(typeof(MissionLogic).IsAssignableFrom(typeof(CreatureSiegeRoleMissionBehavior)));
        Assert.AreEqual(MissionBehaviorType.Logic, _behavior.BehaviorType);
    }

    [TestMethod]
    public void OnCreated_ClearsAStaleSnapshot()
    {
        PublishAnotherMissionsSnapshot();

        _behavior.OnCreated();

        Assert.IsNull(CreatureSiegeSnapshot.Current, "a record a faulted earlier mission never cleared must not reach this one");
    }

    [TestMethod]
    public void AfterStart_WithNoMission_LogsOneError_PublishesNothing_AndDoesNotThrow()
    {
        _behavior.AfterStart();

        Assert.IsNull(CreatureSiegeSnapshot.Current);
        _logger.Received(1).LogError(Arg.Is<string>(s => s.Contains("activation") && s.Contains("ArgumentNullException")));
    }

    [TestMethod]
    public void AfterStart_AFaultLeavesAnotherMissionsSnapshotAlone()
    {
        PublishAnotherMissionsSnapshot();

        _behavior.AfterStart();

        Assert.IsNotNull(CreatureSiegeSnapshot.Current, "the fault path clears by this mission's token, never wholesale");
    }

    [TestMethod]
    public void OnMissionTick_WithNoActiveRole_IsSilent()
    {
        _behavior.OnMissionTick(0.016f);
        _behavior.OnMissionTick(float.NaN);

        Assert.AreEqual(0, _logger.ReceivedCalls().Count());
        Assert.AreEqual(0, MissionThreadGuard.OffThreadCalls);
    }

    [TestMethod]
    public void OnMissionTick_OffTheMainThread_IsReportedOnceAsAWarning()
    {
        MissionThreadGuard.MarkMainThread();

        var thread = new Thread(() =>
        {
            _behavior.OnMissionTick(0.016f);
            _behavior.OnMissionTick(0.016f);
        });
        thread.Start();
        thread.Join();

        Assert.AreEqual(2, MissionThreadGuard.OffThreadCalls, "the tripwire sits at the top of the tick");
        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("CreatureSiegeRoleMissionBehavior.OnMissionTick")));
    }

    [TestMethod]
    public void OnEndMissionInternal_ForABehaviorThatNeverActivated_LeavesAnotherMissionsSnapshot()
    {
        PublishAnotherMissionsSnapshot();

        _behavior.OnEndMissionInternal();

        Assert.IsNotNull(CreatureSiegeSnapshot.Current);
    }

    [TestMethod]
    public void OnRemoveBehavior_ForABehaviorThatNeverActivated_LeavesAnotherMissionsSnapshot()
    {
        PublishAnotherMissionsSnapshot();

        _behavior.OnRemoveBehavior();

        Assert.IsNotNull(CreatureSiegeSnapshot.Current);
    }

    [TestMethod]
    public void TheEndOfAMission_ArrivingTwice_IsHarmless()
    {
        _behavior.OnEndMissionInternal();
        _behavior.OnRemoveBehavior();
        _behavior.OnEndMissionInternal();
        _behavior.OnRemoveBehavior();

        Assert.AreEqual(0, _logger.ReceivedCalls().Count());
    }
}
