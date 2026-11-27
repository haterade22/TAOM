# The great gate of Erebor: handover 2 (for a new lead session on Claude Fable 5.2)

## Read this first

- **What this is.** The entry point for a new lead session that takes over the great gate of Erebor. It carries the
  research itself, so that you look nothing up that it can tell you. Written 2026-11-27, clock read at 10:05 CST, by
  the Rivendell kit session (kingdom kits job, issue #1600), on the maintainer's request of 2026-11-27. Revised the
  same day (clock read at 10:24 and 10:42 CST) after an independent check: its findings are closed in place.
- **It replaces** `E:/repos/taom-kingdom-kits/docs/kitbash/erebor-gate-handover.md` (handover 1, 2026-11-26) as the
  entry point (the trunk copy `E:/repos/TAOM/docs/kitbash/erebor-gate-handover.md` holds the same text with CRLF line
  ends: `diff --strip-trailing-cr` prints nothing). Handover 1 stays a source. Still worth reading in it: section 2
  ("What one piece, with the mountain, siegeable means in this engine"), section 6 ("Carving of a master's quality"),
  section 8 (the Modding Kit and the engine) and section 9 (Tripo by the command line). Its section 3 reading of the
  pictures is replaced by Part B4 here, which adds proportions, landmarks and the cameras. Handover 1 read the statues
  right: "the models show both upright" (line 250), and "alike in armour and not a mirror pair" (line 247). The
  kneeling reading came later, in design language v1 (`E:/repos/TAOM/docs/kitbash/erebor-gate-design-language.md`
  lines 30, 132, 168), and the build followed it.
- **What happened before you.** A session on another AI client built the gate from 2026-11-26 19:39 to 2026-11-27
  09:40: a design language, three massing forms, 1024 px texture sets, five Tripo models, and a "Hero v4.2" with
  levels, bodies, a prefab file and a staged Kit package. The maintainer orbited it in Blender on 2026-11-27, kept
  the mountain, the broken wall and the siege layout, and paused the work: "still far" from the bar. Nothing went
  into the game module. Its working paper with every check of this handover:
  `E:/LOTRAOMAssets/_auto_workspace/kingdom_kits/_shared/handovers_2026-11-27/erebor_gate/SURVEY.md`.
- **The end state, in one sentence:** an exact replica of the great gate of Erebor from his reference pictures
  (more pictures may be fetched), carved as one mass with the mountain from as few pieces as the engine allows, like
  a master dwarf carved it, and siegeable with walls that break as in the game's scenes.

Words used here: "W" is the work folder `E:/LOTRAOMAssets/_auto_workspace/kingdom_kits/erebor_gate`. "The earlier
session" is the session that built Hero v4.2. "His pictures" are the ten in `W/refs/owner_2026-11-26/`. A coordinate
in the KIT frame has X across the front, +Y toward the field, -Y into the mountain, Z up, Z 0 the ground line at the
gate threshold.

## Part B1. The end state, as deliverables

"Exact" is defined here as a measured target. The thresholds marked RECOMMENDATION are this handover's proposal: you
calibrate them in phase P0 on planted faults, then put them to the maintainer on the first lean page.

| # | Deliverable | Proven done when |
|---|---|---|
| 1 | **The reference pack.** His ten pictures, plus web pictures of the views that are missing (B4), in `W/refs/web_<date>/` with a `SOURCE.txt`. Each file gets a generic name (`web_01.jpg` and on). `SOURCE.txt` holds for each file: the generic name, the host name only where it names no film, studio, maker or person (else the words "a fan picture site"), the date fetched, the pixel size, the sha256, and what it shows in generic words. It never holds a full address, a page title or a caption. The same naming rule holds for every file in W and in the repo (Part A5) | `SOURCE.txt` lists every file; the lead has looked at each |
| 2 | **Landmarks and solved cameras** for the five bar pictures (`ref_01`, `ref_03`, `ref_04`, `ref_08`, `ref_09`) and any web picture used as a target: named 2D points per picture (B4 lists them, placed by the method of B1b), and a camera solved from them (focal length, position, rotation) on the model's own named landmark empties | Reprojection RMS at most 0.5 % of the picture height on the solving points, or twice the noise floor of B1b where that is larger (RECOMMENDATION); a solver test recovers a known camera from a synthetic render within 2 % of focal length |
| 3 | **The pixel gate of each round.** For each bar picture: a render from its solved camera, a 50 % blend overlay, a difference picture, and a result file with: the error of every landmark in percent of picture height; the IoU of the front mask, the rock mask and each statue mask; the mean edge distance inside the front mask; the counts of repeated features (slot rows, merlons a gallery, figures in the row) against the counts that the picture shows. No count exists yet: P0 makes them (B1b), and a count that the two counters do not agree on is left out of the gate and listed | RECOMMENDATION: architecture landmarks at most 1.0 % on `ref_04` and `ref_03`, 1.5 % on `ref_01` and `ref_09`; statue landmarks at most 1.5 % on `ref_08` and `ref_09`; IoU front 0.93 or more, statues 0.88 or more, rock 0.90 or more if he asks for the outline (Q11); edge distance at most 0.6 % of picture height; feature counts equal. Each landmark threshold is the larger of its value here and twice the noise floor of B1b; a threshold that the floor raises goes on the lean page. Each metric fails on its planted fault (a lower bay 10 % wider; a statue scaled 0.9; the focal length off 10 %). Pass flags are real booleans |
| 4 | **The statues.** Two colossal standing dwarf warriors, blocky and geometric, as `ref_01`, `ref_04`, `ref_08`, `ref_09` show, their backs grown into the flank rock as the paraphrase of his verdict asks ("emerging directly out of the flanking cliff rock") and `ref_09` shows (on `ref_04` they stand in front of the rock: Q2 asks him); the fallen right statue as a second type | Gate 3 on `ref_08` and `ref_09`; plates are single plates in full rows (his art rule); his word in a Blender window |
| 5 | **The front, 1:1.** Every feature of B4 at its measured proportion | Gate 3 on `ref_04`, `ref_03`, `ref_01`, `ref_09`; an independent critic's report; his word |
| 6 | **One carved mass.** Mountain, front and statues as one continuous carved surface. Cut only where the engine needs an entity (B1a) or where a far level needs a smaller bounding sphere, and then only along a hidden line (a shadow gap, a rock fissure, a gallery soffit) | No joint visible in culled, textured, lit renders at 2, 8, 30 and 130 m; open edges 0 on closed parts; the seam gate passes in both orders at every cut |
| 7 | **The siege function.** Two breach walls (left and right) with solid and broken states, parapet parts, a gatehouse for an outer and an inner gate with a rock hole, stairs with `has_steps`, four defender machine pads, decks at the height of the machines | The spec tests and the Blender probes, each with a planted fault; the prefab tree matches the scene reference (B6); an independent check; a siege test scene counted by hand; a siege played in the game (his step) |
| 8 | **Game assets.** Levels `.lod3` and `.lod6`, `bo_` bodies, texel density at the kit standard (0.45 UV a metre, 460.8 px/m on 1024 px sets) or a recorded exception | `tools/audit_fbx_lods.py`, `tools/blender/mesh_preflight.py`, `tools/blender/qa_views.py`; the measured density table |
| 9 | **A kit file in the Modding Kit,** final names and folder (Q8), a prefab file, a manifest, a copy with hashes and an undo list | Independent check before the copy; his import; `python tools/check_rdc_entries.py` and `python tools/check_prefab_meshes.py` pass after it |
| 10 | **A review file for him** with both broken walls visible, the intact state, and the fallen statue type, each as its own view | He orbits it; a window capture proves the window shows the file |
| 11 | **Records** after every result: `W/STATE.md`, the build record, the design language v2, the lessons, the Tripo job rows | Each file names the result file that proves each claim |

### B1a. "As few pieces as possible": the entities that the engine needs, and nothing more

A siege reads entities with scripts and tags; one entity has one mesh and one body with one set of flags. So the
gate is ONE prefab that a scene builder places once. Inside it, a part is its own entity only where this table says.

| Entity | Count | Why the engine needs it | Source |
|---|---|---|---|
| Prefab root | 1 | Places the whole | `kingdom-kit-layout.md` rule 10 |
| The fixed carved mass: mountain, front, statues, gatehouse shell, decks, causeway head | 1 to 4 (RECOMMENDATION: 4, cut along hidden lines: upper mass with peak and crown; left flank with its statue; right flank with its statue; the lower centre with the gatehouse shell and the deck over the gate room) | Nothing reads it, but the engine picks a level of detail by the distance from the camera to the surface of an entity's bounding sphere; one mountain-sized sphere draws full detail all siege long (handover 1, section 2, "The levels of detail") | `docs/reference/engine/static-mesh-lod-selection.md` |
| Each breach wall | 2 x 15 | Root with `WallSegment` and a side; solid child (mesh, body, `WallSegment`) with six parapet parts, each with a `DestructableComponent` and a `debris_holder` tagged `reference`; broken child (mesh `_dest`, body, `WallSegment`). The rail is a sub-object, so 15 and not 16. Exactly two sided walls a siege level, and a ram | `settlement-scene-needs.md` lines 115 to 123, rows 6 and 7 (lines 66 to 67) |
| Gatehouse parapet parts | 2 per part (part and `debris_holder`); Hero v4.2 has 4 parts | `DestructableComponent`, `DestroyedByStoneOnly` true, `CanBeDestroyedInitially` true, `MaxHitPoint` 1 | `settlement-scene-needs.md` line 525 |
| Each stair | 1 | One body has one set of flags; a stair carries `has_steps` | lines 376 to 390 |
| Fallen right statue | a second type of the right flank mass, swapped in the prefab or a second prefab | A type, not a part | his recorded words: "optional fallen/tilted right twin" |
| NOT in the prefab | the outer and inner gate, the ram, 4 ladders, 2 siege towers, 4 defender `DeploymentPoint`s with a `mangonel_b_spawner` and a `ballista_b_spawner` each, the rock pile and its targets, archer points, position markers, navmesh ids, spawn sets | The scene places them; no prefab file nests a prefab reference | `settlement-scene-needs.md` lines 97 to 110, 528 |

Target count: 1 + 4 + 30 + 8 + 2 = 45 entities (Hero v4.2 has 48: `grep -c '<game_entity'` on the staged file). The
global prefab cap had 6,193 spare on 2026-11-27 at about 10:00 (`tools/check_prefab_budget.py`, five modules:
TAOM_Map 98,104, Native 23,828, SandBox 2,889, StoryMode 39, LOTRLOME_Armory 19). `NavalDLC` has 9,927 more; with it
loaded the sum is 134,806, over the cap (`settlement-scene-needs.md` lines 730 to 731). Other kit sessions draw on the
same margin: on the day of the copy, count every module that the Kit's `rgl_log` lists as loaded, `NavalDLC` included
(`settlement-scene-needs.md` lines 733 to 734 give the command), and sum.

### B1b. How a landmark, a mask and a count are made (the noise floor of P0)

No tool here clicks a point: an agent reads a picture and writes numbers. OpenCV is not installed; numpy 2.3.4, scipy
1.16.3 and Pillow 12.1.1 are (`python -c "import numpy,scipy,PIL"` printed these on 2026-11-27). So P0 works this way:

1. **Crops.** A script cuts each bar picture into crops at 2x (nearest neighbour), each with a pixel grid every 10
   source pixels and its source coordinates printed on the border. The viewer button and every mark are masked out
   first.
2. **Two placers.** Two `kitbash-architect` builders place every landmark of B4 on their own, each reading the crops
   with the Read tool, and write source pixel coordinates. Each then draws its points on the crops and looks again
   before it hands over. The landmark file keeps both sets and their mean.
3. **The noise floor** of a landmark is the distance between the two placements, in percent of the picture height.
   The floor of a picture is the median over its landmarks. A landmark whose two placements differ by more than three
   times that median is placed a third time by the lead, and left out of the gate if it stays in doubt.
4. **Thresholds** (B1 rows 2 and 3) are each the larger of the value written there and twice the floor.
5. **Masks** are polygons through named landmarks and named outline points (same two placers, same rule), never a
   colour key alone. Mask IoU is computed on the polygons.
6. **Counts** (slot rows a bay, merlons a gallery, figures in the row, lattice cells a grille) are made by the same two
   placers on the crops. A count that they do not agree on is left out of the gate and listed in the result file. On
   `ref_04` (634 px wide) a merlon pitch may not resolve: count it on `ref_03`, `ref_09` or a web picture instead.
7. **The scored copy** of Hero v4.2 with its landmark empties is saved under `W/refmatch/r0/`; `W/hero/v1` stays as it
   is (R6).

## Part B2. What exists today

All paths under W unless they start with a drive. Dates are file modified times.

| Item | Path | State | Keep, rework or redo | Why |
|---|---|---|---|---|
| His ten pictures | `refs/owner_2026-11-26/` with `SOURCE.txt` | Complete, hashed | Keep | The brief |
| Swatches and grids | `_tools/measure_refs.py`, `control/ref_and_control_measurements.json`, `control/ref_grids/` (2026-11-26 19:39) | 11 swatch medians; I recomputed all: identical | Keep | Trusted |
| Design language v1 | `E:/repos/TAOM/docs/kitbash/erebor-gate-design-language.md` (trunk only, 2026-11-27 09:45) | Band table of `ref_04`, colour table, exact and free lists | Rework into v2 | It reads the statues as kneeling; it has no landmark list, no camera, no feature counts |
| Build record | `E:/repos/TAOM/docs/kitbash/erebor-gate.md` (trunk only, 2026-11-27 09:45) | Rounds 1 to 3, his verdict as a paraphrase | Keep as history; continue it | Not on `feat/kingdom-kits` (R11 below) |
| Massing round 1 | `massing/r1/` (2026-11-26 20:12) | Forms A, B, C; no pick recorded | Keep as history | He never answered |
| Carving test panel | `design/carving_panel/` (2026-11-26 20:46) | One 6.67 x 8.0 m bay, 2,768 tris, 460.8 px/m | Keep as the texture density control | He never picked a carving standard on it |
| Trim sheet and sets | `textures/sets_1k/` (33 staged maps), `textures/trim_bake/`, `kit_full/src/textures/` | 6-strip trim (ashlar, merlon tablet, cornice dentils, arch bevel, lattice, runes), stone, pavement, bronze, cliff, rubble | Keep; add strips | New members (slot band, figure row, stepped pier) need strips; bronze is a question (Q10) |
| Tripo cliff and rubble | `tripo/out/cliff_v1`, `rubble_v1`, `tripo/prepped/rubble_cliff/` | Height fields 256 x 256 and 1K maps | Keep | Part of what he approved (mountain, breach) |
| Tripo statues | `tripo/out/statue_v1` (axe), `statue_hammer_v1`, `tripo/prepped/statue_axe`, `statue_hammer` | Kneeling, soft, human proportions; 18,000 tris after prep (`tripo/prepped/statue_axe/prep_statue_result.json`), 18,864 each as placed in the hero (`hero/v1/hero_v1_result.json`) | Redo | His first rework item |
| Tripo sentinel | `tripo/out/sentinel_v1`, `tripo/prepped/sentinel` (4.6 m, 1,100 tris) | A human knight in a tabard | Redo | His words: "review sentinel"; the picture's figure row is narrow carved figures |
| Generator code | `kit_full/src/` (`hero/`, `textures/`, `tripo_prep/`, `tests/`) | 32 tests pass (I ran them: `32 passed, 21 subtests passed`) | Keep and extend, test first | Reusable; fix R1, R2 in the prefab generator |
| Hero v4.2 | `hero/v1/` (`erebor_gate_hero_v1.blend`, `hero_v1_result.json`, 3 sheets, 17 renders, 2026-11-27 06:04) | 29 LOD0 objects, 132,020 tris; `.lod3` 49,772; `.lod6` 19,146; `bo_` 4,404 | Mountain and breach walls: keep. Statues: redo. Front: rework to 1:1. Rear hall: rework later | His verdict |
| Mountain | `erebor_gate_mountain_flanks` (15,168 tris), `erebor_gate_mountain_peak` (16,216) | Approved: "The mountain is fine" | Keep its look; hide the joint (R4); the outline is Q11 | Approved, but its joint shows and its peak is blunter than `ref_04` |
| Breach walls | `erebor_gate_wall_l_solid`, `_l_dest`, `_r_solid`, `_r_dest` (7,272 / 8,308 / 7,272 / 8,264 tris) and 12 parts | Approved: "Excellent job with that!" | Keep; show both broken | His words |
| Siege layout | `kit_full/delivery/staged/scene_wiring_manifest.json` (deck Z 12.0; outer gate Y 12.5, inner Y 3.5, 9.0 m apart; rock pile Z 12.0 over targets Z 0.2; four pads 5.8 x 6.0 m at Z 12; ladders at X +-13.8 and +-27.2; towers at X +-20.5; ram stop Y 15.7 with 31.5 m approach) | Approved "as an idea" | Keep as the layout | Checked against B6: deck 12.0 m inside the 14 m ladder's 10.1 to 13.6 m and at the 12 m tower's hinge 11.90 m; gates 9.0 m apart inside 7.15 to 9.81 m; rock drop 11.8 m under the 14 m limit; pads 5.8 x 6.0 m over the mangonel's 5.50 x 5.72 m. NOT checked: the ram. The scene reference puts the rest point 6.25 m to the field side of the outer gate (`settlement-scene-needs.md` lines 583 to 586), so KIT Y 18.75 here; the manifest's `stop_pos_blender_m` is Y 15.7 and does not say which point of the ram it names. UNVERIFIED until P5 |
| Staged package | `kit_full/delivery/staged/` (FBX 7,585,916 B, 118 meshes, 33 maps, prefab XML 48 entities, `OWNER_IMPORT_STEPS.md`) | Hashes match the record (I recomputed); NOT in the module | Redo after the rework | Old forms; R1, R2, R10 |
| Review file | `view/erebor_gate_hero_v1_VIEW_REVIEW.blend` (40.7 MB, 2026-11-27 09:24) | Hid `erebor_gate_wall_l_dest` | Redo with both walls shown | His second rework item |
| Review pages | `review_now.html` (2026-11-27 09:40), `review_now_first.html` | Long; questions never answered | Replace with one lean page | His rule for pages |
| Handover 1 and its checks | `docs/kitbash/erebor-gate-handover.md`; `kingdom_kits/_shared/handover_check_erebor_gate/` | Source | Keep | See "Read this first" |
| Older Erebor kit | `TAOM_Map/AssetSources/Scenes/erebor` (145 pieces), `docs/kitbash/erebor/`, `docs/kitbash/erebor-buildings.md` (house, great hall, barracks, each one FBX with levels and a body) | Live, used by other scenes | Read only | Never change its files; new names in new files |
| Worktree of the earlier session | `E:/repos/taom-erebor-gate` | Gone | | Its records name it |

**Findings of the review (2026-11-27) that you act on.** Full evidence in the survey, section 6.

| Id | What | Evidence | Rule that fixes it |
|---|---|---|---|
| R1 | The stair flag is written as an attribute, `<physics shape="..." body_flags="has_steps" />` (staged XML lines 160, 167). `kit_full/src/hero/generate_erebor_prefab_xml.py` writes it (`_add_mesh_and_body`, lines 52 to 55, called at line 124), and `kit_full/src/tests/test_erebor_prefab_xml.py` line 45 asserts it (`.//physics[@body_flags='has_steps']`) | No prefab in the live `TAOM_Map/Prefabs` or `Native/Prefabs` uses a `body_flags=` attribute; 344 lines use `<body_flags><body_flag name="has_steps" />` inside `<physics>`; `settlement-scene-needs.md` line 379 | First change test line 45 to assert the nested form and fail on the attribute, and see it fail; then change the generator |
| R2 | The staged XML has no `mobility` and no `record_to_scene_replay` at all (0 and 0 by `grep -c`) | The Gondor control `TAOM_Map/Prefabs/Lotraom_comprehend_Gondor_Wall_2_Complete.xml`: `<flags><flag name="record_to_scene_replay" value="true"/></flags>` as the first child of the root, the solid child, the broken child and each parapet part (lines 3 to 5, 26 to 28, 52 to 54, and on); `mobility="1"` on the `<game_entity>` tag of each parapet part and each `debris_holder` (lines 51, 81, and on: 10 in all); the rail part has neither; `settlement-scene-needs.md` line 103 | Write both as the control does, on the same entities |
| R3 | Statues kneel, and stand free of the rock | His pictures show standing figures; the paraphrase of his verdict says "emerging directly out of the flanking cliff rock". A plinth is right: `ref_04` and `ref_09` show one | Phase P1 |
| R4 | A line and an outline step at the joint of the two mountain meshes | `hero/v1/renders/hero_10_backface_culled_qa.png`, `hero_01_front_hero_intact.png` | Cut only along hidden lines |
| R5 | Texel density: statues 4.5 and 4.7 px/m, mountain 86.7 and 88.8, merlons 162 to 178, architecture 363 to 433, against 460.8 | `hero/v1/hero_v1_result.json` | Tiling sets and baked detail normals; parts at the standard |
| R6 | Versions were written over (`hero/v1` holds v4.2) | Folder list | Each round in a new folder |
| R7 | No camera match and no pixel comparison exist; "1:1" was a word | No script under W compares with a reference | Phase P0 before any other build |
| R8 | The "critic audit" was the build script's own probe (132 rays) | `hero_v1_result.json` | An independent critic at every step |
| R9 | Dark gaps between front and rock; the rock should wrap tight | `hero_01`, `hero_02` against `ref_04`, `ref_07` | Phase P3 |
| R10 | `empire_castle_gate_outer_l3` (5.321 m wide closed) is recommended for a 5.2 m opening | `scene_wiring_manifest.json`; `settlement-scene-needs.md` line 491 | Use `european_castle_gate_outer_l2` (4.695 x 6.838 m) or widen the opening and check the swing box |
| R11 | `erebor-gate.md` and the design language are on trunk only, not on `feat/kingdom-kits` | `ls` of both checkouts | A merge of trunk into the branch changes a ref, so it waits for his yes (Q15). Until then, edit neither copy and record in W (B5 step 2) |
| R12 | No massing pick is recorded; the hero mixes readings | `review_now_first.html`; the record | Q5 |
| R13 | One axe and one hammer statue; his recorded words say broad-axes | Tripo jobs `48abbdfe`, `54c30180` | Q2 |

## Part B3. The maintainer's words and decisions

| Date | His words | Source |
|---|---|---|
| 2026-11-26 | "Give me a hand over document for making the erebor gate from the movies. ... But I want the gate to be made as one piece with the mountain in it and it has to be siegeable (walls able to be broken like in the scenes, etc). Precision and master level carving of the 3D objects is critical for this one." | Handover 1, lines 28 to 31 (checked verbatim by its fact check) |
| 2026-11-27 | On the mountain: "The mountain is fine" | `W/STATE.md` line 13 |
| 2026-11-27 | On the broken breach wall: "Excellent job with that!" | `W/STATE.md` line 14 |
| 2026-11-27 | On the siege envelopes: "good job placing the siege as an idea" | `W/STATE.md` line 15 |
| 2026-11-27 | The rest of that verdict, as the earlier session wrote it (a paraphrase, not his text; no raw text was found): an almost exact 1:1 replica of `ref_01`, `ref_03`, `ref_04`, `ref_08`, `ref_09`; Hero v4.2 is far from that but a solid base. The method it names for the front: "overlay orthographic front renders directly over ref_04 / ref_03 / ref_09" (line 19). First the statues: "chunky, blocky, geometric stylized Erebor stone dwarfs with wide square-faceted peaked helms and cheek guards, massive layered square shoulder plates, oversized hexagonal plate scale skirts, giant thick angular gauntlets holding huge geometric double-bladed broad-axes straight vertically tight against the chest, emerging directly out of the flanking cliff rock (with optional fallen/tilted right twin per ref_01)", and review the sentinel. Then show both broken walls. Then the front to 1:1 (bay widths, arch spring heights, deep recesses, stepped piers, the crest, twin-lancet niche tracery, diamond lattice, the lower figure row, the crown) | `W/STATE.md` lines 9 to 19; `E:/repos/TAOM/docs/kitbash/erebor-gate.md` lines 206 to 242 |
| 2026-11-27 | "The end-state of Erebor is to have an exact replica of it from the provided references (if they need more go out and get more). Erebor will probbaly be the hardest because it has to be carved from as few pieces as possible like a master dwarf carved it. Just tripo MCP at will we have a ton of credits." | His request for this handover (to the Rivendell lead) |
| 2026-11-26 | Overnight work was authorized, by the earlier session's record only | `erebor-gate.md` line 111; his words not recorded |

**Decided by him:** keep the mountain, the breach wall craft, the siege layout (his quoted words); statues first, both
broken walls second, the 1:1 front third (the paraphrase); Tripo through the MCP at will, and more references may be
fetched (his request of 2026-11-27).

**Held by the earlier session's record, not by his words:** "do NOT import until rework below passes owner review"
(`W/STATE.md` line 5). Until he answers Q14, nothing of Hero v4.2 goes into the module, approved or not.

**What he has not been asked** (ONE lean page, Q1 to Q15 in this order; each with options and a recommendation; show
it within the first hour, and build on the recommendation where a question does not block; Q14 and Q15 are the
exceptions: until he answers, nothing goes into the module and no git step is taken):

- **Q1. What "exact" binds to.** (a) The film front of `ref_01` and `ref_03` is the design; the model pictures
  (`ref_04`, `ref_07` to `ref_10`) give the proportions and the parts that the stills do not show. (b) The sculpted
  model is the design. (c) Measure everything from `ref_04` and correct to the film where they differ. And: may we lay
  his pictures over our renders for proportions and outlines (tracing), given his earlier rule that "a part too close
  to a bought or reference model goes"? The paraphrase of his verdict already names overlays ("overlay orthographic
  front renders directly over ref_04 / ref_03 / ref_09", `W/STATE.md` line 19), but it is not his text, so ask.
  Recommendation: (a), and yes to overlays for proportion and outline; no picture becomes a texture.
- **Q2. Statue pose and weapons.** (a) Standing, both with a double-bitted axe held vertically against the chest, one
  sculpt mirrored. (b) Standing, two individual figures (axe and hammer). (c) Kneeling as built. Recommendation: (a);
  his pictures show standing figures and his recorded words say broad-axes. Note for him: handover 1 read the two as
  "alike in armour and not a mirror pair" (line 247), so (a) is a choice, not a reading; a second sculpt with other
  helm or plate details is the middle way. And: do their backs grow into the cliff (the paraphrase, `ref_09`), or do
  they stand clear of it (`ref_04`)? Recommendation: grown into the cliff at the back and the plinth.
- **Q3. The right statue.** (a) Whole by default, fallen as a second type. (b) Fallen by default. (c) Whole only.
  Recommendation: (a).
- **Q4. How the one mass is cut.** (a) One fixed entity (fewest pieces; full detail drawn all siege). (b) Up to four
  fixed entities cut along hidden lines for the far levels (B1a). (c) The seven of Hero v4.2. Recommendation: (b).
- **Q5. The siege line.** (a) As built: a 12 m deck across the foot of the front, the row of standing figures is the
  wall's face, the two 20 m breach walls are bays of that foot left and right of the gatehouse. (b) A separate low
  outwork in front of the gate (no picture shows one). (c) Two heights: the foot for ladders, a high gallery for
  defenders only. Recommendation: (a), which he approved "as an idea".
- **Q6. The gates.** (a) Route A: vanilla outer and inner gates placed by the scene inside our carved surround. (b)
  Route B: our own leaves on a vanilla door skeleton (unproven in the Kit). Recommendation: (a), with
  `european_castle_gate_outer_l2` as the outer gate (R10).
- **Q7. The door's size against the gates.** My rough reading of `ref_04` puts the door at about 6.5 % of the front's
  height and about 4 % of its width: on the Hero v4.2 front of 66.0 x 102.0 m (`W/kit_full/src/hero/erebor_hero_spec.py`
  lines 26 to 27) that is a door near 2.6 m wide and 6.6 m high, about half of what a vanilla gate needs (4.70 m). (a) Keep the front's scale and widen the door bay to the gate (one deliberate
  deviation, at the focal place). (b) Scale the whole front until the door fits (front near twice as tall). (c) A small
  carved door as in the picture, with the working gate set behind it. Recommendation: measure in P0 first; then (a).
