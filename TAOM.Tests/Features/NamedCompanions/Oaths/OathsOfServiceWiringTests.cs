using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Composition;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.NamedCompanions;
using TAOM.Features.NamedCompanions.Oaths;
using TAOM.Features.NamedCompanions.Oaths.Hooks;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.NamedCompanions.Oaths;

/// <summary>
/// #904 wiring. The ledger's session reset lives in <c>OathsOfServiceCampaignBehavior</c>'s constructor, so it holds only
/// while the module builds a fresh behavior inside the declaration's factory, which the runner calls once per campaign
/// start. A cached or shared instance would run the constructor once per process and carry one campaign's oath flags
/// into the next, so the pin is the exact shape: inline <c>new</c> as the factory's body, exactly once.
/// </summary>
[TestClass]
public class OathsOfServiceWiringTests
{
    private const string ModulePath = "Main/Features/NamedCompanions/Oaths/OathsOfServiceModule.cs";
    private const string CampaignBehaviorPath = "Main/Features/NamedCompanions/Oaths/Hooks/OathsOfServiceCampaignBehavior.cs";
    private const string HireDialogPath = "Main/Features/NamedCompanions/Oaths/Hooks/OathHireDialog.cs";

    private static readonly Regex InlinePerCampaign =
        new Regex(@"CampaignBehaviorDecl\.Of\(\s*r\s*=>\s*new\s+OathsOfServiceCampaignBehavior\(");

    private static readonly Regex AnyConstruction = new Regex(@"new\s+OathsOfServiceCampaignBehavior\(");

    private static bool IsBuiltInlinePerCampaign(string source) =>
        InlinePerCampaign.Matches(source).Count == 1 && AnyConstruction.Matches(source).Count == 1;

    [TestMethod]
    public void FeatureModules_ListTheModuleOnce()
    {
        Assert.AreEqual(1, FeatureModules.All.OfType<OathsOfServiceModule>().Count(),
            "OathsOfServiceModule must be listed exactly once in Main/Composition/FeatureModules.cs");
    }

    [TestMethod]
    public void Module_OwnsSaveData_SoAFaultFailsClosed() => Assert.IsTrue(new OathsOfServiceModule().OwnsSaveData);

    [TestMethod]
    public void Module_DeclaresBothBehaviors()
    {
        Assert.AreEqual(2, new OathsOfServiceModule().CampaignBehaviors.Count);
    }

    [TestMethod]
    public void Module_BuildsTheCampaignBehaviorInlineAsTheFactoryBody()
    {
        var src = RepoPaths.ReadSource(ModulePath, stripComments: true);

        Assert.IsTrue(IsBuiltInlinePerCampaign(src),
            "OathsOfServiceModule must build OathsOfServiceCampaignBehavior with new as the CampaignBehaviorDecl factory's "
            + "body, exactly once: its constructor is the per-campaign reset of the oath ledger.");
    }

    [TestMethod]
    public void Module_BuildsTheDialogBehaviorOnce()
    {
        var src = RepoPaths.ReadSource(ModulePath, stripComments: true);

        Assert.AreEqual(1, Regex.Matches(src, @"new\s+OathsOfServiceDialogBehavior\(").Count);
    }

    [TestMethod]
    public void ProductionSource_NoOtherFileConstructsTheBehaviors()
    {
        var offenders = RepoPaths.CSharpFiles()
            .Where(path => !IsFile(path, "OathsOfServiceModule.cs"))
            .Where(path => RepoPaths.MentionsOutsideComments(path, "new OathsOfServiceCampaignBehavior(", wholeWord: false)
                || RepoPaths.MentionsOutsideComments(path, "new OathsOfServiceDialogBehavior(", wholeWord: false))
            .ToList();

        Assert.AreEqual(0, offenders.Count,
            "Only OathsOfServiceModule may construct the behaviors: " + string.Join(", ", offenders));
    }

    [TestMethod]
    public void HandWiredEntryPoints_DoNotMentionTheFeature()
    {
        foreach (var path in new[] { "Main/IoC.cs", "Main/SubModule.cs" })
            Assert.IsFalse(RepoPaths.ReadSource(path, stripComments: true).Contains("Oaths"),
                path + " must not mention Oaths: the module owns the wiring");
    }

