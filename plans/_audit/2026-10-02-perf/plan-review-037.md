# Plan review 037, round 1 (cold reviewer)

Plan: `plans/037-campaign-hot-paths.md` (2201 lines). Code read from the program worktree at HEAD
`64dbcac8`; `git diff --stat 0912e1b7..HEAD -- Main TAOM.Tests docs/features/caravan-trade.md
docs/features/culture-marketplace.md docs/reference/harmony-patch-registry.md` is empty, so every
excerpt was compared against `git show 0912e1b7:<path>`. Engine facts were compared against the
cached v1.5.3 decompiles in the taom-src cache (`CaravansCampaignBehavior`, `AiHelper`,
`RecruitmentCampaignBehavior`, `MobilePartyHelper`, `CultureObject`, `Town`, `MBObjectManager`).
No earlier review file exists for this plan. I did not run dotnet (read-only role).

## Verdict

Executable by a weak model: yes. No blocking items. The excerpts, line numbers, engine facts,
TaomSettings defaults, DI registrations, test anchors and doc anchors all match the planned-at tree.
The non-blocking items below would each cost an executor a retry or leave a stale claim behind; N1
to N4 are worth fixing before dispatch.

## Excerpt check (item 3): no mismatches

Checked and matching: every provider read and constructor in the Stage A table (plan 116-133,
including `RealmBordersSettingsProvider.cs:90-116, :122, :224` and `FieldCommissionSettingsProvider.cs:18-20,
:45-48, :50, :113-124`), the eight IoC registrations (`*IoC.cs:10/11/12`, `FieldCommissionIoC.cs:38-40`),
the 15 TaomSettings defaults and their lines (plan 143-150), `Patch42_HourlyTickParty_Postfix.cs:57-77`,
`AlignmentDesertionBehavior.cs:84-102` (file is 142 lines), `AlignmentDesertionService.cs:26-51`,
`RefugeCampaignBehavior.cs:152-163, :192-211` (file is 218 lines), `RefugeService.cs:161, :487-490,
:522-525`, `TaomPartySpeedModel.cs:22-43, :70-80`, `CulturalFeatsService.cs:134-143`,
`ICulturalFeatsService.cs:39-40`, `CultureMarketplaceMaintenanceService.cs:26-43, :53-77`,
`TownRosterAdapter.cs:59-86`, `ITownRosterAdapter.cs:13-15`, `CultureItemPoolService.cs:44, :197-203,
:224-242`, `CultureMarketplaceConfigProvider.cs:62, :228`, `CultureItemPool.cs:16-18`, the Patch59
postfix (lines 20-63), `ICaravanTradeService.cs:48`, `caravan-trade.md:25, :98`, registry lines 315,
496 and 498, `culture-marketplace.md:56, :95, :123, :157`, the decompiled `GetTradeScoreForTown`
(line 966, first two statements 968-969, -1 on every rejection), `FindNextDestinationForCaravan`
(912-940), `ThinkNextDestination` (901-909), `AiHelper` signature (line 11),
`RecruitmentCampaignBehavior.HourlyTickParty` (300, deref on 302), `MobilePartyHelper` (266),
`CultureObject.HasFeat` (245), `Town.OnInventoryUpdated` (753), `MBObjectManager` (366, 373).
Step 2's premise greps give exactly the planned output (one BattleBalance line; nothing; nothing;
the one `FieldCommissionIoC.cs:39` line). `python -B tools/lint_docs.py --fail-on-drift` exits 0 on
this tree. Patch59 is applied once, in `OnSubModuleLoad` after `IoC.Configure()` (`SubModule.cs:120`,
`:578`), as the plan says.

## Item 4 checklist

TDD order holds for every C# stage (RED by compile or by assertion before each GREEN). Issue line
present (orchestrator files it). ADR-002/003/004/005/007/008 named with one line each. No protected
file, no Step 0, and Scope says so. IoC.cs, SubModule.cs, TAOM.csproj and every `*IoC.cs` are out of
scope with a STOP and a done-criterion diff. STOP conditions are specific to this plan's risks.
Planned-at `0912e1b7` is filled in and the drift-check paths cover every Scope path. Every dotnet
command carries both `-p:DisableModuleCopy=true -p:ModuleId=`. No worktree path, no branch name, no
CHANGELOG step. No em or en dash in prose (the two found, lines 139 and 201, sit inside verbatim code
excerpts). No secret values.

## Blocking

None.

## Non-blocking

