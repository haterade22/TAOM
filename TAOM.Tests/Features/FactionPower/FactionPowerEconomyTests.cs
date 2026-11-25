using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.CampaignSystem;
using TAOM.Features.CulturalFeats;
using TAOM.Features.FactionPower;
using TAOM.Features.TroopProgression;
using TAOM.Features.WarChronicle.Effects;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.FactionPower;

/// <summary>Faction power PR B: militia, volunteer refill, auto-resolve strike and wage relief.</summary>
[TestClass]
[TestCategory("RequiresGame")]
public class FactionPowerEconomyTests
{
    private const float Tolerance = 0.001f;

    private readonly FakeFactionPowerSettings _settings = new FakeFactionPowerSettings();

    private FactionPowerService Create() => new FactionPowerService(_settings);

    // Iterates today's militia rule to its fixed point: inflow minus 0.025 x militia, plus the faction power delta.
    private static float Equilibrium(float inflow, float multiplier)
    {
        float militia = 0f;
        for (int day = 0; day < 2000; day++)
        {
            float change = inflow - FactionPowerMath.MilitiaRetireRate * militia;
            militia += change + FactionPowerMath.MilitiaDelta(change, militia, multiplier);
        }
        return militia;
    }

    [DataTestMethod]
    [DataRow(1f, 120f)]
    [DataRow(1.5f, 180f)]
    [DataRow(0.5f, 60f)]
    [DataRow(2f, 240f)]
    public void MilitiaDelta_EquilibriumScalesByTheMultiplier(float multiplier, float expected)
    {
        Assert.AreEqual(expected, Equilibrium(3f, multiplier), 0.5f);
    }

    [TestMethod]
    public void MilitiaDelta_AtOneOrWithBadInput_IsZero()
    {
        Assert.AreEqual(0f, FactionPowerMath.MilitiaDelta(2f, 10f, 1f));
        Assert.AreEqual(0f, FactionPowerMath.MilitiaDelta(-5f, 0f, 1.5f), "a non-positive inflow");
        Assert.AreEqual(0f, FactionPowerMath.MilitiaDelta(float.NaN, 10f, 1.5f));
        Assert.AreEqual(0f, FactionPowerMath.MilitiaDelta(2f, float.NaN, 1.5f));
        Assert.AreEqual(0f, FactionPowerMath.MilitiaDelta(2f, 10f, 3f), "out of range");
    }

    [TestMethod]
    public void StrikeMultiplier_IsTheSquareRoot_AndExactlyOneAtOne()
    {
        Assert.AreEqual(0.7071f, FactionPowerMath.StrikeMultiplier(0.5f), Tolerance);
        Assert.AreEqual(1.4142f, FactionPowerMath.StrikeMultiplier(2f), Tolerance);
        Assert.AreEqual(1f, FactionPowerMath.StrikeMultiplier(1f));
        Assert.AreEqual(1f, FactionPowerMath.StrikeMultiplier(float.NaN));
    }

    [TestMethod]
    public void WageMultiplier_ReliefOnlyAboveOne()
    {
        Assert.AreEqual(1f, FactionPowerMath.WageMultiplier(1f));
        Assert.AreEqual(1f, FactionPowerMath.WageMultiplier(0.5f), "a weaker realm pays normal wages");
        Assert.AreEqual(1f / 1.5f, FactionPowerMath.WageMultiplier(1.5f), 1e-6f);
        Assert.AreEqual(0.5f, FactionPowerMath.WageMultiplier(2f), 1e-6f);
        Assert.AreEqual(1f, FactionPowerMath.WageMultiplier(float.NaN));
    }

    [TestMethod]
    public void AddResultFrameDelta_LandsAsThatManyPointsInTheResult()
    {
        var n = new ExplainedNumber(10f);
        n.AddFactor(1f);

        FactionPowerMath.AddResultFrameDelta(ref n, 5f, null);

        Assert.AreEqual(25f, n.ResultNumber, Tolerance);
    }

