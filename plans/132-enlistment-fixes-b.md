# Plan 132: Land the 1.5.x enlistment fixes for sit-out pay, the pump clock, board pay, siege duties, quartermaster answers, kit-tier names and the enlisted survival roll on the 1.4.x line

> **Executor instructions**: Follow this plan step by step. Run every verification command and
> confirm the expected result before moving on. If anything in "STOP conditions" occurs, stop and
> report; do not improvise. Do not edit `plans/README.md`: the integrator records this plan's status
> and test counts when merging; put the numbers listed under "Hand-off report" in your final
> message. Three parts on one branch, in order: Part A (survival roll, translator check,
> quartermaster answers, kit-tier names), Part B (sit-out rule, board pay, pump clock), Part C (one
> fitness check, siege duties and wage order, the dead battle rule). Each part ends on a green tree
> with its own done criteria; a later session may resume at the next part. A code block inside a
> numbered or bulleted step carries that list's indentation: strip as many leading spaces from each
> line as its opening fence has, then copy the rest exactly.
>
> **Drift check (run in Step A0.6)**:
> `git diff --stat 1b00d1acd..HEAD -- Main/Features/Enlistment Main/Adapters TAOM.Tests/Features/Enlistment TAOM.Tests/Adapters Main/Features/BattleBalance/Models/TaomPartyHealingModel.cs TAOM.Tests/Features/BattleBalance/TaomPartyHealingModelTests.cs Main/SubModule.cs Main/Features/DevConsole/Cheats/EnlistmentRescueCheats.cs Main/Features/MenuLinkColors Main/_Module/ModuleData/taom_enlistment_strings.xml Main/_Module/ModuleData/Languages Main/_Module/ModuleData/enlistment tools/translate_with_claude.py tools/harvest_duty_strings.py tools/tests docs/features/enlistment.md docs/features/battle-balance.md docs/features/menu-link-colors.md docs/reference/harmony-patch-registry.md docs/reference/gamemodel-registry.md docs/reference/taleworlds-api-snapshot`
> WP119 and WP128 changed most of these on purpose (see "What earlier WPs put in place"). Compare
> the live files against the anchors each step checks and STOP only on a real mismatch.

## Status

- **Priority**: P1 (A1, the enlisted survival roll, is a live defect that makes a skilled soldier
  likelier to die; the rest is P2 and P3)
- **Effort**: L overall. Part A: M (two TDD hand-ports, two picks, two scripted renames). Part B: L
  (one large pick with test rebuilds, one TDD hand-port, one pick). Part C: M (one TDD hand-port,
  two picks with fix-ups)
- **Risk**: MED (stateful feature: battle joins, the payout, the service pump; every change comes with
  the 1.5.x unit tests; no save field is added or renamed)
- **Depends on**: WP128 (`plans/128-enlistment-fixes-a.md`), merged with its own dependencies WP105
  and WP119 (and through them WP101 and WP104). Wave 4 runs after waves 1 to 3, so WP110, WP112,
  WP123 and WP125 are normally merged too; Step A0 checks only what this plan needs.
- **Category**: bug (plus two behaviour-neutral refactors and a tool fix)
- **Wave / lane**: wave 4, lane A, after WP128 (both edit the same Enlistment files; never run them
  in parallel)
