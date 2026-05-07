namespace TAOM.Adapters;

public enum PlayerEquipmentApplyResult
{
    Success,
    RosterNotFound,
    NoSuitableEquipment,
    HeroNotFound
}

public interface IPlayerEquipmentAdapter
{
    PlayerEquipmentApplyResult ApplyRosterToPlayer(string rosterId, string playerHeroId);
}
