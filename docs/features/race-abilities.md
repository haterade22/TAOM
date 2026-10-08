# Race Abilities

## Overview

AI soldiers fight with a battle ability for their race, or for men their culture, on a cooldown: a behaviour tree
on each soldier waits for his moment, fires the ability for a few seconds, and kin standing near him whose ability
is ready fire with him. Berserkers go berserk when hurt in melee, Uruk-hai fall into bloodlust on a kill, dwarves
stand fast against a charge or a crowd, elves quicken, orcs swarm, goblins scurry, Gondor closes ranks, Rohan's
riders spur on, and so on: 18 abilities, one generic tree, each on a cooldown of one to two minutes. While a fury,
guard or dread ability is active the soldier wears a coloured outline, and sparks burst as an ability fires. The
numbers live in `race_abilities.json`. Logging and the console command `taom.print_race_abilities` show what each
ability did in a battle. Issue: #730 (the feature), #731 (the translation of its 20 strings).

## Why This Exists

- **Vanilla behavior:** a soldier's fighting is his skills and his kit. Race and culture change nothing in a
  fight beyond the Monster's body.
- **TAOM requirement:** the peoples of Middle-earth should fight differently, and visibly so: a berserker pack
  breaking a shield line, a dwarven line locking against cavalry, elven archers loosing faster as the enemy
  closes, an orc mob growing bolder the bigger it is.
- **Already shipped, and not repeated here:** the permanent race passives in `combat_mechanics_config.json`
  `raceModifiers` (dwarf knockdown and stagger resistance, elf crush-through bonus, orc swing energy) and the
  per-culture tactics, aggression and morale of [Culture Doctrine](culture-doctrine.md). These abilities are the
  timed layer on top.

## Architecture

### Design Challenge

- **Hundreds of trees, not a dozen.** Creature trees run on a few agents every tick; this tree runs on every
  profiled soldier. It runs one pass a second, its first pass staggered across that second, and checks the
  cooldown before anything is sensed. The sensor then gathers only what could change the answer
  (`RaceAbilityService.PlanScan`): nothing at all when the soldier's own state already rules the ability out (a
  Rohirrim on foot, a Dale soldier without a bow), enemies only out to the widest trigger that can still hold,
  a rider's closing speed only for a CavalryClosing trigger, and kin and fallen kin only once every other
  requirement holds. A test pins that the plan never changes an answer for any shipped profile.
  `BehaviorTreeAgentComponent` checks slot identity only when a tree is due, not every tick, and
  `BehaviorTreeMissionLogic` copies its schedule without allocating.
- **Two model stacks.** Custom Battle installs its own stat, damage and morale models. Its damage model
  (`CustomAgentApplyDamageModel`) reads no driven-property damage bonus at all, so ability damage cannot ride the
  stat bag; and none of Combat Mechanics' crush-through or shrug-off rules run there.
- **Stats that compound.** A post-pass that multiplies a driven property is only safe when the base model
  rewrites that property on every `UpdateAgentStats`. Custom Battle sets armour and
  `OffhandWeaponDefendSpeedMultiplier` once, at spawn, so those are never touched (Engine levers).
