using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TAOM.Features.MarshalsWord;
using TAOM.Features.MarshalsWord.Domain;
using TAOM.Features.MarshalsWord.Hooks;

namespace TAOM.Adapters;

/// <summary><see cref="IMarshalThrowerAdapter"/> over the current mission's player team (build-spec 9.4). Reads cached engine values; writes only the published table.</summary>
internal sealed class MarshalThrowerAdapter : IMarshalThrowerAdapter
{
    private readonly Action<Agent> _count;
    private readonly List<float> _speeds = new();
    private bool? _fieldBattle;
    private int _units;
    private int _armed;
    private int _ammo;
    private bool _collectSpeeds;

    public MarshalThrowerAdapter() => _count = CountSoldier;

    public double Now => Mission.Current?.CurrentTime ?? 0.0;

    public bool PlayerCommands
    {
        get
        {
            var team = Mission.Current?.PlayerTeam;
            return team != null && (team.IsPlayerGeneral || team.IsPlayerSergeant);
        }
    }

    public ThrowerCompany Company(int company)
    {
        var formation = FormationOf(company);
        if (formation == null)
            return default;
        return new ThrowerCompany(formation.CountOfUnits > 0, formation.GetReadonlyMovementOrderReference().OrderEnum == MovementOrder.MovementOrderEnum.Advance,
            formation.IsAIControlled);
    }

    public ThrowerCensus Census(int company)
    {
        var formation = FormationOf(company);
        if (formation == null)
            return default;
        Begin(collectSpeeds: false);
        formation.ApplyActionOnEachUnit(_count);
        return new ThrowerCensus(_units, _armed, _ammo);
    }

    public float? Range(int company)
    {
        var formation = FormationOf(company);
        if (formation == null)
            return null;
        Begin(collectSpeeds: true);
        formation.ApplyActionOnEachUnit(_count);
        if (_speeds.Count == 0)
            return null;
        _speeds.Sort();
        return Mission.GetMissileRange(_speeds[_speeds.Count / 2], 0f);
    }

    public ThrowerGates Gates(int company)
    {
        var formation = FormationOf(company);
        var mission = Mission.Current;
        if (formation == null || mission == null)
            return default;
        var query = formation.QuerySystem;
        var target = formation.TargetFormation?.QuerySystem ?? query.ClosestSignificantlyLargeEnemyFormation;
        var targetFormation = target?.Formation;
        var open = ThrowsInArrangement(formation.ArrangementOrder.OrderEnum);
        var own = formation.CurrentPosition;
        var median = targetFormation?.CachedMedianPosition.AsVec2 ?? default;
        return new ThrowerGates(
            SettingOn: true,
            FieldBattle: _fieldBattle ??= mission.IsFieldBattle,
            Deployment: mission.Mode == MissionMode.Deployment,
            Advance: formation.GetReadonlyMovementOrderReference().OrderEnum == MovementOrder.MovementOrderEnum.Advance,
            AiControlled: formation.IsAIControlled,
            Held: MarshalFireTable.HoldOf(formation.Index),
            OnFireMode: MarshalFireTable.ModeOf(formation.Index) != FireMode.Free,
            HoldFire: formation.FiringOrder.OrderEnum == FiringOrder.RangedWeaponUsageOrderEnum.HoldYourFire,
            OpenArrangement: open,
            RangedBranch: query.IsRangedFormation || query.IsRangedCavalryFormation,
            HasTarget: targetFormation != null,
            Range: null,
            OwnDepth: formation.Depth,
            Own: new GroundPoint(own.x, own.y),
            Target: new GroundPoint(median.x, median.y),
            TargetDepth: targetFormation?.Depth ?? 0f);
    }

    /// <summary>
    /// True for every arrangement whose soldiers move on DefaultMove on an Advance (HumanAIComponent.cs:796-808): all but Column, which follows,
    /// and Circle, Shield wall and Square, which hold a defensive move whose Ranged row is zero.
    /// </summary>
    internal static bool ThrowsInArrangement(ArrangementOrder.ArrangementOrderEnum arrangement) =>
        arrangement != ArrangementOrder.ArrangementOrderEnum.Column && arrangement != ArrangementOrder.ArrangementOrderEnum.Circle
        && arrangement != ArrangementOrder.ArrangementOrderEnum.ShieldWall && arrangement != ArrangementOrder.ArrangementOrderEnum.Square;

    public void Publish(IReadOnlyList<ThrowerPoint> points)
    {
        if (points.Count == 0)
        {
            MarshalHooks.Throwers = null;
            return;
        }

        MarshalEngagePoint.Bind();
        var formations = new List<Formation>(points.Count);
        var x = new List<float>(points.Count);
        var y = new List<float>(points.Count);
        foreach (var point in points)
        {
            var formation = FormationOf(point.Company);
            if (formation == null)
                continue;
            formations.Add(formation);
            x.Add(point.X);
            y.Add(point.Y);
        }

        MarshalHooks.Throwers = formations.Count == 0 ? null : new MarshalThrowerTable(formations.ToArray(), x.ToArray(), y.ToArray());
    }

    private void Begin(bool collectSpeeds)
    {
        _units = 0;
        _armed = 0;
        _ammo = 0;
        _speeds.Clear();
        _collectSpeeds = collectSpeeds;
    }

    private void CountSoldier(Agent agent)
    {
        _units++;
        if (agent.HasMount)
            return;
        var equipment = agent.Equipment;
        var ammo = 0;
        var speed = 0f;
        for (var slot = EquipmentIndex.WeaponItemBeginSlot; slot < EquipmentIndex.ExtraWeaponSlot; slot++)
        {
            var weapon = equipment[slot];
            if (weapon.IsEmpty)
                continue;
            var usage = weapon.GetRangedUsageIndex();
            if (usage < 0)
                continue;
            if (!weapon.GetWeaponComponentDataForUsage(usage).IsConsumable)
                return;
            if (weapon.Amount <= 0)
                continue;
            ammo += weapon.Amount;
            if (speed == 0f)
                speed = weapon.GetModifiedMissileSpeedForUsage(usage) * agent.AgentDrivenProperties.MissileSpeedMultiplier;
        }

        if (ammo == 0)
            return;
        _armed++;
        _ammo += ammo;
        if (_collectSpeeds)
            _speeds.Add(speed);
    }

    private static Formation? FormationOf(int company)
    {
        var formations = Mission.Current?.PlayerTeam?.FormationsIncludingSpecialAndEmpty;
        return formations != null && company >= 0 && company < formations.Count ? formations[company] : null;
    }
}
