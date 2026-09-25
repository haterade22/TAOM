using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

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

    public HeroCombatAdapter(Hero hero) : this(hero, hero?.BattleEquipment) { }

    /// <summary>
    /// Classifies <paramref name="hero"/> from <paramref name="equipment"/> instead of the campaign
    /// BattleEquipment, e.g. a mission agent's spawn equipment, whose horse slot vanilla empties in
    /// siege assaults.
    /// </summary>
    public HeroCombatAdapter(Hero hero, Equipment equipment)
    {
        _hero = hero;
        _equipment = new BattleEquipmentSnapshot(equipment);
    }

    public string StringId => _hero?.StringId ?? string.Empty;
    public bool HasMount => _equipment.HasMount;
    public bool HasShield => _equipment.HasShield;
    public IBattleEquipmentSnapshot Equipment => _equipment;
}
