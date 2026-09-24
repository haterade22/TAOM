# Triage bucket B: June 2026 harvest lines 299 to 601 (TECHDEBT 14, DEPS 12, DX 10), re-checked at `b2e387db`

Keyed by the harvest line number of each `### [` heading. Evidence is `path:line` at HEAD unless marked otherwise.

### L302 TECHDEBT-01: ADR architecture-enforcement tests do not exist
- **Verdict:** STILL_VALID · **Impact:** LOW
- **Evidence:** `TAOM.Tests/Architecture/` does not exist (`ls`); `docs/adrs/007-adapter-pattern.md:764` still points at `TAOM.Tests/Architecture/AdapterPatternArchitectureTests.cs`; `docs/adrs/002-thin-entry-points.md:327` still claims "Architecture Tests: Automated tests verify entry point line counts". `grep -rln "ADR-007|ADR-002|150 lines" TAOM.Tests` finds only feature tests.
- **Note:** June verifier REAL: agree. Doc lie plus a missing gate; no runtime risk.

### L310 TECHDEBT-05: Patch0_BattleScenes comment says "dormant" while the patch is live
- **Verdict:** STILL_VALID (half fixed) · **Impact:** LOW
- **Evidence:** Doc half fixed: CLAUDE.md no longer carries a Harmony table; `docs/reference/harmony-patch-registry.md:7-11` now says "ACTIVE" and records the old DISABLED lag. Code half remains: `Main/Features/BattleScenes/Hooks/MBMapScene_GetBattleSceneIndexMap_Patch.cs:18-19` still reads "Dormant today (category Patch0_BattleScenes is commented out in SubModule.cs)", while `git show b2e387db:Main/SubModule.cs:487` calls `_harmony.PatchCategory("Patch0_BattleScenes")` unconditionally.
- **Note:** June verifier REAL: agree. A one-line comment fix remains.

### L318 TECHDEBT-06: tools/ sprawl, three validator generations, spent one-shots at top level
- **Verdict:** STILL_VALID · **Impact:** LOW
- **Evidence:** `ls tools/*.py | wc -l` = 200 (June: 114); no `tools/archive/`. `tools/validate_gondor_refs.py`, `tools/validate_all_troop_refs.py`, `tools/audit_item_refs.py` all still present; `tools/README.md:44-46` marks them superseded "kept for now". Issue-keyed one-shots still present: `cleanup_deleted_troops_212.py`, `expand_party_templates_212.py`, `rollback_erebor_iron_misfile.py`, `apply_gondor_polish_224.py`. 57 top-level `.py` match `apply_|generate_|cleanup_|rollback_|expand_` (June: 41). Three cited one-shots were deleted since (`fix_v1_4_5_item_ids.py`, `migrate_equipment_type_1_4_3.py`, `migrate_hideouts_to_lotr.py`).
- **Note:** June verifier REAL: agree. Worse by count; discoverability cost only.

### L326 DEBT-05: guidance routes agents to superseded validators; spent one-offs remain
- **Verdict:** STILL_VALID (partly fixed) · **Impact:** LOW
- **Evidence:** CLAUDE.md:791 is gone (CLAUDE.md was slimmed; no hit for `validate_all_troop_refs` in CLAUDE.md or AGENTS.md). Still routed to gen-2: `docs/ai-includes/agent-operating-manual.md:44` ("Troop equipment refs | python tools/validate_all_troop_refs.py"), `.claude/skills/author-armor/SKILL.md:32-34`, `.claude/skills/new-culture/SKILL.md:22`. AGENTS.md "Commands" and `.claude/rules/moduledata-validation.md:72` name `validate_moduledata.py` as the consolidation. One-shot counts: see L318.
- **Note:** June verifier REAL: agree. Duplicate of L318 for the tools half; the routing half is the live defect.

