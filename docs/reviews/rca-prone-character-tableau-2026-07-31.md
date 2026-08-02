# RCA — characters render prone ("bendy man") in every UI tableau

**Date:** 2026-07-31
**Reported by:** players (Patreon channel), not caught by any check we run
**Symptom:** the character model renders lying flat in bind pose — Character Customization, the
inventory doll, the encyclopedia — for every race, on new campaigns.
**Did not reproduce on the dev machine.** That fact is the whole story.

## Root cause (probable — see "Confidence" below)

Users were running a current `TAOM.dll` against a **stale `TAOM.Dependencies.dll`**.

`Main/TAOM.csproj` carries a `ProjectReference` to `Dependencies/TAOM.Dependencies.csproj`, and the
comment above it states the arrangement plainly: *"UIExtenderEx + HarmonyLib are provided by
TAOM.Dependencies.dll (ILRepacked / source-included into the dependency assembly)."* TAOM does not
merely call into that module — it resolves **HarmonyLib itself** from it. Pair a new `TAOM.dll` with
an older `TAOM.Dependencies.dll` and type/member resolution fails during patch application. The
HeroRace preview patches never apply, the tableau falls back to vanilla human resolution, and the
skeleton renders in bind pose, which in Bannerlord is a body lying flat.

The evidence is the shipping sequence, not a log:

| Shipped | Result |
|---|---|
| new `TAOM.dll` only | still broken |
| new `TAOM.dll` **+** new `TAOM.Dependencies` | works |

Updating one half could not fix it; updating both did.

### Why nothing could detect it

Three independent version signals all say nothing:

- `Dependencies/_Module/SubModule.xml` has read `<Version value="v2.0.5" />` on every release.
- Both assemblies carry frozen versions — `TAOM.Dependencies` is `0.1.0.0` and `TAOM` is `2.0.0.0`
  for every build ever produced. .NET therefore binds *any* pair without a version complaint and
  fails later at the member level.
- `Main/_Module/SubModule.xml` declares `DependedModule` entries for `Native`, `SandBoxCore`,
  `Sandbox` and `CustomBattle` — and **none for `TAOM.Dependencies`**. The launcher has no
  dependency to check, so it cannot warn.

The only distinguishing evidence was the file timestamp, which does not survive a zip and a download.
This is why "did I ship the newest Dependencies?" could not be answered by inspection, by the user,
or by the launcher.

## Confidence

**Probable, not proven.** Players report the matched pair fixed it, but the bug was reported as
*intermittent per launch* ("alt+F4, relaunch and it's fixed until I launch again"), and a single good
launch is not evidence against an intermittent fault. Closing this requires several consecutive clean
relaunches from an affected user. The intermittency itself is still unexplained by the version-mismatch
mechanism, which should be deterministic — that gap is the reason the instrumentation below was kept
rather than reverted.

## What was eliminated, with evidence

Each of these was a live hypothesis, and each was killed by a specific observation rather than by
argument:

| Hypothesis | Killed by |
|---|---|
| Release ships unpatched LOTRLOME race data | `action_sets.xml` / `monsters.xml` / `skins.xml` in the release are identical in size **and** mtime to the dev's hand-patched copies |
| Missing `as_<race>_facegen` action sets | `audit_action_set_parity.py` — 0 humanoid gaps across 1304 sets; `audit_civilian_action_set_coverage.py` — all 13 settlement races 43/43 male, 39/39 female |
| Stale shipped shader cache | release and live caches byte-identical, baked **after** the Jul 11 / Jul 23 data edits |
| Missing animation asset packages | AssetPackage counts identical release vs live |
| The uncommitted CoopInterop changeset | its only animation-adjacent lines swap an RNG *stream* for a mount mesh key and an elephant trample variant; neither is on the pose path |
| A recent regression | last 40 commits contain zero race / monster / skeleton / action-set / facegen work |
| Engine drift | pinned v1.4.7, `SubModule.xml` pins `v1.4.7.*`, and both users' logs show VersionProbe detecting v1.4.7 |
| Duplicate BUTR modules on user machines | user logs show 9–13 active modules with **no** standalone Harmony/ButterLib/UIExtenderEx, and the environment probe shows one copy of each assembly |
| PatchShield stripping TAOM's patches | both users' shield passes match the dev's exactly (46 / 410), zero unpatch activity, zero shielded exceptions |

Two user `diag.log` files cleared every layer TAOM already instrumented and then stopped: the
character-preview path emitted **nothing at all**. The failure window was completely dark.

## Contributing defects found on the way

These are real and were fixed or flagged regardless of the root cause.

