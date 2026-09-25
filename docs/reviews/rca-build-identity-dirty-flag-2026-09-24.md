# RCA: plan 017, build identity and the dirty-tree flag (2026-09-24)

## Top-line

Branch `improve/017-build-identity-dirty-flag`, diff `a39a9c86..ff84e1b8` (#658), reviewed by
seven `/deep-review` lenses (standards, engine, data flow, tooling, efficiency, completeness,
design) and one Codex adversarial pass (gpt-6-astra, ultra). The crash-report half is sound: the
field is wired through every renderer and the positional fallback cannot shift. **The release
half was not: the new `--require-build` gate failed open four ways** (it read one of four shipped
DLL copies, matched module names case-sensitively, skipped on an empty value, and certified a set
from which a requested module had silently dropped), and it read a missing `.dirty` flag as proof
of a clean tree even for commits that could never write one. The stamp itself trusts a
configurable `git status` default. Report:
`docs/reviews/deep-review-017-build-identity-dirty-flag-2026-09-24.md`.

The root: **the gate was written to check the input the plan named, not the output the packager
produces.** Every fail-open case is a difference between "what the gate reads" and "what ships",
and nothing in the plan or its tests put the two side by side.

## Findings + Root Cause Table

| # | Sev | Bug | Category | Why Missed | Preventive Action |
|---|---|---|---|---|---|
| 1 | HIGH | Gate reads only `bin/Win64_Shipping_Client/<dll>`; three other shipped copies go unread (real patreon package: a Dependencies server copy from a third commit) | Logic error | Plan prescribed a fixed path per module; the executor copied it; tests built only the Win64 folder | Gate reads the module's own copy list; lesson "A release gate reads what the packager ships" |
| 2 | HIGH | `--modules taom` names the plan "taom" and the case-sensitive lookup skips it | Logic error | Windows path resolution was not considered; names came from argv, not the folder | Casefolded keys; same lesson |
| 3 | HIGH | `--require-build ""` disables the gate | Missing null guard | `if args.x:` idiom for an option whose empty value must still refuse | `is not None`; same lesson |
| 4 | MEDIUM | A requested module missing from `--source` is dropped before the gate | Missing null guard | The gate took the post-planning set, already filtered | Requested names passed in; same lesson |
| 5 | MEDIUM | A clean-looking stamp from a commit without the dirty-flag target passes | Logic error | Absence of `.dirty` was read as evidence of cleanliness without asking whether the producer could write it | Gate checks the target exists at the rev; lesson "A missing marker proves nothing unless the producer could have written it" |
| 6 | MEDIUM | `git status --porcelain` honours `status.showUntrackedFiles=no` | Assumed an API worked a certain way | The plan's probe ran on default config only | Fix proposed (hook-protected file, NEEDS MIKE); lesson "Pin every git option a gate's answer depends on" |
| 7 | MEDIUM | Trunk's `GameReferences.targets` sits outside the pathspec after merge | Other: base drift | The branch was cut before trunk added the file; no merge-tree check of the pathspec | Folded into the #6 edit; the props comment now says new imported inputs join the pathspec |
| 8 | MEDIUM | The gate proves the DLLs only, while the additive install ships orphans | Other: overclaimed guarantee | The docs promised "clean build of the tag" for the whole package | Caveat plus `git ls-tree` comparison in Phase 8 and step 8; automated check is a follow-up |
| 9 | MEDIUM | The 1.4.5 release line has none of this | Other: second release line | Plan scoped one branch | Gate fails closed there (#5); port is Mike's decision |
| 10 | LOW | Stale Bannerlord.BuildResources attribution left in the same file and its test | Other: incomplete propagation | The fix corrected the summary it was editing and did not grep the claim | Comment fixed; lesson in testing-qa |
| 11 | LOW | Step 1 offers a worktree of a branch that is checked out elsewhere | Assumed an API worked a certain way | The sentence was written without running `git worktree list` | Reworded to "stop"; one-off |
| 12 | LOW | Test name without its state segment | Convention inconsistency | Pasted from the plan | Renamed; no rule (`tests.md` already says it) |
| 13 | LOW | Release skill description omits Phase 8 | Convention inconsistency | Description treated as fixed text | Rewritten; no new rule (`external-skill-ports.md`) |
| 14 | LOW | #658 not referenced | Convention inconsistency | The plan named the issue only in prose | Added; no new rule (`completion-workflow.md:40`) |
| 15 | LOW | "the only link" claim now stale | Other: incomplete propagation | Same as 10 | Fixed |
| 16 | LOW | No crash-report changelog line | Convention inconsistency | Only the Identity table row was in the plan | Added |
| 17 | LOW | CLI tests error outside git; gate branches untested | Other: test coverage | Tests written for the happy and one-refusal paths | 11 tests added; skip outside git |
| 18 | LOW | Unreadable DLL exits 1 with a traceback | Missing null guard | `read_bytes` assumed to succeed | `cannot read` refusal |
| D1 | MEDIUM | The orphan-removal step compared the whole install with `Main/_Module`, which lists no `bin/` build output (convergence 1) | Logic error | The step was written from what git tracks; nobody listed what a deploy puts in the install | Compare outside `bin/`; the rest of that fix was wrong and D-A and D-B replace it |
| D2 | LOW | The shallow-history skip in `test_refuses_a_commit_that_predates_the_dirty_flag` could never fire (convergence 1) | Assumed an API worked a certain way | `git rev-parse <root>^` was assumed to fail with empty output; it exits 128 and echoes the argument to stdout | The test resolves the parent with `pr.resolve_commit` (`--verify --quiet`); proved in a `--depth 1` clone |
| D3 | LOW | SKILL.md and CHANGELOG said the gate refuses any requested module missing from `--source` (convergence 1) | Other: overclaimed guarantee | The prose described the intent, not `require_build`, which refuses only names in `SHIPPED_DLLS` | Wording now matches `package_release.py:176-178` |
| D-A | HIGH | The D1 fix pruned `TAOM.Dependencies` outside `bin/` against `Dependencies/_Module/`, which would delete 55 install-only MCM assets; the TAOM half also swept `RuntimeDataCache/` (final convergence) | Other: wrong premise carried forward | D1 counted "never compared `TAOM.Dependencies`" as a defect. Nobody walked the live module or read `module-dependencies.md:825-829`, and the same report's follow-up (an MCM allow-list) already contradicted it | Prune `Modules/TAOM/` only, leave `RuntimeDataCache*`, never prune TAOM.Dependencies outside `bin/`; a read-only walk of the live install proved the rule leaves exactly 12 TAOM leftovers |
| D-B | MEDIUM | "Leave `bin/` out" rested on `bin/` holding only DLLs git never tracks (46 are tracked), and three retired BehaviorTree DLLs would ship (final convergence) | Other: wrong premise | The claim was written from memory of `.gitignore`, not from `git ls-files` | `bin/` keeps tracked names plus what the build writes (from `obj/project.assets.json` and the `bin/Debug/net472/` output); the walk found exactly those three DLLs to remove |
| D-C | LOW | The props comment named ignored and skip-worktree paths but not assume-unchanged ones (final convergence) | Other: incomplete propagation | The comment listed the exclusions its author had in mind; the change's own probe relied on assume-unchanged | Comment now names both |

## Root-cause pattern

Findings 1 to 5 share one blindness: the gate was designed from its inputs outward (a module name,
a fixed path, a rev string) instead of from the packager's output inward (every file in
`_copy_list`, every requested module, every commit a rev can name). A gate is only as strong as
the smallest set it reads; each of these was a set smaller than the one that ships. Findings 10 and
15 are the second, familiar pattern: a corrected fact left standing elsewhere (the plan 016 RCA
records the same shape).

The convergence findings repeat the pattern one level down. D1, D-A and D-B each wrote a prune
rule from what git tracks, when what legitimately sits in the install is three sets: tracked
files, build output, and vendored MCM assets that only the install holds. D-A and D-B were fixes
to D1; each rested on a premise that one `git ls-files` or one read-only walk of the live install
would have refuted.

## Why each agent missed these

- **Executor (plan 017):** followed the plan's prescribed `SHIPPED_DLLS` map
  (`plans/017-build-identity-dirty-flag.md:873-877`), its `require_build(plans, rev)` signature
  (`:918-938`) and its git command (`:675`). All three carried the defects.
- **Agent 1 (Standards):** its checklist is conventions and ADRs; it caught the doc, naming and
  description LOWs and correctly left the gate's logic to the tooling and data-flow lenses.
- **Agent 2 (Engine):** found finding 1 by reading `Module.LoadSubModules` (`bin/<ConfigName>`),
  and finding 6 as an out-of-lens observation. It does not exercise CLI argument edge cases.
- **Agent 3 (Efficiency):** found finding 6 through the cost of `status.showUntrackedFiles`; the
  gate's logic is out of lens.
- **Agent 4 (Completeness):** found 7, 9, 14 to 17. Its lens checks artefacts, not the gate's
  truth.
- **Agent 5 (Data flow):** found 5, 8 and the propagation LOWs, and 1 as LOW (it weighed the copy
  mismatch below the orphan-file gap). It traced flows, not argv.
- **Agent 6 (Design):** found 1 as a KEEP proposal.
- **Tooling lens:** found 1 to 3 by running synthetic CLIs; missed 4 (it saw the related
  overclaim, F9, but read it as an output problem) and 5.
- **Codex:** found 1, 4 and 6; missed 2, 3, 5 and the doc findings. It ran nothing, so the argv
  edge cases stayed invisible.

## Feedback memories to codify

Three systemic lessons, appended to the category files:
- build-tooling-workflow: "A release gate reads what the packager ships, and fails closed on
  every input it did not see" (1 to 4).
- build-tooling-workflow: "A missing marker proves nothing unless the producer could have written
  it" (5), and "Pin every git option a gate's answer depends on" (6).
- testing-qa: correction to the #371 parser lesson (10): the SHA suffix is the .NET SDK's.
