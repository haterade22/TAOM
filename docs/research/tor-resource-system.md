# TOR_Core Custom Resource System — Research Reference

> Reverse-engineered from [TOR_Core](https://github.com/TheOldRealms/TOR_Core/tree/development) `development` branch, April 2026.

---

## 1. System Overview

```
                          ┌─────────────────────────┐
                          │  CustomResourceManager   │ (Singleton)
                          │  - _resources: Dict<>    │
                          │  - _resourceChanges[]    │
                          │  - _massBudget{}         │
                          └────┬───────────┬─────────┘
                               │           │
              ┌────────────────┤           ├──────────────────┐
              ▼                ▼           ▼                  ▼
    ┌──────────────┐  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐
    │ CustomResource│  │CampaignEvents│  │ScreenManager │  │ HeroExtended │
    │ (data model)  │  │ (7 events)   │  │ (party screen│  │ Info         │
    │ StringId      │  │ battles,     │  │  push/pop)   │  │ [SaveableF.] │
    │ Name, Icons   │  │ tournaments, │  └──────────────┘  │ Dict<str,f>  │
    │ Cultures[]    │  │ level-ups... │                     └──────────────┘
    └──────────────┘  └──────────────┘
              │                                                    ▲
              ▼                                                    │
    ┌──────────────────┐    ┌─────────────────────┐    ┌──────────┴────────┐
    │ TORCustomResource│    │ CustomResourcePatches│    │ *Behavior classes │
    │ Model (GameModel)│    │ (4 Harmony patches)  │    │ Teef, OathGold,  │
    │ - battle gains   │    │ - PartyCharacterVM   │    │ Prestige, Favor, │
    │ - upkeep calcs   │    │ - PartyVM transfers  │    │ Waaagh           │
    │ - cost factors   │    │ - PartyScreenLogic   │    └──────────────────┘
    └──────────────────┘    └──────────────────────┘
```

**Key pattern:** Resources are identified by string ID, stored as `Dictionary<string, float>` on `HeroExtendedInfo` (a custom save-data extension), earned via `CampaignEvent` handlers in `CustomResourceManager`, spent via Harmony-patched party screen, and displayed via tooltip/icon injection.

---

## 2. Core Classes

### CustomResource (Data Model)
**File:** `CSharpSourceCode/CampaignMechanics/CustomResources/CustomResource.cs`

```csharp
public class CustomResource
{
    public string StringId { get; private set; }       // "Teef", "OathGold", etc.
    public TextObject Name { get; private set; }        // Localized from GameTexts
    public TextObject Description { get; private set; } // Localized from GameTexts
    public string SmallIconName { get; private set; }   // "teef_icon_45"
    public string LargeIconName { get; private set; }   // "teef_icon_100" (auto-derived)
    public List<string> Cultures { get; private set; }  // ["greenskin"] — ONE primary culture per resource

    // Constructor: id + icon + culture(s) + optional tooltip delegate
    // GetCustomResourceIconAsText() → "<img src=\"teef_icon_45\"/>"
    // GetCustomResourceGeneralizedFactor() → delegates to TORCustomResourceModel
}
```

### CustomResourceManager (Singleton Orchestrator)
**File:** `CSharpSourceCode/CampaignMechanics/CustomResources/CustomResourceManager.cs`

**Initialization:** `Initialize()` creates singleton, registers 10 resources:

| StringId | Icon | Culture(s) | Tooltip Helper |
|----------|------|------------|----------------|
| Prestige | `prestige_icon_45` | EMPIRE | — |
| Chivalry | `chivalry_icon_45` | BRETONNIA | `ChivalryHelper.GetChivalryInfo` |
| DarkEnergy | `darkenergy_icon_45` | SYLVANIA, MOUSILLON | — |
| ForestHarmony | `harmony_icon_45` | ASRAI | `ForestHarmonyHelper.GetForestHarmonyInfo` |
| CouncilFavor | `favor_icon_45` | EONIR | `FavorHelper.GetFavorInfo` |
| OathGold | `oathgold_icon_45` | DAWI | `OathGoldHelper.GetOathGoldInfo` |
| Teef | `teef_icon_45` | GREENSKIN | `TeefHelper.GetTeefInfo` |
| Meat | `meat_icon_45` | (neutral) | — |
| Waaagh | `winds_icon_45` | null (no culture) | — |
| WindsOfMagic | `winds_icon_45` | (neutral) | — |

**Critical design rule:** Only ONE primary resource per culture. `GetCultureSpecificCustomResource()` uses `FirstOrDefault` — multiple resources per culture would break this lookup.

**CampaignEvent subscriptions (7 events):**
1. `OnMissionStartedEvent` → Snapshot initial combat strength ratio
2. `OnPlayerBattleEndEvent` → Calculate resource gain from battle
3. `OnHideoutBattleCompletedEvent` → Hideout completion bonus
4. `HeroPrisonerReleased` → Prisoner release bonus (Bretonnia: Chivalry)
5. `TournamentFinished` → Tournament win bonus (culture-specific)
6. `HeroLevelledUp` → Level-up bonus (10 base, +25% undead)
7. `OnIssueUpdatedEvent` → Issue completion bonus (15 base, +25% undead)

**Party screen integration:**
- Hooks into `ScreenManager.OnPushScreen`/`OnPopScreen` to detect party screen open/close
- Tracks pending resource changes in `_resourceChanges[]` during screen session
- `_massBudget{}` pre-calculates available budget for mass upgrade clamping
- Changes applied on screen close, discarded on cancel

### HeroExtendedInfo (Save/Load)
**File:** `CSharpSourceCode/Extensions/ExtendedInfoSystem/HeroExtendedInfo.cs`

```csharp
[SaveableField(2)] public Dictionary<string, float> CustomResources = [];

public void AddCustomResource(string id, float amount)
{
    // Add or create entry, clamp to [0, MaximumCustomResourceValue(5000)]
    // WindsOfMagic has separate MaxWindsOfMagic cap
}

public float GetCustomResourceValue(string id)
{
    return CustomResources.ContainsKey(id) ? CustomResources[id] : 0;
}
```

**Save mechanism:** TaleWorlds `[SaveableField]` attribute on `HeroExtendedInfo` — automatically serialized/deserialized by the engine's save system. No custom save handler needed.

**Manager:** `ExtendedInfoManager` (CampaignBehaviorBase) manages `Dictionary<string, HeroExtendedInfo>` per hero. Daily tick calls resource upkeep calculations.

### TORCustomResourceModel (GameModel)
**File:** `CSharpSourceCode/Models/TORCustomResourceModel.cs`

Provides culture-specific calculation factors:
- `GetFactorForGeneralizedCosts(CustomResource)` — cost multiplier per resource
- Battle gain formulas with culture bonuses (e.g., Asrai +3x vs Beastmen, Dwarf tier-based)
- Upkeep/garrison cost modifiers

---

## 3. Resource Catalog

| Resource | Culture | Primary Earning | Primary Spending | Cap | AI? |
|----------|---------|----------------|-----------------|-----|-----|
| **Teef** | Greenskin | Battle kills (2×tier), item exchange (÷400), gold conversion (÷100) | Troop upgrades | 5,000 | No |
| **OathGold** | Dwarf | Metal delivery, troop donation, grain, tournaments | Troop upgrades, guild unlocks (Rangers, Ironbreakers) | 2,000/guild | No |
| **Chivalry** | Bretonnia | Prisoner release (50+), battles, hideouts, level-ups, tournaments | Passive: affects morale (−75% to +20%) and knight wages (+75% to −20%) | 5,000 | No |
| **Prestige** | Empire | Gold conversion (500:1), issue completion, hideouts | Troop upgrades (1-12 per unit), Demigryph mount (1,000) | 5,000 | No |
| **CouncilFavor** | Eonir | Prisoner release, level-ups, issues | Envoy services (200-500) | 5,000 | No |
| **ForestHarmony** | Asrai | Beastmen prisoners (×3), hideouts in Athel Loren (×3) | Passive: health/regen/winds debuffs at low levels | 5,000 | No |
| **DarkEnergy** | Sylvania/Mousillon | Battle casualties, level-ups (+25%), issues (+25%) | Troop upgrades | 5,000 | No |
| **Waaagh** | (none/Greenskin) | Battle wins (20-100% based on difficulty) | Passive: morale (−40 to normal), damage (−20% to +20%) | 1,000 | No |

**All resources are player-only.** AI parties do not earn or spend custom resources.

---

## 4. TeefBehavior Deep Dive

**File:** `CSharpSourceCode/CampaignMechanics/CustomResourceBehavior/TeefBehavior.cs`

### Lifecycle
1. `OnNewGameCreated` → Spawns "Quartermaster" NPC in every Greenskin town
2. `OnSessionLaunched` → Registers dialog lines for Quartermaster interaction

### Earning Teef
| Source | Formula | Trigger |
|--------|---------|---------|
| Battle kills | `Σ(tier × 2) + hero_kills × 50` | `OnPlayerBattleEndEvent` via CustomResourceManager |
| Gold → Teef | `gold ÷ 100` (×2 with career perk) | Dialog: "SpendGold" consequence |
| Items → Teef | `item_value ÷ 400` | Dialog: "OnItemsDiscarded" consequence |
| Level-up | 10 base | `HeroLevelledUp` via CustomResourceManager |
| Issue completion | 15 base | `OnIssueUpdatedEvent` via CustomResourceManager |

### Spending Teef
| Sink | Cost | Mechanism |
|------|------|-----------|
| Troop upgrades | Per-unit from `tor_extendedunitproperties.xml` | Harmony patch on `PartyCharacterVM.InitializeUpgrades` |
| Teef bags (storage) | 1,000 Teef → bag item | Dialog: "MakeTeefBags" |

### Quartermaster NPC
- Spawned at game start in Greenskin towns
- Dialog tree: convert gold → Teef, discard items → Teef, store Teef in bags
- No passive generation; all player-initiated through dialog

---

## 5. Data Schema

### Troop Resource Costs (`tor_extendedunitproperties.xml`)

```xml
<CharacterExtendedInfo id="tor_empire_halberdier">
  <ResourceCost ResourceType="Prestige" UpkeepCost="0" UpgradeCost="1" />
</CharacterExtendedInfo>

<CharacterExtendedInfo id="tor_empire_greatsword">
  <ResourceCost ResourceType="Prestige" UpkeepCost="0" UpgradeCost="4" />
</CharacterExtendedInfo>
```

| Attribute | Type | Purpose |
|-----------|------|---------|
| `id` | string | CharacterObject StringId of the TARGET troop (the one you upgrade TO) |
| `ResourceType` | string | Must match a registered CustomResource StringId |
| `UpkeepCost` | int | Daily cost (currently 0 for all — infrastructure exists but unused) |
| `UpgradeCost` | int | One-time cost to upgrade one troop to this type |

**Loading:** `CharacterExtendedInfo` is loaded by `ExtendedInfoManager` during campaign start. `CharacterObjectExtensions.GetCustomResourceRequiredForUpgrade()` returns `Tuple<CustomResource, int>` or null.

### Config (`tor_config.xml`)
- `MaximumCustomResourceValue` = 5000 (global cap for all resources except WindsOfMagic)

---

## 6. UI Components

### Party Screen Integration (Harmony patches, NOT UIExtenderEx)
TOR does NOT use UIExtenderEx for resource display. Instead:

1. **`PartyCharacterVM.InitializeUpgrades` PREFIX** — Completely replaces vanilla upgrade logic to add resource cost checks alongside gold/XP/item checks. Shows resource icon + amount in upgrade tooltip.

2. **`PartyVM.TransferAllCharacters` PREFIX+POSTFIX** — Snapshots roster before transfer, calculates delta after, applies resource changes.

3. **`PartyVM.OnTransferTroop` POSTFIX** — Handles single troop transfer resource tracking.

4. **`PartyScreenLogic.AddCommand` PREFIX** — Clamps mass upgrade count to available resource budget. Shows "You only have enough {resource} for N upgrade(s)" message.

### Waaagh Meter (Campaign Map HUD)
**Files:**
- `WaaaghBehavior.cs` — CampaignBehavior, daily tick decay (−5), battle gains (20-100%)
- `WaaaghHelper.cs` — Level thresholds, effect calculations
- `WaaaghMeterVM.cs` — ViewModel with 12 bound properties
- `TORMapNotificationView.cs` — Notification overlay
- `GUI/Prefabs/WaaaghMeter.xml` — 80×455px vertical bar, right-side of map

**Waaagh Levels:**

| Level | Threshold | Morale | Damage | Special |
|-------|-----------|--------|--------|---------|
| 0 (Low) | 0 | −40 | −20% | — |
| 1 | 250 | −20 | −10% | — |
| 2 | 600 | 0 | 0 | — |
| 3 (Max) | 900/1000 | +10 | +20% | Drop from 3→2 triggers collapse to 0 |

### Tooltip Resource Display
Resource costs shown inline with upgrade hints:
```
"Required: 4 <img src=\"prestige_icon_45\"/>"
```
Generated by `CustomResourcePatches.GetUpgradeHint()`.

### Sprite Naming Convention
- Small: `{resourceid}_icon_45` (party screen, tooltips)
- Large: `{resourceid}_icon_100` (auto-derived by replacing `_45` with `_100`)
- Waaagh: `waagh1_icon` through `waagh4_icon` (level-specific)

---

## 7. Bannerlord Integration Points

### Harmony Patches (4 patches in `CustomResourcePatches.cs`)

| # | Target | Method | Type | Purpose |
|---|--------|--------|------|---------|
| 1 | `PartyCharacterVM` | `InitializeUpgrades` | Prefix (returns false) | Replace upgrade UI logic to include resource costs |
| 2 | `PartyVM` | `TransferAllCharacters` | Prefix + Postfix | Track mass transfer resource changes |
| 3 | `PartyVM` | `OnTransferTroop` | Postfix | Track single transfer resource changes |
| 4 | `PartyScreenLogic` | `AddCommand` | Prefix | Clamp mass upgrades to resource budget |

### CampaignEvents (7 subscriptions in `CustomResourceManager`)
- `OnMissionStartedEvent`, `OnPlayerBattleEndEvent`, `OnHideoutBattleCompletedEvent`
- `HeroPrisonerReleased`, `TournamentFinished`, `HeroLevelledUp`, `OnIssueUpdatedEvent`

### GameModel
- `TORCustomResourceModel` — provides culture-specific gain/cost multipliers

### Save System
- `HeroExtendedInfo` with `[SaveableField(2)]` — persists via TaleWorlds SaveSystem
- Managed by `ExtendedInfoManager` (CampaignBehaviorBase with `SyncData`)

### Screen Integration
- `ScreenManager.OnPushScreen` / `OnPopScreen` — detect party screen lifecycle

---

## 8. TAOM Portability Assessment

### Ports Directly (Low Effort)
| Component | Notes |
|-----------|-------|
| `CustomResource` data model | Simple POCO, no TOR dependencies |
| `CustomResourceManager` singleton pattern | Replace TOR culture constants with TAOM culture IDs |
| `CustomResourcePatches` Harmony patches | Target same vanilla classes — should work on 1.3.15 |
| `tor_extendedunitproperties.xml` schema | Rename, populate with TAOM troop IDs |
| Tooltip/icon injection pattern | Same Bannerlord UI APIs |

### Needs Adaptation (Medium Effort)
| Component | What Changes |
|-----------|-------------|
| **Save storage** | TOR uses `HeroExtendedInfo` (their custom extension system). TAOM needs its own — either a similar `CampaignBehaviorBase` with `SyncData` dictionary, or a simpler JSON sidecar save |
| **Culture mapping** | TOR hardcodes `TORConstants.Cultures.GREENSKIN` etc. TAOM replaces with `"mordor"`, `"isengard"`, etc. |
| **Battle gain formulas** | TOR has Warhammer-specific bonuses (vs Beastmen, etc.). TAOM needs LOTR equivalents |
| **Behavior classes** | TeefBehavior's Quartermaster NPC dialog is Warhammer-flavored. TAOM needs LOTR equivalent NPCs/dialogs |
| **Career system coupling** | TOR's `HasCareerChoice()` multipliers won't exist in TAOM (unless career system is implemented) |

### Skip Entirely
| Component | Why |
|-----------|-----|
| `WindsOfMagic` resource | Magic system, not applicable |
| `WaaaghMeter` | Greenskin-specific morale mechanic, not needed for MVP |
| `OathGold` guild system | Complex multi-track reputation, too specific to Dwarfs |
| Career perk multipliers | TAOM has no career system yet |
| `CustomResourceContainerScript` | Item-based resource granting — nice-to-have, not MVP |

### Critical Differences from TAOM Architecture
| TOR Pattern | TAOM Equivalent Needed |
|-------------|----------------------|
| Direct `Hero.MainHero` access in services | `IHeroAdapter` wrapping |
| Static singletons (`CustomResourceManager.Instance`) | IoC-registered service (`ISpecialResourceService`) |
| `HeroExtendedInfo` extension system | New adapter: `IHeroResourceAdapter` or `CampaignBehaviorBase` with `SyncData` |
| Hardcoded culture checks in Manager | Data-driven XML config per culture |
| `ExplainedNumber` in Manager methods | Same pattern works — TAOM already uses it |
| `CharacterObjectExtensions` | Need `ICharacterAdapter` or extension method |

### Bannerlord 1.3.15 API Compatibility
| API | Status |
|-----|--------|
| `PartyCharacterVM.InitializeUpgrades` | Exists in 1.3.15 — verify signature |
| `PartyVM.TransferAllCharacters` | Exists — verify parameters |
| `PartyScreenLogic.AddCommand` | Exists — `TotalNumber` field access via reflection may need adjustment |
| `[SaveableField]` attribute | Exists in 1.3.15 SaveSystem |
| `CampaignEvents.*` (7 events) | All exist in 1.3.15 |
| `ScreenManager.OnPushScreen/OnPopScreen` | Exists |

**Risk:** TOR's `PartyScreenLogic.PartyCommand.TotalNumber` is accessed via reflection (field name or backing field). The field name may differ between Bannerlord versions — **must verify against decompiled 1.3.15 source**.

---

## 9. Minimum Viable Port for TAOM

### Phase 1: One Kingdom (Mordor — "War Spoils")

**Must build:**
1. `CustomResource` data model (port directly)
2. `ISpecialResourceService` + `SpecialResourceService` (TAOM adapter pattern)
3. `IHeroResourceAdapter` (wraps hero resource storage)
4. `SpecialResourceBehavior` (CampaignBehaviorBase — earns resources from battles/raids/sieges)
5. `SpecialResourceSaveData` (CampaignBehaviorBase with SyncData for per-hero Dictionary<string, float>)
6. Harmony patches on `PartyCharacterVM.InitializeUpgrades` and `PartyScreenLogic.AddCommand` (port from TOR)
7. XML config: `taom_resource_definitions.xml` (resource ID, culture, icon) + `taom_troop_resource_costs.xml` (per-troop upgrade cost)
8. IoC registration + SubModule.cs registration

**Can defer:**
- Waaagh-style HUD meter
- Quartermaster NPC dialog
- Item-based resource containers
- Guild/reputation sub-systems
- AI resource management

### Estimated Component Count
- 2 interfaces, 2 services, 1-2 adapters, 1 behavior, 1 save handler, 1 Harmony patch class, 2 XML configs, 1 IoC file
- ~8-10 C# files + ~8-10 test files + 2 XML files
