using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.CombatMechanics.Hooks;
using TAOM.Tests.Migration;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace TAOM.Tests.Features.CombatMechanics;

/// <summary>
/// The per-culture charge multiplier (#610) is keyed on the RIDER's culture, because a mount
/// agent is built with a null <c>Character</c> (<c>Mission.CreateHorseAgentFromRosterElements</c>
/// passes <c>null</c> into <c>CreateAgent</c>, v1.5.3 <c>Mission.cs:4611</c>). The first cut read
/// <c>agent.Character</c> on the mount and multiplied by 1.0 in every battle; the deep review
/// caught it (RCA <c>docs/reviews/rca-cavalry-charge-2026-09-17.md</c>). <c>Agent</c> is sealed,
/// so the pin is on the IL: the lookup must reach <c>Agent.RiderAgent</c>, and both stat-model
/// slots (campaign and Custom Battle) must go through the one applier that does. The model types
/// are resolved by name after the game-assembly guard, the CombatMechanicsModelInvariantsTests
/// shape: a typeof() operand would load SandBox.dll at JIT time, before the Inconclusive guard.
/// </summary>
[TestClass]
public class MountChargeDamageBindingTests
{
    private const string CampaignModel = "TAOM.Features.CareerSystem.Models.TaomAgentStatCalculateModel";
    private const string CustomBattleModel = "TAOM.Features.CultureDoctrine.Models.TaomCustomBattleAgentStatCalculateModel";

    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    private static void RequireGameAssemblies()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));
    }

    [TestMethod]
    public void RiderCultureOf_HopsThroughRiderAgent()
    {
        var method = AccessTools.Method(typeof(MountChargeDamageApplier), nameof(MountChargeDamageApplier.RiderCultureOf));
        Assert.IsNotNull(method, "MountChargeDamageApplier.RiderCultureOf is gone.");

        var called = CalledSequence(method!);

        CollectionAssert.IsSubsetOf(
            new[] { "get_IsMount", "get_RiderAgent", "get_Character", "get_Culture", "get_StringId" },
            called.Distinct().ToList(),
            "the mount's culture lookup no longer hops through Agent.RiderAgent; a mount's own Character is null.");

        // mount.Character and mount.RiderAgent.Character compile to the same get_Character token, so
        // names alone cannot tell the hop from a stray RiderAgent touch: the ONE get_Character must
        // come after get_RiderAgent (IL order is evaluation order for this chain).
        Assert.AreEqual(1, called.Count(n => n == "get_Character"), "expected exactly one Character read, the rider's.");
        Assert.IsTrue(called.IndexOf("get_RiderAgent") < called.IndexOf("get_Character"),
            "Character is read before RiderAgent: the lookup reads the mount's own (null) Character.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void CampaignStatModel_UpdateAgentStats_UsesTheApplier()
    {
        RequireGameAssemblies();
        AssertUsesApplier(CampaignModel, "UpdateAgentStats", new[] { typeof(Agent), typeof(AgentDrivenProperties) });
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void CustomBattleStatModel_AppliesOnceFromInitializeAgentStats_NeverFromUpdateAgentStats()
    {
        // Custom Battle registers its own AgentStatCalculateModel (SubModule.RegisterCustomBattleModels);
        // without this the cheapest smoke route (Custom Battle cavalry) never sees the multiplier.
        // Where it runs matters: CustomBattleAgentStatCalculateModel writes MountChargeDamage in
        // InitializeAgentStats only (v1.5.3 :48) and its UpdateHorseStats never rewrites it, so a
        // multiply from UpdateAgentStats would compound on every re-run (the second review found
        // exactly that). The Sandbox model rewrites the property every call (:1280), which is why
        // the campaign model may multiply from UpdateAgentStats.
        RequireGameAssemblies();
        AssertUsesApplier(CustomBattleModel, "InitializeAgentStats",
            new[] { typeof(Agent), typeof(Equipment), typeof(AgentDrivenProperties), typeof(AgentBuildData) });
        AssertDoesNotUseApplier(CustomBattleModel, "UpdateAgentStats", new[] { typeof(Agent), typeof(AgentDrivenProperties) });
    }

    [TestMethod]
    public void RiderAgent_IsStillAnEngineProperty()
    {
        var prop = typeof(Agent).GetProperty("RiderAgent", BindingFlags.Public | BindingFlags.Instance);
        Assert.IsNotNull(prop, "Agent.RiderAgent is gone; the applier's rider hop needs a new route.");
        Assert.AreEqual(typeof(Agent), prop!.PropertyType);
    }

    private static void AssertUsesApplier(string modelFullName, string methodName, Type[] parameters)
    {
        Assert.IsTrue(CallsApplier(modelFullName, methodName, parameters),
            modelFullName + "." + methodName + " no longer calls MountChargeDamageApplier.Apply.");
    }

    private static void AssertDoesNotUseApplier(string modelFullName, string methodName, Type[] parameters)
    {
        Assert.IsFalse(CallsApplier(modelFullName, methodName, parameters),
            modelFullName + "." + methodName + " calls MountChargeDamageApplier.Apply; on this model the base never rewrites MountChargeDamage there, so the multiply compounds.");
    }

    private static bool CallsApplier(string modelFullName, string methodName, Type[] parameters)
    {
        var modelType = typeof(TAOM.IoC).Assembly.GetType(modelFullName, throwOnError: true);
        var method = AccessTools.Method(modelType, methodName, parameters);
        Assert.IsNotNull(method, modelType.Name + "." + methodName + " is gone.");

        var il = method!.GetMethodBody()?.GetILAsByteArray();
        Assert.IsNotNull(il, method.Name + " has no readable IL body.");

        return IlCallScanner.ExtractCalledMethods(method, il!)
            .Any(m => m.DeclaringType == typeof(MountChargeDamageApplier) && m.Name == nameof(MountChargeDamageApplier.Apply));
    }

    private static List<string> CalledSequence(MethodBase method)
    {
        var il = method.GetMethodBody()?.GetILAsByteArray();
        Assert.IsNotNull(il, method.Name + " has no readable IL body.");

        var names = IlCallScanner.ExtractCalledMethods(method, il!).Select(m => m.Name).ToList();

        Assert.AreNotEqual(0, names.Count, method.Name + " resolved no calls; the scan failed, not the method.");
        return names;
    }
}