1. **The seven preview patch categories were applied unguarded and in sequence**
   (`Main/SubModule.cs`). `PatchCategory` throwing on the first would silently prevent every later
   one from applying — a state indistinguishable in any shipped log from all seven working. Now
   isolated per category, each reporting its own outcome.
2. **Five `catch` blocks swallowed exceptions with no trace** across the tableau patches, so a
   reflection failure against a drifted engine looked exactly like the patch never running.
3. **The release payload shipped the dev's own runtime artifacts** — `diag.log`,
   `failed-mods-catalog.txt`, `last-good-modlist.txt`. Both users' logs therefore began with the
   dev's session history back to 2026-05-27, with their own sessions appended. Removed.
4. **The committed Armory snapshot had drifted 390 lines behind the live file**, and the missing
   region was the spider-rider `as_human_warrior` partial carrying the
   `LOAD-ORDER CRITICAL` comment. Any audit run against the mirror was auditing data the game never
   loads. Re-snapshotted (+402 lines, verified identical).
5. **`SaveDefinerCollisionGuard` false positive** — reports a save-id collision between
   `TaleWorlds.Core::SaveableCoreTypeDefiner` and
   `TaleWorlds.ObjectSystem::SaveableObjectSystemTypeDefiner`, both **vanilla**, and advises the
   user to "Disable one of them." Ships in the current build. Not the cause; will generate bad
   reports.

## Instrumentation added

`Main/Features/HeroRace/Diagnostics/TableauDiagnostics.cs` plus call sites, all tagged
`[TableauDiag]` in `taom_debug_*.log`. Never throws, throttled to one line per distinct situation,
errors deduplicated by message, hard cap 600 lines (~90 in a healthy session).

Records: per-category patch results; an environment dump with the loaded identity and path of
`0Harmony` / `UIExtenderEx` / `ButterLib` / `TAOM` / `TAOM.Dependencies`, flagging duplicates; a
one-shot probe of the engine's global action-set count, race count and names, and per race the
monster, `BaseMonster`, `ActionSetCode` and the resolution of `_facegen` (male/female) and `_warrior`
with skeleton name and the animation clip bound to `act_inventory_idle_start`; the Character
Customization screen's own resolution; the inventory tableau's; and the spawner's action set, pose
action + index, frame origin/rotation and skeleton. A second probe at first tableau reports one line
unless the counts changed since startup.

Verified baseline on the dev machine: all 7 categories `applied OK`, one copy of each assembly,
**1329 action sets / 15 races** identical at both probes, zero errors.

## ADDENDUM 2026-08-01 — the open thread below is CLOSED, and it was a second root cause

The version-mismatch finding above stands, but it never explained the reported **intermittency**, and
the RCA flagged that gap. The diagnostic build answered it. The real mechanism is a **static-initialiser
race in the engine**, and it is independent of the DLL pairing.

`TaleWorlds.MountAndBlade.ActionIndexCache` declares **215** `static readonly` fields (v1.4.7,
counted — an earlier draft of this document said "~700", which was wrong) populated by an **explicit**
static constructor, each via `Create(name)` → `MBAnimation.GetActionCodeWithName(name)`. Because the
cctor is explicit the type is **not** `beforefieldinit`, so *any* static member access — a field read
**or** the `Create` method — forces the whole set to initialise. If that happens before the engine has
loaded action types, every index bakes to `-1` for the life of the process, and `readonly` means the
cctor never re-runs to correct it.

Vanilla `CharacterTableau.GetIdleAction()` returns `ActionIndexCache.act_inventory_idle_start`
whenever `SetIdleAction` was never called (its `_idleAction` field initialises to `act_none`, so the
fallback is the normal path). `SetAction(-1)` is a no-op, so the skeleton never leaves bind pose.
That accounts for every trait the version-mismatch theory could not: **all races** (it is global, not
per-race), **intermittent per launch** (it is a load-order race), **works on the dev machine**
(different timing), **UI-only** (that is where the static is consumed).

Independently corroborated: a community member shipped a patched `TAOM.dll` that fixed the clan-naming
stage by overwriting the incoming action with a **live** `ActionIndexCache.Create(...)` lookup. Needing
a live lookup to repair the value is only explicable if the baked value is unusable.

**Fix:** `Main/Features/HeroRace/ActionIndexCacheRepair.cs` re-resolves poisoned fields from live
lookups and writes them back by reflection (legal for `initonly` statics on net472; the code re-reads
each field and reports a silent refusal). It is gated on `MBAnimation` — a separate struct with no
cctor — so it can never trigger the initialisation it exists to detect, and it is a no-op when the
statics are healthy.

