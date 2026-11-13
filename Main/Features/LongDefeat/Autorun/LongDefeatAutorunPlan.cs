using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using TAOM.Core.Validation;
using TAOM.Features.FordsOfWar.Domain;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Features.LongDefeat.Autorun;

/// <summary>
/// What an unattended Long Defeat run should do, read from a UTF-8 <c>key=value</c> arm file. Armed only by the environment variable
/// <c>TAOM_LONGDEFEAT_AUTORUN</c> holding the file's absolute path: no file fallback, so a stray file can never take over a player's
/// session. Pure: the environment, the existence check and the read are injected. Never throws; a bad value becomes a warning and the
/// default, a set variable that cannot be used becomes <see cref="Problem"/> and leaves the plan unarmed.
/// </summary>
internal sealed class LongDefeatAutorunPlan
{
    internal const string EnvVar = "TAOM_LONGDEFEAT_AUTORUN";

    /// <summary>The launcher set the arm variable for this process, so nobody is at the keyboard.</summary>
    internal static bool ArmedInThisProcess => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(EnvVar));

    internal const double DefaultLiveLimitSeconds = 3600.0;
    internal const double DefaultWallLimitSeconds = 3900.0;
    private const float MaxGarrisonScale = 10f;
    private const int MaxStartWave = 100;
    private const int MaxStartRerolls = 3;

    public bool Armed { get; internal init; }

    /// <summary>Why a set variable could not arm the run; null when it was unset or the plan is armed.</summary>
    public string? Problem { get; internal init; }

    public IReadOnlyList<string> Warnings { get; internal init; } = Array.Empty<string>();

    public string ArmPath { get; internal init; } = "";

    /// <summary>The fortress row's id, or null for the first shipped one.</summary>
    public string? Fortress { get; internal init; }

    /// <summary>The wave target, or null to play until the run is lost.</summary>
    public int? Waves { get; internal init; }

    public float GarrisonScale { get; internal init; } = 1f;

    /// <summary>The seed every roll derives from, 0 to 2^53-1; null takes the start time's ticks.</summary>
    public ulong? Seed { get; internal init; }

    /// <summary>A test option, 1 to 100: the run begins at this wave number.</summary>
    public int StartWave { get; internal init; } = 1;

    /// <summary>A test option, 0 to 3: boon rerolls in the bank at the start.</summary>
    public int StartRerolls { get; internal init; }

    /// <summary>A test option, 0 to 100000: the gold the run starts with instead of the shop's, and the sign the unattended bot buys fortifications before packs (phase3-plan.md D27). Null keeps the shop's.</summary>
    public int? StartGold { get; internal init; }

    /// <summary>A test option, 1 to 100 (#1160): at this clear, when a wave follows it, every ram and siege tower that stands is destroyed, so the next wave proves they come back. Null breaks nothing.</summary>
    public int? BreakEngines { get; internal init; }

    /// <summary>The class id the run asks for. Not judged here: the catalog takes it when the fortress offers it, else warns and plays the seed's draw, and the report names the class that was played.</summary>
    public string? Class { get; internal init; }

    /// <summary>What the launcher expects ("lost" for a run built to lose); the game only echoes it into the done file.</summary>
    public string? Expect { get; internal init; }

    /// <summary>Mission seconds the run may take before the autorun leaves it.</summary>
    public double LiveLimitSeconds { get; internal init; } = DefaultLiveLimitSeconds;

    /// <summary>Wall seconds after the start at which the run is ended whatever the mission clock says.</summary>
    public double WallLimitSeconds { get; internal init; } = DefaultWallLimitSeconds;

    /// <summary>Live seconds at which to save a picture of the screen into the out folder, in rising order.</summary>
    public IReadOnlyList<double> ScreenshotSeconds { get; internal init; } = Array.Empty<double>();

    /// <summary>The folder the done and status files, and the run's best-run file, are written to.</summary>
    public string OutFolder { get; internal init; } = "";

    public bool QuitWhenDone { get; internal init; }

    /// <summary>The mission speed from the first frame: 1, 2, 4 or 10.</summary>
    public int Speed { get; internal init; } = 1;

    internal static LongDefeatAutorunPlan Resolve() =>
        Resolve(Environment.GetEnvironmentVariable, File.Exists, path => File.ReadAllText(path, Encoding.UTF8));

    internal static LongDefeatAutorunPlan Resolve(Func<string, string?> env, Func<string, bool> exists, Func<string, string> read)
    {
        var path = env(EnvVar);
        if (string.IsNullOrWhiteSpace(path))
            return new LongDefeatAutorunPlan();

        path = path!.Trim();
        if (!exists(path))
            return Unarmed(EnvVar + " points to a missing file: " + path);

        string text;
        try
        {
            text = read(path);
        }
        catch (Exception ex)
        {
            return Unarmed("the arm file " + path + " could not be read: " + ex.GetType().Name + ": " + ex.Message);
        }

        return Parse(path, text);
    }

    /// <summary>The setup the driver hands the start path. The out folder doubles as the records folder, so an unattended run never touches the player's best run.</summary>
    internal LongDefeatSetup ToSetup(string fortressId) =>
        new(fortressId, "autorun", new RunOptions { StopAfterWaves = Waves, Autoplay = true, GarrisonScale = GarrisonScale, Seed = Seed, StartWave = StartWave, StartRerolls = StartRerolls, StartGold = StartGold, BreakEnginesAtClear = BreakEngines, ClassId = Class }, OutFolder);

    /// <summary>One line for the log: the choices the run will use.</summary>
    internal string Describe() =>
        "fortress=" + (Fortress ?? "-") + " waves=" + (Waves?.ToString(CultureInfo.InvariantCulture) ?? "-")
        + " garrison_scale=" + Num(GarrisonScale) + " class=" + (Class ?? "-") + " seed=" + (Seed?.ToString(CultureInfo.InvariantCulture) ?? "-")
        + " start_wave=" + StartWave + " start_rerolls=" + StartRerolls + " start_gold=" + (StartGold?.ToString(CultureInfo.InvariantCulture) ?? "-") + " break_engines=" + (BreakEngines?.ToString(CultureInfo.InvariantCulture) ?? "-") + " live_limit_seconds=" + Num(LiveLimitSeconds) + " wall_limit_seconds=" + Num(WallLimitSeconds)
        + " speed=" + Speed + " out=" + OutFolder + " quit=" + (QuitWhenDone ? 1 : 0) + " expect=" + (Expect ?? "-") + " arm=" + ArmPath;

    internal static string Num(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    internal static LongDefeatAutorunPlan Unarmed(string problem) => new() { Problem = problem };

    private static LongDefeatAutorunPlan Parse(string path, string text)
    {
        var warnings = new List<string>();
        string? fortress = null, expect = null, outFolder = null;
        int? waves = null;
        var scale = 1f;
        string? className = null;
        ulong? seed = null;
        var startWave = 1;
        var startRerolls = 0;
        int? startGold = null;
        int? breakEngines = null;
        double liveLimit = DefaultLiveLimitSeconds, wallLimit = DefaultWallLimitSeconds;
        var quit = false;
        var speed = 1;
        var screenshots = new List<double>();

        foreach (var raw in text.TrimStart('\uFEFF').Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line[0] == '#')
                continue;

            var split = line.IndexOf('=');
            if (split <= 0)
            {
                warnings.Add("ignored line without a key: " + line);
                continue;
            }

            var key = line.Substring(0, split).Trim().ToLowerInvariant();
            var value = line.Substring(split + 1).Trim();
            switch (key)
            {
                case "fortress": fortress = value; break;
                case "waves": waves = WaveTarget(key, value, warnings); break;
                case "out": outFolder = value; break;
                case "expect": expect = value; break;
                case "live_limit_seconds": liveLimit = Seconds(key, value, liveLimit, warnings); break;
                case "wall_limit_seconds": wallLimit = Seconds(key, value, wallLimit, warnings); break;
                case "quit": quit = Flag(key, value, quit, warnings); break;
                case "garrison_scale": scale = Scale(key, value, scale, warnings); break;
                case "speed": speed = SpeedLevel(key, value, speed, warnings); break;
                case "class": className = value.Length == 0 ? null : value; break;
                case "seed": seed = SeedValue(key, value, warnings); break;
                case "start_wave": startWave = IntInRange(key, value, 1, MaxStartWave, startWave, warnings); break;
                case "start_rerolls": startRerolls = IntInRange(key, value, 0, MaxStartRerolls, startRerolls, warnings); break;
                case "start_gold": startGold = StartGoldValue(key, value, warnings); break;
                case "break_engines": breakEngines = BreakEnginesValue(key, value, warnings); break;
                case "screenshots": screenshots = SecondsList(key, value, warnings); break;
                default: warnings.Add("unknown key '" + key + "' ignored"); break;
            }
        }

        if (string.IsNullOrEmpty(outFolder) || !IsAbsolute(outFolder!))
            return Unarmed("the arm file " + path + " needs an absolute out=<folder> for the done file");

        return new LongDefeatAutorunPlan
        {
            Armed = true, Warnings = warnings, ArmPath = path, Fortress = fortress, Waves = waves, OutFolder = outFolder!, Expect = expect,
            LiveLimitSeconds = liveLimit, WallLimitSeconds = wallLimit, QuitWhenDone = quit, GarrisonScale = scale, ScreenshotSeconds = screenshots, Speed = speed,
            Seed = seed, StartWave = startWave, StartRerolls = startRerolls, StartGold = startGold, BreakEngines = breakEngines, Class = className,
        };
    }

    // .NET Framework's Path.IsPathRooted throws on a quote, <, > or |; such a value is no folder.
    private static bool IsAbsolute(string folder)
    {
        try
        {
            return Path.IsPathRooted(folder);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static int? WaveTarget(string key, string value, List<string> warnings)
    {
        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var waves) && waves > 0)
            return waves;

        warnings.Add(key + "='" + value + "' is not a positive whole number; the run plays until it is lost");
        return null;
    }

    private static double Seconds(string key, string value, double fallback, List<string> warnings)
    {
        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) && FiniteFloatValidator.IsFinite(seconds) && seconds > 0.0)
            return seconds;

        warnings.Add(key + "='" + value + "' is not a positive number; using " + Num(fallback));
        return fallback;
    }

    private static float Scale(string key, string value, float fallback, List<string> warnings)
    {
        if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var scale) && FiniteFloatValidator.IsFiniteInRange(scale, 0.01f, MaxGarrisonScale))
            return scale;

        warnings.Add(key + "='" + value + "' is not a number from 0.01 to " + Num(MaxGarrisonScale) + "; using " + Num(fallback));
        return fallback;
    }

    private static int SpeedLevel(string key, string value, int fallback, List<string> warnings)
    {
        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var speed) && FordsGameSpeed.IsLevel(speed))
            return speed;

        warnings.Add(key + "='" + value + "' is not 1, 2, 4 or 10; using " + fallback);
        return fallback;
    }

    /// <summary>A decimal from 0 to 2^53-1 (so a JavaScript reader of the report keeps every digit). A sign, a point, a hex prefix or a larger number is refused, and the run takes the clock's ticks instead.</summary>
    private static ulong? SeedValue(string key, string value, List<string> warnings)
    {
        if (ulong.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var seed) && seed <= RunOptions.MaxSeed)
            return seed;

        warnings.Add(key + "='" + value + "' is not a whole number from 0 to 9007199254740991; the run takes the start time's ticks");
        return null;
    }

    /// <summary>A whole number from 0 to <see cref="RunOptions.MaxStartGold"/>; anything else is refused and the run keeps the shop's gold.</summary>
    private static int? StartGoldValue(string key, string value, List<string> warnings)
    {
        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var gold) && gold >= 0 && gold <= RunOptions.MaxStartGold)
            return gold;

        warnings.Add(key + "='" + value + "' is not a whole number from 0 to " + RunOptions.MaxStartGold + "; the run keeps the shop's gold");
        return null;
    }

    /// <summary>A clear from 1 to <see cref="MaxStartWave"/>; anything else is refused and no engine is broken.</summary>
    private static int? BreakEnginesValue(string key, string value, List<string> warnings)
    {
        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var clear) && clear >= 1 && clear <= MaxStartWave)
            return clear;

        warnings.Add(key + "='" + value + "' is not a clear from 1 to " + MaxStartWave + "; no engine is broken");
        return null;
    }

    private static int IntInRange(string key, string value, int min, int max, int fallback, List<string> warnings)
    {
        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) && number >= min && number <= max)
            return number;

        warnings.Add(key + "='" + value + "' is not a whole number from " + min + " to " + max + "; using " + fallback);
        return fallback;
    }

    private static List<double> SecondsList(string key, string value, List<string> warnings)
    {
        var seconds = new List<double>();
        foreach (var part in value.Split(','))
        {
            if (double.TryParse(part.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var number) && FiniteFloatValidator.IsFinite(number) && number > 0.0)
                seconds.Add(number);
            else
                warnings.Add(key + " ignored '" + part + "': not a positive number");
        }

        seconds.Sort();
        return seconds;
    }

    private static bool Flag(string key, string value, bool fallback, List<string> warnings)
    {
        if (value == "1" || value.Equals("true", StringComparison.OrdinalIgnoreCase))
            return true;
        if (value == "0" || value.Equals("false", StringComparison.OrdinalIgnoreCase))
            return false;

        warnings.Add(key + "='" + value + "' is not 0 or 1; using " + (fallback ? 1 : 0));
        return fallback;
    }
}
