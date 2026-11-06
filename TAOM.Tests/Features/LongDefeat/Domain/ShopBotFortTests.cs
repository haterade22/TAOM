using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Tests.Features.LongDefeat.Domain;

/// <summary>
/// The bot's fortification schedule (phase3-plan.md D27). A scripted sequence of views, one per stretch between waves, with the rows the run would show: the replay buys what the schedule says, in order, and owns
/// the level each named run of section 8 needs by the wave that proves it.
/// </summary>
[TestClass]
public class ShopBotFortTests
{
    private static readonly ShopOffer[] Packs =
    {
        new("militia", "Militia", "m", FormationSlot.Infantry, 12, 180),
        new("yeomen", "Yeomen", "y", FormationSlot.Ranged, 10, 200),
    };

    private static readonly int[] GatePrices = { 200, 300, 450 };
    private static readonly int[] TowerPrices = { 250, 300, 400 };
    private static readonly int[] VolleyPrices = { 350, 300 };
    private const int CarePrice = 150;
    private const int RepairPrice = 200;

    private static LdGateFacts Whole() => new(0, 15000f, 15000f, 15000f, false, false);

    private static LdGateFacts Lost(float share) => new(1, 15000f * (1f - share), 15000f, 15000f, false, false);

    private static LdGateFacts Broken() => new(4, 0f, 15000f, 15000f, true, false);

    /// <summary>The state a replay carries between buys: the levels owned, care used, the gold, and the gate as the last wave left it.</summary>
    private sealed class Sim
    {
        public int Gate;
        public int Towers;
        public int Volley;
        public int CareUsed;
        public int Gold = 5000;
        public bool StartGold = true;
        public int Cleared;
        public RunPhase Phase = RunPhase.Preparation;
        public LdGateFacts[] Gates = { Whole() };
        public FortOffer Offered = new(3, 3, true, 2);
        public int Standing = 100;

        public RunView View(IReadOnlyList<ShopOffer>? packs = null)
        {
            FortRow Row(int level, int max, IReadOnlyList<int> prices, int offered) =>
                new(level, max, level < max ? prices[level] : 0, offered > 0 && level < Math.Min(max, offered));
            var missing = Gates.Where(g => g.MissingShare > 0f).Select(g => g.MissingShare).ToList();
            var fort = new FortView(
                Row(Gate, 3, GatePrices, Offered.Gate),
                new FortRow(0, 0, missing.Sum(m => GateMath.RepairPrice(RepairPrice, m)), Offered.Gate > 0 && missing.Count > 0),
                Row(Towers, 3, TowerPrices, Offered.Towers),
                new FortRow(CareUsed, 1, CarePrice, Offered.FieldCare && CareUsed < 1),
                Row(Volley, 2, VolleyPrices, Offered.Volley),
                Gates,
                StartGold);
            return new RunView(Phase, Cleared, Cleared, Gold, 60f, Standing, 0, 0, 0f, packs ?? Packs) { Fort = fort };
        }

        /// <summary>Asks the bot until it stops buying fortifications, applying each buy as the run would. Returns the kinds in order.</summary>
        public List<BotActionKind> Shop()
        {
            var bought = new List<BotActionKind>();
            for (var guard = 0; guard < 30; guard++)
            {
                var action = ShopBot.Decide(View(Array.Empty<ShopOffer>()), 0f);
                switch (action.Kind)
                {
                    case BotActionKind.GateRepair:
                        Gold -= View().Fort.Repair.Price;
                        Gates = Gates.Select(g => Whole()).ToArray();
                        break;
                    case BotActionKind.GateLevel: Gold -= GatePrices[Gate++]; break;
                    case BotActionKind.TowerLevel: Gold -= TowerPrices[Towers++]; break;
                    case BotActionKind.VolleyLevel: Gold -= VolleyPrices[Volley++]; break;
                    case BotActionKind.FieldCare: Gold -= CarePrice; CareUsed++; break;
                    default: return bought;
                }

                bought.Add(action.Kind);
            }

            Assert.Fail("the bot never stopped buying");
            return bought;
        }

        /// <summary>The wave ends: the next stretch begins with care available again.</summary>
        public void Clear(LdGateFacts? gate = null)
        {
            Cleared++;
            Phase = RunPhase.Intermission;
            CareUsed = 0;
            Gates = new[] { gate ?? Whole() };
        }
    }

    // ---- the schedule ----

    [TestMethod]
    public void ThePreparation_BuysTheVolleyThenTheTowers_AndNothingElse()
    {
        var sim = new Sim();

        CollectionAssert.AreEqual(new[] { BotActionKind.VolleyLevel, BotActionKind.TowerLevel }, sim.Shop());
    }

    [TestMethod]
    public void ClearOne_RepairsFirst_ThenBuysGateOne_VolleyTwo_TowersTwo_AndCare()
    {
        var sim = new Sim();
        sim.Shop();
        sim.Clear(Broken());

        CollectionAssert.AreEqual(
            new[] { BotActionKind.GateRepair, BotActionKind.GateLevel, BotActionKind.VolleyLevel, BotActionKind.TowerLevel, BotActionKind.FieldCare },
            sim.Shop());
    }

