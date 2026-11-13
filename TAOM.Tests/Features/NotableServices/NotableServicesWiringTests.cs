using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Composition;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features;
using TAOM.Features.CoopInterop;
using TAOM.Features.Execution;
using TAOM.Features.Messengers;
using TAOM.Features.NotableServices;
using TAOM.Features.Relations;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.NotableServices;

/// <summary>
/// Wiring guards. The feature has no save data, no patch and no model: one dialog behavior built with <c>new</c> for every
/// campaign, registered through its module and nowhere else. The engine calls the behavior's members and the screen's
/// delegates, so the shape rules are pinned on the comment-stripped sources.
/// </summary>
[TestClass]
public class NotableServicesWiringTests
{
    private const string Folder = "Main/Features/NotableServices";

    private static string Source(string relative) => RepoPaths.ReadSource(relative, stripComments: true);

    // One member's text: from its name to the blank line before the next 4-space-indented member.
    private static string Member(string src, string name)
    {
        var start = src.IndexOf(name, StringComparison.Ordinal);
        Assert.IsTrue(start >= 0, name + " is missing");
        var rest = src.Substring(start);
        var end = Regex.Match(rest, @"\n\s*\n    (?=\S)");
        return end.Success ? rest.Substring(0, end.Index) : rest;
    }

    [TestMethod]
    public void FeatureModules_ListTheModuleOnce()
    {
        Assert.AreEqual(1, FeatureModules.All.OfType<NotableServicesModule>().Count());
        Assert.IsTrue(Source("Main/Composition/FeatureModules.cs").Contains("new Features.NotableServices.NotableServicesModule()"));
    }

    [TestMethod]
    public void TheSingleOwnerFiles_NameNothingOfTheFeature()
    {
        foreach (var file in new[] { "Main/IoC.cs", "Main/SubModule.cs" })
            Assert.IsFalse(Source(file).Contains("NotableServices"), file + " wires the feature by hand AND through its module");
    }

    [TestMethod]
    public void Module_OwnsNoSaveData_PatchesNothing_DeclaresOneBehavior()
    {
        var module = new NotableServicesModule();

        Assert.AreEqual("NotableServices", module.Id);
        Assert.IsNull(module.ParkedReason);
        Assert.IsFalse(module.OwnsSaveData);
        Assert.AreEqual(0, module.PatchCategories.Count);
        Assert.AreEqual(0, module.GameModels.Count);
        Assert.AreEqual(0, module.MissionBehaviors.Count);
        Assert.AreEqual(1, module.CampaignBehaviors.Count);
    }

    [TestMethod]
    public void Module_BuildsTheBehaviorWithNew_NeverFromTheContainer()
    {
        var src = Source(Folder + "/NotableServicesModule.cs");

        Assert.IsTrue(src.Contains("new NotableServicesDialogBehavior("));
        Assert.IsFalse(src.Contains("Resolve<NotableServicesDialogBehavior>"));
    }

    [TestMethod]
    public void TheMapRevealAdapter_IsRegisteredByHardQuestionsOnly_NeverByThisFeature()
    {
        Assert.IsFalse(Source(Folder + "/NotableServicesIoC.cs").Contains("MapRevealAdapter"));

        var registrations = RepoPaths.CSharpFiles("Main")
            .Count(f => Regex.IsMatch(RepoPaths.StripComments(RepoPaths.Text(f)), @"Register<IMapRevealAdapter,\s*MapRevealAdapter>"));
        Assert.AreEqual(1, registrations, "one registration of the shared reveal adapter in the whole mod");
    }

