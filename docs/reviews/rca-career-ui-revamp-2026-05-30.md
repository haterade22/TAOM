# RCA — Career Screen Revamp deep-review (2026-05-30)

Deep-review of the career-screen revamp (Phase A prefab/VM + Phase B group names + Phase C rank titles). 6 agents (Standards, API-compat, Efficiency, Completeness, Data-flow, Tooling-correctness). **No HIGH/CRITICAL findings.** Standards PASS, API-compat 11/11 verified, efficiency PASS, completeness COMPLETE, data-flow all bindings connected. Findings below are LOW / pre-existing.

## Findings

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|-----|---------|----------|------------|-------------------|
| 1 | LOW (latent) | `apply_career_group_names.py` / `apply_career_rank_names.py` read `utf-8-sig` but wrote `utf-8`, dropping a BOM if one were present. | Tooling / convention | New scripts written quickly; mirrored the pre-existing `apply_career_group_names.py` read/write idiom without applying `tools/README.md`'s detect-BOM-as-bytes convention. | **FIXED this session** — both scripts now read bytes, detect `\xef\xbb\xbf`, and re-emit it on write. Verified empirically the live data files never had a BOM (no corruption occurred). |
| 2 | LOW (pre-existing) | 29 career `portrait_sprite` values have no `TAOMSpriteData.xml` entry and no PNG → blank portrait widget for those careers. | Content gap | Not introduced by this revamp — only ~21 of 49 careers ever had portrait art. The prefab gates on `@HasCareer`, not on sprite existence (silent-blank, no crash). | Documented as a **known limitation** (CHANGELOG + feature doc). Out of scope for a UI/data revamp; tracked for future art work. No code change. |
| 3 | LOW | `CareerChoiceObjectVM.IconSprite` became dead when the prefab rewrite hardcoded `Sprite="CareerSystem\career_point_pip"` instead of binding `@IconSprite`. (Other dead props — `Name`, `IsKeystone`, `Tier`, `IsExpanded`, `IsLocked` — pre-date this work.) | Dead code | Intentional design change (one shared pip sprite vs per-choice icons); the now-unused property was left in place. | Left as-is this pass (per edit-scope discipline, not churning pre-existing dead props; the single orphan is harmless). Noted for a future `/deslop` pass on the CareerSystem VMs. |
| 4 | LOW | `locked_gate_bottom/top/full` sprite registrations remain in `TAOMSpriteData.xml` after the prefab stopped referencing them. | Orphaned registration | The revamp removed the gate sprites from the prefab (replaced with "Requires Level N" text); the manifest entries + PNGs were intentionally left so gates can be restored if desired. | Left as-is (non-breaking; Bannerlord doesn't error on unreferenced registered sprites). Noted. |

## Root-cause themes

- **No systemic bug.** The revamp's data-flow (XML → domain → VM → prefab), sprite-manifest edit, and 441 localization keys all traced clean; the 3-state pip and rank-name wiring are correct and tested.
- **Finding 1** is the only one with a generalizable lesson, and it's already a documented rule (`feedback_xml_tool_bom_io_convention.md`) that the new scripts didn't follow because they copied the older `apply_*` idiom. The fix was applied; the older sibling scripts predate the convention and are out of scope here. No new rule needed — the existing memory already covers it; the gap was application, not knowledge.

## Why each agent's scope behaved as expected

- **Standards / API-compat / Efficiency / Completeness** correctly PASSED — the C# is convention-clean, uses verified v1.4.5 APIs, is not a hot path, and is fully tested + documented.
- **Data-flow** caught the pre-existing portrait gap (finding 2) and the dead/orphaned registrations (3, 4) — exactly its job; it correctly classified all as non-breaking and noted #2 as pre-existing.
- **Tooling-correctness** (the 6th agent, added per Step 2c because the changeset includes file-writing Python) caught finding 1 — the C#-centric core agents do not review script tooling. This validates the Step 2c trigger.

## Codex adversarial pass (independent)

`/review-codex` dispatched Codex (gpt-5.5, xhigh) on the same changeset. **VERDICT: CLEAN — 0 CRITICAL / 0 HIGH / 0 MEDIUM / 0 LOW.** All 7 Known Suspects DISPUTED with evidence:
- **Sprite-atlas sheet-2 (the highest-risk item):** Codex decompiled the installed v1.4.5 `TaleWorlds.TwoDimension.dll` and confirmed `SheetID` is scoped per-category and each sheet carries its own `Vec2i` size (`_category.SheetSizes[SheetID-1]`), with no requirement that sheets in a category share dimensions — so the 4096² sheet 1 + 256² sheet 2 + pip-at-(0,0) manifest is valid. Independently corroborates the runtime-build-from-loose-PNG model (no baked sheets in repo).
- Binding table (35 rows) all CONNECTED; no orphaned gate bindings. Loc: 288 active group attrs / 147 rank attrs / 435 referenced keys / 0 missing / 0 duplicate IDs / well-formed. IsTierAvailable differs from old only for `heroLevel <= 0` tier 1, which real heroes never hit (start at level 1; `OpenCareerScreen` early-returns on null `MainHero`). Ctor back-compat, IsUnavailable notification, commented-block injection all confirmed safe.

Codex produced no false positives and missed nothing the deep-review caught — its sprite-loader decompile strengthened the one assumption the Claude agents took on inference. Output: `docs/reviews/codex-adversarial-career-ui-revamp-2026-05-30.md`.

> **Correction (2026-05-31, post in-game test):** the Codex "sprite-atlas sheet-2" conclusion above was WRONG on two counts, only exposed by running the game + decompiling the generator. (1) There is no runtime-build-from-loose model and no sheet 2 — the offline `SpriteSheetGenerator.exe` packs the pip onto sheet 1 and writes `AssetSources/GauntletUI/<cat>_<n>.png` + `Assets/GauntletUI/<cat>_<n>_tex.tpac` (there is no `pack0.tpac` for UI sprites). (2) "No baked sheets in repo" was false — the repo tracks `AssetSources/` + `Assets/`. Both the deep-review and Codex verified manifest *shape* and inferred a render model that the live game disproved. See corrected finding A below + `gui-sprite-system.md` "The sprite-bake pipeline" (decompile-verified). This is the canonical example of why a CLEAN static review cannot certify a sprite renders.

## Verdict

READY. Both the 6-agent deep-review and the independent Codex pass returned no HIGH findings. The one actionable item (injector BOM convention) was fixed and verified idempotent. Findings 2–4 are pre-existing or intentional-design and documented as known limitations. Final `dotnet test TAOM.Tests` → 2698 passed / 0 failed / 2 skipped.

The only residual is an **in-game render check** of the new pip sprite (requires a normal deploy — this session's builds used `DisableModuleCopy` — plus the live game). If the pip ever renders blank, the fix is to regenerate the category's sprite sheets via the in-engine UI editor; the manifest + loose PNG are already correct.

## Post-review in-game findings (2026-05-31)

In-game testing after the reviews surfaced 4 issues — confirming that some classes of bug are reachable only in the live game, never by static review.

| # | Sev | Finding | Why both reviews missed it | Fix / preventive action |
|---|-----|---------|----------------------------|-------------------------|
| A | HIGH | **Pip renders blank — TWO causes.** (1) *Not baked:* the pip is a NEW loose PNG; the offline `SpriteSheetGenerator.exe` had never packed it, so the compiled sheet had no pip pixels → blank. (2) *Baked but invisible:* even after the user ran the generator (pip correctly baked to sheet 1 at `SheetX=2428 SheetY=1670`, confirmed by cropping the regenerated `AssetSources` PNG), it stayed blank — the **prefab** drew it at `22×28px` / `Color="#FFFFFF45"` (27% alpha), a thin gold ring that reads as faint embossing on a near-black node. | **Wrong mental model + wrong RCA.** Deep-review + Codex verified only the manifest *shape* and assumed runtime-build-from-loose. The first RCA then asserted "no `pack0.tpac` `_2` → regen fixes it" — **doubly wrong:** there is NO `pack0.tpac` for UI sprites (atlases are per-category `AssetSources/GauntletUI/<cat>_<n>.png` + `Assets/GauntletUI/<cat>_<n>_tex.tpac`), AND regen alone did NOT fix it because the real blocker was the prefab render, not the bake. Verified by decompiling `SpriteSheetGenerator.Library.dll`. | (1) Run `SpriteSheetGenerator.exe` to bake (done by user). (2) **Prefab fix (the one that mattered):** pip → `38×38`, opacities `#FFFFFFFF`/`#FFFFFFE0`/`#FFFFFF78`. Corrected `gui-sprite-system.md` ("The sprite-bake pipeline" — decompile-verified, no `pack0.tpac`) + memory `feedback_sprite_atlas_baked_regen_required` (TWO failure modes). Lesson: **baked ≠ visible**; both the asset bake AND the prefab render must be verified, and only the live game confirms either. |
| B | MED | **Plural titles.** Group/rank names were plural collectives ("Wardens of the East Bank"); it's a single-player career, so the player's title should be singular. | Out of scope for code review — a naming/voice judgment, not a correctness bug. The naming agents produced collective-style names by default. | Singularized the title head noun across 109 names via `tools/singularize_career_names.py` (head-noun rule, preposition-aware so objects like "Trees"/"Stars" stay plural); re-injected + updated 294 source strings. |
| C | LOW | **Tier rank label indented/wrapped** ("Ohtar of the Crossing" not flush-left like "Warden of Osgiliath"). | Visual-only; not detectable without rendering. The `Fixed`-width-200 label centered its text, so a longer title wrapped and looked indented. | Tier labels → `WidthSizePolicy="CoverChildren"` + `HorizontalAlignment="Left"` (single-line, flush). |
| D | LOW | **Locked tiers (T2/T3) node spacing ≠ Tier 1** (headers collided — "Vanguard of the Ramn**Swords** of the Last Bridge"). | Visual-only. Locked tiers hide the `+`/`−` column (`IsVisible="@IsActive"` on the whole panel), collapsing node width so the two nodes sat closer than T1 and their 220px headers overlapped. | Button column → fixed 70px width always reserved; gate only the buttons on `@IsActive`. All three tiers now space identically. |

| E | LOW | **"Requires Level N" not centered between the T2/T3 node columns** (sat off-center). | Visual-only. *Two attempts:* `StretchToParent`→`CoverChildren`+`Center` was a **no-op** (it centers on the row center, exactly where the label already sat). The real cause is the asymmetric **70px `+`/`−` button reserve** on each node's right (kept on locked tiers per finding D), which shifts both node boxes ~35–40px left of the row center. | `PositionXOffset="-40"` on the locked-tier (T2/T3) labels so they land in the actual gap between the boxes. Confirmed centered in-game. |
| F | LOW | **Hover perk descriptions not inline with the pips** (descriptions floated centered in the node and drifted out of row with their pips). | Visual-only. The pip strip and the perk-description list were two parallel `{Choices}` lists with mismatched fixed row heights (pips 46px after the enlargement vs descriptions 40px) and brush-centered text, so they desynced row-by-row and the text centered instead of sitting beside its pip. | Description rows → 46px (match the pip rows); description text → `CoverChildren` + left-align (sits immediately right of the pip; brush-centering bypassed). Each description now shares its pip's row. Confirmed in-game. |

**All findings A–F confirmed fixed in-game (user screenshots, 2026-05-31):** pip renders as a gold ring, T2/T3 level labels centered between the columns, hover descriptions aligned row-by-row with the pips, tier titles flush-left and singular.

**Iteration note (finding A, 2026-05-31):** the *first* RCA for finding A was itself wrong — it concluded "regenerate the sheets and the pip will render," citing a non-existent `pack0.tpac`. The user ran the generator; the pip stayed blank. Only then did decompiling `SpriteSheetGenerator.Library.dll` + cropping the regenerated sheet reveal the bake was fine and the prefab (size/opacity) was the real blocker. Lesson-on-the-lesson: after a fix you *claimed* would work is tested and fails, re-verify from ground truth before re-asserting — don't iterate on the original wrong model. (`evidence-over-claims.md` §C.)

**Systemic lesson (finding A):** the recurring trap is concluding a rendering/asset fact from static evidence. "Manifest valid" ≠ "sprite renders." Reviews verify structure; only the game verifies pixels. The preventive rule (`feedback_sprite_atlas_baked_regen_required`) generalizes: any change whose payoff is *visual rendering of a new asset* must be flagged in-game-only, and a CLEAN review must not imply it will display. Findings B–D are inherently in-game-only (voice + layout) and not review-catchable; no rule change warranted beyond noting that live UI review remains mandatory after a prefab/naming change. Findings E–F reinforce this for layout specifically: a *plausible* fix (the `CoverChildren`+`Center` centering) was a **no-op** until the live game exposed the real cause (the 70px button-reserve asymmetry; and a parallel-list row-height mismatch for the descriptions). Gauntlet layout must be confirmed by rendering, not reasoned about from the prefab alone — the same "verify, don't conclude from static evidence" discipline as finding A, applied to geometry.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/career-system.md](../features/career-system.md)

<!-- backlinks-end -->
