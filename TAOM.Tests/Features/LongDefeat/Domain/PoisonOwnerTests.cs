using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.FordsOfWar.Domain;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Tests.Features.LongDefeat.Domain;

/// <summary>
/// Who owns a poison kill (phase2-plan.md section 4, "A kill must have a living affector"): the poisoner while he stands, else the commander, else any attacker that stands, else nobody, in which
/// case the killing tick is withheld. Never a key that means "no owner": <c>Agent.Die</c> swaps a stale last hitter in for owner -1, and <c>CustomBattleMoraleModel</c> reads the affector's formation.
/// </summary>
[TestClass]
public class PoisonOwnerTests
{
    private static readonly AgentKey Poisoner = new(11);
    private static readonly AgentKey Commander = new(22);
    private static readonly AgentKey Other = new(33);

    [TestMethod]
    public void WhileThePoisonerStands_HeOwnsTheKill()
    {
        Assert.AreEqual(Poisoner, PoisonOwner.Choose(Poisoner, Commander, Other));
    }

    [TestMethod]
    public void WithThePoisonerGone_TheCommanderOwnsTheKill()
    {
        Assert.AreEqual(Commander, PoisonOwner.Choose(null, Commander, Other));
    }

    [TestMethod]
    public void WithThePoisonerAndTheCommanderGone_AnyStandingAttackerOwnsTheKill()
    {
        Assert.AreEqual(Other, PoisonOwner.Choose(null, null, Other));
    }

    [TestMethod]
    public void WithNoAttackerStanding_NobodyOwnsIt_AndTheTickIsWithheld()
    {
        Assert.IsNull(PoisonOwner.Choose(null, null, null));
    }

    [TestMethod]
    public void AKeyThatMeansNoOwner_IsNeverChosen()
    {
        // The registry counts from 1, so key 0 and a negative one are not an agent: they must not reach the engine as an owner (-1 lets Agent.Die swap in a stale hitter).
        Assert.IsNull(PoisonOwner.Choose(new AgentKey(0), new AgentKey(-1), new AgentKey(0)));
        Assert.AreEqual(Other, PoisonOwner.Choose(new AgentKey(0), new AgentKey(-1), Other));
    }

    [TestMethod]
    public void TheOrderIsPoisonerThenCommanderThenAttacker_NotTheSmallestKey()
    {
        Assert.AreEqual(new AgentKey(900), PoisonOwner.Choose(new AgentKey(900), new AgentKey(2), new AgentKey(1)));
    }
}
