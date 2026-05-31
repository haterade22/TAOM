# Codex Adversarial Review — cultural-feats per-occupation town notable counts

## Feature in one paragraph

A follow-up refactor on top of commit `582275f` (which originally shipped 4 uniform per-(culture, settlement-type) notable-count feats with `AdditionType.AddFactor` (multiplier) semantics for Isengard / Dol Guldur / Mordor / Gundabad). In-game testing showed Isengard town had only ~8 notables (Rohan, by contrast, has many small settlements). The uniform-multiplier design also collapsed all four cultures to the same notable count (8) because vanilla town targets are tiny ints (2 Merchant + 1 Artisan + 2 GangLeader = 5) and `ceil(2 * 1.05) == ceil(2 * 1.50) == 3` — the +5% Mordor and +50% Dol Guldur feats both rounded to the same value. The refactor:

- Introduces a TAOM-owned enum `NotableOccupationKind { Other, Merchant, GangLeader, Artisan, RuralNotable, Headman }`.
- Replaces 4 uniform town feats with 9 per-(culture, occupation) town feats using `AdditionType.Add` (flat raw count added per occupation).
- Keeps village feats with `AdditionType.AddFactor` (no asymmetric per-occupation need).
- Changes `ICulturalFeatsService.ApplyNotableCountFeat` from `(culture, bool isTown, baseCount)` to `(culture, NotableOccupationKind occupation, baseCount)`.
- Adds 8 new Gang Leader NPCs to Isengard (`gl5..gl12`) and 9 to Dol Guldur (`gl5..gl13`) so the culture template pools meet the new targets.
- Registers each new NPC in BOTH layers: `<NPCCharacter id="...">` in `npcs_<culture>.xml` AND `<template name="NPCCharacter...." />` in the culture's `<notable_templates>` block in `taom_spcultures.xml`. (Two-layer registration was the systemic lesson from the 582275f RCA.)

Target distributions:

| Culture | Vanilla (M / A / GL) | New (M / A / GL) | Add values |
|---|---|---|---|
| Isengard | 2 / 1 / 2 = 5 | 4 / 2 / 14 = 20 | +2 / +1 / +12 |
| Dol Guldur | 2 / 1 / 2 = 5 | 3 / 2 / 15 = 20 | +1 / +1 / +13 |
| Mordor | 2 / 1 / 2 = 5 | 2 / 1 / 4 = 7 | (none) / (none) / +2 |
| Gundabad | 2 / 1 / 2 = 5 | 2 / 2 / 5 = 9 | (none) / +1 / +3 |

Village targets unchanged (3 RuralNotable + 2 Headman = 5 via AddFactor across all 4 cultures).

## TAOM ID CHEATSHEET

Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale/North, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar, shaghana=Shaghana, abanissa=Abanissa

Culture IDs (custom, used here): gondor, mordor, erebor, rivendell, lothlorien, mirkwood, isengard, gundabad, dolguldur, umbar

Culture IDs (XSLT/vanilla, NOT used in this refactor): vlandia=Rohan, empire=Dunland, etc.

NOTE: "dol_guldur" is NOT valid — use `dolguldur`. "Rohan" is NOT a valid culture id — use `vlandia`.

## READ FIRST

- `docs/features/cultural-feats.md` — feature doc (just-updated)
- `CHANGELOG.md` — top entry describes this refactor
- `.claude/rules/xml-data.md` — the two-layer NPC registration rule + culture naming convention
- `docs/reviews/rca-cultural-feats-3pack-2026-05-31.md` — RCA from yesterday's commit (582275f) that codified the two-layer rule

## Known Suspects (please CONFIRM or DISPUTE each)

