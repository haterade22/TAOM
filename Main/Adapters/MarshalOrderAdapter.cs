using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TAOM.Core.Logging;
using TAOM.Features.AdvancedCombat;
using TAOM.Features.MarshalsWord;
using TAOM.Features.MarshalsWord.Domain;

namespace TAOM.Adapters;

/// <summary>
/// <see cref="IMarshalOrderAdapter"/> over the current mission's player team (build-spec 5.1). A company is a <c>Formation.Index</c>, mapped back
/// through <c>Team.FormationsIncludingSpecialAndEmpty</c>, so a change of player team has to clear every stored company (the adapter
/// raises <see cref="PlayerTeamChanged"/>). Arrival reads values the engine caches: <c>Formation.CurrentPosition</c> (cached for a line, the
/// vanguard's live position for a column) and the order point.
/// </summary>
internal sealed class MarshalOrderAdapter : IMarshalOrderAdapter
{
    private readonly IModLogger _logger;
    private Mission? _mission;
    private OrderController? _controller;
    private Formation? _aimedAt;
    private int _sampled;
    private int _onTarget;
    private int _of;

    public MarshalOrderAdapter(IModLogger logger) => _logger = logger;

    /// <summary>The number the queue and the table know a company by: <c>Formation.Index</c>, a field of the player's team.</summary>
    internal static int CompanyOf(Formation formation) => formation.Index;

    public event Action<IReadOnlyList<int>>? DirectOrder;

    public event Action? PlayerTeamChanged;

    public double Now => Mission.Current?.CurrentTime ?? 0.0;

    public bool IsDeployment => Mission.Current?.Mode == MissionMode.Deployment;

    public void Attach()
    {
        var mission = Mission.Current;
        if (mission == null || _mission != null)
            return;
        _mission = mission;
        mission.Teams.OnPlayerTeamChanged += OnTeamChanged;
        Follow(mission.PlayerTeam);
    }

    public void Detach()
    {
        if (_mission != null)
            _mission.Teams.OnPlayerTeamChanged -= OnTeamChanged;
        _mission = null;
        if (_controller != null)
            _controller.OnOrderIssued -= OnOrderIssued;
        _controller = null;
    }

    public bool PlayerCommands
    {
        get
        {
            var team = Mission.Current?.PlayerTeam;
            return team != null && (team.IsPlayerGeneral || team.IsPlayerSergeant);
        }
    }

    public IReadOnlyList<int> SelectedCompanies()
    {
        var selected = _controller?.SelectedFormations;
        if (selected == null || selected.Count == 0)
            return Array.Empty<int>();
        var companies = new List<int>(selected.Count);
        foreach (var formation in selected)
            companies.Add(formation.Index);
        return companies;
    }

    public bool IsControllable(int company)
    {
        var formation = FormationOf(company);
        return formation != null && formation.CountOfUnits > 0 && !formation.IsAIControlled;
    }

    public bool Arrived(int company)
    {
        var formation = FormationOf(company);
        // A line order sends only formations with undetached units (OrderController.cs:990-998), so a company with none keeps its old order point, and
        // standing on that old point must not read as an arrival no order caused: such a step falls to patience (5.4), unless a unit rejoins on the old point first.
        return formation != null && formation.OrderPositionIsValid && formation.CountOfUnitsWithoutDetachedOnes > 0
            && formation.CurrentPosition.Distance(formation.OrderPosition) <= MarshalTuning.ArrivalSlack;
    }

    public bool WalkUnderWay(int company)
    {
        var formation = FormationOf(company);
        return formation != null && formation.GetReadonlyMovementOrderReference().OrderEnum == MovementOrder.MovementOrderEnum.Move && !Arrived(company);
    }

    public bool OrderEnded(int company)
    {
        var formation = FormationOf(company);
        return formation == null || formation.GetReadonlyMovementOrderReference().OrderEnum != MovementOrder.MovementOrderEnum.AttackEntity;
    }

