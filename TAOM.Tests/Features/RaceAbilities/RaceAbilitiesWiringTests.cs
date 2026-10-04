using System.Linq;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Composition;
using TAOM.Core.Domain;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.RaceAbilities;
using TAOM.Features.RaceAbilities.Hooks;
using TAOM.Tests.Infrastructure;

// The race abilities reach the engine only through six models owned by three other features, in both the
// campaign and Custom Battle, plus one mission logic their module adds. Nothing fails at build time if a
// model loses its call, so each call site is pinned here, with the module's registration.

namespace TAOM.Tests.Features.RaceAbilities;

[TestClass]
public class RaceAbilitiesWiringTests
{
    private static void AssertCalls(string path, params string[] calls)
    {
        var source = RepoPaths.ReadSource(path, stripComments: true);
        foreach (var call in calls)
            StringAssert.Contains(source, call, $"{path} must call {call}");
    }

    [TestMethod]
    public void StatModels_ApplyTheStatsAndResistances_InBothModes()
    {
        var calls = new[]
        {
            "RaceAbilityHooks.ApplyStats(agent, agentDrivenProperties)",
            "RaceAbilityHooks.KnockDownResistance(agent, base.GetKnockDownResistance(agent, strikeType))",
            "RaceAbilityHooks.KnockBackResistance(agent, base.GetKnockBackResistance(agent))",
            "RaceAbilityHooks.DismountResistance(agent, base.GetDismountResistance(agent))",
        };
        AssertCalls("Main/Features/CareerSystem/Models/TaomAgentStatCalculateModel.cs", calls);
        AssertCalls("Main/Features/CultureDoctrine/Models/TaomCustomBattleAgentStatCalculateModel.cs", calls);
    }

    [TestMethod]
    public void DamageModels_CarryEveryCombatHook_InBothModes()
    {
        var calls = new[]
        {
            "RaceAbilityHooks.ReduceDamage(in attackInformation,",
            "RaceAbilityHooks.AmplifyDamage(in attackInformation, in collisionData,",
            "RaceAbilityHooks.CrushVerdict(attackerAgent, defenderAgent, strikeType,",
            "RaceAbilityHooks.ShrugsOff(victimAgent)",
        };
        AssertCalls("Main/Features/CombatMechanics/Models/TaomCombatMechanicsModel.cs", calls);
        AssertCalls("Main/Features/CreatureBandits/Models/TaomCustomBattleCreatureDamageModel.cs", calls);
    }

    [TestMethod]
    public void CrushVerdict_IsAskedBeforeTheCombatRules()
    {
        var source = RepoPaths.ReadSource("Main/Features/CombatMechanics/Models/TaomCombatMechanicsModel.cs", stripComments: true);

        Assert.IsTrue(source.IndexOf("RaceAbilityHooks.CrushVerdict(", System.StringComparison.Ordinal)
                      < source.IndexOf("_crushThroughService.DecideCrushThrough(", System.StringComparison.Ordinal),
            "a defender standing fast must hold before the troll or skill rules can crush through");
    }

    [TestMethod]
    public void MoraleModels_HoldTheNerveOfALiveFloor_InBothModes()
    {
        AssertCalls("Main/Features/CultureDoctrine/Models/TaomBattleMoraleModel.cs", "!RaceAbilityHooks.HoldsNerve(agent)");
        AssertCalls("Main/Features/CultureDoctrine/Models/TaomCustomBattleMoraleModel.cs", "!RaceAbilityHooks.HoldsNerve(agent)");
    }

    [TestMethod]
    public void Module_IsListed_AndAddsTheMissionLogic()
    {
        Assert.AreEqual(1, FeatureModules.All.OfType<RaceAbilitiesModule>().Count());

        var behavior = new RaceAbilitiesModule().MissionBehaviors.Single();

        Assert.AreEqual(typeof(RaceAbilitiesMissionLogic), behavior.BehaviorType);
    }

    [TestMethod]
    public void Module_RegistersARuntimeTheContainerCanBuild_AsOneInstance()
    {
        using var container = new Container();
        container.RegisterInstance(Substitute.For<IPathService>());
        container.RegisterInstance(Substitute.For<IRaceManager>());
        container.RegisterInstance(Substitute.For<IModLogger>());

        new RaceAbilitiesModule().RegisterServices(container);

        var runtime = container.Resolve<RaceAbilityRuntime>();
        Assert.IsNotNull(runtime);
        Assert.AreSame(runtime, container.Resolve<RaceAbilityRuntime>());
        Assert.AreSame(runtime.Service, container.Resolve<RaceAbilityService>());
    }

    [TestMethod]
    public void MissionLogic_MarksTheMainThread_AndDefersRemovals()
    {
        // It owns a DeferredCallbackQueue, so it marks the main thread itself (harmony-patches.md), and
        // OnAgentRemoved can arrive off it (#634).
        AssertCalls("Main/Features/RaceAbilities/Hooks/RaceAbilitiesMissionLogic.cs",
            "MissionThreadGuard.MarkMainThread();",
            "_deferred.RunOrDefer(\"RaceAbilitiesMissionLogic.OnAgentRemoved\"");
    }
}
