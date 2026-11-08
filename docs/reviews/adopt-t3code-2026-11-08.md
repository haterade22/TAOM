# Reviewing pingdotgg/t3code (2026-11-08)

Procedure: `/adopt-external` ([external-repo-adoption.md](../ai-includes/external-repo-adoption.md)).
**Outcome: nothing taken.** No app, skill, rule or text came over. Two ideas were tried against TAOM's own record before
any edit, each with a pass condition set in advance, and both failed it.

## Source, and what was read

T3 Code is an MIT TypeScript application: a web, desktop and mobile GUI that drives coding agent CLIs (Claude Code, Codex,
Cursor and others) through a local WebSocket server. It is a product, not a skill pack, and only its agent instructions,
its `evals/` folder and its test rules sit near TAOM's harness.

Read through the GitHub API at `ade473018d32003658b7d38693e083ee2e314bd8`. The tree has 31,780 files, most of them in
apps and in the vendored `.repos/` references. Nothing was cloned, installed or run.

| Depth | Files |
|---|---|
| Read, with long lines cut at 400 to 600 characters | `AGENTS.md`, `CLAUDE.md`, `.pnpmfile.cjs`, `package.json`, `t3.json`, `.env.example`, `evals/README.md`, `docs/user/permission-modes.md`, `.macroscope/check-run-agents/test-signal.md`, `.macroscope/approvability.md`, `.vite-hooks/pre-commit`, the "What to test" section of `docs/internals/testing.md` |
| Read in part | `scripts/install.ps1` (first 95 lines and the checksum lines), `AnalyticsService.ts` (first 130 lines), `Identify.ts` (first 70 lines), `docs/user/outside-agents.md`, `contribution-triage/SKILL.md`, two lint rules, `pnpm-workspace.yaml` |
| Listed only | `oxlint-plugin-t3code/rules`, `scripts/`, the telemetry folders |
| Not read | `.repos/`, the application source |

## Security

Safe to learn from: yes. Safe to install: not recommended, and TAOM has no need for it.

- **Telemetry is on by default.** `AnalyticsService.ts` defaults `T3CODE_TELEMETRY_ENABLED` to true. Events go to
  `ingest.t3.codes`, and a comment there says the ingest forwards them to PostHog.
- **It reads agent account ids.** `Identify.ts` declares schemas for the Claude `userID` and `oauthAccount.accountUuid`
  and for the Codex `id_token`. A type comment says owner ids are passed through `hashTelemetryId`.
- **New threads start in Full access** (`docs/user/permission-modes.md`), which allows commands and edits with no prompt.
- **The installer is `irm | iex`.** `install.ps1` compares SHA256 against a `SHA256SUMS` fetched from the same release
  location, so the check catches a damaged download and does not prove who built it. `T3CODE_RELEASE_BASE_URL` can
  point it at a mirror.
- **Install scripts:** the root `prepare` runs `vp config --no-agent`, and `pnpm-workspace.yaml` allows build scripts for
  `esbuild`, `node-pty`, `sharp` and one local tarball. No `apps/*` or `packages/*` manifest has a lifecycle script (the
  `"install": {` match in `apps/mobile` is an object key).
- `.env.example` holds identifiers it describes as public, such as a Clerk publishable key.

**Not covered:** `tools/audit_claude_config.py --external` did not run, because it needs a local copy and no skill text was
ported. That check is UNCHECKED, and any later port of T3 skill text has to run it first.

## Verdict by item

