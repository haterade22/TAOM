# The great gate of Erebor: research, decisions and build record

Started 2026-11-26 in dedicated sibling worktree `E:\repos\taom-erebor-gate`, branch `feat/erebor-gate`, branched from
`bannerlord-1.5.x` (`550ae504c`). Work folder: `E:\LOTRAOMAssets\_auto_workspace\kingdom_kits\erebor_gate`.
Nothing in `E:\repos\taom-kingdom-kits`, `TAOM_Map`, or any other session's folder is modified by this job.

## Scope and ownership

- Goal: build a 1:1 siegeable Great Gate of Erebor in Blender (one prefab placed once in the editor, with the mountain
  integrated, working outer and inner gatehouse, two 20 m breachable wall sections with solid and broken states,
  12.0 m fightable wall walks, defender catapult platforms, and master-level stone carving).
- Initial handover brief: [erebor-gate-handover.md](erebor-gate-handover.md).
- Work claim check: `python E:\repos\TAOM\tools\work_claims.py check erebor-gate` returned `free: erebor-gate`
  (non-Claude client fallback per `AGENTS.md`).
- Public tracking issue: owed owner decision (new issue or part of #1600). No public issue created without owner word.
- Tripo CLI balance: started at `610` credits (`0` frozen); `250` credits spent across five v3.1 text-to-model sculpts
  (50 credits each: battleaxe guardian, breach rubble, granite cliff, warhammer guardian, and standing dwarf king sentinel;
  zero reference photos uploaded, every job dry-run verified first); `360` credits remaining (`tripo balance --json`).

## Lessons reviewed from other kit sessions (Claude, Codex, Kimi)

Before starting any geometry, this session reviewed the Rivendell, Lothlorien, Mirkwood, Gondor, and Erebor records:

- **Rivendell (Claude, `rivendell-siege-set.md`, `teardowns/`, `build-tooling-workflow.md`):** exact 12.0 m siege deck
  for 14 m ladders and 12 m towers; 15-entity breach wall tree; one asset per placed piece (railings inside stairs);
  shared Blender lock (`rivendell\_tools\blender_one.py`) with 16 GB per-job ceiling; never judge solely against rules
  without checking against the owner's reference; exact skeleton by code and organic skin/rubble from Tripo; check
  back-face culling because Blender renders two-sided while the engine culls back faces.
- **Lothlorien (Codex/Kimi, `lothlorien-lessons.md`, `lothlorien-kit.md`):** sample palette from documented clean
  crops (L4); validate all arithmetic and placed world-space coordinates in Python tests before Blender runs (L11,
  L12); use `Matrix.Translation` instead of `bmesh.ops.translate` (L16); strip collinear points with `clean_loop`
  before extrusion to prevent zero-area faces (L17); select scale figures by role and Z rather than Blender `.001`
  suffixes (L20); hide viewport helpers and oversized ground planes in the owner's `VIEW_REVIEW.blend` copy and prove
  the live Blender window with a capture (L22).
- **Mirkwood (Codex/Kimi, `mirkwood/lessons.md`, `mirkwood/siege-controls.md`):** check full operating envelopes around
  defender catapults (5.50 x 5.72 m footprint plus 120 degree arc and ammo access) and gate swing pockets separately
  from visible arch throats; keep art direction and stone craft grounded in references before finalizing siege kits.
- **Erebor and Gondor controls (`erebor/meshes.md`, `erebor/design-patterns.md`, `rivendell/control/control_geometry.md`):**
  Erebor existing castle wall (`SM_DW_Castle_Wall_A1_Str`, 20 x 6.2 x 12 m, walk Z = 6 m, 864 tris), gate wall
  (`SM_DW_Castle_Wall_A1_Gate`, 20 x 6.37 x 15.09 m, 6,752 tris, passage 7.67 x 6.21 m), statues
  (`SD_DW_IH_Statue_A/B`, ~28.3k tris LOD0, unit scale), and Gondor control wall (`gondor_castle_wall_20m_l3_a`,
  25,516 tris with merlons; `_dest` 23,084 tris, 12.5 m breach gap; gatehouse `gondor_castle_gatehouse_l1_a`, 18,260
  tris, 5.75 m passage).

## Measured live module baseline (2026-11-26, read only)

Script: `erebor_gate/_tools/measure_refs.py`, output: `control/ref_and_control_measurements.json`.

- **Settlements:** `town_E1` in `TAOM_Map/ModuleData/settlements.xml` (line 8926) is **Erebor** (`Culture.erebor`,
  currently loading vanilla `sturgia_town_a`). `town_E2` Jarnfast, `town_E3` Skarhald, `town_E4` Azanulibar-dum.
- **Dwarf scenes in `TAOM_Map/SceneObj/`:**
  - `taom_dwarf_town_01_forceatmo`: working siege layout with 6,317 prefabs, 2 sided breach walls (`left` at X 392.35,
    `right` at X 472.29, spacing ~80 m), `empire_castle_gate_outer_l3` (X 432.56, Y 907.91) and
    `empire_castle_gate_inner_l3` (X 432.55, Y 913.39, gap 5.48 m, both non-uniformly scaled), 4 defender
    `DeploymentPoint`s, 4 x 7 m ladders, 2 x 9 m towers, 1 ram.
  - `taom_haterade_erebor_town_z`: owner's WIP scene (69 prefabs, no `navmesh.bin` yet), placing `dwarf_gate_a`,
    `sturgia_castle_gate_outer_l3`, two `european_castle_gatehouse_portcullis_a` at +-21 m from center, and **two
    `siege_tower_12m_spawner`** (confirming the Z = 12.0 m target siege deck height).
- **Prefab entity budget across all 5 loaded modules (`check_prefab_budget.py` and `verify_staged_delivery.py`):**
  - `TAOM_Map`: 98,104 | `Native`: 23,828 | `SandBox`: 2,889 | `StoryMode`: 39 | `LOTRLOME_Armory`: 19.
  - **Live total: 124,879 / 131,072 (6,193 entities spare).**
  - Built Erebor gate prefab (`taom_erebor_great_gate.xml`) adds **48 `game_entity` tags** (1 root + 7 bounding-sphere-split
    fixed entities + 30 for two 15-entity breach wall trees + 8 for four gatehouse merlons and debris holders + 2 stairs with
    integrated railings; 0 nested gates) -> **124,927 / 131,072 (6,145 entities spare)**.
- **Existing Erebor textures:** `t_dw_wall_block_a1_d.png` (`#57514a`) and `t_dw_trim_a1_d.png` (`#5c564f`) are brown-grey,
  whereas the owner's Erebor gate references (`ref_01`, `ref_03`, `ref_04`, `ref_06`, `ref_09`, `ref_10`) show a
  verdigris green-grey carved facade contrasting against brown-grey mountain rock (`#362f2b`).

## Round 1: design language and true-geometry massing (complete, awaiting owner pick)

- Design language: [erebor-gate-design-language.md](erebor-gate-design-language.md).
- Pure-Python TDD suite (`kit_full/src/tests/`): **22/22 passed** (`test_erebor_gate_spec.py`,
  `test_erebor_profiles.py`, `test_make_massing_sheet.py`), including **9/9 planted-fault checks** (narrow outer gate,
  short gate spacing, deck too high for 12 m tower, blocked ram approach, wrong breach wall count, steep breach mound,
  undersized catapult pad, asymmetric statues, and disconnected walk route).
- Headless Blender build (`kit_full/src/build_erebor_gate_massing_r1.py`, run through shared
  `rivendell/_tools/blender_one.py`, GPU Cycles, 90 s, 31 GB RAM free at exit):
  - **Planted-fault Blender self-test:** `catches_non_manifold: true`, `catches_inverted_volume: true`,
    `catches_asymmetry: true`.
  - **Variant A (148 x 68 x 138 m, facade 66 x 108 m):** 44 objects, 34,016 tris, 41 prefab entities, 0 non-manifold
    edges, 0 zero-area faces, 0 inverted volumes, 0.000000 m mirror error (`craft_pass: true`).
  - **Variant B (126 x 58 x 96 m, facade 60 x 76 m):** 48 objects, 35,824 tris, 41 prefab entities, 0 non-manifold
    edges, 0 zero-area faces, 0 inverted volumes, 0.000001 m mirror error (`craft_pass: true`).
  - **Variant C (136 x 78 x 120 m, facade 64 x 94 m):** 44 objects, 34,284 tris, 41 prefab entities, 0 non-manifold
    edges, 0 zero-area faces, 0 inverted volumes, 0.000000 m mirror error (`craft_pass: true`).
- Visual and QA outputs (all inspected before presentation; backface-culled Workbench checks against magenta sky show
  zero missing back faces or seam leaks):
  - Equal-finish 6-view comparison sheet (`2820 x 4109` px):
    `E:\LOTRAOMAssets\_auto_workspace\kingdom_kits\erebor_gate\massing\r1\S_erebor_gate_massing_r1.png`
  - Shared-scale side-by-side overview (A left, B center, C right):
    `E:\LOTRAOMAssets\_auto_workspace\kingdom_kits\erebor_gate\massing\r1\renders\all_three_overview.png`
  - Lean review page (generated via `page_maker.py`, 7,557 bytes):
    `E:\LOTRAOMAssets\_auto_workspace\kingdom_kits\erebor_gate\review_now.html`
  - Owner review `.blend` (launched in Blender GUI PID `37540`; `capture_blender_windows.ps1` reported `no main window`
    across all four interactive Blender GUI processes from this shell session, so no viewport capture PNG was produced
    and Blender was not relaunched):
    `E:\LOTRAOMAssets\_auto_workspace\kingdom_kits\erebor_gate\view\erebor_gate_massing_r1_VIEW_REVIEW.blend`
- Lessons caught and fixed during Round 1 iteration:
  1. Replaced separate vertical cliff monolith columns with one continuous closed-manifold sculpted alpine mountain peak
     wrapping an arch-shaped canyon so zero sky holes appear beside the crown.
  2. Built symmetric carved wings and outer bastions on `+X` and mirrored via `mirror_bm_x()` to reach `<= 0.000001 m`
     mirror error.
  3. Replaced `bmesh.ops.delete` on sliver faces in `finalize_bm` with topological `dissolve_degenerate`, removed
     redundant front beveling on the 3-hole lower relief plate, and disabled cross-block seam welding in `merge_bms` so
     all three variants hit 0 non-manifold edges.
  4. Split the mountain into its own `V*_Mountain_Peak` collection so rear-interior and top-plan renders cleanly expose
     the inner gate, sunlit breach rubble, two 12 m stairs, murder hole, and four 120 degree catapult arcs.

## Phase 2: 1K engine textures, carving proof, and Tripo sculpts (complete)

After owner authorization to continue overnight, Phase 2 built the full 1024 px material library and organic Tripo sculpts
while keeping all architectural geometry parametric and exact.

### 2A. 6-strip Cycles-baked trim sheet and 4 core 1K texture sets

- Scripts: `kit_full/src/textures/erebor_trim_strips.py`, `bake_erebor_trim_highpoly.py`, `compose_erebor_textures.py`,
  `build_carving_test_panel.py`, `kit_full/src/pure_png.py`.
- High-poly trim bake (`622,592` tris, Cycles GPU): `bake_vs_analytic_mm_mean: 0.2089 mm`, `ao_mean: 0.9534`. Initial run
  caught a +18.75% Y-stretch (`5.31 mm` error) caused by passing apron-padded width for Y; splitting `width_m` and `height_m`
  removed the stretch.
- Core 1024 px 8-bit RGB Bannerlord sets (`_d` diffuse, `_n` DirectX green-down, `_s` R=metallic / G=gloss / B=AO, `_h`
  height) under `textures/sets_1k/`:
  - `t_erebor_gate_trim_a`: 6 strips (`S1_ashlar`, `S2_merlon` bronze tablet, `S3_cornice` dentils, `S4_arch` triple bevel,
    `S5_lattice` diamond with bronze studs, `S6_runes` V-cut bronze runes; `u_seam_ratio: 0.986`).
  - `t_erebor_gate_stone_a`: verdigris dressed ashlar (`2.222 m` tile = `460.8 px/m`, mean sRGB `[102.2, 117.8, 94.7]`).
  - `t_erebor_gate_pavement_a`: warm chamfered flagstones (mean sRGB `[138.4, 133.4, 125.1]`).
  - `t_erebor_gate_bronze_a`: brushed antique dwarven bronze with patina (metallic mean `217.8 / 255`).
- Master carving test panel (`design/carving_panel/S_carving_test_panel.png`, `2,768` tris, `460.8 px/m`): visually audited
  at 2.5 m, 8 m, 30 m, and backface-culled magenta QA (zero leaks).

### 2B and 2C. Four Tripo v3.1 sculpts and watertight preparation

All four jobs ran via `tripo make` with generic text prompts (zero reference photos uploaded), `--dry-run --json` verified
first, `TRIPO_ANALYTICS_ENABLED=0`, `TRIPO_CLIENT_APP=claude`, and every downloaded GLB verified byte-exact against header
bytes 8 to 12. Full job rows are logged in [tripo-cli.md](../features/tripo-cli.md).

- **Twin 30 m kneeling guardians** (`48abbdfe` left battleaxe, `54c30180` right warhammer; 50 credits each):
  - Raw Tripo GLBs (~1.9M tris, facing `+X`) tore flipped-face holes under direct collapse decimation. Running a watertight
    `VOXEL` remesh (`0.085 m`) BEFORE decimation to LOD0 (`18,000` tris), `.lod3` (`4,500` tris), `.lod6` (`1,200` tris),
    and `bo_` convex hulls (`312` / `332` tris), then baking from the original 1.9M-tri sculpt with anti-black-ray fallback
    clamping produced `watertight_pass: true` on both statues (`0` non-manifold edges, `0` zero-area faces, `0` magenta leaks).
  - `harmonize_statue_textures.py` color-graded both statue diffuse maps with AO-modulated verdigris stone patina
    (axe `[103.8, 116.4, 93.9]`, hammer `[102.7, 115.4, 93.0]` vs facade stone `[102.2, 117.8, 94.7]`) so both guardians
    match the carved facade stone as in `ref_01` and `ref_04`.
- **Breach rubble and alpine cliff rock** (`53b0c86c` rubble, `a492d162` granite cliff; 50 credits each):
  - Orthographic Cycles baking + raised-cosine seamless quilting produced 1K engine maps (`t_erebor_gate_rubble_a`,
    `t_erebor_gate_cliff_a`) and `256x256` float32 displacement heightfields (`tripo/prepped/rubble_cliff/*.npy`).

## Phase 3: master 1:1 hero build, LODs, FBX round-trip, and staged delivery (complete)

Scripts: `kit_full/src/hero/` (`erebor_hero_spec.py`, `erebor_hero_materials.py`, `erebor_hero_builders.py`,
`build_erebor_gate_hero.py`, `generate_erebor_prefab_xml.py`, `stitch_hero_sheets.py`, `render_lod_comparison.py`,
`verify_fbx_roundtrip_blender.py`, `verify_staged_delivery.py`).

- **Pure-Python TDD suite (`kit_full/src/tests/`):** **32/32 tests GREEN** (including 16 planted-fault checks across
  massing spec, hero spec, DirectX normal sign, prefab XML tree, U-arch frame loop area, and statue verdigris grading).
- **Geometry, LODs, and physics (`hero/v1/hero_v1_result.json`, Hero v4.2):** `overall_pass: true`.
  - Bounding-sphere-split layout: 29 LOD0 objects (`132,020` tris total; `115,448` tris rendered in intact state), 29 `.lod3`
    (30 m switch, `49,772` tris; `43,476` intact), 29 `.lod6` (130 m switch, `19,146` tris; `16,828` intact), 29 `bo_`
    collision bodies (`4,404` tris), plus optional broken/tilted right warhammer statue (`erebor_gate_statue_r_broken` +
    `bo_erebor_gate_statue_r_broken`).
  - Craft gates: `0` non-manifold edges, `0` zero-area faces, `0` inverted volumes, `0.000000 m` bilateral mirror error on
    all symmetric architectural entities (`erebor_gate_lower_gatehouse`, `erebor_gate_facade_mid`, `erebor_gate_facade_crown`).
  - BVH raycast and physics probes: `0 / 132` backface hits from attacker, wall-walk, breach, and rear courtyard viewpoints;
    `bo_gate_tunnel_clear: true` (`Y = +30 m` through outer gate, kill chamber, inner gate to courtyard `Y = -14 m`);
    `bo_murder_hole_clear: true`; `bo_4_catapult_120deg_arcs_clear: true` (all 20 rays across all 4 pads clear);
    `planted_fault_catches_blocked_gate: true`.
- **Upgrades and defects caught across Hero v1 to v4.2 iterations:**
  1. Split `erebor_gate_mountain_flanks` into left and right lower cliff cheeks (`|X| >= 39.2 m`) plus an upper canyon backing
     mantle (`Z >= 20.0 m`) so the entire `78.4 m x 20.0 m` rear interior courtyard stays open to the mountain town (fixing a
     v2 defect where the lower mountain blocked the inner gate exit and generated 35 backface hits).
  2. Shifted outer bastions forward (`Y = 17.2 m`, catapults at `Y = 13.8 m`) with 3-step cantilevered 45 degree corbel
     flares (`ref_09`) and recessed statues into smoothstep-carved cliff niches (`X = +-52.5 m, Y = 13.0 m`) so all four
     120 degree catapult cones clear every statue, cliff cheek, and merlon.
  3. Replaced primitive base-wall sentinel figures with a fifth watertight Tripo v3.1 sculpt (`47405754`, standing armored
     bearded dwarf king sentinel, `1,100` tris LOD0, `0` non-manifold edges, AO-graded verdigris maps) placed in all 14
     recessed shadow alcoves across the gatehouse and both 20 m walls (`m_erebor_gate_sentinel_a`).
  4. Rebuilt the 1:1 facade with hole-free grounded U-arch frames (`dwarven_canted_u_arch_frame_loop`, fixing 2 bottom-edge
     backface hits and removing inner reveal UV stretch), a carved verdigris-stone and aged-bronze interlaced Erebor crest
     (`ref_01`, `ref_03`, `ref_09`), a crowned double-pointed lancet niche, 8-tier 45 degree diamond-lattice windows, a
     3-arch concentric Crown Citadel (`ref_04`), carved octagonal statue plinths with corner buttresses (`ref_08`), and a
     vaulted rear interior dwarven hall wall with 6 fluted pilasters, canted breach and inner gate arch hoods, runic friezes,
     blind arches, and 4 bronze braziers.
  5. Rebuilt the enveloping 132 m Lonely Mountain massif into a broad rounded-crag columnar granite profile (`smooth=True`,
     ridged multifractal vertical fracture columns, dual-octave Tripo cliff heightfield, tucked tapered granite buttresses)
     eliminating flat-quad faceting and pointy tent peaks.
  6. Stripped vertex color layers (`Col`) from `bo_erebor_gate_mountain_flanks` and `bo_erebor_gate_mountain_peak`, kept
     only the exact 33 active textures in staged delivery, and normalized all 6 `_h.png` height maps to 8-bit RGB.
- **Staged Modding Kit delivery (`kit_full/delivery/staged/delivery_verification.json`):** `overall_delivery_pass: true`.
  - FBX round-trip (`fbx_roundtrip_pass: true`): `erebor_great_gate_kit.fbx` (`7,585,916` bytes, sha256
    `608ee554047b464ede6f916d5a595e0cc75dec4fe2199bd0904a29510a4e2bed`) re-imported all `118` meshes cleanly (`0` missing
    refs, `0` `.001` suffixes, `0` vertex colors on `bo_`, world bounds `X [-91.53, 91.90]`, `Y [-40.98, 44.00]`,
    `Z [-6.00, 134.49] m`).
  - Staged textures (`texture_pass: true`): exact `33` x `1024x1024` 8-bit RGB PNGs (`0` errors, all 9 normal maps verified).
  - Prefab XML (`taom_erebor_great_gate.xml`, `26,748` bytes, sha256
    `7f3a727f0c561164aff2d3cf1f71663d0c4f439b9fb77abf6faa405f8cd5d3ab`): 48 `game_entity` elements, 0 live `TAOM_Map`
    clashes (`zero_clashes_pass: true`), global cap `124,927 / 131,072` (`6,145` spare).
  - Visual sheets and packed review file:
    - `hero/v1/S_erebor_hero_showcase.png`
    - `hero/v1/S_erebor_hero_siege_qa.png`
    - `hero/v1/S_erebor_hero_lod_compare.png`
    - `view/erebor_gate_hero_v1_VIEW_REVIEW.blend` (`40,707,126` bytes, 11 workspaces, 10 framed Material Preview viewports, all 33 1K maps packed)
    - `kit_full/delivery/staged/OWNER_IMPORT_STEPS.md` and `FILES.sha256`
    - `review_now.html`

## Owner review and pause handover (2026-11-27)

Mike inspected `view/erebor_gate_hero_v1_VIEW_REVIEW.blend` in Blender on 2026-11-27 and paused this task for a future
session to push the asset to a true 1:1 replica before any Modding Kit import into `TAOM_Map`.

Owner feedback verbatim summary (2026-11-27):

- **Overall verdict:** the gate needs to be an almost exact 1:1 replica, and Hero v4.2 is still far from that bar,
  though it provides a solid base to improve from. Work is paused here (not imported into `TAOM_Map`).
- **Keep and build on:**
  - **Mountain massif:** approved ("The mountain is fine"). Keep `erebor_gate_mountain_flanks` and
    `erebor_gate_mountain_peak` (including the open `78.4 m x 20.0 m` rear interior town courtyard below `Z = 20 m`).
  - **Broken breach wall craft:** approved ("Excellent job with that!"). Keep the Tripo-displaced feathered rubble
    mound, torn masonry core, and `<= 21 deg` walkable `bo_` ramp pattern.
  - **Siege envelopes as a planning aid:** approved ("good job placing the siege as an idea"). Keep
    `09_Siege_Machine_Envelopes` (14 m ladders, 12 m towers, ram, 4 defender catapult 120 degree pads, outer/inner gate
    clearances).
- **Required rework for the next session (in priority order):**
  1. **Correct the dwarven statues first (`erebor_gate_statue_l`, `erebor_gate_statue_r`, and review `sentinel`):**
     the current Tripo text-to-model warriors are too soft and human-proportioned with smaller diagonal weapons.
     `ref_01`, `ref_03`, `ref_08`, and `ref_09` call for chunky, heavily stylized angular geometric Erebor stone dwarfs:
     wide faceted square-peaked helms with cheek guards, massive layered square shoulder plates, oversized hexagonal
     scale skirts, thick geometric gauntlets gripping giant double-bladed broad-axes straight vertically tight to the
     chest, carved straight out of the flanking cliff rock (plus optional fallen/tilted right statue per `ref_01`).
     Build an exact angular geometric blockout or Blender sculpt matching `ref_08`/`ref_09` proportions before baking
     (never upload owner reference photos to Tripo or any outside service).
  2. **Show and verify both broken walls clearly (`erebor_gate_wall_l_dest` and `erebor_gate_wall_r_dest`):**
     both left and right broken 20 m breach walls already exist in `04_Breach_Walls_Broken`, the FBX (`118` meshes),
     and `taom_erebor_great_gate.xml` (`2 x 15` `WallSegment` trees, seeded `7701` left and `7702` right), but
     `VIEW_REVIEW.blend` hid `erebor_gate_wall_l_dest` by default so the viewport showed intact-left next to
     broken-right. Add a clear "both walls breached" presentation view in `VIEW_REVIEW.blend` and the review sheets so
     both left and right broken walls are immediately visible and verified side by side.
  3. **Rebuild the carved green-stone facade to a true 1:1 replica (`ref_01`, `ref_03`, `ref_04`, `ref_09`):**
     overlay the orthographic front render directly on `ref_04` and `ref_03` to lock exact bay widths, arch spring
     heights, monolithic stepped pier ratios, deep recessed shadow chambers, exact carved interlaced stone crest,
     lancet niche tracery, dense upper diamond-lattice bays, lower sentinel colonnade rhythm, and upper crown citadel
     proportions before attempting any Modding Kit import.
- **Technical notes for the taking-over session:**
  - **Workspace:** everything reusable lives under `E:\LOTRAOMAssets\_auto_workspace\kingdom_kits\erebor_gate\` (see
    `STATE.md` there for the full inventory: `kit_full/src/` scripts, `32/32` unit tests, 33 x 1K textures, 5 raw Tripo
    GLBs and prepped blends, `delivery_verification.json`, and `OWNER_IMPORT_STEPS.md`). Nothing in live `TAOM_Map` was
    modified.
  - **Tripo credits:** `360` credits remain (`0` frozen; `250` spent across 5 v3.1 text-to-model runs logged in
    [tripo-cli.md](../features/tripo-cli.md)).
  - **Blender GUI `.blend` pitfall (already fixed):** never save an owner review `.blend` from
    `read_factory_settings(use_empty=True)`, which strips all workspaces and windows so Blender opens windowless. Both
    `build_erebor_gate_hero.py` and `pack_view_review_blend.py` now use `use_empty=False` (`11` workspaces, `10` framed
    Material Preview viewports).
  - **Sandboxed shell desktop note:** Antigravity shells on this host execute on an isolated hidden Windows desktop
    (`WinSta0\exebox-...`), so GUI Blender windows launched from that shell do not appear on the user's visible desktop.
    Hand the user the `.blend` path or run command to open locally unless running inside an interactive desktop session.
  - **Open decisions carried forward:** gate leaves route (Route A vanilla gates vs Route B custom dwarven leaves),
    right statue default (upright vs `ref_01` broken swap), target scene (`taom_haterade_erebor_town_z` vs fresh
    `town_E1`), and tracking issue (#1600 vs dedicated issue; no public GitHub issue was opened prior to pausing).