### L334 TECHDEBT-03: ADR-002 fat campaign behaviors (Messengers, SpecialResources) hold decision logic
- **Verdict:** STILL_VALID · **Impact:** LOW
- **Evidence:** `wc -l`: `Main/Features/Messengers/MessengerCampaignBehavior.cs` 665 (June 644), `Main/Features/SpecialResources/SpecialResourcesBehavior.cs` 555 (June 443). The availability matrix is still inline at `MessengerCampaignBehavior.cs:271-291` (`IsAlive`/`Disabled`/`IsFugitive`/`IsActive`/`NotSpawned`). Some SpecialResources logic moved out since June (`PartyUpkeepReader.cs:20,34` now owns roster walk and town count; `SpecialResourceEarnPolicy`), but `ApplyDesertion` still mutates the roster in the behavior (`SpecialResourcesBehavior.cs:439-462`) and the grace/warn ladder is inline (`:236-282`).
- **Note:** Unverified in June; I agree it is real. Messengers carries a reviewed boundary-class note (`MessengerCampaignBehavior.cs:20-27`), so the stronger half is SpecialResources.

### L343 TECHDEBT-04: ~19 copy-pasted JSON config providers; two float-validation idioms
- **Verdict:** STILL_VALID · **Impact:** LOW
- **Evidence:** No shared base exists: `Main/Core/Configuration/` absent; `grep -rln "JsonConfigProviderBase|ConfigProviderBase" Main` returns nothing. `grep -rl "JsonConvert.DeserializeObject" Main --include=*.cs | xargs grep -l "File.Exists" | wc -l` = 37 (June: ~19); 33 of them are `*Provider.cs`. Two idioms still coexist: `FiniteFloatValidator` used in 109 files, hand-rolled `float.IsNaN|float.IsInfinity` in 34 files, e.g. `Main/Features/Messengers/MessengerConfigProvider.cs:63,71`.
- **Note:** June verifier REAL: agree. Grew by count. Simplicity check: a base class is a real win only if migration is done in bulk; otherwise it adds a third idiom.

### L352 DEBT-01: ADR-007 enforcement test never built; four service interfaces carry sealed types
- **Verdict:** STILL_VALID · **Impact:** LOW
- **Evidence:** Gate still absent (see L302). All four cited interfaces unchanged: `Main/Features/TroopWeight/ITroopWeightService.cs:11-14,35,44,62` (`CharacterObject`, `PartyBase`, `TroopRoster`, `TroopRosterElement`); `Main/Features/CultureMarketplace/ICultureMarketplaceMaintenanceService.cs:24,27` (`Settlement`); `Main/Features/FactionMap/ICultureSettingService.cs:7` (`CultureObject` plus two `object` params); `Main/Features/Arena/ITournamentService.cs:30` (`MBList<ItemObject>`). `grep -rl "^using TaleWorlds" Main --include="I*Service.cs" | wc -l` = 25 interface files now import TaleWorlds namespaces (not all are sealed-type violations; some use value structs such as `Vec3`).
- **Note:** Unverified in June; I confirm it. Overlaps L302 (gate) and L378 (implementations).

### L362 DEBT-02: JSON config-provider skeleton duplicated; SiegeDefense copy drifted (no Lazy, no Validate)
- **Verdict:** STILL_VALID · **Impact:** LOW
- **Evidence:** Duplication measured under L343 (37 files). `Main/Features/Siege/SiegeDefenseConfigProvider.cs:21-43` still has a public `LoadConfig()` with no `Lazy` and no `Validate` pass; the ints it loads (`Main/Features/Siege/Models/SiegeDefenseConfig.cs:9-12`: `RelationshipThreshold`, `ResponseWindowDays`, `RewardRelation`, `RewardInfluence`) are unchecked. It is called once (`SiegeDefenseService.cs:52`), so the missing cache costs nothing.
- **Note:** Unverified in June; the duplication is real, a duplicate of L343. The SiegeDefense example is weak: no floats, so the NaN rule does not bite.