    // The container holds exactly one IMapRevealAdapter, as in the game (HardQuestionsIoC registers it): the service resolves.
    [TestMethod]
    public void TheServiceGraph_ResolvesAgainstASingleMapRevealRegistration()
    {
        using var container = new Container();
        var paths = Substitute.For<IPathService>();
        paths.ModuleDataPath.Returns(Path.Combine(Path.GetTempPath(), "taom-ns-wiring-" + Guid.NewGuid().ToString("N")));
        container.RegisterInstance(paths);
        container.RegisterInstance(Substitute.For<IModLogger>());
        container.RegisterInstance(Substitute.For<ICoopSessionProvider>());
        container.RegisterInstance(Substitute.For<IDedicatedServerProvider>());
        container.RegisterInstance(Substitute.For<IAlignmentService>());
        container.RegisterInstance(Substitute.For<IRelationGrantService>());
        container.RegisterInstance(Substitute.For<IMessengerConversationState>());
        container.RegisterInstance(Substitute.For<IMapRevealAdapter>());
        container.RegisterInstance(Substitute.For<IHeroSkillXpAdapter>());
        var module = new NotableServicesModule();

        module.RegisterServices(container);

        Assert.AreEqual(1, container.GetServiceRegistrations().Count(r => r.ServiceType == typeof(IMapRevealAdapter)));
        Assert.AreSame(container.Resolve<NotableServicesService>(), container.Resolve<NotableServicesService>());
        Assert.IsInstanceOfType(container.Resolve<INotableServicesAdapter>(), typeof(NotableServicesAdapter));
        Assert.IsInstanceOfType(container.Resolve<INotableServicesConfigProvider>(), typeof(NotableServicesConfigProvider));
    }

    [TestMethod]
    public void TheService_HasNoInterface_ItHasOneImplementationAndNoFake()
    {
        Assert.IsNull(typeof(NotableServicesService).Assembly.GetType("TAOM.Features.NotableServices.INotableServicesService"));
        Assert.IsFalse(File.Exists(RepoPaths.RepoPath("Main", "Features", "NotableServices", "INotableServicesService.cs")));
        Assert.IsFalse(Source(Folder + "/NotableServicesIoC.cs").Contains("INotableServicesService"));
    }

    [TestMethod]
    public void TheSettingsProvider_HasOnePublicParameterlessConstructor_AndNoTestOnlyOne()
    {
        var ctors = typeof(NotableServicesSettingsProvider).GetConstructors(
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);

        Assert.AreEqual(1, ctors.Length);
        Assert.IsTrue(ctors[0].IsPublic);
        Assert.AreEqual(0, ctors[0].GetParameters().Length);
    }

    [TestMethod]
    public void TheSwitch_IsListedAsPlayerLocal_InTheCoopRelevanceTable()
    {
        var src = Source("Main/Features/CoopInterop/CoopSettingsRelevance.cs");
        var start = src.IndexOf("PlayerLocal =", StringComparison.Ordinal);
        var block = src.Substring(start, src.IndexOf("};", start, StringComparison.Ordinal) - start);

        StringAssert.Contains(block, "\"EnableNotableServices\"");
        Assert.IsFalse(CoopSettingsRelevance.IsSimulationRelevant(typeof(TaomSettings).GetProperty("EnableNotableServices")!));
    }

    [TestMethod]
    public void TheFeature_NeverTouchesHarmony_OrSaveData()
    {
        foreach (var file in Directory.GetFiles(RepoPaths.RepoPath("Main", "Features", "NotableServices"), "*.cs", SearchOption.AllDirectories))
        {
            var src = RepoPaths.StripComments(File.ReadAllText(file));
            Assert.IsFalse(src.Contains("HarmonyLib"), file);
            Assert.IsFalse(src.Contains("[HarmonyPatch"), file);
            Assert.IsFalse(src.Contains("dataStore.SyncData"), file);
        }
    }

    [TestMethod]
    public void EveryEntryPointFile_StaysUnder150Lines()
    {
        var files = Directory.GetFiles(RepoPaths.RepoPath("Main", "Features", "NotableServices", "Hooks"), "*.cs");

        Assert.AreEqual(3, files.Length, "the texts, the lines and the behavior");
        foreach (var file in files)
            Assert.IsTrue(File.ReadAllLines(file).Length < 150, "ADR-002: " + Path.GetFileName(file));
    }

