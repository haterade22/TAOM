using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.FordsOfWar.Domain;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Tests.Features.LongDefeat.Domain;

/// <summary>
/// The one aiming routine of the Elven Volley and the level 3 tower volley (phase3-plan.md D23): the densest group of attackers in reach, or the crosshair's ray hit, a distinct attacker for each arrow
/// then the next nearest and a wrap, each arrow leaving the origin nearest its target and led onto the chest point under the engine's gravity.
/// </summary>
[TestClass]
public class VolleyAimTests
{
    private const float Speed = 45f;
    private const float EngineGravity = 9.806f;

    private static readonly LdPoint[] OneOrigin = { new(0f, 0f, 10f) };

    private static LdVolleyTarget At(int key, float x, float y, float z = 1.3f, float vx = 0f, float vy = 0f) =>
        new(new AgentKey(key), new Vec3f(x, y, z), new Vec3f(vx, vy, 0f));

    private static VolleyAimResult Aim(IReadOnlyList<LdVolleyTarget> targets, IReadOnlyList<LdPoint>? origins = null, VolleyAimMode mode = VolleyAimMode.Auto,
        Vec3f? rayHit = null, int arrows = 6, float radius = 6f, float range = 90f, float speed = Speed, float originHeight = 2.5f) =>
        VolleyAim.Plan(origins ?? OneOrigin, originHeight, targets, mode, rayHit, arrows, radius, range, speed);

    private static int[] Keys(VolleyAimResult result) => result.Shots.Select(s => s.Target.Value).ToArray();

    // ---- the densest group ----

    [TestMethod]
    public void Auto_CentresOnTheDensestGroup_NotTheNearestAttacker()
    {
        var targets = new[]
        {
            At(1, 20f, 0f),
            At(2, 60f, 0f), At(3, 62f, 1f), At(4, 61f, -2f), At(5, 63f, 0f),
            At(6, 40f, 30f), At(7, 41f, 31f),
        };

        var result = Aim(targets);

        Assert.IsNull(result.Why);
        Assert.IsTrue(result.Centre!.Value.Distance2D(new Vec3f(61f, 0f, 1.3f)) < 3f, "the four at x 60 to 63 are the densest group: " + result.Centre);
        CollectionAssert.AreEquivalent(new[] { 2, 3, 4, 5, 6, 7 }, Keys(result), "the four of the group first, then the next nearest the centre");
    }

    [TestMethod]
    public void Auto_CountsAGroupOnTheGround_NotByHeight()
    {
        var targets = new[]
        {
            At(1, 50f, 0f, 1.3f), At(2, 51f, 0f, 9f), At(3, 50f, 1f, 6f),
            At(4, 70f, 0f), At(5, 71f, 0f),
        };

        var result = Aim(targets);

        Assert.IsTrue(result.Centre!.Value.Distance2D(new Vec3f(50f, 0f, 0f)) < 2f, "three at x 50 including two on a ladder: " + result.Centre);
    }

    [TestMethod]
    public void Auto_TiesGoToTheGroupNearerAnOrigin_WhateverTheListOrder()
    {
        var near = new[] { At(1, 30f, 0f), At(2, 31f, 0f) };
        var far = new[] { At(3, 60f, 5f), At(4, 61f, 5f) };

        var forward = Aim(near.Concat(far).ToArray(), arrows: 1);
        var reversed = Aim(far.Concat(near).ToArray(), arrows: 1);

        Assert.AreEqual(1, Keys(forward)[0], "two against two: the nearer pair, and within it the attacker nearer the origin");
        Assert.AreEqual(Keys(forward)[0], Keys(reversed)[0]);
        Assert.AreEqual(forward.Centre, reversed.Centre);
    }

    [TestMethod]
    public void Auto_AnEqualGroupIsMeasuredFromTheNearestOfSeveralOrigins()
    {
        var origins = new[] { new LdPoint(0f, 0f, 10f), new LdPoint(100f, 0f, 10f) };
        var targets = new[] { At(1, 30f, 0f), At(2, 31f, 0f), At(3, 80f, 0f), At(4, 81f, 0f) };

        var result = Aim(targets, origins, arrows: 1);

        Assert.AreEqual(4, Keys(result)[0], "the pair at 80 is 22 m from the second origin, nearer than the pair at 30 from the first; the one at 81 is the nearer of the two");
    }