- **Q8. Folder and file names.** (a) As staged: `AssetSources/Scenes/erebor_great_gate/gate/erebor_great_gate_kit.fbx`
  and `Prefabs/taom_erebor_great_gate.xml`. (b) A new subfolder of the old kingdom folder, `Scenes/erebor/greatgate/`,
  with new file names and the same piece names `erebor_gate_*` (clash-checked against the live module by the earlier
  session), and `Prefabs/taom_erebor_greatgate.xml`. Recommendation: (b): scene builders find Erebor in one folder, and
  nothing old is moved or changed.
- **Q9. Texture size.** (a) 1024 px sets, the trim sheet, and baked detail normals for statues and rock. (b) A 2048 px
  set for the statues and the focal front. Recommendation: (a), asked again only if the 8 m overlay shows blur.
- **Q10. Bronze inlay.** (a) No bronze: the bar pictures show none (bronze is in `ref_05` and `ref_06`, another
  design). (b) Bronze on the crest only. (c) As built. Recommendation: (a), with (b) as a second type if he likes it.
- **Q11. The mountain outline.** He approved the mountain; its peak is 1.8 times as wide as `ref_04` 5 % below the
  top and 1.24 times at 40 % (survey, section 5). (a) Keep as approved. (b) Narrow the outline to `ref_04` with the
  approved surface. Recommendation: (b), shown beside the approved one.
