using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.CareerSystem;
using TAOM.Features.CareerSystem.Diagnostics;
using TAOM.Features.CareerSystem.Domain;
using TAOM.Features.CareerSystem.UI;
using static TAOM.Tests.Infrastructure.RepoPaths;

namespace TAOM.Tests.Features.CareerSystem;

/// <summary>
/// #769 E11: ten campaign career passives, each read by a TAOM GameModel override. The type tables (consumed,
/// event-scoped, described) and the model wiring are pinned here without the game, apart from the two English-line
/// tests (they build a TextObject). The class-filtered, flat and icon tables are pinned where their owners are tested
/// (CareerConfigProviderTests, CareerEffectDisplayMapTests). A wiring pin reads the model's source and is only as strong as the shapes it rejects, so each one is run
/// against the cheapest regressions that keep the file compiling (a deleted call, the key swapped for another
/// hero resolution, two statements in the wrong order) and must fail on all of them.
/// </summary>
[TestClass]
public class CareerCampaignPassivesTests
{
    private static readonly PassiveEffectType[] Ten =
    {
        PassiveEffectType.InfluenceGain, PassiveEffectType.RecruitCost, PassiveEffectType.PrisonerConversion,
        PassiveEffectType.FoodConsumption, PassiveEffectType.WoundRecovery, PassiveEffectType.DailyGold,
        PassiveEffectType.LootGold, PassiveEffectType.RaidLoot, PassiveEffectType.TroopTraining, PassiveEffectType.TradePrices,
    };

    // ── the type tables ──

    [TestMethod]
    public void TheTenTypes_AreAppendedInOrderAfterTheTroopCounters()
    {
        // The enum is parsed by name from XML, but an ordinal shift would still move any numeric reader. Each of the
        // ten sits at its fixed ordinal after the last value before E11, so a later append does not break the pin.
        for (var i = 0; i < Ten.Length; i++)
            Assert.AreEqual((int)PassiveEffectType.TroopCrushHold + 1 + i, (int)Ten[i], Ten[i].ToString());
    }

    [TestMethod]
    public void TheTenTypes_AreAllConsumed()
    {
        foreach (var type in Ten)
            Assert.IsTrue(PassiveEffectConsumers.IsConsumed(type), $"{type} consumed");
    }

    [TestMethod]
    public void OnlyTheTwoBattleEndReads_AreEventScoped()
    {
        // A battle end is an event: InfluenceGain and LootGold. Everything else is asked per tooltip, per tick or per
        // raid step, where a line each time would drown the log, and the zero-base "0 -> 0" line is skipped in the service.
        CollectionAssert.AreEquivalent(
            new[] { PassiveEffectType.InfluenceGain, PassiveEffectType.LootGold },
            Ten.Where(CareerPerkLogPolicy.IsEventScoped).ToArray());
        foreach (var kept in new[] { PassiveEffectType.RenownGain, PassiveEffectType.TroopUpgradeCost, PassiveEffectType.HeroHealing, PassiveEffectType.SmithingCostReduction })
            Assert.IsTrue(CareerPerkLogPolicy.IsEventScoped(kept), $"{kept} stays event-scoped");
    }

    // type, the consumer the line must name, the evidence the line must leave
    private const string NoLine = "no runtime line; taom.career_perks shows the value";

    private static readonly (PassiveEffectType Type, string Consumer, string Evidence)[] Described =
    {
        (PassiveEffectType.InfluenceGain, "TaomBattleRewardModel.CalculateInfluenceGain", "[CareerPerks] InfluenceGain"),
        (PassiveEffectType.RecruitCost, "TaomPartyWageModel.GetTroopRecruitmentCost", NoLine),
        (PassiveEffectType.PrisonerConversion, "TaomPrisonerRecruitmentCalculationModel.GetConformityChangePerHour", NoLine),
        (PassiveEffectType.FoodConsumption, "TaomFoodConsumptionModel.CalculateDailyFoodConsumptionf", NoLine),
        (PassiveEffectType.WoundRecovery, "TaomPartyHealingModel.GetDailyHealingForRegulars", NoLine),
        (PassiveEffectType.DailyGold, "TaomClanFinanceModel.CalculateClanGoldChange", NoLine),
        (PassiveEffectType.LootGold, "TaomBattleRewardModel.GetLootGoldChances", "[CareerPerks] LootGold"),
        (PassiveEffectType.RaidLoot, "TaomRaidModel.GetRaidLootMultiplier", NoLine),
        (PassiveEffectType.TroopTraining, "TaomPartyTrainingModel.GetEffectiveDailyExperience", NoLine),
        (PassiveEffectType.TradePrices, "TaomPartyTradeModel.GetTradePenaltyFactor", NoLine),
    };

