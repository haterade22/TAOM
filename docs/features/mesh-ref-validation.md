# Mesh Reference Validation (item mesh / collision-body validator)

## Overview

A pure-stdlib, read-only tool (`tools/validate_mesh_refs.py`) that extracts every mesh and collision-body reference from Bannerlord item XML and checks whether each referenced asset actually exists, across three independent tiers of evidence. It exists to confirm or eliminate a specific hypothesis: that an item referencing a **missing `bo_` collision mesh** is the cause of intermittent infinite battle-load hangs. CLI / exit-code / report conventions mirror `tools/validate_moduledata.py`.

It is the pure-Python, body-aware, rgl-log-aware successor to `tools/Audit-MeshRefs.ps1` — that PowerShell script depends on the TpacTool C# DLLs and OMITS the collision-body attributes (`body_name` / `shield_body_name`), which are exactly the attributes the bo-mesh hypothesis is about.

## Why This Exists

When a Bannerlord item names an asset that the engine can't resolve, the failure mode varies: a missing **visual mesh** usually just makes the item invisible (or "underwear bug" cousin), but a missing **collision body** (`bo_*`) can stall physics/collision setup. The working hypothesis under investigation is that one such missing `bo_` body causes the engine to hang during battle load. Confirming or refuting that needs hard data:

