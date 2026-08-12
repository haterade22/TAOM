# Adversarial review; TAOM enlistment field-test fixes

You are reviewing an uncommitted changeset on branch `feat/enlistment-field-fixes` in this repo
(`E:/repos/taom-enlist-fixes`, a worktree; base commit `e5ce5e76`). Target game: **Bannerlord
v1.4.8**, installed at
`E:/Steam/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client/`.

Your job is to find **real defects that will bite a player**, not style opinions. Be adversarial.
Assume the author was confident and therefore wrong somewhere.

## How to see the changeset

```
git status --porcelain
git diff
git ls-files --others --exclude-standard
```

Untracked files ARE part of the changeset, `git diff` will not show them. Read them directly.

## What the changeset does

Seven field reports from a live playtest of the Enlistment feature (the player serves as a soldier
in an AI lord's army). Six of the seven trace to one decision: `MobileParty.MainParty.Army` was kept
permanently null, which put the player on a different mission TEAM from the lord he served.

| # | Report | Fix |
|---|--------|-----|
| 1 | Can't enter towns / can't buy anything | Shore-leave pass (`TownLeavePolicy`, `EnlistmentRecord.OnTownLeave`) suspends the town/castle/village menu redirects while the column rests there |
| 2 | Not paid, or it doesn't show in the wallet | `WageReportPolicy` + messages; a display-only line in `TaomClanFinanceModel` |
| 3 | Not enough renown | `BattleRenownPolicy` via the `IServiceRewardService.Grant` chokepoint |
| 4 | Spawn far behind everyone | Transient battle-only army join (`IArmyMembershipAdapter`) |
| 5 | Clan declares war individually | Mirror the commander's wars at oath (`ServiceWarPolicy`, `ServiceDiplomacyService`), unwind only what the mirror created |
| 6 | Commands show wrong | One-instruction transpiler on `BehaviorComponent.OnBehaviorActivated` |
| 7 | Lord's army fought without me; jumped immediately after defeat | `CommanderBattleMatchPolicy` matches the army-leader party; `LeaveArmy()` above every gate in `OnCommanderBattleEnded` |

A second pass (2026-08-12) then fixed six review findings. **Scrutinise these hardest; they are the
newest and least-settled code:**

1. `ArmyMembershipAdapter.DisbandCreatedArmy`, now disbands UNCONDITIONALLY (it previously left the
   army standing when other lords had joined). Rationale: an army built by the bare `Army` ctor has
   `AiBehaviorObject == null` forever, and `Army.GetLongTermBehaviorTextForAILeadedParty`
   dereferences that with no null guard in 5 of 7 cases.
2. `EnlistmentReconciler` gained `IArmyMembershipAdapter` and calls `LeaveArmy()` in its
   stale-battle self-heal branch.
3. `IArmyMembershipAdapter.ResetSessionCaches()`, wired in `EnlistmentBehavior.OnGameLoaded`.
4. `MeritBand.Renown` populated (3/2/1/0) in defaults + shipped `enlistment_config.json`; added to
   `IsValidBandLadder`'s non-negative set.
5. `ServiceRewardService.GetDailyWage()` now also excludes `EnlistmentState.CommanderUnavailable`.
6. `TaomClanFinanceModel.CalculateClanIncome` override added, sharing `AddServiceWageLine` with
   `CalculateClanGoldChange`.

## Known suspects, attack these specifically

**A. The transient army merge (`Main/Adapters/ArmyMembershipAdapter.cs`).** This is the riskiest
code in the changeset. It constructs a real `Army` with the bare constructor, sets
`MainParty.Army`, calls `AddPartyToMergedParties`, and later detaches and disbands.
- Can the created army outlive the battle by ANY path? Enumerate every exit from
  `EnlistmentState.EnlistedBattle` and check each one reaches `LeaveArmy()`. Discharge? Captivity?
  Commander death? Feature toggled off mid-battle? Save/load mid-encounter? Player takes the
  "release" option?
- `_createdArmy` is in-memory only and `ResetSessionCaches()` nulls it on load. Is there a path
  where that leaves a real army orphaned with a null `AiBehaviorObject`?
- Is the ordering in `LeaveArmy` (AttachedTo, then Army, then disband) correct against the engine?
  Decompile `MobileParty.Army`'s setter, `Army.OnRemovePartyInternal`, and
  `Army.DisperseInternal`.
- `DisbandCreatedArmy` clears `_createdArmy` BEFORE calling the engine, and guards on
  `army.Kingdom == null || army.LeaderParty?.Army != army` to detect an already-dispersed army. Is
  that liveness test correct? Can it produce a false "already gone" and leak the army?
- Does disbanding an army a real lord had joined cause any harm the author dismissed too quickly?
  Read `Army.DisperseInternal` and `DisbandArmyAction.ApplyInternal`.

**B. The reconciler's new `LeaveArmy()` call (`Main/Features/Enlistment/EnlistmentReconciler.cs`).**
It sits inside the stale-battle branch. Can that branch fire while a battle is genuinely live,
detaching the player mid-fight? Trace `presence.IsInMapEvent`, `snapshot.PartyIsInMapEvent` and
`_encounter.HasCurrent` and find any window where all three read false during a real battle.

**C. The transpiler (`Main/Features/Enlistment/Hooks/BehaviorComponent_OnBehaviorActivated_Transpiler.cs`).**
Verify against the INSTALLED `TaleWorlds.MountAndBlade.dll`: is there exactly one matching
`ToString()` call? Is the stack balanced? What happens if the matcher finds zero matches, does it
fail loud or silently no-op? Check that the patch category is registered in `Main/SubModule.cs`.

**D. The war mirror (`ServiceDiplomacyService` / `ServiceWarPolicy`).** The catastrophic failure
mode is peacing the player out of wars they started BEFORE enlisting. Verify `EnemiesAtOath` is
snapshotted before any declaration, that unwinding only touches `MirroredWars`, and that both lists
survive a save/load round-trip. What happens if the player enlists twice, or is discharged while
`CommanderUnavailable`?

**E. `EnlistmentRecord` persistence.** Three fields were added (`OnTownLeave`, `MirroredWars`,
`EnemiesAtOath`). Does a save written BEFORE this change deserialize to empty lists rather than
null? Is any consumer enumerating them unguarded?

**F. The wage chain.** `PayDailyWage` computes arrears with a day-denominated cap. Look for
off-by-one, double-pay, or silent confiscation. Cross-check `GetDailyWage` (projection) against
`EnlistmentDailyService.RunDailyTick` (payment), the author just fixed one gate mismatch there;
are there others?

**G. Config.** `Main/_Module/ModuleData/enlistment/enlistment_config.json` gained `renown`,
`battleWinRenown`, `battleLossRenown`. Are any OTHER fields in `EnlistmentContentConfig` /
`ProgressionTables` / `MeritScoringConfig` absent from the shipped JSON, so a tuner cannot reach
them? Are any parsed-but-never-consumed?

## Project rules you must apply

- **ADR-007 Adapter pattern:** services never reference sealed TaleWorlds types (`Hero`, `Clan`,
  `MobileParty`, `Settlement`); they take `IXxxAdapter`. Adapters may.
- **ADR-002 Thin entry points:** `CampaignBehaviorBase` / `GameModel` / Harmony patch classes stay
  under 150 lines and delegate to services.
- **NaN gates:** every decision gate on a float must be written as a POSITIVE requirement
  (`if (!(x > 0f)) return;`), because every NaN comparison is false. This bug class has shipped five
  times in this repo. Check every float comparison and every `(int)<float>` cast in the changeset.
- **Computed TaleWorlds getters throw before your null check**, `if (party.Culture != null)` NREs
  inside the getter when `MapFaction` is null. Use `party.MapFaction?.Culture`.
- **Harmony patches** need BOTH `[HarmonyPatchCategory("X")]` and a matching
  `_harmony.PatchCategory("X")` in `Main/SubModule.cs`, applied at a lifecycle stage that precedes
  the first render of the patched target.

## Verification requirement (non-negotiable)

For every claim about a TaleWorlds API, decompile the INSTALLED DLL, do not rely on the
`E:/Decompiled_Bannerlord/` dump and do not rely on memory:

```
ilspycmd "E:/Steam/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client/TaleWorlds.CampaignSystem.dll" -t "TaleWorlds.CampaignSystem.Army"
```

State the evidence you read for each engine claim. A finding you could not verify must be labelled
UNVERIFIED, not asserted.

## Output format

For each finding:

```
### [P1|P2|P3] <one-line title>
**File:** path:line
**What breaks:** the concrete player-visible failure, with the inputs/state that trigger it
**Evidence:** the code you read (TAOM source and, for engine claims, the decompiled member)
**Fix:** the minimal change
```

P1 = crash, save corruption, or a player losing progress/money. P2 = wrong behaviour a player will
notice. P3 = latent risk or maintainability.

End with:

```
## Verdict
<SHIP | FIX FIRST> — N P1, N P2, N P3
```

If you find nothing in a section, say so explicitly rather than padding. A short honest review beats
a long speculative one. Do not report style preferences, naming opinions, or "consider adding a
comment" as findings.
