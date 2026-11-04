using System;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TAOM.Features.LongDefeat.Hooks;
using TAOM.Tests.Migration;
using static TAOM.Tests.Infrastructure.RepoPaths;

namespace TAOM.Tests.Features.LongDefeat;

/// <summary>
/// The morale clause reaches only the armed Long Defeat mission's attackers. Agents are uninitialized instances with
/// their private-set Mission and Team written by reflection, since nothing here may start an engine mission.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class LongDefeatMoraleTests
{
    [ClassInitialize]
    public static void Init(Microsoft.VisualStudio.TestTools.UnitTesting.TestContext _) => GameAssemblies.EnsureLoaded();

    [TestCleanup]
    public void Clean() => LongDefeatMorale.Arm(null);

    private static Mission NewMission() => (Mission)FormatterServices.GetUninitializedObject(typeof(Mission));

    // FieldInfo.SetValue runs Agent's static constructor, which needs the native engine. A stfld emitted by hand
    // writes the instance field without that.
    private static void SetBacking(object target, string property, object? value)
    {
        var field = target.GetType().GetField($"<{property}>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var write = new DynamicMethod("set_" + property, null, new[] { typeof(object), typeof(object) }, typeof(LongDefeatMoraleTests).Module, true);
        var il = write.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Castclass, field.DeclaringType!);
        il.Emit(OpCodes.Ldarg_1);
        il.Emit(field.FieldType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, field.FieldType);
        il.Emit(OpCodes.Stfld, field);
        il.Emit(OpCodes.Ret);
        ((Action<object, object?>)write.CreateDelegate(typeof(Action<object, object?>)))(target, value);
    }

    private static Agent AgentOf(Mission? mission, BattleSideEnum? side)
    {
        var agent = (Agent)FormatterServices.GetUninitializedObject(typeof(Agent));
        SetBacking(agent, nameof(Agent.Mission), mission);
        if (side.HasValue)
        {
            var team = (Team)FormatterServices.GetUninitializedObject(typeof(Team));
            SetBacking(team, nameof(Team.Side), side.Value);
            SetBacking(agent, nameof(Agent.Team), team);
        }
        return agent;
    }

    [TestMethod]
    public void HoldsAttacker_NothingArmed_IsFalse()
    {
        Assert.IsFalse(LongDefeatMorale.HoldsAttacker(AgentOf(NewMission(), BattleSideEnum.Attacker)));
    }

    [TestMethod]
    public void HoldsAttacker_AttackerOfTheArmedMission_IsTrue()
    {
        var mission = NewMission();
        LongDefeatMorale.Arm(mission);

        var before = LongDefeatMorale.Fired.Passes;

        Assert.IsTrue(LongDefeatMorale.HoldsAttacker(AgentOf(mission, BattleSideEnum.Attacker)));
        Assert.AreEqual(before + 1, LongDefeatMorale.Fired.Passes, "a run can show the clause fired");
    }

    [TestMethod]
    public void HoldsAttacker_DefenderOfTheArmedMission_IsFalse()
    {
        var mission = NewMission();
        LongDefeatMorale.Arm(mission);

        var before = LongDefeatMorale.Fired.Passes;

        Assert.IsFalse(LongDefeatMorale.HoldsAttacker(AgentOf(mission, BattleSideEnum.Defender)));
        Assert.AreEqual(before, LongDefeatMorale.Fired.Passes);
    }

    [TestMethod]
    public void HoldsAttacker_AttackerOfAnotherMission_IsFalse()
    {
        LongDefeatMorale.Arm(NewMission());

        Assert.IsFalse(LongDefeatMorale.HoldsAttacker(AgentOf(NewMission(), BattleSideEnum.Attacker)));
    }

    [TestMethod]
    public void HoldsAttacker_NullAgent_IsFalse()
    {
        LongDefeatMorale.Arm(NewMission());

        Assert.IsFalse(LongDefeatMorale.HoldsAttacker(null));
    }

    [TestMethod]
    public void HoldsAttacker_AgentWithNoTeam_IsFalse()
    {
        var mission = NewMission();
        LongDefeatMorale.Arm(mission);

        Assert.IsFalse(LongDefeatMorale.HoldsAttacker(AgentOf(mission, null)));
    }

    [TestMethod]
    public void Disarm_TheArmedMission_StopsHolding()
    {
        var mission = NewMission();
        LongDefeatMorale.Arm(mission);

        LongDefeatMorale.Disarm(mission);

        Assert.IsFalse(LongDefeatMorale.HoldsAttacker(AgentOf(mission, BattleSideEnum.Attacker)));
    }

    [TestMethod]
    public void Disarm_AnotherMission_LeavesTheArmedOneHeld()
    {
        var armed = NewMission();
        LongDefeatMorale.Arm(armed);

        LongDefeatMorale.Disarm(NewMission());

        Assert.IsTrue(LongDefeatMorale.HoldsAttacker(AgentOf(armed, BattleSideEnum.Attacker)));
    }

    [TestMethod]
    public void Arm_ASecondMission_ReplacesTheFirst()
    {
        var first = NewMission();
        var second = NewMission();
        LongDefeatMorale.Arm(first);
        LongDefeatMorale.Arm(second);

        Assert.IsFalse(LongDefeatMorale.HoldsAttacker(AgentOf(first, BattleSideEnum.Attacker)));
        Assert.IsTrue(LongDefeatMorale.HoldsAttacker(AgentOf(second, BattleSideEnum.Attacker)));
    }

    [TestMethod]
    public void Model_PanicClauseAsksTheLongDefeatHoldFirst()
    {
        var src = ReadSource("Main/Features/CultureDoctrine/Models/TaomCustomBattleMoraleModel.cs", stripComments: true);
        var clause = src.IndexOf("!LongDefeatMorale.HoldsAttacker(agent)", System.StringComparison.Ordinal);
        var others = src.IndexOf("!CreatureBanditAgents.RefusesMoralePanic(agent)", System.StringComparison.Ordinal);

        Assert.IsTrue(clause >= 0, "CanPanicDueToMorale lost the Long Defeat clause");
        Assert.IsTrue(clause < others, "the Long Defeat clause must be the first term, so a held attacker never panics");
    }
}
