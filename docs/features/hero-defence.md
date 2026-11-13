# Hero Defence

## Overview

Hero Defence is a planned third campaign-free mission mode, beside The Long Defeat and Fords of War. It is a tower defence where the
towers are named heroes: creeps from every culture walk one serpentine road to an exit, and the player buys heroes (Legolas, Gimli,
Aragorn and others) and places them during a pause. When the wave comes the heroes fight by themselves. Each creep that reaches the
exit costs lives, and the run is endless until the lives run out.

**Status (2026-11-13):** in development under #1106. Mike's sixteen open choices are decided (see "Decisions" below). The pure core is built: the run rules and the wave generator, with their
config readers and shipped data. Nothing is registered in IoC or `SubModule.xml`, so the mode is not in the game yet. The engine
recipes come from spike runs on the Fords of War scene; the Gate 1 results are in "Spike results (Gate 1)" below.

## Why This Exists

- **Vanilla behavior:** Bannerlord has no tower defence. Custom Battle sends two armies at each other.
- **TAOM requirement:** a short, replayable mode where the player places Middle-earth heroes against waves of every culture's
  troop tree, with a gold sink (hero levels) and a score to beat.
- **Without this feature:** nothing breaks. It is new content.

## Architecture

### Design Challenge

A Bannerlord AI hero must hold a chosen point and still fight, and creeps must follow one long road in packs without the team AI.
Both are engine behaviour, so the rules stay in a pure domain that an engine director drives, and the risky engine recipes are
proven in spikes before the scaffold is built.

### Solution Approach

- `Domain/` holds plain rules with no engine type (`HeroDefenceDomainPurityTests`):
  - `HeroDefenceRun` (with `.Shop` and `.Wave` partials): phases (`Pause`, `Wave`, `Ended`), gold, lives, kills, the shop, placement,
    wave release, leaks, the wave time limit, the clear bonus, and heroes who fall and return at the next pause. Each `Tick` returns
    a `DefenceEffects` list (hero spawns, moves and resets, creep batches, removals) for the director to apply.
  - `CreepWavePlan` (with `.Troops`): builds each wave from the seed and the wave number alone, so a preview and its wave cannot
    differ. It draws the culture rotation, the level band, the class counts, the troops, the leader and the powers.
  - `PlacementRules`: whether a hero can stand at a clicked point (zone, boundary, distance from the road ends and from other posts).
  - `HeroLevels`: the hero ladder, level 1 when bought up to `MaxLevel` (20). Level L costs `price x 0.4 x 1.3^(L-2)`, rounded, at
    least 1 gold, and each level adds 10% health, 8% regular damage, 8% ability numbers and takes 2% off cooldowns (never below
    25%), all as shares of the level-1 value. A setting that gives a zero, negative or NaN price puts the level out of reach
    (`int.MaxValue`, never sold, even from a purse at its cap), so a bad setting never sells a level. `HeroDefenceRun.TryLevelHero` sells a level in a pause only. A
    placed hero gets a `HeroResets` order, so the director spawns it again with the new numbers; a fall and a wave clear keep the
    level. The defaults are the plan's first guesses; `heroes.json` (Slice 5) will carry them, and the engine applies the factors
    in Slice 11.
  - `PackOrderRule`: whether a creep pack marches (Move) or fights (Charge). It takes the flat distance of the nearest creep and
    hero pair whose heights differ by at most 6 m; the nearest creep, not the lead, because a lead key missed a wide line in spike
    BLOCK-O-M1. A pack on Move charges at 12 m or less, and a pack on Charge moves on at 22 m or more, measured over every live
    creep, front and rear. With no hero in reach (all fell, or only plateau heroes stand) the pack moves at once: with no enemy
    left the engine swaps Charge to Stop (spike HERO-FALL), and a Charge against a plateau hero alone was never tested. The director
    passes only the heroes that can draw a charge: standing, and on the road or under the player's control; never a hero on a
    plateau, controlled or not. The band is the foot band from spike run 5; mounted packs have no measured band yet.
  - `PackStallClock`: one per pack. The pack stalls when its mean progress along the road has not grown by 5 m, and no blow given
    or taken involved it (blocked hits included), for 40 s. A pushed-back pack must pass its old mark again. When a creep arrives or
    falls, the mean jumps by itself, so the mark moves by the same jump: the gain since the mark still counts, and the change is
    no activity. The director then calls `HeroDefenceRun.PackStalled(packId)`, which removes the pack by the wave-limit split: free
    with a hero standing, a leak with none. If a leak takes the last life, the rest of the pack still leaves the field, and so do
    the rest of the creeps at the wave limit and at the exit.
