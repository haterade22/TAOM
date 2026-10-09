# Creature Siege Role

## Overview

In a wall battle, trolls (`cave_troll`, `hill_troll`) never use a siege ladder, siege tower, ram, gate
lever, stone pile or siege engine. Attacking trolls go to the castle gate and break it, their blows
multiplied, then break the inner gate and hold the courtyard. Defending trolls under an AI general hold
the ground behind the gate instead of standing on the walls. Humans keep every machine.

## Why This Exists

- **Vanilla behavior:** the siege AI hands ladders, towers and machine standing points to whoever is
  closest. A troll joins a ladder queue it can never climb, stands at a tower it cannot ride, or takes a
  ram slot. Defending trolls are sent up to wall tops.
- **TAOM requirement:** trolls are siege beasts in the lore. They should batter the gate, not wait at
  the foot of a ladder.
- **Without this feature:** trolls block ladder queues for the humans behind them, idle for the whole
  assault, and a troll-heavy Mordor army cannot take a castle without humans to climb.

[Siege Forces](siege-forces.md) (#734) is the companion feature: it lets the player leave trolls out of
a wall battle altogether, and starts them unticked. Both key on the one race set,
`OversizedCreatureRaces`.

## Behavioural reference: TOR_Core

TOR_Core (The Old Realms, GPL-3.0) solves the same problem for its monsters. Its siege logic was read at
commit `5ccd0b91` (its SubModule.xml declares Bannerlord v1.3.15) for the ladder and standing-point vetoes, the gate damage
multiplier, gate targeting and parking. TAOM reproduces the behaviour with its own structure: GameModel
seams instead of six Harmony patches, per-agent navmesh exclusion, per-agent routing, and no copied code,
constants or identifiers. The gate multiplier is TAOM's own provisional value, not TOR_Core's. The
source was not re-read during implementation. Derivation and licence status:
[provenance register, TOR_Core row](../reference/provenance-register.md#tor_core-pending-license).

## Architecture

### Design Challenge

- **The ladder queue is not a climb gate.** `LadderQueueManager` recruits any agent with no scripted
  flags whose current path crosses its managed face (v1.5.4 `LadderQueueManager` :348, :434). Vetoing
  the queue does not stop a released or player-ordered troll from pathing up the ladder. The only real
  barrier is to keep the troll's path planner off the ladder and tower faces.
- **Navmesh exclusion has a hard budget.** Native keeps one exclusion set per exact ordered id list and
  marks each face with one byte. Bit 0 is the engine's no-exclusion bit (the re-mark at `0x401B20`
  starts from index 1) and bit 7 is also the scene's base navmesh boundary, so bits 1 to 6 give 6 sets
  that are clean for certain. Whether the registry's entry 0 is pre-seeded is unverified, so 6 is safe
  either way. N ids added in one fixed order make N shared prefix sets; removing an id would register new
  ones. Hence: at most 6 ids per scene, always in one order, never removed.
- **The detachment cost is read on the async AI thread.** The engine asks
  `GetDetachmentCostMultiplierOfAgent` from `DetachmentManager.TickAgent`, off the main thread, so
  whatever answers it must not lock, allocate or call native code.
- **TAOM scenes move gate anchors.** Several scenes put the outer gate's `middle_pos` between the gates,
  119 m inside (mordor_town_minas), or hide a second inner gate (gondor_castle_001 to 003, Helm's Deep),
  and vanilla takes the first tagged gate whether hidden or not.
- **Scripted movement fights vanilla.** Formation transfers, attack-entity disbands, ladder-queue drops
  and task-force disbands all clear an agent's scripted flags.

### Solution Approach

No Harmony patch. Two GameModel seams, one `MissionLogic`, two adapters:

| Seam | Where | What |
|---|---|---|
| Detachment cost | `GetDetachmentCostMultiplierOfAgent` in `TaomAgentStatCalculateModel` (campaign) and `TaomCustomBattleAgentStatCalculateModel` (Custom Battle) | `+Infinity` for a creature of the active wall battle. The engine starts its selection at `float.MaxValue` and keeps a candidate only on a strict greater-than, so `+Infinity`, and the NaN of `0 x +Infinity`, always lose. `float.MaxValue` would not: the product stays under it for any distance under 1 m. Closes every standing point on both sides, deployment auto-assign included |
| Gate damage | `ApplyDamageScaling` in `TaomCombatMechanicsModel` (campaign) and `TaomCustomBattleDamageModel` (Custom Battle, CombatMechanics), after `base` | A creature's melee blow on one of the battle's gates is multiplied. Not for friendly fire, a mount's blow or a missile; only for a finite positive damage, and only when the product stays finite |
| Routing | `CreatureSiegeRoleMissionBehavior` -> `CreatureSiegeRoleService` | Activation at `AfterStart`; a reconcile every 0.5 s in `OnMissionTick` once deployment is over |

Each model body is one line delegating to `CreatureSiegeHooks` (gamemodels.md rule 4). Both hooks read one
immutable, volatile `CreatureSiegeSnapshot` (mission token, race mask, multiplier, gate components). With
no snapshot published, which is every non-siege mission, a hook hands its input back after one read.

**Activation** (once per mission, whole body in a try, #699). Active only when all hold:

1. `Mission.IsSiegeBattle`;
2. no `SallyOutMissionController` (excludes sally-outs and relief forces);
3. not a network client or replay;
4. the MCM switch is on (read once, so a change applies from the next battle);
5. a creature race is registered;
6. a usable outer gate resolves.

Otherwise the role is inert and logs why. `OnCreated` clears any stale snapshot; `OnEndMission` and
`OnRemoveBehavior` clear it by token.

**Gate resolution and status.** The gate is the siege team AI's `OuterGate` or `InnerGate`; when that one
is hidden or disabled, the first visible, enabled gate with the same tag. A gate is passable when it is
destroyed (latched for good) or has stood open for 2 s without a break; a re-closed intact gate is
impassable again at once, so a flapping lever never counts.

**Ladders and towers: per-agent face exclusion.** At the first armed pass the service builds one id
list, in this fixed order, capped at 6:

1. each enabled tower's ground entrance (`DynamicNavmeshIdStart + 2`, read through a cached
   `AccessTools.FieldRefAccess` because the field is protected);
2. the distinct ladder `OnWallNavMeshId` values, ascending;
3. the tower bridges (`GetGateNavMeshId`), while room remains. They go last and are dropped without a
   warning: a bridge only matters to a creature already on a tower's wall walk.

Ids of 0 or less are skipped with one WARNING each (a tower without a navmesh prefab has start 0, and +2
would then be a scene face group). Each AI-controlled creature gets the whole list once, the first pass
that sees it after deployment, so reinforcements are covered within 0.5 s. Nothing is ever removed. The
v1.5.4 path planner skips a neighbour whose face id is in the agent's list (verified in the
disassembly, see Engine compatibility), and adding an id invalidates a current path that crosses it.

**Roles.** Each pass rebuilds the creature adapters from `Mission.Agents` and decides:

| Creature state | Role |
|---|---|
| Not AI-controlled, fleeing, retreating, a Retreat order, or no side | **Release**, once, and only if the role routed it |
| Defender, formation under AI command | **HoldGate**: a validated ground anchor behind the gate |
| Defender, formation commanded by the player | Release (D8) |
| Attacker, player-commanded, Charge or ChargeToTarget, gates not both passable | As the AI rows below |
| Attacker, player-commanded, any other order or after the breach | Release (D7) |
| Attacker, outer gate impassable and not already through it, a ram at work | **StandOff**: 10 m off the gate, out of the ram's way |
| Attacker, outer gate impassable and not already through it, no working ram | **StrikeOuter** |
| Attacker, outer gate passable (or already through it), inner gate present and impassable | **StrikeInner** |
| Attacker, every gate passable | **HoldCourtyard** (D10) |

- **Through the gate:** a creature counts as through the outer gate when its navmesh face id ends in 1
  (vanilla's own inside test) or it stands on the inner side of the gate's axis within 15 m of the gate
  and within 4 m of the axis sideways (a creature beside the wall is not in the doorway).
- **Working ram:** the ram is present and not deactivated, and agents stand at it now or it was in use
  within the last 20 s.
- **Strike:** a scripted position at a slot 2.2 m off the gate on the creature's side, plus
  `SetScriptedTargetEntity` on the gate, the same flags vanilla uses for gates. Slots are 3 per row
  (centre and 2.8 m to each side), rows 2.8 m apart, 4 rows; each is checked from the agent (same
  navmesh island) and falls back to the centre slot of the same side. It never tries the far side of a
  shut gate: the query only compares navmesh islands, and islands merge when ladders go up, so a "same
  island" answer behind the gate says nothing about the gate being open. The stand-off slots have no centre column: the gate's
  axis is the ram's lane, so a row is 4 m to one side, 4 m to the other, then 8 m out on the first side.
  A creature that finds no place for a role is not path-queried for that role again for 4 passes (2 s),
  unless its role decision changes first.
- **Hold:** a scripted position only, without `NoAttack`, so a holder fights what reaches it.
- **Anchors:** HoldGate tries the outer gate's `middle_pos`, then the inner gate's, then a point 6 m
  inside the outer gate; HoldCourtyard tries the inner gate's first. A `middle_pos` more than 15 m from
  its own gate (in XY) is dropped, so the chain falls through. An anchor counts only when its
  ground is within 4 m of its gate's base height (not a wall walk or a gatehouse roof) and the creature
  stands on the same navmesh island as it. The adapter probes the ground with a downward ray from 1.5 m
  above the hint and reads a miss as NaN (the native height query answers 0 on a miss, which is a
  plausible height). No valid anchor: Release and one WARNING.
- **Reapply:** a creature's scripted state is set only when its role or slot changes, or when the engine
  has cleared a flag the role set. Leaving Strike for any role clears the combat target first.
- **Fail-safe:** an exception inside a pass releases every routed creature, each in its own try, stops
  routing for the battle and logs one ERROR. The snapshot stays, so the cost and damage rules keep
  working.

Nothing is held across frames: route records are keyed by object reference and never dereferenced, and
nothing is keyed on `Agent.Index`. `OnMissionTick` never overlaps the agent tick, so the reconcile needs
no lock; `MissionThreadGuard.NoteCall` guards it.

### Component Diagram

```
siege/creature_siege_role.json     MCM "Battle Tactics/Creature Siege Role"
            |                                    |
CreatureSiegeRoleConfigProvider     CreatureSiegeRoleSettingsProvider
            \                                   /
             CreatureSiegeRoleMissionBehavior (MissionLogic)
                          |
                CreatureSiegeRoleService ------ CreatureSiegeRules (pure)
                 |        |        \             OversizedCreatureRaces (shared)
                 |        |         publishes
                 |        |            v
                 |        |   CreatureSiegeSnapshot <---- CreatureSiegeHooks <---- 4 GameModels
                 |        |                               (DetachmentCost,        (2 stat, 2 damage)
   CreatureSiegeMission  CreatureSiegeAgent                ScaleGateDamage)
   Adapter (gates, ram,  Adapter (exclusion,
   ladders, towers)      scripted movement)
```

## Decisions

| # | Decision |
|---|---|
| D7 | A player-ordered Charge after both gates fall: the trolls are released and obey, and may take stairs |
| D8 | Defender trolls in a formation the player commands obey his orders. (Siege Forces leaves trolls unticked by default) |
| D9 | The gate multiplier also applies to a player-controlled troll |
| D10 | After the breach, attacking trolls hold the courtyard anchor and fight what reaches them. A same-level chase is added only if RG-C fails |
| D11 | Only if RG-A fails: accept player-ordered climbing, or set `hill_troll` `CanClimbLadders="false"` in the live Armory with an in-repo gate (`cave_troll` is already false). Not needed so far |
| D12 | Relief-force battles stay vanilla |
| D13 | The gate break time target (for example 4 hill trolls break a 15,000 HP gate in 60 to 90 s) sets the JSON default, measured in RG-G |

## Edge Cases

| Scenario | Handling |
|---|---|
| Ram, ladders and tower | Humans crew everything. Trolls stand off while the ram works, then strike the outer gate, then the inner gate, then hold the courtyard |
| Ladders only | Trolls break the gate alone. An open gate makes the lane usable and vanilla re-plans |
| 5 m ladderless towers (Osgiliath E and W) | Vanilla's `DifficultNavmeshIDs` misses them; the excluded +2 entrance is the barrier |
| Trolls in a mixed infantry formation | Per agent: humans climb, trolls go to the gate |
| Defence under an AI general | HoldGate on a validated ground anchor, never on a wall top |
| Outer `middle_pos` between the gates, or hidden inner gates | The anchor fallback chain and the hidden-gate skip |
| Defenders open the gate for 2 s, trolls pass, the gate shuts | The through-the-gate test skips the outer-gate role |
| A task force pulls a troll | The role's flags persist while it lasts; its disband clears them and the role reapplies them |
| Fleeing or retreating creature | Released, never routed again while fleeing |
| Sally-out, relief force, lord's hall | Inert |
| Custom Battle siege | The same behaviour, through the Custom Battle models. No wall battle spawns horses, so mounted creatures do not arise |
| Co-op | Not on a client or replay. Puppets have `Controller` None, so they are never routed. The setting is in the co-op fingerprint |
| Player as a troll; Player Switcher | `IsAIControlled` is read every pass, so the player is never routed; the exclusion, once applied, stays |
| MCM toggled mid-battle | No change until the next battle |
| Save and load; a second campaign | No campaign state; the snapshot is token-guarded |
| TrollBruteForce smash near a gate | Its ring targets agents; the multiplier touches only `CalculateDamage` blows |
| Multiple tagged outer gates (only the unbound `haterade_erebor_town_z` stub has two; Orthanc has one, its other gate is retagged `inner_gate`) | The canonical first usable gate |

## Limits

- **Exclusion budget.** The cap is 6 and must not be raised (see Design Challenge). The default TAOM
  scene, two towers and four ladders, fills it exactly with the two entrances and the four ladders, so its
  tower bridge is dropped, quietly; the activation line names it under `dropped`. A scene with more
  ladders or towers drops a ladder or an entrance too, and that one logs a WARNING naming the ids. The
  entrances and ladders, which matter for climbing, come first.
- **A formation made only of creatures** counts as outside the walls until the gate falls
  (`TeamAISiegeComponent` :255-285), which can delay vanilla's breach plans.
- **A troll-only formation assigned to the ram or a tower** leaves that machine unmanned, because every
  troll's cost is `+Infinity`. RG-L watches for it.
- **Defender team in the SallyOut tactic state inside a wall battle:** AI trolls keep holding at ground
  level.
- **No live toggle.** The exclusion is never removed, so switching the role off mid-battle is not
  supported; the switch applies from the next battle.
- **Item pickups** are skipped while scripted, as vanilla does; TOR_Core's `UseGameObject` block is not
  reproduced.
- **The gate multiplier is provisional.** The shipped 2.0 is a placeholder until RG-G measures one troll's
  blow on a gate in game and D13 sets the real value.

## Configuration

### MCM: `Battle Tactics/Creature Siege Role`

| Setting | Default | Notes |
|---|---|---|
| Trolls Stay Off Siege Ladders, Towers and Walls (`EnableCreatureSiegeRole`) | On | Read once per battle at `AfterStart`, `RequireRestart = false`. Off: trolls behave as in vanilla |

### Config File: `Main/_Module/ModuleData/siege/creature_siege_role.json`

| Field | Type | Description |
|---|---|---|
| `gateDamageMultiplier` | number | The factor a creature's melee blow on a castle gate is multiplied by. Finite, in [1, 10] |

```json
{
  "gateDamageMultiplier": 2.0
}
```

Validation: a missing file, text that is not JSON, a missing key, a value that is not a JSON number (a
string or boolean is refused), a NaN or infinity (rejected before the range check), or a value outside
[1, 10] falls back to 2.0 with one WARNING that names the key. The provider is a singleton that reads the
file once per process, so **an edit needs a game restart**; a new battle, a new campaign or a reload does
not reread it.

### Current Values

| Value | Source | Notes |
|---|---|---|
| `gateDamageMultiplier` 2.0 | JSON | **Provisional until RG-G.** TAOM's own number, not TOR_Core's |
| Reconcile stride 0.5 s, gate open debounce 2 s, ram grace 20 s | `CreatureSiegeRules` constants | |
| Strike stand-off 2.2 m (hill troll capsule 1.2 m plus reach), lateral 2.8 m, rows 2.8 m, 3 x 4 slots | `CreatureSiegeRules` | Tuned by RG-D |
| Stand-off 10 m, lateral 4 m (columns at +4, -4, +8, none on the gate axis) | `CreatureSiegeRules` | Tuned by RG-F |
| Through-the-gate radius 15 m and lateral 4 m, anchor height tolerance 4 m, inside anchor 6 m, middle anchor at most 15 m from its gate | `CreatureSiegeRules` | |
| Placement retry after 4 passes | `CreatureSiegeRules.PlaceRetryPasses` | |
| `MaxExcludedFaceGroups` 6 | `CreatureSiegeRules` | Do not raise |

## Reading the log

Every line carries `[CreatureSiegeRole]` and is built by `CreatureSiegeReport`.

| Level | Line starts | Means |
|---|---|---|
| INFO | `active on <scene>: outer gate "..." (visible, closed, hp N, origin x, y, z), inner gate ..., gate damage xN, exclusion [ids], skipped ..., dropped ..., hold anchor <source> (x, y, z) N m from the outer gate, courtyard anchor ...` | The first armed pass, once per battle: the gates resolved, the ids applied in order, and the anchors chosen. Every anchor's XY distance is from the outer gate, so an inner-gate anchor reads far by design where the gates are far apart. Check the anchors against the scene (RG-E) |
| INFO / DEBUG / WARNING | `inert on <scene>: <why>` | The role does not act in this battle. DEBUG for a non-siege mission, WARNING for no usable outer gate, INFO otherwise (sally-out or relief, client, setting off, no creature race) |
| WARNING | `skipped navmesh face id <tier> <value>` | An id of 0 or less; creatures stay free to walk on that face |
| WARNING | `exclusion list cut at 6 ids on <scene>; dropped [...]` | A tower entrance or a ladder id did not fit the budget. A dropped tower bridge alone logs nothing |
| WARNING | `an excluded creature is in a ladder queue` | The exclusion did not keep it out. This is the RG-A failure signal |
| WARNING | `no reachable <role> slot`, `no valid anchor for <role>` | Left to vanilla's AI; each said once per battle |
| DEBUG | `creature <role> (<reason>) slot N at (x, y, z), creature at ..., inLadderQueue ...` | A role change, throttled (a burst of 8, then 2 a second) |
| DEBUG | `creature <role> re-applied: the engine cleared its scripted ...` | Vanilla cleared a flag and the role set it again |
| DEBUG | `creature gate blow: base N, scaled N (xN), gate hp N before the blow` | One multiplied blow, throttled (a burst of 4, then 1 a second). RG-G reads these. There is no "after": see How to tune |
| ERROR | `the reconcile failed on <scene> (...)` | The fail-safe ran: every routed creature released, routing stopped, the cost and damage rules stay on |
| ERROR | `activation failed on <scene> (...)` | `AfterStart` threw; the role is inert and the cost and damage rules are off |

A `[N suppressed]` suffix counts lines the throttle swallowed since the last one written.

## Key Files

| File | Purpose |
|---|---|
| `Main/Features/CreatureSiegeRole/CreatureSiegeRoleModule.cs` | Registrations, the mission behaviour declaration, the hooks' logger |
| `Main/Features/CreatureSiegeRole/CreatureSiegeRoleService.cs` | Activation, arming, the per-pass reconcile, the fail-safe |
| `Main/Features/CreatureSiegeRole/Domain/CreatureSiegeRules.cs`, `CreatureSiegeRules.Roles.cs` | Pure rules: activation, cost, damage, exclusion ids, gates, roles, slots, anchors, reapply |
| `Main/Features/CreatureSiegeRole/Domain/CreatureSiegeSnapshot.cs` | The published, immutable per-mission record |
| `Main/Features/CreatureSiegeRole/Domain/CreatureSiegeReport.cs` | The log line text |
| `Main/Features/CreatureSiegeRole/Hooks/CreatureSiegeHooks.cs` | What the four models call |
| `Main/Features/CreatureSiegeRole/Hooks/CreatureSiegeRoleMissionBehavior.cs` | The `MissionLogic` boundary |
| `Main/Features/CreatureSiegeRole/CreatureSiegeRole{Config,Settings}Provider.cs` | JSON multiplier, MCM switch |
| `Main/Adapters/{I,}CreatureSiegeMissionAdapter.cs` | Gates, ram, ladder and tower ids, ground height, the creature list |
| `Main/Adapters/{I,}CreatureSiegeAgentAdapter.cs` | Per-agent reads, face exclusion, strike, hold, release, path checks |
| The four models (see the [GameModel registry](../reference/gamemodel-registry.md)) | One-line delegates to `CreatureSiegeHooks` |
| `Main/_Module/ModuleData/siege/creature_siege_role.json` | The multiplier |

## Dependencies

- `IRaceManager` (Core): the race ids of `OversizedCreatureRaces`.
- `IModLogger`, `IPathService` (Core).
- `MissionThreadGuard` (AdvancedCombat): the off-thread tripwire on the tick.

## Tests

All under `TAOM.Tests/Features/CreatureSiegeRole/`:

- `CreatureSiegeRulesTests*.cs`: the activation matrix; `+Infinity` and NaN lose while `MaxValue x 0.5`
  wins; the damage gate, one test per condition plus NaN, infinity and zero; exclusion order, dedupe,
  filters, cap and truncation; gate status (latch, 2 s, re-close, NaN clock, hidden or disabled); the
  through-the-gate test; `RamWorking`; the `DecideRole` matrix; slots and anchors with non-finite inputs;
  `NeedsReapply`.
- `CreatureSiegeRoleServiceTests.cs` (NSubstitute adapters): not armed before deployment; exclusion once
  per agent, in order; reapply on cleared flags; combat target cleared on leaving Strike; release exactly
  once; fail-safe; one-time warnings.
- `CreatureSiegeHooksTests.cs`: the hooks' decisions, and IL pins that the cost path has no `newobj`,
  `newarr` or `box` and calls only allowed members.
- `CreatureSiegeSnapshotTests.cs`, `CreatureSiegeLogTests.cs`, `CreatureSiegeRoleMissionBehaviorTests.cs`.
- `CreatureSiegeRoleConfigProviderTests.cs`, `ShippedCreatureSiegeRoleConfigTests.cs`,
  `CreatureSiegeRoleSettingsProviderTests.cs`: one test per validation rule; the shipped file parses.
- `CreatureSiegeRoleBindingTests.cs` (`RequiresGame`): every engine member the hooks, adapters, behaviour
  and four overrides reference, the protected `DynamicNavmeshIdStart` field, the enum members, and that
  each override really overrides the engine virtual.
- `CreatureSiegeRoleWiringTests.cs`: module listed once, no patch or save data, behaviour declared once;
  `IlCallScanner` pins that each model calls its base first and then the hook, with no branch.

## Engine compatibility (v1.5.4)

The design was researched on v1.5.3; Steam moved the installed game to v1.5.4 on 2026-10-05, and every
change here was checked against the v1.5.4 DLLs.

- **Managed:** nothing the role relies on changed. 2 of 1,036 top-level types in TaleWorlds.MountAndBlade
  changed (`SiegeQuerySystem.LocateAttackers`, `MapEvent.RemoveInvolvedPartyInternal`), neither a seam
  here. The `DetachmentManager` selection rule, the `LadderQueueManager` recruit and drop logic, the
  `SiegeTower` entrance and queue face, `SiegeLadder.OnWallNavMeshId`, the `CastleGate` `middle_pos`
  lookup and every `Agent` member used are unchanged, at the same lines.
- **Native (TaleWorlds.Native.dll, sha256 prefix `1e6f5ef562cd2010`):** every function the design relies
  on is instruction-identical to v1.5.3 apart from relocation. Two findings refined the design: the
  registry's clean sets are bits 1 to 6, so the cap is **6** (bit 0 is the no-exclusion bit, the re-mark
  starts from index 1 at `0x401B20`; bit 7 is the navmesh base boundary, set at navmesh load `0x3FD4A4`
  and ORed into every exclusion search `0x4DD0B5`); whether registry entry 0 is pre-seeded is unverified,
  so 6 is safe either way; and **path
  planning skips excluded faces in code** (the planner passes the agent's id list to A*, which skips a
  neighbour in the list, `0x4DCFD0`), so RG-A is a confirmation spike, no longer a decision gate.
- The repo still pins v1.5.3; no engine bump was made for this feature. The binding tests run against
  the installed v1.5.4 DLLs.

## How to tune the gate damage

1. Run RG-G: a Custom Battle siege with trolls on a ladders-only castle; read the `creature gate blow`
   lines.
2. Set `gateDamageMultiplier` in `Main/_Module/ModuleData/siege/creature_siege_role.json` so the D13
   target holds (for example 4 hill trolls break a 15,000 HP gate in 60 to 90 s).
3. Restart the game. No code change.

The `creature gate blow` line shows the damage before and after the multiplier and the gate's hit points
before the blow, and no hit points after it. The engine applies the campaign damage bonuses
(`MeleeWeaponDamageMultiplierBonus`, `DamageMultiplierBonus`, career buffs) after the hook returns, so a
Custom Battle measurement reads lower than the same blow in a campaign. Tune against the campaign number
when it matters.

## How to verify in game

Owed; nothing below has been run yet. Desktop, Debug build, Custom Battle first, then campaign:

1. The RG-A, RG-B, RG-C, RG-D and RG-G spikes (below).
2. Mordor trolls assault a castle with ram, ladders and tower: the activation line lists the ids and
   gates; trolls never queue, climb or crew anything; they stand off while the ram works, then strike;
   humans still use every machine. Watch humans entering tower 1: their path smoothing near the first
   excluded face may change, their access must not.
3. Ladders only: time N trolls against 12k, 15k and 18k HP gates; the trolls stay off the walls.
4. A gate broken by trolls is passable, and both gates' wreckage looks acceptable (the code relies on
   the last destruction state having no door colliders).
