# Dwarf Attacks

## Overview

AI dwarves on foot have two attacks of their own besides their weapons: a head butt on an enemy right in front of
them and a drop kick on an enemy a few steps away. Either attack starts the cooldown of both: 3 minutes of battle time
before the next head butt, 5 before the next drop kick (the clock stops while the game is paused). The head butt
staggers the victim;
the drop kick knocks the victim down unless a shield blocks it. Both hit exactly one enemy.

## Why This Exists

- **Vanilla behavior:** every humanoid fights with the same weapon swings and the same kick, and dwarves play the
  human clips on their own skeleton.
- **TAOM requirement:** dwarves should fight like dwarves. Mike asked for a drop kick and a head butt "to be used in
  game as an attack like the kick" (#1087).
- **Without this feature:** the two clips exist in the Armory but nothing plays them, because the engine's melee AI
  never starts an action it does not know.

## Architecture

### Design Challenge

The engine's AI picks its own attacks from the weapon and the kick action; it has no hook for a new attack type. The
clips are bespoke and live in the unversioned `LOTRLOME_Armory`, so a reinstall can drop them without a build error.
The drop kick moves the dwarf about 2 m through its `DisplacementUsage`, so the hit has to be judged where the dwarf
is at the impact, not where the attack started.

### Solution Approach

The troll Brute Force pattern ([troll-brute-force.md](troll-brute-force.md)): a per-agent behaviour tree layers the
attacks on top of the engine's melee AI. Unlike the troll's, the dwarf tree is not an agent component.
`BehaviorTreeMissionLogic` keeps one tree per agent, and every dwarf soldier already carries the race-ability tree
(Stand Fast, [race-abilities.md](race-abilities.md)). A component took that slot in the first draft, and the dwarf
lost Stand Fast. So `Hooks/DwarfTreeHost` holds the dwarf trees and runs them every mission tick on the main thread,
with the component's own gates: the agent is still its slot's occupant and active, and in co-op only the owner runs it.

The attacks run in every mission except the training yard and a tournament match (`SignatureMissionGate.IsCombatOutsideArena`,
the rule the race abilities and signature strikes follow): the arena's practice mission and a tournament match get no tree.
The yard and the tournament are known by their mission behaviours, because `Mission.CombatType` reads `Combat` in every
mission (#1537). For the same reason that value does not exclude a town or conversation scene, and an arena quest duel,
which carries neither behaviour, stays open.

1. `DwarfAttackReadyDecorator` reads the engine: the dwarf is AI controlled and not the player's own, on foot, not on
   a machine, not already attacking, struck or falling, and at least one attack has its action and is off cooldown.
   Both cooldowns count from `DwarfAttackService.LatestFired`, the later of the two attack stamps.
   It finds the nearest enemy on foot within 2.6 m and within 1 m of the dwarf's height, and asks
   `DwarfAttackService.ChooseAttack` with that enemy's distance and facing. Up to 1.3 m picks the
   head butt; 1.8 to 2.6 m picks the drop kick; nothing fires between the bands, so a drop kick never starts without
   room for its run-up.
2. `DwarfAttackTask` plays the chosen action on channel 0 with the troll smash's start flags, stamps that attack's
   cooldown and keeps the facing the attack started with. When the clip's progress reaches the impact fraction, it
   calls `DwarfAttackHit.Deliver`.
3. `DwarfAttackHit` picks one victim with the war ram's `SingleVictimPick` (the enemy faced most squarely, the nearer
   one breaking a tie) among the enemies on foot within the attack's reach of the dwarf's position at the impact and
   within 1 m of his height, and deals Blunt knock-back damage through `CustomAttacksUtils.TakeDamage`. That
   primitive writes the damage directly, so armour does not reduce it. A raised shield halves the damage and stops
   the knockdown only when its bearer faces the dwarf.

Riders are never attacked and never attack: neither clip can reach a rider, and a mounted dwarf has no clip for them.
The player's own dwarf never fires either attack, also when TAOM hands him to the AI (the Battle Command seat, Fords
of War, the Long Defeat): the decorator refuses `IsMainAgent` and `IsPlayerTroop`.

The engine's nearby query is flat (it takes a `Vec2` centre), so the height test is the feature's own. There is no
line-of-sight test: an enemy at the same height behind a thin wall inside 1.6 m can still be hit, as with the troll
ring and the war ram.

