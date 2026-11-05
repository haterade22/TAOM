# Plan 143: Sync the 2026-11-03 rules review, the doc linter's commit view and the .githooks pre-commit gate onto the 1.4.x line

> **Executor instructions**: Follow this plan Part by Part, step by step. Run every verification
> command and confirm the expected result before moving on. If anything in "STOP conditions"
> occurs, stop and report; do not improvise. Each Part (A to G) is landable on its own and has its
> own done criteria; one session per Part is the expected pace. Do not edit `plans/README.md`:
> report status, commit hashes, measured counts and skipped hunks in your hand-off; the integrator
> keeps the index. This plan closes no old-plan row.
>
> **Drift check (run first, Step 0 shows where)**:
> `git diff --stat 1b00d1acd..HEAD -- .claude .ai AGENTS.md CLAUDE.md .editorconfig .gitattributes .githooks setup-dev-env.ps1 CONTRIBUTING.md README.md docs/ai-includes docs/reference docs/modding docs/adrs docs/features/TEMPLATE.md docs/features/doc-health-linter.md docs/reviews/lessons/build-tooling-workflow.md docs/reviews/lessons/localization-ui.md docs/INDEX.md tools/lint_docs.py tools/check_commit_msg.py tools/test_hooks.sh tools/tests tools/README.md`
> Work packages 101 to 142 change most of these files on purpose. Compare the live text with the
> anchors this plan quotes and STOP only when an anchor a step edits is missing or says something
> else (a precondition check in Step 0 fails, or an Edit cannot find its old text).

## Status

- **Priority**: P2
- **Effort**: L (24 owned 1.5.x commits plus two shared hunks; about 28 port commits in seven Parts)
- **Risk**: MED. Nothing touches game code, game data, `Main/`, `Dependencies/` or C#. The risk is
  harness behaviour: Part E deletes seven hooks and changes five gates, Part F adds a git hook that
  binds every committer. A broken gate fails open in silence, so every gate change lands with its
  test rows, and nothing is turned green by editing a test.
- **Depends on**: WP138 (harness review pipeline), WP139 and WP140 (Part G reads their output) and WP142 (docs process sync), merged into local
  `bannerlord-1.4.5`. Through them this plan assumes WP101 to WP109, WP111, WP116, WP117, WP123, WP131,
  WP133, WP136, WP137 and WP141 are merged too. Step 0 proves the pieces it needs.
- **Category**: dx (harness rules, skills, hooks, doc linter, git hooks)
- **Wave / lane**: wave 5, lane H (harness)
- **Executor**: laptop, ordinary Claude Code session started in the main checkout, working in its own worktree
- **Decisions applied**: D1 (no commit stages `CHANGELOG.md`; the commit body is the changelog entry),
  D3 (Serena stays: keep its read tools and the edit-tool denies; skip every Serena removal),
  D7 (from issue-921 only the `/issue` duplicate-search step), D8 (subjects carry the `<Version>` in
  `Main/_Module/SubModule.xml`: `v2.0.28` until the `v2.4.0` release commit), D9 (no CI: amendments say
  so), D10 (an ordinary session executes this file), D18 (lessons files are union-merged since WP142:
  never apply a 1.5.x diff to them), D19 (git only through the Bash tool), and Authority item 2
  (`.claude/settings.json`, `.claude/settings.local.json`, `Directory.Build.props`, `docs/adrs/*.md`
  are changed by the maintainer only)
- **Planned at**: 1.4.x `origin/bannerlord-1.4.5` `1b00d1acd` and 1.5.x `origin/bannerlord-1.5.x`
  `e45f6df2c`, 2026-11-04. The 1.5.x ref has since moved 71 commits (to `31292c035`); none of them is
  in this plan (see Maintenance notes).
- **Issue**: Port of 1.5.x #763 (`2992763f6`), #895 (`7c6d48628`), #877 (`267834a2e`), #921
  (`175b149fd`), #646 (`709649c3f`) and #1078 (`f4b036e95`). The other commits carry no issue (the
  maintainer waived one for the 1.5.x rules review): the orchestrator creates one 1.4.x tracking issue
  for WP143 before implementation lands.

## Why this matters

The 1.4.x harness is still in its 2026-09-14 fork state, and several of its rules send agents wrong
today: the weapon guide tells authors to register short spears in `TwoHandedPolearm` only (the
defect behind the shield-polearm trap), the review reference and `xslt.md` say "NEVER use
SandBox/ModuleData/" although `spclans.xml`, `lords.xml`, `heroes.xml` and `settlements.xml` exist only
there in the installed v1.4.8, the lord skills guide calls `--all-cultures --apply` "the safe
default", `xml-data.md:37` gives settlement prefixes that its own census contradicts, and
`engine-bump/SKILL.md:24` says triage reads offsets from the wEditor DLL while
`tools/native_crash_triage.py` defaults to the shipping client. The completion workflow, `/deep-review`,
`/localize`, the culture guide, the skill-port checklist and `/adopt-external` ordered paid Codex or
translator runs without the maintainer's word on the 1.5.x pre-image, and B1 checks each 1.4.x copy.
Six registered hooks print where nobody reads or repeat another signal, and a seventh refuses every call
to an MCP server for 60 seconds after one failure (`mcp-health-check.sh:34-42`, exit 2). Only the
commit-msg rule (WP103) binds every committer; the gitignored-file, doc and ModuleData gates bind only
commits Claude makes, and the doc linter cannot refuse a dead memory citation or judge what a commit records. After this
plan the rules match 1.4.x and v1.4.8, the dead hooks are gone, and `.githooks/pre-commit` runs the
gitignored-file, doc and ModuleData gates for every committer, terminal and IDE included.

## Current state

### Preconditions from earlier work packages (Step 0 proves each; a missing one is a STOP unless marked)

| # | Expected in the base branch | From |
|---|---|---|
| P1 | `check-changelog-changed.sh` and `check-changelog-updated.sh` gone; `tools/changelog_from_commits.py` exists; AGENTS.md says "The commit body is the changelog entry" | WP102 |
| P2 | `.githooks/commit-msg`, `tools/check_commit_msg.py`, `tools/tests/test_check_commit_msg.py`, `tools/tests/test_dotnet_commands_flagged.py` exist; `tools/lint_docs.py` has `--drift-only`; gates print `hookSpecificOutput`; `.claude/settings.json` denies Serena's edit tools (`mcp__serena__` entries) | WP103 |
| P3 | `.claude/hooks/_shellwords.py` exists; `_pybin.sh` defines `taom_hook_command()` and `TAOM_HOOKS_DIR=` (both at the WP116 pin `88e805450`; the final `block-no-verify.sh` needs them) | WP116 (with WP103) |
| P4 (informational) | `_pushjudge.py`, `_stop_reminder.sh` exist; `_pybin.sh` defines `taom_hook_segments()` (first at `caa7ac356`). A missing one only skips that file's comment hunk in E1 | WP116, WP138 |
| P5 | `.gitattributes` holds `docs/reviews/lessons/*.md merge=union` | WP142 last step (D18) |
| P6 (informational) | which of these exist decides conditional hunks: `.claude/skills/deep-review/lenses/`, `.claude/agents/{deep-reviewer,implementer,architect,fast-reader}.md`, `.claude/rules/{model-effort-selection,data-scripts}.md`, `tools/check_public_text.py`, `tools/native_archive.py`, `tools/native_decompile.py`, `docs/reference/engine/mission-frame-threads-and-native-costs.md`, `docs/reference/moduledata-validator-codes.md`, `CONTRIBUTING.md`, `tools/tests/test_shell_lint_gate.py`, `check_rca_followups` and `check_feature_doc_verify_section` in `tools/lint_docs.py`, `backlinks-start` in `tools/snapshot_api_surface.ps1`, `Dead ends recorded` in `.claude/hooks/session-start.sh` | WP117, WP122, WP136, WP137, WP138, WP142 |

### Facts on `origin/bannerlord-1.4.5` (read at planning; anchors, not line promises)

- `.claude/rules/harmony-patches.md:65-68`: the Common Pitfalls bullets that start "Null handling",
  "Event timing" and "Static state", then "**Reflection in hot paths**". No ResetForUnload rule. `Main/SubModule.cs:106-108` holds the three once-per-process flags
  `_missionTimePatchesApplied`, `_gameInitPatchesApplied`, `_basicTableauGuardApplied`;
  `TAOM.Tests/Features/CoopInterop/ResetForUnloadSweepTests.cs` exists.
- `.claude/rules/csharp-architecture.md:23-31`: the "Non-Negotiable Rules" table ends with the row
  ``| `?.` for computed properties | ... see `adapters.md` |``. No "Writers before gates" row; no
  Hammerhand lesson on 1.4.x (`git grep -c Hammerhand` finds none).
- `.claude/rules/gui-ui.md:68`: `[PrefabExtension] → inject widget into a NON-data-bound container, bind
  to mixin properties`, then a code fence close and `:71` `**Discouraged, on ownership grounds:**`.
  `docs/reviews/lessons/localization-ui.md` (665 lines) has no hover-target entry and no Reeve's Ledger entry.
- `.claude/skills/issue/SKILL.md:77-83`: `## Steps` with 1 "Determine issue type", 2 "Fill in all
  sections", 3 "Run the `gh issue create` command", 4 "Output the created issue URL", 5 "Reference the
  issue number in your next commit message".
- `.claude/rules/xml-data.md:36-37`: `## Region Codes` then the line starting `EN=Rohan, ES=Mordor`.
  `docs/modding/id-cheatsheet.md:94-114` is the census table (prefixes A, DG, E, EN, ES, EW, FH, G, GBC,
  GT, I, K, L, LN, M, MM, R, RU, S, U, V); `:432` is the Gotchas bullet "**The xml-data rule's
  region-code line disagrees with the map.**".
- `.claude/skills/deep-review/SKILL.md` (567 lines) is the monolithic skill: Agent 2 prompt at `:104-140`
  with item `:129` "For Harmony patches: the target method exists with the expected signature"; the
  tooling agent is one long bullet at `:466` listing checks (1) to (7). `TAOM.Tests/Migration/ReflectionSiteBindingTests.cs`
  and `docs/reference/taleworlds-api-snapshot/reflection-sites.md` exist.
- `docs/ai-includes/weapon-creation-workflow.md:228-230`: "Short spears go in `TwoHandedPolearm` only";
  `tools/register_one_handed_polearms.py` exists. `docs/ai-includes/lord-skills-authoring.md:15,145-149,208,469`:
  `--all-cultures --apply` called "the safe default" at `:208`; "17 cultures" at `:208`; "35 archetypes"
  at `:149`; `tools/repoint_evil_lord_skillsets.py` exists. `.editorconfig` has no `[*.{sh,bash}]` section.
- `.ai/review-reference.md` (49,773 bytes): `## XSLT Rules` at `:460` with `:464` "NEVER use SandBox/ModuleData/";
  copies of the path rules at `:232-527`, including "### Existing Overrides (31+ total)" at `:368`
  (the code has 48 `Taom*Model` classes). `.claude/rules/xslt.md:11` repeats "NEVER use SandBox/ModuleData/".
  Installed v1.4.8 (read-only listing): `spclans.xml`, `lords.xml`, `heroes.xml`, `settlements.xml`,
  `spkingdoms.xml` exist only in `Modules/SandBox/ModuleData`; `spcultures.xml` and `spnpccharacters.xml`
  only in `Modules/SandBoxCore/ModuleData`. `tools/check_external_xslt.py` exists.
- `.claude/rules/gamemodels.md` (9,193 bytes): "TAOM has 48 GameModel overrides (47 registered + 1
  abstract base)", a `TaomFooModel` sample using `IAdapterFactory` (0 C# files define or use it), and an
  "## Existing Overrides (48 total ...)" table. `tools/lint_docs.py` `check_model_registry` (`:900`) checks that
  table and `docs/reference/gamemodel-registry.md` (`MODEL_RULES_DOC` `:861`, `MODEL_COUNT_CLAIM_RES` `:871`);
  `tools/tests/test_model_registry.py:136` `test_the_real_catalogues_are_in_sync`.
  `Main/Features/CulturalFeats/Models/TaomPartyMoraleModel.cs` overrides `GetEffectivePartyMorale`.
- Path rules over the 12,288-byte cap 1.5.x applies: `csharp-architecture.md` 25,828,
  `moduledata-validation.md` 25,306 (WP138 may split it), `vanilla-data-comparison.md` 21,780,
  `gui-ui.md` 19,931, `troops.md` 14,738. 1.4.x `lint_docs.py` has no per-rule cap check.
- `.claude/settings.json` registers 28 hooks. Those this plan deletes: `mcp-health-check.sh`
  (PreToolUse `mcp__.*`), `mcp-health-mark.sh` (PostToolUseFailure `mcp__.*`), `pre-compact.sh`
  (PreCompact), `post-compact.sh` (PostCompact), `check-deep-review.sh` and `check-version-tagged.sh`
  (Stop), `session-stop.sh` (SessionEnd). `env` sets `CLAUDE_CODE_EXPERIMENTAL_AGENT_TEAMS: "1"`.
  `session-start.sh:125` opens the untagged-module-version block (re-asserted at every startup; its
  comments at `:129` and `:140` name `check-version-tagged.sh`), and `:176-179` print a TODO/FIXME count.
- Laptop `.claude/logs/agent-audit.log` in the main checkout (from 2026-09-06; 264 spawns at planning, and it grows
  with every session): `debugger` 0, `error-detective` 0, `refactoring-specialist` 0, `taleworlds-researcher` 14. The 1.5.x line counted 1, 0, 0.
- `docs/ai-includes/{tdd-enforcement,security,research-workflow,iterative-problem-solving,multi-approach-validation}.md`
  are byte-identical to `4fbfb7adc^`; `decompiled-code-analysis.md` is an older copy. Files linking them
  (outside history): `.claude/skills/new-feature/SKILL.md`, `docs/INDEX.md`, `docs/reference/doc-lookup.md`
  and `docs/ai-includes/{agent-operating-manual,agent-teams,architecture,code-quality,patterns,taleworlds-research-guide,testing-guide}.md`.
- ADR claims: `IAdapterFactory` has 0 C# files and 8 Markdown files; `ICampaignTimeProvider`,
  `IModulePathProvider`, `IPlayerHeroProvider`, `ICampaignProvider` have 0 C# files;
  `docs/adrs/007-adapter-pattern.md:764` cites `TAOM.Tests/Architecture/AdapterPatternArchitectureTests.cs`, which does not exist (WP136 Step E3 removes the citation; if `git grep -c AdapterPatternArchitectureTests -- docs/adrs` finds nothing, this line is done).
