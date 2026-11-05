# Plan 142: Take per-user files out of git, pin the MCP servers, sync the process docs and lessons, then union-merge the lessons files

> **Executor instructions**: Follow this plan Part by Part, step by step. Run every verification
> command and confirm the expected result before moving on. If anything in "STOP conditions" occurs,
> stop and report; do not improvise. Do not edit `plans/README.md`: report your status and the
> numbers this plan asks you to record to the integrator, who maintains the index.
>
> **Drift check (run first, in the worktree made in Step A0)**:
> `git -C C:/Users/mikew/source/repos/taom-port/142 diff --stat 1b00d1acd..HEAD -- .gitignore .gitattributes .mcp.json .codex/config.toml README.md CONTRIBUTING.md .github CLAUDE.md .claude/skills/engine-bump .claude/skills/native-crash-triage .claude/skills/release .claude/rules/moduledata-validation.md docs tools/README.md tools/check_external_xslt.py`
> Many of these paths were changed by WPs 101 to 141 (expected). Compare each live file against the
> anchors this plan quotes before editing it; STOP only when an anchor this plan edits is missing.

## Status

- **Priority**: P2
- **Effort**: L (Parts A to F, about 20 commits; Part E grows with the number of WPs merged before it)
- **Risk**: MED (Part A deletes the tracked `.claude/settings.local.json` from every checkout that merges it; Part F changes how git merges every lessons file from then on)
- **Depends on**: WP141 merged (wave 5 starts after waves 0 to 4 are merged). Step A0 checks a marker of WP102, WP103, WP104, WP115, WP116, WP137 and WP141 and STOPS if one is missing
- **Category**: docs / dx / security (repo hygiene)
- **Wave / lane**: wave 5, lane G (WP143 waits for this WP; WP144 may run beside it)
- **Executor**: laptop; an ordinary Claude Code session started in the main checkout `C:\Users\mikew\source\repos\TAOM`, working in its own worktree by absolute path
- **Decisions applied**: D1 (no step stages `CHANGELOG.md`; the commit body is the entry), D2 (Step E2's rows describe module wiring where a WP ported a module), D3 (Serena read tools kept; its 16 edit tools stay denied as WP103 set them), D7 (co-op docs only get count corrections), D8 (subjects carry `v2.0.28`; release docs use the `v2.4.0` numbering), D9 (no CI; cite 1.4.x's own `ccef172ad`), D10, D18 (the `merge=union` line is the last commit, never on `CHANGELOG.md`), D19 (git through the Bash tool only), D20 (no ModuleData change in this WP). Two calls this plan makes under the 2026-11-04 delegation: **W1** the TAOM.Dependencies `<Version>` moves with every 1.4.x release from `v2.4.0` on, so all four modules read the release number (the public `v2.0.29.5` build already labels all four that way by hand); **W2** `.mcp.json` pins Serena to commit `7a2968335f2198b966864de1ce3655c8e485a653`, the commit WP103's 16-entry deny list was derived from.
- **Planned at**: 1.4.x `origin/bannerlord-1.4.5` `1b00d1acd` and 1.5.x `origin/bannerlord-1.5.x` `e45f6df2c`, 2026-11-04
- **Issue**: port of #657 (plan 016), #696, #641, #765, #792, #793, #371, #805, #806, #809, #812, #826, #763, #1026, #1038. Part E (bookkeeping sync), W1 and the outside-PR route have no 1.5.x issue: the integrator creates the tracking issue before this WP is handed out (`gh issue create` is outward, so never the executor's), and M4 closes it after the push.

## Why this matters

