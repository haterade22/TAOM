using TaleWorlds.CampaignSystem;

namespace TAOM.Adapters;

/// <summary>
/// Concrete implementation of <see cref="IHeroCombatAdapter"/>. Wraps a sealed
/// <see cref="Hero"/> and projects only the combat-relevant state for the
/// CompanionTactics role detection service (ADR-007).
/// </summary>
public sealed class HeroCombatAdapter : IHeroCombatAdapter
{
    private readonly Hero _hero;
    private readonly IBattleEquipmentSnapshot _equipment;

    public HeroCombatAdapter(Hero hero)
    {
        _hero = hero;
        _equipment = new BattleEquipmentSnapshot(hero?.BattleEquipment);
    }

    public string StringId => _hero?.StringId ?? string.Empty;
    public bool HasMount => _equipment.HasMount;
    public bool HasShield => _equipment.HasShield;
    public IBattleEquipmentSnapshot Equipment => _equipment;
}
