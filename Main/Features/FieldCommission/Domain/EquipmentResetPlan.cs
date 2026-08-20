namespace TAOM.Features.FieldCommission.Domain;

/// <summary>
/// Where one equipment slot's contents come from when a fired wanderer's kit is reset.
/// </summary>
public enum EquipmentResetSource
{
    /// <summary>Nothing usable to reset from. Leave the slot as it is.</summary>
    None,

    /// <summary>The template supplies this slot itself.</summary>
    Template,

    /// <summary>The template has no equipment for this slot, so its battle kit stands in.</summary>
    BattleFallback,
}

/// <summary>
/// The decision half of <c>Patch71_HeroResetEquipmentsGuard</c> (#486).
///
/// <c>Hero.ResetEquipments</c> clones three equipments off <c>Hero.Template</c> with no null
/// checks. For a FieldCommission-promoted companion that Template is the line troop, and 743 of
/// TAOM's 895 troop blocks declare no civilian roster, so the second clone dereferences null and
/// firing the companion throws mid-way through <c>RemoveCompanionAction.ApplyInternal</c>. The
/// stealth line has the same shape for the eight <c>is_bandit</c> cultures, which declare no
/// stealth roster (as vanilla's own bandit cultures do not either).
///
/// Kept free of TaleWorlds types on purpose: the patch does every dereference at the boundary and
/// passes the answers in as booleans, so this stays unit-testable without a running campaign.
/// </summary>
public static class EquipmentResetPlan
{
    /// <summary>
    /// True when the template can supply all three slots, so vanilla's own reset runs untouched.
    /// Behaviour only changes in the cases that throw today.
    /// </summary>
    public static bool CanDeferToEngine(bool hasBattle, bool hasCivilian, bool hasStealth)
        => hasBattle && hasCivilian && hasStealth;

    /// <summary>
    /// Which source fills one slot. The slot's own equipment always wins; battle gear is only a
    /// stand-in, and a slot with neither is left alone rather than filled with the campaign-wide
    /// dead-equipment default.
    /// </summary>
    public static EquipmentResetSource ForSlot(bool hasSlotEquipment, bool hasBattleEquipment)
    {
        if (hasSlotEquipment)
            return EquipmentResetSource.Template;

        return hasBattleEquipment ? EquipmentResetSource.BattleFallback : EquipmentResetSource.None;
    }

    /// <summary>
    /// Whether the fill should copy the source's <c>EquipmentType</c> onto the target. Only when
    /// the source is the slot's own equipment: copying it on a battle fallback is what retypes a
    /// hero's civilian kit to <c>EquipmentType.Battle</c>.
    /// </summary>
    public static bool KeepsSourceEquipmentType(EquipmentResetSource source)
        => source == EquipmentResetSource.Template;
}
