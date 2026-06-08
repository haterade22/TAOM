using System;
using TAOM.Core.Logging;

namespace TAOM.Features.Music;

internal static class CharacterCreationMusicSmokeTrace
{
    private const string Prefix = "[Patch46-Music][CCSmoke]";

    private static Action<string> _sinkOverride;

    internal static void InitializeForTests(Action<string> sink)
    {
        _sinkOverride = sink;
    }

    internal static void ResetForTests()
    {
        _sinkOverride = null;
    }

    internal static void CultureConfirmed(string cultureId)
    {
        Log($"{Prefix} culture_confirmed culture={Clean(cultureId)}");
    }

    internal static void CultureSelected(string cultureId, string source)
    {
        Log($"{Prefix} culture_selected source={Clean(source)} culture={Clean(cultureId)}");
    }

    internal static void CharacterCreationBucketOwned(MusicPlaybackResult result)
    {
        Log(
            $"{Prefix} cc_bucket_owned outcome={result?.Outcome} " +
            $"culture={Clean(result?.Track?.CultureId ?? result?.Decision?.Pool?.ResolvedCultureId)} " +
            $"track={Clean(result?.Track?.EventName)} channel={result?.Channel ?? -1}");
    }

    internal static void VanillaAmbientSuppressed(MusicPlaybackResult result)
    {
        Log(
            $"{Prefix} vanilla_ambient_suppressed after_outcome={result?.Outcome} " +
            $"track={Clean(result?.Track?.EventName)}");
    }

    private static void Log(string message)
    {
        var sink = _sinkOverride;
        if (sink != null)
        {
            sink(message);
            return;
        }

        try
        {
            IoC.Resolve<IModLogger>()?.LogInfo(message);
        }
        catch
        {
        }
    }

    private static string Clean(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "none"
            : value.Trim().Replace(" ", "_");
    }
}