- **Q12. Causeway and bridge.** (a) A straight stone causeway with a bridge over a gorge as its own kit piece; water
  from the editor. (b) The causeway head only, in the mass. Recommendation: (a).
- **Q13. The scene and the tracking issue.** Which scene: (a) a kit piece and a showcase scene first; (b) his own
  scene `taom_haterade_erebor_town_z`; (c) a new scene for `town_E1` (Erebor). Issue: a new issue linked to #1600, or
  #1600 itself. Recommendation: (a) now; a new issue linked to #1600 (the repo needs an issue; creating one is public).
- **Q14. The approved parts into the Kit now?** Part A step 6 sends an approved set into the Kit the same day; the
  earlier session's record holds everything back until the rework passes his review (`W/STATE.md` line 5, not his
  words). (a) Wait: nothing goes in until the statues and the front pass his review. (b) The approved mountain and
  breach walls go in now as a first kit file, with a note of what they lack. Recommendation: (a), because the cut of
  the mass (Q4: Hero v4.2 has seven fixed entities, the recommendation four) changes the mountain's pieces and names.
- **Q15. The branch.** `erebor-gate.md` and the design language are on trunk only (R11). (a) The lead merges trunk
  into `feat/kingdom-kits` (shared with the Rivendell lead; explicit paths only). (b) Erebor gets its own worktree off
  trunk, and its records land from there. Recommendation: (a), so that all kit records stay on one branch.

