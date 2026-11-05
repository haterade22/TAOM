# Plan 128: Land the 1.5.x enlistment fixes for battle exits, raids, service wars, merit, grace promotion, encounter exits and duty text on the 1.4.x line

> **Executor instructions**: Follow this plan step by step. Run every verification command and
> confirm the expected result before moving on. If anything in "STOP conditions" occurs, stop and
> report; do not improvise. Do not edit `plans/README.md`: the integrator records this plan's
> status and test counts when merging; put the numbers listed under "Hand-off report" in your
> final message. Three parts on one branch, in order: Part A (battle exits, discharged menu,
> raids), Part B (service wars, merit, grace promotion, encounter exits), Part C (the #842 to #845
> batch, including the English duty-text fix). Each part ends on a green tree with its own done
> criteria; a later session may resume at the next part.
>
> **Drift check (run first, Step A0.3)**:
> `git diff --stat 1b00d1acd..HEAD -- Main/Features/Enlistment Main/Adapters TAOM.Tests/Features/Enlistment TAOM.Tests/Adapters Main/Features/TaomSettings.cs Main/_Module/ModuleData/taom_enlistment_strings.xml Main/_Module/ModuleData/Languages Main/_Module/ModuleData/enlistment tools/generate_enlistment_duty_strings.py docs/features/enlistment.md docs/reference/harmony-patch-registry.md docs/reference/taleworlds-api-snapshot`
> Earlier WPs of this programme are expected to have changed several of these (see "What earlier
> WPs put in place"). Compare the live files against "Current state" and STOP only on a real
> mismatch: an anchor quoted below missing or duplicated, or a prerequisite check in Step A0
> failing.

## Status

- **Priority**: P1 (softlocks and untracked wars in a shared feature; Part C is P2/P3 except the
  P1 English duty-text fix)
- **Effort**: L overall. Part A: M (four cherry-picks, one helper script). Part B: M (one trimmed
  partial pick, one pick, two TDD hand-ports, one pick with test hand-merges). Part C: L (one clean
  pick, three picks with hand resolution, a Python tool and its tests)
- **Risk**: MED (stateful feature: battle joins, discharge, wars; every change is guarded by the
  1.5.x unit tests that come with it; no save field is added; Part C drops dead record keys that
  old saves still carry, which the parser ignores)
- **Depends on**: WP105 (`plans/105-composition-root.md`) and WP119
  (`plans/119-memory-cross-campaign-resets.md`), and through them WP101 and WP104. Wave 4 runs
  after waves 1 to 3, so WP106, WP110, WP111, WP112, WP123 and WP125 are normally merged too;
  Step A0 records which, and the steps say what to do either way.
- **Category**: bug
- **Wave / lane**: wave 4, lane A, first in the lane (WP132 "enlistment fixes, part B" depends on
  this WP and edits the same files; never run them in parallel)
- **Executor**: laptop
- **Decisions applied**: D1 (no `CHANGELOG.md`; the commit body is the entry), D2 (Enlistment has
  no `*Module.cs` on either line; it stays hand-wired and this WP adds no registration, so
  `Main/IoC.cs` and `Main/SubModule.cs` are untouched), D7 (the registry's sentences about the
  co-op module's own patches are dropped: unverified for any v1.4.8 co-op build), D8 (subjects
  carry `v2.0.28`), D9 (no CI: every gate runs locally), D10 (an ordinary session executes this
  plan), D19 (git only through the Bash tool). Plan decision P128-1 (under the maintainer's
  2026-11-04 delegation): from `1559b3261` take only the join-time war top-up and the adapter
  hardening; leave the hourly mid-term war follow, the crown-war release, its popup
  (`IInquiryAdapter.ShowNotice`) and its new string. Reason: the join-time top-up closes the defect
  (#832: a join after a mid-term declaration starts an untracked private war); the rest is new
  service behaviour with a new player-facing message.
- **Planned at**: 1.4.x `origin/bannerlord-1.4.5` `1b00d1acd` and 1.5.x `origin/bannerlord-1.5.x`
  `e45f6df2c`, 2026-11-04 (the 1.5.x ref has since moved on; every ported commit is older)
- **Issue**: port of #827, #828 (closes #573), #831, #954, #832 (partial), #960, #834 (partial),
  #835 (partial), #1021, #842, #843, #844, #845. No new work needs its own issue.

## Why this matters

Enlistment ("serve as a soldier in a lord's party") is on both lines, and the 1.4.x copy is still
the fork-point code: `git diff --stat a4c6d7c30 origin/bannerlord-1.4.5 -- Main/Features/Enlistment
TAOM.Tests/Features/Enlistment` is empty. Between 2026-10-19 and 2026-10-26 the 1.5.x line fixed
these player-visible defects, all present on 1.4.x:

- **Softlocks.** Vanilla 'Leave' and 'Abandon army' in the commander's battle strand service in its
  battle state (#827). A discharge during load brings the saved service menu back with every option
  hidden, because 1.4.x gates the whole wait menu on `IsEnlisted` (`EnlistmentMenuBehavior.cs:85`)
  (#828). A village raid the commander leads is joined like a battle (`ServiceAttachmentService.cs:115`
  joins any commander map event), holding the soldier in battle state for the hours a raid lasts and
  refusing release (#831). A landed join whose menu fails to open is rolled back
  (`ServiceBattleService.cs:137-143`), which can freeze or pull the soldier out of his own battle;
  two orphan-encounter shapes are never released; switching Enlistment off mid-battle pulls him
  out (#1021).
- **Untracked wars.** An own-clan soldier who joins his lord's battle against a faction the realm
  declared war on after the oath starts a private war that discharge never undoes (v1.4.8
  `MenuHelper.cs:265-269` calls `BeHostileAction.ApplyEncounterHostileAction`, which declares war
  for the player's map faction, `BeHostileAction.cs:187-197`) (#832). A vassal serving a foreign lord
  declares a kingdom-wide war the same way (#960).
- **Wrong credit.** After the hero falls, v1.4.8 lets the player take over a soldier, whose kills
  then feed the hero's merit (#834); a battle can promote during the grace state the daily tick
  already refuses (#835).
- **English text.** English players read "a task for the company" and "It went well." for every
  field duty, because v1.4.8 never loads string files for English (`LocalizedTextManager.cs:235`)
  and the inline fallback is generic (#845).

Neither Harad log (E3, E4) has an `[Enlistment]` or `[EnlistDiag]` service line; they show only the
three enlistment mission behaviours being added to each battle (E3 lines 202 to 204, E4 lines 405 to
407). These fixes therefore do not bear on the reported freezes; they repair the feature for players
who enlist.

## Current state

### Refs and simulation (this session)

- `origin/bannerlord-1.4.5` = `1b00d1acd9f6ceda81709067ca88ee1393115b48`; fork point
  `a4c6d7c30`. Since the fork, 1.4.x changed only five unrelated adapters
  (`CultureTroopIdReader`, `IObjectManagerAdapter`, `IPlayerIdentityAdapter`, `ObjectManagerAdapter`,
  `PlayerIdentityAdapter`) in the paths this WP touches.
- `git merge-tree --write-tree --name-only --no-messages --merge-base=<sha>^ origin/bannerlord-1.4.5 <sha>`
  for every commit: only `f21123c6a` is clean. A chained simulation on top of the commits earlier
  WPs port first (plan 014 series, `e9854bbd0`, `f75288ebf`) leaves these code conflicts, and they
  are the ones the steps resolve: `1559b3261` in `Main/Adapters/ServiceDiplomacyAdapter.cs`;
  full `9304a4cb9` and `75e190abe` (this plan hand-ports only their guards instead); `388e89de0` in
  `EnlistmentDailyServiceTests.cs`; `218fa4053` in 10 code and test files; `a20b4d154` in the three
  status-board files and two test files; `90035a799` in two comments and two test files. Every
  other conflict is bookkeeping, a feature doc or the 12 language files.

### What earlier WPs put in place (checked in Step A0)

| WP | What this plan relies on, or must expect |
|---|---|
| 104 | `TAOM.Tests/Infrastructure/RepoPaths.cs` with `public static string ReadSource(string relativePath, bool stripComments = false)` (needed by `EnlistmentServiceMenuWiringTests` and the B3 test). Patch categories may be applied through `TryPatchCategory`; `Patch66_Enlistment` stays one category. |
| 105 | `Main/Composition/FeatureModules.cs` exists. Enlistment is not a module (no `EnlistmentModule.cs` on 1.5.x either). |
| 119 | Plan 014: `TAOM.Tests/Features/Enlistment/EnlistmentSessionResetTests.cs` exists; `IArmyRhythmSnapshotService` declares `void ResetForNewSession();` instead of `Invalidate()`; `EnlistmentContainerWiringTests.BuildContainer` normally registers a substitute `IPlayerContextAdapter` (1.5.x `f6ac2d335`; B1 adds it if absent). |
| 106 (normally merged) | `EnlistmentMeritMissionBehavior.OnAgentRemoved` counts with `Interlocked.Increment(ref _kills);` (else `_kills++;`). |
| 112 (normally merged) | `e9854bbd0`: `EncounterAdapter` reads the main party through the campaign. |
| 111 (normally merged) | `Main/Adapters/StuckBattleEventAdapter.cs` (a913629cd, stuck-battle guard) and its section of `docs/features/map-event-guard.md`. |
| 125 (normally merged) | `943ab74c0` flat render: `IServiceStatusTextWriter.IsStale`, `_renderedLanguage`, `text.ToString()` in `ServiceStatusTextWriter.Write`; a language check in `ServiceStatusService.RefreshIfChanged`. |
| 123, 110 (normally merged) | `890fe29de` (trace default off) edits `DischargeServiceTests.cs` and `EnlistmentDiagnosticsGateTests.cs`; `ec2a3026f` edits `EnlistmentBattleBehavior.cs`. Context only. |

### Files and roles

| Area | Files | Role in this WP |
|---|---|---|
| Adapters | `Main/Adapters/EncounterAdapter.cs`, `IEncounterAdapter.cs` | New reads: `IsAttackBarredByTruce` (A1), `IsPartyRaiding` (A3), `GetOpposingLeaderFactionId` (B1), `IsJoinedBattleEncounter`, `ForgetEndedBattleResult` (B5) |
| Adapters | `VillageRaidReader.cs` (new, A3/A4), `CommanderLordAdapter.cs`, `CommanderSnapshot.cs`, `CommanderTickSnapshot.cs` | `PartyIsRaiding` on both commander snapshots |
| Adapters | `IServiceDiplomacyAdapter.cs`, `ServiceDiplomacyAdapter.cs`, `IPlayerContextAdapter.cs`, `PlayerContextAdapter.cs` | `FollowWarOn` (default reason), landed checks, `GetPlayerClanId` (B1) |
| Adapters | `ArmyRhythmProbeAdapter.cs`, `IArmyRhythmProbeAdapter.cs`, `EquipmentRosterCatalogAdapter.cs`, `IEquipmentRosterCatalogAdapter.cs`, `IInquiryAdapter.cs` | Dead reads removed (C2, C3); a comment (C4) |
| Feature | `Main/Features/Enlistment/**` (about 45 files, listed per step) | The fixes |
| Hook | `Main/Features/Enlistment/Hooks/MapEventHelperCanMainPartyLeaveBattlePatch.cs` (new, A1) | Patch66 postfix, 65 lines, existing category |
| Settings | `Main/Features/TaomSettings.cs` | One `HintText` (B5) |
| Data | `taom_enlistment_strings.xml`, the 12 `Languages/*/std_taom_enlistment_strings_*.xml`, `enlistment/enlistment_config.json`, `enlistment/enlistment_duties.json` | 3 new keys; dead JSON keys removed; duty words added |
| Tools | `tools/harvest_duty_strings.py` (new), `tools/tests/test_harvest_duty_strings.py` (new), `tools/generate_enlistment_duty_strings.py` (deleted), `tools/generate_translation_template.py`, `tools/harvest_literal_loc_keys.py` | C4 |
| Docs | `docs/features/enlistment.md`, `docs/reference/harmony-patch-registry.md`, `docs/reference/taleworlds-api-snapshot/patch-targets.md`, `docs/localization/TRANSLATOR_GUIDE.md`, `docs/modding/configs-balance.md`, `docs/modding/file-catalogue.md`, 8 new `docs/reviews/rca-enlistment-*.md` | Feature doc travels with the feature; registry is the crash-triage lookup |

### 1.4.x excerpts (`origin/bannerlord-1.4.5`, read this session; earlier WPs may shift lines)

- `Hooks/EnlistmentMenuBehavior.cs:85`: `args => _store.Record.IsEnlisted,` (the wait-menu condition).
- `ServiceAttachmentService.cs:115`: `if (commander.PartyIsInMapEvent)` (joins any commander event).
- `ServiceBattleService.cs:48` `private void TryJoin(string commanderPartyId, string trigger)`;
  `:121` `if (encounterReady)`; `:137` `if (_gameMenu.EnsureMenuOpen("encounter"))`; `:143` the
  `LogError` that announces the rollback.
- `EnlistmentReconciler.cs:154-159`: `if (_feature?.IsEnabled == false)` then
  `_discharge.Execute(DischargeReason.PlayerRequest);` with no battle check; `:436`
  `BreakStaleBattleLatch`, whose `latched` predicate at `:439` requires `EnlistedBattle`.
- `Hooks/EnlistmentMeritMissionBehavior.cs:96` `if (main == null || !main.IsActive())`; `:111`
  `if (!_active || affectorAgent == null || !affectorAgent.IsMainAgent)`; `:114` `_kills++;`.
- `Content/EnlistmentBattlePayoutService.cs:123` `var promotion = _promotion.EvaluateAndApply();`
  (no state guard); `Content/EnlistmentDailyService.cs:194` refuses promotion in
  `EnlistmentState.CommanderUnavailable`. `EnlistmentRecord.IsEnlisted` includes
  `CommanderUnavailable` (`Domain/EnlistmentRecord.cs:93-98`).
- `EnlistmentStateQuery.cs:35`: `_commander.GetSnapshot(record.CommanderHeroId).PartyId` (C1 makes it
  `GetPartyId`, which exists at `Main/Adapters/ICommanderLordAdapter.cs:39`).
- `ServiceDiplomacyService.cs`: ctor `(IEnlistmentStore, IServiceDiplomacyAdapter, IModLogger)`;
  interface has only `ApplyServiceWars` and `UnwindServiceWars`.
- `Main/Adapters/IPlayerContextAdapter.cs`: `GetPlayerKingdomId`, `GetPlayerCultureId`,
  `IsUnderMercenaryService` (no `GetPlayerClanId`; 1.5.x added it in its v1.5.2 bump). Only
  implementer: `PlayerContextAdapter.cs` (expression-bodied members over `Clan.PlayerClan`).
- `Main/Adapters/ServiceDiplomacyAdapter.cs`: `DeclareWarOn` reports `true` without checking that
  the war landed; `MakePeaceWith` likewise. `Main/Features/Diplomacy/Hooks/DeclareWarAction_ApplyInternal_Patch.cs`
  is a prefix that can return `false` (alliance veto), so a landed check matters on 1.4.x too.
- `Main/Adapters/EncounterAdapter.cs:99` computes `isBattleEncounter` but the snapshot built at
  `:107-115` never passes it, so `EncounterOwnershipSnapshot.IsBattleEncounter` is always false and
  ownership rule R1b (`EncounterOwnershipPolicy`) never fires; `EnlistmentLoadNormalizer.IsMidBattle`
  therefore reduces to `presence.IsInMapEvent`. B5 relies on this (1.5.x `3b848c411`, ported later
  by WP132, deletes the dead rule).
- English fallbacks: `Presentation/ServiceStatusTextWriter.cs:187`, `Duties/FieldDutyRuntime.cs:164`
  and `:321`, `Duties/InteractiveDutyPresenter.cs:18` (a hard-coded `DutyCopy` table) and `:161`.
- Ranks on 1.4.x: `ServiceRank { Recruit, Soldier, Veteran, Sergeant }`
  (`Content/Domain/ServiceEnums.cs`); kit tiers are `Equipment/EnlistmentRank.cs`
  (`IEquipmentIssueLedger.HighestIssuedRank`, `HasIssuedForRank`, `RecordIssue(EnlistmentRank, ...)`).
  1.5.x renamed these (`EnlistmentKitTier`, seven ranks such as `Private`, `Specialist`); adapt every
  ported test that names them (called out per step). `GateSpec.MinRank` is a `ServiceRank`, so a
  JSON `"minRank":"Private"` does not parse on 1.4.x.
- `ServiceStatusService` on 1.4.x already injects `IEnlistmentContentConfigProvider _config` (for the
  wage table); `ServiceStatusModel`'s last ctor parameter is `int nextRequirementTarget = 0`.
- The 12 language files (`BR por-BR, CNs zho-CN, CNt zho-HK, DE deu-DE, FR fre-FR, IT ita-IT, JP
  jpn-JP, KO kor-KO, PL pol-PL, RU rus-RU, SP spa-LA, TR tur-TR`) differ in layout from 1.5.x (rows
  separated by a blank line, no BOM), so every 1.5.x hunk on them conflicts; `LanguageFileCoverageTests`
  requires every English key in all 12. English placeholder rows are the pipeline's normal state
  until a paid translation run (`AccentStrippedTranslationTests` summary says so).
- The repo has `core.autocrlf=true`, and `.gitattributes` covers scripts only (plus one generated
  HTML page); these files are CRLF in the worktree. The helper scripts below read and write bytes
  and keep each file's own line endings.

### Engine facts verified on the installed v1.4.8 (taom-src cache `C:\Users\mikew\.taom-src\v1.4.8`, and the dump `C:\Decompiled_Bannerlord\_categories_v1.4.8`)

| Member | Signature or fact | Where |
|---|---|---|
| `Helpers.MapEventHelper.CanMainPartyLeaveBattleCommonCondition` | `public static bool ...()`; defender branch needs `MobileParty.MainParty.SiegeEvent` on a non-besieger side | `MapEventHelper.cs:109-120` |
| `MobileParty.SiegeEvent` | `public SiegeEvent SiegeEvent => BesiegerCamp?.SiegeEvent;` (so that defender branch is unreachable) | `MobileParty.cs:1132` |
| Callers of the predicate | exactly `game_menu_encounter_leave_on_condition` `:1566`, `..._abandon_army_on_condition` `:1638`, `..._surrender_on_condition` `:1877`; no other reference in the v1.4.8 dump (categories and modules build) | `EncounterGameMenuBehavior.cs` |
| `Helpers.MenuHelper.CheckEnemyAttackableHonorably` | `public static void ...(MenuCallbackArgs args)` sets `args.IsEnabled = false` under `NotAttackableByPlayerUntilTime` | `MenuHelper.cs:452-463` |
| `MenuCallbackArgs` | ctor `(MenuContext menuContext, TextObject text)` `:26`; `public bool IsEnabled = true;` `:10` | `MenuCallbackArgs.cs` |
| Encounter Attack | `EncounterAttackConsequence` calls `BeHostileAction.ApplyEncounterHostileAction` | `MenuHelper.cs:265-269` |
| `BeHostileAction.ApplyEncounterHostileAction` | declares `DeclareWarAction.ApplyByPlayerHostility(attackerParty.MapFaction, ...)` for the main party | `BeHostileAction.cs:187-197` |
| `DeclareWarAction` | `public static void ApplyByDefault(IFaction, IFaction)` `:62`, `ApplyByPlayerHostility` `:67`; `MakePeaceAction.Apply(IFaction, IFaction)` `MakePeaceAction.cs:37` | |
| Quest outcome of a war | `QuestHelper.CheckWarDeclarationAndFailOrCancelTheQuest` fails a quest when `DiplomacyHelper.IsWarCausedByPlayer` (true for `CausedByPlayerHostility`), cancels it otherwise | `QuestHelper.cs:138-151`, `DiplomacyHelper.cs:12-17` |
| `MapEventSide` | `LeaderParty { get; internal set; }` `:94`; `Parties` `:96`; `MissionSide { get; private set; }` `:99`; `OtherSide` `:108`; fields `_battleParties` `:43`, `_mapEvent` `:58` | `MapEventSide.cs` |
| `MapEvent` | `IsRaid => _mapEventType == BattleTypes.Raid` `:205` (also `IsForcingVolunteers` `:207`, `IsForcingSupplies` `:209`); `DefenderSide => _sides[0]` `:137`; fields `_sides` `:61`, `_mapEventType` `:72`; `Component { get; private set; }` `:114`; no `Sides` property | `MapEvent.cs` |
| Forced supplies and volunteers | created only from `PlayerEncounter.cs:861`, `:865` (player-only) | |
| `PartyBase` | `IsMobile` `:126`; `MobileParty { get; private set; }` `:122`; `MemberRoster { get; private set; }` `:129`; `MapEventSide` `:289`; `Side => MapEventSide?.MissionSide ?? None` `:329`; field `_mapEventSide` `:36`; `MapFaction` `:236` | `PartyBase.cs` |
| `MobileParty` | `IsLordParty { get; private set; }` `:1238`; `Party { get; private set; }` `:354` | |
| `TroopRoster` | field `_totalRegulars` `:23`; `TotalHealthyCount` `:63` | |
| Relief lord joins a raid | `StartBattleAction.ApplyInternal` puts the attacker on the opposite side of the running event (`:23-30`) and keeps the type `Raid` (`:113-115`); vanilla's lord-defender test is `EncounterManager.cs:157` | |
| `PlayerEncounter` | `Current` `:124`; static `Battle` `:138-148`; `public bool IsJoinedBattle { get; private set; }` `:253`; static `CampaignBattleResult` get/set `:267-277` (dereferences `Current`); `SetupFields` `:746` skips `MainParty.AttachedTo` `:750`, sets `_encounteredParty` `:763` | `PlayerEncounter.cs` |
| Siege continue check | `CheckIfBattleShouldContinueAfterBattleMission` skips the inner check on a null result `:1853`; `...Internal` `:1860-1866` returns false on null `:1863`, and on `EnemyRetreated` only when not a siege assault or the other side is not the attacker; `DoWait` `:1306` then `ContinueBattle` `:1287-1295` finishes the assault and opens `menu_siege_strategies` | `PlayerEncounter.cs` |
| Finish paths | `FinishEncounterInternal` teleports an active, unattached, losing main party `:1091-1093`; `Finish` leaves the settlement when `AttachedTo == null` `:1082-1084` | `PlayerEncounter.cs` |
| Take-over after falling | `Mission.CanTakeControlOfAgent` requires `!agentToTakeControlOf.Character.IsHero` `:6332-6336`; enabled by `SandBoxMissions.cs:757` (`OpenBattleMission`) and `:1510` (`OpenSiegeMissionWithDeployment`) | |
| `CharacterObject.PlayerCharacter` | `public static CharacterObject PlayerCharacter => Game.Current.PlayerTroop as CharacterObject;` | `CharacterObject.cs:250` |
| `Agent` | `public BasicCharacterObject Character` `:1392`; `IsMainAgent => this == Main` `:638` | `Agent.cs` |
| `Clan.PlayerClan` | `public static Clan PlayerClan => Campaign.Current.PlayerDefaultFaction;` | `Clan.cs:276` |
| `GameMenu` | wait menu ANDs its own condition with each option's `:158-168`; `RunMenuOptionConsequence` runs `EndWait` before an `isLeave` consequence `:266-282` (`:276`) | `GameMenu.cs` |
| English strings | `LoadLanguage`: `bool flag = stringId != "English";` `:235`, strings read only when `flag` `:247` | `LocalizedTextManager.cs` |

### Conventions that bind this work

- ADR-007 adapters: services never touch sealed TaleWorlds types; engine reads live in
  `Main/Adapters` (`VillageRaidReader`, the encounter and diplomacy adapters). ADR-002 thin entry
  points: hook and behaviour files stay under 150 lines and delegate. ADR-003 no `#region`. ADR-004
  no `[Obsolete]`. ADR-005 no `#if DEBUG` outside IoC registration.
- TDD: the two hand-ported guards (B3, B4) are written test first; cherry-picks bring their own tests.
- MCM json2 rule: never flip a persisted default; rename instead (this WP changes only one `HintText`).
- `.claude/rules/harmony-patches.md`: `MovementOrder` postfixes go through the shared deferred
  category (not relevant here; the new postfix is a menu predicate in `Patch66_Enlistment`).
- Localization: new player text is `{=key}English`, registered in `taom_enlistment_strings.xml`,
  with an English placeholder row in each of the 12 language files; never run
  `tools/translate_with_claude.py` (it spends money; WP135 and the maintainer own translation).

## Commands you will need

| Purpose | Command | Expected |
|---|---|---|
| Restore | `dotnet restore TAOM.sln` | exit 0 |
| Build | `dotnet build TAOM.sln -p:DisableModuleCopy=true -p:ModuleId=` | exit 0, 0 errors (a BHA0001 Patch88 warning may print) |
| Test | `dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId=` | `Failed: 0` (600000 ms timeout) |
| Filtered | the Test command plus `--filter "<expr>"` | `Failed: 0` |
| Enlistment filter | `--filter "FullyQualifiedName~TAOM.Tests.Features.Enlistment|FullyQualifiedName~VillageRaidReaderTests|FullyQualifiedName~TAOM.Tests.Infrastructure.Localization"` | `Failed: 0` |
| Data | `python tools/validate_moduledata.py` | no new ERROR vs the WP101 baseline |
| Tool tests | `python -m unittest discover -s tools/tests -t .` | no new failure vs A0 |
| Docs | `python tools/lint_docs.py --summary --dash-base $(git merge-base HEAD bannerlord-1.4.5)` | `ai_dashes: 0`; `dead_links` and `orphan_features` equal L0 |
| Snapshot | `pwsh -NoProfile -File tools/snapshot_api_surface.ps1 -Check` | exit 0 (run after a build) |
| Duty strings | `python tools/harvest_duty_strings.py --check` | exit 0, no change reported (Part C) |

`PY=/c/Python314/python.exe`. Both `-p:` flags are mandatory on every build and test: on 1.4.x
`Main/TAOM.csproj:133-136` does not gate the CopyModule target on `DisableModuleCopy`, so a build
without `-p:ModuleId=` deploys into the game install. Never `./build.ps1`; never two builds at once.

## Scope

**In scope** (only these; every step names its exact paths): the files in "Files and roles";
`TAOM.Tests/Features/Enlistment/**` test files named per step; `TAOM.Tests/Adapters/VillageRaidReaderTests.cs`
(new); `TAOM.Tests/Infrastructure/Localization/UnregisteredLocalizationKeyBaselineTests.cs` (one
comment, C4); `docs/features/map-event-guard.md` (one sentence, B2, only if its anchor exists); the
helper scripts and message files under `C:\Users\mikew\source\repos\taom-port\wp128-scripts\`
(outside the worktree, never committed).

**Single-owner files: not touched.** `Main/IoC.cs`, `Main/SubModule.cs`, `Main/TAOM.csproj`
(SDK-style, globs new files), `Directory.Build.props`, `.claude/settings.json`. All new constructor
dependencies are already registered (`EnlistmentIoC.RegisterEnlistmentFeature` registers
`IEncounterAdapter`, `IGameMenuAdapter`, `IServiceStatusTextWriter`, `IServiceDiplomacyService`,
`IEnlistmentContentConfigProvider`; `SiegeDefenseIoC` registers `IPlayerContextAdapter`, which
`EnlistmentDialogGateService` already consumes). If a step seems to need one of these files: STOP.

**Out of scope**: `CHANGELOG.md` (D1); `.claude/**`, `CLAUDE.md`, `AGENTS.md`,
`docs/reviews/lessons/*`, `docs/reviews/LESSONS-LEARNED.md`, `docs/reviews/REVIEW-LOG.md`,
`docs/INDEX.md`, `docs/reference/feature-map.md`, `docs/reference/doc-lookup.md`, `tools/README.md`
(WP142 to WP144 sync them); `docs/features/war-of-the-ring.md` (its 1.5.x paragraph describes a
1.5.x-only war rule); every hunk of the 1.5.x features these commits sit on but this WP does not port
(personal spoils, promotion notice, kit line, seven ranks, desertion cost, past service, term end,
column line, hourly war follow, crown-war release); translations; `plans/README.md`.

## Git workflow

- Worktree (Bash tool only, D19):
  `mkdir -p /c/Users/mikew/source/repos/taom-port/wp128-scripts && git -C /c/Users/mikew/source/repos/TAOM worktree add C:/Users/mikew/source/repos/taom-port/128 -b port148/128-enlistment-fixes-a bannerlord-1.4.5`.
  `WT=/c/Users/mikew/source/repos/taom-port/128`. Work only there. Never push, open a PR, tag, or
  merge into `bannerlord-1.4.5`; never touch the main checkout, its `.serena/project.yml` edit or its
  `stash@{0}`.
- **Shell state does not persist between Bash calls.** An unset `$WT` makes `cd "$WT"` a silent
  no-op, and the next git command would then hit the main checkout. Start every Bash call with
  `WT=/c/Users/mikew/source/repos/taom-port/128; PY=/c/Python314/python.exe; cd "$WT" || exit 1; test "$(git rev-parse --abbrev-ref HEAD)" = port148/128-enlistment-fixes-a || exit 1`.
- Save each helper script below into `wp128-scripts/` **with your file-writing tool, not a shell
  heredoc**, and run it from `$WT`. A script that prints `STOP:` and exits non-zero is a STOP condition.
- **Port routine** (every pick step; the step lists its specifics):
  1. `cd "$WT" && git status --porcelain` prints nothing.
  2. `git cherry-pick -n <sha>`. Exit 1 with conflicts is expected when the step lists conflicts.
  3. `bash ../wp128-scripts/drop.sh <paths>` for the step's "Drop" list (restores each to HEAD:
     a conflicted path keeps ours and is staged, a new file is removed, a changed one is restored).
  4. Resolve the step's code conflicts as written, then `git add <those paths>`.
  5. `git ls-files -u` prints nothing, and `git diff --cached --check` prints no "conflict marker" line.
  6. Build, then the step's filtered tests.
  7. `git status --porcelain`: every line is one of the step's paths (all staged, column 1 not blank).
  8. Write the message to `wp128-scripts/msg-<step>.txt` with your file-writing tool. If
     `test -f "$(git rev-parse --git-path CHERRY_PICK_HEAD)"` succeeds, run `git cherry-pick --quit`
     (it keeps the staged changes).
  9. In the NEXT Bash call, alone: `cd "$WT" && git commit -F ../wp128-scripts/msg-<step>.txt && git log -1 --format='%h %s' && git status --porcelain`.
- Never let `cherry-pick`, `--continue`, `-C`, `-c` or `--amend --no-edit` create a commit
  (`GIT_EDITOR=true` on this laptop would commit the 1.5.x message unedited).
- **Commit template** (subject at most 72 characters; body wrapped at 72 for 1.4.x readers, it
  becomes the player release note; no em or en dashes; no Co-Authored-By or "Generated with" line):

```
<type>(enlistment): v2.0.28 - <description>

<body>

Research: <what was verified on v1.4.8>
Not-tested: <what needs the in-game check>
Ported-from: <40-hex sha>
```

  `v2.0.28` is the `<Version>` in `Main/_Module/SubModule.xml`; if a `/release` commit has moved it
  (D8: the next one is `v2.4.0`), use the value in that file. Each step gives its subject and body.

## Helper scripts (save once, Step A0)

`wp128-scripts/drop.sh`:

```bash
#!/usr/bin/env bash
# Usage (from the worktree root, during a cherry-pick -n): bash ../wp128-scripts/drop.sh <path>...
set -u
for p in "$@"; do
  if [ -n "$(git ls-files -u -- "$p")" ]; then
    git checkout --ours "$p" && git add -- "$p" && echo "kept ours: $p"
  elif [ -n "$(git diff --cached --name-only --diff-filter=A -- "$p")" ]; then
    git rm -f -q -- "$p" && echo "removed new file: $p"
  elif [ -n "$(git diff --cached --name-only -- "$p")$(git diff --name-only -- "$p")" ]; then
    git checkout HEAD "$p" && echo "restored: $p"
  else
    echo "untouched: $p"
  fi
done
```

`wp128-scripts/add_lang_rows.py` (English placeholder rows in the 12 language files):

```python
"""Usage (from the worktree root): python ../wp128-scripts/add_lang_rows.py KEY "English" [KEY "English" ...]"""
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

`wp128-scripts/drop_tests.py` (removes named test methods; used in B1):

```python
"""Usage (from the worktree root): python ../wp128-scripts/drop_tests.py <file.cs> <MethodName>..."""
import pathlib, sys
path = pathlib.Path(sys.argv[1])
raw = path.read_bytes()
bom = raw.startswith(b"\xef\xbb\xbf")
text = (raw[3:] if bom else raw).decode("utf-8")
nl = "\r\n" if "\r\n" in text else "\n"
lines = text.split(nl)
for name in sys.argv[2:]:
    hits = [i for i, l in enumerate(lines) if l.startswith("    public void " + name + "(")]
    if len(hits) != 1:
        print(f"STOP: {name}: {len(hits)} signature lines in {path}"); sys.exit(1)
    sig = start = hits[0]
    while start > 0 and lines[start - 1].startswith("    ") and not lines[start - 1].startswith("     ") \
            and lines[start - 1].lstrip().startswith(("[", "//")):
        start -= 1
    end = next((j for j in range(sig + 1, len(lines)) if lines[j] == "    }"), None)
    if end is None or any(l.startswith("    public ") or l.strip() == "[TestMethod]" for l in lines[sig + 1:end]):
        print(f"STOP: {name}: cannot bound the method"); sys.exit(1)
    if end + 1 < len(lines) and lines[end + 1].strip() == "":
        end += 1
    del lines[start:end + 1]
    print(f"removed {name}")
path.write_bytes((b"\xef\xbb\xbf" if bom else b"") + nl.join(lines).encode("utf-8"))
```

In shell commands below, `LANGFILES` means
`$(ls Main/_Module/ModuleData/Languages/*/std_taom_enlistment_strings_*.xml)` (12 paths; check the
count once with `ls Main/_Module/ModuleData/Languages/*/std_taom_enlistment_strings_*.xml | wc -l` = 12).

## Steps

### Part A: battle exits, the discharged menu, raids

#### Step A0: Preflight (no commit)

1. Create the worktree (Git workflow) and save the three helper scripts.
2. Prerequisites, each must print its OK line (any missing line is a STOP naming the WP):
   ```
   cd "$WT"
   grep -q "public static string ReadSource" TAOM.Tests/Infrastructure/RepoPaths.cs && echo OK-WP104
   test -f Main/Composition/FeatureModules.cs && echo OK-WP105
   test -f TAOM.Tests/Features/Enlistment/EnlistmentSessionResetTests.cs && grep -q "void ResetForNewSession();" Main/Features/Enlistment/Content/ArmyRhythmSnapshotService.cs && ! grep -q "void Invalidate();" Main/Features/Enlistment/Content/ArmyRhythmSnapshotService.cs && echo OK-WP119
   ```
3. Drift check (header). Then record, without stopping:
   `grep -c "IPlayerContextAdapter" TAOM.Tests/Features/Enlistment/EnlistmentContainerWiringTests.cs` (K119w; B1 adds the line when it is 0),
   `grep -c "Interlocked.Increment(ref _kills)" Main/Features/Enlistment/Hooks/EnlistmentMeritMissionBehavior.cs` (K106),
   `grep -c "bool IsStale" Main/Features/Enlistment/Presentation/ServiceStatusTextWriter.cs` (K125),
   `test -f Main/Adapters/StuckBattleEventAdapter.cs && echo K111=1 || echo K111=0`.
4. Nothing of this WP is present yet:
   `git grep -n -E "ShouldBlockBattleExit|VillageRaidReader|MirrorWarAtJoin|ShouldSitOutAsVassal|IsJoinedBattleEncounter|LeaveAfterDischarge|class DutyText" -- Main`
   prints nothing (exit 1). Any match: STOP.
5. Baseline: Restore; Build (exit 0); Test (record T0 = total, passed, skipped; `Failed: 0`);
   Enlistment filter (record E0); Data (record); Tool tests (record U0); Snapshot (record S0 = exit
   code); Docs (record L0 = `dead_links`, `orphan_features`).

#### Step A1: No walking out of the commander's battle (`2d1e5f338`, #827)

- **Method**: port routine, `git cherry-pick -n 2d1e5f338`.
- **Expected conflicts**: `docs/reference/taleworlds-api-snapshot/patch-targets.md`,
  `docs/reviews/lessons/campaign-mechanics.md`. Code applies cleanly.
- **Drop**: `docs/reviews/lessons/campaign-mechanics.md docs/features/enlistment.md`
  (A5 writes the feature-doc section).
- **patch-targets.md**: `git checkout HEAD docs/reference/taleworlds-api-snapshot/patch-targets.md`
  (works whether or not it conflicted), then insert exactly this row directly after the row for
  `TAOM.Features.Enlistment.Hooks.LordConversationsJoinArmyConditionPatch`, and raise the
  `Patches: N.` number in the header by 1 (the 1.5.x hunk also adds a Diplomacy row from another
  commit: do not add it):
  ```
  | `TAOM.Features.Enlistment.Hooks.MapEventHelperCanMainPartyLeaveBattlePatch` | `Helpers.MapEventHelper.Boolean CanMainPartyLeaveBattleCommonCondition()` |
  ```
  `git add docs/reference/taleworlds-api-snapshot/patch-targets.md`.
- **Edits for v1.4.8** (re-cite 1.5.x engine claims):
  - `Main/Features/Enlistment/Hooks/MapEventHelperCanMainPartyLeaveBattlePatch.cs` summary: replace
    `branch is unreachable in v1.5.5)` with
    `branch is unreachable in v1.4.8, MobileParty.cs:1132 and MapEventHelper.cs:109-120)`.
  - `docs/reference/harmony-patch-registry.md` (the Patch66 paragraph merged cleanly): replace
    `at v1.5.5 no other TaleWorlds or official module assembly references the predicate` with
    `in the v1.4.8 dump (shipping categories and modules build) no other TaleWorlds or official module source references the predicate`;
    replace `so that branch is unreachable in v1.5.5)` with
    `so that branch is unreachable in v1.4.8, MobileParty.cs:1132)`; delete the paragraph's last
    sentence, which starts `The co-op module itself prefixes the predicate` (D7).
  - `git add` both files.
- **Keep**: everything else the pick staged, including the new
  `docs/reviews/rca-enlistment-battle-exit-2026-10-18.md`.
- **Tests that come with it**: `EnlistmentMenuServiceTests` (+7 `ShouldBlockBattleExit_*`),
  `MapEventHelperCanMainPartyLeaveBattlePatchTests` (new, 5), `Patch66EnlistmentBindingTests` (+3,
  including the IL caller-set scan over `TaleWorlds.CampaignSystem`).
- **Verify**: Build exit 0; Filtered
  `FullyQualifiedName~EnlistmentMenuServiceTests|FullyQualifiedName~MapEventHelperCanMainPartyLeaveBattlePatchTests|FullyQualifiedName~Patch66EnlistmentBindingTests`
  shows `Failed: 0` and lists the 15 new names; Snapshot exit 0 (when S0 was 0; when S0 was 1,
  report it and go on).
- **Paths**: `Main/Adapters/EncounterAdapter.cs Main/Adapters/IEncounterAdapter.cs Main/Features/Enlistment/BattleCommandPolicy.cs Main/Features/Enlistment/EnlistmentMenuService.cs Main/Features/Enlistment/IEnlistmentMenuService.cs Main/Features/Enlistment/Hooks/MapEventHelperCanMainPartyLeaveBattlePatch.cs TAOM.Tests/Features/Enlistment/EnlistmentMenuServiceTests.cs TAOM.Tests/Features/Enlistment/MapEventHelperCanMainPartyLeaveBattlePatchTests.cs TAOM.Tests/Features/Enlistment/Patch66EnlistmentBindingTests.cs docs/reference/harmony-patch-registry.md docs/reference/taleworlds-api-snapshot/patch-targets.md docs/reviews/rca-enlistment-battle-exit-2026-10-18.md`
- **Message** (`msg-A1.txt`):
  ```
  fix(enlistment): v2.0.28 - no walking out of the commander's battle

  An enlisted soldier could still take vanilla 'Leave' or 'Abandon army'
  in his commander's battle. Either one stranded service in its battle
  state, and 'Abandon army' also left a stray army around the commander.
  The encounter menu now hides both while the soldier serves in the
  commander's battle and does not lead his side. When a safe-passage
  truce greys out both 'Attack' and 'Send troops', the exits come back,
  so the menu always keeps a way forward. A player's own battles keep
  every exit.

  Research: taom-src MapEventHelper.CanMainPartyLeaveBattleCommonCondition
  (MapEventHelper.cs:109), its three EncounterGameMenuBehavior readers
  (:1566, :1638, :1877), MenuHelper.CheckEnemyAttackableHonorably (:452),
  MenuCallbackArgs(MenuContext, TextObject), MobileParty.SiegeEvent
  (:1132)
  Not-tested: the hidden exits and the truce release in a live battle menu
  Ported-from: 2d1e5f3385f871c5a829611641230266b87fb763
  ```

#### Step A2: A way out of the service menu after a discharge (`b24e8549e`, #828)

- **Method**: port routine, `git cherry-pick -n b24e8549e`.
- **Expected conflicts**: the 12 language files, `docs/reviews/lessons/campaign-mechanics.md`.
  `TAOM.Tests/Features/Enlistment/EnlistmentSessionResetTests.cs` conflicted only on the bare 1.4.x
  tip (file absent); after WP119 it applies. If it does conflict, keep ours and apply the hunk's one
  change by hand: the presenter construction passes
  `Substitute.For<IServiceStatusService>(), Substitute.For<IServiceStatusTextWriter>(), settings, ...`.
  If K125 = 1, `Presentation/ServiceStatusTextWriter.cs` may conflict: keep both sides, interface
  order `Write`, `WriteNotServing`, `IsStale`; in the class put `WriteNotServing` directly after the
  `IsStale` property (that is the 1.5.x tip shape).
- **Drop**: `docs/reviews/lessons/campaign-mechanics.md docs/features/enlistment.md docs/reference/doc-lookup.md` and `LANGFILES`.
- **Language rows**: `"$PY" ../wp128-scripts/add_lang_rows.py taom_enlist_menu_discharged_leave "Go on your way" taom_enlist_wait_discharged "Your service has ended."`
  (prints `+2 rows` for 12 files), then `git add LANGFILES`. The two source rows in
  `taom_enlistment_strings.xml` come with the pick.
- **Edit for v1.4.8**: in `Presentation/EnlistmentWaitMenuOptions.cs` replace
  `(GameMenu.cs:284-287, v1.5.5)` with `(GameMenu.cs:274-278, v1.4.8)`.
- **Keep**: everything else, including `docs/reviews/rca-enlistment-discharged-menu-2026-10-19.md`.
- **Tests**: `EnlistmentPlayerActionServiceTests` (+12), `EnlistmentServiceMenuWiringTests` (new, 3;
  source pins through `RepoPaths.ReadSource`), `EnlistmentWaitMenuPresenterTests` (+2).
- **Verify**: Build exit 0; Enlistment filter `Failed: 0` (it includes `LanguageFileCoverageTests`
  and `UnregisteredLocalizationKeyBaselineTests`).
- **Paths**: `Main/Features/Enlistment/EnlistmentPlayerActionService.cs Main/Features/Enlistment/IEnlistmentPlayerActionService.cs Main/Features/Enlistment/Hooks/EnlistmentMenuBehavior.cs Main/Features/Enlistment/Presentation/EnlistmentWaitMenuOptions.cs Main/Features/Enlistment/Presentation/EnlistmentWaitMenuPresenter.cs Main/Features/Enlistment/Presentation/ServiceStatusTextWriter.cs Main/_Module/ModuleData/taom_enlistment_strings.xml` + `LANGFILES` + `TAOM.Tests/Features/Enlistment/EnlistmentPlayerActionServiceTests.cs TAOM.Tests/Features/Enlistment/EnlistmentServiceMenuWiringTests.cs TAOM.Tests/Features/Enlistment/EnlistmentSessionResetTests.cs TAOM.Tests/Features/Enlistment/EnlistmentWaitMenuPresenterTests.cs docs/reviews/rca-enlistment-discharged-menu-2026-10-19.md`
- **Message** (`msg-A2.txt`):
  ```
  fix(enlistment): v2.0.28 - a way out of the service menu after discharge

  A discharge that runs while a save loads (Enlistment switched off in
  the mod options, or the identity check) left the saved service menu
  on screen with every option hidden, and the campaign could not go on.
  The menu no longer hides itself: each option checks whether anyone is
  serving, and when nobody is, a single 'Go on your way' option leaves
  the menu, into the town menu when the player stands in a town. The
  board then reads "Your service has ended." The two new lines show in
  English until the next translation run.

  Research: taom-src GameMenu.GetMenuOptionConditionsHold (GameMenu.cs:
  158-168, a wait menu ANDs its condition with every option's) and
  RunMenuOptionConsequence (EndWait before an isLeave consequence, :276)
  Not-tested: a discharge during load, in game
  Ported-from: b24e8549e88e8b0b465bc1615f4335855a3486d9
  ```

#### Step A3: Soldiers stay out of their lord's raids (`7a912dcd5`, #831)

- **Method**: port routine, `git cherry-pick -n 7a912dcd5`.
- **Expected conflicts**: the 12 language files, `docs/features/enlistment.md`,
  `docs/reference/harmony-patch-registry.md`, `docs/reviews/lessons/campaign-mechanics.md`. Code
  applies cleanly (`MapEventHelperCanMainPartyLeaveBattlePatch.cs` exists since A1). If K125 = 1,
  `ServiceStatusTextWriter.cs` may conflict around the activity switch: keep ours and add the
  `case CommanderActivity.Raiding:` lines from the pick.
- **Drop**: `docs/reviews/lessons/campaign-mechanics.md docs/features/enlistment.md` and `LANGFILES`.
- **Language rows**: `"$PY" ../wp128-scripts/add_lang_rows.py taom_enlist_act_raid "The column is raiding."`, then `git add LANGFILES`.
- **Registry**: `git checkout HEAD docs/reference/harmony-patch-registry.md`, then in the Patch66
  paragraph replace `'End Raiding' options (a later item)` with
  `'End Raiding' options (decided in #831: reachable only in the army-attached edge case and when the raid read fails open)`;
  `git add docs/reference/harmony-patch-registry.md`.
- Do not edit `VillageRaidReader.cs` here (A4's pick needs its summary lines unchanged as context).
- **Keep**: everything else, including `Main/Adapters/VillageRaidReader.cs`,
  `Main/Features/Enlistment/VillageActionJoinPolicy.cs` (new) and
  `docs/reviews/rca-enlistment-village-raids-2026-10-20.md`.
- **Tests**: `ServiceAttachmentServiceTests` (+3), `ServiceBattleServiceTests` (+3),
  `ServiceMaintenanceServiceTests` (+2), `SettlementFollowingTests` (+1), `EnlistmentPumpAuthorityTests`
  (+4 rows), `ServiceStatusServiceTests` (+2 rows), `VillageActionJoinPolicyTests` (new, 4 rows).
- **Verify**: Build exit 0; Enlistment filter `Failed: 0`.
- **Paths**: `Main/Adapters/CommanderLordAdapter.cs Main/Adapters/CommanderSnapshot.cs Main/Adapters/CommanderTickSnapshot.cs Main/Adapters/EncounterAdapter.cs Main/Adapters/IEncounterAdapter.cs Main/Adapters/VillageRaidReader.cs Main/Features/Enlistment/Domain/ServiceStatusModel.cs Main/Features/Enlistment/EnlistmentReconciler.cs Main/Features/Enlistment/Hooks/MapEventHelperCanMainPartyLeaveBattlePatch.cs Main/Features/Enlistment/Presentation/ServiceStatusTextWriter.cs Main/Features/Enlistment/ServiceAttachmentService.cs Main/Features/Enlistment/ServiceBattleService.cs Main/Features/Enlistment/ServiceMaintenanceService.cs Main/Features/Enlistment/ServiceStatusService.cs Main/Features/Enlistment/VillageActionJoinPolicy.cs Main/_Module/ModuleData/taom_enlistment_strings.xml` + `LANGFILES` + the 7 test files above + `docs/reference/harmony-patch-registry.md docs/reviews/rca-enlistment-village-raids-2026-10-20.md`
- **Message** (`msg-A3.txt`):
  ```
  fix(enlistment): v2.0.28 - soldiers stay out of their lord's raids

  An enlisted soldier was pulled into every village raid his commander
  led, as if it were a battle. He stayed in battle state for the hours a
  raid lasts, could not ask for release, and ended in the raid menu with
  its Abandon Army and Leave Army exits. A raid, forced supplies or
  forced volunteers that the commander leads is no longer a battle to
  join: the soldier keeps marching with the column, release stays open,
  and the service board says "The column is raiding." A commander who
  defends his own village still takes the soldier into that fight, and
  sieges, sally-outs and field battles join as before. The new line
  shows in English until the next translation run.

  Research: taom-src MapEvent.IsRaid/IsForcingSupplies/IsForcingVolunteers
  (MapEvent.cs:205-209), PartyBase.Side (:329); forced events are created
  only from PlayerEncounter.cs:861/865
  Not-tested: a commander's raid while enlisted, in game
  Ported-from: 7a912dcd544c1c3560b52de2df571f406131a782
  ```

#### Step A4: Soldiers fight lords who relieve a raid (`c04ba3fe5`, #954)

- **Method**: port routine, `git cherry-pick -n c04ba3fe5`.
- **Expected conflicts**: `docs/reference/harmony-patch-registry.md`,
  `docs/reviews/lessons/campaign-mechanics.md`, `docs/reviews/lessons/testing-qa.md`, possibly
  `docs/features/enlistment.md`. Code applies cleanly after A3.
- **Drop**: `docs/reviews/lessons/campaign-mechanics.md docs/reviews/lessons/testing-qa.md docs/features/enlistment.md`.
- **Registry**: `git checkout HEAD docs/reference/harmony-patch-registry.md`, then replace
  `(decided in #831: reachable only in the army-attached edge case and when the raid read fails open)` with
  `(decided in #831: reachable only in the army-attached edge case, when the raid read fails open, and possibly after a relief battle, #954)`;
  `git add` it.
- **Edits for v1.4.8**:
  - `Main/Adapters/VillageRaidReader.cs`: replace `(StartBattleAction.cs:116-118,` with
    `(v1.4.8 StartBattleAction.cs:113-115,` and `(EncounterManager.cs:158:` with
    `(v1.4.8 EncounterManager.cs:157:`. If K111 = 0 (no stuck-battle guard on this line), also
    replace `The set matches <c>StuckBattleEventAdapter</c>; forced` with `Forced`.
  - `Main/Adapters/EncounterAdapter.cs`: in the comment that starts `// Since #443 the main party may
    already be attached`, replace `(the merge at` + newline + `// ServiceBattleService.cs:111 runs before this call at :120)`
    with `(the army merge in ServiceBattleService.TryJoin runs before EnsureEncounterAgainst)`,
    `(v1.5.5 :747)` with `(v1.4.8 PlayerEncounter.cs:750)`, and `(:760;` with `(:763;`. Keep the
    comment wrapped near 100 columns.
  - `Hooks/MapEventHelperCanMainPartyLeaveBattlePatch.cs`: replace
    `(#954, docs/features/enlistment.md,` + newline + `/// Open risks 1))` with
    `(#954, docs/features/enlistment.md))`.
  - `TAOM.Tests/Adapters/VillageRaidReaderTests.cs` (stages engine objects; two v1.4.8 facts differ):
    (1) in `Raid(...)`, replace the four lines from `var component = Bare<RaidEventComponent>();`
    through `setter!.Invoke(mapEvent, new object[] { component });` with
    `SetField(mapEvent, "_mapEventType", MapEvent.BattleTypes.Raid);` (v1.4.8 `IsRaid` reads
    `_mapEventType`, not the component); (2) in `IsRaiding_EngineReadThrows_FailsOpenToFalse`,
    replace `commander.MapEvent.Sides[0] = null;` with
    `((MapEventSide[])typeof(MapEvent).GetField("_sides", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(commander.MapEvent))[0] = null;`
    (v1.4.8 has no `Sides` property).
  - `git add` the four files.
- **Keep**: `docs/reviews/rca-enlistment-relief-lord-2026-10-20.md` and the rest.
- **Tests**: `VillageRaidReaderTests` (new, 6), `ServiceMaintenanceServiceTests` (+1),
  `SettlementFollowingTests` (+1).
- **Verify**: Build exit 0; Enlistment filter `Failed: 0` and `VillageRaidReaderTests` shows 6 passed.
- **Paths**: `Main/Adapters/CommanderSnapshot.cs Main/Adapters/CommanderTickSnapshot.cs Main/Adapters/EncounterAdapter.cs Main/Adapters/IEncounterAdapter.cs Main/Adapters/VillageRaidReader.cs Main/Features/Enlistment/Hooks/MapEventHelperCanMainPartyLeaveBattlePatch.cs Main/Features/Enlistment/ServiceAttachmentService.cs Main/Features/Enlistment/VillageActionJoinPolicy.cs TAOM.Tests/Adapters/VillageRaidReaderTests.cs TAOM.Tests/Features/Enlistment/ServiceMaintenanceServiceTests.cs TAOM.Tests/Features/Enlistment/SettlementFollowingTests.cs docs/reference/harmony-patch-registry.md docs/reviews/rca-enlistment-relief-lord-2026-10-20.md`
- **Message** (`msg-A4.txt`):
  ```
  feat(enlistment): v2.0.28 - soldiers fight lords who relieve a raid

  When a lord rode to save a village from the soldier's commander, the
  fight happened inside the raid itself, so the soldier sat it out while
  his commander could lose and be taken. Now, while a lord with men
  still standing defends the raided village, the soldier joins his
  commander's side and fights. A raid defended only by militia or a
  patrol is still left to the column. When the battle is decided the
  raid ends and the soldier returns to normal service.

  Research: v1.4.8 StartBattleAction.ApplyInternal (:23-30 joins the
  running event, :113-115 keeps it a Raid), EncounterManager.cs:157 (the
  lord-defender test), MapEventSide.Parties, MobileParty.IsLordParty,
  TroopRoster.TotalHealthyCount; PlayerEncounter.SetupFields (:750, :763)
  Not-tested: a relief lord attacking the commander's raid, in game
  Ported-from: c04ba3fe58a39c2538dbb5a0708393e63d21b1e9
  ```

#### Step A5: Feature doc for Part A, then the Part A gate

1. `docs/features/enlistment.md`, section `## Patch66_Enlistment (see harmony-patch-registry.md)`:
   replace `All six targets pinned by` with `All seven targets pinned by`, and end that paragraph
   with the sentence: Since the WP128 port the category also holds the
   `MapEventHelper.CanMainPartyLeaveBattleCommonCondition` postfix (#827), described below.
2. Insert this new section directly before `## Interactions with the rest of TAOM` (keep it free of
   em and en dashes):

```markdown
## Fixes ported from the 1.5.x line (port148 WP128)

These fixes reached the 1.4.x line in WP128 of the 2026-11-04 port programme. Engine facts were
re-checked on v1.4.8. The review records are the `docs/reviews/rca-enlistment-*` files named in
each entry; they describe the 1.5.x review and cite v1.5.5 lines.

### No walking out of the commander's battle (#827)

The `Patch66_Enlistment` postfix on `MapEventHelper.CanMainPartyLeaveBattleCommonCondition` hides
vanilla 'Leave' and 'Abandon army' while the soldier is in `EnlistedBattle` and does not lead his
side (`IEnlistmentMenuService.ShouldBlockBattleExit`, which starts from `BattleCommandPolicy`).
On v1.4.8 exactly three `EncounterGameMenuBehavior` conditions read the predicate (`:1566`,
`:1638`, `:1877`); `Patch66EnlistmentBindingTests` pins the set by IL scan. A safe-passage truce
that greys out 'Attack' and 'Send troops' (`MenuHelper.CheckEnemyAttackableHonorably`) releases
the exits, so the menu always keeps one. Review: `rca-enlistment-battle-exit-2026-10-18.md`.

### A way out of the service menu after a discharge during load (#828)

The wait menu's own condition is `args => true`; every service option gates itself on
`IEnlistmentPlayerActionService.IsServing`, because v1.4.8 ANDs a wait menu's condition with each
option's (`GameMenu.cs:158-168`). When nobody serves, the one `isLeave` option "Go on your way"
(`LeaveAfterDischarge`) leaves the menu, into the town menu when the player stands in a town, and
the board reads "Your service has ended." `EnlistmentServiceMenuWiringTests` pins the shape.
Review: `rca-enlistment-discharged-menu-2026-10-19.md`.

### The commander's village raids are not battles to join (#831), unless a lord relieves the village (#954)

`VillageRaidReader.IsRaiding` (one adapter read shared by both commander snapshots and
`IEncounterAdapter.IsPartyRaiding`) is true when the commander is the attacker in a raid, forced
supplies or forced volunteers and no mobile lord party with healthy troops defends it.
`VillageActionJoinPolicy.IsJoinableBattle` gates the assessment, the pump's join request and both
stale-latch predicates, and `ServiceBattleService` refuses the join as a floor. The board shows
"The column is raiding." A relief lord joins the running Raid event on its defender side (v1.4.8
`StartBattleAction.cs:23-30`, `:113-115`), so that fight is joined. Reviews:
`rca-enlistment-village-raids-2026-10-20.md`, `rca-enlistment-relief-lord-2026-10-20.md`.
```

3. **Verify**: Docs command shows `ai_dashes: 0` and `dead_links`, `orphan_features` equal L0.
4. Commit `docs/features/enlistment.md` with `msg-A5.txt`:
   ```
   docs(enlistment): v2.0.28 - battle exits, discharged menu and raids

   The enlistment feature doc records the four fixes ported from the
   1.5.x line: hidden battle exits (#827), the discharged-menu exit
   (#828), raids left to the column (#831) and relief lords (#954).

   Ported-from: 2d1e5f3385f871c5a829611641230266b87fb763
   Ported-from: b24e8549e88e8b0b465bc1615f4335855a3486d9
   Ported-from: 7a912dcd544c1c3560b52de2df571f406131a782
   Ported-from: c04ba3fe58a39c2538dbb5a0708393e63d21b1e9
   ```
5. **Part A gate**: Build exit 0; Test `Failed: 0` (record TA); Data no new ERROR; Tool tests no new
   failure vs U0; Snapshot exit 0 (when S0 was 0).

### Part B: service wars, merit, grace promotion, encounter exits

#### Step B1: A joined battle starts no private war (`1559b3261`, #832, partial: P128-1)

- **Method**: port routine with `git cherry-pick -n 1559b3261`, then trim the hourly follow and the
  crown-war release.
- **Expected conflicts**: `Main/Adapters/ServiceDiplomacyAdapter.cs`, the 12 language files,
  `docs/features/war-of-the-ring.md`, `docs/reference/feature-map.md`,
  `docs/reviews/lessons/campaign-mechanics.md`.
- **Drop** (one `drop.sh` call): `LANGFILES Main/_Module/ModuleData/taom_enlistment_strings.xml docs/features/war-of-the-ring.md docs/reference/feature-map.md docs/reviews/lessons/campaign-mechanics.md docs/features/enlistment.md docs/reviews/rca-enlistment-wars-during-service-2026-10-20.md Main/Adapters/IInquiryAdapter.cs Main/Adapters/InquiryAdapter.cs Main/Features/Enlistment/Content/DischargeConsequenceService.cs Main/Features/Enlistment/DischargeService.cs Main/Features/Enlistment/Domain/DischargeReason.cs Main/Features/Enlistment/EnlistmentReconciler.cs Main/Features/Enlistment/Hooks/EnlistmentBehavior.cs Main/Features/Enlistment/IEnlistmentReconciler.cs TAOM.Tests/Features/Enlistment/DischargeConsequenceServiceTests.cs TAOM.Tests/Features/Enlistment/DischargeServiceTests.cs TAOM.Tests/Features/Enlistment/EnlistmentReconcilerTests.cs TAOM.Tests/Features/Enlistment/EnlistmentHourlyTickTests.cs TAOM.Tests/Features/Enlistment/CommanderLossAnnouncementTests.cs TAOM.Tests/Features/Enlistment/EnlistmentDiagnosticsGateTests.cs TAOM.Tests/Features/Enlistment/EnlistmentFeatureToggleTests.cs TAOM.Tests/Features/Enlistment/EnlistmentLoadNormalizerTests.cs TAOM.Tests/Features/Enlistment/EnlistmentPumpAuthorityTests.cs TAOM.Tests/Features/Enlistment/SettlementFollowingTests.cs`.
  The last six only add `Substitute.For<IServiceDiplomacyService>()` to `new EnlistmentReconciler(...)`
  calls; the reconciler change itself is dropped (P128-1), so those hunks go too.
- **ServiceDiplomacyAdapter.cs**: first `git diff --stat 1b00d1acd..HEAD -- Main/Adapters/ServiceDiplomacyAdapter.cs`
  must print nothing (else STOP: an earlier WP changed it). Then take the commit's whole file:
  `git show 1559b3261:Main/Adapters/ServiceDiplomacyAdapter.cs > Main/Adapters/ServiceDiplomacyAdapter.cs`
  (its only other 1.5.x change since the fork, `2410aec51`, is the landed check this commit builds
  on, and git normalises the line endings on `git add`). Then replace
  `(TAOM's Permanent or same-side veto, or another mod's)` with
  `(TAOM's alliance veto, DeclareWarAction_ApplyInternal_Patch, or another mod's)`. `git add` it.
- **Trim** (by hand, then `git add` each):
  - `Main/Features/Enlistment/ServiceDiplomacyService.cs`: delete the interface members and the
    implementations of `FollowCommanderWars` and `IsServingAgainstOwnCrown`; delete the
    `ICommanderLordAdapter _commander` field, its ctor parameter and assignment (the ctor becomes
    `(IEnlistmentStore store, IServiceDiplomacyAdapter diplomacy, IPlayerContextAdapter playerContext, IModLogger logger)`);
    in the class summary replace `the four moments (the oath, the hourly follow, the join-time top-up
    and the discharge unwind)` (the source wraps it after `the hourly follow, the`) with `the three
    moments (the oath, the join-time top-up and the discharge unwind)`. Keep `MirrorWarAtJoin`, `MayMirrorMidTerm`, `Record`, `Contains`, the
    `ApplyServiceWars` change (clear a stale mirror, record each landed war) and
    `using TAOM.Features.Enlistment.Domain;`.
  - `Main/Features/Enlistment/ServiceWarPolicy.cs`: delete `Diff`, `Ordered` and the `ServiceWarDiff`
    class, each with its summary; keep `ShouldMirrorMidTerm`.
  - `Main/Features/Enlistment/ServiceBattleService.cs`: delete the block that starts with the comment
    `// SERVING AGAINST HIS OWN CROWN (#832)` through the closing brace of its `if`; keep the
    ctor change and the `// TOP UP A MISSING WAR` block.
  - `Main/Adapters/IPlayerContextAdapter.cs`: add `string GetPlayerClanId();` with the summary
    `/// <summary>The player clan's StringId, or "" when there is none (the #832 join-time war check).</summary>`
    (1.5.x has the same member, added in its v1.5.2 bump). `Main/Adapters/PlayerContextAdapter.cs`: add
    `public string GetPlayerClanId() => Clan.PlayerClan?.StringId ?? "";` after `GetPlayerKingdomId`.
  - Tests: `"$PY" ../wp128-scripts/drop_tests.py TAOM.Tests/Features/Enlistment/ServiceDiplomacyServiceTests.cs FollowCommanderWars_NewCommanderWar_IsDeclaredByDefaultReasonAndRecorded FollowCommanderWars_VetoedWar_RecordsNothing FollowCommanderWars_RepeatedHour_OneDeclarationOneEntry FollowCommanderWars_CommanderEndedAMirroredWar_MakesPeaceAndForgetsIt FollowCommanderWars_RefusedPeace_StaysMirrored FollowCommanderWars_EntryThePlayerIsNoLongerAtWarWith_IsPrunedWithoutMakingPeace FollowCommanderWars_PlayerMadePeaceWhileCommanderStillFights_IsRedeclaredNextHour FollowCommanderWars_PreOathEnemyAtPeaceAgain_IsNeverDeclared FollowCommanderWars_CommanderChangesKingdom_PeacesOnlyWhatHeLostAndDeclaresOnlyWhatIsNew FollowCommanderWars_EmptyPlayerEnemyRead_SkipsTheWholePass FollowCommanderWars_VassalIdentity_DoesNothingAndNeverReadsTheCommander FollowCommanderWars_IdentityMismatch_DoesNothingAndNeverReadsTheCommander FollowCommanderWars_NotEnlisted_DoesNothing IsServingAgainstOwnCrown_FactionAtWarWithTheCommander_True IsServingAgainstOwnCrown_IndependentClanAtWar_True IsServingAgainstOwnCrown_WarThatPredatesTheOath_False IsServingAgainstOwnCrown_SameWarBegunAfterTheOath_True IsServingAgainstOwnCrown_KingdomCrownWar_True IsServingAgainstOwnCrown_NotAtWar_False IsServingAgainstOwnCrown_NoFaction_False`
    (20 `removed` lines); in its `Setup` delete the `_commander` field and line and pass
    `(_store, _diplomacy, _playerContext, logger)`. In the same file delete what the script leaves
    behind: the two section comments `// ---- FollowCommanderWars: the hourly sync (#832) ----` and
    `// ---- IsServingAgainstOwnCrown ----`, and the helper `private void CommanderFights(params string[] enemies)`,
    which no remaining test calls (the Check below reads comments too).
    `"$PY" ../wp128-scripts/drop_tests.py TAOM.Tests/Features/Enlistment/ServiceBattleServiceTests.cs BattleStarted_ServingAgainstOwnCrown_DoesNotJoinOrTopUp HourlyRecovery_ServingAgainstOwnCrown_DoesNotJoin BattleStarted_ServingAgainstOwnCrown_ButPlayerAlreadyInAMapEvent_FallsThrough`.
    `"$PY" ../wp128-scripts/drop_tests.py TAOM.Tests/Features/Enlistment/ServiceWarPolicyTests.cs Diff_NewCommanderWar_IsDeclared Diff_StillAtWar_NothingToDo Diff_EndedWar_IsPeaced Diff_OathEnemiesAreNeverDeclaredNorPeaced Diff_DedupesAndToleratesNulls`.
- **Check**: `git grep -n -E "FollowCommanderWars|IsServingAgainstOwnCrown|ServiceWarDiff|ShowNotice\(|CommanderAtWarWithYourRealm" -- Main TAOM.Tests`
  prints nothing, and `git grep -n "OnHourlyTick" -- Main/Features/Enlistment/EnlistmentReconciler.cs Main/Features/Enlistment/IEnlistmentReconciler.cs`
  prints nothing. (`ShowNotice\(` and the path limit matter: 1.4.x has an unrelated
  `ShowNoticeOnSessionStart`, and `OnHourlyTick` is a normal name in `EnlistmentBehavior` and other
  features.)
- **Keep**: `Main/Adapters/EncounterAdapter.cs`, `IEncounterAdapter.cs` (`GetOpposingLeaderFactionId`),
  `IServiceDiplomacyAdapter.cs` (`FollowWarOn`, landed `MakePeaceWith` doc), and the
  `ServiceBattleService` ctor change in `ServiceBattleServiceTests.cs` (its only construction site).
- **Tests kept**: `ServiceDiplomacyServiceTests` (+8 `MirrorWarAtJoin_*`, +1
  `ApplyServiceWars_ClearsAStaleMirrorFirst_AndRecordsEachLandedWar`), `ServiceBattleServiceTests`
  (+5 top-up), `ServiceWarPolicyTests` (`ShouldMirrorMidTerm_*`, 6 rows).
- **Verify**: Build exit 0; Enlistment filter `Failed: 0`. If `EnlistmentContainerWiringTests` fails
  naming `IPlayerContextAdapter`, add
  `container.RegisterInstance(Substitute.For<global::TAOM.Adapters.IPlayerContextAdapter>());` to its
  `BuildContainer` before `EnlistmentIoC.RegisterEnlistmentFeature(container);` (the line plan 014
  added on 1.5.x) and include the file; any other unresolved type is a STOP.
- **Paths**: `Main/Adapters/EncounterAdapter.cs Main/Adapters/IEncounterAdapter.cs Main/Adapters/IServiceDiplomacyAdapter.cs Main/Adapters/ServiceDiplomacyAdapter.cs Main/Adapters/IPlayerContextAdapter.cs Main/Adapters/PlayerContextAdapter.cs Main/Features/Enlistment/ServiceBattleService.cs Main/Features/Enlistment/ServiceDiplomacyService.cs Main/Features/Enlistment/ServiceWarPolicy.cs TAOM.Tests/Features/Enlistment/ServiceBattleServiceTests.cs TAOM.Tests/Features/Enlistment/ServiceDiplomacyServiceTests.cs TAOM.Tests/Features/Enlistment/ServiceWarPolicyTests.cs` (+ `EnlistmentContainerWiringTests.cs` only when K119w was 0)
- **Message** (`msg-B1.txt`):
  ```
  feat(enlistment): v2.0.28 - a joined battle starts no private war

  An enlisted soldier who swore as his own clan took on his lord's wars
  only at the oath. If the lord's realm declared a new war later, his
  'Attack' in the lord's battle against that faction declared a private
  war that ending his service never undid. Now, before he joins such a
  battle, the lord's war is taken on first, with the game's default
  reason (open quests from that faction are cancelled, not failed), and
  it is recorded so that discharge ends it again. A soldier whose clan
  serves a kingdom keeps the oath-only rule. A war or peace that another
  rule vetoes is no longer reported as done.

  Research: v1.4.8 MenuHelper.EncounterAttackConsequence (:265-269) and
  BeHostileAction.ApplyEncounterHostileAction (:187-197) declare war for
  the player's faction; DeclareWarAction.ApplyByDefault (:62);
  QuestHelper.cs:138-151 cancels quests on a war the player did not cause;
  MapEventSide.OtherSide/LeaderParty; Clan.PlayerClan
  Rejected: the hourly war follow and the crown-war release from the same
  1.5.x commit (new behaviour and a new message; plan 128 decision P128-1)
  Save-compat: no new field; join-time wars go into the existing
  MirroredWars list, which discharge already unwinds
  Not-tested: a mid-term war declaration followed by a join, in game
  Ported-from: 1559b3261c2087149f7811bebeca8d7d7c7fc309
  ```

#### Step B2: Vassals stay out of their lord's new wars (`d4af27d3e`, #960)

- **Method**: port routine, `git cherry-pick -n d4af27d3e`.
- **Expected conflicts**: because B1 removed the crown-war block next to these hunks, expect
  `ServiceBattleService.cs`, `ServiceDiplomacyService.cs`, `ServiceBattleServiceTests.cs`,
  `ServiceDiplomacyServiceTests.cs`, plus `docs/features/map-event-guard.md`,
  `docs/reviews/lessons/testing-qa.md`, `docs/features/enlistment.md`.
- **Drop**: `docs/reviews/lessons/testing-qa.md docs/features/enlistment.md docs/features/map-event-guard.md`.
- **Resolve** (keep ours, add d4af27d3e's lines):
  - `ServiceBattleService.cs`, inside `TryJoin`, the end shape after the raid floor:
    ```csharp
            if (!_encounter.IsMainPartyInMapEvent && _encounter.IsPartyRaiding(commanderPartyId))
            {
                _logger?.LogInfo($"[Enlistment] commander raiding: not a battle to join ({trigger})");
                return;
            }

            var foe = _encounter.GetOpposingLeaderFactionId(commanderPartyId);

            // A VASSAL SITS OUT a battle his kingdom is not at war in (#960): choosing Attack there would
            // declare a kingdom-wide war on the commander's behalf (BeHostileAction), which discharge never
            // unwinds. No transition, no join. Same carve-out as the other floors, and it fails open.
            if (!_encounter.IsMainPartyInMapEvent && !string.IsNullOrEmpty(foe) && _diplomacy.ShouldSitOutAsVassal(foe))
            {
                _logger?.LogInfo($"[Enlistment] the player's kingdom is not at war with '{foe}': sitting this battle out ({trigger})");
                return;
            }

            // TOP UP A MISSING WAR before joining (#832), under the oath rules (own-clan identity only).
            // The encounter menu would otherwise declare it as an untracked war discharge never unwinds.
            // A refusal is logged and the join goes ahead anyway: a war check never refuses a join, and
            // CanPartyJoinBattle stays unused (see IEncounterAdapter).
            if (!string.IsNullOrEmpty(foe) && !_diplomacy.MirrorWarAtJoin(foe))
                _logger?.LogInfo($"[Enlistment] war top-up refused for '{foe}' (commander party '{commanderPartyId}'); joining anyway");
    ```
    (the old `var foe = ...` line under the top-up comment is gone).
  - `ServiceDiplomacyService.cs`: add the interface member `bool ShouldSitOutAsVassal(string foeFactionId);`
    with its summary from the pick, after `MirrorWarAtJoin`; add the implementation from the pick
    directly before `private bool MayMirrorMidTerm()`.
  - The two test files: keep ours and add, verbatim from `git show d4af27d3e -- <file>`, the 5 tests
    `BattleStarted_VassalSitOut_DoesNotTransitionOrJoin`, `HourlyRecovery_VassalSitOut_DoesNotJoin`,
    `BattleStarted_NoVassalSitOut_Joins`, `BattleStarted_VassalSitOut_ButPlayerAlreadyInAMapEvent_FallsThrough`,
    `BattleStarted_EmptyFoeRead_JoinsWithoutAskingTheSitOut` and the 6 `ShouldSitOutAsVassal_*` tests.
  - `docs/features/map-event-guard.md`: only if it now contains the line
    `in such a battle stays stuck while the player is enlisted.` (WP111), replace that line with
    `in such a battle stays stuck while the player is enlisted. This includes a battle the soldier`
    + newline + `deliberately sits out (#960 vassal sit-out), where his join would otherwise have been`
    + newline + `the event that finished it.` and include the file.
- **Keep**: `docs/reviews/rca-enlistment-vassal-sit-out-2026-10-21.md`.
- **Verify**: Build exit 0; Enlistment filter `Failed: 0`, and the 11 new names pass.
- **Paths**: `Main/Features/Enlistment/ServiceBattleService.cs Main/Features/Enlistment/ServiceDiplomacyService.cs TAOM.Tests/Features/Enlistment/ServiceBattleServiceTests.cs TAOM.Tests/Features/Enlistment/ServiceDiplomacyServiceTests.cs docs/reviews/rca-enlistment-vassal-sit-out-2026-10-21.md` (+ `docs/features/map-event-guard.md` when edited)
- **Message** (`msg-B2.txt`):
  ```
  fix(enlistment): v2.0.28 - vassals stay out of their lord's new wars

  An enlisted soldier whose clan serves a kingdom could follow his lord
  into a battle against a faction his own kingdom was not at war with.
  Choosing Attack or Send troops there declared war for his whole
  kingdom, and ending his service never undid it. Such a soldier now
  sits that battle out and keeps serving; he joins his lord's next
  battle against a faction his kingdom does fight.

  Research: v1.4.8 BeHostileAction.ApplyEncounterHostileAction declares
  war for attackerParty.MapFaction (BeHostileAction.cs:192-195); the 1.4.x
  enlist gate refuses only a lord at war with the player's kingdom
  (EnlistmentDialogGateService.cs:47-53)
  Not-tested: a vassal under a foreign lord meeting a faction at peace
  with his kingdom, in game
  Ported-from: d4af27d3e464b51eb50de410e28fe081643a5db9
  ```

#### Step B3: Merit counts only the hero's own kills (`9304a4cb9`, #834, guards only), TDD

1. **RED**: create `TAOM.Tests/Features/Enlistment/MeritHeroAttributionWiringTests.cs`:
   ```csharp
   using Microsoft.VisualStudio.TestTools.UnitTesting;
   using TAOM.Tests.Infrastructure;

   namespace TAOM.Tests.Features.Enlistment;

   /// <summary>
   /// #834: merit belongs to the enlisted hero. v1.4.8 lets the player take over a non-hero soldier after
   /// falling (Mission.CanTakeControlOfAgent needs !Character.IsHero, Mission.cs:6332-6336; enabled by
   /// SandBoxMissions.OpenBattleMission and OpenSiegeMissionWithDeployment), so the sampler keys on
   /// CharacterObject.PlayerCharacter, never on whichever agent is the main agent. Source pins from the
   /// 1.5.x PersonalSpoilsWiringTests (9304a4cb9).
   /// </summary>
   [TestClass]
   public class MeritHeroAttributionWiringTests
   {
       private const string Behavior = "Main/Features/Enlistment/Hooks/EnlistmentMeritMissionBehavior.cs";

       [TestMethod]
       public void TheSamplerTick_TreatsAControlledSoldierAsHeroDown()
       {
           var src = RepoPaths.ReadSource(Behavior, stripComments: true);
           var tick = src.Substring(src.IndexOf("OnMissionTick"), src.IndexOf("OnAgentRemoved") - src.IndexOf("OnMissionTick"));
           StringAssert.Contains(tick, "CharacterObject.PlayerCharacter");
       }

       [TestMethod]
       public void TheKillTest_IsTheHeroByCharacter_NotTheMainAgent()
       {
           // A soldier the player takes over after falling is never a hero, so his kills are not the player's.
           var src = RepoPaths.ReadSource(Behavior, stripComments: true);
           var body = src.Substring(src.IndexOf("OnAgentRemoved"));
           StringAssert.Contains(body, "CharacterObject.PlayerCharacter");
           Assert.IsFalse(body.Contains("IsMainAgent"), "OnAgentRemoved must not key on the controlled agent");
       }
   }
   ```
   **Verify**: Build exit 0; Filtered `FullyQualifiedName~MeritHeroAttributionWiringTests`: `Failed: 2`.
2. **GREEN**: in `Main/Features/Enlistment/Hooks/EnlistmentMeritMissionBehavior.cs`:
   `if (main == null || !main.IsActive())` becomes
   `if (main == null || !main.IsActive() || main.Character != CharacterObject.PlayerCharacter)`, and
   `if (!_active || affectorAgent == null || !affectorAgent.IsMainAgent)` becomes
   `if (!_active || affectorAgent == null || affectorAgent.Character != CharacterObject.PlayerCharacter)`.
   Leave the kill line as it is (`Interlocked.Increment(ref _kills);` when K106 = 1, else `_kills++;`).
   Nothing else from 9304a4cb9 (no `ConcurrentQueue`, no `KilledCharacterIds`, no spoils).
   **Verify**: Build exit 0; that filter `Failed: 0, Passed: 2`; Enlistment filter `Failed: 0`.
3. Commit the two files (port routine steps 7 to 9: explicit `git add`, message file, commit in the
   next Bash call) with `msg-B3.txt`:
   ```
   feat(enlistment): v2.0.28 - merit counts the hero's own kills only

   After the player's hero fell in an enlisted battle, the game lets him
   take over one of the soldiers still fighting, and that soldier's kills
   and positioning were counted toward the hero's merit. The sampler now
   counts a kill only when the hero himself lands it, and treats a
   controlled soldier as the hero being down.

   Research: v1.4.8 Mission.CanTakeControlOfAgent (:6332-6336, needs
   !Character.IsHero); SandBoxMissions.cs:757 and :1510 enable it;
   CharacterObject.PlayerCharacter (CharacterObject.cs:250);
   Agent.Character (Agent.cs:1392)
   Not-tested: taking over a soldier after the hero falls, in game
   Ported-from: 9304a4cb941d577cd4b2d5810af13529e313ebf6
   ```

#### Step B4: No battle promotion in the grace state (`75e190abe`, #835, guard only), TDD

1. **RED**: in `TAOM.Tests/Features/Enlistment/EnlistmentBattlePayoutServiceTests.cs`, after
   `PayOut_BattleXpCrossesThreshold_PromotesImmediately`, add:
   ```csharp
       [TestMethod]
       public void PayOut_CommanderUnavailable_EvaluatesNoPromotion()
       {
           // No chain of command is left to promote anyone (#835); the daily tick refuses it too.
           MakeEnlisted();
           _store.Record.State = EnlistmentState.CommanderUnavailable;
           _contentStore.Record.DaysServed = 10;
           _contentStore.Record.ServiceXp = 70; // +40 win = 110 >= 100: promotes in any other state

           _service.PayOutBattle(won: true);

           Assert.IsFalse(_service.LastBattlePromoted);
           Assert.AreEqual(ServiceRank.Recruit, _contentStore.Record.Rank);
       }
   ```
   (1.4.x signature `PayOutBattle(bool won)`; 1.4.x ranks start at `Recruit`.)
   **Verify**: Build exit 0; Filtered `FullyQualifiedName~PayOut_CommanderUnavailable_EvaluatesNoPromotion`: `Failed: 1`.
2. **GREEN**: in `Main/Features/Enlistment/Content/EnlistmentBattlePayoutService.cs`, directly before
   `var promotion = _promotion.EvaluateAndApply();` insert:
   ```csharp
           //
           // Not in the grace state: no chain of command is left to promote anyone (#835; the daily tick
           // makes the same rule).
           if (_store.Record.State == TAOM.Features.Enlistment.Domain.EnlistmentState.CommanderUnavailable)
               return true;
   ```
   **Verify**: that filter passes; Enlistment filter `Failed: 0`.
3. Commit the two files (port routine steps 7 to 9) with `msg-B4.txt`:
   ```
   feat(enlistment): v2.0.28 - no battle promotion in the grace state

   While the commander was missing (the grace state that waits for him to
   return or for the term to lapse), a battle could still promote an
   enlisted soldier, although the daily service tick already refuses
   promotion in that state. The battle payout now refuses it too.

   Research: 1.4.x EnlistmentDailyService.cs:194 already refuses promotion
   in CommanderUnavailable; EnlistmentRecord.IsEnlisted includes that state
   Not-tested: a battle fought during the grace state, in game
   Ported-from: 75e190abe685cba1b403af8847cb4da9d0b944c0
   ```

#### Step B5: No battle encounter is left without a way out (`90035a799`, #1021)

- **Method**: port routine, `git cherry-pick -n 90035a799` (1.5.x applied it after the Part C batch;
  here it lands first, so Part C's `218fa4053` hunks in the same test files are resolved there).
- **Expected conflicts** (simulated on the bare tip; Parts A and B do not touch these hunks):
  `Main/Features/Enlistment/EnlistmentLoadNormalizer.cs`, `Main/Features/Enlistment/IEnlistmentConfigProvider.cs`
  (both comment-only), `TAOM.Tests/Features/Enlistment/DischargeServiceTests.cs`,
  `TAOM.Tests/Features/Enlistment/EnlistmentFeatureToggleTests.cs`, `docs/features/enlistment.md`,
  `docs/reviews/lessons/state-lifecycle-save.md`.
- **Drop**: `docs/features/enlistment.md docs/reviews/lessons/state-lifecycle-save.md`.
- **Resolve**:
  - `EnlistmentLoadNormalizer.cs`: keep ours (1.4.x matrix comment); append to that comment the line
    `// Since #1021 the stale-latch recovery also times an Attached soldier outside a settlement whose open encounter is a joined battle.`
  - `IEnlistmentConfigProvider.cs`: keep ours; append to the `StaleBattleLatchDays` summary, before
    the blank `///` line: `/// The same window times an <c>EnlistedAttached</c> soldier outside a settlement whose open encounter is a joined battle,`
    + newline + `/// and releases an <c>EnlistedBattle</c> soldier inside a settlement without closing the encounter (#1021).`
  - `DischargeServiceTests.cs`: keep ours; add the method
    `Execute_PlayerInHisBattleInsideTheCommandersTown_DoesNotTouchTheBattleEncounter` verbatim from
    `git show 90035a799:TAOM.Tests/Features/Enlistment/DischargeServiceTests.cs` as the last method of
    the class (its fields `_commanderAdapter`, `_attachment`, `_gameMenu`, `_encounter`, `_logger` exist
    on 1.4.x). Skip the comment edit on `Execute_PlayerStillInHisBattle_LeavesTheEncounterToTheBattle`
    when that test is absent.
  - `EnlistmentFeatureToggleTests.cs`: keep ours; add the field `private IEncounterAdapter _encounter;`,
    the setup line `_encounter = Substitute.For<IEncounterAdapter>();`, pass `_encounter` instead of
    the inline `Substitute.For<IEncounterAdapter>()` in the `EnlistmentReconciler` ctor call of the
    class's `[TestInitialize]` method (origin line 49; leave the second ctor call near line 205 and
    the rest of the 1.4.x argument list alone), and add
    `ReconcileHourly_FeatureDisabledMidBattle_WaitsForTheBattleThenReleases`.
- **Edits for v1.4.8**:
  - `Main/Adapters/IEncounterAdapter.cs`, summary of `ForgetEndedBattleResult`: replace the sentences
    from `On the next assault the siege` through `so` + `forgetting it fixes this.` with:
    `On the next assault <c>PlayerEncounter.CheckIfBattleShouldContinueAfterBattleMissionInternal</c> (v1.4.8 PlayerEncounter.cs:1860-1866)`
    `returns false on that result only when the event is not a siege assault or the other side is not the attacker;`
    `for a defender the other side IS the attacker, so the old result reads as a battle to continue and <c>ContinueBattle</c>`
    `(:1287-1295) finishes the new assault and opens <c>menu_siege_strategies</c>. The check returns false on a NULL result`
    `(:1853, :1863), so forgetting it fixes this.` (as `///` lines).
  - `Main/Features/Enlistment/ServiceBattleService.cs`, the comment above `_encounter.ForgetEndedBattleResult();`:
    replace `For a defender the siege component's continue check reads it as a battle to` / `continue, finishes the new assault and switches to menu_siege_strategies. A NULL result makes every` / `component's continue check return false, so forget it first (#1021).`
    with `For a defender, PlayerEncounter's continue check (v1.4.8 PlayerEncounter.cs:1860-1866)` /
    `reads it as a battle to continue, and ContinueBattle (:1287-1295) finishes the new assault and opens` /
    `menu_siege_strategies. A NULL result makes that check return false, so forget it first (#1021).`
  - `Main/Features/Enlistment/EnlistmentReconciler.cs`, the `#1021 item 2` comment inside
    `BreakStaleBattleLatch`: replace `(both need AttachedTo == null);` with
    `(both need AttachedTo == null; v1.4.8 PlayerEncounter.cs:1091-1093 and :1082-1084);`.
- **Keep**: `Main/Features/TaomSettings.cs` (the `EnableEnlistment` `HintText` and comment; no default
  changes), `EncounterAdapter.cs`, `DischargeService.cs`, `Domain/EncounterFinishIntent.cs`,
  `EnlistmentReconciler.cs`, `ServiceBattleService.cs`, `EnlistmentReconcilerTests.cs`,
  `ServiceBattleServiceTests.cs`, `docs/reviews/rca-enlistment-encounter-exits-2026-10-26.md`.
- **Tests**: `EnlistmentReconcilerTests` (+7 `StaleBattleLatch_*`), `ServiceBattleServiceTests` (+5,
  and `BattleStarted_MenuSwitchFails_RollsBackInsteadOfFreezingTheMapEvent` removed by the pick),
  `DischargeServiceTests` (+1), `EnlistmentFeatureToggleTests` (+1).
- **Verify**: Build exit 0; Enlistment filter `Failed: 0` and the 14 new names pass.
- **Paths**: `Main/Adapters/EncounterAdapter.cs Main/Adapters/IEncounterAdapter.cs Main/Features/Enlistment/DischargeService.cs Main/Features/Enlistment/Domain/EncounterFinishIntent.cs Main/Features/Enlistment/EnlistmentLoadNormalizer.cs Main/Features/Enlistment/EnlistmentReconciler.cs Main/Features/Enlistment/IEnlistmentConfigProvider.cs Main/Features/Enlistment/ServiceBattleService.cs Main/Features/TaomSettings.cs TAOM.Tests/Features/Enlistment/DischargeServiceTests.cs TAOM.Tests/Features/Enlistment/EnlistmentFeatureToggleTests.cs TAOM.Tests/Features/Enlistment/EnlistmentReconcilerTests.cs TAOM.Tests/Features/Enlistment/ServiceBattleServiceTests.cs docs/reviews/rca-enlistment-encounter-exits-2026-10-26.md`
- **Message** (`msg-B5.txt`):
  ```
  fix(enlistment): v2.0.28 - no battle encounter is left without a way out

  Four rare states could leave an enlisted soldier stuck after a battle:
  - when the battle menu failed to open after he had joined his
    commander's battle, the join was rolled back, which could freeze the
    battle or pull him out of it; he now stays and the game opens its
    own battle menu;
  - a leftover joined-battle encounter kept through a save was never
    closed once he was back with the column; the stale-battle recovery
    now closes it;
  - inside his commander's town, after attackers retreated from an
    assault, service waited on that battle for the rest of the siege; the
    recovery now hands him back without closing the town encounter, and
    the next assault forgets the old result first so it is fought;
  - switching Enlistment off in the mod options mid-battle could pull
    him out of it; the release now waits until the battle is over.

  Research: v1.4.8 PlayerEncounter.IsJoinedBattle (:253), static
  CampaignBattleResult (:267-277), the siege continue check (:1853,
  :1860-1866) and ContinueBattle (:1287-1295), FinishEncounterInternal
  (:1091-1093) and Finish (:1082-1084); 1.4.x ownership rule R1b is inert
  (GetOwnership never passes isBattleEncounter)
  Not-tested: the four stuck states in game (plan 128 in-game checks)
  Ported-from: 90035a799207619c134c1fe84d495fd8cde6d50b
  ```

#### Step B6: Feature doc for Part B, then the Part B gate

1. Append to the WP128 section of `docs/features/enlistment.md` (no em or en dashes):

```markdown
### A joined battle starts no private war (#832, join-time part only)

Before a join, `ServiceBattleService` asks `IServiceDiplomacyService.MirrorWarAtJoin` for the
faction of the opposing side's leader (`IEncounterAdapter.GetOpposingLeaderFactionId`). For a
soldier whose oath identity is his own clan (`ServiceWarPolicy.ShouldMirrorMidTerm`), a war his
lord's realm fights and he does not is declared first with the default reason
(`IServiceDiplomacyAdapter.FollowWarOn`; v1.4.8 cancels rather than fails quests for a war the
player did not cause, `QuestHelper.cs:138-151`) and recorded in `MirroredWars`, so discharge
unwinds it. Declarations and peace report success only when they landed (TAOM's alliance veto
can skip them). Not ported from 1.5.x: the hourly mid-term war follow and the release when the
lord's realm goes to war with the player's own (plan 128 decision P128-1).

### Vassals sit out battles their kingdom is not at war in (#960)

A soldier whose clan serves a kingdom sits out a commander's battle against a faction his kingdom
is not at war with (`ShouldSitOutAsVassal`), because 'Attack' there would declare a kingdom-wide
war (v1.4.8 `BeHostileAction.cs:187-197`). Review: `rca-enlistment-vassal-sit-out-2026-10-21.md`.

### Merit is the hero's (#834) and the grace state promotes no one (#835)

`EnlistmentMeritMissionBehavior` counts a kill only when `affectorAgent.Character` is
`CharacterObject.PlayerCharacter` and treats a controlled non-hero soldier as the hero being
down (v1.4.8 `Mission.CanTakeControlOfAgent` needs a non-hero, `Mission.cs:6332-6336`).
`EnlistmentBattlePayoutService` evaluates no promotion in `CommanderUnavailable`, matching the
daily tick. The 1.5.x personal spoils and promotion notice are not ported.

### No battle encounter is left without a way out (#1021)

A landed join whose `encounter` menu fails to open stays in the battle (the game opens the menu).
`BreakStaleBattleLatch` also times an `EnlistedAttached` soldier outside a settlement whose open
encounter is a joined battle (`IEncounterAdapter.IsJoinedBattleEncounter`), and releases an
`EnlistedBattle` soldier inside a settlement without finishing the encounter, because a finish
there teleports or walks out an unmerged party (v1.4.8 `PlayerEncounter.cs:1082-1093`). The next
join forgets a reused encounter's old result (`ForgetEndedBattleResult`) so a defender's next
assault is fought (`PlayerEncounter.cs:1860-1866`, `:1287-1295`). A feature-off discharge waits
until the battle ends. This relies on ownership rule R1b being inert on 1.4.x; WP132 ports its
deletion (`3b848c411`). Review: `rca-enlistment-encounter-exits-2026-10-26.md`.
```

2. **Verify**: Docs command as in A5. Commit `docs/features/enlistment.md` with `msg-B6.txt`
   (subject `docs(enlistment): v2.0.28 - wars, merit, grace promotion, exits`, a two-line body
   naming #832, #960, #834, #835, #1021, and one `Ported-from:` line for each of `1559b3261...`,
   `d4af27d3e...`, `9304a4cb9...`, `75e190abe...`, `90035a799...` with the full SHAs from the
   Disposition table).
3. **Part B gate**: Build exit 0; Test `Failed: 0` (record TB); Data; Tool tests vs U0.

### Part C: the #842 to #845 batch

#### Step C1: The commander-party check is an id compare (`f21123c6a`, #842)

- **Method**: port routine, `git cherry-pick -n f21123c6a`; it applies cleanly. Nothing to drop.
- Run `git grep -n "IsCommanderParty" -- Main` and name the callers it lists in the body.
- **Verify**: Build exit 0; Filtered `FullyQualifiedName~EnlistmentStateQueryTests` `Failed: 0` (+2).
- **Paths**: `Main/Features/Enlistment/EnlistmentStateQuery.cs TAOM.Tests/Features/Enlistment/EnlistmentStateQueryTests.cs`
- **Message** (`msg-C1.txt`): subject `perf(enlistment): v2.0.28 - the commander-party check is an id compare`;
  body: `Checking whether a party is the enlisted soldier's commander's party` /
  `(<the callers found above>) built a full picture of the commander,` /
  `with a hero lookup and several names turned into text, only to compare` /
  `one party id. It now reads the party id alone. Nothing a player sees` / `changes.`;
  then `Ported-from: f21123c6aed80344e47a52f5468195a0c3561997`.

#### Step C2: The daily column check skips unread work (`388e89de0`, #843)

- **Method**: port routine, `git cherry-pick -n 388e89de0`.
- **Expected conflicts**: `TAOM.Tests/Features/Enlistment/EnlistmentDailyServiceTests.cs` (its 1.4.x
  form still has tests 1.5.x removed). `EnlistmentSessionResetTests.cs` applies after WP119.
- **Drop**: `docs/features/enlistment.md`.
- **Resolve** `EnlistmentDailyServiceTests.cs`: `git checkout --ours <it>`, then
  `sed -i -E 's/RunDailyTick\(([0-9.]+), 12\.0\)/RunDailyTick(\1)/g; s/GetSnapshot\(Arg\.Any<double>\(\), Arg\.Any<double>\(\)\)/GetSnapshot(Arg.Any<double>())/g' TAOM.Tests/Features/Enlistment/EnlistmentDailyServiceTests.cs`
  and `git add` it.
- **Check**: `git grep -n -E "RunDailyTick\([^)]*,|_rhythm\.GetSnapshot\([^)]*,|RequestDutyNow\([^)]*,|hourOfDay" -- Main/Features/Enlistment Main/Adapters TAOM.Tests/Features/Enlistment`
  prints nothing (the same grep over 1.5.x `388e89de0` is empty); fix any leftover two-argument call
  the same way.
- **Holds on 1.4.x**: the removed snapshot fields (`Phase`, `LowSupplies`, `RecoveryState`,
  `ActiveCampaign`, `HighScrutiny`, `WoundedCount`, `RecruitCount`) have no reader outside their
  producers (`git grep` this session); `ServicePhase` goes with them.
- **Tests**: `ArmyRhythmSnapshotServiceTests` (new, 2 methods with rows) and the edited callers.
- **Verify**: Build exit 0; Enlistment filter `Failed: 0`.
- **Paths**: `Main/Adapters/ArmyRhythmProbeAdapter.cs Main/Adapters/IArmyRhythmProbeAdapter.cs Main/Features/Enlistment/Content/ArmyRhythmSnapshotService.cs Main/Features/Enlistment/Content/Domain/ContentConfigs.cs Main/Features/Enlistment/Content/Domain/ServiceEnums.cs Main/Features/Enlistment/Content/EnlistmentDailyService.cs Main/Features/Enlistment/Content/IEnlistmentDailyService.cs Main/Features/Enlistment/Duties/DutyOrchestrationService.cs Main/Features/Enlistment/Duties/IDutyOrchestrationService.cs Main/Features/Enlistment/EnlistmentPlayerActionService.cs Main/Features/Enlistment/Hooks/EnlistmentContentBehavior.cs Main/Features/Enlistment/Hooks/EnlistmentDutyBehavior.cs Main/Features/Enlistment/IEnlistmentPlayerActionService.cs Main/Features/Enlistment/Presentation/EnlistmentWaitMenuOptions.cs TAOM.Tests/Features/Enlistment/ArmyRhythmSnapshotServiceTests.cs TAOM.Tests/Features/Enlistment/Duties/DutyOrchestrationServiceTests.cs TAOM.Tests/Features/Enlistment/EnlistmentDailyServiceTests.cs TAOM.Tests/Features/Enlistment/EnlistmentPlayerActionServiceTests.cs TAOM.Tests/Features/Enlistment/EnlistmentSessionResetTests.cs`
- **Message** (`msg-C2.txt`): subject `refactor(enlistment): v2.0.28 - the daily column check skips unread work`;
  body: `Once a day, and when the soldier asks for a duty, the service read` /
  `the commander's column: his food, his troops' tiers and wounds, and` /
  `every kingdom's wars. Nothing in the game used those parts. The check` /
  `now reads only what the duties use (a march, a siege, a garrison, an` /
  `army, an enemy close by), and an unused hour value is gone. Nothing a` / `player sees changes.`;
  then `Ported-from: 388e89de080e043bd1b6b18665fed74a10b5e5dd`.

#### Step C3: Remove enlistment data nothing reads (`218fa4053`, #844)

- **Method**: port routine, `git cherry-pick -n 218fa4053`, resolving every conflict by the rule:
  **keep ours, then remove exactly what 218fa4053 removes in that file** (table below; read
  `git show 218fa4053 -- <file>` for each). The removals hold on 1.4.x: this session's `git grep`
  found no reader of these members outside the files below.
- **Expected conflicts**: `Content/Domain/ServiceEnums.cs`, `Content/EnlistmentBattlePayoutService.cs`,
  `EnlistmentService.cs`, `Equipment/IEquipmentIssueLedger.cs`, `Equipment/InMemoryEquipmentIssueLedger.cs`
  (modify/delete: `git rm -f` it), and the tests `EnlistmentBattlePayoutServiceTests.cs`,
  `EnlistmentMenuServiceTests.cs`, `EnlistmentReconcilerTests.cs`, `EnlistmentServiceTests.cs`,
  `Equipment/EquipmentIssueLedgerTests.cs` (modify/delete: `git rm -f` it after moving two tests, see
  below), `PersistedEquipmentIssueLedgerTests.cs`; plus `docs/features/enlistment.md`,
  `docs/reviews/lessons/gamemodels-services.md`. Also possible: the six files whose only change is
  `new EnlistmentConfigProvider()` (`CommanderLossAnnouncementTests`, `EnlistmentDiagnosticsGateTests`,
  `EnlistmentFeatureToggleTests`, `EnlistmentLoadNormalizerTests`, `EnlistmentPumpAuthorityTests`,
  `SettlementFollowingTests`), because their 1.5.x context lines carry reconciler arguments 1.4.x
  does not have: keep ours, then the sed below.
- **Drop**: `docs/features/enlistment.md docs/reviews/lessons/gamemodels-services.md`.

| File | Removed or changed |
|---|---|
| `Content/Domain/ServiceContentRecord.cs` | `DutyFailures`, `FieldRep`, `LogisticsRep`, `CommandRep`, `SiegeRep`, `BattleVictories`, `BattleDefeats`, `TournamentWins`, `ActiveDutyFoodRequired` and every line that resets, snapshots, serializes (`dutyFails=`, `repF=`, `repL=`, `repC=`, `repS=`, `wins=`, `losses=`, `tourneys=`, `dutyFood=`), parses or copies them; the two summary rewrites |
| `Content/Domain/ContentConfigs.cs` | `ServiceProgressSnapshot.DutyFailures`; the `ArmyRhythmSnapshot` summary |
| `Content/Domain/ServiceEnums.cs` | `enum ReputationDomain` |
| `Content/Domain/DutyDefinitions.cs`, `Content/Domain/MeritConfig.cs`, `Content/Domain/ProgressionConfig.cs` | `RewardSpec.RepDomain/RepAmount`; `MeritBand.RepDomain/RepAmount`, `SchedulerConfig.ReputationMax`, summary `Trust clamps`; `TrainingSessionXp` |
| `Content/TrustLedger.cs`, `Equipment/EquipmentPayoffCalculator.cs`, `Equipment/InMemoryEquipmentIssueLedger.cs` | deleted |
| `Content/ServiceRewardService.cs`, `Content/IServiceRewardService.cs` | the `RepDomain` block in `Grant`; `AdjustTrust` clamps inline against `Scheduler.TrustMin/TrustMax`; `ApplyReputation` gone |
| `Content/EnlistmentBattlePayoutService.cs` | the `BattleVictories++`/`BattleDefeats++` lines; `RepDomain`/`RepAmount` in the band `RewardSpec` |
| `Content/EnlistmentContentConfigProvider.cs` | `RepDomain`/`RepAmount` in `DefaultMeritBands`, in band validation and in `RewardValid` |
| `Duties/FieldDutyRuntime.cs`, `Duties/InteractiveDutyPresenter.cs` | `record.DutyFailures++` |
| `Content/DischargeConsequenceService.cs` | the "Confiscation only, no gold payoff" paragraph |
| `EnlistmentService.cs`, `IEnlistmentService.cs` | `EnlistmentStarted` event and its invoke block, `WithdrawPetition`, unused `using System;`, the war-mirror comment |
| `EnlistmentStateMachine.cs`, `IEnlistmentStateMachine.cs` | `Transitioned` event and invoke, unused `using System;`, summary |
| `EnlistmentConfigProvider.cs`, `IEnlistmentConfigProvider.cs` | the `IModLogger` ctor and `using`; summaries |
| `Main/Adapters/EquipmentRosterCatalogAdapter.cs`, `IEquipmentRosterCatalogAdapter.cs` | `GetItemValue` |
| `Equipment/IEquipmentIssueLedger.cs` | summary only (keep the 1.4.x `EnlistmentRank` member names) |
| `ModuleData/enlistment/enlistment_config.json`, `enlistment_duties.json` | `trainingSessionXp`, `reputationMax`, every `repDomain` and `repAmount` key (the pick applies these cleanly on 1.4.x) |

- **Tests** (adapt 1.5.x names: `EnlistmentKitTier` is `EnlistmentRank`, `HighestIssuedTier` is
  `HighestIssuedRank`, `HasIssuedForTier` is `HasIssuedForRank`; 1.5.x rank `Private` is `Recruit`,
  and `rank=2` parses to `ServiceRank.Veteran` on 1.4.x):
  - Move `RecordIssue_AccumulatesDuplicateItemInstances` and
    `RecordIssue_NullOrEmptyItemLists_ToleratedButRankStillRecorded` from
    `Equipment/EquipmentIssueLedgerTests.cs` into `PersistedEquipmentIssueLedgerTests.cs` (against its
    `_ledger`), change its message `"duplicate instances preserved for the payoff sum"` to
    `"duplicate instances preserved for the per-item reclaim"`, then `git rm -f` the old file;
    `git rm -f TAOM.Tests/Features/Enlistment/Equipment/EquipmentPayoffCalculatorTests.cs`.
  - `Equipment/EnlistmentEquipmentServiceTests.cs`: the ledger becomes
    `new PersistedEquipmentIssueLedger(new EnlistmentContentStore(Substitute.For<IModLogger>()))`.
  - `EnlistmentContentConfigProviderTests.cs`: add `GetConfig_RetiredKeys_LoadWithoutWarning` and
    `GetDuties_RetiredRewardKeys_LoadWithoutWarning` with `"minRank":"Recruit"`. If either fails for
    a reason unrelated to the retired keys (the 1.4.x validator rejecting something else in the
    fixture), change only that part of the fixture; the retired keys must stay in it.
  - `EnlistmentContentPureLogicTests.cs`: remove the three `TrustLedger` tests and the retired fields
    from the round-trip initializer; add `ContentRecord_SaveWithRetiredKeys_StillLoads` asserting
    `ServiceRank.Veteran`.
  - `EnlistmentServiceTests.cs`, `EnlistmentStateMachineTests.cs`: remove the petition-withdrawal,
    `EnlistmentStarted` and two `Transitioned` tests. `ServiceRewardServiceTests.cs`: remove the
    reputation lines, add `AdjustTrust_ClampsAtConfiguredFloor`. `FieldDutyRuntimeTests.cs`,
    `InteractiveDutyPresenterTests.cs`, `EnlistmentBattlePayoutServiceTests.cs`: remove the
    `DutyFailures`/`BattleVictories`/`BattleDefeats` asserts and apply the three renames.
  - Every `new EnlistmentConfigProvider(<x>)` becomes `new EnlistmentConfigProvider()`:
    `git grep -l "new EnlistmentConfigProvider([a-zA-Z_]" -- TAOM.Tests Main` then
    `sed -i -E 's/new EnlistmentConfigProvider\([A-Za-z_]+\)/new EnlistmentConfigProvider()/g'` on each.
- **Check**: `git grep -n -E "DutyFailures|FieldRep|BattleVictories|TournamentWins|ReputationDomain|TrustLedger|EquipmentPayoffCalculator|InMemoryEquipmentIssueLedger|WithdrawPetition|EnlistmentStarted|Transitioned|GetItemValue|TrainingSessionXp|ReputationMax" -- Main TAOM.Tests`
  prints nothing.
- **Keep**: `docs/modding/configs-balance.md` (its row now lists `trust`, `relation`, `renown`) and
  `docs/reviews/rca-enlistment-dead-surface-2026-10-26.md`.
- **Verify**: Build exit 0; Enlistment filter `Failed: 0`; Data no new ERROR.
- **Paths** (under `Main/Features/Enlistment/` unless a root is given): `Main/Adapters/EquipmentRosterCatalogAdapter.cs Main/Adapters/IEquipmentRosterCatalogAdapter.cs Content/DischargeConsequenceService.cs Content/Domain/ContentConfigs.cs Content/Domain/DutyDefinitions.cs Content/Domain/MeritConfig.cs Content/Domain/ProgressionConfig.cs Content/Domain/ServiceContentRecord.cs Content/Domain/ServiceEnums.cs Content/EnlistmentBattlePayoutService.cs Content/EnlistmentContentConfigProvider.cs Content/IServiceRewardService.cs Content/ServiceRewardService.cs Content/TrustLedger.cs Duties/FieldDutyRuntime.cs Duties/InteractiveDutyPresenter.cs EnlistmentConfigProvider.cs EnlistmentService.cs EnlistmentStateMachine.cs Equipment/EquipmentPayoffCalculator.cs Equipment/IEquipmentIssueLedger.cs Equipment/InMemoryEquipmentIssueLedger.cs IEnlistmentConfigProvider.cs IEnlistmentService.cs IEnlistmentStateMachine.cs Main/_Module/ModuleData/enlistment/enlistment_config.json Main/_Module/ModuleData/enlistment/enlistment_duties.json`;
  under `TAOM.Tests/Features/Enlistment/`: `CommanderLossAnnouncementTests.cs Duties/FieldDutyRuntimeTests.cs Duties/InteractiveDutyPresenterTests.cs EnlistmentBattlePayoutServiceTests.cs EnlistmentContentConfigProviderTests.cs EnlistmentContentPureLogicTests.cs EnlistmentDiagnosticsGateTests.cs EnlistmentFeatureToggleTests.cs EnlistmentLoadNormalizerTests.cs EnlistmentMenuServiceTests.cs EnlistmentPumpAuthorityTests.cs EnlistmentReconcilerTests.cs EnlistmentServiceTests.cs EnlistmentStateMachineTests.cs Equipment/EnlistmentEquipmentServiceTests.cs Equipment/EquipmentIssueLedgerTests.cs Equipment/EquipmentPayoffCalculatorTests.cs PersistedEquipmentIssueLedgerTests.cs ServiceRewardServiceTests.cs SettlementFollowingTests.cs`,
  any other test file the `EnlistmentConfigProvider` sed changed, `docs/modding/configs-balance.md`
  and `docs/reviews/rca-enlistment-dead-surface-2026-10-26.md`. `git status --porcelain` shows five
  deletions as `D `: `TrustLedger.cs`, `EquipmentPayoffCalculator.cs`, `InMemoryEquipmentIssueLedger.cs`,
  `EquipmentIssueLedgerTests.cs`, `EquipmentPayoffCalculatorTests.cs`.
- **Message** (`msg-C3.txt`): subject `refactor(enlistment): v2.0.28 - remove enlistment data nothing reads`;
  body: `Enlistment kept data that nothing in the game read: battle win and` /
  `loss counters, a duty failure count, tournament wins, a duty's food` /
  `requirement, a training value, four reputation scores that were` /
  `updated but never shown or used, two events nobody listened to, an` /
  `unused petition withdrawal and an equipment payoff calculator that was` /
  `never called. They are removed, with their entries in the enlistment` /
  `settings files. Nothing a player sees changes.`; trailers
  `Save-compat: old saves still load; the retired record keys are ignored by ServiceContentRecord.TryParse (pinned by ContentRecord_SaveWithRetiredKeys_StillLoads)`
  (wrap at 72) and `Ported-from: 218fa405376cb3072cc82c62667214165f41295c`.

#### Step C4: Duty text has one source, in every language (`a20b4d154`, #845)

- **Method**: port routine, `git cherry-pick -n a20b4d154`.
- **Expected conflicts**: `Domain/ServiceStatusModel.cs`, `Presentation/ServiceStatusTextWriter.cs`,
  `ServiceStatusService.cs`, `TAOM.Tests/Features/Enlistment/EnlistmentContentConfigProviderTests.cs`,
  `TAOM.Tests/Features/Enlistment/ServiceStatusServiceTests.cs`, `docs/modding/file-catalogue.md`,
  `docs/reviews/lessons/localization-ui.md`, `tools/README.md`. The JSON word additions apply cleanly
  (13 field, 11 interactive and 3 incident rows, same ids in the same order on both lines).
- **Drop**: `docs/reviews/lessons/localization-ui.md tools/README.md docs/features/enlistment.md`.
- **Resolve**:
  - `ServiceStatusModel.cs`: keep ours; add the `ActiveDutyTitle` property and summary from the pick
    after `ActiveDutyId`, the ctor parameter `string activeDutyTitle = null` after
    `int nextRequirementTarget = 0` (1.4.x has no `kitWaits`, `columnText`, `retinueWage`), its
    assignment, `&& string.Equals(ActiveDutyTitle, other.ActiveDutyTitle, StringComparison.Ordinal)`
    at the end of `Equals`, and `hash = (hash * 397) ^ (ActiveDutyTitle?.GetHashCode() ?? 0);` after
    the `NextRequirementTarget` hash line.
  - `ServiceStatusTextWriter.cs`: keep ours (with WP125's flat render when K125 = 1); change the
    `DUTY` line to `WithKitLine(DutyLine(status.ActiveDutyTitle), status)` if `WithKitLine` exists,
    else `DutyLine(status.ActiveDutyTitle)`; replace the `DutyLine` method with the pick's
    `DutyLine(string activeDutyTitle)` (it renders `new TextObject(activeDutyTitle)`).
  - `ServiceStatusService.cs`: keep ours; do NOT add a ctor parameter (1.4.x already has `_config`);
    add `activeDutyTitle: ActiveDutyTitle(content.ActiveDutyId)` as the last named argument of
    `new ServiceStatusModel(...)` (after `nextRequirementTarget:`), and the pick's private
    `ActiveDutyTitle(string activeDutyId)` method using `_config`.
  - `ServiceStatusServiceTests.cs`: keep ours; in `Setup` add
    `_config.GetDuties().Returns(new EnlistmentDutiesConfig());` after the `GetConfig` stub; add the
    pick's `Build_ActiveFieldDuty_CarriesTheLoadedRowsOwnTitle` and
    `Build_ActiveDutyWhoseRowIsGone_CarriesTheComposedFallback_AndNoDutyHasNone`; in
    `ServiceStatusModelEqualityTests` add `[DataRow("activeDutyTitle")]` to both data tests and the
    argument `activeDutyTitle: field == "activeDutyTitle" ? "{=taom_enlist_duty_recon_sweep_title}a reconnaissance sweep" : null`
    to both `new ServiceStatusModel(...)` calls. Skip the pick's `TheBoardWriter_AppendsTheColumnLine_*`
    hunk (that test is not on 1.4.x).
  - `EnlistmentContentConfigProviderTests.cs`: keep ours; add the pick's `FieldRow`, `PopupRow`,
    `Title`, `Body`, `LabelA`, `LabelB` members and its seven new tests, with `"minRank":"Private"`
    changed to `"minRank":"Recruit"`. Then every duty row a test writes must carry valid text,
    because invalid rows are now skipped: add
    `"title":"{=taom_enlist_duty_<id>_title}x","success":"{=taom_enlist_duty_<id>_success}x","failure":"{=taom_enlist_duty_<id>_failure}x",`
    after `"id":"<id>",` in each field row, and `"title"`, `"body"` and both option `"label"` values
    (`_opta`, `_optb`) in each interactive or incident row, as the pick does for the rows it knew.
    Rows a test deliberately leaves malformed stay as they are.
  - `docs/modding/file-catalogue.md`: keep ours; replace `tools/generate_enlistment_duty_strings.py`
    with `tools/harvest_duty_strings.py` in its two rows.
- **Keep**: `Content/Domain/DutyText.cs` (new), `Content/Domain/DutyDefinitions.cs`,
  `Content/EnlistmentContentConfigProvider.cs`, `Duties/FieldDutyRuntime.cs`,
  `Duties/InteractiveDutyPresenter.cs`, `Main/Adapters/IInquiryAdapter.cs` (comment),
  `enlistment_duties.json`, `taom_enlistment_strings.xml` (three marker comments only),
  `tools/harvest_duty_strings.py`, `tools/tests/test_harvest_duty_strings.py`, the deletion of
  `tools/generate_enlistment_duty_strings.py`, `tools/generate_translation_template.py`,
  `tools/harvest_literal_loc_keys.py`, `TAOM.Tests/Infrastructure/Localization/UnregisteredLocalizationKeyBaselineTests.cs`,
  `TAOM.Tests/Features/Enlistment/DutyTextTests.cs` (new), `Duties/FieldDutyRuntimeTests.cs`,
  `Duties/InteractiveDutyPresenterTests.cs`, `docs/localization/TRANSLATOR_GUIDE.md`,
  `docs/modding/configs-balance.md`, `docs/reviews/rca-enlistment-duty-text-2026-10-26.md`.
- **Verify** (all must hold):
  - `"$PY" tools/harvest_duty_strings.py --check` exits 0 and reports no change. This session checked
    all 95 duty texts of the pick's JSON against the 1.4.x registry English: 0 mismatches, and every
    popup `_success`/`_failure` row is registered. Any change reported is a STOP; never `--apply`.
  - Build exit 0; Enlistment filter `Failed: 0` (includes `FieldDutyReachabilityTests`, which reads the
    shipped JSON, and `DutyTextTests`); Tool tests: no new failure vs U0 and
    `tools.tests.test_harvest_duty_strings` passes.
- **Paths** (26; under `Main/Features/Enlistment/` unless a root is given): `Main/Adapters/IInquiryAdapter.cs Content/Domain/DutyDefinitions.cs Content/Domain/DutyText.cs Content/EnlistmentContentConfigProvider.cs Domain/ServiceStatusModel.cs Duties/FieldDutyRuntime.cs Duties/InteractiveDutyPresenter.cs Presentation/ServiceStatusTextWriter.cs ServiceStatusService.cs Main/_Module/ModuleData/enlistment/enlistment_duties.json Main/_Module/ModuleData/taom_enlistment_strings.xml TAOM.Tests/Features/Enlistment/Duties/FieldDutyRuntimeTests.cs TAOM.Tests/Features/Enlistment/Duties/InteractiveDutyPresenterTests.cs TAOM.Tests/Features/Enlistment/DutyTextTests.cs TAOM.Tests/Features/Enlistment/EnlistmentContentConfigProviderTests.cs TAOM.Tests/Features/Enlistment/ServiceStatusServiceTests.cs TAOM.Tests/Infrastructure/Localization/UnregisteredLocalizationKeyBaselineTests.cs docs/localization/TRANSLATOR_GUIDE.md docs/modding/configs-balance.md docs/modding/file-catalogue.md docs/reviews/rca-enlistment-duty-text-2026-10-26.md tools/generate_enlistment_duty_strings.py tools/generate_translation_template.py tools/harvest_duty_strings.py tools/harvest_literal_loc_keys.py tools/tests/test_harvest_duty_strings.py`
  (`tools/generate_enlistment_duty_strings.py` shows as `D `).
- **Message** (`msg-C4.txt`):
  ```
  fix(enlistment): v2.0.28 - duty text has one source, in every language

  A player using English read "a task for the company" for every field
  duty order and "It went well." for every result, because the game
  shows English straight from the text in the code and never reads the
  translation file. Each duty's words now live with the duty in the
  enlistment duty file, with their translation keys, so English players
  see "a reconnaissance sweep" and the line the duty was written with.
  A tool keeps the translation file in step with the duty file; every
  key and English line is unchanged, so no translation moves. A duty row
  with missing or malformed words is skipped with a warning naming the
  field.

  Research: v1.4.8 LocalizedTextManager.LoadLanguage reads strings only
  when the language is not English (:235, :247)
  Not-tested: the duty toasts and board in English and German, in game
  Ported-from: a20b4d1540b782bde877b0333070b55aa38ea402
  ```

#### Step C5: In-game checks and the Part C doc, then the final gate (`4bd8554a4`, adapted)

1. In `docs/features/enlistment.md`: in the `## Testing` part, the paragraph that says
   `` `tools/generate_enlistment_duty_strings.py` exists `` now names `tools/harvest_duty_strings.py`
   and says the English lives in `enlistment_duties.json` (one sentence; keep the counts). Append to
   the WP128 section:

```markdown
### The #842 to #845 batch

`IsCommanderParty` compares the commander's party id through `GetPartyId` (#842). The army-rhythm
probe reads only what duties use, and the unused hour parameter is gone (#843). Dead data is
removed: battle and tournament counters, duty failures, the four reputation scores and
`TrustLedger`, two unused events, petition withdrawal, `EquipmentPayoffCalculator` and the
in-memory ledger; old saves load because `ServiceContentRecord.TryParse` ignores unknown keys
(#844). Duty words live in `enlistment_duties.json` as `{=key}English` strings, `DutyText` owns
the key formula, and `tools/harvest_duty_strings.py --check` proves the registry is in step; v1.4.8
shows English inline text and never reads string files for English (`LocalizedTextManager.cs:235`)
(#845). Reviews: `rca-enlistment-dead-surface-2026-10-26.md`, `rca-enlistment-duty-text-2026-10-26.md`.

**Owed in-game checks (WP128).** See `plans/128-enlistment-fixes-a.md`, "In-game checks owed".
```

2. **Verify**: Docs command as in A5. Commit `docs/features/enlistment.md` with `msg-C5.txt`
   (subject `docs(enlistment): v2.0.28 - the batch's in-game checks and port notes`; two-line body;
   `Ported-from:` lines for `f21123c6a...`, `388e89de0...`, `218fa4053...`, `a20b4d154...` and
   `4bd8554a4888fc709b12e7894527848708632379`).
3. **Final gate**: Build exit 0; Test `Failed: 0` (record TC); Data no new ERROR; Tool tests no new
   failure vs U0; Snapshot exit 0 (when S0 was 0); Docs `ai_dashes: 0`, L0 holds;
   `git log --oneline bannerlord-1.4.5..HEAD` lists 16 commits, every subject carrying the version
   label.

## Disposition (every commit in the pack)

| Commit | Subject | Group | Disposition |
|---|---|---|---|
| `2d1e5f3385f871c5a829611641230266b87fb763` | fix: no walking out of the commander's battle | issue-827 | **Ported** (A1); lessons and `patch-targets.md` resolved, v1.5.5 claims re-cited, co-op sentence dropped (D7) |
| `b24e8549e88e8b0b465bc1615f4335855a3486d9` | fix: a way out of the service menu after discharge | issue-828 | **Ported** (A2); language rows re-added for the 1.4.x layout; `doc-lookup.md` hunk dropped (WP142) |
| `7a912dcd544c1c3560b52de2df571f406131a782` | fix: soldiers stay out of their lord's raids | issue-831 | **Ported** (A3), after A1 per the orchestrator note |
| `c04ba3fe58a39c2538dbb5a0708393e63d21b1e9` | feat: soldiers fight lords who relieve a raid | issue-831 | **Ported** (A4); engine refs re-cited; `VillageRaidReaderTests` adapted to v1.4.8 (`_mapEventType`, `_sides`) |
| `1559b3261c2087149f7811bebeca8d7d7c7fc309` | feat: a soldier's wars follow his lord's | issue-832 | **Adapted** (B1): join-time top-up, `FollowWarOn`, landed checks, `GetPlayerClanId` (1.4.x adapter gap). Skipped by P128-1: hourly follow (reconciler, its ctor argument in six test files, `EnlistmentHourlyTickTests`), crown-war sit-out and release, `ShowNotice`, `DischargeReason.CommanderAtWarWithYourRealm`, `ServiceWarPolicy.Diff`, the string, its RCA, the WotR doc paragraph |
| `d4af27d3e464b51eb50de410e28fe081643a5db9` | fix: vassals stay out of their lord's new wars | issue-832 | **Ported** (B2); placed without the crown-war block; map-event-guard sentence only with WP111's section |
| `9304a4cb941d577cd4b2d5810af13529e313ebf6` | feat: spoils from the foes you cut down | merit-hero-attribution | **Adapted** (B3, TDD): the two `PlayerCharacter` guards and two source pins (new test file). Skipped: personal spoils, `BattleLootAdapter`, config, strings, the kill-id queue, the `ResetForNewSession` rename, its RCA (feature) |
| `75e190abe685cba1b403af8847cb4da9d0b944c0` | feat: promotions get a notice, kit gets a line | issue-835 | **Adapted** (B4, TDD): the grace-state guard and its test. Skipped: the notice, `QuickNoticeAdapter`, kit tiers, strings, its RCA (feature) |
| `f21123c6aed80344e47a52f5468195a0c3561997` | perf: the commander-party check is an id compare | batch-842-845 | **Ported** (C1, clean) |
| `388e89de080e043bd1b6b18665fed74a10b5e5dd` | refactor: the daily column check skips unread work | batch-842-845 | **Ported** (C2); one test file hand-merged |
| `218fa405376cb3072cc82c62667214165f41295c` | refactor: remove enlistment data nothing reads | batch-842-845 | **Ported** (C3) by re-derivation on the 1.4.x files; tests adapted to 1.4.x rank and ledger names |
| `a20b4d1540b782bde877b0333070b55aa38ea402` | fix: duty text has one source, in every language | batch-842-845 | **Ported** (C4); status-board files hand-merged; lessons and `tools/README.md` hunks dropped (WP142) |
| `4bd8554a4888fc709b12e7894527848708632379` | docs: the batch's in-game checks and feature map | batch-842-845 | **Adapted** (C5): in-game checks moved to this plan and the feature doc; `feature-map.md` hunk dropped (bookkeeping, and its #832 hourly-follow wording is false on 1.4.x) |
| `90035a799207619c134c1fe84d495fd8cde6d50b` | fix: no battle encounter is left without a way out | issue-1021 | **Ported** (B5), ahead of the batch; comments re-cited to v1.4.8 |

## Test plan

- New or extended test classes come with each pick (named per step). New files: 
  `MapEventHelperCanMainPartyLeaveBattlePatchTests`, `EnlistmentServiceMenuWiringTests`,
  `VillageActionJoinPolicyTests`, `TAOM.Tests/Adapters/VillageRaidReaderTests`,
  `MeritHeroAttributionWiringTests` (B3, written here), `ArmyRhythmSnapshotServiceTests`,
  `DutyTextTests`, `tools/tests/test_harvest_duty_strings.py`.
- TDD for the hand-ports: B3 two source pins fail before the guards; B4's unit test promotes to
  `Soldier` before the guard.
- Pattern files: `EnlistmentBattlePayoutServiceTests` for payout tests; `PersonalSpoilsWiringTests`
  on 1.5.x for source pins.
- Structurally untestable (for `Not-tested:`): the Harmony postfix inside a live menu, raid and relief
  battles, the war declarations, take-over after falling, the stuck states of #1021, the English
  board text in game.
- Expect the C# totals to rise by roughly 60 (Part A), 45 (Part B) and 25 (Part C, net of about 24
  removed tests), and the tool tests by about 43; record the exact figures. The gate is `Failed: 0`
  plus the named tests present, not an exact count.

## Done criteria

Part A:
- [ ] 5 commits on `port148/128-enlistment-fixes-a`; `git grep -n "ShouldBlockBattleExit\|VillageRaidReader\|LeaveAfterDischarge" -- Main` matches.
- [ ] Build exit 0; Test `Failed: 0`; `VillageRaidReaderTests` 6 passed; `LanguageFileCoverageTests` passes.
- [ ] Snapshot `-Check` exit 0 (when S0 was 0); Docs `ai_dashes: 0`.
Part B:
- [ ] 6 more commits; `git grep -n -E "FollowCommanderWars|IsServingAgainstOwnCrown|ShowNotice\(" -- Main TAOM.Tests` prints nothing; `git grep -n "MirrorWarAtJoin\|ShouldSitOutAsVassal\|IsJoinedBattleEncounter\|ForgetEndedBattleResult" -- Main` matches.
- [ ] `grep -c "CharacterObject.PlayerCharacter" Main/Features/Enlistment/Hooks/EnlistmentMeritMissionBehavior.cs` = 2.
- [ ] Build exit 0; Test `Failed: 0`.
Part C:
- [ ] 5 more commits; `test ! -e tools/generate_enlistment_duty_strings.py && test -f tools/harvest_duty_strings.py`.
- [ ] `python tools/harvest_duty_strings.py --check` exit 0; the C3 removal grep prints nothing.
- [ ] Build exit 0; Test `Failed: 0`; Tool tests no new failure; Data no new ERROR.
Always:
- [ ] `git diff --name-only bannerlord-1.4.5...HEAD -- Main/IoC.cs Main/SubModule.cs Main/TAOM.csproj CHANGELOG.md .claude docs/reviews/lessons tools/README.md docs/reference/feature-map.md` prints nothing.
- [ ] No em or en dash in any commit body: `git log --format=%B bannerlord-1.4.5..HEAD | "$PY" -c "import sys; t=sys.stdin.buffer.read().decode('utf-8'); print(t.count(chr(0x2014)) + t.count(chr(0x2013)))"` prints `0`.

## STOP conditions

Stop and report (do not improvise) if:

- An A0 prerequisite line is missing (WP104, WP105 or WP119 not merged), or the A0.4 grep matches
  (this WP is partly applied already).
- A pick conflicts in a `Main/` or `TAOM.Tests/` path the step does not list, and the conflict is not
  explained by a WP in "What earlier WPs put in place" (WP125's status-board edits, WP111's adapter).
- A step seems to need `Main/IoC.cs`, `Main/SubModule.cs`, `Main/TAOM.csproj`,
  `Directory.Build.props` or `.claude/settings.json`.
- `EnlistmentContainerWiringTests` fails for an unresolved type other than `IPlayerContextAdapter`.
- `add_lang_rows.py` or `drop_tests.py` prints `STOP:`; or `LanguageFileCoverageTests` fails after
  the rows are added.
- After B1's trim, the forbidden-name grep matches anything.
- `harvest_duty_strings.py --check` reports any change (a changed English line would make 12
  translations stale): never run `--apply`.
- A test this plan does not touch starts failing, or a step's verification fails twice after a
  reasonable fix attempt.
- A taom-src signature differs from the engine table (for example `MapEvent.IsRaid` no longer reads
  `_mapEventType`, or `PlayerEncounter.CampaignBattleResult` loses its setter).
- Snapshot `-Check` fails in A1 when S0 was 0, for a row other than the new one.

## In-game checks owed (maintainer)

Debug log: `Logs/taom_debug_*.log` (lines tagged `[Enlistment]` and `[EnlistDiag]`); console
commands `taom.service_status` and `taom.rescue_service` exist on 1.4.x.

1. **Battle exits (#827).** Join a commander's battle through his army: the encounter menu shows
   neither 'Abandon army' nor 'Leave'. With a kingdomless commander (merge fails): no 'Leave'. Under
   a safe-passage truce with the enemy faction and no army, 'Leave' shows again; after it, the state
   returns to `EnlistedAttached` once the commander's battle ends.
2. **Discharge during load (#828).** Switch "Enable Enlistment" off in MCM, save on the map with the
   service menu open, load: the body reads "Your service has ended." with one option "Go on your way",
   landing on the map. Repeat once in German (the English seed text shows).
3. **Raids (#831, #954).** Serve a lord who raids a village: `taom.service_status` stays
   `EnlistedAttached`, the board says "The column is raiding.", release can be asked; a raid on the
   lord's own village he defends is joined. When a lord with troops relieves the raided village, the
   soldier joins that fight and afterwards returns to normal service.
4. **Wars (#832, #960).** As an independent clan, enlist, let the lord's realm declare a new war,
   then join a battle against that faction: the log shows "topped up the war with '<id>' before a
   join", and after discharge "service war with '<id>' ended". As a vassal of another kingdom, the
   lord fighting a faction your kingdom is at peace with: "sitting this battle out", no new war.
5. **Merit (#834).** Fall early in an enlisted field battle, take over a soldier and kill a few
   enemies with him: the log line `battle merit ... (kills=N, won=...)` counts only the hero's own
   kills (0 if the hero killed nobody before falling).
6. **Encounter exits (#1021).** Defend the lord's town against an assault until the attackers
   retreat: within three in-game hours the log shows "releasing a stale EnlistedBattle latch inside
   a settlement", the next assault logs "cleared the last battle's result from the reused encounter
   (#1021)" then "joined commander battle on side Defender", and that assault is fought. Save at an
   assault's encounter menu as a defender, switch Enlistment off, load: "feature disabled: waiting
   for the battle to end before releasing", then the release after the battle.
7. **Duty text (#845).** In English, ask for work until a field duty is assigned: the order toast
   and the board name the duty ("a reconnaissance sweep"), and its result toast is the duty's own
   line. Night patrol, an incident and their choices read as before. Repeat in Deutsch. The log has
   no "duty '...' skipped" line.
8. **Cleanups (#842 to #844).** An existing save with an enlisted soldier loads; duties are still
   offered by the column's state; battle pay, trust and promotion work as before.

## Maintenance notes

- **WP132 order.** On 1.5.x, `1d9cc42f1`, `5c5072dbc`, `5a8c73d26`, `1a132c727`, `01f02c3a0`,
  `4666a254a`, `069962aa4`, `b0625f0d1`, `5e7a0da6e` and `3b848c411` sit between or before commits
  this WP ports; WP132 applies them on top. Expect conflicts there in `ServiceBattleService.TryJoin`
  (raid floor, vassal sit-out, top-up order), `EnlistmentReconciler.BreakStaleBattleLatch` and the
  normalizer comment (B5 landed before `3b848c411`, keep the #1021 sentences),
  `ServiceStatusModel` (on 1.4.x `activeDutyTitle` is the last ctor parameter; `retinueWage` will
  join before it), and the ledger tests C3 moved (the `EnlistmentKitTier` rename touches them).
- **P128-1 follow-up.** Porting the hourly war follow and the crown-war release later needs
  `IInquiryAdapter.ShowNotice`, `DischargeReason.CommanderAtWarWithYourRealm`, the
  `taom_enlist_crown_war_release` string in 13 files, the reconciler's `OnHourlyTick` and
  `ServiceWarPolicy.Diff`. WP142 must not copy the 1.5.x feature-map wording that claims them.
- **Docs sync (WP142).** `tools/README.md` still lists `generate_enlistment_duty_strings.py`;
  `docs/reference/doc-lookup.md` and the lessons entries of these commits were not synced.
- **Translations (WP135).** Three new keys show English until `/localize`:
  `taom_enlist_menu_discharged_leave`, `taom_enlist_wait_discharged`, `taom_enlist_act_raid`.
- **Review focus** (recommend `/deep-review` per part; C# across 2 or more files): the guard order in
  `ServiceBattleService.TryJoin`; that `MirroredWars` entries added at join are exactly what discharge
  unwinds; the widened `BreakStaleBattleLatch` predicate and the R1b-inert assumption; `DutyText`
  fallbacks; C3 save compatibility. Recommend `/verify-bindings` after Part A (new Harmony target).
- **Release.** No `TAOM.Dependencies` change, so no #371 pairing concern from this WP.

## Hand-off report (put in your final message)

Status (DONE, or the Part reached and the STOP hit); branch tip hash; the 16 commit hashes and subjects; T0, E0, U0, S0, L0; TA, TB, TC (total, passed,
skipped, failed); K119w, K106, K125, K111; any step where an expected conflict did not occur or an extra
one did; the C1 caller list; whether `docs/features/map-event-guard.md` was edited in B2; the
`harvest_duty_strings.py --check` output. The integrator records these against row 128 of
`plans/README.md`; this WP closes no old-plan row.
