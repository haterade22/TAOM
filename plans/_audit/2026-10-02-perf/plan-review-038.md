# Plan review 038, round 1 (cold reviewer)

Plan: `plans/038-battle-equipment-memory-audit.md` (939 lines). Code read at `0912e1b7` (the worktree
HEAD is `f852a0b7`, docs-only commits since; `git diff --stat 0912e1b7..HEAD -- tools/` prints
nothing, so every in-scope file is identical). No earlier `plan-review-038*.md` existed.

## Verdict

Executable with one blocking fix. The excerpts, engine citations, pack facts and sampled oracle all
check out against the code and the packs; the defects are an underspecified de-duplication that the
Step 9 oracle depends on, and a set of API and wording gaps a weak executor would have to improvise.

## What I verified (each a check that could have failed)

- Map tool at `0912e1b7` (`git show 0912e1b7:tools/audit_map_scene_memory.py`): `PACK_TREES` :145,
  `AssetIndex` :272, `add_module` :287-313 (matches the excerpt verbatim), `get` :331, `SEG_MESH_A/B`
  :129-130, `ZERO_GUID` :132, `scan_guids` :433, `parse_material_meta` :411, `parse_metamesh_records`
  :452, `plausible_counts` :477, `texture_chain_bytes` :377, `_texture_row` :766 with the size rule at
  :795-807, docstring :39-44 and :83-84, `build_manifest` :811 with `add_module` at :816-818, `fmt_bytes`
  :1121, `write_tsv` :1141, `refuse_inside_game` :1340, `main` :1348-1382. All match.
- Engine (v1.5.3 decompile cache): `MissionPreloadView.OnPreMissionTick` 18-39, `PreloadHelper` 20-56 and
  112-162, `GetMultiMesh` 31-47, `GetMultiMeshCopyWithGenderData` 42-72, `ArmorComponent` 113, 159-162,
  195, `ItemObject` 355-368 and 503-529, `CraftedDataView` 148 and 224-230, `CraftingPiece` 159 and 234,
  `BladeData` 50-52, `HorseComponent` 157-206, `BasicCharacterObject` 106 and 365-418,
  `MBEquipmentRoster` 55-136, `Equipment.DeserializeNode` 204-224. The gender and slim candidate table
  (plan 617) matches the engine order exactly. Mismatches listed below.
- Packs (probe script in my scratch, importing the map tool): release Armory 10 packs, 4,549 metameshes,
  2,595 textures, 950 materials, 393 physics; 1,203 packs for Armory, TAOM, Native, SandBox. Chainmail
  render 1,925,634, edit 2,201,032, segment `3e6141af` present; `dwarf_hair_a` render 6,570,012, edit
  78,919,980; chainmail `.0` to `.3` records fail `plausible_counts`, `.lod1.*` pass. All five sampled
  rows resolve as the table says (`_slim` exists for the chainmail only; all three bodies resolve).
  `t_gd_ano_chainmail_a1_d`: release DXT1 1024x1024, 11 mips, 699,064 from the pixel segment; live loose
  copy decodes to the same and gives 699,064 by formula. Live Armory: no `AssetPackages`, 5,454 loose
  packs, 13 of 4,549 metameshes carry `97f81dbb`.
