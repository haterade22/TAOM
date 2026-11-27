# Mirkwood kit: handover 2 (for a new lead session on Claude Fable 5.2)

## Read this first

- **What this is.** The entry point for a new lead session that takes over the Mirkwood settlement kit. It holds the
  research itself: what exists, what the maintainer said, what his pictures show, what the scenes need, and a plan.
  Part B8 copies the facts of the first handover that the plan needs, so the first handover is a source only. A fact
  that is still in another file is named with that file at the step that needs it.
- **When and by whom.** Written 2026-11-27, 09:58 to 10:04 CST, and revised 10:13 to 10:32 CST after an independent
  check (clock read with `date`), by workers of the Rivendell kit session (issue #1600), on the maintainer's request of
  the same morning. The survey behind it is
  `E:/LOTRAOMAssets/_auto_workspace/kingdom_kits/_shared/handovers_2026-11-27/mirkwood/SURVEY.md`; the check is
  `.../mirkwood/check/CHECK_HANDOVER.md` and the revision note `.../mirkwood/revise/REVISION.md` in the same folder.
- **Where to start.** Start the session in `E:/repos/taom-kingdom-kits`, the worktree on the branch `feat/kingdom-kits`
  (its `.git` file points to `E:/repos/TAOM/.git/worktrees/taom-kingdom-kits`, whose `HEAD` names that branch). Not in
  `E:/repos/TAOM`: that checkout is on `bannerlord-1.5.x`, where no file may be edited, where this handover does not
  exist and where `docs/features/tripo-cli.md` has no "Tripo Studio MCP" section (grep count 0 there, 4 in the
  worktree). Run every repo command of this handover (`tools/...`, `docs/...`) from the worktree.
- **The memory folder.** `C:/Users/mikew/.claude/projects/e--repos-TAOM/memory`. A session started in the worktree
  does not load it by itself (`C:/Users/mikew/.claude/projects/E--repos-taom-kingdom-kits/memory` does not exist):
  read it by hand. The art rules are `feedback_asset_art_rules.md`; the job card is `features/kingdom-kits.md`. "The
  memory card" in this handover is a new card `features/mirkwood-kit.md` in that folder (none exists today), with one
  index line of at most 100 characters in its `MEMORY.md` under "In-Flight". The index is over its own budget already
  (43 lines and 5,218 bytes by `wc`; its header asks for 40 lines and 4 KB): add no second line.
- **The first handover is a source only.** `D/mirkwood-kit-handover.md` (2026-11-26, version 2 of 17:18, top section
  added later by the Mirkwood session). Part B8 carries what the plan needs from its sections 2, 5, 7 and 8; its
  research list of section 9 is in B3 (questions 6, 7, 9 to 12) and in P1 to P5. Open it only to check a fact.
- **Who did the work so far.** A session of another AI client worked on Mirkwood from 2026-11-26 about 17:05; its last
  work files are of 2026-11-27 06:02. The maintainer calls the result "rough". An independent review of 2026-11-27
  (09:08 to 09:31) confirmed 21 findings; Part B uses them by their ids (ML for look, MC for craft, N for new).
- **New at 10:12 today (read in the revision).** `W/STATE.md` was written again at 10:12:18 (md5
  `449d5aaca4042383a70b1cd596858350`). Its new last bullet (line 40) says: "The repo handoff merged as PR #1626 at
  8b6098858. Start at E:/repos/TAOM/docs/kitbash/mirkwood/delivery.md". The Mirkwood docs in the trunk checkout
  `E:/repos/TAOM/docs/kitbash/mirkwood*` carry the time 10:12:17 and differ from the worktree copies: trunk adds
  `mirkwood/delivery.md` (the Blender master, the library, the four visual stages and the commands to repeat the
  checks) and `docs/features/mirkwood-kit.md` (the in-game checks still owed), and changes a few lines of `README.md`,
  `approach.md`, `distance-studies.md`, `siege-controls.md`, `mirkwood-kit.md` (lines 186 to 189) and the first
  handover's top (`diff`). Who wrote at 10:12 is UNVERIFIED; most likely the other client's session, so question 0
  matters. Read the trunk copies (read only); write in the worktree only as B3 "Where you write" says.
- **The end state, in one sentence.** Mirkwood gets what Rivendell has or is building: an approved design language
  from his pictures, placeable building and structure forms in the Modding Kit imported early and updated in place,
  its own texture sets and trim sheet, his reference scenes as blockouts cut into reusable pieces with walks, a siege
  set that meets the scene checklist, kit sets with far levels, bodies and a prefab file that were checked, and records
  from which any session can resume.

Short names used below: W = `E:/LOTRAOMAssets/_auto_workspace/kingdom_kits/mirkwood` (the work folder);
D = `E:/repos/taom-kingdom-kits/docs/kitbash`; R = `E:/LOTRAOMAssets/_auto_workspace/kingdom_kits/_shared/reviews/2026-11-27/mirkwood`
(the review); SSN = `D/settlement-scene-needs.md` (the scene research, version 2 of 2026-11-26 19:05); T =
`E:/repos/TAOM/docs/kitbash` (the trunk copies, read only). Line numbers in D are of the worktree copies as read on
2026-11-27; in T, `mirkwood/README.md` has one more line (a new line 9), so its later lines are one higher. MODS = `E:/Steam/steamapps/common/Mount &
Blade II Bannerlord/Modules` (quote it: it holds spaces and `&`).

## Part B1. The end state, as deliverables

| # | Deliverable | Proven done when |
|---|---|---|
| 1 | **Design language v2** for Mirkwood, written from his nine pictures: the ten things of B4 as rules with numbers (bay ratio, pier slenderness, arch forms, the two planes, the wave band, the rail rule, the cavern section), what is exact and what is free, a "never" list, a colour table measured from named crops of at least two pictures | He approves it on the lean page (his words recorded with date and source); an independent critic checked each rule against a picture |
| 2 | **Placeable forms in the Modding Kit**: the forms he keeps from the massing rounds, one mesh a piece, final names by the naming rule of P3, kit materials, UVs, front faces outward, in kit files by family | An independent check passed before the copy; he imported the files; `python tools/check_rdc_entries.py` shows no package without a cache entry; the manifest lists each piece and what it lacks |
| 3 | **Texture sets and one trim sheet** at 1024 px and about 0.45 UV a metre (463 px/m), each look a real map set (no look that exists only in Blender nodes), materials named `t_mirkwood_<set>` and free of every held name | Plain-map renders with back faces culled beside his pictures; a material table that he set by hand in the Kit; a look in the Kit beside the held kit |
| 4 | **His reference scenes as blockouts, cut into pieces**: the exterior of ref_05 with the gate porch of ref_01, 03 and 08; the cavern of ref_09 with the throne of ref_07 | At the picture camera the blockout reads as the picture (critic overlay); every walk ends on a stair or a deck; sizes against a 1.8 m figure; a cut list of pieces with their joints; nature left to the editor |
| 5 | **A siege set that meets the SSN checklist** (B6): a solid wall with parapet part entities, a broken wall with its two children, wall towers and a corner tower with a machine platform and a stair entity, a gatehouse with room for an outer and an inner gate and a rock hole, the gate route | Each checklist row 3 to 22 has a proof file; each gate failed on a planted fault; archer windows are real openings with standing room; the gate leaves fit the chosen route |
| 6 | **Kit sets**: far levels `.lod3` and `.lod6` within budgets taken from a control, `bo_` bodies (a stair body with `has_steps` in the prefab), one manifest from one stage folder, the copy tool with an undo list, a prefab file under a new name | An independent check before each copy; his import; the prefab entity count against the cap on that day; the prefab file in only after his import of every mesh it names |
| 7 | **Records**: `W/STATE.md` (newest block on top, with "Next" and "Owed decisions"), `D/mirkwood-kit.md`, `D/mirkwood/lessons.md`, one lean review page, the memory card `features/mirkwood-kit.md` ("Read this first"); until question 0 is answered A, the records go where B3 "Where you write" says | A fresh session resumes from `STATE.md` alone; every claim names its proof file |

**What "levels" means here.** His request names "environments, levels, structures, buildings". This handover reads
"levels" as level design: the reference scenes of item 4 cut into pieces that a scene builder places (Part A3 step 8
calls this the "environment" part). It also counts the far levels `.lod3` and `.lod6` of item 6. A made settlement
scene (a playable level in the game) is a second reading: whether it is part of done is his choice in question 12.
Until he answers, done means items 1 to 7; after an answer A or B, a scene for that settlement is item 8, proven when it
loads in the game from his `scene_name` edit (B6, last paragraph) and one siege runs in it at each level it serves.

## Part B2. What exists today

All local; nothing of the new work is in the Modding Kit or the game. Times are file modified times read on
2026-11-27.

| Item | Path | State | Keep, rework or redo | Why |
|---|---|---|---|---|
| His nine pictures | `W/refs/owner_2026-11-26/ref_01.png` to `ref_09.png`, `SOURCE.txt`, `manifest.json` (11-26 17:03) | Complete; local only | Keep | The brief. Never to an outside service |
| State file | `W/STATE.md` (11-27 10:12, 44 lines by `wc -l`) | A list of pages and folders; its last bullet points to PR #1626 and `T/mirkwood/delivery.md` | Rework | No next step, no owed decisions (MC8) |
| Delivery record | `T/mirkwood/delivery.md` and `E:/repos/TAOM/docs/features/mirkwood-kit.md` (trunk only, 11-27 10:12) | The other session's map of its master blend, library, four visual stages and rerun commands; the in-game checks owed | Read; keep as the built set's record | Not in the worktree until trunk is merged into the branch (his word) |
| Build record | `D/mirkwood-kit.md` (11-27 06:02) | 189 lines (`wc -l`); his directions paraphrased; the trunk copy differs at lines 186 to 189 | Keep, add to it | Honest; says Kit and game checks are owed |
| Lessons | `D/mirkwood/lessons.md` (11-27 06:02) | 302 lines, one lesson a result | Keep | Read before any build (B7 takes the key ones) |
| Held kit catalog | `D/mirkwood/README.md`, `meshes.md`, `materials.md`, `coverage.md` (11-26 18:04 to 21:00); `W/kit_full/baseline_01` (98 files, 44 lit renders with a 1.8 m figure) | Measured, frame rule proven | Keep | The control group for the held pieces |
| Reference study | `D/mirkwood/reference-study.md` (11-26 19:59) | Reading and five crops | Rework into the design language | Misses the cliff, tracery, teal doors, caverns (ML1, ML3) |
| Design language | `D/mirkwood/design-language.md` (11-26 21:26) | Draft; stone and timber town | Rework (B1 item 1) | Keep its member rules; it adds roofs, slate and timber framing that no picture shows; colours "proposed" (ML8) |
| Carving study | `W/kit_full/concept_02` (11-26 18:27) | APPROVED by him | Keep | The standard of craft: tangent jamb, 221 mm beech leaf, pegged joints |
| Smooth limestone | `W/kit_full/textures_02/maps/t_mirkwood_stone_a_*` (11-26 18:48) | APPROVED by him | Keep | His corrected stone |
| Single-block seat | `W/kit_full/plinth_01` (11-26 18:52) | His mark applied | Keep | One dressed block under a jamb |
| Ashlar set | `t_mirkwood_ashlar_a_*`: in seven stage folders (all of the eight below except `siege_02/staging`) and in `siege_03/maps` and `siege_04/maps` (`find -name "t_mirkwood_ashlar_a_d*"`) | Not asked of him | Question 4 | His rule "smooth stone means no tile grid" (ML6) |
| Heartwood and trims | `t_mirkwood_heartwood_a` and `stone_trim_a`, `timber_trim_a` in `W/kit_full/textures_02/maps` (11-26 18:43; an older copy in `textures_01/maps`, 18:32); `stone_trim_b`, `timber_trim_b` in `W/kit_full/trim_03/maps` (19:28 to 19:29) | 1024 px, one density, mip proof | Keep the method; the bands follow the new language | A timber-heavy look may not survive question 1 |
| Blender-only looks | Slate, bronze, amber glazing, damp plinth, end grain, paving joints (`R/craft/material_census.json`) | Node tints on the limestone maps, or no image | Redo as map sets or drop | The Kit takes maps and flags, not nodes (MC2) |
| Siege set | `W/kit_full/siege_04` (11-26 19:36): 10 pieces; `W/kit_full/lod_siege_01/staging_02` (21:15): wall, breach, gatehouse, tower, battery wall at three levels | Clean meshes, one end section, 12 m deck | Rework | Entity tree missing (MC1), blind openings (N1), gate route (MC3, ML5), battlements and square towers that no picture shows (ML1) |
| Gate leaves | `W/kit_full/gate_leaf_01` (11-27 05:54) | Own leaves, braced plank | Redo after question 2 | Outer pair swings to the field (MC3); arch head open (ML5); brown plank against teal (ML4) |
| Ravine approach | `W/kit_full/approach_01` (11-26 20:01), tools `W/_tools/approach_01` | Bridge, landing, three-bay screen, gallery, stair, lantern; two layouts; route checks with planted faults | Rework into the scene blockout (B1 item 4) | Nearest to his pictures, but one plane, no tracery, squat main bay (ML7), timber rails |
| Hall | `W/kit_full/hall_01`, `hall_joint_01`, `lighting_02` (to 11-27 06:00) | Vaulted timber hall, amber lighting | Keep the lighting lesson; redo the form | A timber hall in a court, not a cavern (ML1) |
| Houses and compounds | `W/kit_full/houses_01/staging_02` (11-26 20:49, 13 pieces), `W/kit_full/site_01` (21:03) | One rule set, front gables | Question 8 (type B or not) | No massing variants (ML2) |
| Distance studies | `W/kit_full/lod_02`, `lod_siege_01` | Levels barely shrink | Redo budgets | Not from a control (MC4) |
| Stages | Eight folders under `W/kit_full` (`find -maxdepth 3 -type d -name "staging*"`): `houses_01/staging` and `staging_02` (both `buildings/mirkwood_hall_kit.fbx`, `mirkwood_house_kit.fbx`, `props/mirkwood_small_kit.fbx`, `structures/mirkwood_open_kit.fbx`, `village/mirkwood_village_kit.fbx`); `lod_02/staging` (the same names less `small`); `lod_siege_01/staging` (four `walls/mirkwood_castle*_kit.fbx`) and `staging_02` (five, with `battery`); `siege_02/staging`, `siege_03/staging`, `siege_04/staging` (each one `walls/mirkwood_castle_kit.fbx`). `T/mirkwood/delivery.md` names four of them as the built set's stages: `siege_04/staging`, `houses_01/staging_02`, `lod_siege_01/staging_02`, `lod_02/staging` | Same file names, different content | Redo as one stage | A copy could take a faulty tower (MC6) |
| Asset library | `W/kit_full/library_01` (11-26 21:24) | 26 Blender collections | Keep as a Blender tool | Not a Kit deliverable |
| Other studies | `W/kit_full/siege_blockout_01` (11-26 18:09), `concept_01` (18:16), `textures_01` (18:38), `siege_01` to `siege_03` (19:10 to 19:26), `lod_01` (20:51), `lighting_01` (21:11), `trim_mip_01` (21:19), `village_roof_01` (21:27), `morning_01` (21:41, the Cycles proof index), `morning_lighting_01` (21:42), `weather_01` (11-27 05:52, bridge runoff), `morning_lighting_02` (05:53), `court_paving_01` (05:54, flagstone paving), `village_roof_02` (05:55, slate roofs: pitched slate and timber gables that no picture shows); newest file time of each folder by `find` | Earlier rounds and studies; none shown to him with a question | Keep as versions; build nothing on them before question 1 | The paving and the slate are on the "never decided" list below |
| Review pages | 16 `W/*.html` (15 `review_*_NN.html` and `review_now.html`) | Status only, ask nothing | Replace by one lean page | ML9 |
| Generators | `W/_tools`: 12 folders (plus `__pycache__`) and 25 loose scripts (`find -maxdepth 1`) | Code with unit tests | Keep; copy before change | The repair goes into a generator, never into a mesh |
| Issue | #1620 (siege kit), body in `W/siege_issue_body.md` | Open (UNVERIFIED today) | Keep | The siege work's issue; #1600 is the job's |

## Part B3. The maintainer's words and decisions

The Mirkwood records paraphrase him; direct quotes exist only where marked.

| Date | Words or decision | Source |
|---|---|---|
| 2026-11-26, about 16:53 | Quote: "Give me a complete handover for another session to do mirkwood, yes we have SOME stuff for mirkwood but we need to make it better and iterate on it based on references." | `D/mirkwood-kit-handover.md` lines 38 to 39 |
| 2026-11-26, about 17:03 | Saved the nine pictures in a folder of his; "proceed with our work" | memory card `features/kingdom-kits.md` line 98; `W/refs/owner_2026-11-26/SOURCE.txt` |
| 2026-11-26 | One siege kit for castles and towns: intact and broken wall, a gatehouse with independent outer and inner gates, defender siege stations; a separate issue (#1620) | `D/mirkwood-kit.md` lines 27 to 31 |
| 2026-11-26 | "Study a tower battery and an extra forward wall with its own battery and access" (paraphrase) | `D/mirkwood/siege-controls.md` lines 7 to 8 |
| 2026-11-26 | Art concept before siege massing; the held kit is optional for the new design; free design from the references | `D/mirkwood-kit.md` lines 33 to 37; `D/mirkwood/lessons.md` lines 50 to 52 |
| 2026-11-26 | Asked for "the precision and detail of a master-carved object" (paraphrase) | `D/mirkwood-kit.md` line 41 |
| 2026-11-26 | APPROVED the craft sample (`W/kit_full/concept_02`); asked for texturing | `D/mirkwood-kit.md` line 56 |
| 2026-11-26 | REJECTED the square speckles of the first stone; asked for smooth limestone with some weathering | `D/mirkwood-kit.md` lines 65 to 66; `D/mirkwood/textures.md` lines 7 to 8 |
| 2026-11-26 | APPROVED the limestone finish; marked the three layers under the jamb; chose one dressed block | `D/mirkwood-kit.md` lines 76 to 78 |
| 2026-11-26 | Shared stone and timber trim as the next priority | `D/mirkwood-kit.md` line 96 |
| 2026-11-26 | The next build from his Mirkwood pictures, with the Rivendell worked example | `D/mirkwood-kit.md` lines 108 to 109 |
| 2026-11-26 evening | Hall, houses and compounds were built "headlessly while Mike was away": no approval of them is recorded | `D/mirkwood-kit.md` line 120 |
| 2026-11-26, about 19:00 | Quote: "Dont use codex as it is currently working on the mirkwood kit and dont want to blow its usage." | `D/rivendell-siege-set.md`, `grep -n "Dont use codex"` (a growing file: line 708 on 2026-11-27 10:15) |
| 2026-11-27 | Quote: "(most of it turned out in a rough state unlike Rivendell) ... The end-state is to have the same thing happen with Rivendell (kitbash and already made environments,levels, structures, buildings) for Mirkwood and Lothlorien ... Just tripo MCP at will we have a ton of credits." | The request behind this handover |

**Standing rules of his that bind every Mirkwood piece** (memory file `feedback_asset_art_rules.md`, the block on
built architecture, lines 117 to 182; quotes): "Remove the TILE look from this and make it smooth stone"; "elven
smiths didnt make mistakes. This needs to be symetrical and very fine tuned"; "I would open those windows up so that
archers can shoot from them also"; "You need to make enough room for stairs to connect to your wall, so soldiers can
get up"; "makes it one flushed floor"; "we dont need them as the editor will place them"; "We build these walls for
ladders ... Anything more is wasted material". And (layout rule 10): "These should be made all together, no one is
going to need stair railings. So stairs come with the railings."

**Approved:** the carving study, the smooth limestone, the single-block seat. **Rejected:** the speckled stone.
**Never shown or never decided (no record):** siege 03 and 04, the approach, the hall, the houses, the compounds, the
gate leaves, the paving, the slate. Whether he saw them is UNVERIFIED (ask on the page, question 0).

### What he has not been asked (one lean page)

Put questions 1 to 13 on ONE page, made with
`python E:/LOTRAOMAssets/_auto_workspace/kingdom_kits/_shared/review_page/page_maker.py <HH:MM> <content.json> <folder>`
with the folder `W/v2` (it writes `W/v2/review_now.html`, so the old `W/review_now.html` stays as a version). The
content file holds `{"needs": [...], "new": [...], "decisions": [...]}` with HTML fragments; picture paths are
relative to the folder you give (so `../refs/owner_2026-11-26/ref_01.png`); the form is in
`_shared/review_page/example_content_rivendell_2026-11-26.json`. Show, do not describe: each question with the
pictures named. Each has lettered options and a recommendation.

**Where you write (one rule).** Ask question 0 in chat ALWAYS, first, before any write. Until he answers A, write only
in the new folder `W/v2/` (it is yours alone: the other session never wrote there, so Part A5's "another session's
folder" does not cover it), and keep the records there too (`W/v2/STATE.md`, `W/v2/records/`); change no file in W
outside `W/v2/`, and no file in D. After his answer A, W and the Mirkwood docs in D are yours: `W/STATE.md` gets the
new block on top and the records of B1 item 7 go where it names them. Before the first edit of a Mirkwood doc in D,
the worktree copies are older than the trunk copies T (PR #1626): ask him for his word to merge trunk into
`feat/kingdom-kits` (git is his), or write only new files `D/mirkwood/v2-*.md` until then.

0. **Has the other client's Mirkwood session stopped?** It wrote `W/STATE.md` and landed its docs on trunk at 10:12
   on 2026-11-27 ("Read this first"). A: yes, the new lead owns W and the Mirkwood docs. B: no, it still runs. *Recommend
   A: his request of 2026-11-27 asks for a new session on this kit, and two writers in one folder is trap 19.
   Until he answers, the rule above holds.*
1. **The setting** (pictures: ref_01, 03, 05, 08 beside `W/kit_full/siege_04/renders/castle_frontage.png` and
   `W/kit_full/morning_01/renders/compounds/castle_court.png`). A: a gate front in a cliff with cavern halls, as the
   pictures. B: a walled town in the forest, as built. C: both, as types A (cliff realm) and B (walled town). *Recommend
   C, with type A built first: his pictures show only A, and a form that fits another role is offered as another type,
   never dropped (`.claude/agents/kitbash-architect.md`, "When the brief asks you to judge", lines 175 to 176). The
   letters A and B here name settings, not name suffixes: the names follow the rule of P3.*
2. **The gates** (picture: `W/kit_full/gate_leaf_01/renders/outer_gate_closed.png`; SSN 7.2 and 7.4). A: vanilla gate
   prefabs (route A, used in all 56 TAOM gate instances), with the passage narrowed to 4.70 m clear and 6.9 m high and a
   carved tympanum over the leaves. B: our own teal leaves on the vanilla door skeleton (route B), outer pair turned to
   the castle side, after a Kit probe in a throwaway module. *Recommend A for the first siege set; B later as a look, once
   the Rivendell probe has run.* The Rivendell probe is
   `E:/LOTRAOMAssets/_auto_workspace/kingdom_kits/rivendell/kit_full/gates/probe/PROBE_OWNER_STEPS.md`: 63 owner steps
   that import a box leaf door on the vanilla 7 bone gate skeleton into a throwaway module `TAOM_GateProbe`, to learn
   whether the Kit accepts a skeleton of the vanilla name in another module (SSN section 15 item 18; Rivendell
   decisions GD1 and GD4, `grep -n "GD4" D/rivendell-siege-set.md`). It has not run: its `_scratch` folder holds no
   `tpac_before.json` (the output of its step 3) and MODS has no `TAOM_GateProbe` folder (`ls`, 2026-11-27 10:20).
   Its result will be recorded in that probe folder and in the Rivendell record; route B for Mirkwood waits for it.
3. **The forward batteries** (picture: `W/kit_full/siege_04/renders/forward_battery.png`). A: outworks in the field
   that attackers can ladder (as built; a stair leads from them to the wall walk). B: machine platforms inside the wall
   line only (towers). C: both as types. *Recommend C, with B in the default frontage, and a siege test before A is used.*
4. **Broad stone faces** (pictures: one frontage render in plain `t_mirkwood_stone_a` beside the ashlar render). A:
   smooth limestone. B: ashlar courses. C: both as types. *Recommend A: his standing rule "smooth stone means no tile
   grid", and no picture shows coursing.*
5. **Doors and gate leaves** (crops: ref_01, ref_03 doors; the built heartwood door). A: dark teal with a carved leaf
   vein, the head following the arch. B: heartwood plank. C: both. *Recommend A: ref_01 and ref_03 show dark teal doors (B4 item 6); ref_08's leaf
   reads warm (181, 152, 98, `R/verify/check_colours.txt`) under sunset light, teal and orange to the eye.*
6. **Walks and bridges over a drop** (pictures: ref_04, 05, 09 beside `W/kit_full/morning_01/renders/ravine/bridge_springing.png`).
   A: no rail, as the pictures. B: a low carved stone kerb, about 0.45 m. C: a rail of grown roots, about 1.0 m.
   D: the timber rail as built. *Recommend B for walks that troops use (it keeps the railless outline and gives the
   walk a hard edge) and A for display pieces; what the AI does on a railless edge is UNVERIFIED and is a new open
   point (SSN section 15 has none for it; its item 19 is the least walk width and breach gap), so one siege or battle
   run on a test scene with a railless walk settles it.*
7. **Rock** (pictures: ref_01, 05, 09). A: cliff faces, cavern walls, roof spans, rock stairs and ledges as kit pieces
   (an exact code core with a rock skin from Tripo). B: rock from the editor or another kit. C: both. *Recommend A: a
   cavern roof and a rock-cut stair are not terrain, and the editor has no cavern roof (first handover section 9). What
   TAOM and vanilla hold for caves is still to be measured (the Moria scenes use Mirkwood meshes already).*
8. **The built set** (siege 04, houses, hall, approach, compounds). A: import it now as type B, as it stands, so that
   scene builders have it (the Rivendell rule of 2026-11-27, Part A step 6). B: keep it in Blender only until type A
   exists. C: drop it. *Recommend B if question 1 is A, and A if question 1 is B or C (then fix MC6 and the material
   names first).* Names on an import of the built set: its pieces keep the names that its stages carry today (for
   example `mirkwood_castle_wall_20m_a`, `mirkwood_small_lantern_a`), and its files keep the stage file names
   (`walls/mirkwood_castle_kit.fbx`, `buildings/mirkwood_hall_kit.fbx` and so on); the new pieces of P3 take other file
   names, so the two never meet.
9. **Beside or in place.** A: new pieces as new types beside the held kit, which stays as it is. B: the held kit
   replaced everywhere. *Recommend A: 11 production scenes place the held kit's meshes and twelve scenes and four prefab packs depend
   on its meshes or materials (B6 and B7 trap 1).*
10. **Names of the new prefab file and showcase scene.** Layout rule 9 gives `taom_mirkwood_kitbash.xml` and
    `mod_taom_mirkwood_kitbash`, and both belong to the held files. A: `Prefabs/taom_mirkwood_kitbash_b.xml` and
    `mod_taom_mirkwood_kitbash_b`. B: names of his choice. *Recommend A.*
11. **The held kit's forms** (renders: `W/kit_full/baseline_01/renders_lit/st_mirkwood_entrance_bridge_a01_three_quarter.png`,
    `st_mirkwood_stone_entrance_a03_three_quarter.png`, `st_mirkwood_ornamental_column_a01_centerpiece_three_quarter.png`).
    Its provenance is unknown (`D/mirkwood/README.md` lines 48 to 52). A: our own design takes up the same ideas (wave
    bands, branch tracery, root flare) from his pictures, with no held geometry. B: held pieces serve as donors. C: the
    held pieces stay only as they are. *Recommend A.*
12. **The first settlement and its scene.** A: the town Felegoth (`town_M1`, loads `battania_town_b`, which no other
    culture loads; SSN line 741), as a new scene `taom_mirkwood_town_felegoth_forceatmo`. B: a castle first (for
    example Glad Thaw, `castle_M1`, on the shared `battania_castle_b`). C: no settlement scene in this round; the kit and
    the two reference scenes first. This question also settles what "levels" in his request means for done (B1, "What
    levels means here"): with A or B, a made settlement scene is part of done; with C, it is not part of this round.
    *Recommend C, then A: a settlement scene needs the siege set of B1 item 5 first.*
13. **The image service** for generic concept pictures of Mirkwood (his credits; generic words; his pictures never as
    input). A: yes. B: no, massing only. *Recommend A (Part A step 4).*

## Part B4. The reading of the references

Nine pictures, all looked at for this handover (`W/refs/owner_2026-11-26`). ref_03 and ref_08 are renders of another
artist's model and ref_06 holds concept strips: a target for our own design, never traced; cut the viewer button and the
signature mark away before measuring, and copy no mark.

| Pic | What it shows |
|---|---|
| ref_01 | A gate front in shade set into rock: four broad bundled piers with lattice heads (two at the ends, two beside the centre door) and one thinner shaft between the two doors of each side, five ogee-pointed arch heads with interlace, tall dark teal doors in a second plane, a stone causeway to the centre door, mossy blocks either side |
| ref_02 | A vast dark hall under ground: trunk columns rising into pointed vaults, lit arched alcoves at the sides, a railless walk winding up to a central platform, small warm lamps |
| ref_03 | The gate porch in daylight: an outer screen of three tall round arches on reeded piers with diamond lattice soffits and heads, an inner arcade with teal doors, a causeway whose sides step down in wave bands, rock and moss all round |
| ref_04 | Inside: a bridge that looks grown from roots over a chasm with falls; a carved column of interlaced ribs; lamps; a file of figures for scale |
| ref_05 | Outside: one stone arch bridge over a river below a fall, leading to the gate porch in the cliff; huge autumn trees |
| ref_06 | Five concept strips: giant twisted trees twice, the bridge and gate in a cliff (pale and teal), a hall with a walk on a root |
| ref_07 | The throne: a platform on clustered trunk columns, a stair winding up, a seat under great antlers, vault ribs like branches, a hanging lamp, a railless curved stair |
| ref_08 | The gate porch at sunset: five even bays of reeded piers with flared feet and carved heads, round arches with carved soffits, an inner plane with tall leaf-vein doors, a wave-banded causeway, a great tree with roots |
| ref_09 | A wide cavern: root bridges with interlace in the spandrels, rock-cut railless stairs, trunk columns with interlace collars, amber lanterns hanging on chains, falls, a bright columned hall above with light shafts |

**The ten things that make the look:**

1. **A realm set into a cliff.** The built front is a carved screen against living rock, with rock above and on both
   sides: no roof and no free-standing mass (ref_01, 03, 05, 08).
2. **An even arcade of tall, narrow bays**, three to five, all of one height. A bay is about 1.7 to 2.5 times as tall
   to the spring as it is wide (pixel readings of perspective pictures, `R/look/crops/ref_03_grid.png`, `ref_08_grid.png`).
   The built main bay is 1.06 (`W/_tools/approach_01/threshold.py` lines 7 to 9): too squat.
3. **Piers as bundled reeded shafts**: a flared sheath at the foot and a flared carved head that grows into the arch,
   with no capital block (ref_01, 03, 08).
4. **Organic tracery**: lattice and interlace in the arch soffits, the spandrels and the heads; branch, knot and leaf
   vein, never studs (ref_01, 03, 08).
5. **Two planes**: an outer screen, then an inner arcade or a row of tall doors in deep shadow (ref_01, 03, 08).
6. **Tall narrow doors in dark teal with a carved leaf vein**, the head following the arch (ref_01, 03, 08). Measured:
   ref_01 door 27, 36, 35 and ref_03 door 21, 30, 24 (sRGB medians, `R/verify/check_colours.txt`,
   `R/look/colour_measure.json`); the built door is 61, 44, 32.
7. **A causeway straight to the centre bay**; its sides and the terrace step down in flowing wave bands; no rail
   (ref_03, 08).
8. **Bridges with no rail**: outside one round stone arch over water (ref_05); inside, arches that look grown from
   roots, with interlace on the arch face (ref_04, 09).
9. **Interiors are caverns**: trunk columns whose ribs branch into vaults; railless walks and stairs that wind;
   rock-cut stairs; a throne platform on clustered columns; water and falls below (ref_02, 04, 07, 09).
10. **Light and colour**: pale, smooth, weathered green-grey stone (ref_03 lit pier 183, 190, 160 and shaded 141, 151,
    115, `R/look/REVIEW_MIRKWOOD_LOOK.md` line 35; the right pier reads near neutral 230, 232, 225 in another crop,
    `R/verify_mirkwood.json` finding ML8, so the green cast partly comes from the scene light); moss in recesses; warm amber lamps against brown-black interiors
    (ref_02 amber-lit 161, 106, 60; ref_07 shaded timber 33, 26, 18, `D/mirkwood/reference-study.md` lines 24 to 28);
    shafts of daylight. The autumn forest is the editor's nature.

**What no picture shows:** pitched or slate roofs, timber-framed upper storeys, battlements or merlons, square
flat-topped towers, a town wall, a street, houses, a market or a village, plank doors with iron straps, timber
railings, a gatehouse or any siege structure, the back of the gate front, how the cliff meets the ground at the sides,
a plan of the halls, a view from above. So everything of a siege set and of a town is free design inside the ten
things, and it goes to him as forms, not as decisions taken for him.

## Part B5. The plan

Order of work: Part A3, fitted to a kit that exists and has a rough first set. New files go under `W/v2/` (forms,
design, scenes, kit sets) so that every old version stays; records as B3 "Where you write" says.

### The first session day

| Step | What | Who | Shown to him |
|---|---|---|---|
| 0 | Read the clock. In `E:/repos/taom-kingdom-kits`, claim: `python tools/work_claims.py claim mirkwood-kit --note "handover 2"`. Read Part A3 step 1's files (the memory folder is named in "Read this first") and this handover. List what the other session wrote after this revision: `find W -type f -newermt "2026-11-27 10:13"` (a hit means it still writes; tell him with question 0). Ask question 0 in chat, always; until he answers A, follow B3 "Where you write" | Lead | Question 0 in chat |
| 1 | Look at the nine pictures and these renders yourself: `siege_04/renders/castle_frontage.png`, `morning_01/renders/compounds/castle_court.png`, `houses_01/renders/building_library.png`, `morning_01/renders/ravine/threshold_from_landing.png`, `gate_leaf_01/renders/outer_gate_closed.png`, and the three held renders of question 11. Read `R/FEEDBACK_MIRKWOOD.md`, `R/verify_mirkwood.json` and `T/mirkwood/delivery.md` | Lead | |
| 2 | A fresh inventory, written to `W/v2/inventory/`: md5 of every FBX and map in the eight stage folders of B2; the sha256 of `MODS/TAOM_Map/AssetSources/Scenes/Mirkwood/kit_mirkwood_mod_env.fbx` against `a9bf533e9dc6166aa2a1f1e7728f2a8e18868ca29705f78d21d99878b196c3f1` (`D/mirkwood/README.md` line 31); the prefab entities of the five modules that the Kit loaded on 2026-11-26 (`TAOM_Map`, `Native`, `SandBox`, `StoryMode`, `LOTRLOME_Armory`; SSN section 11.2), one call each, `python -B tools/check_prefab_budget.py --dir "E:/Steam/steamapps/common/Mount & Blade II Bannerlord/Modules/<module>/Prefabs"` (read only), then the sum against 131,072; the 24 settlements' scenes with `python -I culture_scenes.py "$MODS" W/v2/inventory/culture_scenes.json`, run from `E:/LOTRAOMAssets/_auto_workspace/kingdom_kits/_shared/scene_needs/scripts` with `MODS` set to the path above (SSN section 13) | `implementer` (Sonnet) | |
| 3 | The lean page of B3 (questions 1 to 13) with its pictures, made with the `page_maker.py` call of B3 into `W/v2/`; plus one plain-map render request (question 4) if a Blender slot is free | Lead | **Morning: the page (questions 1 to 13)** |
| 4 | Design language v2 draft from the pictures (B1 item 1), with measured swatches from crops of at least two pictures each and a "never" list; then an independent critic beside the pictures | `kitbash-architect` builder, then a second `kitbash-architect` as critic | With the forms |
| 5 | Massing forms, round 1, of type A, true solids from fixed cameras (front, back, side, top, three-quarter) with a 1.8 m figure, and a Blender view file: the gate porch (three bays, five bays, five bays with an inner plane, a stilted centre bay), the outer bridge with wave-band sides, a cavern hall bay of trunk columns and branching ribs (three forms), a root bridge (two forms), a rock-cut stair, the throne platform on clustered columns, and three cliff gate frontages for the siege (a screen in a rock wall with archer galleries; a masonry wall bedded in rock; a defence that is not masonry, SSN section 16). These are 15 named forms (4 porch, 1 bridge, 3 hall bays, 2 root bridges, 1 stair, 1 throne, 3 frontages); the builder adds three or more of its own that go past the obvious reading (Part A3 step 5: Rivendell had 18 in one round), so the round has 18 or more. Copy `rivendell/kit_full/design_v2/massing/_scratch/mlib.py` first | `kitbash-architect` builder | |
| 6 | An independent critic on the forms beside the pictures: craft findings only; taste is noted and never removes a form | `kitbash-architect` critic | |
| 7 | Tripo, first jobs (load the skill `tripo-studio-mcp` first; generic prompts; inputs are renders of our own massing, never his pictures): a trunk column with a root flare and interlace collar, a root bridge underside, a rock face panel, a cavern roof patch. Record each job in the "Jobs" table of `docs/features/tripo-cli.md`; results to `W/tripo/out` | Lead (the MCP is a session tool) | With the forms if ready |
| 8 | Look at every form picture yourself, decide each critic finding by id, open a Blender window of the forms for him | Lead | **Afternoon: the forms sheet and the window** |
| 9 | Records, where B3 "Where you write" puts them: the state file's new block on top (what runs, what is next, owed decisions), the build record, the lessons, and the memory card `features/mirkwood-kit.md` with its one index line ("Read this first") | Lead | |

### The second day

| Step | What | Who | Shown to him |
|---|---|---|---|
| 1 | Record his answers to questions 0 to 13 with date and source, then apply "Where you write" | Lead | |
| 2 | If question 13 is A: concept pictures from generic words through the image connector (Part A3 step 4), all shown, both readings of any look with two; never his pictures as input | Lead (the connector is a session tool) | The concept pictures |
| 3 | Design language v2 revised by his answers; the critic checks each rule against a picture (P1) | `kitbash-architect` builder, then critic | The language, for his approval |
| 4 | Forms round 2 where he asks (P2) | `kitbash-architect` builder, then critic | The forms sheet and a window |
| 5 | The forms he kept into first kit files (P3), checked before the copy; the copy is the lead's | Builder, an independent checker, Lead | The ask to import |
| 6 | Tripo results through `/prep-tripo-prop` and the mesh gates | Lead (skill), then builder | With the forms |
| 7 | If question 8 is A: one stage folder (MC6) and renamed materials (MC5) before any copy of the built set | `kitbash-architect` builder; `implementer` for the md5 list | |
| 8 | P4 start: the set that the kept forms need first (by his answer to question 5, the teal door with leaf vein) | `kitbash-architect` builder | Plain-map renders |
| 9 | Records, as step 9 of the first day | Lead | |

### The later phases, in order

| Phase | Work | Gate before the next phase |
|---|---|---|
| P1 Decisions and language | His answers recorded with date and source; design language v2 revised by them | He approves the language; the critic's rule-by-picture check is green |
| P2 Forms rounds | More forms where he asks; base methods for members (arch outlines, a free rib, a plate, a turned solid, placement along a walk) copied from `rivendell/kit_full/v2_base` and repointed to Mirkwood paths | He keeps forms; each kept form gets a final name and type letter |
| P3 Forms into the Kit, the same day | A first kit file a family with the kit materials, UVs, front faces outward; a note of what each lacks. **Naming rule** (the Rivendell pattern of `rivendell/kit_full/forms_set_1/NAMES.md`): a piece is `mirkwood_<family>_<what>_<size>m_a`, where `<what>` names the idea of the form in a word that no built or held piece uses (for example `screen`, `cavern`, `rootbridge`), the size is the front length in whole metres and `_a` is the first type of that idea; a kit file is `mirkwood_<family>_types_kit.fbx` (layout rule 2, the kind word `types`), never one of the stage file names of B2 (`mirkwood_open_kit.fbx`, `mirkwood_hall_kit.fbx`, `mirkwood_castle_kit.fbx` and the rest stay with the built set, question 8); the castle family of P6 takes a kind word that no stage uses. Before the copy, a clash scan of every new piece, body, file, material and map name against MODS and the eight stages, with a copy of `rivendell/kit_full/forms_set_1/tools/name_scan.py` repointed to Mirkwood; zero hits. A name is final from that first copy (layout rule 3) | Independent check before the copy; the name scan at zero; his import; `check_rdc_entries.py` clean |
| P4 Texture sets and trim | Keep `t_mirkwood_stone_a`; new sets for what the kept forms need (teal door with leaf vein, rock, bark and root from a Tripo model rendered from above, lamp metal and glass); one trim sheet with a tracery and interlace strip and the bands that the forms use; Painter only under the claim `painter-instance`, or in pure code | Plain-map renders, back faces culled, beside his pictures; his pick; a look in the Kit beside the held kit |
| P5 Reference scenes | Blockout of ref_05 exterior with the gate porch, and of the ref_09 cavern with the ref_07 throne, each at its picture camera, with a walk graph (stair and rail measures), cut into pieces at their joints; nature left to the editor (Rivendell pattern: `rivendell/kit_full/design_v2/scene_valley`) | Critic overlay at the picture camera; every walk ends on a stair or a deck; his marks applied |
| P6 Siege set | Re-cut from the kept frontage forms to the SSN checklist (B6): parapet parts `_mN`, tower stair as its own entity, rock hole, archer windows with standing room, a corner tower, gate seats for the chosen route, the outer swing to the castle side, a tympanum. Start from one 20 m wall split into its body and parts, with a test that fails first (`R/FEEDBACK_MIRKWOOD.md` action 2). Tripo for the breach rubble | Each checklist row with a proof file; every gate failed once on a planted fault; an independent check |
| P7 Whole builds and kit sets | Budgets from a control (the Gondor 20 m wall: 25,516, 9,050 and 3,608 triangles at levels 0, 3 and 6, `D/rivendell-siege-set.md`, `grep -n "25,516, 9,050 and 3,608"`; line 2940 on 2026-11-27 10:15); `bo_` bodies from the walk proxies; `.lod3` and `.lod6`; one stage folder named in `STATE.md`; manifest; copy tool from `rivendell/kit_full/editor_set/tools` copied and repointed (`KINGDOM`, the folder, the hash list); prefab file under the new name (question 10) | Independent check; his import; prefab entities counted against the cap on that day; prefab file only after his import |
| P8 Records and commit | Repo tool suite (`python tools/lint_docs.py`, the doc dash check, the tests that the branch runs); a proof on a clean checkout; commit of explicit paths on his word | Green suite; his word for the commit and the push |

**Where Tripo is used:** trunk columns and their root flares and interlace collars (skin on an exact code core),
root bridges (the underside and the arch face as a relief on a code deck), cliff faces, cavern walls and roof patches,
rock-cut stair skins, the breach rubble, the antler crown of the throne (ref_07), a lantern body, and the bark and
root tiling set (a dense model rendered from above). Never for an exact floor, stair, joint or mirror pair: those stay
code-built.

## Part B6. The scene research for this kit

From SSN (`D/settlement-scene-needs.md`, version 2, 2026-11-26 19:05). Line numbers are of that file as read on
2026-11-27. KIT frame in SSN: field face at +Y (SSN line 26); the old Mirkwood siege pieces face -Y (B7 trap 6).

### The checklist (SSN section 1, lines 59 to 89), with the Mirkwood state

| # | Thing | The kit delivers | Mirkwood today | Source line |
|---|---|---|---|---|
| 1 | Centre scene | Its prefab file and showcase | None | 61 |
| 2 | Three wall levels in one scene | One wall height and one gatehouse for all three levels (recommendation) | One height built | 62 |
| 3 | Solid wall | Seamless pieces, deck 12.0 m over the ground line, foundation, `bo_` body, levels 0, 3, 6, railing as a sub-object | 20 m wall, deck Z 12, levels 0, 3, 6; no body | 63 |
| 4 | Parapet parts | Each part its own mesh `_mN` and body; `DestructableComponent` and a `debris_holder` tagged `reference` in the prefab | Missing: merlons inside the wall mesh (MC1) | 64 |
| 5 | Broken wall | Gap about 12 m at the deck; mound path 35 degrees or less, body ramp 30 degrees or less | `_dest` joins exactly; 16.82 degrees sampled (builder's number) | 65, 337 to 341 |
| 6 | Breach wall tree | Three `WallSegment` scripts; 16 entities for a 20 m wall with six parts | Not started | 66, 115 to 123 |
| 7 | Two sided walls and a ram a level | Scene builder's work | | 67, 295 to 302 |
| 9 | Wall tower | Mesh and body, doors on two sides, a machine platform, interior and stairs as own entities | Tower with a Z 16 pad; stair inside the tower mesh (MC1) | 69 |
| 10 | Corner tower | As row 9 with two joint planes at a right angle | Missing (only a curved return) | 70 |
| 11 | Frontage chain | Drop-in pieces in any order | Two frontages assembled | 71 |
| 12 | Stairs | Own mesh and body with `has_steps` | Two stairs as own assets, no bodies; tower stair not separate | 72 |
| 13 | Defender machine platforms | Level deck 5.50 x 5.72 m; no body in the 120 degree arc to the field | Pads 7.4 x 8.0 m (castle), 7.4 x 8.2 m (town) | 73, 637 to 641 |
| 14 | Archer positions | Archer windows and standing room behind them | Missing: every opening blind (N1) | 74 |
| 15 | Ladder landings | Deck served by the 14 m ladder (10.1 to 13.6 m, median 11.8) | 12 m deck fits | 75 |
| 16 | Siege tower landing | Deck served by the 12 m tower (hinge 11.90 m); `tower_merlon` parts | Deck fits; no parts | 76 |
| 17 | Gatehouse | Room for an outer and an inner gate (vanilla gaps 7.15 to 9.81 m), swing room, flush deck with a rock hole, merlons as entities | Seats 8 m apart, flush deck, no hole, no merlon entities | 77 |
| 18 | Outer gate | Opening for the vanilla leaves: 4.70 m clear, 6.9 m high (european l2) | 6.2 m wide, 7.2 m high (MC3) | 78 |
| 19 | Inner gate | Opening 4.52 m (empire inner l3) | 6.2 m | 79 |
| 21 | Gatehouse deck and rock hole | The hole; deck at most about 14 m over the passage targets | No hole | 81 |
| 22 | Ram approach | Open ground at least 6.25 m in front of the outer gate | Unchecked | 82 |
| 26 | Keep and interiors | Interior pieces if the kingdom gets its own | The halls of his pictures are this row | 86 |
| 27 | Prefab file and showcase | `Prefabs/taom_<kingdom>_kitbash.xml` after the import; showcase made in the Kit; count against the cap | Not started; names taken (question 10) | 87 |
| 28 | New scene or shared vanilla scene | His choice | Question 12 | 88 |
| 29 | A defence that is not masonry | Any look, with the engine's parts of rows 3 to 22 | A cliff front can use this | 89, 901 to 910 |

### The numbers a builder needs

| Number | Value | SSN line |
|---|---|---|
| Wall deck | 12.0 m over the ground line, foundation 4 m under it (Rivendell); one height for all levels | 203 to 211 |
| Ladders | 6, 7, 8, 11, 14 m only; tops 6.82 to 14.25 m; the 11 m ladder is at its limit on a 12 m deck | 414 to 415 |
| Breach | Gap 12.0 to 12.8 m at the deck; path 35 degrees or less, 97 percent of nodes under 32; body ramp 30 degrees or less, steps 0.06 m | 337 to 341 |
| Breach tree | Root, solid child (six parts `_m1` to `_m6`, each with a `debris_holder`, and a rail), broken child: 16 entities | 115 to 123 |
| Parapet part script | `DestructionStates=debris_vlandia_l3`, `DestroyedByStoneOnly=true`, `CanBeDestroyedInitially=true`, `MaxHitPoint=1`, `ReferenceEntityTag=reference` | 271 |
| Towers | Doors 1.4 m wide so the walk passes through; ground door 1.6 m; floors Z 0, 6, 12, platform Z 17 (Rivendell) | 360 to 369 |
| Archer windows | A real opening; standing room at least 0.8 m deep and 2.0 m high behind; toward the field and the town | 362 to 365 |
| Stairs | `has_steps` (`BodyFlags.HasSteps = 0x800`) in the prefab's physics entry; one body, one flag set, so a stair is its own entity; Rivendell risers 0.25 m, treads 0.28 m, headroom 2.45 m | 378 to 388 |
| Machine platform | 5.50 x 5.72 m with crew and pile, or 5.24 x 4.82 m turned with the pile elsewhere; mangonel arc 120 degrees; no body in the arc; one weapon a point | 606 to 641 |
| Gate leaves | european outer l2 4.695 x 6.838 m, hinges at +-2.363; empire outer l3 5.321 x 5.921; empire inner l3 frame 4.521 wide | 487 to 493 |
| Gate swing | Both leaves turn to the castle side; swept box X -2.85 to +2.85, Y -2.34 to +0.25, leaf top 6.822 m (GATE frame) | 504 to 515 |
| Gap between gates | 7.15 to 9.81 m (vanilla) | 522, 535 to 541 |
| Vanilla merlon counts on gatehouses | Battania l1 21, l2 22, l3 14 | 525 |
| Rock pile | Range 15 m; a deck above about 14 m over the targets is too high; 3 to 6 targets near the inner gate | 646 to 659 |
| Ram | Rest point 6.25 m in front of the outer gate | 585 |
| Navmesh ids | Castle-side faces end in 1; field faces and ladder landings never | 476 to 477 |
| The field | Open for about 100 m (archers), 135 m (trebuchets), 150 to 215 m (tower and ram paths) | 681 to 683 |
| Per siege level in the elven vanilla scenes | 2 sided breach walls, 1 outer and 1 inner gate, 4 ladders, 2 towers, 1 ram, 4 defender points | 181 to 187 |
| Prefab entity cap | 131,072 across all loaded modules; 124,879 used and 6,193 spare on 2026-11-26 (count again on the day) | 729 to 734 |
| Interiors | Visit: `spawnpoint_player`, `npc_common`, `sp_guard`; hall fight: `FightAreaMarker`, `defender_archer`, `defender_infantry`; dungeon: `sp_prison_break` | 695 to 704 |

### Which settlements use this kit

The new kit is used by no settlement yet. The held kit is used by no Mirkwood settlement. By parsed placements with
prefab expansion it is placed 436 times in 11 production scenes, the campaign map among them and the showcase not
counted (`D/mirkwood/coverage.md` lines 9 to 27). The first handover counts in another way: ten scenes name a held mesh
literally, and twelve scenes and four prefab packs depend on the kit's meshes or materials (`D/mirkwood-kit-handover.md`
lines 113 to 114 and 122). The 24 settlements of culture `mirkwood`
(kingdom `mirkwood`, in game Lasgalen; settlement prefix `M`) load vanilla Battania scenes (`D/mirkwood/coverage.md`
lines 52 to 77, read 2026-11-26; today UNVERIFIED, re-run `culture_scenes.py` of SSN section 13):

| Kind | Settlements and scenes |
|---|---|
| Towns | Felegoth `town_M1` on `battania_town_b`; Caras Laerolin `town_M2` on `battania_town_c` |
| Castles | Glad Thaw `castle_M1`, Torech Emel `castle_M3`, Imnagath `castle_M5` on `battania_castle_b`; Gwígar `castle_M2`, Lasgalor `castle_M4` on `battania_castle_a` |
| Villages | 17, on `battania_village` and its letters e to m |
| Interiors | The five castles and two towns use three Battania keep interiors and `battania_dungeon_stealth`; the towns also `arena_battania_a` and two Battania house interiors |

Shared scenes (SSN lines 738 to 743): `battania_castle_a` serves 8 settlements of 5 cultures, `battania_castle_b` 7 of 4,
`battania_town_b` Mirkwood only, `battania_town_c` Mirkwood and Lindon. A Mirkwood scene is a new scene
`taom_mirkwood_<kind>_<name>_forceatmo` and his `scene_name` edit in the live `settlements.xml`; never an edit of a
vanilla scene.

## Part B7. Risks and traps of this kit

| # | Trap | Rule that avoids it | Source |
|---|---|---|---|
| 1 | The held kit (one FBX, 85 maps of 2048 px, 117 packages, prefab file of 273 entities) is placed in 11 production scenes (436 placements; B6), and `t_mirkwood*` materials are set on meshes of other kits 3,069 times in scenes and 5,829 times in four prefab files | Never change, add to or re-import the held FBX, its maps or its prefab file; new pieces go in new kit files under new names | `D/mirkwood/coverage.md`; first handover lines 113 to 127 |
| 2 | A Kit material binds by name module-wide; a new material named like a held one binds to it with no warning | Check every new name as an exact name against the 136 meshes, 31 Kit materials, 32 FBX material names and 85 maps; name materials `t_mirkwood_<set>`, never with spaces or `|` (the built set has names like "Mirkwood &#124; Limestone ashlar") | First handover line 63; MC5 |
| 3 | A look that exists only in Blender nodes (slate on limestone maps with a tint, bronze and glazing with no image) cannot reach the game | Every look is a map set before he sees it as a look; show a plain-map render beside any shader render | MC2, `R/craft/material_census.json` |
| 4 | Eight stage folders (B2) hold files of the same names with different content; the manifest names the older siege stage, whose tower has 44 zero-area faces | One stage folder, named in `STATE.md`, built from a manifest of each piece's newest source | MC6; `find W/kit_full -maxdepth 3 -type d -name "staging*"` |
| 5 | Siege pieces cut as one mesh: parapet in the wall, stair in the tower, no rock hole | Cut to the engine's entity tree first (SSN 3.4, 4.1), with a test that fails before the code | MC1 |
| 6 | The Mirkwood siege pieces face the field at -Y; the SSN KIT frame and the Rivendell and Gondor tools put the field at +Y | Choose one frame for all new pieces (recommend the SSN KIT frame, field +Y) and prove it at the first import; repoint every copied Rivendell tool | `D/mirkwood/siege-kit.md` lines 9 to 10; SSN line 26 |
| 7 | The held kit is read turned 180 degrees about Z against a Blender import; a symmetric piece passes either frame | Prove a frame on an asymmetric piece (the wall lamp, an eave extension) | `D/mirkwood/README.md` lines 41 to 46; lessons "Compare the full material-split bounds" |
| 8 | Every opening blind; his rule makes a window on a defence a real opening | Each window on a wall, tower or gatehouse: can a defender stand there and shoot, toward the field and the town | N1; art rules |
| 9 | Own gate leaves whose outer pair opens to the field; a 6.2 m passage for 4.695 m vanilla leaves; an open arch head over closed leaves | Research the gates before the gatehouse (SSN 7); fit the route he picks; swing to the castle side; close the head | MC3, ML5 |
| 10 | Budgets taken from nothing: far levels keep 53 to 88 percent; a new bridge of 58,608 triangles against the held 1,848 | Budgets from a control and from placement counts; bodies carry the walk, so the visual levels may shrink | MC4; `D/mirkwood/distance-studies.md` |
| 11 | A gate with no planted fault proves nothing; several counts here have none | Plant one fault per check and keep it in the test | MC9 |
| 12 | Doubled faces at the tower crown (42 same-facing coincident pairs); only tiny faces were checked | Add a coincident-face count to the export check | MC10 |
| 13 | A bevel on a thin return clamps and leaves zero-area faces (40 mm returns, 12 mm bevel on a 24 mm plate) | Keep a flat between opposing bevels; check the evaluated mesh and the FBX reimport | lessons "Mind bevels", "Keep a lower level smaller" |
| 14 | Blender's inactive scene has stale `matrix_world`; a copied scene with no user is dropped on save | Activate and update the scene before reading transforms; give a copied scene a fake user and reopen before rendering | lessons "Measure the whole route", "Reopen copied scenes" |
| 15 | Settling his questions by default (timber rails, halls in a court, no rock) and pages that ask nothing | Every look decision goes to him on one lean page; build both readings | N2, ML9 |
| 16 | A test that passes only inside a git checkout (`test_export_review.py`) | Make tests independent of the checkout before they gate anything | MC11 |
| 17 | En dashes as range marks in `trim.md`, `settlement.md`, `siege-kit.md` | Write "to"; run `python tools/lint_docs.py --only-paths <files>` | MC11 |
| 18 | The forward batteries stand 9 to 19 m in front of the wall with a stair to the walk: a ladder on them reaches the wall walk | Question 3; a siege test before a scene uses them | MC7 |
| 19 | Two writers in one folder: the other client's session may still run (it wrote `W/STATE.md` at 10:12 on 2026-11-27) | Question 0, always first; until he answers A, B3 "Where you write" | This survey; `ls -l W/STATE.md` |
| 20 | Held maps are 16-bit PNG (69 of 85) and 2048 px; a texture overwrite repaints other kingdoms | Never overwrite a held map; a backup on `E:` first if he ever says so; new maps 1024 px | First handover lines 147 to 148 |

## Part B8. Facts carried over from the first handover

Copied from `D/mirkwood-kit-handover.md` (worktree copy of 2026-11-26 21:24) on 2026-11-27, with its section named on
each block. Its numbers were measured on 2026-11-26: count them again on the day (first day, step 2).

### The held kit, and what follows (its section 2)

- **Where it is.** `MODS/TAOM_Map/AssetSources/Scenes/Mirkwood/kit_mirkwood_mod_env.fbx` (22.9 MB; 760 objects, 136
  meshes stored only as `<name>.lodN` with irregular levels, 135 `bo_` bodies, 32 material names of which a texture
  file is named for 3); 85 maps in the `textures` folder beside it; 117 packages in `MODS/TAOM_Map/Assets/Scenes/Mirkwood`
  (one mesh package for all 136 meshes); the prefab file `MODS/TAOM_Map/Prefabs/taom_mirkwood_kitbash.xml` (273 entity
  tags, no `body_flag`, so no `has_steps`); the showcase `MODS/TAOM_Map/SceneObj/mod_taom_mirkwood_kitbash`. A mirror
  in `E:/repos/lotraom-assets/v1.5/TAOM_Map/` is never edited.
- **Three routes give an old form a new look**, each costed in packages, memory and scene edits before it is chosen:
  (a) a new FBX with the same geometry and new material names; (b) a clone of a mesh that binds another material,
  `tools/tpac_clone_metamesh.py` (where `TAOM_Map` loads such a package is UNVERIFIED); (c) a material set on the entity
  in a scene or a prefab, as TAOM's scenes do 3,069 times already (every placement names every level).
- **A retexture changes no vertex.** Prove it with vertex and triangle counts and a hash of the positions against the
  donor at every level. The held `_s` maps read as R metallic, G gloss, B occlusion (UNVERIFIED: check it); check the
  green sense of a normal map against a held map.
- **The palette also comes from Mirkwood's approved art** (read only): `MODS/LOTRLOME_Armory/AssetSources/mirkwood_assets`,
  `MODS/TAOM_Map/AssetSources/Campaign Map/Map Icons/Mirkwood`, `E:/LOTRAOMAssets/Elves/Mirkwood_SPPs`. Name the source
  of each colour.

### Tools to reuse (its section 5)

Copy a tool into `W/_tools` before you change it; call in place only what is marked so. R5 =
`E:/LOTRAOMAssets/_auto_workspace/kingdom_kits/rivendell`.

| What | Where | How |
|---|---|---|
| Blender lock tool | `R5/_tools/blender_one.py` | CALL IN PLACE for every headless job (one shared limit) |
| Base methods (arch outlines, free rib, plate, turned solid, placement along a walk) | `R5/kit_full/v2_base` | COPY and repoint: the copy imports five `rivendell_*` modules from `R5/kit_full/src_m/tools/oneoff`, and `rivendell_kit_paths` points to the Rivendell wall code by default |
| Kit tree: piece modules, build driver with levels and bodies, the checks (probe, G4b, G6b) | `R5/kit_full/src_k`; its state `R5/kit_full/step2g/LEAD_DECISIONS_2G.md` | The pattern only; read the decisions before a copy |
| Kit set tools: kit file, manifest, copy with hash checks and an undo list, after-import check | `R5/kit_full/editor_set/tools` | COPY AND CHANGE: `KINGDOM = "rivendell"` in `kit_set_rules.py`, a default of `Scenes/Rivendell`; a copy run without `--under` aims at the Rivendell folder of the module; no held Mirkwood file may enter its hash list |
| Prefab generator and owner steps | `R5/kit_full/delivery` (`PREFAB_OWNER_STEPS.md`) | COPY AND CHANGE; read the steps first |
| Modding Kit import plan | `R5/build/v07/kit_import_prep/KIT_IMPORT_PLAN.md`, `KIT_SET_IMPORT_STEPS.md` | Read before the first import |
| Massing library | `R5/kit_full/design_v2/massing/_scratch/mlib.py` | COPY: the fastest first sheet |
| Name clash scan | `R5/kit_full/forms_set_1/tools/name_scan.py` | COPY and repoint (P3) |
| Catalog tools for a held kit | `tools/oneoff/blender_catalog_erebor.py`, `tools/blender/probe_fbx.py`, `tools/audit_fbx_lods.py`, `tools/oneoff/fbx_frame_compare.py`; Mirkwood copies are in `W/_tools` already | Never change the export axes of `kitbash_kits.FBX_EXPORT` |
| A Blender window for him | Copy the `.blend` to a `view` folder; `blender-launcher.exe "<file>"` (in `%LOCALAPPDATA%/Microsoft/WindowsApps`); prove it with `E:/LOTRAOMAssets/_auto_workspace/kingdom_kits/_shared/tools/capture_blender_windows.ps1 <folder>` | Tell him to press Home if the saved view is close; the window is his from then on |
| Teardown of a better reference result | `docs/kitbash/teardowns/` (records); procedure `.claude/skills/teardown/SKILL.md` (read the file: subagents cannot invoke skills) | A part rejected twice gets a teardown before the next round |

Not for reuse: the forms and decisions of the Rivendell design language, the Rivendell castle wall code with its siege
wiring, and the motif data of the Rivendell interlace railing (its source is the licensed kit; a Mirkwood railing gets
motif data of its own).

### The Modding Kit and the engine, as measured (its section 7)

- **Prefab entity cap**: 124,879 of 131,072 used on 2026-11-26, 6,193 spare for every later kit; over the cap the Kit
  stops at its start with a queue assert. The held prefab file's 273 entities count already; a new file adds, a
  changed file replaces its own.
- **A prefab that names a mesh that is not imported can crash the Kit**: a prefab file goes in only after his import
  of every mesh it names, with the Kit closed, on his word. After a crash in the Prefabs list, read the newest
  `watchdog_log_*.txt` for "Meta mesh with name".
- **Never write a texture package by tool**, and never put a source file at the path of a cooked third-party texture
  (it crashed the Kit three times).
- **The import**: the FBX dialog keeps the choices of the last import, so "leave the defaults" is not safe. A file point
  (x, y, z) arrives as (-x, z, y) times 0.01. He makes and flags every material by hand from a table that you give him.
  A card's alpha test field counts to 255 (128 is the middle).
- **Names**: the Kit turns mesh names to lower case, and a prefab's lookup is case sensitive. The Kit makes one mesh of
  every FBX object that shares the prefix before the first dot (layout rule 10).
- **Levels of detail**: the engine picks a level by distance only; with levels 0, 3 and 6 the full mesh shows to 30 m,
  `.lod3` from 30 m, `.lod6` from 130 m. A mesh with no `.lod3` shows its level 2 beyond 30 m
  (`docs/reference/engine/static-mesh-lod-selection.md`).
- **The railing card is not proven**: prove one alpha-test card in the Kit and in the game before a family of
  railings or tracery depends on it.
- **Steps**: one body has one set of flags, so a stair is its own entity with its own `bo_` body that carries
  `has_steps`, set in the prefab.
- **Siege machines**: ladders 6, 7, 8, 11 and 14; siege tower 12 has its ramp hinge at 11.90 m (B6;
  `docs/reference/engine/castle-gates.md`).
- **A check tool's first real FAIL is a claim about the tool**: read the package before a FAIL goes to him. Run the Kit
  check tools without `python -I`.
- **Between his import and any wiring**: `python tools/check_rdc_entries.py` (read its docstring for the module and
  folder arguments) shows no package without a cache entry, and `python tools/check_prefab_meshes.py` passes before a
  prefab file goes in. Wire a pilot in a small test prefab, never by a mass replace in a scene in use.
- **The held pieces in Blender**: bind the 85 maps by material name before a sheet or a window, or he reads grey
  pieces as the kit's look. Fold Blender's `.NNN` material copies back before an export.

### What went wrong before (its section 8)

- Checkers judged against rules and never against the concept he chose: a checker's first input is his picture.
- Times, counts and labels were written before they were read: read the clock first; a label says what the picture shows.
- A tool test named the live module as its output: no test, mutation run or trial names a live game path.
- Several code trees grew apart: one tree a kit family, one owner a file in a run.
- Parts were exported as separate meshes with no engine need, and he got three assets for one stair.
- The first Rivendell valley blockout did not read as his picture: size each house, platform and stair from the
  picture against the 1.8 m figure; every walk ends on a stair or a deck; a budget comes from placement counts.
- Some repo docs have CRLF line ends: test with `file <path>`, change them with a script file, then run
  `python tools/lint_docs.py --only-paths <files>`.

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
  an output, a working folder or a target. A copy into the module is the lead's step, after an independent check.
  The Modding Kit does NOT have to be closed for a copy of source files (FBX, textures) into `AssetSources`: the
  maintainer rescans or reimports them with the editor open (his word of 2026-11-27: "WE DO NOT HAVE TO CLOSE THE
  EDITOR TO UPDATE FILES!!!!"). Never ask him to close it for that. Ask him once, in one line, before you change a
  prefab XML or a compiled package while the Kit runs.
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

Read for this handover on 2026-11-27 between 09:50 and 10:04 CST. Modified times as read.

| File | Modified |
|---|---|
| `W/STATE.md` | 2026-11-27 06:02 |
| `W/review_*.html` (16 pages) | 2026-11-26 18:16 to 2026-11-27 06:02 |
| `W/siege_issue_body.md` | 2026-11-26 18:06 |
| `W/refs/owner_2026-11-26/ref_01.png` to `ref_09.png`, `SOURCE.txt`, `manifest.json` | 2026-11-26 17:03 to 17:05 |
| `W/kit_full/*` (folder listing; renders looked at: `siege_04/renders/castle_frontage.png`, `morning_01/renders/compounds/castle_court.png`, `houses_01/renders/building_library.png`, `morning_01/renders/ravine/threshold_from_landing.png`, `plinth_01/renders/single_block_close.png`, `baseline_01/renders_lit/st_mirkwood_stone_entrance_a03_three_quarter.png`, `st_mirkwood_entrance_bridge_a01_three_quarter.png`) | 2026-11-26 18:03 to 2026-11-27 06:00 |
| `W/_tools/*` (listing only) | 2026-11-26 17:06 to 2026-11-27 06:00 |
| `D/mirkwood-kit.md` | 2026-11-27 06:02 |
| `D/mirkwood-kit-handover.md` | 2026-11-26 21:24 |
| `D/mirkwood/README.md`, `coverage.md`, `materials.md`, `meshes.md` (head) | 2026-11-26 18:04 to 21:00 |
| `D/mirkwood/design-language.md`, `reference-study.md`, `siege-controls.md`, `textures.md`, `trim.md`, `distance-studies.md` | 2026-11-26 18:14 to 21:26 |
| `D/mirkwood/approach.md`, `hall.md`, `settlement.md`, `siege-kit.md`, `lessons.md` | 2026-11-27 05:53 to 06:02 |
| `R/FEEDBACK_MIRKWOOD.md`, `R/verify_mirkwood.json` | 2026-11-27 09:31 |
| `R/look/REVIEW_MIRKWOOD_LOOK.md` | 2026-11-27 09:15 |
| `R/craft/REVIEW_MIRKWOOD_CRAFT.md` | 2026-11-27 09:22 |
| `R/craft/audit_all.jsonl`, `audit_siege04.jsonl`, `material_census.json` | 2026-11-27 09:13 to 09:18 |
| `R/verify/end_sections.txt`, `texture_sizes.txt` | 2026-11-27 09:28 |
| `D/kingdom-kits-prompt.md` | 2026-11-27 09:30 |
| `D/kingdom-kit-layout.md` | 2026-11-26 17:09 |
| `D/settlement-scene-needs.md` (lines 1 to 935 read) | 2026-11-26 19:31 |
| `D/rivendell-siege-set.md` (grep only: "Dont use codex", "25,516, 9,050 and 3,608", "probe", "GD4") | 2026-11-27 09:35 (first read), 09:56 (revision) |
| `E:/LOTRAOMAssets/_auto_workspace/kingdom_kits/rivendell/rivendell_reference.html` (text) | 2026-11-26 19:51 |
| `docs/features/tripo-cli.md` (lines 60 to 110) | 2026-11-27 09:51 |
| Memory `feedback_asset_art_rules.md` (lines 100 to 200) | 2026-11-27 08:39 |
| Memory `features/kingdom-kits.md` (grep for Mirkwood) | 2026-11-27 05:51 |

Read for the revision on 2026-11-27 between 10:13 and 10:32 CST:

| File | Modified |
|---|---|
| `.../handovers_2026-11-27/mirkwood/check/CHECK_HANDOVER.md` (the check) | 2026-11-27, about 10:12 |
| `W/STATE.md` (whole) | 2026-11-27 10:12:18 |
| `T/mirkwood/delivery.md`, `E:/repos/TAOM/docs/features/mirkwood-kit.md` (head); `diff` of every `T/mirkwood*` file against D | 2026-11-27 10:12:17 |
| `D/mirkwood-kit-handover.md` sections 1, 2, 5, 7 to 12 | 2026-11-26 21:24 |
| `D/kingdom-kit-layout.md` (whole) | 2026-11-26 17:09 |
| `.claude/agents/kitbash-architect.md` lines 170 to 178 | as read |
| `D/mirkwood/coverage.md` lines 1 to 40; `D/mirkwood/README.md` lines 20 to 60 | 2026-11-26 18:04, 21:00 |
| SSN lines 722 to 745, 805 to 823, 862 to 895 | 2026-11-26 19:31 |
| `R5/kit_full/forms_set_1/NAMES.md` (head); `R5/kit_full/gates/probe/PROBE_OWNER_STEPS.md` (head) and the probe folder listing | 2026-11-26 |
| `_shared/review_page/page_maker.py` (header) | as read |
| `W/kit_full/*/staging*` FBX listings, `houses_01/staging_02/README.md`, `siege_04/staging/review_export.json` (names) | 2026-11-26 |
| `R/verify/check_colours.txt`; memory `feedback_asset_art_rules.md` (grep of the quotes); memory `MEMORY.md` (`wc`) | as read |
| `W/refs/owner_2026-11-26/ref_01.png` (looked at again: the pier count) | 2026-11-26 17:03 |

## Not checked

| Point | Smallest step that checks it |
|---|---|
| Whether he saw siege 04, the houses, the hall and the compounds | Ask him (question 0 and the page) |
| Whether the other client's session has stopped (it wrote `W/STATE.md` at 10:12 on 2026-11-27), and who landed PR #1626 | Ask him (question 0); `find W -type f -newermt "2026-11-27 10:13"` before the first write |
| Whether the Mirkwood docs in the worktree are committed on `feat/kingdom-kits`, and whether the branch holds trunk's merge of PR #1626 | `git -C E:/repos/taom-kingdom-kits status --porcelain docs/kitbash/mirkwood*` and `git -C E:/repos/taom-kingdom-kits log --oneline -3 -- docs/kitbash/mirkwood` (reads; the writers of this handover ran no git) |
| The ref_01 pier count (four broad and two thin, by eye on a 611 px picture) | A crop of the pier feet at full size |
| The held kit's numbers today (FBX hash, 117 packages, 273 prefab entities, 11 scenes) | Step 2 of the first day |
| The 24 settlements and their scenes today | `python -I culture_scenes.py "$MODS" <out.json>` from `_shared/scene_needs/scripts` |
| The prefab entity count today | `check_prefab_budget.py` over the five module folders |
| Back faces in the earlier Cycles renders | One culled viewport render of `W/kit_full/site_01/view/mirkwood_compound_layouts.blend` |
| The outer leaf swing side in a picture | One top view of the open pose of `W/kit_full/gate_leaf_01` |
| Interpenetration, curve turns and UV phase of the built pieces; craft of the lodge, cottage, hall, bridge, gallery, corner | `R/craft/fbx_craft_audit.py` on `houses_01/staging_02` and a Blender job with the reviewer's scripts |
| What TAOM and vanilla hold for caves and cavern rock | A search of the Moria scenes and the vanilla scene prefabs for rock and cave meshes |
| What the AI does on a railless walk, and the least walk width | One siege or battle test on a test scene (SSN section 15 item 19) |
| The colour casts of the pictures (scene light against material) | Crops of the same surface in two or more pictures under different light |
| Whether issue #1620 is open | `gh issue view 1620` |
