# Root Cause Analysis — Player Startup Gold + CC Equipment Persistence Port

**Date:** 2026-05-06
**Feature:** Issue #110 — port LOTRAOM 1.2.12 `StartingEquipmentGold` to TAOM 1.3.15
**Source-of-truth:** Codex review (`xhigh` reasoning), Claude `/deep-review` (5 parallel agents), user-found correction
**Iron Law:** Phase 3e (RCA) applies to every confirmed bug, not just HIGH ones — patching symptoms without extracting the systemic lesson means the same bug class ships in the next feature. (`.claude/rules/harness-facts.md`)

---

## Bug A — `empire` (Dunland) culture missing from `startup_resources_config.xml` [HIGH]

**Symptom:** A player picking Dunland at CC would silently start with 0 gold (PlayerStartupGoldService logs a "no entry" warning, returns).

**Found by:** Claude `/deep-review` Agent 5 (data flow trace).

**What I did wrong:** I added 15 cultures to `startup_resources_config.xml` from memory, listing what I thought were "all the cultures." I forgot `empire`/Dunland.

**Why it slipped through:**
1. The pre-existing `startup_resources_config.xml` had 15 entries — I copied that list and added a new attribute, instead of treating `cultures.json` (the CC-selectable cultures source-of-truth) as authoritative.
2. The pre-existing 15-row config also missed `empire`. So the source I copied from was already incomplete; I inherited its gap.
3. No automated cross-check verifies that every CC-selectable culture in `cultures.json` has a row in `startup_resources_config.xml`.

**Lesson (systemic):** When extending a config with a new attribute, **enumerate from the upstream source-of-truth, not from the existing config rows**. The existing rows reflect what someone added before, not what should be present.

