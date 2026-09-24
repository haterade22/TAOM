# Cold review: plan 016 (repo hygiene, MCP pins, README)

Reviewed file: `plans/016-repo-hygiene-pins-readme.md` (863 lines). Template:
`.claude/skills/improve/references/plan-template.md`. Planned-at `b2e387db`; main-tree HEAD at
review time `4b5662b2` (same branch, `bannerlord-1.5.x`).

**Verdict:** one blocking defect (a verification and a done criterion that cannot pass as written);
otherwise the plan is unusually well grounded. Every "Current state" excerpt I opened matches
`b2e387db` byte for byte, and all four registry pins resolve today.

## What I verified this turn

- Drift check (plan line 20) from the main tree: empty output, exit 0. `b2e387db..4b5662b2` touches
  no `TAOM.Tests` file, so the test baseline is not moved by that commit.
- `git ls-files -s` lists the six tracked paths; `git cat-file -s` sizes match lines 100-102
  exactly (2,627; 37,216,114; 519; 93,658; 71,318; 324,291).
- `.gitignore` at `b2e387db` is 236 lines, LF, ends with the seven lines quoted at 107-115; no
  `pkl`, `crashz` or `settings.local` pattern; no `!` negation touching `.claude/`.
- `.claude/settings.local.json` lines 40-50 (deny), 51-55 (additionalDirectories), 57-65
  (enabledMcpjsonServers), `outputStyle`: match lines 140-164.
- `.claude/settings.json` lines 1-7 match 166-174; no `permissions` key.
- `.mcp.json` lines 5-12, 24-27, 36-37, 60-61 and `.codex/config.toml` lines 12-16, 25-27 match
  207-241.
- `audit_claude_config.py:418-421` matches 270-273; a main-tree run printed `HIGH:1  MED:1  INFO:7`
  on output line 3, HIGH `secret-generic` at `tools/tests/test_audit_repo_secrets.py:178`, MED
  `mcp-npx-unpinned` on `.mcp.json` `filesystem`.
- Registry lookups (Step 2 commands, run read-only): `949a27ef...6ffd refs/tags/v1.7.0`,
  `2026.8.31`, `2026.8.18`, `0.12.2`.
- Serena at `949a27ef`: `pyproject.toml` `version = "1.7.0"`; `cli.py:233-238` defines
  `start-mcp-server` with `--project` and `--context`; `context_mode.py:235-244` maps
  `ide-assistant` to `claude-code` with a warning. All as stated at 253-259.
- The three `crashz/report.json` citations (engine-bump `SKILL.md:29-30`, native-crash-triage
  `SKILL.md:74`, `v1.4.8-impact.md:136`) match; `git grep crashz` outside `plans/` finds only these.
  `git grep pickle|taom_loc` finds only `audit-playbook.md:35`.
- `development-machines.md:31`, `:40-41`, `:42` blank, `:43` heading; `mcp-servers.md:14`, `:23`,
  `:65`; `INDEX.md:178`: all match. The main tree's uncommitted `docs/INDEX.md` hunks are at 23 and
  57 only.
- README at `b2e387db`: 207 lines; every line quoted at 292-314 matches; `dotnet` appears only on
  line 58; `v1.4.8|v1.5.2` only on 3, 37, 175. After the Step 10 edits the version grep yields one
  line (new line 19) and `v1.5.3` appears five times.
- `agent-operating-manual.md:41-43` and `:57` match; no other `dotnet (build|test)` line in the file.
- `SubModule.xml:6` `v2.0.30`, `:32` `version="v1.5.3.*"`; pin file `v1.5.3`; `setup-dev-env.ps1:7`
  `Read-Host`, `:20` sets `BANNERLORD_GAME_DIR`; `Main/Features/` has 108 directories;
  `taom_careers.xml` parses to 67 `Career` elements (a 68th `<Career ` is inside a comment).
- `config-protection.sh:39-44` is the override check, `:54-58` the protected list;
  `check-claude-files-tracked.sh:60` is the `DIRS` array. `check-changelog-changed.sh` and
  `check-claude-files-tracked.sh` both `cd "$CLAUDE_PROJECT_DIR"` (the main tree), so the STOP at
  plan line 662-663 about hooks reading the main checkout is warranted. The main tree currently has
  nothing staged. `check-commit-subject-version.sh` reads `-F` files.
- Named baseline tests exist at `b2e387db` (`ElkConfigTests.cs:93`, `AnimaliaMountWiringTests.cs:127`,
  `WargAttackServiceTests.cs:350,373`).
- Plan prose has no U+2014 or U+2013; the only two occurrences (plan lines 307, 336) are verbatim
  excerpts inside code fences. No secret values; only the fixture's location is named.

**Not verified:** the 10,239 / 10,235 / 2 / 2 test baseline (lines 359-362; I did not run the
suite), the "269 serena checkouts" claim (line 58), and plan 011's settings.json line numbers
(line 852).

## Blocking

