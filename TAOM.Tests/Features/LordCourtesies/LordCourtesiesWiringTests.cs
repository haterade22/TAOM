using System.IO;
using System.Linq;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Composition;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.BondsOfBattle;
using TAOM.Features.CoopInterop;
using TAOM.Features.Enlistment;
using TAOM.Features.Execution;
using TAOM.Features.InitialChildGeneration;
using TAOM.Features.Relations;
using TAOM.Features.LordCourtesies;
using TAOM.Features.Messengers;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.LordCourtesies;

/// <summary>
/// Wiring guards. The feature has no save data, no patch and no model: one dialog behavior built with
/// <c>new</c> for every campaign, registered through its module and nowhere else.
/// </summary>
[TestClass]
public class LordCourtesiesWiringTests
{
    [TestMethod]
    public void FeatureModules_ListTheModuleOnce()
    {
        Assert.AreEqual(1, FeatureModules.All.OfType<LordCourtesiesModule>().Count(),
            "LordCourtesiesModule must be listed exactly once in Main/Composition/FeatureModules.cs");
        var src = RepoPaths.ReadSource("Main/Composition/FeatureModules.cs", stripComments: true);
        Assert.IsTrue(src.Contains("new Features.LordCourtesies.LordCourtesiesModule()"));
    }

    [TestMethod]
    public void TheSingleOwnerFiles_NameNothingOfTheFeature()
    {
        foreach (var file in new[] { "Main/IoC.cs", "Main/SubModule.cs" })
        {
            var src = RepoPaths.ReadSource(file, stripComments: true);
            Assert.IsFalse(src.Contains("LordCourtes"), $"{file} wires the feature by hand AND through its module");
        }
    }

    [TestMethod]
    public void Module_OwnsNoSaveData_AndPatchesNothing()
    {
        var module = new LordCourtesiesModule();

        Assert.AreEqual("LordCourtesies", module.Id);
        Assert.IsFalse(module.OwnsSaveData);
        Assert.AreEqual(0, module.PatchCategories.Count);
        Assert.AreEqual(0, module.GameModels.Count);
        Assert.AreEqual(1, module.CampaignBehaviors.Count);
    }

    [TestMethod]
    public void Module_BuildsTheBehaviorWithNew_NeverFromTheContainer()
    {
        var src = RepoPaths.ReadSource("Main/Features/LordCourtesies/LordCourtesiesModule.cs", stripComments: true);

        Assert.IsTrue(src.Contains("new LordCourtesiesDialogBehavior("));
        Assert.IsFalse(src.Contains("Resolve<LordCourtesiesDialogBehavior>"));
    }

    [TestMethod]
    public void Module_DoesNotRegisterTheMapRevealAdapterAgain()
    {
        var src = RepoPaths.ReadSource("Main/Features/LordCourtesies/LordCourtesiesIoC.cs", stripComments: true);

        Assert.IsFalse(src.Contains("MapRevealAdapter"), "HardQuestionsIoC owns the registration; this feature only resolves it");
    }

    [TestMethod]
    public void TheFeature_NeverTouchesHarmony_OrSaveData()
    {
        foreach (var file in Directory.GetFiles(RepoPaths.RepoPath("Main", "Features", "LordCourtesies"), "*.cs", SearchOption.AllDirectories))
        {
            var src = RepoPaths.StripComments(File.ReadAllText(file));
            Assert.IsFalse(src.Contains("HarmonyLib"), file);
            Assert.IsFalse(src.Contains("[HarmonyPatch"), file);
            Assert.IsFalse(src.Contains("dataStore.SyncData"), file);
        }
    }

    [TestMethod]
    public void EntryPoints_StayUnder150Lines()
    {
        foreach (var file in new[] { "LordCourtesiesDialogBehavior", "LordCourtesiesDialogLines" })
        {
            var lines = RepoPaths.ReadSource($"Main/Features/LordCourtesies/Hooks/{file}.cs").Split('\n').Length;
            Assert.IsTrue(lines < 150, $"{file} is {lines} lines (ADR-002)");
        }
    }