- `tools/native_crash_triage.py:38,463`: `--dll` defaults to `bin\Win64_Shipping_Client\TaleWorlds.Native.dll`.
  `tools/snapshot_api_surface.ps1` (1.4.x) does not strip the backlinks footer that each snapshot file carries.
- Check 11 measured on the 1.4.x tree: 0 missing rule symbols (14 backticked uses of 11 names in
  `.claude/rules`, all found in tracked `.cs` or `tools/*.py`); lines holding a `feedback_` or `feedback-` token: `.claude/rules` 16 (8 files),
  `.claude/agents` 1, `.claude/skills` 36 (10 files), `docs/ai-includes` 25 (5 files),
  `docs/reference/engine` 48 (15 files).
- Under this laptop's Bash tool `grep -c $'\r'` prints 0 even for a CRLF file (`file` reports
  "CRLF line terminators"), so `f4b036e95`'s probe advice holds here.
- `core.autocrlf` is true: markdown is CRLF in the worktree, `*.sh` and `.githooks/*` are LF by `.gitattributes`.

### Engine facts the ported text cites (installed v1.4.8 via `pwsh -NoProfile -File tools/taom-src.ps1 path <Type>`, same lines in the v1.4.8 dump)

- `TaleWorlds.MountAndBlade.Module`: `private void FinalizeSubModulesBases()` `:227` calls
  `value.OnSubModuleUnloaded();` `:231`; `private void FinalizeModule()` `:1268` calls it at `:1283`;
  `internal static void FinalizeCurrentModule()` `:1289`. `TaleWorlds.MountAndBlade.CoreManaged`:
  `[MBCallback(null, false)] internal static void Finalize()` `:113-114` calls `Module.FinalizeCurrentModule();` `:116`.
- `TaleWorlds.CampaignSystem.Roster.ItemRoster`: `public int TotalFood { get; internal set; }` `:31`;
  a livestock element adds `TotalFood += count * item.HorseComponent.MeatCount;` `:449-452`;
  `public int AddToCounts(ItemObject item, int number)` `:185`, `public int AddToCounts(EquipmentElement rosterElement, int number)` `:194`.
- `TaleWorlds.CampaignSystem.Actions.SellItemsAction`: `for (int i = 0; i < number; i++)` `:47` prices each
  unit with `town.GetItemPrice(...)` `:49`.
- `TaleWorlds.GauntletUI.EventManager`: `private static void CollectEnableWidgetsAt(Widget widget, Vector2 position, List<Widget> widgets)`
  `:880`; a child is entered only `if (!child.IsHidden && !child.IsDisabled && IsPointInsideMeasuredArea(child, position))` `:891`;
  `IsPointInsideMeasuredArea` `:742` returns `w.AreaRect.IsPointInside(in p)`.
- `TaleWorlds.MountAndBlade.GauntletUI.Widgets.HintWidget`: `base.ParentWidget.EventFire += ParentWidgetEventFired;` `:15`;
  relays `"HoverBegin"` and `"HoverEnd"` `:31-35`.
- `TaleWorlds.CampaignSystem.GameComponents.DefaultPartyMoraleModel`:
  `public override ExplainedNumber GetEffectivePartyMorale(MobileParty mobileParty, bool includeDescription = false)` `:215`.
- `TaleWorlds.CampaignSystem.Party.PartyBase`: `public CultureObject Culture => MapFaction.Culture;` `:255`.
- ``TaleWorlds.CampaignSystem.Map.DistanceCache.NavigationCache`1``: `public void Serialize(string path)` `:496-537`
  writes two `UInt32` CRCs, an `Int32` count, per record a string id, a `bool` IsPortUsed, an `Int32` pair
  count and pairs of string id, `bool`, `Single` (plus a `Single` land ratio when
  `_navigationType == MobileParty.NavigationType.All`), then the neighbour pairs and the face rows (`Int32`, string, `bool`).

No step binds code to any of these; they are citations inside rules and docs.

### The 1.5.x commits and the cherry-pick simulation (this session)

`git merge-tree --write-tree --name-only --no-messages --merge-base=<sha>^ origin/bannerlord-1.4.5 <sha>`:
clean (exit 0): `5db856e05`, `7c6d48628`, `267834a2e`, `1d88b8633`, `b6b54ce84`. Conflicted (exit 1):
`636b50abf` (CHANGELOG only), `175b149fd` (2 paths), `ed3bb65cc` (2), `2992763f6` (23), `e653257a8` (9),
`625e8fc53` (13), `452796db4` (31), `4fbfb7adc` (48), `250e1b321` (9), `9cfcadd07` (34), `683fd1331` (5),
`3ea14a730` (3), `b6012355b` (3), `f4b036e95` (5), `3ead3765e` (15), `35ff005dc` (18), `434d1d795` (15),
`2129f753c` (4), `709649c3f` (65), `c625ada0f` (292). Between `2129f753c` and `e45f6df2c` none of the
hook, githook, `lint_docs.py` or `check_commit_msg.py` files this plan copies changed, so the blob at
`434d1d795` or `2129f753c` is the 1.5.x end state for them. Replaying commits is wrong here: almost
every file was rewritten by several work packages; each step takes a hunk, a hand edit, or an end-state
blob when the worktree file equals the 1.5.x pre-image.

### Conventions that bind this plan

- No C# changes, so ADR-007 (services take adapters, never sealed TaleWorlds types), ADR-002 (entry
  points under 150 lines), ADR-003 (no `#region`), ADR-004 (no `[Obsolete]`), ADR-005 (no `#if DEBUG`
  outside IoC registration), the MCM json2 rule (rename a persisted setting, never flip its default) and
  `.claude/rules/harmony-patches.md`'s MovementOrder rule do not apply to code here; they appear only as
  text you keep accurate. `Main/IoC.cs`, `Main/SubModule.cs`, `Main/TAOM.csproj` are out of scope.
- TDD for tools and hooks: every tool or hook change in Parts E and F adds its failing test first (RED),
  then the change (GREEN). Docs and rules steps verify with greps and the doc linter.
- `.claude/rules/hook-authoring.md`: a gate fails open on its own fault but never fails silent; bound work
  inside the script below the registered timeout; never a here-string in a hook; never spell `python3`.
- `.claude/rules/output-style.md`: no em or en dash in any line you write; no AI attribution line.
- Never turn a gate green by editing it: no deleting or loosening a test row or allowlist to pass,
  except the adaptations this plan names.

## Commands you will need

Every Bash call starts with `source C:/Users/mikew/source/repos/taom-port/scratch/143/wp143.sh`
(created in Step 0; it `cd`s into the worktree). Pass `timeout: 600000` on calls that run the hook
suite, the tool tests, a full `lint_docs.py` run or `dotnet`. Run git only through the Bash tool (D19).