- `Config/` reads the JSON files. A reader never throws: a bad value reverts to its default with one warning, and a file with a
  repeated key or a zero-byte file reverts as a whole.
- The director, the adapters, the launcher and the client views come in later slices (plan Slices 3 to 14).

### Director contract

The director is the engine side that applies each tick's `DefenceEffects`. The run trusts it to keep these rules:

1. Apply `WaveBegan` before the creep batches of the same tick, because creep armour is written once at spawn.
2. Report every creep of a `CreepBatch` through `CreepSpawned`, with the batch's slot, before the next `Tick`. Until then the run
   counts no creep of that pack.
3. Give each creep a unique `AgentKey`. `CreepSpawned` ignores a key that is already alive, so a repeat cannot count a creep twice.
4. Report each removal through `CreepGone`. A second report for the same key changes nothing.
5. A creep that fails to spawn has no API yet. The run never counts it, so the wave clears without it; Slice 4 decides whether
   that needs a report.
6. Apply each hero order as it is. A hero has at most one order in a tick (a spawn, a reset or a move), made from its post and
   level at the end of the tick: a spawn includes a reset and a move, and a reset includes a move. A spawn and a reset give the
   hero the numbers of the order's `Level`. A hero who falls before the tick gets no order; the next clear spawns it.
7. Keep one `PackStallClock` per `CreepBatch`, made when the batch spawns and dropped when the pack stalls or empties. Feed it
   mission time, which must never go backward; the mean progress of the creeps that have not arrived; the number of those creeps;
   and whether any blow given or taken by the pack, blocked hits included, landed since the last reading. Report a stall with the
   batch's `PackId`, never its slot: a later pack can take the same slot, and an old `PackId` matches no creep.
8. Report through `HeroDown` only a hero who fell in the fight, never the removal the director makes to apply a spawn or a reset.
   Such a report would order a second spawn in a pause, and would mark the hero as fallen in a wave.

A tick that ends the pause charges only the rest of its time to the wave, so a long tick cannot use up the wave's time limit.

The map's entry and exit come from scene tags in Slice 3. A NaN or infinite position fails every distance test, so no creep would
leak and no post would be too close to a road end: the tag reader must refuse such a map.

### Component Diagram

```
waves.json  cultures.json  rosters.json
        \        |        /
      HeroDefenceConfigCatalog (readers, never throw)
                 |
      WaveCatalog + DefenceRules
         /                 \
  CreepWavePlan       HeroDefenceRun  <-- director (later slice) applies DefenceEffects,
  (wave briefs)       (run rules)         reports spawns, deaths and leaks
```

Reused unchanged from the other modes: `Vec3f` and `AgentKey` (`Main/Features/FordsOfWar/Domain/`), `RunRandom` and `EndlessScale`
(`Main/Features/LongDefeat/Domain/`).

## Decisions (2026-11-13)

Mike took the recommended option on all sixteen open choices ("proceed with your best recommendations"). The evidence for each is
in "Spike results (Gate 1)" below. The slice that applies a choice owns its test.

| # | Choice | Decision | Where it lands |
|---|---|---|---|
| 1 | Ability arrow thrust | 100 (pierces the heavy tower shield out to 100 m) | Slice 10 item |
| 2 | Melee hold against a 30-creep pack | A 10 m leash: past 10 m flat from his post the hero teleports back | Slice 5 |
| 3 | Melee facing | M5, face the target | Slice 5 |
| 4 | Mounted packs at a road hero | Ride through: a mounted pack never charges | Slice 6; a short ride-through spike confirms it |
| 5 | Hero cap | 6 on the field | `waves.json` `maxPlaced`, already 6 |
| 6 | Release after a charged hero falls | Move when no road hero stands within the 22 m release distance | `PackOrderRule`, already so (`Charge_ChargedHeroFell_*` tests) |
| 7 | Creep archers | Ranged row plus director stands: stand 10 to 12 s against a hero ahead, never behind; drop the row once past | Slice 6 |
| 8 | Target priority | Steers the regular bow too (automatic selection off, target set at 2 Hz) | Slice 12 |
| 9 | Ranged hero reached in melee on the road | Plateau posts for ranged heroes until base health is tuned | Slice 5 placement |
| 10 | Base hero health | 3000 for now, one JSON value, tuned in the first balance run | Slice 5 `heroes.json` |
| 11 | Controlled hero leads a pack off the road | Pack leash: a pack more than 15 m off the road goes back to Move (15 m is a guess) | Slice 8 |
| 12 | A wave that reaches its time limit | The wave clears, but pays no clear bonus | `HeroDefenceRun.ExpireWave`, done |
| 13 | Mounted bounty rounding | Round once, after the +50% | `HeroDefenceRun.Bounty`, already so |
| 14 | Growth cap | Factor 20 | `waves.json` `maxFactor`, already 20 |
| 15 | Shaghana | Merged into Harad | Roster generator, already so |
| 16 | Wave leaders | Approved as listed in `cultures.json` | Already so |