### L370 DEBT-04: fat CampaignBehavior/MissionLogic entry points; no codified ADR-002 exemption
- **Verdict:** STILL_VALID · **Impact:** LOW
- **Evidence:** `wc -l`: `SpecialResourcesBehavior.cs` 555, `Main/Features/Elephant/ElephantMissionBehavior.cs` 248, `Main/Features/Warg/WargMissionBehavior.cs` 207, `MessengerCampaignBehavior.cs` 665. Decision ladder still inline at `SpecialResourcesBehavior.cs:236-282`. `grep -n -i "exempt|boundary class|deviation" docs/adrs/002-thin-entry-points.md` returns nothing: the exemption criteria were never added to the ADR. Elephant has a "Mission boundary" doc comment (`ElephantMissionBehavior.cs:17`), Warg none.
- **Note:** Unverified in June; confirmed. Duplicate of L334 plus the ADR-amendment ask.

### L378 TECHDEBT-02: services mutate campaign state through sealed types (SiegeDefense, CareerMenu, Tournament)
- **Verdict:** STILL_VALID · **Impact:** LOW
- **Evidence:** `Main/Features/Siege/SiegeDefenseService.cs:325` `Hero.MainHero.Clan.Influence += _config.RewardInfluence`, `:327` `Kingdom.All.FirstOrDefault`, `:330` `ChangeRelationAction.ApplyRelationChangeBetweenHeroes`, `:183` `MobileParty.MainParty?.CurrentSettlement`, `:198,285,299` `Settlement.Find`, `:287,301` `Campaign.Current.VisualTrackerManager`. `Main/Features/CharacterCreation/CareerMenuService.cs:96,183-184,316` read `Hero.MainHero` directly. `Main/Features/Arena/TournamentService.cs:47-52` iterates `Items.All` and returns `MBList<ItemObject>`. `grep -rl "^using TaleWorlds.CampaignSystem" Main/Features --include=*Service.cs | wc -l` = 39 (June: 26).
- **Note:** June verifier REAL: agree. ADR-007 adapters are decided, so this is a violation of the decided pattern, not a challenge to it. No crash class; testability debt.

### L388 TECHDEBT-07: VolunteerRecruitmentService 988-line data-in-code god service; JSON migration stalled at Gondor
- **Verdict:** STILL_VALID (partly fixed) · **Impact:** LOW
- **Evidence:** The god-file half is fixed by a partial-class split, `0f738f88` (2026-07-01): `Main/Features/TroopProgression/VolunteerRecruitmentService.cs` is now 252 lines plus 16 per-culture partials under `Main/Features/TroopProgression/RecruitmentPools/` (1,208 lines total). The data-in-code half remains: the static ctor at `VolunteerRecruitmentService.cs:24-64` still runs the hand-written `Initialize*` tables, `Main/_Module/ModuleData/recruitment_pools/` holds only `gondor.json`, and `.claude/rules/troops.md:19` still makes recruitment a C# edit.
- **Note:** June verifier REAL: agree on the data-in-code half. Recruitment rebalances still need a rebuild.

### L396 DEBT-03: recruitment data as C# code; Gondor dual-source with no consistency test
- **Verdict:** STILL_VALID (partly fixed) · **Impact:** LOW
- **Evidence:** The drift half is fixed: `GondorPools_HandWrittenFallback_MatchesProductionJson` (`TAOM.Tests/Features/TroopProgression/VolunteerRecruitmentServiceTests.cs:2066`) now holds the C# safety net in lockstep with `gondor.json` (added in `6dae36ad`, 2026-07-27; the accessor comment is at `VolunteerRecruitmentService.cs:222-226`). The single-culture JSON migration is unchanged (see L388).
- **Note:** Unverified in June; confirmed, less severe now. Duplicate of L388.

