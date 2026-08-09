using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TAOM.Core.Logging;
using TAOM.Features.AutoResolveDiagnostics.Domain;

namespace TAOM.Adapters;

/// <summary>
/// Snapshots every troop type's simulation stats from the live engine, once per session.
///
/// Why this exists: the offline analyzer derives tier and power from `troops_*.xml` plus a
/// hardcoded copy of the tier table. Both derivations rest on assumptions that fail silently —
/// TAOM's CharacterStatsModel raising MaxCharacterTier to 10, the MCM/JSON-configurable power
/// table, the mounted multiplier. Asking the engine directly turns those assumptions into data.
///
/// Each record is also the pre-flight check for the counter system: it carries the engine's own
/// DefaultFormationClass, so the offline classifier can be validated against ground truth BEFORE
/// any counter matrix ships against it.
/// </summary>
public class TroopCensusAdapter : ITroopCensusAdapter
{
    private readonly IModLogger _logger;

    public TroopCensusAdapter(IModLogger logger)
    {
        _logger = logger;
    }

    public IReadOnlyList<TroopCensusRecord> Capture()
    {
        var results = new List<TroopCensusRecord>();
        try
        {
            var all = CharacterObject.All;
            var powerModel = Campaign.Current?.Models?.MilitaryPowerModel;
            if (all == null)
                return results;

            foreach (var troop in all)
            {
                if (troop == null || string.IsNullOrEmpty(troop.StringId))
                    continue;

                try
                {
                    results.Add(new TroopCensusRecord
                    {
                        Id = troop.StringId,
                        Level = troop.Level,
                        Tier = troop.Tier,
                        Power = powerModel?.GetDefaultTroopPower(troop) ?? 0f,
                        HitPoints = troop.MaxHitPoints(),
                        Formation = troop.DefaultFormationClass.ToString(),
                        Mounted = troop.IsMounted,
                        Ranged = troop.IsRanged,
                        IsHero = troop.IsHero,
                        Culture = troop.Culture?.StringId,
                        Race = troop.Race,
                    });
                }
                catch
                {
                    // One malformed troop must not cost the whole census. A missing row is visible
                    // in the analyzer as a troop with no ground truth; a thrown census is not.
                }
            }
        }
        catch (System.Exception ex)
        {
            _logger?.LogWarning($"[AutoResolve] troop census failed: {ex.Message}");
        }
        return results;
    }
}
