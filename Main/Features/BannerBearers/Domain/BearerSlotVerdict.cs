namespace TAOM.Features.BannerBearers.Domain;

// Outcome of the Patch63 spawn-guard check on a freshly-spawned bearer's ExtraWeaponSlot,
// evaluated BEFORE the engine's unguarded native slot-4 read (issue #360).
public enum BearerSlotVerdict
{
    // Slot holds the expected formation banner — safe to read the native weapon entity.
    Ok,

    // Slot is empty: the Mission.SpawnTroop banner gate silently declined, or the native
    // wield of the sidearm dropped the banner during spawn. Reading the native entity
    // here is the AV site of the Glad Thaw siege CTD.
    SlotEmpty,

    // Slot holds something other than the expected banner (or the expected id is unknown —
    // fail closed). Registering that entity would corrupt the banner bookkeeping.
    WrongItem,
}