The drop kick's force knocks down any victim, a troll and a dwarf under Stand Fast included: `TakeDamage` sets
`BlowFlags.KnockDown` itself, and the engine decides knockdown resistance only for blows it builds from a collision.
The troll ring and the war ram work the same way.

The actions carry no type, as the troll's smash carries none. Until 2026-11-22 they were typed `actt_kick`, the vanilla
kick's type, and that hung battles (#1499): while an agent's own kick is in progress, the engine's kick code (the
function at RVA 0x670270 of the v1.5.7 `TaleWorlds.Native.dll`) reads the combat parameter id of the clip of a
kick-typed action and indexes the combat parameter table with it, with no test for "none". A dwarf still has the
vanilla kick, so both can meet. The dwarf clips have no combat parameter, because this feature
deals the blow, so the read took the record before the table, a hit bone index came from foreign bytes, and a worker
thread waited on a lock that nothing frees. The type's one managed reader,
`StandingPoint.DoesActionTypeStopUsingGameObject`, made a kicking agent let go of a machine. The ready decorator
refuses a dwarf who is on a machine when the attack starts; a dwarf who is given a machine during the one to two
seconds of the clip now keeps it (not seen in play). `tools/tests/test_armory_kick_typed_actions.py` fails when an Armory
action joins the kick family. Five mount attacks keep the type (spider, elephant, war ram, elk, moose): a mount has no
`CanKick` flag, and the engine starts an agent's kick only with it, so a mount's own channel does not reach that read.

### Component Diagram

```
LOTRLOME_Armory: action_types.xml, as_dwarf_warrior, dwarf_*_anm.tpac
        |  (drift guard at mission start)
DwarfAttacksMissionBehavior ── Hooks/DwarfTreeHost ── one tree per Monster "dwarf", run every tick
        |                       (no agent component: the race-ability tree keeps that slot)
DwarfAttacksBehaviorTree
   IsAiAgentDecorator ── DwarfAttackReadyDecorator ── DwarfAttackTask ── Hooks/DwarfAttackHit
                                 \                       |                 |
                         DwarfAttackService (pure)   DwarfAttackChecks   CustomAttacksUtils.TakeDamage
                         DwarfAttacksSettingsProvider (MCM)
```

## Configuration

Compiled constants in `Main/Features/DwarfAttacks/DwarfAttacksConfig.cs`; first guesses, to be tuned in the Custom
Battle smoke. Two MCM settings under **Battle Tactics/Dwarf Attacks**, both read live:

| Setting | Default | Effect |
|---|---|---|
| Enable Dwarf Attacks | on | Off, no new attack starts; a running one finishes |
| Dwarf Attack Damage (%) | 100 | 0 to 300. At 0 the attacks play but hurt and floor no one |

### Current Values

| Constant | Head butt | Drop kick | Note |
|---|---|---|---|
| Trigger distance to the nearest enemy | 0 to 1.3 m | 1.8 to 2.6 m | In front: cosine above 0.7 |
| Cooldown (mission seconds) | 180 | 300 | Counted from the last attack of either kind |
| Impact (clip progress) | 12/35, frame 13 | 20/53, frame 21 | From the authored clips |
| Reach at impact | 1.5 m | 1.6 m | In front of the start facing: cosine above 0.5 |
| Damage | 18 | 25 | Half against a shield |
| Knockdown | never | unless shield-blocked | |
| Blow magnitude | 25 | 45 | Knock-back impulse |