### Deep-review findings on the fix itself (2026-08-01, 5 agents)

The first cut of the repair had three HIGH defects. All are fixed; each is a distinct lesson.

| # | Sev | Finding | Fix |
|---|---|---|---|
| F1 | HIGH | "Field names ARE action names" is **false**: the v1.4.7 cctor contains `act_raid_jump = Create("act_raid_jump_1")`, and no action named `act_raid_jump` exists. The field would stay poisoned and be misreported as "unknown to this engine build". Latent worse case: a future build where field `X` maps to action `Y` *and* an unrelated action `X` exists would write a **wrong** animation index — silent, non-crashing corruption | `KnownNameOverrides` map + a **round-trip check** (`Create(name).GetName() == name`) before every write, so an unprovable name is left at `-1` rather than guessed |
| F2 | HIGH | The retry was wired to `CharacterSpawnerService`, which resolves via live `Create()` (so never reads a poisoned static) **and** is skipped for race 0 — the human case players reported. The paths that actually read the statics had no backstop | Repair now runs first thing in `CharacterTableau_RefreshCharacterTableau_Patch.Prefix` and `FirstTimeInit`, so the same refresh consumes the repaired value and an existing tableau self-corrects |
| F3 | HIGH | The DEFERRED branch used unthrottled `LogAlways` on a path reachable per tableau init — unbounded log growth on exactly the affected machines. This is the `ae2ed426` 6.4 MB regression, reintroduced | New `LogDeduped`; `LogAlways` now counts against the total cap |
| F4 | MED | `DescribeAction` has four failure markers but callers compared only `== "<NONE>"`, so `<action-index-(-1)>` — **the poisoned case** — logged as INFO | Restored the positive predicate `HasAnimation` at both call sites |
| F5 | MED | `MBGlobals.GetActionSet` **throws** on a miss (it does not return an invalid set), so three `!IsValid` branches were dead, a `try` outside the suffix loop halved per-race coverage, and the probe fired an engine `Debug.FailedAssert` per miss at startup | Switched to non-throwing `MBActionSet.GetActionSet`; moved `try` inside the suffix loop |
| F6 | MED | `_completed = true` was set **before** the work, so a failure inside `RepairFields` permanently disabled retry *and* returned `true` | `_completed` is set only after a clean pass; failures return `false` so a later phase retries |
| F10 | LOW | "~700 fields" was an unverified figure that shipped into a user-facing log line | Corrected to 215; the log now reports the **actual enumerated count** rather than any literal |

### Codex adversarial pass (2026-08-01) — one P1 in the deep-review's own fix

Dispatched specifically at the two things the 5-agent pass could not prove. Raw:
`docs/reviews/raw/codex-adversarial-actionindexcache-repair-2026-08-01.md`.

| Suspect | Verdict | Outcome |
|---|---|---|
| **S1** round-trip guard may reject everything | **CONFIRMED RISK — P1** | `GetName()` is a native call and nothing proved names round-trip exactly. If they don't, the guard rejects every field, the repair writes nothing — **and the pass still fell through to `_completed = true`, reporting success.** A guard added to make the fix safer could have made it a permanent silent no-op. Fixed: the guard now self-tests against a known-good action and disables *itself* (not the repair) if the engine doesn't echo names back; and a repaired-nothing-because-all-rejected pass returns failure instead of latching |
| **S2** initonly reflection writes | Mostly disputed | net472 full-trust permits it; field access is `ldsfld` not a baked constant, so an already-JITted read observes the write. Residual risk covered by the existing write-then-re-read check |
| **S3** unbounded retry | **CONFIRMED** | Fixing the deep-review's F6 (latch-before-work) introduced the opposite defect: a permanent failure would re-run the 215-field scan on every tableau refresh, because the call site had moved to a Harmony prefix in the same changeset. Fixed with a 3-attempt cap |
| **S4** gate could poison the type | Disputed | `MBAnimation` has no cctor and only touches `ActionIndexCache` on the empty-name branch; the gate passes a non-empty literal. The design holds |
| **S5** re-entrancy | Partly confirmed | `_completed` was checked under the lock, then released while the repair ran — two threads could enter concurrently and both mutate vanilla statics. Fixed by running the pass under the lock |
| **S6** wrong-index write | Partly disputed | `act_none` is the only intentionally-negative field; `act_raid_jump → act_raid_jump_1` independently re-confirmed (third agent to find it) |

**The lesson worth keeping** is S1's shape: a guard introduced *in response to a review* got less scrutiny than original code, because it reads as pure risk-reduction. Recorded in `lessons/testing-qa.md` — any guard that can reject 100% of its inputs needs a self-test, and "rejected everything" must be distinguishable from "nothing to do".

