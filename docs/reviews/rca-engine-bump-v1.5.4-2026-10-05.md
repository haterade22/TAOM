# RCA: Bannerlord v1.5.4 engine bump and NativeSkinFixes removal (deep review, 2026-10-05)

## Top line

The bump (`/engine-bump`) and the maintainer-requested removal of NativeSkinFixes went through a
seven-lens `/deep-review` (Opus 5.5, two waves). It confirmed 1 HIGH-class finding (a released
binary that cannot run on v1.5.4, narrowed on verification to the v2.0.33 testing build), 6 MEDIUM
and a set of LOWs. All are fixed in the same session except one step that needs the maintainer's own
edit (`.claude/settings.json`, which config protection keeps from the agent). One wave-1 claim was a
false positive and is recorded below, because how it went wrong is itself a lesson.

The theme across most findings: **the bump was verified at the source and repo layer, while what
ships lives outside it** (a compiled DLL built against the old engine, retired DLLs in the install and
the channel folders, live manifests, a reference-assembly package published hours later, per-build
tests that skip instead of failing).

## Findings

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| F1 | HIGH (real for v2.0.33) | v1.5.4 replaced `TooltipProperty(string, string, int, bool, TooltipPropertyFlags)` with a six-parameter twin. The testing channel's v2.0.33 `TAOM.dll` calls the removed member (1 unresolved of 2,940 engine refs), so it throws `MissingMethodException` on 21 tooltip sites on v1.5.4. | Binary compatibility | Every gate in the bump works on source or on the fresh build: a compile binds the new overload, and the binding tests resolve TAOM's patch targets, not its call sites. Nothing resolved a SHIPPED binary's member references | `/engine-bump` Phase 1 step 5: resolve each channel's released `TAOM.dll` against the new engine, a fresh build as negative control. A committed tool is a follow-up on #736 |
| F2 | MED | Retired `MinHook.x64.dll` and `TAOM.NativeSkinFixes.*` stayed in the dev install's three bin folders and all three release channels; `package_release.py` classified both COPY, so the next package would ship MinHook without the BSD-2 notice this change removed | Retired binaries outside the repo (repeat: `BehaviorTreeWrapper.dll`, 2026-05-24) | Deploys never delete, the removal touched only the repo, and the only defence was a manual prune step whose keep-list still counted the two DLLs | The 1.5.x copies (dev install, testing channel) backed up and deleted; the patreon and public copies, deleted in the same sweep, were restored once the convergence pass noticed those channels carry the 1.4.8 line, which keeps the feature; `RETIRED_BINARIES` in `tools/package_release.py` makes the packager refuse them, dry runs included (4 tests); the release prune step names the guard |
| F3 | MED | Rewriting `Main/_Module/THIRD-PARTY-LICENSES.txt` narrowed its scope to "three things" and dropped `_Module/bin/`, while `DryIoc.dll`, `Newtonsoft.Json.dll` and `System.Runtime.CompilerServices.Unsafe.dll` ship there with no notice | Provenance: an incomplete notice turned into a false completeness claim | The edit removed MinHook's entry and "tidied" the scope sentence to match what was left in the file, not what ships in the folder | Scope restored; MIT notices added from the package license files; `redistributed` register rows for DryIoc and Json.NET. `provenance.md` "Never assert ownership you do not have" already covers it: the rule existed, the edit did not consult the folder |
| F4 | MED | The scratch body diff dropped 27 of 292 patch-target rows (targets carrying a generic-arity backtick) and ran on a stale snapshot, so it reported 364 / 6 changed / 0 unresolved; the true result is 397 / 7 changed, one of them `SandBoxSaveHelper.TryLoadSave` | Tool rebuilt from scratch per bump | `SKILL.md` said "rebuild it, it is forty lines"; a rebuilt parser has no row-count check, and "0 unresolved" counted only rows it parsed | Parser fixed with an `unparsed=` count; skill step says regenerate the snapshot first and require `unparsed=0`; script archived beside the decompiles; promoting it into `tools/` is a follow-up on #736 |
| F5 | MED | The docs sweep wrote that `Native2ManagedPatcher` lets TAOM patch `TaleWorlds.Native.dll`; it only wraps managed callback shims for crash capture | Doc claim stated beyond the code | A delegated doc edit rewrote a sentence that named MinHook into one naming the remaining mechanism, without checking what that mechanism does | Rewritten against the code (`SubModule.cs:223`, the two P/Invoke files, 0 `[EngineMethod]`) |
| F6 | MED | Bump follow-through missed: BUTR's `1.5.4.123627-beta` reference assemblies (published hours after the first check, called "blocked"), the `_categories` tree and handbook gate, seven per-build native address tests that skip instead of failing, version statements in README and the module docs, the live TAOM_Map and Armory manifests, a stale memory card | Checklist gaps in `/engine-bump` | The skill's Phase 4 named only the pin and the AGENTS `Target:` line; the native tests are written to skip on an unknown build, so a bump turns them off with a green run | All done; skill Phase 4 step 7 lists every pin with its gate, says to re-check BUTR before calling it blocked, and names the three per-build pin tables |
| F7 | LOW | Removing two language rows left two blank lines in each of 12 files | CR CR LF line handling (repeat) | `str.splitlines()` splits `row\r\r\n` into `row\r` and `\r\n`; the script kept the second piece. `.claude/rules/xml-data.md` "Formatting" names this trap | Fixed by binary replace, verified equal to HEAD minus the two rows. Edit those files as bytes, never through `splitlines` |
| F8 | LOW | Two heredoc-written edits corrupted text: `\b` became a backspace byte in a path, `\n` a real newline | Bash heredoc escapes (repeat) | Python `-` heredocs carrying Windows paths in normal string literals; memory `bash-heredoc-backslashes` already says to write such scripts with the Write tool | Both fixed; a scan of every changed file for new control bytes and for paths split across lines came back clean. Followed the memory for the rest of the session |
| F9 | LOW | Comment and test precision: Patch84's two comments counted the dereferences wrongly; the blood-feud comment named the wrong clan; a test summary kept a removed allowlist clause; the replacement JP row no longer exercised the Latin allowlist; a dead bare-key carve-out; misdated stub history; stale wording in tools and INDEX | Edit precision | First-pass edits written from the diff, not re-read against the decompile and the test logic | All fixed; the new RU row is mutation-checked (dropping `Steam` from the allowlist makes it fail) |

