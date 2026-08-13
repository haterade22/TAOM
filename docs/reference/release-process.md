# Release Process — module versions and git tags

> How a TAOM build gets a version, why that version must be a git tag, and how to turn a player's
> crash report back into a commit. Skill: `/release`. Guard: `.claude/hooks/check-version-tagged.sh`.

## Why this exists

The version a player sees comes from one place: `<Version value="v2.0.18" />` in
[`Main/_Module/SubModule.xml`](../../Main/_Module/SubModule.xml). At runtime
[`IdentityCollector`](../../Main/Features/CrashReport/Collectors/IdentityCollector.cs) reads it via
`ModuleHelper.GetModuleInfo("TAOM")?.Version` and stamps it into every crash bundle as
`TaomVersion`. When a player reports a CTD, that string is the only link between their report and
our source.

Until 2026-08-08 that link went nowhere. The repo had two tags, neither a release
(`crafting-tool-v1.0`, `archive/master-pre-1.4.5-promotion`), and `git describe` read
`crafting-tool-v1.0-492-gd9817f89`. Worse, five versions players ran were never committed at all —
including `v2.0.12`, which appears in two crash reports in [`CHANGELOG.md`](../../CHANGELOG.md)
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
mismatched pair is one line in the log. That stamp identifies a *build*; the tag identifies a
*release*. Both are needed.

## Cutting a release

Use `/release`. It runs the sequence below and fails closed on the #371 pairing check.

1. Tree clean, on `bannerlord-1.4.5`, current version already tagged.
2. `./build.ps1 -RunTests` green — no release on an unrun build.
3. Bump the version fields above.
4. Write `docs/releases/vX.Y.Z-discord.md` (shape: [`v2.0.15-discord.md`](../releases/v2.0.15-discord.md)).
5. CHANGELOG entry.
6. Commit `chore(release): TAOM vX.Y.Z`, staging release paths explicitly.
7. `git tag -a vX.Y.Z -m "…"` then `git push origin bannerlord-1.4.5 vX.Y.Z`.

**Step 7 is the one that gets skipped**, which is why
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

- [docs/reference/doc-lookup.md](./doc-lookup.md)

<!-- backlinks-end -->
