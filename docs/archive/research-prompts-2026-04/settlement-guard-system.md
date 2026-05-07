# Settlement Guard System — Deep Dive Research

## A. Guard Spawning Pipeline

```
PlayerEncounter.LocationEncounter fires
    └─ LocationCharactersAreReadyToSpawnEvent
        └─ GuardsCampaignBehavior.LocationCharactersAreReadyToSpawn()
            ├─ Gate: settlement.IsFortification (villages EXCLUDED)
            ├─ AddGarrisonAndPrisonCharacters(settlement)
            │   ├─ InitializeGarrisonCharacters(settlement)
            │   │   └─ Iterates garrison party roster
            │   │       └─ Filters: Occupation == 7 (Soldier) only
            │   │       └─ Builds _garrisonTroops list: (CharacterObject, healthyCount)
            │   └─ Adds 1 CreatePrisonGuard to "center" location
            │       └─ Uses culture.PrisonGuard directly (NOT garrison)
            │
            └─ AddGuardsFromGarrison(settlement, unusedUsablePointCount, location)
                ├─ Reads spawn point counts from scene:
                │   sp_guard, sp_guard_with_spear, sp_guard_patrol,
                │   sp_guard_unarmed, sp_guard_castle
                ├─ Scales by prosperity: count * (prosperityLevel + 1) / 3
                │   Low=0.33x, Mid=1.0x, High=1.67x
                ├─ Unarmed scaled further: castle=1.6x, town=0.4x
                ├─ sp_guard_castle NOT prosperity-scaled
                │
                └─ For each spawn point type, calls factory method:
                    └─ TakeGuardAgentDataFromGarrisonTroopList(culture, spear?, unarmed?)
                        ├─ If _garrisonTroops not empty:
                        │   └─ Weighted random by troop.Level
                        │   └─ Consumes 1 from count, removes at 0
                        └─ Else: culture.Guard fallback
                            └─ PrepareGuardAgentDataFromGarrison()
                                ├─ GetRandomEquipmentElements()
                                ├─ Remove 1 weapon to free a slot
                                ├─ Spear override OR unarmed strip
                                ├─ FaceGen.GetMonsterWithSuffix(race, "_settlement")
                                └─ NoHorses(true)
```

### World Map Patrol Pipeline (Separate System)

```
DailyTickSettlement
    └─ PatrolPartiesCampaignBehavior
        ├─ Gate: IsTown AND has GuardHouse building (level > 0)
        ├─ Gate: No existing patrol party (one per settlement)
        ├─ Queue spawn with delay: 10 - (guardLevel - 1) * 2 days
        └─ SpawnPatrolParty()
            └─ DefaultSettlementPatrolModel.GetPartyTemplateForPatrolParty()
                └─ GuardHouse effect → Weak/Moderate/Strong template
                    └─ Templates from OwnerClan.Culture
```

---

## B. Interception Points (Ranked by Feasibility)

### Rank 1: TakeGuardAgentDataFromGarrisonTroopList (BEST)

| Aspect | Detail |
|--------|--------|
| **What it controls** | Which troop character becomes a guard |
| **Patch type** | Prefix (skip original) or Postfix (modify result) |
| **Parameters** | `CultureObject culture, bool overrideWeaponWithSpear, bool unarmed` |
| **Settlement context** | YES — via `PlayerEncounter.LocationEncounter.Settlement` (same as vanilla uses) |
| **Risk** | LOW — only changes troop selection, all equipment/behavior logic preserved |
| **Approach** | Prefix: check settlement ID against XML config → if match, return custom troop; if no match, run original |

**Why best**: Minimal override surface. Equipment assembly, behavior assignment, and spawn point logic all remain vanilla. Only the "who" changes.

**Limitation**: Method is `private` and instance-level. Need Harmony `AccessTools.Method` to target it. The `_garrisonTroops` field is also private — a Prefix that skips original needs to handle garrison consumption itself, or a Transpiler to inject the settlement check.

### Rank 2: AddGuardsFromGarrison (GOOD)

| Aspect | Detail |
|--------|--------|
| **What it controls** | Entire guard spawn dispatch for a location |
| **Patch type** | Prefix (skip original for configured settlements) |
| **Parameters** | `Settlement settlement, Dictionary<string, int> unusedUsablePointCount, Location location` |
| **Settlement context** | YES — direct parameter |
| **Risk** | MEDIUM — replacing entire method means maintaining spawn point scaling, prosperity math |
| **Approach** | Prefix: if settlement has custom config, run custom spawn logic and `return false`; otherwise let vanilla run |

**Why good**: Full control over which troops go to which spawn points. Can map specific troops to `sp_guard_castle` vs `sp_guard` etc. But must reimplement prosperity scaling.

