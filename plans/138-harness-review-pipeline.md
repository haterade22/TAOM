# Plan 138: Bring the 1.5.x review pipeline, harness measurement and model routing to the 1.4.x harness

> **Executor instructions**: Follow this plan step by step. Run every verification command and
> confirm the expected result before moving on. If anything in "STOP conditions" occurs, stop and
> report; do not improvise. The plan has four Parts (A, B, C, D). Each lands on its own, in order,
> with its own done criteria; one session per Part is fine. Part A needs the maintainer's settings
> edit M1 before commit A2. `plans/README.md` belongs to the integrator: never edit it. When done,
> report status, commit hashes and measured counts as the "Hand-off" section says.
>
> **Drift check (run first; Step A0 shows how)**:
> `git -C <wt> diff --stat 1b00d1acd..HEAD -- .claude CLAUDE.md AGENTS.md .vscode .gitattributes .codex tools/test_hooks.sh tools/lint_docs.py tools/perf_runs.py tools/data_gates.py tools/tests docs/ai-includes docs/reference docs/features/TEMPLATE.md docs/features/dev-console.md docs/features/doc-health-linter.md docs/reviews/REVIEW-GUIDE.md TAOM.Tests/Infrastructure`
> WP101 to WP137 change many of these files on purpose. Compare the live files with "Current state"
> and STOP only on a real mismatch (a Step A0 hard check that fails).

## Status

- **Priority**: P2
- **Effort**: L (four Parts, about 23 commits)
- **Risk**: MED. Harness text, hooks, tools and tests only; one new C# test file
  (`TAOM.Tests/Infrastructure/VerifyCanaryTests.cs`), no game code, no game data, no `Main/`,
  no `Dependencies/`. A broken gate fails open in silence, so every gate change lands with the
  1.5.x test rows that pinned it.
- **Depends on**: WP116 (git-gate end state), WP136 (data gates, the tool half of `fa8c06f82`),
  WP137 (native tools, taom-src); and through the wave order every WP from 101 to 135. Assumes all
  are merged into local `bannerlord-1.4.5`.
- **Category**: dx (harness)
- **Wave / lane**: wave 5, lane C
- **Executor**: laptop (one worktree; Parts run serially)
- **Decisions applied** (programme decision ids, not step names): D1 (the commit body is the changelog entry; nothing here stages
  `CHANGELOG.md`, and the /improve text that says "CHANGELOG.md is generated at /release" is now
  true on this line), D3 (Serena denies stay as WP103 set them), D7 (the co-op half of `6ce20f6ed`
  stays out), D8 (every subject carries `v2.0.28`), D10 (an ordinary session executes this file;
  the /improve pipeline is not on the programme's critical path), D19 (git through the Bash tool)
- **Planned at**: 1.4.x `origin/bannerlord-1.4.5` `1b00d1acd` and 1.5.x `origin/bannerlord-1.5.x`
  `e45f6df2c`, 2026-11-04. Source pins: `2c737d97c` (deep-review, tier agents), `ad3967222`
  (/improve), `69d2e8c0a` (harness retro); per-file blobs in the steps.
- **Issue**: Port of #621, #769, #896 (rule section only), #962, #983, #984, #985, #987, #996,
  #998, #1002, #1003, #1008, #1012, #1078. The 2026-10-08 commits carry no issue: the orchestrator
  creates one 1.4.x tracking issue for WP138 before implementation lands.

## Why this matters

1. **Reviews on this line run on a stale design.** `.claude/skills/deep-review/SKILL.md` on the
   1.4.x tip is one 567-line file that launches "5 core agents" at `effort: high` (SKILL.md:5, :10),
   with no lens files and no risk tier. The 1.5.x line split it into read-only senior lenses run by
   a pinned `deep-reviewer` agent, sized by risk, and WP116 already ported a Stop hook
   (`check-deep-review.sh`) that looks for `agent_type=deep-reviewer`, an agent this line lacks.
   Its Agent 5 item 10 also orders a HIGH finding for a missing `<DependedModule>` between TAOM
   modules (SKILL.md:380-385), which this line's own engine notes contradict
   (`docs/modding/module-dependencies.md:120-131`: in singleplayer `<DependedModules>` does not
   order XML merging).
2. **No subagent routing.** CLAUDE.md:144-154 routes Explore to Haiku. The 1.5.x line recorded
   (DOC, 2026-10-08) that Explore runs on the main model unless a spawn names one, and added a
   model and effort rule plus three tier agents. Verified this session in the Claude Code env-vars
   docs: `CLAUDE_CODE_MAX_SUBAGENT_SPAWN_DEPTH` (default 3, `1` turns nesting off) and
   `CLAUDE_CODE_EFFORT_LEVEL` (beats every other effort setting).
3. **No harness measurement.** The 1.5.x transcript miner's first run found `detect-docs-gaps.sh`
   killed by its 10 s timeout at session start (one pipeline per feature folder, 12.5 to 17.8 s at
   161 folders) and the 5 s git gates killed about 400 times in a month. The 1.4.x settings still
   register those gates at 5 s (`.claude/settings.json:35-66`). A wrapped call (`timeout 600 git ...`,
   `{ git ...; }`, a subshell) still slips past the confirm and label gates after WP116, whose
   reader finds git only as a segment's first word.
4. **Safety and truth fixes ride along**: the VS Code "Run Tests" task runs plain
   `dotnet test TAOM.Tests` (`.vscode/tasks.json:32`), which deploys into the game install on this
   line (`Main/TAOM.csproj:133-136`); the XSLT rule and `/xslt-check` send five stylesheets to
   SandBoxCore files that do not exist in the installed v1.4.8 (checked this session:
   `spkingdoms.xml`, `spclans.xml`, `lords.xml`, `heroes.xml`, `module_strings.xml` exist only under
   `Modules/SandBox/ModuleData/`); `/migration-status` describes the finished v1.2 to v1.3 migration
   (1.4.x audit finding DOCS-04, `plans/_audit/2026-06-12-harvest.md:673`).

When this lands: /deep-review runs tiered senior lenses, subagents route by model and effort,
`/harness-retro` can judge harness changes from this machine's transcripts, the Bash length gate
and the wrapper-aware git gates are live, and `/improve` can run, review and merge a plans backlog.

## Current state

### What earlier WPs leave behind (Step A0 proves each hard item)

| # | Expected in the base branch | Owner |
|---|---|---|
| H1 | `check-changelog-changed.sh` absent; AGENTS.md has a "Documentation duty" section | WP102 |
| H2 | hook commands anchored on `CLAUDE_PROJECT_DIR`; git gates in a `"Bash|PowerShell"` group; `tools/tests/test_dotnet_commands_flagged.py`; `.claude/skills/review-codex/references/prompt-fixed.md`; `lint_docs.py --drift-only`; `decision_of()` and `cft_run()` in `tools/test_hooks.sh` | WP103 |
| H3 | `.claude/hooks/{_pybin.sh,_shellwords.py,block-broad-git-add.sh,block-dangerous-git.sh,mark-verification-run.sh}` and `tools/tests/test_shellwords.py` byte-equal to their 1.5.x blobs at `88e805450`; `_pushjudge.py` present; `validate-push.sh` registered at `"timeout": 10` | WP116 |
| H4 | `tools/perf_runs.py` and `tools/tests/test_perf_runs.py` equal their `685c6a1e0` blobs; `docs/features/mission-perf-heartbeat.md` exists | WP117 |
| H5 | `tools/audit_armory_refs.py`; `RunOrDefer` in `Main/BehaviorTreeWrapper/DeferredCallbackQueue.cs`; `TAOM.Tests/Infrastructure/PatchCategoryApplierTests.cs` | WP108, WP106, WP104 |
| H6 | `_GENERIC_SECRET` and `_SECRET_COMPILED` in `tools/audit_claude_config.py` (`harness_retro.py` imports them) | 1.4.x tip :163-165, WP116 C1 |

Conditions (recorded, never a STOP; each step says what changes when one is false):
K1 `tools/validate_xml_schemas.py` exists (WP136). K2 `def check_rca_followups` in
`tools/lint_docs.py` (WP136). K3 `lint_docs.py --help` lists `--staged` (WP136). K4
`.claude/rules/moduledata-validation.md` has `## Gate per file kind` (WP136). K5
`_parse_like_engine` in `tools/validate_xml_schemas.py` (WP136). K6 `tools/native_decompile.py`
(WP137). K7 `/verify` has a "Step 2d" API snapshot check (WP137). K8
`docs/reviews/adopt-rea-2026-10-24.md` (WP137). K9 `tools/_loc_sources.py` (WP135). K10 ADR-007 has
"Protected-Virtual Boundary Seams" and K11 ADR-002 has "Interface When Needed" (WP127, maintainer
ADR edits). K12 `tools/data_gates.py` (WP136). K13 `tools/graphify_taom.py` (WP141; expected
absent: the graphify adaptations marked **[G]** apply only when K13 is false).

### Verified on `origin/bannerlord-1.4.5` (1b00d1acd) at planning

- Skills: 43 (`ls .claude/skills`); agents: 5 (`debugger`, `error-detective`, `feature-builder`,
  `refactoring-specialist`, `taleworlds-researcher`); no `deep-reviewer`, no `lenses/` folder.
  Every skill and agent frontmatter passes `c237c84e4`'s key allowlist and name check (run
  read-only this session: no failures).
- `/improve` has `SKILL.md`, `references/audit-playbook.md`, `references/closing-the-loop.md`,
  `references/plan-template.md`; no `dispatch-rules.md`, `run-protocol.md`, `execute-and-review.md`,
  `workflows/`, `tools/improve_ctl.py`, `tools/integrate_branch.py`, `tools/check_public_text.py`.
- `.claude/hooks/session-start.sh` (183 lines): source filter at :14-17 (exits unless `startup`),
  `.claude/worktrees` count at :98-109, "Recent commits" at :155-158; no shared probe deadline
  (`ss_probe_budget` does not exist, and WP103 did not port it).
- `tools/test_hooks.sh` 509 lines (3b frontmatter check at :187); `tools/lint_docs.py` 1,280 lines
  (`iter_lines_outside_code_fences` :290, `scan_text_for_dashes` :955, `_git_out` :991,
  `format_report` :1066, `main` :1195, `--dash-base` :1209).
- CLAUDE.md is 46,283 B as a blob (cap 46,000 B on disk; WP103 trims it): skill count at :57,
  `/improve` routing row at :99, `/migration-status` at :107 and :113, "Model Routing" at :144-154.
- `.claude/statusline.sh` equals the pre-image of `2473fa80e` (simulation: clean). `.vscode/tasks.json`
  equals the pre-image of `b84dbaeb2`. `.codex/config.toml`: `model = "gpt-6-astra"`,
  `model_reasoning_effort = "ultra"`. `.ai/policy.md:22-25`: "Paid dispatch authority ... when they
  explicitly ask to conduct it".
- `tools/doc_graph.py` has `default_eligible`, `load_doc_graph(eligible=...)`, `degrees`,
  `connected_components` (what `69d2e8c0a`'s ratchet needs); `tools/doc_graph_baseline.json` holds
  26 orphans, 29 components.
- `python` and `node` (v24.14.1) are on PATH; `tools/tests/test_improve_workflows.py` runs its node
  tests only when `node` is found.
- The laptop's transcripts live in `C:/Users/mikew/.claude/projects/c--Users-mikew-source-repos-TAOM`;
  `harness_retro.py` at `69d2e8c0a` hardcodes the desktop slug `DEFAULT_SLUG = "e--repos-TAOM"`
  (harness_retro.py:43).
- The 1.5.x provenance checker (`69d2e8c0a:tools/check_provenance.py`), run in memory against the
  laptop tree: 3 misses (adoption records with no register row:
  `adopt-mattpocock-improve-architecture-2026-06-22.md`, `adopt-mattpocock-teach-handoff-2026-06-22.md`,
  `adopt-ponytail-2026-06-18.md`), exit 1. It reports; it does not gate a commit.

### Pins and why

Each transplanted file is taken at the last commit of this pack that touched it, before the
2026-11-03 rules review (WP143's `2992763f6`, `452796db4`, `250e1b321`, `35ff005dc`, `f4b036e95`,
`9cfcadd07`, `4fbfb7adc`), which WP143 applies on top. Three shared hunks of those commits are
taken here on purpose and must be skipped by WP143: `2992763f6`'s first paragraph of
`model-effort-selection.md` (the maintainer reversed "main session = orchestrator" on 2026-11-02;
porting the reversed policy would ship a known-wrong instruction), and `452796db4`'s and
`4fbfb7adc`'s hunks to the harness-retro skill and doc (new files here; `69d2e8c0a` already sits on
them, checked with `git merge-base --is-ancestor`).

| Files | Source state | Later 1.5.x commits left to their owners |
|---|---|---|
| `.claude/skills/deep-review/{SKILL.md,lenses/*.md}`, `.claude/agents/deep-reviewer.md` | `2c737d97c` | `2992763f6`, `452796db4`, `250e1b321`, `35ff005dc`, `f4b036e95` (WP143); `099840f33` adapted in C2 |
| `.claude/rules/model-effort-selection.md` | `2c737d97c` plus the first paragraph of `origin/bannerlord-1.5.x` | `452796db4` (WP143) |
| `.claude/agents/architect.md`, `fast-reader.md` | `2c737d97c` | none |
| `.claude/agents/implementer.md` | `fdec324f8` (before `e6745a492`'s graphify line, WP141) | `452796db4` adds `Monitor` (WP143) |
| `.claude/skills/improve/**`, `tools/tests/test_improve_workflows.py` | `ad3967222` (test file: `a7bbf3fe7`, its last touch) | `452796db4` (WP143) |
| `tools/improve_ctl.py` + test; `tools/integrate_branch.py`; `tools/tests/test_integrate_branch.py`; `tools/check_public_text.py`; `tools/tests/test_check_public_text.py` | `ac1aa578b`; `6c4b0981b`; `d24aff340`; `086abc57b`; `0cc91b320` | none (equal to the tip) |
| `tools/harness_retro.py` + test, `.claude/skills/harness-retro/SKILL.md`, `docs/ai-includes/harness-retro.md` | `69d2e8c0a` | none |
| `tools/loop_progress.py` + test | `a7bbf3fe7` | `9cfcadd07` (WP143) |
| `tools/worktree_audit.py` + test | `88c0b6985` | none |
| `tools/perf_runs.py` + test | `ad3967222` (base must equal `685c6a1e0`, H4) | none |
| `docs/ai-includes/perf-method.md` | `ad3967222` | none |
| `.claude/hooks/check-bash-length.sh` | `6ce20f6ed` | none |
| `.claude/statusline.sh`; `tools/tests/test_statusline.py` | `dfd4cf9e0`; `2473fa80e` | none |

Hunks of other WPs' commits that ride inside the deep-review and /improve source states (the files
did not exist here, so their owners could not apply them): `6e4bfedb7`, `30b54df75` (WP102),
`bdf7d515c` (WP104), `c79a58521` (WP103/107), `332f2ac71` (WP108), `2a5363e90` (WP111), `affcee2ea`
(WP122), `9c29d7324`, `d4e6273a1`, `6a2ce8cb3` (WP127), `48d66b59a`, `c1ce78207`, `bc30b76da` (WP136),
`d51e43028` (WP137), `69b47d00b` (WP140), `b8833474e`, `e6745a492` (WP141), `62157c7e7` (WP142),
`709649c3f` (WP143), `4520af802` (WP144), `fdf9f38cb`, `88e805450` (WP116), `c8d69a0dc` (already on
1.4.x as `ccef172ad`), `03d9db8dc`, `d021426b0` (co-op client split, out). The adaptation tables
below remove what does not hold on 1.4.x; name these in the C2 and D3 commit bodies, not as trailers.

