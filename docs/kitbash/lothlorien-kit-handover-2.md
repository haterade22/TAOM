# Lothlorien kit: handover 2 (for a new lead session on Claude Fable 5.2)

## Read this first

- **What this is.** The entry point for a new lead session that builds the Lothlorien settlement kit of TAOM. It holds
  the research itself, enough to know what to do first; step 1 of the first day (Part B5) names the files that you
  read before the first form. Part B is about this kit. Part A, at the end, is the method of the Rivendell kit and is
  the same in the three handovers of 2026-11-27.
- **When and who.** Written 2026-11-27, clock read at 09:56 CST, for the Rivendell kit session (its lead asked a
  `kitbash-architect` worker to write it). Revised at 10:16 CST after an independent check
  (`E:/LOTRAOMAssets/_auto_workspace/kingdom_kits/_shared/handovers_2026-11-27/lothlorien/check/CHECK_HANDOVER.md`;
  the revision note is `.../lothlorien/revise/REVISION.md` beside it). The working paper behind it, with every path
  and modified time, is `E:/LOTRAOMAssets/_auto_workspace/kingdom_kits/_shared/handovers_2026-11-27/lothlorien/SURVEY.md`.
- **Where the files are.** The repo root of this work is the kit worktree `E:/repos/taom-kingdom-kits` (branch
  `feat/kingdom-kits`). Every relative repo path in this handover (`docs/...`, `tools/...`, `.claude/...`) is under
  that root. The Lothlorien docs (`lothlorien-kit.md`, `lothlorien-kit-layout.md`, `lothlorien-design-language.md`,
  `lothlorien-design-amendment-1.md`, `lothlorien-lessons.md` and this handover) exist only there: `ls` of
  `E:/repos/TAOM/docs/kitbash` on 2026-11-27 shows only the first handover. `STATE.md` line 103 counts them among
  "uncommitted" link targets (UNVERIFIED: no git was run; step: `git -C E:/repos/taom-kingdom-kits status --short
  docs/kitbash/lothlorien*`). Never delete or recreate them elsewhere; their commit is the maintainer's word (A5).
- **It replaces the first handover as the entry point.** `docs/kitbash/lothlorien-kit-handover.md` (2026-11-26 19:46)
  stays as a source. What is still worth reading in it: section 3 (how the owner works, each line cost a round),
  section 6 (the Tripo CLI steps; the Studio MCP of Part A4 is now the first route), section 7 (Modding Kit and engine
  facts that were measured), section 8 (what went wrong in Rivendell). Part B carries the essentials of each.
- **Who worked before you.** A session of another AI client, from 2026-11-26 16:54 to at least 2026-11-27 09:42. The
  maintainer rejected four of its rounds and calls its state rough. Its records are honest about what it could not
  prove, but every owner decision in them is a paraphrase, and its last step uses a source for which no record holds
  his word.
- **The end state, in one sentence.** Lothlorien gets what Rivendell has: a design language that the maintainer
  approved from his pictures, placeable tree-city buildings and structures in the Modding Kit (tree connections,
  flets, stairs round trunks, bridges between trees), the kit's own texture sets and trim sheet, his reference scene
  built as a blockout and cut into reusable pieces with walks, a siege set that meets the checklist of
  `settlement-scene-needs.md` in the form that he decides for a living green wall (B1 row 5), and kit sets with far
  levels, collision bodies and a prefab file, each independently checked, with records from which a fresh session can
  resume.

**Two facts to act on before anything else.**

