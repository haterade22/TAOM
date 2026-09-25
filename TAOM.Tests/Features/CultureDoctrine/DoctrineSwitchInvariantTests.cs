using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.CultureDoctrine.Doctrines;
using TAOM.Features.CultureDoctrine.Domain;
using TAOM.Features.CultureDoctrine.Hooks.Behaviors;
using TAOM.Features.CultureDoctrine.Hooks.Tactics;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.CultureDoctrine;

/// <summary>
/// The two switches where a doctrine enum meets an engine type cannot be enumerated by
/// reflection, so their bodies are read as IL (the way <c>CultureDoctrineBindingTests</c> reads
/// <c>MakeDecision</c>): every <see cref="BehaviorKind"/> must reach a
/// <c>SetBehaviorWeight&lt;T&gt;</c> whose T either <c>TeamAIGeneral</c> registers on every
/// field formation or the applier itself adds first (a TAOM behaviour, through
/// <c>Ensure&lt;T&gt;</c>), and every <see cref="DoctrineTactic"/> must reach a constructor of
/// the type its engine name promises. An enum member added without its case is otherwise a
/// silent no-op (the applier) or a mission-time throw (the factory).
/// </summary>
[TestClass]
public class DoctrineSwitchInvariantTests
{
    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    private static void RequireGame()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));
    }

    private static IEnumerable<KeyValuePair<OpCode, object>> Body(Type type, string method) =>
        PatchProcessor.ReadMethodBody(AccessTools.Method(type, method));

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void BehaviorWeightApplier_SetsAWeightForEveryBehaviorKind()
    {
        RequireGame();

        var setFor = Body(typeof(BehaviorWeightApplier), nameof(BehaviorWeightApplier.Apply))
            .Select(i => i.Value as MethodInfo)
            .Where(m => m != null && m.Name == "SetBehaviorWeight" && m.IsGenericMethod)
            .Select(m => m!.GetGenericArguments()[0].Name)
            .Select(name => name.StartsWith("Behavior") ? name.Substring("Behavior".Length) : name)
            .ToHashSet();

        var kinds = Enum.GetValues(typeof(BehaviorKind)).Cast<BehaviorKind>().Select(k => k.ToString()).ToHashSet();
        CollectionAssert.AreEquivalent(kinds.ToList(), setFor.ToList(),
            "BehaviorWeightApplier.Apply and the BehaviorKind enum disagree; a kind with no case is a silent no-op on the AI thread.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void BehaviorWeightApplier_FootChargeRow_DisarmsTheEngineDefaultCharge()
    {
        RequireGame();

        // TacticComponent.SetDefaultBehaviorWeights arms BehaviorCharge at 1 on every apply
        // (TacticComponent.cs:581-587), and it charges CachedClosestEnemyFormation of any class.
        // A FootCharge row must zero it, or the eored chase comes back through the default row.
        var apply = Body(typeof(BehaviorWeightApplier), nameof(BehaviorWeightApplier.Apply)).Select(i => i.Value as MethodInfo).Where(m => m != null).ToList();
        var disarm = apply.SingleOrDefault(m => m!.Name == "DisarmVanillaCharge");
        Assert.IsNotNull(disarm, "Apply no longer calls DisarmVanillaCharge");
        var body = PatchProcessor.ReadMethodBody(disarm!).ToList();
        var set = body.Select(i => i.Value as MethodInfo).SingleOrDefault(m => m != null && m.Name == "SetBehaviorWeight" && m.IsGenericMethod && m.GetGenericArguments()[0].Name == "BehaviorCharge");
        Assert.IsNotNull(set, "DisarmVanillaCharge no longer writes BehaviorCharge's weight");
        Assert.IsTrue(body.Any(i => i.Key == OpCodes.Ldc_R4 && i.Value is float f && f == 0f), "DisarmVanillaCharge must write 0");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void TaomTacticBase_GivesEveryUnseatedFormationVanillasDefaults()
    {
        RequireGame();

        // Every vanilla tactic gives every formation the default rows; a formation the plan did
        // not seat must not keep stale weights (Mordor's horse stood on BehaviorStop for 30 s
        // in the second A/B). The base's Apply must reach ApplyDefaults, and ApplyDefaults must
        // reset then call SetDefaultBehaviorWeights.
        var apply = PatchProcessor.ReadMethodBody(AccessTools.Method(typeof(TaomTacticBase), "Apply")).Select(i => i.Value as MethodInfo).Where(m => m != null).ToList();
        Assert.IsTrue(apply.Any(m => m!.Name == nameof(BehaviorWeightApplier.ApplyDefaults)), "TaomTacticBase.Apply no longer calls ApplyDefaults");
        var defaults = Body(typeof(BehaviorWeightApplier), nameof(BehaviorWeightApplier.ApplyDefaults)).Select(i => i.Value as MethodInfo).Where(m => m != null).Select(m => m!.Name).ToList();
        CollectionAssert.Contains(defaults, "ResetBehaviorWeights");
        CollectionAssert.Contains(defaults, "SetDefaultBehaviorWeights");
    }

    [TestMethod]
    [TestCategory("RequiresGameIL")]
    [TestCategory("BindingVerification")]
    public void BehaviorWeightApplier_TargetsOnlyBehavioursTeamAIGeneralRegisters()
    {
        RequireGame();

        var teamAIGeneral = AccessTools.TypeByName("TaleWorlds.MountAndBlade.TeamAIGeneral");
        Assert.IsNotNull(teamAIGeneral, "TeamAIGeneral did not resolve.");
        var registered = Body(teamAIGeneral, "OnUnitAddedToFormationForTheFirstTime")
            .Select(i => i.Value as ConstructorInfo)
            .Where(c => c != null)
            .Select(c => c!.DeclaringType!.Name)
            .ToHashSet();

        var body = Body(typeof(BehaviorWeightApplier), nameof(BehaviorWeightApplier.Apply)).ToList();
        var targeted = body
            .Select(i => i.Value as MethodInfo)
            .Where(m => m != null && m.Name == "SetBehaviorWeight" && m.IsGenericMethod)
            .Select(m => m!.GetGenericArguments()[0])
            .ToList();
        var ensured = body
            .Select(i => i.Value as MethodInfo)
            .Where(m => m != null && m.Name == nameof(BehaviorWeightApplier.Ensure) && m.IsGenericMethod)
            .Select(m => m!.GetGenericArguments()[0].Name)
            .ToHashSet();

        var missing = targeted
            .Where(t => !registered.Contains(t.Name) && !(typeof(TaomBehaviorBase).IsAssignableFrom(t) && ensured.Contains(t.Name)))
            .Select(t => t.Name)
            .Distinct()
            .ToList();
        Assert.AreEqual(0, missing.Count,
            "SetBehaviorWeight<T> throws MBException for a T the formation never received; neither TeamAIGeneral registers nor the applier ensures: " + string.Join(", ", missing));
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void EveryTaomBehaviour_DerivesFromBehaviorComponentDirectly_ThroughTheTaomBase()
    {
        RequireGame();

        // GetBehavior<T> and SetBehaviorWeight<T> match with `is T` (FormationAI.cs:120-155): a
        // TAOM behaviour under a vanilla concrete type would be picked up by every vanilla tactic
        // that weights that type.
        var behaviours = typeof(TaomBehaviorBase).Assembly.GetTypes()
            .Where(t => !t.IsAbstract && typeof(TaomBehaviorBase).IsAssignableFrom(t))
            .ToList();
        Assert.IsTrue(behaviours.Count >= 5, "expected the five Phase C behaviours");
        var behaviorComponent = AccessTools.TypeByName("TaleWorlds.MountAndBlade.BehaviorComponent");
        foreach (var t in behaviours)
        {
            Assert.AreSame(behaviorComponent, typeof(TaomBehaviorBase).BaseType, "TaomBehaviorBase must sit directly on BehaviorComponent");
            Assert.AreSame(typeof(TaomBehaviorBase), t.BaseType, t.Name + " must derive from TaomBehaviorBase, not a vanilla behaviour");
            Assert.IsNotNull(t.GetConstructor(new[] { AccessTools.TypeByName("TaleWorlds.MountAndBlade.Formation") }), t.Name + " needs a (Formation) constructor for Ensure<T>");
        }
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void TacticFactory_ConstructsTheNamedTypeForEveryDoctrineTactic()
    {
        RequireGame();

        var constructed = Body(typeof(TacticFactory), nameof(TacticFactory.Create))
            .Select(i => i.Value as ConstructorInfo)
            .Where(c => c != null)
            .Select(c => c!.DeclaringType!)
            .Where(t => t.Namespace == typeof(TacticFactory).Namespace)
            .Select(t => t.Name)
            .ToHashSet();

        var expected = DoctrineTacticIds.All.Select(DoctrineTacticIds.EngineTypeName).ToList();
        var missing = expected.Where(name => !constructed.Contains(name)).ToList();
        Assert.AreEqual(0, missing.Count, "TacticFactory.Create has no case constructing: " + string.Join(", ", missing));
    }
}
