# RCA — Elf Character Creation facegen action_set (2026-05-22)

## Top-line summary

Elves (Mirkwood / Rivendell) rendered as a contorted / horizontally-stretched mesh on the Character Creation parent menu — same visible failure mode as the 2026-05-04 "broken custom-race CC parents" bug, but with a different root cause hidden behind it. The original 2026-05-04 fix patched 1.3 action-type aliases onto 12 *pre-existing* facegen action_sets in LOTRLOME_Armory but never authored the missing `as_elf_facegen` / `as_elf_female_facegen` pair. The commit message and CHANGELOG line both listed "elf" as fixed despite the patch not touching it.

The fix shipped in two iterations the same session:

| Iter | Approach | Scope passed | Scope failed |
|---|---|---|---|
| v1 | Slim 14-action elf facegen (only the CC parent action types, `base_set="as_human_warrior"`) | Parent menu — elves stand upright | Early Childhood + every subsequent CC stage — child agent lying down / T-posed |
| v2 | Verbatim copy of `as_dwarf_facegen` / `as_dwarf_female_facegen` (~420 lines per file), `id` + `base_set` attributes renamed only | Every CC stage (confirmed in-game) | — |

Two distinct lessons:

1. **Doc completeness:** the 2026-05-04 snapshot README said the patch covered "12 facegen sets (dwarf, dwarf_female, orc, orc_female, … etc.)". The `etc.` is what hid the elf hole for 18 days — no missing race was named, no present race was claimed comprehensive. Now the README enumerates every required `as_<race>_facegen` ID explicitly.
2. **Engine inheritance semantics:** Bannerlord 1.3's facegen action-lookup does NOT fall through `base_set` for post-parent CC action types (`act_childhood_*`, `act_character_creation_toddler_*`, `act_inventory_*`, `act_stand_*`, `act_sit_*`, `act_rider_story_*`, `act_horse_story_*`). Those types must be declared **directly** in the facegen action_set. LOTRLOME's `as_dwarf_facegen` is the proof-by-existence: it declares all ~106 actions explicitly even though `as_dwarf_warrior` is its base.

## Findings table

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|-----|-----|----------|------------|-------------------|
| 1 | HIGH | `as_elf_facegen` / `as_elf_female_facegen` never authored in LOTRLOME — engine fell back to default that didn't bind to human skeleton elves use | Cross-mod data gap | The 2026-05-04 patch only touched action_sets that ALREADY existed in LOTRLOME. LOTRLOME was a 1.2-era LOTR armor mod that never had elves as a playable race, so there was nothing for the patch to update. The 2026-05-04 commit message + CHANGELOG line both listed "elf" as fixed, masking the gap. The snapshot README said "12 facegen sets ... etc." — no explicit name list. | **Memory addendum** in `feedback_lotrlome_action_set_aliases.md`: the fix recipe must both (a) PATCH existing facegen action_sets with 1.3 aliases AND (b) CREATE missing facegen action_sets for races that LOTRLOME's authors did not anticipate. The snapshot README now lists every required `as_<race>_facegen` ID by name — no `etc.` |
| 2 | HIGH | v1 fix was a slim 14-action facegen entry; parent menu worked but Early Childhood + all later stages broke (lying-down agent) | Engine semantics gap | I assumed `base_set="as_human_warrior"` inheritance would cover `act_childhood_*` / `act_character_creation_toddler_*` / etc. It does not — those action types must live directly in the facegen action_set. The dwarf block's ~106-action explicit declaration is the evidence: if inheritance worked, LOTRLOME wouldn't bother declaring those in every race's facegen set. I had read the dwarf block before shipping v1 and STILL chose slim because of diff-size aesthetics. | **Memory addendum v2** in `feedback_lotrlome_action_set_aliases.md`: the concrete recipe is "copy LOTRLOME's `as_dwarf_facegen` verbatim, rename `id` + `base_set`, nothing else." The slim form is not "minimum viable" — the dwarf-block form is. Always read existing-working-code to decide what "minimum" means before inventing your own form. |
| 3 | LOW | I told the user "regression is elf-only" without confirming with them — could have been mistaken | Communication / verification | Used an Explore agent's audit to confirm other races still had 106/31 action parity. Was correct, but the user's "the action sets were changed for all races" framing prompted a second guess. | No rule change — the audit was the right move; just be explicit in the response that the audit refuted the framing rather than the user being wrong. |

## Root-cause pattern (#2 deserves its own section)

