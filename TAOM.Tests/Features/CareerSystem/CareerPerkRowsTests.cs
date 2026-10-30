using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.CareerSystem;
using TAOM.Features.CareerSystem.Diagnostics;
using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Tests.Features.CareerSystem;

/// <summary>
/// The decision code behind <c>taom.career_perks</c>' passive rows (#613, #769 E9, E10): which types get a
/// per-hit-mask breakdown and which get a per-class one. Moved out of the console command so it is reachable
/// by a test. The mask breakdown belongs to the types whose consumer passes a hit; the three troop counters
/// read no hit, so a breakdown by hit kind would describe a gate they do not have.
/// </summary>
[TestClass]
public class CareerPerkRowsTests
{
    private const string Hero = "hero1";

    private ICareerPassiveService _passives = null!;

    [TestInitialize]
    public void Setup() => _passives = Substitute.For<ICareerPassiveService>();

    private CareerPerkRow RowOf(PassiveEffectType type)
        => CareerPerkRows.Gather(_passives, Hero).Single(r => r.Type == type);

    private void Holds(PassiveEffectType type, float total) => _passives.GetPassiveMagnitude(Hero, type).Returns(total);

    [TestMethod]
    public void Gather_SkipsTypesTheHeroDoesNotHold()
    {
        Holds(PassiveEffectType.SwingSpeed, 0.05f);

        var rows = CareerPerkRows.Gather(_passives, Hero);

        Assert.AreEqual(1, rows.Count);
        Assert.AreEqual(PassiveEffectType.SwingSpeed, rows[0].Type);
        Assert.IsNull(rows[0].Masked);
        Assert.IsNull(rows[0].ByClass);
    }

    [DataTestMethod]
    [DataRow(PassiveEffectType.Damage)]
    [DataRow(PassiveEffectType.Resistance)]
    [DataRow(PassiveEffectType.TroopDamage)]
    [DataRow(PassiveEffectType.TroopResistance)]
    public void Gather_MaskGatedType_GetsAPerHitKindBreakdown(PassiveEffectType type)
    {
        Holds(type, 0.20f);
        _passives.GetMaskedMagnitude(Hero, type, Arg.Any<AttackTypeMask>()).Returns(0f);
        _passives.GetMaskedMagnitude(Hero, type, AttackTypeMask.Ranged).Returns(0.20f);

        var masked = RowOf(type).Masked;

        Assert.IsNotNull(masked);
        Assert.AreEqual(0.20f, masked!.Single(b => b.mask == AttackTypeMask.Ranged).magnitude);
        Assert.AreEqual(0f, masked.Single(b => b.mask == AttackTypeMask.Melee).magnitude);
    }

    [TestMethod]
    public void Gather_MaskGatedTypeThatReadsTheSameForEveryHit_CollapsesToOneAllEntry()
    {
        Holds(PassiveEffectType.Damage, 0.10f);
        _passives.GetMaskedMagnitude(Hero, PassiveEffectType.Damage, Arg.Any<AttackTypeMask>()).Returns(0.10f);

        var masked = RowOf(PassiveEffectType.Damage).Masked;

        Assert.AreEqual(1, masked!.Count);
        Assert.AreEqual(AttackTypeMask.All, masked[0].mask);
        Assert.AreEqual(0.10f, masked[0].magnitude, 0.0001f);
    }

    [DataTestMethod]
    [DataRow(PassiveEffectType.TroopFearResistance)]
    [DataRow(PassiveEffectType.TroopKnockdownResistance)]
    [DataRow(PassiveEffectType.TroopCrushHold)]
    public void Gather_TroopCounter_GetsNoHitKindBreakdownWhateverTheMaskedReadSays(PassiveEffectType type)
    {
        // The counters read no hit: a per-mask list would describe a gate they do not have.
        Holds(type, 0.30f);
        _passives.GetMaskedMagnitude(Hero, type, AttackTypeMask.Ranged).Returns(0.30f);

        Assert.IsNull(RowOf(type).Masked);
    }

    [DataTestMethod]
    [DataRow(PassiveEffectType.TroopDamage)]
    [DataRow(PassiveEffectType.TroopResistance)]
    [DataRow(PassiveEffectType.TroopFearResistance)]
    [DataRow(PassiveEffectType.TroopKnockdownResistance)]
    [DataRow(PassiveEffectType.TroopCrushHold)]
    public void Gather_ClassFilteredType_GetsAPerClassView(PassiveEffectType type)
    {
        Holds(type, 0.30f);
        _passives.GetTroopMagnitude(Hero, type, Arg.Any<TroopClassMask>(), Arg.Any<AttackTypeMask?>()).Returns(0.10f);
        _passives.GetTroopMagnitude(Hero, type, TroopClassMask.Infantry, Arg.Any<AttackTypeMask?>()).Returns(0.30f);

        var byClass = RowOf(type).ByClass;

        Assert.IsNotNull(byClass);
        Assert.AreEqual(0.30f, byClass!.Single(c => c.troopClass == TroopClassMask.Infantry).magnitude, 0.0001f);
        Assert.AreEqual(0.10f, byClass.Single(c => c.troopClass == TroopClassMask.None).magnitude, 0.0001f);
    }

    [TestMethod]
    public void Gather_HeroTypeThatReadsNoClass_GetsNoPerClassView()
    {
        Holds(PassiveEffectType.Damage, 0.10f);

        Assert.IsNull(RowOf(PassiveEffectType.Damage).ByClass);
    }

    [TestMethod]
    public void Gather_ClassView_AsksTheArmyWideAndTheHorseArcherReading()
    {
        // Army-wide is a troop with no class (only the All-class pips); a horse archer is Ranged and Cavalry at once.
        Holds(PassiveEffectType.TroopDamage, 0.20f);
        _passives.GetTroopMagnitude(Hero, PassiveEffectType.TroopDamage, TroopClassMask.None, Arg.Any<AttackTypeMask?>()).Returns(0.05f);
        _passives.GetTroopMagnitude(Hero, PassiveEffectType.TroopDamage, TroopClassMask.Ranged | TroopClassMask.Cavalry, Arg.Any<AttackTypeMask?>()).Returns(0.15f);

        var byClass = RowOf(PassiveEffectType.TroopDamage).ByClass!;

        Assert.AreEqual(0.05f, byClass.Single(c => c.troopClass == TroopClassMask.None).magnitude, 0.0001f);
        Assert.AreEqual(0.15f, byClass.Single(c => c.troopClass == (TroopClassMask.Ranged | TroopClassMask.Cavalry)).magnitude, 0.0001f);
    }
}
