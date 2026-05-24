using BehaviorTreeWrapper;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.MountAndBlade;

namespace TAOM.Tests.BehaviorTreeWrapper;

// Regression test for docs/reviews/rca-looter-battle-nre-2026-05-24.md.
// The original vendored BehaviorTreeWrapper.dll declared:
//   public class BehaviorTreeMissionLogic : MissionBehavior
//   { public override MissionBehaviorType BehaviorType => MissionBehaviorType.Logic; }
//
// That combination made vanilla Mission.AddMissionBehavior do
// `MissionLogics.Add(this as MissionLogic)` which evaluated to null and NRE'd
// every tick inside Mission.CheckMissionEnded.
//
// These assertions hard-fail if anyone reintroduces that combination during
// future BehaviorTreeWrapper edits or DLL re-vendoring.
[TestClass]
public class BehaviorTreeMissionLogicInheritanceTests
{
    [TestMethod]
    public void BehaviorTreeMissionLogic_InheritsMissionLogic_NotJustMissionBehavior()
    {
        // Arrange / Act
        var type = typeof(BehaviorTreeMissionLogic);

        // Assert
        Assert.IsTrue(
            typeof(MissionLogic).IsAssignableFrom(type),
            "BehaviorTreeMissionLogic must derive from MissionLogic. " +
            "Inheriting MissionBehavior alone while reporting BehaviorType=Logic " +
            "causes vanilla AddMissionBehavior to null-cast a non-MissionLogic into " +
            "_missionLogics, then NRE every tick in CheckMissionEnded.");
    }

    // Note: an instance-based `Assert.AreEqual(BehaviorType.Logic, logic.BehaviorType)` test
    // would need BehaviorTrees.dll in the test output folder (the BehaviorTreeMissionLogic
    // ctor touches BehaviorTree). The reflection-only IsAssignableFrom assertion above
    // catches the regression without that load.
}
