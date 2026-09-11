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

> **The ceiling this scales toward moved on 2026-09-06 (#543).** The raider and boss templates carried a flat `max_value="50"` from #315, so a four-stack raider template topped out at exactly 200 troops and `Patch39` drove most endgame parties there. They are now retuned to a troop-POWER budget (~78 power for raiders, ~105 for bosses), which lands at 56-80 bodies for a raider warband depending on the culture's tier mix. Two consequences for this feature: **`Party Size Curve`'s useful range is now much shorter** (the roster clamps at `stack.MaxValue` around a quarter of the way through a campaign, after which raising the slider does nothing), and early-game parties are smaller and more varied (12-32 bodies, against 31-75 before). Retune with `tools/rebalance_template_power.py`, never by hand; reasoning in [caravan-bandit-parity.md](caravan-bandit-parity.md).

### What gets scaled

| Surface | Vanilla | TAOM scaling target |
|---|---|---|
| Hideouts per bandit faction (max) | 9 max | Up to `BanditMaxHideoutsPerFaction` (default 100 → physical hideout count binds first) |
| **Initial hideouts per faction (new game)** | 7 | `BanditInitialHideoutsPerFaction` (default 7 since #559; 14 before) |
| Bandit parties per hideout | 3 max | Up to `BanditMaxPartiesPerHideout` (default 6 since #559; the old default of 3 equalled vanilla and pinned it there) |
| Min parties to infest a hideout | 2 | `MinPartiesToInfest` (constant 1: hideouts go active/visible sooner) |
| Troops in hideout first fight | `11 × (2 + PlayerProgress)` | × `BossFightCurve` |
| Troops in hideout boss fight | `1 + 5 × (1 + PlayerProgress)` | × `BossFightCurve` |
| Bandit party troops on map | `min + (max-min) × (0.4 + 0.8 × PlayerProgress)` × random(0.2..0.8) | × `PartySizeCurve`, capped at stack `MaxValue` |

### Early-game density ("early burst then settle", 2026-05-29)

`NumberOfInitialHideoutsAtEachBanditFaction` (vanilla 7) is the actual early-game lever: `BanditSpawnCampaignBehavior.SpawnHideoutsAndBanditsPartiallyOnNewGame` fills this many hideouts per faction at new-game init, and each infested hideout drives the hourly roaming-bandit spawn (`SpawnBanditsAroundHideout` scales with infested-hideout count). Raising the *max* cap alone does **not** add early bandits: at `PlayerProgress = 0` the multiplier is `1.0`, so the max stays at vanilla 9. The override shipped this at 14 from 2026-05-29 to 2026-09-11. With 8 bandit factions that is 112 hideouts on a fresh map against vanilla's 42, and players reported a map swamped with bandits (#559). It is 7 now, vanilla per faction, 56 in total. The dynamics are unchanged: `AddNewHideouts` only grows a faction while its infested count is below the max, so initial > max means no growth until attrition drops the count under the max, then refills, and the max itself grows with `PlayerProgress` toward the 100 cap. Combined with `MinPartiesToInfest = 1`, hideouts become active/visible with a single party.

**This value is read once, at world-gen, and nothing ever removes a hideout above the max** (`BanditSpawnCampaignBehavior:283` only adds while below it). Lowering the slider, or switching scaling off, changes nothing about a campaign already in progress; the hideouts it started with stay until the player clears them. That is what the #559 reporter saw: the setting had applied, and could not undo world-gen.

## Configuration

### MCM — Players Tune These

Settings live under **TAOM → World / Bandit Scaling** (`GroupOrder = 35`). MCM is the only surface; there is no config file.

| Setting | Range | Default | Effect |
|---|---|---|---|
| Enable Bandit Scaling | bool | true | Master toggle. Off = vanilla density + party sizes for NEW spawns; hideouts already on the map stay. |
| Density Curve | 0.0 – 5.0 | 1.5 | Multiplier on hideout count + parties/hideout at PlayerProgress=1.0 |
| Party Size Curve | 0.0 – 5.0 | 1.5 | Multiplier on bandit party troop counts at PlayerProgress=1.0 |
| Boss Fight Curve | 0.0 – 5.0 | 1.5 | Multiplier on hideout first-fight + boss-fight troop counts |
| Max Hideouts Per Faction Cap | 1 – 100 | 100 | Hard ceiling regardless of curve (physical hideout count binds first) |
| Max Parties Per Hideout Cap | 1-20 | 6 | Hard ceiling regardless of curve. Vanilla 3 is also the floor, so a cap of 3 or less pins the value at vanilla and the curve cannot move it (that was the default until #559). |
| Initial Hideouts Per Faction | 1-30 | 7 | Hideouts each faction starts with on a NEW campaign (vanilla 7, TAOM shipped 14 until #559). Read once at world-gen. |

**Every setting in the group is read live** through `BanditScalingSettingsProvider` on each model
property get and each `Patch39` spawn, and every attribute carries `RequireRestart = false`. That
flag is load-bearing, not cosmetic: MCM's `BaseSettingPropertyAttribute` defaults it to TRUE, and
with it true, pressing Done raises "Game Needs to Restart" whose Cancel branch is an empty delegate
followed by `return` (decompiled `ModOptionsVM.ExecuteDone`), so the change is never written to
`TAOM.json`. It still applies for the rest of the session, because MCM's undo stack writes through
to the live instance as the slider moves, which is what made it look like it had taken. The bandit
group shipped without the flag from 2026-05-27 to 2026-09-11.
`SettingRequireRestartPostureTests` now asserts the flag on every TAOM setting.

> **Upgrade caveat (MCM persists per-property).** MCM stores every setting in `Configs/ModSettings/Global/TAOM/TAOM.json` and, on load, overrides the C# default for any property already present. A player who launched a build *before* the 2026-05-29 default change keeps their persisted `Max Hideouts Per Faction Cap = 15` / `Max Parties Per Hideout Cap = 5`; only the brand-new `Initial Hideouts Per Faction` picks up its default (14). To get the new "early burst then settle" tuning on an upgraded install, reset the **World / Bandit Scaling** group to defaults in MCM (or edit `TAOM.json`). Fresh installs get the new defaults automatically. This is inherent MCM behaviour, not a bug — there is no per-property migration hook.

`MinPartiesToInfest` (1, vanilla 2) has no MCM knob. It is a constant in `BanditScalingSettingsProvider`, bounded at runtime by `[1, live MaxPartiesPerHideoutCap]`.

### The JSON that used to be here (removed 2026-09-11, #559)

`bandit_management/bandit_scaling_config.json` shipped from 2026-05-27 with the same values as the
MCM defaults. It was the `??` fallback in `SettingClamp.Clamp(TaomSettings.Instance?.Knob, json, min, max)`,
reachable only when `TaomSettings.Instance` is null, and `GlobalSettings<T>.Instance` is populated
whenever MCM is loaded whether or not the player ever opens the UI. MCM is a hard dependency. So on
every real install the file was never read, nothing pinned it to the C# defaults, and a player who
found it at 1.5 across the board concluded (reasonably) that it was what the game obeyed. The
constants now live in the provider and `BanditScalingSettingsProviderTests` pins them to
`new TaomSettings()`. Twelve other features still ship the same shape of shadowed JSON; that is a
separate issue.

## LOTR Bandit Culture Replacement

| Vanilla culture | New culture (StringId) | Display name | Troop source |
|---|---|---|---|
| `forest_bandits` | `dunland_raiders` | Dunlending Raiders | [`troops/troops_dunland.xml`](../../Main/_Module/ModuleData/troops/troops_dunland.xml) |
| `mountain_bandits` | `gundabad_raiders` | Gundabad Orc Raiders | [`troops/troops_gundabad.xml`](../../Main/_Module/ModuleData/troops/troops_gundabad.xml) |
| `desert_bandits` | `harad_raiders` | Haradrim Raiders | [`troops/troops_harad.xml`](../../Main/_Module/ModuleData/troops/troops_harad.xml) |
| `steppe_bandits` | `rhun_raiders` | Rhûn Raiders | [`troops/troops_rhun_new.xml`](../../Main/_Module/ModuleData/troops/troops_rhun_new.xml) |
| `sea_raiders` | `umbar_corsairs` | Corsairs of Umbar | [`troops/troops_umbar.xml`](../../Main/_Module/ModuleData/troops/troops_umbar.xml) |

Each LOTR bandit culture has `is_bandit="true"` and `can_have_settlement="true"` (allows hideout ownership). Troops are pulled from each culture's existing TAOM troop XML — no new troop definitions needed.

**Important:** `is_bandit="true"` on a culture does NOT auto-create a bandit clan — a matching `<Faction is_bandit="true">` clan row must be authored separately (in `characters/clans.xml`), and the homeless vanilla bandit clans must be stripped via `spclans.xslt` (see "Bandit clan contract" below). A bandit culture (`is_bandit` + `can_have_settlement`) is **inert** until both a clan references it AND hideouts of its culture exist; with no clan it's never iterated in `Clan.BanditFactions`, and the engine's `_hideouts[clan.Culture]` hard indexer (`BanditSpawnCampaignBehavior.GetInfestedHideoutCount`) only runs for clans whose `Culture.CanHaveSettlement` is true — so a clan with zero hideouts of its culture KNFEs on new-game.

### Wave 2 — heroic-faction offshoots (2026-05-28, live)

Three further bandit cultures themed as renegade offshoots of heroic factions. Fully wired: culture + raider/boss party templates + bandit clan + 10 hideouts each.

| New culture (StringId) | Display name | Parent kingdom (banner) | Troop source | Hideouts |
|---|---|---|---|---|
| `gondor_soldiers` | Gondor Soldiers | `empire_w` (Gondor) | `troops/troops_gondor.xml` (Anórien line) | `hideout_gondor_1`–`10` |
| `erebor_warriors` | Erebor Warriors | `erebor` | `troops/troops_erebor.xml` (regular line) | `hideout_erebor_1`–`10` |
| `mirkwood_stalkers` | Mirkwood Stalkers | `mirkwood` | `troops/troops_mirkwood.xml` (Silvan militia) | `hideout_mirkwood_1`–`10` |

Hideout `scene_name` = the matching scene id created in the map editor (`hideout_gondor_1` etc.). The clans were authored only after the 10 hideouts of each culture existed (so `_hideouts[culture]` is populated before `GetInfestedHideoutCount` runs — the inert-until-hideouts-exist ordering avoids the new-game KNFE).

Two existing ranges were also expanded via `tools/add_bandit_hideouts.py`: `hideout_desert_30`–`43` → `harad_raiders`, `hideout_seaside_30`–`45` → `umbar_corsairs` (existing cultures+clans, functional immediately).

### Raider-tier troop roster per culture

T1–T4 troops only (vanilla bandits never field elite kingdom troops):

| Bandit Culture | bandit_bandit (T1) | bandit_raider (T2-3) | bandit_chief (T3) | bandit_boss (dedicated) |
|---|---|---|---|---|
| `dunland_raiders` | `dunland_peasant` (L6) | `dunland_raider`, `dunland_hunter` (L11) | `dunland_clan_warrior` (L16) | `dunland_raiders_boss` (L21) |
| `rhun_raiders` | `balcoth_volunteer` (L11) | `balcoth_footman`, `kharaghul_rider` (L16) | `balcoth_archer` (L21) | `rhun_raiders_boss` (L21) |
| `harad_raiders` | `harad_levy` (L6) | `harad_archer`, `harad_camelscout` (L16) | `harad_footman` (L21) | `harad_raiders_boss` (L21) |
| `gundabad_raiders` | `gundabad_snaga`, `gundabad_hunter` (L11) | `gundabad_grunt`, `gundabad_lurker` (L16) | `gundabad_scout` (L21) | `gundabad_raiders_boss` (L26) |
| `umbar_corsairs` | `aux_basic` (L6) | `umbar_elite`, `umbar_elite_root1` (L11-16) | `umbar_elite_root0` (L16) | `umbar_corsairs_boss` (L21) |

The three Wave-2 offshoot cultures use the same pattern: `gondor_soldiers_boss`, `erebor_warriors_boss`, `mirkwood_stalkers_boss`.

### Dedicated hideout-boss troops (`occupation="Bandit"` is load-bearing)

Each bandit culture's `bandit_boss` points at a **dedicated** `{culture}_boss` NPCCharacter (defined in that culture's troop XML), mirroring vanilla (`sea_raiders_boss` etc.) — **not** a shared regular-roster troop. Two attributes MUST match the vanilla bandit-boss template:

- **`occupation="Bandit"`** — the hideout boss fight opens with a forced boss conversation. `HideoutMissionController.OnInitialFadeOutOver` first sets the player/bandit teams **non-enemy** (`SetIsEnemyOf(_enemyTeam, false)`) for the walk-up, and enmity is only restored by the `StartBossFightDuelMode`/`StartBossFightBattleMode` consequence of the vanilla `bandit_hideout_start_defender` dialog. But `GuardsCampaignBehavior.conversation_guard_start_on_condition` matches *any* conversation NPC with `Occupation == Soldier` (enum value **7**) inside a settlement and shows *"Can't talk right now. Got to keep my eye on things around here."* If the boss is `occupation="Soldier"`, that **guard dialog hijacks the boss conversation**, the taunt never fires, enmity is never restored, and **all bandits stay friendly → the player is forced to retreat.**
- **`culture="Culture.{bandit_culture}"`** — `HideoutMissionController.SelectBossAgent` preferentially picks the boss only when `character.Culture.IsBandit && Culture.BanditBoss == character`; a non-bandit culture silently falls back to "highest-level agent on the enemy team."

These are dedicated troops (not edited in place) because the original `bandit_boss` referenced **shared** regular-roster troops — e.g. `dunland_wolf_raider` is mid-upgrade-chain, `harad_camelrider` is in regular Harad party templates — so changing their `occupation`/`culture` would corrupt the normal rosters. (Fix: CHANGELOG 2026-05-31.)

### Hideout migration (one-time)

The 99 existing hideouts in `TAOM_Map/ModuleData/settlements.xml` (external module) had their `culture=` attribute swapped to the new LOTR cultures and their display names rewritten from "Hideout" to "Dunlending Raider's Camp" / "Gundabad Orc Raider's Camp" / "Haradrim Raider's Camp" / "Rhûn Raider's Camp" / "Corsair's Cove". Settlement IDs (`hideout_forest_N`, etc.) were intentionally **left unchanged** to preserve save compatibility — a save started before this migration will load fine and just see the renamed hideouts.

Migration is driven by [`tools/migrate_hideouts_to_lotr.py`](../../tools/oneoff/migrate_hideouts_to_lotr.py). Safe to re-run idempotently. `--backup` flag writes `.bak` copies of each modified file before overwriting.

### Hideout scenes (scene_name) — must exist on disk

A hideout's `<Location id="hideout_center" scene_name="X">` must resolve to a `Modules/*/SceneObj/X/` folder or **raiding it crashes**. The 99 vanilla-derived hideouts use stock scenes that exist (`bandit_forest_sv`, `desert_hideout_002/004_sv`, `hideout_steppe_001/002_sv`, `mountain_hideout_002/004_sv`, `sea_bandit_a-d_sv`). The 30 wave-2 hideouts (`hideout_gondor/erebor/mirkwood_*`) reference editor scenes not yet exported to `SceneObj/`, so they are **interim-repointed to vanilla hideout scenes** (gondor/mirkwood → `forest_hideout_004_sv`, erebor → `mountain_hideout_002_sv`) to prevent raid crashes; revert each `scene_name` to its settlement id once the custom scenes are compiled. Verify scene refs with [`tools/audit_scene_names.py`](../../tools/audit_scene_names.py) — see [`docs/reference/scene-reference-audit.md`](../reference/scene-reference-audit.md). Vanilla renames scenes between versions, so re-run the audit after any Bannerlord bump.

## Hideout Encounter Descriptions (Patch40, 2026-05-29)

When the player visits a hideout, the encounter menu prose led with the literal placeholder **"(Undefined hideout type)"** for every TAOM bandit hideout. Root cause (decompile-verified, v1.4.5): vanilla `HideoutCampaignBehavior.game_menu_hideout_place_on_init` sets the `HIDEOUT_DESCRIPTION` GameText variable to `{=DOmb81Mu}(Undefined hideout type)` and then overrides it **only** for the five hardcoded vanilla bandit culture StringIds. TAOM renamed those cultures, so none match and the placeholder leaks through. The hideout *name* renders correctly because it comes from the settlement `name=` attribute (a different code path); only the description prose is keyed on culture StringId in C#.

**Fix:** `Patch40_HideoutDescription` — a Postfix on the private `game_menu_hideout_place_on_init`. It runs after vanilla's body (still inside `on_init`, before the menu renders its `{=!}{HIDEOUT_TEXT}` text, whose value embeds `{HIDEOUT_DESCRIPTION}`). GameText variables resolve lazily at render — `MBTextManager.SetTextVariable` stores `new TextObject(value)` and nested `{HIDEOUT_DESCRIPTION}` is substituted from the global context when the menu draws — so re-setting `HIDEOUT_DESCRIPTION` in the Postfix propagates to the displayed prose. Only the `hideout_place` menu shows `{HIDEOUT_DESCRIPTION}`; `hideout_after_wait` uses its own culture-agnostic text, so a single patch is complete.

`IHideoutDescriptionService.GetDescription(cultureStringId)` returns the `{=key}default` template for the five TAOM bandit cultures and `null` for any other culture (vanilla / other-mod hideouts keep their own engine description untouched). The service takes a `string` and returns a `string` — no TaleWorlds types cross the boundary (ADR-007).

| Bandit culture | Description key | Biome flavor |
|---|---|---|
| `dunland_raiders` | `taom_hideout_desc_dunland` | Wooded hills of Dunland |
| `gundabad_raiders` | `taom_hideout_desc_gundabad` | Crags of the Misty Mountains |
| `harad_raiders` | `taom_hideout_desc_harad` | Dunes of Harad |
| `rhun_raiders` | `taom_hideout_desc_rhun` | Grasslands of Rhûn |
| `umbar_corsairs` | `taom_hideout_desc_umbar` | Sheltered southern cove |

The five strings live in [`taom_module_strings.xml`](../../Main/_Module/ModuleData/taom_module_strings.xml). The C# default text in `HideoutDescriptionService` matches the XML default verbatim (so the compiled fallback equals the loaded string).

## Key Files

| File | Purpose |
|---|---|
| [`Main/Features/BanditManagement/IHideoutDescriptionService.cs`](../../Main/Features/BanditManagement/IHideoutDescriptionService.cs) | Interface for themed hideout descriptions |
| [`Main/Features/BanditManagement/HideoutDescriptionService.cs`](../../Main/Features/BanditManagement/HideoutDescriptionService.cs) | Culture StringId → `{=key}default` template (null for non-TAOM cultures) |
| [`Main/Features/BanditManagement/Hooks/Patch40_HideoutDescription.cs`](../../Main/Features/BanditManagement/Hooks/Patch40_HideoutDescription.cs) | Postfix on private `game_menu_hideout_place_on_init`; re-sets `HIDEOUT_DESCRIPTION` |
| [`Main/Features/BanditManagement/IBanditScalingSettingsProvider.cs`](../../Main/Features/BanditManagement/IBanditScalingSettingsProvider.cs) | Interface for live MCM read |
| [`Main/Features/BanditManagement/BanditScalingSettingsProvider.cs`](../../Main/Features/BanditManagement/BanditScalingSettingsProvider.cs) | NaN-safe live MCM read; the compiled defaults live here as constants |
| [`Main/Features/BanditManagement/IBanditScalingService.cs`](../../Main/Features/BanditManagement/IBanditScalingService.cs) | Pure math service |
| [`Main/Features/BanditManagement/BanditScalingService.cs`](../../Main/Features/BanditManagement/BanditScalingService.cs) | `multiplier = 1 + curve * progress` |
| [`Main/Features/BanditManagement/Models/TaomBanditDensityModel.cs`](../../Main/Features/BanditManagement/Models/TaomBanditDensityModel.cs) | GameModel override (hideout count, parties/hideout, fight troops) |
| [`Main/Features/BanditManagement/Hooks/Patch39_BanditPartySize.cs`](../../Main/Features/BanditManagement/Hooks/Patch39_BanditPartySize.cs) | Postfix scaling bandit party rosters toward stack MaxValue |
| [`Main/Features/BanditManagement/BanditManagementIoC.cs`](../../Main/Features/BanditManagement/BanditManagementIoC.cs) | DryIoc registration |
| [`Main/_Module/ModuleData/taom_spcultures.xml`](../../Main/_Module/ModuleData/taom_spcultures.xml) | 5 LOTR bandit culture entries (appended) |
| [`Main/_Module/ModuleData/taom_partyTemplates.xml`](../../Main/_Module/ModuleData/taom_partyTemplates.xml) | 10 raider + boss party templates (appended) |
| [`Main/_Module/ModuleData/taom_module_strings.xml`](../../Main/_Module/ModuleData/taom_module_strings.xml) | Culture display names + male/female names (~80 keys) |
| [`tools/migrate_hideouts_to_lotr.py`](../../tools/oneoff/migrate_hideouts_to_lotr.py) | TAOM_Map hideout culture + name swap |
| [`TAOM.Tests/Features/BanditManagement/`](../../TAOM.Tests/Features/BanditManagement/) | unit tests (service, settings provider, density-model helpers, hideout descriptions) |

## Dependencies

- `DefaultBanditDensityModel` — overridden via `campaignStarter.AddModel` in [SubModule.cs](../../Main/SubModule.cs).
- `DefaultPartySizeLimitModel.FindAppropriateInitialRosterForMobileParty` — Harmony Postfix. Coexists peacefully with [TaomPartySizeModel](../../Main/Features/CulturalFeats/Models/TaomPartySizeModel.cs) (which overrides `GetPartyMemberSizeLimit` only).
- `TaomSettings` (MCM) — 7 properties in the `World/Bandit Scaling` group (added `BanditInitialHideoutsPerFaction`).
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

[`TAOM.Tests/Features/BanditManagement/BanditScalingSettingsProviderTests.cs`](../../TAOM.Tests/Features/BanditManagement/BanditScalingSettingsProviderTests.cs), 5 tests: with no MCM instance (the state outside the game) every property returns the compiled default, each default matches `new TaomSettings()`, the two #559 defaults are 7 and 6, and `MinPartiesToInfest` is 1 and never exceeds the cap.

[`TAOM.Tests/Features/BanditManagement/TaomBanditDensityModelTests.cs`](../../TAOM.Tests/Features/BanditManagement/TaomBanditDensityModelTests.cs), 9 tests on the `internal static` `Cap`/`Scale` helpers (the model's only computation), including the regression for the "vanilla is the floor" invariant (`Cap(base, mult, hardCap)` with `hardCap < base` returns `base`, never `hardCap`) and the #559 regression that the SHIPPED parties-per-hideout cap leaves the curve room to move (`Cap(3, 2.5f, shippedCap) > 3`).

[`TAOM.Tests/Features/Mcm/SettingRequireRestartPostureTests.cs`](../../TAOM.Tests/Features/Mcm/SettingRequireRestartPostureTests.cs): every value setting in the four settings classes carries `RequireRestart = false` unless allowlisted with a reason.

[`TAOM.Tests/Features/BanditManagement/HideoutDescriptionServiceTests.cs`](../../TAOM.Tests/Features/BanditManagement/HideoutDescriptionServiceTests.cs) — 9 tests: each of the 5 cultures returns its expected `{=key}`, and unknown / vanilla-bandit / empty / null culture IDs return `null`.

BanditManagement + Mcm filtered run: 105/105 (2026-09-11).

## How-To

### How to tune scaling without recompiling

1. Open MCM in-game → TAOM → World / Bandit Scaling.
2. Adjust `Density Curve`, `Party Size Curve`, `Boss Fight Curve` (range 0.0–5.0).
3. Press Done. No restart prompt appears (every attribute carries `RequireRestart = false`) and the value is on disk in `TAOM.json` at once. Curves and caps apply on the next bandit spawn / hideout query. `Initial Hideouts Per Faction` is the exception: read once at world-gen, so it changes nothing about a campaign already running.

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

Culture display names, male/female names, and the 5 hideout encounter descriptions (`taom_hideout_desc_*`) live in [`taom_module_strings.xml`](../../Main/_Module/ModuleData/taom_module_strings.xml). English defaults are baked into the `text="{=KEY}default"` attribute pattern, so non-English players see English text until translations are produced. To localize, run `python tools/translate_with_claude.py` after authoring; the new keys are picked up automatically.

The 99 hideout name strings in `TAOM_Map/Languages/<LANG>/loc_settlements.xml` were set to the English LOTR camp names by [`tools/migrate_hideouts_to_lotr.py`](../../tools/oneoff/migrate_hideouts_to_lotr.py). Future hand-translation per language is straightforward (each language file has 99 entries with consistent text patterns).

## Save Compatibility

| Surface | Save-compat |
|---|---|
| New MCM settings | Safe — read with `?? default` fallback at every access |
| 5 new LOTR bandit cultures | New cultures only added; no existing culture IDs renamed |
| 10 new party templates | New IDs only added; no existing template renamed |
| Hideout XML migration | Hideout IDs preserved; only `culture=` and display name changed. Saves load and re-bind hideouts to the new (renamed) cultures on next game tick. |
| 80+ new loc keys | Pure additions, can't break existing references |

A save from before this feature loads cleanly; the player sees renamed hideouts + LOTR bandit cultures appear at next bandit spawn. A save FROM this feature loads on a version without it provided the bandit clans get re-mapped to vanilla culture IDs — which they do automatically since `is_bandit="true"` cultures are reaped from the loaded spcultures set at campaign init.

## Changelog

- 2026-09-11, `fix` (#559): `RequireRestart = false` on all 7 settings (MCM was prompting for a restart and discarding the change on Cancel); `bandit_scaling_config.json` and its config provider deleted (never read while MCM is loaded); defaults initial hideouts 14→7 and parties-per-hideout cap 3→6 (the cap equalled vanilla's floor, so Density Curve could not move it). Existing `TAOM.json` files keep 14 / 3 until the group is reset.
- 2026-05-31 — `fix`: hideout boss fight spawned every bandit friendly (forced retreat) — added 8 dedicated `{culture}_boss` troops with `occupation="Bandit"` + matching bandit culture so the guard dialog no longer hijacks the boss conversation.
- 2026-05-29 — `feat`: Patch40 themed LOTR hideout encounter descriptions replace vanilla's "(Undefined hideout type)" placeholder for the 5 TAOM bandit cultures.
- 2026-05-29 — `feat`: early-game density boost + cap tuning — initial hideouts 7→14, min-parties-to-infest 2→1, max caps set to 100 hideouts/3 parties; added the "Initial Hideouts Per Faction" MCM slider and the `Cap()` vanilla-floor fix.
- 2026-05-28 — `feat`: Wave-2 bandit cultures (`gondor_soldiers`, `erebor_warriors`, `mirkwood_stalkers`) + 10 hideouts each, plus expansion of the Haradrim and Umbar hideout ranges.
- 2026-05-28 — `fix`: strip the 5 homeless vanilla bandit clans + retarget each LOTR bandit clan/culture banner to its parent-kingdom heraldry.
- 2026-05-27 — `fix`: XSLT-remove the 5 vanilla hideout-bandit clans to stop the new-game `KeyNotFoundException` in `GetInfestedHideoutCount` (looters clan kept).
- 2026-05-27 — `feat`: initial feature — replace 5 vanilla bandit cultures with LOTR factions, `TaomBanditDensityModel` + `Patch39_BanditPartySize` PlayerProgress scaling, MCM/JSON config, 99-hideout migration (issue #247).

## Migrated notes (from CLAUDE.md, 2026-07-12)

- **`TaomBanditDensityModel` overrides 6 properties** (source-verified): `NumberOfMinimumBanditPartiesInAHideoutToInfestIt`, `NumberOfMaximumHideoutsAtEachBanditFaction`, `NumberOfInitialHideoutsAtEachBanditFaction` (the early-game density lever, vanilla 7 → default 14), `NumberOfMaximumBanditPartiesInEachHideout`, `NumberOfMaximumTroopCountForFirstFightInHideout`, `NumberOfMaximumTroopCountForBossFightInHideout`. (The Architecture diagram's "4 properties" count predates the 2026-05-29 initial-hideouts + min-to-infest additions.)
- The `Cap`/`Scale` helpers are `internal static` and unit-tested **directly via `InternalsVisibleTo("TAOM.Tests")`**; `Cap` floors at vanilla even when an MCM cap is set below the vanilla base.
- The 5 LOTR bandit cultures each have a **matching bandit clan row in `characters/clans.xml`** (5 rows, one `<Faction is_bandit="true">` per culture).
- The vanilla `looters` clan is **kept** because its `StringId == "looters"` is hardcoded in `DefaultBanditDensityModel`, and looter spawning runs on a separate code path from hideout bandits.
- `TAOM_Map/SubModule.xml` declares `<DependedModule Id="TAOM"/>` (the external map module now depends on TAOM, so the LOTR bandit cultures its hideouts reference are guaranteed loaded).

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/caravan-bandit-parity.md](./caravan-bandit-parity.md)
- [docs/modding/clans.md](../modding/clans.md)
- [docs/modding/cultures.md](../modding/cultures.md)
- [docs/modding/load-order-and-dependencies.md](../modding/load-order-and-dependencies.md)
- [docs/modding/settlements.md](../modding/settlements.md)
- [docs/modding/troubleshooting.md](../modding/troubleshooting.md)
- [docs/reference/feature-map.md](../reference/feature-map.md)
- [docs/reviews/rca-bandit-management-2026-05-27.md](../reviews/rca-bandit-management-2026-05-27.md)

<!-- backlinks-end -->
