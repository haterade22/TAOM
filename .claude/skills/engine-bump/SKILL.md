---
name: engine-bump
description: Respond to a Bannerlord engine update (Steam force-bump or deliberate migration) — preserve the decompile baseline, regen, diff, re-verify bindings and creature data, control-battle.
---

# Engine Bump Response

Run when the installed Bannerlord version changes — whether Steam force-updated under us (the
1.4.5 → 1.4.6 event, 2026-06-11 17:39, discovered a day late at the cost of a morning of
misattributed crashes) or we migrate deliberately. The session-start hook warns on drift
(pinned version in `.claude/pinned-game-version.txt`); this skill is the response.

## Phase 1 — Confirm + scope the drift

1. Installed truth: `bin/Win64_Shipping_Client/Version.xml` + DLL `LastWriteTime`s (the update
   timestamp bounds which test runs were on which engine — decisive for crash attribution).
2. **The wEditor build — run this every time; it is the step that got skipped on 1.4.7 → 1.4.8.**
   The Modding Kit build (`bin/Win64_Shipping_wEditor`) updates on its OWN Steam schedule, so read
   its `Version.xml` and its `TaleWorlds.Native.dll` `LastWriteTime` separately from the client's.
   It does not merely "stay a version behind": on 2026-08-10 it jumped **three versions at once**,
   `v1.4.5.114928` → `v1.4.8.119303` (`public const string GameVersion`, `_editor_build_v1.4.5/`
   vs `_editor_build/TaleWorlds.Library.cs`), while the client moved 1.4.7 → 1.4.8.
   **What skipping it breaks: native crash triage, silently.** `/native-crash-triage` derives fault
   offsets from the wEditor `TaleWorlds.Native.dll`, so that binary's version *is* the offset base —
   every offset taken before that jump was against a v1.4.5 image, and it moved with no signal.
   Steam overwrites in place and nothing archives native modules (the decompile stack only *lists*
   them in `_native_dlls.txt`), so **copy both `bin/Win64_Shipping_{Client,wEditor}/TaleWorlds.Native.dll`
   aside now** — that copy is the only baseline the NEXT bump will have. The cost of not having one
   is already booked: the open player report (untracked; read it with
   `git show b2e387db:crashz/report.json`; `BannerlordVersion v1.4.7.117484`) can no longer be
   triaged against a local binary.
3. `taom-src` auto-detects the new version (fresh cache under `~/.taom-src/v<new>/`) — old
   caches remain but are unused.
4. **Warm the native decompiler, and read its map check.** Each new binary gets a fresh Ghidra
   project, so run `python tools/native_decompile.py --engine-method get_current_action_type`
   once for the client and once with `--dll` on the wEditor DLL: the first crash then decompiles in
   seconds, not minutes. Any `WARNING: engine-method map:` line means the map is incomplete for that
   binary, most likely because the registration sweep met an instruction form it does not read:
   `--engine-method` refuses that assembly and the project is not seeded until `tools/native_engine_methods.py`
   reads every id. Fix the sweep before trusting native answers for that build. Native offsets quoted in
   docs and RCAs belong to their old binary; re-derive one by engine method or string, not by
   offset ([ghidra-native-decompile.md](../../../docs/features/ghidra-native-decompile.md)).
