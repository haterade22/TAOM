# Enlistment remediation — sequenced implementation plan (2026-08-07)

Produced by workflow `wf_3d93e0c1-9e3`: six design clusters -> 44 specs -> six adversarial reviews
(**44 kept, 0 dropped**) -> this sequencing pass. Inputs: `donor-diff-enlistment-2026-08-07.md`
(33 confirmed gaps) and the live play-test logs of 2026-08-07.

---

[Certain on the two engine facts I re-verified this turn; [Likely] on claims relayed from the surviving specs, marked where load-bearing.]

# TAOM Enlistment — Ordered Implementation Plan

**Scope:** 44 surviving specs across 6 clusters → 13 sequential batches for one implementer. Read this top-to-bottom; §0 and §1 change what several specs say, so do not start coding from a spec without reading them.

---

## §0. Reconciliation decisions — the specs collide in seven places

These were written by six independent agents. Seven pairs contradict or duplicate. Resolve them **before** batching, or you will implement the same thing twice and delete one of them later.

### R1 — `CommanderTickSnapshot` (#1) and `CommanderFitness` (#36) are the same type. Ship ONE.

Spec #1 mints a `readonly struct CommanderTickSnapshot {Exists, IsAlive, IsPrisoner, PartyId, PartyIsActive, PartyIsInMapEvent, PartySettlementId, MapEventToken}`. Spec #36 mints `CommanderFitness {Exists, IsAlive, IsPrisoner, PartyId, PartyIsActive, PartyIsInMapEvent, PartyIsInSettlement, SettlementId}`. Identical purpose, identical justification (drop `Name.ToString()` / `Culture` / `Clan.MapFaction` off the hot path), 90% identical fields.

**Decision:** one `readonly struct CommanderTickSnapshot` in `Main/Adapters/CommanderTickSnapshot.cs` carrying the **union**:
```
bool Exists, IsAlive, IsPrisoner;
string PartyId; bool PartyIsActive, PartyIsInMapEvent;
bool PartyIsInSettlement; string PartySettlementId;
int MapEventToken;
bool HasParty => !string.IsNullOrEmpty(PartyId);
static CommanderTickSnapshot Missing => default;
```
One adapter member `ICommanderLordAdapter.GetTickSnapshot(string heroId)`. `PartyIsInSettlement` + `PartySettlementId` are required because R2 makes `Assess` consume this type. `MapEventToken` is #1's privately-minted identity (`MapEvent` is unregistered with `MBObjectManager`, so it has no engine `StringId`).

### R2 — The pump (#2) must route placement through `Assess`, not re-derive it. This kills #24.

Three specs define a frame-cadence loop: #2 (`IServiceMaintenanceService.Pump`), #24 (`IEnlistmentReconciler.ReconcilePlacement`), #37 (`IEnlistmentReconciler.ReconcileNow`). #2 and #24 both claim the wait-menu tick and both define a verdict→action mapping. #24's objection to #2 is correct in substance: *"it must NOT re-implement Assess"* — two mappings drift the moment #21 adds settlement verdicts.

**Decision:** adopt **#2's** `ServiceMaintenanceService` (it is the far more complete spec — shared budget, NaN gate, menu backoff, latch break, join dedupe, edge handler, config clamps, 40 named tests) but replace its hand-rolled branch chain in the expensive tier with a switch over `IServiceAttachmentService.Assess`:

```
if (TryBreakBattleLatch(r, c, p)) return;          // NOT an Assess verdict — pump-only
switch (_attachment.Assess(r.State, c, p).Status)
{
    case BattleJoinRequired:        TryRequestBattleJoin(r, c, p, nowHours); break;
    case SettlementFollowRequired:  _attachment.FollowCommanderIntoSettlement(r.CommanderHeroId, c.PartySettlementId); break;
    case SettlementExitRequired:    _attachment.LeaveSettlementAndPark(r.CommanderHeroId); break;
    case AttachRequired:            EnsureParkedIfDrifted(r, p, nowHours); break;
    case Attached:                  break;
    default:                        break;          // Blocked* — hourly owns terminal decisions
}
EnsureServiceMenu(r);
```
Consequences: **#24 is DROPPED entirely** (subsumed), the "SEAM" #2 left for settlement following fills itself when #21 lands (Batch 7 needs zero pump edits), and `Assess` must take `CommanderTickSnapshot` + `PlayerPresenceSnapshot`. **#37 survives** — `ReconcileNow(nowDays, trigger)` is a *full* reconcile for the edge subscriptions in #38, distinct from the pump. Keep #24's one genuinely load-bearing fix: `OnWaitMenuInit`'s unconditional `EnsureParked` must not fire when the player is legitimately inside the commander's settlement — that moves to Batch 7.

### R3 — The pump's cheap-tier position sync must fire on `CampaignTick` ONLY, not on the wait-menu source.

Spec #35 says the measured ~1.8-unit lag is a tick-**phase** bug, not a throttle bug, and #2/#4 do not account for it. **I verified this myself this turn** against `C:\Users\mikew\.taom-src\v1.4.7\TaleWorlds.CampaignSystem.GameState.MapState.cs`:

```
144  protected override void OnTick(float dt) {
156      else if (AtMenu)
158          OnMenuModeTick(dt);      // ← the wait-menu OnTick fires HERE
160      OnMapModeTick(dt);
...
173  private void OnMapModeTick(float dt) {
186      Campaign.Current.RealTick(dt);   // ← party positions integrated HERE
189      Campaign.Current.Tick();          // ← CampaignEvents.TickEvent fires HERE
```
The wait-menu tick runs **before** position integration; `TickEvent` runs **after**. Copying the commander's position from the menu tick is permanently one frame of travel stale, regardless of throttle.

**Amendment to #2:** in `Pump`, guard the cheap tier with `if (source == MaintenancePumpSource.CampaignTick)`. #4 still rewires the wait-menu tick to the pump (it drives the expensive tier, the menu re-assert and the latch break, which are all phase-insensitive), but it stops syncing position. **#35 is otherwise absorbed**; keep only its `DriftWarningThreshold` recalibration (15f → 2f) and put that in Batch 3 alongside the change, not before it.

### R4 — `#9`'s leader carve-out must be the only implementation. `#34`'s inline `main.Army = null` is a bug.

#34 hardens `ParkNear`/`RestorePresence` with two blanket lines including `if (main.Army != null) main.Army = null;`. #9 proves (relayed, spec cites installed 1.4.7 `Army.cs:867-870`) that `Army.OnRemovePartyInternal` runs `if (LeaderParty == mobileParty && !_armyIsDispersing) DisbandArmyAction.ApplyByLeaderPartyRemoved(this)` — so #34's blanket line **disbands an army the player leads**. #9's carve-out (clear `Army` only when the main party is a *follower*; always clear `AttachedTo`) is correct.

**Decision:** implement `IMobilePartyAttachmentAdapter.ClearArmyAttachment()` per #9 exactly once. #34's hardening becomes *"`ParkNear` and `RestorePresence` each call `ClearArmyAttachment()`"* — no inline duplication. Both land in Batch 2.

### R5 — `#33` and `#39` directly contradict each other on `EncountersBlocked`. Take #33 + #39's bug fix.

#33: fold the settlement clause into `EncountersBlocked`. #39: **refuse** to, because the discharge alarm will cry wolf once settlement placement becomes the normal case. #39's objection is real but #33 already answers it — #33 splits the `DischargeService` alarm into a WARNING (settlement-only) and the existing ERROR (everything else).

