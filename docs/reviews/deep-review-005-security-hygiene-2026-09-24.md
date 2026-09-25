# Deep review: plan 005, security hygiene (improve/005-security-hygiene)

```
DEEP REVIEW REPORT
===================
Feature: plan 005, security hygiene (June port: 4310aa6e + 4bc520a1 only)
Date: 2026-09-24
Commit reviewed: 6b34fd00 (diff a39a9c86..6b34fd00, 3 files: tools/process_faction_map.py,
  docs/ai-includes/external-repo-adoption.md, CHANGELOG.md)

Scope:   scripts (one Python tool), docs (one checklist line, one CHANGELOG entry)
Waves:   one wave: Data flow (5), Tooling correctness, Efficiency (3), Completeness (4),
         Design (6). Codex gpt-6-astra ultra in parallel (complete: "END OF CODEX REVIEW").

STANDARDS:     NOT IN SCOPE as a lens (no C#); the one standards defect (an em dash on the new
               checklist line) was raised by three lenses and is fixed
COMPATIBILITY: NOT IN SCOPE (no engine API, no C#, no XML)
EFFICIENCY:    PASS, 0 issues in the changed hunks (3 FOLLOW-UP in pre-existing code)
COMPLETENESS:  INCOMPLETE at 6b34fd00: no regression test (fixed), false CHANGELOG claim
               (fixed), no GitHub issue (NEEDS MIKE)
DATA FLOW:     FAIL at 6b34fd00: 2 gaps, 3 inconsistencies in scope (all in the doc and
               CHANGELOG hunks; fixed except the latent DR3 copy path, FOLLOW-UP)
DESIGN:        4 KEEP proposals (1 apply, 3 follow-up)
XML:           NOT IN SCOPE
TOOLING:       FAIL at 6b34fd00: 1 HIGH, 2 MEDIUM, 1 LOW (all fixed)
```

## The code change is correct

Every lens and Codex agree, and I re-ran the proof this session:

- Both child scripts now read their paths and numbers from `sys.argv`; the three doubled-brace
  f-strings inside the child sources (`tools/process_faction_map.py:189, :258, :381`) are single
  braces now. Lenses rendered old and new children and found they differ only in the import line
  and the trailing call.
- The port is byte-faithful to June's `4310aa6e` (same blob) and `4bc520a1` (same doc line).
- New test `tools/tests/test_process_faction_map.py`, run by me: against the `a39a9c86` tool it
  fails 2 of 3 (`test_quote_in_folder`, `test_python_text_in_folder_is_never_run`:
  `None != (3, 2, 7, 6, 16, 12)`); against HEAD all 3 pass.

The defects were in the two prose hunks and in the verification behind them.

## Finding classification (lenses and Codex, each re-verified this session)

| # | Source | Sev | Finding | Verdict | Evidence I ran | Action |
|---|---|---|---|---|---|---|
| 1 | Tooling 1, Data flow 2, Completeness 2, Efficiency O2a, Design | HIGH | The new checklist grep (`external-repo-adoption.md:25`) reports clean on TAOM's real vendored drops, which are `.tar.gz` archives | CONFIRMED | In `E:\repos\TAOM\Dependencies\.vendor-source` (six `.tar.gz`, nothing extracted) the documented grep exits 1. Streaming each archive through `grep -cE` gives 3 marker lines in butterlib-v2.10.4, mcm-v5.11.4 and uiextenderex-v2.13.2, 0 in the other three. Values never printed. | Fixed: the line now adds an archive loop (`tar -xzOf ... \| grep -qE ...`), which I ran: it lists exactly the three BUTR archives |
| 2 | Tooling 2, Data flow 1, Completeness 1, Efficiency O1, Design, Codex 3 | MED | CHANGELOG:17-18 and the 6b34fd00 commit body say the vendored credential "is already gone from disk" | CONFIRMED (Codex rated it UNVERIFIED P3; it is false) | Same archive counts as #1. The commit body's check ran in the worktree, where `Dependencies/.vendor-source` does not exist (`ls`: no such file; `.gitignore:151` ignores it), and a plain grep cannot read gzip. `plans/README.md:40` already said the credential sits in three tarballs. | CHANGELOG corrected. The commit body cannot be fixed without rewriting 6b34fd00: NEEDS MIKE |
| 3 | Tooling 3, Completeness 3, Data flow notes | MED | The injection fix shipped with no regression test; the plan called a render run "structurally untestable" | CONFIRMED | `tools/tests/` had no module importing the tool. The helpers write only to paths they are given, so a synthetic PNG in a temp folder tests them safely. | Fixed, test first: `tools/tests/test_process_faction_map.py` (RED on a39a9c86, GREEN on HEAD) |
| 4 | Tooling 4, Completeness 4, Efficiency O2b, Data flow notes | LOW | The added checklist line contains an em dash (U+2014) | CONFIRMED | Byte grep of line 25 at 6b34fd00: 1 hit | Fixed in the rewrite; the added lines of this branch now hold 0 em or en dashes (byte grep) |
| 5 | Data flow 5, Tooling 4, Efficiency O2c, Design | LOW | "harvest SEC-02" is ambiguous | CONFIRMED | `plans/_audit/2026-06-12-harvest.md` has `[SEC-02]` at :77 (signature scanner) and :102 (this credential) | Fixed: the line links plan 005 and names "the second `[SEC-02]`" in that file |
| 6 | Data flow 3, Design | LOW | The backslash-pipe alternation works in GNU grep but is a literal pipe in ripgrep, so the pattern pasted into the Grep tool reports clean | CONFIRMED | Grep tool on `plans/005-security-hygiene.md`: backslash-pipe form 0 matches, plain `-E` alternation 16 | Fixed: the line uses `grep -E` with plain alternation and says why |
| 7 | Data flow 4, Completeness F1, Design (gate) | LOW, latent | `docs/migration/dr3-execution-handoff.md:63, :67` copies the three credential-bearing BUTR `src/` trees into the tracked tree without routing through the adoption checklist | CONFIRMED as latent; outside the diff | Lens evidence; `Dependencies/ThirdParty` does not exist today, so nothing is exposed | FOLLOW-UP (with the audit rule, FU6) |
| 8 | Completeness | MED | No GitHub issue for plan 005 (sprint-wide: 001, 002, 003, 005) | CONFIRMED | Lens ran `gh issue list`; the plan requires one (`plans/005-security-hygiene.md:26`) | NEEDS MIKE: filing is public |
| 9 | Codex 1 | P3 | The plan calls `py_compile` read-only (it writes a `.pyc`) and relies on it, but the child programs are string literals the outer compile never checks | CONFIRMED, pre-existing plan text (`plans/005-security-hygiene.md:345-359`, not in the diff) | The children sit inside `"""..."""` literals passed to `-c` (`:161-261`, `:284-385`) | The behaviour check Codex asks for is finding #3's test. Plan text: FOLLOW-UP |
| 10 | Codex 2 | P2 | The branch does not deliver MCP pinning | FALSE POSITIVE as a defect | Moved to plan 016 by decision 17 (`plans/016-repo-hygiene-pins-readme.md:41`); Codex itself calls it a scope observation | None |

