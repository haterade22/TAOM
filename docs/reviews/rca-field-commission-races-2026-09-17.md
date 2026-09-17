# RCA: Battlefield Promotions, the race allow-list that assumed a Free Peoples player (2026-09-17)

**Issue:** [#612](https://github.com/haterade22/TAOM/issues/612) · **Feature:** [#376](https://github.com/haterade22/TAOM/issues/376) Field Commission · **Review:** `/deep-review`, five agents, 0 HIGH

## Top line

A player running Isengard reported that promotions never fire and found the cause themselves:
`allowedRaceNames` shipped as `human`/`dwarf`/`elf`. That list was a deliberate #376 decision
("orcs promotable into the player clan" was called unacceptable for a total conversion), written on
the premise of a Free Peoples player. Character creation offers seven evil races across six cultures,
so for every one of those campaigns the feature was inert from the day it shipped (2026-08-05):
merit banked after every won battle, `CanPromote` failed every troop, nothing logged.

The fix is the ten soldier races as the default, with the creature and unique-hero races
(`cave_troll`, `hill_troll`, `nazghul`, `saruman`, `sauron`) left out. The deep review then found
that a player's own typo in the list reproduces the same silent outcome, and fixed that too.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|-----|-----|----------|------------|-------------------|
| 1 | HIGH (the issue) | The shipped allow-list excluded every playable evil race, so the feature never ran for a Mordor, Isengard, Gundabad, Dol Guldur, Goblin-town or Misty Mountains player | Design premise | #376 was written from the donor's Free Peoples framing and nobody checked it against `charactercreation/cultures.json`, which lists the playable races per culture. The exclusion produced no log line and no failing test, because merit still banked and `CanPromote` fails closed by design | Default widened; `ShippedJson_AllowedRaceNames_MatchCompiledDefault` pins the three copies together. Rule below: a player-facing allow-list is checked against the set of things a player can BE, not against the author's own campaign |
| 2 | MED | A misspelt `allowedRaceNames` entry (a pack author's or a player's) never matches a troop, so that race is silently unpromotable, the exact #612 symptom from a different cause | Config validation | The provider sanitised blanks only. The "string the consumer branches on" case of the config-validation rule (`csharp-architecture.md`) was read as the switch-with-default shape and not recognised in list-membership form. The provider has no race table (FaceGen's), which made the check look impossible at load time | `FieldCommissionMeritService.WarnUnknownRaceNamesOnce`: every configured name checked against `IRaceManager.IsValidRaceName` once per process, a warning naming the entry. Test `CanPromote_ConfiguredRaceNameUnknownToEngine_WarnsOnceNamingTheEntry` |
| 3 | LOW | `RepoPath`/`ThisFile` (`[CallerFilePath]` repo locator) existed as three identical private copies across test files, the third added by this change | Duplication | Copied from the sibling test because the sibling had it; nobody grepped for a shared helper first | `TAOM.Tests/Infrastructure/RepoPaths.cs`, imported with `using static`; the three private copies deleted |
| 4 | LOW (not fixed) | `BasicTableauRaceGuard` has verified only `uruk` as safe for the agentless tableau renderer, so a promoted companion of the six other new races renders a human-headed clan-screen thumbnail | Adjacent surface | Pre-existing for every orc lord and notable; promoted companions make it a surface the player visits every session. Verification is an in-game per-race render test, outside a config change | Recorded as a known limitation in `field-commission.md` and the CHANGELOG. Extend `TableauSafeRaceNames` one race at a time after the render test (see `hero-race.md`) |

## Root-cause pattern

Findings 1 and 2 are the same shape: **an allow-list that fails closed produces no signal when it is
wrong.** Fail-closed is the right posture for an unknown race id (the validate-before-lookup rule),
but it means a short list and a misspelt list look identical to a working one: the feature is
simply quiet. Nothing in the original build asked "what does the player see when the list is
wrong?", and the answer was "nothing, forever". The #376 design brief recorded the intent (creatures
unpromotable) and the mechanism (race allow-list) but never the enumeration it was supposed to be
checked against, so the list encoded the author's campaign instead of the game's.

## Why each agent missed these

These findings came out of the review rather than being missed by it, but the original #376 pass
and this change's first draft both had blind spots worth naming:

- **Standards:** the config-validation rule was applied to numeric fields (NaN, ranges, ordering) and
  to switch-style strings. A list of names the consumer does membership on is the same class and
  was not in the pattern list. This pass caught it because the rule text was quoted in the brief.
- **Completeness:** "tests exist for the default" was true and insufficient; the test asserted the
  default's VALUE, not its RELATION to anything (playable races). The new pin tests fix the
  relation between the three copies and the engine race set; the relation to `cultures.json` is
  documented, not pinned, because the two lists are set-equal by coincidence of content today and
  a future non-playable soldier race would be a legitimate difference.
- **Data flow:** the original build traced JSON to consumer and stopped; it did not trace the
  consumer's OTHER input (the troop's race) back to where those values come from
  (`cultures.json`, the troop XML). This pass did, and confirmed the ten-name list is set-equal to
  the union of playable CC races.
- **Compatibility:** nothing to miss; the engine side was never the problem. This pass proved
  `FillFrom` copies `Race` and body generation keys on the troop race, from the v1.5.3 DLLs.

## Feedback memories to codify

One lesson, appended to `docs/reviews/lessons/data-content-cultures.md`: a player-facing allow-list
is checked against the enumeration of what a player can be or field, and a fail-closed gate on
user-editable data warns when the data cannot match anything. No new harness rule; the existing
config-validation rule covers it once its list-membership form is named, which this RCA does.
