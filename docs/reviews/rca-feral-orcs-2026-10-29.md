# RCA: Feral Orcs bandit faction (#1063), 2026-10-29

## Top line

The Feral Orcs (`0799af376`) are a hideout bandit faction whose clan ships in TAOM and whose 23 dens ship in
TAOM_Map. The tier 1 review ran Codex (`ultra`) and lens 5 (Data flow); lenses 2 and 7 stopped at the account's
weekly limit before they reported. Codex found two crash paths, both born from the module split, that every
earlier bandit faction already had but no review had traced: a clan with no dens (TAOM with an older map) throws on
every new game, and a den with no clan (a save made before the clan) throws from the Gold Rush incident. Mike asked
for the commit before the fixes, so `0799af376` records both as known limitations; this follow-up fixes them with
`Patch125_HideoutClanGuard` and closes the other findings.

## Findings

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | HIGH (Codex, lens 5) | TAOM with a TAOM_Map that lacks the dens: `GetInfestedHideoutCount` reads `_hideouts[clan.Culture]` unguarded and throws `KeyNotFoundException` at new-game stage 11. No channel's map package had the dens | data flow across modules | The plan treated "author the dens before the clan" (bandit-management.md) as an authoring order on one machine; nothing checked the order per release channel, and `release-process.md` had a ship-together paragraph for every kingdom pair but none for hideouts | `Patch125` returns 0 for a culture with no dens; a release-process check in both directions |
| 2 | HIGH (Codex) | A pre-change save loads the 23 dens (settlements load on a saved campaign) but not the clan (factions do not). `AddBanditToHideout` then dereferences a null clan, reachable from the Gold Rush incident | save compatibility | The plan said "new campaigns only" without reading `SandBoxManager.InitializeSandboxXMLs`. Lens 5 traced the load and found no crash on load but stopped before the incident caller | `Patch125` returns null for a bandit den with no clan; the doc now says what an old save gets |
| 3 | MEDIUM (Codex, lens 5) | `BanditHideoutLiveDataTests` filtered clans through the hand list `HideoutOnlyCultures`, so a new hideout culture missing from the list escaped the gate | testing | The gate reused a fixture list instead of the engine's own predicate | The gate derives the cultures from `taom_spcultures.xml` (`is_bandit` and `can_have_settlement`) and asserts it found `feral_orcs` |
| 4 | MEDIUM (Codex) | The scene gate passed when a den lost its `Location` (an empty sequence has no missing scene) | testing | The gate was proven only on the data it was written against | Each den must name exactly one scene; red proven on a copy of the module tree with one den stripped |
| 5 | MEDIUM (lens 5) | Bandit wearers re-tiered 13 Mordor, Gundabad and Dol Guldur pieces (markets, loot, armoury levels, the next restat) | data side effect | The Phase A builder regenerated `armour_classes.xml` to quiet a drift warning without asking what the change meant | Mike chose to exempt the five troops (`_ARMOUR_LADDER_EXEMPT`); the table is byte-identical to `3950d71c8` again |
| 6 | LOW (lens 5) | The next ranged-ladder apply would swap the archer's Mordor bow for the goblin one | data side effect | Same as 5: a line claim added to quiet a warning | Mike chose both bows: the file is on no line and the archer is in `_RANGED_LADDER_EXEMPT` |
| 7 | LOW (lens 5) | Feral warbands could rout; every orc culture never routs | gameplay parity | The culture-keyed lists were checked for "fails closed", not for "does the default fit" | `culture_doctrines.json` gives `feral_orcs` the Gundabad doctrine (Mike) |
| 8 | LOW (lens 5) | Feral parties upgrade in the field and their prisoners can never be recruited; no other TAOM bandit line does either | gameplay parity | An all-Bandit-culture troop line was new and unreviewed against `PartyUpgraderCampaignBehavior` and `IsPrisonerRecruitable` | Mike kept the upgrades; both behaviours are documented in `feral-orcs.md` |
| 9 | LOW (lens 5) | Stale counts in `bandit-management.md`, a stale culture comment, a `map_icon` claim no code reads, and "a loaded save has no Feral Orcs" | docs | Docs written from the plan, not from the review's evidence | Corrected |
| 10 | LOW (orchestrator) | `0799af376` regenerated the name tag table, which then required tagged BR and IT rows the commit lacked: `NameGrammarTagTests` fails on that commit | verification | `/verify` ran the C# suite before the tool-test fixes regenerated the table, and the suite was not rerun after | Rerun the C# suite after any generator a tool test asks for; fixed here |
| 11 | LOW (orchestrator) | The doc and #1063 described where the dens lie before anyone measured it | evidence | Prose written ahead of its evidence | Measured each den's nearest town or castle; the doc table comes from that run |

Disagreement, resolved: lens 5 rated the old-save case MEDIUM (no crash on load), Codex HIGH (a crash from the
incident). Both are right about what they traced; the v1.5.6 `CampaignIncidentEffects.InfestNearbyHideout` and
`AddBanditToHideout` bodies confirm Codex's path.

## Root-cause pattern