    [TestMethod]
    public void Auto_ASingleAttacker_IsTheCentreAndEveryArrowsTarget()
    {
        var result = Aim(new[] { At(9, 40f, 5f) }, arrows: 4);

        CollectionAssert.AreEqual(new[] { 9, 9, 9, 9 }, Keys(result));
    }

    // ---- the crosshair ----

    [TestMethod]
    public void Crosshair_CentresOnTheRayHit_NotOnTheDensestGroup()
    {
        var targets = new[]
        {
            At(1, 60f, 0f), At(2, 61f, 0f), At(3, 62f, 0f), At(4, 61f, 1f),
            At(5, 40f, 40f),
        };
        var hit = new Vec3f(40f, 41f, 0f);

        var result = Aim(targets, mode: VolleyAimMode.Crosshair, rayHit: hit, arrows: 1);

        Assert.AreEqual(hit, result.Centre);
        CollectionAssert.AreEqual(new[] { 5 }, Keys(result), "the attacker the player looks at, though four stand together elsewhere");
    }

    [TestMethod]
    public void Crosshair_WithNoRayHit_WithholdsForWantOfATarget()
    {
        var result = Aim(new[] { At(1, 40f, 0f) }, mode: VolleyAimMode.Crosshair, rayHit: null);

        Assert.AreEqual(0, result.Shots.Count);
        Assert.IsNull(result.Centre);
        Assert.AreEqual(VolleyWithheldReason.NoTargets, result.Why);
    }

    [TestMethod]
    public void Crosshair_AHitThatIsNotANumber_IsNoHit()
    {
        var result = Aim(new[] { At(1, 40f, 0f) }, mode: VolleyAimMode.Crosshair, rayHit: new Vec3f(float.NaN, 0f, 0f));

        Assert.AreEqual(0, result.Shots.Count);
        Assert.AreEqual(VolleyWithheldReason.NoTargets, result.Why);
    }

    // ---- which attacker each arrow takes ----

    [TestMethod]
    public void EachArrowTakesItsOwnAttacker_NearestTheCentreFirst()
    {
        var targets = new[] { At(1, 50f, 0f), At(2, 52f, 0f), At(3, 54f, 0f), At(4, 56f, 0f), At(5, 58f, 0f) };

        var result = Aim(targets, mode: VolleyAimMode.Crosshair, rayHit: new Vec3f(53.5f, 0f, 0f), arrows: 3);

        CollectionAssert.AreEqual(new[] { 3, 2, 4 }, Keys(result), "x 54 (0.5 away), x 52 (1.5) and x 56 (2.5): nearest first by ground distance from the centre");
    }

    [TestMethod]
    public void WithMoreArrowsThanAttackers_TheNextNearestFollow_ThenItWraps()
    {
        var targets = new[] { At(1, 50f, 0f), At(2, 51f, 0f), At(3, 52f, 0f), At(4, 80f, 0f), At(5, 30f, 0f) };

        var result = Aim(targets, mode: VolleyAimMode.Crosshair, rayHit: new Vec3f(51f, 0f, 0f), arrows: 12);

        var keys = Keys(result);
        CollectionAssert.AreEquivalent(new[] { 1, 2, 3, 4, 5 }, keys.Take(5).ToArray(), "one distinct attacker per arrow while attackers last, those beyond the radius included");
        Assert.AreEqual(2, keys[0], "the one on the centre first");
        CollectionAssert.AreEqual(keys.Take(5).ToArray(), keys.Skip(5).Take(5).ToArray(), "then the same order again");
        CollectionAssert.AreEqual(keys.Take(2).ToArray(), keys.Skip(10).ToArray());
        Assert.AreEqual(12, keys.Length);
    }

    [TestMethod]
    public void ADenseGroupOfThree_WithTwelveArrows_AimsFourArrowsAtEach()
    {
        var targets = new[] { At(1, 50f, 0f), At(2, 52f, 0f), At(3, 51f, 2f) };

        var result = Aim(targets, arrows: 12);

        foreach (var key in new[] { 1, 2, 3 })
            Assert.AreEqual(4, Keys(result).Count(k => k == key));
    }

    // ---- origin, direction and lead ----