The slim-vs-full failure mode is the recurring "trust inheritance, fail at runtime" anti-pattern. Bannerlord's action_set / monster / skin XML system is full of `base_set=` / `base_skin=` / `parent=` references that suggest inheritance is the norm — but the engine's behavior is selective about which fields actually fall through. Without decompiling the specific lookup path (and even WITH decompiling — these are native engine calls, hard to fully trace), the only safe heuristic is **"do whatever the proven-working sibling does, verbatim."**

For facegen specifically:
- LOTRLOME's `as_dwarf_facegen`: ~106 actions, works for dwarves at every CC stage. ✓ Proof.
- LOTRLOME's `as_orc_facegen` / `as_uruk_facegen` / etc.: same ~106 actions. ✓ Proof × 12.
- TAOM's v1 `as_elf_facegen`: 14 actions, works only for parent menu. ✗ Counter-example.
- TAOM's v2 `as_elf_facegen`: ~106 actions matching dwarf verbatim. ✓ Works.

The pattern across all 12 LOTRLOME facegens was the evidence. I read it and chose to ignore it for diff-size reasons. v2 corrected the mistake.

**Why this happens generally:** when a new entry must mirror existing entries in a poorly-documented XML schema, the "smaller diff" temptation is strong. The cost of getting it wrong (silent runtime breakage at a different stage than the one being tested) is much higher than the cost of a 420-line verbatim copy. Diff-size is a code-review aesthetic, not a correctness criterion.

## Why each agent / step missed (or caught)

