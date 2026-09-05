# BannerlordCoop internals (for TAOM interop)

> **Purpose:** the durable record of a full decompilation + analysis of BannerlordCoop, so no future
> session has to decompile it or re-run the analysis. Read this before touching anything in
> `Main/Features/CoopInterop/` or `Dependencies/Foundation/Coop*`.
>
> **Subject:** Steam Workshop `3770450698` ("Bannerlord Coop", Joke's Workshop) — module id **`Coop`**,
> v0.0.3, game v1.4.7, upstream `Bannerlord-Coop-Team/BannerlordCoop` commit `52e5ed70`
> (`DedicatedServer/release-info.txt`).
>
> **NOT the same mod as BannerlordTogether.** BT is a separate project with its own architecture and
> an explicit no-decompile policy which TAOM continues to honour —
> see [bannerlord-together-compat.md](../features/bannerlord-together-compat.md). Nothing in this
> document was derived from BT.

## Provenance and confidence

Decompiled with `ilspycmd` 10.0.1 against the installed client assemblies (6 DLLs → 3,270 `.cs`
files), then analysed across 8 dimensions with adversarial verification of every load-bearing claim:
133 findings, 30 verified, **15 held / 26 refuted**. Raw evidence is committed under
[`docs/raw/bannerlordcoop/`](../raw/bannerlordcoop/) (`dimension-reports.json`,
`verifier-verdicts.json`).

**Everything below marked [Verified] was read directly in source this session.** Claims that are
inference or that depend on runtime behaviour are marked explicitly. Two synthesis claims were found
wrong on direct re-check and are corrected here — see [Corrections](#corrections-to-the-analysis).

## Architecture

| Assembly | Role |
|---|---|
| `Coop.dll` | `Coop.CoopMod : NoHarmonyLoader : MBSubModuleBase` — entry point |
| `Coop.Core.dll` | Client/server state machines, per-system services, sync policies |
| `GameInterface.dll` | Object manager, policies, the AutoSync engine, all explicit game patches |
| `Missions.dll` | Battle/mission sync |
| `Common.dll` | Messaging, network, serialization, audit, `ModInformation` |
| `Coop.Steam.dll` | Steam transport |

Bundled third-party: 0Harmony **2.4.2.0**, Autofac, LiteNetLib, protobuf-net, Serilog, Newtonsoft,
Mono.Cecil, MonoMod, Microsoft.CodeAnalysis (Roslyn) + Scriban — the last two because **AutoSync
generates and compiles sync code at runtime**.

### AutoSync

Coop's bulk sync layer. Harmony **transpilers** rewrite every `stfld` into an intercept call across
all declared methods *and constructors* of 43 campaign types (`MobileParty`, `Hero`, `Clan`,
`Kingdom`, `Settlement`, `Town`, `Village`, `CharacterObject`, `BasicCultureObject`, `MBObjectBase`,
…). The mod ships its generated output in plaintext at
`Modules/Coop/bin/Win64_Shipping_Server/AutoSyncExport/` (893 `.cs` files) — read that before
decompiling anything.

## The tick model — the single most important fact

[Verified] `Campaign.Tick()` carries only a Postfix; nothing suppresses it. But the events split:

**Client-blocked.** `GameInterface.Services.MobileParties.Patches/PartyTickPatch.cs:11-31` puts
`return ModInformation.IsServer` prefixes on `CampaignPeriodicEventManager.TickPeriodicEvents`,
`CampaignPeriodicEventManager.MobilePartyHourlyTick`, and `MobileParty.HourlyTick`.
`TickPeriodicEvents` is the sole driver of every per-entity ticker, so these **never fire on a
client**:

`DailyTickPartyEvent` · `DailyTickSettlementEvent` · `DailyTickTownEvent` · `DailyTickHeroEvent` ·
`DailyTickClanEvent` · `HourlyTickPartyEvent` · `HourlyTickSettlementEvent` ·
`HourlyTickClanEvent` · `QuarterDailyPartyTick`

**Client-live.** `OnTick` → `SignalPeriodicEvents` is unpatched, so the *global*
`DailyTickEvent`, `HourlyTickEvent`, `WeeklyTickEvent`, `QuarterHourlyTickEvent` **do** fire, off a
locally-advancing clock slewed toward the server (250 ms `CampaignTimePacket` heartbeats).

⇒ **A TAOM behaviour on a per-entity tick needs no co-op work. One on a global tick does.**

Coop's own countermeasure is a hand-curated allowlist of `[HarmonyPatch("RegisterEvents")]` prefixes
over ~127 **named vanilla** behaviour types (canonical shape:
`GameInterface.Services.Settlements.Patches.Disable/DisableMilitiasCampaignBehavior.cs:6-14`). There
is no generic `CampaignBehaviorBase` gate and no third-party registration hook — a mod must author
its own equivalent.

## Authority and policy

| Signal | Type | Notes |
|---|---|---|
| Am I the host? | `Common.ModInformation.IsServer` / `.IsClient` | **Traps below.** |
| Is a session live? | `GameInterface.ContainerProvider.TryGetContainer(out ILifetimeScope)` | Also the service locator (`TryResolve<T>`). |
| May I mutate now? | `GameInterface.Policies.CallOriginalPolicy.IsOriginalAllowed()` | The predicate every AutoSync intercept consults. |
| Escape hatch | `Common.Util.AllowedThread` | `[ThreadStatic]`; wrap network-driven applies. |
| Do I own this hero? | `HeroExtensions.IsControlledByThisInstance()` / `.IsPlayerHero()` | Only public per-entity ownership query. |

### Traps [Verified]

- **`ContainerProvider.Alive` is permanently `false`.** It is
  `public static bool Alive { get; } = _lifetimeScope != null;` — an inline static initialiser
  evaluated while the field is still null. **Never bind it.** Use `TryGetContainer`.
- **`ModInformation.IsServer` defaults `false`, `IsClient => !IsServer`, and is sticky** (never reset
  on session teardown). So `IsClient` reads `true` in plain singleplayer whenever the Coop module is
  merely enabled, and `false` forever after you host once. **Always AND it with a live-session
  probe; never cache the result.**
- **`CallOriginalPolicy.IsOriginalAllowed()` fails open.** If `ISyncPolicy` does not resolve it logs
  an error and returns `true`. It is also not hot-path safe — an Autofac `TryResolve` per call, and
  the failure branch captures `Environment.StackTrace`.

`ClientSyncPolicy.AllowOriginal()` = `!{CampaignState, MissionState}.Contains(state)`;
`ServerSyncPolicy.AllowOriginal()` = `!(state is ServerRunningState)`.

## Client-side object creation crashes

[Verified in source; runtime-unconfirmed] Creating any `MBObjectBase` on a client inside
`CampaignState` throws. The chain:

1. `GameInterface.Registry.Patches/MBObjectBasePatches.cs` prefixes the `MBObjectBase.StringId`
   **setter**, returning `false` unless `IsOriginalAllowed()` or the type is `MenuContext`.
2. On a client in `CampaignState`, `IsOriginalAllowed()` is false (above).
3. `MBObjectManager.CreateObject<T>(string)` is `new T { StringId = stringId }; RegisterObject(val);`
   — the setter is suppressed, so `StringId` stays **null**.
4. `MBObjectManager.cs:215` (`ObjectTypeRecord<T>.RegisterObject`) calls
   `_registeredObjects.TryGetValue(obj.StringId, …)` on a `Dictionary<string,T>` with **no null
   guard** (contrast `:193`, which guards with `!string.IsNullOrEmpty`) ⇒ `ArgumentNullException`.

Engine path into it: `HeroCreator.CreateNotable` → `CreateHero(useCharacterAsTemplate: true)` →
`CharacterObject.CreateFrom` → `MBObjectManager.CreateObject<CharacterObject>()`.

**Not TAOM-specific** — any mod constructing an `MBObjectBase` on a client hits this. The only
unverified link is runtime: whether Harmony's prefix on a small property setter reliably applies
(inline-prone). Gate regardless — on a client the alternative outcome is a divergent hero the host
never created.

## Object identity and the join baseline

[Verified] Network identity is the string `"{TypeName}_{StringId}"`, built independently on each
peer by walking the live object graph after the campaign is ready — **not** MBGUID, **not**
load-order index. Modded XML content therefore resolves on both peers with zero Coop-side
registration.

[Verified] A joining client is seeded with the host's **real, full engine save**:
`SaveInterface.cs:30` runs `OnBeforeSave()` then `Game.Current.Save(..., "TransferSave",
coopInMemSaveDriver, ...)`; the client applies it at `GameStateInterface.cs:99-114` via
`SaveManager.Load("", driver, loadAsLateInitialize: true)` → `new SandBoxGameManager(loadResult)` —
the same call vanilla's `MBSaveLoad.LoadSaveGameData` makes. Every `[SaveableProperty]` and
`CampaignBehaviorBase.SyncData` round-trips.

⇒ **Mod state that lives in the save needs no wire protocol.** Only ongoing deltas do.

Side effect: this path **bypasses** `MBSaveLoad.LoadSaveGameData` and `SandBoxSaveHelper.TryLoadSave`,
so TAOM's `SaveLoadDiagnostics` patches on those two never fire on a co-op join; only
`SaveManager_Load_Patch` covers it.

Transfer is raw Deflate (`CompressionLevel.Fastest`) over the whole `.sav`, chunked at 64 KiB via
`network.SendImmediate` in an unpaced loop. LZ4 is used only for mission movement packets.

## Harmony

[Verified] Four owner ids: **`Bannerlord.Coop`** (`GameInterfaceModule.HarmonyId` — the main one,
carrying all explicit patches plus the AutoSync engine), `Coop.UILoading` (`CoopMod.cs:97`),
`Coop.BootFix` (`BootPatches.cs:58`), `CoopAutoRegistryFactory` (`AutoRegistryFactory.cs:18` —
declared but never used to patch).

- Ships 0Harmony **2.4.2.0**, byte-identical in version and commit hash to TAOM's vendored copy ⇒
  one assembly loads, one global patch registry, so cross-owner `GetAllPatchedMethods` / `Unpatch`
  work across the boundary.
- **Never unpatches foreign owners**, never asserts exclusivity; its only `Unpatch` targets its own
  patch-method handles.
- `GameInterface.UnpatchAll()` has an **empty body** — Coop's patches persist for the process
  lifetime, including after leaving a session.
- `GameInterface.Utils.FragileDetourGuard` prefixes `HarmonyLib.PatchTools.DetourMethod`
  **process-globally** and skips detours on methods it deems fragile that carry no
  prefix/postfix/finalizer — i.e. exactly the profile of a transpiler-only patch on a small method.
  Whether this drops any third-party transpiler needs a runtime check.

**Coop does not perturb singleplayer.** `GameInterface.PatchAll()` is reached only from
`MainMenuState.Handle_NetworkConnected` and the host start path. The only always-live code is
`BootPatches` (3 font/UI patches applied in `CoopMod`'s *constructor*, before any `OnSubModuleLoad`)
plus the `UILoadingPatches` category. `CoopMod`'s ctor also sets `MBDebug.DisableLogging = false`.

## GameModels and campaign behaviours

[Verified] Coop registers **zero** GameModels and removes zero. Its NoHarmony
`AddModel`/`ReplaceModel`/`RemoveModel`/`ReplaceBehavior` machinery exists but is never called;
`CoopMod.NoHarmonyLoad()` queues no model tasks; the only `CampaignGameStarter` interaction is
`AddBehavior` of two Coop behaviours. Nothing type-scans `gameStarter.Models` or
`CampaignBehaviorManager`. **Third-party GameModel overrides and campaign behaviours survive
untouched, and load order is irrelevant to this.**

Coop instead Harmony-patches 15 concrete `Default*Model` types and ~150 vanilla behaviours by
hardcoded `typeof(...)`.

**Vanilla behaviours Coop kills unconditionally on both peers** (not just clients):
`AgingCampaignBehavior`, `PregnancyCampaignBehavior`, `IssuesCampaignBehavior`,
`MarriageOfferCampaignBehavior`, `HeirSelectionCampaignBehavior`, `EducationCampaignBehavior`,
`NPCEquipmentsCampaignBehavior`, `PrisonerRecruitCampaignBehavior`, `RetirementCampaignBehavior`,
plus ~40 quest behaviours. Anything downstream of those goes inert.

## Missions and combat

[Verified] Distributed authority: each peer owns the agents it spawned; every other peer holds a
`Controller = AgentControllerType.None` puppet driven by position/action packets
(`Missions.Battles/PuppetSpawner.cs:173`).

**Damage is not recomputed.** The attacking peer resolves the blow with its own GameModels and
`MBRandom` rolls and ships the finished `Blow` + `AttackCollisionData`, replayed via
`Agent.RegisterBlow`. ⇒ per-blow RNG in a mod's combat model is *not* a divergence source.

Puppet spawn resolves `CharacterObject` by StringId and defaults
`AgentMonster = FaceGen.GetBaseMonsterFromRace(AgentRace)` ⇒ custom races, monsters, skeletons and
creature mounts come through.

**Coop's attacker-ownership gates in `Missions` are dead code.** `AgentDamagePatch`,
`MeleeHitCallbackPatch`, `ChargeDamageCallbackPatch`, `RegisterBlowPatch` carry no
`[HarmonyPatchCategory]`, and `GameInterface.cs:47-54` runs `PatchAllUncategorized(assembly)` where
`assembly` is **GameInterface.dll only**. `Missions` is patched solely via the registered categories
`CoopMissilePatches` and `CoopAgentVoicePatches` plus `TournamentCombatPatchInstaller.Install`. The
only live blow gate in field/siege battles is `BattleBlowInterceptPatch`, which gates on the
**victim**.

Other constraints: `BattleBlowInterceptPatch.cs:46` routes `!IsHuman` agents through the
mount-authority branch (keep walking combatants `IsHumanoid="true"`);
`CoopTournamentCampaignBehavior.cs:52` is an **exact-type** check on `FightTournamentGame` (a
subclass silently degrades co-op tournaments); `TournamentNativeBracketHydrator.cs:44` throws NRE on
an unresolved `CharacterObject`.

## Extension points (undocumented but load-bearing)

There is **no documented third-party API** — "plugin", "extension point", "third party", "public
api", "IExtension", "RegisterExternal" return zero hits across all 3,266 files. Three usable
mechanisms exist:

1. **Namespace-prefix DI scanning.** `CommonModule.RegisterAllTypesWithInterface<TModule,TInterface>`
   resolves `typeof(TModule).Namespace` and passes it to `InterfaceCollector.GetInterfaces<T>`,
   which walks `AppDomain.CurrentDomain.GetAssemblies()` and admits any concrete, non-generic,
   non-abstract type whose `Namespace.StartsWith(prefix)`. Prefixes: `Coop.Core.Client`,
   `Coop.Core.Server` (side-specific), and `GameInterface` (`ServiceModule.cs:20`, **no assembly
   filter**), registered `AsSelf().InstancePerLifetimeScope().AutoActivate()`.
2. **Protobuf auto-registration.** `SerializableTypeMapper.CollectProtoContracts()` scans every
   non-dynamic loaded assembly for `[ProtoContract]`; wire id = FNV-1a of `Type.FullName` masked to
   31 bits. `CoopNetworkBase.SendAll(IMessage)` has no type allowlist.
3. **`GameInterface.Registry.Auto.AutoRegistryBase<T>`** — `RegistryModule.cs:27` scans the AppDomain
   with **no namespace filter**, so this is the one hook needing no namespace squatting.

### Costs and hazards of using them

- Implementing `IHandler` requires a hard reference to `Common.dll` ⇒ must live in a **separate
  optional satellite assembly**, not the main mod DLL, which must be **force-loaded** before session
  start (`GetAssemblies()` only sees loaded assemblies).
- `AutoActivate` construction happens inside `containerBuilder.Build()`, which is **not** wrapped in
  try/catch (`CoopartiveMultiplayerExperience.cs:379`, `:513`) — **a throwing ctor kills the entire
  Coop session.**
- `ReflectionExtensions` swallows `ReflectionTypeLoadException` per assembly ⇒ a version-mismatched
  glue DLL vanishes **silently**. Assert at startup that handlers actually instantiated.
- `MessageBroker` subscriptions are `WeakDelegate` — hold a strong reference to the subscriber.
- `SerializableTypeMapper` **throws from its constructor** on an FNV-1a `FullName` collision, failing
  the container build for every peer. Never add a `[ProtoContract]` type to the main mod assembly.

### Hard limit: AutoSync cannot cover third-party types

`AutoSyncModule.GetAutoSyncClasses()` scans `GetType().Assembly` only, and `AutoSyncBuilder.Build()`
assembles Roslyn metadata references from GameInterface + BCL + GameInterface's *referenced*
assemblies, emitting **unqualified** type names. A third-party DLL is never a reference ⇒ generated
code would not compile. `AutoSyncRegistry.AddField/AddProperty` **is** usable for fields on *vanilla*
types Coop misses (TaleWorlds assemblies are referenced), but only once per process before the first
`PatchAll` (`AutoSyncPatcher.Assembly` is a static one-shot cache).

The lone crack: `AutoSyncBuilder` contains `asm.FullName.Contains("AutoSyncAsm")`, the only way a
third-party assembly could become a metadata reference. No other reference to that string exists.
**Ask the authors before building on it.**

## Packaging, validation, load order

[Verified] **Module validation is exact-match and content-blind.** On connect the client sends its
full active module list (`ValidateModuleState.cs:61`); the server runs `ModuleValidator.Validate`
(`ResolveCharacterState.cs:68`) and **disconnects on failure** (`:97-100`). After filtering official
non-DLC modules and ids prefixed `DedicatedServer.` (`ModuleValidator.cs:45-56`), every remaining
module's **Id and `ApplicationVersion` (with `checkChangeSet: true`) must match**; any *extra* client
module is rejected; any `OfficialOptional` DLC on a client is rejected (War Sails must be off).

**There is no content hashing anywhere** — `ModuleData` returns zero hits tree-wide. Two peers on the
same declared version with divergent XML pass validation and then diverge. Failure is soft
(`ObjectManager.cs:216` logs an error) except in tournaments, which throw.

**The in-game host spawns a second full game process.** `CoopartiveMultiplayerExperience.cs:153-159`
— on `AttemptHost`, Coop calls `serverProcessManager.Start(...)` then joins `127.0.0.1` as a client.
`ManagedServerLauncher` resolves `Bannerlord.exe` from the current process directory and takes
`ModuleHelper.GetActiveModules()` from the *live client*; `ServerLaunchArguments.cs:44-59` builds
`/singleplayer /server _MODULES_*…*_MODULES_ /coopsave <name> /coopowner <pid>`. So the authoritative
server inherits the client's mods automatically — but the mod is loaded **twice on the host machine**
as two independent processes, and the server instance boots headless-ish with no user interaction.
**Startup must not block on rendered UI or input.**

**`StoryMode`:** Coop force-enables it in its launcher line, and `CoopLoadUI.cs:57` filters *out* any
save whose metadata lists StoryMode from the host picker. Coop itself has zero StoryMode coupling, so
dropping StoryMode from the module line is safe and is the fix when saves do not appear. Bypass:
`GameStateInterface.cs:142` `ForceLoadSave` uses an unfiltered `GetSaveFiles()`.

**TAOM-side load order.** `TAOM.Dependencies` must construct before the vanilla stack (`Native`
included), because its static constructor installs the `AssemblyResolve` redirect every later mod
resolves BUTR assemblies through; `Coop` goes last so its own bundled `0Harmony` collapses onto that
same instance. Full order + rationale: `docs/features/coop-interop.md` "Load order".

**`Campaign.TimeControlMode` is unassignable** — two prefixes return `false`; menu/map writes are
retargeted to an empty method; vanilla's active-state pause is transpiled out of
`MapState.OnMapModeTick`.

## Save definers

[Verified] Coop ships exactly two `SaveableTypeDefiner`s, base ids **44177000** and **44182000** —
~682 million clear of TAOM's `726900501/601/701/801`. **No collision.**

## Diagnostics worth adopting

`Common.Audit.IAuditor` (`HeroAuditor`, `MobilePartyAuditor`, `SettlementAuditor`) backs console
commands `coop.debug.hero audit`, `coop.debug.settlements audit` — a client-invoked round-trip that
reports count and id-registration mismatches against the host. That is exactly the assertion that
catches a client creating objects it should not have. It checks registration, not field values.

## Corrections to the analysis

Two claims from the automated synthesis were wrong on direct re-check. Recorded so they are not
re-inherited:

1. **"Six assemblies collide in the resolve list."** Measured with
   `[Reflection.AssemblyName]::GetAssemblyName` over both bin folders: only **five** both ship and
   differ (`Serilog`, `System.Buffers`, `System.Memory`, `System.Numerics.Vectors`,
   `System.Runtime.CompilerServices.Unsafe`). `System.Threading.Tasks.Extensions` is Coop-only, so
   the redirect resolves harmlessly.
2. **"`System.Collections.Immutable` is in TAOM's redirect array."** It is not — verified by reading
   `Dependencies/SubModule.cs:39-69`.

An earlier analyst pass also claimed all ~31 TAOM behaviours double-run on a client. That is wrong;
see [the tick model](#the-tick-model--the-single-most-important-fact). Only global-tick subscriptions
are affected.

## Open questions (need runtime, not source)

1. Does one Harmony registry actually span both mods at runtime? (Byte-identical 0Harmony makes it
   expected; every PatchShield mitigation depends on it.)
2. ~~What does PatchShield cost when applied over thousands of AutoSync-patched methods?~~
   **ANSWERED 2026-08-02 by a player's profiling capture: enough to collapse frame rate.** Coop's
   `PatchAll` runs on connect, before TAOM's `OnGameInitializationFinished` pass, so PatchShield
   shielded the entire AutoSync surface — every declared method of 43 campaign types, each then
   paying the `__originalMethod` binding tax per call. Same mechanism as the #331 tournament freeze,
   on the campaign hot path instead of a teardown. TAOM now skips `PatchShield.Install()` whenever a
   co-op module is active (`PatchShieldPolicy.ShouldInstall`); see
   `docs/features/coop-interop.md` "PatchShield is skipped under co-op".
3. Does `FragileDetourGuard` silently drop transpiler-only patches?
4. Does the native engine fire `Mission.MeleeHitCallback` for a `Controller=None` puppet? If yes,
   *vanilla* melee already double-applies under Coop.
5. Does the mod start cleanly in the spawned `/server` process?
6. Practical join time for a large total-conversion save.
7. Is `ITimeControlInterface` declared `public`? (Only the `internal` concrete class was read.)
8. Is `asm.FullName.Contains("AutoSyncAsm")` a deliberate seam? → ask the authors.

## See also

- [`docs/features/coop-interop.md`](../features/coop-interop.md) — what TAOM does about all this
- [`docs/features/bannerlord-together-compat.md`](../features/bannerlord-together-compat.md) — the
  *other* co-op mod; no-decompile policy applies there
- [`docs/raw/bannerlordcoop/`](../raw/bannerlordcoop/) — raw dimension reports + verifier verdicts

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/coop-interop.md](../features/coop-interop.md)
- [docs/modding/module-dependencies.md](../modding/module-dependencies.md)
- [docs/reference/provenance-register.md](../reference/provenance-register.md)

<!-- backlinks-end -->
