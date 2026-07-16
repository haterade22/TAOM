# RCA — BUTR dependency update (2026-07-15/16)

**Change:** ButterLib 2.10.4→2.11.0, MCM 5.11.4→5.12.1, UIExtenderEx 2.13.1→2.13.2, Harmony unchanged (2.4.2), Native pin `v1.4.5.*`→`v1.4.7.*`, vendored impl set 1.4.0/1.4.1 → 1.4.0–1.4.5.
**Review:** 6-agent `/deep-review`, 2026-07-16. **No runtime defects.** Every finding was documentation, metadata, or process.
**Audit + applied record:** [`docs/migration/dependency-audit-2026-07-15.md`](../migration/dependency-audit-2026-07-15.md).

## Top-line

The user report ("dependencies look out of date with 1.4.7") was **correct in substance and wrong in target**. Harmony — the dependency named — was already current. The real staleness was ButterLib/MCM being a minor behind, a Native engine pin that never moved through **two** engine bumps, and a vendored implementation set capped at the game-1.4.1 build while BUTR had shipped through 1.4.5.

The interesting part is not the individual stale values. It is that **nothing in the repo could detect any of them**: 4212 tests passed continuously while the dependency stack drifted across the 1.4.6 and 1.4.7 bumps, because no test asserted a single one of these couplings. Every guard was a human reading a doc — and the doc was itself stale, having been logged broken a month earlier and never fixed.

## Findings

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | MED | Native pin sat at `v1.4.5.*` through the 1.4.6 **and** 1.4.7 bumps | Pre-existing | No executable coupling between the pin and the declared engine target. `dr3-maintenance.md` Scenario A actively said "most likely nothing needs to change" for a patch bump | `BundledDependencyManifestTests.NativeConstraint_MatchesPinnedGameVersion`; Scenario A rewritten |
| 2 | MED | Vendored impls capped at `1.4.1` while BUTR shipped through `1.4.5` → loader silently selected a game-1.4.1 build on a 1.4.7 engine | Pre-existing | Same Scenario A advice; the impl set is a folder, and nothing compared it to upstream | Scenario A now mandates re-checking the Workshop impl set on every engine patch |
| 3 | HIGH | `THIRD-PARTY-LICENSES.txt` attributed 4 wrong upstream versions for physically swapped binaries | **Introduced by this change** | Treated a binary swap as a code task. Licensing/attribution was not on any checklist and never entered my head | `ThirdPartyLicenses_NameTheActuallyShippedVersions` test |
| 4 | HIGH | `Dependencies/_Module/SubModule.xml` pin-comment block stale on 6 counts | **Introduced by this change** | I updated the two stub comments I happened to have open and never swept for sibling declarations. Fixed what I read, not what existed | Version values deleted from the comment; it now points at the sources |
| 5 | HIGH | `dr3-maintenance.md` stale on 22 lines; inventory omitted 14 DLLs | Pre-existing, **worsened** | The doc restates values that live in csproj/stubs — drift by construction. Rows 191-194 were logged broken in June (`plans/_audit/2026-06-12-harvest.md` DEPS-05/06) and never fixed; my bump made them version-wrong *and* policy-wrong | Duplicated values deleted; doc points at source files + the new test |
| 6 | MED | Patch41 safety argument was **circular** | **Introduced by this change** | I argued from idempotency — a property that holds identically in the safe and unsafe cases, so it discriminated nothing. Right answer, non-load-bearing reasoning | Retracted in the audit doc; replaced with the prefab byte-scan + the load-order caveat |
| 7 | MED | Audit doc self-defects: a `v2.13.99.0 → v2.13.99.0` no-op, and a Finding that went stale about a line I fixed in the same pass | **Introduced by this change** | Wrote the findings doc in the same pass as the fixes and never re-read it against the final state | Fixed; see also `evidence-over-claims.md` §C trap 1 |
| 8 | LOW | Inventory omitted the 6 `BUTR.CrashReport*` DLLs whose absence causes a known `ReflectionTypeLoadException` | Pre-existing | Nobody rebuilt a bin folder from the inventory, so the omission never bit | Added to the inventory, marked MANDATORY with the crash reference |
| 9 | LOW | UIExtenderEx 2.13.2 removed a null guard in the mixin patcher — unresolvable refresh-method names now NRE at `Register` instead of skipping | Upstream, latent | Not detectable from the changelog; only a decompile diff of the new DLL surfaced it. All 4 TAOM mixins resolve today | Engine-bump checklist item (below) |

## Root-cause patterns

### A. A doc that restates a machine-readable value is a drift site by construction

Findings 3, 4, 5, 8 are one bug wearing four hats. Every stale line restated something that already lives, authoritatively, in a csproj, a stub, or a DLL header. The value doesn't drift — the *copy* does. And the copies are exactly the artifacts a maintainer trusts, so drift is silently load-bearing.

This is why re-syncing the numbers was the wrong fix and deleting them was the right one. Re-syncing resets the clock; it does not remove the mechanism. The evidence that re-syncing fails is on file: `dr3-maintenance.md`'s Category 1 table was re-synced in May 2026 and had drifted again by July.

**Rule:** in TAOM docs, never restate a version that lives in a build file. Point at the file. If the value must be asserted, assert it in a test.

### B. No executable coupling ⇒ unbounded silent drift

Findings 1 and 2 survived two engine bumps under a green 4212-test suite. There was no failure signal at any point because there was no assertion. The `/engine-bump` and `dr3-maintenance` procedures both *described* the coupling in prose; prose does not fail a build.

The tell that this was systemic rather than an oversight: the drift is exactly as old as the last time someone manually remembered. That's the signature of a human-memory guard.

