using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Stamina;
using TAOM.Features.Stamina.Domain;

namespace TAOM.Tests.Features.Stamina;

/// <summary>
/// A simulated fight over the real rules and the shipped default numbers (#1497). It tests the promise the design
/// rests on: a soldier who fights one opponent tires and settles, a soldier who fights two is worn down, and a player
/// who swings without pause empties his pool. The fight tempos are ASSUMED until spike S4 measures them in game:
/// change them here when it does. The AI caution is modelled as the swing interval divided by the band's attack
/// chance multiplier, which is what spike S7 must confirm.
/// </summary>
[TestClass]
public class StaminaEconomySimulationTests
{
    private const float Dt = 0.25f;
    private const float OneHandedSwordKg = 1.2f;
    private const float TwoHandedSwordKg = 2.5f;
    // An engaged AI soldier: one swing every 2 s when Fresh; each opponent swings at him every 2 s and 60% are parried.
    private const float AiSwingInterval = 2f;
    private const float IncomingInterval = 2f;
    private const float BlockedShare = 0.6f;

    private static readonly StaminaEconomy E = StaminaEconomy.Default;
    private static readonly StaminaTuning Tuning = StaminaTuning.Default;

    // Seconds until the soldier is Exhausted, or a negative value if he is not within the limit.
    private static float SecondsToExhaustion(float pool, float weaponKg, float swingInterval, int opponents, bool aiCaution, float limitSeconds)
    {
        var cell = new StaminaCell(0f) { Max = pool, Current = pool };
        var band = StaminaBand.Fresh;
        var nextSwing = 0f;
        var blockDebt = 0f;
        for (var now = 0f; now < limitSeconds; now += Dt)
        {
            if (now >= nextSwing)
            {
                StaminaService.Charge(cell, StaminaService.SwingCost(E, weaponKg), now, E);
                var caution = aiCaution ? Tuning.EffectsOf(band).AiAttack : 1f;
                nextSwing = now + swingInterval / caution;
            }

            // Parried blows as a steady rate, paid in whole blows.
            blockDebt += opponents * BlockedShare * Dt / IncomingInterval;
            while (blockDebt >= 1f)
            {
                cell.AddPending(StaminaService.BlockCost(E, StaminaBlock.WeaponParry, 0f));
                blockDebt -= 1f;
            }

            StaminaService.Step(cell, new StaminaSample(now + Dt, Dt, running: false, blocking: false, bowHeldSeconds: 0f), E);
            band = StaminaService.NextBand(E, band, StaminaService.FractionOf(cell));
            cell.Publish(band, StaminaService.FractionOf(cell));
            if (band == StaminaBand.Exhausted)
                return now + Dt;
        }

        return -1f;
    }

    [TestMethod]
    public void AiSoldier_OneOnOne_TiresButDoesNotReachZeroInThreeMinutes()
    {
        var pool = StaminaService.MaxPool(E, tier: 3, hero: false);

        var seconds = SecondsToExhaustion(pool, OneHandedSwordKg, AiSwingInterval, opponents: 1, aiCaution: true, limitSeconds: 180f);

        Assert.IsTrue(seconds < 0f, $"a tier 3 soldier against one opponent was Exhausted after {seconds:0} s");
    }

    [TestMethod]
    public void AiSoldier_AgainstTwo_IsWornDown_ButNotAtOnce()
    {
        var pool = StaminaService.MaxPool(E, tier: 3, hero: false);

        var seconds = SecondsToExhaustion(pool, OneHandedSwordKg, AiSwingInterval, opponents: 2, aiCaution: true, limitSeconds: 300f);

        Assert.IsTrue(seconds >= 30f && seconds <= 150f, $"a tier 3 soldier against two was Exhausted after {seconds:0} s; expected 30 to 150");
    }

