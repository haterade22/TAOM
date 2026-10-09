using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
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
            "RaceAbilityHooks.ReduceDamage(in attackInformation, in collisionData,",
            "RaceAbilityHooks.AmplifyDamage(in attackInformation, in collisionData,",
            "RaceAbilityHooks.CrushVerdict(attackerAgent, defenderAgent, strikeType,",
            "RaceAbilityHooks.ShrugsOff(victimAgent)",
        };
        AssertCalls("Main/Features/CombatMechanics/Models/TaomCombatMechanicsModel.cs", calls);
        AssertCalls("Main/Features/CombatMechanics/Models/TaomCustomBattleDamageModel.cs", calls);
    }

    [TestMethod]
    public void CrushVerdict_IsAskedBeforeTheCombatRules()
    {
        var source = RepoPaths.ReadSource("Main/Features/CombatMechanics/Models/TaomCombatMechanicsModel.cs", stripComments: true);

        Assert.IsTrue(source.IndexOf("RaceAbilityHooks.CrushVerdict(", System.StringComparison.Ordinal)
                      < source.IndexOf("_combat.CrushThrough(", System.StringComparison.Ordinal),
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

    // Building the runtime builds its engine-facing parts, which hold engine lists: on hosted CI's reference
    // assemblies their constructors throw (tests.md "Test categories").
    [TestMethod]
    [TestCategory("RequiresGame")]
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

    [TestMethod]
    public void MissionLogic_TellsTheDeathsWhetherTheVictimWasASoldier()
    {
        // A horse has no Character and no team; kill credit needs to know (RaceAbilityService.CreditsKill).
        AssertCalls("Main/Features/RaceAbilities/Hooks/RaceAbilitiesMissionLogic.cs",
            "var soldier = affectedAgent.Character != null;",
            "_runtime.Deaths.OnAgentRemoved(affectedAgent, soldier,");
    }

    [TestMethod]
    public void RuntimeClear_EmptiesEveryEngineFacingPart()
    {
        // Their buffers hold agents, and through their teams the ended mission, until cleared.
        AssertCalls("Main/Features/RaceAbilities/Hooks/RaceAbilityRuntime.cs",
            "Sensor.Clear();", "Activator.Clear();", "Ticker.Clear();", "Deaths.Clear();", "Visuals.Clear();");
        AssertCalls("Main/Features/RaceAbilities/Hooks/RaceAbilityTicker.cs", "_scratch.Clear();");
    }

    [TestMethod]
    public void Visuals_RunOnlyFromMainThreadSteps()
    {
        // The outline and the sparks never run off the main thread (#634): the activator (the tree tick), the
        // ticker, and the deaths handler, which the mission logic runs inline when the removal callback is on the
        // main thread and parks for the next tick when it is not. The models reach RaceAbilityHooks and the stat
        // applier from any thread, so no other file in the feature may call the visuals.
        AssertCalls("Main/Features/RaceAbilities/Hooks/RaceAbilityActivator.cs", "_runtime.Visuals.Burst(agent);", "_runtime.Visuals.Refresh();");
        AssertCalls("Main/Features/RaceAbilities/Hooks/RaceAbilityTicker.cs", "_runtime.Visuals.Refresh();", "_runtime.Visuals.Forget(agent);");
        AssertCalls("Main/Features/RaceAbilities/Hooks/RaceAbilityDeaths.cs", "_runtime.Visuals.Forget(affected);");

        // The runtime reaches the any-thread models too (StateOf), so it may only clear at mission end.
        var allowed = new System.Collections.Generic.Dictionary<string, string[]>
        {
            ["RaceAbilityActivator.cs"] = new[] { "Burst", "Refresh" },
            ["RaceAbilityTicker.cs"] = new[] { "Refresh", "Forget" },
            ["RaceAbilityDeaths.cs"] = new[] { "Forget" },
            ["RaceAbilityRuntime.cs"] = new[] { "Clear" },
        };
        var call = new Regex(@"\bVisuals\.(Refresh|Burst|Forget|Clear)\(");
        var root = RepoPaths.RepoPath("Main", "Features", "RaceAbilities");
        foreach (var file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
            foreach (Match match in call.Matches(RepoPaths.StripComments(File.ReadAllText(file))))
            {
                var name = Path.GetFileName(file);
                Assert.IsTrue(allowed.TryGetValue(name, out var calls) && calls.Contains(match.Groups[1].Value),
                    $"{name} must not call Visuals.{match.Groups[1].Value}");
            }
    }

    [TestMethod]
    public void Visuals_GuardEveryHandle_BeforeANativeWrite()
    {
        // The engine's outline setters check nothing, so a deleted or recycled handle's visuals fault natively.
        // One write site, Paint, behind vanilla's own guard (not deleted, valid visuals) plus slot identity (#592);
        // and the mission-end Clear makes no native call, because the agents are being torn down.
        var source = RepoPaths.ReadSource("Main/Features/RaceAbilities/Hooks/RaceAbilityVisuals.cs", stripComments: true);

        Assert.AreEqual(1, Regex.Matches(source, @"SetContourColor\(").Count, "one outline write site");
        Assert.AreEqual(1, Regex.Matches(source, @"CreateBurstParticle\(").Count, "one burst site");
        var paint = Body(source, "private static void Paint(");
        var write = paint.IndexOf("SetContourColor(", System.StringComparison.Ordinal);
        Assert.IsTrue(write >= 0, "Paint must make the outline write");
        foreach (var guard in new[] { "AgentSlotIdentity.IsCurrentOccupant(agent)", "AgentState.Deleted", ".IsValid()" })
        {
            var at = paint.IndexOf(guard, System.StringComparison.Ordinal);
            Assert.IsTrue(at >= 0 && at < write, "Paint must check " + guard + " before the write");
        }
        StringAssert.Contains(source, "MissionThreadGuard.NoteCall(");
        Assert.IsFalse(Body(source, "public void Clear()").Contains("Paint("), "Clear must make no native call");
    }

    // The block body of the member whose declaration starts with the signature, braces matched.
    private static string Body(string source, string signature)
    {
        var start = source.IndexOf(signature, System.StringComparison.Ordinal);
        Assert.IsTrue(start >= 0, "not found: " + signature);
        var open = source.IndexOf('{', start);
        var depth = 0;
        for (var i = open; i < source.Length; i++)
        {
            depth += source[i] == '{' ? 1 : source[i] == '}' ? -1 : 0;
            if (depth == 0)
                return source.Substring(open, i - open + 1);
        }
        Assert.Fail("unbalanced braces after " + signature);
        return "";
    }

    [TestMethod]
    public void MissionLogic_ClearsTheRuntimeAtMissionEnd() =>
        AssertCalls("Main/Features/RaceAbilities/Hooks/RaceAbilitiesMissionLogic.cs", "_runtime.Clear();");
}
