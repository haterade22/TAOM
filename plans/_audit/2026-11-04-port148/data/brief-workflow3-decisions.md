# Decisions brief for the plan writers (binding)

Run 2026-11-04-port148. Read this after `brief-workflow1.md` and `brief-workflow2-addendum.md` in the same folder.
Where this file differs from them, this file wins.

## Authority

On 2026-11-04 the maintainer delegated every open decision in this programme to the orchestrator's recommendation
("my answers will be to go with your best recommendations"). The decisions below are therefore made. A plan states
them as decided and names the decision id; it never reopens one as a question.

Three things stay with the maintainer whatever a plan says, and a plan routes each to the maintainer as an explicit step:

1. Outward-facing actions: push, PR, tag, release, `gh issue create`, and anything that spends money (Codex runs,
   `tools/translate_with_claude.py`).
2. Edits to `.claude/settings.json`, `.claude/settings.local.json`, `Directory.Build.props` and `docs/adrs/*.md`.
   The config-protection hook blocks agents from these. The plan gives the exact diff and the maintainer applies it.
3. In-game checks. Executors cannot run the game unattended; the plan lists each check with what to look for and in
   which log.

## Decisions

| Id | Decision |
|----|----------|
| D1 | Plan 020 (generated CHANGELOG): ADOPT on 1.4.x as WP102, before WP103's hook fix, so the CHANGELOG gates are deleted rather than switched on. From WP102 on, the commit body is the changelog entry and no plan stages `CHANGELOG.md`. Only WP101 hand-edits `CHANGELOG.md`. |
| D2 | Plan 018 composition root: ADOPT the infrastructure in wave 0 as WP105. From then on a ported 1.5.x feature module arrives as its `*Module.cs` plus one `FeatureModules.All` line, keeping its 1.5.x wiring tests and patch numbers; never hand-wire into `IoC.cs`/`SubModule.cs` something that has a 1.5.x module. A 1.4.x feature that never got a module keeps its hand-wired pattern. A module with `OwnsSaveData = true` is a STOP (1.5.x decision 48 is unsettled). |
| D3 | Serena: keep its read tools; deny its edit tools in `.claude/settings.json` (list in 1.5.x `7d13adc36`); applied by the maintainer in WP103. |
| D4 | Shader precompilation (1.5.x `25995dc91` re-parked it): keep it ACTIVE on 1.4.x unless WP113's research shows the park reason holds on v1.4.8; WP113 records the evidence and the outcome. |
| D5 | Skeleton-buffer guard: port the guard AND the watch, both on by default, with the v1.4.8 KnownBuilds row `[14185944] = ("v1.4.8", 0x88681, 0xD96D18, 0x886B4, 0x88475, 0x898F0)` (V03). On any pattern mismatch the guard disables itself and the watch reports. |
| D6 | Shadowfax screen-open speed: port the perf series as one squashed change; do NOT port `f48ef74dc` instant transitions (a UX change with no setting). |
| D7 | Co-op: 1.5.x co-op features stay out of 1.4.x (co-op is not supported there). Groups issue-966 and issue-967 are not ported. From issue-965 take only `bd1ac8292`'s tickCompleted lesson and its comments (`SaveShieldPolicy`, Patch69, one rule sentence; WP106); from issue-921 take only `175b149fd`'s /issue duplicate-search step (WP143). |
| D8 | The next 1.4.x release version is **v2.4.0** (then v2.4.1 and so on): three parts, so the generator and the subject hook accept it unchanged; it sorts above the public build's hand-labelled v2.0.29.5; it never collides with the 1.5.x tags v2.0.29 to v2.0.36; and "2.4" reads as "the 1.4 line" in a crash bundle's TaomVersion. Until that `/release` commit, every 1.4.x subject carries `v2.0.28`. |
| D9 | CI: do not re-add it. `ccef172ad` removed the workflow on both lines (57 of 186 runs had passed). Plan 010's CI commits and its RefAsm no-game gate (`8589a3384`) are not ported. Plan 019 (nullable ratchet) is an optional last part of WP122. |
| D10 | Execution channel: plans are executed by ordinary sessions that read the plan file, not by `/improve execute`. |
| D11 | ShaderCacheKeeper (`372773d1a`): NOT ported now. Revisit only if a v1.4.8-written `shader_mapping.bin` matches `keeper.c`'s parser AND the maintainer accepts shipping a proxy DLL into the game's bin folder. |
| D12 | Bow shader sources (`84f0459bc`): not ported; WP115's `missing_armory_shaders` packager gate (from `d99398f12`) catches the day a 1.4.8 Armory needs them. |
| D13 | Data defects: port a data fix only when the defect is verified on 1.4.x data and the fix needs no OUT content. Per item in WP134's notes. `1641fb985`'s four Dunland default_group retags are ported (formation correctness, no restat). |
| D14 | LOAD-TIME-07 (one generated NPCCharacters file): DROPPED. After plan 042 it saves under 0.5 s, and the 1.5.x maintainer decided against a build-step merge (V08). |
| D15 | `taom_body_length` errors (36 in E2): report them as an Armory pairing signal in WP115's pairing check; never allowlist them silently. |
| D16 | A locked RNG for TaomCombatMechanicsModel's crush roll: REJECTED (V05: concurrent xorshift128 draws cannot plausibly reach a constant state; 1.5.x never moved the draw). |
| D17 | Tick handshake: a launch-aware recovery on `Mission.OnTick` that restores `tickCompleted` only when this call launched no agent-tick job (V04), plus the MissionStartGuard port (WP109). Never restore after `TickAgentsAndTeamsAsync` has launched the job. |
| D18 | `4b7e36250`'s `merge=union` line for `docs/reviews/lessons/*.md`: deferred to the last step of WP142, after the lessons are synced. Never `merge=union` on `CHANGELOG.md`. |
| D19 | PowerShell git gates: WP103 widens the nine git gates (ten before WP102 removes one) to matcher `Bash|PowerShell` (maintainer's settings.json diff). Until WP116 lands the plan 027 end state, executors run git only through the Bash tool. |
| D20 | Laptop data: WP101 has a maintainer step to update the laptop's `LOTRLOME_Armory` to the public 2026-09-29 build and record the validator baseline. Data-touching commits on the laptop before that are a STOP. |
| D21 | `f75288ebf` (#634) is P1 defence in depth, not P0: the freeze cause is unproven (V05). |
| D22 | NameplateCull ports with a v1.4.8 adapter change (hideouts read `ISpottable.IsSpotted`; drop the `Settlement.get_FerryTarget` pin) (V03). |
| D23 | Banner atlases: port through a v1.4.8 Modding Kit re-import and pack republish (V07). The saving is mostly VRAM (19 named sheets: 1,216 MiB to 152 MiB), not private bytes; no plan promises a private-memory saving. |
| D24 | MEMORY-03 BC7 UI sheets: only sheets the 1.4.x rule does not protect (`banner-icon-generation.md:81` keeps `ui_taom_bannericons_NN` uncompressed), each with an in-game visual check. |

## The programme

44 work packages, ids 101 to 144, in waves 0 to 5, plus plan 145 added afterwards. The planning map (groups, lanes) is
`plans/_audit/2026-11-04-port148/data/wp_map.json`; each WP's input pack is `packs/<id>-<slug>.md`.

- Wave 0, foundation: 101 baseline sync; harness lane 102 (plan 020) then 103 (executor-safety harness); code lane
  104 (plan 009 and RepoPaths) then 105 (plan 018 runner). Both lanes start after 101.
- Wave 1, player symptoms (after wave 0): 106 off-thread and tick safety, 107 crash capture and PatchShield,
  108 missing-body load hang, 109 mission-start and skeleton guards, 110 War of the Ring, 111 battle freeze
  mitigations, 112 crash robustness, 113 shader stall and freeze watchdog, 114 save-growth investigation,
  115 release integrity, 116 hook-gate end state.
- Wave 2: 117 frame and memory instruments, 118 campaign load time, 119 cross-campaign resets, 120 map perf and
  VRAM, 121 texture memory diet (Kit), 122 test infrastructure gates.
- Wave 3: 123 always-on cost cuts, 124 mission hot paths, 125 campaign UI perf, 126 race and hero fixes,
  127 architecture refactors.
- Wave 4, shared-feature fixes: 128 and 132 enlistment, 129 careers, 130 battle and creatures, 131 messengers and
  recruitment, 133 inventory and UI, 134 data defects, 135 localization (last).
- Wave 5: 136 data gates, 137 dev tools (native, taom-src), 138 harness review pipeline, 139 in-game verdict
  programme, 140 GUI prefab gates, 141 dev tools (art, data), 142 docs-process sync, 143 harness rules sync,
  144 docs-engine sync.

A WP's "Depends on" lists only WPs that must be MERGED first. Lanes inside a wave run in parallel git worktrees.

## Executor environment (every plan states this)

- Machine: the maintainer's laptop. Repo `C:\Users\mikew\source\repos\TAOM`; Bannerlord v1.4.8 (build 119303) at
  `C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord` (env `BANNERLORD_GAME_DIR`). Never write
  into the game install. WP121 needs the Modding Kit (the laptop has the `Win64_Shipping_wEditor` build).
- Base branch: local `bannerlord-1.4.5` after WP101 (fast-forwarded to origin and merged with the #697 public tree),
  plus every WP merged before this one.
- One worktree per WP, created from the main checkout:
  `git worktree add C:\Users\mikew\source\repos\taom-port\<id> -b port148/<id>-<slug> bannerlord-1.4.5`.
  Work only there. The four repo-root tests accept a worktree's `.git` file.
- Executors commit on their WP branch only. They never push, open PRs, tag, or merge into `bannerlord-1.4.5`. The
  integrator (the maintainer, or a session the maintainer names) merges WP branches in the README order and runs a full build and
  test after each merge.
- `plans/README.md` belongs to the integrator. No executor edits it on a WP branch (every branch shares it, so row
  edits collide at merge): the executor reports status, commit hashes and measured counts in its hand-off, and the
  integrator records them after the merge. WP101 Part C, which runs in the main checkout, is the one exception.
- At most four executor worktrees at once, plus the integrator. Never two builds in one worktree. Never `./build.ps1`.
- Multi-session git safety (CLAUDE.md) applies: stage explicit paths, never `git add -A`, never `pull --rebase` with
  uncommitted work, never touch another session's files or stashes.

## Per-port procedure (every code WP; from D2, binding)

1. Apply a 1.5.x commit with `git cherry-pick -n <sha>`. Never let `cherry-pick`, `--continue`, `-C`, `-c` or
   `--amend --no-edit` create the commit: `GIT_EDITOR=true` on this laptop commits the 1.5.x message unedited. A series
   that merged with its own resolutions (plans 006, 007, 009, 013, 018) is ported from its post-merge END STATE instead.
2. Drop bookkeeping hunks: `CHANGELOG.md` always; also `.claude/`, `CLAUDE.md`, `AGENTS.md`,
   `docs/reviews/lessons/*`, `docs/reviews/LESSONS-LEARNED.md`, `docs/reviews/REVIEW-LOG.md`, `docs/INDEX.md`,
   `docs/reference/feature-map.md` and `tools/README.md` in code WPs (WPs 142, 143 and 144 sync these once). A
   conflicted bookkeeping path: `git checkout --ours <path>` then `git add <path>`. A cleanly changed one:
   `git checkout HEAD <path>`. Never write a bare `--` in a `git checkout` command, in any form: `block-dangerous-git.sh` asks for confirmation on it once WP103 makes the gate real, which stalls an unattended executor. `git checkout <sha> <path>` works the same, including for a path that does not exist in the worktree yet (tested 2026-11-04). Exceptions: a harness WP keeps its `.claude/` hunks; a feature's own
   `docs/features/<name>.md` travels with the feature (every completed feature gets one); a code WP keeps the
   `.claude/rules/*.md` lines that document the exact mechanism it ports (for example `f75288ebf`'s Finalizer bullet in
   `harmony-patches.md`), rewritten for 1.4.x, and WP143 reconciles the full rules later. Accepted beyond this rule, each with its reason in the plan: lessons entries in WP106, WP108 and WP114 (WP142 adds a heading only if it is absent); `.claude/` edits in WP104 (deep-review check 9), WP108 (`check-moduledata-validation.sh`, needed by its gate), WP109 (engine-bump) and WP110 (`/release`).
3. Resolve code conflicts as the plan says, keeping 1.4.x behaviour where the plan says so, and re-cite every engine
   line to the v1.4.8 dump or taom-src.
4. Stage with explicit `git add <paths>`, then run `git commit -F <msgfile>` in the NEXT sequential Bash call: never
   the same command, never a parallel sibling call, never through PowerShell.
5. Build and test after each commit (commands below). Full suite at the end of each part.

Commit message (CLAUDE.md "Commits"):

```
<type>(<scope>): v2.0.28 - <description>        <- at most 72 characters; keep the 1.5.x type and scope

<Body written for 1.4.x readers, wrapped at 72: what changed on the
1.4.8 line and why. Never paste a 1.5.x body: under plan 020 bodies
become player release notes. No em or en dashes. No Co-Authored-By
or "Generated with" line.>

Research: <what was verified on v1.4.8, e.g. taom-src Mission.OnTick>
Not-tested: <what needs an in-game check>
Ported-from: <40-hex sha>          <- one line per included 1.5.x commit
```

Optional trailers from CLAUDE.md (`Constraint:`, `Rejected:`, `Save-compat:`) when relevant. 35 port-ish 1.5.x
subjects carry no version label and 14 exceed 72 characters once relabelled: rewrite them.

## Commands and baseline

| Purpose | Command | Expected |
|---|---|---|
| Restore | `dotnet restore TAOM.sln` | exit 0 |
| Build | `dotnet build TAOM.sln -p:DisableModuleCopy=true -p:ModuleId=` | exit 0, 0 errors (the BHA0001 Patch88 warning is a known false positive until WP104 silences it) |
| Test | `dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId=` | 0 failed |
| Filtered | `dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId= --filter FullyQualifiedName~<Class>` | 0 failed |
| Data | `python tools/validate_moduledata.py` | no new ERROR vs the WP101 baseline |
| Hooks | `bash tools/test_hooks.sh` | no new failure vs the recorded baseline |
| Tool tests | `python -m unittest discover -s tools/tests -t .` | no new failure |
| Docs | `python tools/lint_docs.py` | no new finding in touched files |

Both `-p:` flags are mandatory on every build and test: on 1.4.x `Main/TAOM.csproj:133-136` does not gate the
CopyModule target on DisableModuleCopy, so a build without `-p:ModuleId=` deploys into the game install.

Baseline after WP101 (measured by the orchestrator on the merged tree): 9154 tests, 9151 passed, 3 skipped
(`EveryAllowedRaceIsARealRegisteredRace`, `WargAttack_FastWarg_InvokesRunningAttack`,
`WargAttack_SlowWarg_InvokesStandingAttack`), 0 failed. Later plans say "the count the previous merged WP recorded in
plans/README.md, plus the N new tests this plan adds".

## Plan conventions (on top of .claude/skills/improve/references/plan-template.md)

- File `plans/<id>-<slug>.md`, title `# Plan <id>: <imperative outcome>`.
- The template's sample commands are WRONG for 1.4.x (no `-p:ModuleId=`) and its "50/72" commit rule is stale: use the
  commands, commit template and procedure above.
- Status block adds: **Wave / lane**, **Executor** (laptop, or Kit for WP121), **Decisions applied** (D-ids).
  Planned at: 1.4.x `origin/bannerlord-1.4.5` `1b00d1acd` and 1.5.x `origin/bannerlord-1.5.x` `e45f6df2c`,
  2026-11-04. Drift check: `git diff --stat 1b00d1acd..HEAD -- <in-scope paths>`; changes made by earlier WPs of this
  programme are expected, so compare the live file against the plan's excerpts and STOP only on a real mismatch.
- Issue: cite the GitHub issue numbers the 1.5.x commits carry ("Port of #634") as the tracking reference; new work with
  no issue says "orchestrator creates the issue before implementation lands".
- A disposition table listing EVERY commit in the pack (owned groups, plus the shared hunks the notes assign): ported,
  adapted, transplanted, skipped (with the decision id or the reason). Nothing silently dropped.
- An "In-game checks owed" section for the maintainer.
- A WP too large for one session is split into Parts A, B, ... each independently landable with its own done criteria.
- No em dash or en dash characters in plan prose (commas, colons, semicolons, parentheses, new sentences). Hyphens in
  flags and paths are fine. No secret values. Repo paths only: never a scratchpad path.
- Convention pointers with one-line summaries: ADR-007 adapters (services never touch sealed TaleWorlds types),
  ADR-002 thin entry points (under 150 lines), ADR-003 no `#region`, ADR-004 no `[Obsolete]`, ADR-005 no `#if DEBUG`
  outside IoC registration, TDD (failing test first), the MCM json2 rule (rename a persisted setting, never flip its
  default), `.claude/rules/harmony-patches.md` (MovementOrder postfixes go through the shared deferred category).

## Evidence summary (verified in Workflow 2; reports in ../verify/)

- **Saves (V01).** E2's 12 load and 15 save statistics blocks show counts flat across about 80 campaign days of play,
  stepping up only across in-game loads: about 71,500 containers (85,000 to 90,000 objects) per in-game reload, 12
  increments by 21:18. A first load in a fresh process adds nothing. Not proven: that the growth is permanent in the
  file, and that it is exactly one step per load. The "stale XML object drags a static graph into the save" candidate is
  refuted for v1.4.8 (those types save only StringId, Id and IsRegistered). Holder unknown; two live hypotheses: SaveShield
  swallowing a load-callback fault on a second load, or an in-game load behaving differently from a main-menu load.
  Server GC cannot be recommended (three runtime hosts ship with v1.4.8; the players' host is unknown).
- **Harad first-battle freeze, E3 (V02).** Undecidable from the logs: E4's 1 Hz WaitingForRender stream went silent for
  68.3 s during a load that then completed, so silence alone proves nothing; E3's 92 to 122 s silence fits a compile at
  E4's measured rate (about 117 s for 2,697 shaders). E4 also ends in a second silent stall (806 shaders, at least 206 s,
  534 human agents, no mounts), about six times longer than that rate predicts. The #612 Warg cure does not carry over
  (E3's troops ride vanilla horses and camels). Skeleton-buffer overflow and a swallowed managed exception are ruled out
  for E3. The 1.4.x watchdog cannot settle it: the shader count it reads is published only by the blocked main thread.
  v1.4.8 wipes its shader cache when the mod list changes (D3 report).
- **Battle freeze with sound (V03, V04, V05).** Real v1.4.8 mechanisms that start after the battle is playable: the
  per-frame skeleton buffers have no bounds check (confirmed in the installed DLL; #701 records 1.4.8 freezes that
  stopped when corpses and ragdolls were lowered); the missing-collision-body wait; the tick handshake (Mission.cs:3740
  clears `tickCompleted`, only 3613 sets it, WaitTickCompletion 3585-3591 spins). On 1.4.x the innermost swallower of a
  mission-start throw is SaveShield's MISSION-INIT finalizer on `MissionState.FinishMissionLoading`, which logs only to
  `Modules/TAOM.Dependencies/diag.log`; that is what turns an `AfterStart` throw into an endless reload. Off-thread
  writers on 1.4.x: exactly three Dictionary writers (BT agent component, MountDespawn, FieldCommission RegisterKill);
  a corrupted Dictionary can spin forever, but only if native raised the callbacks on two threads at once, which is
  unverified, and vanilla's own unlocked collections would corrupt first.
- **War of the Ring (V06).** The only Free-win display on 1.4.x is the "End of the War" inquiry, which needs
  MCM "Enable Victory (End the War)" ON (default off since `601a55c57`). With victory on, a Free decision with the bar
  centred needs an empty Evil roster (nothing empties the 10 Evil realms in four quiet days) or an absolute lead the
  ratio bar and popup never show (most plausible after victory is enabled mid-campaign, or in a war that began early,
  such as a second campaign in one process, WOTR-02). WOTR-01 is a real defect but does not stop battles. The 1.5.x
  fixes chain-apply cleanly in order.
- **Memory (V07).** On a 16 GB machine TAOM 1.4.8 sits at about 6 GB at the main menu, 9 to 10 GB on the map, 9.5 to
  10.7 GB in battles and big towns. Texture quality top-to-minimum freed 2.3 GB on the map, foliage 0.77 GB. A cheat-mode
  inventory open lists every item (about 5,166 rows); one E4 open added 2,352 MB and froze 31 s (cheat mode on that
  player: Likely, not proven). 33 banner atlases are RGBA8 4096x4096 (64 MiB each). Each in-game load keeps about 1.8 GB
  more and also grows every later save (see Saves).
- **Load time (V08).** The v1.4.8 module-XML merge is quadratic exactly as plan 042 describes (MBObjectManager.cs:966-982,
  997-1010, 833-845): E1 window 27.2 s, NPCCharacters 15.10 s, TAOM's 42 files 11.80 s; E2's later loads 22.6 to 29.7 s;
  12 loads in E2 spent 292.9 s in merge windows.
- **Harness (D2).** Nine 1.4.x PreToolUse gates print a top-level `permissionDecision`, which Claude Code ignores, so
  the subject, files-tracked, ModuleData, doc-drift, confirm and /freeze gates are inert today; only three exit-2 gates
  block, and they die after a persistent cd (relative hook paths) and never see the PowerShell tool.
- **Patch88 (D4).** BHA0001 is a false positive; all three Patch88 targets exist on v1.4.8 and both player logs show the
  category applied. No Harmony target or statically resolvable reflection site is missing on v1.4.8.

## Not ported (record in your disposition table when it appears in your pack)

ShaderCacheKeeper (D11), bow shader sources (D12), co-op groups (D7), plan 010 CI and the RefAsm no-game gate (D9),
LOAD-TIME-07 (D14), the RNG lock (D16), `f48ef74dc` instant transitions (D6), and anything the verification reports
showed to be 1.5.x-only.

## After the cold reviews (added 2026-11-04)

A plan's Status block is authoritative for its Depends on; `data/wp_map.json` is the planning input and is not kept in step. A conditional on a WP in the same wave needs a dependency edge or a named integrator action, because lanes cut their branches in parallel and the condition reads the executor's base, not the eventual merge order. WP145 covers the 1.5.x commits newer than `e45f6df2c`, pinned at `4d1a9105a`.

## D25: the overnight run of 2026-11-04 (binding until the maintainer lifts it)

The maintainer asked for execution to proceed overnight on the planning session's recommendations. Two maintainer steps
could not be done: the public Armory is not on the laptop (searched: `C:\Games\LOTRAOM\public` is `e1.0.0` from August,
`LOTRAOMv2/lotraom-assets/v1.4` is `v2.0.8`, the OneDrive copy has no `SubModule.xml`), and `config-protection.sh` rightly
blocks agents from `.claude/settings.json`, which WP102 Step 0 and WP103 Part C need. So:

1. **WP101 runs on the installed `v2.0.26` Armory.** Step A2's expectations are recorded, not gated. Its item and data
   counts, `N_MERGED` against `N_ORIGIN`, and the ModuleData gate's exit code are recorded as provisional and are not a
   STOP. The baseline block says in its first line that the data half is provisional on `v2.0.26`. Build and test
   expectations still gate.
2. **Data rule.** A commit may stage `Main/_Module/ModuleData` only when it touches no item, roster, equipment, troop or
   mesh reference (strings, settings and config data qualify) and `python tools/validate_moduledata.py` shows no new ERROR
   against the provisional baseline. Everything else is deferred, not forced: skip that step, say so in the hand-off, and
   continue with whatever does not depend on it. D20 stands for those.
3. **Armory checks.** A check that the installed Armory is not `v2.0.26`, or that the baseline names the public build,
   is waived only for steps the data rule allows. A verdict about items or meshes measured on this Armory is void, so
   report it as owed rather than acting on it.
4. **WP102 and WP103 are not merged.** A Step 0 check that fails only because WP102's or WP103's output is absent is
   waived for work outside `.claude/`, `tools/test_hooks.sh` and the docs those plans rewrite. Any other failed check is
   still a STOP. The hooks print a format Claude Code ignores until WP103, so follow every commit and safety rule by hand.
   Commits still never stage `CHANGELOG.md` (D1).
5. **Integration branch.** After WP101 the trunk stays where it is. Every later WP branches from, and merges into, local
   `port148/overnight`, so the maintainer reviews the result and chooses when it reaches `bannerlord-1.4.5`. Where a plan
   names `bannerlord-1.4.5` as a base or reads `bannerlord-1.4.5:plans/README.md`, use `port148/overnight`. Nothing is
   pushed.
6. **WPs held for the maintainer:** 102, 103, 116 (settings), 115 (release choices), 121 (Modding Kit), 122 (depends on
   115), and 127 onward. Plans 108, 126, 133 and 134 defer their item verdicts under rule 2.
7. **On refresh:** when the public Armory is installed, re-run WP101 Step B7 and the deferred steps, and replace the
   provisional baseline with the measured one.

## D26: the in-game test moves to the end (2026-11-05, maintainer's instruction)

The maintainer said the in-game test happens at the end, so D25 rule 6 no longer holds 127 onward for that reason. D25
otherwise stands, including the data rule (rule 2), the WP102 and WP103 waiver (rule 4) and the integration branch (rule 5).

1. **Now runnable:** 127, 128, 129, 130, 131, 132, 133, 134, 135, 136, 137, 139, 141, 144.
2. **Still held:** 102, 103, 116 (settings), 115 (release choices), 121 (Modding Kit), 122 (depends on 115), and 138, 140,
   142, 143, 145, which depend on those.
3. **Partial is the expected outcome for some.** Where a step needs item, roster, equipment, troop or mesh data, or `.claude/`
   work that needs WP102's or WP103's output, defer that step, list it, and land what does not depend on it. A dependency
   on a held WP is waived only under D25 rule 4.
4. **No paid calls.** `tools/translate_with_claude.py` and anything else that spends money is not run. Strings stay
   registered and untranslated, and the translation pass is listed as owed.
5. **Known plan misprediction.** A compile-RED step that predicts CS0122 for a private member in another assembly prints
   CS1061 at that same member: Roslyn does not import private metadata. Treat that as matching and note it (WP119).