    [TestMethod]
    public void AiSoldier_WithoutCaution_OneOnOne_ReachesZero()
    {
        // The control for the first test: it is the caution, not the numbers alone, that keeps him above zero.
        var pool = StaminaService.MaxPool(E, tier: 3, hero: false);

        var seconds = SecondsToExhaustion(pool, OneHandedSwordKg, AiSwingInterval, opponents: 1, aiCaution: false, limitSeconds: 180f);

        Assert.IsTrue(seconds > 0f, "without the lower attack chance a soldier in a one-on-one fight must tire out");
    }

    [TestMethod]
    public void Recruit_AgainstTwo_TiresOut_AndAVeteranOutlastsHim()
    {
        // Regeneration is a share of the pool, so a larger pool also comes back faster in points: a tier 6 soldier
        // may hold against two for the whole limit. Negative means he was never Exhausted.
        var recruit = SecondsToExhaustion(StaminaService.MaxPool(E, 0, false), OneHandedSwordKg, AiSwingInterval, 2, true, 600f);
        var veteran = SecondsToExhaustion(StaminaService.MaxPool(E, 6, false), OneHandedSwordKg, AiSwingInterval, 2, true, 600f);

        Assert.IsTrue(recruit > 0f, "a recruit against two must tire out");
        Assert.IsTrue(veteran < 0f || veteran > recruit, $"recruit {recruit:0} s, veteran {veteran:0} s");
    }

    [TestMethod]
    public void PlayerHero_WhoSwingsWithoutPause_EmptiesThePoolInAboutHalfAMinute()
    {
        var pool = StaminaService.MaxPool(E, tier: 7, hero: true);

        var seconds = SecondsToExhaustion(pool, OneHandedSwordKg, swingInterval: 1f, opponents: 0, aiCaution: false, limitSeconds: 120f);

        Assert.IsTrue(seconds >= 25f && seconds <= 40f, $"a hero who swings once a second was Exhausted after {seconds:0} s; expected 25 to 40");
    }

    [TestMethod]
    public void PlayerHero_WithATwoHandedSword_EmptiesThePoolSooner()
    {
        var pool = StaminaService.MaxPool(E, tier: 7, hero: true);

        var oneHanded = SecondsToExhaustion(pool, OneHandedSwordKg, 1f, 0, false, 120f);
        var twoHanded = SecondsToExhaustion(pool, TwoHandedSwordKg, 1f, 0, false, 120f);

        Assert.IsTrue(twoHanded > 0f && twoHanded < oneHanded, $"one-handed {oneHanded:0} s, two-handed {twoHanded:0} s");
    }

    [TestMethod]
    public void PlayerHero_WhoPacesSwings_NeverEmptiesThePool()
    {
        var pool = StaminaService.MaxPool(E, tier: 7, hero: true);

        var seconds = SecondsToExhaustion(pool, OneHandedSwordKg, swingInterval: 3f, opponents: 0, aiCaution: false, limitSeconds: 600f);

        Assert.IsTrue(seconds < 0f, $"a hero who swings every 3 s was Exhausted after {seconds:0} s");
    }

    [TestMethod]
    public void ExhaustedSoldier_LeftAlone_RecoversInAFewSeconds()
    {
        var cell = new StaminaCell(0f) { Max = 100f, Current = 0f };
        cell.Publish(StaminaBand.Exhausted, 0f);
        var seconds = 0f;
        while (cell.Band == StaminaBand.Exhausted && seconds < 60f)
        {
            seconds += Dt;
            StaminaService.Step(cell, new StaminaSample(seconds, Dt, running: false, blocking: false, bowHeldSeconds: 0f), E);
            cell.Publish(StaminaService.NextBand(E, cell.Band, StaminaService.FractionOf(cell)), StaminaService.FractionOf(cell));
        }

        Assert.AreEqual(StaminaBand.Tired, cell.Band);
        Assert.IsTrue(seconds >= 3.5f && seconds <= 6f, $"recovery took {seconds:0.0} s; expected 3.5 to 6");
    }
}