No finding was a false positive among the lens reports.

## DETAILS

**Agent 5, Data flow.** 14 flows traced. In scope: traces 1 (false claim), 2 (archive gap), 3
(ripgrep dialect), 4 (latent DR3 copy path), 5 (SEC-02). Traces 6 to 11 connected: bbox and crop
argv paths, output parse, port fidelity, doc consumers, CHANGELOG cross-reference.

**Tooling correctness.** Findings 1 to 4 above. It proved the injection closes (a folder named
`a'+str(__import__('sys').stderr.write('INJECTED'))+'` ran on trunk, not on the port) and that
crops are byte-identical on plain, `{}`-named and non-ASCII folders.

**Agent 3, Efficiency.** No issue in the changed hunks: same two child processes per region, the
120 s timeout far from reach (slowest real call 0.9 s), identical crop hashes on three installed
fullres PNGs.

**Agent 4, Completeness.** INCOMPLETE: tests, CHANGELOG truth, GitHub issue. Commit message format
passes (65-character subject, v2.0.30 matches `SubModule.xml`, no trailer).

**Agent 6, Design.** Four KEEP proposals: the checklist line (APPLY, applied in substance), an
in-process or Pillow rewrite of both helpers, a `secret-nuget-cleartext` audit rule, and dead-code
deletions in the tool (all FOLLOW-UP).

