using System.IO;
using System.Linq;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Adapters;
using TAOM.Composition;
using TAOM.Features.CoopInterop.Missions;
using TAOM.Features.OrcBattleVoice;
using TAOM.Features.OrcBattleVoice.Hooks;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.OrcBattleVoice;

[TestClass]
public class OrcBattleVoiceWiringTests
{
    [TestMethod]
    public void FeatureModules_ListTheOrcBattleVoiceModuleOnce()
    {
        Assert.AreEqual(1, FeatureModules.All.OfType<OrcBattleVoiceModule>().Count());
    }

    [TestMethod]
    public void Module_DeclaresOnlyTheMissionBehavior()
    {
        var module = new OrcBattleVoiceModule();

        CollectionAssert.AreEqual(new[] { typeof(OrcBattleVoiceMissionBehavior) },
            module.MissionBehaviors.Select(d => d.BehaviorType).ToArray());
        Assert.AreEqual(0, module.PatchCategories.Count);
        Assert.AreEqual(0, module.CampaignBehaviors.Count);
        Assert.AreEqual(0, module.GameModels.Count);
    }

    [TestMethod]
    public void RegisterServices_ResolvesTheCatalogAdapter()
    {
        using var container = new Container();
        new OrcBattleVoiceModule().RegisterServices(container);

        Assert.IsInstanceOfType(container.Resolve<IVoiceDefinitionCatalogAdapter>(), typeof(VoiceDefinitionCatalogAdapter));
    }

    [TestMethod]
    public void CoopRole_IsLocal()
    {
        Assert.AreEqual(CoopMissionRole.Local, CoopMissionRoles.All[nameof(OrcBattleVoiceMissionBehavior)]);
    }

    [TestMethod]
    public void Behavior_StaysUnderTheEntryPointLimit()
    {
        var path = RepoPaths.RepoPath("Main", "Features", "OrcBattleVoice", "Hooks", "OrcBattleVoiceMissionBehavior.cs");

        Assert.IsTrue(File.ReadAllLines(path).Length < 150);
    }

    [TestMethod]
    public void Behavior_HandsEachBuiltAgentToTheService()
    {
        var body = RepoPaths.ReadSource("Main/Features/OrcBattleVoice/Hooks/OrcBattleVoiceMissionBehavior.cs", stripComments: true);

        StringAssert.Contains(body, "override void OnAgentBuild(Agent agent, Banner banner)");
        StringAssert.Contains(body, "_service.OnAgentBuild(new OrcBattleVoiceAgent(agent))");
        Assert.IsFalse(body.Contains("OnBehaviorInitialize"), "never fires for a behavior added from OnMissionBehaviorInitialize (#606)");
    }

    [TestMethod]
    public void Behavior_ReappliesThroughTheMissionListenerAndLetsGoAtTheEnd()
    {
        var body = RepoPaths.ReadSource("Main/Features/OrcBattleVoice/Hooks/OrcBattleVoiceMissionBehavior.cs", stripComments: true);

        StringAssert.Contains(body, ": MissionLogic, IMissionListener");
        StringAssert.Contains(body, "override void AfterStart()");
        StringAssert.Contains(body, "Mission.AddListener(this)");
        StringAssert.Contains(body, "Mission?.RemoveListener(this)");
        StringAssert.Contains(body, "public void OnEquipItemsFromSpawnEquipment(Agent agent, Agent.CreationType creationType)");
        StringAssert.Contains(body, "void IMissionListener.OnEndMission()");
        Assert.IsFalse(body.Contains("BannerBearerLogic"), "the listener covers every in-place rebuild, the banner event is not needed");
    }

    [TestMethod]
    public void Adapter_TreatsOnlyValidVisualsAsPresent()
    {
        var body = RepoPaths.ReadSource("Main/Adapters/OrcBattleVoiceAgent.cs", stripComments: true);

        StringAssert.Contains(body, "_agent.AgentVisuals?.IsValid() == true");
    }

    [TestMethod]
    public void Adapter_SetsTheVoiceOnTheAgentVisualsAndReadsItBackFromTheAgent()
    {
        var body = RepoPaths.ReadSource("Main/Adapters/OrcBattleVoiceAgent.cs", stripComments: true);

        StringAssert.Contains(body, "AgentVisuals.SetVoiceDefinitionIndex(index, pitch)");
        StringAssert.Contains(body, "_agent.GetAgentVoiceDefinition()");
    }
}
