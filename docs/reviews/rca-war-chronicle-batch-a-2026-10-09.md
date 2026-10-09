# RCA: War Chronicle Batch A, deep review and Codex pass, #765 (2026-10-09)

**Summary.** Batch A of the War Chronicle work (#765, committed as `d340a725c`) is the war ledger and
its analyzer, the timed war-effect registry, the extra prisoner-escape roll, the rally layer (baselines,
tiers, volunteer and escape multipliers) and the shared volunteer production service. A deep review of 53
findings (source: the orchestrator's `batchA_fixes.md`) confirmed 42, refuted 3 and left 8 NIT and INFO
items unverified. 39 confirmed findings were fixed in the batch; 3 were left for a separate change
(B31, B44, B45) and one fix is partly open (B16, the reload marker). Codex then found 2 MEDIUM and 1 LOW
that the review had missed: the analyzer's capital verdict ignored a capital already lost in the first
row (F1) and let one kingdom's complete rows hide another's gap (F2), and the rally left a saved effect
alone when the tier did not change (F3). All three are fixed here, each with a test seen failing first.

## Findings (deep review)

The highest severity was B01 (HIGH). Every confirmed finding below was fixed in Batch A unless listed
under "Not applied".

| # | Sev | Bug | Category |
|---|---|---|---|
| B01 | HIGH | Capital target (b) and `--compare` said PASS with no evidence: a drifted key, a log from day 150 or one day of data all passed, and `--compare` never showed malformed lines. | False pass |
| B02 | MEDIUM | The extra escape roll dropped vanilla's relative perk and trait factors from `DailyHeroTick`. | Parity gap |
| B03 | MEDIUM | The volunteer multiplier reached towns and villages only; castles never call the engine probability. | Wrong reach |
| B04 | MEDIUM | The MCM hint said the rally never applies to the player's kingdom, but only a player-ruled one was excluded. | Over-claim |
| B05 | MEDIUM | The mid-campaign baseline path for an old save never ran: `SyncData` is not called when the save has no record. | Lifecycle |
| B06 | MEDIUM | Nothing checked that the C# ledger format and the Python analyzer agree. | Missing contract test |
| B07 | MEDIUM | The analyzer's tests left guards and error paths unpinned. | Weak test |
| B08 | MEDIUM | The NaN check moved into the new volunteer service had no NaN test. | Missing test |
| B09 | MEDIUM | `IVolunteerProductionService` had one implementation and no fake. | Unearned interface |
| B10 | LOW | The daily tick ran escapes before the rally refreshed its effects. | Ordering |
| B11 | LOW | The player-clan-maps-to-no-kingdom rule sat in a static adapter helper with no unit test. | Missing test |
| B12 | LOW | The rally status command re-derived the rally-is-on rule in the entry point. | Logic in entry point |
| B13 | LOW | Nullable annotations contradicted the documented null handling. | Contract drift |
| B15 | LOW | The analyzer read `BANNERLORD_GAME_DIR` raw instead of through `tools/_gamedir.py`. | Shared helper skipped |
| B16 | LOW | The analyzer double-counted across a save reload and let argument order pick the winning copy of a day. | Over-claim |
| B17 | LOW | A huge `version` integer in the save threw `OverflowException` out of `Parse`. | Missing validation |
| B18 | LOW | A bad side `base` crashed the analyzer with a traceback; the exit-code contract was narrower than the code. | Missing validation |
| B19 | LOW | `--compare` ignored `--csv`, `--cid` and log paths without saying so. | Silent option |
| B20 | LOW | `open(path, "w").write(...)` relied on reference counting and the locale encoding. | Python hygiene |
| B21 | LOW | Two skip branches (a null kingdom in the list) had no test. | Skip-guard exhaustion |
| B22 | LOW | Several pure-code branches were untested (`FormatAdded`, the unknown phase, save-codec branches). | Missing test |
| B23 | LOW | A test named for a null key asserted only the returned value; its stub answered any key. | Vacuous test |
| B24 | LOW | `war_ledger_dump` broke the console naming rule (verb first, `print_` for read-only). | Naming |
| B27 | LOW | The rally loss was derived twice, in the rally and in the ledger formatter. | Duplication |
| B28 | LOW | `IRallyTierSource` existed for one fake a real store serves as well. | Unearned interface |
| B29 | LOW | No container test resolved the volunteer production service. | Missing wiring test |
| B30 | LOW | The kingdom snapshot adapter had no no-campaign test. | Missing test |
| B32 | LOW | `--compare` read one file per side, so a run spanning a restart could not be compared. | Tooling gap |
| B33 | LOW | A `SubModule.cs` comment still described the removed `ICulturalFeatsService` parameter. | Stale text |
| B34 | LOW | `bannerlord-together-compat.md` said 223 and 222 in one row. | Stale text |
| B35 | LOW | A snapshot comment called two fields inputs of vanilla's daily tick; they are not. | Over-claim |
| B36 | LOW | The MCM hint and doc missed that the volunteer roll also raises volunteer tier upgrades. | Over-claim |
| B37 | LOW | The doc said nothing in Bannerlord pulls a losing kingdom back; the volunteer base rises under 46 fief points. | Over-claim |
| B38 | LOW | "Co-op is host-only" lacked the BannerlordTogether caveat (no session probe, every peer is authority). | Over-claim |
| B39 | LOW | The gamemodel registry row still said the volunteer override applies the culture respawn feats. | Stale text |
| B40 | LOW | The feature doc had no Dependencies section and missed the new TroopProgression dependency. | Missing doc |
| B41 | LOW | A class summary said the rally comes in later milestones; it ships in this batch. | Stale text |
| B42 | LOW | The doc gave `python -m pytest`; the runner is `unittest`. | Wrong command |
| B43 | LOW | The doc said 130 excluded settings where 354 - 223 is 131. | Stale count |

## Refuted and not applied

- **B14 refuted:** `LogChronicleResolution` and `ChronicleEvent` have no production caller in Batch A, but
  the approved plan puts the line in M1 and the Batch B worktree already calls it. Kept.
- **B25 refuted:** the escape count is dropped at the call site, but the pass logs one `Escape:` line per
  breakout to the same log file, so the count is a grep away. No change.
- **B26 refuted:** the opaque chronicle save section has no writer in Batch A, but the built Batch B
  uses it unchanged. Kept.
- **B31 not applied:** the wiring test strips comments from every `Main` file. The fix belongs in
  `TAOM.Tests/Infrastructure/RepoPaths.cs` (shared, committed), as a separate change.
- **B44 not applied:** the same scan in `WarOfTheRingWiringTests`; the same one-place fix as B31.
- **B45 not applied:** the momentum behavior still passes a saved chunk count unchecked to
  `new List<string>(count)`; share one guarded sync helper when momentum is next touched.
- **B16, open:** the logs are read oldest first, but the `t=load` marker and the `h=` hours field that
  would drop an abandoned run's later days are not built. A reload of an older save still leaves those
  days in the report, and the docs say so. Needs a format decision.
- **B46 to B53:** eight NIT and INFO items were never verified and stay notes.

## Codex review (gpt-6-astra, after the deep-review fixes)

Raw output `docs/reviews/raw/codex-adversarial-war-chronicle-batch-a-2026-10-09.md` (gitignored).
0 CRITICAL, 0 HIGH, 2 MEDIUM, 1 LOW, 0 false positives. Codex ran 40 existing Python tests and probed the
production analyzer; the C# suite and the game were not run by Codex.

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| F1 | MEDIUM | The capital verdict counted a loss only on an observed 1 to 0 change. A first row already at `cap=0` was discarded, so a capital lost by day 50 gave UNKNOWN, and `cap=0` on all of days 1 to 119 gave PASS. | Evidence discarded | The B01 fix modelled "when did it change" and never asked what a single `cap=0` row already proves. | Any `cap=0` before day 120 fails the target. The report says "day N" for a seen transition and "lost by day N" otherwise. Tests with the finding's rows. |
| F2 | MEDIUM | Coverage of days 1 to 119 was a set of days across all kingdoms, so Mordor's complete rows hid Gondor's missing ones (held on day 1, unheld on day 120) and gave PASS. | Wrong grouping | The B01 fix wrote one coverage rule over "days" because the fixtures had one kingdom. | Coverage is per kingdom. A `cap=na` day, a kingdom first seen after day 1 and a short series are holes that name the kingdom; a kingdom with an `ev=destroyed` line needs days 1 to D-1 only; a destroyed kingdom with no rows is a hole. Docstring and `war-chronicle.md` updated. |
| F3 | LOW | The rally rebuilt a source only when the tier changed. A saved effect whose kind is now configured to 0 survived a same-tier refresh, because `Apply` skips zero and `kept` kept the source. | Reconcile on change, not on state | The tier-drop fix removed a source on a transition, so the same-tier case looked covered. | Every eligible refresh removes the source, then applies the current tier's nonzero kinds. |

Suspect dispositions: S1 escape parity disputed, no finding (every term matches v1.5.4 `AddFactor`
arithmetic); S2 volunteer reach confirmed as shared-pool reach, no bypass, no finding; S3 save path
disputed (reset in the constructor, chunk size under the 32767-byte limit); S4 rally numbers confirmed
only as F3; S5 float safety disputed, with the note that the volunteer service passes a nonfinite vanilla
base through unchanged; S6 ledger contract confirmed as F1 and F2, no format drift; S7 co-op: a stale
client registry is possible, divergent production is not shown under BannerlordCoop's gates, live
behaviour UNVERIFIED.

## Root-cause patterns

**A coverage check must be per entity.** F2 and, one level up, B01: "every day has a row" was answered
across all kingdoms, so the kingdom that mattered was covered by a different one. The fixtures had one
kingdom, so no test could tell the two readings apart.

**Throwing away affirmative evidence to keep a tidy event model.** F1: a first row with `cap=0` is proof
of a loss; the code wanted a transition and treated the rest as "no information".

**Cleanup keyed to a change instead of to the desired state.** F3 repeats the tier-drop shape: remove on
a transition, not on every refresh. Rebuild the whole owned set and the question disappears.

**A verdict that can say PASS must name what would make it say something else.** B01 and F2 were both
PASS reached by omission. Each verdict needs a fixture for the "unknown" cases, one entity at a time.

## Follow-ups (outside this change; issues on Mike's word)

- B16: decide the `t=load` marker and `h=` field, then drop an abandoned run's later days.
- B31 and B44: speed up `RepoPaths.StripComments` once, as its own change.
- B45: one guarded chunk-sync helper shared with momentum.
- Real-run check: `cap=na` makes a kingdom UNKNOWN. If real logs carry `cap=na` for a normal kingdom,
  decide whether that should read as "no capital" instead.

## Lessons recorded

`docs/reviews/lessons/testing-qa.md`: a coverage check must be per entity.
