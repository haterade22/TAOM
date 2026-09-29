# RCA: Armour Acquisition (deep review, 2026-09-27)

## Top-line

`/deep-review` of the uncommitted armour acquisition feature (worktree `E:/repos/taom-armour-acquisition`,
branch `feat/armour-acquisition`, base `743818cf`) ran ten lenses in three waves: Standards, Engine
compatibility, Data flow split in two (gating and stock; the acquisition routes), the Step 2b adversarial
check, XML and ModuleData, Efficiency, Completeness, Tooling, then Design. Verdict before fixes: **NEEDS
FIXES**. Three CRITICAL ADR-007 violations (one adapter, one service, one service-facing port), three HIGH
(no test ran the sweep over a real town; the named-weapon list covered 4 of 17 hero items; the class-table
drift gate could pass silently), about a dozen MEDIUM and many LOW. Engine compatibility found 36 of 36
API uses correct. Mike took four behaviour-changing decisions in one question batch (lord kit sold at
level 3; all 17 hero items named; a donor culture for markets and lord kit; the daily sweep, the one-quest
harness, the moose route and per-kingdom upgrade links). Everything below was fixed in the same session,
and a Step 4.6 convergence reviewer then checked the fixes: it closed 14 of 15 and found one HIGH in a fix
(row 18), fixed test-first. The full C# suite stands at 11,066 pass, 2 skip, 1 fail (the translation seeds,
owed to Mike's paid run).

