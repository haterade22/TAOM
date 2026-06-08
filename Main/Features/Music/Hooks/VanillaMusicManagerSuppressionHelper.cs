using TaleWorlds.MountAndBlade;

namespace TAOM.Features.Music.Hooks;

internal static class VanillaMusicManagerSuppressionHelper
{
    private static IMusicSettingsProvider _settingsProvider;

    /// <summary>
    /// Returns true when TAOM music is enabled and should suppress vanilla psai.
    /// Lazily resolves the settings provider on first call (DryIoC singleton, safe).
    /// </summary>
    public static bool IsTaomMusicActive()
    {
        try
        {
            var provider = _settingsProvider ??= IoC.Resolve<IMusicSettingsProvider>();
            return provider != null && provider.GetSnapshot().MusicEnabled;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Stops any currently running psai theme. Call once on session launch so the
    /// psai channel is released before TAOM's first tick tries to claim it.
    /// </summary>
    public static void StopVanillaPsaiMusic()
    {
        try
        {
            MBMusicManager.Current?.ForceStopThemeWithFadeOut();
        }
        catch
        {
        }
    }
}
