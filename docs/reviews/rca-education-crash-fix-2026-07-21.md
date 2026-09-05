# RCA — Age-8 child-education CTD (lothlorien + 3 cultures), issue #354

**Date:** 2026-07-21 · **Trigger:** player crash bundle `taom_crash_20260720_203614_94c7b795`
(TAOM v2.0.12, Bannerlord v1.4.7.117484) — clicking the child-education map notification for a
Lothlórien child at age 8 CTD'd with `System.NullReferenceException`.

## Top-line

A data gap, not a code bug: `lothlorien` shipped as `is_main_culture="true"` with **zero**
`child_education_templates_stage_2_page_0_branch_{0-5}_lothlorien` NPCCharacters. The v1.4.7
engine resolves those ids at the Year8 education stage (`EducationCampaignBehavior.
GetSpecialCharacterForOption`, decompile lines 256/261) and dereferences the result with no null
guard (`.Equipment`, lines 278/296). The education screen calls `GetOptionProperties` for every
option at first paint (`EducationVM.InitWithStageIndex(0)`), so the CTD fires on the notification
click. `umbar`, `goblin`, and `mistymountainorcs` carried the identical gap.

A second, compounding defect made the crash unattributable from the bundle: **PatchShield's
finalizer unwrapped `TargetInvocationException` and rethrew the bare inner exception**, which
resets the exception's stack to the Harmony-rewritten frame. The bundle therefore showed a bare
NRE at `ViewModel.ExecuteCommand_Patch3`, `InnerException: null` — UI plumbing, not education
data. Root-causing required re-deriving the real path from engine decompiles.

## Findings table

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|-----|-----|----------|-----------|-------------------|
| 1 | CRITICAL | 4 main cultures missing stage_2 education character templates → guaranteed age-8 CTD | data/content | Requirement existed only in the `kingdom-creation.md` File 8 checklist, which postdates lothlorien/umbar; no validator asserted the culture→template contract; ages 2/5 never touch the ids so campaigns looked healthy for years of game time | `MISSING_EDUCATION_TEMPLATES` ERROR in `tools/validate_moduledata.py`, culture set derived from `taom_spcultures.xml` (not hardcoded); 6 unit tests incl. degraded-mode + partial coverage |
| 2 | MED (process) | #267 fixed the orc cultures' education *equipment* rosters but not the *character* templates | data/content | The two education files look like one system; a grep for "education" hits both, so the fix looked complete. The equipment lookups are null-safe (`?.DefaultEquipment`), the character lookups are not — the halves have opposite failure modes | Lesson entry (`data-content-cultures.md`): enumerate ALL cultures against the FULL contract (both files) before scoping a per-culture data fix; validator covers the crashing half permanently |
| 3 | MED (diagnostics) | PatchShield finalizers rethrew the TIE-unwrapped inner exception, destroying the real stack in every crash bundle involving a reflection-invoked handler | state/lifecycle | The unwrap was written for *classification* (swallow-trinity detection through TIE wrappers) and the same unwrapped object was reused for the *rethrow* — two concerns, one variable. Harmony's `throw result;` resets that object's `_stackTrace`; nobody traced what the reset does to bundles until a bundle was actively misleading | Finalizers now return the ORIGINAL `__exception`; unwrap survives only inside `ShouldSwallow` classification. Verified by a rethrow-semantics harness: `throw inner` loses the real throw-site, `throw originalTie` preserves it via the inner chain |

## Root-cause pattern

**A per-culture data contract enforced by convention (checklist) instead of by a gate.** The
engine's id-interpolation contract (`..._{culture.StringId}`) is invisible in the culture XML —
nothing in `taom_spcultures.xml` references education templates, so no ref-sweep can catch the
gap. The only defenses are (a) the authoring checklist (decays, postdates older cultures) and
(b) a validator that derives the required id set from the culture registry — which did not exist.
This is the same shape as the PrisonerRecruitment lesson (2026-07-16): "safe because the data
says so" claims need a test/gate that derives its set from the authoritative file.

## Why the crash was hard to attribute (diagnostics chain)

1. `ViewModel.ExecuteCommand` invokes command handlers via `MethodInfo.InvokeWithLog` →
   handler exceptions arrive wrapped in `TargetInvocationException`.
2. PatchShield's finalizer (installed on every Harmony-patched method, incl. `ExecuteCommand`)
   unwrapped the TIE and returned the inner NRE → Harmony `throw`s it → stack reset to
   `ExecuteCommand_Patch3`, inner chain lost.
3. The CrashReport pipeline faithfully recorded what it saw: bare NRE, `Source=0Harmony`,
   `InnerException: null`. The report was *accurate about a destroyed exception*.
4. Secondary noise: `CareerSystem: GetPassiveMagnitude` DEBUG logging flooded `taom_debug.log`
   (thousands of lines/min), burying any education-adjacent warnings. (Follow-up candidate; not
   changed in this pass.)

## Fix set (this pass)

- 24 stage_2 tutor templates (lothlorien←rivendell/elf, umbar←gondor/human,
  goblin+mistymountainorcs←`sk_md_orc_*` pool per their own NPC conventions).
- 392 education equipment rosters (lothlorien/umbar/shaghana/abanissa — cosmetic completeness).
- Validator rule + 6 tests; `add_education_roster_cultures.py` KNOWN_CULTURES 10→14.
- PatchShield original-exception rethrow.
- Docs: CHANGELOG, `kingdom-creation.md` File 8 enforcement note, lessons entry.

## Verification

- `python tools/validate_moduledata.py` → PASS (5,867 items, 5,038 NPCCharacters, 38 cultures).
- Negative probe: removing one lothlorien template id from the registry fires
  `MISSING_EDUCATION_TEMPLATES` at `taom_spcultures.xml:1102`.
- `python -m unittest tools.tests.test_validate_moduledata` → 30/30.
- `dotnet build TAOM.sln` 0 errors; `dotnet test TAOM.Tests` 4386 passed / 0 failed.
- Owed (user): in-game — lothlorien save with an age-8 child, click the education notification,
  confirm the screen opens with 6 clothed option previews.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/reviews/REVIEW-LOG.md](./REVIEW-LOG.md)

<!-- backlinks-end -->
