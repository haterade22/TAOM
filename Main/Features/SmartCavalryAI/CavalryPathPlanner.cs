using System;
using System.Collections.Generic;
using TaleWorlds.Library;
using TAOM.Adapters;

namespace TAOM.Features.SmartCavalryAI;

public sealed class CavalryPathPlanner : ICavalryPathPlanner
{
    private const float LateralBuffer = 4f;
    private const float MinDetourClearance = 12f;
    private const float MinAssumedFormationWidth = 6f;
    private const float TargetTooCloseLength = 1f;
    private const float TargetEndOffset = 2f;

    public bool TryGetReroutePoint(
        Vec2 cavPosition,
        Vec2 targetPosition,
        IReadOnlyList<IFormationAdapter> friendlyFormations,
        out Vec2 waypoint)
    {
        waypoint = Vec2.Zero;
        if (friendlyFormations == null || friendlyFormations.Count == 0) return false;

        var toTarget = targetPosition - cavPosition;
        var length = toTarget.Length;
        if (length < TargetTooCloseLength) return false;

        var dir = toTarget * (1f / length);
        var right = dir.RightVec();

        IFormationAdapter? closest = null;
        var closestProjDir = 0f;

        foreach (var formation in friendlyFormations)
        {
            if (formation == null) continue;
            if (formation.CountOfUnits == 0) continue;
            if (formation.RepresentativeIsCavalry) continue;

            var offset = formation.CurrentPosition - cavPosition;
            var projDir = Vec2.DotProduct(offset, dir);
            if (projDir < 0f || projDir > length - TargetEndOffset) continue;

            var projRightAbs = Math.Abs(Vec2.DotProduct(offset, right));
            var halfWidth = Math.Max(formation.Width, MinAssumedFormationWidth) * 0.5f;
            if (projRightAbs > halfWidth + LateralBuffer) continue;

            if (closest == null || projDir < closestProjDir)
            {
                closestProjDir = projDir;
                closest = formation;
            }
        }

        if (closest == null) return false;

        var closestOffset = closest.CurrentPosition - cavPosition;
        var signedProjRight = Vec2.DotProduct(closestOffset, right);
        var clearance = Math.Max(closest.Width, MinAssumedFormationWidth) * 0.5f + MinDetourClearance;
        var sign = signedProjRight <= 0f ? 1f : -1f;
        var sidestepCenter = closest.CurrentPosition + right * (clearance * sign);
        waypoint = sidestepCenter + dir * LateralBuffer;
        return true;
    }
}
