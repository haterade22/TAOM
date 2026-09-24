# Verify batch A-02: adversarial check of ARCH-01, ARCH-06, ARCH-05 (lane 2)

Checker: fresh adversarial pass, read-only. Every read is against `b2e387db` via `git show` or
`git grep <pattern> b2e387db -- <path>` (HEAD has moved to `4b5662b2`; the working tree is another
session's). No build, no test.

## ARCH-01: interface-per-service rule contradiction

**Outcome: CONFIRMED, with corrected evidence and a by-design source the finding missed. Impact today: LOW.**

- **Re-read and holds**: `.claude/skills/deep-review/lenses/1-standards.md:13` is item 6, "Every service
  has an interface. Every adapter has an interface." `.claude/rules/think-before-coding.md:33` says "no
  single-implementation interface". `.claude/agents/feature-builder.md:40` (`I{Name}Service.cs`) and
  `:55` (`Register<I{Name}Service, {Name}Service>`) scaffold the interface. All at `b2e387db`.
- **Counts reproduce**: a comment-stripped scan of `git archive b2e387db Main TAOM.Tests`
  (scratchpad `count_impls.py`) gives 495 interfaces declared in `Main/`, 477 with one implementation,
  3 with none, 15 with two or more, exactly the lane's numbers; 263 interfaces and 1,063 `.cs` files at
  `141b749`, 2,166 `.cs` files now. Of the 477, 284 are `Substitute.For`-ed in tests, consistent with
  the lane's 100 (a) plus 185 (b) (the lane also counts hand fakes). Spot checks: `IChargeKnockdownService`,
  `ICrushThroughService`, `IShieldPenetrationService`, `ICreatureCombatService` are referenced only by
  their class, `CombatMechanicsIoC.cs`, `TaomCombatMechanicsModel.cs` and `SubModule.cs`, never by
  `TAOM.Tests`; `IOnRecruitmentResourceGate.cs:10` has the one patch caller; `IAlignmentDesertionConfigProvider`
  is read only at `AlignmentDesertionSettingsProvider.cs:14`.
- **What the finding missed (by-design source)**: the lens line is not the origin of the rule.
  `docs/adrs/002-thin-entry-points.md` is "Accepted (Mandatory)" (`:3`) and says at `:247` "**Interface-Based**:
  Always define `IServiceName` interface", repeats it in the merge checklist at `:291` ("Service has
  corresponding interface"), and accepts the cost at `:320` ("More files per feature (interface +
  implementation + entry point)"). The lens mechanizes ADR-002; `think-before-coding.md:33` (added
  `b6d84576`, 2026-05-11) is the newer line that contradicts an accepted ADR. So the 154 (c) interfaces are
  ADR-002-compliant by decision, not debt, until that ADR is superseded; and the causal claim that the
  lens "wins in practice" and drove the growth is unproven (interfaces and `.cs` files both roughly
  doubled, which is what ADR-002 compliance predicts anyway).
- **Corrected fix direction**: amending only lens line 13 and `feature-builder.md` would make the lens
  contradict ADR-002. Settling it needs Mike's decision recorded as an ADR change (supersede ADR-002
  guideline 2 and checklist `:291` with the "adapter, test fake or second implementation" condition), or
  the opposite: narrow `think-before-coding.md:33` to non-service code. The contradiction itself is real
  and worth one decision; the code half stays a guideline, as the lane says.

## ARCH-06: retire the static `ReflectionHelper` facade; gate its engine members

