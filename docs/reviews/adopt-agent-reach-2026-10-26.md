# Agent Reach adoption review (2026-10-26)

Review of [Panniantong/Agent-Reach](https://github.com/Panniantong/Agent-Reach) against TAOM's AI
harness and release process, through `/adopt-external`. Mike chose the one recommendation from a
three-way question on 2026-10-26 ("build it, commit and push"). Issue
[#1010](https://github.com/haterade22/TAOM/issues/1010).

## Coverage

- **Repository:** `main` at `14c2134` (MIT, Python, 118 files, pushed 2026-10-25). Metadata and
  the README through `gh api`; the tarball extracted into its own scratch folder and read there.
  Nothing was installed or run. Read in full or in the relevant part: `README.md`, `SECURITY.md`,
  `agent_reach/runtime/session_reuse.py`, the profile handling in `agent_reach/runtime/browser.py`,
  `.github/release-acceptance.json` and `docs/plans/2026-10-20-release-policy-guards.md`.
- **Automated vet:** `python tools/audit_claude_config.py --root <tree> --external`, 118 files
  scanned, the whole tree.
- **TAOM:** `/release`, `docs/reference/release-process.md`, the `triage-needs-ingame` label
  backlog, `tools/check_public_text.py`, the secrets audit, and the MCP-then-validator routing in
  CLAUDE.md.
- **Not read:** the channel adapters beyond their names, `agent_reach/skill/*`, the release
  scripts' code, `docs/install.md` past the audit's hit.

## What Agent Reach is

A CLI that lets an agent read and search Twitter, Reddit, YouTube, GitHub, Bilibili, XiaoHongShu and
other sites without paid APIs. Sites that need a login run in the tool's own background browser.
Each platform has an ordered list of a preferred tool and fallbacks.

## Security

- **Safe to learn from:** yes. The audit's HIGH findings were routine for an installer: one
  `curl | sh` line, uv's official installer in `docs/install.md`, and twelve `subprocess.run` calls
  with argument lists, not shell strings, for `ps`, `uv` and tool installs. No obfuscation or
  decode-then-run in the files read.
- **Safe to install:** no, and not needed.
  - The quick start tells the user to have their agent install it by reading a remote
    `docs/install.md`: an install driven by fetched instructions.
  - `session_reuse.py` exports a site's cookies from a connected local browser through Chrome
    DevTools (`export_site_cookies`) and writes them into the tool's own profile with
    `Storage.setCookies`. The README says the copied account is usually the user's main one. No
    outbound send was seen in that file, but a tool that moves live account cookies on an agent's
    behalf is out of bounds for TAOM.
- **Credibility:** the API reports 103,205 stars and 9,153 forks for a repository created
  2026-02-24 with 30 contributors and 339 watchers. Stars counted for nothing.

## What TAOM took

Agent Reach's release gate records a real run for each user-facing action, bound to the source's
SHA-256, and refuses a release while any action is pending or failed. TAOM's equivalent of a real
run is an in-game check, and a closed issue that still owes one carries `triage-needs-ingame`: 149
do today, 56 of them closed after the v2.0.35 tag. `/release` checked neither.

A blocking gate would stop every release while 56 checks are owed, so TAOM's form never blocks:

- `tools/ingame_ledger.py` lists the issues closed since the last tag that still carry the label,
  as text or as a markdown checklist, with the size of the older backlog.
- `/release` Phase 1 item 7 reports the count. Phase 5 writes `docs/releases/vX.Y.Z-testers.md`
  for the testing channel. Issue titles are developer text, so the list stays out of the Discord
  note.

## Decisions

| # | Item | Decision |
|---|---|---|
| A1 | Release acceptance ledger (`.github/release-acceptance.json`, the release-policy plan) | Adopted as `tools/ingame_ledger.py` and the `/release` steps (#1010), non-blocking |
| X1 | Install the CLI, its skill or its browser | Declined: see Security |
| X2 | Site readers for Reddit, YouTube and others | Declined: TAOM's players report on Discord and Nexus, which it does not read; a public page is one WebFetch away |
| X3 | Credential scrubbing in output | Declined: `tools/check_public_text.py` and the secrets audit cover TAOM's public text |
| X4 | Preferred-then-fallback tool list per platform | Declined: CLAUDE.md already routes the `taom-moduledata` MCP first, then `validate_moduledata.py` |
| X5 | URL safety checks before fetching | Declined: no TAOM tool fetches arbitrary URLs |
| X6 | A ledger of every path the tool creates, for a clean uninstall | Declined: TAOM's few out-of-repo caches are listed in `development-machines.md`; YAGNI |
| X7 | Update notice for a newer release | Declined: `upstream-watch.sh` covers the upstreams TAOM follows |
| X8 | `llms.txt` | Declined: TAOM's agent entry points are AGENTS.md and CLAUDE.md |
