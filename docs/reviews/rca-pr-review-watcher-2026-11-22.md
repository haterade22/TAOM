# RCA: the PR review watcher's Phase 1 (#1543, 2026-11-22)

## Top-line

The tier 2 review of the PR automation branch (Lens 5 Data flow and the Tooling lens; Codex not
authorized) found three HIGH defects in `tools/pr_review_watch.py` before its first commit, and the
second wave (Lenses 3, 4 and 6) found a fourth in the first fix. Each one let a PR that the
`/review-outside-pr` skill stops by hand go past the watcher's Phase 1 to a model review. All four,
and the MEDIUM findings of both waves, were fixed before the commit. The 49 watcher tests pass; the
21 new or changed tests of the first fix fail on the old code (15 failures, 6 errors), and each of 11
fixes, broken on its own in a copy, makes a test fail. Nothing reached a player or a contributor:
the watcher had not run on a real PR.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| F1 | HIGH (Tooling 1, Lens 5 13) | `unreadable_files` flagged a file only when it had no patch and `changes > 0`. GitHub reports a binary with no patch and `changes` 0 (trunk `415277404`, an added PNG), so no binary was ever flagged. | Port narrower than the check it ports; invented fixture | The skill's check is `select(.patch == null)`. I added `changes > 0` to exempt a pure rename, without reading what GitHub returns for a binary. The test built `icon.png` with `changes=2`, a shape GitHub never returns, so it passed. | The filter now flags every file with no patch, as the skill does (see F4). The binary test uses GitHub's real shape and names the commit it came from. Lesson in `build-tooling-workflow.md`. |
| F2 | HIGH (Tooling 2, Lens 5 13) | The `--jq` projection dropped `previous_filename`, so a rename out of a high-risk path (a hook moved to `docs/`) was checked only on its new name. | Port drops a field the manual check sees | The skill reads the whole diff by hand, where a rename shows both names. The port chose its fields from what the checks used, and no check used the old name. | `previous_filename` is fetched, and both names go through `safe_relpath` and the high-risk match. Test: a rename out of `.claude/hooks/` stops. |
| F4 | HIGH (second wave, Design D1) | The F1 fix still exempted a rename with `changes` 0. Trunk `9a409deac` renamed a PNG and changed its content, and GitHub reports that as `renamed`, `changes` 0, no patch: the same shape as a rename with no content change. A changed binary passed if it was also renamed. | Fix built on an unchecked shape | The first fix kept the exemption I had invented for F1 and only narrowed it. I fetched the real shape of an added binary, not of a renamed one, so the fix repeated F1's cause one level down. | No exemption: every file with no patch stops, as the skill's `select(.patch == null)`. A pure rename now stops too, which costs one manual read. The test uses the `9a409deac` shape. |
| F3 | HIGH (Tooling 3, Lens 5 12) | The high-risk match was case-sensitive and root-only for seven names, so `.Claude/`, `Main/CLAUDE.md`, `docs/AGENTS.md` and `Main/NuGet.Config` passed. | Port narrower than the check it ports | The skill's list has no root qualifier; I read the bare names as root files. Case did not come up, because every fixture was spelled as the repo spells it. The drift test compared the two lists' names, not how they match. | Every entry matches at any depth and in any case; the skill says so in step 3. Test: eight case and nesting variants stop. |

## Root-cause pattern

All three are one pattern: **a manual check ported to code came out narrower than the check, and the
tests were built from the port's assumptions, not from real input.** The skill's checks are broad
because a person applies them with judgement; the port turned each into a predicate and narrowed it
where the narrowing looked harmless. The fixtures then encoded the same assumption, so the tests
proved the port agreed with itself.

This repeats a recorded cause: `testing-qa.md` "A rule stated by position is coded by position, and
its fixtures carry the shape every real input has" (2026-10-02) already says to build fixtures from
the shape every real input has. F1's fixture is that failure again.

## Why each lens caught or missed them

- **Tooling lens:** found all three. It ran `high_risk_paths` on adversarial inputs and fetched a
  real binary entry from the GitHub API.
- **Lens 5 Data flow:** found F2 and F3 by comparing the watcher with the skill line by line, and the
  `changes > 0` difference behind F1 from the rename side.
- **Second wave (Lenses 3, 4 and 6):** found F4 by fetching a real renamed binary, and showed with
  mutations that two of the first fixes had no test that guarded them (the fake `gh` dropped null
  fields and ignored the `--jq` projection). The fake now does both, and each fix has a test.
- **My own build pass:** missed them. I wrote the tests from the same assumptions as the code, and
  did not fetch one real response of each shape the code reads.

## Codex review (gpt-6-astra, xhigh, after the commit)

The maintainer then authorized a Codex pass over the PR at `b0960772a`
(`docs/reviews/codex-adversarial-pr-automation-2026-11-22.prompt.md`). It found two more HIGH
defects and six MEDIUM. All were checked against the code and fixed with tests first; each fix,
broken on its own in a copy, makes a test fail.

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| C1 | HIGH (Codex F1) | The file list and the diff came from `pulls/<N>/files` and `gh pr diff <N>`, which follow the PR's current head, while the contents came from the head the PR list named. A push between the calls paired one head's diff with another head's files, and the report named the first head. | Stale state: one snapshot read in three calls | I pinned the one read that took a `ref` and did not ask whether the other two could move. No lens raced a push against the run. | Every read names the listed head: the compare API (`compare/<base>...<head>`) gives the file list and the diff, checked on #1546 to equal the PR endpoints byte for byte. Test: every API call carries the head, and a `pr diff` call fails the fake. |
| C2 | HIGH (Codex F2) | The guard modelled an import as `tools/<name>.py` that trunk has. Python finds a package (`tools/_gamedir/__init__.py`) before that file, and `tools/` is first on the path, so a new `tools/json.py` replaces the standard library's `json`. Neither was guarded. | Port narrower than the mechanism it models | The same pattern as F1 to F4, one level down: I wrote the import rule from the files trunk has, not from Python's resolution order. | Every name a guarded script imports and every standard library name is guarded as a package folder or a file with any import suffix. Test: seven shadow shapes stop, and four ordinary tool paths pass. On trunk's 1,385 `tools/` files the new rule stops none that the old one passed. |

A Tooling lens re-review of those fixes found one more HIGH, there since the first commit:

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| C3 | HIGH (Tooling re-review 1) | `validate_moduledata.py`, which the pre-commit hook runs, runs or `exec_module`s five generators named as path strings in `check_generator_item_refs.py`. The guard followed imports only, so a PR that changed a generator was not stopped. | Port narrower than the mechanism it models | The closure modelled "what a hook runs" as "what it imports"; nobody traced a subprocess or a load by path. | The closure also follows `tools/` paths in string constants. Test: a script run by subprocess and one loaded by path stop. Trunk: 172 of 1,385 `tools/` files are now high risk, up from 64. |

The MEDIUM findings were fixed the same way: only the answer's first line can carry the verdict
(a quoted fake further down set it before), names that collide on a case-insensitive disk stop,
the folder uses the whole head SHA and is emptied before an export, gh lists up to 1,000 open PRs,
the lock is created with `O_EXCL`, and the CI tool floor counts executed tests, not skipped ones.

## Feedback to codify

None beyond the lessons entry: the existing fixture lesson covers the test half, and the new entry
covers the port half. C2 is the same port pattern; C1 adds one line to the lessons entry.
