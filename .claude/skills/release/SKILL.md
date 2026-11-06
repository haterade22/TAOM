---
name: release
description: "Use when cutting a player release: version bump, generated CHANGELOG, release note, commit, tag, push, then rebuild at the tag and gate the packaged DLL stamps. Enforces #371 pairing."
argument-hint: [version, e.g. 2.4.0]
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
2. On `bannerlord-1.4.5`.
3. The *current* version is already tagged: `git rev-parse -q --verify refs/tags/$(grep -o '<Version value="[^"]*"' Main/_Module/SubModule.xml | head -1 | sed 's/.*"\(.*\)"/\1/')`.
   If it is not, tag that one **first** — bumping past an untagged version manufactures another
   unresolvable phantom.
4. `git tag -l 'v*'` does not already contain the target version. **Never move a pushed tag.**
5. The target version follows this line's scheme: `v2.4.0`, then `v2.4.1` and so on. Never
   `v2.0.29` to `v2.0.36` (1.5.x release tags) or `v2.1.x` (tags on no branch); see
   `docs/reference/release-process.md`, "Version numbers on the 1.4.x line". Never a fourth
   component either (the public channel's hand-typed v2.0.29.5 is in no commit); Phase 8's gate
   refuses both forms.
6. `git grep -l taom_test_ -- Main/_Module` prints nothing. Custom-Battle test riders show in every
   player's Custom Battle picker; delete them (with their SubModule node and their ladder and
   recruitment exemptions) before a release.
7. Owed in-game checks (never blocks): `python tools/ingame_ledger.py` lists the closed issues still
   labelled `triage-needs-ingame` that a commit since the last tag names. Tell the maintainer the
   count; Phase 5 hands the list to the testers. Exit 3 is a `gh` or `git` failure: report it and
   say in the release note that the list is missing.

## Phase 2 — Verify

`/verify` (build, full test suite, Python tool tests; it never deploys). Read the exit code and the output. No release on an unrun
build (`evidence-over-claims.md` §B). If red, stop.

Then `pwsh tools/sweep_module_backups.ps1`. It must report **0 files**. Backup sidecars must not
ship: `.bak` breaks the Cloudflare distribution. If it reports any, run it again with `-Apply`
(it moves them to a dated quarantine with a SHA256 manifest, it does not delete). Read the orphan
list before applying: a sidecar with no live sibling is a sole copy, not a backup, and the script
aborts if more turn up than `-MaxOrphans`. Detail: `docs/reference/module-backup-sweep.md`.

Then `python tools/check_mcm_default_flips.py`. It must exit 0: a changed MCM default under an unchanged name reaches no player who saved settings (rename it, or allowlist it with a reason).

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

Source the content from the section Phase 4 just wrote into `CHANGELOG.md`. It runs to thousands
of lines, so list it first: `awk '/^## v/{n++} n==1 && /^###/' CHANGELOG.md` prints its group and
subject lines only. Open the bodies you need (`git show -s --format=%b <sha>`), and grep the
section for `MCM` before writing the persisted-settings line.

Write the testers' checklist too: `python tools/ingame_ledger.py --since <previous tag> --format md`
into `docs/releases/vX.Y.Z-testers.md`, under a first line naming the release. Issue titles are
developer text, so they stay out of the Discord note.

Add one line to the release note naming the TAOM_Map and LOTRLOME_Armory the release ships, copied
from the `modules:` line of Phase 8 step 3, plus any `PAIRING SIGNAL` it printed.

## Phase 6 — Commit

First, `git rev-parse HEAD` must print the SHA Phase 4 ended at. If another commit landed since,
the section misses it: `git checkout CHANGELOG.md` and run Phase 4 again.

Stage **explicitly** with `git add <paths>`, never `-A`: the Phase 3 version files, `CHANGELOG.md`
(Phase 4), the release note and `docs/releases/vX.Y.Z-testers.md` (Phase 5). A shared file routinely
holds two sessions' edits.

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
git push origin bannerlord-1.4.5 vX.Y.Z
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