## Spike results (Gate 1)

Runs (Fords scene, Custom Game): `E:/Temp/hero-defence-runs/20261109-122038` (PROBE, HOLD-R, HOLD-M M1 to M3, CLIFF-A, ARROW, RETARGET, BLOCK-O, CHASE, REMOVE), `20261109-124630` (HOLD-M-M4/M5, BLOCK-O-M2/M4/M5, UNDERFIRE-PLAIN/ROW, RETARGET-POSTS, REDRAW), `20261109-130336` (RETARGET-POSTS, OFFROAD), `20261109-141032` (ARROW, OFFROAD-CROSS, TOWER-FLIP), `20261109-201337` (run 5: 31 verdicts, `run over reason=final faults=0`, no CrashReport fault, build 20261110-020302Z+d1572fcc1.dirty), `20261109-224259` (run 6: PIERCE-RANGE, HOLD-DIAG, LEASH, CAV-BLOCK-2, SIX-2; 29 verdicts, `reason=final`, no CrashReport fault, build 20261110-044225Z+d1572fcc1.dirty; its six PIERCE-RANGE cells at 60 m and 100 m are invalid, because a computed drop raise sent every arrow over the shield), `20261109-233240` (run 7: those six cells again with the raise measured in run 6; 7 verdicts, clean, build 20261110-052512Z+d1572fcc1.dirty). `20261109-152907` is not a run (preflight refused on the idle gate). Runs 6 and 7 ran at about 117 fps against about 240 in run 5; VSync is off in both rgl logs, and the cause is not logged.