`.claude/settings.local.json` is tracked on this line: it carries 35 personal allow rules, three desktop
`E:\` folders in `additionalDirectories` and the MCP trust list (`enabledMcpjsonServers`, 7 servers), so
every clone silently trusts every project MCP server, and a 37 MB pickle (`_taom_loc.pkl`) and a player
crash bundle (`crashz/`) ride along. The MCP servers run whatever upstream ships that day: Serena from
`git+https://github.com/oraios/serena` with no commit, `elevenlabs-mcp` and Codex's
`server-filesystem` and `mcp-server-git` with no version. WP103 denied Serena's 16 editing tools from
the list one commit marks can-edit, so an unpinned Serena can grow an editing tool nothing denies.
The README tells players to install Harmony and MCM separately although TAOM.Dependencies bundles them,
which `docs/migration/dr3-maintenance.md:309` says must be undone; the release docs cannot resolve the
public build's hand-typed `v2.0.29.5` (in no commit, per `git log --all -S`); PRs already reach this
branch (PR #1064 is `1b00d1acd`) with no contributor route. Many feature and modding docs disagree
with the 1.4.x code (examples below, each measured), and the earlier WPs left their lessons, feature-map
rows and tool rows to this WP (36 of the other plans written by 2026-11-04 defer something to it). Finally, the lessons files are append-only and a conflict hot spot for
every port: after this WP they merge with git's union driver.

## Current state (read this session on `origin/bannerlord-1.4.5` 1b00d1acd unless marked)

**Prerequisite markers** (Step A0 checks each in the worktree):

| WP | Marker |
|---|---|
| 102 (plan 020) | `tools/changelog_from_commits.py` exists; `grep -c check-changelog .claude/settings.json` prints 0 |
| 103 (gate contract) | `git grep -c hookSpecificOutput -- .claude/hooks` finds hits; `tools/check_commit_msg.py` exists; `.claude/settings.json` denies `mcp__serena__replace_content` |
| 104 (plan 009) | `git grep -n "bool TryPatchCategory" -- Main/SubModule.cs` finds one line |
| 115 (release integrity) | `tools/check_release_pairing.py` exists |
| 116 (hook end state) | `.claude/hooks/_pushjudge.py` exists |
| 137 (dev tools) | `tools/tests/test_taom_src_short_names.py` exists |
| 141 | `git -C C:/Users/mikew/source/repos/TAOM branch --merged bannerlord-1.4.5 --list "port148/141-*"` prints one branch |

**Files and their state at 1b00d1acd**

- `.gitignore`: ends at `credentials.json` (line 236); nothing ignores `.claude/settings.local.json`, `*.pkl` or `/crashz/`.
- Tracked and to be untracked: `.claude/settings.local.json`, `_taom_loc.pkl`, `crashz/{manifest.txt,report.json,report.txt,rgl_log.txt}`. Nothing in `tools/`, hooks or tests reads the pickle or `crashz/` (`git grep`).
- `crashz/report.json` citations: `.claude/skills/engine-bump/SKILL.md:29`, `.claude/skills/native-crash-triage/SKILL.md:74`, `docs/migration/v1.4.8-impact.md:136`.
- `.mcp.json`: after WP116 Step C2 the servers are `elevenlabs, ilspy, imagine, serena, taom-moduledata`; Serena's `--from` is `git+https://github.com/oraios/serena`, ElevenLabs runs `elevenlabs-mcp`, both unpinned.
- `.codex/config.toml:12-27`: `[mcp_servers.filesystem]` runs `@modelcontextprotocol/server-filesystem` on desktop `E:\` folders; `[mcp_servers.git]` runs `mcp-server-git`; neither pinned. WP116 does not touch this file.
- `.vscode/mcp.json.example`: WP138 deletes it (`b84dbaeb2`). This plan does not touch it.
- `docs/features/kingdom-voices.md:379-388`: the `.mcp.json` snippet, `"args": ["elevenlabs-mcp"],` at :382; :395 `- Requires \`uvx elevenlabs-mcp\` installed ...`.
- `docs/features/moduledata-validation.md:595`: links `[.claude/settings.local.json](../../.claude/settings.local.json)` (a dead link once untracked).
- `docs/reference/development-machines.md:31-42`: "**Three things ignore all of that**"; the third bullet says `.claude/settings.local.json` is tracked. WP101 added a dated laptop paragraph to this file: keep it. WP116 edited the `.mcp.json` bullet.
- `docs/reference/mcp-servers.md`: WP103 added a Serena paragraph to `## Denied write tools` ending "this line runs Serena unpinned, so re-derive it when Serena adds an editing tool."; WP116 removed the github, git and filesystem rows and called the nine git and filesystem deny entries in `settings.local.json` inert. `## Configuration` (planning :65) says servers "must be listed in `.claude/settings.local.json → enabledMcpjsonServers` to be trusted".
- `docs/INDEX.md:175`: "what ignores them (`.mcp.json`, the decompile scripts, the tracked `settings.local.json`)".
- `README.md`: :15-17 hand-kept counts ("58 feature modules · 39 GameModel overrides ..."), :19 branch note, :55 setup comment, :58 `dotnet test TAOM.Tests     # tests only` (deploys on this line), :72-79 counts in the tree, :125 "50 careers across 16 cultures", :149-150 counts and "catalogued in [CLAUDE.md]", :156-163 harness counts, :168-176 "Installing to Play" lists Harmony and MCM as separate modules, :178-184 Contributing.
- `setup-dev-env.ps1:7` prompts for the install path (`Read-Host`) and `:20` sets `BANNERLORD_GAME_DIR`.
- `Dependencies/_Module/SubModule.xml`: `<Version value="v2.0.6" />` (:6); SubModules UIExtenderEx (:151), ButterLib (:173), MCMv5 (:203). `Main/_Module/SubModule.xml:6` `v2.0.28`; #371 comment :15-22, `<DependedModule Id="TAOM.Dependencies" />` :23, metadata mirror :30 `version="v2.0.6"`, `<XmlName id="Kingdoms" path="taom_spkingdoms"/>` :142. WP115 reorders the DependedModules block, so re-measure every manifest line at execution.
- `Dependencies/TAOM.Dependencies.csproj:90-92`: `DeployTAOMDependenciesStubs` runs on `Exists($(GameFolder)) AND $(DisableModuleCopy) != 'true'` (no ModuleId condition); `Main/TAOM.csproj:133-136` says `DisableModuleCopy` does not gate `CopyModule`.
- `.ai/policy.md:78-79`: "Retain the existing prohibition on untrusted PR execution on the personal self-hosted game workstation." `AGENTS.md` sections: Start here, Always, Code Review Rules, Codex workflow skills (WP102 adds "Documentation duty").
- `CLAUDE.md:57` "41 of the 43 skill **descriptions load eagerly**"; 43 skill folders, 2 with `disable-model-invocation: true` (finish-branch, lint-cleanup-loop). Routing table header at :72; rows like :100 (`/release`). `tools/lint_docs.py:49` caps CLAUDE.md at 46,000 bytes on disk; WP103 left a few hundred bytes of headroom.
- Lessons: 13 files under `docs/reviews/lessons/`, 696 `### ` entries at 1b00d1acd (`grep -c '^### '` per file); `docs/reviews/LESSONS-LEARNED.md` lists stale per-file counts (for example adapters 43 against 50) and "all 650 lessons"; the 1.5.x line dropped those counts in `43db0b47a` (WP138 left that hunk to this WP). None of the lesson headings Part D and Step C5 port exists on 1.4.x (`git grep -F` per heading).
- `.gitattributes`: line-ending rules only (`*.sh`, `*.bash`, `docs/reference/ranged-troops.html`; WP103 adds `.githooks/*`); no `merge=` attribute anywhere.
- `tools/validate_moduledata.py:78`: `_EXTRA_REF_MODULES = ("LOTRLOME_Armory", "TAOM_Map")`, so the ModuleData commit hook also reads the live modules.

**Engine facts the ported docs state** (installed v1.4.8 via `pwsh -NoProfile -File tools/taom-src.ps1 path <Type>`, cross-checked in `C:\Decompiled_Bannerlord\_categories_v1.4.8`):

- `TaleWorlds.Core.Equipment.cs:184` `public void FillFrom(Equipment sourceEquipment, bool useSourceEquipmentType = true)`; the body copies `this[i] = sourceEquipment[i]` for all 12 slots, empty ones included (:190-193).
- `TaleWorlds.Core.Equipment.cs:204` `DeserializeNode`: looks the id up with `GetObject<ItemObject>` and assigns when `IsItemFitsToSlot` is true; `IsItemFitsToSlot` returns true for a null item (:445-451), so an empty id clears the slot.
- `TaleWorlds.Core.BasicCharacterObject.cs:315` `public override void Deserialize(MBObjectManager objectManager, XmlNode node)`; the `<Equipments>` block collects direct `<equipment>` children and calls `_equipmentRoster.AddOverriddenEquipments(objectManager, list)` at :412 (dump lines 360-412).
- `TaleWorlds.Core.MBEquipmentRoster.cs:121` `public void AddOverriddenEquipments(MBObjectManager objectManager, List<XmlNode> overridenEquipmentSlots)`: clones every equipment, then deserializes every override node into every equipment, battle and civilian (:121-136).
- `TaleWorlds.CampaignSystem.Party.PartyBase.cs:343` `public int PartySizeLimit`, cached against `MemberRoster.VersionNo` (:347-351); dump `PartyBase.cs:381` `public int NumberOfAllMembers => MemberRoster.TotalManCount;`.
- `TaleWorlds.CampaignSystem.GameComponents.DefaultClanTierModel.cs:11` `TierLowerRenownLimits = { 0, 50, 150, 350, 900, 2350, 6150 }`; `CalculateInitialRenown` :43-49 rolls `MBRandom.RandomInt(floor, next - (next - floor) * 0.4f)`, tier 6 using `6150 + 1500` as next.
- Dump `TaleWorlds.CampaignSystem.Map/LocatorGrid.cs:131` and `:139` skip a locatable when `DistanceSquared(...) >= RadiusSquared` (a half-open radius).
- Git 2.54 `gitattributes` documentation ("Performing a three-way merge"): the `merge` attribute applies to `git merge`, `git revert` and `git cherry-pick`; `union` takes lines from both sides instead of conflict markers, in no guaranteed order.

**Conventions that bind this plan**

- No C# changes. ADR-007 (services never touch sealed TaleWorlds types), ADR-002 (entry points under 150 lines), ADR-003 (no `#region`), ADR-004 (no `[Obsolete]`), ADR-005 (no `#if DEBUG` outside IoC registration), the MCM json2 rule (rename a persisted setting, never flip its default) and `.claude/rules/harmony-patches.md` (MovementOrder postfixes use the shared deferred category) are not exercised; if a step seems to need C#, STOP.
- TDD for the one behaviour change (Part F): the union test is written and seen RED first.
- Config protection: `.claude/settings.json`, `.claude/settings.local.json`, `Directory.Build.props` and `docs/adrs/*.md` are changed by the maintainer only (M1, M3). You stage and commit the maintainer's change; never write those files yourself by any route.
- `.claude/rules/output-style.md` Part 2: no em or en dash in anything you write (the dash check below enforces it). Never a scratchpad path in a committed file.
- Porting rule for every doc edit: a 1.5.x sentence lands only when each fact in it is true on this tree. Re-measure every count, line number and version with the command this plan gives; write the value you measured, not the planning value. Never carry a 1.5.x engine version, a 1.5.x-only feature, file, culture or realm, or `TAOM.Client`.
- Lessons format (`docs/reviews/LESSONS-LEARNED.md` "How to use"): a `### ` title, then `**Why missed:**`, `**Prevent:**`, `**Source:**`, appended at the end of the category file. A ported lesson's Source names its 1.5.x record as text, never as a link (those RCAs are not on this line): ``- **Source:** 1.5.x `docs/reviews/rca-<name>.md` finding N, on `origin/bannerlord-1.5.x` (ported by WP142).``

## Commands you will need

Every Bash call starts with `export WT=C:/Users/mikew/source/repos/taom-port/142 MSYS_NO_PATHCONV=1 PYTHONIOENCODING=utf-8 && cd "$WT" && `
(a `cd` outside the main checkout does not persist). Git only through the Bash tool (D19). Pass
`timeout: 600000` on calls that run `dotnet`, the hook suite, the tool tests or the full doc lint.
Inside the tables of this plan, `\|` is markdown's escape for a pipe: type `|`. Inside a quoted old or
new string, `` \` `` stands for a literal backtick: type the backtick, not the backslash.

| Purpose | Command | Expected |
|---|---|---|
| Restore | `dotnet restore TAOM.sln` | exit 0 |
| Build | `dotnet build TAOM.sln -p:DisableModuleCopy=true -p:ModuleId=` | exit 0, 0 errors |
| Test | `dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId=` | 0 failed; total equals the Step A0 baseline (this WP adds no C# test) |
| Filtered | `dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId= --filter FullyQualifiedName~SettingsFingerprintTests` | 0 failed |
| Tool tests | `python -m unittest discover -s tools/tests -t .` | no failure the Step A0 baseline lacked; `Ran` = baseline + 2 after Part F |
| Hook suite | `bash tools/test_hooks.sh` | no failure the Step A0 baseline lacked |
| Docs | `python tools/lint_docs.py --summary` | no dead link and no finding in a touched file that the Step A0 baseline lacked |
| Drift gate | `python tools/lint_docs.py --drift-only` | exit 0 (CLAUDE.md under its cap) |
| Doc graph | `python tools/check_doc_graph_ratchet.py` | exit 0 (never pass `--update`) |
| Contract | `python tools/reviewctl.py lint` | exit 0 |
| Config audit | `python tools/audit_claude_config.py --min HIGH` | no HIGH the Step A0 baseline lacked |
| Message | `python tools/check_commit_msg.py C:/Users/mikew/source/repos/taom-port/scratch/142/msg.txt` | exit 0, no output |
| Dash check | the command below | prints `0` before every commit |

Dash check (after staging, before every commit; a nonzero count means an added line carries an em or
en dash: rewrite it with a comma, colon, semicolon, parentheses or a new sentence):

```bash
git -C "$WT" diff --cached -U0 | python -c "import sys; t=sys.stdin.buffer.read().decode('utf-8','replace'); print(sum(1 for l in t.splitlines() if l[:1]=='+' and ('\u2014' in l or '\u2013' in l)))"
```

No C# changes, so run Restore, Build and Test at Step A0 and at the end of Parts C, E and F only
(C# tests read some docs: `SettingsFingerprintTests` reads `coop-interop.md` and
`bannerlord-together-compat.md`). Never `./build.ps1`. Never two builds at once in this worktree.

## Scope

**In scope** (the only files you modify):
- Hygiene: `.gitignore`, `.gitattributes` (Part F), `.mcp.json`, `.codex/config.toml`; untracking `_taom_loc.pkl` and `crashz/`; `.claude/settings.local.json` untracked and, only in the Step A0 "servers kept" branch, `.claude/settings.json`, both by the maintainer (M1)
- `README.md`, new `CONTRIBUTING.md`, new `.github/pull_request_template.md`, new `.claude/skills/review-outside-pr/SKILL.md`, `CLAUDE.md` (one routing row and the skill-count sentence only)
- `.claude/skills/engine-bump/SKILL.md`, `.claude/skills/native-crash-triage/SKILL.md` (crashz citation; engine-bump also the Gates item), `.claude/skills/release/SKILL.md`, `.claude/rules/moduledata-validation.md` (one section)
- `docs/adrs/001-xml-config.md`, `docs/adrs/README.md`: by the maintainer (M3)
- `docs/features/*.md`, `docs/modding/*.md`, `docs/reference/**/*.md`, `docs/ai-includes/completion-workflow.md`, `docs/ai-includes/agent-operating-manual.md`, `docs/migration/v1.4.8-impact.md`, `docs/INDEX.md`, `docs/reviews/lessons/*.md`, `docs/reviews/LESSONS-LEARNED.md`, `docs/reviews/rca-memory-instruments-2026-09-12.md` and `docs/investigations/native-commit-audit-2026-08.md` (Step C6): only the edits the steps name
- `tools/README.md`, `tools/check_external_xslt.py` (docstring only), new `tools/tests/test_lessons_merge_union.py`

**Out of scope** (do not touch):
- `CHANGELOG.md` (D1); any C#, `Main/IoC.cs`, `Main/SubModule.cs`, `Main/TAOM.csproj`, `Directory.Build.props`; any ModuleData (D20); `docs/reviews/REVIEW-LOG.md` and every 1.5.x review record, RCA or Codex prompt (not carried to this line; the 1.4.x RCA Step C6 corrects is already on this line)
- `.vscode/mcp.json.example` (WP138 deletes it), `.claude/settings.json` beyond M1, `.claude/hooks/**`, `.claude/rules/**` beyond the one section, `AGENTS.md`, `.ai/**` (WP143's harness sync)
- `plans/**` (integrator), the main checkout's uncommitted `.serena/project.yml` and `stash@{0}` (never pop, drop or apply it)
- Running `tools/build_backlinks.py` (generated footers stay as they are; 1.5.x `6e886fe8d` is skipped)

## Git workflow

- Worktree (Step A0): `git -C C:/Users/mikew/source/repos/TAOM worktree add C:/Users/mikew/source/repos/taom-port/142 -b port148/142-docs-process-sync bannerlord-1.4.5`, then `mkdir -p C:/Users/mikew/source/repos/taom-port/scratch/142` (outside the repo: commit messages and helper scripts).
- Methods: most steps are hand edits with the Edit tool. New files start from a 1.5.x blob written with Git Bash (`git -C "$WT" show <sha>:<path> > "$WT/<path>"`; Bash writes bytes unchanged, PowerShell 5.1 `>` would write UTF-16). One cherry-pick: `git -C "$WT" cherry-pick -n 4b7e36250` in Step F2. Never let `cherry-pick`, `--continue`, `-C`, `-c` or `--amend --no-edit` create a commit.
- Stage explicit paths with `git -C "$WT" add <paths>` in one Bash call; run the dash check and the message check; then `git -C "$WT" commit -F C:/Users/mikew/source/repos/taom-port/scratch/142/msg.txt` in the NEXT sequential Bash call. Never `git add -A`, `-u` or `.`; never `--no-verify`.
- Commit, never push, PR, tag or merge. Subject `<type>(<scope>): v2.0.28 - <description>`, at most 72 characters, where `v2.0.28` is the `<Version>` in `Main/_Module/SubModule.xml` (if a release moved it, use the new value and recheck the length). The 1.5.x type and scope are kept where the whole commit is ported; a hunk taken out of a feature commit takes the scope of what it changes (for example `docs(workflow)` for `4e7efe5a8`'s paragraph). Body:

```
<type>(<scope>): v2.0.28 - <description>

<What changed on the 1.4.8 line and why, wrapped at 72, written for
1.4.x readers; never a pasted 1.5.x body. No em or en dashes. No
Co-Authored-By or "Generated with" line.>

Research: <what was measured or read on v1.4.8 or this tree>
Not-tested: <what needs a live check, when relevant>
Ported-from: <40-hex sha>          (one line per 1.5.x commit taken)
```

## Part A: per-user files out of git, MCP servers pinned (3 commits)

### Step A0: preflight and baselines (no commit)

1. Create the worktree (Git workflow). Run the drift check from the header; read it.
2. Check every prerequisite marker in the table above; any miss is a STOP.
3. `python -c "import json;print(sorted(json.load(open('.mcp.json',encoding='utf-8'))['mcpServers']))"`.
   - Prints `['elevenlabs', 'ilspy', 'imagine', 'serena', 'taom-moduledata']`: branch **servers removed** (expected).
   - Also lists `filesystem` and/or `git`: branch **servers kept** (WP116 Step C2 did not land). Steps A1 and A2 say what changes.
   - Any other name: STOP.
4. `git ls-files .claude/settings.local.json _taom_loc.pkl crashz` lists 6 paths. Fewer: STOP (someone untracked them already).
5. `grep -o '<Version value="[^"]*"' Main/_Module/SubModule.xml | head -1` prints the version your subjects carry (expected `v2.0.28`).
6. Baselines, recorded for your final report (not in a repo file): Restore, Build, Test totals; `bash tools/test_hooks.sh 2>&1 | tail -4`; `python -m unittest discover -s tools/tests -t . 2>&1 | tail -4`; `python tools/lint_docs.py --summary`; `python tools/check_doc_graph_ratchet.py; echo rc=$?`; `python tools/audit_claude_config.py --min HIGH 2>&1 | tail -6`; and the lesson census `for f in docs/reviews/lessons/*.md; do echo "$(grep -c '^### ' $f) $f"; done`.

### Step A1: untrack the per-user settings, the pickle and the crash bundle (470ee64e9, 1ce8c370e restore note)

1. **Maintainer step M1** (see "Maintainer steps"). Ask the maintainer to run it and tell you when done. It (a) backs up the maintainer's settings file, (b) runs `git -C C:/Users/mikew/source/repos/taom-port/142 rm --cached .claude/settings.local.json`, and (c) only in branch **servers kept**, adds the nine git and filesystem denies to `.claude/settings.json`. Resume when `git -C "$WT" status --porcelain` shows `D  .claude/settings.local.json` (and `M .claude/settings.json` in branch servers kept) and nothing else staged.
2. `git -C "$WT" rm -r --cached -q _taom_loc.pkl crashz` (the files stay on disk, untracked).
3. `.gitignore`: append after the last line, with one blank line before it:

```
# Per-user and generated files that were once tracked by mistake.
# settings.local.json is Claude Code's per-user settings slot: personal allow rules,
# machine paths and each developer's MCP trust list (enabledMcpjsonServers). Rules
# every clone needs go in settings.json.
.claude/settings.local.json
# Python pickles are never repo content, and loading one runs code. The only one
# ever tracked was a one-off localization snapshot that nothing read.
*.pkl
# Player crash bundles unpacked for triage. The v1.4.7 report once tracked here
# stays readable from history: git show 1b00d1acd:crashz/report.json
/crashz/
```

4. Repoint the three `crashz/report.json` citations (find each with `git -C "$WT" grep -n "crashz/report.json" -- .claude docs`):
   - `.claude/skills/engine-bump/SKILL.md` (planning :29-30, one sentence over two lines): `the open player report at \`crashz/report.json\`` + line break + `(\`BannerlordVersion v1.4.7.117484\`) can no longer be triaged` becomes `the open player report (untracked; read it with` + line break + `\`git show 1b00d1acd:crashz/report.json\`; \`BannerlordVersion v1.4.7.117484\`) can no longer be triaged` (keep the three-space indent of the continuation line).
   - `.claude/skills/native-crash-triage/SKILL.md` (planning :74): `the open player report at \`crashz/report.json\` (\`BannerlordVersion` becomes `the open player report \`crashz/report.json\` (untracked; \`git show 1b00d1acd:crashz/report.json\`; \`BannerlordVersion`.
   - `docs/migration/v1.4.8-impact.md` (planning :136): `the open report at \`crashz/report.json\` (\`v1.4.7.117484\`)` becomes `the open report \`crashz/report.json\` (\`v1.4.7.117484\`; untracked, read it with \`git show 1b00d1acd:crashz/report.json\`)`.
5. `docs/features/moduledata-validation.md` (activation step 2, planning :595): replace `and enabled in [\`.claude/settings.local.json\`](../../.claude/settings.local.json) → \`enabledMcpjsonServers\`.` with `; each developer enables it in their own untracked \`.claude/settings.local.json\` → \`enabledMcpjsonServers\`.` (the link would be dead in a fresh clone).
6. `docs/reference/development-machines.md`:
   - Replace the bullet that starts ``- `.claude/settings.local.json` is **tracked**`` (two lines) with ``- `.codex/config.toml` hardcodes its own list of desktop `E:\` paths for Codex's `filesystem` server, with the same consequence on the laptop.`` The count stays "**Three things**" (decompile scripts, `.mcp.json`, `.codex/config.toml`).
   - In the `.mcp.json` bullet as WP116 left it, `in the tracked \`.claude/settings.local.json\`` becomes `in its own untracked \`.claude/settings.local.json\``. If that phrase is absent, leave the bullet.
   - After the list, add one paragraph: ``.claude/settings.local.json` is per-user and untracked (since WP142 on this line; the 1.5.x line untracked it on 2026-09-25): each checkout, every worktree included, keeps its own copy (personal allow rules, `additionalDirectories`, `enabledMcpjsonServers`). A new worktree starts without one, so copy yours in. A checkout of a commit before the untrack (`1b00d1acd` and older) still tracks the file: switching to one overwrites your copy and switching back removes it, so keep a copy outside the repo. `git restore --source=1b00d1acd -- .claude/settings.local.json` brings back the last tracked version. Anything every clone needs lives in the tracked `.claude/settings.json`.``
7. `docs/INDEX.md` (development-machines line, planning :175): `what ignores them (\`.mcp.json\`, the decompile scripts, the tracked \`settings.local.json\`)` becomes `what ignores them (\`.mcp.json\`, \`.codex/config.toml\` and the decompile scripts)`.
8. `docs/reference/mcp-servers.md`, `## Denied write tools`: replace WP116's sentence about the nine `mcp__git__*`/`mcp__filesystem__*` entries in `.claude/settings.local.json` with:
   - branch servers removed: ``The nine `mcp__git__*` and `mcp__filesystem__*` entries above lived in `.claude/settings.local.json`, untracked since WP142; both servers left `.mcp.json` with WP116, so no tracked deny names them. If either server comes back, at project or user scope, deny its write tools in the tracked `.claude/settings.json` first, derived from the pinned version's `readOnlyHint: false` annotations.``
   - branch servers kept: ``The nine `mcp__git__*` and `mcp__filesystem__*` entries are under `permissions.deny` in the tracked `.claude/settings.json` (moved there from `settings.local.json` when WP142 untracked it). They are exactly the tools the pinned `git` and `filesystem` versions annotate `readOnlyHint: false`; on a pin bump of either, re-derive them.``
   And in `## Configuration`, replace `must be listed in \`.claude/settings.local.json → enabledMcpjsonServers\` to be trusted` with `each developer trusts them in their own \`.claude/settings.local.json → enabledMcpjsonServers\`; that file is per-user and untracked, because a tracked trust list would approve every server on every clone`.

Stage `.gitignore .claude/skills/engine-bump/SKILL.md .claude/skills/native-crash-triage/SKILL.md docs/migration/v1.4.8-impact.md docs/features/moduledata-validation.md docs/reference/development-machines.md docs/INDEX.md docs/reference/mcp-servers.md` (plus `.claude/settings.json` in branch servers kept; the `git rm --cached` deletions are already staged).

**Verify**: `git -C "$WT" status --porcelain | grep '^D '` lists exactly the 6 untracked paths; `git -C "$WT" check-ignore -q .claude/settings.local.json _taom_loc.pkl crashz/report.json && echo IGNORED` prints `IGNORED`; `git -C "$WT" grep -n "crashz/report.json" -- .claude docs | grep -vc 1b00d1acd` prints `0`; `git -C "$WT" grep -c -F "(../../.claude/settings.local.json)" -- docs` prints nothing; Docs lint shows no new dead link; Hook suite as baseline.

Commit `chore(security): v2.0.28 - untrack per-user settings, pickle and crashz`. Body: the three tracked files and why each must not be shared; the trust list now per user; the crash report stays readable through `git show 1b00d1acd:crashz/report.json`; merging this deletes `.claude/settings.local.json` from each checkout that merges it (M2 restores it). Trailers: `Not-tested: Claude Code start with no tracked settings.local.json (maintainer check after merge)`, `Ported-from: 470ee64e92a2170cbf44ef354d0f685b78585866`, `Ported-from: 1ce8c370e36a1af37a7479b7dd5f49e88029e7b9`, `Ported-from: 35f212c60fe9b981be5eb80ff22d41405606144b`.

### Step A2: pin the MCP servers (470ee64e9, 7d13adc36, 1ce8c370e, 35f212c60)

1. `.mcp.json`: in `serena`'s args, `git+https://github.com/oraios/serena` becomes `git+https://github.com/oraios/serena@7a2968335f2198b966864de1ce3655c8e485a653` (W2); in `elevenlabs`, `"args": ["elevenlabs-mcp"]` becomes `"args": ["elevenlabs-mcp@0.12.2"]`. Branch servers kept only: `@modelcontextprotocol/server-filesystem` becomes `@modelcontextprotocol/server-filesystem@2026.8.31` and `"args": ["mcp-server-git"]` becomes `"args": ["mcp-server-git@2026.8.18"]`.
2. `.codex/config.toml`: `"@modelcontextprotocol/server-filesystem",` becomes `"@modelcontextprotocol/server-filesystem@2026.8.31",`; `args = ["mcp-server-git"]` becomes `args = ["mcp-server-git@2026.8.18"]`.
3. `docs/features/kingdom-voices.md`: the snippet's `"args": ["elevenlabs-mcp"],` becomes `"args": ["elevenlabs-mcp@0.12.2"],`; the bullet that starts ``- Requires `uvx elevenlabs-mcp` installed`` becomes ``- Requires `uvx` (it fetches the pinned `elevenlabs-mcp` on first launch; bump the pin per [mcp-servers.md](../reference/mcp-servers.md)) and `ELEVENLABS_API_KEY` set in the environment. Both live outside the repo.``
4. `docs/reference/mcp-servers.md`:
   - In the Serena paragraph WP103 added, replace `this line runs Serena unpinned, so re-derive it when Serena adds an editing tool.` with ``.mcp.json` pins Serena to that commit, so the list matches what runs. On a pin bump, re-derive the Serena entries from the new commit's class markers (`ToolMarkerCanEdit` in `src/serena/tools/`), never from names ending in `Tool`: `SafeDeleteSymbol` and `JetBrainsInlineSymbol` lack the suffix. The four memory tools stay allowed because they write only Serena's memory folders (`.serena/memories/`, and `~/.serena/memories/global/` for a `global/` name).``
   - After the `## Configuration` paragraph, add: ``**Pins.** Every auto-fetched project server in `.mcp.json` runs an exact version: Serena a commit (`7a2968335f2198b966864de1ce3655c8e485a653`), `elevenlabs-mcp` a package version (`@0.12.2`). A pin bump changes every copy of the launch string together: `.mcp.json`, `.codex/config.toml` (Codex still runs `filesystem` and `git`, pinned to `@modelcontextprotocol/server-filesystem@2026.8.31` and `mcp-server-git@2026.8.18`) and the snippet in `docs/features/kingdom-voices.md`. `tools/audit_claude_config.py` flags only an unpinned `npx -y` in `.mcp.json`, so an unpinned `uvx` server or a stale copy elsewhere goes unflagged. User-scope servers (`~/.claude/.mcp/user.json`) are machine-local and not pinned.`` (branch servers kept: list the two `.mcp.json` pins as well.)

Stage `.mcp.json .codex/config.toml docs/features/kingdom-voices.md docs/reference/mcp-servers.md`.

**Verify**: `python -m json.tool .mcp.json > /dev/null && echo JSON-OK`; `grep -c "serena@7a2968335f2198b966864de1ce3655c8e485a653\|elevenlabs-mcp@0.12.2" .mcp.json` prints `2`; `grep -c "@2026.8.31\|@2026.8.18" .codex/config.toml` prints `2`; `python tools/audit_claude_config.py --min HIGH` no new HIGH; Docs lint clean for the two docs.

Commit `chore(mcp): v2.0.28 - pin Serena, ElevenLabs and the Codex MCP servers`. Body: each server now runs one exact version; Serena is pinned to the commit WP103's deny list was derived from, so a new editing tool cannot slip in unpinned; where each copy of a launch string lives. Trailers: `Not-tested: MCP start in Claude Code and Codex after a restart (maintainer live check)`, `Ported-from:` 470ee64e9 (full sha above), `7d13adc369490cf6d7e866c6a44a4dcc76c85f68`, 1ce8c370e, 35f212c60 (one line each).

### Step A3: README and agent manual (c83352298, 8a6388311, 1ce8c370e)

1. `README.md`:
   - Delete the "**By the numbers:**" paragraph (planning :15-17) and the blank line after it.
   - Replace the branch note (the blockquote that names `bannerlord-1.4.5` as the active development branch) with: ``> This branch, **`bannerlord-1.4.5`**, is the Bannerlord v1.4.8 line. The Bannerlord 1.5 line,`` / ``> **`bannerlord-1.5.x`**, is the GitHub default branch and carries its own README.`` (`git ls-remote --symref origin HEAD` printed `ref: refs/heads/bannerlord-1.5.x` on 2026-11-04, so this README's old "GitHub default" claim is stale; if it now prints another branch, name that branch and say so in the commit body.)
   - Quick start block: the `setup-dev-env.ps1` comment becomes `# set BANNERLORD_GAME_DIR (asks for your install path)`; `dotnet test TAOM.Tests     # tests only` becomes `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=   # tests only, no deploy`. The clone line `git clone https://github.com/haterade22/TAOM      # lands on bannerlord-1.4.5` becomes `git clone -b bannerlord-1.4.5 https://github.com/haterade22/TAOM` (a plain clone lands on the default branch, `bannerlord-1.5.x`).
   - Project tree: drop the counts (`58 feature modules` becomes `Feature modules`; `(MSTest + NSubstitute, 2,600+ tests)` becomes `(MSTest + NSubstitute)`; drop ` (11)` and ` (90 files)`).
   - Headline systems: `50 careers across 16 cultures;` becomes `culture-specific careers;`.
   - The sentence that ends the systems paragraph with the GameModel and Harmony counts and "catalogued in [CLAUDE.md](CLAUDE.md)" becomes `LOTR rules are enforced through GameModel overrides and Harmony patches, both catalogued in [harmony-patch-registry.md](docs/reference/harmony-patch-registry.md) and [gamemodel-registry.md](docs/reference/gamemodel-registry.md).`
   - "How It's Built" Claude bullet: drop every count (`custom slash-command skills, specialized agents, automated hooks, path-scoped rule files, persistent cross-session memory, and project MCP servers (symbolic code navigation, decompilation)`) and end it with ``[AGENTS.md](AGENTS.md), with Claude's [CLAUDE.md](CLAUDE.md) layer, is the authoritative reference every session loads.``; Codex bullet: drop `40+ reviews completed to date;` so it reads `... genuine second opinion. Review instructions live in [AGENTS.md](AGENTS.md).`
   - Replace the body of `## Installing to Play (non-developers)` (from `TAOM ships as a set of modules.` through `existing saves are not supported.`) with:

```
TAOM ships as four modules: `TAOM`, `TAOM.Dependencies`, `TAOM_Map` and `LOTRLOME_Armory`.
`TAOM.Dependencies` carries Harmony, ButterLib, UIExtenderEx and Mod Configuration Menu (MCM)
inside it, so none of those is installed separately: a standalone Workshop or Nexus copy of any of
them must be removed before TAOM is enabled.

Bannerlord **v1.4.8** is required. Place the four modules in your Bannerlord `Modules/` directory,
enable them in the launcher, and start a **new campaign**: existing saves are not supported.
```

2. `docs/ai-includes/agent-operating-manual.md`, the **Build** row: if WP103 already gave it both flags, replace its notes cell with ``Use this, NOT `./build.ps1`, during agent work (avoids `out/` contention). Keep both flags: `-p:ModuleId=` skips the build package's copy targets and `-p:DisableModuleCopy=true` skips `DeployTAOMDependenciesStubs` (`Dependencies/TAOM.Dependencies.csproj`); see the caveat below.``; if it still lacks `-p:ModuleId=`, add both flags to the Build and Test commands too (`dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=`, `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=`). Leave the binding-gate row (its runsettings belong to WP122).

Stage `README.md docs/ai-includes/agent-operating-manual.md`.

**Verify**: `grep -c "Harmony\*\* and \*\*Mod Configuration" README.md` prints `0`; `grep -c "v1.4.8" README.md` is at least 3; `grep -c -E "58 feature modules|2,600\+|50 careers|40\+ reviews" README.md` prints `0`; `grep -c "ModuleId=" README.md` prints `1`; `grep -c "lands on bannerlord-1.4.5" README.md` prints `0`; `python -m unittest discover -s tools/tests -t . -p "test_dotnet_commands_flagged.py"` OK.

Commit `docs(readme): v2.0.28 - players need no separate Harmony or MCM`. Body: install section lists the four modules and what Dependencies carries (dr3-maintenance.md asked for it); hand-kept counts removed rather than refreshed because nothing computes them; the test command no longer deploys; the branch note names both lines. Trailers: `Ported-from: c833522989a9629f998fcd55fd34ee4638867a64`, `Ported-from: 8a638831166f1bc979bef6433b180452347335db`, 1ce8c370e.

### Part A done criteria

- [ ] 3 commits; `git -C "$WT" ls-files .claude/settings.local.json _taom_loc.pkl crashz` prints nothing
- [ ] Hook suite, tool tests and config audit as baseline; Docs lint no new finding

## Part B: process docs and the outside-PR route (6 commits)

### Step B1: issue body reread and closing keywords (4e7efe5a8 paragraph, 04c3c4540 bullet)

`docs/ai-includes/completion-workflow.md`:
1. Directly above the line `**Lifecycle:**` (planning :38), insert: `**Before the commit:** reread the issue body against \`git diff --stat\` and the untracked files. The file list (Files changed, or Implementation for a feature) names every shipped file and no "(planned)" row, and the Solution or Design and the Testing sections describe what ships, not the plan. A body written before the build is a plan, even when it was correct then.` and one blank line.
2. Directly after the bullet that starts `- **Close the issue** with`, insert: ``- A closing keyword in a commit body closes only the number that follows it: `Closes #1036, #1037, #1040` closed #1036 alone on push (seen on the 1.5.x line, 2026-10-27). Write the keyword before each number (`Closes #1036, closes #1037`), or close the rest with `gh issue close`, and check every issue's state after the push.``

**Verify**: `grep -c "Before the commit:\*\*\|closes only the number" docs/ai-includes/completion-workflow.md` prints `2`. Commit `docs(workflow): v2.0.28 - reread the issue body, one keyword per issue`; `Ported-from: 4e7efe5a8a3a1df4179120523596cf97752105cf`, `Ported-from: 04c3c4540f0532b55decf1f4dc90fa151233cedd`.

### Step B2: release numbers, the Dependencies version, v2.0.29.5 (62157c7e7; W1)

`.claude/skills/release/SKILL.md` (WP102 added item 5 and WP115 items 6 and 7 to Phase 1, and Phase 8):
1. After Phase 1 item 4 (the line starting ``4. `git tag -l 'v*'` ``) insert:

```
4b. No release channel already carries the target version, tagged or not. On the desktop:
   `grep -o '<Version value="[^"]*"' /e/LOTRAOM_Releases/*/Modules/TAOM/SubModule.xml`; on a machine
   without that folder (the laptop), ask Mike what each channel holds. A channel holding an untagged
   build of that number means a tester's crash report naming it is not your commit: take the next
   number. The public 1.4.8 build carries the hand-typed `v2.0.29.5`, which is in no commit.
```

2. Phase 3 table, `Dependencies/_Module/SubModule.xml` row: change only its "When" cell to `Every release from v2.4.0 on, the same number as Main's, so all four modules read the release number (W1, 2026-11-04); it must change whenever the Dependencies assembly changed`.
3. Directly after that table, before the paragraph that starts `**The #371 gate`, add a blank line and: ``Outside the repo, set the same version in the live `<game>/Modules/TAOM_Map/SubModule.xml` and `<game>/Modules/LOTRLOME_Armory/SubModule.xml` too. Both modules are unversioned (no git), and the release note tells players all four modules read the release number. The #371 gate checks only the two repo files.``
4. Optional (only if `grep -c "## Phase 8" .claude/skills/release/SKILL.md` prints `1`): at the end of the Phase 8 intro add ``When Mike says the packages and release folders are the maintainer's for this release, stop after step 1 and prove the stamps on the DLLs the build deployed instead of running the step 2 dry run: every `TAOM.dll` and `TAOM.Dependencies.dll` under `<game>/Modules/{TAOM,TAOM.Dependencies}/bin` reads `+<tag commit>` with no `.dirty` (`[System.Diagnostics.FileVersionInfo]::GetVersionInfo(<dll>).ProductVersion`). Hand the maintainer the prune, the sweep `-Apply` and steps 2 to 5 as a list.``

`docs/reference/release-process.md`:
5. Three-field table, Dependencies row "When" cell: `Every release from v2.4.0 on, so all four modules read one version (the public v2.0.29.5 build already labels all four that way by hand); it must change whenever the Dependencies assembly changed`.
6. After the paragraph ending `do not bump one by hand.` (the text wraps after `do not`) add: ``The two unversioned modules carry a version too. The live `<game>/Modules/TAOM_Map/SubModule.xml` and `<game>/Modules/LOTRLOME_Armory/SubModule.xml` hold their own `<Version>`, outside git, and the release note tells players all four modules read the release number. Edit both in the install before Mike packages; no commit records the change.``
7. "Cutting a release" step 1: append ``, and the target version in no tag (`git tag -l 'v*'`) and in no release channel: read the `<Version>` of every `E:\LOTRAOM_Releases\<channel>\Modules\TAOM\SubModule.xml`, because a channel can hold an untagged build of a number git never saw (see "A number a channel carried before git did" below)`` before its final full stop.
8. If WP115's prune bullet for TAOM outside `bin/` exists (`grep -n "outside \`bin/\`" docs/reference/release-process.md`), append to it: ``AssetPackages/pack0.tpac` is what packaging TAOM in the editor writes and is not needed: delete it. The four other tpacs there are git-tracked (`fieldcamp_*` and `refuge_*` prefabs) and stay.``
9. At the end of `## Resolving a crash report to a commit` add: ``**A number a channel carried before git did.** A build can reach a release channel under a number no commit or tag holds, so a tag of that name is not what the player ran. Resolve such a report by its build stamp, never by the tag. `v2.0.29.5` (the public 1.4.8 build) is in no commit on any branch (`git log --all -S v2.0.29.5 -- Main/_Module/SubModule.xml Dependencies/_Module/SubModule.xml` prints nothing). The players' `[BuildStamp]` line names `8df90444` (branch `fix/public-armory-ids-145`, merged into this line by WP101), whose `<Version>` in git is `v2.0.28`, and their log's module list shows all four modules at `v2.0.29.5`. `v2.0.29` to `v2.0.36` are 1.5.x release numbers. `/release` Phase 1 item 4b reads every channel's `<Version>` before a number is chosen.``

Not taken: the hooks-catalog "known gap" note (WP103 Step B4 made the subject gate read the commit's own repository: `git grep -n taom_git_repo_dir -- .claude/hooks/check-commit-subject-version.sh` finds it; if it does not, STOP), `.claude/agents/deep-reviewer.md` and `docs/ai-includes/git-and-commits.md` (absent here), the v2.0.34 INDEX row, the v2.0.33 paragraph (a 1.5.x build).

**Verify**: `grep -c "4b\. No release channel" .claude/skills/release/SKILL.md` prints `1`; `grep -c "A number a channel carried before git did" docs/reference/release-process.md` prints `2` (heading text and the step 1 pointer); `git log --all --oneline -S v2.0.29.5 -- Main/_Module/SubModule.xml Dependencies/_Module/SubModule.xml | wc -l` prints `0`; `bash tools/test_hooks.sh` (section 3b reads skill frontmatter) as baseline. Commit `docs(release): v2.0.28 - channel numbers, version policy, v2.0.29.5`; `Research: E3 player log [BuildStamp] and module list; git log -S`; `Ported-from: 62157c7e775288d7907283a0dfa6fa5a51da19e0`, plus 04c3c4540 if item 4 was taken.

### Step B3: engine bump writes its Gates after the last run (370b4bf2b; 04c3c4540 note)

1. If `grep -n "Gates" .claude/skills/engine-bump/SKILL.md` already finds a Gates rule (WP138 may bring the 1.5.x skill), skip this step and record it. Otherwise, after the `## Phase 4` item that starts `6. CLAUDE.md target-version note`, add: ``7. Write the gate results into a `## Gates` section of `docs/migration/v<ver>-impact.md` from the final `/verify` output, after the commit, in the past tense, and name the commit each number comes from. A later commit that changes one of those numbers rewrites the whole section, not only its own clause.``
2. 04c3c4540's fully-qualified-name note for item 3 is NOT taken when `tools/tests/test_taom_src_short_names.py` exists (WP137's `857734c64` made `taom-src` accept short names and read the game first). If that file is absent, STOP (the WP137 marker should have caught it).

**Verify**: `grep -c "## Gates" .claude/skills/engine-bump/SKILL.md` prints `1`; Hook suite as baseline. Commit `docs(skills): v2.0.28 - engine bump writes its Gates after the last run`; `Ported-from: 370b4bf2b24bf3edf4881a2613500ded15090a75`.

### Step B4: open the repo to outside pull requests (d08637c73 at its 1.5.x tip state)

The three files changed again on 1.5.x after `d08637c73` (`2992763f6`, `452796db4`, `434d1d795`); start from the tip so those edits come along, then adapt.

1. `git -C "$WT" show e45f6df2c:CONTRIBUTING.md > "$WT/CONTRIBUTING.md"`, then edit:
   - "## Where to send it", the **Branch:** bullet becomes ``- **Branch:** this branch, `bannerlord-1.4.5`, is the Bannerlord **v1.4.8** line: send 1.4.8 bug fixes here, built and tested against 1.4.8. Work for the Bannerlord 1.5 line goes to `bannerlord-1.5.x`, which has its own CONTRIBUTING.md.``
   - "## Setting up": `Bannerlord v1.5.6` becomes `Bannerlord v1.4.8`.
   - The git hooks paragraph becomes ``Turn on the repo's git hooks once per clone: `git config core.hooksPath .githooks`. The commit-msg hook checks the commit subject. There is no CI, so this and your own test run are the only checks before a review.`` (only `commit-msg` exists here; WP143 extends the sentence if it ports the pre-commit hook).
   - "## What happens after you open a PR", item 2 becomes ``2. **Build and tests.** The full solution build and the whole test suite, once a maintainer has read the diff and decided to run it.``
2. `mkdir -p "$WT/.github" && git -C "$WT" show e45f6df2c:.github/pull_request_template.md > "$WT/.github/pull_request_template.md"`; its first line becomes `<!-- Target branch: bannerlord-1.4.5 for Bannerlord 1.4.8 fixes, or bannerlord-1.5.x for the 1.5 line. See CONTRIBUTING.md. -->`. GitHub serves the template from the default branch, which is `bannerlord-1.5.x` and has its own copy naming both lines, so this copy takes effect only if the default branch changes.
3. `mkdir -p "$WT/.claude/skills/review-outside-pr" && git -C "$WT" show e45f6df2c:.claude/skills/review-outside-pr/SKILL.md > "$WT/.claude/skills/review-outside-pr/SKILL.md"`, then edit:
   - `(AGENTS.md "Untrusted input")` becomes `(AGENTS.md "Always")`.
   - Phase 0, the base bullet becomes: ``- On this checkout the base must be `bannerlord-1.4.5` (Bannerlord 1.4.8); a PR to `bannerlord-1.5.x` is reviewed on a 1.5.x checkout. This checkout builds and tests against the installed 1.4.8 game, and `taom-src` reads the same DLLs, so Phase 3 can run `/verify` here once the maintainer gives the word for the PR. The worktree branches from the PR head.``
   - `AGENTS.md "Documentation duty"`: keep if `grep -c "## Documentation duty" AGENTS.md` prints `1`, else write `CLAUDE.md "Documentation Requirements"`.
   - Phase 3's `(his rule, 2026-11-03)` becomes `(the maintainer's rule, 2026-11-03)`.
   - Every `E:/repos/taom-pr-<N>` becomes `../taom-pr-<N>` (Phase 2 block, Phase 3 steps 1 and 2, Cleanup).
   - Phase 3 step 2 scope: `base \`bannerlord-1.5.x\`` becomes `base \`bannerlord-1.4.5\``; step 3: `on the pinned one` becomes `on v1.4.8`.
