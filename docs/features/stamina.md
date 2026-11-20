# Combat Stamina

## Overview

Every soldier in a battle gets a stamina band: Fresh, Winded, Tired or Exhausted. A tired soldier swings and
handles his weapon more slowly, and a tired AI soldier decides to attack less often. An Exhausted soldier
cannot attack, block, run or jump until he recovers, and can only walk.

**Status (2026-11-19): Phase 0 of #1497.** The effect side is built: the bands change a soldier's stats, and
the Exhausted lock strips his input. Nothing drains stamina in a fight yet. A band comes only from the
developer console command `taom.set_stamina`, so that the engine questions below can be measured before
the drain rules are written. The MCM switch is off by default.

## Why This Exists

- **Vanilla behavior:** a soldier can swing without limit. Skill sets the swing rate and nothing sets a cost.
- **TAOM requirement:** every action costs stamina, stamina comes back with rest, better troops have more of
  it, and a soldier with none must recover before he can fight (decisions of 2026-11-19 on #1497).
- **Without this feature:** fresh reserves have no edge over men who have fought for ten minutes, and the
  player can attack for a whole battle.

## Architecture

### Design Challenge

The engine has no combat stamina. A search of the v1.5.7 decompile and the Native, SandBox, SandBoxCore and
CustomBattle module data found only smithing stamina, a breath-hold model and cosmetic "tired" animations.
Three engine facts shape the design:

- The engine recomputes every driven property on many events (ammunition use, a weapon change, a mount, a
  formation order). A value written outside the stat model is lost at the next recompute.
- The player's input is rebuilt every frame in `MissionMainAgentController.ControlTick`, and the native tick
  reads it before the managed mission tick runs (`MissionState.TickMissionAux` calls `Mission.Tick` first).
- The AI input callback (`Agent.SetHasOnAiInputSetCallback`) is one flag per agent, and Marshal's Word
  already uses it.

### Solution Approach

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
soldier alike. Two paths call it, chosen each sweep from the
soldier's controller at that moment, because Seat of Seeing, Long Defeat and the Fords control hand a hero to
the AI and back during a battle:

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

**Rejected:** `AgentFlag.CanAttack` and `CanDefend` as the lock (the vanilla ladder code and Marshal's Word
already write them); `Agent.SetMaximumSpeedLimit` for walk-only (the formation code rewrites it for every AI
soldier about every 0.5 s, `HumanAIComponent.cs:662`); clearing the player's input from the mission tick (the
engine has already read it).

### Component Diagram

```
configs/stamina.json        TaomSettings.EnableStamina
        |                            |
StaminaConfigProvider        StaminaSettingsProvider
        |                            |
   StaminaTuning ----------- StaminaRuntime (store, refresh queue, counters)
                                     |
                      StaminaMissionLogic -> StaminaTicker (mission tick, main thread)
                                     |
   pure rules: StaminaLockRules, StaminaInputRules, StaminaRefreshQueue, StaminaStatApplier
                                     |
     StaminaHooks.ApplyStats           StaminaHooks.StripPlayerInput     StaminaInputComponent
     (two stat models, any thread)     (Patch171, own controller)        (OnAIInputSet, any thread)
```

### Threads

The mission tick is the only writer of a cell. Engine callbacks only queue a new soldier or mark one removed,
which is safe from any thread. The stat models read one value, the band, published with `Volatile`; the AI
input component reads three volatile flags the tick sets. The player strip runs in the pre-mission tick, on the
main thread, before the mission tick of the same frame. The feature takes no lock. State is keyed on the
`Agent` object, never on `Agent.Index`.

Which thread the engine calls `OnAIInputSet` on is not known: Marshal's Word's notes disagree with each other,
and no native decompile settles it. The component is written for any thread, and `taom.print_stamina` prints
how many AI strips ran off the main thread, so spike S2 answers the question. The ticker removes and adds the
component from the mission tick. The managed agent tick cannot overlap that (the next pre-tick waits for it),
but a native call of `OnAIInputSet` during it is the one thread risk that is not ruled out. Marshal's Word
already adds its component from the tick.

### Scope

- **Missions:** a Combat mission that is a field battle, a siege or a sally-out, plus the Fords of War match
  (built with no team AI; it is the unattended test battle). Arenas, tournaments, hideouts, towns and
  conversations get no stamina.
- **Who:** human agents. Horses and creature mounts get no stamina. A rider's arm slows and locks as on foot;
  his speed values are not touched. Crew archers on mumakil and howdahs are human agents.
