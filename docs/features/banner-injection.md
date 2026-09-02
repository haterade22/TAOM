# Banner Injection

## Overview
Banner Injection ensures that TAOM factions (kingdoms and clans) display the correct lore-accurate banners on the campaign map, in battles, and on encyclopedia pages. On every session launch it reads expected `banner_key` values from the mod's XML and XSLT data files and overwrites any mismatched banners in the live game state. Player-customised clan banners are tracked across saves and left untouched. The 32-layer banner cap this feature once worked around is gone: the transpiler that lifted it moved to BannerColorPersistence in 2026-04, and v1.4.7 made layers natively unlimited, so `Patch15_BannerLayerLimit` now ships disabled and self-bails.

## Why This Exists
- **Vanilla behavior:** Bannerlord reads banner codes from save data. Once a save exists, the in-memory banner for a kingdom or clan may drift from what the mod XML declares — especially after mod updates that change heraldry, or after Bannerlord resets banners during campaign init.
- **TAOM requirement:** LOTR factions require specific heraldic designs (e.g., the White Tree of Gondor, the Eye of Sauron). These cannot be left to drift or be reset by vanilla systems.
- **Without this feature:** Factions display default or corrupted banners, breaking visual identity on the map, in battle, and in the encyclopedia.

## Architecture

### Design Challenge
The game must be told which banners are canonical (from XML/XSLT data), while still respecting
player choices (the player may edit their clan banner via the in-game banner editor).

This feature once also carried a layer-limit workaround. It no longer does: `BannerLayerExpander`
and `Banner_TryGetBannerDataFromCode_Patch` were deleted in 2026-04 when the transpiler moved to
BannerColorPersistence, and the engine removed the cap in v1.4.7. The rows below describe the
current file set only.

### Solution Approach
- `BannerConfigProvider` reads `banner_key` attributes from `taom_spkingdoms.xml`, `spkingdoms.xslt`, `characters/clans.xml`, and `spclans.xslt`. Both plain XML element attributes and XSLT template match-attribute patterns are parsed.
- `BannerInjectionService` iterates all kingdoms and non-ruling clans, compares live `BannerCode` against the expected key, skips player-modified entries, then calls the adapter to set and invalidate visuals. Ruling clans are skipped deliberately, because vanilla re-paints a ruling clan's banner from its kingdom every time. The consequence worth knowing: `clan_<kingdom>_1` displays the kingdom banner while its `color`/`color2` still tint its own troops (see [clan-heraldry.md](clan-heraldry.md)).
- `BannerExclusionService` maintains a `HashSet<string>` of player-modified entity IDs. It is serialised into save data via `IDataStore.SyncData` under the key `_taom_playerModifiedBanners`.
- `GauntletBannerEditorScreen_OnDone_Patch` (Harmony Postfix on `GauntletBannerEditorScreen.OnDone`) detects when the player saves a banner edit and records `Clan.PlayerClan.StringId` into `BannerExclusionService`.
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
| `Main/Features/BannerInjection/BannerInjectionIoC.cs` | Registers all services; resolves and passes hook to Harmony patch |
| `Main/Features/BannerInjection/Hooks/IOnBannerEditorDone.cs` | Hook interface bridging Harmony patch to service |
| `Main/Features/BannerInjection/Hooks/BannerEditorDoneHook.cs` | Delegates `OnBannerEditorDone` to `IBannerExclusionService.MarkAsPlayerModified` |
| `Main/Features/BannerInjection/Hooks/GauntletBannerEditorScreen_OnDone_Patch.cs` | Harmony Postfix on `GauntletBannerEditorScreen.OnDone`; forwards player clan id |

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

## How to Add a New Kingdom or Clan Banner
1. Open `Main/_Module/ModuleData/taom_spkingdoms.xml` (for a custom kingdom) or `Main/_Module/ModuleData/characters/clans.xml` (for a custom clan).
2. Add or update the `banner_key="..."` attribute on the relevant `<Kingdom>` or `<Faction>` element with the Bannerlord-format banner code string.
3. For vanilla kingdoms/clans being overridden, add an `<xsl:template match="Kingdom[@id='...']">` block in `spkingdoms.xslt` or `spclans.xslt` containing `<xsl:attribute name="banner_key">your_code</xsl:attribute>`.
4. The injection service picks up the new key automatically on next session launch — no code changes required.

## Changelog
- 2026-05-13 — Phase 9b: fixed `BannerExclusionService` singleton not resetting between campaigns; added `Reset()` + `OnNewGameCreatedEvent` reset before injection so canon banners re-inject in later campaigns (closes #124).
- 2026-04-06 — Layer-limit handling moved into BannerColorPersistence: replaced the `Banner_TryGetBannerDataFromCode_Patch` postfix with an IL transpiler and deleted `BannerLayerExpander.cs`.
- 2026-04-02 — Injection now fires once on new-game creation and save load (`OnNewGameCreatedEvent` + `OnGameLoadedEvent`) instead of on every session launch / battle return.
- 2026-03-06 — Initial port of the Banner Injection system: re-applies `banner_key` values to kingdoms and clans, with player-modified-banner exclusion and config parsing from XML + XSLT sources.

## GitHub Issue
- **Issue:** Unknown
- **Status:** Unknown

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/INDEX.md](../INDEX.md)

<!-- backlinks-end -->
