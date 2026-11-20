---
name: channel-sync
description: "Use when asked what changed between the live install and a release channel, or to update a channel. Deletes junk, diffs, gates DLLs, applies approved groups, runs channel checks."
argument-hint: [channel: public|patreon|testing]
---

# Channel sync: live install to a release channel

Lists every file that differs between the live `Modules` folder and
`E:\LOTRAOM_Releases\<channel>\Modules`, grouped so Mike approves what ships. Tool:
`tools/diff_release_channel.py` (#1480). Doc: [channel-sync.md](../../../docs/features/channel-sync.md).

## Traps (read first)

- **Channels are Mike's packages.** This skill is his "unless he asks" in
  [release-process.md](../../../docs/reference/release-process.md). Report freely; write to a channel only
  with his word in this session, group by group.
- **Live is ground truth, except what the packager never ships:** `RuntimeDataCache*`, `AssetSources/`,
  `Prefabs_Unused/`, module-level `Shaders/D3D11/compressed_shader_cache.sack`, backup sidecars,
  `.vs/`, `.pdb`, runtime logs. The tool imports `package_release.classify()` for this. Scene sacks
  (`SceneObj/<scene>/ShaderCache/D3D11/`) ship.
- **Live holds work in progress.** An uncommitted feature, an art pass awaiting Mike's look, a
  `*.bak-pre<issue>` sibling: name it beside its group. Check the In-Flight cards in MEMORY.md.
- **Never ship live DLLs under an old number.** If the live `bin/` build is not a clean build of the
  version tag, its groups wait for `/release`. A build of a later commit under the old number is a
  phantom version (release-process.md).
- **Backups and `Prefabs_Unused` never ship and are deleted from every channel** (Mike, 2026-11-19):
  backup sidecars (`.bak*`, `.backup`, `.orig`, `.old`, `.tmp`), `SceneObj/Backups` and
  `SceneEditData/Backups`, and `Prefabs_Unused/`. Phase 2 runs `--delete-junk` for this.
- **Everything else is quarantined, never deleted.** `--apply` moves every replaced or removed channel file to
  `E:\LOTRAOM_Releases\_set-aside\<channel>-<stamp>\` and logs it in `moves.json` there.
- **A channel `AssetPackages/*.tpac` that live lacks** is Kit output (TAOM's packed UI is
  `pack0.tpac`): the tool reports it as `KIT_PACKAGE` and never offers it for set-aside.
- **The tool never touches `manifest.json`.** The launcher manifest, the ShaderCacheKeeper DLL and
  the zip are Mike's steps after the sync.

## Phase 1: claim and read

1. `python tools/work_claims.py claim channel-sync-<channel> --note "<what>"`.
2. Read the channel's version, and nothing else by hand:
   `grep -o '<Version value="[^"]*"' /e/LOTRAOM_Releases/<channel>/Modules/TAOM/SubModule.xml`.

## Phase 2: delete junk, then diff (read-only)

Run for every channel, not only the one being synced (`public`, `testing`, `patreon`):

```
python tools/diff_release_channel.py E:/LOTRAOM_Releases/<channel>/Modules --delete-junk
python tools/diff_release_channel.py E:/LOTRAOM_Releases/<channel>/Modules --delete-junk --apply
```

The first line lists; read it before the second deletes. The list of deleted paths goes to
`E:\LOTRAOM_Releases\_set-aside\<channel>-<stamp>-deleted.txt`. Tell Mike to regenerate that channel's
`manifest.json`, because it still lists the deleted files.

Then the diff:

```
python tools/diff_release_channel.py E:/LOTRAOM_Releases/<channel>/Modules --json <scratchpad>/<channel>-diff.json
```

Exit 0: nothing differs, stop. Exit 1: groups listed. Exit 2: bad input. Exit 3: crash. 2 and 3 judged nothing.

Group ids are `<module>/<dir up to 3 levels>/<new|changed>`, `<module>/set-aside/<rule|channel-only>`
and `<stub>/(stub)/<new|changed>`. Report to Mike, one row per group: id, file count, newest live
date, and a one-line verdict:

- **ship**: finished, committed work (trunk XML, a Map change already in a commit body).
- **hold**: work in progress, or a DLL group that fails Phase 3.
- **set aside**: channel junk the delete step leaves (`RUNTIME_STATE`), stale channel-only files.
- **ask**: anything you cannot place (`NATIVE_DEBUG` pdbs).

More than about 15 groups: publish the rows as a filterable HTML artifact with batch answers
(`think-before-coding.md`), and keep a short chat summary.

## Phase 3: DLL gate (before any `bin` group)

```
python tools/package_release.py --source "<game>/Modules" --dest <scratchpad>/pkg --game-root "<game>" --require-build v<X.Y.Z> --dry-run
```

`build stamp OK` for every DLL: the `bin` groups may ship with `--allow-bin`. A line `built at
<sha>, but the release is <tag sha>`: the live DLLs are a later build. Hold every `bin` group and
`TAOM/(root)/changed` (`SubModule.xml`), and tell Mike a `/release` comes first. The same run lists
untracked live TAOM files; they are not in the diff's groups only if the channel already holds them,
so name any that a `new` group would carry.

## Phase 4: apply (only with Mike's word)

```
python tools/diff_release_channel.py E:/LOTRAOM_Releases/<channel>/Modules --apply --groups "<id>" "<id>" ... [--allow-bin]
```

Quote the group ids exactly. Read the `moves.json` path it prints.

## Phase 5: channel checks (every one must pass)

Run each against `E:/LOTRAOM_Releases/<channel>/Modules`; the commands and their pass lines are in
release-process.md:

1. `python tools/check_release_pair.py <channel Modules>`: `PASS:`.
2. `python tools/check_siege_wall_scripts.py --scenes <channel Modules>/TAOM_Map/SceneObj`: `PASS:`.
3. `python tools/bind_dwarf_attack_actions.py --check --armory <channel Modules>/LOTRLOME_Armory/ModuleData`: exit 0.
4. `BANNERLORD_GAME_MODULES=<channel Modules> python tools/check_external_loc_coverage.py --processors-only`: `PASS:`.
5. `python tools/validate_moduledata.py --game-modules <channel Modules> --code BROKEN_ITEM_REF`: no new rows
   against the run before the sync.
6. The packager dry run with `--source <channel Modules> --game-root "<game>"`.

A failure: report it with its output. Undo a group by moving its files back from `moves.json`.

## Phase 6: confirm and hand over

1. Re-run Phase 2. Only the held groups remain.
2. Hand Mike his steps: the launcher manifest, the ShaderCacheKeeper DLL
   (`<channel>\bin\Win64_Shipping_Client\`), the zip, and any Kit repackage.
3. Release the claim: `python tools/work_claims.py release channel-sync-<channel>`.
