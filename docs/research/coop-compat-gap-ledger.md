# Co-op compatibility gap ledger (TAOM v1.5.5)

Status of every TAOM defect and gap that Sternab's private `Sternab/TAOM-Coop` repo shims or
lists, checked against TAOM `bannerlord-1.5.x` on 2026-10-21. His register was written against
TAOM public 2.0.29.5 on Bannerlord 1.4.8, so each row was re-read in our code.

**Sources:** his `docs/coop-taom-compat-register.md`, `docs/current-compatibility-ledger.md`,
`docs/taom-dedicated-server-guide.md` section 2, and the shims under `src/TAOM.CoopCompat/`
(branch `codex/taom-coop`, HEAD `e3196ee3`). Provenance:
[register row](../reference/provenance-register.md#taom-coop-sternab). Background:
[coop-interop.md](../features/coop-interop.md), [bannerlordcoop-internals.md](bannerlordcoop-internals.md).

**Status values:** PRESENT (the defect exists in our code), PARTIAL (gated, but the gate switches
the feature off instead of making it work for each player), FIXED, UNVERIFIED (evidence missing),
REFUTED (the defect or its fix does not hold on v1.5.5), OPEN (real, but the planned fix was
refuted), DEFERRED (a maintainer decision holds it).

**Check every row against TAOM's own RCAs and the installed engine before acting on it.** The
register these rows came from was written against Bannerlord 1.4.8 and TAOM 2.0.29.5. In #965 three
of its premises (R1, R5, R9) did not hold on v1.5.5, and one of them had been refuted by a TAOM
RCA two months earlier.

**One framing fact:** `Main/_Module/SubModule.xml:76` tags TAOM `DedicatedServerType=none`, so
`TAOM.SubModule` runs headless only when the server forces it. Every `IsDedicatedServer` gate
matters only in that mode, and most of them stand a feature down.

## Remediation gates

| ID | Item | Status | Evidence | Owner | Action |
|---|---|---|---|---|---|
| R1 | A failed role-provider bind is cached forever; a client then reads both `IsAuthority` and `ShouldDeferToHost` as true | DEFERRED | Retry REFUTED (#965 review): BannerlordCoop loads GameInterface and Common in its own `OnSubModuleLoad`, so the first campaign-time bind succeeds; a bind fails only on an API rename, where a retry never heals. The split itself is real while unbound: `CoopSessionPolicy.cs:45` and `:87` | TAOM (maintainer decision) | Making `IsAuthority` false while unbound would stand down every BannerlordTogether peer; deferred on 2026-10-21 |
| R1b | A client may read authority before Coop's container is live at `OnSessionLaunched` | UNVERIFIED | `CoopSessionProvider.cs:121` (null check) and `:127` (the `TryGetContainer` invoke); consumers such as `WarOfTheRingBehavior.cs:86-91`. Missing: Coop's container timing on a client | TAOM, Coop | Log the probe result at each TAOM `OnSessionLaunched`, read it in a live join |
| R2 | `CollectAssemblyTypesShim` can give Coop AutoSync a partial type list | FIXED (#965) | Co-op scans GameInterface and its runtime `AutoSync` assembly with no catch (`AutoSyncModule.cs:51`, `ObjectManagerModule.cs:32`, `AutoSyncPatcher.cs:35,48`), so the swallow hid a broken co-op build. Now rethrown only while the BannerlordCoop module is active (`CollectAssemblyTypesPolicy`). Residual: while that module is enabled the policy matches by simple name, so another mod's assembly named `Common`, `Missions` or `GameInterface` also loses the swallow | TAOM | Done for Coop's own scans. Harmony and Autofac keep partial lists, and so does the engine's `GetTypesSafe`. But v1.5.5 `GauntletTutorialSystem.RegisterTutorialTypes` calls `GetTypes()` with no catch over the active game assemblies (decompile `SandBox.GauntletUI.Tutorial.GauntletTutorialSystem.cs:555-559`), so a half-loaded Coop assembly now throws there too; where that lands is UNVERIFIED |
| R3 | `SubModuleConstructionGuard` hides a failed Coop constructor | FIXED | Deleted (`docs/migration/v1.5.5-impact.md:26`) | none | Remove the stale mention in `bannerlord-together-compat.md` |
| R4 | SaveShield still swallows `MISSION-INIT` faults under co-op | OPEN (fix refuted) | A rethrow is worse on every target (#965 review, v1.5.5): `SpawnTroop` and `SetMissionMode` can run inside `OnMissionTick`, and an exception there leaves `tickCompleted` false (`Mission.cs:3749`), so the next frame's `WaitTickCompletion` (`:3594-3599`) hangs; a `MissionState.OnInitialize` rethrow leaves an inactive state on top; the loading-time rethrows leave the mission as half-initialised as the swallow does | TAOM | Shipped and reverted in #965. A real fix must stop the half-initialised mission from syncing, not rethrow |
| R5 | `Patch69_TournamentEndGuard` swallows an NRE in the tournament completion path | REFUTED | The swallow costs only the local winner panel: winner, prize, leaderboard and `OnTournamentFinished` run before the event (`TournamentBehavior.cs:245-264`). A rethrow runs inside `OnMissionTick` and hangs the next frame (see R4) | none | Shipped and reverted in #965 |
| R6 | `Hero._exSpouses` is null on replicated heroes; TAOM's screen finalizer leaves a half-pushed screen | PRESENT | No guard in `Main/`; `Patch37_CrashReport.cs:56-62` swallows on `ScreenManager.Tick` | Coop (field), TAOM (finalizer) | Coop initialises constructor-only collections; TAOM recovers the screen stack |
| R7 | Headless FaceGen exposes only the human race | PRESENT | `RaceManager.cs:70-73` falls back to `human` | DS, CoopCompat | A headless race provider on the server; TAOM logs the race count at startup |
| R8 | `DedicatedServerType=none` | PRESENT | `Main/_Module/SubModule.xml:76` | TAOM | The boot gate has passed: a spike build reached SERVING on 2026-10-22 and 2026-10-23, and the committed build `0fb0e6c13` on 2026-10-23 with one client joined. Flip with B9 (manifest flip and the generic staging rule), not before |
| R9 | PatchShield pass 1 may install before co-op is detected; pass 2 does not remove it | REFUTED | On v1.5.5 `Module.Initialize` fills the module list (`Module.cs:301`) before `LoadSubModules` (`:307`), so pass 1 already sees co-op; settled before in `docs/reviews/rca-coop-veto-surface-2026-08-01.md:115-119`. The live log shows pass 1 scanning 12 active modules | none | Shipped and reverted in #965 |

## TAOM feature parity

| ID | Item | Status | Evidence | Owner | Action |
|---|---|---|---|---|---|
| T1 | Special-resource victory earning depends on `MapEventEnded`, which co-op clients rarely get | PARTIAL (#967 C1) | `SpecialResourceEarningService.OnVictory` (`:60`) credits the leader of each registered player party on the winning side on the authority, keyed by hero; who is a player is the registry's answer alone in a session (`CoopPlayerControlQuery.Classify`), applied by `SpecialResourceEarnPolicy.MayEarn` (`:71`). Missing: the balance stays on the server until #967 C4 mirrors it to the owning client, and an in-game co-op run | TAOM | #967 C4 balance mirror, then a two-guest smoke |
| T2 | Prisoner and raid income credits the world-gen `MainHero` | PARTIAL (#967 C1) | Raid and hideout now pay the attacker-side player leaders (`OnRaid` `:91`, `OnHideout` `:105`). Prisoners pay nobody during a session and log once, because `OnPrisonerTakenEvent` carries only the roster (`SpecialResourceEarningService.OnPrisoners`, `:131`) | TAOM | #967 C2: patch the capture to name the capturing party, then credit that hero |
| T3 | Recruit cost charges `MainHero`, so players on a server recruit priced troops free | PARTIAL (#966: attribution fixed, charge has no effect yet) | Attribution: Patch113 brackets Coop's `HandleOnRecruitmentDone` with a thread-static scope (`CoopRecruitAttributionPatch.cs:59-70`, applied at `SubModule.cs:1866`), and `SpecialResourceEventAdapter.SnapshotRecruit` (`:199`) reads that scope, then `SpecialResourceEarningService.ChargeRecruit` (`:278`) charges the scoped party's leader through `RecruitChargePolicy.ChoosePayer` (`RecruitChargePolicy.cs:15-29`): a client pays nothing, a scoped recruit never falls back to the authority's own hero, and an unscoped recruit during a session charges nobody. Not effective yet: the guest's affordability gate (Patch51) reads a stale client balance, and the server charge floors at zero (`SpecialResourceService.cs:250-253`) until balances are server-owned. Coop's prisoner and mercenary recruit paths never raise `OnUnitRecruited` (Coop `PartyDoneLogicHandler.cs:726`, `MercenaryHireHandler.cs:142-146`), so their `recruit_cost` is never charged under co-op, host included. Not checked in game | TAOM | Server-owned balances and the prisoner and mercenary paths: #967. In-game check owed |
| T4 | Emissary troops are granted into a client-side roster | PARTIAL | `EliteEmissaryInquiryPresenter.cs:199-205` refuses the purchase on a guest, so on a server nobody can buy | TAOM, CoopCompat | An authoritative purchase command: charge and grant on the server |
| T5 | War of the Ring participation counts only the authority's `MainHero` | FIXED (#966: attribution only; state sync still open) | `PlayerInvolved` is also true when any party on either side is a player's (`WarEventSnapshotAdapter.cs:69-72`, `HasPlayerParty` at `:188-200`) or the event involves a kingdom holding a player's clan (`:162-186`); `Unknown` counts as not involved (`:171-173`). Single-player is unchanged because the provider reduces to the local comparison. Not checked in game | TAOM | Momentum state: see C2 |
| T6 | Troop weight sheds over-weight parties of remote players | FIXED (#966: the exemption; the setting is still per peer) | `PartyUpgraderUpgradeReadyTroopsHook.cs:52` returns for any mobile party whose `OfParty` is not `NotPlayer` (Player or Unknown), after the `LeaderHero` exit. The toggle is still `TaomSettings.cs:41` on each peer. Not checked in game | TAOM | Server-authoritative setting still open |
| T7 | Career choice groups collapse on refresh | FIXED | PR #373 (`b5bd1d280`) is an ancestor of HEAD; the screen was later rebuilt (`29d8ac413`) | none | Tell Sternab his shim can go |
| T8 | Career and special-resource writers act only for `MainHero` | PARTIAL (#967 C1, special resources only) | `SpecialResourceEarningService.OnDailyTick` (`:176`) ticks every living registered player hero on the authority (`SpecialResourceEventAdapter.SnapshotDaily`, `:171`), seeded to `StartingAmount`, with no desertion until #967 C4 and C6. Career writers still gate on `MainHero` (`CareerCampaignBehavior.cs:56,111,118`) | TAOM | #967 C4 balance mirror and #967 C6 notices for special resources; careers are a separate batch |
| B1 | Unreachable path targets assert every tick; TAOM_Map navmesh holes | UNVERIFIED | Not inspected | TAOM_Map, Coop | Get Sternab's coordinates; test the Edoras path on the server |
| X1 | The field camp overlay calls the local camp service on a co-op client | PARTIAL (#966: off for guests by design) | `ICampMenuActivationQuery.DefersToCoopHost` (`MapScreenCampMenuActivationQuery.cs:38`, which returns `ICoopSessionProvider.ShouldDeferToHost`) hides the camp button (`FieldCampOverlayVM.cs:176`) and makes `ExecuteOpenCampMenu` refuse (`:224`). A player host and a solo player keep the camp. Cost: under BannerlordTogether or another co-op mod TAOM cannot probe, or after a failed Coop role bind, `ShouldDeferToHost` is true on every peer, solo play included, so the button hides there too | TAOM | None planned; per-player camps would be a new design |
| X2 | Wanderer pre-introduction NRE for named heroes without a template | UNVERIFIED | No data scan done | TAOM data | Add a validator rule |
| X3 | Retired Misty Mountain troop ids (2.0.23) have no save alias | PRESENT | No alias in the repo | TAOM | Decide whether pre-2.0.23 saves are supported |
| X4 | Restored Full War saves miss configured wars | FIXED (TAOM half) | `WarOfTheRingBehavior.cs:86-91` | Coop | none |
| X5 | A null TAOM `BodyPropertyRange` aborts Coop's campaign-ready step | UNVERIFIED | Not scanned | Coop, TAOM data | Scan BodyProperties ids |

## Feature support contract

| ID | Feature group | Status | Evidence | Action |
|---|---|---|---|---|
| C1 | Race, culture, possession | PARTIAL | `PlayerPossessionService.cs:39,53` keeps choices in process memory only; a reconnect loses them | Server-held identity snapshot per player |
| C2 | War of the Ring | PARTIAL | `WarOfTheRingMomentumBehavior.cs:139` gated; attribution is fixed (T5); `TAOM.CoopCompat` relays the momentum state from the server to the clients (`MomentumRelayService`, #967 C3). Not checked in a session | In-game check (`taom.coopcompat.momentum.*` in `tools/ingame_checks/967.toml`) |
| C3 | Careers and special resources | PRESENT | T1, T2 and the special-resource half of T8 are partly closed by #967 C1 (careers and the balance mirror remain); T3 attributes the recruit charge but it has no effect until balances are server-owned | Server-owned balances with a command and acknowledgement protocol (#967) |
| C4 | Recruiting and emissary purchase | PARTIAL | T3 (attributed, no effect yet) and T4 (the guest purchase is refused) | As T3 and T4 |
| C5a | Recurring castle recruitment | FIXED (stand-down) | `CastleAiRecruitDispatcher.cs:87` | Accept, or design server-side recruitment later |
| C5b | Alignment desertion | FIXED (#966) | `AlignmentDesertionBehavior.cs:65,86,96-101` and `DesertionOwnerPolicy.cs:15-16,19`: during a session a remote player's clan (Player, but not `Clan.PlayerClan`) and any Unknown owner lose no troops; with no session this peer's own clan follows "Apply To Player", and in a session the authority's own clan (not in the registry) follows "Apply To AI Lords" with the AI clans. Single-player is unchanged. Decision: skip rather than relay. A remote player gets no desertion popup, because that relay is #967 | Done. Not checked in game |
| C5c | Culture marketplace | PARTIAL | `CultureMarketplaceBehavior.cs:73` runs on the server; settlement `ItemRoster` sync unverified | Confirm Coop syncs settlement stock |
| C5d | Troop weight | FIXED (#966: the exemption) | T6 | As T6 |
| C6 | Messenger, siege defence, culture conversion | PARTIAL | `MessengerCampaignBehavior.cs:103,204`, `CultureConversionBehavior.cs:83,103,114` gated; private state not synced | Snapshot, or disable during a session |
| C7 | Enlistment and field commission | PARTIAL (#966: offer hidden for guests by design) | `EnlistmentPlayerActionService.cs:96`, `FieldCommissionBehavior.cs:71` host-only; on a server the host is the placeholder hero. `EnlistmentDialogBehavior.ShowsOfferFor(verdict, deferToHost)` (`:109`, read at `:115-116`) hides the enlist offer for a guest; a player host keeps it. Battle merit scoring (`EnlistmentMeritMissionBehavior`) and field commission logic (`FieldCommissionMissionLogic`) are not added to a mission in a session (#967 C7). Same cost as X1 under a co-op mod TAOM cannot probe | None planned until per-player rules exist |
| C8 | TAOM mission behaviors | PARTIAL (#967 C7) | Every behavior has a role in `CoopMissionRoles`. The add-time filter is `CoopMissionRolePolicy.ShouldAdd` in `AddTaomBehavior` (`Main/SubModule.cs:2150`): the six `Unsafe` behaviors are not added in a session (five in an ordinary battle, six when the shader guard claims the mission). Every other behavior either checks `CoopMissionOwnership.IsOwnedHere` per agent, or is owned by an invariant its row in [coop-interop.md](../features/coop-interop.md#mission-behaviors-967-c7) names (an AI-controller gate, the player team). Not checked in game | TAOM | In-game list "Owed by #967 C7" in coop-interop.md |
| C9 | Tournaments | PARTIAL | R5 is REFUTED (no fix owed); grouping, colours and agent removal are Coop seams | A two-player tournament lifecycle run |

## Co-op-only rows (upstream candidates, not verified in depth)

| IDs | Note |
|---|---|
| J1, J2, J3 | All-or-nothing join baseline, dangling AI references, silent rejection: Coop join and capture code |
| J4, J5, J6 | Assembly resolve path, `Monster` registration, `ArmyRegistry` keyed by kingdom: Coop |
| B2, B3, B4 | Occupancy pause, non-idempotent roster deltas, native warning dumps: Coop and server build flags |
| S1, S2, S4, S5 | Capture of healthy parties, partial battle verdicts, tournament authority race, no position replication: Coop |
| S3, S11 | Tournament agent removal NRE; `HandleClosingHold` NRE after teardown: Coop, TaleWorlds |
| S12 | Headless banner palette returns `0xDEADBEEF`: send the colour id, not the value |
| K1 | Kingdom decisions addressed by list index: Coop protocol |

## Summary

- 36 TAOM-relevant rows across the three tables (counted by script over every status row after
  #967 C7; the earlier "34" miscounted): 5 PRESENT, 14 PARTIAL, 9 FIXED (R2, R3, T5 attribution only,
  T6 exemption only, T7, X4 TAOM half, C5a stand-down, C5b, C5d), 4 UNVERIFIED, 2 REFUTED (R5, R9),
  1 OPEN (R4), 1 DEFERRED (R1). #967 C1 moved T1 and T8 from PRESENT to PARTIAL, and #967 C7 moved C8.
- Most existing co-op gates stop corruption by switching the feature off. On a dedicated server
  that means no player gets special resources or emissaries. War of the Ring events now count a
  remote player's party (T5), but the momentum state itself stays per peer.
- Highest risk first, from the PRESENT, PARTIAL and OPEN rows: T3 (a guest's priced recruits still
  cost nothing until balances are server-owned, #967), T1 and T8 (the balance mirror, and career
  writes for remote players), R6 (half-pushed screen), and R4 (a hidden mission fault whose fix was
  refuted). The silent troop loss for remote players (C5b, T6) is closed by #966. R1 (authority
  fork on a client) stays DEFERRED by decision, not closed.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/enlistment.md](../features/enlistment.md)
- [docs/reference/provenance-register.md](../reference/provenance-register.md)
- [docs/research/taom-headless-server.md](./taom-headless-server.md)
- [docs/reviews/rca-coop-safety-nets-2026-10-21.md](../reviews/rca-coop-safety-nets-2026-10-21.md)

<!-- backlinks-end -->