### Engine members bound (installed v1.4.8, `pwsh -NoProfile -File tools/taom-src.ps1 path <Type>`, this session)

Three places quote engine behaviour; everything else is harness text, hooks and tools.

- `.claude/rules/adapters.md` "Relations and party-screen writes" (from `f382ff6fd`):
  - `TaleWorlds.CampaignSystem.Actions.ChangeRelationAction`: `private static void ApplyInternal(Hero originalHero, Hero originalGainedRelationWith, int relationChange, bool showQuickNotification, ChangeRelationDetail detail)` (:14) calls `Campaign.Current.Models.DiplomacyModel.GetHeroesForEffectiveRelation(originalHero, originalGainedRelationWith, out var effectiveHero, out var effectiveHero2)` (:22); `public static void ApplyPlayerRelation(Hero gainedRelationWith, int relation, bool affectRelatives = true, bool showQuickNotification = true)` (:30) never reads `affectRelatives`.
  - `TaleWorlds.CampaignSystem.Party.PartyScreenData`: `public void ResetUsing(PartyScreenData partyScreenData)` (:231).
  - `TaleWorlds.CampaignSystem.Party.PartyScreenLogic`: `public bool DoneLogic(bool isForced)` (:1228); `public void Reset(bool fromCancel)` (:1525) calls `ResetLogic`, which runs `CurrentData.ResetUsing(_initialData)` (:1534).
  - `TaleWorlds.CampaignSystem.ViewModelCollection.Party.PartyVM`: `ExecuteCancel(bool showCancelInquiry = false)` (:3264) ends in `ExecuteCancelInternal` (:3253), which calls `ExecuteReset()` (:3246), which calls `PartyScreenLogic.Reset(fromCancel: false)` (:3248).
