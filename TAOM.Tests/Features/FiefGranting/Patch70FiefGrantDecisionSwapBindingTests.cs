using System.Linq;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.FiefGranting;

/// <summary>
/// Drift-guard for Patch70 (#458). The swap replaces a vanilla <c>SettlementClaimantDecision</c>
/// with TAOM's subclass inside <c>Kingdom.AddDecision</c>, so it depends on more engine surface than
/// a normal patch: a writable parameter, a readable set of fields on the original, and two members
/// staying overridable. Each assertion below covers a way the feature goes quiet or refuses to load.
/// </summary>
[TestClass]
public class Patch70FiefGrantDecisionSwapBindingTests
{
    private const string KingdomTypeName = "TaleWorlds.CampaignSystem.Kingdom";
    private const string DecisionTypeName =
        "TaleWorlds.CampaignSystem.Election.SettlementClaimantDecision";
    private const string BaseDecisionTypeName = "TaleWorlds.CampaignSystem.Election.KingdomDecision";

    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    private static void RequireGame()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void AddDecision_BindingResolves_AgainstInstalledEngine()
    {
        RequireGame();

        var kingdomType = AccessTools.TypeByName(KingdomTypeName);
        Assert.IsNotNull(kingdomType, KingdomTypeName + " did not resolve — Patch70's target type is gone.");

        var method = AccessTools.Method(kingdomType, "AddDecision");
        Assert.IsNotNull(
            method,
            "Kingdom.AddDecision did not resolve — Patch70 would apply to nothing and every fief " +
            "grant would fall back to vanilla scoring.");

        var parameters = method.GetParameters();
        Assert.IsTrue(parameters.Length >= 1, "Kingdom.AddDecision takes no parameters — signature drifted.");

        Assert.AreEqual(
            "KingdomDecision", parameters[0].ParameterType.Name,
            "Kingdom.AddDecision's first parameter is no longer a KingdomDecision.");

        // Harmony binds prefix arguments by parameter NAME. A rename makes the category throw while
        // applying, which takes the rest of SubModule's batch down with it.
        Assert.AreEqual(
            "kingdomDecision", parameters[0].Name,
            "Kingdom.AddDecision's first parameter was renamed — Patch70's `ref KingdomDecision " +
            "kingdomDecision` prefix binds by name and would throw at module load.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void SettlementClaimantDecision_IsStillSubclassable()
    {
        RequireGame();

        var decisionType = AccessTools.TypeByName(DecisionTypeName);
        Assert.IsNotNull(decisionType, DecisionTypeName + " did not resolve.");

        Assert.IsFalse(
            decisionType.IsSealed,
            "SettlementClaimantDecision is now sealed — TaomSettlementClaimantDecision cannot derive " +
            "from it and the whole feature needs re-siting onto a Harmony postfix.");

        var ctor = decisionType.GetConstructors()
            .FirstOrDefault(c => c.GetParameters().Length == 4);
        Assert.IsNotNull(
            ctor,
            "SettlementClaimantDecision's 4-argument constructor (proposerClan, settlement, " +
            "capturerHero, clanToExclude) is gone — the subclass constructor no longer chains.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void ScoringMembers_AreStillOverridable()
    {
        RequireGame();

        var decisionType = AccessTools.TypeByName(DecisionTypeName);
        Assert.IsNotNull(decisionType, DecisionTypeName + " did not resolve.");

        var merit = AccessTools.Method(decisionType, "CalculateMeritOfOutcome");
        Assert.IsNotNull(merit, "SettlementClaimantDecision.CalculateMeritOfOutcome is gone.");
        Assert.IsTrue(
            merit.IsVirtual && !merit.IsFinal,
            "CalculateMeritOfOutcome is no longer overridable — TAOM's merit multiplier would never " +
            "run and fief grants would silently revert to vanilla scoring.");

        var baseType = AccessTools.TypeByName(BaseDecisionTypeName);
        Assert.IsNotNull(baseType, BaseDecisionTypeName + " did not resolve.");

        var kingsVote = AccessTools.PropertyGetter(baseType, "IsKingsVoteAllowed");
        Assert.IsNotNull(kingsVote, "KingdomDecision.IsKingsVoteAllowed is gone.");
        Assert.IsTrue(
            kingsVote.IsVirtual && !kingsVote.IsFinal,
            "IsKingsVoteAllowed is no longer overridable — a rich ruling clan would resume " +
            "overriding the council on every fief grant.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void CarriedOverState_IsStillReadableAndWritable()
    {
        RequireGame();

        var decisionType = AccessTools.TypeByName(DecisionTypeName);
        var baseType = AccessTools.TypeByName(BaseDecisionTypeName);
        Assert.IsNotNull(decisionType, DecisionTypeName + " did not resolve.");
        Assert.IsNotNull(baseType, BaseDecisionTypeName + " did not resolve.");

        Assert.IsNotNull(
            AccessTools.Field(decisionType, "Settlement"),
            "SettlementClaimantDecision.Settlement is gone — the swap cannot rebuild the decision.");
        Assert.IsNotNull(
            AccessTools.Field(decisionType, "ClanToExclude"),
            "SettlementClaimantDecision.ClanToExclude is gone — the annexation path's excluded clan " +
            "would be dropped and the clan just stripped of the fief could win it straight back.");
        Assert.IsNotNull(
            AccessTools.PropertyGetter(baseType, "ProposerClan"),
            "KingdomDecision.ProposerClan is gone — the swap cannot rebuild the decision.");

        // SettlementClaimantPreliminaryDecision.ApplyChosenOutcome sets IsEnforced on the line BEFORE
        // it calls AddDecision, so the flag is present on the instance Patch70 replaces. Losing the
        // setter would silently downgrade an enforced annexation to an ordinary one.
        Assert.IsNotNull(
            AccessTools.PropertySetter(baseType, "IsEnforced"),
            "KingdomDecision.IsEnforced is no longer writable — Patch70 cannot carry the enforced " +
            "flag onto the replacement decision.");
        Assert.IsNotNull(
            AccessTools.PropertySetter(baseType, "NotifyPlayer"),
            "KingdomDecision.NotifyPlayer is no longer writable — Patch70 cannot carry the " +
            "notification flag onto the replacement decision.");
    }
}
