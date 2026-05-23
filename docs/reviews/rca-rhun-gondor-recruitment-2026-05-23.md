# RCA — Rhun + Gondor Recruitment + Easterling Retirement + Ithil Guard Equipment (deep-review 2026-05-23)

## Top-line

Session built per-settlement Rhun volunteer recruitment pools, orphaned the Easterling troop line (replaced by Loke-Rim everywhere), added a JSON-driven Gondor recruitment loader with a conditional-pool API (Ithil Guard at `town_ES2` only when Gondor-owned), upgraded Wainrider horse armor, and re-equipped the Ithil Guard line (2H sword + 2H polearm rosters + steel bow + piercing arrows).

`/deep-review` ran 5 parallel agents. 4 agents passed clean; **Agent 5 (Data Flow) found 1 HIGH gap** that the other 4 missed. Fixed in the same session; regression test updated; build clean (2418/2420 tests pass, 2 unrelated skips). One MEDIUM finding from Agent 3 was confirmed but PRE-EXISTING in the codebase and infrastructure-blocked (no `IModLogger.IsDebugEnabled` exists); deferred with documentation.

## Findings + Root Cause Table

| # | Sev | Bug | Category | Why Missed | Preventive Action |
|---|-----|-----|----------|-----------|-------------------|
| F1 | HIGH | `InitializeRhunSettlements()` Wain pool referenced `wain_cavalry`, which **does not exist** in `troops_rhun_new.xml`. The actual Wain cavalry troops are `wainrider_horseman` (mid-tier) and `wainrider_cavalry` (elite). The pool entry would silently fail at runtime: `MBObjectManager.GetObject<CharacterObject>("wain_cavalry")` returns null, dropping the volunteer slot for ~20% of recruitment rolls (weight 2/10) at all four Wain settlements (castle_RU7, castle_RU8, town_RU6, castle_RU6). | **ID typo / missing source-of-truth check** | I converted the user's spec "Wain Cavalry - 2" to `wain_cavalry` by literal naming-convention inference (the line has `wain_youngblood`, `wain_glaiveman`, so `wain_cavalry` looked symmetric). I did NOT grep `troops_rhun_new.xml` to verify the ID exists — I trusted my own naming pattern instead of the source of truth. The other 8 troop IDs in the same pool spec (e.g., `dragon_wrath_acolyte`, `balcoth_volunteer`) happened to match exactly, which made the singleton miss invisible. The tests passed because they assert what the service RETURNS (the string ID), not whether the ID resolves to a real troop at runtime. | **New rule (feedback memory):** when authoring recruitment pools / party-template entries / config-driven troop references from a user spec, grep the canonical troop XML for EVERY referenced ID before commit — even when the user's naming "looks obviously correct" relative to siblings. Sibling-symmetry is a false positive signal. Codified in `feedback_verify_troop_ids_against_canonical_xml.md`. **Regression coverage:** the existing test `GetVolunteerTroopId_Torcain_BoundaryRolls_ReturnExpectedTroop` was updated to assert `wainrider_cavalry`. A separate `validate_all_troop_refs.py` script-level check could catch this class of bug at PR time — open as follow-up if recurrence happens. |
| F2 | MEDIUM | `VolunteerRecruitmentService.GetVolunteerTroopId` constructs an interpolated debug-log string on every call (per-notable per-day). Even when log level filters out Debug, the string is built and GC'd. Agent 3 suggested guarding with `IsDebugEnabled`. | **GC pressure on hot path (pre-existing, not introduced this session)** | The LogDebug line was in the original code before this PR — Phase A only added new pools, did not change the logging pattern. `IModLogger` does not currently expose an `IsDebugEnabled` property, so the fix is infrastructure-level (interface change + implementer plumbing), not a one-line guard. Defer. | **Deferred** — pre-existing, requires `IModLogger` extension. Tracked in CHANGELOG under "Known limitation"; will be addressed when the next logging-level audit lands. NOT introduced by this session. |
| F3 | LOW | No `docs/features/volunteer-recruitment.md` exists. Agent 4 flagged feature-doc gap. | **Documentation debt — pre-existing** | The volunteer recruitment system has existed since Gondor was added (months ago). This session added significant new surface (conditional pool API + JSON loader pattern + Rhun pools) which deserves documentation, but the underlying gap pre-dates this session. | **Deferred** — open as follow-up. Will write `docs/features/volunteer-recruitment.md` covering the full system (hand-written + JSON + conditional API) in a separate doc session, since scoping it to "just this session's additions" would produce a fragmented doc. |

## Root cause pattern

**ID-typo-without-canonical-grep** is the same shape as multiple prior bugs in TAOM history:

