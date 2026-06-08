using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.Music;

namespace TAOM.Tests.Features.Music;

[TestClass]
public class MusicIoCTests
{
    [TestMethod]
    public void RegisterMusicFeature_RegistersPureMusicServices()
    {
        var container = new Container();
        container.RegisterInstance<IPathService>(new TestPathService());
        container.RegisterInstance(Substitute.For<IModLogger>());

        MusicIoC.RegisterMusicFeature(container);

        Assert.IsNotNull(container.Resolve<IMusicEngineAdapter>());
        Assert.IsNotNull(container.Resolve<IMusicMissionContextSource>());
        Assert.IsNotNull(container.Resolve<IMusicCampaignContextSource>());
        Assert.IsNotNull(container.Resolve<IMusicMissionContextAdapter>());
        Assert.IsNotNull(container.Resolve<IMusicCampaignContextAdapter>());
        Assert.IsNotNull(container.Resolve<IMusicSettingsProvider>());
        Assert.IsNotNull(container.Resolve<MusicTransitionResolver>());
        Assert.IsNotNull(container.Resolve<NoRepeatShufflePicker>());
        Assert.IsNotNull(container.Resolve<MusicTrackIndex>());
        Assert.IsNotNull(container.Resolve<IMusicPlaybackService>());
        Assert.IsNotNull(container.Resolve<ICharacterCreationMusicContextService>());
        Assert.IsNotNull(container.Resolve<ICustomBattleMusicContextService>());
    }

    private sealed class TestPathService : IPathService
    {
        public string ModuleRootPath => MusicTestPaths.ModuleRootPath;

        public string ModuleDataPath => MusicTestPaths.ModuleDataPath;

        public string ConfigPath => System.IO.Path.Combine(MusicTestPaths.ModuleDataPath, "configs");
    }
}
