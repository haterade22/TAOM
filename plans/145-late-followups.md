# Plan 145: Port the shared fixes from the 62 1.5.x commits newer than the planning pin

> **Executor instructions**: Follow this plan part by part, step by step. Run every verification command
> and confirm the expected result before moving on. If anything in "STOP conditions" occurs, stop and
> report; do not improvise. Parts A to G are each independently landable, in any order, once their row
> in the "Parts and their prerequisites" table holds. Report status, commit hashes and measured counts
> (tests, data, hooks, tool tests) in your hand-off; the integrator records them. Do NOT edit
> `plans/README.md`.
>
> **Drift check (run first, in the worktree)**:
> `git diff --stat 1b00d1acd..HEAD -- Main/Features/Warg Main/Features/CareerSystem Main/Features/RaceAge Main/Features/BannerColorPersistence Main/Features/SettlementNameplateFade Main/Features/MixedFormations Main/_Module/GUI tools .claude/hooks .ai`
> Almost everything here was changed by earlier WPs of this programme, which is expected. Compare the live
> files against each step's precheck and STOP only on a real mismatch.

## Status

- **Priority**: P2 (Part A is a real battle defect; the rest are follow-ups to code the programme already ported)
- **Effort**: L (seven parts; each fits one session)
- **Risk**: MED (Part A changes warg behaviour in every mission that has a warg; the others are gates, tools, one deleted model and one deleted patch)
- **Depends on**: WP104, WP108, WP110, WP114, WP115, WP116, WP123, WP124, WP126, WP129, WP130, WP133, WP135, WP136, WP137, WP138, WP140, WP141, WP142, WP143, WP144 merged (one edge per WP whose code a ported commit touches; per part below)
- **Category**: bug / tests / dx
- **Wave / lane**: wave 5, lane J (runs after every other WP; plans 101 to 144 are not reopened)
- **Executor**: laptop
- **Decisions applied**: D1 (no `CHANGELOG.md`; the commit body is the entry), D2 (no 1.5.x module here, so nothing goes through `FeatureModules.All`), D7 (co-op stays out: every `CoopMissionOwnership` line and `CoopPresenceReadGateTests` are dropped), D8 (subjects carry `v2.0.28` until the v2.4.0 release commit), D9 (no CI; gates run locally), D10, D13 (`14fcc5f33`, `17b738aa6` and the Mordor rosters stay on 1.4.x: dead rows are not a verified defect), D19 (git through the Bash tool)
- **Planned at**: 1.4.x `origin/bannerlord-1.4.5` `1b00d1acd` and 1.5.x pinned range `e45f6df2c..4d1a9105a` (62 non-merge commits; the range also holds 12 merges, which were not classified separately), 2026-11-04. Commits newer than `4d1a9105a` are out of scope. Never read the moving `origin/bannerlord-1.5.x` tip; every source below is a SHA.
- **Issue**: the 1.5.x commits carry none ("No issue: Mike asked for the RCA follow-ups fixed"; the rules-review follow-ups carry the same waiver). The orchestrator creates one issue before Part A lands; cite it as `Refs #<n>` in Part A's bodies. Tell the integrator if you were not given a number.

## Why this matters

While the programme was being planned, 1.5.x landed a review of its own recent work. Some of it corrects code this programme carries to 1.4.x, so without this plan those files land with known defects: on a warg rage, a dead attacker stops that warg's whole behaviour tree for the rest of the battle (proven in two 1.5.x game logs), a rider who leaves a raging warg never gets his formation back, and the 6 s rage timeout can never fire because the timer restarts on every tree pass. The plan also carries the data gates that report a misspelt roster id before it stops character loading, a one-command TAOM and TAOM_Map release check, safer tools (a translation rebuild that writes on a plain run, map tools that overwrite hand-kept `factions.json`), the corrected `block-no-verify` fallback, and the career icons of 18 cloned careers. Features (Seat of Seeing, Marshal's Word, Fords of War, Long Defeat, dire wolves), co-op and 1.5.x-only content are out; see the Disposition table.

## Current state

Simulation: `git merge-tree --write-tree --name-only --no-messages --merge-base=<sha>^ origin/bannerlord-1.4.5 <sha>` against `1b00d1acd` conflicts for every ported commit except where a row says clean, because the code each commit fixes reaches 1.4.x only when its WP merges. That result says nothing about your base, so each part has prechecks against what the WP leaves, and a STOP if an anchor is missing.

### Parts and their prerequisites

