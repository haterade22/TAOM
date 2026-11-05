# Plan 130: Fix banner bearers, cavalry knockdown, Custom Battle combat, warg riders, the battle action bar, the dread aura and Dunland formations on 1.4.x

> **Executor instructions**: Follow this plan step by step. Run every verification command and
> confirm the expected result before moving on. If anything in "STOP conditions" occurs, stop and
> report; do not improvise. Do not edit `plans/README.md`: every WP branch shares it, so the
> integrator records this plan's status and test counts when merging. Put the numbers the hand-off
> report asks for in your final message instead.
>
> **Drift check (run first, Step 0.3)**:
> `git diff --stat 1b00d1acd..HEAD -- Main/SubModule.cs Main/Features/TaomSettings.cs Main/Features/BannerBearers Main/Features/CombatMechanics Main/Features/CompanionTactics Main/Features/DreadAura Main/Features/Warg Main/Features/DevConsole/Cheats/MissionSpawnCheats.cs Main/_Module/ModuleData/combat_mechanics Main/_Module/ModuleData/dread_aura Main/_Module/ModuleData/troops/troops_dunland.xml Main/_Module/GUI/PreFabs/BattleActionBar.xml TAOM.Tests/Features/BannerBearers TAOM.Tests/Features/CombatMechanics TAOM.Tests/Features/CompanionTactics TAOM.Tests/Features/DreadAura TAOM.Tests/Features/Warg TAOM.Tests/Features/DevConsole TAOM.Tests/Features/MissionPerf TAOM.Tests/Infrastructure TAOM.Tests/Composition docs/features/banner-bearers.md docs/features/combat-mechanics.md docs/features/companion-tactics.md docs/features/dread-aura.md docs/features/warg-combat.md docs/modding/configs-factions-and-world.md docs/reference/gamemodel-registry.md .claude/rules/gamemodels.md .claude/rules/gui-ui.md .claude/rules/csharp-patterns.md .ai/scopes.md`
> Earlier WPs are expected to have changed several of these (WP104/105 `SubModule.cs`, `RepoPaths`,
> `TAOM.Tests/Composition`; WP110 `TaomSettings.cs` and `McmRenamedDefaultsTests.cs`; WP117 two
> `MissionPerf` lines in `SubModule.cs` and `MissionTickProfilerWiringTests.cs`; WP123 the settings
> providers, the colour store block and the `FeatureModulesTests` anchor; WP124 the Warg files;
> WP127 the CombatMechanics model). Compare against "Current state" and the Step 0.4 anchors; STOP
> only on a real mismatch.

## Status