    [TestMethod]
    public void ClearOne_WithAGateThatLostNothing_SkipsTheRepair()
    {
        var sim = new Sim();
        sim.Shop();
        sim.Clear(Lost(0.05f));

        CollectionAssert.AreEqual(
            new[] { BotActionKind.GateLevel, BotActionKind.VolleyLevel, BotActionKind.TowerLevel, BotActionKind.FieldCare },
            sim.Shop());
    }

    [DataTestMethod]
    [DataRow(0.1f, true)]
    [DataRow(0.37f, true)]
    [DataRow(0.099f, false)]
    public void TheRepairThreshold_IsAtLeastAMissingTenth(float lost, bool repairs)
    {
        var sim = new Sim { Cleared = 1, Phase = RunPhase.Intermission, Gates = new[] { Lost(lost) }, Gate = 3, Towers = 3, Volley = 2, CareUsed = 1 };

        Assert.AreEqual(repairs, sim.Shop().Contains(BotActionKind.GateRepair));
    }

    [TestMethod]
    public void ClearTwo_BuysGateTwo_TowersThree_AndCare()
    {
        var sim = new Sim();
        sim.Shop();
        sim.Clear();
        sim.Shop();
        sim.Clear();

        CollectionAssert.AreEqual(new[] { BotActionKind.GateLevel, BotActionKind.TowerLevel, BotActionKind.FieldCare }, sim.Shop());
    }

    [TestMethod]
    public void ClearThree_BuysGateThreeAndCare_AndClearFourNothing()
    {
        var sim = new Sim();
        for (var clear = 0; clear < 3; clear++)
        {
            sim.Shop();
            sim.Clear();
        }

        CollectionAssert.AreEqual(new[] { BotActionKind.GateLevel, BotActionKind.FieldCare }, sim.Shop());
        Assert.AreEqual((3, 3, 2), (sim.Gate, sim.Towers, sim.Volley));

        sim.Clear();
        CollectionAssert.AreEqual(Array.Empty<BotActionKind>(), sim.Shop(), "all three kinds are at their top and care is not scheduled past clear 3");
    }

    [TestMethod]
    public void TheSchedule_ThroughClearThree_WithABrokenGateEachTime_CostsWhatItsPricesSum_AndFitsAStartGoldOf5000()
    {
        var sim = new Sim();
        for (var clear = 0; clear < 4; clear++)
        {
            sim.Shop();
            sim.Clear(Broken());
        }

        // Preparation 350 + 250; clear 1: 200 + 200 + 300 + 300 + 150; clear 2: 200 + 300 + 400 + 150; clear 3: 200 + 450 + 150. In all 3600.
        Assert.AreEqual(5000 - 3600, sim.Gold);
    }

    // ---- the named runs of section 8: the level each needs by the wave that proves it ----

    [TestMethod]
    public void TheCareRun_WavesTwo_OwnsCareAtClearOne()
    {
        var sim = new Sim { Offered = new FortOffer(0, 0, true, 0) };
        Assert.AreEqual(0, sim.Shop().Count, "no care in the preparation");
        sim.Clear();

        CollectionAssert.AreEqual(new[] { BotActionKind.FieldCare }, sim.Shop());
    }

    [TestMethod]
    public void TheGateRun_WavesThree_RepairsAtClearOne_ThenOwnsLevelOneForWaveTwo_AndLevelTwoForWaveThree()
    {
        var sim = new Sim { Offered = new FortOffer(3, 0, false, 0) };
        Assert.AreEqual(0, sim.Shop().Count, "wave 1 meets the vanilla gate: no level before clear 1");
        sim.Clear(Broken());
        CollectionAssert.AreEqual(new[] { BotActionKind.GateRepair, BotActionKind.GateLevel }, sim.Shop());
        Assert.AreEqual(1, sim.Gate, "owned for wave 2");
        sim.Clear();

        sim.Shop();

        Assert.AreEqual(2, sim.Gate, "owned for wave 3");
    }

    [TestMethod]
    public void TheVolleyRun_WavesThree_OwnsLevelOneForWaveOne_AndLevelTwoForWaveTwo()
    {
        var sim = new Sim { Offered = new FortOffer(0, 0, false, 2) };
        sim.Shop();
        Assert.AreEqual(1, sim.Volley, "wave 1 fires the auto volley");
        sim.Clear();

        sim.Shop();

        Assert.AreEqual(2, sim.Volley, "waves 2 and 3 fire the crosshair volley");
    }

    [TestMethod]
    public void TheTowerRun_OwnsLevelOneForWaveOne_LevelTwoForWaveTwo_AndLevelThreeForWaveThree()
    {
        var sim = new Sim { Offered = new FortOffer(0, 3, false, 0) };
        sim.Shop();
        Assert.AreEqual(1, sim.Towers);
        sim.Clear();
        sim.Shop();
        Assert.AreEqual(2, sim.Towers);
        sim.Clear();

        sim.Shop();

        Assert.AreEqual(3, sim.Towers, "tower 3 is bought at clear 2, so wave 3 has the tower volley");
    }

