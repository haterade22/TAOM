using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Diplomacy.Hooks;
using TAOM.Tests.Infrastructure;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.Diplomacy;

/// <summary>
/// Drift-guard for the War of the Ring war prefixes (Patch11_Diplomacy). Harmony binds a prefix's
/// parameters by NAME, so a renamed engine parameter is a legal, silent change that leaves the prefix
/// receiving null: its null guard then waves every war through and the phase rule stops working while
/// every behavioural test stays green (lessons/harmony-il.md, "Harmony binds prefix/postfix
/// parameters by NAME"). The decision prefixes also read members in their bodies, which fail at JIT
/// time, before the body's own try/catch, if one is gone ("A patch's own try/catch cannot survive a
/// JIT-time member-resolution failure"). Every one is pinned here.
/// </summary>
[TestClass]
public class DiplomacyWarPrefixBindingTests
{
    private const string Ns = "TaleWorlds.CampaignSystem.";

    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    private static void RequireGame()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));
    }

    private static Type Resolve(string name)
    {
        var type = AccessTools.TypeByName(name);
        Assert.IsNotNull(type, name + " did not resolve.");
        return type;
    }

    private static MethodInfo SingleMethod(Type type, string name)
    {
        var overloads = type.GetMethods(AccessTools.all).Where(m => m.Name == name && m.DeclaringType == type).ToList();
        Assert.AreEqual(1, overloads.Count, type.Name + "." + name + " is missing or gained an overload: the patch binding is ambiguous.");
        return overloads[0];
    }

    // The decisions expose these as public readonly fields (Kingdom is inherited as a property); the
    // prefix bodies read them by name, so either shape counts and the type must stay Kingdom.
    private static Type MemberType(Type type, string name) =>
        AccessTools.Field(type, name)?.FieldType ?? AccessTools.Property(type, name)?.PropertyType;

    private static string[] Names(MethodInfo method) => method.GetParameters().Select(p => p.Name).ToArray();

    // The prefix's engine-bound parameters (everything not a Harmony __special) must all exist, by
    // name and type, on the target.
    private static void AssertPrefixBindsToTarget(Type patch, MethodInfo target)
    {
        var prefix = patch.GetMethod("Prefix", BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(prefix, patch.Name + ".Prefix is gone.");
        var targetParameters = target.GetParameters();
        foreach (var parameter in prefix.GetParameters().Where(p => !p.Name.StartsWith("__", StringComparison.Ordinal)))
        {
            var match = targetParameters.FirstOrDefault(t => t.Name == parameter.Name);
            Assert.IsNotNull(match, patch.Name + ".Prefix parameter '" + parameter.Name + "' has no same-named parameter on " + target.Name + ".");
            Assert.AreEqual(parameter.ParameterType, match.ParameterType,
                patch.Name + ".Prefix parameter '" + parameter.Name + "' no longer matches the target's type.");
        }
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void DeclareWarActionApplyInternal_ParameterNames_StillBind()
    {
        RequireGame();

        var method = SingleMethod(Resolve(Ns + "Actions.DeclareWarAction"), "ApplyInternal");

        CollectionAssert.AreEqual(new[] { "faction1", "faction2", "declareWarDetail" }, Names(method),
            "ApplyInternal parameter names changed: the prefix binds by name and would receive nothing.");
        AssertPrefixBindsToTarget(typeof(DeclareWarAction_ApplyInternal_Patch), method);
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void DeclareWarDetail_MembersTheClassifierNames_StillExist()
    {
        RequireGame();

        var detail = Resolve(Ns + "Actions.DeclareWarAction+DeclareWarDetail");
        foreach (var member in new[] { "CausedByPlayerHostility", "CausedByCrimeRatingChange", "CausedByKingdomCreation" })
            Assert.IsTrue(Enum.GetNames(detail).Contains(member),
                "DeclareWarDetail." + member + " is gone: IsCausedByPlayer would fail at JIT time.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void IFactionAndKingdom_DeclarePrefixMembers_StillExist()
    {
        RequireGame();

        var faction = Resolve(Ns + "IFaction");
        Assert.IsNotNull(AccessTools.Property(faction, "IsKingdomFaction"), "IFaction.IsKingdomFaction is gone.");
        Assert.IsNotNull(AccessTools.Property(faction, "StringId"), "IFaction.StringId is gone.");
        Assert.IsNotNull(AccessTools.Property(Resolve(Ns + "Kingdom"), "StringId"), "Kingdom.StringId is gone.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void StartCallToWarAgreement_ParameterNames_StillBind()
    {
        RequireGame();

        var method = SingleMethod(Resolve(Ns + "CampaignBehaviors.AllianceCampaignBehavior"), "StartCallToWarAgreement");

        CollectionAssert.AreEqual(
            new[] { "callingKingdom", "calledKingdom", "kingdomToCallToWarAgainst", "callToWarCost", "isPlayerPaying" },
            Names(method), "StartCallToWarAgreement parameter names changed: the prefix would receive nothing.");
        AssertPrefixBindsToTarget(typeof(AllianceCampaignBehavior_StartCallToWarAgreement_Patch), method);
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void OnCallToWarAgreementProposedByPlayerKingdom_ParameterNames_StillBind()
    {
        RequireGame();

        var method = SingleMethod(Resolve(Ns + "CampaignBehaviors.AllianceCampaignBehavior"), "OnCallToWarAgreementProposedByPlayerKingdom");

        Assert.IsTrue(method.IsPublic && !method.IsStatic,
            "OnCallToWarAgreementProposedByPlayerKingdom stopped being a public instance method: nameof() in the attribute would not compile.");
        CollectionAssert.AreEqual(new[] { "proposedKingdom", "kingdomToCallToWarAgainst" }, Names(method),
            "OnCallToWarAgreementProposedByPlayerKingdom parameter names changed: the prefix would receive nothing.");
        AssertPrefixBindsToTarget(typeof(AllianceCampaignBehavior_OnCallToWarAgreementProposedByPlayerKingdom_Patch), method);
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void ProposeCallToWarAgreementDecision_MembersThePrefixReads_StillExist()
    {
        RequireGame();

        var kingdom = Resolve(Ns + "Kingdom");
        var decision = Resolve(Ns + "Election.ProposeCallToWarAgreementDecision");

        Assert.AreEqual(kingdom, MemberType(decision, "CalledKingdom"),
            "ProposeCallToWarAgreementDecision.CalledKingdom is gone or no longer a Kingdom.");
        Assert.AreEqual(kingdom, MemberType(decision, "KingdomToCallToWarAgainst"),
            "ProposeCallToWarAgreementDecision.KingdomToCallToWarAgainst is gone or no longer a Kingdom.");
        var isAllowed = SingleMethod(decision, "IsAllowed");
        Assert.AreEqual(typeof(bool), isAllowed.ReturnType, "IsAllowed no longer returns bool: the prefix's __result changed type.");
        Assert.AreEqual(0, isAllowed.GetParameters().Length, "IsAllowed gained parameters.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void AcceptCallToWarAgreementDecision_MembersThePrefixReads_StillExist()
    {
        RequireGame();

        var kingdom = Resolve(Ns + "Kingdom");
        var decision = Resolve(Ns + "Election.AcceptCallToWarAgreementDecision");

        // The decision's own Kingdom is the deciding (called) side; a rename would null the read.
        Assert.AreEqual(kingdom, MemberType(decision, "Kingdom"),
            "AcceptCallToWarAgreementDecision.Kingdom is gone or no longer a Kingdom.");
        Assert.AreEqual(kingdom, MemberType(decision, "KingdomToCallToWarAgainst"),
            "AcceptCallToWarAgreementDecision.KingdomToCallToWarAgainst is gone or no longer a Kingdom.");
        var isAllowed = SingleMethod(decision, "IsAllowed");
        Assert.AreEqual(typeof(bool), isAllowed.ReturnType, "IsAllowed no longer returns bool: the prefix's __result changed type.");
        Assert.AreEqual(0, isAllowed.GetParameters().Length, "IsAllowed gained parameters.");
    }

    // BannerlordCoop prefixes all three at the default priority (400), and equal priorities run in the
    // order they were added, which Coop usually wins. On StartCallToWarAgreement, Coop's server branch
    // does the agreement itself and returns false; Harmony 2.4.2 then skips every later bool prefix
    // (MethodCreator.AddPrefixes), so a veto below High never ran and the gold moved before the war was
    // refused (#1343). On DeclareWarAction.ApplyInternal, Coop's server branch sends the war to the
    // clients and returns true, so a veto below High would refuse the war after the clients heard of it.
    // MakePeaceAction's High predates Coop (BannerlordTogether, AllianceActionHook.DeferToHost).
    [TestMethod]
    public void WarVetoPrefixes_RunBeforeCoopsDefaultPriorityPrefixes()
    {
        foreach (var patch in new[]
                 {
                     typeof(DeclareWarAction_ApplyInternal_Patch),
                     typeof(MakePeaceAction_ApplyInternal_Patch),
                     typeof(AllianceCampaignBehavior_StartCallToWarAgreement_Patch),
                 })
        {
            var prefix = patch.GetMethod("Prefix", BindingFlags.Public | BindingFlags.Static);
            Assert.IsNotNull(prefix, patch.Name + ".Prefix is gone.");
            Assert.AreEqual(Priority.High, prefix.GetCustomAttribute<HarmonyPriority>()?.info.priority,
                patch.Name + ".Prefix must be Priority.High so it runs before Coop's prefix on the same method.");
        }
    }
}
