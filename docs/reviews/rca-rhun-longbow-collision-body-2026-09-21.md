# RCA: the Rhun longbows had no collision body of their own, and the review caught a false mechanism (#633, 2026-09-21)

A player on the September Nexus build reported that battles against Rhun never finish loading,
and narrowed it to four bow items by hand. Their workaround was to repoint the affected troops at
a lower-tier bow. The maintainer's recollection was that this had been fixed in v2.0.28.

It had not. And the first explanation written for it was wrong, which the `/deep-review` found
before anything was committed. This RCA covers both: the data defect, and the review findings.

## What was actually wrong in the data

Three Armory longbow meshes have no `bo_` collision twin, so all three borrowed the elven bow's:

| Item | Mesh | Body it carried |
|---|---|---|
| `sm_rh_drag_longbow_a` | `sm_rh_drag_longbow_a` (`rhun_weapons/dragon`) | `bo_wm_elven_bow_a03` (`elven_weapons`) |
| `sm_dg_khml_longbow_a` | `sm_dg_khml_longbow_a` (`rhun_weapons/khamul`) | `bo_wm_elven_bow_a03` |
| `sm_rh_loke_longbow_a` | `sm_rh_loke_longbow_a` (`rhun_weapons/loke`) | `bo_wm_elven_bow_a03` |

Plus six generated clones that inherited the pairing by `copy.deepcopy`:
`ladder_rhun_new_bow_t7/t8/t9` and `ladder_dolguldur_bow_t7/t8/t9`. Nine items in all.
`sm_rh_loke_longbow_a` had the same defect and no player had ever reported it.

