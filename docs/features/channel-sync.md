# Channel sync

## Overview

`tools/diff_release_channel.py` compares the live install's `Modules` folder with a release channel
(`E:\LOTRAOM_Releases\<channel>\Modules`) and lists every file that is new, changed, stale or junk,
grouped by module and directory. The `/channel-sync` skill wraps it: diff, DLL stamp gate, apply only
the groups Mike approves, then the channel checks from [release-process.md](../reference/release-process.md).

## Why This Exists

- **Before:** each release, someone compared the install and a channel by hand. Nothing named which
  files changed, and junk rode along: on 2026-11-19 `public` held 176 `.bak*` sidecars and a 13.5 MB
  `TAOM.Dependencies/diag.log`.
- **Requirement:** live is the ground truth, except for the files `package_release.py` never ships;
  work in progress in live must not reach players; nothing in a channel is deleted.
- **Without it:** a blind copy ships WIP art (the #1474 elephant tpacs on 2026-11-19), or DLLs of a
  later commit under an old version number.

## Architecture

### Design Challenge

Live and the channel are not the same shape. Live holds editor files (`RuntimeDataCache`,
`AssetSources`) and work in progress; a channel holds Kit output live lacks (TAOM's packed UI in
`AssetPackages/pack0.tpac`). Paths differ in case (`GUI/PreFabs` and `GUI/Prefabs`).

### Solution Approach

- Exclusions come from `package_release.classify()`, imported, so the packager and the diff cannot
  disagree about what ships.
- Contents: size first; a same-size file whose mtime differs by more than 2 s is settled by SHA-256.
  `--apply` copies with `shutil.copy2`, so a synced file keeps the live mtime and reads SAME next run.
- Paths match case-insensitively, and a copy reuses the channel's existing directory casing.
- Groups: `<module>/<directory, up to 3 levels>/<new|changed>`, so one creature or one scene is one
  group. Channel-side groups: `<module>/set-aside/<rule>` and `<module>/set-aside/channel-only`.
- The four BUTR alias stubs compare with the repo's `Stubs/<Id>/_Module/SubModule.xml`, as the packager does.
- `--delete-junk` lists, and with `--apply` deletes, a channel's backup sidecars, `SceneObj/Backups`,
  `SceneEditData/Backups` and `Prefabs_Unused/` files (`DELETE_RULES`), prunes the folders that leaves
  empty, and writes the deleted paths to `E:\LOTRAOM_Releases\_set-aside\<channel>-<stamp>-deleted.txt`.
  These never ship and are deleted from every channel, not quarantined (Mike, 2026-11-19).
- `--apply` refuses without `--groups`, refuses a `bin/` group without `--allow-bin`, and moves every
  replaced or removed channel file to `E:\LOTRAOM_Releases\_set-aside\<channel>-<stamp>\` with a `moves.json` log.

## Configuration

None. Inputs: the channel's `Modules` folder (positional), `--live` (default: the Steam install),
`--modules`, `--no-stubs`, `--json`, `--apply`, `--groups`, `--allow-bin`.

## Key Files

| File | Purpose |
|------|---------|
| `tools/diff_release_channel.py` | Diff, grouping, apply with set-aside |
| `tools/package_release.py` | `classify()`, `STUB_MODULES`, `STUBS_ROOT`, the DLL stamp gate |
| `.claude/skills/channel-sync/SKILL.md` | The procedure, gates and channel checks |

## Dependencies

- `tools/package_release.py` (exclusion rules, stub list, the `--require-build` DLL gate)

## Tests

- `tools/tests/test_diff_release_channel.py`: 25 tests on 2026-11-19 (`python -m pytest tools/tests/test_diff_release_channel.py`) covering every verdict, each exclusion rule,
  scene sacks, `--delete-junk` (dry run and delete), case-insensitive paths, the hash fallback, grouping, stubs, apply with set-aside,
  the `--groups` and `--allow-bin` refusals, re-run after apply, and the CLI exit codes.

## How to verify in game

**Preconditions:** none; this is release tooling, and nothing changes in game.

1. Run `python tools/diff_release_channel.py E:/LOTRAOM_Releases/public/Modules`. Pass: exit 1 with a
   group list, or exit 0 when the channel matches live.
2. After an `--apply`, re-run the same command. Pass: the applied group ids are gone.

## How to sync a channel

1. Run `/channel-sync <channel>`.
2. Approve groups from its report.
3. Let it apply them and run the channel checks.
4. Package the launcher manifest and zip as usual.

## Changelog

- 2026-11-19: `--delete-junk`. First run deleted 234 files from `public` (176 `.bak*`, 58
  `Prefabs_Unused`), 58 from `testing` and 70 from `patreon` (all `Prefabs_Unused`). Each channel's
  `manifest.json` still listed the `Prefabs_Unused` files until regenerated.
- 2026-11-19: first version (#1480). First run on `public`: 50 groups; the live DLLs were a clean
  build of trunk 362 commits after the `v2.0.40` tag, so their groups wait for a release.

## GitHub Issue

- **Issue:** #1480: [Tool and skill: diff the live install against a release channel](https://github.com/haterade22/TAOM/issues/1480)
- **Status:** Open