| Purpose | Command | Expected |
|---|---|---|
| Restore | `dotnet restore TAOM.sln` | exit 0 |
| Build | `dotnet build TAOM.sln -p:DisableModuleCopy=true -p:ModuleId=` | exit 0, 0 errors |
| Test | `dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId=` | 0 failed; total equals the Step 0 baseline (no C# changes) |
| Hook suite | `bash tools/test_hooks.sh` | `N passed, 0 failed`, or no failure the Step 0 baseline lacked |
| Tool tests | `python -m unittest discover -s tools/tests -t .` | no failure the baseline lacked; `Ran` = baseline + the tests you added |
| One tool test file | `python -m unittest discover -s tools/tests -t . -p "<file>.py"` | as stated per step |
| Doc gate | `python tools/lint_docs.py --fail-on-drift --summary` | exit 0 |
| Dead links | `python tools/lint_docs.py --quick --summary` | `dead_links:` no higher than the baseline |
| Instruction contract | `python tools/reviewctl.py lint` | exit 0 |
| Config audit | `python tools/audit_claude_config.py --min HIGH` | no HIGH finding the baseline lacked |
| Message check | `python tools/check_commit_msg.py "$S/msg.txt"` | exit 0, no output |
| Dash check (after staging) | `dash` (helper) | prints `0` |
| CLAUDE.md size | `cmd` (helper), then the doc gate | 46,000 or less (the cap `lint_docs.py` enforces on disk), and `claude_budget:` 0 |

Never `./build.ps1`. Never two builds at once in this worktree. This plan changes no C# and nothing the
build reads, so run Restore, Build and Test in Step 0 and at the end of each Part only.

## Scope

**In scope** (the only files you modify):
- `.claude/rules/*.md` (new: `config-providers.md`); `.claude/skills/**` (text and `freeze/check-freeze.sh`);
  `.claude/agents/*.md` (three deletions); `.claude/hooks/*` (seven deletions, gate deltas, new `check-feedback-memory.sh`)
- `AGENTS.md`, `CLAUDE.md`, `.ai/*.md`, `.ai/routing.json` (Part G only), `.editorconfig`, `.gitattributes` (comments only), `README.md`,
  `CONTRIBUTING.md` (only if it exists), `setup-dev-env.ps1`
- `.githooks/pre-commit`, `.githooks/pre-merge-commit` (new); `.githooks/commit-msg` is not edited
- `docs/ai-includes/*.md` (six deletions), `docs/reference/**/*.md` (new: `gui-prefab-clones.md`),
  `docs/modding/*.md`, `docs/localization/TRANSLATOR_GUIDE.md`, `docs/features/{TEMPLATE,doc-health-linter,commit-gates}.md`
  (`commit-gates.md` new), `docs/research/harmony-fork-feasibility.md` (new), `docs/INDEX.md`,
  `docs/reviews/lessons/{build-tooling-workflow,localization-ui}.md` (appends only)
- `tools/lint_docs.py`, `tools/check_commit_msg.py`, `tools/README.md`, `tools/test_hooks.sh`, `tools/loop_progress.py` (help text, E1),
  `tools/tests/{test_lint_docs,test_check_commit_msg,test_model_registry,test_shellwords,test_dotnet_commands_flagged}.py`,
  new `tools/tests/{test_lint_rule_drift,test_githooks_pre_commit,test_feedback_memory_hook}.py`,
  `tools/tests/test_shell_lint_gate.py` (only if it exists)
- Maintainer-applied, then committed by you: `.claude/settings.json` (Part E), `docs/adrs/*.md` (Part D)

**Out of scope** (do not touch):
- `CHANGELOG.md` (D1); `.claude/settings.local.json`; `Directory.Build.props`; `.mcp.json`; `.serena/**` (D3)
- Any C#, `Main/`, `Dependencies/` (including the `TAOM.Dependencies.csproj` comment `9cfcadd07` edits), ModuleData, `TAOM.Tests/**`
- `plans/` (integrator), `docs/reviews/rca-*.md`, review records, `docs/reference/feature-map.md` rows for 1.5.x folders
- 1.5.x-only files: `docs/ai-includes/{orientation,artist-ladder-brief,git-and-commits}.md`, `docs/adrs/01{1,2,3}-*.md`,
  `.claude/skills/{armory-audit,upstream-review,game-run}/`, `docs/reviews/harness-retro-2026-11-03.md`,
  `docs/reviews/rca-rules-review-2026-11-03.md` (cite the commit hash instead), `docs/reviews/_clan_registry.json`
- The main checkout's uncommitted `.serena/project.yml` and `stash@{0}`

## Git workflow

- Worktree (Step 0): `git worktree add C:/Users/mikew/source/repos/taom-port/143 -b port148/143-harness-rules-sync bannerlord-1.4.5`
- Commit on this branch only. Never push, open a PR, tag, merge, or touch another branch.
- Methods, named in each step:
  - **M-pick**: `git cherry-pick -n <sha>`; resolve; never let a pick, `--continue`, `-C`, `-c` or `--amend --no-edit` commit.
  - **M-apply**: `app <sha> <paths>` (helper: `git diff <sha>^ <sha> -- <paths> | git apply --3way`); resolve any
    `<<<<<<<` region as the step says, then `git add <path>`. If `git apply` says a path "does not match index",
    run `git add <path>` once and retry.
  - **M-blob**: when `same <pre-sha> <path>` prints `SAME`, `gshow <end-sha> <path> > <path>`; otherwise M-apply.
  - **M-hand**: the Edit tool, with the anchor text quoted in the step.
- Stage explicit paths with `git add <paths>` in one Bash call; commit in the NEXT sequential Bash call with
  `git commit -F "$S/msg.txt"`. Never `git add -A`, `-u` or `.`; never `--no-verify`.
- Before each commit: `dash` prints 0, `python tools/check_commit_msg.py "$S/msg.txt"` exits 0, and after any
  CLAUDE.md edit `cmd` prints 46,000 or less and the doc gate shows `claude_budget:` 0.
- Never type `--no-verify` or a force push to a trunk inside a Bash command, even in a file you are writing:
  `block-no-verify.sh` and `validate-push.sh` refuse the whole command. Write such test rows with the Edit or
  Write tool.
- Commit message (write it to `$S/msg.txt` with the Write tool):

```
<type>(<scope>): v2.0.28 - <description, the whole line at most 72 characters>

<Body for 1.4.x readers, wrapped at 72: what changed on this line and
why, what was left out and why. No em or en dashes. No Co-Authored-By
or "Generated with" line.>

Research: <what was verified, e.g. taom-src Module.FinalizeModule>
Not-tested: <what needs a live check, when one applies>
Ported-from: <40-hex sha>          <- one line per included 1.5.x commit
```

## Global port rules (apply to every hunk and hand edit)

- **G1. Engine facts.** Never carry a v1.5.x citation. Re-cite to v1.4.8 (the facts above, or taom-src) or drop
  the citation. Never write that `MBGlobals.GetActionSet` returns a default set (it throws on v1.4.8).
- **G2. Absent targets.** A sentence, row or link naming something absent from the worktree is dropped or
  reworded to the 1.4.x equivalent: `orientation.md`, `git-and-commits.md` (use CLAUDE.md "Commits"),
  `ADR-011`/`ADR-012`/`ADR-013`, `Client/`, `TAOM.Client`, Fords of War, `/game-run`,
  `/armory-audit`, `/upstream-review`, `RequiresGame`/`LiveInstall` categories, RefAsm.
  Lens files, the tier table, `deep-reviewer`, `implementer`, `architect`, `fast-reader`,
  `model-effort-selection.md`, `harness-retro`, `check_public_text.py`, `native_decompile.py`,
  `native_archive.py`, `context_budget_scan.sh`, `graphify_taom.py`, `/review-outside-pr`: use them only when
  `test -e <path>` succeeds (P6). WP142 ports `/review-outside-pr` with `2992763f6`'s and `452796db4`'s edits
  already folded in: never apply those two commits' hunks to it again.
- **G3. Keep 1.4.x specifics**: Bannerlord v1.4.8, `bannerlord-1.4.5` as the trunk, the `v2.0.28` label rule,
  CLAUDE.md "Commits", Serena's read tools and edit-tool denies (D3), the retired CHANGELOG duty (D1),
  machine paths through `docs/reference/development-machines.md`.
- **G4. Counts and dates** measured on 1.5.x are re-measured on the worktree or dropped.
- **G5. Maintainer-only files** (Authority item 2): never write `.claude/settings.json`,
  `.claude/settings.local.json`, `Directory.Build.props` or `docs/adrs/*.md` by any route (Edit, Write,
  `git apply`, `git show >`, cherry-pick, a script). Prepare a patch outside the repo; the maintainer applies it.
- **G6. Lessons files** (D18 made them `merge=union`): never M-pick or M-apply into `docs/reviews/lessons/*.md`;
  append the named entry by hand at the end of the file. A `Source:` line naming a 1.5.x-only RCA cites
  the 1.5.x commit hash instead.
- **G7. Conditional hunks**: when a hunk's file or base is absent, take the fallback the step names, or skip
  and list it under "Skipped hunks" in your report. If you cannot verify a hunk's claim on 1.4.x within
  about five minutes of reading, skip it and list it the same way.
- **G8. Keep what earlier work packages added.** Rule, skill and CLAUDE.md lines that document a mechanism a merged
  WP ported stay, even when the 1.5.x model file has no such line or words it differently. Named by those plans:
  the `Outcome` rows and `/verify` Step 6 (WP139), the 1.4.x release flow and `check_release_pairing.py` (WP115), the
  `verify-bindings`, `engine-bump`, `verify` and `security-scan` lines and the two `harmony-patches.md` gate lines
  (WP122), the Finalizer bullet (WP106), and the rule edits of WP110, WP111, WP130 and WP133. Reconcile the wording; never
  delete the fact. Part C's "use the 1.5.x file as the model" does not override this.
- **G9. Hunks WP138 took early** (its S2, S3, S4): skip `2992763f6`'s first paragraph of `model-effort-selection.md`
  (the maintainer reversed that policy) and `452796db4`'s and `4fbfb7adc`'s harness-retro skill and doc hunks.

## Step 0: Worktree, preconditions, baselines (no commit)

1. From the main checkout: `git worktree add C:/Users/mikew/source/repos/taom-port/143 -b port148/143-harness-rules-sync bannerlord-1.4.5 && mkdir -p C:/Users/mikew/source/repos/taom-port/scratch/143`
2. With the Write tool create `C:/Users/mikew/source/repos/taom-port/scratch/143/wp143.sh`:

```bash
# WP143 helpers: source this at the start of every Bash call.
cd C:/Users/mikew/source/repos/taom-port/143 || return 1
export PYTHONIOENCODING=utf-8
S=C:/Users/mikew/source/repos/taom-port/scratch/143
gshow() { MSYS_NO_PATHCONV=1 git show "$1:$2"; }
same() { [ "$(git hash-object "$2")" = "$(MSYS_NO_PATHCONV=1 git rev-parse "$1^:$2" 2>/dev/null)" ] && echo SAME || echo DIFF; }
app() { local s=$1; shift; MSYS_NO_PATHCONV=1 git diff "$s^" "$s" -- "$@" | git apply --3way --whitespace=nowarn; echo "apply rc=$?"; }
dash() { git diff --cached -U0 -- '*.md' | python -c "import sys; t=sys.stdin.buffer.read().decode('utf-8','replace'); print(sum(1 for l in t.splitlines() if l[:1]=='+' and ('\u2014' in l or '\u2013' in l)))"; }
cmd() { python -c "import pathlib; b=pathlib.Path('CLAUDE.md').read_bytes().replace(b'\r\n', b'\n'); print(len(b)+b.count(b'\n'))"; }
fb() { git grep -n -E 'feedback[_-][A-Za-z0-9]' -- '.claude/rules/*.md' '.claude/agents/*.md' '.claude/skills/*.md' 'docs/ai-includes/*.md' 'docs/reference/engine/*.md'; }
```

3. Preconditions (source the helper first). Each must print as stated; a miss is a STOP:
   - P1: `test ! -e .claude/hooks/check-changelog-changed.sh && test -f tools/changelog_from_commits.py && grep -c 'The commit body is the changelog entry' AGENTS.md` prints 1 or more.
   - P2: `ls .githooks/commit-msg tools/check_commit_msg.py tools/tests/test_check_commit_msg.py tools/tests/test_dotnet_commands_flagged.py && grep -c -- '--drift-only' tools/lint_docs.py && grep -c hookSpecificOutput .claude/hooks/check-commit-subject-version.sh && grep -c mcp__serena__ .claude/settings.json` lists 4 files and prints three nonzero counts.
   - P3: `ls .claude/hooks/_shellwords.py && grep -c -F 'taom_hook_command()' .claude/hooks/_pybin.sh && grep -c -F 'TAOM_HOOKS_DIR=' .claude/hooks/_pybin.sh` lists the file and prints two nonzero counts.
   - P4 (record only): `ls .claude/hooks/_pushjudge.py .claude/hooks/_stop_reminder.sh; grep -c -F 'taom_hook_segments()' .claude/hooks/_pybin.sh`.
   - P5: `grep -c 'docs/reviews/lessons/\*.md merge=union' .gitattributes` prints 1.
   - Version: `grep -o '<Version value="[^"]*"' Main/_Module/SubModule.xml`. If it is not `v2.0.28`, use the printed value in every subject (D8).
   - P6: run `for p in .claude/skills/deep-review/lenses .claude/agents/deep-reviewer.md .claude/agents/implementer.md .claude/rules/model-effort-selection.md .claude/rules/data-scripts.md tools/check_public_text.py tools/native_archive.py tools/native_decompile.py docs/reference/engine/mission-frame-threads-and-native-costs.md docs/reference/moduledata-validator-codes.md CONTRIBUTING.md tools/tests/test_shell_lint_gate.py; do test -e "$p" && echo "HAS $p" || echo "NO  $p"; done; grep -c -e 'def check_rca_followups' -e 'def check_feature_doc_verify_section' tools/lint_docs.py; grep -c backlinks-start tools/snapshot_api_surface.ps1; grep -c 'Dead ends recorded' .claude/hooks/session-start.sh` and keep the output: the conditional steps cite it.
4. Baselines, recorded for your report: hook suite summary and FAIL names; tool tests `Ran N` and failures;
   `python tools/lint_docs.py --fail-on-drift --summary; echo rc=$?`; `python tools/lint_docs.py --quick --summary`;
   `python tools/reviewctl.py lint; echo rc=$?`; `python tools/audit_claude_config.py --min HIGH 2>&1 | tail -5`;
   `cmd`; `fb | wc -l`; Restore, Build, Test totals.

## Part A: small rule rows (up to 7 commits)

### A1: new patches need no ResetForUnload (636b50abf, M-hand)

In `.claude/rules/harmony-patches.md`, directly after the Common Pitfalls bullet that starts `- Static state`
(the line before `- **Reflection in hot paths**`), insert:

```
- **No `ResetForUnload()` for a new patch.** Nothing reloads TAOM inside one process: the engine calls `OnSubModuleUnloaded` only from `Module.FinalizeSubModulesBases` (v1.4.8 `Module.cs:227-233`), reached only from `FinalizeModule` (`:1268-1287`) through `FinalizeCurrentModule` (`:1289-1293`), whose one caller is `CoreManaged.Finalize` (`CoreManaged.cs:113-118`) at shutdown; a rebuild means stopping the game and pressing Play again (maintainer decision, 2026-09-24). A static service cache in a patch class lives exactly as long as the process. The existing `ResetForUnload()` methods stay until their class is next touched, and `ResetForUnloadSweepTests` still requires every one to be called from `OnSubModuleUnloaded`. A reload tool would change this; the three once-per-process flags in `SubModule.cs` (`_missionTimePatchesApplied`, `_gameInitPatchesApplied`, `_basicTableauGuardApplied`) would then need resetting too.
```

**Verify**: `grep -c 'Nothing reloads TAOM inside one process' .claude/rules/harmony-patches.md` prints 1; `grep -c 'v1.5.3' .claude/rules/harmony-patches.md` prints 0.
Commit `docs(rules): v2.0.28 - new patches need no ResetForUnload`; `Research: taom-src Module.FinalizeSubModulesBases, Module.FinalizeModule, CoreManaged.Finalize`; `Ported-from: 636b50abfd80ea4b107b4a66f984d5ef8ab4932c`.

### A2: writers before gates (7c6d48628 and ed3bb65cc, M-hand)

In `.claude/rules/csharp-architecture.md`, directly below the table row that starts ``| `?.` for computed properties |``, add:

```
| Writers before gates | An engine flag, stat or total a plan or gate relies on lists every writer, vanilla first, with citations, and cites the line that builds it, not only its getter (`docs/reviews/lessons/adapters-taleworlds-api.md`; v1.4.8 `ItemRoster.TotalFood` counts livestock meat, `ItemRoster.cs:449-452`, and `SellItemsAction` prices each unit in a loop, `SellItemsAction.cs:47-51`) |
```

Drop: the Hammerhand citation and `docs/reviews/rca-provender-2026-10-23.md` (both 1.5.x-only).
**Verify**: `grep -c '^| Writers before gates |' .claude/rules/csharp-architecture.md` prints 1.
Commit `docs(rules): v2.0.28 - list every writer before gating on an engine flag`; `Research: taom-src ItemRoster (TotalFood), SellItemsAction`; two `Ported-from` lines (`7c6d4862800e7369d957662eb02d12b2485bcb3c`, `ed3bb65cc00ea92fd841733430dfa8be7244d13d`).

### A3: a hover target lies inside every ancestor (267834a2e plus the fcd140907 lesson)

1. M-pick `267834a2e` (simulated clean). If `gui-ui.md` conflicts, keep both sides: the 1.4.x text plus the
   new `**Hover targets:**` paragraph after the PrefabExtension code fence.
2. Leave the new paragraph's citation of `docs/reviews/lessons/localization-ui.md`, #877, as it is: item 3 lands that lesson.
3. G6: unless `grep -c 'A hover target lies inside every ancestor' docs/reviews/lessons/localization-ui.md` already prints 1
   (WP142 synced lessons first), append to the end of that file (Edit tool), separated by one blank line:

```
### A hover target lies inside every ancestor's rectangle: Gauntlet hit-tests by containment (2026-10-23)
`EventManager.CollectEnableWidgetsAt` (v1.4.8 EventManager.cs:880-901) descends into a child only when it is
enabled and the pointer is inside that child's own `AreaRect` (`:891`, `IsPointInsideMeasuredArea` `:742`), and a
`HintWidget` relays only its parent's hover (HintWidget.cs:15, :31-35). A widget placed outside its parent (a
negative `PositionYOffset`, an offset past the parent's edge) still draws, because nothing clips it, but no
pointer ever reaches it, so its tooltip never opens.
- **Why missed:** the markup was copied without its place, and nobody walked the hit test.
- **Prevent:** put a hint on an enabled widget that lies inside every ancestor's rectangle, give that widget
  `DoNotPassEventsToChildren` when it has enabled children (children are collected first), and pin it with a
  structural test (the hint's parent, its events flags, no offset out of the parent). Hover it in game.
- **Source:** 1.5.x `fcd1409074c6eb13709233e5161f6779de1ccd64` (#877), lesson re-cited to v1.4.8.
```

Do not take the second fcd140907 lesson (FillBarHorizontalWidget) or the Reeve's Ledger sentence (both describe 1.5.x-only features).
**Verify**: `grep -c '^\*\*Hover targets:\*\*' .claude/rules/gui-ui.md` prints 1; `grep -c 'A hover target lies inside every ancestor' docs/reviews/lessons/localization-ui.md` prints 1.
Commit `docs(gui): v2.0.28 - a hint's parent must lie inside the bar it sits on`; `Research: taom-src EventManager.CollectEnableWidgetsAt, HintWidget`; `Ported-from:` `267834a2e33ffec947c8b08fd14516f03bb3abe9` and `fcd1409074c6eb13709233e5161f6779de1ccd64`.

### A4: a source the maintainer leaves unnamed (1d88b8633, M-pick)

M-pick (simulated clean). A conflict in `docs/INDEX.md` or `docs/reference/provenance-register.md` (WP142 edits
them): keep the worktree side and apply only the word change "every named third-party source" and the one
sentence ending "gets no row and no mention anywhere in the repo.".
**Verify**: `grep -c '## Sources the maintainer chooses not to name' .claude/rules/provenance.md` prints 1.
Commit `docs(harness): v2.0.28 - a source the maintainer leaves unnamed`; `Ported-from: 1d88b86333617d5efb6b28f29f8ef4a060dd595f`.

### A5: /issue searches the open issues first (175b149fd, D7)

First run `grep -c 'gh issue list --state open --search' .claude/skills/issue/SKILL.md`. WP138 takes this file from
`4407fbc05`, whose step list already opens with this step: if it prints 1, change nothing, make no commit, and record
A5 as "already landed by WP138" in your hand-off. If it prints 0: M-hand in that file under `## Steps`, insert a new
item 1 and renumber the rest by one, keeping their text:

```
1. Search the open issues for the same file, tool or symptom first: `gh issue list --state open --search "<file or symptom>"`. If one exists, update or comment on it instead of filing a duplicate.
```

Skip the 175b149fd lesson and RCA (D7: from issue-921 only this step).
**Verify**: the same grep prints 1; the Steps list numbers 1 to 6 with no gap.
Commit (only if you edited) `docs(skills): v2.0.28 - /issue searches open issues before filing`; `Ported-from: 175b149fd8abd0d52b792be1a5ec4e02503fa7d7`.

### A6: settlement prefixes from the census (c625ada0f, K2 finding F7)

1. M-hand: in `.claude/rules/xml-data.md`, replace the line under `## Region Codes` that starts `EN=Rohan, ES=Mordor` with:

```
Settlement prefixes, measured from the live map in [id-cheatsheet](../../docs/modding/id-cheatsheet.md) "Region prefixes": A=Khand and Harad (Aserai-mapped), DG=Dol Guldur, E=Erebor, EN=Dunland, ES=Mordor, EW=Gondor, FH=Far Harad, G=Gundabad, GBC=Blue Craig, GT=Goblin-town, I=Isengard, K=Khand (Variag), L=Lothlorien, LN=Lindon, M=Mirkwood, MM=Misty Mountains, R=Rivendell, RU=Rhun, S=Dale, U=Umbar, V=Rohan
```

   Keep the 1.4.x lord-prefix paragraph and the custom-culture list unchanged (`AN`, `CA`, `EL`, `RH` and the
   1.5.x realms are not on 1.4.x).
2. In `docs/modding/id-cheatsheet.md` delete the Gotchas bullet that starts `- **The xml-data rule's region-code line disagrees with the map.**` (this step fixes it).
3. Test (a quoted heredoc, so the shell leaves the backticks alone); it prints `21 []`:

```bash
python - <<'EOF'
import re
r = open('.claude/rules/xml-data.md', encoding='utf-8').read()
c = open('docs/modding/id-cheatsheet.md', encoding='utf-8').read()
line = [l for l in r.splitlines() if l.startswith('Settlement prefixes')][0]
ps = re.findall(r'(?:: |, )([A-Z]+)=', line)
cs = set(re.findall(r'^\| `([A-Z]+)` \|', c, re.M))
print(len(ps), sorted(set(ps) - cs))
EOF
```

Commit `docs(rules): v2.0.28 - settlement prefixes follow the live map census`; `Ported-from: c625ada0fa22cc08ce69156e2035a7a9e06a8b73`.

### A7: reflection sites need a binding row; two tooling checks (709649c3f harness hunks)

WP138 takes the lens files from `2c737d97c`, which already carries the reflection bullet and tooling items 14 and 15
(it strips only the #646 clause). First run `git grep -c -e 'plus a row in' -e 'A removed parameter, flag or check' -- .claude/skills/deep-review`.
If it shows `lenses/2-engine-compat.md` and `lenses/tooling.md`, change nothing, make no commit, and record A7 as
"already landed by WP138" in your hand-off. Only for a piece that grep does not show:

- If `.claude/skills/deep-review/lenses/2-engine-compat.md` exists (P6), add the 709649c3f bullet there with
  M-hand (after the Harmony-patch bullet); else add it to `.claude/skills/deep-review/SKILL.md` Agent 2 as a new
  line directly after `   - For Harmony patches: the target method exists with the expected signature`:

```
   - For every member reached by NAME (`AccessTools.Method` / `PropertySetter` / `Field`, `GetMethod` / `GetField` / `GetProperty` with a literal, `TypeByName`): it resolves today, AND it has a `[DataRow]` in `TAOM.Tests/Migration/ReflectionSiteBindingTests.cs` plus a row in `docs/reference/taleworlds-api-snapshot/reflection-sites.md`. A missing row is a finding: the site degrades silently at the next engine bump.
```

- If `lenses/tooling.md` exists, append its items 14 and 15 (from `gshow 709649c3f .claude/skills/deep-review/lenses/tooling.md`,
  lines starting `14.` and `15.`, minus the "#646" example clause); else append to the Step 2c tooling bullet,
  after "(7) **match-count reporting** ...", the sentence: `(8) **a removed parameter, flag or check**: name what input it made the tool reject and run the changed tool on such an input; (9) **a consumer of another tool's report** validates each record (an error record, a zero, a missing field), not only that the key exists.`
- Skip: release pre-flight 5 (WP115 owns it), the Fable model-history edits, everything Animalia, Elk, MonsterSize, ElephantLike, ModuleData and tools.
**Verify**: `git grep -c 'plus a row in' -- .claude/skills/deep-review` prints exactly one file with count 1.
Commit (only if you edited) `docs(review): v2.0.28 - reflection site rows and two tooling checks`; `Ported-from: 709649c3f920dfe86d8cfc1ee0d09a835f2c7eec`.

### Part A done criteria

- [ ] One commit per step that changed a file, with the subjects above (5 to 7: A5 and A7 make none when WP138 already landed them); `git diff --name-only bannerlord-1.4.5..HEAD -- CHANGELOG.md .claude/settings.json docs/adrs` prints nothing
- [ ] Doc gate exit 0; dead links no higher than baseline; Build exit 0 and Test totals equal Step 0

## Part B: rules that caused harm, entry docs and small corrections (3 commits)

### B1: correct the rules that caused harm (2992763f6, adapted)

Per file (M-apply unless stated; resolve conflicts by G1 to G7):

| File | Take | Leave |
|---|---|---|
| `docs/ai-includes/weapon-creation-workflow.md` | the "**Polearm registration:**" paragraph replacing the 1.4.x "Brace + couch-lance" one | |
| `.ai/review-reference.md` | the `## XSLT Rules` body becomes the two-line pointer to `xslt.md` | |
| `.claude/rules/xslt.md` | M-hand: if the bullet `- NEVER use SandBox/ModuleData/` is still there, replace the three bullets under `## Authoritative Source` (the SandBoxCore one, the NEVER one and the Example one) with the 1.5.x bullet that starts `- Where a vanilla file exists in both modules, **SandBoxCore/ModuleData/** is authoritative` (from `gshow 2992763f6 .claude/rules/xslt.md`), then add its two-line bullet naming `tools/check_external_xslt.py` | the v1.5.4 vanilla-file table unless already present |
| `docs/ai-includes/lord-skills-authoring.md` | the "unsafe for live lords" lines, "all cultures" for "all 17 cultures" | |
| `.editorconfig` | `[*.{sh,bash}]` and `[.githooks/*]` with `end_of_line = lf` | |
| `docs/ai-includes/completion-workflow.md`, `docs/ai-includes/new-culture-authoring.md`, `.claude/skills/deep-review/SKILL.md`, `.claude/skills/localize/SKILL.md`, `.claude/skills/adopt-external/SKILL.md`, `.claude/rules/external-skill-ports.md` | every paid-call guard: Codex and `tools/translate_with_claude.py` run only on the maintainer's word; `/localize` seeds by default and records owed translation | the `#763` issue number unless 1.4.x has that issue open (G4) |
| `.claude/rules/gamemodels.md` | the `TaomPartyMoraleModel` sample and its "Live example" line | |
| `.claude/rules/csharp-patterns.md` | removal of the three sample patterns (they use `IAdapterFactory`); keep the 1.4.x "Hook layer" text and the Transpiler note | |
| `.claude/rules/harmony-patches.md` | the `__state` and `[ThreadStatic]`-cleared-in-a-finalizer bullet | |
| `.claude/agents/feature-builder.md` | integration item 1 becomes "Report the exact `Main/IoC.cs` and `Main/SubModule.cs` lines the feature needs; the orchestrator applies them" | |
| `.claude/skills/investigate/SKILL.md`, `.claude/skills/new-feature/SKILL.md` | non-deploying test commands instead of `./build.ps1 -RunTests`; `/verify` as the build check | the `find_symbol (Serena MCP)` removal (D3) |
| `docs/ai-includes/agent-teams.md`, `docs/ai-includes/patterns.md`, `docs/ai-includes/testing-guide.md`, `docs/modding/*.md` | non-command text corrections only | command lines: keep the worktree side (WP103 swept them; `test_dotnet_commands_flagged.py` stays green) |
| `docs/reference/rules-catalog.md` | the `model-effort-selection.md` row only if that rule exists | |
| CLAUDE.md | nothing (its Serena line stays, D3; WP142 added its own `/review-outside-pr` row) | all |
| `docs/localization/TRANSLATOR_GUIDE.md` | nothing (the hunk retargets PRs to `bannerlord-1.5.x`) | all |
| lens 7, `model-effort-selection.md` (never its first paragraph, G9), `artist-ladder-brief.md`, `orientation.md` | only a file that exists (P6) gets its hunk | the rest, and `review-outside-pr` (G2) and `plans/README.md` (the integrator's) always |

**Verify**: `git grep -n -e 'Short spears go in' -e 'NEVER use SandBox/ModuleData' -e 'the safe default' -- docs .ai .claude` prints nothing;
`git grep -n 'IAdapterFactory' -- .claude/rules` prints nothing; `python -m unittest discover -s tools/tests -t . -p "test_dotnet_commands_flagged.py"` OK; doc gate exit 0.
Commit `docs(rules): v2.0.28 - correct the rules that caused harm`; body names the fixed rules and the left-out
hunks; `Research: installed v1.4.8 SandBox and SandBoxCore ModuleData listing; taom-src DefaultPartyMoraleModel`; `Ported-from: 2992763f6c6f777de1d62e4ca4f8a0b83f165aa5`.

### B2: trim the entry docs and always-on rules (e653257a8, M-hand)

The 1.4.x always-on rules and AGENTS.md are older and longer than the 1.5.x pre-image, so take these items
by hand and nothing else. CLAUDE.md imports AGENTS.md (`@AGENTS.md`), so a rule written in AGENTS.md reaches
Claude sessions too: put items 1, 4 and 5 there only, and keep the CLAUDE.md additions to items 2 and 3.
1. Research: AGENTS.md's "Always" bullet that starts `- Research engine behavior` gains the sentence
   `Quote the line that produces an effect; never infer it from a member's name.`
2. Issue waiver: in CLAUDE.md, after the sentence that ends `closed when verified.` in the bullet starting
   `- **Every feature/bug/crash gets a GitHub issue**`, add: `The maintainer may waive the issue for a piece of work; filing one is public, so without a standing word ask once, and record a waiver or decline in the commit body.`
3. Git reads: CLAUDE.md `## Notes` bullet `- No git actions unless explicitly asked` becomes
   `- No git action that changes a ref, the index or the working tree unless asked; reads are free`.
4. TDD: AGENTS.md's bullet that starts `- Use TDD for changes.` gains `A thin entry point needs no unit test of its own.`
5. Preserve others' work: AGENTS.md's first "Always" bullet gains `Give a generator an explicit --out; never write to a path that could be a file handed to the maintainer.`
6. In each always-on rule (`environment-failures`, `evidence-over-claims`, `output-style`, `simplicity-criterion`,
   `think-before-coding`, `working-discipline`): delete a `description:` frontmatter field if present (the
   harness reads only `paths:`), and in `evidence-over-claims.md` replace the "Cadence" table with
   `Compile after each edit, run the filtered suite while iterating, and run the full suite (dotnet test and tools/tests) at each work-item boundary and once before the review gate; quote only that run.`
7. `think-before-coding.md` "When the work is genuinely open-ended": add `Present a decision as the options with a recommendation, one question at a time.` only if no sentence there says it already.
CLAUDE.md headroom is only a few hundred bytes (WP103). If items 2 and 3 would pass 46,000, first make D1 item 2's three
routing-row deletions and E3 item 4's two contract-row deletions in this commit, and skip them later.
Leave: the AGENTS.md section drops (its Code Review Rules and Codex skill list stay on 1.4.x), the risk tier
wording, `orientation.md`, the deleted-agents CLAUDE.md lines (Part D does those), `sed -i` lines, every 1.5.x path.
**Verify**: `cmd` prints 46,000 or less and the doc gate shows `claude_budget:` 0; `python tools/reviewctl.py lint` exit 0; `wc -c < AGENTS.md` under 8192.
Commit `docs(rules): v2.0.28 - entry docs quote effects; issue and git rules`; `Ported-from: e653257a829a1f36c92b32d6b49ea4670600a1e1`.

### B3: correct false lines in skills and rules (f4b036e95)

M-apply `f4b036e95 -- .claude/agents/feature-builder.md .claude/skills/localize/SKILL.md .claude/rules/xslt.md .claude/skills/ship/SKILL.md`, then:
- `ship/SKILL.md` item 8: take the issue wording ("opened on the maintainer's word ... if the maintainer declines, the commit body says so"; write "the maintainer", not the 1.5.x name);
  items 2 and 3 point at `/deep-review`'s Step 2 only if the tier table exists (P6), else keep the 1.4.x text.
- `verify/SKILL.md`: take the "about N" suite-size line only if `/verify` has a floor line; write the Step 0 Test total, not 31,700.
- CLAUDE.md (M-hand): append to the bullet that says never `python3` the sentence
  ``Test for CRLF with `file <path>`; `grep -c $'\r'` reports 0 under this shell.``
Leave: the lens 1 `ServiceLocatorGateTests` line unless lens 1 exists and `TAOM.Tests/Infrastructure/ServiceLocatorGateTests.cs` exists (WP136).
**Verify**: `git grep -n -e '100% service coverage' -e 'Coverage: 100%' -- .claude/agents` prints nothing; `cmd` 46,000 or less.
Commit `docs(harness): v2.0.28 - correct false lines in skills and rules`; `Ported-from: f4b036e95d911b487c36948fa96fddd4f66d5ac0`.

### Part B done criteria

- [ ] 3 commits; doc gate exit 0; `reviewctl lint` exit 0; `cmd` 46,000 or less; Build and Test as Step 0

## Part C: cut the path rules toward 12 KB (625e8fc53, 1 or 2 commits)

Method: re-trim the 1.4.x rule with the 1.5.x post-commit file (`gshow 625e8fc53 <rule>`) as the model, never by
applying the diff. Before deleting a passage, find its destination on the worktree with `git grep`; if the
destination is absent, keep the passage. Keep every MANDATORY rule's instruction; move narratives, copies and
reference tables. Re-cite every engine line to v1.4.8 (G1).

| Rule (1.4.x bytes) | Do |
|---|---|
| `csharp-architecture.md` (25,828) | Target the 1.5.x shape: Non-Negotiable table, IoC Lifetimes, Test Coverage as one sentence ("every decision path has a test, judged by the two-edit test; no percentage is measured"), Entity State Matrix as one paragraph, the per-campaign session-reset rule kept whole, the NaN gate rule as four numbered lines, Lookup Functions, File Layout, the agent-handle and threads section. Move "Config Providers MUST Validate" whole into new `.claude/rules/config-providers.md` with the 1.5.x `paths:` from `gshow 625e8fc53 .claude/rules/config-providers.md`, adapted (drop `Client/` globs) and a one-line pointer left behind. Drop the `TAOM.Client`/ADR-013 rows, and the `IOutcomeLog`/`OutcomeCounter`, `DeferredCallbackQueue`, `MissionThreadGuard` references unless `git grep -l` finds those types in `Main/`. Keep "Stale-file re-read" unless its text exists elsewhere. |
| `gui-ui.md` (19,931) | Move the prefab clone detail into new `docs/reference/gui-prefab-clones.md` (start from `gshow 4fbfb7adc docs/reference/gui-prefab-clones.md`, keep only 1.4.x facts); trim the GauntletLayer input section only where the same text exists in a doc; keep A3's hover paragraph. |
| `vanilla-data-comparison.md` (21,780) | Keep the three opening sections; delete a later section only when its rule already sits in `gui-ui.md` "GUI Prefab Clones Go Stale", `xslt.md` "Passthrough cuts both ways", `docs/reference/armory-guide.md` "action_sets structure" or another named doc (grep first). |
| `troops.md` (14,738) | Remove the race table and the per-tier, royal-last and vanilla-weapon-stat sections only if 1.4.x tooling contradicts them (the 1.5.x reasons: the gate judges DPS, the race table was false; verify with `tools/rebalance_troops.py` and the troop files); move party template bindings to `docs/modding/party-templates.md` (exists). |
| `harmony-patches.md` (10,305) | Replace the lessons-file section with "list a lesson file's headings and read the matching entries"; keep "Which thread runs your target" in place unless `docs/reference/engine/mission-frame-threads-and-native-costs.md` exists with the same table re-verified for v1.4.8. |
| `adapters.md`, `tests.md`, `native-cpp-ports.md`, `provenance.md`, `xml-data.md`, `xslt.md`, `external-skill-ports.md`, `csharp-patterns.md` | Only the false-text removals and named rules: coverage percentages out (`tests.md`, keep the 1.4.x test-category text, no RefAsm), `PartyBase.cs:255` and `ItemRoster.cs:185,194` as v1.4.8 citations (`adapters.md`), the Fab purchase row and the settled `TAOM.dll` wording (`provenance.md`), dead memory citations out (`native-cpp-ports.md`; its `paths:` stay 1.4.x's because `Dependencies/**/*.cpp` exists). |
| `moduledata-validation.md`, `data-scripts.md` | Only the two table-row corrections ("report-only: it always exits 0", "(manual)"), and only if WP138 left the rows. |

**Verify**: `wc -c .claude/rules/*.md` and record before/after; `fb | wc -l` not higher than before; doc gate exit 0;
dead links not higher; `git grep -n -e 'TAOM.Client' -e 'ADR-013' -e 'v1.5.6' -- .claude/rules` prints nothing.
Commit `docs(rules): v2.0.28 - cut the path rules back toward their cap` (split into two commits if the
diff passes about 600 lines); `Ported-from: 625e8fc539d408e8fd8281af60f48b60a997768d` (and `4fbfb7adc8797ae4a6180f2e135ca5849ec08b07` for `gui-prefab-clones.md`).

Part C done criteria: the commit(s) exist; no rule grew; doc gate exit 0; Build and Test as Step 0.

## Part D: skills, agents, guides, ADRs and docs (5 commits, one maintainer step)

### D1: correct the skills, drop three unused agents (452796db4)

1. `git rm .claude/agents/debugger.md .claude/agents/error-detective.md .claude/agents/refactoring-specialist.md`.
2. Fix every live reference: `git grep -n -e 'debugger' -e 'error-detective' -e 'refactoring-specialist' -- CLAUDE.md AGENTS.md .ai .claude docs/ai-includes docs/reference`
   (ignore `tools/`, history and the word "debugger" meaning a native debugger). In CLAUDE.md delete the three
   Skill Routing rows (the `debugger`, `error-detective` and `refactoring-specialist` agent rows) and correct the
   "Custom Agents" count and list to the files that remain in `.claude/agents/`.
3. M-apply `452796db4` for `.claude/skills/freeze/SKILL.md`, `.claude/skills/investigate/SKILL.md`,
   `.claude/agents/feature-builder.md`, `.claude/agents/taleworlds-researcher.md`, `.claude/skills/improve/SKILL.md`,
   `.claude/skills/review-codex/SKILL.md`, `.claude/skills/security-scan/SKILL.md`, `.claude/skills/verify/SKILL.md`,
   `.claude/skills/new-feature/SKILL.md`, `.claude/skills/adopt-external/SKILL.md`, `.claude/skills/deep-review/SKILL.md`. Resolve:
   - `/freeze`: take the git-root path resolution and the memory and scratchpad row.
   - `/investigate`: take the description, "read every line" paragraph, the shared-dimensions list (from the deleted
     `error-detective`), the dead memory removals; Phase 6 ends "name what the maintainer must play and leave it owed"
     (no Fords or `/game-run`). Keep the `find_symbol (Serena MCP)` mention (D3).
   - `/issue` (M-hand, `.claude/skills/issue/SKILL.md`): append to its intro sentence `Creating one is public, so it needs the maintainer's word. Editing a body or adding a progress comment needs no ask.`, plus the `run python tools/check_public_text.py on the text first` clause only if that tool exists.
   - `feature-builder`: Monitor allowed; `taleworlds-researcher`: drop `disallowedTools` only if its `tools:` list lacks Write and Edit.
4. `/native-crash-triage`: M-hand the "read every line" paragraph into its first phase. Its item 7 (SessionStart
   archives native DLLs) only if `tools/native_archive.py` exists and `session-start.sh` calls it; else keep
   the 1.4.x manual-copy advice.
5. `/engine-bump`: take the wEditor correction (1.4.x `native_crash_triage.py` defaults to the shipping client DLL).
   Take the `-Check` correction only if `tools/snapshot_api_surface.ps1` strips the `backlinks-start` footer (P6); else keep 1.4.x's text.
6. If P6 found lens files, `deep-reviewer.md`, `implementer.md` (it gains `Monitor`), `model-effort-selection.md` or
   `.claude/skills/improve/**`, M-apply their `452796db4` hunks too (WP138 left these to this plan; G2, G9). Lens 1: also the
   `250e1b321` phrase "a marker a gate reads (`swallow-ok`)", only if `git grep -l swallow-ok -- TAOM.Tests tools` finds the gate.
Leave: `/release` item 4b, `armory-audit`, `harness-retro`, `upstream-review`, `review-outside-pr`, the improve references and workflows unless present.
**Verify**: `ls .claude/agents/` lacks the three; `bash tools/test_hooks.sh` shows no new failure (3b frontmatter); `cmd` 46,000 or less; `reviewctl lint` exit 0.
Commit `docs(skills): v2.0.28 - correct the skills, drop three unused agents`; body cites the laptop audit log
counts re-measured now (`grep -c agent_type=<name>` and `wc -l` on `C:/Users/mikew/source/repos/TAOM/.claude/logs/agent-audit.log`); `Ported-from: 452796db4b02217624ab0075d5c93742b2820fa3` (plus `250e1b321e8a14452c256b2ed0802ceec0b060d4` if you took its lens phrase). `check-freeze.sh` comes from `9cfcadd07` in E1, not here.

### D2: delete six unread guides, correct the rest (4fbfb7adc)

1. `git rm docs/ai-includes/{tdd-enforcement,security,decompiled-code-analysis,research-workflow,iterative-problem-solving,multi-approach-validation}.md`.
2. Fix each inbound link listed in "Current state" (drop the link or point at the surviving doc that holds the topic:
   `testing-guide.md` for TDD, `taleworlds-research-guide.md` for decompilation). `plans/_audit/` stays untouched.
3. `patterns.md`, `testing-guide.md`: replace with `gshow 4fbfb7adc <path>` and check each link resolves.
   `architecture.md`: the same, minus the ADR-013 client section and every `Client/` line; keep `Composition/` only if
   `Main/Composition` exists. `taleworlds-research-guide.md`: the same, dropping a sentence whose `tools/<x>` or doc link
   is absent. `code-quality.md`: replace only if `git grep -l NullableRatchetGateTests -- TAOM.Tests` finds the test; else leave it.
4. `.ai/review-reference.md`: replace each copied path-rule section with the 1.5.x link paragraph (`gshow 4fbfb7adc .ai/review-reference.md`,
   sections from "ADR Rules, IoC Lifetimes" to "Commit Conventions"; commits link CLAUDE.md "Commits"). Add to
   "Intentional Patterns" only: the ViewModelMixin `IoC.Resolve` line, "Debug builds ship on purpose", "No naval DLC",
   "No new `DependedModule` row", and the `TAOM.dll` wording line if `THIRD-PARTY-LICENSES.txt` has it. Never add
   "No port to bannerlord-1.4.5" or "Desktop only". Keep the 1.4.x lookup-order text (machine paths per G3).
5. M-apply the remaining files, taking only corrections that hold on 1.4.x: `.ai/{README,policy,providers,report-format,scopes,verification}.md`,
   `README.md` (Codex is not mandatory per C# feature), `tools/README.md:8` (not every script has `--dry-run`),
   `docs/features/TEMPLATE.md` (an in-game check names the line that proves it), the maintainer decisions moved
   into `docs/reference/{release-process,development-machines,armory-guide,banner-icon-generation}.md`,
   `docs/modding/module-dependencies.md`, `docs/localization/TRANSLATOR_GUIDE.md`, `docs/features/weapon-xml-pipeline.md`,
   `docs/features/encyclopedia.md`. Skip a decision that names a 1.5.x-only feature, channel or path.
Leave: the plan table, `git-and-commits.md`, `harness-retro.md`, `fords-of-war.md`, `graphify-code-graph.md`, lessons (G6), REVIEW-LOG metrics.
**Verify**: the six files are gone; `git grep -n -e tdd-enforcement.md -e research-workflow.md -e multi-approach-validation.md -e iterative-problem-solving.md -e decompiled-code-analysis.md -e 'ai-includes/security.md' -- . ':!plans/_audit' ':!docs/reviews' ':!docs/changelog-archive' ':!docs/archive' ':!CHANGELOG.md'` prints nothing; dead links not higher; `reviewctl lint` exit 0.
Commit `docs: v2.0.28 - delete guides with no reader, correct the rest`; `Ported-from: 4fbfb7adc8797ae4a6180f2e135ca5849ec08b07`.

### D3: decisions from memory into the docs (683fd1331 and 3ead3765e)

- 683fd1331 (M-apply, then check): `docs/modding/module-map.md` layout paragraph with its citation changed to
  `NavigationCache.Serialize`, v1.4.8 `NavigationCache.cs:496-537`, and the `<!-- measured: ... -->` line dropped
  (it measured the desktop's 1.5.x map); `module-armory.md`, `npcs-notables-and-townsfolk.md`,
  `recipe-add-a-race-or-creature.md` and `TRANSLATOR_GUIDE.md` notes only after `git grep` confirms each id or file
  they name exists on 1.4.x or in the installed modules; `agent-teams.md` "Workflow script gotchas"; new
  `docs/research/harmony-fork-feasibility.md` (`gshow 683fd1331 <path> > <path>`) with an INDEX line. Leave ADR-012, `upstream-review`, `adoption-history.md`.
- 3ead3765e (M-apply, then check): `docs/reference/engine/submodule-lifecycle-and-harmony.md` points at
  `gamemodel-registry.md` and `PatchShield.cs`; `docs/ai-includes/lord-skills-authoring.md` "every `BASE_ARCHETYPES` entry",
  and the `--check` lines only if `python tools/apply_culture_skills_traits.py --check` exits 0 or 1 (WP136, `f1675c058`);
  `testing-guide.md` sentence; `doc-lookup.md` commit-gates row (it needs Part F's doc: add it in F6 instead);
  `tools/README.md` rows only for scripts that exist; provenance rows only for adoption records that exist.
  Leave: README kingdom table, roadmap, the 18 feature-map folders, `harmony-patch-registry.md` (v1.5.x execution text),
  the `mcp-servers.md` davinci row (desktop), `release-process.md` (1.5.x channel text).
**Verify**: dead links not higher; `git grep -n '505-535' -- docs/modding/module-map.md` prints nothing.
Commit `docs: v2.0.28 - decisions move from private memory to the repo`; `Research: taom-src NavigationCache.Serialize (the generic NavigationCache of T)`; two `Ported-from` lines (`683fd1331f6b8bf5ab397984292b0f47b00a37cb`, `3ead3765eb9b357725da0ef127db6c4cbb35d1aa`).

### D4: ADR amendments (250e1b321 and 3ead3765e's ADR-008 hunk; maintainer step M1)

G5: you prepare, the maintainer applies.
1. Build each target in `$S/adrs/` (outside the repo): start from the worktree ADR, apply the 1.5.x dated amendment
   text (`gshow 250e1b321 docs/adrs/<file>`) by hand for 000, 001, 002, 003, 004, 005, 007, 008, 009, 010 and README.
   1.4.x truths to write: `IAdapterFactory` and the four provider interfaces have no C# file; `AdapterPatternArchitectureTests.cs`
   does not exist; no CI since `ccef172ad` (1.4.x's own removal, D9). If WP136 ported `8caac7da5` (`ls
   TAOM.Tests/Infrastructure/{EntryPointLineCountGateTests,ServiceSignatureGateTests,ServiceLocatorGateTests}.cs`),
   ADR-002 names `EntryPointLineCountGateTests` and ADR-007 names `ServiceSignatureGateTests` and
   `ServiceLocatorGateTests`; otherwise the amendment says no test holds the rule. ADR-008 checklist: `Main/Adapters/`, the feature's folder or
   `Main/Core/Infrastructure/`, the non-deploying test line. Drop lens 1, ADR-011 and ADR-012 text.
2. Make a worktree-relative patch. With the Write tool create `$S/adrs_patch.py`:

```python
# Diff each prepared ADR in the scratch folder against the worktree copy, as one git-applicable patch.
import difflib, pathlib
W = pathlib.Path("C:/Users/mikew/source/repos/taom-port/143/docs/adrs")
A = pathlib.Path("C:/Users/mikew/source/repos/taom-port/scratch/143/adrs")
out = []
for p in sorted(A.glob("*.md")):
    old = (W / p.name).read_text(encoding="utf-8").replace("\r\n", "\n").splitlines(True)
    new = p.read_text(encoding="utf-8").replace("\r\n", "\n").splitlines(True)
    out.extend(difflib.unified_diff(old, new, "a/docs/adrs/" + p.name, "b/docs/adrs/" + p.name))
pathlib.Path("C:/Users/mikew/source/repos/taom-port/scratch/143/adrs.patch").write_text("".join(out), encoding="utf-8", newline="\n")
```

   Run `python "$S/adrs_patch.py" && git apply --check "$S/adrs.patch"`: exit 0 (the check writes nothing). Keep a
   leading BOM in a prepared file only where the worktree file has one.
3. **Maintainer step M1**: report the patch path and stop. The maintainer reviews it and runs
   `git -C C:/Users/mikew/source/repos/taom-port/143 apply C:/Users/mikew/source/repos/taom-port/scratch/143/adrs.patch`
   (if that refuses on line endings, the same command with `--3way`).
4. After the maintainer's go-ahead: `git diff --stat -- docs/adrs` lists only the files you prepared; stage them; dash check.
Commit `docs(adrs): v2.0.28 - amend the ADRs, drop false samples and gates`; `Ported-from: 250e1b321e8a14452c256b2ed0802ceec0b060d4`.

### D5: agent teams wording (b6012355b docs half)

M-hand in `docs/ai-includes/agent-teams.md`: replace the "What Are Agent Teams?" section with a "Terms" section
(lead, teammate, `SendMessage` addressed by name; "the experimental teams flag is not needed: `SendMessage` is offered
without it, and nothing here calls `TeamCreate` (probed on the 1.5.x line, 2026-11-03)"), and add the 2026-09-05
budget-incident paragraph after the fan-out width paragraph (`gshow b6012355b docs/ai-includes/agent-teams.md`).
Do not link `harness-retro-2026-11-03.md`. The settings line is maintainer step M2 (Part E).
**Verify**: `grep -c 'CLAUDE_CODE_EXPERIMENTAL_AGENT_TEAMS=1' docs/ai-includes/agent-teams.md` prints 0.
Commit `docs(harness): v2.0.28 - agent teams need no experimental flag`; `Ported-from: b6012355be4f21100a890e62a7774cf7e82e3391`.

Part D done criteria: 5 commits (D4 after M1); dead links not higher; doc gate exit 0; hook suite no new failure; Build and Test as Step 0.

## Part E: hooks (3 commits, maintainer step M2)

### E1: remove dead hooks, fix the gates (9cfcadd07 minus Serena; 434d1d795's feedback hook)

1. RED first: transplant into `tools/test_hooks.sh` the 9cfcadd07 rows for the gate behaviours below (read them with
   `gshow 9cfcadd07 tools/test_hooks.sh`, section by section: the row that an untracked, unstaged file under
   `.claude/` no longer denies a commit, the config-protection prefilter rows, the polearm worktree row, the
   verification-evidence cwd row) and add
   `tools/tests/test_feedback_memory_hook.py` from `gshow 434d1d795 tools/tests/test_feedback_memory_hook.py`,
   its `assertIn("ADR-011", ...)` changed to `assertIn("goes in the repo", ...)`. Run both: the new rows fail.
2. Delete: `git rm .claude/hooks/{post-compact,pre-compact,check-deep-review,check-version-tagged,session-stop,mcp-health-check,mcp-health-mark}.sh`
   (skip any already absent). Remove each `tools/test_hooks.sh` row that runs a deleted hook, as `9cfcadd07` did.
3. Gates, each M-blob when `same 9cfcadd07 <path>` says SAME, else M-apply `9cfcadd07`:
   `check-claude-files-tracked.sh` (gitignored half only), `check-polearm-shield-parity.sh` (audits the edited file's tree),
   `check-verification-evidence.sh` (then M-apply `434d1d795` for its one-line `|| true`), `validate-push.sh` (drop the
   invisible warning), `mark-verification-run.sh`, `log-agent.sh`, `_pushjudge.py`, `_stop_reminder.sh`, `_pybin.sh`
   (a file or function absent per P4 skips its comment-only hunk), `.claude/skills/freeze/check-freeze.sh` (memory and
   scratchpad allowed), `tools/loop_progress.py` (help text; WP138 left it here). `config-protection.sh`: take only the
   prefilter (the payload must name settings, `directory.build.props` or `adrs` before Python starts); do NOT take the
   README and new-ADR allowance (Authority item 2 keeps every `docs/adrs/*.md` maintainer-only).
   `check-moduledata-validation.sh`: the honest deny message (findings may sit in the live modules) and the
   "exited 1 with no findings" unchecked path from 434d1d795; never `TROOP_ID_REMOVED` or `generate_career_kits.py`.
4. `session-start.sh` (M-hand): directly above the comment line `# A WindowsApps interpreter winning the PATH race`
   (1.4.x `:49`, after the PYBIN warning block that ends `fi`), add the 9cfcadd07 lines:
   ``P=$(git config core.hooksPath 2>/dev/null); [[ -n "$P" && -f "$P/commit-msg" ]] || echo "NOTE: core.hooksPath does not name .githooks, so commit-msg is NOT running. Run: git config core.hooksPath .githooks"``;
   delete the TODO/FIXME block and the two `check-version-tagged.sh` comment sentences; change "run /harness-retro" to
   "Tell the maintainer once in your first reply and ask whether to run /harness-retro" only if that banner exists.
5. New `.claude/hooks/check-feedback-memory.sh` from `gshow 434d1d795 .claude/hooks/check-feedback-memory.sh`, its
   header line "(ADR-011)" removed and its message changed to `A correction that should hold on another client or machine goes in the repo. Name the rule, skill or doc that now holds it, or say why this is machine-local.`;
   `git update-index --chmod=+x` after staging.
Skip: every Serena change (D3), `upstream-watch.sh`, `check-graphify-usage.sh`, `context_budget_scan.sh` (unless present), `doc_graph.py` and `graph_query.py` docstrings (D3).
**Verify**: hook suite no failure the baseline lacked and the new rows pass; `python -m unittest discover -s tools/tests -t . -p "test_feedback_memory_hook.py"` OK;
`git ls-files -s .claude/hooks/check-feedback-memory.sh` starts with `100755`; `ls .claude/hooks | grep -c -E 'post-compact|pre-compact|check-deep-review|check-version-tagged|session-stop|mcp-health'` prints 0.
Commit `chore(harness): v2.0.28 - remove dead hooks, fix the gates`; body: the seven hooks and why (six repeated
another signal or reached no reader; `mcp-health-check.sh` refused a server's calls for 60 s after one failure), the
gate fixes, Serena kept by decision; `Not-tested: live harness checks owed (maintainer)`;
`Ported-from:` `9cfcadd07e61e8b21f9c0a296995901990855d02` and `434d1d795465fe9b78c06decd31db5a26558132c`.

### E2: warn when MEMORY.md passes what loads (3ea14a730, adapted)

1. RED: add a `tools/test_hooks.sh` section adapted from 3ea14a730's 7n rows (`gshow 3ea14a730 tools/test_hooks.sh`):
   thresholds 200 lines and 25,000 bytes (the load cap 1.4.x `harness-facts.md` documents), so the rows write
   26,000 bytes and 201 lines; keep the compaction-silence, sibling-worktree and name-containment rows; keep the Dead-ends
   row only if the Dead-ends block exists (P6).
2. GREEN (M-hand in `session-start.sh`): add the `main_checkout_name()` function after the SOURCE parsing, and after
   the core.hooksPath note:

```bash
# MEMORY.md loads only its first ~200 lines or ~25 KB (harness-facts.md); past that a line never reaches a session.
for MEM_F in "$HOME"/.claude/projects/*-"$(main_checkout_name)"/memory/MEMORY.md; do
    [[ -f "$MEM_F" ]] || continue
    MEM_B=$(wc -c < "$MEM_F"); MEM_L=$(wc -l < "$MEM_F")
    if [[ $MEM_L -gt 200 || $MEM_B -gt 25000 ]]; then
        echo "NOTE: MEMORY.md is $MEM_L lines, $MEM_B bytes, past the ~200 lines or ~25 KB that load. Move durable rules to the repo; delete pushed cards."
    fi
    break
done
```

   If the Dead-ends `find` exists, change its memory glob to `"$HOME"/.claude/projects/*-"$(main_checkout_name)"/memory`.
**Verify**: hook suite: the new rows pass, no new failure.
Commit `feat(hooks): v2.0.28 - warn when MEMORY.md passes what loads`; `Ported-from: 3ea14a730b4c811e6d63699616e5df4199bb9416`.

### E3: settings and harness docs (maintainer step M2; 9cfcadd07, b6012355b, 434d1d795)

1. Write `$S/settings_after.py` that reads `.claude/settings.json`, removes every hook whose command ends with one of
   the seven deleted names, removes groups and events left empty, removes `CLAUDE_CODE_EXPERIMENTAL_AGENT_TEAMS` from
   `env` if present, and appends to the PostToolUse `Edit|Write` group
   `{"type": "command", "command": "\"$CLAUDE_PROJECT_DIR\"/.claude/hooks/check-feedback-memory.sh", "timeout": 5}`
   (match the anchoring form the file already uses), writing `$S/settings.after.json` (2-space indent, LF). Show
   `git diff --no-index .claude/settings.json $S/settings.after.json`.
2. **Maintainer step M2**: report the diff and stop. The maintainer copies the file over `.claude/settings.json`
   in the worktree (or edits it to match) and tells you to continue.
3. Check: `python -c "import json;d=json.load(open('.claude/settings.json',encoding='utf-8'));c=[h['command'] for g in d['hooks'].values() for x in g for h in x['hooks']];bad=[n for n in ('post-compact','pre-compact','check-deep-review','check-version-tagged','session-stop','mcp-health-check','mcp-health-mark') if any(n in s for s in c)];print(bad, any('check-feedback-memory' in s for s in c), 'CLAUDE_CODE_EXPERIMENTAL_AGENT_TEAMS' in d.get('env',{}), 'mcp__serena__replace_content' in json.dumps(d))"` prints `[] True False True`.
4. Docs (M-hand): CLAUDE.md Hook Response Contracts: delete the `post-compact.sh` and `check-version-tagged.sh` rows,
   and the hook count sentence under `## Hooks` if it states numbers (G4). `docs/reference/hooks-catalog.md`: delete the
   seven hooks' rows; add `check-feedback-memory.sh` (PostToolUse Edit|Write, additionalContext, informs only); update the
   `session-start.sh` row (core.hooksPath note, MEMORY.md note, no TODO count); the `block-no-verify.sh` row waits for F5.
   `.claude/rules/harness-facts.md`: replace, row by row, the 1.4.x facts these commits correct, copying the
   1.5.x row text with its source column (`gshow 434d1d795 .claude/rules/harness-facts.md`): skill hooks keep running
   for the rest of the session once invoked; Visibility; the `env` block reaches hooks; subagents receive `MEMORY.md`;
   path rules fire only inside this checkout (not a sibling worktree); the skill listing returns after `/compact`.
   `.claude/rules/hook-authoring.md`: M-apply 9cfcadd07 and 434d1d795, keeping 1.4.x text on conflict and taking only
   rules about hooks that exist here. `docs/reference/release-process.md`: the guard is `session-start.sh`'s banner, not
   `check-version-tagged.sh`. `.claude/skills/agent-introspection-debugging/SKILL.md` (the `mcp-health-check.sh` row) and
   `docs/ai-includes/agent-teams.md` (the `pre-compact.sh` sentence): reword without the deleted hook. `.gitattributes`: 9cfcadd07's comment rewording. `docs/reference/rule-provenance.md`:
   the stories table rows for facts you moved. Leave `mcp-servers.md` and `provenance-register.md` Serena edits (D3); take
   `provenance-register.md`'s ECC row path list only for the two deleted MCP hooks.
**Verify**: hook suite (section 2 timeouts) no new failure; `cmd` 46,000 or less; `reviewctl lint` exit 0; doc gate exit 0;
`git grep -n -E 'post-compact\.sh|pre-compact\.sh|check-deep-review|check-version-tagged|session-stop|mcp-health-(check|mark)' -- .claude CLAUDE.md AGENTS.md .ai docs/ai-includes docs/reference tools` prints nothing except dated history sentences, each of which you list in your hand-off.
Commit `chore(harness): v2.0.28 - settings drop dead hooks; harness facts`; `Ported-from:` `9cfcadd07e61e8b21f9c0a296995901990855d02`, `b6012355be4f21100a890e62a7774cf7e82e3391`, `434d1d795465fe9b78c06decd31db5a26558132c`.

Part E done criteria: 3 commits; hook suite and tool tests with no failure the baseline lacked; the settings check prints `[] True False True`.

## Part F: the doc linter's commit view, rule drift and the pre-commit gate (7 commits, maintainer step M3)

### F1: the commit-msg checker refuses a closing list and exits 3 on a crash (35ff005dc)

RED: M-apply `35ff005dc -- tools/tests/test_check_commit_msg.py` (the closing-list and `EntryTests` cases), run it: fails.
GREEN: M-apply `35ff005dc -- tools/check_commit_msg.py`; on conflict keep WP103's `CLAUDE.md "Commits"` citations.
**Verify**: `python -m unittest discover -s tools/tests -t . -p "test_check_commit_msg.py"` OK.
Commit `feat(tools): v2.0.28 - commit-msg refuses a closing keyword list`; `Ported-from: 35ff005dc5bb80870e49e46122b69ae57ac1a916`.

### F2: drop citations of machine-local memory files (prerequisite of check 11)

Run `fb`. For each hit, delete the citation and keep the rule text (a trailing `Memory: ...` sentence, a
`feedback_x.md` parenthetical, a "per `feedback_x`" clause, a `[[feedback-x]]` wikilink, a "Lesson: `feedback-x`" tag). A hit that
names no card (`a feedback-memory entry` in `/codex-verify` and `/deep-review`, a `memory/feedback_*.md` glob) is reworded or removed as
`35ff005dc` did in `/deep-review` ("generalizable rule, or scope extension to an existing rule?"). Never invent a lesson link. Then `fb | wc -l` prints 0.
Commit `docs(harness): v2.0.28 - drop citations of machine-local memory files`; body: the count removed per folder;
`Ported-from: 2129f753c0f8d33730d6303ce3c8dccd6c78423e` (its engine scope).

### F3: the linter judges what a commit records, and rule text drift gates (35ff005dc, 2129f753c)

1. RED: create `tools/tests/test_lint_rule_drift.py` from `gshow 2129f753c tools/tests/test_lint_rule_drift.py`; add the
   35ff005dc `CommitView` and `--staged` cases to `tools/tests/test_lint_docs.py` (`gshow 35ff005dc tools/tests/test_lint_docs.py`,
   the classes that exercise `commit_view`, `_cat_file_batch`, `entry`, `--clean-merge`). Adapt: tests of ADR-011
   budgets, scoped-rule growth and `--context-budget-json` are dropped (1.4.x has none); tests of
   `check_rca_followups`/`--followups` and `--clean-merge` only if those checks exist (P6). Run: they fail.
2. GREEN in `tools/lint_docs.py` (hand port, the 1.5.x file at `2129f753c` is the reference):
   - `_cat_file_batch`, `CommitView`, `commit_view`, `_commit_text`; add `_git_run` and `_is_unborn_head` if absent
     (copy from the reference); `--only-paths` if absent.
   - Thread `commit` through 1.4.x's gating checks: `check_config_example_drift`, `check_version_consistency`
     (`_read_pin(commit)`), and `check_claude_md_budget` (read CLAUDE.md and AGENTS.md through `_commit_text`).
   - Check 11: `DEAD_MEMORY_DIRS = (".claude/rules", ".claude/agents", ".claude/skills", "docs/ai-includes", "docs/reference/engine")`,
     `check_dead_memory_refs`, `check_rule_symbols` (+ `adr_symbols` report-only), `check_unflagged_dotnet` with
     `DOTNET_FLAG_DIRS` equal to `tools/tests/test_dotnet_commands_flagged.py`'s `DIRS`; `rule_drift` gates
     `--fail-on-drift` and `--drift-only`, with its summary counter and report section.
   - `entry()` turns a crash into exit 3; `--clean-merge` skips the new-feature-doc check (only if it exists).
3. `check-doc-config-drift.sh` (M-hand): call the linter with `--drift-only --staged` (keep WP103/WP138's
   `--only-paths` and `--also-new` plumbing); widen the trigger list to `docs/ai-includes/*.md`,
   `.claude/{rules,agents,skills}/*.md` and `docs/{modding,reference}/*.md`; take 434d1d795's quotePath and
   `--no-renames` reads and its `unchecked()` path for exit 2 or 3 and for exit 1 with no findings. If
   `tools/test_hooks.sh` has a 5d-style trigger-coverage section, extend it to the new folders.
4. Only if both `tools/data_gates.py` and `.claude/skills/deep-review/lenses/7-xml.md` exist (WP136, WP138): M-apply `35ff005dc -- .claude/skills/deep-review/lenses/7-xml.md`
   (its gates step calls `data_gates.py`; WP138 left this hunk here). `docs/features/doc-health-linter.md`: check 11 row (with the engine scope) and the `--staged`, `--clean-merge`, exit 3 notes.
**Verify**: both test files OK; tool tests no new failure; hook suite no new failure; doc gate exit 0 with `rule_drift:` 0 in the summary;
`python tools/lint_docs.py --drift-only --staged --summary` exit 0 on a clean index.
Commit `feat(tools): v2.0.28 - the doc linter judges what a commit records`; `Ported-from:` `35ff005dc5bb80870e49e46122b69ae57ac1a916`, `2129f753c0f8d33730d6303ce3c8dccd6c78423e`.

### F4: the registry is the one GameModel catalogue (35ff005dc)

Only if `python -m unittest discover -s tools/tests -t . -p "test_model_registry.py"` passes on the worktree (the
two catalogues agree). RED: M-apply `35ff005dc -- tools/tests/test_model_registry.py`. GREEN: `check_model_registry`
reads only `docs/reference/gamemodel-registry.md` with the single `MODEL_COUNT_CLAIM_RE`; in gamemodels.md replace
the `## Existing Overrides (...)` heading and its table with the 1.5.x `## Existing overrides` pointer paragraph
(`gshow 35ff005dc .claude/rules/gamemodels.md`, its last section) and delete the `TAOM has 48 GameModel overrides` sentence
(leave the 1.4.x "Registration Pattern" section); add to the registry, under its first paragraph,
`TAOM has <N> GameModel overrides, the parked and abstract ones included (tools/lint_docs.py checks the total against the code and that every one is listed here).`
with N from `python -c "import sys;sys.path.insert(0,'tools');import lint_docs as l;print(len(l.discover_model_classes()))"` (48 at planning). Edit `.ai/review-reference.md`'s GameModel link if D2 left a count.
**Verify**: `test_model_registry.py` OK; `python tools/lint_docs.py --summary` shows `model_registry:` 0.
Commit `docs(rules): v2.0.28 - the registry is the one GameModel catalogue`; `Ported-from: 35ff005dc5bb80870e49e46122b69ae57ac1a916`.

### F5: block-no-verify reads git the way bash does (434d1d795)

RED: M-apply `434d1d795 -- tools/tests/test_shellwords.py` and transplant the 434d1d795 `block-no-verify` rows (sections
4c, 4d, 7e) into `tools/test_hooks.sh` with the Edit tool (they contain the banned flag text). GREEN: `_shellwords.py`
and `block-no-verify.sh` by M-blob from `434d1d795` when `same 9cfcadd07 <path>` is SAME for each, else M-apply `9cfcadd07`
then `434d1d795`. Then the `block-no-verify.sh` catalog row (E3 item 4).
**Verify**: `test_shellwords.py` OK; hook suite no new failure; write the payload
`{"tool_name":"Bash","tool_input":{"command":"git commit -n -m x"}}` to `$S/p.json` with the Write tool, then
`bash .claude/hooks/block-no-verify.sh < "$S/p.json"; echo rc=$?` prints `rc=2`; the same with the command
`git commit -m "mention -n only"` prints `rc=0`.
Commit `fix(hooks): v2.0.28 - block-no-verify reads git the way bash does`; `Ported-from: 9cfcadd07e61e8b21f9c0a296995901990855d02`, `434d1d795465fe9b78c06decd31db5a26558132c`.

### F6: commit gates for every committer (434d1d795)

1. RED: `tools/tests/test_githooks_pre_commit.py` from `gshow 434d1d795 <path>`, adapted: remove every
   `generate_career_kits` stub, assertion and test; drop the verify-section tests unless
   `check_feature_doc_verify_section` exists; keep `CodeList` (the hook names every code the Claude gate names).
2. GREEN: `.githooks/pre-commit` from `gshow 434d1d795 .githooks/pre-commit`, adapted: the `--code` list equals the list in
   `.claude/hooks/check-moduledata-validation.sh` exactly; delete the `generate_career_kits` gate line and its name in the header comment; the lint trigger regex
   covers the surfaces `check-doc-config-drift.sh` watches after F3; the comment "Stage one of moving these gates here ... then
   those gates go" becomes `They run beside the Claude commit gates; both stay on this line.` `.githooks/pre-merge-commit` verbatim.
   Stage, then `git update-index --chmod=+x .githooks/pre-commit .githooks/pre-merge-commit`.
3. `setup-dev-env.ps1`: the `core.hooksPath` lines; `CONTRIBUTING.md`: WP142 already wrote a git hooks paragraph naming commit-msg, so edit that paragraph to add pre-commit and never add a second one;
   `tools/tests/test_shell_lint_gate.py`: the `.githooks/*` glob only if the file exists. `docs/features/commit-gates.md`
   from `gshow b6012355b docs/features/commit-gates.md`, adapted to the 1.4.x hooks (no career kits, no retirement date,
   no `git-and-commits.md`), and its `docs/reference/doc-lookup.md` row (3ead3765e). Skip `.gitleaksignore`.
**Verify**: `test_githooks_pre_commit.py` OK; tool tests no new failure; `git ls-files -s .githooks/pre-commit` starts `100755`;
`git ls-files --eol .githooks/pre-commit` shows `i/lf`.
Commit `feat(hooks): v2.0.28 - commit gates for every committer`; `Not-tested: an IDE-bundled git (maintainer)`; three `Ported-from` lines: `434d1d795465fe9b78c06decd31db5a26558132c`, `b6012355be4f21100a890e62a7774cf7e82e3391`, `3ead3765eb9b357725da0ef127db6c4cbb35d1aa`.

### F7: lessons for the tools that landed (b6b54ce84, G6)

Append by hand to `docs/reviews/lessons/build-tooling-workflow.md`, text from `gshow b6b54ce84 docs/reviews/lessons/build-tooling-workflow.md`:
"A git hook that claims to bind every committer runs under the git an IDE ships", "shlex ends a word at any `#`, so drop
comments yourself", "A commit gate reads the tree the commit records, in every check". Add "A table that feeds a decision
gets its rows read" only if `tools/analyze_reviews.py` has the 69d2e8c0a yield rows; skip the ratchet lesson (no ratchet here).
Skip any entry whose heading the file already has (WP142). `Source:` lines cite `b6b54ce844ab925f830c87f41a61f2e472dbba60`.
**Verify**: `grep -c -e 'A git hook that claims to bind every committer' -e 'shlex ends a word at any' -e 'A commit gate reads the tree the commit records' docs/reviews/lessons/build-tooling-workflow.md` prints 3 (commit only if you appended).
Commit `docs(reviews): v2.0.28 - lessons from the commit gate port`; `Ported-from: b6b54ce844ab925f830c87f41a61f2e472dbba60`.

### F8: maintainer step M3 and the final regression

**Maintainer step M3**: `git config core.hooksPath .githooks` once (shared by every worktree; a worktree whose branch
lacks `.githooks/pre-commit` runs only the hooks it has).
**Verify**: `git config core.hooksPath` prints `.githooks`. Then run the full Commands table and record every number.

### Part F done criteria

- [ ] 7 commits (F1 to F7); tool tests and hook suite with no failure the baseline lacked; doc gate exit 0 with `rule_drift:` 0
- [ ] `fb | wc -l` prints 0; `.githooks/pre-commit` and `pre-merge-commit` executable and LF

## Part G: lines earlier work packages left for this plan (1 commit)

These plans handed the rows below to WP143 in their hand-off notes. Work them after Parts A to F, in one commit. Each row
has a condition: when it fails, the source WP is not merged or took another shape, so skip the row and name it in your hand-off
as still owed. Rewrite each line for 1.4.x (G1 to G4); take only the sentence named, never the whole hunk. CLAUDE.md rows must
keep `cmd` at 46,000 or less: if one does not fit, leave it out and report it, starting with G12, then G15.

| # | From | File | Take | Condition |
|---|---|---|---|---|
| 1 | WP106 | `CLAUDE.md` Traps | the `f75288ebf` row title and text "Agent slots recycle; Agent.Tick and callbacks are off-thread" | `git grep -l Patch91 -- Main` finds a file |
| 2 | WP106 | `.claude/rules/harmony-patches.md` | `paths:` gains `Dependencies/Foundation/**` (`bd1ac8292`) | that folder exists |
| 3 | WP106, 108, 114, 117, 131 | `CLAUDE.md` "Harmony Patch Categories" | the count becomes `grep -c '^## Patch' docs/reference/harmony-patch-registry.md`; if that registry has not been synced, leave the number | registry names the WPs' categories (`grep -c Patch91 docs/reference/harmony-patch-registry.md` is nonzero) |
| 4 | WP109 | `.claude/skills/native-crash-triage/SKILL.md` | from `ecaa67d26`, the sentence to read the session's `[SkeletonBuffer] guard` lines. Drop every "Known on v1.5.4" offset | `git grep -l SkeletonBuffer -- Main` finds a file |
| 5 | WP118 | `.claude/skills/engine-bump/SKILL.md` | `a27c6b338`'s `XmlMergeLiveEquivalenceTests` paragraph, minus its runsettings sentence unless `binding-gate.runsettings` exists | `git grep -l XmlMergeLiveEquivalenceTests -- TAOM.Tests` finds a file |
| 6 | WP134 | same | `685db7e46`'s line naming `KingdomPolicyIdsTests.EnginePolicyIds` | `TAOM.Tests/Core/KingdomPolicyIdsTests.cs` exists |
| 7 | WP140 | same, and `.claude/skills/release/SKILL.md` | engine-bump: name `PrefabCloneWidgetReferenceTests` as the test to read when a vanilla prefab moved; re-base the clone on the installed file and re-apply TAOM's edits, and delete a clone with no TAOM edit. Release: the packager refuses untracked TAOM files under `--require-build` (`450332b74`, rewritten for the 1.4.x skill) | `git grep -l PrefabCloneWidgetReferenceTests -- TAOM.Tests` finds a file |
| 8 | WP135, WP141 | `.claude/skills/localize/SKILL.md`, `.claude/skills/release/SKILL.md`, `.ai/routing.json`, `.ai/verification.md` | `df375d99e`'s `loc_length_report.py --since` line; "When translation is deferred, `--sync-ids --seed-only --apply` seeds the rows without the paid translate phase."; `8a4027110`'s two English-only override lines; `41aa2779b`'s release item 6a with the channel path through `docs/reference/development-machines.md`; `616cf12f1`'s `loc-orphans` rows minus the `Client/` patterns | per tool: `tools/loc_length_report.py`, `tools/check_external_loc_coverage.py`, `tools/check_orphan_loc_rows.py` exist; the seed sentence needs `--seed-only` in `tools/translate_with_claude.py` |
| 9 | WP136 | `.ai/review-reference.md`, `.claude/rules/{moduledata-validation,troops,csharp-architecture,gui-ui,adapters}.md`, `.claude/skills/release/SKILL.md` | the two `babcb8335` lessons ("Feeds a new filter or gate", "What the deserializer assembles"); `f1675c058`'s `data_gates.py` paragraph; the `8caac7da5` gate names; `/release` `DependenciesPairing_...` and the `--regenerate-shipped-troop-ids <tag>` reseed, first at v2.4.0 (D8) | `tools/data_gates.py`; `TAOM.Tests/Infrastructure/ServiceLocatorGateTests.cs`; `grep -c regenerate-shipped-troop-ids tools/validate_moduledata.py` |
| 10 | WP137 | `CLAUDE.md` routing, `AGENTS.md` | `/native-crash-triage` also for "a hang" (the routing row that starts `Native CTD:`, the end of its first cell), and the "Research first" native clause (`d51e43028`): 1.4.x AGENTS.md has no such table row, so its "Always" bullet that starts `- Research engine behavior` gains `Native behaviour: python tools/native_decompile.py --engine-method or --string (docs/features/ghidra-native-decompile.md).` | `tools/native_decompile.py` exists |
| 11 | WP138 | `docs/ai-includes/agent-teams.md`, `.claude/skills/deep-review/SKILL.md`, `tools/README.md` | the `### Budget: fan-out width and checkers` section (`4fbfb7adc`), which D5's incident paragraph joins; `099840f33`'s pointer to it in Steps 2 and 2c; rows for `harness_retro.py`, `check_public_text.py`, `improve_ctl.py`, `integrate_branch.py`, `loop_progress.py`, `worktree_audit.py`, `check_provenance.py`, `check_stale_loc_rows.py` unless WP142 added them | each tool exists; the Budget heading is absent |
| 12 | WP141 | `CLAUDE.md` Traps | the row "A tpac the Kit never saved is invisible, with no log line" (`3c5e57e71` wording, under 400 characters, gate `python tools/check_rdc_entries.py --under <folder>`) | `tools/check_rdc_entries.py` exists |
| 13 | WP122 | `.claude/rules/harmony-patches.md`, `.claude/rules/tests.md` | the caller-actor bullet of `8b9f0a233` (its v1.5.x cites re-read or dropped) and the gate paragraph of `tests.md`. E3 does not sync the "Consumed frontmatter" row, so leave `ALLOWLIST_ROW` in `tools/tests/test_claude_config_lint.py` alone | `git grep -l TranspilerSiteBindingTests -- TAOM.Tests` finds a file |
| 14 | WP131 | `.ai/review-reference.md` | drop the two deleted Patch42 targets if D2 left them | `git grep -n Patch42 -- .ai/review-reference.md` finds a hit |
| 15 | WP108 | `CLAUDE.md` Traps | the Armory trap row of `ef6afeb67`, rewritten for 1.4.x: the exact text is below this table; insert it directly below the row that starts `\| **Armory asset trees** \|`, and change that row's opening to: Desktop: no cooked tree, 0 `AssetPackages/*.tpac`. Laptop and public build: cooked, `AssetPackages/` and no `Assets/`. | `tools/audit_armory_refs.py` exists and `git grep -l Patch90 -- Main` finds a file |

Row 15's text (under 400 characters; the 1.5.x wording names `ARMORY ART DRIFT`, `/armory-audit` and a loose-only Armory, none of which holds here):

```
| **An Armory sync renames meshes; the XML keeps the old names** | A `body_name` no tpac ships spins `PreloadHelper.WaitForMeshesToBeLoaded` forever (#352, #599); `Patch90_PreloadBodyGuard` drains it and logs `[PreloadGuard]`. Nothing prompts the audit: after any Armory sync run `python tools/audit_armory_refs.py`, repoint refs, never restore a tpac. `docs/features/armory-ref-audit.md` |
```

Not taken here, report as owed (they need a maintainer decision or name no source commit): the graphify workflow wiring WP141 leaves owed
(`b8833474e`, `e6745a492`, the agent lines, the SessionStart freshness line, ADR-012, the ADR-010 amendment).

Skipped here by decision, closing WP108's hand-off (name both in your hand-off as skipped, with these reasons):
- The `/armory-audit` skill and `ef6afeb67`'s routing row: on this line it would wrap one command, `python tools/audit_armory_refs.py`, and every skill description is a permanent context cost (CLAUDE.md "Workflow → Skill convention"). The 1.5.x body also leans on server skeleton packs, troll wiring and desk paths that this line lacks, and WP108's `docs/features/armory-ref-audit.md` already says this line has no skill.
- The `session-start.sh` `ARMORY ART DRIFT` block and `ef6afeb67`'s Hook Response Contracts row: the block scans `LOTRLOME_Armory/Assets`, but the laptop's Armory and the public 1.4.8 build are cooked (`AssetPackages/`, no `Assets/`; WP108's Laptop facts), so it would print UNCHECKED every session, and `--regen-catalogue` is refused on such an install. WP108's feature doc says this line has no startup trigger.

**Verify**: doc gate exit 0; dead links not higher; `cmd` 46,000 or less; `python tools/reviewctl.py lint` exit 0; tool tests no new failure (`.ai/routing.json` is read by them); `grep -c 'An Armory sync renames meshes' CLAUDE.md` prints 1 unless your hand-off names row 15 as left out for budget or for its condition; `git grep -c 'ARMORY ART DRIFT' -- .claude/hooks/session-start.sh` finds nothing; hand-off lists each row as taken or skipped with its reason.
Commit `docs(harness): v2.0.28 - rule lines earlier work packages left`; one `Ported-from` line per 1.5.x commit whose text you took, full hash from `git rev-parse <short>`.

Part G done criteria: the commit exists (or every row was skipped and the hand-off says so); `cmd` 46,000 or less; doc gate exit 0; Build and Test as Step 0.

## Disposition table

| 1.5.x commit | Subject (short) | Disposition | Where |
|---|---|---|---|
| `709649c3f` | Animalia squash (#646) | Adapted: reflection-site rule, tooling checks 14 and 15 (already in WP138's lens files when those merged). Skipped: release pre-flight 5 (WP115), all Animalia/Elk/MonsterSize code, data, tools, the `taom_body_length` allowlist (D15) | A7 |
| `636b50abf` | new patches need no ResetForUnload | Adapted (v1.4.8 citation) | A1 |
| `5db856e05` | co-op row test (#921) | Skipped: D7 | |
| `175b149fd` | /issue search, RCA (#921) | Adapted: the step only (already in WP138's skill when that merged). Skipped: lesson and RCA (D7) | A5 |
| `c2988ac3d` | baseline five behaviors | Skipped: D7, 1.5.x-only behaviors | |
| `7c6d48628` | writers before gates (#895) | Adapted (generic lesson citation) | A2 |
| `ed3bb65cc` | cite the line that builds a total | Adapted (v1.4.8 facts inline; RCA dropped) | A2 |
| `267834a2e` | hint parent inside the bar (#877) | Ported | A3 |
| `fcd140907` | Loremaster's Line (feature) | Skipped feature; its hover lesson adapted | A3 |
| `1d88b8633` | unnamed-source exception | Ported (pick) | A4 |
| `c625ada0f` | Ered Luin (content) | Skipped content; the settlement-prefix line rebuilt from the 1.4.x census (K2 F7) | A6 |
| `2992763f6` | rules that caused harm (#763) | Adapted per B1 table; Serena, outside-PR, lens 7, plan rows left | B1 |
| `e653257a8` | trim entry docs and always-on rules | Adapted: seven named items | B2 |
| `f4b036e95` | false lines in skills (#1078) | Adapted | B3 |
| `625e8fc53` | path rules toward the cap | Adapted: re-trim modelled on it | C |
| `452796db4` | skills corrected, three agents dropped | Adapted; DLL-copy and `-Check` hunks conditional | D1 |
| `4fbfb7adc` | guides deleted, rest corrected | Adapted; plan table and 1.5.x-only docs left | D2, C |
| `683fd1331` | decisions from memory | Adapted (v1.4.8 NavigationCache citation) | D3 |
| `3ead3765e` | stale docs, catalogs | Adapted; 1.5.x content rows left | D3, D4, F6 |
| `250e1b321` | ADR amendments | Adapted, maintainer-applied (M1); lens 1 phrase conditional | D4, D1 |
| `b6012355b` | agent teams flag, retro | Adapted: settings line (M2), agent-teams text, commit-gates doc. Skipped: harness-retro record | D5, E3, F6 |
| `9cfcadd07` | dead hooks, Serena, gates | Adapted: seven hooks, gate fixes. Skipped: Serena removal (D3), config-protection ADR allowance (Authority 2), csproj comment, graphify and upstream-watch hunks | E1, E3, F5 |
| `3ea14a730` | MEMORY.md limit | Adapted: 200 lines or 25 KB (1.4.x load cap, no ADR-011) | E2 |
| `35ff005dc` | doc linter commit view | Adapted: commit view, check 11, exit 3, registry catalogue. Lens 7 hunk only with `data_gates.py`. Skipped: ADR-011 budgets, scoped-rule growth | F1, F3, F4 |
| `2129f753c` | dead memory lint in engine study | Adapted: engine dir. Skipped: `_clan_registry.json` (1.5.x clans) | F2, F3 |
| `434d1d795` | commit gates for every committer | Adapted: pre-commit, pre-merge-commit, bypass reader, feedback hook. Skipped: career kits, `.gitleaksignore`, retirement plan; `.githooks/commit-msg` already from WP103 | E1, F5, F6 |
| `b6b54ce84` | RCA of the rules review | Adapted: three to four lessons; RCA file not ported (cited by hash) | F7 |
| `30abf550b` | shared hunk, `lint_docs.py` count regex (routed here by WP108, WP130, WP137) | Skipped: superseded by `35ff005dc`, whose `MODEL_COUNT_CLAIM_RE` replaces the tuple that holds this regex (F4). If F4 is skipped, the 1.4.x regex stays as it is; say so in the hand-off | F4 |
| `ef6afeb67` | shared hunks, Armory bookkeeping (routed here by WP108) | Adapted: CLAUDE.md Armory trap row. Skipped: `/armory-audit` skill, routing row, contract row, `session-start.sh` drift block (reasons in Part G) | G |
| Other plans' hand-offs | see Part G | Not this WP's pack: single lines those WPs routed here, taken per row or reported as owed | G |

## Test plan

- New tool tests: `test_lint_rule_drift.py` (dead memory refs incl. the engine dir, rule symbols, unflagged dotnet, staged
  view), `test_githooks_pre_commit.py` (adapted, `CodeList` kept), `test_feedback_memory_hook.py`; additions to
  `test_lint_docs.py` (commit view, exit 3), `test_check_commit_msg.py` (closing lists, `EntryTests`),
  `test_shellwords.py` (bypass), `test_model_registry.py` (one catalogue). Pattern: the 1.5.x files named in each step.
- Hook suite additions: the 9cfcadd07 gate rows, the MEMORY.md rows (E2), the block-no-verify rows (F5); removals only
  for deleted hooks.
- Structurally untestable here: Claude Code reading the hook outputs live, an IDE-bundled git running `.githooks`:
  `Not-tested:` trailers and the live checks below.
- Final: tool tests `Ran` = baseline + new tests, 0 new failures; hook suite 0 new failures; Build exit 0; Test totals equal Step 0.

## Done criteria (whole plan)

- [ ] About 28 commits on `port148/143-harness-rules-sync`, every subject with the SubModule.xml version, at most 72 characters
- [ ] `git diff --name-only bannerlord-1.4.5..HEAD -- CHANGELOG.md Main Dependencies TAOM.Tests .mcp.json .serena` prints nothing
- [ ] `git log --format=%s bannerlord-1.4.5..HEAD -- .claude/settings.json docs/adrs` lists only the D4 and E3 commits
- [ ] `python tools/lint_docs.py --fail-on-drift --summary` exit 0, `rule_drift:` 0; `python tools/reviewctl.py lint` exit 0
- [ ] Tool tests and hook suite: no failure the Step 0 baseline lacked; `python tools/audit_claude_config.py --min HIGH` no new HIGH
- [ ] `git grep -n -e 'Short spears go in' -e 'NEVER use SandBox/ModuleData' -e 'the safe default' -e 'IAdapterFactory' -- .claude .ai docs/ai-includes` prints nothing
- [ ] `ls .claude/agents | grep -c -E 'debugger|error-detective|refactoring-specialist'` prints 0; the seven hooks are gone
- [ ] Build exit 0; Test 0 failed with the Step 0 total; status, commit hashes, counts and skipped hunks in the hand-off (no `plans/README.md` edit)

## STOP conditions

- A precondition in Step 0 fails (P1, P2, P3 or P5), or the SubModule.xml version is neither `v2.0.28` nor a later D8 release.
- `cmd` goes above 46,000 (or `claude_budget:` above 0) after a CLAUDE.md edit and no deletion this plan names (D1 item 2, E3 item 4, also taken early) brings it back: report, do not cut other text.
- A conflict region in a hook, `_shellwords.py`, `_pybin.sh` or `lint_docs.py` touches code (not only comments)
  and the step gives no resolution.
- The hook suite or tool tests show a failure the baseline lacked after two reasonable fix attempts.
- `check-moduledata-validation.sh`'s code list and the pre-commit list cannot be made equal (a code the validator lacks).
- Any step would need you to write `.claude/settings.json`, `.claude/settings.local.json`, `Directory.Build.props` or
  `docs/adrs/*.md` yourself, or to touch C#, `Main/` or `Dependencies/`.
- `test_model_registry.py` fails before F4 (the catalogues disagree): skip F4 and report.
- A gate denies a commit naming files you did not create, or an `ask` prompt appears that this plan did not predict.

## In-game checks owed

None: this plan changes no game code, data or asset. The maintainer's live harness checks, in a fresh session on the
merged branch:
1. `git commit -n -m "x"` in a scratch repo through the Bash tool is refused by `block-no-verify.sh`; a commit message
   that only mentions `-n` passes.
2. With `core.hooksPath` set, a terminal commit that stages a gitignored file under `.claude/hooks/` is refused by
   `.githooks/pre-commit`; a commit of a dead memory citation in `.claude/rules/` is refused with `dead-memory-ref`.
3. The session banner prints the `core.hooksPath` note when the setting is unset, and the MEMORY.md note when that file passes 200 lines.
4. Writing a new `feedback_*.md` memory card shows the "goes in the repo" context line; editing one shows nothing.
5. No startup or Stop output mentions a deleted hook, and after one failed MCP tool call the next call to that server is not refused.

## Maintainer steps

- M1 (Part D4): review and apply `$S/adrs.patch` in the worktree.
- M2 (Part E3): replace `.claude/settings.json` with `$S/settings.after.json` (or edit to match); optionally remove
  `CLAUDE_CODE_EXPERIMENTAL_AGENT_TEAMS` from your user settings too.
- M3 (Part F8): `git config core.hooksPath .githooks` once.
- Afterwards: push, merge and the WP143 tracking issue are the integrator's and the maintainer's.

## Maintenance notes

- `origin/bannerlord-1.5.x` moved 71 commits past `e45f6df2c` (to `31292c035`), touching `block-no-verify.sh`,
  `check_commit_msg.py`, `.githooks/pre-commit`, `lint_docs.py`, `test_hooks.sh`, `config-protection.sh`,
  `check-moduledata-validation.sh`, `harmony-patches.md`, `troops.md`, the `/localize` and `/release` skills and two
  `.ai` files. They are unclassified; a later sync should diff them against this plan's end state.
- `lint_docs.check_unflagged_dotnet` and `tools/tests/test_dotnet_commands_flagged.py` hold the same pattern: the first
  refuses at commit time, the second sweeps the tree in the suite. Keep the two `DIRS` lists equal, or fold the test
  into the linter later.
- The pre-commit `--code` list must follow `check-moduledata-validation.sh` (`CodeList` test). WP136-era validator codes
  arrive in the Claude gate first; copy them here in the same commit.
- 1.5.x planned to retire its Claude commit gates after a comparison around 2026-11-17; this line keeps both.
- WP140 (K1-03) adds a `PrefabHintParentGateTests` sentence that extends A3's hover paragraph in `gui-ui.md`.
- Reviewers (`/deep-review` is not mandatory for a no-C# change; offer `/security-scan` after Parts E and F) should probe:
  the config-protection prefilter still blocks every ADR and settings path (Windows and POSIX spellings), the
  pre-commit trigger regex against `check-doc-config-drift.sh`'s list, `CommitView` reading the index for a plain commit
  and the working copies for a path commit, and that no deleted hook is still named by a test or a doc.