### Rank 3: CreateLocationCharacterDelegate replacement (GOOD)

| Aspect | Detail |
|--------|--------|
| **What it controls** | Individual guard character creation |
| **Patch type** | Transpiler on AddGuardsFromGarrison to swap delegates |
| **Parameters** | `CultureObject culture, CharacterRelations relation` |
| **Settlement context** | NOT directly — would need closure or static state |
| **Risk** | MEDIUM-HIGH — Transpiler complexity |
| **Approach** | Replace `CreateStandGuard` etc. with custom delegates per settlement |

### Rank 4: DefaultSettlementPatrolModel override (FOR PATROLS)

| Aspect | Detail |
|--------|--------|
| **What it controls** | World map patrol party composition |
| **Patch type** | GameModel override (clean, no Harmony needed) |
| **Parameters** | `Settlement settlement, bool naval` |
| **Settlement context** | YES — direct parameter |
| **Risk** | VERY LOW — standard GameModel pattern |
| **Approach** | `TaomSettlementPatrolModel` that checks settlement ID for custom patrol template |

### Rank 5: PrepareGuardAgentDataFromGarrison (EQUIPMENT ONLY)

| Aspect | Detail |
|--------|--------|
| **What it controls** | Equipment assembly after troop selection |
| **Patch type** | Postfix to modify equipment |
| **Parameters** | `CharacterObject guardRosterElement, bool overrideWeaponWithSpear, bool unarmed` |
| **Settlement context** | NO — not available without static state |
| **Risk** | LOW but limited — only changes gear, not troop identity |
| **Approach** | Could force specific equipment sets for settlement-specific guard variants |

### Rank 6: GetSuitableSpear (TARGETED FIX)

| Aspect | Detail |
|--------|--------|
| **What it controls** | Which spear item guards with spear override receive |
| **Patch type** | Prefix (skip original, return custom) |
| **Parameters** | `CultureObject culture` |
| **Settlement context** | NO — only culture |
| **Risk** | VERY LOW — simple lookup replacement |
| **Approach** | Map each TAOM culture to an appropriate spear item ID |

---

## C. Data Model Design

### Recommended XML Structure

```xml
<!-- Main/_Module/ModuleData/settlement_guards/settlement_guards_config.xml -->
<SettlementGuards>
  <!-- Per-settlement guard definitions -->
  <Settlement id="town_EW1"> <!-- Minas Tirith -->
    <Guards>
      <Guard troop="NPCCharacter.gondor_citadel_guard" weight="3" spawn_points="sp_guard_castle,sp_guard_with_spear" />
      <Guard troop="NPCCharacter.gondor_fountain_guard" weight="2" spawn_points="sp_guard,sp_guard_patrol" />
      <Guard troop="NPCCharacter.gondor_gate_warden" weight="1" spawn_points="sp_guard_castle" />
    </Guards>
    <PrisonGuard troop="NPCCharacter.gondor_dungeon_keeper" /> <!-- optional override -->
  </Settlement>

  <Settlement id="town_EN1"> <!-- Edoras -->
    <Guards>
      <Guard troop="NPCCharacter.rohan_door_warden" weight="2" spawn_points="sp_guard_castle,sp_guard" />
      <Guard troop="NPCCharacter.rohan_royal_guard" weight="1" spawn_points="sp_guard_with_spear" />
    </Guards>
  </Settlement>

  <!-- Fallback chain: settlement → clan → culture (matches VolunteerRecruitmentService pattern) -->
  <Clan id="clan_gondor_1"> <!-- Denethor's clan -->
    <Guards>
      <Guard troop="NPCCharacter.gondor_tower_guard" weight="2" />
      <Guard troop="NPCCharacter.gondor_ranger" weight="1" />
    </Guards>
  </Clan>

  <!-- Culture-level fallback (replaces culture.Guard) -->
  <Culture id="gondor">
    <Guards>
      <Guard troop="NPCCharacter.guard_gondor" weight="1" />
    </Guards>
  </Culture>

  <!-- Per-culture spear mapping (replaces GetSuitableSpear hardcode) -->
  <Spears>
    <Spear culture="gondor" item="Item.gondor_guard_spear" />
    <Spear culture="erebor" item="Item.erebor_guard_pike" />
    <Spear culture="rohan" item="Item.rohan_guard_lance" />
    <!-- etc. -->
  </Spears>
</SettlementGuards>
```

### Fallback Resolution Chain

```
1. Check SettlementMap[settlement.StringId]
   → If found, use settlement-specific guard pool
2. Check SettlementMap[settlement.BoundSettlement?.StringId]
   → For villages/castles bound to a town
3. Check ClanMap[settlement.OwnerClan.StringId]
   → Clan-specific guards
4. Check CultureMap[settlement.OwnerClan.Culture.StringId]
   → Culture-level guards
5. Fall back to culture.Guard
   → Vanilla behavior
```

