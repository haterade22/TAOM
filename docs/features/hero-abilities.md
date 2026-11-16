# Hero Abilities

## Overview

The Long Defeat's mission heroes Aragorn (`taom_hero_aragorn`) and Gimli (`taom_hero_gimli`) cast timed abilities
(#1359, Phase 4 of #1325). A cast strengthens the hero and the soldiers of his own team around him, raises their
morale, and shakes the enemies near him. Their overhead strikes can also kill a soldier outright: that part is a
Signature Strikes row ([signature-strikes.md](signature-strikes.md), "Killing blows"). The abilities work only in the
Long Defeat missions. The gate also opens in the Helm's Deep story mission, but no hero with a kit stands there until
a later phase of #1325 loads the mission heroes in a campaign (`characters/taom_mission_heroes.xml` loads for Custom
Game only, `Main/_Module/SubModule.xml`). A hired campaign companion keeps his normal strength.

## Why This Exists

- **Vanilla behavior:** a hero is a soldier with more health; he has no ability of his own.
- **TAOM requirement:** heroes bought at the Long Defeat shop (#1337) must feel like the heroes of the books: Aragorn
  rallies the men at the wall, Gimli shrugs off blows and frightens the orcs.
- **Without this feature:** the shop sells three expensive soldiers.

## Architecture

### Design Challenge

- Every soldier's one behaviour-tree slot already holds the race-ability tree, so a hero cannot get a second tree
  (csharp-architecture.md item 3).
- The race abilities keep one state per soldier and buff only the caster; a hero has several abilities and buffs
  other soldiers. Two overlapping buffs with different end times need one entry per grant.
- The stat, damage and morale models read a soldier's effects from any thread.

### Solution Approach

- `HeroAbilitiesMissionLogic` runs a one-second pass from the mission tick, as `DwarfTreeHost` does. On each pass
  `HeroAbilityHost` gives each hero the first ability, in config order, that is off cooldown and whose triggers
  hold. The triggers are the race abilities' (`RaceAbilitySensor.Sense`, `RaceAbilityService.FiringTrigger`): an
  ability is a synthetic `RaceAbilityProfile`.
- A hero casts only when this peer owns him (`CoopMissionOwnership.IsOwnedHere`), the AI drives him, he is not the
  player's body and he is not running away (`HeroAbilityService.ShouldDecide`). His passive and his strikes still
  work when he is not cast-eligible.
- `HeroAbilityCaster` grants the hero `selfEffects` and `allyEffects` together, grants `allyEffects` to the human
  soldiers of his own team within `radius` that this peer owns (never himself), adds morale to the AI ones, and
  drains morale from the enemies within `enemyShockRadius` through `MoraleShock` (the battle morale model, the
  career fear scaling, the race resistance and `DreadAgentGate`). The drain is applied (the fired line counts it as
  `shocked=`) but cannot rout an attacker while the Long Defeat holds him: `LongDefeatMorale.HoldsAttacker` is read by
  both morale models, and the engine then sets his morale to a floor instead of panicking. What fear should do there
  is an open decision for the maintainer. Then the war cry (only while the race abilities' MCM war-cry switch is on,
  `RaceAbilitySettingsProvider.WarCries`, the switch `RaceAbilityActivator` reads), a burst of sparks, the hero's
  outline in his `glow` colour and one message line ("Enemy" and red when he is against the player).
- `HeroBuffLedger` holds one grant per source per soldier. Each change builds a new dictionary and swaps it, so the
  models read a whole snapshot through `RaceAbilityHooks.BuffEffects`. `RaceAbilityHooks.EffectsOf` merges the
  soldier's own race ability, his hero aura and his buff: the stronger value per channel, never the sum. Each grant
  and each end refreshes the soldier's stats on the main thread, and the stats of the horse he rides at that moment;
  a horse he left keeps its last stats until its own next refresh. No shipped kit grants mount speed.
- The mission gate: a combat mission (`SignatureMissionGate`) that holds `LongDefeatMissionLogic`, `enabled` in the
  JSON and at least one kit. One marker is enough: the story launcher adds `LongDefeatMissionLogic` beside
  `StoryMissionLogic` (`StoryMissionLauncher.cs:107`), so a story arm could never change the answer. It is decided on
  the first tick; the battle phase is read on every tick. A throw stands the feature down for the mission and ends
  every buff it gave.
- Gimli's fear is his strike `fearMorale` and his war-cry shock; both are subject to the Long Defeat hold described
  above. The Dread Aura gate stays closed in the Long Defeat.
- Contact, for the in-game check only: `HeroAbilityHost.InContact` counts a pass as contact when the kit's easiest
  enemy trigger holds by the host's own count (`HeroAbilityService.TryContactRule`: the smallest `EnemiesWithin`
  count among the kit's abilities, a tie taking the larger range). The query is flat, so each candidate is also
  filtered by its 3D distance (`enemy.Position.Distance`), as `RaceAbilitySensor` does. A hero with no such trigger
  has no contact.
- Aragorn's passive is his existing hero aura `aragorn_heir_of_isildur`, which now names `taom_hero_aragorn`
  ([hero-auras.md](hero-auras.md)).

### Component Diagram

```
hero_abilities.json
        |
HeroAbilitiesConfigProvider -> HeroAbilityService (pure rules)
        |
HeroAbilitiesMissionLogic -> HeroAbilityHost (1 s pass) -> HeroAbilityCaster
                                     |                          |
                         RaceAbilitySensor.Sense      HeroBuffLedger -> RaceAbilityHooks.BuffEffects -> the models
                                                      MoraleShock, RaceAbilityVisuals
```

## Configuration

### Config File: `Main/_Module/ModuleData/hero_abilities/hero_abilities.json`

A full restart is needed for a change (the provider is a lazy singleton). The file's `_comment` names every rule.

| Field | Type | Description |
|-------|------|-------------|
| `enabled` | bool | Feature switch |
| `heroes[].characterId` | string | The mission hero's character id; unique, case-insensitive |
| `heroes[].glow` | `#RRGGBB` | The outline while one of his abilities runs; a bad value means no outline |
| `heroes[].passive` | effects | His for the whole mission |
| `abilities[].id` | string | `^[a-z0-9_]{1,63}$`, unique in the file; needs a name row in `HeroAbilityNames` |
| `abilities[].cooldownSeconds` | 60..120 | Else the ability is dropped |
| `abilities[].durationSeconds` | above 0, at most the cooldown | Else the ability is dropped |
| `abilities[].requires`, `anyOf` | triggers | The race abilities' trigger kinds; `KinWithin`, `KinFell` and `LandedKill` are skipped |
| `abilities[].radius`, `enemyShockRadius` | 0..15 m | Else 0 |
| `abilities[].allyEffects`, `selfEffects` | effects | The race abilities' effect keys; an effect only strengthens; damage reduction at most 90, a resistance at most 1000, any other percent at most 300 |
| `abilities[].allyMorale`, `enemyShockMorale` | 0..50 | Else 0 |
| `abilities[].warCry` | `Yell`, `Charge`, `Victory`, `Grunt` or empty | Else silent |

### Current Values

| Ability | Hero | Cooldown / duration | Trigger | Effect |
|---|---|---|---|---|
| For Frodo! (`aragorn_for_frodo`) | Aragorn | 90 s / 15 s | 3 enemies within 10 m | allies in 15 m: melee damage 15 %, swing speed 10 %, damage reduction 15 %, knockdown resistance 50 %, morale floor 60, +25 morale; enemies in 10 m lose 10 morale |
| Heir of Isildur (`aragorn_isildurs_heir`) | Aragorn | 120 s / 12 s | health below 40 % | self: damage reduction 40 %, shrugs off blows; allies in 10 m: block and parry 20 %, damage reduction 20 % |
| Baruk Khazâd! (`gimli_baruk_khazad`) | Gimli | 75 s / 12 s | 3 enemies within 8 m | self: melee damage 25 %, swing speed 20 %, shrugs off blows; allies in 12 m: melee damage 10 %; enemies in 10 m lose 15 morale |
| Still Standing (`gimli_still_standing`) | Gimli | 120 s / 15 s | health below 35 % | self: damage reduction 45 %, swing speed 25 % |

Gimli's passive: knockdown resistance 1000 %, knock-back resistance 300 %. The hero receives his `selfEffects` and
his `allyEffects` together. The trigger counts are 3, not 5, because a wall lane takes attackers one ladder at a
time. Every number is a first guess for tuning. Legolas has no row yet (Phase 5).

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/HeroAbilities/HeroAbilitiesModule.cs` | Registrations, the mission logic, `RaceAbilityHooks.BuffEffects` |
| `Main/Features/HeroAbilities/HeroAbilityService.cs` | The pure rules: kit lookup, ability pick, who decides, ally morale |
| `Main/Features/HeroAbilities/HeroAbilitiesConfigProvider.cs` | Loads and validates the JSON |
| `Main/Features/HeroAbilities/HeroAbilityRuntime.cs` | Mission-time state: the ledger and the services the hooks need |
| `Main/Features/HeroAbilities/HeroAbilityNames.cs` | Ability names and the cast line |
| `Main/Features/HeroAbilities/Domain/HeroBuffLedger.cs` | Copy-on-write grants per soldier |
| `Main/Features/HeroAbilities/Hooks/HeroAbilitiesMissionLogic.cs` | Entry point: gate, pass clock, engine events, check lines |
| `Main/Features/HeroAbilities/Hooks/HeroAbilityHost.cs` | The heroes of one mission, the pass, the outline, the expiry |
| `Main/Features/HeroAbilities/Hooks/HeroAbilityCaster.cs` | One cast: grants, morale, shock, cry, sparks, message |
| `Main/Features/HeroAbilities/Hooks/HeroAbilityChecks.cs` | The pure verdicts of the check lines |
| `Main/Features/HeroAbilities/Hooks/HeroMissionGate.cs` | The mission gate |
| `Main/Features/DreadAura/Hooks/MoraleShock.cs` | The one-shot morale drain, shared with Signature Strikes |
| `Main/_Module/ModuleData/hero_abilities/hero_abilities.json` | Configuration |
| `tools/ingame_checks/1359.toml` | The in-game check manifest |

## Dependencies

- Race Abilities: `RaceAbilityHooks` (the model seams), `RaceAbilitySensor`, `RaceAbilityService`, `RaceAbilityVisuals`
  (`Burst`, `Outline`), `RaceAbilityActivator.WarCry`, `RaceAbilityEffectsMerge`.
- Dread Aura: `MoraleShock`, `DreadAgentGate`, `IDreadRegistry`.
- Signature Strikes: the mission gate and the killing blows.
- Co-op: `CoopMissionOwnership`; the logic is `AgentOwner` ([coop-interop.md](coop-interop.md)).

## Tests

- `TAOM.Tests/Features/HeroAbilities/`: the service, the ledger (with a concurrent reader test), the config provider,
  the gate, the names, the check verdicts (`HeroAbilityChecksTests`), the shipped JSON
  (`ShippedHeroAbilitiesConfigTests`) and the source pins of the engine side (`HeroAbilitiesWiringTests`).
- `ShippedHeroAurasConfigTests.AragornAura_NamesTheMissionHero`.

## How to verify in game

**Preconditions:** a TAOM build of this branch; the Long Defeat from the main menu.

1. `python tools/run_long_defeat.py --waves 3 --garrison-scale 1.3 --start-gold 5000`, then
   `python tools/ingame_verdict.py --issue 1359`.
2. `[Check] id=taom.hero-abilities.gate result=pass ... combat=1 config=1 kits=2` once per mission.
3. `[Check] id=taom.hero-abilities.fired result=pass hero=taom_hero_aragorn fired=<1 or more> ...` and the same for
   `taom_hero_gimli`, at the mission end. `contact_s` counts the passes in which the kit's easiest enemy trigger held
   by an independent count (3 enemies within 10 m for Aragorn, 3 within 8 m for Gimli, in 3D). `fired=0` after 20 or
   more such seconds fails; a hero who never met that many enemies is skipped (`reason=no-contact`). A run of skips
   only is not a pass.
4. `[Check] id=taom.hero-abilities.stats-refreshed result=pass ...`: the swing speed moved by what the buff wants.
5. `[Check] id=taom.signature-strikes.instant-kill result=pass signature=(aragorn|gimli) ... died=1`. A 3-wave run
   may land no killing blow; the check then reads NOT REACHED.
6. The log line `[HeroAuras] Mission end: ... aragorn_heir_of_isildur: <n> soldier(s)` with n of 1 or more: Aragorn's
   aura reached the men around him. The manifest reads this plain line, not the `taom.hero-auras.reached` [Check]
   line, because that line names no aura and trunk already writes it.
7. No `[HeroAbilities] disabled for this mission after` line.
8. Only a person can see: the war cry, the sparks and the outline at a cast, and the message line. Enemies do not
   break and run from the shock in the Long Defeat: the drain is applied (`shocked=` in the fired line) but cannot
   rout an attacker while the Long Defeat holds him. What fear should do there is an open decision for the maintainer.

## How to add an ability

1. Add the ability to the hero's `abilities` in `hero_abilities.json`.
2. Add its name row to `HeroAbilityNames.Name` with a `taom_hero_ability_<id>` key, and register the key.
3. Code is needed only for a new effect channel or trigger kind.

## Traps

- **The story mission has no kit hero yet.** `characters/taom_mission_heroes.xml` loads for Custom Game only
  (`Main/_Module/SubModule.xml`), so no `taom_hero_*` body exists in the campaign-hosted story mission until a later
  phase of #1325 loads the characters in a campaign. The gate opens there, but nothing casts.
- **Phase 6: Aragorn's aura in the story mission.** `HeroAuraPulse` finds a hero through
  `DreadSourceTracker.HeroAxisId`. In a campaign it reads `CharacterObject.HeroObject?.StringId`, which is null for a
  mission character with no `<Hero>` entry, so in the Helm's Deep story mission the aura misses `taom_hero_aragorn`
  without a warning. The Long Defeat is a Custom Battle (`CustomGame.OnRegisterTypes` registers
  `BasicCharacterObject`, line 99 of `TaleWorlds.MountAndBlade.CustomBattle.CustomGame.cs` in the v1.5.7 source), where
  it reads the character id.
- A buffed soldier counts as moved by an ability (`RaceAbilityHooks.HasLiveEffects`) while his buff lasts, so the
  Long Defeat's boon read-back passes him over for 12 to 15 s. With no plain soldier left its check is skipped
  (`reason=no-soldier`), never failed.
- `HeroAurasConfigProvider` sets no upper bound on an aura's damage reduction; the hero abilities cap theirs at 90.

**Proof run.** The unattended run 20261115-232309 (4 waves, commit 5ee569711) reads PASS for all five checks in
`python tools/ingame_verdict.py --issue 1359`. Aragorn cast 11 times and Gimli 8 (`fired=11 contact_s=287 allies=365`
and `fired=8 contact_s=206 allies=249`). The stats sample read `ratio=1.100 want=1.100` on "For Frodo!". Gimli's
overhead killed an Uruk-hai warrior outright (`chance=0.35 roll=0.108 health=77 damage=78 died=1`; one other roll was
lost or filtered). Aragorn's aura reached 283 soldiers. Both heroes stood through all four waves, with 16 and 15 kills
at the last clear; in the run before the abilities, Aragorn fell twice in three waves. A screenshot of the run shows
the line "Aragorn: For Frodo!". Not seen by any run: the war cry, the sparks and the outline, the low-health ability
of Gimli, and an ability of a hero on the enemy side.

## Changelog

- 2026-11-15: first version (#1359): Aragorn's and Gimli's timed abilities, their killing blows and Aragorn's aura in
  the Long Defeat. Proved by the unattended run 20261115-232309.

## GitHub Issue

- **Issue:** #1359: Hero abilities: timed abilities and killing blows for Aragorn and Gimli
- **Status:** Open