**Decision:** implement **#33** (settlement clause + WARNING/ERROR split) and take **#39's** genuinely separate half: `DistanceToCommander` has never been populated, so every `[EnlistDiag]` line in every log the user has sent prints `distToCommander=?` — the one number that decides whether S4 is a sync failure or a perception issue. Drop #39's refusal. Both in Batch 5.

### R6 — `#14` and `#42` are the same service written twice. Take #14's shape, #42's activity reads.

#14 = `IServiceStatusService` + `ServiceStatusModel` in `Features/Enlistment/`. #42 = `Content/IServiceStatusService` + `ServiceStatusLine`. Same feature.

**Decision:** adopt **#14** — it alone carries the root cause (`MBTextManager.SetTextVariable` writes a *global* variable that never lands in the menu `TextObject`'s `Attributes`, so `GameMenuVM.IsMenuTextChanged()` can structurally never observe it; the text is frozen for the whole term and raising the refresh cadence changes nothing — the fix is `args.MenuContext.GameMenu.GetText().SetTextVariable(...)`). Take **#42's richer `GetActivity` implementation** (it adds `BesiegerCamp`, `ShortTermBehavior == RaidSettlement`, `TargetSettlement` — #14's is thinner). Drop #42's duplicate service/model/enum. Batch 9.

### R7 — `#40(b)` is redundant with `#28`. `#40` reduces to a deletion.

#28's `GetOwnership` already computes `EncounteredPartyIsCommanderRelated` via a private verbatim port of the donor's `IsCommanderRelatedParty`. #40 itself says *"If cluster 5 has already specified an equivalent, KEEP THEIRS."* **Decision:** #40 = delete the dead `IEncounterAdapter.EncounteredPartyId` (zero consumers, compiler-proven). Folded into Batch 4.

**Also note:** spec line numbers have drifted from today's edits — `DischargeService.cs` is **110** lines, not the 94 spec #7 assumes. Re-Read every file before editing it; do not trust a spec's line citation as a cursor.

---

## §1. The `AttachedTo` question — answered directly

**VERDICT: do not adopt real party attachment. It supersedes nothing on this plan, and it is not a latent better architecture we are declining for cost reasons — it is a guaranteed campaign CTD in its naive form and a different feature in its correct form.**

Spec #34 makes three findings. I did **not** independently re-decompile these this turn (I verified `MapState.OnTick` and `CampaignObjectManager` only) — flagging that honestly, because the third one is the decisive one and it is worth one `taom-src` check before you commit to the answer:

1. **Naive adoption is a hard CTD.** `DefaultEncounterGameMenuModel.GetGenericStateMenu()` reads `if (mainParty.AttachedTo != null) { if (mainParty.Army.LeaderParty != mainParty …` — an **unguarded** `Army` dereference behind an `AttachedTo` null check. `Campaign.Tick()` calls `GetGenericStateMenu()` every frame the player is on the open map with no menu. `AttachedTo != null` with `Army == null` NREs immediately and unavoidably.
2. **Non-naive adoption means actually joining the commander's army** — `Army.AddPartyInternal` is the only engine writer of `AttachedTo`. That drags in kingdom membership, faction/AI/cohesion semantics, the army UI, and undoes `LordConversationsJoinArmyConditionPatch`, which TAOM added deliberately.
3. **Even then, keeping the party parked poisons everyone else's battles.** `PartyBase.MapEventSide`'s setter cascades to `AttachedParties`; `MapEvent.CanPartyJoinBattle` requires `IsActive` for **every** party on **both** sides. One inactive attached party makes reinforcement joins fail for every AI lord on either side of the commander's fight — a global campaign regression.

**What it would have bought, and where each win is already landing:** live settlement following → Batch 7 delivers it explicitly via `EnterSettlementAction`/`LeaveSettlementAction`. Instant battle participation → Batch 3 + Batch 4 deliver it via the pump + encounter path. Zero position drift → **attachment does not deliver this at all**; leader-relative movement runs through `variables.IsAttachedArmyMember`, which requires `Army != null && AttachedTo != null`. Drift is fixed by R3's five-line tick-phase move.

**Cost if you overrode this anyway:** a new `EnlistedInArmy` state + transition-table rows, army join/leave lifecycle, kingdom-membership handling, removal of the join-army suppression, un-parking (which re-opens every menu-redirect and encounter question this plan closes), plus a fix for finding (3) that does not exist. That is a feature rewrite with its own save-compat story, not a fix for S1–S5.

**Action taken instead:** the *defensive* half of #34 ships in Batch 2 — `ParkNear`/`RestorePresence` clear `Army`/`AttachedTo` via #9's leader-safe `ClearArmyAttachment()`, and a reflection invariant test `MobilePartyAttachmentAdapter_NeverAssignsAttachedTo` pins the decision so a future session cannot re-derive it. Record the three findings in `docs/features/enlistment.md`.

---

## §2. File-ownership map

**Single-owner files (orchestrator edits, you recommend):** `Main/SubModule.cs` — needed by **Batch 3 only** (one `AddBehavior` line for `EnlistmentMaintenanceBehavior`, immediately after the `EnlistmentBattleBehavior` line). `Main/IoC.cs` — **not needed by any batch**; every registration in this plan lands in the feature-owned `Main/Features/Enlistment/EnlistmentIoC.cs`.

**Convergence files (touched by ≥2 batches). Owner = first batch to touch it; later batches must re-Read before editing.**

| File | Owner | Later revisits (in order) |
|---|---|---|
| `Main/Adapters/MobilePartyAttachmentAdapter.cs` | **B0** (`Settlement.Find`) | B2 (cache, flags, ClearArmyAttachment, hardening), B3 (drift threshold), B5 (`GetPresence` distance) |
| `Main/Adapters/ICommanderLordAdapter.cs` + `CommanderLordAdapter.cs` | **B2** (`GetTickSnapshot`) | B5 (`SettlementKind`), B8 (`IsInMapEvent`), B9 (`GetActivity`, mesh) |
| `Main/Adapters/IEncounterAdapter.cs` + `EncounterAdapter.cs` | **B4** (`GetOwnership`, `Finish` default, delete `EncounteredPartyId`) | B10 (`IsConversationActive`) |
| `Main/Features/Enlistment/IServiceAttachmentService.cs` + `ServiceAttachmentService.cs` | **B2** (facade pass-throughs, `Assess` param swap) | B7 (settlement verdicts + follow/exit executors), B5 (`GetPresence` param) |
| `Main/Features/Enlistment/EnlistmentReconciler.cs` | **B3** (shared retry budget) | B4 (ParkedSweep verdict), B7 (settlement verdict routing), B10 (`ReconcileNow` + sweeper hoist + log detector) |
| `Main/Features/Enlistment/ServiceBattleService.cs` | **B3** (pending flag) | B4 (stale-encounter step, rollback verdict) |
| `Main/Features/Enlistment/DischargeService.cs` | **B3** (reset + cache invalidate) | B4 (Discharge intent), **B5 (full pipeline rewrite — the big one)** |
| `Main/Features/Enlistment/EnlistmentMenuService.cs` (+ interface) | **B3** (`IsRedirectable`) | B8 (battle-menu exemption + presence gate) |
| `Main/Features/Enlistment/IEnlistmentConfigProvider.cs` | **B1** (contract fields) | B3 (pump fields), B7 (RedirectMenuIds), B12 (JSON loading) |
| **`Main/Features/Enlistment/Hooks/EnlistmentMenuBehavior.cs`** ⚠️ | **B3** (tick → pump) | B5 (drop `ExitToLast`), B6 (leave → presenter), B7 (init fix), B8 (talk + ask-duty options), B9 (status refresh), B11 (train option) — **7 batches; it is at 134/150 lines today, so B6's and B8's line-shedding moves are mandatory, not optional** |
| `Main/Features/Enlistment/Hooks/EnlistmentWaitMenuPresenter.cs` | **B6** (`RequestRelease`) | B9 (live status + mesh + settlement text) |
| `Main/Features/Enlistment/EnlistmentIoC.cs` | **B3** | B8, B9, B11 |
| `Main/Adapters/PlayerPresenceSnapshot.cs` | **B5** (R5 merge) | — |
| `Main/Adapters/CommanderSnapshot.cs` | **B5** (`SettlementKind`) | B9 (`SettlementName`) |

