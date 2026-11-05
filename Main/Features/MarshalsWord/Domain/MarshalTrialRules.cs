using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace TAOM.Features.MarshalsWord.Domain;

/// <summary>A drag line for the trial, its two ends, and the walk of its nearest and farthest company.</summary>
internal readonly record struct TrialDrag(float BeginX, float BeginY, float EndX, float EndY, float Near, float Far);

/// <summary>
/// The drag of <c>array_keeps_offsets</c>: how far the chosen companies stand apart along their facing, and the shift (a vector on the ground plane, in
/// metres) that the dragged line makes of the group's own front, which every company's order point must follow.
/// </summary>
internal readonly record struct FrontDrag(float Stagger, float ShiftX, float ShiftY);

/// <summary>The numbers and judgments the Marshal's Word field trial rests on, with no engine type.</summary>
internal static class MarshalTrialRules
{
    /// <summary>The distance of each leg of <c>queue_two_moves</c> and of the move that staggers two companies, in metres.</summary>
    internal const float LegMetres = 25f;

    /// <summary>Real seconds a company may be later at its second point than at its first for the steps to count as run in order.</summary>
    internal const float LegGapSeconds = 2f;

    /// <summary>The stagger the array step needs before its drag: under this, vanilla's side by side layout would leave the offset inside the tolerance too.</summary>
    internal const float StaggerFloorMetres = 15f;

    /// <summary>How far a company's offset from the other may move in <c>array_keeps_offsets</c>.</summary>
    internal const float OffsetToleranceMetres = 3f;

    /// <summary>The ratio of the two walks that <c>arrive_together</c> aims for (build-spec 11).</summary>
    internal const float TargetRatio = 2.5f;

    /// <summary>A plan is used only inside these ratios: outside them the march's proof (6.5) would not be tested at about 2.5 to 1.</summary>
    internal const float RatioLow = 2.0f;

    internal const float RatioHigh = 3.2f;

    /// <summary>The far company must be at least this many seconds from the arrival slack at its speed: the proof's floor of 15 s and a margin for a first read that is a little fast.</summary>
    internal const float FarSeconds = 17f;

    /// <summary>The far company's walk may take at most this many seconds at its speed: a longer one takes the group into the enemy before the march ends.</summary>
    internal const float FarSecondsMax = 30f;

    /// <summary>Where <c>arrive_together</c> stands its second company before it plans: this far behind the first, and <see cref="ColumnSideMetres"/> to its right.</summary>
    internal const float ColumnBehindMetres = 45f;

    internal const float ColumnSideMetres = 20f;

    /// <summary>How far <c>array_keeps_offsets</c> drags the group's front sideways along itself, in metres.</summary>
    internal const float ShiftMetres = 20f;

    /// <summary>The shift the array step needs: under this, a build that computes the layout and never applies it would move the order points inside the tolerance too.</summary>
    internal const float ShiftFloorMetres = 15f;

    /// <summary>Metres from the chosen archers to the enemy company the mark steps wait for before they mark it: inside a bow's reach, where a soldier has a target.</summary>
    internal const float ArcherReachMetres = 70f;

    /// <summary>The gap the volley steps walk their archers to, between the archers and the nearest enemy company, once both have walked: inside a bow's reach.</summary>
    internal const float VolleyReachMetres = 80f;

    /// <summary>Mission seconds the volley control counts the archers' shots at free fire, before the hold begins.</summary>
    internal const float ControlSeconds = 10f;

    /// <summary>The farthest candidate drag the plan tries, in metres ahead of the group, and its step.</summary>
    internal const float PlanReachMetres = 260f;

    internal const float PlanStepMetres = 5f;

    /// <summary>The two points are reached in order: the first at least <see cref="LegGapSeconds"/> before the second, and both real numbers.</summary>
    internal static bool ReachedInOrder(float first, float second) => IsReal(first) && IsReal(second) && second - first >= LegGapSeconds;

    /// <summary>The companies' offset from each other kept: it moved by no more than <see cref="OffsetToleranceMetres"/>.</summary>
    internal static bool OffsetKept(float beforeX, float beforeY, float afterX, float afterY)
    {
        var shift = OffsetShift(beforeX, beforeY, afterX, afterY);
        return IsReal(shift) && shift <= OffsetToleranceMetres;
    }

    internal static float OffsetShift(float beforeX, float beforeY, float afterX, float afterY)
    {
        if (!IsReal(beforeX) || !IsReal(beforeY) || !IsReal(afterX) || !IsReal(afterY))
            return float.NaN;
        var dx = (double)afterX - beforeX;
        var dy = (double)afterY - beforeY;
        return (float)Math.Sqrt(dx * dx + dy * dy);
    }