The drop kick clip carries the dwarf 2.0 m by progress 31/53, so about 1.3 m by its impact; `DwarfAttacksConfigTests`
checks that the travel plus the reach covers the far edge of the trigger band.

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/DwarfAttacks/DwarfAttackService.cs` | Pure decisions: who, which attack, cooldowns, impact, reach, the blow |
| `Main/Features/DwarfAttacks/DwarfAttacksConfig.cs` | Tuning and the action names |
| `Main/Features/DwarfAttacks/DwarfAttacksSettingsProvider.cs` | The two MCM settings, the damage clamped |
| `Main/Features/DwarfAttacks/DwarfAttackChecks.cs` | `[Check]` lines and hit counters for the in-game verdict |
| `Main/Features/DwarfAttacks/DwarfAttacksBehaviorTree.cs` | The per-dwarf tree |
| `Main/Features/DwarfAttacks/BehaviorTreeElements/` | Ready decorator, attack task, blackboard |
| `Main/Features/DwarfAttacks/Hooks/DwarfAttackHit.cs` | One victim, one blow |
| `Main/Features/DwarfAttacks/DwarfAttacksMissionBehavior.cs` | Mission gate, builds the trees, the drift guard |
| `Main/Features/DwarfAttacks/Hooks/DwarfTreeHost.cs` | Holds and runs the dwarf trees outside the agent's tree slot |
| `tools/gen_dwarf_attack_clips.ps1` | Writes and verifies the two `_anm.tpac` clips |
| `tools/bind_dwarf_attack_actions.py` | Declares and binds the actions in the Armory and the snapshot |
| `tools/blender/author_dwarf_attack_clips.py` | Keys both clips on the dwarf rig in Blender |
| `docs/reference/lotrlome-dwarf-attack-changes.md` | Ledger of every Armory change, with rollback |
| `tools/ingame_checks/1087.toml` | In-game check manifest |

## Dependencies

- `IsAiAgentDecorator`, `AgentSlotIdentity`, `CustomAttacksUtils` (AdvancedCombat)
- `CoopMissionOwnership` (CoopInterop): the owner gate `DwarfTreeHost` applies
- `SingleVictimPick` (ElephantLike): the war ram's single-target rule
- `IOutcomeLog` (Core/Logging)
- `StaminaHooks.RefusesSpecialAttack` (Stamina): with [Combat Stamina](stamina.md) on, an Exhausted dwarf starts
  neither attack. An attack costs no stamina since the actions lost their kick type (#1499), like the troll's smash

## Co-op

`DwarfAttacksMissionBehavior` is `AgentOwner`: `DwarfTreeHost.RunAll` skips a tree whose agent this peer does not
own, the same `CoopMissionOwnership.IsOwnedHere` check `BehaviorTreeAgentComponent.TickOnMissionThread` makes
([coop-interop.md](coop-interop.md)). A dedicated server adds no TAOM mission behavior, and it stages no Armory clips
([coop-dedicated-server.md](coop-dedicated-server.md)): its boot logs one `Could not find animation` warning per new
row, two more for this feature. A peer that does not own a dwarf probably does not see his two attacks since the
actions lost their type (#1499): BannerlordCoop publishes an action change only for a typed action or a ballista or
mangonel action (`IsDiscreteAction` in `source/Missions/Agents/Handlers/AgentActionHandler.cs`, read at Coop commit
e9906952a), and these two are neither. The blow is the owner's and is not affected. The troll's smash has the same
gap. Not checked in a co-op session, and not checked against the installed Coop DLL.

## Tests

- `TAOM.Tests/Features/DwarfAttacks/DwarfAttackServiceTests.cs`: Monster match, cooldowns, attack choice by band,
  facing and cooldown, the shared stamp (`LatestFired`), impact, reach, the blow, NaN and infinity for every float gate.
- `DwarfAttacksConfigTests.cs`: impact fractions match the authored frames, bands ordered, reach covers each band,
  cooldowns outlast the clips, cooldowns are 3 and 5 minutes.
- `DwarfAttacksSettingsProviderTests.cs`: defaults, the switch, the damage clamp, the MCM group order.
- `DwarfAttackChecksTests.cs`: one `[Check]` line per kind, every hit counted.
- `DwarfAttacksWiringTests.cs`: both actions declared once with no type and bound once in `as_dwarf_warrior`, in the
  snapshot and (LiveInstall) the live Armory; the feature registered and added to missions; no file of the feature
  takes the agent's tree slot; the ready decorator refuses the player's own dwarf.
- `tools/tests/test_gen_dwarf_attack_clips.py`, `tools/tests/test_bind_dwarf_attack_actions.py`: the two tools.
- Shared gates the feature joined: `HotPathSettingsProvidersTests`, `CreatureTreeClockTests`,
  `BehaviorTreeMissionLogicInheritanceTests`, `CoopMissionRolePolicyTests`.

## How to verify in game

**Preconditions:** a build with this feature deployed, the live Armory carrying the clips and the bindings
(`python tools/bind_dwarf_attack_actions.py --check` exits 0), Enable Dwarf Attacks on.

1. Start a Custom Battle with a dwarf culture's infantry against any infantry. The TAOM log shows
   `[DwarfAttacks] mission gate: eligible=True` and `[DwarfAttacks] Built behavior trees for N dwarf(s)` with N above
   0, and no `[DwarfAttacks] ... act_none` or `has no clip` line. No "Error adding a tree" message shows on screen, and
   the mission-end `[RaceAbilities]` report still counts `stand_fast` trees.
2. Let the lines meet. The log shows `[Check] id=taom.dwarf-attacks.head-butt result=pass` and
   `[Check] id=taom.dwarf-attacks.drop-kick result=pass`, each with the victim's distance, the clip progress and how
   far the dwarf travelled. A `result=fail` line means the attack never showed and the dwarf's action set has no clip
   for it; an attack replaced before it showed while the clip exists counts as cut short. Run
   `python tools/ingame_verdict.py --issue 1087` after the session.
3. On the drop kick's pass line, `travelled` well above 0 shows the clip's displacement moved the agent. Near 0, the
   kick reaches only enemies within 1.6 m of where the dwarf stood.
4. A person checks what a log cannot show: the head butt keeps the feet planted, the drop kick leaves the ground and
   lands without the feet sliding, and female dwarves still animate.

## How to Tune an Attack

1. Change the constant in `DwarfAttacksConfig.cs`; `DwarfAttacksConfigTests` keeps the bands and the reach consistent.
2. A changed clip needs its impact frame re-measured, `tools/gen_dwarf_attack_clips.ps1 -Apply` on a fresh copy, a Kit
   save, and the new fraction here and in the config.
3. Rebuild; nothing in the Armory changes for a C# tuning.

## Performance

Dwarves come by the hundred, so an idle dwarf scans at most about three to four times a second, over a 2.6 m radius
(`GetNearbyEnemyAgents` stays far below the 15 m point past which the native query scans every agent). Each dwarf's
scan period is 250 to 349 ms, set by his build order (`DwarfAttackService.IdleScanPeriod`), so the scans of a battle
spread over the frames instead of landing in one. A hit is a
counter increment; the `[Check]` line is written once per attack kind per process.

## Changelog

- 2026-11-06: first version. Clips keyed in Blender on the dwarf rig and imported in the Kit; actions declared and
  bound in the Armory; the C# tree, service, settings and in-game checks.
- 2026-11-06, deep review ([RCA](../reviews/rca-dwarf-attacks-2026-11-06.md)): the trees no longer take the agent's
  tree slot (dwarves had lost Stand Fast); the player's own dwarf is excluded after an AI hand-over; a height limit;
  the shield counts only toward the dwarf; an attack with a missing action never starts; combat missions only.
- 2026-11-06: the shield rule moved to `TAOM.Core.Combat.ShieldCover`, shared with the troll ring, the elephant-like attacks and the signature strike rings; the dwarves' behaviour is unchanged.
- 2026-11-13: the cooldowns went from 12 s (head butt) and 20 s (drop kick) to 50 s and 60 s, Mike's call. Only the two constants changed. An interrupted attack still spends its whole cooldown, so a cut-short attack now costs a dwarf 50 or 60 s.
- 2026-11-20: the cooldowns went to 180 s (head butt) and 300 s (drop kick), and either attack now starts both, Mike's call. Dwarves attacked too often, and each drop kick carried a dwarf 2 m out of his formation line. A dwarf can still attack once at first contact, because a dwarf that never attacked is off cooldown. An attack cut short by a hit reaction still starts both cooldowns. The MCM hint still says "each on its own cooldown": changing the English text would make all 12 translations stale, so it waits for the next translation pass.
- 2026-11-22: the two actions lost their `actt_kick` type (#1499). The engine's kick code read the combat parameter of
  a kick-typed action's clip with no test for "none", these clips have none, and a battle hung. The Armory rows are
  rewritten by `tools/bind_dwarf_attack_actions.py --apply`. A dwarf attack costs no stamina now, and a co-op peer
  that does not own the dwarf probably does not see the attack. Proof, 2026-11-22: 13 unattended Fords of War
  matches of Erebor against Gundabad with the new rows and Combat Stamina on (540 live seconds at 4x, runs
  `E:/Temp/fords-runs/20261122-012800` to `20261122-020917`, build 4892be754), none with a stall or a crash; the same
  matchup had failed 2 times in 10 with the switch on. Each match passed both attack checks with 141 to 205 hits, and
  stamina counted 8 to 23 kicks in a match, where the two dwarf matches of 2026-11-19 had 512 and 651: the engine no
  longer reads the attacks as kicks. 13 clean matches leave about a 1 in 18 chance that a 2 in 10 fault stayed hidden.

## GitHub Issue

- **Issue:** #1087
- **Status:** Closed (in-game check owed: `triage-needs-ingame`)
