---
paths:
  - "**/settlements.xml"
  - "**/sp_battle_scenes.xml"
  - "**/spcultures.xml"
  - "**/taom_spcultures.xml"
  - "**/spclans.xml"
  - "**/spkingdoms.xml"
  - "**/*.xslt"
---

# Compare Against Vanilla Before Modifying Mirrored Data

TAOM ships many XML files that **mirror, extend, or transform vanilla Bannerlord data** (`settlements.xml`, `sp_battle_scenes.xml`, `spcultures`/`spclans`/`spkingdoms`, the `*.xslt` transforms, party templates, equipment rosters). Vanilla renames, removes, and re-schemas this data between versions. A TAOM reference that was valid in an older version goes **silently stale** after a version bump and crashes the game when that data path is exercised — often far from where the stale value lives (e.g. a battle near a specific map cell, entering a specific town).

**Rule:** before authoring or relying on any value that mirrors/references vanilla, diff it against the **currently installed** vanilla version. Don't trust that "it worked before" — the previous version may have had a name that no longer exists.

## What to check, and how

| You're touching… | Compare against | Tool |
|---|---|---|
| `settlements.xml` scene_name refs | on-disk `Modules/*/SceneObj/` folders | `python tools/audit_scene_names.py` |
| `sp_battle_scenes.xml` Scene ids / map_indices | vanilla `SandBox`/`NavalDLC` `sp_battle_scenes.xml` + SceneObj | `python tools/audit_battle_scenes.py` |
| any `scene_name=` that no longer resolves | — | `python tools/remap_stale_scene_names.py --dry-run` |
| TaleWorlds API signatures | installed DLLs | `pwsh tools/taom-src.ps1 path <Type>` (NOT the decompiled dump) |
| culture/clan/kingdom IDs | vanilla `SandBoxCore` XML | grep + `xml-data.md` ID table |
| XSLT passthrough attributes | vanilla source the XSLT transforms | `/xslt-check`, `feedback_xslt_passthrough_unintended_inheritance.md` |

**Matching is case-insensitive.** Windows resolves `HART_ISENGARD` vs `HART_isengard`; an exact-case check produces false positives. The scene audit tools already lower-case both sides.

## When this fires

- **After ANY Bannerlord version bump** — run `audit_scene_names.py` + `audit_battle_scenes.py` as part of the post-bump validation (see `docs/migration/v1.4.x-changes.md`). v1.4.5 renamed the house-interior scenes and TAOM's `sp_battle_scenes.xml` referenced a non-existent `battle_terrain_extended`; both crashed battles/visits until repointed (2026-05-28).
- **When editing any of the `paths:` files above** — re-run the relevant audit before committing.
- **When diagnosing a "crash near a specific place" report** — it is almost always a stale data reference (scene, item, troop, culture), not an engine-internals bug. Audit the references first.

## Why this rule exists

2026-05-28 session: "fighting battles near specific places crashes" after the v1.3.15→v1.4.5 bump. Root causes were ALL stale-vs-vanilla references:
- v1.4.5 renamed `<culture>_house_a_interior_house` interior scenes; 61 stale `scene_name` refs across TAOM towns.
- TAOM's `sp_battle_scenes.xml` mapped map indices 158–255 to `battle_terrain_extended`, a scene that doesn't exist on disk.

Full write-up: `docs/reference/scene-reference-audit.md`. Memory: `feedback_scene_name_refs_break_on_version_bump.md`. Sibling research-first rule for code: CLAUDE.md "Research First" (decompile before guessing TaleWorlds behavior) — this rule is its data-side counterpart.
