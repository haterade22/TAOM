# RCA — Cultural-Feats 3-Pack: party-size retune + volunteer respawn + notable count (2026-05-31)

Feature: three cultural-feat dimensions in one delivery — (a) party-size retune (4 values) + 3 new Dunland/Rhun/Harad feats, (b) village volunteer respawn-rate (4 new feats + `TaomVolunteerModel.GetDailyVolunteerProductionProbability` override), (c) per-settlement notable-count (8 new feats + new `TaomNotableSpawnModel : DefaultNotableSpawnModel`). Feat total 77 → 92.

Review pipeline: `/deep-review` (5 agents, verdict NEEDS FIXES → then READY after fix). Codex pass pending at time of writing.

## Findings

| # | Sev | Bug | Category | Confirmed by | Why Missed | Action |
|---|-----|-----|----------|--------------|-----------|--------|
| 1 | HIGH | The 4 new `spc_notable_{culture}_23` RuralNotable NPCs were added to `characters/npcs_*.xml` but **NOT** added to the culture's `<notable_templates>` block in `taom_spcultures.xml`. The engine populates the notable spawn pool exclusively from `<notable_templates>` — the new templates were unreachable. The very clone-notable problem the `_23` additions existed to prevent (village RuralNotable target ceils 2 → 3, with only 2 templates the engine reuses one) would have shipped silently. | Partial replication of a multi-layer engine convention | Deep-review Agent 5 (Data Flow), verified by grep of `<notable_templates>` blocks in `taom_spcultures.xml` — only `_21` + `_22` present for all 4 affected cultures | The implementation plan step was *"Add 4 new RuralNotable templates to npcs_{isengard,mordor,dolguldur,gundabad}.xml"* — framed as a single XML edit. `.claude/rules/xml-data.md` documents the per-culture 26-NPC convention (`_21/_22` for Rural Notables) but does NOT make explicit that those NPCs only spawn when ALSO listed in the culture's `<notable_templates>` `<template>` lines. I extracted half the convention. | **Fixed** — added `<template name="NPCCharacter.spc_notable_{culture}_23" />` to all 4 culture blocks in `taom_spcultures.xml`. ModuleData validator + tests still green. |
| 2 | LOW (process) | No GitHub issue exists for this delivery (the workflow requires one before commit). | Process gap | Deep-review Agent 4 (Completeness): `gh issue list` returned no match. | The 3 features started as an investigation Workflow that produced research + design questions; I went straight to implementation after the user answered. Issue creation was on the original plan but slipped between phases. | **Will create issue** before the closing commit, referenced in the commit message. |
| 3 | — | Agent 2 (Compatibility) raised an action note: vanilla `DefaultVolunteerModel.GetDailyVolunteerProductionProbability` contains an unguarded `hero.CurrentSettlement.MapFaction.Fiefs` access. If TAOM widens the call surface (calls the method when hero has no current settlement), an NRE would surface. | Vanilla parity / call-surface check | Decompile of vanilla method by Agent 2. | Verified: `TaomVolunteerModel`'s override calls `base.GetDailyVolunteerProductionProbability(hero, index, settlement)` with the EXACT same `(hero, index, settlement)` the engine passed in (this is the only call site — `RecruitmentCampaignBehavior.UpdateVolunteersOfNotablesInSettlement`). The override does not widen the surface; vanilla's invariant (caller only invokes when notable is in a settlement) is preserved by definition. The override applies the feat factor + clamps; it does not invoke `base` with a synthesized hero. | **Investigated — no action.** Recorded here so a future review doesn't re-flag it. |

## Declined / not applicable

- Agent 3 (Efficiency) recorded no findings — pre-existing allocation profile, `ExplainedNumber` is a struct (no heap), `PartyBase` properties are field-backed.
- Agent 1 (Standards) PASS — all ADRs respected, models thin, adapter discipline preserved across the new code.

## Root-cause pattern — partial replication of a multi-layer convention

This is the same family as `feedback_replicate_vanilla_safety_gates_in_prefix.md` (now also covering additive GameModel overrides per the Codex 43 RCA). The shape: **the engine requires N coordinated layers, the author provided N−1.**

Past instances of this family:
- Codex review 36 (MixedFormations) — Prefix returned false but dropped a navmesh-availability gate buried in a vanilla *helper* method. N=2 (entry method + helper); author replicated 1.
- Codex review 43 (cultural-feats terrain, 2026-05-28) — additive GameModel override read vanilla for the *value* but dropped the *application conditions* (the `!IsCurrentlyAtSea` gate around the night penalty; the `PartyBaseHelper.HasFeat` culture-resolution precedence). N=2 (value + conditions); author replicated 1.
- **This review (Finding 1)** — author added the *NPC definitions* but not the *culture's spawn-pool registration*. N=2 (npcs_*.xml + taom_spcultures.xml `<notable_templates>`); author wrote 1.

Each instance differs in domain (Harmony, GameModel, XML data) but shares the same authorial blind spot: extracting the layer you're *focused on* and missing the layer the engine *actually reads*.

## Why each deep-review agent missed Finding 1 (or caught it, in Agent 5's case)

