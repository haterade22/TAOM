namespace TAOM.Features.LordSpawnGuard;

/// <summary>
/// Repairs the one precondition that makes vanilla's <c>HeroSpawnCampaignBehavior.SpawnLordParty</c>
/// throw: a faction with no <c>InitialHomeSettlement</c> whose lords carry a culture that owns no
/// settlement. See <see cref="LordSpawnGuardService"/> for the full failure chain.
/// </summary>
public interface ILordSpawnGuardService
{
    /// <summary>
    /// Called immediately before vanilla spawns a party for <paramref name="heroId"/>. Gives the
    /// hero's faction a home settlement when — and only when — vanilla's culture lookup would
    /// otherwise throw. A no-op for every healthy faction.
    /// </summary>
    void EnsureSpawnAnchor(string heroId);
}