1. **Step 7 verify (plan line 567-568) and done criterion line 798 cannot pass as written.** The
   engine-bump replacement (lines 553-555) splits the citation across two lines: new line 29 ends
   `` the open player report `crashz/report.json` (untracked; read it with `` and new line 30 starts
   `` `git show b2e387db:crashz/report.json` ``. `git grep -n "crashz/report.json"` therefore prints
   **four** lines, not three, and line 29 does not contain `git show b2e387db:crashz/report.json`.
   The Step 7 check fails, and `... | grep -vc "git show b2e387db:crashz/report.json"` prints `1`,
   not `0`. A weak executor either STOPs on a correct edit or "fixes" the wording on its own.
   Fix: reflow the replacement so no line holds `crashz/report.json` without the `git show` form,
   for example line 29 `` is already booked: the open player report (untracked; read it with `` and
   line 30 `` `git show b2e387db:crashz/report.json`; `BannerlordVersion v1.4.7.117484`) can no `` then
   line 31 `longer be triaged against a local binary.`; or change the verify to expect four lines
   and grep for `git show b2e387db:crashz` per file.

## Non-blocking

1. **Step 13 and done criterion 805 use a two-dot diff** (`git diff --stat bannerlord-1.5.x..HEAD`,
   lines 768, 805). Another session is committing to `bannerlord-1.5.x` in the main tree; if it
   advances during execution, the two-dot tree diff also lists that session's paths (reversed) and
   "names only in-scope paths" fails spuriously. Use `bannerlord-1.5.x...HEAD` (merge-base form).
2. **Evidence rule vs. prescribed CHANGELOG text.** Lines 79-80 bind the executor to write only
   numbers it read this run, but the Step 9 and Step 12 entries (lines 612-632, 731-739) and the
   Step 12 commit body carry `37 MB`, `seven servers`, `50 against 67`, and the Step 12 body
   "several were wrong (50 careers vs 67)". No step produces those numbers. Add a Step 1 sub-step
   that prints them (`git cat-file -s HEAD:_taom_loc.pkl`, the `enabledMcpjsonServers` length, the
   `Career` element count via an XML parse, not `grep -c "<Career "`, which gives 68 because of a
   commented element), or say explicitly that these plan-quoted figures are exempt.
3. **Step 7 old-text indentation.** The quoted old text at lines 548-549 carries the plan's list
   indent plus the file's three spaces. The Edit tool needs the file's three-space indent only. Say
   "the file lines start with three spaces" as Step 7 does for line 74.
4. **Step 10 line numbers shift mid-step.** Item 2 deletes four lines and item 5 inserts one, so
   items 3 onward no longer sit at the quoted numbers. Items 3 and 2 give no old text inline (the
   executor must look back to the excerpt). Tell the executor to match by the quoted text, or apply
   the items bottom-up (16 to 1).
5. **`docs/features/moduledata-validation.md:686` becomes a dead link on every other clone.** Line
   195 lists it as "stays true, not edited", but it is a Markdown link to
   `../../.claude/settings.local.json`. `lint_docs.py` resolves links with `Path.exists()`, so it
   stays clean in the worktree (the file is on disk) and reports a dead link on a fresh clone; CI
   runs only `--fail-on-drift`, so nothing gates it. Either unlink it in this plan (add the file to
   Scope and the drift paths) or list it as a deferred follow-up.
6. **`plans/README.md` handling is split three ways.** Scope (line 382) lists it inside the worktree,
   the header (lines 6-9) says leave it alone when no 016 row exists, and done criterion 806 says
   "in the main tree only if the orchestrator told you to" while lines 13-15 forbid writing under
   `E:/repos/TAOM`. The worktree copy is the committed one and has no 016 row, and the main tree's
   copy is modified and uncommitted by another session. State one rule: "never edit it; report."
7. **Step 6 STOP "would remove anything beyond the six paths"** (line 825) asks for a prediction.
   Make it checkable: run `git rm --cached -r -n -- ...` first and expect exactly six `rm '...'`
   lines.
8. **Step 6 blank-line instruction is doubled.** Line 519 says "with one blank line before it" and
   the fenced block already opens with a blank line. Say "the block below already starts with the
   blank line".
9. **Step 9.1 "the archive note"** (line 605) is the `> **Archive:**` blockquote under the title;
   name it. `CHANGELOG.md` starts with a UTF-8 BOM; the Edit tool keeps it, a shell rewrite may not.
10. **Test total pinned to 10,239** (lines 352, 771) assumes the worktree base adds no tests. True
    for `4b5662b2`; "or later" (line 69) could change it. Phrase it as "failures limited to the two
    named tests" and report the total, which the STOP at 830 already covers.
11. **"Session scratchpad"** (lines 407, 637): a weak executor may not know the path. Give a concrete
    fallback, for example the scratchpad path from its system prompt, never Git Bash `/tmp`.

## Checklist (template "Quality bar" and TAOM additions)

| Item | Result |
|---|---|
| Self-contained | Yes, except the numbers in non-blocking 2 |
| Every step ends in a command with an expected result | Yes; Step 7 expected result is wrong (blocking 1) |
| Excerpts match `b2e387db` | All matched; no mismatch found |
| TDD order | N/A, no C#; stated at line 93-94 |
| Issue-first | Status line 35 names the orchestrator |
| Binding ADRs named | ADR-002/007/008 named as not engaged; AGENTS.md rules summarized |
| Single-owner files | Lines 95-96, 392-393, 832-833: not touched |
| STOP conditions specific | Yes (Step 0 gate, registry failure, hook denial, Armory tests) |
| Done criteria machine-checkable | Yes; criterion 798 fails (blocking 1), 805 fragile (non-blocking 1) |
| Planned-at SHA vs drift paths vs Scope | Consistent; CHANGELOG and plans/README exclusions explained |
| `-p:ModuleId=` on non-deploying commands | Yes, every dotnet command |
| No em or en dashes in prose; no secrets | Pass |
