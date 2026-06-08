using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Features.Music;

[TestClass]
public class MusicRuntimeWiringTests
{
    [TestMethod]
    public void MainSubModule_AddsMusicCampaignBehaviorOnGameStart()
    {
        var subModuleSource = ReadProjectSource("Main", "SubModule.cs");

        StringAssert.Contains(subModuleSource, "campaignStarter.AddBehavior(IoC.Resolve<MusicCampaignBehavior>());",
            "Main/SubModule.cs::OnGameStart must register MusicCampaignBehavior through CampaignGameStarter.AddBehavior.");
    }

    [TestMethod]
    public void MainSubModule_AddsMusicMissionBehaviorOnMissionInit()
    {
        var subModuleSource = ReadProjectSource("Main", "SubModule.cs");

        StringAssert.Contains(subModuleSource, "mission.AddMissionBehavior(new MusicMissionBehavior());",
            "Main/SubModule.cs::OnMissionBehaviorInitialize must add MusicMissionBehavior so mission ticks feed music playback.");
    }

    [TestMethod]
    public void MainSubModule_AppliesMusicTavernSuppressionPatchCategory()
    {
        var subModuleSource = ReadProjectSource("Main", "SubModule.cs");

        StringAssert.Contains(subModuleSource, "_harmony.PatchCategory(\"Patch46_Music\");",
            "Main/SubModule.cs::OnGameInitializationFinished must apply Patch46_Music for explicit MusicianGroup suppression patches.");
    }

    [TestMethod]
    public void MusicIoC_RegistersCampaignBehavior()
    {
        var iocSource = ReadProjectSource("Main", "Features", "Music", "MusicIoC.cs");

        StringAssert.Contains(iocSource, "container.Register<MusicCampaignBehavior>",
            "MusicIoC.RegisterMusicFeature must register MusicCampaignBehavior for SubModule resolution.");
    }

    [TestMethod]
    public void MusicIoC_RegistersTavernSuppressionServices()
    {
        var iocSource = ReadProjectSource("Main", "Features", "Music", "MusicIoC.cs");

        StringAssert.Contains(iocSource, "container.Register<IMusicianGroupSuppressionService, MusicianGroupSuppressionService>",
            "MusicIoC.RegisterMusicFeature must register the tavern suppression service.");
        StringAssert.Contains(iocSource, "container.Register<IMusicianGroupSuppressionAdapter, MusicianGroupSuppressionAdapter>",
            "MusicIoC.RegisterMusicFeature must register the adapter that releases MusicianGroup._trackEvent.");
    }

    [TestMethod]
    public void CustomBattlesIoC_WiresMusicSelectedCultureContextIntoCultureSelectionPatch()
    {
        var customBattleIocSource = ReadProjectSource("Main", "Features", "CustomBattles", "CustomBattlesIoC.cs");
        var subModuleSource = ReadProjectSource("Main", "SubModule.cs");

        StringAssert.Contains(customBattleIocSource, "ICustomBattleMusicContextService customBattleMusicContext",
            "CustomBattlesIoC.InitializeHooks must accept the music selected-culture service.");
        StringAssert.Contains(customBattleIocSource, "CustomBattleSideVM_OnCultureSelection_Patch.Initialize(sideCommanderFilter, logger, customBattleMusicContext);",
            "CustomBattlesIoC.InitializeHooks must pass the music selected-culture service to the culture selection patch.");
        StringAssert.Contains(subModuleSource, "IoC.Resolve<ICustomBattleMusicContextService>()",
            "Main/SubModule.cs must resolve ICustomBattleMusicContextService when initializing Patch19 custom battle hooks.");
    }

    [TestMethod]
    public void MainModule_DeclaresTaomDependenciesLoadEdge()
    {
        var subModuleXml = ReadProjectSource("Main", "_Module", "SubModule.xml");

        StringAssert.Contains(subModuleXml, "<DependedModule Id=\"TAOM.Dependencies\" />",
            "Main/_Module/SubModule.xml must depend on TAOM.Dependencies before music relies on Harmony/MCM dependency behavior.");
        StringAssert.Contains(subModuleXml, "<DependedModuleMetadata id=\"TAOM.Dependencies\" order=\"LoadBeforeThis\" />",
            "Main/_Module/SubModule.xml must load TAOM.Dependencies before TAOM.");
    }

    private static string ReadProjectSource(params string[] relativeParts)
    {
        var path = Path.Combine(MusicTestPaths.RepositoryRootPath, Path.Combine(relativeParts));
        Assert.IsTrue(File.Exists(path), $"Project source file not found: {path}");
        return File.ReadAllText(path);
    }
}