- **Priority**: P1 (the warg formation fix is P1; the rest P2 and P3)
- **Effort**: L (13 commits in six Parts; two executor sessions: Parts A to C, then D to F)
- **Risk**: MED (both single-owner `SubModule.cs` regions, three MCM renames, one data file)
- **Depends on**: WP105 (feature-module runner, its kernel tests; brings WP104's `RepoPaths`),
  WP123 (plan 031 `74e9172d4`: cached `Settings` and internal test constructors in the DreadAura,
  CompanionTactics and CombatMechanics providers; the kernel anchor move of `e0ad0db76`), WP110
  (`McmRenamedDefaultsTests`, `tools/check_mcm_default_flips.py`), and for Part D only WP127
  (`f7e9bd0d2`'s `CombatMechanicsHooks` facade). All are earlier waves.
- **Category**: bug
- **Wave / lane**: wave 4, lane C
- **Executor**: laptop
- **Decisions applied**: D1 (no `CHANGELOG.md`; the body is the entry), D2 (none of these features
  has a 1.5.x module, so they stay hand-wired), D7 (no TAOM.Client split), D8 (`v2.0.28`), D9 (no
  CI; every gate runs locally; a ported test drops its `RequiresGame` category, as WP104, WP112 and
  WP123 do), D10, D13 (the four Dunland retags), D16 (no RNG lock), D19 (git through the Bash
  tool), D20 (data commits only after WP101's Armory baseline)
- **Orchestrator decisions** (made under the maintainer's 2026-11-04 delegation, not reopened here):
  the dread aura stops routing troops by itself on 1.4.x too (Part B); Part D ports the Custom
  Battle damage twin although its player impact is low; the battle action bar ships parked, off by
  default under a new setting name (Part E)
- **Planned at**: 1.4.x `origin/bannerlord-1.4.5` `1b00d1acd`; 1.5.x `origin/bannerlord-1.5.x`
  `e45f6df2c` (every cited 1.5.x commit is an ancestor of it); 2026-11-04
- **Issue**: Port of #610, #611/#615, #692, #788, #953 and #970. The banner race gate (`30abf550b`,
  `25995dc91`), the dread aura fix (`efa32184b`), the warg formation fix (`726975a52` carries #763,
  which is the Fords of War feature, not this fix) and the warg controller guard (`c91380274` carries
  #968, the client split, not this fix) carry no issue of their own: orchestrator creates the issue
  before implementation lands.

## Why this matters

These are 1.4.x defects in features both lines ship, each fixed on 1.5.x and verified present on
`origin/bannerlord-1.4.5`. A cave troll can lift a dropped standard in any campaign battle and carry
banners at will in Custom Battle. Cavalry rarely floors orcs or uruks because the charge rule pushes
penetration below vanilla for every victim heavier than the charger. Custom Battle, the maintainer's
test venue, runs none of TAOM's combat mechanics. Warg riders that join after the first scan leave
their formation for the rest of the battle after a rage or a warg death. The battle action bar is
dead on 1.4.x and its `SubModule` call builds a second warg rage controller per mission that never
unsubscribes its handlers; fixing the registration alone would make the bar swallow attack and
block, so it is fixed and parked together. The dread aura routs troops by itself and the routs
spread through vanilla's formation-wide contagion. `taom.spawn_troops` finds no troop in Custom
Battle. Four Dunland bowmen fight in the wrong formation.

## Current state

### Executor environment

- Laptop; repo `C:\Users\mikew\source\repos\TAOM`; Bannerlord v1.4.8 (build 119303) at
  `C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord`
  (`BANNERLORD_GAME_DIR`). Never write into the game install.
- Base: local `bannerlord-1.4.5` after WP101 plus every WP merged before this one.
- Worktree `C:\Users\mikew\source\repos\taom-port\130`, branch `port148/130-battle-creature-fixes`.
  A `cd` there does not persist between Bash calls: start EVERY command with
  `cd /c/Users/mikew/source/repos/taom-port/130 &&`.
- Python: `python` (if Git Bash cannot find it, `/c/Python314/python.exe`).

### Files and roles

| File | Role here |
|---|---|
| `Main/Features/BannerBearers/{IBannerBearerService,BannerBearerService}.cs`, `Models/TaomBattleBannerBearersModel.cs` | Race gate policy and the campaign banner model (A1) |
| `Main/Features/BannerBearers/Models/TaomCustomBattleBannerBearersModel.cs` (new) | Custom Battle race gate (A2) |
| `Main/SubModule.cs` (single-owner) | A2 adds `RegisterCustomBattleModels` and its call; D1 adds one model line; E3 deletes two `AddTaomBehavior` lines |
| `Main/Features/DreadAura/*`, `Main/_Module/ModuleData/dread_aura/dread_aura_config.json` | Aura radius, floor, MCM rename (B1) |
| `Main/Features/CombatMechanics/{CombatMechanicsConfig,CombatMechanicsSettingsProvider}.cs`, `combat_mechanics_config.json` | Knockdown parity values (C1) |
| `Main/Features/CombatMechanics/Models/TaomCustomBattleDamageModel.cs` (new) | Custom Battle damage twin (D1) |
| `Main/Features/Warg/{WargMissionBehavior,WargRiderHandManager}.cs`, `BehaviorTreeElements/{PrepareOnFirstCall,CleanIfEnemyDied,FinishRageMode}.cs` | Formation fix (E1), controller null guards (E2) |
| `Main/Features/CompanionTactics/**` (action bar), `Main/_Module/GUI/PreFabs/BattleActionBar.xml` | Registration, input, park (E3) |
| `Main/Features/TaomSettings.cs` | Three renames: `DreadAuraCloseRadius` (B1), `ChargeAutoKnockdownRatio` (C1), `ShowBattleActionBar` (E3) |
| `Main/Features/DevConsole/Cheats/MissionSpawnCheats.cs` | Custom Battle troop lookup (F1) |
| `Main/_Module/ModuleData/troops/troops_dunland.xml` | Four `default_group` retags (F3) |

### 1.4.x excerpts (`origin/bannerlord-1.4.5`, read this session)

- `TaomBattleBannerBearersModel.cs:51-61` overrides `CanAgentBecomeBannerBearer` only; no
  `CanAgentPickUpAnyBanner`. `BannerBearerConfig.cs:43-50` excludes `cave_troll`, `hill_troll`,
  `nazghul`, `saruman`, `sauron`. `IBannerBearerService.cs:58` ends at `IsReinforcementBearerAllowed`.
- `SubModule.cs:710` `if (gameStarterObject is CampaignGameStarter campaignStarter)`: no Custom
  Battle branch. `:1117-1123` registers `TaomBattleBannerBearersModel` with the comment
  "Campaign-only: Custom Battle ... is unaffected". `:1107` registers `TaomCombatMechanicsModel` on
  the campaign starter only. `:1805` `AddTaomBehavior(new AutonomousMovementPlayerController());`,
  `:1853` `AddTaomBehavior(new Features.CompanionTactics.BattleActionBar.Hooks.BattleActionBarMissionView());`.
- What earlier WPs do to the same spots (read in their plans this session, so expected on your base):
  WP105 inserts `FeatureModuleHooks.AddMissionBehaviors(mission, AddTaomBehavior);` after the colour
  store block and `FeatureModuleHooks.AddGameStartContent(gameStarterObject);` after the campaign
  branch (`plans/105-composition-root.md:540-572`). WP117 inserts a `MissionPerfHeartbeatBehavior` add
  directly before the action bar line and a multi-line `MissionTickProfilerBehavior` add after it
  (`plans/117-frame-and-memory-instruments.md:396-401`, `:776-786`); its `MissionTickProfilerWiringTests`
  (from `685c6a1e0`) anchors on `src.IndexOf("BattleActionBarMissionView")`. WP123 deletes the colour
  store block and moves `FeatureModulesTests`' mission anchor onto the action bar line (`e0ad0db76`,
  plan 123 Step A3). WP127 makes the campaign damage model registration resolve
  `IoC.Resolve<Features.CombatMechanics.Hooks.CombatMechanicsHooks>()` (plan 127's b-code spec).
- `ChargeKnockdownService.cs:76-79`: `penetration = _config.HorseChargePenetration * Clamp(weightRatio /
  _config.NeutralWeightRatio, _config.MinPenetrationFactor, _config.MaxPenetrationFactor) * speedFactor`.
  `combat_mechanics_config.json:21` `"autoKnockdownWeightRatio": 8.0,`, `:24` `"minPenetrationFactor": 0.25,`,
  `:54` `"uruk_hai": { "swingEnergyBonusFactor": 0.10, "knockdownResistanceMultiplier": 1.25 }`;
  `CombatMechanicsConfig.cs:30, :67, :73` hold the same compiled defaults. The provider deserializes
  with `ObjectCreationHandling.Replace` and validates auto >= neutral and min <= max.
- `TaomSettings.cs:803` `EnableBattleActionBar = true`, `:1042` `ChargeAutoKnockdownWeightRatio = 8`,
  `:1054` `DreadAuraRadius = 12f`. None is in `CoopSettingsRelevance` (only `BattleActionBarDebug` at
  `:54`), so renames keep every `SettingsFingerprintTests` count.
- `BattleActionBarMissionView.cs:33-70` attaches in `OnMissionScreenInitialize` behind
  `Mode == Battle`, `:60` `SetInputRestrictions()` (defaults: cursor shown, all input), `:128-159`
  polls keys 1 to 9. The 1.4.x file equals the 1.5.x one before `c91380274`, which only added
  nullable marks. Its service, model, both VMs, prefab and service tests equal `40385aa5b^`, so that
  commit's hunks for them apply as they are (checked with `git apply --check` this session).
- `WargBehaviorTree.cs:30` stores `agent.RiderAgent?.Formation` when the tree is built;
  `WargMissionBehavior.cs:129-136` builds late trees in `OnAgentBuild`. `PrepareOnFirstCall.cs:44-45`
  clears the rider's formation without storing it; `CleanIfEnemyDied.cs:40-45` and
  `OnWargDied.cs:38` write the stored (null) formation back. `WargMissionBehavior.cs:185-199`
  disables vanilla's controller, then dereferences the rage controller unguarded; `PrepareOnFirstCall.cs:39-40`,
  `FinishRageMode.cs:37-38`, `WargRiderHandManager.cs:27-28, :33-38` dereference it unguarded too.
- `dread_aura_config.json:26-31`: radius 12, inner 4, rate 5, floor 0.
- `MissionSpawnCheats.cs:54` `GetObject<CharacterObject>(troopId)`, `:61` `new SimpleAgentOrigin(character)`.
- `troops_dunland.xml`: `dunland_raven_noble_son` (Cigfran-lûth Hunter, bow and arrows only) is
  `Infantry`; `dunland_falcon_archer`, `_wildrider`, `_noble_horse_archer` (Hebog-lûth, bow, arrows,
  axe) are `Cavalry`. Same ids and lines (1705, 2082, 2165, 2248) on the public #697 tree. None of
  the three Hebog-lûth battle rosters has a `Horse` slot on 1.4.x, the public tree or 1.5.x.
- Not on 1.4.x (`git grep`, this session): `PassesRaceGate`, `TaomCustomBattleBannerBearersModel`,
  `RegisterCustomBattleModels`, `TaomCustomBattleDamageModel`, `ChargeAutoKnockdownRatio`,
  `DreadAuraCloseRadius`, `ShowBattleActionBar`, `ShouldAttach(`, `CreateOrigin(`,
  `MissionViewRegistrationGate`, `AgentsFormation.SetValue(rider.Formation)`.

### Engine facts the code binds (installed v1.4.8; "src" = `pwsh -NoProfile -File tools/taom-src.ps1 path <Type>` cache, "dump" = `C:\Decompiled_Bannerlord\_categories_v1.4.8` or `_modules_build`)

| Fact | Where (read this session) |
|---|---|
| `FindBestSearcherForBanner` picks a dropped banner's carrier by `CanAgentPickUpAnyBanner` alone | src `TaleWorlds.MountAndBlade.BannerBearerLogic.cs:349-369` (call at `:358`) |
| `public abstract bool CanAgentPickUpAnyBanner(Agent agent);`, `public abstract bool CanAgentBecomeBannerBearer(Agent agent);` | src `ComponentInterfaces.BattleBannerBearersModel.cs:112`, `:114` |
| `SandBox.SandboxBattleBannerBearersModel.CanAgentPickUpAnyBanner` is `public override`, race-blind (IsHuman, no banner, scripted movement, not panicked, no important action) | src `:43-54` |
| `public class CustomBattleBannerBearersModel : BattleBannerBearersModel` (namespace `TaleWorlds.MountAndBlade`), `public override` pickup `:46`, become-bearer `:59` (no race check) | src |
| Second pickup path: `DefaultItemPickupModel` banner score 120 `:14-16`, banner into empty slot `:76-77`, eligibility `:82-101`; `HumanAIComponent.ItemPickupTick` `:210`, gate `:229` | src |
| `CustomGame.OnInitialize`: `InitializeGameModels` then `GameManager.OnGameStart` (`:4054-4063`); adds `CustomAgentApplyDamageModel` `:4116`, `CustomBattleBannerBearersModel` `:4126`; `OnRegisterTypes` registers `BasicCharacterObject` for `NPCCharacter` `:4155` | dump `CustomBattle__TaleWorlds.MountAndBlade.CustomBattle.cs` |
| `BasicGameStarter.AddModel<T>(MBGameModel<T>)` `:34-39`; `GameModelsManager.GetGameModel<T>` walks backwards (last added wins) `:15-25`; `public class CampaignGameStarter : IGameStarter` `:9` | src |
| `CustomAgentApplyDamageModel`: `DecideMissileWeaponFlags` grants nothing `:111-113`; `DecideCrushedThrough` needs `CanCrushThrough`, overhead swing, energy > 58 (x1.2 vs shield) `:115-133`; `GetHorseChargePenetration` = 0.4 `:252-255`; flat stagger threshold `:257-261`; shield damage x1.25 `:294-305`; plus `DecideAgentShrugOffBlow` `:383`, `DecideAgentKnockedDownByBlow` `:398`, `DecideWeaponCollisionReaction` `:408`, `CalculateRemainingMomentum` `:418` | src |
| SandBox's damage model reads `DefaultSkillEffects.MountedWeaponDamagePenalty` for a mounted attacker; that resolves through `Campaign.Current` | src `SandBox.GameComponents.SandboxAgentApplyDamageModel.cs:509-512`; dump `DefaultSkillEffects.cs:94`, `:126` |
| Horse charge: knock-back needs dot >= 0.7 `:54-59`; knock-down only with the KnockBack flag, `DecideCombatEffect` `:74-90`, `damage >= maxHP * max(0, resist - pen)` `:336-340` | src `MissionCombatMechanicsHelper.cs` |
| `MissionState.FinishMissionLoading`: `OnMissionAfterStarting` `:343` before `AfterStart` `:345`; `MissionScreen.OnMissionAfterStarting` registers MissionViews `:3991-4002`; default views join the mission `:749-755`; `ViewCreatorManager` keeps types with exactly one `[DefaultView]` `:253-262` and builds them with `Activator.CreateInstance` `:85-118`; `[DefaultView]` is `TaleWorlds.MountAndBlade.View.DefaultView` | src |
| `Mission.AfterStart` `:3799` calls `OnMissionBehaviorInitialize` `:3815`; `GetMissionBehavior<T>` walks forward `:4631-4641`; `IsFieldBattle` `:1373`; `Mode` `:1183`; `DeploymentHandler.cs:41` sets Deployment at start (dump); `MissionMode` has `StartUp`, `Battle`, `Deployment` (dump) | src / dump |
| `GauntletLayer.LoadMovie` returns `GauntletMovieIdentifier` `:119`, `ReleaseMovie(GauntletMovieIdentifier)` `:140`; `InputRestrictions.SetInputRestrictions(bool isMouseVisible = true, InputUsageMask mask = InputUsageMask.All)` `:28`; `InputUsageMask.MouseButtons = 1` (dump); `MissionMainAgentController` runs combat input only `if (!MissionScreen.MouseVisible ...)` `:1001`, `Disable()` `:1256`, `Enable()` `:1261` | src |
| `Mission.SpawnAgent` builds the mount and fires its `OnAgentBuild` (`:4343-4347`) before building the rider (`:4349`, `OnAgentBuild` `:4358-4361`); `Agent.Build` sets `Formation = IsMount ? null : AgentFormation` | src `Mission.cs`, `Agent.cs:5168-5173` |
| `MBObjectManager.GetObject<T>`: exact type match when `T` is sealed, assignable match otherwise `:573-597`; `CharacterObject` is sealed (dump `:16`); the campaign registers `CharacterObject` (dump `Campaign.cs:1537`); `SimpleAgentOrigin` casts `(CharacterObject)troop` `:137`; `BasicBattleAgentOrigin(BasicCharacterObject troop)` `:43` | src / dump |
| Morale contagion: radius 4 `:12`, 10 allies `:14`, `:16`, nearby first then the formation `:80-85`, fleeing `:57-64` (dump `...Source.Missions.Handlers.Logic/AgentMoraleInteractionLogic.cs`); casualties factor 1 + 2 x removed ratio (dump `SandboxBattleMoraleModel.cs:412-424`) | dump |
| `AgentProximityMap.BeginSearch` loops every agent above `ProximityMapMaxSearchRadius` `:54-63`, which is native (`Mission.cs:1713-1716`). The native value is UNVERIFIED on v1.4.8 (the 1.5.x line read 15.0 on v1.5.5) | src |

### Conventions that bind this change

- ADR-007 adapters: services never touch sealed TaleWorlds types (the banner service takes an `int`
  race id; models do the boundary reads). ADR-002 thin entry points under 150 lines (the damage twin
  is about 70). ADR-003 no `#region`; ADR-004 no `[Obsolete]`; ADR-005 no `#if DEBUG` outside IoC.
- TDD: each step writes or applies its failing tests first (RED), then the change (GREEN). Ported
  cherry-picks bring their own tests.
- MCM json2 rule: MCM loads a saved value by property name, so a changed default needs a new
  property name (never flip a default in place). `tools/check_mcm_default_flips.py` (WP110) enforces it.
- `.claude/rules/gamemodels.md`: a model override folds its feature toggle by deferring to `base`;
  every `Taom*Model` is `new`-ed in `SubModule.cs` (`GameModelOverrideBindingTests`) and listed in
  `gamemodels.md` and `docs/reference/gamemodel-registry.md` (`lint_docs.py` model registry).
- `.claude/rules/harmony-patches.md`: no Harmony patch is added or changed here.
- `docs/features/composition-root.md` (WP105): a 1.4.x feature with no 1.5.x module keeps its hand
  wiring; a deleted kernel-test anchor moves to the nearest surviving line, never deleted.

## Commands you will need

Start every command with `cd /c/Users/mikew/source/repos/taom-port/130 &&`.

| Purpose | Command | Expected |
|---|---|---|
| Restore | `dotnet restore TAOM.sln` | exit 0 |
| Build | `dotnet build TAOM.sln -p:DisableModuleCopy=true -p:ModuleId=` | exit 0, 0 errors |
| Test | `dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId=` | `Failed: 0` (600000 ms timeout) |
| Filtered | `dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId= --filter "<expr>"` | as each step says |
| Gate | `python tools/check_mcm_default_flips.py; echo "exit=$?"` | `exit=0`, no `FLIP`, `UNPARSED` or `STALE` line |
| Data | `python tools/validate_moduledata.py > C:/Users/mikew/source/repos/taom-port/130-validate.txt 2>&1` | no new ERROR against Step 0.8 |
| Tool tests | `python -m unittest discover -s tools/tests -t .` | no new failure against Step 0.6 |
| Docs | `python tools/lint_docs.py --summary --dash-base $(git merge-base HEAD bannerlord-1.4.5)` | `ai_dashes: 0`; `dead_links`, `orphan_features`, `model_registry` equal Step 0.7 |

Both `-p:` flags are mandatory on every build and test (`Main/TAOM.csproj:133-136` does not gate
CopyModule on `DisableModuleCopy`). Never `./build.ps1`. Never two builds at once. Build before
every `--no-build` run. Each data row counts as one test result in the totals below; if your
runner reports data rows differently, report both numbers instead of stopping.

## Scope

**In scope**: the files in "Files and roles"; the test files each step names; `docs/features/{banner-bearers,combat-mechanics,companion-tactics,dread-aura,warg-combat}.md`;
`docs/modding/configs-factions-and-world.md`; `docs/reference/gamemodel-registry.md`;
`docs/reviews/rca-battle-action-bar-2026-10-23.md` (new); `.ai/scopes.md` (one link); and the
mechanism lines in `.claude/rules/gamemodels.md` (two rows, counts), `.claude/rules/gui-ui.md`
(battle overlay input) and `.claude/rules/csharp-patterns.md` (one anti-pattern).
Single-owner: `Main/SubModule.cs` only in the regions named above. `Main/IoC.cs`, `Main/TAOM.csproj`,
`TAOM.Tests/TAOM.Tests.csproj`: no edit (SDK projects pick up new files).

**Out of scope**: `CHANGELOG.md` (D1); `CLAUDE.md`, `AGENTS.md`, `docs/reviews/lessons/*`,
`docs/reviews/LESSONS-LEARNED.md`, `docs/reviews/REVIEW-LOG.md`, `docs/reviews/codex-*`,
`docs/INDEX.md`, `docs/reference/feature-map.md`, `tools/README.md` (WPs 142 to 144);
`.claude/settings*.json`, `Directory.Build.props`, `docs/adrs/**` (maintainer only); `plans/README.md`;
the per-culture charge damage, the three extra charge sliders, TrollBruteForce, CreatureBandits,
CultureDoctrine, SignatureStrikes, RaceAbilities, Fords of War and TAOM.Client code; any skill or
equipment value; `docs/reference/ranged-troops.html` (generated); `docs/reference/taleworlds-api-snapshot/*`.

## Git workflow

- Work and commit only in the worktree, on `port148/130-battle-creature-fixes`. Never push, open a
  PR, tag or merge. Run git only through the Bash tool (D19).
- Cherry-picks (B1, B2): `git cherry-pick -n <sha>` only (never let git create the commit:
  `GIT_EDITOR=true` on this laptop would commit the 1.5.x message unedited). Then: a conflicted
  bookkeeping path `git checkout --ours <path> && git add -- <path>`; a cleanly changed one
  `git checkout HEAD <path>`; a cleanly added bookkeeping file `git rm -f -q -- <path>`; a
  modify/delete conflict on a path 1.4.x lacks (`DU`) `git rm -f -q -- <path>`. Other ports use
  `git show <sha> -- <paths> | git apply` or the edits the step gives.
- Subjects keep the 1.5.x type and scope, except where only a side fix is taken from a commit of
  another feature (A1 from an unlabelled sprint commit, E1 from `feat(fords-of-war)`, E2 from
  `feat(coop)`, F1 from `feat(creature-bandits)`, F2 from `docs(review)`): those name the code
  actually changed, and the `Ported-from:` trailer names the source.
- Stage with explicit `git add -- <paths>` (never `-A`, `-u`, `.`). `git diff --cached --name-status`
  must equal the step's list before committing.
- Write each message with the Write tool to `C:/Users/mikew/source/repos/taom-port/130-msg-<step>.txt`,
  then run `git commit -F <that file>` in the NEXT sequential Bash call (never the same call, never
  parallel). Never `--no-verify`; a denied commit is a STOP (quote the hook message). After each
  commit `git status --porcelain` is empty and `git status` shows no cherry-pick in progress (if it
  does: `git cherry-pick --quit`).
- Message shape: subject (given, at most 72 characters, `v2.0.28`), blank line, the body given
  (keep it wrapped at 72, no long dashes, no `Co-Authored-By` or "Generated with" line), then the
  trailers given.

## Steps

### Step 0: worktree, prerequisites, baseline

**0.1** `mkdir -p /c/Users/mikew/source/repos/taom-port && cd /c/Users/mikew/source/repos/TAOM && git worktree add /c/Users/mikew/source/repos/taom-port/130 -b port148/130-battle-creature-fixes bannerlord-1.4.5`
(skip if it exists; then `git -C /c/Users/mikew/source/repos/taom-port/130 branch --show-current`
must print the branch). **Verify**: `Preparing worktree (new branch ...)`.

**0.2 Prerequisites**

```bash
cd /c/Users/mikew/source/repos/taom-port/130 && \
grep -cF 'public static string ReadSource(string relativePath, bool stripComments = false)' TAOM.Tests/Infrastructure/RepoPaths.cs; \
grep -cF 'public static IEnumerable<string> CSharpFiles(params string[] roots)' TAOM.Tests/Infrastructure/RepoPaths.cs; \
grep -cF 'FeatureModuleHooks.AddGameStartContent(gameStarterObject);' Main/SubModule.cs; \
grep -cF 'FeatureModuleHooks.AddMissionBehaviors(mission, AddTaomBehavior);' Main/SubModule.cs; \
for f in DreadAura/DreadAuraSettingsProvider CompanionTactics/CompanionTacticsSettingsProvider CombatMechanics/CombatMechanicsSettingsProvider; do grep -cF 'Settings => _settings ??= TaomSettings.Instance;' Main/Features/$f.cs; done; \
grep -cF 'internal CombatMechanicsSettingsProvider(ICombatMechanicsConfigProvider configProvider, TaomSettings settings)' Main/Features/CombatMechanics/CombatMechanicsSettingsProvider.cs; \
grep -cF '"BanditMaxPartiesPerHideout", "BanditPartiesPerHideoutCap", 6' TAOM.Tests/Infrastructure/McmRenamedDefaultsTests.cs; \
grep -c 'ChargeAutoKnockdown' TAOM.Tests/Infrastructure/McmRenamedDefaultsTests.cs; \
test -f tools/check_mcm_default_flips.py && echo gate-present; \
test -f TAOM.Tests/Features/CombatMechanics/CombatMechanicsSettingsProviderTests.cs && echo cm-provider-tests-present; \
git grep -l -E 'PassesRaceGate|RegisterCustomBattleModels|TaomCustomBattle|ShowBattleActionBar|DreadAuraCloseRadius|ChargeAutoKnockdownRatio' -- Main | wc -l
```

**Verify**: `1` (x9), `0`, `gate-present`, `cm-provider-tests-present` (WP123 step 4 creates that
file), `0`. A missing prerequisite or a non-zero last line (work already landed): STOP.

**0.3 Drift check**: the header command; read each change against the anchors below.

**0.4 Anchors** (each line must start with `1`):

```bash
cd /c/Users/mikew/source/repos/taom-port/130 && chk(){ printf '%s  %s\n' "$(grep -cF -- "$2" "$1")" "$2"; }; S=Main/SubModule.cs; T=Main/Features/TaomSettings.cs; W=Main/Features/Warg; \
chk $S 'AddTaomBehavior(new AutonomousMovementPlayerController());'; \
chk $S 'AddTaomBehavior(new Features.CompanionTactics.BattleActionBar.Hooks.BattleActionBarMissionView());'; \
chk $S 'if (gameStarterObject is CampaignGameStarter campaignStarter)'; \
chk $S 'campaignStarter.AddModel<BattleBannerBearersModel>(new TaomBattleBannerBearersModel('; \
chk $S "// takes the LAST registered model, so ours wins over SandBox's. Campaign-only: Custom"; \
chk $S '// Campaign-life behaviors: startup resources, companions, inventory/equipment QoL, fief +'; \
chk $T 'public int ChargeAutoKnockdownWeightRatio { get; set; } = 8;'; \
chk $T 'public bool EnableBattleActionBar { get; set; } = true;'; \
chk $T 'public float DreadAuraRadius { get; set; } = 12f;'; \
chk Main/_Module/ModuleData/combat_mechanics/combat_mechanics_config.json '"minPenetrationFactor": 0.25,'; \
chk Main/Features/CombatMechanics/CombatMechanicsSettingsProvider.cs 'var mcm = Settings?.ChargeAutoKnockdownWeightRatio;'; \
chk Main/Features/CombatMechanics/CombatMechanicsSettingsProvider.cs 'return SettingClamp.Clamp(mcm.Value, 8, neutralFloor, 30);'; \
chk Main/Features/CompanionTactics/CompanionTacticsSettingsProvider.cs 'public bool EnableBattleActionBar => Settings?.EnableBattleActionBar ?? true;'; \
chk $W/WargMissionBehavior.cs 'public static void SwitchMainAgentController(bool switchToSpecialController)'; \
chk $W/BehaviorTreeElements/PrepareOnFirstCall.cs 'if (rider != null) rider.Formation = null;'; \
chk $W/WargRiderHandManager.cs 'private static void UpdateWargRiderHandle()'; \
chk Main/Features/DevConsole/Cheats/MissionSpawnCheats.cs 'var character = MBObjectManager.Instance?.GetObject<CharacterObject>(troopId);'; \
chk Main/_Module/ModuleData/dread_aura/dread_aura_config.json '"radius": 12.0,'
```

**Verify**: 18 lines, each `1`. Otherwise STOP.

**0.5 Baseline**: Restore, Build, Test. Record `Total`, `Passed`, `Skipped` as **T0**. Any failure: STOP.
T0 should equal the count the previous merged WP recorded in the main checkout's
`/c/Users/mikew/source/repos/TAOM/plans/README.md` (read it there; never edit it); a different number
is not a STOP, report both. Every total below is T0 plus this plan's new tests.
**0.6** Tool tests: record the `Ran N tests` line and failures as **P0**.
**0.7** Docs: record `dead_links`, `orphan_features`, `model_registry` (and confirm `ai_dashes: 0`).
**0.8** Data: run it; record the per-code ERROR counts as **V0** (D20: WP101's laptop baseline is in
the integrator section of the main checkout's `plans/README.md`; a large mismatch with it is a STOP
before Part F).

---

### Part A: banner bearers keep their race gate everywhere (`30abf550b`, `25995dc91`)

#### Step A1: trolls never pick up a dropped banner (RED, then GREEN)

1. Create `TAOM.Tests/Features/BannerBearers/BannerBearerRaceGateWiringTests.cs`:

```csharp
using System;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Infrastructure;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.BannerBearers;

/// <summary>
/// A dropped banner is fetched by the nearest formation member that passes CanAgentPickUpAnyBanner, and the engine
/// asks nothing else (v1.4.8 BannerBearerLogic.FindBestSearcherForBanner). SandBox's and Custom Battle's answers
/// are race-blind, so every TAOM banner model adds the race gate there. Agent is sealed, so the gate is pinned in
/// source; the reflection rows prove each method overrides the engine's instead of shadowing it.
/// </summary>
[TestClass]
public class BannerBearerRaceGateWiringTests
{
    private const string CampaignModelSource = "Main/Features/BannerBearers/Models/TaomBattleBannerBearersModel.cs";
    private const BindingFlags Declared = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;
    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    private static Type Taom(string fullName)
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));
        return typeof(TAOM.IoC).Assembly.GetType(fullName, throwOnError: true)!;
    }

    [DataTestMethod]
    [DataRow(CampaignModelSource)]
    public void EachBannerModel_GatesTheDroppedBannerPickupByRace(string path)
    {
        var source = RepoPaths.ReadSource(path, stripComments: true);

        StringAssert.Contains(source, "public override bool CanAgentPickUpAnyBanner(Agent agent)");
        StringAssert.Contains(source, "base.CanAgentPickUpAnyBanner(agent) && _service.PassesRaceGate(agent?.Character?.Race ?? -1)");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void CampaignModel_OverridesTheEnginesPickupCheck()
    {
        var method = Taom("TAOM.Features.BannerBearers.Models.TaomBattleBannerBearersModel").GetMethod("CanAgentPickUpAnyBanner", Declared);

        Assert.IsNotNull(method, "TaomBattleBannerBearersModel does not declare CanAgentPickUpAnyBanner");
        Assert.AreNotSame(method, method!.GetBaseDefinition(), "it shadows the engine's method instead of overriding it");
    }
}
```

2. `cd /c/Users/mikew/source/repos/taom-port/130 && git show 30abf550b7fa5613984bc3ec55cde80aa86f4ba1 -- TAOM.Tests/Features/BannerBearers/BannerBearerServiceTests.cs | git apply`
   (adds the four `PassesRaceGate_*` tests). **Verify (RED)**: Build fails with CS1061 naming `PassesRaceGate`.
3. GREEN: `git show 30abf550b7fa5613984bc3ec55cde80aa86f4ba1 -- Main/Features/BannerBearers/BannerBearerService.cs Main/Features/BannerBearers/IBannerBearerService.cs Main/Features/BannerBearers/Models/TaomBattleBannerBearersModel.cs | git apply`
   (`PassesRaceGate` folds the toggle: disabled true, else `IsRaceAllowed`; the campaign model gains
   `CanAgentPickUpAnyBanner => base... && _service.PassesRaceGate(...)`).
4. Docs, `docs/features/banner-bearers.md`:
   - Directly above the line that starts `` `IsRaceAllowed` **validates before lookup** `` insert two paragraphs:

```markdown
The gate covers a dropped banner as well as a new bearer. `BannerBearerLogic.FindBestSearcherForBanner` sends the nearest formation member that passes `CanAgentPickUpAnyBanner` to fetch a banner lying on the ground, and never asks `CanAgentBecomeBannerBearer` (v1.4.8 `BannerBearerLogic.cs:349-369`). SandBox's `CanAgentPickUpAnyBanner` checks only `IsHuman` (the humanoid flag), an empty banner slot, scripted-movement availability, no panic and no important combat action (`SandboxBattleBannerBearersModel.cs:43-54`), so a troll could pick one up. The model therefore overrides it as `base.CanAgentPickUpAnyBanner(agent) && _service.PassesRaceGate(race)`. `BannerBearerService.PassesRaceGate` folds the master toggle (disabled returns `true`, vanilla parity) and otherwise defers to `IsRaceAllowed`. A missing character reads as race `-1`, which fails closed while the feature is on.

A second pickup path never asks the gate, and only the troll kits keep it shut. `HumanAIComponent.ItemPickupTick` walks an agent to a nearby dropped item, and `DefaultItemPickupModel` scores the agent's own formation banner at 120 (`GetItemScoreForAgent`, v1.4.8 `DefaultItemPickupModel.cs:14-16`) and accepts it into an empty slot (`IsItemAvailableForAgent`, `:76-77`) without calling `CanAgentPickUpAnyBanner`. The tick runs only for an agent `IsAgentEquipmentSuitableForPickUpAvailability` passes (`:82-101`): one that lost the shield it spawned with, one holding a consumable stack at half or less, or a banner searcher, which the gate above already filtered. No troll kit carries a shield or a consumable on 1.4.x (`troops_mordor.xml`: the `cave_troll`'s maces and spear, one weapon per roster; TAOM's 1.4.x ModuleData has no hill troll troop). **A troll kit that gains a shield or throwables (any consumable, arrows included) reopens this path**; that change needs a race check on the item pickup as well.
```

   - Testing table: prefix the `BannerBearerServiceTests.cs` cell with
     `` `PassesRaceGate` (disabled admits a troll; enabled denies both trolls and an invalid id, admits a human); ``,
     add a row `` | `BannerBearerRaceGateWiringTests.cs` | Every banner model gates the dropped-banner pickup by race and overrides the engine's check (source and reflection pins). | ``,
     and set the folder total and the `BannerBearerServiceTests.cs (N)` count to the numbers the
     filtered run below reports for the folder and for `BannerBearerServiceTests`.
   - Manual test list: `- No troll ever raises a banner.` becomes `- No troll ever raises a banner or picks up a dropped one.`
   - `docs/reference/gamemodel-registry.md`, `TaomBattleBannerBearersModel` row: after
     `+ JSON race gate` insert `` and infantry-only gate on `CanAgentBecomeBannerBearer`; `CanAgentPickUpAnyBanner` adds the race gate (`PassesRaceGate`), because the dropped-banner pickup (`BannerBearerLogic.FindBestSearcherForBanner`) asks only that method and SandBox's answer is race-blind ``.

**Verify**: Build exit 0; Filtered `--filter "FullyQualifiedName~TAOM.Tests.Features.BannerBearers|FullyQualifiedName~GameModelOverrideBindingTests"`
0 failed. Stage `Main/Features/BannerBearers/{BannerBearerService,IBannerBearerService}.cs`,
`Main/Features/BannerBearers/Models/TaomBattleBannerBearersModel.cs`,
`TAOM.Tests/Features/BannerBearers/{BannerBearerServiceTests,BannerBearerRaceGateWiringTests}.cs`,
`docs/features/banner-bearers.md`, `docs/reference/gamemodel-registry.md` (7 paths). Commit:

```text
fix(banners): v2.0.28 - trolls never pick up a dropped banner

A formation whose bearer falls sends its nearest member to fetch the
banner off the ground, and the engine picks that member by
CanAgentPickUpAnyBanner alone (v1.4.8 BannerBearerLogic, the
FindBestSearcherForBanner loop). SandBox's answer checks only that the
agent is humanoid, holds no banner and is free to move, so a cave troll
could lift the standard TAOM already refuses to hand it. The campaign
banner model now adds the race gate to that check through
IBannerBearerService.PassesRaceGate, which defers to vanilla while the
feature is off.

Research: taom-src v1.4.8 BannerBearerLogic.FindBestSearcherForBanner,
  SandboxBattleBannerBearersModel.CanAgentPickUpAnyBanner,
  DefaultItemPickupModel, HumanAIComponent.ItemPickupTick
Not-tested: a dropped banner next to a troll in a campaign battle
Ported-from: 30abf550b7fa5613984bc3ec55cde80aa86f4ba1
```

#### Step A2: Custom Battle keeps trolls off banners (RED, then GREEN)

1. RED, in `BannerBearerRaceGateWiringTests.cs`: add
   `private const string CustomModelSource = "Main/Features/BannerBearers/Models/TaomCustomBattleBannerBearersModel.cs";`,
   a second `[DataRow(CustomModelSource)]` on `EachBannerModel_GatesTheDroppedBannerPickupByRace`, and:

```csharp
    [TestMethod]
    [TestCategory("BindingVerification")]
    public void CustomBattleModel_ExtendsTheEnginesCustomBattleModel_AndOverridesBothGates()
    {
        var type = Taom("TAOM.Features.BannerBearers.Models.TaomCustomBattleBannerBearersModel");
        Assert.AreEqual("TaleWorlds.MountAndBlade.CustomBattleBannerBearersModel", type.BaseType!.FullName);
        foreach (var name in new[] { "CanAgentBecomeBannerBearer", "CanAgentPickUpAnyBanner" })
        {
            var method = type.GetMethod(name, Declared);
            Assert.IsNotNull(method, name + " is not declared");
            Assert.AreNotSame(method, method!.GetBaseDefinition(), name + " shadows instead of overriding");
        }
    }

    [TestMethod]
    public void RegisterCustomBattleModels_AddsTheRaceGateOnABasicGameStarterOnly()
    {
        var src = RepoPaths.ReadSource("Main/SubModule.cs", stripComments: true);
        var start = src.IndexOf("private static void RegisterCustomBattleModels(IGameStarter gameStarterObject)", StringComparison.Ordinal);
        Assert.IsTrue(start >= 0, "RegisterCustomBattleModels is missing");
        var body = src.Substring(start, src.IndexOf("private static void", start + 20, StringComparison.Ordinal) - start);

        StringAssert.Contains(body, "if (gameStarterObject is CampaignGameStarter || !(gameStarterObject is BasicGameStarter basicStarter))");
        StringAssert.Contains(body, "basicStarter.AddModel<BattleBannerBearersModel>(new TaomCustomBattleBannerBearersModel(");
    }

    [TestMethod]
    public void RegisterCustomBattleModels_RunsBeforeTheCampaignBranchAndTheModuleStep()
    {
        var src = RepoPaths.ReadSource("Main/SubModule.cs", stripComments: true);
        var call = src.IndexOf("RegisterCustomBattleModels(gameStarterObject);", StringComparison.Ordinal);
        var campaign = src.IndexOf("if (gameStarterObject is CampaignGameStarter campaignStarter)", StringComparison.Ordinal);
        var modules = src.IndexOf("FeatureModuleHooks.AddGameStartContent(gameStarterObject);", StringComparison.Ordinal);

        Assert.IsTrue(call >= 0 && call < campaign && campaign < modules,
            "the Custom Battle step must precede the campaign branch, and the module step must come last (the last model added wins)");
    }
```

   **Verify (RED)**: Build exit 0; Filtered `--filter "FullyQualifiedName~BannerBearerRaceGateWiringTests"` shows 4 failures.

2. GREEN: create `Main/Features/BannerBearers/Models/TaomCustomBattleBannerBearersModel.cs`:

```csharp
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.BannerBearers.Models;

/// <summary>
/// Custom Battle (and the editor's test battle): <c>CustomBattleBannerBearersModel</c> with TAOM's race gate on top,
/// so a troll never carries or fetches a standard there either. <see cref="TaomBattleBannerBearersModel"/> is
/// registered on the campaign starter only, and Custom Battle builds its own model off a BasicGameStarter (v1.4.8
/// CustomGame.InitializeGameModels), so before this model the vanilla gate alone ran there. Only the race gate is
/// added: Custom Battle keeps its own bearer count, tiers and formation rules.
/// </summary>
public class TaomCustomBattleBannerBearersModel : CustomBattleBannerBearersModel
{
    private readonly IBannerBearerService _service;

    public TaomCustomBattleBannerBearersModel(IBannerBearerService service)
    {
        _service = service;
    }

    // Same polarity as the campaign model: the disabled feature defers to vanilla, and -1 (no character) is an
    // invalid race id, which the service fails closed.
    public override bool CanAgentBecomeBannerBearer(Agent agent) =>
        base.CanAgentBecomeBannerBearer(agent) && _service.PassesRaceGate(agent?.Character?.Race ?? -1);

    // BannerBearerLogic.FindBestSearcherForBanner picks the nearest formation member passing ONLY this check to
    // fetch a banner dropped on the ground; CanAgentBecomeBannerBearer is never consulted for that path.
    public override bool CanAgentPickUpAnyBanner(Agent agent) =>
        base.CanAgentPickUpAnyBanner(agent) && _service.PassesRaceGate(agent?.Character?.Race ?? -1);
}
```

3. `Main/SubModule.cs` (single-owner, three edits):
   - Directly above `        if (gameStarterObject is CampaignGameStarter campaignStarter)` insert
     `        // Custom Battle and the editor's test battle hand a BasicGameStarter here; a campaign returns at once.`
     and `        RegisterCustomBattleModels(gameStarterObject);` followed by one blank line.
   - Replace the two comment lines `// takes the LAST registered model, so ours wins over SandBox's. Campaign-only: Custom`
     and `// Battle builds CustomBattleBannerBearersModel off a BasicGameStarter and is unaffected.` with:
     `// takes the LAST registered model, so ours wins over SandBox's. Custom Battle builds`,
     `// CustomBattleBannerBearersModel off a BasicGameStarter; RegisterCustomBattleModels adds the`,
     `// race gate there (TaomCustomBattleBannerBearersModel), so no troll carries a standard.` (same indentation).
   - Directly above `    // Campaign-life behaviors: startup resources, companions, inventory/equipment QoL, fief +` insert:

```csharp
    /// <summary>
    /// Custom Battle (and the editor's test battle) hand a BasicGameStarter here after the game type has added its own
    /// models (v1.4.8 CustomGame.OnInitialize: InitializeGameModels, then GameManager.OnGameStart), so a model added now
    /// is the one MissionGameModels resolves (GameModelsManager.GetGameModel returns the last one added). Mirrored: the
    /// banner bearers' race gate. Every other TAOM model stays campaign-only. The feature-module step
    /// (FeatureModuleHooks.AddGameStartContent) runs after this one, so no module may add a model for a slot filled here.
    /// </summary>
    private static void RegisterCustomBattleModels(IGameStarter gameStarterObject)
    {
        if (gameStarterObject is CampaignGameStarter || !(gameStarterObject is BasicGameStarter basicStarter))
            return;
        // BannerBearers: trolls never carry or fetch a standard, in Custom Battle too (race gate only).
        basicStarter.AddModel<BattleBannerBearersModel>(new TaomCustomBattleBannerBearersModel(
            IoC.Resolve<Features.BannerBearers.IBannerBearerService>()));
    }

```

4. Docs:
   - `banner-bearers.md`: replace the line
     `` **Campaign-only.** Custom Battle builds `CustomBattleBannerBearersModel` off a `BasicGameStarter` and is unaffected. ``
     with
     `` **Custom Battle gets the race gate only.** Custom Battle builds `CustomBattleBannerBearersModel` off a `BasicGameStarter`, so the campaign model never runs there. `TaomCustomBattleBannerBearersModel` subclasses Custom Battle's model and adds only the race gate (see "Race gate" below); the bearer count, the infantry-only gate and the culture banners stay campaign-only. ``;
     below the wiring row that names `OnGameStart` add
     ``| [Main/SubModule.cs](../../Main/SubModule.cs) `RegisterCustomBattleModels` (called from `OnGameStart`, `BasicGameStarter` only) | `basicStarter.AddModel<BattleBannerBearersModel>(new TaomCustomBattleBannerBearersModel(...))`: Custom Battle and the editor's test battle |``;
     above the paragraph `The gate covers a dropped banner ...` insert
     ``The gate holds in Custom Battle too. `TaomBattleBannerBearersModel` goes on the campaign starter only, and Custom Battle builds `CustomBattleBannerBearersModel` off a `BasicGameStarter`, so before this change the vanilla gate alone ran there. `TaomCustomBattleBannerBearersModel` (registered in `SubModule.RegisterCustomBattleModels`) adds only this race gate, on both `CanAgentBecomeBannerBearer` and `CanAgentPickUpAnyBanner`; its bearer count, tiers and formation rules stay vanilla.``;
     extend the manual test line to `- No troll ever raises a banner or picks up a dropped one, in the campaign or in Custom Battle.`;
     change the existing `- Shader-precompile walk → no NRE, no hang.` to
     `- Shader-precompile walk (its hidden battles are Custom Battles, so the Custom Battle model runs there) → no NRE, no hang.`
     (the walk's `TaomShaderGameManager` extends `CustomGameManager`).
   - `gamemodel-registry.md`: below the `TaomBattleBannerBearersModel` row add
     ``| `TaomCustomBattleBannerBearersModel` | `CustomBattleBannerBearersModel` | BannerBearers in Custom Battle (and the editor's test battle): the race gate only (`IBannerBearerService.PassesRaceGate`, toggle-folded) on `CanAgentBecomeBannerBearer` and `CanAgentPickUpAnyBanner`; added from `SubModule.RegisterCustomBattleModels` |``.
   - `.claude/rules/gamemodels.md`: below the `TaomBattleBannerBearersModel` row add
     ``| `TaomCustomBattleBannerBearersModel` | `CustomBattleBannerBearersModel` | `BannerBearers` (Custom Battle) |``,
     then raise both count claims (`TAOM has N GameModel overrides (N-1 registered + 1 abstract base)` and
     `## Existing Overrides (N total: N-1 registered + 1 abstract base)`) by one. Run Docs: if
     `model_registry` is above Step 0.7, read `python tools/lint_docs.py` (full report, "GameModel
     registry drift") and set N to the number it says "exist".

**Verify**: Build exit 0; Filtered `--filter "FullyQualifiedName~TAOM.Tests.Features.BannerBearers|FullyQualifiedName~GameModelOverrideBindingTests|FullyQualifiedName~TAOM.Tests.Composition"`
0 failed; Docs `model_registry` equals Step 0.7. Stage the new model, `Main/SubModule.cs`, the test
file, `banner-bearers.md`, `gamemodel-registry.md`, `.claude/rules/gamemodels.md` (6 paths). Commit:

```text
feat: v2.0.28 - Custom Battle keeps trolls off banners

Custom Battle builds its own banner model,
CustomBattleBannerBearersModel, off a BasicGameStarter (v1.4.8
CustomGame.InitializeGameModels), so TAOM's campaign model never ran
there and nothing kept a troll from carrying or picking up a standard.
TaomCustomBattleBannerBearersModel subclasses the Custom Battle model
and adds only the race gate to CanAgentBecomeBannerBearer and
CanAgentPickUpAnyBanner; bearer counts, tiers and formation rules stay
Custom Battle's own.

SubModule gains RegisterCustomBattleModels, called from OnGameStart
before the campaign branch and the feature-module step. It returns for
a campaign and adds Custom Battle models on a BasicGameStarter only,
after the game type added its own, so the TAOM model is the one the
mission resolves (the last model added wins).

Research: taom-src v1.4.8 CustomBattleBannerBearersModel,
  BasicGameStarter.AddModel, GameModelsManager.GetGameModel; dump
  CustomGame.OnInitialize and InitializeGameModels
Not-tested: a troll formation with banners in Custom Battle
Ported-from: 25995dc91d019fa90038bd849be4c84ba0d55765
Ported-from: 30abf550b7fa5613984bc3ec55cde80aa86f4ba1
```

**Part A done**: Test `Failed: 0`, `Total` = T0 + 10; two commits; `git status --porcelain` empty.

---

### Part B: the dread aura stays local (`efa32184b`, `f4f12a737`, `1e62adeb2`)

#### Step B1: cherry-pick `efa32184b`

```bash
cd /c/Users/mikew/source/repos/taom-port/130 && git cherry-pick -n efa32184b31cd903b49989d603226b3907b10f1b; echo "exit=$?"; git status --porcelain
```

Expected conflicts (`UU`): `docs/features/dread-aura.md`, `docs/reference/feature-map.md` (with
WP123 landed as planned, the provider and its tests apply cleanly; simulated this session on a
1.4.x tree carrying `74e9172d4`). Any other conflict: STOP, except the two fallbacks below.
Resolve:
1. `git checkout --ours docs/reference/feature-map.md && git add -- docs/reference/feature-map.md`.
2. `dread-aura.md` has two conflict blocks in its `## Tests` section (the total line and the
   `DreadAuraConfigProviderTests` bullet). Replace each block, markers included, with its first
   (1.4.x) side; B2 sets the numbers. `git add -- docs/features/dread-aura.md`.
3. Fallback, only if `Main/Features/DreadAura/DreadAuraSettingsProvider.cs` conflicts:
   `git checkout --ours` it, then replace `private const float MaxRadius = 30f;` with
   `    // Above 15 m native IMBMission.GetNearbyAgentsAux stops searching its proximity grid and walks`,
   `    // every agent in the mission (15f is what ProximityMapMaxSearchRadius returns on v1.5.5).`,
   `    private const float MaxRadius = 15f;` and `Settings?.DreadAuraRadius` with
   `Settings?.DreadAuraCloseRadius`; `git add` it.
4. Fallback, only if `TAOM.Tests/Features/DreadAura/DreadAuraSettingsProviderTests.cs` conflicts:
   keep ours, change the row `(s => s.DreadAuraRadius = 20f, p => p.Radius, 20f)` to
   `(s => s.DreadAuraCloseRadius = 12f, p => p.Radius, 12f)`, and append
   `McmDefaults_MatchTheCompiledProfileDefaults` and `Radius_McmAboveTheEngineGridLimit_ClampsTo15`
   verbatim from `git show efa32184b -- TAOM.Tests/Features/DreadAura/DreadAuraSettingsProviderTests.cs`; `git add` it.

**Verify**: `git diff --cached --name-status` lists exactly `M` for
`Main/Features/DreadAura/{Domain/DreadAuraConfig,DreadAuraConfigProvider,DreadAuraService,DreadAuraSettingsProvider,Hooks/DreadMissionGate,Hooks/DreadPulseScheduler}.cs`,
`Main/Features/TaomSettings.cs`, `Main/_Module/ModuleData/dread_aura/dread_aura_config.json`,
`TAOM.Tests/Features/DreadAura/{DreadAuraConfigProviderTests,DreadAuraServiceTests,DreadAuraSettingsProviderTests,ShippedDreadAuraConfigTests}.cs`,
`docs/features/dread-aura.md`, `docs/modding/configs-factions-and-world.md` (14 paths);
`git grep -n 'DreadAuraRadius\b' -- Main TAOM.Tests` prints only the two `// ...` rename comment
lines in `TaomSettings.cs`; Build exit 0; Filtered
`--filter "FullyQualifiedName~TAOM.Tests.Features.DreadAura|FullyQualifiedName~SettingsFingerprintTests|FullyQualifiedName~McmRenamedDefaults"`
0 failed; Gate `exit=0`. Commit:

```text
fix(dread): v2.0.28 - the dread aura weakens close troops, never routs

The Aura of Dread of the Nazgul and Sauron no longer reaches across the
battlefield. It touches only the enemy troops in contact with a wraith
(5 m, full strength within 2 m; it was 12 m and 4 m), and it drains
their morale down to 10 and no further, so the fighting finishes them
instead of the aura routing them.

Why it spread on 1.4.x too: the aura could drain morale to 0, and each
rout takes morale from up to 10 allies, first those within 4 m and then
anyone in the routing troop's formation (v1.4.8
AgentMoraleInteractionLogic), so once a line thinned, dread seemed to
act on the whole field.

The MCM "Dread Radius" setting is stored under a new name,
DreadAuraCloseRadius, so every settings file starts on the new 5 m and
an old saved 12 is ignored. The slider now stops at 15 m, where the
1.5.x line measured the engine's proximity search switching to a scan
of every agent; the v1.4.8 managed switch is the same, its native value
is not measured here.

Research: dump v1.4.8 AgentMoraleInteractionLogic (4 m, 10 allies,
  formation fill); taom-src AgentProximityMap.BeginSearch
Not-tested: a campaign battle against a Nazgul
Save-compat: none; MCM key DreadAuraRadius is now DreadAuraCloseRadius
Ported-from: efa32184b31cd903b49989d603226b3907b10f1b
```

#### Step B2: `f4f12a737` plus the v1.4.8 re-citation of the dread doc

```bash
cd /c/Users/mikew/source/repos/taom-port/130 && git cherry-pick -n f4f12a737c7e020be154ba857b42bcb871e65d96; echo "exit=$?"; git status --porcelain
```

Expected: `UU docs/reviews/lessons/adapters-taleworlds-api.md`, `DU docs/reviews/rca-dread-aura-local-2026-10-15.md`
(the doc, configs doc and binding-test comment apply cleanly after B1, simulated). Resolve:
`git checkout --ours docs/reviews/lessons/adapters-taleworlds-api.md && git add -- docs/reviews/lessons/adapters-taleworlds-api.md`;
`git rm -f -q -- docs/reviews/rca-dread-aura-local-2026-10-15.md`. Then run (every replacement was
dry-run against the simulated result this session; each must match once):

```bash
cd /c/Users/mikew/source/repos/taom-port/130 && python - <<'PY'
import pathlib
p = pathlib.Path("docs/features/dread-aura.md")
raw = p.read_bytes(); crlf = b"\r\n" in raw
t = raw.decode("utf-8").replace("\r\n", "\n")
def sub(old, new):
    global t
    assert t.count(old) == 1, (t.count(old), old[:70]); t = t.replace(old, new)
def block(start, end, new):
    global t
    assert t.count(start) == 1, start; i = t.index(start); j = t.index(end, i) + len(end); t = t[:i] + new + t[j:]
sub("for a hero (v1.5.5 `CharacterObject.cs:619-623`).", "for a hero (v1.4.8 `CharacterObject.cs:613-617`).")
sub("then the morale model (perks,\ntraits and TAOM's culture doctrine), clamped to [15, 100] (v1.5.5 `CommonAIComponent.InitializeMorale`).",
    "then the morale model (perks and\ntraits), clamped to [15, 100] (v1.4.8 `CommonAIComponent.cs:66-76`).")
sub("at any distance (v1.5.5\n`AgentMoraleInteractionLogic.cs:79-84`,", "at any distance (v1.4.8\n`AgentMoraleInteractionLogic.cs:80-85`,")
sub("still counts it in `TroopCasualties` (v1.5.5 `:642-643`).", "still counts it in `TroopCasualties` (v1.4.8 `MapEventSide.cs:689-690`).")
sub("clamped [15, 100] (re-read on v1.5.5) |", "clamped [15, 100] (re-read on v1.4.8, `CommonAIComponent.cs:66-76`) |")
sub("stores it in a property nothing reads, so it changes no behaviour (re-read on v1.5.5) | `Formation.GetMovementTypeOfUnits` (`Formation.cs:1266-1291`), `MissionOrderTroopControllerVM.cs:875`; `Mission.GetMovementTypeOfAgents` (`Mission.cs:5280`, no caller) |",
    "stores it in a property no managed code reads, so it changes no behaviour (re-read on v1.4.8) | `Formation.GetMovementTypeOfUnits` (`Formation.cs:1147-1171`), `MissionOrderTroopControllerVM.cs:875`; `Mission.GetMovementTypeOfAgents` (`Mission.cs:5237`, no caller) |")
sub("for a hero (re-read on v1.5.5) |", "for a hero (re-read on v1.4.8, `CharacterObject.cs:613-617`) |")
sub("(v1.5.5 `BattleAgentLogic.cs:176-179`)", "(v1.4.8 SandBox `BattleAgentLogic.cs:176-179`)")
sub("`PartyGroupAgentOrigin.SetRouted` (`:130-137`) passes it to\n`MapEventSide.OnTroopRouted` (`:633-644`), which",
    "`PartyGroupAgentOrigin.SetRouted` (`:130-137`) passes it through\n`PartyGroupTroopSupplier.OnTroopRouted` (`:121-125`) to `MapEventSide.OnTroopRouted` (`:680-691`), which")
sub("carve-outs, each read on v1.5.5:", "carve-outs, each read on v1.4.8:")
sub("`MapEventSide.cs:636` removes the troop only when", "`MapEventSide.cs:683` removes the troop only when")
sub("casualty tally (`MapEventSide.cs:640-643`).", "casualty tally (`MapEventSide.cs:687-690`).")
sub("in a siege assault: `MapEventSide.cs:636` requires", "in a siege assault: `MapEventSide.cs:683` requires")
block("Three other fear effects have no floor", "so their routs start next to the source.",
    "On 1.4.x nothing else in TAOM lowers an agent's morale: `DreadPulseRunner` is the only morale\n"
    "drain, and the career morale burst only raises allies'. The 1.5.x line also has three fear effects\n"
    "with no floor that can rout a troop the aura weakened (two Signature Strikes and race-ability\n"
    "fear); neither feature exists on 1.4.x. A rout next to a wraith therefore starts from vanilla's own\n"
    "morale rules and spreads by the rule above. The floor of 10 is the 1.5.x maintainer's choice of\n"
    "2026-10-15, adopted for 1.4.x by the 2026-11-04 port programme.")
block("**The radius clamp** was designed in,", "costs a full agent scan on\nevery pulse.",
    "**The radius clamp** was designed in, then removed after reading `Mission.GetNearbyAgentsAux`\n"
    "(v1.4.8 Mission.cs:2314), then restored on 2026-10-15 at 15 m. The managed side is pure native\n"
    "paging, but the 1.5.x line read the native side and found a fallback: `IMBMission.GetNearbyAgentsAux`\n"
    "searches the proximity grid only up to the value `ProximityMapMaxSearchRadius` returns (15.0 on\n"
    "v1.5.5) and walks every agent in the mission beyond it. The v1.4.8 managed half is the same\n"
    "(`Mission.ProximityMapMaxSearchRadius` asks native at `Mission.cs:1713-1716`, and\n"
    "`AgentProximityMap.BeginSearch` loops over every agent above that value, `AgentProximityMap.cs:54-63`),\n"
    "but the native value has not been measured on v1.4.8. So the MCM slider and both providers cap the\n"
    "radius at 15, and a value above it may cost a full agent scan on every pulse.")
p.write_bytes((t.replace("\n", "\r\n") if crlf else t).encode("utf-8"))
d = t.split("\n")
a = next(i for i, l in enumerate(d) if l.startswith("Race names and hero ids are **not**")) + 1
b = next(i for i, l in enumerate(d) if i + 1 >= a and "are pinned instead by `ShippedDreadAuraConfigTests`." in l) + 1
c = pathlib.Path("docs/modding/configs-factions-and-world.md")
raw = c.read_bytes(); cr = b"\r\n" in raw; u = raw.decode("utf-8").replace("\r\n", "\n")
assert u.count("dread-aura.md:287-291") == 1
u = u.replace("dread-aura.md:287-291", "dread-aura.md:%d-%d" % (a, b))
c.write_bytes((u.replace("\n", "\r\n") if cr else u).encode("utf-8"))
print("ok", a, b)
PY
```

Then set the counts in the `## Tests` section of `dread-aura.md` from runs: the total line (B1
left the 1.4.x `161 tests, all green as part of a 6,615-test suite.`) becomes
`` <N> tests (`dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter FullyQualifiedName~TAOM.Tests.Features.DreadAura`, <today>). ``
with N = `Passed + Skipped` of that filter, and each `(<n>)` after `DreadAuraServiceTests`,
`DreadAuraConfigProviderTests`, `ShippedDreadAuraConfigTests` and `DreadAuraSettingsProviderTests`
becomes the count of a `--filter "FullyQualifiedName~<ClassName>"` run.

**Verify**: the script prints `ok <a> <b>`; `grep -c 'v1\.5\.5' docs/features/dread-aura.md` prints
`1`; `grep -c -E 'native_decompile|0xB2E120|doctrine|Scream|Slam' docs/features/dread-aura.md`
prints `0`; staged list is `M` for `TAOM.Tests/Features/DreadAura/DreadAuraBindingTests.cs`,
`docs/features/dread-aura.md`, `docs/modding/configs-factions-and-world.md`; Docs `ai_dashes: 0`.
Commit:

```text
docs(dread): v2.0.28 - correct the rout chain from the v1.4.8 engine

The Aura of Dread doc now describes how a routed troop really leaves a
party, read from the v1.4.8 engine: only a panicked troop is lost; a
troop that leaves on a retreat order keeps its place but still counts
as a casualty; deserters come only from the defeated side; and siege
defenders are exempt only in a siege assault, not in a sally-out. It
also corrects three engine claims: the 30% formation "Flee" value
changes no behaviour (no managed code reads it), only a direct Retreat()
bypasses the panic handler's bracket, and a direct Panic() skips the
morale model's veto and the siege-ladder rule. Every engine line the
1.5.x text read on v1.5.5 is re-cited to v1.4.8, and the fear effects of
features 1.4.x does not have are dropped.

Research: taom-src v1.4.8 MapEventSide.OnTroopRouted,
  MapEventParty.OnTroopRouted, PartyGroupAgentOrigin.SetRouted,
  CommonAIComponent, CharacterObject.GetMoraleResistance; dump
  BattleAgentLogic, BattleEndLogic, PartyGroupTroopSupplier, Formation,
  DesertersCampaignBehavior
Not-tested: docs and a test comment only
Ported-from: f4f12a737c7e020be154ba857b42bcb871e65d96
```

#### Step B3: the morale-write rule (`1e62adeb2`, by hand)

- `.ai/scopes.md`, the `managed` row: after `[Architecture](../.claude/rules/csharp-architecture.md), `
  insert `[patterns](../.claude/rules/csharp-patterns.md), ` (skip if already present).
- `.claude/rules/csharp-patterns.md`: append to the `## Anti-Patterns` list
  `` - A local fear effect that drains morale below 0.01 with nothing said: each rout spreads a morale loss to up to 10 allies, from the whole formation when fewer stand within 4 m. Floor the morale, or say in the feature doc that its routs spread ([lesson](../../docs/reviews/lessons/adapters-taleworlds-api.md)) ``
  (the lesson file exists on 1.4.x; its entry arrives with WP142).

**Verify**: `grep -c 'csharp-patterns.md' .ai/scopes.md` prints `1`; staged `.ai/scopes.md`,
`.claude/rules/csharp-patterns.md`. Commit:

```text
chore(ai): v2.0.28 - a local fear effect floors morale or says so

csharp-patterns.md gains one anti-pattern from the dread aura fix: a
local fear effect that drains morale below 0.01 must floor the morale
or say in its feature doc that its routs spread, because each rout
passes a morale loss to up to 10 allies, from the whole formation when
fewer stand within 4 m. .ai/scopes.md links csharp-patterns.md from the
managed lane so other AI clients read it too.

Not-tested: docs only
Ported-from: 1e62adeb2029dc9e00a86551cb3d01432db81f50
```

**Part B done**: Test `Failed: 0`, `Total` = T0 + 22; Gate `exit=0`; three more commits.

---

### Part C: cavalry knockdown parity (`3f75217dc`, adapted by hand)

Taken: the parity values (JSON and compiled defaults), the troll rows, the dropped `uruk_hai` row,
the auto ratio 8 to 6 under a NEW MCM name (WP110 left this row out because 1.4.x still shipped 8),
the matching tests. Not taken: the per-culture charge damage and the three extra charge sliders.

1. RED:
   - `TAOM.Tests/Infrastructure/McmRenamedDefaultsTests.cs`: directly below the
     `"BanditMaxPartiesPerHideout"` row insert
     `            new object[] { "ChargeAutoKnockdownWeightRatio", "ChargeAutoKnockdownRatio", 6 },`.
   - `TAOM.Tests/Features/CombatMechanics/CombatMechanicsConfigProviderTests.cs`: change
     `Assert.AreEqual(5, c.RaceModifiers.Count);` (2 places) to `7`,
     `Assert.AreEqual(8f, c.ChargeKnockdown.AutoKnockdownWeightRatio, 0.0001f);` (3 places) to `6f`,
     `Assert.AreEqual(0.25f, c.ChargeKnockdown.MinPenetrationFactor, 0.0001f);` (1 place) to `1f`
     (count each with `grep -cF` first: 2, 3, 1; otherwise STOP).
   - `ChargeKnockdownServiceTests.cs`: replace the whole method
     `DecideChargeKnockdown_HeavierVictim_ScaledPenetrationFlipsVerdict` with:

```csharp
    [TestMethod]
    public void DecideChargeKnockdown_HeavierVictim_KeepsVanillaParity()
    {
        // #610: with the shipped floor of 1.0 the weight term never scales the penetration below vanilla.
        // vs man: ratio 6.0 -> pen 0.4 -> threshold 20 -> damage 25 floors him. vs an uruk (160): ratio 3.0 ->
        // clamp(0.5, floor 1.0) = 1.0 -> pen 0.4 -> the same 20. With the old floor of 0.25 the uruk needed 40.
        var manResult = _sut.DecideChargeKnockdown(Context(victimWeight: 80, inflictedDamage: 25f));
        var urukResult = _sut.DecideChargeKnockdown(Context(victimWeight: 160, inflictedDamage: 25f));

        Assert.IsTrue(manResult.HasValue && manResult.Value);
        Assert.IsTrue(urukResult.HasValue && urukResult.Value);
    }

    [TestMethod]
    public void DecideChargeKnockdown_MinFactorBelowOne_ShrinksPenetrationForHeavyVictims()
    {
        _config.ChargeKnockdown.MinPenetrationFactor = 0.25f;   // the pre-#610 floor: uruk pen 0.2 -> needs 40
        var vsUruk = CreateSut().DecideChargeKnockdown(Context(victimWeight: 160, inflictedDamage: 25f));

        Assert.IsTrue(vsUruk.HasValue);
        Assert.IsFalse(vsUruk.Value);
    }

    [TestMethod]
    public void DecideChargeKnockdown_AutoRatioSix_FloorsHorseAndManVsManFromAnyAngle()
    {
        // Horse 400 + rider 80 over man 80 is exactly 6.0: a full-speed contact is Branch A and never asks the 0.7-dot flag.
        _settings.ChargeAutoKnockdownWeightRatio.Returns(6f);

        var result = _sut.DecideChargeKnockdown(Context(inflictedDamage: 1f, hasKnockBackFlag: false));

        Assert.IsTrue(result.HasValue && result.Value);
    }

    [TestMethod]
    public void DecideChargeKnockdown_TrollRaceRow_StaysUpAtParityPenetration()
    {
        // Trolls share weight 160 with uruks, so the race row must hold them: 100 * (0.6 * 4 - 0.4) = 200.
        _raceModifiers.Resolve(7).Returns(new RaceCombatModifiers { KnockdownResistanceMultiplier = 4f });

        var result = _sut.DecideChargeKnockdown(Context(victimRaceId: 7, victimWeight: 160, inflictedDamage: 100f));

        Assert.IsTrue(result.HasValue);
        Assert.IsFalse(result.Value);
    }

    [TestMethod]
    public void DecideChargeKnockdown_WeightTermAboveMax_ClampsToTheJsonMaxPenetrationFactor()
    {
        // Branch B only. Ratio 480 / 20 = 24 is 4x neutral; the max of 2.5 caps it: pen 0.1 * 2.5 = 0.25,
        // threshold 100 * (0.6 - 0.25) = 35. Unclamped it would be 0.4 and 30 damage would floor him.
        _settings.ChargeAutoKnockdownWeightRatio.Returns(30f);
        _config.ChargeKnockdown.HorseChargePenetration = 0.1f;
        var sut = CreateSut();

        Assert.IsFalse(sut.DecideChargeKnockdown(Context(victimWeight: 20, inflictedDamage: 30f))!.Value);
        Assert.IsTrue(sut.DecideChargeKnockdown(Context(victimWeight: 20, inflictedDamage: 36f))!.Value);
    }
```

   - `ShippedCombatMechanicsConfigTests.cs`: above `public void ShippedConfig_FileExists()`'s
     `[TestMethod]` insert:

```csharp
    [TestMethod]
    public void ShippedConfig_ChargeKnockdown_IsVanillaParityWithAnyAngleAutoFloor()
    {
        var c = _sut.GetConfig().ChargeKnockdown;

        Assert.AreEqual(6f, c.NeutralWeightRatio, 0.0001f);
        Assert.AreEqual(6f, c.AutoKnockdownWeightRatio, 0.0001f);
        Assert.AreEqual(1f, c.MinPenetrationFactor, 0.0001f);
        Assert.AreEqual(0.4f, c.HorseChargePenetration, 0.0001f);
    }

    [TestMethod]
    public void ShippedConfig_OnlyTrollsDwarvesAndSauronResistAHorse()
    {
        var races = _sut.GetConfig().RaceModifiers;

        Assert.AreEqual(4f, races["cave_troll"].KnockdownResistanceMultiplier, 0.0001f);
        Assert.AreEqual(4f, races["hill_troll"].KnockdownResistanceMultiplier, 0.0001f);
        Assert.AreEqual(2.5f, races["dwarf"].KnockdownResistanceMultiplier, 0.0001f);
        Assert.AreEqual(3f, races["sauron"].KnockdownResistanceMultiplier, 0.0001f);
        Assert.AreEqual(1f, races["uruk_hai"].KnockdownResistanceMultiplier, 0.0001f);
    }

```

   - `TAOM.Tests/Features/CombatMechanics/CombatMechanicsSettingsProviderTests.cs`: add these two
     methods (if the file does not exist, create it with `using Microsoft.VisualStudio.TestTools.UnitTesting;`,
     `using NSubstitute;`, `using TAOM.Features;`, `using TAOM.Features.CombatMechanics;`, namespace
     `TAOM.Tests.Features.CombatMechanics` and `[TestClass] public class CombatMechanicsSettingsProviderTests`):

```csharp
    // The parity default moved 8 to 6 under a new MCM name, so no TAOM.json saved before keeps 8; the MCM and
    // compiled JSON defaults must agree.
    [TestMethod]
    public void McmDefault_ChargeAutoKnockdownRatio_MatchesTheCompiledJsonDefault()
    {
        Assert.AreEqual(6, new TaomSettings().ChargeAutoKnockdownRatio);
        Assert.AreEqual((int)new ChargeKnockdownConfig().AutoKnockdownWeightRatio, new TaomSettings().ChargeAutoKnockdownRatio);
    }

    [TestMethod]
    public void Mcm_ChargeAutoKnockdownRatio_IsTheValueTheProviderReturns()
    {
        var config = Substitute.For<ICombatMechanicsConfigProvider>();
        config.GetConfig().Returns(new CombatMechanicsConfig());
        var sut = new CombatMechanicsSettingsProvider(config, new TaomSettings { ChargeAutoKnockdownRatio = 10 });

        Assert.AreEqual(10f, sut.ChargeAutoKnockdownWeightRatio, 0.0001f);
    }
```

   **Verify (RED)**: Build fails with CS0117/CS1061 naming `ChargeAutoKnockdownRatio`.

2. GREEN:
   - `combat_mechanics_config.json`: `"autoKnockdownWeightRatio": 8.0,` to `6.0`;
     `"minPenetrationFactor": 0.25,` to `1.0`; the `uruk_hai` row to
     `"uruk_hai": { "swingEnergyBonusFactor": 0.10 },` followed by two rows
     `"cave_troll": { "knockdownResistanceMultiplier": 4.0 },` and
     `"hill_troll": { "knockdownResistanceMultiplier": 4.0 }` (keep valid JSON: the last row has no comma).
   - `CombatMechanicsConfig.cs`: `["uruk_hai"] = new RaceCombatModifiers { SwingEnergyBonusFactor = 0.10f, KnockdownResistanceMultiplier = 1.25f },`
     becomes `["uruk_hai"] = new RaceCombatModifiers { SwingEnergyBonusFactor = 0.10f },` plus
     `["cave_troll"] = new RaceCombatModifiers { KnockdownResistanceMultiplier = 4.0f },` and
     `["hill_troll"] = new RaceCombatModifiers { KnockdownResistanceMultiplier = 4.0f },`, under a comment
     `// #610: trolls share weight 160 with uruks, so the race row keeps a troll on its feet against a horse; uruk-hai lost their 1.25 (parity).`;
     `AutoKnockdownWeightRatio { get; set; } = 8f;` to `6f` with comment
     `// #610: 6.0 is horse + man vs man exactly, so a full-speed contact floors a man from any angle. Was 8.`;
     `MinPenetrationFactor { get; set; } = 0.25f;` to `1f` with comment
     `// #610: 1.0, so the weight term never scales the penetration below vanilla for a heavy victim. Was 0.25.`
   - `TaomSettings.cs`: replace the three attribute and property lines that end in
     `public int ChargeAutoKnockdownWeightRatio { get; set; } = 8;` with:

```csharp
    // Renamed from ChargeAutoKnockdownWeightRatio when the knockdown parity port (#610) moved its default from 8 to
    // 6: MCM loads the saved value by name, so under the old name every TAOM.json saved before kept 8. Never rename
    // it back; files not yet re-saved still hold ChargeAutoKnockdownWeightRatio = 8.
    [SettingPropertyGroup("Combat Mechanics")]
    [SettingPropertyInteger("Auto-Knockdown Weight Ratio", 2, 30, Order = 10, RequireRestart = false,
        HintText = "Charger-to-victim weight ratio at which a charge ALWAYS knocks the victim down, from any angle, regardless of resistance (mumakil vs man is ~125, horse + man vs man exactly 6). Default 6, so a full-speed horse contact floors a man; raise it to 7 or 8 to keep that for heavier chargers only. Values below the neutral weight ratio (6) are treated as the neutral ratio. A value saved by an earlier TAOM version was reset to this default; if you had chosen your own, set it again.")]
    public int ChargeAutoKnockdownRatio { get; set; } = 6;
```

   - `CombatMechanicsSettingsProvider.cs`: `var mcm = Settings?.ChargeAutoKnockdownWeightRatio;` to
     `var mcm = Settings?.ChargeAutoKnockdownRatio;`; `SettingClamp.Clamp(mcm.Value, 8, neutralFloor, 30)`
     to `SettingClamp.Clamp(mcm.Value, 6, neutralFloor, 30)`. The provider and interface member
     `ChargeAutoKnockdownWeightRatio` keeps its name (as on 1.5.x).
   - Any test that assigns or reads the TaomSettings property by its old name (the compiler lists
     them, for example a WP123 row `s => s.ChargeAutoKnockdownWeightRatio = 10`) uses
     `ChargeAutoKnockdownRatio`; reads through the provider (`p.`, `sut.`, `_settings.`) stay. A test
     that pins the provider's invalid-value fallback at 8 now expects 6.
3. Docs, `docs/features/combat-mechanics.md`: `### Charge knockdown formula (v1)` becomes `(v2, #610)`;
   in the formula block, the `Branch A:` line becomes
   `Branch A: weightRatio >= auto (MCM ChargeAutoKnockdownRatio, default 6) && speedFactor >= 0.4 -> knock down (ignores the 0.7-dot gate)`
   and `clamp(weightRatio / 6.0, 0.25, 2.5)` becomes `clamp(weightRatio / 6.0, 1.0, 2.5)`; replace the
   paragraph that starts `Calibration: neutral 6.0` with:

```markdown
Calibration (v2, #610, ported from the 1.5.x line): neutral 6.0 is Native horse 400 + rider 80 over a human 80, so an ordinary horse against a man meets vanilla's rule. The v1 floor of 0.25 scaled the penetration down for every victim heavier than the charger; the 1.5.x line measured that a horse-vs-orc knockdown then needed 30 to 52 damage on hits that deal 5 to 15. v2: `minPenetrationFactor` 1.0, so the weight term never pushes a light charger below vanilla (lighter victims keep the bonus up to 2.5x); `autoKnockdownWeightRatio` 6, so a full-speed horse and man contact floors a man from any angle; the race rows do the resisting: `cave_troll` and `hill_troll` 4.0 (a threshold near 200 damage, above any horse; a mumak is Branch A), `dwarf` 2.5, `sauron` 3.0, and `uruk_hai` no longer 1.25. Vanilla for reference (v1.4.8 `MissionCombatMechanicsHelper.cs:54-59`, `:74-90`, `:336-340`): a horse charge knocks back only on a head-on contact (dot product at least 0.7) and knocks down only then, when damage >= maxHP x max(0, knockdown resistance - 0.4). Branch B's `false` is still an owned verdict; `ChargeKnockdownContext` stays the extension point for future factors.
```

   and in the `MCM:` line replace `` `ChargeAutoKnockdownWeightRatio` slider `` with
   `` `ChargeAutoKnockdownRatio` slider (renamed from `ChargeAutoKnockdownWeightRatio` when its default moved from 8 to 6, so the 6 reaches saved settings) ``.

**Verify**: Build exit 0; Filtered `--filter "FullyQualifiedName~TAOM.Tests.Features.CombatMechanics|FullyQualifiedName~McmRenamedDefaults|FullyQualifiedName~SettingsFingerprintTests"`
0 failed; Gate `exit=0`; `git grep -n 'public int ChargeAutoKnockdownWeightRatio' -- Main` prints
nothing; Docs `ai_dashes: 0`. Stage the JSON, `CombatMechanicsConfig.cs`,
`CombatMechanicsSettingsProvider.cs`, `TaomSettings.cs`, `McmRenamedDefaultsTests.cs`, the four
CombatMechanics test files, any test the compiler made you touch, `combat-mechanics.md`. Commit:

```text
feat(combat): v2.0.28 - cavalry knockdown parity (#610)

Cavalry knocks down like vanilla again. The charge knockdown rule scaled
vanilla's 0.4 penetration by clamp(weightRatio / 6, 0.25, 2.5), so every
victim heavier than the charger got less than vanilla's penetration;
with TAOM's heavy orcs, uruks and trolls the 1.5.x line measured a
horse-vs-orc knockdown needing 30 to 52 damage on hits that deal 5 to
15. The floor is now 1.0, so the weight term never pushes a light
charger below vanilla, and the race rows do the resisting: cave and
hill trolls 4.0 (new), dwarves 2.5 and Sauron 3.0 (unchanged), and
uruk-hai lose their 1.25. The auto-knockdown ratio drops from 8 to 6,
so a full-speed horse and man contact (ratio exactly 6) floors a man
from any angle.

The MCM slider "Auto-Knockdown Weight Ratio" is stored under a new
name, ChargeAutoKnockdownRatio, so the new default of 6 reaches every
player; a value saved before is reset and must be set again if it was
chosen. The per-culture charge damage and the three extra charge
sliders of the 1.5.x change are not part of this port.

Research: taom-src v1.4.8 MissionCombatMechanicsHelper (horse charge
  knock-back and knock-down)
Not-tested: cavalry into orcs, uruks, trolls and dwarves in game
Save-compat: none; MCM key ChargeAutoKnockdownWeightRatio is now
  ChargeAutoKnockdownRatio
Ported-from: 3f75217dcde84c96974dffa0977f0c3403cec1da
```

**Part C done**: Test `Failed: 0`, `Total` = T0 + 30; Gate `exit=0`.

---

### Part D: Custom Battle runs TAOM's combat mechanics (`b0154d86e`, re-implemented on WP127's facade)

#### Step D0: facade check (decides whether Part D runs)

```bash
cd /c/Users/mikew/source/repos/taom-port/130 && f=Main/Features/CombatMechanics/Hooks/CombatMechanicsHooks.cs; test -f $f && echo facade-present; grep -c '^namespace TAOM.Features.CombatMechanics.Hooks;' $f; \
for m in 'bool? CrushThrough(' 'float? CleaveMomentum(' 'MeleeCollisionReaction CollisionReaction(' 'bool IsUnstoppable(' 'float StaggerThreshold(' 'bool? ChargeKnockdown(' 'WeaponFlags PenetrationFlags(' 'float ShieldDamage(' 'float? HorseChargePenetration('; do printf '%s  %s\n' "$(grep -cF "public $m" $f)" "$m"; done; \
grep -c 'CombatMechanicsHooks' Main/Features/CombatMechanics/CombatMechanicsIoC.cs; \
grep -cF 'campaignStarter.AddModel<AgentApplyDamageModel>(new TaomCombatMechanicsModel(' Main/SubModule.cs; \
grep -cF 'IoC.Resolve<Features.CombatMechanics.Hooks.CombatMechanicsHooks>()' Main/SubModule.cs
```

**Verify**: `facade-present`, `1`, nine lines starting `1`, then at least `1`, `1`, `1`. If the facade
is missing, a method line prints `0` or the IoC count is `0`: **Part D is BLOCKED** (WP127 absent or
shaped differently). Skip to Part E and report it; do not build a facade here. If only the last
line prints `0`, continue, and in step D1.1 make the registration-collaborator test assert only that
the twin's registration resolves `IoC.Resolve<Features.CombatMechanics.Hooks.CombatMechanicsHooks>()`.

#### Step D1: the twin (RED, then GREEN)

1. RED: `git show b0154d86ea126363c23f9190d7e45a8891d5681e:TAOM.Tests/Features/CombatMechanics/CustomBattleDamageModelTests.cs > TAOM.Tests/Features/CombatMechanics/CustomBattleDamageModelTests.cs`, then edit:
   - `ExpectedOverrides`: exactly `"DecideCrushedThrough", "CalculateRemainingMomentum", "DecideWeaponCollisionReaction",
     "DecideAgentShrugOffBlow", "CalculateStaggerThresholdDamage", "DecideAgentKnockedDownByBlow",
     "DecideMissileWeaponFlags", "CalculateShieldDamage", "GetHorseChargePenetration"`.
   - `ExpectedCallOrder`: exactly these nine rows:
     `DecideCrushedThrough` = `"CombatMechanicsHooks.CrushThrough,base"`, `CalculateRemainingMomentum` =
     `"CombatMechanicsHooks.CleaveMomentum,base"`, `DecideWeaponCollisionReaction` =
     `"base,CombatMechanicsHooks.CollisionReaction"`, `DecideAgentShrugOffBlow` =
     `"base,CombatMechanicsHooks.IsUnstoppable"`, `CalculateStaggerThresholdDamage` =
     `"base,CombatMechanicsHooks.StaggerThreshold"`, `DecideAgentKnockedDownByBlow` =
     `"CombatMechanicsHooks.ChargeKnockdown,base"`, `DecideMissileWeaponFlags` =
     `"base,CombatMechanicsHooks.PenetrationFlags"`, `CalculateShieldDamage` =
     `"base,CombatMechanicsHooks.ShieldDamage"`, `GetHorseChargePenetration` =
     `"CombatMechanicsHooks.HorseChargePenetration,base"`.
   - Below the `CampaignOnly` field add
     `private static readonly HashSet<string> CampaignOnlyOverrides = new(StringComparer.Ordinal) { "ApplyDamageReductions" };`
     and in `Source_OverridesExactlyWhatTheCampaignModelOverrides` use
     `var campaign = DeclaredOverrides(CampaignSource).Where(n => !CampaignOnlyOverrides.Contains(n)).ToArray();`
     (the campaign model's `ApplyDamageReductions` exists only for the Refuge reduction).
   - In `Source_DerivesFromTheCustomBattleBase_NeverFromSandBox` change `(SandboxAgentApplyDamageModel :641-644)`
     to `(v1.4.8 SandboxAgentApplyDamageModel.cs:509-512)`.
   - `Source_HandsEachSeamItsOwnAgents`: delete every `DataRow` that names `ApplyDamageReductions`,
     `ApplyDamageAmplifications`, `ApplyDamageScaling`, `DecideAgentKnockedBackByBlow`,
     `RaceAbilityHooks`, `CreatureBanditDamage`, `CreatureSiegeHooks` or `SignatureStrikeVerdicts`
     (15 rows remain).
   - Replace `Source_AsksTheRaceVerdictBeforeTheCombatRules` with:

```csharp
    [TestMethod]
    public void Source_AsksTheCombatRulesBeforeTheEnginesOwn()
    {
        var text = OverrideText("DecideCrushedThrough");

        Assert.IsTrue(text.IndexOf("_combat.CrushThrough(", StringComparison.Ordinal)
                      < text.IndexOf("base.DecideCrushedThrough(", StringComparison.Ordinal),
            "the combat rules decide before the engine's own flag-and-energy rule");
    }
```

   - `Registration_ResolvesTheSameCollaboratorsTheCampaignRegistrationDoes`: the resolve list holds only
     `"IoC.Resolve<Features.CombatMechanics.Hooks.CombatMechanicsHooks>()"` (see D0 for the looser form).
   - Delete `RegisterCustomBattleModels_RunsOutsideTheCampaignBranch_BeforeTheModuleStep` (A2 pins it).
   - Replace `BuildTwin()` with (builds the 1.4.x facade whatever its constructor):

```csharp
    private static (object Model, ICombatMechanicsSettingsProvider Settings, ICreatureCombatService Creature) BuildTwin()
    {
        var settings = Substitute.For<ICombatMechanicsSettingsProvider>();
        var creature = Substitute.For<ICreatureCombatService>();
        var ctor = typeof(CombatMechanicsHooks).GetConstructors().Single();
        var args = ctor.GetParameters().Select(p =>
            p.ParameterType == typeof(ICombatMechanicsSettingsProvider) ? settings
            : p.ParameterType == typeof(ICreatureCombatService) ? (object)creature
            : Substitute.For(new[] { p.ParameterType }, new object[0])).ToArray();
        return (Activator.CreateInstance(Twin, ctor.Invoke(args))!, settings, creature);
    }
```

   - Replace `HorseChargePenetration_FeatureOn_IsTheSetting_AndOff_IsCustomBattlesOwn0Point4` with:

```csharp
    [TestMethod]
    [TestCategory("BindingVerification")]
    public void HorseChargePenetration_FeatureOff_IsCustomBattlesOwn0Point4()
    {
        RequireGameAssemblies();
        var (model, settings, _) = BuildTwin();
        settings.ChargeKnockdownEnabled.Returns(false);

        Assert.AreEqual(0.4f, new AgentApplyDamageModelAccess(model).HorseChargePenetration(), "off falls through to the Custom Battle base");
    }
```

   **Verify (RED)**: Build exit 0; Filtered `--filter "FullyQualifiedName~CustomBattleDamageModelTests"`
   reports failures (the twin does not exist yet).

2. GREEN: create `Main/Features/CombatMechanics/Models/TaomCustomBattleDamageModel.cs`:

```csharp
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TAOM.Features.CombatMechanics.Hooks;

namespace TAOM.Features.CombatMechanics.Models;

// Custom Battle's twin of TaomCombatMechanicsModel (#788). Custom Battle installs the engine's CustomAgentApplyDamageModel
// (v1.4.8 CustomGame.InitializeGameModels). The campaign model derives from SandboxAgentApplyDamageModel, whose
// ApplyDamageReductions reads DefaultSkillEffects.MountedWeaponDamagePenalty for a mounted attacker; that resolves
// through Campaign.Current (v1.4.8 SandboxAgentApplyDamageModel.cs:509-512, DefaultSkillEffects.cs:94 and :126), null
// outside a campaign. So this extends the Custom Battle base and makes the campaign model's CombatMechanicsHooks calls,
// in the same order. SubModule.RegisterCustomBattleModels adds it on a BasicGameStarter.
//
// Campaign-only, so absent here: the career passives (TaomAgentApplyDamageModel reads heroes) and the Refuge reduction
// (reads the defender's party). Where the Custom Battle base answers differently from SandBox's, TAOM's delegate sits on
// the Custom Battle answer: docs/features/combat-mechanics.md, "Custom Battle".
public class TaomCustomBattleDamageModel : CustomAgentApplyDamageModel
{
    private readonly CombatMechanicsHooks _combat;

    public TaomCustomBattleDamageModel(CombatMechanicsHooks combat)
    {
        _combat = combat;
    }

    // The combat rules first, then the engine's own: Custom Battle's base crushes through only for a CanCrushThrough
    // weapon swung overhead with energy above 58, x1.2 against a shield (v1.4.8 CustomAgentApplyDamageModel.cs:115-133).
    public override bool DecideCrushedThrough(Agent attackerAgent, Agent defenderAgent, float totalAttackEnergy, Agent.UsageDirection attackDirection, StrikeType strikeType, WeaponComponentData defendItem, bool isPassiveUsageHit)
        => _combat.CrushThrough(attackerAgent, defenderAgent, totalAttackEnergy, attackDirection, strikeType, defendItem, isPassiveUsageHit)
           ?? base.DecideCrushedThrough(attackerAgent, defenderAgent, totalAttackEnergy, attackDirection, strikeType, defendItem, isPassiveUsageHit);

    public override float CalculateRemainingMomentum(float originalMomentum, in Blow b, in AttackCollisionData collisionData, Agent attacker, Agent victim, in MissionWeapon attackerWeapon, bool isCrushThrough)
        => _combat.CleaveMomentum(attacker, originalMomentum, in collisionData)
           ?? base.CalculateRemainingMomentum(originalMomentum, in b, in collisionData, attacker, victim, in attackerWeapon, isCrushThrough);

    public override void DecideWeaponCollisionReaction(in Blow registeredBlow, in AttackCollisionData collisionData, Agent attacker, Agent defender, in MissionWeapon attackerWeapon, bool isFatalHit, bool isShruggedOff, float momentumRemaining, out MeleeCollisionReaction colReaction)
    {
        base.DecideWeaponCollisionReaction(in registeredBlow, in collisionData, attacker, defender, in attackerWeapon, isFatalHit, isShruggedOff, momentumRemaining, out colReaction);
        colReaction = _combat.CollisionReaction(attacker, momentumRemaining, in collisionData, colReaction);
    }

    // Base = the engine's stagger check (it asks our CalculateStaggerThresholdDamage through the registered model);
    // then creature unstoppability.
    public override bool DecideAgentShrugOffBlow(Agent victimAgent, in AttackCollisionData collisionData, in Blow blow)
        => base.DecideAgentShrugOffBlow(victimAgent, in collisionData, in blow)
           || _combat.IsUnstoppable(victimAgent, in collisionData);

    // The per-race multiplier scales Custom Battle's flat managed-parameter threshold (v1.4.8 :257-261).
    public override float CalculateStaggerThresholdDamage(Agent defenderAgent, in Blow blow)
        => _combat.StaggerThreshold(defenderAgent, base.CalculateStaggerThresholdDamage(defenderAgent, in blow));

    // The charge rules (horse charges only), then the engine's.
    public override bool DecideAgentKnockedDownByBlow(Agent attackerAgent, Agent victimAgent, in AttackCollisionData collisionData, WeaponComponentData attackerWeapon, in Blow blow)
        => _combat.ChargeKnockdown(attackerAgent, victimAgent, in collisionData, in blow)
           ?? base.DecideAgentKnockedDownByBlow(attackerAgent, victimAgent, in collisionData, attackerWeapon, in blow);

    // The Custom Battle base grants no flag (v1.4.8 :111-113), so the shield-penetration lists start from the weapon's own.
    public override void DecideMissileWeaponFlags(Agent attackerAgent, in MissionWeapon missileWeapon, ref WeaponFlags missileWeaponFlags)
    {
        base.DecideMissileWeaponFlags(attackerAgent, in missileWeapon, ref missileWeaponFlags);
        missileWeaponFlags = _combat.PenetrationFlags(in missileWeapon, missileWeaponFlags);
    }

    // The Custom Battle base multiplies by 1.25 and applies the banner reduction (v1.4.8 :294-305).
    public override float CalculateShieldDamage(in AttackInformation attackInformation, float baseDamage)
        => _combat.ShieldDamage(in attackInformation, base.CalculateShieldDamage(in attackInformation, baseDamage));

    // Feature off = the engine's own value, 0.4 in Custom Battle too (v1.4.8 :252-255).
    public override float GetHorseChargePenetration()
        => _combat.HorseChargePenetration() ?? base.GetHorseChargePenetration();
}
```

   If a facade parameter differs in passing (for example `AttackCollisionData` by value, not `in`),
   match the call to the facade; never edit the facade.
3. `SubModule.cs`, inside `RegisterCustomBattleModels` after the banner model, add:

```csharp
        // CombatMechanics (#788): the damage rules run in Custom Battle too, through the facade the campaign model calls.
        // The twin extends the engine's Custom Battle base, never SandBox's (a mounted hit would throw outside a campaign).
        basicStarter.AddModel<AgentApplyDamageModel>(new TaomCustomBattleDamageModel(
            IoC.Resolve<Features.CombatMechanics.Hooks.CombatMechanicsHooks>()));
```

   and in its summary change `Mirrored: the banner bearers' race gate.` to
   `Mirrored: the banner bearers' race gate and the Combat Mechanics damage rules (TaomCustomBattleDamageModel).`
4. Run Filtered `--filter "FullyQualifiedName~CustomBattleDamageModelTests"`. If only
   `EachOverride_MakesTheSameHookCallsInTheSameOrder_AsTheCampaignModel` and the documented-order row
   fail for one override (WP127 kept a gate in the campaign override, for example an early
   `if (!collisionData.IsHorseCharge) return base...`), give the twin's override the campaign
   override's exact structure, set that `ExpectedCallOrder` row and its source `DataRow`s to what the
   campaign model does, and say so in the hand-off. Any other failure: fix within two attempts, else STOP.
5. Docs: `combat-mechanics.md`, below the charge knockdown section add:

```markdown
### Custom Battle

Custom Battle (and the editor's test battle) installs the engine's `CustomAgentApplyDamageModel` on a `BasicGameStarter` (v1.4.8 `CustomGame.InitializeGameModels`). The campaign model derives from `SandboxAgentApplyDamageModel`, whose `ApplyDamageReductions` reads `DefaultSkillEffects.MountedWeaponDamagePenalty` for a mounted attacker through `Campaign.Current` (v1.4.8 `SandboxAgentApplyDamageModel.cs:509-512`), null outside a campaign. So Custom Battle gets a twin, `TaomCustomBattleDamageModel` on the Custom Battle base, registered from `SubModule.RegisterCustomBattleModels`. It makes the campaign model's `CombatMechanicsHooks` calls in the same order; `CustomBattleDamageModelTests` fails when the campaign model gains an override or a hook call the twin lacks. Campaign-only by design: the career damage passives (they read heroes) and the Refuge reduction (it reads the defender's party). The MCM toggles apply in both modes.

Where the Custom Battle base answers differently (v1.4.8 `CustomAgentApplyDamageModel.cs`), the same delegate lands on a different number: crush-through falls back to a stricter base that needs a `CanCrushThrough` weapon (`:115-133`); the stagger threshold is the flat managed parameter (`:257-261`); shield damage is multiplied by 1.25 before the runtime correction (`:294-305`); the missile flags start with no perk grant (`:111-113`). Custom Battle results measured before this change are pre-#788 baselines.
```

   add a Key Files row
   ``| `Main/Features/CombatMechanics/Models/TaomCustomBattleDamageModel.cs` | Custom Battle's twin (#788) on `CustomAgentApplyDamageModel`: the nine 1.4.x Combat Mechanics overrides, no career passives, no Refuge; pinned by `CustomBattleDamageModelTests` |``;
   `gamemodel-registry.md`: below the `TaomCombatMechanicsModel` row add
   ``| `TaomCustomBattleDamageModel` | `CustomAgentApplyDamageModel` | CombatMechanics in Custom Battle (#788): the campaign model's `CombatMechanicsHooks` calls in the same order on the Custom Battle base; career passives and Refuge stay campaign-only; added from `SubModule.RegisterCustomBattleModels` |``;
   `.claude/rules/gamemodels.md`: below the `TaomCombatMechanicsModel` row add
   ``| `TaomCustomBattleDamageModel` | `CustomAgentApplyDamageModel` | `CombatMechanics` (Custom Battle) |``
   and raise both count claims by one (same check as A2).

**Verify**: Build exit 0; Filtered `--filter "FullyQualifiedName~TAOM.Tests.Features.CombatMechanics|FullyQualifiedName~GameModelOverrideBindingTests|FullyQualifiedName~BannerBearerRaceGateWiringTests"`
0 failed; Docs `model_registry` equals Step 0.7. Stage the twin, the test, `SubModule.cs`,
`combat-mechanics.md`, `gamemodel-registry.md`, `.claude/rules/gamemodels.md`. Commit:

```text
fix(combat): v2.0.28 - Custom Battle runs TAOM's combat mechanics

TAOM's Combat Mechanics rules now apply in Custom Battle too (#788).
Before, only the campaign model carried them, so a Custom Battle used
vanilla rules for skill, monster and orc shield crush-through, creature
cleave and stagger immunity, the per-race stagger threshold, the
weight-driven charge knockdown, shield penetration and the charge
penetration.

TaomCustomBattleDamageModel extends the engine's Custom Battle damage
model, because the campaign model's SandBox base reads Campaign.Current
on a mounted hit (v1.4.8 SandboxAgentApplyDamageModel, the mounted
weapon penalty), and makes the campaign model's CombatMechanicsHooks
calls in the same order. RegisterCustomBattleModels adds it on the
BasicGameStarter. Career passives and the Refuge reduction stay
campaign-only: they need heroes and parties. A new test fails when the
campaign model gains an override or a hook call the twin lacks.

Research: taom-src v1.4.8 CustomAgentApplyDamageModel,
  SandboxAgentApplyDamageModel.ApplyDamageReductions; dump
  DefaultSkillEffects, CustomGame.InitializeGameModels
Not-tested: crush-through, cleave, stagger and charge knockdown in a
  Custom Battle
Ported-from: b0154d86ea126363c23f9190d7e45a8891d5681e
```

**Part D done**: Test `Failed: 0`, `Total` = T0 + 75.

---

### Part E: warg riders and the battle action bar (`726975a52`, `c91380274`, `d96f349f1`, `40385aa5b`)

#### Step E1: late-spawned warg riders keep their formation (`726975a52`)

1. RED: `git show 726975a529427d6e68f3240d3c2eb8b39d8b0e5e:TAOM.Tests/Features/Warg/WargRageFormationTests.cs | sed 's/(see the fords-of-war build spec)/(plan 130, In-game checks)/' > TAOM.Tests/Features/Warg/WargRageFormationTests.cs`
   **Verify**: Build exit 0; Filtered `WargRageFormationTests` shows 2 failures.
2. GREEN: `git show 726975a529427d6e68f3240d3c2eb8b39d8b0e5e -- Main/Features/Warg/BehaviorTreeElements/PrepareOnFirstCall.cs Main/Features/Warg/BehaviorTreeElements/CleanIfEnemyDied.cs | git apply`
3. Docs, `warg-combat.md`, "Rage Mode Flow": `4. AI: rider leaves formation, warg navigates to enemy` becomes
   `4. AI: rider leaves formation (its live formation is stored first and restored when the rage ends or the warg dies; a warg spawned after the first scan used to store null), warg navigates to enemy`.

**Verify**: Filtered `--filter "FullyQualifiedName~TAOM.Tests.Features.Warg"` 0 failed; staged the two
Warg files, the test, `warg-combat.md`. Commit:

```text
fix(warg): v2.0.28 - late-spawned warg riders keep their formation

A warg's tree stores its rider's formation when it is built, but for a
warg that joins after the first scan the tree is built in the mount's
OnAgentBuild, before the rider is built, so it stored null (v1.4.8
Mission.SpawnAgent builds the mount first, and Agent.Build sets a
rider's formation). When that warg raged or died, the reset wrote the
null back and the rider left his formation for the rest of the battle.
The rage start now stores the rider's live formation before clearing
it, and the reset writes a formation only when one was stored.

Research: taom-src v1.4.8 Mission.SpawnAgent, Agent.Build
Not-tested: warg reinforcements raging, and losing their warg, in a
  campaign battle
Ported-from: 726975a529427d6e68f3240d3c2eb8b39d8b0e5e
```

#### Step E2: the rage controller swap tolerates a missing controller (`c91380274`'s guard)

1. RED: create `TAOM.Tests/Features/Warg/WargControllerNullGuardTests.cs`:

```csharp
using System;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.Warg;

/// <summary>
/// The warg rage controller is a [DefaultView] the engine builds only for a mission with a screen, and SubModule no
/// longer adds a second one (#970). A mission without it must switch nothing instead of disabling vanilla's controller
/// and then throwing (the guard the 1.5.x line added in c91380274). Mission is engine state, so the guards are pinned
/// in source: every lookup is null-checked before its first use.
/// </summary>
[TestClass]
public class WargControllerNullGuardTests
{
    private static readonly Regex Lookup = new Regex(
        @"(?:var|AutonomousMovementPlayerController)\s+(\w+)\s*=\s*[\w.?]*GetMissionBehavior<AutonomousMovementPlayerController>\(\);");

    private static string Squashed(string path) =>
        Regex.Replace(RepoPaths.ReadSource(path, stripComments: true), @"\s+", " ");

    private static int Pos(string text, string needle)
    {
        var i = text.IndexOf(needle, StringComparison.Ordinal);
        return i < 0 ? int.MaxValue : i;
    }

    [DataTestMethod]
    [DataRow("Main/Features/Warg/WargMissionBehavior.cs")]
    [DataRow("Main/Features/Warg/WargRiderHandManager.cs")]
    [DataRow("Main/Features/Warg/BehaviorTreeElements/PrepareOnFirstCall.cs")]
    [DataRow("Main/Features/Warg/BehaviorTreeElements/FinishRageMode.cs")]
    public void EveryRageControllerLookup_IsNullCheckedBeforeItsFirstUse(string path)
    {
        var src = Squashed(path);
        var lookups = Lookup.Matches(src);
        Assert.IsTrue(lookups.Count > 0, path + " no longer looks the rage controller up; update this pin");
        foreach (Match m in lookups)
        {
            var name = m.Groups[1].Value;
            var rest = src.Substring(m.Index + m.Length);
            var use = rest.IndexOf(name + ".", StringComparison.Ordinal);
            var guard = Math.Min(Pos(rest, "if (" + name + " == null)"), Pos(rest, "if (" + name + " != null)"));
            Assert.IsTrue(use < 0 || guard < use, $"{path}: '{name}' is used before a null check");
        }
    }

    [TestMethod]
    public void SwitchMainAgentController_ReturnsBeforeTouchingVanillasController()
    {
        var src = Squashed("Main/Features/Warg/WargMissionBehavior.cs");
        var guard = Pos(src, "if (noMovementController == null) return;");
        Assert.IsTrue(guard < int.MaxValue, "no null guard on the rage controller");
        Assert.IsTrue(guard < Pos(src, "baseController.Disable();"), "vanilla's controller is switched before the guard");
    }
}
```

   **Verify (RED)**: Filtered `WargControllerNullGuardTests` shows 5 failures.
2. GREEN:
   - `WargMissionBehavior.SwitchMainAgentController`: replace its body with (keep the signature):

```csharp
    // A mission without the rage controller (a [DefaultView], built only for a mission with a screen) switches nothing
    // and leaves the player on vanilla's controller. Before, this disabled vanilla's controller and then threw on the
    // null one (the guard the 1.5.x line added in c91380274).
    public static void SwitchMainAgentController(bool switchToSpecialController)
    {
        AutonomousMovementPlayerController noMovementController = Mission.Current.GetMissionBehavior<AutonomousMovementPlayerController>();
        if (noMovementController == null) return;
        MissionMainAgentController baseController = Mission.Current.GetMissionBehavior<MissionMainAgentController>();
        if (switchToSpecialController)
        {
            baseController.Disable();
            noMovementController.Enable();
        }
        else
        {
            baseController.Enable();
            noMovementController.Disable();
        }
    }
```

   - `PrepareOnFirstCall.cs`: `noMovementController.TargetAgent = AgentHitBy.GetValue();` becomes
     `if (noMovementController != null) noMovementController.TargetAgent = AgentHitBy.GetValue();`.
   - `FinishRageMode.cs`: `noMovementController.TargetAgent = null;` becomes
     `if (noMovementController != null) noMovementController.TargetAgent = null;`.
   - `WargRiderHandManager.cs`: in `ClearCustomLookDirection`,
     `missionMainAgentController.CustomLookDir = Vec3.Zero;` becomes
     `if (missionMainAgentController != null) missionMainAgentController.CustomLookDir = Vec3.Zero;`;
     in `UpdateWargRiderHandle`, directly after its `GetMissionBehavior<AutonomousMovementPlayerController>()`
     line insert `        if (missionMainAgentController == null) return;`. Keep WP124's other changes.

**Verify**: Filtered `--filter "FullyQualifiedName~TAOM.Tests.Features.Warg"` 0 failed; staged the four
Warg files and the test. Commit:

```text
fix(warg): v2.0.28 - the rage controller swap tolerates a missing one

The warg rage controller is a [DefaultView] the engine builds only for a
mission with a screen. Without it, SwitchMainAgentController disabled
vanilla's controller and then threw on the null one, leaving the player
without controls; the tree steps that set its target or look direction
threw too. Every lookup is now null-checked, and the swap returns
before touching vanilla's controller. This lands before the next change,
which stops SubModule adding a second controller instance.

Not-tested: a warg rider raging, then regaining control, in a field
  battle
Ported-from: c9138027466ee2bcdcdcf8a84117abca8a3968ab
```

#### Step E3: the battle action bar registers, keeps combat input, and is parked (`d96f349f1` + `40385aa5b`, one commit)

1. RED, new tests:
   - `git show 40385aa5bdb759eb52d92a30b3a78a194cf000e0:TAOM.Tests/Features/CompanionTactics/BattleActionBar/BattleActionBarInputTests.cs | sed 's/@"Client/@"Main/' > TAOM.Tests/Features/CompanionTactics/BattleActionBar/BattleActionBarInputTests.cs`
   - `git show d96f349f1124ab474eb9b299b7c1389f6d9f8365:TAOM.Tests/Features/CompanionTactics/BattleActionBar/BattleActionBarMissionViewGateTests.cs | sed -e 's/^using TAOM.Client;$//' -e 's/typeof(ClientSubModule)/typeof(SubModule)/' -e '/TestCategory("RequiresGame")/d' > TAOM.Tests/Features/CompanionTactics/BattleActionBar/BattleActionBarMissionViewGateTests.cs`
   - `TAOM.Tests/Infrastructure/MissionViewRegistrationGateTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Infrastructure;

/// <summary>
/// Issue #970. MissionScreen registers views once, in OnMissionAfterStarting (v1.4.8 MissionScreen.cs:3991-4002), which
/// MissionState.FinishMissionLoading calls before Mission.AfterStart runs the submodules' OnMissionBehaviorInitialize
/// (MissionState.cs:343 and :345, Mission.cs:3815). A MissionView TAOM adds there never ticks or finalizes. The engine
/// builds a [DefaultView] with the mission (ViewCreatorManager.CreateDefaultMissionBehaviors, Activator.CreateInstance),
/// so every TAOM MissionView carries exactly one attribute and a public parameterless constructor.
/// </summary>
[TestClass]
public class MissionViewRegistrationGateTests
{
    // Type full name, reason. A view built by a TAOM-owned [ViewMethod] mission list would go here.
    private static readonly Dictionary<string, string> Allowlist = new Dictionary<string, string>();

    [ClassInitialize]
    public static void Init(Microsoft.VisualStudio.TestTools.UnitTesting.TestContext _) =>
        TAOM.Tests.Migration.GameAssemblies.EnsureLoaded();

    private static IEnumerable<Type> LoadableTypes(Assembly assembly)
    {
        try { return assembly.GetTypes(); }
        catch (ReflectionTypeLoadException ex) { return ex.Types.Where(t => t != null)!; }
    }

    [TestMethod]
    public void EveryTaomMissionView_IsADefaultView_WithAParameterlessConstructor()
    {
        var missionView = Assembly.Load("TaleWorlds.MountAndBlade.View")
            .GetType("TaleWorlds.MountAndBlade.View.MissionViews.MissionView", throwOnError: true)!;
        var views = LoadableTypes(typeof(SubModule).Assembly)
            .Where(t => !t.IsAbstract && missionView.IsAssignableFrom(t) && !Allowlist.ContainsKey(t.FullName!))
            .ToList();
        Assert.IsTrue(views.Count >= 2, "expected at least the warg rage controller and the battle action bar");

        var offenders = views
            .Where(t => t.GetCustomAttributesData().Count(a => a.AttributeType.FullName == "TaleWorlds.MountAndBlade.View.DefaultView") != 1
                        || t.GetConstructor(Type.EmptyTypes) == null)
            .Select(t => t.FullName)
            .ToList();
        Assert.AreEqual(0, offenders.Count,
            "These MissionViews lack exactly one [DefaultView] or a public parameterless constructor: " +
            string.Join(", ", offenders) + ". A view added in OnMissionBehaviorInitialize never ticks (#970). " +
            "Mark it [DefaultView], or list it in Allowlist with the reason.");
    }
}
```

   - `TAOM.Tests/Composition/SubModuleMissionViewWiringTests.cs`:

```csharp
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Composition;

/// <summary>
/// Issue #970. A MissionView added in OnMissionBehaviorInitialize is never registered with the MissionScreen (v1.4.8
/// MissionScreen.cs:3991-4002 runs before Mission.cs:3815), so it never ticks. TAOM's two views are [DefaultView]
/// types, and no file under Main constructs either.
/// </summary>
[TestClass]
public class SubModuleMissionViewWiringTests
{
    private static readonly Regex Construct = new Regex(
        @"\bnew\s+(?:[\w.]+\.)?(?:AutonomousMovementPlayerController|BattleActionBarMissionView)\s*\(");

    [TestMethod]
    public void NoFileUnderMain_ConstructsATaomMissionView()
    {
        var offenders = RepoPaths.CSharpFiles("Main")
            .Where(f => Construct.IsMatch(RepoPaths.StripComments(File.ReadAllText(f))))
            .Select(Path.GetFileName)
            .ToList();
        CollectionAssert.AreEqual(new string[0], offenders, "no file under Main may construct a TAOM MissionView");
    }
}
```

   - `BattleActionBarServiceTests.cs`: `git show 40385aa5bdb759eb52d92a30b3a78a194cf000e0 -- TAOM.Tests/Features/CompanionTactics/BattleActionBar/BattleActionBarServiceTests.cs | git apply`.
   - If `TAOM.Tests/Features/CompanionTactics/CompanionTacticsSettingsProviderTests.cs` exists and holds
     `Assert.IsTrue(sut.EnableBattleActionBar, nameof(sut.EnableBattleActionBar));`, make it
     `Assert.IsFalse(sut.ShowBattleActionBar, nameof(sut.ShowBattleActionBar));`.

   **Verify (RED)**: Build fails (CS1061 `ShowBattleActionBar`).
2. GREEN:
   - `git show 40385aa5bdb759eb52d92a30b3a78a194cf000e0 -- Main/Features/CompanionTactics/BattleActionBar/BattleActionBarService.cs Main/Features/CompanionTactics/BattleActionBar/Models/BattleAction.cs Main/Features/CompanionTactics/BattleActionBar/UI/ActionButtonVM.cs Main/Features/CompanionTactics/BattleActionBar/UI/BattleActionBarVM.cs Main/_Module/GUI/PreFabs/BattleActionBar.xml | git apply`
     (no hotkey labels, a guarded click, an immediate highlight re-sync, `DoNotAcceptEvents` on every non-button widget).
   - The view, transplanted byte for byte from the 1.5.x end state (the `Client/` copy differs from
     the 1.4.x file only by `c91380274`'s nullable marks):
     `git show 40385aa5bdb759eb52d92a30b3a78a194cf000e0:Client/Features/CompanionTactics/BattleActionBar/Hooks/BattleActionBarMissionView.cs > Main/Features/CompanionTactics/BattleActionBar/Hooks/BattleActionBarMissionView.cs`
     (`[DefaultView]`, attach on the first Battle-mode tick via `ShouldAttach`, `SetInputRestrictions(false, InputUsageMask.MouseButtons)`,
     `LoadMovie`/`ReleaseMovie`, detach when the setting turns off).
   - `ICompanionTacticsSettingsProvider.cs`: `bool EnableBattleActionBar { get; }` becomes `bool ShowBattleActionBar { get; }`.
     `CompanionTacticsSettingsProvider.cs`: `public bool EnableBattleActionBar => Settings?.EnableBattleActionBar ?? true;`
     becomes `public bool ShowBattleActionBar => Settings?.ShowBattleActionBar ?? false;`.
   - `TaomSettings.cs`: replace the three lines `[SettingPropertyGroup("Battle Tactics/Battle Action Bar", GroupOrder = 29)]`,
     `[SettingPropertyBool("Enable Battle Action Bar", ...` (two lines) and `public bool EnableBattleActionBar { get; set; } = true;` with:

```csharp
    // Renamed from EnableBattleActionBar (default on) in #970. MCM keeps a saved value per property, so flipping that
    // default would have reached fresh installs only; a new property name reaches every install. The old key in
    // TAOM.json is ignored.
    [SettingPropertyGroup("Battle Tactics/Battle Action Bar", GroupOrder = 29)]
    [SettingPropertyBool("Show Battle Action Bar", Order = 0, RequireRestart = false,
        HintText = "Parked: show a bar of stance buttons during field battles. The buttons record a stance but do not change formation behavior yet, and they can be clicked only while a menu shows the cursor (for example Esc). Default: off.")]
    public bool ShowBattleActionBar { get; set; } = false;
```

   - `Main/SubModule.cs` (single-owner): delete `        AddTaomBehavior(new AutonomousMovementPlayerController());`
     and `        AddTaomBehavior(new Features.CompanionTactics.BattleActionBar.Hooks.BattleActionBarMissionView());`;
     directly above `        AddTaomBehavior(new WargMissionBehavior());` insert:
     `        // The warg rage controller and the battle action bar are [DefaultView]s the engine builds with the`,
     `        // mission. Never add a MissionView here: this runs in Mission.AfterStart, after MissionScreen`,
     `        // registered its views, so it would never tick or finalize (#970, SubModuleMissionViewWiringTests).`
   - Kernel anchors: `grep -rnF -e 'BattleActionBarMissionView' -e 'new AutonomousMovementPlayerController()' TAOM.Tests --exclude-dir=CompanionTactics --exclude=SubModuleMissionViewWiringTests.cs`.
     Every hit is a test anchor. Expect two; any other hit is moved the same way:
     - `TAOM.Tests/Composition/FeatureModulesTests.cs` (WP123 moved its mission-behavior anchor onto the
       action bar line). Replace the anchor with the text, up to its first line end, of the nearest
       `AddTaomBehavior(new ...` statement above `FeatureModuleHooks.AddMissionBehaviors(mission, AddTaomBehavior);`
       (`grep -n -B8 'FeatureModuleHooks.AddMissionBehaviors(mission, AddTaomBehavior);' Main/SubModule.cs`;
       with WP117 merged it is `AddTaomBehavior(new Features.MissionPerf.Hooks.MissionTickProfilerBehavior(`,
       and without it `AddTaomBehavior(new Features.DreadAura.Hooks.DreadAuraMissionLogic());`), after
       checking that text occurs once (`grep -cF`).
     - `TAOM.Tests/Features/MissionPerf/MissionTickProfilerWiringTests.cs` (WP117), the anchor move
       `d96f349f1` made on 1.5.x: in `SubModule_AddsTheProfilerBehavior_RightAfterTheHeartbeat` replace
       `var actionBar = src.IndexOf("BattleActionBarMissionView");` with
       `var modules = src.IndexOf("FeatureModuleHooks.AddMissionBehaviors(mission, AddTaomBehavior);");`,
       `heartbeat >= 0 && actionBar >= 0` with `heartbeat >= 0 && modules >= 0`, and
       `add < actionBar` with `add < modules`.

     Never delete an assertion.
   - `git grep -n 'EnableBattleActionBar' -- Main TAOM.Tests` prints only the `// Renamed from` comment
     in `TaomSettings.cs` and the `GetProperty("EnableBattleActionBar")` pin in `BattleActionBarInputTests.cs`.
3. Docs and rules:
   - `docs/features/companion-tactics.md`: Overview item 3 becomes
     `3. **BattleActionBar**: context-sensitive on-screen action bar that appears in field battles. It is parked: click-only (no hotkeys), **off by default** (\`ShowBattleActionBar\`), and clickable only while a menu shows the cursor (see the #970 section); the buttons are Hold Fire, Brace, Shield Wall, etc. **Stances are display-only: they record state but do NOT change formation behavior** (the original developer's mod was UI-only here; the engine doesn't expose the firing-order / tighten-spacing APIs the original referenced).`;
     the `EnableBattleActionBar` config row becomes
     ``| `ShowBattleActionBar` | **false** | Show the click-only stance bar in field battles only (NOT siege). Renamed from `EnableBattleActionBar` (default true) in #970: MCM keeps a saved value per property name, so only a new name moves every install to off. Turning it off mid-battle removes the layer; turning it on again re-attaches it. |``;
     the `BattleActionBarMissionView.cs` key-files row becomes
     ``| `Main/Features/CompanionTactics/BattleActionBar/Hooks/BattleActionBarMissionView.cs` | `[DefaultView]` MissionView the engine builds in every mission with a screen (a view added through `AddTaomBehavior` never ticks, #970). A field battle is `Mission.IsFieldBattle`; the layer attaches on the first screen tick whose mode is Battle (the mode is Deployment at screen init), then a 0.5s refresh. The layer takes mouse buttons only, so the cursor stays hidden and attack, block and the camera keep working |``;
     the Performance bullet on `OnMissionScreenTick` ends `...at most twice per second; with the setting off (the default) its per-frame work is one settings read.`;
     the Known-limitations bullet that starts `**Stances are display-only.**` becomes
     `- **Stances are display-only, and the bar is click-only and off by default.** Clicking a bar button updates the stance dict and highlights the button, but the formation behavior is unchanged. Number-key hotkeys were removed (#970): they collided with the vanilla formation-select keys and the badges did not match button positions. Real stance enforcement requires APIs the engine does not expose and is deferred to a follow-up feature.`;
     in `## Tests` add bullets for `BattleActionBar/BattleActionBarInputTests.cs` (9: mouse-buttons-only
     input, no hotkeys or badge, non-button widgets pass clicks, the renamed setting is off, a click after
     turning it off records nothing, a click re-syncs the highlight), `BattleActionBar/BattleActionBarMissionViewGateTests.cs`
     (5 rows: attach only enabled, in a field battle, in Battle mode) and the two infrastructure gates
     (`MissionViewRegistrationGateTests`, `SubModuleMissionViewWiringTests`), and set the headline count
     from a `TAOM.Tests.Features.CompanionTactics` filter run; above `## Reviews` add:

```markdown
## Battle action bar: parked, and the in-game check (#970)

- **Parked** (the 1.5.x maintainer's decision of 2026-10-23, ported 2026-11). The bar registers and ticks, and it no
  longer blocks attack, block or mouse-look. It stays off by default because its stances change nothing in battle, and
  because with the cursor hidden it can be clicked only while another view shows a cursor, such as the Esc menu (the
  1.5.x review read the cursor pinning in the native input update; not re-checked on v1.4.8).
- **In-game check owed** (turn `Show Battle Action Bar` on first): the log shows `[BattleActionBar] screen init` at
  load; after Begin battle the cursor is hidden, left-click attacks, right-click blocks and the camera follows the
  mouse; a ground click with the order menu open gives a move order; with Esc open, clicking a bar button highlights
  it; turning the setting off mid-battle removes the bar and turning it on again shows it once.
```

     and at the top of `## Changelog` add
     `- <YYYY-MM-DD> (#970, ported from the 1.5.x line): the view is a \`[DefaultView]\` the engine registers, attaches on the first Battle tick, takes mouse buttons only, has no hotkeys, and is parked behind \`ShowBattleActionBar\` (off); \`SubModule\` no longer adds it or a second warg rage controller.`
   - `docs/features/warg-combat.md`: below the `**AutonomousMovementPlayerController**` bullet add
     `- **One controller instance per mission.** The engine builds it from \`[DefaultView]\` when the mission opens, before \`MissionScreen.OnMissionAfterStarting\` registers the views, so it ticks and finalizes. SubModule adds none: a MissionView added in \`Mission.AfterStart\` is never registered with the screen (#970), and the second instance it used to add leaked its \`EarlyStart\` handlers.`
   - `.claude/rules/gui-ui.md` (the mechanism this step ports): in the line under
     `## Custom GauntletLayer Input Wiring (MANDATORY)`, change `MissionView.OnMissionScreenInitializeFirstTime`
     to `MissionView.OnMissionScreenInitialize`, `SetInputRestrictions()` to `SetInputRestrictions(...)`
     and `dispatcher — mouse clicks pass through.` to `dispatcher; mouse clicks pass through.`; after
     that line insert a blank line and
     ``**In a battle, pass `(false, InputUsageMask.MouseButtons)`, never the parameterless call** (#970). The defaults are a visible cursor and all input: a visible cursor stops vanilla attack and block (`MissionMainAgentController` runs them only while it is hidden, v1.4.8 `MissionMainAgentController.cs:1001`) and turns the camera into an edge scroll. Put `DoNotAcceptEvents="true"` on every non-button widget of the prefab, because any widget that accepts events wins the mouse buttons from the scene layer. With the cursor hidden, a battle overlay button can be clicked only while another view shows a cursor (the Esc menu).``;
     in the "Correct pattern (MissionScreen overlay via MissionView)" block use
     `public override void OnMissionScreenInitialize()`, `base.OnMissionScreenInitialize();` and
     `_layer.InputRestrictions.SetInputRestrictions(false, InputUsageMask.MouseButtons); // REQUIRED; the cursor stays hidden`.
   - The RCA: `git show 40385aa5bdb759eb52d92a30b3a78a194cf000e0:docs/reviews/rca-battle-action-bar-2026-10-23.md > docs/reviews/rca-battle-action-bar-2026-10-23.md`, then:

```bash
cd /c/Users/mikew/source/repos/taom-port/130 && python - <<'PY'
import pathlib
p = pathlib.Path("docs/reviews/rca-battle-action-bar-2026-10-23.md"); t = p.read_text(encoding="utf-8")
i = t.index("Sources: Codex outputs"); j = t.index("## Findings")
t = t[:i] + ("Carried from the 1.5.x line (`40385aa5b`, reviewing `d96f349f1`). The same faults existed on 1.4.x,\n"
             "where `SubModule` added the view in `OnMissionBehaviorInitialize`; the Codex prompts and outputs this\n"
             "record cites live on the 1.5.x line.\n\n") + t[j:]
old = "and deletes two factory seams and an inert second controller instance."
assert t.count(old) == 1; t = t.replace(old, "and stops `SubModule` adding the view and an inert second controller instance.")
old = "Appended to `docs/reviews/lessons/state-lifecycle-save.md` (the late add and the mode gate)."
assert t.count(old) == 1
t = t.replace(old, "On the 1.5.x line the lesson is in `docs/reviews/lessons/state-lifecycle-save.md` (the late add and the\nmode gate); the 1.4.x copy arrives with the lessons sync (WP142).")
p.write_text(t, encoding="utf-8"); print("ok")
PY
```

**Verify**: Build exit 0; Filtered `--filter "FullyQualifiedName~TAOM.Tests.Features.CompanionTactics|FullyQualifiedName~TAOM.Tests.Features.Warg|FullyQualifiedName~MissionViewRegistrationGateTests|FullyQualifiedName~MissionTickProfilerWiringTests|FullyQualifiedName~TAOM.Tests.Composition|FullyQualifiedName~SettingsFingerprintTests|FullyQualifiedName~McmRenamedDefaults"`
0 failed; Gate `exit=0`; `grep -cE 'new AutonomousMovementPlayerController\(\)|BattleActionBarMissionView\(\)' Main/SubModule.cs`
prints `0`; Docs `ai_dashes: 0`. Staged: `Main/SubModule.cs`, the view, the service, model, both
VMs, both provider files, `TaomSettings.cs`, the prefab, the four new test files, the service test,
`CompanionTacticsSettingsProviderTests.cs` and any re-anchored test (if touched),
`companion-tactics.md`, `warg-combat.md`, `.claude/rules/gui-ui.md`, the RCA. Commit:

```text
fix(combat): v2.0.28 - battle action bar registers, keeps combat input

The Companion Tactics battle action bar never worked on 1.4.x. SubModule
added its view in OnMissionBehaviorInitialize, which runs in
Mission.AfterStart after MissionScreen.OnMissionAfterStarting has
registered the views (v1.4.8 MissionState.FinishMissionLoading), so the
view never ticked or finalized. The same code added a second warg rage
controller beside the [DefaultView] the engine builds, and that second
one never unsubscribed the handlers it registers in EarlyStart.

Both views are now [DefaultView]s the engine builds with the mission,
and SubModule adds neither. The bar attaches on the first Battle-mode
tick of a field battle (the mode is Deployment at screen init). Its
layer takes mouse buttons only, so the cursor stays hidden and attack,
block and mouse-look keep working; the 1 to 9 hotkeys, which clashed
with vanilla's formation keys, are gone; turning the setting off in a
battle removes the bar. The bar stays parked: the setting is now "Show
Battle Action Bar", off by default, under a new name so a saved "on"
does not carry over, because its stances still change nothing in
battle and a hidden cursor cannot click it.

Research: taom-src v1.4.8 MissionState.FinishMissionLoading,
  MissionScreen.OnMissionAfterStarting, ViewCreatorManager,
  InputRestrictions.SetInputRestrictions, GauntletLayer.LoadMovie,
  MissionMainAgentController
Not-tested: the #970 in-game check in docs/features/companion-tactics.md
Save-compat: none; MCM key EnableBattleActionBar is now
  ShowBattleActionBar
Ported-from: d96f349f1124ab474eb9b299b7c1389f6d9f8365
Ported-from: 40385aa5bdb759eb52d92a30b3a78a194cf000e0
```

**Part E done**: Test `Failed: 0`, `Total` = T0 + 98 (T0 + 53 if Part D was BLOCKED).

---

### Part F: three small fixes (`d469914ab`, `f54581d6c`, `1641fb985`)

#### Step F1: `taom.spawn_troops` finds troops in Custom Battle

1. RED: `git show d469914abaa48cc32907256aa911597ad4243101:TAOM.Tests/Features/DevConsole/MissionSpawnOriginTests.cs | sed -e 's/(v1.5.3 CustomGame.cs:135)/(v1.4.8 CustomGame.OnRegisterTypes)/' -e 's/(Campaign.cs:1558)/(v1.4.8 Campaign.cs:1537)/' -e 's/(SimpleAgentOrigin.cs:136)/(v1.4.8 SimpleAgentOrigin.cs:137)/' -e '/TestCategory("RequiresGame")/d' > TAOM.Tests/Features/DevConsole/MissionSpawnOriginTests.cs`
   **Verify (RED)**: Build fails (CS0117 `CreateOrigin`).
2. GREEN: `git show d469914abaa48cc32907256aa911597ad4243101 -- Main/Features/DevConsole/Cheats/MissionSpawnCheats.cs | git apply`,
   then in that file replace `(v1.5.3 CustomGame.cs:135)` with `(v1.4.8 CustomGame.OnRegisterTypes)` and
   `(SimpleAgentOrigin.cs:136)` with `(v1.4.8 SimpleAgentOrigin.cs:137)` (`MBObjectManager.cs:586-594` is the same on v1.4.8).

**Verify**: Filtered `--filter "FullyQualifiedName~TAOM.Tests.Features.DevConsole"` 0 failed;
`grep -c 'v1.5' Main/Features/DevConsole/Cheats/MissionSpawnCheats.cs TAOM.Tests/Features/DevConsole/MissionSpawnOriginTests.cs`
prints `0` for both. Commit (the two files):

```text
fix(devconsole): v2.0.28 - spawn_troops finds troops in Custom Battle

taom.spawn_troops looked its troop up as a CharacterObject, the type the
campaign registers for NPCCharacter. Custom Battle registers
NPCCharacter as BasicCharacterObject (v1.4.8 CustomGame.OnRegisterTypes)
and CharacterObject is sealed, so the lookup found nothing there and
the command reported an unknown troop in TAOM's main test venue. It now
looks up BasicCharacterObject, which still returns the campaign's
CharacterObject in a campaign, and gives a non-campaign troop a
BasicBattleAgentOrigin, because SimpleAgentOrigin casts its troop to
CharacterObject.

Research: taom-src v1.4.8 MBObjectManager.GetObject, SimpleAgentOrigin,
  BasicBattleAgentOrigin; dump CustomGame.OnRegisterTypes
Not-tested: taom.spawn_troops in a Custom Battle
Ported-from: d469914abaa48cc32907256aa911597ad4243101
```

#### Step F2: the WargConfig table matches the code

`git show f54581d6c2b41221f57184e8a53091d4e1739259 -- docs/features/warg-combat.md | git apply`
(adds `TargetDetectionRange` 20f; `SpeedForMaxDamage` 20f, `DamageToFall` 40, rage roll distance 20,
which is what `Main/Features/Warg/WargConfig.cs:35-46` holds). **Verify**: `git diff --stat` shows only
`docs/features/warg-combat.md`. Commit:

```text
docs(warg): v2.0.28 - WargConfig table matches the code (#615)

The WargConfig table in docs/features/warg-combat.md had drifted from
WargConfig.cs: SpeedForMaxDamage is 20, not 8, DamageToFall 40, not 20,
the rage roll distance 20, not 10, and TargetDetectionRange was
missing. The table now matches Main/Features/Warg/WargConfig.cs.

Not-tested: docs only
Ported-from: f54581d6c2b41221f57184e8a53091d4e1739259
```

#### Step F3: Hunter and Hebog-lûth troops take the bow formations (D13)

```bash
cd /c/Users/mikew/source/repos/taom-port/130 && python - <<'PY'
import pathlib, re
p = pathlib.Path("Main/_Module/ModuleData/troops/troops_dunland.xml")
raw = p.read_bytes(); bom = raw.startswith(b"\xef\xbb\xbf"); t = raw[3:].decode("utf-8") if bom else raw.decode("utf-8")
for tid, old, new in [("dunland_raven_noble_son", "Infantry", "Ranged"),
                      ("dunland_falcon_archer", "Cavalry", "HorseArcher"),
                      ("dunland_falcon_wildrider", "Cavalry", "HorseArcher"),
                      ("dunland_falcon_noble_horse_archer", "Cavalry", "HorseArcher")]:
    t, n = re.subn(r'(<NPCCharacter\s+id="%s"\s+default_group=")%s(")' % (tid, old), r"\g<1>%s\g<2>" % new, t)
    print(tid, n)
p.write_bytes((b"\xef\xbb\xbf" if bom else b"") + t.encode("utf-8"))
PY
```

**Verify**: four lines each ending ` 1`; `git diff --stat` shows `troops_dunland.xml | 8 ++++----`;
Data: no ERROR count above V0; Filtered `--filter "FullyQualifiedName~TAOM.Tests.Core|FullyQualifiedName~BannerBearers|FullyQualifiedName~TroopProgression|FullyQualifiedName~LordPartyTemplates"`
0 failed. Commit:

```text
fix(dunland): v2.0.28 - Hunter and Falcon troops fight as archers

The Cigfran-lûth Hunter carries only a bow but was tagged Infantry, and
the three Hebog-lûth troops (Scout, Horse Archer, Noble Horse Archer)
carry bows but were tagged Cavalry. For a regular troop default_group
is the battle formation (v1.4.8 Mission.GetAgentTroopClass reads
GetFormationClass, which is DefaultFormationClass), so the Hunter
fought in the infantry line and the Hebog-lûth with the melee cavalry,
and in sieges as infantry. The Hunter is now Ranged and the three are
HorseArcher. Only the four tags change; skills and kits are untouched,
and the Hunter is no longer a banner-bearer candidate.

Known limitation: the next reseed of Dunland's enlistment kits will
take the Hunter's kit for the archer recruit, and it has no sidearm.

Research: taom-src v1.4.8 Mission.GetAgentTroopClass; dump
  BasicCharacterObject.GetFormationClass
Not-tested: a battle with Dunland archers and the Hebog-lûth
Ported-from: 1641fb98542bdd3ec7ad6fcbf4d33c1f96a30b7b
```

**Part F done**: Test `Failed: 0`, `Total` = T0 + 100 (T0 + 55 if Part D was BLOCKED).

---

### Step G: review gate, final verification, hand-off

1. Run every Done criterion below.
2. Review gate: run `/deep-review` on `git diff $(git merge-base HEAD bannerlord-1.4.5)..HEAD` and ask
   it to probe the "Maintenance notes" spots. Act only on findings that change runtime behaviour or
   break this plan, each as one extra commit `fix(<scope>): v2.0.28 - review follow-ups` (tests
   first, same commit procedure), then re-run the Done criteria. Never rewrite earlier commits. If
   your session cannot invoke skills, say so; the integrator runs it. Never run `/review-codex` or
   `/codex-verify` (paid, maintainer only).
3. Hand-off report (final message): status (DONE, or BLOCKED with the reason); branch; each commit's
   short SHA and subject; T0 and final totals; whether Part D ran or was BLOCKED (with the D0 output);
   any D1.4 mirror adaptation; each kernel anchor you moved in E3 and its new text; the Step B2
   `ok <a> <b>` line; the `/deep-review` outcome; the "In-game checks owed" list. The integrator
   records these in `plans/README.md`; this plan closes no old-plan row.

## Disposition (every commit in the pack, plus the shared hunks)

Simulation, re-run this session: `git merge-tree --write-tree --name-only --no-messages --merge-base=<sha>^ origin/bannerlord-1.4.5 <sha>`.

| 1.5.x commit | Subject | Simulation onto `1b00d1acd` | Disposition |
|---|---|---|---|
| `3f75217dc` | feat(combat): v2.0.29 - cavalry knockdown parity, culture charge (#610) | exit 1, 10 paths (`TaomAgentStatCalculateModel.cs`, `TaomCustomBattleAgentStatCalculateModel.cs` absent, `SubModule.cs`, fingerprint test, bookkeeping) | **Adapted** (C1): parity JSON and compiled values, troll rows, `uruk_hai` row dropped, MCM rename to `ChargeAutoKnockdownRatio` (6) with the `McmRenamedDefaultsTests` row WP110 left out, tests. Not taken: per-culture charge damage (`IChargeDamageService`, `ChargeDamageService`, `MountChargeDamageApplier`, the `chargeDamage` table), the three live sliders, the stat-model and `SubModule` hunks, the census, RCA and lessons |
| `f54581d6c` | docs(review): v2.0.29 - creature mount retune deep review, RCA and lesson (#615) | exit 1: CHANGELOG, a lesson | **Ported** (F2): the `warg-combat.md` table rows only. REVIEW-LOG, RCA and lesson review the 1.5.x retune |
| `0f57f29ff` | balance(mounts): v2.0.29 - creature mount speeds retuned (#615) | exit 1: CHANGELOG | **Skipped**: SKIP-CONTENT (Armory mount data) |
| `30abf550b` | Add unit tests for various tools and functionalities | exit 1, 52 paths | **Adapted** (A1, A2): `PassesRaceGate`, both `CanAgentPickUpAnyBanner` overrides, four service tests, banner doc text re-cited to v1.4.8. ExcludedTargetMethods WP107; config-protection WP116; validate_mesh_refs WP108; native_crash_triage registers WP137; lint regex WP143; TrollBruteForce, `troops_mordor.xml`, hill-troll tools: OUT |
| `25995dc91` (shared, group of WP113) | feat: Add custom battle banner bearers model to restrict trolls from carrying banners | exit 1, 20 paths | **Adapted** (A2): `TaomCustomBattleBannerBearersModel` at its `30abf550b` end state and a minimal `RegisterCustomBattleModels` seam (the method shape of SKIP-FEATURE `eab517aad`, K1). Shader walk re-park: rejected by D4 (WP113); TrollBruteForce and hill troll content: OUT |
| `d469914ab` | feat(creature-bandits): v2.0.30 - riderless spider broods in Mirkwood | exit 1, 44 paths | **Adapted** (F1): `MissionSpawnCheats.cs` and `MissionSpawnOriginTests.cs`, citations re-pinned to v1.4.8. Everything else is the 1.5.x feature: OUT |
| `b0154d86e` | fix(combat): v2.0.34 - Custom Battle runs TAOM's combat mechanics | exit 1, 35 paths | **Re-implemented** (D1) on WP127's facade: nine overrides, registration, adapted tests. Creature Bandits, Race Abilities, Creature Siege Role, Signature Strikes: OUT. The crush roll stays in the shared facade, so no second draw site (K1 T6; D16 rejected a lock) |
| `efa32184b` | fix(dread): v2.0.34 - the dread aura weakens close troops, never routs | exit 1: provider, its tests, two docs; with `74e9172d4` modelled: two docs | **Ported** (B1, cherry-pick) |
| `0103a8271` | docs(reviews): v2.0.34 - dread aura review RCA and lessons | exit 1: three lessons | **Not here**: lessons go to WP142 (per-port procedure); the RCA is not ported |
| `f4f12a737` | docs(dread): v2.0.34 - correct the rout chain from the v1.5.5 engine | exit 1: dread doc, configs doc, lesson, RCA (absent); after `efa32184b`: lesson and RCA | **Adapted** (B2): doc, configs doc and test-comment hunks, every v1.5.5 citation re-read on v1.4.8; lesson WP142; RCA hunk dropped |
| `1e62adeb2` | chore(ai): v2.0.34 - native-branch trap and morale-write rule | exit 1: `docs/ai-includes/orientation.md` (absent) | **Adapted** (B3): `.ai/scopes.md` link and the `csharp-patterns.md` line; the trap index is absent on 1.4.x |
| `d96f349f1` | fix(combat): v2.0.35 - battle action bar view registers with the screen | exit 1, 13 paths | **Adapted, squashed with `40385aa5b`** (E3): Main-path view, both `SubModule` adds removed, gates scan `TAOM.dll`. `MissionTickProfilerWiringTests`' anchor move is taken too (WP117 ships that test, so the pack's "MissionPerf is 1.5.x-only" no longer holds). Factory and interface deletions (undo `c91380274`, absent here), `Client/` composition, `CoopMissionRoles`, headless-server doc: not applicable |
| `40385aa5b` | fix(combat): v2.0.35 - battle action bar keeps combat input, parked | exit 1: gui-ui rule, view (absent path), provider and tests, gate test (absent), docs | **Adapted** (E3): service, model, VMs, prefab, settings rename, tests, docs, rule lines, RCA. Codex prompt and lesson: bookkeeping |
| `726975a52` | feat(fords-of-war): v2.0.36 - Fords of War, a lane-push battle mode | exit 1, 26 paths (the two Warg files clean) | **Adapted** (E1): `PrepareOnFirstCall.cs`, `CleanIfEnemyDied.cs`, `WargRageFormationTests.cs`. Fords of War, the DreadAura gate and tracker changes, the DevConsole menu gate, strings, lessons: OUT |
| `1641fb985` (shared, D13) | fix(dunland): v2.0.35 - Hunter and Falcon troops fight as archers | exit 1: generated HTML, a lesson; `troops_dunland.xml` clean | **Adapted** (F3): the four `default_group` retags. Skill restat (balance), RCA, lesson, HTML: not taken |
| `c91380274` (K1, absorbed) | feat(coop): v2.0.35 - mission and WinForms types move to TAOM.Client | not simulated (SKIP-FEATURE) | **Guard re-implemented** (E2) on the 1.4.x class. The client split: OUT (D7) |

Prerequisites owned elsewhere: `74e9172d4` (WP123), `f7e9bd0d2` (WP127), `429713746` (WP110; this plan
restores its `ChargeAutoKnockdown` row), `e0ad0db76` (WP123, the anchor E3 moves). Deep-dive findings
owned: none.

## Test plan

| Part | New results | Files |
|---|---|---|
| A | 10 | `BannerBearerServiceTests` (+4), `BannerBearerRaceGateWiringTests` (6: 2 source rows, 2 reflection, 2 registration) |
| B | 12 | from `efa32184b`: config provider +2, service +7 net, settings provider +2, shipped config +1 |
| C | 8 | `ChargeKnockdownServiceTests` +4 net, `ShippedCombatMechanicsConfigTests` +2, `CombatMechanicsSettingsProviderTests` +2 (`McmRenamedDefaultsTests` gains a row, not a result) |
| D | 45 | `CustomBattleDamageModelTests` (3 source pins, 15 seam rows, 1 order, 3 registration, 2 type, 9 + 9 IL rows, 1 classifier control, 2 behaviour) |
| E | 23 | `WargRageFormationTests` 2, `WargControllerNullGuardTests` 5, `BattleActionBarInputTests` 9, `BattleActionBarMissionViewGateTests` 5, `MissionViewRegistrationGateTests` 1, `SubModuleMissionViewWiringTests` 1 |
| F | 2 | `MissionSpawnOriginTests` |

Patterns: `CombatMechanicsModelInvariantsTests` (resolve by name after the game guard), `WandererAllegianceWiringTests`
(source pins through `RepoPaths`). Structurally untestable, owed in game: banner pickup by a live troll,
the knockdown feel, Custom Battle combat, the warg swap and formation, the bar's input, the dread
aura's reach, `spawn_troops` in Custom Battle, Dunland formations (Not-tested trailers name each).

## Done criteria

- [ ] Build exit 0; Test `Failed: 0`, `Total` = T0 + 100 (T0 + 55 if Part D was BLOCKED; data rows counted singly).
- [ ] `git log --format=%s $(git merge-base HEAD bannerlord-1.4.5)..HEAD` lists the 13 subjects above (12 without Part D), each with `v2.0.28 - `, plus any review follow-up.
- [ ] `git diff --name-only $(git merge-base HEAD bannerlord-1.4.5)..HEAD -- CHANGELOG.md Main/IoC.cs Main/TAOM.csproj docs/reviews/lessons docs/reference/feature-map.md` prints nothing.
- [ ] `git grep -n -E 'EnableBattleActionBar|DreadAuraRadius\b|public int ChargeAutoKnockdownWeightRatio' -- Main TAOM.Tests | grep -v -E 'TaomSettings\.cs:[0-9]+: +//|BattleActionBarInputTests\.cs'` prints nothing (the rename comments and the old-name-is-gone pin stay).
- [ ] `grep -cE 'new AutonomousMovementPlayerController\(\)|BattleActionBarMissionView\(\)' Main/SubModule.cs` prints `0`; `grep -c 'RegisterCustomBattleModels' Main/SubModule.cs` prints `2`.
- [ ] Gate `exit=0`; Tool tests no new failure against P0; Data no ERROR count above V0.
- [ ] Docs: `ai_dashes: 0`; `dead_links`, `orphan_features`, `model_registry` equal Step 0.7.
- [ ] `git status --porcelain` empty; nothing pushed.

## STOP conditions

- Step 0.2 or 0.4 output differs (a dependency is missing, or part of this WP already landed).
- A cherry-pick conflicts in a path its step does not list (beyond the B1 fallbacks), or a
  `git apply` fails (an earlier WP changed the file: report which).
- The B2 script raises an assertion, or `grep -c 'v1\.5\.5' docs/features/dread-aura.md` is not `1`.
- `check_mcm_default_flips.py` prints `FLIP`, `UNPARSED` or `STALE`, or exits non-zero.
- `MissionViewRegistrationGateTests` names a type other than the two TAOM views (an earlier WP added
  a MissionView): report it; do not allowlist it yourself.
- `WargRiderHandManager.cs` no longer looks the controller up with
  `GetMissionBehavior<AutonomousMovementPlayerController>()` (WP124 restructured it).
- The F3 script prints a count other than 1, or the validator shows an ERROR count above V0.
- A step's verification fails twice after a reasonable fix, or a test outside the step's filter
  that passed at T0 fails.
- A commit hook denies a commit (quote it; never `--no-verify`).
- Any change would need `Main/IoC.cs`, a csproj, `.claude/settings*.json`, `Directory.Build.props`
  or `docs/adrs/**`.
- A TaleWorlds member behaves or is shaped differently from "Engine facts": report, do not decompile
  and improvise. (Part D's facade mismatch is a BLOCK of Part D only, not a STOP.)

## In-game checks owed (maintainer)

Log: `Modules/TAOM/Logs/taom_debug_*.log` unless stated.

1. Campaign battle with a cave troll unit and banners: no troll carries a banner; kill a bearer next
   to a troll and watch the dropped banner fetched by a non-troll or left lying.
2. Custom Battle with Mordor (trolls): no troll raises or fetches a banner.
3. One shader pre-compile batch (it runs hidden Custom Battles): no exception naming
   `TaomCustomBattleBannerBearersModel` or `TaomCustomBattleDamageModel`.
4. Dread: a campaign battle against a Nazgûl: only troops in contact weaken; no formation-wide rout
   from the aura alone; MCM "Dread Radius" shows 5 on an install with an old TAOM.json.
5. Knockdown: Rohan cavalry into Mordor orcs and uruks floors them head-on and floors men from any
   angle at full speed; cave trolls and dwarves mostly stay up; MCM "Auto-Knockdown Weight Ratio"
   shows 6 on an old TAOM.json.
6. Custom Battle combat (Part D): troll cleave and stagger immunity, crush-through and charge
   knockdown work; a mounted hit causes no crash.
7. Battle action bar: the #970 checklist in `docs/features/companion-tactics.md`; with the setting off
   (default) no bar and no input change; the `[BattleLoad]` TAOM-behaviour count is two lower.
8. Warg: a player on a warg still loses and regains control in a rage; warg reinforcements return to
   their formation after a rage or after their warg dies.
9. `taom.spawn_troops <id> 5 enemy` in a Custom Battle (cheat mode) spawns troops.
10. Dunland: the Hunter fights with the archers and the Hebog-lûth with the horse archers. Check
    whether the Hebog-lûth are mounted: their battle rosters have no `Horse` item on 1.4.x, the
    public tree or 1.5.x, so they may fight on foot in the horse archer formation. A horse is
    content (D13): report it, do not add one.

## Maintenance notes

- **Parallel wave-4 merges**: WP133 (`b2e387db7`) also edits `DreadAuraConfigProvider.cs`, the
  BannerBearers config loader and their tests, and adds lines to `dread-aura.md` and
  `banner-bearers.md`; expect small merge conflicts at integration (different regions; keep both).
  WP139 later applies `b4ad748f2` and `0bc2a132f` (attach and stance check lines) to the view this
  plan transplants; keeping that file byte-identical to `40385aa5b` lets their hunks apply.
- **Seam**: `RegisterCustomBattleModels` is now the one place for Custom Battle models; the module
  step runs after it, so a module must never add a model for the same slot.
- **Renamed MCM keys** (`DreadAuraCloseRadius`, `ChargeAutoKnockdownRatio`, `ShowBattleActionBar`):
  never rename back; saved files still hold the old keys.
- **Review probes**: the twin's call order against the campaign model after WP127; `PassesRaceGate`
  folding the toggle; the E3 kernel anchor move; `ShouldAttach` and detach-on-disable; that no
  MissionView is constructed in `Main`; the dread doc's v1.4.8 citations.
- **Follow-ups not in this plan**: refresh `docs/reference/taleworlds-api-snapshot/` for the two new
  models (`/verify-bindings`, maintainer); regenerate `docs/reference/ranged-troops.html` at the next
  ranged-ladder report run; the Hebog-lûth horse gap (content, maintainer); the per-culture charge
  damage and live charge sliders (1.5.x feature, not ported); lessons from `d96f349f1`, `40385aa5b`,
  `efa32184b`, `0103a8271`, `f4f12a737`, `726975a52`, `30abf550b`, `3f75217dc`, `1641fb985` (WP142);
  `gamemodels.md`, `gui-ui.md` and `csharp-patterns.md` reconciliation (WP143). No new localization
  keys (all new text is MCM hints, logs or docs), so WP135 has nothing to translate from this WP.
