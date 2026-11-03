---
name: release
description: "Use when cutting a player release: version bump, generated CHANGELOG, release note, commit, tag, push, then rebuild at the tag and gate the packaged DLL stamps. Enforces #371 pairing."
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
2. On `bannerlord-1.5.x`, the only release line. Tag the release commit on that branch and push that branch.
3. The *current* version is already tagged: `git rev-parse -q --verify refs/tags/$(grep -o '<Version value="[^"]*"' Main/_Module/SubModule.xml | head -1 | sed 's/.*"\(.*\)"/\1/')`.
   If it is not, tag that one **first** — bumping past an untagged version manufactures another
   unresolvable phantom.
4. `git tag -l 'v*'` does not already contain the target version. **Never move a pushed tag.**
4b. No release channel already carries the target version, tagged or not:
   `grep -o '<Version value="[^"]*"' /e/LOTRAOM_Releases/*/Modules/TAOM/SubModule.xml`. A channel holding an
   untagged build of that number means a tester's crash report naming it is not your commit: take the next number
   (it happened with v2.0.31 and v2.0.33, both renumbered after the fact).
   This grep is the one read-only exception to the rule that the release folders are Mike's (his word,
   2026-11-03): read the version string and nothing else in a channel folder.
5. `git grep -l taom_test_ -- Main/_Module` prints nothing. Custom-Battle test riders show in every player's
   Custom Battle picker; delete them (with their SubModule node and their ladder and recruitment exemptions)
   before a release. The Animalia ones (#646) were removed for v2.0.31.
**Items 6 and 6a check Mike's packages, and the packages and release folders are his** (Mike,
2026-10-27: "Dont worry about the packaged or release folders!!! I will do that later"). Run them only
when he asks; otherwise list them as owed for his packaging pass in the hand-over, and never write a
release note around what a channel folder holds.
6. Every release channel's `TAOM_Map` package carries each settlement TAOM data names; Arthedain's
   `town_AN1` to `town_AN3` are the open case. The check per channel is in
   [`release-process.md`](../../../docs/reference/release-process.md) ("TAOM_Map ships in the same release").
6a. For the channel this release ships to, `BANNERLORD_GAME_MODULES="E:/LOTRAOM_Releases/<channel>/Modules" python tools/check_external_loc_coverage.py --processors-only` (PowerShell: set `$env:BANNERLORD_GAME_MODULES` first). Any exit other than 0 blocks the release: the TAOM_Map and Armory packages must carry Native's text processor ([`release-process.md`](../../../docs/reference/release-process.md), #1026).
6b. `python tools/check_mcm_default_flips.py` exits 0 before the tag: a changed MCM default under an
   unchanged name reaches no player who saved settings (rename it, or allowlist it with a reason).
7. **Owed in-game checks (never blocks):** `python tools/ingame_ledger.py` lists the issues closed since
   the last tag that still carry `triage-needs-ingame`. Tell Mike the count; Phase 5 hands the list to
   the testers. Exit 3 is a `gh` or `git` failure: report it, and note in the release note that the
   list is missing.

## Phase 2 — Verify

`/verify` (build, full test suite, Python tool tests; it never deploys). Read the exit code and the output. No release on an unrun
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
| `Dependencies/_Module/SubModule.xml` | `<Version value="v2.0.Y" />` | Every release since v2.0.29 (all four modules read one version); must change whenever the Dependencies assembly changed |
| `Main/_Module/SubModule.xml` | `<DependedModuleMetadata id="TAOM.Dependencies" … version="v2.0.Y" />` | **Must equal the line above** |

Outside the repo, set the same `v2.0.X` in the live `<game>/Modules/TAOM_Map/SubModule.xml` and
`<game>/Modules/LOTRLOME_Armory/SubModule.xml` too. Both modules are unversioned (no git), and the release
note tells players all four modules read the release number. The gate below checks only the two repo files.

**The #371 gate — do not skip.** After editing, read both files back and assert the two `v2.0.Y`
values are byte-identical:

```bash
grep -o '<Version value="[^"]*"' Dependencies/_Module/SubModule.xml | head -1
grep -o 'id="TAOM.Dependencies"[^>]*version="[^"]*"' Main/_Module/SubModule.xml
```

BUTR/BLSE launchers read `DependedModuleMetadatas`; the vanilla launcher does not. A mismatch lets a
new TAOM load against an old Dependencies, Harmony/UIExtenderEx types fail at the member level, and
every character renders in bind pose — with a file timestamp as the only evidence.

