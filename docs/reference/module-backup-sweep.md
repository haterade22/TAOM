# Module backup sweep: getting sidecars out of what ships

`.bak` breaks the Cloudflare distribution, so no backup sidecar may ship to players. The three
shipped modules accumulate them constantly: nearly every tool under `tools/` writes one before a
destructive edit, which is the [XML I/O convention](../../tools/README.md#xml-io-convention-mandatory-for-scripts-that-edit-moduledata-xml)
doing its job. Nothing was removing them.

`tools/sweep_module_backups.ps1` does. Dry-run by default, `-Apply` to move.

```powershell
pwsh tools/sweep_module_backups.ps1            # report only
pwsh tools/sweep_module_backups.ps1 -Apply     # move into the dated quarantine
```

## It moves, it does not delete

`LOTRLOME_Armory` and `TAOM_Map` are not git-tracked. Their sidecars were the only rollback their
live XML had, so the sweep relocates rather than deletes:

```
E:\Bannerlord_Backups\module_bak_sweep_<date>\<Module>\<original relative path>
E:\Bannerlord_Backups\module_bak_sweep_<date>\MANIFEST.csv
```

Relative paths are preserved exactly, so restoring one file is a copy back to the same place under
the module root. `MANIFEST.csv` carries `Module, Relative, Category, Suffix, Bytes, Orphan, Sha256`
for every file and is written and flushed **before** the first move, so an interrupted run is still
reconstructable.

## Two things a naive version of this gets wrong

**Do not glob `*.bak`.** Our tools write dated, topic-tagged suffixes, and those are the bulk of
them: on the first run, 260 `.bak-armoryloc`, 85 `.bak-guidremap`, 57 `.bak-preskel`. A bare
`*.bak` matched **18 of 658**. The script matches `.bak`, `.bak<N>`, `.bak-<topic>`, `.bak_<topic>`,
`.backup`, `.orig`, `.prev`, `.old`, `.tmp` and `.transplanted-<date>`, in each case only where the
suffix sits after the real extension.

**A sidecar whose live sibling is gone is not a backup, it is the sole copy.** The script strips the
suffix, tests for the live file, and reports those separately. `-MaxOrphans` (default 3) aborts an
`-Apply` run if more turn up than the survey expects, because a new orphan means a live asset
disappeared since.

## The repo `_Module` tree is the fourth root, and it is the one that gets missed

Sweeping the install is not enough on its own. `TAOM.csproj`'s `CopyModule` target **"recurses
`_Module` verbatim and deploys whatever it finds"** (its own comment, sitting right above the guard
that already stops a `.vs` folder reaching the game). So a sidecar in
`Main/_Module/ModuleData/` is redeployed into `<game>\Modules\TAOM\` on the next build, and an
install-only sweep reads clean right up until someone builds.

These files are also invisible to `git status`, because `.gitignore` line 24 covers `*.bak*`. Two
mechanisms hiding the same files in opposite directions is why this went unnoticed: git will not
show them, and the install keeps regenerating them.

The script therefore scans `Main/_Module` as a fourth root, quarantining under the label
`_repo_Main_Module`. `-SkipRepoModule` opts out, and `-RepoModuleRoot` overrides the path.

This is the same defect class the Dependencies stub glob already hit once, when
`..\Stubs\**\*.*` was tightened to `..\Stubs\**\SubModule.xml` to stop stray `.bak` and `.tmp`
files deploying to the install.

## The invariant that makes it safe: the LAST extension decides

The engine globs `GetFiles("*.xml")`. So what matters is never the presence of `.bak`, it is which
extension comes last:

| Name | Engine sees it? |
|---|---|
| `action_sets.xml.bak-wargabsorb-20260828` | No. Last extension is not `.xml` |
| `action_sets.bak.xml` | **Yes**, parsed as real data, duplicating every id in the file |

This is the same rule [lotrlome-warg-changes](lotrlome-warg-changes.md) and
[lotrlome-soln-id-fix](lotrlome-soln-id-fix.md) already called load-bearing when they picked a
non-`.xml` suffix. It also settles a contradiction between two of our own docs: the
[native commit audit](../investigations/native-commit-audit-2026-08.md) listed stale `.bak` XMLs as
a load-surface hazard causing duplicate Monster registrations, while a later CHANGELOG lesson found
the engine never loads them. Both were half right. The script asserts the invariant before it moves
anything: zero matched files may have a real game extension last.

## The 2026-09-01 run

**781 files, 937.3 MB**, all three modules.

| Module | Category | Files | Size |
|---|---|---|---|
| LOTRLOME_Armory | `Assets` | 232 | 100.6 MB |
| LOTRLOME_Armory | `ModuleData` | 372 | 21.2 MB |
| LOTRLOME_Armory | `AssetSources` | 4 | 25.3 MB |
| LOTRLOME_Armory | scene `Backups\` | 43 | 1.4 MB |
| TAOM_Map | `ModuleData` | 39 | 17.8 MB |
| TAOM_Map | scene `Backups\` | 80 | 769.4 MB |
| TAOM | `ModuleData` | 11 | 1.6 MB |

Verified: 0 sidecars remaining on a re-scan, 10 sampled files re-hashed at the destination against
the manifest with 0 mismatches, `validate_moduledata.py` PASS, `check_prefab_budget.py` OK,
`dotnet test TAOM.Tests` 7,770 passed.

### What this run moved that other docs point at

Several docs and tools name a specific sidecar as their rollback route. Those routes still work, but
the file is now one directory tree away. Restore from the quarantine, same relative path.

| Route | Suffix | Moved |
|---|---|---|
| [lotrlome-warg-changes](lotrlome-warg-changes.md) "Backups" | `.bak-wargabsorb-20260828` | 7 |
| [lotrlome-war-ram-changes](lotrlome-war-ram-changes.md) "Backups" | `.bak-warram-20260828` | 2 |
| [lotrlome-soln-id-fix](lotrlome-soln-id-fix.md) rollback column | `.bak-solnfix-20260828`, `.bak-superseded-20260828` | 3 |
| [lotrlome-spider-mount-changes](lotrlome-spider-mount-changes.md) rollback inventory | `.bak-spider-mount` (1), `.bak-untagged` (2), `.bak-canattack-146` (1), `.bak-parity-146` (1), the spider `.backup` (1) | 30 from the spider folder in all |
| `register_one_handed_polearms.py --revert` | `.bak-1hpolearm` | 1 |
| `generate_black_numenorean_armor.py --revert` | `.bak-blacknum-*` | 18 |
| [armory snapshot README](lotrlome-armory-snapshot/README.md) team-colour revert | `.bak-teamcolor` | 5 |
| [editor-cache-rebuild](../features/editor-cache-rebuild.md) `.prev` rollback | `.prev` | 1 |
| `remap_stale_scene_names.py --backup` output | `.bak_scenes` | 1 |

The `.bak_scenes` file is `TAOM\ModuleData\sp_battle_scenes.xml.bak_scenes`. The
`settlements.xml.bak_scenes` that [plans/004](../../plans/004-live-town-scene-crashes.md) asks for
beside the live `TAOM_Map` file was already gone before this run, so that checklist item is stale
for an unrelated reason and the sweep did not cause it.

A `--revert` flag on those tools will not find its backup beside the live file any more. Copy it
back from the quarantine first, then revert.

### The one that is not recoverable from anywhere else

`LOTRLOME_Armory\Assets\creature\spider\meshes\sk_spider_forest_c_geo.tpac.backup` (7.4 MB) has no
`.tpac` sibling and never did. It is the sole copy, and
[lotrlome-spider-mount-changes](lotrlome-spider-mount-changes.md) records the `spider_skeleton`
resource surviving only in it. Nothing loads it, so moving it changed no behaviour, but any future
spider asset work fetches it from the quarantine, not from the module.

## Recurring files, not just stale ones

One swept file regenerates: the editor cache rebuild feature writes
`TAOM_Map/ModuleData/DistanceCaches/settlements_distance_cache_Default.bin.prev` on every successful
run, as its documented rollback slot. It ships (9.9 MB), so a pre-release sweep should take it, but
running the sweep mid-development closes that rollback window. If you are about to rebuild the
distance cache, sweep afterwards, not before.

## When to run it

Before cutting a release, as part of [the release process](release-process.md). It is also worth a
run after any large scripted data pass, since those are what produce the sidecars in bulk.

## Related

- [`tools/README.md`](../../tools/README.md): the XML I/O convention that writes these files
- [release-process.md](release-process.md): where the sweep sits in a release
- [native-commit-audit-2026-08.md](../investigations/native-commit-audit-2026-08.md): install-weight ledger

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/editor-cache-rebuild.md](../features/editor-cache-rebuild.md)
- [docs/features/spider.md](../features/spider.md)
- [docs/features/spider/wolf-parity-and-render-tests.md](../features/spider/wolf-parity-and-render-tests.md)
- [docs/INDEX.md](../INDEX.md)
- [docs/modding/editing-safely.md](../modding/editing-safely.md)
- [docs/modding/items-armor.md](../modding/items-armor.md)
- [docs/modding/items-mounts-and-harness.md](../modding/items-mounts-and-harness.md)
- [docs/modding/items-weapons-and-crafting.md](../modding/items-weapons-and-crafting.md)
- [docs/modding/module-armory.md](../modding/module-armory.md)
- [docs/modding/module-taom.md](../modding/module-taom.md)
- [docs/modding/modules-overview.md](../modding/modules-overview.md)
- [docs/modding/recipe-retire-content.md](../modding/recipe-retire-content.md)
- [docs/reference/lotrlome-armory-snapshot/README.md](lotrlome-armory-snapshot/README.md)
- [docs/reference/lotrlome-soln-id-fix.md](./lotrlome-soln-id-fix.md)
- [docs/reference/lotrlome-spider-mount-changes.md](./lotrlome-spider-mount-changes.md)
- [docs/reference/lotrlome-war-ram-changes.md](./lotrlome-war-ram-changes.md)
- [docs/reference/lotrlome-warg-changes.md](./lotrlome-warg-changes.md)
- [docs/reference/release-process.md](./release-process.md)

<!-- backlinks-end -->