    [TestMethod]
    public void DialogLines_SitOnHeroMainOptionsAtPriority111_WithLocalizedText()
    {
        var src = RepoPaths.ReadSource("Main/Features/LordCourtesies/Hooks/LordCourtesiesDialogLines.cs", stripComments: true);

        Assert.IsTrue(src.Contains("\"hero_main_options\""));
        Assert.IsTrue(src.Contains("Priority = 111"));
        Assert.IsTrue(src.Contains("{=taom_lc_"));
        Assert.IsFalse(src.Contains("Tell me where your kin hold their lands"), "the Hard Questions captive line must stay distinct");
    }

    [TestMethod]
    public void TaomSettings_DeclaresBothTogglesInTheirOwnGroupDefaultingOn()
    {
        var src = RepoPaths.ReadSource("Main/Features/TaomSettings.cs", stripComments: true);

        Assert.IsTrue(src.Contains("[SettingPropertyGroup(\"World/Lord Courtesies\", GroupOrder = 63)]"));
        Assert.IsTrue(src.Contains("public bool EnableTroopGifts { get; set; } = true;"));
        Assert.IsTrue(src.Contains("public bool EnableDomainReveal { get; set; } = true;"));
        Assert.AreEqual(1, src.Split(new[] { "GroupOrder = 63" }, System.StringSplitOptions.None).Length - 1,
            "GroupOrder 63 is used by another group");
        Assert.IsTrue(src.Contains("[SettingPropertyBool(\"Enable Troop Gifts\", Order = 0, RequireRestart = false,"));
        Assert.IsTrue(src.Contains("[SettingPropertyBool(\"Enable Domain Reveal\", Order = 1, RequireRestart = false,"));
    }

    [TestMethod]
    public void TheGiftScreen_IsNotDonating_SoVanillaAndPatch106DoNotBothPay()
    {
        var adapter = RepoPaths.ReadSource("Main/Adapters/LordCourtesyAdapter.cs", stripComments: true);
        var patch = RepoPaths.ReadSource("Main/Features/BondsOfBattle/Hooks/PartyScreenLogic_DoneLogic_Patch.cs", stripComments: true);

        // Vanilla sets the influence only on a donating screen (PartyScreenLogic.cs:834-860), and Patch106 pays only
        // from that influence; the service pays the hand-over itself from the commit.
        Assert.IsTrue(adapter.Contains("state.IsDonating = false;"));
        Assert.IsTrue(patch.Contains("PartyInfluenceChangeAmount.Item2"));
        Assert.IsFalse(adapter.Contains("IsDonating = true"));
    }

    [TestMethod]
    public void TheGiftScreen_SettlesOnTheEnginesCommit_AndRefusesTalking()
    {
        var adapter = RepoPaths.ReadSource("Main/Adapters/LordCourtesyAdapter.cs", stripComments: true);

        Assert.IsTrue(adapter.Contains("partyPresentationDoneButtonDelegate:"));
        Assert.IsFalse(adapter.Contains("partyScreenClosedDelegate"), "the commit, not the closed event, is where the gift settles");
        Assert.IsTrue(adapter.Contains("logic.CanTalkToHeroDelegate = RefuseTalking;"));
    }

    [TestMethod]
    public void TheMapReveal_MarksWithTheVisualTracker_AndWritesNoHasVisited()
    {
        var adapter = RepoPaths.ReadSource("Main/Adapters/MapRevealAdapter.cs", stripComments: true);

        Assert.IsTrue(adapter.Contains("tracker.RegisterObject(settlement)"));
        Assert.IsTrue(adapter.Contains("tracker.CheckTracked(settlement)"), "RegisterObject on a tracked object raises its count");
        Assert.IsFalse(adapter.Contains("HasVisited"));
    }

    [TestMethod]
    public void TheMapReveal_MarksFortificationsOnly_SoNoVillagePlateStaysDrawnEveryFrame()
    {
        var adapter = RepoPaths.ReadSource("Main/Adapters/MapRevealAdapter.cs", stripComments: true);

        Assert.IsTrue(adapter.Contains("settlement.IsFortification"));
    }

