# Release Process — module versions and git tags

> How a TAOM build gets a version, why that version must be a git tag, and how to turn a player's
> crash report back into a commit. Skill: `/release`. Guard: `.claude/hooks/check-version-tagged.sh`.

## Why this exists

The version a player sees comes from one place: `<Version value="v2.0.18" />` in
[`Main/_Module/SubModule.xml`](../../Main/_Module/SubModule.xml). At runtime
[`IdentityCollector`](../../Main/Features/CrashReport/Collectors/IdentityCollector.cs) reads it via
`ModuleHelper.GetModuleInfo("TAOM")?.Version` and stamps it into every crash bundle as
`TaomVersion`. When a player reports a CTD, that string is the link between their report and our
source (bundles written since plan 017 also carry the build stamp, which names the commit; see
"Resolving a crash report to a commit" below).

Until 2026-08-08 that link went nowhere. The repo had two tags, neither a release
(`crafting-tool-v1.0`, `archive/master-pre-1.4.5-promotion`), and `git describe` read
`crafting-tool-v1.0-492-gd9817f89`. Worse, five versions players ran were never committed at all —
including `v2.0.12`, which appears in two crash reports in [`CHANGELOG-2026-H2-handwritten.md`](../changelog-archive/CHANGELOG-2026-H2-handwritten.md)
(a Rhûn notable CTD and the Nan Angren deserters CTD). Those reports cannot be pinned to a commit,
or even to a range, because git has never seen the version they name.

## The contract

**`Main/_Module/SubModule.xml`'s `<Version>` changes only in a release commit, and that commit is
tagged `vX.Y.Z` and pushed immediately.**

Two consequences, both load-bearing:

- A version string in a crash report is a lookup key. `git show v2.0.18:<path>` reconstructs
  exactly what shipped.
- A build whose version was never committed cannot go out — that is the `v2.0.12` failure.

Tag names are plain `v2.0.19`, matching `SubModule.xml` byte-for-byte. Non-release tags keep their
own namespaces (`archive/…`, `crafting-tool-…`), so `git describe --tags --match 'v[0-9]*'` isolates
releases deterministically.

Tags are **annotated** (`git tag -a`), never lightweight — an annotated tag is a real git object
carrying a tagger, a date and a message, and `git describe` prefers it. **Never move a pushed tag.**
Anyone who already fetched it keeps the old target silently. If a release is wrong, cut a new
version.

## The three version fields

A release bumps up to three fields, and the pairing between the last two is issue #371 — the failure
that shipped bind-posed characters to players:

| File | Field | When |
|------|-------|------|
| [`Main/_Module/SubModule.xml`](../../Main/_Module/SubModule.xml) | `<Version value="v2.0.X" />` | Every release |
| [`Dependencies/_Module/SubModule.xml`](../../Dependencies/_Module/SubModule.xml) | `<Version value="v2.0.Y" />` | Only when the Dependencies assembly changed |
| [`Main/_Module/SubModule.xml`](../../Main/_Module/SubModule.xml) | `<DependedModuleMetadata id="TAOM.Dependencies" … version="v2.0.Y" />` | **Must equal the line above** |

The third line exists because BUTR/BLSE launchers read `DependedModuleMetadatas` and the vanilla
launcher does not — the in-file comment above it spells this out. A stale pairing lets a new TAOM
load against an old Dependencies, Harmony/UIExtenderEx types fail to resolve at the member level,
and every character renders in bind pose. `/release` asserts the two `v2.0.Y` values match; do not
bump one by hand.

Assembly identity is separate and deliberately static: `Directory.Build.props` freezes
`AssemblyVersion` (changing it alters binding identity for no benefit) and stamps
`InformationalVersion` as `build.yyyyMMdd-HHmmssZ` per build, which both modules log at startup so a
mismatched pair is one line in the log. The .NET SDK appends `+<commit SHA>` to that stamp, and a
build of a tree with uncommitted changes to its inputs appends `.dirty` after the SHA (`nogit` or
`.nogit` when git could not tell). That stamp identifies a *build*; the tag identifies a
*release*. Both are needed.

