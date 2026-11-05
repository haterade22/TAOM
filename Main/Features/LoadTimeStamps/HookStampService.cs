using System;
using TAOM.Core.Diagnostics;
using TAOM.Core.Logging;

namespace TAOM.Features.LoadTimeStamps;

/// <summary>Starts a <see cref="HookTimer"/> for one TAOM hook when the detailed stamps are on.</summary>
public sealed class HookStampService
{
    private readonly LoadStampDetailGate _gate;
    private readonly IStampClock _clock;
    private readonly IModLogger _logger;

    public HookStampService(LoadStampDetailGate gate, IStampClock clock, IModLogger logger)
    {
        _gate = gate ?? throw new ArgumentNullException(nameof(gate));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>Null when "Enable Load-Time Stamps" is off.</summary>
    public HookTimer? Start(string hook, string? game) =>
        _gate.Enabled ? new HookTimer(hook, game, _clock, _logger) : null;
}