    [TestMethod]
    public void TheMapRevealContract_IsTheTrunkOne_VoidReveals()
    {
        var contract = RepoPaths.ReadSource("Main/Adapters/IMapRevealAdapter.cs", stripComments: true);

        Assert.IsTrue(contract.Contains("void RevealClan(string clanId);"));
        Assert.IsTrue(contract.Contains("void RevealKingdom(string kingdomId);"));
    }

    [TestMethod]
    public void TheCoLocation_IsFalseWhileAMessengerConversationIsOpen()
    {
        var adapter = RepoPaths.ReadSource("Main/Adapters/LordCourtesyAdapter.cs", stripComments: true);

        Assert.IsTrue(adapter.Contains("IMessengerConversationState"));
        Assert.IsTrue(adapter.Contains("_messenger.IsOpen"));
    }

    [TestMethod]
    public void TheGiftDoneStep_IsGuardedAndLogsAFault()
    {
        var adapter = RepoPaths.ReadSource("Main/Adapters/LordCourtesyAdapter.cs", stripComments: true);
        var start = adapter.IndexOf("private bool GiftDone(", System.StringComparison.Ordinal);
        var body = adapter.Substring(start, adapter.IndexOf("return true;", start, System.StringComparison.Ordinal) - start);

        Assert.IsTrue(body.Contains("try"));
        Assert.IsTrue(body.Contains("catch (Exception ex)"));
        Assert.IsTrue(body.Contains("LogError"));
    }

    [TestMethod]
    public void TheCoLocation_AlsoRefusesDuringABattleSimulation()
    {
        var adapter = RepoPaths.ReadSource("Main/Adapters/LordCourtesyAdapter.cs", stripComments: true);

        Assert.IsTrue(adapter.Contains("PlayerEncounter.Current?.BattleSimulation != null"));
    }

    [TestMethod]
    public void TheRosterDiff_IsAStaticOnTheService_AndTroopCountHasNoTier()
    {
        var module = RepoPaths.ReadSource("Main/Features/LordCourtesies/LordCourtesiesIoC.cs", stripComments: true);

        Assert.IsFalse(module.Contains("TroopGiftService"));
        Assert.IsFalse(File.Exists(RepoPaths.RepoPath("Main", "Features", "LordCourtesies", "TroopGiftService.cs")));
        Assert.IsNull(typeof(TAOM.Features.LordCourtesies.Domain.TroopCount).GetProperty("Tier"));
    }

    [TestMethod]
    public void TheGiftHint_NamesBothSwitchesThePaymentNeeds()
    {
        var src = RepoPaths.ReadSource("Main/Features/TaomSettings.cs");
        var start = src.IndexOf("\"Enable Troop Gifts\"", System.StringComparison.Ordinal);
        var hint = src.Substring(start, src.IndexOf("EnableTroopGifts", start, System.StringComparison.Ordinal) - start);

        Assert.IsTrue(hint.Contains("Bonds of Battle"));
        Assert.IsTrue(hint.Contains("Troop Gifts Win Lords' Favour"));
        Assert.IsTrue(hint.Contains("10"));
    }

    [TestMethod]
    public void TheBondsHandoverHint_NamesItsSecondCaller()
    {
        var src = RepoPaths.ReadSource("Main/Features/TaomSettings.cs");
        var start = src.IndexOf("\"Troop Gifts Win Lords' Favour\"", System.StringComparison.Ordinal);
        var hint = src.Substring(start, src.IndexOf("BondsTroopHandoverEnabled", start, System.StringComparison.Ordinal) - start);

        Assert.IsTrue(hint.Contains("Lord Courtesies"));
        Assert.IsTrue(hint.Contains("Default: on."));
        Assert.IsTrue(src.Contains("public bool BondsTroopHandoverEnabled { get; set; } = TAOM.Features.BondsOfBattle.BondsOfBattleSettingsProvider.DefaultTroopHandoverEnabled;"));
    }

    [TestMethod]
    public void TheLanguageFiles_KeepOneBlankLineAfterTheGiftHeaderRow()
    {
        var moduleData = RepoPaths.RepoPath("Main", "_Module", "ModuleData");
        foreach (var file in Directory.GetFiles(Path.Combine(moduleData, "Languages"), "std_taom_module_strings_*.xml", SearchOption.AllDirectories))
        {
            var xml = File.ReadAllText(file);
            Assert.IsFalse(System.Text.RegularExpressions.Regex.IsMatch(xml, "taom_lc_gift_header\"[^\\n]*\\n([ \\t\\r]*\\n){2}"), file);
        }
    }

