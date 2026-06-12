# Creature Mount Authoring — the complete workflow

How to take a non-humanoid creature from raw assets to a **rideable, battle-stable mount** in
TAOM on Bannerlord 1.4.6. Distilled from the two campaigns that proved every step the hard way:
the **war elephant** (2026-06-03 → 06-10, ADOD port) and the **giant spider**
(2026-06-04 → 06-12, custom skeleton + custom clips — the maximal case). The warg
(Alliance.Wargs) is the always-on reference implementation: when in doubt, do what the warg does.

**Architecture in one line:** a creature mount is a vanilla cavalry spawn — a rider TROOP with a
Horse-slot ITEM whose `HorseComponent` names a `Monster`; the engine does all mount work
(movement, blows, deaths, rider seating) off that Monster's data surfaces, and TAOM layers
attacks on top with a per-agent behavior tree. **No spawn patches, no detached combatants** —
that architecture was built twice and deleted twice (see spider.md "the three architectures").

Companion docs: [spider.md](../features/spider.md) (architecture + every RCA),
[elephant.md](../features/elephant.md), [spider-skeleton-animation-pipeline.md](../features/spider-skeleton-animation-pipeline.md)
(asset pipeline), [lotrlome-spider-mount-changes.md](../reference/lotrlome-spider-mount-changes.md)
(external-module change ledger), [mount-and-rider-runtime.md](../reference/engine/mount-and-rider-runtime.md)
(engine internals).

---

## Phase 0 — Assets (skeleton, meshes, physics)

| Requirement | Rule | Failure mode if violated |
|---|---|---|
| Skeleton bone count | ≤ 64 bones total (engine cap) | import failure / corrupt rig |
| Per-mesh bone palette | ≤ ~38-40 bones per mesh — **split large rigs into L/R half-meshes**, recombine via the item's `<AdditionalMeshes>` (warg precedent: body + fur) | `Agent.PreloadForRendering` AV at spawn (spider 2026-06-05) |
| Physics | bodies + joints transplanted onto the final skeleton in the same tpac | ragdoll-less corpses, collision holes |
| Movement clips | authored **IN-PLACE** (zero net root travel) — the engine translates the agent | double-speed slide if root motion baked (verified vs warg 2026-06-03) |

## Phase 1 — Animation clips (the Kit, or the byte-patch emergency path)

**THE rule that cost a 6-hour RCA:** every **movement/gait clip** (walks, runs, turns-in-motion,
jump) compiled for a `movement_system="quadrupedal"` action set MUST carry the
**`quad_movement` Clip *usage*** (bottom of the Kit clip panel, below Flags — it is NOT a Flag)
plus **step points**, and gait sound needs the `make_walk_sound` Flag. An untagged gait clip
builds a null native gait structure → AV (`+0x10`) on the FIRST `Skeleton.TickAnimations` in
EVERY mount context: thumbnail, inventory tableau, mission. Detached (non-mount) spawns never
trip it — which is why the bug hides until the creature becomes a mount.