This mirrors the existing `VolunteerRecruitmentService` pattern already proven in TAOM.

### Spawn Point Type Mapping

The `spawn_points` attribute allows controlling which guard types use which characters:

| Spawn Point | Vanilla Role | Custom Mapping Example |
|-------------|-------------|----------------------|
| `sp_guard_castle` | Castle entrance, NOT prosperity-scaled | Elite guards (Citadel Guard) |
| `sp_guard_with_spear` | Standing with spear, prosperity-scaled | Ceremonial guards (Fountain Guard) |
| `sp_guard` | Basic standing, prosperity-scaled | Standard watch (Tower Guard) |
| `sp_guard_patrol` | Walking patrol routes, prosperity-scaled | Patrol units (City Watch) |
| `sp_guard_unarmed` | Unarmed wanderers, prosperity×castle ratio | Off-duty / civilian guards |

If no `spawn_points` attribute, the guard can appear at any point (weighted random from pool).

---

## D. Spear Culture Hardcode

### Current Vanilla Code

```csharp
private static ItemObject GetSuitableSpear(CultureObject culture)
{
    string text = (culture.StringId == "battania") ? "northern_spear_2_t3" : "western_spear_3_t3";
    return MBObjectManager.Instance.GetObject<ItemObject>(text);
}
```

Only two spear IDs for ALL cultures. Battania (Khand in TAOM) gets `northern_spear_2_t3`, everyone else gets `western_spear_3_t3`.

### TAOM Impact

These are **vanilla item IDs**. If TAOM's armory module doesn't include these items, spear guards will have **null weapons** (invisible spears or crashes). Options:

1. **Ensure items exist**: Verify `northern_spear_2_t3` and `western_spear_3_t3` exist in LOTRLOME_Armory or vanilla
2. **Patch GetSuitableSpear**: Harmony Prefix that maps each TAOM culture to a lore-appropriate spear
3. **Use XML config**: Include spear mapping in the settlement guards config (see Section C)

### Recommended Per-Culture Spear Mapping

| Culture | LOTR Faction | Spear Item |
|---------|-------------|------------|
| gondor | Gondor | Gondorian guard spear |
| rohan | Rohan | Rohirrim lance |
| erebor | Erebor | Dwarven pike |
| isengard | Isengard | Uruk pike |
| mordor | Mordor | Orc spear |
| lothlorien | Lothlorien | Elven spear |
| rivendell | Rivendell | Noldor spear |
| mirkwood | Mirkwood | Silvan spear |
| gundabad | Gundabad | Goblin spear |
| dolguldur | Dol Guldur | Dark spear |
| dunland | Dunland | Dunlending spear |
| harad | Harad | Haradrim spear |
| rhun | Rhun | Easterling pike |
| dale | Dale | Dalish spear |
| khand | Khand | Variag spear |
| umbar | Umbar | Corsair spear |

Each must be verified against LOTRLOME_Armory item IDs.

---

## E. Risks and Gotchas

### Save Compatibility

**SAFE** — The guard system has NO save data. `GuardsCampaignBehavior.SyncData` is empty. Guards are spawned fresh every time the player enters a settlement scene. Adding per-settlement config is purely additive and save-compatible.

The patrol system (`PatrolPartiesCampaignBehavior`) DOES save: `_partyGenerationQueue`, `_lastHomeSettlementVisitTimes`, `_interactedPatrolParties`. A custom `TaomSettlementPatrolModel` is also save-safe since it's a stateless GameModel.

### Performance

- `InitializeGarrisonCharacters` iterates the full garrison roster once per settlement entry
- `TakeGuardAgentDataFromGarrisonTroopList` does weighted random selection (builds list + choose) per guard spawned
- Typical guard count: 10-30 per settlement scene
- **Not a performance concern** — happens once on settlement entry, not per-frame

A per-settlement XML lookup would add negligible overhead (dictionary lookup).

### Interaction with Existing TAOM Patches

| Patch | Risk | Notes |
|-------|------|-------|
| **Patch8_SiegeCampGuard** | NONE | Different system — handles siege camp positioning, not settlement guards |
| **Patch23_BannerColorPersistence** | LOW | Banner color patches affect guard agent visuals. Custom guards should still get correct clan banners since `PrepareGuardAgentDataFromGarrison` uses `settlement.OwnerClan.Banner` |
| **Patch3_SetRace** | CHECK | Race assignment patches may interact with guard monster selection. Verify custom guard troops have correct `Race` attribute |
| **Patch5_FaceGen** | CHECK | Face generation for custom races. Guards use `FaceGen.GetMonsterWithSuffix(race, "_settlement")` — ensure `_settlement` monster variants exist for all TAOM races |

