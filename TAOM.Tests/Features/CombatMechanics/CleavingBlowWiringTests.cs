using System;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Infrastructure;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.CombatMechanics;

/// <summary>
/// Cleaving Blows wiring pins. The chain counter keeps one entry per attacker object and takes a lock because the engine's own
/// single-thread declaration of the melee hit callback is not something this mod has ever observed at run time. Both damage models
/// ask creature cleave, then Cleaving Blows, then the engine, and the service stays free of agent indexes and random rolls.
/// </summary>
[TestClass]
public class CleavingBlowWiringTests
{
    private const string ServiceSource = "Main/Features/CombatMechanics/CleavingBlowService.cs";

    // The game-bound test: the engine says Mission.MeleeHitCallback is not multi-thread callable (v1.5.7). Read by reflection on the
    // attribute data, so there is no compile reference to TaleWorlds.DotNet.
    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGame")]
    public void MeleeHitCallback_IsDeclaredSingleThreaded()
    {
        if (!GameAssemblies.EnsureLoaded())
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));

        var mission = AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType("TaleWorlds.MountAndBlade.Mission", throwOnError: false))
            .FirstOrDefault(t => t != null);
        Assert.IsNotNull(mission, "TaleWorlds.MountAndBlade.Mission is not loaded");

        var method = mission!.GetMethod("MeleeHitCallback", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.IsNotNull(method, "Mission.MeleeHitCallback is gone: re-read the chain counter's lock and the docs thread row");

        var data = method!.GetCustomAttributesData().FirstOrDefault(a => a.AttributeType.Name == "MBCallback");
        Assert.IsNotNull(data, "Mission.MeleeHitCallback no longer carries MBCallback: re-read the chain counter's lock and the docs thread row");

        var parameters = data!.Constructor.GetParameters();
        int index = Array.FindIndex(parameters, p => p.Name == "isMultiThreadCallable");
        object? value = index >= 0 && index < data.ConstructorArguments.Count
            ? data.ConstructorArguments[index].Value
            : data.NamedArguments.FirstOrDefault(n => n.MemberName == "isMultiThreadCallable").TypedValue.Value;

        Assert.IsNotNull(value, "MBCallback has no readable isMultiThreadCallable argument");
        Assert.AreEqual(false, value,
            "Mission.MeleeHitCallback is now multi-thread callable: re-read the chain counter's lock and the docs thread row");
    }

    [DataTestMethod]
    [DataRow("Main/Features/CombatMechanics/Models/TaomCombatMechanicsModel.cs")]
    [DataRow("Main/Features/CombatMechanics/Models/TaomCustomBattleDamageModel.cs")]
    public void Model_AsksCreatureCleaveThenCleavingBlowsThenBase(string modelSource)
    {
        var momentum = OverrideText(modelSource, "CalculateRemainingMomentum");
        int creature = momentum.IndexOf("CleaveMomentum(", StringComparison.Ordinal);
        int cleaving = momentum.IndexOf("CleavingBlowMomentum(", StringComparison.Ordinal);
        int baseCall = momentum.IndexOf("base.CalculateRemainingMomentum(", StringComparison.Ordinal);

        Assert.IsTrue(creature >= 0 && creature < cleaving, "creature cleave answers before Cleaving Blows");
        Assert.IsTrue(cleaving >= 0 && cleaving < baseCall, "Cleaving Blows answers before the engine's own carry");

        var reaction = OverrideText(modelSource, "DecideWeaponCollisionReaction");
        int creatureReaction = reaction.IndexOf("CollisionReaction(", StringComparison.Ordinal);
        int cleavingReaction = reaction.IndexOf("CleavingBlowReaction(", StringComparison.Ordinal);

        Assert.IsTrue(creatureReaction >= 0 && creatureReaction < cleavingReaction, "creature cleave forces its reaction first");
    }

    [TestMethod]
    public void Service_KeysTheChainOnTheAgentObject_UnderALock_WithoutIndexOrRandom()
    {
        var source = RepoPaths.ReadSource(ServiceSource, stripComments: true);

        StringAssert.Contains(source, "ConditionalWeakTable<object");
        StringAssert.Contains(source, "lock (");
        Assert.IsFalse(source.Contains(".Index"), "never key mission state on Agent.Index: a deleted agent's slot goes to the next spawn");
        Assert.IsFalse(source.Contains("MBRandom"), "the rule is deterministic");
    }

    private static string OverrideText(string path, string name)
    {
        var source = RepoPaths.ReadSource(path, stripComments: true);
        var start = source.IndexOf($" {name}(", StringComparison.Ordinal);
        Assert.IsTrue(start >= 0, $"{path} no longer declares {name}");
        var end = source.IndexOf("public override", start, StringComparison.Ordinal);
        return end < 0 ? source.Substring(start) : source.Substring(start, end - start);
    }
}
