---
name: engine-bump
description: Respond to a Bannerlord engine update (Steam force-bump or deliberate migration) — preserve the decompile baseline, regen, diff, re-verify bindings and creature data, control-battle.
---

# Engine Bump Response

Run when the installed Bannerlord version changes — whether Steam force-updated under us (the
1.4.5 → 1.4.6 event, 2026-06-11 17:39, discovered a day late at the cost of a morning of
misattributed crashes) or we migrate deliberately. The session-start hook warns on drift
(pinned version in `.claude/pinned-game-version.txt`); this skill is the response. Dated bump stories and
calibrations live in [engine-bump-history.md](../../../docs/migration/engine-bump-history.md).

## Phase 1 — Confirm + scope the drift

1. Installed truth: `bin/Win64_Shipping_Client/Version.xml` + DLL `LastWriteTime`s (the update
   timestamp bounds which test runs were on which engine — decisive for crash attribution).
   `Version.xml` carries only `vX.Y.Z`; read the build number from `TaleWorlds.Library`
   (`ApplicationVersion.DefaultChangeSet`, `BuildInfo.GameVersion`) or `package_info.txt`. Steam
   re-stamps every engine DLL on an update, so
   timestamps bound the update time but cannot show what changed.
2. **The wEditor build — run this every time; it is the step that got skipped on 1.4.7 → 1.4.8.**
   The Modding Kit build (`bin/Win64_Shipping_wEditor`) updates on its OWN Steam schedule, so read
   its `Version.xml` and its `TaleWorlds.Native.dll` `LastWriteTime` separately from the client's.
   It can land well after the client (44 minutes on v1.5.6; whole versions before 2026-08-10), so re-read its `Version.xml` and the DLL hash right before the
   Phase 2 decompile and again before trusting `_editor_build`.
   It can jump several versions in one update ([engine-bump-history.md](../../../docs/migration/engine-bump-history.md), "wEditor jump of 2026-08-10").
   What skipping it breaks: triage of an editor crash, which passes `--dll` on the wEditor binary. An offset
   read from that binary holds only for the version in its own Version.xml.
   Steam overwrites in place, so since 2026-10-26 (#1004) the session-start hook archives each new
   client and wEditor `TaleWorlds.Native.dll` the first session it appears, with its engine-method
   map and matcher index, into `E:\Decompiled_Bannerlord\_native\TaleWorlds.Native.v<ver>.{client,wEditor}.dll`.
   **Check it ran:** run `python tools/native_archive.py` with no flags (it archives any installed build
   whose size or mtime differs from its saved row), then `--status`. `--status` shows saved state only,
   one row per install path, and does not re-check the installed DLL, so confirm each row's `sha16`
   (the first 16 hex digits of the SHA-256) against the installed file (#1038):
   `python -c "import hashlib,sys;print(hashlib.sha256(open(sys.argv[1],'rb').read()).hexdigest()[:16])" <dll>`.
   A mismatch or no row means the installed build is not archived (a silent run can mean the lock is
   held): copy it to `_native\TaleWorlds.Native.v<ver>.{client,wEditor}.dll` with `<ver>` from that
   folder's `Version.xml`. `E:\Decompiled_Bannerlord\_native\` must hold the OLD
   `TaleWorlds.Native.v<old>.{client,wEditor}.dll` pair; if it does not, copy both `bin/Win64_Shipping_{Client,wEditor}/TaleWorlds.Native.dll` aside by hand
   now:
   that copy is the only baseline the NEXT bump will have, and `native_build_match.py` reads it. If the OLD pair
   is already missing it is gone: record that and expect `oracle=unscored`. The cost of not having one
   is already booked: the open player report (untracked; read it with
   `git show b2e387db:crashz/report.json`; `BannerlordVersion v1.4.7.117484`) can no longer be
   triaged against a local binary.
3. `taom-src` auto-detects the new version: a fresh cache and DLL index under `~/.taom-src/v<new>/`, and the
   old caches stay unused. Pass a short or fully qualified type name. A mod's own copy of an engine assembly
   (`Modules\Coop`) searches last (#1038), so only a type that copy alone still holds resolves from it. The
   tool prints only the cache path; `~/.taom-src/sources.json` names the DLL it read.
4. **Warm the native decompiler, and read its map check.** Each new binary gets a fresh Ghidra
   project, so run `python tools/native_decompile.py --engine-method get_current_action_type`
   once for the client and once with `--dll` on the wEditor DLL: the first crash then decompiles in
   seconds, not minutes. Any `WARNING: engine-method map:` line means the map is incomplete for that
   binary, most likely because the registration sweep met an instruction form it does not read:
   `--engine-method` refuses that assembly and the project is not seeded until `tools/native_engine_methods.py`
   reads every id. Fix the sweep before trusting native answers for that build. Native offsets quoted in
   docs and RCAs belong to their old binary; re-derive one by engine method or string, not by
   offset ([ghidra-native-decompile.md](../../../docs/features/ghidra-native-decompile.md)), or
   carry it. The session-start archive scores the new pair on its own (`python tools/native_archive.py
   --status` shows `oracle=ok vs <old>`, or a banner NOTE names WRONG matches); without that result, run
   `python tools/native_build_match.py --old <archived old client DLL> --oracle` once
   (exit 1, a wrong match: do not use it on this pair; exit 2, no map for the old build: the pair is
   unscored), then `--rva <old offset>`. Only `MATCHED ... (hash)` gives an exact offset, and it
   means identical code, not proven identity: read the new address with `native_decompile.py --rva`
   before it goes into a pin ([native-build-match.md](../../../docs/features/native-build-match.md)).
5. **Resolve the last RELEASED `TAOM.dll` against the new engine, not just a fresh build.** A fresh
   compile binds whatever overload the new engine offers, so it hides a removed member that a shipped
   binary still calls (v1.5.4 removed a `TooltipProperty` constructor: every gate stayed green while
   the released build would have thrown `MissingMethodException`). Run
   `pwsh tools/check_engine_member_refs.ps1 -Dll <every shipped engine-referencing DLL, comma-joined>
   -Baseline <the cached old BUTR reference assemblies, comma-joined>`
   (`E:\nuget\packages\bannerlord.referenceassemblies.{core,native,sandbox,storymode,custombattle}\<old>\ref\net472`;
   they keep non-public members, so they also serve as the old-engine baseline after Steam overwrites
   the install). Shipped DLLs live under `E:\LOTRAOM_Releases\<channel>\Modules\`. Negative control:
   a fresh build of HEAD must report 0 misses. A NEW miss means that channel needs a rebuilt release
   before players take the engine update; a miss the baseline also has is pre-existing. The checker
   reads member and type references only, so it cannot see an override of a changed abstract member
   (a shipped `TaomHowdahMachine` overriding the old one-argument `GetDescriptionText` passed it); the
   source compile of HEAD catches those (#1038). It also cannot see an engine enum or const whose
   value moved, which the compiler bakes into the caller (v1.5.6 `BattleEnvironment` lost `None`; only a
   type diff shows it, and a rebuild fixes it). Either kind in HEAD means every channel built before the
   fix needs a rebuild, even with 0 checker misses.

## Phase 2 — Preserve the baseline, THEN regenerate

`decompile_bannerlord.ps1` writes **three** folders. Archive all three — not just `_shipping_build`.

```powershell
$D = "E:\Decompiled_Bannerlord"
Rename-Item "$D\_shipping_build" "_shipping_build_v<OLD>"   # BEFORE regen!
Rename-Item "$D\_editor_build"   "_editor_build_v<OLD>"     # ditto — own Steam schedule, see Phase 1.2
Rename-Item "$D\_modules_build"  "_modules_build_v<OLD>"    # ditto — the module-bin satellites
pwsh tools/decompile_bannerlord.ps1    # background; ~5-10 min; rewrites all three
```
The renamed baseline is what makes diffing possible — 1.4.5's lives at
`E:\Decompiled_Bannerlord\_shipping_build_v1.4.5`.

**Archiving is not optional, because the loss is one-way.** Steam overwrites the install in place, so an assembly
that was not in the decompile stack when the update landed has no recoverable baseline afterwards (the 1.4.7 to
1.4.8 loss: [engine-bump-history.md](../../../docs/migration/engine-bump-history.md), "Partial baseline of 1.4.7").

The curated category folders (`Campaign\`, `MountAndBlade\`, …) come from a **different** tool
(`tools/decompile_to_folder.ps1`, which needs `-Force` against a non-empty destination) and stay at
the OLD version until you run it — archive them first as `_categories_v<OLD>`, same convention as
the build folders. Note that tool takes only the PRIMARY DLL per module (SandBox.dll, StoryMode.dll,
…), which is the other half of why the satellites were never in any artifact.

`tools/decompile_bannerlord.ps1` decompiles every module's bin, including third-party mods that
bundle their own engine DLLs (the Coop module ships stale copies). Never read an engine type from a
non-vanilla `_modules_build` file; `engine_body_diff.py` skips them.

## Phase 3 — Diff to scope the blast radius

Compare **all three** archived folders against their regenerated twins — `_shipping_build`,
`_editor_build`, AND `_modules_build`. A base-bin-only diff is the partial one that missed the
satellites (Phase 2).

**Filter the ilspycmd nag line first, or every file reads as changed.** `ilspycmd` appends a
`Latest version is '<theirs>' (yours is '<ours>')` line to each `.cs`, and `<theirs>` moves with
the tool's own release cadence, not the game's — on 1.4.7 → 1.4.8 it was `10.1.0.8386` in the
baseline vs `10.1.1.8388` in the fresh run, so a raw MD5/diff loop reported all 56 shipping files
changed. The idiom that actually works (Git Bash), per file:

```bash
diff <(grep -v "^Latest version is" "$OLD/$f") <(grep -v "^Latest version is" "$NEW/$f")
```

No output = the assembly is byte-identical. Then `git diff --no-index` the ones that survive the
filter. Per-bump calibrations: [engine-bump-history.md](../../../docs/migration/engine-bump-history.md).

**Then diff at the MEMBER level, not the file level, for every member TAOM binds.** When every
assembly moves (1.5.0 rewrote 56 of 56), the file diff is worthless and the compile gates answer
only "does the signature still resolve". A member whose signature is unchanged and whose BODY now
does something else passes every gate. Enumerate the bound surface from the API snapshot
(`docs/reference/taleworlds-api-snapshot/{patch-targets,gamemodel-bases,reflection-sites}.md`) plus
the XML `Deserialize` loaders, extract each member's body from the archived and the fresh
decompile, and diff them. **Regenerate the API snapshot first** (`pwsh tools/snapshot_api_surface.ps1`
after a TAOM.Tests build), or members added since the last regeneration are never compared. Run
`python tools/engine_body_diff.py --old-suffix _v<OLD> --out E:\Decompiled_Bannerlord\_diff_<OLD>_to_<NEW>\bound_members`;
it requires `unparsed=0`, covers reflection sites and `Deserialize` loaders, and normalizes
non-ASCII text (older decompiles mapped some letters to ASCII or `?`, so identical bodies differed
in quotes and accents). Positive control: run it on the previous bump pair; it must still report that
bump's known changes. Rank the CHANGED rows and hand them to review agents in
batches of six to ten with the diff, the TAOM override and the question "does TAOM re-implement a
term that moved". **Full-replacement GameModel overrides first**: an additive override (`base.`
then TAOM on top) inherits a body change for free, a replacement inherits nothing, and the two are
indistinguishable to a signature gate. Calibration: [engine-bump-history.md](../../../docs/migration/engine-bump-history.md), "v1.5.2 member diff". A row it cannot resolve is a failure, not a pass. Until #1038 item 4 lands, two rows fail every
run: `CoopRecruitAttributionPatch` (UNPARSED) and `MissionScreen.IMissionSystemHandler.UpdateCamera`
(UNRESOLVED). Diff both by hand; any other UNPARSED or UNRESOLVED row is a failure. A `TargetMethods()` patch appears in the snapshot by its
first target only, so diff its other targets by hand (#1038): `grep -rlE 'IEnumerable<MethodBase>\s+TargetMethods' Main Client Dependencies` lists them. Record the verdicts in `docs/migration/v<ver>-diff-ranked.md`.

## Phase 4 — Re-verify TAOM against the new engine

1. **The binding gate** ([Running the gate](../../../docs/reference/taleworlds-api-snapshot/README.md)): every Harmony patch / GameModel / reflection site / `[ViewModelMixin]` refresh method (`ViewModelMixinBindingTests`), and the prefab gates
   in the same category. `PrefabCloneWidgetReferenceTests` is the one to read when a vanilla prefab
   moved: a TAOM clone REPLACES the vanilla file, so a widget reference the engine adds (v1.5.0
   `BloodFeudIconWidget`, v1.5.3 the ship banners) is a null the widget dereferences on frame one.
   Re-base the clone on the installed file, re-apply TAOM's edits; a clone with no TAOM edit is deleted, in the repo and in the deployed `Modules/TAOM/GUI/Prefabs/` (a deploy never deletes, and Phase 5 runs the installed module). Two things about the
   snapshot half (`pwsh tools/snapshot_api_surface.ps1`): it reads `TAOM.dll` out of
   `TAOM.Tests/bin/{Debug,Release}/net472` and **throws if TAOM.Tests has not been built**, so build
   first; and `-Check` exits 1 with DRIFT when a patch target or GameModel base moved, which after a bump is expected until you
   regenerate. It ignores the backlinks footer.
   The same gate runs Patch99's live harness (`XmlMergeLiveEquivalenceTests`), because the IL
   fingerprint of the merge bodies does not pin every input of the merge. Its runsettings set
   `TAOM_RUN_BENCHMARKS=1`, which a plain `dotnet test` needs to run it. A red harness means the fast
   path no longer matches the engine's merge, or the gate's inventory or speed rule failed: read the
   message ([fast path](../../../docs/features/xml-merge-fast-path.md), "How to run the harness").
2. **`python tools/audit_mount_parity.py`** — creature data vs the (possibly re-schema'd)
   vanilla baselines. **Report-only: it contains no `sys.exit` and always exits 0**, so it cannot be
   gated on in a script and a green exit code proves nothing — READ the output.
3. **`python tools/audit_action_set_parity.py`** — two independent gates, **exit 1 on either**, so
   read the report before assuming which one fired:
   - **Humanoid surface.** Resolves EVERY action_set's effective surface (own actions + `base_set`
     chain + cross-module field-merge) and flags any HUMANOID set missing part of Native's
     `as_human_warrior`. A new engine version can add action types to `as_human_warrior` that a
     standalone set won't inherit → CTD on first use (history: [engine-bump-history.md](../../../docs/migration/engine-bump-history.md), "Dwarf action parity"). For each gap, fix with
     `python tools/patch_dwarf_action_parity.py --target <action_sets.xml> --set-id <id> --apply`.
     **`--target` is `required=True`** — the command previously written here omitted it and argparse
     rejects it outright — and it patches ONE file per run, so run it twice: the live
     `<game>\Modules\LOTRLOME_Armory\ModuleData\action_sets.xml` and the tracked
     `docs/reference/lotrlome-armory-snapshot/action_sets.xml`. Creature mounts
     (spider/elephant/chariot) use a separate surface → `audit_mount_parity.py`.
   - **Structure.** Flags any root-level `<action>` — one parented by `<action_sets>` instead of an
     `<action_set>`. Build 1.4.7.117484 tolerates these silently; build 117131, which TaleWorlds'
     DEDICATED SERVER engine ships, throws `KeyNotFoundException` in `MBObjectManager.MergeElements` at
     `/action_sets/action` and dies on boot, so re-check the merge path per bump. Fix with
     `python tools/oneoff/fix_orphaned_tavern_conversation_actions.py --apply` (idempotent; rewrites the live Armory
     file and the tracked snapshot together). History: [engine-bump-history.md](../../../docs/migration/engine-bump-history.md), "Orphaned tavern actions".
4. Vanilla data rescan per `.claude/rules/vanilla-data-comparison.md` (scene renames/removals,
   XML re-schemas) + check which vanilla `ModuleData` XMLs the update actually touched
   (timestamp filter). TAOM rows that define vanilla GameText ids (the `str_last_seen` block in
   `taom_module_strings.xml`) override Native's on merge, so delete them once vanilla defines those ids.
5. Update the pin: write the new version into `.claude/pinned-game-version.txt` (the file
   `session-start.sh` reads; note it's `.claude/`, NOT `.claude/state/`).
6. The `Target:` line in AGENTS.md (`lint_docs.py` checks it against the pin), plus the
   memory resume card if one tracks the bump.
7. Every other pin the version feeds, each gated or found by grep:
   - `Main/_Module/SubModule.xml` Native `DependedModuleMetadata` to `v<new>.*`
     (`NativeConstraint_MatchesPinnedGameVersion`), and the same row in the live
     `TAOM_Map/SubModule.xml` and `LOTRLOME_Armory/SubModule.xml` (unversioned: back up first).
   - `GameReferences.targets` `BannerlordRefAsmVersion` to BUTR's build for the new changeset
     (`BannerlordRefAsmVersion_PinnedGameVersion_IsTheSameGameBuild`). Check the
     NuGet flat-container index for `bannerlord.referenceassemblies.core`; BUTR can publish hours
     after Steam, so re-check before calling it blocked.
     `GameReferencesTargetsTests.BannerlordRefAsmVersion_PinnedGameVersion_IsTheSameGameBuild` goes
     red as soon as the engine moves (`DefaultChangeSet` is compiled in), whatever the pin says, and
     stays red until BUTR publishes. No CI uses the reference assemblies since the workflows were
     removed (`c8d69a0dc`). After the pin moves, run the three no-game commands in
     `.ai/verification.md` and quote that run.
   - The per-build native address pins. The two C# `KnownBuilds` tables, in
     `ClipBudgetSignatureInstalledBinaryTests` and `SkeletonBufferInstalledBinaryTests`, skip in the
     default suite and FAIL in the binding gate (item 1) on a build with no row; `NativePinGateTests`
     holds both to that and to this list. The binding gate will therefore fail until this item adds
     the rows, which is expected. `PINS` in `tools/tests/test_native_decompile.py` and the `pins`
     table in `tools/tests/test_native_engine_methods.py` still SKIP on an unknown build and run only
     with `TAOM_GHIDRA_IT=1`, so read their skip count. Add a row
     per build; derive engine methods with `native_engine_methods.load_or_build`, other addresses
     with `native_build_match.py --rva` (Phase 1 item 4). The engine-method map
     counts are version-keyed and the `ProcessPreloadQueue` thunk target is a per-row `thunk_target`
     key. The latest numbers are in `docs/migration/v<ver>-impact.md`.
   - The category tree (`decompile_to_folder.ps1 -Destination _categories_v<new>`) and the three
     handbook-gate references to it (`tools/check_handbook_attributes.py`,
     `tools/handbook_attribute_manifest.json`, its test).
   - `git grep -n "v<old>"` over README.md, `docs/modding/` and `docs/reference/` for statements of
     the current version.
   - The hand-copied engine id sets, which stay green when the engine drops an id: the 32 policy ids
     in `KingdomPolicyIdsTests.EnginePolicyIds` (re-read `DefaultPolicies.RegisterAll`, #756) and
     `ENGINE_REGISTERED_ITEMS` in `tools/validate_moduledata.py` (`DefaultItems`).
     - Re-derive `REQUESTED_IDS` and `NATIVE_BY_TYPE` in `tools/audit_mbproj_registration.py` from the
       new engine (the managed requesters in `Module.cs`, `MapColorGradeManager` and
       `MBMusicManager`; the native by-type loader's callers) and `MANAGED_TWIN` from `Game.cs`.
8. `TAOM.Dependencies` binds some engine members by name. The binding gate (item 1) covers two of
   them, `ModuleHelper.GetActiveModules` and `ApplicationVersion.FromParametersFile` (Category B of
   `reflection-sites.md`). Re-check the rest by hand: `ModuleInfo.Id` (`IncompatibleModDetector.cs`,
   Category C), the `ApplicationVersion` Major, Minor and Revision reads (`VersionProbe.cs`) and the
   string targets in `SaveShield.cs`.
9. Run `/armory-audit` and `tools/audit_scene_names.py` plus `tools/build_battle_scenes.py --check`,
   because an update can re-pack Native asset packages and scenes.
10. Write the Gates section of `docs/migration/v<ver>-impact.md` from the final `/verify` output,
    after the commit, in the past tense, and name the commit each number comes from. A later commit
    that changes one of those numbers rewrites the whole section, not only its own clause.

## Phase 5 — Control battles before believing anything

First read the new build's first start in `taom_debug_*.log` for the native guards
(`docs/features/skeleton-buffer-guard.md`, "After an engine bump"): both `[SkeletonBuffer] guard ON pool 1` and
`guard ON pool 2` must appear; a `guard OFF pool N` (or one `guard OFF:`) with a pattern reason means players lost
that pool's freeze protection until the patterns are re-derived. Then: vanilla-only → warg → each TAOM creature, charging and meleeing. **On any CTD, compare the
Windows Event Log fault offset against the previous version's known sites BEFORE assuming your
last change caused it** (`/native-crash-triage` Phase 1) — the rewritten native lookups in new
engine versions turn previously-tolerated data misses into AVs.

Worked example of the whole protocol: `docs/features/spider.md` "The v1.4.6 engine-bump
campaign" + CHANGELOG 2026-06-12.
