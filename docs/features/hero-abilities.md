# Hero Abilities

## Overview

The Long Defeat's mission heroes Aragorn (`taom_hero_aragorn`) and Gimli (`taom_hero_gimli`) cast timed abilities
(#1359, Phase 4 of #1325). A cast strengthens the hero and the soldiers of his own team around him, raises their
morale, and shakes the enemies near him. Their overhead strikes can also kill a soldier outright: that part is a
Signature Strikes row ([signature-strikes.md](signature-strikes.md), "Killing blows"). The abilities work only in the
Long Defeat missions. The gate also opens in the Helm's Deep story mission, but no hero with a kit stands there until
a later phase of #1325 loads the mission heroes in a campaign (`characters/taom_mission_heroes.xml` loads for Custom
Game only, `Main/_Module/SubModule.xml`). A hired campaign companion keeps his normal strength.

Legolas (`taom_hero_legolas`) has a ranged kit (#1361, Phase 5): his arrows have no aim error, pass a shield that
blocks them, and can kill a soldier outright; three timed abilities make him shoot faster, add two arrows to each shot,
or speed up the archers around him. His kit works where the gate opens, the Long Defeat today.

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
  has no contact. The query takes the trigger's full range, as the sensor's does (`RaceAbilitySensor.cs:46`).
- Aragorn's passive is his existing hero aura `aragorn_heir_of_isildur`, which now names `taom_hero_aragorn`
  ([hero-auras.md](hero-auras.md)).

### The ranged kit (#1361)

- **Recognition.** `HeroAbilityRuntime.Ranged` is a copy-on-write roster of agent to `HeroRangedConfig`. The host adds
  a hero with a `ranged` block on his first owned pass, before the stats refresh that reads it, and removes him when he
  drops. A stand-down clears it before its expiry refresh, so the refresh drops his zeros. The models read it through
  `HeroRangedHooks.Roster`; a Custom Battle that merely contains `taom_hero_legolas` arms nothing.
- **Aim.** `zeroAimError` makes `HeroRangedHooks.ApplyStats`, the last statement of both stat models'
  `UpdateAgentStats`, write 0 to the ten aim-error properties the engine's models set (`HeroPrecisionApplier`). Native
  reads them; what native does with them is unverified. "Never misses" is zero aim error, never a homing arrow.
- **Shield pierce.** `pierceShields` makes `CombatMechanicsHooks.PenetrationFlags` add `CanPenetrateShield` (never
  `MultiplePenetration`) after the shield-penetration service, so its MCM switch does not turn it off. The engine
  passes a flagged arrow when the shield breaks or the inflicted damage exceeds 30 + 3 x the shield's armour
  (`Mission.cs:5831-5845`). `CombatMechanicsHooks.ShieldPierceFloor`, in both damage models' `ApplyGeneralDamageModifiers`
  (the last step before the engine rounds the damage), raises his shield-blocked damage to just above that threshold
  ([combat-mechanics.md](combat-mechanics.md)).
- **Arrow kill.** `HeroRangedMissionLogic` sees a registered missile blow in `OnAgentHit` and only enqueues; on the
  next tick `HeroArrowKills` asks the one instant-kill executor of the melee strikes (`SignatureInstantKill.TryKill`,
  decision R3) with `headshotKillChance` or `bodyKillChance`. Its victim filter decides (no hero, player, general,
  signature bearer, kill-immune race or soldier shrugging off blows). A hit a shield or a weapon blocked, an extra
  arrow and a hit that lands after he fell or while he fades out never roll, and a victim the arrow itself already
  killed is no call. The check line reads `signature=taom_hero_legolas_arrow`.
- **Extra arrows.** An ability with `extraArrows` opens a window (`HeroAbilityRuntime.Bursts`). During it each of his
  own shots (`OnAgentShootMissile`, enqueued) makes `HeroMultiShotRunner` pick the enemy within 6 degrees of the shot
  (`ExtraShotPicker`), then up to N enemies within `extraArrowSpreadMeters` of it, and fire `taom_legolas_arrow` at
  each through `Mission.AddCustomMissile`. The release angle is the engine's own solver
  (`Mission.GetMissileVerticalAimCorrection`, as `RangedSiegeWeapon.GetTargetReleaseAngle` uses it), at the chest and
  then at a lead point. `AddCustomMissile` adds no bow damage, so the item has the bow's thrust plus the arrow's (90).
  The extras belong to the live hero; the Long Defeat volley filter matches only its own items and indices. An
  arrow is an extra by its item alone (`HeroMissileEvent.IsExtraArrow`): native gives a freed missile index to the
  next missile at once, so an index tells nothing about a later arrow.
- **Missile indices.** The hits and pierce counts key on the missile index a hit carries. What a tally remembers of an
  index ends at the missile's removal: `Mission.OnMissileRemovedEvent` enqueues the index into the same queue as the
  hits, behind that missile's own hit callbacks, and every tally forgets it on the drain.
- **No volley of his.** A held hero never owns an Elven Volley (`VolleyOwnerRule`, [long-defeat.md](long-defeat.md)),
  so a volley arrow never carries his pierce, his damage floor or his kill roll, and never counts in his checks.
- **Ranged allies.** `alliesRangedOnly` gives `allyEffects` only to allies with a bow or crossbow and ammunition
  (`Agent.IsRangedCached`).
- No Legolas ability touches enemy morale: the Long Defeat holds every attacker (`LongDefeatMorale.HoldsAttacker`).

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
| `heroes[].ranged` | object, optional | The ranged kit (#1361); absent on Aragorn and Gimli |
| `ranged.zeroAimError`, `ranged.pierceShields` | bool | Zero aim error; arrows pass a blocking shield |
| `ranged.headshotKillChance`, `ranged.bodyKillChance` | 0..1 | The kill roll on an unblocked arrow hit; else 0 |
| `ranged.extraArrowItem` | `^[a-z0-9_]{1,63}$` or empty | The extra arrow's item (`hero_abilities_items.xml`); else empty |
| `ranged.extraArrowSpreadMeters` | 0..20 m | How near the target an extra arrow's enemy stands; else 0 |
| `ranged.extraArrowRangeMeters` | 0..150 m | The farthest extra arrow target; else 0 |
| `abilities[].extraArrows` | 0..3 | Extra arrows per shot while it runs; 0 unless the kit has a ranged block with an item, a spread and a range above 0 |
| `abilities[].alliesRangedOnly` | bool | `allyEffects` only for allies with a bow or crossbow and ammunition |

### Current Values

| Ability | Hero | Cooldown / duration | Trigger | Effect |
|---|---|---|---|---|
| For Frodo! (`aragorn_for_frodo`) | Aragorn | 90 s / 15 s | 3 enemies within 10 m | allies in 15 m: melee damage 15 %, swing speed 10 %, damage reduction 15 %, knockdown resistance 50 %, morale floor 60, +25 morale; enemies in 10 m lose 10 morale |
| Heir of Isildur (`aragorn_isildurs_heir`) | Aragorn | 120 s / 12 s | health below 40 % | self: damage reduction 40 %, shrugs off blows; allies in 10 m: block and parry 20 %, damage reduction 20 % |
| Baruk Khazâd! (`gimli_baruk_khazad`) | Gimli | 75 s / 12 s | 3 enemies within 8 m | self: melee damage 25 %, swing speed 20 %, shrugs off blows; allies in 12 m: melee damage 10 %; enemies in 10 m lose 15 morale |
| Still Standing (`gimli_still_standing`) | Gimli | 120 s / 15 s | health below 35 % | self: damage reduction 45 %, swing speed 25 % |
| Rapid Fire (`legolas_rapid_fire`) | Legolas | 75 s / 15 s | a ranged target within 40 m and 3 enemies within 40 m | self: draw speed 60 %, reload speed 50 % |
| Many Arrows (`legolas_many_arrows`) | Legolas | 90 s / 12 s | a ranged target within 40 m and 5 enemies within 40 m | 2 extra arrows per shot |
| Archer's Eye (`legolas_archers_eye`) | Legolas | 120 s / 20 s | 5 enemies within 40 m | ranged allies in 15 m: draw speed 20 %, aim error -50 %, ranged damage 15 % |

Gimli's passive: knockdown resistance 1000 %, knock-back resistance 300 %. The hero receives his `selfEffects` and
his `allyEffects` together. The trigger counts are 3, not 5, because a wall lane takes attackers one ladder at a
time. Legolas's ranged block: zero aim error, shield pierce, headshot kill chance 1.0, body kill chance 0.10, extra
arrow `taom_legolas_arrow` within 10 m of his target and 120 m of him; outline `#7FD4FF`. "A ranged target within
40 m" needs his bow in his hand (`RaceAbilityService.cs:79-80`). Every number is a first guess for tuning.

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
| `Main/Features/HeroAbilities/Domain/HeroRangedConfig.cs`, `HeroRangedRoster.cs`, `HeroBurst.cs` | The ranged block, the roster the models read, an extra-arrow window |
| `Main/Features/HeroAbilities/Ranged/` | Pure: the extra arrows' targets (`ExtraShotPicker`) and aim (`ExtraShotAim`), the kill chance (`HeroRangedService`) |
| `Main/Features/HeroAbilities/Hooks/HeroRangedHooks.cs`, `HeroPrecisionApplier.cs` | What the stat and damage models ask: the zero aim error, the shield grant |
| `Main/Features/HeroAbilities/Hooks/HeroRangedMissionLogic.cs` | Entry point of the ranged kit: the missile callbacks enqueue, the tick drains, check lines |
| `Main/Features/HeroAbilities/Hooks/HeroMissileQueue.cs`, `HeroRangedTally.cs`, `HeroRangedTallies.cs`, `HeroRangedChecks.cs` | The callback queue, the counts of one hero body, one mission's tallies, the verdicts |
| `Main/Features/HeroAbilities/Hooks/HeroMultiShotRunner.cs`, `HeroArrowKills.cs` | The extra arrows; the arrow's kill roll through `SignatureInstantKill` |
| `Main/Features/CombatMechanics/ShieldPierceMath.cs` | The damage floor above the engine's shield pass-through threshold |
| `Main/_Module/ModuleData/hero_abilities/hero_abilities.json` | Configuration |
| `Main/_Module/ModuleData/hero_abilities/hero_abilities_items.xml` | `taom_legolas_arrow`, the extra arrow (Custom Game only) |
| `tools/ingame_checks/1359.toml`, `1361.toml` | The in-game check manifests |

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
- The ranged kit: `HeroAbilitiesRangedConfigTests`, `HeroRangedRosterTests`, `ExtraShotPickerTests`,
  `ExtraShotAimTests`, `HeroRangedServiceTests`, `HeroRangedHooksTests`, `HeroPrecisionApplierTests`,
  `HeroRangedTallyTests`, `HeroRangedTalliesTests`, `HeroMissileQueueTests`, `HeroRangedChecksTests` (each verdict's line matched against the
  patterns of `1361.toml`), `TAOM.Tests/Features/CombatMechanics/ShieldPierceMathTests`, and the model pins in
  `CustomBattleDamageModelTests` and `CombatMechanicsModelInvariantsTests`.
- The hooks this kit reaches in other features: the held-hero case in `VolleyOwnerRuleTests` (a held hero never owns a
  Long Defeat volley) and the `PenetrationFlags` grant and `ShieldPierceFloor` cases in `CombatMechanicsHooksTests`.
- `tools/tests/test_hero_abilities_items.py`: the item file, its thrust against the installed Armory bow and arrow, and
  its meshes.
- `tools/tests/test_ingame_verdict.py`, `HeroAbilities1359And1361Tests`: it keeps the two manifests from reading each
  other's lines.
- `ShippedHeroAurasConfigTests.AragornAura_NamesTheMissionHero`.

## How to verify in game

**Preconditions:** a TAOM build of this branch; the Long Defeat from the main menu.

1. `python tools/run_long_defeat.py --waves 4 --garrison-scale 1.3 --start-gold 5000`, then
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

**Legolas (#1361).** The same run, then `python tools/ingame_verdict.py --issue 1361`. Every ranged line has the key
`hr<N>-taom_hero_legolas-<k>`. A run in which the bot never buys him writes none, and every check reads NOT REACHED.

1. `taom.hero-abilities.fired ... hero=taom_hero_legolas fired=<1 or more> ... owned=1`.
2. `taom.hero-abilities.precision result=pass ... nonzero=0`: the ten aim-error stats were 0 at the first sample with
   his bow in hand. It proves the zeros were written, not what native does with them.
3. `taom.hero-abilities.hits result=pass`: 20 or more shots, and at least half met an enemy. One run has no vanilla
   control, and his error is already small at the highest AI difficulty, so this is a floor, not a proof of a gain.
4. `taom.hero-abilities.pierce result=pass ... passed=<1 or more>`: an unblocked hit by the same arrow on the same
   victim followed his shield-blocked hit before the missile was removed. With `pierceShields` off the line is a skip
   (`reason=off`); with `zeroAimError` off, so is the precision line.
5. `taom.hero-abilities.multishot result=pass`: two or more extra arrows fired and one hit. The line has no `added=`
   field: `AddCustomMissile` always lists the missile it makes (`Mission.cs:4969-4971`).
6. `taom.hero-abilities.arrow-kill result=pass` and `taom.signature-strikes.instant-kill result=pass
   signature=taom_hero_legolas_arrow ... died=1`.
7. No `[HeroAbilities] ranged kit disabled for this mission after` line.
8. Only a person can see: Rapid Fire's faster draw, the extra arrows in flight, Archer's Eye on the archers around him.

## Known limits

- **Native can also ask the stat model for his inaccuracy.** `Agent.GetWeaponInaccuracy` (`Agent.cs:1876`, a native
  callback) computes the value from the stat model's `GetWeaponInaccuracy` formula, not from the zeroed
  `AgentDrivenProperties`. For his bow (accuracy 98) at Bow skill 300 that is (100 - 98) x (1 - 0.002 x 300) x 0.001,
  about 0.0008 (`AgentStatCalculateModel.cs:76-88`). That is small, so no override was added; the precision check
  reads only the properties.
- **The arrow kill's hit sound plays between them.** The kill blow is `CustomAttacksUtils.TakeDamage`'s, whose
  position is the midpoint of shooter and victim, so at range the hit sound plays between Legolas and his victim. A
  known cosmetic point.

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
- **The 15 m clamp.** `HeroAbilityHost.InContact` once clamped its query to 15 m. With Legolas's 40 m trigger it would
  count no contact, and his fired check could never fail. The query now takes the trigger's range. Past 15 m the
  native query walks every agent (traps/mission.md, "Native branches"); it runs once a second per hero.
- **The extra arrow is Custom Game only.** `hero_abilities_items.xml` loads beside `long_defeat_items`. A kit that
  names an item the game did not load fires no extra arrow; the multishot line then shows `item=0` and fails.
  `taom_legolas_arrow` loads in Custom Game only, so the phase that puts the heroes in a campaign story mission must
  load the item there too.
- **Co-op.** `AddCustomMissile` writes `missionObjectToIgnore.Id` on a server or a recorder (`Mission.cs:4972-4975`),
  and an extra arrow has none, so no extra is added there (logged once). The ranged logic is `AgentOwner` and the
  roster holds only heroes this peer owns. Co-op is not proven in a game.
- **His arrows break shields.** The pierce floor raises his shield damage to above 30 + 3 x armour, so a shield with
  little health left breaks. That is the design (decision R7), not a bug.
- The Long Defeat's boon read-back skips heroes and soldiers with live effects (`LongDefeatEffectsAdapter.cs:294,
  316`), so it cannot sample him.

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
- 2026-11-16: Legolas's ranged kit (#1361): zero aim error, shield pierce, the arrow kill, extra arrows, and three
  abilities. Not yet run in the game.

## GitHub Issue

- **Issue:** #1359: Hero abilities: timed abilities and killing blows for Aragorn and Gimli
- **Issue:** #1361: Hero abilities: Legolas's ranged kit
- **Status:** Open