5. Osgiliath E or W with a 5 m tower: trolls never enter it; check the activation line's `dropped` list
   (a bridge there is expected and quiet).
6. Scenes: gondor_castle_001 (hidden inner gate), Helm's Deep, Minas Tirith (its outer `middle_pos` is
   not between the gates; read the anchor line), Orthanc (one outer gate, the other is retagged
   `inner_gate`), and Minas Morgul (`mordor_town_minas`, the outer `middle_pos` 119 m away) and Dale (27
   m): in those two the outer middle is dropped, so the trolls hold the inner gate there by design; the
   anchor line shows the inner-gate anchor well away from the outer gate. In each scene, check the anchor
   line and that defender trolls reach the ground anchor.
7. Defence with a troll garrison under an AI general: trolls come down from the walls and never stand
   on wall tops; humans still man the stone piles and the lever.
8. Player Charge sends trolls to the gate; Follow and Move stop at the ladder foot; Charge after the
   breach follows D7.
9. A lever held or fought over: no role flicker in the log.
10. Reinforcement waves are routed within 0.5 s.
11. Flee and Retreat release trolls.
12. Sally-out and relief force log `inert`. The next field battle is vanilla.
13. A task force forms on trolls: no stair chase (RG-H).
14. A troll-heavy AI Mordor army (RG-L).
15. Player as a troll.
16. MCM toggled mid-battle: no change until the next battle.
17. Save, load and fight a second siege.
18. No `MissionThreadGuard` warnings and no exceptions; the first armed pass shows no hitch in the
    profiler.