- **Executor**: laptop
- **Decisions applied**: D1 (no `CHANGELOG.md`; the commit body is the entry), D2 (Enlistment and
  BattleBalance have no `*Module.cs` on either line; the one `Main/SubModule.cs` edit removes an
  argument from an existing hand-wired registration), D7 (co-op is not supported on 1.4.x: the
  co-op branches inside ported code come along unchanged, and the 1.5.x registry sentences about
  the co-op module are not copied), D8 (subjects carry `v2.0.28`), D10 (an ordinary session executes
  this plan), D19 (git only through the Bash tool), D20 (A3 and B1 commit ModuleData, so the laptop
  Armory refresh of WP101 must be done; A0 checks it). Plan decisions, made under the maintainer's
  2026-11-04 delegation:
  - **P132-1** The 1.5.x hunks of `docs/features/enlistment.md` are not applied: they describe
    unported 1.5.x features (seven ranks, retinue line, column line, term end) and cite v1.5.5
    lines. This plan writes a WP132 section with v1.4.8 citations instead.
  - **P132-2** From `4666a254a` only the board's daily-pay source; the retinue-cost line is a feature.
  - **P132-3** From `5a8c73d26` only the identifier renames and the save-key pin; its parameter and
    comment renames sit on the unported seven-rank ladder.
  - **P132-4** In `b0625f0d1`, 1.4.x keeps `RunDailyTick(double nowDays)`: the 1.4.x daily tick still
    reads `nowDays` for contract expiry (`EnlistmentDailyService.cs:222-223`); 1.5.x moved that to the
    unported term-end feature (`033bd2e4c`).
  - **P132-5** From `113d3483b` only the translator fix and its test; the survival-roll split is
    skipped because A1 applies the redirect removal straight to the 1.4.x model.
  - **P132-6** `1d9cc42f1`'s three sit-out tunables ship at the 1.5.x values (0.85 share, half base
    XP, 1 trust). They are the exploit fix's parameters and live in `enlistment_config.json` `sitOut`.
  - **P132-7** A review record (`docs/reviews/rca-*.md`) travels with a commit ported whole, as in
    WP128; the records of partial ports (`4666a254a`, `5e7a0da6e`, `113d3483b`, `5d70714c4`) describe
    unported features and stay behind.
  - **P132-8** From `5e7a0da6e` only `CommanderSnapshot.IsFit` and its six call sites. Its board line
    for a battle the oath keeps the soldier out of (`OathSitOutRule`) is deferred: after WP128 only
    the vassal reason (#960) exists on 1.4.x, and the change is display only.
- **Planned at**: 1.4.x `origin/bannerlord-1.4.5` `1b00d1acd`; 1.5.x `origin/bannerlord-1.5.x`
  `e45f6df2c`; 2026-11-04 (the 1.5.x ref has since moved on; every ported commit is an ancestor of
  `e45f6df2c`)
- **Issue**: port of #829, #936 (partial), #830, #839 (partial), #841, #860 (the translator part of
  `113d3483b`; `5d70714c4` carries no issue, its record is the #860 camp review), #1014, #1016
  (partial), #796. No new work needs its own issue.

## Why this matters

Enlistment ("serve as a soldier in a lord's party") is on both lines. WP128 lands the first half of
the 1.5.x fixes; these are the rest, every one checked against `origin/bannerlord-1.4.5`:

- **A skilled soldier is likelier to die.** `TaomPartyHealingModel.cs:44` rolls an enlisted player's
  survival against his commander's party on the belief that his own party has no surgeon. v1.4.8
  uses the party leader as surgeon when none is assigned (`DefaultPartyHealingModel.cs:328-338`,
  `MobileParty.EffectiveSurgeon` `:834-844`), so the redirect swaps the player's own Medicine and
  Cheat Death for the commander's.
- **Paid for a battle he never fought (#830).** A soldier can pick "Leave it to the others" in his
  commander's battle, let it auto-resolve and collect full battle XP and renown.
- **The service upkeep runs twice per frame (#841).** `EnlistmentMaintenanceBehavior.cs:67` (campaign
  tick) and `EnlistmentMenuBehavior.cs:133` (wait-menu tick) both pump it while time runs.
- **The board promises unpaid wages (#839).** `ServiceStatusService.cs:98` quotes the rank's table wage
  while `ServiceRewardService.GetDailyWage()` (`:85-89`) pays 0 in the grace state.
- **Defenders never get siege duties, and a debt above the cap eats a day's pay (#1014).**
  `ArmyRhythmProbeAdapter.cs:34` reads only `MobileParty.SiegeEvent`, which is the besieger's camp
  event (v1.4.8 `MobileParty.cs:1132`); `ServiceRewardService.cs:148` floors banked debt at
  `priorArrears - delivered`.
- **The quartermaster always says "already drawn" (#829):** the refusal line has no condition
  (`EnlistmentQuartermasterBehavior.cs:76-83`, priority 111).
- **Dead code and duplication (#796, #1016, #936):** ownership rule R1b never fires
  (`EncounterAdapter.cs:98-100` computes the flag and never passes it, `:107-115`); the same commander
  fitness test is written six times; `EnlistmentRank` and `ServiceRank` have identical members.
- **Translations can silently drop `{newline}` (#860 follow-up):** `tools/translate_with_claude.py:392`
  and `:394` match only uppercase tokens. WP135's translation run comes after this plan.

Neither Harad log (E3, E4) has an enlistment service line, so none of this bears on the reported
freezes; it repairs the feature for players who enlist.

## Current state

### Executor environment

- Laptop; repo `C:\Users\mikew\source\repos\TAOM`; Bannerlord v1.4.8 (build 119303) at
  `C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord` (`BANNERLORD_GAME_DIR`).
  Never write into the game install.
- Base: local `bannerlord-1.4.5` after WP101 plus every WP merged before this one (WP128 included).
- Worktree `C:\Users\mikew\source\repos\taom-port\132`, branch `port148/132-enlistment-fixes-b`,
  helper folder `C:\Users\mikew\source\repos\taom-port\wp132-scripts` (outside the worktree, never
  committed). A `cd` does not persist between Bash calls. In this plan **`WT$` at the start of a
  command means** `cd /c/Users/mikew/source/repos/taom-port/132 && PY=/c/Python314/python.exe && S=../wp132-scripts && `.
- The repo has `core.autocrlf=true`, and `.gitattributes` covers scripts only (plus one generated
  HTML page); these files are CRLF in the worktree. The helper scripts read and write bytes and keep
  each file's BOM and line endings.

### Refs and simulation (this session)

- `git merge-tree --write-tree --name-only --no-messages --merge-base=<sha>^ origin/bannerlord-1.4.5 <sha>`:
  all 12 commits conflict on the bare tip (exit 1), mostly in the 12 language files, docs and code
  that later 1.5.x commits or WP128 bring.
- A chained simulation in git objects on a projection of this plan's base (the tip plus WP112's
  `e9854bbd0` and `2c09a4145`, the plan 014 end state `aac0e05bf`, WP128's `2d1e5f338`,
  `b24e8549e`, `7a912dcd5`, `c04ba3fe5`, `1559b3261`, `d4af27d3e`, `f21123c6a`, `388e89de0`,
  `90035a799`) then this plan's commits in 1.5.x order leaves only these code conflicts:
  `1d9cc42f1` in `EnlistmentContentConfigProviderTests.cs`; `069962aa4` in
  `ServiceMaintenanceService.cs` (one block) and `EnlistmentHooksLineCountTests.cs` (absent);
  `b0625f0d1` in `ContentConfigs.cs` (a comment), `EnlistmentDailyServiceTests.cs`,
  `EnlistmentPumpAuthorityTests.cs`, `ServiceMaintenanceServiceTests.cs` (constructor lines) and the
  harvest tool files; `3b848c411` in `ContractRenewalService.cs`/`ContractRenewalTests.cs` (absent)
  and the comment regions WP128 hand-edited. `1a132c727`, `01f02c3a0` and `5c5072dbc` apply cleanly
  there. The projection carries the full `1559b3261` and lacks WP128's hand ports of `9304a4cb9`,
  `75e190abe`, `218fa4053` and `a20b4d154`, so the steps below also name the extra conflicts those
  hand ports can cause.
- Nothing ported here exists on 1.4.x yet: `QuartermasterAnswer`, `EnlistmentKitTier`,
  `IsServingUnder`, `SitOutPolicy`, `OnWaitMenuFrame`, `PartyIsInRaidEvent`, `bool IsFit` and
  `arrearsReleased` have 0 hits on `origin/bannerlord-1.4.5` and `origin/fix/public-armory-ids-145`.

### What earlier WPs put in place (checked in Step A0)

| WP | What this plan relies on |
|---|---|
| 104 | `TAOM.Tests/Infrastructure/RepoPaths.cs` with `public static string ReadSource(string relativePath, bool stripComments = false)` |
| 105 | `Main/Composition/FeatureModules.cs` (Enlistment is not a module; nothing here registers through it) |
| 119 | Plan 014: `IArmyRhythmSnapshotService.ResetForNewSession()`, `ServiceMaintenanceService` holds `_presenter` and `_rhythm` and calls `_rhythm?.ResetForNewSession()` in `ResetSessionCaches`; `EnlistmentSessionResetTests.cs` exists |
| 128 A1 | `IEncounterAdapter.IsAttackBarredByTruce`, `IEnlistmentMenuService.ShouldBlockBattleExit()`, `MapEventHelperCanMainPartyLeaveBattlePatch` (the code `1d9cc42f1` refactors) |
| 128 A2/A3 | `EnlistmentServiceMenuWiringTests.cs` (`069962aa4` adds two tests); `VillageRaidReader` and `partyIsRaiding:` in both `CommanderLordAdapter` snapshot builders |
| 128 B1 | Only the join-time part of `1559b3261`: `EnlistmentReconciler.cs` has **no** crown-war release, the reconciler takes no `IServiceDiplomacyService` |
| 128 B4 | The grace guard in `EnlistmentBattlePayoutService.PayOutBattle` and the test `PayOut_CommanderUnavailable_EvaluatesNoPromotion`, which calls `PayOutBattle(won: true)` |
| 128 B5 | `90035a799` (`IsJoinedBattleEncounter`, `ForgetEndedBattleResult`); its comment hunks in `EnlistmentLoadNormalizer.cs` and `IEnlistmentConfigProvider.cs` were hand-merged |
| 128 C2 | `388e89de0`: `RunDailyTick(double nowDays)` and `GetSnapshot(double nowDays)` (one argument each) |
| 128 C3 | `218fa4053`: `BattleVictories`/`BattleDefeats` gone from `ServiceContentRecord`, `InMemoryEquipmentIssueLedger.cs` and `EquipmentIssueLedgerTests.cs` deleted, `new EnlistmentConfigProvider()` takes no argument |
| 128 C4 | `a20b4d154`: `ServiceStatusService` keeps `_config` and gains `ActiveDutyTitle(...)`; `tools/harvest_duty_strings.py` exists |
| 112, 125 (normally merged) | `EncounterAdapter` main-party reads (`e9854bbd0`); `ServiceStatusService.RefreshIfChanged` language check. Context only |

### Files and roles

| Part | Files | Role |
|---|---|---|
| A1 | `Main/Features/BattleBalance/Models/TaomPartyHealingModel.cs`, `Main/SubModule.cs` (single-owner, one line), `TAOM.Tests/Features/BattleBalance/TaomPartyHealingModelTests.cs`, `docs/features/battle-balance.md`, `docs/reference/gamemodel-registry.md` | Survival redirect removed |
| A2 | `tools/translate_with_claude.py`, `tools/tests/test_translate_placeholders.py` (new) | Placeholder regexes |
| A3-A6 | `Main/Features/Enlistment/Equipment/*`, `Hooks/EnlistmentQuartermasterBehavior.cs`, the dialog gate and its two dialog behaviors, `Content/Domain/ServiceContentRecord.cs`, `Main/Adapters/IPartyItemRosterAdapter.cs` (a comment), their tests, strings | Quartermaster answers, renames |
| B1 | `Main/Adapters/EncounterAdapter.cs`, `IEncounterAdapter.cs`, `SitOutSnapshot.cs` (new); `Main/Features/Enlistment/SitOutPolicy.cs`, `SitOutLedger.cs`, `Hooks/EncounterGameMenuOrderAttackConditionPatch.cs` (new); `EnlistmentMenuService.cs`, payout, content behavior, announcer, maintenance, IoC, config classes, `enlistment_config.json`, strings, tests, registry, API snapshot | Sit-out rule |
| B2 | `ServiceStatusService.cs`, `Content/IEnlistmentWagePreview.cs`, `ServiceStatusServiceTests.cs` | Board pay |
| B3 | `CommanderLordAdapter.cs`, `CommanderTickSnapshot.cs`, `ServiceMaintenanceService.cs`, `IServiceMaintenanceService.cs`, `IEnlistmentReconciler.cs`, `Hooks/EnlistmentBattleBehavior.cs`, `EnlistmentMaintenanceBehavior.cs`, `EnlistmentMenuBehavior.cs`, tests | Pump clock |
| C1 | `CommanderSnapshot.cs` and six call sites, `TAOM.Tests/Adapters/CommanderSnapshotIsFitTests.cs` (new) | One fitness check |
| C2 | Rhythm probe and snapshot, reward service, `MeritConfig.cs`, two MenuLinkColors comments, tests, `tools/harvest_duty_strings.py` and its test, `docs/features/menu-link-colors.md`, registry | Siege duties, wage order |
| C3 | `EncounterAdapter.cs`, `EncounterOwnershipSnapshot.cs`, `EncounterOwnershipPolicy.cs`, `EnlistmentLoadNormalizer.cs`, `EnlistmentReconciler.cs` and comment-only files, tests | R1b removed |
| A7, B4, C4 | `docs/features/enlistment.md` | The WP132 section |

### 1.4.x excerpts (`origin/bannerlord-1.4.5`; earlier WPs may shift lines)

- `TaomPartyHealingModel.cs:16` `private readonly TAOM.Features.Enlistment.IEnlistmentStateQuery _enlistment;`;
  `:18-27` the constructor takes it as its fourth parameter; `:32-44` a comment block ending
  `party = RedirectEnlistedPlayerToCommanderParty(party, character);`; `:106-142` the private method,
  which reads `_enlistment.CommanderHeroId` (`:127`).
- `Main/SubModule.cs:1040`: `campaignStarter.AddModel(new TaomPartyHealingModel(battleBalanceSettings, battleBalanceConfig, IoC.Resolve<ICareerPassiveService>(), IoC.Resolve<Features.Enlistment.IEnlistmentStateQuery>()));`
- `EnlistmentQuartermasterBehavior.cs:29` `private EquipmentIssueResult _lastResult;`, `:63` `() => _gate.CanRequestDischargeFrom(PartnerId()),`,
  `:100` `_lastResult = _equipment.IssueForRank(cultureId, assignment, rank);`.
- `Equipment/EnlistmentRank.cs:10` `public enum EnlistmentRank` (Recruit, Soldier, Veteran, Sergeant);
  `Content/Domain/ServiceEnums.cs` `ServiceRank` has the same four names.
- `Content/Domain/ServiceContentRecord.cs:48-49` the `HighestIssuedEquipmentRank` summary and property;
  `:144-145` writes `kitRank=`; `:203-207` reads `kitRank`.
- `ServiceStatusService.cs:48` `private readonly IEnlistmentContentConfigProvider _config;`; `:98`
  `dailyWage: DailyWageFor(content.Rank),`; `:104-115` the private `DailyWageFor`.
- `Content/ServiceRewardService.cs:85-89` `GetDailyWage()` returns 0 unless a wage is due
  (`IsWageEarningToday`, `:101-103`, excludes `CommanderUnavailable`); `:148`
  `var alreadyBanked = System.Math.Max(0, priorArrears - delivered);`.
- `Main/Adapters/ArmyRhythmProbeAdapter.cs:34` `probe.InSiege = party.SiegeEvent != null;`.
- `Content/Domain/MeritConfig.cs:83` `public float MaxOfferChance { get; set; } = 0.45f;`; the shipped
  `enlistment_config.json:57` has `"maxOfferChance": 0.4`.
- `Content/EnlistmentDailyService.cs:165` `RunDailyTick(double nowDays, double hourOfDay)` (one
  argument after WP128 C2); `:222-223` contract expiry reads `nowDays`.
- `Hooks/EnlistmentContentBehavior.cs:72` `if (!_battle.PayOutBattle(mapEvent.WinningSide == mapEvent.PlayerSide))`.
- `EnlistmentIoC.cs:48` registers `IEnlistmentLoadNormalizer`; `:87`
  `container.Register<Content.IEnlistmentBattlePayoutService, Content.EnlistmentBattlePayoutService>(Reuse.Singleton);`.
- `EncounterOwnershipPolicy.cs:61` `if (snapshot.IsBattleEncounter)` (R1b);
  `EnlistmentLoadNormalizer.cs:114` `IsMidBattle` reads the same always-false flag.
- `tools/translate_with_claude.py:392` `VAR_PATTERN = re.compile(r"\{[A-Z_][A-Z0-9_.]*\}")`; `:394`
  `CONDITIONAL_OPEN = re.compile(r"\{\?[A-Z_][A-Z0-9_.]*\}")`.
- `MenuLinkColors/Hooks/Patch64_MenuLinkColors.cs:14-15` and `MenuLinkStyleRewriter.cs:76-77` say the
  `ContextText` setter runs once per menu open; `MenuLinkStyleRewriter.cs:71` exits early on link-free
  text (`IndexOf(LinkTagMarker)`).
- `TAOM.Tests/Migration/IlCallScanner.cs`: `FindCallers(Assembly, Func<MethodBase,bool>, out List<string>, out int)`
  `:28` and `ExtractCalledMethods(MethodBase, byte[])` `:111`, used by the new binding tests.

### Engine facts verified on the installed v1.4.8

Source: taom-src cache `C:\Users\mikew\.taom-src\v1.4.8\<Type>.cs` (marked T) or the dump
`C:\Decompiled_Bannerlord\_categories_v1.4.8\Campaign\TaleWorlds.CampaignSystem\...` (marked D).

| Member | Signature or fact | Where |
|---|---|---|
| `EncounterGameMenuBehavior.game_menu_encounter_order_attack_on_condition` | `private bool ...(MenuCallbackArgs args) { return MenuHelper.EncounterOrderAttackCondition(args); }`; registered as option `"encounter", "str_order_attack"` at `:176` | T `:1919-1922` |
| `...game_menu_encounter_attack_on_condition` | calls exactly `MenuHelper.CheckEnemyAttackableHonorably(args)` then `return MenuHelper.EncounterAttackCondition(args)` | D `EncounterGameMenuBehavior.cs:1898-1902` |
| `MenuHelper` | `public static bool EncounterAttackCondition(MenuCallbackArgs)` `:182`; `public static void CheckEnemyAttackableHonorably(MenuCallbackArgs)` `:452` (both write only to the args); `EncounterOrderAttackCondition` `:465` writes `IsInvulnerable` and `SEND_TROOPS_TEXT` ("Leave it to the others." when the player's own party has no healthy men) | T |
| `MenuCallbackArgs` | `public bool IsEnabled = true;` `:10`; `public TextObject Tooltip;` `:14`; ctor `(MenuContext, TextObject)` `:26` only stores | T |
| `MapEvent` | `static PlayerMapEvent` `:107`; `PlayerSide` `:109`; `IsRaid` `:205`; `IsSiegeAssault` `:211`; `IsSallyOut` `:215`; `HasWinner` `:229`; `public bool IsPlayerSimulation { get; set; }` `:242`; `IsPlayerMapEvent` `:282`; `WinningSide` `:307`; `public void GetStrengthsRelativeToParty(BattleSideEnum, out float, out float)` `:1456` (read-only sum, each side starts at 0.1) | T |
| `MapEvent.Update` / `FinalizeEventAux` | `FinishBattle()` only `if (!IsPlayerMapEvent \|\| PlayerEncounter.Current == null)` `:1041`; `FinalizeEventAux` `:2057` raises `OnMapEventEnded` `:2079` and calls `sides[i].HandleMapEventEnd()` `:2158` | D |
| `BattleSimulation` | ctor sets `_mapEvent.IsPlayerSimulation = true` | D `BattleSimulation.cs:60` |
| `PlayerEncounter` | `InitSimulation` `:2007` builds the only `BattleSimulation` `:2011`, called only from `MenuHelper.cs:576`; `JoinBattleInternal` `:930` sets `_encounteredParty` to the opposing leader `:945`; `Finish` `:1013` calls `FinalizeBattle` `:1071` (which calls `_mapEvent.FinalizeEvent()` `:1909`) and clears `PlayerEncounter` `:1060`; `LeaveBattle` `:1964` clears `IsJoinedBattle` `:1992` | D |
| `MapEventSide.HandleMapEventEnd` | `:420-427`; `HandleMapEventEndForPartyInternal` sets `party.MapEventSide = null` `:434` | D |
| `Hero` | `static Hero MainHero` `:892`; `bool IsWounded` `:332` | T |
| `TextObject.IsNullOrEmpty(TextObject)` `:188`; `GameTexts.FindText(string, string = null)` `:69`, field `_gameTextManager` `:61`; `str_string_newline_string` is in `Modules/Native/ModuleData/module_strings.xml` | | T |
| `GameMenu` | `RunOnTick` passes `CampaignTime.Now - _previousTickTime` to a wait menu's tick `:224-232`; `ActivateGameMenu` `:335-337` and `SwitchToMenu` `:358-360` set `TimeControlMode = Stop` | D |
| `MapState.OnTick` | at a menu, `OnMenuModeTick` (the wait-menu tick) runs `:156-158` before `OnMapModeTick` `:160` (`RealTick` `:186`, `Campaign.Tick` `:189`) | D |
| `Campaign` | `TickMapTime`: `_dt = num; MapTimeTracker.Tick(4320f * num);` `:873-874`; `Tick`: gate `if (_dt > 0f \|\| CurrentTickCount < 3)` `:958`, `CampaignEventDispatcher.Instance.Tick(_dt)` `:960`, `MapEventManager.Tick()` `:962`, `EncounterManager.Tick(_dt)` `:974` | D `Campaign.cs` |
| `MapEventManager.Tick` | `:51`; updates an event `if (IsRaid \|\| event != MainParty.MapEvent)` `:59-61` | D |
| `CampaignTime` | `public double ToSeconds` `:134`; `operator -` `:347` | T |
| `MobileParty` | `SiegeEvent => BesiegerCamp?.SiegeEvent` `:1132`; `CurrentSettlement` `:578`; `IsCurrentlyAtSea` `:482`; `EffectiveSurgeon` falls back to `LeaderHero` `:834-844` | T |
| `Settlement` | `public SiegeEvent SiegeEvent { get; set; }` `:103` | T |
| `DefaultPartyHealingModel` | `GetSurvivalChance` `:61`; `AddSurgeonSurvivalBonus` uses the surgeon role holder or else the party leader `:328-338` | T |
| `GameMenuVM` | `OnFrameTick` sets `ContextText` whenever `_requireContextTextUpdate` and recomputes it each frame `:481-512`; `IsMenuTextChanged` is true while the text has no attributes (`0 != null`) `:522` | T |

### Conventions that bind this work

- **ADR-007** adapters: services never touch sealed TaleWorlds types; engine reads stay in
  `Main/Adapters` (`EncounterAdapter.GetSitOutSnapshot`, the rhythm probe). **ADR-002** thin entry
  points: hooks and behaviors stay under 150 lines and delegate (the new patch is 93 lines).
  **ADR-003** no `#region`; **ADR-004** no `[Obsolete]`; **ADR-005** no `#if DEBUG` outside IoC
  registration. None is introduced.
- **TDD**: A1, A2, B2 and C1 are written test first (RED, then GREEN); cherry-picks bring their own
  tests; A5 and A6 are behaviour-neutral renames verified by the compiler and the existing tests.
- **MCM json2 rule** (rename a persisted setting, never flip its default): no MCM setting changes.
- **`.claude/rules/harmony-patches.md`**: `MovementOrder` postfixes go through the shared deferred
  category. Not relevant: the new postfix is a menu predicate in the existing `Patch66_Enlistment`
  category, which `Main/SubModule.cs` already applies.
- **Localization**: new player text is `{=key}English`, registered in `taom_enlistment_strings.xml`,
  with an English placeholder row in each of the 12 language files (`LanguageFileCoverageTests`
  requires all 12). Never run `tools/translate_with_claude.py` (it spends money; WP135 owns it).

## Commands you will need

| Purpose | Command (prefix `WT$`) | Expected |
|---|---|---|
| Restore | `dotnet restore TAOM.sln` | exit 0 |
| Build | `dotnet build TAOM.sln -p:DisableModuleCopy=true -p:ModuleId=` | exit 0, 0 errors (a BHA0001 Patch88 warning may print) |
| Test | `dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId=` | `Failed: 0`; give the call a 600000 ms timeout |
| Filtered | the Test command plus `--filter "<expr>"` | `Failed: 0` unless a RED step says otherwise |
| ENL filter | `--filter "FullyQualifiedName~TAOM.Tests.Features.Enlistment\|FullyQualifiedName~TAOM.Tests.Infrastructure.Localization\|FullyQualifiedName~TAOM.Tests.Adapters"` | `Failed: 0` |
| Data | `"$PY" tools/validate_moduledata.py` | no new ERROR vs D0 (recorded in A0) |
| Tool tests | `"$PY" -m unittest discover -s tools/tests -t .` | no new failure vs U0 |
| Docs | `"$PY" tools/lint_docs.py --summary --dash-base $(git merge-base HEAD bannerlord-1.4.5)` | `ai_dashes: 0`; `dead_links` and `orphan_features` equal L0 |
| Snapshot | `pwsh -NoProfile -File tools/snapshot_api_surface.ps1 -Check` (after a build) | exit 0 when S0 was 0 |
| Duty strings | `"$PY" tools/harvest_duty_strings.py --check` | exit 0, no change reported |

(The `\|` in the ENL row is a table escape: type a plain `|`.) Both `-p:` flags are mandatory on every
build and test: on 1.4.x `Main/TAOM.csproj:133-136` does not gate the CopyModule target on
`DisableModuleCopy`, so a build without `-p:ModuleId=` deploys into the game install. Never
`./build.ps1`; never two builds at once; always build before a `--no-build` test.

## Scope

**In scope**: the files in "Files and roles" and the test files each step names; the 12
`Main/_Module/ModuleData/Languages/*/std_taom_enlistment_strings_*.xml` files (written `LANGFILES`
below: `$(ls Main/_Module/ModuleData/Languages/*/std_taom_enlistment_strings_*.xml)`, 12 paths); the
review records kept per P132-7; the helper scripts and message files in `wp132-scripts`.

**Single-owner file, one line only**: `Main/SubModule.cs` in A1 (the healing model's fourth argument).
`Main/IoC.cs`, `Main/TAOM.csproj` (SDK-style, globs new files), `Directory.Build.props` and
`.claude/settings.json` are not touched; if a step seems to need one: STOP.

**Out of scope**: `CHANGELOG.md` (D1); `.claude/**`, `CLAUDE.md`, `AGENTS.md`,
`docs/reviews/lessons/*`, `docs/reviews/LESSONS-LEARNED.md`, `docs/reviews/REVIEW-LOG.md`,
`docs/INDEX.md`, `docs/reference/feature-map.md`, `tools/README.md` (WP142 to WP144 sync them);
`docs/reviews/rca-camp-work-2026-10-26.md`, `rca-enlistment-retinue-cost-2026-10-24.md`,
`rca-enlistment-followups-board-2026-10-26.md`, `docs/reviews/sas-comparative-analysis-2026-08-08.md`,
`docs/modding/configs-balance.md`; every hunk of 1.5.x-only code these commits touch
(`ServiceHistoryService`, `ContractRenewalService`, `EnlistmentKitTiers`, `CoopSessionPolicy`,
`SurvivalChanceLine`, FarWatch, FieldCamp and the other 1.5.x feature folders, `PrefabBindingWalker`,
translation caches); the seven-rank ladder (`813657bf3`); `plans/README.md`.

## Git workflow

- Worktree (Bash tool only, D19):
  `mkdir -p /c/Users/mikew/source/repos/taom-port/wp132-scripts && git -C /c/Users/mikew/source/repos/TAOM worktree add C:/Users/mikew/source/repos/taom-port/132 -b port148/132-enlistment-fixes-b bannerlord-1.4.5`.
  Work only there. Never push, open a PR, tag, or merge into `bannerlord-1.4.5`; never touch the main
  checkout, its `.serena/project.yml` edit or its `stash@{0}`.
- **Port routine** (every pick step; the step lists its specifics):
  1. `WT$ git status --porcelain` prints nothing.
  2. `WT$ git cherry-pick -n <sha>`. Exit 1 with conflicts is expected when the step lists conflicts.
  3. `WT$ bash $S/drop.sh <paths>` for the step's "Drop" list.
  4. Resolve the step's conflicts and fix-ups as written, then `git add <those paths>`.
  5. `WT$ git ls-files -u` prints nothing, and `WT$ git diff --cached --check` prints no line naming a
     conflict marker.
  6. Build, then the step's tests.
  7. `WT$ git status --porcelain`: every line is staged (column 2 blank) and names a path of the step.
  8. Write the message to `wp132-scripts/msg-<step>.txt` with your file-writing tool. If
     `WT$ test -f "$(git rev-parse --git-path CHERRY_PICK_HEAD)" && echo pick-pending` prints
     `pick-pending`, run `WT$ git cherry-pick --quit` (it keeps the staged changes).
  9. In the NEXT Bash call, alone: `WT$ git commit -F $S/msg-<step>.txt && git log -1 --format='%h %s' && git status --porcelain`.
- Hand-port steps stage with explicit `git add <paths>` and commit the same way (steps 8 and 9).
  Never `git add -A`, `-u` or `.`.
- Never let `cherry-pick`, `--continue`, `-C`, `-c` or `--amend --no-edit` create a commit
  (`GIT_EDITOR=true` on this laptop would commit the 1.5.x message unedited). Never `--no-verify`. If a
  hook denies a commit, STOP and report its message verbatim.
- **Commit messages** (CLAUDE.md "Commits"): subject `<type>(<scope>): v2.0.28 - <description>`, at
  most 72 characters, the 1.5.x type and scope kept (A2 uses `fix(tools)`: none of `113d3483b`'s
  `coop` scope survives); a body for 1.4.x readers wrapped at 72 (bodies become player release notes),
  no em or en dashes, no `Co-Authored-By` or "Generated with" line; trailers `Research:`, `Rejected:`,
  `Save-compat:`, `Not-tested:` (continuation lines indented two spaces); one `Ported-from: <40-hex sha>`
  per included 1.5.x commit. `v2.0.28` is the `<Version>` in `Main/_Module/SubModule.xml`; if a
  `/release` commit has moved it (D8: the next is `v2.4.0`), use the value in that file. Each step gives
  its message.

## Helper scripts (save once, Step A0, with your file-writing tool, not a shell heredoc)

`wp132-scripts/drop.sh`:

```bash
#!/usr/bin/env bash
# Usage (worktree root, during a cherry-pick -n): bash ../wp132-scripts/drop.sh <path>...
# Puts each path back to HEAD: a path HEAD has gets HEAD's content (a conflict resolves to ours);
# a path HEAD lacks (a new file, or modify/delete) leaves the index and the disk.
set -u
for p in "$@"; do
  if git cat-file -e "HEAD:$p" 2>/dev/null; then
    git checkout HEAD "$p" && echo "restored: $p"
  elif [ -n "$(git ls-files -s -- "$p")" ] || [ -e "$p" ]; then
    git rm -q -f --cached -- "$p" >/dev/null 2>&1; rm -f -- "$p"; echo "removed: $p"
  else
    echo "untouched: $p"
  fi
done
```

`wp132-scripts/add_lang_rows.py` (English placeholder rows; same logic as WP128's):

```python
"""Usage (worktree root): python ../wp132-scripts/add_lang_rows.py KEY "English" [KEY "English" ...]"""
import pathlib, re, sys
import xml.etree.ElementTree as ET
LANGS = {"BR": "por-BR", "CNs": "zho-CN", "CNt": "zho-HK", "DE": "deu-DE", "FR": "fre-FR", "IT": "ita-IT",
         "JP": "jpn-JP", "KO": "kor-KO", "PL": "pol-PL", "RU": "rus-RU", "SP": "spa-LA", "TR": "tur-TR"}
ROW = re.compile(r'<string id="[^"]+" text="[^"]*" />')
args = sys.argv[1:]
if not args or len(args) % 2:
    print("STOP: pass KEY TEXT pairs"); sys.exit(1)
pairs = list(zip(args[0::2], args[1::2]))
for d, code in LANGS.items():
    p = pathlib.Path(f"Main/_Module/ModuleData/Languages/{d}/std_taom_enlistment_strings_{code}.xml")
    raw = p.read_bytes()
    bom = raw.startswith(b"\xef\xbb\xbf")
    text = (raw[3:] if bom else raw).decode("utf-8")
    rows = list(ROW.finditer(text))
    gap = text[rows[-2].end():rows[-1].start()] if len(rows) >= 2 else "x"
    if gap.strip():
        print(f"STOP: cannot find two adjacent plain rows in {p}"); sys.exit(1)
    add = "".join(f'{gap}<string id="{k}" text="{v}" />' for k, v in pairs if f'id="{k}"' not in text)
    text = text[:rows[-1].end()] + add + text[rows[-1].end():]
    p.write_bytes((b"\xef\xbb\xbf" if bom else b"") + text.encode("utf-8"))
    ET.parse(str(p))
    print(f"{p}: +{add.count('<string ')} rows")
```

`wp132-scripts/rename_ids.py`:

```python
"""Usage (worktree root): python ../wp132-scripts/rename_ids.py OLD=NEW [OLD=NEW ...] -- ROOT [ROOT ...]
Renames whole-word identifiers in every .cs file under the roots, byte-exact (BOM, line endings kept)."""
import pathlib, re, sys
args = sys.argv[1:]
if "--" not in args:
    print("STOP: usage OLD=NEW ... -- ROOT ..."); sys.exit(1)
cut = args.index("--")
pairs = [a.split("=", 1) for a in args[:cut]]
roots = args[cut + 1:]
if not pairs or not roots or any(len(p) != 2 or not p[0] or not p[1] for p in pairs):
    print("STOP: bad arguments"); sys.exit(1)
changed = 0
for root in roots:
    for path in sorted(pathlib.Path(root).rglob("*.cs")):
        raw = path.read_bytes()
        new, notes = raw, []
        for old, rep in pairs:
            new, n = re.subn(rb"\b" + re.escape(old.encode()) + rb"\b", rep.encode(), new)
            if n:
                notes.append(f"{old} x{n}")
        if new != raw:
            path.write_bytes(new)
            changed += 1
            print(f"{path.as_posix()}: {', '.join(notes)}")
print(f"files changed: {changed}")
```

`wp132-scripts/edit_lines.py`:

```python
"""Line edits that keep a file's BOM and line endings. Run from the worktree root.
cut FILE START END [--and-blank] [--with LINE]...
    Replace the lines from the one line containing START through the first line at or after it
    containing END (with --and-blank, plus one blank line right after) by the --with lines.
member FILE SIGNATURE
    Delete the member whose declaration line (after indentation) starts with SIGNATURE, with the ///
    and [ lines right above it and one blank line above those, through the first later line that is
    exactly its indentation plus "}".
blocks FILE ours|theirs|union [--expect TEXT]
    Resolve every conflict block: HEAD's side, the incoming side, or HEAD's then the incoming side.
    With --expect, STOP (writing nothing) unless every incoming side contains TEXT.
Prints STOP: and exits 1, writing nothing, when an anchor is missing or not unique."""
import pathlib, sys

def stop(msg):
    print("STOP: " + msg)
    sys.exit(1)

cmd, path = sys.argv[1], pathlib.Path(sys.argv[2])
raw = path.read_bytes()
bom = raw.startswith(b"\xef\xbb\xbf")
text = (raw[3:] if bom else raw).decode("utf-8")
nl = "\r\n" if "\r\n" in text else "\n"
lines = text.split(nl)
if cmd == "cut":
    start, end, rest = sys.argv[3], sys.argv[4], sys.argv[5:]
    repl = [rest[i + 1] for i, a in enumerate(rest) if a == "--with"]
    hits = [i for i, l in enumerate(lines) if start in l]
    if len(hits) != 1:
        stop(f"start text on {len(hits)} lines of {path}")
    s = hits[0]
    e = next((j for j in range(s, len(lines)) if end in lines[j]), None)
    if e is None:
        stop(f"end text not found after line {s + 1} of {path}")
    if "--and-blank" in rest and e + 1 < len(lines) and not lines[e + 1].strip():
        e += 1
    lines[s:e + 1] = repl
    print(f"{path}: lines {s + 1}-{e + 1} replaced by {len(repl)} line(s)")
elif cmd == "member":
    sig = sys.argv[3]
    hits = [i for i, l in enumerate(lines) if l.lstrip().startswith(sig)]
    if len(hits) != 1:
        stop(f"signature on {len(hits)} lines of {path}")
    d = hits[0]
    ind = lines[d][:len(lines[d]) - len(lines[d].lstrip())]
    s = d
    while s > 0 and lines[s - 1].startswith((ind + "///", ind + "[")):
        s -= 1
    e = next((j for j in range(d + 1, len(lines)) if lines[j] == ind + "}"), None)
    if e is None:
        stop(f"no closing brace for {sig}")
    if s > 0 and not lines[s - 1].strip():
        s -= 1
    del lines[s:e + 1]
    print(f"{path}: lines {s + 1}-{e + 1} removed")
elif cmd == "blocks":
    mode = sys.argv[3]
    expect = sys.argv[sys.argv.index("--expect") + 1] if "--expect" in sys.argv else None
    out, i, n = [], 0, 0
    while i < len(lines):
        if not lines[i].startswith("<<<<<<< "):
            out.append(lines[i]); i += 1; continue
        mid = next((k for k in range(i, len(lines)) if lines[k].startswith("=======")), None)
        end = None if mid is None else next((k for k in range(mid, len(lines)) if lines[k].startswith(">>>>>>> ")), None)
        if end is None:
            stop(f"unterminated conflict block at line {i + 1} of {path}")
        base = next((k for k in range(i, mid) if lines[k].startswith("|||||||")), mid)
        ours, theirs = lines[i + 1:base], lines[mid + 1:end]
        if expect is not None and not any(expect in l for l in theirs):
            stop(f"block at line {i + 1} of {path}: incoming side lacks '{expect}'")
        out += {"ours": ours, "theirs": theirs, "union": ours + theirs}[mode]
        i, n = end + 1, n + 1
    if n == 0:
        stop(f"no conflict block in {path}")
    lines = out
    print(f"{path}: {n} block(s) resolved ({mode})")
else:
    stop("unknown command " + cmd)
path.write_bytes((b"\xef\xbb\xbf" if bom else b"") + nl.join(lines).encode("utf-8"))
```

`wp132-scripts/copy_tests.py`:

```python
"""Usage (worktree root): python ../wp132-scripts/copy_tests.py REF FILE METHOD...
Copies each named test method of REF:FILE (with the [, /// and // lines right above it) into the
worktree FILE, before the closing brace of its first top-level class. STOP when a method is missing
or duplicated in REF, or already present in FILE."""
import pathlib, subprocess, sys
def stop(msg):
    print("STOP: " + msg); sys.exit(1)
ref, rel, names = sys.argv[1], sys.argv[2], sys.argv[3:]
src = subprocess.run(["git", "show", f"{ref}:{rel}"], capture_output=True, check=True).stdout
src = src.decode("utf-8-sig").replace("\r\n", "\n").split("\n")
path = pathlib.Path(rel)
raw = path.read_bytes()
bom = raw.startswith(b"\xef\xbb\xbf")
text = (raw[3:] if bom else raw).decode("utf-8")
nl = "\r\n" if "\r\n" in text else "\n"
lines = text.split(nl)
block = []
for name in names:
    sig = "    public void " + name + "("
    if any(l.startswith(sig) for l in lines):
        stop(f"{name} is already in {rel}")
    hits = [i for i, l in enumerate(src) if l.startswith(sig)]
    if len(hits) != 1:
        stop(f"{name}: {len(hits)} matches in {ref}:{rel}")
    s = hits[0]
    while s > 0 and src[s - 1].startswith("    ") and src[s - 1].lstrip().startswith(("[", "//")):
        s -= 1
    e = next((j for j in range(hits[0] + 1, len(src)) if src[j] == "    }"), None)
    if e is None:
        stop(f"{name}: no closing brace")
    block += [""] + src[s:e + 1]
close = next((i for i, l in enumerate(lines) if l == "}"), None)
if close is None:
    stop(f"no top-level closing brace in {rel}")
lines[close:close] = block
path.write_bytes((b"\xef\xbb\xbf" if bom else b"") + nl.join(lines).encode("utf-8"))
print(f"{rel}: copied {len(names)} method(s)")
```

`wp132-scripts/fix_patch64_registry.py` (used in C2; `\u2014` is the escape, so this file contains
no long dash):

```python
"""Usage (worktree root): python ../wp132-scripts/fix_patch64_registry.py
Corrects the MenuLinkColors paragraph of docs/reference/harmony-patch-registry.md for v1.4.8 and
removes the three long dashes on that line. Prints SKIP when the expected text is not there once."""
import pathlib
p = pathlib.Path("docs/reference/harmony-patch-registry.md")
raw = p.read_bytes()
bom = raw.startswith(b"\xef\xbb\xbf")
t = (raw[3:] if bom else raw).decode("utf-8")
D = "\u2014"
pairs = [
    ("The setter is reached only when `_requireContextTextUpdate` is set (menu open / `Refresh()`), and the prefix runs *before* vanilla's `if (value != _contextText)` guard so field and comparison both see the rewritten string " + D + " no extra `OnPropertyChanged`.",
     "`GameMenuVM.OnFrameTick` calls the setter whenever `_requireContextTextUpdate` is set, and `IsMenuTextChanged` sets it every frame while the menu's `TextObject` has no attributes (v1.4.8 `GameMenuVM.cs:481-512`, `:522`), so the prefix runs per frame; link-free text exits at the `IndexOf` probe. The prefix runs *before* vanilla's `if (value != _contextText)` guard, so field and comparison both see the rewritten string and no extra `OnPropertyChanged` fires."),
    ("**every frame** " + D + " a postfix", "**every frame**: a postfix"),
    ("silently returns `Default` " + D + " every hyperlink", "silently returns `Default`, and every hyperlink"),
]
if any(t.count(old) != 1 for old, _ in pairs):
    print("SKIP: the 1.4.x MenuLinkColors text is not there exactly once")
    raise SystemExit(0)
for old, new in pairs:
    t = t.replace(old, new)
p.write_bytes((b"\xef\xbb\xbf" if bom else b"") + t.encode("utf-8"))
print("fixed the MenuLinkColors paragraph")
```

## Steps

### Part A: survival roll, translator check, quartermaster answers, kit-tier names

#### Step A0: Preflight (no commit)

1. Create the worktree (Git workflow) and save the six helper scripts.
2. Prerequisites; each line must print its OK token (a missing token is a STOP naming the WP):
   ```
   WT$ grep -q "public static string ReadSource" TAOM.Tests/Infrastructure/RepoPaths.cs && echo OK-WP104
   WT$ test -f Main/Composition/FeatureModules.cs && echo OK-WP105
   WT$ test -f TAOM.Tests/Features/Enlistment/EnlistmentSessionResetTests.cs && grep -q "void ResetForNewSession();" Main/Features/Enlistment/Content/ArmyRhythmSnapshotService.cs && echo OK-WP119
   WT$ grep -q "bool IsAttackBarredByTruce" Main/Adapters/IEncounterAdapter.cs && grep -q "bool ShouldBlockBattleExit();" Main/Features/Enlistment/IEnlistmentMenuService.cs && echo OK-WP128-A1
   WT$ test -f TAOM.Tests/Features/Enlistment/EnlistmentServiceMenuWiringTests.cs && test "$(grep -c 'partyIsRaiding: VillageRaidReader.IsRaiding(party)' Main/Adapters/CommanderLordAdapter.cs)" = 2 && echo OK-WP128-A3
   WT$ grep -q "bool IsJoinedBattleEncounter" Main/Adapters/IEncounterAdapter.cs && grep -q "EnlistmentState.CommanderUnavailable" Main/Features/Enlistment/Content/EnlistmentBattlePayoutService.cs && echo OK-WP128-B
   WT$ test ! -e Main/Features/Enlistment/Equipment/InMemoryEquipmentIssueLedger.cs && test -f tools/harvest_duty_strings.py && grep -q "ActiveDutyTitle(" Main/Features/Enlistment/ServiceStatusService.cs && echo OK-WP128-C
   ```
3. Record without stopping: `WT$ grep -c "BattleVictories" Main/Features/Enlistment/Content/Domain/ServiceContentRecord.cs`
   (value **KBV**, expected `0` after WP128 C3).
4. Nothing of this WP is present yet:
   `WT$ git grep -n -E "QuartermasterAnswer|EnlistmentKitTier|IsServingUnder|SitOutPolicy|EvaluateSitOut|OnWaitMenuFrame|PartyIsInRaidEvent|bool IsFit|arrearsReleased" -- Main TAOM.Tests`
   prints nothing, and `WT$ git grep -c "RedirectEnlistedPlayerToCommanderParty" -- Main` prints
   `Main/Features/BattleBalance/Models/TaomPartyHealingModel.cs:2`. Anything else: STOP.
5. D20: `grep -o '<Version value="[^"]*"' "$BANNERLORD_GAME_DIR/Modules/LOTRLOME_Armory/SubModule.xml"`
   must not print `v2.0.26` (WP101 Part A replaces that Armory with the public 2026-09-29 build). If it
   does, or `BANNERLORD_GAME_DIR` is empty: STOP, A3 and B1 commit ModuleData.
6. Drift check (header). Then baseline: Restore; Build (exit 0); Test (record **T0** = total, passed,
   skipped; `Failed: 0`); ENL filter (record **E0**); Tool tests (record **U0**); Snapshot (record
   **S0** = exit code); Docs (record **L0** = `dead_links`, `orphan_features`); Data (record **D0** =
   its ERROR count). If D0 is above 0, say so in the hand-off: the ModuleData commit hook denies a
   commit while the validator reports an ERROR, and a denied commit is a STOP.

#### Step A1: Enlisted players roll survival as themselves (`5d70714c4`, adapted), TDD

1. **RED.** Edit `TAOM.Tests/Features/BattleBalance/TaomPartyHealingModelTests.cs` (Edit tool). Old:
   ```
       [TestMethod]
       public void ApplyCulturalSurvivalBonus_ZeroBonus_ReturnsVanillaUnchanged()
   ```
   New (the two new tests, then the same two lines):
   ```
       // An enlisted player rolls survival with his own party, as a freelancer does: vanilla reads Medicine
       // and Cheat Death off that party's effective surgeon, his assigned surgeon or else himself (v1.4.8
       // DefaultPartyHealingModel.cs:328-338, MobileParty.cs:834-844). The redirect to the commander's party
       // is gone (1.5.x 5d70714c4), so the model takes no Enlistment state.
       [TestMethod]
       public void SurvivalRoll_TakesNoEnlistmentState()
       {
           foreach (var ctor in typeof(TaomPartyHealingModel).GetConstructors())
               foreach (var p in ctor.GetParameters())
                   Assert.AreNotEqual(typeof(TAOM.Features.Enlistment.IEnlistmentStateQuery), p.ParameterType, p.Name);
       }

       [TestMethod]
       public void SurvivalRoll_NeverReadsTheCommander()
       {
           var src = TAOM.Tests.Infrastructure.RepoPaths.ReadSource(
               "Main/Features/BattleBalance/Models/TaomPartyHealingModel.cs", stripComments: true);
           Assert.IsFalse(src.Contains("CommanderHeroId"), "the survival roll must not look up the commander");
       }

       [TestMethod]
       public void ApplyCulturalSurvivalBonus_ZeroBonus_ReturnsVanillaUnchanged()
   ```
   **Verify**: Build exit 0; Filtered `FullyQualifiedName~TaomPartyHealingModelTests` shows `Failed: 2`
   (the two new names).
2. **GREEN.** In `Main/Features/BattleBalance/Models/TaomPartyHealingModel.cs` (Edit tool), old:
   ```
       private readonly ICareerPassiveService _careerPassives;
       private readonly TAOM.Features.Enlistment.IEnlistmentStateQuery _enlistment;

       public TaomPartyHealingModel(IBattleBalanceSettingsProvider settings,
           IBattleBalanceConfigProvider configProvider,
           ICareerPassiveService careerPassives,
           TAOM.Features.Enlistment.IEnlistmentStateQuery enlistment)
       {
           _settings = settings;
           _configProvider = configProvider;
           _careerPassives = careerPassives;
           _enlistment = enlistment;
       }
   ```
   new:
   ```
       private readonly ICareerPassiveService _careerPassives;

       public TaomPartyHealingModel(IBattleBalanceSettingsProvider settings,
           IBattleBalanceConfigProvider configProvider,
           ICareerPassiveService careerPassives)
       {
           _settings = settings;
           _configProvider = configProvider;
           _careerPassives = careerPassives;
       }
   ```
   Then:
   ```
   WT$ "$PY" $S/edit_lines.py cut Main/Features/BattleBalance/Models/TaomPartyHealingModel.cs "ENLISTED PLAYERS ARE ROLLED AGAINST THE COMPANY" "party = RedirectEnlistedPlayerToCommanderParty(party, character);" --and-blank --with "        // An enlisted player rolls with his own party, as a freelancer does: vanilla uses the party leader as" --with "        // surgeon when none is assigned (v1.4.8 DefaultPartyHealingModel.cs:328-338, MobileParty.cs:834-844)."
   WT$ "$PY" $S/edit_lines.py member Main/Features/BattleBalance/Models/TaomPartyHealingModel.cs "private PartyBase RedirectEnlistedPlayerToCommanderParty("
   ```
   In `Main/SubModule.cs` (Edit tool) replace
   `IoC.Resolve<ICareerPassiveService>(), IoC.Resolve<Features.Enlistment.IEnlistmentStateQuery>()));`
   with `IoC.Resolve<ICareerPassiveService>()));`.
   **Verify**: Build exit 0; Filtered `FullyQualifiedName~TaomPartyHealingModelTests` `Failed: 0`;
   `WT$ git grep -n "IEnlistmentStateQuery\|Redirect" -- Main/Features/BattleBalance` prints nothing;
   `WT$ git diff --stat -- Main/SubModule.cs` shows `1 insertion(+), 1 deletion(-)`.
3. **Docs.** `docs/features/battle-balance.md` (Edit tool): old `### Solution Approach`, new:
   ```
   An enlisted player rolls survival with his own party, exactly as a freelancer does. v1.4.8 uses the party leader as surgeon when none is assigned (`DefaultPartyHealingModel.cs:328-338`; `MobileParty.EffectiveSurgeon`, `MobileParty.cs:834-844`), so his own Medicine and Cheat Death already count. An older redirect that rolled him against his commander's party was removed in WP132 (port of 1.5.x `5d70714c4`); `SurvivalRoll_TakesNoEnlistmentState` pins it.

   ### Solution Approach
   ```
   and old `## Changelog`, new:
   ```
   ## Changelog

   - 2026-11-04: an enlisted player rolls survival with his own party; the redirect to the commander's party is removed (WP132, port of 1.5.x `5d70714c4`).
   ```
   `docs/reference/gamemodel-registry.md` (Edit tool): old
   `| \`TaomPartyHealingModel\` | \`DefaultPartyHealingModel\` | Cultural survival bonuses (JSON per-faction death chance multiplier) |`
   (the backslashes are not part of the text), new:
   ```
   | `TaomPartyHealingModel` | `DefaultPartyHealingModel` | Cultural survival bonuses (JSON per-faction death chance multiplier) and the career `TroopSurvival` factor in `GetSurvivalChance`; the career `HeroHealing` factor in `GetDailyHealingHpForHeroes`. An enlisted player rolls survival with his own party (no commander-party redirect since WP132) |
   ```
4. Stage `Main/Features/BattleBalance/Models/TaomPartyHealingModel.cs TAOM.Tests/Features/BattleBalance/TaomPartyHealingModelTests.cs Main/SubModule.cs docs/features/battle-balance.md docs/reference/gamemodel-registry.md`
   and commit `msg-A1.txt`:
   ```text
   fix(battle): v2.0.28 - enlisted players roll survival as themselves

   An enlisted player who goes down in his commander's battle now rolls
   survival exactly as a freelancer does, with his own party.

   The healing model rolled him against his commander's party, on the
   belief that his own party had no surgeon. The game uses the party
   leader as surgeon when none is assigned, so he already had his own
   Medicine and Cheat Death. The redirect swapped those for the
   commander's surgeon, which made a player with better Medicine or
   Cheat Death more likely to die, and it gave him the commander's
   culture and career survival bonuses instead of his own. The healing
   model no longer reads enlistment state.

   Research: taom-src v1.4.8 DefaultPartyHealingModel
     .AddSurgeonSurvivalBonus (:328-338, surgeon role holder or else the
     party leader) and MobileParty.EffectiveSurgeon (:834-844)
   Not-tested: an enlisted player falling in his commander's battle
   Ported-from: 5d70714c48c93ed7d93315536d433d9e9f433581
   ```

#### Step A2: The translator checks every placeholder (`113d3483b`, tools part), TDD

1. **RED.** `WT$ git diff 113d3483b^ 113d3483b -- tools/tests/test_translate_placeholders.py | git apply --index && "$PY" -m unittest discover -s tools/tests -t . -p "test_translate_placeholders.py" 2>&1 | tail -3`
   **Verify**: `FAILED (failures=3)` (dropped `{newline}`, mixed-case token, expression head).
2. **GREEN.** `WT$ git diff 113d3483b^ 113d3483b -- tools/translate_with_claude.py | git apply --3way; echo "exit=$?"; git diff --name-only --diff-filter=U`
   **Verify**: `exit=0`, no unmerged path; the placeholder test now ends `OK` (6 tests); Tool tests no new
   failure vs U0. If the 3-way apply fails: STOP (another WP changed the two regex lines).
3. Stage `tools/translate_with_claude.py tools/tests/test_translate_placeholders.py`; commit `msg-A2.txt`:
   ```text
   fix(tools): v2.0.28 - the translator checks every placeholder

   The translation tool's placeholder check now also sees lowercase and
   mixed-case engine tokens such as {newline}, and conditional heads
   written as expressions such as {?DAYS>1}. Before, a translation that
   dropped one of those passed the check and shipped broken text.
   Nothing in the game changes; the next translation run uses the
   stricter check.

   Rejected: the rest of the 1.5.x commit (a single-player gate for
     1.5.x-only features, the survival-roll split, prefab test helpers)
   Not-tested: a paid translation run (WP135 owns it)
   Ported-from: 113d3483bb1c6b303546e96c63e369eb2d920d5d
   ```

#### Step A3: The quartermaster says why he refuses (`1a132c727`, #829)

- **Method**: port routine, `git cherry-pick -n 1a132c727`.
- **Expected conflicts**: the 12 language files and `docs/features/enlistment.md`. The code, the
  test and the source rows in `taom_enlistment_strings.xml` apply cleanly
  (`EnlistmentQuartermasterBehavior.cs` on 1.4.x equals the commit's parent; no earlier WP edits it).
- **Drop**: `LANGFILES docs/features/enlistment.md` (A7 writes the doc).
- **Language rows**: `WT$ "$PY" $S/add_lang_rows.py taom_enlist_qm_refused_host "Only the host's quartermaster can issue kit in this campaign." taom_enlist_qm_refused_load "Your kit cannot be loaded into your baggage just now. Ask me again later." taom_enlist_qm_refused_stores "The quartermaster has nothing in his stores for your company."`
  (12 lines `+3 rows`), then `git add LANGFILES`.
- **Check**: `WT$ grep -c 'id="taom_enlist_qm_refused_\(host\|load\|stores\)"' Main/_Module/ModuleData/taom_enlistment_strings.xml` prints `3`.
- **Keep**: the new `docs/reviews/rca-quartermaster-answers-2026-10-19.md` (P132-7).
- **Tests**: `QuartermasterAnswerPolicyTests` (new, 12 methods; source pins through `RepoPaths.ReadSource`).
- **Verify**: Build exit 0; Filtered `FullyQualifiedName~QuartermasterAnswerPolicyTests|FullyQualifiedName~LanguageFileCoverageTests|FullyQualifiedName~UnregisteredLocalizationKeyBaselineTests` `Failed: 0`.
- **Paths**: `Main/Features/Enlistment/Equipment/QuartermasterAnswer.cs Main/Features/Enlistment/Equipment/QuartermasterAnswerPolicy.cs Main/Features/Enlistment/Hooks/EnlistmentQuartermasterBehavior.cs Main/_Module/ModuleData/taom_enlistment_strings.xml` + `LANGFILES` + `TAOM.Tests/Features/Enlistment/QuartermasterAnswerPolicyTests.cs docs/reviews/rca-quartermaster-answers-2026-10-19.md`
- **Message** (`msg-A3.txt`):
  ```text
  fix(enlistment): v2.0.28 - the quartermaster says why he refuses

  When an enlisted soldier asked for his service kit and did not get
  it, the answer was always "You have drawn your due for your rank
  already", whatever the real reason. The quartermaster now gives the
  true reason: already drawn (the old line), nothing in his stores for
  the soldier's company, or the kit cannot be loaded into the baggage
  just now. The three new lines show in English until the next
  translation run.

  Not-tested: each answer in a live conversation
  Ported-from: 1a132c727cf473f6010bd5fc29e36f5a99589c54
  ```

#### Step A4: Name the dialog gate for what it checks (`01f02c3a0`, #829)

- **Method**: port routine, `git cherry-pick -n 01f02c3a0`. **Expected**: clean (the RCA edit applies
  because A3 kept the file). Nothing to drop.
- **Verify**: `WT$ git grep -n "CanRequestDischargeFrom" -- Main TAOM.Tests` prints nothing;
  `WT$ git diff --ignore-cr-at-eol 01f02c3a0 -- Main/Features/Enlistment/EnlistmentDialogGateService.cs Main/Features/Enlistment/IEnlistmentDialogGateService.cs Main/Features/Enlistment/Hooks/EnlistmentAssignmentDialogBehavior.cs Main/Features/Enlistment/Hooks/EnlistmentQuartermasterBehavior.cs Main/Features/Enlistment/Hooks/EnlistmentReleaseDialogBehavior.cs Main/Features/Enlistment/Equipment/QuartermasterAnswer.cs Main/Features/Enlistment/Equipment/QuartermasterAnswerPolicy.cs`
  prints nothing (byte-identical to the 1.5.x commit); Build exit 0; Filtered
  `FullyQualifiedName~EnlistmentDialogGateServiceTests|FullyQualifiedName~QuartermasterAnswerPolicyTests` `Failed: 0`.
- **Paths**: the five `Main` files and `TAOM.Tests/Features/Enlistment/EnlistmentDialogGateServiceTests.cs` the pick staged, and `docs/reviews/rca-quartermaster-answers-2026-10-19.md`.
- **Message** (`msg-A4.txt`):
  ```text
  refactor(enlistment): v2.0.28 - name the dialog gate for what it checks

  IEnlistmentDialogGateService.CanRequestDischargeFrom is now
  IsServingUnder. It is true when the player serves under the
  conversation partner, and it gates the kit request, reassignment and
  release lines, not only discharge. Nothing a player sees changes.

  Ported-from: 01f02c3a0dd52746bc313c31b77bdb4ce58db9e3
  ```

#### Step A5: EnlistmentRank becomes EnlistmentKitTier (`5c5072dbc`, #936, scripted)

The pick would hit files WP128 deleted or edited (`InMemoryEquipmentIssueLedger.cs`, the moved ledger
tests); a whole-word rename reproduces the commit exactly on every file nobody else changed.

1. ```
   WT$ git mv Main/Features/Enlistment/Equipment/EnlistmentRank.cs Main/Features/Enlistment/Equipment/EnlistmentKitTier.cs && "$PY" $S/rename_ids.py EnlistmentRank=EnlistmentKitTier -- Main/Features/Enlistment Main/Adapters TAOM.Tests/Features/Enlistment
   ```
2. In `Main/Features/Enlistment/Equipment/EnlistmentKitTier.cs` (Edit tool) replace the line
   `/// Enlistment service ranks, ordered: numeric comparison IS the progression order` with:
   ```
   /// Kit tier key for the enlistment equipment rosters (four tiers), distinct from the
   /// career rank <c>ServiceRank</c>. Ordered: numeric comparison IS the progression order
   ```
3. **Verify**: `WT$ git grep -n -w "EnlistmentRank" -- Main TAOM.Tests` prints nothing;
   `WT$ git diff --ignore-cr-at-eol 5c5072dbc -- Main/Features/Enlistment/Equipment/EnlistmentKitTier.cs Main/Features/Enlistment/Equipment/EnlistmentEquipmentService.cs Main/Features/Enlistment/Equipment/EnlistmentRosterIds.cs Main/Features/Enlistment/Equipment/EnlistmentRosterResolver.cs Main/Features/Enlistment/Equipment/IEnlistmentEquipmentService.cs Main/Features/Enlistment/Equipment/PersistedEquipmentIssueLedger.cs Main/Features/Enlistment/Hooks/EnlistmentQuartermasterBehavior.cs`
   prints nothing; Build exit 0; ENL filter `Failed: 0`.
4. Stage `WT$ git add -- $(git diff --name-only -- Main/Features/Enlistment Main/Adapters TAOM.Tests/Features/Enlistment)`
   (the `git mv` is already staged); `git status --porcelain` shows one `R ` line and `M ` lines only.
   Commit `msg-A5.txt`:
   ```text
   refactor(enlistment): v2.0.28 - EnlistmentRank becomes EnlistmentKitTier

   The equipment rosters key on four kit tiers, and the career rank is a
   separate enum, ServiceRank, with the same four member names. The
   kit-tier enum now says what it is, so the two cannot be confused.
   Members, values and roster ids are unchanged. Nothing a player sees
   changes.

   Ported-from: 5c5072dbc2f5dc50189ddb04c3df663bc2b99839
   ```

#### Step A6: Kit-tier members say tier, not rank (`5a8c73d26`, #936, partial: P132-3)

1. ```
   WT$ "$PY" $S/rename_ids.py IssueForRank=IssueForTier HasIssuedForRank=HasIssuedForTier HighestIssuedRank=HighestIssuedTier RankToken=TierToken ProbeRanks=ProbeTiers AlreadyIssuedForRank=AlreadyIssuedForTier HighestIssuedEquipmentRank=HighestIssuedKitTier -- Main/Features/Enlistment Main/Adapters TAOM.Tests/Features/Enlistment
   ```
2. In `Main/Features/Enlistment/Content/Domain/ServiceContentRecord.cs` (Edit tool, three edits):
   - replace `    /// <summary>Highest equipment rank already drawn (monotonic), or null. -1 sentinel on the wire.</summary>`
     with `    /// <summary>Highest kit tier already drawn (monotonic; an <c>EnlistmentKitTier</c> ordinal, not a career rank), or null. Omitted from the serialized form when null; TryParse rejects negatives.</summary>`
     (skip this one edit if the old line is not there);
   - insert `        // The key stays "kitRank" for save compatibility.` directly above
     `        if (HighestIssuedKitTier.HasValue)`;
   - insert the same comment line directly above `        if (map.TryGetValue("kitRank", out var kitRank)`.
3. Create `TAOM.Tests/Features/Enlistment/KitTierSaveKeyTests.cs` (Write tool):
   ```csharp
   using Microsoft.VisualStudio.TestTools.UnitTesting;
   using TAOM.Features.Enlistment.Content.Domain;

   namespace TAOM.Tests.Features.Enlistment;

   /// <summary>
   /// #936: the kit-tier members say tier now, but the save key stays "kitRank". Renaming the key would
   /// wipe every save's kit ledger, so this pins the old name on the read and the write path
   /// (ServiceContentRecord.TryParse and Serialize). Moved here from the 1.5.x ServiceRankLadderTests,
   /// which belongs to the unported seven-rank ladder.
   /// </summary>
   [TestClass]
   public class KitTierSaveKeyTests
   {
       [TestMethod]
       public void ContentRecord_SaveKeyKitRank_IsFrozen_ReadAndWrittenUnderTheOldName()
       {
           Assert.IsTrue(ServiceContentRecord.TryParse("rank=1;kitRank=2;kitItems=a,b", out var parsed));
           Assert.AreEqual(2, parsed.HighestIssuedKitTier);

           var record = new ServiceContentRecord { HighestIssuedKitTier = 2 };
           StringAssert.Contains(record.Serialize(), "kitRank=2");
       }
   }
   ```
4. **Verify**: `WT$ git grep -n -w -E "IssueForRank|HasIssuedForRank|HighestIssuedRank|RankToken|ProbeRanks|AlreadyIssuedForRank|HighestIssuedEquipmentRank" -- Main TAOM.Tests`
   prints nothing; `WT$ git diff --ignore-cr-at-eol 5a8c73d26 -- Main/Features/Enlistment/Equipment/EquipmentIssueResult.cs Main/Adapters/IPartyItemRosterAdapter.cs`
   prints nothing; `WT$ grep -c '"kitRank' Main/Features/Enlistment/Content/Domain/ServiceContentRecord.cs` prints `2`;
   Build exit 0; ENL filter `Failed: 0` with one more test than A5.
5. Stage `WT$ git add -- TAOM.Tests/Features/Enlistment/KitTierSaveKeyTests.cs $(git diff --name-only -- Main/Features/Enlistment Main/Adapters TAOM.Tests/Features/Enlistment)`;
   commit `msg-A6.txt`:
   ```text
   refactor(enlistment): v2.0.28 - kit-tier members say tier, not rank

   IssueForRank, HasIssuedForRank, HighestIssuedRank, RankToken,
   ProbeRanks, AlreadyIssuedForRank and HighestIssuedEquipmentRank now
   say tier. The quartermaster log line now prints AlreadyIssuedForTier.
   Nothing a player sees changes.

   The save key stays "kitRank", and a new test pins it, so a later
   rename cannot wipe every save's kit ledger.

   Rejected: the parameter and comment renames of the 1.5.x commit (they
     sit on the seven-rank ladder, which is not ported)
   Save-compat: unchanged; the record still reads and writes "kitRank"
   Ported-from: 5a8c73d26018d0e795a1efeccb4cf1057061f518
   ```

#### Step A7: Feature doc for Part A, then the Part A gate

1. `docs/features/enlistment.md` (Edit tool): old `## Interactions with the rest of TAOM` (it follows
   WP128's port section), new = the block below, a blank line, then that same heading:
   ```markdown
   ## Fixes ported from the 1.5.x line (port148 WP132)

   These fixes reached the 1.4.x line in WP132 of the 2026-11-04 port programme, after WP128's.
   Engine facts were re-checked on v1.4.8. The `docs/reviews/rca-*` files named below describe the
   1.5.x review and cite v1.5.5 lines.

   ### The quartermaster says why he refuses (#829)

   `QuartermasterAnswerPolicy.For(EquipmentIssueResult)` picks the reply to a kit request: issued,
   already drawn for this tier, empty stores (no roster or no valid item) or cannot load (party
   unavailable, and the default for any result added later). On a co-op client the behavior's
   authority branch answers "Only the host's quartermaster can issue kit" without reaching the
   issuer. Each conditioned answer is its own dialog line at priority 112; the cannot-load line has
   no condition and sits at 100, below them, so it also answers anything without a line of its own.
   Before #829 the refusal line had no condition at 111, so every refusal read "already drawn". The
   kit, reassignment and release lines are gated by `IEnlistmentDialogGateService.IsServingUnder`
   (renamed from `CanRequestDischargeFrom`). `QuartermasterAnswerPolicyTests` pins the mapping and the
   wiring. Review: `rca-quartermaster-answers-2026-10-19.md`.

   ### Kit tiers are not ranks (#936)

   The equipment rosters key on `EnlistmentKitTier` (renamed from `EnlistmentRank`; same four members,
   values and roster ids), distinct from the career rank `ServiceRank`. The issuer and ledger members
   say tier: `IssueForTier`, `HasIssuedForTier`, `HighestIssuedTier`, `TierToken`,
   `AlreadyIssuedForTier`, `ServiceContentRecord.HighestIssuedKitTier`. The save key stays `kitRank`,
   pinned by `KitTierSaveKeyTests`, so old saves keep their kit ledger. The 1.5.x seven-rank ladder
   that prompted the rename is not ported.
   ```
2. **Verify**: Docs shows `ai_dashes: 0`, and `dead_links`, `orphan_features` equal L0.
3. Commit `docs/features/enlistment.md` with `msg-A7.txt`:
   ```text
   docs(enlistment): v2.0.28 - quartermaster answers and kit-tier names

   The enlistment feature doc records the quartermaster's answers (#829)
   and the kit-tier renames with their frozen save key (#936).

   Ported-from: 1a132c727cf473f6010bd5fc29e36f5a99589c54
   Ported-from: 5c5072dbc2f5dc50189ddb04c3df663bc2b99839
   Ported-from: 5a8c73d26018d0e795a1efeccb4cf1057061f518
   ```
4. **Part A gate**: Build exit 0; Test `Failed: 0` (record **TA**); Tool tests no new failure vs U0.

### Part B: sit-out rule, board pay, pump clock

#### Step B1: Soldiers fight their commander's battles (`1d9cc42f1`, #830)

- **Method**: port routine, `git cherry-pick -n 1d9cc42f1`.
- **Expected conflicts**: `LANGFILES`, `docs/features/enlistment.md`,
  `docs/reference/harmony-patch-registry.md`, `docs/reference/taleworlds-api-snapshot/patch-targets.md`,
  `docs/reviews/lessons/campaign-mechanics.md`, `TAOM.Tests/Features/Enlistment/EnlistmentContentConfigProviderTests.cs`
  (1.5.x put the sit-out tests next to seven-rank tests). Because WP128 hand-ported later 1.5.x commits
  first, these may conflict too and are resolved below: `EnlistmentBattlePayoutService.cs`,
  `EnlistmentBattlePayoutServiceTests.cs`, `EnlistmentMenuServiceTests.cs`, `EncounterAdapter.cs`,
  `IEncounterAdapter.cs`, `EnlistmentIoC.cs`.
- **Drop**: `LANGFILES docs/features/enlistment.md docs/reference/harmony-patch-registry.md docs/reference/taleworlds-api-snapshot/patch-targets.md docs/reviews/lessons/campaign-mechanics.md TAOM.Tests/Features/Enlistment/EnlistmentContentConfigProviderTests.cs TAOM.Tests/Features/Enlistment/EnlistmentBattlePayoutServiceTests.cs`
  (the last two are rebuilt below).
- **Resolve and rebuild**:
  1. Config tests: `WT$ "$PY" $S/copy_tests.py 1d9cc42f1 TAOM.Tests/Features/Enlistment/EnlistmentContentConfigProviderTests.cs SitOutDefaults_AreTheApprovedValues ShippedConfig_SitOut_EqualsTheCompiledDefaults GetConfig_NoSitOutSection_UsesTheDefaults GetConfig_SitOutValidValues_AreKept GetConfig_SitOutShareNotFiniteOrOutOfRange_RevertsWithWarning GetConfig_SitOutShareExactlyOne_IsKept GetConfig_SitOutNegativeTrustCost_Reverts GetConfig_SitOutZeroTrustCost_IsKept GetConfig_SitOutBaseXpFactorNotFiniteOrOutOfRange_RevertsWithWarning GetConfig_SitOutBaseXpFactorAtTheEnds_IsKept`
     prints `copied 10 method(s)` (the two seven-rank tests beside them stay behind).
  2. Payout tests, setup (Edit tool on `TAOM.Tests/Features/Enlistment/EnlistmentBattlePayoutServiceTests.cs`). Old:
     ```
             _service = new EnlistmentBattlePayoutService(
                 _store, _contentStore, _config, _accumulator, _rewards, promotion, _logger);
         }
     ```
     New:
     ```
             _ledger = new SitOutLedger();
             _service = new EnlistmentBattlePayoutService(
                 _store, _contentStore, _config, _accumulator, _rewards, promotion, _ledger, _logger);
         }

         private SitOutLedger _ledger = null!;

         private static bool PaysRenown(RewardSpec r) => r.Renown > 0;

         private static MeritSample Fought() => new MeritSample { Kills = 0, SurvivalRatio = 1f };
     ```
  3. Payout tests, the 14 new methods and the two-argument calls:
     ```
     WT$ "$PY" $S/copy_tests.py 1d9cc42f1 TAOM.Tests/Features/Enlistment/EnlistmentBattlePayoutServiceTests.cs PayOut_SimulatedNoSampleOverwhelming_HalfXpNoRenownOneTrustCost PayOut_SimulatedNoSampleWoundedOrForcedOpen_FullXpNoRenownNoTrustCost PayOut_SimulatedNoSampleButNoAllowedVerdict_IsPaidNormallyWithRenown PayOut_SimulatedLossOverwhelming_HalvesTheLossXpToo PayOut_SimulatedOverwhelmingHalfPoint_RoundsAwayFromZeroNotToEven PayOut_SimulatedWithAFoughtSample_IsPaidAsNormal PayOut_WalkoutNeverDownedWithOverwhelmingVerdict_IsSatOutButKeepsBandPay PayOut_WalkoutAfterBeingDowned_IsNotSatOut PayOut_NotSimulatedNoSample_IsPaidAsNormal PayOut_AnyOutcome_ConsumesTheLedger PayOut_NotEnlisted_StillConsumesTheLedger PayOut_NextBattle_ResetsTheSatOutReason PayOut_ZeroXpFactorAndTrustCost_PaysNoXpAndNoTrust
     WT$ sed -i -E 's/PayOutBattle\(won: (true|false)\)/PayOutBattle(won: \1, playerSimulated: false)/g' TAOM.Tests/Features/Enlistment/EnlistmentBattlePayoutServiceTests.cs
     ```
     The first prints `copied 13 method(s)`. Count the names: there are 13 (1.5.x added 14 entries,
     one of them the helper pair above). If KBV was `0`, also run
     `WT$ sed -i -E '/_contentStore\.Record\.Battle(Victories|Defeats)/d' TAOM.Tests/Features/Enlistment/EnlistmentBattlePayoutServiceTests.cs`
     (the counters went with WP128 C3; 1.5.x `218fa4053` dropped the same two asserts). These steps
     reproduce `1d9cc42f1`'s whole test diff: it changes nothing else in that file.
  4. `EnlistmentBattlePayoutService.cs`, if conflicted: the region between `var tables = config.Progression;`
     and `var bandRenown = 0;` must read exactly as below (WP128 C3 removed the win and loss counters;
     keep WP128's grace guard before `_promotion.EvaluateAndApply()` and everything else of ours):
     ```
             var sample = _accumulator.Consume();

             // A simulated player battle (he chose 'Send troops') is a battle he sat out when the menu gate
             // recorded an allowed verdict and the mission never ran for him: no sample, or a walkout from
             // a mission he was never downed in. A recorded Unchanged or Refused verdict means the
             // simulation did not come from an option the gate let him use, so it pays as normal.
             // Renown is withheld for every allowed reason. Only the healthy allowance (OverwhelmingCost)
             // also cuts the base XP and docks trust; the wounded and the forced-open keep the full base XP.
             var allowed = recorded == SitOutVerdict.WoundedFree
                 || recorded == SitOutVerdict.ForcedOpen
                 || recorded == SitOutVerdict.OverwhelmingCost;
             var satOut = playerSimulated && allowed
                 && (sample == null || (sample.LeftTheField && sample.SurvivalRatio >= 1f));
             var baseXp = won ? tables.BattleWinXp : tables.BattleLossXp;
             if (satOut)
             {
                 LastSatOutReason = recorded;
                 if (recorded == SitOutVerdict.OverwhelmingCost)
                 {
                     baseXp = (int)System.Math.Round(baseXp * config.SitOut.BaseXpFactor, System.MidpointRounding.AwayFromZero);
                     if (config.SitOut.TrustCost > 0)
                         _rewards.Grant(new RewardSpec { Trust = -config.SitOut.TrustCost }, "sat-out");
                 }
                 _logger?.LogInfo($"[Enlistment] sat out a simulated battle ({recorded}, won={won}); base XP {baseXp}");
             }
             record.ServiceXp += baseXp;

     ```
     (Then `var bandRenown = 0;` and an `if (sample != null)` block that no longer declares `sample`.)
     Also check `WT$ grep -c "renown > 0 && !satOut" Main/Features/Enlistment/Content/EnlistmentBattlePayoutService.cs` prints `1`.
  5. `EnlistmentMenuServiceTests.cs`, if conflicted (WP128 C3 changed the same constructor line):
     `WT$ "$PY" $S/edit_lines.py blocks TAOM.Tests/Features/Enlistment/EnlistmentMenuServiceTests.cs theirs --expect SitOutLedger`.
  6. `EncounterAdapter.cs`, `IEncounterAdapter.cs` or `EnlistmentIoC.cs`, if conflicted (both sides added
     members or registrations at one spot): `WT$ "$PY" $S/edit_lines.py blocks <file> union`, then read
     the result: each member or registration must appear once. Any other conflict in a `Main/` or
     `TAOM.Tests/` path: STOP.
  7. In every staged test file: `WT$ sed -i -E 's/new EnlistmentConfigProvider\([A-Za-z_]+\)/new EnlistmentConfigProvider()/g' $(git diff --cached --name-only -- TAOM.Tests)`
     (WP128 C3 removed that constructor's logger parameter); `WT$ git grep -n "new EnlistmentConfigProvider([A-Za-z_]" -- TAOM.Tests Main` prints nothing.
- **Fix-ups**:
  - `WT$ git grep -n -E "PayOutBattle\(won: (true|false)\)" -- TAOM.Tests` prints nothing (this catches
    WP128's `PayOut_CommanderUnavailable_EvaluatesNoPromotion`, converted by the sed above).
  - `TAOM.Tests/Features/Enlistment/Patch66EnlistmentBindingTests.cs` (Edit tool): replace
    `(informational pin from the v1.5.5 verification).` with
    `(informational pin, verified on v1.4.8: EncounterGameMenuBehavior.cs:1919).`
  - **Language rows**: `WT$ "$PY" $S/add_lang_rows.py taom_enlist_sitout_wounded_tip "You are wounded. The column fights on without you, and no one will hold it against you." taom_enlist_sitout_odds_tip "Your side holds the field by a wide margin. You may stay back, but your commander will remember it." taom_enlist_sitout_refused_tip "You serve in your commander's line. Take your place in the fight." taom_enlist_sitout_siege_tip "A siege needs every soldier. Take your place in the assault." taom_enlist_sat_out_line "You stayed back while the column fought, and your share of the battle's due was cut." taom_enlist_sat_out_wounded_line "You were wounded and stayed back while the column fought." taom_enlist_sat_out_forced_line "The column fought without you, and you had no way into the line."`
    (12 lines `+7 rows`); the seven source rows in `taom_enlistment_strings.xml` come with the pick
    (`WT$ grep -c 'taom_enlist_sitout_\|taom_enlist_sat_out_' Main/_Module/ModuleData/taom_enlistment_strings.xml` prints `7`).
  - **API snapshot**: in `docs/reference/taleworlds-api-snapshot/patch-targets.md` (Edit tool) insert,
    directly after the row for `TAOM.Features.Enlistment.Hooks.BehaviorComponent_OnBehaviorActivated_Transpiler`, the row
    ```
    | `TAOM.Features.Enlistment.Hooks.EncounterGameMenuOrderAttackConditionPatch` | `TaleWorlds.CampaignSystem.CampaignBehaviors.EncounterGameMenuBehavior.Boolean game_menu_encounter_order_attack_on_condition(MenuCallbackArgs args)` |
    ```
    and raise the number in the `Patches: N.` header line by 1 (read it with `grep -n "^Patches: "`).
  - **Registry**: in `docs/reference/harmony-patch-registry.md` (Edit tool) old `## Patch67_TableauResidencyDiag`,
    new = this paragraph, a blank line, then that heading (D7: the 1.5.x co-op sentences are not copied):
    ```
    WP132 adds `EncounterGameMenuBehavior.game_menu_encounter_order_attack_on_condition(MenuCallbackArgs)` (private instance, bool, Postfix `MenuCallbackArgs args, ref bool __result`, only narrows) to this category: `EncounterGameMenuOrderAttackConditionPatch` (#830) stops an enlisted soldier from auto-resolving his commander's battle through 'Send troops' or 'Leave it to the others' and being paid for it. It returns at once unless vanilla showed the option and left it enabled, asks `IEnlistmentMenuService.EvaluateSitOut()`, and writes only `args.IsEnabled = false` and `args.Tooltip`; it never sets the result to false, never re-enables an option and adds none, and fails open with one log line. The pure `SitOutPolicy` disables the option only while vanilla's Attack is available. `IEncounterAdapter.GetSitOutSnapshot` probes Attack with `MenuHelper.CheckEnemyAttackableHonorably` then `EncounterAttackCondition` on a throwaway `MenuCallbackArgs` (v1.4.8 `game_menu_encounter_attack_on_condition` calls exactly those two, pinned by IL scan) and reads the strength share with the read-only `MapEvent.GetStrengthsRelativeToParty`; vanilla's `EncounterOrderAttackCondition` is never called because it writes `IsInvulnerable` and `SEND_TROOPS_TEXT`. The payout reads `MapEvent.IsPlayerSimulation` at `MapEventEnded`; `Patch66EnlistmentBindingTests` pins by IL scan of TaleWorlds.CampaignSystem only that `PlayerEncounter.InitSimulation` is called only from `MenuHelper.EncounterOrderAttack`, the `BattleSimulation` constructor only from `InitSimulation`, and the `IsPlayerSimulation` setter only from that constructor. Docs: `docs/features/enlistment.md`.
    ```
- **Keep**: everything else the pick staged, including the new `docs/reviews/rca-enlistment-sit-out-2026-10-19.md`.
- **Tests that come with it**: `EncounterGameMenuOrderAttackConditionPatchTests` (new, 14, `RequiresGame`),
  `SitOutPolicyTests` (new, 15), `ServiceDailyAnnouncerSatOutTests` (new, 4), `Patch66EnlistmentBindingTests`
  (+6 `BindingVerification`, three of them IL scans), `EnlistmentMenuServiceTests` (+8), payout (+13),
  config provider (+10), `ServiceMaintenanceServiceTests`, `EnlistmentPumpAuthorityTests`.
- **Verify**: Build exit 0; ENL filter `Failed: 0`; Filtered `FullyQualifiedName~Patch66EnlistmentBindingTests|FullyQualifiedName~HarmonyPatchBindingTests|FullyQualifiedName~EnlistmentContainerWiringTests`
  `Failed: 0`; Snapshot exit 0 when S0 was 0 (when S0 was 1, report it and go on); Data no new ERROR.
- **Paths**: `Main/Adapters/EncounterAdapter.cs Main/Adapters/IEncounterAdapter.cs Main/Adapters/SitOutSnapshot.cs Main/Features/Enlistment/Content/Domain/ContentConfigs.cs Main/Features/Enlistment/Content/Domain/MeritConfig.cs Main/Features/Enlistment/Content/EnlistmentBattlePayoutService.cs Main/Features/Enlistment/Content/EnlistmentContentConfigProvider.cs Main/Features/Enlistment/EnlistmentIoC.cs Main/Features/Enlistment/EnlistmentMenuService.cs Main/Features/Enlistment/Hooks/EncounterGameMenuOrderAttackConditionPatch.cs Main/Features/Enlistment/Hooks/EnlistmentContentBehavior.cs Main/Features/Enlistment/IEnlistmentMenuService.cs Main/Features/Enlistment/Presentation/ServiceDailyAnnouncer.cs Main/Features/Enlistment/ServiceMaintenanceService.cs Main/Features/Enlistment/SitOutLedger.cs Main/Features/Enlistment/SitOutPolicy.cs Main/_Module/ModuleData/enlistment/enlistment_config.json Main/_Module/ModuleData/taom_enlistment_strings.xml`
  + `LANGFILES` + under `TAOM.Tests/Features/Enlistment/`: `EncounterGameMenuOrderAttackConditionPatchTests.cs EnlistmentBattlePayoutServiceTests.cs EnlistmentContentConfigProviderTests.cs EnlistmentMenuServiceTests.cs EnlistmentPumpAuthorityTests.cs Patch66EnlistmentBindingTests.cs ServiceDailyAnnouncerSatOutTests.cs ServiceMaintenanceServiceTests.cs SitOutPolicyTests.cs`
  + `docs/reference/harmony-patch-registry.md docs/reference/taleworlds-api-snapshot/patch-targets.md docs/reviews/rca-enlistment-sit-out-2026-10-19.md`
- **Message** (`msg-B1.txt`):
  ```text
  fix(enlistment): v2.0.28 - soldiers fight their commander's battles

  An enlisted soldier could let his commander's battle resolve without
  him by choosing "Leave it to the others" (or "Send troops") and still
  be paid as if he had fought. That option now follows his place in the
  line:
  - wounded: it stays open and costs nothing;
  - when Attack is impossible: it stays open, so he always has a way
    forward;
  - in a siege assault or a sally-out: it is closed;
  - when his side holds 85 percent of the strength on the field: it is
    open, but it costs a little of his commander's trust;
  - otherwise: it is closed, with the reason in the tooltip.

  The rule only narrows the option: anything the game already closed
  stays closed, and a soldier who leads his side keeps the normal
  choice. A battle he stayed out of pays no renown; at overwhelming
  odds it also pays half the base battle XP and costs one point of
  trust. A line says when he stayed back. The new lines show in
  English until the next translation run.

  Research: taom-src v1.4.8 EncounterGameMenuBehavior
    .game_menu_encounter_order_attack_on_condition (:1919, private bool),
    MenuHelper.EncounterAttackCondition (:182) and
    CheckEnemyAttackableHonorably (:452), MapEvent
    .GetStrengthsRelativeToParty (:1456, read-only), MapEvent
    .IsPlayerSimulation (:242), set only by the BattleSimulation
    constructor (dump BattleSimulation.cs:60)
  Not-tested: the option in a live encounter menu, and the payout after
    a simulated battle
  Ported-from: 1d9cc42f1bf13a49a9c340182cc77dc9ec44817c
  ```

#### Step B2: The board's daily pay is what service pays today (`4666a254a`, partial: P132-2), TDD

1. **RED.** In `TAOM.Tests/Features/Enlistment/ServiceStatusServiceTests.cs` (Edit tool, four edits):
   - after the line `    private IEnlistmentContentConfigProvider _config;` insert
     `    private IEnlistmentWagePreview _wagePreview;`
   - after the line `        _config.GetConfig().Returns(EnlistmentContentConfigProvider.BuildDefaults());` insert:
     ```
             // What service pays today: the rank's table wage, as ServiceRewardService.GetDailyWage answers it.
             _wagePreview = Substitute.For<IEnlistmentWagePreview>();
             _wagePreview.GetDailyWage().Returns(_ => WagePolicy.DailyWageForRank(
                 (int)_content.Record.Rank, EnlistmentContentConfigProvider.BuildDefaults().Progression.DailyWageByRank));
     ```
   - in the `_sut = new ServiceStatusService(...)` line replace `_promotion, _config);` with
     `_promotion, _config, _wagePreview);`
   - directly above `    public void Build_FreshRecruit_NamesTheNextRankAndItsBindingRequirement()` and its
     `[TestMethod]` line, insert:
     ```
         [TestMethod]
         public void Build_DailyWage_IsWhatServicePaysToday_NotTheRanksTableWage()
         {
             _content.Record.Rank = ServiceRank.Sergeant;
             _wagePreview.GetDailyWage().Returns(0);

             Assert.AreEqual(0, _sut.Build().DailyWage, "while the column is lost nothing is paid, and the board must not promise it");
         }

     ```
   **Verify (RED)**: Build fails with `CS1729` in `ServiceStatusServiceTests.cs` only.
2. **GREEN.** In `Main/Features/Enlistment/ServiceStatusService.cs` (Edit tool):
   - after `    private readonly IEnlistmentContentConfigProvider _config;` insert
     `    private readonly IEnlistmentWagePreview _wagePreview;`
   - replace `        IEnlistmentContentConfigProvider config)` with
     `        IEnlistmentContentConfigProvider config,` + newline + `        IEnlistmentWagePreview wagePreview)`
   - replace `        _config = config;` with `        _config = config;` + newline + `        _wagePreview = wagePreview;`
   - replace `            dailyWage: DailyWageFor(content.Rank),` with
     ```
                 // What service pays today, so 0 while the column is lost: the board must not promise unpaid wages.
                 dailyWage: _wagePreview.GetDailyWage(),
     ```
   - `WT$ "$PY" $S/edit_lines.py member Main/Features/Enlistment/ServiceStatusService.cs "private int DailyWageFor("`
   - `WT$ grep -c "_config" Main/Features/Enlistment/ServiceStatusService.cs`: when it prints `2` (only the
     field and the assignment are left), also delete the field line, the assignment line and the
     `IEnlistmentContentConfigProvider config,` parameter line; with WP128 C4 merged it prints more
     (`ActiveDutyTitle` reads `_config`) and they stay.
   In `Main/Features/Enlistment/Content/IEnlistmentWagePreview.cs` (Edit tool) replace
   `    /// Nominal daily wage for the player's current rank, or 0 when not enlisted.` with
   `    /// The rank's daily wage when a wage is due today, otherwise 0: not enlisted, or the column is lost (CommanderUnavailable).`
   **Verify**: Build exit 0; Filtered `FullyQualifiedName~ServiceStatusServiceTests|FullyQualifiedName~EnlistmentContainerWiringTests`
   `Failed: 0` (the two existing wage tests still read 5 and 14 through the substitute).
3. Stage the three files; commit `msg-B2.txt`:
   ```text
   feat(enlistment): v2.0.28 - the board's daily pay is what service pays

   While the commander was missing and no wage was paid (the grace
   state that waits for him to return), the service board still showed
   the rank's usual wage as "Daily pay". It now shows what service pays
   today, which is 0 in that state, the same number the wallet tooltip
   already used.

   Rejected: the retinue-cost line on the oath and the board from the
     same 1.5.x commit (a new feature; plan 132 decision P132-2)
   Not-tested: the board during the grace state, in game
   Ported-from: 4666a254a1a0f4c9020110e4c6ff3a3216628ef4
   ```

#### Step B3: The service upkeep runs once a frame (`069962aa4`, #841)

- **Method**: port routine, `git cherry-pick -n 069962aa4`.
- **Expected conflicts**: `Main/Features/Enlistment/ServiceMaintenanceService.cs` (one block in
  `ResetSessionCaches`), `TAOM.Tests/Features/Enlistment/EnlistmentHooksLineCountTests.cs` (absent on
  1.4.x: modify/delete), `docs/features/enlistment.md`, `docs/reviews/lessons/campaign-mechanics.md`.
  Everything else applies (`CommanderLordAdapter.cs` has WP128's `partyIsRaiding:` context;
  `EnlistmentServiceMenuWiringTests.cs` exists since WP128).
- **Drop**: `TAOM.Tests/Features/Enlistment/EnlistmentHooksLineCountTests.cs docs/features/enlistment.md docs/reviews/lessons/campaign-mechanics.md`.
- **Resolve** `ServiceMaintenanceService.cs`: the block sits after `_sitOutLedger?.Consume();`. Keep
  ours (`_meritAccumulator?.Consume();`) and add the pending-join reset above it; do not take
  `_meritAccumulator?.ResetForNewSession();` or `_renewal?.ResetForNewSession();` (1.5.x only). The
  result reads:
  ```
          _sitOutLedger?.Consume();
          _pendingRaidJoinHeroId = null;   // a raid join waiting from the previous world
          _meritAccumulator?.Consume();
  ```
  Then `git add` it. `WT$ grep -c "_renewal\|ContractRenewal" Main/Features/Enlistment/ServiceMaintenanceService.cs` prints `0`.
- **Keep**: `docs/reviews/rca-enlistment-trace-and-pump-clock-2026-10-24.md` (new; it also records the
  trace default that WP123 ports).
- **Tests**: `ServiceMaintenanceServiceTests` (+13, `OnWaitMenuFrame`, `RequestBattleJoin`, the deferred
  raid join), `EnlistmentServiceMenuWiringTests` (+2 source pins).
- **Verify**: Build exit 0; ENL filter `Failed: 0`;
  `WT$ grep -c "_maintenance\." Main/Features/Enlistment/Hooks/EnlistmentMenuBehavior.cs` prints `1`;
  `WT$ git grep -n "MaxWaitMenuStepSeconds" -- Main` prints nothing.
- **Paths**: `Main/Adapters/CommanderLordAdapter.cs Main/Adapters/CommanderTickSnapshot.cs Main/Features/Enlistment/Hooks/EnlistmentBattleBehavior.cs Main/Features/Enlistment/Hooks/EnlistmentMaintenanceBehavior.cs Main/Features/Enlistment/Hooks/EnlistmentMenuBehavior.cs Main/Features/Enlistment/IEnlistmentReconciler.cs Main/Features/Enlistment/IServiceMaintenanceService.cs Main/Features/Enlistment/ServiceMaintenanceService.cs TAOM.Tests/Features/Enlistment/EnlistmentServiceMenuWiringTests.cs TAOM.Tests/Features/Enlistment/ServiceMaintenanceServiceTests.cs docs/reviews/rca-enlistment-trace-and-pump-clock-2026-10-24.md`
- **Message** (`msg-B3.txt`):
  ```text
  fix(enlistment): v2.0.28 - the service upkeep runs once a frame

  While an enlisted soldier waits in his service menu, a short upkeep
  pass keeps him beside his commander's column and refreshes the status
  board. Two clocks fed that pass: the campaign's own tick and the wait
  menu's tick. With time running both fired in the same frame, so the
  upkeep ran twice as often as intended. The wait menu now feeds it
  only while time is stopped, the one case the campaign tick does not
  cover (a paused game, or a save loading onto the menu).

  When the commander fights off a raid on a village and the soldier
  joins from the running service menu, the join now waits for the next
  frame of that menu, where the game does not advance the raid first,
  so his troops no longer take a round of casualties before he sees the
  battle. Field battles, sieges and joins made away from the service
  menu happen at once, as before.

  Research: v1.4.8 dump MapState.OnTick (:156-160, the wait menu ticks
    before the campaign), GameMenu.RunOnTick (:224-232, campaign time
    since its last frame), Campaign.Tick (:958-962), MapEventManager
    .Tick (:59-61, a raid updates even as the player's own event),
    GameMenu.SwitchToMenu and ActivateGameMenu stop time (:358-360,
    :335-337)
  Not-tested: the board cadence and a raid relief joined from the
    running service menu, in game
  Ported-from: 069962aa43773a2201f8ec358f4c0c9b40f046e9
  ```

#### Step B4: Feature doc for Part B, then the Part B gate

1. In `docs/features/enlistment.md` (Edit tool): replace `All seven targets pinned by` with
   `All eight targets pinned by` (skip if that phrase is absent). Then insert the block below directly
   above the line `## Interactions with the rest of TAOM` (old = that heading, new = block, blank line,
   heading):
   ```markdown
   ### Soldiers fight their commander's battles (#830)

   A `Patch66_Enlistment` postfix on the encounter menu's private
   `EncounterGameMenuBehavior.game_menu_encounter_order_attack_on_condition` (v1.4.8 `:1919`, the
   "Send troops" or "Leave it to the others" option) asks `IEnlistmentMenuService.EvaluateSitOut()`
   while the soldier is in `EnlistedBattle` and does not lead his side. The pure `SitOutPolicy`
   decides: wounded stays free; an unavailable Attack keeps the option open; a siege assault or
   sally-out refuses; any other battle refuses unless his side holds at least
   `sitOut.overwhelmingShare` (0.85) of the strength on the field, which allows it at a cost. The
   postfix only narrows: it never hides the option, never re-enables one vanilla disabled, and every
   refusal leaves Attack enabled. `IEncounterAdapter.GetSitOutSnapshot` probes vanilla's Attack with
   `MenuHelper.CheckEnemyAttackableHonorably` then `EncounterAttackCondition` on a throwaway
   `MenuCallbackArgs` (both write only to the args, `MenuHelper.cs:452`, `:182`) and reads the share
   with the read-only `MapEvent.GetStrengthsRelativeToParty` (`MapEvent.cs:1456`). The verdict goes
   to `SitOutLedger`. At `MapEventEnded` the payout reads `MapEvent.IsPlayerSimulation`, which on
   v1.4.8 only the `BattleSimulation` constructor sets, and only `PlayerEncounter.InitSimulation`
   builds one, called only from `MenuHelper` (`MenuHelper.cs:576`). A battle sat out with an allowed
   verdict pays no renown; at overwhelming odds it also pays `sitOut.baseXpFactor` (half) of the base
   XP and costs `sitOut.trustCost` (1) trust. `Patch66EnlistmentBindingTests` pins the target and, by
   IL scan of TaleWorlds.CampaignSystem, the setter and constructor caller sets. Review:
   `rca-enlistment-sit-out-2026-10-19.md`.

   ### The board's daily pay is what service pays today (#839, partial)

   `ServiceStatusService` reads the board's daily pay from `IEnlistmentWagePreview.GetDailyWage()`
   (`ServiceRewardService`), which is 0 while the column is lost (`CommanderUnavailable`), the number
   the clan finance tooltip already uses. The 1.5.x retinue-cost line on the oath and the board is
   not ported.

   ### The service upkeep runs once a frame (#841)

   Two clocks feed `ServiceMaintenanceService.Pump`: the campaign `TickEvent` and the service wait
   menu's tick. v1.4.8 ticks a wait menu before the campaign in the same frame (`MapState.OnTick`,
   `MapState.cs:156-160`) and hands it the campaign time since its previous frame
   (`GameMenu.RunOnTick`, `GameMenu.cs:224-232`). The wait menu now feeds the pump only when that
   delta is zero, which means time is stopped (`OnWaitMenuFrame`), so a running menu is no longer
   pumped twice per frame. While time runs the budget is the campaign tick's delta (0.25 is 18
   campaign minutes: `Campaign.cs:873-874` advances 4320 campaign seconds per delta unit); on a
   stopped menu it is real seconds. A raid join raised from the campaign tick while the service wait
   menu is open waits for the next wait-menu frame (`RequestJoin`, `FlushPendingRaidJoin`), because
   `Campaign.Tick` runs `MapEventManager.Tick` right after the tick dispatch (`Campaign.cs:958-962`)
   and that updates a raid even when it is the player's own event (`MapEventManager.cs:59-61`), while
   a join from the wait-menu frame switches menus, which stops time (`GameMenu.cs:335-337`,
   `:358-360`) before the campaign tick runs. Field battles, sieges and joins away from the service
   menu happen at once. The hourly reconciler's join request takes the same path
   (`IServiceMaintenanceService.RequestBattleJoin`). The "within 250 ms" and "real-time service pump"
   wording earlier in this doc predates #841. Review:
   `rca-enlistment-trace-and-pump-clock-2026-10-24.md`.
   ```
2. **Verify**: Docs as in A7. Commit `docs/features/enlistment.md` with `msg-B4.txt`:
   ```text
   docs(enlistment): v2.0.28 - sit-out, board pay and pump clock

   The enlistment feature doc records the sit-out rule (#830), the
   board's daily pay (#839) and the service pump clock (#841).

   Ported-from: 1d9cc42f1bf13a49a9c340182cc77dc9ec44817c
   Ported-from: 4666a254a1a0f4c9020110e4c6ff3a3216628ef4
   Ported-from: 069962aa43773a2201f8ec358f4c0c9b40f046e9
   ```
3. **Part B gate**: Build exit 0; Test `Failed: 0` (record **TB**); Data no new ERROR; Snapshot exit 0
   when S0 was 0.

### Part C: one fitness check, siege duties and wage order, the dead battle rule

#### Step C1: One fitness check for a commander (`5e7a0da6e`, partial: P132-8), TDD

1. **RED.** `WT$ git show 5e7a0da6e:TAOM.Tests/Adapters/CommanderSnapshotIsFitTests.cs > TAOM.Tests/Adapters/CommanderSnapshotIsFitTests.cs`
   **Verify (RED)**: Build fails with `CS1061` (`CommanderSnapshot` has no `IsFit`) in that file only.
2. **GREEN.** In `Main/Adapters/CommanderSnapshot.cs` (Edit tool) replace
   `    public bool HasParty => !string.IsNullOrEmpty(PartyId);` with:
   ```
       public bool HasParty => !string.IsNullOrEmpty(PartyId);

       /// <summary>Alive, free, and belonging to an active party: the one fitness test for a commander an enlisted soldier can serve under.</summary>
       public bool IsFit => Exists && IsAlive && !IsPrisoner && HasParty && PartyIsActive;
   ```
   **Verify**: Build exit 0; Filtered `FullyQualifiedName~CommanderSnapshotIsFitTests` `Failed: 0` (8 cases).
3. **REFACTOR** (Edit tool; each old text must match once):
   - `Main/Features/DevConsole/Cheats/EnlistmentRescueCheats.cs`: old
     ```
                 var commanderFit = snapshot != null && snapshot.Exists && snapshot.IsAlive
                     && !snapshot.IsPrisoner && snapshot.PartyIsActive;
     ```
     new `            var commanderFit = snapshot?.IsFit == true;`
   - `Main/Features/Enlistment/EnlistmentDialogGateService.cs`: old
     ```
             if (!snapshot.Exists || !snapshot.IsAlive || snapshot.IsPrisoner
                 || !snapshot.HasParty || !snapshot.PartyIsActive)
     ```
     new `        if (!snapshot.IsFit)`
   - `Main/Features/Enlistment/EnlistmentPlayerActionService.cs`: old
     ```
             if (commander == null || !commander.Exists || !commander.IsAlive
                 || commander.IsPrisoner || !commander.HasParty || !commander.PartyIsActive)
     ```
     new `        if (commander == null || !commander.IsFit)`
   - `Main/Features/Enlistment/EnlistmentReconciler.cs`: old
     `        if (snapshot.HasParty && snapshot.PartyIsActive && !snapshot.IsPrisoner)` new
     `        if (snapshot.IsFit)` (the lines above already return on `!Exists || !IsAlive`).
   - `Main/Features/Enlistment/EnlistmentService.cs`: old
     ```
             return snapshot.Exists && snapshot.IsAlive && !snapshot.IsPrisoner
                 && snapshot.HasParty && snapshot.PartyIsActive;
     ```
     new `        return snapshot.IsFit;`
   - `Main/Features/Enlistment/ServiceAttachmentService.cs` (its comment holds a long dash, so use the script):
     `WT$ "$PY" $S/edit_lines.py cut Main/Features/Enlistment/ServiceAttachmentService.cs "IsPrisoner is checked explicitly even though a captured hero" "|| !commander.HasParty || !commander.PartyIsActive)" --with "        if (commander == null || !commander.IsFit)"`
   **Verify**: `WT$ git grep -l "\.IsFit\b" -- Main | sort` lists exactly the six files above;
   Build exit 0; ENL filter `Failed: 0`.
4. Stage the eight files (`CommanderSnapshot.cs`, the six call-site files, the new test); commit `msg-C1.txt`:
   ```text
   fix(enlistment): v2.0.28 - one fitness check for a commander

   The six places that ask whether a commander can still lead an
   enlisted soldier (alive, free, with an active party) now share one
   check, CommanderSnapshot.IsFit. Nothing a player sees changes.

   Rejected: the board line for a battle the oath keeps the soldier out
     of, from the same 1.5.x commit (deferred, plan 132 P132-8)
   Ported-from: 5e7a0da6ef91d512a90dc9764f431c3d1b1534a7
   ```

#### Step C2: Siege duties for defenders, and wage order (`b0625f0d1`, #1014, P132-4)

- **Method**: port routine, `git cherry-pick -n b0625f0d1`.
- **Expected conflicts**: `EnlistmentDailyServiceTests.cs`, `EnlistmentPumpAuthorityTests.cs`,
  `ServiceMaintenanceServiceTests.cs` (constructor lines), `docs/features/enlistment.md`,
  `docs/reference/harmony-patch-registry.md`, `tools/README.md`; possibly `Content/Domain/ContentConfigs.cs`
  (a summary comment) and `EnlistmentContentConfigProviderTests.cs` (both sides added members).
- **Drop**: `TAOM.Tests/Features/Enlistment/EnlistmentDailyServiceTests.cs Main/Features/Enlistment/Content/IEnlistmentDailyService.cs Main/Features/Enlistment/Hooks/EnlistmentContentBehavior.cs docs/features/enlistment.md docs/reference/harmony-patch-registry.md tools/README.md`
  (P132-4: the first three keep `RunDailyTick(double nowDays)`).
- **Resolve and fix up**:
  1. `Main/Features/Enlistment/Content/EnlistmentDailyService.cs` (Edit tool): replace
     `    public DailySummary RunDailyTick()` with `    public DailySummary RunDailyTick(double nowDays)`.
     Keep the pick's `var rhythm = _rhythm.GetSnapshot();`.
  2. `WT$ sed -i -E 's/GetSnapshot\(Arg\.Any<double>\(\)\)/GetSnapshot()/g' TAOM.Tests/Features/Enlistment/EnlistmentDailyServiceTests.cs`
     (three rhythm stubs), then `git add` it.
  3. `EnlistmentPumpAuthorityTests.cs` and `ServiceMaintenanceServiceTests.cs`: in each conflict keep
     ours minus the rhythm argument, so the constructor lines end
     `new SitOutLedger(), new BattleMeritAccumulator(), _logger);` and
     `_reconciler, _presenter, _sitOutLedger, _accumulator, _logger);` (1.4.x has no
     `EnlistmentTestDoubles.ContractRenewal()`); `git add` both.
  4. `ContentConfigs.cs`, if conflicted: `WT$ "$PY" $S/edit_lines.py blocks Main/Features/Enlistment/Content/Domain/ContentConfigs.cs theirs --expect "World snapshot for the duty scheduler."`.
  5. `EnlistmentContentConfigProviderTests.cs`, if conflicted: `WT$ "$PY" $S/edit_lines.py blocks TAOM.Tests/Features/Enlistment/EnlistmentContentConfigProviderTests.cs union`
     and check each test name appears once.
  6. Registry: `WT$ "$PY" $S/fix_patch64_registry.py` prints `fixed the MenuLinkColors paragraph` (or
     `SKIP: ...`; record it for the hand-off and go on); `git add docs/reference/harmony-patch-registry.md`
     when it fixed it.
  7. Checks: `WT$ git grep -n "GetSnapshot(double\|GetSnapshot(nowDays\|_rhythm?.ResetForNewSession" -- Main TAOM.Tests`
     prints nothing; `WT$ git grep -n "IArmyRhythmSnapshotService" -- Main/Features/Enlistment/ServiceMaintenanceService.cs`
     prints nothing (the daily service and the duty orchestrator keep their `rhythm` parameter);
     `WT$ git grep -n "RunDailyTick(double nowDays)" -- Main` prints two lines (interface and class).
- **Keep**: `docs/features/menu-link-colors.md` (applies cleanly; its claims hold on v1.4.8,
  `GameMenuVM.cs:481-512`, `:522`), `tools/harvest_duty_strings.py` and its test (WP128 C4 brought
  them; this pick adds the commented-row and duplicate-row rules),
  `docs/reviews/rca-enlistment-followups-siege-wages-2026-10-26.md`.
- **Tests**: `ArmyRhythmSnapshotServiceTests` (+3), `EnlistmentContentConfigProviderTests` (+2; the
  compiled-default test fails on today's 0.45 default), `ServiceRewardServiceTests` (+1), the
  `EnlistmentSessionResetTests` rhythm test removed, `tools/tests/test_harvest_duty_strings.py` (+5).
- **Verify**: Build exit 0; ENL filter `Failed: 0`; Filtered `FullyQualifiedName~ServiceRewardServiceTests|FullyQualifiedName~DutyOrchestrationServiceTests`
  `Failed: 0`; Tool tests no new failure vs U0; Duty strings exit 0.
- **Paths**: `Main/Adapters/ArmyRhythmProbeAdapter.cs Main/Adapters/IArmyRhythmProbeAdapter.cs Main/Features/Enlistment/Content/ArmyRhythmSnapshotService.cs Main/Features/Enlistment/Content/Domain/ContentConfigs.cs Main/Features/Enlistment/Content/Domain/MeritConfig.cs Main/Features/Enlistment/Content/EnlistmentDailyService.cs Main/Features/Enlistment/Content/ServiceRewardService.cs Main/Features/Enlistment/Duties/DutyOrchestrationService.cs Main/Features/Enlistment/ServiceMaintenanceService.cs Main/Features/MenuLinkColors/Hooks/Patch64_MenuLinkColors.cs Main/Features/MenuLinkColors/MenuLinkStyleRewriter.cs`
  + under `TAOM.Tests/Features/Enlistment/`: `ArmyRhythmSnapshotServiceTests.cs Duties/DutyOrchestrationServiceTests.cs EnlistmentContentConfigProviderTests.cs EnlistmentDailyServiceTests.cs EnlistmentPumpAuthorityTests.cs EnlistmentSessionResetTests.cs ServiceMaintenanceServiceTests.cs ServiceRewardServiceTests.cs`
  + `tools/harvest_duty_strings.py tools/tests/test_harvest_duty_strings.py docs/features/menu-link-colors.md docs/reviews/rca-enlistment-followups-siege-wages-2026-10-26.md`
  (+ `docs/reference/harmony-patch-registry.md` when fixed).
- **Message** (`msg-C2.txt`):
  ```text
  fix(enlistment): v2.0.28 - siege duties for defenders, and wage order

  A soldier whose commander was defending a besieged town was never
  offered the siege duty (walking the works) and was offered the quiet
  night watch instead: only the besiegers counted as being in a siege.
  Defenders now count too.

  When a soldier's unpaid wages were already above the cap, a day's
  wage paid by his commander went to the banked debt instead of that
  day, so he lost a day of back pay. Today's wage is now paid first.
  This could only happen after the wage table or the cap changed
  mid-term.

  The column check behind the duty offers no longer keeps an hourly
  copy, so a new commander's column is read at once. The offer
  chance's built-in default now matches the shipped value, two
  comments about the menu link colours were corrected, and the duty
  text tool now ignores commented-out rows and refuses duplicates.

  Research: taom-src v1.4.8 MobileParty.SiegeEvent (:1132, the
    besieger's camp event), Settlement.SiegeEvent (:103),
    GameMenuVM.OnFrameTick (:481-512) and IsMenuTextChanged (:522)
  Not-tested: a siege duty offered to a defender, in game
  Ported-from: b0625f0d18e6d7a67efc70fd16f23a321fa91133
  ```

#### Step C3: Drop a battle rule that never ran (`3b848c411`, #796)

- **Method**: port routine, `git cherry-pick -n 3b848c411`.
- **Expected conflicts**: `Main/Features/Enlistment/Content/ContractRenewalService.cs` and
  `TAOM.Tests/Features/Enlistment/ContractRenewalTests.cs` (absent on 1.4.x), `docs/features/enlistment.md`,
  `docs/reviews/lessons/campaign-mechanics.md`, `docs/reviews/lessons/state-lifecycle-save.md`, and,
  because WP128 B1 left out the crown-war release and WP128 B5 hand-merged comments here:
  `EnlistmentReconciler.cs`, `EnlistmentReconcilerTests.cs`, `EnlistmentLoadNormalizer.cs`,
  `IEnlistmentConfigProvider.cs`.
- **Drop**: `Main/Features/Enlistment/Content/ContractRenewalService.cs TAOM.Tests/Features/Enlistment/ContractRenewalTests.cs docs/features/enlistment.md docs/reviews/lessons/campaign-mechanics.md docs/reviews/lessons/state-lifecycle-save.md`.
- **Resolve**:
  1. `EnlistmentReconciler.cs` and `EnlistmentReconcilerTests.cs`, if conflicted: every block is the
     crown-war release (`ReleaseIfServingAgainstOwnCrown`), which 1.4.x does not have. Run
     `WT$ "$PY" $S/edit_lines.py blocks <file> ours --expect ServingAgainstOwnCrown` on each conflicted
     one; a `STOP:` (a block about anything else) is a STOP condition. If the build later fails naming
     `ReleaseIfServingAgainstOwnCrown`, run
     `WT$ "$PY" $S/edit_lines.py member TAOM.Tests/Features/Enlistment/EnlistmentReconcilerTests.cs "public void ReleaseIfServingAgainstOwnCrown_EncounterOutlivedItsBattle_Releases("`.
  2. `EnlistmentLoadNormalizer.cs` and `IEnlistmentConfigProvider.cs`, conflicted or not:
     `WT$ git checkout 90035a799 Main/Features/Enlistment/EnlistmentLoadNormalizer.cs Main/Features/Enlistment/IEnlistmentConfigProvider.cs`
     (the 1.5.x end state of this commit plus `90035a799`, whose code WP128 already has). Then check:
     - `WT$ git diff -U0 HEAD -- Main/Features/Enlistment/IEnlistmentConfigProvider.cs | grep -E '^[-+][^-+]' | grep -v -E '^[-+]\s*///'`
       prints nothing (only summary lines changed);
     - `WT$ git diff -U0 HEAD -- Main/Features/Enlistment/EnlistmentLoadNormalizer.cs | grep -E '^[-+][^-+]' | grep -v -E '^[-+]\s*//'`
       prints exactly: `-` lines for `private readonly IEncounterAdapter _encounter;`, `IEncounterAdapter encounter,`,
       `_encounter = encounter;`, `if (record.State == EnlistmentState.EnlistedAttached && IsMidBattle(presence))`,
       `private bool IsMidBattle(PlayerPresenceSnapshot presence)`, `{`, `if (presence.IsInMapEvent)`,
       `return true;`, `return presence.HasPlayerEncounter && _encounter.GetOwnership(null).IsBattleEncounter;`, `}`,
       and one `+` line `if (record.State == EnlistmentState.EnlistedAttached && presence.IsInMapEvent)`.
     If either check prints anything else, run `git checkout HEAD <that file>` and apply this
     commit's change to it by hand: in `IEnlistmentConfigProvider.cs` replace the summary lines
     `/// (issue #551). One in-game hour: long enough that no loot or aftermath screen is still in` and
     `/// scope, short enough that a stuck player is not left there.` with
     `/// (issue #551). One in-game hour: a conservative margin (the loot and aftermath screens do not` and
     `/// have this shape), short enough that a stuck player is not left there.`; in
     `EnlistmentLoadNormalizer.cs` delete the `_encounter` field, constructor parameter and assignment,
     change the condition to `presence.IsInMapEvent` and delete `IsMidBattle`
     (`edit_lines.py member ... "private bool IsMidBattle("`). `git add` both files.
  3. `EnlistmentLoadNormalizerTests.cs`, if its normalizer constructor line conflicts (1.4.x's
     reconciler takes no `IServiceDiplomacyService`): keep ours and remove `_encounter, ` from the
     `new EnlistmentLoadNormalizer(` arguments.
- **Edits for v1.4.8** (Edit tool):
  - `TAOM.Tests/Features/Enlistment/DischargeServiceTests.cs`: replace `(PlayerEncounter.cs:910)` with
    `(v1.4.8 PlayerEncounter.cs:945)`. In `Execute_PlayerStillInHisBattle_LeavesTheEncounterToTheBattle`
    replace the comment line ending `A merged soldier leaves his event at step 6 (LeaveArmy), the #577 path.`
    with the two lines (the WP128 B5 note asked for this once the test exists):
    ```
            // event, so R1 defers. A merged soldier is pulled out of his event at step 5 (RestorePresence clears
            // AttachedTo), which is why the reconciler holds a feature-off discharge until the battle ends (#1021).
    ```
    (that is, replace the whole line `        // event, so R1 defers. A merged soldier leaves his event at step 6 (LeaveArmy), the #577 path.`).
  - `TAOM.Tests/Features/Enlistment/EncounterOwnershipPolicyTests.cs`: replace
    `(PlayerEncounter.cs:910), and IsJoinedBattle` with `(v1.4.8 PlayerEncounter.cs:945), and IsJoinedBattle`
    and `stays set until LeaveBattle (:1646)` with `stays set until LeaveBattle (:1992)`.
- **Checks**: `WT$ git grep -n -w "IsBattleEncounter\|isBattleEncounter" -- Main TAOM.Tests` prints
  nothing; `WT$ git grep -n "ServingAgainstOwnCrown\|CommanderAtWarWithYourRealm\|ContractRenewal" -- Main TAOM.Tests`
  prints nothing; `WT$ git grep -n "PlayerEncounter.cs:910\|:1646)" -- Main TAOM.Tests` prints nothing.
- **Keep**: `docs/reviews/rca-enlistment-battle-encounter-rule-2026-10-26.md` and the rest.
- **Tests**: `EncounterOwnershipPolicyTests` (+3, R1b tests restated), `DischargeServiceTests` (+2),
  `EnlistmentReconcilerTests` (renamed fixtures, +1 repulsed-assault sweep), `EnlistmentLoadNormalizerTests`,
  `ServiceAttachmentServiceTests`, `ServiceBattleServiceTests`, `ServiceMaintenanceServiceTests` (comments).
- **Verify**: Build exit 0; ENL filter `Failed: 0`.
- **Paths**: `Main/Adapters/EncounterAdapter.cs Main/Adapters/EncounterOwnershipSnapshot.cs Main/Features/Enlistment/Domain/EncounterFinishIntent.cs Main/Features/Enlistment/EncounterOwnershipPolicy.cs Main/Features/Enlistment/EnlistmentLoadNormalizer.cs Main/Features/Enlistment/EnlistmentReconciler.cs Main/Features/Enlistment/IEnlistmentConfigProvider.cs Main/Features/Enlistment/IServiceBattleService.cs Main/Features/Enlistment/ServiceAttachmentService.cs Main/Features/Enlistment/ServiceBattleService.cs Main/Features/Enlistment/ServiceMaintenanceService.cs`
  + under `TAOM.Tests/Features/Enlistment/`: `DischargeServiceTests.cs EncounterOwnershipPolicyTests.cs EnlistmentLoadNormalizerTests.cs EnlistmentReconcilerTests.cs ServiceAttachmentServiceTests.cs ServiceBattleServiceTests.cs ServiceMaintenanceServiceTests.cs`
  + `docs/reviews/rca-enlistment-battle-encounter-rule-2026-10-26.md`
- **Message** (`msg-C3.txt`):
  ```text
  refactor(enlistment): v2.0.28 - drop a battle rule that never ran

  A rule meant to keep the service away from a battle's loot and
  prisoner screens never ran in a game: the check it needed was never
  passed to it. It was not needed either. The player stays in his
  battle until those screens close, and an older rule already waits for
  that. Turned on, it would have made a soldier released right after a
  battle wait forever for an encounter that was already over. The rule
  and its check are removed, and the comments that described the wrong
  battle order now cite the game's own code. Nothing a player sees
  changes.

  Research: v1.4.8 dump PlayerEncounter.Finish (FinalizeBattle :1071,
    FinalizeEvent :1909), MapEvent.FinalizeEventAux (HandleMapEventEnd
    :2158), MapEventSide.cs:434, MapEvent.Update skips FinishBattle for
    the player's event while an encounter is open (:1041),
    JoinBattleInternal (:945)
  Ported-from: 3b848c4111db58b86be10f126b643839ed8cb2b6
  ```

#### Step C4: Feature doc for Part C, then the final gate

1. Insert the block below directly above `## Interactions with the rest of TAOM` in
   `docs/features/enlistment.md` (Edit tool, as in B4):
   ```markdown
   ### One fitness check for a commander (#1016, partial)

   `CommanderSnapshot.IsFit` (exists, alive, free, with an active party) replaces six copies of the
   same test: `EnlistmentRescueCheats`, `EnlistmentDialogGateService.CanEnlistWith`,
   `EnlistmentPlayerActionService`, the grace path of `EnlistmentReconciler`,
   `EnlistmentService.IsCommanderFit` and the `ServiceAttachmentService` assessment. It matches
   `CommanderTickSnapshot.IsFollowable` (`CommanderSnapshotIsFitTests`). The 1.5.x board line for a
   battle the oath keeps the soldier out of is not ported yet.

   ### Siege duties for defenders, and wage order (#1014)

   The army-rhythm probe counts a commander defending a besieged town as in a siege:
   `MobileParty.SiegeEvent` is the besieger's camp event (`BesiegerCamp?.SiegeEvent`, v1.4.8
   `MobileParty.cs:1132`), null for a defender, so the probe also reads the current settlement's
   `SiegeEvent`. The snapshot is probed fresh on every call (the daily tick, the daily duty offer and
   a duty request), so the hourly cache and its session reset are gone. `ServiceRewardService.PayDailyWage`
   pays today's wage before it releases banked arrears, so arrears above the cap no longer eat a
   day's pay. The compiled default of `scheduler.maxOfferChance` is 0.4, the shipped value. Review:
   `rca-enlistment-followups-siege-wages-2026-10-26.md`.

   ### A battle rule that never ran is gone (#796)

   Ownership rule R1b and `EncounterOwnershipSnapshot.IsBattleEncounter`, described as live in older
   sections of this doc, are removed. `EncounterAdapter.GetOwnership` computed the flag but never
   passed it to the snapshot, so R1b never fired and `EnlistmentLoadNormalizer.IsMidBattle` reduced to
   `presence.IsInMapEvent`. It was not needed either: on v1.4.8 the main party stays in its map event
   through the loot and aftermath screens and leaves it inside `PlayerEncounter.Finish`
   (`PlayerEncounter.cs:1071` calls `FinalizeBattle`, which finalizes the event at `:1909`;
   `MapEvent.FinalizeEventAux` raises `MapEventEnded` and calls `MapEventSide.HandleMapEventEnd`,
   which clears each party's side), and `MapEvent.Update` skips `FinishBattle` for the player's event
   while an encounter is open (`MapEvent.cs:1041`). Rule R1 (the player is in a map event) already
   protects that window. After a join the encountered party is the opposing leader
   (`PlayerEncounter.cs:945`). Review: `rca-enlistment-battle-encounter-rule-2026-10-26.md`.

   **Owed in-game checks (WP132).** See `plans/132-enlistment-fixes-b.md`, "In-game checks owed".
   ```
2. **Verify**: Docs as in A7. Commit `docs/features/enlistment.md` with `msg-C4.txt`:
   ```text
   docs(enlistment): v2.0.28 - siege duty, wage order, dead battle rule

   The enlistment feature doc records the shared commander fitness
   check (#1016), siege duties for defenders and the wage order (#1014),
   and the removal of the battle rule that never ran (#796).

   Ported-from: 5e7a0da6ef91d512a90dc9764f431c3d1b1534a7
   Ported-from: b0625f0d18e6d7a67efc70fd16f23a321fa91133
   Ported-from: 3b848c4111db58b86be10f126b643839ed8cb2b6
   ```
3. **Final gate**: Build exit 0; Test `Failed: 0` (record **TC**); Data no new ERROR; Tool tests no new
   failure vs U0; Snapshot exit 0 when S0 was 0; Docs `ai_dashes: 0` with L0 holding;
   `WT$ git log --oneline bannerlord-1.4.5..HEAD | wc -l` prints `15`, every subject carrying the
   version label.

## Disposition (every commit in the pack)

| Commit | Subject | Group | Disposition |
|---|---|---|---|
| `5d70714c48c93ed7d93315536d433d9e9f433581` | fix(battle): enlisted players roll survival as themselves | battlebalance-enlisted-survival-roll | **Adapted** (A1, TDD): redirect, field and constructor parameter removed from the 1.4.x model; `SurvivalChanceLine` and camp rest (`4528caf3f`) do not exist here; lessons and the camp RCA left behind |
| `113d3483bb1c6b303546e96c63e369eb2d920d5d` | refactor(coop): one single-player gate, survival roll split | battlebalance-enlisted-survival-roll | **Adapted** (A2, TDD): the translator regexes and `test_translate_placeholders.py`. Skipped: `CoopSessionPolicy.IsSinglePlayerAuthority` (its callers are 1.5.x-only; WP129 C4 takes that hunk pair itself when it needs the predicate), the survival split (P132-5), `PrefabBindingWalker`/`PrefabNodes`, 1.5.x feature docs |
| `1a132c727cf473f6010bd5fc29e36f5a99589c54` | fix(enlistment): the quartermaster says why he refuses | issue-829 | **Ported** (A3); language rows re-added for the 1.4.x layout; doc hunk replaced by A7 |
| `01f02c3a0dd52746bc313c31b77bdb4ce58db9e3` | refactor(enlistment): name the dialog gate for what it checks | issue-829 | **Ported** (A4), byte-identical on the seven files it touches |
| `5c5072dbc2f5dc50189ddb04c3df663bc2b99839` | refactor(enlistment): EnlistmentRank becomes EnlistmentKitTier | issue-936 | **Ported** (A5) as a scripted rename: identical to the commit on every file no other WP changed |
| `5a8c73d26018d0e795a1efeccb4cf1057061f518` | refactor(enlistment): kit-tier members say tier, not rank | issue-936 | **Adapted** (A6, P132-3): the seven identifier renames, the `kitRank` comments and summary, the save-key pin in a new `KitTierSaveKeyTests` (1.5.x `ServiceRankLadderTests` belongs to `813657bf3`). Skipped: parameter and comment renames, hunks naming `EnlistmentKitTiers.Best`, `AlreadyDrawnBest`, `ServiceRank.MasterSergeant` |
| `813657bf31a009c628b1876821895ba2d863427e` | feat(enlistment): seven ranks on the US Army enlisted ladder | issue-936 | **Not ported** (SKIP-FEATURE: new ranks, pay and save migration). Its in-scope piece, the frozen `kitRank` pin, is absorbed by A6 (K1 table) |
| `1d9cc42f1bf13a49a9c340182cc77dc9ec44817c` | fix(enlistment): soldiers fight their commander's battles | issue-830 | **Ported** (B1, P132-6); two test files rebuilt around WP128's earlier hand ports; registry and API snapshot rows re-written for v1.4.8 without the co-op sentences (D7); doc in B4 |
| `4666a254a1a0f4c9020110e4c6ff3a3216628ef4` | feat(enlistment): the oath states what your own men cost | issue-839 | **Adapted** (B2, TDD, P132-2): the board reads `IEnlistmentWagePreview.GetDailyWage()`, `DailyWageFor` deleted, the preview summary corrected. Skipped: `RetinueCostText`, `IPlayerPartyAdapter.GetMainPartyDailyWage`, oath text, strings, `ServiceHistoryService` (1.5.x-only), its RCA |
| `069962aa43773a2201f8ec358f4c0c9b40f046e9` | fix(enlistment): the service upkeep runs once a frame | issue-841 | **Ported** (B3); `ResetSessionCaches` keeps 1.4.x's `_meritAccumulator?.Consume()` and gets only the pending-join reset; `EnlistmentHooksLineCountTests` hunk dropped (file is 1.5.x-only) |
| `5e7a0da6ef91d512a90dc9764f431c3d1b1534a7` | fix(enlistment): the board knows when you sit a battle out | issue-1016 | **Adapted** (C1, TDD, P132-8): `CommanderSnapshot.IsFit`, the six call sites, `CommanderSnapshotIsFitTests`. Deferred: `OathSitOutRule`, the `SittingOut` activity, its string, the board service changes and its RCA |
| `b0625f0d18e6d7a67efc70fd16f23a321fa91133` | fix(enlistment): siege duties for defenders, and wage order | issue-1014 | **Ported** (C2, P132-4) with `RunDailyTick(double nowDays)` kept; harvest tool rules taken (WP128 brought the tool); registry Patch64 sentence corrected by script; `tools/README.md` hunk left to WP142 |
| `3b848c4111db58b86be10f126b643839ed8cb2b6` | refactor(enlistment): drop a battle rule that never ran | issue-796 | **Ported** (C3); `ContractRenewalService`/`ContractRenewalTests` hunks dropped (1.5.x-only); crown-war blocks kept as 1.4.x has them (absent); test comments re-cited to v1.4.8 |
| `f6ac2d335`, `b9d18458e`, `a67792c40`, `fd61757bf` (plan 014) | session resets | prerequisite (orchestrator note) | **Not in this WP**: ported by WP119 |
| `2d1e5f3385f871c5a829611641230266b87fb763` | no walking out of the commander's battle | prerequisite (orchestrator note) | **Not in this WP**: ported by WP128 A1 |

## Test plan

- New test files: `TAOM.Tests/Features/Enlistment/KitTierSaveKeyTests.cs` (A6, written here),
  `QuartermasterAnswerPolicyTests.cs` (A3), `EncounterGameMenuOrderAttackConditionPatchTests.cs`,
  `SitOutPolicyTests.cs`, `ServiceDailyAnnouncerSatOutTests.cs` (B1), `TAOM.Tests/Adapters/CommanderSnapshotIsFitTests.cs`
  (C1), `tools/tests/test_translate_placeholders.py` (A2).
- TDD for the hand ports: A1's two tests fail before the redirect goes (the constructor takes the
  enlistment query; the source reads `CommanderHeroId`); A2's test fails 3 of 6 on the uppercase-only
  regexes; B2 and C1 fail to compile before the change. B2's new test asserts the board shows 0 while
  the preview pays 0.
- Pattern files: `EnlistmentBattlePayoutServiceTests` for payout tests; `QuartermasterAnswerPolicyTests`
  for source pins through `RepoPaths.ReadSource`; `Patch66EnlistmentBindingTests` for binding pins.
- Structurally untestable (for `Not-tested:`): the postfix in a live encounter menu, a simulated battle's
  payout, a raid relief joined from the running service menu, the board in the grace state, a defender's
  siege duty, the survival roll in a real battle.
- Expect the C# total to rise by about 15 (Part A), 70 to 75 (Part B) and 10 (Part C), counting data
  rows as the runner does; record the exact figures. The gate is `Failed: 0` plus the named classes
  present, not an exact count.

## Done criteria

Part A:
- [ ] 7 commits on `port148/132-enlistment-fixes-b`.
- [ ] `git grep -n -w -E "EnlistmentRank|CanRequestDischargeFrom|IssueForRank|HasIssuedForRank|HighestIssuedRank|HighestIssuedEquipmentRank|AlreadyIssuedForRank|RankToken|ProbeRanks" -- Main TAOM.Tests` prints nothing.
- [ ] `git grep -n "RedirectEnlistedPlayerToCommanderParty\|IEnlistmentStateQuery" -- Main/Features/BattleBalance` prints nothing.
- [ ] Build exit 0; Test `Failed: 0`; `test_translate_placeholders` 6 OK; Tool tests no new failure; Docs `ai_dashes: 0`.
Part B:
- [ ] 4 more commits; `git grep -n "EvaluateSitOut\|OnWaitMenuFrame\|PartyIsInRaidEvent\|SitOutLedger" -- Main` matches.
- [ ] `git grep -n "IEnlistmentBattlePayoutService\|MaxWaitMenuStepSeconds" -- Main TAOM.Tests` prints nothing; `git grep -n -E "PayOutBattle\(won: (true|false)\)" -- TAOM.Tests` prints nothing.
- [ ] `grep -c '"sitOut"' Main/_Module/ModuleData/enlistment/enlistment_config.json` prints `1`; `LanguageFileCoverageTests` passes.
- [ ] Build exit 0; Test `Failed: 0`; Data no new ERROR; Snapshot exit 0 when S0 was 0.
Part C:
- [ ] 4 more commits; `git grep -n -w "IsBattleEncounter" -- Main TAOM.Tests` prints nothing; `git grep -l "\.IsFit\b" -- Main` lists the six call-site files.
- [ ] `grep -c "CurrentSettlement?.SiegeEvent != null" Main/Adapters/ArmyRhythmProbeAdapter.cs` prints `1`; `grep -c "ResetForNewSession\|GetSnapshot(double" Main/Features/Enlistment/Content/ArmyRhythmSnapshotService.cs` prints `0`.
- [ ] Build exit 0; Test `Failed: 0`; Tool tests no new failure; Duty strings exit 0; Docs `ai_dashes: 0`, L0 holds.
Always:
- [ ] `git diff --name-only bannerlord-1.4.5...HEAD -- Main/IoC.cs Main/TAOM.csproj CHANGELOG.md .claude docs/reviews/lessons tools/README.md docs/reference/feature-map.md docs/INDEX.md` prints nothing; `git diff --stat bannerlord-1.4.5...HEAD -- Main/SubModule.cs` shows 1 insertion and 1 deletion (three dots: the integrator may merge other WPs while this branch is open).
- [ ] No long dash in any commit body: `git log --format=%B bannerlord-1.4.5..HEAD | "$PY" -c "import sys; t=sys.stdin.buffer.read().decode('utf-8'); print(t.count(chr(0x2014)) + t.count(chr(0x2013)))"` prints `0`.

## STOP conditions

Stop and report (do not improvise) if:

- An A0 prerequisite token is missing (WP104, WP105, WP119 or a WP128 part not merged), the A0.4
  check finds this WP partly applied, or the installed Armory still reads `v2.0.26` (A0.5, D20).
- A pick conflicts in a `Main/` or `TAOM.Tests/` path its step does not name, or a named one cannot be
  resolved the way the step says; or `edit_lines.py`, `copy_tests.py`, `add_lang_rows.py` or
  `rename_ids.py` prints `STOP:`.
- A step seems to need `Main/IoC.cs`, `Main/TAOM.csproj`, `Directory.Build.props`,
  `.claude/settings.json`, or a `Main/SubModule.cs` edit other than A1's one line.
- `EnlistmentContainerWiringTests` fails for an unresolved type (every new dependency here is
  registered: `SitOutLedger` and the concrete payout service by `EnlistmentIoC`,
  `IEnlistmentWagePreview` by its mapping at `EnlistmentIoC.cs:79`).
- A test this plan does not touch starts failing, or a step's verification fails twice after a
  reasonable fix attempt.
- A taom-src or dump fact differs from the engine table (for example
  `game_menu_encounter_order_attack_on_condition` is no longer private bool with one `MenuCallbackArgs`,
  or `IsPlayerSimulation` gains a setter caller).
- In C3 the `IEnlistmentConfigProvider.cs` or `EnlistmentLoadNormalizer.cs` check prints other lines
  and the hand fallback does not build.
- `harvest_duty_strings.py --check` reports any change (never `--apply`).
- Snapshot `-Check` fails in B1 for a row other than the new one when S0 was 0.

## In-game checks owed (maintainer)

Debug log: `<game>/bin/Win64_Shipping_Client/Logs/taom_debug_*.log` (lines tagged `[Enlistment]`; `[EnlistDiag]` lines
need the enlistment trace switched on in MCM). Console commands `taom.service_status` and
`taom.rescue_service` exist on 1.4.x.

1. **Survival (A1).** Enlist with a hero who has good Medicine, fall in a few commander's battles: no
   error from `TaomPartyHealingModel`, and deaths are no likelier than when freelancing.
2. **Quartermaster (#829).** Ask the commander for kit twice at one rank: the issued line, then "You have
   drawn your due for your rank already". In German the three new lines read in English until WP135.
3. **Sit-out (#830).** In a commander's field battle that is not lopsided, "Leave it to the others" is
   greyed with "You serve in your commander's line..." while Attack is enabled. Wounded: the option is
   enabled with the wounded tooltip; after the battle, "You were wounded and stayed back..." and no
   renown. At about 85 percent strength or more on his side: the option is enabled with the odds
   tooltip; after the battle the log shows `[Enlistment] sat out a simulated battle (OverwhelmingCost, ...)`,
   half base XP and one trust lost. In a siege assault the option is greyed with the siege tooltip.
4. **Board pay (#839).** While the commander is lost (grace state, the board says the column is lost),
   "Daily pay" reads 0.
5. **Pump clock (#841).** On the service menu with time running the board still updates, and it updates
   while paused and right after loading onto the menu. A commander relieving a raided village, joined
   from the running service menu, opens the battle on the next frame with no casualties taken first
   (trace on: `join into a raid deferred to the wait-menu frame`); a field battle joins at once.
6. **Siege duty (#1014).** With the commander defending a besieged town, ask for work: the siege duty
   (walking the works) can be offered, and the quiet night watch is no longer the only kind.
7. **Battle rule (#796).** After a battle's loot screens, service returns to normal as before (no stuck
   state; `taom.service_status` reads `EnlistedAttached`).

## Maintenance notes

- **Translations (WP135).** Ten new keys show English until `/localize`: `taom_enlist_qm_refused_host`,
  `taom_enlist_qm_refused_load`, `taom_enlist_qm_refused_stores`, `taom_enlist_sitout_wounded_tip`,
  `taom_enlist_sitout_odds_tip`, `taom_enlist_sitout_refused_tip`, `taom_enlist_sitout_siege_tip`,
  `taom_enlist_sat_out_line`, `taom_enlist_sat_out_wounded_line`, `taom_enlist_sat_out_forced_line`.
  A2's stricter placeholder check applies to that run; 1.5.x reported no verdict change on its files,
  unverified on 1.4.x.
- **Docs sync (WP142 to WP144).** Not synced here: the lessons entries of `1d9cc42f1`, `069962aa4`,
  `5d70714c4`, `3b848c411` (campaign-mechanics, adapters-taleworlds-api, state-lifecycle-save) and
  `b0625f0d1`'s `tools/README.md` line.
- **P132-8 follow-up.** The board line for a battle the oath keeps the soldier out of needs
  `OathSitOutRule` (vassal reason only on 1.4.x), a `SittingOut` activity, `taom_enlist_act_sitout` in
  13 files, and `ServiceStatusService` taking `IEncounterAdapter` and `IServiceDiplomacyService`;
  1.5.x also moved `ResolveActivity`'s own fitness test to `IsFit` in that change.
- **Later ports into these files.** Kit-tier parameters still say `rank` (P132-3), so a future 1.5.x
  equipment pick conflicts there and on the seven-rank code. `TaomPartyHealingModel` has no
  `SurvivalChanceLine` split, so a camp-rest healing port must re-derive. `RunDailyTick` keeps
  `nowDays` until the term-end feature is ported.
- **Review focus** (recommend `/deep-review` per part; C# across two or more files): the payout's
  sat-out condition and that the ledger is consumed on every path; that the menu postfix only narrows
  and fails open; the raid-join deferral (`_inMenuFrame`, the menu id check, the reset in
  `ResetSessionCaches` and `Pump`); `RunDailyTick` keeping `nowDays`; R1b's removal resting on R1.

## Hand-off report (put in your final message)

Status per part (DONE, or where it stopped and why; the integrator updates `plans/README.md` from
this); T0, TA, TB, TC (total, passed, skipped); E0; U0 and the final tool-test result; S0 and the last
Snapshot exit; L0 and the last Docs line; D0 and the last Data ERROR count; KBV; whether `fix_patch64_registry.py` printed `SKIP`; any
conditional branch taken (B1 items 4 to 6, B2's `_config` count, C2 items 4 and 5, C3's fallback); the
15 commit hashes with subjects.
