# Bandit Management

## Overview

Replaces vanilla's 5 bandit cultures (`forest_bandits`, `mountain_bandits`, `desert_bandits`, `steppe_bandits`, `sea_raiders`) with lore-appropriate LOTR factions and adds PlayerProgress-driven scaling for hideout density and bandit party size. All scaling is MCM-tunable; defaults give bandits roughly 1.0× vanilla strength in early game and up to 2.5× in endgame.

## Why This Exists

1. Vanilla bandit culture names ("Forest Bandits", "Sea Raiders") are immersion-breaking in a Middle-earth total conversion. LOTR has natural raider-faction analogues — Dunlendings, Rhûn raiders, Haradrim raiders, Gundabad orcs, Corsairs of Umbar.
2. Vanilla bandit scaling caps at PlayerProgress×1.2 — late-game player parties of 200+ T6 troops trivialise the 25-30-troop bandit warbands that vanilla still spawns. The scaling curve is too flat for a total-conversion late game.
3. Vanilla hideout density is hard-coded (9 hideouts/faction max, 3 parties/hideout max). No way to add more without code patching.

## Architecture

Standard TAOM feature module pattern (ADR-002 thin entry, ADR-007 adapter, single-responsibility services). The runtime side has three pieces:

```
TaomBanditDensityModel : DefaultBanditDensityModel       ← GameModel override
    └── delegates 4 properties to IBanditScalingService

Patch39_BanditPartySize (Harmony Postfix)                ← scales party troop counts
    └── targets DefaultPartySizeLimitModel.FindAppropriateInitialRosterForMobileParty
    └── filters to party.IsBandit only

IBanditScalingService                                    ← pure math, no TaleWorlds deps
    └── reads IBanditScalingSettingsProvider (MCM + JSON defaults)
    └── multiplier = 1 + curve * playerProgress    (vanilla floor enforced)
    └── all inputs NaN/Infinity-guarded via FiniteFloatValidator pattern
```

### Scaling formula

For each of `DensityCurve`, `PartySizeCurve`, `BossFightCurve`:

```
multiplier(playerProgress) = 1 + curve × clamp(playerProgress, 0, 1)
```

Defaults (`curve = 1.5`) give:

| PlayerProgress | Multiplier | Effect |
|---|---|---|
| 0.0 (new campaign) | 1.0× | Vanilla density + party sizes |
| 0.5 (mid-campaign) | 1.75× | Hideouts/parties up ~75%; bandit parties 75% larger toward MaxValue |
| 1.0 (endgame) | 2.5× | Up to 2.5× vanilla density + 2.5× troop counts (capped at template MaxValue and MCM caps) |

A negative or NaN curve floors the multiplier at 1.0 — bandits **cannot** become weaker than vanilla through this feature.

### What gets scaled

| Surface | Vanilla | TAOM scaling target |
|---|---|---|
| Hideouts per bandit faction | 9 max | Up to `BanditMaxHideoutsPerFaction` (default 15) |
| Bandit parties per hideout | 3 max | Up to `BanditMaxPartiesPerHideout` (default 5) |
| Troops in hideout first fight | `11 × (2 + PlayerProgress)` | × `BossFightCurve` |
| Troops in hideout boss fight | `1 + 5 × (1 + PlayerProgress)` | × `BossFightCurve` |
| Bandit party troops on map | `min + (max-min) × (0.4 + 0.8 × PlayerProgress)` × random(0.2..0.8) | × `PartySizeCurve`, capped at stack `MaxValue` |

## Configuration

### MCM — Players Tune These

Settings live under **TAOM → World / Bandit Scaling** (`GroupOrder = 35`):

| Setting | Range | Default | Effect |
|---|---|---|---|
| Enable Bandit Scaling | bool | true | Master toggle. Off = vanilla density + party sizes. |
| Density Curve | 0.0 – 5.0 | 1.5 | Multiplier on hideout count + parties/hideout at PlayerProgress=1.0 |
| Party Size Curve | 0.0 – 5.0 | 1.5 | Multiplier on bandit party troop counts at PlayerProgress=1.0 |
| Boss Fight Curve | 0.0 – 5.0 | 1.5 | Multiplier on hideout first-fight + boss-fight troop counts |
| Max Hideouts Per Faction Cap | 1 – 100 | 15 | Hard ceiling regardless of curve |
| Max Parties Per Hideout Cap | 1 – 20 | 5 | Hard ceiling regardless of curve |

### JSON — Defaults & Advanced Tuning