    [TestMethod]
    public void EachArrowLeavesTheOriginNearestItsTarget_RaisedByTheOriginHeight()
    {
        var origins = new[] { new LdPoint(0f, 0f, 10f), new LdPoint(100f, 0f, 10f) };
        var targets = new[] { At(1, 30f, 0f), At(2, 80f, 0f) };

        var result = Aim(targets, origins, mode: VolleyAimMode.Crosshair, rayHit: new Vec3f(55f, 0f, 0f), arrows: 2, originHeight: 3f);

        var byTarget = result.Shots.ToDictionary(s => s.Target.Value, s => s.Origin);
        Assert.AreEqual(new Vec3f(0f, 0f, 13f), byTarget[1]);
        Assert.AreEqual(new Vec3f(100f, 0f, 13f), byTarget[2]);
    }

    [TestMethod]
    public void NothingBeyondRange_IsAimedAt_AndAnAttackerAtExactlyRangeIs()
    {
        var origins = new[] { new LdPoint(0f, 0f, 0f) };
        var inside = At(1, 0f, 90f, 0f);
        var outside = At(2, 0f, 91f, 0f);

        Assert.AreEqual(1, Aim(new[] { outside, inside }, origins, originHeight: 0f, arrows: 1).Shots.Single().Target.Value);

        var none = Aim(new[] { outside }, origins, originHeight: 0f);
        Assert.AreEqual(0, none.Shots.Count);
        Assert.AreEqual(VolleyWithheldReason.NoTargets, none.Why);
    }

    [TestMethod]
    public void AGroupBeyondRange_DoesNotPullTheCentreAway()
    {
        var targets = new[] { At(1, 50f, 0f), At(2, 51f, 0f), At(10, 150f, 0f), At(11, 151f, 0f), At(12, 152f, 0f), At(13, 150f, 1f) };

        var result = Aim(targets, arrows: 2);

        CollectionAssert.AreEquivalent(new[] { 1, 2 }, Keys(result));
    }

    [TestMethod]
    public void NoTargets_IsNoTargets()
    {
        var result = Aim(Array.Empty<LdVolleyTarget>());

        Assert.AreEqual(VolleyWithheldReason.NoTargets, result.Why);
        Assert.AreEqual(0, result.Shots.Count);
    }

    [TestMethod]
    public void NoUsableOrigin_IsNoOrigin_WhateverTheTargets()
    {
        var targets = new[] { At(1, 30f, 0f) };

        Assert.AreEqual(VolleyWithheldReason.NoOrigin, Aim(targets, Array.Empty<LdPoint>()).Why);
        Assert.AreEqual(VolleyWithheldReason.NoOrigin, Aim(targets, new[] { new LdPoint(float.NaN, 0f, 0f) }).Why);
        Assert.AreEqual(VolleyWithheldReason.NoOrigin, Aim(targets, originHeight: float.PositiveInfinity).Why);
    }

    [TestMethod]
    public void ABadOriginIsDropped_AndTheGoodOneStillFires()
    {
        var origins = new[] { new LdPoint(float.NaN, 0f, 0f), new LdPoint(0f, 0f, 10f) };

        var result = Aim(new[] { At(1, 30f, 0f) }, origins, arrows: 1);

        Assert.AreEqual(new Vec3f(0f, 0f, 12.5f), result.Shots.Single().Origin);
    }

    [TestMethod]
    public void NotANumberOrInfinity_IsRefusedEverywhere_NeverReachingADirection()
    {
        var good = At(1, 40f, 0f);
        var nanChest = At(2, float.NaN, 0f);
        var infChest = At(3, 40f, float.PositiveInfinity);
        var nanVelocity = At(4, 40f, 1f, vx: float.NaN);
        var infVelocity = At(5, 41f, 1f, vy: float.NegativeInfinity);

        var result = Aim(new[] { nanChest, infChest, nanVelocity, infVelocity, good }, arrows: 8);

        Assert.IsTrue(Keys(result).All(k => k == 1), "only the attacker with real numbers is a target");
        Assert.IsTrue(result.Shots.All(s => IsFinite(s.Direction) && IsFinite(s.Origin)));

        foreach (var (radius, range, speed, arrows) in new[] { (float.NaN, 90f, Speed, 6), (6f, float.NaN, Speed, 6), (6f, 90f, float.NaN, 6), (6f, 90f, float.PositiveInfinity, 6),
                     (6f, float.PositiveInfinity, Speed, 6), (float.PositiveInfinity, 90f, Speed, 6), (6f, 90f, 0f, 6), (6f, 90f, Speed, 0) })
        {
            var refused = Aim(new[] { good }, radius: radius, range: range, speed: speed, arrows: arrows);
            Assert.AreEqual(0, refused.Shots.Count, $"radius {radius} range {range} speed {speed} arrows {arrows}");
            Assert.IsNotNull(refused.Why);
        }
    }