**Prevention:** Added [`feedback_enumerate_from_source_of_truth.md`](#bug-h-summary-of-systemic-lessons) memory rule.

---

## Bug B — `taom_youth_sturgia_1` `title_type="retainer"` with no roster XML [HIGH]

**Symptom:** A Dale (sturgia) player picking the first youth option ("Royal Guard of Dale") walks into the campaign with no equipment applied — `PlayerEquipmentService` logs `RosterNotFound` for `player_char_creation_sturgia_retainer_m/f`.

**Found by:** Claude `/deep-review` Agent 5.

**What I did wrong:** The `title_type` strings are user-authored in `youth_menu.json`. I assumed every (culture, title_type) tuple from the JSON had a corresponding `<EquipmentRoster id="player_char_creation_..."/>` somewhere — vanilla SandBox XML or TAOM custom XML. I never grepped to verify.

**Why it slipped through:**
1. The roster lookup happens at runtime via `MBObjectManager.GetObject<MBEquipmentRoster>` and degrades to a warning log on miss. No build-time validation.
2. The `title_type` value `retainer` exists for many other cultures; assumption was that it existed for sturgia too.
3. The Codex review process Phase 5 in-game smoke testing was deferred to a separate phase — automated review missed it.

**Lesson (systemic):** **Cross-reference data IDs across files when authoring content** — the roster ID format `player_char_creation_{culture}_{titleType}_{m|f}` joins three sources (`youth_menu.json` provides culture+titleType, equipment XML provides rosters). Adding/changing any one without validating the other two leaves silent gaps.

**Prevention candidate:** Build-time test that, for every `(culture_id, title_type)` pair in `youth_menu.json`, verifies a matching roster pair (`_m` and `_f`) exists across the loadable equipment XMLs. Filed as follow-up consideration; not in this session's scope.

---

## Bug C — `CareerMenuService.cs:227` inline format string bypassing the helper [MEDIUM, INCONSISTENCY]

**Symptom:** Three callers of `player_char_creation_{culture}_{titleType}_{m|f}` — `NarrativeMenuBuilder`, `CareerMenuService`, `PlayerEquipmentService`. After my refactor, two routed through the new shared `PlayerEquipmentRosterIds.Build` helper. `CareerMenuService:227` still inlined the format string. Identical output today, but a future rename would silently diverge.

**Found by:** Claude `/deep-review` Agent 5.

**What I did wrong:** I extracted `BuildEquipmentRosterId` from `NarrativeMenuBuilder` into a shared helper and updated that one caller. I did not grep for other callers of the same string format.

**Why it slipped through:**
1. Pattern-extract refactors typically Read-then-Edit one file and the new helper. I didn't do a project-wide grep for `"player_char_creation_"` or the format-string template.
2. The career screen and the CC screen don't visually overlap, so the duplication wasn't obvious from a single mental model.

**Lesson (systemic):** **When promoting an inline pattern to a shared helper, grep the entire codebase for every existing inlined copy of that pattern, not just the file you started from.** Search by both the format-template (`"player_char_creation_"`) and any structural fingerprint (e.g., `_{cultureId}_{titleType}_`).

---

## Bug D — `DeadBattleEquipment` guard absent (defensive) [MEDIUM, original]

**Symptom:** `Hero.BattleEquipment` falls through to `Campaign.Current.DeadBattleEquipment` (a process-wide shared singleton) when `_battleEquipment` is null. Calling `FillFrom` on that fallback would corrupt equipment for every dead/uninitialized hero in the session.

**Found by:** Claude `/deep-review` Agent 2 (taleworlds-researcher).

**What I did wrong:** I wrote `hero.BattleEquipment.FillFrom(battle)` without considering the `??` fallback path in the getter. The adapter accepts any `playerHeroId`, so a future caller passing a dead hero would silently corrupt shared state.

**Why it slipped through:** I read the property declaration `Hero.BattleEquipment` and saw "Equipment getter, mutable, FillFrom works." I did NOT decompile the **body** of the getter to see the `??` fallback.

**Lesson (already in memory):** [`feedback_taleworlds_vm_setter_decompile.md`](C:\Users\mikew\.claude\projects\c--Users-mikew-source-repos-TAOM\memory\feedback_taleworlds_vm_setter_decompile.md) — decompile the **body** of any TaleWorlds property/setter, not just its signature, before mutating it. I knew this rule. I didn't apply it.

**Reinforcement:** Apply the rule even when the property looks "obviously" simple. If the body uses `??` or `??=` to fall back to a shared singleton, mutating the receiver corrupts global state.

---

## Bug E — Civilian guard targeted the WRONG dead-equipment singleton [P1 / HIGH]

**Symptom:** The deep-review fix for Bug D introduced a guard checking `hero.CivilianEquipment != Campaign.Current.DeadBattleEquipment`. But `Hero.CivilianEquipment` falls through to `Campaign.Current.DeadCivilianEquipment` (a separate singleton). The civilian guard never tripped; calling `FillFrom` would corrupt `DeadCivilianEquipment`.

**Found by:** Codex `/codex:review` (independent verifier, `xhigh` reasoning, re-ran `ilspycmd`).

**What I did wrong:** I trusted the Claude `taleworlds-researcher` agent's earlier output, which had reported (incorrectly):
> ```csharp
> public Equipment BattleEquipment => _battleEquipment ?? Campaign.Current.DeadBattleEquipment;
> public Equipment CivilianEquipment => _civilianEquipment ?? Campaign.Current.DeadBattleEquipment;
> ```

Both lines said `DeadBattleEquipment`. The agent's confidence + the visible identical fallback target made the bug pattern look symmetric. I didn't re-decompile.

The truth, re-verified by Codex and confirmed by `ilspycmd` against the installed v1.3.15 DLL:
> ```csharp
> public Equipment BattleEquipment => _battleEquipment ?? Campaign.Current.DeadBattleEquipment;
> public Equipment CivilianEquipment => _civilianEquipment ?? Campaign.Current.DeadCivilianEquipment;
> ```

**Why it slipped through:**
1. **Agent confidence ≠ correctness.** The `taleworlds-researcher` agent presented its decompilation as a verified summary table.
2. The bug only fires when `_civilianEquipment` is null on a hero passed through the adapter — for `MainHero` at CC finalize this never happens, so the bug is latent until a future caller exercises it.
3. The Claude /deep-review's adversarial-escalation step (Step 2b) only triggers on CRITICAL findings; it didn't re-verify Agent 2's output.

**Lesson (systemic):** [`feedback_codex_caught_api_misread.md`](C:\Users\mikew\.claude\projects\c--Users-mikew-source-repos-TAOM\memory\feedback_codex_caught_api_misread.md) — when two reviews disagree on a TaleWorlds API, **re-run `ilspycmd`**. Don't pick a side based on which agent sounded more confident or had more detail. The disagreement itself is the signal.

**Reinforcement:** **Decompile the body, even when an agent-summary already paraphrases it.** Especially for `??` fallbacks and computed-property paths. The 5 seconds of `ilspycmd` time is cheaper than a P1 bug ship.

---

## Bug F — `shaghana` and `abanissa` kingdoms missing from XML [P2]

**Symptom:** Both are CC-selectable cultures and full kingdoms with NPC clans/lords. Without entries, neither the player nor the 17 NPC lords across both kingdoms get any startup gold or influence on a new game.

**Found by:** Codex `/codex:review` (after Claude data-flow agent flagged but dismissed).

**What I did wrong:** Same root cause as Bug A (forgot to enumerate from `cultures.json`), and additionally — when the Claude data-flow agent **did** flag `shaghana` and `abanissa`, it added "may be intentional zero-gold cultures" to the finding. I accepted that hedge without verifying.

**Why it slipped through:**
1. Bug A's root cause repeats: enumeration from existing config, not from upstream source-of-truth.
2. **Hedge-language acceptance.** "May be intentional" is a guess phrased with epistemic humility — but it asserts nothing. I treated it as if the data-flow agent had validated the intent. It hadn't.

**Lesson (systemic):** **When a review agent says "may be intentional / acceptable / fine" without evidence, treat that as an open question, not a closed one.** Either verify the intent (read the kingdom XML, check for NPC presence, ask the user) or fix it as if it were a real finding. The hedge cannot replace verification.

---

## Bug G — `shaghana`/`abanissa` misclassified as "Aserai-region cultures with no NPC clans" [HIGH]

**Symptom:** When applying the Bug F fix, I added the rows with `gold="0" influence="0"` and a comment "Aserai-region custom cultures (CC-selectable per cultures.json, no NPC clans)." This was wrong on two counts:
1. They are full **independent kingdoms** in the Harad region (registered in `taom_spkingdoms.xml` with their own ruler titles `Taskral` / `Châjaphân`, banner keys, settlements), NOT Aserai sub-cultures.
2. They have **17 NPC lords combined** (Shaghana 9 + Abanissa 8). The `gold="0" influence="0"` setting meant those NPC lords got **zero** startup gold and influence — a real player-visible bug that would manifest as Shaghana/Abanissa lords starting their campaigns broke and politically powerless.

**Found by:** User feedback ("shaghana and abanissa are two kingdoms").

**What I did wrong:** When applying the Codex fix, I read the entry in `cultures.json` (showed the cultures as having `town_A6` / `town_A14` starting settlements) and concluded "Aserai-region, no NPC clans." I did NOT:
1. Grep `taom_spkingdoms.xml` to see if the IDs are kingdoms.
2. Grep `lords.xml` to see if they have NPC lords.
3. Grep `clans.xml` to see if they have NPC clans.

The `kingdom-culture-mapping.md` memory entry **already said** they were independent kingdoms ("`shaghana` and `abanissa` are independent kingdoms, NOT Harad sub-kingdoms" — line 58). I had access to that memory but didn't load it before classifying.

**Why it slipped through:**
1. **Plausible-sounding classification beats verification.** The starting-settlement IDs `town_A6` and `town_A14` use the Aserai prefix `A`. That looked like enough evidence to classify them as Aserai-region. It wasn't.
2. **Confidence in cultures.json as the only source.** I read one file, drew a conclusion, didn't cross-reference against the kingdom/clan/lord XMLs.
3. The Codex fix was time-pressured (user asked to "ensure you execute commands to codex, no copy-paste") so I batch-applied the fix and moved on without grep-validation.

**Lesson (systemic):** **When you encounter unfamiliar IDs and need to classify them, grep the codebase exhaustively — kingdom XML, clan XML, lord XML, memory entries — before classifying.** A single source (e.g. `cultures.json`) can show that an ID exists for one purpose without ruling out other purposes.

**Specific reinforcement:** The kingdom-culture-mapping memory entry was **already correct**. The lesson here is: **load relevant memory entries at task start**, not after the user corrects you.

---

## Bug H — Summary of systemic lessons

Three classes of root cause repeat across this session's bugs:

### Class 1: Enumeration from existing artifact, not from source-of-truth (Bugs A, F)

| Symptom | Root cause | Prevention |
|---------|-----------|------------|
| Missing rows in extended config | Copied existing rows + added new attr; didn't enumerate upstream | When extending config with new attr, list expected rows from the upstream source-of-truth (`cultures.json`, etc.) and verify every row is present |

**New memory entry:** [`feedback_enumerate_from_source_of_truth.md`](#) (saved alongside this RCA).

### Class 2: Insufficient decompilation — body, not signature (Bugs D, E)

| Symptom | Root cause | Prevention |
|---------|-----------|------------|
| Mutated shared singleton via `??` fallback path | Read property signature, not body | Decompile property bodies for any `??` / `??=` / computed-property mutation target |
| Civilian guard targeting wrong singleton | Trusted agent paraphrase of decompilation | Re-run `ilspycmd` when two reviews disagree, don't pick by confidence |

**Existing memory entries reinforced:**
- [`feedback_taleworlds_vm_setter_decompile.md`](#) (already existed; not applied)
- [`feedback_codex_caught_api_misread.md`](#) (created this session)

### Class 3: ID classification without cross-reference (Bugs B, F, G)

| Symptom | Root cause | Prevention |
|---------|-----------|------------|
| `title_type` with no roster | Assumed (culture, title_type) coverage; didn't grep equipment XML | Cross-reference data IDs across all files that consume them before authoring |
| "May be intentional" review hedge accepted | Hedge-language acceptance without verification | When a review agent hedges ("may be", "could be", "probably"), treat as open question |
| Kingdom misclassified as sub-culture | Read one file (`cultures.json`); didn't grep kingdom/clan/lord XML | When classifying unfamiliar IDs, exhaustive grep across kingdom/clan/lord XML + memory entries first |

**New memory entry:** [`feedback_classify_by_grep_not_by_assumption.md`](#) (saved alongside this RCA).

---

## Process bugs (meta-RCA)

### Process Bug 1: Memory not loaded at task start

The `kingdom-culture-mapping.md` memory file already said `shaghana`/`abanissa` are independent kingdoms (Bug G's correct classification). I had it. I didn't load it before classifying.

**Fix:** When starting any task that involves project IDs, kingdom/culture concepts, or named entities — proactively grep the memory directory for relevant entries before drawing conclusions.

### Process Bug 2: Hedge language treated as verification

The Claude data-flow agent flagged `shaghana`/`abanissa` (Bug F) but added "may be intentional zero-gold cultures." I accepted the hedge instead of verifying.

**Fix:** Skill prompts should instruct review agents to either verify or escalate, not hedge. And when receiving review output, **treat every hedge as an open question** — Codex review will frequently push back on these (it did this session, twice — Bug F and the original civilian-guard misread).

### Process Bug 3: Codex review caught a Claude review mistake

Bug E (civilian guard wrong singleton) is the canonical case: Phase 1 Claude /deep-review built the bug, Phase 2 Codex caught it. **This is the workflow working as designed.** The lesson is not "Claude reviews are bad" — it's **always run Phase 2 (Codex) for any change that touches sealed-type internals**, even when Phase 1 looks clean. The cost of the second pass is low; the cost of a P1 ship is high.

### Process Bug 4: Time pressure compromised verification

Bug G was applied under user-prompted time pressure ("ensure you execute commands to codex, no copy-paste"). I batch-applied the Codex fix and moved on without grep-validation.

**Fix:** Time pressure shifts priority but does NOT remove the verification floor. Even under pressure, the floor is: "what is this ID, and where is it referenced?" — a 30-second grep that has caught two bugs this session alone.

---

## Bug I — `shaghana`/`abanissa` narrative menu coverage missing [HIGH, found Phase 3 self-review]

**Symptom:** Both cultures are CC-selectable per `cultures.json` but have zero entries across all 5 narrative menu JSONs (`parents_menu`, `childhood_menu`, `education_menu`, `youth_menu`, `adulthood_menu`). A player picking them at the culture step would render an empty Family/Childhood/Education/Youth/Adulthood page; vanilla CC crashes on advance from an empty `SelectionList`. The `playerGold` rows I added in fix F are functionally dead because finalize is unreachable for these cultures.

**Found by:** Codex Phase 3 self-review of session fixes (2026-05-06).

**What I did wrong:** When fixing Bug F (adding shaghana/abanissa to startup_resources_config.xml), I verified they were full kingdoms with NPC clans + lords (Bug G correction) and that they were CC-selectable per cultures.json. I did NOT verify whether their CC selection actually leads to a working finalize — I only checked the existence of the row in the upstream source-of-truth file (`cultures.json`), not the existence of culture-keyed content across the downstream narrative menu JSONs.

**Why it slipped through:**
1. **Same root cause as Bug A/F (Class 1):** I cross-referenced one upstream file but stopped there. I didn't trace the full CC pipeline (cultures.json → SetSelectedCulture → narrative menus → finalize) to confirm each step works for the new cultures.
2. **Phase 2 Codex review didn't run this trace either.** It checked startup_resources_config.xml for missing rows; it didn't trace the player-flow pipeline. Phase 3 (this self-review) is the one that asked "what does a player see when they pick shaghana?" — that question wasn't asked in Phase 2.
3. **`playerGold` for these cultures looks like a feature.** The XML row says "shaghana player gets 4000 gold." If finalize is unreachable, that row is dead config. Today's review treats dead config rows like "no bug here, just unused" — but the `cultures.json` registration makes them visible at CC, creating an implicit user promise that doesn't pay off.

**Lesson (systemic, extends Class 1):** **Source-of-truth enumeration must extend to the FULL pipeline a feature touches, not just the entry point.** For shaghana/abanissa player flow:
- Entry point: `cultures.json` (selectable) ✓
- Stage 1: `parents_menu.json` (parent options) ✗ (missing)
- Stage 2: `childhood_menu.json` (childhood) ✗
- Stage 3: `education_menu.json` (education) ✗
- Stage 4: `youth_menu.json` (youth) ✗
- Stage 5: `adulthood_menu.json` (adulthood) ✗
- Finalize: `startup_resources_config.xml` (gold/equipment) ✓ (added this session)

A 30-second grep across the 5 menu JSONs would have caught the dead-end. Same shape as the cross-file cross-reference for Bug B (sturgia retainer roster missing).

**Resolution this session:**
- Added a defensive XML comment in startup_resources_config.xml flagging the gap explicitly
- Filed follow-up issue [#111](https://github.com/haterade22/TAOM/issues/111) with three remediation options (A: author full menu coverage, B: hide from CC, C: safe fallback)
- This issue is OUT OF SCOPE for #110 (gold/equipment port, not narrative menu authoring)
- Per "no silent deferrals" rule, the deferral is recorded in: GitHub issue #111, RCA bug I, CHANGELOG, and an in-line XML comment

## Bug J — XML header comment misattributes `influence` to NPC lords [LOW]

**Symptom:** XML config header comment said "influence is granted to NPC lords." Actually `StartupInfluenceService` applies to eligible CLANS (not lords). Wrong audience for future tuners reading the config to understand what to change.

**Found by:** Codex Phase 3 self-review.

**What I did wrong:** I wrote the XML header comment by paraphrasing my mental model of the feature. I didn't re-read `StartupInfluenceService.cs` to confirm the actual target.

**Lesson (Class 4 — new):** **Doc/comment text near config files must be verified against the consuming code, not paraphrased from memory.** Especially for retuning knobs where the user reads the comment to know what to change.

**Resolution this session:** Comment corrected. Now says "influence granted to each eligible CLAN of this culture (StartupInfluenceService applies to clans, not lords)."

## Bug count summary

| Phase | Source | HIGH/P1 | MEDIUM/P2 | LOW | Total caught |
|-------|--------|---------|-----------|-----|--------------|
| Phase 1 (Claude `/deep-review`) | 5 parallel agents | 2 | 2 | 0 | 4 |
| Phase 2 (Codex `/codex:review` first pass) | independent verifier | 1 | 1 | 0 | 2 |
| Phase 3a (user feedback) | direct correction | 1 | 0 | 0 | 1 |
| Phase 3b (Codex self-review of fixes) | independent re-verifier | 1 | 0 | 1 | 2 |
| **Total this session** | | **5** | **3** | **1** | **9** |

Phase 3b (self-review of fixes) caught 2 more bugs that Phase 1 + Phase 2 missed. The pattern from earlier this session (each phase catches what the previous missed) holds. **Phase 3 paid for itself again** — the HIGH finding (shaghana/abanissa dead-end) would have shipped as silent player crash without it.

Each phase caught bugs the previous phase missed. Each phase pays for itself.

---

## What changes after this RCA

1. **Memory entries created/reinforced:**
   - [`feedback_codex_caught_api_misread.md`](C:\Users\mikew\.claude\projects\c--Users-mikew-source-repos-TAOM\memory\feedback_codex_caught_api_misread.md) (created this session)
   - `feedback_enumerate_from_source_of_truth.md` (creating now)
   - `feedback_classify_by_grep_not_by_assumption.md` (creating now)
   - `feedback_taleworlds_vm_setter_decompile.md` (already existed; reinforce by reference)

2. **No agent-prompt changes this session.** The lesson "review agents shouldn't hedge" is real but adjusting the deep-review skill is its own scoped change with its own RCA risk. Filing as follow-up.

3. **No build-time validation infra this session.** A test that cross-references `youth_menu.json` (culture, title_type) pairs against equipment XML rosters would prevent Bug B's class. Filing as follow-up.

4. **Pre-existing tech debt noted, NOT fixed:** `CharacterCreationContentService.AssignCareer` uses `IoC.Resolve<>` at lines ~218, 235. Pre-dates this session. Out of scope; flagged in CHANGELOG and issue #110.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/reviews/REVIEW-LOG.md](./REVIEW-LOG.md)

<!-- backlinks-end -->
