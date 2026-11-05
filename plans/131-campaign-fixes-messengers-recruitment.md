# Plan 131: Port the messenger, castle-recruitment, alignment-scoring and troop-weight fixes to 1.4.x

> **Executor instructions**: Follow this plan step by step. Run every verification command and
> confirm the expected result before moving to the next step. If anything in "STOP conditions"
> occurs, stop and report; do not improvise. Do not edit `plans/README.md`: every WP branch shares
> it, so the integrator records this plan's status and test counts when merging. Put the numbers
> each Part's hand-off step lists in your final message instead.
>
> The plan has two Parts. Each Part is independently landable and has its own done criteria. Run
> Part A first; Part B may run in the same session or a later one on the same worktree.
>
> **Drift check (run first, Step A0.2)**:
> `git diff --stat 1b00d1acd..HEAD -- Main/SubModule.cs Main/Composition Main/Features/TroopWeight Main/Features/CastleRecruitment Main/Features/AlignmentRecruitment Main/Features/Messengers Main/Features/TroopProgression/Models/TaomVolunteerModel.cs Main/Features/Enlistment/Hooks/EnlistmentDialogBehavior.cs Main/Features/Enlistment/Presentation/ServiceVocabulary.cs Main/Features/ReturnToArmy/Hooks/Patch87_ReturnToArmy.cs TAOM.Tests/Features/TroopWeight TAOM.Tests/Features/CastleRecruitment TAOM.Tests/Features/AlignmentRecruitment TAOM.Tests/Features/Messengers TAOM.Tests/Features/Enlistment TAOM.Tests/Features/CampaignHotPathSettingsProvidersTests.cs TAOM.Tests/Migration TAOM.Tests/Infrastructure docs/features/troop-weight-system.md docs/features/castle-recruitment.md docs/features/alignment-recruitment.md docs/features/messengers.md docs/features/enlistment.md docs/reference/harmony-patch-registry.md docs/reference/gamemodel-registry.md docs/reference/taleworlds-api-snapshot docs/modding/configs-balance.md docs/features/caravan-bandit-parity.md docs/features/tavern-mercenaries.md docs/reference/engine/campaign-tick-time-and-party-ai.md .claude/rules/gamemodels.md .claude/rules/gui-ui.md Main/_Module/ModuleData/taom_module_strings.xml Main/_Module/ModuleData/Languages`
> Earlier WPs of this programme are expected to have changed several of these (WP104 and WP105
> `Main/SubModule.cs` and `Main/Composition`, WP123 `CampaignHotPathSettingsProvidersTests.cs`,
> WP112 `MessengerCampaignBehavior.cs`, `MessengerCampaignBehaviorTests.cs` and
> `TroopWeightDisplayHook.cs`, many WPs the registry). Compare the live files against "Current
> state" and the Step A0.3 anchors; STOP only on a real mismatch (an anchor missing or duplicated).

## Status

- **Priority**: P1
- **Effort**: L (two Parts, nine commits, most of them cherry-picks with documentation conflicts)
- **Risk**: MED (changes AI recruiting decisions and three Harmony patch sets; edits the single-owner
  `Main/SubModule.cs` twice and the shared `Main/Composition/FeatureModules.cs` list twice)
- **Depends on**: WP105 (`plans/105-composition-root.md`: the feature-module runner, which itself
  requires WP104's `TryPatchCategory`, `Main/PatchCategoryIndex.cs` and
  `TAOM.Tests/Infrastructure/RepoPaths.cs`), WP123 (its port of `423d948d8` creates
  `TAOM.Tests/Features/CampaignHotPathSettingsProvidersTests.cs`)
