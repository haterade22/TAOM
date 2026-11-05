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
using TAOM.Features.LongDefeat.Diagnostics;
using TAOM.Features.LongDefeat.Domain;
using TAOM.Features.LongDefeat.Hooks;
using TAOM.Features.RaceAbilities.Domain;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.LongDefeat;

/// <summary>
/// The stat and armour hooks of the Long Defeat mission: inert unless armed and unless the agent belongs to the armed mission's attackers,
/// a horse takes its rider's record (the mount speed only), a riderless horse nothing, and armour is raised only for a human attacker built from
/// real build data. Agents are uninitialized instances with their private-set fields written by emitted stfld, as in
/// <see cref="LongDefeatCombatHooksTests"/> and the creature-bandit guard tests, since nothing here may start an engine mission.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class LongDefeatStatHooksTests
{
    private readonly List<IntPtr> _flagBlocks = new();

    [ClassInitialize]
    public static void Init(Microsoft.VisualStudio.TestTools.UnitTesting.TestContext _) => GameAssemblies.EnsureLoaded();

    [TestCleanup]
    public void Clean()
    {
        LongDefeatStatHooks.Arm(null);
        foreach (var block in _flagBlocks)
            Marshal.FreeHGlobal(block);
        _flagBlocks.Clear();
    }

    private static Mission NewMission() => (Mission)FormatterServices.GetUninitializedObject(typeof(Mission));

    private static void Stfld(object target, FieldInfo field, object? value)
    {
        var write = new DynamicMethod("set_" + field.Name, null, new[] { typeof(object), typeof(object) }, typeof(LongDefeatStatHooksTests).Module, true);
        var il = write.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Castclass, field.DeclaringType!);
        il.Emit(OpCodes.Ldarg_1);
        il.Emit(field.FieldType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, field.FieldType);
        il.Emit(OpCodes.Stfld, field);
        il.Emit(OpCodes.Ret);
        ((Action<object, object?>)write.CreateDelegate(typeof(Action<object, object?>)))(target, value);
    }

    private static void SetBacking(object target, string property, object? value) =>
        Stfld(target, target.GetType().GetField($"<{property}>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!, value);

    private static void SetField(Agent agent, string name, object? value) =>
        Stfld(agent, typeof(Agent).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!, value);

    private static Team TeamOf(BattleSideEnum side)
    {
        var team = (Team)FormatterServices.GetUninitializedObject(typeof(Team));
        SetBacking(team, nameof(Team.Side), side);
        return team;
    }

    // IsMount and IsHuman read the agent's flag word through a pointer, so each agent owns a real block of memory.
    private Agent AgentOf(Mission? mission, BattleSideEnum? side, AgentFlag flags, Agent? rider = null)
    {
        var agent = (Agent)FormatterServices.GetUninitializedObject(typeof(Agent));
        var block = Marshal.AllocHGlobal(sizeof(uint));
        _flagBlocks.Add(block);
        Marshal.WriteInt32(block, unchecked((int)(uint)flags));
        SetField(agent, "_flagsPointer", new UIntPtr((ulong)block.ToInt64()));
        SetBacking(agent, nameof(Agent.Mission), mission);
        if (side.HasValue)
            SetBacking(agent, nameof(Agent.Team), TeamOf(side.Value));
        if (rider != null)
            SetField(agent, "_cachedRiderAgent", rider);
        return agent;
    }

    private static AgentBuildData BuildData(BattleSideEnum? side)
    {
        var data = (AgentBuildData)FormatterServices.GetUninitializedObject(typeof(AgentBuildData));
        if (side.HasValue)
            data.Team(TeamOf(side.Value));
        return data;
    }

    private static AgentDrivenProperties Props() => new()
    {
        SwingSpeedMultiplier = 1f,
        MaxSpeedMultiplier = 1f,
        MountSpeed = 1f,
        ArmorHead = 10f,
        ArmorTorso = 20f,
        ArmorLegs = 30f,
        ArmorArms = 40f,
    };

    private static LdStatTable Swing(float percent) => new(1f, new RaceAbilityEffects { SwingSpeedPercent = percent, MoveSpeedPercent = 10f, MountSpeedPercent = 10f });

    private const AgentFlag Human = AgentFlag.IsHumanoid;
    private const AgentFlag Horse = AgentFlag.Mountable;

    [TestMethod]
    public void UpdateStats_NothingArmed_ChangesNothing()
    {
        var mission = NewMission();
        LongDefeatStatHooks.Publish(mission, Swing(50f));
        var props = Props();

        LongDefeatStatHooks.UpdateStats(AgentOf(mission, BattleSideEnum.Attacker, Human), props);

        Assert.AreEqual(1f, props.SwingSpeedMultiplier);
    }

    [TestMethod]
    public void UpdateStats_AnAttackerOfTheArmedMission_TakesTheRecord()
    {
        var mission = NewMission();
        LongDefeatStatHooks.Arm(mission);
        LongDefeatStatHooks.Publish(mission, Swing(15f));
        var props = Props();

        LongDefeatStatHooks.UpdateStats(AgentOf(mission, BattleSideEnum.Attacker, Human), props);

        Assert.AreEqual(1.15f, props.SwingSpeedMultiplier, 1e-4f);
        Assert.AreEqual(1.10f, props.MaxSpeedMultiplier, 1e-4f);
        Assert.AreEqual(1f, props.MountSpeed, "a soldier on foot takes Apply, never ApplyMount");
    }

    [TestMethod]
    public void UpdateStats_ADefender_IsUntouched()
    {
        var mission = NewMission();
        LongDefeatStatHooks.Arm(mission);
        LongDefeatStatHooks.Publish(mission, Swing(15f));
        var props = Props();

        LongDefeatStatHooks.UpdateStats(AgentOf(mission, BattleSideEnum.Defender, Human), props);

        Assert.AreEqual(1f, props.SwingSpeedMultiplier);
    }

    [TestMethod]
    public void UpdateStats_AnAgentOfAnotherMission_IsUntouched()
    {
        var mission = NewMission();
        LongDefeatStatHooks.Arm(mission);
        LongDefeatStatHooks.Publish(mission, Swing(15f));
        var props = Props();

        LongDefeatStatHooks.UpdateStats(AgentOf(NewMission(), BattleSideEnum.Attacker, Human), props);

        Assert.AreEqual(1f, props.SwingSpeedMultiplier, "a Fords match or another Custom Battle must pass through");
    }

    [TestMethod]
    public void UpdateStats_NoRecordInTheTable_IsUntouched()
    {
        var mission = NewMission();
        LongDefeatStatHooks.Arm(mission);
        LongDefeatStatHooks.Publish(mission, new LdStatTable(1.5f));
        var props = Props();

        LongDefeatStatHooks.UpdateStats(AgentOf(mission, BattleSideEnum.Attacker, Human), props);

        Assert.AreEqual(1f, props.SwingSpeedMultiplier);
    }

    [TestMethod]
    public void UpdateStats_AHorseWithAnAttackerRider_TakesTheRidersMountSpeedOnly()
    {
        var mission = NewMission();
        LongDefeatStatHooks.Arm(mission);
        LongDefeatStatHooks.Publish(mission, Swing(15f));
        var rider = AgentOf(mission, BattleSideEnum.Attacker, Human);
        var horse = AgentOf(mission, null, Horse, rider);
        var props = Props();

        LongDefeatStatHooks.UpdateStats(horse, props);

        Assert.AreEqual(1.10f, props.MountSpeed, 1e-4f, "the horse's stats go through its rider's record");
        Assert.AreEqual(1f, props.SwingSpeedMultiplier, "a horse has no swing");
        Assert.AreEqual(1f, props.MaxSpeedMultiplier);
    }

    [TestMethod]
    public void UpdateStats_AHorseWithADefenderRider_IsUntouched()
    {
        var mission = NewMission();
        LongDefeatStatHooks.Arm(mission);
        LongDefeatStatHooks.Publish(mission, Swing(15f));
        var rider = AgentOf(mission, BattleSideEnum.Defender, Human);
        var props = Props();

        LongDefeatStatHooks.UpdateStats(AgentOf(mission, null, Horse, rider), props);

        Assert.AreEqual(1f, props.MountSpeed);
    }

    [TestMethod]
    public void UpdateStats_ARiderlessHorse_IsUntouched()
    {
        var mission = NewMission();
        LongDefeatStatHooks.Arm(mission);
        LongDefeatStatHooks.Publish(mission, Swing(15f));
        var props = Props();

        LongDefeatStatHooks.UpdateStats(AgentOf(mission, null, Horse), props);

        Assert.AreEqual(1f, props.MountSpeed, "its own Character is null and it is nobody's mount (RaceAbilityHooks.ApplyStats does the same)");
    }

    [TestMethod]
    public void Disarm_ForAnotherMission_KeepsTheTable_AndForTheArmedOneClearsIt()
    {
        var mission = NewMission();
        LongDefeatStatHooks.Arm(mission);
        LongDefeatStatHooks.Publish(mission, Swing(15f));

        LongDefeatStatHooks.Disarm(NewMission());
        var kept = Props();
        LongDefeatStatHooks.UpdateStats(AgentOf(mission, BattleSideEnum.Attacker, Human), kept);
        LongDefeatStatHooks.Disarm(mission);
        var gone = Props();
        LongDefeatStatHooks.UpdateStats(AgentOf(mission, BattleSideEnum.Attacker, Human), gone);

        Assert.AreEqual(1.15f, kept.SwingSpeedMultiplier, 1e-4f);
        Assert.AreEqual(1f, gone.SwingSpeedMultiplier);
    }

    [TestMethod]
    public void Publish_FromAnUnarmedMission_IsIgnored()
    {
        var mission = NewMission();
        LongDefeatStatHooks.Arm(mission);
        LongDefeatStatHooks.Publish(NewMission(), Swing(15f));
        var props = Props();

        LongDefeatStatHooks.UpdateStats(AgentOf(mission, BattleSideEnum.Attacker, Human), props);

        Assert.AreEqual(1f, props.SwingSpeedMultiplier);
    }

    [TestMethod]
    public void Baseline_RunsTheHookAsIfNothingWerePublished_OnThisThreadOnly()
    {
        var mission = NewMission();
        LongDefeatStatHooks.Arm(mission);
        LongDefeatStatHooks.Publish(mission, Swing(15f));
        var inside = Props();
        var after = Props();
        var agent = AgentOf(mission, BattleSideEnum.Attacker, Human);

        LongDefeatStatHooks.Baseline(() => LongDefeatStatHooks.UpdateStats(agent, inside));
        LongDefeatStatHooks.UpdateStats(agent, after);

        Assert.AreEqual(1f, inside.SwingSpeedMultiplier, "the effects adapter reads a baseline this way, so the published record is left out");
        Assert.AreEqual(1.15f, after.SwingSpeedMultiplier, 1e-4f, "and the next ordinary call sees the record again");
    }

    [TestMethod]
    public void InitializeArmour_AnAttackerOfAHideWave_RaisesTheFourArmourValues()
    {
        var mission = NewMission();
        LongDefeatStatHooks.Arm(mission);
        LongDefeatStatHooks.Publish(mission, new LdStatTable(1f, null, 30f));
        var props = Props();

        LongDefeatStatHooks.InitializeArmour(AgentOf(mission, BattleSideEnum.Attacker, Human), props, BuildData(BattleSideEnum.Attacker));

        Assert.AreEqual(13f, props.ArmorHead, 1e-3f);
        Assert.AreEqual(26f, props.ArmorTorso, 1e-3f);
        Assert.AreEqual(39f, props.ArmorLegs, 1e-3f);
        Assert.AreEqual(52f, props.ArmorArms, 1e-3f);
    }

    [TestMethod]
    public void InitializeArmour_NullBuildData_IsUntouched()
    {
        var mission = NewMission();
        LongDefeatStatHooks.Arm(mission);
        LongDefeatStatHooks.Publish(mission, new LdStatTable(1f, null, 30f));
        var props = Props();

        LongDefeatStatHooks.InitializeArmour(AgentOf(mission, BattleSideEnum.Attacker, Human), props, null);

        Assert.AreEqual(10f, props.ArmorHead, "SpawnMonster passes null build data, and the model is global to every Custom Battle");
    }

    [TestMethod]
    public void InitializeArmour_AHorse_IsUntouched()
    {
        var mission = NewMission();
        LongDefeatStatHooks.Arm(mission);
        LongDefeatStatHooks.Publish(mission, new LdStatTable(1f, null, 30f));
        var props = Props();

        LongDefeatStatHooks.InitializeArmour(AgentOf(mission, BattleSideEnum.Attacker, Horse), props, BuildData(BattleSideEnum.Attacker));

        Assert.AreEqual(10f, props.ArmorHead, "a horse is built with its rider's build data and takes no human armour");
    }

    [TestMethod]
    public void InitializeArmour_ADefenderBuild_IsUntouched()
    {
        var mission = NewMission();
        LongDefeatStatHooks.Arm(mission);
        LongDefeatStatHooks.Publish(mission, new LdStatTable(1f, null, 30f));
        var props = Props();

        LongDefeatStatHooks.InitializeArmour(AgentOf(mission, BattleSideEnum.Defender, Human), props, BuildData(BattleSideEnum.Defender));

        Assert.AreEqual(10f, props.ArmorHead);
    }

    [TestMethod]
    public void InitializeArmour_AnInvalidTeamBuild_IsUntouched()
    {
        var mission = NewMission();
        LongDefeatStatHooks.Arm(mission);
        LongDefeatStatHooks.Publish(mission, new LdStatTable(1f, null, 30f));
        var props = Props();

        LongDefeatStatHooks.InitializeArmour(AgentOf(mission, null, Human), props, BuildData(null));

        Assert.AreEqual(10f, props.ArmorHead);
    }

    private static AffixPlan ShippedNumbers() => new(
        new[] { new AffixStage(1, 3) },
        new Dictionary<string, IReadOnlyDictionary<string, float>>
        {
            ["lash"] = new Dictionary<string, float> { ["swingSpeedPercent"] = 15f, ["attackEagernessPercent"] = 25f },
            ["drums"] = new Dictionary<string, float> { ["moveSpeedPercent"] = 12f, ["accelerationPercent"] = 25f },
            ["eye"] = new Dictionary<string, float>
            {
                ["aimErrorPercent"] = -30f, ["drawSpeedPercent"] = 15f, ["reloadSpeedPercent"] = 15f, ["missileSpeedPercent"] = 10f,
            },
            ["discipline"] = new Dictionary<string, float> { ["blockAbilityPercent"] = 25f, ["parryAbilityPercent"] = 25f },
            ["hide"] = new Dictionary<string, float> { ["armourPercent"] = 30f },
        },
        new Dictionary<string, string>());

    // A soldier's properties as the engine left them: each stat the check reads at a value a percentage can move (a chance below 1 so the clamp never hides it).
    private static AgentDrivenProperties EngineProps(IReadOnlyList<AffixExpect> expected)
    {
        var props = Props();
        foreach (var e in expected)
        {
            if (Enum.TryParse(e.Stat, out DrivenProperty property))
                props.SetStat(property, e.Math == AffixMath.Chance ? 0.5f : 1.5f);
        }

        return props;
    }

    private static LdStatSample ReadBack(IReadOnlyList<AffixExpect> expected, AgentDrivenProperties before, AgentDrivenProperties after) =>
        new("fow_test", expected.Select(e => Enum.TryParse(e.Stat, out DrivenProperty p)
            ? new LdStatReading(e.Stat, before.GetStat(p), after.GetStat(p))
            : new LdStatReading(e.Stat, float.NaN, float.NaN)).ToList());

    [TestMethod]
    public void EveryPoolPower_PublishedThroughRunModifiers_ReadsBackAsTheCheckExpects_AndFailsWithoutThePublish()
    {
        // The check's stat names and arithmetic against the real producers (RunModifiers, then the hooks), not a canned sample.
        var plan = ShippedNumbers();
        foreach (var power in AffixPlan.Pool)
        {
            var expected = LongDefeatAffixChecks.Expectations(plan, power);
            Assert.IsTrue(expected.Count > 0, power + " has a stat effect");
            var mission = NewMission();
            var attacker = AgentOf(mission, BattleSideEnum.Attacker, Human);

            LongDefeatStatHooks.Arm(mission);
            LongDefeatStatHooks.Publish(mission, RunModifiers.StatTable(plan, new[] { power }, 1f));
            var before = EngineProps(expected);
            var after = EngineProps(expected);
            LongDefeatStatHooks.UpdateStats(attacker, after);
            LongDefeatStatHooks.InitializeArmour(attacker, after, BuildData(BattleSideEnum.Attacker));
            var published = LongDefeatAffixChecks.Effect(power, expected, ReadBack(expected, before, after));

            LongDefeatStatHooks.Arm(mission);
            var unchanged = EngineProps(expected);
            LongDefeatStatHooks.UpdateStats(attacker, unchanged);
            LongDefeatStatHooks.InitializeArmour(attacker, unchanged, BuildData(BattleSideEnum.Attacker));
            var unpublished = LongDefeatAffixChecks.Effect(power, expected, ReadBack(expected, before, unchanged));

            Assert.AreEqual(CheckResult.Pass, published.Result, power + ": " + published.Detail);
            Assert.AreEqual(CheckResult.Fail, unpublished.Result, power + " with no publish: " + unpublished.Detail);
        }
    }

    [TestMethod]
    public void InitializeArmour_NothingArmedOrAnotherMissionOrNoPercent_IsUntouched()
    {
        var mission = NewMission();
        var first = Props();
        LongDefeatStatHooks.Publish(mission, new LdStatTable(1f, null, 30f));
        LongDefeatStatHooks.InitializeArmour(AgentOf(mission, BattleSideEnum.Attacker, Human), first, BuildData(BattleSideEnum.Attacker));

        LongDefeatStatHooks.Arm(mission);
        LongDefeatStatHooks.Publish(mission, new LdStatTable(1f, null, 30f));
        var other = Props();
        LongDefeatStatHooks.InitializeArmour(AgentOf(NewMission(), BattleSideEnum.Attacker, Human), other, BuildData(BattleSideEnum.Attacker));

        LongDefeatStatHooks.Publish(mission, new LdStatTable(1f, null, 0f));
        var none = Props();
        LongDefeatStatHooks.InitializeArmour(AgentOf(mission, BattleSideEnum.Attacker, Human), none, BuildData(BattleSideEnum.Attacker));

        Assert.AreEqual(10f, first.ArmorHead, "unarmed");
        Assert.AreEqual(10f, other.ArmorHead, "another mission");
        Assert.AreEqual(10f, none.ArmorHead, "no percent");
    }
}
