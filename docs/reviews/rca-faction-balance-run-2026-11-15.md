# RCA: the first 150-day run of #1283, and the changes it called for

The unattended run `E:\Temp\scenario-runs\20261115-163251` (build 879c63d15) passed 150 days. It showed three things the #1283 batch
did not settle: Dunland and Gondor still went to war, TAOM logged no line that could say how, and Gondor's strength lead over Mordor
grew from 8,014 to 15,231. An architect read of the ledger found that Gondor gained a fief on 13 days and Harad lost one on 11 of them,
with Harad at 19 party slots.

The changes: one INFO line for each kingdom war TAOM allows (`DeclareWarAction_ApplyInternal_Patch`), one for each call to war that
starts, naming the caller (`DiplomacyBehavior`, from `OnCallToWarAgreementStartedEvent`), and Harad's clans 2 to 9 at tier 5
(`spclans.xslt`, 19 to 27 slots). The review ran at tier 1 for the patches (lens 2 and lens 5 at `max`) and tier 2 for the XSLT (lens 7
at `high`). Codex was skipped on the maintainer's word (usage limit). No lens found a HIGH or an incompatibility.

| # | Sev | Finding (source) | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | MED | A war TAOM allowed left no line, so the run could not say how Dunland and Gondor went to war (game run) | Unlogged outcome | The patch was written to veto; only its refusals were worth a line at the time | Fixed: an INFO line with the engine's `DeclareWarDetail`. Lesson in `campaign-mechanics.md` |
| 2 | MED | For a call to war the line names the called realm and the target, never the realm that made the call, so it could not answer the doc's own question (lens 5) | Missing actor | `ApplyByCallToWarAgreement` passes `(calledKingdom, target)`; the caller lives one frame up | Fixed: `DiplomacyBehavior` listens to `OnCallToWarAgreementStartedEvent` and writes `Call to war started: <caller> calls <called> against <target>`. A first version logged from the call-to-war prefix; lens 2 found it ran before the engine's ally check and, on a co-op host, after Coop's prefix had skipped it |
| 3 | MED | The #1283 reading procedure named no step that reads the new lines (lens 5) | Evidence nobody reads | The log line and the procedure were written apart | Fixed: step 6 of "How to verify in game" reads both lines and names each engine path |
| 4 | MED | The doc named only Harad's party slots, not the other effects of tier 5 (lens 7) | Partial effect list | The Isengard paragraph lists them; the Harad row was written from the slot count alone | Fixed: a "Harad party slots" section with party size, renown, influence, war scoring and new campaigns only |
| 5 | LOW | The patch comment said a rebellion reaches the line; both `ApplyByRebellion` callers have a clan on one side, which returns early (lens 2, lens 5) | Comment from a name | The enum member was read as a path | Fixed: the comment names kingdom pairs only |
| 6 | LOW | The test's example `rivendell -> empire (CausedByCallToWarAgreement)` read as Rivendell calling (lens 5) | Misleading fixture | The example was picked before the argument order was read | Fixed: the example is `empire_w -> empire` |
| 7 | LOW | No Key Files row for `spclans.xslt` (lens 7) | Doc gap | Only the Isengard file was listed | Fixed |
| 8 | LOW | On a co-op peer that defers to the host, "allowed" means TAOM deferred, and a Coop client can log one war twice (lens 2, lens 5) | Meaning under co-op | The line was judged in single player | Documented in `diplomacy.md`; no code change (`simplicity-criterion.md`) |

Lens 7 also corrected a figure in the builder's brief: Harad's clans have three to seven adult heroes, not four to seven. The doc and the
test say "at least three", which holds (`clan_aserai_8` has exactly three).

FOLLOW-UP #1343: on a co-op host, Coop's `StartCallToWarAgreement` prefix (default priority) is likely added before TAOM's and returns false, so TAOM's veto prefix on that method never runs and the gold moves before TAOM refuses the war (lens 2, [Likely], not seen in a live session). The fix it names, `[HarmonyPriority(Priority.High)]` as the other TAOM diplomacy prefixes use, changes co-op behaviour and needs a co-op test, so it is its own change. Fixed on trunk in e722addfc (PR #1347).

## Root-cause pattern

Findings 1 to 3 share one cause: a long run produced an outcome whose cause no line recorded, and the reading procedure did not ask for
it. A balance run is only as good as the questions its log can answer, so each question in "How to verify in game" needs a line that
answers it and a step that reads that line.

## Second run

Run `E:\Temp\scenario-runs\20261115-172417` met all three targets: the log named the war's path and caller
(`mistymountainorcs` called `empire` against `empire_w` on day 2), Harad held 16 castles on day 150, and the Gondor to Mordor gap
ended at 6,108. The figures are in `faction-balance.md`, "The second 150-day run".
