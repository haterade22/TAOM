using System;
using System.Globalization;
using System.IO;
using TAOM.Core.Logging;

namespace TAOM.Features.LongDefeat.Spike;

/// <summary>Phase 0 spike settings (#1079), from the console or from the UTF-8 key=value file that <c>TAOM_LONGDEFEAT_SPIKE</c> names; a player session never sets it.</summary>
internal sealed class LongDefeatSpikeOptions
{
    internal const string EnvVar = "TAOM_LONGDEFEAT_SPIKE";

    public string Fortress { get; set; } = "helms";
    public int Waves { get; set; } = 3;

    // TacticBreachWalls stops using machines at 100 attackers or fewer (DetermineCurrentLanes), so the default wave must be larger or S3 sees idle engines.
    public int WaveSize { get; set; } = 120;
    public int Garrison { get; set; } = 100;
    public float IntermissionSeconds { get; set; } = 15f;
    public float LiveLimitSeconds { get; set; } = 900f;
    public bool DefendersAi { get; set; } = true;
    public bool Quit { get; set; }
    public string? OutFolder { get; set; }

    /// <summary>The armed options, or null when the variable is unset, names no file, or its file cannot be read.</summary>
    public static LongDefeatSpikeOptions? FromEnvironment(IModLogger logger) =>
        Parse(Environment.GetEnvironmentVariable(EnvVar), File.Exists, File.ReadAllLines, logger);

    internal static LongDefeatSpikeOptions? Parse(string? path, Func<string, bool> exists, Func<string, string[]> readLines, IModLogger logger)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;
        if (!exists(path!))
        {
            logger.LogWarning($"[LongDefeat][Spike] not armed: no file at {path}");
            return null;
        }

        string[] lines;
        try
        {
            lines = readLines(path!);
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
        {
            logger.LogWarning($"[LongDefeat][Spike] not armed: cannot read {path}: {ex.Message}");
            return null;
        }

        var options = new LongDefeatSpikeOptions();
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            var eq = line.IndexOf('=');
            if (line.Length == 0 || line[0] == '#' || eq <= 0)
                continue;
            options.Set(line.Substring(0, eq).Trim().ToLowerInvariant(), line.Substring(eq + 1).Trim());
        }

        return options;
    }

    private void Set(string key, string value)
    {
        switch (key)
        {
            case "fortress": Fortress = value; break;
            case "waves": Waves = Int(value, Waves); break;
            case "wave_size": WaveSize = Int(value, WaveSize); break;
            case "garrison": Garrison = Int(value, Garrison); break;
            case "intermission_seconds": IntermissionSeconds = Float(value, IntermissionSeconds); break;
            case "live_limit_seconds": LiveLimitSeconds = Float(value, LiveLimitSeconds); break;
            case "defenders_ai": DefendersAi = value == "1"; break;
            case "quit": Quit = value == "1"; break;
            case "out": OutFolder = value; break;
        }
    }

    private static int Int(string text, int fallback) =>
        int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value > 0 ? value : fallback;

    private static float Float(string text, float fallback) =>
        float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && value > 0f && value < 100000f ? value : fallback;

    public override string ToString() =>
        $"fortress={Fortress} waves={Waves} wave_size={WaveSize} garrison={Garrison} intermission={IntermissionSeconds} limit={LiveLimitSeconds} "
        + $"defenders_ai={DefendersAi} quit={Quit} out={OutFolder ?? "none"}";
}
