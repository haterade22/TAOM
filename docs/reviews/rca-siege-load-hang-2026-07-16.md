# RCA — siege load hang: two physics-body typos, and the validator that never read the file (2026-07-16)

**Scope:** issue #352 — a user field report of a permanent siege-load hang (100% of one core, no crash, no error log). Two `body_name` typos in `LOTRLOME_Armory` v2.0.8 `LOTRLOME_crafting_pieces.xml`; scope + Tier-C fixes in `tools/validate_mesh_refs.py`. No TAOM C# changed.

**Result: 2 confirmed data defects, 1 confirmed cause promoted from hypothesis, and 3 process failures — two in the tooling, one in this session's own method.** The reporter's diagnosis located the right line and drew the wrong conclusion. TAOM's purpose-built validator had been reporting PASS on this bug for a year.

## The bug

`PreloadHelper.WaitForMeshesToBeLoaded` polls every registered physics-body name and only exits once each resolves. One unresolvable name = an infinite loop on the main thread. Not a crash — a hang, so the `CrashReport` pipeline never fires and there is nothing in the log.

| # | Ref (as shipped) | Should be | Reachable via | Blast radius |
|---|---|---|---|---|
| 1 | `bo_dunland_caerdh_sword_blade_2h` | `..._2h_a` — the `mesh` attr carried the `_a`, the `body_name` didn't | troop equipment (`dunland_bat_template_medium_c`, 2 rosters) | **every siege with Dunland troops** |
| 2 | `bo_wm_harad_spear_a02_blade` | `..._a02_head` — Harad spears use `_head`; `_blade` is the sword/glaive suffix, copy-pasted | crafting-template `UsablePiece` | only players who craft that spear head — which is why it was never reported |

Both assets ship correctly in `pack1.tpac`. Only the refs were wrong. These were the **only** two unresolved body refs in the module; everything else resolves against Native.

This **confirms** the hypothesis `validate_mesh_refs.py` was built for in #262 ("a missing `bo_` collision mesh causes intermittent infinite battle-load hangs"). It is now a demonstrated cause, not a suspect — though not an exclusive one, and note it hung in *preload*, not agent-spawn as the original hypothesis guessed.

## Finding 1 — the reporter's root cause was wrong, and the fix deleted working content

The report concluded the physics body "does not exist in any asset package shipped with LOTRLOME_Armory v2.0.8 or any other loaded module", and worked around it by swapping the sword for an axe in their submod's `Replacements` dictionary.

The evidence for "missing" was real but misread: a binary scan for `bo_dunland_caerdh_sword_blade_2h` across all 238 tpacs returns nothing. The name one character longer — `bo_dunland_caerdh_sword_blade_2h_a` — is present in `pack1.tpac` **and** in the source geo `dunland_caerdh_weapons_a_geo.tpac`, the same file carrying the axe blades that work. Every sibling Dunland piece uses a variant suffix (`..._axe_blade_2h_a`, `..._2h_b`, `..._spear_head_a`). The convention is even documented in `weapon-creation-workflow.md`: `bo_<same full mesh id>`.

**Why missed:** "the string is in no tpac" and "the asset was never shipped" are not the same claim, and the scan can't distinguish them. A near-match check — one that asks *is something almost-this-name shipped?* — separates them instantly and was never run.

**Preventive action:** `validate_mesh_refs.py` now reports missing bodies against the exact packaged set, so the near-match is trivially visible; the feature doc, `weapon-creation-workflow.md`, and the lesson all state the rule: **a missing asset is a typo until proven otherwise — look for a near-match before deleting content.**

## Finding 2 — the validator built for this exact bug never read the file containing it

