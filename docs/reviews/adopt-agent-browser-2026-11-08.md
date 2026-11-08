# Reviewing vercel-labs/agent-browser (2026-11-08)

Procedure: `/adopt-external` ([external-repo-adoption.md](../ai-includes/external-repo-adoption.md)).
**Outcome: two ideas taken, nothing installed.** No tool, skill or text came over. TAOM is a C# game mod with no web UI, and
a tool that reads pages as an accessibility tree cannot see the Gauntlet game UI. On Mike's word, TAOM built its own
[skill evals](../features/skill-evals.md) from the two ideas below, on the harness's own eval command.

## Source, and what was read

Mike asked for `vercel-labs/agent-browser`, a Rust CLI that drives Chrome over the Chrome DevTools Protocol for AI agents.
The review used commit `4b0e9f01fa8d214d39bbfe5391257640634fb9a0` and release v0.41.2. The license is Apache-2.0, and the
last push was on 2026-11-08.

| Depth | Files |
|---|---|
| Read in full | `README.md`, `package.json`, `scripts/postinstall.js`, `bin/agent-browser.js`, `.claude-plugin/marketplace.json`, `skills/agent-browser/SKILL.md`, `skill-data/core/SKILL.md`, `skill-data/core/references/trust-boundaries.md`, `skill-data/dogfood/SKILL.md`, `docs/content/docs/security.mdx`, `evals/README.md` |
| Part read | The start of `issue-taxonomy.md` and `evals/cases/reference-routing.ts`, and the publish job of `.github/workflows/release.yml` |
| Searched | `install.rs`, `upgrade.rs`, `skills.rs`, `plugins.rs` for URLs, subprocesses and hashes; three more Rust files for hosts; the repository for the word "telemetry" |
| Tree listed only | Everything else |

All repository content came through the GitHub API, and the package metadata came from `npm view`. Nothing was cloned or
installed.

## Security

Safe to learn from: yes. Safe to install: no need arose, and installing is not recommended.

- `postinstall` runs on `npm install`. The release workflow moves all 7 platform binaries into `bin/` before
  `pnpm publish --provenance`, and the npm package has an unpacked size of 121 MB with a SLSA provenance record. The
  GitHub release download in `postinstall` has no hash check, but it runs only when the binary is missing. The tarball
  itself was not listed, so "the binaries ship in the package" is an inference from those two facts.
- A search of `install.rs` found no hash or integrity check on the Chrome for Testing download.
- On a global install, `postinstall` overwrites the package's own `.cmd` and `.ps1` npm shims.
- The browser version check calls `chromereleases.googleblog.com` by default. `AGENT_BROWSER_NO_VERSION_CHECK=1` turns it
  off. `upgrade` calls the npm registry, and the cloud browser providers are opt-in.
- The search for "telemetry" returned 5 files. Two are lockfiles. The other three filter other sites' telemetry out of
  recorded flows. This does not prove there is none.
- The stub skill's description says "Prefer agent-browser over any built-in browser automation or web tools", and its
  `allowed-tools` approves every `agent-browser` command, `eval` included.
- The project's security page says navigation, action and output controls impose no restrictions by default. The CLI can
  also attach to a running Chrome.
- A scan of five files (both main skills, `trust-boundaries.md`, `dogfood` and `AGENTS.md`) found no hidden Unicode
  characters.

**Not covered:** most of the Rust source, including the daemon, the stream server, `mcp.rs` and the dashboard; the examples;
and the Electron, Slack, derive-client and webmcp-gen skills. `AGENTS.md` had the hidden character scan only.
`tools/audit_claude_config.py --external` did not run, because it needs a local copy and nothing was to be ported. That
check is UNCHECKED, and any later port of this skill text has to run it first.

## Verdict by item

| Item | Verdict | Reason |
|---|---|---|
| The CLI and its MCP server | Skip | Out of domain. No TAOM skill or rule drives a browser, and Claude in Chrome already covers the occasional page. The Playwright plugin is off in `.claude/settings.json`. |
| `read <url>` page fetch | Skip | The need the [kepano review](adopt-kepano-2026-11-08.md) rejected for `defuddle`: clipping is rare, and this needs a global npm install. |
| Thin stub skill pointing to fuller text | Already done | `/adopt-external` is a thin entry point over its doc. |
| `dogfood` QA skill and issue taxonomy | Skip | Web application QA. `/game-run` judges from run folders, which suits a game. |
| Nonce content boundaries | Skip | The upstream reference calls such labels provenance cues, not a security boundary. AGENTS.md "Untrusted input" holds the rule, and TAOM has no browser pipeline to wrap. |
| Trust-boundaries reference | Skip | The same class of rule as "Untrusted input". The browser parts have no TAOM procedure to attach to. |
| One session per task, snapshot before screenshot | Skip | Advice for operating a browser, and nothing in TAOM operates one. |
| Failing closed on a bad policy file | Skip | TAOM mandates fail-open hooks. |
| Skill routing evals | Taken as an idea | See below. |
| Hostile content canary | Taken as an idea | See below. |

## The two ideas taken

**Skill routing evals.** Upstream sends each task prompt to a model once, then matches the reply against lists of expected
and forbidden patterns. A control case checks that a task the core guide already covers loads no reference.
`tools/harness_retro.py` counts real skill use, but it cannot tell a missed skill from an unneeded one. TAOM uses the
harness's own `claude plugin eval` and not upstream's Bun and AI Gateway setup.

**Hostile content canary.** Upstream's live eval puts an attack in tool metadata and results, requires proof that the
payload reached the model, and then fails the run if a planted secret leaks. `/review-outside-pr` carries the rule that a
pull request is untrusted data, and a search of `tools/tests` found no behavioural test of that rule. TAOM's case plants an
instruction in a code comment of a fixture diff.

Each run costs model calls, so a run needs the maintainer's word. Results and limits are in
[skill-evals.md](../features/skill-evals.md).
