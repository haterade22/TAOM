# Codex adversarial review: plan 005 (security-hygiene), branch improve/005-security-hygiene

Feature: Scrub a vendored GitHub Packages credential, pin MCP servers, and close two python-source injection sites. Three independent security-hygiene items, all confirmed by reading the real files this session: 1. **A GitHub Packages username + ClearTextPassword (PAT-shaped) credential pair sits in a vendored `nuget.config`** under `Dependencies/.vendor-source/`. It is NOT a TAOM secret — it is BUTR's own token shipped inside their UIExtenderEx 2.13.2 release source, and the file is **gitignored / untracked** in TAOM (verified below). The risk is that this gitignored reference copy is the kind of file a future `/adopt-external` or port could accidentally drag into the tracked tree. The TAOM action is to **scrub the local copy and add a vet-checklist grep** so it can never ride a port into the public repo; rotation is upstream's call (see Step 2 "Rotation caveat"). 2. **The MCP servers in `.mcp.json` and `.codex/config.toml` launch upstream code with no version/rev pin** — every session executes whatever HEAD/latest resolves to, with broad filesystem access (the repo, the entire Bannerlord Modules dir, `E:\LOTRAOMAssets`, `E:\Decompiled_Bannerlord`). A compromised or typosquatted upstream release runs arbitrary code on the dev machine. `.mcp.json`'s `filesystem` server is the pre-existing MED `/security-scan` already reports (`mcp-npx-unpinned`); the serena/git servers and the entire `.codex/config.toml` are not caught by the existing gate today. 3. **`tools/process_faction_map.py` interpolates file paths into Python source that is then run via `python -c`.** A path containing a single quote terminates the raw-string literal and the remainder executes as Python in the child interpreter. The paths are currently TAOM-internal, so this is breakage/injection-by-quote rather than a live RCE — but it's a trivial, correct fix (pass paths as argv, not interpolated source). When this lands: no live-looking credential on disk in a port-reachable location, MCP servers pinned to known revisions, and the faction-map tool immune to quote-in-path.

HOW TO READ THE CODE (important): you are running in E:\repos\TAOM, but its working tree holds ANOTHER session's unrelated uncommitted edits. Review ONLY the branch under test through git refs:
- The change: git diff a39a9c86..improve/005-security-hygiene
- Any file as the branch has it: git show improve/005-security-hygiene:<path>
- The base for comparison: git show a39a9c86:<path>
Do NOT modify any file anywhere. Do NOT run builds, tests or the game. Read-only review.

TAOM ID CHEATSHEET:
Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale/North, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar, shaghana=Shaghana, abanissa=Abanissa
Culture IDs (custom): gondor, mordor, erebor, rivendell, lothlorien, mirkwood, isengard, gundabad, dolguldur, umbar
Culture IDs (XSLT/vanilla): vlandia=Rohan, empire=Dunland, empire_w=Gondor, empire_s=Mordor, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale
NOTE: rohan is NOT a valid ID (Rohan uses vlandia). dol_guldur is NOT valid (use dolguldur).

READ FIRST:
- The plan the change implements: git show improve/005-security-hygiene:plans/005-security-hygiene.md (intent, scope, STOP conditions)
- AGENTS.md (the project's rules; ADR-002 thin entry points, ADR-007 adapters, TDD, banned constructs)
- Any feature doc under docs/features/ the diff touches (git show on the branch)
- Engine behaviour: the decompile dump at E:\Decompiled_Bannerlord\ (Bannerlord v1.5.3; it can lag, so treat it as a guide, and say UNVERIFIED where it matters)

KNOWN SUSPECTS (CONFIRM or DISPUTE each with file:line evidence):
1. From the plan's STOP conditions, did the change hit or mishandle this risk? The `nuget.config` credential block doesn't match the Step 1 excerpt, OR the file is now tracked by git (`git ls-files --error-unmatch <path>` succeeds) — if it's tracked, this is a HIGHER-severity case (credential IS in history); report it so the orchestrator escalates (history-scrub + forced upstream disclosure) rather than treating it as the gitignored case this plan assumes.
2. From the plan's STOP conditions, did the change hit or mishandle this risk? `grep -rln "packageSourceCredentials" Dependencies/` finds matches in files other than the one named — report the paths (NOT the values).
3. From the plan's STOP conditions, did the change hit or mishandle this risk? A pin-resolution network command in Step 4 fails (offline/proxy) — environment failure, report and stop; do NOT guess a version number.
4. From the plan's STOP conditions, did the change hit or mishandle this risk? After dropping the outer `f` in Step 6/7, you're unsure whether a `{...}` inside the child source should be single or double braces — STOP and report rather than guessing; a wrong brace silently corrupts the child's stdout and the parent's parse.
5. From the plan's STOP conditions, did the change hit or mishandle this risk? `python -m py_compile` fails twice after a reasonable fix attempt.
6. From the plan's STOP conditions, did the change hit or mishandle this risk? Any step appears to require editing `tools/audit_claude_config.py`, `.gitignore`, or a C# file — all out of scope; report the need instead of acting.
7. Fail-safe defaults: any new or changed '?? true' / '?? false', config default or MCM default. Is the failure direction the safe one? Does a changed MCM default use a renamed setting (json2 persists old values)?
8. Stale state across lifecycle boundaries: singletons, statics or caches that survive a save load, a new campaign or a mission end.
9. Tests that pass without proving the behaviour: assertions on source text, mocks that test themselves, Assert.Inconclusive paths that report green.
10. Dead or no-op code introduced by the change; a gate that can never fire.

CHANGED FILES (3 files changed, 28 insertions(+), 11 deletions(-)):
Scripts and hooks:
- tools/process_faction_map.py
Harness and docs:
- CHANGELOG.md
- docs/ai-includes/external-repo-adoption.md

REQUIRED SECTIONS:
1. VANILLA CODE: for every Harmony patch, GameModel override or engine API the diff touches or relies on, paste the relevant v1.5.3 decompile excerpt as a code block and state what the change assumes about it.
2. DEEP ANALYSIS: walk concrete scenarios through the changed code (first boot, save load, new campaign in the same process, mission start and end, co-op or dedicated server where relevant, the failure path of every try/catch the diff adds or changes).
3. CONFIG CROSS-REFERENCE: every ID, path, setting name or config key the diff adds or reads, checked against its source of truth.
4. FINDINGS OR OBSERVATIONS: numbered, each with severity (P1 crash/data loss/security, P2 wrong behaviour, P3 quality), file:line on the branch, the reasoning, and a concrete fix. Say explicitly when the plan itself is wrong.

QUALITY GATES: cite file:line for every claim; do not flag vanilla-matching code as a bug; do not assume empire=Rohan; do not skip hard sections; mark engine behaviour you could not read as UNVERIFIED.

PRIOR REVIEW LESSONS: SUCCESSES: config ID cross-reference caught rohan/dol_guldur mismatches; vanilla decompilation caught missing gates; lifecycle tracing caught stale caches. FAILURES: assuming empire=Rohan (it is Dunland); flagging vanilla-matching code as bugs; skipping hard sections.

OUTPUT: return the full review as your FINAL MESSAGE (the dispatcher redirects stdout into a file; do NOT write any file yourself). The very last line of your final message must be exactly: END OF CODEX REVIEW
