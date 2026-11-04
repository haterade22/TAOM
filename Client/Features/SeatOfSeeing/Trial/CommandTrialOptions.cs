using System;
using System.Globalization;
using System.IO;
using TAOM.Core.Logging;

namespace TAOM.Client.Features.SeatOfSeeing.Trial;

/// <summary>The field trial's settings (build-spec section 12), from the UTF-8 key=value file that the environment variable <c>TAOM_COMMAND_TRIAL</c> names, which <see cref="FromEnvironment"/> reads once per process.</summary>
internal sealed class CommandTrialOptions
{
    internal const string EnvVar = "TAOM_COMMAND_TRIAL";
    internal const string DefaultScene = "battle_terrain_a";
    internal const float DefaultLiveLimitSeconds = 420f;

    public string OutFolder { get; private set; } = "";
    public bool Quit { get; private set; }
    public string Scene { get; private set; } = DefaultScene;
    public float LiveLimitSeconds { get; private set; } = DefaultLiveLimitSeconds;

    /// <summary>The comma list of steps as written, or null for every step, read by <c>SeatTrialSteps.TryChoose</c>.</summary>
    public string? Steps { get; private set; }

    /// <summary>The armed options, or null when the variable is unset, names no file, its file cannot be read, or it holds no absolute <c>out</c> folder.</summary>
    internal static CommandTrialOptions? FromEnvironment(IModLogger logger) =>
        Parse(Environment.GetEnvironmentVariable(EnvVar), File.Exists, File.ReadAllLines, logger);

    internal static CommandTrialOptions? Parse(string? path, Func<string, bool> exists, Func<string, string[]> readLines, IModLogger logger)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;
        if (!exists(path!))
        {
            logger.LogWarning($"[SeatTrial] not armed: no file at {path}");
            return null;
        }

        string[] lines;
        try
        {
            lines = readLines(path!);
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
        {
            logger.LogWarning($"[SeatTrial] not armed: cannot read {path}: {ex.Message}");
            return null;
        }

        var options = new CommandTrialOptions();
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            var eq = line.IndexOf('=');
            if (line.Length == 0 || line[0] == '#' || eq <= 0)
                continue;
            options.Set(line.Substring(0, eq).Trim().ToLowerInvariant(), line.Substring(eq + 1).Trim());
        }

        if (options.OutFolder.Length == 0)
        {
            logger.LogWarning($"[SeatTrial] not armed: {path} has no out folder, and the done file needs one");
            return null;
        }

        if (!Path.IsPathRooted(options.OutFolder))
        {
            logger.LogWarning($"[SeatTrial] not armed: out must be an absolute folder, not '{options.OutFolder}'");
            return null;
        }

        return options;
    }

    private void Set(string key, string value)
    {
        switch (key)
        {
            case "out": OutFolder = value; break;
            case "quit": Quit = value == "1"; break;
            case "scene": Scene = value.Length > 0 ? value : Scene; break;
            case "live_limit_seconds": LiveLimitSeconds = Float(value, LiveLimitSeconds); break;
            case "steps": Steps = value.Length > 0 ? value : null; break;
        }
    }

    private static float Float(string text, float fallback) =>
        float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && value > 0f && value < 100000f ? value : fallback;

    public override string ToString() =>
        $"scene={Scene} live_limit={LiveLimitSeconds.ToString(CultureInfo.InvariantCulture)} steps={Steps ?? "all"} quit={Quit} out={OutFolder}";
}
