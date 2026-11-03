using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace TAOM.Features.FordsOfWar.Autorun;

/// <summary>
/// What an unattended Fords of War run should do, read from a UTF-8 <c>key=value</c> arm file. Armed only by the environment variable
/// <c>TAOM_FORDS_AUTORUN</c> holding the file's absolute path: no file fallback, so a stray file can never take over a player's
/// session. Pure: the environment, the existence check and the read are injected. Never throws; a bad value becomes a warning and the
/// default, a set variable that cannot be used becomes <see cref="Problem"/> and leaves the plan unarmed.
/// </summary>
internal sealed class FordsAutorunPlan
{
    internal const string EnvVar = "TAOM_FORDS_AUTORUN";

    /// <summary>The launcher set the arm variable for this process, so nobody is at the keyboard.</summary>
    internal static bool ArmedInThisProcess => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(EnvVar));

    internal const double DefaultLiveSeconds = 600.0;
    internal const double DefaultLiveWaitSeconds = 600.0;
    internal const double DefaultWallLimitSeconds = 2400.0;

    public bool Armed { get; internal init; }

    /// <summary>Why a set variable could not arm the run; null when it was unset or the plan is armed.</summary>
    public string? Problem { get; internal init; }

    public IReadOnlyList<string> Warnings { get; internal init; } = Array.Empty<string>();

    public string ArmPath { get; internal init; } = "";

    public string? West { get; internal init; }

    public string? East { get; internal init; }

    public string? Ai { get; internal init; }

    public string? Scene { get; internal init; }

    /// <summary>Mission seconds the match runs before the autorun ends it.</summary>
    public double LiveSeconds { get; internal init; } = DefaultLiveSeconds;

    /// <summary>Wall seconds after the start the run may take to go live.</summary>
    public double LiveWaitSeconds { get; internal init; } = DefaultLiveWaitSeconds;

    /// <summary>Wall seconds after the start at which the match is ended whatever the mission clock says.</summary>
    public double WallLimitSeconds { get; internal init; } = DefaultWallLimitSeconds;

    /// <summary>Live seconds at which to save a picture of the screen into the out folder, in rising order; none by default.</summary>
    public IReadOnlyList<double> ScreenshotSeconds { get; internal init; } = Array.Empty<double>();

    /// <summary>The folder the done and status files are written to.</summary>
    public string OutFolder { get; internal init; } = "";

    public bool QuitWhenDone { get; internal init; }

    /// <summary>An AI commands the West side too (it is the human slot otherwise), so an unattended match plays both sides.</summary>
    public bool WestAi { get; internal init; }

    /// <summary>The plan the running game uses: the real environment and file system.</summary>
    internal static FordsAutorunPlan Resolve() =>
        Resolve(Environment.GetEnvironmentVariable, File.Exists, path => File.ReadAllText(path, Encoding.UTF8));

    internal static FordsAutorunPlan Resolve(Func<string, string?> env, Func<string, bool> exists, Func<string, string> read)
    {
        var path = env(EnvVar);
        if (string.IsNullOrWhiteSpace(path))
            return new FordsAutorunPlan();

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

        return FordsAutorunArmFile.Parse(path, text);
    }

    /// <summary>One line for the log: the choices the run will use.</summary>
    internal string Describe() =>
        "west=" + (West ?? "-") + " east=" + (East ?? "-") + " ai=" + (Ai ?? "-") + " scene=" + (Scene ?? "-")
        + " live_seconds=" + Num(LiveSeconds) + " live_wait_seconds=" + Num(LiveWaitSeconds) + " wall_limit_seconds=" + Num(WallLimitSeconds)
        + " out=" + OutFolder + " quit=" + (QuitWhenDone ? 1 : 0) + " west_ai=" + (WestAi ? 1 : 0) + " arm=" + ArmPath;

    internal static string Num(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    internal static FordsAutorunPlan Unarmed(string problem) => new() { Problem = problem };
}
