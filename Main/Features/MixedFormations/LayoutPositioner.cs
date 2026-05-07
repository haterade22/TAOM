using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Adapters;
using TAOM.Features.MixedFormations.Models;

namespace TAOM.Features.MixedFormations;

/// <summary>
/// Pure-function slot assignment engine. Given formation geometry (width, interval, units)
/// and a layout type, produces the (row, file) offset every unit should occupy. Math-only;
/// no engine state, no caching, no MissionBehavior calls.
/// </summary>
public sealed class LayoutPositioner : ILayoutPositioner
{
    public SlotAssignment BuildInitialAssignment(IFormationAdapter formation, FormationLayoutType layout)
    {
        var unitInterval = Math.Max(1f, formation.Interval + 1f);
        var filesPerRow = formation.Width > 1f
            ? Math.Max(1, (int)Math.Round(formation.Width / unitInterval))
            : Math.Max(1, (int)Math.Ceiling(Math.Sqrt(Math.Max(1, formation.CountOfUnits))));

        var assignment = new SlotAssignment(layout, filesPerRow);
        var unitsByIndex = formation.Units.OrderBy(u => u.Index).ToList();
        var melee = unitsByIndex.Where(u => !u.IsRanged).ToList();
        var ranged = unitsByIndex.Where(u => u.IsRanged).ToList();

        switch (layout)
        {
            case FormationLayoutType.InfantryFrontRangedBack:
                AssignBlock(melee, startRow: 0, filesPerRow, assignment.ByAgentIndex);
                assignment.NextMeleeIndex = melee.Count;
                var rangedStartRow = (melee.Count + filesPerRow - 1) / Math.Max(1, filesPerRow);
                AssignBlock(ranged, rangedStartRow, filesPerRow, assignment.ByAgentIndex);
                assignment.NextRangedIndex = ranged.Count;
                break;

            case FormationLayoutType.RangedFrontInfantryBack:
                AssignBlock(ranged, startRow: 0, filesPerRow, assignment.ByAgentIndex);
                assignment.NextRangedIndex = ranged.Count;
                var meleeStartRow = (ranged.Count + filesPerRow - 1) / Math.Max(1, filesPerRow);
                AssignBlock(melee, meleeStartRow, filesPerRow, assignment.ByAgentIndex);
                assignment.NextMeleeIndex = melee.Count;
                break;

            case FormationLayoutType.RangedWingsInfantryCenter:
                AssignWings(melee, ranged, filesPerRow, assignment.ByAgentIndex);
                assignment.NextMeleeIndex = melee.Count;
                assignment.NextRangedIndex = ranged.Count;
                break;

            case FormationLayoutType.Checkerboard:
                AssignCheckerboard(melee, ranged, filesPerRow, assignment.ByAgentIndex);
                assignment.NextMeleeIndex = melee.Count;
                assignment.NextRangedIndex = ranged.Count;
                break;

            default:
                // Fail-fast on a future enum value to avoid the silent empty-assignment bug
                // a 6th FormationLayoutType would introduce. Vanilla never reaches here (the
                // service short-circuits before calling EnsureAssignment). Per deep-review
                // Agent 5's future-proofing recommendation.
                throw new System.ArgumentOutOfRangeException(
                    nameof(layout),
                    layout,
                    $"LayoutPositioner.BuildInitialAssignment received unsupported layout '{layout}'. Add a case to the switch.");
        }

        return assignment;
    }

    public (int row, int file) AssignNextSlot(SlotAssignment assignment, FormationUnit unit)
    {
        var filesPerRow = Math.Max(1, assignment.FilesPerRow);
        var halfFiles = filesPerRow / 2;
        int n;
        int rowOffset;

        switch (assignment.Layout)
        {
            case FormationLayoutType.InfantryFrontRangedBack:
                if (unit.IsRanged)
                {
                    n = assignment.NextRangedIndex++;
                    rowOffset = (assignment.NextMeleeIndex + filesPerRow - 1) / filesPerRow;
                }
                else
                {
                    n = assignment.NextMeleeIndex++;
                    rowOffset = 0;
                }
                break;

            case FormationLayoutType.RangedFrontInfantryBack:
                if (unit.IsRanged)
                {
                    n = assignment.NextRangedIndex++;
                    rowOffset = 0;
                }
                else
                {
                    n = assignment.NextMeleeIndex++;
                    rowOffset = (assignment.NextRangedIndex + filesPerRow - 1) / filesPerRow;
                }
                break;

            default:
                n = unit.IsRanged ? assignment.NextRangedIndex++ : assignment.NextMeleeIndex++;
                rowOffset = (assignment.ByAgentIndex.Count + filesPerRow - 1) / filesPerRow;
                break;
        }

        var row = rowOffset + n / filesPerRow;
        var file = n % filesPerRow - halfFiles;
        return (row, file);
    }

    private static void AssignBlock(
        List<FormationUnit> group,
        int startRow,
        int filesPerRow,
        Dictionary<int, (int row, int file)> map)
    {
        if (group.Count == 0) return;
        var halfFiles = filesPerRow / 2;
        for (var i = 0; i < group.Count; i++)
        {
            var row = startRow + i / filesPerRow;
            var file = i % filesPerRow - halfFiles;
            map[group[i].Index] = (row, file);
        }
    }

    private static void AssignWings(
        List<FormationUnit> melee,
        List<FormationUnit> ranged,
        int filesPerRow,
        Dictionary<int, (int row, int file)> map)
    {
        var wingFiles = Math.Max(1, Math.Min(filesPerRow / 4, Math.Max(1, ranged.Count)));
        var centerFiles = Math.Max(1, filesPerRow - wingFiles * 2);
        var halfCenter = centerFiles / 2;

        for (var i = 0; i < melee.Count; i++)
        {
            var row = i / centerFiles;
            var file = i % centerFiles - halfCenter;
            map[melee[i].Index] = (row, file);
        }

        var leftWingStart = -(centerFiles / 2) - wingFiles;
        var rightWingStart = (centerFiles + 1) / 2;

        for (var j = 0; j < ranged.Count; j++)
        {
            var leftSide = j % 2 == 0;
            var pairIndex = j / 2;
            var row = pairIndex / wingFiles;
            var fileInWing = pairIndex % wingFiles;
            var file = leftSide ? leftWingStart + fileInWing : rightWingStart + fileInWing;
            map[ranged[j].Index] = (row, file);
        }
    }

    private static void AssignCheckerboard(
        List<FormationUnit> melee,
        List<FormationUnit> ranged,
        int filesPerRow,
        Dictionary<int, (int row, int file)> map)
    {
        var totalRows = (melee.Count + ranged.Count + filesPerRow - 1) / Math.Max(1, filesPerRow);
        var halfFiles = filesPerRow / 2;
        var meleeIdx = 0;
        var rangedIdx = 0;

        for (var row = 0; row < totalRows; row++)
        {
            for (var col = 0; col < filesPerRow; col++)
            {
                var meleeSquare = (row + col) % 2 == 0;
                FormationUnit? pick = null;

                if (meleeSquare)
                {
                    if (meleeIdx < melee.Count) pick = melee[meleeIdx++];
                    else if (rangedIdx < ranged.Count) pick = ranged[rangedIdx++];
                }
                else
                {
                    if (rangedIdx < ranged.Count) pick = ranged[rangedIdx++];
                    else if (meleeIdx < melee.Count) pick = melee[meleeIdx++];
                }

                if (!pick.HasValue) return;
                map[pick.Value.Index] = (row, col - halfFiles);
            }
        }
    }
}