**Rule:** when a change requires two files to agree, and the agreement is not enforced by the compiler, write the test in the same change. `BundledDependencyManifestTests` now pins compile-pin parity, the v99 stub derivation, vendored-DLL homogeneity, the Native↔engine coupling, and licence attribution — and was verified RED against the pre-fix state (`v1.4.5.*` vs pinned `v1.4.7`; `ButterLib 2.10.4` attribution vs shipped 2.11.0).

### C. An argument that also holds in the failure case proves nothing

Finding 6 is the most instructive. I argued: *Patch41 only rewrites `BottomToTop`→`TopToBottom`, therefore it cannot double-invert a corrected screen.* That is true, and it is not an argument. In the dangerous world — where MCM fixed the ordering in code and left the attribute — Patch41 is **still** one-directional, **still** flips, and the screen **still** re-inverts. The property I cited was constant across both hypotheses, so it discriminated neither.

The correct move was the one the adversarial agent made: go read the prefabs. `MBOptionScreen.v1.4.1.dll` @5.11.4 → `VerticalBottomToTop` ×9 / `VerticalTopToBottom` ×2; `v1.4.5.dll` @5.12.1 → ×0 / ×11. Total conserved at 11 — MCM rewrote exactly the 9 reversed attributes. *That* discriminates.

**Rule:** before accepting a safety argument, ask "would this same sentence be true if the thing were unsafe?" If yes, it is not evidence — go get the fact. This is `evidence-over-claims.md` §C applied to reasoning rather than to facts.

### D. Swapping a binary carries non-code obligations

Finding 3 is small but was invisible to me: I thought about compile pins, stub versions, loader manifests, and impl ranges — and not once about the fact that we *redistribute* these DLLs and make a legal attribution claim about them. A binary swap has a licence surface.

## Why the review caught what the work missed

Not a per-agent post-mortem (the agents performed well); the useful question is what the *original pass* lacked:

- **I fixed what I read.** I updated the two stub comments because I opened those files to bump `<Version>`. I never opened `Dependencies/_Module/SubModule.xml` — nothing forced me to — so its six stale lines survived. The stale-reference agent found them in one grep. **A targeted edit pass needs a repo-wide sweep for sibling declarations, not just the files the change forces you to touch.**
- **I trusted a changelog over an artifact.** "Fixed mod list was upside down" is a claim about intent. The prefab bytes are the fact. I built a whole recommendation on the former.
- **I let the multi-install case slip.** The adversarial agent proved Patch41 is a dead no-op *against TAOM's own bundle* and concluded "delete." The API agent found `DOTS.Dependencies` shipping MCM 5.11.4 on the same machine and concluded "keep — unsigned assemblies resolve by simple name, load order decides." **The narrower, more-confident finding was the wrong one.** Two agents disagreeing was worth more than either agent alone.

## Preventive actions taken

1. **[`BundledDependencyManifestTests`](../../TAOM.Tests/Infrastructure/Dependencies/BundledDependencyManifestTests.cs)** — 8 tests, RED-proven. Asserts relationships, never version literals (a test that restates a version is one more drift site).
2. **`dr3-maintenance.md` restructured** — version values deleted from Category 1 / Category 2 / the stub list; Scenario A rewritten (its old "nothing needs to change" advice caused findings 1 and 2); the `:206` vs `:208` contradictory stub rules reconciled to the minor-keyed one; CrashReport family added to the inventory as MANDATORY.
3. **`Dependencies/_Module/SubModule.xml`** — pin block replaced with pointers to the authoritative sources.
4. **`THIRD-PARTY-LICENSES.txt`** — corrected, and now test-enforced.
5. **Audit doc** — circular reasoning retracted, BLSE wildcard semantics downgraded from flat fact to `[Likely]` + sourcing note, self-defects fixed, addendum added.

## Engine-bump checklist addition (finding 9)

Add to `/engine-bump`: **after any UIExtenderEx bump, verify all TAOM `ViewModelMixin` refresh-method names still resolve.** Since 2.13.2 the mixin patcher no longer null-guards the lookup — an unresolvable name now throws NRE at `UIExtender.Register` (previously a silent skip). Current mixins: `CharacterDeveloperVM.RefreshValues`, `EncyclopediaHeroPageVM.RefreshValues`, `MapTimeControlVM.RefreshValues`, `MapInfoVM.Refresh` — all resolve as of v1.4.7. A future engine rename turns a quiet no-op into a hard startup failure.

## Lessons to codify

Appended to [`docs/reviews/lessons/build-tooling-workflow.md`](lessons/build-tooling-workflow.md):
- Never restate a build-file version in prose; point at the file, assert it in a test (pattern A).
- Two files that must agree get a test in the same change (pattern B).
- A safety argument that also holds in the failure case is not evidence (pattern C).
- Vendoring a binary is a licence event, not just a build event (pattern D).

## Outstanding

- **GitHub issue** — none exists for this work (`CLAUDE.md` policy requires one). Not created: `/issue` publishes a public artifact and needs explicit intent.
- **In-game smoke test** — 6/6 steps outstanding (launcher was running). Deploy is verified correct on disk; the MCM options screen still needs a human eye.
- **Patch41 removal** — remains KEEP pending the load-order question. If TAOM's MCM is confirmed to win load order in practice, the generic-prefab-name collision risk (`SettingsView`, `ModOptionsView` are not TAOM-specific) becomes pure downside and removal is the right call.
- **`Dependencies/bin/Release/net472/`** — orphaned folder from an older output layout (its `TAOM.Dependencies.dll` predates the current build by a month; `dotnet clean -c Release` does not touch it). Not the ship path — the deployed game install is verified correct — but it misleads a version audit and should be deleted.