**Fully disjoint batches (no shared file with any other):** B0 (after its one revisit is noted), B1, B12. Everything else shares at least `EnlistmentMenuBehavior.cs` or `EnlistmentIoC.cs` — which is fine for a sequential implementer but means **you cannot reorder batches 3–11 freely**.

---

## §3. The batches

Sizes are rough LOC of *net new + changed* production code, excluding tests.

---

### **Batch 0 — `Settlement.Find` prerequisite** · ~30 lines · ships alone, immediate player value

Spec #20. **I verified the core claim this turn:** `CampaignObjectManager._objects` registers exactly `MobileParty`, `Hero`(dead), `Hero`(alive), `Clan`, `Kingdom` (lines 300–304), the private `CampaignObjects` enum has no `Settlement` member (188–194), and `Find<T>` only descends when `typeof(T) == campaignObjectType.ObjectClass` (683+). **`Find<Settlement>(anyId)` returns null unconditionally.**

**Files:** `Main/Adapters/MobilePartyAttachmentAdapter.cs` (`MoveIntoSettlement` body), `Main/Adapters/DutyWorldAdapter.cs` (`FindSettlement`), new `TAOM.Tests/Features/Enlistment/SettlementLookupBindingTests.cs`.

**Changes:** swap both to `Settlement.Find(id)` (→ `MBObjectManager.Instance.GetObject<Settlement>`), which `LordSpawnGuardAdapter.cs:72` already uses correctly. Rewrite `MoveIntoSettlement` to the leave-then-enter shape with a **verified** return (`main.CurrentSettlement == settlement`), guarding `LeaveSettlementAction.ApplyForParty` behind `CurrentSettlement != null` (it NREs on null). Do **not** touch the other 14 `Find<T>` sites — Hero/MobileParty/Clan/Kingdom all resolve.

**Tests:** `AdapterSources_ContainNo_CampaignObjectManagerFindSettlement` — source scan over `Main/Adapters/**` and `Main/Features/**` asserting the literal `Find<Settlement>` is absent. Adapters aren't unit-testable; this scan *is* the gate.

**Proof line:** the `[EnlistDiag] SpawnLooterParty: settlement='…' or looters clan unresolved` error **stops appearing**, and a hunt/recon duty actually spawns its target. Those duties have failed 100% of the time.

**Protects:** nothing at risk. New exposure: `MoveIntoSettlement` now fires `CampaignEvents.OnSettlementLeft` for the main party where it previously never ran at all — grep `OnSettlementLeftEvent` across `Main/` first (the diff established Enlistment subscribes to none).

---

### **Batch 1 — Kill the silent desertion (S5)** · ~90 lines · fully disjoint, ships alone

Spec #10 only. **Deliberately excludes #11's inquiry UX** so the wage-confiscation bug is fixed in the player's hands one batch from now instead of six.

**Files:** `IEnlistmentDialogGateService.cs`, `EnlistmentDialogGateService.cs`, new `Domain/LeaveRequestResult.cs`, `Domain/DischargeReason.cs` (docs), `IEnlistmentConfigProvider.cs`, plus the two call sites `Hooks/EnlistmentMenuBehavior.cs:120` and `Hooks/EnlistmentDialogBehavior.cs:136`.

**Changes:** delete `ClassifyLeaveReason(double)` outright (ADR-004 forbids `[Obsolete]`; exactly two callers, both migrated here) and add `LeaveRequestResult EvaluateLeaveRequest(double nowDays)` with verdicts `Granted / RefusedTooSoon / RefusedInBattle`. `ContractEndDay` stops deciding honour entirely. `MinimumServiceDays = 7.0` (matches the Soldier promotion gate), `ContractDays 365.0 → 60.0` (matches the Sergeant gate — 365 made the existing "term complete" notice unreachable in any real campaign). Use `GetPartyId`, **not** `GetSnapshot` — this runs from dialog `OnCondition` delegates every conversation frame. Guard the float→int cast per `csharp-architecture.md`: `(int)Math.Ceiling(double.NaN)` is `int.MinValue` on net472/x64.

Interim wiring (until Batch 6): `Granted` → `RequestDischarge(DischargeReason.PlayerRequest)`; refused → the option's condition returns false and the dialog line does not appear. No `Desertion` producer exists this batch.

**Tests:** 13 named in #10 — `EvaluateLeaveRequest_TwentyDaysServed_Granted` is the S5 regression pin (today returns `Desertion`), plus `_NaNNowDays_RefusedTooSoon_DaysRemainingIsZero` (explicitly asserts *not* `int.MinValue`), `_ContractEndDayFarInFuture_StillGranted`, `_NullEnlistedAtDay_Granted`, `_UsesGetPartyIdNotGetSnapshot`. Note `ClassifyLeaveReason` has **zero** tests anywhere in the suite — that is why S5 shipped.

**Proof line:** enlist, serve 20 days, "Ask to be released" → the log reads `[Enlistment] service ended: PlayerRequest` and the arrears are **paid**, not forfeited.

**Protects P6 (config load):** these are compiled defaults; `EnlistmentConfigProvider` still never reads JSON, so nothing about loading changes. Save-compat cuts the right way — `ContractEndDay` is persisted per record, but since `EvaluateLeaveRequest` no longer reads it, **S5 is fixed on existing saves too**, not just new campaigns.

---

### **Batch 2 — Cheap adapter surface + attachment hardening** · ~180 lines · no player-visible change

Specs #1 (with R1's merged type), #9, #34's defensive half, #40's deletion, and the `Assess` parameter swap from #36.

**Files:** `Main/Adapters/CommanderTickSnapshot.cs` (new), `PlayerPresenceFlags.cs` (new), `ICommanderLordAdapter.cs` + `CommanderLordAdapter.cs`, `IMobilePartyAttachmentAdapter.cs` + `MobilePartyAttachmentAdapter.cs`, `IEncounterAdapter.cs` + `EncounterAdapter.cs` (deletion only), `IServiceAttachmentService.cs` + `ServiceAttachmentService.cs`.