    [TestMethod]
    public void AddResultFrameDelta_NaNOrZeroDelta_LeavesTheNumber()
    {
        var n = new ExplainedNumber(10f);

        FactionPowerMath.AddResultFrameDelta(ref n, float.NaN, null);
        FactionPowerMath.AddResultFrameDelta(ref n, 0f, null);

        Assert.AreEqual(10f, n.ResultNumber);
    }

    [TestMethod]
    public void Militia_At100_IsBitIdentical()
    {
        var change = new ExplainedNumber(2f, true);
        var before = (change.BaseNumber, change.SumOfFactors, change.ResultNumber, change.GetLines().Count);

        Create().ScaleMilitiaChange(1f, false, 50f, ref change);

        Assert.AreEqual(before, (change.BaseNumber, change.SumOfFactors, change.ResultNumber, change.GetLines().Count));
    }

    [TestMethod]
    public void Militia_150_AddsHalfTheInflow_AndNeverForThePlayerClan()
    {
        // Inflow 3 per day, 40 militia: the change is 3 - 1 = 2; the inflow is 3, so 150 percent adds 1.5.
        var change = new ExplainedNumber(2f);
        Create().ScaleMilitiaChange(1.5f, false, 40f, ref change);
        Assert.AreEqual(3.5f, change.ResultNumber, Tolerance);

        var player = new ExplainedNumber(2f);
        Create().ScaleMilitiaChange(1.5f, true, 40f, ref player);
        Assert.AreEqual(2f, player.ResultNumber, Tolerance);
    }

    [TestMethod]
    public void Strike_IsScaledBySqrt_NeverForTheMainPartyOrThePlayerClan()
    {
        var damage = new ExplainedNumber(40f);
        Create().ScaleStrike(2f, false, false, ref damage);
        Assert.AreEqual(40f * 1.4142f, damage.ResultNumber, 0.01f);

        var main = new ExplainedNumber(40f);
        Create().ScaleStrike(2f, true, false, ref main);
        Assert.AreEqual(40f, main.ResultNumber, Tolerance);

        var clan = new ExplainedNumber(40f);
        Create().ScaleStrike(2f, false, true, ref clan);
        Assert.AreEqual(40f, clan.ResultNumber, Tolerance);

        var weak = new ExplainedNumber(40f);
        Create().ScaleStrike(0.5f, false, false, ref weak);
        Assert.AreEqual(40f * 0.7071f, weak.ResultNumber, 0.01f);
    }

    [TestMethod]
    public void Strike_At100_IsBitIdentical()
    {
        var damage = new ExplainedNumber(40f, true);
        damage.AddFactor(-0.1f);
        var before = (damage.BaseNumber, damage.SumOfFactors, damage.ResultNumber, damage.GetLines().Count);

        Create().ScaleStrike(1f, false, false, ref damage);

        Assert.AreEqual(before, (damage.BaseNumber, damage.SumOfFactors, damage.ResultNumber, damage.GetLines().Count));
    }

    [TestMethod]
    public void Wage_AboveOne_AiLordPartyAndGarrisonPayOneOverS()
    {
        var lord = new ExplainedNumber(300f);
        Create().ScaleWage(2f, false, false, true, true, false, ref lord);
        Assert.AreEqual(150f, lord.ResultNumber, Tolerance);

        var garrison = new ExplainedNumber(300f);
        Create().ScaleWage(1.5f, true, false, false, false, false, ref garrison);
        Assert.AreEqual(200f, garrison.ResultNumber, Tolerance);
    }

    [TestMethod]
    public void Wage_AtOrBelowOne_PaysNormal_AndNeverForThePlayerSide()
    {
        var weak = new ExplainedNumber(300f);
        Create().ScaleWage(0.5f, false, false, true, true, false, ref weak);
        Assert.AreEqual(300f, weak.ResultNumber, Tolerance);

        var main = new ExplainedNumber(300f);
        Create().ScaleWage(2f, false, true, true, true, false, ref main);
        Assert.AreEqual(300f, main.ResultNumber, Tolerance);

        var clan = new ExplainedNumber(300f);
        Create().ScaleWage(2f, false, false, true, true, true, ref clan);
        Assert.AreEqual(300f, clan.ResultNumber, Tolerance);

        var playerFief = new ExplainedNumber(300f);
        Create().ScaleWage(2f, true, false, false, false, true, ref playerFief);
        Assert.AreEqual(300f, playerFief.ResultNumber, Tolerance);

        var caravan = new ExplainedNumber(300f);
        Create().ScaleWage(2f, false, false, false, false, false, ref caravan);
        Assert.AreEqual(300f, caravan.ResultNumber, Tolerance);
    }