**Outcome: CONFIRMED, with two corrections (3 of the 14 members are already gated; the "throw rather
than degrade" impact is wrong for the HeroRace sites). Impact today: LOW.**

- **Re-read and holds**: `Main/Core/Infrastructure/Reflection/ReflectionHelper.cs` is 47 lines, a
  static class whose static constructor (`:7-10`) calls `IoC.Resolve<IReflectionService>()` and whose
  six methods forward one-for-one (`:12-46`). `IReflectionService.cs` 16 lines, `ReflectionService.cs`
  134 lines, registered at `Main/IoC.cs:221` (`git show b2e387db`). The harm is recorded in code at
  `Main/Features/HeroRace/EyeHeightAdjustmentHook.cs:141-144`. `ReflectionService` throws
  `InvalidOperationException` on a missing field (`:31-35`, `:47-51`), property (`:69-73`, `:85-89`) or
  method (`:108-112`). 74 `Main/` files use `AccessTools.Field/Property/Method` (`git grep -l`, reproduces).
- **Call sites**: `git grep -n "ReflectionHelper\." b2e387db -- Main TAOM.Tests` gives 27, not the
  heading's 26: `CharacterSpawnerService.cs` 21, `CharacterTableau_SetRace_Patch.cs:18,20,22` 3,
  `RacePositionTuningCheats.cs:192` 1, `BannerHeroAdapter.cs:34-35` 2 (the body's per-file numbers are
  right; only the heading total is off by one). 14 distinct engine members, all present in the v1.5.3
  decompile cache (`~/.taom-src/v1.5.3/...CharacterSpawner.cs:37-51,513`, `...CharacterTableau.cs:31,39,131,1002`,
  `TaleWorlds.CampaignSystem.Kingdom.cs:133,136` auto-properties), so no live defect.
- **Correction 1 (gate gap is 11, not 14)**: none of the 14 is in `ReflectionSiteBindingTests.cs` or
  `reflection-sites.md` (grep counts 0 each, reproduces), but three are gated by feature binding tests
  in the same `BindingVerification` category: `CharacterTableau._agentVisuals`
  (`TAOM.Tests/Features/HeroRace/Patch67TableauResidencyBindingTests.cs:103`,
  `Patch72TableauRacePositionBindingTests.cs:66`), `_oldAgentVisuals` (`Patch67...:104`) and
  `_isVisualsDirty` (`Patch72...:110`, whose comment at `:105-106` names exactly the tuner's redraw write).
  Truly ungated: the 8 `CharacterSpawner` members (`_agentEntity`, `_horseEntity`, `_agentVisuals`,
  `_spawnFrame`, `CreateFaceImmediately`, `ClothColor1`, `ClothColor2`, `WieldWeapon`),
  `CharacterTableau.InitializeAgentVisuals`, and the two `Kingdom` backing fields.
- **Correction 2 (failure mode)**: the finding says a rename "would throw inside character-creation
  and tableau code rather than degrade". All three HeroRace sites are inside catches:
  `CharacterSpawner_InitWithCharacter_Patch.cs:15-29` catches everything and returns `true` (vanilla
  runs); `CharacterTableau_SetRace_Patch.cs:30-35` logs; `RacePositionTuningCheats.cs:194-198` swallows.
  A throw after `CharacterSpawnerService.cs:52-55` (entity mutation) would hand vanilla a partly edited
  spawner, which is UNVERIFIED engine behaviour. The one uncaught path is the banner one:
  `Main/Features/BannerColorPersistence/Hooks/Clan_UpdateBannerColor_Patch.cs:21-27` is a postfix with no
  try/catch calling `BannerHeroAdapter.SyncKingdomColors`, so a `Kingdom` backing-field rename would
  throw out of `Clan.UpdateBannerColor` whenever the drift guard is enabled. That is the row to gate first.
- **Fix-sketch caveats**: ADR-002 guideline 6 (`docs/adrs/002-thin-entry-points.md:251`, "No Reflection
  in Services") means injecting `IReflectionService` into `CharacterSpawnerService` still leaves
  reflection in a service; and deleting `IReflectionService` runs into ADR-002 guideline 2 (see ARCH-01).
  Adding the 11 rows is the uncontested half.

## ARCH-05: record the protected-virtual seam as an ADR-007 exception; seam the raw-read services

**Outcome: CONFIRMED for the "record" half, with corrections; the "seam SupplySource and SupplyCaravan
first" half is mis-targeted. Impact today: LOW.**

- **Re-read and holds (the pattern)**: `virtual` member counts at `b2e387db` reproduce exactly:
  `RefugeService.cs` 39 (first `:789`, last `:1214`, file 1,226 lines), `CampService.cs` 32 (from `:661`),
  `SupplyOrderService.cs` 13 (from `:457`), `WardenService.cs` 10, `RuntimeCacheRebuildService.cs` 3; 97
  total. Test `override` counts: `RefugeServiceTests.cs` 42 (1,796 lines), `CampServiceTests.cs` 32
  (1,256), `SupplyOrderServiceTests.cs` 13 (821), `WardenServiceTests.cs` 10 (314). Seam signatures at
  `RefugeService.cs:789,818,860` are as quoted; the pattern note is at `:48-50`. No engine-static read
  precedes the first seam in the four campaign services (grep, line numbers below the first `virtual`:
  none). `CampService.cs:763-766` is byte-identical to `RefugeService.cs:791-794` (`diff`), and all nine
  named seams exist in both files. Refuge, Camp, Warden and SupplyOrder were added 2026-08-22
  (`03bcd465`, `24cce287`, `2244fed5`).
- **Re-read and holds (not recorded)**: `docs/adrs/007-adapter-pattern.md:552-580` "Exceptions" lists
  only value types and entry points; `.ai/review-reference.md:21` grades "ADR-007 (sealed type in
  service)" CRITICAL; the Standards lens item 1 (`1-standards.md:8`) says "Flag ANY direct TaleWorlds
  type usage in service classes". `git grep` for protected virtual, virtual seam, test subclass, seam
  over `docs/adrs`, `.ai`, `.claude/rules`, `.claude/skills/deep-review`, `.claude/agents`, `AGENTS.md`,
  `CLAUDE.md`, `docs/ai-includes` finds no record. The only acceptance is non-policy:
  `docs/reviews/REVIEW-LOG.md:887` and `docs/reviews/agents-md-review-lessons-archive.md:104` call the
  `SpawnBuild` virtual seam "correct" for `RuntimeCacheRebuildService` (a threading seam, not an engine one).
- **Correction 1 (four services, not five)**: `RuntimeCacheRebuildService.cs:84,302,360` are
  `SpawnBuild`, `WriteOutputAtomically(INavigationCacheAdapter, ...)` and `VerifyOutputRoundTrip`, a
  `Task.Run` and file-IO seam, not campaign statics; its test overrides one member
  (`RuntimeCacheRebuildServiceTests.cs:373`), not three. The engine-static seam pattern is four services,
  94 members, all from 2026-08-22. The Delta line's "all five seamed services ... date from 2026-08-22"
  is wrong for this one (`646484b4`, 2026-05-12).
- **Correction 2 (the contrast is mis-targeted)**: my own comment-stripped scan (scratchpad
  `seams_check.py`, a similar static list) reproduces the raw-read ranking: `SupplySourceService.cs` 25,
  `SupplyCaravanService.cs` 17, `SiegeDefenseService.cs` 13, then 11, 8, 5, 4 (16 files, 103 reads with my
  list against the lane's 15 and 115; heuristic). But the two lead targets are self-declared engine
  boundaries, not decision services: `SupplySourceService.cs:15` "Engine-boundary implementation of
  `ISupplySourceService`", `SupplyCaravanService.cs:20` and `ISupplyCaravanService.cs:6` "Engine boundary".
  Both are faked through their interfaces by the tests of the logic that consumes them
  (`SupplyOrderServiceTests.cs:109-110`, `SupplyOrderScreenVMTests.cs:60`), `SupplyCaravanService`'s pure
  math is already extracted and tested (`SupplyCaravanCargoMathTests.cs:31`, `SubtractNonCargo`), and its
  reflection has a binding test (`SupplyCaravanBearingBindingTests.cs:46,63,92`). They play the ADR-007
  adapter role under a Service name, as do `SupplyRouteVisualService`, `CampVisualService` and
  `RefugeVisualService` (each headed "Engine boundary"). So "15 services with no seam are the actual
  untestable debt" overstates it; the residual debt there is decision logic living inside a boundary
  class (for example the eligibility filter in `SupplySourceService.cs:41-60`), which the house would
  extract, not seam. `SiegeDefenseService`, `CareerMenuService` and `CultureSettingService` are already on
  the books (triage-B, triage-A), as the lane says.
- **By-design tension**: BRIEF says ADR-007 adapters are decided. An exception entry does not remove
  adapters, but it sanctions engine code inside services, which also contradicts ADR-002 guidelines 4
  and 5 (`docs/adrs/002-thin-entry-points.md:249-250`); a record needs both ADRs amended, by Mike.

## What I did not cover

- No `dotnet build` or `dotnet test` (brief). Binding-test coverage claims are from reading the test
  sources, not from running them.
- ARCH-01: I reproduced the interface totals and single-implementation count, not the lane's (a)/(b)/(c)
  split of the 477; the 154 (c) rows were spot-checked (six), not re-derived.
- ARCH-06: what vanilla `CharacterSpawner.InitWithCharacter` does with a spawner TAOM partly mutated
  before a mid-method throw is engine behaviour I did not trace (UNVERIFIED). Whether the banner drift
  guard is on by default was not checked.
- ARCH-05: my engine-static list differs from the lane's `seams.py` list, so per-file counts below the
  top three differ by one or two; I did not audit the eight small raw-read services individually, and I
  did not price the adapter-route cost comparison.
