using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.MarshalsWord.Domain;

namespace TAOM.Tests.Features.MarshalsWord;

/// <summary>The reasons of the two thrower trial steps (build-spec 9.4): the first that holds, in the contract's order.</summary>
[TestClass]
public class MarshalThrowerTrialRulesTests
{
    private static readonly ThrowerCostVerdict CostPass = new(ThrowerVerdictKind.Pass, "", 20.0, 900.0);

    private static ThrowerTrialRead Working() => new(
        Faulted: false, Armed0: 40, EnemyFound: true, Controllable: true, Published: true, PostfixMoved: true, ClosedBeforeStand: false, Stood: true, Released: true,
        MaxOvershoot: 1f, Shots: 60, RunShots: 60, AmmoSpent: 70, MeleePassed: true, PubCalls: 5000, Cost: CostPass);

    private static readonly (string Reason, System.Func<ThrowerTrialRead, ThrowerTrialRead> Break)[] Breaks =
    {
        ("faulted", r => r with { Faulted = true }),
        ("no-throwers", r => r with { Armed0 = 9 }),
        ("no-enemy", r => r with { EnemyFound = false }),
        ("not-controllable", r => r with { Controllable = false }),
        ("never-published", r => r with { Published = false }),
        ("postfix-silent", r => r with { PostfixMoved = false }),
        ("closed-before-stand", r => r with { ClosedBeforeStand = true }),
        ("walked-in", r => r with { MaxOvershoot = 6.1f }),
        ("never-stood", r => r with { Stood = false }),
        ("never-released", r => r with { Released = false }),
        ("shots-not-counted", r => r with { Shots = 0, RunShots = 0, AmmoSpent = 10 }),
        ("few-throws", r => r with { Shots = 9, RunShots = 9, AmmoSpent = 9 }),
        ("no-melee", r => r with { MeleePassed = false }),
        ("timer-silent", r => r with { PubCalls = 0, Cost = new ThrowerCostVerdict(ThrowerVerdictKind.Skip, "too-few-calls", 0, 0) }),
        ("too-few-calls", r => r with { PubCalls = 20, Cost = new ThrowerCostVerdict(ThrowerVerdictKind.Skip, "too-few-calls", 0, 0) }),
        ("cost-mean", r => r with { Cost = new ThrowerCostVerdict(ThrowerVerdictKind.Fail, "mean", 80.0, 900.0) }),
        ("cost-max", r => r with { Cost = new ThrowerCostVerdict(ThrowerVerdictKind.Fail, "max", 20.0, 9000.0) }),
    };

    [TestMethod]
    public void ThrowerTrialReason_AWorkingRun_IsEmpty()
    {
        Assert.AreEqual("", MarshalTrialRules.ThrowerTrialReason(Working()));
    }

    [TestMethod]
    public void ThrowerTrialReason_EachReasonOnItsOwn_IsNamed()
    {
        foreach (var (reason, broken) in Breaks)
            Assert.AreEqual(reason, MarshalTrialRules.ThrowerTrialReason(broken(Working())), reason);
    }

    private static int FieldsOf(string reason) => reason switch
    {
        "shots-not-counted" or "few-throws" => 1,
        "timer-silent" or "too-few-calls" or "cost-mean" or "cost-max" => 2,
        _ => 0,
    };

    [TestMethod]
    public void ThrowerTrialReason_TwoBroken_NamesTheFirstInTheContractsOrder()
    {
        var pairs = 0;
        for (var first = 0; first < Breaks.Length; first++)
        {
            for (var second = first + 1; second < Breaks.Length; second++)
            {
                if (FieldsOf(Breaks[first].Reason) != 0 && FieldsOf(Breaks[first].Reason) == FieldsOf(Breaks[second].Reason))
                    continue;
                pairs++;
                var read = Breaks[second].Break(Breaks[first].Break(Working()));
                Assert.AreEqual(Breaks[first].Reason, MarshalTrialRules.ThrowerTrialReason(read), Breaks[first].Reason + " before " + Breaks[second].Reason);
            }
        }

        Assert.IsTrue(pairs > 100, "the sweep asked " + pairs + " pairs");
    }

