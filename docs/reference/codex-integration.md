# Codex Integration

> How Codex is dispatched + the mandatory completion sequence. Extracted from CLAUDE.md 2026-07-18. Dispatch contract also in `.claude/skills/{codex-verify,review-codex}/SKILL.md`.


Codex operates as an independent verifier via the local `codex` CLI binary (`C:\Users\mikew\AppData\Roaming\npm\codex.cmd` on Windows). It shares no session context with Claude — providing a genuine second opinion.

**As of 2026-05-25, Claude dispatches Codex DIRECTLY via Bash — no terminal hand-off to the user.** Previous workflow asked the user to run `/codex:adversarial-review --background` in a separate terminal; the new flow uses `codex exec - < prompt.md > output.md 2>&1` from inside the skill (`run_in_background: true`). The user receives one notification when the background job completes and Claude continues automatically. See `.claude/skills/{codex-verify,review-codex}/SKILL.md` "Codex CLI invocation contract" for the full dispatch contract.

| Skill | Purpose | Dispatch model |
|-------|---------|----------------|
| `/codex-verify [feature]` | Lightweight verification (architectural compliance, 5-20 min) | Claude → `codex exec` via Bash, background |
| `/review-codex [feature]` | Heavyweight adversarial review (Known Suspects + vanilla decompile + RCA, 10-45 min) | Claude → `codex exec` via Bash, background |
| `/deep-review [feature] --codex` | Codex pre-review + 5+ Claude agents in parallel | Claude → `codex exec` + parallel `Agent` calls |
| `/codex:rescue [task]` | Delegate investigation to Codex (plugin-based; interactive) | Plugin/`SendMessage` (user prompt) |

**Pre-flight:** every skill that dispatches calls `codex login status` first. If not `Logged in using ChatGPT`, the skill stops and surfaces the message — the user must run `codex login` (interactive browser flow). Claude does NOT attempt to authenticate.

**Config:** `~/.codex/config.toml` (model + reasoning effort; project root has `.codex/config.toml` for project-scoped overrides if needed) | **Instructions:** `AGENTS.md` (project root)

**Completion workflow (MANDATORY for every C# feature, no exceptions):**
1. `/verify` — build + tests pass
2. `/deep-review [feature]` — 5+ parallel Claude agents
3. Fix all confirmed findings (HIGH must be fixed in-session per `.claude/skills/deep-review/SKILL.md` "HIGH findings — no silent deferrals")
4. `/review-codex [feature]` — dispatches Codex via Bash, harness notifies on completion
5. Claude auto-resumes when notification arrives — verify Codex findings, implement confirmed fixes, write Phase 3e RCA
6. `/verify` again — confirm green after fixes
7. Issue + docs + CHANGELOG + final commit

Steps 2-6 are blocking before commit. Past failure mode: the session author skipped 2 and 4 and shipped a 60-file feature with 1 HIGH + 2 MED + 3 LOW deep-review findings (see `docs/reviews/rca-crash-report-2026-05-25.md` meta-finding). With direct dispatch, there's no "I forgot to open the terminal" excuse — invoking the skill IS the dispatch.

