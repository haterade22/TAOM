using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TAOM.Features.CreatureBandits;
using TAOM.Features.CreatureBandits.Hooks;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.CreatureBandits;

/// <summary>
/// The three Patch93 wield guards ask <see cref="CreatureBanditAgents.Is"/> for every agent, on the engine's
/// worker threads as well as the main thread. Each guard is driven here on bare agents of every kind and must
/// answer as the predicate did before plan 032 reordered its reads.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class CreatureBanditWieldGuardTests
{
    private const string Brood = "taom_spider_brood_forest";   // a catalogue creature troop (CreatureBanditRulesTests)
    private readonly List<IntPtr> _flagBlocks = new();

    [TestCleanup]
    public void FreeFlagBlocks()
    {
        foreach (var block in _flagBlocks) Marshal.FreeHGlobal(block);
        _flagBlocks.Clear();
    }

    private static FieldInfo AgentField(string name)
    {
        var field = typeof(Agent).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(field, $"Agent.{name} is gone; the bare-agent fixture no longer matches the engine.");
        return field;
    }

    // FieldInfo.SetValue runs Agent's static constructor, which needs the native animation tables and throws
    // outside the game. A plain stfld, emitted here, writes the instance field without initialising the type.
    private static void SetAgentField(Agent agent, string name, object value)
    {
        var field = AgentField(name);
        var setter = new DynamicMethod("Set" + name, null, new[] { typeof(Agent), typeof(object) },
            typeof(CreatureBanditWieldGuardTests).Module, skipVisibility: true);
        var il = setter.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldarg_1);
        il.Emit(field.FieldType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, field.FieldType);
        il.Emit(OpCodes.Stfld, field);
        il.Emit(OpCodes.Ret);
        ((Action<Agent, object>)setter.CreateDelegate(typeof(Action<Agent, object>)))(agent, value);
    }

    // A bare agent whose IsHuman, Character and RiderAgent read what the row says. IsHuman dereferences
    // _flagsPointer (AgentHelper.GetAgentFlags), so every agent gets a real block of unmanaged memory.
    private Agent MakeAgent(AgentFlag flags, string? characterId, bool hasRider)
    {
        var agent = (Agent)FormatterServices.GetUninitializedObject(typeof(Agent));
        var block = Marshal.AllocHGlobal(sizeof(uint));
        _flagBlocks.Add(block);
        Marshal.WriteInt32(block, unchecked((int)(uint)flags));
        SetAgentField(agent, "_flagsPointer", new UIntPtr((ulong)block.ToInt64()));
        if (characterId != null)
        {
            var character = (BasicCharacterObject)FormatterServices.GetUninitializedObject(typeof(BasicCharacterObject));
            character.StringId = characterId;
            SetAgentField(agent, "_character", character);
        }
        if (hasRider)
            SetAgentField(agent, "_cachedRiderAgent", FormatterServices.GetUninitializedObject(typeof(Agent)));
        return agent;
    }

    // The predicate as it stood before plan 032: every cell must give the answer it gave.
    private static bool OldPredicate(AgentFlag flags, string? characterId, bool hasRider)
        => characterId != null
           && CreatureBanditRules.IsCreatureBandit(characterId, (flags & AgentFlag.IsHumanoid) != 0, hasRider);

    [DataTestMethod]
    [DataRow(AgentFlag.IsHumanoid | AgentFlag.CanWieldWeapon, "taom_test_soldier", false, false, DisplayName = "soldier")]
    [DataRow(AgentFlag.IsHumanoid, Brood, false, false, DisplayName = "husk rider of a creature troop")]
    [DataRow(AgentFlag.IsHumanoid, null, false, false, DisplayName = "half-built humanoid, no Character")]
    [DataRow(AgentFlag.Mountable, null, false, false, DisplayName = "ordinary riderless mount")]
    [DataRow(AgentFlag.Mountable, null, true, false, DisplayName = "ridden mount")]
    [DataRow(AgentFlag.CanAttack | AgentFlag.CanDefend, Brood, false, true, DisplayName = "creature bandit, route A applied")]
    [DataRow(AgentFlag.Mountable, Brood, false, true, DisplayName = "creature bandit, route A skipped")]
    [DataRow(AgentFlag.Mountable, Brood, true, false, DisplayName = "creature troop agent with a rider")]
    [DataRow(AgentFlag.Mountable, "taom_test_soldier", false, false, DisplayName = "non-humanoid with a non-creature Character")]
    public void EveryWieldGuard_EveryAgentKind_DecidesAsTheOldPredicate(AgentFlag flags, string? id, bool hasRider, bool creature)
    {
        Assert.AreEqual(creature, OldPredicate(flags, id, hasRider), "the row disagrees with the old predicate");
        var agent = MakeAgent(flags, id, hasRider);

        var primary = EquipmentIndex.Weapon2;
        Assert.AreEqual(!creature, Patch93_CreatureBanditPrimaryWieldGuard.Prefix(agent, ref primary), "primary: run vanilla");
        Assert.AreEqual(creature ? EquipmentIndex.None : EquipmentIndex.Weapon2, primary, "primary: result");

        var offhand = EquipmentIndex.Weapon2;
        Assert.AreEqual(!creature, Patch93_CreatureBanditOffhandWieldGuard.Prefix(agent, ref offhand), "offhand: run vanilla");
        Assert.AreEqual(creature ? EquipmentIndex.None : EquipmentIndex.Weapon2, offhand, "offhand: result");

        var range = 123f;
        Assert.AreEqual(!creature, Patch93_CreatureBanditMissileRangeGuard.Prefix(agent, ref range), "missile range: run vanilla");
        Assert.AreEqual(creature ? 0f : 123f, range, "missile range: result");
    }
}

/// <summary>
/// The engine-free half of the guard checks: no agent is built, so these run in hosted CI against the reference
/// assemblies. The IL pin proves a humanoid is ruled out by its flags before the troop id is read.
/// </summary>
[TestClass]
public class CreatureBanditAgentsTests
{
    [TestMethod]
    public void Is_NullAgent_IsFalse()
    {
        Assert.IsFalse(CreatureBanditAgents.Is(null));
    }

    [TestMethod]
    public void Is_RulesOutAHumanoidBeforeReadingTheTroopId()
    {
        var method = typeof(CreatureBanditAgents).GetMethod("Is", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.IsNotNull(method, "CreatureBanditAgents.Is is gone");

        var names = IlCallScanner.ExtractCalledMethods(method, method.GetMethodBody().GetILAsByteArray())
            .Select(m => m.Name)
            .ToList();

        var firstIsHuman = names.IndexOf("get_IsHuman");
        var firstStringId = names.IndexOf("get_StringId");
        Assert.IsTrue(firstIsHuman >= 0, "Is no longer reads IsHuman");
        Assert.IsTrue(firstStringId >= 0, "Is no longer reads the troop id");
        Assert.IsTrue(firstIsHuman < firstStringId,
            "a humanoid agent must be ruled out by its flags before the troop id is read");
    }
}
