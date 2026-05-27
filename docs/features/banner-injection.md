# Banner Injection

## Overview
Banner Injection ensures that TAOM factions (kingdoms and clans) display the correct lore-accurate banners on the campaign map, in battles, and on encyclopedia pages. On every session launch it reads expected `banner_key` values from the mod's XML and XSLT data files and overwrites any mismatched banners in the live game state. Player-customised clan banners are tracked across saves and left untouched. A separate Harmony patch lifts Bannerlord's hardcoded 32-layer banner limit so complex TAOM heraldry renders correctly.

## Why This Exists
- **Vanilla behavior:** Bannerlord reads banner codes from save data. Once a save exists, the in-memory banner for a kingdom or clan may drift from what the mod XML declares — especially after mod updates that change heraldry, or after Bannerlord resets banners during campaign init.
- **TAOM requirement:** LOTR factions require specific heraldic designs (e.g., the White Tree of Gondor, the Eye of Sauron). These cannot be left to drift or be reset by vanilla systems.
- **Without this feature:** Factions display default or corrupted banners, breaking visual identity on the map, in battle, and in the encyclopedia.

## Architecture

### Design Challenge
Two distinct problems must be solved together:
1. **Banner re-injection:** The game must be told which banners are canonical (from XML/XSLT data), while still respecting player choices (player may edit their clan banner via the in-game banner editor).
2. **Layer limit:** Bannerlord's `Banner.TryGetBannerDataFromCode` silently truncates any banner with more than 32 layers. Complex TAOM heraldry needs more layers.

### Solution Approach
- `BannerConfigProvider` reads `banner_key` attributes from `taom_spkingdoms.xml`, `spkingdoms.xslt`, `characters/clans.xml`, and `spclans.xslt`. Both plain XML element attributes and XSLT template match-attribute patterns are parsed.
- `BannerInjectionService` iterates all kingdoms and non-ruling clans, compares live `BannerCode` against the expected key, skips player-modified entries, then calls the adapter to set and invalidate visuals.
- `BannerExclusionService` maintains a `HashSet<string>` of player-modified entity IDs. It is serialised into save data via `IDataStore.SyncData` under the key `_taom_playerModifiedBanners`.
- `GauntletBannerEditorScreen_OnDone_Patch` (Harmony Postfix on `GauntletBannerEditorScreen.OnDone`) detects when the player saves a banner edit and records `Clan.PlayerClan.StringId` into `BannerExclusionService`.
- `Banner_TryGetBannerDataFromCode_Patch` (Harmony Postfix on `Banner.TryGetBannerDataFromCode`) intercepts the parsed layer list. If the raw banner code contains more layers than the 32 returned by the engine, it re-parses all layers using `BannerLayerExpander` and replaces the list.
- `BannerInjectionBehavior` (`CampaignBehaviorBase`) calls `InjectBanners()` on `CampaignEvents.OnNewGameCreatedEvent` and `CampaignEvents.OnGameLoadedEvent` (fires exactly once per game start or save load), and delegates `SyncData` to the service.

### Component Diagram
```
CampaignEvents.OnNewGameCreatedEvent  ─┐
CampaignEvents.OnGameLoadedEvent      ─┘
  `-- BannerInjectionBehavior.RegisterEvents()
        `-- IBannerInjectionService.InjectBanners()
              |-- IBannerConfigProvider.GetKingdomBannerKeys()  [parses XML + XSLT]
              |-- IBannerConfigProvider.GetClanBannerKeys()
              |-- IKingdomBannerAdapter.GetAllKingdoms()
              |-- IBannerExclusionService.IsPlayerModified(id)
              `-- IKingdomBannerAdapter.SetBanner() + InvalidateVisuals()

GauntletBannerEditorScreen.OnDone (Harmony Postfix)
  `-- IOnBannerEditorDone.OnBannerEditorDone(clanId)
        `-- IBannerExclusionService.MarkAsPlayerModified(clanId)

Banner.TryGetBannerDataFromCode (Harmony Postfix)
  `-- BannerLayerExpander.ParseAllLayers(bannerCode)
        `-- replaces bannerDataList if > 32 layers present

IDataStore.SyncData
  `-- BannerExclusionService serialises _playerModifiedIds
```

