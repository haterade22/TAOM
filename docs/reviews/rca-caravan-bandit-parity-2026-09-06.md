# RCA: caravan / bandit strength parity (#543, #544, #549)

**Date:** 2026-09-06
**Changeset:** `tools/rebalance_template_power.py` (new), 50 retuned party templates,
`AiPartySizeService.ApplyCaravanScaling`, the four repaired Rohan caravan NPCs.
**Review:** `/deep-review`, 6 agents (5 core plus the mandatory tooling-correctness agent, launched
because the changeset adds a data-mutating script).

## Top line

No CRITICAL findings, no data corruption, no API incompatibility. Standards, compatibility,
efficiency and completeness all passed. Eleven findings, of which one is a genuine cross-feature
regression that a per-file review could not have seen, four are latent correctness hazards in the
new tool, two are factual errors in prose I wrote, and two are balance consequences of the target
that were never checked before the review asked for them.

The single most useful thing the review did was force numbers onto three questions I had asserted
without measuring: the early-game bandit spawn range, the remaining useful range of an existing MCM
slider, and who else consumes the XML I rewrote.

## Findings

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | MED (FIXED) | `SupplyLines` builds its caravans from `culture.CaravanPartyTemplates[0]`, the template the retune resized, but is not an `IsCaravan` party so the paired cap raise cannot reach it. Escort 20-29 to 60-70, provisioning cost 2.4x to 6x | cross-feature data flow | The change was designed as two coupled halves for AI notable caravans. Nobody asked who ELSE reads the XML half. Three files, two unrelated features | **FIXED**: SupplyLines now has its own `supply_caravan_template_*` crew templates (#549). Rule below |
| 2 | MED | Tool docstring and feature doc both stated tiers 7-10 come from `battle_balance_config.json`. The engine reads the MCM-settable `TaomSettings.Tier7Power..Tier10Power`; the JSON arm is unreachable dead code | fabricated fact | I read `CalculateTierPower`, saw the config parameter, and did not follow the `tier >= 7` switch to the end. The JSON values equal the compiled defaults, so every number agreed and nothing contradicted me | Fixed in both. Rule below |
| 3 | MED | `MOUNTED_MULTIPLIER = 1.2` hardcoded in the tool while the engine reads the settable `TaomSettings.MountedMultiplier`. Live: Gundabad, Harad and Rhun rosters carry mounted troops | config drift | I verified the value once and treated a settable property as a constant | Coupling documented in the tool docstring |
| 4 | MED | `troop_power` had no concept of `is_hero`. The engine costs a hero on `Level / 4 + 1` with a 1.5x multiplier, so a hero-flagged troop in any stack would have been silently mis-budgeted | latent correctness | No template carries a hero today, so no test could fail and no output looked wrong | Hero troops now refused like unknown ids, skip-and-report. 3 tests |
| 5 | MED | `solve_band` is not a fixed point in isolation; feeding its output back as `shape` drifts the mins. Safe only because `canonical_shape_for` is a constant table. `test_is_idempotent` asserted the general property using a fixture that does not trigger it | overclaiming test | I fixed the same bug class in `solve_flat` and did not re-ask the question of its sibling. The test name told me it was covered | Caveat in the docstring, test renamed to what it proves, 3 tests pin the real invariant |
| 6 | LOW | `main()` returned 0 even when templates were skipped for unresolved troops | silent partial success | Exit codes were never exercised; no test covers the CLI | Returns 2 when anything was skipped, and says why |
| 7 | LOW | "Order matters: elite_caravan must be tested before caravan" is false; both patterns are `^...$` anchored | wrong comment | Written from the general rule about prefix matching without checking that the anchors already made it moot | Comment corrected to state the real reason |
| 8 | LOW | Shipped-data tests re-parsed every file under `troops/` and `characters/` once per test method, seven times for identical results | test cost | Wrote the loader per-test for simplicity and never looked at the aggregate | Cached behind `[ClassInitialize]` |
| 9 | INFO | "Caravans cap at 20-50" omits v1.4.8's naval sub-branch, where a player-owned naval caravan caps at 66 | inexact prose | Read the branch I needed and summarised the rest | Noted in the feature doc; unreachable in TAOM, floor of 30 unaffected |
| 10 | FIXED | Early-game raider spawn compressed to 20-33 bodies with spread 10-15, against 31-75 with spread ~44 before, because `max` moved close to `min` while the early ratio spans only 0.08-0.32. (The agent reported 7-9 and 9-11; those were PER STACK, not per party, and I repeated them before checking) | balance | I verified the endgame clamp and never computed the low end | **FIXED**: bandit floor set to 12.5% of the ceiling, giving 12-32 bodies |
| 11 | OPEN | `BanditPartySizeCurve`'s useful range collapses. With the lower ceiling the roster clamps at roughly a quarter of campaign progress, after which the slider does nothing | user-facing promise | I checked the knob still functions, not that it still has room to function in | **DOCUMENTED** in `bandit-management.md` beside the curve it describes |

## The root-cause pattern

Findings 1, 2, 5, 7 and 10 are one shape: **I verified the half of a fact I needed and stopped.**

- I read `CalculateTierPower` for the tiers 0-6 formula and stopped before the `tier >= 7` switch (2).
- I fixed `solve_flat`'s convergence and did not ask the same of `solve_band` (5).
- I checked the bandit clamp at the top of the range and not the bottom (10).
- I knew the caravan templates fed AI caravans and did not ask who else read them (1).
- I applied the prefix-matching rule without checking the anchors already handled it (7).

Each is individually defensible and collectively a habit. The tell is that in every case the thing I
checked came back clean, which felt like confirmation and was actually just a smaller question than
the one that mattered.

Finding 2 is the one that matters most, because it is the "never fabricate" rule: I wrote a specific
mechanical claim into a tool docstring and a feature doc from a partial read, and it was wrong. That
it was numerically harmless is luck, not diligence. The JSON and the compiled defaults agree today.

## Why each agent missed what it missed

| Agent | Caught | Missed, and why |
|---|---|---|
| Standards | Nothing to catch; clean pass, and correctly judged the ungated `ApplyCaravanScaling` defensible | Cannot see cross-feature data flow or numeric balance |
| API compatibility | All 13 engine claims verified, plus the naval sub-branch (9) I had summarised away | Its brief was my claims. It cannot find a claim I never made, which is what 1 and 10 were |
| Efficiency | The test-loading cost (8) | Scoped to hot paths; 2/4/5 are correctness, not cost |
| Completeness | The untracked feature doc | Checks presence, not truth. A doc can exist, be linked, and state a wrong fact (2) |
| Data flow | 1, 10, 11, the three findings nothing else could reach | Nearly missed 1 too: it surfaced only because the prompt named SupplyLines as a suspicion to investigate |
| Tooling | 2, 3, 4, 5, 6, 7 | Would not have run at all under the 5-agent baseline. Every one of its findings is in a file no core agent reviews |

**The mandatory tooling agent earned its place.** Six of eleven findings came from it, in a Python
file the five C#-centric core agents do not read. This is the second time that rule has paid
(previously the 2026-05-28 scene tooling BOM defect).

**The data-flow agent's finding needed correcting, which is itself the lesson.** It rated finding 1
as HIGH on the strength of two predicted consequences, daily desertion and a -0.69 speed penalty.
Both are wrong: the desertion gate requires `IsLordParty || IsCaravan || IsGarrison` and a supply
caravan is none of them, and the party does not move by map speed because
`SupplyCaravanService.cs:363` assigns `party.Position` directly. Verifying the finding downgraded it
to MEDIUM and changed what the fix has to achieve. A confident agent report is a hypothesis
(`evidence-over-claims.md` A.1), and this is a clean worked example: the finding was real, the
reasoning attached to it was not.

## Preventive actions

**1. New rule: when you retune shared data, enumerate its consumers before you enumerate its values.**
The changeset's own design note said the XML and the C# cap were "two halves of one change." That
framing is exactly what hid finding 1: it asserts a closed system. Before editing any
`ModuleData` entity that a culture, clan or component binds, grep for every reader of that binding
(here `CaravanPartyTemplates`), not just the one the change is about. Candidate for
`.claude/rules/moduledata-validation.md`, whose existing cross-reference guidance covers *ids that
break* and not *sizes that shift under a second consumer*.

**2. Extend the deep-review data-flow agent prompt** with an explicit "who else reads this XML
collection" step for any changed `ModuleData` file, phrased as an enumeration rather than a
suspicion. The agent found it only because I named SupplyLines in the prompt; the next author will
not know to.

**3. For the fabrication (2): follow a `switch` to its last arm before describing what it reads.**
No new rule needed, this is `evidence-over-claims.md` C already. Worth recording as a concrete
instance because the failure mode was subtle: the wrong claim produced correct numbers.

**4. Repeat-offender check.** Finding 5 is the second appearance of the fixed-point bug in one
session (`solve_flat` was rewritten for exactly this, and `gundabad_raiders_boss_party_template`
oscillated 18/19 on alternate runs before the fix). Preventive action taken: the caveat is now in
the docstring AND pinned by a test that asserts the docstring still warns about it, so removing the
warning fails a test.

## Balance decisions, resolved

Findings 10 and 11 were consequences of the chosen target rather than bugs. Both were put to
the user and decided:

- **Finding 10 is fixed.** The bandit floor is now 12.5% of the ceiling (`min_frac`), putting
  early parties at 12-32 bodies with spread 12-17, against 20-33 with spread 10-15 before.
  Stated honestly: this mostly lowers the early floor. It cannot restore the original 44-body
  spread, which needed the original 200-troop ceiling.
- **Finding 11 is documented rather than changed.** `BanditPartySizeCurve` clamping out around
  a quarter of the way through a campaign is inherent to a lower ceiling, and is now recorded
  in `docs/features/bandit-management.md` beside the curve it describes.

Original framing, kept for the record:

- **Early-game compression.** Rhun raiders now spawn 7-9 and Harad 9-11 in the early game, where the
  vanilla randomisation used to give 10-20. The variance that made early bandit encounters feel
  uneven is largely gone for the higher-tier cultures. Widening those templates' `min` would restore
  it at the cost of a higher floor.
- **`BanditPartySizeCurve` headroom.** The slider still works but clamps out at roughly a quarter of
  the way through a campaign, so raising it past that point does nothing. Either accept it, raise the
  raider power budget, or document the effective range in the MCM hint.

Both are recorded here rather than deferred silently, per the deep-review skill's no-silent-deferral
rule.

## Verification after fixes

- 47 tool tests (was 41; +6 for hero handling and the canonical-shape invariant), all green.
- Tool still idempotent: 0 stacks changed on re-run, parity unchanged at `L = 1.18`.
- Line endings preserved: the tool file is still 100% CRLF, the feature doc still 100% LF.
- Full C# suite green.