5. **Resolve the last RELEASED `TAOM.dll` against the new engine, not just a fresh build.** A fresh
   compile binds whatever overload the new engine offers, so it hides a removed member that a shipped
   binary still calls. v1.5.4 removed `TooltipProperty(string, string, int, bool, TooltipPropertyFlags)`
   for a six-parameter twin: source and every binding gate stayed green while the v2.0.33 testing
   build would throw `MissingMethodException` on 21 tooltip sites. Read every engine `MemberRef` of
   each channel's `TAOM.dll` (`E:\LOTRAOM_Releases\<channel>\Modules\TAOM\bin\Win64_Shipping_Client`)
   with System.Reflection.Metadata and resolve it, by name and full signature, against the installed
   engine assemblies; run a fresh build as the negative control (it must report 0). The v1.5.4 run's
   checker is archived at `E:\Decompiled_Bannerlord\_diff_1.5.3_to_1.5.4\refcheck\` (`dotnet run --
   <TAOM.dll> <engine bin dirs...>`) until a committed tool exists (follow-up on #736); it skips
   members whose parent is a generic type instantiation, so a clean run is not proof for those. A hit means that channel needs a rebuilt release before players take the engine update.

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

**Archiving is not optional, because the loss is one-way.** Steam overwrites the install in place,
so an assembly that was not in the decompile stack when the update landed has **no recoverable
baseline afterwards** — there is nothing to go back to. That is what happened on 1.4.7 → 1.4.8:
`_modules_build` did not exist yet, and the two `<GameBin>\Win64_Shipping_*` folders the script
walked never held the module-bin satellites — `SandBox.View`, `SandBox.ViewModelCollection`,
`SandBox.GauntletUI` (+ its two `AutoGenerated`), `TaleWorlds.MountAndBlade.View`,
`TaleWorlds.MountAndBlade.GauntletUI` (+ its two `AutoGenerated`),
`TaleWorlds.MountAndBlade.Platform.PC`, and the StoryMode / Multiplayer / CustomBattle / NavalDLC /
BirthAndDeath / FastMode satellites. TAOM patches into several of them (`AgentVisuals`,
`CharacterTableau`, `MobilePartyVisual`, `SPInventoryVM`, the tournament controllers), so that
bump's assembly diff was silently partial. Partial recovery came only from the per-type decompiles
`~/.taom-src/v1.4.7/` happened to have cached (42 of them from module DLLs — all 42 diffed
identical against 1.4.8); anything not cached had no 1.4.7 form left to compare.

The curated category folders (`Campaign\`, `MountAndBlade\`, …) come from a **different** tool
(`tools/decompile_to_folder.ps1`, which needs `-Force` against a non-empty destination) and stay at
the OLD version until you run it — archive them first as `_categories_v<OLD>`, same convention as
the build folders. Note that tool takes only the PRIMARY DLL per module (SandBox.dll, StoryMode.dll,
…), which is the other half of why the satellites were never in any artifact.

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
filter. Calibration: 1.4.6 left the combat assembly (`TaleWorlds.MountAndBlade`) and interop
(`AutoGenerated`) byte-identical, only CampaignSystem/UI moved → all gameplay change was
NATIVE-internal; 1.4.8 moved 8 of 56 base-bin assemblies, 3 of those by build number alone.

**Then diff at the MEMBER level, not the file level, for every member TAOM binds.** When every
assembly moves (1.5.0 rewrote 56 of 56), the file diff is worthless and the compile gates answer
only "does the signature still resolve". A member whose signature is unchanged and whose BODY now
does something else passes every gate. Enumerate the bound surface from the API snapshot
(`docs/reference/taleworlds-api-snapshot/{patch-targets,gamemodel-bases,reflection-sites}.md`) plus
the XML `Deserialize` loaders, extract each member's body from the archived and the fresh
decompile, and diff them. **Regenerate the API snapshot first** (`pwsh tools/snapshot_api_surface.ps1`
after a TAOM.Tests build), or members added since the last regeneration are never compared. The
v1.5.4 script is archived as `E:\Decompiled_Bannerlord\_diff_1.5.3_to_1.5.4\bodydiff.py`; it prints an
`unparsed=` count, which must be 0. Its first cut parsed table rows with a regex that skipped every
target carrying a generic-arity backtick (`` List`1 ``): 27 of 292 patch rows, one of them a real
change, while still reporting "0 unresolved". Rank the CHANGED rows and hand them to review agents in
batches of six to ten with the diff, the TAOM override and the question "does TAOM re-implement a
term that moved". **Full-replacement GameModel overrides first**: an additive override (`base.`
then TAOM on top) inherits a body change for free, a replacement inherits nothing, and the two are
indistinguishable to a signature gate. v1.5.2 calibration: 51 changed bodies, one drift
(`TaomPartyWageModel.GetTroopRecruitmentCost`, a replacement still carrying the v1.4.8 perk
shape), one abstract-new patch target that `HarmonyPatchBindingTests` had passed because an
abstract method resolves by name. Record the verdicts in `docs/migration/v<ver>-diff-ranked.md`.

## Phase 4 — Re-verify TAOM against the new engine

