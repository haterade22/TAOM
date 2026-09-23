using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.AdvancedCombat;
using TAOM.Features.ElephantLike.BehaviorTreeElements;
using TAOM.Tests.Migration;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace TAOM.Tests.Features.AdvancedCombat;

/// <summary>
/// Since #643 a creature's blow is owned by its rider, and the engine picks a weaponless blow's hit sound from its
/// owner (v1.5.3 <c>BlowWeaponRecord.GetHitSound</c>): the charge-damage sound for a mount, a punch or kick for a human.
/// Mike chose to keep the rider's credit and the creature's sound (2026-09-23), so the elephant-like hit asks
/// <see cref="CustomAttacksUtils.TakeDamage(Agent, Agent, int, float, bool, BlowFlags, DamageTypes, bool)"/> to silence
/// the engine's block and replay it with the charge sound. Engine calls, so the pins read the IL.
/// </summary>
[TestClass]
public class CreatureImpactSoundTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ComposeBlowFlags_NoSound_IsKeptForEveryVictim(bool victimHasMount)
    {
        // The silence must survive the composition, or the engine plays its punch as well as TAOM's charge sound.
        var flags = CustomAttacksUtils.ComposeBlowFlags(knockDown: true, victimHasMount, BlowFlags.NoSound);
        Assert.AreEqual(BlowFlags.NoSound, flags & BlowFlags.NoSound);
    }

    [TestMethod]
    public void TakeDamage_ChargeImpactSoundDefault_IsFalse()
    {
        // The warg, spider and signature strikes keep the engine's own sound choice.
        var parameter = typeof(CustomAttacksUtils).GetMethods()
            .Where(m => m.Name == nameof(CustomAttacksUtils.TakeDamage))
            .SelectMany(m => m.GetParameters())
            .Single(p => p.Name == "chargeImpactSound");
        Assert.AreEqual(false, parameter.DefaultValue);
    }

    [TestMethod]
    public void PlayChargeImpactSound_ReplaysTheEnginesSoundBlockWithTheChargeSound()
    {
        // Agent.HandleBlow's block (v1.5.3 :5466-5484): the sound with its "Armor Type" parameter, then the alarm.
        var play = AccessTools.Method(typeof(CustomAttacksUtils), "PlayChargeImpactSound");
        Assert.IsNotNull(play, "CustomAttacksUtils.PlayChargeImpactSound is gone.");
        var names = Called(play!).Select(m => m.Name).ToList();
        CollectionAssert.Contains(names, "get_SoundCodeMissionCombatChargeDamage");
        CollectionAssert.Contains(names, nameof(Agent.GetSoundParameterForArmorType));
        CollectionAssert.Contains(names, nameof(Mission.MakeSound));
        CollectionAssert.Contains(names, nameof(Mission.AddSoundAlarmFactorToAgents));
    }

    [TestMethod]
    public void TakeDamage_PlaysTheChargeImpact()
    {
        var takeDamage = typeof(CustomAttacksUtils).GetMethods()
            .Single(m => m.Name == nameof(CustomAttacksUtils.TakeDamage) && m.GetParameters().Any(p => p.Name == "damageType"));
        Assert.IsTrue(Called(takeDamage).Any(m => m.Name == "PlayChargeImpactSound"),
            "TakeDamage no longer replays the charge sound for a silenced creature blow.");
    }

    [TestMethod]
    public void ChargeImpactFlags_Asked_IsNoSound()
        => Assert.AreEqual(BlowFlags.NoSound, CustomAttacksUtils.ChargeImpactFlags(chargeImpactSound: true));

    [TestMethod]
    public void ChargeImpactFlags_NotAsked_IsNone()
        => Assert.AreEqual(BlowFlags.None, CustomAttacksUtils.ChargeImpactFlags(chargeImpactSound: false));

    [TestMethod]
    public void TakeDamage_SilencesTheEngineThroughChargeImpactFlags()
    {
        // Without the silence the engine plays its punch as well as TAOM's charge sound.
        var takeDamage = typeof(CustomAttacksUtils).GetMethods()
            .Single(m => m.Name == nameof(CustomAttacksUtils.TakeDamage) && m.GetParameters().Any(p => p.Name == "damageType"));
        Assert.IsTrue(Called(takeDamage).Any(m => m.Name == nameof(CustomAttacksUtils.ChargeImpactFlags)),
            "TakeDamage no longer marks a charge-impact blow NoSound.");
    }

    [TestMethod]
    public void ElephantLikeHit_AsksForTheChargeImpact()
    {
        // chargeImpactSound is TakeDamage's last parameter, so its value is the last push before the call: ldc.i4.1.
        var hit = AccessTools.Method(typeof(ElephantLikeAttackTaskBase), "Hit");
        Assert.IsNotNull(hit, "ElephantLikeAttackTaskBase.Hit is gone.");
        var il = hit!.GetMethodBody()!.GetILAsByteArray()!;
        var takeDamage = typeof(CustomAttacksUtils).GetMethods()
            .Single(m => m.Name == nameof(CustomAttacksUtils.TakeDamage) && m.GetParameters().Any(p => p.Name == "damageType"));
        int calls = 0;
        for (int i = 1; i + 4 < il.Length; i++)
        {
            if (il[i] != 0x28) continue; // call
            MethodBase? target;
            try { target = hit.Module.ResolveMethod(BitConverter.ToInt32(il, i + 1)); }
            catch (ArgumentException) { continue; }
            if (target != takeDamage) continue;
            calls++;
            Assert.AreEqual((byte)0x17, il[i - 1], "Hit no longer passes chargeImpactSound: true, so the creature's blow plays a punch.");
        }
        Assert.AreEqual(1, calls, "Hit should call TakeDamage exactly once.");
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
