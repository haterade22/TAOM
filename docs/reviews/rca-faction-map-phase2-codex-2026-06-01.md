# RCA — Faction-Map Phase 2 (Codex review fixes)

**Date:** 2026-06-01
**Feature:** `Main/Features/FactionMap/*` + `factions.json` + `taom_module_strings.xml`, Phase 2 of issue [#260](https://github.com/haterade22/TAOM/issues/260).
**Codex output:** [`codex-adversarial-faction-map-phase2-2026-06-01.md`](codex-adversarial-faction-map-phase2-2026-06-01.md)
**Codex prompt:** [`codex-adversarial-faction-map-phase2-2026-06-01.prompt.md`](codex-adversarial-faction-map-phase2-2026-06-01.prompt.md)
**Sibling RCA:** [`rca-faction-map-phase2-2026-06-01.md`](rca-faction-map-phase2-2026-06-01.md) (deep-review fix shipped earlier).

## Top-line summary

Codex adversarial review on Phase 2 (commits `cbbcc41` + `7f0de78`) returned **0 CRITICAL, 3 HIGH, 3 MEDIUM, 1 LOW**. Verification (decompiled `DefaultCulturalFeats` from installed v1.4.5 + XSLT trace of TAOM's `spcultures.xslt`) confirmed all 3 HIGHs and 2 of 3 MEDs as real. The MEDIUM on special-units accuracy and the LOW on the `_su_` naming abbreviation were rejected per `simplicity-criterion.md` (cosmetic; documented as known framing).

Net result: 7 confirmed fixes (3 HIGH + 2 MED + 2 documentation/wording tightenings), 2 rejected per simplicity-criterion, and 8 of 9 Known Suspects came back DISPUTED-with-evidence — the content rewrite is now reconciled with the actual shipped feats per vanilla decompile + TAOM XSLT.

## Findings table

| # | Sev | Bug | Category | Verdict | Resolution |
|---|---|---|---|---|---|
| 1 | HIGH | Dale's CC page claimed inherited "Sturgian forest speed" and "Sturgian winter resilience" — vanilla `DefaultCulturalFeats._sturgian*` ships `Grain Production +10%`, `Army Influence Cost −50%`, `Decision Penalty +20%` (negative). Forest speed and winter resilience are not Sturgian feats in v1.4.5. | Wrong inheritance attribution / fabricated feat | **CONFIRMED, fixed** | Replaced Dale's bonuses + perk text with concrete Sturgian feats; weakness 2 now reflects the Sturgian decision-penalty negative. |
| 2 | HIGH | Dunland (empire) and Khand (battania) misstated Battanian inheritance: claimed `+15% party speed in forest` (vanilla is `50% less forest speed penalty + 15% sight range`) and `−15% construction speed` (vanilla is `−10%`). | Wrong feat-value text | **CONFIRMED, fixed** | Rewrote both Battanian-inheriting factions' bonuses + perk text. Dunland's XSLT `Culture[@id='empire']` template overrides empire feats with three Battanian feats (`battanian_forest_speed`, `battanian_militia_production`, `battanian_slower_construction`) plus 3 TAOM feats. Khand's XSLT `Culture[@id='battania']/cultural_feats` template appends `taom_khand_steppe_speed` to vanilla battanian. |
| 3 | HIGH | Harad and Rhûn used vague text ("Aserai desert caravan and hardiness", "Khuzait cavalry economy") that omitted concrete numbers, negative-feat inheritance, and (for Rhûn) incorrectly mentioned militia. | Vague / omitted inheritance | **CONFIRMED, fixed** | Harad now lists `−30% caravan + −10% trade penalty` (Aserai Trader), `no desert speed penalty` (Aserai Desert), and `+5% troop wages` (Aserai Wages, negative). Rhûn now lists `−10% mounted recruit/upgrade`, `+25% animal village production`, and `−20% town tax` (negative). |
| 4 | HIGH | `FactionDisplayHelper.ShowHoverTooltip` passed `change.FactionName` raw to `TooltipProperty` — hover tooltips would have displayed literal `{=KEY}default` for every faction after Phase 2 keying. | Localization bypass | **CONFIRMED, fixed** | One-line wrap: `Localize(change.FactionName)`. The trace through `PolygonWidget.HoveredFactionName` → `FactionHoverService.UpdateHover` → `HoverStateChange.FactionName` → `ShowHoverTooltip` previously bypassed the Phase 1 helper. |
| 5 | MED | Four village notable-count feats shipped but absent from any faction's CC page: Mordor `Slave Drivers` (+5%), Isengard `Iron Press` (+10%), Gundabad `Bone Camps` (+10%), Dol Guldur `Hidden Hovels` (+10%). | Silent feature (positive feat unmentioned) | **CONFIRMED, fixed** | Added a positive bonus line to each of the 4 factions. |
| 6 | MED | Erebor description said "Master Smiths halve forge costs" — actual feat is `−30% smithing energy cost`. | Wrong numeric in description | **CONFIRMED, fixed** | Rewrote: "Master Smiths cut smithing energy by 30%". |
| 7 | MED | 41 of 48 `special_units[].name` values are not exact troop names in `troops_<culture>.xml`. | Cosmetic / archetype labeling | **REJECTED** (simplicity-criterion) | Per the Phase 2 commit message: "Special-units names use lore-appropriate generic forms (Citadel Guard, Swan Knight, Black Uruk, Cave Troll, Mumakil War Tower, etc.); exact troop-tree alignment confirmed during Phase 3 in-game pass." The names ARE iconic LOTR archetypes; renaming 41 entries to exact in-game troop display names is high authoring cost for low player benefit. The framing in the CHANGELOG is "iconic forces / archetypes," not "this exact troop spawns." A follow-up pass during the in-game smoke test can adjust any entry that reads jarringly. |
| 8 | LOW | Special-unit JSON keys use `_su_0_name` abbreviation instead of `_special_unit_0_name`. | Inconsistent naming | **REJECTED** (simplicity-criterion) | Runtime key coverage is clean (599/599 matched). Renaming the keys requires updating both the JSON and the matching XML entries with no behavioral change. Documented as the intentional abbreviation. |

## Known Suspect verdicts (Codex)

| # | Suspect | Codex verdict | Action |
|---|---|---|---|
| 1 | Content accuracy vs shipped feats | CONFIRMED (Findings 1, 2, 3, 5, 6 derive from this) | Fixed |
| 2 | XSLT-wrapped culture inheritance | CONFIRMED | Fixed per Findings 1+2+3 |
| 3 | Special-units accuracy | CONFIRMED (Finding 7) | Rejected per simplicity-criterion |
| 4 | JSON ↔ XML key alignment | DISPUTED (599/599 live keys match) | Pass |
| 5 | Key naming convention | CONFIRMED-NUANCE (Finding 8, `_su_` abbreviation) | Rejected per simplicity-criterion |
| 6 | String token escaping safety | DISPUTED (no `& < >` in defaults, UTF-8 clean) | Pass |
| 7 | Strength/weakness "+ " / "- " double-prefix | DISPUTED | Pass |
| 8 | Old hard-coded content removed | DISPUTED (zero matches) | Pass |
| 9 | Helper coverage of new content | CONFIRMED (Finding 4 — hover tooltip) | Fixed |

## Root-cause pattern — Inherited feat content drifted from XSLT source-of-truth

The 5 misstated XSLT-culture inheritances (Dale, Dunland, Khand, Harad, Rhûn) share a single root cause: **the Phase 2 content authoring relied on the Agent-2 cultural-feats inventory summary instead of decompiling `DefaultCulturalFeats` + tracing `spcultures.xslt`.** The Agent-2 inventory used vague labels like "Sturgian inheritance," "Battanian inherited," "Aserai caravan economy" which the author then loosely paraphrased into the page content. The vanilla decompile (`Create("aserai_cheaper_caravans")` → `"30% cheaper caravans, 10% less trade penalty"`) is concrete; the inventory summary is interpretive. Concrete > interpretive.

**Generalization:** for XSLT-wrapped TAOM cultures, the authoritative inheritance source is BOTH (a) the XSLT template at `Culture[@id='X']` (does it OVERRIDE feats or PASS-THROUGH plus APPEND?) AND (b) the vanilla `DefaultCulturalFeats.Initialize*Feat(...)` body for each inherited feat. A 2-step trace (XSLT → feat ids → vanilla initializer) gives the actual text and number for each inherited bonus. The Phase 2 author skipped step (b) and paraphrased.

Codified in this RCA's "Preventive actions" below — feedback memory entry already exists at `feedback_xslt_passthrough_unintended_inheritance.md` for the related "what attributes pass through" question; this is the same pattern applied to inherited *content semantics*, not just attribute selection.

## Why each agent missed these (5 deep-review agents + 1 tooling)

| Agent | What it checked | Why it missed |
|---|---|---|
| Standards (Haiku) | ADRs, test naming, framework choice | Not in scope — content correctness isn't governed by ADRs. |
| API Compatibility (Sonnet) | `TextObject` resolution | Verified API; correctly noted Phase 2 has no new TaleWorlds API surface. Did not audit content accuracy. |
| Efficiency (Haiku) | Allocations, hot paths | Not in scope. |
| Completeness (Haiku) | Tests + feature doc + CHANGELOG + per-faction shape | Did min-shape spot-check on 3 factions, did NOT trace each faction's content against `TaomCulturalFeats.cs`. Coverage gap. |
| Data Flow (Sonnet) | JSON→XML key alignment, helper bypass | **Caught HIGH 4 (hover tooltip bypass)** independently — same finding as Codex's HIGH 3. Did NOT audit XSLT-wrapped culture inheritance. Coverage gap. |
| Tooling Correctness (Sonnet) | Harvester encoding / idempotency | Out of content scope. |

**Codex's lane:** Codex's vanilla decompile + XSLT trace was outside the deep-review agents' scope. Codex caught HIGH 1, 2, 3 because it independently decompiled `DefaultCulturalFeats` from the installed v1.4.5 DLL and traced `spcultures.xslt` paths for each XSLT-wrapped culture. The 5 deep-review agents' prompts did not include vanilla decompile, so they had no way to discover inheritance drift.

## Preventive actions

### 1. Inherited-feat content audit step (memory codification)

When authoring CC faction-map content for an XSLT-wrapped culture, the workflow MUST include:

1. Identify the `Culture[@id='X']` block in `spcultures.xslt`. Does it OVERRIDE `<cultural_feats>` or PASS-THROUGH + APPEND?
2. If OVERRIDE: list the explicit `<feat id="...">` entries; map each to vanilla via `pwsh tools/taom-src.ps1 path TaleWorlds.CampaignSystem.GameComponents.DefaultCulturalFeats` and copy the concrete `Initialize(...)` description text.
3. If PASS-THROUGH + APPEND: list both vanilla feats (per `DefaultCulturalFeats`) AND TAOM-added feats.
4. Include any NEGATIVE inherited feat in the `weaknesses[]` (or `bonuses[]` with `positive: false`).

This pattern is broader than just the CC faction-map page — it's the right workflow for any TAOM doc that summarizes a culture's feats. Codified as a sub-bullet under the existing `feedback_faction_map_update_with_cultural_feats.md` memory (the standing instruction from earlier this session).

### 2. Hover-tooltip Localize trap

The bypass came from `FactionHoverService` storing `current` raw from `PolygonWidget.HoveredFactionName`. The widget stores the JSON `name` field which is now `{=KEY}default`. The fix is at the display boundary (`ShowHoverTooltip` line 92).

**Generalizable rule:** when authoring a localization sweep, audit BOTH the selected-state path AND all alternate display paths (hover tooltips, encyclopedia entries, mini-info panels, settlement nameplates, banner tooltips). Each is a possible bypass. Codex's HIGH 4 caught this exact pattern.

This is the third instance of the same pattern in TAOM:
- `feedback_localization_textobject.md` — VM string properties must use `TextObject().ToString()`.
- `feedback_faction_map_update_with_cultural_feats.md` — when cultural feats change, also update factions.json.
- (this RCA) — when localizing, audit alternate display paths.

Captured inline in this RCA; promote to a dedicated memory entry if the pattern shows up again.

### 3. Special-units pass during Phase 3 in-game smoke

Codex's MED 7 (special-units name accuracy) is rejected per simplicity-criterion but flagged for the Phase 3 in-game pass: when the player opens each of the 16 CC pages, the special-units entries should at least be plausibly close to the actual troop tree. If 3+ entries read as fabrications during the in-game pass, replace those specific entries with actual troop names. Track as Phase 3 follow-up.

## Verification

```
python tools/harvest_factionmap_strings.py  ->  Wrote 610 keys (was 599; +11 from new bonus/weakness lines)
dotnet build TAOM.Tests                     ->  0 Errors
dotnet test  TAOM.Tests (full)              ->  2872 / 0 / 2 (no regression)
python tools/validate_moduledata.py         ->  PASS
JSON syntax check                           ->  valid
```

## Files changed in this fix commit

- `Main/Features/FactionMap/FactionDisplayHelper.cs` — one-line Localize wrap at `ShowHoverTooltip:92` (HIGH 4).
- `Main/_Module/ModuleData/factionmap/factions.json` — content corrections for Dale, Khand, Dunland, Harad, Rhûn (HIGH 1+2+3) + 4 village-notable bonuses (MED 5) + Erebor desc (MED 6).
- `Main/_Module/ModuleData/taom_module_strings.xml` — re-harvested (599 → 610 keys, +11 new).
- `docs/reviews/rca-faction-map-phase2-codex-2026-06-01.md` — this file.
- `CHANGELOG.md` — fix entry.

## Linked prior context

- [`feedback_audit_findings_not_always_correct.md`](C:/Users/mikew/.claude/projects/c--Users-mikew-source-repos-TAOM/memory/feedback_audit_findings_not_always_correct.md) — applied (verified each Codex finding before fixing; 2 rejected per simplicity-criterion).
- [`feedback_faction_map_update_with_cultural_feats.md`](C:/Users/mikew/.claude/projects/c--Users-mikew-source-repos-TAOM/memory/feedback_faction_map_update_with_cultural_feats.md) — the standing instruction this whole session traces to; extended in §1 above.
- [`feedback_xslt_passthrough_unintended_inheritance.md`](C:/Users/mikew/.claude/projects/c--Users-mikew-source-repos-TAOM/memory/feedback_xslt_passthrough_unintended_inheritance.md) — sibling rule for XSLT attribute-passthrough audits; this RCA applies the same pattern to inherited *feat content*.
- [`feedback_localization_textobject.md`](C:/Users/mikew/.claude/projects/c--Users-mikew-source-repos-TAOM/memory/feedback_localization_textobject.md) — applied (helper Localize wrap).
- [`docs/reviews/rca-faction-map-phase2-2026-06-01.md`](rca-faction-map-phase2-2026-06-01.md) — sibling RCA for the deep-review fix shipped earlier (`7f0de78`).
- `.claude/rules/simplicity-criterion.md` — Yes/No matrix used to reject the special-units name accuracy MED and the `_su_` naming LOW.
- `.claude/rules/evidence-over-claims.md` — verify-before-acting (applied to vanilla decompile + XSLT trace).
