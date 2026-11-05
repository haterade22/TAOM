using System;
using TAOM.Adapters;
using TAOM.Core.Logging;

namespace TAOM.Features.ButterLibDistanceMatrix;

/// <summary>
/// Switches ButterLib's Distance Matrix off at the first main menu (#740). The subsystem is a "Mod Developer feature"
/// that nothing installed reads, and on TAOM's map it costs about 170 ms of map stall whenever a settlement passes to another
/// clan, plus a full settlement table at every load. There is deliberately no way to keep it on: MCM's ButterLib page rewrites
/// the options file and applies its own toggle, so an opt-in would not hold. One log line either way; never throws.
/// </summary>
internal sealed class DistanceMatrixSwitch
{
    private const string Tag = "[ButterLibDistance] ";

    private readonly IButterLibDistanceMatrixAdapter _adapter;
    private readonly IModLogger _logger;

    public DistanceMatrixSwitch(IButterLibDistanceMatrixAdapter adapter, IModLogger logger)
    {
        _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    internal void Apply()
    {
        try
        {
            var problem = _adapter.TryDisable(out var wasAlreadyOff);
            if (problem != null)
                _logger.LogWarning(Tag + "not switched off: " + problem);
            else if (wasAlreadyOff)
                _logger.LogInfo(Tag + "ButterLib's Distance Matrix was already off (ButterLib's saved options)");
            else
                _logger.LogInfo(Tag + "switched off ButterLib's Distance Matrix for this session (no distance tables, no rebuild at owner changes)");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(Tag + "could not switch the Distance Matrix off: " + ex.GetType().Name + ": " + ex.Message);
        }
    }
}
