# Mesh Reference Validation (item mesh / collision-body validator)

## Overview

A pure-stdlib, read-only tool (`tools/validate_mesh_refs.py`) that extracts every mesh and collision-body reference from Bannerlord item XML and checks whether each referenced asset actually exists, across three independent tiers of evidence. CLI / exit-code / report conventions mirror `tools/validate_moduledata.py`.

It was built to confirm or eliminate a hypothesis: that a **missing `bo_` collision body** causes intermittent infinite battle-load hangs. **The hypothesis is CONFIRMED** (#352, 2026-07-16) — a user field report traced a permanent siege-load hang via ClrMD to `PhysicsShape.GetFromResource` ← `PreloadHelper.WaitForMeshesToBeLoaded`, which polls every registered body name and only exits once each resolves. One unresolvable name spins the main thread forever: no crash, no error log, one CPU core at 100%. The cause was two `body_name` typos in LOTRLOME_Armory v2.0.8 — the assets shipped fine, the refs were a suffix off.

It is the pure-Python, body-aware, rgl-log-aware successor to `tools/Audit-MeshRefs.ps1` — that PowerShell script depends on the TpacTool C# DLLs and OMITS the collision-body attributes (`body_name` / `shield_body_name`), which are exactly the attributes the load-hang class is about. **This tool owns body validation; don't add body checks to the PS1.** (A 2026-07-16 session started doing exactly that, misled by a false-negative grep, before catching the overlap.)

## Why This Exists

When a Bannerlord item names an asset that the engine can't resolve, the failure mode varies: a missing **visual mesh** usually just makes the item invisible (or "underwear bug" cousin), but a missing **collision body** (`bo_*`) hangs mission load outright — see Overview. Catching that needs hard data:

> **The scope lesson (#352).** This tool caught both live typos at the exact line — but only once pointed at the right directory. Its `--items` default was `ModuleData/LOTRLOME_items/`, while crafting pieces live in `ModuleData/LOTRLOME_crafting_pieces.xml`, one level up. For a year the tool built to catch this class never read the file that contained it, and reported PASS. **A clean run means "clean within `--items` scope", never "the hang isn't a missing body".** The default is now `ModuleData/`; widen it further for any module whose body-naming XML lives elsewhere.

> **The inverse trap (2026-08-03).** #352 was a good ref-vs-asset pair broken on the *ref* side, which trains the reflex "malformed name ⇒ fix the name." It runs the other way too. `wm_isengard_shield_a04` references `bo_capwm_isengard_shield_a02_clean` — underscore missing, unlike all 224 sibling shields — and the asset is packaged under **that exact misspelling**, while the corrected spelling exists in no `.tpac`. Correcting it would manufacture the #352 hang. **A PASS on a name that looks wrong is positive evidence the name is right, not a tool gap.** Only names this tool flags `MISSING_BODY` are safe to rewrite; query `build_present_set(...).physicsshapes` for both spellings before touching either half of the pair. Full case: [armory-shield-audit.md](../reference/armory-shield-audit.md).

- Which items reference which meshes/bodies, and at what `file:line`?
- Does each referenced visual mesh exist in a packaged `.tpac`?
- Does each referenced collision body exist (as a `PhysicsShape` entry in a `.tpac` table-of-contents)?
- What does the **running engine** itself say it couldn't load?

No single check answers all of these, so the tool runs three tiers and an interpretation footer that reads the findings back in plain language.

## Architecture (the three tiers)

```
ModuleData/**/*.xml   (items AND crafting pieces — see the scope lesson above)
   │  extract_refs()  (attribute-exact, comment-stripped, line-numbered)
   ▼
MeshRef[]  (name, attr, kind ∈ {visual_mesh, collision_body, prefab}, file, line, item_id, culture)
   │
   ├── Tier A  parse_rgl_log()    the running engine's own content warnings  (AUTHORITATIVE)
   ├── Tier B  build_present_set() Metamesh names from .tpac TOCs            (offline, visual)
   └── Tier C  build_present_set() PhysicsShape names from .tpac TOCs        (offline, EXACT)
               └─ falls back to bodies_present_in_tpacs() raw-byte scan only
                  for packs that soft-failed to parse                        (coarse)
   │  classify()
   ▼
Issue[]  ──▶ format_report()  (tiers run, counts, grouped missing, interpretation, exit code)
```

| Tier | What it observes | Codes it emits | Authority |
|------|------------------|----------------|-----------|
| **A** (needs an rgl_log) | The engine's `rgl_log[_errors]` content warnings from a real session | `RUNTIME_MISSING_BODY` (ERR if item-referenced, INFO if not), `RUNTIME_MISSING_MATERIAL` (WARN), `DUPLICATE_MESH_NAME` (WARN) | **Lead signal** — the only tier that sees the running engine |
| **B** (offline, visual meshes) | Union of Metamesh names across the `.tpac` tables-of-contents | `MISSING_MESH` (ERR), `UNVERIFIED_MESH` (WARN, degraded), `UNPARSED_TPAC` (WARN) | Reliable offline for *visual* meshes |
| **C** (offline, opt-in, exact) | Union of `PhysicsShape` names across the `.tpac` tables-of-contents | `MISSING_BODY` (ERR) | Reliable offline for *collision bodies*; falls back to the coarse byte scan only for packs that soft-failed to parse |

Plus `PREFAB_REF` (INFO) — `prefab="X"` is recorded but is not a mesh.

### The reverse audit (`--unreferenced`, added 2026-08-04)

All three tiers ask the same direction: *does every **referenced** mesh exist?* That direction
cannot answer the complaint "this armour shipped but it isn't in the game", which is the
opposite question: *does every **packaged** mesh have an item?* `--unreferenced` runs that pass
over the Tier B present-set and emits `UNREFERENCED_MESH` (INFO — the exit code is unaffected,
since unreferenced art is an audit signal, not a failure).

- `--prefix <p>` filters the **candidate** (packaged) side only, e.g. `--prefix sk_gd_` for a
  per-culture audit. The referenced set is never filtered, so narrowing to one culture can
  never invent an orphan that another culture's item actually references.
- **`_slim` suppression is mandatory, not cosmetic.** The engine resolves the female body
  variant by appending `_slim` to the mesh name, so those meshes are never named in item XML.
  Without the suppression the Gondor set alone reports 100 false positives.
- Matching is **case-insensitive** here, unlike Tier B's exact lookup. That is deliberate and
  conservative in this direction: casing drift makes a mesh look *referenced* (dropped from the
  report) rather than manufacturing a false orphan.
- **Scope matters more than in the forward tiers.** At the default `--tpac-modules`, vanilla's
  entire scene/prop/weapon mesh library counts as "unreferenced" against mod item XML — 12,483
  hits, useless. Pass `--tpac-modules LOTRLOME_Armory` for a modded-art audit; the report prints
  a NOTE when the candidate side is that wide and no prefix is set.

First run (2026-08-04): Gondor **0 orphans of 418 packaged meshes**; armory-wide **549**, of
which 276 are `clo_*` cloth variants (201 with an item-referenced base) and 69 are `sm_*`
crafting pieces including the deliberately-removed Gondor poleaxe. Full write-up:
[`audit-gondor-armory-2026-08-04.md`](../reviews/audit-gondor-armory-2026-08-04.md).

**Key design decisions (mirrors `taom_schema.py` patterns):**
- **Engine importable separately from CLI.** `extract_refs_from_text`, `classify`, `scan_tpac_metameshes`, `bodies_present_in_tpacs`, `parse_rgl_text` are pure functions the unit tests call directly with synthetic data — no game install needed (the present-set is injectable, analogous to `Registries` in the ModuleData validator).
- **Attribute-exact extraction, not "ends-with."** The include set is the exact names `mesh`, `body_name`, `shield_body_name`, `holster_mesh`, `holster_mesh_with_weapon`, `flying_mesh` (derived by grepping the LOTRLOME_Armory + SandBoxCore item XML). This deliberately excludes the look-alike traps `mesh_maturity_type` (enum), `holster_mesh_length` (numeric), `recalculate_body` / `covers_body` (bool). Defensive support for `multi_mesh` / `<meshes>`/`<Mesh name>` is included (0× in the Armory today).
- **Unparsed `.tpac` degrades, never lies.** On parse drift / suspicious `udep_count`, `scan_tpac_metameshes` soft-fails *that one pack* (records `UNPARSED_TPAC`) instead of aborting the run; any visual mesh that would have been "missing" because of an unparsed pack is downgraded to `UNVERIFIED_MESH` (WARNING) — a false `MISSING_MESH` ERROR is never emitted from an unparsed pack.
- **Tier C matches the TOC exactly (corrected in #352).** Collision bodies ARE first-class `.tpac` TOC items (`PhysicsShape`, TYPE_GUID `e8528e0e-64b6-4e61-bae0-7569c0452aea` — `pack1.tpac` exposes 382). The tool originally asserted the opposite ("bodies aren't in the TOC; they live embedded in mesh metadata") and byte-scanned instead, which is why Tier C carried a "coarse, confirm via rgl_log" caveat for a year. The `PhysicsShape` count is confirmed independently two ways: this tool's hand-rolled GUID parse, and TpacTool's own `PhysicsShape.TYPE_GUID` via reflection. Exact-set matching also makes the suffix-typo case (`..._2h` vs the shipped `..._2h_a`) impossible to pass by accident.
- **The raw-byte scan survives as the degraded fallback.** `bodies_present_in_tpacs` is still used when a pack soft-fails to parse (or Tier B is skipped), so an unreadable pack never produces a false `MISSING_BODY` — same philosophy as `UNVERIFIED_MESH`. It is framed to avoid prefix false-positives (length/NUL-framed tokens, so `bo_helm` does not match `bo_helm_a`) and reads each pack once for ALL needles (64 MB chunks with overlap) — O(total bytes), tractable against the ~150 Native packs (one is 2.4 GB).
- **Cross-reference is the proof.** A `get_object failed for body: X` line is reported as an ERROR only when a scanned item references `X`; otherwise it's INFO ("likely a scene body"). The live `bo_gondor_brick_rubble_c` warning is a scene body no item references — the tool correctly classifies it INFO, proving the cross-reference works end to end.

## How to run + interpret

```bash
# Offline default — Tier B always; Tier C auto-on when no rgl_log is available.
python tools/validate_mesh_refs.py --scan-bodies

# Authoritative — point at a real session's error log (Tier A). Auto-discovery
# picks the NEWEST rgl_log_errors_*.txt, which may be an empty fresh session;
# pass --rgl-log explicitly to target a log that actually contains warnings.
python tools/validate_mesh_refs.py --rgl-log "C:\ProgramData\Mount and Blade II Bannerlord\logs\rgl_log_errors_77136.txt"

# Machine-readable + filter to one code + treat warnings as errors
python tools/validate_mesh_refs.py --scan-bodies --json report.json --code MISSING_BODY --warnings-as-errors

# On a machine without the asset packages (Tier B/C skip; ref extraction + Tier A still run)
python tools/validate_mesh_refs.py --no-tier-b

# Reverse audit — packaged meshes that NO item references. Narrow the candidate
# side, or vanilla's whole mesh library drowns the result.
python tools/validate_mesh_refs.py --unreferenced --prefix sk_gd_
python tools/validate_mesh_refs.py --unreferenced --tpac-modules LOTRLOME_Armory --code UNREFERENCED_MESH
```

**Interpreting the result:**
- **0 missing bodies + 0 missing visual meshes** → nothing *in the scanned scope* can cause the hang. Confirm `--items` actually covers every XML that names a body (the #352 trap) before concluding; then look elsewhere (scene bodies, prefab refs, async asset load order, GPU-driver stalls per `feedback_bannerlord_async_load_check_gpu_first.md`).
- **≥1 missing body** → that is a confirmed hang cause, not a suspect. Check `bodies-present` for a near-match first: both real cases were suffix typos with the correct body shipped one character away, so the fix is the ref, not deleting the item.
- **N > 0 missing** → those items are *prime suspects*; cross-reference the referencing item ids against troop rosters (`tools/validate_all_troop_refs.py`) to find which troops spawn them, then reproduce a battle with those troops.
- **Tier A vs Tier B/C can disagree** legitimately: Tier A only sees assets the engine actually touched that session, so an offline `MISSING_BODY` for an item that wasn't spawned won't appear in Tier A. The offline finding is still a real candidate — exercise that item in-game and re-check Tier A to confirm.

Exit code (mirrors `validate_moduledata.py`): `1` if any ERROR (or any WARNING with `--warnings-as-errors`), `2` if an input path is bad, else `0`.

### First real run (2026-06-01, offline `--scan-bodies`)

3570 refs extracted (3084 visual / 486 collision-body / 0 prefab). Tier B: 2944/2946 unique visual meshes present across 159 `.tpac` (0 unparsed). Findings:

| Code | Asset | Item | Location |
|------|-------|------|----------|
| `MISSING_BODY` | `bo_cap_wm_boromir_shield` | `wm_boromir_shield` | `LOTRAOM_shields.xml:1161` |
| `MISSING_MESH` | `sk_dg_uruk_pauldron_med_c` | `sk_dg_uruk_pauldron_med_c` | `dol_guldur/shoulder_armors.xml:333` |
| `MISSING_MESH` | `ar_ardunian_elite_hand` | `ar_ardunian_elite_hand` | `mordor/arm_armors.xml:518` |

Tier A against `rgl_log_errors_77136.txt`: 1 INFO (`bo_gondor_brick_rubble_c`, a scene body — unreferenced by items), 28 `RUNTIME_MISSING_MATERIAL` (all scene meshes), 3 `DUPLICATE_MESH_NAME`. No item-referenced runtime-missing body in that session's log.

## Key Files

| File | Purpose |
|------|---------|
| `tools/validate_mesh_refs.py` | Engine (extract / present-set / rgl parse / classify / report) + CLI front-end |
| `tools/tests/test_validate_mesh_refs.py` | 46 unittest cases (pure stdlib, synthetic fixtures, injectable present-set) |
| `tools/tpac_skeleton_scan.py` | The proven `.tpac` TOC parser the Tier-B/`scan_tpac_metameshes` loop was lifted from |
| `tools/Audit-MeshRefs.ps1` | The C#-DLL-dependent predecessor (visual-mesh only, no collision bodies) — design precedent |

## Dependencies

**Python 3 standard library only** (`re`, `json`, `struct`, `argparse`, `dataclasses`, `enum`, `pathlib`) — no pip install. Reads (when present, all overridable via flags):
- Item + crafting-piece XML: `E:\Steam\...\Modules\LOTRLOME_Armory\ModuleData\` (`--items`; covers `LOTRLOME_items/**` AND the `ModuleData/`-root `LOTRLOME_crafting_pieces.xml`)
- `.tpac` present-set: `<game>\Modules\{LOTRLOME_Armory,Native,SandBoxCore}\AssetPackages\*.tpac` (`--game`, `--tpac-modules`) — shared meshes live outside the Armory, so the set is the union
- rgl logs: newest `rgl_log_errors_*.txt` under `C:\ProgramData\Mount and Blade II Bannerlord\logs` (`--rgl-log` / `--no-rgl-log`)

Missing paths degrade gracefully (report the skip, run what you can), per `.claude/rules/environment-failures.md`.

## Tests

```bash
python -m unittest discover -s tools/tests -p "test_*.py"     # full tools suite
python -m unittest tools.tests.test_validate_mesh_refs        # this tool only
```

46 cases covering: extraction of all 6 attrs with correct kind + line numbers; exclusion of the 3 false-positive attrs + `prefab` → `PREFAB_REF`; defensive `multi_mesh`/`<Mesh>`; comment-stripping; malformed-XML tolerance; Tier B present-vs-missing + `.lodN` base-name matching; unparsed-tpac → `UNVERIFIED_MESH` + `UNPARSED_TPAC` (no false ERROR); Tier C body byte-scan present/absent + prefix-no-false-match; Tier A `get_object failed` → `RUNTIME_MISSING_BODY` (ERROR when item-referenced, INFO when a scene body) + missing-material WARN + duplicate-line WARN; a **hand-built minimal TOC byte buffer** (magic `0x43415054`, version 2, one Metamesh item) proving the Tier-B struct offsets, plus its non-Metamesh / drifted-`udep_count` / non-magic soft-fail variants; CLI exit codes (0 / 1 / 2), `--code` filter, `--warnings-as-errors`; culture grouping in the report; and the reverse audit — orphan reported, referenced not reported, `_slim` suppressed only when its base is referenced, `--prefix` filtering candidates but never the referenced set, `.lodN` collapse, case-drift counting as referenced rather than as a false orphan, `UNREFERENCED_MESH` being INFO, the report block rendering, the report staying byte-identical when the mode is off, and both CLI degradations (`--prefix` without `--unreferenced`, `--unreferenced` without Tier B).

## Known Scope (intentional)

- **Tier C is coarse by nature.** Collision bodies aren't in the `.tpac` TOC, so the raw-byte scan can only prove a name's *bytes* are present, not that the engine can instantiate the body. Always confirm a `MISSING_BODY` against an rgl_log.
- **Auto-discovered rgl_log may be empty.** "Newest" is by mtime; a freshly-launched session writes an essentially-empty error log. Pass `--rgl-log` explicitly to target a log with real warnings.
- **Item XML only.** Scene-body / scene-mesh refs in `.sco`/scene XML are out of scope (Tier A still *reports* engine warnings about them, classified INFO). Material/texture refs, weapon-craft piece meshes, and skin/race body-meta meshes (`hands_mesh`, `legs_mesh`, etc. — present in race XML, not item XML) are not extracted.
- **No MCP tool / no pre-commit hook (yet).** This is a diagnostic run on demand, not a commit gate. An MCP tool (`mesh_ref_check`) over the same engine could be added later alongside the `taom-moduledata` server if interactive querying proves useful; it was deliberately left out of v1 to keep scope tight.

## Changelog

- 2026-08-04 — **Added the reverse audit (`--unreferenced` / `--prefix`).** All three tiers only ever asked "does every referenced mesh exist?", so the tool could not answer a "this armour shipped but isn't in the game" report, which asks the opposite. The new pass reuses the Tier B present-set and the same ref extraction, emits `UNREFERENCED_MESH` (INFO, exit code unaffected), and suppresses `_slim` female variants whose base mesh is referenced — without that suppression the Gondor set alone reports 100 false positives. Prompted by a player report about missing Gondor helmets, which the audit falsified: 0 orphans of 418 packaged Gondor meshes, the cause being marketplace reachability instead ([audit](../reviews/audit-gondor-armory-2026-08-04.md)). Two stale `30`s in this doc corrected to the real count. +13 tests (33 → 46).
- 2026-07-16 (#352) — **Hypothesis CONFIRMED, and the tool's two blind spots fixed.** A user field report tied a permanent siege-load hang to a missing `bo_` body, so this is now a demonstrated cause rather than a suspect. (1) **Scope:** `--items` defaulted to `ModuleData/LOTRLOME_items/`, but the two live typos sat in `ModuleData/LOTRLOME_crafting_pieces.xml` — the tool never read the file containing the bug it was built for. Default widened to `ModuleData/`. (2) **Tier C:** was a coarse byte-scan because the code asserted bodies "are NOT in the .tpac TOC"; they are (`PhysicsShape`, 382 in `pack1.tpac`, confirmed independently against TpacTool's `PhysicsShape.TYPE_GUID`). Tier C now matches the exact TOC set, byte-scanning only as a fallback for unparsable packs. The clean-run footer no longer claims the hypothesis is "WEAKENED" — it can only ever support "clean within `--items` scope". +3 tests (30 → 33).
- 2026-06-01 — Added `tools/validate_mesh_refs.py`, a pure-stdlib three-tier mesh / collision-body existence validator (Tier A authoritative `rgl_log` cross-ref, Tier B offline `.tpac` TOC for visual meshes, Tier C coarse `bo_` byte-scan); first run found 3 real Armory data bugs and 30 new tests were added.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/ai-includes/weapon-creation-workflow.md](../ai-includes/weapon-creation-workflow.md)
- [docs/features/battle-load-diagnostics.md](./battle-load-diagnostics.md)
- [docs/INDEX.md](../INDEX.md)
- [docs/reference/doc-lookup.md](../reference/doc-lookup.md)

<!-- backlinks-end -->