## The false positive

Wave 1's engine lens reported F1 against the **v2.0.34** dev build, from its decompiled source: the
source shows five arguments to `new TooltipProperty(...)` because the sixth is optional. Reading the
DLL's metadata showed both constructor references in that build already carry six parameters (blob
`20 06 ...`), and resolving all 2,981 of its engine member references against v1.5.4 found none
missing. The incompatibility was real, but in the v2.0.33 testing build, which was built before the
engine changed under it. Lesson: a claim about what a binary calls is checked against the binary's
metadata, never against a decompile of it, because optional parameters and overload resolution are
invisible in source.

## Root-cause pattern: the shipped artefact is not the repo

F1, F2, F3 and F6 share one shape: the check ran where TAOM's source and tests live, while the thing
players receive is assembled elsewhere (a DLL compiled on an earlier engine, an install folder deploys
never prune, a notice describing a folder's contents, a reference package on NuGet, manifests in the
live install). Each preventive action moves a check to the artefact: resolve the released DLL, refuse
retired binaries at the packager, read the folder before writing its notice, and list every out-of-repo
pin in the bump checklist.

## Why each lens caught or missed what it did

- **Standards (1)** caught F3, F7, F9 and the stale prune count; it was the only lens to read the notice
  against the shipped folder.
- **Engine compatibility (2)** found F1's mechanism, F4 and F5, and produced the false positive above by
  reading decompiled source for a binary claim.
- **Data flow (5)** traced F2 end to end through deploy, mirrors, packager and channels.
- **XML (7)** found F7 byte-exactly.
- **Completeness (4)** found F6, including that BUTR had published.
- **Design (6)** turned F2 into the refusal design (an exclude rule would have been invisible in the
  dry-run flow) and proposed the released-binary checker.
- **Efficiency (3)** confirmed the C# deletions keep parity; its one item was the pending hook.
- None of the first-pass work, including this session's own verification, looked at a compiled
  release before the review.

## Feedback to codify

- `/engine-bump` now carries the released-binary check (Phase 1 step 5), the snapshot-first rule and
  `unparsed=0` (Phase 3), and the full pin list (Phase 4 step 7). Lessons appended to
  `docs/reviews/lessons/build-tooling-workflow.md` and `docs/reviews/lessons/testing-qa.md`.
- No new rule file: F3, F7 and F8 are already covered (`provenance.md`, `xml-data.md`, the heredoc
  memory); the misses were in following them.