### L404 TECHDEBT-08: SubModule.cs 749-line single-owner registration hub
- **Verdict:** STILL_VALID · **Impact:** MED
- **Evidence:** Read at `b2e387db` via `git show` (working tree is another session's). `git show b2e387db:Main/SubModule.cs | wc -l` = 2148 (June 749, nearly 3x). Phase methods: `OnSubModuleLoad` 111 to 628, `OnGameLoaded` 849 to 1448 (about 600 lines), `OnGameInitializationFinished` 1449 to 1910, `OnMissionBehaviorInitialize` 1911 to 2077. No `IFeatureBootstrapper` or installer type exists (`grep` over Main). Also seed F9 in BRIEF.md.
- **Note:** June verifier REAL: agree, and it is much worse. Impact raised to MED: it is the contention point for every parallel session (the skip list in BRIEF.md shows it live right now).

### L412 DEBT-06: SubModule.cs highest-contention god file, 751 lines, 114 commits in 2026
- **Verdict:** STILL_VALID · **Impact:** MED
- **Evidence:** Same file as L404: 2,148 lines at HEAD; `git log --oneline --since=2026-01-01 -- Main/SubModule.cs | wc -l` = 250 (June 114), of which 135 since 2026-06-12. Registration-ish calls `grep -cE "PatchCategory|AddModel|AddBehavior|IoC.Resolve|AddMissionBehavior"` = 481 (June 227).
- **Note:** Unverified in June; confirmed. Duplicate of L404; F9 in the brief is the same finding.

### L423 DEPS-01: skill docs claim engine v1.3.15 and tell agents to distrust the dump
- **Verdict:** STILL_VALID (mostly fixed) · **Impact:** LOW
- **Evidence:** Fixed: `.claude/skills/taom-src/SKILL.md:3,9` now says it auto-detects the engine from `Version.xml` (rewrite landed in `0fc9b8c1`, the v1.4.7 bump); `.claude/skills/investigate/SKILL.md:67,100` no longer mention 1.3.15. Remaining: `.claude/skills/build-fix/SKILL.md:62` still reads "verify v1.3.15 signatures via `ilspycmd`, NOT the v1.4 decompile". New drift of the same class: `.claude/pinned-game-version.txt` is `v1.5.3`, yet `taom-src/SKILL.md:9` ("currently v1.5.2") and `investigate/SKILL.md:67` ("installed game is v1.5.2") plus five more skills (`grep -rln "v1\.5\.2" .claude/skills/*/SKILL.md`: codex-verify, engine-bump, lint-docs, lord-skills, native-crash-triage) name v1.5.2 as current.
- **Note:** June verifier REAL: agree on the original. The eager taom-src description is fixed; one routing line plus a one-minor-version lag remain.

### L431 DEPS-02: committed API snapshot stale; `-Check` gate wired nowhere
- **Verdict:** STILL_VALID · **Impact:** LOW
- **Evidence:** Snapshot regenerated for v1.5.3 (`docs/reference/taleworlds-api-snapshot/patch-targets.md:1,7`, "Patches: 247", last touched `ef6afeb6` / `8d5cfc62`, 2026-09-15), so the three June patches are now covered. It is stale again: no row for `Patch90_PreloadBodyGuard` (`Main/Features/PreloadBodyGuard/Hooks/PreloadHelper_WaitForMeshesToBeLoaded_Patch.cs:43-45`, added `fb8d7314`) or Patch91 (`Main/Features/BattleLoadDiagnostics/Hooks/Patch91_MissionTickStallProbes.cs:17,28`, added `f75288eb` 2026-09-22); `grep -c "PreloadHelper|WaitForMeshes"` and `grep -c "Patch91|MissionTickStall"` on the snapshot both return 0. `-Check` is still documented only (`README.md:68`); `grep -rln snapshot_api_surface .claude/hooks .github/workflows` returns nothing.
- **Note:** June verifier REAL: agree. The recurrence mechanism (manual refresh, no gate) is unchanged; runtime binding tests still cover live patches.

### L440 DEPS-03: dr3-maintenance.md drifted from shipped reality in 3+ places
- **Verdict:** STILL_VALID (mostly fixed) · **Impact:** LOW
- **Evidence:** Fixed: the exact-version stub list is gone; `docs/migration/dr3-maintenance.md:218` now says the `<Version>` values "are NOT listed here" and points at the stubs plus the v99 rule (`:231-238`); the six `BUTR.CrashReport*` DLLs are in the inventory (`:55,385-390,410`, added 2026-07-16). Remaining: `:225` still says each stub "Has `<SubModules />` empty"; `:229` right after it says that design was superseded by the `AliasStubSubModule` entry, so the text contradicts itself. `Dependencies/TAOM.Dependencies.csproj:59-60` comment names "ButterLib 2.11.0, UIExtenderEx 2.13.2, MCMv5 5.12.1" against pins `UIExtenderEx 2.13.3`, `MCM 5.12.3` (`:71-72`).
- **Note:** June verifier REAL: agree on the original. The dangerous line (empty SubModules) survives but is now followed by its correction.

### L449 DEPS-06: abandoned 89 MB `Dependencies/.vendor-source/` tree, UIExtenderEx 2.13.2 source
- **Verdict:** FIXED (residue) · **Impact:** LOW
- **Evidence:** The extracted source trees are gone: `ls -la Dependencies/.vendor-source` (dir mtime 2026-08-05) holds only six tarballs, 22 MB by `du -sh` (butterlib 2.10.4, cecil 0.11.5, harmony 2.4.2, mcm 5.11.4, monomod-master, uiextenderex 2.13.2). Tarballs are not grep-visible source, which was the harm. Still gitignored (`.gitignore:151`). Every tarball except Harmony now lags the pins (`Dependencies/TAOM.Dependencies.csproj:71-72`: UIExtenderEx 2.13.3, MCM 5.12.3).
- **Note:** June verifier REAL: agree it was real. Residue is untracked local disk; user action only.

### L456 DEPS-02: API snapshot missing patches added since 2026-05-31
- **Verdict:** STILL_VALID · **Impact:** LOW
- **Evidence:** Duplicate of L431. The June gap (Patch46) was closed by the 2026-09-15 regeneration; the same drift recurred for Patch90 and Patch91 (see L431).
- **Note:** June verifier REAL: agree on the class; the specific rows cited are fixed.

### L464 DEPS-03: taom-src SKILL.md describes v1.3.15 as installed and the dump as the thing to distrust
- **Verdict:** FIXED · **Impact:** LOW
- **Evidence:** `.claude/skills/taom-src/SKILL.md:3` description no longer names a version; `:9` "auto-detects the engine version from `Version.xml` ... caches at `~/.taom-src/<version>/`" and says the dump "can lag the installed engine after a bump", which matches AGENTS.md "Research first". `git log -S "v1.3.15 DLLs"` puts the rewrite in `0fc9b8c1` (v1.4.7 engine bump).
- **Note:** June verifier REAL: agree it was real. Residual "currently v1.5.2" vs the v1.5.3 pin is logged under L423.

### L472 DEPS-05: dr3-maintenance.md stub rows contradict v99; csproj comment cites MCM 5.11.3
- **Verdict:** FIXED (residue) · **Impact:** LOW
- **Evidence:** Stub rows removed (`docs/migration/dr3-maintenance.md:218`); stubs are all v99 (`Stubs/Bannerlord.Harmony/_Module/SubModule.xml:44` v2.4.99.0, `Stubs/Bannerlord.MBOptionScreen/_Module/SubModule.xml:12` v5.12.99.0, UIExtenderEx v2.13.99.0, ButterLib v2.12.99.0). The "5.11.3" comment is gone; `Dependencies/TAOM.Dependencies.csproj:59-60` has a new stale trio (logged under L440).
- **Note:** June verifier REAL: agree it was real. Duplicate of L440.

### L480 DEPS-06: no NuGet lockfile or NuGet.config; CI build job unreachable (dead workflow_dispatch)
- **Verdict:** STILL_VALID (half fixed) · **Impact:** LOW
- **Evidence:** Fixed half: `.github/workflows/build.yml:8` now declares `workflow_dispatch:` (added `fb609f29`, 2026-07-21), and the build job is deliberately self-hosted and owner-only (`:253,259`). Still valid: `git ls-files | grep -i "packages.lock.json|nuget.config"` returns nothing; no `RestorePackagesWithLockFile` in `Directory.Build.props` or any csproj; restore is `dotnet restore TAOM.sln` without `--locked-mode` (`build.yml:266`). The branch trigger is now `bannerlord-1.4.5` (`:3-7`), not the active `bannerlord-1.5.x` (seed F1).
- **Note:** June verifier REAL: agree. Lockfile half is cheap insurance, not a defect.

### L488 DEPS-05: lockfile-less NuGet restore
- **Verdict:** STILL_VALID · **Impact:** LOW
- **Evidence:** As L480: no lock files, no nuget.config, no locked-mode restore (`.github/workflows/build.yml:266`). Direct pins remain exact (`Main/TAOM.csproj:95-114`, `Dependencies/TAOM.Dependencies.csproj:36-72`); `Bannerlord.BuildResources 1.1.0.129` still runs build-time tasks.
- **Note:** June verifier REAL: agree it is present. Duplicate of L480; June itself flagged it borderline under the simplicity criterion, and I agree it needs Mike's decision rather than a fix.

### L495 DEPS-04: migration #210 closeout; 7 punch-list sections unchecked; TRACKING.md self-references stale
- **Verdict:** STALE · **Impact:** LOW
- **Evidence:** A decision was recorded: `docs/migration/TRACKING.md:8` says #210 was closed 2026-08-08 as `obsolete-premise` (per `docs/audits/issue-triage-2026-08-08.md`), and `:57` now reads "Complete, #210; closed 2026-08-08". The engine has since moved to v1.5.3 (`.claude/pinned-game-version.txt`). Residue: every box in `docs/migration/s6-runtime-punchlist.md:20-50` is still `- [ ]` (TRACKING.md:8 states this on purpose), and the machine-local plan path is still at `TRACKING.md:21` and `:191`.
- **Note:** June verifier REAL: it was real then. The 1.3.15 to 1.4.5 closeout premise is gone; the surviving in-game checks (alliance veto, ruler gear, hideout award gating) are known and deliberately left open.

### L504 DEPS-01: installed engine bumped to v1.4.6; repo still targets v1.4.5
- **Verdict:** STALE · **Impact:** LOW
- **Evidence:** Installed `Version.xml` reads `Singleplayer Value="v1.5.3"`; the pin is `v1.5.3` (`.claude/pinned-game-version.txt`); `Main/_Module/SubModule.xml:32` declares `version="v1.5.3.*"` for Native. The 1.4.6, 1.4.7, 1.4.8, 1.5.2 and 1.5.3 bumps all landed (`docs/migration/v1.4.7-impact.md` through `v1.5.3-impact.md`; `8d5cfc62`).
- **Note:** June verifier REAL: agree it was real then. Superseded by five later bumps.

### L513 DEPS-04: #210 open with a 7-item human punch list; TRACKING.md stale about it
- **Verdict:** STALE · **Impact:** LOW
- **Evidence:** Duplicate of L495. `docs/migration/TRACKING.md:8,57` record the 2026-08-08 close; the punch list stays unticked by decision.
- **Note:** June verifier REAL: agree it was real then.

### L524 DX-01: pre-commit build gate never blocks (pipeline status) yet builds on every commit
- **Verdict:** STALE · **Impact:** LOW
- **Evidence:** `.claude/hooks/check-build-before-commit.sh` no longer exists (deleted in `fde1fd52`, 2026-08-20). `docs/reference/hooks-catalog.md:22` records the decision: the hook became `block-no-verify.sh`; the build half "was dropped rather than re-armed" because hooks run in the main tree's cwd (a worktree commit would gate on another tree's build) and the build lacked `-p:DisableModuleCopy=true`. Verification moved to `check-verification-evidence.sh` (Stop) and `/verify`.
- **Note:** June verifier REAL: agree it was real (and the catalog adds a second cause, the missing `jq`). A recorded decision since, so the premise is gone.