| Part | What | WPs that must be merged |
|---|---|---|
| A | Warg rage lifecycle | WP104 (`RepoPaths`), WP124 (constructors, UTC clock), WP130 (first formation fix, `WargRageFormationTests`) |
| B | Career icons, tooltip, NaN guard | WP104 (`RepoPaths`), WP129, WP140 (ratchet) |
| C | RaceAge, banner patch, nameplate provider, Mixed Formations, race test | WP114 (`#628` in `TaomPregnancyModel`), WP123 (`HotPathSettingsProvidersTests`), WP124, WP126, WP133 |
| D | GUI gates | WP130 (`BattleActionBarInputTests`), WP136 (`PrefabSpriteTests`), WP140 (`PrefabAttributeNameBindingTests`) |
| E | Validator codes, release pair check | WP108 (`check-moduledata-validation.sh` rows), WP110 (`/release` edits), WP115 (the pin E3's paragraph names), WP136, WP138 (`moduledata-validator-codes.md`), WP143 (`.githooks/pre-commit`) |
| F | Hooks, gates, routing | WP110 (`.ai/routing.json`), WP116, WP136, WP138, WP143 |
| G | Tools, docs, RCA, API snapshot | WP135, WP136, WP137, WP141, WP142, WP144, and Parts B to F of this plan for the docs step |

### Engine facts verified on the installed v1.4.8 (taom-src, cache `C:\Users\mikew\.taom-src\v1.4.8`)

- `TaleWorlds.MountAndBlade.Agent`: `public bool IsRunningAway { get; private set; }` `:784`; `internal void OnFleeing()` `:5128-5136` clears `Formation`, so a panicked rider must not be put back. `public Agent RiderAgent` `:718`.
- `TaleWorlds.CampaignSystem.GameComponents.DefaultHeroCreationModel.GetCharacterTemplateForOffspring` `:346-353`: a son gets `father.CharacterObject`, a daughter `mother.CharacterObject`. `TaomHeroCreationModel` repeats exactly this.
- `TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement.ClanPartyItemVM.GetCharacterCode` is `private static` `:1331-1342`: for a hero it returns `CampaignUIHelper.GetCharacterCode(character)`, which TAOM's `CampaignUIHelper_GetCharacterCode_Patch` already recolours. For a non-hero `BannerHeroAdapter.GetClanColorInfo` returns null (`BannerHeroAdapter.cs:8-12`), so `ClanPartyItemVM_GetCharacterCode_Patch` returns early. It can change nothing on v1.4.8.
- `TaleWorlds.MountAndBlade.DeploymentMissionController`: `OnMissionTick` `:83` calls `SetupTeams()` `:88`; `SetupTeams` ends with the auto-finish `FinishDeployment()` at `:181-184`.
- Native sprite data and brush for Part B2: `Modules/Native/GUI/Brushes/Popup.xml` defines `Popup.Frame`; `Modules/Native/GUI/NativeSpriteData.xml` names `StdAssets\Popup\canvas`.

### Facts about 1.4.x data, read this session

- 1.4.x has 18 enabled careers whose `ability_template_id` has no `CareerSystem\Abilities\` sprite in `Main/_Module/GUI/TAOMSpriteData.xml` (49 are registered). 1.5.x's `42` includes realms 1.4.x lacks. `far_harad_halftroll` also lacks one, but that career is commented out (WP129 line 161) and needs no row.
- `Main/_Module/ModuleData/factionmap/factions.json` already has `game_faction` `bluecraig` and `lindon` on the two pinned rows.
- `tools/border_match.py:24` imports `REGION_META`; `Main/_Module/SubModule.xml` names no NavalDLC row (case-insensitive grep), so `334c187e4`'s premise for excluding NavalDLC rosters does not hold on 1.4.x (E3).
- `VisualDefinition` rule run over the 51 prefabs at `1b00d1acd`: no widget names a missing definition. Orphans: `GameMenu.xml` `InfoPanel`, `Party/PartyScreen.xml` `FormationListPanel` and `FormationSeperator`, `FacGen/PreBuildCharacterSelection.xml` `LeftPanel`. The first three are the ones `498d07e08` removes (their widgets are inside comments on 1.4.x too). `LeftPanel`: UNVERIFIED whether WP140's `abb718c2f` step removes it; D3 handles either.
- `TaomHeroCreationModel` on 1.4.x holds only the offspring override, and no plan brings 1.5.x's `ILordKitDonorAdapter`, so here the whole class goes, not one method (C2).

### Conventions that bind this work

- **TDD**: failing test first (RED, GREEN, REFACTOR). A deletion that holds parity needs no new test. Pattern for source-pin tests: `TAOM.Tests/Features/Warg/WargRageFormationTests.cs` (sealed `Agent` has no unit seam).
- **ADR-007**: services never touch sealed TaleWorlds types; `RaceAgeService` takes plain ints and floats, the model keeps the `Hero` reads.
- **ADR-002** thin entry points; **ADR-003** no `#region`; **ADR-004** no `[Obsolete]`; **ADR-005** no `#if DEBUG`.
- **Single-owner**: `Main/SubModule.cs` is in scope for exactly two line deletions (C2, C3). `Main/IoC.cs` and `Main/TAOM.csproj`: nothing here needs them; if one seems to, STOP.
- **MCM json2**: no setting renamed or added; the Mixed Formations edit is hint text only.
- **Git gates**: test rows and diffs containing the banned hook flag text must never appear in a Bash command line. Pipe them from `git diff`, or use the Edit tool.

## Commands you will need

Run from the worktree root `C:\Users\mikew\source\repos\taom-port\145`.

| Purpose | Command | Expected |
|---|---|---|
| Restore | `dotnet restore TAOM.sln` | exit 0 |
| Build | `dotnet build TAOM.sln -p:DisableModuleCopy=true -p:ModuleId=` | exit 0, 0 errors |
| Test | `dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId=` | 0 failed; total = the count the previous merged WP recorded in `plans/README.md`, plus the new tests of the parts done |
| Filtered | `dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId= --filter FullyQualifiedName~<Class>` | 0 failed |
| Data | `python tools/validate_moduledata.py` | no new ERROR vs the WP101 baseline |
| Hooks | `bash tools/test_hooks.sh` | no new failure vs your F0 count |
| Tool tests | `python -m unittest discover -s tools/tests -t .` | no new failure vs your first run |
| Docs | `python tools/lint_docs.py` | no new finding in touched files |
| Dash check | `git diff --cached -U0 \| python -X utf8 -c "import sys; b=[l for l in sys.stdin.read().splitlines() if l.startswith('+') and ('\u2014' in l or '\u2013' in l)]; print(len(b)); print('\n'.join(b))"` | `0` for lines you wrote (the `\|` is the table's escaped pipe; a 1.5.x hunk carrying a dash: rewrite it) |

Both `-p:` flags are mandatory on every build and test: `Main/TAOM.csproj:133-136` does not gate the copy target on `DisableModuleCopy`. Never `./build.ps1`. Never two builds at once. Never write into the game install at `C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord` (env `BANNERLORD_GAME_DIR`), and never start the game.

Path-limited port helper, used throughout (define it in each Bash call that uses it):
```bash
pk() { local s=$1; shift; git diff "$s^" "$s" -- "$@" | git apply --3way; }
```
It stages what applies and leaves conflict markers in what does not. A conflict in a listed code path: resolve so that the 1.4.x lines stay and the commit's new lines arrive, using the step's notes; if the resolution needs a symbol that does not exist, STOP. The end-state steps diff `e45f6df2c` to `4d1a9105a` instead of one commit; use the ranges as written.

## Scope

**In scope**: exactly the paths each step names. Single-owner lines: `Main/SubModule.cs` (C2, C3 deletions).

**Out of scope** (do NOT touch):
- `CHANGELOG.md` (D1), `plans/README.md`, `.claude/settings.json`, `Directory.Build.props`, `docs/adrs/*`.
- `Client/**`, `Main/Composition/**`, `Main/Features/{SeatOfSeeing,MarshalsWord,FordsOfWar,LongDefeat,ArmourAcquisition,RaceAbilities,RealmBorders}/**`: absent on 1.4.x.
- `Main/Features/Warg/WargRageLog.cs` and `WargRageLogTests.cs` (Fords run report only), `Main/Features/CoopInterop/Missions/**` (D7).
- Dead-row deletions in `equipmentsets/*.xml` and `taom_partyTemplates.xml`, and the generator edits that go with them (D13).
- Every other RCA, codex-prereview prompt and feature doc in the pack.

## Git workflow

1. Worktree (Bash tool, from the main checkout):
   `git -C /c/Users/mikew/source/repos/TAOM worktree add /c/Users/mikew/source/repos/taom-port/145 -b port148/145-late-followups bannerlord-1.4.5`. Work only there.
2. Git only through the Bash tool (D19). Never `git add -A`, `-u` or `.`; stage explicit paths.
3. This plan uses `pk` and blob copies, not `cherry-pick`. If you do use `git cherry-pick -n`, never let cherry-pick, `--continue`, `-C`, `-c` or `--amend --no-edit` create the commit (`GIT_EDITOR=true` on this laptop would commit the 1.5.x message unedited).
4. Commit: `git add <paths>`, write the message to `/c/Users/mikew/source/repos/taom-port/145-msg.txt`, then in the NEXT, separate Bash call `git commit -F /c/Users/mikew/source/repos/taom-port/145-msg.txt`. Never in the same call, never in a parallel call, never through PowerShell. Never `--no-verify`; if a hook blocks, read it and fix the cause or STOP.
5. Message template (subject at most 72 characters; no em or en dash; no Co-Authored-By or "Generated with" line; body written for 1.4.x readers, wrapped at 72, never a pasted 1.5.x body):
   ```
   <type>(<scope>): v2.0.28 - <description>

   <what changed on the 1.4.8 line and why>

   Research: <what was verified on v1.4.8>
   Not-tested: <what needs an in-game check>
   Ported-from: <40-hex sha>      (one line per included 1.5.x commit)
   ```
6. Never push, open a PR, tag or merge into `bannerlord-1.4.5`; the integrator merges.

---

## Part A: a warg rage always closes and gives the formation back

Sources: `17b071c7a`, `7a21239dc`, `4a7b7164d`, `931d59084` (one line), `38e5e6274` (warg half). They rewrite each other, so this is one end-state port from `4d1a9105a`, minus `WargRageLog` and co-op.

### Step A0: prechecks

```bash
W=Main/Features/Warg; E=$W/BehaviorTreeElements
grep -c 'new PeriodicallyCheckIfCanAttackAnyone(adapterFactory)' $W/WargBehaviorTree.cs        # 2 (WP124)
grep -c 'GetNearAliveAgentsInRange(10, warg, _scratch)' $E/PeriodicallyCheckIfCanAttackAnyone.cs # 2 (WP124)
grep -c 'DateTime.UtcNow' $E/SetRageAttackTimer.cs                                              # 1 (WP124)
grep -c 'AgentsFormation.SetValue(rider.Formation)' $E/PrepareOnFirstCall.cs                    # 1 (WP130 E1)
grep -c 'if (formation != null) rider.Formation = formation' $E/CleanIfEnemyDied.cs             # 1 (WP130 E1)
grep -c 'noMovementController != null' $E/PrepareOnFirstCall.cs                                 # 1 (WP130 E2)
git ls-files TAOM.Tests/Features/Warg/WargRageFormationTests.cs TAOM.Tests/Infrastructure/RepoPaths.cs | wc -l   # 2
git grep -c 'CoopMissionOwnership' -- Main ; echo "grep exit $?"                                # no match, exit 1 (D7)
git grep -c 'WargRageLog' -- Main ; echo "grep exit $?"                                         # no match, exit 1
```
**Verify**: every line prints the shown result. Else STOP and name the WP that did not land. Run the Build and Test commands and record the total.

### Step A1: RED, the pins

```bash
T=TAOM.Tests/Features/Warg/WargRageFormationTests.cs
git show 4d1a9105a:$T > $T
sed -i 's/(see the fords-of-war build spec)/(plan 145, In-game checks)/' $T
sed -i 's/"CoopMissionOwnership.IsOwnedHere(live)) ResetAiControlledAgent(live, null)"/"live != owner) ResetAiControlledAgent(live, null)"/' $T
sed -i 's/, and only his owner may touch him"/"/' $T
sed -i 's/ValidStoredRider_NeedsTheSlotsOccupantAndOwnedHere/ValidStoredRider_NeedsTheSlotsOccupant/' $T
sed -i '/CoopMissionOwnership.IsOwnedHere(stored)/d' $T
sed -i 's/ In co-op only his owner touches him\.//' $T
sed -i 's/ (check 12 "stranded")//' $T
grep -c 'CoopMission\|co-op\|Fords\|fords' $T                                                  # 0
git diff e45f6df2c 4d1a9105a -- TAOM.Tests/Features/Warg/WargTreeNodeInjectionTests.cs | git apply --3way
```
`WargTreeNodeInjectionTests` gains `WargAiControlledIsNotFacingEnemy_EvaluateWithNoHitter_IsFalse`, `PeriodicallyCheckIfCanAttackAnyone_EvaluateOnARiderlessWargWithNoTeam_IsFalse`, `CheckOnceIfCanAttackEnemy_EvaluateOnARiderlessWargWithNoTeam_IsFalse`.

**Verify**: Build exit 0. Filtered `WargRageFormationTests`: `Total: 21` (20 methods, one with 2 rows), most failing. Filtered `WargTreeNodeInjectionTests`: exactly those 3 fail.

### Step A2: GREEN

1. New tasks, verbatim:
   `for f in CloseOpenRage CloseRageOnNewRider RestoreDismountedRider; do git show 4d1a9105a:$E/$f.cs > $E/$f.cs; done`
2. `CleanIfEnemyDied.cs` and `OnWargDied.cs` from the pin, then adapted:
   ```bash
   git show 4d1a9105a:$E/CleanIfEnemyDied.cs > $E/CleanIfEnemyDied.cs
   sed -i '/^using TAOM.Features.CoopInterop.Missions;/d; /WargRageLog.Active/d' $E/CleanIfEnemyDied.cs
   sed -i 's/ && CoopMissionOwnership.IsOwnedHere(live))/)/; s/ || !CoopMissionOwnership.IsOwnedHere(stored))/)/' $E/CleanIfEnemyDied.cs
   sed -i 's|// The stored handle is held across frames, so it must still be the slot.s occupant, and in co-op only its owner restores it|// The stored handle is held across frames, so it must still be the slot'"'"'s occupant.|; /^    \/\/ (as OnWargDied does). Null otherwise.$/d' $E/CleanIfEnemyDied.cs
   git show 4d1a9105a:$E/OnWargDied.cs > $E/OnWargDied.cs
   sed -i 's|// The stored rider.s slot and owner are checked there. Co-op (#967): a peer holding him as a puppet must not reset him.|// The stored rider'"'"'s slot is checked there.|' $E/OnWargDied.cs
   grep -c 'Coop\|co-op\|WargRageLog' $E/CleanIfEnemyDied.cs $E/OnWargDied.cs                # 0 and 0
   ```
3. `PrepareOnFirstCall.cs`, by hand (1.4.x has no `IAutonomousMovementController`, keep WP130's `noMovementController` lines):
   - Class line gains `, IBTMountBase`; add `BTBlackboardValue<Agent> _rider;` and `public BTBlackboardValue<Agent> Rider { get => _rider; set => _rider = value; }` beside the others.
   - Player branch, directly after the `noMovementController.TargetAgent` line:
     ```csharp
     // The no-rider branch and OnWargDied find whose controller this rage holds through this.
     Rider.SetValue(Agent.GetValue().RiderAgent);
     ```
   - AI branch body becomes:
     ```csharp
     Agent rider = Agent.GetValue()?.RiderAgent;
     if (rider != null)
     {
         // The tree was built before the rider's formation was set, so its stored formation is null for a warg spawned after
         // the first scan. A hit in the second after a rage's last attack starts the next rage before the first closes: this
         // rider has no formation by then, and the stored one is still his.
         if (rider.Formation != null || !ReferenceEquals(Rider.GetValue(), rider))
             AgentsFormation.SetValue(rider.Formation);
         // A rider who leaves the living warg mid-rage is no longer its RiderAgent: the reset finds him through this.
         Rider.SetValue(rider);
         rider.Formation = null;
     }
     ```
4. Clean range diffs (the WP124 state of these files equals `e45f6df2c` plus the UTC change; a conflict here is a STOP):
   ```bash
   git diff e45f6df2c 4d1a9105a -- $E/PeriodicallyCheckIfCanAttackAnyone.cs $E/WargAiControlledIsNotFacingEnemy.cs $E/SetRageAttackTimer.cs $E/WargAttackTask.cs $E/WargTryToGoRage.cs $W/WargBehaviorTree.cs | git apply --3way
   ```
5. `WargConfig.cs`, by hand (do NOT take `53cf195ac`'s dire wolf hunks): replace `public const int minRageAttacks = 2;` and `public const int maxRageAttacks = 3;` with `public const int rageAttacks = 2;`.
6. `docs/features/warg-combat.md`, by hand:
   - Config table: replace the `minRageAttacks` and `maxRageAttacks` rows with `| \`rageAttacks\` | 2 | Attacks in rage mode |`.
   - "Rage Mode Flow" item 2: `2. 10% chance to enter rage, for 2 attacks`. Item 6: `6. On completion, enemy death or the 6 s timeout: control returns. The timer starts once per rage and each attack restarts it, so the timeout means 6 s with no attack, and it ends a chase`.
   - After the list add: `**How a rage closes.** The rage start stores the rider and his formation on the blackboard. A rage belongs to that rider. Four tasks close it, all through \`CleanIfEnemyDied\`: its own end inside the rage branch, \`CloseOpenRage\` when no enemy is within 60 m (that branch wins every pass, so the rage branch would not run), \`RestoreDismountedRider\` when the warg has no rider, and \`CloseRageOnNewRider\` when another rider has mounted. The stored rider gets his formation back even if he has left the warg. A new rider keeps his own, and a panicked rider stays out, because \`Agent.OnFleeing\` cleared his on purpose. Closing clears both stored values, so a later warg death touches nobody; \`OnWargDied\` gives a formation back only while a rage is open. On a dismount, a player's rage with no stored formation stays open until a remount or the warg's death. Whether a mount's \`IsPlayerControlled\` is ever true is UNVERIFIED on v1.4.8: \`Agent.Build\` gives a mount the AI controller, and no managed code read so far changes it. If it never is, step 3 above never happens.`
   - Key Files: change the `BehaviorTreeElements/` row's count to `BT nodes (decorators, tasks, listeners)`.

**Verify**: Build exit 0. Filtered `TAOM.Tests.Features.Warg` 0 failed; `WargRageFormationTests` 21 passed. `git grep -n 'minRageAttacks\|maxRageAttacks' -- Main TAOM.Tests` prints nothing. Full Test 0 failed. Stage: the 3 new files, the 8 edited nodes, `WargBehaviorTree.cs`, `WargConfig.cs`, the two test files, `warg-combat.md`.

Commit A subject: `fix(warg): v2.0.28 - a rage always closes and gives the formation back`
Body must say: a dead hitter threw in the pass that took the formation and stopped that warg's tree for the battle (two more throws on a riderless warg, because a mount has no Team); the stored rider gets his formation back first; a new rider closes the old rage; a panicked rider stays out; the 6 s timeout now means 6 s with no attack, so rages end sooner than before, a behaviour change players can see. Trailers: `Research: v1.4.8 Agent.OnFleeing :5128, Agent.IsRunningAway :784`, `Not-tested: a rider leaving a raging warg, and a rage's end, in a real battle`, `Ported-from:` for `17b071c7a85b20434115bc5e3408e80762ad0eda`, `7a21239dcb59e8fa6f5d5d1f543a0bd01ef8e301`, `4a7b7164d0168eb42354be20c087b793564e9650`, `931d59084b4e5b571628d1baf32dd05680b5d0d4`, `38e5e627427d8f8bb7064d77fca37ab7a2d5945b`, `Refs #<n>`.

### Part A done criteria

- [ ] `git grep -n 'CoopMissionOwnership\|WargRageLog\|IAutonomousMovementController' -- Main/Features/Warg TAOM.Tests/Features/Warg` prints nothing.
- [ ] `git grep -c 'new CloseOpenRage\|new RestoreDismountedRider\|new CloseRageOnNewRider' -- Main/Features/Warg/WargBehaviorTree.cs` prints `Main/Features/Warg/WargBehaviorTree.cs:5` (`CloseOpenRage` 1, `RestoreDismountedRider` 1, `CloseRageOnNewRider` 3).
- [ ] Full Test 0 failed, total = baseline + 22 (19 net cases in the formation file, 3 injection tests).

---

## Part B: career icons, the diamond tooltip, the mutation guard

### Step B0: prechecks

`git grep -c 'IsFormerPlayerHeroQuest' -- Main/Features/CareerSystem/CareerQuestService.cs` at least 1 (WP129); `git ls-files TAOM.Tests/Infrastructure/GauntletStretchInCoverRatchetTests.cs` one line (WP140); `grep -c 'CareerSystem/CareerScreen.xml"\] = 3' TAOM.Tests/Infrastructure/GauntletStretchInCoverRatchetTests.cs` prints 1; `grep -c 'Id="DiamondTooltip"' Main/_Module/GUI/PreFabs/CareerSystem/CareerScreen.xml` prints 3. Else STOP.

### Step B1: 18 cloned careers show their source's icon (`4d9ae0e8a`, adapted)

1. RED: `git show 4d9ae0e8a:TAOM.Tests/Features/CareerSystem/CareerAbilitySpriteTests.cs > TAOM.Tests/Features/CareerSystem/CareerAbilitySpriteTests.cs`. It uses `RepoPaths` (WP104). Build fails on `AbilitySprite`: that is the red.
2. GREEN code: `pk 4d9ae0e8a Main/Features/CareerSystem/CareerConfigProvider.cs Main/Features/CareerSystem/Domain/CareerDefinition.cs Main/Features/CareerSystem/UI/CareerScreenVM.cs tools/insert_new_faction_careers.py`. Conflicts: keep the parameters WP129 added; `abilitySprite` goes last in the constructor call and signature.
3. Data: do not apply the XML hunk (24 of its 42 rows are careers 1.4.x lacks). In `Main/_Module/ModuleData/career_system/taom_careers.xml`, directly after each career's `ability_template_id="..."` line, add a line with the same indent, per this table (source ids read from `4d1a9105a`):

   | Career | `ability_sprite` |
   |---|---|
   | `goblin_troll_driver`, `misty_troll_goad`, `craig_pit_driver` | `cave_troll_master_ability` |
   | `goblin_tunnel_stalker`, `misty_deep_marksman`, `craig_crag_stalker` | `goblin_sniper_ability` |
   | `goblin_warg_master`, `misty_warg_chieftain`, `craig_warg_master` | `warg_pack_leader_ability` |
   | `falathrim_blade_dancer` | `blade_dancer_ability` |
   | `falathrim_sentinel` | `elven_archer_ability` |
   | `falathrim_mariner` | `rivendell_knight_ability` |
   | `shaghana_zajana_tribesman`, `abanissa_house_guard` | `tribesman_of_jelut_ability` |
   | `shaghana_chatak_javelineer`, `abanissa_jiret_javelineer` | `pezarsani_javelineer_ability` |
   | `shaghana_beast_rider`, `abanissa_ivory_rider` | `mahud_beast_rider_ability` |

   If `EveryShippedCareer_AbilitySprite_IsRegisteredInTheSpriteData` then names another career, STOP and report it (an earlier WP changed the career set). Do not add art.

**Verify**: Build exit 0; Filtered `CareerAbilitySpriteTests` 3 passed; `grep -c 'ability_sprite="' Main/_Module/ModuleData/career_system/taom_careers.xml` prints 18.

Commit: `fix(careers): v2.0.28 - cloned careers show their source's ability icon`. Stage the test, three code files, the tool, the XML. Trailers: `Not-tested: the icons on the career screen`, `Ported-from: 4d9ae0e8a514f412ba83abaf9546c567d6821ce2`.

### Step B2: the diamond tooltip's frame draws under the canvas (`95a348a60`, CareerScreen half)

A `StretchToParent` child inside a `CoverChildren` parent is the fault the ratchet counts. WP140's sed rewrote this file's brush names, so `pk` will not apply. Run this transform; it keeps every text line as it is:

```bash
python -X utf8 - <<'EOF'
import re, pathlib
p = pathlib.Path("Main/_Module/GUI/PreFabs/CareerSystem/CareerScreen.xml")
raw = p.read_bytes().decode("utf-8-sig"); nl = "\r\n" if "\r\n" in raw else "\n"
L = raw.split(nl); out = []; i = 0; n = 0
while i < len(L):
    if 'Id="DiamondTooltip"' not in L[i]:
        out.append(L[i]); i += 1; continue
    ind = re.match(r"\s*", L[i]).group(0); j = i
    while L[j] != ind + "</Widget>": j += 1
    body = L[i+1:j]
    assert body[0].strip() == "<Children>" and body[-1].strip() == "</Children>"
    frame = [k for k, l in enumerate(body) if 'Brush="Popup.Frame"' in l and "<BrushWidget" in l]
    assert len(frame) == 1
    del body[frame[0]]
    head = L[i].replace("<Widget ", "<BrushWidget ", 1)
    assert head.count(r'Sprite="StdAssets\Popup\canvas"') == 1
    head = head.replace(r'Sprite="StdAssets\Popup\canvas"', 'Brush="Popup.Frame"')
    out += [head, ind + "  <Children>",
            ind + '    <Widget DoNotAcceptEvents="true" WidthSizePolicy="StretchToParent" HeightSizePolicy="CoverChildren" Sprite="StdAssets\\Popup\\canvas">',
            ind + "      <Children>"]
    out += ["    " + l for l in body[1:-1]]
    out += [ind + "      </Children>", ind + "    </Widget>", ind + "  </Children>", ind + "</BrushWidget>"]
    i = j + 1; n += 1
assert n == 3
bom = b"\xef\xbb\xbf" if p.read_bytes().startswith(b"\xef\xbb\xbf") else b""
p.write_bytes(bom + nl.join(out).encode("utf-8"))
print("ok", n)
EOF
python -c "import xml.etree.ElementTree as E; E.parse('Main/_Module/GUI/PreFabs/CareerSystem/CareerScreen.xml'); print('parsed')"
```
Then delete `["Main/_Module/GUI/PreFabs/CareerSystem/CareerScreen.xml"] = 3,` from `Known` in `GauntletStretchInCoverRatchetTests.cs`.

**Verify**: `ok 3`, `parsed`; Filtered `GauntletStretchInCoverRatchetTests` 0 failed; Filtered `PrefabAttributeNameBindingTests` and `PrefabSpriteTests` 0 failed.

Commit: `fix(career): v2.0.28 - the tooltip's frame draws under its canvas`. Trailers: `Not-tested: the tooltip in game`, `Ported-from: 95a348a602a168e50db793d009c5685033113307`.

### Step B3: a non-finite mutation keeps the old value (`95a348a60`, MutationService half)

The simulation shows these two paths clean on `1b00d1acd`. RED: `pk 95a348a60 TAOM.Tests/Features/CareerSystem/MutationServiceTests.cs`; Filtered `MutateAbility_ResultOverflowsToInfinity_KeepsTheValueAndWarns` fails. GREEN: `pk 95a348a60 Main/Features/CareerSystem/Mutations/MutationService.cs`.

**Verify**: Filtered `MutationServiceTests` 0 failed.

Commit: `fix(career): v2.0.28 - a non-finite mutation keeps the old value`. `Ported-from: 95a348a602a168e50db793d009c5685033113307`.

Not taken from `95a348a60` (see Disposition): the ladder clause in `CareerQuestService`, `StagedSiegeHud.xml`, `FordsMatchConfig`, `match.json`, `armour-acquisition.md`.

### Part B done criteria

- [ ] Three commits; `grep -c CareerScreen TAOM.Tests/Infrastructure/GauntletStretchInCoverRatchetTests.cs` prints 0; full Test 0 failed, total = baseline + 4 (3 sprite tests, 1 mutation test).

---

## Part C: RaceAge, banner patch, nameplate provider, Mixed Formations, race test

Prechecks: `grep -c 'bonusAllowed' Main/Features/RaceAge/Models/TaomPregnancyModel.cs` prints 2 (WP114); `git ls-files TAOM.Tests/Infrastructure/HotPathSettingsProvidersTests.cs TAOM.Tests/Infrastructure/CountingMcm.cs | wc -l` prints 2 (WP123); `grep -c 'RegisteredRaces' TAOM.Tests/Core/CultureRaceConsistencyTests.cs` at least 1 (WP126); `git grep -c 'ClanPartyItemVM_GetCharacterCode_Patch.Initialize' -- Main/SubModule.cs` prints `Main/SubModule.cs:1`; else STOP.

### Step C1: pregnancy rules move into the service (`79d90c08f` and `174a7e898`, squashed)

`174a7e898` corrects a defect `79d90c08f` introduced (a clanless immortal went from 0 to an NRE, because the arguments read `hero.Clan` before the guards), so only their net goes in. These are the only two commits in that range that touch RaceAge (`git log` checked). After WP114 the pregnancy files equal the 1.5.x parent of `79d90c08f`.

RED: `git diff 79d90c08f^ 174a7e898 -- TAOM.Tests/Features/RaceAge/RaceAgeServiceTests.cs TAOM.Tests/Features/RaceAge/TaomPregnancyModelTests.cs | git apply --3way`; build fails on `CanConceive`. GREEN: the same with `Main/Features/RaceAge/IRaceAgeService.cs Main/Features/RaceAge/RaceAgeService.cs Main/Features/RaceAge/Models/TaomPregnancyModel.cs`. A conflict: STOP (WP114 left a different file).

**Verify**: Filtered `RaceAge` 0 failed; `git grep -n 'ComputeBaseChance' -- Main TAOM.Tests` prints nothing; `grep -n 'CanConceive' Main/Features/RaceAge/Models/TaomPregnancyModel.cs` is above the `hero.Clan` line.

Commit: `refactor(race-age): v2.0.28 - pregnancy rules move into the service`. Body: the guards keep their own call so no clan is read first; every input gives what it gave. Trailers: `Research: PregnancyCampaignBehavior lets a clanless hero through (1.5.x finding; the model keeps the order either way)`, `Ported-from:` `79d90c08f518e757ff629a5b69c0aca8c72a9541`, `174a7e898fff45a72675319e2dbc8ed5a933e3d3`.

### Step C2: delete `TaomHeroCreationModel` (`7983854e7`, adapted)

On v1.4.8 `DefaultHeroCreationModel.GetCharacterTemplateForOffspring` (`:346-353`) does the same. On 1.5.x the class stays only for lord kits, which 1.4.x lacks. Check first: `git grep -n 'TaomHeroCreationModel' -- Main TAOM.Tests` lists only the class file and one `Main/SubModule.cs` line; `git diff --stat 1b00d1acd HEAD -- Main/Features/RaceAge/Models/TaomHeroCreationModel.cs` is empty. Else STOP.

1. `git rm Main/Features/RaceAge/Models/TaomHeroCreationModel.cs`.
2. `Main/SubModule.cs`: delete `campaignStarter.AddModel(new TaomHeroCreationModel());`.
3. `docs/reference/gamemodel-registry.md`: delete the `TaomHeroCreationModel` row. `docs/features/offspring-race-inheritance.md`: rewrite the lines naming the override and its flow to say vanilla's `DefaultHeroCreationModel.GetCharacterTemplateForOffspring` (v1.4.8 `:346-353`) gives a son his father's template and a daughter her mother's, and `Patch13_RaceAge` still does the race work. `git grep -n 'TaomHeroCreationModel' -- docs/features docs/reference/gamemodel-registry.md docs/ai-includes` then shows no claim that the model exists (historical RCAs stay).

**Verify**: Build exit 0; full Test 0 failed, total unchanged.

Commit: `refactor(race-age): v2.0.28 - drop an offspring rule vanilla repeats`. `Save-compat: none; a GameModel holds no save data`, `Ported-from: 7983854e79d87e985bc09cc6f19defb70eecfe64`.

### Step C3: remove a patch that changes nothing (`8dbdfeb12`)

Verified on v1.4.8 (Current state). Precheck: the file's Postfix still reads `_heroAdapter?.GetClanColorInfo(character)` and returns on null (WP133 may have changed the file; if it now does something else, STOP).

1. `git rm Main/Features/BannerColorPersistence/Hooks/ClanPartyItemVM_GetCharacterCode_Patch.cs`.
2. `Main/SubModule.cs`: delete its `.Initialize(...)` line.
3. `docs/reference/harmony-patch-registry.md`: drop `ClanPartyItemVM` from the Patch23 target list and, in the GetCharacterCode postfix note, from the list (lower its count by one). `pk 8dbdfeb12 docs/features/banner-color-persistence.md`; on a conflict, keep ours and remove only that patch's row.

**Verify**: Build; Filtered `BannerColor` 0 failed. Full Test 0 failed. `git grep -n 'ClanPartyItemVM_GetCharacterCode' -- Main TAOM.Tests` prints nothing.

Commit: `refactor(banner-colors): v2.0.28 - remove a patch that changes nothing`. `Research: v1.4.8 ClanPartyItemVM.GetCharacterCode :1331-1342`, `Not-tested: a clan screen party row's hero portrait with clan colours on`, `Ported-from: 8dbdfeb125dcff6555eb00e4abac018131eec262`.

### Step C4: nameplate fade stops pinning MCM defaults (`69275212f`)

A resolve before MCM is up kept the defaults for the whole session.

RED: in `TAOM.Tests/Infrastructure/HotPathSettingsProvidersTests.cs` (WP123), with the Edit tool, add `using TAOM.Features.SettlementNameplateFade;`, `[DataRow(typeof(NameplateFadeSettingsProvider))]` in each of the two DataRow lists, and the `Probes` row:
```csharp
// Settlement nameplate fade: read per frame per visible plate.
[typeof(NameplateFadeSettingsProvider)] = (() => new NameplateFadeSettingsProvider(),
    nameof(INameplateFadeSettingsProvider.Enabled), nameof(TaomSettings.EnableNameplateFade), () => new TaomSettings()),
```
Filtered `HotPathSettingsProvidersTests`: the fade rows fail. GREEN: `pk 69275212f Main/Features/SettlementNameplateFade/NameplateFadeSettingsProvider.cs Main/Features/CompanionTactics/BattleActionBar/Hooks/Patch35_Formation_SetMovementOrder.cs docs/modding/module-dependencies.md`. (The `BattleBalance` and `NameplateRelation` rows of the 1.5.x commit are coverage only; add them too if each compiles and passes, else leave them and say so.)

**Verify**: Filtered `HotPathSettingsProvidersTests` 0 failed.

Commit: `fix(nameplate-fade): v2.0.28 - stop pinning MCM defaults at construction`. `Not-tested: fade distances after a slider change`, `Ported-from: 69275212f939569d20a30f7ebbbfcc7d6db7f925`.

### Step C5: Mixed Formations logs its cavalry gate, comments say what is true (`38e5e6274`, `35aa37439`, `35044f583`)

RED: `pk 38e5e6274 TAOM.Tests/Features/MixedFormations/FormationLayoutServiceTests.cs`; Filtered `ComputeUnitPlanePosition_CavalryGate_LogsOncePerFormationEachMission` fails. GREEN: `pk 38e5e6274 Main/Features/MixedFormations/FormationLayoutService.cs` (the context needs WP124's formation prefix; a conflict there: keep WP124's lines and add the `_cavalrySuspendLogged` field, the log inside the `RepresentativeIsCavalry` branch, and the `Clear()` in `OnMissionEnd`). `Main/Features/TaomSettings.cs`: replace the `Mixed Formations Debug Mode` `HintText` with `"Writes two diagnostic lines, the auto-assigned default layout and the cycled layout, to the TAOM log file. Nothing is shown on the HUD."` (text only, property name and default unchanged). Check first that those two `LogDebug` lines are the only uses: `grep -c 'IsDebugMode' Main/Features/MixedFormations/FormationLayoutService.cs` prints 2 (lines 124 and 154 at `1b00d1acd`), and each one guards a log call. Else leave the hint and report it. Comments: `pk 35aa37439 Main/Adapters/IFormationAdapter.cs docs/features/mixed-formations.md`; `pk 35044f583 Main/Features/MixedFormations/FormationLayoutService.cs Main/Features/MixedFormations/IFormationLayoutService.cs TAOM.Tests/Features/MixedFormations/FormationLayoutServiceTests.cs docs/features/mixed-formations.md`. A conflicted comment hunk may be dropped and listed in your hand-off; never drop code.

**Verify**: Filtered `FormationLayoutService` 0 failed. Any `HintText` localization or settings count test: full Test 0 failed. If a test pins the old hint in a strings file, STOP and report.

Commit: `fix(mixed-formations): v2.0.28 - log the cavalry gate, correct comments`. `Ported-from:` `38e5e627427d8f8bb7064d77fca37ab7a2d5945b`, `35aa37439e17db923b672e0cde1184d8b9709f23`, `35044f5832fa3e8c067a2a20f8fd91121d11df9c`.

### Step C6: the race test reads cultures through the real loader (`834e0703c`)

WP126 took `437d59111`'s blob, which differs from `834e0703c^`, so expect a conflict. `pk 834e0703c TAOM.Tests/Core/CultureRaceConsistencyTests.cs`. Resolve to: `FindRepoRoot` gone, `AllowedRaces()` as in `834e0703c` (`CultureCreationDataProvider`, whose `LoadCultures()` is on 1.4.x at `:24`), every caller through `RepoPaths.RepoPath(...)`. Keep WP126's binding-settings handling of `EveryAllowedRaceIsARealRegisteredRace`.

**Verify**: Filtered `CultureRaceConsistencyTests` shows the same passed and skipped counts as before the edit.

Commit: `test(core): v2.0.28 - race test reads cultures through the real loader`. `Not-tested: none (test only)`, `Ported-from: 834e0703c9b9563fe78c97d708b71460d27fa66e`.

### Part C done criteria

- [ ] Six commits; full Test 0 failed, total = baseline + the new `RaceAgeServiceTests` and `TaomPregnancyModelTests` count + 1 Mixed test + the fade rows (report each).
- [ ] `git diff bannerlord-1.4.5...HEAD -- Main/SubModule.cs | grep -c '^[-+] '` prints 2 for this part's commits alone (two deletions).

---

## Part D: GUI gates

Prechecks: `git ls-files TAOM.Tests/Infrastructure/PrefabSpriteTests.cs TAOM.Tests/Migration/PrefabAttributeNameBindingTests.cs TAOM.Tests/Features/CompanionTactics/BattleActionBar/BattleActionBarInputTests.cs | wc -l` prints 3; `grep -c 'internal static IEnumerable<string> ModuleFolders' TAOM.Tests/Infrastructure/PrefabExtensionBrushTests.cs` prints 1. Else STOP. These tests need the install: if they report Inconclusive, say so; never call that a pass.

### Step D1: gate brush sprites, delete `GoldIcon.xml` (`462d745ec`)

RED: `pk 462d745ec TAOM.Tests/Infrastructure/PrefabSpriteTests.cs`; Build; Filtered `EveryBrushSprite_ResolvesOrIsNamedByTheGamesOwnBrushes`. Expect it to name `ROT.Icon.Gold`'s sprite from `GoldIcon.xml`. If it names anything else, STOP and report the list. GREEN: `git grep -n 'ROT.Icon.Gold' -- Main` prints only that file; then `git rm Main/_Module/GUI/Brushes/GoldIcon.xml`.

**Verify**: Filtered `PrefabSpriteTests` 0 failed.

Commit: `fix(ui): v2.0.28 - gate brush sprites, delete an unused gold brush`. `Ported-from: 462d745ec4bb5d278a39f4d7654a86aa2cd3d04a`.

### Step D2: drop the dead `DataSource` on the action bar root (`258db5e73`)

RED: `pk 258db5e73 TAOM.Tests/Features/CompanionTactics/BattleActionBar/BattleActionBarInputTests.cs`; `TheMovieRoot_CarriesNoDataSource` fails. GREEN: in `Main/_Module/GUI/PreFabs/BattleActionBar.xml`, delete ` DataSource="{..}"` from the root `<Widget>` under `<Window>` only (the 1.4.x line differs from 1.5.x's, so do it with the Edit tool).

**Verify**: Filtered `BattleActionBarInputTests` 0 failed.

Commit: `fix(action-bar): v2.0.28 - drop the dead DataSource on the movie root`. `Ported-from: 258db5e7320a88aec25173c78a4658c241fb1e7b`.

### Step D3: the VisualDefinition gate and its dead definitions (`498d07e08`, `ed3f911eb`, `0385cc041`)

RED: their net on the gate file, against WP140's copy of the `7012783b3` blob:
```bash
T=TAOM.Tests/Migration/PrefabAttributeNameBindingTests.cs
git diff 498d07e08^ 0385cc041 -- $T | git apply --3way
```
(Only those three commits change this file in that range; `git log --no-merges --oneline 498d07e08^..0385cc041 -- $T` prints 3 lines, else STOP.) Filtered `VisualDefinitions_AndTheWidgetsThatNameThem_MatchInEveryPrefab`: fails naming the definitions.

GREEN, by hand (1.4.x's `TransitionDuration` values differ, so no `pk`):
- `Main/_Module/GUI/PreFabs/GameMenu/GameMenu.xml`: delete the `InfoPanel` `VisualDefinition` block and the `InfoPanel.Width` constant. The commented-out widget line that names it may stay.
- `Main/_Module/GUI/PreFabs/Party/PartyScreen.xml`: delete the `FormationListPanel` and `FormationSeperator` `VisualDefinition` blocks and the constants only they read: `FormationListPanel.NegativeHeight`, `Party.Formation.Seperator.Width`. Keep `FormationListPanel.Height`, `FormationListPanel.MarginTop`, `FormationClipRect.Height` and `Party.Formation.Seperator.Height` (read by others). Check each before deleting: outside comments, `grep -c '!FormationListPanel.NegativeHeight\|!Party.Formation.Seperator.Width'` must fall to 0 only after the blocks go.
- `FacGen/PreBuildCharacterSelection.xml` `LeftPanel`, if the gate still names it: delete that block the same way (WP140 touched this file; the planning scan saw it orphaned at `1b00d1acd`).
- Any other name the gate reports: STOP and report; WP131 and WP133 may have added a prefab.

**Verify**: Filtered `PrefabAttributeNameBindingTests` 0 failed; both XML files parse.

Commit: `fix(ui): v2.0.28 - remove dead visual definitions and gate them`. `Not-tested: the game menu and party screen, opened in game`, `Ported-from:` `498d07e084799de6e09c0021399b95c10f14887d`, `ed3f911eb7f6637a81fd9eed20e5aadaf876cfba`, `0385cc0418719b2d000e19632c20e7b2a44b85da`.

### Part D done criteria

- [ ] Three commits; `git ls-files Main/_Module/GUI/Brushes/GoldIcon.xml` prints nothing; `git grep -n 'Name="InfoPanel"\|Name="FormationSeperator"' -- Main/_Module/GUI/PreFabs` prints nothing; full Test 0 failed.

---

## Part E: the validator reuses its reads, reports bad roster and settlement refs, and checks a release pair

Commit-by-commit in order, because each builds on the last. WP136 transplanted these tools from `e45f6df2c` and adapted them, so hunks should apply; `tools/validate_moduledata.py` and `tools/taom_schema.py` are serial, one step at a time.

Prechecks: `git grep -c 'SCHEMA_CODE = "SCHEMA_INVALID"' -- tools/validate_moduledata.py` prints 1 line; `git ls-files docs/reference/moduledata-validator-codes.md .githooks/pre-commit tools/data_gates.py | wc -l` prints 3; `python tools/validate_moduledata.py 2>&1 | tail -3` saved as your baseline (per-code counts). Else STOP.

### Step E1: reuse file reads and the ref walk (`5f87810ed`)

RED: `pk 5f87810ed tools/tests/test_validate_moduledata.py tools/tests/test_taom_query.py`; run them, the new tests fail. GREEN: `pk 5f87810ed tools/taom_schema.py tools/taom_query.py tools/validate_moduledata.py`.

**Verify**: both test files `OK`; the validator's per-code counts equal the baseline; time a run and put both timings in the body (1.5.x measured 25.3 s to 14.5 s; do not quote that as yours).

Commit: `fix(tools): v2.0.28 - validator reuses its file reads and ref walk`. `Ported-from: 5f87810edf77ff8a55329dc7f12722be33550d14`.

### Step E2: `BROKEN_ROSTER_REF` (`3b4a406f7`)

RED: `pk 3b4a406f7 tools/tests/test_validate_moduledata.py`; `BrokenRosterRefTests` (7) fail. GREEN: `pk 3b4a406f7 tools/taom_schema.py docs/reference/moduledata-validator-codes.md`. Run `python tools/validate_moduledata.py --code BROKEN_ROSTER_REF`: 1.5.x read clean, 1.4.x is UNVERIFIED. Any finding: do NOT add the code to the hooks, do not edit data; STOP and report the list (D13). When clean, add `--code BROKEN_ROSTER_REF` beside `--code TROOP_ID_REMOVED` in `.claude/hooks/check-moduledata-validation.sh` and in `.githooks/pre-commit` (`pk 3b4a406f7 .claude/hooks/check-moduledata-validation.sh .githooks/pre-commit`; 1.4.x's hook lists more codes, so a conflict means keep ours and add the one code).

**Verify**: `test_validate_moduledata.py` `OK`, including `CommitGateCoverageTests`.

Commit: `feat(tools): v2.0.28 - BROKEN_ROSTER_REF, a misspelt EquipmentSet id`. Body: a misspelt `<EquipmentSet id>` stops every later character loading. `Ported-from: 3b4a406f702ac87475e78c4cb56280a762f3f44c`.

### Step E3: one release check for the pair, `BROKEN_SETTLEMENT_REF` (`002f8ff7e`, `334c187e4`, the check hunks of `0385cc041` and `1499aaa69`)

1. RED: `git show 0385cc041:tools/tests/test_check_release_pair.py > tools/tests/test_check_release_pair.py`; `pk 002f8ff7e tools/tests/test_validate_moduledata.py tools/tests/test_taom_query.py`; `pk 334c187e4 tools/tests/test_validate_moduledata.py tools/tests/test_taom_query.py tools/tests/test_taom_mcp_server.py`.
2. GREEN: `pk 002f8ff7e tools/taom_schema.py tools/taom_query.py tools/validate_moduledata.py`; `pk 334c187e4 tools/taom_schema.py tools/taom_query.py tools/taom_mcp_server.py tools/validate_moduledata.py`; `git show 0385cc041:tools/check_release_pair.py > tools/check_release_pair.py`.
3. **NavalDLC adaptation**: `334c187e4` removes NavalDLC from `BROKEN_ROSTER_REF`'s roster roots because 1.5.x's `SubModule.xml` lists it incompatible. Run `git grep -n -i 'naval' -- Main/_Module/SubModule.xml`. It printed nothing at planning. If still nothing: keep NavalDLC in the roots and remove the test that asserts its exclusion. If a row is there: take the hunk as written.
4. Run `python tools/validate_moduledata.py --code BROKEN_SETTLEMENT_REF`. 1.4.x is UNVERIFIED (the tool reads `spclans.xslt`, `spkingdoms.xslt`, `spcultures.xslt`, all on 1.4.x). Any finding: do not add it to the hooks; report it. Clean: add `--code BROKEN_SETTLEMENT_REF` beside `--code BROKEN_ROSTER_REF` in both hooks.
5. Docs (`pk`; on a conflict keep ours and add the rows): `docs/reference/moduledata-validator-codes.md`, `docs/features/moduledata-validation.md` from `002f8ff7e`, `0385cc041`, `1499aaa69`. `docs/reference/release-process.md`: do not take the deletions (1.4.x has no realm paragraphs for Ered Luin, Anduin Vale or Feral Orcs). Add one paragraph, your own wording from `002f8ff7e`'s hunk: TAOM and TAOM_Map ship as a pair, `python tools/check_release_pair.py <channel Modules folder>` checks it, exit 0 clean, 1 a disagreement, 2 a missing or unreadable file, 3 a crash; WP115's pin still applies. `.claude/skills/release/SKILL.md`: `pk 002f8ff7e .claude/skills/release/SKILL.md`, then `pk 0385cc041 .claude/skills/release/SKILL.md`; keep ours where it names v2.4.0 or the 1.4.x steps. `docs/modding/equipment-rosters.md`: `pk 002f8ff7e docs/modding/equipment-rosters.md`, or skip on conflict and say so.

**Verify**: `python -m unittest tools.tests.test_check_release_pair tools.tests.test_validate_moduledata tools.tests.test_taom_query tools.tests.test_taom_mcp_server`: `OK`. Tool tests no new failure. `python tools/check_release_pair.py` with no argument exits non-zero and never 0. The maintainer runs it on a real channel (see In-game checks owed).

Commit: `feat(tools): v2.0.28 - one release check for the TAOM and TAOM_Map pair`. `Not-tested: a real release channel folder`, `Ported-from:` `002f8ff7e16e66bdfb796958dc7e13ce83b7af64`, `334c187e4ba93a27247b698ad5309b876f2e9513`, `0385cc0418719b2d000e19632c20e7b2a44b85da`, `1499aaa697fb216c951b41a1b34ee19ba521a8f2`.

### Part E done criteria

- [ ] Three commits; `git grep -c 'BROKEN_ROSTER_REF' -- tools/taom_schema.py` and `BROKEN_SETTLEMENT_REF` print at least 1; `python tools/validate_moduledata.py` no new ERROR vs the WP101 baseline.

---

## Part F: hooks, gates and routing

### Step F0: prechecks and baseline

`git rev-parse HEAD:.claude/hooks/block-no-verify.sh | cut -c1-9` must print `0bd40eb59`, the blob at `434d1d795` and at `e45f6df2c` (WP143 F5 lands it). Different: run `git diff e45f6df2c HEAD -- .claude/hooks/block-no-verify.sh` and STOP. `git ls-files tools/check_commit_msg.py tools/data_gates.py TAOM.Tests/Infrastructure/ServiceSignatureGateTests.cs tools/tests/test_check_commit_msg.py tools/tests/test_data_gates.py | wc -l` prints 5. Run the Hooks command and record the failure count.

### Step F1: `block-no-verify`'s raw fallback (`8afe46d39`, `000bcf1d1`, `0385cc041`, `931d59084`, `ff2114556`)

`8afe46d39` alone is wrong: `000bcf1d1` says it lost eight refusals. Only the net goes in.

RED: `git diff e45f6df2c 4d1a9105a -- tools/test_hooks.sh | git apply --3way`. If `.claude/skills/context-budget/scan.sh` is 1.4.x's scanner and `tools/context_budget_scan.sh` does not exist (WP138 kept it there), edit the new section 8 rows with the Edit tool so each `tools/context_budget_scan.sh` reads `.claude/skills/context-budget/scan.sh` and its `cp` target folder exists. GREEN: `git checkout 4d1a9105a .claude/hooks/block-no-verify.sh`; `pk ff2114556 .claude/hooks/config-protection.sh` (the range diff above also brings its two `docs/./adrs` and `docs/x/../adrs` rows in section 7g, which fail until this hunk is in).

Scanner: `git diff e45f6df2c 4d1a9105a -- tools/context_budget_scan.sh | sed 's#tools/context_budget_scan.sh#.claude/skills/context-budget/scan.sh#g' | git apply --3way`. If that conflicts, restore the scanner (`git checkout HEAD .claude/skills/context-budget/scan.sh`), remove the section 8 scan rows from `test_hooks.sh`, and list it in your hand-off as not ported. The hook fix must not wait for it.

**Verify**: Hooks no new failure; with a payload file written by the Write tool (not a command line), `{"tool_name":"Bash","tool_input":{"command":"git commit -n -m x"}}`, `bash .claude/hooks/block-no-verify.sh < <file>; echo rc=$?` prints `rc=2`, and for `git commit -m "mention -n only"` prints `rc=0`.

Commit: `fix(hooks): v2.0.28 - raw fallback refuses again, scan finds its memory`. Body: the raw path runs only past the reader's limit, so over-blocking is the safe direction; `config-protection` normalises `docs/./adrs`. `Ported-from:` the five 40-hex SHAs `8afe46d3926dbdf108084cf4e2bec6aaa826cc31`, `000bcf1d152f69ea49149029b6258c41f52f89e5`, `0385cc0418719b2d000e19632c20e7b2a44b85da`, `931d59084b4e5b571628d1baf32dd05680b5d0d4`, `ff21145567ce5c0c6110dfe8c87320d510456018`.

### Step F2: what Codex found in the gates (`ff2114556`, the rest)

RED: `pk ff2114556 TAOM.Tests/Infrastructure/ServiceSignatureGateTests.cs tools/tests/test_check_commit_msg.py tools/tests/test_data_gates.py tools/tests/test_doc_graph_ratchet.py tools/tests/test_lint_rule_drift.py tools/tests/test_validate_moduledata.py`. GREEN: `pk ff2114556 tools/check_commit_msg.py tools/check_doc_graph_ratchet.py tools/data_gates.py tools/lint_docs.py tools/taom_schema.py tools/validate_moduledata.py`. The test `tools/tests/test_lint_rule_drift.py` exists only if WP143 brought it: drop that path if `git ls-files` prints nothing. The "release baseline test" hunk needs a v-tag; if it fails only because 1.4.x has no reachable new tag, adapt it to read `v2.0.28` and say so. The new generic-service check may find 1.4.x services: list them; if any is a real violation, STOP.

**Verify**: Filtered `ServiceSignatureGateTests` 0 failed; tool tests no new failure; `python tools/check_doc_graph_ratchet.py` exit as before; Hooks no new failure.

Commit: `fix(tools): v2.0.28 - gate holes from the 1.5.x review`. Body lists the fixes that landed: commit-msg reads the index only, `data_gates` handles a non-ASCII path, generic services are scanned, `TROOP_ID_REMOVED` reads a single-quoted id, the dotnet lint rejects a named ModuleId, the doc ratchet refuses an empty corpus. `Ported-from: ff21145567ce5c0c6110dfe8c87320d510456018`.

### Step F3: the NuGet secret rule (`1011a4ef6`, `285c5a1e8`, `0385cc041`, `931d59084`, `ff2114556`)

RED: `git diff e45f6df2c 4d1a9105a -- tools/tests/test_audit_repo_secrets.py | git apply --3way`. GREEN: `git diff e45f6df2c 4d1a9105a -- tools/audit_claude_config.py | git apply --3way` (only a new rule; 1.4.x's file is shorter, so after WP138 it should take it). **Verify**: `python -m unittest tools.tests.test_audit_repo_secrets`: `OK`; `python tools/audit_claude_config.py` has no new finding. Never print a value it finds.

Commit: `fix(tools): v2.0.28 - secret-nuget-cleartext reads either order`. `Ported-from:` `1011a4ef6786ffee68d2f716a8e206145c6326ac`, `285c5a1e8631fe30ce2bc69c42ca7d96f57cb571`, `0385cc0418719b2d000e19632c20e7b2a44b85da`, `931d59084b4e5b571628d1baf32dd05680b5d0d4`, `ff21145567ce5c0c6110dfe8c87320d510456018`.

### Step F4: route the data gates and provenance checks (`b935b830a`)

`.ai/routing.json` and `.ai/verification.md` differ on 1.4.x (WP110 and WP143 edited them). Edit by hand: `moduledata` lane `checks` gains `"data-gates"`; `assets-localization` gains `"data-gates"` and `"provenance"`; `native-dependencies` gains `"provenance"` and the patterns `git show b935b830a:.ai/routing.json` lists. Do this only for check ids `.ai/verification.md` defines as a table row: `grep -c '^| .data-gates. ' .ai/verification.md` and `grep -c '^| .provenance. ' .ai/verification.md` each print 1; for one that prints 0, drop that id and report. A bare word count is not enough: the file's prose already names `provenance`. Verification line: `provenance` checks the register's form, not intent.

**Verify**: `python -c "import json;json.load(open('.ai/routing.json'));print('ok')"`; `python tools/reviewctl.py lint` exit 0.

Commit: `docs(ai): v2.0.28 - route the data gates and provenance checks`. `Ported-from: b935b830aa8a4b3c7622b129d9622e334eaff769`.

### Part F done criteria

- [ ] Up to four commits; `git rev-parse HEAD:.claude/hooks/block-no-verify.sh | cut -c1-9` prints `4030adfaa`; Hooks no new failure vs F0; `/security-scan` recommended to the integrator (these are hook edits).

---

## Part G: tools, API snapshot, docs, RCA

Each step: tests first through `pk`, then the tool; **Verify** is the named test module `OK` plus Tool tests no new failure. Prechecks: `git ls-files tools/tests/test_process_faction_map.py tools/tests/test_ranged_ladder.py tools/tests/test_add_map_villages.py tools/tests/test_audit_mount_parity_strict.py tools/tests/test_loc_sources.py tools/tests/test_english_only_writers.py | wc -l` prints 6; else name the missing file and its WP (WP136, WP137, WP141, WP135) and STOP.

| Step | Source | Paths for `pk` | Subject (`<type>(tools): v2.0.28 - ...`) | Notes |
|---|---|---|---|---|
| G1 | `b7fefdae6`, `b6dc611dd` | `b7fefdae6`: `tools/tests/test_process_faction_map.py`, then `tools/assemble_faction_map.py tools/process_faction_map.py docs/features/new-factions-misty-mountains-lindon.md docs/modding/file-catalogue.md`; `git rm tools/make_new_factions_playable.py`. `b6dc611dd`: `tools/assemble_faction_map.py tools/border_match.py tools/process_faction_map.py`. Then `pk f9a8bdebc TAOM.Tests/Features/FactionMap/FactionMapDataTests.cs`, Build, Filtered `FactionMapDataTests` 0 failed | `fix: map tools stop writing factions.json` | 1.4.x's `factions.json` already reads as the pin expects. First confirm `git grep -n make_new_factions_playable -- tools docs` shows nothing that still needs it. `REGION_META` must leave `border_match.py:24` |
| G2 | `c30c4e618`, `ad8f13d00`, `b6dc611dd` | `c30c4e618`: `tools/tests/test_add_map_villages.py tools/tests/test_ranged_ladder.py`, then `tools/add_map_villages.py tools/rebalance_ranged_ladders.py docs/features/ranged-ladders.md`. `ad8f13d00`: `tools/tests/test_add_map_villages.py tools/add_map_villages.py`. `b6dc611dd`: `tools/tests/test_ranged_ladder.py tools/rebalance_ranged_ladders.py` | `fix: village writer checks scenes, ladder html opt-in` | Never run the tool with `--apply`. Run `python tools/add_map_villages.py --check` and report any dead scene on the laptop's install; no data edit. No `add_map_fortifications`, `realm_troops` or `test_live_map_writers_main` on 1.4.x unless `git ls-files` finds them |
| G3 | `44d4dba46`, `570fa0e85`, `0385cc041`, `931d59084` | `git diff e45f6df2c 4d1a9105a -- tools/rebuild_translation_files.py tools/tests/test_english_only_writers.py \| git apply --3way`; `git show 4d1a9105a:tools/tests/test_rebuild_name_tags.py > tools/tests/test_rebuild_name_tags.py` if absent, else `pk 570fa0e85` and `pk 0385cc041` on it; `pk 44d4dba46 tools/tests/test_loc_sources.py tools/tests/test_translate_batching.py tools/translate_with_claude.py`; docs `pk 44d4dba46` and `pk 570fa0e85` on `docs/localization/TRANSLATOR_GUIDE.md .claude/skills/localize/SKILL.md` | `fix: the translation rebuild writes only on --apply` | Never run the rebuild with `--apply` and never run the translator (paid). `test_rebuild_name_tags.py`'s tag cases need `_loc_sources` rows WP135 kept; a test for a 1.5.x-only language source: drop that test and report it |
| G4 | `1011a4ef6`, `b6dc611dd` | `tools/tests/test_audit_mount_parity_strict.py tools/audit_mount_parity.py` from each | `fix: mount parity gains a war ram section that can fail` | Also `pk 1011a4ef6 docs/ai-includes/completion-workflow.md` if `git grep -c 'Sweep docs for the facts' -- docs/reviews/lessons/misc.md` finds the lesson (WP142); else leave the file and report it. `fbx_remap_materials.py` and `skeleton_hit_capsules.py` are absent on 1.4.x: skip |
| G4b | `53cf195ac` (tool half) | RED: `git show 53cf195ac:tools/tests/test_verify_mount_assets.py > tools/tests/test_verify_mount_assets.py`; it fails on `vma.CLIP_GUID`. GREEN: `git show 53cf195ac:tools/verify_mount_assets.py > tools/verify_mount_assets.py`, then with the Edit tool delete the `# Dire wolf (#1081)` comment and the whole `"dire_wolf"` entry from `CREATURES` | `fix: mount asset gate matches exact clip names` | First: `git diff --stat 1b00d1acd HEAD -- tools/verify_mount_assets.py` is empty (no WP touches it), else STOP. Then `grep -c 'dire_wolf' tools/verify_mount_assets.py` prints 0. The fix: a byte scan let `x_trot` hide inside `x_trot_strafe`, an empty usage set passed, and `--game` never worked. Generic `untagged_ok` stays; its test uses a synthetic config. Run `python tools/verify_mount_assets.py elephant` and report the result: 1.5.x found elephant and mumakil stand clips failing this stricter gate, and a FAIL here is a report, never a data edit |
| G5 | `ca1bc5f2e` | `git show ca1bc5f2e:tools/assert_auto_ignore.py > tools/assert_auto_ignore.py`, same for `tools/tests/test_assert_auto_ignore.py`; `pk ca1bc5f2e docs/reference/lotrlome-warg-changes.md docs/reference/provenance-register.md` | `feat: click through engine asserts without a stall` | Clean on `1b00d1acd` except `tools/README.md` (G7). Kit runs for WP121 want it; do not run it here. Provenance row stays `uncleared`, license UNKNOWN, as the source says: the integrator should know before shipping the tool to anyone (it is a dev tool, never packaged) |

Trailers for G1 to G5: `Not-tested: none (offline tool)` (G5: `a live Modding Kit assert`), one `Ported-from:` per source, using these 40-hex SHAs: `b7fefdae60a1b8debff14bce8877b480f32572e1`, `b6dc611dd014d6e3b5bddb899e23eccabe8dbb99`, `f9a8bdebc7a2996c3ffca9533d75ff5518a12b27`, `c30c4e618e0d906569fa2f3a46247dfda2d2d40f`, `ad8f13d00023e0c24d09002fd7d5570d31f9aff2`, `44d4dba4697207fe0e33fd4a83c6b9595d0b933d`, `570fa0e8526400a3bbc51538fe1d630e88ebe0f3`, `0385cc0418719b2d000e19632c20e7b2a44b85da`, `931d59084b4e5b571628d1baf32dd05680b5d0d4`, `1011a4ef6786ffee68d2f716a8e206145c6326ac`, `53cf195ac7e59854fb4a4fd57657a058be808101`, `ca1bc5f2e62de5263df40de74a1a5b39814a3116`.

### Step G6: API snapshot (`333e74907`, regenerated, not picked)

Only after Part C. WP137 made the snapshot reproducible. Run `pwsh tools/snapshot_api_surface.ps1`, then `pwsh tools/snapshot_api_surface.ps1 -Check`. Expect the `ClanPartyItemVM_GetCharacterCode_Patch` row and the `TaomHeroCreationModel` override row to go. The tool's self-label must read v1.4.8.

**Verify**: `-Check` reproduces; `git diff --stat` lists only `docs/reference/taleworlds-api-snapshot/*`. Other lines changing: list them; if one is not explained by a merged WP, STOP.

Commit: `docs(api-snapshot): v2.0.28 - regenerate after two deletions`. `Ported-from: 333e7490784f2fd17d81f4b61a8a62e1977a659b`.

### Step G7: docs that the ports make true, the RCA, its lessons, the catalog (`35044f583`, `1d5d0a561`, `931d59084`, `3bb89aff5`, `1499aaa69`, `0385cc041`)

1. `docs/reference/engine/mission-and-missionbehavior-lifecycle.md` (claim re-checked on v1.4.8, see Current state). Only if the file holds the sentence: `grep -c 'not traced here' <file>` prints 1. Replace the two lines from `The engine's own call is the auto-finish` to `is open.` with: `The engine's own call is the auto-finish at the end of \`SetupTeams\` (\`:181-184\`), and the controller's own \`OnMissionTick\` runs \`SetupTeams\` (\`:83-96\`, v1.4.8), so that call sits inside the tick loop.` Do not take the rest of `1d5d0a561`'s text: it cites a v1.5.6 line and a 2026-11-04 mission list that were not re-checked here. Prints 0: skip and report.
2. Career docs, 1.4.x numbers: `pk 35044f583 docs/features/gui-sprite-system.md docs/features/career-cc-selection.md docs/features/career-system.md` only as a guide. After applying, every `42` in the changed lines must read `18`, and any line naming the count of careers must match 1.4.x. If a hunk conflicts, write the fact by hand: the VM builds `CareerSystem\Abilities\{AbilitySprite}`, the `ability_sprite` attribute when set, else `ability_template_id`; 18 cloned careers name their source's icon.
3. `pk 35044f583 docs/features/banner-color-persistence.md docs/features/offspring-race-inheritance.md docs/features/elephant.md`; `pk 69275212f docs/features/elephant.md`; on a conflict keep ours and hand-edit only what Parts C and G made false. Skip `party-templates.md`, `recipe-*`, `ered-luin.md`, `file-catalogue.md`'s template lines and every generator comment (they describe the template removal 1.4.x does not take).
4. RCA and lessons (accepted beyond the per-port rule: WP142 has already merged, and nothing else will bring these). `git show 4d1a9105a:docs/reviews/rca-follow-ups-2026-11-04.md > docs/reviews/rca-follow-ups-2026-11-04.md`, then add under its title: `> 1.4.x note (plan 145): this is the 1.5.x record, kept for its reasoning. Fords, Seat and co-op rows do not apply on this line; Fords check 12 and WargRageLog are not here.` Append to `docs/reviews/lessons/gamemodels-services.md`, `state-lifecycle-save.md` and `build-tooling-workflow.md` the entries `git show 931d59084 --format= -- <that file>` adds, each only if `git grep -c '<its heading>' -- <file>` prints 0.
5. `tools/README.md`: add or correct rows for `check_release_pair.py`, `assert_auto_ignore.py`, the rebuild's `--apply` and `--module`, the village writer's scene check; remove the `make_new_factions_playable.py` row. Use the wording of `git show 4d1a9105a:tools/README.md` for those rows only. `git diff --check` on it stays clean; no `bannerlord-1.5.x` paths.

**Verify**: Docs no new finding in touched files; `python tools/lint_docs.py` shows no new dead link; Dash check `0` for lines you wrote. Lessons appended text carrying a long dash from the source: rewrite it.

Commit: `docs: v2.0.28 - docs follow the late 1.5.x fixes, RCA and lessons`. `Ported-from:` `1d5d0a5614306e475ea8ffd3a05503325cc6dba4`, `35044f5832fa3e8c067a2a20f8fd91121d11df9c`, `931d59084b4e5b571628d1baf32dd05680b5d0d4`, `3bb89aff5922b27e988fc41d06558d306fbecbdc`, `1499aaa697fb216c951b41a1b34ee19ba521a8f2`, `0385cc0418719b2d000e19632c20e7b2a44b85da`.

### Part G done criteria

- [ ] `git ls-files tools/make_new_factions_playable.py` prints nothing; `git grep -c 'REGION_META' -- tools/border_match.py` finds nothing; `git grep -c 'CLIP_GUID' -- tools/verify_mount_assets.py` prints at least 1; `pwsh tools/snapshot_api_surface.ps1 -Check` reproduces; Tool tests no new failure.

---

## Disposition of every pack commit (62)

Vocabulary from `data/classified.jsonl`. "Part" names where the port lands; "WP" names what it depends on.

| SHA | Subject | Verdict | Reason / where |
|---|---|---|---|
| `8a7725d94` | docs: build contracts for two commander features | SKIP-FEATURE | Seat of Seeing, Marshal's Word specs |
| `a8aac6399` | docs: tidy two old records | SKIP-BOOKKEEPING | One mod name in an old investigation; audit evidence absent on 1.4.x |
| `f2ee9a16f` | feat(seat): a commander's view | SKIP-FEATURE | New feature. Its warg swap hunk serves only the Seat. Its `harmony-patches.md` teardown paragraph cites a 1.5.x line; WP107 already recorded it as not ported |
| `1d5d0a561` | docs(commander): correct both build contracts | PORT-PARTIAL | G7: one engine-doc claim, re-checked on v1.4.8 (WP144). Specs skipped |
| `8b041f03e` | feat(seat): a field trial | SKIP-FEATURE | Its lessons concern a runner absent on 1.4.x |
| `5f87810ed` | fix(tools): validator reuses its file reads | PORT | E1 (WP136) |
| `8afe46d39` | fix(hooks): raw fallback stops over-blocking | PORT | F1, net only: `000bcf1d1` corrects it (WP143) |
| `462d745ec` | fix(ui): gate brush sprites | PORT | D1 (WP136) |
| `b935b830a` | docs(ai): route the data gates | PORT | F4 (WP143) |
| `3b4a406f7` | feat(tools): BROKEN_ROSTER_REF | PORT | E2 (WP136) |
| `b7fefdae6` | fix(tools): map tools stop writing factions.json | PORT | G1 (WP137, WP141) |
| `c30c4e618` | fix(tools): realm tools refuse named weapons, dead scenes | PORT-PARTIAL | G2 (WP141): village scene check, ladder html. `realm_troops.py`, `add_map_fortifications.py` absent |
| `c31a7ef69` | fix(tools): runners try a crash dump | SKIP-1.5-ONLY-FIX | `run_fords_autorun.py`, `run_long_defeat.py` absent, no plan brings them |
| `44d4dba46` | fix(tools): loc tools read one source list | PORT | G3 (WP135) |
| `1011a4ef6` | fix(tools): four small tool follow-ups | PORT-PARTIAL | F3 NuGet rule, G4 war ram section, workflow step. `fbx_remap_materials.py`, `skeleton_hit_capsules.py` absent |
| `53febe1a1` | test(coop): gate co-op presence reads | SKIP-FEATURE | D7: co-op is unsupported on 1.4.x; only two reads at `1b00d1acd`; the allowlist names 1.5.x files |
| `834e0703c` | test(core): race test reads cultures through the real loader | PORT | C6 (WP126) |
| `498d07e08` | fix(ui): remove dead visual definitions | PORT | D3 (WP140); the same three are dead on 1.4.x |
| `258db5e73` | fix(action-bar): drop the dead DataSource | PORT | D2 (WP130) |
| `c59a2b00c` | refactor(ui): one strict hex colour parser | SKIP-1.5-ONLY-FIX | Both callers (`RaceAbilities`, `RealmBorders`) absent on 1.4.x; an unused helper would be YAGNI |
| `f9a8bdebc` | test(faction-map): pin game_faction | PORT | G1 (WP136); both values already hold |
| `7983854e7` | refactor(race-age): drop an offspring rule | PORT | C2, adapted: the whole class goes on 1.4.x |
| `8dbdfeb12` | refactor(banner-colors): remove a patch | PORT | C3 (WP133); verified on v1.4.8 |
| `69275212f` | fix(nameplate-fade): stop pinning MCM defaults | PORT | C4 (WP123) |
| `32aeb9e10` | fix(armour-ladder): own journal per quest | SKIP-1.5-ONLY-FIX | `Main/Features/ArmourAcquisition` absent; WP129 confirms the ladder is 1.5.x-only |
| `79d90c08f` | refactor(race-age): pregnancy rules move | PORT | C1, squashed with `174a7e898` (WP114) |
| `4d9ae0e8a` | fix(careers): cloned careers' ability icon | PORT | B1, adapted: 18 careers, not 42 (WP129) |
| `17b071c7a` | fix(warg): rider who leaves a raging warg | PORT | A, end state (WP124, WP130) |
| `002f8ff7e` | feat(tools): one release check for the pair | PORT | E3 (WP136) |
| `14fcc5f33` | chore(data): remove 46 templates and 22 rosters | SKIP-CONTENT | D13: dead rows, no defect, most realms absent; not verified against 1.4.x's Armory and map |
| `333e74907` | docs(api-snapshot): regenerate | PORT-PARTIAL | G6: regenerated here, not picked (WP137) |
| `35aa37439` | docs(mixed-formations): correct three stale claims | PORT-PARTIAL | C5 (WP124). `commit-gates.md` timing is a 1.5.x measure: skipped |
| `7a21239dc` | fix(warg): a rage always closes | PORT | A |
| `174a7e898` | fix(race-age): keep the guards before the clan read | PORT | C1 |
| `000bcf1d1` | fix(hooks): raw fallback refuses again | PORT | F1 |
| `ad8f13d00` | fix(tools): map writers read the live row's scenes | PORT-PARTIAL | G2: village half (WP141); fort tool absent |
| `570fa0e85` | fix(tools): translation rebuild writes only on --apply | PORT | G3 (WP135) |
| `91185bdc5` | fix(tools): a crash dump is judged by its file | SKIP-1.5-ONLY-FIX | `run_fords_autorun.py` absent |
| `b6dc611dd` | fix(tools): small tool fixes | PORT-PARTIAL | G1, G2, G4. Art tool hunks absent |
| `334c187e4` | fix(tools): settlement refs in XSLT, skip comments | PORT | E3, NavalDLC hunk conditional (WP136) |
| `285c5a1e8` | fix(tools): NuGet rule | PORT | F3 |
| `ed3f911eb` | test(gates): close two holes | PORT-PARTIAL | D3: prefab half (WP140). Co-op half skipped (D7) |
| `17b738aa6` | chore(data): remove unbound rosters | SKIP-CONTENT | D13, as `14fcc5f33` |
| `35044f583` | docs: correct false statements | PORT-PARTIAL | C5, G7. Template, realm and generator lines skipped |
| `1499aaa69` | docs(tools): catalog rows | PORT-PARTIAL | E3, G7. `test_realm_troops.py` absent |
| `a4317fd8b` | feat(marshal): orders in turn | SKIP-FEATURE | New feature. Patch141 `PatchShieldPolicy` entry is its own |
| `aaff9477a` | feat(seat): a cursor and a kept panel | SKIP-FEATURE | New feature |
| `4a7b7164d` | fix(warg): a new rider closes the rage | PORT | A. `CoopMissionSeamPinTests` hunk skipped (D7) |
| `0385cc041` | fix(tools): last review fixes | PORT-PARTIAL | D3, E3, F1, F3, G3, G7. MCP-doc and count hunks taken only where the row exists |
| `931d59084` | docs(reviews): RCA, 59 lines closed | PORT-PARTIAL | A (one line), F1, F3, G3, G7: RCA plus three lessons. The 40 other RCA row edits and two prompts skipped |
| `5976636cf` | chore(snapshot): record Patch132 | SKIP-1.5-ONLY-FIX | Patch132 (dwarven armour icon, 1.5.x) absent; G6 regenerates 1.4.x's own |
| `b7a3ac17b` | feat(fords-of-war): towers throw | SKIP-FEATURE | Fords |
| `95a348a60` | fix(career): ladder, gate, HUD, income | PORT-PARTIAL | B2, B3 (WP129, WP140). Ladder clause (no ladder here; WP129's `IsFormerPlayerHeroQuest` keeps its career check), `StagedSiegeHud.xml` and Fords income skipped |
| `316b09529` | docs(reviews): 14 RCA decisions | SKIP-BOOKKEEPING | 1.5.x RCA bookkeeping; its test row is Fords |
| `38e5e6274` | fix(warg): rage timeout, Mixed logs its gate | PORT-PARTIAL | A, C5. Mordor rosters skipped (D13) |
| `ff2114556` | fix(tools): what Codex found | PORT | F1, F2, F3 (WP116, WP136, WP138, WP143) |
| `3bb89aff5` | docs(reviews): Codex passes, Mike's answers | PORT-PARTIAL | G7: only through the RCA blob; prompts skipped |
| `53cf195ac` | feat(dire-wolf): ride dire wolves | PORT-PARTIAL | G4b: only the `verify_mount_assets.py` gate fix and its test, minus the wolf's `CREATURES` entry (the 1.4.x blob equals this commit's parent, no WP touches it). All else is the wolf: `WargAttackService`, `WargConfig` and `audit_mount_parity.py` hunks, its adapter method, troops, resources, its lessons. `rageAttacks` comes from `38e5e6274` |
| `b42b3d1de` | feat(long-defeat): muster | SKIP-FEATURE | Long Defeat |
| `ca1bc5f2e` | feat(tools): click through engine asserts | PORT | G5. Dev tool, clean but `tools/README.md` |
| `dba8f886e` | feat(long-defeat): speed controls | SKIP-FEATURE | Long Defeat. Its one `git-and-commits.md` clean-up rule is a maintainer process note, not taken here |
| `4d1a9105a` | docs(long-defeat): proof runs | SKIP-FEATURE | Pin; Long Defeat |

43 ported or partly ported, 19 skipped.

## Test plan

- **New C# tests**: `WargRageFormationTests` (grows to 20 methods, source pins; `Agent` is sealed), 3 in `WargTreeNodeInjectionTests`, `CareerAbilitySpriteTests` (3), `MutateAbility_ResultOverflowsToInfinity_KeepsTheValueAndWarns`, the RaceAge tests of C1, the fade rows, `ComputeUnitPlanePosition_CavalryGate_LogsOncePerFormationEachMission`, `EveryBrushSprite_ResolvesOrIsNamedByTheGamesOwnBrushes`, `TheMovieRoot_CarriesNoDataSource`, `VisualDefinitions_AndTheWidgetsThatNameThem_MatchInEveryPrefab` and its 5 helper tests, the Faction pin (2 rows). The hand-off gives the measured total.
- **New Python tests**: `test_check_release_pair.py`, `BrokenRosterRefTests`, the settlement ref tests, `test_rebuild_name_tags.py`, `test_assert_auto_ignore.py`, `test_verify_mount_assets.py`, new `test_hooks.sh` rows.
- **Structurally untestable** (name in `Not-tested:`): a live rage in battle, Gauntlet rendering, a real release channel, a live Kit assert.
- **Verification**: Test `Failed: 0`; 3 skips remain the WP101 set unless a merged WP recorded a change.

## Done criteria

ALL must hold for the parts you ran; report which parts you ran:

- [ ] Build exit 0; Test exit 0, 0 failed.
- [ ] Each part's done criteria above.
- [ ] `git log --format=%s bannerlord-1.4.5..HEAD | grep -vc ': v2.0.28 - '` prints 0, and `git log --format=%s bannerlord-1.4.5..HEAD | awk 'length > 72' | wc -l` prints 0.
- [ ] `git log --format=%B bannerlord-1.4.5..HEAD | grep -c 'Co-Authored-By\|v2.0.36'` prints 0.
- [ ] `git diff --name-only bannerlord-1.4.5...HEAD | grep -c 'CHANGELOG.md\|plans/README.md\|^Client/\|settings.json\|Directory.Build.props\|docs/adrs/'` prints 0.
- [ ] `git diff --name-only bannerlord-1.4.5...HEAD | grep -c 'equipmentsets/\|taom_partyTemplates.xml'` prints 0 (D13).
- [ ] Python: `python -m unittest discover -s tools/tests -t .` no new failure; Hooks no new failure; Data no new ERROR vs the WP101 baseline.

## STOP conditions

- A precheck prints anything but the stated value: name the WP and stop that part. Other parts may go on.
- `git diff e45f6df2c 4d1a9105a` on a Part A "clean range" file conflicts (WP124 or WP130 left another shape).
- A `Coop`, `Fords`, `Seat` or `Client` reference is needed to compile anything here. It means a wrong hunk: back it out, do not bring the type.
- `BROKEN_ROSTER_REF` or `BROKEN_SETTLEMENT_REF` reports 1.4.x data: report it; no data edit, no hook row (D13).
- `EveryBrushSprite_...` or the VisualDefinition gate names anything other than this plan lists (D1, D3).
- `EveryShippedCareer_AbilitySprite_IsRegisteredInTheSpriteData` names a career outside the 18.
- `ServiceSignatureGateTests` finds a real ADR-007 violation after F2's generic scan.
- A step seems to need `IoC.cs`, `TAOM.csproj`, `settings.json`, a language file or a paid tool.
- A commit hook blocks twice for the same reason; a verification fails twice after a reasonable fix.
- `taom-src` reports a version other than v1.4.8: treat every engine fact here as unverified and run `/engine-bump` through the integrator.

## In-game checks owed (maintainer)

Log: newest `<game>\bin\Win64_Shipping_Client\Logs\taom_debug_*.log` (`FileLogger.cs`), where `<game>` is `BANNERLORD_GAME_DIR`.

1. **Warg battle (Part A).** A field battle with wargs under AI riders, long enough for rages. Pass: no `Exception running the behavior tree` line; riders back in formation after a rage (watch one, or a rider who dismounts mid-rage). Rages end sooner than before: a rider who chases out of reach is released about 6 s after the last attack. Say if this feels wrong; it is a visible behaviour change.
2. **Career screen (B1, B2).** A `goblin_troll_driver` or `falathrim_mariner` career shows an icon. Hover a diamond: the tooltip's text is readable, with the frame under the canvas.
3. **Clan screen (C3).** Clan colours on: a party row's hero portrait keeps clan colours.
4. **Lords' children (C2).** A child born in a campaign takes the same-sex parent's race and look.
5. **Nameplates (C4).** Change a fade slider in MCM, on the map: fade follows it, after a clean launch too.
6. **Game menu and party screen (D3).** Both open and draw as before. The party screen's formation area in particular.
7. **Release (E3).** `python tools/check_release_pair.py <channel Modules folder>` on a real channel: exit 0, or read what it reports.

## Maintenance notes

- A reviewer should probe: `CleanIfEnemyDied` (each of the four closes with a panicked, dead and replaced rider); the Part C deletions (`TaomHeroCreationModel`: nothing else assumed an instance of it); `git diff` of `CareerScreen.xml` (three identical blocks, text untouched); the NavalDLC decision in E3. Recommend `/deep-review`, `/verify` and, after Part F, `/security-scan` to the integrator; executors cannot invoke skills.
- `check_release_pair.py` was proved only on fixtures on 1.5.x too. Its `LANDLESS_CULTURE` and settlement reads on a real 1.4.x channel are the first real run.
- Deferred, on purpose: `14fcc5f33`'s template removal. If a later plan wants less XML, it must verify on 1.4.x's own Armory and TAOM_Map that nothing binds a row, and change the generators together.
- `WargRageLog` (per-rage counters) is not on 1.4.x. If rage-formation trouble is reported again, it is the instrument to port, minus its Fords report hook.
- Whether a mount's `IsPlayerControlled` is ever true is unverified on both lines; the player branch keeps its old shape.
- Commits newer than `4d1a9105a` are unclassified. A later sync starts there.