    /// <summary>The companies stand far enough apart along the front for a side by side layout to move one of them: a stagger under the floor proves nothing.</summary>
    internal static bool StaggerProves(float staggerMetres) => IsReal(staggerMetres) && staggerMetres >= StaggerFloorMetres;

    /// <summary>The shift a drag makes needs a length of <see cref="ShiftFloorMetres"/> or more, in real numbers.</summary>
    internal static bool ShiftProves(float shiftX, float shiftY)
    {
        if (!IsReal(shiftX) || !IsReal(shiftY))
            return false;
        return Math.Sqrt((double)shiftX * shiftX + (double)shiftY * shiftY) >= ShiftFloorMetres;
    }

    /// <summary>The front line moved <paramref name="metres"/> along itself, from its begin to its end, and the shift that is; null for a line with no length or a value that is not real.</summary>
    internal static (float BeginX, float BeginY, float EndX, float EndY, float ShiftX, float ShiftY)? ShiftAlong(
        (float BeginX, float BeginY, float EndX, float EndY) line, float metres)
    {
        if (!IsReal(line.BeginX) || !IsReal(line.BeginY) || !IsReal(line.EndX) || !IsReal(line.EndY) || !IsReal(metres))
            return null;
        var dx = (double)line.EndX - line.BeginX;
        var dy = (double)line.EndY - line.BeginY;
        var length = Math.Sqrt(dx * dx + dy * dy);
        if (length < 1e-9)
            return null;
        var shiftX = (float)(dx / length * metres);
        var shiftY = (float)(dy / length * metres);
        return (line.BeginX + shiftX, line.BeginY + shiftY, line.EndX + shiftX, line.EndY + shiftY, shiftX, shiftY);
    }

    /// <summary>How far a company that stood at <c>before</c> ended from where the shift should have put it: 0 when it moved by exactly the shift, NaN for a value that is not real.</summary>
    internal static float ShiftMiss(float beforeX, float beforeY, float shiftX, float shiftY, float afterX, float afterY) =>
        OffsetShift(beforeX + shiftX, beforeY + shiftY, afterX, afterY);

    /// <summary>A company followed the shift when it ended within <see cref="OffsetToleranceMetres"/> of the place the shift put it.</summary>
    internal static bool ShiftFollowed(float miss) => IsReal(miss) && miss <= OffsetToleranceMetres;

    /// <summary>
    /// The group's own front, flank to flank: the line the solver treats as the front of the group (build-spec 6.2), so a drag along it must
    /// change no company's place. Null when the group has no length or an input is not a real number.
    /// </summary>
    internal static (float BeginX, float BeginY, float EndX, float EndY)? FrontLine(IReadOnlyList<ArrayCompany> companies)
    {
        var shape = ShapeOf(companies);
        if (shape == null)
            return null;
        var s = shape.Value;
        var begin = At(s, s.Low, s.MaxAlong);
        var end = At(s, s.High, s.MaxAlong);
        return (begin.X, begin.Y, end.X, end.Y);
    }

    /// <summary>A drag that turns the group a quarter ahead of it, with the walks of its nearest and farthest company about 2.5 to 1 and the farthest between the floor and the cap, or null.</summary>
    internal static TrialDrag? PlanArrival(IReadOnlyList<ArrayCompany> companies, float speed, float bipedalRadius)
    {
        var shape = ShapeOf(companies);
        if (shape == null || !IsReal(speed) || speed <= 0f || companies.Count < 2)
            return null;
        var s = shape.Value;
        TrialDrag? best = null;
        var bestMiss = double.MaxValue;
        foreach (var sign in new[] { 1f, -1f })
        {
            for (var ahead = PlanStepMetres; ahead <= PlanReachMetres; ahead += PlanStepMetres)
            {
                var candidate = Candidate(companies, s, sign, ahead, bipedalRadius);
                if (candidate == null)
                    continue;
                var drag = candidate.Value;
                var ratio = drag.Far / Math.Max(drag.Near, 0.001f);
                var farSeconds = (drag.Far - MarshalTuning.ArrivalSlack) / speed;
                if (ratio < RatioLow || ratio > RatioHigh || farSeconds < FarSeconds || farSeconds > FarSecondsMax)
                    continue;
                var miss = Math.Abs(ratio - TargetRatio);
                if (miss < bestMiss)
                {
                    bestMiss = miss;
                    best = drag;
                }
            }
        }

        return best;
    }

