# Lessons Learned

The canonical, **uncapped** record of every engineering lesson TAOM has distilled from a `/deep-review`, a `/review-codex` adversarial pass, or a root-cause analysis (RCA). One entry per lesson: the rule, why it was missed, how to prevent it, and the source memory + RCA it came from. There is no line limit — this file grows as the codebase teaches us more.

This document **is the index** of the per-category lesson files under `lessons/`, indexed from the harness `MEMORY.md`. The record is grouped by subsystem so a session can absorb the whole body of hard-won knowledge for the area it is about to touch. Each entry's **Source:** line names the original flat memory file the lesson was distilled from — those `feedback_*.md` files were retired in the 2026-06-24 memory reorg (a snapshot remains in `memory_backup_20260624/`), so the `Source:` is *provenance*, not a live link.

**Every session should consult this file before a review and append to it after one.** A lesson that lives only in a transcript evaporates; a lesson that lands here stops the same class of bug from shipping a third time.

## How to use

- **Before touching a subsystem**, read its category file below. Each file is the accumulated trap list for that area — `lessons/gamemodels-services.md` before overriding a model, `lessons/harmony-il.md` before writing a transpiler, `lessons/xslt-moduledata.md` before editing a `*.xslt`, and so on. Reading the relevant file is cheaper than re-deriving the trap from a crash — and far cheaper than reading all 243 lessons at once, which is why the record is split per category (2026-07-12).
- **`/deep-review` and `/review-codex` read-before / append-after.** Both review skills consult the matching category file before they start (so an agent knows the known blindspots for the code under review) and append any newly-confirmed lesson there after the RCA (Phase 3e) completes. A confirmed bug's root-cause table is not done until its lesson is in the category file.
- **One entry = one lesson.** Mirror the existing shape: a `### ` rule title, then `**Why missed:**`, `**Prevent:**`, and `**Source:**`. Append to the CATEGORY FILE, not here — this file is the index. Keep `MEMORY.md` thin — it points here, and here points at the category files.

## Categories

- [GameModels & Services](lessons/gamemodels-services.md) — 27 lessons
- [Adapters & TaleWorlds API](lessons/adapters-taleworlds-api.md) — 18 lessons
- [Build, Tooling & Workflow](lessons/build-tooling-workflow.md) — 47 lessons
- [Misc](lessons/misc.md) — 4 lessons
- [Testing & QA](lessons/testing-qa.md) — 16 lessons
- [Data, Content & Cultures](lessons/data-content-cultures.md) — 39 lessons
- [Harmony & IL (Patches, Transpilers, Prefixes, Patch Lifecycle)](lessons/harmony-il.md) — 22 lessons
- [Animation & Skeleton](lessons/animation-skeleton.md) — 16 lessons
- [State, Lifecycle & Save](lessons/state-lifecycle-save.md) — 13 lessons
- [XSLT & ModuleData](lessons/xslt-moduledata.md) — 16 lessons
- [Campaign Mechanics](lessons/campaign-mechanics.md) — 9 lessons
- [Localization & UI](lessons/localization-ui.md) — 13 lessons
- [Native C++ Port](lessons/native-cpp-port.md) — 3 lessons

---

## Referenced by

- [docs/features/race-age-system.md](../features/race-age-system.md)

<!-- backlinks-end -->

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/race-age-system.md](../features/race-age-system.md)
- [docs/reviews/lessons/adapters-taleworlds-api.md](lessons/adapters-taleworlds-api.md)
- [docs/reviews/lessons/animation-skeleton.md](lessons/animation-skeleton.md)
- [docs/reviews/lessons/build-tooling-workflow.md](lessons/build-tooling-workflow.md)
- [docs/reviews/lessons/campaign-mechanics.md](lessons/campaign-mechanics.md)
- [docs/reviews/lessons/data-content-cultures.md](lessons/data-content-cultures.md)
- [docs/reviews/lessons/gamemodels-services.md](lessons/gamemodels-services.md)
- [docs/reviews/lessons/harmony-il.md](lessons/harmony-il.md)
- [docs/reviews/lessons/localization-ui.md](lessons/localization-ui.md)
- [docs/reviews/lessons/misc.md](lessons/misc.md)
- [docs/reviews/lessons/native-cpp-port.md](lessons/native-cpp-port.md)
- [docs/reviews/lessons/state-lifecycle-save.md](lessons/state-lifecycle-save.md)
- [docs/reviews/lessons/testing-qa.md](lessons/testing-qa.md)
- [docs/reviews/lessons/xslt-moduledata.md](lessons/xslt-moduledata.md)

<!-- backlinks-end -->
