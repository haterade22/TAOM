# Plan 127: Land the 1.4.x-safe architecture refactors: seam decision logic, dead scaffolds, the damage-model split and the rule text that matches them

> **Executor instructions**: Follow this plan step by step. Run every verification command and confirm
> the expected result before moving on. If anything in "STOP conditions" occurs, stop and report; do not
> improvise. Do not edit `plans/README.md`: every WP branch shares it, so the integrator records this
> plan's status and test count when merging. Put the numbers Step D lists in your final message.
> Part M is the maintainer's, not yours: you prepare the script it uses and stop before it.
>
> **Drift check (run first, Step 0.2)**:
> `git diff --stat 1b00d1acd..HEAD -- Main/Adapters/IEditorSceneAdapter.cs Main/Features/EditorCacheRebuild TAOM.Tests/Features/EditorCacheRebuild TAOM.Tests/Migration/GameAssemblies.cs TAOM.Tests/Migration/ReflectionSiteBindingTests.cs Main/Features/FieldCamp/CampService.cs Main/Features/Refuge Main/Features/SupplyLines/SupplyOrderService.cs TAOM.Tests/Features/FieldCamp TAOM.Tests/Features/Refuge TAOM.Tests/Features/SupplyLines/SupplyOrderServiceTests.cs Main/Features/CombatMechanics TAOM.Tests/Features/CombatMechanics TAOM.Tests/Infrastructure/CollisionDataFixture.cs Main/SubModule.cs docs/features/editor-cache-rebuild.md docs/features/field-camp.md docs/features/supply-lines.md docs/features/combat-mechanics.md docs/features/refuge.md docs/reference/taleworlds-api-snapshot/reflection-sites.md docs/reference/engine/submodule-lifecycle-and-harmony.md docs/ai-includes/architecture.md docs/ai-includes/decompiled-code-analysis.md docs/ai-includes/patterns.md .ai/review-reference.md .claude/rules/csharp-architecture.md .claude/rules/csharp-patterns.md .claude/rules/harmony-patches.md .claude/rules/think-before-coding.md .claude/agents/feature-builder.md .claude/skills/new-feature/SKILL.md .claude/skills/codex-verify/SKILL.md .claude/skills/improve/references/audit-playbook.md .claude/skills/deep-review/SKILL.md CLAUDE.md docs/adrs`
> Earlier WPs of this programme are expected to have changed `Main/SubModule.cs`, the two Migration
> test files, `reflection-sites.md`, `.ai/review-reference.md`, several `.claude/` files, `CLAUDE.md`,
> `docs/ai-includes/architecture.md` and the lifecycle doc (WPs 101 to 126). The helper script checks
> every anchor it needs and stops on a real mismatch; the blob and form checks in Steps 0 and B0 cover
> the code. STOP only when one of them fails.

## Status

- **Priority**: P3; **Effort**: M (four executor commits, two maintainer commits; picks plus one hand port)
- **Risk**: LOW (every change is behaviour-neutral and pinned by tests). WP123 changes
  `TaomCombatMechanicsModel.ApplyDamageReductions` in the same wave, so it merges first; Step B0 checks
  that its form is on the branch.
- **Depends on**: WP105 (`plans/105-composition-root.md`, merged; it implies WP104's
  `TAOM.Tests/Infrastructure/RepoPaths.cs`) and WP123 (`plans/123-always-on-cost-cuts.md`, merged; its
  Step C3 creates `RefugeDamageHooks` and its tests). Cut this branch only after WP123 is merged.
- **Category**: tech-debt (architecture); **Executor**: laptop
- **Wave / lane**: wave 3, lane E
- **Decisions applied**: D1 (no plan stages `CHANGELOG.md`; the commit body is the changelog entry),
  D2 (CombatMechanics has no feature module on 1.5.x, so its model stays hand-wired in `SubModule.cs`),
  D8 (`v2.0.28` subjects), D9 (no CI; the 1.5.x `RequiresGame` tags are dropped from ported tests),
  D10 (ordinary session), D16 (the crush roll moves unchanged and stays unlocked), D19 (git only
  through the Bash tool). Maintainer-only (decisions brief "Authority" item 2): the `docs/adrs/*.md`
  edits are Part M.
- **Planned at**: 1.4.x `origin/bannerlord-1.4.5` `1b00d1acd`; 1.5.x `origin/bannerlord-1.5.x`
  `e45f6df2c`; 2026-11-04
- **Issue**: Port of #737 (the damage-model split; its message also cites #610) and #870 (ADR-003).
  1.5.x plans 021, 025 and 026 carried no GitHub issue: orchestrator creates the issue before
  implementation lands. If you are given numbers, add `Refs: #<n>` as the first trailer.

## Why this matters

`TaomCombatMechanicsModel`, the single `AgentApplyDamageModel` slot every combat feature shares, is 230
lines on 1.4.x against ADR-002's 150-line cap for an entry point, and every later combat port lands in
it: the #741 refuge-mount fix WP123 ports (`bb68eeca3`) edits `RefugeDamageHooks` on 1.5.x, and
WP130's Custom Battle damage model can reuse `CombatMechanicsHooks` instead of copying the extractors.
Four camp, refuge and supply services hold TAOM decisions inside protected-virtual seams that their
test subclasses override, so no unit test ever ran that logic; plan 026 moves it into tested internal
methods (78 new tests) and makes WP144's later refuge fix (`63630e8a0`) apply to the same code it was
written against. Plan 025 deletes 331 lines under `Main/` (scaffolding nothing ever resolved, its two
registrations and two reserved properties), the 26 tests (four files, 406 lines) that kept it alive,
and a reflection-binding row for a `TaleWorlds.Engine` type that does not exist. Finally, the 1.4.x rules
still say every service needs an interface, every patch a hook interface, and that engine access in a
service is always an ADR-007 CRITICAL, while the code already relies on seams (lines with `protected
virtual`: `RefugeService.cs` 40, `CampService.cs` 32, `SupplyOrderService.cs` 13, `WardenService.cs` 11
on `origin/bannerlord-1.4.5`); every `/deep-review` in this programme would flag ported 1.5.x code as a
violation until the rules and ADRs say what the code does. WP143's later ADR sync (`250e1b321`) is
written on top of exactly this ADR text.

## Current state

### Executor environment

- Laptop; repo `C:\Users\mikew\source\repos\TAOM`; Bannerlord v1.4.8 (build 119303) at
  `C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord` (`BANNERLORD_GAME_DIR`).
  Never write into the game install.
- Base branch: local `bannerlord-1.4.5` after WP101 plus every WP merged before this one (WP105 and
  WP123 among them).
- Worktree: `C:\Users\mikew\source\repos\taom-port\127`, branch `port148/127-architecture-refactors`.
  It is outside the session's working directory, so start EVERY command with
  `cd /c/Users/mikew/source/repos/taom-port/127 &&` (called `WT$` below).