The borrow was never meant to survive. `docs/ai-includes/weapon-creation-workflow.md:78` states the
rule (a weapon body is `bo_` plus the exact mesh id) and :80-82 sanctions borrowing a same-shaped
body as a placeholder until the artist delivers. The placeholder shipped. A borrow is a dependency
on art the borrower does not own: the 2026-09-11 art drop had already renamed the elven bow bodies
once (#599), which is what makes it a hazard regardless of anything else.

## Why the version history misled

Three separate things made this look already-fixed.

1. **`v2.0.28` predates the bug.** The tag is `8ce42fe8`, 2026-09-04. The ladder items were created
   2026-09-12 in `f46abb62`. `bannerlord-1.4.5` kept its `<Version>` at `v2.0.28` for eleven days
   past the tag, so "2.0.28" names both a tag and an open moving window.
2. **#617 looked like it touched these items.** It renamed the band ids (`_x`, `_c`) to per-tier ids
   (`_t7`, `_t8`, `_t9`) across exactly these files. It changed no art ref.
3. **#599 looked like it had repaired these items.** It did repair them: it repointed the retired
   `bo_wm_elven_bow_v*` names onto `bo_wm_elven_bow_a03`. It repaired the borrow instead of ending
   it.

**"The last change touched this file" is not evidence that it addressed this defect.**

## Why every desk-side gate missed it

Every gate asked whether a reference resolves, and a borrowed name does:

- `MISSING_COLLISION_BODY` passed. `audit_armory_refs.py` reported **CLEAN** on 2026-09-20.
- `generate_ranged_ladder_items.py --verify` checks ids, speed, damage, accuracy, usage and the
  localization row, never `mesh` or `body_name`. A clone inherits its donor's art verbatim.
- `check_rdc_entries.py` checks package registration, not whether a named shape is in a pack.
- There was also a window where `missing_collision_body_issues` was defined but never called by
  `main()` (2026-09-15 to 2026-09-18, #622), covering the period when these items were current.

## The fix

The maintainer asked whether the body could be authored in Blender from the FBX the mesh already
lives in, rather than repointed to a native body. That was adopted: `tools/blender/add_collision_body.py`
duplicates the mesh's lowest LOD into `bo_<Mesh>` in the same FBX, which is how the 18
`bo_SM_RH_Loke_*` bodies already in that file were made (several are the mesh's `.lod4`
verbatim). All three longbows are one geometry (1524 verts, 1.869 m), so one shape serves them:
30 verts, the full 1.87 m, against the 1.20 m the longest native bow body (`bo_longbow_a`, by its
`weapon_length` proxy) would have given. Modding Kit import confirmed: physics shapes 388 to 391,
`check_rdc_entries.py` `without-rdc=0`, the catalogue shows each twin beside its mesh. Donors
repointed, clones regenerated. The three bodies carry the FBX material `metal_weapon` (the Loke
precedent) where the artist's elven bow body carries `wood_weapon`; the item XML's
`physics_material` is what `Mission.cs:3244` hands to the physics engine, so the FBX material is
not known to matter, and the tool now requires `--material` instead of guessing.

The shipped `E:\LOTRAOM_Releases\patreon` tree cannot carry the twins (its cooked packs predate
them), so its seven affected items were repointed to native `bo_longbow_c` (present in
`Native/AssetPackages/bodies_shared.tpac`). That edit invalidated three `sha256` entries in the
tree's `manifest.json`; the maintainer chose to keep the edit and re-cut the manifest with the
packaging tool, which lives outside this repo. Until that is done the tree is inconsistent with
its own manifest.

## The review findings

The `/deep-review` ran eight lenses in two waves. Every finding below is confirmed against the
source, not taken on report.

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | HIGH | The mechanism written as fact in eleven places (mesh cooks into `pack6`, body into `pack0`, so the body is absent when the mesh loads) is false. The cook groups every Armory body into `pack0`/`pack1` and every mesh into the other packs; the working elven and Isengard bows are split identically; `bo_wm_elven_bow_a03` is present in the patreon `pack0`, so the pre-fix XML resolved there. Three lenses found it independently; the orchestrator confirmed it with `scan_tpac_metameshes`. | fabrication | A byte-grep "proved" the working bows' mesh and body shared `pack0`; the mesh hit was `wm_elven_bow_a03` matched inside `bo_wm_elven_bow_a03`. A substring count was treated as a TOC read, and the hypothesis went into CLAUDE.md, a gate message, a lesson and an RCA before any per-pack TOC scan. Issue #633 even said "being confirmed separately". | Gate rewritten as `COLLISION_BODY_BORROWED` (ownership, not residency); all eleven passages reworded; the player's cause recorded as OPEN. Rule: a claim about what a pack contains is made from a TOC read (`validate_mesh_refs.scan_tpac_metameshes` reads cooked packs), never a substring grep. |
| 2 | HIGH | `validate_mesh_refs._ITEM_OPEN_RE` never matched `<CraftingPiece>`, so all 1,055 piece refs carried item id `""`; the gate paired all 313 piece bodies with the file's LAST mesh and skipped every one. 58 real piece borrows were invisible and 303 false positives were latent. | data flow | The gate was built and proven on `<Item>` refs only; nothing in the synthetic tests had a piece. The existing `test_validate_mesh_refs.py` piece case used `<Item>` elements under a `crafting_pieces.xml` filename. | Regex gains `CraftingPiece`; `test_crafting_piece_owns_its_refs` and `test_crafting_pieces_are_paired_per_piece` added. Rule: a per-item pairing is tested on every element kind the extractor sees. |
| 3 | MED | `add_collision_body.py` overwrote an unversioned artist FBX with no backup, against `.claude/rules/moduledata-validation.md` ("mandatory for any script that writes outside the repo") and the sibling's `.bak-matremap` convention. The session had backed up by hand; a future run would not. | tooling | The rule loads on `tools/**/*.py` and was in context; the fingerprint was treated as the safety net and the docstring even said "back the FBX up anyway". | Write-once `<fbx>.bak-bo` before `os.replace`. |
| 4 | MED | `.claude/rules/moduledata-validation.md` had no row for the new code; `tools/README.md`, the feature doc and the lesson did. | registration | No test checks rule-table parity (`CommitGateCoverageTests` checks the hook, not the rule). | Row added. Follow-up worth its own issue: a parity test between the code table and `validate_moduledata.py`'s emitted codes. |
| 5 | LOW | `parse_args` ran before the report path existed, so a bad argument under the detached launcher left no report; no `__main__` guard, so the script could not be imported for tests. | tooling | Copied the sibling's export settings but not its `__main__` error envelope. | Report path derived from a best-effort `--fbx` first, `parse_args` inside the envelope, guard added, nine unit tests with `bpy` stubbed. |
| 6 | LOW | "The longest native bow body is 118" (`bo_longbow_c`, `woodland_longbow`) is wrong by its own proxy: `bo_longbow_a` serves `weapon_length` 120. | fact | Took the first single-length row of a table as the maximum. | Corrected in CHANGELOG and here. |
| 7 | LOW | The hook's timing ledger said 8.2 s warm; measured 14.9 to 15.6 s, and the ledger blamed the tpac TOC scan (0.2 s) for cost that is `extract_refs` (3.1 s per body pass, 1.3 s of it a quadratic line count). | measurement | The ledger was not re-measured when the pass was added. | Ledger rewritten with the stage breakdown. Follow-up: the `_lineno` quadratic in `validate_mesh_refs.py:206` (3.06 s to 1.79 s measured, output identical). |
| 8 | LOW | The three Shields tpacs were rewritten by the Kit import (2 bytes each) and three RDC entries were rewritten; none were synced to the mirror, and the `.bo-report.json` sidecars sat in the live `AssetSources/`. | live state | The sweep for changed live files was run only at review time, not after the Kit import. | Synced; sidecars moved to the backup folder. Rule: re-run the newer-than sweep after any Kit save, it rewrites more than you asked it to. |

**Root-cause pattern across findings 1, 6 and 7:** a number or a mechanism was stated from a
proxy (a substring count, a `weapon_length`, an old ledger) instead of the thing itself, and each
proxy was one step cheaper than the real read. `.claude/rules/evidence-over-claims.md` §C names
this; the failure was treating a grep as evidence because it printed a pack name.

## Why each agent missed these, and which found them

Lens 1 (Standards) found 3, 4, 5, 7. Lens 2 (Engine) found 1 and 6 by decompiling `PreloadHelper`
and reading the packs' TOCs. Lens 5 (Data flow) found 1, 2 and the manifest mismatch, and measured
the mirror gap. Lens 7 (XML) found 1 independently by scanning the release packs against their own
XML. Lens 3 (Efficiency) measured 7 and rejected a shared-scan refactor under the simplicity
criterion. Lens 4 (Completeness) listed every passage carrying the false mechanism. Lens 6 (Design)
proposed the ownership test that replaced the residency test and measured its 0 item violations.
The Tooling lens found 2, 3, 5 with reproductions and confirmed the scratch scripts' byte
discipline.

Nothing was missed by every lens; the orchestrator was the single point that produced the false
mechanism, and eight independent readers were what caught it.

## What is still open

- **The player's hang cause.** Ask for their `LOTRLOME_Armory/AssetPackages` listing or
  `manifest.json`, the `body_name` on the three donors in their `LOTRAOM_weapons.xml`, an
  `rgl_log`, and their TaomVersion. If their packs predate the 2026-09-11 art drop (no
  `bo_wm_elven_bow_a03`) under post-#599 XML, that is the #599 class and elven-bow carriers hang
  for them too. #633 does not close on the dev-tree fix alone.
- **A cooked-tree body pass.** `validate_mesh_refs` reads `AssetPackages/*.tpac`; a MISSING-body
  run of a release tree's own XML against its own packs takes 3 s and nothing runs it. The lens
  ran it on patreon: 0 missing. Worth an issue and a `/release` step.
- **The patreon manifest** must be re-cut before that tree ships.
- **In-game smoke on both trees.** Nothing has been smoked.
- Follow-ups not applied here (pre-existing code): `holster_body_name` is polled by the engine and
  read by no gate (`validate_mesh_refs.COLLISION_BODY_ATTRS`); the `_lineno` quadratic; `Languages/`
  folders scanned for mesh refs they cannot carry; `_ART_MODULES` omits `TAOM.Dependencies`,
  `TAOM/Assets` and `TAOM_Map/Assets` (no ref resolves there today).

## Verification

`validate_moduledata.py` PASS on `MISSING_COLLISION_BODY`, `COLLISION_BODY_BORROWED`,
`MISSING_VISUAL_MESH`; the new gate fires on `LOTRAOM_weapons.xml:8643` with the borrow restored on
one donor and is clean after revert; `audit_armory_refs.py --regen-catalogue` CLEAN, 0 errors;
`bash tools/test_hooks.sh` 210 passed; the tool's `--apply` proven on a scratch copy (report on bad
arguments, backup written, body asserted, zero drift). Full suite: see the CHANGELOG entry.

## Lessons codified

`docs/reviews/lessons/xslt-moduledata.md`: a collision body that resolves can still be borrowed, and
a substring grep is not a TOC read.