`tools/validate_mesh_refs.py` (#262, 2026-06-01) is body-aware, purpose-built for this hypothesis, and **catches both typos at the exact lines** — `LOTRLOME_crafting_pieces.xml:5871` and `:14617` — when pointed at the right directory.

Its `DEFAULT_ITEMS` was `ModuleData/LOTRLOME_items/`. Crafting pieces live in `ModuleData/LOTRLOME_crafting_pieces.xml`, one level up. The tool reported PASS the entire time the bug was live.

**Why missed:** the scan root was derived from where *items* live, and crafting pieces are not items — they are a different element in a different file that happens to be the **only** file referencing mesh + collision names (`weapon-creation-workflow.md` says exactly this, in a doc the tool's author would have had no reason to read). Nothing cross-checked the tool's scan root against the set of files that actually carry `body_name`. A clean PASS was read as "no missing bodies", when it only ever supported "no missing bodies **in the scanned scope**" — and the report's own interpretation footer actively reinforced the stronger reading by printing that the hypothesis was "WEAKENED".

**Preventive action:** default widened to `ModuleData/`; the clean-run footer now names the scope trap instead of claiming the hypothesis is weakened; the `SCANNED SCOPE` wording is pinned by a test (`test_clean_report_says_pass_and_warns_about_scope`, which also asserts `WEAKENED` is absent).

## Finding 3 — Tier C was coarse for a year on an assumption nobody tested

The tool's header asserted, as fact: *"`bo_` collision bodies are NOT in the .tpac TOC (they live embedded in mesh metadata), so scan the raw .tpac bytes."* Tier C was therefore a raw-byte substring scan, explicitly documented as **coarse** and needing rgl_log confirmation.

They are in the TOC. `PhysicsShape` is a first-class item type (TYPE_GUID `e8528e0e-64b6-4e61-bae0-7569c0452aea`); `pack1.tpac` exposes **382** of them. The count agrees across two independent implementations — a hand-rolled GUID parse in this tool, and TpacTool's own `PhysicsShape.TYPE_GUID` read via reflection.

**Why missed:** the assumption was plausible (bodies *are* authored alongside mesh geometry), was written into a docstring as settled fact, and the byte-scan it justified *worked* — it found the typos in the reproduction. A wrong premise that produces correct-looking output generates no pressure to test it. Nothing in the tool asked "what item types does this TOC actually contain?", which is a two-line query.

**Preventive action:** Tier C matches the exact `PhysicsShape` set, with the byte scan retained only as the degraded fallback for packs that soft-fail to parse (so an unreadable pack still never produces a false `MISSING_BODY`). New test builds a synthetic `PhysicsShape` TOC entry and asserts it is collected as a body and not as a mesh.

## Finding 4 (process, this session) — a false-negative grep sent the fix to the wrong tool

This session ran `grep -rln "body_name|tpac" tools/`, got **empty output**, concluded no validator covered `body_name`, and extended `tools/Audit-MeshRefs.ps1` instead: PhysicsShape enumeration, `AssetPackages/` scanning, vanilla-resolution, a report section, an end-to-end verification that it caught the reverted typo. All of it duplicated a tool explicitly documented as its successor. The work was reverted (`git checkout`) once `tools/README.md` was read and the overlap surfaced.

The grep was simply wrong — `tools/README.md` contains `body_name` on the very line describing `validate_mesh_refs.py`.

**Why missed:** an empty result from a search tool reads as *evidence of absence*, and nothing was spent distinguishing "no matches" from "the search didn't work". This is the reuse-ladder failure (`think-before-coding.md`): rung 2 ("does an existing TAOM service/tool already do it?") was answered by a single unverified command instead of by reading the tool catalogue.

**Preventive action:** lesson filed — **confirm a negative grep with a positive control** before building on it. The cost here was bounded (one revert) only because the docs named a successor; had `tools/README.md` been silent, TAOM would now carry two overlapping body validators.

## Root-cause pattern: the guard existed, was correct, and was pointed slightly wrong

Findings 2, 3, and 4 are one shape. In each, the safeguard was **present and functional**, and failed on a parameter nobody re-examined:

- The validator's logic was right; its **scan root** was one directory off.
- Tier C's implementation was right; its **premise** about the container was wrong.
- The reuse check was right; its **search** silently returned nothing.

None of these are logic bugs, so no amount of reading the logic finds them — and all three produced *confident, clean-looking output* while wrong. A tool reporting PASS, a tier reporting hits, a grep reporting nothing: each one looked like an answer. The lesson generalizes past this bug: **when a purpose-built check says clean and the defect is still live, suspect its inputs — scope, premises, and search — before its logic.**

The corollary for a validator's UX: it must never phrase a clean result more strongly than its inputs support. The "hypothesis WEAKENED" footer is the sharpest artifact here — the tool actively argued the *correct* hypothesis was losing support, on evidence it had never actually gathered.

## Verification

| Check | Result |
|---|---|
| Both typos flagged pre-fix | `MISSING_BODY` at `LOTRLOME_crafting_pieces.xml:5871` + `:14617` (throwaway copy carrying both original typos) |
| Live files, default scope | PASS — 0 missing bodies across 785 body refs; Tier C reports `EXACT` |
| `PhysicsShape` count | 382 in `pack1.tpac`, agreeing across two independent implementations |
| Both fixed bodies resolve | present in `pack1.tpac`; XML parses; old typo strings absent |
| `python -m pytest tools/tests/` | 213 passed (mesh-ref suite 30 → 33) |
| `python tools/validate_moduledata.py` | PASS |
| `python tools/lint_docs.py --fail-on-drift` | exit 0 (a CLAUDE.md 443-char row, over the 400 cap, was caught here and trimmed) |
| **In-game siege load with Dunland troops** | **OWED** — the real gate; the Armory needs a version bump + release for players to get the fix |

## Lessons filed

`docs/reviews/lessons/xslt-moduledata.md` — *"A validator's SCOPE is part of its correctness — a clean PASS only ever means 'clean within the scope you pointed it at'"*, carrying all three traps (typo-until-proven-otherwise, verify a tool's assumptions, confirm a negative grep).

## Not adopted

The reporter's v0.5.0 also Harmony-prefixes `WaitForMeshesToBeLoaded` with a 30s-timeout wait loop that drops unresolvable shapes and continues. Reasonable resilience for a third-party submod; wrong for TAOM. It replaces an engine loop wholesale and converts a loud, findable hang into quiet missing-collision behavior — trading a bug that reports itself for one that doesn't. TAOM's answer to this class is the validator, which catches it before ship.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/reviews/REVIEW-LOG.md](./REVIEW-LOG.md)

<!-- backlinks-end -->