    public bool PayloadSpent(object payload)
    {
        var order = (MarshalOrderPayload)payload;
        if (order.Target != null)
            return order.Target.CountOfUnits <= 0;
        var side = Mission.Current?.PlayerTeam?.Side;
        return order.Orderable != null && (side == null || order.Orderable.GetOrder(side.Value) == OrderType.None);
    }

    public double RemainingDistance(int company, object? payload)
    {
        var formation = FormationOf(company);
        if (formation == null)
            return double.NaN;
        var order = payload as MarshalOrderPayload;
        if (order?.Type == OrderType.AttackEntity && order.TryPoint(out var x, out var y, out _))
            return formation.CurrentPosition.Distance(new Vec2(x, y));
        return formation.OrderPositionIsValid ? formation.CurrentPosition.Distance(formation.OrderPosition) : double.NaN;
    }

    public double Speed(int company) => FormationOf(company)?.CachedMovementSpeed ?? 1.0;

    public bool Issue(IReadOnlyList<int> companies, object payload)
    {
        var team = Mission.Current?.PlayerTeam;
        var master = team?.MasterOrderController;
        if (master == null)
            return false;

        var order = (MarshalOrderPayload)payload;
        if (order.IsMark)
            return Mark(companies, order.Target!);

        // The array prefix reads the widths key, and a queued drag must lay out with the key its player held (6.2). Main thread only: the
        // screen tick that writes the key and the mission tick that runs this are never concurrent.
        var keptWidths = MarshalHooks.KeepWidthsHeld;
        try
        {
            foreach (var company in companies)
            {
                var formation = FormationOf(company);
                if (formation != null && !master.SelectedFormations.Contains(formation) && master.IsFormationSelectable(formation))
                    master.SelectFormation(formation);
            }

            // SetOrderWithOrderableObject reads SelectedFormations[0] with no test (OrderController.cs:1010).
            if (master.SelectedFormations.Count == 0)
                return false;
            MissionThreadGuard.NoteCall("MarshalOrderAdapter.Issue", _logger.LogError);
            MarshalHooks.KeepWidthsHeld = order.KeepWidths;
            order.Apply(master);
            return true;
        }
        finally
        {
            MarshalHooks.KeepWidthsHeld = keptWidths;
            master.ClearSelectedFormations();
        }
    }

    // SetTargetFormation writes the target index of every soldier (Formation.cs:240-249) and moves no order, so a mark is the one thing a
    // charge does that this keeps. Patch31b re-points a running cavalry cycle from its postfix.
    private bool Mark(IReadOnlyList<int> companies, Formation target)
    {
        var marked = false;
        foreach (var company in companies)
        {
            var formation = FormationOf(company);
            if (formation == null || formation.CountOfUnits == 0 || formation.IsAIControlled)
                continue;
            MissionThreadGuard.NoteCall("MarshalOrderAdapter.Mark", _logger.LogError);
            formation.SetTargetFormation(target);
            marked = true;
        }

        return marked;
    }

    public IReadOnlyList<MarkSample> ReadMarks()
    {
        var formations = Mission.Current?.PlayerTeam?.FormationsIncludingSpecialAndEmpty;
        if (formations == null)
            return Array.Empty<MarkSample>();
        var samples = new List<MarkSample>();
        foreach (var formation in formations)
        {
            var target = formation.TargetFormation;
            if (target == null || formation.CountOfUnits == 0)
                continue;
            var order = formation.GetReadonlyMovementOrderReference().OrderEnum;
            if (order == MovementOrder.MovementOrderEnum.Charge || order == MovementOrder.MovementOrderEnum.Advance || order == MovementOrder.MovementOrderEnum.ChargeToTarget)
                continue;
            _sampled = 0;
            _onTarget = 0;
            _of = 0;
            _aimedAt = target;
            formation.ApplyActionOnEachUnit(Sample);
            var context = MarshalMarkRules.ContextOf(formation.QuerySystem.IsRangedFormationReadOnly, order == MovementOrder.MovementOrderEnum.Stop);
            samples.Add(new MarkSample(formation.Index, target.Index, _onTarget, _of, context));
        }

        return samples;
    }

