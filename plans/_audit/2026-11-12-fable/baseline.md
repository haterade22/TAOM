# Baseline: 2026-11-12-fable

Measured by the orchestrator at `7087fa633` (`bannerlord-1.5.x`, `v2.0.38`), in a detached worktree,
2026-11-12 between 21:42 and 21:52. Non-deploying forms only. The logs are in the run's scratch folder
(`scratch\baseline`), outside the repo. Later checks compare failure sets by name, never against zero.

| Check | Command | Result | Wall time |
|---|---|---|---|
| Build | `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=` | exit 0; `11 Warning(s)`, `0 Error(s)` | 6 s |
| C# suite | `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=` | `Passed!  - Failed:     0, Passed: 39250, Skipped:     9, Total: 39259, Duration: 1 m 52 s` | 129 s |
| Python suite | `python -B -m unittest discover -s tools/tests -t .` | `Ran 8578 tests in 363.306s`, `FAILED (failures=7, errors=2, skipped=13)` | 370 s |
| ModuleData | `python -B tools/validate_moduledata.py` | exit 0; `0 error(s), 1748 warning(s)` | 16 s |
| Doc lint | `python -B tools/lint_docs.py --fail-on-drift --summary` | exit 0; `total_findings: 5` (`context_budget: 5`), `ai_dashes: 0` | 7 s |
| Hook suite | `CLAUDE_PROJECT_DIR=<baseline worktree> bash tools/test_hooks.sh` | exit 0; `1267 passed, 0 failed` | under 10 min |

The 6 s build was the first build in a fresh worktree (the log shows the restore and both projects'
compile), with a warm compiler server on this desktop.

## The Python suite's failing tests at the baseline

All of them are heraldry and kingdom-generator checks that compare shipped data with a fresh generator
run. They fail on the trunk before this run changes anything, so they are not this run's to fix, and
an executor that touches `tools/` compares against this list by name.

- `tools.tests.test_make_faction_emblems.ShippedDataTests.test_check_is_clean_after_apply` (ERROR:
  `high_kingdom_of_lindon: sigil 505 has no ui_taom_bannericons sprite`)
- `tools.tests.test_make_faction_emblems.ShippedDataTests.test_every_listed_faction_resolves_to_a_256_emblem` (ERROR)
- `tools.tests.test_clan_heraldry_specs.ShippedSpecsMirrorTheLiveFiles.test_applying_every_spec_is_a_no_op`
  (`clans.xml would change`)
- `tools.tests.test_generator_regions_in_sync.GeneratorRegionsInSyncTests.test_every_owned_region_matches_a_fresh_run`
  (three subtests: `generate_new_faction_kingdoms.py --only arthedain --check`, the same with
  `--only eredluin`, and `promote_borrowed_cultures.py --only eredluin --check`)
- `tools.tests.test_lindon_clan_expansion.LindonClanExpansionDataTests.test_every_lindon_clan_flies_its_own_elven_banner`
- `tools.tests.test_lindon_clan_expansion.LindonClanExpansionDataTests.test_new_clans_names_tiers_homes_and_owners`
- `tools.tests.test_lindon_clan_expansion.LindonClanExpansionDataTests.test_the_lindon_kingdom_flies_an_elven_banner_of_its_own`

## ModuleData warnings at the baseline

`ARMOUR_MESH_TIER_LADDER 1504`, `MELEE_LADDER_INVERSION 135`, `INCONSISTENT_ARMOUR_SLOT 93`,
`CROSS_CULTURE_ARMOUR_INVERSION 12`, `UPGRADE_ARMOUR_REGRESSION 4`. Game data is outside this run.

## Repository shape at the baseline (tracked files)

`Main` 7,425 (4,342 `.cs`); `TAOM.Tests` 2,412 (2,403 `.cs`); `docs` 1,827 (1,762 `.md`); `tools` 1,260
(741 `.py`, 253 of them under `tools/tests`); `Client` 270 (268 `.cs`); `Dependencies` 70 (22 `.cs`);
`.claude` 103 (25 rules, 23 hooks, 44 skill files, 6 agents). Counted with `git ls-files` and `grep -c`.
