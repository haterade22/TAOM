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

    /// <summary>
    /// Strips every slot of <c>Hero.MainHero.BattleEquipment</c> and <c>CivilianEquipment</c>,
    /// depositing each non-empty <c>EquipmentElement</c> into <c>PartyBase.MainParty.ItemRoster</c>
    /// (the modifier-preserving overload). Returns the count of slots that had items.
    /// Returns 0 if main hero, party, or roster is unavailable, OR when the hero's equipment
    /// pointer is the dead-equipment singleton (do not strip dead-hero state).
    /// </summary>
    int TryUnequipAllPlayerSlots();
}
