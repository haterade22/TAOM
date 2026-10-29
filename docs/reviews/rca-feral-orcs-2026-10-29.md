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
- **Lenses 2 and 7** stopped at the weekly usage limit before reporting (their partial work is saved). Their scope
  (engine attribute semantics, XML gates) is covered here by Codex's refuted suspects 4, 6 and 7 and by the gates
  rerun for this commit; they can resume after the limit resets on 2026-10-31.
- **Codex** found 1 to 4. It did not raise 5 to 8 as defects (it confirmed 5's attribution and refuted drift).

## Follow-ups

- FOLLOW-UP: the settlement distance cache predates the dens (2026-10-28); missing pairs are computed lazily, so it degrades rather than crashes. Rebuild it together with the Dorwinion and South Rhun owner, whose rows it would also bake in. no issue: a rebuild is a release step, recorded in `feral-orcs.md`.
- FOLLOW-UP: `GarrisonCultureCoverageTests` lists minor factions by hand and lacks `feral_orcs`. no issue: a bandit culture owns no garrison, so the test does not apply; noted for the next edit of that test.

## Lessons codified

`docs/reviews/lessons/harmony-il.md`: "A pair that must load together across modules or a save needs a guard on
every engine lookup between them".
