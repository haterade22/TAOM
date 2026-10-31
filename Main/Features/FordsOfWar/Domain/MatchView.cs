using System.Collections.Generic;

namespace TAOM.Features.FordsOfWar.Domain;

/// <summary>
/// The read model for one commander slot, for the command panel and for the AI. Lanes lists every lane (Owned marks the
/// ones this slot commands); Keeps and Heroes cover both sides. OwnLive and EnemyLive come from the last tick's snapshot.
/// Threat is how many enemies stand in reach of the lane's outermost standing own tower (the enemy lane count once no
/// tower is left). AssaultThreshold and HeroSlotsFree carry the config numbers the AI plans with. A hero's RespawnTotal is
/// the full respawn delay it died with, so a panel or the AI can tell a fresh death from one about to end.
/// A hero's CanBuyback is true when the viewing slot's own dead hero passes every buyback rule but gold: the timer still
/// runs, no buyback is paid yet, and the side is under its alive limit.
/// </summary>
internal sealed record MatchView(int Slot, MatchSide Side, MatchPhase Phase, float Elapsed, int Gold, int AssaultThreshold,
    int HeroSlotsFree, IReadOnlyList<int> OwnedLanes, IReadOnlyList<MatchView.LaneView> Lanes,
    IReadOnlyList<MatchView.TowerView> Keeps, IReadOnlyList<MatchView.HeroView> Heroes,
    IReadOnlyList<MatchView.UnitOfferView> UnitOffers, IReadOnlyList<MatchView.HeroOfferView> HeroOffers,
    IReadOnlyList<MatchView.CapturePointView> CapturePoints, MatchSide? Winner)
{
    internal sealed record LaneView(int Index, bool Owned, LaneOrder Order, float CooldownLeft, int OwnLive, int EnemyLive,
        int Threat, IReadOnlyList<TowerView> Towers);

    internal sealed record TowerView(string Id, MatchSide Owner, int Lane, int Order, TowerKind Kind, int HitPoints,
        int MaxHitPoints, bool Locked, bool Destroyed);

    internal sealed record HeroView(string HeroId, MatchSide Side, int OwnerSlot, int Lane, HeroState State,
        float RespawnLeft, int BuybackPrice, float RespawnTotal, bool CanBuyback);

    internal sealed record UnitOfferView(string OfferId, string TroopId, UnitRole Role, int PackSize, int Price, bool CanAfford);

    internal sealed record HeroOfferView(string HeroId, int Price, bool CanAfford, bool Fielded);

    internal sealed record CapturePointView(string PointId, MatchSide? Owner, MatchSide? Capturer, float Progress, bool Contested, bool Rising);
}