**Codex (gpt-6-astra, ultra).** No implementation defect; one P3 plan defect (#9), one P2 scope
observation (#10), one P3 UNVERIFIED claim (#2). It missed the archive blindness because it
reviewed git objects only and could not see an ignored folder.

## ACTION ITEMS

1. Mike: decide whether to delete or repack the three BUTR tarballs, and whether to tell BUTR.
   Item A of plan 005 stays open until then.
2. Mike: the 6b34fd00 commit body repeats the false "gone from disk" claim; reword it at merge
   (squash) or accept the correction in the follow-up commit and CHANGELOG.
3. Mike: file (or waive) the GitHub issue for plan 005.
4. Orchestrator: `plans/README.md:40` row for 005 should read PARTIAL (item C landed, item A open,
   item B in plan 016).

## IMPROVEMENTS (Step 4)

APPLIED:
- `docs/ai-includes/external-repo-adoption.md:25`: Agent 6's checklist proposal, applied as the fix
  for defects 1, 4, 5 and 6. It uses `grep -E` plus a `tar -xzOf` loop instead of the proposed
  `rg -lza`, because `rg` is not on this machine's PATH (`which rg`: not found). Proof: the loop,
  run on the vendor folder, prints the three BUTR archives; on an empty folder it prints nothing.
- `tools/tests/test_process_faction_map.py` (new): Tooling 3's test, characterising the changed
  call sites. Plain folder passes on both versions; quote and Python-text folders fail on a39a9c86
  and pass on HEAD.

NOT APPLIED:
- None of the changed-code proposals was left out. No behaviour-changing improvement to changed
  code remained after the defect fixes, so no question to Mike was needed.
- Step 4.6 convergence pass: not run here (this delegate cannot spawn agents). The applied diff is
  one doc line, one CHANGELOG paragraph and one test file; the orchestrator may run it.

FOLLOW-UP (pre-existing code, no issue filed: filing is public and needs Mike's word):
- FU1 `tools/process_faction_map.py:265-273, 529-532, 574, 614`: a failed bbox child is reported as
  "EMPTY (fully transparent)", a failed crop still ends in "Done!", and the CLI always exits 0.
- FU2 `:204-253`: the bbox decoder does not refresh `prev_row` after a filter-0 row below row 0, so
  a later Up, Average or Paeth row decodes against a stale row (two lenses reproduced it; the census
  of the 46 installed fullres PNGs found no such sequence).
- FU3 `:13, :276-287`: `max_width` / `max_w` is never read; the promised 512 px cap never happens.
- FU4 `:505, :569-574`: `--output` equal to `--input` overwrites the originals and the `fullres/`
  backup on a second run.
- FU5: move both children in-process (or onto Pillow, already a sibling dependency). Deletes the
  code-in-a-string class, the stdout protocol, FU1 and FU2. Behaviour-changing on failures.
- FU6 `tools/audit_claude_config.py`: no rule for a NuGet `ClearTextPassword`; add
  `secret-nuget-cleartext` with a fixture test, and mirror the archive sweep into
  `docs/migration/dr3-execution-handoff.md` Phases B and C (finding 7). Plan 016 parks gate
  extensions the same way.
- FU7 `:603, :578`: `--old-factions` is parsed and never read.
- FU8 `:23, :138-147, :153-158, :281`: dead `read_png_dimensions`, stale System.Drawing and tkinter
  comments, unused `canvas_w, canvas_h`.
- FU9 `plans/005-security-hygiene.md:345-359`: correct "read-only" and the compile-only test plan
  (Codex 1).

## Tests

- `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=` (worktree, TEMP on E:):
  `Passed! - Failed: 0, Passed: 10313, Skipped: 2, Total: 10315`.
- `python -m unittest tools.tests.test_process_faction_map -v`: 3 passed on HEAD; 2 of 3 fail on
  the a39a9c86 tool.
- `python -m unittest discover -s tools/tests -t .`: 1886 run, 1 failure,
  `test_clan_heraldry_specs.ShippedSpecsMirrorTheLiveFiles.test_applying_every_spec_is_a_no_op`
  (`spclans.xslt would change`, LF against CRLF). Not caused by this branch: it touches neither
  `spclans.xslt` nor the heraldry tool, and the worktree checks that file out with CRLF
  (`git ls-files --eol`: `i/lf w/crlf`, `core.autocrlf=true`). Recorded, not fixed here.

## CODEX REVIEW

Codex gpt-6-astra at ultra, raw log
`docs/reviews/raw/codex-adversarial-005-security-hygiene-2026-09-24.md` (complete).

| # | Codex Severity | Your Severity | Agree? | Reason |
|---|---|---|---|---|
| 1 | P3 (plan: `py_compile` is not read-only and cannot check the child programs) | LOW | Yes | Both halves are true; pre-existing plan text. The new test is the behaviour check it asks for. Plan wording is FU9. |
| 2 | P2 (MCP pinning not delivered) | none | Partly | Codex itself calls it a scope observation: decision 17 moved it to plan 016. Not a defect of this branch. |
| 3 | P3 ("gone from disk" is UNVERIFIED) | MEDIUM | Yes, understated | It is false, not merely unproven: three archives hold the block (counted this session). |
| Suspects 1-10 | 4 DISPUTED, rest UNVERIFIED or no tracked hit | agree | Yes | Brace conversions correct, no auditor or `.gitignore` edit, no new state. History clean: `git log --all -- Dependencies/.vendor-source '**/nuget.config'` prints 0 commits; no `nuget.config` is tracked. |

- **Confirmed bugs:** #3 (CHANGELOG, fixed); #1 (plan text, FOLLOW-UP; its testing gap fixed).
- **False positives:** #2 as a defect.
- **Design questions:** none from Codex.
- **Things Codex missed:** the HIGH archive blindness of the checklist line (it reviewed git
  objects only, and the drops live in an ignored folder), the em dash, the ripgrep dialect, the
  ambiguous SEC-02, and the missing regression test as a finding (it noted "no tests were added"
  only under a suspect).

## AGENTS.md lessons (pending)

Not edited here; for the consolidated Phase 3h pass:
- Bugs Codex typically misses: facts about files in gitignored folders or on local disk (vendored
  archives, live installs). Codex reviewing git objects should mark such a claim UNVERIFIED and
  say which local check would settle it, as it did here, but it cannot find the defect.
- What Codex does well: it separated plan-quality defects from implementation defects and
  refused to call an unprovable disk claim true.
- False positive pattern: none new.

VERDICT: READY FOR COMMIT (defects fixed, final suite green; the four NEEDS MIKE items are
decisions, not code defects)