    [TestMethod]
    public void AFiveWaveRun_OwnsEveryKindAtItsTop_BeforeWaveFour()
    {
        var sim = new Sim();
        for (var clear = 0; clear < 3; clear++)
        {
            sim.Shop();
            sim.Clear(clear == 0 ? Broken() : Whole());
        }

        sim.Shop();

        Assert.AreEqual((3, 3, 2), (sim.Gate, sim.Towers, sim.Volley));
    }

    // ---- order against the packs ----

    [TestMethod]
    public void WithAStartGold_FortificationsComeBeforePacks()
    {
        var sim = new Sim { StartGold = true };

        Assert.AreEqual(BotActionKind.VolleyLevel, ShopBot.Decide(sim.View(), 0f).Kind);
    }

    [TestMethod]
    public void WithoutAStartGold_PacksComeFirst_AndFortificationsFollowWhenNoPackFits()
    {
        var sim = new Sim { StartGold = false, Gold = 600 };
        Assert.AreEqual(BotActionKind.Buy, ShopBot.Decide(sim.View(), 0f).Kind);

        sim.Standing = 169;
        var capped = sim.View() with { GarrisonCap = 170 };
        Assert.AreEqual(BotActionKind.VolleyLevel, ShopBot.Decide(capped, 0f).Kind, "no pack fits under the cap, so what is left goes to the volley");
    }

    [TestMethod]
    public void AnUnaffordableRow_IsSkipped_ForTheNextOneTheGoldReaches()
    {
        var sim = new Sim { Gold = 260, Cleared = 1, Phase = RunPhase.Intermission, Gate = 3 };

        Assert.AreEqual(BotActionKind.TowerLevel, ShopBot.Decide(sim.View(Array.Empty<ShopOffer>()), 0f).Kind, "volley 1 costs 350 and the gold stops at 260; towers 2 costs 250");
    }

    [TestMethod]
    public void ARepairTheGoldDoesNotReach_IsNotAsked()
    {
        var sim = new Sim { Gold = 150, Cleared = 1, Phase = RunPhase.Intermission, Gates = new[] { Broken() }, Gate = 3, Towers = 3, Volley = 2, CareUsed = 1 };

        Assert.AreEqual(BotActionKind.Wait, ShopBot.Decide(sim.View(Array.Empty<ShopOffer>()), 0f).Kind);
    }

    [TestMethod]
    public void ARowTheViewShowsUnavailable_IsNeverAsked_SoTheIntermissionDoesNotWaitOnARefusal()
    {
        var sim = new Sim { Cleared = 1, Phase = RunPhase.Intermission };
        var view = sim.View(Array.Empty<ShopOffer>());
        view = view with { Fort = view.Fort with { Towers = view.Fort.Towers with { Available = false }, Gate = FortRow.None, Volley = FortRow.None, Care = FortRow.None } };

        Assert.AreEqual(BotActionKind.Wait, ShopBot.Decide(view, 0f).Kind);
        Assert.AreEqual(BotActionKind.Horn, ShopBot.Decide(view, 30f).Kind);
    }

    // ---- D31: an empty Offered decides exactly as no fortifications at all ----

    [TestMethod]
    public void WithNothingOffered_TheBotDecidesAsItDidWithNoFortifications_AtEveryGoldAndStage()
    {
        foreach (var gold in new[] { 0, 150, 199, 200, 600, 5000 })
        {
            foreach (var cleared in new[] { 0, 1, 2, 3, 4 })
            {
                foreach (var startGold in new[] { false, true })
                {
                    var sim = new Sim { Offered = FortOffer.Offered, Gold = gold, Cleared = cleared, StartGold = startGold, Phase = cleared == 0 ? RunPhase.Preparation : RunPhase.Intermission, Gates = new[] { Broken() } };
                    var withRows = sim.View();
                    var without = withRows with { Fort = FortView.None };

                    Assert.AreEqual(ShopBot.Decide(without, 12f), ShopBot.Decide(withRows, 12f), $"gold {gold}, clear {cleared}, start_gold {startGold}");
                }
            }
        }
    }

    [TestMethod]
    public void AViewWithTheDefaultFort_DecidesAsBefore()
    {
        var view = new RunView(RunPhase.Preparation, 0, 0, 600, 60f, 100, 0, 0, 0f, Packs);

        Assert.AreEqual(FortView.None, view.Fort);
        Assert.AreEqual(BotActionKind.Buy, ShopBot.Decide(view, 0f).Kind);
    }

    [TestMethod]
    public void TheBotAnswersAnOpenPickBeforeAnyFortification()
    {
        var sim = new Sim();
        var card = new BoonCard("b", "{=K}b", "{=T}t", "", BoonCategory.WarChest);
        var view = sim.View() with { BoonOffer = new[] { card } };

        Assert.AreEqual(BotActionKind.Pick, ShopBot.Decide(view, 0f).Kind);
    }

    [TestMethod]
    public void NothingIsBought_OutsideTheShop()
    {
        var sim = new Sim { Phase = RunPhase.Wave };

        Assert.AreEqual(BotActionKind.Wait, ShopBot.Decide(sim.View(), 100f).Kind);
    }
}