## Configuration
Banner keys are declared in:
- `Main/_Module/ModuleData/taom_spkingdoms.xml` — custom TAOM kingdoms, `banner_key` attribute on `<Kingdom>` elements
- `Main/_Module/ModuleData/spkingdoms.xslt` — vanilla kingdom overrides via `<xsl:template match="Kingdom[@id='...']">` containing `<xsl:attribute name="banner_key">`
- `Main/_Module/ModuleData/characters/clans.xml` — custom TAOM clans, `banner_key` attribute on `<Faction>` elements
- `Main/_Module/ModuleData/spclans.xslt` — vanilla clan overrides, same XSLT pattern

## Key Files
| File | Purpose |
|------|---------|
| `Main/Features/BannerInjection/BannerInjectionBehavior.cs` | `CampaignBehaviorBase` entry point; hooks session launch event |
| `Main/Features/BannerInjection/IBannerInjectionService.cs` | Service interface: `InjectBanners()`, `SyncData()` |
| `Main/Features/BannerInjection/BannerInjectionService.cs` | Core injection logic; skips player-modified and already-correct banners |
| `Main/Features/BannerInjection/IBannerExclusionService.cs` | Interface for tracking player-modified banner IDs |
| `Main/Features/BannerInjection/BannerExclusionService.cs` | `HashSet`-backed exclusion list; serialised to save via `IDataStore` |
| `Main/Features/BannerInjection/IBannerConfigProvider.cs` | Interface for reading expected banner keys from data files |
| `Main/Features/BannerInjection/BannerConfigProvider.cs` | Parses XML element attributes and XSLT template match patterns |
| `Main/Features/BannerInjection/BannerLayerExpander.cs` | Parses a `banner_key` string into 10-field-per-layer int arrays |
| `Main/Features/BannerInjection/BannerInjectionIoC.cs` | Registers all services; resolves and passes hook to Harmony patch |
| `Main/Features/BannerInjection/Hooks/IOnBannerEditorDone.cs` | Hook interface bridging Harmony patch to service |
| `Main/Features/BannerInjection/Hooks/BannerEditorDoneHook.cs` | Delegates `OnBannerEditorDone` to `IBannerExclusionService.MarkAsPlayerModified` |
| `Main/Features/BannerInjection/Hooks/GauntletBannerEditorScreen_OnDone_Patch.cs` | Harmony Postfix on `GauntletBannerEditorScreen.OnDone`; forwards player clan id |
| `Main/Features/BannerInjection/Hooks/Banner_TryGetBannerDataFromCode_Patch.cs` | Harmony Postfix on `Banner.TryGetBannerDataFromCode`; lifts 32-layer limit |

## Dependencies
- `IKingdomBannerAdapter` — wraps sealed `Kingdom` type; provides `GetAllKingdoms()`, `SetBanner()`, `InvalidateVisuals()`
- `IClanBannerAdapter` — wraps sealed `Clan` type; same surface
- `IPathService` — provides `ModuleDataPath` for locating XML/XSLT files
- `IModLogger` — used throughout for info, warning, and error logging

## Tests
| Test File | Coverage |
|-----------|---------|
| `TAOM.Tests/Features/BannerInjection/BannerInjectionServiceTests.cs` | Injection logic: mismatch injects, already-correct skips, player-modified skips, ruling-clan skips, `InvalidateVisuals` called on update, summary log emitted, multi-kingdom batch |
| `TAOM.Tests/Features/BannerInjection/BannerExclusionServiceTests.cs` | `MarkAsPlayerModified`, `IsPlayerModified`, `SyncData` round-trip |
| `TAOM.Tests/Features/BannerInjection/BannerConfigProviderTests.cs` | XML attribute parsing, XSLT template match parsing, missing file warnings |
| `TAOM.Tests/Features/BannerInjection/BannerLayerExpanderTests.cs` | Empty string, valid multi-layer codes, malformed codes return empty |

## How to Add a New Kingdom or Clan Banner
1. Open `Main/_Module/ModuleData/taom_spkingdoms.xml` (for a custom kingdom) or `Main/_Module/ModuleData/characters/clans.xml` (for a custom clan).
2. Add or update the `banner_key="..."` attribute on the relevant `<Kingdom>` or `<Faction>` element with the Bannerlord-format banner code string.
3. For vanilla kingdoms/clans being overridden, add an `<xsl:template match="Kingdom[@id='...']">` block in `spkingdoms.xslt` or `spclans.xslt` containing `<xsl:attribute name="banner_key">your_code</xsl:attribute>`.
4. The injection service picks up the new key automatically on next session launch — no code changes required.

## GitHub Issue
- **Issue:** Unknown
- **Status:** Unknown

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/INDEX.md](../INDEX.md)

<!-- backlinks-end -->