    // At most MarkSampleMax soldiers; one native read each.
    private void Sample(Agent agent)
    {
        if (_sampled >= MarshalTuning.MarkSampleMax || !agent.IsHuman)
            return;
        _sampled++;
        var aim = agent.GetTargetAgent();
        if (aim == null)
            return;
        _of++;
        if ((aim.RiderAgent ?? aim).Formation == _aimedAt)
            _onTarget++;
    }

    public bool TryPoint(object payload, out float x, out float y, out float z) => ((MarshalOrderPayload)payload).TryPoint(out x, out y, out z);

    public IReadOnlyList<MarchLeg> ReadMarch(IReadOnlyList<MarchTarget> targets)
    {
        var legs = new List<MarchLeg>(targets.Count);
        foreach (var target in targets)
        {
            var formation = FormationOf(target.Company);
            if (formation == null)
            {
                legs.Add(new MarchLeg(target.Company, float.NaN, 1f, 0f));
                continue;
            }

            // An emptied company is no longer ticked (Team.cs:616-622), so its position and its ETA never change: it leaves the march (6.4).
            if (formation.CountOfUnits == 0)
                continue;

            var order = formation.OrderPosition;
            legs.Add(new MarchLeg(target.Company, formation.CurrentPosition.Distance(order), formation.CachedMovementSpeed, TargetShift(formation, target)));
        }

        return legs;
    }

    // Charge, Stop and Retreat carry no position (MovementOrder.cs:178), hand an invalid one back (:552) and apply none (:690-691), and
    // Formation.SetPositioning ignores an invalid position (Formation.cs:862, 2485-2490), so the order point of a company given one of
    // them stays where it was. A company that left its Move, or that the AI took, therefore reports a shift over the limit (6.4).
    private static float TargetShift(Formation formation, MarchTarget target) =>
        formation.IsAIControlled || formation.GetReadonlyMovementOrderReference().OrderEnum != MovementOrder.MovementOrderEnum.Move ? float.MaxValue
            : formation.OrderPosition.Distance(new Vec2(target.X, target.Y));

    public void PublishPaces(IReadOnlyList<MarchPace> paces)
    {
        if (paces.Count == 0)
        {
            MarshalHooks.Paces = null;
            return;
        }

        var formations = new List<Formation>(paces.Count);
        var values = new List<float>(paces.Count);
        foreach (var pace in paces)
        {
            var formation = FormationOf(pace.Company);
            if (formation == null)
                continue;
            formations.Add(formation);
            values.Add(pace.Pace);
        }

        MarshalHooks.Paces = formations.Count == 0 ? null : new MarshalPaceTable(formations.ToArray(), values.ToArray());
    }

    private Formation? FormationOf(int company)
    {
        var formations = Mission.Current?.PlayerTeam?.FormationsIncludingSpecialAndEmpty;
        return formations != null && company >= 0 && company < formations.Count ? formations[company] : null;
    }

    private void OnTeamChanged(Team oldTeam, Team newTeam)
    {
        Follow(newTeam);
        PlayerTeamChanged?.Invoke();
    }

    private void Follow(Team? team)
    {
        if (_controller != null)
            _controller.OnOrderIssued -= OnOrderIssued;
        _controller = team?.PlayerOrderController;
        MarshalHooks.PlayerController = _controller;
        if (_controller != null)
            _controller.OnOrderIssued += OnOrderIssued;
    }

    private void OnOrderIssued(OrderType orderType, MBReadOnlyList<Formation> appliedFormations, OrderController orderController, params object[] delegateParams)
    {
        if (!MarshalOrderPayload.ClearsQueue(orderType) || DirectOrder == null || appliedFormations.Count == 0)
            return;
        var companies = new List<int>(appliedFormations.Count);
        foreach (var formation in appliedFormations)
            companies.Add(formation.Index);
        DirectOrder.Invoke(companies);
    }
}