    [TestMethod]
    public void ThrowerTrialReason_TenArmedIsEnough_AndNineIsNot()
    {
        Assert.AreEqual("", MarshalTrialRules.ThrowerTrialReason(Working() with { Armed0 = 10 }));
        Assert.AreEqual("no-throwers", MarshalTrialRules.ThrowerTrialReason(Working() with { Armed0 = 9 }));
        Assert.AreEqual(10, MarshalTrialRules.ThrowerTrialMinArmed);
    }

    [TestMethod]
    public void ThrowerTrialReason_AmmunitionThatFellWithNoShotCounted_PointsAtTheShotCallback_NotAtTheRule()
    {
        Assert.AreEqual("shots-not-counted", MarshalTrialRules.ThrowerTrialReason(Working() with { Shots = 0, RunShots = 0, AmmoSpent = 10 }));
        Assert.AreEqual("few-throws", MarshalTrialRules.ThrowerTrialReason(Working() with { Shots = 5, RunShots = 5, AmmoSpent = 9 }), "ammunition under the minimum is a plain few-throws");
        Assert.AreEqual("few-throws", MarshalTrialRules.ThrowerTrialReason(Working() with { Shots = 5, RunShots = 5, AmmoSpent = 40 }), "some shots counted: the callback works, the soldiers threw too little");
        Assert.AreEqual("few-throws", MarshalTrialRules.ThrowerTrialReason(Working() with { Shots = 0, RunShots = 15, AmmoSpent = 40 }), "throws on the approach show the callback works");
        Assert.AreEqual("", MarshalTrialRules.ThrowerTrialReason(Working() with { Shots = 10, RunShots = 10, AmmoSpent = 10 }));
    }

    [TestMethod]
    public void ThrowerTrialReason_AnOvershootOfSixMetres_Passes_AndOverItWalksIn()
    {
        Assert.AreEqual("", MarshalTrialRules.ThrowerTrialReason(Working() with { MaxOvershoot = 6f }));
        Assert.AreEqual("walked-in", MarshalTrialRules.ThrowerTrialReason(Working() with { MaxOvershoot = float.NaN }));
    }

    [TestMethod]
    public void ThrowerVanillaReason_AControlThatClosedWithNothingPublished_IsEmpty()
    {
        Assert.AreEqual("", MarshalTrialRules.ThrowerVanillaReason(false, 40, true, true, false, true, 4f));
    }

    [TestMethod]
    public void ThrowerVanillaReason_EachReasonOnItsOwn_AndTheFirstOfSeveral()
    {
        Assert.AreEqual("faulted", MarshalTrialRules.ThrowerVanillaReason(true, 40, true, true, false, true, 4f));
        Assert.AreEqual("no-throwers", MarshalTrialRules.ThrowerVanillaReason(false, 9, true, true, false, true, 4f));
        Assert.AreEqual("no-enemy", MarshalTrialRules.ThrowerVanillaReason(false, 40, false, true, false, true, 4f));
        Assert.AreEqual("not-controllable", MarshalTrialRules.ThrowerVanillaReason(false, 40, true, false, false, true, 4f));
        Assert.AreEqual("published-while-off", MarshalTrialRules.ThrowerVanillaReason(false, 40, true, true, true, true, 4f));
        Assert.AreEqual("never-closed", MarshalTrialRules.ThrowerVanillaReason(false, 40, true, true, false, false, 4f));
        Assert.AreEqual("faulted", MarshalTrialRules.ThrowerVanillaReason(true, 1, false, false, true, false, 4f));
        Assert.AreEqual("published-while-off", MarshalTrialRules.ThrowerVanillaReason(false, 40, true, true, true, false, 4f));
    }

    [TestMethod]
    public void ThrowerVanillaReason_ACompanyThatStoppedOrWasNeverMeasured_IsStandBlind_AndNeverClosedComesFirst()
    {
        Assert.AreEqual("stand-blind", MarshalTrialRules.ThrowerVanillaReason(false, 40, true, true, false, true, 1f));
        Assert.AreEqual("stand-blind", MarshalTrialRules.ThrowerVanillaReason(false, 40, true, true, false, true, 0.5f));
        Assert.AreEqual("stand-blind", MarshalTrialRules.ThrowerVanillaReason(false, 40, true, true, false, true, float.NaN));
        Assert.AreEqual("", MarshalTrialRules.ThrowerVanillaReason(false, 40, true, true, false, true, 1.1f));
        Assert.AreEqual("never-closed", MarshalTrialRules.ThrowerVanillaReason(false, 40, true, true, false, false, 0f));
    }
}
