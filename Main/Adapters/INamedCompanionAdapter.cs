namespace TAOM.Adapters;

public interface INamedCompanionAdapter
{
    bool HeroExists(string characterId);
    bool IsHeroAlive(string characterId);
    bool IsPlacedInSettlement(string characterId);
    bool IsRecruitedOrInParty(string characterId);

    // #127 — Entity State Matrix completion. Prisoner / Fugitive bypass the existing guards
    // (PartyBelongedTo=null, CurrentSettlement=null, StayingInSettlement=null) and were being
    // force-placed on every load — corrupting captor prison rosters and resetting fugitive state.
    bool IsHeroPrisoner(string characterId);
    bool IsHeroFugitive(string characterId);

    void PlaceInSettlement(string characterId, string settlementId);
    void MarkAsMet(string characterId);
}