- **Co-op:** `StaminaMissionLogic` is `AgentOwner`. The ticker asks `CoopMissionOwnership.IsOwnedHere` for each
  soldier before it acts on him. A soldier another peer owns stays Fresh here. Nothing is synced.

## Configuration

### Config File: `Main/_Module/ModuleData/configs/stamina.json`

Loaded once per process: an edit needs a game restart. A missing value takes its compiled default. A value
out of range, or not a number, takes its default with a warning in the log.

| Field | Type | Description |
|-------|------|-------------|
| `effects.<band>.swing` | float 0.5 to 1 | On `SwingSpeedMultiplier` and `ThrustOrRangedReadySpeedMultiplier` |
| `effects.<band>.handling` | float 0.5 to 1 | On `HandlingMultiplier` |
| `effects.<band>.move` | float 0.1 to 1 | On `MaxSpeedMultiplier` and `CombatMaxSpeedMultiplier`. For `exhausted` this is the walk factor. Never applied to a rider |
| `effects.<band>.aiAttack` | float 0.05 to 1 | On `AIAttackOnDecideChance`, kept at or above the engine's 0.05 floor |
| `tick.sweepSeconds` | float 0.1 to 1 | Seconds between two looks at the same soldier |
| `tick.maxRefreshesPerFrame` | int 1 to 64 | The most stat recomputes in one frame |
| `tick.minRefreshAgeSeconds` | float 0 to 5 | A younger soldier is not recomputed yet |

`<band>` is `winded`, `tired` or `exhausted`. A band may not be milder than the band before it: a multiplier
that rises from winded to tired to exhausted goes back to its defaults in all three bands.

### Current Values

| Band | swing | handling | move | aiAttack |
|---|---|---|---|---|
| winded | 0.95 | 1.0 | 1.0 | 0.8 |
| tired | 0.9 | 0.9 | 0.95 | 0.5 |
| exhausted | 0.9 | 0.9 | 0.45 | 0.5 |

Sweep 0.25 s, 8 recomputes per frame, minimum age 1 s. These are starting values. Spike S3 sets the walk
factor and spike S7 checks that the attack chance changes what the AI does.

### MCM