    [TestMethod]
    public void CampaignBehavior_SavesUnderTheTwoKeys_AndNotTheOldFlatStringKeys()
    {
        var src = RepoPaths.ReadSource(CampaignBehaviorPath, stripComments: true);

        foreach (var key in new[] { "_taom_oath_flagList", "_taom_oath_topicDays" })
            StringAssert.Contains(src, "\"" + key + "\"");
        foreach (var old in new[] { "\"_taom_oath_flags\"", "\"_taom_oath_topicsDone\"", "\"_taom_oath_topicLastDay\"" })
            Assert.IsFalse(src.Contains(old), old + " was the flat-string key; the value type changed, so the key did too");
    }

    [TestMethod]
    public void CampaignBehavior_ConstructorResetsTheLedger()
    {
        var src = RepoPaths.ReadSource(CampaignBehaviorPath, stripComments: true);
        var ctor = Regex.Match(src, @"public OathsOfServiceCampaignBehavior\([^)]*\)\s*\{(?<body>[^}]*)\}").Groups["body"].Value;

        StringAssert.Contains(ctor, "_ledger.ResetForNewSession()");
    }

    [TestMethod]
    public void CampaignBehavior_RaisesTheAnchorOnTheDailyTick_AndRunsItOnTheHourlyTick()
    {
        var src = RepoPaths.ReadSource(CampaignBehaviorPath, stripComments: true);

        StringAssert.Contains(src, "CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick)");
        StringAssert.Contains(src, "CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, OnHourlyTick)");
        Assert.IsFalse(src.Contains("OnSessionLaunched"), "the id resolution moved to the dialog behavior");
    }

    [TestCategory("RequiresGame")]
    [TestMethod]
    public void Module_BuildsAFreshBehaviorPerCampaign_OverOneSharedLedger()
    {
        using var container = new Container();
        container.RegisterInstance(Substitute.For<IModLogger>());
        container.RegisterInstance(Substitute.For<ICoopSessionProvider>());
        container.RegisterInstance(Substitute.For<INamedCompanionConfigProvider>());
        container.RegisterInstance(Substitute.For<INamedCompanionAdapter>());
        container.RegisterInstance(Substitute.For<IPartyItemRosterAdapter>());
        container.RegisterInstance(Substitute.For<ICareerPurseAdapter>());
        container.RegisterInstance(Substitute.For<IItemPoolAdapter>());
        container.RegisterInstance(Substitute.For<IArmourItemCatalogAdapter>());
        // The backstory intro asks the Wanderer Allegiance verdict (#1061); that feature registers the service.
        container.RegisterInstance(Substitute.For<TAOM.Features.Execution.IAlignmentService>());
        container.RegisterInstance(Substitute.For<TAOM.Features.WandererAllegiance.IWandererAllegianceSettingsProvider>());
        container.Register<TAOM.Features.WandererAllegiance.WandererAllegianceService>(Reuse.Singleton);
        var module = new OathsOfServiceModule();
        module.RegisterServices(container);
        var campaign = module.CampaignBehaviors.Single(d => d.BehaviorType == typeof(OathsOfServiceCampaignBehavior));
        var dialog = module.CampaignBehaviors.Single(d => d.BehaviorType == typeof(OathsOfServiceDialogBehavior));
        var ledger = container.Resolve<OathLedger>();

        Assert.AreSame(ledger, container.Resolve<IOathFlagStore>(), "quests set flags on the ledger the dialogs read");
        ledger.Set("from_the_last_campaign");
        var first = campaign.Create(container);

        Assert.IsFalse(ledger.Has("from_the_last_campaign"), "Create runs the constructor, which resets the ledger");
        ledger.Set("second_campaign");
        var second = campaign.Create(container);
        Assert.AreNotSame(first, second, "a fresh behavior per campaign start");
        Assert.IsFalse(ledger.Has("second_campaign"));
        Assert.AreNotSame(dialog.Create(container), dialog.Create(container));
    }