4. `README.md` `## Contributing`: before the numbered list add ``Pull requests go to `bannerlord-1.4.5` for Bannerlord 1.4.8 fixes, or to `bannerlord-1.5.x` for the 1.5 line. [CONTRIBUTING.md](CONTRIBUTING.md) covers the route, what a PR needs and the checks every PR goes through.`` and a blank line; item 1 becomes ``1. Read [AGENTS.md](AGENTS.md) for coding standards and conventions``.
5. `CLAUDE.md`: in the Skill Routing table, directly after the `/release` row, add ``| An outside contributor's pull request ("review PR #N") | **`/review-outside-pr`** | None, always: security read of the diff before any checkout or build |``. Recount the skill sentence: `ls -d .claude/skills/*/ | wc -l` (S) and `grep -l "disable-model-invocation: true" .claude/skills/*/SKILL.md | wc -l` (D); the sentence becomes `<S-D> of the <S> skill **descriptions load eagerly**`, and the parenthetical count of non-eager skills becomes D. Then `python tools/lint_docs.py --drift-only`: if it reports the CLAUDE.md budget, shorten the row to ``| Outside contributor's PR | **`/review-outside-pr`** | always |``; if it still fails, STOP with the measured size.

Stage `CONTRIBUTING.md .github/pull_request_template.md .claude/skills/review-outside-pr/SKILL.md README.md CLAUDE.md`.

