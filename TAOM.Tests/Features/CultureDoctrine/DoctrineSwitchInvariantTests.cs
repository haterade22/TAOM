using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.CultureDoctrine.Doctrines;
using TAOM.Features.CultureDoctrine.Domain;
using TAOM.Features.CultureDoctrine.Hooks.Tactics;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.CultureDoctrine;

/// <summary>
/// The two switches where a doctrine enum meets an engine type cannot be enumerated by
/// reflection, so their bodies are read as IL (the way <c>CultureDoctrineBindingTests</c> reads
/// <c>MakeDecision</c>): every <see cref="BehaviorKind"/> must reach a
/// <c>SetBehaviorWeight&lt;T&gt;</c> whose T <c>TeamAIGeneral</c> registers on every field
/// formation, and every <see cref="DoctrineTactic"/> must reach a constructor of the type its
/// engine name promises. An enum member added without its case is otherwise a silent no-op
/// (the applier) or a mission-time throw (the factory).
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

        var targeted = Body(typeof(BehaviorWeightApplier), nameof(BehaviorWeightApplier.Apply))
            .Select(i => i.Value as MethodInfo)
            .Where(m => m != null && m.Name == "SetBehaviorWeight" && m.IsGenericMethod)
            .Select(m => m!.GetGenericArguments()[0].Name)
            .ToList();

        var missing = targeted.Where(t => !registered.Contains(t)).Distinct().ToList();
        Assert.AreEqual(0, missing.Count,
            "SetBehaviorWeight<T> throws MBException for a T the formation never received; TeamAIGeneral does not register: " + string.Join(", ", missing));
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