- **N1 (lines 1386-1390): `FilterForeignCultureItems_PoolIdSetIsBuiltOncePerPool` never says to give
  the roster a row.** `FilterForeignCultureItems` returns at `snapshot.Count == 0`
  (`CultureMarketplaceMaintenanceService.cs:59`) before it reaches the pool, and an unstubbed
  `EnumerateRoster` on an NSubstitute fake returns an empty list. Written as described, the test reads
  the indexer 0 times and fails after GREEN, which reads as "the cache is wrong". The routed test (1383-1385)
  says "roster snapshot with one row so the filter reaches its lookups"; add the same sentence here.

- **N2 (lines 1411-1460, 1551, 2094): a comment the plan leaves stale.** `TownRosterAdapter.cs:104`
  says "Mirror GetItemCount above" inside `RemoveItem`; Step 19 deletes `GetItemCount`, and the new
  comment it supplies (1434-1435) points at "the 2026-05-21 deep-review note this replaces", which the
  same step deletes. Both greps for `GetItemCount(` miss them (no parenthesis). Add `:104` to Step 19
  ("Mirror the stack walk in GetItemCounts above") and keep the 2026-05-21 rationale in one self-contained line.

- **N3 (lines 2094-2095): the doc half of the GetItemCount done criterion cannot fail.**
  `docs/features/culture-marketplace.md` holds `GetItemCount` only as `GetItemCount /` (:56),
  `` GetItemCount` `` (:95) and `` TownRosterAdapter.GetItemCount` `` in the changelog entry (:225), none
  followed by `(`. So the grep passes before Step 20 too. Use
  `git grep -n -w "GetItemCount" -- docs/features/culture-marketplace.md` and expect only line 225 (the
  dated changelog entry, which is history).

