using System;
using TAOM.Core.Logging;

namespace TAOM.Features.Music;

internal static class MusicRuntimeSmokeTrace
{
    private const string Prefix = "[Patch46-Music][RuntimeSmoke]";
    private static readonly object Sync = new object();

    private static Action<string> _sinkOverride;
    private static string _lastMessage;

    internal static void InitializeForTests(Action<string> sink)
    {
        lock (Sync)
        {
            _sinkOverride = sink;
            _lastMessage = null;
        }
    }

    internal static void ResetForTests()
    {
        lock (Sync)
        {
            _sinkOverride = null;
            _lastMessage = null;
        }
    }

    internal static void PlaybackResult(string source, MusicPlaybackResult result)
    {
        var message =
            $"{Prefix} source={Clean(source)} outcome={result?.Outcome} " +
            $"bucket={Clean((result?.Track?.Bucket ?? result?.Decision?.Bucket).ToString())} " +
            $"culture={Clean(result?.Track?.CultureId ?? result?.Decision?.Pool?.ResolvedCultureId)} " +
            $"track={Clean(result?.Track?.EventName)} channel={result?.Channel ?? -1} " +
            $"reason={Clean(result?.Reason ?? result?.Decision?.Reason)}";

        LogDeduped(message);
    }

    private static void LogDeduped(string message)
    {
        Action<string> sink;
        lock (Sync)
        {
            if (message == _lastMessage)
                return;

            _lastMessage = message;
            sink = _sinkOverride;
        }

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
