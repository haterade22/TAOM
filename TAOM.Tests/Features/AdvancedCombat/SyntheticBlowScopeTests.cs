using System.Linq;
using System.Reflection;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.AdvancedCombat;
using TAOM.Features.CareerSystem;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.AdvancedCombat;

/// <summary>
/// TAOM's own blows (a creature's attack, a signature strike's ring) write their damage directly and never run the
/// damage model. <see cref="CustomAttacksUtils.IsRegisteringSyntheticBlow"/> marks them while the engine raises its hit
/// callbacks, which it does synchronously inside Mission.RegisterBlow (v1.5.3: Agent.RegisterBlow, HandleBlow,
/// Mission.OnAgentHit, OnScoreHit). The career's "+N from ability" line reads it, so a player-owned creature blow (#643)
/// claims no ability share while a punch or kick, which the ability's DamageMultiplierBonus does reach, keeps its line
/// (Codex review, 2026-09-23).
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class SyntheticBlowScopeTests
{
    [TestMethod]
    public void IsRegisteringSyntheticBlow_OutsideAScope_IsFalse()
        => Assert.IsFalse(CustomAttacksUtils.IsRegisteringSyntheticBlow);

    [TestMethod]
    public void EnterSyntheticBlow_MarksTheThreadUntilDisposed()
    {
        using (CustomAttacksUtils.EnterSyntheticBlow())
            Assert.IsTrue(CustomAttacksUtils.IsRegisteringSyntheticBlow);

        Assert.IsFalse(CustomAttacksUtils.IsRegisteringSyntheticBlow);
    }

    [TestMethod]
    public void EnterSyntheticBlow_Nested_StaysMarkedUntilTheOuterScopeEnds()
    {
        using (CustomAttacksUtils.EnterSyntheticBlow())
        {
            using (CustomAttacksUtils.EnterSyntheticBlow())
                Assert.IsTrue(CustomAttacksUtils.IsRegisteringSyntheticBlow);

            Assert.IsTrue(CustomAttacksUtils.IsRegisteringSyntheticBlow);
        }

        Assert.IsFalse(CustomAttacksUtils.IsRegisteringSyntheticBlow);
    }

    [TestMethod]
    public void DefaultScope_DisposedByMistake_DoesNotUnmarkAnOpenScope()
    {
        using (CustomAttacksUtils.EnterSyntheticBlow())
        {
            default(CustomAttacksUtils.SyntheticBlowScope).Dispose();
            Assert.IsTrue(CustomAttacksUtils.IsRegisteringSyntheticBlow);
        }
    }

    [TestMethod]
    public void RegisterBlow_OpensTheScopeAroundTheEngineCall()
    {
        var register = AccessTools.Method(typeof(CustomAttacksUtils), nameof(CustomAttacksUtils.RegisterBlow));
        Assert.IsNotNull(register, "CustomAttacksUtils.RegisterBlow is gone.");
        var names = Called(register!).Select(m => m.Name).ToList();
        CollectionAssert.Contains(names, nameof(CustomAttacksUtils.EnterSyntheticBlow),
            "RegisterBlow no longer marks its blow, so OnScoreHit listeners cannot tell it from an engine-computed one.");
        CollectionAssert.Contains(names, "Dispose", "RegisterBlow no longer closes the scope it opens.");
    }

    [TestMethod]
    public void CareerOnScoreHit_SkipsTaomBlows()
    {
        var onScoreHit = AccessTools.DeclaredMethod(typeof(CareerPerkMissionBehavior), "OnScoreHit");
        Assert.IsNotNull(onScoreHit, "CareerPerkMissionBehavior.OnScoreHit is gone.");
        Assert.IsTrue(Called(onScoreHit!).Any(m => m.DeclaringType == typeof(CustomAttacksUtils) && m.Name == "get_IsRegisteringSyntheticBlow"),
            "OnScoreHit no longer skips TAOM's synthetic blows: a player-owned creature blow would print a false \"+N from ability\" line.");
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
