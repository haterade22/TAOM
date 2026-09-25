using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.CultureDoctrine;
using TAOM.Features.CultureDoctrine.Domain;
using TaleWorlds.Core;

namespace TAOM.Tests.Features.CultureDoctrine;

[TestClass]
public class CultureMoraleTests
{
    [TestMethod]
    public void Vanilla_CanPanic_AndAddsNothing()
    {
        Assert.IsTrue(CultureMorale.Vanilla.CanPanic);
        Assert.AreEqual(50f, CultureMorale.Vanilla.InitialMorale(50f));
    }

    [TestMethod]
    public void NeverRout_CannotPanic()
        => Assert.IsFalse(new CultureMorale(neverRout: true, bravery: 0f).CanPanic);

    [TestMethod]
    public void Bravery_IsAddedToTheBaseMorale_AndTheCallerClamps()
    {
        Assert.AreEqual(65f, new CultureMorale(false, 15f).InitialMorale(50f));
        Assert.AreEqual(40f, new CultureMorale(false, -10f).InitialMorale(50f));
    }

    [TestMethod]
    public void Bravery_OutOfRangeOrNaN_IsZero()
    {
        Assert.AreEqual(0f, new CultureMorale(false, 31f).Bravery);
        Assert.AreEqual(0f, new CultureMorale(false, -31f).Bravery);
        Assert.AreEqual(0f, new CultureMorale(false, float.NaN).Bravery);
        Assert.AreEqual(30f, new CultureMorale(false, 30f).Bravery);
    }

    [TestMethod]
    public void NaNBaseMorale_PassesThroughUnchanged()
        => Assert.IsTrue(float.IsNaN(new CultureMorale(false, 15f).InitialMorale(float.NaN)));
}

[TestClass]
public class CultureAggressionTests
{
    [TestMethod]
    public void Vanilla_IsAllOnes()
    {
        Assert.IsTrue(CultureAggression.Vanilla.IsVanilla);
        Assert.IsFalse(new CultureAggression(1.5f, 1f, 1f, 1f).IsVanilla);
    }

    [TestMethod]
    public void OutOfRangeOrNaNMultipliers_RevertToOne()
    {
        var a = new CultureAggression(attack: 5f, shield: 0.1f, shooterError: float.NaN, chargeDistance: float.PositiveInfinity);
        Assert.AreEqual(1f, a.Attack);
        Assert.AreEqual(1f, a.Shield);
        Assert.AreEqual(1f, a.ShooterError);
        Assert.AreEqual(1f, a.ChargeDistance);
        Assert.AreEqual(4f, new CultureAggression(4f, 0.25f, 1f, 1f).Attack);
    }

    [TestMethod]
    public void Math_ScalesAndClampsToTheEngineRanges()
    {
        var orc = new CultureAggression(attack: 1.5f, shield: 0.7f, shooterError: 1.3f, chargeDistance: 1f);
        Assert.AreEqual(0.6f, AggressionMath.AttackChance(0.4f, orc), 1e-5f);
        Assert.AreEqual(1f, AggressionMath.AttackChance(0.9f, orc), "clamped to the engine's 1");
        Assert.AreEqual(0.7f, AggressionMath.ShieldDecision(1f, orc), 1e-5f);
        Assert.AreEqual(0.35f, AggressionMath.ShieldAgainstMissiles(0.5f, orc), 1e-5f);
        Assert.AreEqual(0.0104f, AggressionMath.Error(0.008f, orc), 1e-6f);
        Assert.AreEqual(-0.13f, AggressionMath.Error(-0.1f, orc), 1e-5f, "a negative lead error keeps its sign");
    }

    [TestMethod]
    public void Math_ClampsTheFloor()
    {
        var timid = new CultureAggression(attack: 0.25f, shield: 4f, shooterError: 0.25f, chargeDistance: 0.25f);
        Assert.AreEqual(0.05f, AggressionMath.AttackChance(0.1f, timid), 1e-5f, "the engine never goes below 0.05");
        Assert.AreEqual(2f, AggressionMath.ShieldDecision(1f, timid), 1e-5f, "the engine caps at 2");
        Assert.AreEqual(1f, AggressionMath.ShieldAgainstMissiles(0.5f, timid), 1e-5f);
    }

    [TestMethod]
    public void Math_NonFiniteEngineValue_ComesBackUnchanged()
    {
        var any = new CultureAggression(2f, 2f, 2f, 2f);
        Assert.IsTrue(float.IsNaN(AggressionMath.AttackChance(float.NaN, any)));
        Assert.IsTrue(float.IsNaN(AggressionMath.Error(float.NaN, any)));
        Assert.IsTrue(float.IsPositiveInfinity(AggressionMath.ChargeDistance(float.PositiveInfinity, any)));
    }
}

[TestClass]
[TestCategory("RequiresGame")]
public class FormationRoutingTests
{
    [TestMethod]
    public void ParsesTheEightRegularClasses_CaseInsensitively_AndNothingElse()
    {
        Assert.IsTrue(FormationRouting.TryParseClass("heavycavalry", out var c) && c == FormationClass.HeavyCavalry);
        Assert.IsTrue(FormationRouting.TryParseClass(" Skirmisher ", out c) && c == FormationClass.Skirmisher);
        Assert.IsFalse(FormationRouting.TryParseClass("NumberOfDefaultFormations", out _), "the enum alias for 4 is not a formation");
        Assert.IsFalse(FormationRouting.TryParseClass("4", out _));
        Assert.IsFalse(FormationRouting.TryParseClass("General", out _));
        Assert.IsFalse(FormationRouting.TryParseClass("Bodyguard", out _));
        Assert.IsFalse(FormationRouting.TryParseClass("", out _));
        Assert.IsFalse(FormationRouting.TryParseClass(null, out _));
    }