    private static bool IsFinite(Vec3f v) => !float.IsNaN(v.X + v.Y + v.Z) && !float.IsInfinity(v.X + v.Y + v.Z);

    [TestMethod]
    public void EveryDirection_IsAUnitVector_ThatFliesTowardItsTarget()
    {
        var targets = new[] { At(1, 40f, 10f), At(2, 55f, -8f), At(3, 70f, 0f) };

        var result = Aim(targets, arrows: 3);

        foreach (var shot in result.Shots)
        {
            var d = shot.Direction;
            Assert.AreEqual(1f, Math.Sqrt(d.X * d.X + d.Y * d.Y + d.Z * d.Z), 1e-3);
            var target = targets.Single(t => t.Key == shot.Target).Chest;
            Assert.IsTrue(d.X * (target.X - shot.Origin.X) + d.Y * (target.Y - shot.Origin.Y) > 0f, "leaves toward the target, not away");
            Assert.AreEqual(Speed, shot.Speed);
        }
    }

    // ---- the lead ----

    /// <summary>How near the arrow comes to the moving chest point: the arrow under 9.806 against the attacker walking his velocity, over ten seconds.</summary>
    private static float Miss(LdVolleyShot shot, LdVolleyTarget target)
    {
        var best = float.MaxValue;
        for (var t = 0f; t < 10f; t += 0.002f)
        {
            var arrow = new Vec3f(
                shot.Origin.X + shot.Direction.X * shot.Speed * t,
                shot.Origin.Y + shot.Direction.Y * shot.Speed * t,
                shot.Origin.Z + shot.Direction.Z * shot.Speed * t - 0.5f * EngineGravity * t * t);
            var chest = new Vec3f(target.Chest.X + target.Velocity.X * t, target.Chest.Y + target.Velocity.Y * t, target.Chest.Z);
            best = Math.Min(best, arrow.Distance(chest));
        }

        return best;
    }

    [TestMethod]
    public void AStandingAttacker_IsHitOnTheChestPoint_UnderTheEnginesGravity()
    {
        var target = At(1, 50f, 20f, 1.3f);

        var shot = Aim(new[] { target }, arrows: 1).Shots.Single();

        Assert.IsTrue(Miss(shot, target) < 0.15f, "miss " + Miss(shot, target));
    }

    [TestMethod]
    public void AWalkingAttacker_IsMetByTheArrow_InEveryDirectionOfTravel()
    {
        foreach (var (vx, vy) in new[] { (0f, 4f), (0f, -4f), (4f, 0f), (-4f, 0f), (3f, 3f), (-2.5f, 3.5f) })
        {
            var target = At(1, 55f, 0f, 1.3f, vx, vy);

            var shot = Aim(new[] { target }, arrows: 1).Shots.Single();

            Assert.IsTrue(Miss(shot, target) < 0.35f, $"velocity ({vx}, {vy}) missed by {Miss(shot, target)}");
        }
    }

    [TestMethod]
    public void TheLead_IsWhatMakesTheHit_AShotAtTheStandingPointMissesAWalker()
    {
        var walker = At(1, 55f, 0f, 1.3f, 0f, 5f);
        var still = walker with { Velocity = new Vec3f(0f, 0f, 0f) };

        var led = Aim(new[] { walker }, arrows: 1).Shots.Single();
        var unled = Aim(new[] { still }, arrows: 1).Shots.Single();

        Assert.IsTrue(Miss(led, walker) < 0.35f);
        Assert.IsTrue(Miss(unled, walker) > 3f, "five metres a second is several metres of lead over a second of flight");
    }

    [TestMethod]
    public void AHighOrigin_AndAHighTarget_StillMeetOnTheChestPoint()
    {
        var origins = new[] { new LdPoint(0f, 0f, 25f) };
        var target = At(1, 70f, 10f, 4f, 2f, -1f);

        var shot = Aim(new[] { target }, origins, arrows: 1, originHeight: 3f).Shots.Single();

        Assert.IsTrue(Miss(shot, target) < 0.35f, "miss " + Miss(shot, target));
    }
}
