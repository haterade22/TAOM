# RCA: deep review of the Lothlorien round 2 work (2026-11-06)

## Top line

A tier 1 `/deep-review` of commits `d59fcef0c` and `12503a211` (issue #1085) ran Codex at `ultra` and four Claude lenses: Data flow, Engine compatibility, XML and Tooling. The scope included the round 1 fix pass, which landed after the 2026-11-05 review and had never been reviewed again. Codex and the lenses together confirmed one HIGH finding, seven MEDIUM and about a dozen LOW. The HIGH one is a consumer I never listed: the generated lord template file still gave a new Lorien ruler, a rebel lord and a woman who comes of age as a lord the Rivendell gold set. Codex rated it HIGH and two lenses rated it MEDIUM because the rosters were Rivendell before the commit; I treated it as HIGH because players meet it and my docs promised the opposite.

Every confirmed finding below is fixed, recorded as a decision for the maintainer, or listed as a deferred item with its reason. Record of the raw reviews: `docs/reviews/raw/codex-prereview-lothlorien-round2-2026-11-06.md` (Codex) and the prompt beside it.

## Findings

| # | Sev | Finding | Found by | Status |
|---|---|---|---|---|
| F1 | HIGH | `taom_lord_template_equipment.xml` is generated from the first battle set of each culture file. Its four Lorien lord and ruler battle rosters kept `rivendell_helmet_gold`, `rivendell_body_gold_a` and three more Rivendell pieces after I rewrote `lothlorien_bat_template_medium_a`. The engine picks them for a new ruler, for lords spawned from `spc_lothlorien_lord_1` and `_2` (rebellions, companion clans) and for a Lorien woman who comes of age or becomes a lord. Regenerating the file is not safe: its dry run renders 186 rosters against the 306 shipped. | Codex, Data flow, Engine | Fixed: the five armour slots of the four rosters hand-ported from `medium_a`; `LothlorienGeneratedLordAndRulerBattleRosters_WearLorienArmourOnEveryArmourSlot` fails on 20 slots before the port and passes after. |
| F2 | MEDIUM | Three Isengard starter boots (`starter_*_isengard_leg_a`) draw the Uruk-hai shoe mesh and were `covers_legs="true"`, against the maintainer's decision that those shoes show the shin. `covers_legs=true` clears `SkinMask.LegsVisible` for the whole outfit. | me, Codex, XML, Tooling | Fixed: set to false by mesh; `tools/audit_leg_cover_flags.py` reports all three on the pre-fix file. |
| F3 | MEDIUM | No gate read `covers_legs`; the two armour generators' `--check` was in no `data_gates` row; `--apply` and `--check` could be passed together and `--check` won silently. | XML, Tooling | Fixed: the audit, three `data_gates` rows, mutually exclusive flags. |
| F4 | MEDIUM | `tools/_generated_files.py`, the writer both armour generators use, had no test; the Lorien troop generator's skill carry-over had none either. | Tooling | Fixed: `tools/tests/test_generated_files.py` (9 tests). |
| F5 | MEDIUM | The Snow Orc armour generator also owns its values, but the `rebalance_armor.py` skip covers only `sk_ll_`. A documented restat would change 5 of its 20 items and the generator would change them back. | Tooling | Fixed (Mike, 2026-11-06, D3): the generator owns the values; `sk_gb_orc_` joins the restat skip. The every-mode test covers both kits, and a restat of the Snow Orc folder changes nothing. |
| F6 | MEDIUM | The seven Lorien lord-grade pieces are `is_merchandise="false"`. `docs/features/armour-acquisition.md` says lord kit is sold at a level 3 armoury, and 169 of the 171 lord-class pieces are merchandise (the two that are not are Lorien's `_lord_` helmets). | XML | Decided (Mike, 2026-11-06, D1): keep them lord-only; recorded as the exception in `armour-acquisition.md`. The ladder awards them. |
| F7 | MEDIUM | `armour_from="rivendell"` still sends Lothlorien to Rivendell's armour in the lord ladder, the lord harness and the culture item pool, and the ladder's material and weapon rows are Rivendell's. My docs said only that markets stock Rivendell armour. Dropping the row alone empties the ladder's rungs. Correction: the weapon rung already had Galadriel's sword; only the material, the Deep Seam row and the commission row were Rivendell's. | Data flow, Codex | Fixed (Mike, 2026-11-06, D2): Lothlorien has its own ladder, with no donor. See below. |
| F8 | MEDIUM | The lotraom-assets mirror holds none of this change's Armory edits (eight live files, including the t2 and t3 bows that six TAOM references name). A sync from the mirror reverts all of them. | XML, Tooling, Codex | Owed: the maintainer syncs the mirror (D6). Listed in the feature doc. |
| F9 | LOW | The skip test for generator-owned items ran one restat mode. Moving the guard into one branch left it green while the materials-only mode rewrote the item. | Tooling | Fixed: every mode, with control items that prove each mode would have changed the item; two mutants of the guard both fail it. |
| F10 | LOW | The restat report dropped generator-owned items silently, and `derive_armor_tiers.py` called them under or over target. | Tooling | Fixed: the report counts them; the map's status is `generator-owned`. |
| F11 | LOW | Wrong numbers and places in my writing: "56 of the 97" (97 is Lorien plus Snow Orc), "26 roster ids" (6 rosters, 26 changed lines), "Snow Orc armour exists only in the live Armory" (the mirror holds it). | Tooling, Codex | Fixed in the comment, the docs and issue #1085. |
| F12 | LOW | The generator order in two docs was wrong: the ladder item generator and `rebalance_ranged_ladders.py` come before `rebalance_troops.py`, which refuses to run otherwise. | Tooling | Fixed. |
| F13 | LOW | I wrote that a package without a cache entry is skipped whole. The native code is not traced, and material and animation master packages load without one. | Engine | Fixed: mesh and animation clip packages, "observed". |
| F14 | LOW | "All 11 lords wear Lorien armour": Galadriel's greaves are Rivendell's, and Galadion (age 10) is created with child equipment. | Engine, Codex | Docs corrected; Galadriel's greaves became `sk_ll_gl_grvs_heavy_a` (Mike, 2026-11-06, D5), pinned by `LothlorienNamedHeroBattleSets_WearLorienArmourOnEveryArmourSlot`. |
| F15 | LOW | Stale text: `party-template-sizing.md` named the Rivendell default template for Lothlorien; three docs and the generator header told readers to regenerate the lord template file. | Data flow | Fixed, with a warning citing #637. |
| F16 | LOW | The round 1 RCA rated the gender flag flip MEDIUM. A missing `_female` mesh falls back to the base mesh without a log line, so the flip changed nothing a player sees. | Engine | Corrected in that RCA. |
| F17 | LOW | Weak tests of mine: the lord template test accepted a deleted slot; the clan test read template names, not the clan bindings; the leg test let eight pieces shrink to six. | Codex | Fixed. |
| F18 | LOW | `_generated_files.py` always writes CRLF without a BOM. | Codex | Not applied: every live armour file I compared with its backup is CRLF without a BOM, and the docstring states the format. A guard for a case no folder has fails the simplicity rule; reopen if a folder uses another format. |
| F19 | LOW | The villager reuses Rivendell's name key `{=aom_rv_villager}`. | XML, Engine | Not applied: the English is identical and Lindon does the same. Rename if this villager's name ever diverges. |
| F20 | LOW | The Lorien and Snow Orc troop generators have no `--check`. | Tooling | Standing deferred item, below. |
| F21 | UNVERIFIED | A skirt in `SK_LL_RG_Chests_A.fbx` has LODs but no LOD0; Lorien helmets mix `hair_cover_type` type2 (19) and all (10); `sk_ll_rg_boots_light_a` is now `true` and may leave a gap above the boot. | XML, Engine | In-game checks 13 to 15 in the feature doc; the first is an art question. |

## Root-cause pattern: the review followed the diff, and the promise lives in consumers

F1, F5, F7 and F8 are one failure. Each fix edited a source (a template, a value, a flag) and left a copy or a second owner of the same data: a generated file, a second generator, a donor setting, a mirror. A review scoped by the diff cannot see a file the diff does not touch. F1 was found only by the reviewers that read the generator and followed the file forward.

F2 and F11 are a second pattern, selecting or stating by the wrong key. The script chose boots by id when the decision was about a mesh. The docs gave counts and places that no command had printed.

## Why each reviewer missed what it missed

- **Me, before the review:** the consumer list in section 3d was written from the troop side. It named no file that is derived from a hand-edited source.
- **XML lens:** its file list named the Lorien equipment file but not the generated lord template file, and the generated file is in neither commit, so a diff-scoped list could not contain it. It found F2, F6 and F8 and did not report F1.
- **Tooling lens:** it examined the Lorien and Snow Orc generators and the one-off scripts. It did not open the lord template generator. It found F2 by reading the scripts, and F3 to F5.
- **Data flow lens:** it found F1 and F7 by walking consumers, and did not group leg items by mesh, so it did not report F2.
- **Engine lens:** it checked every engine claim against the installed DLLs (F13, F14, F16) and found F1 independently from the model's call sites.
- **Codex:** it found F1, F2, F8 and F11 with engine quotes. It treated the retained `armour_from` as a documented limit and did not count it as a defect.
- **Round 1 (2026-11-05):** its Data flow lens walked the consumers of the troops and did not report the lord template file.

## Decisions (Mike, 2026-11-06)

- D1 (lord-grade pieces): kept lord-only, `is_merchandise="false"`. Recorded in `armour-acquisition.md` as Lorien's exception.
- D2 (armour ladder): Lothlorien gets its own. `taom_lords_material_lothlorien` ("Lórien Mallorn-silver", a placeholder name, wearing
  Mirkwood's bar `lord_material_06` until the artist clones a fourteenth), its `<Material>` row and the compiled default in
  `ArmourAcquisitionConfig.cs`, its English name seeded into the 12 language files (a paid translation is owed, row added to #763),
  a Deep Seam row and an Armourer's Commission row (reward `sk_ll_gl_chest_heavy_a`), and no `armour_from`. Lindon's rows no longer
  list Lothlorien. `LothlorienArmourLadder_UsesItsOwnMaterialWeaponKitAndQuestRows` pins it; four shipped-data tests and the issue
  count (82 to 84) moved with it.
- D3 (Snow Orc values): the generator owns them. `GENERATOR_OWNED_ID_PREFIXES` is `('sk_ll_', 'sk_gb_orc_')`; no other item id starts
  with either.
- D4 (Elite Emissary): Lothlorien joins. Key settlement `town_L1` (Caras Galadhon), six offers (three Galadhrim terminal troops at
  28, three Marchwarden terminal troops at 18 Elven Wine), priced on the same level scale as the other cultures.
  `LothlorienEliteEmissary_OffersItsOwnEliteTroopsAtTheElvenWinePriceForTheirLevel` pins it.
- D5 (Galadriel's greaves): `sk_ll_gl_grvs_heavy_a`.

Still owed to Mike:

- D6: the mirror sync. The eight live files are `lothlorien/head_armors.xml`, `body_armors.xml`, `leg_armors.xml`,
  `ranged_ladder.xml`, `Languages/loc_lothlorien.xml`, `isengard/leg_armors.xml`, `isengard/starter_armors.xml` and
  `mordor/leg_armors.xml`. Re-run `python tools/generate_ranged_ladder_items.py --verify --asset-repo <mirror>` afterwards until it
  exits 0.
- The 14th lord's material bar: an artist clones `lord_material_14` (`tools/tpac_clone_metamesh.py`).
- The paid translation of `taom_lm_lothlorien` (issue #763).

## Deferred items

FOLLOW-UP: `generate_lothlorien_troops.py`, `generate_snow_orc_troops.py` and `generate_enlistment_rosters.py` have no `--check`, so `test_generator_regions_in_sync` cannot cover them. no issue filed (public action, waits for the maintainer's word).

FOLLOW-UP: issue #637 says the lord template generator would delete 40 rosters and re-equip 78. A script that compares the dry run with the shipped file, roster by roster, found on 2026-11-06 that the render holds 186 rosters against the 306 shipped, so `--apply` would delete 120 and re-equip 78 (82 before the Lorien port). A comment with the new counts goes on #637.

FOLLOW-UP: `docs/reviews/_clan_registry.json` is a scratch snapshot that still binds `clan_lothlorien_2` to the clan 1 template. Nothing reads it at runtime, and `tools/clan_registry.py` rewrites it. no issue: regenerate it the next time `build_clan_specs.py` needs it.

FOLLOW-UP: `LordKitSelector` counts a 10-year-old hero toward a peer kit: `lord_L1_3` (Galadion) makes `medium_c` look shared by two men. no issue filed (not this change; waits for the maintainer's word).

## Feedback codified

Added to `docs/reviews/lessons/data-content-cultures.md`: a file generated from a hand-edited source is a consumer, so regenerate it or hand-port it in the same change; restore a decision by the property behind it. Corrected there: the cache lesson (mesh packages) and the leg lesson (what the code proves). `docs/ai-includes/new-culture-authoring.md` section 3d now lists the generated lord template file and the armour donor, and the corrected generator order. `tools/audit_leg_cover_flags.py` and three `data_gates` rows make the leg rule a gate.
