using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Tests.Features.LongDefeat.Domain;

/// <summary>The unattended player's fire (phase3-plan.md D27): in a wave it arms the Elven Volley whenever the view shows it ready, and in no other phase.</summary>
[TestClass]
public class ShopBotVolleyTests
{
    private static RunView View(RunPhase phase, bool ready) =>
        new(phase, 1, 0, 0, 0f, 100, 0, 10, 0f, Array.Empty<ShopOffer>())
        {
            Volleys = new VolleyView(true, ready, ready ? 0f : 12f, null, 0f, Array.Empty<VolleySourceState>()),
        };

    [TestMethod]
    public void InAWave_AReadyVolley_IsArmed()
    {
        Assert.AreEqual(BotActionKind.Volley, ShopBot.Decide(View(RunPhase.Wave, true), 5f).Kind);
    }

    [TestMethod]
    public void InAWave_AVolleyOnCooldown_IsLeftAlone()
    {
        Assert.AreEqual(BotActionKind.Wait, ShopBot.Decide(View(RunPhase.Wave, false), 5f).Kind);
    }

    [TestMethod]
    public void WithNoVolleyOwned_TheBotWaitsInAWave()
    {
        var view = new RunView(RunPhase.Wave, 1, 0, 0, 0f, 100, 0, 10, 0f, Array.Empty<ShopOffer>());

        Assert.AreEqual(BotActionKind.Wait, ShopBot.Decide(view, 5f).Kind);
    }

    [TestMethod]
    public void BetweenWaves_AndAfterTheEnd_TheVolleyIsNeverFired()
    {
        foreach (var phase in new[] { RunPhase.Preparation, RunPhase.Intermission })
            Assert.AreNotEqual(BotActionKind.Volley, ShopBot.Decide(View(phase, true), 99f).Kind, phase.ToString());
        Assert.AreEqual(BotActionKind.Wait, ShopBot.Decide(View(RunPhase.Ended, true), 99f).Kind);
    }

    // ---- the bot against the run ----

    private static readonly VolleyPlan Volley = new("ld_elven_arrow", 3f, new[] { new VolleyLevelPlan(350, 12, 45f, 8f, 90f, VolleyAimMode.Auto) });

    private static RunPlan Plan(FortOffer offered) =>
        new(
            "helms",
            new[] { new GarrisonRow("fow_militia", FormationSlot.Infantry, 30) },
            new WavePlan(new[] { new WaveSpec(20, new[] { new WaveTroop("orc", FormationSlot.Infantry, 3), new WaveTroop("skirm", FormationSlot.Ranged, 1) }) }, new WaveScaling(10, 300)),
            new ShopPlan(5000, Array.Empty<ShopOffer>(), new FortPlan(null, null, null, Volley)),
            RunRules.Default with { PreparationSeconds = 5f, IntermissionSeconds = 10f, QuietSeconds = 1e6f, QuietAnySeconds = 1e6f, StrandedSeconds = 1e6f },
            new RunOptions { Seed = 1UL, StartGold = 5000 })
        {
            Offered = offered,
            Volley = new LdVolleyOrigins(new[] { new LdPoint(0f, 0f, 0f) }),
        };

    /// <summary>The director's rhythm: tick and apply (the director reports each ordered shot), then the bot decides.</summary>
    private static (int Orders, int Fired, List<float> Times) PlayAWave(bool targets, int seconds)
    {
        var run = new SurvivalRun(Plan(new FortOffer(0, 0, false, 1)));
        run.Begin();
        var times = new List<float>();
        var orders = 0;
        var fired = 0;
        for (var i = 0; i < seconds + 20; i++)
        {
            var fx = run.Tick(new RunSnapshot(1f, 10, 0, 40, 0, 0, 1000));
            foreach (var order in fx.FireVolleys)
            {
                orders++;
                times.Add(run.WaveSeconds);
                if (targets)
                {
                    fired++;
                    run.VolleyFired(order.Source, order.Arrows);
                }
                else
                {
                    run.VolleyWithheld(order.Source, VolleyWithheldReason.NoTargets);
                }
            }

            var view = run.GetView();
            var action = ShopBot.Decide(view, i);
            if (action.Kind == BotActionKind.VolleyLevel)
                run.TryBuyVolleyLevel();
            else if (action.Kind == BotActionKind.Volley)
                run.ArmVolley();
            else if (action.Kind == BotActionKind.Horn)
                run.TryStartWave();
            if (run.Phase == RunPhase.Wave && run.WaveSeconds >= seconds)
                break;
        }

        return (orders, fired, times);
    }

    [TestMethod]
    public void TheBot_BuysTheLevelInThePreparation_AndFiresOnTheCooldown_InAWave()
    {
        var (orders, fired, times) = PlayAWave(targets: true, seconds: 100);

        Assert.AreEqual(orders, fired);
        Assert.IsTrue(fired >= 3, "a use at the start, then one every 45 s: " + string.Join(",", times));
        for (var i = 1; i < times.Count; i++)
            Assert.IsTrue(times[i] - times[i - 1] >= 45f, $"uses at {times[i - 1]} and {times[i]}: the cooldown from a fired shot is 45 s");
    }

    [TestMethod]
    public void WithNoAttackersInReach_TheBotAsksNoMoreThanOnceASecond_AndTheCooldownIsNeverSpent()
    {
        var (orders, _, times) = PlayAWave(targets: false, seconds: 30);

        Assert.IsTrue(orders >= 10, "it keeps asking while the volley stays ready: " + orders);
        for (var i = 1; i < times.Count; i++)
            Assert.IsTrue(times[i] - times[i - 1] >= 1f, $"asked at {times[i - 1]} and {times[i]}: the retry hold is a second");
    }

    [TestMethod]
    public void WithNothingOffered_TheBotNeverArmsAnything_InAWholeWave()
    {
        var run = new SurvivalRun(Plan(FortOffer.Offered));
        run.Begin();
        for (var i = 0; i < 90; i++)
        {
            var fx = run.Tick(new RunSnapshot(1f, 10, 0, 40, 0, 0, 1000));
            Assert.AreEqual(0, fx.FireVolleys.Count);
            var kind = ShopBot.Decide(run.GetView(), i).Kind;
            Assert.AreNotEqual(BotActionKind.Volley, kind);
            Assert.AreNotEqual(BotActionKind.VolleyLevel, kind);
            if (kind == BotActionKind.Horn)
                run.TryStartWave();
        }

        Assert.AreEqual(RunPhase.Wave, run.Phase);
    }
}
