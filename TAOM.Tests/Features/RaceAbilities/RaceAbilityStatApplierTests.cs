using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.RaceAbilities.Domain;
using TAOM.Features.RaceAbilities.Hooks;
using TaleWorlds.MountAndBlade;

// The stat post-pass: each percentage multiplies the value the engine just derived. The engine rewrites
// every one of these on each UpdateAgentStats in both the campaign and Custom Battle models, so the
// multiply never compounds (docs/features/race-abilities.md, "Engine levers").

namespace TAOM.Tests.Features.RaceAbilities;

[TestClass]
public class RaceAbilityStatApplierTests
{
    [DataTestMethod]
    [DataRow(1f, 20f, 1.2f)]
    [DataRow(1f, -20f, 0.8f)]
    [DataRow(0.93f, 0f, 0.93f)]
    public void Times_AppliesThePercentage(float value, float percent, float expected) =>
        Assert.AreEqual(expected, RaceAbilityStatApplier.Times(value, percent), 0.0001f);

    [TestMethod]
    public void Times_NaNValue_PassesItThrough() =>
        Assert.IsTrue(float.IsNaN(RaceAbilityStatApplier.Times(float.NaN, 20f)));

    [TestMethod]
    public void Over_FasterAccelerationShortensTheTime() =>
        Assert.AreEqual(2f, RaceAbilityStatApplier.Over(2.5f, 25f), 0.0001f);

    [TestMethod]
    public void Over_ZeroPercent_KeepsTheValue() =>
        Assert.AreEqual(2.5f, RaceAbilityStatApplier.Over(2.5f, 0f), 0.0001f);

    [DataTestMethod]
    [DataRow(0.9f, -60f, 0.36f)]
    [DataRow(0.9f, 25f, 1f)]       // a probability stops at 1
    [DataRow(0.5f, 20f, 0.6f)]
    public void Chance_AppliesThePercentageWithinZeroToOne(float value, float percent, float expected) =>
        Assert.AreEqual(expected, RaceAbilityStatApplier.Chance(value, percent), 0.0001f);

    [TestMethod]
    public void Chance_NaNValue_PassesItThrough() =>
        Assert.IsTrue(float.IsNaN(RaceAbilityStatApplier.Chance(float.NaN, 20f)));

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void ApplyMount_RaisesTheHorsesSpeedAndNothingElse()
    {
        var props = new AgentDrivenProperties { MountSpeed = 1f, MaxSpeedMultiplier = 1f };

        RaceAbilityStatApplier.ApplyMount(props, new RaceAbilityEffects { MountSpeedPercent = 15f, MoveSpeedPercent = 50f });

        Assert.AreEqual(1.15f, props.MountSpeed, 0.0001f);
        Assert.AreEqual(1f, props.MaxSpeedMultiplier, 0.0001f);
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void Apply_NoEffects_ChangesNothing()
    {
        var props = new AgentDrivenProperties { MaxSpeedMultiplier = 1f, SwingSpeedMultiplier = 0.95f };

        RaceAbilityStatApplier.Apply(props, null);

        Assert.AreEqual(1f, props.MaxSpeedMultiplier, 0.0001f);
        Assert.AreEqual(0.95f, props.SwingSpeedMultiplier, 0.0001f);
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void Apply_WritesEachEffectToItsProperty()
    {
        var props = new AgentDrivenProperties
        {
            MaxSpeedMultiplier = 1f,
            CombatMaxSpeedMultiplier = 0.9f,
            TopSpeedReachDuration = 2.5f,
            SwingSpeedMultiplier = 1f,
            ThrustOrRangedReadySpeedMultiplier = 1f,
            ReloadSpeed = 1f,
            MissileSpeedMultiplier = 1f,
            AIBlockOnDecideAbility = 0.9f,
            AIParryOnDecideAbility = 0.8f,
            AIAttackOnDecideChance = 0.5f,
            AiShooterError = 0.01f,
        };
        var effects = new RaceAbilityEffects
        {
            MoveSpeedPercent = 20f, AccelerationPercent = 25f, SwingSpeedPercent = 15f, DrawSpeedPercent = 25f,
            ReloadSpeedPercent = 15f, MissileSpeedPercent = 10f, BlockAbilityPercent = -60f, ParryAbilityPercent = -50f,
            AttackEagernessPercent = 20f, AimErrorPercent = -30f,
        };

        RaceAbilityStatApplier.Apply(props, effects);

        Assert.AreEqual(1.2f, props.MaxSpeedMultiplier, 0.0001f);
        Assert.AreEqual(1.08f, props.CombatMaxSpeedMultiplier, 0.0001f);
        Assert.AreEqual(2f, props.TopSpeedReachDuration, 0.0001f);
        Assert.AreEqual(1.15f, props.SwingSpeedMultiplier, 0.0001f);
        Assert.AreEqual(1.25f, props.ThrustOrRangedReadySpeedMultiplier, 0.0001f);
        Assert.AreEqual(1.15f, props.ReloadSpeed, 0.0001f);
        Assert.AreEqual(1.1f, props.MissileSpeedMultiplier, 0.0001f);
        Assert.AreEqual(0.36f, props.AIBlockOnDecideAbility, 0.0001f);
        Assert.AreEqual(0.4f, props.AIParryOnDecideAbility, 0.0001f);
        Assert.AreEqual(0.6f, props.AIAttackOnDecideChance, 0.0001f);
        Assert.AreEqual(0.007f, props.AiShooterError, 0.0001f);
    }
}