## Open thread (CLOSED — see addendum above)

`CharacterTableau.GetIdleAction()` (decompiled from
`Modules/Native/bin/Win64_Shipping_Client/TaleWorlds.MountAndBlade.View.dll`, which is **absent from
the `E:\Decompiled_Bannerlord` dump**) falls back to `act_inventory_idle_start`. Patch2 injects
`as_<race>_warrior` into that tableau, and for uruk `as_uruk_warrior` is a **zero-action stub**
inheriting via `base_set="as_human_warrior"`. The Armory snapshot README records that the engine does
**not** fall through `base_set` for `act_inventory_*`. If that holds on the `_warrior` path, a set can
be *valid* and still bind no idle clip — `SetAction` is a no-op and the skeleton stays in bind pose.
Every check that existed before today stopped at "is the action set valid", and all of them passed.
The new `idleStart-anim=` field answers this on the next launch.

## Prevention — IMPLEMENTED 2026-08-01 (`633b87e5`, `e0e4fd57`)

All four are in. The mismatch that caused half of this incident can no longer ship undetected.

| # | Prevention | Where |
|---|---|---|
| 1 | Per-build `InformationalVersion` stamp (`build.yyyyMMdd-HHmmssZ`) on every TAOM assembly. `AssemblyVersion` deliberately left fixed — changing it alters binding identity for no benefit | `Directory.Build.props` |
| 2 | `Dependencies` module `<Version>` bumped to `v2.0.6`, with the matching pin in TAOM's metadata block for BUTR/BLSE launchers | `Dependencies/_Module/SubModule.xml`, `Main/_Module/SubModule.xml` |
| 3 | `<DependedModule Id="TAOM.Dependencies" />` — the element the **vanilla** launcher actually parses (`ModuleInfo.LoadWithFullPath` reads `DependedModules`, never `DependedModuleMetadatas`), so a missing or mis-ordered Dependencies is blocked at the launcher rather than surfacing as bind-posed characters | `Main/_Module/SubModule.xml` |
| 4 | Both stamps logged at `OnSubModuleLoad` with a verdict; a pairing more than an hour apart reports `MISMATCH … (issue #371)` | `Main/Core/Diagnostics/BuildStampReport.cs` |

Verified end-to-end against the built DLLs, not just unit tests: a matched pair reports `(pair OK)`,
and the **07-31 / 07-17 pairing that actually shipped** is flagged as a mismatch.

### The detector shipped broken first, and the tests did not notice

Worth recording because it is the same class of error as the bug it guards against. The stamp parser
searched for `"+build."` and took everything after it, trimming a trailing `Z`. The build emits
`build.<stamp>Z+<sha>` — no version prefix (`Directory.Build.props` is imported *before* the csproj's
`PropertyGroup`, so `$(Version)` is empty there) and a commit-SHA suffix appended by
`Bannerlord.BuildResources`, so the timestamp is not at the end of the string. Every real assembly
fell through to "cannot verify pairing": the detector reported the absence of a stamp that was
present in the DLL.

The unit tests passed the whole time, because they asserted the format the code **assumed** rather
than the one the build **produces**. It was caught by running the parser against the built DLLs.
There is now a test using the real emitted string verbatim. Lesson recorded in `lessons/testing-qa.md`.

That broken state was also pushed, because a parallel session's `git add -A` swept the in-progress
work into an unrelated commit (`633b87e5`) — see the build/tooling lesson on concurrent sessions.

### Process change (still worth stating)

The dev install ran a **newer build than the one shipped** (`TAOM.dll` 07-31 vs released 07-30;
`TAOM.Dependencies.dll` 07-31 vs released **07-17**), so the exact shipped combination was never
tested. **Build both, ship both, never hand-copy one module.**

## Instrumentation retired 2026-08-01

The per-race action-set probe, the environment dump and the action-index health probe did their job
— they identified the static-initialiser race — and were removed (293 lines). What remains is the
repair's own verdict line plus the error paths that fire only when a preview actually resolves badly,
which is what a user log needs to confirm the fix. The rest comes out once #371 closes in the wild.

## Related

- `docs/reviews/lessons/build-tooling-workflow.md` — version-identity lesson
- `docs/reviews/lessons/harmony-il.md` — patch-category batching lesson
- `docs/reviews/lessons/animation-skeleton.md` — the all-races-vs-one-race discriminator
- `docs/reference/lotrlome-armory-snapshot/README.md` — snapshot re-sync 2026-07-31