### L533 DX-02: CI dark on the active branch; Build & Test permanently unrunnable
- **Verdict:** STILL_VALID (partly fixed) · **Impact:** MED
- **Evidence:** Fixed: `.github/workflows/build.yml:8` adds `workflow_dispatch:`; the build job is now explicitly self-hosted and owner-only (`:253,259`), with a warning job explaining the runner setup (`:16-25`); `.github/workflows/doc-budget.yml` runs `lint_docs.py --fail-on-drift` on every branch. Still valid: `build.yml:3-7` triggers only on `bannerlord-1.4.5`, while all current work is on `bannerlord-1.5.x`, so the XML, native-CRT, handbook and Python-test jobs never run for it and no C# compiles anywhere. Same as seed F1.
- **Note:** June verifier REAL: agree. The branch moved from master to 1.4.5 to 1.5.x and the trigger keeps trailing it.

### L541 DX-05: build.ps1 reads BANNERLORD_GAME_DIR only from the User registry scope
- **Verdict:** STILL_VALID · **Impact:** LOW
- **Evidence:** `build.ps1:11` `[System.Environment]::GetEnvironmentVariable("BANNERLORD_GAME_DIR", "User")`; `:13` then reports "is not set" even when `$env:BANNERLORD_GAME_DIR` is set for the process, which MSBuild does read (`Directory.Build.props:38` `<GameFolder Condition="'$(GameFolder)' == ''">$(BANNERLORD_GAME_DIR)</GameFolder>`).
- **Note:** June verifier REAL: agree. Single-developer impact; matters only for CI or a process-scoped shell.

