---
name: author-armor
description: Author a culture's LOTRLOME armor items and swap troop equipment rosters via the generator + validation pipeline. Use when adding/revamping armor. Enforces the canonical-folder + cover-attribute rules.
argument-hint: [culture]
---

# Armor Item Authoring + Troop Roster Revamp

Author or revamp a culture's `LOTRLOME_Armory` armor items and re-point troop equipment rosters. This is the **armor-only** flow (revamps like #99 / #211 / #212 / #224). For a brand-new culture (armor + troops + recruitment + culture wiring), use `/new-culture` instead.

Reference: CLAUDE.md "Equipment & Armory" table + tools/README.md (Content Generation / Rebalancing sections).

## Step 0 — Find the canonical folder (MANDATORY, the recurring bug)
Before authoring ANY item, grep **all** `LOTRLOME_items/*/` subfolders for the item-id prefix:
```bash
grep -rl 'sk_<prefix>_' "<armory>/ModuleData/LOTRLOME_items/"
```
The first folder that already contains that prefix is the canonical home. Authoring into a different folder creates runtime duplicate-ID warnings (the engine silently shadows one). Even when the spec is named for culture X, the home may be a sub-culture — e.g. `sk_dwarf_iron_*` lives in `iron_hills/`, NOT `erebor/` (caught in #211). CLAUDE.md has the full per-prefix → folder table. Memory: `feedback_multi_folder_id_uniqueness.md`.

## Step 1 — Author the armor items
- Use/clone the matching generator: `tools/generate_<culture>_armor.py` (`--dry-run` then `--apply`; `--armory-path` to target the Steam install).
- **Cover attributes** — leg items need `covers_legs="true"`, gloves `covers_hands="true"`, or the engine equips but doesn't render the mesh (bare legs/hands). Memory: `feedback_lotrlome_armor_cover_attributes.md`.
- `<Flags UseTeamColor="true" />` for banner tint.
- Standalone `<EquipmentRosters>` civ rosters need `equipmentType="Civilian"` (battle is the implicit default) — memory `feedback_equipmenttype_civilian_required.md`.

## Step 2 — Swap troop rosters
- `tools/apply_<culture>_troop_revamp.py --dry-run` / `--apply` — mechanical EquipmentRoster swap (+ any new troops / deletes).
- If troops were added/deleted, also run the sweep + party-template scripts (`tools/cleanup_deleted_troops_*.py`, `tools/expand_party_templates_*.py`).

## Step 3 — Validate (underwear-bug gate)
```bash
python tools/validate_all_troop_refs.py
```
Cross-checks every `sk_*/ar_*/clo_urukscout_*/urukscout_*` ref across all 7 culture troop XMLs against the Armory. Missing refs → characters spawn in underwear. Prefer this over the Gondor-only `validate_gondor_refs.py`.

## Step 4 — Ship
`/ship` if any C# changed; otherwise `/verify` + CHANGELOG + issue.

## Gotchas
- Verify troop IDs against canonical troop XML — sibling-naming symmetry is a false signal (memory `feedback_verify_troop_ids_against_canonical_xml.md`).
- Armory dependency is `LOTRLOME_Armory` (NOT `Armory_2` — being deleted).
