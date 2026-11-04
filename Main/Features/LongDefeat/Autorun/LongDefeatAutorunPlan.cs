using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using TAOM.Core.Validation;
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
        new(fortressId, "autorun", new RunOptions { StopAfterWaves = Waves, Autoplay = true, GarrisonScale = GarrisonScale }, OutFolder);

    /// <summary>One line for the log: the choices the run will use.</summary>
    internal string Describe() =>
        "fortress=" + (Fortress ?? "-") + " waves=" + (Waves?.ToString(CultureInfo.InvariantCulture) ?? "-")
        + " garrison_scale=" + Num(GarrisonScale) + " live_limit_seconds=" + Num(LiveLimitSeconds) + " wall_limit_seconds=" + Num(WallLimitSeconds)
        + " out=" + OutFolder + " quit=" + (QuitWhenDone ? 1 : 0) + " expect=" + (Expect ?? "-") + " arm=" + ArmPath;

    internal static string Num(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    internal static LongDefeatAutorunPlan Unarmed(string problem) => new() { Problem = problem };

    private static LongDefeatAutorunPlan Parse(string path, string text)
    {
        var warnings = new List<string>();
        string? fortress = null, expect = null, outFolder = null;
        int? waves = null;
        var scale = 1f;
        double liveLimit = DefaultLiveLimitSeconds, wallLimit = DefaultWallLimitSeconds;
        var quit = false;
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
                case "screenshots": screenshots = SecondsList(key, value, warnings); break;
                default: warnings.Add("unknown key '" + key + "' ignored"); break;
            }
        }

        if (string.IsNullOrEmpty(outFolder) || !IsAbsolute(outFolder!))
            return Unarmed("the arm file " + path + " needs an absolute out=<folder> for the done file");

        return new LongDefeatAutorunPlan
        {
            Armed = true, Warnings = warnings, ArmPath = path, Fortress = fortress, Waves = waves, OutFolder = outFolder!, Expect = expect,
            LiveLimitSeconds = liveLimit, WallLimitSeconds = wallLimit, QuitWhenDone = quit, GarrisonScale = scale, ScreenshotSeconds = screenshots,
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