## Research gates

All owed unless marked done.

| Gate | Question | How | What it decides |
|---|---|---|---|
| RG-A | Does native path planning honour per-agent exclusion for ladder faces and the tower entrance? | Verified in the v1.5.4 disassembly; a Custom Battle spike still owed: one excluded hill troll in a player formation ordered to Move onto a wall top past a ladder and a tower, with `inLadderQueue` and position logged | Fail: always route AI creatures in player formations, and D11 |
| RG-B | Does an in-formation troll with a strike slot and a gate target walk there and damage the gate? | Custom Battle, ladders-only castle, gate HP logged | Fail: the parking fallback (one loose `ClimbingMachineDetachment` per team), not built |
| RG-C | Does a holder fight enemies in reach? | Defender trolls when attackers enter | No: add a same-level engage rule (D10) |
| RG-D | Strike distance | Spike measurement | Sets `StrikeStandOff` |
| RG-E | Exact TAOM anchors per scene | Read-only scene survey done 2026-10-05 (20 TAOM_Map scenes with gates; every one has 2 tower spawners, the 19 with ladders have 4 ladder ids each); the runtime anchor line per scene is owed | Scenes to fix in the Kit (Mike) |
| RG-F | Do stand-off slots block ram pushers? | In game | Tunes 10 m, 4 m and 20 s |
| RG-G | Gate damage per troll blow | In game, the `creature gate blow` lines | The JSON default (D13); 2.0 is provisional until then |
| RG-H | Does a task-force troll chase up the stairs while its position persists? | In game | Yes: add vanilla's `RemoveAgent` then `AttachUnit` |
| RG-I | Hidden inner gates: is physics present, and does vanilla target them? | In game, gondor_castle_001 and Helm's Deep | Confirms the hidden-gate skip |
| RG-J | How co-op runs battles | Read-only research done: each peer runs the AI of the agents it owns, other peers hold puppets with no controller | A co-op battle check is still owed |
| RG-K | Overlap with other mods' patches | Done for Siege Forces' targets; the role adds no patch | None |
| RG-L | A troll-only formation joined to a machine | In game, troll-heavy AI Mordor army | If seen: a machine hand-off follow-up |
| RG-M | Cancel path and menu after a defeat | Siege Forces' gate, see [siege-forces.md](siege-forces.md) | |

## Changelog

- 2026-10-05: first build on `feat/siege-forces` (#735). Detachment cost and gate damage seams on four
  models, per-agent face exclusion, gate roles, anchors, MCM switch, JSON multiplier (provisional 2.0).
  Checked against the v1.5.4 DLLs. Not yet seen in game.

## GitHub Issue

- **Issue:** #735, [Creature siege role: trolls skip ladders, break gates and stay off walls](https://github.com/haterade22/TAOM/issues/735)
- **Status:** Open (in-game check owed)
