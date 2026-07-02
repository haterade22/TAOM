# Weapon XML Pipeline

Project-agnostic FBX -> 4-XML weapon-build automation. Replaces the manual workflow of editing four separate files (in three different formats) every time a new weapon mesh ships.

> **Authoring by hand instead?** See [weapon-creation-workflow.md](../ai-includes/weapon-creation-workflow.md) — the manual Step A–Z guide for the same four files. Prefer the manual workflow when pieces are **shared / interchangeable** (e.g. 8 spear+polearm heads across 3 shafts), which this one-weapon-per-manifest-entry pipeline can't express without redundant duplicate pieces. The manual guide also documents the per-piece schema, the `bo_` collision convention, and the **bows/shields-cannot-use-decimals** datatype rule.

## Overview

`tools/build_weapon_xml.py` reads a small XML manifest describing one or more weapons, optionally cross-checks against an FBX file, and emits idempotent edits across the four weapon-system files in a Bannerlord module's `ModuleData/`:

| File | Type | Purpose |
|------|------|---------|
| `LOTRLOME_crafting_pieces.xml` | Full XML | Defines `<CraftingPiece>` entries; the only file that references FBX mesh IDs |
| `LOTRLOME_items/LOTRAOM_weapons.xml` | Full XML | `<CraftedItem>` (4-piece) and single-piece `<Item>` (bow/javelin/etc.) entries |
| `crafting_templates.xslt` | XSLT | `<UsablePiece>` inserts so the smithing UI lists new pieces under each `CraftingTemplate` |
| `weapon_descriptions.xslt` | XSLT | `<AvailablePiece>` inserts so the weapon class accepts the new pieces |

The four file paths are configurable; defaults match the LOTRLOME_Armory layout.

## Why This Exists

Adding a new sword to the Armory historically required:

1. Hand-typing four `<CraftingPiece>` blocks (~100 lines) into `LOTRLOME_crafting_pieces.xml`, each referencing a specific `mesh="..."` from the FBX.
2. Hand-typing one `<CraftedItem>` block in `LOTRAOM_weapons.xml` referencing the four piece IDs.
3. Adding four `<UsablePiece piece_id="..." />` lines to the `OneHandedSword` block in `crafting_templates.xslt`.
4. Adding four `<AvailablePiece id="..." />` lines to the `OneHandedSword` block in `weapon_descriptions.xslt`.

A typo in any one of the eight piece IDs that span those four files means the weapon silently fails to load, with no error in the game log. The Armory contains hundreds of `wm_*_blade/guard/hilt/pommel` quartets; each was hand-typed.

## Architecture

```
+-----------+     +-------------------+     +--------------------+
| .fbx file | --> | extract.py        | --> | mesh-name list     |
| (optional)|     | (Blender headless)|     | (cached as .txt)   |
+-----------+     +-------------------+     +--------------------+
                                                     |
                  +----------------+      classify   |
                  | manifest.xml   | -----+          |
                  | (human input:  |      v          v
                  |  damage,tier,  |     +--------------------------+
                  |  weight,name)  |     |       pipeline.py        |
                  +----------------+     |  - manifest specs        |
                                         |  - mesh-name validation  |
                                         |  - culture resolve       |
                                         |  - emit 4 file deltas    |
                                         +--------------------------+
                                                     |
              +--------+--------+--------+--------+--+
              v        v        v        v        v
       +------+------+------+------+------+------+------+------+
       | render_pieces.py                                       |
       |   crafting_pieces.xml  <- new <CraftingPiece> blocks   |
       +--------------------------------------------------------+
       +--------------------------------------------------------+
       | render_items.py                                        |
       |   weapons.xml          <- new <CraftedItem> / <Item>   |
       +--------------------------------------------------------+
       +--------------------------------------------------------+
       | render_xslt.py                                         |
       |   crafting_templates.xslt   <- new <UsablePiece>       |
       |   weapon_descriptions.xslt  <- new <AvailablePiece>    |
       +--------------------------------------------------------+
                                                     |
                                                     v
                                             +---------------+
                                             | verify.py     |
                                             | cross-ref     |
                                             | check         |
                                             +---------------+
```

All file emissions are **idempotent**: re-running the same manifest is a zero-diff no-op. Existing IDs are skipped, never overwritten. To re-author an existing piece, delete it from the file first.

## Configuration

The tool is project-agnostic. Resolution order for the `ModuleData/` target:

1. `--module-data <path>` CLI flag
2. `weapon_xml.toml` in the current working directory
3. Interactive prompt: scans `%BANNERLORD_GAME_DIR%/Modules/*/ModuleData/` for any folder containing all four target files; user picks.

A starter config looks like:

```toml
# weapon_xml.toml
[output]
module_data = "E:/Steam/steamapps/common/Mount & Blade II Bannerlord/Modules/LOTRLOME_Armory/ModuleData"
# Each filename is configurable; uncomment to override:
# crafting_pieces_xml      = "LOTRLOME_crafting_pieces.xml"
# items_xml                = "LOTRLOME_items/LOTRAOM_weapons.xml"
# crafting_templates_xslt  = "crafting_templates.xslt"
# weapon_descriptions_xslt = "weapon_descriptions.xslt"
```

