using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.CreatureBandits;
using TaleWorlds.Core;

// Creature Bandits route A (#692): the one-shot scope around the spawner's own SpawnMonster call. While armed, the
// flags Mission.CreateAgent passes to the native creation call gain CanWieldWeapon (so the engine allocates the weapon
// state), once; the postfix then takes the Created state and strips the flag before the build. Every other agent,
// and every other thread, passes through untouched.

namespace TAOM.Tests.Features.CreatureBandits;

[TestClass]
public class CreatureWeaponStateScopeTests
{
    private const AgentFlag Spider = AgentFlag.Mountable | AgentFlag.CanCharge | AgentFlag.CanRear;

    [TestCleanup]
    public void Cleanup() => CreatureWeaponStateScope.Disarm();

    [TestMethod]
    public void OnCreationFlags_NotArmed_PassesThrough()
    {
        Assert.AreEqual(Spider, CreatureWeaponStateScope.OnCreationFlags(Spider));
        Assert.IsFalse(CreatureWeaponStateScope.TakeCreated());
    }

    [TestMethod]
    public void OnCreationFlags_Armed_AddsTheWeaponFlagOnce()
    {
        CreatureWeaponStateScope.Arm();

        Assert.AreEqual(Spider | AgentFlag.CanWieldWeapon, CreatureWeaponStateScope.OnCreationFlags(Spider));
        Assert.IsTrue(CreatureWeaponStateScope.WeaponFlagAtCreate);
        Assert.AreEqual(Spider, CreatureWeaponStateScope.OnCreationFlags(Spider), "a second agent in the same scope is untouched");
    }

    [TestMethod]
    public void TakeCreated_AfterCreation_ReturnsTrueOnce()
    {
        CreatureWeaponStateScope.Arm();
        CreatureWeaponStateScope.OnCreationFlags(Spider);

        Assert.IsTrue(CreatureWeaponStateScope.TakeCreated());
        Assert.IsFalse(CreatureWeaponStateScope.TakeCreated());
    }

    [TestMethod]
    public void TakeCreated_ArmedButNothingCreated_ReturnsFalse()
    {
        // The transpiler found no Flags read (engine drift): the postfix must not strip anything.
        CreatureWeaponStateScope.Arm();
        Assert.IsFalse(CreatureWeaponStateScope.TakeCreated());
        Assert.IsFalse(CreatureWeaponStateScope.WeaponFlagAtCreate);
    }

    [TestMethod]
    public void ReachedCreation_TellsAThrowBeforeTheNativeCallFromOneAfterIt()
    {
        // The spawner's catch asks it: a SpawnMonster that threw after the native call leaves a native agent behind.
        CreatureWeaponStateScope.Arm();
        Assert.IsFalse(CreatureWeaponStateScope.ReachedCreation, "armed, nothing created yet");
        CreatureWeaponStateScope.OnCreationFlags(Spider);
        Assert.IsTrue(CreatureWeaponStateScope.ReachedCreation, "created, postfix not run");
        CreatureWeaponStateScope.TakeCreated();
        Assert.IsTrue(CreatureWeaponStateScope.ReachedCreation, "created and stripped");
        CreatureWeaponStateScope.Disarm();
        Assert.IsFalse(CreatureWeaponStateScope.ReachedCreation);
    }

    [TestMethod]
    public void Disarm_ResetsTheScope()
    {
        CreatureWeaponStateScope.Arm();
        CreatureWeaponStateScope.Disarm();

        Assert.AreEqual(Spider, CreatureWeaponStateScope.OnCreationFlags(Spider));
        Assert.IsFalse(CreatureWeaponStateScope.WeaponFlagAtCreate);
    }

    [TestMethod]
    public void Arm_OnOneThread_DoesNotArmAnother()
    {
        CreatureWeaponStateScope.Arm();
        AgentFlag seenElsewhere = AgentFlag.None;
        var worker = new Thread(() => seenElsewhere = CreatureWeaponStateScope.OnCreationFlags(Spider));
        worker.Start();
        worker.Join();

        Assert.AreEqual(Spider, seenElsewhere, "an agent created on another thread is never touched");
    }

    [TestMethod]
    public void OnCreationFlags_ArmedHumanoid_Unchanged()
    {
        const AgentFlag human = AgentFlag.IsHumanoid | AgentFlag.CanAttack;
        CreatureWeaponStateScope.Arm();

        Assert.AreEqual(human, CreatureWeaponStateScope.OnCreationFlags(human));
        Assert.IsFalse(CreatureWeaponStateScope.WeaponFlagAtCreate);
    }
}
