# BannerColorPersistence

## Overview

Ensures the player's custom clan banner colors persist correctly across all UI screens and prevent vanilla systems from overwriting lore-accurate injected banners mid-campaign. Replaces TAOM's old string-parsing postfix patch with a superior IL transpiler and adds drift guard + full UI coverage.

## Why This Exists

Three active bugs in TAOM's banner system before this feature:

1. **Layer limit**: TAOM's old `Banner_TryGetBannerDataFromCode_Patch` was a Postfix that re-parsed the banner string after `RemoveRange` had already cut it — allocations and wasted work every render call.
2. **Color drift**: Vanilla `Clan.UpdateBannerColorsAccordingToKingdom` (private, called frequently during War of the Ring events) silently overwrote injected lore-accurate banners to match the kingdom's current primary color. No protection existed.
3. **UI color loss**: Player's custom clan colors reset to kingdom colors in inventory, party screen, character sheet, encyclopedia characters, and battle — because vanilla `CampaignUIHelper.GetCharacterCode` and related VMs read colors from the kingdom's faction rather than the clan.

## Architecture

### Design Challenge

Vanilla reads banner colors from `hero.MapFaction` (the kingdom), not from the player's clan. For LOTR factions where each clan has unique lore colors, this causes all characters to appear in generic kingdom colors rather than their clan's correct colors.

### Solution Approach

```
Patch15_BannerLayerLimit (Transpiler)
  └── IBannerColorConfigProvider → BannerColorConfig (JSON)
      └── IBannerColorService → enabled/drift guard/paste flags

Patch24_BannerDriftGuard
  └── Clan.UpdateBannerColorsAccordingToKingdom — Prefix, skip when enabled
  └── Clan.UpdateBannerColor — Postfix, sync kingdom colors from ruling clan

Patch23_BannerColorPersistence (11 patches + 1 manual)
  └── GetCharacterCode (×2), SPInventory, PartyVM, HeroVM, PartyCharacterVM,
      ClanPartyItemVM, Mission.SpawnAgent, NotificationHelper, Banner.GetFirstIconColor,
      BannerEditorView.OnTick (BannerPaste)
  └── MobilePartyVisual.AddCharacterToPartyIcon (manual reflection patch)
      └── IBannerHeroAdapter → extracts ClanColorInfo from CharacterObject/Hero/Clan boundary
```

### Component Diagram

```
SubModule.OnSubModuleLoad()
    ├── Banner_TryGetBannerDataFromCode_Transpiler.Initialize(config, logger)
    ├── Clan_UpdateBannerColorsAccordingToKingdom_Patch.Initialize(service)
    ├── Clan_UpdateBannerColor_Patch.Initialize(service, heroAdapter)
    └── [12 UI patches].Initialize(service, heroAdapter)

SubModule.OnGameInitializationFinished()
    ├── harmony.PatchCategory("Patch23_BannerColorPersistence")
    ├── harmony.PatchCategory("Patch24_BannerDriftGuard")
    └── harmony.Patch(MobilePartyVisual.AddCharacterToPartyIcon via reflection)
```

### Key Design Decisions

- **Transpiler over Postfix for layer limit**: The transpiler inserts a `Brtrue_S` before `Ble_S` to skip the `RemoveRange` block entirely — zero allocations, zero string parsing, nothing to undo.
- **Clan directly in SyncKingdomColors**: The `IBannerHeroAdapter.SyncKingdomColors` accepts `Clan` directly (not a string ID) to avoid LINQ enumeration at the adapter boundary.
- **CharacterCode is mutable**: `CharacterCode.Color1`/`Color2` are settable properties on the class instance — all UI patches mutate the returned `__result` directly rather than creating a new instance (no `CreateFrom` roundtrip needed).
- **MobilePartyVisual patched manually**: The target method is private with an 11-parameter signature including `in ActionIndexCache` params. Harmony's category system cannot auto-discover private methods; it's patched manually via `AccessTools` in `OnGameInitializationFinished`.

## Configuration

`Main/_Module/ModuleData/configs/banner_color_config.json`:

```json
{
  "EnableColorPersistence": true,
  "EnableDriftGuard": true,
  "EnableBannerPaste": true,
  "EnableUniqueSecondaryColor": true,
  "EnableLayerLimitTranspiler": true
}
```

All flags default to `true`. Set individual flags to `false` to disable specific sub-features without disabling the entire feature.

## Key Files