    [TestMethod]
    public void TheBehavior_SubscribesTheSessionLaunchAndTheConversationEnd_AndRunsEveryMemberBehindTheFaultLog()
    {
        var src = Source(Folder + "/Hooks/NotableServicesDialogBehavior.cs");

        StringAssert.Contains(src, "CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched)");
        StringAssert.Contains(src, "CampaignEvents.ConversationEnded.AddNonSerializedListener(this, OnConversationEnded)");
        StringAssert.Contains(src, "new ScreenFaultLog(logger, \"NotableServices\")");
        foreach (var site in new[] { "\"session launched\"", "\"gift condition\"", "\"gift consequence\"", "\"introduce condition\"",
                     "\"introduce clickable condition\"", "\"introduce consequence\"", "\"reveal condition\"",
                     "\"reveal clickable condition\"", "\"reveal consequence\"" })
            StringAssert.Contains(src, site);
        Assert.IsTrue(Regex.IsMatch(src, @"void OnConversationEnded\([^)]*\)\s*\{\s*_introduced = false;\s*_outcome = RevealOutcome\.Unavailable;"));
    }

    [TestMethod]
    public void DialogLines_SitOnHeroMainOptionsAtPriority111_WithLocalizedText_AndClickableConditionsOnThePricedLines()
    {
        var src = Source(Folder + "/Hooks/NotableServicesDialogLines.cs");

        Assert.AreEqual(3, Regex.Matches(src, "AddPlayerLine\\(\"taom_ns_\\w+\", \"hero_main_options\"").Count);
        StringAssert.Contains(src, "Priority = 111");
        StringAssert.Contains(src, "{=taom_ns_");
        StringAssert.Contains(src, "b.IntroduceClickable(out why)");
        StringAssert.Contains(src, "b.RevealClickable(out why)");
        Assert.IsFalse(src.Contains("\"lord_start\""), "no line rewrites a vanilla state");
    }

    [TestMethod]
    public void TheConditions_MakeNoWrite_InTheServiceOrTheBehavior()
    {
        var service = Source(Folder + "/NotableServicesService.cs");
        var behavior = Source(Folder + "/Hooks/NotableServicesDialogBehavior.cs");
        var writes = new[] { "PayNotable(", "MarkMet(", "Track(", "Grant(", "RunAfterConversation(", "OpenGiftScreen(", "AddMilitia(", "ShowIntroducedNotice(" };

        foreach (var name in new[] { "public bool OffersGift()", "public IntroduceQuote? QuoteIntroduce()", "public bool OffersReveal()",
                     "public bool CanAffordReveal()", "private NotableSnapshot? Speaker(" })
            foreach (var write in writes)
                Assert.IsFalse(Member(service, name).Contains(write), name + " calls " + write);

        foreach (var name in new[] { "internal bool CanGift()", "internal bool CanIntroduce()", "internal bool IntroduceClickable(",
                     "internal bool CanReveal()", "internal bool RevealClickable(", "internal bool Introduced()", "internal bool Answered(" })
        {
            var body = Member(behavior, name);
            Assert.IsFalse(Regex.IsMatch(body, @"_service\.(Introduce|Reveal|QueueGift)\("), name + " writes");
        }
    }

    [TestMethod]
    public void TheGate_IsTheSettlementServicesSinglePlayerPredicate()
    {
        var service = Source(Folder + "/NotableServicesService.cs");

        StringAssert.Contains(service, "CoopSessionPolicy.IsSinglePlayerAuthority(_coop, _server)");
        Assert.IsFalse(service.Contains("IsSessionActive"), "the four-term form lives in CoopSessionPolicy only");
        StringAssert.Contains(service, "_messenger.IsOpen");
    }

    [TestMethod]
    public void TheMapReveal_TheByIdPair_KeepsTheTrackedGuard_AndMarksFortificationsOnly()
    {
        var contract = Source("Main/Adapters/IMapRevealAdapter.cs");
        var adapter = Source("Main/Adapters/MapRevealAdapter.cs");

        StringAssert.Contains(contract, "void RevealClan(string clanId);");
        StringAssert.Contains(contract, "void RevealKingdom(string kingdomId);");
        StringAssert.Contains(contract, "IReadOnlyList<RevealCandidate> FortificationsAround(string settlementId);");
        StringAssert.Contains(contract, "int Track(IReadOnlyList<string> settlementIds);");
        Assert.AreEqual(1, Regex.Matches(adapter, @"tracker\.RegisterObject\(").Count, "one marking site, behind the guard, for the clan, the kingdom and the by-id pair");
        StringAssert.Contains(adapter, "settlement.IsFortification && !tracker.CheckTracked(settlement)");
        Assert.IsFalse(adapter.Contains("HasVisited"));
    }

