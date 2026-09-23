using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.AdvancedCombat;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace TAOM.Tests.Features.AdvancedCombat;

/// <summary>
/// The flag composition <see cref="CustomAttacksUtils.TakeDamage"/> puts on a synthetic blow.
/// Extracted so the SignatureStrikes <c>extraFlags</c> extension is pinned without an engine:
/// the engine's own melee path grants <c>KnockBack</c> only to an unmounted human
/// (Mission.CreateMeleeBlow, v1.5.3 Mission.cs:5634), and a mounted victim gets
/// <c>CanDismount</c> in place of <c>KnockDown</c>, which is the rule every creature tree has
/// relied on since the elephant port.
/// </summary>
[TestClass]
public class CustomAttacksUtilsBlowFlagsTests
{
    [TestMethod]
    public void ComposeBlowFlags_KnockDownOnUnmountedVictim_IsKnockDown()
    {
        Assert.AreEqual(BlowFlags.KnockDown,
            CustomAttacksUtils.ComposeBlowFlags(knockDown: true, victimHasMount: false, BlowFlags.None));
    }

    [TestMethod]
    public void ComposeBlowFlags_KnockDownOnMountedVictim_IsCanDismount()
    {
        Assert.AreEqual(BlowFlags.CanDismount,
            CustomAttacksUtils.ComposeBlowFlags(knockDown: true, victimHasMount: true, BlowFlags.None));
    }

    [TestMethod]
    public void ComposeBlowFlags_NoKnockDownNoExtra_IsNone()
    {
        Assert.AreEqual(BlowFlags.None,
            CustomAttacksUtils.ComposeBlowFlags(knockDown: false, victimHasMount: false, BlowFlags.None));
    }

    [TestMethod]
    public void ComposeBlowFlags_ExtraKnockBackOnUnmountedVictim_IsKept()
    {
        Assert.AreEqual(BlowFlags.KnockBack,
            CustomAttacksUtils.ComposeBlowFlags(knockDown: false, victimHasMount: false, BlowFlags.KnockBack));
    }

    [TestMethod]
    public void ComposeBlowFlags_ExtraKnockBackOnMountedVictim_IsStripped()
    {
        Assert.AreEqual(BlowFlags.None,
            CustomAttacksUtils.ComposeBlowFlags(knockDown: false, victimHasMount: true, BlowFlags.KnockBack));
    }

    [TestMethod]
    public void ComposeBlowFlags_KnockDownAndExtraKnockBack_Combine()
    {
        Assert.AreEqual(BlowFlags.KnockDown | BlowFlags.KnockBack,
            CustomAttacksUtils.ComposeBlowFlags(knockDown: true, victimHasMount: false, BlowFlags.KnockBack));
    }

    [TestMethod]
    public void ComposeWeaponFlags_Blunt_CanKillEvenIfBlunt()
    {
        // A Blunt killing blow wounds instead of killing unless its weapon carries CanKillEvenIfBlunt
        // (DefaultPartyHealingModel.GetSurvivalChance, v1.5.3). A synthetic blow kills as the Pierce ones always did,
        // so a Blunt one carries the flag: the elk's antler charge (#636).
        Assert.AreEqual(WeaponFlags.CanKillEvenIfBlunt, CustomAttacksUtils.ComposeWeaponFlags(DamageTypes.Blunt));
    }

    [TestMethod]
    [DataRow(DamageTypes.Pierce)]
    [DataRow(DamageTypes.Cut)]
    public void ComposeWeaponFlags_NotBlunt_AddsNothing(DamageTypes damageType)
    {
        // The warg, spider, elephant, mumakil and ram blows and the signature strikes keep an empty weapon record.
        Assert.AreEqual((WeaponFlags)0, CustomAttacksUtils.ComposeWeaponFlags(damageType));
    }

    [TestMethod]
    public void TakeDamage_DamageTypeDefault_IsPierce()
    {
        // Every caller but the elk relies on the default, so changing it would re-type every creature blow at once.
        var parameter = typeof(CustomAttacksUtils).GetMethods()
            .Where(m => m.Name == nameof(CustomAttacksUtils.TakeDamage))
            .SelectMany(m => m.GetParameters())
            .Single(p => p.Name == "damageType");
        Assert.AreEqual(DamageTypes.Pierce, parameter.DefaultValue);
    }
}
