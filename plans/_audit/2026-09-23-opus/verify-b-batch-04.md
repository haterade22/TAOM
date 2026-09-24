# Verify B, batch 04 (adversarial checker, baseline `b2e387db`)

Keys: DX-L6-03, DX-L6-04, DEPS-L6-05 (full text in `lane-6.findings.md`).

## DX-L6-03: CONFIRMED (impact today LOW)

- **Re-read**: `git show b2e387db:.claude/hooks/validate-push.sh`. Line 115 is `master|main|bannerlord-1.4.5) return 0 ;;`,
  the comment at 110-112 is as quoted. `git grep -n "1\.5\.x" b2e387db -- .claude/hooks` returns 0 lines.
- **Ancestry**: `v2.0.29^{commit}` = `a12fec9b...`, `v2.0.30^{commit}` = `96f17fec...`; both are ancestors of
  `bannerlord-1.5.x` and of neither `bannerlord-1.4.5` nor `origin/bannerlord-1.4.5` (`git merge-base --is-ancestor`).
- **Server side**: `gh api repos/haterade22/TAOM/branches/bannerlord-1.5.x` and `.../bannerlord-1.4.5` both
  `"protected": false`; `gh api repos/haterade22/TAOM/rulesets` length 0; `rules/branches/bannerlord-1.5.x` length 0;
  repo is `public`.
- **Refutation attempts**: `block-dangerous-git.sh:16` says it "Deliberately does NOT touch `git push`, owned by
  validate-push.sh". No deny or ask rule for `git push` in `.claude/settings.json` (no `permissions` block) or the
  user settings (`deny: []`). The only mitigation is that `git push` is not on any allow list, so a Claude push still
  raises a permission prompt; that is not a hook block and does not cover other clients. Not by design: the
  hooks-catalog row (`docs/reference/hooks-catalog.md:36`) and the hook comment both intend "the branch everyone works
  on" to be protected.
- **Extension (not in the lane text)**: `validate-push.sh` is registered only under `"matcher": "Bash"`
  (`.claude/settings.json:35,44`), so a `git push --force` through the PowerShell tool bypasses the hook for every branch,
  `bannerlord-1.4.5` included. Worth folding into the same fix.
- **Corrected evidence**: none needed; citations hold.

## DX-L6-04: CONFIRMED, one evidence leg REFUTED and one overstated (impact today MED)

