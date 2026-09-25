# Codex adversarial review: plan 016 (repo-hygiene-pins-readme), branch improve/016-repo-hygiene-pins-readme

Feature: Untrack personal and generated files, pin the MCP servers, and correct the README. Three paths are tracked that were never repo content: Claude Code's per-user settings file (`.claude/settings.local.json`, which ships Mike's personal allow rules, his machine paths and a trust list that pre-approves seven MCP servers on every clone), a 37 MB orphaned Python pickle (`_taom_loc.pkl`, unsafe to load, read by nothing) and an unpacked player crash bundle (`crashz/`, 490 KB). Separately, four MCP servers in `.mcp.json` and two in `.codex/config.toml` launch whatever version the registry serves that day (`npx -y`, `uvx` with no version, serena straight from its main branch); the uv cache on the desktop already holds 269 distinct serena checkouts, one per upstream commit a session happened to start on. Finally, the README on this branch tells players to install the wrong Bannerlord beta, names the wrong active branch, gives a `dotnet test` command that copies the build into the game install, and quotes counts nothing recomputes (several already wrong). After this plan: the shared deny list for MCP write tools lives in the tracked `.claude/settings.json`, the three paths are untracked and ignored, every MCP server runs a pinned version, `python tools/audit_claude_config.py` no longer reports `mcp-npx-unpinned`, and the README states this branch's truth without hand-kept counts.

HOW TO READ THE CODE (important): you are running in E:\repos\TAOM, but its working tree holds ANOTHER session's unrelated uncommitted edits. Review ONLY the branch under test through git refs:
- The change: git diff bec0389d..improve/016-repo-hygiene-pins-readme
- Any file as the branch has it: git show improve/016-repo-hygiene-pins-readme:<path>
- The base for comparison: git show bec0389d:<path>
Do NOT modify any file anywhere. Do NOT run builds, tests or the game. Read-only review.

TAOM ID CHEATSHEET:
Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale/North, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar, shaghana=Shaghana, abanissa=Abanissa
Culture IDs (custom): gondor, mordor, erebor, rivendell, lothlorien, mirkwood, isengard, gundabad, dolguldur, umbar
Culture IDs (XSLT/vanilla): vlandia=Rohan, empire=Dunland, empire_w=Gondor, empire_s=Mordor, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale
NOTE: rohan is NOT a valid ID (Rohan uses vlandia). dol_guldur is NOT valid (use dolguldur).

READ FIRST:
- The plan the change implements: git show improve/016-repo-hygiene-pins-readme:plans/016-repo-hygiene-pins-readme.md (intent, scope, STOP conditions)
- AGENTS.md (the project's rules; ADR-002 thin entry points, ADR-007 adapters, TDD, banned constructs)
- Any feature doc under docs/features/ the diff touches (git show on the branch)
- Engine behaviour: the decompile dump at E:\Decompiled_Bannerlord\ (Bannerlord v1.5.3; it can lag, so treat it as a guide, and say UNVERIFIED where it matters)

KNOWN SUSPECTS (CONFIRM or DISPUTE each with file:line evidence):
1. From the plan's STOP conditions, did the change hit or mishandle this risk? Step 0 was not done, or its check does not print `9 True False`. Never edit `settings.json` or
2. From the plan's STOP conditions, did the change hit or mishandle this risk? `.claude/settings.local.json` in the worktree holds anything shared beyond what "Current state"
3. From the plan's STOP conditions, did the change hit or mishandle this risk? The drift check prints anything, or any "Current state" excerpt does not match the worktree file.
4. From the plan's STOP conditions, did the change hit or mishandle this risk? A registry lookup, `npm`, `uvx` or `git ls-remote` fails, or a pinned version does not exist
5. From the plan's STOP conditions, did the change hit or mishandle this risk? The serena `--help` launch fails or its help lacks `--context` or `--project`; or the
6. From the plan's STOP conditions, did the change hit or mishandle this risk? The audit still reports `mcp-npx-unpinned`, or reports any new HIGH or MED finding other than the
7. Fail-safe defaults: any new or changed '?? true' / '?? false', config default or MCM default. Is the failure direction the safe one? Does a changed MCM default use a renamed setting (json2 persists old values)?
8. Stale state across lifecycle boundaries: singletons, statics or caches that survive a save load, a new campaign or a mission end.
9. Tests that pass without proving the behaviour: assertions on source text, mocks that test themselves, Assert.Inconclusive paths that report green.
10. Dead or no-op code introduced by the change; a gate that can never fire.

CHANGED FILES (20 files changed, 103 insertions(+), 6879 deletions(-)):
Harness and docs:
- .claude/settings.json
- .claude/settings.local.json
- .claude/skills/engine-bump/SKILL.md
- .claude/skills/native-crash-triage/SKILL.md
- .codex/config.toml
- .gitignore
- .mcp.json
- CHANGELOG.md
- README.md
- docs/INDEX.md
- docs/ai-includes/agent-operating-manual.md
- docs/features/moduledata-validation.md
- docs/migration/v1.4.8-impact.md
- docs/reference/development-machines.md
- docs/reference/mcp-servers.md
Other:
- _taom_loc.pkl
- crashz/manifest.txt
- crashz/report.json
- crashz/report.txt
- crashz/rgl_log.txt

REQUIRED SECTIONS:
1. VANILLA CODE: for every Harmony patch, GameModel override or engine API the diff touches or relies on, paste the relevant v1.5.3 decompile excerpt as a code block and state what the change assumes about it.
2. DEEP ANALYSIS: walk concrete scenarios through the changed code (first boot, save load, new campaign in the same process, mission start and end, co-op or dedicated server where relevant, the failure path of every try/catch the diff adds or changes).
3. CONFIG CROSS-REFERENCE: every ID, path, setting name or config key the diff adds or reads, checked against its source of truth.
4. FINDINGS OR OBSERVATIONS: numbered, each with severity (P1 crash/data loss/security, P2 wrong behaviour, P3 quality), file:line on the branch, the reasoning, and a concrete fix. Say explicitly when the plan itself is wrong.

QUALITY GATES: cite file:line for every claim; do not flag vanilla-matching code as a bug; do not assume empire=Rohan; do not skip hard sections; mark engine behaviour you could not read as UNVERIFIED.

PRIOR REVIEW LESSONS: SUCCESSES: config ID cross-reference caught rohan/dol_guldur mismatches; vanilla decompilation caught missing gates; lifecycle tracing caught stale caches. FAILURES: assuming empire=Rohan (it is Dunland); flagging vanilla-matching code as bugs; skipping hard sections.

OUTPUT: return the full review as your FINAL MESSAGE (the dispatcher redirects stdout into a file; do NOT write any file yourself). The very last line of your final message must be exactly: END OF CODEX REVIEW