- Which items reference which meshes/bodies, and at what `file:line`?
- Does each referenced visual mesh exist in a packaged `.tpac`?
- Does each referenced collision body exist (bodies are NOT in the `.tpac` table-of-contents — they're embedded in mesh metadata)?
- What does the **running engine** itself say it couldn't load?

No single check answers all of these, so the tool runs three tiers and an interpretation footer that states whether the hypothesis is supported or weakened by what was found.

## Architecture (the three tiers)

```
LOTRLOME_items/**/*.xml
   │  extract_refs()  (attribute-exact, comment-stripped, line-numbered)
   ▼
MeshRef[]  (name, attr, kind ∈ {visual_mesh, collision_body, prefab}, file, line, item_id, culture)
   │
   ├── Tier A  parse_rgl_log()    the running engine's own content warnings  (AUTHORITATIVE)
   ├── Tier B  build_present_set() Metamesh names from .tpac TOCs            (offline, visual)
   └── Tier C  bodies_present_in_tpacs() raw-byte scan for bo_ names         (offline, coarse)
   │  classify()
   ▼
Issue[]  ──▶ format_report()  (tiers run, counts, grouped missing, interpretation, exit code)
```

| Tier | What it observes | Codes it emits | Authority |
|------|------------------|----------------|-----------|
| **A** (needs an rgl_log) | The engine's `rgl_log[_errors]` content warnings from a real session | `RUNTIME_MISSING_BODY` (ERR if item-referenced, INFO if not), `RUNTIME_MISSING_MATERIAL` (WARN), `DUPLICATE_MESH_NAME` (WARN) | **Lead signal** — the only tier that sees the running engine |
| **B** (offline, visual meshes) | Union of Metamesh names across the `.tpac` tables-of-contents | `MISSING_MESH` (ERR), `UNVERIFIED_MESH` (WARN, degraded), `UNPARSED_TPAC` (WARN) | Reliable offline for *visual* meshes |
| **C** (offline, opt-in, coarse) | Raw `.tpac` bytes scanned for `bo_` names (bodies aren't in the TOC) | `MISSING_BODY` (ERR) | **Coarse** — a hit means the bytes exist, not that the engine can load it; confirm via Tier A |

Plus `PREFAB_REF` (INFO) — `prefab="X"` is recorded but is not a mesh.

**Key design decisions (mirrors `taom_schema.py` patterns):**
- **Engine importable separately from CLI.** `extract_refs_from_text`, `classify`, `scan_tpac_metameshes`, `bodies_present_in_tpacs`, `parse_rgl_text` are pure functions the unit tests call directly with synthetic data — no game install needed (the present-set is injectable, analogous to `Registries` in the ModuleData validator).
- **Attribute-exact extraction, not "ends-with."** The include set is the exact names `mesh`, `body_name`, `shield_body_name`, `holster_mesh`, `holster_mesh_with_weapon`, `flying_mesh` (derived by grepping the LOTRLOME_Armory + SandBoxCore item XML). This deliberately excludes the look-alike traps `mesh_maturity_type` (enum), `holster_mesh_length` (numeric), `recalculate_body` / `covers_body` (bool). Defensive support for `multi_mesh` / `<meshes>`/`<Mesh name>` is included (0× in the Armory today).
- **Unparsed `.tpac` degrades, never lies.** On parse drift / suspicious `udep_count`, `scan_tpac_metameshes` soft-fails *that one pack* (records `UNPARSED_TPAC`) instead of aborting the run; any visual mesh that would have been "missing" because of an unparsed pack is downgraded to `UNVERIFIED_MESH` (WARNING) — a false `MISSING_MESH` ERROR is never emitted from an unparsed pack.
- **Tier C is framed to avoid prefix false-positives.** Bodies are matched as length/NUL-framed tokens (the byte after the needle must not be a name-continuation byte), so `bo_helm` does not match the longer `bo_helm_a`.
- **Tier C reads each pack once.** `bodies_present_in_tpacs` scans every `.tpac` a single time for ALL body needles (streamed in 64 MB chunks with overlap) — O(total bytes), not O(bodies × bytes) — so it stays tractable against the ~150 Native packs (one is 2.4 GB).
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
```

**Interpreting the result:**
- **0 missing bodies + 0 missing visual meshes** → the bo-mesh hypothesis is *weakened*; look elsewhere (scene bodies, prefab refs, async asset load order, GPU-driver stalls per `feedback_bannerlord_async_load_check_gpu_first.md`).
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
| `tools/tests/test_validate_mesh_refs.py` | 30 unittest cases (pure stdlib, synthetic fixtures, injectable present-set) |
| `tools/tpac_skeleton_scan.py` | The proven `.tpac` TOC parser the Tier-B/`scan_tpac_metameshes` loop was lifted from |
| `tools/Audit-MeshRefs.ps1` | The C#-DLL-dependent predecessor (visual-mesh only, no collision bodies) — design precedent |

## Dependencies

**Python 3 standard library only** (`re`, `json`, `struct`, `argparse`, `dataclasses`, `enum`, `pathlib`) — no pip install. Reads (when present, all overridable via flags):
- Item XML: `E:\Steam\...\Modules\LOTRLOME_Armory\ModuleData\LOTRLOME_items\` (`--items`)
- `.tpac` present-set: `<game>\Modules\{LOTRLOME_Armory,Native,SandBoxCore}\AssetPackages\*.tpac` (`--game`, `--tpac-modules`) — shared meshes live outside the Armory, so the set is the union
- rgl logs: newest `rgl_log_errors_*.txt` under `C:\ProgramData\Mount and Blade II Bannerlord\logs` (`--rgl-log` / `--no-rgl-log`)

Missing paths degrade gracefully (report the skip, run what you can), per `.claude/rules/environment-failures.md`.

## Tests

```bash
python -m unittest discover -s tools/tests -p "test_*.py"     # full tools suite
python -m unittest tools.tests.test_validate_mesh_refs        # this tool only
```

30 cases covering: extraction of all 6 attrs with correct kind + line numbers; exclusion of the 3 false-positive attrs + `prefab` → `PREFAB_REF`; defensive `multi_mesh`/`<Mesh>`; comment-stripping; malformed-XML tolerance; Tier B present-vs-missing + `.lodN` base-name matching; unparsed-tpac → `UNVERIFIED_MESH` + `UNPARSED_TPAC` (no false ERROR); Tier C body byte-scan present/absent + prefix-no-false-match; Tier A `get_object failed` → `RUNTIME_MISSING_BODY` (ERROR when item-referenced, INFO when a scene body) + missing-material WARN + duplicate-line WARN; a **hand-built minimal TOC byte buffer** (magic `0x43415054`, version 2, one Metamesh item) proving the Tier-B struct offsets, plus its non-Metamesh / drifted-`udep_count` / non-magic soft-fail variants; CLI exit codes (0 / 1 / 2), `--code` filter, `--warnings-as-errors`; culture grouping in the report.

## Known Scope (intentional)

- **Tier C is coarse by nature.** Collision bodies aren't in the `.tpac` TOC, so the raw-byte scan can only prove a name's *bytes* are present, not that the engine can instantiate the body. Always confirm a `MISSING_BODY` against an rgl_log.
- **Auto-discovered rgl_log may be empty.** "Newest" is by mtime; a freshly-launched session writes an essentially-empty error log. Pass `--rgl-log` explicitly to target a log with real warnings.
- **Item XML only.** Scene-body / scene-mesh refs in `.sco`/scene XML are out of scope (Tier A still *reports* engine warnings about them, classified INFO). Material/texture refs, weapon-craft piece meshes, and skin/race body-meta meshes (`hands_mesh`, `legs_mesh`, etc. — present in race XML, not item XML) are not extracted.
- **No MCP tool / no pre-commit hook (yet).** This is a diagnostic run on demand, not a commit gate. An MCP tool (`mesh_ref_check`) over the same engine could be added later alongside the `taom-moduledata` server if interactive querying proves useful; it was deliberately left out of v1 to keep scope tight.
