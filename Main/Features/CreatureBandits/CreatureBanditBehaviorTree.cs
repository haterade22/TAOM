using System;
using BehaviorTrees;
using BehaviorTreeWrapper.BlackBoardClasses;
using BehaviorTreeWrapper.Tasks;
using TAOM.Features.CreatureBandits.BehaviorTreeElements;
using TAOM.Features.Spider;
using TAOM.Features.Spider.BehaviorTreeElements;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.CreatureBandits;

/// <summary>
/// The riderless creature bandit's own behaviour tree (#692), apart from the ridden spider's
/// (<see cref="SpiderBehaviorTree"/>), attached by <see cref="CreatureBanditMissionBehavior"/>. It reuses the spider's
/// engage gate and its two attacks, the priority pounce (the front strike, or the charge variant at speed) and the
/// left/right swipe, and adds <see cref="CreatureHuntTask"/>, which moves a creature no rider steers toward the nearest
/// enemy. Gated on <see cref="IsCreatureBanditDecorator"/>: an agent that stops being a creature bandit (a rider climbed
/// on) falls through to an idle sleep. The fight waits for deployment to end (<see cref="CreatureMayFightDecorator"/>):
/// until then, and whenever no enemy is left, the creature holds where it stands (<see cref="CreatureHoldTask"/>).
/// Ticked on the main thread by BehaviorTreeMissionLogic (#592).
///
/// Blackboard: the spider's cooldown stamps and target bearing (<see cref="IBTSpiderBlackboard"/>), which its nodes read.
///
/// Tuning (<see cref="CreatureBanditTuning"/>, MCM "Creature Bandits"): the cooldowns are read when the
/// tree is built, at the creature's spawn; the strike rules (targets per attack, damage, knockdown) at each attack.
/// </summary>
public class CreatureBanditBehaviorTree : BehaviorTree, IBTBannerlordBase, IBTSpiderBlackboard
{
    public BTBlackboardValue<Agent> Agent { get; set; }
    public BTBlackboardValue<DateTime?> PounceLastFired { get; set; }
    public BTBlackboardValue<DateTime?> SideAttackLastFired { get; set; }
    public BTBlackboardValue<float> TargetBearing { get; set; }

    // base(10) is not a throttle (see SpiderBehaviorTree): the tree runs every component tick, paced by its sleeps.
    public CreatureBanditBehaviorTree(Agent agent) : base(10)
    {
        Agent = new BTBlackboardValue<Agent>(agent);
        PounceLastFired = new BTBlackboardValue<DateTime?>(null);
        SideAttackLastFired = new BTBlackboardValue<DateTime?>(null);
        TargetBearing = new BTBlackboardValue<float>(0f);
    }

    public static new BehaviorTree BuildTree(object[] objects)
    {
        if (objects[0] is not Agent agent) return null;
        var tuning = CreatureBanditTuning.Current;
        System.Func<SpiderStrikeSet> strikes = () => CreatureBanditTuning.Current.Strikes;

        return StartBuildingTree(new CreatureBanditBehaviorTree(agent))
            .AddSelector("main")
                .AddSelector("creature bandit", new IsCreatureBanditDecorator())
                    .AddSelector("fight", new CreatureMayFightDecorator())                   // deployment over
                        .AddSelector("engage", new SpiderEngageDecorator())
                            .AddSequence("pounce", new SpiderAttackOffCooldownDecorator(SpiderAttackKind.Pounce, tuning.PounceCooldownSeconds))
                                .AddTask(new SpiderPounceTask(strikes))
                                .AddTask(new SleepTask(new TimeSpan(0, 0, 0, 0, 300)))       // settle before next eval
                            .Up()
                            .AddSequence("side attack", new SpiderAttackOffCooldownDecorator(SpiderAttackKind.SideAttack, tuning.SwipeCooldownSeconds))
                                .AddTask(new SpiderSideAttackTask(strikes))
                                .AddTask(new SleepTask(new TimeSpan(0, 0, 0, 0, 300)))
                            .Up()
                        .Up()                                                                // not engaged, or both on cooldown
                        .AddSequence("hunt")
                            .AddTask(new CreatureHuntTask(creature => SpiderAttackActions.IsSpiderAttack(creature.GetCurrentAction(0))))
                            .AddTask(new SleepTask(new TimeSpan(0, 0, 0, 0, 250)))           // re-aim ~4/s
                        .Up()
                    .Up()
                    .AddSequence("hold")                                                     // deploying, or no enemy left
                        .AddTask(new CreatureHoldTask())
                        .AddTask(new SleepTask(new TimeSpan(0, 0, 1)))
                    .Up()
                .Up()
                .AddSequence("idle")
                    .AddTask(new SleepTask(new TimeSpan(0, 0, 1)))                            // no longer a creature bandit
                .Up()
            .Up()
            .AddConstantEventListener(new OnSpiderDied())
            .Finish();
    }
}
