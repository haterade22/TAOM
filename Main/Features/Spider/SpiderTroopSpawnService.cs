namespace TAOM.Features.Spider;

/// <summary>
/// Pure decision: is this character the recruitable spider troop anchor? The detached spider spawn
/// itself is engine-coupled boundary glue in <c>SpiderDetachedAgentSpawner</c> (Hooks/), driven by the
/// Mission.SpawnAgent prefix. See docs/features/spider.md + docs/reviews/rca-spider-troop-2026-06-04.md.
/// </summary>
public class SpiderTroopSpawnService : ISpiderTroopSpawnService
{
    public bool IsSpiderTroop(string characterId) => characterId == SpiderConfig.SpiderCharacterId;
}