    [TestMethod]
    public void Describe_EachOfTheTen_NamesItsConsumerAndTheEvidenceItLeaves()
    {
        Assert.AreEqual(Ten.Length, Described.Length);
        foreach (var (type, consumer, evidence) in Described)
        {
            var text = CareerPerkConsumerMap.Describe(type);
            StringAssert.Contains(text, consumer, $"{type} must name its consumer");
            StringAssert.Contains(text, evidence, $"{type} must say what it leaves in the log");
        }
    }

    [TestMethod]
    public void Describe_PromisesALogLine_ExactlyWhenTheTypeIsEventScoped()
    {
        // The line a tester is told to look for must be one the code writes (testing-qa: an in-game check names
        // the log line and the trigger that produces it), and a type that writes one must say so.
        // Every campaign consumer: the ten E11 types plus the older party, troop and economy reads. The hero and
        // mount combat types are left out on purpose: their "[CareerPerks]" lines are the per-hit and per-stat
        // DEBUG lines, which the event policy does not govern.
        var campaign = Ten.Concat(new[]
        {
            PassiveEffectType.PartyMovementSpeed, PassiveEffectType.PartySpottingRange, PassiveEffectType.PartySize,
            PassiveEffectType.CompanionLimit, PassiveEffectType.InventoryCapacity, PassiveEffectType.TroopMorale,
            PassiveEffectType.TroopWages, PassiveEffectType.TroopUpgradeCost, PassiveEffectType.TroopSurvival,
            PassiveEffectType.HeroHealing, PassiveEffectType.RenownGain, PassiveEffectType.SmithingCostReduction,
        });
        foreach (var type in campaign)
            Assert.AreEqual(CareerPerkLogPolicy.IsEventScoped(type), CareerPerkConsumerMap.Describe(type).Contains("[CareerPerks]"), type.ToString());
    }

    [TestMethod]
    public void Describe_RaidLoot_SaysTheItemChanceSaturates()
    {
        // The item chance is 0.5 x the multiplier, so a total of +100% makes every drawn item certain.
        StringAssert.Contains(CareerPerkConsumerMap.Describe(PassiveEffectType.RaidLoot), "certain");
    }

    [TestMethod]
    public void Describe_RecruitCost_NamesTheOtherBuyers()
    {
        // The garrison auto-recruit and SupplyLines price a troop with the player as buyer too: they get the cut.
        var text = CareerPerkConsumerMap.Describe(PassiveEffectType.RecruitCost);
        StringAssert.Contains(text, "garrison auto-recruit");
        StringAssert.Contains(text, "SupplyLines");
    }

    // ── presentation ──

