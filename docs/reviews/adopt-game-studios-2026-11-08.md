# Reviewing Claude Code Game Studios (2026-11-08)

Procedure: `/adopt-external` ([external-repo-adoption.md](../ai-includes/external-repo-adoption.md)).
**Outcome: nothing installed, and four ideas taken in TAOM's own words.** They are two new items and one clause
in the `/deep-review` tooling lens, and a screenshot step in `/game-run`. No upstream text, hook, skill or agent came over.

## Source, and what was read

`Donchitos/Claude-Code-Game-Studios` at `05ad7c8a5b8aa2aa86bbd3439ae7cf7295cac418` (MIT, committed 2026-10-19,
638 files). It is a template that a developer clones to start a new Godot, Unity or Unreal game: 49 agents, 74 skills,
15 wired hooks and 14 rules.

| Depth | Files |
|---|---|
| Read in full or nearly in full | `.claude/settings.json`, `update-check.sh`, `notify.sh`, `log-instructions.sh`, `validate-agent-artifact.sh` |
| First part read | `read-guard.sh`, `review-receipts.sh`, `skill-lint.sh`, the README, `skill-authoring.md`, `run-and-observe.md`, `error-recovery-protocol.md`, `studio-tests/README.md`, four other rules (`ai-code`, `network-code`, `data-files`, `test-standards`), `/balance-check` |
| Header comment only | Every other hook and script |
| Frontmatter description only | The 74 skills, apart from `/balance-check` |
| Pattern sweep only | All 638 files |

**Not read:** the 49 agent bodies, the other 73 skill bodies, the other nine rules, `docs/engine-reference` and
the templates. The verdicts below on those groups rest on their names, descriptions and domain.

## Security

Safe to learn from: yes. Safe to install: not needed and not recommended, because it replaces
`.claude/settings.json` and wires 15 hooks across 8 events.

- The repo has no `package.json`, no install script and no `.exe` or `.dll`.
- The sweep found no hidden Unicode in the 638 files.
- `update-check.sh` is the only outbound call in the runnable files. At session start, and on by default, it sends one
  unauthenticated GET a day to GitHub's latest-release URL and writes a cache in the home folder. It sends no project data.
- Skills run a shell helper at load through a `!` backtick line, which is one more reason not to install them.
- `notify.sh` doubles only the straight single quote before it builds a PowerShell string. PowerShell also takes curly
  quotes as string delimiters, so a curly quote could end the string early. Claude Code writes that text, so the risk is
  low, and the file was not ported. This delimiter behaviour was not tested here.
- `tools/audit_claude_config.py --root <clone> --external` scanned 638 files, matching the tree, and exited 0 with
  2 HIGH and 5 MED. All seven were read and none is a real finding:

| Finding | Location | Why it is not a defect |
|---|---|---|
| HIGH fetch-and-execute | `setup-requirements.md:14` | The official Claude Code install line in a prerequisites table |
| HIGH prompt reveal | `story-facts.sh:20` | The usage comment "also print the rules each story trips" |
| MED skip confirmation (2) | a UI guidance template, `SECURITY.md:43` | Prose that argues against skipping confirmation |
| MED auto-approve (2) | `reverse-document/SKILL.md:382`, a test spec | "don't auto-execute" and a check that a skill does not auto-approve |
| MED chmod 777 | `settings.json:89` | A deny rule |

## Measured on TAOM's transcripts

Both numbers come from local transcripts modified in the 14 days before this review: 2,180 files, 5.04 GB.

- **A guard on large reads would save little.** The Read tool returned 17,579,303 characters. Whole reads of Markdown files
  of at least 40,000 characters were 750,684 of them, or 4.27%, and many were scratchpad or memory paths.
- **No silent-lens problem was found.** Of 751 `deep-reviewer` transcripts, 85 had no report-like final text. Of those,
  81 end on a tool result, which is the shape a handback or structured report leaves, and 75 of the 85 ran inside workflows. The
  parent's notice was read for two of the agents and says the report arrived by handback. The other four (three end on a user
  message, one on a short final turn) were not explained.

## Verdict by item

| Item | Verdict | Reason |
|---|---|---|
| A key with a reader and no writer has one real branch | Taken | Now item 17 of the [tooling lens](../../.claude/skills/deep-review/lenses/tooling.md) |
| A gate's hand-written list is silent about later additions | Taken | Item 18 of the same lens |
| An assertion that passes by accident (`unity` in `community`) | Taken | A clause on item 12 of the same lens |
| HUD screenshots read by the agent in `/game-run` | Taken | The autorun already took them ([fords-of-war.md](../features/fords-of-war.md)), and `/game-run` did not mention them. Phases 4, 5 and 7 now do, for the Fords runner. `tools/run_long_defeat.py` has the flag, but no PNG conversion was found in that file. A `[[human]]` row stays a person's to check. |
| 49 agent hierarchy, director gates, sprint, story and GDD skills | Skip | Studio process for a new game. The README reports that its 30-document build ranked last against a one-page brief. That is upstream's claim and was not checked. |
| Godot, Unity and Unreal rules, specialists and engine reference | Skip | Wrong engines. |
| `studio-tests`, `/skill-test`, `/skill-improve`, `skill-lint.sh` | Skip | 123 specs for a model to read. `test_claude_config_lint.py` and `harness_retro.py` cover what TAOM has needed. |
| `validate-agent-artifact.sh` | Skip | It needs a named output file, which TAOM lenses do not use, and the measurement above found no problem to solve. |
| `validate-read.sh`, `read-guard.sh` | Skip | 4.27% at most, and each refusal costs a turn. |
| `review-receipts.sh` | Skip | TAOM ties reviews to an exact commit. Content hashes solve a problem of uncommitted review baselines. |
| `run-and-observe.md` | Skip | The autorun covers it. The remainder is the `/game-run` step above. |
| `log-instructions.sh` | Skip | Its claim that creating a file fires no path rule conflicts with [harness-facts.md](../../.claude/rules/harness-facts.md) (Write and Edit since v2.1.288), and upstream's own `settings.json` does not wire it. |
| A "could not assess" verdict value | Already done | `UNVERIFIED`, `UNCHECKED`, `NOT_SEEN` and `NOT REACHED`. |
| `/balance-check`, `/soak-test`, `/playtest-report` | Skip | `analyze_*_balance.py` and the Fords round robins. |
| `flaky-stats.sh`, `/test-flakiness` | Skip | TAOM has no CI, and `docs/reviews/lessons` mentions a flake once. |
| Update check, notify, session logs, commit and push hooks, deny lists | Skip | `upstream-watch.sh` and TAOM's own git gates. |

**Review of the port.** The maintainer approved an inline review in place of the `Workflow` find and verify pass, because the
change is about four lines of lens text. Items 9 and 12 were read first to avoid restating them. The `/game-run` step was added
later on the maintainer's request, after the runner's flag and PNG conversion were read.
