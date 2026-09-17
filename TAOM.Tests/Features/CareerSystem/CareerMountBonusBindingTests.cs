using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.CareerSystem.Abilities;
using TAOM.Tests.Migration;
using TaleWorlds.MountAndBlade;

namespace TAOM.Tests.Features.CareerSystem;

/// <summary>
/// #611: the career mount bonuses (the MountChargeDamage passive, the Cavalry ability's mount
/// speed and charge) apply on the MOUNT's driven properties through the rider, because no base
/// model writes those two properties on a human and the engine reads them off the horse. Three
/// pins, all on the IL since <c>Agent</c> is sealed: the campaign stat model's mount block calls
/// <c>ApplyMountStatModifiers</c> and reaches <c>Agent.RiderAgent</c>; the buff apply/restore
/// path refreshes the human's <c>MountAgent</c> beside the human (a mount-side multiply is inert
/// until the mount's stats are recomputed); and the human path of the service no longer touches
/// the two mount properties (the unit tests pin the values, this pins the absence of the writes).
/// </summary>
[TestClass]
public class CareerMountBonusBindingTests
{
    private const string CampaignModel = "TAOM.Features.CareerSystem.Models.TaomAgentStatCalculateModel";

    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void CampaignStatModel_UpdateAgentStats_AppliesMountBonusesThroughTheRider()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));

        var modelType = typeof(TAOM.IoC).Assembly.GetType(CampaignModel, throwOnError: true);
        var method = AccessTools.Method(modelType, "UpdateAgentStats", new[] { typeof(Agent), typeof(AgentDrivenProperties) });
        var called = Called(method!);

        Assert.IsTrue(called.Any(m => m.DeclaringType == typeof(ICareerAgentStatService) && m.Name == nameof(ICareerAgentStatService.ApplyMountStatModifiers)),
            "UpdateAgentStats no longer calls ICareerAgentStatService.ApplyMountStatModifiers.");
        Assert.IsTrue(called.Any(m => m.DeclaringType == typeof(Agent) && m.Name == "get_RiderAgent"),
            "UpdateAgentStats no longer reads Agent.RiderAgent; a mount's own Character is null, the rider is the identity.");
    }

    [TestMethod]
    public void ApplyAoeBuff_RefreshesTheMountBesideTheHuman()
    {
        var method = AccessTools.Method(typeof(MissionAbilityExecutionContext), "ApplyAoeBuff");
        Assert.IsNotNull(method, "MissionAbilityExecutionContext.ApplyAoeBuff is gone.");
        var called = Called(method!);

        Assert.IsTrue(called.Any(m => m.DeclaringType == typeof(Agent) && m.Name == "get_MountAgent"),
            "ApplyAoeBuff no longer reaches Agent.MountAgent: the mount fields of the buff never recompute on the horse.");
        Assert.IsTrue(called.Any(m => m.DeclaringType == typeof(Agent) && m.Name == "UpdateAgentProperties"),
            "ApplyAoeBuff no longer calls Agent.UpdateAgentProperties.");
    }

    [TestMethod]
    public void HumanPath_NoLongerWritesTheMountProperties()
    {
        // The three private helpers behind ApplyAgentStatModifiers: none may set MountSpeed or
        // MountChargeDamage. ApplyMountBuff is the one place allowed to.
        foreach (var name in new[] { "ApplyHeroPassives", "ApplyHeroSelfBuff", "ApplyAllyBuff" })
        {
            var method = AccessTools.Method(typeof(CareerAgentStatService), name);
            Assert.IsNotNull(method, "CareerAgentStatService." + name + " is gone.");
            var setters = Called(method!).Where(m => m.Name == "set_MountSpeed" || m.Name == "set_MountChargeDamage").ToList();
            Assert.AreEqual(0, setters.Count, name + " writes a mount property on the rider's properties again: " + string.Join(", ", setters.Select(s => s.Name)));
        }

        var mountBuff = AccessTools.Method(typeof(CareerAgentStatService), "ApplyMountBuff");
        Assert.IsNotNull(mountBuff, "CareerAgentStatService.ApplyMountBuff is gone.");
        var names = Called(mountBuff!).Select(m => m.Name).ToList();
        CollectionAssert.IsSubsetOf(new[] { "set_MountSpeed", "set_MountChargeDamage" }, names, "ApplyMountBuff no longer writes both mount properties.");
    }

    private static MethodBase[] Called(MethodBase method)
    {
        var il = method.GetMethodBody()?.GetILAsByteArray();
        Assert.IsNotNull(il, method.Name + " has no readable IL body.");
        var called = IlCallScanner.ExtractCalledMethods(method, il!).ToArray();
        Assert.AreNotEqual(0, called.Length, method.Name + " resolved no calls; the scan failed, not the method.");
        return called;
    }
}