    [DataTestMethod]
    [TestCategory("RequiresGame")]
    [DataRow(PassiveEffectType.InfluenceGain, 0.10f, "+10% battle influence gain")]
    [DataRow(PassiveEffectType.RecruitCost, -0.20f, "-20% recruit cost")]
    [DataRow(PassiveEffectType.PrisonerConversion, 0.25f, "+25% prisoner conversion speed")]
    [DataRow(PassiveEffectType.FoodConsumption, -0.15f, "-15% food consumption")]
    [DataRow(PassiveEffectType.WoundRecovery, 0.30f, "+30% troop wound recovery")]
    [DataRow(PassiveEffectType.DailyGold, 40f, "+40 gold per day")]
    [DataRow(PassiveEffectType.LootGold, 0.20f, "+20% battle loot gold")]
    [DataRow(PassiveEffectType.RaidLoot, 0.50f, "+50% raid loot")]
    [DataRow(PassiveEffectType.TroopTraining, 5f, "+5 experience per troop per day")]
    [DataRow(PassiveEffectType.TradePrices, -0.20f, "-20% trade penalty")]
    public void Format_EachOfTheTen_RendersItsEnglishLine(PassiveEffectType type, float magnitude, string expected)
    {
        Assert.AreEqual(expected, CareerEffectDisplayMap.Format(type, magnitude));
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void Format_TroopTraining_CarriesTheClassSuffix()
    {
        Assert.AreEqual("+5 experience per troop per day for infantry",
            CareerEffectDisplayMap.Format(PassiveEffectType.TroopTraining, 5f, TroopClassMask.Infantry));
    }

    // ── the loot share rule, without the game ──

    [TestMethod]
    public void LootShares_ScaleOnlyTheCareerHeroesShares_KeepOrderAndKeys()
    {
        var passives = Substitute.For<ICareerPassiveService>();
        passives.ScaleByPassive("hero", Arg.Any<float>(), PassiveEffectType.LootGold).Returns(c => c.ArgAt<float>(1) * 1.5f);
        passives.ScaleByPassive(Arg.Is<string?>(id => id != "hero"), Arg.Any<float>(), Arg.Any<PassiveEffectType>()).Returns(c => c.ArgAt<float>(1));
        var chances = new List<KeyValuePair<string, float>>
        {
            new("lord-a", 0.5f), new("player", 0.3f), new("lord-b", 0.2f),
        };

        var scaled = CareerLootShares.Scale(chances, winner => winner == "player" ? "hero" : null, passives);

        CollectionAssert.AreEqual(new[] { "lord-a", "player", "lord-b" }, scaled.Select(k => k.Key).ToArray());
        Assert.AreEqual(0.5f, scaled[0].Value, 0.0001f);
        Assert.AreEqual(0.45f, scaled[1].Value, 0.0001f);
        Assert.AreEqual(0.2f, scaled[2].Value, 0.0001f);
        passives.Received(1).ScaleByPassive("hero", 0.3f, PassiveEffectType.LootGold);
    }

    [TestMethod]
    public void LootShares_AreNotRenormalised_SoABonusMintsGold()
    {
        // Vanilla's shares sum to 1 and the defeated side loses only the plundered total; the bonus is extra gold,
        // not a bigger cut of the same pot, and the other winners keep their share.
        var passives = Substitute.For<ICareerPassiveService>();
        passives.ScaleByPassive(Arg.Any<string?>(), Arg.Any<float>(), PassiveEffectType.LootGold).Returns(c => c.ArgAt<float>(1) * 2f);
        var chances = new[] { new KeyValuePair<string, float>("a", 0.5f), new KeyValuePair<string, float>("b", 0.5f) };

        var scaled = CareerLootShares.Scale(chances, _ => "hero", passives);

        Assert.AreEqual(2f, scaled.Sum(k => k.Value), 0.0001f);
    }

    [TestMethod]
    public void LootShares_EmptyList_ComesBackEmpty()
    {
        var scaled = CareerLootShares.Scale(new List<KeyValuePair<string, float>>(), _ => "hero", Substitute.For<ICareerPassiveService>());

        Assert.AreEqual(0, scaled.Count);
    }

    // ── model wiring pins ──

    private const string RewardModel = "Main/Features/CulturalFeats/Models/TaomBattleRewardModel.cs";
    private const string WageModel = "Main/Features/TroopProgression/Models/TaomPartyWageModel.cs";
    private const string PrisonerModel = "Main/Features/PrisonerRecruitment/Models/TaomPrisonerRecruitmentCalculationModel.cs";
    private const string FoodModel = "Main/Features/CulturalFeats/Models/TaomFoodConsumptionModel.cs";
    private const string HealingModel = "Main/Features/BattleBalance/Models/TaomPartyHealingModel.cs";
    private const string FinanceModel = "Main/Features/CulturalFeats/Models/TaomClanFinanceModel.cs";
    private const string RaidModel = "Main/Features/CulturalFeats/Models/TaomRaidModel.cs";
    private const string TrainingModel = "Main/Features/SeasonedHosts/Models/TaomPartyTrainingModel.cs";
    private const string SurvivalLine = "Main/Features/BattleBalance/Hooks/SurvivalChanceLine.cs";
    private const string TradeModel = "Main/Features/CareerSystem/Models/TaomPartyTradeModel.cs";

    /// <summary>A member of a model: where it is, how to find it, and the statements that must appear in its body in this order.</summary>
    private sealed class Pin
    {
        public Pin(string file, string signature, string[] inOrder)
        {
            File = file;
            Signature = signature;
            InOrder = inOrder;
        }

        public string File { get; }
        public string Signature { get; }
        public string[] InOrder { get; }

        public override string ToString() => $"{System.IO.Path.GetFileName(File)}: {Signature}";
    }

    private static readonly Pin[] Pins =
    {
        new Pin(RewardModel, "public override ExplainedNumber CalculateInfluenceGain(", new[]
        {
            "base.CalculateInfluenceGain(",
            "_careerPassives.ApplyFactor(CareerPassiveHero.ResolveId(winnerParty), ref result, PassiveEffectType.InfluenceGain);",
        }),
        new Pin(RewardModel, "public override MBReadOnlyList<KeyValuePair<MapEventParty, float>> GetLootGoldChances(", new[]
        {
            "CareerLootShares.Scale(",
            "base.GetLootGoldChances(winnerParties)",
            "winner => winner.Party.LeaderHero?.StringId",
            "_careerPassives",
        }),
        new Pin(WageModel, "public override ExplainedNumber GetTroopRecruitmentCost(", new[]
        {
            "var buyerPerks = ResolveBuyerRecruitmentPerks(buyerHero, troop, _careerPassives.GetPassiveMagnitude(buyerHero?.StringId, PassiveEffectType.RecruitCost))",
            "_wageModifiers.CalculateRecruitmentCost(",
            "buyerPerks);",
            "return new ExplainedNumber(cost, includeDescriptions: false);",
        }),
        new Pin(WageModel, "private static RecruitmentPerkInputs ResolveBuyerRecruitmentPerks(", new[]
        {
            "float careerRecruitCostFactor",
            "careerRecruitCostFactor: careerRecruitCostFactor",
        }),
        new Pin(PrisonerModel, "public override ExplainedNumber GetConformityChangePerHour(", new[]
        {
            "base.GetConformityChangePerHour(party, troopToBoost)",
            "_careerPassives.ApplyFactor(party?.LeaderHero?.StringId, ref result, PassiveEffectType.PrisonerConversion);",
        }),
        new Pin(FoodModel, "public override ExplainedNumber CalculateDailyFoodConsumptionf(", new[]
        {
            "base.CalculateDailyFoodConsumptionf(party, baseConsumption)",
            "_feats.ApplyFoodConsumptionFeats(",
            "_careerPassives.ApplyFactor(party?.LeaderHero?.StringId, ref result, PassiveEffectType.FoodConsumption);",
            "_aiPartySize.ApplyAiFoodRelief(",
        }),
        new Pin(SurvivalLine, "public static float Apply(", new[]
        {
            "var heroId = CareerPassiveHero.ResolveId(party);",
            "return Math.Min(1f, careerPassives.ScaleByPassive(heroId, result, PassiveEffectType.TroopSurvival));",
        }),
        new Pin(HealingModel, "public override ExplainedNumber GetDailyHealingForRegulars(", new[]
        {
            "base.GetDailyHealingForRegulars(party, isPrisoners, includeDescriptions)",
            "_careerPassives.ApplyWoundRecovery(party?.LeaderHero?.StringId, isPrisoners, ref result);",
        }),
        new Pin(FinanceModel, "public override ExplainedNumber CalculateClanGoldChange(", new[]
        {
            "base.CalculateClanGoldChange(",
            "AddServiceWageLine(clan, applyWithdrawals, ref result);",
            "AddCareerDailyGold(clan, ref result);",
        }),
        new Pin(FinanceModel, "public override ExplainedNumber CalculateClanIncome(", new[]
        {
            "base.CalculateClanIncome(",
            "AddServiceWageLine(clan, applyWithdrawals, ref result);",
            "AddCareerDailyGold(clan, ref result);",
        }),
        new Pin(FinanceModel, "private void AddCareerDailyGold(", new[]
        {
            "_careerPassives.ApplyFlat(clan?.Leader?.StringId, ref result, PassiveEffectType.DailyGold);",
        }),
        new Pin(RaidModel, "public override float GetRaidLootMultiplier(", new[]
        {
            "_careerPassives.ScaleByPassive(receivingParty?.LeaderHero?.StringId, base.GetRaidLootMultiplier(receivingParty), PassiveEffectType.RaidLoot)",
        }),
        new Pin(TrainingModel, "public override ExplainedNumber GetEffectiveDailyExperience(", new[]
        {
            // The Seasoned Hosts factor goes on first: ApplyFlat divides out only the factors already on the number.
            "WithBonus(base.GetEffectiveDailyExperience(mobileParty, troop), mobileParty)",
            "_careerPassives.ApplyFlat(mobileParty?.LeaderHero?.StringId, ref result, PassiveEffectType.TroopTraining, TroopClassMapper.FromCharacter(troop.Character));",
            "return result;",
        }),
        new Pin(TradeModel, "public override float GetTradePenaltyFactor(", new[]
        {
            "_careerPassives.ScaleByPassive(party?.LeaderHero?.StringId, base.GetTradePenaltyFactor(party), PassiveEffectType.TradePrices)",
        }),
    };

    /// <summary>The text of a member, from its signature to the end of its body (a brace block or an expression).</summary>
    internal static string MemberText(string source, string signature)
    {
        var start = source.IndexOf(signature, StringComparison.Ordinal);
        if (start < 0) return "";
        var open = source.IndexOf('{', start);
        var arrow = source.IndexOf("=>", start, StringComparison.Ordinal);
        if (arrow >= 0 && (open < 0 || arrow < open))
            return source.Substring(start, source.IndexOf(';', arrow) - start + 1);
        if (open < 0) return "";
        var depth = 0;
        for (var i = open; i < source.Length; i++)
        {
            if (source[i] == '{') depth++;
            else if (source[i] == '}' && --depth == 0)
                return source.Substring(start, i - start + 1);
        }
        return "";
    }

    /// <summary>True when every fragment appears in the member's text, in the given order.</summary>
    internal static bool HasInOrder(string memberText, IEnumerable<string> fragments)
    {
        var at = 0;
        foreach (var fragment in fragments)
        {
            var found = memberText.IndexOf(fragment, at, StringComparison.Ordinal);
            if (found < 0) return false;
            at = found + fragment.Length;
        }
        return true;
    }

    private static string Source(string file) => ReadSource(file, stripComments: true);

    [TestMethod]
    public void EveryModelBody_HoldsItsStatementsInOrder()
    {
        foreach (var pin in Pins)
        {
            var member = MemberText(Source(pin.File), pin.Signature);
            Assert.IsTrue(member.Length > 0, $"{pin}: member not found");
            Assert.IsTrue(HasInOrder(member, pin.InOrder), $"{pin}: expected, in this order:\n  " + string.Join("\n  ", pin.InOrder));
        }
    }

    [TestMethod]
    public void EveryPin_RejectsADeletedStatement()
    {
        foreach (var pin in Pins)
        {
            var member = MemberText(Source(pin.File), pin.Signature);
            foreach (var fragment in pin.InOrder)
                Assert.IsFalse(HasInOrder(member.Replace(fragment, ""), pin.InOrder), $"{pin}: deleting '{fragment}' kept the pin green");
        }
    }

    [TestMethod]
    public void EveryPin_RejectsTwoStatementsSwapped()
    {
        foreach (var pin in Pins.Where(p => p.InOrder.Length >= 2))
        {
            var member = MemberText(Source(pin.File), pin.Signature);
            for (var i = 0; i + 1 < pin.InOrder.Length; i++)
            {
                var a = pin.InOrder[i];
                var b = pin.InOrder[i + 1];
                var swapped = member.Replace(a, "\u0001").Replace(b, a).Replace("\u0001", b);
                Assert.IsFalse(HasInOrder(swapped, pin.InOrder), $"{pin}: swapping '{a}' and '{b}' kept the pin green");
            }
        }
    }

    [TestMethod]
    public void EachFinanceProjection_AddsTheCareerDailyGoldExactlyOnce()
    {
        foreach (var signature in new[] { "public override ExplainedNumber CalculateClanGoldChange(", "public override ExplainedNumber CalculateClanIncome(" })
        {
            var member = MemberText(Source(FinanceModel), signature);
            Assert.AreEqual(1, CountOf(member, "AddCareerDailyGold(clan, ref result);"), signature);
        }
    }

    private static int CountOf(string text, string fragment)
    {
        var count = 0;
        for (var at = text.IndexOf(fragment, StringComparison.Ordinal); at >= 0; at = text.IndexOf(fragment, at + fragment.Length, StringComparison.Ordinal))
            count++;
        return count;
    }

    [TestMethod]
    public void EveryPin_RejectsTheKeyReplacedByAnotherHeroResolution()
    {
        // The cheapest wrong key: owner-first where the engine pays the leader (or the reverse). Every pinned
        // statement that names a hero must stop matching when the hero expression changes.
        foreach (var pin in Pins)
        {
            var member = MemberText(Source(pin.File), pin.Signature);
            foreach (var key in new[] { "LeaderHero?.StringId", "CareerPassiveHero.ResolveId(" })
            {
                if (!pin.InOrder.Any(f => f.Contains(key))) continue;
                var mutated = member.Replace(key, key == "LeaderHero?.StringId" ? "Owner?.StringId" : "CareerPassiveHero.ResolveOwner(");
                Assert.IsFalse(HasInOrder(mutated, pin.InOrder), $"{pin}: changing '{key}' kept the pin green");
            }
        }
    }

    [TestMethod]
    public void TheOverrideBodies_HaveNoBranchOrLoop()
    {
        // gamemodels.md rule 4: a body is a boundary conversion and one delegate. The branching lives in the service.
        // The two exceptions have branches that were there before E11: the recruit-perk resolver and the survival roll's culture block.
        foreach (var pin in Pins.Where(p => !p.Signature.Contains("ResolveBuyerRecruitmentPerks") && p.File != SurvivalLine))
        {
            var member = MemberText(Source(pin.File), pin.Signature);
            foreach (var banned in new[] { "if (", "foreach", "for (", "while (", "switch", "? " })
                Assert.IsFalse(member.Contains(banned), $"{pin}: contains '{banned}'");
        }
    }

    // The cost is rebuilt from an int; a factor applied to the rebuilt number would sit outside the 1-gold floor.
    // The career term goes into WageModifierService.CalculateRecruitmentCost, before the clamp.
    private static bool LeavesTheRebuiltCostAlone(string body)
        => !(body.Contains("ApplyFactor(") || body.Contains("ApplyFlat(") || body.Contains("AddFactor("));

    [TestMethod]
    public void TheWageModel_NeverMultipliesTheRebuiltRecruitCost()
    {
        var body = MemberText(Source(WageModel), "public override ExplainedNumber GetTroopRecruitmentCost(");

        Assert.IsTrue(LeavesTheRebuiltCostAlone(body), body);
    }

    [TestMethod]
    public void TheWageModelPin_RejectsAFactorOnTheRebuiltNumber()
    {
        var body = MemberText(Source(WageModel), "public override ExplainedNumber GetTroopRecruitmentCost(");
        var mutant = body.Replace("return new ExplainedNumber(cost, includeDescriptions: false);",
            "var rebuilt = new ExplainedNumber(cost, includeDescriptions: false); _careerPassives.ApplyFactor(buyerHero?.StringId, ref rebuilt, PassiveEffectType.RecruitCost); return rebuilt;");

        Assert.AreNotEqual(body, mutant, "the mutant must carry the regression");
        Assert.IsFalse(LeavesTheRebuiltCostAlone(mutant));
    }

    [TestMethod]
    public void TheEnlistmentWageLine_StaysApplyWithdrawalsGated_AndHoldsNoCareerGold()
    {
        // AddServiceWageLine returns when applyWithdrawals is true, and ClanVariablesCampaignBehavior.DailyTickClan
        // is the call that pays: the career gold must sit OUTSIDE it, or the preview shows it and the payment omits it.
        var line = MemberText(Source(FinanceModel), "private void AddServiceWageLine(");
        StringAssert.Contains(line, "if (applyWithdrawals");
        Assert.IsFalse(line.Contains("DailyGold") || line.Contains("_careerPassives"), line);
    }

    [TestMethod]
    public void TheFinancePin_RejectsTheGoldMovedInsideTheWageLine()
    {
        var source = Source(FinanceModel);
        var mutant = source
            .Replace("AddCareerDailyGold(clan, ref result);", "")
            .Replace("var wage = _wagePreview?.GetDailyWage() ?? 0;", "_careerPassives.ApplyFlat(clan?.Leader?.StringId, ref result, PassiveEffectType.DailyGold); var wage = _wagePreview?.GetDailyWage() ?? 0;");
        var line = MemberText(mutant, "private void AddServiceWageLine(");

        Assert.IsTrue(line.Contains("DailyGold"), "the mutant must contain the regression");
        Assert.IsFalse(HasInOrder(MemberText(mutant, "public override ExplainedNumber CalculateClanGoldChange("), Pins.First(p => p.Signature.Contains("CalculateClanGoldChange(")).InOrder));
    }

    [TestMethod]
    public void TheHeroHealingLine_IsUntouched_AndWoundRecoveryHasItsOwnRule()
    {
        // HeroHealing keeps its own method (its starving and prisoner flaws are a separate follow-up); the new
        // WoundRecovery read goes through ApplyWoundRecovery, which gates the sign and the prisoner path.
        var source = Source(HealingModel);
        var heroHealing = MemberText(source, "public override ExplainedNumber GetDailyHealingHpForHeroes(");
        StringAssert.Contains(heroHealing, "_careerPassives.ApplyFactor(CareerPassiveHero.ResolveId(party), ref result, PassiveEffectType.HeroHealing);");
        Assert.IsFalse(heroHealing.Contains("ApplyWoundRecovery"));
        var regulars = MemberText(source, "public override ExplainedNumber GetDailyHealingForRegulars(");
        Assert.IsFalse(regulars.Contains("ApplyFactor(") || regulars.Contains("HeroHealing"), regulars);
    }

    [TestMethod]
    public void TheSurvivalPin_RejectsTheScaleThrownAwayAndTheMinDropped()
    {
        // The call and the assignment that carries its answer are one statement: if the result were discarded, or
        // the 1.0 cap dropped (a +20% pip on a 90% chance would read 108%), the roll would change and the pin must fail.
        var body = MemberText(Source(SurvivalLine), "public static float Apply(");
        var pin = Pins.First(p => p.File == SurvivalLine);
        var discarded = body.Replace("return Math.Min(1f, careerPassives.ScaleByPassive(heroId, result, PassiveEffectType.TroopSurvival));",
            "careerPassives.ScaleByPassive(heroId, result, PassiveEffectType.TroopSurvival); return result;");
        var uncapped = body.Replace("return Math.Min(1f, careerPassives.ScaleByPassive(heroId, result, PassiveEffectType.TroopSurvival));",
            "return careerPassives.ScaleByPassive(heroId, result, PassiveEffectType.TroopSurvival);");

        Assert.IsTrue(HasInOrder(body, pin.InOrder));
        Assert.AreNotEqual(body, discarded, "the mutant must carry the regression");
        Assert.AreNotEqual(body, uncapped, "the mutant must carry the regression");
        Assert.IsFalse(HasInOrder(discarded, pin.InOrder));
        Assert.IsFalse(HasInOrder(uncapped, pin.InOrder));
    }

    [TestMethod]
    public void TheHealingPin_RejectsALiteralFalseForThePrisonerFlag()
    {
        var regulars = MemberText(Source(HealingModel), "public override ExplainedNumber GetDailyHealingForRegulars(");
        var mutant = regulars.Replace("party?.LeaderHero?.StringId, isPrisoners, ref result", "party?.LeaderHero?.StringId, false, ref result");
        var pin = Pins.First(p => p.Signature.Contains("GetDailyHealingForRegulars"));

        // The owner-first key reaches the player's garrisons, militias and caravans: the pin must reject it.
        var ownerFirst = regulars.Replace("party?.LeaderHero?.StringId", "CareerPassiveHero.ResolveId(party)");

        Assert.IsTrue(HasInOrder(regulars, pin.InOrder));
        Assert.IsFalse(HasInOrder(mutant, pin.InOrder));
        Assert.AreNotEqual(regulars, ownerFirst, "the mutant must carry the regression");
        Assert.IsFalse(HasInOrder(ownerFirst, pin.InOrder));
    }
}
