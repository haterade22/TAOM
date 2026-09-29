# RCA: ECC re-review changeset, deep-review findings (2026-09-29)

## Summary

The `/deep-review` of the ECC re-review changeset ([adopt-ecc-2026-09-29.md](adopt-ecc-2026-09-29.md)) ran six
lenses in two waves (Standards, Data flow, Tooling, Completeness; then Efficiency, Design). No CRITICAL.
Two HIGH, both in this change's new scanner code, and a set of MEDIUM and LOW findings. Every finding
below was re-checked against the file or reproduced before it was fixed. The dominant pattern is the one
the change set out to remove: **a gate that checks less than it says, and reports clean**. Step 1 fixed
the zero-file case of it and left the one-file case; the git gate copied a prefix rule's absence from
before #689; a doc claimed a server was removed that a plugin still declared.

## Findings

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|-----|---------|----------|------------|-------------------|
| 1 | HIGH | `--user` vetted 0 of the 7 plugins TAOM loads: it read `enabledPlugins` only from `~/.claude/settings.json`, while TAOM's tracked `.claude/settings.json` enables them | silent partial gate | The fake home in the tests enabled plugins the one way the code read; no test put the enabling in another scope | Vet every installed plugin with a live folder, once (`_plugin_roots`); test a plugin no settings file enables |
| 2 | HIGH | `--external` passed plugin code outside six named folders: a `lib/` or `bin/` script ran by `hooks.json` was scanned 0 times, exit 0; hook rules ran only under `/hooks/` | silent partial gate (repeat of Step 1's own defect) | Step 1 replaced "read `.claude/`" with "read a list of folders", the same shape one level down | Read the whole foreign tree except `.git`/`node_modules`; hook rules on every script and JSON there; binaries get the secret rules only |
| 3 | MED | Git gate: `--del --forc`, `--disc`, `--work` ran ungated; `restore -sSTABLE` regressed from ask to allow; `update-ref --stdin` and an all-zero id delete refs; `update-ref --delete` does not exist | option grammar (repeat of #689) | The prefix rule lived in `_pushjudge.py` and a lesson, not where the next gate was written; `-s` was not known to take a glued value | `long_is` prefix helper; `-s` ends a short bundle; extra `update-ref` forms; 40 test rows, including the original ask cases of the rewritten arms |
| 4 | MED | "GitHub MCP removed" was false: `github@claude-plugins-official`, enabled in tracked settings, declares the same server | claim not proven where it applies | The removal was checked in `.mcp.json` and by grep, not in a session's live server list | Plugin disabled; lesson "Removing an MCP server means every scope that declares it" |
| 5 | MED | MCP rules missed a plugin's flat `.mcp.json`, `headers`, and the secret patterns on `~/.claude.json` servers | parser reads one shape | The rule was written against TAOM's own `.mcp.json` | `_servers_of` reads all three shapes; `headers` treated like `env`; secret patterns run over the server maps only |
| 6 | MED | The new `_ENV_PATH` regex backtracked: 3 s at 80 characters, far longer at 110, on untrusted input | performance on crafted input | No timing test for a regex fed foreign data | Rewritten alternative; timing test |
| 7 | MED | The UNCHECKED early return skipped the repo-wide secret sweep: a git root with no config and a committed token went from rc 2 to rc 3 | ordering | The new branch was added before the sweep, not merged with it | One pipeline in `main`: collect, scan, sweep, then decide UNCHECKED |
| 8 | MED | The gait test's stub modules leaked into the process; with the other Blender test loaded first it failed to import | test isolation | Written by copying a sibling's pattern that installs stubs for good | `mock.patch.dict(sys.modules, ...)` for the load only |
| 9 | MED | Two user-scope tests could not fail (`>= 3` passed without the code under test; the history test passed by masking) | test that proves nothing | Assertions written loosely after the code worked | Exact counts; assert no `secret-*` rule at all |
| 10 | MED | Provenance row claimed every listed file names ECC; five do not | claim unchecked | Written from memory of the recent ports | Detail now names which files carry attribution; `context-restore` gained its line |
| 11 | LOW | Polearm hook: the no-Python path never muted; a mute from before the fix kept an unseen report silent | state carried across a behaviour change | The mute predates the channel fix | Mute on both answers; state file renamed to `-v2` |
| 12 | LOW | `analyze_gait` hardcoded four foot keys, so the spider layer raised `KeyError` (pre-existing, now feeding the new metric) | assumption | Only the elephant was run | `keys = list(FEET)` |
| 13 | LOW | Docs: `mcp-servers.md` table split and stale deny text, catalog counts, three stale mentions, an over-cap rule paragraph duplicated from `harness-facts.md`, `Last verified` date, index entries, review-record counts | doc drift | Edits made to the doc that described the change, not to every doc that reads it | Fixed; lint dash count 0 |

## Root-cause pattern

Findings 1, 2, 4 and 5 are one pattern: **the check was proven on the shape the author had in mind, not on
the shapes the harness actually has.** Plugins are enabled from several scopes; plugin code lives anywhere
in its tree; a server can be declared by a plugin; an MCP config has three shapes. Each time, the test
fixture was built from the same assumption as the code, so the test agreed with it. This is the silence
lesson ("A fail-open guard whose failure mode is silence") one level down, and it is recorded there as a
further instance rather than as a new lesson (one root cause, one entry).

## Why each lens caught or missed what it did

- **Tooling** found 1, 2, 3 by building the real shapes in probes (a plugin in `lib/`, a live
  `installed_plugins.json`, git's own option parsing on a throwaway repo).
- **Data flow** found 1, 4, 5 and 7 by following each input to its end, including files outside the diff.
- **Standards** found 3 (prefixes), 10 and most of 13 by checking claims against files.
- **Completeness** found 8, 9 and the untested original gate cases.
- **Efficiency** found 6 by timing the new regex on crafted input.
- **Design** proposed the single pipeline (7) and the whole-tree walk (2), which removed code while fixing both.
- The author's own verification (tests green, the 22-file ECC vet, a live polearm proof) missed all of them,
  because each was built from the author's picture of the input.

## Prevention recorded

- Lessons: `lessons/build-tooling-workflow.md` "A fail-open guard whose failure mode is silence" (instances),
  "An advisory hook's output must reach Claude" (again), "A gate that matches full option spellings misses what
  git accepts" (repeat), and two new entries: "A tool kept in two places drifts" and "Removing an MCP server
  means every scope that declares it".
- Rule text: `LESSONS-LEARNED.md` one-root-cause rule; `4-completeness.md` item 8 with its grep; the plan
  template's gate boundary.
- Gates: `tools/test_hooks.sh` 7k (PostToolUse output channel), 40 git-gate rows, and the scanner tests for
  every shape above.

No new always-load rule: every prevention sits where the next author of that kind of change reads.
