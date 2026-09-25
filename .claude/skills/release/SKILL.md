---
name: release
description: "Use when cutting a player release: version bump, release note, commit, tag and push, then a rebuild at the tag and the packaged-DLL stamp gate. Enforces #371 pairing."
argument-hint: [version, e.g. 2.0.19]
---

# Cut a Release

Bump `Main/_Module/SubModule.xml`'s `<Version>` and anchor it with an annotated git tag, in one
sequence, so the version a player quotes in a crash report resolves to a commit.

This skill exists because two steps get silently skipped. **Tagging** — the repo ran 492 commits
with no release tag at all. And the **#371 Dependencies pairing** — a stale pairing ships bind-posed
characters to players. Full contract: [`docs/reference/release-process.md`](../../../docs/reference/release-process.md).

## When to invoke

- Cutting a build for players (Discord / Patreon / Nexus).
- The user says "bump the module version", "cut a release", "ship v2.0.x".
- **Not** for ordinary feature merges — that is `/ship`. A release is downstream of it.

## Phase 1 — Pre-flight (all must hold)

1. `git status --porcelain` is **empty**. Another session's edits must not ride along in a release
   commit (CLAUDE.md multi-session git safety).
2. On the branch the release line lives on: `bannerlord-1.5.x` since v2.0.29 (Bannerlord 1.5 players), `bannerlord-1.4.5` for a 1.4.8 build. Tag the release commit on that branch and push that branch.
3. The *current* version is already tagged: `git rev-parse -q --verify refs/tags/$(grep -o '<Version value="[^"]*"' Main/_Module/SubModule.xml | head -1 | sed 's/.*"\(.*\)"/\1/')`.
   If it is not, tag that one **first** — bumping past an untagged version manufactures another
   unresolvable phantom.