- `lotraom-assets` Erebor `sk_dwarf_iron_*` misfiled to `erebor/` instead of canonical `iron_hills/` (RCA `rca-multi-culture-armor-revamp-2026-05-22.md`) — author trusted folder-naming inference instead of grepping the actual folder for prefix existence.
- `feedback_classify_by_grep_not_by_assumption.md` (2026-05-21) — shaghana/abanissa misclassified as Aserai sub-cultures despite kingdom-culture-mapping memory already getting it right — author assumed from sibling names instead of grepping.
- `feedback_enumerate_from_source_of_truth.md` (2026-05-21) — player-startup-gold port: extended config from existing-rows instead of from cultures.json source-of-truth. 3 bugs shipped in one session from the same anti-pattern.

The unifying lesson: **sibling-naming-symmetry is a false-positive signal**. When the user's spec uses descriptive language ("Wain Cavalry", "Easterling Recruit") and the codebase uses IDs (`wainrider_cavalry`, `easterling_recruit`), the gap is bridged by **grep**, not by **pattern-matching**. This was Codex finding category #5 in CLAUDE.md `Equipment & Armory` section: *"MANDATORY: before authoring a new item, grep ALL `LOTRLOME_items/*/` subfolders for the prefix."* The rule generalizes to: **before referencing any cross-file identifier, grep the source-of-truth file for that exact identifier.**

## Why each deep-review agent missed F1

- **Agent 1 (Standards):** Standards rules check structural conventions (ADR-007, interface segregation, no-#region). A missing troop ID is not a structural violation; the code IS well-structured around a bogus value. Out of scope by design.
- **Agent 2 (Compatibility):** Verifies TaleWorlds API surface (`Hero.CurrentSettlement`, `Settlement.OwnerClan`, etc.). Item IDs and NPCCharacter IDs are TAOM/Armory content, not TaleWorlds API — the agent's prompt scoped item-ID checks to NEW items I authored (`lrd_horse_armour_4` etc.), not to EXISTING troop IDs I referenced. Could be extended: "Also verify any NPCCharacter ID referenced from C# code exists in `troops/*.xml`." This is a real prompt gap.
- **Agent 3 (Efficiency):** Performance analysis. Out of scope.
- **Agent 4 (Completeness):** Checked test coverage, IoC registration, save-compat. The test for the Wain pool exists and asserts `wain_cavalry` is returned — which is what the BUGGY service returns. The test passing did not validate the ID's existence; it validated the service's pool wiring. Completeness review can't catch this without crossing into Agent 5's data-flow territory.
- **Agent 5 (Data Flow):** ✓ Caught it. The agent explicitly traced "every troop ID in the pools" against `troops_rhun_new.xml` and found `wain_cavalry` missing. This is exactly the class of bug the data-flow agent is designed for — cross-file consistency. Working as intended.

The lesson: Agent 5 IS the catch-net for ID typos. The other 4 agents are not, and that's correct scope. The prompt gap is in Agent 2: **the compatibility agent should also cross-check NPCCharacter / troop IDs referenced from C# code against the troop XML files, not only TaleWorlds API surface.** This catches a class of bug that Agent 5 already catches via data flow, but the duplication would catch the bug faster (Agent 2 finishes before Agent 5 typically) and would also catch it if Agent 5's scope ever narrows in the future.

## Feedback memories to codify

- **`feedback_verify_troop_ids_against_canonical_xml.md`** — when authoring recruitment pools, party-template entries, or any config-driven troop references from a user spec, grep the canonical troop XML (`troops_<culture>.xml` or `troops_<culture>_new.xml`) for EVERY referenced ID before commit. Sibling-symmetry is a false positive signal: `wain_youngblood` + `wain_glaiveman` existing does NOT imply `wain_cavalry` exists. RCA: this file.

I will NOT codify the pre-existing logger gap (F2) as a feedback memory — it's an infrastructure improvement, not a recurring pattern.

I will NOT codify the missing feature doc (F3) as a feedback memory — it's documentation debt, tracked normally.

## Patch history

| Pre-review | Post-review |
|------------|------------|
| `("wain_cavalry", 2)` in `wainPool` array | `("wainrider_cavalry", 2)` — confirmed existing NPCCharacter at troops_rhun_new.xml:3628 |
| Test `[DataRow(8, "wain_cavalry")]` / `[DataRow(9, "wain_cavalry")]` | `[DataRow(8, "wainrider_cavalry")]` / `[DataRow(9, "wainrider_cavalry")]` |

## Test results

- Pre-fix: build clean, 2418/2420 tests pass (2 unrelated skips). The buggy test passed because it asserted the buggy return value.
- Post-fix: 11/11 Torcain + Wain tests pass against the corrected ID. Full suite still 2418/2420.

## Verdict

READY FOR COMMIT after applying the one-line fix above. Feature doc + logger improvement deferred as follow-ups.
