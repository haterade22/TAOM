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
    [TestCategory("RequiresGameIL")]
    [TestCategory("BindingVerification")]
    public void TheFiveLordTextIds_AreStillReadByLiteral_InVanillasLordConversations()
    {
        RequireGame();
        var lordTypes = new[] { typeof(LordConversationsCampaignBehavior) }
            .Concat(typeof(LordConversationsCampaignBehavior).GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Public));
        var literals = new HashSet<string>();
        var farewellLineAdded = false;
        foreach (var method in lordTypes.SelectMany(t => AccessTools.GetDeclaredMethods(t)).Where(m => !m.IsAbstract))
        {
            var strings = Strings(method);
            foreach (var literal in strings)
                literals.Add(literal);

            // AddDialogLineWithVariation("hero_leave", "hero_leave", "close_window", ...): the line id, then the input token, then the output token.
            // "hero_leave" is also an output token of three player lines, so the bare literal would survive a rename of the line itself.
            for (var i = 0; i + 2 < strings.Count; i++)
                farewellLineAdded |= strings[i] == "hero_leave" && strings[i + 1] == "hero_leave" && strings[i + 2] == "close_window";
        }
        Assert.IsTrue(farewellLineAdded, "the hero_leave farewell line (id, input and output token) is no longer added by LordConversationsCampaignBehavior");

        // Vanilla asks for these ids only by literal, so a rename would strand every lord row without any other failure.
        foreach (var id in new[] { "hero_leave", "str_surrender_demand_hero", "str_context_line", "str_surrender_offer", "str_prisoner_released" })
            Assert.IsTrue(literals.Contains(id), id + " is no longer a literal in LordConversationsCampaignBehavior");
    }

    /// <summary>Each liege sentence id and the start of the vanilla text it is added with (the TAOM rows keep that text's variables).</summary>
    private static readonly Dictionary<string, string> LiegeSentenceTexts = new()
    {
        ["1220"] = "{=roIaYqrx}",
        ["1221"] = "{=231s4Fqi}",
        ["1225"] = "{=CtEbTd47}",
        ["player_leave_faction_accepted"] = "{=xCjCHRcS}",
        ["player_leaves_faction"] = "{=ZMbvMK6K}",
        ["player_want_to_end_mercenary_service_response"] = "{=EN9s6oZz}",
        ["lord_ask_exit_service_confirm_final"] = "{=7Qr1yZhJ}",
        ["player_want_to_join_faction_as_mercenary_or_vassal_answer"] = "{=MlTofjrU}",
        ["vassalage_offer_player_has_low_relation"] = "{=r3pGLoKN}",
        ["vassalage_offer_rejected"] = "{=!}{VASSALAGE_REJECTION}",
        ["vassalage_offer_accepted"] = "{=2c7dIRla}",
        ["player_is_accepted_as_a_vassal"] = "{=XqWloWK0}{PLAYER_ACCEPTED_AS_VASSAL}",
        ["1210"] = "{=dT3cdDSg}",
        ["player_want_to_join_faction_as_mercenary_lord_answer_3"] = "{=!}{MERCENARY_HIRING_PITCH}",
        ["mercenary_player_accepts_lord_answer"] = "{=yUYNlQhU}",
        ["mercenary_player_rejects_lord_answer"] = "{=wxK1bTZm}",
    };

    [TestMethod]
    [TestCategory("RequiresGameIL")]
    [TestCategory("BindingVerification")]
    public void TheLiegeSentences_AreStillAddedAsLordLines_WithTheirVanillaText_InInstalledEngine()
    {
        RequireGame();
        CollectionAssert.AreEquivalent(LiegeSentenceTexts.Keys.ToList(), LiegeLines.TextOfSentence.Keys.ToList());

        var lordTypes = new[] { typeof(LordConversationsCampaignBehavior) }
            .Concat(typeof(LordConversationsCampaignBehavior).GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Public));
        var found = new HashSet<string>();
        foreach (var method in lordTypes.SelectMany(t => AccessTools.GetDeclaredMethods(t)).Where(m => !m.IsAbstract))
        {
            var strings = Strings(method);
            // AddDialogLine(id, input token, output token, text, ...): the text is the fourth literal after the id.
            for (var i = 0; i + 3 < strings.Count; i++)
            {
                if (LiegeSentenceTexts.TryGetValue(strings[i], out var text) && strings[i + 3].StartsWith(text, StringComparison.Ordinal))
                    found.Add(strings[i]);
            }
        }

        var missing = LiegeSentenceTexts.Keys.Where(k => !found.Contains(k)).ToList();
        Assert.AreEqual(0, missing.Count, "no longer added with their vanilla text: " + string.Join(", ", missing));
    }

    [TestMethod]
    [TestCategory("RequiresGameIL")]
    [TestCategory("BindingVerification")]
    public void ProcessSentence_StillCallsUpdateCurrentSentenceText_InInstalledEngine()
    {
        var processSentence = Method(typeof(ConversationManager), "ProcessSentence");
        var seam = SeamTarget();

        var calls = PatchProcessor.GetOriginalInstructions(processSentence)
            .Count(ci => (ci.opcode == OpCodes.Call || ci.opcode == OpCodes.Callvirt) && ci.operand is MethodBase called && called.MetadataToken == seam.MetadataToken && called.Module == seam.Module);

        Assert.AreEqual(1, calls, "ProcessSentence no longer calls UpdateCurrentSentenceText: the text seam would never run");
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
