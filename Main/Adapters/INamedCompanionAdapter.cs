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

    // Named companions are XML heroes carrying faction="Faction.neutral", and Hero.Deserialize
    // skips the Clan assignment for that literal id — so Clan stays null. XML deserialization never
    // assigns BornSettlement either (only HeroCreator does), so HomeSettlement resolves to null too,
    // and a companion sitting in a tavern has no PartyBelongedTo. All three null makes
    // Hero.MapFaction return null, which the engine dereferences unguarded in several places.
    // Returns true when a repair was applied.
    bool EnsureHomeSettlement(string characterId, string settlementId);

    void PlaceInSettlement(string characterId, string settlementId);
    void MarkAsMet(string characterId);
}