## Phase 4: generate the CHANGELOG section

`CHANGELOG.md` is written here and nowhere else; the commit body is the changelog entry (AGENTS.md
"Documentation duty"). With the version fields bumped but not yet committed, run:

```bash
python tools/changelog_from_commits.py --version vX.Y.Z --write
```

It reads every non-merge commit since the previous release tag
(`git describe --tags --abbrev=0 --match 'v[0-9]*'`), groups the labelled ones by type with subject
and body verbatim, lists the commits without the version label in a last group, and inserts
`## vX.Y.Z (<today>)` above the previous release's section. Read its stderr summary
(`N commits, L with the version label, U without; ending at <sha>`), note that SHA for Phases 6
and 7 (the release commit's parent must be that commit), and read the unlabelled group before
writing the release note. Exit 2 means it refused;
show the user the message, and never delete a hand-written heading without their OK.

## Phase 5: release note

`docs/releases/vX.Y.Z-discord.md`, following `docs/releases/v2.0.15-discord.md`: emoji section
headers, player-facing framing (what changed for them, not which class was refactored), and an
explicit ⚠️ line whenever MCM-persisted settings mean **existing players keep old values** and must
reset them by hand.

First, before writing a line of either note, run
`python tools/ingame_verdict.py --owed --sessions 10` and read its output
([ingame-verdicts.md](../../../docs/features/ingame-verdicts.md)). It never blocks the release. Ten
sessions let a two-session check such as #1011 pass, and per check the newest PASS or FAIL wins;
#981 also needs `--ds-data` and `--ds-engine`, so after a staged-server run re-run it with those
flags. A FAIL (exit 1) is evidence against the logged build the output names: name it to Mike before
Phase 6, because holding the release for it is his call. Exit 3 (`git` missing or `gh` failed) means
the testers' checklist says the verdicts are missing. Every owed check that is not PASS goes into
the testers' checklist below, one line each: the issue, the check id, its verdict and its `needs`
text, FAIL lines first. They are developer text, so they stay out of the Discord note.

Source the content from the section Phase 4 just wrote into `CHANGELOG.md`. It runs to thousands
of lines, so list it first: `awk '/^## v/{n++} n==1 && /^###/' CHANGELOG.md` prints its group and
subject lines only. Open the bodies you need (`git show -s --format=%b <sha>`), and grep the
section for `MCM` before writing the persisted-settings line.

Then the testers' checklist, for the testing channel only (issue titles are developer text, so they
stay out of the Discord note): `python tools/ingame_ledger.py --since <previous tag> --format md`
into `docs/releases/vX.Y.Z-testers.md`, under a first line naming the release, followed by an
"Owed checks not PASS" section with the verdict lines from the run above. Stage it with the
release note in Phase 6. A tester who confirms a check closes the loop: remove the label from that
issue.

## Phase 6 — Commit

First, `git rev-parse HEAD` must print the SHA Phase 4 ended at. If another commit landed since,
the section misses it: `git checkout -- CHANGELOG.md` and run Phase 4 again.

Stage **explicitly** with `git add <paths>`, never `-A`: the Phase 3 version files, `CHANGELOG.md`
(Phase 4), the release note and the testers' checklist (Phase 5). A shared file routinely holds two sessions' edits.

```
chore(release): vX.Y.Z - TAOM vX.Y.Z
```

The label is the NEW version, the one this commit writes into `SubModule.xml`; the
`check-commit-subject-version.sh` hook reads the staged copy, so it agrees.

## Phase 7 — Tag and push (the step that gets skipped)

First, `git rev-parse <release commit sha>^` must print the SHA Phase 4 ended at. If it does not,
a commit landed between the Phase 6 check and the commit, and it is in no section: stop and ask
the user before tagging anything.

```bash
git tag -a vX.Y.Z <release commit sha> -m "TAOM vX.Y.Z

<one-line summary>. Release notes: docs/releases/vX.Y.Z-discord.md"
git push origin <release branch> vX.Y.Z
```

Tag the Phase 6 commit by its SHA, not `HEAD`: a commit another session lands after it belongs
to the next release's section, and tagging it here would drop it from both.
Annotated (`-a`), never lightweight. **`git push` does not push tags** — the tag needs its own
refspec. Then confirm it landed:

```bash
git ls-remote --tags origin | grep vX.Y.Z          # expect the ref and its ^{} peel
git describe --tags --match 'v[0-9]*' <release commit sha>   # expect vX.Y.Z
```

## Phase 8: Build and package at the tag

The Phase 2 build ran before the release commit existed, so its DLL carries the parent commit's SHA.
Rebuild at the tag before anything ships.

1. `git status --porcelain` is empty and `git rev-parse HEAD` equals `git rev-parse vX.Y.Z^{commit}`.
   Then run `./build.ps1`. If another session has started editing, do not build that tree: build a
   clean worktree of the tag instead (`git worktree add ../taom-release-vX.Y.Z vX.Y.Z`, run
   `./build.ps1` there, then `git worktree remove ../taom-release-vX.Y.Z`). `build.ps1` compiles and
   deploys every file in the tree, committed or not.
2. Gate the DLLs: `python tools/package_release.py --source "<game>/Modules" --dest <out> --game-root "<game>" --require-build vX.Y.Z --dry-run`
   must print `build stamp OK` and exit 0. It reads every `bin/<platform>/` copy of `TAOM.dll`,
   `TAOM.Client.dll` and `TAOM.Dependencies.dll` and refuses any copy whose stamp says `.dirty` or
   `nogit`, or names a commit other than the tag's; a requested TAOM or TAOM.Dependencies missing from
   `--source`; and a tag whose `Directory.Build.props` predates the `.dirty` flag. Every run also
   refuses a `TAOM.Client.dll` in a `bin/Win64_Shipping_Server/` (ADR-013).
   Its scene shader cache, module shader sack and `JIT optimization` sections are reports that
   never refuse: a scene without a sack ships as it is (Mike's call), JIT optimization OFF is
   expected (Debug builds ship on purpose), `ON (no DebuggableAttribute on the assembly)` means the
   DLL has no attribute to read, `unknown (<reason>)` means the DLL has no readable .NET metadata
   or a malformed attribute, and a `WARNING` names a sack out of step with the other shipped
   sacks. The module sack section also states a policy: every `<module>/Shaders/D3D11` sack it
   lists is left out of the copy, whatever the module. Once Mike's test shows the game uses a
   Kit-built sack, TAOM's and the Armory's may ship; TAOM_Map's never does. Read them with
   [release-process.md](../../../docs/reference/release-process.md) step 9.
   Prune per [release-process.md](../../../docs/reference/release-process.md) step 9; the dry run refuses what remains.
3. Package: **Mike packages through the Modding Kit editor, not Claude** (2026-09-28). Stop after the
   prune and hand over; never write a package with the command above. When he says the packages and
   release folders are his for this release (as on 2026-10-27), stop after step 1 and prove the
   stamps on the DLLs the build deployed instead of the step 2 dry run: every `TAOM.dll`,
   `TAOM.Client.dll` and `TAOM.Dependencies.dll` under `<game>/Modules/{TAOM,TAOM.Dependencies}/bin`
   must read `+<tag commit>` with no `.dirty` (`[System.Diagnostics.FileVersionInfo]::GetVersionInfo(<dll>).ProductVersion`).
   Hand the prune, the sweep `-Apply`, gates 6 and 6a and steps 4 and 5 to him as a list; a
   `Gaming.Desktop.x64_Shipping_Client` folder left in the install by an older build is one of them.
4. Once his editor package exists (in `E:\LOTRAOM_Releases\<channel>\Modules\`), offer step 2's dry
   run with `--source` pointing at that folder (and `--game-root "<game>"`) before he uploads it.
   It refuses an untracked TAOM file there too, but never `AssetPackages/pack0.tpac`: in a channel
   folder that is the TAOM UI package, so do not delete it.
5. ShaderCacheKeeper, which ships beside the modules: at the tag, `python Native/ShaderCacheKeeper/build.py`
   and `pwsh -File Native/ShaderCacheKeeper/test/run_tests.ps1` both exit 0. Offer to copy
   `Native/ShaderCacheKeeper/out/xinput9_1_0.dll` to `E:\LOTRAOM_Releases\<channel>\bin\Win64_Shipping_Client\`
   before the launcher manifest is generated, and put its SHA-256 in the release note. Patreon and public
   only after its in-game check has passed on testing
   ([shader-cache-keeper.md](../../../docs/features/shader-cache-keeper.md)).

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