    private VolunteerProductionService Volunteers(float war = 1f)
    {
        var feats = Substitute.For<ICulturalFeatsService>();
        var warService = Substitute.For<IWarEffectService>();
        warService.GetMultiplier(Arg.Any<string>(), Arg.Any<WarEffectKind>()).Returns(war);
        return new VolunteerProductionService(feats, warService, _settings);
    }

    [TestMethod]
    public void Volunteers_AfterTheWarStep_WithOneClamp()
    {
        _settings.Percents["mordor"] = 150;

        Assert.AreEqual(0.4f * 1.2f * 1.5f, Volunteers(1.2f).Compute(0.4f, null, "k", false, "mordor"), 1e-6f);
        // War 1.5 and power 0.5 on 0.9: one clamp at the end gives 0.675; a clamp after the war step first would give 0.5.
        _settings.Percents["gondor"] = 50;
        Assert.AreEqual(0.9f * 1.5f * 0.5f, Volunteers(1.5f).Compute(0.9f, null, "k", false, "gondor"), 1e-6f);
    }

    [TestMethod]
    public void Volunteers_ClampAtOne_AndPlayerClanGetsNothing()
    {
        _settings.Percents["mordor"] = 200;

        Assert.AreEqual(1f, Volunteers().Compute(0.7f, null, null, false, "mordor"));
        Assert.AreEqual(0.7f, Volunteers().Compute(0.7f, null, null, true, "mordor"));
    }

    [TestMethod]
    public void Volunteers_At100OrNoRealm_EqualToday()
    {
        foreach (var p in new[] { 0f, 0.1f, 0.37f, 0.9f, 1f, 1.3f })
        {
            Assert.AreEqual(p, Volunteers().Compute(p, null, null, false, null));
            Assert.AreEqual(p, Volunteers().Compute(p, null, null, false, "mordor"));
            Assert.AreEqual(p, Volunteers().Compute(p, null, null, false));
        }
    }

    [TestMethod]
    public void TheModels_CallTheEconomyHooks()
    {
        var militia = RepoPaths.ReadSource("Main/Features/CulturalFeats/Models/TaomSettlementMilitiaModel.cs", stripComments: true);
        var wage = RepoPaths.ReadSource("Main/Features/TroopProgression/Models/TaomPartyWageModel.cs", stripComments: true);
        var combat = RepoPaths.ReadSource("Main/Features/BattleBalance/Models/TaomCombatSimulationModel.cs", stripComments: true);
        var volunteer = RepoPaths.ReadSource("Main/Features/TroopProgression/Models/TaomVolunteerModel.cs", stripComments: true);

        StringAssert.Contains(militia, "_factionPower.ApplyMilitiaChange(settlement, ref result)");
        StringAssert.Contains(combat, "FactionPower.ApplyStrike(strikerParty, ref result)");
        StringAssert.Contains(volunteer, "FactionPowerRealm.KeyOf(settlement?.MapFaction)");
        Assert.IsFalse(RepoPaths.ReadSource("Main/SubModule.cs", stripComments: true).Contains("FactionPower"));

        // The wage relief sits after the AI wage relief and before the free garrison upkeep zeroes the line.
        int ai = wage.IndexOf("_aiPartySize.ApplyAiWageRelief(", System.StringComparison.Ordinal);
        int power = wage.IndexOf("FactionPowerWageLine.Apply(mobileParty, ref result)", System.StringComparison.Ordinal);
        int free = wage.IndexOf("FreeGarrisonUpkeepLine.Wage(", System.StringComparison.Ordinal);
        Assert.IsTrue(ai >= 0 && ai < power && power < free);
    }
}
