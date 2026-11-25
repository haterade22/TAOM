# Codex re-review of the fixes: dwarven war chariot (2026-11-24)

You are an independent reviewer. Review only; change no file. Repository root: this working directory
(`E:/repos/taom-dwarf-chariot`, branch `feat/dwarf-chariot`, base `bcecab80a`, all changes uncommitted: use
`git status` and `git diff`, and read the untracked files). Read `AGENTS.md` and `.ai/roles/reviewer.md` first.

A first review found two HIGH defects in two scripts that wrote item rows into the live, unversioned module
`LOTRLOME_Armory` from outside the repo: a "cooked package exists" guard that matched by substring, and a rollback
that restored a whole-file snapshot. Both scripts are replaced by one repo tool. Review that fix and the other
corrections of the same pass.

## Read

- `tools/apply_dwarf_chariot_armory.py` and `tools/tests/test_apply_dwarf_chariot_armory.py` (new). The tool holds
  three item rows and three English name rows as its spec and has dry run, `--check`, `--apply` and `--remove`.
  Its I/O helpers come from `tools/apply_animalia_armory.py` (`read`, `write`, `nl_of`) and `tools/_gamedir.py`.
- The live files it guards:
  `E:/Steam/steamapps/common/Mount & Blade II Bannerlord/Modules/LOTRLOME_Armory/ModuleData/LOTRLOME_items/LOTRAOM_horses.xml`
  and `.../ModuleData/Languages/loc_LOTRAOM_horses.xml`. Run `python tools/apply_dwarf_chariot_armory.py --check`
  (read only).
- `docs/reviews/rca-dwarf-chariot-2026-11-24.md` (the findings and what was done about each).
- The other corrections: `Main/_Module/ModuleData/special_resources/troop_resource_costs.xml` and
  `TroopWeights/troop_weights.xml` (two rows each), `tools/audit_polearm_shield_parity.py` (two ratchet rows),
  `tools/taom_schema.py` (comment), `tools/data/armour_colour_families.json`, `tools/armour_cover_flags.json`,
  `tools/data/armor_roster_tiers.json`, `tools/external-loc-coverage-baseline.json`, `docs/features/chariot.md`.

## Questions

1. In the new tool, can `--apply`, `--remove` or `--check` give a wrong answer or write a wrong byte: a block that
   matches twice, an anchor inside a comment, a file with mixed line endings, a BOM, rows present in one file and
   absent in the other, a package whose file name differs only in case, a package that exists twice under `Assets`,
   a `--remove` after a person edited one row? Is exit 0, 1 or 2 right in each case, by the convention of
   `tools/data_gates.py`?
2. Do the tests pin each guard from both sides, or does one pass for the wrong reason?
3. Are the two Gems rows and the two weight rows consistent with the rules written in the comments of their files
   and with the tests that pin those files?
4. Is any statement in the new section of `docs/features/chariot.md` or in the RCA not supported by the files?

## Output

Findings by severity (P1, P2, P3) with file and line, the evidence you read and the smallest fix. Mark what you
could not verify as UNVERIFIED. End with a one-line verdict.
