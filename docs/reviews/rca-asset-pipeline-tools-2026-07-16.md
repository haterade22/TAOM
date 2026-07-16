# RCA — UE→Bannerlord Asset-Pipeline Python Tools (deep review 2026-07-16)

**Scope:** the 10 uncommitted `tools/oneoff/` scripts built across 2026-07-15/16 for the Rivendell + Tents kit conversions (UE export, Blender normalization/placement/reconstruction, texture conversion, material tpac generation). Reviewed by 5 focused tooling agents (the 5 core C#-centric agents don't apply to a pure-Python changeset; per deep-review Step 2c). All CRITICAL/HIGH findings verified against source before fixing; fixes applied in-session, deferred items recorded below.

## Verdict

NEEDS FIXES → fixes applied same session. 1 CRITICAL, 8 HIGH, ~10 MED confirmed; the pipeline's OUTPUTS shipped so far are largely unaffected (the critical/high bugs live in the parked citysplit/reconstruct paths or in latent branches), with two exceptions noted under "Output audit".

## Findings table (confirmed; fixed unless marked deferred)

| # | Sev | Bug | Why missed | Prevention |
|---|-----|-----|-----------|------------|
| 1 | CRIT | citysplit recorded every foliage placement at world origin — `bake_world` zeroes all matrices BEFORE the foliage split reads them | The foliage feature was bolted onto an already-bake-first flow; no output ever inspected (city runs kept being killed before completion) | Fixed: foliage split + transform snapshot moved BEFORE `normalize_scale`. Lesson: any "record transforms" feature must run before any bake step; grep for `matrix_world` reads after `bake_world` calls |
| 2 | HIGH | `chunk.dimensions`/`bound_box` stale right after `join()` — tiny-cluster filter degraded, report dims 0.0, re-pivot silently no-op | The 0.0-dims symptom was SEEN in a report and noted as "cosmetic display artifact" instead of investigated — classic symptom-dismissal | Fixed: `view_layer.update()` after join + after `transform_apply`. Lesson: NEVER label an anomalous report value cosmetic without tracing it; in Blender-headless, every `dimensions/bound_box` read needs a fresh depsgraph |
| 3 | HIGH | `FOLIAGE_TOKENS` substring match (`'tree' ⊂ 'street'`) misclassifies structural geometry → silent geometry loss | Token list written for the happy path; substring vs token-membership never considered | Fixed: token-exact matching via the same tokenizer `dominant_token` uses |
| 4 | HIGH | `generate_rivendell_materials --force` would overwrite hand-made materials while the docstring promised the opposite | Docstring written as intent, never implemented; helper text repeated the lie | Fixed: `_generated_manifest.json` tracks script-authored names; `--force` only overwrites members; manifest seeded (183 generated / hand-made excluded). Lesson: a safety CLAIM in a docstring is a bug until enforced in code |
| 5 | HIGH | texture converter `--dry-run` still mkdir'd the live dst + wrote the report CSV | dry-run added late; only the obvious PNG writes were gated | Fixed: all writes gated. Lesson: dry-run must gate EVERY filesystem mutation, mkdir included |
| 6 | HIGH | folder-collision fallback stem never re-checked → two sets could silently overwrite each other's PNGs | Collision handling stopped at first fallback; no uniqueness assertion | Fixed: fallback loop with numeric suffix until unclaimed |
| 7 | HIGH | tent converter shared-part dedupe = last-folder-wins with no identity check | "subfolders duplicate these" assumption v2 — the assumption was fixed for scanning but reintroduced for dedupe | Fixed: first-wins + size-mismatch warning |
| 8 | HIGH | meshlist pollution: bld_/chunk outputs share `_meshlists` with the modular known-set the placement matchers trust (observed live: `library_book_00` matched as template) | The meshlist dir was designed for one producer; three more producers were added without revisiting the reader contract | Fixed: assembled outputs → `_meshlists_assembled`, matchers read `_meshlists` only. Lesson: adding a producer to a shared directory requires re-auditing every reader |
| 9 | HIGH | Sheet↔converter stem contract: collision-disambiguated texture stems are underivable by `tex_stem()` → missing material tpacs (latent; 0 live cases) | Two scripts independently re-derive stems instead of sharing a map | DEFERRED (recorded): converter should emit a stem-map sidecar the sheet consumes; revisit before the Wide-tents/city texture re-runs |
| 10 | MED | `MISSING:` sheet refs flowed into the generator and only degraded safely because `:` is illegal in Windows paths | Convention invented in one script, never handled in its consumer | Fixed: explicit `MISSING:` guard |
| 11 | MED | 5 divergent `sanitize()` copies (S_-prefix/case handling disagree between normalizer and matchers) | Copy-paste evolution under session pressure | DEFERRED: unify into one helper when the assembled path un-parks; inventoried in the review transcript |
| 12 | MED | tent `_clear` strip was case-sensitive (worked only because this kit's filenames are lowercase) | Regex written against observed filenames, not the format's case space | Fixed: IGNORECASE |
| 13 | MED | weld-below-target fix doesn't protect thin geometry inside a LARGE join (drapes melt when the aggregate crosses the cap) | The fix was scoped to the observed repro (small chunks), not the mechanism | DEFERRED + recorded as known limitation of building/reconstruct modes (assembled path parked anyway) |
| 14 | MED | UE export: vector-param None crash risk; success accounting without `isfile`; skip filters unanchored (`SM_god_ray_plane` matches `..._frame`) | Written fast against one kit's data | Fixed (all three) |
| 15 | MED | reconstruct: collision-less templates silently omitted from `bo_`, unreported; `god_ray` token missed `GodRay` | Summary designed around one failure mode (missing template) | Fixed: `templates_without_collision` in summary; `godray` token added |
| 16 | LOW | citysplit layout JSON written only at loop end (mid-run crash orphans placements) | — | Fixed: incremental flush |
| 17 | OBS | Normal maps: bilinear/LANCZOS resize without renormalization + no resolution-match to `_d`; alpha heuristic drops uniform semi-transparency; non-atomic PNG writes | — | DEFERRED, recorded here; none currently manifests (no resized normals in either kit run; translucents are manual materials; no interrupted run occurred) |

## Output audit (what shipped with bugs vs clean)

- **Modular Rivendell kit (458 FBX), textures (~660), tent kit, material tpacs**: unaffected by the above — verified via the earlier binding/name audits plus the fact that findings 1–3 live in citysplit (whose outputs were deleted) and 13 in parked modes.
- **`*_layout.json` files currently in `assembled/`**: the four assembly-level layouts predate fix #1's code path (their foliage arrays were produced by the SAME bug) — their `foliage` entries are origin-garbage and chunk `pos` values are (0,0,0)-suspect (finding #2's re-pivot no-op). These files are superseded whenever the assembled direction un-parks; do not build prefabs from them.
- **`t_rivendell_arch_starlight_mtl.tpac`** (user hand-made) is missing from disk as of this review — no tooling in this changeset deletes material files; most plausibly removed during an editor session. Surfaced to the user.

## Root-cause pattern

Three systemic themes across 17 findings:
1. **Duplicated derivations instead of shared contracts** (findings 6, 9, 11, 12): every script re-derives names; the pipeline's only integration mechanism is string agreement, and copies drift under iteration pressure. This is the tooling twin of the CombatMechanics parallel-builder seam lesson (`harness-facts.md` "Parallel builder briefs") — shared sub-problems need ONE pinned solution.
2. **Blender-headless evaluation staleness** (1, 2, and the earlier in-session bake bugs): `matrix_world`, `dimensions`, and `bound_box` are all lazily evaluated; every read after a mutating op needs an explicit `view_layer.update()`. Four separate incidents in two days.
3. **Stated-but-unenforced safety** (4, 5): docstring promises and flag semantics that code never implemented. Same class as `evidence-over-claims` §C — the claim was authored before/without the mechanism.

## Why the review structure caught these

Per-file review during the session caught none of findings 1/8/9 — they are cross-file contract bugs, found only by the dedicated cross-script agent and the pipeline-wide Blender agent (validating the deep-review doctrine that the data-flow agent is the highest-value pass). Finding 2 was VISIBLE in session output and explicitly dismissed as cosmetic — process failure, not coverage failure.

## Deferred items (recorded per "no silent deferrals")

Findings 9, 11, 13, 17 deferred with rationale above; all live in the parked assembled-scene path or in currently-unreachable branches, and are recorded here + in CHANGELOG. Revisit trigger: un-parking the assembled direction, or the Wide-tents/city texture re-run (for #9).
