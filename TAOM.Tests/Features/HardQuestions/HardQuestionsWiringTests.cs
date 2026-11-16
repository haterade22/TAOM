using System.IO;
using System.Linq;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Composition;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.ArmourAcquisition;
using TAOM.Features.CoopInterop;
using TAOM.Features.Execution;
using TAOM.Features.HardQuestions;
using TAOM.Features.InitialChildGeneration;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.HardQuestions;

/// <summary>
/// Wiring guards. The module owns save data, so a dependency its behaviors cannot resolve throws out of
/// OnGameStart and no campaign starts. Both behaviors reset per-campaign state in their constructors, so they
/// must be built with <c>new</c> for every campaign, never taken from the container (one construction per process).
/// </summary>
[TestClass]
public class HardQuestionsWiringTests
{
    [TestMethod]
    public void FeatureModules_ListTheHardQuestionsModuleOnce()
    {
        Assert.AreEqual(1, FeatureModules.All.OfType<HardQuestionsModule>().Count(),
            "HardQuestionsModule must be listed exactly once in Main/Composition/FeatureModules.cs");
        var src = RepoPaths.ReadSource("Main/Composition/FeatureModules.cs", stripComments: true);
        Assert.IsTrue(src.Contains("new Features.HardQuestions.HardQuestionsModule()"));
    }

    [TestMethod]
    public void CaptiveLordAdapter_PassesTheLordsClanId_SoTheNeutralFloorReachesHisClan()
    {
        var src = RepoPaths.ReadSource("Main/Adapters/CaptiveLordAdapter.cs", stripComments: true);

        Assert.IsTrue(src.Contains("relationId, hero.Clan?.StringId)"),
            "LordRef must carry the lord's clan id, or the Neutral floor (HardQuestionsPenaltyApplier) never reaches his clan");
    }

    [TestMethod]
    public void CaptiveLordAdapter_ReadsTheRansomScreenFromTheWholeStateStack()
    {
        var src = RepoPaths.ReadSource("Main/Adapters/CaptiveLordAdapter.cs", stripComments: true);

        StringAssert.Contains(src, "LastOrDefault<PartyState>()");
        StringAssert.Contains(src, "PartyScreenHelper.PartyScreenMode.Ransom");
        Assert.IsFalse(src.Contains("ActiveState"), "the conversation's MissionState sits above the screen");
        Assert.IsFalse(src.Contains("GetActivePartyState"), "the conversation's MissionState sits above the screen");
    }

    [TestMethod]
    public void IoC_DoesNotRegisterTheFeatureByHand()
    {
        var src = RepoPaths.ReadSource("Main/IoC.cs", stripComments: true);

        Assert.IsFalse(src.Contains("RegisterHardQuestionsFeature"),
            "Main/IoC.cs registers the feature by hand AND through its module");
    }

    [TestMethod]
    public void Module_OwnsSaveData()
    {
        Assert.IsTrue(new HardQuestionsModule().OwnsSaveData);
        Assert.AreEqual("HardQuestions", new HardQuestionsModule().Id);
    }

    [TestMethod]
    public void Module_BuildsBothBehaviorsWithNew_NeverFromTheContainer()
    {
        var src = RepoPaths.ReadSource("Main/Features/HardQuestions/HardQuestionsModule.cs", stripComments: true);

        Assert.IsTrue(src.Contains("new HardQuestionsCampaignBehavior("), "the campaign behavior must be built per campaign");
        Assert.IsTrue(src.Contains("new HardQuestionsDialogBehavior("), "the dialog behavior must be built per campaign");
        Assert.IsFalse(src.Contains("Resolve<HardQuestionsCampaignBehavior>"));
        Assert.IsFalse(src.Contains("Resolve<HardQuestionsDialogBehavior>"));
    }

    [TestMethod]
    public void Module_DeclaresTwoCampaignBehaviors()
    {
        Assert.AreEqual(2, new HardQuestionsModule().CampaignBehaviors.Count);
    }

    [TestMethod]
    public void BehaviorConstructors_ResetPerCampaignState()
    {
        var campaign = RepoPaths.ReadSource("Main/Features/HardQuestions/Hooks/HardQuestionsCampaignBehavior.cs", stripComments: true);
        var dialog = RepoPaths.ReadSource("Main/Features/HardQuestions/Hooks/HardQuestionsDialogBehavior.cs", stripComments: true);

        Assert.IsTrue(campaign.Contains("_state.Reset();"));
        Assert.IsTrue(dialog.Contains("_service.ResetPending();"));
        Assert.IsTrue(campaign.Contains("CampaignEvents.HeroKilledEvent.AddNonSerializedListener(this, OnHeroKilled)"),
            "a death in captivity raises no release event, so the record is cleared on HeroKilled");
        Assert.IsFalse(campaign.Contains("OnSessionLaunched"), "persisted state must not be reset from OnSessionLaunched");
    }

    [TestMethod]
    public void EntryPoints_StayUnder150Lines()
    {
        foreach (var file in new[] { "HardQuestionsCampaignBehavior", "HardQuestionsDialogBehavior", "HardQuestionsDialogLines" })
        {
            var lines = RepoPaths.ReadSource($"Main/Features/HardQuestions/Hooks/{file}.cs").Split('\n').Length;
            Assert.IsTrue(lines < 150, $"{file} is {lines} lines (ADR-002)");
        }
    }

    [TestMethod]
    public void TaomSettings_DeclaresTheToggleInItsOwnGroupDefaultingOn()
    {
        var src = RepoPaths.ReadSource("Main/Features/TaomSettings.cs", stripComments: true);

        Assert.IsTrue(src.Contains("[SettingPropertyGroup(\"{=taom_mcm_group_world}World/{=taom_mcm_group_hard_questions}Hard Questions\", GroupOrder = 57)]"));
        Assert.IsTrue(src.Contains("public bool EnableHardQuestions { get; set; } = true;"));
        Assert.AreEqual(1, src.Split(new[] { "GroupOrder = 57" }, System.StringSplitOptions.None).Length - 1,
            "GroupOrder 57 is used by another group");
    }

    [TestMethod]
    public void SettingsProvider_FallsBackToOnWithoutMcm()
    {
        Assert.IsTrue(new HardQuestionsSettingsProvider().IsEnabled);
    }

    [TestCategory("RequiresGame")]
    [TestMethod]
    public void Module_RegistersTheServiceGraph_AndEveryBehaviorDeclResolves()
    {
        using var container = new Container();
        var paths = Substitute.For<IPathService>();
        paths.ModuleDataPath.Returns(Path.Combine(Path.GetTempPath(), "taom-hq-wiring-" + System.Guid.NewGuid().ToString("N")));
        container.RegisterInstance(paths);
        container.RegisterInstance(Substitute.For<IModLogger>());
        container.RegisterInstance(Substitute.For<ICoopSessionProvider>());
        container.RegisterInstance(Substitute.For<IDedicatedServerProvider>());
        container.RegisterInstance(Substitute.For<IAlignmentService>());
        container.RegisterInstance(Substitute.For<IExecutionRelationService>());
        container.RegisterInstance(Substitute.For<IArmouryPlayerAdapter>());
        container.RegisterInstance(Substitute.For<IArmourGateService>());
        container.RegisterInstance(Substitute.For<IHeroSkillXpAdapter>());
        container.RegisterInstance(Substitute.For<IRandomSource>());
        var module = new HardQuestionsModule();

        module.RegisterServices(container);

        Assert.IsNotNull(container.Resolve<InterrogationService>());
        foreach (var decl in module.CampaignBehaviors)
            Assert.IsNotNull(decl.Create(container));
    }
}
