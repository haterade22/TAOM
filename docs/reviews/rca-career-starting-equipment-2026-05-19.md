# RCA — Career Starting Equipment (2026-05-19)

## Top-line summary

Feature shipped with one CRITICAL behavioral bug (player spawns naked despite the roster XML being correct) and three lower-severity quality issues. The CRITICAL bug was caught by `/deep-review` (Agent 2 + Agent 5) BEFORE in-game testing — but only AFTER the file was already written to the wrong path. The user's in-game test confirmed the deep-review finding in parallel.

The cluster of findings shares one systemic lesson: **Write tool path handling for paths containing `&` silently writes to a URL-entity-encoded phantom directory**, and the only way to detect that this happened is to `ls` the real target path before declaring success. The cache state inside the Write tool's success message ("File created successfully at: ...&amp;...") is not the same as the filesystem state.

## Findings table

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|-----|-----|----------|------------|-------------------|
| 1 | **CRITICAL** | 15 starter armor item IDs unresolvable at runtime → player spawns naked | Cross-module XML reference gap | Write tool reported success at `E:/Steam/.../Mount &amp; Blade II Bannerlord/...` (URL-entity-encoded `&amp;` instead of literal `&`), creating a phantom directory tree. The success message reflected the encoded path, not the filesystem state. No post-write `ls` verification on the real target. | **Feedback memory:** when writing to paths under any of the harness's "Additional working directories" that contain `&`, immediately verify with `ls "<actual-path>"` before declaring success. The Write tool's reported path string is not authoritative — the filesystem is. |
| 2 | HIGH (would've shipped) | Ranged + infantry rosters silently inherit a Horse from culture-default roster because `Equipment.FillFrom` doesn't clear unspecified slots | Bannerlord API semantic gap | The implementation reused the `FillFrom` pattern that `PlayerEquipmentService` already uses for culture-default, assuming "second apply overwrites." It actually MERGES — `FillFrom` only writes slots present in the source. Agent 5 (Data Flow) caught this. | **Feedback memory:** `Equipment.FillFrom` is a slot-by-slot merge, not a wholesale replacement. Any second roster apply on top of a first roster needs explicit empty entries (`<Equipment slot="X" id=""/>`) for any slot it intends to be empty. |
| 3 | MEDIUM | `GetCareerArchetypeMap()` allocated a new dict on every call (twice during IoC startup) | GC pressure / startup allocation | The implementation lifted directly from the previous explicit-Register pattern. The single-source-of-truth refactor was correct but didn't memoize. Agent 3 (Efficiency) caught it. | No new rule — existing rule "cache repeated expensive lookups" covers this. |
| 4 | LOW | Misleading "menu service is authoritative" comment in `CharacterCreationContentService` | Code clarity | The comment was written defensively to explain why we don't re-query `ICareerDataService` — but the wording over-claimed. Agent 5 (Data Flow) flagged it. | No new rule — code review judgment. |
| 5 | LOW | Redundant `?.` on constructor-injected `_careerMenuService` | Defensive programming smell | Carried over from existing usage pattern in `AssignCareer`. Agent 5 (Data Flow) flagged it. | No new rule — existing `feedback_no_service_locator_in_services.md` covers the "constructor-injected fields cannot be null at runtime" principle. |
| 6 | **HIGH (post-#1)** | Leg + glove items missing `covers_legs="true"` / `covers_hands="true"` — meshes would not render even after path fix from #1 | LOTRLOME armor schema gap | I copied the body-item attribute pattern (`covers_body="true"`) but didn't check the per-slot equivalent on leg/glove items. The vanilla-LOTRLOME source items I duplicated from explicitly carry these attributes and the engine treats their absence as "this item does not cover this slot" — no mesh rendered. Caught by user in-game testing AFTER the path fix from #1, NOT by any deep-review agent. | **Feedback memory:** `feedback_lotrlome_armor_cover_attributes.md` — every LOTRLOME leg item needs `covers_legs="true"`, every glove item needs `covers_hands="true"`. Without these the slot's mesh does not render. Cross-check by grepping existing per-slot files; the attribute is universal in the source data. |

## Root-cause pattern (#1 deserves its own section)

The CRITICAL bug is a NEW pattern this repo has not hit before: **Write tool entity-encoding paths with `&`**. It is NOT a Bannerlord API issue, NOT a TAOM architecture issue, NOT a deep-review-scope gap — it's a harness/tool issue at the file-creation boundary.

The bug shape:
1. Pass `E:\Steam\steamapps\common\Mount & Blade II Bannerlord\...` to the Write tool.
2. Tool succeeds and reports `File created successfully at: E:\Steam\steamapps\common\Mount &amp; Blade II Bannerlord\...` — note the `&amp;` in the response.
3. The file is at the encoded path, NOT the literal-ampersand path.
4. Game reads the literal-ampersand path. File not present. Silent null resolution. Player spawns naked.

The reported-path-is-not-actual-path failure mode is detectable only by a post-write `ls` on the literal expected path. Sub-bugs of this same class are likely to recur for any harness "Additional working directory" with `&` or other XML-special characters.

**Why each agent missed (or caught) finding #1:**

- **Agent 1 (Standards):** PASS — the code itself is correct; the bug is data layer.
- **Agent 2 (Bannerlord API):** ✅ **CAUGHT** — explicitly grepped for the 15 `starter_*_gondor_*` item IDs in `LOTRLOME_Armory` and reported all 15 missing. Recommended verifying.
- **Agent 3 (Efficiency):** N/A — not in scope.
- **Agent 4 (Completeness):** PASS — checked that the file existed, didn't verify items resolved.
- **Agent 5 (Data Flow):** ✅ **CAUGHT** — independent of Agent 2, traced XML id="X" references and identified the same gap with the additional note that `MBObjectManager.GetObject<ItemObject>` returns null silently.

Both agents that *should* have caught it *did* catch it. The bug was confirmed by user's in-game test before I had read the review reports — the review and the test arrived at the same conclusion in parallel. The systemic fix is at the Write-tool boundary, not in the review scope.

## Feedback memories to codify

Two new memories worth writing:

**`feedback_write_tool_ampersand_path_encoding.md`** — When using the Write tool to create files at paths under harness "Additional working directories" containing `&` (or other XML-special characters), the tool's reported success path may not match the actual filesystem path. The Write tool entity-encoded `&` to `&amp;` in the LOTRLOME_Armory write on 2026-05-19, creating a phantom `Mount &amp; Blade II Bannerlord` directory tree alongside the real one. The reported path string is not authoritative. Always follow Write with `ls "<actual-path>"` to confirm the file is at the expected location, especially for files that won't be auto-validated by a build step. Symptom in this case: player spawned naked despite review showing the XML was correct — the file existed at a path the game never reads.

**`feedback_lotrlome_armor_cover_attributes.md`** — Every LOTRLOME leg item must declare `covers_legs="true"` and every glove item must declare `covers_hands="true"` on its `<Armor>` element. Without these, the engine equips the item but doesn't render its mesh — the player appears bare-legged / bare-handed despite the item being present in the slot. The pattern is universal in existing LOTRLOME source data, so the failure mode is "duplicating an item without preserving the cover attribute." Bare-bones grep check: `<Armor leg_armor=` lines in LOTRLOME leg files always have `covers_legs="true"`; `<Armor arm_armor=` lines in glove files always have `covers_hands="true"`. The 2026-05-19 career starting equipment shipped without these attributes and surfaced as bare legs/hands AFTER the path-encoding bug (#1) was fixed.

The FLOW-7 finding (FillFrom merge vs replace) could also become a memory, but it's adequately captured in the feature doc's new "Critical: FillFrom does NOT clear unspecified slots" section and is unlikely to bite again now that the pattern is documented inline.

## Files referenced in fixes

- [Main/_Module/ModuleData/equipmentsets/taom_career_starting_equipment.xml](../../Main/_Module/ModuleData/equipmentsets/taom_career_starting_equipment.xml) — Horse/HorseHarness explicit clears added to ranged + infantry rosters
- [Main/Features/CareerSystem/CareerSystemIoC.cs](../../Main/Features/CareerSystem/CareerSystemIoC.cs) — `CareerArchetypeMap` static field cache
- [Main/Features/CharacterCreation/CharacterCreationContentService.cs](../../Main/Features/CharacterCreation/CharacterCreationContentService.cs) — removed `?.` on `_careerMenuService`, replaced misleading "authoritative" comment
- [docs/features/career-system.md](../features/career-system.md) — added "Starting Equipment Override" section with FillFrom slot-merge warning + how-to for new cultures
- Phantom directory `E:/Steam/steamapps/common/Mount &amp; Blade II Bannerlord` — deleted; `starter_armors.xml` moved to literal-ampersand path before deletion