| Recipe | Chosen | Evidence | Status |
|---|---|---|---|
| Ranged hold | Posts form: shared Ranged formation, one Move to the post, `SetScriptedPositionAndDirection` (ConsiderRotation) re-asserted at 2 Hz, Alarmed, FireAtWill | HOLD-R-A and HOLD-R-B (122038): `shots60s=20 drift60s=0.00`. Run 5 HOLD-POSTS-4X: `shots60s=20 drift60s=0.00 swaps=1 driftAll=4.05`; 10X the same with `driftAll=5.60` (10x is smoke only) | Proven with limits: standing fire with no pressure, at 1x and 4x. When melee reaches him he swaps to his sword, stops firing, is pushed 4 to 6 m and loses 1289 HP in about 14 s (log about line 20771). The contact phase has no gate |
| Melee hold | Posts form in the shared Infantry formation, M4 (fixed facing) or M5 (face target). Against a 30-creep pack, candidate: a 10 m leash (teleport home past 10 m flat, then re-assert the hold), pending Mike's look | HOLD-M-M4 `blows=11 steady=1.34 drift=7.83`, HOLD-M-M5 `blows=9 steady=0.88 drift=1.22` (124630); run 5 HOLD-M5-4X `steady=0.59 drift=1.44 blows=4`, HOLD-M5-10X `steady=1.27 blows=7`. 30 creeps: run 5 BLOCK-R peaks M4 2.66, 2.24, 24.65 and M5 5.13, 76.03, 7.90; run 6 HOLD-DIAG peaks M4 116.44, 17.54, 49.78, 14.98, 13.88 and M5 78.92, 14.31, 2.79, 28.24, 70.91. Run 6 LEASH-M5 (3 reps): `fires=5, 8, 4 maxDriftAfter=0.00 crowd1mPeak=0`, re-contact after 1.50 to 1.85 s, a new push about every 7 s, `peakDrift` 10.40 to 10.61, `blows` 21, 14, 15, HP 2553 to 2765 of 3000 | Fails against 30 creeps without a leash: 11 of 16 reps over 8 m (M4 6 of 8, M5 5 of 8), so the form makes no difference. A rep holds only when the pack wraps round him within about 3 s. The leash keeps him within about 10.6 m, a looser bar than 8 m; the pack follows him back each time and never releases. Mike judges how the jump looks |
| Cliff archer | None chosen. Scripted arrows from height work | CLIFF-A (122038) `shots=0`; TOWER-FLIP-HOWDAH and POSTS (141032) FAIL (`hiddenShots=5` and `6`); run 5 ARROW-ARC-HEIGHT-SHIELD 30 of 30 hits at 62 m flat from the tower top with the drop raise | Unproven for a native archer. Owed: CLIFF-B |
| Regular-attack target | `SetAutomaticTargetSelection(false)` plus `SetTargetAgent` at 2 Hz; restore automatic selection when priority is "any" | RETARGET (122038) `onIntended=19` of 19; RETARGET-POSTS (124630, 130336) 19 of 19; run 5 RETARGET-MOVING-SET 15 of 15 strikes on cavalry, `readbackAgree=139 readbackSamples=139`, against AUTO `cavalryShare=0.32` | Proven with limits: one bow hero, cavalry class only, automatic selection off. 10 of 14 rider strikes hit shields |
| Charge rule (foot) | Key on the nearest live creep, not the lead; Charge at 12 m flat, Move when every live creep is 22 m or more away, Move at once when no candidate hero stands; poll every 0.25 s | CHARGE-MIX-NEAR PASS `changes=1` for about 78 s; HERO-FALL PASS (`after_fall t=55.00 packOrder=Move`, `pack1Arrived=29/29`); BLOCK-O-M2/M4/M5 `passed=0/30`; BLOCK-O-M1 `passed=30/30` with the lead key. CHARGE-MIX-DOWN FAIL is a harness defect (arrived creeps counted as still) | Proven for one foot pack and one road hero. The 6 m height gate was never exercised. Hero down with a second road hero standing is untested |
| Charge rule (cavalry) | None chosen; Mike decides between ride-through and a Charge that holds riders on the hero | Run 5 CAV-BLOCK: Charge at 11.99 m, 19 of 20 riders more than 5 m past by t=21, `blows=0 hitsOnHero=5`, release at 22.33 m during the run-out. Run 6 CAV-BLOCK-2-D8 (release only after every mounted rider stayed 45 m out for 8 s): PASS, `returnsCharging=74 returnedRiders=20/20 blows=8 hitsOnHero=53 hp=2281`; it released once, at 75 s with the pack upstream, and Move brought the pack back onto him at 89 s (log lines 1168 to 1276). CAV-BLOCK-2-HOLD (Charge held): PASS, `returnsCharging=25 returnedRiders=14/20 blows=3 hitsOnHero=23 hp=2120` | Riders return for more passes while the pack stays on Charge, so the plan gate passes. The hero lands 3 to 8 blows in 120 s and loses 700 to 900 HP. Riders wheel about 72 m out (median), which a 12 m road with 92 m runs cannot hold (estimate). Ride-through with no Charge at all is untested |
| Removal | `FadeOut(true, true)` on the mission tick; handle `OnAgentRemoved` and `OnAgentDeleted` | REMOVE (122038) `fades=408 removedEvents=580 deletedEvents=267 activeLeft=0`, run `faults=0`; HERO-FALL `faults=0 removedEvents=388 deletedEvents=387` | Proven in Fords. `OnAgentDeleted` lags the fade by about 30 s. The REMOVE line does not split foot from mounted |
| Ability arrows | Dedicated Arrow item at 45 m/s: CanPenetrateShield, MultiplePenetration, BonusAgainstShield, CannotBePickedUp; thrust 100 recommended, Mike decides | Run 5 PIERCE-LIGHT-30 shield damage 46 against gate 33: pierced. PIERCE-RANGE against the heavy tower shield (gate 75), front view, 30 arrows a cell (34 m in 224259, 60 m and 100 m in 233240): thrust 60 does 69, 65, 60 at 34, 60, 100 m (none pierced); thrust 100 does 118, 112, 103 (every shield hit pierced); thrust 140 does 167, 159, 147. `gateAgree` held on every shield hit but one | Proven, front view: thrust 100 pierces the heaviest roster shield out to 100 m with 28 points to spare; the lowest piercing thrust at 100 m is about 74. Body damage after a pierce averaged 49 to 68 (thrust 100). At 100 m, 5 and 6 of 30 arrows missed by up to about 1.1 m with thrust 100 and 140, and none with thrust 60 |
| Six heroes | Cap 6 | SIX: `formationFaults=0 faults=0`, inf=3 rng=3 in every sample, all six up at 120 s, MissionPerf 234 to 247 fps; FAIL on M1 drift 13.60, M2 steady 2.71, B2 drift 2.11. Run 6 SIX-2 (4 reps: the Infantry Move point on M1's post, or one formation per melee hero): `faults=0 formationFaults=0`, all six up, FAIL on M1 drift 20.62, 29.92, 19.40, 2.45; M3, B1 and B3 never reached; 112 to 118 fps | Stable. The drift is the 30-creep press on the front hero (every pack charges M1), not the formation point. Six heroes all in contact at once, and more than 6 heroes, are untested |

Owed before Gate 2: CLIFF-B, HAIRPIN (real scene), PLACE (Mike, 10 minutes).

**Engine findings**

- A CanPenetrateShield missile passes only when shield damage exceeds 30 + 3 x shield armour (`Mission.cs:5836`, `managed_core_parameters.xml:232-236`); run 5 matched it on armour 1 and armour 15 shields. A pierced arrow reaches the body at full damage (neck 114 + 40 absorbed pierced, 96 + 58 bare).
- Missile shield factor is 0.5 only with CanPenetrateShield and MultiplePenetration (0.15 otherwise), and BonusAgainstShield doubles it (`MissionCombatMechanicsHelper.cs:539`, `:554-557`). Same thrust-30 arrow on the same shield: 6 without the flags, 46 with them.
- Missile magnitude is `(missileSpeed / startSpeed)^2 x totalDamage` (`DefaultStrikeMagnitudeModel.cs:13-14`), so shield damage falls with range. On the aimed flat path (v/v0)^2 was 0.940 at 34 m, 0.894 at 60 m and 0.827 at 100 m, and the heavy shield's damage fit `1.303 x thrust x (v/v0)^2 - 4.5` within 2 points in all nine PIERCE-RANGE cells.
- Native drag is weaker than the earlier k=0.003 guess. On flat ground a vacuum-aimed 45 m/s arrow lands about 0 m low at 34 m, 0.22 m low at 60 m and 1.6 m low at 100 m (runs 6 and 7, spread under 0.07 m). A raise computed with k=0.003 (1.01 m and 6.08 m) sent every arrow over the shield. A drag-aware `VolleyAim` must use measured values.
- A formation's Charge becomes Stop when no enemy team has active agents (`Formation.cs:2467-2469`, `MovementOrder.cs:804-815`, `:1107-1115`); HERO-FALL logged `packOrder=Stop` at the fall. The pack rule must own the return to Move.
- Native target picking never chose the unreachable tower hero, on Move or on Charge (`towerTargetedPeak=0` in all three CHARGE-MIX kinds).
- A mounted pack on Charge rides past a lone foot hero; the hero landed no blow, blocked hits included.
- The Ranged row the plan names (2,7,4,20,5) is the native Charge set's row (`HumanAIComponent.cs:764`). A marching pack with it stops short of its Move goal and stands shooting (UNDERFIRE-HALT-OFF stood 160 s at meanY about 560 against a goal of 585). Creep archers fire 0 shots while they march.
- Plain creep arrows barely hurt a shielded hero: 65 hits before melee did 12 HP (UNDERFIRE-HALT-ON). Unshielded: about 9.7 HP a hit.
- Under a 30-creep press a Posts hero is displaced in most reps (11 of 16 over 8 m in runs 5 and 6). The move starts within about 1 s of contact, with about 20 creeps within 3 m on the press side and 0 to 3 on the other, and it carries him away from the press at 2 to 3 m/s. His scripted position (flags 5), Move order and formation stay set, and no knockback or knockdown blow lands. `GetRealGlobalVelocity` matches the displacement; `Agent.Velocity` reads 0.07 to 0.74 of it, and whether that native value includes a collision push is unknown. He moves back only when creeps flow round to his other side.
- `Agent.GetTargetPosition` read (0,0) in every HOLD-DIAG sample of a hero with a scripted position, so it does not read back the scripted point (native, `Agent.cs:2653-2656`).
- `TeleportToPosition` onto the post (LEASH) put the hero exactly on it (`maxDriftAfter=0.00`), kept his formation and target, logged no fault, and the pack re-engaged within about 1.8 s.
- A mounted pack held on Charge wheels about 72 m out (median) and returns to the hero. A pack released to Move while it is upstream rides back onto him on its way to the exit.

## Co-op (design, not built)

Mike chose the route below on 2026-11-13 ("proceed with your best recommendations"). Nothing of it is built. The solo slices come
first; the co-op slices start after Slice 14. Coop facts were read in the `bannerlord-1.5.x` working tree of `E:\repos\BannerlordCoop`
(head `e9906952a`) and in [bannerlordcoop-internals](../research/bannerlordcoop-internals.md); none was run.

**Why the mode cannot stay campaign-free in co-op.** BannerlordCoop exists only inside a shared campaign: a client joins by loading
the host's full save, and every co-op mission is a peer-to-peer instance tied to a map event (battles) or a settlement location
(taverns, tournaments). The dedicated server opens no mission; it elects the host and relays membership
(`Missions/Locations/LocationHostHandler.cs`). So co-op Hero Defence opens from a town menu inside a Coop campaign, for every
player in that town. Solo Hero Defence stays in Custom Game.

**Routes considered.**

| Route | Verdict | Reason |
|---|---|---|
| A. A new Coop mission kind, opened from a town | **Chosen** | Coop already replicates agents, movement, formations and blows, and moves the host mid-battle |
| B. Native Bannerlord multiplayer (a custom server game mode) | Rejected for now | Needs multiplayer characters and items, a server-side rewrite of the mission and networked UI; whether TAOM races and Armory content load in multiplayer is untested |
| C. TAOM's own netcode | Rejected | It repeats Coop's agent replication |

**Authority (route A).**

- The elected host owns every creep and runs the run rules: phase, countdown, lives, waves, bounties, purses. Coop's model fits: each
  peer owns the agents it spawned, the others hold `Controller = None` puppets (`Missions/Battles/PuppetSpawner.cs`).
- Each player owns the heroes they bought, so the P takeover needs no change: the player's own game controls those agents.
- A blow is resolved on the attacker's peer with its own models and replayed on the others (`Agent.RegisterBlow`), so TAOM's damage
  hooks stay consistent when every peer runs the same TAOM build. `TAOM.CoopCompat` compares the builds on join and warns; it does
  not block a mismatch.
- Shop actions (buy, place, level, target priority) are requests to the host, which accepts or rejects each one and broadcasts the
  new state. Clients only display it.
- A host handoff mid-wave must carry the run state to the new host, as Coop's `BattleAuthorityMigrator` does for battles.

**Where the code goes.**

- **Coop fork (upstream `bannerlord-1.5.x`, Garrett reviews):** a generic seam for a mod-defined location mission: open a mission at
  a town with an instance kind, elect its host, register its agents. Coop types such as `CoopMissionController` take Coop-internal
  services in their constructor, and Coop finds handlers by namespace scan, so a third-party mission type cannot be added from
  outside today. A generic seam also serves other mods.
- **`TAOM.CoopCompat.Coop.dll`:** the Hero Defence wire contracts (`[ProtoContract]`, never in `TAOM.dll`, ADR-014) and the handlers
  that relay state and shop requests.
- **`TAOM.dll`:** the director split into host rules and a client view of the state. The pure domain does not change.

**Modes, in order.**

1. **Together:** one road, shared lives, one purse per player. Each player buys, places and levels their own heroes; every
   player's bounties go to the player whose hero landed the kill, and the clear bonus is split evenly.
2. **Versus race:** two roads in one scene, one per player, each with its own lives. Kills send extra creeps to the rival's road;
   the last player with lives wins. The host owns both roads' creeps.
3. **Versus attacker:** one player buys the waves, the other defends. Last, because the waves become player-made and need new
   balance work.

**Limits.**

- No game speed above 1x: every peer runs one mission clock.
- The engine agent ceiling (Coop's `IBattleAgentBudget`, `GameInterface/Services/MapEvents/BattleAgentBudget.cs`) and the movement
  traffic for up to 160 creeps plus heroes are unmeasured.
- The co-op scene needs a town entry point, so the town menu option and the scene load are new work in both repos.

**First spike (needs the install and two game clients).** Two players in one town open a test location mission through the
generic seam; the host spawns 160 creeps on Move orders along a road, and each client spawns one hero. Pass: puppets track their
owners within 1 m at the median, a blow on a puppet hero lands once on its owner, a host handoff mid-wave keeps every creep, and the
frame time stays within 1.5x of solo. The seam's shape is decided from that spike.

## Configuration

### Config File: `Main/_Module/ModuleData/hero_defence/waves.json`

| Group | Fields | What it controls |
|-------|--------|------------------|
| `rules` | `startingGold`, `startingLives`, `firstPauseSeconds`, `pauseSeconds`, `maxPlaced`, `earlyGoldPerSecond`, `earlyGoldCap`, `waveBonusBase`, `waveBonusPerWave`, `bountyBase`, `bountyPerLevel`, `bountyMountedPercent`, `packGapSeconds`, `exitRadiusMetres`, `waveLimitBaseSeconds`, `waveLimitPerCreepSeconds`, `minPostGapMetres`, `roadEndClearMetres` | The run's numbers |
| `ranks` | `soldier`, `captain`, `lieutenant`, `general`: `lives`, `bounty`, `health` | Life cost of a leak, bounty of a leader, leader health factor |
| `opening` | culture ids | The pinned first waves: Mordor, then Erebor, also when the key is missing or bad. An id is dropped with a warning when it names no culture, repeats the wave before it, or has no infantry at or below its wave's band |
| `count` | `start`, `perWave`, `max`, `packSize` | Creeps per wave: `min(max, round((start + perWave(w-1)) x countFactor))`, at least 1, in packs |
| `troops` | `perLevel`, `nearestLowerLevels`, `highLevelPercent` | Troops drawn per level, how far a class may fall below its band, the share at the band's top level |
| `bands` | `fromWave`, `low`, `high` | The level band per wave range |
| `growth` | `healthPerWave`, `damagePerWave`, `maxFactor`, `armourPercentPerWave`, `armourPercentMax`, `swingPercentPerWave`, `swingPercentMax`, `gapHealthPerLevel`, `gapDamagePerLevel` | How creeps grow each wave. `maxFactor` caps the per-wave health and damage growth only. When a culture has no infantry row in the band, its infantry falls to the tree's top row, and those packs alone get the gap bonus for each level from that row to the band's high level (`CreepPack.HealthBonus`, `DamageBonus`, added to the wave's factors; no cap) |
| `leaders` | `everyWaves` | A captain, lieutenant, then general every N waves |
| `powers` | `schedule` (`fromWave`, `count`), `pool` | How many creep powers a wave has, and which |

### Config File: `Main/_Module/ModuleData/hero_defence/cultures.json`

`signatures` gives each wave shape its arrival order, `shuffled` flag, `countFactor` and first power. `cultures` gives each culture
its signature, class `mix` (infantry, ranged, cavalry, horse archer weights) and three `leaders` (captain, lieutenant, general).
Leaders are curated by hand, because a buyable hero must never lead a wave, and a famous villain leads only as a general
(`ShippedHeroDefenceDataTests` checks both against named lists). A leader replaces one soldier; in a wave with no infantry pack it
takes one creep from the largest pack and walks in a pack of its own, so the count does not change. Culture ids follow the
cheatsheet: `empire` is Dunland, `vlandia` Rohan, `khuzait` the Easterlings, `aserai` Harad, `sturgia` Dale.

### Config File: `Main/_Module/ModuleData/hero_defence/rosters.json` (generated)

Per culture, every troop a wave can draw: id, level and class (`default_group`). `tools/generate_hero_defence_rosters.py` writes it
from `Main/_Module/ModuleData/troops/troops_*.xml`; run it with `--check` for the current troop and culture counts. Its filters: not
`troops_feral_orcs.xml`, the file's main culture (Shaghana merges into Harad), occupation Soldier, not settlement militia, and an
upgrade edge in or out. Never edit the file by hand: `ShippedHeroDefenceDataTests` and the `hero-defence-rosters` data gate fail on
drift. Regenerate with `python tools/generate_hero_defence_rosters.py --out Main/_Module/ModuleData/hero_defence/rosters.json`.

### Ranges the readers must enforce (Slices 5 and 6)

`HeroLevels`, `PackOrderBand` and `PackStallLimits` have no reader yet, and their records trust their values. The review of
2026-11-09 found these inputs that break a rule; the reader that adds each setting must reject them with a warning:

| Setting | Must be | Else |
|---|---|---|
| `HeroLevels.MaxLevel` | 1 or more | Below 1: every level reads as level 1 and no level can be bought |
| `PriceShare`, `PriceGrowth` | finite, above 0 | Zero, negative or NaN: some or all levels are out of reach. A growth below 1 makes later levels cheaper |
| `HealthPerLevel`, `DamagePerLevel`, `AbilityPerLevel` | finite, 0 or more | A negative share takes the factor below 1, and below 0 for a share under -1/(MaxLevel - 1) (about -0.053 at level 20); a NaN share gives a NaN factor |
| `CooldownPerLevel` | finite, 0 or more | A negative rate makes cooldowns longer than at level 1; a NaN rate gives the 25% floor |
| `PackOrderBand.EngageMetres`, `ReleaseMetres` | finite, release above engage | Equal or inverted bands flap; a NaN release never lets go |
| `PackOrderBand.HeightMetres` | finite, 0 or more | Infinite lets a hero with an infinite height pair; NaN or negative pairs nothing |
| `PackStallLimits.WindowSeconds`, `StepMetres` | finite, above 0 | A zero step counts a standing pack as marching; a NaN window never stalls; a zero window always stalls |

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/HeroDefence/Domain/HeroDefenceRun*.cs` | Run rules, shop, wave flow |
| `Main/Features/HeroDefence/Domain/CreepWavePlan*.cs` | Wave generator |
| `Main/Features/HeroDefence/Domain/PlacementRules.cs` | Placement rules |
| `Main/Features/HeroDefence/Domain/HeroLevels.cs` | Hero level prices and factors |
| `Main/Features/HeroDefence/Domain/PackOrderRule.cs`, `PackStallClock.cs` | Pack Move or Charge, and the stall clock |
| `Main/Features/HeroDefence/Domain/DefenceTypes.cs`, `DefenceRules.cs`, `WaveCatalog.cs` | Shared records and defaults |
| `Main/Features/HeroDefence/Config/*.cs` | JSON readers and the catalog |
| `Main/_Module/ModuleData/hero_defence/*.json` | Data |
| `tools/generate_hero_defence_rosters.py` | Writes `rosters.json` |

## Dependencies

None yet beyond the reused domain types above. The engine side (director, adapters, client) is not built.

## Tests

- `TAOM.Tests/Features/HeroDefence/Domain/`: the run rules (`HeroDefenceRunShopTests`, `HeroDefenceRunWaveTests`,
  `HeroDefenceRunLevelTests`, `HeroDefenceRunStallTests`), the wave generator (`CreepWavePlanTests`), placement
  (`PlacementRulesTests`), the level ladder (`HeroLevelsTests`), the pack rules (`PackOrderRuleTests`, `PackStallClockTests`) and the
  purity gate.
- `TAOM.Tests/Features/HeroDefence/HeroDefenceConfigCatalogTests.cs`: every reader, bad values and missing files.
- `TAOM.Tests/Features/HeroDefence/ShippedHeroDefenceDataTests.cs`: the shipped JSON loads with no warning, and `rosters.json`
  matches the troop trees.
- `tools/tests/test_generate_hero_defence_rosters.py`: the generator's filters and output.
- Run them with `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~HeroDefence"` and
  `python -m unittest tools.tests.test_generate_hero_defence_rosters`.

## How to verify in game

Not reachable yet: nothing registers the mode, so no menu entry, command or log line exists. The first in-game checks arrive with
the scaffold slice (`boot`, `map`, `islands`, `route` through `tools/run_hero_defence.py`), each with a `[Check] id=taom.hero-defence.*`
line and a manifest in `tools/ingame_checks/1106.toml`. Until then the evidence is the unit and shipped-data tests above.

## Changelog

- 2026-11-09: the pure core: run rules, wave generator, config readers, shipped data and the roster generator (plan Slices 1 and 2).
  Review fixes ([RCA](../reviews/rca-hero-defence-s12-2026-11-09.md)): the gap bonus moved onto clamped infantry packs, the pause
  rest carries into the wave, the opening checks entry, waves have at least one creep, leaders keep the count, gold saturates, the
  signature never repeats while another has entered, and the readers reject repeated keys.
- 2026-11-09: hero levels 1 to 20 in the pure run (`HeroLevels`, `TryLevelHero`), the pure part of plan Slice 11. The pack rules
  from spike run 5: `PackOrderRule` (Move or Charge) and `PackStallClock` with `HeroDefenceRun.PackStalled`. Gate 1: the spike
  results of runs 1 to 5. Review fixes ([RCA](../reviews/rca-hero-defence-levels-packs-2026-11-09.md)): one hero order per tick
  with its level, pack ids for stall reports, whole-pack removal on the last life, a stall mark that follows the creep count, and
  no free level from a bad setting.
- 2026-11-09: Gate 1 spike runs 6 and 7 (PIERCE-RANGE, HOLD-DIAG, LEASH, CAV-BLOCK-2, SIX-2): ability arrow thrust, the melee hold
  and its leash, the mounted pack rule and the six-hero layout. The open decisions stay with Mike.

## GitHub Issue

- **Issue:** #1106: Hero Defence
- **Status:** Open
