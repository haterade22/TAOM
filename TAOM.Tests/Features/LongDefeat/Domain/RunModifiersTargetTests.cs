using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Tests.Features.LongDefeat.Domain;

/// <summary>
/// What the troop and class boons owned fold into (phase2-plan.md section 2, "Boons"): a stat record, a damage factor and a damage-taken factor for each of
/// the defenders' three groups and for the lord, and a health factor for each. A defender's group comes from its character's <c>IsMounted</c> and <c>IsRanged</c>;
/// the lord's body takes the player record only. Pure.
/// </summary>
[TestClass]
public class RunModifiersTargetTests
{
    private static BoonRow Boon(string id, params (string Key, float Value, BoonTarget Target)[] effects) =>
        new(id, BoonCategory.Infantry, "{=K_" + id + "}" + id, "{=T_" + id + "}text", effects.Select(e => new BoonEffect(e.Key, e.Value, e.Target)).ToList());

    private static BoonRow WithDrawback(BoonRow row, params (string Key, float Value, BoonTarget Target)[] drawback) =>
        row with { Drawback = drawback.Select(e => new BoonEffect(e.Key, e.Value, e.Target)).ToList(), DrawbackText = "{=D}drawback" };

    private static LdStatTable Table(params BoonRow[] owned) => RunModifiers.StatTable(AffixPlan.None, new string[0], 1f, owned);

    [DataTestMethod]
    [DataRow(false, false, "Infantry")]
    [DataRow(false, true, "Archers")]
    [DataRow(true, false, "Cavalry")]
    [DataRow(true, true, "Cavalry")]
    public void ACharactersGroup_IsCavalryIfMounted_ElseArchersIfRanged_ElseInfantry(bool mounted, bool ranged, string expected)
    {
        Assert.AreEqual(expected, BoonTargets.OfCharacter(mounted, ranged).ToString());
    }

    [TestMethod]
    public void NoBoons_LeaveEveryTargetWithNoRecordAndFactorsOfOne()
    {
        var table = Table();

        foreach (var target in BoonTargets.All)
        {
            Assert.IsNull(table.For(target).Stats, target + " keeps the stat hook inert");
            Assert.AreEqual(1f, table.For(target).Damage, target.ToString());
            Assert.AreEqual(1f, table.For(target).DamageTaken, target.ToString());
        }
    }

    [TestMethod]
    public void AStatBoon_FillsItsTargetsRecordOnly()
    {
        var table = Table(Boon("swords", (BoonKeys.SwingSpeedPercent, 12f, BoonTarget.Infantry)));

        Assert.AreEqual(12f, table.Infantry.Stats!.SwingSpeedPercent);
        Assert.IsNull(table.Archers.Stats);
        Assert.IsNull(table.Cavalry.Stats);
        Assert.IsNull(table.Player.Stats, "the lord takes the player record only, never a troop group's");
    }

    [TestMethod]
    public void EveryStatKey_LandsOnTheFieldTheEngineApplierReads()
    {
        var table = Table(Boon("all",
            (BoonKeys.SwingSpeedPercent, 1f, BoonTarget.Archers), (BoonKeys.MoveSpeedPercent, 2f, BoonTarget.Archers), (BoonKeys.DrawSpeedPercent, 3f, BoonTarget.Archers),
            (BoonKeys.ReloadSpeedPercent, 4f, BoonTarget.Archers), (BoonKeys.BlockPercent, 5f, BoonTarget.Archers), (BoonKeys.ParryPercent, 6f, BoonTarget.Archers),
            (BoonKeys.AimErrorPercent, -7f, BoonTarget.Archers)));

        var fx = table.Archers.Stats!;
        Assert.AreEqual(1f, fx.SwingSpeedPercent);
        Assert.AreEqual(2f, fx.MoveSpeedPercent);
        Assert.AreEqual(3f, fx.DrawSpeedPercent);
        Assert.AreEqual(4f, fx.ReloadSpeedPercent);
        Assert.AreEqual(5f, fx.BlockAbilityPercent);
        Assert.AreEqual(6f, fx.ParryAbilityPercent);
        Assert.AreEqual(-7f, fx.AimErrorPercent);
    }

    [TestMethod]
    public void StatPercents_SumWithinATarget_DrawbacksIncluded_AndStayApartAcrossTargets()
    {
        var table = Table(
            WithDrawback(Boon("a", (BoonKeys.MoveSpeedPercent, 10f, BoonTarget.Infantry)), (BoonKeys.MoveSpeedPercent, -4f, BoonTarget.Infantry)),
            Boon("b", (BoonKeys.MoveSpeedPercent, 5f, BoonTarget.Infantry), (BoonKeys.MoveSpeedPercent, 20f, BoonTarget.Archers)));

        Assert.AreEqual(11f, table.Infantry.Stats!.MoveSpeedPercent, 1e-4f);
        Assert.AreEqual(20f, table.Archers.Stats!.MoveSpeedPercent, 1e-4f);
    }

    [TestMethod]
    public void TheMountedRecords_CopyMoveSpeedIntoMountSpeed_TheFootOnesDoNot()
    {
        var table = Table(Boon("fast",
            (BoonKeys.MoveSpeedPercent, 12f, BoonTarget.Cavalry), (BoonKeys.MoveSpeedPercent, 8f, BoonTarget.Player), (BoonKeys.MoveSpeedPercent, 9f, BoonTarget.Infantry)));

        Assert.AreEqual(12f, table.Cavalry.Stats!.MountSpeedPercent, "a horse reads MountSpeedPercent, which ApplyMount applies");
        Assert.AreEqual(8f, table.Player.Stats!.MountSpeedPercent);
        Assert.AreEqual(0f, table.Infantry.Stats!.MountSpeedPercent);
    }

