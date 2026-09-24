# RCA: plan 008 review, the binding gate that passed by skipping (2026-09-24)

## Top-line

`/deep-review` of plan 008 (`7f02fc8d..8c89e042`, branch `improve/008-binding-gate-no-silent-skips`)
ran seven lenses: Standards, Engine compatibility, Data flow, Tooling, Efficiency, Completeness and
Design. A Codex adversarial review ran beside it. Together they confirmed 13 defects: 0 HIGH, 3
MED and 10 LOW. There was 1 false positive. The core mechanism held: the `TaomGameFolder` fallback,
the opt-in `binding-gate.runsettings` and the discovery floors all work as the plan intended. The
strict gate still runs 368 passed, 0 skipped with the game present.

The one finding that reaches past the change: **the new `PASSED WITH SKIPS` banner is written where
nobody reads it.** `notify-test-results.sh` prints to stderr and exits 0, and Claude Code sends that
channel to the debug log only (`.claude/rules/harness-facts.md` "Visibility"). The plan asked for
stderr. `hook-authoring.md` still advises stderr for an advisory hook. And `tools/test_hooks.sh` 7c
checked the text the hook prints, not whether it arrives. That is the same gap #647 found for nine
PreToolUse gates a day earlier, so this is a repeat.

Ten findings are fixed in this branch. Three are confirmed but left for Mike, because each changes
behaviour or needs a design choice: the banner's delivery channel, summing counts across several
summaries, and `TreatNoTestsAsError` plus the CI `if:`.

## Findings

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| F1 | MED | The skip banner goes to stderr from an exit-0 hook, which reaches the debug log only. The hooks catalog ("a non-zero skip count is always shown") and the CHANGELOG described it as visible. Found by Agent 1, Agent 5 and Codex P2. | Dead / no-op code | The plan specified stderr. `hook-authoring.md:128` still advises stderr for an advisory hook, which contradicts `harness-facts.md:60`. Section 7c pinned the printed text through `2>&1 >/dev/null`, not the delivery. | Claims corrected in the catalog, the CHANGELOG and the hook header. The channel is Mike's decision (NEEDS MIKE). Lesson in build-tooling-workflow. Recommended: reword `hook-authoring.md:128` (not edited here, to avoid conflicts between parallel branches). |
| F2 | MED | The hook printed nothing for an all-skipped run at normal verbosity: vstest prints no `Passed:` line then, and the new branch required `$PASSED`. Found by Tooling #1 and Agent 4 F7. | Missing null guard | The 7c fixtures were all minimal-verbosity summary lines, and CI runs `--verbosity normal`. | Fixed: the branch keys on the skip count alone and prints `${PASSED:-0}`. A new 7c case failed first (hook silent), then passed. |
| F3 | LOW | Two of the three existence guards in `ResolveGameDir` had no test: the override's `Bannerlord.exe` check and `Directory.Exists(gameDir)`. The change also moved the set-but-missing `BANNERLORD_GAME_DIR` from "skip" to "fall back", which nothing pinned. Found by Agents 1, 4 and 5 and Tooling #4. | Test gap | The tests covered each input's happy path. `tests.md` "Skip-Guard Exhaustion" asks for one test per guard in each direction, and it was not applied to a resolver. | Fixed: two tests. Deleting either guard turns exactly one of them red (checked by mutation). Lesson in testing-qa. |
| F4 | LOW | `verify-bindings/SKILL.md:27` said "every gate test calls `Assert.Inconclusive`" without the game. 33 of 368 pass without it (`6c.log`, `6d.log`). "When neither is set" also missed the missing-folder fallback. Found by Agents 1, 2, 4 and 5 and Tooling #5. | Other: doc claim contradicted by the measured run | The executor measured 33 passed and wrote "every". | Fixed. Lesson in build-tooling-workflow (docs follow the gate). |
| F5 | LOW | `SKILL.md:42` said "a red gate is a real finding, one of three classes". Plan 008 added two red forms it did not list: an environment Inconclusive mapped to Failed, and the discovery floors. Found by Agent 2 F1 and Agent 5 F6. | Other: consumer doc not updated | The line itself was unchanged, so no diff pointed at it. | Fixed: the environment case gets its own sentence and the floor messages get a table row. Same lesson as F4. |
| F6 | LOW | The changed floor messages kept stale counts: "(expected ~37)" against 46 models, and "(expected ~70)" against 247 patches. Found by Agent 2 F3. | Stale constant | The plan said to keep the messages word for word. | Fixed: each message points to the snapshot file that carries the current count. |
| F7 | LOW | `reflection-sites.md:10` still gave the gate command without `--settings` or `-p:ModuleId=`. The CHANGELOG said every doc giving the command had been updated. Found by Agent 5 F5. | Other: incomplete rename | The plan's Step 9 grep matched only the `TestCategory=BindingVerification"` form, and this line filters by `FullyQualifiedName`. | Fixed. Same lesson as F4. |
| F8 | LOW | The `GameAssemblies` doc comment listed "a -p: property on the build" as covered. On this desktop `BANNERLORD_GAME_DIR` is always set and wins, so that case is not covered. Found by Agent 5 F10 and Agent 2 F2. | Other: doc claim | The comment described the case where no variable is set. | Fixed: the comment now says a test-time variable wins. |
| F9 | LOW | The CHANGELOG entry was headed `## 2026-09-24`, the UTC date. The commit is 2026-09-23 23:25 -0500. Found by Agents 1, 2, 4 and 5 and Tooling. | Convention inconsistency | The executor ran after 23:00 local and dated the entry in UTC. | Fixed: the entry moved under `## 2026-09-23`. One-off. |
| F10 | LOW | The CHANGELOG said the change "closes the binding-gate half" of an RCA item. That item names only `LordFamilyTransformTests`, which is not in the gate. Found by Agent 4 F4. | Other: overclaim | The item was not re-read. | Fixed: reworded. One-off. |
| F11 | LOW | When one Bash response holds two test summaries, the hook reads only the first match of each count (`head -1`), so skips in a later summary are dropped. Found by Codex P3. | Logic error | The fixtures had one summary each. | Not fixed: the fix changes the old first-match parsing of Failed and Passed too, and it only matters once F1's channel is chosen. NEEDS MIKE, together with F1. The catalog now says "from the first summary line". |
| F12 | MED | The strict gate exits 0 when its filter matches no tests. Measured: `--filter "TestCategory=NoSuchCategory"` with the gate settings gives rc 0. Found by Tooling #2, Agent 6 P2 and Agent 4 F6. | Dead / no-op code | The problem predates the change, but the new file is the natural place to fix it: its stated job is to make a gate that checked nothing fail. | Not applied, because it changes behaviour: NEEDS MIKE. Evidence is ready. A scratch copy of the settings with `<RunConfiguration><TreatNoTestsAsError>true</TreatNoTestsAsError></RunConfiguration>` gives rc 1 on the zero-match run and leaves the gate at rc 0 (368/0/0). |
| F13 | LOW | The new CI step has no `if:`, so it is skipped whenever the default Test step fails. On this runner that step fails today on the two live-Armory tests. Found by Tooling #3, Agent 2 and Agent 4 F8. | Convention inconsistency | `build.yml`'s own always-run convention (validate-xml) was not applied to the new step. | Not applied (a CI behaviour change): NEEDS MIKE. The proposed line is `if: ${{ !cancelled() }}`. |