- **Riders.** A mounted soldier moves at his horse's speed: the rider's own speed stats do nothing in the saddle
  (#611). Mount speed is written on the horse's own stats through its rider.
- **Agent handles and threads.** The engine recycles a dead agent's slot (#592) and raises removals off the main
  thread (#634). The store is keyed by object identity, every held handle is checked with
  `AgentSlotIdentity.IsCurrentOccupant`, and a death is handled with the values (position, team, race, culture,
  whether the victim was a soldier) captured at the callback: inline on the main thread, inside a catch because
  the engine's removal loop guards nothing, and deferred to the mission tick anywhere else.
- **One tree per agent.** TAOM's `BehaviorTreeMissionLogic` keeps a single tree per agent, so the attach skips an
  agent that already carries one. Late spawns are always safe (the creature behaviors see `OnAgentBuild` first);
  on the first tick behaviors tick last to first, so this scan runs before the creatures'. No shipped profile
  covers a creature-tree agent, so that order matters only if one ever does.

### Solution Approach

`RaceAbilitiesModule` (a `TaomFeatureModule`) registers the services, sets `RaceAbilityHooks.Runtime`, and adds
`RaceAbilitiesMissionLogic` to every mission. The mission logic decides its gate on the first tick (no
`OnBehaviorInitialize`: a module's behavior is added after the engine dispatches it, #606): combat missions with
no multiplayer session (`SignatureMissionGate`), the MCM switch on, and `race_abilities.json`'s own `enabled`
on. It attaches a `RaceAbilityBehaviorTree` to every agent with a profile, at the first tick and then as soldiers
spawn (Custom Battle spawns its armies after the first tick, so there nearly every tree is a late spawn).

A profile comes from `RaceAbilityProfileResolver`: the soldier's race profile when his race has one; otherwise, for
a soldier of the human race only, his culture's profile. A troll in Mordor's ranks is not one of Mordor's men. The
culture is the troop's own (`Character.Culture`); for a hero that is his template's culture, which can differ from
`Hero.Culture` for a born child. Culture Doctrine reads it the same way.

The tree, the same for every profile:

```
[root]
  main (Selector)
    ai soldier (Sequence, IsAiAgentDecorator)
      RaceAbilityTask: not fleeing, off cooldown, a trigger holds -> activate, war cry, rally ready kin
    ReturnTrueTask (player-controlled: never fires)
```

`base(1000)` gives one pass a second (`BehaviorTreeAgentComponent` divides the delay in whole seconds). The one
leaf finishes on the pass it starts, so each pass is one decision; a `SleepTask` would finish only on the next pass
and halve the decision rate. The tree resolves the runtime once in `BuildTree` and passes it to the task (the warg
tree's shape). A pass that throws logs once per battle and fires nothing, instead of stopping the soldier's tree.

The engine side is six small boundary classes around one runtime, and every rule they apply is
`RaceAbilityService`'s:

| Class | Does |
|---|---|
| `RaceAbilityRuntime` | Holds the store, telemetry, fallen-kin memory and wave counter; resolves profiles and kinship; prints the status; clears everything at mission end |
| `RaceAbilitySensor` | Reads health, morale, mount and weapon, then what the scan plan asks for: enemies, kin, fallen kin |
| `RaceAbilityActivator` | Fires the ability and the rally, scales each soldier for his tier (and crowd, for a kin bonus), refreshes his stats and his horse's, shouts |
| `RaceAbilityTicker` | Ages every ability (Active, Spent, Ready), settles a burnt-out frenzy's morale price, tops up morale floors, pulses fear auras, repaints the outlines, posts the wave messages and the 30 s report |
| `RaceAbilityDeaths` | Forgets the dead (and clears a fallen soldier's outline), remembers fallen kin, credits the killer of a soldier (extension, heal, fear on kill) |
| `RaceAbilityVisuals` | Outlines the active soldiers nearest the camera, sparks as an ability fires (Glow and sparks below) |

The effects reach the engine through six shared models, each calling one `RaceAbilityHooks` method per seam:

| Model | Mode | Seams |
|---|---|---|
| `TaomAgentStatCalculateModel` | campaign | stat post-pass after the aggression pass (a horse gets its rider's mount speed); knockdown, knock-back and dismount resistance |
| `TaomCustomBattleAgentStatCalculateModel` | Custom Battle | the same |
| `TaomCombatMechanicsModel` | campaign | crush-through verdict first; melee and ranged damage amplification; damage reduction last; shrug-off |
| `TaomCustomBattleCreatureDamageModel` | Custom Battle | the same four, nothing else of Combat Mechanics |
| `TaomBattleMoraleModel` | campaign | no panic while a morale floor is live |
| `TaomCustomBattleMoraleModel` | Custom Battle | the same |

### Crush-through order

`RaceAbilityHooks.CrushVerdict` runs before Combat Mechanics' own rules: a defender in Stand Fast holds against
every crush-through (a troll's included), a raging swing breaks any block, and with neither live the existing
rules decide. Hold beats force. The engine never crushes a chamber block, Berserk or not.

### Glow and sparks

Mike's pick (2026-10-04): an outline for the whole window, and a burst of sparks as the ability fires.

- **Colour by kind, the same on both sides.** Fury red `#E03A2E`: Berserk, Bloodlust, Swarm, Hill-clan Fury,
  Corsair Raid, Variag Ferocity. Guard steel blue `#5B9BD5`: Stand Fast, Iron Discipline, Guard of the Citadel,
  Wainrider Wall. Dread violet `#9B59FF`: Shadow of the Necromancer, Servants of the Shadow. The six speed and aim
  abilities (Swiftness, Scurry, Hunter's Rush, Forth Eorlingas, Bard's Aim, Serpent's Venom) stay dark, so a
  crowd stays readable. Only the active phase glows: a spent soldier is dark, so an outline means "dangerous now".
- **Who.** The `visuals.maxGlowing` (40) active soldiers nearest the camera, picked again and repainted every half
  second on the ticker's pulse, and at once after a glowing ability's wave fires (`RaceAbilityGlowLedger`). An
  outline ends the moment its window does. Repainting is required: the outline is painted on the soldier's meshes
  as they are, and an equipment rebuild (a soldier made banner bearer) replaces them
  (`Agent.UpdateSpawnEquipmentAndRefreshVisuals`). Vanilla's multiplayer outline view repaints at the same rhythm.
  Only the rider's visuals are painted; his horse is a separate agent.
- **Sparks.** `visuals.burst` (`psys_game_sparkle_a`, a vanilla effect) plays once at the chest of the soldier who
  fires and of every third kinsman who joins him, the war cry's crowd rule; it is fired and forgotten, as vanilla's
  own bursts are. The engine answers -1 for an effect name it does not know (its native lookup), which turns the
  sparks off with one warning; `RaceAbilitiesLiveKeyTests` checks the shipped name against the particle files the
  game loads.
- **Engine calls.** `MBAgentVisuals.SetContourColor(uint? color, bool alwaysVisible)` (an opaque `0xFFRRGGBB`; null
  clears it, 0 does not; `visuals.seeThrough` is the second argument) and `Scene.CreateBurstParticle(int,
  MatrixFrame)` with `ParticleSystemManager.GetRuntimeIdByName`. Nothing in vanilla single player or Custom Battle
  outlines soldiers in v1.5.3: `MissionAgentContourControllerView` sits in every mission but is compiled with
  `IsEnabled = false`. The engine keeps one colour per mesh and the last writer wins, so an engine bump should
  re-check for a new writer.
- **Safety.** The engine's outline setters check nothing, so a call on a deleted or recycled soldier's visuals can
  fault natively. Every write passes vanilla's own guard (not deleted, valid visuals) plus slot identity (#592),
  and runs only on the main thread (#634): from the activator, the ticker and the deaths handler, which runs inline
  inside the engine's removal callback when that arrives on the main thread and is parked for the next mission tick
  when it does not. A fallen soldier's outline is cleared on the death path, while his body is still his. Each
  visual call catches its own failures and logs once per battle on its own line, so it can never abort a rally, a
  phase change or a death. The effect name reaches a native 64-byte buffer, so the config bounds it.
- **Off.** MCM `Ability Glow and Sparks` switches both, and the outlines clear on the next pulse. Hide Battle UI
  hides both, as it hides vanilla's own outlines.
- **UNVERIFIED until seen in game:** the look (outline weight at a distance, the see-through setting), the frame
  cost of 40 outlined soldiers, that a re-equipped banner bearer is painted again within half a second, and whether
  a rider's outline also reaches his horse through the engine's entity tree. The first look (2026-10-04, a packed
  line) showed the outline mostly around legs and on a few raised arms (First in-game test below).

## Engine levers

Checked against the v1.5.3 decompile (`SandboxAgentStatCalculateModel`, `CustomBattleAgentStatCalculateModel`,
`AgentStatCalculateModel`, `CustomAgentApplyDamageModel`, `MissionCombatMechanicsHelper`, `Mission`).

| Effect field | Engine target | Notes |
|---|---|---|
| `moveSpeedPercent` | `MaxSpeedMultiplier`, `CombatMaxSpeedMultiplier` | rewritten in `UpdateHumanStats` in both models; on foot only |
| `accelerationPercent` | `TopSpeedReachDuration` (divided) | same |
| `mountSpeedPercent` | the ridden horse's `MountSpeed`, through its rider | rewritten in `UpdateHorseStats` in both models. A rider dismounted mid-ability may leave his old horse boosted until its next stat update (UNVERIFIED) |
| `swingSpeedPercent` | `SwingSpeedMultiplier` | rewritten every update |
| `drawSpeedPercent` | `ThrustOrRangedReadySpeedMultiplier` (the skill-driven bow draw, throw and thrust readying) | rewritten every update |
| `reloadSpeedPercent`, `missileSpeedPercent` | `ReloadSpeed`, `MissileSpeedMultiplier` | rewritten every update. Whether `MissileSpeedMultiplier` speeds a bow's arrows is UNVERIFIED, though likely: vanilla's own wet-weather penalty writes it for bows and crossbows |
| `blockAbilityPercent`, `parryAbilityPercent`, `attackEagernessPercent`, `aimErrorPercent` | `AIBlockOnDecideAbility`, `AIParryOnDecideAbility`, `AIAttackOnDecideChance` (each kept in 0 to 1), `AiShooterError` | rewritten in `SetAiRelatedProperties` on every update; how native weighs `AiShooterError` (base 0.008) is UNVERIFIED |
| `knockdownResistancePercent` | `GetKnockDownResistance` | on foot, the engine floors a soldier when the hit reaches `HealthLimit x (resistance - penetration)`, on weapon hits and on a horse charge that knocked him back. In campaign, Combat Mechanics floors any victim of a full-speed charge whose weight times 6 is at most the horse's and rider's before it reads resistance (`ChargeKnockdownService`, Branch A): every man, while dwarves and the uruk races are heavy enough to stay in Branch B, where resistance counts. Custom Battle reads the resistance on every charge. A rider is never floored as such: his knockdown resistance is the second roll of the dismount decision (below) |
| `knockbackResistancePercent` | `GetKnockBackResistance` | read only for a soldier on foot, for missiles, crush-throughs and wide-grip thrusts, and never for a hit that is shrugged off: a frontal horse charge and a kick or shield bash knock back regardless. So Stand Fast carries none: its shrug-off already prevents every knock-back that reads it |
| `dismountResistancePercent` | `GetDismountResistance` | a rider's first roll when a blow can dismount (a thrust with the dismount flag, or a hook swung by a man on foot, to the head, neck, chest, abdomen or shoulders; in campaign also a hero's bolt with the Hammer Bolts perk or throw with Knock Off); if he keeps his seat, a blow that can knock down then rolls his knockdown resistance (`MissionCombatMechanicsHelper.DecideAgentDismountedByBlow`), and either roll unhorses him |
| `meleeDamagePercent`, `rangedDamagePercent`, `damageReductionPercent` | the damage models' amplification and reduction steps | Custom Battle reads no driven-property damage bonus. They also scale damage to and from shields (Berserk breaks shields faster) and to objects; a fall keeps its damage both ways, and a hit on the soldier's horse is the horse's |
| `forceCrushThrough`, `holdAgainstCrush` | `DecideCrushedThrough` | a verdict, not a stat |
| `shrugOffBlows` | `DecideAgentShrugOffBlow` | on a weapon or missile hit: no flinch, no knockdown, knock-back or dismount, and the attacker's weapon bounces, as vanilla's shrug-off does. A horse charge never asks, so it still knocks a Stand Fast dwarf back (his knockdown resistance keeps him on his feet), and a kick or shield bash still knocks back. It also spares the soldier Sauron's guaranteed slam knockdown |
| `moraleFloor` | `CanPanicDueToMorale` false while live, plus a top-up every 0.5 s | exact for morale: the soldier cannot panic while it lasts. It does not stop a retreat order, or a retreat already under way |
| `moraleOnEnd` | `ChangeMorale` when the active window ends | the orcs' frenzy burns out |
| `healPerKill`, `fearOnKill*`, `fearAura*` | `Agent.Health`, `ChangeMorale` | fear is scaled through the registered morale model, as the Dread Aura and Signature Strikes do (in a campaign the victim's tier and hero resistance apply; Custom Battle's characters all resist alike), and only reaches live AI humans (`DreadAgentGate`); where several auras reach one enemy, only the strongest drains him. Unlike the Dread Aura, no per-race fear resistance applies |

**Never touched:** armour (`ArmorHead` and the rest) and `OffhandWeaponDefendSpeedMultiplier`. Custom Battle writes
them only in `InitializeAgentStats`, so a post-pass would stack on every update. A tougher stance is damage
reduction instead.

### Known limits

- **Scripted creature blows.** The troll's Brute Force ring, warg bites, trample and the signature-strike ring land
  through `CustomAttacksUtils.TakeDamage`, which writes the damage past the damage models. None of the defensive
  effects (damage reduction, shrug-off, resistances, the crush hold) applies to them.
- **Riderless creatures.** The sensor counts humanoid enemies only, and the engine's proximity query appears to
  return nothing else anyway (UNVERIFIED in native), so a riderless spider or warg is invisible to the enemy
  triggers.
- **Khand and Umbar field other kingdoms' troops.** Khand recruits Rhun's roster, so its soldiers, cavalry and
  chariots included, carry `khuzait` and fire Wainrider Wall. Variag Ferocity reaches only `battania` characters:
  Khand's lords (on chargers), the `caravan_master_khand` with each Khand notable's caravan, the
  `caravan_guard_khand` mercenaries Khand's taverns hire out (to the player, lords and caravans), the Variag
  Ravagers (vanilla's Wolfskins outlaws, kept `battania`, on foot), Khand's own ten wanderers (#758; vanilla's
  battania wanderers no longer spawn in new campaigns), and perhaps `guard_khand`
  (UNVERIFIED). The two never rally each other: a rally takes the same profile. Mike's decision
  (2026-10-04): leave Khand's ability as it is; he expects Khand's armies to use a lot of cavalry and chariots. As
  shipped, a Khand lord's party (Rhun's default template) has one cavalry stack in eight, and chariots come only by
  upgrading along Rhun's Wain line. Umbar's militia, patrols, villagers and rebels are Harad troops, and its
  garrisons start as Harad troops; they fire Serpent's Venom. Umbar's own recruits (`aux_basic` and the
  `umbar_elite` line), its lords and their armies carry `umbar` and fire Corsair Raid, as the corsair bandits do.
- **Riders and charioteers.** On a horse or a chariot, Wainrider Wall and Variag Ferocity act on the rider alone:
  neither protects, speeds or strengthens the mount (Combat Mechanics already scales charge damage by the rider's
  culture). A rider is never knocked back or down as such. A blow that can dismount him rolls his dismount
  resistance first; if he keeps his seat, a blow that can knock down rolls his knockdown resistance; either
  unhorses him. So Variag Ferocity guards both rolls and Wainrider Wall only the second. No horse or chariot charge
  strikes a mounted soldier at all (read in the engine's native code, not yet seen in play), so Wainrider Wall's
  damage reduction and knockdown resistance meet a charge only once he is on foot. The swing bonuses work in the
  saddle, Variag Ferocity's melee bonus covers the couched lance, and thrown spears get no bonus. Whether the
  block bonus changes how a mounted AI fights is UNVERIFIED.

## Configuration

### Config File: `Main/_Module/ModuleData/race_abilities/race_abilities.json`

Loaded once per process: an edit needs a restart. A missing or unreadable file gives the compiled profiles
(`RaceAbilityDefaults`), which the shipped file mirrors exactly (`ShippedRaceAbilitiesConfigTests`).

| Field | Meaning |
|---|---|
| `enabled` | the file's own switch, beside MCM's; the mission gate line and the console report show both |
| `tierScaling` | `baseTier`, `percentPerTier`, `minFactor`, `maxFactor`: effects scale with `GetBattleTier()` (0 to 7); heroes take `heroFactor`. The speed and guard prices scale too (an elite pays more for more); morale floors, radii and `moraleOnEnd` do not |
| `races.<race>` | keyed by the race name in `skins.xml`; a comma-separated key gives several races one profile. A race the engine does not register is skipped with a warning |
| `cultures.<culture>` | keyed by culture id (`vlandia` is Rohan, `empire` Dunland, `sturgia` Dale, `aserai` Harad, `khuzait` Rhun, `battania` Khand); human soldiers only, and only when their race has no profile; comma-separated aliases share one profile, and so one ability for the rally. Culture ids are not checked in game: `ShippedRaceAbilitiesConfigTests` pins the shipped ones against `taom_spcultures.xml` and the six re-skinned vanilla ids |
| `abilityId` | log name and display name (`RaceAbilityNames`); defaults to the first key |
| `cooldownSeconds`, `durationSeconds` | the cooldown runs from activation; raised to cover the longest window plus the spent phase |
| `spentSeconds`, `spent` | the aftermath and its effects (a `spent` block without `spentSeconds` warns, and so does a `moraleOnEnd` there, which is never read) |
| `killExtensionSeconds`, `maxDurationSeconds` | each kill while active adds time, never past the maximum from activation (an extension without room to extend warns) |
| `rallyRadius` | ready kin of the same profile and team within it fire too |
| `warCry` | `Yell`, `Charge`, `Victory`, `Grunt` or empty, in the soldier's own voice set |
| `glow` | the outline colour while the ability is active, `#RRGGBB` in any case, or empty for none; anything else warns and draws none |
| `visuals` | `maxGlowing` (0 to 200, default 40): at most this many soldiers outlined, the nearest to the camera; a profile's `glow` with `maxGlowing` 0 warns. `seeThrough`: draw the outline through walls and soldiers. `burst`: the engine particle effect played as an ability fires, or empty for none; 1 to 63 letters, digits or underscores (the engine copies it into a 64-byte buffer), else it reverts with a warning; checked against the engine on first use, and an unknown name turns the sparks off with one warning |
| `kinRaces` | races that count as kin for `KinWithin`, `KinFell` and the kin bonus, besides the same profile (never widens the rally); a list none of those reads warns |
| `kinBonus` | `radius`, `perKinPercent`, `maxKin`: extra melee damage per kinsman close by at activation (`perKinPercent` 0 warns) |
| `requires`, `anyOf` | every `requires` trigger holds and, when `anyOf` is not empty, one of them does |
| `effects` | percentages are whole numbers (20 = +20%); a fear radius without its morale, or the reverse, warns |

Trigger kinds: `Always` (a plain timer), `EnemyWithin` (range), `EnemiesWithin` (range, count), `NoEnemyWithin`
(range), `HealthBelow` (fraction), `MoraleBelow` (fraction of 100), `TookDamage` (lost health since the previous
pass, about a second), `Mounted`, `KinFell` (range, seconds), `KinWithin` (range, count), `LandedKill` (seconds),
`WoundedEnemyWithin` (range, fraction), `CavalryClosing` (range: a rider whose mount closes faster than 3 m/s),
`RangedTargetWithin` (range, while wielding a ranged weapon). A kind is one name, any case; a number or a
comma-separated list is rejected. Ranges are capped at 40 m and event windows at 30 s.

### Current Values

First guesses, to be tuned in a Custom Battle. The cooldowns are Mike's 1 to 2 minutes (2026-10-04): his
first four numbers (berserker 15 s, Uruk-hai 25 s, dwarf and elf 30 s) times four, keeping their order. The
effects are a tier-3 soldier's: `tierScaling` runs them from x0.85 to x1.2 by tier (Rhun's chariot riders
get x1.2), and heroes take x1.25.

**Races**

| Race | Ability | Cooldown / length | Fires when | Effect | Price |
|---|---|---|---|---|---|
| `berserker` | Berserk | 60 s / 6 s | enemy within 3 m and (health 75% or less, or took damage, or kin fell within 10 m in 5 s) | swings crush through; +20% melee; +15% swing; +10% speed; no flinch; cannot panic | block and parry -60%; 3 s spent: -20% speed, -10% swing |
| `uruk_hai` | Bloodlust | 100 s / 8 s, +2 s a kill to 14 s | a kill in 1.5 s, or an enemy within 3 m at half health | +15% melee; +10% swing; knockdown resistance x2; 8 health a kill; each kill frightens enemies within 6 m | block -20% |
| `dwarf` | Stand Fast | 120 s / 10 s | enemy within 20 m and (cavalry closing within 20 m, or 3 enemies within 5 m, or health half) | no crush-through against him; no flinch; 20% less damage; knockdown resistance x3; block +25%; cannot panic | -15% speed |
| `elf` | Swiftness of the Eldar | 120 s / 8 s | a target within 30 m with a bow, or an enemy within 6 m | +20% speed; +25% acceleration and draw; +15% reload; +10% arrow speed; aim error -30%; parry +20%; horse +10% | none |
| `orc` | Swarm | 80 s / 8 s | enemy within 4 m and 3 kin (orcs or goblins) within 6 m | +5% melee, +3% per kinsman within 6 m (up to 5); +10% swing | morale -8 when it ends; 3 s spent: -10% speed |
| `goblin` | Scurry | 80 s / 6 s | enemy within 8 m, or health half | +25% speed; +30% acceleration; +20% swing | 3 s spent: -15% speed |
| `uruk` | Iron Discipline | 120 s / 10 s | enemy within 5 m and (health 60%, or morale 60, or 3 enemies within 4 m) | cannot panic (morale held at 60); 15% less damage; block +15%; knock-back resistance x2 | -10% speed |
| `pale_uruk` | Hunter's Rush | 100 s / 6 s | an enemy at 4 to 15 m | +30% speed; +40% acceleration; knockdown resistance x2; +10% melee | 3 s spent: -15% speed |
| `dg_uruk` | Shadow of the Necromancer | 120 s / 8 s | 2 enemies within 6 m | enemies within 8 m lose 2 morale a second; +10% melee | none |

**Human cultures** (men only)

| Culture | Ability | Cooldown / length | Fires when | Effect | Price |
|---|---|---|---|---|---|
| `gondor`, `gondor_soldiers` | Guard of the Citadel | 120 s / 10 s | enemy within 15 m and (2 within 5 m, or cavalry closing, or health half) | block +30%; 10% less damage; knock-back resistance x2; cannot panic | -10% speed |
| `vlandia` (Rohan) | Forth Eorlingas | 120 s / 10 s | mounted, enemy within 30 m | horse +15%; +15% melee; +10% swing; dismount resistance x2.5; cannot panic | none |
| `sturgia` (Dale) | Bard's Aim | 120 s / 8 s | a target within 40 m with a bow | +20% draw; aim error -40%; +15% arrow speed; +10% ranged damage | none |
| `empire`, `dunland_raiders` (Dunland) | Hill-clan Fury | 80 s / 6 s | enemy within 3 m and (took damage, or kin fell within 8 m) | +15% melee; +10% swing; +10% speed | block -30%; 3 s spent: -10% speed |
| `aserai`, `harad_raiders`, `shaghana`, `abanissa` (Harad) | Serpent's Venom | 120 s / 8 s | a target within 35 m with a bow, or an enemy within 4 m | +20% ranged damage; aim error -15%; +10% melee | none |
| `khuzait`, `rhun_raiders` (Rhun, and Khand's soldiers, cavalry and chariots included) | Wainrider Wall | 120 s / 10 s | enemy within 15 m and (2 within 5 m, or cavalry closing) | block +20%; 10% less damage; knockdown resistance x2; +10% swing | none |
| `umbar`, `umbar_corsairs` | Corsair Raid | 80 s / 6 s | enemy within 4 m and (took damage, or a kill in 2 s) | +20% swing; +15% speed; +10% melee | 3 s spent: -10% speed |
| `battania` (Khand's lords, caravan masters and tavern mercenaries, and the Variag Ravagers) | Variag Ferocity | 100 s / 8 s | enemy within 6 m | +15% melee; +10% swing; knockdown resistance x2; dismount resistance x2 | none |
| `mordor`, `dolguldur` (their men) | Servants of the Shadow | 120 s / 8 s | enemy within 4 m and (a kill in 2 s, or health half) | each kill frightens enemies within 6 m; cannot panic; +10% melee | none |

Rally radii run 6 to 12 m. The initiator and every third joiner shout.

### MCM: `Battle Tactics/Race Abilities`

`Enable Race Abilities` (switching on takes effect from the next battle; switching off stops new abilities at once
and lets running ones finish), `War Cries`, `Show Ability Messages` (one line when five or more soldiers on one side
fire the same ability within two seconds; "your side" counts an allied lord's troops too), `Race Ability Debug Log`
(off: see below), `Ability Glow and Sparks` (on: the outlines and sparks above, drawn on this client only, so co-op
classifies it as presentation).

## Logging: is it working?

Everything goes to the TAOM log with the `[RaceAbilities]` tag.

| Line | When | Tells you |
|---|---|---|
| `mission gate: eligible=True MCM=True json=True combatType=Combat` | first tick of every mission | the feature is on for this battle (False in arenas, tournaments, conversations, or with either switch off) |
| `Attached ability trees to N soldier(s) at the first tick` | first tick | in Custom Battle this is usually 0: the armies spawn afterwards |
| `First late-spawn tree attached` | the first later spawn | trees are attaching |
| `<ability> by <name> (tier T, player side) at S s, trigger=<kind>, rallied=R` | the first 12 waves of a battle; every wave with the debug log on | an ability fired, why, and how many joined |
| `<ability> on <name>: Active -> Spent at S s` | every phase change, debug log on | windows end when they should |
| `Battle so far (S s):` then one line per ability | every 30 s of mission time with new activity | the running counters |
| `Mission end: N tree(s) late-attached, M soldier(s) tracked at end`, then `Mission end:` and the counters | mission end | the battle's totals |
| `RaceAbilitiesConfigProvider: ...` / `RaceAbilityProfileResolver: ...` | first use | a config value was reverted or is never read, or a race name matched nothing |
| `RaceAbilityTask threw ...` / `RaceAbilityDeaths threw ...` | once per battle | a decision or a death failed (that soldier fires nothing that pass) |
| `visuals Refresh threw ...` (or `Burst`, `Forget`) | once per battle | an outline or a burst failed; the abilities themselves are untouched |
| `visuals.burst '<name>' is not a particle effect the engine knows; no sparks` | the first burst | the configured effect does not exist, so no sparks this session |

Each counter line reads, per ability: `trees`, `decisions` (passes off cooldown that sensed), `waves`, `soldiers`
(activations, rallies included), `rallied`, `ended`, `kills while live` (active or spent), `extensions`,
`health healed`, `fear landed` (once per enemy per kill or aura pulse), `morale restored`, `crush forced`,
`blocks braced` (every block by a soldier holding against crush-throughs, whether or not the blow could crush),
`shrug-offs`, `melee hits boosted`, `ranged hits boosted`, `bonus damage`, `hits softened`, `damage prevented`,
`stat passes`, and `fired by` with a count per trigger kind. Many `decisions` and few `waves` means the triggers
are strict; `trees 0` means the race or culture has no soldiers here.

`taom.print_race_abilities` (dev console, cheats on) prints the same in game: the gate, who is live now (active and
spent per ability), how many soldiers are outlined, and the counters.

## Key Files

| File | Purpose |
|---|---|
| `Main/Features/RaceAbilities/RaceAbilitiesModule.cs` | Registration, the hooks' runtime, the mission logic |
| `Main/Features/RaceAbilities/Domain/RaceAbilitiesConfig.cs`, `RaceAbilityDefaults.cs` | Config shape and the compiled profiles |
| `Main/Features/RaceAbilities/Domain/RaceAbilitySenses.cs`, `RaceAbilityScanPlan.cs` | What a soldier perceives, and what the sensor still has to gather |
| `Main/Features/RaceAbilities/RaceAbilitiesConfigProvider.cs` | Load and validate, aliases, accepted-but-unread warnings |
| `Main/Features/RaceAbilities/RaceAbilityProfileResolver.cs` | Race and culture to profile; kin races |
| `Main/Features/RaceAbilities/RaceAbilityService.cs` | Every decision, pure: triggers, the scan plan, scaling, rally, kill credit, damage, crush, morale, the percentage arithmetic |
| `Main/Features/RaceAbilities/RaceAbilityStore.cs` | Live state per agent, identity-keyed, immutable states, transitions, live counts |
| `Main/Features/RaceAbilities/RaceAbilityTelemetry.cs` | The thread-safe counters behind the logs and the console command |
| `Main/Features/RaceAbilities/RaceAbilityFallenMemory.cs`, `RaceAbilityAuraLedger.cs`, `RaceAbilityReportClock.cs`, `RaceAbilityWaveCounter.cs` | Fallen kin, strongest aura per enemy, the 30 s report, the message throttle |
| `Main/Features/RaceAbilities/RaceAbilityGlowLedger.cs` | Who wears an outline: the nearest up to the cap, repainted each pass, cleared on dropping out |
| `Main/Features/RaceAbilities/RaceAbilitySettingsProvider.cs` | The MCM switches, cached and read through |
| `Main/Features/RaceAbilities/RaceAbilityBehaviorTree.cs`, `BehaviorTreeElements/RaceAbilityTask.cs` | The tree and its one decision |
| `Main/Features/RaceAbilities/Hooks/RaceAbilityRuntime.cs`, `RaceAbilitySensor.cs`, `RaceAbilityActivator.cs`, `RaceAbilityTicker.cs`, `RaceAbilityDeaths.cs`, `RaceAbilityVisuals.cs` | The engine boundary |
| `Main/Features/RaceAbilities/Hooks/RaceAbilitiesMissionLogic.cs` | Gate, attach, tick, deaths, mission-end report |
| `Main/Features/RaceAbilities/Hooks/RaceAbilityHooks.cs`, `RaceAbilityStatApplier.cs` | What the six models call |
| `Main/Features/RaceAbilities/Hooks/RaceAbilityNames.cs` | Display names and message lines |
| `Main/Features/RaceAbilities/Cheats/RaceAbilitiesCheats.cs` | `taom.print_race_abilities` |
| `Main/_Module/ModuleData/race_abilities/race_abilities.json` | The profiles |

## Dependencies

- `IRaceManager` (Core): race validity and ids.
- `CreatureTreeTracker`, `AgentSlotIdentity`, `IsAiAgentDecorator`, `ReturnTrueTask`, `MissionThreadGuard` (AdvancedCombat).
- `DeferredCallbackQueue` (BehaviorTreeWrapper): deaths off the main thread.
- `SignatureMissionGate` (SignatureStrikes): which missions carry battle abilities.
- `DreadAgentGate` (DreadAura): who fear can reach.
- `CreatureBanditsModule` (CreatureBandits) declares `TaomCustomBattleCreatureDamageModel` for Custom Battle, so the
  Custom Battle damage, crush and shrug-off hooks ride that module's declaration.

## Tests

All under `TAOM.Tests/Features/RaceAbilities/`:

- `RaceAbilitiesConfigProviderTests`: missing and broken files, every validation rule (one bad value for every
  numeric effect field, with a test that fails when a new field has none), trigger kinds as names only, aliases,
  kin fields, the outline colour and the `visuals` block, the accepted-but-unread warnings, the summary warning.
- `RaceAbilityProfileResolverTests`: invalid ids never reach a name lookup, race over culture, culture for men only,
  kin races.
- `RaceAbilityServiceTests`: every trigger kind, the scan plan (and that it never changes an answer for any
  shipped profile), cooldown and NaN, tier and kin scaling and caps, rally recruits, kill credit (no horses), the
  heal, cavalry closing, crush verdicts, damage (falls keep theirs), resistance, morale, auras, who glows, the
  percentage arithmetic.
- `RaceAbilityGlowLedgerTests`: the nearest up to the cap, one slot per soldier, repainting, clearing on dropping
  out, a cap of 0, bad distances, forgetting the dead.
- `RaceAbilityStoreTests`: phases and transitions, a stall past both ends, kill extension, identity keys, eviction,
  live counts.
- `RaceAbilityStatApplierTests` (`RequiresGame`): each effect's property, and the mount.
- `RaceAbilityTelemetryTests`, `RaceAbilityFallenMemoryTests`, `RaceAbilityAuraLedgerTests`,
  `RaceAbilityReportClockTests`, `RaceAbilityWaveCounterTests`: counters across threads and their labels, fallen
  kin, the aura ledger, the report clock, the message throttle.
- `RaceAbilitySettingsProviderTests`: the no-MCM fallbacks and reading through the cached settings
  (`HotPathSettingsProvidersTests` pins the cache itself).
- `RaceAbilityHooksTests`: with no runtime, every model hook hands its input back.
- `ShippedRaceAbilitiesConfigTests`: the shipped file loads clean, equals the compiled profiles, every ability has a
  display name, every culture key is a real culture id, the cooldowns stay one to two minutes and the outline palette
  is Mike's; `RaceAbilitiesLiveKeyTests` (`LiveInstall`): every race and kin race it names is registered by the
  installed game, and the spark effect is in a particle file the game loads (the ones `project.mbproj` registers).
- `RaceAbilitiesWiringTests`: every model call site, the module, the container (`RequiresGame`), the main-thread
  mark, the deaths' soldier flag, the mission-end clearing, the visuals' call sites (main-thread steps only, and no
  other file in the feature), one outline write site behind its guard, and a mission-end clear with no native call.
- `TAOM.Tests/BehaviorTreeWrapper/BehaviorTreeAgentComponentThreadingTests`: the slot check still comes before a
  tree runs, and the schedule copy allocates nothing.

## How to add a race or culture

1. Add a `races.<race>` or `cultures.<culture>` row to `race_abilities.json` and the same profile to
   `RaceAbilityDefaults`; `ShippedRaceAbilitiesConfigTests` fails until they agree. Do not profile a race that
   already carries its own behaviour tree (TAOM's `BehaviorTreeMissionLogic` keeps one per agent; the attach skips
   such agents).
2. Pick triggers from the kinds above; only the parameters a kind reads are validated.
3. Use only the effect fields in Engine levers. A new driven property needs its write site in both stat models
   checked first: it must be assigned on every update, or the post-pass compounds.
4. Give the ability a display name in `RaceAbilityNames` and run `/localize`.

## How to verify in game

Run `/armory-audit` first if the session banner reports Armory drift. Turn on `Race Ability Debug Log`, then
Custom Battle:

1. Isengard (pick `urukhai_berserker` or `urukhai_nazg_hai` for berserkers, plus Uruk-hai) against Erebor. Watch for
   blocks broken by berserkers and held by bracing dwarves, and a dwarf line bracing together. `crush forced` and
   `blocks braced` should both climb.
2. Rohan cavalry charging Gondor's line: `forth_eorlingas` and `citadel_guard` waves; the riders visibly faster.
3. An elven army against orcs: archers loosing faster as the orcs close; `swarm` firing in the orc mob.
4. Dol Guldur against anyone: `fear landed` under `necromancer_shadow`.
5. `taom.print_race_abilities` mid-battle; the mission-end summary in the log.
6. A 1,000-agent Custom Battle with the MCM switch on and off, comparing frame time.
7. One campaign field battle, to see the campaign models behave as Custom Battle's do; Rhun spearmen against a
   full-speed charge there shows the Branch A limit.
8. A Rohirrim unhorsed mid-ability: whether his horse keeps its boost (the UNVERIFIED row above).
9. Outlines and sparks: active soldiers wear their kind's colour and lose it when the ability ends or they fall,
   bodies carry none, sparks pop as abilities fire, `Ability Glow and Sparks` off clears every outline within half a
   second, and Hide Battle UI hides them. `taom.print_race_abilities` shows `outlined now`: a count above 0 with
   nothing on screen means the engine drew nothing. Watch the edge of a big lit crowd for outlines blinking as
   soldiers trade places around the 40th, and whether soldiers behind the camera take outlines from ones in view.
   Try `seeThrough` true once (restart after editing the JSON).
10. Step 6's frame-time comparison with the glow on and off, before choosing whether it stays on by default: MCM
    keeps a player's first saved value, so the default is changed only by renaming the setting.

### First in-game test (2026-10-04): a success, tuning later

Mike ran two Custom Battles on v2.0.34 (Isengard against Dunland, about 200 a side; Isengard, 261, against Erebor
and the Iron Hills, 200) and called the feature a success for now, to be tuned later. The dev install's build was
the tag plus the working tree's uncommitted edits (`TAOM.dll` stamp `+18a4e402...dirty`), none of them in Race
Abilities, so the abilities ran as tagged. From `taom_debug.log`:

- Every trigger fired as designed (`TookDamage`, `KinFell`, `WoundedEnemyWithin`, `HealthBelow`, `CavalryClosing`,
  `EnemiesWithin`), kin rallied (79 dwarves in one Stand Fast wave), the firing counts fit the cooldowns (no
  ability fired more often than the soldiers carrying it), and the feature logged no error; the spark effect
  resolved. The wave lines showed in the message log.
- The abilities moved each fight by a few percent: Isengard's added 705 damage, 4.1% of all the damage Dunland took,
  plus 54 health healed and 121 morale hits; Dunland's added 168, 1.2% of Isengard's; against the dwarves,
  Isengard's added 193 (5.8%) and Stand Fast prevented 254. The dwarves were winning that fight on armour, not
  Stand Fast: by the last sample their 180-strong infantry line had lost 18 and Isengard 112, and the Iron Hills
  elites took 713 damage from 227 hits.

Tuning notes, not yet acted on:

- **The outline in a packed line** showed mostly around legs and on a few raised arms, not around each body (step
  9). Whether it reads better on a soldier standing alone is the next look.
- **Hill-clan Fury** fired 195 times but boosted only 59 hits (0.3 a firing; Bloodlust 1.3, Berserk 1.5).
- **Balance:** the clean test is one matchup run three times with the MCM switch off and three times on.
- Not yet run: steps 2 to 4, 6 to 8 and 10, and the sparks in step 9.

### Bannerlord v1.5.4 (2026-10-05)

Checked when Steam moved the game to v1.5.4. Every managed engine member the feature calls, overrides or reads is
byte-identical to v1.5.3: `Agent`, `AgentDrivenProperties`, `Mission` and the mission behaviour callbacks, the stat,
damage and morale model bases with their Sandbox and Custom Battle subclasses, `MBAgentVisuals`, `SkinVoiceManager`
and `ParticleSystemManager` (decompile diff of the two builds). The strict binding gate passes 464 of 464 on v1.5.4.
On the native side, the code behind each bridge call it makes (`MakeVoice`, the contour calls, the particle lookup and
burst, the nearby-agent query) is instruction-identical as far as its direct calls reach; code behind virtual calls
was not compared. What v1.5.4 did change natively is the agent-visuals state
code that runs when a soldier is built, re-equips or dies, so the next look in game should confirm three things on
v1.5.4: an outline survives a weapon or banner re-equip; it clears on death and leaves nothing on the corpse; the war
cry and the sparks still play.

## Not yet

- **Custom war-cry audio, a lasting aura (embers for the whole window), a player "Unleash" order.** The aura was
  left out of the glow: an effect pinned to a bone needs its own handle, may need re-attaching after an equipment
  rebuild, and needs its bones checked on every race's skeleton.
- Trolls, the Nazgul, Sauron and Saruman keep their own systems (Brute Force, Signature Strikes, the Dread Aura).
- Calradia's bandit cultures and the minor peoples (`nord`, `vakken`, `darshi`, `neutral_culture`) have no profile;
  TAOM's raider cultures share their kingdom's ability through the aliases.

## Changelog

Dated, feature-sliced history (newest first). The commit bodies, which `/release` gathers into `CHANGELOG.md`, are
the chronological log of record.

- 2026-10-05: checked against Bannerlord v1.5.4: no code change needed; three in-game checks owed, listed under
  "Bannerlord v1.5.4" above.
- 2026-10-04: first in-game test on a v2.0.34 dev build (Mike): a success for now, tuning later; the findings are
  under "First in-game test" above.
- 2026-10-04: part of the v2.0.34 release (tag on `18a4e402`). The v2.0.33 tag carries the same code but was never
  packaged, because a testing build already carried that number.
- 2026-10-04: Khand's ability left as it is (Mike). The known limits now name everyone who carries Variag
  Ferocity (Khand's caravan masters and the Variag Ravagers added), correct Umbar (its own recruits, lords and
  armies fire Corsair Raid), and say what Wainrider Wall and Variag Ferocity do for a rider or charioteer; the knockdown,
  knock-back and dismount rows now cover a mounted soldier.
- 2026-10-04: outline glow and sparks (Mike's option C1 of three): fury, guard and dread abilities outline their
  soldier while active, the 40 nearest the camera, repainted every half second; sparks burst as an ability fires;
  MCM `Ability Glow and Sparks`, on.
- 2026-10-04: cooldowns lengthened to one to two minutes (Mike): his first numbers times four, so their order holds.
- 2026-10-04: round-2 review fixes. A horse's death no longer counts as a kill; falls keep their damage; the
  sensor gathers only what can change a decision; the tree's decorator and task are one node; the console command
  is `taom.print_race_abilities`; Stand Fast drops its unreachable knock-back resistance and goblins their unread
  kin races; telemetry labels say what they count; the documentation records the known limits.
- 2026-10-04: first version (#730): 18 abilities for nine races and nine cultures, one generic tree, telemetry.

## GitHub Issue

- **Issue:** #730, [Race Abilities: a battle ability per race and per culture](https://github.com/haterade22/TAOM/issues/730);
  translation backlog #731
- **Status:** Closed 2026-10-04, verified in two Custom Battles (a success for now, tuning later), with
  `triage-needs-ingame` and `triage-blocked-decision` for the remaining in-game steps and decisions listed above