### L549 DX-06: README "By the numbers" drifted
- **Verdict:** STILL_VALID · **Impact:** LOW
- **Evidence:** `README.md:15-17` "58 feature modules ... 2,600+ unit tests · 90 feature docs"; `:72` "58 feature modules"; `:76` "2,600+ tests"; `:157-158` "41 ... skills, 5 ... agents, 22 ... hooks, 18 ... rule files". Measured: `ls -d Main/Features/*/ | wc -l` = 110; `grep -rhoE "\[(TestMethod|DataTestMethod)" TAOM.Tests --include=*.cs | wc -l` = 9,256; `ls docs/features/*.md | wc -l` = 157; skills dirs 44; `.claude/agents/*.md` 6; hook scripts 29; rule files 23.
- **Note:** June verifier REAL: agree. Same as seed F8; the numbers were refreshed once and drifted again, which supports June's "drop exact counts" option.

### L556 DX-07: seven hooks hardcode the absolute repo path
- **Verdict:** FIXED · **Impact:** LOW
- **Evidence:** `grep -rn "mikew/source/repos|c:/Users/mikew" .claude/hooks/*.sh` returns nothing. `session-start.sh:19`, `session-stop.sh:7`, `pre-compact.sh:5`, `post-compact.sh:6`, `detect-docs-gaps.sh:16` use `cd "${CLAUDE_PROJECT_DIR:-$(pwd)}" || exit 0`; `log-agent.sh:18` uses `${CLAUDE_PROJECT_DIR:-$(pwd)}`. The build hook was deleted (L524). Path references changed in `c24f0dcd` (2026-07-10, repo relocation).
- **Note:** June verifier REAL: agree it was real.