[Main/_Module/ModuleData/bandit_management/bandit_scaling_config.json](../../Main/_Module/ModuleData/bandit_management/bandit_scaling_config.json) holds the fallback defaults applied when an MCM value is corrupted (NaN, Infinity, out-of-range) or when a fresh install runs without MCM ever opened. Same field names as the MCM group.

Per [`.claude/rules/csharp-architecture.md`](../../.claude/rules/csharp-architecture.md) "Config Providers MUST Validate": every numeric field is `FiniteFloatValidator`-guarded, every range is enforced, every invalid value is reverted to the compiled default with a warning log.

## LOTR Bandit Culture Replacement

| Vanilla culture | New culture (StringId) | Display name | Troop source |
|---|---|---|---|
| `forest_bandits` | `dunland_raiders` | Dunlending Raiders | [`troops/troops_dunland.xml`](../../Main/_Module/ModuleData/troops/troops_dunland.xml) |
| `mountain_bandits` | `gundabad_raiders` | Gundabad Orc Raiders | [`troops/troops_gundabad.xml`](../../Main/_Module/ModuleData/troops/troops_gundabad.xml) |
| `desert_bandits` | `harad_raiders` | Haradrim Raiders | [`troops/troops_harad.xml`](../../Main/_Module/ModuleData/troops/troops_harad.xml) |
| `steppe_bandits` | `rhun_raiders` | Rhûn Raiders | [`troops/troops_rhun_new.xml`](../../Main/_Module/ModuleData/troops/troops_rhun_new.xml) |
| `sea_raiders` | `umbar_corsairs` | Corsairs of Umbar | [`troops/troops_umbar.xml`](../../Main/_Module/ModuleData/troops/troops_umbar.xml) |

Each LOTR bandit culture has `is_bandit="true"` (auto-creates a bandit clan at campaign start) and `can_have_settlement="true"` (allows hideout ownership). Troops are pulled from each culture's existing TAOM troop XML — no new troop definitions needed.

### Raider-tier troop roster per culture

T1–T4 troops only (vanilla bandits never field elite kingdom troops):

| Bandit Culture | bandit_bandit (T1) | bandit_raider (T2-3) | bandit_chief (T3) | bandit_boss (T4) |
|---|---|---|---|---|
| `dunland_raiders` | `dunland_peasant` (L6) | `dunland_raider`, `dunland_hunter` (L11) | `dunland_clan_warrior` (L16) | `dunland_wolf_raider` (L21) |
| `rhun_raiders` | `balcoth_volunteer` (L11) | `balcoth_footman`, `kharaghul_rider` (L16) | `balcoth_archer` (L21) | `kharaghul_raider` (L21) |
| `harad_raiders` | `harad_levy` (L6) | `harad_archer`, `harad_camelscout` (L16) | `harad_footman` (L21) | `harad_camelrider` (L21) |
| `gundabad_raiders` | `gundabad_snaga`, `gundabad_hunter` (L11) | `gundabad_grunt`, `gundabad_lurker` (L16) | `gundabad_scout` (L21) | `gundabad_despoiler_of_the_vale` (L26) |
| `umbar_corsairs` | `aux_basic` (L6) | `umbar_elite`, `umbar_elite_root1` (L11-16) | `umbar_elite_root0` (L16) | `umbar_elite_root00` (L21) |

### Hideout migration (one-time)

The 99 existing hideouts in `TAOM_Map/ModuleData/settlements.xml` (external module) had their `culture=` attribute swapped to the new LOTR cultures and their display names rewritten from "Hideout" to "Dunlending Raider's Camp" / "Gundabad Orc Raider's Camp" / "Haradrim Raider's Camp" / "Rhûn Raider's Camp" / "Corsair's Cove". Settlement IDs (`hideout_forest_N`, etc.) were intentionally **left unchanged** to preserve save compatibility — a save started before this migration will load fine and just see the renamed hideouts.

Migration is driven by [`tools/migrate_hideouts_to_lotr.py`](../../tools/migrate_hideouts_to_lotr.py). Safe to re-run idempotently. `--backup` flag writes `.bak` copies of each modified file before overwriting.

## Key Files