### Empty Garrison Fallback

When garrison is empty (or all troops are wounded), the system falls back to `culture.Guard`. With per-settlement config, this fallback should also be configurable:

```
If garrison not empty → weighted random from garrison (vanilla)
If garrison empty AND settlement has config → weighted random from config
If garrison empty AND no config → culture.Guard (vanilla fallback)
```

### Action Set Requirements

Guards need action sets with these suffixes for each monster/race:
- `_guard` — used by all armed guards
- `_unarmed_guard` — used by unarmed wandering guards

If TAOM custom races (orc, elf, dwarf, hobbit) don't have `{monster}_guard` and `{monster}_unarmed_guard` action sets, guards will T-pose or crash. Verify in `action_sets.xml`.

### Unarmed Guard Equipment Stripping

Unarmed guards lose: all 4 weapon slots, shield, head armor, AND gloves. They keep body armor, leg armor, and cape. For races with non-standard equipment mapping (e.g., if orcs use different slot assignments), verify the stripped slots produce the intended visual.

### Village Guards — NOT SUPPORTED

Villages are gated out by `settlement.IsFortification`. To add village guards, you would need:
1. A separate `CampaignBehavior` that listens to `LocationCharactersAreReadyToSpawn`
2. Check for village settlements
3. Village scenes would need `sp_guard` spawn points added in the scene editor
4. This is a significant additional scope item

### Prison Guard Exception

Prison guards use `culture.PrisonGuard` directly and bypass the garrison roster entirely. Per-settlement prison guard customization requires either:
- Patching `CreatePrisonGuard` separately
- Including `<PrisonGuard>` in the settlement config (as shown in Section C)

---

## F. Guard Type Summary Table

| Guard Type | Spawn Point | Behavior | Equipment | Source | Prosperity Scaled | Notes |
|-----------|-------------|----------|-----------|--------|-------------------|-------|
| Castle Guard | `sp_guard_castle` | StandGuard | Forced spear in slot 0 | Garrison roster | NO | Elite positions |
| Stand Guard | `sp_guard` | StandGuard | Normal (1 weapon removed) | Garrison roster | YES | Standard posts |
| Spear Guard | `sp_guard_with_spear` | StandGuard | Forced spear in slot 0 | Garrison roster | YES | Ceremonial positions |
| Patrol Guard | `sp_guard_patrol` | PatrollingGuard | Normal (1 weapon removed) | Garrison roster | YES | Walking routes |
| Unarmed Guard | `sp_guard_unarmed` | OutdoorWanderer | Stripped (weapons+helm+gloves) | Garrison roster | YES × castle ratio | Off-duty, no lordshall |
| Prison Guard | `sp_prison_guard` | StandGuard | Full equipment (no modification) | culture.PrisonGuard | NO | Bypasses garrison |

---

## G. Existing TAOM Guard Infrastructure

### Current Guard Characters per Culture

All 16 TAOM cultures define these guard NPCs:
- `guard_{culture}` — Level 16 soldier, default settlement guard
- `prison_guard_{culture}` — Prison guard character
- `caravan_guard_{culture}` — Caravan escort
- `veteran_caravan_guard_{culture}` — Elite caravan escort
- `gangleader_bodyguard_{culture}` — Gang leader protection

### Current Patrol Templates

3 levels per culture in `taom_partyTemplates.xml`:
- Level 1: Militia-tier (8-15 troops)
- Level 2: Regular-tier (15-20 troops)
- Level 3: Elite-tier (20-25 troops)

### What Doesn't Exist Yet

- No per-settlement guard troop definitions
- No settlement-specific guard characters (e.g., "Citadel Guard" vs "Fountain Guard")
- No per-settlement patrol template overrides
- No per-culture spear item mapping
- No village guard system

---

## H. Recommended Implementation Architecture

```
Main/Features/SettlementGuards/
├── ISettlementGuardService.cs          — Service interface
├── SettlementGuardService.cs           — XML config loading, fallback resolution
├── SettlementGuardConfig.cs            — Data model (settlement → guard pool)
├── Hooks/
│   ├── GuardsCampaignBehavior_TakeGuardAgentData_Patch.cs  — Rank 1 interception
│   └── GuardsCampaignBehavior_GetSuitableSpear_Patch.cs    — Spear culture fix
├── Models/
│   └── TaomSettlementPatrolModel.cs    — Per-settlement patrol templates
└── Adapters/
    ├── ISettlementAdapter.cs           — Settlement abstraction
    └── ICultureAdapter.cs              — Already exists in TAOM
```

Config: `Main/_Module/ModuleData/settlement_guards/settlement_guards_config.xml`