    /// <summary>The metres to walk toward an enemy that walks toward you, to leave about <paramref name="reach"/> between you once both have walked; NaN for a value that is not real.</summary>
    internal static float ApproachWalk(float gap, float reach, float ownSpeed, float enemySpeed)
    {
        if (!IsReal(gap) || !IsReal(reach) || !IsReal(ownSpeed) || ownSpeed <= 0f || !IsReal(enemySpeed) || enemySpeed < 0f)
            return float.NaN;
        return Math.Max(0f, (gap - reach) / (1f + enemySpeed / ownSpeed));
    }

    /// <summary>The line a step writes for a plan, to put the numbers a reader needs beside the verdict.</summary>
    internal static string Describe(TrialDrag drag) =>
        "near=" + drag.Near.ToString("F1", CultureInfo.InvariantCulture) + " far=" + drag.Far.ToString("F1", CultureInfo.InvariantCulture);

    /// <summary>Enemy soldiers that must stand within <see cref="HoldReachMetres"/> of the held company before a hold-the-line step reads its result as proved.</summary>
    internal const int HoldMinEnemiesInReach = 10;

    /// <summary>The metres within which an enemy soldier counts as in reach of a company in the hold-the-line step.</summary>
    internal const float HoldReachMetres = 15f;

    /// <summary>The Massed rule's own answer for the numbers a word was given on, as if no word had been given before.</summary>
    internal static bool MassedWordWasDue(int holding, int eligible) =>
        MarshalFireRules.MassedWordDue(holding, eligible, now: MarshalTuning.WordGap, lastWord: 0.0);

    /// <summary>
    /// Massed fire passed: a word was given by the share rule on enough holding soldiers to prove a release, the share was the rule's, and the archers loosed. The share is
    /// read again here from the numbers on the log line, so a word given below the share cannot pass.
    /// </summary>
    internal static bool MassedPasses(int words, int holding, int eligible, bool releasePassed, bool faulted) =>
        words >= 1 && holding >= MarshalTuning.VolleyMinHolding && MassedWordWasDue(holding, eligible) && releasePassed && !faulted;

    /// <summary>The walk of an approach is waited out for at most this multiple of its time at the company's speed, because a line gathers itself before it walks.</summary>
    internal const float ApproachSlackFactor = 2f;

    /// <summary>The least real seconds an approach is waited for, the time a company needs to turn and set off.</summary>
    internal const float ApproachFloorSeconds = 15f;

    /// <summary>The most real seconds an approach is waited for, whatever the walk.</summary>
    internal const float ApproachCeilingSeconds = 150f;

    /// <summary>The slowest speed an approach is planned at, in metres a second, because the engine's cached speed has a floor of 0.1.</summary>
    internal const float ApproachMinSpeed = 1f;

    /// <summary>The real seconds a settle waits for after an order that walks <paramref name="walkMetres"/> at <paramref name="speed"/>; the ceiling when the walk is not a number.</summary>
    internal static float ApproachWaitSeconds(float walkMetres, float speed)
    {
        if (!IsReal(walkMetres) || walkMetres < 0f)
            return ApproachCeilingSeconds;
        var planned = IsReal(speed) && speed > ApproachMinSpeed ? speed : ApproachMinSpeed;
        return Math.Min(ApproachCeilingSeconds, ApproachFloorSeconds + ApproachSlackFactor * walkMetres / planned);
    }

    /// <summary>The unit widths a drag asks the square's side to differ from the side it had.</summary>
    internal const float SquareDragUnits = 4f;

    /// <summary>A side to ask of the square: several unit widths over the side it has, or under it when the maximum is too near, never outside its bounds. NaN when neither fits.</summary>
    internal static float SideTarget(float before, float minimum, float maximum, float unitWidth)
    {
        if (!IsReal(before) || !IsReal(minimum) || !IsReal(maximum) || !IsReal(unitWidth) || unitWidth <= 0f || maximum < minimum)
            return float.NaN;
        var step = SquareDragUnits * unitWidth;
        if (before + step <= maximum)
            return before + step;
        return before - step >= minimum ? before - step : float.NaN;
    }

    /// <summary>The side after the drag is more than one unit's width from the side before it, so a drag that did nothing reads false.</summary>
    internal static bool SideMoved(float before, float after, float unitWidth) =>
        IsReal(before) && IsReal(after) && IsReal(unitWidth) && unitWidth > 0f && Math.Abs(after - before) > unitWidth;

