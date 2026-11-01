using System;
using TAOM.Core.Logging;
using TAOM.Features.FordsOfWar.Diagnostics;

namespace TAOM.Features.FordsOfWar.Autorun;

/// <summary>Everything the autorun says: its <c>[FordsOfWar][Autorun]</c> log lines and the files it leaves for the launcher. Never throws.</summary>
internal sealed class FordsAutorunOutput
{
    private const string Prefix = "[FordsOfWar][Autorun] ";

    private readonly IFordsAutorunHost _host;
    private readonly IModLogger _logger;

    internal FordsAutorunOutput(IFordsAutorunHost host, IModLogger logger)
    {
        _host = host;
        _logger = logger;
    }

    internal void Info(string body) => _logger.LogInfo(Prefix + body);

    internal void Warn(string body) => _logger.LogWarning(Prefix + body);

    /// <summary>What the plan armed, and the arm file's warnings. A plan that is not armed has no driver (<see cref="FordsAutorunStart"/> says why).</summary>
    internal void Announce(FordsAutorunPlan plan)
    {
        if (!plan.Armed)
            return;

        Info("armed plan=" + FordsLogLine.Q(plan.Describe()));
        foreach (var warning in plan.Warnings)
            Warn("arm file: " + warning);
    }

    /// <summary>A file write that fails is logged, not thrown: the done file is the last thing the run does and the quit must still follow.</summary>
    internal void Write(string name, string text)
    {
        try
        {
            _host.WriteFile(name, text);
        }
        catch (Exception ex)
        {
            Warn("could not write " + name + ": " + ex.GetType().Name + ": " + ex.Message);
        }
    }
}
