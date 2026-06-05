namespace TAOM.Features.Spider;

/// <summary>
/// Decides whether an engine-built character is the recruitable spider troop anchor
/// (<see cref="SpiderConfig.SpiderCharacterId"/>). The actual detached spider spawn is engine-coupled
/// boundary glue in <c>SpiderDetachedAgentSpawner</c> (Hooks/), invoked from the Mission.SpawnAgent
/// prefix (Patch45_SpiderTroopSpawn). See docs/reviews/rca-spider-troop-2026-06-04.md.
/// </summary>
public interface ISpiderTroopSpawnService
{
    /// <summary>True iff <paramref name="characterId"/> is the spider troop anchor (taom_spider_creature).</summary>
    bool IsSpiderTroop(string characterId);
}
