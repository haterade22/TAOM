# Arachnophobia Mode

## Overview

One MCM switch, **Arachnophobia Mode** (group Accessibility, off by default), takes the giant spiders out of the game. Troops that ride a spider ride a warg instead, nothing creates, sells or lists a new spider troop, and spider broods leave the campaign map.

## Why This Exists

- **Vanilla behavior:** none. Spiders are TAOM content.
- **TAOM requirement:** players with arachnophobia asked to play without the spiders, the troops and party templates included (#1110).
- **Without this feature:** `Spawn Spider Broods` stops new broods only. Broods on the map stay, and the Dol Guldur and goblin Spider Rider lines still render.

## Architecture

### Design Challenge

Troop ids cannot be deleted, because saves refer to troops by id (`.claude/rules/troops.md`, gate `TROOP_ID_REMOVED`). The mode makes the troops unreachable instead.

Everything the mode writes must be undone by turning it off. Troop equipment, upgrade targets and the two list flags reload from XML with each game and go into no save, so the mode writes only those. It does not convert a troop already in a party or garrison, because that write goes into the save.

### Solution Approach

`SubModule.OnGameInitializationFinished` calls `IArachnophobiaService.ApplyForGame` for every game type, after the Monster size pass. With the mode on, the service:

1. Checks that every warg item and the harness exist. If one is missing, it logs an error and changes no troop. The daily brood disband reads the setting itself, so it still runs.
2. Reads every non-hero troop with a battle set on `Monster.spider`, and keeps those ids as the hidden set. A future spider troop joins the set with no code.
3. Swaps those troops to wargs, in every battle set. A spawned agent draws each slot from its own random set (`Equipment.cs:566-593`), so one missed set still shows a spider.
4. Closes the routes in the table below. The swap comes first: if a later step fails, the wargs already written stay, and the routes that read the hidden set stay closed. A failure inside the swap can leave some sets on spiders; the `[Check]` line then reports a fail.

| Route | With the mode on | Seam |
|---|---|---|
| Party templates | Only `mirkwood_spiders_brood_template` names a spider. The daily spawner stops reading it, so it stays intact. The dev console command `taom.spawn_creature_band broods confirm` still spawns a brood from it | none |
| Spider broods | No new broods. Existing broods disband on the daily tick, authority only. One in a battle waits for a later day. One holding a hero prisoner waits until the hero leaves: an NPC hero has a daily escape chance (`PrisonerReleaseCampaignBehavior.DailyHeroTick`), and the player leaves through player captivity. Regular prisoners go with the brood, as when vanilla destroys a party (`MobileParty.RemoveParty`) | `CreatureBroodSpawnBehavior`, `CreatureBandParties.DisbandAll` |
| A brood battle before the first tick | The Horse slot now holds a warg, so the troop takes the vanilla spawn as a husk on a warg. The brood tree plays spider clips only | `Patch93_CreatureBanditSpawn`, `CreatureBanditRules.IsBroodMountMonster` |
| Volunteers | Hidden troops leave the pool before the weighted pick, on a copy, because the pools are static | `VolunteerRecruitmentService.GetVolunteerTroopId` |
| Upgrades | Every edge into the hidden set is dropped, from a hidden troop too, so a leftover cannot upgrade into another spider troop. A hidden troop keeps any edge to a visible troop | `ArachnophobiaService`, private setter `CharacterObject.UpgradeTargets` |
| Garrison conversion | The index walks the volunteer pool roots through the upgrade edges (`GarrisonCultureSwapAdapter.BuildCandidates`), so the pool filter and the dropped edge keep hidden troops out | `VolunteerRecruitmentService.GetCulturePoolTroopIds` |
| Elite Emissary | Hidden troops are not offered and cannot be bought | `EliteEmissaryService.GetCultureOffers` |
| Encyclopedia | `HiddenInEncyclopedia`, campaign only | `TroopMountCatalogAdapter` |
| Custom Battle list | `IsObsolete`, Custom Battle only: the Custom Battle troop list (`ArmyCompositionGroupVM`, `IsSoldier && !IsObsolete`) and TAOM's default pick (`CustomBattleService`) read it for a soldier. The campaign reads it only for a hero's own character (`Hero.cs:1610`) | `TroopMountCatalogAdapter`, private setter |

The adapter finds troops as `BasicCharacterObject`. A campaign registers `CharacterObject` and a Custom Battle registers `BasicCharacterObject`, and `MBObjectManager` matches a non-sealed type with `IsAssignableFrom` (`MBObjectManager.cs:590`, `:755`), so one path serves both.

### Component Diagram

```
TaomSettings.ArachnophobiaMode
        |
  SubModule (once per game)        CreatureBroodSpawnBehavior (daily, authority only)
        |                                   |
  ArachnophobiaService                CreatureBandParties.DisbandAll
    /        \
ITroopMountCatalogAdapter   IsHidden(troopId)
                              |            |
              VolunteerRecruitmentService  EliteEmissaryService
```

## Configuration

| Setting | Default | Applies |
|---|---|---|
| `ArachnophobiaMode` | off | Broods: the next daily tick, within a day (after a load, the engine schedules it from the elapsed part of the day). All else: the next game start or load |

The two halves take effect at different times. Turned on mid-session, broods leave at the next tick and the rest waits for the next load. Turned off mid-session, broods spawn again at one a day, but the troops keep their wargs until the next load, so a new brood fights as goblins on wargs until then.

### Current Values

| Spider item | Warg item (with `warg_saddle` in `HorseHarness`) |
|---|---|
| `spider_mount_a`, `spider_mount_mountain_a1`, any spider item not in the table | `warg_brown` |
| `spider_mount_brown`, `spider_mount_mountain_a2` | `warg_dark` |
| `spider_mount_pale` | `fell_warg` |

## What Stays

- **Spider troops already in a save** stay until they die, as warg riders still named "Spider Rider". The same applies to a spider already in a notable's volunteer slot, and to a captured one, which a party can still recruit from its prisoners. One exception: a garrison culture conversion now replaces a leftover whose culture differs from the target, because hidden troops are not in the target culture's index. `GarrisonCultureSwapAdapter.cs:173-177` is the idiom if a conversion is wanted later.
- **Heroes and items:** the mode never writes a hero, because a hero's battle set is its saved gear. A hero already on a spider keeps it, and so does a spider item already in an inventory. No shipped hero rides one. A leftover Spider Rider commissioned as a companion while the mode is on copies the warg into the new hero's saved gear, and that warg stays after the mode is turned off.
- **Fords of War** keeps its `spider_riders` offer in two factions, riding wargs, and the AI buys it too. `FordsConfigCatalog` caches factions for the process, so removal needs a per-match filter (a follow-up).
- **Text:** "Spiders of Mirkwood" lore, hero biographies and career names.
- **Cobwebs:** `spider_web_a` and `spider_web_b` in four `TAOM_Map` scenes.
- **Balance:** Dol Guldur and the goblin cultures lose a rare cavalry line. A leftover warg is weaker than a spider.
- **Co-op:** all players need the same value. The setting is in the settings fingerprint, which each peer logs for the players to compare by hand. UNVERIFIED: what a mismatch does in play.

## Key Files

| File | Purpose |
|---|---|
| `Main/Features/Arachnophobia/ArachnophobiaService.cs` | Hidden set, swap, route order, in-game check |
| `Main/Features/Arachnophobia/ArachnophobiaRules.cs` | The replacement table |
| `Main/Features/Arachnophobia/ArachnophobiaIoC.cs` | Registration |
| `Main/Adapters/TroopMountCatalogAdapter.cs` | Reads troops, writes mounts, upgrade targets and list flags |
| `Main/Features/CreatureBandits/CreatureBandParties.cs` | `DisbandAll` |

## Dependencies

- `ITroopMountCatalogAdapter` (Adapters): the troop and item catalogue.
- `IOutcomeLog` (Core): the `[Check]` lines.
- `SpiderConfig.SpiderMonsterId` (Spider): the Monster that defines a spider rider.

## Tests

- `TAOM.Tests/Features/Arachnophobia/`: the table, the service paths, the wiring, and a `LiveInstall` gate that the Armory has every required item.
- `TAOM.Tests/Features/TroopProgression/VolunteerRecruitmentHiddenTroopTests.cs`: the pool filter, and a static pool left intact.
- `EliteEmissaryServiceTests`, `CreatureBanditRulesTests`: the Emissary filter and the brood rules.
- `ReflectionSiteBindingTests`: both private setters.

## How to verify in game

**Preconditions:** the mode on in MCM, then a new game start or a load. Run `/armory-audit` first if the ARMORY ART DRIFT banner shows. The manifest is `tools/ingame_checks/1110.toml`.

The log writes each `[Check]` line once per process for each result and game type, so use a new process for each step that needs a line.

1. Start a Custom Battle. The log shows `[Check] id=taom.arachnophobia.apply result=pass hidden=N sets=N edges=0`, with `sets` at 1 or more. A person checks that no Spider Rider is in the troop list. Then spawn `goblin_spider_rider`, `goblin_spider_lord` and `taom_spider_rider_pale` (`taom.spawn_troops <id> 3 ally`): a person checks that each goblin sits on the saddle of a brown warg, a dark warg and a fell warg. No goblin rode these wargs before.
2. Start a new campaign. The log shows the same check with `edges` at 1 or more. A person checks a Dol Guldur fief for spider recruits, the `goblin_lurker` upgrade list, the encyclopedia and the Elite Emissary.
3. Load a save with broods and pass one day. The log shows `[Check] id=taom.arachnophobia.broods-disbanded result=pass count=N`. A person checks that a leftover Spider Rider rides a warg and fights.
4. Load a save with broods and meet one before the first daily tick. A person checks that it fights as unarmed goblins on wargs, the wargs bite, and the battle ends. Nothing writes a line for this step.
5. Turn the mode off and load again. A person checks that spiders return. Nothing writes a line for this step.

## How to add a spider

1. Put the new mount item on `Monster.spider`. The troop that rides it joins the hidden set with no code.
2. To choose its warg, add a row to `ArachnophobiaRules`. Without a row, it gets the brown warg.

## Changelog

- 2026-11-09: first version (#1110).

## GitHub Issue

- **Issue:** #1110: [Arachnophobia Mode: a setting that hides spiders and takes spider troops out of play](https://github.com/haterade22/TAOM/issues/1110)
- **Status:** Closed at merge; the in-game checks above are owed (label `triage-needs-ingame`), and the two MCM strings wait for the translator (#763)
