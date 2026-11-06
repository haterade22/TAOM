---
paths:
  - "**/settlements.xml"
  - "**/sp_battle_scenes.xml"
  - "**/spcultures.xml"
  - "**/taom_spcultures.xml"
  - "**/spclans.xml"
  - "**/spkingdoms.xml"
  - "**/action_sets.xml"
  - "**/*.xslt"
  - "**/GUI/PreFabs/**/*.xml"
  - "**/GUI/Prefabs/**/*.xml"
---

# Compare Against Vanilla Before Modifying Mirrored Data

TAOM ships many XML files that **mirror, extend, or transform vanilla Bannerlord data** (`settlements.xml`, `sp_battle_scenes.xml`, `spcultures`/`spclans`/`spkingdoms`, `action_sets.xml`, the `*.xslt` transforms, party templates, equipment rosters). Vanilla renames, removes, and re-schemas this data between versions. A TAOM reference that was valid in an older version goes **silently stale** after a version bump and crashes the game when that data path is exercised — often far from where the stale value lives (e.g. a battle near a specific map cell, entering a specific town).

**Rule:** before authoring or relying on any value that mirrors/references vanilla, diff it against the **currently installed** vanilla version. Don't trust that "it worked before" — the previous version may have had a name that no longer exists.

**Three failure shapes the stale-name case doesn't cover**, all invisible to a per-value check: the data can be structurally illegal in a way one engine build tolerates and another refuses to boot on (see [armory-guide.md](../../docs/reference/armory-guide.md) "action_sets structure"), it can be perfectly well-formed yet unreachable relative to the rest of the data (see [dev-console.md](../../docs/features/dev-console.md) `taom.audit_settlement_entrances`), and it can be complete and self-consistent yet orphaned relative to a *different module's* file (see [lord-spawn-guard.md](../../docs/features/lord-spawn-guard.md)). The first two were found on 2026-08-03, the third on 2026-08-04.

## What to check, and how

| You're touching… | Compare against | Tool |
|---|---|---|
| `settlements.xml` scene_name refs | on-disk `Modules/*/SceneObj/` folders | `python tools/audit_scene_names.py` |
| `sp_battle_scenes.xml` Scene ids / map_indices | vanilla `SandBox`/`NavalDLC` `sp_battle_scenes.xml` + SceneObj | `python tools/audit_battle_scenes.py` |
| settlement entrance coordinates (`posX/posY`, `gate_posX/gate_posY`) | the navmesh island every OTHER settlement sits on: set-relative, not vanilla | `taom.audit_settlement_entrances` (needs a loaded campaign); see [dev-console.md](../../docs/features/dev-console.md) `taom.audit_settlement_entrances` |
| `action_sets.xml`: structure as much as content | vanilla `Native/ModuleData/action_sets.xml` (the engine field-merges same-id sets across modules) | `python tools/audit_action_set_parity.py`: **exits non-zero** on any root-level `<action>`; see [armory-guide.md](../../docs/reference/armory-guide.md) "action_sets structure" |
| a culture / kingdom / clan you authored in `Main/_Module/ModuleData/` | whether any settlement in `TAOM_Map` actually carries that culture | `python tools/validate_moduledata.py`: `LANDLESS_CULTURE` (ERROR); see [lord-spawn-guard.md](../../docs/features/lord-spawn-guard.md) |
| any `scene_name=` that no longer resolves | — | `python tools/remap_stale_scene_names.py --dry-run` |
| TaleWorlds API signatures | installed DLLs | `pwsh tools/taom-src.ps1 path <Type>` (NOT the decompiled dump) |
| culture/clan/kingdom IDs | vanilla `SandBoxCore` XML | grep + `xml-data.md` ID table |
| XSLT passthrough attributes | vanilla source the XSLT transforms | `/xslt-check` |
| a culture's party-template bindings (`spcultures.xslt` or `taom_spcultures.xml`) | what the block EMITS vs what vanilla supplies for everything it does not name | `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter FullyQualifiedName~CulturePartyTemplate`; see [xslt.md](xslt.md) "Passthrough cuts both ways" |
| GUI prefab **clones** (full `<Prefab>` copies of a vanilla prefab) | vanilla `Modules/{SandBox,SandBoxCore,Native}/GUI/Prefabs/<same-name>.xml` | `diff -w --strip-trailing-cr <vanilla> <taom>`; see [gui-prefab-clones.md](../../docs/reference/gui-prefab-clones.md) |

**Matching is case-insensitive.** Windows resolves `HART_ISENGARD` vs `HART_isengard`; an exact-case check produces false positives. The scene audit tools already lower-case both sides.

## When this fires

- **After ANY Bannerlord version bump** — run `audit_scene_names.py` + `audit_battle_scenes.py` as part of the post-bump validation (see `docs/migration/v1.4.x-changes.md`). v1.4.5 renamed the house-interior scenes and TAOM's `sp_battle_scenes.xml` referenced a non-existent `battle_terrain_extended`; both crashed battles/visits until repointed (2026-05-28).
- **When editing any of the `paths:` files above** — re-run the relevant audit before committing.
- **Before editing, or after a version bump touching, any TAOM GUI prefab that is a CLONE of a vanilla prefab**: diff it against installed vanilla first (see [gui-prefab-clones.md](../../docs/reference/gui-prefab-clones.md)). v1.4.5 silently broke every troop thumbnail this way (2026-05-31).
- **Before committing any `action_sets.xml` edit** — run `audit_action_set_parity.py`. It now fails on a structural defect the client build loads without complaint and the dedicated-server build dies on (2026-08-03).
- **When adding a settlement, or moving one's `posX/posY` or `gate_posX/gate_posY`** — a coordinate can be on-mesh and still unreachable. Only an island comparison finds that, and only in a loaded campaign.
- **After authoring or re-theming a culture, kingdom or clan** — run `python tools/validate_moduledata.py` and read any `LANDLESS_CULTURE` error. A culture whose own files are all valid can still own nothing, because the settlements that would carry it live in a different module (2026-08-04, Khand).
- **When diagnosing a "crash near a specific place" report**: it is almost always a stale data reference (scene, item, troop, culture), not an engine-internals bug. Audit the references first. The non-crashing variant of the same report (**AI parties wedging near a specific place, no log, no crash**) is the navmesh-island case in [dev-console.md](../../docs/features/dev-console.md).

## Why this rule exists

A version bump renamed scenes and attributes, and every stale reference crashed or silently mis-rendered: [scene-reference-audit.md](../../docs/reference/scene-reference-audit.md), `rca-party-troop-thumbnail-stale-prefab-clone-2026-05-31.md`. This is the data-side counterpart of CLAUDE.md "Research First".