**False positive (1):** Agent 4 F2 wanted the metadata test to assert a non-empty value. Plan 010's
reference-assembly build deliberately emits an empty `TaomGameFolder`
(`plans/010-ci-on-hosted-windows.md:272-276`, `:782-783`). A non-empty assert would fail that build,
so the presence-only check is correct.

## Root-cause pattern

F1, F4, F5 and F7 share one cause: **the change's own claims were checked against its code, not
against what consumes them.** The banner was checked as text, not as something delivered. The skill
line was not checked against the 33 passes in `6c.log`, which was on disk. The triage table was not
checked against the new red messages. The doc sweep grepped for one spelling of the command. Each
claim was true of the diff and false of the system it describes.

F1 is a repeat. #647 (`rca-adr011-batch1-2026-09-23.md` F1) found gates whose output format Claude
Code ignores, and its lesson says to prove a gate live. `suggest-compact.sh` printed about 18
invisible reminders in one session (`harness-facts.md` "Visibility", EMPIRICAL 2026-09-23). This
plan was written the same day and still specified stderr, because the authoring rule it followed
(`hook-authoring.md:128`) was never reconciled with the visibility fact.

## Why each agent missed these

The lenses caught all of them between them. This section covers the builder and the plan, which
missed them, and each lens's blind spots.

- **Builder and plan:** the plan specified stderr (plan:392-393, 813, 831-839). Its Step 9 grep was
  narrower than the set of docs that give the command. Its test plan claimed "every cell" was
  covered, but covered only each input's happy path.
- **Agent 1 (Standards):** caught F1, F3, F4 and F9. It missed F5 and F7 because the unchanged
  `SKILL.md:42` and `reflection-sites.md` were outside the diff it checks line by line.
- **Agent 2 (Engine compatibility):** caught F4, F5 and F6. It did not look at hook delivery, which
  is outside its lens.
- **Agent 3 (Efficiency):** no defects expected from it. It timed the hook but did not ask where
  the output goes.
- **Agent 4 (Completeness):** caught F2, F3, F9, F10 and F12. Its F2 (metadata) was the one false
  positive, because it did not read plan 010.
- **Agent 5 (Data flow):** caught F1, F4, F5, F7 and F8. This is the lens whose "produced, never
  delivered" rule names F1's pattern.
- **Tooling lens:** caught F2 (by decompiling vstest's `ConsoleLogger`), F12 and F13. It checked
  that the hook exits 0 but did not look at where the output goes.
- **Agent 6 (Design):** proposed F12's fix and the build-folder-first order, which is a design
  choice, not a defect.
- **Codex:** caught F1 independently, with the vendor doc, and alone found F11. It missed F2 to F10.
  Its review stayed on the code paths and did not audit the prose claims.

## Feedback to codify

- `docs/reviews/lessons/build-tooling-workflow.md`: "An advisory hook's output must reach Claude:
  test the channel, not the text" (F1). Also "A change to how a gate behaves updates every doc that
  runs or reads it" (F4, F5, F7).
- `docs/reviews/lessons/testing-qa.md`: "Every existence guard in a resolver gets a test that fails
  it" (F3).
- Recommended rule edit, not made on this branch: `.claude/rules/hook-authoring.md:128` should read
  "or, for an advisory hook, a channel from `harness-facts.md` 'Visibility'; stderr from an exit-0
  hook reaches no one".

## Resolution of the open findings (2026-09-24, #652)

- **F1 and F11:** Mike chose to remove the banner change. `notify-test-results.sh` is back to its
  content at `7f02fc8d`, and `tools/test_hooks.sh` 7c is gone. The signal for a skip is the
  `Skipped:` count in `dotnet test`'s own output, and for the gate the strict runsettings. F11
  lapses with the banner.
- **F12:** fixed. `binding-gate.runsettings` sets `TreatNoTestsAsError`; a zero-match filter under
  it now exits 1, and `BindingGateRunSettingsTests` pins the setting.
- **F13:** not among the decisions; still open.