    [TestMethod]
    public void TheFortificationWalk_VisitsOnlyTheFiefs_AndSkipsTheOrigin()
    {
        var body = Member(Source("Main/Adapters/MapRevealAdapter.cs"), "public IReadOnlyList<RevealCandidate> FortificationsAround");

        StringAssert.Contains(body, "foreach (var fief in Town.AllFiefs)");
        StringAssert.Contains(body, "fief.Settlement");
        StringAssert.Contains(body, "settlement == origin");
        Assert.IsFalse(body.Contains("Settlement.All"), "the walk covers fiefs, not every settlement on the map");
    }

    [TestMethod]
    public void TheNotices_UseVanillasRelationTexts_AndTheSettlementNameFromTheEngine()
    {
        var adapter = Source("Main/Adapters/NotableServicesAdapter.cs");
        var relation = Member(adapter, "public void ShowRelationNotice");

        StringAssert.Contains(relation, "str_your_relation_increased_with_notable");
        StringAssert.Contains(relation, "str_your_relation_decreased_with_notable");
        StringAssert.Contains(relation, "StringHelpers.SetCharacterProperties(\"HERO\"");
        StringAssert.Contains(relation, "\"VALUE\"");
        StringAssert.Contains(relation, "\"MAGNITUDE\"");
        StringAssert.Contains(relation, "change == 0");
        StringAssert.Contains(relation, "LogError");
        StringAssert.Contains(Member(adapter, "public void ShowGiftNotice"), "Settlement.Find(settlementId)?.Name");
        Assert.IsFalse(adapter.Contains("taom_ns_relation_notice"), "vanilla's text replaces the TAOM key");
    }

    [TestMethod]
    public void TheRelationNoticeKey_IsGoneFromEveryStringFile()
    {
        var moduleData = RepoPaths.RepoPath("Main", "_Module", "ModuleData");
        var files = Directory.GetFiles(moduleData, "taom_module_strings.xml")
            .Concat(Directory.GetFiles(Path.Combine(moduleData, "Languages"), "std_taom_module_strings_*.xml", SearchOption.AllDirectories)).ToList();
        Assert.AreEqual(13, files.Count);
        foreach (var file in files)
            Assert.IsFalse(File.ReadAllText(file).Contains("taom_ns_relation_notice"), file);
    }

    [TestMethod]
    public void TheSnapshot_CarriesNoNames_SoNoConditionRendersOne()
    {
        var snapshot = typeof(TAOM.Features.NotableServices.Domain.NotableSnapshot);

        Assert.IsNull(snapshot.GetProperty("Name"));
        Assert.IsNull(snapshot.GetProperty("SettlementName"));
    }

    [TestMethod]
    public void TheService_HoldsTheLimitsOnce_TheProviderOwnsTheRanges_AndDecision4IsWrittenDirectly()
    {
        var service = Source(Folder + "/NotableServicesService.cs");

        Assert.IsFalse(Regex.IsMatch(service, @"Math\.Max\(1,\s*Math\.Min\(12"), "revealCount is clamped by the provider");
        Assert.IsFalse(Regex.IsMatch(service, @"Math\.Max\(1,\s*Whole\("), "maxMenPerGift is clamped by the provider");
        StringAssert.Contains(service, "settlement == FactionSide.Free && prisoner == FactionSide.Evil");
        Assert.IsFalse(service.Contains("AreOpposed"));
    }

    [TestMethod]
    public void ThePricedLines_ReadTheSameVariablesTheBehaviorSets()
    {
        var behavior = Source(Folder + "/Hooks/NotableServicesDialogBehavior.cs");
        var lines = Source(Folder + "/Hooks/NotableServicesDialogLines.cs");

        foreach (var name in new[] { "TAOM_NS_INTRODUCE_LINE", "TAOM_NS_REVEAL_LINE" })
        {
            StringAssert.Contains(behavior, "MBTextManager.SetTextVariable(\"" + name + "\"");
            StringAssert.Contains(lines, "\"{=!}{" + name + "}\"");
        }
    }

