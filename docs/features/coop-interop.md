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
| `CultureConversionBehavior` | `OnDailyTick`, `OnGameLoaded` | Replaces notables via `HeroCreator.CreateNotable`. Client's store holds the same pending records — it loaded the host's save. (`OnSettlementOwnerChanged` only queues a pending timer, so it stays ungated.) |
| `RaceAgeBehavior` | `OnDailyTick` | Re-kills locally what the host already replicated. |
| `WarOfTheRingBehavior` | `OnDailyTick`, **`OnSessionLaunched`** | Both call the identical `CheckPhaseTransition` → `DeclareWar`. `OnSessionLaunchedEvent` fires on every peer, and a co-op join *is* a save-load, so an ungated client issued its own war set on connect. |
| `WarOfTheRingMomentumBehavior` | `OnDailyTick`, **`OnKingdomDestroyed`, `OnMapEventEnded`, `OnSiegeCompleted`, `OnRaidCompleted`, `OnArmyGathered`, `OnSessionLaunched`** | `MomentumWarState` rides TAOM `SyncData` that nothing replicates. `OnKingdomDestroyed` reached the same `CheckAndApplyVictory` → `EndWar`/`MakePeace` as the tick. Daily gate sits **after** `RefreshMapMeter` — local UI a client still needs drawn. |
| `MessengerCampaignBehavior` | `OnHourlyTick` | Arrival writes `MobileParty.MainParty.Position` — on a client, *its own* party. |
| `SiegeDefenseBehavior` | `OnHourlyTick` | `Clan.Influence` + global relation off `Hero.MainHero`, which differs per peer. **Known limitation below.** |
| `CastleRecruitmentBehavior` | `OnGameLoaded`, `OnNewGameCreated` | The one castle path a client reaches; creates heroes. |

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

## Open: client-reachable entry points the gate later blocks

Three findings from the 2026-08-01 Codex authority pass share one shape, and it is the **opposite**
of the divergence the gates were built for. The gate correctly stops a client mutating shared state
— but the *entry point* is still client-reachable, so a client can start something it can never
finish. Nobody desyncs; the client just quietly gets nothing.

| Area | What a client can do | What it never gets | Status |
|---|---|---|---|
| Messengers | Pay `MessengerGoldCost` and enqueue a `PendingMessenger` (send path ungated) | Hourly processing is authority-only, so it never advances or arrives — **gold is simply lost** | **Open** |
| Siege defence | Be prompted, accept, and be tracked | Reward tick is authority-only | Partly addressed by `_locallyClaimed`; the prompt/accept path still needs an owner check |
| Career quests | Be offered and accept | — (fixed: the dedup scan now filters by owner) | **Fixed 2026-08-01** |

The Messenger case is the sharpest because it costs the player real gold. `PendingMessenger` has no
owner field (`TargetHeroId`, `DispatchTimeDays`, `Position`, `Arrived`), so processing cannot tell
whose messenger it is. Two options, neither done: add owner identity and process only the local
peer's own entries client-side, or suppress the send UI on a client. **Do not simply ungate the
hourly body** — arrival calls `target.ChangeState(...)` and `EnterSettlementAction.ApplyForCharacterOnly(...)`,
which are shared-state mutations.

**Also open — `CareerQuest` construction on a client [P1].** `new CareerQuest` on player acceptance
is client-side `MBObjectBase` construction during a live campaign: Coop's `MBObjectBasePatches`
prefixes the `StringId` setter and returns false when the sync policy disallows the write, and
`QuestManager.OnQuestStarted` then adds the object anyway. It does not go through
`MBObjectManager.CreateObject<T>`, so the exact `TryGetValue(null, …)` chain is not proven for this
path — but "per-player, therefore safe" is not a sufficient defence, and this needs a live session
to characterise. The existing try/catch around acceptance is containment, not correctness.

## What is NOT done

- **No MCM settings parity.** 284 `SettingProperty` entries, mostly gameplay-affecting, per-user and
  outside the save; BannerlordCoop syncs none. Divergent settings defeat everything above.
- **No ModuleData content hash.** Coop's handshake compares module ids and version strings only, and
  disconnects on mismatch — so identical TAOM versions on all peers are mandatory, and a TAOM version
  bump is a co-op compatibility break.
- **No sync of TAOM's own campaign state.** Clients get the join-time baseline from the host's save,
  then never hear about WotR momentum or culture-conversion deltas.
- **Dedicated server unsupported.** The real obstacle is the runtime: the appliance runs
  `Win64_Shipping_Server` on `Microsoft.NETCore.App`, while TAOM targets .NET Framework 4.7.2 and
  tags itself `DedicatedServerType=none`. Its `engine/Modules` also ships no TAOM, and its
  `default_new_game.sav` bootstrap is a vanilla world. (The missing `CustomBattle` module is *not* a
  blocker — TAOM declares it as a `DependedModule` but does not need it on a server.) Coop's own
  DLLs are SHA-256 pinned, but that covers only the `Coop` module, so adding modules alongside is
  not in itself refused. Use the listen-host path, which carries TAOM into the spawned server
  process automatically.

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
