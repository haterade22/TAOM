# RCA — v1.4.7 engine-bump deep review (2026-07-08)

**Scope:** `/deep-review` of the Bannerlord v1.4.6→v1.4.7 engine-bump changeset (Patch15 disable, Patch49 comment refresh, snapshot generator version-derive, config/test/doc updates). 5 agents (standards, v1.4.7 API compat, efficiency, completeness, data flow).

**Result:** 4 agents PASS with zero findings. **1 confirmed finding, LOW, documentation-only, fixed in-session.** No HIGH/MED. The API-compat agent independently re-verified every load-bearing v1.4.7 decompile claim (Banner 32-cap removed; Patch49 NREs still unguarded at Army.cs:726/659) against the installed DLLs — matching the firsthand reads taken during the bump.

## Findings

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|-----|---------|----------|------------|-------------------|
| 1 | LOW | `docs/features/banner-color-persistence.md` still showed `"EnableLayerLimitTranspiler": true` in its Configuration JSON example + the sentence "All flags default to `true`", contradicting the new `false` default. | doc-consistency | When flipping the default I synced the value across the C# default, the shipped JSON, and the 3 tests that pin it (correctly applying the "both config surfaces must agree" instinct), but only grepped `Main/` + `TAOM.Tests/` for consumers — I did not grep `docs/features/` for the feature doc that mirrors the config example. | Fixed the doc (example → `false` + an explanatory sentence pointing to `v1.4.7-impact.md`). Generalizable note below. |

Found independently by BOTH the data-flow agent (config→consumption trace) and the completeness agent (dangling-reference check) — high-confidence real, not a false positive. Verified firsthand (read the doc) before fixing, per `evidence-over-claims.md`.

## Why each agent's scope did/didn't catch it

- **Standards / Efficiency / API-compat:** out of scope by design (they review code + engine signatures, not feature-doc prose). Correctly silent.
- **Data flow (caught it):** its "XML config → consumption" trace grepped the whole repo for `EnableLayerLimitTranspiler`, which surfaced the doc example.
- **Completeness (caught it):** its check #4 "does anything else document the feature as enabled that this change contradicts" is exactly this case.
- **My own pass (missed it):** the `banner_color_config.json` ↔ `BannerColorConfig.cs` two-surface sync was front-of-mind (it's a runtime-behavior trap the csharp-architecture rule warns about), so I checked those + the tests. The feature-doc example is a *third* mirror surface with no runtime effect, so it fell outside the "must agree or the feature breaks" mental checklist.

## Root-cause pattern (minor)

Flipping a **documented** config default has up to four mirror surfaces, not two: (1) the C# default, (2) the shipped JSON, (3) tests that pin the default, (4) the **feature doc's config example + any "defaults to X" prose**. The runtime-behavior surfaces (1)+(2)+(3) are self-enforcing (a mismatch fails a test or changes behavior); surface (4) is silent — nothing breaks, it just misleads the next reader. The fix is to grep `docs/` — not only `Main/` + `TAOM.Tests/` — for the flag name when changing a default.

## LESSONS-LEARNED disposition

**Not appended.** This is a single LOW doc-sync miss, already fixed, with no runtime impact. Per the deep-review skill's Step 3e ("only if there's a genuine systemic pattern; don't manufacture rules"), a one-surface documentation drift does not rise to a cross-feature `LESSONS-LEARNED.md` category rule — the runtime-invariant sibling ("settable from BOTH JSON and MCM → enforce at both") is already codified in `.claude/rules/csharp-architecture.md` and was correctly applied here. The generalizable note (grep `docs/` too when flipping a documented default) lives in this RCA; escalate only if a *second* instance appears.

## Verdict

**READY FOR COMMIT** after the doc fix (applied). Offline verification green throughout: BindingVerification 50/50, full suite 4169/0/2, snapshot reproducible, creature/scene parity clean. In-game control battles remain owed to the user (per `docs/migration/v1.4.7-impact.md`).