## Cutting a release

Use `/release`. It runs the sequence below and fails closed on the #371 pairing check.

1. Tree clean (`git status --porcelain` empty; if another session's edits are present, stop, because `build.ps1` compiles and deploys every file in the tree, committed or not; git refuses a second worktree on a branch that is already checked out, so only the Phase 8 build moves to a detached worktree of the tag),
   on the release branch (`bannerlord-1.5.x` since v2.0.29; `bannerlord-1.4.5` for a 1.4.8 build), current version
   already tagged.
2. `./build.ps1 -RunTests` green — no release on an unrun build.
3. `pwsh tools/sweep_module_backups.ps1` reports 0 files. If it does not, run it with `-Apply`:
   backup sidecars must not ship, because `.bak` breaks the Cloudflare distribution. The first run
   found 781 of them, 937 MB. See [module-backup-sweep](module-backup-sweep.md).
4. Bump the version fields above.
5. Generate the CHANGELOG section: `python tools/changelog_from_commits.py --version vX.Y.Z --write` (every non-merge commit since the previous tag, subject and body verbatim, grouped by type). Its summary names the commit the range ends at.
6. Write `docs/releases/vX.Y.Z-discord.md` from that section (shape: [`v2.0.15-discord.md`](../releases/v2.0.15-discord.md)).
7. Confirm `HEAD` is still the commit step 5 ended at (if not, restore `CHANGELOG.md` and repeat step 5), then commit `chore(release): vX.Y.Z - TAOM vX.Y.Z`, staging release paths explicitly. Every other
   commit carries the CURRENT version the same way (`<type>: vX.Y.Z - <description>`, user rule
   2026-09-13, hook `check-commit-subject-version.sh`), so between releases
   `git log --grep 'vX.Y.Z - '` lists the commits a build reporting that `TaomVersion` can contain.
8. Confirm `git rev-parse <release commit>^` prints the commit step 5 ended at (if not, stop and ask), then `git tag -a vX.Y.Z <release commit> -m "…"`, tagging the step 7 commit by SHA, then `git push origin <release branch> vX.Y.Z`.
9. Build at the tag and gate the DLLs: `python tools/package_release.py --source "<game>/Modules" --dest <out> --require-build vX.Y.Z --dry-run` must print `build stamp OK`, then package without `--dry-run` (the skill's Phase 8).
   The gate reads every `bin/<platform>/` copy of `TAOM.dll` and `TAOM.Dependencies.dll` and
   refuses a tag whose `Directory.Build.props` predates the `.dirty` flag (the 1.4.5 line until it
   is ported). It proves the DLLs only. Deploys never delete, so the install also holds files from
   every earlier deploy. Before packaging, prune only what neither the tag nor its build owns:
   - **`<game>/Modules/TAOM/` outside `bin/`:** remove what `Main/_Module/` does not hold at the
     tag (`git ls-tree -r --name-only vX.Y.Z -- Main/_Module`). Compare paths
     case-insensitively, as Windows resolves them: the tag spells `GUI/PreFabs/`, the install
     `GUI/Prefabs/`, and an exact comparison deletes every live prefab. Leave
     `RuntimeDataCache*` alone: the packager already excludes it unless `--keep-rdc` asks for it.
   - **`<game>/Modules/TAOM.Dependencies/` outside `bin/`:** prune nothing. MCM's UI assets
     (`AssetPackages/`, `EmAssetPackages/`, `GUI/`, `ModuleData/Languages*/`) exist in the install
     only, and no build step recreates them
     ([module-dependencies.md](../modding/module-dependencies.md), "Five folders").
   - **`bin/<platform>/` of both modules:** keep a file whose name the tag tracks under
     `_Module/bin/` (`git ls-tree -r --name-only vX.Y.Z -- Main/_Module/bin Dependencies/_Module/bin`:
     2 files for TAOM, 44 for TAOM.Dependencies) or the tag's build writes. The build writes
     `TAOM.dll`, `TAOM.pdb`, `DryIoc.dll`, `Newtonsoft.Json.dll` and
     `System.Runtime.CompilerServices.Unsafe.dll` into TAOM, and `TAOM.Dependencies.dll`,
     `TAOM.Dependencies.pdb`, `0Harmony.dll`, `Bannerlord.UIExtenderEx.dll`, `MCMv5.dll` and
     `System.Runtime.CompilerServices.Unsafe.dll` into TAOM.Dependencies. That is each project's
     `bin/Debug/net472/` output, every runtime DLL its packages bring in (the other packages are
     compile-only or carry none). The build then mirrors `Win64_Shipping_Client` into `_Server` for
     both modules, and into `_wEditor` for TAOM only. Remove any other file; `.pdb`, `.exp` and `.lib` may stay, since the packager
     never ships them. A retired binary such as `BehaviorTreeWrapper.dll` would otherwise ship.

**The Armory ships in the same release when the TAOM build needs a file it did not have.** Players get
`LOTRLOME_Armory` only from the editor package Mike builds into `E:\LOTRAOM_Releases\<channel>\Modules\`. Since #627
the TAOM build instantiates `taom_howdah_platform` from `LOTRLOME_Armory/Prefabs`; a TAOM build released without an Armory
package carrying it logs `not found` and spawns no howdah platform. Since the same issue the Harad elephant rider
(`troops_harad.xml`) wears `sk_elephant_armor_howdah_elite`, an item only the Armory defines (`LOTRAOM_horses.xml`):
without it the rider's elephant spawns with no harness, no howdah and no crew.

**Step 8 is the one that gets skipped**, which is why
[`check-version-tagged.sh`](../../.claude/hooks/check-version-tagged.sh) reminds at turn end
whenever the version in `SubModule.xml` has no tag pointing at any commit. That single condition
catches both a bump committed without a tag and a version that never entered git.

`git push` does not push tags. The tag needs its own refspec, or `--follow-tags`.

## Resolving a crash report to a commit

Given `TaomVersion: v2.0.15.0` in a bundle (the engine renders `v2.0.15` with a fourth component):

```bash
git show v2.0.15                              # the release commit
git show v2.0.15:Main/_Module/SubModule.xml   # exactly what that build declared
git log v2.0.15..v2.0.18 --oneline            # everything that changed after it
git describe --tags --match 'v[0-9]*' <sha>   # which release a given commit is after
```

If the version is one of the five phantoms below, stop — there is nothing to find.

A bundle's `report.txt` (Identity section, `Build:` line) and `manifest.txt` (`TAOM build:` line)
also carry the build stamp, for example `v2.0.0.0 build.20260923-184249Z+c79a585218ad...`. That SHA
is the commit the DLL was compiled from: `git show <sha>`. A `.dirty` suffix means the build also
held uncommitted edits, so the commit is only the nearest known state; `nogit` means git could not
tell. Bundles written before this field existed lack the line: read the `[BuildStamp]` line near the
top of the bundled `taom_debug.log` instead.

## Historical record: the backfill (2026-08-08)

Eleven `v2.0.x` tags were created retroactively at the commit that **introduced** each version.
Each was gated on reading `<Version>` back out of `Main/_Module/SubModule.xml` at that commit, and
backdated (`GIT_COMMITTER_DATE`) to the commit's author date. A tag marks where a version *began*;
the version then held until the next bump, so it is the release-cut anchor, not proof that only that
commit shipped under the name.

| Tag | Commit | Date | Window ends |
|-----|--------|------|-------------|
| `v2.0.0` | `a1d45ae5` | 2026-05-23 | v2.0.2 |
| `v2.0.2` | `773dc8c2` | 2026-05-26 | v2.0.4 |
| `v2.0.4` | `ae5205c0` | 2026-05-31 | v2.0.5 (same day) |
| `v2.0.5` | `121e972b` | 2026-05-31 | v2.0.7 |
| `v2.0.7` | `8c5c909f` | 2026-06-19 | v2.0.8 |
| `v2.0.8` | `0445a3ae` | 2026-06-30 | v2.0.9 |
| `v2.0.9` | `9286814c` | 2026-07-03 | v2.0.10 |
| `v2.0.10` | `7b7a8dce` | 2026-07-07 | v2.0.13 |
| `v2.0.13` | `777411cc` | 2026-07-13 | v2.0.15 |
| `v2.0.15` | `54667df3` | 2026-07-30 | v2.0.18 |
| `v2.0.18` | `e396263d` | 2026-08-04 | v2.0.20 |
| `v2.0.20` | `094ff0a8` | 2026-08-09 | (current) |

`v2.0.20` was added on 2026-08-12, three days after its bump, on the same terms as the eleven above
(gated on reading `<Version>` back out at that commit, backdated to the commit's author date). It is
a second-generation instance of the same failure, not a leftover from the original sweep: the bump
was a bare `fix(module)` commit rather than a `/release` run, so Phase 1's "current version is
already tagged" pre-flight never executed. `check-version-tagged.sh` did catch it 60 seconds later,
then muted itself per-version and stayed silent for 33 commits. `session-start.sh` now re-asserts the
check every startup so a single missed warning cannot go quiet again.

`v2.0.19` was skipped outright and exists in no commit and no tag.

The pre-2.0 line (`v0.1.0`, `v1.0.0`–`v1.0.3`) was deliberately not tagged — no crash report will
ever be triaged against it.

## The five phantom versions — unresolvable, do not guess

`v2.0.11`, `v2.0.12`, `v2.0.14`, `v2.0.16`, `v2.0.17` **appear in no commit on any branch.**
Verified by reading `Main/_Module/SubModule.xml` at every commit that ever touched it and collecting
the distinct `<Version>` values; the complete set is `v0.1.0`, `v1.0.0`–`v1.0.3`, `v2.0.0`, `.2`,
`.4`, `.5`, `.7`, `.8`, `.9`, `.10`, `.13`, `.15`, `.18`, `.20`.

(`.20` postdates the 2026-08-08 sweep that produced this list and was appended on 2026-08-12. The
phantom set itself is unchanged at five; `.19` never existed, so it is a skipped number rather than a
sixth phantom.)

`v2.0.12` is the one that matters — two player crash reports cite it. A build went out carrying a
version string that was set outside git. **A triage session that meets one of these five should
record "version not in history" and fall back to other evidence** (the `TaomDllSha1` in the bundle,
the engine version, the reported date) rather than inventing a commit range.

`v2.0.6` is not a phantom: it is the `TAOM.Dependencies` module's version, not Main's.

## Related

- [`doc-lookup.md`](doc-lookup.md) — task index
- [`completion-workflow.md`](../ai-includes/completion-workflow.md) — the per-feature ship sequence `/release` sits downstream of
- [`dr3-maintenance.md`](../migration/dr3-maintenance.md) — updating the bundled BUTR stack, which is what forces a Dependencies bump

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/modding/editing-safely.md](../modding/editing-safely.md)
- [docs/modding/module-dependencies.md](../modding/module-dependencies.md)
- [docs/modding/module-taom.md](../modding/module-taom.md)
- [docs/modding/modules-overview.md](../modding/modules-overview.md)
- [docs/modding/recipe-new-mod-from-zero.md](../modding/recipe-new-mod-from-zero.md)
- [docs/reference/doc-lookup.md](./doc-lookup.md)
- [docs/reference/module-backup-sweep.md](./module-backup-sweep.md)

<!-- backlinks-end -->