`Battle Balance / Stamina`: **Enable Combat Stamina** (`EnableStamina`), default off, read when a battle's gate
is decided. Decide the release default before the first release that carries the property: MCM keeps a saved
value over a later default (`python tools/check_mcm_default_flips.py`).

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/Stamina/StaminaModule.cs` | Feature module: services, the mission logic, the patch category |
| `Main/Features/Stamina/Domain/StaminaLockRules.cs` | Which lock path a soldier is on, and what a path change needs |
| `Main/Features/Stamina/Domain/StaminaInputRules.cs` | What an Exhausted soldier's input loses |
| `Main/Features/Stamina/Domain/StaminaRefreshQueue.cs` | Which soldiers get their stats recomputed this frame |
| `Main/Features/Stamina/Domain/StaminaCell.cs` | One soldier's state, with its thread contract |
| `Main/Features/Stamina/StaminaStore.cs` | The cells, keyed by the soldier's object |
| `Main/Features/Stamina/StaminaConfigProvider.cs` | Validating loader for `stamina.json` |
| `Main/Features/Stamina/Hooks/StaminaHooks.cs` | The two seams other code calls |
| `Main/Features/Stamina/Hooks/StaminaStatApplier.cs` | The band's multipliers on the driven properties |
| `Main/Features/Stamina/Hooks/StaminaTicker.cs` | The mission tick: engine reads and writes only |
| `Main/Features/Stamina/Hooks/StaminaInputComponent.cs` | The AI half of the lock |
| `Main/Features/Stamina/Hooks/StaminaMissionLogic.cs`, `StaminaMissionGate.cs` | Mission entry point and its gate |
| `Main/Features/Stamina/Cheats/StaminaCheats.cs` | `taom.set_stamina`, `taom.print_stamina` |
| `Client/Features/Stamina/Hooks/Patch171_StaminaPlayerInput.cs` | The player half of the lock |
| `Main/Adapters/AiInputCallbackClaims.cs`, `Main/Core/Collections/OwnerClaimLedger.cs` | The shared AI input flag |

## Dependencies

- `CoopMissionOwnership` (CoopInterop): who owns a soldier in a co-op battle.
- `AgentSlotIdentity`, `MissionThreadGuard` (AdvancedCombat): a live agent handle, and the thread tripwire.
- `FordsOfWarMission` (FordsOfWar): whether the mission is the Fords match.
- Marshal's Word shares `AiInputCallbackClaims`.

## Tests

`TAOM.Tests/Features/Stamina/`:

- `StaminaInputRulesTests`: what the strip removes on foot, mounted and in water; the masks against the engine enums.
- `StaminaLockRulesTests`: the path for each controller and state; every path change.
- `StaminaStoreAndQueueTests`: admission, removal, reference keys; the refresh budget, order and minimum age.
- `StaminaStatApplierTests`: each multiplier on its own property; a rider; the attack chance floor; values that are not numbers.
- `StaminaConfigProviderTests`: missing and broken files, every range, the band order rule, the shipped file, the MCM switch.
- `StaminaGateAndCheatTests`: the mission gate; the console words.
- `StaminaWiringTests`: both stat models call the hook, the module and its patch category, the ownership check before any write, the single writer of the shared flag, both player controllers.

`TAOM.Tests/Core/Collections/OwnerClaimLedgerTests.cs`: the claim rules. The Marshal's Word pins for the shared
flag are in `MarshalMarkCaptureAndShotTableTests`.

## How to verify in game

**Preconditions:** MCM `Battle Balance / Stamina / Enable Combat Stamina` on, cheat mode on, a Custom Battle
(field battle). No campaign is needed. None of these steps has been run yet: they are the Phase 0 spikes of
#1497, and each result goes on the issue.

1. Start the battle and run `taom.print_stamina`. The first line must read `mission gate: open`, and
   `Soldiers tracked` must be near the number of soldiers on the field. The TAOM log has one line
   `[Stamina] mission gate: open (...)`.
2. **S1, the player strip.** Run `taom.set_stamina exhausted player`. Attack, block, jump and kick must do
   nothing, and walking must still work. Do it once while holding a ready swing and once with a drawn bow, and
   note whether that one attack is released. `taom.print_stamina` must show `Locked now: player input 1` and a
   rising `player` strip count. Repeat on a horse (the horse must still jump) and on a warg. Then
   `taom.set_stamina auto player` must give every action back.
3. **S2, the AI strip.** Run `taom.set_stamina exhausted enemies`. For 30 s no enemy may attack or block.
   `taom.print_stamina` must show `AI input` locks equal to the enemies alive and a rising `AI` strip count.
   Note the count `off the main thread` beside it: it tells which thread the engine calls the AI input
   callback on. Watch whether any exhausted enemy still raises a block (the `DefendBlock` question).
   Then give a company of archers a Marshal's Word hold and exhaust them: the hold must not make them draw.
   `taom.set_stamina auto enemies` must restore them, and after it Marshal's Word must still work.
4. **S3, the walk factor.** With `exhausted allies`, the allies must move at about a walking pace and keep
   their formation. Note whether the formation as a whole slows down.
5. **S5, the rider.** Exhaust a mounted soldier: the horse's speed must not change. Note what a rider's kick
   key does in vanilla; the strip removes the kick for a rider too.
6. **S6, the cost.** Read `us each on average` and `slowest batch` in `taom.print_stamina` after
   `taom.set_stamina tired all` in a battle of 500 against 500, and compare `[MissionPerf]` with the switch off.
7. **S7, AI caution.** Compare how often `tired` enemies attack with `fresh` ones over one minute.
8. Leave the battle. The log has one `[Stamina] Mission end:` line and no `[ERROR]`.

## How to tune a band

1. Edit `Main/_Module/ModuleData/configs/stamina.json`.
2. Restart the game: the file is read once per process.
3. Read the TAOM log for a `StaminaConfigProvider` warning. A rejected value fell back to its default.

## Performance

Estimated, not yet measured (spike S6): at 1,000 soldiers and 60 frames per second the sweep looks at about
67 soldiers per frame, and a band change costs one stat recompute, at most 8 per frame. `taom.print_stamina`
reports the measured cost of the recomputes. The tick allocates nothing per frame.

## Changelog

- 2026-11-19: Phase 0. The effect side, the Exhausted lock on both input paths, the shared AI input flag,
  two console commands. No drain yet.

## GitHub Issue

- **Issue:** #1497: [Combat stamina: every action costs stamina, and an exhausted agent must recover](https://github.com/haterade22/TAOM/issues/1497)
- **Status:** Open
