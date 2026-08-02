# BannerlordTogether Compatibility

## Overview

[BannerlordTogether (BT)](https://www.nexusmods.com/mountandblade2bannerlord/mods/10426) is a
third-party host-authoritative co-op mod: one player's game owns the campaign simulation and
replicates towns, villages, AI parties, battles and diplomacy to the other. TAOM does **not** bundle
or extend it. This doc records what TAOM does on its own side so a TAOM + BT session can boot,
survive, and tell players when their two installs have drifted apart.

**Status: TAOM-side interop layer shipped 2026-07-31; end-to-end co-op unverified.** The boot matrix
below has not been run against BT a0.5.3.2. Do not tell players co-op works until it has.

## Requirements

| Requirement | Details |
|-------------|---------|
| Bannerlord version | **v1.4.7** — TAOM's pin, and BT a0.5.3.2's declared dependency version for Native/SandBoxCore/Sandbox/StoryMode/CustomBattle |
| BannerlordTogether | a0.5.3.2 (the reviewed build) |
| TAOM version | **Same version on every client — not enforced.** The `TAOM_Build` stamp in save metadata records which build wrote a save and is logged on load, which makes a mismatch diagnosable after the fact, not preventable |
| All players | Must have TAOM installed and enabled, with matching MCM settings |

**StoryMode must be enabled.** BT hard-depends on it; TAOM does not list it. TAOM's `Xmls` block
covers `CampaignStoryMode`, so data merges, but the vanilla main-storyline behaviors reference
vanilla ids that TAOM's XSLT has renamed. This is unaudited — watch for it in testing.

**BattleLinkMPClient stays disabled** in the singleplayer mod list, per BT's own instructions.
Enable it only for a shared battle in the separate multiplayer window.

## Load order

```
TAOM.Dependencies → BUTR stubs → Native → SandBoxCore → Sandbox → StoryMode → CustomBattle
                  → TAOM → BannerlordTogether
```

Pinned in the manifests, not left to the launcher's alphabetical default:

- `Dependencies/_Module/SubModule.xml` lists `BannerlordTogether` and `BattleLinkMPClient` in
  `<ModulesToLoadAfterThis>`.
- `Main/_Module/SubModule.xml` carries BOTH the BUTR-launcher mirror
  (`<DependedModuleMetadata id="BannerlordTogether" order="LoadAfterThis" optional="true"/>`) AND the
  engine-honoured `<ModulesToLoadAfterThis>` entry. The metadata block alone establishes nothing for
  a vanilla-launcher user — `ModuleInfo.LoadWithFullPath` has no branch for it. Neither is a
  `<DependedModule>`, so TAOM stays launchable without BT installed. (Corrected 2026-08-01; this
  bullet previously said "metadata only", which stopped being true when the pin was added.)

The sharp reason, beyond patch ordering: **BT ships its own `0Harmony.dll` at the same version TAOM
deploys (2.4.2.0), and HarmonyLib's patch registry is per-assembly-instance static state.** If BT's
copy wins the AppDomain slot, `Harmony.GetAllPatchedMethods()` called from TAOM's copy cannot see
BT's patches at all — which blinds both PatchShield and the census. Constructing TAOM.Dependencies
first lets its `AssemblyResolve` redirect collapse the two onto one instance.

## What TAOM changes when a co-op module is active

`CoopPresence` (`Dependencies/Foundation/CoopPresence.cs`) reads the launcher's active-module list
and matches it against the ids in `Dependencies/_Module/coop-modules.txt`. That file is shipped and
user-editable; parsing is **union-only**, so a bad edit can add ids but never remove a compiled
default. Detection fails closed — when the module list can't be read, TAOM behaves exactly as it
does today.

| Behaviour | Solo | Co-op module active |
|---|---|---|
| `PatchShield` unpatching a non-allowlisted Harmony owner after a `MissingMethod`/`MissingField`/`TypeLoad` | Strips the owner's patches (the rescue path) | **Withheld** — logs `would unpatch owner 'X' … (withheld)` instead |
| `PatchShield` swallowing those exceptions | Swallows | Swallows (unchanged) |
| `SaveShield` on the `SAVE-LOAD` chain | Swallows | **Rethrows**, and still records the failure |
| `SaveShield` on the `MISSION-INIT` chain | Swallows | Swallows (unchanged) |
| Harmony census | Not written | Written at the end of `OnGameInitializationFinished` |
| TAOM diplomacy veto (war / peace / alliance-end) | Enforced | **Off** — host is authoritative (see "Diplomacy ordering") |
| TAOM time-acceleration UI (`MapBar` extra fast-forward + 4 layout patches + `MapTimeControlVM` mixin) | Registered | **Not registered** — BT owns `Campaign.TimeControlMode` |

**Why unpatching inverts.** In singleplayer, stripping a broken third-party patch converts a crash
into a survivable degradation. Under host-authoritative co-op it does something worse: removing one
peer's copy of a sync patch produces no crash at all, it silently desynchronises two campaigns — and
a desync corrupts both saves undiagnosably, while a crash is visible and recoverable. The swallow
half of the shield, which is what actually keeps the session alive, is untouched.

**Why the save-load swallow inverts.** Swallowing inside `SaveManager.Load` /
`LoadResult.InitializeObjects` leaves a partially deserialised campaign that keeps running and looks
fine. A host then replicates that half-built state as authoritative. A loud load failure is far
cheaper than two silently-corrupted saves. `MISSION-INIT` keeps swallowing because that fault is
local — the cost is one broken battle, not a corrupted campaign.

Both are still overridden by the blunt flag files in `<game>/Modules/TAOM.Dependencies/`:
`patchshield-disabled.flag` (skips the shield install entirely) and
`saveshield-swallow-disabled.flag` (rethrow everything, read live per call).

## The Harmony census — our substitute for decompiling

Written to the TAOM log as `[HarmonyCensus]` lines when a co-op module is active. It reads
HarmonyLib's own public runtime registry (`GetAllPatchedMethods`, `GetPatchInfo`, `Patch.owner`) plus
reflection metadata — never a method body, never IL, never Cecil. A unit test pins the census *model*
so it cannot grow an IL- or body-bearing field; the writer itself is not covered by a test, so the
boundary in `HarmonyCensusWriter` is held by review, not mechanically.

It reports:

- every Harmony owner in the process, its patch count, and a rollup of the namespaces it targets —
  which yields BT's Harmony id and its real patch count (the "~129" figure in this doc's earlier
  revision was never sourced);
