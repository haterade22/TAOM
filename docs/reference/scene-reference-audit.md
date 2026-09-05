# Scene Reference Audit

How to verify that every scene TAOM references actually exists on disk — and repoint the stale ones. Run this **after any Bannerlord version bump** and whenever editing `settlements.xml` or `sp_battle_scenes.xml`.

## Why

TaleWorlds renames and removes scenes between versions. TAOM references scenes in two places:

1. **`settlements.xml`** — each settlement's `<Location scene_name="X">` (town/village/castle/hideout scene + house/tavern/dungeon interiors).
2. **`sp_battle_scenes.xml`** — `<Scene id="X" map_indices="...">` maps campaign-map terrain cells to the battle-terrain scene used for **field battles**.

If `X` has no matching `SceneObj/X/` folder, the game **crashes** when that scene loads — entering the settlement/house, or fighting a field battle on a map cell mapped to the missing battle terrain. The crash surfaces far from the stale value, so it reads as "battles near a specific place crash" rather than "bad XML."

This is a data-reference bug, not an engine bug. **Check the references before suspecting engine internals.**

## The tools

| Tool | What it does |
|---|---|
| [`tools/audit_scene_names.py`](../../tools/audit_scene_names.py) | Extracts every settlement `scene_name` (live `TAOM_Map`, vanilla `SandBox`/`NavalDLC`, repo shadow), cross-references vs all `Modules/*/SceneObj/` folders (case-insensitive), classifies missing-everywhere vs WIP-in-`SceneEditData`, and diffs TAOM vs vanilla. |
| [`tools/audit_battle_scenes.py`](../../tools/audit_battle_scenes.py) | Compares TAOM's `sp_battle_scenes.xml` Scene ids vs vanilla + on-disk SceneObj; flags ids with no folder; checks 0–255 `map_indices` coverage (an uncovered index = no battle scene). |
| [`tools/remap_stale_scene_names.py`](../../tools/remap_stale_scene_names.py) | Verified `scene_name` remap (every replacement confirmed present on disk before writing); backs up the external file. Edit its `REMAP` dict for new renames. |

```bash
python tools/audit_scene_names.py        # settlement scene refs -> full report
python tools/audit_battle_scenes.py      # field-battle terrain scenes -> crash suspects + coverage
python tools/remap_stale_scene_names.py --dry-run   # preview fixes; --apply --backup to commit
```

## Key gotchas

- **Case-insensitive.** Windows resolves `HART_ISENGARD` vs `HART_isengard`. An exact-case audit false-flags these; the tools lower-case both sides.
- **Live vs shadow.** The loaded map is `<game>/Modules/TAOM_Map/ModuleData/settlements.xml` (external). The repo's `Main/_Module/ModuleData/settlements.xml` is a stale, unregistered shadow — fixing it is cosmetic. See [`taom-map-settlement-naming.md`](taom-map-settlement-naming.md).
- **Custom-scene typo vs vanilla fallback.** If a missing scene has a near-match in `TAOM_Map/SceneObj` (a real custom scene referenced with a typo, e.g. `lotraom_e_osgiliath` vs on-disk `lotrtaom_e_osgiliath`), repoint to the real custom scene. Only fall back to a vanilla scene of the matching settlement type when no custom scene exists.
- **Packed scenes.** Scenes can in principle be packed (`.tpac`) rather than `SceneObj/` folders; the folder check could false-negative. In practice TAOM/vanilla scenes are folders.
- **No duplicate Scene ids in `sp_battle_scenes.xml`.** Vanilla never reuses a `<Scene id>`. When repointing a broken battle terrain, use a real id NOT already in TAOM's file (don't duplicate an existing one).

## v1.4.5 findings (2026-05-28) — reference for the pattern

| Stale reference | Cause | Fix |
|---|---|---|
| `<culture>_house_a_interior_house` (battania/khuzait/sturgia/vlandia) | v1.4.5 renamed house-interior scenes | repointed to `battania_town_house_b_interior_b_house`, `khuzait_house_c_interior_a_house`, `sturgia_town_house_d1_interior_b_house`, `vlandia_city_house_a_interior_house` (61 refs) |
| `battle_terrain_extended` (map cells 158–255) | TAOM-invented catch-all scene that never existed | repointed to `battle_terrain_r` (real Plain scene) |
| `lotraom_e_osgiliath_i_forceatmo` | typo — real scene is `lotrtaom_e_osgiliath_i_forceatmo` | repointed to the real custom scene |
| `castle_orthanc_gate`, `castle_village_isengard_a`, `village_isengard_a` | custom Isengard scenes never built | repointed to rugged vanilla scenes (`battania_castle_a`, `battania_village_c`, `battania_village_e`); rebuild proper Isengard scenes later |

All 99 vanilla-derived hideout scenes were present (`bandit_forest_sv`, `desert_hideout_002/004_sv`, `hideout_steppe_001/002_sv`, `mountain_hideout_002/004_sv`, `sea_bandit_a-d_sv`). The 30 new `hideout_gondor/erebor/mirkwood_*` scenes are authored in the editor but not yet exported to `SceneObj/` — interim-repointed to vanilla hideout scenes so raids don't crash; revert each `scene_name` to its settlement id once the custom scenes are compiled to disk.

Full machine output: [`docs/reviews/scene-name-audit-2026-05-28.txt`](../reviews/scene-name-audit-2026-05-28.txt).

## Related

- [`worldmap-battle-scene-grid.md`](worldmap-battle-scene-grid.md) — how the `map_indices` get *chosen*: the baked `worldmap_battle_scene_grid` texture that maps map cells → `sceneIndex`. This audit validates the `sp_battle_scenes.xml` side; that doc covers the texture side.
- Rule: [`.claude/rules/vanilla-data-comparison.md`](../../.claude/rules/vanilla-data-comparison.md) (fires when editing the relevant XML)
- Memory: `feedback_scene_name_refs_break_on_version_bump.md`
- Post-bump checklist: [`docs/migration/v1.4.x-changes.md`](../migration/v1.4.x-changes.md)

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/bandit-management.md](../features/bandit-management.md)
- [docs/modding/module-map.md](../modding/module-map.md)
- [docs/modding/settlements.md](../modding/settlements.md)
- [docs/reference/bannerlord-engine-and-toolchain.md](./bannerlord-engine-and-toolchain.md)
- [docs/reference/doc-lookup.md](./doc-lookup.md)
- [docs/reference/worldmap-battle-scene-grid.md](./worldmap-battle-scene-grid.md)

<!-- backlinks-end -->