Run `python tools/build_weapon_xml.py --init` to write one after the prompt.

## Manifest Schema

Manifests are XML, intentionally close to the output dialect so familiar field names carry over.

### Crafted (4-piece) weapon

```xml
<WeaponManifest>
  <CraftedWeapon id="wm_swan_knight_sword_c"
                 name="{=aom_swan_knight_sword_c_name}[Gondor] Swan Knight Sword (variant C)"
                 weapon_class="OneHandedSword"
                 culture="gondor"
                 tier="4"
                 modifier_group="sword">
    <Blade length="92.4" weight="0.93">
      <Thrust damage_type="Pierce" damage_factor="2.4" />
      <Swing  damage_type="Cut"    damage_factor="2.8" />
    </Blade>
    <Guard length="2.93" weight="0.15" armor_bonus="5" />
    <Hilt   length="11.2" weight="0.20" />
    <Pommel length="3.6"  weight="0.10" />
  </CraftedWeapon>
</WeaponManifest>
```

The pipeline auto-derives:
- `id="wm_swan_knight_sword_c_blade"` (and `_guard`, `_hilt`, `_pommel`) for each piece
- `mesh="wm_swan_knight_sword_c_blade"` (mesh name == piece id by Armory convention)
- `body_name="bo_wm_swan_knight_sword_c_blade"` for blades (auto-prefixed `bo_`) unless explicitly given
- Localization keys: `{=aom_swan_knight_sword_c_blade_name}` etc., based on the weapon's name key

### Single-piece weapon (bow, javelin, throwing)

```xml
<WeaponManifest>
  <SinglePieceWeapon id="gondor_warbow_a"
                     name="{=aom_gondor_warbow_a_name}[Gondor] Gondor Warbow"
                     weapon_class="Bow"
                     culture="gondor"
                     body_name="bo_warbow_a"
                     mesh="warbow_a"
                     weight="0.4"
                     Type="Bow">
    <Weapon thrust_speed="92" speed_rating="87" missile_speed="84"
            weapon_length="106" thrust_damage="30" thrust_damage_type="Pierce"
            item_usage="bow" physics_material="wood_weapon">
      <WeaponFlags RangedWeapon="true" HasString="true" StringHeldByHand="true" />
    </Weapon>
  </SinglePieceWeapon>
</WeaponManifest>
```

Auto-detected when `weapon_class` is `Bow`, `Crossbow`, `Javelin`, `ThrowingKnife`, `ThrowingAxe`, or `Stone` — single-piece routing produces only an `<Item>` in `LOTRAOM_weapons.xml` and skips the crafting/XSLT flow.

## Mesh Naming Convention

The pipeline expects the existing Armory naming pattern:

| Mesh suffix | Piece type | Notes |
|-------------|------------|-------|
| `_blade` | `Blade` | Carries `<BladeData>` with damage/physics info |
| `_guard` | `Guard` | Cross-guard or grip flange |
| `_hilt` or `_handle` | `Handle` | Grip; `hilt` and `handle` are aliases |
| `_pommel` | `Pommel` | Counterweight |

Optional collision-mesh pairing: any `bo_<same_stem>` mesh in the FBX will be detected and surfaced as the blade's `body_name`.

## Culture Resolution

A weapon's culture is resolved in this order:

1. Explicit `culture="..."` attribute on the manifest entry
2. Prefix-based detection (e.g., `wm_swan_knight_*` -> `gondor`, `wm_witch_king_*` -> `mordor`) — see `tools/weapon_xml/classify.py:_PIECE_CULTURE_PREFIXES`
3. Interactive prompt with `empire` as the default (skipped if `--no-interactive`)

The fallback is `empire` because that's the most common vanilla culture and won't break the file load if the modder forgot to specify one.

## CLI Modes

```bash
# Default: dry-run, prints unified diffs of all four files
python tools/build_weapon_xml.py --manifest weapon.xml

# Apply changes
python tools/build_weapon_xml.py --manifest weapon.xml --apply

# Batch all manifests in a directory
python tools/build_weapon_xml.py --manifest-dir weapons/ --apply

# Cross-check FBX mesh names against the manifest
python tools/build_weapon_xml.py --manifest weapon.xml --fbx weapon.fbx --apply

# Override the target ModuleData directory
python tools/build_weapon_xml.py --manifest weapon.xml --module-data /path/to/ModuleData --apply

# Write a starter weapon_xml.toml after the interactive prompt
python tools/build_weapon_xml.py --init

# Suppress all prompts (CI-friendly); fail rather than ask
python tools/build_weapon_xml.py --manifest weapon.xml --apply --no-interactive
```

## Configuration

| File | Purpose |
|------|---------|
| `weapon_xml.toml` | Sticky per-project default for `module_data` and override filenames |
| `--module-data` CLI flag | Per-invocation override |
| Manifest `culture=` attribute | Per-weapon culture override |

## Key Files