4. `git tag -l 'v*'` does not already contain the target version. **Never move a pushed tag.**
5. `git grep -l taom_test_ -- Main/_Module` prints nothing. Custom-Battle test riders show in every player's
   Custom Battle picker; delete them (with their SubModule node and their ladder and recruitment exemptions)
   before a release. The Animalia ones (#646, `troops/troops_animalia_test.xml`) are kept visible until then.

## Phase 2 — Verify

`/verify` (`./build.ps1 -RunTests`). Read the exit code and the output. No release on an unrun
build (`evidence-over-claims.md` §B). If red, stop.

Then `pwsh tools/sweep_module_backups.ps1`. It must report **0 files**. Backup sidecars must not
ship: `.bak` breaks the Cloudflare distribution. If it reports any, run it again with `-Apply`
(it moves them to a dated quarantine with a SHA256 manifest, it does not delete). Read the orphan
list before applying: a sidecar with no live sibling is a sole copy, not a backup, and the script
aborts if more turn up than `-MaxOrphans`. Detail: `docs/reference/module-backup-sweep.md`.

## Phase 3 — Bump the version fields

| File | Field | When |
|------|-------|------|
| `Main/_Module/SubModule.xml` | `<Version value="v2.0.X" />` | Every release |
| `Dependencies/_Module/SubModule.xml` | `<Version value="v2.0.Y" />` | Only if the Dependencies assembly changed |
| `Main/_Module/SubModule.xml` | `<DependedModuleMetadata id="TAOM.Dependencies" … version="v2.0.Y" />` | **Must equal the line above** |

**The #371 gate — do not skip.** After editing, read both files back and assert the two `v2.0.Y`
values are byte-identical:

```bash
grep -o '<Version value="[^"]*"' Dependencies/_Module/SubModule.xml | head -1
grep -o 'id="TAOM.Dependencies"[^>]*version="[^"]*"' Main/_Module/SubModule.xml
```

BUTR/BLSE launchers read `DependedModuleMetadatas`; the vanilla launcher does not. A mismatch lets a
new TAOM load against an old Dependencies, Harmony/UIExtenderEx types fail at the member level, and
every character renders in bind pose — with a file timestamp as the only evidence.

## Phase 4 — Release note

`docs/releases/vX.Y.Z-discord.md`, following `docs/releases/v2.0.15-discord.md`: emoji section
headers, player-facing framing (what changed for them, not which class was refactored), and an
explicit ⚠️ line whenever MCM-persisted settings mean **existing players keep old values** and must
reset them by hand.

Source the content from CHANGELOG entries since the previous tag:
`git log <previous-tag>..HEAD --format='%s'`.

## Phase 5 — CHANGELOG

Entry under today's date. Mandatory (AGENTS.md "Documentation duty").

## Phase 6 — Commit

Stage **explicitly** — `git add <paths>`, never `-A`. A shared file routinely holds two sessions'
edits.

```
chore(release): vX.Y.Z - TAOM vX.Y.Z
```

The label is the NEW version, the one this commit writes into `SubModule.xml`; the
`check-commit-subject-version.sh` hook reads the staged copy, so it agrees.

## Phase 7 — Tag and push (the step that gets skipped)

```bash
git tag -a vX.Y.Z -m "TAOM vX.Y.Z

<one-line summary>. Release notes: docs/releases/vX.Y.Z-discord.md"
git push origin <release branch> vX.Y.Z
```

Annotated (`-a`), never lightweight. **`git push` does not push tags** — the tag needs its own
refspec. Then confirm it landed:

```bash
git ls-remote --tags origin | grep vX.Y.Z          # expect the ref and its ^{} peel
git describe --tags --match 'v[0-9]*' HEAD         # expect vX.Y.Z
```

## Phase 8: Build and package at the tag

The Phase 2 build ran before the release commit existed, so its DLL carries the parent commit's SHA.
Rebuild at the tag before anything ships.

1. `git status --porcelain` is empty and `git rev-parse HEAD` equals `git rev-parse vX.Y.Z^{commit}`.
   Then run `./build.ps1`. If another session has started editing, do not build that tree: build a
   clean worktree of the tag instead (`git worktree add ../taom-release-vX.Y.Z vX.Y.Z`, run
   `./build.ps1` there, then `git worktree remove ../taom-release-vX.Y.Z`). `build.ps1` compiles and
   deploys every file in the tree, committed or not.
2. Gate the DLLs: `python tools/package_release.py --source "<game>/Modules" --dest <out> --require-build vX.Y.Z --dry-run`
   must print `build stamp OK` and exit 0. It reads every `bin/<platform>/` copy of `TAOM.dll` and
   `TAOM.Dependencies.dll` and refuses one whose stamp says `.dirty` or `nogit`, or names a commit
   other than the tag's; a requested TAOM or TAOM.Dependencies missing from `--source`; and a tag whose
   `Directory.Build.props` predates the `.dirty` flag (the 1.4.5 line until it is ported).
   It proves the DLLs only. Deploys never delete, so the install also holds files from every
   earlier deploy. Before packaging, prune only what neither the tag nor its build owns:
   - **`<game>/Modules/TAOM/` outside `bin/`:** remove what `Main/_Module/` does not hold at the
     tag (`git ls-tree -r --name-only vX.Y.Z -- Main/_Module`). Compare paths
     case-insensitively, as Windows resolves them: the tag spells `GUI/PreFabs/`, the install
     `GUI/Prefabs/`, and an exact comparison deletes every live prefab. Leave
     `RuntimeDataCache*` alone: the packager already excludes it unless `--keep-rdc` asks for it.
   - **`<game>/Modules/TAOM.Dependencies/` outside `bin/`:** prune nothing. MCM's UI assets
     (`AssetPackages/`, `EmAssetPackages/`, `GUI/`, `ModuleData/Languages*/`) exist in the install
     only, and no build step recreates them
     ([module-dependencies.md](../../../docs/modding/module-dependencies.md), "Five folders").
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
3. Package: the same command without `--dry-run` (plus `--keep-rdc` or `--allow-unknown` if the dry
   run's report calls for them).
4. If the player package is assembled somewhere else (the editor package in
   `E:\LOTRAOM_Releases\<channel>\Modules\`), run step 2's dry run with `--source` pointing at that
   folder before uploading it.

## Gotchas

- **Backfilling an old release?** Backdate the tagger date or it claims to have been cut today:
  `GIT_COMMITTER_DATE="$(git log -1 --format=%aI <sha>)" git tag -a <tag> <sha> -m "…"`.
- **Tag names are plain `vX.Y.Z`**, matching `SubModule.xml` byte-for-byte. Non-release tags live in
  their own namespaces, so filter with `--match 'v[0-9]*'`.
- **Never retag.** Moving a pushed tag leaves everyone who fetched it on the old target, silently.
  A wrong release gets a new version.
- **Version ≠ build stamp.** `Directory.Build.props` stamps `InformationalVersion` per build
  (`build.yyyyMMdd-HHmmssZ`) and freezes `AssemblyVersion` deliberately. The SDK appends
  `+<commit SHA>`, and a build of a tree with uncommitted changes (untracked files included)
  under `Main`, `Dependencies`, `Stubs`, `Directory.Build.props` or `GameReferences.targets`
  appends `.dirty` after it (`nogit` or `.nogit` when git could not tell). The stamp identifies a
  build; the tag identifies a release.
- GitHub Releases are deliberately **not** part of this flow — tag-only, by decision.