    [TestMethod]
    public void HireDialog_SitsAboveVanillaAndBelowWandererAllegiance_OnCompanionHire()
    {
        var hire = RepoPaths.ReadSource(HireDialogPath, stripComments: true);
        var allegiance = RepoPaths.ReadSource("Main/Features/WandererAllegiance/Hooks/WandererAllegianceDialogBehavior.cs", stripComments: true);
        var ours = int.Parse(Regex.Match(hire, @"private const int NpcPriority = (?<p>\d+);").Groups["p"].Value);
        var theirs = int.Parse(Regex.Match(allegiance, @"private const int Priority = (?<p>\d+);").Groups["p"].Value);

        Assert.IsTrue(ours > 100, "vanilla's hire reply sits at the default 100");
        Assert.IsTrue(ours < theirs, "Wanderer Allegiance's refusals must answer first: that gate is kept by priority alone");
        foreach (var id in new[] { "taom_oath_locked", "taom_oath_terms" })
            Assert.IsTrue(Regex.IsMatch(hire, "AddDialogLine\\(\\s*\"" + id + "\"\\s*,\\s*\"companion_hire\""), id + " must sit on companion_hire");
        Assert.AreEqual(2, Regex.Matches(hire, @"null,\s*NpcPriority\)").Count, "both NPC lines must pass NpcPriority");
        Assert.IsFalse(hire.Contains("IWandererAllegianceService"), "the allegiance verdict is kept by priority, not re-asked");
    }

    [TestMethod]
    public void DialogBehavior_ResolvesTheIdsThenRegistersBothHalves()
    {
        var src = RepoPaths.ReadSource("Main/Features/NamedCompanions/Oaths/Hooks/OathsOfServiceDialogBehavior.cs", stripComments: true);

        var resolve = src.IndexOf("OathConfigValidator.ResolveIds(", System.StringComparison.Ordinal);
        var hire = src.IndexOf("_hire.Register(starter)", System.StringComparison.Ordinal);
        var topics = src.IndexOf("_topics.Register(starter)", System.StringComparison.Ordinal);
        Assert.IsTrue(resolve > 0, "ResolveIds is gone");
        Assert.IsTrue(hire > resolve && topics > resolve, "both halves read the config, so the ids resolve first");
    }

    [TestMethod]
    public void TopicDialog_OpensOnHeroMainOptions_AndNeverStallsWithoutAReply()
    {
        var src = RepoPaths.ReadSource("Main/Features/NamedCompanions/Oaths/Hooks/OathTopicDialog.cs", stripComments: true);

        Assert.IsTrue(Regex.IsMatch(src, "AddPlayerLine\\(\\s*\"taom_oath_topics_open\"\\s*,\\s*\"hero_main_options\""));
        Assert.IsTrue(Regex.IsMatch(src, "AddDialogLine\\(\\s*\"taom_oath_topic_reply_fallback\"\\s*,\\s*\"taom_oath_topic_reply\"\\s*,\\s*\"lord_pretalk\"[^;]*null,\\s*1\\)"),
            "a priority-1 fallback must follow the reply line: the conversation manager has no exit when no NPC line matches");
        StringAssert.Contains(src, "prompt.Value");
    }

    [DataTestMethod]
    [DataRow("Main/Features/NamedCompanions/Oaths/Hooks/OathTopicDialog.cs")]
    [DataRow("Main/Features/NamedCompanions/Oaths/Hooks/OathHireDialog.cs")]
    public void MissingItemsHint_IsBoundOnItsTextObject_NotTheGlobalVariable(string path)
    {
        var src = RepoPaths.ReadSource(path, stripComments: true);

        Assert.IsFalse(src.Contains("MBTextManager.SetTextVariable(\"TAOM_OATH_MISSING\""),
            path + " writes the global TAOM_OATH_MISSING, which is read on hover: two blocked lines would show the last value. "
            + "Bind it on the hint's TextObject.");
    }

    [TestMethod]
    public void NamedCompanionAdapter_IsRecruitedOrInParty_CoversAPromotedLordByClan()
    {
        var src = RepoPaths.ReadSource("Main/Adapters/NamedCompanionAdapter.cs", stripComments: true);
        var body = Regex.Match(src, @"public bool IsRecruitedOrInParty\(.*?\n    \}", RegexOptions.Singleline).Value;

        Assert.IsTrue(body.Length > 0, "IsRecruitedOrInParty was not found");
        StringAssert.Contains(body, "hero.Clan != null");
    }

    [TestMethod]
    public void OathTopicDialog_TreatsAnyHeroWithAClanAsRecruited()
    {
        // A companion promoted to lord has CompanionOf null and its own clan; it must not see "unrecruited" topics,
        // the same rule the daily anchor applies through IsRecruitedOrInParty.
        var src = RepoPaths.ReadSource("Main/Features/NamedCompanions/Oaths/Hooks/OathTopicDialog.cs", stripComments: true);

        StringAssert.Contains(src, "_topics.Visible(hero.StringId, hero.Clan != null,");
    }

