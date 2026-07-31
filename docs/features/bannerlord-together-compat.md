# BannerlordTogether Compatibility

## Overview

[BannerlordTogether (BT)](https://www.nexusmods.com/mountandblade2bannerlord/mods/10426) is a
third-party host-authoritative co-op mod: one player's game owns the campaign simulation and
replicates towns, villages, AI parties, battles and diplomacy to the other. TAOM does **not** bundle
or extend it. This doc records what TAOM does on its own side so a TAOM + BT session can boot,
survive, and tell players when their two installs have drifted apart.

**Status: TAOM-side interop layer shipped 2026-07-31; end-to-end co-op unverified.** The boot matrix
below has not been run against BT a0.5.3.2. Do not tell players co-op works until it has.

> ### We do not decompile BannerlordTogether
>
> The package ships `AI_USAGE_POLICY_DO_NOT_DECOMPILE.txt` and a proprietary `LICENSE.txt` in
> `bin/Win64_Shipping_Client/`, forbidding any person or automated system from decompiling,
> disassembling, reverse-engineering or otherwise analyzing the implementation of
> `BannerlordTogether.dll` / `BattleLinkCommonSvMp.dll`, and stating that no authorization exists.
> TAOM honours this. Everything we know about BT comes from three legitimate sources: its own
> shipped manifest and config, HarmonyLib's public runtime registry (see the census below), and
> asking its authors. Anyone extending this work must keep to those three.
>
> Third-party libraries BT bundles (`0Harmony`, `LiteNetLib`) are explicitly outside that licence
> and are version-checked normally.

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
- `Main/_Module/SubModule.xml` carries
  `<DependedModuleMetadata id="BannerlordTogether" order="LoadAfterThis" optional="true"/>` —
  metadata only, so TAOM stays launchable without BT installed.

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

`SaveDefinerCollisionGuard` runs at `OnBeforeInitialModuleScreenSetAsRoot`, groups every definer it
finds by base id, and logs `[SaveDefiners] SAVE ID COLLISION on base id N between: …` naming both
assemblies — before the engine hits the same constructors. It never repairs anything; it makes the
crash attributable.

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

## Diplomacy ordering

TAOM and BT both patch `DeclareWarAction.ApplyInternal` and `MakePeaceAction.ApplyInternal`. TAOM's
prefixes carry `[HarmonyPriority(Priority.High)]` so they run first: TAOM validates racial enmity and
War of the Ring constraints, and if it blocks the action BT never syncs it. Without that ordering BT
could broadcast a war declaration that TAOM then blocks on the host, leaving clients desynchronised.

The census's contested-method rows are how to verify this ordering actually holds at runtime rather
than assuming it.

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
| [DeclareWarAction_ApplyInternal_Patch.cs](../../Main/Features/Diplomacy/Hooks/DeclareWarAction_ApplyInternal_Patch.cs) | `Priority.High` — validate before BT syncs |
| [SiegeDefenseBehavior.cs](../../Main/Features/Siege/) | Host-only; expected not to fire on clients |
| [RacePersistenceBehavior.cs](../../Main/Features/HeroRace/) | Race data rides the campaign save |

## Changelog

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
