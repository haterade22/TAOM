namespace TAOM.Adapters;

/// <summary>Finds a hero by StringId on the co-op server for career sync (docs/features/coop-interop.md, "Career sync").</summary>
public interface ICareerSyncHeroAdapter
{
    /// <summary>The hero as the career rules read it, or null when no hero has this id.</summary>
    ICareerHeroAdapter? Find(string heroId);
}
