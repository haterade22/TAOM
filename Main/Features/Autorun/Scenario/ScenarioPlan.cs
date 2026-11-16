using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace TAOM.Features.Autorun.Scenario;

/// <summary>
/// What an unattended campaign run should do (#1075), read from a UTF-8 <c>key=value</c> arm file. Armed only by the environment
/// variable <c>TAOM_AUTORUN</c> holding the file's absolute path: no file fallback, so a stray file can never take over a player's
/// session. Pure: the environment, the existence check and the read are injected. Never throws; a bad value becomes a warning and
/// the default, a set variable that cannot be used becomes <see cref="Problem"/> and leaves the plan unarmed.
/// </summary>
internal sealed class ScenarioPlan
{
    internal const string EnvVar = "TAOM_AUTORUN";
    internal const string KindCampaignNew = "campaign-new";
    internal const string KindStoryMission = "story-mission";
    internal const string DefaultCulture = "gondor";
    internal const int DefaultDays = 3;
    internal const int MaxDays = 1000;
    internal const float DefaultSpeed = 15f;
    internal const float MaxSpeed = 30f;
    internal const double DefaultStallSeconds = 600.0;
    internal const double DefaultWallLimitSeconds = 3600.0;
    internal const double DefaultMissionLimitSeconds = 1200.0;

    // Each of these also drives the game from the main menu (CommandTrialAutostart is the Seat's field trial), so two would both start one.
    private static readonly string[] OtherEnvVars = { "TAOM_FORDS_AUTORUN", "TAOM_LONGDEFEAT_AUTORUN", "TAOM_COMMAND_TRIAL" };

    public bool Armed { get; internal init; }

    /// <summary>Why a set variable could not arm the run; null when it was unset or the plan is armed.</summary>
    public string? Problem { get; internal init; }

    public IReadOnlyList<string> Warnings { get; internal init; } = Array.Empty<string>();

    public string ArmPath { get; internal init; } = "";

    public string Kind { get; internal init; } = "";

    /// <summary>The folder the done and status files are written to.</summary>
    public string OutFolder { get; internal init; } = "";

    /// <summary>A free label, echoed in the log and the done file.</summary>
    public string Scenario { get; internal init; } = "-";

    /// <summary>The story mission id the <see cref="KindStoryMission"/> kind opens; "-" for any other kind.</summary>
    public string MissionId { get; internal init; } = "-";

    /// <summary>How long the story mission may run before the driver gives up on it.</summary>
    public double MissionLimitSeconds { get; internal init; } = DefaultMissionLimitSeconds;

    /// <summary>The culture StringId; the game judges it at start, not the plan.</summary>
    public string Culture { get; internal init; } = DefaultCulture;

    public int Days { get; internal init; } = DefaultDays;

    public float Speed { get; internal init; } = DefaultSpeed;

    public double StallSeconds { get; internal init; } = DefaultStallSeconds;

    public double WallLimitSeconds { get; internal init; } = DefaultWallLimitSeconds;

    public bool Quit { get; internal init; }

    internal static ScenarioPlan Resolve() =>
        Resolve(Environment.GetEnvironmentVariable, File.Exists, path => File.ReadAllText(path, Encoding.UTF8));

    internal static ScenarioPlan Resolve(Func<string, string?> env, Func<string, bool> exists, Func<string, string> read)
    {
        var path = env(EnvVar);
        if (string.IsNullOrWhiteSpace(path))
            return new ScenarioPlan();

        foreach (var other in OtherEnvVars)
            if (!string.IsNullOrWhiteSpace(env(other)))
                return Unarmed("two autorun variables are set: " + EnvVar + " and " + other);

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

        return ScenarioArmFile.Parse(path, text);
    }

    /// <summary>One line for the log: every value the run will use.</summary>
    internal string Describe() =>
        "kind=" + Kind + " scenario=" + Scenario + " mission=" + MissionId + " mission_limit_seconds=" + Num(MissionLimitSeconds) + " culture=" + Culture + " days=" + Days + " speed=" + Num(Speed)
        + " stall_seconds=" + Num(StallSeconds) + " wall_limit_seconds=" + Num(WallLimitSeconds) + " quit=" + (Quit ? 1 : 0)
        + " out=" + OutFolder + " arm=" + ArmPath;

    internal static string Num(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    internal static ScenarioPlan Unarmed(string problem) => new() { Problem = problem };
}