| Path | Purpose |
|------|---------|
| `tools/build_weapon_xml.py` | CLI entry point; argparse + orchestration |
| `tools/weapon_xml/__init__.py` | Package marker |
| `tools/weapon_xml/extract.py` | Headless Blender wrapper around `tools/list_fbx_objects_all.py` |
| `tools/weapon_xml/classify.py` | Mesh-name -> (role, piece_type, culture); collision-pair detection |
| `tools/weapon_xml/manifest.py` | Parse `<WeaponManifest>` XML into spec dataclasses |
| `tools/weapon_xml/render_pieces.py` | Emit `<CraftingPiece>` blocks into `crafting_pieces.xml` |
| `tools/weapon_xml/render_items.py` | Emit `<CraftedItem>` and single-piece `<Item>` into the items XML |
| `tools/weapon_xml/render_xslt.py` | Insert `<UsablePiece>` / `<AvailablePiece>` into the two XSLT files |
| `tools/weapon_xml/verify.py` | Cross-reference checks across the four output files |
| `tools/weapon_xml/config.py` | TOML loader + interactive ModuleData scan/prompt |
| `tools/weapon_xml/pipeline.py` | Glue: manifest + FBX -> file deltas |
| `tools/tests/test_build_weapon_xml.py` | 19 unit tests (classify, manifest, render, idempotency, end-to-end) |
| `tools/tests/fixtures/*.xml` | Sample manifests (existing-weapon, fresh-weapon) |

## Dependencies

- Python 3.9+ (3.11+ uses stdlib `tomllib`; older needs `tomli`)
- Blender (headless) — only required when `--fbx` is passed; PATH or `BLENDER_EXE` env var
- No other Python packages

## Tests

19 unit tests in `tools/tests/test_build_weapon_xml.py` cover:

- Mesh name -> role classification, including `hilt`/`handle` aliasing
- Culture detection from prefix
- Bo_ collision-mesh pairing
- Manifest XML parsing for both `CraftedWeapon` and `SinglePieceWeapon`
- Required-attribute validation (raises `ManifestError`)
- `<CraftingPiece>` block shape and blade-data emission
- Idempotency: existing piece IDs are skipped, not duplicated
- `<CraftedItem>` references pieces in canonical order
- Single-piece `<Item>` includes `<ItemComponent>` and `<WeaponFlags>`
- XSLT insertion preserves existing entries, skips duplicates
- XSLT template synthesis when `weapon_class` block doesn't exist yet
- End-to-end: dry-run emits all four file deltas; second run is zero-diff

```bash
python -m unittest tools.tests.test_build_weapon_xml -v
```

## How-To: Add a New Weapon

1. Export the FBX from Blender with meshes named `wm_<theme>_<weapon>_<role>` (and optionally `bo_<theme>_<weapon>_<role>` for collisions).
2. Author a manifest at `weapons/<theme>_<weapon>.xml`:
    ```xml
    <WeaponManifest>
      <CraftedWeapon id="wm_<theme>_<weapon>"
                     name="{=aom_<key>_name}<Display Name>"
                     weapon_class="OneHandedSword"
                     culture="<culture>"
                     tier="3">
        <Blade length="..." weight="...">
          <Thrust damage_type="Pierce" damage_factor="..." />
          <Swing  damage_type="Cut"    damage_factor="..." />
        </Blade>
        <Guard length="..." weight="..." />
        <Hilt   length="..." weight="..." />
        <Pommel length="..." weight="..." />
      </CraftedWeapon>
    </WeaponManifest>
    ```
3. Dry-run: `python tools/build_weapon_xml.py --manifest weapons/<theme>_<weapon>.xml --fbx <theme>_<weapon>.fbx`
4. Apply: add `--apply`.
5. Launch the game, open the smithing menu, confirm the new pieces appear.

## How-To: Reuse for Another Mod's ModuleData

The pipeline is project-agnostic. Run `--init` from any directory containing weapon manifests and pick a target `ModuleData/`. The toml file pins the choice for future runs in that directory.

## What's NOT in Scope

- **Damage tuning** — handled by `tools/rebalance_weapons.py`, which runs on the output of this pipeline (computes per-culture multipliers, applies them to `damage_factor`).
- **FBX -> .tpac/.meta conversion** — Bannerlord's modding kit handles this; the pipeline only consumes mesh names.
- **Texture / material assignment** — lives in the FBX/material editor.
- **Hand-tuning numeric stats** — the manifest is the human-input layer; the pipeline does no balancing.

## Changelog

- 2026-04-27 — Added `tools/build_weapon_xml.py` + the `tools/weapon_xml/` package automating the four-file weapon-authoring flow (crafting pieces, items XML, both XSLTs); project-agnostic, idempotent, supports crafted (4-piece) and single-piece weapons; 25 unit tests; ships with in-session deep-review fixes (XSLT self-heal, `body_name` `sm_`/`wm_` derivation, atomic writes, newline preservation, prefix culture resolution). (#95)

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/ai-includes/weapon-creation-workflow.md](../ai-includes/weapon-creation-workflow.md)
- [docs/INDEX.md](../INDEX.md)

<!-- backlinks-end -->
