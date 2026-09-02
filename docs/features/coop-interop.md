# CoopInterop

TAOM's side of sharing one campaign with a third-party co-op mod without the two corrupting each
other's saves.

**Status (2026-08-02): co-op CONFIRMED WORKING by a player.** A community member completed a real
TAOM + BannerlordCoop session. That retires the "unverified, do not tell players it works" caveat
this file carried on 2026-08-01.

Be precise about what that confirms, though: it establishes that the pair **runs and is playable**.
It does not individually confirm each gate below — nobody has audited object sets between peers, and
two known-risk paths are still unproven (see [What is NOT done](#what-is-not-done)). Report it as
"working, with known rough edges", not as "fully verified".

The same session found a **frame-rate collapse caused by TAOM's own PatchShield**, now fixed — see
[PatchShield is skipped under co-op](#patchshield-is-skipped-under-co-op).

**Field report (2026-08-03).** A testing group ran TAOM v2.0.16 on Bannerlord 1.4.7.117484 across
three configurations — BannerlordCoop v0.1.1 client-hosted, a BannerlordCoop dedicated server, and
Bannerlord Together a0.5.3.1 — and filed nine sections of findings. Most of what this file gained on
that date traces to it. It does not upgrade the framing above: the group ran a server and two
clients, not a peer-to-peer object-set audit, so "nobody has audited object sets between peers"
still stands.

> **Why this file has content now.** It was a deliberate stub pointing at
> `bannerlord-together-compat.md`, on the sound principle that a second partial copy of the compat
> rules would drift. That held while there was one co-op mod. There are now two, with different
> architectures, and the BT doc is BT-specific — so this file owns what is SHARED plus the
> BannerlordCoop-specific authority layer, and still routes rather than restates.

## Routing

| For | Read |
|---|---|
| BannerlordTogether specifics, its no-decompile policy, its boot matrix | [`bannerlord-together-compat.md`](bannerlord-together-compat.md) |
| BannerlordCoop internals — Harmony ids, tick model, policies, object identity, integration surface | [`../research/bannerlordcoop-internals.md`](../research/bannerlordcoop-internals.md) |
| What happens to the hero a joiner created, and how TAOM's character-creation grants get re-applied | [`player-possession.md`](player-possession.md) |
| Why a TAOM-less host can corrupt every hero's race on a full client | [`hero-race.md`](hero-race.md) — the degenerate-legend capture guard |
| Raw analysis evidence (133 findings, 30 adversarial verdicts) | [`../raw/bannerlordcoop/`](../raw/bannerlordcoop/) |

## The two co-op mods are different projects

| | BannerlordTogether | BannerlordCoop |
|---|---|---|
| Launcher id | `BannerlordTogether` | **`Coop`** |
| Workshop | 3761555398 | 3770450698 |
| TAOM inspection | **No-decompile policy — respected.** Ids learned only at runtime from Harmony's public registry, or from its authors | Public upstream project, ships 893 of its own generated `.cs` files; decompiled 2026-08-01 |
| TAOM support | Detected, shields applied; nothing further verified | Detected + host-authority gating (below) |

`BattleLinkMPClient` is also detected; it is a multiplayer-window mod, not a shared campaign.
**Never enable two co-op mods together** — both transpile the same campaign types and each ships its
own `0Harmony`.

## Where the code lives

| File | Role |
|---|---|
| `ICoopPresenceProvider` / `CoopPresenceProvider` | Test seam over static `CoopPresence`. **Process-constant**: "is a co-op module enabled". |
| `ICoopSessionProvider` / `CoopSessionProvider` | **Session-varying**: "is a session live, and do I own the simulation". Reflection-bound, no compile-time reference to Coop. |
| `IDedicatedServerProvider` / `DedicatedServerProvider` | **Process-constant**: "is there a real player in this process at all". Reads the binaries folder this assembly loaded from, NOT co-op role. |
| `CoopSessionPolicy` | The pure authority decision, split out so it is testable without a game — same pattern as `PatchShieldPolicy` / `SaveShieldPolicy`. |
| `CoopSuppressedUiAttribute` / `CoopUiRegistrationPolicy` | Marks and filters UIExtenderEx types a co-op host has taken ownership of. |
| `SaveDefinerCollisionDetector` / `Guard` / `SaveDefinerRecord` | Base-id preflight and crash attribution. **Heuristic** — the engine keys on `_saveBaseId + saveId`, so a shared base is legal; engine-only groups are dropped and the rest warn rather than assert. [RCA](../reviews/rca-savedefiner-false-positive-2026-08-01.md). |
| `Diagnostics/HarmonyCensus*` | Runtime patch-overlap report — the sanctioned substitute for reading another mod's binary. |

Other half in `Dependencies/Foundation/`: `CoopPresence`, `CoopModuleList`, and the `PatchShield` /
`SaveShield` co-op inversions.

## Layer 1 — detection (process-constant)

`CoopPresence` reads the launcher's active-module list by reflection and matches ids by **exact,
case-insensitive equality** against `CompiledModuleDefaults` ∪ shipped `coop-modules.txt`.

Exact equality is why `Coop` had to be added explicitly: nothing about `BannerlordTogether` matches
it, so from the moment BannerlordCoop shipped until 2026-08-01 **every TAOM shield was inert against
the co-op mod players actually had.** Parsing is union-only — the file can add ids, never remove a
compiled default, because it also feeds PatchShield's protected-owner allowlist.
`coop-force-active.flag` forces presence on when detection fails (renamed build, fork, unknown mod).

Adding an id requires four coupled edits, pinned by `BundledDependencyManifestTests` +
`AssemblyRedirectListTests`: `CompiledModuleDefaults`, `coop-modules.txt`, and
`<ModulesToLoadAfterThis>` in **both** `SubModule.xml` manifests (the engine ignores
`<DependedModuleMetadatas>`).

When a co-op module is active: PatchShield stops unpatching foreign owners (stripping one peer's copy
of a sync patch does not crash — it silently diverges two campaigns, which is worse); SaveShield
**rethrows** `SAVE-LOAD` faults; the HarmonyCensus writes its report.

## Layer 2 — assembly resolution

The `AssemblyResolve` handler in `Dependencies/SubModule.cs` matches on **simple name only, discarding
the requested version** — safe only while TAOM's copy is newest. BannerlordCoop ships five higher:

| Name | TAOM | Coop |
|---|---|---|
| `Serilog` | 2.0.0.0 | 4.2.0.0 |
| `System.Runtime.CompilerServices.Unsafe` | 4.0.4.1 | 6.0.1.0 |
| `System.Memory` | 4.0.1.1 | 4.0.2.0 |
| `System.Buffers` | 4.0.3.0 | 4.0.4.0 |
| `System.Numerics.Vectors` | 4.1.4.0 | 4.1.5.0 |

All five removed from `RedirectedSimpleNames`; the BUTR stack stays. A version comparison would not
work (Coop calls `Assembly.Load` with a bare partial name — no version to compare) and it cannot be
gated on co-op presence (the handler installs from a static cctor, long before any probe).

## Load order

`TAOM.Dependencies` goes **above the entire vanilla stack, `Native` included** — not merely above the
co-op mod:

```
TAOM.Dependencies → BUTR alias stubs → Native → SandBoxCore → Sandbox → StoryMode → CustomBattle
                  → TAOM → TAOM_Map → LOTRLOME_Armory → Coop
```

**Why it is first, and not "after the vanilla modules like every other mod":** `TAOM.Dependencies`
bundles the whole BUTR stack (0Harmony, ButterLib, UIExtenderEx, MCM) and installs the
`AssemblyResolve` redirect from its **static constructor**. Everything loaded afterwards resolves
those assemblies through that hook. If anything constructs before it, a mod shipping its own bundled
`0Harmony`/`MCMv5` can win the AppDomain slot first — and then TAOM's patches attach to a Harmony
instance nobody else can see, which fails silently rather than loudly. This is pinned by the
`<ModulesToLoadAfterThis>` block in `Dependencies/_Module/SubModule.xml`, which lists `Native`,
`SandBoxCore`, `Sandbox`, `StoryMode` and `CustomBattle` explicitly, plus 20-odd known
BUTR-bundling consumer mods.

**Why `Coop` is last:** it also ships its own `0Harmony` (2.4.2.0, byte-identical to ours) plus
Mono.Cecil/MonoMod/Serilog 4.x. Constructing `TAOM.Dependencies` first lets the redirect collapse
those onto one instance, which is what keeps `Harmony.GetAllPatchedMethods()` — and therefore
PatchShield and the `[HarmonyCensus]` report — able to see across both mods. Ordering TAOM before the
co-op layer additionally matters for the `Priority.High` DeclareWar/MakePeace prefixes: TAOM validates
racial enmity and War of the Ring constraints first, so a blocked action is never synced.

A BUTR/BLSE launcher sorts this from the manifests. The explicit list matters for the vanilla
launcher and for anyone hand-ordering. Same order applies on **every** peer — Coop's join handshake
compares module lists and disconnects on a mismatch.

## Layer 3 — host authority (session-varying)

**BannerlordCoop does not stop a client ticking the campaign.** Its `PartyTickPatch` blocks only the
per-entity tickers, so `DailyTickSettlementEvent` and friends never reach a client — but the **global**
`DailyTickEvent` / `HourlyTickEvent` fire on both peers. Coop's own countermeasure is a hand-written
allowlist of ~127 `RegisterEvents` prefixes over *named vanilla* types; there is no generic hook a
third-party mod can register into, so TAOM authors its own.

**Three signals, not one.** The predicates below all come out of the session/role probe. Two other
facts are process-constant and answer different questions. Both directions have cost TAOM a real
bug: presence read where session/role was needed (the diplomacy vetoes, 2026-08-01), and no signal
at all where the dedicated-server question was the one being asked (special-resource earning,
2026-08-03).

| Signal | Question it answers | Constant for the process? | Read it at |
|---|---|---|---|
| `ICoopPresenceProvider.IsCoopActive` | Is a co-op module enabled? | Yes | Patch-application and other one-shot startup decisions — PatchShield install, UI registration |
| `ICoopSessionProvider` (the predicates below) | Is a session live, and do I own the simulation? | No | Anything that mutates shared world state |
| `IDedicatedServerProvider.IsDedicatedServer` | Is there a real player in this process at all? | Yes | Anything that credits, charges or rewards `Hero.MainHero` |

**`IsDedicatedServer` is deliberately NOT derived from co-op role**, and that distinction is the
whole reason the type exists. A client-hosted session's host also reports `IsServer`, but it is a
real player at a real keyboard who must keep earning normally; only on a headless server is
`Hero.MainHero` the idle world-gen hero the campaign was created around. So the provider reads the
binaries folder this assembly was loaded from — `Assembly.GetExecutingAssembly().Location`
containing `Win64_Shipping_Server` — which is a fact about the process rather than a probe of
another mod's state, and cannot change mid-session. An unreadable location reports "not a server",
because every gate built on it only ever suppresses behaviour.

**Which predicate to use.** Three, and picking the wrong one has already shipped bugs both ways:

| Use | When |
|---|---|
| `IsAuthority` | A world-mutating handler that only BannerlordCoop can gate. Fails open to singleplayer. |
| `ShouldDeferToHost` | A shared-world DECISION (diplomacy veto, alliance enforcement, global time). Also covers co-op mods TAOM cannot probe. |
| `MayWriteSaveBackedState` | About to write a field that round-trips through a `SyncData` key. |

Do **not** gate on `ICoopPresenceProvider.IsCoopActive` for anything world-mutating. It is
process-constant — true whenever the module is merely *enabled* — so it disabled TAOM's diplomacy
rules for solo players and for the co-op host itself (Codex, 2026-08-01). It is correct only for
one-shot startup decisions such as UI registration.

**One consumer breaks that shape and is not a violation.**
`PlayerPossessionService.TryConsumePossession` gates on `IsCoopActive`, and what follows it is a
hero mutation. Presence is used there as an **heir-succession discriminator**, not as an authority
decision: `Hero.MainHero` also changes in ordinary solo play when the player continues as an heir,
and the presence gate is what keeps a solo heir out of the re-grant path entirely. It is one of
three independent guards, any one of which suffices — presence, single consumption, and a
`SyncData`-persisted per-hero marker (`_taom_possessionReconciledHeroes`) so a reconnect cannot
re-grant. See [player-possession.md](player-possession.md).

`ShouldDeferToHost` keys on whether the ROLE PROBE RESOLVED, which is what makes it safe for
BannerlordTogether: TAOM cannot read host/client there, so it yields on every peer. Gating those
decisions on `IsAuthority` instead would fail open and gate nothing.

`CoopSessionPolicy.IsAuthority = !sessionActive || isServer` — **fails open to singleplayer**. Every
input is best-effort reflection into another mod; a false negative would silently disable TAOM
features for a solo player, which is worse and far less diagnosable than the divergence it guards.

Two traps the provider exists to avoid, both verified in source:

- **`ContainerProvider.Alive` is permanently `false`** — an inline static initialiser evaluated while
  the backing field is still null. Bind `TryGetContainer` instead.
- **`ModInformation.IsServer` defaults `false` and is sticky.** `IsClient` is its negation, so read
  alone it reports "client" in plain singleplayer whenever the Coop module is enabled, and "host"
  forever after hosting once. Only meaningful alongside a live session.

### Gated (host-only)

**Gate every handler that reaches the mutation, not just the tick.** The first pass gated only the
tick handlers and a deep-review data-flow pass found two HIGH bypasses within hours: sibling handlers
on the *same* behaviour calling the *same* service method with no gate. When adding a gate, grep the
behaviour's whole `RegisterEvents` and follow each handler to the service.

| Behaviour | Gated handlers | Why |
|---|---|---|
| `CultureConversionBehavior` | **`OnSettlementOwnerChanged`**, `OnDailyTick`, `OnGameLoaded` | Replaces notables via `HeroCreator.CreateNotable`. Client's store holds the same pending records — it loaded the host's save. `OnSettlementOwnerChanged` was left ungated on the reasoning that queuing a pending timer is harmless; but the store is `SyncData`-backed and the daily processor that would mature or clear those records is itself gated, so a client accumulated conversions nothing ever services (Codex, 2026-08-01). |
| `RaceAgeBehavior` | `OnDailyTick` | Re-kills locally what the host already replicated. |
| `WarOfTheRingBehavior` | `OnDailyTick`, **`OnSessionLaunched`** | Both call the identical `CheckPhaseTransition` → `DeclareWar`. `OnSessionLaunchedEvent` fires on every peer, and a co-op join *is* a save-load, so an ungated client issued its own war set on connect. |
| `WarOfTheRingMomentumBehavior` | `OnDailyTick`, **`OnKingdomDestroyed`, `OnMapEventEnded`, `OnSiegeCompleted`, `OnRaidCompleted`, `OnArmyGathered`, `OnSessionLaunched`** | `MomentumWarState` rides TAOM `SyncData` that nothing replicates. `OnKingdomDestroyed` reached the same `CheckAndApplyVictory` → `EndWar`/`MakePeace` as the tick. Daily gate sits **after** `RefreshMapMeter` — local UI a client still needs drawn. |
| `MessengerCampaignBehavior` | `OnHourlyTick` | Arrival writes `MobileParty.MainParty.Position` — on a client, *its own* party. |
| `SiegeDefenseBehavior` | `OnHourlyTick` | `Clan.Influence` + global relation off `Hero.MainHero`, which differs per peer. **Known limitation below.** |
| `CastleRecruitmentBehavior` | `OnGameLoaded`, `OnNewGameCreated` | The one castle path a client reaches; creates heroes. |
| `SpecialResourcesBehavior` | `OnMapEventEnded`, `OnRaidCompleted`, `OnPrisonerTaken`, `OnHideoutCompleted`, `OnTournamentFinished` | **A dedicated-server gate, not an authority gate.** All five earn paths run through a private `CanEarn()` → `SpecialResourceEarnPolicy.MayCreditMainHero(IDedicatedServerProvider.IsDedicatedServer)`, which logs once and then stays quiet. A headless server was banking prisoner and raid income against the idle world-gen hero — dozens of `[SpecRes] PRISONERS: +N` lines — while the remote players who fought those battles earned nothing. |

**What makes any peer earn at all is the participation fix in the same change.** The victory gate
used to ask whether the player IS the winning side's `LeaderParty.LeaderHero`, conflating
participating with commanding. That was never multiplayer-only: in ordinary single-player, joining
any lord's army stops you being the leader party's hero, so every victory you fought paid zero.
Multiplayer made it total rather than different — under a client/server split no player leads the
authoritative side either, and one reported session produced a single `MapEventEnded` out of 33
fought missions, with `state=None`. The gate is now
`SpecialResourceEarnPolicy.IsPlayerVictory(mapEvent.PlayerSide, mapEvent.WinningSide)`, with
`BattleSideEnum.None` on either side failing it, because a client routinely observes an unresolved
battle the server has already decided.

**Siege defence uses a split gate, not a wholesale one (fixed 2026-08-01).** The hourly tick did two
unrelated jobs and the first pass gated both, so a co-op client could defend a siege to completion
and receive no influence, no relation and no message. `ISiegeDefenseService.OnHourlyTick` is now two
methods:

| Half | Method | Gate | Why |
|---|---|---|---|
| Shared | `OnHourlyTickShared()` | **Authority only** | Expires events and prunes `_activeEvents`, which backs the `_taom_siege_active_events` save key. A client pruning a timeline the host owns is the divergence this layer exists to stop. |
| Local | `OnHourlyTickLocalPlayer()` | **Every peer** | Grants the reward to the hero *this* peer is playing, keyed on `MobileParty.MainParty` / `Hero.MainHero`. Same reasoning that leaves `CareerQuestCampaignBehavior` ungated. |

`GrantReward` sets `RewardClaimed` on the local `_activeEvents` entry. That is a write to a
save-backed field, but each peer keeps its own TAOM `SyncData` after join (see "What is NOT done"),
so the flag reads naturally as *this peer's player has claimed it* — which is the intended meaning.
The consequence to know: on a client the shared sweep never runs, so expired entries linger until
`OnSiegeEnded` removes them. Harmless — the local half filters on `!Deadline.IsPast`, so a stale
entry can never pay out. Pinned by three tests in `CoopAuthorityGateTests`.

### Deliberately NOT gated

`CareerQuestCampaignBehavior` (daily) — per-player, so gating it would strip career quests from
clients.

The original justification said "keyed entirely on `Hero.MainHero`", and **that was false when
written**: the one-quest-at-a-time dedup scanned `QuestManager.Quests` globally, and a client loads
the host's save — so the host's active career quest blocked the client from ever being offered one.
Fixed 2026-08-01 by filtering on `CareerQuest.OwnerHeroStringId`; the claim is now true of the scan
as well as the offer. Codex found it after the claim had already been written down twice and
believed once. **Still owed: in-game confirmation that quest start does not trip the `StringId`
suppression** (see the P1 note above).

## Client-side object creation

[Source-verified, runtime-unconfirmed] Constructing any `MBObjectBase` on a client inside
`CampaignState` throws: `MBObjectBasePatches` prefixes the `StringId` **setter** and returns `false`
when the sync policy disallows the write, so `CreateObject<T>` leaves `StringId` null and
`MBObjectManager.cs:215` calls `Dictionary<string,T>.TryGetValue(null, …)` → `ArgumentNullException`.
Full chain in the internals doc. **Not TAOM-specific** — any mod creating an `MBObjectBase` on a
client hits it; TAOM merely had two paths in, both now gated.

## Client-reachable entry points the gate later blocks

These share one shape, and it is the **opposite** of the divergence the gates were built for. The
gate correctly stops a client mutating shared state — but the *entry point* is still
client-reachable, so a client can start something it can never finish, and pay for it. Nobody
desyncs; the client just quietly gets nothing.

| Area | What a client could do | What it never got | Status |
|---|---|---|---|
| Messengers | Pay `MessengerGoldCost` and enqueue a `PendingMessenger` | Hourly processing is authority-only, so it never advanced or arrived — gold simply lost | **Fixed 2026-08-01** — the send refuses on a non-authority peer and logs `[Messengers][coop] send refused on non-authority peer — delivery is host-side` |
| Elite emissary | Pay special resources for an elite troop | The troop landed in a client-side roster the next resync overwrote — pay real, get phantom | **Fixed 2026-08-03** — `ExecutePurchase` refuses before charging when `ShouldDeferToHost` |
| Siege defence | Be prompted, accept, and be tracked | Reward tick is authority-only | Partly addressed by `_locallyClaimed`; the prompt/accept path still needs an owner check |
| Career quests | Be offered and accept | — (fixed: the dedup scan now filters by owner) | **Fixed 2026-08-01** |

**Both fixes decline rather than forward, and that is the ceiling here.** Granting either
authoritatively would need TAOM to send a message across the co-op layer, which it cannot do without
a compile-time dependency on one specific co-op mod. Declining is the honest behaviour, not a
placeholder for a forwarding path that is coming.

The emissary decline shows the player `{=taom_emissary_coop_guest}` *"The emissary only deals with
the host of this campaign."* **That string is not yet localized** — it appears only at
`EliteEmissaryInquiryPresenter.cs`, in no localization XML, so `/localize` is owed for it.

**Do not simply ungate the messenger hourly body** if the send is ever re-opened: arrival calls
`target.ChangeState(...)` and `EnterSettlementAction.ApplyForCharacterOnly(...)`, which are
shared-state mutations, and `PendingMessenger` still has no owner field (`TargetHeroId`,
`DispatchTimeDays`, `Position`, `Arrived`) for processing to filter on.

**Also open — `CareerQuest` construction on a client [P1].** `new CareerQuest` on player acceptance
is client-side `MBObjectBase` construction during a live campaign: Coop's `MBObjectBasePatches`
prefixes the `StringId` setter and returns false when the sync policy disallows the write, and
`QuestManager.OnQuestStarted` then adds the object anyway. It does not go through
`MBObjectManager.CreateObject<T>`, so the exact `TryGetValue(null, …)` chain is not proven for this
path — but "per-player, therefore safe" is not a sufficient defence, and this needs a live session
to characterise. The existing try/catch around acceptance is containment, not correctness.

## What is NOT done

- **No MCM settings parity: reported, not yet exchanged.** TAOM ships **228** settings across
  four MCM classes (the 284 here counted `[SettingPropertyGroup]` lines alongside the properties;
  the split is 209 in `TaomSettings`, 7 in `BattleLoadDiagnosticsSettings`, 6 in
  `CrashReportSettings` and 1 in `BlowDiagnosticsSettings`).
  **169 are simulation-relevant**, traced to the feature that consumes each one and kept when that feature
  ships a GameModel, CampaignBehavior, MissionBehavior or Harmony patch; all 169 are in
  `TaomSettings`. The 57 excluded are instrumentation, player-local inventory convenience,
  presentation, one action button, and the three time-acceleration knobs whose UI co-op already
  suppresses; the list with its reasons is
  `Main/Features/CoopInterop/CoopSettingsRelevance.cs`.

  Those numbers are pinned per class in `SettingsFingerprintTests`, which is the guard that a new
  setting cannot arrive unclassified: "relevant or excluded" is true of everything by construction
  — classification is include-by-default — so only the count can fail, and adding a setting
  anywhere moves one and forces the decision. Two further tests keep the list itself honest: no
  excluded name may stop matching a real property, and no two settings classes may share a
  property name.

  `SettingsFingerprint` hashes those 169: one code per MCM group plus a global, culture-invariant
  so a comma decimal separator cannot fake a mismatch — and `SettingsFingerprintLog` writes them
  to each peer's log under the co-op gate. It reads all four classes, not just `TaomSettings`:
  every diagnostics property is excluded so this moves no hash today (a test asserts exactly
  that), and it means a simulation-affecting setting added to a diagnostics page later is covered
  the day it lands. That makes the "compare settings manually" workaround a comparison of a few
  short strings instead of 169 values read off two screens.

  **All four classes are `AttributeGlobalSettings<T>`, and the reasoning above depends on it.**
  MCM has three scopes, and only one of them travels with a save. Verified against MCM 5.12.1:
  `MCM.Internal.GameFeatures.PerSaveCampaignBehavior : CampaignBehaviorBase` carries
  `SyncData<Dictionary<string, string>>("_settings", ref _settings)`, so **PerSave** is in the
  savegame. **PerCampaign** is not, despite the name — `PerCampaignSettingsContainer` roots at a
  `PerCampaign` directory, then `GetOrCreateDirectory(RootFolder, GetCurrentCampaignId())`, and
  loads through `ISettingsFormat.Load(…)`: a per-machine file keyed by campaign id. It would NOT
  arrive in parity with the host's save — the campaign id matches while the local file may not
  exist at all, which is worse than global rather than better. TAOM declares neither:
  the only `AttributePerCampaignSettings` reference in the tree is a reflection target string in
  `McmSettingsCollector.cs`, which scans third-party mods. So nothing here is save-inherited and
  nothing arrives guaranteed-equal. **If a PerSave or PerCampaign settings class ever lands in
  TAOM, this paragraph stops being true and the fingerprint's meaning changes with it** — a
  PerSave class would be equal by construction and need not be hashed; a PerCampaign one would
  need hashing more urgently than a global, not less.

  **A matching fingerprint means the MCM settings agree, and nothing wider.** It covers the 169
  values on the four settings pages. It does not cover the `ModuleData` JSON/XML that several
  features fall back to when a setting is unset, the `TAOM.Dependencies` flag files
  (`patchshield-disabled.flag`, `saveshield-swallow-disabled.flag`, `coop-force-active.flag`),
  or any difference outside TAOM. None of those three flags gates a setting in the 169 (each
  governs a Dependencies-side shield with no MCM property), but the general point holds: read the
  log line as "our settings pages match", not as "our installs are equivalent".

  **Still not done:** peers do not exchange them. Putting the fingerprint in save metadata and
  comparing on join is the remaining step, and it cannot be verified without two machines in a
  session — `FingerprintReport.DivergentGroups` is already there for it. BannerlordCoop still
  syncs no setting, so divergent settings continue to defeat everything above; they are now
  visible rather than silent.
- **No ModuleData content hash.** Coop's handshake compares module ids and version strings only, and
  disconnects on mismatch — so identical TAOM versions on all peers are mandatory, and a TAOM version
  bump is a co-op compatibility break.
- **No sync of TAOM's own campaign state.** Clients get the join-time baseline from the host's save,
  then never hear about WotR momentum or culture-conversion deltas.
- **War of the Ring participation credits one kingdom, not one session.**
  `WarEventSnapshotAdapter.FromSiege` now applies the same `IsPlayerRelated` test as the battle
  snapshot beside it (fixed 2026-08-03), so taking a fief inside an ally's army finally records a
  player event — that was a single-player bug too, and it is the half that is fixed.
  `IsPlayerRelated` resolves to "the main party, or any party whose `MapFaction.StringId` equals the
  player kingdom id", and that kingdom comes from the LOCAL player context. So a remote player in a
  DIFFERENT kingdom is still never credited, and on a dedicated server the reference kingdom is the
  idle world-gen hero's. Crediting across peers needs a seam TAOM does not have. (The field report
  stated the requirement "can only be satisfied by the authority's MainHero" — that was inaccurate
  even before the fix: any party in that MainHero's kingdom already satisfied it.)
- **Dedicated server: no longer "unsupported", but not supported either.** The 2026-08-03 field
  group ran TAOM on a BannerlordCoop dedicated server by hand-copying the client binaries into
  `bin/Win64_Shipping_Server/`, and that session is the evidence source for the race-capture,
  SpecialResources and `action_sets.xml` findings on this page. The build now does that copy: both
  `Main/TAOM.csproj` and `Dependencies/TAOM.Dependencies.csproj` carry a
  `MirrorWin64ShippingClientToServer` target modelled on the existing `…ToEditor` mirror. It
  mirrors the **assembled** client folder rather than build output, deliberately — the vendored
  natives and NuGet companions only exist there, after `PostBuildCopyToModules`. A verified
  deploying build mirrored 10 files for TAOM and 42 for TAOM.Dependencies. A server that boots also
  needs the `LOTRLOME_Armory` `action_sets.xml` fix: build 117131, which the dedicated-server engine
  ships, throws `KeyNotFoundException` in `MBObjectManager.MergeElements` at schema path
  `/action_sets/action` on data that 1.4.7.117484 tolerates. What is still true: both manifests tag
  `DedicatedServerType=none` (`Main/_Module/SubModule.xml`, four entries in
  `Dependencies/_Module/SubModule.xml`), the appliance's `engine/Modules` ships no TAOM, and its
  `default_new_game.sav` bootstrap is a vanilla world. An earlier revision of this bullet also said
  the appliance runs on `Microsoft.NETCore.App` against TAOM's .NET Framework 4.7.2; that is
  **unverified and now in tension with the field evidence** — treat it as a claim to re-check, not
  as the reason not to try. The listen-host path remains the one that carries TAOM into the spawned
  server process with no copying at all.

## PatchShield is skipped under co-op

`PatchShield.Install()` is suppressed whenever a co-op module is active
(`PatchShieldPolicy.ShouldInstall`). This is a **performance** fix, not a safety one.

A shield finalizer binds `__originalMethod`, so Harmony's generated wrapper pays a
`MethodBase.GetMethodFromHandle` plus a try/catch **on every call** (~50 µs) — the same mechanism
that turned a millisecond tournament teardown into a measured 104–109 s freeze in #331. Co-op
amplifies it: Coop's AutoSync transpiles every declared method and constructor of 43 campaign types,
and Coop's `PatchAll` runs on connect, *before* TAOM's `OnGameInitializationFinished` pass — so pass
2 shielded that entire surface. Those methods are the campaign hot path, so the symptom was frame
rate rather than a single stall. A player profiled it and traced it here.

Extending `ExcludedTargetNamespacePrefixes` would have been the wrong lever: adding
`TaleWorlds.CampaignSystem` there suppresses shielding **in solo play too**, because that list is not
co-op-scoped.

**What this gives up:** the swallow half — surviving `MissingMethodException` /
`MissingFieldException` / `TypeLoadException` from engine drift. Under co-op that is the right trade
and matches SaveShield, which already rethrows save-load faults on the same reasoning: a visible
crash beats a silent divergence between two campaigns. The unpatch half was already withheld.

**Known cost:** a player with the co-op module merely *enabled* but playing solo also loses the
shield, for no benefit — Coop only calls `PatchAll` on connect. Unavoidable here, because install
runs before any session can exist, so there is nothing session-scoped to read. This is the one place
where **module presence is the correct signal** and `ICoopSessionProvider` is not; `CoopPresence`'s
class docs explain why a patch-application site needs a process-constant fact.

**This makes two steps in community dedicated-server recipes redundant.** Those recipes tell
operators to drop `patchshield-disabled.flag` and `saveshield-swallow-disabled.flag` into
`Modules/TAOM.Dependencies/`. Under co-op both are already the default:
`PatchShieldPolicy.ShouldInstall(coopActive, disabledByFlag) => !disabledByFlag && !coopActive`
skips install outright, and `SaveShieldPolicy.ShouldSwallow` already returns false for the
`SAVE-LOAD` category. The flags still work — and `saveshield-swallow-disabled.flag` still does
strictly more, because it also rethrows the `MISSION-INIT` chain, which co-op deliberately leaves
swallowing (that fault is local: one broken battle, not a corrupted campaign).

## Verification status

Confirmed by a player (2026-08-02):

- TAOM + BannerlordCoop launches, connects and plays.
- The PatchShield frame-rate collapse is fixed by skipping install.

Still unconfirmed — worth walking if you have two machines:

1. `[CoopPresence] EnsureProbed: co-op module(s) ACTIVE: Coop` in `Modules/TAOM.Dependencies/diag.log`,
   and `[PatchShield] co-op module(s) active … install skipped` alongside it.
2. A `[HarmonyCensus]` block naming `Bannerlord.Coop`; check its `TRANSPILER CONFLICT` rows against
   TAOM's 7 transpilers.
3. TAOM behaves identically **solo** with the Coop module merely enabled — what the `IsAuthority`
   fail-open direction protects, and now also where the PatchShield cost above lands.
4. The spawned `/server` process starts TAOM cleanly (no blocking UI).
5. No `ArgumentNullException` from `MBObjectManager` on a client load — the `CultureConversion`
   chain, still source-verified only.
6. Several in-game days, then Coop's `coop.debug.hero audit` / `coop.debug.settlements audit` to
   compare object sets between peers.

Owed by the 2026-08-03 work specifically, none of it run:

7. A dedicated-server boot against the mirrored `Win64_Shipping_Server` binaries **and** the fixed
   `action_sets.xml` — the two together are what a server needs to start with TAOM's simulation.
8. A two-client join where the joiner keeps their character-creation race, culture startup gold,
   career and special-resource seed. The line to look for is
   `[Possession] Controlled hero changed 'X' -> 'Y'`.
9. An emissary purchase attempted on a guest: it must decline, and no resources may be deducted.
10. A server log carrying the one-shot `[SpecRes] Dedicated server —` line, with the remote players
    earning on their own clients instead.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/bannerlord-together-compat.md](./bannerlord-together-compat.md)
- [docs/features/dread-aura.md](./dread-aura.md)
- [docs/features/hero-race.md](./hero-race.md)
- [docs/features/player-possession.md](./player-possession.md)
- [docs/features/player-switcher.md](./player-switcher.md)
- [docs/INDEX.md](../INDEX.md)
- [docs/migration/dr3-maintenance.md](../migration/dr3-maintenance.md)
- [docs/reference/doc-lookup.md](../reference/doc-lookup.md)
- [docs/reference/feature-map.md](../reference/feature-map.md)
- [docs/research/bannerlordcoop-internals.md](../research/bannerlordcoop-internals.md)

<!-- backlinks-end -->