1. **Template pool sufficiency vs. new targets.** The new targets are: Isengard 14 GL templates needed (pool has exactly 14 after additions), Dol Guldur 15 GL needed (pool has exactly 15), Mordor 4 GL needed (pool has 6 existing — `gl1, _10, _11, _gl4, _12, _13`), Gundabad 5 GL needed (pool has 6 existing). Does the vanilla notable-respawn engine cycle / sample-with-replacement from `<notable_templates>` so that having a pool exactly equal to the target is sufficient? Or does it require headroom (pool > target) to avoid "duplicate notable" symptoms? Decompile `HeroCreator.CreateNotable` and the relevant calls in `NotablesCampaignBehavior` to settle this.

2. **`AdditionType.Add` semantics with vanilla `baseCount = 0`.** `CulturalFeatsService.ApplyNotableCountFeat` has a guard `if (baseCount <= 0) return baseCount;` at the top. Is this guard correct, or does it suppress an intended +Add bonus that would push the count from 0 to N (e.g., for cultures that vanilla returns 0 for in some occupation)? Specifically — does vanilla `DefaultNotableSpawnModel.GetTargetNotableCountForSettlement` ever return 0 for `Merchant`/`Artisan`/`GangLeader` in a town that we'd want to override? If yes, the guard is wrong. If no, the guard is a defensive no-op for an impossible case and is fine.

3. **Mapping of `Occupation` values to `NotableOccupationKind.Other`.** `MapOccupation` in `TaomNotableSpawnModel.cs` switches on `Occupation` and maps `Merchant`/`Artisan`/`GangLeader`/`RuralNotable`/`Headman` to the matching TAOM enum, and everything else to `Other`. Does v1.4.5 vanilla `DefaultNotableSpawnModel.GetTargetNotableCountForSettlement` return a non-zero target for any other `Occupation` value (e.g., `Preacher`, `Mercenary`, `Lord`)? If yes, mapping to `Other` (which returns `baseCount` unchanged) is still correct (no override, vanilla wins). If we accidentally mapped one of those to Merchant/Artisan/etc., that would be a bug — verify the switch handles only the 5 spawn-pool occupations.

4. **Per-feat `Add` value math vs. documented targets.** Walk the 9 new feats and confirm each `Initialize(name, desc, **bonus**, isPositiveEffect:true, AdditionType.Add)` value matches the documented target:
   - Isengard Merchant: +2 (vanilla 2 → 4)
   - Isengard Artisan: +1 (vanilla 1 → 2)
   - Isengard GangLeader: +12 (vanilla 2 → 14)
   - Dol Guldur Merchant: +1 (vanilla 2 → 3)
   - Dol Guldur Artisan: +1 (vanilla 1 → 2)
   - Dol Guldur GangLeader: +13 (vanilla 2 → 15)
   - Mordor GangLeader: +2 (vanilla 2 → 4)
   - Gundabad Artisan: +1 (vanilla 1 → 2)
   - Gundabad GangLeader: +3 (vanilla 2 → 5)

5. **Feat string-ID consistency.** Confirm that the 9 string IDs registered in `TaomCulturalFeats.RegisterAll` (e.g., `taom_isengard_notable_count_town_merchant`) exactly match the 9 `<feat id="...">` lines in `taom_spcultures.xml` under the four cultures' `<cultural_feats>` blocks. Off-by-one underscores or pluralization typos here = the feat never fires.

6. **Old uniform feats completely removed.** Grep the entire repo for the four deleted feat IDs (`taom_isengard_notable_count_town`, `taom_dolguldur_notable_count_town`, `taom_mordor_notable_count_town`, `taom_gundabad_notable_count_town` — without per-occupation suffix). Any leftover reference is dead config (slow burn — looks fine on day 1, fails when someone tries to reuse the ID).