    [TestMethod]
    public void Routes_ByTroopId_Ordinal()
    {
        var routing = new FormationRouting(new System.Collections.Generic.Dictionary<string, FormationClass> { { "harad_mumakil_rider", FormationClass.HeavyCavalry } });
        Assert.IsTrue(routing.TryRoute("harad_mumakil_rider", out var c) && c == FormationClass.HeavyCavalry);
        Assert.IsFalse(routing.TryRoute("Harad_Mumakil_Rider", out _), "troop ids are exact");
        Assert.IsFalse(routing.TryRoute(null, out _));
        Assert.IsTrue(FormationRouting.None.IsEmpty);
    }

    [TestMethod]
    public void Rule_RoutedClassWins_AndDismountsWhereVanillaWould()
    {
        var routing = new FormationRouting(new System.Collections.Generic.Dictionary<string, FormationClass> { { "rider", FormationClass.HeavyCavalry } });
        Assert.AreEqual(FormationClass.HeavyCavalry, FormationRoutingRule.Apply(FormationClass.Cavalry, dismount: false, routing, "rider"));
        Assert.AreEqual(FormationClass.Cavalry, FormationRoutingRule.Apply(FormationClass.Cavalry, false, routing, "someone_else"));
        Assert.AreEqual(FormationClass.Infantry, FormationRoutingRule.Apply(FormationClass.Cavalry, dismount: true, routing, "someone_else"), "vanilla's dismount rule is reproduced");
        Assert.AreEqual(FormationClass.HeavyCavalry.DismountedClass(), FormationRoutingRule.Apply(FormationClass.Cavalry, dismount: true, routing, "rider"));
    }

    [TestMethod]
    public void Dismounts_MatchesVanillasCondition()
    {
        Assert.IsTrue(FormationRoutingRule.Dismounts(isSiege: true, false, false, false, BattleSideEnum.Defender));
        Assert.IsTrue(FormationRoutingRule.Dismounts(false, isNaval: true, false, false, BattleSideEnum.Defender));
        Assert.IsTrue(FormationRoutingRule.Dismounts(false, false, isNavalRaid: true, false, BattleSideEnum.Defender));
        Assert.IsTrue(FormationRoutingRule.Dismounts(false, false, false, isSallyOut: true, BattleSideEnum.Attacker));
        Assert.IsFalse(FormationRoutingRule.Dismounts(false, false, false, isSallyOut: true, BattleSideEnum.Defender));
        Assert.IsFalse(FormationRoutingRule.Dismounts(false, false, false, false, BattleSideEnum.Attacker));
    }
}

[TestClass]
public class CultureMoraleAndAggressionServiceTests
{
    private static (CultureMoraleService morale, CultureAggressionService aggression, ICultureDoctrineSettingsProvider settings) Build(bool moraleOn, bool aggressionOn)
    {
        var doctrine = new Doctrine("erebor", new TacticEntry[0], isDefault: false,
            new CultureMorale(neverRout: true, bravery: 15f), new CultureAggression(0.9f, 1.5f, 1f, 1f), FormationRouting.None);
        var catalog = new DoctrineCatalog(enabled: true, DoctrineCatalog.VanillaDefault(), new[] { doctrine });
        var config = Substitute.For<ICultureDoctrineConfigProvider>();
        config.GetCatalog().Returns(catalog);
        var settings = Substitute.For<ICultureDoctrineSettingsProvider>();
        settings.IsMoraleEnabled.Returns(moraleOn);
        settings.IsAggressionEnabled.Returns(aggressionOn);
        return (new CultureMoraleService(config, settings), new CultureAggressionService(config, settings), settings);
    }

    [TestMethod]
    public void Morale_On_AnswersFromTheCulturesRow()
    {
        var (morale, _, _) = Build(true, true);
        Assert.IsFalse(morale.CanPanic("erebor"));
        Assert.AreEqual(65f, morale.InitialMorale("erebor", 50f));
        Assert.IsTrue(morale.CanPanic("vlandia"), "no row: vanilla");
        Assert.IsTrue(morale.CanPanic(null));
        Assert.AreEqual(50f, morale.InitialMorale(null, 50f));
    }

    [TestMethod]
    public void Morale_Off_IsVanillaForEveryone()
    {
        var (morale, _, _) = Build(false, true);
        Assert.IsTrue(morale.CanPanic("erebor"));
        Assert.AreEqual(50f, morale.InitialMorale("erebor", 50f));
    }

    [TestMethod]
    public void Aggression_OnAndOff()
    {
        var (_, on, _) = Build(true, true);
        Assert.AreEqual(1.5f, on.Profile("erebor").Shield);
        Assert.IsTrue(on.Profile("nobody").IsVanilla);
        var (_, off, _) = Build(true, false);
        Assert.IsTrue(off.Profile("erebor").IsVanilla);
    }
}