| Item | Verdict | Reason |
|---|---|---|
| Reverse states ("If you added a way in, add the way out") | Tried, not added | See below. |
| Test placement ("Extend before you add", "Test at one layer") | Tried, not added | See below. |
| Eval method for model-facing text | Not now | A case that fails on the old text and passes on the new, control cases, several samples and a baseline on one model would give `/harness-retro` something better than observation. It needs paid calls and a runner, and no rule change is waiting on it. Worth raising when a rule fails twice. |
| Rule against killing a process by name | Mike's word | See below. |
| The app, its MCP server, remote control | Skip | Security above, and out of domain. |
| Suppression reason lint | Skip | `Main/` has two `#pragma warning disable` lines, so a gate is a tiny win. |
| Low-value comment and em dash lints | Skip | TypeScript rules. `/deep-review` and `lint_docs.py` already cover the review side. |
| Sleep and timeout test rules | Skip | 36 `Thread.Sleep` or `Task.Delay` lines sit in 7 test files, whose names are all diagnostics, profiler and hitch probes (bodies not read). A `Skip = "` search of `TAOM.Tests` found none, and lens 4 item 8 flags a new `[Ignore]`. |
| Human review of a changed product default | Already done | `check_mcm_default_flips.py`. |
| `contribution-triage` skill | Skip | It closes pull requests without approval for each one. `/review-outside-pr` fits TAOM. |
| "Do not document every feature", "Do not commit plans" | Skip | They conflict with TAOM's documentation duty and `plans/` backlog. |
| Live install rule, dev seeding, Replay provider, ports per worktree | Skip | Web server concerns. TAOM deploys to the live game on purpose. |

## The reverse states trial

The draft was a tenth item for `/deep-review` lens 4: name the exit for each state a change lets a hero, party, player or
save enter, and flag a state with no exit or with an exit that only some paths reach. The gap looked real, since
`gui-ui.md` covers a way out for screens and lens 3 covers event and static cleanup only. Lessons hold 16 lines that
match one-way state wording, which is a text match and not a bug count. The agreed condition was that the draft must
name the missing exit in the real cases, or it is dropped.

| Lesson | Does the draft reach it? | Who found it |
|---|---|---|
| `harmony-il.md:469`, popup latch never reset | No. The latch is in vanilla's `KingdomDecisionPopupWidget`, the change added no such state, and the defect shows only on the second binding | A player report (#550) |
| `state-lifecycle-save.md:799`, shore-leave latch | Yes | Four review lenses and Codex |
| `state-lifecycle-save.md:64`, emissary greeting flag | Yes | `/deep-review`, 2026-06-25 |

The draft misses the case that escaped review and fits the two that review already caught, so it fails the condition and
adds nothing the lenses do not do. The hero death control was not run, because the check had already failed. The fix for
`harmony-il.md:469` was read in part (`1571c2f91`). The shore-leave fix commit was not pinned down.

## The test placement trial

T3 asks reviewers for fewer and smaller tests, while lens 4 item 2 pushes only toward more. The condition was to add the
rule only if at least one of the tests in the last five test-adding commits repeated a case that another test or layer
already covers. The commits were `1c275b73f`, `ce5099189`, `ca32cb111`, `d679586f0` and `6b7c5f782`, with 103 added
`[TestMethod]` or `[DataRow]` lines.

Zero true repeats. Every test name was screened, and the bodies were read where names overlapped:

- `TheDamageReduction_MultipliesAPierceBlowOnCavalry` looks like a third layer over `MountedPierceRuleTests`, but it
  checks the wiring (which hook calls the rule, and that a match's end turns it off), and the rule tests check arithmetic.
- The two `Decide_RegroupingLane...AtTheReturnRatio` tests share a ratio, and one of them holds the minimum enemy count.
- The four `Tactics_..._AreDroppedWithOneWarning` tests could be one `[DataRow]` table. Each input takes a different
  validator branch, so nothing is covered twice, and folding them would only shorten the file.

**Limits:** all five commits are Fords of War work, `1c275b73f` and `ca32cb111` were judged by name only, and the body
of `Default_PierceVsMounted` was not read. Five commits from one feature cannot show the problem is absent elsewhere.

## Open: killing a process by name

T3's first listed hazard is killing by pattern, because a name match can hit the agent's own process or another dev server.
A search of TAOM's rules, hooks, skills and agent docs found no such line. One session transcript (`617ee2e3`) repeats `taskkill //IM blender.exe //F`, which would also close Blender
if Mike had it open, and `native-crash-triage/SKILL.md:136` stops `DbgX.Shell` by name. No loss is recorded, and the
transcript search hit its time limit, so the count is partial. Fixing it costs one clause in the AGENTS.md "Preserve
others' work" row and one line in that skill. It was left out as a guard against a failure that has not occurred
([simplicity-criterion.md](../../.claude/rules/simplicity-criterion.md)), and Mike can ask for it.