### L563 DX-03: deep-review Stop reminder mute structurally dead; fires post-commit anyway
- **Verdict:** STILL_VALID (half fixed) · **Impact:** LOW
- **Evidence:** Fixed: `.claude/hooks/check-deep-review.sh:13` now matches `PATTERN="agent_type=deep-reviewer"`, which is the line format `log-agent.sh:32` writes, scoped to the last 8 hours (`:15-21`), and the stale "Bannerlord 1.3 compatibility" text is gone (`:32`). Pattern landed in `fa8c06f8` (2026-09-18). Still valid: it is still a Stop hook, so it cannot prevent a commit before review; `docs/reference/hooks-catalog.md:33` lists it as a reminder and no PreToolUse `git commit` gate references deep-review (`grep -ln deep-review .claude/hooks/*.sh` shows only Stop and doc/parity hooks).
- **Note:** June verifier REAL: agree. The alarm-fatigue half is fixed; the "prevent" half was optional in June and remains a design choice.

### L571 DX-03: CI gates are a strict subset of local gates (no validator, no doc lint, no tests)
- **Verdict:** STILL_VALID (partly fixed) · **Impact:** LOW
- **Evidence:** Fixed: `tools/tests` now run in CI (`.github/workflows/build.yml:192-217`, `a4141bbc` 2026-09-01); `lint_docs.py --fail-on-drift` runs on every branch (`.github/workflows/doc-budget.yml:30-35`). Still valid: `grep -n validate_moduledata .github/workflows/*.yml` returns nothing, so the degraded no-game validator mode is not run remotely; and `build.yml` does not trigger on `bannerlord-1.5.x` (L533).
- **Note:** June verifier REAL: agree on what remains.