## Part B4. The reading of the references

I looked at each of the ten. Two of them (`ref_08`, `ref_09`) carry a title and a maker's mark, and `ref_05` a logo:
copy none into a file. Every picture is a screen capture with a viewer button in a corner: mask it out before you
measure.

| Ref | What it is | Use |
|---|---|---|
| `ref_01.webp` 785 x 537 | Film still from low on the left | Bar. The front, the figure row, the left statue standing, the right statue fallen, the gorge |
| `ref_02.png` 976 x 521 | Concept of another design (fins, terraces) | Mood only, unless he says |
| `ref_03.png` 1457 x 609 | Film still, square on, dark | Bar. Bay rhythm, the long causeway, the door's smallness |
| `ref_04.png` 634 x 814 | Photograph of a sculpted model, square on | Bar. The clearest front: all proportions start here |
| `ref_05.png`, `ref_06.png` | A printable model of another design | Detail only (knotwork, grilles) |
| `ref_07.png` 556 x 612 | The `ref_04` model from the right | Depth of the recess; rock wrap |
| `ref_08.png` 582 x 576 | The model's statue from the side | Bar. Statue anatomy |
| `ref_09.png` 648 x 669 | The right statue against the lower front | Bar. Statue grip; lower bays; figure row |
| `ref_10.png` 535 x 803 | The foot of the model's front from above | Piers, plinths, door bay |

**The ten things that make the look, with proportions** (from `ref_04` unless named; percent of the picture's width
W or height H; the band values were measured by the earlier session and I checked them on `control/ref_grids/ref_04_grid.png`
within about 2 %; the rest are my readings on the same grid and are estimates until P0 measures them):

| # | Feature | Proportion today | State in Hero v4.2 |
|---|---|---|---|
| 1 | Rock peak with the front deep in a pointed recess; rock tight to the front, closing in a pointed arch over the crown (`ref_04`, `ref_07`) | Peak at 3.5 % H; ground line (plinth tops) 86.5 % H; recess apex about 20 % H; outline width over peak height 0.119, 0.301, 0.531, 0.648, 0.758 at 5, 10, 20, 30, 40 % below the peak | Peak blunter (0.217 to 0.940 at the same depths); wide dark gaps beside the front |
| 2 | Strict symmetry; a deep narrow central cleft in the middle tier | Axis 47.5 % W; cleft about 46.5 to 48.5 % W, 38 to 58 % H | No deep cleft |
| 3 | Canted arch heads of straight segments with doubled, stepped frames; lower bays tall and narrow; no round arch (`ref_01`, `ref_03`, `ref_09`, `ref_10`) | Lower canted arch band 64.5 to 72.5 % H; bay height about 3 to 4 times its width (`ref_04`, `ref_09`) | Bays about 1.8 times as tall as wide |
| 4 | Stepped monolithic piers, full height, on stepped plinths (`ref_09`, `ref_10`) | Three-bay inner width 31 to 64 % W against the front 24 to 71 % W (0.70 : 1) | Piers present; plinths missing at the foot |
| 5 | Rows of small square slots in every bay at several levels (`ref_01`, `ref_03`, `ref_09`, `ref_10`) | Lower three-bay wall 72.5 to 81.5 % H holds two to three slot rows | Present, but as large dark blocks |
| 6 | Fine close-set merlon galleries at several heights, a sill gallery in each arch (`ref_01`, `ref_03`, `ref_09`) | First horizontal gallery and crest 58.5 to 69.5 % H | Present at a coarser pitch |
| 7 | Diamond lattice grilles, twin-lancet niches, an interlaced crest over the central bay (`ref_01`, `ref_09`, `ref_10`) | Middle cleft and lattice tier 39.5 to 58.5 % H; crown citadel 24.5 to 39.5 % H | Lattice reads as zigzag trusses; crown a box with an X panel and a gable |
| 8 | At the foot a continuous row of narrow standing figures, a small door in the central bay, a long straight narrow causeway (`ref_01`, `ref_03`, `ref_09`, `ref_10`) | Base apron and door 81.5 to 86.5 % H | 14 human knight figures in spaced alcoves; short causeway |
| 9 | Two colossal standing dwarf warriors at the foot of the flanks (backs against the rock on `ref_09`, clear of it on `ref_04`): peaked faceted helm with cheek guards, beard, two or three layered square shoulder plates, a big hexagonal plate skirt in rows, boots, a double-bitted axe held vertically against the chest, a low slab plinth (rectangular on `ref_04`, a chamfered corner on `ref_09`; its plan is a P0 reading); the right one fallen in the film (`ref_01`, `ref_03`, `ref_04`, `ref_08`, `ref_09`) | Helm top 64 % H, plinth top 86 % H (statue about 0.27 of the peak-to-ground height); left statue 5 to 24 % W, right 73 to 92 % W (about 0.18 of that height wide) | Kneeling; 0.20 tall and 0.12 wide on the same measure; soft and human; one axe, one hammer |
| 10 | Green-grey verdigris stone against brown-charcoal rough rock, near-black recesses | Medians: front midtone `#3a3b31` (`ref_04`), front lit `#d2e7c2` (`ref_01`, graded), recess `#1c1d18`, statue `#4c4b41` (`ref_09`), rock `#362f2b` (`ref_04`), causeway `#bfaca5` | Close in hue; bronze added (Q10) |

The film front's width to height: carved front 0.59 : 1 (height 1.70 times width) from `ref_04` (design language
v1 line 34). Hero v4.2 builds 0.65 : 1 (66.0 x 102.0 m, `W/kit_full/src/hero/erebor_hero_spec.py` lines 26 to 27,
whose comment calls it "true 0.65:1"): a measured deviation of about 10 % in width that P3 closes. Statue helm height
to the first gallery: 0.95 : 1 (design language v1 line 38).