- Lens 2 (#606 example): `TaleWorlds.MountAndBlade.Mission.AfterStart()` (:3799) calls `MissionBehaviors[i].OnBehaviorInitialize()` at :3811 before `cachedSubModule2.OnMissionBehaviorInitialize(this)` at :3815 (the 1.5.x lens cites :3827 and :3831).
- The schema checker's engine reader (B7, only under K5): `TaleWorlds.ObjectSystem.XmlResource.GetMbprojxmls` (:114) reads `new StreamReader(mbprojPath)` (:121), then `XmlDocument.Load` (:123); a second loader reads `new StreamReader(path)` at :153 (v1.4.8 dump `Core/TaleWorlds.ObjectSystem/.../XmlResource.cs`; the 1.5.x tool cites :92).

### Conventions that bind this work

- **TDD** (test first, RED then GREEN) for every tool, hook and test change; copied 1.5.x tests count
  as the failing test when they are staged before the code they test.
- **No C# production code.** ADR-007 (services use adapters, never sealed TaleWorlds types), ADR-002
  (thin entry points under 150 lines), ADR-003 (no `#region`), ADR-004 (no `[Obsolete]`), ADR-005
  (no `#if DEBUG` outside IoC registration), the MCM json2 rule (rename a persisted setting, never
  flip its default) and `.claude/rules/harmony-patches.md` (MovementOrder postfixes through the
  shared deferred category) do not apply; `Main/IoC.cs`, `Main/SubModule.cs`, `Main/TAOM.csproj` are
  not touched.
- `.claude/rules/hook-authoring.md`: fail open but never silent; a timeout is a kill, so bound slow
  work inside the script; never a here-string; never spell `python3`.
- `.claude/rules/harness-facts.md` "What this rule changes": every new fact cites a doc URL (DOC) or
  an observation (EMPIRICAL); move the "Last verified" date with it.
- **Never turn a gate green by editing it**: no deleting or loosening a test row, threshold or
  allowlist. The only test edits allowed are the adaptations this plan lists.
- **Decision ids** such as "decision 55" inside copied 1.5.x text are 1.5.x plan-record ids; the
  adaptation tables say where they go.
- Prose you write (commit bodies, docs, lens text you reword): no em or en dash characters.
- **Bookkeeping stays out** (per-port procedure step 2): `CHANGELOG.md`, `docs/INDEX.md`,
  `tools/README.md`, `docs/reference/feature-map.md`, `docs/reviews/lessons/*`,
  `docs/reviews/LESSONS-LEARNED.md`, `docs/reviews/REVIEW-LOG.md`. This harness WP keeps its
  `.claude/` hunks and makes the CLAUDE.md edits each step names (as WP102, WP103 and WP116 did).

## Commands you will need

| Purpose | Command (Bash tool) | Expected |
|---|---|---|
| Restore | `cd <wt> && dotnet restore TAOM.sln` | exit 0 |
| Build | `cd <wt> && dotnet build TAOM.sln -p:DisableModuleCopy=true -p:ModuleId=` | exit 0, 0 errors (BHA0001 Patch88 warning is a known false positive) |
| Test | `cd <wt> && dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId=` | 0 failed; total = the A0 baseline, plus 1 after B6 |
| Filtered | `... --filter FullyQualifiedName~<Class>` on the Test command | 0 failed |
| Hook suite | `source <notes>/wp138.sh && hooks <name>` (background call; wait for it) | `N passed, 0 failed`, or only the A0 baseline failures |
| Tool tests | `source <notes>/wp138.sh && pyt [pattern]` | `OK`, or only the A0 baseline failures |
| Docs gate | `cd <wt> && python tools/lint_docs.py --drift-only; echo rc=$?` | `rc=0` |
| Docs report | `cd <wt> && python tools/lint_docs.py --dash-base bannerlord-1.4.5 --report <notes>/lint-<step>.md` | no new finding in a file you touched |
| Ref check | `source <notes>/wp138.sh && refs <files...>` | `refcheck: clean` |
| Security | `cd <wt> && python tools/audit_claude_config.py --min HIGH` | no HIGH or CRITICAL the A0 baseline lacks |

`<wt>` = `C:/Users/mikew/source/repos/taom-port/138`; `<notes>` =
`C:/Users/mikew/source/repos/taom-port/138-notes` (outside every repository: helpers, logs, commit
messages). Both `-p:` flags on every dotnet build and test: on this line `Main/TAOM.csproj:133-136`
does not gate CopyModule on DisableModuleCopy. Never `./build.ps1`. Never write into the game
install (`BANNERLORD_GAME_DIR`, `C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord`).

**Every Bash call in this plan**: start with `source C:/Users/mikew/source/repos/taom-port/138-notes/wp138.sh`.
A Verify command that shows a relative path (`python tools/...`, `bash .claude/hooks/...`,
`wc -c < .claude/...`, `grep ... .claude/...`, `ls .claude/...`, `python <notes>/check_m1.py .claude/settings.json`)
runs in the worktree: prefix `cd "$WT" &&`. From the main checkout it reads the wrong tree and its
result proves nothing. Pass `timeout: 600000` on every Bash call that runs the hook suite, a tool test
or a full lint: `pyt test_integrate_branch.py` alone took 135 s at planning, past the 120 s default.

### `<notes>/wp138.sh` (write with the Write tool in A0)

```bash
# In every Bash call: source C:/Users/mikew/source/repos/taom-port/138-notes/wp138.sh
WT=C:/Users/mikew/source/repos/taom-port/138
NOTES=C:/Users/mikew/source/repos/taom-port/138-notes
# gshow REF PATH: the file at REF (MSYS_NO_PATHCONV only on git reads, never for hooks)
gshow() { MSYS_NO_PATHCONV=1 git -C "$WT" show "$1:$2"; }
# blob REF PATH: overwrite the worktree file with its blob at REF
blob() { mkdir -p "$(dirname "$WT/$2")"; gshow "$1" "$2" > "$WT/$2"; }
# same REF PATH: is the base branch file byte-equal to REF's?
same() { [ "$(MSYS_NO_PATHCONV=1 git -C "$WT" rev-parse "bannerlord-1.4.5:$2" 2>/dev/null)" = "$(MSYS_NO_PATHCONV=1 git -C "$WT" rev-parse "$1:$2" 2>/dev/null)" ] && echo "SAME $2" || echo "DIFF $2"; }
# take SHA PATH...: apply that commit's hunks for PATH... with a 3-way fallback (stages the files)
take() { MSYS_NO_PATHCONV=1 git -C "$WT" show --format= "$1" -- "${@:2}" > "$NOTES/take.diff" && git -C "$WT" apply --3way "$NOTES/take.diff"; }
hooks() { (cd "$WT" && env -u MSYS_NO_PATHCONV bash tools/test_hooks.sh) > "$NOTES/hooks-$1.log" 2>&1; echo "rc=$?" >> "$NOTES/hooks-$1.log"; grep -E 'FAIL|passed, |^rc=' "$NOTES/hooks-$1.log" | tail -n 60; }
pyt() { (cd "$WT" && env -u MSYS_NO_PATHCONV python -m unittest discover -s tools/tests -t . -p "${1:-test*.py}") 2>&1 | tail -n 20; }
refs() { (cd "$WT" && python "$NOTES/refcheck.py" "$@"); }
```

### `<notes>/refcheck.py` (write with the Write tool in A0)

```python
"""WP138 reference check for transplanted instruction files. Run from the worktree root."""
import re, sys
from pathlib import Path
ROOT = Path.cwd().resolve()
TOKENS = [r"graphify", r"(?<![\w])Client/", r"TAOM\.Client", r"\bFords\b", r"fords-of-war",
          r"orientation\.md", r"ADR-01[123]\b", r"agent-budget-discipline", r"e--repos-TAOM"]
if "--with-graphify" in sys.argv:
    TOKENS.remove(r"graphify")
PATH = re.compile(r"(?<![\w/.-])((?:\.claude|\.ai|\.agents|docs|tools|Main|TAOM\.Tests)/[\w./-]+\.(?:md|py|sh|cs|ps1|json|js|txt))(?![\w-])")
# Files a tool writes on demand, legitimately absent from the tree.
ALLOW = {"docs/audits/armory-ref-audit.md"}
LINK = re.compile(r"\]\(([^)#\s]+\.md)(?:#[^)]*)?\)")
BARE = re.compile(r"(?<![\w/.-])((?:rca|adopt)-[\w.-]+\.md)")
SECT = re.compile(r"(CLAUDE|AGENTS)\.md\]?(?:\([^)]*\))?\s*\"([^\"]+)\"")
def heads(doc):
    return [l.lstrip("#").strip().lower() for l in (ROOT / doc).read_text(encoding="utf-8").splitlines() if l.startswith("#")]
def rel(base, m):
    try:
        return (base / m).resolve().relative_to(ROOT).as_posix()
    except ValueError:
        return None
bad = []
for arg in [a for a in sys.argv[1:] if not a.startswith("--")]:
    p = ROOT / arg
    for n, line in enumerate(p.read_text(encoding="utf-8").splitlines(), 1):
        bad += [f"{arg}:{n}: token {t}" for t in TOKENS if re.search(t, line)]
        cands = PATH.findall(line) + [rel(p.parent, m) for m in LINK.findall(line) if "://" not in m]
        cands += ["docs/reviews/" + m for m in BARE.findall(line)]
        for c in cands:
            if (not c or any(ch in c for ch in "<{*") or "NNN" in c or "..." in c or "/X/" in c
                    or c.startswith(".claude/logs/") or c in ALLOW):
                continue
            if not (ROOT / c).exists():
                bad.append(f"{arg}:{n}: missing {c}")
        for doc, h in SECT.findall(line):
            if not any(h.lower() in x for x in heads(doc + ".md")):
                bad.append(f"{arg}:{n}: no {doc}.md section \"{h}\"")
print("\n".join(bad) if bad else "refcheck: clean")
sys.exit(1 if bad else 0)
```

Pass `--with-graphify` when K13 is true.

## Scope

**In scope** (the only files you may change):

- `.claude/hooks/`: changed `_pybin.sh`, `_shellwords.py`, `block-broad-git-add.sh`,
  `block-dangerous-git.sh`, `mark-verification-run.sh`, `detect-docs-gaps.sh`, `session-start.sh`,
  `check-doc-config-drift.sh`; new `check-bash-length.sh`.
- `.claude/settings.json`: **maintainer only** (M1); you check, stage and commit the maintainer's edit.
- `.claude/agents/`: new `deep-reviewer.md`, `architect.md`, `fast-reader.md`, `implementer.md`;
  changed `debugger.md`, `error-detective.md`, `feature-builder.md`, `refactoring-specialist.md`,
  `taleworlds-researcher.md`.
- `.claude/rules/`: new `model-effort-selection.md`, `data-scripts.md`; changed `adapters.md`,
  `csharp-architecture.md`, `csharp-patterns.md`, `evidence-over-claims.md`, `gamemodels.md`,
  `harmony-patches.md`, `harness-facts.md`, `hook-authoring.md`, `moduledata-validation.md`,
  `output-style.md`, `provenance.md`, `simplicity-criterion.md`, `tests.md`,
  `think-before-coding.md`, `working-discipline.md`, `xslt.md`.
- `.claude/skills/`: `deep-review/**` (rewritten, new `lenses/`), `improve/**` (rewritten, delete
  `references/closing-the-loop.md`), new `harness-retro/SKILL.md`; changed `codex-verify`,
  `engine-bump`, `freeze`, `investigate`, `issue`, `localize`, `native-crash-triage`, `release`,
  `review-codex`, `ship`, `unfreeze`, `verify`, `xslt-check`; delete `migration-status/`.
- `.claude/statusline.sh`, `.codex/config.toml` (comment), `.gitattributes` (append),
  `.vscode/tasks.json`, delete `.vscode/mcp.json.example`.
- `CLAUDE.md` (only the lines each step names), `AGENTS.md` (none expected).
- `docs/ai-includes/`: `agent-operating-manual.md`, `agent-teams.md`, `code-quality.md`,
  `codex-operating-guide.md`, `completion-workflow.md`, `external-repo-adoption.md`,
  `new-culture-authoring.md`; new `harness-retro.md`, `perf-method.md`.
- `docs/reference/`: `codex-integration.md`, `hooks-catalog.md`, `mcp-servers.md`,
  `powershell-tool.md`, `provenance-register.md`, `rule-provenance.md`, `rules-catalog.md`,
  `taleworlds-api-snapshot/{README,gamemodel-bases,patch-targets}.md` (under K7 only); new
  `moduledata-validator-codes.md`.
- `docs/features/`: `TEMPLATE.md`, `dev-console.md`, `doc-health-linter.md`,
  `mission-perf-heartbeat.md` (one line and the footer), `moduledata-validation.md` (one line).
- `docs/reviews/REVIEW-GUIDE.md`; new review records listed in the steps.
- `tools/`: changed `analyze_reviews.py`, `check_doc_graph_ratchet.py`, `doc_graph_baseline.json`,
  `lint_docs.py`, `perf_runs.py`, `test_hooks.sh`, `snapshot_api_surface.ps1` (under K7 only),
  `validate_xml_schemas.py` (under K5 only); new `check_provenance.py`, `check_public_text.py`,
  `check_stale_loc_rows.py` (under K9 only), `harness_retro.py`, `improve_ctl.py`,
  `integrate_branch.py`, `loop_progress.py`, `worktree_audit.py`; their tests under `tools/tests/`.
- `tools/data_gates.py` and `tools/tests/test_validate_moduledata.py` (C8 only, under K12: WP136's hand-off).
- `TAOM.Tests/Infrastructure/VerifyCanaryTests.cs` (new).

**Out of scope**: `Main/**`, `Dependencies/**` (so the co-op half of `6ce20f6ed`), every other
`TAOM.Tests/**` file, `Directory.Build.props`, `docs/adrs/**`, ModuleData of any kind,
`.claude/settings.local.json`, `.githooks/**`, `tools/check_commit_msg.py`, `plans/README.md` (the
integrator's); the bookkeeping files
listed under Conventions; `tools/audit_mbproj_registration.py` (see the disposition of `09f3126a7`);
graphify wiring (owed, not ported; Maintenance notes); every 1.5.x review record not named in a step.

## Git workflow

- Worktree (Bash tool, from the main checkout):
  `git -C C:/Users/mikew/source/repos/TAOM worktree add C:/Users/mikew/source/repos/taom-port/138 -b port148/138-harness-review-pipeline bannerlord-1.4.5`.
  Start the Claude session in the main checkout and work in the worktree by absolute path and
  `git -C`, so the live hooks are the merged ones, never a half-edited copy.
- Git only through the Bash tool. Never stash. Never touch `stash@{0}` or the uncommitted
  `.serena/project.yml` in the main checkout.
- Never type dangerous git text on a command line, not even quoted: a force push, `--no-verify`,
  `reset --hard`, `clean -f`, `stash drop`, `switch -f`, `checkout --`. The live gates read the
  whole command and block or ask. Test payloads and messages that need such text go in files
  written with the Write tool.
- Copy blobs and apply hunks with the helpers (`blob`, `take`) or the Edit/Write tools; never
  PowerShell redirection (it changes encodings and line ends). `take` stages what it applies.
- Stage explicit paths (`git -C "$WT" add <paths>`, `git -C "$WT" rm <paths>`); never `-A`, `-u`
  or `.`. Stage a new file under `.claude/` before running the hook suite.
- Commit: write the message with the Write tool to `<notes>/msg-<step>.txt`; in the NEXT, separate
  Bash call run `git -C C:/Users/mikew/source/repos/taom-port/138 commit -F C:/Users/mikew/source/repos/taom-port/138-notes/msg-<step>.txt`.
  Never in the same command as the add, never as a parallel call, never `-C`, `-c`,
  `--amend --no-edit` or `--no-verify`. If `tools/check_commit_msg.py` exists, run it on the file
  first (it must pass).
- Message template (subject at most 72 characters; body wrapped at 72, written for 1.4.x readers,
  becomes the release note; no Co-Authored-By or "Generated with" line; no em or en dash):

```
<type>(<scope>): v2.0.28 - <description>

<What changed on the 1.4.8 line and why. For a partial port, what was
left out and why.>

Research: <what was verified this session>
Not-tested: <what needs a live session>
Ported-from: <40-hex sha>        (one line per included 1.5.x commit)
```

- The steps list Ported-from commits by 9-character prefix; write every trailer with the full
  40-hex SHA from the disposition table.
- After each commit: `git -C "$WT" log -1 --format='%s%n%(trailers:key=Ported-from,valueonly)'`
  shows the subject and the Ported-from lines. Never push, open a PR, tag or merge.
- **Merging into `tools/test_hooks.sh`, the hooks catalog or a rule**: keep every row this line's
  file has; add a commit's new rows only inside sections this file has. A new row whose section this
  line lacks (upstream-watch, graphify, ADR-011 budget) goes into a new section placed immediately
  before `head2 "Summary"` when it tests a hook this plan ports, and is otherwise named in the
  commit body as not ported.

## Maintainer step M1: `.claude/settings.json` (before commit A2)

The maintainer edits `C:\Users\mikew\source\repos\taom-port\138\.claude\settings.json` in a text
editor (config-protection blocks agents). Use the anchored command spelling WP103 chose. Leave
every other key unchanged.

```diff
 "env": {
+    "CLAUDE_CODE_MAX_SUBAGENT_SPAWN_DEPTH": "1",
+    "PYTHONIOENCODING": "utf-8",
     ...keep the existing keys...
 },
 PreToolUse, group "Bash|PowerShell": every entry registered at "timeout": 5 or 10
 (block-no-verify, validate-push, check-claude-files-tracked, check-commit-subject-version,
 block-dangerous-git, block-broad-git-add, check-native-dll-crt) becomes "timeout": 30;
 check-moduledata-validation (60) and check-doc-config-drift (30) stay.
 PreToolUse: add a group right after "Bash|PowerShell":
+      {
+        "matcher": "Bash",
+        "hooks": [
+          { "type": "command", "command": "\"$CLAUDE_PROJECT_DIR\"/.claude/hooks/check-bash-length.sh", "timeout": 30 }
+        ]
+      },
 PreToolUse, group "Edit|Write": config-protection.sh "timeout": 5 becomes 30.
 Optional (recommended; 1.5.x b84dbaeb2 found them unused): in "enabledPlugins" set
 context7, code-review, feature-dev, playwright, skill-creator and codex@openai-codex to false.
```

Why: 1.5.x raised the git gates to 30 s after its retro found them killed (failing open) about 400
times in a month (`1c622732e`); the length gate is Bash-tool only (the PowerShell tool has no such
quote wrapper); spawn depth 1 keeps a subagent from spawning its own (verified in the env-vars docs
this session); `/review-codex` and `/codex-verify` call the `codex` CLI, not the plugin.

Check after the edit (write `<notes>/check_m1.py` with the Write tool, run it on the file):

```python
import json, sys
d = json.load(open(sys.argv[1], encoding="utf-8")); bad = []
env = d.get("env", {})
bad += [f"env {k}" for k in ("CLAUDE_CODE_MAX_SUBAGENT_SPAWN_DEPTH", "PYTHONIOENCODING") if k not in env]
rows = [(ev, g.get("matcher", ""), h.get("command", "").rsplit("/", 1)[-1], h.get("timeout"))
        for ev, gs in d.get("hooks", {}).items() for g in gs for h in g.get("hooks", [])]
want = {"block-no-verify.sh", "validate-push.sh", "check-claude-files-tracked.sh", "check-commit-subject-version.sh",
        "block-dangerous-git.sh", "block-broad-git-add.sh", "check-native-dll-crt.sh"}
bad += [f"timeout {r[2]}={r[3]}" for r in rows if r[0] == "PreToolUse" and r[2] in want and r[3] != 30]
if ("PreToolUse", "Bash", "check-bash-length.sh", 30) not in rows: bad.append("check-bash-length.sh not registered")
if not any(r[2] == "config-protection.sh" and r[3] == 30 for r in rows): bad.append("config-protection timeout")
print("plugins:", d.get("enabledPlugins")); print("FAIL" if bad else "OK", bad); sys.exit(1 if bad else 0)
```

Expected: `OK []`. The plugin line is information (optional part).

## Part A: gates and measurement (6 commits)

### Step A0: worktree, prerequisites, baselines (no commit)

1. Create the worktree (Git workflow) and `<notes>`; write `wp138.sh` and `refcheck.py`.
2. Hard checks (`source <notes>/wp138.sh` first; each prints the `ok` text; any other output is a STOP):
   `test ! -e "$WT/.claude/hooks/check-changelog-changed.sh" && grep -q 'Documentation duty' "$WT/AGENTS.md" && echo H1-ok`;
   `grep -q 'Bash|PowerShell' "$WT/.claude/settings.json" && test -f "$WT/tools/tests/test_dotnet_commands_flagged.py" && test -f "$WT/.claude/skills/review-codex/references/prompt-fixed.md" && grep -q '^decision_of()' "$WT/tools/test_hooks.sh" && grep -q '^cft_run()' "$WT/tools/test_hooks.sh" && echo H2-ok`;
   `for f in .claude/hooks/_pybin.sh .claude/hooks/_shellwords.py .claude/hooks/block-broad-git-add.sh .claude/hooks/block-dangerous-git.sh .claude/hooks/mark-verification-run.sh tools/tests/test_shellwords.py; do same 88e805450 $f; done` prints six `SAME` lines (H3);
   `same 685c6a1e0 tools/perf_runs.py; same 685c6a1e0 tools/tests/test_perf_runs.py; test -f "$WT/docs/features/mission-perf-heartbeat.md" && echo H4-ok`;
   `test -f "$WT/tools/audit_armory_refs.py" && grep -q RunOrDefer "$WT/Main/BehaviorTreeWrapper/DeferredCallbackQueue.cs" && test -f "$WT/TAOM.Tests/Infrastructure/PatchCategoryApplierTests.cs" && echo H5-ok`;
   `grep -q _GENERIC_SECRET "$WT/tools/audit_claude_config.py" && grep -q _SECRET_COMPILED "$WT/tools/audit_claude_config.py" && echo H6-ok`.
3. Conditions: evaluate K1 to K13 (Current state) with `test -f` / `grep -q` and write one line per
   condition (`K1 true`) to `<notes>/conditions.txt`. Every later step reads it.
4. Baselines into `<notes>/baseline.md`: Restore, Build, Test (total, passed, skipped), `hooks a0`,
   `pyt` (failures by name), `python tools/lint_docs.py --drift-only` (rc), Security (HIGH list),
   `wc -c CLAUDE.md`.

**Verify**: H1-ok to H6-ok printed; eight `SAME` lines (six for H3, two for H4) and no `DIFF`;
baseline.md and conditions.txt written; Test 0 failed.

### Step A1: git gates read past wrapper words (`caa7ac356`)

Simulation onto the 1.4.x tip conflicted in every hook file (they were the pre-WP116 versions);
after WP116 the five hook files and `test_shellwords.py` equal `caa7ac356^` exactly
(`git diff --stat 88e805450 caa7ac356^ -- <those files>` printed nothing this session), so the
patch applies byte for byte.

1. RED: `take caa7ac356 tools/tests/test_shellwords.py`; `pyt test_shellwords.py` fails (new cases).
2. GREEN: `take caa7ac356 .claude/hooks/_pybin.sh .claude/hooks/_shellwords.py .claude/hooks/block-broad-git-add.sh .claude/hooks/block-dangerous-git.sh .claude/hooks/mark-verification-run.sh`; `pyt test_shellwords.py` prints `OK`.
3. `take caa7ac356 tools/test_hooks.sh`. Its hunks extend `MVR_CASES`, `MVR_TOOL_CASES`, `G7E_ROWS` and
   the section 7e sandbox rows that WP116 copied from `88e805450`. On a conflict keep every base row
   and add the commit's rows; never drop a row.
4. `.claude/rules/hook-authoring.md`: add the row
   ``| `timeout 60 git ...`, `{ git ...; }`, `! git ...`, `then git ...` | A wrapper word or shell grammar in front of git (#985) | NO: read segments from `_shellwords.py commands`, a parsed call through `_git_call` |``
   to the `| Form | Purpose | Handled by ... |` table under "## Git invocation forms hooks must handle"
   (1.4.x hook-authoring.md:25-39), keeping that table's three columns.
   `docs/reference/hooks-catalog.md`: `take caa7ac356 docs/reference/hooks-catalog.md`; on conflict
   keep the base rows and carry the new wording for the four hooks this commit changes.

Commit `fix(hooks): v2.0.28 - git gates read past wrapper words and grammar`. Body: confirm gates,
the label gate and the verification marker now find git past `timeout`, `env`, `nice`, `xargs`,
subshells, brace groups and loop bodies; a heredoc fed to a command is data. Ported-from
`caa7ac35669d728bdcd0498f1044e5e744558d9e`.

**Verify**: `pyt test_shellwords.py` OK; `hooks a1` no new failure; `git -C "$WT" diff --stat HEAD~1`
lists only the files above.

### Step A2: Bash length gate, faster docs-gap hook, 30 s gates (`6ce20f6ed` harness half, `ae7c4edbf`, `1c622732e`, settings of `b84dbaeb2`)

M1 must be applied first (check_m1.py prints `OK []`).

1. RED: `take 6ce20f6ed tools/test_hooks.sh` and `take ae7c4edbf tools/test_hooks.sh`. Keep the new
   sections `7l2` (detect-docs-gaps finishes inside its 10 s registration) and `7l3`
   (check-bash-length refuses only what the Bash tool would cut), the 4c prefilter rows and the
   registration and inner-bound rows; on conflict keep base rows and add these. If a hunk's context is
   a 1.5.x-only section (upstream-watch, graphify), move its rows into a new section placed
   immediately before `head2 "Summary"`. `hooks a2-red` fails on the length gate rows.
2. GREEN: `blob 6ce20f6ed .claude/hooks/check-bash-length.sh`; `take 6ce20f6ed .claude/hooks/detect-docs-gaps.sh`
   (the single-sed rewrite; on conflict keep the base's here-string-free split and take the
   one-sed `KEBABS` loop). In `.claude/hooks/session-start.sh` add `check-bash-length` to the
   degraded-banner list of python-only gates (WP102's `tools/test_hooks.sh` 5b2 requires every
   python-only gate's file name there).
3. Docs: in `hook-authoring.md` change the validate-push example's "its 10 s registration" to
   "its 30 s registration" if the sentence exists (`ae7c4edbf`); `hooks-catalog.md`: add the
   check-bash-length row and the new timeouts (`take 6ce20f6ed docs/reference/hooks-catalog.md`,
   `take ae7c4edbf docs/reference/hooks-catalog.md`, resolve by hand), and recount any registration
   total from settings.json. Drop the CLAUDE.md, lesson, co-op, BannerIcons and Dependencies hunks.
4. `.vscode/tasks.json`: `take b84dbaeb2 .vscode/tasks.json` (clean: "Run Tests" gains both flags);
   `git -C "$WT" rm .vscode/mcp.json.example` (it names the removed filesystem and git servers).

Stage the hook, tests, docs, `.vscode/tasks.json`, the deletion and `.claude/settings.json`.
Commit `fix(settings): v2.0.28 - 30 s git gates, Bash length gate, depth 1`. Ported-from
`6ce20f6edde1df897ed0032927dfcb43b35d756b`, `1c622732ecfce085fab27c56d3c7f39e3d59f2c5`,
`ae7c4edbf3f2ec958bbf50ef009ca5bca609f6fa`, `b84dbaeb2a713301908c69b8366a2a8b639e6ede`.
Not-tested: that the harness delivers check-bash-length's exit-2 reason (live check L1).

**Verify**: `hooks a2` no new failure; `python <notes>/check_m1.py .claude/settings.json` OK;
`cd "$WT" && echo '{"tool_input":{"command":"ls"}}' | bash .claude/hooks/check-bash-length.sh; echo rc=$?`
prints `rc=0`.

### Step A3: the harness retro (`c8a3ac9d9` and its series)

1. RED, the slug: copy `blob 69d2e8c0a tools/tests/test_harness_retro.py`, then add one test class
   `DefaultSlugTests` asserting `harness_retro.default_slug(PureWindowsPath("C:/Users/mikew/source/repos/TAOM"))`
   returns `"c--Users-mikew-source-repos-TAOM"` and `default_slug(PureWindowsPath("E:/repos/TAOM"))` returns
   `"e--repos-TAOM"`. `pyt test_harness_retro.py` errors (no module).
2. GREEN: `blob 69d2e8c0a tools/harness_retro.py`; replace `DEFAULT_SLUG = "e--repos-TAOM"` with
   `def default_slug(repo: Path) -> str:` returning `repo.drive[0].lower() + "--" + "-".join(repo.parts[1:])`
   (the empirical rule in harness-facts.md "Memory file" semantics), and at its use (`home = ...`,
   about line 1415) use `default_slug(repo_default.resolve())`. `pyt test_harness_retro.py` OK.
3. Skill and doc: `blob 69d2e8c0a .claude/skills/harness-retro/SKILL.md`,
   `blob 69d2e8c0a docs/ai-includes/harness-retro.md`. In the doc replace
   `CLAUDE.md "Where new knowledge goes"` with `CLAUDE.md "Documentation Requirements (MANDATORY)"`.
4. Session-start cadence: insert `c8a3ac9d9`'s "Harness retro cadence (#962)" block (30 lines,
   `gshow c8a3ac9d9 .claude/hooks/session-start.sh`, from `# Harness retro cadence` to the `fi`
   before `# Last 5 commits`) immediately before `# Last 5 commits`; in its comment drop "so it costs
   nothing of the probes' shared deadline" (this line has no shared deadline). Tests: a new section
   `head2 "7o. session-start.sh prints the harness retro cadence line"` before `head2 "Summary"`
   holding `c8a3ac9d9`'s five cadence cases, with its own fixture instead of 1.5.x's upstream-watch
   one: `RC="$SANDBOX/retro"; mkdir -p "$RC/.claude/hooks"; cp .claude/hooks/session-start.sh .claude/hooks/_pybin.sh "$RC/.claude/hooks/"`,
   a stub interpreter `RCPY="$RC/fakepy"` built as section 4c's FAKEPY stub (it answers the
   `_pybin.sh` probe), and `ss_retro` calling `env CLAUDE_PROJECT_DIR="$RC" TAOM_PYBIN="$RCPY"`.
5. Records (bytes as stored): `blob ae7c4edbf docs/reviews/harness-retro-2026-10-21.md` (the hook
   comments cite it; it is 1.5.x history, so the banner reads DUE until a 1.4.x retro exists),
   `blob c8a3ac9d9 docs/reviews/adopt-harness2-2026-10-21.md`, `blob 78fe34c16 docs/reviews/adopt-rtk-2026-10-23.md`,
   `blob b6f0ec794 docs/reviews/adopt-paperclip-2026-10-26.md`.
6. `docs/reference/provenance-register.md`: add the register rows and detail sections for
   harness2 (`c8a3ac9d9`), rtk (`549bf3902`) and Paperclip (`b6f0ec794`) inside the
   `provenance-register-start/end` markers. `docs/ai-includes/external-repo-adoption.md`: add the
   three bullets from the same commits. `hooks-catalog.md`: the session-start row text of
   `c8a3ac9d9`. CLAUDE.md: add the routing row
   `| HARNESS RETRO DUE banner; "did that rule help" | **`/harness-retro`** | |` to "Strong
   proactive-invoke triggers" and make :57 read `42 of the 44`.
7. Drop: the `.gitleaksignore` hunk of `4358d1622`, every `tools/README.md`, INDEX and lessons hunk.

Commit `feat(harness): v2.0.28 - harness retro measures harness changes`. Body: the miner, the
skill, the seven-day banner, the slug derived per machine. Ported-from: `c8a3ac9d9`, `c64c78e72`,
`78fe34c16`, `0985c9515`, `c237c84e4`, `d94bddecb`, `ad3967222`, `4358d1622`, `3f53e4623`,
`69d2e8c0a`, `549bf3902`, `b6f0ec794`, `a87bdc3a9`, `452796db4`, `4fbfb7adc`.

**Verify**: `pyt test_harness_retro.py` OK; `hooks a3` no new failure;
`cd "$WT" && python tools/harness_retro.py --transcripts "C:/Users/mikew/.claude/projects/c--Users-mikew-source-repos-TAOM" --since 2026-11-01 --out "$NOTES/retro"; echo rc=$?`
prints rc 0 (or 3 with `UNCHECKED: no transcript records`, which is a pass on a quiet machine;
the worktree's own slug has no transcripts, hence the explicit folder; rc 2 means a bad argument);
`refs .claude/skills/harness-retro/SKILL.md docs/ai-includes/harness-retro.md` clean;
`python tools/lint_docs.py --drift-only` rc=0.

### Step A4: review-yield and provenance tools (`69d2e8c0a`, non-retro half)

1. RED: `blob 69d2e8c0a tools/tests/test_analyze_reviews.py`, `blob 69d2e8c0a tools/tests/test_check_provenance.py`,
   `blob 69d2e8c0a tools/tests/test_doc_graph_ratchet.py`, and under K9 `blob 69d2e8c0a tools/tests/test_check_stale_loc_rows.py`;
   `pyt test_analyze_reviews.py`, `pyt test_check_provenance.py`, `pyt test_doc_graph_ratchet.py`
   (and under K9 `pyt test_check_stale_loc_rows.py`) each fail or error. Adapt one copied test,
   `LiveLogTests.test_the_real_log_parses_the_at_merge_rows` in `test_analyze_reviews.py`: it reads this
   line's own `docs/reviews/REVIEW-LOG.md`, which has 0 "(at merge)" rows and parses to 59 reviews at
   `1b00d1acd` (1.5.x: 84 such rows; WP142 carries no log rows). Rename it `test_the_real_log_parses`,
   delete its `(at merge)` assertion, and change `assertGreater(len(reviews), 80)` to
   `assertGreater(len(reviews), 50)`. Say so in the commit body. Unadapted, it fails after GREEN (it was
   the one failure among these three test files when run against the 1.4.x tip at planning).
2. GREEN: `take 69d2e8c0a tools/analyze_reviews.py tools/check_doc_graph_ratchet.py` (both were
   clean in the simulation), `blob 69d2e8c0a tools/check_provenance.py`, and under K9
   `blob 69d2e8c0a tools/check_stale_loc_rows.py`. Re-measure the ratchet baseline: run
   `python tools/check_doc_graph_ratchet.py --update` and keep its numbers (never 1.5.x's 27/32);
   say the measured numbers in the commit body.
3. `/verify` (`.claude/skills/verify/SKILL.md`): add Step 5b "Gates silenced" and Step 5c
   "Provenance" with their output rows (`Provenance: [N misses]`) from `69d2e8c0a`; add Step 2e
   "Data gates" only under K12; skip Step 7 (`lint_docs --followups` is `35ff005dc`, WP143).
   Under K9 add `/localize` "Case E: changing English that is already translated" exactly as
   `f1675c058` words it (the paragraph only, not its seed-only edits; Ported-from `f1675c0581a472fcc72fb3c8da7f1566912255fe`).
4. `.claude/rules/provenance.md` "Enforcement" and the register's intro paragraphs: take
   `69d2e8c0a`'s text (the checker now exists). Under K7 only: its snapshot README paragraphs and
   the two header strings in `tools/snapshot_api_surface.ps1`, then edit the matching header line
   in `gamemodel-bases.md` and `patch-targets.md` by hand. Drop the REVIEW-LOG.md hunk.

Commit `feat(tools): v2.0.28 - yield by lens, provenance and stale loc checks`. Ported-from
`69d2e8c0a27ac3b061df1549065b1e68460ab437` (and `f1675c058` under K9).

**Verify**: `pyt` no new failure; `python tools/check_provenance.py; echo rc=$?` lists the misses
and exits 1 (3 at planning; the number goes in the body); `python tools/check_doc_graph_ratchet.py`
prints `PASS`; under K7 `pwsh tools/snapshot_api_surface.ps1 -Check` reproduces.

### Step A5: status line usage windows (`2473fa80e`, `dfd4cf9e0`, `49bf3b115`)

1. `git -C "$WT" cherry-pick -n 2473fa80e` (clean in the simulation: statusline plus
   `tools/tests/test_statusline.py`); `pyt test_statusline.py` OK. Then `take dfd4cf9e0 .claude/statusline.sh`.
2. `blob dfd4cf9e0 docs/reviews/adopt-claude-hud-2026-10-26.md`; add the claude-hud register row
   and detail (`49bf3b115`) and the adoption bullet; add `dfd4cf9e0`'s harness-facts row
   (statusLine renders only in the terminal CLI; the VS Code panel shows `/usage`) to a new
   `## Tool and session behaviour` table in `harness-facts.md` (C4 adds more rows there).

Commit `feat(harness): v2.0.28 - status line shows the usage windows`. Ported-from
`2473fa80edfa4bba8beb3bef48999b443cb3e3ea`, `dfd4cf9e0a8f0850b5e8547f3549646d08732b8d`,
`49bf3b11518082cbcd43e92e636bbaa3301c1517`.

**Verify**: `pyt test_statusline.py` OK; `hooks a5` no new failure.

### Step A6: the Headroom record (`6a54436c6`)

`blob 6a54436c6 docs/reviews/adopt-headroom-2026-11-03.md`; add its register row and detail section
and the external-repo-adoption bullet. Commit `docs(reviews): v2.0.28 - measure Headroom, do not install it`.
Ported-from `6a54436c6a1b421f5d89c5483c1b8b3c90fc2243`.

**Verify**: `python tools/check_provenance.py` lists no new miss for `adopt-headroom`.

### Part A done criteria

- [ ] Six commits; `hooks` and `pyt` no failure beyond the A0 baseline; Test count unchanged.
- [ ] `check_m1.py` OK; `cd "$WT" && printf '{"source":"startup"}' | CLAUDE_PROJECT_DIR="$WT" bash .claude/hooks/session-start.sh | grep 'HARNESS RETRO DUE'`
      prints `HARNESS RETRO DUE: last retro 2026-10-21 (N days ago); run /harness-retro.`
- [ ] `python tools/lint_docs.py --drift-only` rc=0; Security no new HIGH.

## Part B: process rules, doc gates, review checks (7 commits)

### Step B1: the public-text gate and the /issue body file (`5e5877bdf`, `086abc57b`, `0cc91b320`, `9e9f04df4` issue hunk, `4407fbc05` issue hunk)

1. RED: `blob 0cc91b320 tools/tests/test_check_public_text.py`; `pyt test_check_public_text.py` errors.
2. GREEN: `blob 086abc57b tools/check_public_text.py` (it imports `lint_docs.scan_text_for_dashes`,
   present at lint_docs.py:955). OK.
3. `take 9e9f04df4 .claude/skills/issue/SKILL.md` (all 38 removed lines exist on 1.4.x, checked this
   session), then `take 4407fbc05 .claude/skills/issue/SKILL.md` (Analysis marks observed,
   source-derived or hypothesis; Testing lists only checks that ran).

Commit `feat(tools): v2.0.28 - check public text before it is posted`. Ported-from
`5e5877bdf`, `086abc57b`, `0cc91b320`, `9e9f04df4`, `4407fbc05`.

**Verify**: `pyt test_check_public_text.py` OK (the tool imports `INLINE_CODE_RE` and
`scan_text_for_dashes` from `lint_docs.py`, both present on this line at :82 and :955); a file
written with the Write tool holding one plain sentence gives
`python tools/check_public_text.py <file>; echo rc=$?` `rc=0`, and the same file with a local
drive path added gives `rc=1` with a `local-path` line.

### Step B2: ralph rules enforce themselves (`4407fbc05`, `4d17aeab8`, `a7bbf3fe7`, `a87bdc3a9` rule line)

The 1.5.x rule files were rewritten after the fork, so none of these hunks applies (0 of their
removed lines exist on 1.4.x); insert the final wording at the matching place:

| 1.4.x file | Insert (source of the final text) |
|---|---|
| `evidence-over-claims.md` | §B, after the step that states the claim: `4407fbc05`'s "Name what the run did not cover (no in-game check, one build, the laptop untested) beside the claim."; §C traps: item 4 "Reporting an empty search as absence" (`4407fbc05`) |
| `simplicity-criterion.md` | the table row "A guard for a speculative race or security case" (`4407fbc05`) |
| `working-discipline.md` | "Autonomous-loop stewardship": the no-progress breaker bullet as `a7bbf3fe7` words it; "Edit scope discipline": the "Fix a bad value where it is produced..." paragraph (`4407fbc05`); and in the "NEVER STOP to ask permission" bullet replace the clause that begins `think harder` and ends `before stopping` with "first re-read the transcript for missed angles and recombine prior near-misses before stopping" (`9e9f04df4`; this also removes the two dashes that clause holds) |
| `hook-authoring.md` | the section "Digits only into bash arithmetic (ported from ralph-claude-code, #996)" as `a7bbf3fe7` words it, before "Never spell it `python3`" |
| `think-before-coding.md` | the "Conventions documented in CLAUDE.md / ADRs / scoped rules" bullet becomes: "**Conventions documented in CLAUDE.md, ADRs or scoped rules, and facts a quick run can show:** apply them or run the check; don't re-litigate. *"Should I use an adapter for this sealed type?"* is not a question: ADR-007 already answers it." (`a87bdc3a9`) |

Then: RED `blob a7bbf3fe7 tools/tests/test_loop_progress.py`; GREEN `blob a7bbf3fe7 tools/loop_progress.py`.
Session-start: insert `a7bbf3fe7`'s compact block (`if [[ "$SOURCE" == compact ]]; then ... exit 0; fi`)
between the source parse and the "Only run on fresh startup" test (1.4.x :14), update line 3's
comment as `a7bbf3fe7` does, and drop "CLAUDE.md item 8" from its comment (this CLAUDE.md has no
such item). Tests: `take a7bbf3fe7 tools/test_hooks.sh` for sections `4f` and `7n`; section 4f
enumerates `.claude/hooks/*.sh` itself, so its 1.5.x count of 26 becomes whatever this line has
(never edit the hooks to fit). `docs/ai-includes/codex-operating-guide.md`: `take a7bbf3fe7` (clean
line). `blob 4d17aeab8 docs/reviews/adopt-ralph-2026-10-24.md`; the ralph adoption bullet;
hooks-catalog session-start text. Drop CLAUDE.md item 8, perf_runs (B3), execute-and-review and
run-protocol (D3), execute.js and its tests (D2).

Commit `feat(tools): v2.0.28 - ported ralph rules enforce themselves`. Ported-from `4407fbc05`,
`4d17aeab8`, `a7bbf3fe7`, `a87bdc3a9`, `9e9f04df4`.

**Verify**: `pyt test_loop_progress.py` OK; `hooks b2` no new failure (4f's control row passes);
`printf '{"source":"compact"}' | bash .claude/hooks/session-start.sh; echo rc=$?` rc=0.

### Step B3: pstack checks the workflow runs (`2fb9d914a`, `88c0b6985`, `28de0dc7d`, `21f2ba79d`, `5eacdedc1`, `4407fbc05` and `ad3967222` perf_runs)

1. RED: `blob 88c0b6985 tools/tests/test_worktree_audit.py`; GREEN `blob 88c0b6985 tools/worktree_audit.py`.
2. Session-start worktree block (`4407fbc05` with `5eacdedc1`'s `WT_MAX=15`), placed before the
   `.claude/worktrees` block. This line has no shared probe deadline: drop the `ss_probe_budget`
   branch and run `WT_LIST=$(timeout -k 0.5 2 git worktree list --porcelain 2>/dev/null)`; keep the
   `exited $WT_RC` UNCHECKED note. Change the `.claude/worktrees` message as `88c0b6985` does
   ("Run python tools/worktree_audit.py; prune only on Mike's word."). Tests: the worktree rows of
   `4407fbc05` and `88c0b6985` (`take ... tools/test_hooks.sh`; rows that rely on `ss_probe_budget`
   are adapted to the 2 s timeout, never deleted).
3. perf_runs: `blob ad3967222 tools/tests/test_perf_runs.py` (RED), `blob ad3967222 tools/perf_runs.py`
   (GREEN; H4 proved the base is `685c6a1e0`, and only `4407fbc05` and `ad3967222` touch it after).
4. Docs: `blob ad3967222 docs/ai-includes/perf-method.md`; `take 2fb9d914a` for
   `.claude/rules/output-style.md` (insert the "More tells" paragraph by hand if it does not apply),
   `.claude/skills/investigate/SKILL.md`, `.claude/skills/ship/SKILL.md`,
   `docs/ai-includes/code-quality.md`, `docs/ai-includes/completion-workflow.md`,
   `docs/features/TEMPLATE.md`, `docs/features/dev-console.md`, `docs/features/mission-perf-heartbeat.md`;
   `take 28de0dc7d docs/features/mission-perf-heartbeat.md`. Records: `blob a87bdc3a9 docs/reviews/adopt-pstack-2026-10-24.md`;
   under K8 `take 21f2ba79d docs/reviews/adopt-rea-2026-10-24.md docs/reference/provenance-register.md`.
   The pstack adoption bullet. Leave `2fb9d914a`'s `tests.md` line for C2 (it cites a lens).

Commit `docs(harness): v2.0.28 - pstack checks the workflow runs`. Ported-from `2fb9d914a`,
`88c0b6985`, `28de0dc7d`, `21f2ba79d`, `5eacdedc1`, `4407fbc05`, `ad3967222`.

**Verify**: `pyt test_worktree_audit.py` and `pyt test_perf_runs.py` OK;
`cd "$WT" && python tools/worktree_audit.py; echo rc=$?` lists the main checkout and the
taom-port worktrees and changes nothing (`git -C C:/Users/mikew/source/repos/TAOM status --porcelain` unchanged); `hooks b3` no new failure.

### Step B4: doc lint gates (`4407fbc05`, `88c0b6985`, `bc7a7f0d9`, `09f3126a7`, `ad3967222` lint halves)

The 1.4.x `lint_docs.py` never took the 1.5.x refactors (the 4407fbc05 hunk sits on `--staged`,
`check_rca_followups` and `check_context_budget`), so port by hand, tests first:

1. RED: add to `tools/tests/test_lint_docs.py` the test classes of the five commits for these
   functions only (copy each class from `gshow <sha> tools/tests/test_lint_docs.py`): the
   new-feature-doc verify section (`4407fbc05`, `88c0b6985`: body required, fenced heading ignored,
   `--also-new`), the merge-parent case (`bc7a7f0d9`), the closing-fence length and the
   `git-diff-failed` finding plus `--only-paths` (`09f3126a7`), `_dash_base_resolves` and exit 2
   (`ad3967222`). Drop test cases that need `check_context_budget` or ADR-011.
2. GREEN in `tools/lint_docs.py`: the `feature_verify` report field and total;
   `VERIFY_HEADING`, `_FEATURE_DOC_SKIP`, `_FEATURE_DOC_PATHSPEC`, `_has_verify_section`,
   `check_feature_doc_verify_section(base, staged, also_new)` at their `88c0b6985` state with
   `bc7a7f0d9`'s merge-parent fix; its report section and summary counter; call it in the full run
   and in `--drift-only` with `args.dash_base`, `staged=args.staged`, `also_new=args.also_new`;
   add `feature_verify` to the exit-1 condition. Under not-K3 add
   `--staged` (help: "Read the new-feature-doc check from the index, not the working tree: what a
   commit records.") and `--also-new` (`88c0b6985`'s help). From `09f3126a7`: the closing-fence rule in
   `iter_lines_outside_code_fences`, the `git-diff-failed` finding with its counter, `--only-paths`;
   from `ad3967222`: `_dash_base_resolves` and exit 2 on an unresolvable `--dash-base`.
3. `docs/features/doc-health-linter.md`: the flag and check lines of these commits.
4. `tools/tests/test_audit_repo_secrets.py`: add the `audit-allow:` marker `bc7a7f0d9` adds at the
   deliberate password literal (1.4.x :178 carries the same literal; the auditor honours the marker).

Commit `fix(tools): v2.0.28 - doc lint gates new feature docs and dash bases`. Ported-from
`4407fbc05`, `88c0b6985`, `bc7a7f0d9`, `09f3126a7`, `ad3967222`.

**Verify**: `pyt test_lint_docs.py` OK; `python tools/lint_docs.py --drift-only; echo rc=$?` rc=0;
`python tools/lint_docs.py --dash-base no-such-ref --summary; echo rc=$?` rc=2;
`python tools/audit_claude_config.py --min HIGH` no new HIGH.

### Step B5: the drift gate judges what a commit records (`88c0b6985`, `09f3126a7` hook halves)

1. RED: `take 09f3126a7 tools/tests/test_shellwords.py` (the `commit-plan` cases); `pyt test_shellwords.py` fails.
2. GREEN: `take 09f3126a7 .claude/hooks/_shellwords.py` (the `commit-plan` mode; it sits on A1's state,
   and no commit touched the file between `caa7ac356` and `09f3126a7`).
3. `check-doc-config-drift.sh` (WP103's `--drift-only` form): add `88c0b6985`'s `ALSO_NEW` block after
   the relevance loop, and `09f3126a7`'s commit-plan reading (`AMEND`, `COMMIT_PATHS`, the
   `LINT_ARGS` array with `--staged`, `--dash-base HEAD~1` on an amend, `--only-paths`, the
   `--also-new` items), its JSON-encoded ask and deny texts and the `FIX` wording. Keep the base's
   relevance list, `taom_git_repo_dir` lines and `hookSpecificOutput` contract. Add the new section
   heading "Feature docs with no in-game verification section" to the awk that extracts gating
   sections. Tests: `take 88c0b6985 tools/test_hooks.sh`, `take 09f3126a7 tools/test_hooks.sh` and
   `ad3967222`'s drift amend rows; adapt only their expected flag strings to this hook's arguments.

Commit `fix(hooks): v2.0.28 - drift gate judges what the commit records`. Ported-from `88c0b6985`,
`09f3126a7`, `ad3967222`.

**Verify**: `pyt test_shellwords.py` OK; `hooks b5` no new failure; then a live probe: write
`docs/features/x.md` (one line, no verify heading) in the worktree and `<notes>/drift-probe.json`
holding `{"tool_name":"Bash","cwd":"C:/Users/mikew/source/repos/taom-port/138","tool_input":{"command":"git add docs/features/x.md && git commit -m m"}}`
(both with the Write tool); `cd "$WT" && CLAUDE_PROJECT_DIR="$WT" bash .claude/hooks/check-doc-config-drift.sh < "$NOTES/drift-probe.json"`
prints a deny whose reason names `no-verify-section`; delete `docs/features/x.md` afterwards
(`rm`, it was never staged).

### Step B6: frontmatter allowlist and a verify canary (`c237c84e4`, `ad3967222` 3b)

1. RED (hook suite): add `SKILL_KEYS`, `AGENT_KEYS` and the allowlist and name checks to section 3b
   (`c237c84e4`'s hunk; the 1.4.x loop is `for p in list(...) + list(...)`, so insert by hand), then
   `ad3967222`'s planted-tree and zero-files tests for 3b.
2. C#: `blob c237c84e4 TAOM.Tests/Infrastructure/VerifyCanaryTests.cs` (file-scoped namespace; C# 10
   is set in `Directory.Build.props:5`).
3. `/verify`: Step 2c "Canary" (`c237c84e4`, both flags as given), and after Step 2 "A total under
   7,000 (the WP101 baseline was 9,154) means discovery broke: not a pass." (1.5.x's 20,000 is its own
   suite). `harness-facts.md`: replace the "Frontmatter fields documented as consumed" row with
   `c237c84e4`'s skill allowlist row and add its agent allowlist row.
4. `blob a87bdc3a9 docs/reviews/adopt-claude-plugins-official-2026-10-26.md`; its adoption bullet.

Commit `feat(tools): v2.0.28 - frontmatter allowlist and a verify canary`. Ported-from `c237c84e4`, `ad3967222`.

**Verify**: `hooks b6` no new failure (3b reports its COUNT and no BAD line);
Build; `cd "$WT" && TAOM_VERIFY_CANARY=1 dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId= --filter FullyQualifiedName~VerifyCanaryTests 2>&1 | grep -c VERIFY_CANARY_TRIPPED`
prints at least 1 and the run fails; without the variable the full Test run passes with A0 + 1.

### Step B7: the schema check reads project.mbproj like the engine (`09f3126a7`, under K5 only)

Under K5: `take 09f3126a7 tools/tests/test_validate_xml_schemas.py` (the `EngineReaderTests` class;
RED), then the `_parse_like_engine` hunk of `tools/validate_xml_schemas.py` (GREEN); re-cite its
docstring to `XmlResource.cs:121` (v1.4.8, `new StreamReader(mbprojPath)`). Commit
`fix(tools): v2.0.28 - schema check reads project.mbproj like the engine`. Ported-from
`09f3126a7`. Not K5: skip the step and say so in the report.

**Verify**: `pyt test_validate_xml_schemas.py` OK.

### Part B done criteria

- [ ] Six or seven commits; `hooks`, `pyt` no failure beyond A0; Test = A0 total + 1.
- [ ] `python tools/lint_docs.py --drift-only` rc=0; Security no new HIGH.

## Part C: the review pipeline (8 commits)

### Step C1: route subagents by model and effort (`fdec324f8`, `d0e4bc947`, shared `2992763f6`)

1. `blob 2c737d97c .claude/rules/model-effort-selection.md`, then replace its first paragraph with
   the first paragraph of `gshow origin/bannerlord-1.5.x .claude/rules/model-effort-selection.md`
   ("**The main session does most of the work** (Mike, 2026-11-02). ...").
2. `blob 2c737d97c .claude/agents/architect.md`, `blob 2c737d97c .claude/agents/fast-reader.md`,
   `blob fdec324f8 .claude/agents/implementer.md`.
3. Tiers (`d0e4bc947`): `debugger` and `feature-builder` add `effort: high`; `refactoring-specialist`
   adds `effort: medium`; `error-detective` adds `model: opus` and `effort: high`;
   `taleworlds-researcher` changes `model: sonnet` to `model: opus` and adds `effort: high`.
4. CLAUDE.md: replace "## Model Routing" (heading through the Haiku deprecation note, :144-154 on the
   tip) with "## Model & Effort Selection" and `fdec324f8`'s three-line pointer (the "Custom Agents"
   line changes in C2, once `deep-reviewer` exists). `rules-catalog.md`: the
   always-load row for the new rule (count 8). `rule-provenance.md`: `fdec324f8`'s section, with its
   budget sentence reworded for this line (46,000 B cap) and its precedence sentence replaced by
   "the Agent tool's effort beats a definition's; `CLAUDE_CODE_EFFORT_LEVEL` beats both (DOC,
   2026-11-02; harness-facts.md)".

Commit `docs(ai): v2.0.28 - route subagents by model and effort tier`. Ported-from `fdec324f8`,
`d0e4bc947`, `b84dbaeb2`, `9e9f04df4`, `2c737d97c`, `2992763f6c6f777de1d62e4ca4f8a0b83f165aa5`.

**Verify**: `hooks c1` (3b) no new failure; `python tools/lint_docs.py --drift-only` rc=0;
`refs .claude/rules/model-effort-selection.md .claude/agents/architect.md .claude/agents/fast-reader.md .claude/agents/implementer.md` clean.

### Step C2: /deep-review senior lenses and risk tiers (shared `fa8c06f82` lens half, then `0a8d61a4e` to `2c737d97c`, `099840f33` adapted)

1. Overwrite with the source blobs (the old monolithic SKILL.md is replaced, not deleted):
   `blob 2c737d97c .claude/skills/deep-review/SKILL.md`, `blob 2c737d97c .claude/agents/deep-reviewer.md`,
   and each of `1-standards 2-engine-compat 3-efficiency 4-completeness 5-data-flow 6-design 7-xml adversarial tooling`
   as `blob 2c737d97c .claude/skills/deep-review/lenses/<name>.md`.
2. Before adapting, list what earlier WPs changed in the old file:
   `git -C "$WT" log --oneline 1b00d1acd..HEAD -- .claude/skills/deep-review/SKILL.md`. For each such
   commit grep one changed phrase in the new files; if a rule it added is missing from them, STOP.
3. Apply this adaptation table (line numbers at `2c737d97c`):

| File:line | Change |
|---|---|
| SKILL.md:22 | Codex trigger: "the Step 2 risk tier is 1 or 2 and the maintainer has asked for paid dispatch for this work (`.ai/policy.md` "Paid dispatch authority"; CLAUDE.md "Never auto-invoke"), or `$ARGUMENTS` contains `--codex` (strip it from the feature name). Without that, a tier 1 or 2 review reports `CODEX: NOT RUN (no paid-dispatch request)`." Delete the B3 and "2026-10-22" sentences |
| SKILL.md:56, :145 **[G]** | The paragraph becomes: "**Map the blast radius (mandatory when C# or a script is in scope).** For every public type, interface or member the change adds, renames, re-signs or alters in behaviour, list its callers with `git grep -n "<Type>" -- Main TAOM.Tests`; each caller not in the changed-file list goes to Agents 4 and 5 in SCOPE NOTES. An XML-only change skips this step." Report row: `Blast radius: [N types checked with git grep, M callers outside the diff / NOT IN SCOPE (XML only)]` |
| SKILL.md:95, :115 | delete "memory `agent-budget-discipline`; " and "(memory `agent-budget-discipline`)" (`099840f33` points them at agent-teams.md "Budget", a section WP143's `4fbfb7adc` adds) |
| SKILL.md:124 | end the bullet after "per-file lenses cannot see this"; delete the Fords of War parenthetical |
| SKILL.md:217 | not K2: delete from "; `python tools/lint_docs.py --fail-on-drift` fails" to "the staged RCA" |
| SKILL.md:231 | not K4: `"Gate per file kind"` becomes `"Validate ModuleData cross-references"` |
| lenses/1-standards.md:8 | not K10: delete the "and a `protected virtual` member ... (part of the seam body, decision 55)" clauses and the "Flag a seam ..." sentence |
| lenses/1-standards.md:13 | not K11: "Every adapter has an interface (ADR-007)." replaces the item |
| lenses/1-standards.md:36 | H5 becomes: "**CLAUDE.md budget**: run `python tools/lint_docs.py --drift-only` and quote its CLAUDE.md budget line; report growth the change added." |
| lenses/2-engine-compat.md:19 | ":3827 vs :3831" becomes ":3811 vs :3815" (v1.4.8 Mission.cs, Current state) |
| lenses/2-engine-compat.md:20 | delete the sentence from "`Mission.AddCustomMissile`" to "D6)" |
| lenses/2-engine-compat.md:22 | delete "(MonsterSize's three sites shipped without, #646)" |
| lenses/2-engine-compat.md:25 | not K6: replace the native_decompile sentence with "No native decompiler is on this line, so a native claim is UNVERIFIED unless the dump or a debugger shows it." |
| lenses/4-completeness.md:16 | delete "(`rca-fords-of-war-2026-10-31.md` addendum)" |
| lenses/5-data-flow.md:95 | "under `Main/` or `Client/`" becomes "under `Main/`" |
| lenses/5-data-flow.md:97 | "(such as the Patch61 and Patch89 sub-categories or the character-preview batch)" becomes "(such as the Patch61 sub-categories)" |
| lenses/7-xml.md:19 | not K1: replace the validate_xml_schemas sentence with "There is no engine-XSD gate on this line yet: say so as NOT RUN." |
| lenses/7-xml.md:46 | "cross-module refs need a <DependedModule> (Agent 5 item 10 owns that trace)" becomes "cross-module refs are Agent 5 item 10's trace" (the lens item 10 now says a missing `<DependedModule>` between TAOM modules is not a finding, which matches `docs/modding/module-dependencies.md:120-131`) |
| lenses/7-xml.md:47 | "the trap index in `docs/ai-includes/orientation.md`" becomes "the Traps table in CLAUDE.md" |
| deep-reviewer.md:32-34 | not K1: drop `validate_xml_schemas.py, `; always: "rewrites the tracked `docs/audits/armory-ref-audit.md`" becomes "writes `docs/audits/armory-ref-audit.md`" (WP108 dropped the 1.5.x report, so it is not tracked here) |

4. With the lenses in place: add `2fb9d914a`'s line to `.claude/rules/tests.md`; replace the
   /deep-review item in `.claude/skills/ship/SKILL.md` Phase 1 with `9e9f04df4`'s wording
   (`gshow 9e9f04df4 .claude/skills/ship/SKILL.md`, Phase 1 item 2: "senior `deep-reviewer` agents,
   one per lens, in waves of four ...").
5. `docs/ai-includes/agent-operating-manual.md` section 1: add item 6 as `9a2a8dae9` words it
   (compute across files; a `paths:` rule loads by itself, so read it only if it has not arrived)
   after this line's item 5. `blob 9a2a8dae9 docs/reviews/adopt-context-mode-2026-10-26.md`; the
   context-mode register row and detail (`9d327f43e`); its adoption bullet.
6. CLAUDE.md "Custom Agents": name nine agents (the five specialists, the tiers `fast-reader`,
   `implementer`, `architect` with a pointer to `.claude/rules/model-effort-selection.md`, and
   `deep-reviewer` for `/deep-review` lenses), removing the dash in the line you rewrite.

Commit `chore(harness): v2.0.28 - /deep-review senior lenses and risk tiers`. Body: lenses as
files, a pinned read-only `deep-reviewer` (Opus 5.5, max), tiers that pick lenses and the Codex
effort, a second wave only on HIGH or CRITICAL; name the riding hunks (Current state) and each
adaptation. Ported-from `fa8c06f82fbc5ddd4f23ebe78157f5f2144439d2`, `0a8d61a4e`, `ef9559b14`,
`b84dbaeb2`, `2fb9d914a`, `9a2a8dae9`, `9d327f43e`, `c237c84e4`, `a87bdc3a9`, `6ce20f6ed`,
`ce7f8d6ba`, `d3fc3c4d0`, `2ca35d6e2`, `2c737d97c`, `099840f33`, `9e9f04df4`. Research: taom-src
v1.4.8 `Mission.AfterStart` (:3799-3815); `docs/modding/module-dependencies.md:120-131`.
Not-tested: a live tiered review (live check L4).

**Verify**: `refs .claude/skills/deep-review/SKILL.md .claude/skills/deep-review/lenses/*.md .claude/agents/deep-reviewer.md`
clean; `hooks c2` no new failure (3b covers the new agent; WP116's check-deep-review rows still
pass); `pyt test_dotnet_commands_flagged.py` OK; `python tools/lint_docs.py --drift-only` rc=0.

### Step C3: Codex effort is sized to each review (`ef9559b14`, `b84dbaeb2` review-codex timeout)

`take ef9559b14` for `.claude/skills/codex-verify/SKILL.md`, `.claude/skills/review-codex/SKILL.md`,
`.claude/skills/ship/SKILL.md`, `.codex/config.toml`, `docs/ai-includes/completion-workflow.md`,
`docs/ai-includes/new-culture-authoring.md`, `docs/reviews/REVIEW-GUIDE.md` (every removed line
of the codex-verify, review-codex, ship and completion-workflow hunks exists on 1.4.x, and so do
the new-culture-authoring and REVIEW-GUIDE lines they replace, checked this session; review-codex
applies on top of WP103's `785b2703e` pick, which is this commit's parent state for that file;
`.codex/config.toml` only gains two comment lines). `docs/reference/codex-integration.md` differs from the pre-image: add, after the
"`codex exec --help`" paragraph, one sentence that each review passes
`-c model_reasoning_effort="<level>"` sized to the change (`ultra` is the config's default and
ceiling). In codex-verify replace the probe string "Evidence, never invention" with "Verify evidence
before making claims" (AGENTS.md "Always"), as WP103 did for review-codex. Then `take b84dbaeb2
.claude/skills/review-codex/SKILL.md` (the 7200000 background timeout).

Commit `docs(skills): v2.0.28 - Codex effort is sized to each review`. Ported-from `ef9559b14`, `b84dbaeb2`.

**Verify**: `git -C "$WT" grep -n "codex exec" -- .claude docs/ai-includes docs/reference docs/reviews/REVIEW-GUIDE.md | grep -v model_reasoning_effort`
prints only prose lines that run no command (read each); `grep -c 'Evidence, never invention' .claude/skills/codex-verify/SKILL.md` prints 0.

### Step C4: align the harness with Claude Code (`b84dbaeb2`, `2ca35d6e2`, `2c737d97c` facts)

1. `harness-facts.md` (this line's layout; WP103 and WP116 rows stay). Add or update by section,
   each with the source the commit gives:

| Section | Row (first words) | From |
|---|---|---|
| Skill load semantics | `disable-model-invocation` row: add "when Claude tries to invoke one, it is told to ask the user to run it (v2.1.222)"; new row "A `context: fork` skill runs in the background ..." | `b84dbaeb2` |
| Agent (subagent) load semantics | "CLAUDE.md should stay under 200 lines ..." (keep the installed-version clause as "on the desktop, 2026-10-08"); "Custom and general-purpose subagents load the CLAUDE.md hierarchy ..." (`2ca35d6e2` wording); "On a native 1M model auto-compaction fires at about 967K ..."; "Explore runs on the main conversation's model ..."; "The Agent tool takes an `effort` parameter ..." (`2c737d97c` wording); "Subagents run in the background by default ..." | `b84dbaeb2`, `2ca35d6e2`, `2c737d97c` |
| Hook lifecycle | fail-open row gains the v2.1.288 sentence; the ask-in-bypass row gains its DOC source | `b84dbaeb2` |
| Rule loader (memory) semantics | the path-rule row: Read, Write or Edit (v2.1.288) and single-file Bash reads (v2.1.293) | `b84dbaeb2` |
| Tool and session behaviour (A5 created it) | background command time limit; VS Code Stop and Escape; Todo tools on Opus 5.5 | `b84dbaeb2` |

   Move "Last verified" to the execution date.
2. `docs/reference/mcp-servers.md`, `docs/reference/powershell-tool.md`: `take b84dbaeb2` each;
   resolve by keeping this line's server list (WP116 dropped github, git and filesystem).
3. `blob b84dbaeb2 docs/reviews/adopt-claude-code-2026-10-08.md`. Drop the CLAUDE.md hunks of both
   commits (this CLAUDE.md has none of those lines; the facts live in harness-facts.md).

Commit `chore(harness): v2.0.28 - align the harness with Claude Code 2.1.292`. Ported-from
`b84dbaeb2`, `2ca35d6e2`, `2c737d97c`.

**Verify**: `python tools/lint_docs.py --drift-only` rc=0; Docs report no new finding in the files.

### Step C5: fix stale facts in skills, agents and rules (`9e9f04df4`, rest)

`take 9e9f04df4` per file, keeping only hunks whose removed lines exist (measured this session):
`debugger.md` (7/7), `error-detective.md` (3/3), `refactoring-specialist.md` (14/16),
`taleworlds-researcher.md` (17/21; make the version lines say "the version pinned in
`.claude/pinned-game-version.txt`" and drop the ILSpy MCP fallback the tool list never granted),
`feature-builder.md` (13/15: "Rules" points at `AGENTS.md "Always"` and `.ai/review-reference.md`
"Architecture", not AGENTS.md "Architecture"; delete the "Iterative Retrieval" section **[G]**; take
the hunk that makes its post-build step 3 run `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=`
instead of `./build.ps1 -RunTests`), `csharp-architecture.md`
(delete "Stale-file re-read"; the threads hunk only if its pre-image text is present),
`csharp-patterns.md`, `gamemodels.md`, `harmony-patches.md`, `provenance.md`, `tests.md`,
`freeze/SKILL.md`, `unfreeze/SKILL.md`, `localize/SKILL.md`, `release/SKILL.md`. Hand edit
`native-crash-triage/SKILL.md` to `9e9f04df4`'s version-neutral wording. Skip the `engine-bump`
CI hunk and `external-skill-ports.md` (pre-images absent), and every `improve/` hunk (D3).
`blob 9e9f04df4 docs/reviews/prompt-audit-2026-10-08.md`.

Commit `docs(ai): v2.0.28 - fix stale facts in skills, agents and rules`. Ported-from `9e9f04df4`.

**Verify**: `pyt test_dotnet_commands_flagged.py` OK; `hooks c5` (3b) no new failure;
`grep -n "mcp__ilspy" .claude/agents/taleworlds-researcher.md` prints nothing.

### Step C6: builder template (`8c1305e20`)

`git -C "$WT" cherry-pick -n 8c1305e20` (clean in the simulation). Commit
`docs(ai): v2.0.28 - builder template asks for docs, bounds, true text`. Ported-from
`8c1305e20d718912649de7da8af72e94b8dc158e`.

**Verify**: `git -C "$WT" diff HEAD~1 --stat` shows only `docs/ai-includes/agent-teams.md`.

### Step C7: engine-bump body diff, relations rule, XSLT map, one stale skill (`43fe5ebe8`, `f382ff6fd`, `d6b537092`, `8b1083127` partial)

1. `engine-bump/SKILL.md`: insert `43fe5ebe8`'s paragraph "**Then diff at the MEMBER level ...**"
   before "## Phase 4" (the anchor exists at 1.4.x :85-89), so it follows WP137's closing Phase 3
   paragraph; begin its calibration sentence with "On the 1.5.x line, v1.5.2 calibration:". WP137 asks
   that the two merge, not duplicate: when `tools/engine_body_diff.py` exists (WP137), replace the
   parenthetical "(the 2026-09-14 pass: 62 rows, `bodydiff.py` in that session's scratchpad; rebuild it,
   it is forty lines)" with "(`tools/engine_body_diff.py`, the command in the paragraph above)".
2. `adapters.md`: append `f382ff6fd`'s "## Relations and party-screen writes (MANDATORY)" with its
   last bullet replaced by "Sources (v1.4.8): `ChangeRelationAction.cs:14-30`,
   `PartyScreenLogic.cs:1525-1534`, `PartyVM.cs:3246-3264`." (the lessons entry it links is
   WP142's).
3. `xslt.md` "Authoritative Source": take `d6b537092`'s bullets and table, with "(checked against the
   v1.5.4 install, 2026-10-08)" changed to "(checked against the installed v1.4.8, 2026-11-04)";
   in `xslt-check/SKILL.md` step 2 make the mapping match that table (five stylesheets read
   `Modules/SandBox/ModuleData/`, `spcultures.xslt` reads SandBoxCore, `lord_template_rosters.xslt`
   reads both).
4. Delete `.claude/skills/migration-status/` (its only reference doc is the finished v1.2 to v1.3
   tracker) and remove its CLAUDE.md soft-suggest line (:107) and its mention at :113; make :57
   read `41 of the 43`.

Commit `docs(ai): v2.0.28 - body diff, relations rule, XSLT map, stale skill`. Ported-from
`43fe5ebe8`, `f382ff6fd`, `d6b537092`, `8b1083127`. Research: taom-src v1.4.8
`ChangeRelationAction` (:14-30), `PartyScreenLogic` (:1525-1534), `PartyVM` (:3246-3264); the
installed v1.4.8 `Modules/SandBox` and `Modules/SandBoxCore` ModuleData listings.

**Verify**: `hooks c7` no new failure; `git -C "$WT" grep -n migration-status -- .claude CLAUDE.md AGENTS.md docs/ai-includes`
prints nothing; `python tools/lint_docs.py --drift-only` rc=0.

### Step C8: split the moduledata rule (`2ca35d6e2`)

On the live file (WP108 and WP136 edited it):
1. New `.claude/rules/data-scripts.md`: frontmatter `paths:` `"tools/**/*.py"`, `"tools/**/*.ps1"`,
   then the section "## Writing a data-mutating script? ..." moved verbatim (from its heading to the
   line before `# Validate ModuleData cross-references`).
2. New `docs/reference/moduledata-validator-codes.md`: a title, one intro line ("Moved from
   `.claude/rules/moduledata-validation.md`"), then verbatim the sections "What actually gets
   scanned", the long part of "Discipline" and "Why this rule exists", plus, under a `## Codes` heading,
   the whole `| Code (severity) | Bug class it catches |` table (it sits inside "Validate ModuleData
   cross-references"; that section stays in the rule without it, or the rule cannot reach the size below).
3. `moduledata-validation.md`: `paths:` narrowed to the ModuleData globs plus `tools/schemas/*.json`,
   `tools/validate_moduledata.py`, `tools/taom_schema.py`; keep "Validate ModuleData
   cross-references", "Gate per file kind" (K4) and a short "Discipline"; one paragraph "Three modules,
   not one" linking the codes doc. `rules-catalog.md`: the data-scripts row and the narrowed scope;
   `docs/features/moduledata-validation.md`: the one-line pointer from `2ca35d6e2`. CLAUDE.md "Scoped
   Rules": set the "N path-scoped + M always-load" counts to the catalog's rows now (1.4.x tip: 16 + 7;
   C1 adds one always-load rule, this step one path-scoped rule).
4. WP136's hand-off, under K12 only. RED: into the class of `tools/tests/test_validate_moduledata.py`
   that holds `test_the_hook_names_no_code_the_validator_cannot_emit`, copy from
   `gshow f1675c058 tools/tests/test_validate_moduledata.py` the attribute `DOC` and
   `test_every_emitted_code_has_a_row_in_the_codes_doc`, plus `VALIDATOR` and `_named_codes` if the class
   lacks them. `pyt test_validate_moduledata.py` names the missing codes (at the 1.4.x tip about 7, for
   example `BROKEN_BODY_PROPERTY_REF`). GREEN: for each, add its row to the `## Codes` table, copied from
   `gshow 2ca35d6e2 docs/reference/moduledata-validator-codes.md` or, failing that,
   `gshow f1675c058 docs/reference/moduledata-validator-codes.md`; a code in neither is a STOP. In
   `tools/data_gates.py` restore `CODES_DOC = "docs/reference/moduledata-validator-codes.md"` and its entry
   in `moduledata`'s `read_first` (`f1675c058:tools/data_gates.py:49`, `:98`; WP136 deleted them until this
   doc exists). No Ported-from line for this item; name WP136's hand-off in the body.

Commit `docs(harness): v2.0.28 - split the moduledata rule, data-scripts rule`. Ported-from `2ca35d6e2`.

**Verify** (after the commit): `wc -c < .claude/rules/moduledata-validation.md` under 12288;
`refs .claude/rules/data-scripts.md docs/reference/moduledata-validator-codes.md` clean; under K12
`pyt test_validate_moduledata.py` and `pyt test_data_gates.py` OK; and
`<notes>/check_split.py` (Write tool), run from the worktree root, prints `missing 0`:

```python
import subprocess
old = subprocess.run(["git", "show", "HEAD~1:.claude/rules/moduledata-validation.md"], capture_output=True,
                     text=True, encoding="utf-8").stdout.split("---", 2)[2]
new = "".join(open(f, encoding="utf-8").read() for f in (".claude/rules/moduledata-validation.md",
              ".claude/rules/data-scripts.md", "docs/reference/moduledata-validator-codes.md"))
miss = [l for l in old.splitlines() if l.strip() and l not in new]
print("missing", len(miss)); print("\n".join(miss[:20]))
```

### Part C done criteria

- [ ] Eight commits; `hooks`, `pyt` no failure beyond A0; Test = A0 + 1.
- [ ] `ls .claude/skills/deep-review/lenses | wc -l` prints 9; `ls .claude/agents | wc -l` prints 9.
- [ ] `refs` clean on every file named in C1, C2, C8; `wc -c CLAUDE.md` not above the A0 value.

## Part D: the /improve run pipeline (3 commits)

### Step D1: improve_ctl and integrate_branch (`218488f4e` and series)

RED: `blob ac1aa578b tools/tests/test_improve_ctl.py`, `blob d24aff340 tools/tests/test_integrate_branch.py`.
GREEN: `blob ac1aa578b tools/improve_ctl.py`, `blob 6c4b0981b tools/integrate_branch.py`
(`improve_ctl.py` imports `check_public_text` from B1 and reads `prompt-fixed.md` from WP103 and
`dispatch-rules.md` from D3; its tests that need `dispatch-rules.md` run green after D3).
`integrate_branch.py` unions conflicts only in `docs/reviews/lessons/*.md` and REVIEW-LOG.md;
CHANGELOG.md needs no rule here (decision D1: no WP branch edits it).

Commit `feat(tools): v2.0.28 - improve_ctl and integrate_branch run /improve`. Ported-from
`218488f4e`, `d6742dd91`, `ac1aa578b`, `0cc91b320`, `d648e00c0`, `459b63ad7`, `d24aff340`, `6c4b0981b`.

**Verify**: `pyt test_integrate_branch.py` OK; `pyt test_improve_ctl.py` OK with one skip
(`RealFileTests` skips its dispatch-rules case until D3; its prompt-fixed case reads WP103's file).

### Step D2: durable Workflow scripts (`1198881f0`, `bc160ca85`, `a7bbf3fe7` execute.js, `e4c43e59e` test row)

RED: `blob a7bbf3fe7 tools/tests/test_improve_workflows.py`. GREEN: `blob ad3967222` for the five
`.claude/skills/improve/workflows/*.js`; **[G]** in `plans.js` (about line 160, the "Filtered test"
line of `COMMANDS`) delete the space and the parenthetical that starts `(python tools/graphify_taom.py tests`
and ends `a change reaches)`.
Append `1198881f0`'s five-line block to the end of `.gitattributes` (LF for the workflows folder).
Add `e4c43e59e`'s 7b rows (an untracked `.js` under `.claude/skills` is denied) after the clean-tree
row of section 7b (`cft_run`); the hook's filter already names `js` (WP116, `88e805450`).

Commit `feat(improve): v2.0.28 - durable Workflow scripts for /improve runs`. Ported-from
`1198881f0`, `bc160ca85`, `a7bbf3fe7`, `e4c43e59e`.

**Verify**: after staging, `git -C "$WT" ls-files --eol .claude/skills/improve/workflows` shows
`i/lf w/lf` on every row (the Workflow tool refuses a script holding a CR);
`pyt test_improve_workflows.py` OK (node tests run); `hooks d2` no new failure.

### Step D3: rebuild /improve around the run pipeline (`1d1abd694` and series)

1. `blob ad3967222` for `.claude/skills/improve/SKILL.md` and `references/{audit-playbook,dispatch-rules,execute-and-review,plan-template,run-protocol}.md`;
   `git -C "$WT" rm .claude/skills/improve/references/closing-the-loop.md`.
2. Adaptations (line numbers at `ad3967222`):

| File:line | Change |
|---|---|
| SKILL.md:27, execute-and-review.md:153 | `AGENTS.md "Reviews"` becomes `AGENTS.md "Code Review Rules"` |
| SKILL.md:31 | `(AGENTS.md "Commands")` becomes `(.ai/verification.md)` |
| SKILL.md:33 | "outside the repo and off C:" becomes "outside the repo (on the desktop also off C:)" |
| SKILL.md:34 | `([CLAUDE.md](../../../CLAUDE.md) "Subagents")` becomes `([agent-teams.md](../../../docs/ai-includes/agent-teams.md))` |
| SKILL.md:38 | `AGENTS.md "Untrusted input"` becomes `AGENTS.md "Always"` |
| SKILL.md:43 | `CLAUDE.md "MCP and shell"` becomes `CLAUDE.md "MCP Usage Guide"` |
| audit-playbook.md:15, :61-62, :64, :228-229; run-protocol.md:53 | every orientation.md trap-index cite becomes CLAUDE.md "Traps" (rows "Agent slots recycle", "Co-op gating", "TAOM_Map settlements" exist there) |
| audit-playbook.md:55 | `AGENTS.md "Target"` becomes `.claude/pinned-game-version.txt` |
| audit-playbook.md:104 | `AGENTS.md "Verify before reference"` becomes `CLAUDE.md "Critical Rules"` |
| audit-playbook.md:138 | `AGENTS.md "Architecture"` becomes `.ai/review-reference.md` "Architecture" |
| audit-playbook.md:190 | `bash tools/context_budget_scan.sh` becomes `bash .claude/skills/context-budget/scan.sh` (this line keeps the skill and its scanner) |
| dispatch-rules.md:25-27; run-protocol.md:12, :15, :20 | the E: and off-C: drive rules apply on the desktop; on the laptop worktrees and scratch live on C: outside the repo (`docs/reference/development-machines.md`) |
| execute-and-review.md:99-103 **[G]** | blast radius from `git grep -n "<Type>"` in the worktree; callers outside the diff go in the item's `note` |
| plan-template.md:25 | `AGENTS.md "TDD"` becomes `AGENTS.md "Always"` |
| plan-template.md:26-28, :89 **[G]** | "the callers outside the change found with `git grep -n \"<Type>\"`, quoted in Current state"; drop `(AGENTS.md "Blast radius first")` |

   `AGENTS.md "Documentation duty"` stays (WP102 added it).
3. CLAUDE.md:99, the `/improve` routing row: add "; execute, review, merge or resume a plans
   backlog" to its trigger cell and "merge, push, issues and paid calls only on the maintainer's
   word" to its gate cell (`989c87497`), removing the dash in the cell you rewrite.
4. Restore what WP103 held back until this run protocol existed (its Step A2 items 2 and 3):
   `.claude/skills/review-codex/SKILL.md` Phase 3h item 3, the two lines `3. In an `/improve` run the
   review lead lists these instead of writing them, and the orchestrator` / `consolidates them once
   at wrap-up.` (`785b2703e`:251-252); and in `.claude/skills/review-codex/references/prompt-fixed.md`
   the preface clause back to "`/review-codex` Phase 2d pastes them, and `python tools/improve_ctl.py
   codex-prompt` reads them from here; edit them only here." (`785b2703e`, lines 3-4).

Commit `docs(improve): v2.0.28 - rebuild the skill around the run pipeline`. Body: the run
protocol, dispatch rules, execute-and-review stages, the workflow scripts' tools; adaptations as
listed; riding hunks named. Ported-from `1d1abd694`, `84c606fa3`, `37ddf739d`, `dc260635b`,
`014e23022`, `56e9970fd`, `6c4b0981b`, `989c87497`, `ef9559b14`, `9e9f04df4`, `4407fbc05`,
`4d17aeab8`, `a7bbf3fe7`, `2fb9d914a`, `09f3126a7`, `ad3967222`, `8b1083127`, `d6b537092`,
`785b2703e` (shared, WP103: the two restored texts).

**Verify**: `refs .claude/skills/improve/SKILL.md .claude/skills/improve/references/*.md .claude/skills/improve/workflows/*.js`
clean; `pyt test_dotnet_commands_flagged.py` OK; `pyt test_improve_ctl.py` OK (all, now);
`cd "$WT" && python tools/improve_ctl.py --help; echo rc=$?` rc=0.

### Part D done criteria

- [ ] Three commits; `pyt` no failure beyond A0; `hooks` no new failure; Test = A0 + 1.
- [ ] `test ! -e "$WT/.claude/skills/improve/references/closing-the-loop.md"`.

## Disposition table

Every commit in the pack and every shared hunk. Steps name where each lands.

| # | 1.5.x commit (40-hex) | Subject (short) | Disposition | Where / why not |
|---|---|---|---|---|
| 1 | `43fe5ebe8ea8f7b20b9ab337072d3468e3674103` | v1.5.2 impact, ranked body diff | adapted (partial) | C7: the engine-bump paragraph; target lines, v1.5.2 docs, PartyIconScale comments are 1.5.x |
| 2 | `5e5877bdfaba2746d98b94167cac616b9a41f65e` | check public text | ported | B1 |
| 3 | `218488f4e773597209d66326c6810a4d90575242` | improve_ctl | ported | D1 |
| 4 | `d648e00c001f838ef89b0ecd52248a9d5fbed247` | integrate_branch | ported | D1 |
| 5 | `e4c43e59e30803c3bf7eefe62b5ace8cb9d41856` | deny untracked .js | adapted | D2: test row; hook filter already in base (WP116) |
| 6 | `1198881f078bbe7d3fc74aa26e7fbf2dc64b5603` | Workflow scripts | transplanted | D2 |
| 7 | `1d1abd6940e7765f3d08247afa3ec68acda12d23` | rebuild /improve | transplanted | D3 (ModuleId part was WP103's) |
| 8 | `43db0b47a73acc19f522e6d11ab31a8a0b6daced` | drop lesson counts | skipped | bookkeeping: LESSONS-LEARNED.md is WP142's; the rest is a 1.5.x audit folder |
| 9 | `989c87497291fc9be92abd6c067af71d227001eb` | /improve index row | adapted | D3: CLAUDE.md routing row |
| 10 | `84c606fa341fd091bc893754135879b7fd5378db` | playbook section names | transplanted | D3 |
| 11 | `086abc57b8f8bded08e59bf30cf92afb49f933cd` | public-text path forms | ported | B1 |
| 12 | `459b63ad7813256b5e428702d9d5068e6f7b7e61` | integrate_branch stages | ported | D1 |
| 13 | `d6742dd91fd17aaedda8aaee9949e0286a5c1497` | improve_ctl hook-ask watch | ported | D1 |
| 14 | `bc160ca8518675e5721b09ed5bd586b57cad7862` | workflows never guess CLEAN | transplanted | D2 |
| 15 | `0cc91b320d1b411ade2eca28d0b498a80ad2c4d6` | two test cases | ported | B1, D1 |
| 16 | `37ddf739d2c2dce7c095f5bd223ab07f03347313` | dispatch rules ask forms | transplanted | D3 (its ask list matches the A1 gates) |
| 17 | `dc260635b2fc6543831d72481545858935ae16b1` | skill text matches tools | transplanted | D3 |
| 18 | `4f1a7ed9dd75b6bf7a3c32174d6c2c021cea177d` | close the run's PROGRESS | skipped | SKIP-BOOKKEEPING (1.5.x run record) |
| 19 | `014e23022c279628c3dab8cab52f217377313ebb` | exit 3 in merge steps | transplanted | D3 |
| 20 | `d24aff3408261dbb7adb830de597a71c0e951c79` | dry run union rule | ported | D1 |
| 21 | `ac1aa578bf800be54fcb59c45acadcd5edb4dd16` | watch and args paths | ported | D1 |
| 22 | `56e9970fd8ebcbeed8f565dce04cf51cfffc733a` | merge, cleanup, watch text | transplanted | D3 |
| 23 | `6c4b0981b79f5f6ff588c9fd51b6d2c29e53afbd` | quoted templates | transplanted | D1, D3 |
| 24 | `ef9559b14f849f43477e5b9a55628fb6f2314ea7` | Codex effort per review | ported | C3; deep-review and execute-and-review hunks ride C2, D3 |
| 25 | `fdec324f89d7e7e7f12d77c1724a24588cdfbe54` | model and effort tiers | ported | C1 |
| 26 | `d0e4bc947ef00a53dcd32db67d0f5e94b8b8d4b0` | specialist tiers | ported | C1 |
| 27 | `8b108312744c3daabc5fab6383dbea67db0d96dd` | remove 14 skills | adapted (partial) | C7: `migration-status` only; 13 kept (table below) |
| 28 | `d6b537092e85ab8ee854cf64eecde34219525214` | remove the last 14 skills | adapted (partial) | C7: the XSLT input map (true on v1.4.8); 13 kept |
| 29 | `b84dbaeb2a713301908c69b8366a2a8b639e6ede` | align with Claude Code | adapted | A2 (settings, .vscode), C3 (timeout), C4 (facts, docs, record) |
| 30 | `9e9f04df40c4a2350fd705b9dc2c0c158aee49c6` | stale facts | adapted | B1 (/issue), B2 (one rule line), C1 (architect), C2 (ship), C5; v1.5.4 pin and CI hunk skipped |
| 31 | `8c1305e20d718912649de7da8af72e94b8dc158e` | builder template | ported | C6 (clean pick) |
| 32 | `f382ff6fdc6c11de2d51ceae6d68fdd91588cc82` | Hard Questions | adapted (partial) | C7: the adapters.md section, re-cited to v1.4.8; feature, strings, settings out |
| 33 | `328ef9fc1a9cdedf0c431f7f3fbb41148e5c559e` | Hard Questions clans | skipped | SKIP-FEATURE |
| 34 | `79528cc81e146e623f15f95af2bde5027a25ee23` | ransom screen | skipped | SKIP-1.5-ONLY-FIX |
| 35 | `c8a3ac9d97ec33acfac996d1478ccf8404aa4317` | harness retro | transplanted | A3 |
| 36 | `6ce20f6edde1df897ed0032927dfcb43b35d756b` | Bash length, co-op | adapted (partial) | A2 (hook, detect-docs-gaps, banner, tests); C2 rides; co-op and tpacs out (D7; `bd1ac8292` reverts the co-op part) |
| 37 | `1c622732ecfce085fab27c56d3c7f39e3d59f2c5` | 30 s timeouts | adapted | A2 via M1 |
| 38 | `ae7c4edbf3f2ec958bbf50ef009ca5bca609f6fa` | length gate tests | ported | A2 |
| 39 | `0a8d61a4eb8f4607f531170bf073567f256782b1` | risk tiers | adapted (partial) | C2: SKILL.md; the Client/ and Main/ renames are out |
| 40 | `caa7ac35669d728bdcd0498f1044e5e744558d9e` | wrapper words | ported | A1 |
| 41 | `c64c78e7273d666404335845af266b31a596dba4` | retro output size | transplanted | A3 |
| 42 | `549bf390246afd94a364a6fab082445b2da6d751` | rtk records | adapted | A3 (record, row, bullet); lesson is WP142's |
| 43 | `6e59b4d6af6272291b2e834947d636dcb14f55de` | INDEX and tools rows | skipped | bookkeeping (WP142, WP143) |
| 44 | `78fe34c16878593b0ba15cfe8355e0ad865793cb` | retro output growth | transplanted | A3 |
| 45 | `0985c95157c461615c96232b4d0a5af099d88e0f` | retro top files read | transplanted | A3 |
| 46 | `d94bddecb3d50967a8b9266183cee879cb8d6844` | retro tokens by agent | transplanted | A3 |
| 47 | `b6f0ec794b03e8341001bd2052f05e0aebab1ce8` | Paperclip records | adapted | A3 (row, record); #1009 narrative and INDEX out |
| 48 | `3f53e4623a632c66feb4df9fa8477f632dbc3f2e` | retro exclude-session | transplanted | A3 |
| 49 | `4407fbc055cb48f7f748415e5298540e210af060` | ralph mixed commit | adapted | B1, B2, B3, B4, D3; CLAUDE.md item 8 out |
| 50 | `4d17aeab8da29c7426ebfa80c813fdda890ee6fd` | ralph breaker fixes | adapted | B2, D3 |
| 51 | `5eacdedc1d45957dff584583dc7b66468436a1ee` | WT_MAX 15 | ported | B3 |
| 52 | `a7bbf3fe7915393a9151cd7fdffe0fe59f91da61` | ralph self-enforcing | adapted | B2, D2, D3 |
| 53 | `2fb9d914abd7c93e3e11c7553a8d34de687e1382` | pstack checks | adapted | B3, C2, D3; lesson out |
| 54 | `88c0b6985bf7828abd744c1eef54eea6ae3a67d4` | pstack gate fixes | adapted | B3, B4, B5 |
| 55 | `28de0dc7d49df9e7a6a11f4d460213babd05a4cc` | pstack footers | ported | B3 |
| 56 | `bc7a7f0d9a157a395b06d6afee61802823abad6e` | merged-in feature docs | ported | B4 |
| 57 | `21f2ba79dff0e1af5688df69c3a981806ab8139a` | perf method section 4 | ported | B3 (rea record and row under K8) |
| 58 | `9d327f43e405e57af6065857b94c6bd22f30503e` | context-mode records | adapted | C2 (manual item 6, row, bullet) |
| 59 | `9a2a8dae9c8893626521134855b964c77fb6c2a2` | reviewers stop re-reading | adapted | C2 (lenses, agent, manual, record); lesson out |
| 60 | `c237c84e48d8b51a3c2e94508adbf99c3343e456` | marketplace checks | adapted | A3 (retro 10), B6 (3b, canary, facts), C2 (lenses) |
| 61 | `54ec60973eaf09a0808da1bdb8e926d1c66eb613` | backlink footer | skipped | SKIP-BOOKKEEPING |
| 62 | `09f3126a759c3b956e63cee789ff4bd6f8ed355f` | mbproj ids, drift hook | adapted (partial) | B4 (lint), B5 (hook, `_shellwords`), B7 (K5), D3. Skipped: the `audit_mbproj_registration.py` half, a WP136 follow-up (Maintenance notes; it sits on WP136's `bc30b76da`, and its 13-id table is v1.5.5-sourced: native rows need v1.4.8 native evidence; `soln_worldmap_color_grades` is not in the v1.4.8 managed dump), its modding docs and engine-bump line, lessons |
| 63 | `2473fa80edfa4bba8beb3bef48999b443cb3e3ea` | status line usage | ported | A5 (clean pick) |
| 64 | `49bf3b11518082cbcd43e92e636bbaa3301c1517` | claude-hud records | adapted | A5 |
| 65 | `ad3967222267b7e0d5a142a6fca1e74ef0197650` | retro, lint, perf follow-ups | adapted | A3, B3, B4, B5, B6, D3 |
| 66 | `c4ff1db517e73abab5c48fb68ef73b39c0f01258` | backlink | skipped | SKIP-BOOKKEEPING |
| 67 | `a87bdc3a95ed7bab1766e5523401ea7aa6437964` | quick run rule | adapted | B2 (rule), A3, C2 (ride) |
| 68 | `dfd4cf9e0a8f0850b5e8547f3549646d08732b8d` | status line terminal-only | ported | A5 |
| 69 | `ce7f8d6ba0ec9d90315942f5d9bdd6434d2f3fdc` | mission types, engine effects | adapted | C2 (Fords citations removed) |
| 70 | `d3fc3c4d038f6ccdafe8ec28a3893d737c534ffe` | self-check rules | adapted | C2 (citation removed) |
| 71 | `2ca35d6e2edcdaf38c057a53b0da11e1c141c0ad` | cut rule load | adapted | C2 (agent), C4 (facts), C8 (split); CLAUDE.md hunk out |
| 72 | `2c737d97cc7c22df1d1b3fc59135da9cf96facf2` | effort follows tier | ported | C1, C2, C4 |
| 73 | `6a54436c6a1b421f5d89c5483c1b8b3c90fc2243` | Headroom record | ported | A6 |
| 74 | `4358d1622c073ae6b2827c83223aad71a04a52bf` | retro prices usage | transplanted | A3 (`.gitleaksignore` line out) |
| 75 | `69d2e8c0a27ac3b061df1549065b1e68460ab437` | yield, provenance, stale loc | adapted | A3, A4; REVIEW-LOG hunk out; Step 7 is WP143's |
| 76 | `099840f33a63eb4d3b1cc0d9b46c6e1a4faeaf7f` | budget citation home | adapted | C2: the dead memory citation removed; WP143 adds the pointer with `4fbfb7adc` |
| S1 | `fa8c06f82fbc5ddd4f23ebe78157f5f2144439d2` (shared) | lens half | transplanted | C2 (tool half is WP136's) |
| S2 | `2992763f6c6f777de1d62e4ca4f8a0b83f165aa5` (shared, WP143) | model rule first paragraph | taken early | C1; WP143 skips this hunk |
| S3 | `452796db4b02217624ab0075d5c93742b2820fa3` (shared, WP143) | harness-retro skill hunk | taken early | A3; WP143 skips it |
| S4 | `4fbfb7adc8797ae4a6180f2e135ca5849ec08b07` (shared, WP143) | harness-retro doc hunk | taken early | A3; WP143 skips it |
| S5 | `f1675c0581a472fcc72fb3c8da7f1566912255fe` (shared, WP136) | /localize Case E | taken under K9 | A4; WP136 skips it |
| S6 | `785b2703e897fe3c3ccf01b8e7d9b847074d7bd1` (shared, WP103) | review-codex 3h item 3, prompt-fixed preface | restored | D3: WP103 held these back until the /improve run protocol existed |

**Skills the 1.5.x tip deleted (27 on this line).** Kept unless the deletion is justified here:

| Skills | Decision | Reason |
|---|---|---|
| `migration-status` | deleted (C7) | describes the finished v1.2 to v1.3 migration; flagged by this line's own audit (DOCS-04) |
| `build-fix`, `research`, `doc-graph`, `xslt-check`, `commit-split`, `codex-verify`, `context-save`, `context-restore`, `humanizer`, `scope-check`, `deslop`, `skill-stocktake`, `agent-introspection-debugging` | kept | CLAUDE.md routes to each by name (Skill Routing table); removing them means rewriting the routing table WP103 just trimmed |
| `finish-branch`, `context-budget`, `lint-cleanup-loop`, `new-adr`, `new-creature-mount`, `author-armor`, `verify-bindings` | kept | WP102 or WP116 edited them this programme (their scope lists) |
| `taom-src`, `new-culture`, `lord-skills`, `refine-creature-anim`, `knowledge-compile`, `lint-docs` | kept | the 1.5.x evidence is 1.5.x-era transcripts; A3's retro measures this line's own use, so a later cut rests on 1.4.x evidence |

## Test plan

- Every step names its RED and GREEN commands; the tests are the 1.5.x ones at the source states
  in "Pins and why", plus one new class, `DefaultSlugTests` (A3). Record `Ran N` per `pyt` run
  and the hook-suite summary per step in `<notes>/baseline.md`.
- Hook-suite sections added or extended: A1 (wrapper words), A2 (7l2, 7l3, 4c, timeouts), A3 (7o),
  B2 (4f, 7n), B3 (worktree), B5 (drift), B6 (3b allowlist), D2 (7b `.js`). C#: `VerifyCanaryTests`.
- Not testable here (Not-tested trailers, live checks L1 to L5): that the harness shows the length
  gate's exit-2 reason, that a spawn's `effort` beats a definition's in this install, a tiered
  /deep-review end to end, and an /improve run through the Workflow tool.

## Done criteria (whole WP)

- [ ] Parts A to D done criteria hold.
- [ ] `git -C "$WT" log --format=%s bannerlord-1.4.5..HEAD | grep -vc ': v2.0.28 - '` prints 0 and
      `... | awk 'length > 72'` prints nothing.
- [ ] Write the 69 short SHAs of rows 1-76 whose disposition is not "skipped", plus S1-S4 and S6 (and S5
      under K9), one per line to `<notes>/expected.txt`; then
      `git -C "$WT" log --format='%(trailers:key=Ported-from,valueonly)' bannerlord-1.4.5..HEAD | sed '/^$/d' | cut -c1-9 | sort -u > "$NOTES/got.txt"; sort -u "$NOTES/expected.txt" | diff - "$NOTES/got.txt"`
      prints nothing (B7 absent under not-K5 is fine only for `09f3126a7`, which other steps carry).
- [ ] No skipped row's SHA appears in got.txt.
- [ ] `git -C "$WT" diff --name-only bannerlord-1.4.5..HEAD` lists only Scope files; none of
      `CHANGELOG.md`, `docs/INDEX.md`, `tools/README.md`, `docs/reviews/lessons/`.
- [ ] Test total = A0 baseline + 1; `hooks`, `pyt` no failure beyond A0; lint `--drift-only` rc=0.

## Hand-off (the integrator records it; do not edit `plans/README.md`)

Report: per Part, status and commit hashes; `conditions.txt` (K1 to K13); the A0 baseline and final
counts (Test total, passed, skipped; hook-suite summary; tool-test failures by name); the
`check_provenance.py` miss count and the re-measured ratchet numbers; which M1 optional part was
applied. WP138 closes no old-plan row in `plans/README.md`; the integrator updates only WP138's own row.

## STOP conditions

Stop and report (do not improvise) if:

- An A0 hard check fails (H1 to H6), or any of the six H3 files is `DIFF`: copying `caa7ac356` onto a
  state other than `88e805450` could roll back a newer gate.
- M1 is not applied, or `check_m1.py` still fails after the maintainer's edit. Never write
  `.claude/settings.json` yourself by any route.
- C2 step 2 finds a rule an earlier WP added to the old deep-review SKILL.md that the new files lack.
- A transplanted file still fails `refs` after the listed adaptations (a 1.5.x-only reference the
  plan did not foresee): report the line; do not invent a replacement.
- The 3b allowlist (B6) fails on a skill or agent this plan does not add: report the key.
- A copied 1.5.x test fails after its GREEN step for a reason the adaptations do not explain, or a
  WP102, WP103 or WP116 hook-suite section fails. Never loosen an assertion or allowlist.
- `lint_docs.py --drift-only` stays non-zero after trimming only your own CLAUDE.md additions.
- The hook suite hangs (an `rc=124` row at a 10 s bound) or runs past 30 minutes.
- A main-checkout gate denies a commit over files you did not create.
- `audit_claude_config.py --min HIGH` reports a new HIGH or CRITICAL in a file you added.
- Any step seems to need a file outside Scope (`Main/**`, `Dependencies/**`, ADRs, ModuleData,
  `Directory.Build.props`).

## In-game checks owed

None. This WP changes no game code and no game data.

## Live harness checks owed (maintainer, after the integrator merges WP138)

Restart Claude Code in the main checkout. Record outcomes in the WP138 tracking issue.

- **L1** Bash tool: run a command over 7,500 characters (a long `echo` built from a file): blocked
  with the check-bash-length reason.
- **L2** Bash tool: `timeout 30 git commit --dry-run -m "no label"`: denied by the subject gate.
- **L3** Session start prints `HARNESS RETRO DUE`; run `/harness-retro` once and commit its record
  (`docs/reviews/harness-retro-<date>.md`), which quiets the banner for seven days.
- **L4** `/deep-review` on a small tier 3 change: it names the tier and runs only the lint step;
  on a tier 1 change without a paid-dispatch request it reports `CODEX: NOT RUN`.
- **L5** Spawn `fast-reader` with `effort: "high"`: its transcript shows `high`.

## Maintenance notes

- **WP143** applies the 2026-11-03 rules review on top of these files and must skip S2, S3 and S4.
  It should also restore `099840f33`'s agent-teams.md "Budget" pointer in deep-review SKILL.md
  Steps 2 and 2c once `4fbfb7adc` adds that section, and add the tools/README.md rows for the tools
  this WP adds (`harness_retro.py`, `check_public_text.py`, `improve_ctl.py`, `integrate_branch.py`,
  `loop_progress.py`, `worktree_audit.py`, `check_provenance.py`, `check_stale_loc_rows.py`).
- **WP141** lands only the graphify wrapper. The graphify workflow wiring is owed and not ported in
  this programme. That wiring re-adds, if wanted, the graphify blast-radius text this plan replaced with `git grep`
  (deep-review Step 1, execute-and-review Stage 2, plan-template, plans.js) and the `implementer.md`
  tests line.
- **WP142** syncs the lessons this WP left out (`build-tooling-workflow.md` entries of `c8a3ac9d9`,
  `6ce20f6ed`, `549bf3902`, `2fb9d914a`, `9a2a8dae9`, `09f3126a7`; the adapters entry of `f382ff6fd`)
  and LESSONS-LEARNED.md (`43db0b47a`).
- **WP136 follow-up**: the `audit_mbproj_registration.py` half of `09f3126a7` needs a v1.4.8
  re-derivation of the requested-id table, native rows included.
- **Skill set**: re-decide the 26 kept skills from a 1.4.x `/harness-retro` after a few weeks of
  sessions on this line (maintainer's call).
- **Reviewer focus** (no C# beyond one test, so `/deep-review` is not mandated): `/security-scan`
  (new hook, settings, tools), then read the C2 and D3 adaptation diffs against the tables here,
  the session-start blocks (A3, B2, B3) for timing, and `lint_docs.py`'s exit-code paths (B4).