Findings 1 and 2 are one defect class: **a pair of objects that must load together, split across modules or
across the save boundary, with an unguarded engine lookup from one to the other.** TAOM already guards one member of
the class (Patch65, a lord's culture that owns no settlement). The bandit pair existed since the first LOTR bandit
cultures; the Feral Orcs were the first pair added after a release, which is what exposed it. Findings 5 and 6 share
a second theme: a builder quieted a validator warning by changing shared data (a regenerated table, a line claim)
without asking what the change did to other cultures.

## Why each lens missed or did not run

- **Lens 5 (Data flow)** found 1, 3, 5 to 9 and half of 2. It traced the old-save load to "no crash on load" and
  stopped at the hideout behaviour; the incident caller sits in another class.
- **Lenses 2 and 7** stopped at the weekly usage limit before `cd06da904` and resumed after it, with both commits in
  scope. Their findings are the second wave below.
- **Codex** found 1 to 4. It did not raise 5 to 8 as defects (it confirmed 5's attribution and refuted drift).

## Second wave (lenses 7 and 2, after `cd06da904`)

| # | Sev | Finding | Why missed | Preventive action |
|---|---|---|---|---|
| 12 | HIGH (lens 7) | `tools/translation_name_tags.json` as committed in `0799af376` and `cd06da904` is not valid JSON: one tag landed in `IT.plain_by_category`, the other after the closing brace. Eight tools and `NameGrammarTagTests` fail on a clean checkout | The orchestrator staged two lines of a file that also held another session's lines with `git diff -U0` plus `git apply --cached --unidiff-zero`; with no context lines the hunks applied at the wrong offsets, and the staged blob was never parsed. The local tree hid it, because the working copy was valid | Rebuilt the blob from the working copy minus the other session's keys, with the generator's own writer, parsed it, and staged it with `git hash-object --path` and `update-index` |
| 13 | HIGH (lens 7) | Snagas carried a shield in every set and the two-handed `wm_dol_goldur_axe_a01` in two; slots draw independently, so half the culture's basic troop got a weapon the AI will not draw | The scope check was "no polearm on a shield roster", narrower than the rule (`requires_no_shield`, across sets, `.claude/rules/troops.md`); `audit_polearm_shield_parity.py` passes it and lists it only under WARN | One-handed `wm_dol_goldur_1h_mace_a01`; every feral troop's weapons checked against its crafting template |
| 14 | MEDIUM (lens 7) | Snaga shields were player-start twins (220 hit points, "(Starter)") | Picked by the Phase A builder from a list that did not mark starter items | Real `sm_mordor_shield_small_a/_b` |
| 15 | MEDIUM (lens 7) | The den names were in no translation cache, so the next rebuild would revert all 12 languages to English | The writer wrote the live rows and nothing else | 23 ids added to all 12 caches |
| 16 | MEDIUM (lens 7) | BR and IT den names had no grammar tag | The tag rules had no head noun for Covil or Covo | Head nouns added, table regenerated, live rows tagged; coverage complete |
| 17 | MEDIUM (lens 2) | The doc said the faction never routs; doctrines act only with Culture Doctrine on, off by default. The review question to Mike also said every orc culture never routs, which is false (Mordor, goblin and Misty Mountain orcs rout) | The orchestrator relayed lens 5's claim into a decision question without checking it | Doc corrected; `ShippedCultureDoctrinesConfigTests` pins `feral_orcs`; Mike told the premise was wrong |
| 18 | MEDIUM (lens 2) | Two in-game steps could not pass: the encyclopedia shows no bandit clans, and `hideout_feral_1` is not the den a new game fills | Steps written from the plan, not from the engine | Steps rewritten |
| 19 | LOW (lens 2) | Registry and patch comment named the `:211` caller a new-game spawn and missed `AddNewHideouts`; no catalogue row for the injected `_hideouts`; `patch-targets.md` not regenerated | Caller list written before the full read | Corrected; reflection-site row and `[DataRow]`; snapshot regenerated |
| 20 | LOW (lens 7) | The 23 live loc rows ended in `\r\n` in files whose rows end in `\r\r\n`; the writer parsed nothing before writing | The writer detected `\r\n` anywhere in the file | Rows rewritten with the file's own terminator by a script that parses before writing |
| 21 | LOW (lens 7) | Commit bodies state that the faction never routs "like every other orc culture" and that the names are English everywhere (the den names are translated) | Bodies written from the same unchecked claim as 17 | Corrected in this commit's body |

## Follow-ups

- FOLLOW-UP: `GarrisonCultureCoverageTests` lists minor factions by hand and lacks `feral_orcs`. no issue: a bandit culture owns no garrison, so the test does not apply; noted for the next edit of that test.
- The settlement distance cache was rebuilt by Mike at 14:31 and holds all 23 dens (lens 2).

## Lessons codified

- `docs/reviews/lessons/harmony-il.md`: "A pair that must load together across modules or a save needs a guard on
  every engine lookup between them".
- `docs/reviews/lessons/build-tooling-workflow.md`: "Stage part of a shared generated file by building and parsing
  the blob, never with a zero-context patch".
