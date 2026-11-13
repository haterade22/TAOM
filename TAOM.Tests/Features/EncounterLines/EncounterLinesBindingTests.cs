using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Localization;
using TAOM.Adapters;
using TAOM.Composition;
using TAOM.Core.Logging;
using TAOM.Features.EncounterLines;
using TAOM.Features.EncounterLines.Hooks;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.EncounterLines;

/// <summary>
/// Pins the vanilla seams Encounter Lines rides (Patch149) against the installed engine, with no campaign: the four targets
/// keep their shape, the private fields bind with the types the adapter and the text seam read them as, the three sentence
/// ids and the text variables are still in vanilla's IL, and the category attaches exactly the patches it declares.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class EncounterLinesBindingTests
{
    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(Microsoft.VisualStudio.TestTools.UnitTesting.TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    private static void RequireGame()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));
    }

    private static MethodInfo Method(Type type, string name)
    {
        RequireGame();
        var method = AccessTools.Method(type, name);
        Assert.IsNotNull(method, type.Name + "." + name + " did not resolve.");
        return method!;
    }

    private static MethodInfo CaravanTarget() => Method(typeof(CaravansCampaignBehavior), "caravan_start_talk_on_condition");
    private static MethodInfo VillagerTarget() => Method(typeof(VillagerCampaignBehavior), "village_farmer_talk_start_on_condition");
    private static MethodInfo RobberyTarget() => Method(typeof(BanditInteractionsCampaignBehavior), "bandit_start_defender_condition");
    private static MethodInfo SeamTarget() => Method(typeof(ConversationManager), "UpdateCurrentSentenceText");

    private static List<string> Strings(MethodBase method) =>
        PatchProcessor.GetOriginalInstructions(method)
            .Where(ci => ci.opcode == OpCodes.Ldstr)
            .Select(ci => (string)ci.operand)
            .ToList();

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void TheThreeConditions_ArePrivateInstanceBools_WithNoParameters_InInstalledEngine()
    {
        foreach (var target in new[] { CaravanTarget(), VillagerTarget(), RobberyTarget() })
        {
            Assert.IsFalse(target.IsStatic, target.Name);
            Assert.IsTrue(target.IsPrivate, target.Name);
            Assert.AreEqual(typeof(bool), target.ReturnType, target.Name);
            Assert.AreEqual(0, target.GetParameters().Length, target.Name);
        }
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void TheSeamTarget_IsAPublicInstanceVoid_WithNoParameters_InInstalledEngine()
    {
        var target = SeamTarget();

        Assert.IsFalse(target.IsStatic);
        Assert.IsTrue(target.IsPublic);
        Assert.IsFalse(target.IsVirtual);
        Assert.AreEqual(typeof(void), target.ReturnType);
        Assert.AreEqual(0, target.GetParameters().Length);
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void ThePatchMethods_DeclareTheNamesHarmonyInjectsBy()
    {
        foreach (var patch in new[] { typeof(CaravanGreetingPatch), typeof(VillagerGreetingPatch) })
        {
            var prefix = patch.GetMethod("Prefix")!.GetParameters();
            Assert.AreEqual("__state", prefix.Single().Name, patch.Name);
            Assert.IsTrue(prefix.Single().IsOut && prefix.Single().ParameterType == typeof(bool).MakeByRefType(), patch.Name);
            CollectionAssert.AreEqual(new[] { "__result", "__state" }, patch.GetMethod("Postfix")!.GetParameters().Select(p => p.Name).ToArray(), patch.Name);
        }
        CollectionAssert.AreEqual(new[] { "__result" }, typeof(RobberyLinesPatch).GetMethod("Postfix")!.GetParameters().Select(p => p.Name).ToArray());
        var seam = typeof(EncounterSentenceTextPatch).GetMethod("Postfix")!.GetParameters().Single();
        Assert.AreEqual("__instance", seam.Name);
        Assert.AreEqual(typeof(ConversationManager), seam.ParameterType);
    }

    [TestMethod]
    [TestCategory("RequiresGameIL")]
    [TestCategory("BindingVerification")]
    public void TheConditions_StillWriteTheVariablesThePostfixesOverwrite_InInstalledEngine()
    {
        Assert.IsTrue(Strings(CaravanTarget()).Contains(EncounterLinesService.CaravanGreetingVariable), "caravan greeting variable");
        Assert.IsTrue(Strings(VillagerTarget()).Contains(EncounterLinesService.VillagerGreetingVariable), "villager greeting variable");
        var robbery = Strings(RobberyTarget());
        foreach (var (variable, textId) in EncounterLinesService.RobberyLines)
        {
            Assert.IsTrue(robbery.Contains(variable), variable);
            Assert.IsTrue(robbery.Contains(textId), textId);
        }
    }

    [TestMethod]
    [TestCategory("RequiresGameIL")]
    [TestCategory("BindingVerification")]
    public void TheThreeSentenceIds_AreStillAddedByVanilla_InInstalledEngine()
    {
        Assert.IsTrue(Strings(Method(typeof(CaravansCampaignBehavior), "AddDialogs")).Contains(EncounterLinesService.CaravanDefySentence), "caravan surrender sentence");
        Assert.IsTrue(Strings(Method(typeof(VillagerCampaignBehavior), "AddVillageFarmerTradeAndLootDialogs")).Contains(EncounterLinesService.VillagerDefySentence), "villager surrender sentence");
        var hideout = AccessTools.TypeByName("SandBox.CampaignBehaviors.HideoutConversationsCampaignBehavior");
        Assert.IsNotNull(hideout, "HideoutConversationsCampaignBehavior is gone");
        Assert.IsTrue(Strings(Method(hideout!, "AddDialogs")).Contains(EncounterLinesService.HideoutBossSentence), "hideout boss sentence");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void TheConversationManagerFields_BindWithTheTypesTheSeamReads()
    {
        RequireGame();
        var sentences = AccessTools.Field(typeof(ConversationManager), "_sentences");
        var current = AccessTools.Field(typeof(ConversationManager), "_currentSentence");
        var text = AccessTools.Field(typeof(ConversationManager), "_currentSentenceText");

        Assert.AreEqual(typeof(List<ConversationSentence>), sentences?.FieldType, "_sentences");
        Assert.AreEqual(typeof(int), current?.FieldType, "_currentSentence");
        Assert.AreEqual(typeof(TextObject), text?.FieldType, "_currentSentenceText");
        Assert.IsNotNull(AccessTools.Property(typeof(ConversationSentence), "Id"), "ConversationSentence.Id");

        var logger = Substitute.For<IModLogger>();
        EncounterSentenceTextPatch.Initialize(new EncounterLinesServiceFactory().Create(), logger);
        logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void TheMeetingRecords_ArePrivateDictionariesKeyedByTheParty_AndTheAdapterBindsThem()
    {
        RequireGame();
        foreach (var (type, name) in new[] { (typeof(CaravansCampaignBehavior), "_interactedCaravans"), (typeof(VillagerCampaignBehavior), "_interactedVillagers") })
        {
            var field = AccessTools.Field(type, name);
            Assert.IsNotNull(field, name + " is gone");
            Assert.IsTrue(field!.IsPrivate, name);
            Assert.IsTrue(typeof(IDictionary).IsAssignableFrom(field.FieldType), name + " is no longer a non-generic dictionary");
            Assert.AreEqual(typeof(MobileParty), field.FieldType.GetGenericArguments()[0], name + " is no longer keyed by the party");
        }

        var logger = Substitute.For<IModLogger>();
        Assert.IsNotNull(EncounterLinesAdapter.Bind<CaravansCampaignBehavior>("_interactedCaravans", logger));
        Assert.IsNotNull(EncounterLinesAdapter.Bind<VillagerCampaignBehavior>("_interactedVillagers", logger));
        logger.DidNotReceive().LogWarning(Arg.Any<string>());
        Assert.IsNull(EncounterLinesAdapter.Bind<CaravansCampaignBehavior>("_noSuchField", logger));
        logger.Received(1).LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void TheHideoutBossClanChain_KeepsTheTypesTheAdapterReads_InInstalledEngine()
    {
        RequireGame();
        Assert.AreEqual(typeof(TaleWorlds.Core.IAgent), AccessTools.Property(typeof(ConversationManager), "OneToOneConversationAgent")?.PropertyType, "ConversationManager.OneToOneConversationAgent");
        Assert.AreEqual(typeof(MobileParty), AccessTools.Property(typeof(ConversationManager), "ConversationParty")?.PropertyType, "ConversationManager.ConversationParty");
        Assert.IsTrue(typeof(TaleWorlds.Core.IAgent).IsAssignableFrom(typeof(TaleWorlds.MountAndBlade.Agent)), "Agent no longer implements IAgent");
        Assert.AreEqual(typeof(TaleWorlds.Core.IAgentOriginBase), AccessTools.Property(typeof(TaleWorlds.MountAndBlade.Agent), "Origin")?.PropertyType, "Agent.Origin");
        Assert.AreEqual(typeof(TaleWorlds.Core.IBattleCombatant), AccessTools.Property(typeof(TaleWorlds.Core.IAgentOriginBase), "BattleCombatant")?.PropertyType, "IAgentOriginBase.BattleCombatant");
        Assert.IsTrue(typeof(TaleWorlds.Core.IBattleCombatant).IsAssignableFrom(typeof(PartyBase)), "PartyBase is no longer an IBattleCombatant");
        Assert.AreEqual(typeof(MobileParty), AccessTools.Property(typeof(PartyBase), "MobileParty")?.PropertyType, "PartyBase.MobileParty");
        Assert.AreEqual(typeof(TaleWorlds.CampaignSystem.Clan), AccessTools.Property(typeof(MobileParty), "ActualClan")?.PropertyType, "MobileParty.ActualClan");
        foreach (var origin in new[] { "PartyAgentOrigin", "PartyGroupAgentOrigin", "SimpleAgentOrigin" })
        {
            var type = AccessTools.TypeByName("TaleWorlds.CampaignSystem.AgentOrigins." + origin);
            Assert.IsNotNull(type, origin + " is gone");
            Assert.IsTrue(typeof(TaleWorlds.Core.IAgentOriginBase).IsAssignableFrom(type), origin);
            Assert.IsNotNull(AccessTools.Property(type, "Party"), origin + ".Party, which its BattleCombatant returns");
        }
    }

    [TestMethod]
    [TestCategory("RequiresGameIL")]
    [TestCategory("BindingVerification")]
    public void PatchCategory_AppliedToTheInstalledEngine_AttachesThePatchesItDeclares_AndTheJitAccepts()
    {
        const string id = "taom.tests.encounterlines.jit";
        var harmony = new Harmony(id);
        try
        {
            PatchCategoryIndex.Build(typeof(EncounterLinesModule).Assembly).Apply(harmony, EncounterLinesModule.PatchCategory);

            foreach (var (target, prefixes) in new[] { (CaravanTarget(), 1), (VillagerTarget(), 1), (RobberyTarget(), 0), (SeamTarget(), 0) })
            {
                var info = Harmony.GetPatchInfo(target);
                Assert.IsNotNull(info, target.Name + " was not patched: the class is not in the category");
                Assert.AreEqual(1, info!.Postfixes.Count(p => p.owner == id), target.Name + " postfix count");
                Assert.AreEqual(prefixes, info.Prefixes.Count(p => p.owner == id), target.Name + " prefix count");
                Assert.AreEqual(0, info.Transpilers.Count(p => p.owner == id), target.Name + " transpiler count");
                Assert.AreEqual(0, info.Finalizers.Count(p => p.owner == id), target.Name + " finalizer count");
                RuntimeHelpers.PrepareMethod(target.MethodHandle);
            }
        }
        finally
        {
            harmony.UnpatchAll(id);
        }
    }

    /// <summary>A service with every collaborator substituted, for the binding checks that only need an instance.</summary>
    private sealed class EncounterLinesServiceFactory
    {
        internal EncounterLinesService Create() => new(
            Substitute.For<IEncounterLinesSettingsProvider>(),
            Substitute.For<IEncounterLinesAdapter>(),
            Substitute.For<IEncounterGroupsConfigProvider>(),
            Substitute.For<TAOM.Features.Execution.IAlignmentService>(),
            Substitute.For<TAOM.Features.InitialChildGeneration.IRandomSource>(),
            Substitute.For<TAOM.Features.CoopInterop.IDedicatedServerProvider>(),
            Substitute.For<TAOM.Features.Messengers.IMessengerConversationState>());
    }
}
