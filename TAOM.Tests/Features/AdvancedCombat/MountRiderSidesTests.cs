using System;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.AdvancedCombat;
using TaleWorlds.MountAndBlade;

namespace TAOM.Tests.Features.AdvancedCombat;

/// <summary>
/// The side a mount keeps after its rider is gone (#594). Bare uninitialized agents and teams: <c>Agent.Team</c> is
/// a plain auto-property and <c>Agent.MountAgent</c> reads the managed field <c>_cachedMountAgent</c> (v1.5.7
/// Agent.cs:518, :5324), so nothing here reaches native code.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class MountRiderSidesTests
{
    private static Agent BareAgent() => (Agent)FormatterServices.GetUninitializedObject(typeof(Agent));
    private static Team BareTeam() => (Team)FormatterServices.GetUninitializedObject(typeof(Team));

    private static Agent AgentOn(Team team)
    {
        var agent = BareAgent();
        typeof(Agent).GetProperty(nameof(Agent.Team))!.GetSetMethod(nonPublic: true)!.Invoke(agent, new object[] { team });
        return agent;
    }

    // FieldInfo.SetValue runs Agent's static constructor, which needs the native animation tables and throws
    // outside the game. A plain stfld, emitted here, writes the instance field without initialising the type
    // (the CreatureBanditWieldGuardTests pattern).
    private static void Seat(Agent rider, Agent mount)
    {
        var field = typeof(Agent).GetField("_cachedMountAgent", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field, "Agent._cachedMountAgent is gone (an engine update); Agent.MountAgent read it in v1.5.7.");
        var setter = new DynamicMethod("SetCachedMountAgent", null, new[] { typeof(Agent), typeof(Agent) },
            typeof(MountRiderSidesTests).Module, skipVisibility: true);
        var il = setter.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldarg_1);
        il.Emit(OpCodes.Stfld, field);
        il.Emit(OpCodes.Ret);
        ((Action<Agent, Agent>)setter.CreateDelegate(typeof(Action<Agent, Agent>)))(rider, mount);
    }

    [TestMethod]
    public void SideOf_NullAgent_IsNull() => Assert.IsNull(MountRiderSides.SideOf(null));

    [TestMethod]
    public void SideOf_LooseMountNeverRidden_IsNull() => Assert.IsNull(MountRiderSides.SideOf(BareAgent()));

    [TestMethod]
    public void SideOf_MountWithARecordedRider_IsThatRidersTeam()
    {
        var mount = BareAgent();
        var team = BareTeam();
        MountRiderSides.Record(mount, team);
        Assert.AreSame(team, MountRiderSides.SideOf(mount));
    }

    [TestMethod]
    public void Record_ALaterRider_ReplacesTheEarlierRidersTeam()
    {
        var mount = BareAgent();
        var first = BareTeam();
        var last = BareTeam();
        MountRiderSides.Record(mount, first);
        MountRiderSides.Record(mount, last);
        Assert.AreSame(last, MountRiderSides.SideOf(mount));
    }

    [TestMethod]
    public void Record_NullTeam_KeepsTheEarlierTeam()
    {
        var mount = BareAgent();
        var team = BareTeam();
        MountRiderSides.Record(mount, team);
        MountRiderSides.Record(mount, null);
        Assert.AreSame(team, MountRiderSides.SideOf(mount));
    }

    [TestMethod]
    public void Record_NullMount_DoesNotThrow() => MountRiderSides.Record(null, BareTeam());

    [TestMethod]
    public void SideOf_AgentWithItsOwnTeam_IsItsOwnTeamOverARecord()
    {
        var own = BareTeam();
        var agent = AgentOn(own);
        MountRiderSides.Record(agent, BareTeam());
        Assert.AreSame(own, MountRiderSides.SideOf(agent));
    }

    [TestMethod]
    public void RecordRider_RiderOnAMount_RecordsHisTeamAgainstTheMount()
    {
        var team = BareTeam();
        var rider = AgentOn(team);
        var mount = BareAgent();
        Seat(rider, mount);

        MountRiderSides.RecordRider(rider);

        Assert.AreSame(team, MountRiderSides.SideOf(mount));
    }

    [TestMethod]
    public void RecordRider_NullRider_DoesNotThrow() => MountRiderSides.RecordRider(null);
}