- **Category**: bug (fixes to features that exist on both lines)
- **Wave / lane**: wave 4, lane D (campaign)
- **Executor**: laptop
- **Decisions applied**: D1 (no `CHANGELOG.md`; the commit body is the entry), D2 (ported 1.5.x
  modules arrive as `*Module.cs` plus one `FeatureModules.All` line and keep their 1.5.x patch
  numbers Patch114 and Patch117), D7 (no co-op features: the #920 stand-down is not ported), D8
  (`v2.0.28` subjects), D9 (no CI; every gate runs locally), D10 (ordinary session), D18 (lessons
  are not ported here), D19 (git only through the Bash tool), D20 (ModuleData commits only after the
  WP101 Armory baseline)
- **Planned at**: 1.4.x `origin/bannerlord-1.4.5` `1b00d1acd` and 1.5.x `origin/bannerlord-1.5.x`
  `e45f6df2c`, 2026-11-04. The 1.5.x ref moved to `ca1cba35f` during planning; none of the 13 newer
  commits touches a file in scope (checked).
- **Issue**: Port of #886, #887, #898, #918, #944 and #989 (1.5.x issues). #920 is not ported (D7).
  `c2449a306` carries no issue number: the orchestrator creates or names one before Part A lands.

## Why this matters

Four shared features carry live defects on the 1.4.x line that the 1.5.x line fixed in October:

- **Troop weight.** `TaomPartySizeModel.cs:74` applies the troop-weight size penalty to every party.
  A garrison's limit is deflated by its elite troops, so vanilla desertion removes a quarter of the
  "excess" every day while lords refill it to the unweighted limit (on 1.5.x a campaign run counted
  about 20,600 deserters in the Free block over 150 days, 19,600 in the four elven realms). Elite
  patrols sit over their reduced limit for good and crawl under the over-limit speed penalty.
- **Castle recruitment.** The Patch42 transpiler opens every castle as an AI recruiting stop, so lords
  ride to empty castles and leave with nothing, and an empty castle loses vanilla's normal score.
  With "AI Recruits From Castles" off, AI lords still empty castle notables on entry
  (`RecruitmentCampaignBehavior.OnBeforeSettlementEntered`), because the toggle only steers scoring.
- **Alignment recruitment.** AI lords score recruiting trips with vanilla's volunteer estimate, which
  ignores the Free/Evil block `TaomVolunteerModel` applies, so they ride to towns that give them
  nothing.
- **Messengers.** A messenger conversation lets the player join or call an army (the party snaps
  across the map), swear enlistment (a teleport to the commander), move troops and gear, recall a
  companion or open a board game at a distance; a throw while the conversation opens leaves state
  behind. The greyed Send Messenger button never shows its reason (Gauntlet never hovers a disabled
  widget), and the button names two brushes no module declares, so it draws as a plain box.

None of these is one of the E1 to E4 player-log symptoms; they are gameplay correctness fixes. Part A
lands troop weight, castle recruitment and alignment scoring; Part B lands the messenger fixes and a
brush gate (`PrefabExtensionBrushTests`) that WP140 builds on.

## Current state

### Executor environment

- Laptop; repo `C:\Users\mikew\source\repos\TAOM`; Bannerlord v1.4.8 (build 119303) at
  `C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord`
  (`BANNERLORD_GAME_DIR`). Never write into the game install.
- Base branch: local `bannerlord-1.4.5` after WP101 plus every WP merged before this one; WP104,
  WP105 and WP123 must be among them.
- Worktree: `C:\Users\mikew\source\repos\taom-port\131`, branch
  `port148/131-campaign-fixes-messengers-recruitment`. The folder is outside the session's working
  directory, so a `cd` does not persist between Bash calls. In this plan **`WT$`** means: start the
  command with `cd /c/Users/mikew/source/repos/taom-port/131 && `.
- Commit message files live in `C:\Users\mikew\source\repos\taom-port\scratch\131\` (outside the
  worktree, so they can never be staged). Write them with the Write tool; pass them to git as
  `C:/Users/mikew/source/repos/taom-port/scratch/131/<file>`.
- Python: `python` (if Git Bash cannot find it, `/c/Python314/python.exe`).
- Edit files with the Edit tool using absolute paths under
  `C:\Users\mikew\source\repos\taom-port\131\`.
- Line endings: `core.autocrlf=true` here, so checked-out files are CRLF (`git ls-files --eol` shows
  `i/lf w/crlf`), while a file written with `git show <sha>:<path> > <path>` is LF. Both are fine (git
  normalises on `git add`; an "LF will be replaced by CRLF" warning is expected). Never end a `grep`
  pattern with `$`: the CR before the newline defeats it.

### Commits this plan ports (all verified on `origin/bannerlord-1.5.x` this session)

| Step | 1.5.x commit | Subject | Method |
|---|---|---|---|
| A1 | `c2449a306` | fix(troopweight): garrisons and patrols pay no size penalty | `cherry-pick -n` |
| A2 | `f83a63e57` | feat(recruitment): AI castle recruiting gated on volunteers | `cherry-pick -n` |
| A3 | `0d3f2bc88` | fix(recruitment): AI castle toggle covers entry recruits | `cherry-pick -n`, #920 dropped |
| A4 | `93684c32c` | feat(recruitment): AI lords skip recruit trips alignment bars | `cherry-pick -n` |
| B1 | `6647f8f57` | fix(messengers): greyed Send Messenger button says why | `cherry-pick -n` (clean) |
| B2 | `2c7f7fae9` | fix(messengers): send-messenger button gets its button look | `cherry-pick -n` |
| B3 | `464cbd375` | refactor(messengers): one config check and a dead field gone | hand re-implementation (dead field only) |
| B4 | `06a1a1b71` (part) | feat(lords): Lord Courtesies (its messenger flag hunks only) | path-limited `git apply --3way` |
| B5 | `1dc0a8d7c` | fix(messengers): messengers no longer move armies or troops | `cherry-pick -n` |

Cherry-pick simulation this session (`git merge-tree --write-tree --name-only --no-messages
--merge-base=<sha>^ origin/bannerlord-1.4.5 <sha>`, so before WP104/105/123 and before the earlier
steps of this plan): `6647f8f57` clean; `c2449a306` conflicts only in two docs; the others conflict
in the paths each step lists. `06a1a1b71`'s seven flag paths merge cleanly (all of its conflicts are in
paths this plan does not take: Lord Courtesies adapters, settings, strings and docs).

### Files and roles (1.4.x, `origin/bannerlord-1.4.5`)

| File | Role here |
|---|---|
| `Main/Features/TroopWeight/TroopWeightService.cs` | `ApplyPartySizeWeightPenalty` (`:103`) deflates every party's limit; gains a scope early-out |
| `Main/Features/TroopWeight/Hooks/TroopWeightDisplayHook.cs` | `Frame` (`:71`) and `BuildLabel` (`:139`) render weighted frames; gain the same scope check |
| `Main/Features/CastleRecruitment/Hooks/*` | `CastleAiToggle`, `CastleAiTranspiler`, three Patch42 classes, `CastleRecruitmentBehavior`; identical to `f83a63e57^` |
| `Main/SubModule.cs` (single-owner) | `:511-520` Patch42 block; `:923` `new TaomVolunteerModel(...)` |
| `Main/Features/TroopProgression/Models/TaomVolunteerModel.cs` | `MaximumIndexHeroCanRecruitFromHero` (`:45-53`) applies the alignment rule; ctor takes `ICulturalFeatsService feats` (`:23`), not 1.5.x's `VolunteerProductionService` |
| `Main/Features/AlignmentRecruitment/*` | Identical to `93684c32c^`; `RecruitmentAlignmentSettingsProvider` reads `TaomSettings.Instance` per getter |
| `Main/Features/Messengers/MessengerCampaignBehavior.cs` | Boundary; `StartMessengerConversation` `:359-432` opens the encounter with no flag and no try; `OnEndMission` `:436`; `ResolveHero` `:573`; `SnapshotHero` `:579-597` |
| `Main/Features/Messengers/UI/MessengerEncyclopediaPrefabExtension.cs` | Button with `IsEnabled="@IsMessengerAvailable"` and the hint inside it (`:26-53`); `Brush="Popup.PartySelection.Confirm"` (`:36`), `Brush="Popup.PartySelection.Button.Text"` (`:42`) |
| `Main/Features/Messengers/Domain/HeroSnapshot.cs` | `IsInMapEvent` property (`:14`), ctor parameter (`:26`), assignment (`:37`); never read |
| `Main/Features/Enlistment/Hooks/EnlistmentDialogBehavior.cs` | `OfferIsTakeable(EnlistGateResult verdict)` (`:111`); `OfferIsClickable` (`:117-128`) calls `EnlistUnavailableReason(verdict)` |
| `Main/Features/Enlistment/Presentation/ServiceVocabulary.cs` | `EnlistUnavailableReason(EnlistGateResult verdict)` (`:106`), one parameter (1.5.x has a commander-name parameter from the unported `64691a56b`) |
| `Main/Composition/FeatureModules.cs` (from WP105) | `FeatureModules.All`, append-only list of modules |

Absent on 1.4.x and created by this plan: `TroopWeightPenaltyScope`, `CastleVolunteerScan`,
`CastleAiRecruitDispatcher`, `CastleRecruitmentEngineMirrorTests`, `AlignmentRecruitmentModule`,
`Patch114_AiVolunteerEstimate_Postfix`, `IMessengerConversationState`, `MessengerConversationState`,
`MessengerEntryPolicy`, `MessengerSessionRulesModule`, `MessengerSessionRulesPatch`,
`PrefabExtensionBrushTests`, `MessengerEncyclopediaPrefabTests` (`git grep` on `origin/bannerlord-1.4.5`
finds none). Absent and NOT created: `Main/Features/TokensOfEsteem`, `Main/Features/LordCourtesies`,
`Client/` (all 1.5.x-only).

Excerpts (1.4.x):

```csharp
// Main/SubModule.cs:515-520 (before WP104; WP104 turns the last line into TryPatchCategory(...)
// and WP105 adds FeatureModuleHooks.RunPhase(ApplyPhase.ProcessLoad, TryPatchCategory); after it)
        var castleRecruitmentSettings = IoC.Resolve<ICastleRecruitmentSettingsProvider>();
        CastleAiToggle.Initialize(castleRecruitmentSettings);
        Patch42_AiHourlyTick_Transpiler.Initialize(logger);
        Patch42_FillSettlements_Transpiler.Initialize(logger);
        Patch42_HourlyTickParty_Postfix.Initialize(castleRecruitmentSettings, logger);
        _harmony.PatchCategory("Patch42_CastleRecruitment");

// Main/SubModule.cs:923
        campaignStarter.AddModel(new TaomVolunteerModel(volunteerService, recruitmentService, volunteerContextAdapter, culturalFeats, recruitmentAlignment));

// Main/Features/Enlistment/Hooks/EnlistmentDialogBehavior.cs:111 and :126
    public static bool OfferIsTakeable(EnlistGateResult verdict) => verdict == EnlistGateResult.Ok;
        explanation = Presentation.ServiceVocabulary.EnlistUnavailableReason(verdict);
```

### Engine facts the ported code binds (installed v1.4.8, `pwsh -NoProfile -File tools/taom-src.ps1 path <Type>`, read this session)

| Member | Verified at (taom-src cache file) |
|---|---|
| `DefaultPartySizeLimitModel.GetPartyMemberSizeLimit(PartyBase, bool)` routes `IsGarrison` to `CalculateGarrisonPartySizeLimit` and `IsPatrolParty` to the private `CalculatePatrolPartySizeLimit`; patrol limit `10 + 5 * level` | `DefaultPartySizeLimitModel.cs:81-96`, `:99-115` |
| `MobileParty.IsPatrolParty`, `MobileParty.IsGarrison` (`public bool ... { get; private set; }`); `PartyBase.MobileParty` | `MobileParty.cs:1247`, `:1250`; `PartyBase.cs:122` |
| Desertion removes a quarter of the excess over `PartySizeLimit`; garrisons are ticked | `DefaultPartyDesertionModel.cs:54-65`; `DesertionCampaignBehavior.cs:21` |
| Over-limit morale and speed penalties | `DefaultPartyMoraleModel.cs:132` (`GetPartySizeMoraleEffect`); `DefaultPartySpeedCalculatingModel.cs:165-172` |
| `private void AiVisitSettlementBehavior.AiHourlyTick(MobileParty mobileParty, PartyThinkParams p)`; recruit gate `if (!settlement.IsCastle && item < 1f && mobileParty.GetAvailableWageBudget() > 0)`; second gate; estimate call; x1.4 castle bonus | `:137`; `:279` (first `IsCastle` in the method); `:302`; `:304`; `:327` |
| `private (int, float) GetApproximateVolunteersCanBeRecruitedDataFromSettlement(Hero hero, Settlement settlement)`: 4 slots own faction, 2 when `hero.MapFaction != settlement.MapFaction` | `AiVisitSettlementBehavior.cs:406-435` |
| `private static void FillSettlementsToVisitWithDistancesAsDays(MobileParty, List<SettlementNavigationData>)`; its `!settlement.IsCastle` targeting gate | `:681`; `:706` |
| `public void RecruitmentCampaignBehavior.HourlyTickParty(MobileParty)` checks `IsVillage`/`IsTown` only; `private void CheckRecruiting(MobileParty, Settlement)`; `private void RecruitVolunteersFromNotable(MobileParty, Settlement)` calls `VolunteerModel.MaximumIndexHeroCanRecruitFromHero`; `public void OnBeforeSettlementEntered(...)` calls `CheckRecruiting` | `:291-303`; `:414`; `:504`, `:522`; `:563`, `:601` |
| `public abstract int VolunteerModel.MaximumIndexHeroCanRecruitFromHero(Hero buyerHero, Hero sellerHero, int useValueAsRelation = -101)`; `MaximumIndexGarrisonCanRecruitFromHero(Settlement, Hero)`; namespace `TaleWorlds.CampaignSystem.ComponentInterfaces` | `VolunteerModel.cs:10`, `:12` |
| Garrisons recruit through `MaximumIndexGarrisonCanRecruitFromHero` (daily) | dump `C:\Decompiled_Bannerlord\_categories_v1.4.8\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\GarrisonRecruitmentCampaignBehavior.cs:48`, `:133`, `:155` |
| `public static IMbEvent<MobileParty> CampaignEvents.HourlyTickPartyEvent`; `TickEvent` | `CampaignEvents.cs:841`, `:847` |
| Hourly party and partial-hourly AI tickers run with `doParallel: false` (main thread) | `CampaignPeriodicEventManager.cs:205-260` |
| `AiPartyThinkBehavior`: `DefaultThinkingPeriodInHours = 6`, listens to `TickPartialHourlyAiEvent`, 1 or 3 hours for armies and rethinks, raises `AiHourlyTick` | `AiPartyThinkBehavior.cs:14`, `:18`, `:56-60`, `:73` |
| `MobilePartyHelper.GetCurrentSettlementOfMobilePartyForAICalculation(MobileParty)`; `Settlement.Notables` (`MBReadOnlyList<Hero>`), `IsCastle`, `IsUnderSiege`, `MapFaction`; `Hero.VolunteerTypes` (`CharacterObject[]` field), `IsAlive`, `Clan`, `MapFaction`, `CurrentSettlement`, `static MainHero` | `MobilePartyHelper.cs:266`; `Settlement.cs:267`, `:382`, `:458`, `:293`; `Hero.cs:47`, `:317`, `:499`, `:567`, `:742`, `:892` |
| Six conditions, all `private bool X()`: `LordConversationsCampaignBehavior.conversation_lord_join_army_on_condition`, `player_ask_to_join_players_army_on_condition`, `conversation_clan_member_manage_troops_on_condition`, `conversation_clan_member_manage_inventory_on_condition`; `CompanionRolesCampaignBehavior.companion_rejoin_after_emprisonment_role_on_condition`; `SandBox.CampaignBehaviors.BoardGameCampaignBehavior.conversation_lord_talk_game_on_condition` | `:2044`, `:2622`, `:3826`, `:3836` (lines registered `:798`, `:799`, `:818`, `:819`); `:495` (line `:73`); `:823` (line `:503`) |
| Join-army consequences call `Army.AddPartyToMergedParties`, which sets `AttachedTo = LeaderParty` | `LordConversationsCampaignBehavior.cs:1017-1031`; `Army.cs:843-845` |
| `EnterSettlementAction.ApplyForParty` attaches a party in an army it does not lead when the leader is in that settlement; `PlayerEncounter.Finish` leaves the settlement only when `AttachedTo == null` | `EnterSettlementAction.cs:71-75`; `PlayerEncounter.cs:1013`, `:1082-1084` |
| The `lord_tell_gathering` answers exist but no line outputs that token | `LordConversationsCampaignBehavior.cs:334-335` |
| `ConversationManager.GetSentenceOptions` runs each `RunCondition` | `ConversationManager.cs:515`, `:536` |
| A new `CampaignGameStarter` per campaign in `Campaign.OnInitialize` | `Campaign.cs:1366`, `:1383` |
| `Hero.FindFirst` scans `Campaign.Characters`; `Hero.Find` goes through `CampaignObjectManager`; dead heroes are unregistered | `Hero.cs:2130-2138`, `:1719`; `CampaignObjectManager.cs:477` |
| Gauntlet hit-testing skips disabled widgets and disabled children | dump `C:\Decompiled_Bannerlord\_categories_v1.4.8\UI\TaleWorlds.GauntletUI\TaleWorlds.GauntletUI\EventManager.cs:859`, `:870`, `:882`, `:897` |
| `ButtonWidget.RefreshState` passes its state to children only with `UpdateChildrenStates` | dump `C:\Decompiled_Bannerlord\_categories_v1.4.8\UI\TaleWorlds.GauntletUI\TaleWorlds.GauntletUI.BaseTypes\ButtonWidget.cs:111-153` (`:141`) |
| Vanilla governor button: enabled wrapper `Widget` holding a `HintWidget` beside the `ToggleButtonWidget` | installed `Modules\SandBox\GUI\Prefabs\TownManagement\TownManagement.xml:105-111` |
| `Popup.Button.Base` and `Popup.Button.Text` declared; neither `Popup.PartySelection.*` name is declared by any installed module | installed `Modules\Native\GUI\Brushes\Popup.xml:2`, `:195` |

On the laptop's install, every brush the five 1.4.x in-code PrefabExtensions name is declared by
TAOM's dependency modules or TAOM's own `GUI/Brushes` except the two messenger names (a read-only
emulation of the new gate run this session). So the new gate goes green on the laptop once B2 swaps
those two names.

### Conventions that bind this change

- **ADR-007 adapters**: services never touch sealed TaleWorlds types. The ported engine-touching code
  sits in boundaries: `Hooks/` classes, the GameModel, `MessengerCampaignBehavior`, and
  `TroopWeightService`, whose interface summary already documents it as engine-touching
  (`ITroopWeightService.cs`); `TroopWeightPenaltyScope` follows that existing exception. Pure logic
  (`CastleVolunteerScan`, `MessengerEntryPolicy`, `ICastleRecruitmentService.BlocksAiRecruitAtCastle`)
  takes no TaleWorlds type.
- **ADR-002** thin entry points (under 150 lines, delegate to services); **ADR-003** no `#region`;
  **ADR-004** no `[Obsolete]`; **ADR-005** no `#if DEBUG` outside IoC registration.
- **TDD**: ported commits bring their own tests; the new work in B3 and B4 is RED first, and B2 proves
  its gate RED with a one-file revert.
- **gamemodels.md rule 4**: a GameModel override stays a branch-free boundary; the castle rule joins
  the existing `? :` expression, no `if`.
- **D2 module rules** (`docs/features/composition-root.md` "How to port a 1.5.x feature module",
  from WP105): copy the module as is with its `internal const string` category; append one line at
  the END of `FeatureModules.All`; never read `IoC.Resolver`; never call `.PatchCategory(`; keep the
  1.5.x tests. STOP if a module sets `OwnsSaveData`, overrides `RegisterCampaignObjects`, declares
  `GameModels` or touches `Client/` (neither module here does: both only declare one category and
  initialise statics).
- **MCM json2 rule** (rename a persisted setting, never flip its default): no setting changes here.
- **`.claude/rules/harmony-patches.md`**: no `MovementOrder` postfix is added.
- **Per-port procedure** (decisions brief): bookkeeping hunks are dropped (`CHANGELOG.md`,
  `docs/reviews/lessons/*`, `docs/reference/feature-map.md`, `docs/INDEX.md`, `REVIEW-LOG.md`);
  feature docs and RCAs travel with their code; `.claude/rules` lines that document the ported
  mechanism are kept and rewritten for 1.4.x (here: one `gamemodels.md` row, one `gui-ui.md` line).

## Commands you will need

| Purpose | Command | Expected |
|---|---|---|
| Restore | `WT$ dotnet restore TAOM.sln` | exit 0 |
| Build | `WT$ dotnet build TAOM.sln -p:DisableModuleCopy=true -p:ModuleId=` | exit 0, 0 errors (the BHA0001 Patch88 warning is a known false positive) |
| Test | `WT$ dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId=` | `Failed: 0`; give the call a 600000 ms timeout |
| Filtered | `WT$ dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId= --filter "<expr>"` | as each step states |
| Snapshot check | `WT$ pwsh -NoProfile -File tools/snapshot_api_surface.ps1 -Check; echo "exit=$?"` | `exit=0` means the committed snapshot reproduces (needs a prior Build) |
| Snapshot write | `WT$ pwsh -NoProfile -File tools/snapshot_api_surface.ps1` | rewrites `docs/reference/taleworlds-api-snapshot/{gamemodel-bases,patch-targets}.md` |
| Data | `WT$ python tools/validate_moduledata.py` | no new ERROR vs the Step B0 baseline |
| Docs | `WT$ python tools/lint_docs.py --summary --dash-base $(git merge-base HEAD bannerlord-1.4.5)` | `dead_links` and `orphan_features` equal the Step A0 values; `ai_dashes: 0` |

Both `-p:` flags are mandatory on every build and test: `Main/TAOM.csproj:133-136` does not gate
the CopyModule target on `DisableModuleCopy`, so a build without `-p:ModuleId=` deploys into the game
install. Never `./build.ps1`. Never two builds at once in this worktree. Always Build before a
`--no-build` test run.

## Scope

**In scope** (the only files you create, modify or delete): exactly the paths in the expected staged
sets of Steps A1 to A4 and B1 to B5, plus `TAOM.Tests/Migration/TranspilerSiteBindingTests.cs` only
when W122's count is above 0. In short: named files under `Main/Features/{TroopWeight,
CastleRecruitment, AlignmentRecruitment, Messengers}`, `TaomVolunteerModel.cs`, the two Enlistment
files, the doc comment of `Patch87_ReturnToArmy.cs`, the 13 strings files (one row each), their tests, `CampaignHotPathSettingsProvidersTests.cs`
and `CoopAuthorityGateTests.cs` (one line each), `CastleRecruitmentEngineMirrorTests.cs`,
`ReflectionSiteBindingTests.cs`, `PrefabExtensionBrushTests.cs`, the docs the steps name, four new
RCAs, one row in `.claude/rules/gamemodels.md` and one line in `.claude/rules/gui-ui.md`.

- **Single-owner and shared files** (this WP is their only editor while it runs; the integrator
  merges): `Main/SubModule.cs` (Step A2: the Patch42 block; Step A3: the `TaomVolunteerModel`
  construction; nothing else) and `Main/Composition/FeatureModules.cs` (one appended line in A4 and
  one in B5).

**Out of scope** (do not touch): `Main/IoC.cs` (no change is needed: Messengers, CastleRecruitment
and AlignmentRecruitment stay hand-registered there, as on 1.5.x); `CHANGELOG.md` (D1);
`docs/reviews/lessons/*`, `docs/reference/feature-map.md`, `docs/INDEX.md`, `REVIEW-LOG.md`,
`CLAUDE.md`, `AGENTS.md`, `.ai/**` (WPs 142 to 144 sync them); `.claude/settings*.json`,
`Directory.Build.props`, `docs/adrs/**` (maintainer only); `plans/README.md` (integrator);
`docs/features/coop-interop.md` (D7); `.claude/rules/moduledata-validation.md` (1.4.x has no
GUI prefab row; WP140 adds the gate line); `docs/features/race-age-system.md`; every Tokens of
Esteem, Lord Courtesies, Lines of Descent and `Client/` path; `tools/translate_with_claude.py` (paid;
WP135 translates the new string); `tools/build_backlinks.py` (it rewrites footers repo-wide);
renaming or reformatting anything nearby.

## Git workflow

- Create the worktree in Step A0.1; work and commit only there, on
  `port148/131-campaign-fixes-messengers-recruitment`. Never push, open a PR, tag, or merge into
  `bannerlord-1.4.5`. Run git only through the Bash tool (D19).
- Per-port procedure: apply a 1.5.x commit with `git cherry-pick -n <sha>` (with `-n` git writes no
  `CHERRY_PICK_HEAD`, so the later `git commit -F` is an ordinary commit with you as author). Never let
  `cherry-pick`, `--continue`, `-C`, `-c` or `--amend` create a commit.
- Resolve a conflicted bookkeeping path with `git checkout --ours <path> && git add -- <path>`;
  revert a cleanly applied hunk you do not take with `git checkout HEAD <path>` (this form works
  whether or not the path is unmerged); remove a 1.5.x-only path that shows `DU` (absent here) with
  `git rm -f -q -- <path>`. Revert per file, never per directory. When a step says "`--ours`, then
  add these lines" but the path merged cleanly instead (earlier WPs can make a context match), do not
  reset it: check with `grep -c` whether the pick already added the lines and add them only if not.
- Stage with explicit `git add -- <paths>` (never `-A`, `-u` or `.`). Before each commit run
  `WT$ git diff --cached --name-status` and compare it with the step's expected staged set; any
  extra or missing path is a STOP. Then run `git commit -F <file>` in the NEXT sequential Bash call:
  never the same command, never a parallel sibling call.
- Never `--no-verify`. If a hook denies a commit, STOP and report the message verbatim.
- Commit message template: subject `<type>(<scope>): v2.0.28 - <description>` (at most 72
  characters, the 1.5.x type and scope kept); a body for 1.4.x readers wrapped at 72 with no long
  dashes; trailers `Research:`, `Not-tested:`, then one `Ported-from: <40-hex sha>` per included
  1.5.x commit. No `Co-Authored-By`, no "Generated with" line. Each step gives the exact message.

## Steps

### Part A: troop weight, castle recruitment, alignment scoring

#### Step A0: worktree, prerequisites, baseline

**A0.1 Worktree.**

```bash
cd /c/Users/mikew/source/repos/TAOM && git worktree add /c/Users/mikew/source/repos/taom-port/131 -b port148/131-campaign-fixes-messengers-recruitment bannerlord-1.4.5 && mkdir -p /c/Users/mikew/source/repos/taom-port/scratch/131
```

**Verify**: `WT$ git status --porcelain | wc -l` prints `0`; `WT$ git rev-parse --abbrev-ref HEAD`
prints `port148/131-campaign-fixes-messengers-recruitment`.

**A0.2 Drift check.** Run the command at the top of this plan. Expected: changes from earlier WPs
only. Continue to A0.3, which is the real gate.

**A0.3 Prerequisites and anchors.** Run each line; every expected value must match.

```bash
WT$ for f in Main/Composition/FeatureModules.cs Main/Composition/TaomFeatureModule.cs Main/PatchCategoryIndex.cs TAOM.Tests/Infrastructure/RepoPaths.cs TAOM.Tests/Features/CampaignHotPathSettingsProvidersTests.cs; do test -f "$f" && echo "OK $f" || echo "MISSING $f"; done
WT$ grep -c 'internal static readonly TaomFeatureModule\[\] All' Main/Composition/FeatureModules.cs
WT$ grep -c 'CodeRoots = { "Main" }' TAOM.Tests/Infrastructure/RepoPaths.cs
WT$ grep -c 'TryPatchCategory("Patch42_CastleRecruitment");' Main/SubModule.cs
WT$ grep -c 'FeatureModuleHooks.RunPhase(ApplyPhase.ProcessLoad, TryPatchCategory);' Main/SubModule.cs
WT$ grep -c 'Patch42_FillSettlements_Transpiler.Initialize(logger);' Main/SubModule.cs
WT$ grep -c 'campaignStarter.AddModel(new TaomVolunteerModel(volunteerService, recruitmentService, volunteerContextAdapter, culturalFeats, recruitmentAlignment));' Main/SubModule.cs
WT$ grep -c 'public void OnlyTheLazySettingsAccessor_ReadsTheMcmInstance' TAOM.Tests/Features/CampaignHotPathSettingsProvidersTests.cs
WT$ grep -c 'ICulturalFeatsService feats,' Main/Features/TroopProgression/Models/TaomVolunteerModel.cs
WT$ grep -c 'public static TextObject EnlistUnavailableReason(EnlistGateResult verdict)' Main/Features/Enlistment/Presentation/ServiceVocabulary.cs
WT$ grep -c 'public static bool OfferIsTakeable(EnlistGateResult verdict) => verdict == EnlistGateResult.Ok;' Main/Features/Enlistment/Hooks/EnlistmentDialogBehavior.cs
WT$ git grep -l -e TroopWeightPenaltyScope -e CastleVolunteerScan -e CastleAiRecruitDispatcher -e Patch114_ -e IMessengerConversationState -e MessengerSessionRulesPatch -e PrefabExtensionBrushTests -- Main TAOM.Tests | wc -l
WT$ test -f TAOM.Tests/Migration/TranspilerSiteBindingTests.cs && { echo PRESENT; grep -c -e 'Patch42_' -e 'CastleAiTranspiler' TAOM.Tests/Migration/TranspilerSiteBindingTests.cs; } || echo ABSENT
```

**Verify**: five `OK` lines; then `1`, `1`, `1`, `1`, `1`, `1`, `1`, `1`, `1`, `1`; then `0`. A
`MISSING` line or any other count is a STOP (a prerequisite WP is not merged, an enlistment WP
changed the shapes this plan resolves against, or part of this work already landed). Record the
last command's result as **W122**: `ABSENT`, `PRESENT` with `0`, or `PRESENT` with a count above 0
(WP122's transpiler-site gate holds Patch42 tests that Step A2 must remove).

**A0.4 Baseline.** Run Restore, Build, Test. **Verify**: build exit 0; `Failed: 0`. Record `Total`,
`Passed`, `Skipped` as **T0** (the WP101 baseline was 9154 total, 9151 passed, 3 skipped; later WPs
added tests, so T0 is higher). Any failure: STOP.

**A0.5 Snapshot and docs baselines.** Run Snapshot check; record **S0** = `OK` (exit 0) or `DRIFT`
(exit 1, the committed snapshot was already stale before this plan). Run Docs; record `dead_links`
and `orphan_features` as **L0**.

#### Step A1: port `c2449a306` (garrisons and patrols pay no size penalty)

```bash
WT$ git cherry-pick -n c2449a306c0f813fdebc641f9907b16a045a0b13; echo "exit=$?"; git status --porcelain
```

Expected: `exit=1`; `UU docs/features/troop-weight-system.md` and
`UU docs/reviews/lessons/gamemodels-services.md`; the six code and test paths staged. A conflict in
any other path: STOP.

Resolve:

1. `WT$ git checkout --ours docs/reviews/lessons/gamemodels-services.md && git add -- docs/reviews/lessons/gamemodels-services.md`
   (lessons are WP142's).
2. `docs/features/troop-weight-system.md`: in each conflict block keep the upper (ours) lines and add
   only these lines from the lower side, then delete every marker line:
   - the changelog line that starts `- 2026-10-16: the weight penalty exempts garrisons and patrol
     parties` as the first entry under `## Changelog` (on the base, this block is the only conflict);
   - the Key Files row, ONLY if `grep -c 'TroopWeightPenaltyScope.cs. | .PaysSizePenalty' docs/features/troop-weight-system.md`
     prints `0` (on the base it merges cleanly, so it is already there; adding it again duplicates it):
     ``| `Main/Features/TroopWeight/TroopWeightPenaltyScope.cs` | `PaysSizePenalty`: garrisons and patrol parties pay no size penalty; read by the penalty and by every capacity display (2026-10-16) |``
     directly below the `TroopWeightDisplay.cs` row.
   Do not take any Lindon (#749) or `ClanPartyItemWithPartyVM` line: those are 1.5.x content.
3. In the same file make two replacements (the paragraph under `### Scope: leader-run parties only`
   applied cleanly from the pick):
   - in the line that starts `every day (v1.5.5`, replace `v1.5.5` with `v1.4.8` (the desertion
     lines it cites are the same on v1.4.8, verified);
   - `[adopt-yotthani-2026-10-16.md](../reviews/adopt-yotthani-2026-10-16.md)` becomes the text
     `recorded on the 1.5.x line in docs/reviews/adopt-yotthani-2026-10-16.md`, with the path in
     backticks and no space before the `)` that follows (the review file is absent here; a link would
     be dead). Then `git add -- docs/features/troop-weight-system.md`.
4. `Main/Features/TroopWeight/TroopWeightPenaltyScope.cs` summary, two replacements, then `git add`:
   - `v1.5.5 <c>DefaultPartySizeLimitModel.GetPartyMemberSizeLimit</c>` becomes
     `v1.4.8 <c>DefaultPartySizeLimitModel.GetPartyMemberSizeLimit</c> (DefaultPartySizeLimitModel.cs:81-96)`;
   - `(2026-10-16, docs/reviews/adopt-yotthani-2026-10-16.md)` becomes
     `(2026-10-16, yotthani's campaign runs, recorded on the 1.5.x line)`.

**Verify**:

```bash
WT$ grep -c -e '<<<<<<<' -e '>>>>>>>' docs/features/troop-weight-system.md; grep -c -e 'v1.5.5' -e 'adopt-yotthani-2026-10-16.md](' docs/features/troop-weight-system.md Main/Features/TroopWeight/TroopWeightPenaltyScope.cs; grep -c 'TroopWeightPenaltyScope' docs/features/troop-weight-system.md
```

Expected: `0`; `...troop-weight-system.md:0` and `...TroopWeightPenaltyScope.cs:0`; a number of 4
or more. Then Build (exit 0) and Filtered
`--filter "FullyQualifiedName~TAOM.Tests.Features.TroopWeight"`: `Failed: 0`, and the output names
`PenaltyScope_ExemptsGarrisonsAndPatrols`,
`WeightPenalty_ExemptParty_ReturnsBeforeItCachesOrSubtracts` and
`DisplayFrames_ExemptParty_KeepVanillaText` when run with `--logger "console;verbosity=normal"`.

**Expected staged set** (7): `M docs/features/troop-weight-system.md`,
`M Main/Features/TroopWeight/Hooks/TroopWeightDisplayHook.cs`,
`M Main/Features/TroopWeight/ITroopWeightService.cs`, `M Main/Features/TroopWeight/TroopWeightDisplay.cs`,
`A Main/Features/TroopWeight/TroopWeightPenaltyScope.cs`, `M Main/Features/TroopWeight/TroopWeightService.cs`,
`M TAOM.Tests/Features/TroopWeight/DisplayFrameSourceTests.cs`.

Message file `msg-A1.txt`:

```
fix(troopweight): v2.0.28 - garrisons and patrols pay no size penalty

Garrisons and patrol parties no longer pay the troop-weight size
penalty. The penalty shrank a garrison's limit by the weight of its
elite troops; vanilla desertion then removed a quarter of the excess
each day and morale fell with it, while lords refilled the garrison
to its full, unweighted limit. A patrol is refilled at full size and
nothing trims it, so an elite patrol sat over its reduced limit for
good, under the over-limit speed penalty.

One predicate, TroopWeightPenaltyScope.PaysSizePenalty, now decides
for the penalty and for every capacity display, so the party and clan
screens show a garrison's or patrol's raw count against its real
limit. Lord parties and caravans are unchanged.

Research: v1.4.8 DefaultPartySizeLimitModel.cs:81-96 routes garrisons
Research: and patrols; DefaultPartyDesertionModel.cs:54-65 trims
Not-tested: an elite garrison over a month; an elite patrol's speed
Ported-from: c2449a306c0f813fdebc641f9907b16a045a0b13
```

Commit: `WT$ git commit -F C:/Users/mikew/source/repos/taom-port/scratch/131/msg-A1.txt` (next Bash
call). **Verify**: `WT$ git log -1 --format=%s` prints the subject.

#### Step A2: port `f83a63e57` (AI castle recruiting gated on volunteers)

```bash
WT$ git cherry-pick -n f83a63e57e4a03d956ccaf76e0274baf6993e226; echo "exit=$?"; git status --porcelain
```

Expected `exit=1` and these unmerged paths (from the simulation; after WP104 and WP105
`Main/SubModule.cs` may merge cleanly): `UU TAOM.Tests/Migration/ReflectionSiteBindingTests.cs`;
`TAOM.Tests/Migration/TranspilerSiteBindingTests.cs` as `DU` when W122 is `ABSENT`, else `UU`;
`UU docs/features/castle-recruitment.md`; `UU docs/reference/taleworlds-api-snapshot/patch-targets.md`;
`UU docs/reference/taleworlds-api-snapshot/reflection-sites.md`; `UU` for
`docs/reviews/lessons/{build-tooling-workflow,campaign-mechanics,harmony-il}.md`; possibly
`UU Main/SubModule.cs`. Staged cleanly: the nine `Main/Features/CastleRecruitment` changes (two `A`,
five `M`, two `D`), `A TAOM.Tests/Features/CastleRecruitment/CastleVolunteerScanTests.cs`,
`A TAOM.Tests/Migration/CastleRecruitmentEngineMirrorTests.cs`, `M docs/features/caravan-bandit-parity.md`,
`M docs/features/tavern-mercenaries.md`, `M docs/reference/engine/campaign-tick-time-and-party-ai.md`,
`M docs/reference/harmony-patch-registry.md`, `A docs/reviews/rca-castle-recruitment-seam-2026-10-18.md`.
An unmerged path not named here: STOP.

Resolve:

1. Lessons: `git checkout --ours <path> && git add -- <path>` for each of the three.
2. `WT$ git checkout HEAD docs/reference/taleworlds-api-snapshot/patch-targets.md` (whether it
   conflicted or not: the pick's extra Diplomacy row belongs to the unported `2410aec51`; rows are
   handled in item 10).
3. `WT$ git checkout --ours docs/reference/taleworlds-api-snapshot/reflection-sites.md` (if it
   merged cleanly and already holds the `CastleAiRecruitDispatcher.cs:34` row, skip to `git add`),
   then add this row as the LAST row of the gate table (the table that ends just above the first line
   starting `Status (`), then `git add` it:

```
| `TaleWorlds.CampaignSystem.CampaignBehaviors.RecruitmentCampaignBehavior` | `CheckRecruiting` | method (private) | `CastleAiRecruitDispatcher.cs:34` | CastleRecruitment (#886). Bound once per campaign to a closed delegate; an AI lord in a castle recruits hourly through vanilla's own logic (caravans never recruit in a castle). Missing: one warning per session launch and the hourly in-castle top-up stops; lords still recruit on entry through vanilla's OnBeforeSettlementEntered |
```

4. `WT$ git checkout --ours TAOM.Tests/Migration/ReflectionSiteBindingTests.cs` (if it merged
   cleanly, for example because WP109 added the MissionStartGuard rows the hunk anchors on, confirm
   `grep -c 'CastleAiRecruitDispatcher.cs:34'` prints `1` and skip to `git add`), then insert these
   four lines directly above the line `public void ReflectionSite_ResolvesAgainstInstalledEngine(`
   (after the last `[DataRow(...)]`, whatever earlier WPs added), then `git add` it:

```csharp
    // --- CastleRecruitment (#886): the private CheckRecruiting bound once per campaign to a closed delegate.
    // A miss logs one warning per session launch and the hourly in-castle top-up stops; lords still recruit
    // on entry through vanilla's OnBeforeSettlementEntered.
    [DataRow("TaleWorlds.CampaignSystem.CampaignBehaviors.RecruitmentCampaignBehavior", "RecruitmentCampaignBehavior", "CheckRecruiting", "Method", "CastleAiRecruitDispatcher.cs:34")]
```

5. `TranspilerSiteBindingTests.cs`: if W122 is `ABSENT`,
   `WT$ git rm -f -q -- TAOM.Tests/Migration/TranspilerSiteBindingTests.cs`. Otherwise
   `git checkout --ours` it; if W122's count was above 0, delete every test method whose body
   names `Patch42_` or `CastleAiTranspiler` (attribute lines through the closing brace) and the line
   `using TAOM.Features.CastleRecruitment.Hooks;` if nothing else in the file uses that namespace;
   then `git add` it. On 1.4.x the castle site check lives in the castle-owned mirror file (item 6),
   so the outcome does not depend on WP122.
6. `TAOM.Tests/Migration/CastleRecruitmentEngineMirrorTests.cs`: add `using NSubstitute;` and
   `using TAOM.Core.Logging;` directly BELOW `using TAOM.Features.CastleRecruitment.Hooks;` (not among
   the TaleWorlds usings: Step A3's pick adds a using there), and append this method at the END of the
   class, after the `CallsGetter` helper and before the class's closing brace (Step A3's pick inserts
   its test above `CallsGetter`). Do not edit the class summary. Then `git add` it.

```csharp
    [TestMethod]
    [TestCategory("RequiresGameIL")]
    [TestCategory("BindingVerification")]
    public void CastleRecruitment_AiHourlyTickGate_StillResolves_InInstalledEngine()
    {
        if (!_gameLoaded) Assert.Inconclusive("Game assemblies unavailable.");

        var target = Patch42_AiHourlyTick_Transpiler.TargetMethod();
        Assert.IsNotNull(target,
            "AiVisitSettlementBehavior.AiHourlyTick did not resolve. AI lords no longer score castles for recruitment.");

        var il = PatchProcessor.GetOriginalInstructions((MethodBase)target!).ToList();
        var logger = Substitute.For<IModLogger>();

        var rewritten = CastleAiTranspiler.SwapIsCastleGate(il, logger);

        logger.DidNotReceive().LogWarning(Arg.Any<string>());

        var toggle = AccessTools.Method(typeof(CastleAiToggle), nameof(CastleAiToggle.IsExcludedFromRecruitScoring));
        Assert.AreEqual(1, rewritten.Count(ci => ci.operand is MethodInfo mi && mi == toggle),
            "Expected exactly one IsCastle gate to be swapped for CastleAiToggle.IsExcludedFromRecruitScoring.");
    }
```

7. `Main/SubModule.cs` (single-owner): whether it merged cleanly or conflicted, the block after
   `Mission_Initialize_Patch.Initialize(logger);` must hold these lines in this order (resolve a
   conflict to it; WP105's runner call stays the last line; the first comment line may keep the
   1.5.x dash where this plan writes a colon), then `git add` it:

```csharp
        // Patch42_CastleRecruitment: castle notable recruitment. Targets AiVisitSettlementBehavior
        // (TaleWorlds.CampaignSystem, no View/Mission.cctor dependency, safe in OnSubModuleLoad). The
        // transpiler swaps the AI IsCastle gate to a runtime toggle; fail-safe. The in-castle recruit
        // is an event listener in CastleRecruitmentBehavior, not a patch.
        var castleRecruitmentSettings = IoC.Resolve<ICastleRecruitmentSettingsProvider>();
        CastleAiToggle.Initialize(castleRecruitmentSettings);
        Patch42_AiHourlyTick_Transpiler.Initialize(logger);
        TryPatchCategory("Patch42_CastleRecruitment");
        FeatureModuleHooks.RunPhase(ApplyPhase.ProcessLoad, TryPatchCategory);
```

8. `docs/features/castle-recruitment.md`: take the 1.5.x version, then scrub its 1.5.x-only facts
   with the script below (it asserts each edit happens exactly once and prints the counts of the
   strings that must be gone):

```bash
WT$ git show f83a63e57:docs/features/castle-recruitment.md > docs/features/castle-recruitment.md && python - A2 <<'PY'
import sys, pathlib
step = sys.argv[1]
p = pathlib.Path('docs/features/castle-recruitment.md')
t = p.read_bytes().decode('utf-8')
def sub(old, new=''):
    global t
    assert t.count(old) == 1, ('sub', old, t.count(old))
    t = t.replace(old, new)
def cut(start, end):
    global t
    assert t.count(start) == 1, ('cut', start, t.count(start))
    i = t.find(start); j = t.find(end, i)
    assert j >= 0, ('cut-end', end)
    t = t[:i] + t[j + len(end):]
def dropline(marker):
    global t
    ls = t.split('\n'); hits = [k for k, l in enumerate(ls) if marker in l]
    assert len(hits) == 1, ('dropline', marker, len(hits))
    del ls[hits[0]]; t = '\n'.join(ls)
cut(' Since #765 the fixed curve', '(war-chronicle.md).')
dropline('`IWarEffectService` (WarChronicle)')
cut(', the War Chronicle multiplier on the curve', "none for the player's clan)")
if step == 'A2':
    sub('`CastleRecruitmentServiceTests` (20)', '`CastleRecruitmentServiceTests` (16)')
    sub('CastleRecruitmentEngineMirrorTests` (3) pins', 'CastleRecruitmentEngineMirrorTests` pins')
else:
    sub('`CastleRecruitmentServiceTests` (25)', '`CastleRecruitmentServiceTests` (21)')
    sub('CastleRecruitmentEngineMirrorTests` (4) pins', 'CastleRecruitmentEngineMirrorTests` pins')
sub('`CastleRecruitmentConfigProviderTests` (9)', '`CastleRecruitmentConfigProviderTests` (8)')
sub(', valid values logging info')
sub('- `TranspilerSiteBindingTests.CastleRecruitment_AiHourlyTickGate', '- `CastleRecruitmentEngineMirrorTests.CastleRecruitment_AiHourlyTickGate')
sub('; `binding-gate.runsettings` turns Inconclusive into a failure')
cut(' Since #765 the curve is scaled', '`VolunteerRate` multiplier.')
sub('If `TranspilerSiteBindingTests` goes red', 'If `CastleRecruitmentEngineMirrorTests.CastleRecruitment_AiHourlyTickGate_StillResolves_InInstalledEngine` goes red')
dropline('- 2026-10-09: The castle slot curve goes through')
if step == 'A3':
    cut(' The hourly recruit stands down in a co-op session', 'Recruiting on entry is unaffected.')
    sub(", `TaomVolunteerModel`'s castle extraction and the dispatcher's co-op stand-down)", " and `TaomVolunteerModel`'s castle extraction)")
    cut(', then reads the co-op provider for a party in a castle', 'when BannerlordCoop is enabled)')
    sub(', its `IsUnderSiege` filter and the co-op stand-down are not pinned.', ' and its `IsUnderSiege` filter are not pinned.')
    sub(', siege filter and co-op stand-down are not pinned.', ' and siege filter are not pinned.')
    dropline('- **After a BannerlordCoop session ends**')
    cut(' (#920) The hourly castle recruit stands down', 'recruiting on entry is unaffected.')
p.write_bytes(t.encode('utf-8'))
for bad in ('#765', 'War Chronicle', 'TranspilerSiteBindingTests.', 'binding-gate', '#920', 'co-op stand-down', 'VolunteerProductionService', 'stands down'):
    print(step, repr(bad), t.count(bad))
print(step, 'scrub OK')
PY
```

   **Verify**: every printed count is `0` and the last line is `A2 scrub OK`. An `AssertionError`
   means the text drifted: STOP. This plan dry-ran the script against both 1.5.x blobs. Then
   `git add -- docs/features/castle-recruitment.md`. (The 1.4.x doc differs from the 1.5.x parent only
   in the War Chronicle #765 lines and two test counts, which the scrub restores; the backlinks footer
   is identical.)
9. Re-cite and adjust the cleanly applied files, then `git add` each:
   - `Main/Features/CastleRecruitment/Hooks/CastleAiToggle.cs`: `AiHourlyTick :269` becomes
     `AiHourlyTick :279`; `bonus at :317` becomes `bonus at :327` (v1.4.8 lines).
   - `Main/Features/CastleRecruitment/Hooks/CastleAiTranspiler.cs`: `line ~269 recruit gate` becomes
     `line ~279 recruit gate`; `line ~317 x1.4` becomes `line ~327 x1.4`.
   - `docs/reference/harmony-patch-registry.md` (it normally merges cleanly; if it is unmerged, keep
     the lower side of the conflict inside `## Patch42_CastleRecruitment` and the upper side of any
     other conflict): in the `## Patch42_CastleRecruitment` paragraph, the test class named just
     before `(the real` changes from `TranspilerSiteBindingTests` to
     `CastleRecruitmentEngineMirrorTests`.
   - `docs/reviews/rca-castle-recruitment-seam-2026-10-18.md`: insert after line 1 (the title) a blank
     line and:

```markdown
> **1.4.x port note (plans/131, 2026-11-04).** Ported to the 1.4.x line with `f83a63e57`; the
> addendum's `0d3f2bc88` follows in the same plan without its #920 co-op stand-down (decision D7).
> Engine lines below are the 1.5.x review's v1.5.5 citations. On the installed v1.4.8 the same facts
> sit at `AiVisitSettlementBehavior.cs:279` (recruit gate), `:327` (x1.4 castle bonus), `:406-435`
> (the estimate's 4 and 2 slots) and `:706` (the targeting gate the removed transpiler patched), and
> `RecruitmentCampaignBehavior.cs:291-303` (`HourlyTickParty` skips castles), `:414`
> (`CheckRecruiting`), `:504` and `:522` (`RecruitVolunteersFromNotable` asks the volunteer model).
> On 1.4.x the transpiler-site check lives in `CastleRecruitmentEngineMirrorTests`.
```

10. Build (exit 0). Then the snapshot rows. If **S0** is `OK`: run Snapshot write, then
    `WT$ git diff --stat -- docs/reference/taleworlds-api-snapshot/` must list only `patch-targets.md`,
    and `WT$ git diff -- docs/reference/taleworlds-api-snapshot/patch-targets.md | grep '^[-+]|'`
    must show exactly two removed rows, for `Patch42_FillSettlements_Transpiler` and
    `Patch42_HourlyTickParty_Postfix` (plus the changed `Patches:` header line). If anything else
    changed, or if **S0** is `DRIFT`: `git checkout HEAD docs/reference/taleworlds-api-snapshot/`,
    then delete just those two rows by hand and leave the header. Then `git add` the file.

**Verify**: Build exit 0; Filtered
`--filter "FullyQualifiedName~CastleRecruitment|FullyQualifiedName~ReflectionSiteBindingTests|FullyQualifiedName~CoopAuthorityGateTests|FullyQualifiedName~HarmonyPatchBindingTests|FullyQualifiedName~TAOM.Tests.Composition|FullyQualifiedName~PatchCategory"`:
`Failed: 0`; `CastleVolunteerScanTests` 8 passed and `CastleRecruitmentEngineMirrorTests` 4 passed
(none Inconclusive: `BANNERLORD_GAME_DIR` is set on the laptop). Also
`WT$ git grep -n -e 'Patch42_FillSettlements' -e 'Patch42_HourlyTickParty' -e 'IsCastleAndAiDisabled' -- Main TAOM.Tests`
prints nothing.

**Expected staged set**: the nine `Main/Features/CastleRecruitment` entries
(`M CastleRecruitmentConfig.cs`, `A CastleVolunteerScan.cs`, `A Hooks/CastleAiRecruitDispatcher.cs`,
`M Hooks/CastleAiToggle.cs`, `M Hooks/CastleAiTranspiler.cs`, `M Hooks/CastleRecruitmentBehavior.cs`,
`M Hooks/Patch42_AiHourlyTick_Transpiler.cs`, `D Hooks/Patch42_FillSettlements_Transpiler.cs`,
`D Hooks/Patch42_HourlyTickParty_Postfix.cs`), `M Main/SubModule.cs`,
`A TAOM.Tests/Features/CastleRecruitment/CastleVolunteerScanTests.cs`,
`A TAOM.Tests/Migration/CastleRecruitmentEngineMirrorTests.cs`,
`M TAOM.Tests/Migration/ReflectionSiteBindingTests.cs`, `M TAOM.Tests/Migration/TranspilerSiteBindingTests.cs`
(only when W122's count was above 0), `M docs/features/caravan-bandit-parity.md`,
`M docs/features/castle-recruitment.md`, `M docs/features/tavern-mercenaries.md`,
`M docs/reference/engine/campaign-tick-time-and-party-ai.md`, `M docs/reference/harmony-patch-registry.md`,
`M docs/reference/taleworlds-api-snapshot/patch-targets.md`,
`M docs/reference/taleworlds-api-snapshot/reflection-sites.md`,
`A docs/reviews/rca-castle-recruitment-seam-2026-10-18.md`.

Message file `msg-A2.txt`:

```
feat(recruitment): v2.0.28 - AI castle recruiting gated on volunteers

AI lords now treat a castle as a recruiting stop only when one of its
notables holds a volunteer in the slots vanilla's own estimate counts
(0 to 3). Before, every castle drew lords in: a lord could ride to an
empty castle and leave with nothing, and an empty castle lost
vanilla's normal score for other visits.

One transpiler on the AI visit scoring replaces three patches. Kingdom
lords far from home no longer target friendly foreign castles; lords
outside a kingdom keep them, as in vanilla. An AI lord inside a castle
now recruits each hour through an event listener that calls vanilla's
own recruit method, bound once per campaign, instead of a postfix.

New binding tests fail if an engine update breaks the patch site, the
private recruit method, or the vanilla facts the code relies on.

Research: v1.4.8 AiVisitSettlementBehavior.cs:279, :327, :406-435
Research: v1.4.8 RecruitmentCampaignBehavior.cs:291-303, :414
Not-tested: AI lords skipping empty castles; the hourly castle recruit
Ported-from: f83a63e57e4a03d956ccaf76e0274baf6993e226
```

Commit in the next Bash call; verify the subject as in A1.

#### Step A3: port `0d3f2bc88` (AI castle toggle covers entry recruits), without #920

```bash
WT$ git cherry-pick -n 0d3f2bc88e664e10d62589b114757eed2609f117; echo "exit=$?"; git status --porcelain
```

Expected `exit=1` with `UU` on `Main/Features/TroopProgression/Models/TaomVolunteerModel.cs`,
`Main/SubModule.cs`, `.claude/rules/gamemodels.md`, `docs/reference/gamemodel-registry.md`,
`docs/features/castle-recruitment.md`, `docs/reviews/lessons/campaign-mechanics.md`,
`docs/reviews/lessons/gamemodels-services.md`, and possibly on
`docs/reference/harmony-patch-registry.md`, `docs/features/coop-interop.md`,
`docs/reference/feature-map.md`, `TAOM.Tests/Migration/ReflectionSiteBindingTests.cs`,
`docs/reference/taleworlds-api-snapshot/reflection-sites.md` and the RCA. Clean: the two castle
service files, the dispatcher, the behavior, `CastleRecruitmentServiceTests.cs`, the mirror tests,
`docs/features/alignment-recruitment.md`. An unmerged path outside this list: STOP.

Resolve:

1. Drop #920 (D7): `WT$ git checkout HEAD Main/Features/CastleRecruitment/Hooks/CastleAiRecruitDispatcher.cs Main/Features/CastleRecruitment/Hooks/CastleRecruitmentBehavior.cs`.
   For `TAOM.Tests/Migration/ReflectionSiteBindingTests.cs`,
   `docs/reference/taleworlds-api-snapshot/reflection-sites.md`, `docs/features/coop-interop.md` and
   `docs/reference/feature-map.md`: `git checkout --ours <path> && git add -- <path>` if unmerged,
   `git checkout HEAD <path>` if cleanly changed (the pick only renumbers the dispatcher line, which
   does not move without #920, or edits co-op and bookkeeping rows).
2. Lessons: `--ours` and `git add`, both files.
3. `Main/Features/TroopProgression/Models/TaomVolunteerModel.cs`: `git checkout --ours` it, then:
   - add `using TAOM.Features.CastleRecruitment;` directly below `using TAOM.Features.AlignmentRecruitment;`;
   - add the field `    private readonly ICastleRecruitmentService _castleRecruitment;` below the
     `_recruitmentAlignment` field;
   - in the constructor change `        IRecruitmentAlignmentService recruitmentAlignment)` to
     `        IRecruitmentAlignmentService recruitmentAlignment,` followed by a new line
     `        ICastleRecruitmentService castleRecruitment)`, and add
     `        _castleRecruitment = castleRecruitment;` as the constructor's last statement (keep the
     1.4.x `ICulturalFeatsService feats` parameter and `_feats` field);
   - replace the `MaximumIndexHeroCanRecruitFromHero` doc comment and method (1.4.x lines 35-53, from
     `    /// <summary>` above it to the method's closing brace) with lines 38-59 of
     `git show 0d3f2bc88:Main/Features/TroopProgression/Models/TaomVolunteerModel.cs`, character for
     character (Step A4's pick edits that comment and needs it identical).
   Keep everything else, including `using TaleWorlds.Library;`. Then `git add` it.
4. `Main/SubModule.cs`: resolve to the 1.4.x line plus the new argument, then `git add`:

```csharp
        campaignStarter.AddModel(new TaomVolunteerModel(volunteerService, recruitmentService, volunteerContextAdapter, culturalFeats, recruitmentAlignment, IoC.Resolve<ICastleRecruitmentService>()));
```

5. `.claude/rules/gamemodels.md`: `--ours`, then change the row
   ``| `TaomVolunteerModel` | `DefaultVolunteerModel` | `TroopProgression` |`` to
   ``| `TaomVolunteerModel` | `DefaultVolunteerModel` | `TroopProgression` (+ `AlignmentRecruitment`, `CastleRecruitment`) |``;
   `git add`.
6. `docs/reference/gamemodel-registry.md`: `--ours`, then in the `TaomVolunteerModel` row replace
   ``returns -1 to block recruiting at an enemy-aligned settlement (AlignmentRecruitment feature) |``
   with
   ``returns -1 to block an AI lord recruiting from a castle notable while AI castle recruitment is off (`ICastleRecruitmentService.BlocksAiRecruitAtCastle`, #918), then to block recruiting at an enemy-aligned settlement (AlignmentRecruitment feature) |``;
   `git add`.
7. `docs/features/castle-recruitment.md`: run the item 8 script of Step A2 again with
   `git show 0d3f2bc88:...` instead of `f83a63e57` and the argument `A3` instead of `A2` (the `A3`
   branch also removes the #920 text). **Verify**: every count `0`, last line `A3 scrub OK`. `git add`.
8. `docs/reference/harmony-patch-registry.md`: if unmerged, keep the lower (theirs) `Patch42`
   paragraph and delete the upper one and the markers; if it merged cleanly, it already holds the
   new paragraph. Either way make two edits in that paragraph: the test class named just before
   `(the real` changes from `TranspilerSiteBindingTests` to `CastleRecruitmentEngineMirrorTests`;
   and `, siege filter and co-op stand-down are not pinned` (leading comma included) becomes
   ` and siege filter are not pinned`, so the text reads "the dispatcher's copied party guard and
   siege filter are not pinned". `git add`.
9. `TAOM.Tests/Migration/CastleRecruitmentEngineMirrorTests.cs` (merged cleanly): in the class summary
   replace `guard, the siege filter and the co-op stand-down are not pinned.` with
   `guard and the siege filter are not pinned.`; `git add`.
10. `docs/reviews/rca-castle-recruitment-seam-2026-10-18.md`: if unmerged, keep both sides (the port
    note at the top from A2 and the appended `## Addendum 2026-10-20` from the pick); `git add`.

**Verify**:

```bash
WT$ grep -c 'BlocksAiRecruitAtCastle(sellerInCastle: sellerSettlement?.IsCastle == true, buyerIsPlayer: isPlayer)' Main/Features/TroopProgression/Models/TaomVolunteerModel.cs; grep -c 'ICoopSessionProvider' Main/Features/CastleRecruitment/Hooks/CastleAiRecruitDispatcher.cs; grep -c 'CastleAiRecruitDispatcher.cs:34' TAOM.Tests/Migration/ReflectionSiteBindingTests.cs; git diff --cached --check | grep -c 'conflict marker'
```

Expected: `1`, `0`, `1`, `0`. Build exit 0; Filtered
`--filter "FullyQualifiedName~CastleRecruitment|FullyQualifiedName~AlignmentRecruitment|FullyQualifiedName~CoopAuthorityGateTests|FullyQualifiedName~GameModelOverrideBindingTests|FullyQualifiedName~SupplyCaravanBearingBindingTests"`:
`Failed: 0`; the five new `BlocksAiRecruitAtCastle_*` tests and
`RecruitVolunteersFromNotable_StillAsksTheVolunteerModel_InInstalledEngine` pass.

**Expected staged set** (12): `M .claude/rules/gamemodels.md`,
`M Main/Features/CastleRecruitment/CastleRecruitmentService.cs`,
`M Main/Features/CastleRecruitment/ICastleRecruitmentService.cs`,
`M Main/Features/TroopProgression/Models/TaomVolunteerModel.cs`, `M Main/SubModule.cs`,
`M TAOM.Tests/Features/CastleRecruitment/CastleRecruitmentServiceTests.cs`,
`M TAOM.Tests/Migration/CastleRecruitmentEngineMirrorTests.cs`, `M docs/features/alignment-recruitment.md`,
`M docs/features/castle-recruitment.md`, `M docs/reference/gamemodel-registry.md`,
`M docs/reference/harmony-patch-registry.md`, `M docs/reviews/rca-castle-recruitment-seam-2026-10-18.md`.

Message file `msg-A3.txt`:

```
fix(recruitment): v2.0.28 - AI castle toggle covers entry recruits

"AI Recruits From Castles" off now also stops AI lords taking castle
volunteers when they enter a castle, not only on their hourly recruit
(#918). Every AI lord recruit asks the volunteer model, so the castle
rule sits there, before the alignment rule. With the setting or
Castle Recruitment itself off, an AI lord recruits nothing from a
castle notable. The player's own party is never blocked; parties of
the player's clan led by a companion count as AI lords. Garrisons keep
their daily recruit, which uses a different engine call.

The 1.5.x change also paused the hourly castle recruit during a co-op
session (#920). That part is not ported: co-op is not supported on
this line.

Research: v1.4.8 RecruitmentCampaignBehavior.cs:504, :522, :563, :601
Research: v1.4.8 GarrisonRecruitmentCampaignBehavior uses its own index
Not-tested: entering a castle with the AI toggle off
Ported-from: 0d3f2bc88e664e10d62589b114757eed2609f117
```

Commit in the next Bash call; verify the subject.

#### Step A4: port `93684c32c` (AI lords skip recruit trips alignment bars)

```bash
WT$ git cherry-pick -n 93684c32cac592270911513816b84a81a51602fd; echo "exit=$?"; git status --porcelain
```

Expected `exit=1` with `UU` on `Main/Composition/FeatureModules.cs`,
`docs/modding/configs-balance.md`, `docs/reference/feature-map.md`,
`docs/reference/harmony-patch-registry.md`, `docs/reference/taleworlds-api-snapshot/patch-targets.md`,
`docs/reviews/lessons/harmony-il.md`, and possibly on
`TAOM.Tests/Features/CampaignHotPathSettingsProvidersTests.cs` and
`docs/features/castle-recruitment.md`. `TaomVolunteerModel.cs` and
`docs/features/alignment-recruitment.md` merge cleanly after A3; if either is unmerged, STOP (A3's
resolution drifted). Any other unmerged path: STOP.

Resolve:

1. `WT$ git checkout HEAD docs/reference/feature-map.md docs/reviews/lessons/harmony-il.md docs/reference/taleworlds-api-snapshot/patch-targets.md`
   (whether they conflicted or not; the snapshot row is handled in item 8).
2. `Main/Composition/FeatureModules.cs`: `--ours`, then append
   `        new Features.AlignmentRecruitment.AlignmentRecruitmentModule(),` as the LAST entry of
   `All` (no ordering constraint applies); `git add`.
3. `TAOM.Tests/Features/CampaignHotPathSettingsProvidersTests.cs`: if unmerged, `--ours`. Then make
   sure it holds `using TAOM.Features.AlignmentRecruitment;` once and, as the last `[DataRow]` above
   `public void OnlyTheLazySettingsAccessor_ReadsTheMcmInstance(`, exactly these two lines once:

```csharp
    // #887: read per foreign candidate per AI lord think (Patch114) and per notable per party-hour (TaomVolunteerModel).
    [DataRow(typeof(RecruitmentAlignmentSettingsProvider))]
```

   `git add`.
4. `docs/features/castle-recruitment.md`: if unmerged, `--ours`, then add below the
   `CastleRecruitmentIoC.cs` Key Files row:
   ``| `Main/Features/AlignmentRecruitment/Hooks/Patch114_AiVolunteerEstimate_Postfix.cs` | Zeroes the same vanilla AI recruit estimate for an alignment-blocked pair (see [alignment-recruitment.md](alignment-recruitment.md), #887) |``;
   `git add`.
5. `docs/modding/configs-balance.md`: `--ours`, then three cell replacements in the
   `recruitment_alignment` table (the 1.4.x key counts stay as they are: 24 keys):
   ``| `RecruitmentAlignmentConfigProvider.cs:36` |`` becomes ``| `RecruitmentAlignmentConfigProvider.cs:59` |``;
   ``| `alignment-recruitment.md:47` |`` becomes ``| `RecruitmentAlignmentConfigProvider.cs:61` |``;
   ``| `alignment-recruitment.md:48` |`` becomes ``| `RecruitmentAlignmentConfigProvider.cs:62` |``
   (v1.4.x lines `Enabled` :59, `ApplyToAi` :61, `ApplyToPlayer` :62, verified); `git add`.
6. `docs/reference/harmony-patch-registry.md`: `--ours`, then insert this section in numeric order:
   after the last `## PatchNN_...` section whose number is below 114 (on the base that is
   `## Patch88_LordPartyTemplate`; earlier WPs may have added higher numbers) and before the next
   `## ` heading; `git add`:

```markdown
## Patch114_AlignmentRecruitmentScoring

**Target:** `private (int, float) TaleWorlds.CampaignSystem.CampaignBehaviors.AiBehaviors.AiVisitSettlementBehavior.GetApproximateVolunteersCanBeRecruitedDataFromSettlement(Hero hero, Settlement settlement)` (v1.4.8, `AiVisitSettlementBehavior.cs:406-435`), one postfix class `Patch114_AiVolunteerEstimate_Postfix` in `Main/Features/AlignmentRecruitment/Hooks/`, category `Patch114_AlignmentRecruitmentScoring` (ProcessLoad, declared by `AlignmentRecruitmentModule`). Ported from the 1.5.x line (#887, `93684c32c`); the 1.5.x number is kept.

Vanilla counts filled volunteer slots (4 own faction, 2 foreign) with no alignment check, and the result feeds the AI lord's recruiting score. `TaomVolunteerModel` blocks the actual recruit across the Free/Evil divide, so a lord could plan a trip that yields nothing. The postfix returns when the service is null, the count is 0 or an argument is null, then skips a settlement of the lord's own map faction; otherwise it asks `IRecruitmentAlignmentService.IsRecruitmentBlocked` with the recruiter's `Clan.Kingdom.StringId`, the settlement's `MapFaction.StringId` and `hero == Hero.MainHero`, and sets `__result = (0, 0f)` on a block. A throw is caught and logged once.

**Callers and thread:** `AiHourlyTick` `:304`, once per candidate settlement that passes the gates at `:279` and `:302`, for every AI lord party; main thread (`CampaignPeriodicEventManager` builds its partial-hourly-AI tickers with `doParallel` false). Food and trade scoring for the same settlement is separate and unchanged.

**PatchShield.** A non-UI engine target, so a PatchShield pass (`Dependencies/SubModule.cs:234`, `:288`) wraps it with the shield finalizer, which swallows only MissingMethod, MissingField and TypeLoad and strips the failing owner's patches on the method. The postfix catches its own run-time faults.

**Co-op.** A void postfix on a pure estimate: no skip and no campaign state written, so `CoopVetoClassificationTests` needs no entry. Feature: [alignment-recruitment.md](../features/alignment-recruitment.md). **Tests:** `AiVolunteerEstimateBindingTests`, `AlignmentRecruitmentWiringTests`. **ACTIVE**, not yet checked in game.
```

7. Post-pick edits (each must match exactly once), then `git add` each file:
   - `docs/features/alignment-recruitment.md`: `(private, v1.5.5)` becomes
     ``(private, v1.4.8 `AiVisitSettlementBehavior.cs:406`)``; and
     `31 keys (kingdom ids, culture ids and minor clan ids; see` becomes
     `24 keys (kingdom ids and culture ids; see` (1.4.x `execution/alignment.json` has 24 keys).
   - `Main/Features/AlignmentRecruitment/Hooks/Patch114_AiVolunteerEstimate_Postfix.cs`:
     `/// (v1.5.5), which counts` becomes `/// (v1.4.8, AiVisitSettlementBehavior.cs:406), which counts`.
   - `docs/reviews/rca-alignment-scoring-2026-10-23.md`: insert after line 1 a blank line and:

```markdown
> **1.4.x port note (plans/131, 2026-11-04).** Ported with `93684c32c` as `AlignmentRecruitmentModule`
> plus one `FeatureModules.All` line (decision D2), keeping the 1.5.x number Patch114. The v1.5.5 facts
> below hold on the installed v1.4.8: the estimate is the private `(int, float)
> GetApproximateVolunteersCanBeRecruitedDataFromSettlement(Hero, Settlement)`
> (`AiVisitSettlementBehavior.cs:406`), called from `AiHourlyTick` at `:304`. Finding 1 (a number clash
> with a parallel 1.5.x worktree) has no 1.4.x counterpart.
```

8. Build (exit 0), then the snapshot row: if **S0** is `OK`, Snapshot write and confirm the only
   `patch-targets.md` change is one added row for `Patch114_AiVolunteerEstimate_Postfix` plus the
   `Patches:` header; otherwise (or if more changed) `git checkout HEAD` the snapshot folder and add
   this row by hand in sorted position (after the `TAOM.Features.AdvancedCombat` rows):

```
| `TAOM.Features.AlignmentRecruitment.Hooks.Patch114_AiVolunteerEstimate_Postfix` | `TaleWorlds.CampaignSystem.CampaignBehaviors.AiBehaviors.AiVisitSettlementBehavior.ValueTuple`2 GetApproximateVolunteersCanBeRecruitedDataFromSettlement(Hero hero, Settlement settlement)` |
```

   `git add` the file.

**Verify**: `WT$ grep -c 'AlignmentRecruitmentModule()' Main/Composition/FeatureModules.cs` prints
`1`; `WT$ grep -rn 'v1\.5' Main/Features/AlignmentRecruitment docs/features/alignment-recruitment.md`
prints nothing. Build exit 0; Filtered
`--filter "FullyQualifiedName~AlignmentRecruitment|FullyQualifiedName~CampaignHotPathSettingsProviders|FullyQualifiedName~TAOM.Tests.Composition|FullyQualifiedName~CastleRecruitment|FullyQualifiedName~HarmonyPatchBindingTests|FullyQualifiedName~PatchCategory"`:
`Failed: 0`; `AiVolunteerEstimateBindingTests` 3 passed, `AlignmentRecruitmentWiringTests` 6 passed.

**Expected staged set** (14): `M Main/Composition/FeatureModules.cs`,
`A Main/Features/AlignmentRecruitment/AlignmentRecruitmentModule.cs`,
`A Main/Features/AlignmentRecruitment/Hooks/Patch114_AiVolunteerEstimate_Postfix.cs`,
`M Main/Features/AlignmentRecruitment/RecruitmentAlignmentSettingsProvider.cs`,
`M Main/Features/TroopProgression/Models/TaomVolunteerModel.cs`,
`A TAOM.Tests/Features/AlignmentRecruitment/AiVolunteerEstimateBindingTests.cs`,
`A TAOM.Tests/Features/AlignmentRecruitment/AlignmentRecruitmentWiringTests.cs`,
`M TAOM.Tests/Features/CampaignHotPathSettingsProvidersTests.cs`, `M docs/features/alignment-recruitment.md`,
`M docs/features/castle-recruitment.md`, `M docs/modding/configs-balance.md`,
`M docs/reference/harmony-patch-registry.md`, `M docs/reference/taleworlds-api-snapshot/patch-targets.md`,
`A docs/reviews/rca-alignment-scoring-2026-10-23.md`.

Message file `msg-A4.txt`:

```
feat(recruitment): v2.0.28 - AI lords skip recruit trips alignment bars

AI lords no longer plan recruiting trips to towns and villages where
the Free and Evil recruitment rule would refuse them. When the AI
scores a settlement for recruiting, the volunteer estimate now counts
zero where the alignment rule blocks that lord, using the same rule
and settings as recruiting itself, including "Apply To AI Lords".
Lords still visit those places to buy food and trade.

The postfix arrives as AlignmentRecruitmentModule plus one line in
the feature-module list and keeps the 1.5.x number, Patch114. The
alignment settings are now read through a cached accessor, because the
AI reads them far more often.

Research: v1.4.8 AiVisitSettlementBehavior.cs:406 estimate, :304 call
Not-tested: AI trip choices across the Free and Evil divide
Ported-from: 93684c32cac592270911513816b84a81a51602fd
```

Commit in the next Bash call; verify the subject.

#### Step A5: Part A verification and hand-off

1. Run Build and Test. **Verify**: `Failed: 0`; `Skipped` equals T0's; `Total` is T0 plus 32 (3 in
   A1, 13 in A2, 6 in A3, 10 in A4; data rows count separately), minus any WP122 Patch42 tests A2
   removed. A different total: list the new test classes and STOP if any expected one is missing.
2. Run Docs. **Verify**: `dead_links` and `orphan_features` equal L0; `ai_dashes: 0`.
3. `WT$ git log --oneline bannerlord-1.4.5..HEAD` lists four commits whose subjects carry `v2.0.28`.
4. Report (Part A hand-off): T0; the final Total, Passed, Skipped; W122 and what A2 did with it; S0
   and whether the snapshot was regenerated or hand-edited; the four commit SHAs.

### Part B: messengers

#### Step B0: Part B baselines

If Part B starts in a new session, run Build and Test on the worktree as Part A left it and record
the totals as **TA**; if it follows Part A in the same session, TA is Step A5's total. Either way, run
Data once and record its ERROR count as
**V0** (Step B5 touches ModuleData strings; D20 applies: a validator ERROR that is not in V0 later is a
STOP). If V0 is above 0, say so in the hand-off: the ModuleData commit hook blocks a commit while the
validator reports an ERROR, so B5's commit will be denied, which is a STOP for the maintainer (D20).

#### Step B1: port `6647f8f57` (greyed Send Messenger button says why)

```bash
WT$ git cherry-pick -n 6647f8f577eaac144206df9d5be52f22cd8a253d; echo "exit=$?"; git status --porcelain
```

Expected `exit=0`; staged `M Main/Features/Messengers/UI/MessengerEncyclopediaMixin.cs`,
`M Main/Features/Messengers/UI/MessengerEncyclopediaPrefabExtension.cs`,
`A TAOM.Tests/Features/Messengers/MessengerEncyclopediaPrefabTests.cs`, `M docs/features/messengers.md`.
Any conflict: STOP.

Edit: in `docs/features/messengers.md`, in the `EnableMessengers` settings row the pick rewrote,
replace the long-dash placeholder in the Range column (the cell between `bool` and `true`) with `n/a`:
the row then starts ``| `EnableMessengers` | bool | n/a | true |``. It is the only long dash on a line
this plan writes, and `lint_docs.py` counts it. `git add` it.

**Verify**: Build exit 0; Filtered `--filter "FullyQualifiedName~TAOM.Tests.Features.Messengers"`:
`Failed: 0`, `MessengerEncyclopediaPrefabTests` 3 passed.

Message file `msg-B1.txt`:

```
fix(messengers): v2.0.28 - greyed Send Messenger button says why

The Send Messenger button on a hero's encyclopedia page now explains
itself when it is greyed out: hovering it shows why. Before, nothing
appeared, because the game never hovers a disabled widget and the hint
sat inside the button. The button now sits in an always-enabled
wrapper of the same size that holds the hint, the shape the vanilla
governor button uses. A failure while checking availability also gives
the "not available" reason.

Research: v1.4.8 EventManager.cs:859-897 skips disabled widgets
Research: SandBox TownManagement.xml:105-111 governor button shape
Not-tested: the hint on a greyed button in game
Ported-from: 6647f8f577eaac144206df9d5be52f22cd8a253d
```

Commit in the next Bash call; verify the subject.

#### Step B2: port `2c7f7fae9` (send-messenger button gets its button look) and its brush gate

```bash
WT$ git cherry-pick -n 2c7f7fae934b015857e8a3be6f30bde8ba122962; echo "exit=$?"; git status --porcelain
```

Expected `exit=1` with: `UU .claude/rules/moduledata-validation.md`;
`DU Client/Features/LinesOfDescent/UI/EncyclopediaHeroFamilyTreePrefab.cs`;
`UU TAOM.Tests/Features/Messengers/MessengerEncyclopediaPrefabTests.cs`;
`DU docs/features/lines-of-descent.md`; `UU docs/features/messengers.md`;
`UU docs/reviews/lessons/localization-ui.md`, `UU docs/reviews/lessons/testing-qa.md`. Staged cleanly:
`M .claude/rules/gui-ui.md`, `M Main/Features/Messengers/UI/MessengerEncyclopediaPrefabExtension.cs`
(git maps the 1.5.x `Client/` path onto the 1.4.x `Main/` path by rename detection),
`A TAOM.Tests/Infrastructure/PrefabExtensionBrushTests.cs`,
`A docs/reviews/rca-messengers-brushes-2026-10-24.md`.

If instead a `Client/Features/Messengers/UI/MessengerEncyclopediaPrefabExtension.cs` path appears,
`git rm -f -q --` it and hand-apply the same change to the `Main/` file: in the `ButtonWidget`,
replace the line `" Brush=\"Popup.PartySelection.Confirm\">" +` with the two lines
`" UpdateChildrenStates=\"true\"" +` and `" Brush=\"Popup.Button.Base\">" +`, and in the
`TextWidget` replace `" Brush=\"Popup.PartySelection.Button.Text\"" +` with
`" Brush=\"Popup.Button.Text\"" +`; then `git add` it.

Resolve:

1. `git rm -f -q -- Client/Features/LinesOfDescent/UI/EncyclopediaHeroFamilyTreePrefab.cs docs/features/lines-of-descent.md`.
2. `.claude/rules/moduledata-validation.md` and both lessons: `--ours` and `git add` (1.4.x has no
   GUI prefab row; WP140 adds the gate line there).
3. `TAOM.Tests/Features/Messengers/MessengerEncyclopediaPrefabTests.cs`: `--ours` (the B1 file), then
   in `TheButton_KeepsItsEnabledBindingAndClickCommand`, directly after the line
   `Assert.AreEqual("ExecuteSendMessenger", button.GetAttribute("Command.Click"));`, insert:

```csharp
        // ButtonWidget.RefreshState hands its state to the label only when UpdateChildrenStates is set (#989).
        Assert.AreEqual("true", button.GetAttribute("UpdateChildrenStates"), "the label must dim with the disabled button");
```

   `git add`.
4. `docs/features/messengers.md`: `--ours` (the B1 file), then add as the first entry under
   `## Changelog`:
   ``- 2026-10-24: the Send Messenger button now uses the declared brushes `Popup.Button.Base` and `Popup.Button.Text` (its two old names resolved to nothing), and its label dims when the button is disabled (#989).``
   `git add`.
5. `TAOM.Tests/Infrastructure/PrefabExtensionBrushTests.cs`: in the class summary replace
   `every C# source under Main/ and Client/ that declares` with
   `every C# source under Main/ (RepoPaths.CodeRoots) that declares`; `git add`.
6. `docs/reviews/rca-messengers-brushes-2026-10-24.md`: insert after line 1 a blank line and:

```markdown
> **1.4.x port note (plans/131, 2026-11-04).** Ported with `2c7f7fae9`. On 1.4.x the prefab extension
> lives in `Main/Features/Messengers/UI/` and the gate scans `Main/` only. The brush counts below are the
> 1.5.x desktop's. On the installed v1.4.8, `Popup.Button.Base` and `Popup.Button.Text` are declared in
> `Native/GUI/Brushes/Popup.xml` (lines 2 and 195) and neither old name is declared by any module. The
> follow-up on TAOM's own `GUI/PreFabs` brushes is WP140's.
```

   `git add`.

Prove the gate (RED, then GREEN) without losing the staged change:

```bash
WT$ git show HEAD:Main/Features/Messengers/UI/MessengerEncyclopediaPrefabExtension.cs > Main/Features/Messengers/UI/MessengerEncyclopediaPrefabExtension.cs
```

Build, then Filtered `--filter "FullyQualifiedName~PrefabExtensionBrushTests"`. **Verify (RED)**:
`EveryPrefabExtensionBrush_IsDeclaredByAModule` fails and its message names
`Popup.PartySelection.Confirm` and `Popup.PartySelection.Button.Text`. Then restore the staged
version from the index:

```bash
WT$ git checkout Main/Features/Messengers/UI/MessengerEncyclopediaPrefabExtension.cs && grep -c 'Popup.Button.Base' Main/Features/Messengers/UI/MessengerEncyclopediaPrefabExtension.cs
```

Expected `1`. Build again. **Verify (GREEN)**: Filtered
`--filter "FullyQualifiedName~PrefabExtensionBrushTests|FullyQualifiedName~TAOM.Tests.Features.Messengers"`:
`Failed: 0`; `PrefabExtensionBrushTests` 2 passed (Inconclusive means no install: STOP).

**Expected staged set** (6): `M .claude/rules/gui-ui.md`,
`M Main/Features/Messengers/UI/MessengerEncyclopediaPrefabExtension.cs`,
`M TAOM.Tests/Features/Messengers/MessengerEncyclopediaPrefabTests.cs`,
`A TAOM.Tests/Infrastructure/PrefabExtensionBrushTests.cs`, `M docs/features/messengers.md`,
`A docs/reviews/rca-messengers-brushes-2026-10-24.md`.

Message file `msg-B2.txt`:

```
fix(messengers): v2.0.28 - send-messenger button gets its button look

The Send Messenger button drew as a plain box: it named two brushes no
module defines, and the game falls back to a default style without a
warning (#989). It now uses the game's popup button brushes, with
their hover and pressed looks, and its label dims with the button when
no messenger can be sent.

A new test checks every brush a TAOM prefab extension names against
the brushes the engine loads for TAOM's module set, so a wrong brush
name fails a test instead of shipping unstyled.

Research: v1.4.8 Native/GUI/Brushes/Popup.xml:2 and :195 declare both
Research: v1.4.8 ButtonWidget.cs:141 needs UpdateChildrenStates
Not-tested: the button look in game
Ported-from: 2c7f7fae934b015857e8a3be6f30bde8ba122962
```

Commit in the next Bash call; verify the subject.

#### Step B3: drop the dead `HeroSnapshot.IsInMapEvent` (from `464cbd375`, hand re-implementation)

Only the dead field and the `ResolveHero` comment are taken; `464cbd375`'s config refactor, pricing
fields and docs belong to the unported distance pricing (`bbf2db053`).

**RED.** `WT$ sed -i 's/, isInMapEvent: false);/);/' TAOM.Tests/Features/Messengers/MessengerServiceTests.cs && grep -c 'isInMapEvent' TAOM.Tests/Features/Messengers/MessengerServiceTests.cs`
prints `0` (nine call sites had that exact form). Build. **Verify (RED)**: the build fails with
CS7036 errors naming `isInMapEvent` in `MessengerServiceTests.cs`.

**GREEN.**

1. `Main/Features/Messengers/Domain/HeroSnapshot.cs`: delete `    public bool IsInMapEvent { get; }`;
   change `        bool isInPlayerParty,` plus the next line `        bool isInMapEvent)` into the single
   line `        bool isInPlayerParty)`; delete `        IsInMapEvent = isInMapEvent;`.
2. `Main/Features/Messengers/MessengerCampaignBehavior.cs`, `SnapshotHero`: delete the line
   `        var inMapEvent = hero.PartyBelongedTo?.MapEvent != null;`; change
   `            isInPlayerParty: inPlayerParty,` plus `            isInMapEvent: inMapEvent);` into
   `            isInPlayerParty: inPlayerParty);`. Directly above `    private static Hero ResolveHero(string heroId)`
   insert:

```csharp
    // FindFirst scans Characters on purpose: Hero.Find misses a dead hero the engine unregistered (UnregisterDeadHero),
    // which would drop the messenger with no "lost" notice.
```

3. `Main/Features/Messengers/UI/MessengerEncyclopediaMixin.cs`, `SnapshotForMixin`: the same two
   edits as item 2's first two (delete the `inMapEvent` line; end the constructor call at
   `isInPlayerParty: inPlayerParty);`).
4. `docs/features/messengers.md`: `Boundary POCO (10 hero properties) for validation` becomes
   `Boundary POCO (9 hero properties) for validation`.

**Verify (GREEN)**: `WT$ git grep -n -e 'IsInMapEvent' -e 'isInMapEvent' -- Main/Features/Messengers TAOM.Tests/Features/Messengers`
prints nothing; Build exit 0; Filtered `--filter "FullyQualifiedName~TAOM.Tests.Features.Messengers"`:
`Failed: 0` with the same count as after B2.

Stage: `git add -- Main/Features/Messengers/Domain/HeroSnapshot.cs Main/Features/Messengers/MessengerCampaignBehavior.cs Main/Features/Messengers/UI/MessengerEncyclopediaMixin.cs TAOM.Tests/Features/Messengers/MessengerServiceTests.cs docs/features/messengers.md`.
**Expected staged set**: exactly those five, all `M`.

Message file `msg-B3.txt`:

```
refactor(messengers): v2.0.28 - drop the dead HeroSnapshot.IsInMapEvent

HeroSnapshot.IsInMapEvent was filled in two places and never read:
arrival waits for a battle to end by reading the engine directly. The
field and its two constructor arguments are gone. A comment now
records why ResolveHero scans with Hero.FindFirst: Hero.Find misses a
dead hero the engine has unregistered, which would drop the messenger
without the "messenger lost" notice. No change in game.

Research: v1.4.8 Hero.cs:2130-2138 FindFirst and Find; Hero.cs:1719
Ported-from: 464cbd37575cb1afde901dc9b77cd278fc679e45
```

Commit in the next Bash call; verify the subject.

#### Step B4: the open-conversation flag (the messenger hunks of `06a1a1b71`)

New work on 1.4.x (Lord Courtesies is not ported), so RED first.

**RED.**

```bash
WT$ git show 06a1a1b71:TAOM.Tests/Features/Messengers/MessengerConversationStateTests.cs > TAOM.Tests/Features/Messengers/MessengerConversationStateTests.cs
```

Build. **Verify (RED)**: the build fails with CS0246 for `MessengerConversationState`.

**GREEN.** Apply only the six other flag paths of `06a1a1b71`:

```bash
WT$ git diff 06a1a1b71^ 06a1a1b71 -- Main/Features/Messengers/IMessengerConversationState.cs Main/Features/Messengers/MessengerConversationState.cs Main/Features/Messengers/MessengerIoC.cs Main/Features/Messengers/MessengerCampaignBehavior.cs TAOM.Tests/Features/Messengers/MessengerCampaignBehaviorTests.cs TAOM.Tests/Features/CoopInterop/CoopAuthorityGateTests.cs | git apply --3way --index; echo "exit=$?"
```

Expected `exit=0` and no conflict (the simulation showed these paths merge cleanly; B3's edits are
in other regions). A conflict: STOP. Then `git add -- TAOM.Tests/Features/Messengers/MessengerConversationStateTests.cs`.

Docs, `docs/features/messengers.md` (then `git add`):

- below the `MessengerService` bullet in `### Solution Approach`, add:
  ``- **`MessengerConversationState`** (Reuse.Singleton, `IMessengerConversationState`): one in-memory flag, `IsOpen`, set just before the encounter starts, cleared in `OnEndMission`, on the null-mission recovery and at session launch. The conversation looks like a real visit to the engine (a real `PlayerEncounter`, and the main party inside the target's settlement), so other features read this flag to tell them apart.``
- below the `MessengerCampaignBehavior.cs` Key Files row, add:
  ``| [Main/Features/Messengers/IMessengerConversationState.cs](../../Main/Features/Messengers/IMessengerConversationState.cs) | Open-conversation flag contract |``
  and ``| [Main/Features/Messengers/MessengerConversationState.cs](../../Main/Features/Messengers/MessengerConversationState.cs) | The volatile flag behind it |``
- below the `MessengerEncyclopediaPrefabTests.cs` bullet under `## Tests`, add:
  ``- [TAOM.Tests/Features/Messengers/MessengerConversationStateTests.cs](../../TAOM.Tests/Features/Messengers/MessengerConversationStateTests.cs): the flag opens and closes, the behavior opens it before the encounter starts and closes it on every end path, and `MessengerIoC` registers it.``
- second entry under `## Changelog` (below the #989 line):
  ``- 2026-10-20: added `IMessengerConversationState`, the open-conversation flag (the messenger part of a 1.5.x change).``

**Verify (GREEN)**: Build exit 0; Filtered
`--filter "FullyQualifiedName~TAOM.Tests.Features.Messengers|FullyQualifiedName~CoopAuthorityGateTests"`:
`Failed: 0`; `MessengerConversationStateTests` 4 passed.

**Expected staged set** (8): `A Main/Features/Messengers/IMessengerConversationState.cs`,
`A Main/Features/Messengers/MessengerConversationState.cs`, `M Main/Features/Messengers/MessengerIoC.cs`,
`M Main/Features/Messengers/MessengerCampaignBehavior.cs`,
`A TAOM.Tests/Features/Messengers/MessengerConversationStateTests.cs`,
`M TAOM.Tests/Features/Messengers/MessengerCampaignBehaviorTests.cs`,
`M TAOM.Tests/Features/CoopInterop/CoopAuthorityGateTests.cs`, `M docs/features/messengers.md`.

Message file `msg-B4.txt`:

```
refactor(messengers): v2.0.28 - flag an open messenger conversation

A messenger conversation sets up a real encounter and, for a target in
a settlement, moves the party into that settlement, so no engine state
tells it apart from a visit. A small singleton flag now records it:
set just before the encounter starts, cleared when the mission ends,
when it fails to open and at each session launch. Nothing reads it
yet; the next commit's session rules do. No change in game.

Taken from the messenger part of a 1.5.x change whose feature, Lord
Courtesies, is not on this line.

Ported-from: 06a1a1b71b6a178e1762800077cf2f8381603f2d
```

Commit in the next Bash call; verify the subject.

#### Step B5: port `1dc0a8d7c` (messengers no longer move armies or troops)

```bash
WT$ git cherry-pick -n 1dc0a8d7c552cf56341cdaedface37da90ded9c1; echo "exit=$?"; git status --porcelain
```

Expected `exit=1` with: `UU Main/Composition/FeatureModules.cs`,
`UU Main/Features/Enlistment/Hooks/EnlistmentDialogBehavior.cs`,
`UU Main/Features/Enlistment/Presentation/ServiceVocabulary.cs`; `DU` for
`Main/Features/TokensOfEsteem/{GiftConversationFlow.cs, Hooks/GiftBehavior.cs, TokensOfEsteemModule.cs}`,
`TAOM.Tests/Features/TokensOfEsteem/{GiftBehaviorTests.cs, GiftConversationFlowTests.cs, TokensOfEsteemWiringTests.cs}`
and `docs/features/tokens-of-esteem.md`; `UU` for `Main/_Module/ModuleData/taom_module_strings.xml` and
the 12 language files; `UU TAOM.Tests/Features/Enlistment/EnlistOfferVisibilityTests.cs`; `UU` for
`docs/features/{coop-interop,enlistment,messengers}.md`, `docs/reference/feature-map.md`,
`docs/reference/harmony-patch-registry.md`, `docs/reviews/lessons/{adapters-taleworlds-api,harmony-il}.md`.
`Main/Features/Messengers/MessengerCampaignBehavior.cs` should merge cleanly after B4; if it is
unmerged, resolve each block to the lower (theirs) side, which is `1dc0a8d7c`'s shape. Staged cleanly
besides: `Main/Features/ReturnToArmy/Hooks/Patch87_ReturnToArmy.cs` (kept, see item 1), the three new
`Main/Features/Messengers` files, `EnlistmentContainerWiringTests.cs`, `EnlistmentMessengerWiringTests.cs`,
`ServiceVocabularyTests.cs`, the four new messenger test files and the RCA. Any other unmerged path:
STOP.

Resolve:

1. `Main/Features/ReturnToArmy/Hooks/Patch87_ReturnToArmy.cs`: keep the hunk as the pick staged it
   (doc comment only, no code). The comment it removes is false on v1.4.8: `LordConversationsCampaignBehavior.cs:336`
   (consequence `:3510-3514`) answers the token `lord_talk_player_ask_join_army`, only `:334` outputs
   that token, and `:334` needs `lord_tell_gathering`, which no line in the v1.4.8 dump outputs. So
   vanilla does not reach this state through the "follow us" line.
2. `git rm -f -q --` the seven Tokens of Esteem paths (1.5.x-only feature).
3. `docs/features/coop-interop.md` (D7), `docs/reference/feature-map.md` and both lessons: `--ours`,
   `git add`.
4. `Main/Composition/FeatureModules.cs`: `--ours`, append
   `        new Features.Messengers.MessengerSessionRulesModule(),` as the LAST entry of `All`; `git add`.
5. `Main/Features/Enlistment/Hooks/EnlistmentDialogBehavior.cs`: `--ours`, then:
   - add `using TAOM.Features.Messengers;` below `using TAOM.Features.Enlistment.Domain;`;
   - add the field `    private readonly IMessengerConversationState _messenger;` below `_coopSession`;
   - constructor: add the parameter `        IMessengerConversationState messenger,` between
     `ICoopSessionProvider coopSession,` and `IModLogger logger)`, and `        _messenger = messenger;`
     below `_coopSession = coopSession;`;
   - replace the `OfferIsTakeable` summary and method (1.4.x `:110-111`) with:

```csharp
    /// <summary>Only a clean Ok is takeable, and never through a messenger (#898: the oath would teleport the player).</summary>
    public static bool OfferIsTakeable(EnlistGateResult verdict, bool throughMessenger) =>
        verdict == EnlistGateResult.Ok && !throughMessenger;
```

   - replace the body of `OfferIsClickable` with:

```csharp
        var verdict = _gate.CanEnlistWith(PartnerId());
        var throughMessenger = _messenger.IsOpen;
        if (OfferIsTakeable(verdict, throughMessenger))
        {
            explanation = null;
            return true;
        }

        explanation = Presentation.ServiceVocabulary.EnlistUnavailableReason(verdict, throughMessenger);
        return false;
```

   Leave `ShowsOfferFor` as it is (1.4.x has no co-op parameter, D7). `git add`.
6. `Main/Features/Enlistment/Presentation/ServiceVocabulary.cs`: `--ours`, then:
   - replace the summary's first line `    /// Why the enlist offer is greyed out, as words for the disabled-option hint.`
     with the two lines
     `    /// Why the enlist offer is greyed out, as words for the disabled-option hint. A clean Ok verdict through a messenger`
     and `    /// (<paramref name="throughMessenger"/>) returns the messenger text before the switch, since the verdict itself passed.`;
   - change the signature to
     `    public static TextObject EnlistUnavailableReason(EnlistGateResult verdict, bool throughMessenger = false)`;
   - insert as the first two lines of the body, above `switch (verdict)`:

```csharp
        if (verdict == EnlistGateResult.Ok && throughMessenger)
            return new TextObject("{=taom_enlist_not_by_messenger}Swear service in person, not through a messenger.");
```

   `git add`.
7. `TAOM.Tests/Features/Enlistment/EnlistOfferVisibilityTests.cs`: `--ours`, then change both
   `EnlistmentDialogBehavior.OfferIsTakeable(verdict)` calls (1.4.x `:78`, `:153`) to
   `EnlistmentDialogBehavior.OfferIsTakeable(verdict, throughMessenger: false)`, and append before the
   class's closing brace:

```csharp

    // ---- a messenger conversation (#898) -------------------------------------------------

    [DataTestMethod]
    [DataRow(EnlistGateResult.Ok)]
    [DataRow(EnlistGateResult.AtWarWithYourKingdom)]
    public void OfferIsTakeable_ThroughAMessenger_IsNeverClickable(EnlistGateResult verdict)
    {
        Assert.IsFalse(EnlistmentDialogBehavior.OfferIsTakeable(verdict, throughMessenger: true));
    }
```

   `git add`.
8. `TAOM.Tests/Features/Enlistment/EnlistmentContainerWiringTests.cs` (merged cleanly; it now
   registers `IMessengerConversationState` and adds `DialogBehavior_Resolvable_WithTheMessengerConversationState`).
   `EnlistmentDialogGateService` needs `IPlayerContextAdapter` (`EnlistmentDialogGateService.cs:16`), which
   1.4.x registers in `SiegeDefenseIoC.cs:12`, outside the Enlistment module, so directly above the
   pick's `IMessengerConversationState` line add:

```csharp
        // IPlayerContextAdapter (SiegeDefenseIoC) is read by IEnlistmentDialogGateService, which the dialog behavior takes.
        container.RegisterInstance(Substitute.For<global::TAOM.Adapters.IPlayerContextAdapter>());
```

   `git add`. If the new test then reports another unresolved interface registered outside
   `EnlistmentIoC`, register a substitute for exactly that interface the same way; an unresolved
   `TAOM.Features.Enlistment` type is a STOP.
9. Strings. `git checkout --ours` and `git add --` `Main/_Module/ModuleData/taom_module_strings.xml`
   and the 12 language files, then run:

```bash
WT$ python - <<'PY'
import pathlib
key = 'taom_enlist_not_by_messenger'
en = pathlib.Path('Main/_Module/ModuleData/taom_module_strings.xml')
t = en.read_bytes().decode('utf-8')
nl = '\r\n' if '\r\n' in t else '\n'
assert t.count('id="%s"' % key) == 0, 'already registered'
i = t.rfind('</strings>')
assert i > 0
block = '\t<!-- Enlistment -->' + nl + '\t<string id="%s" text="{=%s}Swear service in person, not through a messenger." />' % (key, key) + nl
en.write_bytes((t[:i] + block + t[i:]).encode('utf-8'))
print('OK', en)
files = sorted(pathlib.Path('Main/_Module/ModuleData/Languages').glob('*/std_taom_module_strings_*.xml'))
print(len(files), 'language files')
for p in files:
    s = p.read_bytes().decode('utf-8')
    if 'id="%s"' % key in s:
        print('ALREADY', p); continue
    j = s.rfind('  </strings>')
    assert j > 0, p
    row = '    <string id="%s" text="Swear service in person, not through a messenger." />\r\n' % key
    p.write_bytes((s[:j] + row + s[j:]).encode('utf-8'))
    print('OK', p)
PY
```

   **Verify**: `OK` for the English file, `12 language files`, then 12 `OK` lines (any `ALREADY` or
   assertion: STOP). `git add --` the 13 files. The language rows stay English until WP135 runs the
   paid translation.
10. `docs/features/enlistment.md`: `--ours`, then insert after the paragraph that starts `**Co-op.**
    Every world-mutating handler is host-only.` (in `## Interactions with the rest of TAOM`) a blank
    line and:
    ``**Not through a messenger (#898).** `EnlistmentDialogBehavior.OfferIsTakeable(verdict, throughMessenger)` greys the offer while `IMessengerConversationState.IsOpen`, with `taom_enlist_not_by_messenger`. Swearing finishes the live encounter (`EnlistmentService`, `_encounter.Finish(true)`) and `MobilePartyAttachmentAdapter.ParkNear` sets the player's position to the commander's, which through a messenger would be a teleport. The line stays visible, so the player learns why. See [messengers.md](messengers.md).``
    `git add`.
11. `docs/features/messengers.md`: `--ours` (the B4 file), then:
    - in the `MessengerConversationState` bullet replace
      ``cleared in `OnEndMission`, on the null-mission recovery and at session launch.`` with
      ``cleared in `OnEndMission`, on the null-mission recovery, when opening throws (`AbortConversation`) and at session launch.``
      and replace `so other features read this flag to tell them apart.` with
      `so other features read this flag to tell them apart: Enlistment greys the oath and Patch117 hides six vanilla lines.`;
    - below the `MessengerConversationState.cs` Key Files row add:
      ``| [Main/Features/Messengers/MessengerEntryPolicy.cs](../../Main/Features/Messengers/MessengerEntryPolicy.cs) | Pure rule: when entering the town would attach the player to an army (#898) |``,
      ``| [Main/Features/Messengers/MessengerSessionRulesModule.cs](../../Main/Features/Messengers/MessengerSessionRulesModule.cs) | Applies Patch117 at GameInit |`` and
      ``| [Main/Features/Messengers/Hooks/MessengerSessionRulesPatch.cs](../../Main/Features/Messengers/Hooks/MessengerSessionRulesPatch.cs) | Patch117 postfix over six vanilla conditions |``;
    - insert the two sections in the fenced block below directly above `## Dependencies`;
    - below the `MessengerConversationStateTests.cs` bullet under `## Tests` add the four bullets in
      the second fenced block below;
    - as the second entry under `## Changelog` (directly below the 2026-10-24 #989 line) add:
      ``- 2026-10-23: a messenger conversation no longer offers the join-army lines, the board game, the clan troop and inventory exchange or the companion recall (Patch117); the enlist oath is greyed there; a player in an army whose leader is in the target town gets the field conversation; a throw while opening recovers and rethrows (#898).``;
    - under `## GitHub Issue`, above the `#109` entry, add
      ``- **Issue:** [#898 (messenger refinements)](https://github.com/haterade22/TAOM/issues/898)``,
      `- **Status:** Open` and a blank line.
    `git add`.

```markdown
## What a messenger conversation cannot do (#898)

The conversation is a real `PlayerEncounter` with the messenger, and for a target inside a settlement the main party is moved there, so the engine cannot tell it from a visit. These things would break or be wrong if offered, and each is closed:

- **"I want to join your army" and "I want you to join my army"** are hidden. Their consequences attach a party to an army (`Army.AddPartyToMergedParties` sets `AttachedTo`), which snaps it across the map. Patch117.
- **Board games** are hidden: the game starts after the window closes, when the messenger has already ended the mission. Patch117.
- **Enlisting** stays visible but greyed, with "Swear service in person, not through a messenger." Swearing finishes the encounter and parks the player at the commander, a teleport. `EnlistmentDialogBehavior.OfferIsTakeable(verdict, throughMessenger)`.
- **Clan member troop and inventory exchange** (`clan_member_manage_troops`, `clan_member_manage_inventory`) are hidden: they open the party screens for a person who is not there. Patch117.
- **Companion recall** (`companion_rejoin_after_emprisonment_role`) is hidden for the same reason. Patch117.
- **A main party in an army it does not lead, with that army's leader in the target's town**: entering the town would make `EnterSettlementAction.ApplyForParty` attach the party, and `PlayerEncounter.Finish` never leaves the town for an attached party. That state opens the field conversation instead (`MessengerEntryPolicy.WouldAttachToArmy`).

The flag is `IMessengerConversationState.IsOpen`: set when the arrived messenger's conversation starts, cleared when its mission ends, when the mission fails to open, when opening throws (`AbortConversation`, which also schedules the encounter cleanup), and at each session launch. Enlistment and Patch117 read it.

Wanderer hiring and barter stay on purpose: remote hiring is intended, and a trade settles by gold. The army "gathering" answers are unreachable on v1.4.8 (no line outputs `lord_tell_gathering`). Registry: [Patch117_MessengerSessionRules](../reference/harmony-patch-registry.md).

## In-game Checklist (#898)

1. **Army join, both ways.** Talk to a lord through a messenger: neither "I want to join your army" nor "I want you to join my army" shows.
2. **Enlist.** "I wish to enlist under your command." shows greyed with "Swear service in person, not through a messenger."
3. **Board game.** No board game line in a lord's hall conversation through a messenger.
4. **Clan member.** A clan member who leads a party: no "Let me inspect your troops." and no "Let me inspect your equipment."
5. **Companion recall.** A companion in a town: no recall line.
6. **In an army, not attached.** Join a lord's army, stay unattached, send a messenger to a lord in a town where the army's leader is: the field conversation opens and the party does not attach.
7. **The lord's hall.** A lord in a town and a lady in her home castle: the conversation opens in the hall, ends cleanly, and the party is back where it was.
8. **In person.** Every line above is still there when you talk to the same people face to face.
```

```markdown
- [TAOM.Tests/Features/Messengers/MessengerSessionRulesPatchTests.cs](../../TAOM.Tests/Features/Messengers/MessengerSessionRulesPatchTests.cs): the postfix filter, the six targets and the JIT check against the installed DLLs (#898).
- [TAOM.Tests/Features/Messengers/MessengerSessionRulesWiringTests.cs](../../TAOM.Tests/Features/Messengers/MessengerSessionRulesWiringTests.cs): one module line, Patch117 at GameInit, nothing registered by hand in `IoC.cs`.
- [TAOM.Tests/Features/Messengers/MessengerCampaignBehaviorSourceTests.cs](../../TAOM.Tests/Features/Messengers/MessengerCampaignBehaviorSourceTests.cs): source pins on the recovery when opening throws or returns no mission, and on the cleanup `AbortConversation` schedules.
- [TAOM.Tests/Features/Messengers/MessengerEntryPolicyTests.cs](../../TAOM.Tests/Features/Messengers/MessengerEntryPolicyTests.cs): the army-attach rule and the source pin on the settlement branch.
```

12. `docs/reference/harmony-patch-registry.md`: `--ours`, then insert this section in numeric order:
    after the `## Patch114_AlignmentRecruitmentScoring` section from A4 and any section numbered 115
    or 116, before the next `## ` heading; `git add`:

```markdown
## Patch117_MessengerSessionRules

**Targets:** six private instance conditions, all `bool` with no parameters (v1.4.8), resolved by name in `[HarmonyTargetMethods]`: `TaleWorlds.CampaignSystem.CampaignBehaviors.LordConversationsCampaignBehavior` `conversation_lord_join_army_on_condition` (`:2044`, line `main_option_discussions_1`), `player_ask_to_join_players_army_on_condition` (`:2622`, `main_option_discussions_2`), `conversation_clan_member_manage_troops_on_condition` (`:3826`, `clan_member_manage_troops`) and `conversation_clan_member_manage_inventory_on_condition` (`:3836`, `clan_member_manage_inventory`); `TaleWorlds.CampaignSystem.CampaignBehaviors.CompanionRolesCampaignBehavior.companion_rejoin_after_emprisonment_role_on_condition` (`:495`); `SandBox.CampaignBehaviors.BoardGameCampaignBehavior.conversation_lord_talk_game_on_condition` (`:823`, line `lord_player_play_game`). One postfix class `MessengerSessionRulesPatch` in `Main/Features/Messengers/Hooks/`, category `Patch117_MessengerSessionRules` (GameInit, applied by `MessengerSessionRulesModule`). Ported from the 1.5.x line (#898, `1dc0a8d7c`); the 1.5.x number is kept.

**Why:** while `IMessengerConversationState.IsOpen`, the join-army consequences call `Army.AddPartyToMergedParties` (`LordConversationsCampaignBehavior.cs:1020`, `:1030`), which sets `AttachedTo = LeaderParty` (`Army.cs:843-845`) and snaps the attached party across the map; the board game starts after the window closes, when the messenger has already ended the mission; the clan member troop and equipment inspections and the companion recall are physical hand-overs a messenger cannot do. The postfix is `__result = Filter(__result, state)`, which only ever turns true into false. It is a postfix so it runs after Enlistment's Patch66 prefix on the join-army condition (`LordConversationsConditionPatches.cs`). The enlist oath is a TAOM line and reads the flag itself (`EnlistmentDialogBehavior`). Wanderer hiring and barter stay on purpose. The "gathering" answers (`LordConversationsCampaignBehavior.cs:334-335`) consume a `lord_tell_gathering` token no line outputs, so they are unreachable.

**Callers and thread:** each condition runs from `ConversationManager.GetSentenceOptions` (`ConversationManager.cs:515`, `RunCondition` at `:536`) whenever the sentence options are rebuilt, on the main thread; the postfix reads one volatile bool.

**PatchShield.** Non-UI engine targets: a PatchShield pass that runs after the GameInit category applies (`Dependencies/SubModule.cs:234`, `:288`) wraps them; the finalizer swallows only MissingMethod, MissingField and TypeLoad.

**Co-op.** A postfix on local conversation predicates that skips nothing and writes no campaign state, so `CoopVetoClassificationTests` needs no entry. Feature: [messengers.md](../features/messengers.md). **Tests:** `MessengerSessionRulesPatchTests`, `MessengerSessionRulesWiringTests`. **ACTIVE**, not yet checked in game.
```

13. Re-cite `Main/Features/Messengers/MessengerCampaignBehavior.cs` (each replacement exactly once),
    then `git add`:
    `Every campaign gets a new starter in v1.5.5` becomes `Every campaign gets a new starter in v1.4.8`;
    `// (Campaign.cs:1402-1403), so the gate` becomes `// (Campaign.OnInitialize, Campaign.cs:1383), so the gate`;
    `(v1.5.5 :71-75)` becomes `(v1.4.8 :71-75)`.
14. `docs/reviews/rca-messenger-session-rules-2026-10-23.md`: insert after line 1 a blank line and:

```markdown
> **1.4.x port note (plans/131, 2026-11-04).** Ported with `1dc0a8d7c` as `MessengerSessionRulesModule`
> plus one `FeatureModules.All` line (decision D2), keeping the 1.5.x number Patch117. The open-conversation
> flag came from the messenger hunks of `06a1a1b71` (Lord Courtesies, not ported). Tokens of Esteem and
> Lord Courtesies do not exist on 1.4.x, so finding 3 and the troop-gift lines have no 1.4.x counterpart.
> The six targets, `EnterSettlementAction.ApplyForParty` (`:71-75`) and `PlayerEncounter.Finish`
> (`:1082-1084`) were re-read on the installed v1.4.8.
```

    `git add`.
15. Build (exit 0), then the snapshot row: if **S0** is `OK`, Snapshot write and confirm the only
    `patch-targets.md` change is one added row for `TAOM.Features.Messengers.Hooks.MessengerSessionRulesPatch`
    (the generator records the first `TargetMethods` target) plus the `Patches:` header; otherwise
    restore the folder from HEAD and add by hand, in sorted position:

```
| `TAOM.Features.Messengers.Hooks.MessengerSessionRulesPatch` | `TaleWorlds.CampaignSystem.CampaignBehaviors.LordConversationsCampaignBehavior.Boolean conversation_lord_join_army_on_condition()` |
```

    `git add` the file.

**Verify**:

```bash
WT$ git grep -n -e 'v1\.5\.5' -e 'TokensOfEsteem' -e 'Lord Courtesies' -- Main/Features/Messengers Main/Features/Enlistment docs/features/messengers.md docs/features/enlistment.md; grep -c 'MessengerSessionRulesModule()' Main/Composition/FeatureModules.cs; grep -c 'Patch117\|MessengerSessionRules' Main/IoC.cs; grep -c 'follow us' Main/Features/ReturnToArmy/Hooks/Patch87_ReturnToArmy.cs
```

Expected: no grep output; `1`; `0`; `0`. Build exit 0. Filtered
`--filter "FullyQualifiedName~TAOM.Tests.Features.Messengers|FullyQualifiedName~TAOM.Tests.Features.Enlistment|FullyQualifiedName~TAOM.Tests.Composition|FullyQualifiedName~Localization|FullyQualifiedName~CoopAuthorityGateTests|FullyQualifiedName~HarmonyPatchBindingTests|FullyQualifiedName~PatchCategory"`:
`Failed: 0`; `MessengerSessionRulesPatchTests` 8, `MessengerSessionRulesWiringTests` 6,
`MessengerCampaignBehaviorSourceTests` 4, `MessengerEntryPolicyTests` 6,
`EnlistmentMessengerWiringTests` 2 passed. Data: no ERROR beyond V0.

**Expected staged set** (34): `M Main/Composition/FeatureModules.cs`,
`M Main/Features/Enlistment/Hooks/EnlistmentDialogBehavior.cs`,
`M Main/Features/Enlistment/Presentation/ServiceVocabulary.cs`,
`A Main/Features/Messengers/Hooks/MessengerSessionRulesPatch.cs`,
`M Main/Features/Messengers/MessengerCampaignBehavior.cs`, `A Main/Features/Messengers/MessengerEntryPolicy.cs`,
`A Main/Features/Messengers/MessengerSessionRulesModule.cs`,
`M Main/Features/ReturnToArmy/Hooks/Patch87_ReturnToArmy.cs`, the 12 language files and
`taom_module_strings.xml` (all `M`), `M TAOM.Tests/Features/Enlistment/EnlistOfferVisibilityTests.cs`,
`M TAOM.Tests/Features/Enlistment/EnlistmentContainerWiringTests.cs`,
`A TAOM.Tests/Features/Enlistment/EnlistmentMessengerWiringTests.cs`,
`M TAOM.Tests/Features/Enlistment/ServiceVocabularyTests.cs`, the four new
`TAOM.Tests/Features/Messengers/*Tests.cs` (`A`), `M docs/features/enlistment.md`,
`M docs/features/messengers.md`, `M docs/reference/harmony-patch-registry.md`,
`M docs/reference/taleworlds-api-snapshot/patch-targets.md`,
`A docs/reviews/rca-messenger-session-rules-2026-10-23.md`.

Message file `msg-B5.txt`:

```
fix(messengers): v2.0.28 - messengers no longer move armies or troops

A conversation through a messenger let you do things that need you to
stand beside the lord. Agreeing to join his army snapped your party
across the map to him, asking him to join yours did the same to his
party, swearing service moved you to his camp, and a few lines handed
troops, goods or a companion over at a distance.

Through a messenger these are now hidden: joining his army or asking
him to join yours, the board game, trading troops and goods with a
party of your clan, and calling a companion back. The offer to take
service is greyed with a reason: swear it in person. Barter and hiring
a wanderer still work through a messenger.

A messenger to someone in a town where your army's leader is, while
you belong to that army but are not marching with it, used to attach
your party to the army in that town. Such a talk now happens in the
field. If anything goes wrong while the conversation opens, the game
cleans up as after a normal talk. The new greyed-oath line is English
in every language until it is translated.

Research: v1.4.8 LordConversationsCampaignBehavior.cs:2044, :2622
Research: v1.4.8 EnterSettlementAction.cs:71-75, PlayerEncounter :1082
Research: v1.4.8 LordConversationsCampaignBehavior.cs:334-336, so the
Research: "follow us" claim in the Patch87 comment is false; comment fix
Not-tested: the in-game checklist in docs/features/messengers.md
Ported-from: 1dc0a8d7c552cf56341cdaedface37da90ded9c1
```

Commit in the next Bash call; verify the subject.

#### Step B6: Part B verification and hand-off

1. Build and Test. **Verify**: `Failed: 0`; `Skipped` equals TA's; `Total` is TA plus 41 (3 in B1, 2
   in B2, 0 in B3, 4 in B4, 32 in B5; data rows count separately). A different total: list the new
   test classes and STOP if any expected one is missing.
2. Docs: `dead_links` and `orphan_features` equal L0; `ai_dashes: 0`. Data: no ERROR beyond V0.
3. `WT$ git log --oneline bannerlord-1.4.5..HEAD` lists nine commits (four from Part A), every
   subject with `v2.0.28`; `WT$ git status --porcelain | wc -l` prints `0`.
4. Report (Part B hand-off): TA; the final Total, Passed, Skipped; V0 and the final ERROR count;
   whether the B2 rename detection worked; the five commit SHAs. Recommend to the orchestrator:
   `/deep-review` over the branch (C# in many files), `/verify-bindings` (Patch42 changed, Patch114
   and Patch117 added) and the in-game checks below. You cannot run skills yourself.

## Disposition (every commit in the pack, plus the shared hunk the notes assign)

| 1.5.x commit | Group | Disposition |
|---|---|---|
| `c2449a306` | troopweight-garrison-patrol-penalty | **Ported** (A1). Lesson hunk dropped (WP142); v1.5.5 and review-link citations rewritten |
| `f83a63e57` | issue-886-castle-recruitment | **Ported** (A2). The transpiler-site test moves into `CastleRecruitmentEngineMirrorTests` (1.4.x lacks `TranspilerSiteBindingTests` unless WP122 landed); the patch-targets Diplomacy row (from unported `2410aec51`) and three lessons dropped; castle doc keeps 1.4.x's lack of War Chronicle (#765) |
| `0d3f2bc88` | issue-918-castle-ai-recruit-toggle | **Adapted** (A3). #918 ported, with the 1.4.x `TaomVolunteerModel` constructor (`ICulturalFeatsService`). Skipped: the #920 co-op stand-down in the dispatcher and behavior, its doc lines and the `coop-interop.md` row (D7); the dispatcher line-number edits that only #920 caused; feature-map and lessons (bookkeeping) |
| `93684c32c` | issue-887-ai-recruit-trip-alignment | **Ported** (A4) as `AlignmentRecruitmentModule` plus one `FeatureModules.All` line, Patch114 kept (D2); depends on WP123's `CampaignHotPathSettingsProvidersTests`. 1.5.x key counts in `configs-balance.md` (29, 31) not taken: 1.4.x has 24 |
| `6647f8f57` | issue-944-messenger-button-hint | **Ported** (B1), clean pick |
| `2c7f7fae9` | issue-989-messenger-button-brushes | **Ported** (B2) at the 1.4.x `Main/` path; creates `PrefabExtensionBrushTests` (WP140 extends it). Skipped: the Lines of Descent prefab and doc (1.5.x-only), the `moduledata-validation.md` row (absent on 1.4.x; WP140), two lessons |
| `464cbd375` | issue-898-messenger-pricing | **Adapted** (B3): only the dead `IsInMapEvent` field and the `ResolveHero` comment. Skipped: the pricing config refactor, `MessengerTargetReader`, pricing tests, the pricing RCA edits, the race-age doc line and the lesson (all serve `bbf2db053`). The optional fold of 1.4.x's two range checks into a `Checked` helper is rejected (simplicity criterion: a new helper for no behaviour change) |
| `1dc0a8d7c` | issue-898-messenger-session-rules | **Adapted** (B5) as `MessengerSessionRulesModule` plus one `FeatureModules.All` line, Patch117 kept (D2); Enlistment greyed oath adapted to 1.4.x's one-argument `EnlistUnavailableReason`. Ported as is: the comment-only `Patch87_ReturnToArmy.cs` edit (the claim it drops is false on v1.4.8, B5 item 1). Skipped: Tokens of Esteem code, tests and doc (1.5.x-only), the `coop-interop.md` row (D7), feature-map and lessons |
| `06a1a1b71` (shared hunk) | (SKIP-FEATURE Lord Courtesies) | **Transplanted, partial** (B4): only the messenger flag (`IMessengerConversationState`, `MessengerConversationState`, the `MessengerIoC` line, the behavior's constructor parameter and its four `Open`/`Close` calls, two constructor calls in tests, `MessengerConversationStateTests`), as the `1dc0a8d7c` notes require |
| `358c94559` | issue-898-messenger-session-rules | **Skipped** (SKIP-1.5-ONLY-FIX): it re-threads the commander-name parameter that `64691a56b` (SKIP-FEATURE) added to `EnlistUnavailableReason`; 1.4.x has no such parameter, and `1dc0a8d7c`'s own wiring regex already matches the 1.4.x call |
| `bbf2db053` | issue-898-messenger-pricing | **Skipped** (SKIP-FEATURE): distance pricing is a new feature |
| `00cbd43d1` | issue-898-messenger-pricing | **Skipped** (SKIP-FEATURE): its docs describe `bbf2db053` |

## Test plan

- New tests come with the ported commits; each step's Verify names them and their counts (Part A:
  3 + 13 + 6 + 10; Part B: 3 + 2 + 0 + 4 + 32). The relocated castle transpiler-site test, B2's RED
  proof by a one-file revert, B3's RED build and B4's RED-first flag test are this plan's own TDD
  points.
- Existing gates that must stay green: `HarmonyPatchBindingTests` (every target resolves on v1.4.8),
  `TAOM.Tests.Composition` (WP105's module rules and kernel anchors; `TryPatchCategory("Patch42_CastleRecruitment");`
  stays the ProcessLoad anchor), `PatchCategoryApplierTests`, `CoopAuthorityGateTests`,
  `CoopVetoClassificationTests` (only bool prefixes need entries; everything here is a postfix or
  transpiler), `GameModelOverrideBindingTests`, the localization gates (`LanguageFileCoverageTests`,
  `UnregisteredLocalizationKeyBaselineTests`).
- Structural pattern for any extra test: `TAOM.Tests/Features/WandererAllegiance/WandererAllegianceBindingTests.cs`
  (binding) and `WandererAllegianceWiringTests.cs` (module wiring).
- Not unit-testable (for the `Not-tested:` trailers and the in-game list): the Harmony patches running
  in a campaign, AI travel decisions, the castle entry recruit, the messenger conversation paths, the
  button look.
- Final: Test shows `Failed: 0` with T0 + 32 after Part A and TA + 41 after Part B.

## Done criteria

**Part A** (all must hold):

- [ ] `WT$ git log --oneline bannerlord-1.4.5..HEAD` shows the four Part A commits, each subject
  carrying `v2.0.28`, each body ending with its `Ported-from:` line.
- [ ] Build exits 0; Test shows `Failed: 0`, `Skipped` unchanged from T0, `Total` T0 + 32 (minus any
  removed WP122 Patch42 tests).
- [ ] `git grep -n -e 'Patch42_FillSettlements' -e 'Patch42_HourlyTickParty' -e 'IsCastleAndAiDisabled' -- Main TAOM.Tests`
  prints nothing; `git grep -c 'AlignmentRecruitmentModule()' -- Main/Composition/FeatureModules.cs` is 1.
- [ ] `git grep -n 'v1\.5\.5' -- Main/Features/TroopWeight Main/Features/CastleRecruitment Main/Features/AlignmentRecruitment docs/features/castle-recruitment.md docs/features/alignment-recruitment.md docs/features/troop-weight-system.md`
  prints nothing.
- [ ] Docs: `dead_links` and `orphan_features` equal L0; `ai_dashes: 0`.
- [ ] `git status --porcelain` is empty; no file outside the Scope list changed
  (`git diff --name-only bannerlord-1.4.5..HEAD`).

**Part B** (all must hold):

- [ ] Five more commits as above.
- [ ] Build exits 0; Test shows `Failed: 0`, `Skipped` unchanged from TA, `Total` TA + 41.
- [ ] `PrefabExtensionBrushTests` passed (not Inconclusive), and B2's RED run failed naming both old
  brush names.
- [ ] `git grep -n -e 'IsInMapEvent' -e 'isInMapEvent' -- Main/Features/Messengers TAOM.Tests/Features/Messengers`
  prints nothing; `git grep -c 'MessengerSessionRulesModule()' -- Main/Composition/FeatureModules.cs`
  is 1; `git grep -n -e Patch117 -e MessengerSessionRules -- Main/IoC.cs` prints nothing.
- [ ] `git grep -c 'id="taom_enlist_not_by_messenger"' -- Main/_Module/ModuleData` reports 13 files
  with 1 each.
- [ ] Data: no validator ERROR beyond V0. Docs: as Part A.
- [ ] `git status --porcelain` is empty; no file outside the Scope list changed.

## STOP conditions

Stop and report (do not improvise) if:

- A Step A0.3 check fails: WP104, WP105 or WP123 is not merged, the enlistment shapes
  (`EnlistUnavailableReason`, `OfferIsTakeable`) or the `TaomVolunteerModel` constructor differ from
  the 1.4.x ones this plan resolves against, or part of this work already exists.
- A cherry-pick leaves an unmerged path a step does not list, or a staged set differs from the
  step's expected set.
- A scrub script or a "replace exactly once" edit does not find its text (the files drifted).
- A binding test (`CastleRecruitmentEngineMirrorTests`, `AiVolunteerEstimateBindingTests`,
  `MessengerSessionRulesPatchTests`, `HarmonyPatchBindingTests`) fails or goes Inconclusive. Do not
  edit the transpiler anchor or a target name to make it pass: report the engine mismatch.
- The castle transpiler logs a warning in the site test (the v1.4.8 IL no longer matches the
  `GetAvailableWageBudget` anchor within 24 instructions of the first `get_IsCastle`).
- A `TAOM.Tests.Composition` test fails on one of the two new modules (D2 rules: `OwnsSaveData`,
  `GameModels`, `IoC.Resolver`, `.PatchCategory(`), or a kernel anchor moved.
- `PrefabExtensionBrushTests` goes Inconclusive, or B2's RED run does not fail on the two old brush
  names, or it fails on any other brush (another WP introduced an undeclared brush: report it).
- B4's `git apply --3way` reports a conflict, or B4's RED build does not fail with CS0246.
- `python tools/validate_moduledata.py` shows an ERROR that is not in V0, or the ModuleData commit
  hook denies the B5 commit (D20).
- Any change appears to need `Main/IoC.cs`, `Directory.Build.props`, `.claude/settings*.json` or a
  csproj.
- A commit hook denies a commit (report the message verbatim; never `--no-verify`).
- A build or test failure persists after two reasonable fix attempts.
- The B2 snapshot or any regeneration changes rows that belong to other features and the hand-edit
  path is unclear.

## In-game checks owed (maintainer)

Logs: TAOM writes `taom_debug_*.log` to the relative folder `Logs` (`Main/Core/Logging/FileLogger.cs:26`),
which resolves beside the game executable (`bin\Win64_Shipping_Client\Logs`); also `rgl_log_*.txt`.

1. **Startup.** No main-menu inquiry about failed patch categories; the debug log has no
   `[Module] AlignmentRecruitment failed`, no `[Module] MessengerSessionRules failed`, no
   `[CastleRecruitment] AiHourlyTick:` warning ("AI castle gate not patched") and no
   `[CastleRecruitment] ... not found` or `CheckRecruiting bind failed` line after loading a campaign.
2. **Troop weight.** An elven (Rivendell or Mirkwood) castle garrison over about a month of game time:
   no daily desertion of elite troops; the party screen and clan screen show a garrison's raw count
   against its real limit; an elite patrol is not slowed by an over-limit penalty.
3. **Castle recruitment.** AI lords ride to castles only when castle notables hold volunteers; an AI
   lord parked in a castle tops up hourly; with "AI Recruits From Castles" OFF, an AI lord entering a
   castle takes no castle volunteers while garrisons still recruit daily; with the setting ON nothing
   regresses. Kingdom lords far from home no longer target friendly foreign castles.
4. **Alignment scoring.** With "Apply To AI Lords" on, a Free lord near an Evil town at peace does not
   plan a recruiting trip there (food and trade visits still happen); no `[AlignmentRecruitment]
   Patch114 postfix failed` line.
5. **Messenger button.** On a hero's encyclopedia page the Send Messenger button has the popup button
   look, its label dims when greyed, and hovering a greyed button shows the reason.
6. **Messenger session rules.** Run the eight-item "In-game Checklist (#898)" in
   `docs/features/messengers.md`.

## Maintenance notes

- **Merge contention.** `Main/Composition/FeatureModules.cs` gets one appended line per ported module
  from many WPs; the integrator keeps every line (order does not matter for these two modules).
  `Main/SubModule.cs` changes only in the Patch42 block and the `TaomVolunteerModel` construction.
  The harmony registry gains Patch114 and Patch117 sections after Patch88; other WPs insert their own
  numbers nearby.
- **Patch numbers.** Patch114 and Patch117 are the 1.5.x numbers (D2), so a crash triage reads the
  same number on both lines. If WP122's patch-number gate lands, both need their registry sections,
  which this plan writes.
- **Later WPs that build on this one.** WP139 ports `852678f86` (a daily log check for AI castle
  recruits) and `da5c87c23` onto `CastleAiRecruitDispatcher`, `BlocksAiRecruitAtCastle` and
  `CastleRecruitmentEngineMirrorTests`; on 1.4.x that mirror file also holds the transpiler-site test,
  so a 1.5.x hunk there may need a context fix. WP140 extends `PrefabExtensionBrushTests` (`62fb6bb1d`,
  `103636439`); the `GameDir` member it may expect arrives with `8caac7da5`, not here. WP135 translates
  `taom_enlist_not_by_messenger` (paid; maintainer).
- **Stale after this WP (synced by WP143/WP144, not here).** `.ai/review-reference.md:575` still lists
  the two deleted Patch42 targets; CLAUDE.md's patch-category notes and `docs/reference/feature-map.md`
  do not mention Patch114 or Patch117; the backlinks footers of the touched docs are regenerated by
  the docs sync, not by hand.
- **Deliberate behaviour changes to review.** Removing the `FillSettlementsToVisitWithDistancesAsDays`
  transpiler means kingdom lords far from home stop targeting friendly foreign castles. The castle
  rule in `TaomVolunteerModel` blocks companion-led parties of the player's clan as AI lords. The #920
  co-op stand-down is absent (D7): under a co-op session, castles still recruit hourly while
  BannerlordCoop stops vanilla's town recruit.
- **What `/deep-review` should probe.** The `TaomVolunteerModel` constructor change and its single
  construction site; the dispatcher's guards against vanilla `HourlyTickParty`; `CastleAiToggle`'s
  per-think notable scan (castles only); the messenger open path's `catch { AbortConversation(); throw; }`
  (it rethrows into the inquiry callback, as on 1.5.x); the Enlistment greyed-oath wiring; the
  `EnlistmentContainerWiringTests` substitutes.
- **Deferred.** The 1.4.x
  `EnableMessengers` row still claims the behavior is not registered when off; it is registered
  unconditionally (`SubModule.cs` Messengers block); fixing that wording belongs to a messenger docs
  pass.