    [TestMethod]
    public void DamageAndDamageTaken_AreOneFactorPerTarget_1PlusTheSumOver100()
    {
        var table = Table(
            Boon("a", (BoonKeys.DamagePercent, 15f, BoonTarget.Infantry), (BoonKeys.DamageTakenPercent, -10f, BoonTarget.Player)),
            WithDrawback(Boon("b", (BoonKeys.DamagePercent, 10f, BoonTarget.Infantry)), (BoonKeys.DamageTakenPercent, 20f, BoonTarget.Player)));

        Assert.AreEqual(1.25f, table.Infantry.Damage, 1e-5f);
        Assert.AreEqual(1f, table.Infantry.DamageTaken);
        Assert.AreEqual(1.1f, table.Player.DamageTaken, 1e-5f);
        Assert.AreEqual(1f, table.Archers.Damage);
        Assert.IsNull(table.Infantry.Stats, "a combat-only boon needs no stat refresh");
    }

    [TestMethod]
    public void ADamageTakenFactor_NeverGoesBelowZero()
    {
        var table = Table(Boon("a", (BoonKeys.DamageTakenPercent, -60f, BoonTarget.Cavalry)), Boon("b", (BoonKeys.DamageTakenPercent, -60f, BoonTarget.Cavalry)));

        Assert.AreEqual(0f, table.Cavalry.DamageTaken);
    }

    [TestMethod]
    public void EachCall_BuildsFreshRecords_SoATableTheEngineHoldsIsNeverChanged()
    {
        var owned = new[] { Boon("a", (BoonKeys.SwingSpeedPercent, 10f, BoonTarget.Infantry)) };

        var first = RunModifiers.StatTable(AffixPlan.None, new string[0], 1f, owned);
        var second = RunModifiers.StatTable(AffixPlan.None, new string[0], 1f, owned);

        Assert.AreNotSame(first.Infantry.Stats, second.Infantry.Stats);
    }

    [TestMethod]
    public void ABoonsEffectsAimedAtOneTarget_NeverTouchTheAttackersRecord()
    {
        var table = Table(Boon("a", (BoonKeys.SwingSpeedPercent, 10f, BoonTarget.Infantry)));

        Assert.IsNull(table.Attackers);
        Assert.AreEqual(1f, table.AttackerDamage);
    }

    [TestMethod]
    public void HealthFactor_IsTheProductOfEachBoonsOwnFactor_ForItsTargetOnly()
    {
        var owned = new[]
        {
            Boon("a", (BoonKeys.HealthPercent, 20f, BoonTarget.Infantry)),
            Boon("b", (BoonKeys.HealthPercent, 10f, BoonTarget.Infantry), (BoonKeys.HealthPercent, 50f, BoonTarget.Player)),
        };

        Assert.AreEqual(1.32f, RunModifiers.HealthFactor(owned, BoonTarget.Infantry), 1e-5f, "1.2 times 1.1, so a standing soldier scaled by each boon's own factor agrees with a new spawn");
        Assert.AreEqual(1.5f, RunModifiers.HealthFactor(owned, BoonTarget.Player), 1e-5f);
        Assert.AreEqual(1f, RunModifiers.HealthFactor(owned, BoonTarget.Cavalry));
        Assert.AreEqual(1f, RunModifiers.HealthFactor(Array.Empty<BoonRow>(), BoonTarget.Archers));
    }

    [TestMethod]
    public void AHealthDrawback_LowersTheBoonsOwnFactor()
    {
        var boon = WithDrawback(Boon("a", (BoonKeys.HealthPercent, 40f, BoonTarget.Player)), (BoonKeys.HealthPercent, -15f, BoonTarget.Player));

        Assert.AreEqual(1.4f * 0.85f, RunModifiers.OwnHealthFactor(boon, BoonTarget.Player), 1e-5f);
    }

    [TestMethod]
    public void TheTargetsABoonTouches_SplitIntoStatsAndHealth()
    {
        var boon = Boon("mixed",
            (BoonKeys.MoveSpeedPercent, 5f, BoonTarget.Cavalry), (BoonKeys.HealthPercent, 10f, BoonTarget.Infantry), (BoonKeys.DamagePercent, 10f, BoonTarget.Archers));

        CollectionAssert.AreEqual(new[] { BoonTarget.Cavalry }, RunModifiers.StatTargets(boon).ToArray(), "damage is read at the blow and needs no refresh");
        CollectionAssert.AreEqual(new[] { BoonTarget.Infantry }, RunModifiers.HealthTargets(boon).ToArray());
    }

    [TestMethod]
    public void ARowWithNoTarget_ForATargetedKey_ChangesNothing()
    {
        var table = RunModifiers.StatTable(AffixPlan.None, new string[0], 1f,
            new[] { new BoonRow("x", BoonCategory.Infantry, "{=K}x", "{=T}t", new[] { new BoonEffect(BoonKeys.SwingSpeedPercent, 10f) }) });

        foreach (var target in BoonTargets.All)
            Assert.IsNull(table.For(target).Stats, target.ToString());
    }
}
