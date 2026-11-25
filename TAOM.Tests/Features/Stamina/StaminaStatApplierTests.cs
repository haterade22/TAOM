using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Stamina.Domain;
using TAOM.Features.Stamina.Hooks;
using TaleWorlds.MountAndBlade;

namespace TAOM.Tests.Features.Stamina;

[TestClass]
public class StaminaStatApplierTests
{
    private const float Delta = 0.0001f;

    // The values a base model leaves on a soldier, far enough apart that a swapped property shows.
    private static AgentDrivenProperties Base() => new AgentDrivenProperties
    {
        SwingSpeedMultiplier = 0.93f,
        ThrustOrRangedReadySpeedMultiplier = 0.91f,
        HandlingMultiplier = 1f,
        MaxSpeedMultiplier = 0.8f,
        CombatMaxSpeedMultiplier = 0.7f,
        AIAttackOnDecideChance = 0.6f,
    };

    [TestCategory("RequiresGame")]   // constructs AgentDrivenProperties
    [TestMethod]
    public void Apply_NoEffect_LeavesEveryValueIdentical()
    {
        var p = Base();

        StaminaStatApplier.Apply(p, StaminaBandEffects.None, mounted: false);

        Assert.AreEqual(0.93f, p.SwingSpeedMultiplier);
        Assert.AreEqual(0.91f, p.ThrustOrRangedReadySpeedMultiplier);
        Assert.AreEqual(1f, p.HandlingMultiplier);
        Assert.AreEqual(0.8f, p.MaxSpeedMultiplier);
        Assert.AreEqual(0.7f, p.CombatMaxSpeedMultiplier);
        Assert.AreEqual(0.6f, p.AIAttackOnDecideChance);
    }

    [TestCategory("RequiresGame")]   // constructs AgentDrivenProperties
    [TestMethod]
    public void Apply_TiredOnFoot_ScalesEachPropertyByItsOwnMultiplier()
    {
        var p = Base();
        var tired = new StaminaBandEffects(swing: 0.9f, handling: 0.8f, move: 0.5f, aiAttack: 0.25f);

        StaminaStatApplier.Apply(p, tired, mounted: false);

        Assert.AreEqual(0.93f * 0.9f, p.SwingSpeedMultiplier, Delta);
        Assert.AreEqual(0.91f * 0.9f, p.ThrustOrRangedReadySpeedMultiplier, Delta);
        Assert.AreEqual(0.8f, p.HandlingMultiplier, Delta);
        Assert.AreEqual(0.8f * 0.5f, p.MaxSpeedMultiplier, Delta);
        Assert.AreEqual(0.7f * 0.5f, p.CombatMaxSpeedMultiplier, Delta);
        Assert.AreEqual(0.6f * 0.25f, p.AIAttackOnDecideChance, Delta);
    }

    [TestCategory("RequiresGame")]   // constructs AgentDrivenProperties
    [TestMethod]
    public void Apply_Mounted_KeepsTheSpeedValuesAndStillSlowsTheArm()
    {
        var p = Base();

        StaminaStatApplier.Apply(p, StaminaTuning.DefaultExhausted, mounted: true);

        Assert.AreEqual(0.8f, p.MaxSpeedMultiplier, "a rider's speed values are not touched: mounts are left alone");
        Assert.AreEqual(0.7f, p.CombatMaxSpeedMultiplier);
        Assert.AreEqual(0.93f * 0.9f, p.SwingSpeedMultiplier, Delta);
    }

    [TestCategory("RequiresGame")]   // constructs AgentDrivenProperties
    [TestMethod]
    public void Apply_OnAFreshBaseEachTime_GivesTheSameResult()
    {
        // The engine assigns these properties afresh before every pass, so the pass must depend on its input only.
        var first = Base();
        var second = Base();

        StaminaStatApplier.Apply(first, StaminaTuning.DefaultTired, mounted: false);
        StaminaStatApplier.Apply(second, StaminaTuning.DefaultTired, mounted: false);

        Assert.AreEqual(first.SwingSpeedMultiplier, second.SwingSpeedMultiplier);
        Assert.AreEqual(first.MaxSpeedMultiplier, second.MaxSpeedMultiplier);
        Assert.AreEqual(first.AIAttackOnDecideChance, second.AIAttackOnDecideChance);
    }

    [TestCategory("RequiresGame")]   // constructs AgentDrivenProperties
    [TestMethod]
    public void Apply_AttackChanceWouldFallBelowTheEngineFloor_StopsAtTheFloor()
    {
        var p = Base();
        p.AIAttackOnDecideChance = 0.08f;

        StaminaStatApplier.Apply(p, new StaminaBandEffects(1f, 1f, 1f, aiAttack: 0.5f), mounted: false);

        Assert.AreEqual(StaminaStatApplier.MinAttackChance, p.AIAttackOnDecideChance, Delta);
    }

    [TestCategory("RequiresGame")]   // constructs AgentDrivenProperties
    [TestMethod]
    public void Apply_AttackChanceAlreadyBelowTheFloor_IsNotRaised()
    {
        var p = Base();
        p.AIAttackOnDecideChance = 0.02f;

        StaminaStatApplier.Apply(p, new StaminaBandEffects(1f, 1f, 1f, aiAttack: 0.5f), mounted: false);

        Assert.AreEqual(0.02f, p.AIAttackOnDecideChance, Delta);
    }

    [TestCategory("RequiresGame")]   // constructs AgentDrivenProperties
    [TestMethod]
    public void Apply_MultiplierNotANumber_LeavesTheValuesAsTheyWere()
    {
        var p = Base();
        var broken = new StaminaBandEffects(float.NaN, float.PositiveInfinity, float.NaN, float.NaN);

        StaminaStatApplier.Apply(p, broken, mounted: false);

        Assert.AreEqual(0.93f, p.SwingSpeedMultiplier);
        Assert.AreEqual(0.91f, p.ThrustOrRangedReadySpeedMultiplier);
        Assert.AreEqual(1f, p.HandlingMultiplier);
        Assert.AreEqual(0.8f, p.MaxSpeedMultiplier);
        Assert.AreEqual(0.7f, p.CombatMaxSpeedMultiplier);
        Assert.AreEqual(0.6f, p.AIAttackOnDecideChance);
    }
}
