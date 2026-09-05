# Dependency Audit — BUTR stack vs engine 1.4.7 (2026-07-15)

**Status: APPLIED 2026-07-15** — the recommended updates below were applied and verified (build 0 errors; suite 4212 passed; restored runtime DLLs confirmed UIExtenderEx `2.13.2.0` / MCMv5 `5.12.1.0` / 0Harmony `2.4.2.0`). In-game verification of the MCM options screen is still pending (the launcher must be closed to deploy). The original audit findings are retained below as the rationale record; see "Applied" for what changed.

**Trigger:** user reports (second-hand, no specific functional break named) that TAOM's bundled dependencies look out of date with Bannerlord 1.4.7.

## TL;DR

The BUTR dependency versions have been static since the May 2026 DR3 internalization — they were never revisited for the 1.4.6 or 1.4.7 engine bumps. [`v1.4.7-impact.md`](v1.4.7-impact.md) only re-certified *TAOM's own* Harmony/GameModel/reflection bindings; it never re-checked the bundled BUTR DLLs.

Two dependencies are a full minor behind current (ButterLib, MCM), one is a patch behind (UIExtenderEx), and **Harmony — the one most often named — is already current.** Separately, TAOM ships only the `1.4.0`/`1.4.1` game-target implementation DLLs while the current BUTR modules ship through `1.4.5`, so on a 1.4.7 game TAOM's ButterLib/MCM run an implementation built for game 1.4.1. And `Main/_Module/SubModule.xml` still declares `Native version="v1.4.5.*"`.

