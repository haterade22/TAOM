# Spider Clip → Binding Map (3 naming layers) — Round 2, Iteration 1 (2026-06-03)

> **CLIP LAYER SUPERSEDED (2026-06-03, later same day).** The `an_dg_spi_*` clips below were the OLD broken-rig set and no longer ship. The spider now uses the TAOM-authored **`an_spi_*`** set: rest-compensated retarget of the `sp_*` takes onto `spider_skeleton`, with the forward walk + run built by the procedural metachronal-wave builder. `action_sets_spider.xml` is bound to `an_spi_*`. **Final bindings:** main idle `an_spi_idle_2` / alt `an_spi_idle`; `walk_forward`→`an_spi_walk_2`; `walk_left`/`right`→`an_spi_walk_left`/`right` (strafes); `run`→`an_spi_run` (never `an_spi_run_2`); jump/turns/attacks/hits/deaths→same-named `an_spi_*`; `attack_bottom` (typo fixed); `hit_right` added; `attack_back`→`attack_front` and `taunt`→`an_spi_idle_2` as fallbacks. Locomotion is in-place. See `docs/features/spider-skeleton-animation-pipeline.md` + CHANGELOG 2026-06-03. The **`sp_*` source-take column below is still accurate** and remains the useful part of this table.

Source-of-truth table connecting the three naming layers that were previously conflated (LESSONS L-naming):
- **`act_spider_*` (TYPE)** — engine action type, declared in `LOTRLOME_Armory/ModuleData/Animations/action_types_spider.xml` (24 types).
- **`an_dg_spi_*` (CLIP)** — animation clip name bound via `animation=` in `action_sets_spider.xml`.
- **source take (`sp_*`)** — the action in the new `ErkamSpider (1).blend` (`sp_skeleton`, 59 bones) that should produce that clip on re-export.

Skeleton binding is now `spider_skeleton` (fixed this session, matches the compiled clips + mesh). Dedup via `find_duplicates` (threshold 0.17, 245 kept channels): **confirmed duplicates** = `sp_attack_front`≡`sp_attack_front_001` (0.0), `sp_attack_bottom`≡`sp_bottom_attack_001` (0.104), `sp_charge_001`≡`sp_charge_002` (0.137).