**Changes:**
- `GetTickSnapshot(heroId)` per R1 — the one `Find<Hero>` a pump is allowed. Mint `MapEventToken` from a one-slot reference cache (`MapEvent` is `new`'d and never registered with `MBObjectManager`, so `StringId`/`Id` are unset — the engine supplies no identity).
- `SyncPositionCached(commanderHeroId, expectedCommanderPartyId)` + `InvalidateCommanderCache()` + `GetPresenceFlags()`.
- `ClearArmyAttachment()` per **R4** (leader carve-out), called from `ParkNear` and `RestorePresence`.
- `Assess(EnlistmentState, CommanderTickSnapshot, PlayerPresenceSnapshot)` — parameter swap only, **zero logic change**. Mechanical churn across `EnlistmentReconciler` + every `ServiceAttachmentServiceTests` / `EnlistmentReconcilerTests` construction.
- Delete `IEncounterAdapter.EncounteredPartyId` (compiler-proven dead).
- **Add to `GetSnapshot`'s own XML doc: "forbidden on any per-frame path."** State the rule in both new members' docs: *a pump may make at most one `Find<Hero>` per expensive pass and zero `Find<MobileParty>`.*

**Tests:** `PlayerPresenceFlagsTests.LooksParked_MatchesPlayerPresenceSnapshotForEveryFlagCombination` (8-row DataRow — pins the duplicate predicate), `CommanderTickSnapshotTests.Missing_HasExistsFalseAndZeroMapEventToken` + `MapEventToken_ZeroMeansNoMapEvent`, facade delegation tests, and **`EnlistmentAttachedToPolicyTests.MobilePartyAttachmentAdapter_NeverAssignsAttachedTo`** (reflection/IL scan — the §1 decision's pin).

**Proof line:** none — this batch is deliberately behaviour-neutral. Proof is `dotnet test TAOM.Tests` green with the pre-existing count plus the new tests, and **no change to any `[EnlistDiag]` line in a smoke run**. If the log changes, something leaked.

**Risk to protect:** the commander-party handle is a singleton field that survives campaign switches in one process. `InvalidateCommanderCache()` **must** be called on discharge, session launch and game load — Batch 3 wires those. The `StringId` revalidation makes a stale hit near-impossible, but the explicit invalidation is the reviewable guard.

**Deferred tech debt (record it):** `PlayerPresenceFlags` and `PlayerPresenceSnapshot` are two types with an identical `LooksParked`. The right end state is one `readonly struct`. Collapsing now would change null semantics across four services mid-plan (`GetPresence()?.X ?? true` stops compiling; mocks returning `null` become `default`). The 8-row equivalence test is the guard until then.

---

### **Batch 3 — The real-time service loop (foundational)** · ~450 lines · the largest and highest-risk batch

Specs #2 (as amended by **R2** and **R3**), #3, #4, #5, #6.

**Files:** new `IServiceMaintenanceService.cs` + `ServiceMaintenanceService.cs`; new `Hooks/EnlistmentMaintenanceBehavior.cs`; `Hooks/EnlistmentMenuBehavior.cs`; `Hooks/EnlistmentBattleBehavior.cs`; `Domain/EnlistmentRecord.cs`; `EnlistmentReconciler.cs`; `ServiceBattleService.cs`; `EnlistmentLoadNormalizer.cs`; `DischargeService.cs`; `IEnlistmentMenuService.cs` + `EnlistmentMenuService.cs`; `IEnlistmentConfigProvider.cs`; `EnlistmentIoC.cs`; **`Main/SubModule.cs` (orchestrator — one `AddBehavior` line)**.

**Changes:**
1. **`ServiceMaintenanceService`** per #2, with R2's `Assess` switch and R3's `CampaignTick`-only cheap tier. Two pumps, one policy owner. Keep #2's shared budget (`_maintenanceIntervalDt = 0.25f` on the campaign source, `1/30` per wait-menu frame), its NaN-as-zero-contribution gate written as a *positive requirement*, its `TryBreakBattleLatch` (the `EnlistedBattle → EnlistedAttached` demote is the only state transition the pump makes), its `TryRequestBattleJoin` with `MapEventToken` dedupe, and its `EnsureServiceMenu` with `IsRedirectable` ownership gate + `_menuFailures` backoff.
2. **`EnlistmentMaintenanceBehavior`** (~50 lines, ADR-002-clean) subscribing `TickEvent` and **`OnPartyAddedToMapEventEvent`** — the latter is the zero-poll answer to a commander joining a running fight, dispatched from `MapEvent.AddInvolvedPartyInternal`, and **neither mod subscribes to it today**. `OnPartyJoinedRunningMapEvent` must stay at one bool + one null-conditional deref + one ordinal compare against the *cached* party id, with **no logging on the non-match path** — it fires for every party joining every battle in the world.
3. **`EnlistmentMenuBehavior.OnWaitMenuTick`** → one-line delegation. Delete `PositionSyncTickInterval` and `_tickCounter`.
4. **`EnlistmentRecord`** gains persisted `PendingCommanderAttachment` (`pendingAttach=1`, emitted only when true) and `NextAttachRetryAtHours` (`nextAttachHour=`, parsed through `ParseFiniteDayOrNull` — a non-finite stamp freezes every retry for the rest of the campaign). Additive; old saves default to `false`/`null` = "retry immediately".
5. **`EnlistmentReconciler`** honours and advances the *same* budget on `BattleJoinRequired`, and keeps its stale-battle demote **byte-identical** to the pump's as the backstop for windows the pump cannot reach (paused clock, non-wait menu, conversation — all zero `_dt` and silence `TickEvent`).
6. **`EnlistmentBattleBehavior`** subscribes the pump's `BattleJoinRequested` with the existing detach-first idiom, so there remains exactly **one** hero-id → party-id → `TryJoinCommanderBattle` implementation.
7. `DischargeService.Reset` path calls `InvalidateCommanderCache()`.

**Write these two contracts verbatim into class XML docs, or the next session re-litigates them:**
- *`GameMenu.ActivateGameMenu`/`SwitchToMenu` set `TimeControlMode = Stop`, and `Campaign.Tick()` gates the dispatcher on `_dt > 0f` — so `TickEvent` does NOT fire in `encounter` / `join_encounter` / `town` / `castle` or during a map conversation. Neither pump can rescue those; only the existing `SetNextMenu` / `EnterMenuMode` patch edges can. Do not re-spec a poll for it.*
- *Ownership: the hourly reconciler remains the ONLY terminal authority (discharge, grace open/expire, captivity, `CommanderUnavailable`, stranded-encounter sweep). The pump asserts continuous invariants and makes exactly one state transition. The pump never calls `ReconcileHourly` and never injects `IDischargeService`.*

**Tests:** the full ~40 from #2 (gates, throttle incl. `Pump_NaNDt_ContributesNothingToBudget` over 1000 pumps, latch, join, edge, park, menu, config clamp), #3's five behaviour tests, #4's five, #5's eight record round-trips including `TryParse_LegacyRecordWithoutNewKeys_DefaultsToFalseAndNull`, #6's cross-authority pins — especially **`Pump_NeverExecutesDischarge`** (reflection over the ctor parameter types) and **`Reconciler_AndPump_UseIdenticalStaleBattlePredicate`** (same 8 flag combinations, decisions must match). Plus new `EnlistmentTickBindingTests` `[TestCategory("BindingVerification")]` for `TickEvent : IMbEvent<float>` and `OnPartyAddedToMapEventEvent : IMbEvent<PartyBase>`.

**Proof line:** with the column marching, let the commander join a battle that was **already running** (ride up to a fight in progress). The log emits `[Enlistment] maintenance BattleJoinRequested` and you are pulled into it — today this is structurally invisible, because `CampaignEventDispatcher.OnMapEventStarted` is dispatched exactly once, as the last statement of `MapEvent.Initialize`.

**Protects — read this before starting:**
| Protected behaviour | How this batch could break it | Guard |
|---|---|---|
| **P1 field-battle join** | The pump now raises `BattleJoinRequested` at ~4 Hz into the same subscriber the hourly path uses → double join | Three independent re-entrancy guards, all cheap, all tested: `r.State != EnlistedAttached`, `ServiceBattleService.TryJoin`'s own state guard, `p.IsInMapEvent`. Plus the shared `NextAttachRetryAtHours` budget (`ReconcileAttached_BattleJoinRequired_WithinSharedRetryBudget_DoesNotRaise`) |
| **P2 siege-assault join** | Unchanged path; the only new pressure is cadence | Same three guards; `Pump_PlayerAlreadyInMapEvent_DoesNotRaise` |
| **P3 hourly recovery join** | If `nowDays * 24.0` ≠ `CampaignTime.Now.ToHours`, the budget check silently suppresses hourly recovery **entirely** — restoring exactly the S2 behaviour this batch exists to fix | Give the equivalence its **own assertion**, not a comment. `ReconcileAttached_BattleJoinRequired_BudgetExpired_RaisesAndAdvancesBudget` + `_NoBudgetSet_RaisesImmediately` |
| **P5 post-battle wait-menu re-assert** | `EnsureServiceMenu` can now open a menu on paths that had none, and `"encounter"`/`"join_encounter"` **are** in the default redirect list | The `State == EnlistedAttached` gate is **load-bearing, not defensive** — `Pump_EnlistedBattle_DoesNotTouchTheMenu` protects the encounter/loot/aftermath menus |
| **P4 oath close, P6 config** | untouched | — |

---

### **Batch 4 — Encounter ownership policy** · ~320 lines · closes the CRITICAL that fires on the tester's existing save

Specs #28, #29, #30, #31, #32, plus #40's deletion (already done in B2).

**Files:** new `Main/Adapters/EncounterOwnershipSnapshot.cs`, `Domain/EncounterFinishIntent.cs`, `Domain/EncounterFinishVerdict.cs`, `IEncounterOwnershipPolicy.cs` + `EncounterOwnershipPolicy.cs`; `IEncounterAdapter.cs` + `EncounterAdapter.cs`; `EnlistmentService.cs`; `ServiceBattleService.cs`; `DischargeService.cs`; `EnlistmentReconciler.cs`; `EnlistmentMenuService.cs` + interface; `EnlistmentIoC.cs`.

**Changes:** one `IEncounterAdapter.GetOwnership(commanderPartyId)` producing a flat snapshot; one **pure** `EncounterOwnershipPolicy.Evaluate(intent, snapshot)` (no adapters, no logger, 100% unit-testable) with five intents — `OathHandoff`, `StaleBeforeCommanderBattle`, `JoinRollback`, `ParkedSweep`, `Discharge` — and two universal rules (`R0` nothing live → `NothingToFinish`; `R1` player in a foreign map event → `DeferPlayerOwnBattle`, so no site can ever finish the player's own battle). All five Finish call sites route through it. `IEncounterAdapter.Finish`'s **default parameter is deleted** so no site can silently inherit TAOM's inverted polarity (`PlayerEncounter.Finish(bool = true)`; TAOM's was `= false`, and `false` leaves `CurrentSettlement` set, which `EncounterManager.HandleEncounterForMobileParty` treats as a permanent encounter block — that is S3). `EnsureEncounterAgainst` stops force-clearing blind and returns `false` with a WARNING instead. #32 exempts `encounter`/`join_encounter` from the redirect while the commander is genuinely fighting, and adds the presence gate.

**Ship #32's presence gate (`!LooksParked → don't redirect`) as its own commit** so it can be reverted independently — it is the lower-confidence half and its failure mode is the player escaping the wait menu.

**Tests:** #28's 17 pure-policy cases — `Evaluate_Oath_SettlementEncounter_SkipsNotOurs` is **the** critical pin; #29's five `Received.InOrder` ordering pins on `TryJoin`; #30's `Evaluate_EveryFinishVerdict_ForcesPlayerOutOfSettlement` (5-row invariant); #31's discharge/sweeper cases incl. `Execute_CommanderDeadAndUnresolvable_StillFinishesEncounter`; #32's seven incl. `TryRedirectMenu_UnlistedMenuId_ReadsNeitherCommanderNorPresence` (hot-path pin).

**Proof line:** swear the oath to a lord **inside a town keep**. The town visit survives — you can still walk the town after the conversation — and the log reads `[EnlistDiag] oath left the live encounter alone: SkipNotOurs`.

**Protects:**
| | Break risk | Guard |
|---|---|---|
| **P4 oath encounter close** | The unconditional Finish added today becomes conditional. If `GetOwnership` mis-reports (a swallowed throw → `None`), the oath stops closing the encounter it *does* own and S3 returns | `GetOwnership` catches **per-field**, never wholesale, and logs at ERROR on a throw. The `ParkedSweep` still self-heals within one campaign hour. Rewrite the existing `CompleteOath` `Finish-before-Park` `InOrder` test to stub a commander-party encounter |
| **P1/P2 battle join** | `EnsureEncounterAgainst` returning false where it previously force-cleared means a foreign encounter the policy declines produces a **rollback instead of a join**. Correct — but it will surface as `could not join commander battle` lines in play. Do not mistake that for a new bug | Pin the whole ordering (ownership step → state transition → presence → position → seed → join → menu) with `Received.InOrder` so a refactor cannot move the new step inside the sequence |
| **P5 re-assert** | Rollback verdict is now conditional | Everything after the rollback Finish (forced transition, `EnsureParked`, `ReassertServiceMenu`) must still run on **every** rollback path regardless of the verdict — `TryJoin_RollbackWhenEncounterIsNotOurs_SkipsFinishButStillReparksAndReassertsMenu` |

---

### **Batch 5 — Discharge hand-back (INV-D1)** · ~200 lines · closes the frozen-menu soft-lock

Specs #7, #8, #9's wiring, #33+#39 per **R5**.

**Files:** `DischargeService.cs` (94 → ~140 lines; it is a service, so ADR-002's 150-line ceiling does not bind — but do not let it grow further), `Hooks/EnlistmentMenuBehavior.cs` (delete the now-redundant `ExitToLast`), `Main/Adapters/CommanderSnapshot.cs` (+`SettlementKind`), `CommanderLordAdapter.cs`, `Main/Adapters/PlayerPresenceSnapshot.cs`, `IMobilePartyAttachmentAdapter.cs` + `MobilePartyAttachmentAdapter.cs` (`GetPresence(commanderHeroId)`).

**Changes:** `Execute` becomes the complete fixed-order pipeline, and the order is load-bearing at every step:
```
1 guard → 2 Discharging → 3 CAPTURE commander snapshot (BEFORE Reset) → 4 log begin
5 RestorePresence → 6 ClearArmyAttachment → 7 encounter verdict (B4) → 8 NotEnlisted + Reset
9 EnlistmentEnded → 10 RestoreCampaignContext → 11 re-read presence + alarm → 12 return
```
6 after 5 (`SetAttachedToInternal` only tears down the inherited `MapEventSide` when `IsActive`); 7 after 6 (`PlayerEncounter.Finish` branches on `Army` and `AttachedTo`); **10 after 9** (`EnlistmentContentBehavior.OnEnlistmentEnded` cancels the active duty first, and step 10's `EnterSettlementAction` dispatches `OnSettlementEntered`, which `FieldDutyRuntime` treats as a duty **completion** trigger).

`RestoreCampaignContext` places the player in the commander's settlement (town/castle/village menu) when one exists, with a **mandatory rollback** — `LeaveSettlement()` if `EnsureMenuOpen` fails, because a player inside a settlement with no settlement menu is S3 one layer deeper. Load-time reasons (`HeirSuccessionOrPossessionMismatch`, `SaveNormalization`) are excluded — the saved position is authoritative. The tail `ExitToLast` is **gated on `CurrentMenuId == ServiceWaitMenuId`**: `GameMenu.ExitToLast` sets `TimeControlMode = Stop` **unconditionally** before delegating to the null-guarded manager, so an ungated call freezes campaign time with no menu open.

Per **R5**: `EncountersBlocked` gains `IsHeldInsideSettlement`, the discharge alarm splits WARNING (settlement-only) / ERROR (everything else), and `DistanceToCommander` is finally populated — **capture `record.CommanderHeroId` before `Reset()`** or the `after` snapshot silently reproduces `distToCommander=?`.

**State `INV-D1` in `IDischargeService`'s XML doc:** after `Execute` returns true, for **every** reason — presence restored exactly once before the record cleared; any live encounter finished per the ownership verdict; `AttachedTo` null; the player is **not** in `taom_enlistment_service_wait`; the record is `NotEnlisted`.

**Tests:** `Execute_EveryReason_LeavesTheServiceWaitMenu` (loop `Enum.GetValues(typeof(DischargeReason))` — **the** CRITICAL pin), `Execute_EveryReason_ClearsArmyAttachment`, three `Received.InOrder` ordering pins, `Execute_SettlementMenuFailsToOpen_LeavesSettlementAndExitsWaitMenu` (anti-softlock), `Execute_LoadNormalizationReasons_NeverMoveThePlayer`, `Execute_CapturesCommanderId_BeforeReset_SoAfterSnapshotHasDistance`, new `PlayerPresenceSnapshotTests`. Mechanical: 4 test files construct `DischargeService` and all gain ctor args.

**Proof line:** kill the commander (or let the grace window expire) mid-service. The player lands on the map or in the commander's town **with a usable UI** — instead of frozen in a wait menu whose two options are both condition-gated to false with no Escape. `distToCommander=` now prints a number in the DISCHARGE lines.

**Protects:** `TimeControlMode` goes to `Stop` after every discharge — vanilla-equivalent (leaving any settlement menu does the same) but it will read as "the game paused itself" in a smoke test. **Expected, not a bug.** New exposure: a discharge inside a settlement that is an active `DeliverGoods` target will now complete that delivery (five TAOM behaviours subscribe to `SettlementEntered`). Semantically honest, and the donor shipped it — but put it on the smoke list.

---

### **Batch 6 — Informed leave choice** · ~140 lines

Specs #11, #12. Depends on B1's policy and B5's pipeline (the deferred inquiry callback is exactly why the menu exit had to move into `DischargeService`).

**Files:** `Hooks/EnlistmentWaitMenuPresenter.cs` (+`RequestRelease`), `Hooks/EnlistmentMenuBehavior.cs` (collapses to `_presenter.RequestRelease()`, **drops two ctor deps — this is the line-shedding that keeps it under 150**), `Hooks/EnlistmentDialogBehavior.cs` (three answer lines + desert branch), `IInquiryAdapter.cs` + `InquiryAdapter.cs` (two optional trailing params for `SetTextVariable`).

**The one thing not to miss:** `ConfirmDesertion` runs on a **later frame**, outside the menu-option consequence. Re-check `_coopSession.IsAuthority` inside the callback — the gate at the option site no longer covers the moment the discharge actually runs. `RequestRelease_RefusedConfirm_NonAuthority_DoesNotDischarge` is the pin.

Register the dialog's `RefusedInBattle` line **before** `RefusedTooSoon` (one verdict per evaluation makes them disjoint, but registration order decides ties).

**Tests:** new `EnlistmentWaitMenuPresenterTests` (6 cases; the presenter has none today) + re-run `InteractiveDutyPresenterTests` unchanged — green proves the optional params didn't break NSubstitute's arg matchers. If one breaks, fix the matcher; **do not add an overload to dodge it.**

**Proof line:** ask for release on day 3 → the inquiry says *"…{DAYS} more days are owed. Leaving now is desertion: you forfeit the pay still owed to you."* with a real number, and choosing "Stay" leaves you enlisted.

---

### **Batch 7 — Settlement following** · ~250 lines · needs B0, B3, B4

Specs #21, #22, #23, #25, #26, plus **R2's free win** (the pump routes the new verdicts with zero pump edits) and **#24's** `OnWaitMenuInit` fix.

**Files:** `Domain/AttachmentAssessment.cs` (+2 verdicts), `IServiceAttachmentService.cs` + `ServiceAttachmentService.cs` (+3 members, +`IGameMenuAdapter` ctor dep), `EnlistmentReconciler.cs` (+2 switch cases + the warning exemption), `IEnlistmentConfigProvider.cs` (+`town`/`castle`/`village` to `RedirectMenuIds`), `Duties/FieldDutyRuntime.cs`, `Hooks/EnlistmentMenuBehavior.cs` (init fix).

**The design decision, so it isn't re-litigated:** the player **is** moved into the commander's settlement but is **held in the TAOM wait menu** throughout — never handed to vanilla town flow. `EnterSettlementAction.ApplyForParty` pushes no menu; the donor's crash came from the player *reaching* a vanilla settlement menu with no encounter (`game_menu_settlement_wait_on_init` opens with `PlayerEncounter.EncounterSettlement.IsVillage`, an unguarded NRE when `Current` is null), which #25 closes. Letting the player actually *use* the town is a different feature — every in-settlement affordance routes through `PlayerEncounter.LocationEncounter`, and a live `PlayerEncounter` while `EnlistedAttached` is precisely what the reconciler's sweeper destroys. That needs a new `EnlistedOnLeave` state; do not attempt it here.

Ordering inside `Assess` is load-bearing: **`SettlementExitRequired` is checked ABOVE the battle branch.** Joining a map event while `MainParty.CurrentSettlement` still points at some *other* settlement puts the party in two places at once, and `MapEvent.AddInvolvedPartyInternal` rewrites a siege **assault** to `SiegeOutside` off exactly that field for a joining defender.

`FollowCommanderIntoSettlement` is **one transaction**: `RestorePresence` → `MoveIntoSettlement` → assert the wait menu. Never separable — a settlement placement without the wait menu **is** the donor's crash state.

**Two mandatory companion edits** or you flood the log and un-park a legitimate player: the reconciler's `Attached` branch must skip both the position sync and the "NOT parked" warning when `presence.SettlementId` is non-empty (the party is deliberately active+visible and pinned to the gate); and `OnWaitMenuInit`'s unconditional `EnsureParked` must not fire inside the commander's settlement.

**Also this batch:** flip #30's `Discharge` intent's `ForcePlayerOutFromSettlement` decision if you want B5's settlement placement to survive — it lives on the verdict precisely so this is a one-line change.

**Tests:** #21's nine `Assess` cases (esp. `Assess_PlayerInSettlementCommanderInMapEvent_SettlementExitRequired_NotBattleJoin` — pins the ordering that protects the siege type), #22's ten executor cases, #23's seven reconciler cases incl. `ReconcileHourly_AttachedWhileInSettlement_DoesNotSyncAndDoesNotWarn` **and** `_AttachedNotParkedOutsideSettlement_StillWarns` (proves the exemption didn't swallow the real anomaly), #25's four redirect cases incl. `TryRedirectMenu_VanillaSettlementMenus_NotRedirectedWhenNotEnlisted` (proves discharge restores town access), #26's three.

**Proof line:** the commander's column enters a town. Your party is **inside** it (and leaves with him when he leaves) — instead of standing invisibly outside the gate for the whole stop.

**Protects P2 (siege join) — this batch is the one that most improves it:** with the player inside the besieged settlement as a defender, `AddInvolvedPartyInternal` stops rewriting the assault to `SiegeOutside`. Risk: this makes the main party **active and visible** while `EnlistedAttached`, which was previously an invariant violation the reconciler logged as an anomaly. The warning exemption is mandatory alongside it. Also `ServiceAttachmentService`'s ctor grows — every test construction breaks (compiler-caught).

---

### **Batch 8 — Dialog agency (talk / ask for work / settlement affordances)** · ~280 lines

Specs #13, #15, #16.

**Files:** new `Main/Adapters/IMapConversationAdapter.cs` + `MapConversationAdapter.cs`; new `IEnlistmentPlayerActionService.cs` + `EnlistmentPlayerActionService.cs`; `Hooks/EnlistmentMenuBehavior.cs`; `Duties/IDutyOrchestrationService.cs` + `DutyOrchestrationService.cs`; `EnlistmentIoC.cs`.

**Changes:** `CampaignMapConversation.OpenConversation(playerData, commanderData)` behind an adapter that **carries the `is MapState` guard itself** — `ConversationManager.OpenMapConversation` does an ungauarded `(GameStateManager.Current?.ActiveState as MapState).OnMapConversationStarts(...)` cast-then-deref. Re-run the gate inside `OpenWithHero`; never trust a stale condition (a frame passes between condition and consequence). `CanTalkToCommander` excludes `EnlistedBattle` (a conversation would tear the seeded `PlayerEncounter` that `ServiceBattleService` owns) and `EnlistedDetachedOnDuty` (the player is loose and can click the lord normally). Pass `isLeave: false` — an `isLeave` option on a wait menu makes `RunMenuOptionConsequence` call `EndWait()` **before** the consequence, killing the tick loop before the conversation opens.

`RequestDutyNow` extracts the offer half of `DailyOfferTick` into a shared `TryOffer` so both callers use ONE path, and reuses the **existing** `ShouldOfferDuty` cadence rather than inventing a second cooldown — asking is free, but the commander only has work when the rotation says so.

**ADR-002 arithmetic, do not skip:** `EnlistmentMenuBehavior` is at 134. This batch adds ~19 and needs ~14 back by moving `OnLeaveServiceSelected`'s body into `IEnlistmentPlayerActionService.TryLeaveService` and dropping the two orphaned ctor deps. Do it **in this batch**, not as a follow-up.

**Tests:** #13's twelve service cases + new `MapConversationBindingTests` `[TestCategory("BindingVerification")]` (three engine-surface pins), #15's eight incl. `RequestDutyNow_AndDailyOfferTick_ShareTheSameOfferPath` (anti-drift), #16's six.

**Proof line:** "Speak with your commander" opens the conversation, and the **reassignment** and **quartermaster** dialog lines appear — three shipped behaviours that have never once fired.

**Risk to watch in-game (the single most important check in this batch):** the conversation replaces `MapState` as the active game state, so the wait menu's `OnTick` stops for its duration. Recovery is engine-side (`MapState.OnMapConversationOver` → `MenuContext.Refresh()` → menu rebuild → `OnWaitMenuInit` + `RunWaitMenuCondition` → `StartWait()`), and `OnConversationEnded` is a second belt — but verify it, because a failure here silently kills the pump's wait-menu source.

**Interacts with B4:** the ownership gate must **not** set `CurrentConversationContext` to `CapturedLord` or `FreeOrCapturePrisonerHero` for the enlisted flow — both vanilla greeting conditions hard-return false for those, which would leave the map conversation with no line out of `start` and dead-end it.

---

### **Batch 9 — The live status board** · ~300 lines · the largest *felt* change

Spec #14 per **R6** (absorbing #42's activity reads), plus #18, #27, #43.

**Files:** `ICommanderLordAdapter.cs` + `CommanderLordAdapter.cs` (`GetActivity`, `GetEncounterBackgroundMesh`), new `CommanderActivitySnapshot.cs`, new `IServiceStatusService.cs` + `ServiceStatusService.cs`, new `Domain/ServiceStatusModel.cs`, `Hooks/EnlistmentWaitMenuPresenter.cs`, `Hooks/EnlistmentMenuBehavior.cs`, `EnlistmentIoC.cs`.

**The root cause, which is the whole reason this is a batch and not a one-liner:** `RefreshWaitText` currently calls `MBTextManager.SetTextVariable`, a **global** variable. `GameMenuVM.OnFrameTick` only recomputes when `IsMenuTextChanged()` fires, which compares the menu `TextObject`'s **`Attributes`**. A global never lands there, so the text is frozen for the whole term and raising the refresh cadence changes nothing. Fix: `args.MenuContext.GameMenu.GetText().SetTextVariable(...)`.

`ServiceStatusModel`'s hand-written value equality **is** the throttle — if it silently ignores a field, that field's changes never reach the screen. `Equals_IsFalse_WhenAnySingleFieldDiffers` with one `DataRow` per field is the guard.

Do **not** use `IArmyRhythmProbeAdapter.Probe` for activity — its body runs a locator-grid enemy scan, a full `MemberRoster` walk and a `Kingdom.All` war loop. Priced for one call per game hour, not a menu tick. `GetActivity` is the cheap sibling. Cache the commander **name** (`GetSnapshot` allocates via `Name.ToString()`) and re-resolve only when `CommanderHeroId` changes.

Guard the progress bar's NaN: `xp / (float)0` fed into `SetProgressOfWaitingInMenu` is the exact class `csharp-architecture.md` has now caught five times.

**Tests:** #14's eleven service cases (incl. `_NaNNowDays_ReportsNoDeadline_NotIntMinValue`, `_ResolvesCommanderNameOnce_ThenServesFromCache`), the per-field equality DataRow, #27's three `SelectWaitText` cases, #18's four pure `BackgroundMeshResolver.Resolve` cases.

**Proof line:** the wait-menu text **changes** when the column enters a settlement — *"resting inside Minas Tirith"* replacing the marching line. Any change at all proves the frozen-text root cause is fixed; today the sentence is identical from oath to discharge.

---

### **Batch 10 — Re-attach edges + reconciler hygiene** · ~120 lines

Specs #37, #38, #36's logging half.

**Files:** `IEnlistmentReconciler.cs` (+`ReconcileNow(double, string)`), `EnlistmentReconciler.cs`, `Hooks/EnlistmentBehavior.cs`, `Hooks/EnlistmentBattleBehavior.cs`.

**Changes:** hoist the stranded-encounter sweeper **above** the state switch and widen it to `EnlistedAttached | CommanderUnavailable | EnlistedDetachedOnDuty`, excluding `EnlistedBattle` (a live encounter is the legitimate loot/aftermath state) and `EnlistedPlayerCaptive` (vanilla owns the party). Add the **conversation guard** the donor has and TAOM lacks — mandatory once the pump is live, or the sweeper races the oath conversation's own encounter. Subscribe exactly **two** edges (`OnSettlementLeftEvent`, `OnPartyLeftArmyEvent` — the donor's only zero-budget retries); **decline the other four and record why** in `docs/features/enlistment.md`, since the pump already reduces their latency to sub-second and `simplicity-criterion.md` rejects six subscriptions for no win. Add a `_reconcileInFlight` re-entrancy guard (B7 calls `LeaveSettlementAction` *from* the reconciler, which dispatches `OnSettlementLeft` back into it). Add #36's diagnostic change-detector with a once-per-campaign-day heartbeat.

**Do NOT restructure the sweep→re-park chain.** The diff's narrowing (3) was **refuted** — control already falls through to the status switch and `AttachRequired → EnsureParked` runs in the same pass. `Reconcile_SweepsThenReParks_InSamePass` is the regression pin.

**Proof line:** the commander leaves a town → a `[EnlistDiag]` reconcile line with `trigger="settlement left"` on that same frame, and you leave with him instead of standing at the gate.

---

### **Batch 11 — Content beats (training + muster + after-action)** · ~250 lines · *partly a content effort, not a code fix*

Specs #17, #44.

**Files:** `Content/Domain/ServiceContentRecord.cs` (+`LastTrainingDay`, additive key), new `Content/ICompanyTrainingService.cs` + `CompanyTrainingService.cs`, `Content/EnlistmentDailyService.cs`, `Content/EnlistmentBattlePayoutService.cs`, `Hooks/EnlistmentContentBehavior.cs`, `Hooks/EnlistmentMenuBehavior.cs`, `EnlistmentIoC.cs`.

**Scale honesty:** the *code* here is small. The value is almost entirely in **writing good strings** — the muster brief, after-action report, promotion moment, four training outcomes, and the ten activity fragments from B9. Budget writing time, not engineering time, and resist adding new *mechanics* (camp activities, drill mini-games): the diff shows the feature's problem is that shipped content is unreachable, not that content is missing. `ProgressionTables.TrainingSessionXp = 20` already ships with **zero** consumers.

Hard constraints: **one consolidated message per beat**, never one per sub-system (three sources × fast-forward = a message every few real seconds); a config flag on the daily brief; all authority-gated; none on the frame path.

**Proof line:** exactly **one** morning muster message per campaign day, and "Drill with the company" grants XP once and refuses the second time the same day.

---

### **Batch 12 — Config JSON loading + localization** · ~150 lines code + a 12-language content pass

Specs #41, #19, plus #25's JSON keys.

**Files:** `EnlistmentConfigProvider.cs` (21 lines → ~120), `IEnlistmentConfigProvider.cs`, `Main/_Module/ModuleData/enlistment/enlistment_config.json`, `Main/_Module/ModuleData/module_strings.xml`, `tools/translation_overrides/*.json`.

**Last on purpose.** `EnlistmentConfigProvider` is a stub whose own doc comment promises JSON loading; today every value is an unreachable constant, so behaviour is *fixed*. Making it real is the batch that **adds** risk — a user who empties `RedirectMenuIds` disables the wait-menu guard entirely and reproduces several reported symptoms. Port `EnlistmentContentConfigProvider`'s shape exactly (do not invent a second pattern), including `ObjectCreationHandling.Replace` — without it Json.NET **appends** to the compiled list instead of replacing it, giving the union. That bug was fixed in the content provider **today**; `RedirectMenuIds_InFile_ReplacesDefaultList_DoesNotAppend` (supply 2 entries, assert `Count == 2`, not 14) is the single most important test in this batch. Log at WARNING when the redirect list ends up empty. Document the reload scope as **full application restart** (`Reuse.Singleton`), not "next save load".

Localization is ~30 new `{=key}` strings across B1/B6/B8/B9/B11, all literal in source so static extraction finds them (unlike the Duties layer, whose keys are built at runtime — do not copy that pattern). The existing localization validation test is the RED/GREEN: confirm it **fails before** registration and passes after.

**Proof line:** set `contractDays` to 3 in the JSON, restart Bannerlord, enlist — release is granted on day 3. And `dotnet test --filter Localization` green.

**Protects P6 (config load):** this batch *is* the risk to P6. The append-vs-replace pin has already bitten this feature once.

---

## §4. Scale summary

| Batch | Net new/changed prod LOC | Tests to write | Nature |
|---|---|---|---|
| 0 Settlement.Find | ~30 | 1 | code fix, ships alone |
| 1 Desertion policy | ~90 | 13 | code fix, ships alone |
| 2 Adapter surface | ~180 | ~10 + wide mechanical churn | plumbing, zero behaviour |
| **3 Real-time pump** | **~450** | **~55** | **foundational, highest risk** |
| 4 Encounter ownership | ~320 | ~35 | closes a CRITICAL |
| 5 Discharge hand-back | ~200 | ~20 | closes the soft-lock |
| 6 Leave UX | ~140 | ~8 | UX + copy |
| 7 Settlement follow | ~250 | ~30 | behaviour |
| 8 Dialog agency | ~280 | ~25 | unlocks 3 dead behaviours |
| 9 Status board | ~300 | ~20 | **largest felt change**; part copywriting |
| 10 Edges + hygiene | ~120 | ~12 | latency + log volume |
| 11 Content beats | ~250 | ~12 | **mostly a writing effort** |
| 12 Config + localize | ~150 | ~15 | infra + **12-language content pass** |

Roughly **2,760 lines** of production code and **~255 tests**. Batches 3 and 4 together are about a third of the total and carry nearly all the regression risk; batches 0, 1, 9 and 11 carry nearly all the *perceived* improvement. Batches 9, 11 and 12 are substantially **content** effort (strings, tuning, translation) rather than engineering.

**Symptom coverage:** S5 closes at Batch 1 · S3 closes across Batches 4+5 · S2 closes at Batch 3 · S4 closes across Batches 3+5+7 · S1 is incremental across 0, 8, 9, 11 and never fully "closes" — it is a feel symptom.

## §5. Dropped, and what that leaves unaddressed

- **#24 `ReconcilePlacement`** — dropped, subsumed by R2. Its `OnWaitMenuInit` fix survives into Batch 7. No confirmed finding left unaddressed.
- **#42's duplicate status service** — dropped per R6; its `GetActivity` reads survive into Batch 9. Nothing unaddressed.
- **#39's refusal to fold the settlement clause into `EncountersBlocked`** — overruled per R5 (#33's WARNING/ERROR split answers the objection). Its `DistanceToCommander` fix survives.
- **#34's inline `Army = null`** — overruled per R4 (would disband a player-led army). Its verdict and its invariant test survive in full.
- **#35's "move the sync to `TickEvent`" as a standalone spec** — absorbed into Batch 3 as R3. Its drift recalibration survives.
- **#40(b) `IsEncounteredPartyRelatedTo`** — dropped per R7 as redundant with #28's `GetOwnership`. If, after Batch 4, that member has no consumer, delete it rather than leave a second dead adapter member.
- **The donor's settlement "Leave military service" and "Renew your contract" options (#16)** — deliberately not ported. A discharge in a town with no commander present is exactly the unattended exit that produced S5; and a renewal surface before `ContractDays` settles would cement the 365 value.
- **Real town access while enlisted (an `EnlistedOnLeave` state)** — deferred with reasoning in Batch 7. It is a feature with its own save-compat and transition-table work.
- **`PlayerPresenceFlags`/`PlayerPresenceSnapshot` collapse** — deferred tech debt from Batch 2, guarded by the 8-row equivalence test.

**Recommended skills for the orchestrator** (I cannot invoke them): `/deep-review` before each of Batches 3, 4, 5 and 7 (each is ≥2 C# files and touches a feature module); `/verify` between every batch since the player is play-testing; `/localize` at Batch 12; `/issue` for each batch before implementation; and `/research` before Batch 8 if `CampaignMapConversation`'s two-arg overload does not resolve on the installed DLLs.