## Findings

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | CRITICAL | `ArmourStockSweepService.SweepTown(Settlement)`, `IMarketplaceStockGate.GetTownLevel(Settlement)` and `IArmouryTownAdapter` returning `Settlement` (Standards, adversarial) | ADR-007 | Copied the "opaque token" comment from `ICultureMarketplaceMaintenanceService`, which the 2026-06-12 audit had already ruled "exactly the erosion pattern" (triage-B DEBT-01). A precedent was read as a licence | Lesson (adapters): a precedent is not an exemption; key services by id. Fixed: id-keyed adapter, `ForTown(townId)`, `SweepTown(townId)` |
| 2 | HIGH | No test ran the sweep over a real town; `null!` tokens and an empty town list (adversarial, Completeness) | Test coverage | The sealed-type signature made the service untestable with distinct towns, so the tests passed `null!` and never entered the loop | Follows from #1: id-keyed services test with ids. Fixed: per-town sweep tests, the market gate, a RequiresGame wiring test |
| 3 | HIGH | `<NamedWeapons>` listed 4 of 17 hero items; the doc said no Glamdring exists (XML) | Unverified relay | A planning Explore agent searched by name ("Glamdring") and reported absence; the ids are `glamdring_sword` and friends. The absence claim was relayed to Mike and into the doc unchecked | Repeat offender of evidence-over-claims A.4. Lesson (misc): an absence claim needs an id grep of the live data before it is repeated. Fixed: 17 named, doc table corrected |
| 4 | HIGH | The class-table drift gate passed silently when the generator read another Armory or found no armour (Tooling) | Silent gate | The gate checked `<game_modules>/LOTRLOME_Armory` but the generator reads `rebalance_armor.ARMORY_DIR`; two roots, one assumed | Repeat of the 2026-09-18 XSD gate lesson (tooling lens rule 9). Fixed: NOT verified warnings, root match, tests both ways |
| 5 | MED | The XML non-merchandise market rule silently reversed Mike's 2026-09-23 moose decision (Data flow A) | Rule scope | A new global rule was checked against the feature's own items only, not against other features' recorded decisions about the items it newly covers | Lesson (data-content): list what a new global rule newly excludes and grep the docs for decisions about those ids. Fixed per Mike: moose routed, rule kept |
| 6 | MED | Every town starts at Barracks 1 or more, so "heavy at 1" was never a gate (Data flow A) | Unverified data assumption | The threshold was recommended to Mike without measuring the live Barracks distribution | Lesson (campaign-mechanics): measure the live distribution before recommending a threshold. Mike re-decided with the numbers |
| 7 | MED | The harness chain keyed on the current hero; a Player Switcher change or a `StartStep` throw stranded the line for good (Data flow B) | State lifecycle | The chain stored a "running" stage that nothing reconciled; completion was matched to the current main hero, not the quest's owner | Lesson (state-lifecycle): derive "running" from the engine's quest list and key completion on the quest's owner. Fixed by the one-quest redesign |
| 8 | MED | Harness step 2 (renown threshold) completed inside `QuestBase.StartQuest`, leaving a finished quest in `QuestManager` (Data flow B, Engine) | Engine ordering | The CareerQuest shell seeds thresholds and completes in `OnStartQuest`, before the engine adds the quest; no earlier quest was threshold-only | Lesson (campaign-mechanics) and a career-quest doc note. Fixed: the one quest has two counted deeds; the shell's latent bug is a follow-up |
| 9 | MED | Nine lord cultures own no Armory armour: their lord kit fell back to any culture's (vanilla pieces too), and their 22 towns could never sell heavy or elite (Data flow B, Design) | Data coverage | The culture key was taken as the kit key; the market side of the gap came from the flag closing the workshops' any-culture fallback | Lesson (data-content): a culture-keyed armour feature needs a donor map. Fixed: `armour_from` shared by pools and lord kit; `GetPieces` table-only |
| 10 | MED | One-time sweep latch: a visit ending, AI lords selling old loot or the player selling left gated stock in markets (Design) | Invariant enforcement | An invariant a daily process can break was enforced once | Lesson (state-lifecycle): enforce such an invariant with an idempotent periodic pass, not a latch. Fixed: daily per-town sweep |
| 11 | MED | ADR-002: sweep latch, event decisions and a direct `ChangeRelationAction` in behaviors; claim transaction and wallet in the presenter (Standards) | Thin entry points | Decisions that read engine state were left beside the engine reads | Fixed: `RollHarnessFind`, `Claim`, `CanClaimAt`, `ChangeRelationWith`, one affordability rule |
| 12 | MED | 77 upgrade links crossed kingdom kits sharing a folder (Tooling) | Data quality | The fallback treated the Armory folder as the culture; `rebalance_armor` already defines kit identity by `kingdom_key` | Fixed: kit-line fallback, table regenerated (93 links changed) |
| 13 | MED | Tooling crash paths: a conflicted table crashed `--apply` and the validator (blocking commits); untested CLI; two tests that could not fail (Tooling) | Tool robustness | Happy-path tests only | Fixed with tests for each path |
| 14 | MED | No gate checked the commission cultures, reward items, named weapon or marketplace culture ids (XML) | Missing gate | New C#-read configs with ids were added without a validator pass; the engine resolver never sees them | Fixed: `ARMOUR_ACQUISITION_REF` (ERROR, in the commit hook) |
| 15 | MED | The deleted-mesh audit could not see `reward_item` or the armour config (XML) | Missing coverage | New reference shapes in existing files | Fixed in `audit_deleted_mesh_impact.py` |
| 16 | MED | No wiring test for a save-owning module (Completeness) | Test coverage | The module pattern's wiring test was not copied | Fixed: `ArmourAcquisitionWiringTests` |
| 17 | LOW | Visiting armourers rolled level-3 towns; the master switch read two ways; `ApplyGating` ran for custom battles and left a half gate on failure; the offer latch stuck on a throw; `taom_lh_ready` hardcoded "level 3"; unused members; `!` after `IsNullOrEmpty`; price text joined outside `TextObject`; wrong items-load method named; the armory-audit step order; stale doc engine claims (elite prize fallback, workshop and loot wording, caravans) | Various | Per item | All fixed; the elite prize fallback and the player's own pieces are documented Known limitations |
| 18 | HIGH (convergence pass) | The `armour_from` fix itself did not reach players: CultureMarketplace's daily foreign-culture filter stripped the donated armour the next day, as its culture (`rivendell`) differs from the town's (`lindon`) and it is not routed | Fix interaction | The merge was tested at the pool (build) seam only; the filter, a second pass over the same roster with its own notion of "belongs here", was never exercised with a donated piece | Lesson (data-content): a new source of pool items must be recognised by every pass that prunes the roster. Fixed test-first: the filter keeps anything the town culture's own pool carries |
| 19 | LOW (convergence pass) | Stale "lord kit is never sold" and "three steps" wording in two C# doc comments, the MCM hint, the generator docstring, `feature-map.md` and `INDEX.md`; the `armour_from` merge runs with the master switch off, undocumented | Doc drift | Decisions changed mid-review; wording outside the feature doc was not re-swept | Fixed; the master-off merge is a documented Known limitation |
| 20 | MED (found in play, 2026-09-28) | `<NamedWeapons>` still missed ten hero weapons: Tuor's two `[Heirloom]` axes (one turned up for sale in Mike's game), Galadriel's sword and the seven `[Noldor]` swords of Fingon, Finarfin, Finwë, Ingwë, Túrin, Voronwë and Celegorm, three of which the ladder even offered as unnamed picks | Incomplete enumeration | Row 3's fix completed the list from the heroes people knew to look for; nobody searched the Armory's own markers of a hero item (the `[Heirloom]` tag, possessive names, a First Age line), and a name with no possessive ("Galadriel Sword") matched no search at all | Repeat of row 3. Lesson (data-content): enumerate candidates from the data's own markers and have Mike rule on the full list. Fixed per Mike: 27 named; Rivendell's weapon rung offers the Noldor swords and Tuor's axes, Lórien's Galadriel's sword; `Ladder_EveryNamedWeaponIsAWeaponRungChoice` pins the route |

