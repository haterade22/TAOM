using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TAOM.Features.LongDefeat.Domain;
using TAOM.Features.LongDefeat.Hooks;
using TAOM.Features.RaceAbilities.Domain;
using TAOM.Tests.Infrastructure;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.LongDefeat;

/// <summary>
/// The troop and class boons at the engine's hooks (phase2-plan.md section 4, Slice 6): the stat hook writes the record of the group a defender's character puts it in
/// (<c>IsMounted</c>, else <c>IsRanged</c>, else infantry) and the lord's body takes the player record only, whoever he was before; a horse takes its rider's; the damage model's
/// two hooks scale a blow a defender deals and a blow it takes by the same groups. Agents are uninitialized instances, as in <see cref="LongDefeatStatHooksTests"/>.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class LongDefeatTargetHooksTests
{
    private readonly List<IntPtr> _flagBlocks = new();

    [ClassInitialize]
    public static void Init(Microsoft.VisualStudio.TestTools.UnitTesting.TestContext _) => GameAssemblies.EnsureLoaded();

    [TestCleanup]
    public void Clean()
    {
        LongDefeatStatHooks.Arm(null);
        LongDefeatCombatHooks.Arm(null);
        foreach (var block in _flagBlocks)
            Marshal.FreeHGlobal(block);
        _flagBlocks.Clear();
    }

    private static Mission NewMission() => (Mission)FormatterServices.GetUninitializedObject(typeof(Mission));

    private static void Stfld(object target, FieldInfo field, object? value)
    {
        var write = new DynamicMethod("set_" + field.Name, null, new[] { typeof(object), typeof(object) }, typeof(LongDefeatTargetHooksTests).Module, true);
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

    private static void SetField(object target, Type declaring, string name, object? value) =>
        Stfld(target, declaring.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!, value);

    private static Team TeamOf(BattleSideEnum side)
    {
        var team = (Team)FormatterServices.GetUninitializedObject(typeof(Team));
        SetBacking(team, nameof(Team.Side), side);
        return team;
    }

    private static BasicCharacterObject Character(bool mounted, bool ranged)
    {
        var character = (BasicCharacterObject)FormatterServices.GetUninitializedObject(typeof(BasicCharacterObject));
        SetField(character, typeof(BasicCharacterObject), "_isMounted", mounted);
        SetField(character, typeof(BasicCharacterObject), "_isRanged", ranged);
        return character;
    }

    private Agent Soldier(Mission mission, BattleSideEnum side, bool mounted = false, bool ranged = false, bool noCharacter = false)
    {
        var agent = MakeAgent(mission, side, AgentFlag.IsHumanoid);
        if (!noCharacter)
            SetField(agent, typeof(Agent), "_character", Character(mounted, ranged));
        return agent;
    }

    private Agent MakeAgent(Mission? mission, BattleSideEnum? side, AgentFlag flags, Agent? rider = null)
    {
        var agent = (Agent)FormatterServices.GetUninitializedObject(typeof(Agent));
        var block = Marshal.AllocHGlobal(sizeof(uint));
        _flagBlocks.Add(block);
        Marshal.WriteInt32(block, unchecked((int)(uint)flags));
        SetField(agent, typeof(Agent), "_flagsPointer", new UIntPtr((ulong)block.ToInt64()));
        SetBacking(agent, nameof(Agent.Mission), mission);
        if (side.HasValue)
            SetBacking(agent, nameof(Agent.Team), TeamOf(side.Value));
        if (rider != null)
            SetField(agent, typeof(Agent), "_cachedRiderAgent", rider);
        return agent;
    }

    private static void MakeMain(Mission mission, Agent agent) => SetField(mission, typeof(Mission), "_mainAgent", agent);

    private static AgentDrivenProperties Props() => new() { SwingSpeedMultiplier = 1f, MaxSpeedMultiplier = 1f, MountSpeed = 1f };

    private static LdTargetStats Record(float swing, float move = 0f, float damage = 1f, float taken = 1f) =>
        new(new RaceAbilityEffects { SwingSpeedPercent = swing, MoveSpeedPercent = move, MountSpeedPercent = move }, damage, taken);

    private static LdStatTable Table(LdTargetStats? infantry = null, LdTargetStats? archers = null, LdTargetStats? cavalry = null, LdTargetStats? player = null) =>
        new LdStatTable(1f)
        {
            Infantry = infantry ?? LdTargetStats.None,
            Archers = archers ?? LdTargetStats.None,
            Cavalry = cavalry ?? LdTargetStats.None,
            Player = player ?? LdTargetStats.None,
        };

    private static Mission Armed(LdStatTable table)
    {
        var mission = NewMission();
        LongDefeatStatHooks.Arm(mission);
        LongDefeatStatHooks.Publish(mission, table);
        LongDefeatCombatHooks.Arm(mission);
        LongDefeatCombatHooks.PublishTargets(mission, table);
        return mission;
    }

    [TestMethod]
    public void UpdateStats_ADefenderTakesTheRecordOfTheGroupItsCharacterIsIn()
    {
        var table = Table(Record(10f), Record(20f), Record(30f), Record(40f));
        var mission = Armed(table);

        var infantry = Props();
        LongDefeatStatHooks.UpdateStats(Soldier(mission, BattleSideEnum.Defender), infantry);
        var archer = Props();
        LongDefeatStatHooks.UpdateStats(Soldier(mission, BattleSideEnum.Defender, ranged: true), archer);
        var rider = Props();
        LongDefeatStatHooks.UpdateStats(Soldier(mission, BattleSideEnum.Defender, mounted: true), rider);
        var horseArcher = Props();
        LongDefeatStatHooks.UpdateStats(Soldier(mission, BattleSideEnum.Defender, mounted: true, ranged: true), horseArcher);

        Assert.AreEqual(1.10f, infantry.SwingSpeedMultiplier, 1e-4f);
        Assert.AreEqual(1.20f, archer.SwingSpeedMultiplier, 1e-4f);
        Assert.AreEqual(1.30f, rider.SwingSpeedMultiplier, 1e-4f, "a rider is cavalry even when bought to fight on foot");
        Assert.AreEqual(1.30f, horseArcher.SwingSpeedMultiplier, 1e-4f, "mounted wins over ranged");
    }

    [TestMethod]
    public void UpdateStats_TheMainAgent_TakesThePlayerRecordOnly_WhateverHisCharacterIs()
    {
        var mission = Armed(Table(Record(10f), Record(20f), Record(30f), Record(40f)));
        var lord = Soldier(mission, BattleSideEnum.Defender, ranged: true);
        MakeMain(mission, lord);
        var props = Props();

        LongDefeatStatHooks.UpdateStats(lord, props);

        Assert.AreEqual(1.40f, props.SwingSpeedMultiplier, 1e-4f, "not the archers' 20 percent, and not both");
    }

    [TestMethod]
    public void UpdateStats_ATakenOverSoldier_SwitchesFromHisGroupsRecordToThePlayers()
    {
        var mission = Armed(Table(Record(10f), player: Record(40f)));
        var soldier = Soldier(mission, BattleSideEnum.Defender);
        var before = Props();
        LongDefeatStatHooks.UpdateStats(soldier, before);

        MakeMain(mission, soldier);
        var after = Props();
        LongDefeatStatHooks.UpdateStats(soldier, after);

        Assert.AreEqual(1.10f, before.SwingSpeedMultiplier, 1e-4f);
        Assert.AreEqual(1.40f, after.SwingSpeedMultiplier, 1e-4f);
    }

    [TestMethod]
    public void UpdateStats_AGroupWithNoStatBoon_IsUntouched()
    {
        var mission = Armed(Table(Record(10f)));
        var props = Props();

        LongDefeatStatHooks.UpdateStats(Soldier(mission, BattleSideEnum.Defender, ranged: true), props);

        Assert.AreEqual(1f, props.SwingSpeedMultiplier);
    }

    [TestMethod]
    public void UpdateStats_ADefenderWithNoCharacter_IsUntouched()
    {
        var mission = Armed(Table(Record(10f), Record(10f), Record(10f), Record(10f)));
        var props = Props();

        LongDefeatStatHooks.UpdateStats(Soldier(mission, BattleSideEnum.Defender, noCharacter: true), props);

        Assert.AreEqual(1f, props.SwingSpeedMultiplier);
    }

    [TestMethod]
    public void UpdateStats_ADefendersHorse_TakesItsRidersMountSpeed_TheLordsHorseThePlayers()
    {
        var mission = Armed(Table(cavalry: Record(0f, 12f), player: Record(0f, 8f)));
        var rider = Soldier(mission, BattleSideEnum.Defender, mounted: true);
        var lord = Soldier(mission, BattleSideEnum.Defender, mounted: true);
        MakeMain(mission, lord);
        var knightsHorse = Props();
        var lordsHorse = Props();

        LongDefeatStatHooks.UpdateStats(MakeAgent(mission, null, AgentFlag.Mountable, rider), knightsHorse);
        LongDefeatStatHooks.UpdateStats(MakeAgent(mission, null, AgentFlag.Mountable, lord), lordsHorse);

        Assert.AreEqual(1.12f, knightsHorse.MountSpeed, 1e-4f);
        Assert.AreEqual(1.08f, lordsHorse.MountSpeed, 1e-4f);
        Assert.AreEqual(1f, knightsHorse.MaxSpeedMultiplier, "a horse takes ApplyMount, never the walking speed");
    }

    [TestMethod]
    public void UpdateStats_TheAttackersRecord_StillGoesToAttackersOnly()
    {
        var table = new LdStatTable(1f, new RaceAbilityEffects { SwingSpeedPercent = 15f }) { Infantry = Record(10f) };
        var mission = Armed(table);
        var attacker = Props();
        var defender = Props();

        LongDefeatStatHooks.UpdateStats(Soldier(mission, BattleSideEnum.Attacker), attacker);
        LongDefeatStatHooks.UpdateStats(Soldier(mission, BattleSideEnum.Defender), defender);

        Assert.AreEqual(1.15f, attacker.SwingSpeedMultiplier, 1e-4f);
        Assert.AreEqual(1.10f, defender.SwingSpeedMultiplier, 1e-4f);
    }

    private static AttackInformation Blow(Agent? attacker, Agent? victim = null, bool victimIsMount = false, bool attackerIsMount = false) =>
        new() { AttackerAgent = attacker!, VictimAgent = victim!, IsVictimAgentMount = victimIsMount, IsAttackerAgentMount = attackerIsMount };

    private static readonly AttackCollisionData Plain = default;

    [TestMethod]
    public void Amplify_ADefendersBlow_IsScaledByItsGroupsDamageFactor_AndCountsNothing()
    {
        var mission = Armed(Table(Record(0f, damage: 1.25f), Record(0f, damage: 1.5f)));
        var archer = Blow(Soldier(mission, BattleSideEnum.Defender, ranged: true));
        var infantry = Blow(Soldier(mission, BattleSideEnum.Defender));

        Assert.AreEqual(15f, LongDefeatCombatHooks.Amplify(in archer, in Plain, 10f), 1e-4f);
        Assert.AreEqual(12.5f, LongDefeatCombatHooks.Amplify(in infantry, in Plain, 10f), 1e-4f);
        Assert.AreEqual(0L, LongDefeatCombatHooks.Raised, "the count proves the attackers' wave factor ran; a defender's boon blow must not satisfy the scale check");
    }

    [TestMethod]
    public void Raised_CountsTheAttackersBlowsOnly_WhenBothSidesAreScaled()
    {
        var table = new LdStatTable(1f) { Infantry = Record(0f, damage: 1.5f) };
        var mission = NewMission();
        LongDefeatStatHooks.Arm(mission);
        LongDefeatCombatHooks.Arm(mission);
        LongDefeatCombatHooks.Publish(mission, 2f);
        LongDefeatCombatHooks.PublishTargets(mission, table);
        var defender = Blow(Soldier(mission, BattleSideEnum.Defender));
        var attacker = Blow(Soldier(mission, BattleSideEnum.Attacker));

        Assert.AreEqual(15f, LongDefeatCombatHooks.Amplify(in defender, in Plain, 10f), 1e-4f);
        Assert.AreEqual(20f, LongDefeatCombatHooks.Amplify(in attacker, in Plain, 10f), 1e-4f);
        Assert.AreEqual(1L, LongDefeatCombatHooks.Raised);
    }

    [TestMethod]
    public void Amplify_TheLordsBlow_TakesThePlayerFactorOnly()
    {
        var mission = Armed(Table(Record(0f, damage: 2f), player: Record(0f, damage: 1.3f)));
        var lord = Soldier(mission, BattleSideEnum.Defender);
        MakeMain(mission, lord);
        var blow = Blow(lord);

        Assert.AreEqual(13f, LongDefeatCombatHooks.Amplify(in blow, in Plain, 10f), 1e-4f);
    }

    [TestMethod]
    public void Amplify_AMountedDefendersHorseBlow_CountsAsItsRiders()
    {
        var mission = Armed(Table(cavalry: Record(0f, damage: 1.2f)));
        var rider = Soldier(mission, BattleSideEnum.Defender, mounted: true);
        var blow = Blow(MakeAgent(mission, null, AgentFlag.Mountable, rider), attackerIsMount: true);

        Assert.AreEqual(12f, LongDefeatCombatHooks.Amplify(in blow, in Plain, 10f), 1e-4f);
    }

    [TestMethod]
    public void Amplify_ADefendersOwnFall_IsUntouched()
    {
        var mission = Armed(Table(Record(0f, damage: 2f)));
        var blow = Blow(Soldier(mission, BattleSideEnum.Defender));
        var fall = CollisionDataFixture.With(fallSpeed: 7f);

        Assert.AreEqual(10f, LongDefeatCombatHooks.Amplify(in blow, in fall, 10f));
    }

    [TestMethod]
    public void Amplify_ADefenderOfAnotherMission_OrWithNoTable_IsUntouched()
    {
        var mission = Armed(Table(Record(0f, damage: 2f)));
        var stranger = Blow(Soldier(NewMission(), BattleSideEnum.Defender));
        var noCharacter = Blow(Soldier(mission, BattleSideEnum.Defender, noCharacter: true));

        Assert.AreEqual(10f, LongDefeatCombatHooks.Amplify(in stranger, in Plain, 10f));
        Assert.AreEqual(10f, LongDefeatCombatHooks.Amplify(in noCharacter, in Plain, 10f));
    }

    [TestMethod]
    public void Reduce_ABlowOnADefender_IsScaledByItsGroupsDamageTakenFactor()
    {
        var mission = Armed(Table(Record(0f, taken: 0.8f), Record(0f, taken: 1.1f)));
        var onInfantry = Blow(Soldier(mission, BattleSideEnum.Attacker), Soldier(mission, BattleSideEnum.Defender));
        var onArcher = Blow(Soldier(mission, BattleSideEnum.Attacker), Soldier(mission, BattleSideEnum.Defender, ranged: true));

        Assert.AreEqual(8f, LongDefeatCombatHooks.Reduce(in onInfantry, in Plain, 10f), 1e-4f);
        Assert.AreEqual(11f, LongDefeatCombatHooks.Reduce(in onArcher, in Plain, 10f), 1e-4f);
    }

    [TestMethod]
    public void Reduce_ABlowOnTheLord_TakesThePlayerFactorOnly()
    {
        var mission = Armed(Table(Record(0f, taken: 0.5f), player: Record(0f, taken: 0.7f)));
        var lord = Soldier(mission, BattleSideEnum.Defender);
        MakeMain(mission, lord);
        var blow = Blow(Soldier(mission, BattleSideEnum.Attacker), lord);

        Assert.AreEqual(7f, LongDefeatCombatHooks.Reduce(in blow, in Plain, 10f), 1e-4f);
    }

    [TestMethod]
    public void Reduce_ABlowOnADefendersHorse_IsTheRidersFactor()
    {
        var mission = Armed(Table(cavalry: Record(0f, taken: 0.6f)));
        var rider = Soldier(mission, BattleSideEnum.Defender, mounted: true);
        var blow = Blow(Soldier(mission, BattleSideEnum.Attacker), MakeAgent(mission, null, AgentFlag.Mountable, rider), victimIsMount: true);

        Assert.AreEqual(6f, LongDefeatCombatHooks.Reduce(in blow, in Plain, 10f), 1e-4f);
    }

    [TestMethod]
    public void Reduce_AnAttackerVictim_AnotherMission_OrNothingArmed_IsUntouched()
    {
        var mission = Armed(Table(Record(0f, taken: 0.5f), player: Record(0f, taken: 0.5f)));
        var onAttacker = Blow(Soldier(mission, BattleSideEnum.Defender), Soldier(mission, BattleSideEnum.Attacker));
        var elsewhere = Blow(Soldier(NewMission(), BattleSideEnum.Attacker), Soldier(NewMission(), BattleSideEnum.Defender));
        var onDefender = Blow(Soldier(mission, BattleSideEnum.Attacker), Soldier(mission, BattleSideEnum.Defender));

        Assert.AreEqual(10f, LongDefeatCombatHooks.Reduce(in onAttacker, in Plain, 10f));
        Assert.AreEqual(10f, LongDefeatCombatHooks.Reduce(in elsewhere, in Plain, 10f));
        LongDefeatCombatHooks.Disarm(mission);
        Assert.AreEqual(10f, LongDefeatCombatHooks.Reduce(in onDefender, in Plain, 10f), "disarmed");
    }

    [TestMethod]
    public void Reduce_AFactorOfOne_ChangesNothing_AndNoVictim_IsUntouched()
    {
        var mission = Armed(Table(Record(0f)));
        var blow = Blow(Soldier(mission, BattleSideEnum.Attacker), Soldier(mission, BattleSideEnum.Defender));
        var noVictim = Blow(Soldier(mission, BattleSideEnum.Attacker));

        Assert.AreEqual(10f, LongDefeatCombatHooks.Reduce(in blow, in Plain, 10f));
        Assert.AreEqual(10f, LongDefeatCombatHooks.Reduce(in noVictim, in Plain, 10f));
    }

    [TestMethod]
    public void PublishTargets_FromAMissionThatIsNotArmed_IsIgnored_AndAReArmStartsClean()
    {
        var mission = NewMission();
        LongDefeatCombatHooks.Arm(mission);
        LongDefeatCombatHooks.PublishTargets(NewMission(), Table(Record(0f, taken: 0.5f)));
        var blow = Blow(Soldier(mission, BattleSideEnum.Attacker), Soldier(mission, BattleSideEnum.Defender));
        Assert.AreEqual(10f, LongDefeatCombatHooks.Reduce(in blow, in Plain, 10f));

        LongDefeatCombatHooks.PublishTargets(mission, Table(Record(0f, taken: 0.5f)));
        Assert.AreEqual(5f, LongDefeatCombatHooks.Reduce(in blow, in Plain, 10f), 1e-4f);
        LongDefeatCombatHooks.Arm(mission);
        Assert.AreEqual(10f, LongDefeatCombatHooks.Reduce(in blow, in Plain, 10f), "a new mission never inherits the last run's boons");
    }
}