1. **`/verify-bindings`**: every Harmony patch / GameModel / reflection site, and the prefab gates
   in the same category. `PrefabCloneWidgetReferenceTests` is the one to read when a vanilla prefab
   moved: a TAOM clone REPLACES the vanilla file, so a widget reference the engine adds (v1.5.0
   `BloodFeudIconWidget`, v1.5.3 the ship banners) is a null the widget dereferences on frame one.
   Re-base the clone on the installed file, re-apply TAOM's edits. Two things about the
   snapshot half (`pwsh tools/snapshot_api_surface.ps1`): it reads `TAOM.dll` out of
   `TAOM.Tests/bin/{Debug,Release}/net472` and **throws if TAOM.Tests has not been built**, so build
   first; and `-Check` reports **DRIFT against the committed files by design** — the generator emits
   no backlinks footer while `tools/build_backlinks.py` appends one to every doc, so `-Check` only
   returns 0 in the window between regenerating and restoring backlinks. That drift is the footer
   alone. If it names anything above the `---`, the API surface genuinely moved.
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
     standalone set won't inherit → CTD on first use (the 1.3→1.4.6 dwarf water-CTD: 423 types had
     drifted). For each gap, fix with
     `python tools/patch_dwarf_action_parity.py --target <action_sets.xml> --set-id <id> --apply`.
     **`--target` is `required=True`** — the command previously written here omitted it and argparse
     rejects it outright — and it patches ONE file per run, so run it twice: the live
     `<game>\Modules\LOTRLOME_Armory\ModuleData\action_sets.xml` and the tracked
     `docs/reference/lotrlome-armory-snapshot/action_sets.xml`. The audit confirmed
     `as_dwarf_warrior` was the only humanoid gap (1110 humanoid sets, 0 others). Creature mounts
     (spider/elephant/chariot) use a separate surface → `audit_mount_parity.py`.
   - **Structure.** Flags any root-level `<action>` — one parented by `<action_sets>` instead of an
     `<action_set>`. Build 1.4.7.117484 tolerates these silently; build 117131, which TaleWorlds'
     DEDICATED SERVER engine ships, throws `KeyNotFoundException` in
     `MBObjectManager.MergeElements` at schema path `/action_sets/action` and dies on boot. A bump is
     exactly when this bites — the byte-identical file one build accepts, the next can reject, with
     no change of ours in between, and no single-player run reproduces it. Re-check the merge path
     per bump rather than assuming: 1.4.8 moved nothing here, `TaleWorlds.ObjectSystem` (which holds
     `MBObjectManager.MergeElements`) being byte-identical 1.4.7 → 1.4.8 at 62,632 B. Fix with
     `python tools/oneoff/fix_orphaned_tavern_conversation_actions.py --apply` (idempotent; rewrites
     the live Armory file and the tracked snapshot together). Found live at 168 stray elements —
     twelve `as_<race>_female_villager_in_aserai_tavern` sets authored self-closing, orphaning the 14
     female-conversation overrides that belong nested inside each.
4. Vanilla data rescan per `.claude/rules/vanilla-data-comparison.md` (scene renames/removals,
   XML re-schemas) + check which vanilla `ModuleData` XMLs the update actually touched
   (timestamp filter).
5. Update the pin: write the new version into `.claude/pinned-game-version.txt` (the file
   `session-start.sh` reads; note it's `.claude/`, NOT `.claude/state/`).
6. The `Target:` line in AGENTS.md (`lint_docs.py` checks it against the pin), plus the
   memory resume card if one tracks the bump.
7. Every other pin the version feeds, each gated or found by grep:
   - `Main/_Module/SubModule.xml` Native `DependedModuleMetadata` to `v<new>.*`
     (`NativeConstraint_MatchesPinnedGameVersion`), and the same row in the live
     `TAOM_Map/SubModule.xml` and `LOTRLOME_Armory/SubModule.xml` (unversioned: back up first).
   - `GameReferences.targets` `BannerlordRefAsmVersion` to BUTR's build for the new changeset
     (`BannerlordRefAsmVersion_PinnedGameVersion_IsTheSameGameBuild`, which CI runs too). Check the
     NuGet flat-container index for `bannerlord.referenceassemblies.core`; BUTR can publish hours
     after Steam, so re-check before calling it blocked.
   - The per-build native address pins, which SKIP rather than fail on an unknown build: the
     `KnownBuilds` table in `ClipBudgetSignatureInstalledBinaryTests`, `PINS` in
     `tools/tests/test_native_decompile.py` and the `pins` table in
     `tools/tests/test_native_engine_methods.py` (run those two with `TAOM_GHIDRA_IT=1`). Add a row
     per build; derive engine methods with `native_engine_methods.load_or_build`.
   - The category tree (`decompile_to_folder.ps1 -Destination _categories_v<new>`) and the three
     handbook-gate references to it (`tools/check_handbook_attributes.py`,
     `tools/handbook_attribute_manifest.json`, its test).
   - `git grep -n "v<old>"` over README.md, `docs/modding/` and `docs/reference/` for statements of
     the current version.
   - The hand-copied engine id sets, which stay green when the engine drops an id: the 32 policy ids
     in `KingdomPolicyIdsTests.EnginePolicyIds` (re-read `DefaultPolicies.RegisterAll`, #756) and
     `ENGINE_REGISTERED_ITEMS` in `tools/validate_moduledata.py` (`DefaultItems`).

## Phase 5 — Control battles before believing anything

Vanilla-only → warg → each TAOM creature, charging and meleeing. **On any CTD, compare the
Windows Event Log fault offset against the previous version's known sites BEFORE assuming your
last change caused it** (`/native-crash-triage` Phase 1) — the rewritten native lookups in new
engine versions turn previously-tolerated data misses into AVs
(memory: `feedback_engine_lookup_total_key_coverage`).

Worked example of the whole protocol: `docs/features/spider.md` "The v1.4.6 engine-bump
campaign" + CHANGELOG 2026-06-12.