**Verify**: `git -C "$WT" check-ignore -v .claude/skills/review-outside-pr/SKILL.md` prints nothing; `grep -c -e "E:/repos" -e "we still port nothing" -e "which reads the 1.5.x DLLs" .claude/skills/review-outside-pr/SKILL.md` prints `0`; `grep -c "bannerlord-1.4.5" CONTRIBUTING.md` is at least 1; `bash tools/test_hooks.sh` (3b parses the new frontmatter) as baseline; Drift gate exit 0; Contract exit 0; tool tests as baseline. Commit `docs(contributing): v2.0.28 - open the repo to outside pull requests`; body says build and test of a contributor's PR need the maintainer's word on this machine (`.ai/policy.md`); `Ported-from: d08637c738363d31ac614b34016a07296a833dc3` (the later 1.5.x edits to these files are WP143's commits; name them in the body, not as trailers).

### Step B5: ADR-001 allows validated JSON feature configs (dc275dc4e; maintainer step M3)

1. Measure: `git -C "$WT" ls-files Main/_Module/ModuleData | grep -c '\.json$'` (J) and `... | grep -c '\.xml$'` (X); planning values 66 and 286.
2. **Maintainer step M3**: hand the maintainer the exact edits (see "Maintainer steps", with J and X filled in). Resume when `git -C "$WT" status --porcelain` shows only `M docs/adrs/001-xml-config.md` and `M docs/adrs/README.md`.
3. Stage both files. **Verify**: `grep -c "Amendment: JSON feature configs" docs/adrs/001-xml-config.md` prints `1`; Docs lint clean. Commit `docs: v2.0.28 - ADR-001 allows validated JSON feature configs`; `Ported-from: dc275dc4ef92ac469013b575352d5060fb810287`. The four B04 feature-doc hunks are not taken (those docs are 1.5.x-only).

### Step B6: snapshot counts, the ModuleData hook note, the CompanionTactics row (04c3c4540, 66e3fd593)

1. `docs/reference/taleworlds-api-snapshot/README.md`: in the `patch-targets.md` row, `each of the 220 \`[HarmonyPatch]\` / \`TargetMethod\` classes and the engine method it patches, resolved as Harmony resolves it.` becomes ``each `[HarmonyPatch]` / `TargetMethod` class and the engine method it patches, resolved as Harmony resolves it. The file's `Patches:` header line holds the current count.``; `HarmonyPatchBindingTests` row: `All 220 ... targets` becomes ``Every `[HarmonyPatch]` / `TargetMethod` target, resolved as Harmony does at `PatchAll` (count: `patch-targets.md` header).``; `GameModelOverrideBindingTests` row: `All 46 GameModels:` becomes `Every GameModel (count: \`gamemodel-bases.md\` header):`. Leave the historical "21 rows stale" note.
2. `docs/reference/hooks-catalog.md`, the `check-moduledata-validation.sh` row: append to its purpose cell ``**It also reads the live `TAOM_Map` and `LOTRLOME_Armory`** (`_EXTRA_REF_MODULES` in `tools/validate_moduledata.py`), so an ERROR there blocks every ModuleData commit, including one that touches neither module. Fix the live module, wait for its owner or ask Mike; never bypass the hook.``
3. `docs/reference/feature-map.md`: directly after the `CombatMechanics` row add ``| CompanionTactics | `Main/Features/CompanionTactics/` (Patch35): equipment-based role badges on party and Order of Battle tooltips; an Order of Battle overlay whose Auto-Assign button is still a stub (it shows a message; heroes are placed by hand) and Save/Load presets whose loading is not wired yet (MCM `EnableFormationPresets`, off by default); a display-only battle action bar. See [companion-tactics.md](../features/companion-tactics.md) |`` (facts: `OOBButtonsVM.ExecuteAssignCharacters` shows the Phase-1 stub message; `TaomSettings.cs:786` `EnableFormationPresets = false` with the hint "loading a preset is not yet wired"; companion-tactics.md Overview item 3).
4. `docs/features/companion-tactics.md`: the `TroopStanceManagerTests.cs` bullet's `8 tests` becomes the measured count (`grep -c -E '^\s*\[(TestMethod|DataTestMethod)' TAOM.Tests/Features/CompanionTactics/BattleActionBar/TroopStanceManagerTests.cs`, 9 at planning).

**Verify**: `grep -c "220\|All 46" docs/reference/taleworlds-api-snapshot/README.md` prints `1` (the historical note); `grep -c "| CompanionTactics |" docs/reference/feature-map.md` prints `1`. Commit `docs(reference): v2.0.28 - snapshot counts, ModuleData hook, tactics row`; `Ported-from:` 04c3c4540 and `66e3fd593bb4538e48724131e2335398a2adfca7`.

### Part B done criteria

- [ ] 6 commits (5 if B3 was skipped); hook suite, tool tests, contract and drift gate as baseline; Docs lint no new finding

## Part C: docs that disagree with the 1.4.x code (6 commits)

Each row below was measured on 1b00d1acd this session. Re-run the "Measure" command first and write the measured value; if the doc no longer holds the stale text (another WP fixed it), skip the row and name it in the commit body.

### Step C1: stale paths in the living docs (bb7da8ff4, redone by hand)

Living docs = `docs/features`, `docs/modding`, `docs/reference`, `docs/ai-includes`. Never `docs/archive`, `docs/changelog-archive`, `docs/reviews`, `docs/audits`, `docs/migration`, `docs/raw` (history).

| Edit | Measure / note |
|---|---|
| `Main/_Module/GUI/Prefabs` becomes `Main/_Module/GUI/PreFabs` | `git grep -n "GUI/Prefabs" -- docs/features docs/modding docs/reference docs/ai-includes` (16 lines in 13 docs at planning); the folder on disk is `PreFabs` (`git ls-tree -d --name-only HEAD Main/_Module/GUI/`) |
| `docs/modding/banners-and-heraldry.md` "Spelling traps" bullet becomes ``- **Spelling traps.** The directory is `Main/_Module/GUI/PreFabs` (capital F), and the kingdoms file is `taom_spkingdoms.xml`, all lowercase. Windows resolves either spelling; git and case-sensitive tools do not.`` | it currently states the wrong spelling as the right one |
| `TAOM_spkingdoms.xml` becomes `taom_spkingdoms.xml` in `docs/features/kingdom-creation.md` (8 lines) and `docs/features/army-targeting.md:261` | `git grep -n TAOM_spkingdoms -- docs/features` |
| Sentences that describe that misspelling become false, so remove the claim: `configs-factions-and-world.md` delete the two-line "Note that `kingdom-creation.md` spells ..." sentence; `kingdoms.md` cut the clause after "what line N registers" so the sentence ends there, correct N to the measured line and drop that line's `<!-- measured: rg -c 'TAOM_spkingdoms' ... -->` comment; delete the `**8** lines of ... spell the file` bullet; `recipe-add-a-culture.md` item 4 becomes ``4. `taom_spkingdoms.xml` ([kingdoms.md](kingdoms.md)); the file name is all lowercase.``; `recipe-add-a-kingdom.md` paragraph "**Two spelling corrections to the older docs.** ..." becomes ``**The kingdoms file name is all lowercase.** The file on disk is `taom_spkingdoms.xml`, and `Main/_Module/SubModule.xml` registers it as `<XmlName id="Kingdoms" path="taom_spkingdoms"/>` (line N).`` | `grep -n 'path="taom_spkingdoms"' Main/_Module/SubModule.xml` (142 at planning; the docs say 130) |
| `tools/add_bandit_hideouts.py`, `tools/migrate_hideouts_to_lotr.py`, `tools/singularize_career_names.py`, `tools/Assign-RhunSettlementOwners.py` in link text and prose become `tools/oneoff/<same name>` | all four live under `tools/oneoff/`; link targets already point there |
| `docs/features/race-age-system.md`: delete the `GetCivilianEquipment_Patch.cs` row; `Main/_Module/ModuleData/taom_child_equipment_templates.xml` becomes `Main/_Module/ModuleData/equipmentsets/taom_child_equipment_templates.xml` | the patch file is in no tree and no `Main/` history (`git log --oneline -- Main/Features/RaceAge/Hooks/GetCivilianEquipment_Patch.cs` prints nothing) |
| `docs/features/nazgul-family.md`: `characters/heroes.xslt` becomes `heroes.xslt` (the file is `Main/_Module/ModuleData/heroes.xslt`) | 5 lines at planning |
| `docs/features/custom-battles.md`: the commanders row path becomes `Main/_Module/ModuleData/custom_battle/custom_battle_commanders.json` | |

**Verify**: `git -C "$WT" grep -c "GUI/Prefabs\|TAOM_spkingdoms\|tools/add_bandit_hideouts.py\|GetCivilianEquipment_Patch" -- docs/features docs/modding docs/reference docs/ai-includes` prints nothing; Docs lint no new dead link. Commit `docs: v2.0.28 - correct stale paths in the living docs`; `Ported-from: bb7da8ff4e364a34aea8833ee6b3dde4b80e1039`. `faction-ui.md` is absent here.

### Step C2: feature docs behind their code (2e34cade1, 20616aad3, 8ce80c2ff, 28b8f6282, 24e52abb9, f4058993a)

| Doc | Edit | Measure |
|---|---|---|
| `lord-party-templates.md` | `ShippedLordPartyTemplateTests.cs` (13) and `Patch88LordPartyTemplateTests.cs` (12) become the counts | `grep -c -E '^\s*\[(TestMethod\|DataTestMethod)'` on each file under `TAOM.Tests/Features/LordPartyTemplates/` (14 and 11) |
| `map-event-guard.md` | `SubModule.cs:721` becomes the line of the comment | `grep -n "after Sandbox registered its behaviors" Main/SubModule.cs` (714) |
| `return-to-army.md` | the deferral log string becomes `could not read the party's army state or place it at the gate, deferring to vanilla's wait menu`; drop the claim that vanilla's follow us line reaches this state, at `:42`, `:82`, `:206` and `harmony-patch-registry.md:982` (find each by text) | `Patch87_ReturnToArmy.cs` (grep the string); v1.4.8 `LordConversationsCampaignBehavior.cs:334-336`: nothing outputs `lord_tell_gathering` |
| `naval-travel.md` | `enabled` default `true` becomes `false`, meaning cell: `Master gate (MCM-overridable). Shipped false while the feature is parked (JSON, \`NavalTravelConfig.Enabled\` and MCM \`EnableNavalTravel\` all default false).` | `naval_travel_config.json` `"enabled": false`; `NavalTravelConfig.cs:16`; `TaomSettings.cs:943` |
| `time-acceleration.md` | `carries 10 tests` becomes the count; `the null-\`KeyboardKey\` premise` becomes `the null-\`KeyboardKey\` and cleared-to-\`Invalid\` premises` | `TaomTimeControlHotKeyCategoryTests.cs` (11; `GameKeyClearedInOptions_KeepsANonNullKeyHoldingInvalid`) |
| `docs/modding/module-armory.md` | after the line `A resize pass never renames an editor-owned texture ...` add a blank line and `Mike also wants every texture name lower case (2026-09-13: "all textures will need to be all lower case, keep that in mind"). That is a requirement for new textures, not a licence to rename files the editor already tracks.` | a pre-fork maintainer decision |
| `settlement-food.md` | apply `git -C "$WT" diff 8ce80c2ff^ 8ce80c2ff -- docs/features/settlement-food.md \| git -C "$WT" apply`, then change `PartyBase.cs:394` back to `PartyBase.cs:381` | the doc equals 8ce80c2ff^ at planning; `bee07b487` is on this line; v1.4.8 `PartyBase.cs:381`; 22 + 25 + 4 = 51 test attributes; `TownFoodSnapshot.cs:14` says the correction is an inert no-op |
| `caravan-trade.md` | only the Key Files row: `3 singleton registrations.` becomes `5 singleton registrations (config provider, settings provider, service, visit memory, visit-memory behavior).` Leave the `ResolveSide` hunks: here the side comes from a private `CaravanTradeService.ResolveSide` that calls `GetKingdomSide` then `GetCultureSide` (`CaravanTradeService.cs:110-116`), which the 1.4.x text already says | `grep -c "container.Register" Main/Features/CaravanTrade/CaravanTradeIoC.cs` (5) |
| `army-targeting.md` | take these hunks of `git -C "$WT" diff 8ce80c2ff^ 8ce80c2ff -- docs/features/army-targeting.md` by hand: "Why weighting" and "Fail open" paragraphs; "Only `Besieger` receives the priority and theater terms"; both component-diagram changes (`IsWithinBorderRescueRange` replaces `GetReachMultiplier`/`IsWithinReach`; `ITargetScoreContextFactory.Create`); MCM `ArmyBorderRescueRadius`; "vanilla's distance term decides" (that sentence only); the Key Files row; the Tests block; the Performance row deletion and the "Feature disabled" bullet; the Patch22 polarity paragraph. Leave the theater table and realm lists, the Ered Luin lines and every Patch49 change | `IsWithinBorderRescueRange` `ArmyTargetingService.cs:111-120` (true when disabled, false on NaN); no `GetReachMultiplier`/`IsWithinReach` in `Main/`; `TaomSettings.cs:362` `ArmyBorderRescueRadius`; `ITargetScoreContextFactory` in `TaomTargetScoreModel.cs`; test attributes: `ArmyTargetingServiceTests` 20 (12 + 5 + 3 `IsInPriorityList`), `WarTheaterAndReachTests` 37 (7 + 7 + 11 + 12), `ArmyTargetingConfigProviderValidationTests` 13 |
| `war-of-the-ring-momentum.md` | the `events.armyMomentum` row becomes 25 and the `events.killMomentumPerHundred` row 40, with the 28b8f6282 wording (commits `c3dd7f109`, `683799804`; code default 10 in `MomentumConfig.cs`). Leave the test-count row | `momentum.json:9` 25, `:11` 40; `MomentumConfig.cs:46` default 10; both commits on this line |
| `troop-weight-system.md` | `(7 patch initialisations)` becomes the count | `grep -c "Initialize(" Main/Features/TroopWeight/TroopWeightIoC.cs` (6) |
| `multi-culture-armor-revamp.md` | ``- `LOTRLOME_Armory` module declared in `SubModule.xml`.`` becomes ``- `LOTRLOME_Armory` is a separate module that TAOM's items and troops reference; `Main/_Module/SubModule.xml` does not declare it (its `DependedModules` list names only <the ids, in file order>, lines <L>).`` | `grep -n "DependedModule Id=" Main/_Module/SubModule.xml`; `grep -c LOTRLOME_Armory Main/_Module/SubModule.xml` prints 0 |
| `siege-trebuchets.md` | `git -C "$WT" diff f4058993a^ f4058993a -- docs/features/siege-trebuchets.md \| git -C "$WT" apply` (whole hunk) | the doc equals f4058993a^ at planning; `ISiegeEngineAvailabilityService.GetDefenderEngines(bool hasFirePerks)`, `DefenderSiegeEngineKind`, `TaomSiegeEventModel.Resolve`, `SubModule.cs` `new TaomSiegeEventModel(IoC.Resolve<ISiegeEngineAvailabilityService>())`, `SiegeEngineAvailabilityServiceTests.cs` all exist |
| `auto-resolve-diagnostics.md` | only if the "`IsFinalized` is already true inside the handler" paragraph survived WP123's rewrite: `EncounterAdapter.cs:137` becomes the measured line | `grep -n "IsFinalized" Main/Adapters/EncounterAdapter.cs` (166) |

**Verify**: each edited doc shows the measured values (`git -C "$WT" diff --cached --stat` lists only these docs); Docs lint clean for them. Commit `docs: v2.0.28 - correct feature docs that fell behind their code`; one `Ported-from:` line each for `2e34cade18aff82914278723a271d80ad4dfbe62`, `20616aad311cddf736d1107ab33d2d6c9fd81b9b`, `8ce80c2ff98475938c2270fa406b4f7024470b80`, `28b8f62824332b5a2d40d55c7c88fbff4799d867`, `24e52abb95f55fbb2dd5a9f2f49c6117773addaf`, `f4058993aa86d98d3feaa4546131d0ab337cd2a7`.

### Step C3: modding and reference docs (3b1af2777, ff1fe1933, 1a2da1e3e, 2e2a422dc, 86d3a774e)

| Doc | Edit | Measure |
|---|---|---|
| `docs/features/cultural-feats.md` | `registers 30 custom` (:23), `30 FeatObject instances` (:30) and `(77 feats)` (:222) become the count; ` with 3 feat IDs:` (:60, with its leading space) becomes `; the number of feat IDs varies by culture (<min> to <max>). Erebor's begins:` (the example below it is Erebor's first three); `CountIs77` (:339) becomes the test name. Leave the "9 new thin models" and "9 GameModel overrides" lines (not re-measured) | `grep -o '"taom_[a-z0-9_]*"' Main/Features/CulturalFeats/TaomCulturalFeats.cs \| sort -u \| wc -l` (130); feat ids per `<cultural_feats>` block of `taom_spcultures.xml` (16 blocks, 1 to 13); `grep -o "CountIs[0-9]*" TAOM.Tests/Features/CulturalFeats/TaomCulturalFeatsDefinitionTests.cs` (`CountIs130`) |
| `docs/features/localization.md` | Overview: `all other languages are community-contributed overrides.` becomes `every other language is an AI first draft, and the rows in \`tools/translation_overrides/<lang>.json\` replace the draft.`; "Why This Exists": `ships 1,773 LOTR-specific strings` becomes `ships thousands of LOTR-specific strings in <F> English source files`; the prefix table lead becomes ff1fe1933's "The two oldest key prefixes ..." sentence; table counts and Key Files counts become the measured ones (module file: `Source of <S> English strings, <T> of them with \`taom_str_*\` keys`); `follows the same 3-file pattern.` becomes `holds one translation file per English source file: <F> files, all listed in its \`language_data.xml\`.`; `Community translation files (stub templates)` becomes `Translation files, one per English source file`; `Translation stubs parse` becomes `Translation files parse`; how-to step 3 becomes `Create the translation files, one per \`LanguageFile\` entry, ...` | F = `grep -c LanguageFile Main/_Module/ModuleData/Languages/DE/language_data.xml` (13); T = unique `{=taom_str_` keys in `taom_module_strings.xml` (374); S = `grep -c '<string ' .../taom_module_strings.xml` (2,657); aom = unique `{=aom_` keys in `taom_wanderer_strings.xml` (1,337); other prefixes exist (`{=aom_` 90 and `{=TAOM_` 42 in the module file; `{=aom_` 873 in `taom_xslt_strings.xml`) |
| `docs/modding/module-dependencies.md` | replace the paragraph from `**The third field is not in the file today.**` through the end of the `Two facts about the numbers.` sentence about `v2.0.6` with 1a2da1e3e's version rewritten with this tree's values: all three fields are present; `cc1713eb` removed them and `95085de30` (2026-09-14) restored them; the element's line and the #371 comment's lines; the mirror's line and its `version=` value; both `<Version>` values (Dependencies and Main, which differ until the first W1 release). Also correct the two other manifest cites in this doc (planning `:20-22, 32-37` and `:15-22`). Leave 3b1af2777's hunks of this doc | `grep -n "TAOM.Dependencies\|DependedModuleMetadatas\|Issue #371" Main/_Module/SubModule.xml`; `grep -o '<Version value="[^"]*"' Dependencies/_Module/SubModule.xml Main/_Module/SubModule.xml`; `95085de30` on this line |
| `docs/features/troop-progression.md` | the `TaomVolunteerModel` bullet and Key Files row name four overrides: `MaxVolunteerTier` (via `IVolunteerTierService`), `GetBasicVolunteer`, `MaximumIndexHeroCanRecruitFromHero` (alignment-gated recruitment through `IRecruitmentAlignmentService`) and `GetDailyVolunteerProductionProbability` (vanilla's value with the owner culture's respawn feats applied, clamped to 0 to 1). No `VolunteerProductionService` rows | `grep -n override Main/Features/TroopProgression/Models/TaomVolunteerModel.cs` (:33, :45, :55, :79) |
| `docs/features/castle-recruitment.md` | `CastleRecruitmentConfigProviderTests` (8) becomes the count, with `valid values logging info` added to its list. No War Chronicle line | 9 test attributes |
| `docs/features/dev-console.md` | naming table: Order row gets ``(shipped exception with a feature prefix before the verb: `fc_grant_merit`)``; Read-only row gets ``Shipped exceptions, kept rather than renamed and not a precedent: the `<subject>_status` reports (`time_status`, `service_status`, `fc_status`).``; Mutating row gets ``Shipped exceptions with a verb outside this list, kept rather than renamed and not a precedent: `grant_` (`fc_grant_merit`), `nudge_`, `save_` and `reload_` (`nudge_race_offset`, `save_race_offsets`, `reload_race_offsets`) and `rescue_` (`rescue_service`, `rescue_time`).``; Tier A row becomes `Read-only (\`print_*\`, \`audit_*\`, and the shipped read-only exceptions in the naming table)`. After the `taom.print_party_size` row add 2e2a422dc's rows for `taom.print_ai_food_relief [count]`, `taom.fc_status` and `taom.fc_grant_merit <troopId> <amount>`. Nothing for `tactic_status`, `rally_status`, `chronicle_*`, `war_effect*`, `realm_*`, `career_perks`, `start_`/eye rows | each command exists: `git grep -n 'CommandLineArgumentFunction("<name>", "taom")' -- Main` (`AiPartySizeCheats.cs:34`, `FieldCommissionCheats.cs:27`, `:45`); usage text and limits: default 25 rows, 1 to 100,000 merit, save key `_taom_fc_merits` (`FieldCommissionSaveData.cs:16`) |
| `docs/features/coop-interop.md` | `Pinned by three tests in \`CoopAuthorityGateTests\`.` becomes ``The authority half is pinned by two tests in `CoopAuthorityGateTests` (`SiegeDefense_OnHourlyTick_CoopClient_DoesNotTick`, `SiegeDefense_OnHourlyTick_Authority_Ticks`); no test calls `OnHourlyTickLocalPlayer`, so the local half's deadline filter is unpinned.``; `TAOM's 7 transpilers.` becomes ``TAOM's own transpilers (`grep -rl 'IEnumerable<CodeInstruction> Transpiler(' Main`; <M> in <N> files on <date>).`` | `CoopAuthorityGateTests.cs:137`, `:148`; no `OnHourlyTickLocalPlayer` in `TAOM.Tests`; M and N: `git grep -c 'IEnumerable<CodeInstruction> Transpiler(' -- Main` (9 in 9 at planning) |
| `docs/features/bannerlord-together-compat.md` | `TAOM brings 7 transpilers of its own;` gets the same grep sentence; `32 classes are currently classified; three are gated.` becomes `<C> classes are classified (<date>: <R> ReviewedSafe, <G> GatedForCoop, <P> Parked); <G> are gated.` | registry entries in `CoopVetoClassificationTests.cs` matching `\] = new\(CoopVeto\.` (49: 45, 3, 1) |

Never touch the MCM settings counts in `coop-interop.md` and `bannerlord-together-compat.md` (`SettingsFingerprintTests.EveryDocQuotingTheSettingsCounts_AgreesWithReflection` reads them). Not taken here: the War Chronicle and Eye of Sauron rows, the v1.5.5 pin row, the WarOfTheRingBehavior reconcile row (WP110 owns it) and the refuge.md #773 note (Part E checks it).

**Verify**: Filtered `SettingsFingerprintTests` 0 failed; Docs lint clean for touched docs. Commit `docs: v2.0.28 - correct modding and reference docs against the code`; `Ported-from:` `3b1af2777d211fd15acf0518015aef1552109c36`, `ff1fe1933dcf6d0eca4daae2fd1b1992faf32820`, `1a2da1e3eabe12f4497232956b2cb77b7a3bf0bf`, `2e2a422dc86e5575d7f27f618f271205433ee066`, `86d3a774e32381cf61413478fb2efe4a40e955a6`.

### Step C4: engine docs cite lessons, FillFrom copies all slots (50e8dc7ae, 66dd97536)

1. `50e8dc7ae`, `docs/reference/engine/*.md` except `settlement-economy-food-prosperity.md` (its 1.4.x text at :149-153 is right; the hunk would claim overrides 1.4.x lacks): for each hunk of `git -C "$WT" diff 50e8dc7ae^ 50e8dc7ae -- docs/reference/engine/`, take
   - every swap of a `feedback_*` memory citation for a lesson link or lesson file, when the lesson heading it quotes exists in the named file (`grep -c -F "<first 40 characters of the heading>" docs/reviews/lessons/<file>`; all 14 quoted headings exist at planning);
   - every `CLAUDE.md` pointer swapped for `gamemodel-registry.md`, `harmony-patch-registry.md`, `harmony-patches.md` or AGENTS.md;
   - `item-equipment-model.md`: the `FillFrom` correction with the cite `Equipment.cs:184-194 on v1.4.8`, and the "`FillFrom` replaces all 12 slots" bullet (`PlayerEquipmentAdapter`, `starting-equipment-tuning.md` exist).
   Leave: every TAOM line number from 1.5.x (`SubModule.cs:1285`, `:2183`, `:2186`, `:2197`, `:1841`; write the measured line only if you re-measure it), the agent-stat override list naming spider and Mumakil difficulty, race abilities, charge or siege detachment (1.5.x code), `gamemodel-system.md`'s model count and `FeatureModuleHooks`, the `config-providers.md` rule (absent here: keep `csharp-architecture.md`), and the scene doc's howdah instantiation sentence (the howdah is disabled here).
2. `66dd97536`: `animation-binding-and-playback.md` (cite `ElephantLikeEngageDecorator.cs:41`, measured), `monster-model.md`, `mount-and-rider-runtime.md` (drop the "Mirkwood broods ride it riderless" clauses), `usable-machines.md` (`TaomHowdahMachine`, `TaomHowdahStandingPoint` exist). Facts: the live Armory's `lotr_monster_spider.xml` sets `Mountable="true"` and `rider_sit_bone="chest_m"`, action set `as_spider`, usage `spider`; `docs/features/spider.md:8` says Horse-slot mount. Leave `main-map-vista.md` (not checked), the tools/README rows (Part E) and the tester checklist.
3. `tools/check_external_xslt.py`: the docstring paragraph from `TAOM ships 16 stylesheets` through `which is the only place such a check can live.` (lines 4-9 at planning) becomes: `TAOM ships <A+B+C> stylesheets across three modules (<A> in the repo, <B> in \`LOTRLOME_Armory\`, <C> in \`TAOM_Map\` on <date>; run this script for the current count) and, until #462, only the repo's own had any gate at all. The "Validate XML & XSLT" CI job globbed \`Main/_Module/ModuleData/**/*.xslt\`, so it never reached \`TAOM_Map\` or \`LOTRLOME_Armory\`: those modules are not in the checkout. That job went with the workflow in \`ccef172ad\`, and no CI has run since. This script is the only gate for the live modules, so run it by hand.` Keep the rest of the docstring.

Measure: A `git -C "$WT" ls-files Main/_Module/ModuleData | grep -c '\.xslt$'` (8); B and C `find "$BANNERLORD_GAME_DIR/Modules/LOTRLOME_Armory/ModuleData" -name "*.xslt" | wc -l` (8) and the same for `TAOM_Map` (1).

**Verify**: `git -C "$WT" grep -c "feedback_" -- docs/reference/engine` is lower than at Step A0 (record both numbers); `python -m unittest discover -s tools/tests -t . -p "test_check_external_xslt.py"` OK; Docs lint no new dead link. Commit `docs(engine): v2.0.28 - engine docs cite lessons, FillFrom copies all`; `Ported-from: 50e8dc7aea26193691a93fb8750231daff74a191`, `Ported-from: 66dd975363a5e227e354a3e87bb950c22086da5f`.

### Step C5: model the deserializer when reading troop kit (df273ea24 and its lesson)

1. `git -C "$WT" cherry-pick -n df273ea24` (simulated clean). Expect one change, `.claude/rules/moduledata-validation.md`; anything else staged is a STOP.
2. Append to `docs/reviews/lessons/xslt-moduledata.md` the link target the section names, as:

```
### Troop-level `<equipment>` overrides beat roster slots and reach every set, civilian included (1.5.x #636, ported 2026-11)
In `troops_*.xml`, an `<equipment>` written directly under `<Equipments>` is not a default a roster can
override: `BasicCharacterObject.Deserialize` adds the troop's rosters first, then
`MBEquipmentRoster.AddOverriddenEquipments` deserializes each such node into every equipment the troop has,
battle and civilian (v1.4.8 `BasicCharacterObject.cs:412`, `MBEquipmentRoster.cs:121-136`); an empty id
clears the slot (`Equipment.cs:204-221`, `IsItemFitsToSlot` :445-451). 88 troops in `troops_*.xml` get
their mount this way (2026-11-04).
- **Why missed:** on the 1.5.x line a wiring test and two tools were written from the markup, reading the
  roster's slot first as the more specific scope (#636, #641, #696).
- **Prevent:** a check of what a troop carries reads the `<Equipments>`-level override first and the roster
  slot only as the fallback, and treats it as present in the civilian set too. Read the deserializer
  (`pwsh tools/taom-src.ps1 path TaleWorlds.Core.BasicCharacterObject`) before modelling precedence from XML
  shape; `.claude/rules/moduledata-validation.md` states the rule for scripts under `tools/`.
- **Source:** 1.5.x `docs/reviews/rca-elk-2026-09-22.md` finding F3 and `docs/reviews/rca-keyforce-art-wiring-2026-09-29.md` fix review R1, on `origin/bannerlord-1.5.x` (ported by WP142).
```

Stage `docs/reviews/lessons/xslt-moduledata.md` (the pick already staged the rule file).

**Verify**: `grep -c "Model the deserializer" .claude/rules/moduledata-validation.md` prints `1`; `grep -c '^### Troop-level `<equipment>` overrides beat roster slots' docs/reviews/lessons/xslt-moduledata.md` prints `1`; Hook suite as baseline. Commit `docs(rules): v2.0.28 - model the deserializer when reading troop kit`; `Ported-from: df273ea24cb342708622536826d49ed3b07083c9`, `Ported-from: c79a585218ad22841c8db1f273e013385512b332` (its lesson half).

### Step C6: the inventory memory reading is corrected (af990ca76's two corrections)

Both docs are on this line and still say the 2.6 GB that stayed between inventory opens survived two engine collections and is rooted. `af990ca76` corrected that on 1.5.x with v1.5.3 lines; plan 117 left these two files to this WP. Engine facts, re-read on v1.4.8 (`pwsh -NoProfile -File tools/taom-src.ps1 path TaleWorlds.Core.GameStateManager`): `OnPushState` is at :257, its listeners run at :277-279 and its `Common.MemoryCleanupGC()` at :285; `OnPopState` is at :288 and its collection at :313. `MemoryStationSampler` writes `[MemStation] enter` from `ScreenManager.OnPushScreen`, raised under those listeners, so before :285. If a doc already holds its correction (grep below), skip it and name it in the commit body.

1. `docs/investigations/native-commit-audit-2026-08.md`, the `Transient UI spikes` row: directly after `(Codex pass, RCA \`rca-memory-instruments-2026-09-12.md\`)` and before ` | nothing |`, insert `. **Corrected 2026-10-02 on the 1.5.x line, re-cited for v1.4.8:** it survived one collection, the pop's (\`GameStateManager.cs:313\`), taken while the closing screen was still on the stack (the \`[MemStation] enter\` line precedes the push's collection at \`:285\`); the next collection frees it, and the size is cheat mode's every-item list (trace: \`plans/_audit/2026-10-02-perf/evidence/memory/inventory-open-alloc.md\`, on \`origin/bannerlord-1.5.x\` only)`. Leave the row's older `:278/306` cite as written: it is the record being corrected.
2. `docs/reviews/rca-memory-instruments-2026-09-12.md`: append at the end of the file, with one blank line before it:

```
## Correction, 2026-10-02 (1.5.x), re-cited for v1.4.8

Finding 3's conclusion that the 2.6 GB "had survived two engine collections and is rooted, so a release could never have returned it" is wrong on the count. The `[MemStation] enter` line is written before the push's own collection (`GameStateManager.OnPushState` :257, listeners :277-279, `Common.MemoryCleanupGC()` :285 on v1.4.8), so each re-entry reading followed one collection, the pop's (:313), which runs while the closing screen and its layer are still on the stack; they keep their movie and widget tree after release, so the next collection frees it. A release one frame after the pop would return it at close; the removed class ran in the same tick and could not. The size itself is cheat mode's every-item inventory list. Trace (measured on the 1.5.x line): `plans/_audit/2026-10-02-perf/evidence/memory/inventory-open-alloc.md`, on `origin/bannerlord-1.5.x` only.
```

Stage `docs/investigations/native-commit-audit-2026-08.md docs/reviews/rca-memory-instruments-2026-09-12.md`.

**Verify**: `grep -c "Corrected 2026-10-02" docs/investigations/native-commit-audit-2026-08.md` prints `1`; `grep -c "^## Correction, 2026-10-02" docs/reviews/rca-memory-instruments-2026-09-12.md` prints `1`; Docs lint no new dead link; dash check `0`. Commit `docs(perf): v2.0.28 - the inventory memory survived one collection`; `Research: taom-src TaleWorlds.Core.GameStateManager`; `Ported-from: af990ca769da52f408b8e804d127c446b515a379`.

### Part C done criteria

- [ ] 6 commits; Restore, Build, Test: 0 failed, total equals Step A0; tool tests and hook suite as baseline; Docs lint no new finding

## Part D: lessons from this pack's commits (2 commits)

For each row: `git -C "$WT" show <sha> -- docs/reviews/lessons/<file>` gives the 1.5.x entry. Append it to the same file on this tree with the adaptation named, the Source rewritten to the text form (Conventions), and every link into a 1.5.x-only file turned into plain text. Skip a row whose heading already exists (another WP synced it) and name it in the commit body.

| # | Source | File | Entry | Adaptation |
|---|---|---|---|---|
| 1 | 1ce8c370e | build-tooling-workflow | When a fact moves, grep every statement of it | as is (1.5.x history) |
| 2 | 1ce8c370e | build-tooling-workflow | Give the command that writes the file itself | as is |
| 3 | 1ce8c370e | build-tooling-workflow | Untracking a file another live branch tracks ... | add: this line untracked it in WP142; `1b00d1acd` and older still track it |
| 4 | 1ce8c370e | build-tooling-workflow (existing entry, planning :1795) | `nine write tools are now in \`permissions.deny\` in \`.claude/settings.local.json\`,` | append `(untracked since WP142; the git and filesystem servers left \`.mcp.json\` with WP116)` |
| 5 | 0aad0d941 | animation-skeleton | A crash tied to one mesh is not a rule for the next | Source: the 1.5.x `spider.md` and spider-mount ledger entries of 2026-09-29 |
| 6 | 0aad0d941 | build-tooling-workflow | A Visual Studio launch deploys the whole working tree ... | as is (`Main/Properties/launchSettings.json` exists here) |
| 7 | 0aad0d941 | build-tooling-workflow | A subagent's issue number is a claim | as is |
| 8 | 0aad0d941 | build-tooling-workflow | A backup written beside a live module file ships with it | as is (`tools/sweep_module_backups.ps1` exists) |
| 9 | 0aad0d941 | build-tooling-workflow | Before hand-editing a data file, check who writes it | drop `taom_troop_name_strings.xml` and `generate_name_localization_strings.py` (absent here); keep the translation-cache example (`translate_with_claude.save_cache` sorts on save, `translate_with_claude.py:382-386`) |
| 10 | 0aad0d941 | data-content-cultures | A troop joining a tree is read by every consumer ... | Prevent greps `default_group`, `DefaultFormationClass`, `UpgradeTargets` and the `troops_*.xml` readers under `tools/`; drop `GetCulturePoolTroopIds`, `CreatureMountRiders`, `drop_creature_riders` (absent here) |
| 11 | 62157c7e7 | build-tooling-workflow | A memory card's "UNCOMMITTED" is a claim about the past | as 1.5.x history; Prevent unchanged |
| 12 | 62157c7e7 | build-tooling-workflow | Pick a release number no channel already carries | Prevent names `/release` Phase 1 item 4b (Step B2) |
| 13 | 0b08eeeab | adapters-taleworlds-api | An engine cache keyed on a different version counter ... | cite v1.4.8 `PartyBase.cs:343-355` (`PartySizeLimit` against `MemberRoster.VersionNo`) |
| 14 | 0b08eeeab | adapters-taleworlds-api | A broad-phase search radius must exceed the exact filter's edge | `LocatorGrid.cs:126` becomes v1.4.8 `LocatorGrid.cs:131` and `:139` |
| 15 | 0b08eeeab | campaign-mechanics | A resume path must reuse the start path's availability checks | as 1.5.x history |
| 16 | 0b08eeeab | state-lifecycle-save | Every engine callback path re-checks the terminal state | as 1.5.x history |
| 17 | dde30a5b7 | data-content-cultures | A port that touches troop files diffs the whole warning set ... | `generate_enlistment_rosters.py --culture` exists here (:800), no `--verify` |
| 18 | 42693d9bd | data-content-cultures | A baseline run proves it read the baseline ... | keep the generic rule; drop the melee-pass detail and the "Until #812 lands" sentence (no melee pass in this validator) |
| 19 | 42693d9bd | data-content-cultures | Read the decision record before calling data hand-authored ... | as 1.5.x history |
| 20 | 42693d9bd | xslt-moduledata | An artist's rows inside a TAOM tool's marker block belong to the tool | `generate_starter_kit.py` and its `TAOM-STARTER-KIT` markers exist here (:79); drop `HaradWeaponsLiveEditTests` (absent) |
| 21 | 33253ea9d, b3fac3021 | harmony-il | A patch category fails as one unit ... | one entry: `PatchCategoryIndex.Apply` throws at the first failing class and `TryPatchCategory` returns false; a category whose every class was skipped applies nothing and returns true (b3fac3021's second instance). Name `Patch98_HitchProbe`/`HitchProbeInstaller` only if `git grep -l HitchProbeInstaller -- Main` finds it |
| 22 | 33253ea9d | native-cpp-port | A native pin table fails the engine-bump gate on a build it does not list | name `SkeletonBufferInstalledBinaryTests`, `KnownBuilds` and `NativePinGateTests` only if each exists in `TAOM.Tests`, and `MapInconclusiveToFailed` only if `TAOM.Tests/binding-gate.runsettings` holds it; otherwise state the rule alone |
| 23 | 33253ea9d, b3fac3021 | native-cpp-port | (new entry) A figure copied into a reference doc takes its source tag and build with it | from the two recurrence bullets: copy the source's tag with the figure, and read the doc's tag legend and name the build before adding a figure or an address |
| 24 | fd2072a0a | build-tooling-workflow | Commit a single path with `git commit -- <path>` when anything else is staged | cite `0a8d61a4e` and `10f1f0b80` as 1.5.x commits; no RCA link |
| 25 | bc01a545c (via dc275dc4e) | state-lifecycle-save | A config list with a non-empty default needs ObjectCreationHandling.Replace | keep Json.NET's `Auto` append behaviour and the Prevent; Why missed and Source as 1.5.x history (#871); add that this line's providers already use `Replace` (`git grep -l "ObjectCreationHandling.Replace" -- Main`) |

Not taken: the Erebor dwarf-dress entry (content), the mesh-tool guard entry (`strip_upper_mesh_channels.py` is absent here), and 33253ea9d's and b3fac3021's recurrence bullets under entries this line does not have (folded into rows 21 and 23).

Commit D1 (rows 1-12, 24): `docs(lessons): v2.0.28 - process lessons from the 1.5.x records`. Commit D2 (rows 13-23, 25): `docs(lessons): v2.0.28 - data, IL and API lessons from the 1.5.x records`. `Ported-from:` one line per source commit in each, including `bc01a545ca75e847be5a2ad9ab4567b7c2460e54`, `0aad0d941a574d48c43660435457cacfe387d7ec`, `0b08eeeabbba2a58d9a611fc46315d18787b2494`, `dde30a5b72f6cd4390d282903fc697ff0701e03b`, `42693d9bde181d26a14eab890407b0f562a5a958`, `33253ea9d4ab6cb1e93d8f4d82afdc4a61e9d55b`, `b3fac3021dcffbbfb1190a1854fa886dc5d78ff1`, `fd2072a0a1c7179226fc4c0f28e1dc81975ab450` where used.

**Verify** (each commit): the census command shows each touched file grew by exactly the number of new rows; `python tools/lint_docs.py --quick --summary` reports no dead link the Step A0 baseline lacked (a link into a 1.5.x-only RCA would be one); dash check `0`.

## Part E: bookkeeping the earlier WPs left to WP142 (2 commits plus one per lesson batch)

The per-port procedure made every code WP drop its lessons, `feature-map.md`, `docs/INDEX.md` and
`tools/README.md` hunks for WPs 142 to 144 to sync once. This Part takes those for every WP merged
before this one: lessons files (a hand-off that asks for a LESSONS-LEARNED count change, such as WP106's "+1", is moot after F1), `docs/reference/feature-map.md`,
`docs/INDEX.md`, `docs/reference/doc-lookup.md`, `docs/reference/provenance-register.md` (rows a merged WP
deferred, such as WP116's "beyond the ECC row") and `tools/README.md`. `.claude/**`, `CLAUDE.md`
and `AGENTS.md` hand-offs are WP143's; engine docs under `docs/reference/engine/` WP144's.

### Step E0: gather the hand-offs (no commit)

1. Merged WPs: `git -C C:/Users/mikew/source/repos/TAOM branch --merged bannerlord-1.4.5 --list "port148/*"`. Work only from those.
2. Hand-offs: `grep -n -E "WP142|WPs? 142|142 to 144|142 and 143|142/WP143" C:/Users/mikew/source/repos/TAOM/plans/1[0-4][0-9]-*.md | grep -v "plans/14[2-4]-"`; keep the hits in merged WPs' plans; classify each as lesson, feature-map, INDEX, doc-lookup, provenance-register, tools/README or other doc. Starting list from plans written by 2026-11-04 (line numbers in those files at planning; reviews have moved some, so the grep above is authoritative): 102:1143 (plan 020 lessons; `module-taom.md` v2.1.x sentence now points at the D8 paragraph in `release-process.md`), 106:1432, 107:1429, 108:1574, 110:1541 and 1682 (with lesson verdicts; the refuge.md #773 note may now cite WP110's rule), 111:1107, 112:1078, 1080, 1088, 1190, 113:864 to 884, 115:1264 (rows for `ingame_ledger.py` and `check_release_pairing.py`), 116:1204, 117:1702 to 1713 and 1796, 118:1562 and 1687, 119:1199, 122:1668 and 1780, 123:270, 1128, 1137, 124:1131 and 1251, 125:1556, 1557, 1707, 127:1345 to 1363 and 1456, 128:1332, 1342, 1452, 1453, 129:1027, 1682 to 1696, 1831, 130:732, 1746, 1747, 1850, 131:355 and 1526, 132:1651, 133:2202, 2207, 2318, 134:1471 and 1486, 135:1261 (tool rows and the `release-process.md` processor step), 137:1430 (`04c3c4540`'s taom-src sentence, moot after its Part A), 141:1433 to 1449 (a list of tool rows and lessons), and plan 121's "Hand-off report" item "For WP142" at 121:1531 (the `check_map_vista.py` row, owed only if plan 121's Step E1 landed; also 121:278, 291 and 1390); and, only if their branches merged before you start (wave 5 siblings), 136:706, 708, 713 and 795 (about eight lessons and the README rows), 138:1108, 1142, 1143, 1266, 139:1181 and 1268 (`ingame_verdict.py` row, feature-map row and INDEX entry for `ingame-verdicts.md`), 140:1025.
3. Gaps by rule: write this helper to `C:/Users/mikew/source/repos/taom-port/scratch/142/gaps.py` (outside the repo) and run `python C:/Users/mikew/source/repos/taom-port/scratch/142/gaps.py "$WT"`:

```python
import pathlib, re, subprocess, sys
WT = pathlib.Path(sys.argv[1]); BASE = "1b00d1acd"
def git(*a):
    return subprocess.run(["git", "-C", str(WT), *a], capture_output=True, text=True,
                          encoding="utf-8", check=True).stdout
def dirs(ref):
    return set(git("ls-tree", "-d", "--name-only", ref, "Main/Features/").split())
readme = (WT / "tools/README.md").read_text(encoding="utf-8")
fmap = (WT / "docs/reference/feature-map.md").read_text(encoding="utf-8")
index = (WT / "docs/INDEX.md").read_text(encoding="utf-8")
added = git("diff", "--name-only", "--diff-filter=A", BASE + "..HEAD").split()
print("TOOLS added without a tools/README row:")
for p in added:
    if re.fullmatch(r"tools/[^/]+\.(py|ps1)", p) and p.split("/")[-1] not in readme:
        print("  " + p)
new, gone = dirs("HEAD") - dirs(BASE), dirs(BASE) - dirs("HEAD")
print("FEATURE folders added without a feature-map row:")
for d in sorted(new):
    if d + "/" not in fmap:
        print("  " + d)
print("FEATURE folders removed whose row remains:")
for d in sorted(gone):
    if d + "/" in fmap:
        print("  " + d)
print("FEATURE docs added that docs/INDEX.md does not name:")
for p in added:
    if re.fullmatch(r"docs/features/[^/]+\.md", p) and p.split("/")[-1] not in index:
        print("  " + p)
```

### Step E1: tools/README rows

For each tool the helper lists, plus `check_external_loc_coverage.py` (1a2da1e3e) and, if they exist here now, `taom-src.ps1`, `native_archive.py`, `check_engine_member_refs.ps1` and `engine_body_diff.py` (04c3c4540): take the tool's row from `git show origin/bannerlord-1.5.x:tools/README.md` (grep its name), rewrite it for this line (no v1.5.x facts, no `TAOM.Client`, no 1.5.x-only flags: check every flag with `python tools/<tool> --help` or the script's argparse), and put it in the matching section. No 1.5.x row: write one from the tool's docstring. `check_release_pairing.py`: WP115's wording ("checks that the TAOM_Map and LOTRLOME_Armory a release ships pair with this TAOM; pins their ModuleData fingerprints in tools/release_pins.json"). `check_external_loc_coverage.py` (identical to 1a2da1e3e's copy): its 1a2da1e3e row without the 2026-10-14 #792 clause. `check_map_vista.py`, only if `tools/check_map_vista.py` exists: plan 121's row as its "Hand-off report" gives it, after the `check_prefab_budget.py` row; if the tool is absent, no row is owed. Also update every existing row a merged WP's hand-off names for new behaviour, changed flags or a stale description (for example WP141's list, WP118's `complete_lords_xslt.py`, WP128's `generate_enlistment_duty_strings.py`, WP132's `b0625f0d1` line), checking each claim against the tool on this tree.

**Verify**: rerun the helper: the TOOLS list is empty. Commit `docs(tools): v2.0.28 - README rows for the tools the port added`; `Ported-from:` 1a2da1e3e and 04c3c4540, plus each 1.5.x commit whose row you took.

### Step E2: feature map, INDEX, doc-lookup and provenance rows

For each FEATURE line the helper prints and each feature-map, INDEX, doc-lookup or provenance-register hand-off: take the 1.5.x row (`git show origin/bannerlord-1.5.x:docs/reference/feature-map.md`, `:docs/INDEX.md`), rewrite it for this line (patch numbers as this line registers them, wiring as this tree has it: the module where the WP ported one (D2), `IoC.cs`/`SubModule.cs` otherwise, no 1.5.x feature named); delete the row of a removed feature (WP123's AutoResolveDiagnostics). Apply the "other doc" hand-offs in the same commit (WP102's `module-taom.md` sentence, which can now point at the "Version numbers on the 1.4.x line" paragraph of `release-process.md`; WP135's `release-process.md` processor step; WP110's refuge.md #773 note, citing WP110's rule only if `.claude/rules/csharp-architecture.md` holds it), each checked against this tree. WP128: never copy 1.5.x feature-map wording that claims what this line lacks.

**Verify**: rerun the helper: the FEATURE lists are empty; Docs lint no new dead link; Doc graph exit 0. Commit `docs: v2.0.28 - feature map and index rows for the ported features`.

### Step E3 onward: deferred lessons, one commit per batch of source WPs (merge order)

For each lesson hand-off: `git show <1.5.x sha> -- docs/reviews/lessons/` gives the entries. Port an entry when its rule holds on this tree (re-cite every engine line to v1.4.8 via `taom-src`, every TAOM file and line to this tree; a plan's own verdicts, such as WP110's, decide), with the text-form Source, and only if its heading is absent. Skip an entry built on a 1.5.x-only feature, file or engine behaviour; list skipped entries in the commit body with one-line reasons. Subject `docs(lessons): v2.0.28 - lessons the WP<first>-WP<last> ports deferred`, `Ported-from:` per source commit. Keep each commit to about 15 entries.

If this Part cannot finish in one session, stop at a commit boundary and report the next source WP; a later session resumes here. Part F waits until every merged WP's lesson hand-offs are done (D18).

### Part E done criteria

- [ ] The helper prints four empty lists; every hand-off hit from Step E0 is ported, rewritten or skipped with a reason in a commit body
- [ ] Restore, Build, Test: 0 failed, Step A0 total; Docs lint no new dead link; Doc graph exit 0

## Part F: drop the lesson counts, then union-merge the lessons files (2 commits; F2 is the last commit of WP142)

### Step F1: the lessons index keeps no counts (43db0b47a's index hunk; supersedes 35f212c60's recount)

The 1.5.x line stopped printing lesson counts on 2026-09-27 (`43db0b47a`): nothing computed them, so they
drifted (here every category is stale, adapters 43 against 50 at planning), and with union merges a count line
would be the one place two branches still collide. In `docs/reviews/LESSONS-LEARNED.md`:
1. In the "**Before touching a subsystem**" bullet, replace the text from `and far cheaper than reading all` through the end of that bullet with `and far cheaper than reading every lesson at once, which is why the record is split per category (split 2026-07-12). The index keeps no lesson counts: nothing computed them, so they drifted, and every merge that touched a lessons file had to recount them. To count, run \`grep -c '^### ' docs/reviews/lessons/*.md\`.`
2. In `## Categories`, delete the `, N lessons` tail of each of the 13 lines (keep the links).
Leave the other two bullets as they are. Commit `docs(lessons): v2.0.28 - the lessons index keeps no counts`; `Ported-from: 43db0b47a73acc19f522e6d11ab31a8a0b6daced` (its `LESSONS-LEARNED.md` hunk only; the `plans/_audit` half is 1.5.x).

**Verify**: `grep -c -E ", [0-9]+ lessons" docs/reviews/LESSONS-LEARNED.md` prints `0`; `grep -c "lessons/.*\.md)" docs/reviews/LESSONS-LEARNED.md` is at least 13; Docs lint no new dead link.

### Step F2: correct the renown bands; union-merge the lessons files (4b7e36250; D18)

1. RED: create `tools/tests/test_lessons_merge_union.py`:

```python
"""docs/reviews/lessons/*.md merge with git's union driver, CHANGELOG.md never does (WP142, D18)."""
import pathlib
import subprocess
import unittest

REPO = pathlib.Path(__file__).resolve().parents[2]


def merge_attr(path):
    out = subprocess.run(["git", "check-attr", "merge", "--", path], cwd=REPO,
                         capture_output=True, text=True, check=True).stdout
    return out.strip().rsplit(": ", 1)[-1]


class LessonsMergeUnionTests(unittest.TestCase):
    def test_every_lessons_file_merges_with_union(self):
        files = sorted((REPO / "docs/reviews/lessons").glob("*.md"))
        self.assertTrue(files)
        for f in files:
            self.assertEqual(merge_attr(f.relative_to(REPO).as_posix()), "union", f.name)

    def test_changelog_never_merges_with_union(self):
        self.assertNotEqual(merge_attr("CHANGELOG.md"), "union")


if __name__ == "__main__":
    unittest.main()
```

   `python -m unittest discover -s tools/tests -t . -p "test_lessons_merge_union.py"`: the first test FAILS (`unspecified`), the second passes. If the first passes, STOP.
2. `git -C "$WT" cherry-pick -n 4b7e36250`. Expected: a conflict in `.gitattributes` only (`docs/modding/clans.md` applies cleanly). Any other conflicted or staged path: `git -C "$WT" reset --merge` and STOP (`cherry-pick --abort` errors after `-n` because no cherry-pick is recorded as in progress; `reset --merge` restores the tracked files and keeps the untracked test file).
3. Resolve `.gitattributes`: keep this tree's whole file and append, with one blank line before it (the 1.5.x `Shaders/Sources` lines are not taken):

```
# The lessons files are append-only: two branches that each append a lesson at the end
# conflict, and keeping one side silently drops a lesson (merge 70c164bb3 on the 1.5.x line
# lost one that way). union keeps both sides' new lines. It cannot see a conflict, so two
# edits of the SAME existing line both survive: reread the entry after such a merge.
# Never on CHANGELOG.md (maintainer decision D18; tools/tests/test_lessons_merge_union.py).
docs/reviews/lessons/*.md merge=union
```

   Then `git -C "$WT" add .gitattributes`.
4. `docs/modding/clans.md`: the cherry-picked line cites `DefaultClanTierModel.cs:11, 44-50`; change it to `DefaultClanTierModel.cs:11, 43-49` (v1.4.8). The rest of the sentence holds: the roll goes up to 60 percent of the band (`next - (next - floor) * 0.4`), tier 4 900 to about 1,770, tier 5 2,350 to about 4,630, tier 6 6,150 to about 7,050.
5. GREEN: the union test file passes (2 tests); `git -C "$WT" check-attr merge -- docs/reviews/lessons/misc.md CHANGELOG.md` prints `merge: union` for the first and `merge: unspecified` for the second.

Stage `.gitattributes docs/modding/clans.md tools/tests/test_lessons_merge_union.py`. Commit `docs(clans): v2.0.28 - correct renown bands; union-merge lessons`; body: the clan guide said 40 percent, the engine rolls up to 60 percent of the band; lessons files now merge with the union driver so parallel appends never drop an entry; CHANGELOG stays a normal merge; reread an entry after a merge that touched the same line on both sides. Trailers: `Research: taom-src DefaultClanTierModel :11, :43-49; git 2.54 gitattributes (merge applies to cherry-pick)`, `Ported-from: 4b7e36250b969c05abea32876795b4b4c3178c80`.

### Part F done criteria

- [ ] F2 is the branch's last commit; tool tests `Ran` = baseline + 2, no new failure
- [ ] Restore, Build, Test: 0 failed, Step A0 total; hook suite as baseline

## Part G (optional follow-up after WP142 merges; not in its done criteria)

The doc-truth commits also change many hunks this plan did not verify (464 of 703 hunks on files
present here had their removed lines present on 1b00d1acd). A later session may take them on a new
branch from the merged trunk: for each such hunk, take it only when every count, path, line, version
and identifier in the added text re-measures true on this tree and it names nothing 1.5.x-only;
otherwise leave it. Part G never touches `docs/reviews/lessons/`. Candidate listing: for each
truth-pass commit, the hunks of `git diff <sha>^ <sha> -- <doc>` whose `-` lines all occur in the live doc.

## Disposition table

| 1.5.x commit | Subject (short) | Disposition | Step |
|---|---|---|---|
| `c83352298` | players need no separate Harmony or MCM | adapted: v1.4.8, no "same version" claim | A3 |
| `7f02fc8d3` | plans 006-019 and the Opus audit | skipped: desktop executor specs anchored on 1.5.x; the port programme's plans 101-144 supersede them, and each implementing WP read them through `git show` | none |
| `66e3fd593` | document OOB Auto-Assign | partial: the CompanionTactics feature-map row without Assign Heroes, the test count 8 to 9; Auto-Assign docs and CHANGELOG left | B6 |
| `470ee64e9` | untrack local files, pin MCP servers | adapted: untrack (M1), citations to `1b00d1acd`, ignore block, docs; denies per branch; pins | A1, A2 |
| `8a6388311` | drop stale counts, test command | adapted: counts and test command; no v1.5.3 or 1.5.x-branch text | A3 |
| `1ce8c370e` | review follow-ups for plan 016 | adapted: restore note, kingdom-voices pins, activation wording, manual row, pins paragraph, three lessons; README clone line, `.vscode` example (WP138 deletes it), CHANGELOG, review records left | A1-A3, D |
| `35f212c60` | convergence fixes for plan 016 | adapted: deny-list scope, user servers unpinned, INDEX names `.codex/config.toml`, dev-machines wording; its LESSONS-LEARNED recount superseded by `43db0b47a` (counts dropped, F1); review records left | A1, A2 |
| `43db0b47a` (shared, from WP138's pack) | drop lesson counts, retire sprint scripts | partial: the `LESSONS-LEARNED.md` hunk; the `plans/_audit` half is 1.5.x | F1 |
| `7d13adc36` | newest Serena pin, deny its edit tools | partial: the pin (W2) and the derivation paragraph; the denies and the freeze line came with WP103 (D3) | A2 |
| `0aad0d941` | RCA and lessons for the KEYforce art wiring | partial: six lessons adapted; RCA left | D |
| `df273ea24` | model the deserializer when reading troop kit | ported (cherry-pick -n) with its lesson target | C5 |
| `62157c7e7` | channel numbers, version bumps, hook gap | adapted: item 4b, W1, live module versions, v2.0.29.5, pack0 prune, two lessons; hook-gap note (closed by WP103), deep-reviewer, git-and-commits, INDEX row, v2.0.33 text, mesh-tool lesson left | B2, D |
| `0b08eeeab` | RCAs for chronicle events and the Eye | partial: four lessons re-sourced and re-cited; RCAs and REVIEW-LOG left | D |
| `2e2a422dc` | War Chronicle and Eye docs current | partial: dev-console rows and exceptions for 1.4.x commands | C3 |
| `86d3a774e` | chronicle gates, hunt under Together, counts | partial: two-test wording, transpiler grep, veto count; Eye, Chronicle, v1.5.5, WotR row (WP110), refuge note left | C3 |
| `1a2da1e3e` | volunteers, Arthedain, catalogues current | partial: module-dependencies #371, troop-progression, castle-recruitment, the loc-coverage tool row | C3, E1 |
| `dde30a5b7` | KEYforce Harad port RCA and lesson | partial: the lesson; RCA left | D |
| `42693d9bd` | KEYforce javelins port RCA and lessons | partial: three lessons adapted; Erebor dress lesson and RCA left | D |
| `33253ea9d` | yotthani review RCA and lessons | partial: two lessons, recurrence folded; RCA and the v1.5.5 bump edit left | D |
| `b3fac3021` | second review of the yotthani items | partial: two lesson points folded; Codex prompt and RCA section left | D |
| `4b7e36250` | correct renown bands; union-merge lessons | ported (cherry-pick -n), comment and line cite adapted; last commit (D18) | F2 |
| `4e7efe5a8` | TAOM towns get a conversation backdrop | partial: the completion-workflow paragraph only; HallOfFire and the rest left (feature) | B1 |
| `fd2072a0a` | commit one path when the index holds more | partial: the new lesson; the client-composition repoint left | D |
| `dc275dc4e` | ADR-001 allows JSON configs; B04 statuses | adapted (maintainer M3): amendment and README lines recounted; B04 docs left | B5 |
| `04c3c4540` | v1.5.6 record after the release, tool rows | partial: closing keyword, snapshot counts, ModuleData hook note, optional packaging rule, tool rows; FQN note superseded by WP137; v1.5.6 record, TRACKING, coop block left | B1, B2, B6, E1 |
| `370b4bf2b` | engine bump writes its Gates | partial: the Gates rule as Phase 4 item 7; RefAsm sentence left (D9) | B3 |
| `d08637c73` | open the repo to outside pull requests | adapted from the 1.5.x tip state of its three files | B4 |
| `bb7da8ff4` | correct stale paths in the living docs | redone by hand on this tree | C1 |
| `2e34cade1` | feature docs behind their code | partial: lord-party-templates counts, map-event-guard cite | C2 |
| `20616aad3` | 23 feature docs | partial: return-to-army, naval-travel, time-acceleration, module-armory; rest to Part G | C2 |
| `8ce80c2ff` | 22 more feature docs | partial: settlement-food, caravan-trade IoC row, army-targeting by hand; rest to Part G | C2 |
| `28b8f6282` | 40 more feature docs | partial: two momentum rows; rest to Part G | C2 |
| `3b1af2777` | 15 feature and modding docs | partial: cultural-feats counts; rest to Part G | C3 |
| `ff1fe1933` | 26 modding and feature docs | partial: localization.md re-measured; rest to Part G | C3 |
| `50e8dc7ae` | reference docs later commits made false | partial: engine-doc lesson links and FillFrom; settlement-economy hunk skipped (K1); 1.5.x pointers left | C4 |
| `66dd97536` | tool rows, tester checklist, ledgers | partial: four engine-study docs, xslt docstring; tool rows via E1 | C4, E1 |
| `24e52abb9` | 25 more feature docs | partial: troop-weight count, armor-revamp manifest claim; rest to Part G | C2 |
| `f4058993a` | finish the feature doc pass | partial: siege-trebuchets, auto-resolve cite; open-questions record left | C2 |
| `d1b2c44c1`, `daeb127e5`, `1b21a2145` | OOB Auto-Assign wiring and fixes | skipped: feature (classifier SKIP-FEATURE) | none |
| `0192a858a`, `4d912ee5f`, `b8988815a`, `f479d96f3`, `6e886fe8d`, `49bf62fea` | review prompts, plan index, review records, footers | skipped: 1.5.x bookkeeping | none |
| `98ca32e0c`, `d94ee1631`, `f6b5bc7fe` | KEYforce art, Harad javelins | skipped: content | none |
| `1d8fc1d8e`, `f7cf6def6`, `a2d3dc6bf` | creature rider rule, catalog rows, career art | skipped: 1.5.x-only fixes | none |
| `12b010626`, `3cdffd8a2`, `c03ed426d` | v1.5.6 engine, yotthani on v1.5.6, BUTR v1.5.6 | skipped: 1.5.x engine | none |
| `bc01a545c` (shared) | Hold the Walls RCA lesson | partial: the ObjectCreationHandling lesson only | D |
| `c79a58521` (shared, lesson half) | (umbrella) | the troop-level equipment lesson (C5); its other lessons through Part E (WP107's hand-off) | C5, E |
| `af990ca76` (shared) | engine performance programme records | adapted: the corrections to `rca-memory-instruments-2026-09-12.md` and `native-commit-audit-2026-08.md`, re-cited for v1.4.8; `battle-load-diagnostics.md` is WP120 Part A3's, the rest WP117's; its lessons and INDEX through Part E | C6, E |
| `2992763f6`, `452796db4`, `434d1d795` (shared) | rules review, skills, commit gates | only their edits to the three B4 files, folded in; the rest is WP143's | B4 |

## Test plan

- New: `tools/tests/test_lessons_merge_union.py` (2 tests: every lessons file has `merge=union`; `CHANGELOG.md` never does), seen RED in F2 step 1 and GREEN in step 5. Pattern: other `tools/tests` modules that read the repo through `REPO = Path(__file__).resolve().parents[2]`.
- Existing gates that cover this WP: `tools/test_hooks.sh` 3b (frontmatter of the new skill), 7k (Serena denies still present); `tools/tests/test_dotnet_commands_flagged.py` (no unflagged dotnet command in skills and reference docs); `SettingsFingerprintTests` (co-op docs' settings counts); `lint_docs.py` (dead links), `check_doc_graph_ratchet.py`, `reviewctl.py lint`, `audit_claude_config.py`.
- Not testable here (for `Not-tested:` lines): MCP server start with the pins (needs a Claude Code and Codex restart); Claude Code start with an untracked `settings.local.json`; the README and CONTRIBUTING.md on GitHub's branch page (after a push); union behaviour across a real merge (documented by git, pinned only as an attribute).
- Verification of the whole WP: Test 0 failed with the Step A0 total; tool tests `Ran` = baseline + 2.

## Done criteria (whole plan)

- [ ] `git -C "$WT" log --format=%s bannerlord-1.4.5..HEAD` lists the subjects of Parts A to F; each passes `python tools/check_commit_msg.py` (write `git log -1 --format=%B <sha>` to the msg file, then check)
- [ ] The last commit is `docs(clans): v2.0.28 - correct renown bands; union-merge lessons`
- [ ] `git -C "$WT" diff --name-only bannerlord-1.4.5..HEAD` contains no `CHANGELOG.md`, no `*.cs`, no `Main/_Module/ModuleData/`, no `Directory.Build.props`, no `plans/`, no `.vscode/`
- [ ] `git -C "$WT" ls-files .claude/settings.local.json _taom_loc.pkl crashz` prints nothing; `git -C "$WT" check-attr merge -- docs/reviews/lessons/misc.md` prints `merge: union`
- [ ] Restore, Build exit 0; Test 0 failed, Step A0 total; tool tests baseline + 2, no new failure; hook suite, Docs lint, Doc graph, Contract, Drift gate and config audit as baseline or better
- [ ] `git -C "$WT" diff bannerlord-1.4.5..HEAD | python -c "import sys; t=sys.stdin.buffer.read().decode('utf-8','replace'); print(sum(1 for l in t.splitlines() if l[:1]=='+' and ('\u2014' in l or '\u2013' in l)))"` prints `0`
- [ ] Final report (the hand-off to the integrator, who maintains `plans/README.md`): status and the branch's commit hashes; the old-plan row to update (`005`, "WP142 takes the MCP pins": name the Step A2 commit); Step A0 baselines, the branch taken in A0 step 3, the measured values written in Parts B and C, skipped rows with reasons, Part E coverage (which merged WPs' hand-offs were done), final suite numbers

## STOP conditions

Stop and report (do not improvise) if:

- A prerequisite marker is missing, `.mcp.json` names a server outside the expected set, or the six files to untrack are not all tracked (Step A0).
- The maintainer declines M1 or M3. Parts B to F do not depend on Step A1 or B5 and stay landable: report and continue only with what does not need them.
- An anchor this plan edits is missing or different (drift), or a measured value contradicts the code it describes in a way this plan did not foresee.
- A step seems to need C#, ModuleData, `CHANGELOG.md`, `Directory.Build.props`, `.claude/settings.json` outside M1, `docs/adrs/` outside M3, `Main/IoC.cs` or `Main/SubModule.cs`.
- An edit would change the MCM settings counts in `coop-interop.md` or `bannerlord-together-compat.md`, or the filtered `SettingsFingerprintTests` fails.
- CLAUDE.md stays over its cap with the minimal routing row.
- `git cherry-pick -n df273ea24` stages anything besides the rule file, or `git cherry-pick -n 4b7e36250` conflicts anywhere but `.gitattributes`.
- The union test passes before the `.gitattributes` line exists.
- The same verification fails twice after a reasonable fix, or a check that passed at Step A0 starts failing.
- A hook in your own session refuses or asks about something this plan did not do (your session must run the main checkout's gates).

## In-game checks owed

None in the game: this WP changes no code or game data. **Live checks owed by the maintainer** after the
integrator merges WP142:

1. Restore `.claude/settings.local.json` from the M1 backup into the main checkout (the merge deletes it), restart Claude Code, run `/mcp`: serena, ilspy, taom-moduledata, imagine and elevenlabs (plus github, git and filesystem in branch servers kept) are listed and trusted as before.
2. Serena pin: `uvx --from git+https://github.com/oraios/serena@7a2968335f2198b966864de1ce3655c8e485a653 serena start-mcp-server --help` exits 0 and lists `--context` and `--project`; in Claude Code a Serena read tool (`find_symbol`) works and `replace_content` is denied. If the pinned commit fails to start, revert the Serena pin line only and record it.
3. Codex: `codex mcp list` (or a Codex session start) shows filesystem and git starting with the pinned versions.
4. After the push (outward, the maintainer's): GitHub's page for the `bannerlord-1.4.5` branch shows the new README install section and CONTRIBUTING.md. The PR template is served from the default branch (`bannerlord-1.5.x`), so this push does not change it.
5. `git check-attr merge -- docs/reviews/lessons/misc.md` in the main checkout prints `merge: union`.

## Maintainer steps

**M1 (Step A1, before its commit).**
1. Back up: `cp C:/Users/mikew/source/repos/TAOM/.claude/settings.local.json C:/Users/mikew/source/repos/taom-port/scratch/142/settings.local.main.json` and the same for the WP142 worktree copy (`.../taom-port/142/.claude/settings.local.json` to `settings.local.wt.json`).
2. `git -C C:/Users/mikew/source/repos/taom-port/142 rm --cached .claude/settings.local.json` (stages the deletion, keeps the file on disk).
3. Branch servers kept only: run this from the WP142 worktree root (idempotent; it changes nothing else):

```python
import json, pathlib
p = pathlib.Path(".claude/settings.json")
d = json.loads(p.read_text(encoding="utf-8"))
NINE = ["mcp__git__git_add", "mcp__git__git_commit", "mcp__git__git_reset", "mcp__git__git_checkout",
        "mcp__git__git_create_branch", "mcp__filesystem__write_file", "mcp__filesystem__edit_file",
        "mcp__filesystem__move_file", "mcp__filesystem__create_directory"]
deny = d.setdefault("permissions", {}).setdefault("deny", [])
deny += [n for n in NINE if n not in deny]
p.write_text(json.dumps(d, indent=2, ensure_ascii=False) + "\n", encoding="utf-8", newline="\n")
```

**M2 (after the integrator merges WP142 into `bannerlord-1.4.5`).** Copy `settings.local.main.json` back to `C:/Users/mikew/source/repos/TAOM/.claude/settings.local.json`; run the live checks above. Never pop, apply or drop `stash@{0}`.

**M3 (Step B5).** In the WP142 worktree, edit (config protection blocks agents):
- `docs/adrs/001-xml-config.md` line 3: `**Status**: Accepted` becomes `**Status**: Accepted, amended 2026-11 (JSON feature configs, below)`. Directly above the `---` that precedes the backlinks block, insert `dc275dc4e`'s "## Amendment: JSON feature configs" section (`git show dc275dc4e -- docs/adrs/001-xml-config.md`) with three changes: the heading date becomes the commit date; `(93 tracked \`.json\` files beside 420 \`.xml\` files on 2026-10-23)` becomes `(<J> tracked \`.json\` files beside <X> \`.xml\` files on <date>)` with the executor's measured J and X; `(\`docs/reviews/lessons/state-lifecycle-save.md\`)` stays (Part D adds that lesson).
- `docs/adrs/README.md`: the ADR 001 row's summary `All config files use XML (no JSON)` becomes `Engine data is XML; a feature's own tuning file may be validated JSON (amended 2026-11)`; the bullet `- Use XML for game config, not JSON (ADR-001)` becomes `- Use XML for engine data; a feature's own tuning numbers may be validated JSON (ADR-001)`.

**M4 (after merge, outward).** Push when ready; close the tracking issue for Part E, W1 and the outside-PR route (the integrator created it before this WP was handed out; see Status); run `/security-scan` (settings and MCP changed).

## Maintenance notes

- After F2, a cherry-pick or merge no longer conflicts on a lessons file. Code WPs that run later must still restore lessons paths they do not own (`git checkout HEAD <path>` once staged), or union will carry 1.5.x lesson text in silently. Union keeps both sides of an edit to the same existing line: reread such an entry. `tools/tests/test_lessons_merge_union.py` fails if anyone adds `merge=union` for `CHANGELOG.md` or drops it for the lessons files.
- Serena is pinned (W2): a pin bump must re-derive the 16 Serena denies in `.claude/settings.json` (maintainer) from the new commit's `ToolMarkerCanEdit` classes, and update `docs/reference/mcp-servers.md`.
- `.claude/settings.local.json` is per-user from now on: every new worktree starts without one; executors run their session in the main checkout, so their hooks and permissions come from there.
- W1: the next `/release` (v2.4.0) bumps `Dependencies/_Module/SubModule.xml` and the TAOM.Dependencies metadata row to the same number, and the live TAOM_Map and Armory manifests by hand.
- WP143 (its plan treats `review-outside-pr` and CONTRIBUTING.md as conditional on existing) will find both: `2992763f6`'s and `452796db4`'s edits to the skill are already folded in, and CONTRIBUTING.md already has the git hooks paragraph (commit-msg only). WP143's `434d1d795` step should edit that paragraph when it ports the pre-commit hook, not add a second one. WP143 also owns the `.claude/rules` and CLAUDE.md hand-offs Part E skipped, and checks that F2's union line exists (its prerequisite P5).
- WP138 deletes `.vscode/mcp.json.example` and edits `mcp-servers.md` (`b84dbaeb2`); if it merges after this WP, expect a small conflict in `mcp-servers.md` (keep both sides' facts). WP144 shares `docs/reference/engine/*.md` with Step C4 and lessons with Part D/E; whichever merges second keeps both changes.
- Generated backlink footers were not rebuilt (1.5.x `6e886fe8d` skipped); a later `tools/build_backlinks.py` run may rewrite many footers at once: give it its own commit.
- Reviewer focus: M1's two branches and the restore path; the B4 skill's Phase 0 and Phase 3 (build and test only on the maintainer's word); every measured value in Parts B and C against its command; Part D's adapted Source lines (no link to an absent RCA); F2's `.gitattributes` comment and the clans.md cite.
- Deferred on purpose: the unverified doc-truth hunks (Part G); REVIEW-LOG rows and every 1.5.x review record (not carried); `GUI/Prefabs` in `.claude/` files (WP143).