| File | Purpose |
|------|---------|
| `BannerColorConfig.cs` | POCO with 7 bool flags |
| `IBannerColorConfigProvider.cs` / `BannerColorConfigProvider.cs` | Reads JSON config, lazy cache |
| `IBannerColorService.cs` / `BannerColorService.cs` | Pure logic: enabled checks, unique icon color calculation |
| `Main/Adapters/ClanColorInfo.cs` | `readonly struct` carrying ClanStringId, Color1, Color2 across the sealed-type boundary |
| `Main/Adapters/IBannerHeroAdapter.cs` / `BannerHeroAdapter.cs` | Extracts clan colors from `CharacterObject`/`Hero`/`Clan`; syncs kingdom colors for ruling clans |
| `IAgentColorStore.cs` / `AgentColorStore.cs` | Per-mission agent color cache (`ConcurrentDictionary<int, ClanColorInfo>`) |
| `AgentColorStoreCleanupBehavior.cs` | MissionBehavior that clears the agent color store on mission end |
| `BannerColorPersistenceIoC.cs` | Registers all 4 singletons |
| `Hooks/Banner_TryGetBannerDataFromCode_Transpiler.cs` | Patch15 — IL transpiler skipping RemoveRange |
| `Hooks/Clan_UpdateBannerColorsAccordingToKingdom_Patch.cs` | Patch24 — drift guard Prefix |
| `Hooks/Clan_UpdateBannerColor_Patch.cs` | Patch24 — kingdom color sync Postfix |
| `Hooks/BannerEditorView_OnTick_Patch.cs` | Patch23 — BannerPaste Ctrl+C/V; `MethodInfo` cached at Initialize |
| `Hooks/MobilePartyVisual_AddCharacterToPartyIcon_Patch.cs` | No category — manual patch via reflection |
| `Hooks/Agent_EquipItemsFromSpawnEquipment_Patch.cs` | Patch23 — registers agent in color store + resolves clan colors |
| `Hooks/AgentVisuals_Create_Patch.cs` | Manual patch — disables color randomness when clan colors set |
| `Hooks/MapConversationTableau_SpawnOpponentLeader_Patch.cs` | Manual patch — conversation leader clan colors |
| `Hooks/MapConversationTableau_SpawnOpponentBodyguard_Patch.cs` | Manual patch — conversation bodyguard clan colors |
| `Hooks/OrderOfBattleHeroItemVM_RefreshInformation_Patch.cs` | Patch23 — pre-battle deployment screen colors |
| `TAOM.Tests/Features/BannerColorPersistence/` | 5 test files, 22 tests |

## Dependencies

- `IBannerColorConfigProvider` → reads `configs/banner_color_config.json` via `IPathService.ConfigPath`
- `IPathService` (Core.Infrastructure) — resolves module root path
- `IModLogger` (Core.Logging) — used in transpiler and BannerPaste patches
- Harmony 2.4.2 — IL transpiler, AccessTools for private method reflection
- `SandBox.GauntletUI.BannerEditor.BannerEditorView` — in `SandBox.GauntletUI.dll` (loaded before OnGameInitializationFinished)

## Tests

`TAOM.Tests/Features/BannerColorPersistence/`

| Test File | Coverage |
|-----------|---------|
| `BannerColorServiceTests.cs` | 14 tests — all `IBannerColorService` methods, enabled/disabled, unique icon color, agent visual + tableau flags |
| `AgentColorStoreTests.cs` | 4 tests — register, overwrite, unregistered lookup, clear |
| `BannerColorConfigProviderTests.cs` | 4 tests — valid JSON, missing file (defaults), invalid JSON (defaults), caching |
| `Clan_UpdateBannerColorsAccordingToKingdom_PatchTests.cs` | 3 tests — null service, disabled, null instance guards |
| `Clan_UpdateBannerColor_PatchTests.cs` | 3 tests — null service, disabled, null instance guards |

Harmony patches themselves are not unit-testable (require live game) per ADR-008.

## How-To

### Disable drift guard only

Set `"EnableDriftGuard": false` in `banner_color_config.json`.

### Add a new UI screen that loses clan colors

1. Identify the ViewModel method that calls `GetCharacterCode` or sets `ArmorColor1/2`
2. Add a new Postfix patch in `Hooks/` with `[HarmonyPatchCategory("Patch23_BannerColorPersistence")]`
3. Use `_heroAdapter.GetClanColorInfo(characterObject)` → `_service.ShouldUseClanColor(info)` → mutate `__result.Color1/2`
4. Add `Initialize(service, heroAdapter)` + static fields
5. Register in SubModule `OnSubModuleLoad` Initialize calls

### Verify BannerPaste is working

Open the Banner Editor in-game, copy another clan's banner code from the encyclopedia, then Ctrl+V in the banner editor. The shield and character model should update immediately.
