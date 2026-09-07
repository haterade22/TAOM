using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.CampaignSystem;
using TAOM.Features.AiPartySize;

namespace TAOM.Tests.Features.AiPartySize;

/// <summary>
/// The caravan half of the bandit/caravan parity work.
///
/// Vanilla caps a caravan hard and low. <c>DefaultPartySizeLimitModel.CalculateMobilePartyMemberSizeLimit</c>
/// opens at 20 and its lord branch is guarded <c>!party.IsCaravan</c>, so a caravan collects neither
/// clan tier nor Steward; it gets only <c>10 * (Power &lt; 100 ? 1 : Power &lt; 200 ? 2 : 3)</c> for a
/// notable owner, or <c>IsElite ? 30 : 10</c> for the player. That is 30, 40 or 50 men, full stop.
///
/// The parity templates spawn rosters far above that, and an over-cap caravan is not merely held
/// there: <c>DesertionCampaignBehavior.DailyTickParty</c> gates on
/// <c>(IsLordParty || IsCaravan || IsGarrison)</c> and sheds a quarter of the excess every day with
/// no morale condition, and <c>DefaultPartySpeedCalculatingModel.GetOverPartySizeEffect</c> is
/// <c>1/(count/limit) - 1</c>, which is a -0.5 speed factor at twice the cap. Raising the templates
/// without raising the cap ships a strictly worse game than leaving both alone, so these two halves
/// are one atomic change.
/// </summary>
[TestClass]
public class CaravanPartySizeTests
{
    private const float Tolerance = 0.01f;

    [TestMethod]
    public void ShippedBonus_RaisesEveryVanillaNotableBand()
    {
        // Vanilla's three notable bands, by Owner.Power: 20 + 10, 20 + 20, 20 + 30.
        foreach (var (vanillaCap, expected) in new[]
                 {
                     (30f, 30f + AiPartySizeService.DefaultCaravanFlatBonus),
                     (40f, 40f + AiPartySizeService.DefaultCaravanFlatBonus),
                     (50f, 50f + AiPartySizeService.DefaultCaravanFlatBonus),
                 })
        {
            var limit = new ExplainedNumber(vanillaCap);

            AiPartySizeService.ApplyCaravanCapBonus(ref limit);

            Assert.AreEqual(expected, limit.ResultNumber, Tolerance,
                $"vanilla cap {vanillaCap} did not gain the flat bonus");
        }
    }

    [TestMethod]
    public void ShippedBonus_PreservesTheNotablePowerLadder()
    {
        // The bonus is flat rather than a multiplier precisely so vanilla's 10-man steps between
        // notable Power bands survive. A multiplier would widen them and make a rich notable's
        // caravan disproportionately large.
        var weak = new ExplainedNumber(30f);
        var middling = new ExplainedNumber(40f);
        var strong = new ExplainedNumber(50f);

        AiPartySizeService.ApplyCaravanCapBonus(ref weak);
        AiPartySizeService.ApplyCaravanCapBonus(ref middling);
        AiPartySizeService.ApplyCaravanCapBonus(ref strong);

        Assert.AreEqual(10f, middling.ResultNumber - weak.ResultNumber, Tolerance);
        Assert.AreEqual(10f, strong.ResultNumber - middling.ResultNumber, Tolerance);
    }

    [TestMethod]
    public void Bonus_IsNotAmplifiedByACultureFeatAlreadyInTheFrame()
    {
        // The discriminating case, and the reason this routes through AddResultFrameBonus rather
        // than ExplainedNumber.Add. Add lands in the BASE frame and the result is
        // BaseNumber * (1 + SumOfFactors), so a raw Add alongside a culture party-size feat would
        // be worth more than the men it names. CulturalFeatsService.ApplyPartySizeFeats runs before
        // this on the same frame and is NOT gated by party type, so a caravan of an evil culture
        // really does arrive here with a factor in play.
        var limit = new ExplainedNumber(40f);
        limit.AddFactor(0.20f);

        AiPartySizeService.ApplyCaravanCapBonus(ref limit);

        Assert.AreEqual(40f * 1.20f + AiPartySizeService.DefaultCaravanFlatBonus,
            limit.ResultNumber, Tolerance);
    }

    [TestMethod]
    public void Bonus_CoversTheLargestRosterTheShippedTemplatesCanSpawn()
    {
        // The coupling between the C# cap and the XML templates, asserted as a number rather than
        // left to a comment. The widest shipped caravan template is an elite one for a culture
        // whose guards are a tier low (harad / rhun / isengard), and
        // CaravanPartyComponent.InitializeCaravanOnCreation puts one CaravanMaster on the roster on
        // top of whatever the template drew. The shipped-data test pins the template side; this
        // pins that the smallest cap the engine can hand a caravan still covers it.
        const float weakestVanillaCap = 30f;   // a notable with Power < 100
        const float largestShippedRoster = 89f; // 88 template bodies + 1 CaravanMaster

        var limit = new ExplainedNumber(weakestVanillaCap);
        AiPartySizeService.ApplyCaravanCapBonus(ref limit);

        Assert.IsTrue(limit.ResultNumber >= largestShippedRoster,
            $"cap {limit.ResultNumber} is below the {largestShippedRoster}-man roster the shipped " +
            "templates can spawn, so caravans would shed a quarter of the excess daily and take " +
            "the over-size speed penalty");
    }

    [TestMethod]
    public void Bonus_IsUnconditional_SoItCannotDriftOutOfStepWithTheTemplates()
    {
        // Deliberately NOT behind EnableAiPartyScaling and deliberately not an MCM knob. The cap and
        // the template maxima are two halves of one change: a switch that reverts the cap while the
        // XML stays large would ship exactly the daily shed and the -0.5 speed factor this exists to
        // prevent, and a player would have no way to revert the other half. One constant, no
        // surfaces, no drift. csharp-architecture.md's "one surface, one clamp" taken to zero.
        Assert.IsTrue(AiPartySizeService.DefaultCaravanFlatBonus > 0f,
            "a zero bonus would silently re-introduce the over-cap shed");
    }

    [TestMethod]
    public void Bonus_AppliedTwice_IsNotCompounded()
    {
        // PartyBase.PartySizeLimit recomputes whenever MemberRoster.VersionNo changes, so this runs
        // repeatedly over a party's life. It must be a function of the frame it is handed, not an
        // accumulator.
        var once = new ExplainedNumber(40f);
        AiPartySizeService.ApplyCaravanCapBonus(ref once);

        var fresh = new ExplainedNumber(40f);
        AiPartySizeService.ApplyCaravanCapBonus(ref fresh);

        Assert.AreEqual(once.ResultNumber, fresh.ResultNumber, Tolerance);
    }
}