- **Phase-1 Explore agent (2026-05-22, finding #1):** ✅ caught the elf hole. Audited every TAOM `race=` attribute against LOTRLOME's facegen list and reported `elf` as the only missing entry. Specifically called out: "no `as_elf_facegen` ... the engine falls back ... contorted-mesh bug visible on Mirkwood / Rivendell."
- **Phase-1 same agent (finding #2):** ✗ missed the slim-vs-full issue because the audit only checked presence/absence of facegen IDs, not the action-type count per facegen. A "complete" audit at that point would have flagged "elf facegen has 14 actions, dwarf has 106 — wide gap" before I shipped v1.
- **v1 ship decision (mine):** ✗ I had the data — I'd already read the full dwarf block at lines 16812-17134 and saw its ~322-line structure. I chose slim because:
  1. Diff-size aesthetic.
  2. Assumed `base_set="as_human_warrior"` inheritance would cover non-parent action types (untested assumption).
  3. Did not in-game-test BEFORE shipping the slim entry — relied on the user's first-screenshot scope (parent menu only) without anticipating they'd progress to later CC stages.
- **User's in-game test (between v1 and v2):** ✅ caught finding #2 within minutes of shipping v1. Same-day iteration cost ~30 min of additional work + ~420 lines of new diff per file. Acceptable cost relative to the alternative (shipping v1, user reports lying-down child days later, full RCA + reload context cost).
- **Phase-2 Explore agent (between v1 and v2):** ✅ confirmed the engine doesn't fall through `base_set` for `act_childhood_*`, validated XML parse health, and pointed at the dwarf block's full action surface as the correct template. Vindicated the same data the Phase-1 agent had earlier but I hadn't acted on.

## Preventive actions taken

1. **Doc:** `docs/reference/lotrlome-armory-snapshot/README.md` rewritten to list every required `as_<race>_facegen` ID and every required action-type category that must be declared directly in the facegen action_set. No `etc.`
2. **Doc:** `docs/features/character-creation.md` gained a new section ("LOTRLOME `as_<race>_facegen` action_set requirement") with the full recipe + warning about the slim form.
3. **Doc:** `docs/features/race-age-system.md` "How to Add a New Race" now includes a step pointing at the CC facegen requirement.
4. **Memory:** `feedback_lotrlome_action_set_aliases.md` extended with two same-day addenda — the create-missing-not-just-patch rule (from finding #1) and the declare-everything-don't-trust-inheritance rule with concrete recipe (from finding #2).
5. **Code:** the v2 fix itself — `as_elf_facegen` + `as_elf_female_facegen` are now full 106/31-action entries in both the live LOTRLOME and the tracked snapshot, with attribute parity vs `as_dwarf_facegen` confirmed by Python `xml.etree.ElementTree.parse` + action-count audit.

## What this does NOT need

- **No TAOM C# changes** — the fix is pure XML, no Harmony, no GameModel, no SubModule edits. `Patch20_NarrativeHorseGuard`'s race-sync prefix was correct all along; it just needed the engine's lookup target (`as_elf_facegen`) to actually exist with a complete action surface.
- **No startup check / build-time injector** — user explicitly chose "snapshot + doc only" prevention. The audit table + per-race checklist in the README is the safety net.
- **No `/deep-review`** — XML-only change, no C# touched, doesn't meet the deep-review threshold ("≥2 C# files or any feature module").
- **No GitHub issue** — XML-only fix lives entirely in another mod's `ModuleData`; no TAOM-feature change to ticket. Documented via CHANGELOG + this RCA + memory.

## Cost

- v1 → v2 same-day iteration: ~30 min of additional work, ~420 lines of additional diff per file (live LOTRLOME + tracked snapshot).
- Counterfactual cost if v1 had shipped without immediate in-game test and the lying-down-child bug was reported days later: full RCA + context-reload + cold rediscovery of the slim-vs-full distinction. Probably 2-3x.
- Lesson worth carrying forward: when shipping any XML data fix in a poorly-documented engine schema, the in-game test is the only credible verification. Code-only validation (XML parses, schema looks right, action types match what the screenshot suggested) does not catch failure modes at adjacent stages.

---

## Addendum v3 (same session, 2026-05-22) — vanilla age-30 animation bug, NOT a LOTRLOME data issue

After the elf v2 fix shipped and the user confirmed elf rendering worked at every CC stage (parent menu → Early Childhood → Youth → Adolescence → Adulthood), a third bug surfaced: at the **Starting Age** narrative menu, clicking age 30 ("You are at your prime...") rendered the player as a horizontally-stretched / lying-down mesh. Ages 20/40/50 worked correctly. User initially reported this for orc; on the third clarification round they confirmed in-game testing showed the bug on every race (dwarf / uruk / elf / human / orc).

### Investigation

The user's initial hypothesis was "controlled by action set ids youth, adult, etc." That was off-target — no such IDs exist in LOTRLOME or Native action_sets.xml. Vanilla `CharacterCreationCampaignBehavior` actually uses the same `as_<race>_facegen` action_set across all ages and just hard-codes a different animation_id per age handler:

| Age | Vanilla animation ID | Status |
|---|---|---|
| 20 | `act_childhood_focus` | works |
| **30** | **`act_childhood_athlete`** | **broken on all races** |
| 40 | `act_childhood_sharp` | works |
| 50 | `act_childhood_tough` | works |

Bit-for-bit compare confirmed orc / dwarf / uruk / elf facegens are identical — all map `act_childhood_athlete → anim_childhood_athlete`. The action type is properly declared in `Native/ModuleData/action_types.xml` (line 15301). The bug is at the runtime `anim_childhood_athlete ↔ human_skeleton` binding layer — a vanilla v1.3.15 regression on a single animation file, not an LOTRLOME data issue.

### Findings table (v3)

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|-----|-----|----------|------------|-------------------|
| 1 | HIGH | Vanilla age-30 CC option plays a broken animation on the human_skeleton chain (all TAOM races affected) | TaleWorlds engine bug | The bug shipped in vanilla v1.3.15 itself; not introduced by TAOM or LOTRLOME. Caught only at the user's first traversal of the Starting Age menu. | **Memory addendum v3** in `feedback_lotrlome_action_set_aliases.md`: when all races break at the same CC stage despite identical action_set data, the bug is at the engine/anim binding layer and a TAOM-side Harmony override is the only fix. Pre-flight audit checklist: (a) is the action_set declaration present + correct? (b) is the action type registered in `Native/action_types.xml`? (c) does the anim file render correctly on the relevant skeleton (test in-game)? |
| 2 | LOW | User hypothesis "action set ids youth, adult, etc." led the investigation toward a non-existent LOTRLOME data pattern | Communication / hypothesis verification | The user had reasonable intuition that "ages produce different visuals → action_set is age-keyed", but the actual lookup is anim-id per age, not action_set per age. | No rule change — re-verify hypotheses against decompiled source when the data trail goes dry. Took two grep cycles to refute (LOTRLOME action_set IDs + Native action_set IDs). |

### Fix shape (v3)

Single Harmony Postfix appended to the existing `Patch20_NarrativeHorseGuard` group in [`Main/Features/CharacterCreation/Hooks/CharacterCreationCampaignBehavior_GetYouthMenuArgs_Patch.cs`](../../Main/Features/CharacterCreation/Hooks/CharacterCreationCampaignBehavior_GetYouthMenuArgs_Patch.cs):

```csharp
[HarmonyPatch]
[HarmonyPatchCategory("Patch20_NarrativeHorseGuard")]
public static class CharacterCreationCampaignBehavior_AgeSelectionAdultOptionOnSelect_Patch
{
    static MethodBase TargetMethod() =>
        AccessTools.DeclaredMethod(
            typeof(CharacterCreationCampaignBehavior),
            "AgeSelectionAdultOptionOnSelect");

    [HarmonyPostfix]
    static void Postfix(CharacterCreationManager characterCreationManager)
    {
        var characters = characterCreationManager?.CurrentMenu?.Characters;
        if (characters == null) return;
        foreach (var character in characters)
        {
            if (character.StringId == "player_age_selection_character")
            {
                character.SetAnimationId("act_childhood_focus");
                break;
            }
        }
    }
}
```

The Postfix runs **after** vanilla, finds the `player_age_selection_character`, and re-sets the animation to `act_childhood_focus` (proven-working age-20 anim). Vanilla's `ChangeAge(30)`, `SetEquipment(...)`, `SetBirthDay(-30y)`, `StartingAge = 30`, and focus/attribute bonuses are all preserved.

Scope deliberately limited to the age-30 code path. Vanilla references `act_childhood_athlete` in two other locations (`CharacterCreationCampaignBehavior.cs:1599` + `:2016`, both youth backstory option handlers); those are untouched because no user report has surfaced lying-down behavior there yet.

### Build status (v3)

C# compile: `dotnet build Main/TAOM.csproj -t:Restore,Compile` → **0 errors / 0 warnings**. Post-build deploy step blocked by Bannerlord holding `0Harmony.dll` + `DryIoc.dll` locked (user has the game running). This is a pure environment issue, not a code issue — the deploy will succeed on the next clean build after the game is closed. Per `.claude/rules/environment-failures.md`, no process killing or DLL-unlock workaround attempted.

### Why each agent / step missed (or caught)

- **Phase-1 Explore agent on the original elf bug (2026-05-22):** N/A — the orc-age-30 bug wasn't in scope.
- **v1 + v2 elf fix:** N/A — different code path.
- **First Phase-1 Explore agent on this v3 bug:** ✗ misled by the TAOM `NarrativeHorseGuardService.AnimationId = "act_childhood_schooled"` constant — claimed TAOM was forcing a single anim across all ages and overriding vanilla's per-age selection. Actually wrong: `NarrativeHorseGuardService` is for the no-horse-culture guard at Youth/Adult menus, not the Age Selection menu. Misread.
- **Manual decompile check on `CharacterCreationCampaignBehavior` lines 3290-3470:** ✅ caught the four hard-coded animation IDs per age handler, identifying `act_childhood_athlete` at age 30 as the unique variable across the four options.
- **User in-game test of all races:** ✅ confirmed global scope (not orc-specific), which de-scoped data fixes and confirmed a TAOM-side patch was needed.

### Cost

- v3 investigation + fix + docs: ~45 min (one round of misdirected agent exploration on the wrong service, then a 5-min decompile that pinpointed it).
- The fix itself is ~25 lines of C# (one Harmony Postfix class with xmldoc + null guard).
- Counterfactual cost if shipped without the user's "all races" clarification: I would have proposed a data-side fix that masked the engine bug at one call site and probably broke the two unrelated youth-option call sites — a regression worse than the original bug.

### Lessons (v3-specific, on top of v1/v2 lessons)

1. **Hypothesis discipline.** "Action set IDs youth, adult, etc." was a plausible guess from a user with hands-on context. It was also wrong. I verified by grepping LOTRLOME + Native first (took 30 sec), then went to the decompiled source. Cheap verification rounds before deep dives save context. Without grep-first I might have spent time auditing skin maturity types, age_keys, body morphs — none of which were the bug.
2. **"All races break" is the diagnostic signal.** Whenever a CC bug shows on multiple races with identical underlying data, the bug is upstream of the data layer (engine, anim binding, vanilla code). XML edits cannot fix it. The fix must be C# Harmony.
3. **Don't change data to mask engine bugs.** If LOTRLOME had remapped `act_childhood_athlete → anim_childhood_focus` to fix this, the change would propagate to the other two call sites where the original anim was working fine. Engine bugs need targeted Harmony patches, not data overrides.
