using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.RaceAbilities.Hooks;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

// Six shared models call these on every hit and every stat update. Until RaceAbilitiesModule sets the
// runtime, and in any process where it never does, each must hand its input back untouched and touch no
// engine object (the null agents below would throw if one did).

namespace TAOM.Tests.Features.RaceAbilities;

[TestClass]
public class RaceAbilityHooksTests
{
    private RaceAbilityRuntime? _saved;

    [TestInitialize]
    public void Setup()
    {
        _saved = RaceAbilityHooks.Runtime;
        RaceAbilityHooks.Runtime = null;
    }

    [TestCleanup]
    public void Cleanup() => RaceAbilityHooks.Runtime = _saved;

    [TestMethod]
    public void NoRuntime_TheResistancesPassThrough()
    {
        Assert.AreEqual(0.7f, RaceAbilityHooks.KnockDownResistance(null!, 0.7f));
        Assert.AreEqual(0.7f, RaceAbilityHooks.KnockBackResistance(null!, 0.7f));
        Assert.AreEqual(0.7f, RaceAbilityHooks.DismountResistance(null!, 0.7f));
    }

    [TestMethod]
    public void NoRuntime_TheCombatRulesHaveNoOpinion()
    {
        Assert.IsNull(RaceAbilityHooks.CrushVerdict(null!, null!, StrikeType.Swing, isPassiveUsage: false));
        Assert.IsFalse(RaceAbilityHooks.ShrugsOff(null!));
        Assert.IsFalse(RaceAbilityHooks.HoldsNerve(null!));
    }

    [TestMethod]
    public void NoRuntime_DamagePassesThrough()
    {
        var attack = default(AttackInformation);
        var collision = default(AttackCollisionData);

        Assert.AreEqual(42f, RaceAbilityHooks.AmplifyDamage(in attack, in collision, 42f));
        Assert.AreEqual(42f, RaceAbilityHooks.ReduceDamage(in attack, in collision, 42f));
    }

    [TestMethod]
    public void NoRuntime_TheStatPassDoesNothing() => RaceAbilityHooks.ApplyStats(null!, null!);
}
