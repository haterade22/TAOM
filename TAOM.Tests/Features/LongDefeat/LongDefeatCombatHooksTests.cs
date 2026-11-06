using System;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TAOM.Features.LongDefeat.Domain;
using TAOM.Features.LongDefeat.Hooks;
using TAOM.Tests.Infrastructure;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.LongDefeat;

/// <summary>
/// The damage factor reaches only blows of the armed Long Defeat mission's attackers, and nothing else: a Fords match, a Custom
/// Battle and a defender's blow all pass straight through. Agents are uninitialized instances with their private-set Mission and
/// Team written by reflection, as in <see cref="LongDefeatMoraleTests"/>, since nothing here may start an engine mission.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class LongDefeatCombatHooksTests
{
    [ClassInitialize]
    public static void Init(Microsoft.VisualStudio.TestTools.UnitTesting.TestContext _) => GameAssemblies.EnsureLoaded();

    [TestCleanup]
    public void Clean() => LongDefeatHookState.Arm(null);

    private static Mission NewMission() => (Mission)FormatterServices.GetUninitializedObject(typeof(Mission));

    // FieldInfo.SetValue runs Agent's static constructor, which needs the native engine; a stfld emitted by hand does not.
    private static void SetBacking(object target, string property, object? value)
    {
        var field = target.GetType().GetField($"<{property}>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var write = new DynamicMethod("set_" + property, null, new[] { typeof(object), typeof(object) }, typeof(LongDefeatCombatHooksTests).Module, true);
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

    private static AttackInformation Blow(Agent? attacker) => new() { AttackerAgent = attacker! };

    private static readonly AttackCollisionData Plain = default;

    [TestMethod]
    public void Amplify_AnAttackersOwnFall_IsUntouched_AndCountsNothing()
    {
        var mission = NewMission();
        LongDefeatHookState.Arm(mission);
        LongDefeatHookState.Publish(mission, new LdStatTable(2f));
        var faller = AgentOf(mission, BattleSideEnum.Attacker);
        var blow = Blow(faller);
        var fall = CollisionDataFixture.With(fallSpeed: 7f);

        Assert.IsTrue(fall.IsFallDamage, "the fixture must build a fall");
        Assert.AreEqual(10f, LongDefeatCombatHooks.Amplify(in blow, in fall, 10f), "the engine names the faller as its own attacker (Mission.cs:6151), and a fall is no blow");
        Assert.AreEqual(0L, LongDefeatHookState.Raised);
    }

    [TestMethod]
    public void Amplify_NothingArmed_PassesTheDamageThrough()
    {
        var mission = NewMission();
        LongDefeatHookState.Publish(mission, new LdStatTable(2f));

        var blow = Blow(AgentOf(mission, BattleSideEnum.Attacker));

        Assert.AreEqual(10f, LongDefeatCombatHooks.Amplify(in blow, in Plain, 10f));
    }

    [TestMethod]
    public void Amplify_AnAttackerOfTheArmedMission_ScalesTheBlow_AndCountsIt()
    {
        var mission = NewMission();
        LongDefeatHookState.Arm(mission);
        LongDefeatHookState.Publish(mission, new LdStatTable(1.5f));
        var blow = Blow(AgentOf(mission, BattleSideEnum.Attacker));

        Assert.AreEqual(15f, LongDefeatCombatHooks.Amplify(in blow, in Plain, 10f), 1e-4f);
        Assert.AreEqual(1L, LongDefeatHookState.Raised, "a run can show the seam ran");
    }

    [TestMethod]
    public void Amplify_FactorOne_ChangesNothing_AndCountsNothing()
    {
        var mission = NewMission();
        LongDefeatHookState.Arm(mission);
        LongDefeatHookState.Publish(mission, new LdStatTable(1f));
        var blow = Blow(AgentOf(mission, BattleSideEnum.Attacker));

        Assert.AreEqual(10f, LongDefeatCombatHooks.Amplify(in blow, in Plain, 10f));
        Assert.AreEqual(0L, LongDefeatHookState.Raised);
    }

    [TestMethod]
    public void Amplify_ADefenderOfTheArmedMission_IsUntouched()
    {
        var mission = NewMission();
        LongDefeatHookState.Arm(mission);
        LongDefeatHookState.Publish(mission, new LdStatTable(2f));
        var blow = Blow(AgentOf(mission, BattleSideEnum.Defender));

        Assert.AreEqual(10f, LongDefeatCombatHooks.Amplify(in blow, in Plain, 10f));
        Assert.AreEqual(0L, LongDefeatHookState.Raised);
    }

    [TestMethod]
    public void Amplify_AnAttackerOfAnotherMission_IsUntouched()
    {
        var armed = NewMission();
        LongDefeatHookState.Arm(armed);
        LongDefeatHookState.Publish(armed, new LdStatTable(2f));
        var blow = Blow(AgentOf(NewMission(), BattleSideEnum.Attacker));

        Assert.AreEqual(10f, LongDefeatCombatHooks.Amplify(in blow, in Plain, 10f));
    }

    [TestMethod]
    public void Amplify_NoAttacker_OrNoTeam_IsUntouched()
    {
        var mission = NewMission();
        LongDefeatHookState.Arm(mission);
        LongDefeatHookState.Publish(mission, new LdStatTable(2f));

        var none = Blow(null);
        var teamless = Blow(AgentOf(mission, null));

        Assert.AreEqual(10f, LongDefeatCombatHooks.Amplify(in none, in Plain, 10f));
        Assert.AreEqual(10f, LongDefeatCombatHooks.Amplify(in teamless, in Plain, 10f));
    }

    [TestMethod]
    public void Publish_FromAMissionThatIsNotArmed_ChangesNothing()
    {
        var armed = NewMission();
        LongDefeatHookState.Arm(armed);
        LongDefeatHookState.Publish(NewMission(), new LdStatTable(2f));
        var blow = Blow(AgentOf(armed, BattleSideEnum.Attacker));

        Assert.AreEqual(10f, LongDefeatCombatHooks.Amplify(in blow, in Plain, 10f));
    }

    [TestMethod]
    public void Publish_ReplacesTheFactorOfTheWaveBefore()
    {
        var mission = NewMission();
        LongDefeatHookState.Arm(mission);
        var blow = Blow(AgentOf(mission, BattleSideEnum.Attacker));

        LongDefeatHookState.Publish(mission, new LdStatTable(2f));
        LongDefeatHookState.Publish(mission, new LdStatTable(1.25f));

        Assert.AreEqual(12.5f, LongDefeatCombatHooks.Amplify(in blow, in Plain, 10f), 1e-4f);
    }

    [TestMethod]
    public void Publish_StartsTheCountOver_SoTheCountIsTheWaves()
    {
        var mission = NewMission();
        LongDefeatHookState.Arm(mission);
        var blow = Blow(AgentOf(mission, BattleSideEnum.Attacker));
        LongDefeatHookState.Publish(mission, new LdStatTable(2f));
        LongDefeatCombatHooks.Amplify(in blow, in Plain, 10f);
        LongDefeatCombatHooks.Amplify(in blow, in Plain, 10f);
        Assert.AreEqual(2L, LongDefeatHookState.Raised);

        LongDefeatHookState.Publish(mission, new LdStatTable(1.5f));

        Assert.AreEqual(0L, LongDefeatHookState.Raised);
    }

    [TestMethod]
    public void Disarm_TheArmedMission_StopsScaling_AndAReArmStartsAtFactorOne()
    {
        var mission = NewMission();
        LongDefeatHookState.Arm(mission);
        LongDefeatHookState.Publish(mission, new LdStatTable(2f));
        var blow = Blow(AgentOf(mission, BattleSideEnum.Attacker));

        LongDefeatHookState.Disarm(mission);
        Assert.AreEqual(10f, LongDefeatCombatHooks.Amplify(in blow, in Plain, 10f));

        LongDefeatHookState.Arm(mission);
        Assert.AreEqual(10f, LongDefeatCombatHooks.Amplify(in blow, in Plain, 10f), "a new mission never inherits the last run's factor");
    }

    [TestMethod]
    public void Disarm_AnotherMission_LeavesTheArmedOneScaled()
    {
        var armed = NewMission();
        LongDefeatHookState.Arm(armed);
        LongDefeatHookState.Publish(armed, new LdStatTable(2f));

        LongDefeatHookState.Disarm(NewMission());

        var blow = Blow(AgentOf(armed, BattleSideEnum.Attacker));
        Assert.AreEqual(20f, LongDefeatCombatHooks.Amplify(in blow, in Plain, 10f), 1e-4f);
    }
}