| File | Purpose |
|---|---|
| [`Main/Features/BanditManagement/BanditScalingConfig.cs`](../../Main/Features/BanditManagement/BanditScalingConfig.cs) | POCO with curve + cap defaults |
| [`Main/Features/BanditManagement/IBanditScalingConfigProvider.cs`](../../Main/Features/BanditManagement/IBanditScalingConfigProvider.cs) | Interface for JSON loader |
| [`Main/Features/BanditManagement/BanditScalingConfigProvider.cs`](../../Main/Features/BanditManagement/BanditScalingConfigProvider.cs) | Loads + validates `bandit_scaling_config.json` |
| [`Main/Features/BanditManagement/IBanditScalingSettingsProvider.cs`](../../Main/Features/BanditManagement/IBanditScalingSettingsProvider.cs) | Interface for live MCM read |
| [`Main/Features/BanditManagement/BanditScalingSettingsProvider.cs`](../../Main/Features/BanditManagement/BanditScalingSettingsProvider.cs) | NaN-safe MCM read with config-default fallback |
| [`Main/Features/BanditManagement/IBanditScalingService.cs`](../../Main/Features/BanditManagement/IBanditScalingService.cs) | Pure math service |
| [`Main/Features/BanditManagement/BanditScalingService.cs`](../../Main/Features/BanditManagement/BanditScalingService.cs) | `multiplier = 1 + curve * progress` |
| [`Main/Features/BanditManagement/Models/TaomBanditDensityModel.cs`](../../Main/Features/BanditManagement/Models/TaomBanditDensityModel.cs) | GameModel override (hideout count, parties/hideout, fight troops) |
| [`Main/Features/BanditManagement/Hooks/Patch39_BanditPartySize.cs`](../../Main/Features/BanditManagement/Hooks/Patch39_BanditPartySize.cs) | Postfix scaling bandit party rosters toward stack MaxValue |
| [`Main/Features/BanditManagement/BanditManagementIoC.cs`](../../Main/Features/BanditManagement/BanditManagementIoC.cs) | DryIoc registration |
| [`Main/_Module/ModuleData/bandit_management/bandit_scaling_config.json`](../../Main/_Module/ModuleData/bandit_management/bandit_scaling_config.json) | Default config values |
| [`Main/_Module/ModuleData/taom_spcultures.xml`](../../Main/_Module/ModuleData/taom_spcultures.xml) | 5 LOTR bandit culture entries (appended) |
| [`Main/_Module/ModuleData/taom_partyTemplates.xml`](../../Main/_Module/ModuleData/taom_partyTemplates.xml) | 10 raider + boss party templates (appended) |
| [`Main/_Module/ModuleData/taom_module_strings.xml`](../../Main/_Module/ModuleData/taom_module_strings.xml) | Culture display names + male/female names (~80 keys) |
| [`tools/migrate_hideouts_to_lotr.py`](../../tools/migrate_hideouts_to_lotr.py) | TAOM_Map hideout culture + name swap |
| [`TAOM.Tests/Features/BanditManagement/`](../../TAOM.Tests/Features/BanditManagement/) | 30 unit tests (service + config provider) |

## Dependencies

- `DefaultBanditDensityModel` — overridden via `campaignStarter.AddModel` in [SubModule.cs](../../Main/SubModule.cs).
- `DefaultPartySizeLimitModel.FindAppropriateInitialRosterForMobileParty` — Harmony Postfix. Coexists peacefully with [TaomPartySizeModel](../../Main/Features/CulturalFeats/Models/TaomPartySizeModel.cs) (which overrides `GetPartyMemberSizeLimit` only).
- `TaomSettings` (MCM) — 6 new properties in the `World/Bandit Scaling` group.
- Existing TAOM culture troop XMLs — pulls raider-tier T1–T4 troops by ID; no new troop authoring.
- `taom_partyTemplates.xml` — 10 new templates (5 raider + 5 boss).

## Tests

