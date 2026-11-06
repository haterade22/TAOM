# RCA: language text processors reset (#1026, deep-review, 2026-10-26)

## Top line

`/deep-review` ran all eight lenses in three waves on the fix that restores the game's grammar processors. The first fix covered only the seven per-language folders, and the review found that English was broken the same way: the live Armory's root `language_data.xml` named English and declared no processor, so lords' parties read "Aragorn Party". The other findings were in the guards around the fix: the gate that checks the files, its tests, and the release check that carries the fix to players. All of them were fixed before the commit, apart from two decisions left to Mike.

The gate findings repeat a pattern already recorded twice today (#1020, #1017 in `lessons/build-tooling-workflow.md`): a gate that passes on input it never read. The pattern behind most findings: the fix and its gate were built for the files that showed the fault, not for every file the engine reads, and the gate was proven only on the state it was written to catch.

## Findings

| # | Sev | Finding | Why missed | Fix |
|---|---|---|---|---|
| 1 | HIGH | English stayed on the default processor. The live Armory root file has `name="English"` and no processor, and TAOM's root file restored nothing. | The issue said "every language except English", so the fix, the gate (`*/language_data.xml`) and the test (12 subfolders) all left out the root folder, where English lives. | TAOM's root file and the live Armory root file carry Native's English processor; the gate and the test read every depth. |
| 2 | MED | Players keep the fault until a release ships new TAOM_Map and Armory packages. All three channels still carry the 15 old files, and a profile with no saved order loads TAOM_Map after TAOM. | The fix was proven on this desktop, whose saved order and live files hide the channels' state. | A rule in `release-process.md`, `--processors-only` for a channel, and a blocking step in `/release` Phase 1. |
| 3 | MED | The release check could pass without checking anything: a bash path lost its backslashes and printed SKIP with exit 0, PowerShell did not run the command, and an empty channel passed after reading 0 files. | The rule's pass test was "no FAIL line", which every failure to run also meets. | `--processors-only` exits 0, 1 or 2 with one meaning each, and exits 2 when it reads 0 files; the rule needs exit 0 and the PASS line. |
| 4 | MED | The gate's reader differed from the engine's. A UTF-8 file that declares utf-16 (22 of Native's 23 TR files have that shape) failed to parse and was skipped, so the gate passed the fault it exists to catch. A missing Native also passed. | The gate used `ET.parse` and treated a parse error as "nothing to check". | The gate reads with `_parse_like_engine` at any depth, and exits 2 on an unreadable file, an empty Native table or no files. |
| 5 | MED | The tests could not fail on the UTF-16 path, on the exit code, on reading Native from the real install, or on running both checks. Mutants of each kept all tests green. | The tests were written beside the code and checked only the happy path of each helper. | Tests through `main()` and the new mode; four mutants are now killed. |
| 6 | MED | The 15 live edits had no APPLIED EDIT record, and their backups were only in a session scratchpad. | The live-edit convention (`module-armory.md`) was not in the fix brief. | The snapshot README block, durable backups in `E:\LOTRAOMAssets\_loc_backup_20261026\`, and a LiveInstall C# test of the live modules. |
| L | LOW | Other LOW findings, all fixed: the gate checked one direction only; one level of folders, where the engine reads every depth; a returned-early ratchet; the exit contract undocumented; an inverted test branch; a test that could never fail alone; a private path locator; a JP fixture with the wrong id; doc drift ("Assembly-qualified", the `under_development` default, "TAOM loads last"); an issue draft naming a command that does not exist yet. | Hygiene, and the same "built for the case in hand" pattern. | Fixed. |

## Known effects, not fixed here

Running the processors again changes how vanilla grammar meets TAOM's untagged names: an untagged name can take the gender of an earlier tagged word (IT, SP, DE, PL, RU), and French puts a bare "de" before a name that starts with its own article. Tagging TAOM's names is #1023's work; phase 1 covers BR and IT. Turkish `{._}` lowercases with Turkish rules, as it does for vanilla names.

## Decisions owed to Mike

- A test that pins TAOM.Dependencies' `ModulesToLoadAfterThis` Native entry, the load order that keeps its name-bearing files harmless.
- A replay script for the 15 live edits. Its use ends when the next release ships the fixed packages.

## Lessons

- `lessons/localization-ui.md`: the #1026 entry (every module's `language_data.xml` that has a `name` must carry Native's processor).
- `lessons/build-tooling-workflow.md`: "Prove a gate on a tree it cannot read, not only on the fault it was built for".