- **contested** methods patched by more than one distinct owner where at least one isn't TAOM;
- **TRANSPILER CONFLICT** rows where two owners both rewrite the same method's IL — the highest-risk
  Harmony conflict class, and TAOM brings 7 transpilers of its own;
- how many `0Harmony` assemblies are loaded, with paths and versions.

That last line is the one to read first. **If BT's owner id never appears while the game plainly
runs its patches, there are two Harmony instances and every other line is unreliable.**

## Save-definer collision preflight

The engine instantiates every `SaveableTypeDefiner` in every loaded assembly and registers each into
a dictionary keyed by save id; a duplicate throws during `Module.Initialize` with a message naming
neither mod. TAOM is unusually exposed: `FormationPresetSaveableTypeDefiner` deliberately reuses an
upstream mod's base id (726900601) so existing CompanionTactics saves import, which makes "enable
the donor mod alongside TAOM" a guaranteed, unattributable startup crash.

`SaveDefinerCollisionGuard` runs at `OnSubModuleLoad` (`Main/SubModule.cs`, immediately after
`IoC.Configure()` — `OnBeforeInitialModuleScreenSetAsRoot` fires from `OnApplicationTick`, long
after the engine's throw), groups every definer it finds by base id, and warns before the engine
hits the same constructors. It never repairs anything; it makes the crash attributable.

**It is a heuristic, and the wording says so.** The engine keys on `_saveBaseId + saveId`
(`SaveableTypeDefiner.AddClassDefinition`, v1.4.7), not on the base — so two definers can share a
base id and never collide when their per-type offsets differ. Vanilla does exactly that:
`SaveableCoreTypeDefiner` (TaleWorlds.Core) and `SaveableObjectSystemTypeDefiner`
(TaleWorlds.ObjectSystem) both use 10000, in a game that starts fine. Until 2026-08-01 the guard
reported that pair as a fatal cross-mod collision and told players to *"Disable one of them"* — at
the top of every collected user log. Now:

- groups made up entirely of game-shipped assemblies are **dropped** (nothing a player can act on);
- surviving groups log a **WARNING** saying the shared range *may* collide and naming the first two
  things to try disabling — not an ERROR asserting the game *will* fail.

Reading the true ids would mean invoking each definer's `Define*` virtuals against a synthetic
`DefinitionContext`, i.e. running arbitrary third-party code speculatively at startup against
engine internals that drift per version. Deliberately not done. RCA:
[`../reviews/rca-savedefiner-false-positive-2026-08-01.md`](../reviews/rca-savedefiner-false-positive-2026-08-01.md).

TAOM's own base ids: 726900501 (EquipPresets), 726900601 (CompanionTactics), 726900701
(CareerSystem), 726900801 (LotrIssues). Next free by the +100 convention: 726900901.

## Determinism

`MBRandom`'s default stream is `Game.Current.RandomGenerator`, which is state on the saved `Game`
root — so two peers starting from the host's save start with the same stream, and any draw one
machine makes alone advances only its copy. The engine ships a separate
`MBRandom.NondeterministicRandomFloat`/`Int` for values that must not touch it.

TAOM's two presentation-only draws were moved onto it (2026-07-31): the character-tableau mount mesh
key in `HeroRace/CharacterSpawnerService.cs` and the trample animation-clip variant in
`ElephantLike/BehaviorTreeElements/ElephantLikeAttackTasks.cs`. Correct on its own merits in
singleplayer — cosmetic code should never have been spending campaign RNG. The elephant *damage*
roll stays on the normal stream.

Not yet addressed, and honest about it: TAOM has six unseeded `new System.Random()` sites driving
campaign decisions (recruitment pools, settlement guards, initial children, marketplace injection),
and the creature behavior-tree cooldowns run on `DateTime.Now` rather than mission time. Whether any
of it matters depends on whether BT suppresses the client's campaign tick — which the boot matrix
and a two-peer session will tell us, and nothing else will.

## Time control — BT owns the clock

BT's `TimeControlModePatch` prefixes the `Campaign.TimeControlMode` **setter** and overwrites the
assigned value outright whenever a co-op session is active (host: `UnstoppablePlay` /
`UnstoppableFastForward` / `Stop`; hideouts and pending BattleLink force `Stop`). TAOM's
`TimeControlAdapter` writes the same property.

BT winning is correct — a host-authoritative mod must own campaign time. The problem was purely that
TAOM kept *advertising* control it no longer had: the `MapBar` extra fast-forward button still
rendered and still accepted clicks while doing nothing.

**Resolution (2026-08-01):** the five `TimeAcceleration` prefab extensions and the
`MapTimeControlVM` mixin carry `[CoopSuppressedUi]`, and `SubModule.RegisterUiExtensions` filters
them out of the UIExtenderEx registration when `CoopPresence.IsActive`. Registration is a one-shot at
`OnSubModuleLoad`, so this is the only point where a widget can be kept out of the prefab — a
runtime check inside the mixin cannot un-inject an already-built widget. Solo play is untouched; the
registered/suppressed counts are logged either way, which is the first thing to check if a TAOM
widget goes missing.

**Is the co-op flag reliable that early? Yes — verified, not assumed.** This is the only
`CoopPresence` consumer that cannot self-correct later, so it mattered. Decompiled v1.4.7 (two
independent Codex passes, 2026-08-01): `Module.Initialize` populates `ModuleHelper`'s
`_loadedModules` from the native module-code string **before** calling `LoadSubModules`, which is
what invokes `OnSubModuleLoad` — so the list is complete here, and even a `SubModule` constructor
already sees it. `CoopPresence.Refresh`'s "may not be populated this early" caution refers to the
pre-managed native string, not an `OnSubModuleLoad` race; an extra re-probe would not help with that
and was removed.

## Why this lives in TAOM and not in a separate compat module

Asked and answered 2026-08-01. A separate module cannot do the job, and this is a fact about
UIExtenderEx rather than a preference:

- `UIExtender.Deregister()` guards on `Instances[_moduleName] == this`. A compat module holds a
  *different* `UIExtender` instance, so it **cannot** touch TAOM's UI registration at all.
- Module load order puts a later module's `OnSubModuleLoad` *after* TAOM's, which is already too
  late — registration is a one-shot.
- It could only reach the diplomacy half, via `Harmony.Unpatch` against TAOM's own patches, which
  goes stale silently the moment TAOM's patch layout moves. Two release artifacts, one version-skew
  failure mode, to solve a problem that costs solo players nothing.

**Solo cost of the in-TAOM approach is zero, by construction rather than by care:**

| Mechanism | Solo behaviour |
|---|---|
| `CoopPresence.IsActive` | Fails **closed** — unreadable module list ⇒ false ⇒ unmodded behaviour. Solo is the default, not a branch that must be got right. |
| UI registration | Solo calls `extender.Register(assembly)` — the original UIExtenderEx call, byte-for-byte. The filtered path is reached *only* when a co-op module is present, so our type collection can never affect a solo player's UI. |
| Diplomacy gates | One bool read on war/peace/alliance-end — a cold path. Deliberately **not** cached: `Refresh()` re-probes twice during startup, so a cached read would risk staleness for no measurable gain. |

## Forcing co-op mode on: `coop-force-active.flag`

Detection matches module **ids**, so it silently fails for a renamed BT build, a fork, or a co-op
mod nobody has told us about — and that failure is the expensive direction, because TAOM keeps
enforcing vetoes and UI it should have yielded.

`coop-modules.txt` covers the case where the player knows the new id. For the case where they do
not, place a file named **`coop-force-active.flag`** in the `TAOM.Dependencies` module directory:
`CoopPresence.IsActive` then reports true and `ActiveCoopModuleIds` reports the synthetic marker
`(forced-by-flag)`.

The flag only ever **adds** presence — it can force co-op on when detection found nothing, and can
never force it off. Same direction-of-safety as `coop-modules.txt`'s union-only parse. It matches
the `patchshield-disabled.flag` / `saveshield-swallow-disabled.flag` idiom rather than being an MCM
setting on purpose: MCM persists a saved value over a changed compiled default, which is what forced
NavalTravel and NativeSkinFixes to be disabled at the wiring level instead.

## Keeping the veto surface from drifting

D1 survived for months because nothing forced anyone to ask the question. `CoopVetoClassificationTests`
now does, at build time: it scans `Main/` for every class declaring a bool-returning Harmony prefix
and requires each to carry a disposition — `GatedForCoop`, `ReviewedSafe`, or `Parked` — with a
written reason. Adding a prefix without classifying it fails the build. 32 classes are currently
classified; three are gated.

The classification question is: **can this skip a campaign-state mutation the co-op host replicates,
AND can its condition evaluate differently on two peers?** Both halves matter — `TraitLevelingHelper`
skips a real campaign mutation but reads a static alignment table, so peers always agree.

Two things about *how* it scans are load-bearing, both found by the test failing on itself:

- It scans **source, not the loaded assembly.** Reflecting over `typeof(SubModule).Assembly`
  under-reported by four, because a patch class referencing a View/Engine type comes back null from
  `ReflectionTypeLoadException.Types` in the test host — and those engine-coupled patches are exactly
  the ones worth classifying. A scan that silently misses the risky half reads as coverage.
- It keys on **class, not file**, matches prefixes with **no access modifier** (several are
  implicitly private), and strips comments before parsing. Each of those three was a real miss:
  `CharacterCreationCampaignBehavior_GetYouthMenuArgs_Patch.cs` alone holds three patch classes.

`Registry_HasNoStaleEntries` is the other half — it fails when a registry entry no longer matches a
real prefix, which is what caught the reflection under-reporting in the first place.

## Verified non-issues

Checked against BT a0.5.3.2 and found safe. Recorded so a later session does not re-litigate them.

| Area | Finding |
|---|---|
| **Save-definer collision** | BT declares **no** `SaveableTypeDefiner` at all — it persists through `MBSaveLoad.SaveAsCurrentGame` / `LoadSaveGameData` patches and a serialization gate. TAOM's four base ids, including the reused 726900601, are unthreatened by BT. The preflight guard stays valuable for *other* mods. |
| **GameModel shadowing** | BT patches vanilla model *methods*; TAOM replaces models via `AddModel`. This is safe wherever TAOM's override calls `base` (or does not override at all), because the inherited body is the Harmony-patched one. Verified for every method BT patches that TAOM also touches: `DefaultPartyWageModel.GetTotalWage` (TAOM overrides, calls base), `DefaultPartySpeedCalculatingModel.CalculateFinalSpeed` (calls base), `.CalculateBaseSpeed` and `DefaultCharacterStatsModel.WoundedHitPointLimit` (not overridden), and all five `DefaultClanFinanceModel` methods (`TaomClanFinanceModel` overrides only `CalculateTownIncomeFromTariffs`, and calls base). |
| **Weather bounds guard** | Both mods prefix `DefaultMapWeatherModel.GetWeatherEventInPosition`. TAOM's is a void prefix that clamps `ref Vec2 pos` into terrain bounds; BT's takes `pos` by value and short-circuits to `WeatherEvent.Clear` on a client when out of bounds. Either ordering is safe — TAOM-first clamps so BT's check passes; BT-first returns Clear and TAOM's clamp is harmless. Worst case is a cosmetic weather difference on out-of-bounds positions, never a crash. |

**Rule this leaves us with:** a TAOM GameModel override is co-op-safe as long as it calls `base`.
An override that fully replaces a vanilla body silently deletes any BT patch on that method. Check
`base` discipline before adding an override to a model BT patches.

## Known limitations

| Limitation | Impact | Workaround |
|------------|--------|------------|
| **No settings parity check** | TAOM's ~135 MCM settings live in a per-user file outside the save and are read live by ~30 providers. Two players with different values simulate differently — battle autocalc, bandit spawn density, AI army targeting, desertion rates — with no warning | Compare settings manually before playing. A fingerprint handshake through save metadata is designed but not built |
| **Detection reflects what the player enabled, not what constructed** | `CoopPresence` reads the launcher's active-module list. If the co-op mod's SubModule constructor throws, `SubModuleConstructionGuard` swallows it and the module still reads as active — so TAOM spends the session in co-op mode (PatchShield withholding its rescue, SaveShield rethrowing save-load faults) for a co-op layer that never came up | Check the log for the co-op mod's own init lines. Closing this needs the construction guard to map a failing assembly back to its module id plus a suppression set that survives re-probing |
| **PatchShield still swallows the missing-API trinity everywhere it shields** | The co-op SAVE-LOAD rethrow covers SaveShield's own targets (PatchShield now skips those), but on any other method a `MissingMethod`/`MissingField`/`TypeLoad` exception is still swallowed | Intended: that swallow is what keeps a stale third-party mod from killing the session |
| **TAOM state that mutates mid-session is join-time-only** | War of the Ring momentum, culture-conversion progress, messenger fleet, career progression and caravan memory reach the client through the host's save at join and then drift. BT has no schema for these fields, so its replication cannot correct them | None yet. Needs an "am I the simulation authority" signal from BT |
| Siege defense rewards fire on the host only | Client players don't trigger `SiegeDefenseBehavior` rewards | Host handles siege events; client still fights |
| Race data is host-save-authoritative | Joining client loads races from the host's save | None needed — correct behaviour |
| BattleLink battles need the separate MP window | Both players need BT's BattleLinkMPClient enabled | Follow BT's battle-server setup |

## Diplomacy ordering — TAOM's veto is OFF under co-op

TAOM and BT both prefix `DeclareWarAction.ApplyInternal` and `MakePeaceAction.ApplyInternal`. TAOM's
carry `[HarmonyPriority(Priority.High)]` (600); BT's are default `Normal` (400), so **TAOM runs
first**.

An earlier revision of this document treated that ordering as the fix. It is only half the picture,
and the half it missed is the dangerous one:

- **Host originates a war.** TAOM evaluates first and may block. BT never broadcasts. Both peers
  agree there is no war. This is the case the ordering does handle, and it is fine.
- **Client applies a war the host already committed.** BT's `SuppressClientDeclareWarPatch` lets the
  re-application through via its `KingdomSyncBehavior.IsApplyingSync` guard — but TAOM's prefix runs
  *ahead* of BT's and re-evaluates `IsWarAllowed` locally. If the client's answer differs, it returns
  false and the vanilla body never runs: **host at war, client at peace.** No crash, no log, two
  saves that disagree.

**How much the client's answer can differ depends on which veto**, and the three are not alike. The
criterion is what the condition reads:

| Veto | Condition reads | Can peers disagree? |
|---|---|---|
| `ShouldPreventPeace` | `IsWarOfTheRingActive` → `WarOfTheRingService.CurrentPhase`, persisted as the `WarOfTheRing_CurrentPhase` **`SyncData` key** | **Yes — genuinely.** TAOM campaign-behavior state that BT knows nothing about and never replicates. This is the "join-time-only state, then drifts" limitation below, wired into a gate on a vanilla state change. |
| `ShouldPreventWarDeclaration` | `GetRelationshipTier` (static `_permanentRelationships` config) + `AreSameAlignment` (static alignment table) | Only on **peer mismatch** — different TAOM versions, or edited diplomacy config. Identical installs compute identical answers. |
| `ShouldPreventAllianceEnd` | `GetRelationshipTier` — static config | Same as above. |

So the peace veto is a confirmed divergence; the war and alliance-end vetoes are a narrower
config-mismatch risk. All three are gated anyway: peers running mismatched TAOM builds or edited
configs is an ordinary co-op scenario, not an exotic one, and a veto that silently applies on one
machine and not the other is the same failure whatever made the answers differ.

**Resolution (2026-08-01):** under co-op, TAOM defers. `AllianceActionHook.ShouldPreventWarDeclaration`
/ `.ShouldPreventAllianceEnd` and `PeaceActionHook.ShouldPreventPeace` return false immediately when
`ICoopPresenceProvider.IsCoopActive`, logging `[Diplomacy][coop] … veto skipped … host is
authoritative`. One peer's ruleset has to win and TAOM cannot know which peer the session agreed on,
so it yields the whole decision rather than applying a rule to half of it.

Deliberately gated on TAOM's own `CoopPresence`, **not** on BT's `KingdomSyncBehavior.IsApplyingSync`:
reflecting into another mod's private field couples us to their internals and breaks silently on
their next build.

`AllianceCampaignBehavior.AddAllianceDecision` is **not** gated. It is a dedup guard — it skips
queuing a start-alliance decision for a pair that is already allied — and its condition
(`IsAllyWith`) is derived from replicated vanilla state, so both peers compute the same answer.
Gating it would reintroduce the decision-queue saturation it was written to fix.
`AllianceCampaignBehavior.StartAlliance` is a log-only postfix and needs nothing.

The census's contested-method rows are how to verify the ordering at runtime rather than assuming it.

## Startup crash: `DefaultClanFinanceModel..cctor()` — status

The April 2026 investigation against BT **v0.2.2** recorded a hard startup CTD:

```
NullReferenceException at TaleWorlds.CampaignSystem.GameComponents.DefaultClanFinanceModel..cctor()
```

BT's `Harmony.PatchAll()` at `OnSubModuleLoad` makes MonoMod `PrepareMethod` a
`DefaultClanFinanceModel` method, which runs the class static constructor while `Game.Current` is
still null.

**Re-verified against installed v1.4.7 (2026-07-31): still not fixable from TAOM's side, and one
tempting fix does not work.** The type's 16 static field initializers call
`Game.Current.GameTextManager.FindText(...)` — the dereference that NREs is `Game.Current` itself.
A Harmony guard on `GameTexts.FindText`, or pre-seeding `GameTexts.Initialize(new GameTextManager())`,
intercepts neither: `GameTexts._gameTextManager` is a different static from
`Game.Current.GameTextManager`, and the crash happens before either is reached. Do not implement
that guard — it would add a hot-path patch that fixes nothing.

TAOM's own defensive fix from April stands and is still worth having: all 13 TAOM GameModel override
classes use lazy `??=` properties instead of static `TextObject` field initializers, so TAOM emits no
`.cctor()` of its own for BT to trip over.

**Whether this still reproduces on a0.5.3.2 is unknown and is the first thing to find out.** If it
does, the fix is BT's: defer `PatchAll()` — or at minimum the `DefaultClanFinanceModel` patches — to
a hook where `Game.Current` is non-null. TAOM's RCA is precise enough to hand to them as-is.

## Boot matrix — run this first

Six launches to main menu, capturing `Logs/taom_debug_*.log`, `diag.log` and `rgl_log.txt` each time.
Zero code required; it gates everything else.

| # | Modules | Flag files |
|---|---|---|
| 1 | TAOM only | none (control — proves the flags don't break TAOM alone) |
| 2 | BT only | — |
| 3 | TAOM + BT | none |
| 4 | TAOM + BT | both |
| 5 | TAOM + BT | `patchshield-disabled.flag` only |
| 6 | TAOM + BT | `saveshield-swallow-disabled.flag` only |

Runs 5–6 only if 3 and 4 disagree. `IncompatibleModDetector` deletes its launch marker only in
`OnGameInitializationFinished`, so a missing deletion on the next boot yields a `NEW: <moduleId>`
culprit diff for free.

| No flags | With flags | Conclusion |
|---|---|---|
| ✗ | ✗ | Shields innocent. If the stack is `DefaultClanFinanceModel..cctor()`, the blocker survives and it is BT's to fix |
| ✗ | ✓ | Shields implicated — the interop layer above is the fix; confirm which shield via runs 5–6 |
| ✓ | ✗ | Shields are **masking** a real fault. That masked fault is what desyncs later. Capture the stack, run `/investigate` |
| ✓ | ✓ | The 2026-04 blocker is gone in a0.5.3.2. Proceed to a two-peer session |

## Testing checklist

- [ ] Boot matrix complete, outcome recorded above
- [ ] `[HarmonyCensus]` block captured on **both** machines — a difference between them is itself a finding
- [ ] Census names exactly one `0Harmony` instance
- [ ] No `[SaveDefiners] SAVE ID COLLISION` line
- [ ] Both players load with TAOM + BT, no startup crash
- [ ] TAOM factions and culture names visible on the map for the client
- [ ] Client hero has the correct race after joining
- [ ] Racial enmity blocks invalid war declarations (e.g. elves vs elves)
- [ ] War of the Ring forced wars trigger on host; client mirrors state
- [ ] Shared field battle: both peers finish, casualty counts agree
- [ ] Player's own agent responds to their own input in a shared battle (watch for the
      `AutonomousMovementPlayerController` input fight — stutter or snap-back)
- [ ] Save/load three ways: host reload, client loads host's save, client rejoins
- [ ] No `[SaveShield] swallowed` lines during any load

## Key files

| File | Purpose |
|------|---------|
| [CoopPresence.cs](../../Dependencies/Foundation/CoopPresence.cs) | Detects an active co-op module; process-constant, fails closed |
| [CoopModuleList.cs](../../Dependencies/Foundation/CoopModuleList.cs) | Union-only parser for `coop-modules.txt` |
| [PatchShieldPolicy.cs](../../Dependencies/Foundation/PatchShieldPolicy.cs) | Protected-owner allowlist + the unpatch gate |
| [SaveShieldPolicy.cs](../../Dependencies/Foundation/SaveShieldPolicy.cs) | The category × co-op × flag swallow matrix |
| [SaveDefinerCollisionGuard.cs](../../Main/Features/CoopInterop/SaveDefinerCollisionGuard.cs) | Base-id preflight and attribution |
| [HarmonyCensusReportBuilder.cs](../../Main/Features/CoopInterop/Diagnostics/HarmonyCensusReportBuilder.cs) | The report; pure and tested |
| [HarmonyCensusWriter.cs](../../Main/Features/CoopInterop/Diagnostics/HarmonyCensusWriter.cs) | The Harmony registry walk |
| [ICoopPresenceProvider.cs](../../Main/Features/CoopInterop/ICoopPresenceProvider.cs) | Test seam over the static `CoopPresence` |
| [CoopUiRegistrationPolicy.cs](../../Main/Features/CoopInterop/CoopUiRegistrationPolicy.cs) | Filters `[CoopSuppressedUi]` types out of UIExtenderEx registration |
| [CoopSuppressedUiAttribute.cs](../../Main/Features/CoopInterop/CoopSuppressedUiAttribute.cs) | Marks UI the co-op host has taken ownership of |
| [AllianceActionHook.cs](../../Main/Features/Diplomacy/Hooks/AllianceActionHook.cs) | War / alliance-end veto; `DeferToHost` under co-op |
| [PeaceActionHook.cs](../../Main/Features/Diplomacy/Hooks/PeaceActionHook.cs) | Peace veto; defers under co-op |
| [DeclareWarAction_ApplyInternal_Patch.cs](../../Main/Features/Diplomacy/Hooks/DeclareWarAction_ApplyInternal_Patch.cs) | `Priority.High` prefix — runs ahead of BT's suppression patch |
| [SiegeDefenseBehavior.cs](../../Main/Features/Siege/) | Host-only; expected not to fire on clients |
| [RacePersistenceBehavior.cs](../../Main/Features/HeroRace/) | Race data rides the campaign save |

## Changelog

- 2026-08-01 — Source review of BT a0.5.3.2 under permission granted by Hobohoppy (see Overview).
  Corrected the diplomacy-ordering section: `Priority.High` handles host origination but leaves the
  client free to veto a war the host already committed, so TAOM's war / peace / alliance-end vetoes
  now defer entirely under co-op (`ICoopPresenceProvider`). Suppressed the time-acceleration UI via
  `[CoopSuppressedUi]` + a UIExtenderEx registration filter, because BT's `TimeControlMode` setter
  prefix already owns the clock. Recorded three verified non-issues (no BT save definers, GameModel
  `base`-call safety, complementary weather guards). Boot matrix still unrun.
- 2026-07-31 — Reviewed BT a0.5.3.2 (targets v1.4.7, TAOM's own pin). Shipped the TAOM-side interop
  layer: `CoopPresence` detection, PatchShield unpatch gate, SaveShield save-load rethrow, load-order
  pins in both manifests, save-definer collision preflight, and the Harmony census. Moved TAOM's two
  cosmetic `MBRandom` draws onto the engine's non-deterministic generator. Corrected the April
  entry: the `DefaultClanFinanceModel` crash calls `Game.Current.GameTextManager.FindText`, so a
  `GameTexts.FindText` guard cannot fix it — verified against installed v1.4.7. Recorded the
  no-decompile policy that governs all future work here.
- 2026-04-03 — Noted that removing the 13 GameModel static `TextObject` field initializers does NOT
  fix the BT startup crash; RCA confirmed the crash is in vanilla `DefaultClanFinanceModel..cctor()`,
  triggered by BT's `OnSubModuleLoad`-time `Harmony.PatchAll()` when `Game.Current` is null.
- 2026-04-02 — Initial passive-compat pass: `[HarmonyPriority(Priority.High)]` on the
  `DeclareWarAction`/`MakePeaceAction` `ApplyInternal` patches so TAOM constraints validate before BT
  syncs, and this feature doc.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/INDEX.md](../INDEX.md)

<!-- backlinks-end -->
