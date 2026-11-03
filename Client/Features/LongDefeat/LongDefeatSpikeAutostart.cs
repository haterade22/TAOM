using System;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TAOM.Core.Logging;
using TAOM.Features.LongDefeat;
using TAOM.Features.LongDefeat.Spike;

namespace TAOM.Client.Features.LongDefeat;

/// <summary>Starts the Phase 0 spike by itself when <c>TAOM_LONGDEFEAT_SPIKE</c> is set (#1079), after the main menu has been up for 4 s, as the Fords autorun does.</summary>
internal sealed class LongDefeatSpikeAutostart
{
    private const float SettleSeconds = 4f;

    private readonly LongDefeatSpikeOptions _options;
    private readonly Func<ILongDefeatGameManagerFactory> _factory;
    private readonly IModLogger _logger;
    private bool _finished;
    private float _settled;

    private LongDefeatSpikeAutostart(LongDefeatSpikeOptions options, Func<ILongDefeatGameManagerFactory> factory, IModLogger logger)
    {
        _options = options;
        _factory = factory;
        _logger = logger;
    }

    /// <summary>The autostart for this process, or null when no arm file is set, which is every player session.</summary>
    public static LongDefeatSpikeAutostart? Armed(IModLogger logger, Func<ILongDefeatGameManagerFactory> factory)
    {
        var options = LongDefeatSpikeOptions.FromEnvironment(logger);
        if (options == null)
            return null;

        logger.LogInfo("[LongDefeat][Spike] armed: " + options);
        return new LongDefeatSpikeAutostart(options, factory, logger);
    }

    public void Tick(float dt)
    {
        if (_finished)
            return;
        try
        {
            if (!(GameStateManager.Current?.ActiveState is InitialState) || Game.Current != null)
            {
                _settled = 0f;
                return;
            }

            if (InformationManager.IsAnyInquiryActive())
                InformationManager.HideInquiry();
            _settled += dt;
            if (_settled < SettleSeconds)
                return;

            _finished = true;
            var manager = _factory().Create(_options);
            if (manager != null)
                MBGameManager.StartNewGame(manager);
        }
        catch (Exception ex)
        {
            _finished = true;
            _logger.LogError($"[LongDefeat][Spike] the autostart failed, so no run starts: {ex}");
        }
    }
}