- Helper folder (outside the worktree, never staged): `C:\Users\mikew\source\repos\taom-port\wp127-scripts\`
  for the edit script and the commit messages. Write files there with your file-writing tool, never a
  shell heredoc.
- Python: `python` (if Git Bash cannot find it, `/c/Python314/python.exe`).

### What exists and what does not (git grep and ls-tree on `origin/bannerlord-1.4.5`, this session)

- Absent: `Main/Features/CombatMechanics/Hooks/`, `RefugeDamageHooks` (WP123 creates it), `CombatMechanicsHooks` (0 files),
  `TAOM.Tests/Infrastructure/CollisionDataFixture.cs`, `FortificationSearch` and `MintedHero` (0 files),
  "Protected-Virtual Boundary Seams" and "Amendment: partial services" in `docs/` (0 files),
  `.claude/skills/deep-review/lenses/` (the skill is the single `SKILL.md`), `HowdahCrewAgentOrigin` and
  `MumakilCrewAgentOrigin`, and the 1.5.x-only features RaceAbilities, SignatureStrikes,
  CreatureSiegeRole and CreatureBandits (0 files each).
- Present: `Main/Features/EditorCacheRebuild/Caching/` (6 files) and its 4 test files; the only
  references to the scaffold are the files `032481cc6` touches (plus old review records).
- CombatMechanics has no `*Module.cs` on 1.5.x either (`git ls-tree e45f6df2c -- Main/Features/CombatMechanics`;
  `SubModule.cs:1290` there still builds the model), so D2's module route does not apply.

### Files and roles

| File (1.4.x blob) | Role in this WP |
|---|---|
| `Main/Features/EditorCacheRebuild/**`, `Main/Adapters/IEditorSceneAdapter.cs` | Plan 025 deletes the scaffold, two registrations, two config properties |
| `Main/Features/{FieldCamp/CampService,Refuge/RefugeService,Refuge/WardenService,SupplyLines/SupplyOrderService}.cs` (`560e434ad`, `094c2285b`, `121a74746`, `e5416df60`) | Plan 026; each blob equals the 1.5.x parent of `a39575410` |
| Their four test files | Three differ from that parent only by the 1.5.x `[TestCategory("RequiresGame")]` line (outside every hunk); `WardenServiceTests.cs` is identical |
| `Main/Features/CombatMechanics/Models/TaomCombatMechanicsModel.cs` (`7e2161373c`, 230 lines at planning; WP123 shortens it) | Split into `CombatMechanicsHooks`; the refuge glue is already in WP123's `RefugeDamageHooks` |
| `Main/Features/CombatMechanics/CombatMechanicsIoC.cs` | Gains one transient registration |
| `Main/SubModule.cs` (single-owner) | Six resolve lines become one |
| `TAOM.Tests/Features/CombatMechanics/CombatMechanicsModelInvariantsTests.cs` (3 tests) | Gains the line ceiling and the seam pins |
| The 15 rule and guide files of Part C | Plan 021's non-ADR text |
| `docs/adrs/002`, `007`, `008`, `003`, `README.md` | Part M (maintainer). 002/007/008 equal the 1.5.x parent of `1cdf8eb02` (blobs `2fefea5ea`, `688037de8`, `c5c539956`); 003 equals the parent of `583585818` (`0c75579c0`) |

### 1.4.x excerpts (`origin/bannerlord-1.4.5`)

`TaomCombatMechanicsModel.cs`: the constructor (`:33-51`) takes eight parameters; `ApplyDamageReductions`
(`:58-68`) applies `RefugeDamageReduction.Apply(result, reduction)` with the reduction for
`VictimPartyId(attackInformation.VictimAgentOrigin)`; `VictimPartyId` (`:70-79`) switches on
`PartyAgentOrigin` / `PartyGroupAgentOrigin`; `DecideAgentKnockedDownByBlow` (`:116-126`) returns base
for `!collisionData.IsHorseCharge`; `GetHorseChargePenetration` (`:154-157`) returns
`_configProvider.GetConfig().ChargeKnockdown.HorseChargePenetration` when `ChargeKnockdownEnabled`; the
crush roll `randomRoll: MBRandom.RandomFloat` is at `:177`. WP123's Step C3 changes this before this WP
starts: `ApplyDamageReductions` calls `RefugeDamageHooks.Reduce` and `VictimPartyId` is gone, so the later
line numbers move up on the branch.

```csharp
// Main/SubModule.cs:1107-1115
        campaignStarter.AddModel<AgentApplyDamageModel>(new TaomCombatMechanicsModel(
            careerAgentStat,
            IoC.Resolve<Features.CombatMechanics.ICrushThroughService>(),
            IoC.Resolve<Features.CombatMechanics.IChargeKnockdownService>(),
            IoC.Resolve<Features.CombatMechanics.ICreatureCombatService>(),
            IoC.Resolve<Features.CombatMechanics.IShieldPenetrationService>(),
            IoC.Resolve<Features.CombatMechanics.ICombatMechanicsConfigProvider>(),
            IoC.Resolve<Features.CombatMechanics.ICombatMechanicsSettingsProvider>(),
            IoC.Resolve<Features.Refuge.IRefugeDefenseService>()));
```

- `EditorCacheRebuildIoC.cs:2` `using TAOM.Features.EditorCacheRebuild.Caching;`, `:16-17` register
  `IPathReuseCache` and `IPersistentPathCache`; `CacheRebuildConfig.cs:42-46` the two reserved
  properties. The shipped `cache_rebuild_config.json` carries neither key.
- 1.4.x vs 1.5.x for the split: the four service interfaces and both `Domain/*Context.cs` files are
  byte-identical to the 1.5.x parent of `f7e9bd0d2`; only `ICombatMechanicsSettingsProvider` differs
  (1.5.x #610 added `ChargeHorsePenetration` and three other knobs). So the 1.4.x hooks keep reading the
  penetration from `ICombatMechanicsConfigProvider`, as the 1.4.x model does.
- The parent model already reads a mount hit through its rider: `TaomAgentApplyDamageModel.cs:105-106`
  (`info.IsVictimAgentMount ? info.VictimRiderAgentOrigin : info.VictimAgentOrigin`, then
  `origin?.BattleCombatant as PartyBase`). That is the form WP123's #741 fix uses (Step B0 checks it).

### Engine facts (installed v1.4.8 through `pwsh -NoProfile -File tools/taom-src.ps1 path <Type>`, read this session)

| Member | Signature or behaviour | Used by |
|---|---|---|
| `TaleWorlds.MountAndBlade.ComponentInterfaces.AgentApplyDamageModel` | abstract `ApplyDamageReductions(in AttackInformation, in AttackCollisionData, float)` :28, `DecideMissileWeaponFlags` :32, `CalculateStaggerThresholdDamage` :36, `DecideWeaponCollisionReaction` :44, `CalculateShieldDamage` :46, `DecideCrushedThrough` :64, `CalculateRemainingMomentum` :66, `DecideAgentShrugOffBlow` :94, `DecideAgentKnockedDownByBlow` :100, `GetHorseChargePenetration` :112 | the ten overrides (unchanged signatures) |
| `AttackCollisionData` | `private bool _isColliderAgent` :20; `public int InflictedDamage` :63; `IsColliderAgent => _isColliderAgent` :82; `IsHorseCharge => ChargeVelocity > 0f` :102; `AffectorWeaponSlotOrMissileIndex { get; }` :108; `ChargeVelocity { get; }` :138 | `CollisionDataFixture` (same lines as the v1.5.4 it cites) |
| `AttackInformation` | public fields `AttackerWeapon` :27, `IsVictimAgentMount` :71, `VictimAgentOrigin` :91, `VictimRiderAgentOrigin` :93 | refuge hook, tests |
| `IAgentOriginBase.BattleCombatant` | :13 | `RefugeDamageHooks` (WP123) |
| `PartyAgentOrigin` / `PartyGroupAgentOrigin` | `Party` :31 / :29; `BattleCombatant => Party` :48 / :31 | `RefugeDamageHooks` (WP123) |
| `Blow.BlowFlag` | public field :38 | hooks tests |
| `MBRandom.RandomFloat` | `Random.NextFloat()` :32; `Random` falls back to an internal `MBFastRandom` when `Game.Current` is null (:16-30) | hooks tests build a crush context with no game |
| `MissionWeapon` | `Item { get; private set; }` :61; `CurrentUsageItem` returns null when `_weapons` is null or empty (:67-77) | hooks tests pass `default` |
| `SandBox.GameComponents.SandboxAgentApplyDamageModel.DecideCrushedThrough` | :626, offhand then primary wield resolution :641-655 | comment in the hooks |
| `TaleWorlds.CampaignSystem.Map.DistanceCache.SandBoxNavigationCache` | `GetRealDistanceAndLandRatioBetweenSettlements` :81 builds a local `NavigationPath` and passes extra-cost multiplier `1f` (:88); `CheckBeingNeighbor` :123 passes `2f` (:134); the parameter is `extraCostMultiplier` (`IMapScene.cs:34`, v1.4.8 dump) | plan 025 doc sentence (1.5.x cited v1.5.3; holds on v1.4.8) |
| `MobileParty` | `GetPosition2D` :1064, `PrisonRoster` :1074, `MapFaction` :1080 | plan 026 |
| `TroopRoster` | `Count` :46, `GetCharacterAtIndex(int)` :567 | plan 026 |
| `Hero.MapFaction` :567; `IFaction.IsAtWarWith(IFaction)` :94; `Settlement.GetPosition2D` :199 | | plan 026 |
| `PartyBase.MobileParty { get; private set; }` :122; `MBObjectBase.StringId { get; set; }` :12 | | `RefugeDamageHooksTests` (WP123) |
| `CampaignTime` | `public struct CampaignTime` :11 | ADR-007 edit (Part M) |
| `Mission.cs` (v1.4.8 dump) | `agent.Origin = agentBuildData.AgentOrigin` :4131 (the rider); `CreateHorseAgentFromRosterElements` :4537-4548 sets no origin | why a mount hit reads its rider (already landed by WP123) |

The other identifiers plan 026 adds over the 1.4.x files are TAOM types or `Array.Empty`; every other
engine call in its end state already appears in the 1.4.x versions of the four files.

### Conventions that bind this change

- **ADR-007 adapters**: services never touch sealed TaleWorlds types. `CombatMechanicsHooks` and
  `RefugeDamageHooks` are boundary code beside a GameModel (the facade pattern RaceAbilities uses on
  1.5.x), not services, exactly as the model they come from. The seam rule Part M adds is what plan
  026's services follow.
- **ADR-002 thin entry points**: entry points under 150 lines, delegating. The split is this rule.
- **ADR-003** no `#region`; **ADR-004** no `[Obsolete]`; **ADR-005** no `#if DEBUG` outside the IoC
  registration exception.
- **TDD**: Part B writes its tests first (Steps B1 and B2 are RED). Parts A and C are ports whose tests
  travel with the picks.
- **MCM json2 rule** (rename a persisted setting, never flip its default): no setting changes here.
- **`.claude/rules/harmony-patches.md`** (`MovementOrder` postfixes go through the shared deferred
  category): no patch changes here.
- **Single-owner files**: this WP edits `Main/SubModule.cs` only at the model construction (Step B3);
  it never edits `Main/IoC.cs` or either csproj.

## Commands you will need

Start every command with `cd /c/Users/mikew/source/repos/taom-port/127 &&`.

| Purpose | Command | Expected |
|---|---|---|
| Restore | `dotnet restore TAOM.sln` | exit 0 |
| Build | `dotnet build TAOM.sln -p:DisableModuleCopy=true -p:ModuleId=` | exit 0, 0 errors (a BHA0001 Patch88 warning may print; ignore it) |
| Test | `dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId=` | `Failed: 0`; give the call a 600000 ms timeout |
| Filtered | `dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId= --filter "<expr>"` | as each step states |
| Edit script | `python ../wp127-scripts/wp127_edit.py <spec>` | `OK <spec>: ...`; anything starting `STOP:` is a STOP condition |
| Docs | `python tools/lint_docs.py --summary --dash-base $(git merge-base HEAD bannerlord-1.4.5)` | `dead_links` and `orphan_features` equal Step 0.6; `ai_dashes: 0` |
| Doc gate | `python tools/lint_docs.py --fail-on-drift --summary; echo rc=$?` | `rc=0` |
| CLAUDE.md size | `python -c "import pathlib; b=pathlib.Path('CLAUDE.md').read_bytes().replace(b'\r\n', b'\n'); print(len(b)+b.count(b'\n'))"` | under 46000 |

Both `-p:` flags are mandatory on every build and test: `Main/TAOM.csproj:133-136` does not gate the
CopyModule target on `DisableModuleCopy`, so a build without `-p:ModuleId=` deploys into the game install.
Never `./build.ps1`. Never two builds at once in this worktree. Always build before a `--no-build` test.

## Scope

**In scope** (the only files you create, modify or delete):

- Part A (picks): the 18 paths `032481cc6` touches besides `CHANGELOG.md`; the paths `76f22e111`,
  `a39575410` and `12b1c2003` touch outside `CHANGELOG.md` and `docs/reviews/`. Listed exactly in
  Steps A1 and A2.
- Part B: create `Main/Features/CombatMechanics/Hooks/CombatMechanicsHooks.cs`,
  `TAOM.Tests/Infrastructure/CollisionDataFixture.cs`,
  `TAOM.Tests/Features/CombatMechanics/CombatMechanicsHooksTests.cs`,
  `TAOM.Tests/Features/CombatMechanics/CombatMechanicsContainerWiringTests.cs` (WP123 already created
  `Main/Features/Refuge/Hooks/RefugeDamageHooks.cs` and `TAOM.Tests/Features/Refuge/RefugeDamageHooksTests.cs`;
  leave them alone); modify `TaomCombatMechanicsModel.cs`,
  `CombatMechanicsIoC.cs`, `CrushThroughService.cs` (comment), `Domain/ChargeKnockdownContext.cs`
  (comment), `Domain/CrushThroughContext.cs` (comment), `Main/Features/Refuge/RefugeDamageReduction.cs`
  (comment), `CombatMechanicsModelInvariantsTests.cs`, `docs/features/combat-mechanics.md`,
  `docs/features/refuge.md`; single-owner `Main/SubModule.cs` (the one block). Only if present on the
  branch: the WP123 comment in `CombatMechanicsSettingsProvider.cs` and the WP106 crush-roll citation in
  `.claude/rules/harmony-patches.md`.
- Part C: the 15 files of the `c` spec (Step C1).
- Part M (maintainer): `docs/adrs/002-thin-entry-points.md`, `007-adapter-pattern.md`,
  `008-testability-requirements.md`, `003-no-regions.md`, `README.md`.

**Out of scope** (do not touch): `CHANGELOG.md` (D1); `docs/reviews/**` (review records, RCAs, Codex
prompts, REVIEW-LOG and lessons: WP142 syncs lessons); `docs/INDEX.md`, `docs/reference/feature-map.md`,
`tools/README.md`, `AGENTS.md` (the 1.4.x AGENTS.md has no "Architecture" row; CLAUDE.md's one-liner is
the 1.4.x home of that meaning); `docs/adrs/**` for the executor (Part M is the maintainer's);
`plans/**`; `Main/IoC.cs`, `Main/TAOM.csproj`, `TAOM.Tests/TAOM.Tests.csproj`, `Directory.Build.props`,
`.claude/settings*.json`; the `CombatMechanicsSettingsProvider` caching and the #741 mount branch (WP123's
`74e9172d4` and `bb68eeca3`; you only move what WP123 already landed); the 1.5.x-only
SignatureStrikes, RaceAbilities, CreatureSiegeRole, CreatureBandits glue and the #610 settings; the
deep-review lens files (absent; WP138 ports them); renaming or reformatting anything nearby.

## Git workflow

- Create the worktree from the main checkout (Step 0.1); work and commit only there, on
  `port148/127-architecture-refactors`. Never push, open a PR, tag, or merge into `bannerlord-1.4.5`.
  Run git only through the Bash tool (D19).
- Per-port procedure: apply a 1.5.x commit only with `git cherry-pick -n <sha>`; never let
  `cherry-pick`, `--continue`, `-C`, `-c` or `--amend --no-edit` create a commit. Drop bookkeeping:
  for every path under `CHANGELOG.md` or `docs/reviews/` that `git status --porcelain` shows after a
  pick, apply this table, then confirm `git status --porcelain -- CHANGELOG.md docs/reviews` prints
  nothing:

  | Status code | Command |
  |---|---|
  | any code with `U`, or `AA` | `git checkout --ours <path> && git add -- <path>` |
  | `A` (added by the pick) | `git rm -f -q -- <path>` |
  | `M` (changed cleanly) | `git checkout HEAD <path>` |

  Write these two `git checkout` forms without a ` -- ` separator: `block-dangerous-git.sh` prompts on
  that separator and an unattended session cannot answer. After resolving, run `test -f "$(git rev-parse --git-path CHERRY_PICK_HEAD)" && git cherry-pick --quit; true`
  (it forgets the sequencer state and keeps the index and worktree) before the next pick or commit.
- Stage with explicit `git add -- <paths>` (never `-A`, `-u` or `.`), then run `git commit -F <file>`
  in the NEXT sequential Bash call: never the same command, never a parallel sibling call. Pass the
  message path as `C:/Users/mikew/source/repos/taom-port/wp127-scripts/<file>`.
- Never `--no-verify`, `--amend`, `-C` or `-c`. If a hook denies a commit, STOP and report its message.
- After each commit: `git status --porcelain` prints nothing, and plain `git status` does not mention
  a cherry-pick in progress.
- Commit messages are given complete in each step (subject at most 72 characters, body wrapped at 72,
  no long dashes, no `Co-Authored-By` or "Generated with" line, trailers last, one `Ported-from:` per
  included 1.5.x commit). Change only the measured values a step names.

## Steps

### Step 0: Worktree, prerequisites, baseline, helper script

**0.1 Worktree.**
`mkdir -p /c/Users/mikew/source/repos/taom-port/wp127-scripts && git -C /c/Users/mikew/source/repos/TAOM worktree add C:/Users/mikew/source/repos/taom-port/127 -b port148/127-architecture-refactors bannerlord-1.4.5`
**Verify**: prints `Preparing worktree (new branch 'port148/127-architecture-refactors')`. If the branch
or folder exists already: STOP.

**0.2 Drift check** (the header command). Read any listed change; Steps 0.3, B0 and the script decide.

**0.3 Prerequisites and absence checks.**

```bash
cd /c/Users/mikew/source/repos/taom-port/127 && test -f Main/Composition/FeatureModules.cs && echo wp105-ok; \
grep -q "IsVictimAgentMount" Main/Features/Refuge/Hooks/RefugeDamageHooks.cs && echo wp123-ok; \
grep -cF 'public static string RepoPath(params string[] parts)' TAOM.Tests/Infrastructure/RepoPaths.cs; \
grep -cF 'public static string ReadSource(string relativePath, bool stripComments = false)' TAOM.Tests/Infrastructure/RepoPaths.cs; \
ls Main/Features/EditorCacheRebuild/Caching | wc -l; \
ls -d Main/Features/CombatMechanics/Hooks TAOM.Tests/Infrastructure/CollisionDataFixture.cs TAOM.Tests/Features/FieldCamp/FortificationSearchTests.cs 2>&1 | grep -c "No such file"; \
git grep -l "Protected-Virtual Boundary Seams\|Amendment: partial services" -- docs | wc -l; \
for f in Main/Features/FieldCamp/CampService.cs Main/Features/Refuge/RefugeService.cs Main/Features/Refuge/WardenService.cs Main/Features/SupplyLines/SupplyOrderService.cs; do git rev-parse HEAD:$f | cut -c1-9; done
```

**Verify**: `wp105-ok`; `wp123-ok`; `1`; `1`; `6`; `3`; `0`; then `560e434ad`, `094c2285b`, `121a74746`, `e5416df60`.
A missing `wp105-ok`, `wp123-ok` or RepoPaths member, a count other than shown for the folder or absence lines, or a
`docs/` hit: STOP. A different service blob is not yet a STOP: run
`git log --oneline 1b00d1acd..HEAD -- <that file>`, note it for the hand-off, and let Step A2's pick
decide (a conflict in a code path there is the STOP).

**0.4 Baseline.** Run Restore, Build, Test. **Verify**: build exit 0, `Failed: 0`. Record `Total`,
`Passed`, `Skipped` as **T0** (it should equal the latest count the integrator recorded in
`plans/README.md`). Any failure: STOP.

**0.5 Doc gate and size.** Run the Doc gate and CLAUDE.md size commands. Record both. If the doc gate is
not `rc=0` on the untouched branch, note the finding names; later steps must not add a new one.

**0.6 Docs baseline.** Run the Docs command; record `dead_links` and `orphan_features`.

**0.7 Helper script.** Write the block below, unchanged, to
`C:\Users\mikew\source\repos\taom-port\wp127-scripts\wp127_edit.py` with your file-writing tool. It
edits text at anchors it checks first; it never runs git. Specs `m1` and `m2` are for the maintainer
(Part M): never run them yourself.

```python
"""WP127 text edits (plan 127). Run from the worktree root:
    python ../wp127-scripts/wp127_edit.py <spec>
Every op is checked before anything is written; any mismatch prints STOP: and exits 1 with
no file changed. Run each spec once: a second run stops, because its anchors are gone."""
import datetime
import os
import sys

TODAY = datetime.date.today().isoformat()


class Stop(Exception):
    pass


def read(path):
    with open(path, "r", encoding="utf-8", newline="") as f:
        return f.read()


def write(path, text):
    with open(path, "w", encoding="utf-8", newline="") as f:
        f.write(text)


def one(lines, anchor, path):
    hits = [i for i, line in enumerate(lines) if anchor in line]
    if len(hits) != 1:
        raise Stop("%s: %r is on %d lines, expected 1" % (path, anchor, len(hits)))
    return hits[0]


def run(ops):
    files = {}
    for op in ops:
        kind, path = op[0], op[1]
        if path not in files:
            if not os.path.isfile(path):
                raise Stop("missing file " + path)
            files[path] = read(path)
        text = files[path]
        eol = "\r\n" if "\r\n" in text else "\n"
        lines = text.split(eol)
        if kind in ("sub", "sub?"):
            count = text.count(op[2])
            if kind == "sub?" and count == 0:
                continue
            if count != 1:
                raise Stop("%s: %r occurs %d times, expected 1" % (path, op[2], count))
            files[path] = text.replace(op[2], op[3])
            continue
        if kind == "absent":
            if op[2] in text:
                raise Stop("%s: %r is still present" % (path, op[2]))
            continue
        if kind == "line":
            i = one(lines, op[2], path)
            lines[i:i + 1] = op[3]
        elif kind == "lines":
            hits = [i for i, line in enumerate(lines) if op[2] in line]
            if len(hits) != op[3]:
                raise Stop("%s: %r is on %d lines, expected %d" % (path, op[2], len(hits), op[3]))
            for i in reversed(hits):
                lines[i:i + 1] = op[4]
        elif kind == "before":
            i = one(lines, op[2], path)
            lines[i:i] = op[3]
        elif kind == "after":
            i = one(lines, op[2], path)
            lines[i + 1:i + 1] = op[3]
        elif kind == "after-unless":
            if op[3] in text:
                continue
            i = one(lines, op[2], path)
            lines[i + 1:i + 1] = op[4]
        elif kind == "after-close":
            i = one(lines, op[2], path)
            if i + 1 >= len(lines) or lines[i + 1] != "    }":
                raise Stop("%s: the line after %r is not '    }'" % (path, op[2]))
            lines[i + 2:i + 2] = op[3]
        elif kind == "block":
            old, new = op[2], op[3]
            starts = [i for i in range(len(lines) - len(old) + 1) if lines[i:i + len(old)] == old]
            if len(starts) != 1:
                raise Stop("%s: block starting %r found %d times, expected 1" % (path, old[0], len(starts)))
            lines[starts[0]:starts[0] + len(old)] = new
        elif kind == "cut":
            i = one(lines, op[2], path)
            j = one(lines, op[3], path)
            if j < i:
                raise Stop("%s: cut end %r is above its start" % (path, op[3]))
            del lines[i:j + 1 + op[4]]
        else:
            raise Stop("unknown op " + kind)
        files[path] = eol.join(lines)
    return files


def fill(value, values):
    if isinstance(value, str):
        for key, text in values.items():
            value = value.replace(key, text)
        return value
    if isinstance(value, list):
        return [fill(v, values) for v in value]
    return value


def main():
    if len(sys.argv) != 2 or sys.argv[1] not in SPECS:
        print("STOP: usage: wp127_edit.py <" + "|".join(sorted(SPECS)) + ">")
        return 1
    name = sys.argv[1]
    values = {"@@TODAY@@": TODAY, "@@MONTH@@": TODAY[:7]}
    if name == "b-docs":
        hooks = read(HOOKS).replace("\r\n", "\n").split("\n")
        rolls = [str(i + 1) for i, line in enumerate(hooks) if "randomRoll: MBRandom.RandomFloat" in line]
        if len(rolls) != 1:
            print("STOP: %s has %d randomRoll lines, expected 1" % (HOOKS, len(rolls)))
            return 1
        values["@@ROLL@@"] = rolls[0]
    ops = [tuple(fill(part, values) for part in op) for op in SPECS[name]]
    try:
        files = run(ops)
    except Stop as stop:
        print("STOP: " + str(stop))
        return 1
    for path, text in files.items():
        write(path, text)
    print("OK %s: %d ops, %d files" % (name, len(ops), len(files)))
    return 0


HOOKS = "Main/Features/CombatMechanics/Hooks/CombatMechanicsHooks.cs"
HOOKS_TESTS = "TAOM.Tests/Features/CombatMechanics/CombatMechanicsHooksTests.cs"
INVARIANTS = "TAOM.Tests/Features/CombatMechanics/CombatMechanicsModelInvariantsTests.cs"
FIXTURE = "TAOM.Tests/Infrastructure/CollisionDataFixture.cs"
ECR = "docs/features/editor-cache-rebuild.md"
SITES = "docs/reference/taleworlds-api-snapshot/reflection-sites.md"
FORT = "TAOM.Tests/Features/FieldCamp/FortificationSearchTests.cs"
CM = "docs/features/combat-mechanics.md"
RR = ".ai/review-reference.md"
FENCE = "`" * 3
SEAM_ROWS = [
    '| No sealed types in services | ADR-007: `ICareerHeroAdapter` not `Hero`; the one exception is a protected-virtual boundary seam (ADR-007 "Exceptions") |',
    "| Interfaces that earn their file | Every adapter has one (ADR-007); a service gets one only when a test fakes it or a second class implements it (ADR-002) |",
]
THIN_PATCH = "- Patches are **thin entry points**: delegate ALL logic to a service, directly or through an `IOnXxx` hook interface when the patch needs a narrow seam or a test fake"


def invariant_tests(refuge_pin):
    return [
        "",
        "    // ADR-002 caps an entry point at 150 lines (#737). Every feature's damage seam lands in this one slot, so each keeps",
        "    // its glue in its own Hooks/ facade and adds a delegate line here.",
        "    [TestMethod]",
        "    public void ModelSource_StaysUnderTheEntryPointCeiling()",
        "    {",
        "        var lines = File.ReadAllLines(RepoPaths.RepoPath(ModelSource.Split('/'))).Length;",
        "",
        "        Assert.IsTrue(lines < 150,",
        "            $\"TaomCombatMechanicsModel.cs is {lines} lines, over ADR-002's 150 for an entry point. Put the new seam's glue in its feature's Hooks/ facade and leave one delegate line here.\");",
        "    }",
        "",
        "    // A live Agent cannot be built outside the game, so the facades' tests cannot see two agents swapped, the verdict flag",
        "    // flipped, the wrong agent origin read, or the raw damage passed where base's result belongs. Each call the model makes",
        "    // into CombatMechanicsHooks and RefugeDamageHooks where such a mistake would still compile is pinned here; the context",
        "    // builders inside CombatMechanicsHooks moved unchanged from the model.",
        "    [DataTestMethod]",
        '    [DataRow("ApplyDamageReductions", "' + refuge_pin + '")]',
        '    [DataRow("CalculateShieldDamage", "_combat.ShieldDamage(in attackInformation, base.CalculateShieldDamage(in attackInformation, baseDamage))")]',
        '    [DataRow("DecideCrushedThrough", "_combat.CrushThrough(attackerAgent, defenderAgent, totalAttackEnergy, attackDirection, strikeType, defendItem, isPassiveUsageHit)")]',
        '    [DataRow("CalculateRemainingMomentum", "_combat.CleaveMomentum(attacker, originalMomentum, in collisionData)")]',
        '    [DataRow("DecideWeaponCollisionReaction", "_combat.CollisionReaction(attacker, momentumRemaining, in collisionData, colReaction)")]',
        '    [DataRow("DecideAgentKnockedDownByBlow", "_combat.ChargeKnockdown(attackerAgent, victimAgent, in collisionData, in blow)")]',
        "    public void ModelSource_HandsEachSeamItsOwnAgents(string overrideName, string call)",
        "    {",
        '        StringAssert.Contains(OverrideText(overrideName), call, $"{overrideName} must call {call}");',
        "    }",
        "",
        "    // From the override's name to the next override, comments blanked. A base call reads \".Name(\", so \" Name(\" finds the",
        "    // declaration.",
        "    private static string OverrideText(string name)",
        "    {",
        "        var source = RepoPaths.ReadSource(ModelSource, stripComments: true);",
        '        var start = source.IndexOf($" {name}(", StringComparison.Ordinal);',
        '        Assert.IsTrue(start >= 0, $"TaomCombatMechanicsModel no longer declares {name}");',
        '        var end = source.IndexOf("public override", start, StringComparison.Ordinal);',
        "        return end < 0 ? source.Substring(start) : source.Substring(start, end - start);",
        "    }",
    ]


def red1(refuge_pin):
    return [
        ("before", INVARIANTS, "using System.Linq;", ["using System.IO;"]),
        ("before", INVARIANTS, "using TAOM.Tests.Migration;", ["using TAOM.Tests.Infrastructure;"]),
        ("after", INVARIANTS, 'private const string ModelFullName = "TAOM.Features.CombatMechanics.Models.TaomCombatMechanicsModel";',
         ['    private const string ModelSource = "Main/Features/CombatMechanics/Models/TaomCombatMechanicsModel.cs";']),
        ("after-close", INVARIANTS, "update ExpectedOverrides deliberately if a new mechanic landed.", invariant_tests(refuge_pin)),
    ]


RED2 = [
    ("sub", FIXTURE, "(v1.5.4 <c>AttackCollisionData.cs:20,82,108,138</c>)", "(v1.4.8 <c>AttackCollisionData.cs:20,82,108,138</c>)"),
    ("sub", FIXTURE, "Reading the result runs engine getters, so a caller is <c>RequiresGame</c>.",
     "Reading the result runs engine getters, so a caller needs the game's DLLs, which TAOM.Tests copies from the install."),
    ("line", HOOKS_TESTS, '[TestCategory("RequiresGame")]', []),
    ("line", HOOKS_TESTS, "engine struct's getter runs engine code, hence the category.",
     ["/// engine struct's getter runs engine code, so these need the game's DLLs, which TAOM.Tests copies from the install."]),
    ("after", HOOKS_TESTS, "private IShieldPenetrationService _shield = null!;", ["    private ICombatMechanicsConfigProvider _config = null!;"]),
    ("after", HOOKS_TESTS, "_shield = Substitute.For<IShieldPenetrationService>();", ["        _config = Substitute.For<ICombatMechanicsConfigProvider>();"]),
    ("line", HOOKS_TESTS, "_sut = new CombatMechanicsHooks(_crush, _charge, _creature, _shield, _settings);",
     ["        _sut = new CombatMechanicsHooks(_crush, _charge, _creature, _shield, _config, _settings);"]),
    ("line", HOOKS_TESTS, "public void HorseChargePenetration_FeatureOn_IsTheSetting()", ["    public void HorseChargePenetration_FeatureOn_IsTheConfigValue()"]),
    ("lines", HOOKS_TESTS, "_settings.ChargeHorsePenetration.Returns(0.55f);", 2,
     ["        _config.GetConfig().Returns(new CombatMechanicsConfig { ChargeKnockdown = new ChargeKnockdownConfig { HorseChargePenetration = 0.55f } });"]),
]

SPECS = {
    "a1": [
        ("sub", ECR, "was deleted with its scaffold in 2026-09 (plan 025).",
         "was deleted with its scaffold in @@MONTH@@ (1.5.x plan 025, ported)."),
        ("sub", ECR, "and v1.5.3 `SandBoxNavigationCache.CheckBeingNeighbor` builds its own path with an extra-cost multiplier of 2 where Phase 1 passes 1.",
         "and v1.4.8 `SandBoxNavigationCache.CheckBeingNeighbor` builds its own path with an extra-cost multiplier of 2 where Phase 1 passes 1 (`SandBoxNavigationCache.cs:134` against `:88` in the installed v1.4.8 decompile)."),
        ("sub", ECR, "it was deleted in 2026-09 (plan 025).", "it was deleted in @@MONTH@@ (1.5.x plan 025, ported)."),
        ("sub", ECR, "- 2026-09-24: **Deleted the unwired path-reuse scaffold (plan 025).**",
         "- @@TODAY@@: **Deleted the unwired path-reuse scaffold (1.5.x plan 025, ported).**"),
        ("sub", SITES, "Status (2026-09-24): the `PathReuseCache._store` row (added in `41258657`) is removed together with the unwired path-reuse scaffold it covered (plan 025).",
         "Status (@@TODAY@@): the `PathReuseCache._store` row (added in `41258657`) is removed together with the unwired path-reuse scaffold it covered (1.5.x plan 025, ported)."),
    ],
    "a2": [
        ("line", FORT, "nearest wins, and a NaN distance never wins. Pure, so the class is untagged and hosted CI",
         ["/// nearest wins, and a NaN distance never wins. Pure, so it needs no game assemblies; the"]),
        ("line", FORT, "runs it; the services' own keep-out tests stay in their RequiresGame classes.",
         ["/// services' own keep-out tests stay in CampServiceTests and RefugeServiceTests."]),
    ],
    "b-red1": red1("RefugeDamageHooks.Reduce(_refugeDefense, in attackInformation, result)"),
    "b-red2": RED2,
    "b-code": [
        ("after", HOOKS, "private readonly IShieldPenetrationService _shieldPenetrationService;", ["    private readonly ICombatMechanicsConfigProvider _configProvider;"]),
        ("after", HOOKS, "IShieldPenetrationService shieldPenetrationService,", ["        ICombatMechanicsConfigProvider configProvider,"]),
        ("after", HOOKS, "_shieldPenetrationService = shieldPenetrationService;", ["        _configProvider = configProvider;"]),
        ("line", HOOKS, "// ChargeHorsePenetration getter ChargeKnockdownService's Branch B reads, so both use one number",
         ["    // ChargeKnockdown.HorseChargePenetration config value ChargeKnockdownService's Branch B reads, so both use one number"]),
        ("line", HOOKS, "? _settingsProvider.ChargeHorsePenetration", ["            ? _configProvider.GetConfig().ChargeKnockdown.HorseChargePenetration"]),
        ("sub", HOOKS, "SandboxAgentApplyDamageModel v1.5.4 :773-787", "SandboxAgentApplyDamageModel v1.4.8 :641-655"),
        ("sub", HOOKS, "is built only when the model asks, after the race abilities.", "is built only when the model asks."),
        ("after", "Main/Features/CombatMechanics/CombatMechanicsIoC.cs",
         "container.Register<IShieldPenetrationService, ShieldPenetrationService>(Reuse.Singleton);",
         ["        // The campaign damage model's boundary to the four services above (#737); SubModule resolves it once per game start.",
          "        container.Register<Hooks.CombatMechanicsHooks>(Reuse.Transient);"]),
        ("block", "Main/SubModule.cs",
         ["            IoC.Resolve<Features.CombatMechanics.ICrushThroughService>(),",
          "            IoC.Resolve<Features.CombatMechanics.IChargeKnockdownService>(),",
          "            IoC.Resolve<Features.CombatMechanics.ICreatureCombatService>(),",
          "            IoC.Resolve<Features.CombatMechanics.IShieldPenetrationService>(),",
          "            IoC.Resolve<Features.CombatMechanics.ICombatMechanicsConfigProvider>(),",
          "            IoC.Resolve<Features.CombatMechanics.ICombatMechanicsSettingsProvider>(),"],
         ["            IoC.Resolve<Features.CombatMechanics.Hooks.CombatMechanicsHooks>(),"]),
        ("sub", "Main/Features/CombatMechanics/CrushThroughService.cs", "(TaomCombatMechanicsModel.DecideCrushedThrough \u2192 here)",
         "(TaomCombatMechanicsModel.DecideCrushedThrough \u2192 CombatMechanicsHooks.CrushThrough \u2192 here)"),
        ("sub", "Main/Features/CombatMechanics/Domain/ChargeKnockdownContext.cs", "// line in the model when a factor becomes real",
         "// line in CombatMechanicsHooks when a factor becomes real"),
        ("sub", "Main/Features/CombatMechanics/Domain/CrushThroughContext.cs", "Built by TaomCombatMechanicsModel from the sealed",
         "Built by CombatMechanicsHooks from the sealed"),
        ("block", "Main/Features/Refuge/RefugeDamageReduction.cs",
         ["/// sites (TaomCombatMechanicsModel.ApplyDamageReductions, real-time; TaomCombatSimulationModel",
          "/// .SimulateHit, auto-resolve): <b>a reduction r scales the FINAL damage by (1 - r).</b>"],
         ["/// sites (TaomCombatMechanicsModel.ApplyDamageReductions through Hooks.RefugeDamageHooks, real-time;",
          "/// TaomCombatSimulationModel.SimulateHit, auto-resolve): <b>a reduction r scales the FINAL damage by (1 - r).</b>"]),
        ("sub?", "Main/Features/CombatMechanics/CombatMechanicsSettingsProvider.cs", "the one blow TaomCombatMechanicsModel hands to",
         "the one blow CombatMechanicsHooks hands to"),
    ],
    "b-docs": [
        ("line", CM, "Thin model \u2192 four pure services (ADR-002/007; gamemodels.md rule 4)",
         ["Thin model \u2192 `CombatMechanicsHooks` \u2192 four pure services (ADR-002/007; gamemodels.md rule 4): every override is base plus one call per feature, and the hooks extract primitives/DTOs at the boundary and delegate (#737). Services precompute `HashSet`/`Dictionary` lookups at construction (per-hit hot path: no LINQ or allocation per call) and take caller-supplied random rolls so probability tests are deterministic."]),
        ("line", CM, "| `DecideAgentKnockedDownByBlow` | `ChargeKnockdownService` |",
         ["| `DecideAgentKnockedDownByBlow` | `ChargeKnockdownService` | the weight-driven two-branch rules (below), which `CombatMechanicsHooks.ChargeKnockdown` asks for a horse charge only; every other hit is base |"]),
        ("line", CM, "| `Main/Features/CombatMechanics/Models/TaomCombatMechanicsModel.cs` |",
         ["| `Main/Features/CombatMechanics/Models/TaomCombatMechanicsModel.cs` | The 10 overrides, each base plus one call per feature; under ADR-002's 150 lines (`CombatMechanicsModelInvariantsTests` pins the exact set, the ceiling and which agent reaches which seam) |",
          "| `Main/Features/CombatMechanics/Hooks/CombatMechanicsHooks.cs` | The model's boundary to the four services, one method per seam, with the context builders and primitive extractors (#737) |"]),
        ("line", CM, "Single registration: `AddModel<AgentApplyDamageModel>(new TaomCombatMechanicsModel(...))`",
         ["| `Main/SubModule.cs` | Single registration: `AddModel<AgentApplyDamageModel>(new TaomCombatMechanicsModel(...))`, handed `CombatMechanicsHooks` from the container |"]),
        ("line", CM, "| `TAOM.Tests/Features/CombatMechanics/*` |",
         ["| `TAOM.Tests/Features/CombatMechanics/*` | Service/provider/resolver tests + `CombatMechanicsModelInvariantsTests` (derivation + abstract parent + exact override set + line ceiling + agent-per-seam pins) + `CombatMechanicsHooksTests` + `CombatMechanicsContainerWiringTests` |"]),
        ("sub", "docs/features/refuge.md", "TaomCombatMechanicsModel.ApplyDamageReductions (real-time)",
         "TaomCombatMechanicsModel.ApplyDamageReductions (real-time, via RefugeDamageHooks)"),
        ("after-unless", "docs/features/refuge.md", "| `RefugeDefenseService.cs` (+ `IRefugeBook`) |", "`Hooks/RefugeDamageHooks.cs`",
         ["| `Hooks/RefugeDamageHooks.cs` | The real-time reduction the campaign damage model applies: the victim's party from its agent origin, then `RefugeDamageReduction` (#737) |"]),
        ("sub?", ".claude/rules/harmony-patches.md", "`TaomCombatMechanicsModel.cs:177`", "`CombatMechanicsHooks.cs:@@ROLL@@`"),
    ],
    "c": [
        ("line", RR, "ADR-007 (sealed type in service), ADR-002 (fat entry point)",
         ['- **CRITICAL**: ADR-007 (sealed type in service; a protected-virtual boundary seam that meets ADR-007 "Exceptions" is not one), ADR-002 (fat entry point), Harmony target method does not exist in the installed engine (the version pinned in `.claude/pinned-game-version.txt`)']),
        ("line", RR, "**One-liner:** `[HarmonyPatch/GameModel/CampaignBehavior]`",
         ["**One-liner:** `[HarmonyPatch/GameModel/CampaignBehavior]` -> `Service` -> `IAdapter` (sealed types); an `IOnXxx` hook interface sits between patch and service only when the patch needs a narrow seam or a test fake"]),
        ("line", RR, "| No sealed types in services |", SEAM_ROWS),
        ("line", RR, "    IMyFeatureService.cs", ["    IMyFeatureService.cs     <-- only if a test fakes it or a second class implements it"]),
        ("line", RR, "delegate ALL logic to services via `IHookInterface`", [THIN_PATCH]),
        ("line", RR, "### 1. Hook Pattern (Harmony -> Hook Interface -> Service)",
         ["### 1. Hook Pattern (Harmony -> Hook Interface -> Service), optional", "",
          "Optional layer. Most patches resolve their service at the boundary and call it directly (`patch -> service -> adapter`). Add an `IOnXxx` hook interface only when the patch needs a narrow seam over a wide service or a test fakes the hook; a hook that only forwards one call is a deletion candidate when its files are next touched."]),
        ("line", ".claude/rules/csharp-architecture.md", "| No sealed types in services |", SEAM_ROWS),
        ("line", ".claude/rules/csharp-architecture.md", "\u251c\u2500\u2500 IMyFeatureService.cs",
         ["\u251c\u2500\u2500 IMyFeatureService.cs     \u2190 only if a test fakes it or a second class implements it"]),
        ("line", ".claude/rules/csharp-patterns.md", "## 1. Hook Pattern (Harmony \u2192 Hook Interface \u2192 Service)",
         ["## 1. Hook Pattern (optional: Harmony \u2192 Hook Interface \u2192 Service)", "",
          'Optional layer. Most patches resolve their service at the boundary and call it directly (patch \u2192 service \u2192 adapter, CLAUDE.md "Architecture (One-liner)"). Add an `IOnXxx` hook only when the patch needs a narrow seam over a wide service (`PartyUpgradeResourceCheckHook` narrows `ISpecialResourceService` for the party-upgrade patches) or a test fakes the hook; a hook that forwards one call adds a file and hides nothing.']),
        ("line", ".claude/rules/harmony-patches.md", "delegate ALL logic to services via `IHookInterface`", [THIN_PATCH]),
        ("line", ".claude/rules/think-before-coding.md", "No interface-with-one-implementation",
         ['4. **Only then: write the minimum.** No interface unless it wraps an engine type (an adapter), a test fakes it, a second class implements it, or it narrows a wide service for a patch (a hook, CLAUDE.md "Architecture (One-liner)"); no "in case we need it later" plumbing (`simplicity-criterion.md` rejects it).']),
        ("line", ".claude/agents/feature-builder.md", "Entry Points (thin, <150 lines)",
         ["Entry Points (thin, <150 lines) \u2192 Service \u2192 IAdapter (sealed types)"]),
        ("before", ".claude/agents/feature-builder.md", "## Rules You MUST Follow",
         ["A hook interface (`IOnXxx`) goes between an entry point and its service only when the patch needs a narrow seam or a test fake. A service gets an `I{Name}Service` interface only when a test fakes it or a second implementation exists; every adapter has one (ADR-002, ADR-007).", ""]),
        ("line", ".claude/agents/feature-builder.md", "\u251c\u2500\u2500 I{Name}Service.cs",
         ["\u251c\u2500\u2500 I{Name}Service.cs            # Only if a test fakes it or a 2nd impl exists"]),
        ("line", ".claude/agents/feature-builder.md", "container.Register<I{Name}Service, {Name}Service>(Reuse.Singleton);",
         ["        container.Register<{Name}Service>(Reuse.Singleton);",
          "        // when a test fakes it: container.Register<I{Name}Service, {Name}Service>(Reuse.Singleton);"]),
        ("line", ".claude/skills/new-feature/SKILL.md", "- `I{FeatureName}Service.cs`",
         ["- `I{FeatureName}Service.cs`: only when a test fakes the service or a second implementation exists (ADR-002); otherwise register the concrete class, `container.Register<{FeatureName}Service>(Reuse.Singleton)`"]),
        ("line", ".claude/skills/codex-verify/SKILL.md", "1. Adapter pattern violations (ADR-007)",
         ['1. Adapter pattern violations (ADR-007): sealed TaleWorlds types in service classes, except a protected-virtual boundary seam that meets ADR-007 "Exceptions"']),
        ("line", ".claude/skills/improve/references/audit-playbook.md", "- Adapter violations: services touching",
         ['- Adapter violations: services touching `Hero`/`Settlement`/`MobileParty` etc. directly instead of `I*Adapter` (ADR-007); a protected-virtual boundary seam that meets ADR-007 "Exceptions" is not one.']),
        ("line", ".claude/skills/deep-review/SKILL.md", "1. **Adapter Pattern (ADR-007):**",
         ['1. **Adapter Pattern (ADR-007):** Services NEVER reference TaleWorlds types directly (Hero, Clan, Kingdom, etc.). They use IXxxAdapter interfaces. Flag ANY direct TaleWorlds type usage in service classes. Two exceptions, both under ADR-007 "Exceptions": the value types in "Value Types Don\'t Need Adapters" (`Vec2`, `TextObject`, `ExplainedNumber`), and a `protected virtual` member of the service that meets the four conditions in "Protected-Virtual Boundary Seams" (a test subclass overrides it). Flag a seam that breaks a condition, and any other TaleWorlds use outside a seam or a private helper only seams call (part of the seam body, ADR-007).']),
        ("line", ".claude/skills/deep-review/SKILL.md", "6. **Interface Segregation:**",
         ["6. **Interface Segregation:** Every adapter has an interface. A service gets one only when a test fakes it or a second implementation exists (ADR-002 guideline 2): flag a new service interface that no test fakes and no second class implements, and do not demand one that nothing would fake."]),
        ("after", "docs/ai-includes/architecture.md", "- Only use adapter interfaces, never sealed types (ADR-007)",
         ['- TaleWorlds access outside an adapter, other than the value types, only through protected-virtual boundary seams (ADR-007 "Exceptions")',
          "- An `IXxxService` interface only when a test fakes it or a second implementation exists (ADR-002)"]),
        ("line", "docs/ai-includes/architecture.md", "- Fully unit testable with mocked adapters",
         ["- Fully unit testable with mocked adapters, or a test subclass that overrides its boundary seams"]),
        ("block", "docs/ai-includes/architecture.md",
         ["Entry Point \u2192 Hook Interface \u2192 Service \u2192 Engine \u2192 Adapter",
          "     \u2193            \u2193              \u2193         \u2193         \u2193",
          "   Thin      Orchestrate      Logic     Algorithm   Wrap",
          FENCE],
         ["Entry Point \u2192 [Hook Interface] \u2192 Service \u2192 Engine \u2192 Adapter",
          "     \u2193               \u2193              \u2193        \u2193         \u2193",
          "   Thin     Narrow seam (opt.)    Logic  Algorithm   Wrap",
          FENCE, "",
          "The hook interface is optional: a patch uses one only when it needs a narrow seam or a test fake; most patches call their service directly."]),
        ("sub", "docs/ai-includes/decompiled-code-analysis.md", "Hook Interface (IOn[EventName])",
         "Hook Interface (IOn[EventName]), only when the patch needs a narrow seam or a test fake"),
        ("line", "docs/ai-includes/decompiled-code-analysis.md", "container.Register<IFeatureService, FeatureService>(Reuse.Singleton);",
         ["container.Register<FeatureService>(Reuse.Singleton);",
          "// only when a test fakes the service or a second implementation exists (ADR-002);",
          "// this replaces the line above, never sits beside it:",
          "// container.Register<IFeatureService, FeatureService>(Reuse.Singleton);"]),
        ("line", "docs/ai-includes/decompiled-code-analysis.md", "container.Register<IOnSomeEvent, FeatureHook>(Reuse.Transient);",
         ["// only when the patch needs a narrow seam or a test fakes the hook:",
          "// container.Register<IOnSomeEvent, FeatureHook>(Reuse.Transient);"]),
        ("sub", "docs/ai-includes/decompiled-code-analysis.md", "IOn[EventName].cs", "IOn[EventName].cs (only for a narrow seam or a test fake)"),
        ("sub", "docs/ai-includes/decompiled-code-analysis.md", "I[Feature]Service.cs", "I[Feature]Service.cs (only if a test fakes it or a second class implements it)"),
        ("sub", "docs/ai-includes/decompiled-code-analysis.md", "[Feature]HookTests.cs", "[Feature]HookTests.cs (when there is a hook class)"),
        ("sub", "docs/ai-includes/decompiled-code-analysis.md", "var service = IoC.Resolve<IFeatureService>();", "var service = IoC.Resolve<FeatureService>();"),
        ("sub", "docs/ai-includes/decompiled-code-analysis.md", "(patches -> hooks -> services -> adapters)",
         "(patches -> services -> adapters, with a hook interface only where a narrow seam or a test fake needs one)"),
        ("line", "docs/ai-includes/patterns.md", "Separation between game integration and business logic.",
         ['Separation between game integration and business logic. This layer is optional. Use it only when a patch needs a narrow seam over a wide service or a test fakes the hook; most patches call their service directly (CLAUDE.md "Architecture (One-liner)").']),
        ("sub", "docs/reference/engine/submodule-lifecycle-and-harmony.md", "`[Patch]\u2192IHook\u2192Service\u2192IAdapter`", "`[Patch]\u2192Service\u2192IAdapter`"),
        ("sub", "docs/reference/engine/submodule-lifecycle-and-harmony.md",
         "`[HarmonyPatch/GameModel/CampaignBehavior] \u2192 IHook \u2192 Service \u2192 IAdapter`",
         "`[HarmonyPatch/GameModel/CampaignBehavior] \u2192 Service \u2192 IAdapter`"),
        ("after", "docs/reference/engine/submodule-lifecycle-and-harmony.md", "**The architecture one-liner is wired HERE:**",
         ['  (an `IOnXxx` hook in between only when the patch needs a narrow seam or a test fake, CLAUDE.md "Architecture (One-liner)")']),
        ("line", "CLAUDE.md", "**Mod**: `[HarmonyPatch/GameModel/CampaignBehavior]`",
         ["**Mod**: `[HarmonyPatch/GameModel/CampaignBehavior]` -> `Service` -> `IAdapter` (sealed types); `IOnXxx` hook optional"]),
    ],
    "m1": [
        ("sub", "docs/adrs/002-thin-entry-points.md", "The 2026-09-23 audit found most", "The 2026-09-23 audit of the 1.5.x line found most"),
        ("sub", "docs/adrs/002-thin-entry-points.md", "`plans/_audit/2026-09-23-opus/lane-2.findings.md`",
         "`origin/bannerlord-1.5.x:plans/_audit/2026-09-23-opus/lane-2.findings.md`"),
        ("sub", "docs/adrs/007-adapter-pattern.md", "(decision 55)", "(1.5.x maintainer decision 55)"),
        ("sub", "docs/adrs/007-adapter-pattern.md", "the 2026-09-23 audit counted 94 seam members",
         "the 2026-09-23 audit of the 1.5.x line counted 94 seam members"),
        ("sub", "docs/adrs/007-adapter-pattern.md", "`plans/_audit/2026-09-23-opus/verify-a-batch-02.md`",
         "`origin/bannerlord-1.5.x:plans/_audit/2026-09-23-opus/verify-a-batch-02.md`"),
        ("sub", "docs/adrs/007-adapter-pattern.md", "The plan 021", "The 1.5.x plan 021"),
        ("sub", "docs/adrs/007-adapter-pattern.md", "that logic into the services (decision 56).",
         "that logic into the services (1.5.x decision 56; this line carries plan 026 too)."),
    ],
    "m2": [
        ("sub", "docs/adrs/003-no-regions.md", "as `FireAndTributeService` is.",
         "as `VolunteerRecruitmentService` is (its 16 culture pools are `Main/Features/TroopProgression/RecruitmentPools/VolunteerRecruitmentService.<Culture>.cs`)."),
        ("sub", "docs/adrs/003-no-regions.md", "(for example `FireAndTributeService.Steering.cs`)", "(for example `VolunteerRecruitmentService.Gondor.cs`)"),
        ("line", "docs/adrs/003-no-regions.md", "Decided by the maintainer on 2026-10-21, after the Fire and Tribute review (#870)",
         ["Decided by the maintainer on 2026-10-21 on the 1.5.x line, after the Fire and Tribute review (#870) found other classes already split this way, and ported to this line on @@TODAY@@. Known gap when it was ported: `VolunteerRecruitmentService` does not list its parts in its remarks. Bring it into line when it is next changed."]),
        ("line", "docs/adrs/README.md", "| [003](./003-no-regions.md) | No Code Regions |",
         ["| [003](./003-no-regions.md) | No Code Regions | No `#region` directives, use class decomposition; a service may be split into partial files by concern (2026-10-21 amendment) | Accepted | Standard |"]),
    ],
}

if __name__ == "__main__":
    sys.exit(main())
```

**Verify**: `WT$ python ../wp127-scripts/wp127_edit.py nothing` prints a line starting `STOP: usage:` that
lists `a1|a2|b-code|b-docs|b-red1|b-red2|c|m1|m2`. (Planning dry-ran every spec
against the real blobs, with CRLF line endings as the worktree has them: all report `OK`, and the `c`
spec reproduces the 1.5.x end state of `docs/ai-includes/decompiled-code-analysis.md` byte for byte.)

### Part A: plans 025 and 026 (picks)

#### Step A1: Delete the two unreachable scaffolds (`032481cc6`, `76f22e111`)

1. `WT$ git cherry-pick -n 032481cc6`. Expected: a conflict in `CHANGELOG.md` only (simulated this
   session onto `1b00d1acd`). Drop bookkeeping (Git workflow table), then the CHERRY_PICK_HEAD line.
2. `WT$ git cherry-pick -n 76f22e111`. Expected on top of step 1 (simulated this session): conflicts
   only in `CHANGELOG.md` (its entry from step 1 was dropped) and under `docs/reviews/`
   (`REVIEW-LOG.md`, three lessons files), plus three added review records
   (`codex-adversarial-025-...prompt.md`, `deep-review-025-...md`, `rca-delete-unreachable-scaffolds-2026-09-24.md`).
   Drop them all (table), then the CHERRY_PICK_HEAD line.
3. If either pick conflicts in `TAOM.Tests/Migration/ReflectionSiteBindingTests.cs`,
   `TAOM.Tests/Migration/GameAssemblies.cs` or `docs/reference/taleworlds-api-snapshot/reflection-sites.md`
   (other WPs add rows there), resolve that file by `git checkout --ours <file>`, then make exactly the
   pick's change by hand: delete the two lines `// --- EditorCacheRebuild path cache (PersistentPathCache.cs) ---`
   and the `[DataRow("TaleWorlds.Engine.PathReuseCache", ...)]` row; replace the comment
   `types like TaleWorlds.Engine's PathReuseCache aren't in GetAssemblies() until touched).` with
   `engine types aren't in GetAssemblies() until touched).`; delete the table row naming
   `TaleWorlds.Engine.PathReuseCache` and insert, directly above the first line that starts `Status (`,
   the `Status (2026-09-24): the \`PathReuseCache._store\` row ...` line exactly as
   `git show 76f22e111:docs/reference/taleworlds-api-snapshot/reflection-sites.md` has it. `git add` the
   file. A conflict in any other non-bookkeeping path: STOP.
4. `WT$ python ../wp127-scripts/wp127_edit.py a1` then
   `WT$ git add -- docs/features/editor-cache-rebuild.md docs/reference/taleworlds-api-snapshot/reflection-sites.md`.
   This dates the doc lines to today, labels them as the 1.5.x plan, and re-cites the path-cost sentence
   to v1.4.8 (verified: `:88` passes `1f`, `:134` passes `2f`).

**Verify**:
- `WT$ git diff --cached --name-status` lists exactly 18 paths: `D Main/Adapters/IEditorSceneAdapter.cs`;
  `M Main/Features/EditorCacheRebuild/CacheRebuildConfig.cs`; `D` the six
  `Main/Features/EditorCacheRebuild/Caching/*.cs`; `M Main/Features/EditorCacheRebuild/EditorCacheRebuildIoC.cs`;
  `M TAOM.Tests/Features/EditorCacheRebuild/CacheRebuildConfigProviderTests.cs`; `D` the four
  `TAOM.Tests/Features/EditorCacheRebuild/Caching/*Tests.cs`; `M` both Migration files;
  `M docs/features/editor-cache-rebuild.md`; `M docs/reference/taleworlds-api-snapshot/reflection-sites.md`.
- `WT$ git grep -n -E "PathReuseCache|IEditorSceneAdapter|NavigationPathCloner|SortedPathKey" -- Main TAOM.Tests`
  prints nothing (the retired-key test writes the JSON keys `enablePathReuse` and
  `enablePersistentPathCache`, which this pattern does not match).
- `WT$ grep -c "RetiredPathReuseScaffold_IsGoneFromTheTaomAssembly" TAOM.Tests/Features/EditorCacheRebuild/CacheRebuildConfigProviderTests.cs`
  prints `0`; `grep -c "GetConfig_JsonWithRetiredPathCacheKeys_StillLoadsTheLiveFields"` on the same file prints `1`.
- Build exit 0; Filtered `"FullyQualifiedName~CacheRebuildConfigProviderTests"`: `Passed: 21`, `Failed: 0`;
  Filtered `"FullyQualifiedName~EditorCacheRebuild|FullyQualifiedName~ReflectionSiteBindingTests"`: `Failed: 0`.

Write `msg-a1.txt`:

```
refactor(cache-rebuild): v2.0.28 - delete two unreachable scaffolds

Nothing changes in game. Two pieces of code that nothing ever called
are gone. Main/Adapters/IEditorSceneAdapter.cs was an adapter
interface with no implementation and no reference. The
EditorCacheRebuild Caching/ folder (PathReuseCache,
PersistentPathCache, their interfaces, NavigationPathCloner and
SortedPathKey) was a path-memoization scaffold that
EditorCacheRebuildIoC registered but nothing resolved or injected.
Both arrived in 6a80bac6 and never gained a caller.

Also removed: the reserved EnablePathReuse and
EnablePersistentPathCache config properties (never read), the 26
tests that kept the scaffold alive, and the ReflectionSiteBindingTests
row for PathReuseCache._store, which named a TaleWorlds.Engine type
that does not exist and only resolved to TAOM's own class. A config
that still carries the two keys loads as before (new test). The
feature doc stops promising that Phase 1 paths can be reused by Phase
2: on v1.4.8 Phase 1 keeps its path local and CheckBeingNeighbor
pathfinds with a different extra-cost multiplier.

Research: taom-src SandBoxNavigationCache (v1.4.8 :88 passes 1f,
:134 passes 2f); git grep found no other reference to the deleted
types on this line.
Not-tested: the in-game distance cache rebuild; no runtime path
changed.
Ported-from: 032481cc6d23d31e9f08de048bc4b96a2d3a1d99
Ported-from: 76f22e111a18673bc0d9a33be7090a787c1b4b98
```

Commit (next call): `WT$ git commit -F C:/Users/mikew/source/repos/taom-port/wp127-scripts/msg-a1.txt`.
**Verify**: exit 0; `git status --porcelain` empty.

#### Step A2: Move decision logic out of nine seams (`a39575410`, `12b1c2003`)

Both picks go into one commit: `12b1c2003` fixes a slip `a39575410` introduced (the refuge's per-row
peace check answered "release" when the refuge or its faction was missing), so no commit on this line
may carry the first without the second.

1. `WT$ git cherry-pick -n a39575410`. Expected: a conflict in `CHANGELOG.md` only. Drop bookkeeping,
   then the CHERRY_PICK_HEAD line.
2. `WT$ git cherry-pick -n 12b1c2003`. Expected (simulated this session on top of step 1): conflicts only
   in `docs/reviews/REVIEW-LOG.md` and three lessons files, plus two added records
   (`deep-review-026-seam-decision-logic-2026-09-24.md`, `rca-seam-decision-logic-2026-09-24.md`) and a
   `CHANGELOG.md` change. Drop them all, then the CHERRY_PICK_HEAD line. Keep the added
   `TAOM.Tests/Features/FieldCamp/FortificationSearchTests.cs`. A conflict in any `Main/` or `TAOM.Tests/`
   path, or in `docs/features/field-camp.md` or `supply-lines.md`: STOP.
3. `WT$ python ../wp127-scripts/wp127_edit.py a2` (the new test file's summary named hosted CI and
   `RequiresGame` classes, neither of which exists on 1.4.x), then
   `WT$ git add -- TAOM.Tests/Features/FieldCamp/FortificationSearchTests.cs`.

**Verify**:
- `WT$ git diff --cached --name-status` lists exactly 11 paths: `M` the four service files,
  `M TAOM.Tests/Features/FieldCamp/CampServiceTests.cs`, `A TAOM.Tests/Features/FieldCamp/FortificationSearchTests.cs`,
  `M` `RefugeServiceTests.cs`, `WardenServiceTests.cs`, `SupplyOrderServiceTests.cs`,
  `M docs/features/field-camp.md`, `M docs/features/supply-lines.md`.
- `WT$ grep -c "RequiresGame" TAOM.Tests/Features/FieldCamp/CampServiceTests.cs TAOM.Tests/Features/FieldCamp/FortificationSearchTests.cs TAOM.Tests/Features/Refuge/RefugeServiceTests.cs TAOM.Tests/Features/Refuge/WardenServiceTests.cs TAOM.Tests/Features/SupplyLines/SupplyOrderServiceTests.cs`
  prints `0` for each file.
- Build exit 0; Filtered
  `"FullyQualifiedName~CampServiceTests|FullyQualifiedName~FortificationSearchTests|FullyQualifiedName~RefugeServiceTests|FullyQualifiedName~WardenServiceTests|FullyQualifiedName~SupplyOrderServiceTests"`:
  `Passed: 336` (83 + 8 + 140 + 37 + 68, counted from the picked files), `Failed: 0`; Filtered
  `"FullyQualifiedName~CampsContainerWiringTests"`: `Failed: 0`.

Write `msg-a2.txt`:

```
refactor(seams): v2.0.28 - move decision logic out of nine seams

Nothing changes in game. Nine protected-virtual seams in
SupplyOrderService, RefugeService, WardenService and CampService held
TAOM decisions that no unit test ran, because each service's test
subclass overrides every seam: the supply payee routing and its
unreachable-payee fallback, the refund route and volunteer-slot walk,
the refuge raid-target filters and nearest pick, the peace release of
refuge-held prisoners, the warden companion filter, the promotion
policy, and the nearest town-or-castle search behind the camp and
refuge keep-outs.

Each decision now lives in its service as an internal method, and
each seam is one engine operation whose signature carries ids,
primitives and TAOM data classes only (the ADR-007 boundary-seam rule
this port series adds). The two copies of the fortification search
share FortificationSearch.NearestDistance, declared in CampService.cs.
Amounts, flags, log texts, filter order, the tie-break and the RNG
draws are as before. The port includes the 1.5.x review fixes: a
refuge's per-row peace check keeps a prisoner when the refuge or its
faction is missing, as the old walk did, and the promotion's rename
and enrol steps act on the hero the create step returned. 78 new
tests pin the moved logic.

Research: the four service files matched the 1.5.x parent of
a39575410 byte for byte; taom-src v1.4.8 MobileParty.MapFaction,
PrisonRoster and GetPosition2D, TroopRoster.GetCharacterAtIndex,
Hero.MapFaction, IFaction.IsAtWarWith, Settlement.GetPosition2D.
Not-tested: the seam bodies (gold transfers, roster, item and
volunteer-slot writes, the MobileParty.All and Settlement.All scans,
EndCaptivityAction, HeroCreator and the enrol actions) run only in
game.
Ported-from: a395754109cd26d829a83680dfce5d5dcf29cf55
Ported-from: 12b1c2003fbf2539f9df62246f70e655c20bfc6d
```

Commit (next call): `WT$ git commit -F C:/Users/mikew/source/repos/taom-port/wp127-scripts/msg-a2.txt`.
**Verify**: exit 0; `git status --porcelain` empty.

#### Part A done criteria

- [ ] Two commits with the subjects above; Build exit 0; Test `Failed: 0` and `Total` = T0 + 52
      (plan 025: 26 test methods deleted, `CacheRebuildConfigProviderTests` 20 to 21, one
      `ReflectionSiteBindingTests` row gone, so -26; plan 026: +78). Record it as **TA**. A different
      delta: STOP and report both numbers.
- [ ] `git diff --name-only bannerlord-1.4.5..HEAD -- CHANGELOG.md docs/reviews` prints nothing.

Parts A and B change no rule text, so the integrator may merge them without Parts C and M if the session
ends early.

### Part B: split the damage model (`f7e9bd0d2`, hand port)

`f7e9bd0d2` cannot be picked: it conflicts in five code paths on 1.4.x (simulated this session:
`CombatMechanicsSettingsProvider.cs`, `TaomCombatMechanicsModel.cs`, `StrikeContextFactory.cs`,
`SubModule.cs`, `RaceAbilitiesWiringTests.cs`) and half of it is 1.5.x-only glue. Re-implement it on
the 1.4.x model, which holds CombatMechanics plus Refuge only. Leave SignatureStrikeVerdicts,
RaceAbilities, CreatureSiegeRole, CreatureBandits and the #610 settings out.

#### Step B0: Check that WP123's form of `ApplyDamageReductions` is present

```bash
cd /c/Users/mikew/source/repos/taom-port/127 && grep -c "RefugeDamageHooks.Reduce(_refugeDefense, in attackInformation, result)" Main/Features/CombatMechanics/Models/TaomCombatMechanicsModel.cs; \
wc -l < Main/Features/CombatMechanics/Models/TaomCombatMechanicsModel.cs; \
git log --oneline 1b00d1acd..HEAD -- Main/Features/CombatMechanics/Models/TaomCombatMechanicsModel.cs; \
grep -c "IsVictimAgentMount" Main/Features/Refuge/Hooks/RefugeDamageHooks.cs; \
grep -c "\[TestMethod\]\|\[DataRow" TAOM.Tests/Features/Refuge/RefugeDamageHooksTests.cs
```

- **Expected** (WP123 merged its #741 fix; its Step C3 creates both refuge files): the first line prints
  `1`, the log names WP123's commit, the `IsVictimAgentMount` grep prints `1` and the test grep prints
  `8`. Read
  `git log -p 1b00d1acd..HEAD -- Main/Features/CombatMechanics/Models/TaomCombatMechanicsModel.cs`: the
  only change must be that `ApplyDamageReductions` calls `RefugeDamageHooks.Reduce(_refugeDefense, in
  attackInformation, result)` and that `VictimPartyId` and its `using` lines are gone. Any other change
  to the model: STOP, because Step B3 replaces the whole file.
- Anything else, including a first line of `0` or a missing refuge file: STOP and report the diff.

Record the line count (**N0**, for the commit body).

#### Step B1 (RED): pin the ceiling and the seams

`WT$ python ../wp127-scripts/wp127_edit.py b-red1`. It adds `using System.IO;`,
`using TAOM.Tests.Infrastructure;`, the `ModelSource` constant, the test
`ModelSource_StaysUnderTheEntryPointCeiling`, the six-row test `ModelSource_HandsEachSeamItsOwnAgents`
and its `OverrideText` helper to `CombatMechanicsModelInvariantsTests.cs` (the 1.5.x tests of
`f7e9bd0d2`, minus the two SignatureStrikes rows).

**Verify**: Build exit 0; Filtered `"FullyQualifiedName~CombatMechanicsModelInvariantsTests"`:
`Failed: 6` (the ceiling, because the model has N0 lines, and five seam pins, because no `_combat.` call
exists yet) and `Passed: 4` (the three existing tests, and the `ApplyDamageReductions` pin, which WP123's
call already satisfies).

#### Step B2 (RED): the facade tests

```bash
cd /c/Users/mikew/source/repos/taom-port/127 && git show f7e9bd0d2:TAOM.Tests/Infrastructure/CollisionDataFixture.cs > TAOM.Tests/Infrastructure/CollisionDataFixture.cs && \
git show f7e9bd0d2:TAOM.Tests/Features/CombatMechanics/CombatMechanicsHooksTests.cs > TAOM.Tests/Features/CombatMechanics/CombatMechanicsHooksTests.cs && \
git show f7e9bd0d2:TAOM.Tests/Features/CombatMechanics/CombatMechanicsContainerWiringTests.cs > TAOM.Tests/Features/CombatMechanics/CombatMechanicsContainerWiringTests.cs
```

Keep the 8-test `RefugeDamageHooksTests.cs` WP123 landed; never overwrite it.
Then `WT$ python ../wp127-scripts/wp127_edit.py b-red2`. The spec drops the
`RequiresGame` tags (D9), re-cites the fixture to v1.4.8 (same field lines, verified), hands the hooks a
`ICombatMechanicsConfigProvider` and points the two penetration tests at
`ChargeKnockdown.HorseChargePenetration` in the config (1.4.x has no MCM penetration setting). It
never touches the refuge test file.
The wiring test is used verbatim: its three substitutes (`IPathService`, `IModLogger`, `IRaceManager`)
are exactly what the 1.4.x CombatMechanics services take.

**Verify**: Build fails, and every `error CS` line names `Hooks` (the missing namespace
`TAOM.Features.CombatMechanics.Hooks`) or `CombatMechanicsHooks`. Any other error:
fix only if it is a typo you made, else STOP.

#### Step B3 (GREEN): the facades, the thin model, the wiring

1. `WT$ mkdir -p Main/Features/CombatMechanics/Hooks && git show f7e9bd0d2:Main/Features/CombatMechanics/Hooks/CombatMechanicsHooks.cs > Main/Features/CombatMechanics/Hooks/CombatMechanicsHooks.cs`
2. Do nothing to the refuge hook. WP123's `RefugeDamageHooks.cs` already has
   `Reduce(IRefugeDefenseService?, in AttackInformation, float)`, which picks
   `IsVictimAgentMount ? VictimRiderAgentOrigin : VictimAgentOrigin`, an `internal` origin overload, and
   the party as `origin?.BattleCombatant as PartyBase`.
3. Replace the whole of `Main/Features/CombatMechanics/Models/TaomCombatMechanicsModel.cs` with this
   content (85 lines).

```csharp
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TAOM.Features.CareerSystem.Abilities;
using TAOM.Features.CareerSystem.Models;
using TAOM.Features.CombatMechanics.Hooks;
using TAOM.Features.Refuge;
using TAOM.Features.Refuge.Hooks;

namespace TAOM.Features.CombatMechanics.Models;

// CombatMechanics feature. Derives from the CareerSystem model so career passives ride along via
// inheritance: only ONE AgentApplyDamageModel can be registered. Thin boundary per gamemodels.md
// rule 4: each override is base plus one call per feature, in the order that matters; each feature
// keeps its extraction, and the hits it declines, in its own Hooks/ facade (CombatMechanicsHooks for
// the four combat services, RefugeDamageHooks for the refuge defence; #737).
public class TaomCombatMechanicsModel : TaomAgentApplyDamageModel
{
    private readonly CombatMechanicsHooks _combat;
    private readonly IRefugeDefenseService _refugeDefense;

    public TaomCombatMechanicsModel(
        ICareerAgentStatService careerAgentStatService,
        CombatMechanicsHooks combat,
        IRefugeDefenseService refugeDefense = null)
        : base(careerAgentStatService)
    {
        _combat = combat;
        _refugeDefense = refugeDefense;
    }

    // Refuge (#507): defenders of a ready refuge take reduced real-time damage. base runs the
    // career-passive reduction chain first (the parent's override), then the refuge factor rides
    // on top: one service, shared with the auto-resolve path, so the two cannot drift. The source
    // module reached this line with a Harmony postfix on the SAME method TAOM's chain overrides,
    // an accidental and untested ordering; the override makes the order explicit.
    public override float ApplyDamageReductions(in AttackInformation attackInformation, in AttackCollisionData collisionData, float baseDamage)
    {
        var result = base.ApplyDamageReductions(in attackInformation, in collisionData, baseDamage);

        return RefugeDamageHooks.Reduce(_refugeDefense, in attackInformation, result);
    }

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

    // Base = vanilla stagger threshold (which re-enters our CalculateStaggerThresholdDamage via the
    // registered model) + career shrug-off passives; then creature unstoppability.
    public override bool DecideAgentShrugOffBlow(Agent victimAgent, in AttackCollisionData collisionData, in Blow blow)
        => base.DecideAgentShrugOffBlow(victimAgent, in collisionData, in blow)
           || _combat.IsUnstoppable(victimAgent, in collisionData);

    public override float CalculateStaggerThresholdDamage(Agent defenderAgent, in Blow blow)
        => _combat.StaggerThreshold(defenderAgent, base.CalculateStaggerThresholdDamage(defenderAgent, in blow));

    // The charge rules own horse charges only: CombatMechanicsHooks.ChargeKnockdown declines every
    // other hit before it builds a context, so every other hit is base.
    public override bool DecideAgentKnockedDownByBlow(Agent attackerAgent, Agent victimAgent, in AttackCollisionData collisionData, WeaponComponentData attackerWeapon, in Blow blow)
        => _combat.ChargeKnockdown(attackerAgent, victimAgent, in collisionData, in blow)
           ?? base.DecideAgentKnockedDownByBlow(attackerAgent, victimAgent, in collisionData, attackerWeapon, in blow);

    public override void DecideMissileWeaponFlags(Agent attackerAgent, in MissionWeapon missileWeapon, ref WeaponFlags missileWeaponFlags)
    {
        // Base first: preserves the vanilla Javelin + Impale perk grant.
        base.DecideMissileWeaponFlags(attackerAgent, in missileWeapon, ref missileWeaponFlags);
        missileWeaponFlags = _combat.PenetrationFlags(in missileWeapon, missileWeaponFlags);
    }

    public override float CalculateShieldDamage(in AttackInformation attackInformation, float baseDamage)
        => _combat.ShieldDamage(in attackInformation, base.CalculateShieldDamage(in attackInformation, baseDamage));

    // Master off = exactly pre-feature behavior; the gate and its reason: CombatMechanicsHooks.HorseChargePenetration.
    public override float GetHorseChargePenetration()
        => _combat.HorseChargePenetration() ?? base.GetHorseChargePenetration();
}
```

4. `WT$ python ../wp127-scripts/wp127_edit.py b-code`. It gives the hooks the config provider (field,
   constructor parameter between the shield service and the settings provider, assignment) and makes
   `HorseChargePenetration()` return `_configProvider.GetConfig().ChargeKnockdown.HorseChargePenetration`
   when `ChargeKnockdownEnabled`, else null; re-cites the wield comment to v1.4.8 `:641-655`; registers
   `Hooks.CombatMechanicsHooks` as `Reuse.Transient` in `CombatMechanicsIoC`; turns the six resolve lines
   in `SubModule.cs` into one; points four comments at the hooks; and, only if WP123's caching comment is
   on the branch, renames the model in it.

Why each verdict is unchanged: the hooks' methods are the model's old bodies (context builders and
extractors moved unchanged; `BuildCrushThroughContext` still draws one `MBRandom.RandomFloat` per call,
D16); `ChargeKnockdown` returns null for `!IsHorseCharge`, so `?? base` reproduces the old early return,
NaN velocity included (`IsHorseCharge` is `ChargeVelocity > 0f`); `DecideAgentShrugOffBlow`'s `||` keeps
the old short-circuit; `GetHorseChargePenetration` reads the same config value when the toggle is on and
base otherwise.

**Verify**:
- `WT$ wc -l < Main/Features/CombatMechanics/Models/TaomCombatMechanicsModel.cs` prints `85` (or `84`
  if the file has no final newline; `wc` counts newlines).
- `WT$ grep -c "Reuse.Transient" Main/Features/CombatMechanics/CombatMechanicsIoC.cs` prints `1`;
  `grep -c "CombatMechanicsHooks>()" Main/SubModule.cs` prints `1`;
  `grep -c "ICrushThroughService>()" Main/SubModule.cs` prints `0`.
- Build exit 0; Filtered `"FullyQualifiedName~CombatMechanics|FullyQualifiedName~RefugeDamage"`:
  `Failed: 0`, and the counts include `CombatMechanicsModelInvariantsTests` 10 passed,
  `CombatMechanicsHooksTests` 17, `CombatMechanicsContainerWiringTests` 1, `RefugeDamageHooksTests` 8.
- Run Filtered `"FullyQualifiedName~Refuge|FullyQualifiedName~CombatMechanics"`. A test
  WP123 added that now fails because it read the model's removed inline code: rewrite it against
  `RefugeDamageHooks.Reduce(...)` with the same inputs and the same expected value, and name it in the
  hand-off. If it cannot be expressed that way: STOP.

#### Step B4: Docs, then commit B

`WT$ python ../wp127-scripts/wp127_edit.py b-docs`. It rewrites the architecture sentence and the
knockdown row in `docs/features/combat-mechanics.md`, its model, SubModule (dropping the stale `:913`)
and tests rows, adds the `CombatMechanicsHooks.cs` row, routes `docs/features/refuge.md`'s defence line
through `RefugeDamageHooks` (its Key Files row is WP123's, so the spec skips that insert), and, only if WP106's crush-roll sentence is on the
branch, re-cites `TaomCombatMechanicsModel.cs:177` in `.claude/rules/harmony-patches.md` to the roll's new
line in `CombatMechanicsHooks.cs`.

Then stage:
`WT$ git add -- Main/Features/CombatMechanics/Hooks/CombatMechanicsHooks.cs Main/Features/CombatMechanics/CombatMechanicsIoC.cs Main/Features/CombatMechanics/CrushThroughService.cs Main/Features/CombatMechanics/Domain/ChargeKnockdownContext.cs Main/Features/CombatMechanics/Domain/CrushThroughContext.cs Main/Features/CombatMechanics/Models/TaomCombatMechanicsModel.cs Main/Features/Refuge/RefugeDamageReduction.cs Main/SubModule.cs TAOM.Tests/Features/CombatMechanics/CombatMechanicsContainerWiringTests.cs TAOM.Tests/Features/CombatMechanics/CombatMechanicsHooksTests.cs TAOM.Tests/Features/CombatMechanics/CombatMechanicsModelInvariantsTests.cs TAOM.Tests/Infrastructure/CollisionDataFixture.cs docs/features/combat-mechanics.md docs/features/refuge.md`
and, only if `git status --porcelain` shows them modified,
`Main/Features/CombatMechanics/CombatMechanicsSettingsProvider.cs` and `.claude/rules/harmony-patches.md`.

**Verify**: `git diff --cached --name-status` lists the 14 paths above (four `A`, ten `M`; the two refuge
hook files stay as WP123 left them) plus at most those two; `git status --porcelain | grep -v '^[AM]  '` prints nothing; Doc gate `rc=0`; Docs command
`ai_dashes: 0`.
If you retargeted a WP123 test file, stage it explicitly and list it in the hand-off.

Write `msg-b.txt` (replace `230` with the N0 Step B0 measured):

```
refactor(combat): v2.0.28 - damage model glue moves to feature hooks

Nothing changes in game. TaomCombatMechanicsModel, the one
AgentApplyDamageModel slot every combat feature shares, had grown to
230 lines against the 150 that ADR-002 allows an entry point (#737).
Each override is now base plus one call per feature, in the order
that matters, and the model is 85 lines.

CombatMechanicsHooks (CombatMechanics/Hooks) holds the four combat
services with their context builders and extractors, moved unchanged;
RefugeDamageHooks (Refuge/Hooks) holds the refuge defender reduction.
The horse-charge gate moved into CombatMechanicsHooks.ChargeKnockdown,
which declines every other hit before it builds a context, so every
verdict is unchanged, a NaN charge velocity included. The charge
penetration still comes from combat_mechanics_config.json through the
config provider. SubModule resolves the hooks from the container.
RefugeDamageHooks itself landed earlier with the #741 fix.

Tests: a ceiling test keeps the model under 150 lines; source pins
check which agent, origin and base result reach each seam; the hooks,
the refuge reduction and the container registration have their own
tests.

Research: taom-src v1.4.8 AgentApplyDamageModel (the ten overridden
members), AttackCollisionData :20 :82 :102 :108 :138 (test fixture),
MBRandom.RandomFloat (falls back when there is no Game),
MissionWeapon.CurrentUsageItem (null for an empty weapon),
SandboxAgentApplyDamageModel.DecideCrushedThrough :641-655.
Not-tested: a live battle; behaviour is pinned by unit and source
tests only.
Ported-from: f7e9bd0d230c57922859fd84e5184c903da22113
```

Commit (next call): `WT$ git commit -F C:/Users/mikew/source/repos/taom-port/wp127-scripts/msg-b.txt`.
**Verify**: exit 0; `git status --porcelain` empty.

#### Part B done criteria

- [ ] Build exit 0; Test `Failed: 0`, `Total` = TA + 25 (invariants +7, hooks +17, wiring +1; WP123's
      refuge hook tests are already in T0; retargeted WP123 tests keep their count).
      Record it as **TB**. A different delta: STOP and report.
- [ ] `TaomCombatMechanicsModel.cs` is 85 lines; `git grep -n "SignatureStrike\|RaceAbilit\|CreatureSiege\|CreatureBandit" -- Main/Features/CombatMechanics Main/Features/Refuge TAOM.Tests/Features/CombatMechanics TAOM.Tests/Features/Refuge`
      prints nothing.
- [ ] `git grep -c "TaomCombatMechanicsModel.cs:177" -- .claude docs` prints nothing.

### Part C: the rule text (plan 021, non-ADR hunks)

Plan 021's hunks cannot be picked: on 1.4.x `think-before-coding.md`, `.ai/review-reference.md` and
`AGENTS.md` differ from the 1.5.x parent (1.5.x rewrote them in `c79a58521`), the lens file
`1-standards.md` is absent, and earlier WPs of this programme edit several of the same files. The `c`
spec carries the end state of `9c29d7324`, `d4e6273a1`, `e809f258c` and `6a2ce8cb3`'s lens hunk into
the 1.4.x text at checked anchors, with three adaptations: the lens wording goes into the Standards
agent prompt of the single `deep-review/SKILL.md` (checks 1 and 6); text that points at AGENTS.md
"Architecture" points at CLAUDE.md "Architecture (One-liner)", the 1.4.x home of that rule, whose line
the spec also amends (4 bytes longer); the severity line names the pinned game version instead of a
version number, as `d4e6273a1` does. This is a rules port, so its `.claude/` hunks are kept (the
harness exception of the per-port procedure).

#### Step C1: Apply, check, commit

`WT$ python ../wp127-scripts/wp127_edit.py c`, then
`WT$ git add -- .ai/review-reference.md .claude/agents/feature-builder.md .claude/rules/csharp-architecture.md .claude/rules/csharp-patterns.md .claude/rules/harmony-patches.md .claude/rules/think-before-coding.md .claude/skills/codex-verify/SKILL.md .claude/skills/deep-review/SKILL.md .claude/skills/improve/references/audit-playbook.md .claude/skills/new-feature/SKILL.md CLAUDE.md docs/ai-includes/architecture.md docs/ai-includes/decompiled-code-analysis.md docs/ai-includes/patterns.md docs/reference/engine/submodule-lifecycle-and-harmony.md`

**Verify**:
- The script printed `OK c: 36 ops, 15 files`; `git diff --cached --name-only | wc -l` prints `15`.
- `WT$ git grep -n "IHookInterface" -- .ai .claude CLAUDE.md` prints nothing;
  `grep -c "IHook" docs/reference/engine/submodule-lifecycle-and-harmony.md` prints `0`;
  `git grep -c "IHookInterface" -- docs/ai-includes` prints `docs/ai-includes/architecture.md:1` and
  `docs/ai-includes/patterns.md:1` (two code samples the 1.5.x end state also keeps).
- CLAUDE.md size under 46000 and Doc gate `rc=0`. If the doc gate now fails only on the CLAUDE.md size
  finding: `git checkout HEAD CLAUDE.md` (drop that one edit), remove `and CLAUDE.md's architecture
  one-liner` from the body below, and report it for WP143. Any other new doc-gate finding: STOP.
- Docs command: `ai_dashes: 0`; `dead_links` equal Step 0.6.

Write `msg-c.txt`:

```
docs(rules): v2.0.28 - align the architecture rules with the code

The architecture rules now describe the code this line already has,
so a review stops flagging a pattern the services rely on.

- A service may keep an engine call in a protected-virtual boundary
  seam that a test subclass overrides (RefugeService, CampService,
  SupplyOrderService and WardenService do); a seam that carries
  decision logic, or engine use outside a seam, is still a finding.
- A service gets an interface only when a test fakes it or a second
  class implements it; every adapter keeps one.
- A patch calls its service directly; an IOnXxx hook interface sits
  between them only when the patch needs a narrow seam or a test
  fake.

Updated: the review reference, the C# architecture, patterns, Harmony
and think-before-coding rules, the feature-builder agent, the
new-feature, codex-verify, improve and deep-review skills (the
standards checks the 1.5.x lens file carries), the architecture,
patterns and decompiled-code guides, the lifecycle engine doc and
CLAUDE.md's architecture one-liner. The ADR text these lines cite
(ADR-002, ADR-007, ADR-008) is the maintainer's commit on this branch.

Ported-from: 9c29d73242e05b33428970ae54bd439dba099b26
Ported-from: d4e6273a198f576cd91f938de6401cc043cb24cf
Ported-from: e809f258c0cc104254c27a27022de0eb0f46deac
Ported-from: 6a2ce8cb3df4b239571346b795183ddea66d8542
```

Commit (next call): `WT$ git commit -F C:/Users/mikew/source/repos/taom-port/wp127-scripts/msg-c.txt`.
**Verify**: exit 0; `git status --porcelain` empty.

#### Part C done criteria

- [ ] The commit exists; Build exit 0 and Test `Total` = TB (docs only).
- [ ] The three greps above hold; Doc gate `rc=0`.
- [ ] The branch must not merge until Part M's commits are on it (the rules cite ADR sections Part M adds).

### Step D: Hand-off (executor's last step)

1. Run Build and Test once more. Record `Total`, `Passed`, `Skipped` (**TD** = TB).
2. Run the Docs command; record the three numbers.
3. `WT$ git log --format=%s bannerlord-1.4.5..HEAD` lists the four subjects above, oldest last.
4. `WT$ git log --format=%B bannerlord-1.4.5..HEAD | python -c "import sys; t=sys.stdin.buffer.read().decode('utf-8'); print(sum(len(l) > 72 for l in t.splitlines()), t.count('\u2014') + t.count('\u2013'))"`
   prints `0 0`.
5. Final message: T0, TA, TB, TD; N0; whether the two conditional edits applied
   (WP123's provider comment, WP106's crush-roll citation) and whether the CLAUDE.md edit stayed; any
   service blob that differed in Step 0.3; any retargeted WP123 test; any conflict you resolved by hand
   in Step A1.3. Before that, write the two Part M message blocks, verbatim, to `msg-m1.txt` and
   `msg-m2.txt` in the helper folder. Recommend to the orchestrator: `/deep-review` on the branch (C# across many files and
   one single-owner file), then Part M by the maintainer. Never run `/review-codex` or `/codex-verify`
   (paid; maintainer only).

### Part M (maintainer, not the executor): the ADR text

The config-protection hook blocks agents from `docs/adrs/`; the decisions brief routes these edits to
the maintainer. Run in a terminal in the WP worktree after Part C.

**M1. Plan 021's ADR end state** (`1cdf8eb02`, `7d1b7a542`, `b22edd473`, the ADR hunks of `6a2ce8cb3`
and `ec5a19270`). The 1.4.x ADR-002, ADR-007 and ADR-008 equal the 1.5.x parent of `1cdf8eb02`, and
`git log 1cdf8eb02^..ec5a19270 -- docs/adrs/` lists only those five commits, so the 1.5.x files at
`ec5a19270` are exactly the 1.4.x files plus plan 021:

```bash
cd /c/Users/mikew/source/repos/taom-port/127 && git checkout ec5a19270 docs/adrs/002-thin-entry-points.md docs/adrs/007-adapter-pattern.md docs/adrs/008-testability-requirements.md && python ../wp127-scripts/wp127_edit.py m1 && git add -- docs/adrs/002-thin-entry-points.md docs/adrs/007-adapter-pattern.md docs/adrs/008-testability-requirements.md && git diff --cached --numstat
```

The `m1` spec makes the 1.5.x references honest on this line: the two audit paths become
`origin/bannerlord-1.5.x:plans/_audit/2026-09-23-opus/...` (neither file exists on 1.4.x), "the
2026-09-23 audit" becomes "the 2026-09-23 audit of the 1.5.x line", decisions 55 and 56 and plan 021
are labelled 1.5.x, and the plan 026 sentence notes this line carries plan 026 too. Exact old and new
strings are in the spec. **Verify**: `OK m1: 7 ops, 2 files`; numstat `17 6` (002), `37 3` (007),
`8 7` (008); `grep -c "origin/bannerlord-1.5.x:plans/_audit" docs/adrs/002-thin-entry-points.md docs/adrs/007-adapter-pattern.md`
prints `1` for each; the added lines carry no long dash (checked at planning).

Commit: `git commit -F ../wp127-scripts/msg-m1.txt` (the executor wrote the file in Step D, with this text):

```
docs(adr): v2.0.28 - record boundary seams, conditional interfaces

ADR-007 gains an exception: a service may keep an engine call in a
protected-virtual boundary seam when the seam's signature carries
ids, value types and TAOM types only, its body is one engine
operation, the service's interface stays engine-free, and a test
subclass overrides every seam the tests reach. A seam that breaks a
condition is still a finding. ADR-008 points its static-call rule and
checklist at that exception and drops a CI grep that could not tell a
seam from a violation. ADR-002 lets a service skip its interface
unless a test fakes it or a second class implements it, and accepts a
test subclass that overrides seams. CampaignTime leaves ADR-007's list
of sealed singletons; it is a struct (v1.4.8 CampaignTime.cs:11).

Ported-from: 1cdf8eb022a7ac7dd3ad7bf2150b081a3357661d
Ported-from: 7d1b7a5422becd542cc1eeecd3effcb9e64b3d45
Ported-from: b22edd4733caf4bc58b2df38f30ee4f61f7a723a
Ported-from: 6a2ce8cb3df4b239571346b795183ddea66d8542
Ported-from: ec5a192702c08dbbdde92194a5aec8e264455799
```

**M2. ADR-003 partial services** (`583585818`). The 1.4.x ADR-003 equals that commit's parent; the
README differs (no ADR-011/012 rows), so its row is edited in place:

```bash
cd /c/Users/mikew/source/repos/taom-port/127 && git checkout 583585818 docs/adrs/003-no-regions.md && python ../wp127-scripts/wp127_edit.py m2 && git add -- docs/adrs/003-no-regions.md docs/adrs/README.md && git diff --cached --numstat
```

The `m2` spec replaces the 1.5.x-only example (`FireAndTributeService`) with the one 1.4.x partial
service, `VolunteerRecruitmentService` (main file `Main/Features/TroopProgression/VolunteerRecruitmentService.cs`
holds the fields and the static constructor; 16 parts `RecruitmentPools/VolunteerRecruitmentService.<Culture>.cs`
declare no fields; its main file has no remarks, the one gap recorded), replaces the 1.5.x gap list,
and amends the README row. **Verify**: `OK m2: 4 ops, 2 files`; numstat `14 1` (003) and `1 1`
(README); `grep -c "FireAndTributeService\|HiddenHostsService" docs/adrs/003-no-regions.md` prints `0`.

Commit: `git commit -F ../wp127-scripts/msg-m2.txt` (the executor wrote the file in Step D, with this text):

```
docs(adrs): v2.0.28 - ADR-003 allows a service split by concern

A hand-written service may be split into partial files by concern
when its parts share state, the main file holds the fields,
constructor and lifecycle, each part is Type.Part.cs and is listed in
the main file's remarks, and the class keeps one responsibility. A
partial file is never a way past the entry-point size limit, and
regions stay forbidden. On this line VolunteerRecruitmentService
already splits its 16 culture pools this way; it does not list its
parts in its remarks yet, which the ADR records as a known gap.

Ported-from: 583585818824083484989e8360fd376b04052f76
```

**After M**: `python tools/lint_docs.py --summary` shows no new dead link; the integrator merges the
branch and runs the full build and test.

## Disposition (every commit in the pack)

| Commit | Subject | Group | Disposition |
|---|---|---|---|
| `032481cc6` | refactor(cache-rebuild): delete two unreachable scaffolds | plan-025 | **Ported** (A1, cherry-pick); CHANGELOG dropped |
| `76f22e111` | fix(cache-rebuild): review follow-ups for plan 025 | plan-025 | **Ported** (A1): absence test removal, feature doc, reflection-sites line (re-cited to v1.4.8, dated); review report, Codex prompt, RCA, REVIEW-LOG dropped; lessons left to WP142 |
| `97344cf59` | fix(cache-rebuild): convergence fixes for plan 025 | plan-025 | **Skipped**: only a review record, a lessons recurrence note and CHANGELOG (records not carried; lessons WP142) |
| `1cdf8eb02` | docs(adr): record boundary seams, conditional interfaces | plan-021 | **Ported** in M1 (end state, maintainer) |
| `9c29d7324` | docs(rules): align three architecture rules with the code | plan-021 | **Adapted** (C1): rules, review reference, agent, skills, guides; AGENTS.md hunk carried into CLAUDE.md's one-liner (no Architecture row on 1.4.x); lens hunk folded into `deep-review/SKILL.md`; CHANGELOG dropped |
| `7d1b7a542` | docs(adr): let seams call the engine, interface optional | plan-021 | **Ported** in M1 |
| `d4e6273a1` | fix(rules): review follow-ups for plan 021 | plan-021 | **Adapted** (C1): rule, skill, guide and lifecycle-doc hunks, version-neutral severity line kept; review report, RCA, REVIEW-LOG dropped; the two `lessons/misc.md` entries left to WP142 |
| `e809f258c` | fix(rules): convergence fixes for plan 021 | plan-021 | **Adapted** (C1): the `architecture.md` and `decompiled-code-analysis.md` hunks; records dropped |
| `b22edd473` | docs(adr): three ADR lines the plan 021 review found | plan-021 | **Ported** in M1 |
| `16e45bf8e` | docs(reviews): Codex prompt for plan 021 | plan-021 | **Skipped**: a review record, carried only with the review report, which is not carried |
| `6a2ce8cb3` | docs(adr): seam helpers, seam debt, convergence fixes | plan-021 | **Adapted**: ADR hunks in M1; lens hunk folded into `deep-review/SKILL.md` check 1 (C1); records dropped |
| `ec5a19270` | docs(reviews): close the plan 021 review records | plan-021 | **Adapted**: ADR-007 hunk in M1; records dropped |
| `9dcf1a4de` | docs(changelog): rewrap the plan 021 entry | plan-021 | **Skipped**: SKIP-BOOKKEEPING (CHANGELOG only; D1) |
| `a39575410` | refactor(seams): move decision logic out of nine seams | plan-026 | **Ported** (A2, cherry-pick) |
| `12b1c2003` | fix(seams): review follow-ups for plan 026 | plan-026 | **Ported** (A2, same commit): code, tests, `FortificationSearchTests` (summary adapted), two feature-doc hunks; review report, RCA, REVIEW-LOG dropped; three lessons left to WP142 |
| `aeb04bc48` | fix(seams): convergence fixes for plan 026 | plan-026 | **Skipped**: corrects only CHANGELOG, the review report and the RCA, none carried |
| `5685676e3` | docs(reviews): plan 026 Codex prompt and RED-step note | plan-026 | **Skipped**: review record only |
| `af6329eac` | docs(plans): plan 026 and its two cold reviews | plan-026 | **Skipped**: SKIP-BOOKKEEPING (1.5.x plan text) |
| `8c60d134f` | docs(plans): plan 026 merged | plan-026 | **Skipped**: SKIP-BOOKKEEPING (1.5.x plans index) |
| `f7e9bd0d2` | refactor(combat): damage model glue moves to feature hooks | issue-737 | **Adapted** (Part B, hand port): CombatMechanicsHooks (config-provider penetration), thin model calling the RefugeDamageHooks WP123 landed from `bb68eeca3`, IoC, SubModule, comments, tests, three doc tables; SignatureStrikeVerdicts, StrikeContextFactory, RaceAbilities test, elephant.md and signature-strikes.md hunks left out (1.5.x-only); lessons left to WP142; RCA not carried |
| `583585818` | docs(adrs): ADR-003 allows a service split by concern | adr-003 | **Adapted** in M2 (1.4.x example and gap list) |

No shared hunks are assigned to this WP. Related work owned elsewhere: `bb68eeca3` and `74e9172d4`
(WP123: the #741 mount branch and the cached settings reads; WP123 merges first and Step B0 checks it).

## Test plan

- New tests: `CombatMechanicsModelInvariantsTests` +7 (one line ceiling, six seam pins);
  `CombatMechanicsHooksTests` 17 (every seam with null agents and non-default collision and blow fields,
  the charge gate including a NaN velocity, the penetration toggle against the config value);
  `CombatMechanicsContainerWiringTests` 1 (DryIoc `Validate` of the registration, no campaign);
  WP123's 8 `RefugeDamageHooksTests` are already in T0; plan 026's 78 (moved decision logic; `FortificationSearchTests` 8 of them as a separate class);
  plan 025's retired-key load test.
- Removed: plan 025's 26 scaffold tests, the scaffold absence test (added and removed within A1) and one
  `ReflectionSiteBindingTests` row.
- Structural pattern: model new combat tests after `CombatMechanicsHooksTests` (substitute services,
  `CollisionDataFixture`); seam tests after `TestableRefugeService` in `RefugeServiceTests.cs`.
- Untestable here (the `Not-tested:` trailers): a live battle through the model, the seam bodies of
  plan 026, the distance-cache rebuild.
- Totals: T0, then TA = T0 + 52, TB = TA + 25, TD = TB. MSTest counts each `[DataRow]` as
  one test; the planning counts follow that rule.

## Done criteria (whole WP)

- [ ] Executor: four commits, subjects exactly as given, each `v2.0.28`, at most 72 characters; message
      check prints `0 0`; no `Co-Authored-By` or "Generated with" line.
- [ ] `git diff --name-only bannerlord-1.4.5..HEAD -- CHANGELOG.md docs/reviews plans Main/IoC.cs Main/TAOM.csproj TAOM.Tests/TAOM.Tests.csproj Directory.Build.props .claude/settings.json .claude/settings.local.json AGENTS.md`
      prints nothing; before Part M, `-- docs/adrs` prints nothing too.
- [ ] Build exit 0; Test `Failed: 0`; `Total` = T0 + 77.
- [ ] `ls Main/Features/EditorCacheRebuild/Caching TAOM.Tests/Features/EditorCacheRebuild/Caching 2>&1 | grep -c "No such file"` prints `2`.
- [ ] `wc -l < Main/Features/CombatMechanics/Models/TaomCombatMechanicsModel.cs` prints a number under 150.
- [ ] `git grep -n "IHookInterface" -- .ai .claude CLAUDE.md` prints nothing.
- [ ] Doc gate `rc=0`; Docs command `ai_dashes: 0` and no new dead link.
- [ ] Maintainer: the two Part M commits are on the branch before the integrator merges it.

## STOP conditions

Stop and report (do not improvise) if:

- Step 0.3 fails: WP105 or WP123 is not merged, a `RepoPaths` member is missing, the Caching folder is not 6
  files, or any file this plan creates (or an ADR section it adds) already exists.
- A pick conflicts in a non-bookkeeping path other than the three Step A1.3 names, or Step A2 conflicts
  in any `Main/`, `TAOM.Tests/` or feature-doc path.
- The helper script prints `STOP:` (an anchor moved or vanished). Report the line; do not edit the spec.
- Step B0 does not find WP123's form, or a WP123 test cannot be retargeted without changing its
  expected value.
- A test count differs from the step's expected delta, or a test that passed at T0 fails afterwards.
- The model ends at 150 lines or more, or the build needs a change outside the in-scope list (above all
  `Main/IoC.cs` or a csproj).
- An engine member behaves or is named differently from the "Engine facts" table: do not decompile and
  improvise; report it.
- A hook denies a commit, or `git status` shows another session's files in the worktree.
- You find yourself about to edit `docs/adrs/**`: that is Part M, the maintainer's.

## In-game checks owed (maintainer)

All in one campaign on the merged build; the log is the session's `taom_debug_*.log`, which `FileLogger`
writes under its relative `Logs` folder (`Main/Core/Logging/FileLogger.cs:26`).

1. **Damage model (Part B).** Start or load a campaign (a failed hooks resolve would fail the game start
   itself), then fight one field battle with cavalry charges and at least one creature (troll, warg or
   spider). Look for: charges still knock infantry down, a cave or hill troll still crushes through a
   block that is not a shield (both are in the shipped `monsterCrushMonsterIds`), nothing in
   the log names `CombatMechanicsHooks`, `RefugeDamageHooks` or `TaomCombatMechanicsModel` in an
   exception, no `[CrashReport]` capture. If a refuge is available, defend it once: its defenders still
   take visibly less damage.
2. **Supply Lines (A2).** Place one order paid to a lord and one paid to a settlement; let one fail or
   cancel it to trigger the refund. Look for: gold and refunded troops move as before; no
   `[SupplyLines] charge: ... unreachable` or `[SupplyLines] refund: ... unreachable` warning for a
   reachable payee.
3. **Refuge and warden (A2).** With a refuge holding a prisoner lord, make peace with his faction: he is
   released. Promote a warden from a troop: the new companion appears; no `[Refuge] promoted-warden
   rename failed` or `[Refuge] promotion unwind failed` line. Let a hostile party near a refuge: the
   raid picks the nearest hostile as before.
4. **Keep-outs (A2).** Try to make camp, and to found a refuge, next to a town or castle: both are
   refused as before.
5. Plans 025 and 021 and the ADRs need no in-game check (no runtime path changed).

## Maintenance notes

- **WP123 ordering (integrator).** WP123 merges before this branch is cut, so the two never conflict at
  `ApplyDamageReductions`. The end state: the model calls
  `RefugeDamageHooks.Reduce(_refugeDefense, in attackInformation, result)`, `RefugeDamageHooks` holds the
  `bb68eeca3` form, and the invariants pin carries that call.
- **WP130** can build its Custom Battle damage model on `CombatMechanicsHooks` (registered transient in
  `CombatMechanicsIoC`) instead of copying the extractors.
- **WP138** ports the deep-review lens files; `lenses/1-standards.md` must carry the check 1 and check 6
  wording this plan folded into the single `SKILL.md` (the lens hunks of `9c29d7324`, `d4e6273a1`,
  `6a2ce8cb3`).
- **WP142** owns the lessons this plan did not carry: plan 025's (`76f22e111`: adapters-taleworlds-api,
  misc, testing-qa), plan 021's two `misc.md` entries (`d4e6273a1`), plan 026's three (`12b1c2003`:
  gamemodels-services, misc, testing-qa) and the split's three (`f7e9bd0d2`).
- **WP143**'s `250e1b321` ADR amendments are written on top of the Part M text; it also trims CLAUDE.md
  (this plan grew it by 4 bytes, or not at all if the fallback in C1 fired).
- **WP144**'s refuge fix `63630e8a0` edits the plan 026 code; it applies on top of A2.
- Review focus for `/deep-review`: the hooks' penetration read (config provider, not settings), the
  `?? base` and `||` short-circuits that keep each verdict, the transient registration and the single
  resolve in `SubModule.cs`, and that the model still hands `RefugeDamageHooks` the whole `in attackInformation`.
- Deferred on purpose: plan 025's, 021's and 026's 1.5.x review records and RCAs (records of 1.5.x runs);
  the `RequiresGame` tags (D9); the AGENTS.md "Architecture" row (absent on 1.4.x).