7. **Two-layer registration for the 17 new NPCs.** For each of `spc_notable_isengard_gl5..gl12` (8) and `spc_notable_dolguldur_gl5..gl13` (9):
   - The `<NPCCharacter id="..." is_template="true" occupation="GangLeader" culture="Culture.{isengard|dolguldur}" ...>` definition is in `Main/_Module/ModuleData/characters/npcs_<culture>.xml`.
   - The `<template name="NPCCharacter.<id>" />` entry is in the matching culture's `<notable_templates>` block in `Main/_Module/ModuleData/taom_spcultures.xml`.
   - Both layers required (this is the 582275f RCA lesson).

8. **Village isolation (cross-feat-feature safety).** When `ApplyNotableCountFeat` is called for `NotableOccupationKind.RuralNotable` / `Headman`, the service should consult the existing village `AddFactor` feats (e.g., `_isengardNotableCountVillage`, `_dolguldurNotableCountVillage`, `_mordorNotableCountVillage`, `_gundabadNotableCountVillage`). Confirm these still exist, are still registered in XML, still use `AddFactor` semantics, and don't accidentally fire on a town occupation.

## File lists

### C# (in scope)

- `Main/Features/CulturalFeats/NotableOccupationKind.cs` (NEW)
- `Main/Features/CulturalFeats/ICulturalFeatsService.cs` (signature change on one method)
- `Main/Features/CulturalFeats/CulturalFeatsService.cs` (dispatch rewrite for `ApplyNotableCountFeat`)
- `Main/Features/CulturalFeats/Models/TaomNotableSpawnModel.cs` (GameModel override; adds `MapOccupation` boundary helper)
- `Main/Features/CulturalFeats/TaomCulturalFeats.cs` (−4 fields/accessors/Register/Initialize/yields, +9 fields/accessors/Register/Initialize/yields)

### XML (in scope)

- `Main/_Module/ModuleData/taom_spcultures.xml` — 4 cultures' `<cultural_feats>` blocks rewritten (−4 lines / +9 lines), `<notable_templates>` blocks for Isengard (+8 GL templates) and Dol Guldur (+9 GL templates).
- `Main/_Module/ModuleData/characters/npcs_isengard.xml` — +8 `<NPCCharacter id="spc_notable_isengard_gl5..gl12">` definitions.
- `Main/_Module/ModuleData/characters/npcs_dolguldur.xml` — +9 `<NPCCharacter id="spc_notable_dolguldur_gl5..gl13">` definitions.

### Tests (in scope)

- `TAOM.Tests/Features/CulturalFeats/CulturalFeatsServiceTests.cs` — rewrites the 8 old uniform-multiplier tests as 13 per-occupation dispatch tests.
- `TAOM.Tests/Features/CulturalFeats/TaomCulturalFeatsDefinitionTests.cs` — bumps total feat count 92 → 97, per-culture expected counts updated.

### Docs

- `docs/features/cultural-feats.md` — Notable-Count Feats section rewritten.
- `CHANGELOG.md` — new entry above the prior 3-pack entry.

## REQUIRED SECTIONS

### 1. VANILLA CODE (paste decompiled code blocks)

Decompile the installed v1.4.5 DLL and paste these exact bodies for reference:

- `TaleWorlds.CampaignSystem.GameComponents.DefaultNotableSpawnModel.GetTargetNotableCountForSettlement(Settlement, Occupation)`
- The vanilla `NotableSpawnModel` base class (abstract surface)
- `TaleWorlds.CampaignSystem.CharacterDevelopment.FeatObject` — confirm `AdditionType` enum has `Add` and `AddFactor` and what each means in the vanilla math (look at one consumer site).
- `Helpers.PartyBaseHelper.HasFeat(PartyBase, FeatObject)` — confirm it walks the precedence (LeaderHero.Culture → party.Culture → Owner.Culture → Settlement.Culture).
- One vanilla call site of `GetTargetNotableCountForSettlement` (likely in `NotablesCampaignBehavior` / `HeroCreator`) — to settle Known Suspect #1 (does the engine fail closed when `notable_templates` pool size equals target?).

