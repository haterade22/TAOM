# RCA: The Dependencies deploy guard, review of the bundled-DLL fix (2026-11-16)

Scope: TAOM worktree `C:\Users\luism\Desktop\Claude\tools\TAOM-dlldeploy`, branch
`fix/dependencies-deploy-keeps-bundled-dlls`, on top of trunk, not pushed. The slice adds `FailOnMissingBundledDlls`
to `Dependencies/TAOM.Dependencies.csproj`: a deploy now stops before Bannerlord.BuildResources'
`CopyBinariesWindows` deletes the bundled DLLs in the install when a DLL named in `Dependencies/_Module/SubModule.xml`
is neither built nor staged. Review: `/deep-review` tier 2, lens 5. Nothing blocking; three findings applied.

## Findings and root causes

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| F1 | LOW | The csproj comment and `docs/modding/module-dependencies.md` named "a partial clone" as a way the DLLs go missing. A partial clone fetches blobs at checkout; only a sparse checkout (skip-worktree, `git ls-files -v` flag S) leaves them out. | Doc accuracy | The cause was written from the symptom (files absent after a clone) without checking which Git feature produces it. | Both texts name only the sparse checkout, and the error text points at `git ls-files -v`. |
| F2 | MED | The guard's scheduling and condition rest on two facts of the pinned package's `Basic.targets` that no test pinned: `PostBuildCopyToModules` is declared before `CopyBinariesWindows`, and the clean's condition ignores `DisableModuleCopy`. A package bump could change either and the guard would pass its wiring tests while no longer running first. | Test coverage of an external contract | The wiring tests read only this repo's csproj; the package facts were checked once by hand against a scratch game folder. | `BundledDllDeployGuardTests.PinnedBuildResources_DeclaresTheGuardHostBeforeTheCleanAndIgnoresDisableModuleCopy` reads the pinned version from the csproj and asserts both facts in the cached `Basic.targets`; it is Inconclusive when the package is not in the NuGet cache. |
| F3 | LOW | The doc did not say what the guard covers. It checks the DLLs `SubModule.xml` names (9 names, 6 of them among the 44 vendored files; `TAOM.Dependencies.dll`, `Bannerlord.UIExtenderEx.dll` and `MCMv5.dll` come from the build output), not every vendored file. | Doc scope | The doc described the failure, not the guard's reach. | The doc states the scope: it catches the real failure, since a sparse checkout drops all 44 together, but not a partial set. |
| F4 | LOW | `tools/package_release.py` has the same gap for a release zip built from a checkout without the vendored DLLs. | Older gap, outside this slice | The slice fixes the deploy path only. | FOLLOW-UP: no issue: the owner asked for PRs only, no new issues; noted for haterade in the PR body. |

Lenses 1 to 4: no finding.

## Lessons

- A guard scheduled around another package's targets needs a test on that package's own file, not only on our
  wiring, so a version bump that reorders or re-conditions the targets fails a test.
- Name the Git feature that causes a missing-file state exactly; a partial clone and a sparse checkout differ.
- State what a presence check covers when it reads a manifest instead of the whole folder.

## Fixes

Committed on the branch (`fix(build): v2.0.40 - Deploy guard notes and package pin test`), test first for F2. Build
and filtered tests pass; the full suite adds no failing name beyond the known trunk failure
`EveryKey_IsSeededInAllTwelveLanguageFiles`. F4 stays open as noted above.