    [TestMethod]
    public void TheNotableAdapter_PaysTheSpeakerAPositiveAmount_AndMarksMetNotKnown()
    {
        var adapter = Source("Main/Adapters/NotableServicesAdapter.cs");

        StringAssert.Contains(adapter, "GiveGoldAction.ApplyBetweenCharacters(player, notable, price);");
        StringAssert.Contains(adapter, "price <= 0");
        StringAssert.Contains(adapter, ".SetHasMet()");
        Assert.IsFalse(adapter.Contains("IsKnownToPlayer"), "vanilla's menus already set the known flag");
    }

    [TestMethod]
    public void TheGiftScreen_IsBuiltFromTheBasicInitData_NotTheLootScreen_AndRefusesTalking()
    {
        var adapter = Source("Main/Adapters/NotableServicesAdapter.cs");

        StringAssert.Contains(adapter, "PartyScreenLogicInitializationData.CreateBasicInitDataWithMainParty(");
        Assert.IsFalse(adapter.Contains("OpenScreenAsLoot"), "the loot screen lets heroes of other clans move");
        StringAssert.Contains(adapter, "TroopRoster.CreateDummyTroopRoster()");
        StringAssert.Contains(adapter, "state.IsDonating = false;");
        // PartyVM.cs:2059 hides both prisoner panels in TroopsManage and QuestTroopManage, while the D hotkey still moves prisoners.
        StringAssert.Contains(adapter, "state.PartyScreenMode = PartyScreenHelper.PartyScreenMode.Shared;");
        Assert.IsFalse(adapter.Contains("PartyScreenMode.TroopsManage"), "TroopsManage hides the prisoner panels");
        Assert.IsFalse(adapter.Contains("PartyScreenMode.QuestTroopManage"), "QuestTroopManage hides the prisoner panels");
        StringAssert.Contains(adapter, "isTroopUpgradesDisabled: true");
        StringAssert.Contains(adapter, "partyPresentationDoneButtonDelegate:");
        StringAssert.Contains(adapter, "partyPresentationDoneButtonConditionDelegate:");
        StringAssert.Contains(adapter, "data.CanTalkToTroopDelegate = RefuseTalking;");
        Assert.IsFalse(adapter.Contains("partyScreenClosedDelegate"), "the commit, not the closed event, is where the gift settles");
        Assert.IsFalse(adapter.Contains("PartyScreenCommit"), "no conversation was opened from this screen");
    }

    [TestMethod]
    public void ThePartyScreenRead_LooksForAnyPartyState_WithoutACatchThatWouldFailOpen()
    {
        var adapter = Source("Main/Adapters/NotableServicesAdapter.cs");
        var body = Member(adapter, "public bool PartyScreenIsOpen");

        StringAssert.Contains(body, "public bool PartyScreenIsOpen => GameStateManager.Current?.LastOrDefault<PartyState>() != null;");
        Assert.IsFalse(Regex.IsMatch(body.Split(';')[0], @"(try|catch)"), "a catch answering false would show the gift line");
    }

    [TestMethod]
    public void TheGiftDoneStep_IsGuardedAndLogsAFault()
    {
        var adapter = Source("Main/Adapters/NotableServicesAdapter.cs");
        var body = Member(adapter, "private bool Done(");

        StringAssert.Contains(body, "try");
        StringAssert.Contains(body, "catch (Exception ex)");
        StringAssert.Contains(body, "LogError");
        StringAssert.Contains(body, "return true;");
    }

