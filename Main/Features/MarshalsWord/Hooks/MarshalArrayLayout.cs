using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TAOM.Features.MarshalsWord.Domain;

namespace TAOM.Features.MarshalsWord.Hooks;

/// <summary>
/// The engine half of keep-the-array (build-spec 6.1 and 6.3), called by <see cref="MarshalArrayPatch"/> alone: it reads the selected
/// companies into the pure solver, then writes the outputs that <c>OrderController.SimulateNewOrderWithPositionAndDirectionAux</c> would,
/// with public calls only, so vanilla's commit code still runs. It solves first and writes no output until everything has been built, so a
/// throw leaves vanilla untouched outputs. The preview runs it at 30 Hz with the mouse held (OrderTroopPlacer.cs:300, 799), so it writes
/// no log line.
/// </summary>
internal static class MarshalArrayLayout
{
    internal static bool TryTakeOver(IEnumerable<Formation> formations, Dictionary<Formation, Formation>? simulationFormations,
        WorldPosition lineBegin, WorldPosition lineEnd, bool wantFrames, out List<WorldPosition>? frames, bool wantChanges,
        out List<(Formation, int, float, WorldPosition, Vec2)>? changes, out bool isLineShort)
    {
        frames = null;
        changes = null;
        isLineShort = false;

        var mission = Mission.Current;
        if (mission == null || mission.Mode == MissionMode.Deployment)
            return false;
        var group = formations.ToList();
        if (group.Count < 2 || !group.All(f => IsOurs(f, mission.PlayerTeam)))
            return false;

        var solved = MarshalArraySolver.Solve(group.Select(Read).ToArray(), lineBegin.AsVec2.x, lineBegin.AsVec2.y, lineEnd.AsVec2.x,
            lineEnd.AsVec2.y, MarshalHooks.KeepWidthsHeld, ManagedParameters.Instance.GetManagedParameter(ManagedParametersEnum.BipedalRadius));
        if (!solved.Ok)
            return false;

        var builtFrames = wantFrames ? new List<WorldPosition>(group.Sum(f => f.CountOfUnitsWithoutDetachedOnes)) : null;
        var builtChanges = wantChanges ? new List<(Formation, int, float, WorldPosition, Vec2)>() : null;
        for (var i = 0; i < group.Count; i++)
            Build(mission, group[i], simulationFormations?[group[i]], solved.Placements[i], lineBegin, builtFrames, builtChanges);

        frames = builtFrames;
        changes = builtChanges;
        isLineShort = solved.IsLineShort;
        return true;
    }

    /// <summary>
    /// Tells the session that a commit was laid out and asks for a march. The Move itself applies after the prefix returns
    /// (OrderController.cs:1574, 1588, 1596), so nothing compares with it yet; the march driver decides whether the request stands.
    /// </summary>
    internal static void AfterCommit(List<(Formation, int, float, WorldPosition, Vec2)> changes, bool isLineShort)
    {
        MarshalHooks.ReportArray(isLineShort, changes.Count);
        var targets = new List<MarchTarget>(changes.Count);
        foreach (var change in changes)
            targets.Add(new MarchTarget(change.Item1.Index, change.Item4.AsVec2.x, change.Item4.AsVec2.y));
        MarshalHooks.RequestMarch(targets);
    }

    // Every company must be the player's and orderable by the player. The commit's BeforeSetOrder lifts a company that has a player owner
    // off AI control (OrderController.cs:724-727), the preview does not run it, so the test reads the state both see (6.1).
    private static bool IsOurs(Formation f, Team? playerTeam) =>
        f.Team == playerTeam && !(f.IsAIControlled && f.PlayerOwner == null) && f.CountOfUnitsWithoutDetachedOnes > 0;

    /// <summary>One company as the solver sees it, read on the main thread; the field trial plans its drags from the same reading.</summary>
    internal static ArrayCompany Read(Formation f)
    {
        var place = PlaceOf(f);
        return new ArrayCompany(f.Index, place.x, place.y, f.Width, f.MinimumWidth, f.MaximumWidth, f.Depth, f.CountOfUnitsWithoutDetachedOnes,
            IsColumn(f), f.Direction.x, f.Direction.y);
    }

    private static Vec2 PlaceOf(Formation f) =>
        f.OrderPositionIsValid && f.GetReadonlyMovementOrderReference().MovementState == MovementOrder.MovementStateEnum.Hold ? f.OrderPosition : f.CurrentPosition;

    private static bool IsColumn(Formation f) => f.ArrangementOrder.OrderEnum == ArrangementOrder.ArrangementOrderEnum.Column;

    private static void Build(Mission mission, Formation f, Formation? simulation, ArrayPlacement placement, WorldPosition lineBegin,
        List<WorldPosition>? frames, List<(Formation, int, float, WorldPosition, Vec2)>? changes)
    {
        // A column's custom width must be its own, or the long line branch transposes it (6.3).
        var width = IsColumn(f) ? f.Arrangement.FlankWidth : placement.Width;
        var direction = new Vec2(placement.DirX, placement.DirY);
        var position = mission.GetStraightPathToTarget(new Vec2(placement.X, placement.Y), lineBegin);

        // Vanilla replaces the simulation copy inside the fit function only (OrderController.cs:1336-1339) and runs the frames on the dictionary's
        // copy (:1329, :1410), which syncs its spacing (Formation.cs:3121). A replacement kept for the frames would cost a new Formation per company
        // on every preview frame, so the fit test alone gets it.
        var fit = simulation != null && simulation.UnitSpacing != f.UnitSpacing ? new Formation(null, -1) : simulation;

        // Vanilla's DecreaseUnitSpacingAndWidthIfNotAllUnitsFit (1334-1357), over the public overload that Patch92 scopes.
        var reduction = 0;
        var unitIndex = f.CountOfUnitsWithoutDetachedOnes - 1;
        var actualWidth = width;
        do
        {
            f.GetUnitPositionWithIndexAccordingToNewOrder(fit, unitIndex, in position, in direction, width, f.UnitSpacing - reduction,
                out var last, out _, out actualWidth);
            if (last.HasValue)
                break;
            reduction++;
        }
        while (f.UnitSpacing - reduction >= 0);
        reduction = Math.Min(reduction, f.UnitSpacing);
        if (reduction > 0)
            width = actualWidth;

        if (frames != null)
        {
            for (var index = 0; index < f.CountOfUnitsWithoutDetachedOnes; index++)
            {
                f.GetUnitPositionWithIndexAccordingToNewOrder(simulation, index, in position, in direction, width, f.UnitSpacing - reduction,
                    out var unitPosition, out _);
                if (unitPosition.HasValue)
                    frames.Add(unitPosition.Value);
            }
        }

        changes?.Add((f, reduction, width, position, direction));
    }
}