1. **Do not touch the raw export.** Since 2026-11-27 09:07 the earlier session read the raw assembled house of the
   licensed elven building kit (`E:/LOTRAOMAssets/_export/rivendell/assembled_reference/rivendell_house.fbx`; its
   probe and converted maps are in `lothlorien/kit_full/build/rivendell_mesh_r1`, newest file 09:42). No record holds
   the maintainer's word for that use (review finding M1; `lothlorien-kit.md` lines 833 to 848 give none). Do not read,
   copy or build from that export, or from `rivendell_mesh_r1`, until his exact words for it are written in the kit
   record. It is question Q1 of your first page.
   **The earlier session's reading may still be what he wants.** It read his last rejection, "another procedural form,
   not the requested kitbash" (a paraphrase), as a request to compose from the licensed kit's authored parts
   (`lothlorien-kit.md` lines 833 to 836; lesson L57: "B2's approved massing was still the wrong answer to a request
   for kit bashing"). Two facts bear on it: the licensed kit is a parts library of 458 meshes, about 150 of them
   building parts, with no house or hall shell (`rivendell-siege-set.md` line 2051); and Rivendell already uses outline
   data taken from it for its railing motif, kept outside the repo (`rivendell-siege-set.md` line 3334; first handover
   lines 176 to 180 add: "Whether the licensed kit's raw export is a donor of forms for Lothlórien is the owner's
   decision; do not use it on your own"). So this reading is a question for him (Q1, and Q2 option D), not a fault
   of the earlier session, and no form is built on it before his answer.
2. **B2 comes from the Rivendell build, not from the massing file.** The Rivendell session builds house B2 as a real
   kit piece, `rivendell_house_b2_a`, with UVs, levels and a body, in
   `E:/LOTRAOMAssets/_auto_workspace/kingdom_kits/rivendell/kit_full/house_b2`. Its newest result at 09:48 is round r8,
   status "built, gates pass": 28,708 / 18,500 / 9,504 triangles at levels 0, 3 and 6, body 1,164 (`house_b2_result.json`;
   `state.json` step `build_r8` gives the same triangles; r5's 24,684 / 14,020 / 8,920 is older). Its `state.json` "next" is "the lead's word
   on the requests (REQUEST_TO_THE_LEAD.md)": ten requests, among them a turret, an oriel and a link that the piece
   lacks, no stair to the gallery, no door leaves, and level shares 0.644 and 0.331 against aims of 0.40 and 0.18. Its
   `check/` folder was still changing at 10:09. Read `state.json`, `house_b2_result.json` and `REQUEST_TO_THE_LEAD.md`
   again on the day. A Lothlorien house that starts from B2 takes that built piece, with the maintainer's word
   (question Q3), and never `pilot_r2_massing.blend` (no UVs, 2,560 crossing pairs in its turret, review LC1). The
   maintainer rejected the Lothlorien copy of the massing.

**One more caution.** The earlier session may still be running: files in its folder changed until 09:42. Write your
new work in a new folder, `E:/LOTRAOMAssets/_auto_workspace/kingdom_kits/lothlorien/kit_v2`, keep `kit_full` as read-only
history, and ask the maintainer on your first page whether the other client's session has stopped.

## Part B1. The end state, as deliverables

| # | Deliverable | Proven done when |
|---|---|---|
| 1 | A design language for Lothlorien: the ten traits, a colour table measured from his pictures, exact numbers for joints and walks, free ranges for the look, and a list of what the pictures never have | His approval in his words is quoted in `docs/kitbash/lothlorien-kit.md`, with the date; an independent critic held it against his seven pictures first |
| 2 | A first set of placeable forms in the Modding Kit: tree connections (a flet ring round a trunk, a house on a bough, a house between trunks), stairs round trunks, bridges between trees, lantern pavilions, ground approaches, houses in the readings he keeps | The kit files are in `TAOM_Map/AssetSources/Scenes/Lothlorien/<family>/` with final names, an independent check passed before the copy, he imported them, and a read of the packages after his import (the Rivendell tool in Part B5 "Tools", adapted) found every mesh bound to its material. Until he has looked in the Kit: UNVERIFIED |
| 3 | The kit's texture sets (1024 px, 0.45 UV a metre, Part A3 step 7) and one trim sheet with modelled and baked relief | Each set judged on a member with back faces culled and a 1.8 m figure, then in the Kit beside a control; his choice recorded |
| 4 | His reference scene (the picture he names, question Q5) built whole as a blockout, cut into reusable pieces, with a walk graph | Every walk ends on a stair, a door or a deck; stair and rail measures printed; an independent critic compared it with his picture from its camera; his marks recorded |
| 5 | A siege set that meets the checklist of `settlement-scene-needs.md` (Part B6), in the form he decides for a living green wall (questions Q6, Q7) | Each row of the checklist has a piece, a test that fails on a planted fault, and its measured number; a siege run in a test scene is the maintainer's step and stays UNVERIFIED until he runs it |
| 6 | Kit sets: far levels `.lod3` and `.lod6`, `bo_` bodies, a budget taken from the first built levels, a manifest, a copy tool with an undo list, the prefab file `taom_lothlorien_kitbash.xml` | An independent check of each set before any copy; the prefab entity count of all loaded modules stays under 131,072 (`tools/check_prefab_budget.py`, one call a module, summed; with `NavalDLC` loaded the sum was already over the cap on 2026-11-26, Part B6 row 27) |
| 7 | Records from which a fresh session can resume | `STATE.md` in the work folder with the newest block on top; `lothlorien-kit.md`; the lessons; the lean page; the memory card. A test: a fresh agent reads only `STATE.md` and names the next step correctly |

## Part B2. What exists today

Work folder: `E:/LOTRAOMAssets/_auto_workspace/kingdom_kits/lothlorien` (WORK). Docs: `E:/repos/taom-kingdom-kits/docs/kitbash`.

| Item | Path | State (modified) | Keep, rework or redo | Why |
|---|---|---|---|---|
| His seven pictures | WORK/refs/owner_2026-11-26/ | 7 files, all different (11-26 16:46 to 16:48). Screenshot names, not ref_01..07. No `SOURCE.txt` | Keep as they are | The map to ids is in Part B4 |
| Settlement census | `lothlorien-kit.md` lines 52 to 70 | Read from the live `settlements.xml` 11-26, SHA-256 recorded | Keep; run again on the day | Agrees with `settlement-scene-needs.md` section 12 |
| Controls | WORK/kit_full/control/; the record is `lothlorien-kit.md` lines 72 to 106 | Mirkwood bridge, path and stair; editor tree (11-26 16:55 to 17:01). Numbers in Part B5 step 2b | Keep as measures | Mirkwood meshes are not donors: their upstream provenance is unresolved (lesson L51) |
| Design language v1 | `lothlorien-design-language.md` | Draft (11-26 21:11), never approved | Rework | Keep the picture readings, the palette (re-measured within one unit by the review), the exact and free split, the round joint system. Add look numbers and an exclusion list (LL4, LL6). Its siege numbers are wrong (LC2) |
| Amendment 1 | `lothlorien-design-amendment-1.md` | Sections 2 to 10 withdrawn by its own sections 11 to 14 (11-27 09:31) | History only | The "Mallorn Crown" scene and its colours were rejected with the pilot |
| Layout | `lothlorien-kit-layout.md` | Family table decided by the owner 11-26 (11-27 01:42) | Keep | His decision. Its LOD naming rule `piece.part.lod3` and the 63 character limit are proven by the standard audit |
| Massing round 1 | WORK/kit_full/design_v1/massing_r1/ | Three forms, 63/63/51 placements, seven views each (11-26 19:31) | History | Shown once (`review_now_first.html`), never answered; then "way off". Thin pointed trunks, small houses on a wide ring (I looked at the sheet) |
| Concept city and pilot | WORK/kit_full/design_v2/concept_r1/, build/pilot_r1/ | Withdrawn (11-26 23:38; 11-27 00:14) | History | Rejected: "extremely fantasy" (paraphrase) |
| Concept maps | WORK/kit_full/design_v2/concept_r1/textures/ | 23 maps at 1024 px: seven sets of colour, normal and roughness, and two concept heights (11-26 20:45) | Redo | Near flat: luminance spread 0.73 to 3.86 against 9.99 for the approved Rivendell wood; no tile size (LL5) |
| Trim bake | WORK/kit_full/design_v2/concept_r1/baked_trim/ | Five strips, modelled relief, normal and AO at 1024 (11-26 20:17) | Keep the method; redo the motifs with his choice | I looked at `trim_relief_detail.png`: shallow leaf loops and beads. He never saw them as a choice |
| Arcade, tree tower | WORK/kit_full/build/donor_r1, flet_r1, flet_r2 | Withdrawn (11-27 05:49 to 06:33) | History | Rejected. Gondor stone parts and a stone drum round a dead tree: none of the ten traits |
| B2 massing copy | WORK/kit_full/build/b2_donor_r1/ | Withdrawn (11-27 08:54) | Do not reuse | Rejected; the two copies differ in 1.2 % of pixels (LL1); no UVs |
| Raw export probe | WORK/kit_full/build/rivendell_mesh_r1/ | 09:07 to 09:42, no owner word | Do not touch | M1; see "Read this first" |
| Source and tests | WORK/kit_full/src/ (37 modules), src/tests/ (24 files) | 11-26 18:09 to 11-27 09:41 | Read, copy what earns it into `kit_v2` | Round joint, ring, bridge and scene-plan modules have planted-fault tests. Some tests cannot fail (LC5); `lothlorien_siege_plan.py` enforces wrong gate numbers (LC2); `lothlorien_piece_delivery.py` lets a body equal the mesh (LC4) |
| Owner pages | WORK/review_now.html (08:57), review_now_first.html (11-26 19:33) | Out of date | Replace with your first lean page | |
| Records | WORK/STATE.md (09:31), `lothlorien-kit.md` (09:31), `lothlorien-lessons.md` L1 to L57 (09:31) | STATE lines 54 to 72 show withdrawn work as verified (LC7); line 80 is false since 09:07 | Start a new top block in STATE.md | Lessons are useful history (list in Part B7) |
| Public issue | #1621 "Lothlorien tree-city kit: Caras Galadhon with siege"; draft WORK/issue/lothlorien_issue.md | Open | Keep; use it | Created with his approval 11-26 |
| Kit in the module | `TAOM_Map/AssetSources/Scenes/Lothlorien`, `TAOM_Map/Prefabs` | Nothing (craft reviewer listed 11-27 09:10) | Missing | |
| Rivendell B2 piece | `rivendell/kit_full/house_b2` | r8 "built, gates pass" (09:48); ten requests wait for the Rivendell lead (`REQUEST_TO_THE_LEAD.md`); `check/` changing at 10:09 | Take when its check passes, on his word (Q3) | One B2 build, in the Rivendell pipeline (LC1) |
| Review of 2026-11-27 | `_shared/reviews/2026-11-27/lothlorien/` (FEEDBACK, verify json, look and craft reports) | 09:14 to 09:27 | Read once | The confirmed findings are in Part B7 |

**The seven parts of the end state, today.**

| Part | What exists | Keep as it is | Rework, and why | Missing |
|---|---|---|---|---|
| 1 Design language | Draft v1, amendment 1 | Picture readings, palette swatches, three readings kept apart, exact rules for walks, the round joint system (15 degree grid, 6 m spiral of 36 risers at 5 degrees) | Numbers exist for joints only (LL6); no list of traits the pictures never have (LL4); the rejection words are paraphrased (LL2) | His approval |
| 2 Forms in the Kit | Nothing in the Kit | | Every built form was rejected | All |
| 3 Textures and trim | 23 concept maps, a trim bake | The bake method (modelled relief, baked normal and AO) | The field maps are flat and have no tile size (LL5); the motifs were never his choice | Approved sets |
| 4 Scene, cut into pieces | Massing round 1, the concept city (withdrawn) | The route-graph test idea (`lothlorien_scene_plan.py`) | No picture of his was named as the scene; built forms did not read as his pictures | All |
| 5 Siege set | `lothlorien_siege_plan.py` and its test | Nothing of its numbers | Gates 3.5 m apart (vanilla 7.15 to 9.81 m), openings 5.75 m (vanilla leaves need 4.70 and 4.52 m), no broken wall, no parapet parts, no tower platform (LC2) | All pieces |
| 6 Kit sets | Withdrawn pilot exports | The LOD name order and the export settings test | Body equal to the visual (rootwalk 7,440 = 7,440), house LOD6 57 % of base, trunk LOD3 = LOD6; no coincident or crossing gate (LC4) | All |
| 7 Records | STATE, kit record, lessons, two handovers | Lessons | STATE stale (LC7); no quotes of his words | A clean resume block |

## Part B3. The maintainer's words and decisions

Quoted where the record quotes him. "Paraphrase" means the record gives no exact words: when you show him your first
page, read the paraphrase back to him and record his own words.

| Date, time | Word or decision | Source |
|---|---|---|
| 2026-11-25 (Rivendell job) | Of a pavilion in his trim concepts for Rivendell: "More Lothlorien" (which picture: UNVERIFIED) | memory card `features/kingdom-kits.md` line 129 |
| 2026-11-26, about 16:28 | "Give me a handover for another session to pick up and start a lothlorien kitbash ... use these references to start a lothlorien one." "An environment in the trees basically." | `lothlorien-kit-handover.md` lines 17 to 19 |
| 2026-11-26, about 17:00 | DECIDED: kit structural trunks with exact joints, roots and bough stubs; leaves and the forest from the editor (paraphrase) | `lothlorien-kit.md` line 20 |
| 2026-11-26 | DECIDED: ref_01 (the domed city) is the ground-level civic core, below and between the trees (paraphrase) | `lothlorien-kit.md` line 210 |
| 2026-11-26 | DECIDED: Caras Galadhon with siege in round one (paraphrase) | `lothlorien-kit.md` line 227 |
| 2026-11-26 | DECIDED: folders `trunks`, `platforms`, `stairs`, `bridges`, `buildings`, `structures`, `props`, `textures` (paraphrase) | `lothlorien-kit.md` line 258; `lothlorien-kit-layout.md` line 3 |
| 2026-11-26 | DECIDED: a new public issue, #1621 (paraphrase) | `lothlorien-kit.md` line 278 |
| 2026-11-26 | "Ensure you update your documentation, lessons learned, knowledge is critical to this!" | `lothlorien-lessons.md` line 3 |
| 2026-11-26, evening | Asked for trim sheets, colours and a conceptual scene with Rivendell's creative freedom; a review of Gondor, Erebor and Rivendell, because the work is "way off where you need to be to do a full lothlorien kitbash..." | `lothlorien-kit.md` lines 396 to 398 |
| 2026-11-26 or 27 | Gave the Rivendell reference page; allowed bounded Claude reviews, not Codex (paraphrase) | `lothlorien-kit.md` lines 434, 547 |
| 2026-11-27, after 00:03 | REJECTED the four-piece pilot: "extremely fantasy and unlike the Rivendell, Gondor or dwarf kitbashes"; "looking like a child made it in Blender" (both paraphrase) | `lothlorien-kit.md` line 681; lessons L46 |
| 2026-11-27, after 05:52 | REJECTED the Gondor-part arcade: "extremely fantasy and unlike a proper kitbash" (paraphrase) | `STATE.md` line 29; `lothlorien-kit.md` line 747 |
| 2026-11-27, after 06:30 | REJECTED the curved tree tower "with the same criticism" (paraphrase) | `lothlorien-kit.md` line 791 |
| 2026-11-27, before 08:47 | CHOSE Rivendell B2 as donor, with reuse of its geometry and members (paraphrase) | `lothlorien-kit.md` line 805 |
| 2026-11-27, after 08:57 | REJECTED the copy of the B2 massing: "another procedural form, not the requested kitbash" (paraphrase, recorded at 09:31) | `lothlorien-kit.md` line 835; `STATE.md` line 7 |
| none recorded | The raw export of the licensed kit; the image service for this kit | grep of every Lothlorien doc (review M1, LL3) |
| 2026-11-27 | The request behind this handover (quote): "With all of your new found knowledge and the knowledge and documentation that the other sessions did (most of it turned out in a rough state unlike Rivendell) Please make me 3 handovers ... The end-state is to have the same thing happen with Rivendell (kitbash and already made environments,levels, structures, buildings) for Mirkwood and Lothlorien. ... Just tripo MCP at will we have a ton of credits." | His message to the Rivendell lead session, 2026-11-27; the Tripo sentence is also in `docs/features/tripo-cli.md` line 116 |

His words from the Rivendell job that hold here too, each with its source:

| Quote | Source |
|---|---|
| "When making buildings you are very precise. 0 or 1. Black or white. I think that is limiting your creativity ... The balance is key! ... Try and be more creative." | `rivendell-siege-set.md` line 1491; memory `feedback_precision_and_creativity.md` line 13 |
| "EVerything you showed off in the rivendell review_now.html is amazing and should be built out." | `kingdom-kits-prompt.md` line 56 |
| "We can have more than one wall type is what I am saying! The approved one is A and now we can have B" | `kingdom-kits-prompt.md` line 57; `rivendell-siege-set.md` line 1121 |
| "The goal here is to create the whole scene and be able to cut it up to be used elsewhere"; "We dont need the nature (trees, water, etc) We have that in the editor" | `rivendell-siege-set.md` line 1146 |
| "These should be made all together, no one is going to need stair railings. So stairs come with the railings." | `rivendell-siege-set.md` line 1013; memory `feedback_one_asset_a_piece.md` line 11 |
| "just provide me the html with the changes/what you need not all of the fluff" | `kingdom-kits-prompt.md` line 247; memory `feedback_lean_review_page.md` line 16 |
| On the 18 Rivendell forms, 2026-11-27: "people can just plop down these buildings to make a scene. This is PERFECT for our sceners. Lets import it in. We can always update it as we go." | `rivendell-siege-set.md` line 490; memory `feedback_kit_forms_into_editor_early.md` line 13 |

The memory files are in the session memory folder of the project `e--repos-TAOM` (under the user's `.claude/projects`).

**What he approved for this kit:** the five decisions of 2026-11-26 above, and B2 as a donor (its massing copy then
failed). **What he rejected:** four rounds. **What he liked:** nothing of Lothlorien is recorded as liked. The pictures
are his definition of right.

**Reading the rejections (mine, for you to put to him, not to settle).** The words "extremely fantasy", "unlike the
Rivendell, Gondor or dwarf kitbashes", "not a proper kitbash" and "another procedural form, not the requested
kitbash" have three readings. (a) The look was toy-like: the builds added glowing slabs that fill a frame, circle
motifs in every rail, gold hoops on roofs, smooth cylinder trunks with spike roots, thin single roof shells (I saw them
in the review's `look/refs_vs_builds_compare.png`). (b) The construction did not read as authored modular kit pieces
like the other kits. (c) "Kitbash" meant composing from the licensed kit's authored parts: the earlier session's
reading (`lothlorien-kit.md` lines 833 to 836, lesson L57; see "Read this first", fact 1). The arcade, the tree tower
and the B2 copy answered only (b), and each was rejected too. Note that the Rivendell forms that he loves are also
made by generator code, and that Rivendell's B2 is a generator build of the same form whose massing copy he rejected
at 08:57: so "procedural" alone cannot be the whole fault, and reading (c) cannot be ruled out. Ask Q1 and Q2 before
the first form (Part B5 step 6).

### What he has not been asked: ONE lean page

Put these on one page with the page maker:
`python E:/LOTRAOMAssets/_auto_workspace/kingdom_kits/_shared/review_page/page_maker.py <HH:MM> <content json> E:/LOTRAOMAssets/_auto_workspace/kingdom_kits/lothlorien/kit_v2`.
Give it the `kit_v2` folder: it writes `review_now.html` into the folder that it is given (`page_maker.py` line 21),
so the old page in WORK stays as history. The content file is JSON of the form
`{"needs": [...], "new": [...], "decisions": [...]}`, each entry an HTML fragment, pictures as paths relative to the
work folder (`page_maker.py` lines 3 to 4); a whole example is
`E:/LOTRAOMAssets/_auto_workspace/kingdom_kits/_shared/review_page/example_content_rivendell_2026-11-26.json`.
Each question with letters and a recommendation. Ask nothing that a file settles.

| Q | Question | Options | Recommendation |
|---|---|---|---|
| Q0 | Has the other client's Lothlorien session stopped? May the new session take over the work folder? | A yes, B no, it still runs (then the two split folders) | A, with new work in `lothlorien/kit_v2` |
| Q1 | The raw assembled house of the licensed elven building kit (and the kit's own pieces, a library of 458 meshes, about 150 building parts): a donor for Lothlorien? Rivendell already keeps railing outline data from it outside the repo | A no; B yes, its pieces may be reused with Lothlorien surfaces; C only to measure, no geometry | His call on the licence. Until he answers: A. If B, quote his words in the kit record first |
| Q2 | When you rejected the rounds as "extremely fantasy", "not a proper kitbash" and "another procedural form, not the requested kitbash", did you mean | A the look (toy shapes, glow panels, circle rails, spike roots); B the construction (not built like real kit pieces); C both; D compose the buildings from the licensed kit's authored parts (needs Q1 B) | My reading is C; D is the earlier session's reading and is possible. The forms wait for this answer (Part B5 step 6) |
| Q3 | B2 for Lothlorien. Rivendell's `rivendell_house_b2_a` is a generator build of the same form whose massing copy you rejected at 08:57 | A share Rivendell's built piece, Lothlorien adds only the tree connection and its surfaces; B no B2 in Lothlorien | A, as one type among the new forms, not as the kit's base; B if his Q2 answer is B or D |
| Q4 | May the session make concept pictures with the image connector (the ImagineArt MCP server `imagine`), from generic words, before geometry? He allowed it for the Rivendell kit work (memory card `features/kingdom-kits.md` lines 145 to 146), and `kingdom-kits-prompt.md` line 152 asks for his word in each session | A yes, all readings, shown all; B no | A. This was step 2 of the method and was never asked for this kit (LL3) |
| Q5 | Which picture is the scene to build whole and cut into pieces? | A ref_04, the city among many trunks; B ref_05, the spiral arcade round one trunk and its neighbours; C ref_07, the timber spiral by the water; D another picture that he gives | A: it holds the most pieces (pavilions at several heights, spirals, ramps on arches, bridges) |
| Q6 | Siege for Caras Galadhon. Option B changes your decision of 2026-11-26, "Caras Galadhon with siege in round one" (`lothlorien-kit.md` lines 225 to 231) | A keep the siege in round one, with vanilla gate leaves (route A: openings 4.70 m outer, 4.52 m inner, gates 7.15 to 9.81 m apart); B after the first forms set; C own leaves (route B, a Kit probe first) | B then A, as a change of his decision that he must confirm: the forms set goes into the Kit first, the siege set follows in the same week. Until he answers, his decision of 2026-11-26 stands |
| Q7 | What is a wall and a breach in a living green wall? | A a hedge on an earth bank with a timber walk on top; the breach a torn hedge and a slumped bank (Tripo); B a timber flet walk carried by a row of trees; the breach a broken walk and fallen boughs; C white stone only at the south-west gate (the ref_01 language), hedge and bank elsewhere | A with C at the gate: the book's fosse, green wall and white gates, and the engine's wall pieces (Part B6) |
| Q8 | Which reading first | A dark crown (ref_02, ref_03); B pale lace (ref_05, ref_06); C warm timber (ref_07); D all three as types `_a`, `_b`, `_c` | D, with A and B in the first round |
| Q9 | Two web links in `lothlorien-kit.md` (lines 236 to 240) carry the book author's surname in their titles | A replace the titles with plain words; B keep | A |

Decided already, do not ask again: kit trunks (2026-11-26), ref_01 as the ground civic core, the folder words,
issue #1621, every texture 1024 px, Tripo at will. Also decided, and asked again only because Q6 B would change it:
Caras Galadhon with siege in round one (2026-11-26).

## Part B4. The reading of the references

**The map.** The files keep their screenshot names in `WORK/refs/owner_2026-11-26/`:

| Id | File | What it shows (I looked at each on 2026-11-27) |
|---|---|---|
| ref_01 | `Screenshot 2026-11-26 164552.png` | A white domed city on cliffs with waterfalls, colonnaded drums, a bridge of one great arch, terraced stairs to the water. Not trees: his role for it is the ground civic core |
| ref_02 | `Screenshot 2026-11-26 164552 - Copy.png` | A tall house in a great tree at night: dark swept roofs with upturned points, tall glowing lancets with tracery, a railed walk along a bough to a small pavilion, blue lights |
| ref_03 | `Screenshot 2026-11-26 164714.png` | The same design as a print. It carries a title mark: never copy any mark or name from it |
| ref_04 | `Screenshot 2026-11-26 164733.png` | A city among many trunks at dusk: round lantern pavilions with domes at several heights, spiral stairs and long ramps on slender arches, gold leaves, blue glass |
| ref_05 | `Screenshot 2026-11-26 164752.png` | A drawing on brown paper: broad ground stairs to a huge fluted trunk, a walk of pointed arches that spirals round it, arcades to other trunks, a small pointed shrine between roots |
| ref_06 | `Screenshot 2026-11-26 1648125.png` | A white lace house in a tree at night: swept scale-shingle roofs with curled tips, lace gables, hanging pendants, a slatted stair down to round platforms, a small lace pavilion |
| ref_07 | `Screenshot 2026-11-26 164841.png` | Daylight: a wooden spiral stair with plain rails round a pale buttressed trunk standing in water; platforms and lace arches on other trees; warm lamps |

**The ten things that make this look.**

1. **The tree is the structure.** Houses wrap a trunk or ride a bough; walks run along boughs (02, 04, 05, 06, 07).
2. **The trees are vast.** In 05 and 07 a trunk is wider than a house is deep, fluted and buttressed, with roots and
   lianas. In 04 the trunks are tall straight columns, and a pavilion that wraps one is as wide as the trunk or wider
   (I looked at 04 on 2026-11-27). So trunk width against house width is a free range by picture, not one number.
3. **Tall and narrow.** Lancet and ogee openings several times taller than wide, in tiers (02, 03, 06).
4. **Flared concave roofs** with upturned tips, in two or three overlapping tiers like petals, with scale shingles
   (02, 03, 06).
5. **More opening than wall.** The wall is a frame of slender mullions and tracery with light behind (02, 05, 06).
6. **The rail is an arcade** of small pointed arches along every walk (02, 05); plain posts and rails in timber (07).
7. **Spirals and curved ribbons.** Stairs wind round trunks; walks curve from tree to tree on thin arches (04, 05, 07).
8. **Ornament only at focal places:** flame gables, leaf finials, pendants under decks (02, 06).
9. **A narrow colour hierarchy:** pale silver or ivory members, very dark roofs at night, warm glass, few blue lamps;
   brown wood only in 07.
10. **A forest at height:** many levels, small buildings against huge trees, a ground of roots, water and stone steps.

**Measured colours** (sRGB medians from his pictures, `lothlorien-design-language.md` section 6; the review
re-measured six of them within one unit): dark roof (10, 10, 5); pale gilded frame (214, 199, 158); warm window
(183, 168, 130); blue glass (112, 134, 146); white lace (188, 192, 192); white shingles (149, 151, 149); blue lamp
(181, 201, 212); stair wood (61, 34, 16); rail wood (79, 52, 31); pale bark (128, 127, 94); drawing bark
(122, 114, 99); gold leaves (135, 93, 34); warm lamp (245, 216, 170); ref_01 columns (122, 112, 85) and dome
(166, 148, 107). The earlier "silver bark" (147, 153, 140) is cooler and greener than any measured bark: show both.

**What no picture shows.** A wall, a gate, the fosse or any defence; a breach; the inside of a house; a stair foot
or a joint in detail; a house from behind; a whole plan of the city; how a spiral is carried in 02, 03 and 06 (posts
or cantilever); a roof drain. These are design questions for you to answer with forms, and to show him.

**Traits that no picture has (an exclusion list for every brief):** circle or ring motifs in rails; opaque glowing
slabs that fill a whole frame; gold hoops on roofs; smooth cylinder trunks; straight spike roots; single thin roof
shells; stone drums and conical slate caps round a tree; square stone keeps; equal grey boxes with a few windows.

**Free ranges to measure from his pictures before the first form** (the review's LL6; measure against a door or a
figure and print them as ranges): trunk width against house width; lancet height against width; mullions a bay; roof
tiers and flare; eave overhang; deck depth under a house; rail height and arch cell width.

## Part B5. The plan

### The first session day

| Step | What | Who | Shown to him |
|---|---|---|---|
| 0 | Read the clock. Claim: `python tools/work_claims.py claim lothlorien-kit --note "handover 2"` (exit 3: stop and ask). Check the newest mtime in WORK; if it is less than an hour old, the other session may run | Lead | |
| 1 | Read the files of Part A3 step 1 and this handover. Read `.claude/agents/kitbash-architect.md`, and section "Frames and units" of `lothlorien-design-language.md` (lines 95 to 100: the frame of a trunk, a ring segment and a bridge, below). Make `WORK/kit_v2` | Lead | |
| 2 | Write the lean page with Q0 to Q9, and read back the paraphrased decisions of Part B3 so that he can correct them. Put the ten traits and the exclusion list on it in one short block | Lead (page maker, into `kit_v2`) | Within the first hour |
| 2b | The control record, in `kit_v2/control/CONTROL.md`, from `lothlorien-kit.md` lines 72 to 106 and `kit_full/control/mirkwood_probe.json` and `uv_density.json` (numbers below). Each form brief of step 6 carries its budget | `kitbash-architect` builder | |
| 3 | Look numbers: measure the free ranges of Part B4 from his pictures (the pictures stay local; an agent may read them) | `kitbash-architect` builder | |
| 4 | Design language v2: the ten traits, the colour table, the exclusion list, the free ranges, the exact walk and joint rules kept from v1 (Part B6 numbers for siege). Write to `docs/kitbash/lothlorien-design-language-2.md` | Lead writes; a second `kitbash-architect` as critic holds it against the seven pictures | With the forms, not alone |
| 5 | If Q4 is yes: concept pictures through the image connector (the ImagineArt MCP server `imagine`) from generic words (no names): each reading in three tree connections; show all of them, two readings where a brief has two | Lead | As soon as made |
| 6 | Starts after his answers to Q1, Q2 and Q8. If steps 3 to 5 are done and he has not answered, build one form for each reading of Q2 that needs no Q1 answer (A, B, C) and keep D on the page as the open choice. Massing forms round 1: at least 12 forms as true solids, one sheet, seven fixed cameras (overview, eye level on the walk, front, back, side, top, below), a 1.8 m figure in each, back faces culled, an orbit `.blend`. Families: flet ring round a trunk (full and partial), house on a bough, house between two trunks, stair round a trunk (arcade and timber), bridge between trees (three sections), lantern pavilion, ground approach between roots, a kit trunk with ring seats | Up to three `kitbash-architect` builders (one family group each, own scratch folders); one critic beside his pictures | When the critic says ready: the sheet, the orbit file, a Blender window |
| 7 | Trunk skin: one Tripo job for a buttressed pale trunk with roots (picture made for the job, or a render of the code trunk), to be read as a height field on the code trunk. The method for a flat face is in `docs/features/tripo-cli.md` lines 319 to 326 (render the model straight on, with no perspective and no light, read the distance to the viewer as a height field, move a grid by it). Round a trunk it needs a cylindrical read about the trunk axis: no record proves that (UNVERIFIED; the smallest step is one trial on a 2 m ring of the code trunk) | Lead runs the MCP; a builder measures the model | Beside the forms |
| 8 | Record: STATE top block, `lothlorien-kit.md`, lessons, memory card | Lead | |

**The control numbers for step 2b** (`lothlorien-kit.md` lines 84 to 92, the Mirkwood kit as measured on 2026-11-26;
the shares are computed from those counts):

| Control | Level 0 | `.lod3`, share | `.lod6`, share | `bo_` body, share | Texel density |
|---|---|---|---|---|---|
| `st_mirkwood_bridge_a03` | 25,268 | 12,633, 0.50 | 2,526, 0.10 | 2,526, 0.10 | tiling stone 0.333 UV a metre; tree and stair atlases about 199 and 304 px a metre |
| `st_mirkwood_wood_path_a01` | 888 | 360, 0.41 | 72, 0.08 | 72, 0.08 | about 0.25 UV a metre |
| `st_mirkwood_stairs_a01` | 1,104 | 552, 0.50 | 110, 0.10 | 164, 0.15 | unique stair atlas |

Beside them, the Rivendell building driver's aims: `.lod3` at 0.40 and `.lod6` at 0.18 of level 0, a body at 0.03
(`rivendell/kit_full/house_b2/REQUEST_TO_THE_LEAD.md` item 10), and a house budget of 32,000 / 24,000 / 16,000
triangles (`house_b2/house_b2_result.json`, key `triangles.budget`). This kit's maps: 1024 px at 0.45 UV a metre, about
461 px a metre (Part A3 step 7). A form round is massing only; the budget goes into each form's brief so that a kept
form can be built inside it.

**The frame of a piece** (`lothlorien-design-language.md` lines 95 to 100, v1, kept): metres, Z up. A trunk's origin
is at its ground centre; angle 0 points along local +X, angles increase counterclockwise seen from above. A ring
segment's origin is on the walking centreline at its start joint and deck top; local X follows the tangent forward,
local Y points radially outward. A bridge's origin is at the centre of its start socket, local X leaving the ring
along the bridge. Write the frame of every other family (house, open, small) into design language v2 before its first
form. The engine turns a file point (x, y, z) into (-x, z, y) x 0.01 (Part B6).

**Tools** (each path read on 2026-11-27):

| Need | Path |
|---|---|
| Massing helper: plates, lathes, lofts, cameras that frame a point set, triangle counts, mirror error | `E:/LOTRAOMAssets/_auto_workspace/kingdom_kits/rivendell/kit_full/design_v2/massing/_scratch/mlib.py`, with `make_sheet.py` and `job_massing.py` beside it (copy into `kit_v2`; they are wired to Rivendell) |
| A round 2 massing job of one family, with its sheet and `massing_result.json` | `rivendell/kit_full/design_v2/massing_house_eave_r2/` (`_scratch/job_massing.py`, `S_house_eave_r2_massing.png`) |
| Forms into a first kit file: build, sheet, rules, planted faults | `rivendell/kit_full/forms_set_1/jobs/job_build_piece.py`, `job_render_sheet.py`; `forms_set_1/tools/forms_rules.py`, `mutants.py`, `name_scan.py` (a run was still writing reports at 10:09) |
| Prove a Blender window for him | `E:/LOTRAOMAssets/_auto_workspace/kingdom_kits/_shared/tools/capture_blender_windows.ps1 <folder>` |
| Level chains of an FBX | `E:/repos/taom-kingdom-kits/tools/audit_fbx_lods.py` |
| Read the packages after his import (B1 row 2) | `rivendell/kit_full/editor_set/tools/check_kit_set_after_import.py` (copy and change: wired to Rivendell) |
| The lean page | `_shared/review_page/page_maker.py` (Part B3) |
| Blender lock | `rivendell/_tools/blender_one.py` (Part A5) |

If he keeps forms the same day, they become a first kit file at once (Part A3 step 6): final names, one mesh a piece,
the kit's materials, UVs, front faces outward, an independent check, then his import.

### Later phases, in order, each with its gate

| Phase | Work | Gate before the next phase |
|---|---|---|
| 1 Forms in the Kit | `lothlorien_<family>_kit.fbx` files for the kept forms, a manifest, the copy tool of Rivendell (`rivendell/kit_full/editor_set/tools`, copy and change: it is wired to Rivendell), his import | Independent check before the copy; his import; a read of the packages |
| 2 Texture sets and trim | Tiling sets for bark, pale wood, dark shingle, pearl shingle, warm timber, moonstone (1024 px, 0.45 UV a metre, record the metres a tile); one trim sheet planned from the member list, modelled, baked. Painter only with the claim `painter-instance`; a set may be made in code | Each set on a member, culled, with a figure; his choice; then the Kit beside a control |
| 3 The scene | The picture of Q5 read part by part, its camera matched, sizes taken against the figure, a plan with cuts, a walk graph (every walk ends on a stair, door or deck), built as blockout and cut into pieces | Critic from the picture's camera; his marks |
| 4 Details | The members and focal ornament of the kept forms at 5, 15 and 30 m, beside his marks. Tripo for finials, pendants, lantern bodies, bark | Critic beside the marks; his marks |
| 5 Siege set | From Part B6 and his answers to Q6 and Q7: wall lengths, the broken wall with its two children, parapet parts as entities, tower with a 5.50 x 5.72 m platform and a stair, a gatehouse for two gates, the gate route, stairs with their own bodies | Each checklist row has a test that fails on a planted fault; critic; his look |
| 6 Whole builds | `.lod3`, `.lod6`, `bo_` bodies, budgets from the first built levels and the control, coincident and crossing gates | Independent check of every piece |
| 7 Kit set and prefab | Manifest, copy with an undo list, prefab file `taom_lothlorien_kitbash.xml` after his import with the Kit closed; `has_steps` in the prefab | Prefab count against the cap; independent check; his word for the copy |
| 8 Landing | Docs on `feat/kingdom-kits`, the repo's whole tool suite, a proof on a clean checkout | All gates green; his word for commit and push |

**Where Tripo is used.** Trunk bark and root skin (as a height field on an exact code trunk); bough forks; finials,
leaf pendants and lantern bodies; the torn hedge, slumped bank and fallen boughs of a breach; ground roots between
which the stair climbs. Never for a floor, a stair, a rail, a joint or a mirror pair.

## Part B6. The scene research for this kit

From `docs/kitbash/settlement-scene-needs.md` (version 2, 2026-11-26 19:05; read its section 1 whole before any
siege piece). Section numbers in the last column are that file's.

| # | Thing | The kit delivers | The scene builder adds | For Lothlorien | Source |
|---|---|---|---|---|---|
| 1 | Centre scene | Its prefab file and showcase scene | One `center` scene for visit and siege of all three wall levels, named `taom_<kingdom>_<kind>_<name>_forceatmo` | Caras Galadhon loads `battania_town_a`, used by no other culture: a new scene is the clean route | 2.1, 11.3, 12 |
| 2 | Three wall levels | One wall height and one gatehouse for all three (RECOMMENDATION) | `<levels>` on each siege entity | Same | 2.2, 2.4 |
| 3 | Solid wall | Seamless pieces, deck 12.0 m over the ground line, a foundation, a `bo_` body, levels, the railing as a sub-object | Nothing | The living wall's walk (Q7) | 2.4, 3 |
| 4 | Parapet parts | Each its own mesh `_mN` and body; in the prefab a `DestructableComponent` and a `debris_holder` tagged `reference` | `tower_merlon` where a tower ramp lands | Needed where a tower lands, in any material | 1.1, 3.4 |
| 5 | Broken wall | A separate piece; gap about 12 m at the deck; a mound that troops walk: path 35 degrees or less, body ramp 30 degrees or less | | Its look (Q7) | 4.4 |
| 6 | Breach wall tree | Three `WallSegment` scripts (root, solid, broken child); 16 entities for a 20 m wall with six parts | Tags, nine navmesh ids, `visible="false"` on the broken child, markers | Same | 1.1, 4.1 |
| 7 | Two sided breach walls and a ram | | Exactly two walls `left` and `right` and one ram each siege level | Same | 4.2 |
| 9, 10 | Wall and corner tower | Mesh and body; doors on two sides (Rivendell 1.4 m clear); a platform for one machine; stairs as their own entities | Machine slot, archer points | A tree platform can be the tower (Q7) | 5.1 |
| 12 | Stairs | Own mesh and body with `has_steps` (`HasSteps = 0x800u`, `BodyFlags.cs:23`); the flag is written in the PREFAB file | The flag where a kit did not ship it | Every spiral and ground stair | 5.2 |
| 13 | Defender machine platforms | A level deck of 5.50 x 5.72 m (mangonel, crew, pile), no body in the 120 degree arc to the field | Four `DeploymentPoint` a level, each with a mangonel and a ballista spawner | Four a level | 8.1 |
| 14 | Archer positions | Archer windows with standing room (Rivendell: 0.8 m deep, 2.0 m high) | `archer_position`, `strategic_archer_point`, `arrow_barrel` | A window on a defence is a real opening | 5.1, 8.3 |
| 15 | Ladder landings | A deck that the 14 m ladder serves: vanilla 10.1 to 13.6 m, median 11.8 | Four ladder spawners a level with tags and `OnWallNavMeshId` | 12 m deck | 6.1 |
| 16 | Siege tower landing | A deck that the 12 m tower serves: ramp hinge 11.90 m, footprint about 6.8 x 6.8 m | Two tower spawners a level with a `<path>` | 12 m deck | 6.2 |
| 17 | Gatehouse | Room for an outer and an inner gate: vanilla gates 7.15 to 9.81 m apart; a flush deck with a rock hole over the room; merlons as entities | Gates, rock pile, targets | The south-west gates between overlapping wall ends (the book) | 7.3 |
| 18 | Outer gate | An opening of 4.70 m clear and 6.9 m high for `european_castle_gate_outer_l2`; leaves swing to the castle side, swept box X -2.85 to +2.85, Y -2.34 to +0.25, leaf top 6.822 m | A vanilla gate (route A), tagged `outer_gate` | Route A until he says otherwise | 7.1, 7.2 |
| 19 | Inner gate | An opening of 4.52 m (empire inner l3; frame to 1.8 m below its origin) | A gate tagged `inner_gate`, visible, one each siege level | Same | 7.1, 7.2 |
| 20 | Navmesh ids | Nothing | Castle-side ids end in 1; ladder landings never | Same | 3.1, 7.1 |
| 21 | Rock hole | The hole; a deck at most about 14 m over the targets (15 m range, eye 1.6 m assumed) | Pile, targets, volume box | Same | 8.2 |
| 22 | Ram approach | Open ground at least 6.25 m in front of the outer gate | Ram spawner with `GateTag outer_gate` | The white bridge must not end inside this | 7.5 |
| 23 | Attackers' zone | Open field: about 100 m for archers, 135 m for trebuchets, 150 to 215 m for tower and ram paths | Deployment points and boundaries | The fosse and forest are editor work: keep the field open | 9 |
| 24 | Spawn sets and navmesh | | One `battle_set` in a castle, two in a town; a hand-made `navmesh.bin` | Same | 2.3 |
| 25, 26 | Civilian layer, interiors | Streets, houses; interiors if the kingdom gets its own | Spawn tags, keep scenes with `FightAreaMarker` | Interiors: a question for later | 10 |
| 27 | Prefab file and showcase | `TAOM_Map/Prefabs/taom_lothlorien_kitbash.xml` after his import, Kit closed; a count against the cap (6,193 spare on 2026-11-26 for five modules; with `NavalDLC` (9,927) the sum is 134,806, over the cap) | Showcase scene `mod_taom_lothlorien_kitbash` | Count every module that the Kit loads again on the day (the list is in the session's `rgl_log`); if `NavalDLC` loads, there is no room for any new prefab entity until the maintainer decides | 11.1, 11.2 |
| 28 | New scene or shared edit | | | `battania_castle_a` is shared by 8 settlements of 5 cultures, `_b` by 7 of 4: edit neither; build new scenes | 11.3 |
| 29 | A defence that is not masonry | Any look, with the engine's parts of rows 3 to 22 | | This is Lothlorien's case: the mesh is the look; the engine needs the parts | 16 |

**More numbers a builder needs.** The engine reads a kit FBX turned: a file point (x, y, z) arrives as (-x, z, y) x
0.01 ("Frames"). Ladders exist as 6, 7, 8, 11 and 14 m, no 10 or 12 (6.1). The least walk width and breach gap that the
AI takes are not in any record: Rivendell's walk is 4.27 m clear, Gondor's 6.0 m (16). Rivendell stair pattern: risers
0.25 m, treads 0.28 m, 48 risers for 12 m (5.2). Levels of detail switch by distance only: the full mesh to 30 m,
`.lod3` from 30 m, `.lod6` from 130 m (first handover section 7). The earlier Lothlorien joint system: a 6 m spiral of
36 risers of 0.166667 m, 5 degrees a step, walking radius 3.30 m, tread 0.287979 m (design language section 4): a
design value, not an engine-proven width. A fall is real damage (`Mission.cs:6132-6153`, cited by the earlier session,
not re-read): rails wherever a fall is possible. A prefab can hold `light_component` children
(`Native/Prefabs/props_other.xml`, cited, not re-read); no Lothlorien light was tried.

**The book on Caras Galadhon** (read by the earlier session, `lothlorien-kit.md` lines 234 to 247): a deep dry fosse,
a high living green wall beyond it, a white bridge, great gates facing south-west between the overlapping ends of the
wall, many stairs, silver lamps, a fountain. The editor owns the fosse, terrain and foliage.

**Which settlements use this kit** (`lothlorien-kit.md` lines 59 to 70, read from the live `settlements.xml` on
2026-11-26; agrees with `settlement-scene-needs.md` section 12: 1 town, 3 castles, 15 villages):

| Settlement | Id | Centre scene today | Shared with |
|---|---|---|---|
| Caras Galadhon | `town_L1` | `battania_town_a` at every level | Lothlorien only; also the story mission `lorien_defend` ("Lothlorien: hold the borders", `Main/_Module/ModuleData/story_missions/story_missions.json` lines 136 to 138; `docs/features/story-missions.md` line 368 calls it a stand-in scene) |
| Cerin Amroth | `castle_L1` | `battania_castle_a` | 8 settlements of 5 cultures |
| Talas Duiren | `castle_L2` | `battania_castle_b` | 7 settlements of 4 cultures |
| Tol Calan | `castle_L3` | `battania_castle_a` | as above |
| 15 villages | Glorinant, Tawarmir, Lasrond (`battania_village`); Mallorlas, Imloth, Sergalad (`_e`); Anglhad (`_g`); Nimphroth, Iauphen (`_h`); Celebloth, Naerondol (`_i`); Glanlin, Anglond (`_j`); Idhralas (`_k`); Lothbar (`_l`) | stock Battania village scenes | |

All four fortifications also reuse the Battania keep interiors and dungeon; Caras Galadhon the Battania arena, tavern
and houses. UNVERIFIED today: the census was not run again on 2026-11-27; run `python -I culture_scenes.py "$MODS" <out.json>`
from `E:/LOTRAOMAssets/_auto_workspace/kingdom_kits/_shared/scene_needs/scripts`. A new scene for the town replaces the
story mission's stand-in by an edit of that row. The `scene_name` edit in the live `settlements.xml` is the maintainer's
step.

## Part B7. Risks and traps of this kit

| Trap | Evidence | The rule that avoids it |
|---|---|---|
| A source with no owner word | M1: raw export read since 09:07; STATE line 80 says the opposite | Use a donor only after his words are quoted in the kit record |
| The earlier session may still write | Files changed to 09:42 | New work in `kit_v2`; Q0 on the first page |
| Two sessions build one piece | LC1: B2 in Rivendell and in Lothlorien | One build of a shared piece, in the owning kit; take it when it passes |
| The same family in the Mirkwood kit | Mirkwood is the other forest kingdom: its handover 2 (`docs/kitbash/mirkwood-kit-handover-2.md`) plans root bridges, a rock-cut stair, trunk columns and lanterns; both kingdoms load the shared `battania_castle_a` and `_b` (`settlement-scene-needs.md` section 11.3) | Before a family with a match there (a bridge between trees or roots, a stair round a trunk, a lantern, a trunk column), read that handover and Mirkwood's `STATE.md` (`E:/LOTRAOMAssets/_auto_workspace/kingdom_kits/mirkwood/STATE.md`), agree with the Mirkwood lead which kit owns the piece, and write the owner into both kit records. Neither kit edits a shared vanilla scene (Part B6 row 28) |
| Look settled by default; nothing asked | LL3, M2: no concept pictures, no checker beside his pictures before four owner pages | One lean page first; an independent critic beside his pictures before every page |
| One form a round | LC3: the arcade was one geometry in two finishes; the tree tower one shell | Twelve or more forms on one sheet; a finish is not a form |
| Two pictures he cannot tell apart | LL1: 1.2 % of pixels differ | A page shows real options only |
| Toy traits | LL4: glow slabs, circle rails, roof hoops, spike roots, thin shells | The exclusion list in every brief |
| Numbers for joints only | LL6 | Free ranges measured from his pictures |
| Rejections answered with technical repairs | L54: three rounds answered a look rejection with arc fits and measures | Ask what he meant; show both readings |
| Siege numbers from the Gondor control | LC2: openings 5.75 m, gates 3.5 m apart; the test enforces them | Test first with 4.70 / 4.52 m openings and 7.15 to 9.81 m between gates; a broken wall, parts, a tower platform |
| Gates that cannot fail | LC5: constants tested against the same literals; a hash equal by construction | Every gate proven on a planted fault; build scripts in `src`, never `_scratch` |
| Preflight blind to coincident faces and crossings | LC4: arcade 535 coincident faces and 13,995 crossing pairs under a PASS (craft reviewer's count, not re-measured) | Add both gates, each with a planted fault |
| Loose bodies and levels | LC4: body = visual (7,440); LOD6 57 % of base; LOD3 = LOD6. Mirkwood control bodies are 10 %, 8 % and 15 % of level 0 (bridge, path, stair; `lothlorien-kit.md` lines 86 to 88) | A body and level budget from the control before the first export (Part B5 step 2b) |
| Flat field maps | LL5 | Record metres a tile; start from an approved set; Tripo relief for bark |
| The source kit's name in a file | Lesson L57 and `lothlorien_rivendell_kit.py` line 59 quote the licensed kit's own content folder path | Never copy that path or name into a new file, prompt or page; say "a licensed elven building kit" |
| A person's name in a link title | LC10: `lothlorien-kit.md` lines 236 to 240 | Q9; write plain titles in new files |
| Maps from another kit's trial folders | LC6: B2 maps from Rivendell `_trial_staging` | Point at a kept texture folder or copy once into `kit_v2` |
| Test temp folders in the work root | LC8 | Use the scratchpad or `_scratch` |
| A stale resume file | LC7 | Mark history as history; the top block names the current sources |
| A scripted open is not a window | L56, L22, L23: two GUI launches exited before a capture | Prove a window with `E:/LOTRAOMAssets/_auto_workspace/kingdom_kits/_shared/tools/capture_blender_windows.ps1`; after one unexplained exit, stop and ask, never loop |
| The lock tool and paths | Lesson L52: a wrong `--wait-for` path gave `NO BLENDER PROCESS SEEN` after the job succeeded; `lothlorien-kit.md` lines 451 to 453: the lock tool does not put the script's folder on Blender's path | Read the job's report file; give each driver an entry point that adds its source path |
| LOD name order | L31: `piece.lod3.part` read as a base part by `tools/audit_fbx_lods.py` | `piece.part.lod3`; names at most 63 characters (layout) |
| Donor details | L49 joists against the floor slab; L52 the curved stair id keeps its spelling `gondor_stairs_cuv_3m_a_clean`; a vanilla PSD for `plank_c_trunk` is missing | Measure a donor's walking surface; keep source ids as they are |
| Mirkwood geometry | L51: upstream provenance unresolved | Comparison only, never a donor |
| Emissive glass and alpha-card lace | First handover section 7: the Rivendell railing card had the wrong cut-off in the Kit; no Lothlorien light was built | Geometry lace near the eye; prove one card and one light in the Kit before a family depends on it |
| A paid reviewer that ends with nothing | `lothlorien-kit.md` line 547: one bounded call spent its budget with no findings | Use the `kitbash-architect` agent as critic (Part A1) |

---

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

Modified times read with `ls` on 2026-11-27 between 09:50 and 09:55.

| File | Modified |
|---|---|
| `E:/LOTRAOMAssets/_auto_workspace/kingdom_kits/lothlorien/STATE.md` | 11-27 09:31 |
| `.../lothlorien/review_now.html`; `review_now_first.html` | 11-27 08:57; 11-26 19:33 |
| `.../lothlorien/refs/owner_2026-11-26/` (7 pictures, each looked at) | 11-26 16:46 to 16:48 |
| `.../lothlorien/issue/lothlorien_issue.md`; `_tools/view_b2.py` | 11-26 18:06; 11-27 08:52 |
| `.../lothlorien/kit_full/` (control, design_v1, design_v2, build, src: listed; `S_lothlorien_massing_r1.png` and `trim_relief_detail.png` looked at) | 11-26 16:55 to 11-27 09:42 |
| `docs/kitbash/lothlorien-kit.md` (read whole) | 11-27 09:31 |
| `docs/kitbash/lothlorien-design-language.md` (read whole) | 11-26 21:11 |
| `docs/kitbash/lothlorien-design-amendment-1.md` (read whole) | 11-27 09:31 |
| `docs/kitbash/lothlorien-kit-layout.md` (read whole) | 11-27 01:42 |
| `docs/kitbash/lothlorien-lessons.md` (headings, L46 to L57) | 11-27 09:31 |
| `docs/kitbash/lothlorien-kit-handover.md` (read whole) | 11-26 19:46 |
| `_shared/reviews/2026-11-27/lothlorien/FEEDBACK_LOTHLORIEN.md`, `verify_lothlorien.json`, `look/REVIEW_LOTHLORIEN_LOOK.md`, `craft/REVIEW_LOTHLORIEN_CRAFT.md`, `look/refs_vs_builds_compare.png` | 09:14 to 09:27 |
| `docs/kitbash/settlement-scene-needs.md` (sections 1 to 16) | 11-26 19:31 |
| `docs/kitbash/kingdom-kits-prompt.md` (whole); `kingdom-kit-layout.md` (whole) | 11-27 09:30; 11-26 17:09 |
| `kingdom_kits/rivendell/rivendell_reference.html` (text of steps 1 to 15) | 11-26 19:51 |
| `kingdom_kits/rivendell/kit_full/house_b2/state.json`; `forms_set_1/NAMES.md` (grep) | 11-27 09:48; 09:43 |
| `docs/features/tripo-cli.md` (section "Tripo Studio MCP") | 11-27 09:51 |
| `Main/_Module/ModuleData/story_missions/story_missions.json` lines 136 to 145; `docs/features/story-missions.md` line 368 | not read for time |
| Memory: `feedback_asset_art_rules.md`; `features/kingdom-kits.md` (lines 94 to 135) | 11-27 08:39; 11-27 05:51 |
| Read for the revision, 2026-11-27 10:09 to 10:16: `rivendell/kit_full/house_b2/state.json`, `house_b2_result.json`, `REQUEST_TO_THE_LEAD.md` | 09:48 (`check/` 10:09) |
| `rivendell-siege-set.md` lines 490, 1013, 1121, 1146, 1491, 2051, 3334; `kingdom-kits-prompt.md` lines 56, 57, 152, 247; first handover lines 170 to 180 | not read for time |
| `lothlorien-kit.md` lines 18 to 21, 72 to 106, 225 to 231, 679 to 683, 803 to 807, 825 to 848; `lothlorien-lessons.md` L57; `lothlorien-design-language.md` lines 95 to 100; `lothlorien-kit-layout.md` (whole) | not read for time |
| `settlement-scene-needs.md` lines 724 to 741; `docs/features/tripo-cli.md` lines 317 to 334; `.claude/skills/prep-tripo-prop/SKILL.md` (grep) | not read for time |
| `_shared/review_page/page_maker.py` lines 1 to 40, `example_content_rivendell_2026-11-26.json` | 11-26 16:42; 16:33 |
| `lothlorien/kit_full/control/mirkwood_probe.json` (keys); `concept_r1/textures/` (`ls`: 23 PNG); ref_04 looked at | 11-26 |
| `rivendell/kit_full/design_v2/massing/_scratch/`, `massing_house_eave_r2/`, `forms_set_1/` (`ls`); `mirkwood-kit-handover-2.md` (grep); `mirkwood/STATE.md` (`ls`) | 10:09; 10:12 |
| Memory `features/kingdom-kits.md` lines 128 to 129, 145 to 146; `feedback_one_asset_a_piece.md`, `feedback_lean_review_page.md`, `feedback_kit_forms_into_editor_early.md`, `feedback_precision_and_creativity.md` (grep) | not read for time |

## Not checked

- No Blender job was run. The crossing, coincident and zero-area counts are the craft reviewer's; two verifier jobs
  failed with `NO BLENDER PROCESS SEEN`. Step: run `verify/coincident_job.py` of the review folder through the lock tool.
- The live `settlements.xml` was not read today. Step: `culture_scenes.py` (Part B6).
- The prefab entity count of today. Step: `tools/check_prefab_budget.py` on each loaded module, summed.
- The engine lines that the earlier session cited (`Mission.cs:6132-6153`, `Scene.cs:325-334`, `monsters.xml:103-106`,
  `props_other.xml`). Step: `pwsh tools/taom-src.ps1 path Mission` and read the lines.
- Whether the earlier session still runs. Its newest file was still 09:42 at 10:16 (`find -newermt` over WORK). Step:
  Q0, and the newest mtime in its folder.
- Which picture is the "More Lothlorien" pavilion of 2026-11-25. Step: ask him, or search the Rivendell refs folder.
- The exact words of every decision and rejection in Part B3: only paraphrases exist. Step: read them back to him on
  the first page.
- What "the requested kitbash" meant (reading c, Q2 D). Step: his answer to Q1 and Q2.
- Whether the Lothlorien docs are tracked in git. Step: `git -C E:/repos/taom-kingdom-kits status --short
  docs/kitbash/lothlorien*`.
- Whether issue #1621 is still open. Step: `gh issue view 1621`.
- Rivendell B2 after 09:48: its `check/` folder was still changing at 10:09. Step: read `state.json` and the check's
  result on the day.
- A Tripo height field round a trunk (Part B5 step 7). Step: one trial on a 2 m ring of the code trunk.
- ref_01, ref_03, ref_05 and ref_06 were looked at by the writer, not again by the reviser (ref_04 was).
- How any form reads at 5 m and 15 m, in the Kit or in the game: nothing exists to look at.