    [TestMethod]
    public void TheRogueryNotice_BindsTheLocalizedSkillName_InTheCodeAndEveryLanguageFile()
    {
        const string text = "Dealing in prisoners teaches you something of the trade (+{XP} {SKILL_NAME} experience).";
        var adapter = RepoPaths.ReadSource("Main/Adapters/NotableServicesAdapter.cs");

        StringAssert.Contains(adapter, "{=taom_ns_roguery_notice}" + text);
        StringAssert.Contains(adapter, "SetTextVariable(\"SKILL_NAME\", DefaultSkills.Roguery.Name)");

        var moduleData = RepoPaths.RepoPath("Main", "_Module", "ModuleData");
        var files = Directory.GetFiles(moduleData, "taom_module_strings.xml")
            .Concat(Directory.GetFiles(Path.Combine(moduleData, "Languages"), "std_taom_module_strings_*.xml", SearchOption.AllDirectories)).ToList();
        Assert.AreEqual(13, files.Count);
        foreach (var file in files)
        {
            var xml = File.ReadAllText(file);
            var rows = Regex.Matches(xml, "id=\"taom_ns_roguery_notice\" text=\"(?:\\{=taom_ns_roguery_notice\\})?([^\"]*)\"");
            Assert.AreEqual(1, rows.Count, file + ": one row");
            var row = System.Net.WebUtility.HtmlDecode(rows[0].Groups[1].Value);
            if (Path.GetFileName(file) == "taom_module_strings.xml")
                Assert.AreEqual(text, row, file);
            else
            {
                StringAssert.Contains(row, "{XP}", file);
                StringAssert.Contains(row, "{SKILL_NAME}", file);
            }
            Assert.IsFalse(xml.Contains("+{XP} Roguery experience"), file);
        }
    }

    [TestMethod]
    public void TheWrites_GoThroughTheEnginesOwnSetters()
    {
        var adapter = Source("Main/Adapters/NotableServicesAdapter.cs");

        StringAssert.Contains(adapter, "settlement.Militia += amount;");
        StringAssert.Contains(adapter, "village.Hearth += amount;");
        StringAssert.Contains(adapter, "town.Prosperity += amount;");
    }

    [TestMethod]
    public void TaomSettings_DeclaresTheToggleInItsOwnGroupDefaultingOn()
    {
        var src = Source("Main/Features/TaomSettings.cs");

        StringAssert.Contains(src, "[SettingPropertyGroup(\"World/Notable Services\", GroupOrder = 77)]");
        StringAssert.Contains(src, "public bool EnableNotableServices { get; set; } = true;");
        Assert.AreEqual(1, Regex.Matches(src, "GroupOrder = 77\\b").Count, "GroupOrder 77 is used by another group");
        StringAssert.Contains(src, "[SettingPropertyBool(\"Enable Notable Services\", Order = 0, RequireRestart = false,");
    }

    [TestCategory("RequiresGame")]
    [TestMethod]
    public void TheBehaviorDeclaration_CreatesTheBehaviorFromTheContainer()
    {
        using var container = new Container();
        var paths = Substitute.For<IPathService>();
        paths.ModuleDataPath.Returns(Path.Combine(Path.GetTempPath(), "taom-ns-decl-" + Guid.NewGuid().ToString("N")));
        container.RegisterInstance(paths);
        container.RegisterInstance(Substitute.For<IModLogger>());
        container.RegisterInstance(Substitute.For<ICoopSessionProvider>());
        container.RegisterInstance(Substitute.For<IDedicatedServerProvider>());
        container.RegisterInstance(Substitute.For<IAlignmentService>());
        container.RegisterInstance(Substitute.For<IRelationGrantService>());
        container.RegisterInstance(Substitute.For<IMessengerConversationState>());
        container.RegisterInstance(Substitute.For<IMapRevealAdapter>());
        container.RegisterInstance(Substitute.For<IHeroSkillXpAdapter>());
        var module = new NotableServicesModule();
        module.RegisterServices(container);

        foreach (var decl in module.CampaignBehaviors)
            Assert.IsInstanceOfType(decl.Create(container), typeof(TAOM.Features.NotableServices.Hooks.NotableServicesDialogBehavior));
    }

    [TestMethod]
    public void SettingsProvider_FallsBackToOnWithoutMcm()
    {
        Assert.IsTrue(new NotableServicesSettingsProvider().Enabled);
        Assert.IsTrue(NotableServicesSettingsProvider.EnabledFrom(null));
        Assert.IsFalse(NotableServicesSettingsProvider.EnabledFrom(new TaomSettings { EnableNotableServices = false }));
    }
}