    [DataTestMethod]
    [DataRow("CampaignBehaviorDecl.Of(r => _cached ??= new OathsOfServiceCampaignBehavior(a, b)),")]
    [DataRow("private static readonly OathsOfServiceCampaignBehavior Shared = new OathsOfServiceCampaignBehavior(a, b); CampaignBehaviorDecl.Of(r => Shared),")]
    [DataRow("CampaignBehaviorDecl.Of(r => new OathsOfServiceCampaignBehavior(a, b)), var x = new OathsOfServiceCampaignBehavior(a, b);")]
    [DataRow("CampaignBehaviorDecl.Of(r => r.Resolve<OathsOfServiceCampaignBehavior>()),")]
    public void Predicate_RejectsACachedOrSharedConstruction(string mutant)
    {
        Assert.IsFalse(IsBuiltInlinePerCampaign(mutant), mutant);
    }

    [TestMethod]
    public void Predicate_AcceptsTheInlineConstructionAcrossLines()
    {
        Assert.IsTrue(IsBuiltInlinePerCampaign("CampaignBehaviorDecl.Of(r =>\n    new OathsOfServiceCampaignBehavior(\n a, b)),"));
    }

    private static bool IsFile(string path, string fileName) =>
        string.Equals(Path.GetFileName(path), fileName, StringComparison.OrdinalIgnoreCase);

    private const string IntroDialogPath = "Main/Features/NamedCompanions/Oaths/Hooks/OathIntroDialog.cs";

    [TestMethod]
    public void IntroDialog_EntersOnLordStartAt110_ExitsToHeroMainOptions()
    {
        var src = RepoPaths.ReadSource(IntroDialogPath, stripComments: true);

        Assert.IsTrue(Regex.IsMatch(src, "AddDialogLine\\(\\s*\"taom_nc_intro_a\"\\s*,\\s*\"lord_start\"\\s*,\\s*\"taom_nc_intro_pre\"[^;]*IntroCondition,\\s*RecordShown,\\s*Priority\\)"),
            "the opening line must sit on lord_start at the intro priority, above vanilla's default 100 lines");
        StringAssert.Contains(src, "private const int Priority = 110;");
        Assert.IsTrue(Regex.IsMatch(src, "AddDialogLine\\(\\s*\"taom_nc_intro_d\"\\s*,\\s*\"taom_nc_intro_d\"\\s*,\\s*\"hero_main_options\""),
            "the last line must hand the conversation back to hero_main_options");
        foreach (var part in new[] { "PREBACKSTORY", "BACKSTORY_A", "BACKSTORY_B", "BACKSTORY_C", "BACKSTORY_D", "RESPONSE_1", "RESPONSE_2" })
            StringAssert.Contains(src, "{=!}{TAOM_NC_" + part + "}");
    }

    [TestMethod]
    public void IntroDialog_ConditionIsWrappedInTryCatch()
    {
        var src = RepoPaths.ReadSource(IntroDialogPath, stripComments: true);
        var body = Regex.Match(src, @"private bool IntroCondition\(\)\s*\{(?<b>.*?)\n    \}", RegexOptions.Singleline).Groups["b"].Value;

        Assert.IsTrue(body.Length > 0, "IntroCondition was not found");
        Assert.IsTrue(Regex.IsMatch(body.Trim(), @"^try\s*\{.*\}\s*catch \(Exception", RegexOptions.Singleline),
            "ConversationSentence.RunCondition has no catch: the whole condition body must be in try/catch");
    }

    [TestMethod]
    public void DialogBehavior_RegistersTheIntro()
    {
        var src = RepoPaths.ReadSource("Main/Features/NamedCompanions/Oaths/Hooks/OathsOfServiceDialogBehavior.cs", stripComments: true);

        StringAssert.Contains(src, "new OathIntroDialog(");
        var resolve = src.IndexOf("OathConfigValidator.ResolveIds(", System.StringComparison.Ordinal);
        var intro = src.IndexOf("_intro.Register(starter)", System.StringComparison.Ordinal);
        Assert.IsTrue(intro > resolve && resolve > 0, "the intro registers after the ids resolve");
    }
}