    /// <summary>Why the square step failed, in the order the causes happen: not the plain square, soldiers the mixed layout places, a drag that moved nothing, and the side, the removal and the slots after it. Empty when it passed.</summary>
    internal static string SquareTrialReason(bool plain, bool laidOut, bool dragMoved, bool askedReached, bool sideKept, int placed, int units, int lost)
    {
        if (!plain)
            return "not-the-plain-square";
        if (laidOut)
            return "laid-out";
        if (!dragMoved)
            return "drag-did-nothing";
        if (!askedReached)
            return "side-not-reached";
        if (lost < 1)
            return "nobody-removed";
        if (!sideKept)
            return "side-not-kept";
        return MarshalSquareRules.ProofPasses(placed, units) ? "" : "unplaced";
    }

    /// <summary>Real seconds the hold-the-line steps wait for the line's judged result after the hold goes on.</summary>
    internal const float HoldVerdictWaitSeconds = 150f;

    /// <summary>Real seconds after a charge within which every held soldier's attack flag must read on again.</summary>
    internal const float HoldRestoreSeconds = 1.5f;

    /// <summary>Hold reads the slot step needs after the clamp's first steer, enough for the driver's slot windows to have judged them.</summary>
    internal const int SlotMinSamples = 45;

    /// <summary>Why the hold-the-line proof step failed, resting on the driver's judged verdict for this mission; empty when it passed.</summary>
    internal static string HoldVerdictReason(HoldVerdict? driver, int peakEnemiesNear, bool restored, int flagOffNow)
    {
        if (driver == null)
            return flagOffNow < MarshalTuning.HoldMinFlagOff ? "flag-not-off" : "driver-never-judged";
        if (peakEnemiesNear < HoldMinEnemiesInReach)
            return "enemy-not-in-reach";
        switch (driver.Value.Proof)
        {
            case HoldProof.Pending:
                return "flag-not-off";
            case HoldProof.NoControl:
                return "control-quiet";
            case HoldProof.Fail:
                return "held-company-struck";
        }

        return restored ? "" : "flag-not-restored";
    }

    /// <summary>
    /// Why the slot step failed, ground first: the driver's slot check failed or the totals of the whole hold moved more soldiers away than toward their slot, the clamp
    /// never replaced an input, too few hold reads followed the first steer, or too few reads were steered at all. Empty when the clamp acted and walked the right way.
    /// </summary>
    internal static string SlotTrialReason(int samples, bool slotFailed, int steered, int closer, int farther, string order, int qualified, int anchored, int woundedEver,
        int peakEnemiesNear, int clamps)
    {
        if (qualified < MarshalTuning.HoldMinFlagOff)
            return "not-qualified";
        if (order != MarshalHoldLineRules.StopOrder)
            return "order-not-stop";
        if (peakEnemiesNear < HoldMinEnemiesInReach)
            return "enemy-not-in-reach";
        if (anchored * 2 < qualified)
            return "no-anchor";
        if (woundedEver < 1)
            return "nobody-struck";
        var totals = MarshalHoldLineRules.TrendProof(steered, closer, farther);
        if (slotFailed || totals == HoldProof.Fail)
            return "steered-away";
        if (clamps < 1)
            return "no-clamp";
        if (samples < SlotMinSamples)
            return "too-few-samples";
        return totals == HoldProof.Pending ? "too-few-steers" : "";
    }

    /// <summary>Why the strike back step failed: the line was never disarmed before a wound, no soldier was wounded, no wound settled in the window, the flag stayed off for the wounded, or no blow followed.</summary>
    internal static string StrikeTrialReason(int flagOffBeforeWound, int woundedEver, int settledPeak, int flagOnAtPeak, int blowsAfterWound)
    {
        if (flagOffBeforeWound < MarshalTuning.HoldMinFlagOff)
            return "flag-not-off";
        if (woundedEver < 1)
            return "nobody-wounded";
        if (settledPeak < 1)
            return "strike-window-missed";
        if (MarshalHoldLineRules.StrikeProof(settledPeak, flagOnAtPeak) != HoldProof.Pass)
            return "flag-stayed-off";
        return blowsAfterWound < 1 ? "no-blow-after-wound" : "";
    }

    /// <summary>Why the charge step failed: the line never stood, the flags never read off, or they did not come back in time.</summary>
    internal static string FlagTrialReason(int qualified, int flagOffBefore, bool restored)
    {
        if (qualified < MarshalTuning.HoldMinFlagOff)
            return "not-qualified";
        if (flagOffBefore < MarshalTuning.HoldMinFlagOff)
            return "flag-not-off";
        return restored ? "" : "flag-not-restored";
    }

