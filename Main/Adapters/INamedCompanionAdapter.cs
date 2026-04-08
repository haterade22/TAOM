namespace TAOM.Adapters;

public interface INamedCompanionAdapter
{
    bool HeroExists(string characterId);
    bool IsHeroAlive(string characterId);
    bool IsPlacedInSettlement(string characterId);
    bool IsRecruitedOrInParty(string characterId);
    void PlaceInSettlement(string characterId, string settlementId);
    void MarkAsMet(string characterId);
}