## Root-cause patterns

- **Unverified facts behind decisions (#3, #6, #8).** Three findings trace to a fact assumed, not measured:
  a subagent's absence claim, the Barracks distribution, and the threshold objective's start state. Each
  reached Mike as a recommendation or a statement. The fix is the evidence rule applied to the planning
  phase, not only to the claims in a final report.
- **A precedent read as a licence (#1).** The copied comment even named its precedent. Precedent debt
  spreads unless the review reference lists it as debt where a builder will read it.
- **Once-only enforcement of a continuous invariant (#7, #10).** A stored "running" stage and a sweep latch
  both assumed nothing changes after they are set.
- **Silent gates, again (#4).** The tooling lens exists because of this class; the new gate still shipped
  with two roots and one check.

- **A fix verified at one seam only (#18).** The donor merge passed its own tests and was wrong in the
  game, because a second pass over the same roster disagreed about what belongs there. The Step 4.6
  convergence reviewer, reading the fix against its neighbours, is what caught it.

## Why the builder missed these, and which lens caught them

The feature was built test-first with a green suite, which proved each service in isolation and none of
the seams above. Standards caught ADR-007 and ADR-002; Data flow (both halves) caught the harness,
the moose, the Barracks distribution and the lord kit gap; Engine caught the step-2 ordering; XML caught
the named-weapon gap and the missing gates; Tooling caught the silent gate and the crash paths; Design
caught the latch and the market side of the donor gap; Completeness caught the missing wiring tests. The
split of Data flow into two reviewers is what found #7 and #9: a single data-flow pass over roughly 40
files would have had to trade depth for breadth.

## Feedback to codify

- Lessons appended: `adapters-taleworlds-api.md` (#1), `state-lifecycle-save.md` (#7, #10),
  `campaign-mechanics.md` (#6, #8), `data-content-cultures.md` (#5, #9), `build-tooling-workflow.md` (#4),
  `misc.md` (#3).
- No new rule file: evidence-over-claims A.4 and the tooling lens rule 9 already cover #3 and #4; both are
  repeat offenders, recorded here so the next review reads them.

## Follow-ups (pre-existing code, not this change)

- `ICultureMarketplaceMaintenanceService` and `ITownRosterAdapter` still take `Settlement` (triage-B
  DEBT-01); the id overloads added here are their migration path.
- The CareerQuest shell completes a threshold-only quest inside `QuestBase.StartQuest`
  (`CareerQuest.OnStartQuest`); no shipped quest is threshold-only now.
- The ADR-007 architecture test ADR-007 names (`TAOM.Tests/Architecture/AdapterPatternArchitectureTests.cs`)
  does not exist; a gate that scans public members of concrete services would have caught #1.
- No GitHub issues filed: the repo is public, so filing is Mike's call.
