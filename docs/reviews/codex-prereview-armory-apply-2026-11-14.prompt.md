# Codex independent review: the live Armory rewrite for #1271, 2026-11-14

You are the reviewer (see `.ai/roles/reviewer.md`). Review only: change no file in the repo or the
game install, run no tool with `--apply`, run no git command that changes anything. Report defects
with evidence. Engine facts come from the installed game (`pwsh tools/taom-src.ps1 path <Type>`).

## What happened

The tools of commit a08efb741 on this branch (`balance/weapon-armour-vanilla`) were run against the
live, unversioned module `E:/Steam/steamapps/common/Mount & Blade II Bannerlord/Modules/LOTRLOME_Armory`
in the order of `docs/features/armor-balance.md` "The apply sequence":

1. `derive_armor_tiers.py`, then `rebalance_armor.py --apply --all --tier-source roster-first
   --keep-weights --keep-material-type --backup-tag kcurve-20261114` (83 item files, then 2 more in the
   repair loop; a further run plans 0).
2. `generate_lothlorien_armor.py --apply`, `generate_snow_orc_armor.py --apply`.
3. `fix_upgrade_armour_regressions.py --apply` (repo troop rosters, 50 slot swaps in 7 files; 0 edges left),
   and the Cardolan levy swap carried into `tools/generate_cardolan_troops.py`.
4. `generate_armour_classes.py --apply` (repo `armour_classes.xml`).
5. `restat_melee_blades.py --apply` (203 pieces in `LOTRLOME_crafting_pieces.xml`; a further run plans 0).
6. `generate_career_kits.py --apply` (repo), `generate_starter_kit.py --apply` (live starter blades),
   `generate_placeholder_weapons.py --apply` (repo).
7. `generate_ranged_ladder_items.py --apply` (147 live ladder items).

Not run, by decision: `fix_melee_ladder.py --apply` (its plan swapped poleaxes for short spears),
`generate_starter_armor.py --apply`, and the `WORN_ARMOUR_CEILING` error flip.

A full copy of the Armory's ModuleData from before any write is at
`E:/Backups/LOTRLOME_Armory-ModuleData-pre-rebalance-20261114`. The uncommitted repo changes are in
`git status --short` of this worktree.

## What to check

1. The live item files against the copy from before: for every armour item, is each changed value
   the one the rule gives (kingdom cap x slot share x band share, with the head band overrides;
   secondaries 0 below the heavy band and capped above; modifier group as the rule says)? Is anything
   changed that must not be: troll kit, HorseHarness, weights, materials, meshes, any attribute other
   than the armour stats and `modifier_group`? Did any file lose its BOM, change line endings, or
   lose or gain an item? Does every file parse?
2. Hero-only armour: none raised. Hero kit that ordinary troops wear (the `gf_` Golden Flower set,
   Erkenbrand, Theoden boots and glove, Theodred armour and helmet): on the troop curve.
3. What real troops wear now, built the way `BasicCharacterObject.Deserialize` builds battle sets
   from the repo troop files as they are in this worktree: any troop above 64 / 77 / 69 / 59
   (head, body, arm, leg); any troop with head or body 0 that had armour before; the best elite
   totals per kingdom against the design (Erebor 54 / 67 / 57 / 44).
4. `LOTRLOME_crafting_pieces.xml` against the copy: only `damage_factor` values of blade pieces
   changed; no weapon a troop carries displays 0 or 1; no ordinary one handed weapon displays 100 or
   more; the seven blades shared by one handed and two handed weapons moved only down to a class
   limit; hero blades only lowered.
5. The 147 ranged ladder items: damage, speed and accuracy equal `tools/ranged_ladder.py cell()`; no
   id lost that a troop or a translation row names.
6. The repo files of the second commit: the 7 troop files (every swapped item exists, the troop's
   culture owns it, no slot holds two armour pieces, the upgrade edges no longer lower armour),
   `armour_classes.xml`, `taom_career_starting_equipment.xml`, `ph_crafting_pieces.xml`,
   `tools/data/armor_roster_tiers.json`, `tools/generate_cardolan_troops.py`.
7. Anything the sequence left inconsistent: a generator whose `--check` or `--verify` now fails, a
   gate that passes because it reads nothing, a doc that states a value the live Armory no longer has.
8. `python tools/analyze_armor_balance.py --stdout` reports 10 errors after the write, among them
   TIER INVERSION on `sk_gb_orc_inf_pauld_cape_light_a`, six `sk_ll_` medium helmets,
   `sk_ll_ml_pauld_med_a` and `sk_rh_loke_grvs_plate_light_a`. Are these real defects of the new
   values (a legendary roll on a low band piece reaching a piece two bands up), were they present
   before the write (run the same analysis on the copy), and what is the fix?

## Output

Findings by severity (P1, P2, P3), each with file, item id, evidence and a concrete fix. Then what
you verified and found sound, and what you could not verify.
