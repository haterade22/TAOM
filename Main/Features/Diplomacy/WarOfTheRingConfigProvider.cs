using System;
using System.IO;
using Newtonsoft.Json;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.Diplomacy.Models;

namespace TAOM.Features.Diplomacy;

public class WarOfTheRingConfigProvider : IWarOfTheRingConfigProvider
{
    private readonly IPathService _pathService;
    private readonly IModLogger _logger;

    public WarOfTheRingConfigProvider(IPathService pathService, IModLogger logger)
    {
        _pathService = pathService;
        _logger = logger;
    }

    public WarOfTheRingConfig LoadConfig()
    {
        var path = Path.Combine(_pathService.ModuleDataPath, "diplomacy", "war_of_the_ring.json");

        if (!File.Exists(path))
        {
            _logger.LogWarning($"WarOfTheRingConfigProvider: war_of_the_ring.json not found: {path}");
            return new WarOfTheRingConfig();
        }

        try
        {
            var json = File.ReadAllText(path);
            // Phase 9b #129 P2 — null-literal JSON ("null") returned by DeserializeObject is null.
            // Without `?? new T()`, the next `.Phase1.TriggerDay` access would NRE crash mod startup.
            // Matches BattleBalanceConfigProvider, RevoltTuningConfigProvider patterns.
            var config = JsonConvert.DeserializeObject<WarOfTheRingConfig>(json) ?? new WarOfTheRingConfig();
            ValidateConfig(config);
            _logger.LogInfo($"Loaded War of the Ring config: Phase1 day {config.Phase1.TriggerDay}, Phase2 day {config.Phase2.TriggerDay}");
            return config;
        }
        catch (Exception ex)
        {
            _logger.LogError($"WarOfTheRingConfigProvider: Failed to parse war_of_the_ring.json: {ex.Message}");
            return new WarOfTheRingConfig();
        }
    }

    private void ValidateConfig(WarOfTheRingConfig config)
    {
        // Phase 9b #129 P2 — semantic validation per csharp-architecture.md "Config Providers
        // MUST Validate". Pre-fix: shipped config has Phase2.TriggerDay == Phase1.TriggerDay == 1.
        if (config.Phase1 == null) config.Phase1 = new PhaseConfig { TriggerDay = 1 };
        if (config.Phase2 == null) config.Phase2 = new PhaseConfig { TriggerDay = 365 };
        if (config.TestMode == null) config.TestMode = new TestModeConfig();

        if (config.Phase1.TriggerDay < 1)
        {
            _logger.LogWarning($"WarOfTheRing config: Phase1.TriggerDay ({config.Phase1.TriggerDay}) < 1; reverting to 1.");
            config.Phase1.TriggerDay = 1;
        }
        // Strictly-after, not merely not-before: CheckPhaseTransition's guards are sequential ifs,
        // so equal days run Peace->IsengardWar->FullWar in a single tick and the Isengard war is
        // never observable. The service clamps this too (GetEffectivePhaseDays) — this pass exists
        // to WARN the author that their edited JSON is wrong, which a silent clamp cannot do.
        if (config.Phase2.TriggerDay <= config.Phase1.TriggerDay)
        {
            _logger.LogWarning($"WarOfTheRing config: Phase2.TriggerDay ({config.Phase2.TriggerDay}) must be after Phase1.TriggerDay ({config.Phase1.TriggerDay}); reverting Phase2 to {config.Phase1.TriggerDay + 1}.");
            config.Phase2.TriggerDay = config.Phase1.TriggerDay + 1;
        }

        // Same invariant for the test-mode pair — it feeds the identical CheckPhaseTransition path.
        if (config.TestMode.Phase1Day < 1)
        {
            _logger.LogWarning($"WarOfTheRing config: TestMode.Phase1Day ({config.TestMode.Phase1Day}) < 1; reverting to 1.");
            config.TestMode.Phase1Day = 1;
        }
        if (config.TestMode.Phase2Day <= config.TestMode.Phase1Day)
        {
            _logger.LogWarning($"WarOfTheRing config: TestMode.Phase2Day ({config.TestMode.Phase2Day}) must be after TestMode.Phase1Day ({config.TestMode.Phase1Day}); reverting to {config.TestMode.Phase1Day + 1}.");
            config.TestMode.Phase2Day = config.TestMode.Phase1Day + 1;
        }
    }
}
