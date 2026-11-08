using System.Collections.Generic;

namespace TAOM.Features.FordsOfWar.Domain;

/// <summary>
/// What the engine layer must do after a tick. Heals is an addition to the build spec: the regenerated hit points to
/// mirror onto the tower entities. Winner is set from the tick after a keep falls and stays set.
/// </summary>
internal sealed class MatchEffects
{
    public List<SpawnRequest> Spawns { get; } = new();

    public List<FormationCommand> Commands { get; } = new();

    public List<TowerVolley> Volleys { get; } = new();

    public List<MatchEvent> Events { get; } = new();

    /// <summary>Log lines the match wants written (an AI regroup order); they never reach the command panel.</summary>
    public List<string> Notes { get; } = new();

    public List<TowerHeal> Heals { get; } = new();

    public List<CampOrder> CampOrders { get; } = new();

    public List<PoisonTick> PoisonTicks { get; } = new();

    public MatchSide? Winner { get; set; }
}