- **Holds (re-read)**:
  - `Directory.Build.props:23-24` (at `b2e387db`) sets `InformationalVersion` to `build.$(TaomBuildStamp)` and nothing else.
    No dirty or git logic anywhere in the build: `git grep -n -i -E "SourceRevision|IncludeSourceRevision|dirty|git (status|rev-parse|describe)|SourceLink" b2e387db -- '*.csproj' '*.props' '*.targets' build.ps1`
    returns nothing; `build.ps1` has no `git|dirty|stamp|tag|version` hit; the referenced `Bannerlord.BuildResources`
    1.1.0.129 package (`Main/TAOM.csproj:95`) ships `.props/.targets` with no git reference (checked in the NuGet cache).
    The live log line 2 (`taom_debug_2026-09-23_13-43-40.log`) shows `...+c79a585218ad...` with no dirty marker. So a
    build of a dirty tree carries HEAD's SHA and looks clean. `git status --porcelain -- Main Dependencies Directory.Build.props`
    right now: 21 paths (14 `.cs`), matching the lane's count. Whether that particular 18:42Z build was dirty is not
    reconstructable, but the mechanism does not depend on it.
  - `tools/package_release.py` (397 lines) has no `git`, tag, dirty or hash-of-DLL logic (`grep -n -i "git|tag|dirty|sha|hash"`:
    only the `--json` manifest of copied paths).
  - `release-process.md:69` permits a release "when another session's edits are present, every path staged explicitly";
    `.claude/skills/release/SKILL.md:24` requires `git status --porcelain` **empty**. The two sources disagree, and
    under the doc's wording `build.ps1` compiles and deploys the other session's uncommitted C#, which no tag holds.
  - Neither source mentions `package_release.py` (`grep -n -i package` on the skill: 0 hits) or a rebuild after the tag.
  - No DLL hash registry: `git grep -n -i -E "dll sha1|DllSha1|sha1" b2e387db -- docs/releases` returns nothing.
  - `MBSaveLoad_GetSaveMetaData_Patch.cs:18-42` writes `<AssemblyVersion> (<InformationalVersion>)` into save metadata
    as `TAOM_Build` (the lane's "full InformationalVersion" is right; the range ends at 42, not 38).
- **REFUTED leg ("the crash bundle does not carry the stamp or the SHA")**: the bundle zip copies the **whole** session
  log, not a 500-line tail. `CrashBundleWriter.cs:17,46` adds `taom_debug.log` via `TryCopyFile` (`:87-95`,
  `fs.CopyTo(es)` of the full file). `FileLogger.cs:45-49` opens one `taom_debug_<timestamp>.log` per session and has
  no in-session size cap or rotation (only launch-time pruning of old files, `:61-83`). `SubModule.cs:119-126` logs the
  `[BuildStamp]` line (full `InformationalVersion`, SHA included) right after `IoC.Configure()` in `OnSubModuleLoad`,
  which is why it is line 2 of the live log. `ProcessEnvironmentCollector.cs:39` itself calls it "the bundled
  taom_debug.log". The 500-line `LogTailCollector` tail feeds only `report.txt`/`report.json`. So every crash zip does
  carry the build SHA; what is missing is a structured field in the Identity section (`IdentityCollector.cs:13-25`,
  `PlainTextCrashReportRenderer.cs:67`), a convenience rather than a traceability hole. The lane's Impact sentence
  ("names a label ... plus a DLL hash nothing maps back") is therefore wrong for zip bundles.
- **Overstated leg (release built at the parent)**: `SKILL.md:32-35` (Phase 2 build) does precede Phase 3 bump and
  Phase 6 commit, but at Phase 2 the bump has not happened yet (the lane's "parent plus the uncommitted bump" is off),
  and the release commit edits only `SubModule.xml`, the release note and CHANGELOG, no compiled input. A DLL built
  at the parent is code-identical to one built at the tag, and its SHA still resolves to exactly the shipped code.
  The real release-flow holes are the dirty-tree case above and the unstated redeploy/packaging step.
- **Delta nit**: `IdentityCollector.cs` was added in `7df18ca0` (2026-05-25), an ancestor of `141b749`, so the crash
  bundle identity is pre-existing; the release process, `BuildStampReport.cs` and `package_release.py` are post-June.
- **Corrected evidence**: drop design item 2 to optional; cite `CrashBundleWriter.cs:46,87-95`, `FileLogger.cs:45-49`,
  `SubModule.cs:119-126` as the reason the zip already carries the SHA; save metadata range `:18-42`.

## DEPS-L6-05: CONFIRMED (impact today LOW)

- **Reproduced (own scripts `vb04_weight.py`, `vb04_growth.py` in this session's scratchpad)**: `git count-objects -vH`
  size-pack 3.27 GiB; `rev-list --objects --all` + `cat-file --batch-check` gives 24,914 blobs, 3,341 MiB on disk; in the
  `b2e387db` tree 739 MiB, not in it 2,603 MiB. That is **77.9%**, so the title's "80%" is a rounding up, not wrong in
  substance. Not-in-HEAD by extension: png 1,645 MiB, ogg 541, rdc 366 (exact match). Superseded PNGs under
  `Main/_Module/AssetSources/GauntletUI` 1,065 MiB (match); `Main/_Module/GUI/SpriteData` 496 MiB in total (the lane's
  421 + 69 FactionMap split was not re-split, the sum is consistent). HEAD tree 1,154 MiB raw, `AssetSources` 550 MiB,
  `GauntletUI` 52 files 242 MiB, 34 `.psd` 309 MiB (33 in `AssetSources/BannerIcons/`, 1
  `AssetSources/main_map_textures/taom_gui_map_circle_atlas.psd`): all match.
- **Growth**: since 2026-06-23 (A/M blob ids): png 694 (724 MiB), tpac 111 (34), psd 9 (36), pkl 1 (35), rdc 78 (3,392),
  exact match. Since 2026-08-24 I get 1 new blob of these extensions (the 35 MiB pkl); the lane's "5 (36 MiB)"
  presumably counted other extensions too. Either way the recent rate is near zero, which weakens the 0.26 GiB/month
  LFS forecast the lane already rates MED.
- **Docs and code re-read at `b2e387db`**: `docs/features/gui-sprite-system.md:115` ("emptied then rewritten" every
  run), `docs/modding/banners-and-heraldry.md:156`, `docs/reference/banner-icon-generation.md:67`,
  `tools/package_release.py:134-135` (`AssetSources` EXCLUDE "editor-only, never loaded at runtime"),
  `docs/modding/module-taom.md:46` (551 MB; it cites `package_release.py:116-117`, stale, the lane's 134-135 is right),
  `Main/TAOM.csproj:12-13` (`ExcludeSourceFilesFromModule=false`), `.gitignore:128-130` (5.4 GB RDC note),
  `docs/reference/armory-catalogue/README.md:72` (`*.tpac` LFS-tracked in the sibling repo),
  `ShippedSignatureStrikesConfigTests.cs:225-226` (`File.Exists` on `ModuleSounds`). All hold.
- **F5 extension**: pattern sweep over `git ls-tree -r b2e387db` finds only `.claude/settings.local.json`,
  `.claude/tmp/freeze/.gitignore`, `_taom_loc.pkl`, `crashz/`; `git check-ignore -v --no-index` matches none of the three
  (rc 1). Introducing commits `d9817f89` (8 files, 6,862 insertions), `b930fd8a` (42 files), `78892259` "Updpdate"
  (2026-01-24, an ancestor of `141b749`, so pre-existing as the lane says). Match.
- **Worktrees and sparse option**: `git worktree list` 9 entries. `wt-opus-head` `du -sm` 1,235 MB, `Main` 996,
  `Main/_Module/AssetSources` 551 (45%). `git grep -l AssetSources b2e387db -- TAOM.Tests`: 0 files; the only tools test
  that names it (`tools/tests/test_package_release.py:88,271,364`) builds synthetic temp trees, so a sparse checkout does
  not break it. Refutation attempt on "missing art cannot reach the game": `Main/TAOM.csproj:143` states deployment is
  additive, so even a deploying build from a sparse worktree would not delete the installed `AssetSources`.
- **Option 2 side counts**: 26 tags, 1,986 commits, 49 refs match; backticked short SHAs at `b2e387db` are 853 (the lane's
  "at least 833" still holds).
- **By-design check**: no ADR, rule or orientation trap decides LFS, sparse checkout or repo weight
  (`git grep -i "lfs|sparse|repo size|pack size"` over `docs/adrs`, `.claude/rules`, `docs/ai-includes`: 0). The lane
  already routes LFS as a decision item, consistent with the brief's direction-only stance on unversioned data.
- **UNVERIFIED by me**: GitHub LFS quota and prices (external page, not fetched).
- **Corrected evidence**: "80%" is 77.9% (2,603 of 3,341 MiB); `GauntletUI` history is 29 commits with `--all` today
  (lane 28); largest sheet `ui_taom_career_system_1.png` is 31.9 MB (30.4 MiB).

## What I did not cover

- Did not trigger a live hook run of `validate-push.sh` (static read only); the permission-prompt behaviour for
  `git push` was inferred from the allow lists, not exercised.
- Did not confirm the .NET SDK target names in the DX-L6-04 design (no build, by rule), nor whether the 18:42Z build in
  the live log was made from a dirty tree.
- Did not re-split the superseded `GUI/SpriteData` PNG weight into `FactionMap` vs `FactionMap_backup`, and did not fetch
  the GitHub LFS billing page.
