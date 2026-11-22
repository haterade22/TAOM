using TAOM.Adapters;
using TAOM.Features.CareerSystem;

namespace TAOM.Features.CoopInterop.CareerSync;

/// <summary>
/// The career tier trust (docs/features/coop-interop.md, "Career sync"). On the co-op server a choice's tier counts only
/// when the hero's level on the server opens it: a player's report may list a tier a career quest unlocked, which the
/// server cannot check, so the server keeps the choice in its book but applies none of its passives until the level.
/// In single player and on a co-op client every tier counts, because there the quest unlock is local. The session
/// flags are read on each call, because they change within one process (<see cref="ICoopSessionProvider"/>).
/// </summary>
public sealed class CoopCareerTierTrust : ICareerTierTrust
{
    private readonly ICoopSessionProvider _session;
    private readonly ICareerSyncHeroAdapter _heroes;
    private readonly ICareerRegistry _registry;

    public CoopCareerTierTrust(ICoopSessionProvider session, ICareerSyncHeroAdapter heroes, ICareerRegistry registry)
    {
        _session = session;
        _heroes = heroes;
        _registry = registry;
    }

    public bool Counts(string heroId, int tier)
    {
        if (!(_session.IsSessionActive && _session.IsAuthority)) return true;

        var hero = _heroes.Find(heroId);
        return hero != null && _registry.IsTierAvailable(hero.Level, tier);
    }
}