### L579 DX-04: tools/README.md indexes ~33 of 113 scripts while billed as the full tool list
- **Verdict:** STILL_VALID (mostly fixed) · **Impact:** LOW
- **Evidence:** Coverage measured with a loop over `tools/*.py tools/*.ps1 tools/*.sh` and `grep -qF <basename> tools/README.md`: 225 scripts, 57 not mentioned (about 75% covered, June about 30%). `tools/README.md` is now 460 lines. Now documented: `lint_docs.py`, `graph_query.py`, `build_backlinks.py`, `taom-src.ps1`, `snapshot_api_surface.ps1`, `decompile_bannerlord.ps1`. Still missing, including the ones June singled out: `audit_scene_names.py`, `audit_battle_scenes.py`, `remap_stale_scene_names.py` (post-bump scene-crash battery), `pe_inspect.py` (used by CI, `.github/workflows/build.yml:96`), `audit_claude_config.py` (documented instead in `docs/ai-includes/agent-operating-manual.md:49`), `build_weapon_xml.py`, `native_crash_triage.py`, `package_release.py`. `docs/ai-includes/agent-operating-manual.md:51` still says "Full tool list | see tools/README.md". No lint asserts coverage (`grep -n "tools/README" tools/lint_docs.py` returns nothing).
- **Note:** June verifier REAL: agree. Much improved; the scene-audit gap is the one that matters for crash triage.

### L586 DX-02: CI dormant on the active branch; Build & Test unrunnable; game-independent gates not wired
- **Verdict:** STILL_VALID (partly fixed) · **Impact:** MED
- **Evidence:** Duplicate of L533 plus L571. Fixed: `workflow_dispatch` present (`.github/workflows/build.yml:8`); runner is `[self-hosted, windows]` (`:253`), matching its comment; `tools/tests` wired (`:192-217`); `lint_docs.py` wired in `doc-budget.yml`. Still valid: trigger branch `bannerlord-1.4.5` only (`:3-7`), not `bannerlord-1.5.x`; `validate_moduledata.py` not wired; the C# job skips unless the repo variable is set (`:259`), and seed F1 records it is not.
- **Note:** June verifier REAL: agree on the remainder.

### L595 DX-04: tools/README.md documents 39 of 130 scripts
- **Verdict:** STILL_VALID (mostly fixed) · **Impact:** LOW
- **Evidence:** Duplicate of L579: 168 of 225 scripts now mentioned; 57 missing, including the scene-audit suite and `pe_inspect.py`. One-shots are still not separated from durable tools (no `tools/archive/`, see L318).
- **Note:** June verifier REAL: agree on the remainder.

## What I did not cover

- I did not run `dotnet build`, `dotnet test`, `python tools/validate_moduledata.py` or `lint_docs.py`; every verdict rests on file reads, `git log`/`git show` and grep counts at `b2e387db`.
- `Main/SubModule.cs` was read only via `git show b2e387db:` (skip list). I did not read the Elephant or ElephantLike working-tree files beyond `wc -l` and a header grep; Elephant/ is not on the skip list and has no working-tree changes (`git status --short`).
- L431/L456: I compared snapshot rows by file basename and then class name for the two new patches only; I did not reconcile all 217 `[HarmonyPatch]` files class by class, so there may be more missing rows than Patch90 and Patch91.
- L352/L378: the 25 and 39 counts are `using TaleWorlds` imports, not a per-method sealed-type audit.
- I did not check GitHub state (no `gh` calls): whether the repo variable `BANNERLORD_GAME_DIR` or a self-hosted runner exists today is taken from seed F1, not re-verified.
- I did not re-verify the punch-list items themselves in game (alliance veto, ruler gear, hideout award), only that they remain unticked.
- The June verifier line for L334, L352, L362, L370, L396 and L412 is "unmatched" (never verified); I verified each against HEAD.