    /// <summary>Why the hold-the-line step has no ground to stand on: the split moved no soldier into the control, or a company is not the player's. Empty when it has.</summary>
    internal static string HoldGroundReason(int heldUnits, int controlUnits, bool heldControllable, bool controlControllable)
    {
        if (heldUnits < 1 || controlUnits < 1)
            return "no-split";
        return heldControllable && controlControllable ? "" : "not-controllable";
    }

    /// <summary>Why the spacing step failed, ground first: the rider joined no one, the soldier who joined is on foot, or the engine's own count missed the rider. Empty when it passed.</summary>
    internal static string SpacingTrialReason(int before, int units, int riders, bool any, bool significant)
    {
        if (units <= before)
            return "rider-not-moved";
        if (riders < 1)
            return "no-rider-in-company";
        return any && !significant ? "" : "rider-not-counted";
    }

    /// <summary>The side after a change is within one unit's width of the side before it.</summary>
    internal static bool SideKept(float before, float after, float unitWidth) =>
        IsReal(before) && IsReal(after) && IsReal(unitWidth) && unitWidth > 0f && Math.Abs(after - before) <= unitWidth;

    private static TrialDrag? Candidate(IReadOnlyList<ArrayCompany> companies, Shape s, float sign, float ahead, float bipedalRadius)
    {
        // The line runs along the group's forward (or back), a quarter turn from its front, as long as the group is wide, ahead of the group.
        var centre = At(s, (s.Low + s.High) / 2f, s.MaxAlong + ahead);
        var half = (s.High - s.Low) / 2f;
        var beginX = centre.X - sign * s.ForwardX * half;
        var beginY = centre.Y - sign * s.ForwardY * half;
        var endX = centre.X + sign * s.ForwardX * half;
        var endY = centre.Y + sign * s.ForwardY * half;
        var solved = MarshalArraySolver.Solve(companies, beginX, beginY, endX, endY, keepWidths: true, bipedalRadius);
        if (!solved.Ok || solved.IsLineShort || solved.Placements.Count != companies.Count)
            return null;

        var walks = new List<float>(companies.Count);
        for (var i = 0; i < companies.Count; i++)
        {
            var dx = (double)solved.Placements[i].X - companies[i].X;
            var dy = (double)solved.Placements[i].Y - companies[i].Y;
            walks.Add((float)Math.Sqrt(dx * dx + dy * dy));
        }

        return walks.All(IsReal) ? new TrialDrag(beginX, beginY, endX, endY, walks.Min(), walks.Max()) : null;
    }

    private readonly record struct Shape(float OriginX, float OriginY, float ForwardX, float ForwardY, float RightX, float RightY, float Low, float High,
        float MaxAlong);

    // The solver's own frame (6.2): origin is the unit-weighted centre, forward the facing of the company with most units, right forward turned a quarter clockwise.
    private static Shape? ShapeOf(IReadOnlyList<ArrayCompany> companies)
    {
        if (companies.Count == 0 || companies.Any(c => !IsReal(c.X) || !IsReal(c.Y) || !IsReal(c.Width) || !IsReal(c.DirX) || !IsReal(c.DirY) || c.Units <= 0))
            return null;
        var units = companies.Sum(c => (double)c.Units);
        var originX = companies.Sum(c => c.X * (double)c.Units) / units;
        var originY = companies.Sum(c => c.Y * (double)c.Units) / units;
        var largest = companies.Aggregate((a, b) => b.Units > a.Units ? b : a);
        var length = Math.Sqrt((double)largest.DirX * largest.DirX + (double)largest.DirY * largest.DirY);
        if (length < 1e-9)
            return null;
        var fx = largest.DirX / length;
        var fy = largest.DirY / length;
        var rx = fy;
        var ry = -fx;
        var low = double.MaxValue;
        var high = double.MinValue;
        var maxAlong = double.MinValue;
        foreach (var c in companies)
        {
            var dx = c.X - originX;
            var dy = c.Y - originY;
            var across = dx * rx + dy * ry;
            low = Math.Min(low, across - c.Width / 2.0);
            high = Math.Max(high, across + c.Width / 2.0);
            maxAlong = Math.Max(maxAlong, dx * fx + dy * fy);
        }

        return high > low ? new Shape((float)originX, (float)originY, (float)fx, (float)fy, (float)rx, (float)ry, (float)low, (float)high, (float)maxAlong) : null;
    }

    private static (float X, float Y) At(Shape s, float across, float along) =>
        (s.OriginX + s.RightX * across + s.ForwardX * along, s.OriginY + s.RightY * across + s.ForwardY * along);

    private static bool IsReal(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
