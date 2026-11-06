using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Features.FordsOfWar;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Features.LongDefeat.Domain;

/// <summary>What <see cref="VolleyAim.Plan"/> decided: the centre it chose (null when there was none), the shots in arrow order, and, when there are none, why.</summary>
internal sealed record VolleyAimResult(Vec3f? Centre, IReadOnlyList<LdVolleyShot> Shots, VolleyWithheldReason? Why)
{
    public static VolleyAimResult Withheld(VolleyWithheldReason why) => new(null, Array.Empty<LdVolleyShot>(), why);
}

/// <summary>
/// The one aiming routine of the Elven Volley and the level 3 tower volley (phase3-plan.md D23), pure so a test can pin it. Candidates are the attackers the adapter read (standing and not running away)
/// that lie within <c>range</c> of the nearest origin. The automatic centre is the candidate with the most candidates within <c>radius</c> of it on the ground, ties going to the one nearer an origin;
/// the crosshair centre is the point the caller's ray hit. The arrows take the candidates nearest that centre first, one attacker each, then wrap. Each arrow leaves the origin nearest its target, raised
/// by <c>originHeight</c>, and is aimed at the target's chest point, led by its horizontal velocity over the flight time (two passes) on the flat arc of <see cref="FordsTowerGeometry.AimDirection"/>.
/// A number that is not finite is refused: an origin or a candidate with one is dropped, and a bad scalar withholds the whole volley, so no NaN direction reaches the engine.
/// </summary>
internal static class VolleyAim
{
    private const float MinHorizontal = 1e-3f;
    private const int LeadPasses = 2;

    public static VolleyAimResult Plan(IReadOnlyList<LdPoint> origins, float originHeight, IReadOnlyList<LdVolleyTarget> targets, VolleyAimMode mode,
        Vec3f? rayHit, int arrows, float radius, float range, float speed)
    {
        if (arrows < 1 || !(radius > 0f) || !(range > 0f) || !(speed > 0f) || float.IsInfinity(radius) || float.IsInfinity(range) || float.IsInfinity(speed))
            return VolleyAimResult.Withheld(VolleyWithheldReason.NoTargets);

        var raised = !IsFinite(originHeight)
            ? new List<Vec3f>()
            : origins.Where(o => IsFinite(o.X) && IsFinite(o.Y) && IsFinite(o.Z)).Select(o => new Vec3f(o.X, o.Y, o.Z + originHeight)).ToList();
        if (raised.Count == 0)
            return VolleyAimResult.Withheld(VolleyWithheldReason.NoOrigin);

        var candidates = targets
            .Where(t => IsFinite(t.Chest) && IsFinite(t.Velocity))
            .Select(t => new Candidate(t, NearestOrigin(raised, t.Chest)))
            .Where(c => c.Reach <= range)
            .ToList();
        var centre = mode == VolleyAimMode.Crosshair ? Crosshair(rayHit, candidates) : Densest(candidates, radius);
        if (candidates.Count == 0 || centre == null)
            return VolleyAimResult.Withheld(VolleyWithheldReason.NoTargets);

        var middle = centre.Value;
        var order = candidates.OrderBy(c => c.Target.Chest.Distance2D(middle)).ToList();
        var shots = new List<LdVolleyShot>(arrows);
        for (var i = 0; i < arrows; i++)
        {
            var target = order[i % order.Count].Target;
            var origin = raised.OrderBy(o => o.Distance(target.Chest)).First();
            var direction = Lead(origin, target, speed);
            if (direction != null)
                shots.Add(new LdVolleyShot(origin, direction.Value, speed, target.Key));
        }

        return shots.Count == 0 ? VolleyAimResult.Withheld(VolleyWithheldReason.NoTargets) : new VolleyAimResult(middle, shots, null);
    }

    private readonly record struct Candidate(LdVolleyTarget Target, float Reach);

    private static float NearestOrigin(IReadOnlyList<Vec3f> origins, Vec3f point) => origins.Min(o => o.Distance(point));

    private static Vec3f? Crosshair(Vec3f? rayHit, IReadOnlyList<Candidate> candidates) =>
        rayHit is { } hit && IsFinite(hit) && candidates.Count > 0 ? hit : null;

    /// <summary>The chest point of the candidate with the most others on the ground within the radius; ties go to the one nearer an origin, then to the earlier in the list.</summary>
    private static Vec3f? Densest(IReadOnlyList<Candidate> candidates, float radius)
    {
        Candidate? best = null;
        var bestCount = 0;
        foreach (var c in candidates)
        {
            var count = candidates.Count(o => o.Target.Chest.Distance2D(c.Target.Chest) <= radius);
            if (count > bestCount || (count == bestCount && best != null && c.Reach < best.Value.Reach))
            {
                best = c;
                bestCount = count;
            }
        }

        return best?.Target.Chest;
    }

    /// <summary>
    /// The direction that meets the target: aim at the chest, take the flight time of that shot, move the aim point by the velocity over it, and do it once more.
    /// Velocity is horizontal (the engine's z is 0), so the lead moves the aim point on the ground plane only.
    /// </summary>
    private static Vec3f? Lead(Vec3f origin, LdVolleyTarget target, float speed)
    {
        var aim = target.Chest;
        for (var pass = 0; pass < LeadPasses; pass++)
        {
            var time = FlightSeconds(origin, aim, speed);
            if (time == null)
                return null;

            aim = new Vec3f(target.Chest.X + target.Velocity.X * time.Value, target.Chest.Y + target.Velocity.Y * time.Value, target.Chest.Z);
        }

        var direction = FordsTowerGeometry.AimDirection(origin, aim, speed);
        return direction is { } d && IsFinite(d) ? d : null;
    }

    /// <summary>Seconds a missile of <paramref name="speed"/> takes from the origin to the aim point along the flat arc: ground distance over the horizontal speed.</summary>
    private static float? FlightSeconds(Vec3f origin, Vec3f aim, float speed)
    {
        var direction = FordsTowerGeometry.AimDirection(origin, aim, speed);
        if (direction == null)
            return null;

        var horizontal = (float)Math.Sqrt(direction.Value.X * direction.Value.X + direction.Value.Y * direction.Value.Y);
        var seconds = horizontal > MinHorizontal
            ? origin.Distance2D(aim) / (speed * horizontal)
            : Math.Abs(aim.Z - origin.Z) / speed;
        return IsFinite(seconds) ? seconds : null;
    }

    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    private static bool IsFinite(Vec3f v) => IsFinite(v.X) && IsFinite(v.Y) && IsFinite(v.Z);
}
