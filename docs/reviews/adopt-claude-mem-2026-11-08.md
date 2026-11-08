# Reviewing claude-mem (2026-11-08)

Procedure: `/adopt-external` ([external-repo-adoption.md](../ai-includes/external-repo-adoption.md)).
**Outcome: nothing installed and no upstream text ported.** The review exposed one gap in TAOM's own
procedure, which now tells a reviewer to check [adoption-history.md](adoption-history.md) first.

Mike asked for updates on the assumption that TAOM had already adopted this repository. It had not.
`git grep` found no mention in tracked files, `git log --all -G` found none in the history of `docs`,
`.claude`, `tools`, `AGENTS.md` or `CLAUDE.md`, the memory folder had none, and the plugin is absent
from `installed_plugins.json`. This is a first review. The nearest record is
[Hindsight](adopt-ghidra-hindsight-2026-09-26.md), a similar memory plugin rejected on 2026-09-26.

## Source, and what was read

`thedotmack/claude-mem` at `3ab6e1a215c7bbcf716b9d8d763aa78bacba52a0` (Apache-2.0, committed
2026-11-07, package version 15.12.0, 3,113 files). It records what an agent does through hooks and a
background worker, compresses that with a model into a local database, and injects the result into
later sessions.

| Depth | Files |
|---|---|
| Read in full or nearly in full | `plugin/hooks/hooks.json` as far as `SubagentStop`, `package.json` to line 120 plus every lifecycle script, `plugin/package.json` to `allowScripts`, the release notes from v15.3.0 to v15.12.0 (first 700 characters each) |
| First part read | Skills `mem-search`, `smart-explore`, `handoff`, `agent-cost-report`, `pathfinder`, `community-sweep`; `.claude/commands/anti-pattern-czar.md`; `src/cli/handlers/file-context.ts`, `file-context-dedupe.ts`; `src/services/telemetry/consent.ts`; `src/utils/tag-stripping.ts`; `docs/public/telemetry.mdx`, `progressive-disclosure.mdx`; `.github/SECURITY.md`; `scripts/check-hook-io-discipline.cjs`, `check-spawn-env-discipline.cjs` |
| Pattern sweep only | All 3,113 files, by `audit_claude_config.py --external` |

**Not read:** the worker and search source in `src/`, the other 25 skill bodies, and the minified
bundles. `plugin/scripts/worker-service.cjs` (4.8 MB) and `server-service.cjs` (2.3 MB) exceed the
audit's text limit, so only its YARA layer read them. The verdicts on `make-plan`, `do` and `babysit`
rest on their names and the release notes.

## Security

Safe to learn from: yes. Safe to install: no.

- `hooks.json` registers hooks on 9 events. `PostToolUse` matches every tool, and `Stop` summarizes.
- Stored text comes back as context at SessionStart and before Read, Bash, Grep and Glob. Text from
  reviewed code or logs can return in a later session, which is the standing injection channel the
  Hindsight review rejected under AGENTS.md "Untrusted input".
- `file-context.ts` denies a whole-file Read of 32 KB or more (markdown, YAML, TOML and unknown types
  excepted). That changes how every session reads source.
- `consent.ts` ends its precedence chain with "Default: on", and `telemetry.mdx` confirms opt-out.
- The v15.8.3 release note says browser setup uploaded the full text of the last 30 days of prompts
  without asking, from v15.2.0 until that fix.
- The v15.7.0 release note describes about 230 hidden `codex exec` runs in four days on one owner's
  ChatGPT account (#5189), before that fix.
- Since v15.12.0, screenshots and images an agent reads are kept by default.
- `src/npx-cli/install/setup-runtime.ts:144` and `:182` pipe downloaded install scripts into a shell.
- `plugin/.mcp.json` starts the MCP server from an inline `node -e` script, and `plugin/package.json`
  allows native install scripts for its tree-sitter packages and others.
- Neither `package.json` has its own `preinstall`, `install` or `postinstall`. The root has only
  `prepublishOnly`, which runs at publish time, and `plugin/package.json` has no scripts.

**Automated vet.** `python tools/audit_claude_config.py --root <clone> --external` scanned 3,113 files,
the same count as `git ls-files`, and exited 2: 46 CRITICAL, 292 HIGH, 89 MED, 3 LOW, 5 INFO.

- The credential-class CRITICALs (Anthropic, GitHub, Google, private key) were read with their values
  masked. All 30 are in test files, as patterned values for redaction and token tests.
- The 16 `secret-db-uri` CRITICALs were checked by location only: tests, CI, `.env.example`, and
  redaction code and comments. One line in `request-groups.mjs` was read, and it is a comment.
- `secret-tracked-cred` on `.npmrc` is a false positive: the file holds one `legacy-peer-deps` key.
- 35 of the HIGH hits are in translated docs, and the largest test group is `tests/worker`. The HIGH
  hits were sampled outside `tests` and `docs`, not read one by one.

## Is there a need?

Measured on the 122 main-session transcripts active since 2026-10-08, read-only, with regex
classification. Subagent transcripts are not included.

| Question | Result |
|---|---|
| Distinct user prompts under 1,500 characters that look back at earlier work | 4, and none asked for a search of past sessions |
| Shell calls that search session transcript content | 76, in the classifier's coarse bucket. The sampled calls were harness diagnosis |
| Lesson file reads | 19 partial, 3 whole, so `grep -n '^### '` first is holding |

A first count of 139 look-back prompts was discarded. One repeated template sentence produced most of
them. Keyword matching can also miss a prompt that looks back in other words, so the result supports
"no visible demand", not "no value".

## Mapping against TAOM

| Upstream part | Verdict | Reason |
|---|---|---|
| Capture and injection core | Reject | ADR-011 keeps durable knowledge in the repo. Model calls on every session conflict with AGENTS.md "Paid AI dispatch" |
| `mem-search` three-step recall | Skip | No measured need. Lessons, MEMORY.md and graphify already go from index to detail |
| `smart-explore` | Skip | `plugin/package.json` lists no C# grammar, and graphify covers structure |
| File Read Gate | Skip | TAOM measured this kind of guard at 4.27% of Read output in the Claude Code Game Studios review of 2026-11-08 |
| `handoff` | Skip | Resume cards with dead ends cover it ([ralph review](adopt-ralph-2026-10-24.md)) |
| `anti-pattern-czar` | Skip | `SilentCatchRatchetTests` and `swallow-ok` do this with a ratchet |
| Hook output lint | Skip | `tools/test_hooks.sh` section 7k tests that advisories reach Claude |
| Spawn environment lint | Skip | No `claude -p`, `claude --print` or `codex exec` in `tools`, `.claude/hooks` or `/game-run`. `/review-codex` was not searched |
| `pathfinder`, `make-plan`, `do`, `babysit` | Skip | `/improve`, `agent-teams.md` and the evidence rules cover them |
| `community-sweep` | Skip | Its rule that nothing needs the owner conflicts with AGENTS.md |
| `agent-cost-report` | Open | Cost per completed outcome is a different cut from the tokens by agent and model in `harness_retro.py`. Whether a per-issue view exists was not checked. Not built without a request |

## Changed in TAOM

`external-repo-adoption.md` step 1 now starts with a search of `adoption-history.md`. Before this, a
first review and a re-review looked the same until someone searched. The idea came from this review,
not from upstream. Nothing was installed and nothing from the clone was run.
