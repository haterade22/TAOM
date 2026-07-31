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

## Open thread

`CharacterTableau.GetIdleAction()` (decompiled from
`Modules/Native/bin/Win64_Shipping_Client/TaleWorlds.MountAndBlade.View.dll`, which is **absent from
the `E:\Decompiled_Bannerlord` dump**) falls back to `act_inventory_idle_start`. Patch2 injects
`as_<race>_warrior` into that tableau, and for uruk `as_uruk_warrior` is a **zero-action stub**
inheriting via `base_set="as_human_warrior"`. The Armory snapshot README records that the engine does
**not** fall through `base_set` for `act_inventory_*`. If that holds on the `_warrior` path, a set can
be *valid* and still bind no idle clip — `SetAction` is a no-op and the skeleton stays in bind pose.
Every check that existed before today stopped at "is the action set valid", and all of them passed.
The new `idleStart-anim=` field answers this on the next launch.

## Prevention (not yet implemented)

1. Stamp real build versions into both assemblies so a DLL identifies itself.
2. Bump `Dependencies/_Module/SubModule.xml` `<Version>` whenever the assembly changes.
3. Add `TAOM.Dependencies` to `Main/_Module/SubModule.xml` `DependedModules` with a version pin, so
   the launcher blocks a mismatched pair instead of the game failing silently.
4. Log both build stamps at startup, so a future report answers this in one line.

Process changes worth making independently: the dev install ran a **newer build than the one
shipped** (`TAOM.dll` 07-31 vs released 07-30; `TAOM.Dependencies.dll` 07-31 vs released **07-17**),
so the exact shipped combination was never tested. **Build both, ship both, never hand-copy one
module.**

## Related

- `docs/reviews/lessons/build-tooling-workflow.md` — version-identity lesson
- `docs/reviews/lessons/harmony-il.md` — patch-category batching lesson
- `docs/reviews/lessons/animation-skeleton.md` — the all-races-vs-one-race discriminator
- `docs/reference/lotrlome-armory-snapshot/README.md` — snapshot re-sync 2026-07-31
