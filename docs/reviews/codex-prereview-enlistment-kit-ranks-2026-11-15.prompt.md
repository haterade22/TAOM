# Codex pre-review: enlistment kit rank rule (2026-11-15, #1290)

You are the reviewer. Read `.ai/roles/reviewer.md` and `.ai/policy.md` first. Review only; change no
file, run no command that writes outside `docs/reviews/raw/`. Never write to the game install.

## Scope

Worktree `E:/repos/taom-enlistment-kits`, the uncommitted diff against `HEAD` (`git diff` and any
untracked file `git status --short` lists):

- `Main/_Module/ModuleData/equipmentsets/taom_enlistment_equipment.xml`: repaired kits.
- `tools/generate_enlistment_rosters.py`: the rule `rank_regressions`, the mode `--repair-ranks`,
  the guards on `--apply` and `--reseed`.
- `tools/taom_schema.py`, `tools/validate_moduledata.py`: warning `ENLISTMENT_KIT_RANK_REGRESSION`.
- `tools/promote_borrowed_cultures.py` if changed, `tools/data_gates.py`, tests, docs.

Intent: after the armour and weapon rebalance some enlistment ranks (the kit a player receives per
rank in a lord's service) were weaker than the rank below. The rule: a rank must not lose total
armour; for each weapon category both kits hold, the upper kit's best weapon must not be weaker
(3% tolerance); a category the upper kit drops is reported and never auto-repaired. Rank pairs
follow the order in which the C# resolver hands out kits (`Main/Features/Enlistment`). The repair
prefers the culture's own gear. Maintainer's rules: a kit keeps its weapon category at every rank
(one handed weapon with shield; two handed sword or axe; spear and shield; lance; polearm; pike;
bow; crossbow); items stay in the culture's own gear; no player-facing text changes.

## Questions to answer with evidence

1. Read the C# resolver and prove or disprove that the Python pair builder mirrors it for every
   culture, assignment and tier, the fall back to the infantry kit and to the default kits included.
2. Recompute the findings independently from the kit file and the live Armory values
   (`E:/Steam/steamapps/common/Mount & Blade II Bannerlord/Modules/LOTRLOME_Armory/ModuleData`): the
   count at HEAD's kit file and at the working tree, by reason. Does any real weaker rank escape the
   rule (a weapon category misread, a shielded weapon compared with an unshielded one, ammunition
   count, a mounted kit handed a foot-only launcher)?
3. For every changed slot: old item, new item, weapon template or armour slot of both, whether the
   new item is worn or carried by that culture's troops, and whether the change keeps the category.
   Flag any kit that now holds one item id in two slots, an empty slot or two pieces for one slot.
4. Ownership: prove that a promoter run, a `--reseed --culture`, a `--seed-missing` and a full
   `--apply` each either keep the repair or refuse. Run every promoter `--check` and
   `python -m unittest tools.tests.test_generator_regions_in_sync`.
5. The validator code without the Armory installed, with a kit id the index does not know, and with
   the generator module missing.
6. Every count in the changed docs: reproduce it with the named command or name it as wrong.

## Output

Findings by severity (P1 blocks the commit, P2 fix before merge, P3 note), each with file, line and
evidence. Say UNVERIFIED where you could not check. End with a verdict line.