    [TestMethod]
    public void DialogBehavior_RoutesTheReplyThroughTheService()
    {
        var src = RepoPaths.ReadSource("Main/Features/LordCourtesies/Hooks/LordCourtesiesDialogBehavior.cs", stripComments: true);

        Assert.IsTrue(src.Contains("LordCourtesiesService.RepliesWithRefusal("));
        Assert.IsFalse(src.Contains("DomainOutcome.Revealed"), "the outcome-to-reply mapping belongs to the service");
    }

    [TestMethod]
    public void TheNoRoomHint_IsGenderNeutral_InTheCodeAndEveryLanguageFile()
    {
        const string text = "That party has no room for so many.";
        Assert.IsTrue(RepoPaths.ReadSource("Main/Adapters/LordCourtesyAdapter.cs").Contains("{=taom_lc_gift_full}" + text));

        var moduleData = RepoPaths.RepoPath("Main", "_Module", "ModuleData");
        var files = Directory.GetFiles(moduleData, "taom_module_strings.xml")
            .Concat(Directory.GetFiles(Path.Combine(moduleData, "Languages"), "std_taom_module_strings_*.xml", SearchOption.AllDirectories)).ToList();
        Assert.AreEqual(13, files.Count);
        foreach (var file in files)
        {
            var xml = File.ReadAllText(file);
            if (Path.GetFileName(file) == "taom_module_strings.xml")
                Assert.IsTrue(System.Text.RegularExpressions.Regex.IsMatch(xml,
                    "id=\"taom_lc_gift_full\" text=\"(\\{=taom_lc_gift_full\\})?" + System.Text.RegularExpressions.Regex.Escape(text) + "\""), file);
            else
                Assert.AreEqual(1, System.Text.RegularExpressions.Regex.Matches(xml, "id=\"taom_lc_gift_full\"").Count, file + ": one row");
            Assert.IsFalse(xml.Contains("taom_lc_gift_thanks"), file);
        }
    }

    [TestMethod]
    public void SettingsProvider_FallsBackToOnWithoutMcm()
    {
        var provider = new LordCourtesiesSettingsProvider();

        Assert.IsTrue(provider.TroopGiftsEnabled);
        Assert.IsTrue(provider.DomainRevealEnabled);
    }

    [TestCategory("RequiresGame")]
    [TestMethod]
    public void Module_RegistersTheServiceGraph_AndTheBehaviorDeclResolves()
    {
        using var container = new Container();
        var paths = Substitute.For<IPathService>();
        paths.ModuleDataPath.Returns(Path.Combine(Path.GetTempPath(), "taom-lc-wiring-" + System.Guid.NewGuid().ToString("N")));
        container.RegisterInstance(paths);
        container.RegisterInstance(Substitute.For<IModLogger>());
        container.RegisterInstance(Substitute.For<ICoopSessionProvider>());
        container.RegisterInstance(Substitute.For<IDedicatedServerProvider>());
        container.RegisterInstance(Substitute.For<IAlignmentService>());
        container.RegisterInstance(Substitute.For<IArmouryPlayerAdapter>());
        container.RegisterInstance(Substitute.For<IMapRevealAdapter>());
        container.RegisterInstance(Substitute.For<IMessengerConversationState>());
        container.RegisterInstance(new TroopHandoverService(Substitute.For<IRelationGrantService>(),
            Substitute.For<IBondsOfBattleSettingsProvider>(), Substitute.For<IEnlistmentStateQuery>(),
            Substitute.For<IRandomSource>(), Substitute.For<IBondsNoticeAdapter>(), Substitute.For<ICoopSessionProvider>()));
        var module = new LordCourtesiesModule();

        module.RegisterServices(container);

        Assert.IsNotNull(container.Resolve<LordCourtesiesService>());
        foreach (var decl in module.CampaignBehaviors)
            Assert.IsNotNull(decl.Create(container));
    }
}
