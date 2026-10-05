# RCA: troll gear lockout and class-capped tournament prizes (2026-10-02)

**Summary.** Mike asked that troll weapons and armour never be bought, smithed or won, and that every tournament
prize be light, medium or heavy class and below. The change set `is_merchandise="false"` and `is_hidden="true"` in
the dev install's Armory, added the `CREATURE_GEAR_OBTAINABLE` gate, made `ArmourMarketplaceGate` honour the XML
flag with gating off, and rebuilt the prize pools on armour classes (`TournamentPrizeRules`). The deep review ran
Standards and XML as lenses; Engine compatibility and Data flow hit the weekly usage limit before reporting and were
run by hand by the orchestrator, as were the four wave-two lenses (the same limit). One HIGH (delivery), one MEDIUM
and seven LOW findings were confirmed; every one in this change's code is fixed.

## Findings

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | HIGH | The Armory fix lived only in the dev install; all three `E:\LOTRAOM_Releases\<channel>` Armories still let players buy, loot and smith troll gear, and the gate reads only the dev install | delivery | The plan said "live Armory" and treated it as player-facing; the memory `armory-is-shipped-to-players` records that players get the Armory from the releases folder at the next editor package, and its three-part guard (replay script in `tools/`, gate, snapshot README block) was applied one part in three | `tools/lock_creature_gear.py` (`--modules` targets a channel), the snapshot README "APPLIED EDIT" block, the troll-race doc names the channel step; propagation is Mike's call |
| 2 | MEDIUM | Three new decisions in `BuildPrizePool` (culture fallback, culture scope, merchandise source) were added inside the `Items.All` loop no test reaches; the fallback guards a crash | testing | The loop was already labelled "not unit-testable", so new logic went where the label said tests cannot follow | Each decision is now a pure `TournamentPrizeRules` call (`PreferCulture`, `XmlMerchandise`, `Fits`) with tests; the loop only reads items |
| 3 | LOW | `IMarketplaceStockGate` and `IArmourGateService` docs still said every item qualifies with gating off | docs drift | The behaviour change was made in the implementation; the interface comments that state the contract were not grepped | Reworded both; lesson below |
| 4 | LOW | The gate read an undefined crafting piece as hidden (`hidden.get(pid, True)`) | silent pass | A convenience default chose the passing answer | The default now reports the piece as undefined, with a test |
| 5 | LOW | A path-scoped rule row carried an incident narrative | knowledge tiers | The row was written like a feature doc | Trimmed to trigger, attributes and repair |
| 6 | LOW | Test hygiene: three test names off the `Method_State_Expected` form, no test that the elite band refuses light or civilian, a redundant floor test | testing | Written quickly after the first green | Renamed, added, deleted |
| 7 | LOW | `TournamentPrizeRules` comments omitted civilian kit and misdescribed the 2f floor as engine Tier1 | comment accuracy | Tier is `Round(Tierf) - 1`, so the floor also drops half of Tier2 | Reworded |
| 8 | LOW | `BuildPrizePool`'s `cultureId` should be `string?` | nullability | `.editorconfig` silences CS8600 to CS8625 | Annotated |
| 9 | LOW | A piece finding points at the weapon's file and line | gate ergonomics | The registry keeps a piece's hidden flag, not its location | Not applied: the piece id is in the message, so a grep finds it |

Pre-existing, outside this change (follow-ups, no issue filed yet): Troll Mace I's `modifier_group="false"` names no
modifier group (already in `docs/modding/open-questions.md`); `ITournamentService` has one implementation and no
fake (ADR-002 amendment; removing it touches the single-owner `SubModule.cs`, which another session holds);
`TaomTournamentModel.GetTournamentStartChance` keeps its siege and hero-count rules inline (gamemodels rule 4);
`audit_polearm_shield_parity.py` exits 1 on four stale `KNOWN_FAILURES` rows for `wm_mordor_set1_polearm_a02`,
removed in `ea0592eb`.

## Root-cause pattern

Findings 1 and 3 share one shape: a change to what reaches the player was verified where the author was looking
(the dev install, the implementation) and not where the contract is stated or consumed (the release folder, the
interface). Finding 2 is the testing face of the same habit: logic was placed where the existing label said it
could not be tested, instead of moving the logic.

## Why each lens missed or caught these

- **Standards** caught 2, 3, 5 to 8; it was not asked about delivery.
- **XML & ModuleData** caught 1, 4 and 9 by running the gate against a release channel, which the author had not.
- **Engine compatibility** and **Data flow** never reported (usage limit). The orchestrator re-ran their checks: the
  engine facts in `arena.md` match v1.5.3, and `ApplyGating` (`SubModule.OnGameInitializationFinished`) runs before a
  new game's tournaments roll prizes (`TournamentCampaignBehavior.OnNewGameCreatedPartialFollowUpEnd`), so the prize
  pool never sees an empty gate. **A convergence pass on the fix diff is owed** when the limit resets.

## Feedback memories to codify

None new. `armory-is-shipped-to-players` already held the delivery fact and the three-part guard; the lesson below
makes it reach the next live-Armory change.

## Convergence pass (2026-10-04)

The pass owed above ran on 2026-10-04 as one `deep-reviewer` (Opus 5.5, max effort) on `bc39f6e4` and the four live
Armory edits, inside the Tournament Rewards review (workflow `wf_614275cb-eec`, whose checker confirmed each item).

- **Findings 2 to 8:** resolved at HEAD, each re-read (the pure `TournamentPrizeRules` calls and their tests, the
  interface comments, the undefined-piece report, the rule row, the test names, the tier comment, `string?`).
  Finding 9 stands as decided.
- **Finding 1 (HIGH, delivery): still open for two channels.** The testing channel's Armory (its tester build of
  2026-10-02) passes `CREATURE_GEAR_OBTAINABLE`; patreon and public (v2.0.29.5) fail it with 15 errors each. The
  HIGH rule needs the decision on record: lock both with `tools/lock_creature_gear.py --modules <channel>/Modules
  --apply` before either is published, or ship the lock in their next editor package. Mike's decision (2026-10-04):
  the next editor package, built from the dev install, carries the lock to both.
- **New, fixed in the same change:** the snapshot README's troll block named backups the 2026-10-04 sweep had moved
  and said no channel had the lock; `arena.md` said an old save always re-rolls its prize at the join menu, while the
  engine keeps a saved prize until the hero count changes (`FightTournamentGame.cs:333`), as `armour-acquisition.md`
  already said; `/armory-audit` never ran `CREATURE_GEAR_OBTAINABLE`, though an Armory reinstall is exactly when the
  lock reverts.
- **New, follow-up:** when `ApplyGating` throws, its catch clears the record and class snapshot that both consumers
  of this fix now read, so culture stalls admit every item and the prize class falls back to engine tier (error path
  only; no gate failure has been logged). Fix with a test, or document it.