- Run clips serving the gallop pace also need the **`cyclic`** flag (the spider's run regraft).
- Non-gait clips (attacks, deaths, hits) must NOT carry `quad_movement`; their polish flags are
  per-category (attack: `client_prediction, lock_movement, enforce_all`; death:
  `make_bodyfall_sound, client_prediction, do_not_keep_track_of_sound, enforce_all,
  update_bounding_volume`; rear: `lock_movement, enforce_lowerbody`) — full Kit field map in
  spider-skeleton-animation-pipeline.md §3c.
- **Durable path:** set these in the Modding Kit and recompile. **Emergency path:** the
  byte-patch template graft (spider.md "How-to") — structurally validated clean by
  `tools/_scratch`-class verifiers on 2026-06-12, but treat it as a stopgap; recompile when the
  Kit is open anyway.
- tpac filename ≠ resource name. Validate every binding target against the **parsed resource
  inventory** (byte-scan the tpacs), never by name plausibility — a binding to a nonexistent
  animation compiles into a **degenerate record** that AVs when dereferenced (the
  `an_spi_attack_back` phantom, 2026-06-11). Sweep script pattern: scan candidate names against
  the module's tpacs + Alliance.Wargs packs + vanilla `Native/.../animation_clips.tpac`.

## Phase 2 — Monster XML (`Monsters/<file>.xml`, engine-registered via SubModule `<Xmls>`)

Copy the warg's shape (`Alliance.Wargs/ModuleData/Monsters/LOTR/lotr_monster_warg.xml`), then:

| Field | Value | Why (campaign evidence) |
|---|---|---|
| `num_paces` | **6** | every mountable monster is 6; the mount machinery indexes the gallop pace (5) |
| `family_type` | **1** (horse family) | family 1 carries vanilla's complete rider-death / dismount / rider-fall surface. The spider tried 11 — no such surface exists for it. Harness isolation is done in C# (mount-lock), not via family |
| `<Flags>` | **EXACTLY** `Mountable CanRear RunsAwayWhenHit CanCharge CanWander` | **`CanAttack` is forbidden on a mountable monster** — it activates the engine's own attack-AI (`Agent_ai::set_attack_entity`), a code path NO working mount takes, and the site of the 1.4.6 charge-CTD (null `agent+0xAD8`, Event-Log-proven 2026-06-12). The BT bite/trample does NOT need it — the warg proves it. No other extras: no baseline mount declares any |
| Rein surface | `rein_handle_bone` + `rein_handle_left/right_local_pos` + `rein_collision_1/2_bone` | every working mount declares one (warg-minimal is fine) |
| Rider adders | `rider_eye_height_adder`, `rider_body_capsule_height_adder`, `rider_body_capsule_forward_adder` | warg + vanilla horse parity (camera + rider capsule) |
| `rider_sit_bone` | a spine/chest bone | seats the rider; `rider_camera_height_adder` tunes camera |
| Slope block | `front/back_bone_to_detect_ground_slope_index` + `bones_to_modify_on_sloping_ground_*` | quadruped slope pitch (indices verified vs decompiled `MonsterExtensions`) |
| `sound_and_collision_info_class` | an existing class (`horse`) or author your own collision_infos (warg does) | footstep sounds/particles only — cosmetic |

## Phase 3 — action_types (typed verb surface)

The native dispatch is **TYPE-driven**. Declare, exactly:

| Action | Type | Notes |
|---|---|---|
| 12 falls: `act_<c>_fall_{right,left,roll,backwards,slow_right,slow_left}` + `_continue` siblings | `actt_fall` | the blow handler chains `<fall>_continue` on downed mounts; missing/untyped = `HandleBlowAux` AV |
| `act_<c>_rear`, `act_<c>_rear_damaged` | `actt_rear` | fires when the mount takes damage |
| `act_<c>_dash` | `actt_dash` | |
| `act_<c>_kick` | `actt_kick` | |
| `act_<c>_quick_stop`, `act_<c>_quick_stop_when_fast` | `actt_mount_quick_stop` | wire BOTH usage-set slots to these (the spider had an untyped idle in `quick_stop_action` — same lookup-miss class as the jump bug) |
| `act_<c>_hit_object`, `act_<c>_hit_object_while_falling` | `actt_hit_object` | |
| `act_<c>_strike_front`, `act_<c>_strike_back` | `actt_mount_strike` | the HEAVY trample rows |
| `act_<c>_strike_front_while_moving`, `act_<c>_strike_back_while_moving` | **UNTYPED** | the LIGHT (`is_heavy=False`) strike rows — warg-exact shape |
| `act_<c>_idle_1` | `actt_idle` | the upper-body pace-1/front row's dedicated typed idle (warg's `act_warg_idle_1`) |
| **`act_<c>_jump`** (the usage-set `jump_start_action`) | **`actt_dash` — NEVER `actt_jump`** | warg AND elephant both type it dash. A jump-TYPED action absent from the jump ROWS misses `jump_actions_map_` — tolerated on 1.4.5, **AV on 1.4.6** |
| movement/idle/attack/hit/death/taunt actions | untyped | bindings + BT use them |

## Phase 4 — action_sets

1. **The creature's set** (`as_<c>`, `skeleton=`, `movement_system="quadrupedal"`): bind EVERY
   action the usage set references (verbs, falls, strikes incl. while-moving, jumps via the
   global `act_horse_jump_*` family, movements, idles incl. `_1`, turns) to a **real, validated**
   clip. Also bind `act_horse_forward_canter` explicitly — the thumbnail/tableau pose resolves it
   (`as_warg` does the same).
2. **Children**: `as_<c>_town_and_village` + `as_<c>_map` (`base_set="as_<c>"`) — campaign map
   icons + settlement scenes resolve them; missing child = native AV (elephant "Crash #4" class).
