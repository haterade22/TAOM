# Adversarial review: TAOM Enlistment — six changes, none compiled by their authors

You are an independent adversarial reviewer on a Mount & Blade II: Bannerlord **v1.4.7** total-conversion mod (TAOM). Your job is to find real, demonstrable defects. Do not restate the design back to me, do not praise, and do not report style opinions. **A finding is only worth writing down if you can name the file, the line, the concrete failure, and the player-visible consequence.**

## Ground rules

- The installed game DLLs are authoritative for every TaleWorlds signature and semantic:
  `E:/Steam/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client/*.dll`
  A decompile cache is at `C:\Users\mikew\.taom-src\v1.4.7\*.cs`. `E:\Decompiled_Bannerlord\` may lag — do not trust it for signatures.
- The code **compiles** (main project 0 errors) and the **full suite is 6234 passing / 0 failing**. So do not hunt for missing types or typos — the compiler already did that. Hunt for the layer underneath: **code that compiles and passes tests but is wrong.**
- Architecture rules that make something a real defect here: services must not touch sealed TaleWorlds types directly (adapters only); entry points (CampaignBehaviorBase / MissionLogic / GameModel / Harmony patch) stay under 150 lines and delegate; constructor injection only, no service locator in services.

## Critical context: how this code was written

Six agents implemented six independent work items **in parallel, with builds forbidden** (concurrent `dotnet build` would corrupt `obj/`). Each wrote C# by inspection with no compiler feedback, and each was forbidden from touching localization files and the DI registration files. I compiled and tested afterwards.

That history predicts a specific defect profile, and I want you to weight your search accordingly:

1. **Semantically wrong but compiling API use** — an overload that silently discards data, a `virtual` the engine never actually calls, a property whose getter has different semantics than the call site assumes.
2. **Seams between the six items** — each author saw only their own slice. Two of them touched wage/reward code. Two touched the merit path. Two touched dialog behaviors.
3. **Duplicated logic that can drift** — an author who could not edit a neighbour's file may have copied its logic instead.

## The six changes

1. **Status board legibility.** `PromotionEvaluation` gained `MostBindingUnmetKey` + `MostBindingUnmetTarget`. `IPromotionService` gained `Peek()` (evaluate without mutating). `ServiceStatusService.Build()` now shows service XP, the single most-binding unmet promotion requirement, and today's wage. `ServiceStatusModel` gained five fields.
2. **Deferred-wage cap.** Config key `maxDeferredWages` (a GOLD cap, 60) renamed to `maxDeferredWageDays` (a DAY count); the cap is now computed inside `WagePolicy.ComputeDaily` as `days * dailyWage`. A forfeit log line was added.
3. **Dialog greying.** The reassignment line and the enlist offer now render shown-and-disabled with a reason instead of vanishing.
4. **`LeftTheField` merit.** `EnlistmentMeritMissionBehavior` overrides `OnMissionResultReady` to latch whether the battle reached a verdict; leaving early no longer banks the full survival weight.
5. **Equipment reclaim at discharge.** `DischargeConsequenceService` now calls `IPartyItemRosterAdapter.RemoveItem` per issued-ledger entry on honourable discharge.
6. **Riders.** `GateSpec.Weight` for weighted duty selection; new `enlist_lothlorien_*` / `enlist_battania_*` equipment roster rows.

## Files

```
Main/Features/Enlistment/Content/{PromotionEvaluator,IPromotionService,WagePolicy,ServiceRewardService,EnlistmentContentConfigProvider,BattleMeritScorer,DischargeConsequenceService,MeritGeometryAccumulator}.cs
Main/Features/Enlistment/Content/Domain/{DutyDefinitions,MeritConfig,ProgressionConfig}.cs
Main/Features/Enlistment/Domain/ServiceStatusModel.cs
Main/Features/Enlistment/ServiceStatusService.cs
Main/Features/Enlistment/Presentation/{ServiceStatusTextWriter,ServiceVocabulary}.cs
Main/Features/Enlistment/Duties/{DutySelector,IDutySelector}.cs
Main/Features/Enlistment/Hooks/{EnlistmentAssignmentDialogBehavior,EnlistmentDialogBehavior,EnlistmentMeritMissionBehavior,MeritGeometryScanner}.cs
Main/Adapters/IPartyItemRosterAdapter.cs
Main/_Module/ModuleData/enlistment/enlistment_config.json
Main/_Module/ModuleData/equipmentsets/taom_enlistment_equipment.xml
tools/generate_enlistment_rosters.py
TAOM.Tests/Features/Enlistment/**
```

Use `git diff` and `git status` to see exactly what changed; everything above is uncommitted.

## Specific hypotheses to attack

Each of these is a way this changeset could be broken. Confirm or refute each **from the code**, and say which.

1. **`OnMissionResultReady` may not do what item 4 assumes.** The whole feature hangs on: the engine calls this override, `Mission.MissionResult` is assigned in exactly one place, and a player-initiated retreat leaves it null. Verify all three against the installed DLLs. **If a retreat DOES produce a MissionResult, the feature is inverted** — a player who fights to the end would be penalised and a quitter rewarded. Also check the latch is reset per mission and cannot leak the previous battle's verdict.

2. **Wage display vs wage payment can disagree.** `ServiceStatusService` reportedly added a private `DailyWageFor(rank)` that "mirrors `ServiceRewardService.PayDailyWage`'s table read exactly." Read both. If the board can show a number the payment path would not pay — including at a rank beyond the table, or with a malformed config — that is a real defect. Parallel-method drift is a named recurring bug class in this repo.

3. **The wage-cap rename is a silent migration.** An existing user config still carrying `maxDeferredWages` will not match the new key. Does it warn, or silently take the new default? Separately: `DeferredWages` is **persisted in the save**. An existing save's arrears value is now measured against a different (larger, at every rank above the lowest) cap. Can that produce a nonsensical or exploitable state?

4. **The equipment reclaim may confiscate on the wrong discharge reasons, or double-count.** Enumerate every `DischargeReason` and check each is deliberately classified. `CommanderDead` and grace-expiry are honourable but not the player's fault — is taking their kit correct? Also: does the code trust `RemoveItem`'s **return value** (correct — it only drains the unmodified stack, so a player's modified variants survive) or the ledger (wrong — it would over-report)? And can the new code throw *before* the discharge pipeline restores party presence? There is an invariant that every discharge reason must restore presence; a throw that skips it strands the player's party hidden and inactive forever.

5. **Weighted duty selection can silently change existing behaviour.** An absent `weight` in existing data must yield exactly today's uniform behaviour. A `weight <= 0` in a cumulative-sum weighted pick silently shifts every subsequent probability rather than disabling one row. Check the default, the skip rule, the boundary arithmetic, and whether a non-finite or absurd weight is rejected at load.

6. **The new equipment roster ids may not match what the resolver builds.** The resolver constructs `enlist_{runtimeCultureId}_{rank}` at runtime from the **engine's culture StringId**. TAOM's single most recurring data bug is using a lore name where the engine wants the vanilla id (`vlandia` IS Rohan here). Confirm `lothlorien` and `battania` are the real runtime StringIds, and confirm every item id referenced by the new rows actually exists.

7. **The enlist-offer greying rests on an enum ordering claim.** The author asserts the verdict enum's ORDER means one `CanEnlistWith` call suffices to prove the earlier states passed. Verify that. If the order changes later this breaks silently — and check no verdict can now produce a line that is visible but has no explanation text.

8. **NaN / float→int.** Any engine- or config-sourced float feeding a decision must be gated as a positive requirement so NaN fails it, and any `(int)someFloat` must be finiteness-checked **at the cast** — `(int)float.NaN` is `int.MinValue`, and `int.MinValue - 1` wraps to `int.MaxValue`, defeating downstream guards. This exact class has shipped **five times** in this codebase. Check every float→decision path in every touched method, not only the added lines.

9. **Dead code being advertised.** For each of the five new `ServiceStatusModel` fields, is a non-default value actually produced in normal play, or is it structurally populated but always zero/null because its precondition is never met by the real caller? A field that is always empty but described to the player is a defect. This class has shipped here before.

## Output

Group by severity: **P1** (wrong behaviour a player will hit), **P2** (wrong under a reachable edge case), **P3** (latent / maintainability). For each:

```
[P?] <file>:<line> — <one-line defect>
Evidence: <what you read that proves it — quote the code or the decompiled signature>
Consequence: <what the player experiences>
Fix: <the minimal change>
```

End with a **VERDICT: SHIP / FIX FIRST**, and — separately — list anything in my nine hypotheses above that you checked and found to be **NOT** a problem, so I know what has been cleared rather than merely unexamined. If you disagree with a premise in this prompt, say so directly; I would rather be corrected than agreed with.
