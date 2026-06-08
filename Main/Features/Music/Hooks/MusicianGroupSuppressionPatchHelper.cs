using TAOM.Adapters;

namespace TAOM.Features.Music.Hooks;

internal static class MusicianGroupSuppressionPatchHelper
{
    // Lazily cached on first use. ??= is safe here: DryIoC singletons are idempotent,
    // reference assignments are atomic in .NET, and Bannerlord fires these patches on the main thread.
    private static IMusicianGroupSuppressionService _suppressionService;
    private static IMusicianGroupSuppressionAdapter _suppressionAdapter;
    private static IMusicTavernContextSource _tavernContextSource;

    public static bool ShouldSuppressVanillaTavernMusic(object musicianGroup)
    {
        try
        {
            MarkTavernContextActive();

            var service = _suppressionService ??= IoC.Resolve<IMusicianGroupSuppressionService>();
            if (service == null || !service.ShouldSuppressVanillaTavernMusic())
                return false;

            (_suppressionAdapter ??= IoC.Resolve<IMusicianGroupSuppressionAdapter>())?.StopAndReleaseVanillaTrack(musicianGroup);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void MarkTavernContextActive()
    {
        try
        {
            (_tavernContextSource ??= IoC.Resolve<IMusicTavernContextSource>())?.EnterTavernContext();
        }
        catch
        {
        }
    }

    public static void MarkTavernContextInactive()
    {
        try
        {
            (_tavernContextSource ??= IoC.Resolve<IMusicTavernContextSource>())?.ExitTavernContext();
        }
        catch
        {
        }
    }
}