- **N4 (lines 2096-2097): the stale-claim grep is narrower than the quality bar.** It searches only
  `docs Main`, is case-sensitive, and so misses the patch class summary's "SAME public inputs"
  (`CaravansCampaignBehavior_GetTradeScoreForTown_Patch.cs:16`; Step 24.1 does rewrite it, but nothing
  checks that). Use `git grep -n -i -e "same public inputs" -e "recomputes the distance via" -e "recomputes raw travel days" -- . ':!docs/reviews' ':!plans'`
  and expect nothing (the template's "whole repo, tests included").

- **N5 (Scope 675-684): line references Stage A makes stale, outside Scope.** Adding the comment,
  field, accessor and constructor shifts every getter: `docs/modding/file-catalogue.md:257`
  (`AlignmentDesertionSettingsProvider.cs:19-29`), `:262` (`CaravanTradeSettingsProvider.cs:21-33`,
  `:25`), `:263` (`CastleRecruitmentSettingsProvider.cs:20-25`); `docs/modding/configs-balance.md:209`
  and `:216` (`FieldCommissionSettingsProvider.cs:87`, `:104`, which move once `Capture` changes
  shape). Review 031 recorded the same gap for plan 031. Either add both files to Scope and the drift
  check with member-name references, or list them under Maintenance notes as a known follow-up.
  `quick-actions.md:79` and `time-acceleration.md:187` ("Wraps/Reads `TaomSettings.Instance`") stay
  roughly true.

- **N6 (lines 796-811, 899, 907, 928-932): the draft test lacks a using.** `FieldCommissionConfig`
  lives in `TAOM.Features.FieldCommission.Domain` (the provider's own `using`, line 3). Step 4 says to
  add only `TAOM.Core.Logging`, then says every error will be constructor-arity; the executor will
  also see CS0246. Add `using TAOM.Features.FieldCommission.Domain;` to the Step 4 instruction.

- **N7 (lines 1004, 1319-1320, 1579-1580, 2013-2014, 2105-2106): `git status --porcelain` "exactly".**
  The program worktree carries the run's untracked plan and review files today. If the executor's
  worktree does too, every "lists exactly the N Stage paths" check and the final "lists nothing" fail
  on files it never touched. Record a Step 1 `git status --porcelain` snapshot and compare against it,
  or restrict the check to `-- Main TAOM.Tests docs`.

- **N8 (lines 2043-2048): the RefAsm step does not say how to unset the variables.**
  `BANNERLORD_GAME_DIR` is set in this machine's shell, and shell state does not persist between tool
  calls, so "in a shell where ... are unset" needs the exact prefix:
  `env -u BANNERLORD_GAME_DIR -u BANNERLORD_OVERRIDE_DIR TEMP="<tmp>" TMP="<tmp>" dotnet build ...`
  on both commands. Without it the build records the install as `TaomGameFolder` and the run is
  partial evidence (`.ai/verification.md`).

- **N9 (lines 1225-1229, 2100-2102): the refuge line budget is exactly the snippet.** Two 4-line
  inserts are 8 lines; one habitual blank line after a `return;` breaks the "at most 8" criterion.
  Say "insert exactly these four lines, no blank line" or allow 10.

- **N10 (lines 1546-1549): the Step 19 premise greps need judgment.** State the expected output:
  `_pools` is written only at `CultureItemPoolService.cs:111` and `:115` (inside `BuildPools`, from :42),
  `_routing` only at `CultureMarketplaceConfigProvider.cs:62` and `:228` (inside `EnsureLoaded`, from
  :49). Any other assignment line is the STOP.

- **N11 (lines 1880-1888, 554-558): a re-applied category chains the transpiler twice.** The plan
  covers a rebuild from original IL, not a second registration of the same transpiler (the
  `harmony-il.md` lesson at line 35 describes exactly that chaining). The second instance sees the
  recorder, not `AiHelper`, gets `sites == 0`, sets `HandoffBound = false` and logs the WARNING
  "Vanilla IL is unchanged", which is then false: the swap from the first instance is live. Behaviour
  is still correct (the recorder runs, the postfix hits), only the critical log lies and the first-miss
  warning is disabled. `PatchCategoryApplier.TryApply` (`Main/PatchCategoryApplier.cs:38`) has no
  re-apply guard, though Patch59 is applied once today. Cheap fix: in `SwapDistanceCall`, count calls
  to `replacement` too and report "already bound" (treat as bound, no log change) when target calls
  are 0 and replacement calls are 1; add one synthetic test for it.

- **N12 (lines 1448-1452, 1588): one failed walk now zeroes every guaranteed count.** The old
  per-item `GetItemCount` isolated a failure to one item; the new catch returns zeros for all, so the
  other items would be topped up although present. Practically unreachable (`GetObject` on an
  unknown id returns null, it does not throw), but the commit body says "Stock decisions are
  unchanged". Add "except after a failed market read" or note it under Maintenance notes.

- **N13 (Step 25, lines 1974-2008): caravan-trade.md "Key Files" not updated.** Its row
  `Main/Features/CaravanTrade/Hooks/*.cs | The 4 postfixes (Patch59_CaravanTrade)` becomes wrong
  (a transpiler helper joins Hooks), and `CaravanDistanceHandoff.cs` has no row. Add both.

- **N14 (line 9): the drift check omits files whose behaviour the plan relies on.** `RefugeService.cs`
  (the early return Stage B3 rests on), `CultureItemPoolService.cs` and `CultureMarketplaceConfigProvider.cs`
  (Stage C's cache validity), `Main/Features/TaomSettings.cs` (the Step 4 oracles) and
  `CastleAiToggle.cs`. Add them so a change there is compared against the excerpts.

- **N15 (lines 52-53 vs 1992-1997, 2027-2030): "three new caravan hand-off lines".** Stage D adds four
  line kinds (bound INFO, not-bound WARNING in two variants, first-miss WARNING, periodic INFO). Say
  "up to three per session" or "four kinds".

- **N16 (lines 1152-1200, 1028-1150): two stages are thin under the simplicity criterion.** After
  Stage A, Patch42's two toggle reads are interface calls over a cached field; the reorder saves those
  per party-hour and makes the feature-off case run the whole filter. Stage B2's gate runs the owner
  gates and `GetKingdomSide` twice for every Free or Evil owner's roster to skip the snapshot for
  Neutral owners (four kingdoms per the 2026-07-04 caravan RCA, C4). Both are probably net wins, but
  `.claude/rules/simplicity-criterion.md` asks for the trade-off in the commit body; the Stage B body
  (1324-1334) states only the win.

- **N17 (lines 969-973): markdown backticks in an XML doc comment.** The replacement class comment
  quotes `` `RequireRestart = false` `` and `` `Lazy` ``; the file uses `<c>...</c>`. Say "use `<c>`
  for the two code spans".

- **N18 (lines 1249-1262): the speed-model tests are never seen RED.** They sit in the same build as
  the `NeedsMountedCount` service tests, which fail to compile, so the reflection test (2-argument
  `CountMountedAndTotal`) and the IL-order test never fail against the old code (rule 8). Create
  `TaomPartySpeedModelTests.cs` first, run its filter and quote its two failures, then add the
  service tests.

## Notes for the orchestrator

- The plan's claims about MCM v5 and Harmony internals (lines 93-104, 554-556) cite the local decompile
  dump; I did not open it. They match the `BattleBalanceSettingsProvider` precedent that already
  shipped and was reviewed. UNVERIFIED by me.
- The NaN oracle in Step 7 (`DesertCount` 1) holds on net472, where `(int)NaN` is `int.MinValue`, and
  would also hold on a saturating runtime (0, floored to 1).