**Landmarks to place on each bar picture** (names shared with the model's landmark empties): F01 peak apex; F02
recess apex; F03, F04 recess edges at the ground; F05, F06 front edges at the ground; F07 crown top; F08, F09 crown
portal springs; F10, F11 cleft top and bottom; F12 to F14 the three lower arch apexes; F15 to F20 their six springs;
F21 door head; F22, F23 door jambs at the ground; F24 to F27 the gallery lines on the axis; F28, F29 outer pier tops.
Statue: S01 helm apex; S02, S03 cheek guard tips; S04 beard tip; S05, S06 shoulder plate tips; S07 axe head top; S08
axe blade outer tip; S09 belt centre; S10 skirt hem centre; S11, S12 boot toes; S13, S14 plinth front corners.

**What no picture shows** (yours to design, in the gate's language): the back of the front and the hall behind the
door; the top and what lies behind the crown; every stair and way onto a gallery; a walk wide enough to fight on; the
door leaves; the depth of each tier in plan (`ref_07` hints only); where the statues meet the rock at side and back;
the bridge from the side; the forecourt; any broken wall.

**Views to fetch from the web** (his words allow it): a daylight square-on frame of the full film front; a side
profile of the tiers; close frames of the crest, the door and the figure row; the film statue from the front and
three-quarter; the fallen statue close; a view from above; production concept pictures of the gate. Save them to
`W/refs/web_<date>/` with `SOURCE.txt` in the form of B1 row 1. They stay on this machine and go to no outside service, Tripo included.

## Part B5. The plan

### The first session day

| Step | Who | What | Shown to him |
|---|---|---|---|
| 1 | Lead | Claim the work: `python tools/work_claims.py claim erebor-gate --note "handover 2"` (exit 3: another session owns it: stop and ask). Read this file, then the Part A3 reads | |
| 2 | Lead | Git, read only: `E:/repos/taom-kingdom-kits/docs/kitbash/erebor-gate.md` and `erebor-gate-design-language.md` are absent on `feat/kingdom-kits` (R11). A merge changes a ref: it waits for his answer to Q15. Until then, read both from `E:/repos/TAOM/docs/kitbash/`, edit neither, and write the records in W (`W/STATE.md` and `W/build_record_2026-11-27.md`); they go into the repo doc after his answer. Never edit on `bannerlord-1.5.x` | |
| 3 | Lead | Look at his ten pictures and at the newest renders (`W/hero/v1/renders/hero_01` to `hero_10`); correct B4 where you see otherwise | |
| 4 | Lead | Write the lean page with Q1 to Q15: `python E:/LOTRAOMAssets/_auto_workspace/kingdom_kits/_shared/review_page/page_maker.py <HH:MM> <content json> <W>`; the content file is `{"needs": [...], "new": [...], "decisions": [...]}` with HTML fragments, and picture paths relative to W (`page_maker.py` lines 3 to 4). It writes `W/review_now.html`. Show it | Within the first hour |
| 5 | Lead | Fetch the missing views (B4); `SOURCE.txt` in the form of B1 row 1; look at each. The `kitbash-architect` agent has no web tool (its frontmatter: Read, Write, Edit, Bash, Grep, Glob, Monitor). Whether the lead has one is UNVERIFIED: look for a web search and a web fetch tool in the session's tool list. If there is none, add the list of views to the lean page and ask him to fetch them or to turn the tools on | |
| 6 | `kitbash-architect`, builders A1 and A2 | **P0, the reference match gate, by the method of B1b.** A1 writes the crop script, the camera solver (numpy and scipy; no OpenCV), the metrics of B1 row 3 and their planted faults. A1 and A2 each place every landmark, mask point and count on their own. A1 merges the two sets, computes the noise floor, and scores Hero v4.2 on all five bar pictures from a copy in `W/refmatch/r0/`. Output `W/refmatch/r0/refmatch_result.json`, the landmark files, the floor table and the overlays | The baseline overlays and the floor table, end of day |
| 7 | `kitbash-architect`, builder B | **P1 start, the statue blockout by code:** a standing blocky dwarf from the S landmarks of `ref_08` and `ref_09` (helm, cheek guards, beard block, layered square shoulder plates, hex plate skirt rows, gauntlets, vertical double-bitted axe, boots, slab plinth), with a 1.8 m figure; renders front, side, back and three-quarter on plain mid-grey, even light, no figure: these are the pictures that will go to Tripo | The blockout in a Blender window and the four renders, end of day |
| 8 | `kitbash-architect`, builder C | **P2, both broken walls:** a review file state and a sheet with both `_dest` walls shown, beside the intact state; and R1, R2 in `generate_erebor_prefab_xml.py`, test first (R1 names the test line). A Blender sheet and a generator change are builds, so they go to the builder agent of Part A1, not to `implementer`. Day 1 then runs four builders and one critic, the limit of Part A1 | The sheet, end of day |
| 9 | `kitbash-architect`, independent critic | Judges steps 6 to 8 beside his pictures, with its own scripts; no repair | |
| 10 | Lead | Read every result file and look at every picture yourself; decide each finding; record (`W/STATE.md` top block, the W build record); update the lean page with the four statue renders of step 7 and the line "these four renders go to Tripo next" | The lean page, end of day |

No Tripo job on day 1: the Tripo doc lets only a picture that he has seen go to Tripo
(`E:/repos/taom-kingdom-kits/docs/features/tripo-cli.md` "Rules for a session", rule 3), and he sees the blockout
renders at the end of day 1.

### The second session day

| Step | Who | What | Shown to him |
|---|---|---|---|
| 1 | Lead | Read `W/STATE.md` and his answers; set the working defaults; record each answer with his words | |
| 2 | Lead | **Tripo for the statue,** after he has seen the four renders (record that he has): load the skill `tripo-studio-mcp` first (user scope, `C:/Users/mikew/.claude/skills/tripo-studio-mcp`), read the live schema, then `tripo_multiview_to_model` with the front, side and back renders, generic file names and prompt, a saved idempotency key. Through the Studio MCP only: his word of 2026-11-27 names the MCP, and the command line spends the API pool (360 credits left, `W/STATE.md` line 29), so it is used only on his word. Log the job in the "Jobs" table of `docs/features/tripo-cli.md` with the route `MCP` | The first Tripo statue beside the blockout |
| 3 | `kitbash-architect`, builders A1 and A2 | **P0 close:** thresholds set from the floor (B1b step 4), each metric proven to fail on its planted fault; a landmark placed a third time where B1b step 3 says | The threshold table on the lean page (a threshold that the floor raised is marked) |
| 4 | `kitbash-architect`, builder B | **P1:** the Tripo statue through `/prep-tripo-prop`: the builder follows `E:/repos/taom-kingdom-kits/.claude/skills/prep-tripo-prop/SKILL.md` by reading it (agents cannot invoke a skill; its agent file says to follow a named skill this way); voxel remesh before decimation; then grow its back and plinth into the flank rock; first statue gate on `ref_08` and `ref_09` | The statue in a Blender window, beside the blockout |
| 5 | `kitbash-architect`, builder C | **P2 close** (R10 too), then **P3 start:** the member library, the first two members (canted double-framed arch head, stepped pier with plinth), each test first | The member sheet |
| 6 | Critic, then lead | The critic judges steps 3 to 5 beside his pictures; the lead decides, records, and updates the lean page | The lean page |

Use the recommendations of Q1 to Q13 as the working defaults until he answers; a build that a later answer changes
is kept as a type, never deleted. Q14 and Q15 hold their steps until he answers.

### The later phases, in order, each with its gate

| Phase | Work | Gate before the next |
|---|---|---|
| P0 | The reference match gate (days 1 and 2, B1b) | Each metric fails on its planted fault; the floor and threshold table exists; the baseline table exists; he has seen it |
| P1 | **Statues.** Blockout, Tripo (MCP) from the blockout views after he has seen them, `/prep-tripo-prop` (voxel remesh before decimation; the raw model faces +X), the hex plates as single plates in full rows, the axe exact by code if Tripo softens it; carved out of the flank rock (the statue's back and plinth grow into the cliff mesh); the fallen right type from the same sculpt, broken and tilted, with Tripo rubble | B1 row 3 statue thresholds on `ref_08` and `ref_09`; the critic; his word in Blender |
| P2 | **Both broken walls shown** (days 1 and 2); R1, R2, R10 fixed in the generator | Tests with planted faults; the sheet; his look |
| P3 | **The front, 1:1, tier by tier from the foot up.** First the library of members, one rule each (canted double-framed arch head, stepped pier with plinth, slot band, merlon gallery and sill gallery, lattice grille, twin-lancet niche, interlaced crest, the narrow carved figure of the row), each as a trim strip or exact geometry; then each tier from its band of B4; the figure row from one Tripo figure repeated by code | Per tier: landmark errors of that tier inside the threshold on `ref_04` and `ref_03`, feature counts equal, the critic; then the whole front on all four architecture pictures; his word |
| P4 | **One carved mass.** Front, statues and approved rock as one surface; the rock wraps tight to the front (R9); cuts only along hidden lines (Q4); the mountain outline if he says (Q11); the causeway and bridge piece (Q12) | No visible joint at 2, 8, 30, 130 m (culled, textured, lit); seam gate in both orders at every cut; open edges 0; IoU of the rock mask if reshaped |
| P5 | **Siege re-proof.** Regenerate the prefab (B1a tree); decks, gate room, rock hole, pads and arcs, stairs with the nested `has_steps`; the ram: open ground from the outer gate to its rest point at KIT Y 18.75 (outer gate Y 12.5 + 6.25, `settlement-scene-needs.md` lines 583 to 586) and beyond, and the manifest's ram stop written as that rest point or explained; the rear hall reworked as a designed room with stairs to every deck | The spec tests and Blender probes with planted faults; the critic; entity count against the cap |
| P6 | **Surfaces and levels.** Texel density at the standard (R5); new trim strips; `.lod3`, `.lod6`, `bo_` | `audit_fbx_lods.py`, `mesh_preflight.py`, `qa_views.py`; density table |
| P7 | **Into the Modding Kit.** Part A step 6 sends an approved set in at once; for Erebor, nothing of Hero v4.2 goes in, approved or not, until he answers Q14 (the hold is the earlier session's record, `W/STATE.md` line 5). Then: final names (Q8), the copy with hashes and an undo list, the prefab file only after his import with the Kit closed | Independent check before the copy; his import; `check_rdc_entries.py`, `check_prefab_meshes.py` |
| P8 | **A siege test scene** with two sided walls, both gates, the ram, ladders, towers, pads, rock pile and targets, navmesh ids; counted by hand; then a siege played | His step in the editor and the game; until then every claim about the game is UNVERIFIED |

**Where Tripo is used:** the two statues and the fallen type (from renders of your own blockout); one figure of the
foot row; extra rock relief if the reshaped peak needs it; rubble (held). **Where it is not:** the piers, arches,
slot bands, galleries, lattice, crest, door surround, decks, stairs, causeway, and every part with an exact plane or
an exact joint. Those are built from solids by code, and a sculpt pass may follow only on a built mass.

## Part B6. The scene research for this kit

From `E:/repos/taom-kingdom-kits/docs/kitbash/settlement-scene-needs.md` (version 2, 2026-11-26 19:05). Line numbers
are of that file.

| Row (line) | Thing | The kit delivers | The scene builder adds | For this gate today |
|---|---|---|---|---|
| 1 (61) | Centre scene | Its prefab file and showcase scene | One `center` scene used by visit and siege of all three wall levels; `scene_name` edit is his | Q13 |
| 2 (62) | Three wall levels | One wall height and one gatehouse for all three (RECOMMENDATION) | `<levels>` on each siege entity | One height (12 m) built |
| 3 (63) | Solid wall | Deck 12.0 m over the ground line; foundation; `bo_`; `.lod3`, `.lod6` | Nothing | Built; keep |
| 4 (64) | Parapet parts | Each part its own mesh `_mN` and body; `DestructableComponent` and `debris_holder` tagged `reference` | `tower_merlon` where a ramp lands | Built; add R2 |
| 5 (65) | Broken wall | Gap about 12 m at the deck; mound path 35 degrees or less, body ramp 30 degrees or less | | Built and approved |
| 6 (66) | Breach wall tree | Three `WallSegment` scripts; 16 entities with a rail entity, 15 with the rail as sub-object | Root tag, `SideTag`, nine navmesh ids, `solid_child`, `broken_child`, `visible="false"`, markers | Built: 2 x 15 |
| 7 (67) | Two sided walls and a ram a level | | Exactly two walls `left` and `right` and one ram (one or three throw at set-up) | Scene's |
| 8 (68) | Position markers | | `middle_pos`, `wait_pos`, `attacker_wait_pos` | Scene's |
| 9, 10 (69, 70) | Wall and corner towers | Platform for one machine; interior and stairs as entities | Machine slot, archer points | The bastions serve as pads |
| 12 (72) | Stairs | Own mesh and body with `has_steps`, written nested (line 379) | | Built; fix R1 |
| 13 (73) | Defender machine platforms | Level deck 5.50 x 5.72 m for a mangonel with crew and pile; no body in the 120 degree arc | Four `DeploymentPoint` `Side=Defender` a level, each with `mangonel_b_spawner` and `ballista_b_spawner` | Four pads 5.8 x 6.0 m, arcs probed clear |
| 14 (74) | Archer positions | Archer windows that are real openings | `archer_position`, `strategic_archer_point`, `arrow_barrel` | Owed: every window on a defence a real opening (his rule) |
| 15 (75) | Ladder landings | A deck that the 14 m ladder serves (10.1 to 13.6 m, median 11.8) | `siege_ladder_<n>m_spawner` with tags and ids; 4 a level | 12.0 m deck |
| 16 (76) | Siege tower landing | A deck that the 12 m tower serves (hinge 11.90 m) | `siege_tower_<n>m_spawner` with a path; 2 a level | 12.0 m deck |
| 17 (77) | Gatehouse | Room for two gates (vanilla gaps 7.15 to 9.81 m); flush deck; rock hole; merlons as entities | Gates, rock pile, targets, volume box | 9.0 m gap; hole present |
| 18 (78) | Outer gate | Opening for the vanilla leaves (4.70 m clear, 6.9 m high for european l2) | Vanilla gate prefab tagged `outer_gate`, `SideTag middle`, `CanBeDestroyedInitially` false | 5.2 x 7.2 m; R10 |
| 19 (79) | Inner gate | Inner opening (4.52 m for empire inner l3) | Gate tagged `inner_gate`, visible, one a level | 4.8 x 6.2 m |
| 20 (80) | Navmesh ids | Nothing | Castle side ids end in 1; ladder landings never | Scene's |
| 21 (81) | Rock hole | Deck at most about 14 m over the passage targets | `throwable_rock_pile`, `murder_hole_target` | 12 m drop |
| 22 (82) | Ram approach | Open ground at least 6.25 m to the field of the outer gate | `batteringram_a_spawner`, `GateTag outer_gate`, `SideTag middle`, a path | 31.5 m of approach; the rest point by lines 583 to 586 is KIT Y 18.75, the manifest's stop Y 15.7: UNVERIFIED (B2) |
| 23, 24 (83, 84) | Attackers' zone; spawn sets, navmesh | The field stays open | Deployment, `battle_set`, `navmesh.bin` | Scene's |
| 27 (87) | Prefab file and showcase scene | `taom_<kingdom>_kitbash.xml` after the import, Kit closed | Showcase scene in the Kit | Q8: `taom_erebor_kitbash.xml` exists and is not ours |
| 28 (88) | New scene or shared vanilla scene | | | Q13 |

**Numbers a builder needs, with their lines:**

| Number | Value | Line |
|---|---|---|
| Ladders | 6, 7, 8, 11, 14 m; the 14 m serves decks 10.1 to 13.6 m (median 11.8); no 10 or 12 | 410 to 427 |
| Siege towers | 5, 9, 12 m; hinges 5.072, 8.485, 11.900 m; the 12 m tower footprint about 6.8 x 6.8 m | 429 to 446 |
| The only dwarf scene with a siege set (`taom_dwarf_town_01_forceatmo`) | 7 m ladders, 9 m towers: a lower wall (first handover check M1) | `handover_check_erebor_gate/check_facts.md` |
| Gate leaves | european outer l2 4.695 x 6.838 m, hinges +-2.363; empire outer l3 5.321 x 5.921; empire inner l3 frame 4.521 wide, Z -1.80 to 5.77 | 487 to 493 |
| Swing | leaves turn to the castle side (-Y of the gate frame): european l2 sweeps X -2.85 to +2.85, Y -2.34 to +0.25 | 504 to 515 |
| Gate distance | 7.15 to 9.81 m | 522 |
| Gatehouse merlons | `DestructableComponent`, `DestroyedByStoneOnly` true, `CanBeDestroyedInitially` true, `MaxHitPoint` 1, a `debris_holder` tagged `reference` | 525 |
| Ram | rest point 6.25 m in front of the outer gate; ram body about 6.9 x 10.1 x 4.1 m | 574 to 587 |
| Mangonel | 5.50 x 5.72 m with crew and pile; 5.24 x 4.82 m turned; 120 degree arc; a ray of radius 0.295 m must hit no body | 624 to 642 |
| Rock piles | range 15 m; targets less than 75 degrees below the eye; a deck above about 14 m over the targets is too high | 646 to 651 |
| Rubble mound | gap at the deck about 12 m; path 35 degrees or less, 97 % of nodes under 32; body ramp 30 degrees or less | 333 to 347 |
| Stairs | `has_steps` nested in `<physics>`; Rivendell 0.25 m risers, 0.28 m treads, 48 risers for 12 m | 376 to 390 |
| Prefab cap | 131,072 across all loaded modules; a crossing asserts at Kit start | 725 to 734 |
| Frame | in FBX file axes (Y up) a point (x, y, z) arrives as (-x, z, y) x 0.01; in the KIT (Blender) frame that this handover uses, a point (x, y, z) arrives as (-x, -y, z) x 0.01 (the manifest's `coordinate_frame` says the same). The KIT field face +Y lands at SCENE -Y | 28 to 34; manifest |

**Which settlements use this kit.** Recorded: the live `TAOM_Map/ModuleData/settlements.xml` line 8926 has `town_E1`
"Erebor" (`Culture.erebor`), whose centre slots all name `sturgia_town_a` (read 2026-11-27). The Erebor culture has
4 towns, 9 castles and 39 villages, all on vanilla Sturgia scenes (`settlement-scene-needs.md` line 777). Two dwarf
scenes exist that no settlement loads: `taom_haterade_erebor_town_z` (his scene, files dated 2026-04-26, holds two
`siege_tower_12m_spawner` by `erebor-gate.md` line 57) and `taom_dwarf_town_01_forceatmo` (a siege set, proposed for
the Ered Luin capital `town_EL3` in `docs/features/ered-luin.md`). That the gate is for `town_E1`: UNVERIFIED (his
choice, Q13).

## Part B7. Risks and traps of this kit

| Risk or trap | Rule that avoids it |
|---|---|
| A stair flag written in a form that no prefab uses (R1) | Write `<physics shape="bo_x"><body_flags><body_flag name="has_steps" /></body_flags></physics>`; a test fails on any other form |
| Parapet parts without `mobility="1"` and `record_to_scene_replay` (R2) | Copy the control's part form; prove it in the siege test scene |
| "1:1" said with nothing measured (R7) | No round is shown without the P0 gate's numbers and overlays |
| Builder and critic in one script (R8) | One builder and one independent critic a step; no repair inside a check |
| Versions written over (R6) | A new folder for every round; never write over a result |
| A visible joint in the "one piece" (R4) | Cuts only along hidden lines; seam gate in both orders; judge culled, textured, lit |
| A statue sculpt on a soft text prompt | Tripo is fed renders of your own geometric blockout; the blockout carries the proportions |
| A picture sent to Tripo before he has seen it, or the API pool spent | Show him the exact renders first (Tripo doc "Rules for a session", rule 3); the Studio MCP only; the command line only on his word |
| A threshold tighter than the landmark noise | Two placers, the floor of B1b, each threshold at least twice the floor |
| A raw Tripo model torn by decimation | Voxel remesh before decimate; the raw v3.1 model faces +X (STATE pitfall 2) |
| Plates that read as a printed sheet | Single plates in full rows to the border, no gaps (his art rules) |
| His pictures sent to a service | Never: Tripo gets renders of your own model; web pictures stay local |
| A name of a film, studio, maker or person in a file | "The gate of Erebor", "his references"; marks on `ref_05`, `ref_08`, `ref_09` are never copied |
| Copying another maker's model part for part (handover 1, section 3) | Q1: proportions and outlines by overlay on his word; no picture becomes a texture |
| A door that the vanilla gate does not fit (Q7, R10) | Measure the door in P0; the outer gate opening at least 4.70 m clear and 6.9 m high |
| One big mesh drawing full detail all siege | Split the mass by where the eye comes near, along hidden lines (B1a) |
| The prefab cap | 6,193 spare on 2026-11-27 over five modules; `NavalDLC` (9,927) would cross it. On the day of the copy, count every module that the Kit's `rgl_log` lists, `NavalDLC` included (B1a) |
| A prefab that names a mesh not yet imported crashes the Kit | The prefab file goes in only after his import, with the Kit closed |
| The Kit lowercases names; a prefab lookup is case sensitive | Lower case for every piece, part and file name |
| A new material named like an old Erebor material binds to the old one | Material names `m_erebor_gate_*` or the kit's own new prefix; never change `Scenes/erebor` files |
| The review file saved from an empty factory scene opens with no window | `bpy.ops.wm.read_factory_settings(use_empty=False)` (STATE pitfall 1) |
| A window launched from a hidden desktop | Prove each window for him with `kingdom_kits/_shared/tools/capture_blender_windows.ps1` |
| Vertex colour layers on bodies | None on `bo_` (STATE pitfall 4) |
| The open rear courtyard closed again | Keep the flank split at abs(X) 39.2 m or more below Z 20 m (STATE pitfall 3) |
| A checker judging against rules, not his picture (the Rivendell broken wall passed 133 gate rows and did not look like the picture) | The critic's first input is his picture and the P0 overlay |
| Two sessions on `feat/kingdom-kits` | Stage explicit paths; never another session's hunks |

## Part A. How this work is done (the same for all three sessions)

This part is the knowledge of the Rivendell kit session, which ran from 2026-11-24 to 2026-11-27. The lead of that
session wrote it. It is the same text in the three handovers of 2026-11-27. Part B is about your kit only.

### A1. Who does what (models)

| Work | Who | Why |
|---|---|---|
| The lead: read the references, write the design language, talk to the maintainer, decide each finding, judge a dispute between a builder and a checker | You, the main session, on Claude Fable 5.2 | This is judgement, and a wrong judgement costs days |
| Builders and independent critics and checkers | The agent `kitbash-architect` (`.claude/agents/kitbash-architect.md`: Opus, high effort) | Every build and every check of the Rivendell kit ran on it. Its file holds the role, the order of work and the report form |
| Mechanical work: a record script, md5 lists, a doc lint, a proof run on a clean checkout, a render list | The agent `implementer` (Sonnet) or `fast-reader` (Haiku) | No judgement is needed |
| Fable for a subagent | Only after the same gate failed twice after an Opus repair, or two checkers disagree on a finding and pictures do not settle it | The repo rule: never routine |

Staffing rules: one builder and one independent critic for each step, and no repair inside a check. At most four
builders and one checker at one time. With "ultracode" on, a multi-agent step goes through the Workflow tool; with it
off, through the Agent tool. Every brief says: no git, no skills, no spawning, no command that needs a new permission
prompt (an agent hung for hours on one while the maintainer was away), and "if your files exist, a worker before you
stopped". Put the task and its authorization in the first lines of a brief: a workflow that started while a message of
the maintainer arrived handed that message to its agents, and they answered the message and built nothing.

### A2. What made the quality (keep all of it)

1. **The builder's gates are the critic's measures**: winding of each face, back hits of view rays, crossings, turns on
   smooth curves (4 degrees), open edges, coincident faces, the seam section. Each gate is proven on a control and on a
   fault that the worker plants. A zero from a gate with no planted fault is not evidence.
2. **A sheet prints measured values only**, and every picture has back faces culled (the engine culls them, Blender
   does not), the kit's textures, and a 1.8 m figure.
3. **The lead looks at the pictures and reads the result files itself**, then decides each finding with an id, then
   records, then shows the maintainer. An agent's "done" is a claim.
4. **After a context summary, read the files again.** A summary of the Rivendell session said that a decision was met;
   the check file said that it was not.
5. **When a builder and a checker disagree, order pictures and one more measure.** Do not side with the more
   confident one.
6. **The lead is wrong too.** Independent checks caught two wrong picks of the Rivendell lead in one morning. Let a
   checker judge what the lead built itself.
7. **A pass flag is a real boolean.** One result file wrote the text "False", which a program reads as true, and a
   failed gate stayed unseen for a night.
8. **Run the repo's whole tool suite before a kit's first commit.** The first Rivendell commit failed three repo gates.

### A3. The order of work (each step is a step of the Rivendell run)

1. **Read first**: `E:/repos/taom-kingdom-kits/docs/kitbash/kingdom-kits-prompt.md` (the job and its rules), `E:/repos/taom-kingdom-kits/docs/kitbash/kingdom-kit-layout.md` (folders, names,
   one asset a piece), `E:/repos/taom-kingdom-kits/docs/kitbash/settlement-scene-needs.md` (what each scene needs from a kit, with the engine lines),
   `E:/LOTRAOMAssets/_auto_workspace/kingdom_kits/rivendell/rivendell_reference.html` (the Rivendell run in 17 steps, each with a lesson and a best way forward),
   `E:/repos/taom-kingdom-kits/docs/kitbash/rivendell-siege-set.md` (the build record: search it with `grep -n '^## '`, never read it whole), the
   maintainer's art rules in the memory folder (`feedback_asset_art_rules.md` and the feedback cards beside it), and
   `docs/features/tripo-cli.md`.
2. **The references.** Look at every picture of the maintainer. Write the ten things that make this look. Then put
   the open questions to him on ONE lean page: each question with lettered options and your recommendation. The review
   of 2026-11-27 found that the Mirkwood and Lothlorien sessions asked him nothing and settled look questions by
   default.
3. **A design language**: numbers and sections where a builder must not go wrong, and free ranges where the design
   must be free. His direction: "When making buildings you are very precise. 0 or 1. Black or white. I think that is
   limiting your creativity ... The balance is key! ... Try and be more creative."
4. **Concept pictures early**, from generic words through the image connector. Show all of them. A look brief with
   two readings gets both.
5. **Massing forms**: many forms (Rivendell: 18 in one round), each a true solid form, on one sheet from fixed
   cameras, and in a Blender view file that he can orbit. An independent critic judges them beside his pictures. He
   keeps or rejects forms. Base methods carry the forms into builds (Rivendell: an arch outline, a free rib, a plate
   and gable frame, a turned solid; `E:/LOTRAOMAssets/_auto_workspace/kingdom_kits/rivendell/kit_full/v2_base`).
6. **FORMS GO INTO THE MODDING KIT AT ONCE.** His word of 2026-11-27 on the 18 Rivendell forms: "We are building a kit
   bash and an environmental level design set so that people can just plop down these buildings to make a scene. This
   is PERFECT for our sceners. Lets import it in. We can always update it as we go." So an approved set of forms
   becomes a first kit file the same day: final piece names from the first copy (a later build replaces the piece in
   place), one mesh a piece, the kit's own materials, UVs, front faces outward, an independent check before the copy.
   Its note says what it lacks (far levels, collision, detail). The Rivendell example: `E:/LOTRAOMAssets/_auto_workspace/kingdom_kits/rivendell/kit_full/forms_set_1`.
7. **Texture sets**: tiling sets at 1024 px and 0.45 UV a metre, and one trim sheet (`E:/repos/taom-kingdom-kits/docs/kitbash/tiling-texture-sets.md`).
   Substance Painter is ONE shared instance: claim the key `painter-instance` first, never close a project that you did
   not open. A set can also be made in pure code (the Rivendell wood: `E:/LOTRAOMAssets/_auto_workspace/kingdom_kits/rivendell/kit_full/design_v2/pilot_details/wood_tiling`).
8. **His reference scene as a blockout, cut into reusable pieces** (Rivendell: the valley scene,
   `E:/LOTRAOMAssets/_auto_workspace/kingdom_kits/rivendell/kit_full/design_v2/scene_valley`): the picture camera, a walk graph with stair and rail measures, an
   independent critic. This is the "environment" part of the end state: a scene builder gets the houses and also the
   terraces, stairs, bridges, towers and ground pieces that join them.
9. **The siege set, from the scene reference, not from memory**: the wall in the lengths that the scenes use, the
   broken wall with its two children and its tags, towers with a platform for a defender machine (5.50 x 5.72 m) and a
   stair, a gatehouse with room for an outer and an inner gate, the gate route. Facts that cost Rivendell time: the
   stairs flag `has_steps` lives in the PREFAB file, not in the FBX; a parapet part is its own entity; a window on a
   defence is a real opening; vanilla gate leaves need a passage near 4.7 m and swing to the castle side.
10. **Whole builds**: far levels (`.lod3`, `.lod6`), collision bodies (`bo_`), a budget taken from the first built
    levels, the gates of the kit tree. Then a **kit set**: a manifest, a copy tool with an undo list, a prefab file,
    and an independent check before any copy into the module.
11. **The Modding Kit and the game are the maintainer's steps.** He imports. A changed FBX with an old name shows no
    mark in the Kit: he uses the editor's rescan or reimport option. Until he has imported and looked, every claim
    about the Kit or the game is UNVERIFIED, and the record says so.

### A4. Tripo: use it freely

The maintainer, 2026-11-27: "Just tripo MCP at will we have a ton of credits." The Tripo Studio MCP is set up (server
`tripo`, skill `tripo-studio-mcp`, `docs/features/tripo-cli.md` section "Tripo Studio MCP"). So a generation needs no
question about credits. These rules stay: load the skill before the first call; prompts, pictures and file names are
generic; submit once with a saved idempotency key; no signed link in any file; add each job to the "Jobs" table of the
Tripo doc; a Tripo model is a source that becomes an asset only through `/prep-tripo-prop` and the mesh gates. Use it
for carved, organic and broken things (statues, finials, tree forms, rock, rubble). Exact modular architecture is
still built from solids. **His reference pictures go to no outside service unless he says so for that picture**: feed
Tripo with pictures that were made for the job, or with renders of your own model.

### A5. Limits that hold in every session

- Never name the bought source kit in any file ("a licensed elven building kit"). Its raw export is a donor only on
  the maintainer's recorded word for that use.
- No film, studio, maker or person is named in a file. A mark on a picture is never copied.
- `TAOM_Map` and `LOTRLOME_Armory` are live and unversioned: no test, trial or mutation run names a live game path as
  an output, a working folder or a target. A copy into the module is the lead's step, with the Kit closed when a file
  is replaced, after an independent check.
- All work files on `E:`, in `E:/LOTRAOMAssets/_auto_workspace/kingdom_kits/<kingdom>`. Never write in another session's folder or docs.
- Blender only through the lock tool `python E:/LOTRAOMAssets/_auto_workspace/kingdom_kits/rivendell/_tools/blender_one.py` (with `--wait-for`); it holds a job
  back when memory is short. Open a Blender window for the maintainer when a critic says "ready"; a window that he
  uses is his, never close it.
- No purchase. Never ask for or paste a key. Never stop a process that you did not start.
- Never edit on `bannerlord-1.5.x`. Kit docs live on the branch `feat/kingdom-kits` in `E:/repos/taom-kingdom-kits`,
  which several sessions share: stage explicit paths only. Land on trunk only after a proof on a clean checkout
  (the Rivendell pattern: `E:/LOTRAOMAssets/_auto_workspace/kingdom_kits/rivendell/_loop/session_save_2026-11-26/scratch/land_proven.sh`).
- Repo prose: no em or en dash, the commit form of AGENTS.md, a claim with its evidence, a time only after the clock
  was read.

### A6. Records (a session can stop at any moment)

After each result and before the next step: the kit's `STATE.md` (newest block on top, with what runs and what is
next), the kit's doc in `docs/kitbash`, the lessons, the lean review page for the maintainer (only the changes and
what you need from him), and the session's memory card. Write a repo doc with a script file, never with a long inline
command (the shell halves backslashes, and a command over 7,500 characters is blocked).


## Sources

Read for this handover (modified times as `ls` printed them on 2026-11-27):

| File | Modified |
|---|---|
| `E:/LOTRAOMAssets/_auto_workspace/kingdom_kits/erebor_gate/STATE.md` (whole) | 2026-11-27 09:39 |
| `W/review_now.html`, `W/review_now_first.html` | 2026-11-27 09:40; 2026-11-26 21:21 |
| `W/refs/owner_2026-11-26/SOURCE.txt` and the ten pictures | 2026-11-26 19:23 |
| `W/hero/v1/hero_v1_result.json`, `lod_compare_result.json`, `review_content_hero.json`; renders `hero_01` to `hero_10`; sheets `S_erebor_hero_siege_qa.png`, `S_erebor_hero_lod_compare.png` | 2026-11-27 06:04 to 09:39 |
| `W/kit_full/delivery/staged/` (`delivery_verification.json`, `fbx_roundtrip_audit.json`, `scene_wiring_manifest.json`, `OWNER_IMPORT_STEPS.md` head, `Prefabs/taom_erebor_great_gate.xml`) | 2026-11-27 06:04 to 06:07 |
| `W/control/ref_and_control_measurements.json`, `W/control/ref_grids/ref_04_grid.png` | 2026-11-26 19:39 |
| `W/design/carving_panel/S_carving_test_panel.png`, `carving_panel_result.json`; `W/massing/r1/renders/all_three_overview.png` | 2026-11-26 20:12 to 20:46 |
| `W/tripo/prepped/sentinel/statue_01_front.png`, `prep_statue_result.json`; `W/tripo/prepped/statue_axe/prep_statue_result.json`; `W/tripo/run_tripo_batch.py`; `W/view/pack_view_review_result.json` | 2026-11-26 20:26 to 2026-11-27 09:24 |
| `E:/repos/taom-kingdom-kits/docs/kitbash/erebor-gate-handover.md` (whole) | 2026-11-26 19:46 |
| `E:/repos/TAOM/docs/kitbash/erebor-gate.md`, `erebor-gate-design-language.md` (whole) | 2026-11-27 09:45 |
| `E:/LOTRAOMAssets/_auto_workspace/kingdom_kits/_shared/handover_check_erebor_gate/check_facts.md` (lines 1 to 60 and headings) | 2026-11-26 19:38 |
| `E:/repos/taom-kingdom-kits/docs/kitbash/settlement-scene-needs.md` (lines 1 to 212, 333 to 347, 376 to 446, 485 to 664, 725 to 780, 837 to 861) | 2026-11-26 19:31 |
| `E:/repos/taom-kingdom-kits/docs/kitbash/kingdom-kit-layout.md` (whole); `kingdom-kits-prompt.md` (lines 117 to 247) | 2026-11-26 17:09; 2026-11-27 09:30 |
| `E:/LOTRAOMAssets/_auto_workspace/kingdom_kits/rivendell/rivendell_reference.html` (titles, "The best way forward") | 2026-11-26 19:51 |
| `E:/repos/taom-kingdom-kits/docs/features/tripo-cli.md` (lines 60 to 104); `E:/repos/TAOM/docs/features/tripo-cli.md` (Jobs rows 187 to 191) | 2026-11-27 09:51; 09:45 |
| `E:/repos/taom-kingdom-kits/docs/kitbash/erebor/README.md` (head), `erebor-buildings.md` (head) | 2026-11-24 17:39 |
| Memory card `feedback_asset_art_rules.md` (lines 1 to 240) | 2026-11-27 08:39 |
| `.claude/agents/kitbash-architect.md` (frontmatter: tools Read, Write, Edit, Bash, Grep, Glob, Monitor) | 2026-11-26 15:58 |
| Revision of 2026-11-27 (after the independent check `E:/LOTRAOMAssets/_auto_workspace/kingdom_kits/_shared/handovers_2026-11-27/erebor_gate/check/CHECK_HANDOVER.md`; notes in the `revise` folder beside it): `W/kit_full/src/hero/erebor_hero_spec.py` lines 26 to 27; `W/kit_full/src/tests/test_erebor_prefab_xml.py` line 45; `W/kit_full/src/hero/generate_erebor_prefab_xml.py` lines 52 to 55, 124; `W/kit_full/delivery/staged/scene_wiring_manifest.json` (gates, ram, frame); the Gondor control prefab (flags and `mobility` by entity); `settlement-scene-needs.md` lines 26 to 34, 574 to 587, 724 to 734; the Tripo doc lines 65 to 135; `page_maker.py` lines 1 to 5; handover 1 lines 240 to 255 and its trunk copy; design language v1 lines 30, 34, 38, 132, 168; `ref_04.png`, `ref_09.png` | read 2026-11-27 10:24 to 10:42 |
| Live, read only: `TAOM_Map/ModuleData/settlements.xml` (Erebor rows), `TAOM_Map/Prefabs` and `Native/Prefabs` (flag form grep), `Lotraom_comprehend_Gondor_Wall_2_Complete.xml` (part form), `SceneObj/taom_haterade_erebor_town_z` (listing) | settlements 2026-11-25 17:05 |
| Commands run: the 32 tests (`32 passed`); `sha256sum` of the staged FBX and XML (match); `tools/check_prefab_budget.py` on five modules (124,879); my swatch and outline scripts (survey, sections 5 and 7) | 2026-11-27 about 09:55 to 10:01 |

**Not checked:** nothing ran in Blender (no mesh opened, no render, no probe); the texel density method of the
earlier build; whether the engine reads the attribute form of the stair flag (only the file convention was counted);
the LOD switch distances in the engine; anything in the Modding Kit or the game; the Tripo balance and the live list of
Studio MCP tools; whether the trunk commit `3615ec91d` is pushed; `fresh_session.md` of the first handover check; no
web picture was searched; the door and statue proportions of B4 marked "my readings" are estimates until P0; the
feature counts (none made; B1b); the noise floor of a landmark placed by reading a crop (P0 measures it); which point of
the ram the manifest's `stop_pos_blender_m` names (P5); whether the lead session has a web tool (B5 day 1 step 5).
