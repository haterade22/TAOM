# Combat Stamina

## Overview

Every soldier in a battle has a stamina pool. A melee swing, a shot, a throw, a jump, a kick, a shield bash
and a blow stopped with a weapon or shield cost stamina, and running on foot drains it down to half. Stamina
comes back while a soldier does not attack. A tired soldier swings and handles his weapon more slowly, and a
tired AI soldier attacks less often. A soldier whose stamina reaches zero is Exhausted: he cannot attack,
block, kick or jump, and on foot slows to a walk, until his stamina is back at about a third.

**Status (2026-11-19): built, proven in unattended matches, not yet played by a person.** The rules, the lock,
the player's stamina bar and the peoples table are in. Unattended Fords of War matches pass the three check
lines ("Unattended runs" under "How to verify in game"). What a person must play is still owed (the lock on
your own soldier, the bar's colours, riders), and the numbers are starting values. The MCM switch is off by
default.

## Why This Exists

- **Vanilla behavior:** a soldier can swing without limit. Skill sets the swing rate and nothing sets a cost.
- **TAOM requirement** (decisions of 2026-11-19 on #1497): every action costs stamina; stamina comes back with
  rest; better troops have more of it; a soldier with none must recover before he can fight; the same rules
  hold for the player and for AI soldiers; horses and other mounts are left alone; the player sees his own
  stamina on the battle HUD.
- **Without this feature:** fresh reserves have no edge over men who have fought for ten minutes, and the
  player can attack for a whole battle.

## The rules

### Bands

| Band | Starts (falling) | Ends (rising) |
|---|---|---|
| Fresh | | |
| Winded | below 60% | 65% |
| Tired | below 30% | 35% |
| Exhausted | at 0 | 30%, into Tired |

### Pool

`base + perTier x battle tier`, where the tier is `BasicCharacterObject.GetBattleTier()`: 0 to 7 by level, and
7 for every hero. A hero (a lord, a companion, the player) adds `heroBonus + heroPerLevel x level`, the level
counted up to `heroLevelCap`. With the shipped values a tier 0 recruit has 100, a tier 5 soldier 150, a hero
of level 0 has 200 and a level 25 lord 225. The pool is then multiplied by the soldier's people (below).

### Costs

| Action | Cost in points |
|---|---|
| Melee swing or thrust, hit or miss, paid at release | `clamp(4 + 2 x weapon kg, 5, 14)`: 6.4 for a 1.2 kg sword, 9 for a 2.5 kg two-hander |
| Bow shot, crossbow shot, throw | 4, 2, 5 (any other ranged release, a sling for example, costs as a bow shot) |
| A drawn bow held past 1.5 s (a player's soldier only) | 2 per second |
| Jump, kick, shield bash | 8, 6, 6 (a rider's jump and kick are free: the jump is the horse's) |
| A blow stopped with a weapon | 6 |
| A melee blow stopped with a shield | `3 + 0.05 x the damage the shield took`, at most 10 |
| A missile stopped with a shield | 1 |
| Running on foot | 2% of the pool per second, never below 50% of the pool |

Free: walking, riding, being hit (it costs health), a chamber block, a feint, a reload, and everything a
soldier does while he works a siege engine. The weapon weight is `ItemObject.Weight` of the wielded item, the
weight of one item; `MissionWeapon.GetWeight()` is the whole stack plus loaded ammunition. Every cost in
points is multiplied by the MCM cost scale and by the soldier's armour factor. The run drain is a share of the
pool, and neither of the two changes it.

### Regeneration

5% of the pool per second, times the soldier's people factor. It pauses for 1.5 s after the soldier's own
action (a swing, a shot, a throw, a jump, a kick, a bash, a held bow). It runs at half rate while he holds a
block, not at all while he runs, and at 1.5 times while he is Exhausted. A blow stopped or a hit taken does
**not** pause it. This is the rule the economy rests on: with a pause after every block, the opponent decides
the pauses and every soldier in contact reaches zero in about 30 s whatever he does. With the pause tied to
the soldier's own actions, a soldier who attacks less often recovers more, which is what makes AI caution work.

While Exhausted a soldier pays nothing and his pace does not count as running, so nothing can hold him at
zero: he is back at 30% in about 4 to 5.5 s.

### What a band does

| Band | Swing, thrust and draw speed | Handling | Move speed on foot | AI attack chance |
|---|---|---|---|---|
| Winded | x0.95 | | | x0.8 |
| Tired | x0.90 | x0.90 | x0.95 | x0.5 |
| Exhausted | x0.90 | x0.90 | x0.45 (the walk factor) | x0.5, and the lock |

### Peoples and armour

A soldier's race and culture multiply his pool and his regeneration. The two multipliers are multiplied and
kept between `multiplierMin` (0.7) and `multiplierMax` (1.6). An exempt people has no stamina at all.

| People | Pool | Regeneration | Pointer |
|---|---|---|---|
| `elf` | 1.3 | 1.4 | Legolas in the chase across Rohan (The Two Towers, III 2) |
| `dwarf` | 1.35 | 0.9 | Made to endure toil; Gimli keeps pace in the same chase |
| `uruk_hai`, `uruk`, `pale_uruk`, `dg_uruk` | 1.2 | | The Uruk-hai's run with their prisoners (The Two Towers, III 3) |
| `berserker` | 1.3 | 0.8 | Design choice |
| `orc` | 0.9 | | Design choice |
| `goblin` | 0.8 | 1.1 | Design choice |
| `cave_troll`, `hill_troll` | 1.5 | 0.7 | Design choice: great strength, slow to recover |
| `nazghul`, `sauron`, `saruman` | exempt | | Design choice: they do not tire |
| culture `gondor`, `arthedain`, `cardolan` | 1.1 | | The Dunedain realms; the long-lived Men of the West |

These rows are design choices. The pointers name the passage that suggested each one; none is a quotation,
because the research for #1497 could not open the reference pages. The data has no hobbit race. Rohan, Harad,
Rhun and the other peoples of Men have no row on purpose: no number is better than an invented one.

Armour: every cost is multiplied by `clamp(1 + (armour kg - 10) / 60, 1, 1.35)`, with the armour weight read
once from the soldier's spawn equipment. A soldier in 22 kg pays 1.2 times; the maximum is reached at 31 kg.

## Architecture

### Design Challenge

The engine has no combat stamina. A search of the v1.5.7 decompile and the Native, SandBox, SandBoxCore and
CustomBattle module data found only smithing stamina, a breath-hold model and cosmetic "tired" animations.
Four engine facts shape the design:

- No callback reports the start of a swing, a miss, a jump or running.
- The engine recomputes every driven property on many events (ammunition use, a weapon change, a mount, a
  formation order). A value written outside the stat model is lost at the next recompute.
- The player's input is rebuilt every frame in `MissionMainAgentController.ControlTick`, and the native tick
  reads it before the managed mission tick runs (`MissionState.TickMissionAux` calls `Mission.Tick` first).
- The AI input callback (`Agent.SetHasOnAiInputSetCallback`) is one flag per agent, and Marshal's Word
  already uses it.

### Solution Approach

**Detection.** Each frame the ticker reads every tracked soldier's two action indices
(`Agent.GetCurrentAction`, a pointer read). Only when an index changed does it ask the engine for the action
types, and it charges the action that began: `ReleaseMelee`, `ReleaseRanged`, `ReleaseThrowing`, `JumpStart`,
`Kick`, `WeaponBash`. A stopped blow comes from `MissionBehavior.OnMeleeHit` (`Blocked` or `Parried`, with
`AttackBlockedWithShield` and the shield's damage in `InflictedDamage`) and `OnMissileHit`. Those callbacks
only add the cost to the defender's cell with an atomic add. Running comes from `Agent.MovementVelocity` at
the soldier's look, every 0.25 s.

**Stats.** `StaminaHooks.ApplyStats` is one line in each of the two existing stat models, right after the
race abilities: `TaomAgentStatCalculateModel` (campaign) and `TaomCustomBattleAgentStatCalculateModel`.
There is no stamina stat model: the engine keeps one model per slot, and a second would replace the first.
The base models assign every property the pass touches afresh on each call, so a multiply never compounds.
`Agent.UpdateAgentProperties()` reruns the whole model, so the ticker calls it only when a soldier's band
changes, at most `maxRefreshesPerFrame` times per frame, the player's soldier first, and never for a soldier
younger than `minRefreshAgeSeconds` (a spawn wave may still be in progress).

**The Exhausted lock.** One pure function, `StaminaInputRules`, takes the attack and defend bits out of the
movement flags and the kick and jump out of the event flags. A rider keeps the jump (it is the horse's) and a
swimmer keeps it (vanilla adds the jump while its key is held in water), for a player's soldier and an AI
soldier alike. Two paths call it, chosen at each look from the soldier's controller at that moment, because
Seat of Seeing, Long Defeat and the Fords control hand a hero to the AI and back during a battle:

- **A player's soldier:** Patch171, a postfix on `MissionMainAgentController.ControlTick`, the last writer in
  the call that builds the input. TAOM's own `AutonomousMovementPlayerController` replaces the vanilla
  controller during a creature ride, so it makes the same call at the end of its own `ControlTick`.
- **An AI soldier:** `StaminaInputComponent`, an `AgentComponent`, strips in `OnAIInputSet`. On every look at
  a soldier on this path the ticker makes sure the component is his last one, because the engine calls
  components in the order they were added and Marshal's Word's hold adds an attack input.

A soldier who uses a game object (he works a siege engine, or raises a siege ladder) is never locked. A
soldier who climbs a ladder is most likely not in that state (climbing goes through the ladder queue), so he
is locked as on the ground and still climbs; vanilla's own ladder code already takes attack and block from a
climber (`ClimbingMachineDetachment.cs:243`).

The strip also clears `MovementControlFlag.DefendBlock` (0x8000). The engine's `DefendMask` (0x7C00) holds the
four directions and `DefendAuto` but not that bit. No managed v1.5.7 code sets it; whether the native AI does
is not known.

**Known behaviour, not yet measured.** Clearing the attack bits on a soldier who holds a ready swing or a
drawn bow releases it: Marshal's Word's release is built on exactly that. So a soldier who becomes Exhausted
in the middle of an attack makes that one attack, and cannot cancel it, because the defend bits are the cancel
input and they are cleared too. Spikes S1 and S2 record what happens.

**Mission end.** `StaminaTicker.Teardown` only drops stamina's claims on the AI input flag. It reads no agent
and calls no engine method: before a behavior is removed the engine has cleared every agent's native pointers
(`Mission.EndMissionInternal` calls `Agent.Clear`), so even `Agent.IsActive` would fault there.

**The shared AI input flag.** `AiInputCallbackClaims` is the only code that writes the flag. Marshal's Word
and stamina each claim and release it under an owner bit, and the flag is on while either holds a claim.
A released `MarshalWordComponent` answers nothing (`_on`), because native keeps calling every component on
the agent while stamina alone holds a claim.

**An attack that a behaviour tree starts.** A dwarf's head-butt and drop-kick (Dwarf Attacks) and a troll's
brute-force smash (Troll Brute Force) start with `SetActionChannel` from a tree, past the AI's input, so the
strip cannot stop them. Each ready decorator asks `StaminaHooks.RefusesSpecialAttack` first and starts nothing
for an Exhausted soldier. The engine reads the two dwarf attacks as kicks (`actt_kick` in the Armory's
`action_types.xml`), so each costs the kick's 6 points. The troll's smash has no action type and costs nothing.
A new feature that starts an attack this way must ask the same hook. An attack that is already under way
when the soldier becomes Exhausted is not cancelled. Not covered: a couched lance or a braced spear that is
already held (the engine reads the stance, not an input), and a mount's own attack, which stamina leaves
alone with the mount.

**The stamina bar.** `BattleHudStaminaMapper` turns the share and the band into a height and a colour. The
battle HUD mixin reads the player's current soldier through `StaminaHooks.TryRead` each refresh (a soldier
with no stamina, of an exempt people or under the MCM opt-out, gets no bar) and the
injected prefab draws a thin upright bar left of the rings: pale gold when Fresh, amber when Winded, orange
when Tired, grey while Exhausted, where it rises as the pool comes back. The player also gets one message the
first time he becomes Tired in a battle and one each time he becomes Exhausted.

**Rejected:** `AgentFlag.CanAttack` and `CanDefend` as the lock (the vanilla ladder code and Marshal's Word
already write them); `Agent.SetMaximumSpeedLimit` for walk-only (the formation code rewrites it for every AI
soldier about every 0.5 s, `HumanAIComponent.cs:662`); clearing the player's input from the mission tick (the
engine has already read it); a pause in regeneration after a block or a hit taken (see "Regeneration"); a
fourth ring in the HUD widget (it changes the ring geometry and its tests for no gain over a bar).

### Component Diagram

```
configs/stamina.json                 TaomSettings (the MCM group Stamina)
        |                                        |
StaminaConfigProvider                   StaminaSettingsProvider
        |                                        |
StaminaTuning (effects, economy, peoples) --- StaminaRuntime (store, refresh queue, counters)
                                                 |
                              StaminaMissionLogic -> StaminaTicker (mission tick, main thread)
                                                 |
  pure rules: StaminaService, StaminaLockRules, StaminaInputRules, StaminaRefreshQueue, StaminaStatApplier
                                                 |
 StaminaHooks.ApplyStats        StaminaHooks.StripPlayerInput      StaminaInputComponent     StaminaHooks.TryRead
 (two stat models, any thread)  (Patch171, own controller)         (OnAIInputSet, any thread) (the HUD mixin)
```

### Threads

The mission tick is the only writer of a cell. Engine callbacks only queue a new soldier, mark one removed, or
add a stopped blow's cost with an atomic add, which is safe from any thread. The stat models read one value,
the band, published with `Volatile`; the AI input component reads three volatile flags the tick sets. The
player strip runs in the pre-mission tick, on the main thread, before the mission tick of the same frame. The
feature takes no lock. State is keyed on the `Agent` object, never on `Agent.Index`.

Which thread the engine calls `OnAIInputSet` on is not known: Marshal's Word's notes disagree with each other,
and no native decompile settles it. The component is written for any thread, and `taom.print_stamina` prints
how many AI strips ran off the main thread, so spike S2 answers the question. The ticker removes and adds the
component from the mission tick. The managed agent tick cannot overlap that (the next pre-tick waits for it),
but a native call of `OnAIInputSet` during it is the one thread risk that is not ruled out. Marshal's Word
already adds its component from the tick.

### Scope

- **Missions:** a Combat mission that is a field battle, a siege or a sally-out, plus the Fords of War match
  (built with no team AI; it is the unattended test battle). Arenas, tournaments, hideouts, towns and
  conversations get no stamina. The rules run only while the mission is in Battle mode, so nothing drains
  during deployment.
- **Who:** human agents. Horses and creature mounts get no stamina. A rider's arm slows and locks as on foot;
  his speed values are not touched. Crew archers on mumakil and howdahs are human agents.
- **Co-op:** `StaminaMissionLogic` is `AgentOwner`. The ticker asks `CoopMissionOwnership.IsOwnedHere` for each
  soldier before it acts on him. A soldier another peer owns stays Fresh here. Nothing is synced, so both
  peers need the same `EnableStamina`, `StaminaCostScale`, `StaminaApplyToPlayer` and `StaminaApplyToAi`; the
  settings fingerprint covers all four.
- **Auto-resolve** does not see stamina.

## Configuration

### Config File: `Main/_Module/ModuleData/configs/stamina.json`

Loaded once per process: an edit needs a game restart. A missing value takes its compiled default. A value
out of range, or not a number, takes its default with a warning in the log, and so does a name the economy
sections do not know (a misspelled key).

| Section | Fields |
|---|---|
| `pool` | `base`, `perTier`, `heroBonus`, `heroPerLevel`, `heroLevelCap` |
| `costs` | `swingBase`, `swingPerKg`, `swingMin`, `swingMax`, `bowShot`, `crossbowShot`, `throw`, `jump`, `kick`, `bash`, `bowHoldPerSecond`, `bowHoldGraceSeconds`, `parry`, `shieldBase`, `shieldPerDamage`, `shieldMax`, `missileBlock` |
| `run` | `drainPerSecond` and `floor` (shares of the pool), `speedThreshold` (m/s) |
| `regen` | `perSecond` (share of the pool), `pauseSeconds`, `blocking`, `exhausted` (multipliers) |
| `bands` | `windedBelow`, `tiredBelow`, `hysteresis`, `recoverAt` (shares of the pool) |
| `armour` | `freeKg`, `kgPerStep`, `factorMax` |
| `peoples` | `races` and `cultures`: a name with `pool`, `regen` or `exempt`; `multiplierMin`, `multiplierMax` |
| `effects.<band>` | `swing`, `handling`, `move`, `aiAttack` for `winded`, `tired`, `exhausted` |
| `tick` | `sweepSeconds`, `maxRefreshesPerFrame`, `minRefreshAgeSeconds` |

Values that must keep an order go back to their defaults together when the order is broken: an effect may not
be milder in a more tired band, `swingMin` may not pass `swingMax`, `shieldBase` may not pass `shieldMax`, and
the band edges must leave a recovered soldier inside Tired (`recoverAt` at or below `tiredBelow + hysteresis`).
A race name that the game does not know is named in one log warning at the first battle.

### Current Values

The shipped file holds the compiled defaults of `StaminaEconomy`, `StaminaTuning` and `StaminaPeoples`
(`StaminaConfigProviderTests` and `StaminaPeoplesTests` compare them), and the values are the ones in "The
rules" above. Sweep 0.25 s, 8 recomputes per frame, minimum age 1 s.

### MCM: `Battle Balance / Stamina`

| Setting | Default | Read |
|---|---|---|
| Enable Combat Stamina (`EnableStamina`) | off | when a battle's gate is decided |
| Stamina Applies To You (`StaminaApplyToPlayer`) | on | once per battle tick. Off: the soldier you control has no stamina, is never locked, and the bar is hidden |
| Stamina Applies To AI Soldiers (`StaminaApplyToAi`) | on | once per battle tick. Off: no AI soldier has stamina, ally or enemy |
| Stamina Cost Scale (`StaminaCostScale`, 0.5 to 2) | 1.0 | once per battle tick; on every cost of every soldier |
| Show Stamina Bar (`ShowStaminaBar`) | on | by the HUD, about twice a second; this client only |

The two "Applies To" switches are independent, as the rule for every feature that changes how the player
fights requires (`docs/reviews/lessons/gamemodels-services.md`). With both on, the player and the AI follow the
same rules (Mike's decision 2). A soldier that a switch exempts in the middle of a battle goes back to Fresh
and unlocked at the next sweep, and keeps the stamina he had if the switch comes back on. The blows he
stopped while exempt are dropped, not charged later. "Your soldier" is the main agent or a soldier a player
controls. Seat of Seeing hands the hero to the AI and he stays the main agent, so he stays yours. The Fords
control clears the main agent when it gives a soldier back, so that soldier follows the AI switch again.

Decide the release default of `EnableStamina` before the first release that carries the property: MCM keeps a
saved value over a later default (`python tools/check_mcm_default_flips.py`).

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/Stamina/StaminaModule.cs` | Feature module: services, the mission logic, the patch category |
| `Main/Features/Stamina/StaminaService.cs` | The rules: pool, profile, costs, the drain and regeneration step, the bands |
| `Main/Features/Stamina/Domain/StaminaEconomy.cs`, `StaminaPeoples.cs`, `StaminaTuning.cs` | The numbers, immutable once loaded |
| `Main/Features/Stamina/Domain/StaminaLockRules.cs`, `StaminaInputRules.cs` | The lock path and what the strip removes |
| `Main/Features/Stamina/Domain/StaminaRefreshQueue.cs` | Which soldiers get their stats recomputed this frame |
| `Main/Features/Stamina/Domain/StaminaCell.cs` | One soldier's state, with its thread contract |
| `Main/Features/Stamina/StaminaStore.cs` | The cells, keyed by the soldier's object |
| `Main/Features/Stamina/StaminaConfigProvider.cs` | Validating loader for `stamina.json` |
| `Main/Features/Stamina/StaminaChecks.cs`, `StaminaTelemetry.cs` | The in-game check lines and the battle's counters |
| `Main/Features/Stamina/Hooks/StaminaHooks.cs` | The three seams other code calls |
| `Main/Features/Stamina/Hooks/StaminaStatApplier.cs` | The band's multipliers on the driven properties |
| `Main/Features/Stamina/Hooks/StaminaTicker.cs` | The mission tick: engine reads and writes only |
| `Main/Features/Stamina/Hooks/StaminaInputComponent.cs` | The AI half of the lock |
| `Main/Features/Stamina/Hooks/StaminaMissionLogic.cs`, `StaminaMissionGate.cs` | Mission entry point and its gate |
| `Main/Features/Stamina/Cheats/StaminaCheats.cs` | `taom.set_stamina`, `taom.print_stamina` |
| `Main/Features/BattleHud/UI/BattleHudStaminaMapper.cs` | The bar's height and colour |
| `Client/Features/BattleHud/UI/MissionAgentStatusBattleHudMixin.cs`, `Client/Features/CareerSystem/UI/CareerEnergyBarPrefab.cs` | The bar's values and its markup |
| `Client/Features/Stamina/Hooks/Patch171_StaminaPlayerInput.cs` | The player half of the lock |
| `Main/Adapters/AiInputCallbackClaims.cs`, `Main/Core/Collections/OwnerClaimLedger.cs` | The shared AI input flag |
| `tools/ingame_checks/1497.toml` | The manifest of the check lines |

## Dependencies

- `CoopMissionOwnership` (CoopInterop): who owns a soldier in a co-op battle.
- `AgentSlotIdentity`, `MissionThreadGuard` (AdvancedCombat): a live agent handle, and the thread tripwire.
- `FordsOfWarMission` (FordsOfWar): whether the mission is the Fords match.
- `IRaceManager` (Core): a soldier's race name, with the id checked before the lookup.
- `IOutcomeLog` (Core): the check lines.
- Marshal's Word shares `AiInputCallbackClaims`.

## Tests

`TAOM.Tests/Features/Stamina/`:

- `StaminaServiceTests`: the pool, every cost, the action classifier, the step (pause, blocking, running and
  its floor, the Exhausted rules, values that are not numbers), every band edge, the atomic cost add.
- `StaminaEconomySimulationTests`: a simulated fight on the shipped numbers. A tier 3 soldier in a one-on-one
  fight is not Exhausted in 180 s; against two he is, between 30 and 150 s; without the AI caution he is in a
  one-on-one fight too; a hero who swings once a second empties his pool in 25 to 40 s and one who swings
  every 3 s never does. **The fight tempos are assumed** (one swing every 2 s, one incoming blow every 2 s per
  opponent, 60% parried): spike S4 measures them, and the constants at the top of the file take the result.
- `StaminaPeoplesTests`: the profile (race, culture, the limits, exempt peoples), the armour factor, hero
  level, the `peoples` and `armour` sections, and the shipped names against the mod's race list and id sheet.
- `StaminaConfigProviderTests`: missing and broken files, every range and order rule, unknown names, the
  shipped file against the compiled defaults.
- `StaminaChecksAndSettingsTests`: every pass, fail and skip branch of the three checks; the cost scale.
- `StaminaInputRulesTests`, `StaminaLockRulesTests`, `StaminaStoreAndQueueTests`, `StaminaStatApplierTests`,
  `StaminaGateAndCheatTests`: the strip, the lock paths, the store and the refresh budget, the stat pass, the
  gate and the console words.
- `StaminaWiringTests`: both stat models call the hook, the module and its patch category, the ownership
  check before any write, the poll's order of reads, the single writer of the shared flag, the engine-free
  teardown, both player controllers, the hit callbacks.

`TAOM.Tests/Features/BattleHud/BattleHudStaminaMapperTests.cs`: the bar. `TAOM.Tests/Core/Collections/OwnerClaimLedgerTests.cs`:
the claim rules. The Marshal's Word pins for the shared flag are in `MarshalMarkCaptureAndShotTableTests`.

## How to verify in game

**Preconditions:** MCM `Battle Balance / Stamina / Enable Combat Stamina` on, cheat mode on. A Custom Battle
(field battle) reaches every step; no campaign is needed. **No step that needs a person has been run**; step
11 has, see "Unattended runs" below. Each result goes on #1497.

The log proves three things by itself (`python tools/ingame_verdict.py`, manifest `tools/ingame_checks/1497.toml`):
`[Check] id=taom.stamina.armed`, `taom.stamina.lock` (an Exhausted soldier recovered and none stayed locked
past 15 s) and `taom.stamina.refresh` (no batch of stat recomputes over 1 ms). Every 30 s the log has a
`[Stamina][Pulse]` line and at the end one `[Stamina][Summary]`, whose last line starts `peoples:` and gives
per race name the fighters, the soldiers ever Exhausted and the exhaustions: the race rows of the peoples table
are tuned from it (a culture row is not separated). A band that `taom.set_stamina` forced and a
soldier an MCM switch exempted are outside the lock counters: only an exhaustion and a recovery that the rules
made count, and a soldier that `auto` gives back while he is Exhausted gets his 15 s from that moment.

1. Start the battle and run `taom.print_stamina`. The first line must read `mission gate: open`, and
   `tracked=` must be near the number of soldiers on the field.
2. **A plain fight.** Fight on foot with the bar visible. The bar must fall with each swing and each blow you
   stop, change colour at 60% and 30%, and rise when you stop attacking. Swing without pause: you must become
   Exhausted in about half a minute, get the message, lose attack, block, jump and kick, walk slowly, and be
   back in about 5 s.
3. **S1, the player strip.** Run `taom.set_stamina exhausted player`. Attack, block, jump and kick must do
   nothing, and walking must still work. Do it once while holding a ready swing and once with a drawn bow, and
   note whether that one attack is released. Repeat on a horse (the horse must still jump) and on a warg.
   `taom.set_stamina auto player` must give every action back.
4. **S2, the AI strip.** Run `taom.set_stamina exhausted enemies`. For 30 s no enemy may attack or block.
   `taom.print_stamina` must show `lockedAI=` equal to the enemies alive and a rising `AI` strip count. Note
   the count `off the main thread` beside it: it tells which thread the engine calls the AI input callback
   on. Watch whether any exhausted enemy still raises a block (the `DefendBlock` question). Then give a
   company of archers a Marshal's Word hold and exhaust them: the hold must not make them draw.
   `taom.set_stamina auto enemies` must restore them, and after it Marshal's Word must still work.
5. **S3, the walk factor.** With `exhausted allies`, the allies must move at about a walking pace and keep
   their formation. Note whether the formation as a whole slows down, and read a running soldier's speed
   against the 3 m/s run threshold.
6. **S4, the poll and the tempo.** In a duel, compare `actions=` and its count per kind with what you did (a
   jump, kick or bash counted twice means the poll saw it on both action channels in different frames), and
   note swings,
   stopped blows and hits per engaged soldier per minute: they replace the assumed tempos in
   `StaminaEconomySimulationTests`.
7. **S5, the rider.** Exhaust a mounted soldier: the horse's speed must not change. Note what a rider's kick
   key does in vanilla; the strip removes the kick for a rider too.
8. **S6, the cost.** Read `us each on average` and `slowest batch` in `taom.print_stamina` in a battle of 500
   against 500, and compare `[MissionPerf]` with the switch off.
9. **S7, AI caution.** Compare how often `tired` enemies attack with `fresh` ones over one minute, melee and
   archers. The design needs at least 30% fewer attacks.
10. **Fresh against tired.** 50 against 50 for two minutes, then `taom.spawn_troops` the same troop for one
    side. Do it once with the switch on and once with it off: the fresh side's margin must be clearly larger
    with it on.
11. **The unattended run.** `/game-run` (the Fords match) with the switch on: the three check lines, no
    `[ERROR]`, and in `[Stamina][Summary]` fewer than half of `fighters=` in `exhaustedSoldiers=`. This run
    covers AI soldiers only: the Fords control hands the hero to the AI. Add `--control-check 240` and
    `--screenshots` so the battle HUD and the bar show.

### Unattended runs, 2026-11-19

Fords of War matches with nobody at the keyboard (`/game-run`), 540 live seconds each, both sides under AI
commanders. The switch was turned on for a run by one added key in the MCM settings file, restored after it.
Each value below was read from the run's copied TAOM log.

| Run folder (`E:/Temp/fords-runs/`) | Build | Setup | Stamina checks | Swallowed exceptions | Fighters, of them ever Exhausted | Exhaustions, recoveries, stuck |
|---|---|---|---|---|---|---|
| `20261119-200716` | 303cccfdb | on, 1x, soldier control | 3 pass | 178 (the bar, fixed in 2e4e407c4) | 811, 144 | 529, 502, 0 |
| `20261119-201805` | 303cccfdb | off, 1x, soldier control | armed: skip, `toggle-off` | 0 | no stamina | no stamina |
| `20261119-210511` | 2e4e407c4 | on, 1x, soldier control | 3 pass | 0 | 863, 70 | 181, 153, 0 |
| `20261119-211835` | 2e4e407c4 | on, 4x | 3 pass | 0 | 796, 86 | 219, 186, 0 |
| `20261119-212159` | 2e4e407c4 | on, 1x, Rivendell against Mordor, soldier control | 3 pass | 0 | 649, 139 | 454, 397, 0 |

`python tools/ingame_verdict.py --issue 1497` reads all three checks PASS on the session of `20261119-210511`.
The fighters and exhausted counts are from the last 30 s pulse line, the other three numbers from the lock
check line, which is written a few seconds later.

What the runs settle:

- **The unattended criterion (step 11).** Between 8% and 21% of the soldiers who paid stamina were ever
  Exhausted in the four runs above, and at most 29% in the batch below: in every run fewer than half. Nobody
  stayed Exhausted past 15 s in any run.
- **S2, the thread.** Every AI input strip ran on the main thread, in all ten runs with the switch on (262,602
  in the first run, 0 off it). This is
  the Fords mission, which has no team AI; a field battle with team AI is not measured.
- **S6, the cost.** A stat recompute took 19 to 28 microseconds on average and the slowest batch of one frame
  0.43 to 0.55 ms, against the 1 ms limit of `taom.stamina.refresh`. The frame time was the same with the switch
  on and off (8.58 ms against 8.56 ms on average in the two 1x runs of one build); every run sat at the frame
  cap, so this bounds the cost only from above.
- **S4, in part.** At 4x the poll counted 12.1 actions per fighter and at 1x 12.2, so the per-frame poll shows
  no loss at speed. No AI soldier jumped, and kicks and bashes together were 0 to 14 in a match, so whether such
  an action is counted twice is still a person's check. The exception is Isengard against Erebor with 512 and
  651 kicks: they are the dwarves' head-butts and drop-kicks (`taom.dwarf-attacks.*` counted 511 attempts in
  the first of the two matches). So a dwarf pays the kick cost, 6 points, for each of them. In these runs the
  Exhausted lock did not reach them, because Dwarf Attacks starts the action itself (read from the code, not
  seen in a log); the gate described under "An attack that a behaviour tree starts" came after them. Whether these attacks should
  cost stamina, and what a troll's smash should cost, is Mike's decision.
- **The logs.** On the fixed build, the three runs with the switch on hold the same one `[ERROR]` line as the
  run with it off, and no `[WARNING]` that names stamina. The warnings differ only in two Fords lines that
  depend on the match: a tower warning of the Mordor map, and a "move target is off the navmesh" line that the
  run with the switch off has and two of the others do not.
- **The bar.** A screenshot inside a soldier hold (`hud_250_0.png` of `20261119-210511`) shows the bar left of
  the rings, pale gold and full. No run showed it change colour: the held soldier does not fight.

A HUD change needs `--control-check 240` in the run, or the match never shows the battle HUD. The first run
found the one defect of the night that way: the bar's height was bound to an int, and Gauntlet writes the
widget's float back (`GauntletFloatWriteBackGateTests`, the RCA addendum).

**A balance batch**, 12 matches at 4x on build 2e4e407c4 (`E:/Temp/fords-runs-stamina/`), every one a launcher
PASS with no swallowed exception. Deaths West and East after 530 live seconds:

| Matchup (West against East) | Switch off, two matches | Switch on, two matches | Ever Exhausted, of fighters (on) |
|---|---|---|---|
| Gondor against Rohan | 363 and 378; 344 and 306 | 384 and 355; 348 and 403 | 74 of 852; 89 of 887 |
| Rivendell against Mordor | 78 and 406; 42 and 488 | 66 and 246; 76 and 646 | 73 of 391; 156 of 822 |
| Isengard against Erebor | 425 and 295; 405 and 269 | 327 and 262; 340 and 271 | 203 of 709; 199 of 738 |

Two matches a cell is a small sample, and two matches of one cell differ by more than the cells do in the first
two rows, so those rows show no effect of the switch. Isengard against Erebor, the matchup with the most melee
(13,647 and 13,816 swings), has the highest share of exhausted soldiers (27% and 29%) and is the one row where
both matches with the switch on had fewer deaths than both with it off (589 and 611 against 720 and 674), most
of the difference on the Isengard side. That is a lead for tuning, not a result.

**The build with the gate and the per-people counters** (77d051805), 23 more matches at 4x, 540 live seconds each
(`E:/Temp/fords-runs-stamina2/` to `-stamina5/`): 12 with the switch on (Isengard against Erebor, Rivendell against
Mordor, ten of Erebor against Gundabad) and 11 of Erebor against Gundabad with it off.

- **The gate.** In each of the nine passing matches that had dwarves, an Exhausted soldier failed between 193 and 678
  ready checks of a special attack (`special attack checks refused`), with no swallowed exception. Rivendell
  against Mordor counted 0: its four hill trolls were never Exhausted.
- **Per people**, summed over the ten passing matches with the switch on (the `peoples:` line):

| People (pool, regeneration in `stamina.json`) | Fighters | Ever Exhausted | Exhaustions |
|---|---|---|---|
| `pale_uruk` (1.2, 1) | 4,752 | 1,146 (24%) | 3,794 |
| `uruk_hai` (1.2, 1) | 463 | 103 (22%) | 274 |
| `orc` (0.9, 1) | 585 | 102 (17%) | 259 |
| `dwarf` (1.35, 0.9) | 3,291 | 437 (13%) | 829 |
| `elf` (1.3, 1.4) | 98 | 10 (10%) | 15 |
| `berserker` (1.3, 0.8) | 10 | 7 | 9 |
| `uruk` (1.2, 1) | 12 | 1 | 1 |
| `hill_troll` (1.5, 0.7) | 31 | 0 | 0 |

  These are Fords matches of one faction against one other, so a people's share also shows how hard its side
  fought. A lead for tuning, not a result: the pool multiplier alone does not order the peoples (the Orcs with
  the smallest pool are in the middle), a troll never tires (its smash costs nothing), and the two large Uruk
  peoples with the same row (`pale_uruk`, `uruk_hai`) differ little.

- **Two native failures, in Erebor against Gundabad with the switch on.** One match hung at mission second 201
  and one crashed at 542. With the switch on that matchup failed 2 times in 10, with it off 0 times in 11. That
  split can be chance (about one in five by Fisher's exact test), so the runs neither clear stamina nor accuse
  it. The hang has a confirmed cause that is not stamina code: the dwarves' two attack actions are typed
  `actt_kick` and their clips have no combat parameter, so the engine's kick code reads the record before its
  table (#1499). The crash is a strike by a dwarf with empty hands: the engine reads a null weapon record at
  `TaleWorlds.Native.dll+0x5B5F5D`. By the decompiled engine code it is a passive-usage strike (a braced
  weapon), and what emptied the hands is not known. Until #1499 is settled, a battle with dwarves can end in the
  hang, with the switch on or off by its mechanism; the crash cannot be placed until its trigger is known.

Still owed, all by a person: the lock on your own soldier (S1), the walk and the run threshold (S3), riders
(S5), how much less a tired AI attacks (S7), fresh against tired (step 10), Marshal's Word beside stamina, and
the bar as you fight: that it falls, changes colour and rises again.

## How to tune

1. Edit `Main/_Module/ModuleData/configs/stamina.json`.
2. Restart the game: the file is read once per process.
3. Read the TAOM log for a `StaminaConfigProvider` warning. A rejected value fell back to its default.
4. If too many soldiers tire out (`exhaustedSoldiers` against `fighters` in the summary), the levers are, in
   order: `effects.tired.aiAttack` (more caution), `regen.perSecond`, then the costs. If too few do, the same
   levers the other way.
5. When the measured tempos differ from the assumed ones, change the constants in
   `StaminaEconomySimulationTests` first and tune until it passes again.

## Performance

Measured on 2026-11-19 in Fords matches with 399 to 502 agents at their peak: 19 to 28 microseconds for a stat recompute, at
most 0.55 ms for one frame's batch, and no change in the frame time with the switch on ("Unattended runs").
Estimated for a larger battle, not measured: at 1,000 soldiers and 60 frames per second the poll is 2,000
pointer reads per frame, the sweep looks at about 67 soldiers per frame, and a band change costs one stat
recompute, at most 8 per frame. `taom.print_stamina` and `taom.stamina.refresh` report the measured cost of the
recomputes. The tick allocates nothing per frame.

## Changelog

- 2026-11-19: the first unattended game runs. The bar's height became a float (the widget's write-back threw on
  an int); the measured results are under "Unattended runs".
- 2026-11-19: Phases 1 and 2. The drain and regeneration rules, the lock wired to them, the player's stamina
  bar and messages, the cost scale, the check lines, the peoples table, the armour factor, hero pools by level.
- 2026-11-19: Phase 0. The effect side, the Exhausted lock on both input paths, the shared AI input flag,
  two console commands.

## GitHub Issue

- **Issue:** #1497: [Combat stamina: every action costs stamina, and an exhausted agent must recover](https://github.com/haterade22/TAOM/issues/1497)
- **Status:** Open
