using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.CampaignSystem.Actions;
using TAOM.Features.Diplomacy.Hooks;

namespace TAOM.Tests.Features.Diplomacy;

/// <summary>
/// Which DeclareWarAction reasons count as the player's own hostile act, so the phase vetoes let the
/// war start. The crime-rating case matters most: with the war allowed, ChangeCrimeRatingAction
/// latches once instead of re-applying a relation loss every day.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class DeclareWarActionPatchTests
{
    [DataTestMethod]
    [DataRow(DeclareWarAction.DeclareWarDetail.CausedByPlayerHostility, true)]
    [DataRow(DeclareWarAction.DeclareWarDetail.CausedByCrimeRatingChange, true)]
    [DataRow(DeclareWarAction.DeclareWarDetail.Default, false)]
    [DataRow(DeclareWarAction.DeclareWarDetail.CausedByKingdomDecision, false)]
    [DataRow(DeclareWarAction.DeclareWarDetail.CausedByRebellion, false)]
    [DataRow(DeclareWarAction.DeclareWarDetail.CausedByKingdomCreation, true)]
    [DataRow(DeclareWarAction.DeclareWarDetail.CausedByClaimOnThrone, false)]
    [DataRow(DeclareWarAction.DeclareWarDetail.CausedByCallToWarAgreement, false)]
    [DataRow(DeclareWarAction.DeclareWarDetail.CausedByQuest, false)]
    public void IsCausedByPlayer_Detail_MatchesTheReason(DeclareWarAction.DeclareWarDetail detail, bool expected)
    {
        Assert.AreEqual(expected, DeclareWarAction_ApplyInternal_Patch.IsCausedByPlayer(detail));
    }

    // #1283: the declaration line of a call to war names the called realm and the target, never the realm that made the call.
    [TestMethod]
    public void CallToWarLine_NamesTheCallerTheCalledRealmAndTheTarget()
    {
        Assert.AreEqual(
            "[Diplomacy] Call to war started: rivendell calls empire_w against empire",
            TAOM.Features.Diplomacy.DiplomacyBehavior.CallToWarLine("rivendell", "empire_w", "empire"));
    }

    // #1283: a war TAOM lets through left no line, so a run could not say who started a war or by which path.
    [TestMethod]
    public void AllowedLine_NamesBothRealmsAndTheEnginePath()
    {
        Assert.AreEqual(
            "[Diplomacy] War declaration allowed: empire_w -> empire (CausedByCallToWarAgreement)",
            DeclareWarAction_ApplyInternal_Patch.AllowedLine(
                "empire_w", "empire", DeclareWarAction.DeclareWarDetail.CausedByCallToWarAgreement));
    }
}