[`TAOM.Tests/Features/BanditManagement/BanditScalingServiceTests.cs`](../../TAOM.Tests/Features/BanditManagement/BanditScalingServiceTests.cs) — 16 tests covering:
- Linear curve at 0, 0.5, 1.0
- NaN/Infinity guards on PlayerProgress AND on curve
- Negative-input clamping
- Monotonicity in PlayerProgress
- Vanilla floor (multiplier ≥ 1.0)
- Per-curve isolation (DensityCurve doesn't bleed into PartySizeCurve)
- IsEnabled + cap delegation

[`TAOM.Tests/Features/BanditManagement/BanditScalingConfigProviderTests.cs`](../../TAOM.Tests/Features/BanditManagement/BanditScalingConfigProviderTests.cs) — 14 tests covering:
- Valid JSON parsing
- Missing file → defaults + warning log
- Malformed JSON → defaults + error log
- NaN/Infinity → revert + warning (per `feedback_clamp_nan_infinity_propagates.md`)
- Out-of-range → revert + warning
- `MinPartiesToInfest > MaxPartiesPerHideoutCap` → revert ordering invariant
- Lazy caching (same instance across calls)

**30/30 tests pass** in baseline build.

## How-To

### How to tune scaling without recompiling

1. Open MCM in-game → TAOM → World / Bandit Scaling.
2. Adjust `Density Curve`, `Party Size Curve`, `Boss Fight Curve` (range 0.0–5.0).
3. Settings apply on next bandit spawn / hideout query — no game restart needed.

### How to add a new hideout

1. Edit `E:\Steam\...\Modules\TAOM_Map\ModuleData\settlements.xml`.
2. Copy an existing `<Settlement type="Hideout">` block.
3. Change `id` (must be unique), `posX`/`posY`, `culture=` (use one of the 5 LOTR bandit cultures).
4. Rename `name=` text to a unique camp name.
5. Add a `<string>` entry in `taom_module_strings.xml` for the new name key.
6. Rebuild the settlement distance cache via MCM → Map Tools → Rebuild Settlement Distance Cache.

### How to add a new bandit culture

1. Add `<Culture>` entry in [`taom_spcultures.xml`](../../Main/_Module/ModuleData/taom_spcultures.xml). Required attributes: `id`, `name`, `bandit_chief/raider/bandit/boss`, `elite_basic_troop`, `basic_troop`, `is_bandit="true"`, `can_have_settlement="true"`, `encounter_background_mesh`, `bandit_boss_party_template`.
2. Add two party templates in [`taom_partyTemplates.xml`](../../Main/_Module/ModuleData/taom_partyTemplates.xml): `{culture}_raider_party_template` and `{culture}_boss_party_template`. Each references troop NPCCharacter IDs.
3. Add localization keys for culture name + male/female names in [`taom_module_strings.xml`](../../Main/_Module/ModuleData/taom_module_strings.xml).
4. Author hideouts referencing the new culture in `TAOM_Map/settlements.xml`.

### How to disable scaling entirely

Either:
- MCM → World / Bandit Scaling → uncheck **Enable Bandit Scaling** (single boolean), OR
- Set every curve to 0.0 (multipliers floor at 1.0).

Both leave the LOTR culture replacement intact — bandits keep their LOTR names, just spawn at vanilla density + sizes.

## Performance

The GameModel properties (`NumberOfMaximumHideoutsAtEachBanditFaction` etc.) are read by vanilla campaign behaviours at low frequency (hourly tick or less). The Patch39 Postfix runs once per party spawn (~10s of times per in-game day at most). No hot-path concerns.

`Campaign.Current.PlayerProgress` is a TaleWorlds-computed property — assumed cheap. No additional caching needed.

## Localization

Culture display names and male/female names live in [`taom_module_strings.xml`](../../Main/_Module/ModuleData/taom_module_strings.xml). English defaults are baked into the `name="{=KEY}default"` attribute pattern, so non-English players see English LOTR names until translations are produced. To localize, run `python tools/translate_with_claude.py` after authoring; the new keys are picked up automatically.

The 99 hideout name strings in `TAOM_Map/Languages/<LANG>/loc_settlements.xml` were set to the English LOTR camp names by [`tools/migrate_hideouts_to_lotr.py`](../../tools/migrate_hideouts_to_lotr.py). Future hand-translation per language is straightforward (each language file has 99 entries with consistent text patterns).

## Save Compatibility

| Surface | Save-compat |
|---|---|
| New MCM settings | Safe — read with `?? default` fallback at every access |
| `bandit_scaling_config.json` | Safe — missing file falls through to compiled defaults |
| 5 new LOTR bandit cultures | New cultures only added; no existing culture IDs renamed |
| 10 new party templates | New IDs only added; no existing template renamed |
| Hideout XML migration | Hideout IDs preserved; only `culture=` and display name changed. Saves load and re-bind hideouts to the new (renamed) cultures on next game tick. |
| 80+ new loc keys | Pure additions, can't break existing references |

A save from before this feature loads cleanly; the player sees renamed hideouts + LOTR bandit cultures appear at next bandit spawn. A save FROM this feature loads on a version without it provided the bandit clans get re-mapped to vanilla culture IDs — which they do automatically since `is_bandit="true"` cultures are reaped from the loaded spcultures set at campaign init.
