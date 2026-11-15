# Codex pre-review: troop skills follow the kit (2026-11-15, #343)

You are the reviewer. Read `.ai/roles/reviewer.md` and `.ai/policy.md` first. Review only; change no
file, run no command that writes outside `docs/reviews/raw/`. Never write to the game install.

## Scope

Worktree `E:/repos/taom-troop-purpose`, the uncommitted diff against `HEAD` (`git diff` and the
untracked files `git status --short` lists):

- `tools/troop_purpose.py` (new, the one pure rule), `tools/rebalance_troops.py` (the rule inside
  `calculate_skills`, new mode `--fix-purpose`), `tools/realm_troops.py` and the realm generators,
  `tools/taom_schema.py` (codes `TROOP_SKILL_KIT_MISMATCH` and `TROOP_ITEM_ZERO_NOT_MAIN`),
  `tools/analyze_troop_balance.py`.
- `.claude/hooks/check-moduledata-validation.sh`, `.githooks/pre-commit`: one error code added.
- About 22 files under `Main/_Module/ModuleData/troops/`: `<skill>` values only.
- Tests and docs, `docs/reviews/rca-troop-skill-purpose-2026-11-15.md`,
  `docs/reviews/troop-roster-analysis-2026-11-15.md`.

Intent (maintainer): a troop's skills must fit its purpose. The class of the Item0 weapon (the
weapon `Equipment.GetInitialWeaponIndicesToEquip` puts in the hand at spawn) is the main class and
takes the troop's highest weapon skill; other carried classes come next; classes the troop does not
carry come last. On disk the fix permutes the troop's own values inside the three melee skills and
inside the two launcher skills, so no troop's total moves, with one exception: when battle sets tie
between two classes for one role, both take the higher value. A thrown weapon in Item0 is a valid
main weapon (skirmishers, javelineers). No item changes and no slot changes. Upgrade monotonicity
holds: no skill on an upgrade target sits below its source. The level of the curve is not in scope.

## Questions to answer with evidence

1. Recompute independently, from the troop XML and the item class registry
   (`taom_schema.build_item_class_registry`), for HEAD and for the working tree: troops whose best
   melee skill is on a class they do not carry, troops whose Item0 class is not their top carried
   skill, by culture. Compare with the validator's counts.
2. Prove from the diff that only `<skill value>` attributes changed in the troop files (no item, no
   slot, no level, no other attribute), and that for every changed troop the multiset of melee
   values and of launcher values is unchanged except the documented tie raises and clamp raises.
   List every troop whose total of the eight skills changed, with the reason.
3. Monotonicity: run the validator's upgrade skill code and `python tools/rebalance_troops.py
   --fix-monotonicity --dry-run`; find an upgrade edge where the child's main class is no longer its
   top skill after the clamp.
4. `calculate_skills`: does a later full `--apply` reproduce the on-disk result for the changed
   troops, or would it undo the permutation? What did the change remove from the name-keyword
   shifts and the Polearm and TwoHanded swap, and does any troop lose an intended specialization?
5. Generators: every generator that owns a troop file must produce the same skills
   (`--check` of each, `python -m unittest tools.tests.test_generator_regions_in_sync`). The realm
   generators now need the game install: what happens on a machine without it?
6. C#: which tests and which runtime code read troop skills or weapon slot order
   (`TAOM.Tests`, `Main/Features/**`: career kits, enlistment, Marshal's Word trials, Long Defeat,
   Fords of War, formation classes, AI weapon choice)? Does any of them pin a value that moved?
7. The two hook edits: correct, minimal, and consistent with
   `tools/tests/test_commit_gate_coverage.py`?
8. The 89 remaining warnings (launcher main class with a higher melee skill): are they all ranged
   ladder cells as the RCA says?

## Output

Findings by severity (P1 blocks the commit, P2 fix before merge, P3 note), each with file, line and
evidence. Say UNVERIFIED where you could not check. End with a verdict line.
