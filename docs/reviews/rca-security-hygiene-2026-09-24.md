# RCA: plan 005 security hygiene review (improve/005-security-hygiene)

**Summary.** The code fix (faction-map child scripts read paths from `sys.argv`) was correct. Every
confirmed defect but one sat in the prose around it and in how it was verified (the exception is
process: no GitHub issue was filed for the plan, finding 8): the new credential check
cannot see inside the `.tar.gz` archives TAOM actually keeps, the CHANGELOG called the credential
"gone from disk" on the strength of that same blind check run in a worktree where the folder does
not exist, and the injection fix shipped with no regression test because the plan called a render
run untestable and relied on `py_compile`, which never compiles the child programs. Review commit
`6b34fd00`; report `docs/reviews/deep-review-005-security-hygiene-2026-09-24.md`.

## Findings

| # | Sev | Bug | Category | Why Missed | Preventive Action |
|---|---|---|---|---|---|
| 1 | HIGH | The checklist grep in `docs/ai-includes/external-repo-adoption.md:25` exits 1 on `Dependencies/.vendor-source`, whose six drops are `.tar.gz`; three hold a `packageSourceCredentials` block | Absence check blind to the data's real form | The June line was written against an extracted tree; the port copied it byte for byte without running it on the folder as it exists now | Line rewritten with an archive loop, run on the real folder; lesson "An absence check must run where the thing lives, in the form it is stored" |
| 2 | MED | CHANGELOG and commit body: the credential "is already gone from disk" | Claim from a check that could not fail | The check ran in the worktree, where the gitignored folder is absent, and with a grep that cannot read gzip; `plans/README.md:40` already said three tarballs hold it | CHANGELOG corrected; same lesson as #1; the commit body is a Mike decision (rewrite at merge) |
| 3 | MED | No regression test for the injection fix | Verification by compile only | Plan Step 8 and its test plan call `py_compile` sufficient and a render "structurally untestable"; the child programs are string literals the compiler never parses, and the helpers take explicit paths, so a temp-folder test was always possible | `tools/tests/test_process_faction_map.py` (RED on a39a9c86); lesson "Code carried in a string is only tested by running it" |
| 4 | LOW | Em dash on the added line | Prose rule | The port copied June's line verbatim; `reconcile.md` said to drop the dash, and nothing mechanical checks a doc line inside `docs/ai-includes/` for it at commit | Fixed; `lint_docs.py` reports dashes on new markdown lines, run it before committing a doc hunk |
| 5 | LOW | "harvest SEC-02" names two findings | Ambiguous reference | The harvest reused the ID; the line cited an ID instead of a path | Fixed: link plus "the second `[SEC-02]`" |
| 6 | LOW | Backslash-pipe alternation is literal in ripgrep, so the pattern reports clean in the Grep tool | Tool dialect | The command was written for GNU grep; Claude Code's preferred search tool is ripgrep | Fixed; lesson "Write a documented search pattern in `-E` form" |
| 7 | LOW, latent | `dr3-execution-handoff.md:63, :67` copies the credential-bearing `src/` trees without the checklist | Guard on the wrong path | The guard went into `/adopt-external`, but the vendored drops enter through the DR3 migration procedure | FOLLOW-UP FU6: audit rule `secret-nuget-cleartext` plus a line in the handoff |
| 8 | MED | No GitHub issue for plan 005 (sprint-wide: plans 001, 002, 003 and 005) | Issue-first mandate | `plans/005-security-hygiene.md:26` assigns filing to the orchestrator, so neither the executor nor the review filed one; four plans missed it the same way, a repeat under Step 3e item 4 | NEEDS MIKE (filing is public). Prevent: the orchestrator files or waives each plan's issue before dispatching its executor, and the executor's report states the issue number or the waiver |
| 9 | LOW | Plan says `py_compile` is read-only (it writes a `.pyc`) | Plan wording | Assumed, not checked against the Python docs | FOLLOW-UP FU9 |

## Root-cause pattern

Findings 1, 2 and 3 share one theme: **the verification could not fail.** A grep that cannot open
the file, run in a tree that lacks the folder, returns "no match" whether or not the secret exists;
a compile that treats the child program as a string passes whether or not the child works. The
plan's own "Done" criteria (`plans/005-security-hygiene.md` Step 1 and 2 greps) had the same blind
spot, so a faithful executor reproduced it. The sprint record (`plans/README.md:40`,
`reconcile.md:146-155`) already knew about the tarballs; the claim was written against the
executor's own check, not against that record.

This is a repeat of a filed pattern: `docs/reviews/lessons/build-tooling-workflow.md`, "A fix
verified against the artifact that motivated it cannot see the defect the fix introduces", and
`misc.md`, "A claim found wrong is wrong everywhere it was written". Both describe a check that
exercises the wrong object. The new lessons name the two concrete forms seen here.

## Why each agent missed these (at build time and in review)

- **The builder (plan 005 port).** Followed the plan's greps and `py_compile` literally, in a
  worktree. Neither check can report a hit there.
- **Codex.** Reviewed git objects only; the credential lives in an ignored folder, so it correctly
  marked the disk claim UNVERIFIED but could not find the archive blindness. It listed "no tests
  were added" under a suspect instead of as a finding.
- **Efficiency lens.** Out of its scope for 1, 2 and 6; it still reported 1 and 2 as outside-lens
  notes.
- **Data flow, Tooling, Completeness, Design.** All caught 1 and 2 by running the check on the real
  folder; nothing was missed in review.

## Feedback memories to codify

None beyond the three lessons appended to `docs/reviews/lessons/build-tooling-workflow.md`.
