using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Tests.Features.LongDefeat.Domain;

/// <summary>
/// Who owns a volley arrow (phase3-plan.md D22). A kill must reach <c>CustomBattleMoraleModel</c> with a real affector (E17), and a friendly hit is cancelled only when the shooter is AI-controlled (E14), so
/// the owner is a live, AI-controlled defender who is not the main agent. Archers come first, and among them the one farthest from the nearest attacker; the main agent is never taken, which departs from
/// <c>Fell</c> on purpose.
/// </summary>
[TestClass]
public class VolleyOwnerRuleTests
{
    private static LdOwnerCandidate Archer(float nearest) => new(true, true, false, true, nearest);

    private static LdOwnerCandidate Foot(float nearest) => new(true, true, false, false, nearest);

    [TestMethod]
    public void NoCandidate_IsNobody()
    {
        Assert.AreEqual(-1, VolleyOwnerRule.Choose(System.Array.Empty<LdOwnerCandidate>()));
    }

    [TestMethod]
    public void TheMainAgent_IsNeverTheOwner_EvenWhenHeIsTheOnlyArcher()
    {
        var main = new LdOwnerCandidate(true, true, true, true, 90f);

        Assert.AreEqual(-1, VolleyOwnerRule.Choose(new[] { main }));
        Assert.AreEqual(1, VolleyOwnerRule.Choose(new[] { main, Foot(5f) }), "a foot soldier is taken before the main agent");
    }

    [TestMethod]
    public void APlayerControlledBody_IsRefused_BecauseItsFriendlyHitWouldNotBeCancelled()
    {
        var player = new LdOwnerCandidate(true, false, false, true, 80f);

        Assert.AreEqual(-1, VolleyOwnerRule.Choose(new[] { player }));
    }

    [TestMethod]
    public void AnInactiveOrRecycledSlot_IsRefused()
    {
        var gone = new LdOwnerCandidate(false, true, false, true, 80f);

        Assert.AreEqual(-1, VolleyOwnerRule.Choose(new[] { gone }));
        Assert.AreEqual(1, VolleyOwnerRule.Choose(new[] { gone, Archer(10f) }));
    }

    [TestMethod]
    public void AmongArchers_TheOneFarthestFromTheNearestAttackerOwns()
    {
        var candidates = new[] { Archer(12f), Archer(40f), Archer(25f) };

        Assert.AreEqual(1, VolleyOwnerRule.Choose(candidates));
    }

    [TestMethod]
    public void AnArcherCloseToTheMelee_IsTakenBeforeAFootSoldierFarFromIt()
    {
        var candidates = new[] { Foot(90f), Archer(3f) };

        Assert.AreEqual(1, VolleyOwnerRule.Choose(candidates), "archers first");
    }

    [TestMethod]
    public void WithNoArcher_TheFootSoldierFarthestFromTheMeleeOwns()
    {
        var candidates = new[] { Foot(8f), Foot(60f), Foot(30f) };

        Assert.AreEqual(1, VolleyOwnerRule.Choose(candidates));
    }

    [TestMethod]
    public void ATie_GoesToTheEarlierCandidate()
    {
        Assert.AreEqual(0, VolleyOwnerRule.Choose(new[] { Archer(20f), Archer(20f) }));
    }

    [TestMethod]
    public void ADistanceThatIsNotANumber_IsTheLeastPreferred_ButStillAnOwner()
    {
        Assert.AreEqual(1, VolleyOwnerRule.Choose(new[] { Archer(float.NaN), Archer(5f) }));
        Assert.AreEqual(0, VolleyOwnerRule.Choose(new[] { Archer(float.NaN) }), "any eligible body beats nobody");
    }

    [TestMethod]
    public void NoAttackerAtAll_IsAnInfiniteDistance_AndStillChoosesAnArcher()
    {
        Assert.AreEqual(0, VolleyOwnerRule.Choose(new[] { Archer(float.PositiveInfinity) }));
    }
}
