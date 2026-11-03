---
paths:
  - "**/settlements.xml"
  - "**/sp_battle_scenes.xml"
  - "**/taom_spcultures.xml"
  - "**/*.xslt"
  - "docs/reference/lotrlome-armory-snapshot/action_sets.xml"
---

# Compare Against Vanilla Before Modifying Mirrored Data

TAOM ships many XML files that **mirror, extend, or transform vanilla Bannerlord data** (`settlements.xml`, `sp_battle_scenes.xml`, `spcultures`/`spclans`/`spkingdoms`, `action_sets.xml`, the `*.xslt` transforms, party templates, equipment rosters). Vanilla renames, removes, and re-schemas this data between versions. A TAOM reference that was valid in an older version goes **silently stale** after a version bump and crashes the game when that data path is exercised — often far from where the stale value lives (e.g. a battle near a specific map cell, entering a specific town).

**Rule:** before authoring or relying on any value that mirrors/references vanilla, diff it against the **currently installed** vanilla version. Don't trust that "it worked before" — the previous version may have had a name that no longer exists.

**Three failure shapes the stale-name case doesn't cover**, all invisible to a per-value check: the data can be structurally illegal in a way one engine build tolerates and another refuses to boot on (`action_sets.xml`, [armory-guide.md](../../docs/reference/armory-guide.md) "action_sets structure"), it can be perfectly well-formed yet unreachable relative to the rest of the data (a settlement entrance on a navmesh island, `taom.audit_settlement_entrances`, [dev-console.md](../../docs/features/dev-console.md)), and it can be complete and self-consistent yet orphaned relative to a *different module's* file ([lord-spawn-guard.md](../../docs/features/lord-spawn-guard.md), `LANDLESS_CULTURE`).

When you author a culture, faction or kingdom in one module, ask which other module's file must agree with it.

## What to check, and how

| You're touching… | Compare against | Tool |
|---|---|---|
| `settlements.xml` scene_name refs | on-disk `Modules/*/SceneObj/` folders | `python tools/audit_scene_names.py` |
| `sp_battle_scenes.xml` Scene ids / map_indices | vanilla `SandBox` `sp_battle_scenes.xml` + SceneObj; **generated**: edit `REGIONS` in `tools/build_battle_scenes.py`, never the XML | `python tools/build_battle_scenes.py --check` |
| settlement entrance coordinates (`posX/posY`, `gate_posX/gate_posY`) | the navmesh island every OTHER settlement sits on: set-relative, not vanilla | `taom.audit_settlement_entrances` (needs a loaded campaign); see [dev-console.md](../../docs/features/dev-console.md) `taom.audit_settlement_entrances` |
| `action_sets.xml`: structure as much as content | vanilla `Native/ModuleData/action_sets.xml` (the engine field-merges same-id sets across modules) | `python tools/audit_action_set_parity.py`: **exits non-zero** on any root-level `<action>`; see [armory-guide.md](../../docs/reference/armory-guide.md) "action_sets structure" |
| a culture / kingdom / clan you authored in `Main/_Module/ModuleData/` | whether any settlement in `TAOM_Map` actually carries that culture | `python tools/validate_moduledata.py`: `LANDLESS_CULTURE` (ERROR); see [lord-spawn-guard.md](../../docs/features/lord-spawn-guard.md) |
| any `scene_name=` that no longer resolves | — | `python tools/remap_stale_scene_names.py --dry-run` |
| TaleWorlds API signatures | installed DLLs | `pwsh tools/taom-src.ps1 path <Type>` (NOT the decompiled dump) |
| culture/clan/kingdom IDs | vanilla `SandBoxCore` XML | grep + `xml-data.md` ID table |
| XSLT passthrough attributes | vanilla source the XSLT transforms (`xslt.md` file map) | transform and diff, `feedback_xslt_passthrough_unintended_inheritance.md` |
| a culture's party-template bindings (`spcultures.xslt` or `taom_spcultures.xml`) | what the block EMITS vs what vanilla supplies for everything it does not name | `dotnet test TAOM.Tests --filter FullyQualifiedName~CulturePartyTemplate`; see [xslt.md](xslt.md) "Passthrough cuts both ways" |
| GUI prefab **clones** (full `<Prefab>` copies of a vanilla prefab) | vanilla `Modules/{SandBox,SandBoxCore,Native}/GUI/Prefabs/<same-name>.xml` | `diff -w --strip-trailing-cr <vanilla> <taom>`; see [gui-prefab-clones.md](../../docs/reference/gui-prefab-clones.md) |

**Matching is case-insensitive.** Windows resolves `HART_ISENGARD` vs `HART_isengard`; an exact-case check produces false positives. The scene audit tools already lower-case both sides.

## When this fires

- **When diagnosing a "crash near a specific place" report:** it is almost always a stale data reference (scene, item, troop, culture), not an engine-internals bug. Audit the references first. The non-crashing variant of the same report, AI parties wedging near a specific place with no log and no crash, is the navmesh-island case (`taom.audit_settlement_entrances`, [dev-console.md](../../docs/features/dev-console.md)).
- **After a Bannerlord version bump:** `/engine-bump` runs these audits.
