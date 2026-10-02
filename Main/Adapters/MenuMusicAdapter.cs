using System.Reflection;
using HarmonyLib;
using TaleWorlds.MountAndBlade;

namespace TAOM.Adapters;

/// <summary><see cref="IMenuMusicAdapter"/> over <see cref="MBMusicManager"/>, whose
/// <c>CurrentMode</c> setter is private (Kysaro's <c>MenuMusic.SetCurrentMode</c>, #704).</summary>
public sealed class MenuMusicAdapter : IMenuMusicAdapter
{
    private static readonly MethodInfo? CurrentModeSetter =
        AccessTools.PropertySetter(typeof(MBMusicManager), nameof(MBMusicManager.CurrentMode));

    public void MarkMenuModeWithoutTheme(object musicManager) => SetMode(musicManager, MusicMode.Menu);

    public void MarkPaused(object? musicManager) => SetMode(musicManager ?? MBMusicManager.Current, MusicMode.Paused);

    private static void SetMode(object? musicManager, MusicMode mode)
    {
        if (musicManager is MBMusicManager manager)
            CurrentModeSetter?.Invoke(manager, new object[] { mode });
    }
}
