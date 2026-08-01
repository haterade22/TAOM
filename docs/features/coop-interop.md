# CoopInterop

TAOM's side of sharing one campaign with a third-party co-op mod without the two corrupting each
other's saves.

**Status (2026-08-01): interop layer shipped, end-to-end co-op UNVERIFIED.** Nothing below has run in
a live two-peer session. Do not tell players co-op works until the checklist at the end is walked.

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
| `SaveDefinerCollisionDetector` / `Guard` / `SaveDefinerRecord` | Base-id preflight and crash attribution. |
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

## Layer 3 — host authority (session-varying)

**BannerlordCoop does not stop a client ticking the campaign.** Its `PartyTickPatch` blocks only the
per-entity tickers, so `DailyTickSettlementEvent` and friends never reach a client — but the **global**
`DailyTickEvent` / `HourlyTickEvent` fire on both peers. Coop's own countermeasure is a hand-written
allowlist of ~127 `RegisterEvents` prefixes over *named vanilla* types; there is no generic hook a
third-party mod can register into, so TAOM authors its own.

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

**Known limitation — siege-defence rewards are host-only.** `SiegeDefenseBehavior.OnHourlyTick` is
gated wholesale, and `GrantReward()` is reachable *only* through it. So a co-op client who accepts a
siege-defence event never receives the influence/relation reward. This is deliberate but
inconsistent with how `CareerQuestCampaignBehavior` treats the same `Hero.MainHero`-keyed shape
(left ungated so each player gets their own). The clean fix is to split the shared timer tick from
the per-player reward and gate only the former; until then clients silently lose the reward.
Raised by the 2026-08-01 deep review.

### Deliberately NOT gated

`CareerQuestCampaignBehavior` (daily) — keyed entirely on `Hero.MainHero`, a legitimately *different*
hero per peer. Gating it would strip career quests from clients. Its only object creation
(`new CareerQuest`) happens on player acceptance and is already try/catch-wrapped.
**Owed: in-game confirmation that quest start does not trip the `StringId` suppression below.**

## Client-side object creation

[Source-verified, runtime-unconfirmed] Constructing any `MBObjectBase` on a client inside
`CampaignState` throws: `MBObjectBasePatches` prefixes the `StringId` **setter** and returns `false`
when the sync policy disallows the write, so `CreateObject<T>` leaves `StringId` null and
`MBObjectManager.cs:215` calls `Dictionary<string,T>.TryGetValue(null, …)` → `ArgumentNullException`.
Full chain in the internals doc. **Not TAOM-specific** — any mod creating an `MBObjectBase` on a
client hits it; TAOM merely had two paths in, both now gated.

## What is NOT done

- **No MCM settings parity.** 284 `SettingProperty` entries, mostly gameplay-affecting, per-user and
  outside the save; BannerlordCoop syncs none. Divergent settings defeat everything above.
- **No ModuleData content hash.** Coop's handshake compares module ids and version strings only, and
  disconnects on mismatch — so identical TAOM versions on all peers are mandatory, and a TAOM version
  bump is a co-op compatibility break.
- **No sync of TAOM's own campaign state.** Clients get the join-time baseline from the host's save,
  then never hear about WotR momentum or culture-conversion deltas.
- **Dedicated server unsupported** — its module tree lacks `CustomBattle` (a TAOM dependency) and
  TAOM itself, and its Coop DLLs are SHA-256 pinned (exit code 4 if modified). Use the listen-host
  path, which carries TAOM into the spawned server process automatically.

## Verification checklist (none of this has been run)

1. Launch TAOM + Coop; confirm `[CoopPresence] EnsureProbed: co-op module(s) ACTIVE: Coop` in
   `Modules/TAOM.Dependencies/diag.log`.
2. Confirm a `[HarmonyCensus]` block naming `Bannerlord.Coop`; check its `TRANSPILER CONFLICT` rows
   against TAOM's 7 transpilers.
3. Confirm TAOM behaves identically **solo** with the Coop module merely enabled — what the
   `IsAuthority` fail-open direction protects.
4. Host; confirm the spawned `/server` process starts TAOM cleanly (no blocking UI).
5. Join as client; confirm no `ArgumentNullException` from `MBObjectManager` on load.
6. Run several in-game days; use Coop's `coop.debug.hero audit` / `coop.debug.settlements audit` to
   compare object sets between peers.
