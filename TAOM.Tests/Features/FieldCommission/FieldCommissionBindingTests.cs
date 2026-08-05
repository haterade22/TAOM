using System.Linq;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.FieldCommission;

/// <summary>
/// Drift-guards for every 1.4.7-verified TaleWorlds signature Battlefield Promotions depends on.
/// Most load-bearing: <c>TextInquiryData</c>'s ctor — bug fix (b) exists BECAUSE the donor mod got
/// this exact parameter order wrong (troop name landed in <c>soundEventPath</c> instead of
/// <c>defaultInputText</c>). A silent TaleWorlds rename here would either fail to bind (loud) or —
/// worse — bind to a DIFFERENT overload with a different parameter at position 11/12 (quiet).
/// </summary>
[TestClass]
public class FieldCommissionBindingTests
{
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
    public void TextInquiryData_CtorBindingResolves_WithVerifiedParameterOrder()
    {
        RequireGame();
        var type = AccessTools.TypeByName("TaleWorlds.Library.TextInquiryData");
        Assert.IsNotNull(type, "TextInquiryData did not resolve — the rename prompt adapter has no target.");

        var ctor = type.GetConstructors().SingleOrDefault(c => c.GetParameters().Length == 12);
        Assert.IsNotNull(ctor, "TextInquiryData's 12-parameter ctor did not resolve — arity drifted.");

        var p = ctor.GetParameters();
        Assert.AreEqual("String", p[0].ParameterType.Name, "position 0 (titleText) drifted.");
        Assert.AreEqual("String", p[1].ParameterType.Name, "position 1 (text) drifted.");
        Assert.AreEqual("Boolean", p[2].ParameterType.Name, "position 2 (affirmativeShown) drifted.");
        Assert.AreEqual("Boolean", p[3].ParameterType.Name, "position 3 (negativeShown) drifted.");
        Assert.AreEqual("String", p[4].ParameterType.Name, "position 4 (affirmativeText) drifted.");
        Assert.AreEqual("String", p[5].ParameterType.Name, "position 5 (negativeText) drifted.");
        Assert.AreEqual("Action`1", p[6].ParameterType.Name, "position 6 (affirmativeAction: Action<string>) drifted.");
        Assert.AreEqual("Action", p[7].ParameterType.Name, "position 7 (negativeAction) drifted.");
        Assert.AreEqual("Boolean", p[8].ParameterType.Name, "position 8 (shouldInputBeObfuscated) drifted.");
        Assert.AreEqual("String", p[10].ParameterType.Name, "position 10 (soundEventPath) drifted — this is the slot the donor mod wrongly used for the troop name.");
        Assert.AreEqual("String", p[11].ParameterType.Name, "position 11 (defaultInputText) drifted — this MUST carry the troop name, not position 10.");
        Assert.IsTrue(p[10].HasDefaultValue && p[11].HasDefaultValue, "soundEventPath/defaultInputText must both be optional — a required-param drift would break every existing call site silently at the wrong position.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void InquiryData_CtorBindingResolves_WithEightRequiredParameters()
    {
        RequireGame();
        var type = AccessTools.TypeByName("TaleWorlds.Library.InquiryData");
        Assert.IsNotNull(type, "InquiryData did not resolve.");

        var ctor = type.GetConstructors().FirstOrDefault(c => c.GetParameters().Length >= 8);
        Assert.IsNotNull(ctor, "InquiryData's ctor did not resolve with at least 8 parameters.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void InformationManager_ShowInquiryAndShowTextInquiry_BindingResolves()
    {
        RequireGame();
        var type = AccessTools.TypeByName("TaleWorlds.Library.InformationManager");
        Assert.IsNotNull(type, "InformationManager did not resolve.");

        Assert.IsNotNull(AccessTools.Method(type, "ShowInquiry"), "ShowInquiry did not resolve.");
        Assert.IsNotNull(AccessTools.Method(type, "ShowTextInquiry"), "ShowTextInquiry did not resolve.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void DefaultClanTierModel_GetCompanionLimit_BindingResolvesWithClanParameter()
    {
        RequireGame();
        var type = AccessTools.TypeByName("TaleWorlds.CampaignSystem.GameComponents.DefaultClanTierModel");
        Assert.IsNotNull(type, "DefaultClanTierModel did not resolve.");

        var method = AccessTools.Method(type, "GetCompanionLimit");
        Assert.IsNotNull(method, "GetCompanionLimit did not resolve — companion-room checks would be blind.");
        Assert.AreEqual(1, method.GetParameters().Length);
        Assert.AreEqual("Clan", method.GetParameters()[0].ParameterType.Name);
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void AddCompanionAction_Apply_BindingResolvesWithClanAndHero()
    {
        RequireGame();
        var type = AccessTools.TypeByName("TaleWorlds.CampaignSystem.Actions.AddCompanionAction");
        Assert.IsNotNull(type, "AddCompanionAction did not resolve — bug fix (e) has no engine action to call.");

        var method = AccessTools.Method(type, "Apply");
        Assert.IsNotNull(method, "AddCompanionAction.Apply did not resolve.");
        Assert.AreEqual(2, method.GetParameters().Length);
        Assert.AreEqual("Clan", method.GetParameters()[0].ParameterType.Name);
        Assert.AreEqual("Hero", method.GetParameters()[1].ParameterType.Name);
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void AddHeroToPartyAction_Apply_BindingResolves()
    {
        RequireGame();
        var type = AccessTools.TypeByName("TaleWorlds.CampaignSystem.Actions.AddHeroToPartyAction");
        Assert.IsNotNull(type, "AddHeroToPartyAction did not resolve.");
        Assert.IsNotNull(AccessTools.Method(type, "Apply"), "AddHeroToPartyAction.Apply did not resolve.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void HeroCreator_CreateSpecialHero_BindingResolvesWithFiveParameters()
    {
        RequireGame();
        var type = AccessTools.TypeByName("TaleWorlds.CampaignSystem.HeroCreator");
        Assert.IsNotNull(type, "HeroCreator did not resolve.");

        var method = AccessTools.Method(type, "CreateSpecialHero");
        Assert.IsNotNull(method, "CreateSpecialHero did not resolve — companion creation has no engine factory to call.");
        Assert.AreEqual(5, method.GetParameters().Length, "CreateSpecialHero arity drifted.");
    }

    [DataTestMethod]
    [TestCategory("BindingVerification")]
    [DataRow("SetInitialLevel", 1)]
    [DataRow("SetInitialSkillLevel", 2)]
    [DataRow("AddFocus", 3)]
    [DataRow("AddAttribute", 3)]
    public void HeroDeveloper_VerifiedMethods_BindingResolves(string methodName, int minArity)
    {
        RequireGame();
        var type = AccessTools.TypeByName("TaleWorlds.CampaignSystem.CharacterDevelopment.HeroDeveloper");
        Assert.IsNotNull(type, "HeroDeveloper did not resolve.");

        var method = AccessTools.Method(type, methodName);
        Assert.IsNotNull(method, $"HeroDeveloper.{methodName} did not resolve — the skill-plan adapter has no target.");
        Assert.IsTrue(method.GetParameters().Length >= minArity, $"HeroDeveloper.{methodName} arity drifted below {minArity}.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void Hero_LevelField_BindingResolvesAsPublicIntField()
    {
        RequireGame();
        var type = AccessTools.TypeByName("TaleWorlds.CampaignSystem.Hero");
        Assert.IsNotNull(type, "Hero did not resolve.");

        var field = AccessTools.Field(type, "Level");
        Assert.IsNotNull(field, "Hero.Level did not resolve as a field — CommissionSkillBudget's hero-level = troop-level rule has no target.");
        Assert.AreEqual("Int32", field.FieldType.Name, "Hero.Level's type drifted.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void MapEventSide_GetTotalHealthyTroopCountOfSide_BindingResolves()
    {
        RequireGame();
        var type = AccessTools.TypeByName("TaleWorlds.CampaignSystem.MapEvents.MapEventSide");
        Assert.IsNotNull(type, "MapEventSide did not resolve.");
        Assert.IsNotNull(
            AccessTools.Method(type, "GetTotalHealthyTroopCountOfSide"),
            "GetTotalHealthyTroopCountOfSide did not resolve — the fair-fight ratio has no enemy-side count.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void MissionBehavior_OnAgentRemoved_BindingResolvesWithFourParameters()
    {
        RequireGame();
        var type = AccessTools.TypeByName("TaleWorlds.MountAndBlade.MissionBehavior");
        Assert.IsNotNull(type, "MissionBehavior did not resolve.");

        var method = AccessTools.Method(type, "OnAgentRemoved", new[]
        {
            AccessTools.TypeByName("TaleWorlds.MountAndBlade.Agent"),
            AccessTools.TypeByName("TaleWorlds.MountAndBlade.Agent"),
            AccessTools.TypeByName("TaleWorlds.Core.AgentState"),
            AccessTools.TypeByName("TaleWorlds.MountAndBlade.KillingBlow"),
        });
        Assert.IsNotNull(method, "MissionBehavior.OnAgentRemoved(Agent,Agent,AgentState,KillingBlow) did not resolve — kill tracking has no override target.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void MissionLogic_DerivesFromMissionBehavior_NotTheOtherWayAround()
    {
        RequireGame();
        var missionLogic = AccessTools.TypeByName("TaleWorlds.MountAndBlade.MissionLogic");
        var missionBehavior = AccessTools.TypeByName("TaleWorlds.MountAndBlade.MissionBehavior");
        Assert.IsNotNull(missionLogic);
        Assert.IsNotNull(missionBehavior);
        Assert.IsTrue(missionBehavior.IsAssignableFrom(missionLogic), "MissionLogic must derive from MissionBehavior — the regression rule this feature follows.");
    }
}