Use `ilspycmd` against `E:\Steam\steamapps\common\Mount & Blade II Bannerlord\bin\Win64_Shipping_Client\*.dll` for authoritative signatures — the `E:\Decompiled_Bannerlord\` folder is a v1.4.5 dump and is fine for browsing, but verify signatures against installed DLLs.

### 2. Per-occupation Add math walk

Walk each of the 9 new `FeatObject.Initialize(...)` calls in `TaomCulturalFeats.cs` and:
- Confirm the bonus arg matches the target table in Known Suspect #4.
- Confirm `isPositiveEffect: true` is passed (encyclopedia rendering).
- Confirm `AdditionType.Add` is passed (not `AddFactor` — that would silently apply a factor to baseCount).

### 3. Two-layer NPC registration audit (CRITICAL)

For each of the 17 new NPCs, output a 2-column line:

```
spc_notable_isengard_gl5    | NPCCharacter? YES/NO | Template line? YES/NO
spc_notable_isengard_gl6    | ...
...
spc_notable_dolguldur_gl13  | ...
```

If ANY of the 34 cells is "NO", that's a CRITICAL finding (notable spawns will silently reuse another template → clone notables).

Also verify each new NPC's `occupation="GangLeader"`, `culture="Culture.{isengard|dolguldur}"`, `is_template="true"`, and that the `<face>` / `<Equipments>` / `<face_key_template>` blocks aren't malformed (engine-rejection symptoms).

### 4. CONFIG CROSS-REFERENCE

For the 9 new feats:
- C# `Register("taom_isengard_notable_count_town_merchant")` ↔ XML `<feat id="taom_isengard_notable_count_town_merchant" />`
- C# `Register("taom_isengard_notable_count_town_artisan")` ↔ XML `<feat id="taom_isengard_notable_count_town_artisan" />`
- ... walk all 9 string IDs both directions (any in C# but not in XML = orphan feat; any in XML but not in C# = unregistered, parse error or silent ignore).

For the 4 deleted feats:
- `grep -r 'taom_isengard_notable_count_town"' .` (note the trailing quote — to exclude per-occupation IDs)
- ... for all 4 deleted IDs. Zero matches each = clean.

### 5. FINDINGS OR OBSERVATIONS

For each Known Suspect: CONFIRMED / DISPUTED with a one-paragraph reason and (if CONFIRMED) the file:line of the bug.

Then, list any ADDITIONAL findings you discover beyond the suspects — same format.

## QUALITY GATES

- Paste vanilla code as inline code blocks (not links or descriptions).
- For each Known Suspect, output an explicit CONFIRMED / DISPUTED verdict — do not silently skip.
- For the two-layer registration table, output all 34 cells — do not summarize.
- Severity scale: CRITICAL / HIGH / MEDIUM / LOW / INFO. Each finding gets exactly one.
- Where a finding requires action, name the file + approximate line.

## Prior review lessons

SUCCESSES from prior reviews:
- Config ID cross-ref caught `rohan` (invalid; should be `vlandia`) and `dol_guldur` (invalid; should be `dolguldur`) typos.
- Vanilla decompilation caught missing gates that all 5 Claude deep-review agents missed.
- Lifecycle tracing caught stale caches surviving past their intended scope.
- Codex review #28 caught the two-layer registration bug class that motivated this refactor's discipline.

FAILURES from prior reviews:
- Codex once assumed `empire = Rohan` (it is Dunland) and flagged correct code as a bug.
- Codex sometimes flags vanilla-matching code as a bug because it doesn't decompile the vanilla target.
- Codex has skipped hard sections ("CONFIG CROSS-REFERENCE" left empty). Don't skip. If a section has no findings, write "no findings" explicitly.

## Output location

Write your review to `docs/reviews/codex-adversarial-cultural-feats-per-occupation-2026-05-31.md`. (This file — the prompt — lives at `docs/reviews/codex-adversarial-cultural-feats-per-occupation-2026-05-31.prompt.md`.)
