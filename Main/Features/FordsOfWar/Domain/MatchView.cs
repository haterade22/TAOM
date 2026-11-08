using System.Collections.Generic;

namespace TAOM.Features.FordsOfWar.Domain;

/// <summary>
/// The read model for one commander slot, for the command panel and for the AI. Lanes lists every lane (Owned marks the
/// ones this slot commands); Keeps and Heroes cover both sides. OwnLive and EnemyLive come from the last tick's snapshot.
/// Threat is how many enemies stand in reach of the lane's outermost standing own tower (the enemy lane count once no
/// tower is left). AssaultThreshold (the config's share of this faction's lane cap) and HeroSlotsFree carry the numbers the AI plans with. A hero's RespawnTotal is
/// the full respawn delay it died with, so a panel or the AI can tell a fresh death from one about to end.
/// A hero's CanBuyback is true when the viewing slot's own dead hero passes every buyback rule but gold: the timer still
/// runs, no buyback is paid yet, and the side is under its alive limit.
/// </summary>
internal sealed record MatchView(int Slot, MatchSide Side, MatchPhase Phase, float Elapsed, int Gold, int AssaultThreshold,
    int HeroSlotsFree, IReadOnlyList<int> OwnedLanes, IReadOnlyList<MatchView.LaneView> Lanes,
    IReadOnlyList<MatchView.TowerView> Keeps, IReadOnlyList<MatchView.HeroView> Heroes,
    IReadOnlyList<MatchView.UnitOfferView> UnitOffers, IReadOnlyList<MatchView.HeroOfferView> HeroOffers,
    IReadOnlyList<MatchView.CapturePointView> CapturePoints, MatchSide? Winner, string FactionId = "",
    IReadOnlyList<FactionTrait>? Traits = null, MatchView.RegionView? Region = null, MatchView.CreatureCallView? Creatures = null,
    int IncomePerMinute = 0, string FactionName = "", IReadOnlyList<MatchView.CampView>? Camps = null, float OrderCooldownSeconds = 0f,
    IReadOnlyList<string>? AiBuyCycle = null, IReadOnlyList<MatchView.UpgradeView>? Upgrades = null, string EnemyFactionName = "", float? NextFreeHeroIn = null, bool EndedOnTime = false)
{
    /// <summary>
    /// A lane as one commander sees it. NearOwnValue is the summed value of the lane's own units within the pressure radius of its lead,
    /// NearEnemyValue and NearEnemyCount the same for enemy units of any lane; all three are 0 for a lane with no lead.
    /// AtOwnTower is true when the lead stands within fire range of the lane's outermost standing own tower (the keep when none stands).
    /// </summary>
    internal sealed record LaneView(int Index, bool Owned, LaneOrder Order, float CooldownLeft, int OwnLive, int EnemyLive,
        int Threat, IReadOnlyList<TowerView> Towers, bool CanSeize = false, float NearOwnValue = 0f, float NearEnemyValue = 0f,
        int NearEnemyCount = 0, bool AtOwnTower = false);

    /// <summary>Gondor's rotation as the panel shows it. CanAffordCall is the gold test only.</summary>
    internal sealed record RegionView(string Id, string Name, float SecondsLeft, float TotalSeconds, string NextId, string NextName, int CallFee,
        bool CanAffordCall, IReadOnlyList<RegionChoiceView> Choices);

    internal sealed record RegionChoiceView(string Id, string Name);

    /// <summary>The creature call as the panel and the AI read it: the shared timer and what each creature costs.</summary>
    internal sealed record CreatureCallView(float CooldownLeft, float CooldownTotal, IReadOnlyList<UnitOfferView> Offers);

    /// <summary>A neutral camp as the board shows it: creatures left and what clearing it pays.</summary>
    internal sealed record CampView(string Id, int Alive, int Reward);

    internal sealed record TowerView(string Id, MatchSide Owner, int Lane, int Order, TowerKind Kind, int HitPoints,
        int MaxHitPoints, bool Locked, bool Destroyed);

    internal sealed record HeroView(string HeroId, MatchSide Side, int OwnerSlot, int Lane, HeroState State,
        float RespawnLeft, int BuybackPrice, float RespawnTotal, bool CanBuyback);

    internal sealed record UnitOfferView(string OfferId, string TroopId, UnitRole Role, int PackSize, int Price, bool CanAfford);

    internal sealed record HeroOfferView(string HeroId, int Price, bool CanAfford, bool Fielded);

    /// <summary>One upgrade track as this side stands on it. NextPrice is null at the top level.</summary>
    internal sealed record UpgradeView(string Id, string Name, UpgradeEffect Effect, float StepPerLevel, int Level, int MaxLevel, int? NextPrice, bool CanAfford);

    internal sealed record CapturePointView(string PointId, MatchSide? Owner, MatchSide? Capturer, float Progress, bool Contested, bool Rising);
}
