using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TAOM.Features.StagedSiege.Domain;

namespace TAOM.Features.StagedSiege.Hooks;

/// <summary>
/// The engine's custom reinforcement timer for a staged siege: <see cref="Check"/> is asked for each side every tick by
/// <c>DefaultBattleMissionAgentSpawnLogic.CheckCustomReinforcementBatch</c>, and <see cref="ResetTimer"/> once per side when
/// reinforcements are enabled after deployment. Both land on the main thread; the service owns every decision. A side other
/// than the attacker or the defender never releases.
/// </summary>
public sealed class StagedReinforcementTimer : ICustomReinforcementSpawnTimer
{
    private readonly StagedSiegeService _service;

    public StagedReinforcementTimer(StagedSiegeService service) => _service = service;

    public bool Check(BattleSideEnum side) => TryMap(side, out var mapped) && _service.ShouldReleaseBatch(mapped);

    public void ResetTimer(BattleSideEnum side)
    {
        if (TryMap(side, out var mapped))
            _service.ResetCadence(mapped);
    }

    private static bool TryMap(BattleSideEnum side, out StagedSide mapped)
    {
        mapped = side == BattleSideEnum.Attacker ? StagedSide.Attacker : StagedSide.Defender;
        return side == BattleSideEnum.Attacker || side == BattleSideEnum.Defender;
    }
}
