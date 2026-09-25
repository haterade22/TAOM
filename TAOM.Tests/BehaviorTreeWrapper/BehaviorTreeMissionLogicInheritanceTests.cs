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

    [TestMethod]
    public void CreatureMissionBehaviors_InheritMissionLogic()
    {
        // The creature features' own mission behaviors carry the same rule in their doc comments ("MUST be
        // : MissionLogic, NEVER : MissionBehavior ... pinned by BehaviorTreeMissionLogicInheritanceTests"); this
        // is that pin. Each attaches creature trees from OnMissionTick, so a MissionBehavior-only base would be
        // the looter-battle NRE again. Add a new creature's behavior here when it is written (#636 added the elk).
        var creatureBehaviors = new[]
        {
            typeof(TAOM.Features.Warg.WargMissionBehavior),
            typeof(TAOM.Features.Spider.SpiderMissionBehavior),
            typeof(TAOM.Features.Elephant.ElephantMissionBehavior),
            typeof(TAOM.Features.Mumakil.MumakilMissionBehavior),
            typeof(TAOM.Features.WarRam.WarRamMissionBehavior),
            typeof(TAOM.Features.Elk.ElkMissionBehavior),
            typeof(TAOM.Features.Animalia.AnimaliaMissionBehavior),
            typeof(TAOM.Features.TrollBruteForce.TrollBruteForceMissionBehavior),
        };

        foreach (var type in creatureBehaviors)
            Assert.IsTrue(typeof(MissionLogic).IsAssignableFrom(type), $"{type.Name} must derive from MissionLogic");
    }

    // Note: an instance-based `Assert.AreEqual(BehaviorType.Logic, logic.BehaviorType)` test
    // would need BehaviorTrees.dll in the test output folder (the BehaviorTreeMissionLogic
    // ctor touches BehaviorTree). The reflection-only IsAssignableFrom assertion above
    // catches the regression without that load.
}