- Release XML: troop excerpt (`troops_gondor.xml:6-37`), woodsman's two shields, dolguldur roster
  :6-17, SandBoxCore civilian roster :5471-5480, crafted axe and its Blade piece, shield and chainmail
  attributes (`has_gender_variations="false"`). `docs/reference/release-process.md:114` names
  `<releases>\<channel>\Modules\`. Cited doc lines (battle-load-diagnostics 420-427, native audit L2,
  `native-mesh-editdata.txt` "Callers") exist.
- Tests: `python -B -m pytest tools/tests/test_audit_map_scene_memory.py -q -p no:cacheprovider` gives
  `33 passed in 0.29s`. `python -B -m unittest discover -s tools/tests -t .` gives `Ran 2962 tests`,
  `FAILED (failures=3, skipped=8)`, failing exactly `test_applying_every_spec_is_a_no_op`,
  `test_the_committed_career_file_is_what_the_rule_derives`, `test_default_is_on_the_e_drive`. The
  dotnet baseline (plan 22) is UNVERIFIED: my role does not run dotnet.
- The Step 2 CLI fixture works: a minimal `<scene><entities/></scene>` game folder runs the map tool's
  `main` to `rc 0` with nine outputs (today it prints `packs indexed: 0`).
- Step 10's dash grep form detects both U+2014 and U+2013 on this machine; row 62 of `tools/README.md`
  and the map tool hold no dash today. The plan prose has no em or en dash, no local absolute path, no
  secret.
- Drift-check paths (plan 8-10) equal Scope (plan 423-429) plus two read-only references.

## Blocking

1. **`meshes` and `bodies` in `items.tsv` are not said to be distinct (plan 289, 619, 728-730), yet
   the Step 9 oracle needs them distinct.** The crafted axe's blade gives `body_name` and, with no
   `holster_body_name`, `body_name` again (plan 619, `ItemObject.cs:366-367`), so a literal list prints
   `bo_wm_lossarnach_1h_axe__blade;bo_wm_lossarnach_1h_axe__blade`; the chainmail's `[m]`, `[m_male, m]`
   and both female lists all resolve to `sk_gd_los_inf_chainmail_a`. `resolve_names` returns lists
   (plan 628). The executor then fails Step 9.2, and the STOP at plan 889-892 ("do not tune the rule to
   the data") fires. The engine itself de-duplicates (`PreloadHelper` keeps `HashSet`s, lines 12-18).
   Fix: say "the distinct resolved names" at plan 729 and have `resolve_names` return each item once.

## Non-blocking

1. `battle_sets(character)` (plan 569-571, 600-601) and `troop_assets(character)` (plan 669) take no
   definitions or index, but must resolve `<EquipmentSet id>` against standalone rosters and skins
   against races. Give the signatures (for example `battle_sets(character, defs, include_civilian=False)`)
   and the set's shape (a dict keyed by slot, with `Item0`..`Item4` normalised to the engine names so an
   override replaces the same slot, `Equipment.cs:225-233`). Step 7 also leaves `AssetSet`, the
   per-culture and per-side functions and most test names to the executor.
2. Refusal exit (plan 757-759, 764): `amsm.refuse_inside_game` raises `SystemExit("refusing to write
   inside the game install: ...")` (:1345), so exit status 1, not 2, and its text says "game install"
   for the release folder too. Name the oracle for `test_refuses_to_write_inside_the_game_or_release`
   (assertRaises SystemExit, called for both roots).
3. `IncludedGameTypes` is ignored (plan 541-546, 594-597). Native registers `mpitems`,
   `native_equipment_sets` and `mp_crafting_pieces` for `MultiplayerGame` only (`Native/SubModule.xml`
   80-90, 131-135). Loading them gives 11 `DUPLICATE_DEFINITION` rows against SandBoxCore items and lets
   an MP-only id resolve when the campaign would not. Skip nodes whose `IncludedGameTypes` omit
   `Campaign`, with a test, or list it under APPROXIMATIONS.
4. Skins: the Armory has nameless `<beard_mesh>` (`skins.xml:1297`) and empty attributes
   (`underwear_bottom_mesh=""`). Plan 576-578 and 670-672 should say empty or missing names are dropped,
   as plan 623 says for items, or `RACE_MESH_UNRESOLVED` counts blanks.
5. `amsm.write_tsv` joins list values with `|` (:1146); `items.tsv` wants `;` (plan 730). Say to pass
   pre-joined strings.
6. Step 2 (plan 479-482): the copied encoders use `a.KIND_BY_TYPE_GUID` (test module :61) and need
   `hashlib` and `struct`; say to rename `a.` to `amsm.`.
7. Step 10 dash check (plan 829) diffs the working tree after commit 1, so the map tool's new docstring
   and help text are never checked. Use `git diff -U0 <base> -- tools/README.md tools/audit_map_scene_memory.py`.
8. Step 11.4 and Done (plan 840-844, 879): `git status --porcelain` "empty" or "only three files" fails
   in a worktree with pre-existing untracked files (this program worktree has nine `?? plans/...`). Phrase
   it as "nothing beyond what Step 1 recorded".
9. Step 10's local-path grep (plan 830-832) expects "the output folder" constant, but plan 715-716
   derives it from `amsm.DEFAULT_OUT`, so only the release constant should appear; and the README row
   "in the same style" (plan 821) would copy row 62's `E:\` path. Say placeholders there too (rule 14).
10. APPROXIMATIONS (plan 808-820) omits the siege missiles `MissionPreloadView` also preloads (lines
    40-44, `PreloadItems(GetSiegeMissiles())`) and the `Stealth` equipment type (`MBEquipmentRoster.cs:114`).
11. Plan 106-117 says to import `AssetIndex` "unchanged" while Step 3 changes it; harmless, but the
    blast-radius list (plan 398-401) misses `plans/_audit/2026-10-02-perf/evidence/native/editdata_share.py:12`,
    which imports the map tool (default unchanged, so no impact).

## Excerpt mismatches

- Plan 200-201: `GetMultiMesh` falls back to `GetMultiMeshCopy()` when the result is null **or
  `MeshCount == 0`** (`ItemCollectionElementViewExtensions.cs:42`).
- Plan 245-249: `<Equipments>` and `<equipments>` are both accepted (`BasicCharacterObject.cs:365`); a
  lowercase `<equipmentRoster>` passes the name test (:377) but `MBEquipmentRoster.Init` (:44-54) builds
  a set only for `EquipmentRoster`, so it adds none. No release or vanilla file uses the lowercase form.
- Plan 125-127: `OnPreMissionTick` also preloads siege missiles (lines 40-44).
- Plan 398-401: the `git grep` list is incomplete (see non-blocking 11).

## Template checks

- Every step ends in a command with an exact expected result, except Step 7's "all classes so far
  pass" and Step 9.4's read of one row, which are acceptable.
- No C#, so TDD is per Python behaviour; RED steps name the expected exception (`TypeError`,
  `SystemExit: 2`, `ModuleNotFoundError`). ADRs correctly declared not applicable; no protected or
  single-owner file in scope; non-deploying dotnet with `-p:ModuleId=`; no worktree path or branch
  name; no CHANGELOG step; issue line present; STOP conditions are plan-specific.