3. **The rider partial**: a partial `<action_set id="as_human_warrior">` block mapping every
   creature usage-set action to a RIDER overlay clip (`rider_warg_*` + vanilla `rider_fall_*`).
   **It MUST sit at the TOP of the file** — `base_set` inheritance snapshots at definition time,
   so the partial must precede every race set or riders inherit nothing (the goblin
   "thrust-loop", 2026-06-11). The engine merges same-id sets across modules
   (Alliance.Wargs' own file leads with exactly such a partial).
4. **Registration truth:** native animation XML loads ONLY via `project.mbproj` `<file>` entries
   with the standard ids (`soln_action_sets` / `soln_action_types` / `soln_monster_usage_sets`)
   → the module-root `action_sets.xml` / `action_types.xml` / `monster_usage_sets.xml` are live;
   subfolder copies are superseded decoys. SubModule.xml `<Xmls>` is managed types only.

## Phase 5 — monster_usage_sets (the lookup tables the engine CRASHES on)

> **The 1.4.6 hardening principle, learned three times in one day: the engine's usage-record
> lookups are hash maps whose miss path dereferences the end-sentinel (asserts compiled out of
> shipping builds). A MISSING key crashes; an EXTRA row is inert. Make every table TOTAL over
> the key combinations the engine can produce.** 1.4.5 tolerated misses; 1.4.6 does not.

The set element: all **10 verb attributes** wired to the Phase-3 typed actions
(`rear`, `rear_damaged`, `dash`, `kick`, `fast_quick_stop` → `_when_fast`, `quick_stop`,
`gallop_acceleration_head` (untyped idle is fine — horse/elephant parity), `hit_object`,
`hit_object_falling`, `jump_start` → the `actt_dash`-typed jump). A verb-less set on a Mountable
monster leaves null native entries → spawn AV.

| Table | Coverage rule | Evidence |
|---|---|---|
| `monster_usage_movements` | every pace 0..5 × directions front/right/left/none, `turn_direction` rows at pace 1, **both `is_left_foot` variants at pace 5**; EVERY pace needs its `direction="none" turn_direction="none"` reference row | missing per-pace reference = native ÷0 at CreateAgent (2026-06-04) |
| `monster_usage_upper_body_movements` | warg shape + pace-1 front/back/none rows; pace-1/front uses the typed `_idle_1` | horse/elephant parity |
| `monster_usage_movement_adders` | the global `act_horse_rider_*_adder` rows (they animate the RIDER) — full pace × direction matrix incl. left-foot pace 5 | rider bob/sway |
| `monster_usage_jumps` | **ALL NINE directions** (`front`, `front_left`, `front_right`, `none`, `left`, `right`, `back`, `back_left`, `back_right`) × (start + loop×is_hard + end×is_hard) = **45 rows**. Vanilla/warg ship only front+none — and vanilla riders never produce directional jump queries, but BT-driven creatures turning mid-jump DO | the 1.4.6 riverbank crash: `monster_usage.cpp` sentinel deref (offset `0x634396`), spider caught mid `act_horse_jump_high_loop` with corrupted record memory |
| `monster_usage_falls` | warg-exact 9 rows: heavy front roll; light × (right/left/front/back) × death_type roll/other — on the TYPED fall codes | knockdown machinery |
| `monster_usage_strikes` | warg-exact 4 rows: heavy front/back on TYPED `actt_mount_strike` codes; light front/back on the UNTYPED `_while_moving` codes | trample dispatch |

## Phase 6 — Item + troop + recruitment

- Horse-slot ITEM (`ItemType.Horse`) with `HorseComponent monster="<monster_id>"`; split meshes
  recombined via `<AdditionalMeshes>`; `horse_harness` optional (family-1 fits horse harnesses —
  the player-side mount-lock prevents abuse, Phase 7).
- Rider TROOP equips the item in the Horse slot → vanilla cavalry spawn does everything else.
- Recruitment via the standard pools (`VolunteerRecruitmentService`) or clan-restricted
  (`harad_elephant_rider` precedent).

## Phase 7 — C# (the TAOM layer)

| Piece | Pattern | Notes |
|---|---|---|
| `<C>MissionBehavior : MissionLogic` | elephant wiring verbatim: `BTRegister.RegisterClass` in lazy `Initialize()`, first-tick scan + `OnAgentBuild` late-attach keyed on **`Monster.StringId`** (NEVER character id — the mount agent's Character is the RIDER), dedup shadow list, dead pruning, error-dedup logging | custom-battle deployment spawns AFTER the first tick — late-attach is the main path |
| `<C>BehaviorTree` | elephant shell (has-rider → ai-controlled → attack gate → task + SleepTask pacing; player-ridden and riderless branches sleep) + `On<C>Died` listener (warg parity); `base(10)` ctor (NOT a throttle — int division truncates <1000 to 0) | the BT layers attacks ON TOP of engine mount AI; it needs no Monster flags |
| Attack service | pure, TaleWorlds-free (`ShouldEngage` / cooldowns / damage), boundary nodes hold the raw `Agent` | warg-pattern rider damage attribution |
| Eager `ActionIndexCache` + `AnyUnresolved()` drift guard | resolve attack clips at mission start; log if any → `act_none` | Armory rename = silent `act_none` = the "slide" |
| Mount-lock | `TaomAgentStatCalculateModel`: `CanAgentRideMount=false` + `MountDifficulty=999` for the monster id | players can't steal the mount; the assigned rider's cavalry spawn ignores it |
| **Patch47 `Agent_Die_SpiderDismount_Patch`** | Prefix on `Agent.Die`: a rider dying on the creature is hard-dismounted via cached private `SetMountAgent(null)` → dies the proven on-foot death; a dying creature frees its rider first | the 1.4.6 native mounted-death path still AVs on (at least melee) deaths — Die-path read of float-bits-as-index from a corrupted action record (debugger-proven 2026-06-12). Patch47 routes around it and was wrongly indicted once (the charge crash fired without it too); it is REQUIRED |
| `SpawnMountLogged` (HeroRace) | instrumented replica of the engine's private `SpawnMount` with graceful mount-skip | keep; demote to LogDebug at ship |

## Phase 8 — Validation instruments (run them ALL before battle-testing)

| Instrument | What it catches |
|---|---|
| `python tools/audit_mount_parity.py` | ANY divergence from warg/elephant/horse across Monster XML, usage sets, action types, action-set binding coverage, rider partial — the tool that found 5 of the spider's 7 deltas in one pass. **Extend its `FILES`/`MOUNTS` maps for each new creature** |
| Animation-target sweep | every `animation=` in your sets byte-scanned against the real tpac inventories (module + Alliance.Wargs packs + vanilla `animation_clips.tpac`) — phantoms compile into degenerate records |
| The probe battery (`RunSpiderMountDiagnostics` pattern) | one-shot in-game probes: skeleton × set × usage spawn tests, engine-binding name resolution (compiled truth vs XML-on-disk), rider-partial inheritance per race set |
| Control battles | warg-only (the known-good baseline), then the new creature; on a crash, bisect single-variable — and expect ≥2 independent causes (the spider had FIVE) |
| Native crash triage | Event Log fault offsets (no debugger needed) discriminate crash sites across runs; VS mixed-mode (Managed .NET Framework + Native) gives faulting registers; pdata function bounds + rip-relative string scan + E8 caller-chain naming — the technique that named `set_attack_entity` and `monster_usage.cpp` |

## The engine-bump protocol (what 1.4.5 → 1.4.6 taught)

Steam force-bumped the engine mid-campaign (2026-06-11 17:39) and three latent data quirks became
CTDs because the rewritten native lookups stopped tolerating misses. After ANY engine bump:
run `/verify-bindings`, re-run the parity audit + control battles, and check the Event Log fault
offsets against the previous version's known sites before assuming your last change caused a
crash. Keep the previous decompile as `_shipping_build_vX.Y.Z` (the 1.4.5 baseline lives at
`E:\Decompiled_Bannerlord\_shipping_build_v1.4.5`) — managed diffs scope the blast radius fast
(1.4.6: combat assembly byte-identical; all changes native-internal).

## The complete gotcha index (chronological, both campaigns)

1. Non-humanoid mounts ARE viable — the "never make creatures mounts" verdict was reversed
   2026-06-11 (`quad_movement` was the real killer, not the mount architecture).
2. Per-mesh bone palette → split meshes + `<AdditionalMeshes>` (spider 06-05).
3. Per-pace `direction="none"` reference row or ÷0 (spider 06-04).
4. `quad_movement` + step points on gait clips or universal mount-context AV (THE root cause,
   06-10/11).
5. Run clips on the gallop pace need `cyclic` (mid-charge CTD).
6. Typed falls WITH `_continue` siblings + typed verbs or blow-path AVs (06-11).
7. Binding targets must exist — phantom = degenerate record = delayed AV (06-11).
8. Rider partial at the TOP of the file or race sets inherit nothing (06-11).
9. `family_type=1` for the vanilla rider surface (06-11).
10. NO `CanAttack` on mountable monsters — engine attack-AI path, 1.4.6 CTD (06-12).
11. `jump_start_action` typed `actt_dash`, never `actt_jump` (06-12).
12. Jump tables TOTAL over all 9 directions — BT creatures jump sideways (06-12).
13. Quick-stop slots wired to the typed codes, not an idle (06-12).
14. Light strikes on dedicated UNTYPED `_while_moving` codes (06-12).
15. Patch47 dismount-before-death stays — the 1.4.6 native mounted-death path still corrupts
    (06-12, melee-thrust repro).
16. A missing lookup key CRASHES on 1.4.6; an extra row is inert. When in doubt, add rows.