| `act_spider_*` type | bound `an_dg_spi_*` clip | chosen source take (`sp_*`) | notes |
|---|---|---|---|
| idle | an_dg_spi_idle_01 | sp_idle_1_001 | |
| idle_2 | an_dg_spi_idle_02 | sp_idle_2_001 | |
| idle_dg | an_dg_spi_idle_01 | sp_idle_1_001 | reuses idle_01 |
| walk_forward | an_dg_spi_walk | sp_walk_1_001 | sp_walk_2_001 is a near-twin — pick one |
| walk_left | an_dg_spi_walk_left | sp_walk_left_001 | in-place variant available (`strip_root_motion`) |
| walk_right | an_dg_spi_walk **(FALLBACK)** | sp_walk_right_001 | ⚠ binding currently falls back to fwd-walk. Genuine `sp_walk_right_001` exists in the blend → needs recompile to `an_dg_spi_walk_right`, then rebind. The OLD compiled `spider_walk_right_geo.tpac` holds a **mislabeled `an_dg_spi_turn_right.001`** — do NOT bind to it. |
| run_forward | an_dg_spi_run | sp_run_001 | sp_run_2_001 / sp_run_001.001 are alt takes |
| jump | an_dg_spi_jump | sp_jump_1_001 | |
| turn_left | an_dg_spi_turn_left | sp_turn_left_001 | |
| turn_right | an_dg_spi_turn_right | sp_turn_right_001 | |
| attack_front | an_dg_spi_attack_front | sp_attack_front | **DUP** sp_attack_front_001 (0.0) — drop one |
| attack_back | an_dg_spi_attack_front **(FALLBACK)** | — *(NO SOURCE)* | ⚠ no attack_back anywhere; stays on front fallback until a Cascadeur/mirror candidate is authored |
| attack_left | an_dg_spi_attack_left | sp_attack_left_001 | sp_attack_left_002 alt |
| attack_right | an_dg_spi_attack_right | sp_attack_right_001 | sp_attack_right_002 / _02_002 alts |
| attack_top | an_dg_spi_attack_top | sp_attack_top_001 | _002/_003 alts |
| attack_bottom | an_dg_spi_attack_botom *(typo is Erkam's clip name)* | sp_attack_bottom | **DUP** sp_bottom_attack_001 (0.104) — drop one |
| attack_charge | an_dg_spi_attack_charge | sp_attack_charge | **DUP** sp_charge_001≡sp_charge_002; sp_charge_attack_001 alt — resolve to one |
| hit_front | an_dg_spi_hit_front | sp_hit_front_001 | |
| hit_back | an_dg_spi_hit_back | sp_hit_back_001 | |
| hit_left | an_dg_spi_hit_left | sp_hit_left_001 | |
| death | an_dg_spi_death_01 | sp_death_1_001 | |
| death_2 | an_dg_spi_death_02 | sp_death_2_001 | |
| taunt | an_dg_spi_taunt | — *(NOT in new blend)* | compiled tpac has it; the new `.blend` has **no taunt take**. Keep the existing compiled taunt, or author one. |

**Unbound new-blend content** (no `act_spider_*` type / binding): `sp_hit_right_001` — a real hit-right; could add `act_spider_hit_right` + a binding if wanted.

**Canonical source set** = 34 base actions − 3 confirmed dups = **31 distinct takes**; per act type, the "chosen source take" column resolves which take to export.

## Layer-c verification (R2.2, 2026-06-03) — scan of every compiled spider tpac

**20 distinct compiled clips exist** (`spider_skeleton|an_dg_spi_*`): idle_01, idle_02, walk, walk_left, run, jump, turn_left, turn_right, attack_front, attack_left, attack_right, attack_top, attack_botom, attack_charge, hit_front, hit_back, hit_left, death_01, death_02, taunt.

**RESULT: with the skeleton-name fix applied, every action_set binding resolves to a real compiled clip — NO broken bindings.** The only two bound clips that don't exist as dedicated resources are `an_dg_spi_walk_right` and `an_dg_spi_attack_back`, and the action_set *correctly* falls those back to `an_dg_spi_walk` / `an_dg_spi_attack_front`. So the spider's existing animations should all bind in-game (pending the human test); the skeleton name was the sole blocker.

- `spider_walk_right_geo.tpac` holds the **mislabeled `an_dg_spi_turn_right.001`** (a turn_right duplicate) — confirmed; never bind `act_spider_walk_right` to it.
- `ani_dg_spi_idle_03_geo.tpac` + the `*_anm`/`new_animation_clip*` scratch files + `sk_spider_forest_v2_a_geo.tpac` yield NO `spider_skeleton|` clip — not usable bound clips (idle_03 confirmed empty, consistent with iter 1).
- `an_dg_spi_taunt` IS compiled (from the old generation) even though the new `.blend` has no taunt take.

**True remaining gaps** (each needs a NEWLY-COMPILED clip — human Modding-Kit seam): (1) a genuine `an_dg_spi_walk_right` strafe (source `sp_walk_right_001` exists in the blend); (2) `an_dg_spi_attack_back` (no source — Cascadeur/mirror).

## Resource model (RESOLVED 2026-06-03 — user-confirmed in the Modding Kit)

The previously-UNVERIFIED question ("does the Modding Kit adopt an FBX take name as the clip resource ID?") is resolved. The Modding Kit has **two distinct resource types**, and we'd been conflating them:

| Resource | What it is | Name format |
|---|---|---|
| **Skeleton Animation** | raw FBX motion bound to an **Owner Skeleton** | `<OwnerSkeleton>\|<take>` — e.g. `spider_skeleton\|an_dg_spi_walk`. The `<skeleton>\|` prefix is **added by the Owner-Skeleton assignment in the editor**, NOT stored in the FBX take. The FBX take must be **bare** (`an_dg_spi_walk`). |
| **Animation Clip** | gameplay resource that **references** a skeleton anim ("Animation source") + adds Duration / Sample Rate / blend periods / hand poses / sound / flags | arbitrary (e.g. `spiderclip`) |

So: name FBX takes **bare** (`an_dg_spi_*`); the `spider_skeleton|` comes from Owner Skeleton. Blender export caveat: the **all-actions** FBX path bakes `<armatureObject>|<action>` into the take (`get_blenderID_name((ob,act))`, export_fbx_bin.py:2444) — use the **NLA-strip** path instead (take name = strip name = bare). See L11.

**Skeleton choice (user, 2026-06-03):** bind to **`spider_skeleton`** (the `Assets/creature/spider` set with the full compiled `an_dg_spi_*` clip set), NOT `erkamspider_skeleton` (a separate, earlier resource under `Assets/creatures/spider`). The round-2 `action_sets_spider.xml` edit (`skeleton="spider_skeleton"`) is therefore correct as-is. See `spider-rig-generations.md`.

**Clean drop-in FBX:** `E:\LOTRAOMAssets\_auto_workspace\spider_an_dg_spi_clips.fbx` — 21 bare-named `an_dg_spi_*` takes, dedup applied, motion verified distinct (re-import differential prefix check + per-clip frame-range/valsum). Mixed-case bones (case-fold by the importer assumed — verify in editor).

**No further action_set edits are warranted right now** — all bindings are valid; the walk_right/attack_back rebinds must wait until their genuine clips are compiled (else they'd point at non-existent clips).
