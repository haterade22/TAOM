---
name: release
description: "Cut a TAOM module release: bump the version fields, write the release note, commit, tag, and push. Enforces the #371 Dependencies pairing."
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
2. On `bannerlord-1.4.5`.
3. The *current* version is already tagged: `git rev-parse -q --verify refs/tags/$(grep -o '<Version value="[^"]*"' Main/_Module/SubModule.xml | head -1 | sed 's/.*"\(.*\)"/\1/')`.
   If it is not, tag that one **first** — bumping past an untagged version manufactures another
   unresolvable phantom.
4. `git tag -l 'v*'` does not already contain the target version. **Never move a pushed tag.**

## Phase 2 — Verify

`/verify` (`./build.ps1 -RunTests`). Read the exit code and the output. No release on an unrun
build (`evidence-over-claims.md` §B). If red, stop.

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

Entry under today's date. Mandatory (CLAUDE.md Documentation Requirements).

## Phase 6 — Commit

Stage **explicitly** — `git add <paths>`, never `-A`. A shared file routinely holds two sessions'
edits.

```
chore(release): TAOM vX.Y.Z
```

## Phase 7 — Tag and push (the step that gets skipped)

```bash
git tag -a vX.Y.Z -m "TAOM vX.Y.Z

<one-line summary>. Release notes: docs/releases/vX.Y.Z-discord.md"
git push origin bannerlord-1.4.5 vX.Y.Z
```

Annotated (`-a`), never lightweight. **`git push` does not push tags** — the tag needs its own
refspec. Then confirm it landed:

```bash
git ls-remote --tags origin | grep vX.Y.Z          # expect the ref and its ^{} peel
git describe --tags --match 'v[0-9]*' HEAD         # expect vX.Y.Z
```

## Gotchas

- **Backfilling an old release?** Backdate the tagger date or it claims to have been cut today:
  `GIT_COMMITTER_DATE="$(git log -1 --format=%aI <sha>)" git tag -a <tag> <sha> -m "…"`.
- **Tag names are plain `vX.Y.Z`**, matching `SubModule.xml` byte-for-byte. Non-release tags live in
  their own namespaces, so filter with `--match 'v[0-9]*'`.
- **Never retag.** Moving a pushed tag leaves everyone who fetched it on the old target, silently.
  A wrong release gets a new version.
- **Version ≠ build stamp.** `Directory.Build.props` stamps `InformationalVersion` per build
  (`build.yyyyMMdd-HHmmssZ`) and freezes `AssemblyVersion` deliberately. The stamp identifies a
  build; the tag identifies a release.
- GitHub Releases are deliberately **not** part of this flow — tag-only, by decision.
