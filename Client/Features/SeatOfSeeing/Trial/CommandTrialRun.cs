using System;
using System.Linq;
using TAOM.Core.Logging;
using TAOM.Features.SeatOfSeeing;
using TAOM.Features.SeatOfSeeing.Domain;

namespace TAOM.Client.Features.SeatOfSeeing.Trial;

/// <summary>What the process knows about the field trial (build-spec section 12): whether it is armed, how the run ended, and the script a mission gets.</summary>
internal static class CommandTrialRun
{
    /// <summary>The armed options, or null (every player session).</summary>
    internal static CommandTrialOptions? Options { get; private set; }

    internal static bool IsArmed => Options != null;

    /// <summary>The reason the run ended (the first one set wins), or null while it runs. The autostart turns it into the done file.</summary>
    internal static string? Outcome { get; private set; }

    /// <summary>The step the script is on, for the status file.</summary>
    internal static string Step { get; private set; } = "none";

    internal static void Arm(CommandTrialOptions options) => Options = options;

    /// <summary>The trial's own settings of the latest mission, which a step changes through the host; null until a mission asks for them.</summary>
    internal static CommandTrialSettings? TrialSettings { get; private set; }

    /// <summary>The settings a mission's session gets: the trial's own when armed, else the plain provider itself.</summary>
    internal static ISeatSettingsProvider Settings(ISeatSettingsProvider plain) => IsArmed ? TrialSettings = new CommandTrialSettings() : plain;

    internal static void Finish(string reason) => Outcome ??= reason;

    internal static void OnStep(string step) => Step = step;

    /// <summary>The script for a mission, or null when the trial is not armed or its run has ended; a refused step list ends the run as <c>start_refused</c> and a throwing setup as <c>script_failed</c>, never as a throw into the mission's first tick.</summary>
    internal static CommandTrialScript? Script(Func<ICommandTrialHost> makeHost, IModLogger logger)
    {
        var options = Options;
        if (options == null || Outcome != null)
            return null;

        try
        {
            if (!SeatTrialSteps.TryChoose(options.Steps, out var steps, out var error))
            {
                logger.LogError($"[SeatTrial] the steps cannot run: {error}");
                Finish("start_refused");
                return null;
            }

            var host = makeHost();
            logger.LogInfo("[SeatTrial] script armed: steps=" + string.Join(",", steps.Select(s => s.Name)));
            return new CommandTrialScript(host, steps, logger, Finish, OnStep);
        }
        catch (Exception ex)
        {
            logger.LogError($"[SeatTrial] the script could not start: {ex}");
            Finish("script_failed");
            return null;
        }
    }

    /// <summary>Clears everything, for a test.</summary>
    internal static void Reset()
    {
        Options = null;
        Outcome = null;
        Step = "none";
        TrialSettings = null;
    }
}
