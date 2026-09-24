# Plan 016: Untrack personal and generated files, pin the MCP servers, and correct the README

> **Executor instructions**: Follow this plan step by step. Run every
> verification command and confirm the expected result before moving to the
> next step. If anything in the "STOP conditions" section occurs, stop and
> report; do not improvise. **Never edit `plans/README.md`** (neither copy: the worktree's has no
> 016 row, and the main tree's belongs to the orchestrator). Instead, end your report with the
> status the 016 row should get (DONE, or BLOCKED with a one-line reason); the orchestrator
> maintains the index.
>
> **Where you work**: every Read, Edit and Write path in this plan is relative to the worktree
> `E:/repos/wt-016-repo-hygiene/` (for example `E:/repos/wt-016-repo-hygiene/.mcp.json`). Your
> default cwd is the main tree `E:\repos\TAOM`, where another session has uncommitted edits
> (including `CHANGELOG.md`, `Main/IoC.cs`, `Main/SubModule.cs` and `docs/INDEX.md`): never edit,
> stage or write any file under `E:/repos/TAOM`. Run every command with the **Bash tool**,
> prefixed with `cd E:/repos/wt-016-repo-hygiene && ` unless it uses `git -C`. Pass
> `timeout: 600000` for every `dotnet` and `uvx` command.
>
> **Scratch files** (commit message files): write them with the Write tool into the scratchpad
> directory your system prompt names. If it names none, use
> `E:/repos/wt-016-repo-hygiene-msg.txt` (beside the worktree, not inside it). Never a Git Bash
> `/tmp` path: the Read and Write tools cannot see it.
>
> **Drift check (run first, from the main tree `E:\repos\TAOM`)**:
> `git diff --stat b2e387db..HEAD -- .gitignore .mcp.json .codex/config.toml .claude/settings.json .claude/settings.local.json _taom_loc.pkl crashz README.md docs/ai-includes/agent-operating-manual.md docs/reference/mcp-servers.md docs/reference/development-machines.md docs/INDEX.md docs/features/moduledata-validation.md .claude/skills/engine-bump/SKILL.md .claude/skills/native-crash-triage/SKILL.md docs/migration/v1.4.8-impact.md Main/_Module/ModuleData/career_system/taom_careers.xml`
> Expected: no output (verified empty at `4b5662b2` while planning). If any
> in-scope file changed since this plan was written, compare the "Current
> state" excerpts against the live file before proceeding; on a mismatch,
> treat it as a STOP condition. `CHANGELOG.md` is left out on purpose (every
> commit touches it), and so is `plans/README.md` (you never edit it).

## Status

- **Priority**: P2
- **Effort**: S
- **Risk**: MED (merging the untrack commit deletes three paths from every existing checkout's disk, including Mike's personal `.claude/settings.local.json`; see "Maintenance notes")
- **Depends on**: none
- **Category**: security
- **Planned at**: commit `b2e387db`, 2026-09-23
- **Issue**: create before implementation lands (orchestrator)
- **Supersedes**: item B (MCP pins) of `plans/005-security-hygiene.md`. The unmerged branch
  `impl-005` carries June pins in commit `1d566be6`; never cherry-pick it (the reconcile pass
  already recommends dropping it).
- **Needs before dispatch**: Step 0, done by Mike or by the orchestrator on Mike's explicit
  request, never by the executor. `.claude/hooks/config-protection.sh` (lines 39-67 at `b2e387db`)
  refuses the Edit and Write tools on any file whose basename is `settings.json` or
  `settings.local.json` (exit 2) unless the user-approved override file
  `/tmp/claude-config-override-${CLAUDE_SESSION_ID}` exists for the calling session. An executor
  has no way to receive Mike's OK, so the one `settings.json` insertion is made in the worktree
  before dispatch, and the executor only verifies it (Step 1) and commits it (Step 9). The
  executor never edits `settings.json` or `settings.local.json`, never creates an override file,
  and never rewrites either file through a shell command.

## Why this matters

Three paths are tracked that were never repo content: Claude Code's per-user settings file
(`.claude/settings.local.json`, which ships Mike's personal allow rules, his machine paths and a
trust list that pre-approves seven MCP servers on every clone), a 37 MB orphaned Python pickle
(`_taom_loc.pkl`, unsafe to load, read by nothing) and an unpacked player crash bundle
(`crashz/`, 490 KB). Separately, four MCP servers in `.mcp.json` and two in `.codex/config.toml`
launch whatever version the registry serves that day (`npx -y`, `uvx` with no version, serena
straight from its main branch); the uv cache on the desktop already holds 269 distinct serena
checkouts, one per upstream commit a session happened to start on. Finally, the README on this
branch tells players to install the wrong Bannerlord beta, names the wrong active branch, gives a
`dotnet test` command that copies the build into the game install, and quotes counts nothing
recomputes (several already wrong). After this plan: the shared deny list for MCP write tools
lives in the tracked `.claude/settings.json`, the three paths are untracked and ignored, every MCP
server runs a pinned version, `python tools/audit_claude_config.py` no longer reports
`mcp-npx-unpinned`, and the README states this branch's truth without hand-kept counts.

## Current state

All excerpts are from `b2e387db` (read with `git show b2e387db:<path>`). The worktree is created
from `bannerlord-1.5.x` at `4b5662b2` or later; none of these files changed between the two.

### Binding rules (the executor has not read them)

- **AGENTS.md "Preserve others' work"**: never stash, reset, clean, commit or push someone else's
  changes. Work only in the worktree.
- **AGENTS.md "Human prose"**: commit bodies, CHANGELOG and docs use no em dash (U+2014) or en dash
  (U+2013); use a comma, colon, semicolon, parentheses or a new sentence. Hyphens in flags, paths and
  versions are fine. Code spans are exempt. When you replace words on a line that already holds an
  em dash, leave the existing dash alone (edit scope), but never type a new one.
- **AGENTS.md "Evidence, never invention"**: every number you write into the CHANGELOG or a commit
  body comes from output you read in this run. Step 1.6 prints each number the prescribed
  CHANGELOG entries and commit bodies carry (37 MB, seven servers, nine denies, 50 vs 67 careers);
  if one differs, write the number you read, not the plan's.
- **AGENTS.md "Git and commits"**: subject `<type>(<scope>): v<Version> - <description>`, where
  `<Version>` is the `<Version value=...>` in `Main/_Module/SubModule.xml` (`v2.0.30` at planning),
  at most 72 characters, body wrapped at 72, **no AI attribution trailer** (no `Co-Authored-By`).
  Stage explicit paths only; never `git add -A`, `git add .` or `git commit -a`.
- **CLAUDE.md "Where new knowledge goes"**: never put counts nothing computes into docs. That is why
  the README counts are removed rather than refreshed.
- **`.claude/rules/environment-failures.md`**: if a registry lookup, `npm`, `uvx` or the network
  fails, report the exact error and stop; do not install or reconfigure tools.
- **CLAUDE.md skills table**: "After editing hooks, settings, MCP config or CLAUDE.md: `/security-scan`".
  You cannot invoke skills; run its engine, `python tools/audit_claude_config.py`, directly (Steps 1,
  5, 13). The orchestrator runs `/security-scan` itself.
- **ADR-002 (thin entry points under 150 lines), ADR-007 (services talk to TaleWorlds types only
  through adapters), ADR-008 (every service is unit-testable)**: not engaged. This plan changes no
  C#. **TDD does not apply** for the same reason; every gate is a git, grep, parser or audit command.
- **Single-owner files**: `Main/IoC.cs`, `Main/SubModule.cs`, `Main/TAOM.csproj` and
  `Directory.Build.props` are not touched and must not be.

### 1. The tracked files that should not be

`git ls-files -s .claude/settings.local.json _taom_loc.pkl crashz/` lists six entries; sizes from
`git cat-file -s`: `.claude/settings.local.json` 2,627 B; `_taom_loc.pkl` 37,216,114 B;
`crashz/manifest.txt` 519 B, `crashz/report.json` 93,658 B, `crashz/report.txt` 71,318 B,
`crashz/rgl_log.txt` 324,291 B. `git check-ignore -v --no-index .claude/settings.local.json
_taom_loc.pkl crashz/rgl_log.txt` matches nothing (exit 1), and `git show b2e387db:.gitignore`
contains no `pkl`, `crashz` or `settings.local` pattern. The `.gitignore` ends (lines 230-236) with:

```text
# Tool + package-manager credential files.
.npmrc
.pypirc
.netrc
_netrc
secrets.json
credentials.json
```

- **`_taom_loc.pkl`**: added in `b930fd8a` (2026-08-29). Its header is a protocol-5 pickle of a
  dict keyed `old` holding `TAOM_*` string ids and English text (a one-off localization snapshot).
  `git grep -n -i "pickle\|taom_loc" b2e387db` outside `plans/` finds only a generic line in
  `.claude/skills/improve/references/audit-playbook.md:35`. Nothing reads or writes it.
  `git ls-files '*.pkl'` lists only this file.
- **`crashz/`**: added in `d9817f89` (2026-08-08). An unpacked v1.4.7 player crash bundle. Three
  tracked docs cite `crashz/report.json` by path, so they must be repointed (Step 7):
  - `.claude/skills/engine-bump/SKILL.md:29-30`:
    ```text
       is already booked: the open player report at `crashz/report.json`
       (`BannerlordVersion v1.4.7.117484`) can no longer be triaged against a local binary.
    ```
  - `.claude/skills/native-crash-triage/SKILL.md:74`:
    ```text
       Consequence: the open player report at `crashz/report.json` (`BannerlordVersion v1.4.7.117484`)
    ```
  - `docs/migration/v1.4.8-impact.md:136`:
    ```text
    v1.4.7, so the open report at `crashz/report.json` (`v1.4.7.117484`) still has no matching local
    ```
  After untracking, the bundle stays readable forever with `git show b2e387db:crashz/report.json`
  (`b2e387db` is on `bannerlord-1.5.x`'s history).
- **`.claude/settings.local.json`** (full content read at `b2e387db`, no credentials in it):
  - `permissions.allow` (35 rules, lines 3-39): personal and machine-specific, e.g.
    `Read(//c/Users/mikew/**)`, `Bash(node:*)`, `Bash(git checkout *)`, `Skill(codex:rescue)`,
    `PowerShell(./build.ps1 -RunTests 2>&1)`. **Stays per-user; do not move.**
  - `permissions.deny` (lines 40-50), a shared security control:
    ```json
        "deny": [
          "mcp__git__git_add",
          "mcp__git__git_commit",
          "mcp__git__git_reset",
          "mcp__git__git_checkout",
          "mcp__git__git_create_branch",
          "mcp__filesystem__write_file",
          "mcp__filesystem__edit_file",
          "mcp__filesystem__move_file",
          "mcp__filesystem__create_directory"
        ],
    ```
    **Moves to the tracked `.claude/settings.json` in Step 0** (the checker's condition: anything
    shared that lives only in the local file moves first, or the plan stops).
  - `permissions.additionalDirectories` (lines 51-55): three `E:\` paths. Machine-specific; stays.
  - `enabledMcpjsonServers` (lines 57-65): `serena`, `github`, `filesystem`, `git`, `ilspy`,
    `taom-moduledata`, `imagine`. **Deliberately stays per-user**: it is a trust approval, and a
    tracked copy would pre-approve seven servers (which run `uvx`/`npx`/`python` commands) on every
    clone, the exposure this plan removes. Each developer keeps it in their own local file.
  - `outputStyle: "default"`: trivial, stays.
- `.claude/settings.json` at `b2e387db` has **no `permissions` key at all**. It opens:
  ```json
  {
    "attribution": {
      "commit": "",
      "pr": "",
      "sessionUrl": false
    },
    "env": {
  ```
  (lines 1-7). Claude Code merges `permissions.deny` arrays across the project `settings.json` and
  `settings.local.json`, so Mike's copy keeps working with the deny list in both.
- Docs that state the file is tracked or holds the deny list (become false after this plan):
  - `docs/reference/development-machines.md:31` `**Three things ignore all of that** and need the
    path passed by hand:` and its third bullet, lines 40-41:
    ```text
    - `.claude/settings.local.json` is **tracked**, despite the name, so it is not a machine-local
      slot either. Machine-specific values belong in Windows user environment variables.
    ```
    (line 42 is blank, line 43 is `## The trap: a red validator on the laptop is usually the laptop`).
  - `docs/reference/mcp-servers.md:14` (the `git` row) ends `... Write tools are denied in
    settings.local.json because the safety hooks match Bash only | `.mcp.json` |`.
  - `docs/reference/mcp-servers.md:23`: `Nine MCP write tools are listed under `permissions.deny` in `.claude/settings.local.json`:`
  - `docs/reference/mcp-servers.md:65` begins `Project-level MCP servers (Serena, GitHub, filesystem,
    git, ilspy, taom-moduledata, imagine) are configured in `.mcp.json` at the project root and must
    be listed in `.claude/settings.local.json → enabledMcpjsonServers` to be trusted.`
  - `docs/INDEX.md:178` contains `what ignores them (`.mcp.json`, the decompile scripts, the tracked
    `settings.local.json`)`. (The main tree's uncommitted `docs/INDEX.md` hunks are at lines 23 and
    57 only; your worktree edit at 178 does not overlap them.)
  - `docs/features/moduledata-validation.md:686` is true in wording but is a Markdown **link** to
    the file. The line at `b2e387db`:
    ```text
    2. It is registered in [`.mcp.json`](../../.mcp.json) as the `taom-moduledata` stdio server and enabled in [`.claude/settings.local.json`](../../.claude/settings.local.json) → `enabledMcpjsonServers`.
    ```
    Once the file is untracked, that link is dead on every fresh clone (`lint_docs.py` checks links
    with `Path.exists()`, so it stays quiet in any checkout that still has the file on disk). Step 8
    turns the link into a plain code span. It is the only Markdown link to that file outside `plans/`
    (`git grep -n "](.*settings\.local\.json)" b2e387db -- ':!plans'`).
  - References that stay true and are **not** edited (they describe the per-user file):
    `tools/taom_mcp_server.py:17`,
    `.claude/skills/agent-introspection-debugging/SKILL.md:65`, `docs/context-budget-baseline.md:37`,
    and every historical CHANGELOG, archive, review and lessons line.
- Hooks that matter here: `check-claude-files-tracked.sh` checks only `.claude/skills`, `agents`,
  `rules` and `hooks` (its `DIRS` array, line 60), so ignoring `.claude/settings.local.json` does not
  trip it. `check-changelog-changed.sh` refuses a commit that stages `.claude/`, `CLAUDE.md` or
  `AGENTS.md` changes without `CHANGELOG.md` staged too.

### 2. The unpinned MCP servers

`.mcp.json` at `b2e387db` (the relevant lines, numbered):

```text
 5	      "command": "uvx",
 6	      "args": [
 7	        "--from",
 8	        "git+https://github.com/oraios/serena",
 9	        "serena",
10	        "start-mcp-server",
11	        "--context",
12	        "ide-assistant",
...
24	      "command": "npx",
25	      "args": [
26	        "-y",
27	        "@modelcontextprotocol/server-filesystem",
...
36	      "command": "uvx",
37	      "args": ["mcp-server-git"],
...
60	      "command": "uvx",
61	      "args": ["elevenlabs-mcp"],
```

`.codex/config.toml` at `b2e387db`:

```text
12	[mcp_servers.filesystem]
13	command = "npx"
14	args = [
15	  "-y",
16	  "@modelcontextprotocol/server-filesystem",
...
25	[mcp_servers.git]
26	command = "uvx"
27	args = ["mcp-server-git"]
```

Pins resolved read-only from the registries on 2026-09-23 (the executor re-confirms they exist in
Step 2; it does **not** chase newer versions):

| Server | Registry evidence | Pin |
|---|---|---|
| filesystem (npm) | `npm view @modelcontextprotocol/server-filesystem version` printed `2026.8.31`; dist-tag `latest` is `2026.8.31` | `@modelcontextprotocol/server-filesystem@2026.8.31` |
| git (PyPI) | PyPI JSON `mcp-server-git` latest `2026.8.18` (uploaded 2026-08-18) | `mcp-server-git@2026.8.18` |
| elevenlabs (PyPI) | PyPI JSON `elevenlabs-mcp` latest `0.12.2` (uploaded 2026-08-04) | `elevenlabs-mcp@0.12.2` |
| serena (git) | `git ls-remote --tags https://github.com/oraios/serena`: newest release tag `v1.7.0` = commit `949a27ef1e5fda1a6e7b561e777bcece345c6ffd` (lightweight tag); PyPI `serena-agent` latest is also `1.7.0` | `git+https://github.com/oraios/serena@949a27ef1e5fda1a6e7b561e777bcece345c6ffd` |

Facts about serena at that commit, read from GitHub raw files during planning:
`pyproject.toml` says `version = "1.7.0"`; `src/serena/cli.py:233-238` defines
`start-mcp-server` with `--project` and `--context`; `src/serena/config/context_mode.py:235-244`
maps the legacy context name `ide-assistant` to `claude-code` with a deprecation warning (so
`--context ide-assistant` still works; **leave that argument unchanged**); `src/serena/tools/symbol_tools.py`
defines `FindSymbolTool`, `GetSymbolsOverviewTool`, `FindReferencingSymbolsTool`,
`ReplaceSymbolBodyTool`, `RenameSymbolTool` among others (the repo only names
`mcp__serena__find_symbol`). The newest serena checkout in the desktop's uv cache is upstream
`HEAD` `b83b655c`, whose `pyproject.toml` says `2.0.0.dev0`: pinning moves sessions from an
unreleased dev build to the latest release. That is intended.

`uvx <package>@<version>` is uv's documented form for running a tool at a pinned version, and
`git+https://...@<sha>` is uv's documented git-revision form (installed uv: `0.9.4`).

`tools/audit_claude_config.py:418-421` is the rule this plan must clear:

```python
        if "npx" in cmd or "npx" in argstr:
            if "-y" in (args if isinstance(args, list) else []) and not re.search(r"@\d", argstr):
                res.findings.append(Finding("mcp-npx-unpinned", "MED", "mcp-risk", rel, 0,
```

It reads only `.mcp.json` (not `.codex/config.toml`, and it has no `uvx` rule), so the serena, git
and elevenlabs pins and the Codex file are checked by grep in this plan, not by the tool. Run in the
main tree during planning, the tool printed `HIGH:1  MED:1  INFO:7`: the HIGH is
`secret-generic` at `tools/tests/test_audit_repo_secrets.py:178`, **a known test fixture, decided
not a finding; ignore it**; the MED is `mcp-npx-unpinned` for `.mcp.json` `filesystem`.

### 3. The README and the agent command table

`README.md` at `b2e387db` (207 lines). The pin is `v1.5.3` (`.claude/pinned-game-version.txt`;
`Main/_Module/SubModule.xml:32` `<DependedModuleMetadata id="Native" ... version="v1.5.3.*" />`).
The GitHub default branch is `bannerlord-1.4.5` (the v1.4.8 line), and GitHub renders **that**
branch's README on the repo page: this plan fixes **this branch's** README for this branch's truth
and leaves `bannerlord-1.4.5` alone.

Lines this plan changes (verbatim):

```text
3	A Lord of the Rings total conversion mod for **Mount & Blade II: Bannerlord v1.4.8**.
15	**By the numbers:** 58 feature modules · 39 GameModel overrides · 30+ Harmony patch categories ·
16	50 careers across 16 cultures · 11 special resources across 18 kingdoms · 800+ troop definitions ·
17	2,600+ unit tests · 90 feature docs.
18	
19	> The active development branch (and the GitHub default) is **`bannerlord-1.4.5`**.
37	- Mount & Blade II: Bannerlord **v1.4.8** installed
52	git clone https://github.com/haterade22/TAOM      # lands on bannerlord-1.4.5
53	cd TAOM
55	.\setup-dev-env.ps1        # configure BANNERLORD_GAME_DIR + dependencies
58	dotnet test TAOM.Tests     # tests only
72	│   ├── Features/             # 58 feature modules (CareerSystem, SpecialResources, LotrIssues, Elephant, …)
76	├── TAOM.Tests/               # Unit tests (MSTest + NSubstitute, 2,600+ tests)
78	│   ├── adrs/                 # Architecture Decision Records (11)
79	│   ├── features/             # Feature documentation (90 files)
125	- **Career System** — 50 careers across 16 cultures; pick one at character creation, progress a
149	[`docs/features/`](docs/features/). LOTR rules are enforced through **39 GameModel overrides** and
150	**30+ Harmony patch categories**: both registries are catalogued in [harmony-patch-registry.md](docs/reference/harmony-patch-registry.md) and [gamemodel-registry.md](docs/reference/gamemodel-registry.md).
157	  code generator: 41 custom slash-command skills, 5 specialized agents, 22 automated hooks,
158	  18 path-scoped rule files, persistent cross-session memory, and 7 MCP servers (symbolic code
162	  with Claude, so it provides a genuine second opinion. 40+ reviews completed to date; review
163	  instructions live in [AGENTS.md](AGENTS.md).
175	Bannerlord **v1.5.2** is required (the Steam beta branch as of 2026-09-14). Place the four modules
```

Why each is wrong: lines 3, 37 and 175 disagree with the v1.5.3 pin (175 is the player install
section, so a player installs the wrong beta). Line 19: development happens on `bannerlord-1.5.x`
(the `v2.0.29` and `v2.0.30` tags are on it). Line 55: `setup-dev-env.ps1` only prompts for the
install path and sets the user variable `BANNERLORD_GAME_DIR` (`setup-dev-env.ps1:7,20`); there is no
dependency step. Line 58: `TAOM.Tests` builds `Main`, whose post-build targets copy into the game
install unless both `-p:DisableModuleCopy=true` and `-p:ModuleId=` are passed
(`docs/ai-includes/agent-operating-manual.md:55-57`). Counts: `git ls-tree -d --name-only
b2e387db Main/Features/` lists 108 directories (README: 58); the baseline suite has 10,239 tests
(README: 2,600+); `taom_careers.xml` at `b2e387db` has 67 `Career` elements (README: 50). Nothing
recomputes any of these numbers, so they are removed, not refreshed.

**Out of scope on purpose** (product claims, not contradicted by evidence read here): "More than
twenty kingdoms" (line 9), "Over 100 clans and 800+ unique troop definitions" (line 120), "11
per-kingdom resources" (line 129), "18 lore companions" (line 141), "12 languages" (line 144), and
every existing em dash in lines you do not rewrite.

`docs/ai-includes/agent-operating-manual.md:41-43` at `b2e387db`:

```text
| **Build** | `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true` | Use this, NOT `./build.ps1`, during agent work (avoids `out/` contention). ⚠️ See the caveat below — this flag does **not** actually stop deployment. |
| **Test** | `dotnet test TAOM.Tests/TAOM.Tests.csproj -p:DisableModuleCopy=true` | Add `--filter "FullyQualifiedName~X"` to narrow. Same caveat. |
| Engine-binding gate | `dotnet test TAOM.Tests/TAOM.Tests.csproj --filter "TestCategory=BindingVerification"` | Verifies patch/GameModel/reflection bindings resolve against the installed engine. |
```

Line 57 of the same file already documents the fix: `Add -p:ModuleId= to genuinely skip all
three copy targets ... dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=`.
`TestCategory("BindingVerification")` exists in the suite (e.g.
`TAOM.Tests/Features/BanditManagement/Patch86HideoutBossFightBindingTests.cs`).

## Commands you will need

| Purpose | Command (from the worktree) | Expected on success |
|---|---|---|
| Config security audit | `python tools/audit_claude_config.py` | before Step 3: `HIGH:1  MED:1`; after Step 5: `HIGH:1  MED:0` (the HIGH is the known fixture) |
| Docs | `python tools/lint_docs.py` | exit 0 |
| Test (final sanity only; no C# changes) | `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=` | no failures except the two named below |
| Filtered test (not needed) | `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~<ClassName>"` | n/a |
| Build (not needed) | `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=` | exit 0 |
| Data (the commit hook runs it) | `python tools/validate_moduledata.py` | 0 ERRORs |
| JSON parse | `python -c "import json;json.load(open('.mcp.json',encoding='utf-8'));json.load(open('.claude/settings.json',encoding='utf-8'));print('ok')"` | `ok` |
| TOML parse | `python -c "import tomllib;tomllib.load(open('.codex/config.toml','rb'));print('ok')"` | `ok` |

**Test baseline at `b2e387db`**: 10,239 tests, 10,235 pass, 2 ignored
(`WargAttack_FastWarg_InvokesRunningAttack`, `WargAttack_SlowWarg_InvokesStandingAttack`, deliberate
`[Ignore]`), and 2 fail: `TheElkItem_DeclaresTheScaleTheReachIsTunedFor` (in the Elk tests) and
`AnimaliaActionSets_BindOnlyHorseActions_ToClipsThatExist` (in `AnimaliaMountWiringTests`). Both
assert on the live, unversioned `LOTRLOME_Armory` install that another session is editing. They are
not caused by any plan; **do not chase them**. If the worktree base is newer than `4b5662b2`, the
total may differ; the check is that nothing else fails. Report the totals you read. Never run
`./build.ps1` (it deploys into the game).

## Scope

**In scope** (the only files you may change, all inside the worktree):

- `.mcp.json` (four pins)
- `.codex/config.toml` (two pins)
- `.gitignore` (append one block)
- `.claude/settings.json`: **commit only**; Step 0 made the edit before dispatch
- `.claude/settings.local.json`, `_taom_loc.pkl`, `crashz/` (4 files): `git rm --cached` only; the
  files stay on disk in the worktree
- `.claude/skills/engine-bump/SKILL.md`, `.claude/skills/native-crash-triage/SKILL.md`,
  `docs/migration/v1.4.8-impact.md` (repoint the `crashz` citation)
- `docs/reference/mcp-servers.md`, `docs/reference/development-machines.md`, `docs/INDEX.md`
  (settings.local.json wording), `docs/features/moduledata-validation.md` (line 686: unlink only)
- `README.md`, `docs/ai-includes/agent-operating-manual.md`
- `CHANGELOG.md` (two entries; edit it with the Edit tool only: the file starts with a UTF-8 BOM
  that a shell rewrite may drop)

**Out of scope** (do NOT touch, even though they look related):

- Rewriting git history to purge the old blobs (the 37 MB pickle stays in the pack): Mike's decision.
- Moving `enabledMcpjsonServers`, `additionalDirectories` or any allow rule into `.claude/settings.json`.
- `tools/audit_claude_config.py` (adding a `uvx` rule or a `.codex/config.toml` scan is a follow-up).
- The hardcoded `E:\` paths in `.mcp.json` and `.codex/config.toml`, `.codex/config.toml`'s
  `inherit = "all"`, and serena's deprecated `--context ide-assistant` argument.
- The `bannerlord-1.4.5` branch and its README; any product count listed as out of scope above.
- `Main/IoC.cs`, `Main/SubModule.cs`, `Main/TAOM.csproj`, `Directory.Build.props`: single-owner and
  unrelated; nothing here needs them.
- Anything under the main tree `E:/repos/TAOM`, and the lotraom-assets mirror.
- `plans/README.md` in either tree (report the 016 status instead).

## Git workflow

- Branch `plan/016-repo-hygiene` in the worktree `E:/repos/wt-016-repo-hygiene` (Step 0 creates both).
  Do NOT push, open a PR, merge, or delete the worktree.
- Two commits (Steps 9 and 12). Subject format `<type>(<scope>): v<Version> - <description>`, where
  `<Version>` comes from `Main/_Module/SubModule.xml` in the worktree (`grep -o 'Version value="[^"]*"' Main/_Module/SubModule.xml | head -1`,
  `v2.0.30` at planning). Max 72 characters, body wrapped at 72, no AI attribution trailer.
  Optional trailers: `Not-tested:`, `Constraint:`.
- Stage explicit paths only (`git add <path> ...`, `git rm --cached <path> ...`). Never `-A`, `.`,
  `-a`, `--no-verify`, `stash`, `reset` or `checkout -- <path>`.
- Commit with `git -C E:/repos/wt-016-repo-hygiene commit -F <message file>`; write the message file
  with the Write tool in the scratch location named under "Scratch files" at the top (Bash heredocs
  mangle quotes).

## Steps

### Step 0: Before dispatch (Mike, or the orchestrator on Mike's explicit request; NOT the executor)

1. From the main tree:
   `git -C E:/repos/TAOM worktree add E:/repos/wt-016-repo-hygiene -b plan/016-repo-hygiene bannerlord-1.5.x`
2. In `E:/repos/wt-016-repo-hygiene/.claude/settings.json` only (never the main tree's copy), insert
   a `permissions` block after the `attribution` block. Mike can do it in an editor; the orchestrator
   may do it with the Edit tool only after Mike explicitly asks, using the user-approved override
   that `config-protection.sh:39-44` checks, and deletes the override file right after. Replace
   lines 6-7:
   ```json
     },
     "env": {
   ```
   with:
   ```json
     },
     "permissions": {
       "deny": [
         "mcp__git__git_add",
         "mcp__git__git_commit",
         "mcp__git__git_reset",
         "mcp__git__git_checkout",
         "mcp__git__git_create_branch",
         "mcp__filesystem__write_file",
         "mcp__filesystem__edit_file",
         "mcp__filesystem__move_file",
         "mcp__filesystem__create_directory"
       ]
     },
     "env": {
   ```
3. Check, from the worktree:
   `cd E:/repos/wt-016-repo-hygiene && python -c "import json;s=json.load(open('.claude/settings.json',encoding='utf-8'));l=json.load(open('.claude/settings.local.json',encoding='utf-8'));print(len(s['permissions']['deny']), sorted(s['permissions']['deny'])==sorted(l['permissions']['deny']), 'allow' in s['permissions'])"`
   prints `9 True False`.

### Step 1: Check Step 0, run the drift check, record the audit baseline

1. Run the drift check from the top of this file (from the main tree). Expected: no output.
2. `git -C E:/repos/wt-016-repo-hygiene rev-parse --abbrev-ref HEAD` prints `plan/016-repo-hygiene`.
3. Re-run the Step 0.3 command in the worktree. Expected: `9 True False`.
4. `cd E:/repos/wt-016-repo-hygiene && git status --short` shows exactly one line:
   ` M .claude/settings.json`.
5. `cd E:/repos/wt-016-repo-hygiene && python tools/audit_claude_config.py | sed -n '1,3p;9,12p'`
   and note the summary line.
6. Print the numbers the CHANGELOG entries and commit bodies will carry (pickle bytes; trust-list
   length, deny count and `Career` elements via a real XML parse, because `grep -c "<Career "`
   also counts one commented-out element; the README's career claim):
   `cd E:/repos/wt-016-repo-hygiene && git cat-file -s HEAD:_taom_loc.pkl && python -c "import json,xml.etree.ElementTree as E;l=json.load(open('.claude/settings.local.json',encoding='utf-8'));print(len(l['enabledMcpjsonServers']),len(l['permissions']['deny']),sum(1 for _ in E.parse('Main/_Module/ModuleData/career_system/taom_careers.xml').getroot().iter('Career')))" && grep -o "[0-9]* careers across" README.md`

**Verify**: 1 to 4 as stated; 5 shows `HIGH:1  MED:1` with the MED being `mcp-npx-unpinned` on
`.mcp.json`; 6 prints `37216114` (37 MB), then `7 9 67`, then `50 careers across` twice. A
different number is not a STOP: use the number you read in Steps 9 and 12. If Step 0 is missing or differs (wrong count, `False`, an `allow` key, other modified
files), STOP before editing anything and report which check failed.

### Step 2: Confirm the four pins exist in their registries

Run (read-only network lookups):

```bash
npm view @modelcontextprotocol/server-filesystem@2026.8.31 version
python -c "import json,urllib.request as u;print(json.load(u.urlopen('https://pypi.org/pypi/mcp-server-git/2026.8.18/json',timeout=30))['info']['version'])"
python -c "import json,urllib.request as u;print(json.load(u.urlopen('https://pypi.org/pypi/elevenlabs-mcp/0.12.2/json',timeout=30))['info']['version'])"
git ls-remote https://github.com/oraios/serena refs/tags/v1.7.0
```

**Verify**: the four outputs are `2026.8.31`, `2026.8.18`, `0.12.2`, and
`949a27ef1e5fda1a6e7b561e777bcece345c6ffd	refs/tags/v1.7.0`. Newer versions existing is fine; do
not change the pins. A network or tool error is an environment failure: STOP and report it.

### Step 3: Pin the four servers in `.mcp.json`

Edit exactly four lines (keep indentation and trailing commas as they are):

- line 8: `        "git+https://github.com/oraios/serena",` becomes
  `        "git+https://github.com/oraios/serena@949a27ef1e5fda1a6e7b561e777bcece345c6ffd",`
- line 27: `        "@modelcontextprotocol/server-filesystem",` becomes
  `        "@modelcontextprotocol/server-filesystem@2026.8.31",`
- line 37: `      "args": ["mcp-server-git"],` becomes `      "args": ["mcp-server-git@2026.8.18"],`
- line 61: `      "args": ["elevenlabs-mcp"],` becomes `      "args": ["elevenlabs-mcp@0.12.2"],`

**Verify**: `cd E:/repos/wt-016-repo-hygiene && git diff --numstat -- .mcp.json` prints
`4	4	.mcp.json`; `grep -c -E "serena@949a27ef1e5fda1a6e7b561e777bcece345c6ffd|server-filesystem@2026\.8\.31|mcp-server-git@2026\.8\.18|elevenlabs-mcp@0\.12\.2" .mcp.json`
prints `4`; the JSON parse command from "Commands you will need" prints `ok`.

### Step 4: Pin the two servers in `.codex/config.toml`

- line 16: `  "@modelcontextprotocol/server-filesystem",` becomes
  `  "@modelcontextprotocol/server-filesystem@2026.8.31",`
- line 27: `args = ["mcp-server-git"]` becomes `args = ["mcp-server-git@2026.8.18"]`

**Verify**: `git diff --numstat -- .codex/config.toml` prints `2	2	.codex/config.toml`;
`grep -c -E "server-filesystem@2026\.8\.31|mcp-server-git@2026\.8\.18" .codex/config.toml` prints
`2`; the TOML parse command prints `ok`.

### Step 5: Prove the pinned launch forms work and the audit finding is gone

```bash
uvx --from "git+https://github.com/oraios/serena@949a27ef1e5fda1a6e7b561e777bcece345c6ffd" serena start-mcp-server --help
uvx mcp-server-git@2026.8.18 --help
python tools/audit_claude_config.py | sed -n '1,3p'
python tools/audit_claude_config.py | grep -c mcp-npx-unpinned
```

(The two `uvx` calls populate the uv cache exactly as an MCP launch would; allow up to 10 minutes.)

**Verify**: the serena call exits 0 and its help text contains `--context` and `--project`; the git
call exits 0 and prints a usage/options screen; the audit summary shows `HIGH:1  MED:0`; the grep
prints `0`. Do not launch the filesystem or elevenlabs servers (they wait on stdin); Step 2 proved
their versions exist.

### Step 6: Untrack the three paths and ignore them

1. Dry run first: `cd E:/repos/wt-016-repo-hygiene && git rm --cached -r -n -- .claude/settings.local.json _taom_loc.pkl crashz`
   must print exactly six `rm '...'` lines: `.claude/settings.local.json`, `_taom_loc.pkl`,
   `crashz/manifest.txt`, `crashz/report.json`, `crashz/report.txt`, `crashz/rgl_log.txt`. Any
   other count or path is a STOP. Then run the same command without `-n`.
2. Append this block to the end of `.gitignore`, after the `credentials.json` line. The block
   below already starts with the one blank line that separates it; add no other blank line:
   ```text

   # Per-user and generated files that were once tracked by mistake.
   # settings.local.json is Claude Code's per-user settings slot: personal allow rules,
   # machine paths and each developer's MCP trust list (enabledMcpjsonServers). Rules
   # every clone needs, such as the MCP write-tool deny list, go in settings.json.
   .claude/settings.local.json
   # Python pickles are never repo content, and loading one runs code. The only one
   # ever tracked was a one-off localization snapshot that nothing read.
   *.pkl
   # Player crash bundles unpacked for triage. The v1.4.7 report once tracked here
   # stays readable from history: git show b2e387db:crashz/report.json
   /crashz/
   ```

**Verify**:
- `git ls-files .claude/settings.local.json _taom_loc.pkl crashz` prints nothing.
- `ls .claude/settings.local.json _taom_loc.pkl crashz/report.json` lists all three (still on disk).
- `git check-ignore -v .claude/settings.local.json _taom_loc.pkl crashz/report.json` prints three
  lines, each starting `.gitignore:` and naming the new patterns.
- `git status --short` shows six `D ` lines (the untracked paths), ` M .claude/settings.json`,
  ` M .gitignore`, ` M .mcp.json`, ` M .codex/config.toml`, and nothing else.

### Step 7: Repoint the three `crashz/report.json` citations

- `.claude/skills/engine-bump/SKILL.md:29-30`: replace these two lines with the three below. In
  the file each line starts with exactly three spaces (the block here adds the plan's own list
  indent on top; match the file's three spaces, not the plan's). Old:
  ```text
     is already booked: the open player report at `crashz/report.json`
     (`BannerlordVersion v1.4.7.117484`) can no longer be triaged against a local binary.
  ```
  New (only the middle line names `crashz/report.json`, and it carries the `git show` form, so
  the Step 7 grep sees one line from this file):
  ```text
     is already booked: the open player report (untracked; read it with
     `git show b2e387db:crashz/report.json`; `BannerlordVersion v1.4.7.117484`) can no longer be
     triaged against a local binary.
  ```
- `.claude/skills/native-crash-triage/SKILL.md:74`: replace
  `Consequence: the open player report at `crashz/report.json` (`BannerlordVersion v1.4.7.117484`)`
  with
  `Consequence: the open player report `crashz/report.json` (untracked; `git show b2e387db:crashz/report.json`; `BannerlordVersion v1.4.7.117484`)`
  (the file line starts with exactly three spaces; keep them; line 75 is unchanged).
- `docs/migration/v1.4.8-impact.md:136`: replace
  `v1.4.7, so the open report at `crashz/report.json` (`v1.4.7.117484`) still has no matching local`
  with
  `v1.4.7, so the open report `crashz/report.json` (`v1.4.7.117484`; untracked, read it with `git show b2e387db:crashz/report.json`) still has no matching local`

**Verify**: `git grep -n "crashz/report.json" -- .claude/skills docs/migration/v1.4.8-impact.md`
prints exactly three lines (engine-bump line 30, native-crash-triage line 74, v1.4.8-impact line
136), and every one contains `git show b2e387db:crashz/report.json`.

### Step 8: Correct the docs that say `settings.local.json` is tracked or holds the deny list, and unlink it

- `docs/reference/mcp-servers.md:14`: in the `git` row, replace
  `Write tools are denied in settings.local.json because` with
  `Write tools are denied in `.claude/settings.json` because`.
- `docs/reference/mcp-servers.md:23`: replace the line with
  `Nine MCP write tools are listed under `permissions.deny` in the tracked `.claude/settings.json`, so every clone gets them (they lived in `settings.local.json` until it was untracked):`
- `docs/reference/mcp-servers.md:65`: replace
  `and must be listed in `.claude/settings.local.json → enabledMcpjsonServers` to be trusted.`
  with
  `and each developer trusts them in their own `.claude/settings.local.json → enabledMcpjsonServers`. That file is per-user and untracked: a tracked trust list would approve every server on every clone.`
  (the `→` arrow is existing text inside a code span; keep the rest of the paragraph unchanged).
- `docs/reference/development-machines.md:31`: `**Three things ignore all of that**` becomes
  `**Two things ignore all of that**`. Delete lines 40-41 (the `.claude/settings.local.json` is
  **tracked** bullet). In their place, after the `.mcp.json` bullet (which ends at line 39) and
  separated from it by one blank line, add this paragraph (the blank line before `## The trap`
  stays):
  ```text
  `.claude/settings.local.json` is per-user and untracked: each machine keeps its own copy (personal
  allow rules, `additionalDirectories`, `enabledMcpjsonServers`). Anything every clone needs, such
  as the MCP write-tool deny list, lives in the tracked `.claude/settings.json`.
  ```
- `docs/INDEX.md:178`: replace
  `what ignores them (`.mcp.json`, the decompile scripts, the tracked `settings.local.json`)` with
  `what ignores them (`.mcp.json` and the decompile scripts)`.
- `docs/features/moduledata-validation.md:686`: replace
  `[`.claude/settings.local.json`](../../.claude/settings.local.json)` with
  `` `.claude/settings.local.json` `` (a plain code span; the rest of the line, including the
  `.mcp.json` link and the `→`, stays).

**Verify**:
- `git grep -n -E "tracked\*\*|the tracked .settings\.local|denied in settings\.local|deny. in .\.claude/settings\.local" -- docs/reference/mcp-servers.md docs/reference/development-machines.md docs/INDEX.md`
  prints nothing.
- `grep -c "Two things ignore all of that" docs/reference/development-machines.md` prints `1`.
- `grep -c "settings.json" docs/reference/mcp-servers.md` is at least `2`.
- `git grep -n "](.*settings\.local\.json)" -- ':!plans'` prints nothing.
- `python tools/lint_docs.py` exits 0.

### Step 9: CHANGELOG entry and the first commit

1. In the worktree's `CHANGELOG.md` (Edit tool only; see Scope), find the `> **Archive:**`
   blockquote near the top. If the first `## ` heading after it is today's date
   (`## YYYY-MM-DD`), insert the entry directly under that heading, above its existing first `###`
   entry; otherwise add a new `## <today>` heading above the current first date heading and put the
   entry under it. Entry (replace `v2.0.30` if SubModule.xml says otherwise, and any number that
   Step 1.6 printed differently):
   ```markdown
   ### chore(security): v2.0.30 - untrack local files and pin MCP servers

   Three tracked paths were never repo content, and four MCP servers ran whatever version their
   registry served that day.

   - **Untracked and ignored**: `.claude/settings.local.json` (Claude Code's per-user settings:
     personal allow rules, machine paths and the MCP trust list), `_taom_loc.pkl` (a 37 MB pickle
     from a one-off localization session that nothing reads) and `crashz/` (an unpacked v1.4.7
     player crash bundle). The blobs stay in history; the three docs that cite the crash report now
     give `git show b2e387db:crashz/report.json`, and `moduledata-validation.md` no longer links to
     the now-untracked settings file.
   - **Deny list moved**: the nine MCP write-tool denies (`mcp__git__git_add` and the rest) now live
     in the tracked `.claude/settings.json`, so every clone keeps them. `enabledMcpjsonServers`
     stays per-user on purpose: a tracked trust list would pre-approve seven servers on every clone.
   - **Pinned**: serena to the `v1.7.0` commit `949a27ef`, `@modelcontextprotocol/server-filesystem@2026.8.31`,
     `mcp-server-git@2026.8.18` and `elevenlabs-mcp@0.12.2`, in `.mcp.json` and (filesystem, git)
     `.codex/config.toml`. `python tools/audit_claude_config.py` no longer reports
     `mcp-npx-unpinned`. Serena moves from its unreleased main branch to the latest release.

   **After merging or pulling this commit**, git deletes `.claude/settings.local.json`,
   `_taom_loc.pkl` and `crashz/` from that working tree. Restore your own settings file with
   `git show b2e387db:.claude/settings.local.json > .claude/settings.local.json` (or from a copy
   taken before the merge); it is ignored from now on. Then restart Claude Code so it reloads the
   pinned servers.
   ```
2. Stage explicitly:
   `git add .gitignore .mcp.json .codex/config.toml .claude/settings.json .claude/skills/engine-bump/SKILL.md .claude/skills/native-crash-triage/SKILL.md docs/migration/v1.4.8-impact.md docs/reference/mcp-servers.md docs/reference/development-machines.md docs/INDEX.md docs/features/moduledata-validation.md CHANGELOG.md`
   (the three `git rm --cached` removals from Step 6 are already staged).
3. Message file (Write tool, at the "Scratch files" location), subject then blank line then body
   wrapped at 72:
   ```text
   chore(security): v2.0.30 - untrack local files and pin MCP servers

   Untrack .claude/settings.local.json, _taom_loc.pkl and crashz/ and
   ignore them. Move the nine MCP write-tool denies into the tracked
   .claude/settings.json first so every clone keeps them; the MCP trust
   list stays per-user. Repoint the three crashz/report.json citations
   to git show b2e387db:crashz/report.json, and unlink the one Markdown
   link to settings.local.json (moduledata-validation.md).

   Pin serena (v1.7.0, 949a27ef), server-filesystem 2026.8.31,
   mcp-server-git 2026.8.18 and elevenlabs-mcp 0.12.2 in .mcp.json
   and .codex/config.toml. audit_claude_config.py: MED 1 -> 0.

   Merging this deletes settings.local.json from each checkout's disk;
   restore it from b2e387db (see CHANGELOG).

   Not-tested: MCP startup inside Claude Code and Codex (needs a restart)
   ```
   Replace `MED 1 -> 0` with the numbers you actually read in Steps 1 and 5.
4. `git -C E:/repos/wt-016-repo-hygiene commit -F <message file>`

**Verify**: `git -C E:/repos/wt-016-repo-hygiene log -1 --stat --format=%s` shows the subject and
exactly these 18 paths: the 6 deletions (`.claude/settings.local.json`, `_taom_loc.pkl`, 4 under
`crashz/`) plus the 12 staged in 9.2; `git log -1 --format=%B | grep -ci co-authored` prints `0`;
`git status --short` prints nothing. If a commit hook denies, read its message; if it names files
you did not touch (a hook may be reading the main checkout), STOP and report it verbatim.

### Step 10: Correct the README

Apply these replacements to `README.md` (exact old text on the left of "becomes"). The line
numbers are those of the unedited file; items 2 and 5 change the line count, so **apply the items
bottom-up (16 first, 1 last)**, or match each by its quoted old text, never by line number alone.

1. Line 3: `**Mount & Blade II: Bannerlord v1.4.8**.` becomes `**Mount & Blade II: Bannerlord v1.5.3**.`
2. Delete lines 15-18: the three lines below plus the blank line after them, so the paragraph
   ending `...to fit Tolkien's world.` is followed by one blank line and then the old line 19.
   ```text
   **By the numbers:** 58 feature modules · 39 GameModel overrides · 30+ Harmony patch categories ·
   50 careers across 16 cultures · 11 special resources across 18 kingdoms · 800+ troop definitions ·
   2,600+ unit tests · 90 feature docs.
   ```
3. Line 19, `> The active development branch (and the GitHub default) is **`bannerlord-1.4.5`**.`,
   becomes:
   `> Development happens on **`bannerlord-1.5.x`** (Bannerlord v1.5.3). The GitHub default branch, **`bannerlord-1.4.5`**, is the v1.4.8 line and shows its own README.`
4. Line 37: `- Mount & Blade II: Bannerlord **v1.4.8** installed` becomes
   `- Mount & Blade II: Bannerlord **v1.5.3** installed (the Steam beta branch)`
5. After line 53 (`cd TAOM`) insert a new line:
   `git switch bannerlord-1.5.x     # this branch: the v1.5.3 line`
6. Line 55: `# configure BANNERLORD_GAME_DIR + dependencies` becomes
   `# set BANNERLORD_GAME_DIR (asks for your install path)` (keep the command and its padding).
7. Line 58: `dotnet test TAOM.Tests     # tests only` becomes
   `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=   # tests only, no deploy`
8. Line 72: `# 58 feature modules (CareerSystem,` becomes `# Feature modules (CareerSystem,`
9. Line 76: `# Unit tests (MSTest + NSubstitute, 2,600+ tests)` becomes `# Unit tests (MSTest + NSubstitute)`
10. Line 78: `# Architecture Decision Records (11)` becomes `# Architecture Decision Records`
11. Line 79: `# Feature documentation (90 files)` becomes `# Feature documentation`
12. Line 125: `50 careers across 16 cultures; pick one` becomes `Culture-specific careers; pick one`
    (the em dash before it is existing text: leave it).
13. Lines 149-150: `**39 GameModel overrides** and` + newline + `**30+ Harmony patch categories**: both registries are catalogued in`
    becomes `GameModel overrides and` + newline + `Harmony patches, both catalogued in` (the two links after it stay).
14. Lines 157-158: `code generator: 41 custom slash-command skills, 5 specialized agents, 22 automated hooks,`
    + newline + `  18 path-scoped rule files, persistent cross-session memory, and 7 MCP servers (symbolic code`
    becomes `code generator: custom slash-command skills, specialized agents, automated hooks,`
    + newline + `  path-scoped rule files, persistent cross-session memory, and project MCP servers (symbolic code`.
15. Line 162: `genuine second opinion. 40+ reviews completed to date; review` becomes
    `genuine second opinion. Review` (line 163 `  instructions live in [AGENTS.md](AGENTS.md).` stays).
16. Line 175: `Bannerlord **v1.5.2** is required (the Steam beta branch as of 2026-09-14). Place the four modules`
    becomes `Bannerlord **v1.5.3** is required (the Steam beta branch). Place the four modules`.

**Verify** (from the worktree):
- `grep -n -E "v1\.4\.8|v1\.5\.2" README.md` prints exactly one line, the new line-19 sentence
  (it contains `bannerlord-1.4.5`).
- `grep -c "v1\.5\.3" README.md` prints at least `4`.
- `grep -n -E "58 feature|2,600|39 GameModel|30\+ Harmony|41 custom|5 specialized|22 automated|18 path-scoped|7 MCP|40\+ reviews|Records \(11\)|\(90 files\)|50 careers|By the numbers" README.md` prints nothing.
- `grep -n "dotnet test" README.md | grep -v -- "-p:ModuleId="` prints nothing.
- `python -c "s=open('README.md',encoding='utf-8').read();import subprocess;o=subprocess.run(['git','show','HEAD:README.md'],capture_output=True).stdout.decode('utf-8');print(s.count('\u2014')-o.count('\u2014'), s.count('\u2013')-o.count('\u2013'))"`
  prints `0 0` (no new em or en dashes; `HEAD` is still the Step 9 commit here).

### Step 11: Add `-p:ModuleId=` to the agent command table

In `docs/ai-includes/agent-operating-manual.md`:

- Line 41: the command `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true` becomes
  `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=`, and the notes cell
  becomes `Use this, NOT `./build.ps1`, during agent work (avoids `out/` contention). `-p:ModuleId=` is what actually stops the copy into the game; see the caveat below.`
- Line 42: the command becomes `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=` and
  the notes cell becomes `Add `--filter "FullyQualifiedName~X"` to narrow.`
- Line 43: the command becomes
  `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "TestCategory=BindingVerification"`
  (notes cell unchanged).

**Verify**: `grep -n -E "dotnet (test|build)" docs/ai-includes/agent-operating-manual.md | grep -v -- "-p:ModuleId="`
prints nothing; `git diff --numstat -- docs/ai-includes/agent-operating-manual.md` prints
`3	3	docs/ai-includes/agent-operating-manual.md`; `python tools/lint_docs.py` exits 0.

### Step 12: CHANGELOG entry and the second commit

1. Insert above the Step 9 entry (same date heading; Edit tool; the `50` and `67` are the Step
   1.6 figures, so use yours if they differ, in the commit body too):
   ```markdown
   ### docs(readme): v2.0.30 - drop stale counts, fix version and test command

   The README on this branch now names Bannerlord v1.5.3 (it said v1.4.8 twice and v1.5.2 in the
   player install section), says development happens on `bannerlord-1.5.x`, and gives the
   non-deploying test command, `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=`
   (the bare `dotnet test TAOM.Tests` builds Main and copies it into the game). The hand-kept
   engineering counts (feature modules, tests, GameModel overrides, Harmony categories, skills,
   agents, hooks, rules, MCP servers, reviews, ADRs, feature docs) are gone rather than refreshed,
   since nothing recomputes them; the career count said 50 against 67 in `taom_careers.xml`.
   `docs/ai-includes/agent-operating-manual.md` now passes `-p:ModuleId=` in its build, test and
   binding-gate rows. The default branch `bannerlord-1.4.5` keeps its own README.
   ```
2. `git add README.md docs/ai-includes/agent-operating-manual.md CHANGELOG.md`
3. Message file:
   ```text
   docs(readme): v2.0.30 - drop stale counts, fix version and test command

   README: Bannerlord v1.5.3 in all three places (was v1.4.8 twice and
   v1.5.2), development branch bannerlord-1.5.x, the setup script's
   real job, and the non-deploying dotnet test command. Hand-kept
   engineering counts removed rather than refreshed; nothing computes
   them and several were wrong (50 careers vs 67).

   agent-operating-manual.md: build, test and binding-gate rows now pass
   -p:DisableModuleCopy=true -p:ModuleId=.
   ```
4. `git -C E:/repos/wt-016-repo-hygiene commit -F <message file>` (write a fresh message file, or
   overwrite the Step 9 one with the Write tool)

**Verify**: `git log -1 --stat --format=%s` lists exactly `CHANGELOG.md`, `README.md` and
`docs/ai-includes/agent-operating-manual.md`; the subject is at most 72 characters
(`git log -1 --format=%s | awk '{print length}'`); `git status --short` prints nothing.

### Step 13: Final gates

```bash
cd E:/repos/wt-016-repo-hygiene && python tools/lint_docs.py; echo "lint=$?"
cd E:/repos/wt-016-repo-hygiene && python tools/audit_claude_config.py | sed -n '1,3p'
cd E:/repos/wt-016-repo-hygiene && dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=
git -C E:/repos/wt-016-repo-hygiene log --oneline bannerlord-1.5.x..HEAD
git -C E:/repos/wt-016-repo-hygiene diff --stat bannerlord-1.5.x...HEAD
```

(The diff uses three dots on purpose: it compares against the merge base, so commits another
session adds to `bannerlord-1.5.x` meanwhile do not show up.)

**Verify**: `lint=0`; audit `HIGH:1  MED:0`; the test run fails nothing except
`TheElkItem_DeclaresTheScaleTheReachIsTunedFor` and
`AnimaliaActionSets_BindOnlyHorseActions_ToClipsThatExist` (either may pass if the live Armory has
been fixed meanwhile), with 2 skipped (10,239 total at a `4b5662b2` base); two commits; the diff
stat names only in-scope paths. Put the actual test counts in your report (not in the CHANGELOG,
which is already committed).

## Test plan

- No C# changes, so no new unit tests. The regression surface is configuration and docs, gated by:
  JSON/TOML parsers (Steps 3, 4), registry lookups (Step 2), real `uvx` launches of the two
  `uvx`-pinned servers that print help (Step 5), `tools/audit_claude_config.py` (Steps 1, 5, 13),
  `git ls-files` / `git check-ignore` (Step 6), greps (Steps 7, 8, 10, 11), `tools/lint_docs.py`
  (Steps 8, 11, 13), and the full suite as a sanity check (Step 13).
- Structurally untestable here (name it in the `Not-tested:` trailer): the servers starting inside
  Claude Code and Codex, and the deny list taking effect from `.claude/settings.json`, both of which
  need a client restart on a checkout that has the merge. Mike's post-merge check: restart Claude
  Code, run `/mcp` (serena, filesystem, git connected) and `/permissions` (the nine denies listed).

## Done criteria

Machine-checkable, from the worktree. ALL must hold:

- [ ] `git ls-files .claude/settings.local.json _taom_loc.pkl crashz` prints nothing
- [ ] `git check-ignore -q .claude/settings.local.json && git check-ignore -q _taom_loc.pkl && git check-ignore -q crashz/report.json && echo ok` prints `ok`
- [ ] `python -c "import json;print(len(json.load(open('.claude/settings.json',encoding='utf-8'))['permissions']['deny']))"` prints `9`
- [ ] `grep -c -E "serena@949a27ef1e5fda1a6e7b561e777bcece345c6ffd|server-filesystem@2026\.8\.31|mcp-server-git@2026\.8\.18|elevenlabs-mcp@0\.12\.2" .mcp.json` prints `4`, and the same pattern over `.codex/config.toml` prints `2`
- [ ] `python tools/audit_claude_config.py | grep -c mcp-npx-unpinned` prints `0`
- [ ] `git grep -n "crashz/report.json" -- .claude/skills docs/migration/v1.4.8-impact.md | grep -vc "git show b2e387db:crashz/report.json"` prints `0`
- [ ] `grep -n -E "v1\.4\.8|v1\.5\.2" README.md` prints exactly one line, containing `bannerlord-1.4.5`
- [ ] The Step 10 count grep over `README.md` prints nothing
- [ ] `grep -n -E "dotnet (test|build)" README.md docs/ai-includes/agent-operating-manual.md | grep -v -- "-p:ModuleId="` prints nothing
- [ ] `python tools/lint_docs.py` exits 0
- [ ] `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=` fails only the two known Armory tests (or none)
- [ ] `git log --format=%B bannerlord-1.5.x..HEAD | grep -ci co-authored` prints `0`; both subjects are at most 72 characters
- [ ] `git status --short` prints nothing, and `git diff --stat bannerlord-1.5.x...HEAD` names only in-scope paths
- [ ] `git grep -n "](.*settings\.local\.json)" -- ':!plans'` prints nothing
- [ ] `plans/README.md` is untouched in both trees, and your report ends with the status the 016 row should get

## STOP conditions

Stop and report back (do not improvise) if:

- Step 0 was not done, or its check does not print `9 True False`. Never edit `settings.json` or
  `settings.local.json` yourself, never create `/tmp/claude-config-override-*`, and never rewrite
  either file from a shell or Python command: that is bypassing a safety gate.
- `.claude/settings.local.json` in the worktree holds anything shared beyond what "Current state"
  lists (for example a `hooks` block, an `env` block, or deny rules other than the nine): moving it
  is not in this plan; report the key names.
- The drift check prints anything, or any "Current state" excerpt does not match the worktree file.
- A registry lookup, `npm`, `uvx` or `git ls-remote` fails, or a pinned version does not exist
  (environment failure: report the exact error; do not pick a different version).
- The serena `--help` launch fails or its help lacks `--context` or `--project`; or the
  `mcp-server-git` launch fails. Do not change the context name or switch serena to PyPI.
- The audit still reports `mcp-npx-unpinned`, or reports any new HIGH or MED finding other than the
  known `secret-generic` fixture at `tools/tests/test_audit_repo_secrets.py:178`.
- The Step 6.1 dry run (`git rm --cached -r -n ...`) prints anything other than the six named
  `rm '...'` lines, or `git status` after Step 6 shows a path outside the Step 6 list.
- `git grep` finds a reader of `_taom_loc.pkl` or `crashz/` beyond the three cited docs (a script or
  test that would break once the files leave clones).
- A commit hook denies or asks for confirmation. Never use `--no-verify`; report the hook's message.
- The test run fails anything other than the two known Armory tests: this plan changes no C#, so a
  new failure means the worktree or the install is not what the plan assumed.
- Any step appears to need `Main/IoC.cs`, `Main/SubModule.cs`, `Main/TAOM.csproj`,
  `Directory.Build.props`, `tools/audit_claude_config.py` or any path under `E:/repos/TAOM`.

## Maintenance notes

- **Merging deletes local copies.** The untrack commit removes `.claude/settings.local.json`,
  `_taom_loc.pkl` and `crashz/` from the working tree of every checkout that merges or pulls it: Mike's
  desktop main tree, the laptop, and each existing worktree (`E:/repos/taom-145`, `taom-635`,
  `taom-camps`, `taom-enlist-fixes`, `wt-634-145`, `wt-mumakil`, the scratchpad `wt-duty`) when it
  next takes the commit. Before merging, copy `.claude/settings.local.json` aside; afterwards restore
  it (or run `git show b2e387db:.claude/settings.local.json > .claude/settings.local.json`). If the
  local copy has uncommitted edits at merge time, git refuses the merge instead ("would be
  overwritten"), which is safe: save the edits, then merge. The pickle and the crash bundle need no
  restore; both stay in history.
- **Trust after the merge**: a fresh clone no longer inherits `enabledMcpjsonServers`, so Claude Code
  asks each developer to approve the project MCP servers; that is the intended behaviour.
- **Pins go stale by design.** Bump them deliberately: re-resolve from the registries (the Step 2
  commands without the version), update `.mcp.json` and `.codex/config.toml` together, and add a
  CHANGELOG line. Serena's `--context ide-assistant` is deprecated upstream (mapped to `claude-code`
  at `v1.7.0`); rename it on the next bump rather than now.
- **Plan 011** also inserts into `.claude/settings.json` (hook groups near lines 91 and 154). This
  plan's block sits near the top (after `attribution`), so the two do not overlap; whichever lands
  second rebases cleanly unless someone reformats the file.
- **Reviewer focus**: that the moved deny list is byte-identical to the local one (Step 0.3 checks
  the set); that no allow rule or trust list leaked into `settings.json`; that each pin string is
  exactly the resolved version; the README line 19 sentence and the player-install version.
- **Deferred follow-ups** (each its own change): teach `tools/audit_claude_config.py` a `uvx`
  unpinned rule and a `.codex/config.toml` scan; a `lint_docs.py` check that the root `README.md`
  names the pinned Bannerlord version and that every `dotnet (build|test)` line in `README.md`,
  `docs/ai-includes/`, `.ai/` and `.agents/` carries `-p:ModuleId=`; purging the pickle from history
  (Mike's decision; it would rewrite every SHA); the out-of-scope product counts in the README.
