using DryIoc;
using TAOM.Adapters;
using TAOM.Core.Infrastructure;
using TAOM.Features.Music.Hooks;

namespace TAOM.Features.Music;

public static class MusicIoC
{
    public static void RegisterMusicFeature(IContainer container)
    {
        container.Register<IMusicEngineAdapter, MusicEngineAdapter>(Reuse.Singleton);
        container.Register<IMusicTavernContextSource, MusicTavernContextSource>(Reuse.Singleton);
        container.Register<IMusicMissionContextSource, TaleWorldsMusicMissionContextSource>(Reuse.Singleton);
        container.Register<IMusicCampaignContextSource, TaleWorldsMusicCampaignContextSource>(Reuse.Singleton);
        container.Register<IMusicMissionContextAdapter, MusicMissionContextAdapter>(Reuse.Singleton);
        container.Register<IMusicCampaignContextAdapter, MusicCampaignContextAdapter>(Reuse.Singleton);
        container.Register<IMusicSettingsProvider, MusicSettingsProvider>(Reuse.Singleton);
        container.Register<ICharacterCreationMusicContextService, CharacterCreationMusicContextService>(Reuse.Singleton);
        container.Register<ICustomBattleMusicContextService, CustomBattleMusicContextService>(Reuse.Singleton);
        container.Register<IMusicianGroupSuppressionAdapter, MusicianGroupSuppressionAdapter>(Reuse.Singleton);
        container.Register<IMusicianGroupSuppressionService, MusicianGroupSuppressionService>(Reuse.Singleton);
        container.Register<MusicCampaignBehavior>(Reuse.Singleton);
        container.Register<MusicTransitionResolver>(Reuse.Singleton);
        container.Register<NoRepeatShufflePicker>(Reuse.Singleton);
        container.Register<IMusicPlaybackService, MusicPlaybackService>(Reuse.Singleton);
        container.RegisterDelegate<MusicTrackIndex>(
            resolver => MusicTrackIndex.LoadFromModuleRoot(resolver.Resolve<IPathService>().ModuleRootPath),
            Reuse.Singleton);
    }
}
