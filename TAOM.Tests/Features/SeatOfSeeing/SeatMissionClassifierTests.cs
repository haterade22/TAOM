using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Client.Features.FordsOfWar;
using TAOM.Client.Features.SeatOfSeeing;
using TAOM.Features.AdvancedCombat;
using TAOM.Features.CompanionTactics.BattleActionBar.Hooks;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.SeatOfSeeing;

/// <summary>
/// <c>SeatMissionClassifier.OwnsTheCamera</c> (build-spec 5.6): the reflection on one view type. The mission walk around it needs a
/// live mission; this is the part a test can reach.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class SeatMissionClassifierTests
{
    // The view types derive from MissionView, which sits in TaleWorlds.MountAndBlade.View, an assembly the test project does not reference.
    [ClassInitialize]
    public static void Init(Microsoft.VisualStudio.TestTools.UnitTesting.TestContext _)
    {
        if (!GameAssemblies.EnsureLoaded())
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));
    }

    [TestMethod]
    public void OwnsTheCamera_TheFordsCommanderCameraView_IsTrue()
    {
        Assert.IsTrue(SeatMissionClassifier.OwnsTheCamera(typeof(FordsCommanderCameraView)));
    }

    [TestMethod]
    public void OwnsTheCamera_TheSeatsOwnView_IsFalse()
    {
        Assert.IsFalse(SeatMissionClassifier.OwnsTheCamera(typeof(SeatView)));
    }

    [TestMethod]
    public void OwnsTheCamera_AViewThatDoesNotOverrideTheCamera_IsFalse()
    {
        Assert.IsFalse(SeatMissionClassifier.OwnsTheCamera(typeof(BattleActionBarMissionView)));
        Assert.IsFalse(SeatMissionClassifier.OwnsTheCamera(typeof(AutonomousMovementPlayerController)));
    }
}
