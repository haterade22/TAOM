# context-mode adoption review (2026-10-26)

Review of [mksglu/context-mode](https://github.com/mksglu/context-mode) against TAOM's AI harness,
through `/adopt-external`, three days after the [rtk review](adopt-rtk-2026-10-23.md). Mike chose
the items below on 2026-10-26. Issues [#1002](https://github.com/haterade22/TAOM/issues/1002) and
[#1003](https://github.com/haterade22/TAOM/issues/1003).

## Coverage

- **Repository:** `main` at `7e9ebef` (599 files), npm package `context-mode` 1.0.169. Read through
  a shallow clone in the session scratchpad; nothing in it was installed or run. Read at source:
  `README.md`, `LICENSE`, `package.json`, `hooks/hooks.json`, `hooks/core/routing.mjs`,
  `scripts/postinstall.mjs` (by its calls), `hooks/ensure-deps.mjs`, and the executor test that the
  vet flagged.
- **TAOM:** every local transcript since 2026-01-01 (65,147 Read results, 458 M characters), and
  section 9 of `tools/harness_retro.py`.

## What context-mode is

An MCP server plus Claude Code hooks. The server's tools run shell or code in a subprocess and
return only what the script prints (`ctx_execute`, `ctx_batch_execute`), index text into a SQLite
FTS5 store and search it (`ctx_index`, `ctx_search`, `ctx_fetch_and_index`), and rebuild session
state from SQLite after a compaction. Its hooks route work to those tools: `curl`, `wget` and Java
build tools in Bash are replaced by a message that names `ctx_execute`; Read, Grep and other Bash
calls get a reminder; every Agent prompt is rewritten to carry its routing text. TypeScript,
Elastic License 2.0.

## Security and licence

- **Safe to learn from:** yes.
- **Safe to install into the harness:** no.
  - A shell command run through `ctx_execute` is an MCP tool call, so it skips every TAOM git gate:
    those are PreToolUse hooks matched on Bash and PowerShell, and the sandbox checks only the
    deny and ask rules in `settings.json`. It would reopen the bypass that #985 closed.
  - Its Agent hook returns `updatedInput` for every subagent spawn, adding its own routing text to
    TAOM's briefs.
  - `scripts/postinstall.mjs` repairs `~/.claude/plugins/installed_plugins.json` and the enabled
    plugins in settings; `hooks/ensure-deps.mjs` runs `npm install` from a hook during a session.
  - Hooks on SessionStart, UserPromptSubmit, PreCompact, Stop and PostToolUse for almost every
    tool: the always-on surface the adoption procedure calls prompt injection by design.
  - `ctx_execute` passes the user's environment variables into the sandbox by default
    (`tests/executor.test.ts` asserts it).
- **Licence:** Elastic License 2.0. Local use and derivative works are allowed; offering it as a
  hosted service and removing licence functions or notices are not. Nothing was copied, so no ELv2
  text or code is in TAOM.
- **Automated vet:** `python tools/audit_claude_config.py --root <clone> --external` scanned all
  599 files: CRITICAL 2, HIGH 164, MED 9, LOW 1. The two CRITICAL rows are a fake Slack token in
  `tests/executor.test.ts`. Of the HIGH rows, 75 network calls sit in tests and the built bundle,
  57 are `rm -rf` strings in permission tests, and the 3 credential reads are comments in code that
  blocks `~/.ssh` paths.

## Measurements

Read output since 2026-01-01, over every transcript, each result counted once:

| Measure | Value |
|---|---|
| Whole-file reads | 72% of Read characters |
| Reads over 20 KB | 35% of Read characters, 4,909 reads |
| `.cs` / `.md` files | 46% / 36% of Read characters |
| Re-reads of a file unchanged since its last read | 0.3% of Read characters |
| Largest single file | `.claude/skills/deep-review/lenses/5-data-flow.md`: 331 whole reads, about 29 KB each, 2.1% |

So the session-memory and caching half of context-mode would save almost nothing here: TAOM
sessions rarely read the same unchanged file twice. The output is spread over many files; the
largest levers are files every session reads whole, such as review lenses and path rules. In the
week to 2026-10-26, `5-data-flow.md` was 3.4% of Read output, `csharp-architecture.md` and
`moduledata-validation.md` 1.9% each.

## Decisions

| # | Item | Decision |
|---|---|---|
| A1 | Show which files the Read tool spends output on | Adopted (#1002): section 9 of `tools/harness_retro.py` gains "Top files read" with a growth check against the previous run, so the weekly retro sees it without a manual step |
| A2 | Slim the review lens files | Done (#1003): `5-data-flow.md` 28,949 to about 19,800 bytes, history replaced by `Source:` pointers and checked by an independent comparison; its check 10 no longer asks for `<DependedModule>` rows (standing decision of 2026-09-21); `deep-reviewer.md`, the XML lens and the agent operating manual stop reading a `paths:` rule that already loaded, the larger share (about 270 reviewer reads of two rules in October); memory-file pointers removed from two lenses |
| A3 | "Think in code": a script prints the result instead of files being read into the conversation | Adopted as item 6 of `docs/ai-includes/agent-operating-manual.md` |
| X1 | Install context-mode, its MCP server or its hooks | Declined: bypasses the git gates, rewrites agent briefs, installs at run time |
| X2 | SQLite session continuity after compaction | Declined: re-reads are 0.3%; TAOM has PreCompact and PostCompact hooks and memory resume cards |
| X3 | FTS5 knowledge base for docs and fetched pages | Declined: Grep and `doc_graph.py` cover the repo, WebFetch the web |
| X4 | `curl` and `wget` redirection | Declined: TAOM sessions use WebFetch |
| X5 | Search throttling and the per-tool reminders | Declined: no measured cause in TAOM |

## A TAOM rebuild, if one is wanted later

Mike noted on 2026-10-26 that the system could be rebuilt for TAOM's machines. A rebuild is
allowed (the ideas are not under licence; only copied code would be), but it would have to:

- run commands through the Bash or PowerShell tool, never an MCP tool, so the git gates still judge
  them;
- keep the full output on disk under the session scratchpad and print only the result, which is
  what plans 008 and 010 already ask for large `dotnet test` output;
- name a measured target first: section 9 now shows which families and files grow, and a rebuild
  earns its cost only against one of them.

---