The Phase 2 build ran before the release commit, so its DLLs carry the parent's SHA. Rebuild at the
tag. TAOM and TAOM.Dependencies always ship from this one build, even when Dependencies did not
change (#371).

1. `git status --porcelain` is empty and `git rev-parse HEAD` equals `git rev-parse vX.Y.Z^{commit}`,
   then `./build.ps1` (it compiles and deploys every file in the tree, committed or not). If another
   session is editing, build a clean worktree of the tag: `git worktree add ../taom-release-vX.Y.Z vX.Y.Z`,
   `./build.ps1` there, `git worktree remove ../taom-release-vX.Y.Z`.
2. Gate the DLLs:

   ```bash
   python tools/package_release.py --source "<game>/Modules" --dest <out> --game-root "<game>" --require-build vX.Y.Z --dry-run
   ```

   It must print `build stamp OK` and exit 0. It refuses a `.dirty` or `nogit` copy or one from
   another commit, a missing requested module, a tag that predates the dirty flag, an installed TAOM
   or TAOM.Dependencies `SubModule.xml` whose `<Version>` or DLL list differs from the tag's, and a
   release number in the 1.5.x range, with four parts or held by another line's tag. On every run it
   also refuses retired binaries, a `Gaming.Desktop.x64_Shipping_Client` bin, a shipped ModuleData
   file defining a `taom_test_` id, and an Armory shader with no source players have. Its scene sack,
   module sack and `JIT optimization` sections only report: JIT optimization OFF is expected (Debug
   builds ship on purpose, maintainer 2026-09-30), and module sacks ship on 1.4.x until a v1.4.8
   Kit-sack test says otherwise.
3. Check the pairing:

   ```bash
   python tools/check_release_pairing.py --modules "<game>/Modules" --game-root "<game>"
   python tools/validate_moduledata.py --game-modules "<game>/Modules"
   python tools/audit_armory_refs.py
   ```

   The first two must exit 0 (the validator's codes include `MISSING_COLLISION_BODY` and
   `COLLISION_BODY_BORROWED`); the audit must report no finding against the Armory the release ships.
   The desktop's live TAOM_Map, exported after 2026-10-29, carries 1.5.x ids: package TAOM_Map and
   LOTRLOME_Armory from the copies `tools/release_pins.json` pins, never from that install. A new pin
   is recorded with `--record` only after the other checks pass, and committed on its own.
4. Prune per `docs/reference/release-process.md`, "Cutting a release", step 9. The dry run refuses
   what remains.
5. The maintainer packages (an outward-facing action). Then re-run steps 2 and 3 with `--source` and
   `--modules` pointing at the channel's `Modules` folder (desktop: `E:\LOTRAOM_Releases\<channel>\Modules`)
   and `--game-root "<game>"` before the maintainer uploads it.

## Gotchas

- **Backfilling an old release?** Backdate the tagger date or it claims to have been cut today:
  `GIT_COMMITTER_DATE="$(git log -1 --format=%aI <sha>)" git tag -a <tag> <sha> -m "…"`.
- **Tag names are plain `vX.Y.Z`**, matching `SubModule.xml` byte-for-byte. Non-release tags live in
  their own namespaces, so filter with `--match 'v[0-9]*'`.
- **Never retag.** Moving a pushed tag leaves everyone who fetched it on the old target, silently.
  A wrong release gets a new version.
- **Version ≠ build stamp.** `Directory.Build.props` stamps `InformationalVersion` per build
  (`build.yyyyMMdd-HHmmssZ`) and freezes `AssemblyVersion` deliberately. The SDK appends
  `+<commit SHA>`, and a build of a tree with uncommitted changes (untracked files included) under
  `Main`, `Dependencies`, `Stubs` or `Directory.Build.props` appends `.dirty` after it (`nogit` or
  `.nogit` when git could not tell). The stamp identifies a
  build; the tag identifies a release.
- GitHub Releases are deliberately **not** part of this flow — tag-only, by decision.