All version numbers below were read this session from the NuGet flat-container API, the BUTR GitHub releases, **and the current Steam Workshop installs on this machine** (`E:\Steam\steamapps\workshop\content\261550\<id>\`) — the same builds an end user's auto-updating subscription would have.

## Applied — 2026-07-15

Executed the same day (build 0 errors; suite 4212 passed; restored runtime DLLs confirmed UIExtenderEx `2.13.2.0` / MCMv5 `5.12.1.0` / 0Harmony `2.4.2.0`):

- **Native constraint** `v1.4.5.*` → `v1.4.7.*` (`Main/_Module/SubModule.xml`).
- **ButterLib 2.10.4 → 2.11.0** — refreshed vendored `Bannerlord.ButterLib.dll` + `Implementation.1.4.0`–`1.4.5` (added `1.4.2`–`1.4.5`); stub → `v2.11.99.0`.
- **MCM 5.11.4 → 5.12.1** — `Bannerlord.MCM` NuGet in both csprojs + vendored `MBOptionScreen.v1.4.0`–`v1.4.5` (added `v1.4.2`–`v1.4.5`) + `MCM.UI.Adapter.MCMv5.dll`; stub → `v5.12.99.0`.
- **UIExtenderEx 2.13.1 → 2.13.2** (both csprojs; stub `v2.13.99.0` unchanged — patch bump within the same minor, already covered).
- **Harmony unchanged** (2.4.2, current). Polyfills / CrashReport / ModuleLoader verified byte-identical to current Workshop — untouched.
- New vendored DLLs auto-track via the existing `.gitignore` `!…Bannerlord.ButterLib*.dll` / `!…Bannerlord.MBOptionScreen*.dll` wildcards.

**Deferred (needs the game running):** removing `Patch41_McmLayoutFix` — gated on the in-game signal (Finding 1 / recommendation 3). Kept for now as a safe idempotent no-op. Before ship: close the launcher, deploy, and confirm the MCM options screen renders top-to-bottom and whether `[McmLayoutFix]` still logs "Flipped N".

## Findings table

| Dependency | TAOM ships | Current (Workshop / NuGet) | Delta | 1.4.7-safe? | Severity |
|---|---|---|---|---|---|
| Harmony (`Lib.Harmony`) | 2.4.2 | 2.4.2 | **none — current** | yes | — |
| UIExtenderEx | 2.13.1 | 2.13.2 | 1 patch | yes (current build for 1.4.x) | LOW |
| ButterLib | 2.10.4 | 2.11.0 | 1 minor | yes (`2.11.0` notes: "For … v1.4.x" + "v1.4.5 compatibility") | MED |
| MCM (`Bannerlord.MCM` / MBOptionScreen) | 5.11.4 | 5.12.1 | 1 minor | yes (current build for 1.4.x) | MED |
| `BUTR.CrashReport` family | 14.0.0.99 | 14.0.0.99 | none — current | yes | — |
| Native engine constraint | `v1.4.5.*` | should be 1.4.7 | stale | n/a | MED |

## Finding 1 (headline) — implementation-DLL fallback lands on the 1.4.1 build

ButterLib and MBOptionScreen ship one runtime *implementation* DLL per game version they were built against (`Bannerlord.ButterLib.Implementation.<gameversion>.dll`, `Bannerlord.MBOptionScreen.v<gameversion>.dll`). At load the BUTR meta-loader picks the **highest implementation whose game-version suffix is ≤ the running game version**.

- **TAOM bundles** (`Dependencies/_Module/bin/Win64_Shipping_Client/`): ButterLib `Implementation.1.4.0` + `1.4.1`; MBOptionScreen `v1.4.0` + `v1.4.1`. Nothing above `1.4.1`.
- **Current Workshop bundles**: ButterLib `Implementation.1.4.0 … 1.4.5`; MBOptionScreen `v1.4.0 … v1.4.5`.

On a **1.4.7** game there is no `1.4.6`/`1.4.7` implementation in either bundle, so the loader falls back to the highest available:
- TAOM → **`1.4.1`** (built against game 1.4.1)
- Current BUTR → **`1.4.5`** (built against game 1.4.5)

So TAOM users run a ButterLib/MCM implementation compiled for game **1.4.1** on a **1.4.7** engine, four implementation revisions behind what current BUTR would select. BUTR only ships a new implementation when the engine surface it binds changes, so `1.4.2`–`1.4.5` each exist because *something* shifted. This is the [`dr3-maintenance.md`](dr3-maintenance.md) "Bundled Implementation falls back wrong version" risk, now confirmed real, and it is the most concrete match to "not up to date with 1.4.7."

**Caveat (don't overstate):** TAOM has shipped on 1.4.5–1.4.7 with the `1.4.1` impl and no documented ButterLib/MCM crash, so the `1.4.1` implementation still *loads* on 1.4.7 — impact ranges from cosmetic to real depending on which ButterLib/MCM code paths TAOM (and co-loaded third-party mods) exercise. The safe posture is to bundle `1.4.2`–`1.4.5`; confirming a specific in-game break is not required to justify it.

## Finding 2 — Native engine constraint is stale (`v1.4.5.*`)

[`Main/_Module/SubModule.xml`](../../Main/_Module/SubModule.xml) line 17:

```xml
<DependedModuleMetadata id="Native" order="LoadBeforeThis" version="v1.4.5.*" />
```

The game is pinned to 1.4.7. **[Likely]** `v1.4.5.*` wildcards only the 4th component, so it does not match `v1.4.7.x` — but note the sourcing: the BUTR `SubModule.xsd` types `version` as a bare `xs:string` with no pattern, and the **vanilla engine never reads `DependedModuleMetadata` at all** (zero references in the v1.4.7 decompile — `ModuleInfo` reads only `DependedModules` + `DependentVersion`). It is a BUTR/BLSE-only construct, and the only in-repo authority is an attestation in the Harmony stub's comment, not a spec. The bump is safe under *either* reading (minimum-check or exact-with-build-wildcard), which is what makes it the right call regardless. [`dr3-maintenance.md`](dr3-maintenance.md)'s own Scenario A prescribes bumping this per engine version — it was missed on both the 1.4.6 and 1.4.7 bumps. The only other in-repo `1.4.5` module reference is a descriptive comment in `Dependencies/_Module/SubModule.xml:89`.

- **[Likely]** Under BLSE's dependency-version checking this can surface as a "built for an older game version" indicator in the launcher — a plausible driver of the vague "outdated" perception. The vanilla launcher does not enforce these constraints, and the game clearly still launches (users are playing on 1.4.7), so this is at most a warning, not a hard block.
- **Owed before asserting it to users:** confirm the exact BLSE behavior (a user screenshot of the launcher warning would settle whether this is *the* symptom). Recommended fix regardless: `version="v1.4.7.*"` to match the pin and the documented convention.

## Finding 3 — package versions behind (ButterLib, MCM, UIExtenderEx); Harmony current

Authoritative current versions, from the Workshop module manifests (`<id>/SubModule.xml`) + NuGet:

| Module (Workshop id) | Current `<Version>` | TAOM pin | Pin location |
|---|---|---|---|
| Bannerlord.Harmony (`2859188632`) | `v2.4.2.0` | 2.4.2 | `Lib.Harmony` NuGet — **matches** |
| Bannerlord.UIExtenderEx (`2859222409`) | `v2.13.2` | 2.13.1 | `Bannerlord.UIExtenderEx` NuGet |
| Bannerlord.ButterLib (`2859232415`) | `v2.11.0` | 2.10.4 | **vendored DLLs only** (no NuGet pin) |
| Bannerlord.MBOptionScreen (`2859238197`) | `v5.12.1` | 5.11.4 | `Bannerlord.MCM` NuGet + vendored DLLs |

Committed-DLL FileVersions match their declared pins (ButterLib `2.10.4.0`, MCM adapter/impl `5.11.4.0`), so there's no *internal* drift — the committed binaries are consistent, just old. UIExtenderEx `2.13.2` is already sitting unused in `Dependencies/.vendor-source/` — downloaded during the DR3 investigation but never carried into the pin.

## Finding 4 — documentation / comment drift (minor, in-repo)

- ~~`Dependencies/TAOM.Dependencies.csproj` (~line 47) comment says "MCMv5 5.11.3"; the actual pin (line 59) is `Bannerlord.MCM 5.11.4`.~~ **FIXED 2026-07-15** in the same pass (the comment now names the post-bump versions).
- [`dr3-maintenance.md`](dr3-maintenance.md) prose lists stub versions as exact-match (`v2.4.2` etc.), contradicting its own v99 rule and the on-disk stubs (`v2.4.99.0` etc.). The `.99.0` files are authoritative.
- `dr3-maintenance.md`'s ~28-DLL file inventory omits the `BUTR.CrashReport` family (6 DLLs) that `Dependencies/_Module/SubModule.xml` actually vendors.

## What actually changed between the bundled and current versions

Pulled from the BUTR / Aragas release notes (both version sets are already on disk — no download needed; old = TAOM's committed bundle, new = the Steam Workshop installs):

- **ButterLib 2.10.4 → 2.11.0** (2026-07-05): DistanceMatrix setup fixes, module-finalization fixes, and "Fixed v1.4.5 compatibility." The finalization + 1.4.5 items are the ones that bear on a 1.4.7 engine.
- **MCM 5.11.4 → 5.12.1**: `5.12.0` — "Fixed display order of items" + reverted the Settings instance-cache/invalidation; `5.12.1` — **"Fixed mod list was upside down."** That is precisely the defect `Patch41_McmLayoutFix` (#252) was written to correct. See recommendation 3.
- **UIExtenderEx 2.13.1 → 2.13.2**: `2.13.1` removed obsolete UI patches; `2.13.2` fixed derived ViewModels that don't override `RefreshValues`. Minor. (The release notes' "for v1.0.x–v1.3.x" line is stale boilerplate — these are the current builds and the Workshop ships them for the live 1.4.x game; TAOM already runs 2.13.1 on 1.4.7.)

## What is NOT a problem

- **Harmony is current** (2.4.2 = latest = Workshop). The user's specific worry about Harmony does not hold.
- **`BUTR.CrashReport` is current** (14.0.0.99 committed = Workshop) — no bump needed.
- **No internal version drift** — every committed BUTR DLL matches its declared pin; the compile pins in `Main/TAOM.csproj` and `Dependencies/TAOM.Dependencies.csproj` match each other.

## Recommendations (prioritized — for a follow-up, user-approved pass)

Each follows the matching [`dr3-maintenance.md`](dr3-maintenance.md) scenario. **Build with Bannerlord closed** (DLLs deploy into the live install and file-lock otherwise).

1. **Bump the Native constraint** `v1.4.5.*` → `v1.4.7.*` (`Main/_Module/SubModule.xml:17`). One line, lowest risk, directly addresses the most likely launcher "outdated" flag. *(Scenario A step 4.)*
2. **Refresh ButterLib to 2.11.0** — copy `Bannerlord.ButterLib.dll` + `Implementation.1.4.2/1.4.3/1.4.4/1.4.5.dll` from Workshop `2859232415` into `Dependencies/_Module/bin/…`; bump the `Bannerlord.ButterLib` stub `v2.10.99.0` → `v2.11.99.0`. *(Category 2; ButterLib is vendored — no NuGet edit.)*
3. **Refresh MCM to 5.12.1** — bump `Bannerlord.MCM` NuGet `5.11.4` → `5.12.1` in **both** `Main/TAOM.csproj` and `Dependencies/TAOM.Dependencies.csproj` (compile pins must stay matched); copy the newer `Bannerlord.MBOptionScreen.v1.4.2…v1.4.5.dll` + `MCM.UI.Adapter.MCMv5.dll` from Workshop `2859238197`; bump the `Bannerlord.MBOptionScreen` stub `v5.11.99.0` → `v5.12.99.0`. **Then re-evaluate `Patch41_McmLayoutFix`** — MCM `5.12.1` fixed the upside-down mod list Patch41 works around. **Verdict: KEEP.** See the addendum below for the evidence; the reasoning originally given here (that Patch41's one-directionality proves it is a safe no-op) was **circular** and is retracted.
4. **Bump UIExtenderEx 2.13.1 → 2.13.2** in both csprojs. **No stub edit** — the v99 rule keys on the *minor*, and 2.13.1→2.13.2 stays inside minor 13, so `v2.13.99.0` already covers it. Lowest-value of the four; a patch.
5. **Fix the in-repo drift** from Finding 4 (csproj comment, dr3-maintenance stub prose + CrashReport inventory) while touching these files.
6. **Harmony: no change.**

Prune superseded implementation DLLs only per the `.gitignore` allowlist step in `dr3-maintenance.md` (don't orphan the loader).

## Follow-up validation (when bumps are applied)

1. `./build.ps1 -RunTests` — build + suite green.
2. `dr3-maintenance.md` 6-step smoke test — Mod Options tab renders, a setting persists across re-entry, and **only `TAOM` + `TAOM.Dependencies` are *required*** (no external `Bannerlord.*` module demanded).
3. `/verify-bindings` — TAOM's own Harmony/GameModel/reflection sites still resolve against 1.4.7.
4. In-game: confirm no `[McmLayoutFix]` regression and no ButterLib/CrashReport load error in `Logs/`.

## Addendum — deep-review findings (2026-07-16)

A 6-agent `/deep-review` over the applied change. **No runtime defects; every finding was docs/metadata.** Full RCA: [`docs/reviews/rca-butr-dependency-update-2026-07-16.md`](../reviews/rca-butr-dependency-update-2026-07-16.md). What it changed about the record above:

**1. The Patch41 verdict is KEEP — now on evidence, not the retracted circular argument.**

- **Proven:** MCM 5.12.x fixed the inversion by rewriting the prefab **attribute**. Byte-scan of the embedded `MCM.UI.GUI.Prefabs.*.xml` resources, with the pre-bump build as a control: `MBOptionScreen.v1.4.1.dll` @5.11.4 → `VerticalBottomToTop` ×9 / `VerticalTopToBottom` ×2; `v1.4.5.dll` @5.12.1 → ×0 / ×11. Total conserved at 11 — exactly the 9 reversed attributes were corrected. So against TAOM's *own* bundled MCM, `FlipMcmLayout` finds nothing and returns 0.
- **But KEEP anyway:** MCM DLLs are **unsigned and resolved by simple name**, and multiple MCM copies can coexist on one install (this dev machine has `DOTS.Dependencies` shipping MCM **5.11.4** beside TAOM's 5.12.1). **Load order decides which MBOptionScreen wins.** If an older MCM wins, the inverted prefabs load and Patch41 is still load-bearing. This is the "External module conflict" risk generalised to MCM. Deleting on the single-install prefab scan would be unsafe.
- **The retracted argument:** "Patch41 is one-directional, therefore it can't double-invert" proves safety *only if you already assume the prefabs were corrected* — the very question at issue. Had MCM fixed the ordering in code while keeping the attribute, Patch41 would still be one-directional, still flip, and still re-invert. Right answer, non-load-bearing reasoning.
- **Counter-consideration on file** (argues for eventual removal): `McmLayoutRewriter` scopes by *bare prefab name* — `SettingsView`, `ModOptionsView`, `SettingsPropertyGroupView` are generic, so a third-party prefab registered under those names with a legitimate `VerticalBottomToTop` would be flipped. Once the load-order question is settled in TAOM's favour, that residual risk has no offsetting benefit.
- **Note:** `McmLayoutRewriterTests` asserts against *synthetic* XML and never touches a real MCM resource — a green suite is **not** evidence Patch41 is inert.

**2. API compatibility independently verified** (13 verified / 0 incompatible / 0 unverified) against the *restored* DLLs: `CreateAndRegister(string, XmlDocument)` signature intact; `WidgetFactoryManager` byte-identical 2.13.1→2.13.2; `MCMv5.dll` 5.11.4→5.12.1 identical apart from version stamps; all string-named MCM reflection targets resolve.

**3. Latent landmine (LOW, no action today):** UIExtenderEx 2.13.2 **removed a null guard** in the mixin patcher — an unresolvable refresh-method name now NREs at `UIExtender.Register` instead of silently skipping. All four TAOM mixins resolve today (`CharacterDeveloperVM`/`EncyclopediaHeroPageVM`/`MapTimeControlVM.RefreshValues`, `MapInfoVM.Refresh`), so behaviour is unchanged — but a future engine bump renaming any of them now hard-fails. Added to the engine-bump checklist.

**4. Premises of mine that were false:** the Gaming.Desktop folder holds **no** BUTR DLLs (nothing to diverge); and the "impls cap at 1.4.5 vs Native 1.4.7" gap is **not** an inconsistency — BUTR ships no newer impl, so TAOM resolves exactly what the Workshop module would.

**5. The systemic root cause → fixed.** Nothing asserted any of these couplings, which is why 4212 tests stayed green across two engine bumps. [`BundledDependencyManifestTests`](../../TAOM.Tests/Infrastructure/Dependencies/BundledDependencyManifestTests.cs) now pins compile-pin parity, the v99 stub derivation, vendored-DLL version homogeneity, the Native↔pinned-engine coupling, and the licence attribution. Verified RED against the pre-fix state.

## Evidence sources (this session)

- NuGet flat-container index: `api.nuget.org/v3-flatcontainer/{lib.harmony,bannerlord.uiextenderex,bannerlord.mcm,bannerlord.butterlib}/index.json`.
- BUTR GitHub releases: `Bannerlord.ButterLib` v2.11.0 ("For … v1.4.x", "v1.4.5 compatibility"), `Bannerlord.UIExtenderEx` v2.13.2.
- Steam Workshop manifests + bin (`E:\Steam\steamapps\workshop\content\261550\{2859188632,2859222409,2859232415,2859238197}\`) — module `<Version>` + the full `Implementation.*` / `MBOptionScreen.v*` DLL lists + `Get-Item …VersionInfo`.
- Repo: `Main/_Module/SubModule.xml:17`, `Dependencies/TAOM.Dependencies.csproj:57-59`, the four `Stubs/*/\_Module/SubModule.xml`, `Dependencies/_Module/bin/Win64_Shipping_Client/*.dll`.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/INDEX.md](../INDEX.md)
- [docs/migration/dr3-maintenance.md](./dr3-maintenance.md)
- [docs/modding/module-dependencies.md](../modding/module-dependencies.md)
- [docs/reviews/rca-butr-dependency-update-2026-07-16.md](../reviews/rca-butr-dependency-update-2026-07-16.md)

<!-- backlinks-end -->