- **Agent 1 (Standards):** the violation is data-side, not C# code-side. Out of scope for the ADR/convention checks Agent 1 runs.
- **Agent 2 (Compatibility):** scope is TaleWorlds API signatures. The XML data-flow gap is outside the API-signature lens.
- **Agent 3 (Efficiency):** hot-path/allocation review. Not a perf concern.
- **Agent 4 (Completeness):** verified the new NPC templates **exist** in `npcs_*.xml` (the literal todo item was satisfied). It did NOT trace whether those NPCs are referenced by any culture's spawn pool. This is the gap most worth fixing — Completeness should mean "the data is reachable by the engine," not "the data exists on disk."
- **Agent 5 (Data Flow):** CAUGHT IT. The agent's check #9 ("RuralNotable template-count headroom") + #10 (Mordor Artisan edge case) led it to read `<notable_templates>` blocks in `taom_spcultures.xml` and compare against the NPC files. This is exactly the data-flow tracing this agent exists to do. Agent 5 also explicitly noted: *"the engine pools from `<notable_templates>`; the new `_23` RuralNotable will never spawn"* — calling out the engine consumption path, not just the existence of the data.

Net: the pipeline worked as designed. **Agent 5 (Data Flow) remains the highest-value agent in the suite**; this finding is the second consecutive review (after Codex 43's snow-weather Known Suspect) where a HIGH-severity catch came from the data-flow / vanilla-parity lens, not from per-file inspection.

## Feedback memory to codify

One new memory + one updated memory.

**New memory: `feedback_notable_template_two_layer_registration.md`** —

> When adding a new notable NPC (`occupation` ∈ {Merchant, Artisan, GangLeader, RuralNotable, Headman}) to `Main/_Module/ModuleData/characters/npcs_{culture}.xml`, you ALSO MUST add `<template name="NPCCharacter.<the-new-id>" />` to that culture's `<notable_templates>` block in `Main/_Module/ModuleData/taom_spcultures.xml`. The engine pools notable spawns from `<notable_templates>`, not from the NPC file directly — an NPC with `is_template="true"` that no culture references is unreachable. The convention `xml-data.md` documents the per-culture 26-NPC pattern but does not call out the second layer; this rule does.
>
> Mechanically: also confirm Preacher and headman additions in the same way — they're in the NPC file but reached through the same `<notable_templates>` registration.
>
> RCA source: `docs/reviews/rca-cultural-feats-3pack-2026-05-31.md`.

**Updated memory: `feedback_replicate_vanilla_safety_gates_in_prefix.md`** — generalize once more. The existing description already covers Prefix-returns-false → additive GameModel overrides. Add a third bullet: **XML data references that the engine consumes via a separate registration layer** (notable spawn pools, party-template member refs, equipment-roster bindings — anything where the data lives in file A but the engine reads file B's reference to it).

## Doc updates

- **`.claude/rules/xml-data.md`** — add one paragraph to the "Culture NPC Naming Convention" section: *"NPCs with `is_template='true'` are only reachable when the culture's `<notable_templates>` block (in `taom_spcultures.xml` or its XSLT) lists them via `<template name='NPCCharacter.<id>' />`. Adding an NPC to `npcs_{culture}.xml` is necessary but not sufficient — both layers are required."*
- **Optional validator extension** (deferred): a new schema-rule in `tools/schemas/taom_npccharacter.json` could flag `is_template='true'` notables that are unreferenced by any `<notable_templates>` block, mirroring how the existing `BROKEN_*_REF` rules catch the opposite direction. Filed as a follow-up.

## Verification

- `python tools/validate_moduledata.py` → PASS (no validation issues).
- `dotnet test TAOM.Tests/.../CulturalFeats` → 180 passed / 0 failed.
- (Full-suite test re-run after the fix is the next step before the closing commit.)
- In-game verification still outstanding: a 7-day weekly tick after a save load should now spawn `spc_notable_{culture}_23` in one of the new RuralNotable slots in each of the 4 affected cultures' villages.

## Process improvement triggered by this RCA

1. New memory + extended sibling memory (above).
2. `.claude/rules/xml-data.md` paragraph (above).
3. GitHub issue creation enforced in the next commit's discipline (Agent 4's process gap).
4. Filed follow-up: schema-rule for unreferenced notable templates.

---

## Codex follow-up (post-deep-review, 2026-05-31)

`/review-codex` (gpt-5.5, xhigh) ran after the deep-review fix landed. Verdict: **0 CRITICAL / 0 HIGH / 1 MEDIUM / 1 LOW.** All 10 Known Suspects either no-bug or disputed-with-evidence (notably S2 — the vanilla `hero.CurrentSettlement` NRE risk — was DISPUTED with a clean rebuttal: the sole vanilla caller iterates `settlement.Notables` and the invariant holds since notables are settlement-staying heroes).

### Codex findings

| # | Sev | Bug | Action |
|---|-----|-----|--------|
| C1 | MED | `TaomPartyTroopUpgradeModel.cs:26` still uses `Owner?.Culture ?? party.Culture` — same systemic pattern Codex 43 and this RCA's deep-review phase fixed for speed + size models. Isengard/Rohan mounted-upgrade-cost feats can resolve from `Owner` or `party.Culture` only, skipping vanilla's `LeaderHero` first and `Settlement` fallback. | **Fixed** — switched to `CultureFeatAdapter.FromOrNull(party)`. |
| C2 | LOW | `docs/features/cultural-feats.md` party-size table still showed pre-retune values (Gundabad +30%, Dol Guldur +25%, Gondor +10%, Mordor +30%). | **Fixed** — table rows updated to retuned values with a "retuned 2026-05-31" annotation. |

### Broader audit triggered by Codex C1

Codex flagged ONE remaining narrow-resolver model. I grepped all of `Main/Features/**/Models/` for the same pattern and found **3 additional models** Codex didn't enumerate but had the same systemic gap:

| Model | Old pattern | Fix |
|-------|-------------|-----|
| `TaomFoodConsumptionModel.cs:21` | `party.Party?.Owner?.Culture` (no fallback at all) | `CultureFeatAdapter.FromOrNull(party.Party)` |
| `TaomPartyMoraleModel.cs:25` | same as food | `CultureFeatAdapter.FromOrNull(party.Party)` |
| `TaomPartyHealingModel.cs:39` | `party.Owner?.Culture ?? party.Culture` | `CultureFeatAdapter.ResolvePartyCulture(party)` (raw `CultureObject?` overload — this model needs the `StringId` for a per-culture config lookup, not a feat check) |

**Helper expansion:** `CultureFeatAdapter` gained a public static `ResolvePartyCulture(PartyBase?)` returning `CultureObject?` directly. The existing `FromOrNull(PartyBase?)` overload now delegates to it (single source of truth for the precedence walk).

### Root-cause pattern (now THREE consecutive reviews)

The "partial replication of a multi-layer convention" family captured in `feedback_replicate_vanilla_safety_gates_in_prefix.md` has another consecutive instance:

- Codex 36 (MixedFormations) — dropped a vanilla navmesh-availability gate.
- Codex 43 (cultural-feats terrain, 2026-05-28) — dropped vanilla's night land-gate + culture-resolution precedence.
- Deep-review Agent 5 (this delivery, 2026-05-31, earlier today) — defined NPCs without registering them in the spawn pool.
- **Codex 44 (this delivery, 2026-05-31, just now)** — extended the same culture-resolution precedence fix to 2 of 6 GameModels, missed 4 sibling models.

This last instance is a NEW sub-pattern: *when fixing a systemic pattern in one model, audit ALL sibling models for the same pattern.* The 2026-05-28 RCA fixed the resolver in speed + size; the natural next question was "do other Default*Model overrides do the same lookup?" — I didn't ask it. Codex did.

**Preventive action:** add a one-line rule to AGENTS.md and a new feedback memory entry — *"when fixing a culture-resolution pattern (or any other per-model boundary convention) in one GameModel, grep ALL `Main/Features/**/Models/Taom*Model.cs` for sibling instances of the same pattern before declaring done."*

### Things Codex did particularly well (review 44)

1. **Identified a sibling-pattern bug the deep-review agents missed.** The 5 deep-review agents only inspected files in the diff; `TaomPartyTroopUpgradeModel.cs` wasn't in the diff, so it was out of their scope. Codex independently checked sibling models and caught the pattern repetition.
2. **Calibrated dispute on S2.** The Agent 2 (Compatibility) flag about vanilla `hero.CurrentSettlement` NRE risk was DISPUTED with a clean line of reasoning: vanilla's sole caller passes `Settlement.Notables`, notables are settlement-staying, so the invariant holds. This avoided a false-positive guard that would have added complexity for no real risk.
3. **Calibrated finding count.** Only 2 findings reported, 1 MED + 1 LOW. No noise, no overclaiming. After the deep-review already caught the HIGH (notable templates), Codex correctly read the room and surfaced what was actually still latent.

### What Codex got wrong / could improve

Nothing material this round. The "verification note" at the end noted that Codex tried `dotnet test` but was blocked by a sandbox permission on `C:\Users\mikew\AppData\Local\Microsoft SDKs` — that's an environment issue, not a Codex failure. Codex correctly reported it and didn't fabricate a test result.

### Verification after the broader-audit fix

- `dotnet test TAOM.Tests` → **2760 passed / 0 failed / 2 skipped** (up from 2735, auto-discovered tests for the helper paths).
- ModuleData validator still clean.
- The 4 GameModel fixes are boundary-only changes — same character as the Codex 43 fix; no unit tests are required at the model level (thin entry points, per gamemodels rule).

### Codified preventive actions (this Codex round)

1. **New systemic rule** added to AGENTS.md "What Codex does well" / "Bugs Codex catches": *sibling-pattern audits across GameModels.*
2. **Updated memory** `feedback_replicate_vanilla_safety_gates_in_prefix.md` (already broadened twice) — added a fourth note: when fixing a culture-resolution or feat-precedence pattern in one model, **grep `Main/Features/**/Models/Taom*Model.cs` for sibling instances** before declaring done. The pattern repeats more than the author thinks.
