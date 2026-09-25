# RCA: Order of Battle Auto-Assign (plan 022), deep review and Codex (2026-09-24)

## Top-line

Plan 022 wired the Order of Battle overlay's Assign Heroes button to `HeroAutoAssigner.PlanCaptains` through a
new boundary class, `OOBCaptainAutoAssigner` (branch `improve/022-order-of-battle-auto-assign`, diff
`1091f3b6..66e3fd59`). Seven deep-review lenses (Standards, Engine, Efficiency, Completeness, Data flow, Design,
XML) and one Codex adversarial review read it.

**No CRITICAL or HIGH, no engine incompatibility.** One MEDIUM defect, found independently by the Engine, Data
flow and Design lenses and by Codex (P2): in a siege assault the button never placed a companion who owns a horse.
Thirteen LOW findings (docs, tests, labels, line endings), all fixed on the branch. Seven design questions go to
Mike (report, "NEEDS MIKE").

The MEDIUM is a **repeat of a recorded category**: `lessons/adapters-taleworlds-api.md` "In OnAgentBuild the gear an
agent wears is `agent.SpawnEquipment`, not `Character.Equipment`" (#627, 2026-09-19). That lesson was scoped to one
callback, so a mission-time decision in a UI handler did not match it.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | MED | `OOBCaptainAutoAssigner.AddCandidate` classified each candidate from `new HeroCombatAdapter(hero)`, the campaign `Hero.BattleEquipment`. In a siege assault vanilla spawns every agent without a horse (`SandBoxSiegeMissionSpawnHandler.AfterStart` calls `SetSpawnHorses(false)` for both sides; `Mission.DecideAgentSpawnEquipment` clears the Horse slot of a clone, v1.5.3 `Mission.cs:4114-4118`), and the OOB class selector offers only classes 1, 2 and 5 there (`OrderOfBattleFormationItemVM.cs:755-760`). A horse owner still read as Cavalry or HorseArcher, which scores 0 on all three, so the button said "No hero suits an open captain slot." | Campaign state read for a mission decision | The plan copied the tooltip badge's adapter call "so the role matches the badge", and the badge was written for the party screen, where campaign gear is the truth. Nobody asked which equipment the agent actually spawned with; the plan's test plan named a field battle only, where the two agree | Fixed: `HeroCombatAdapter(Hero, Equipment)` overload; the boundary passes `item.Agent.SpawnEquipment ?? hero.BattleEquipment`. RED then green: `HeroCombatAdapterTests` (2). Siege step added to the owed in-game check. Lesson broadened (adapters-taleworlds-api) |
| 2 | LOW | `companion-tactics.md` said the action changes "nothing campaign-side or save-backed". The accept path writes `agent.Formation`, `Formation.Captain` and the formation banner, and at deployment end `SPOrderOfBattleVM.SaveConfiguration` persists the captains through `OrderOfBattleCampaignBehavior.SetFormationInfos`, which writes one of four saved lists chosen by siege and army (`SPOrderOfBattleVM.cs:256`, `OrderOfBattleCampaignBehavior.cs:161-180`) | Unverified premise used as a gate rationale | The plan stated it (plan line 242) and the builder copied it; "TAOM adds no save field" was read as "nothing is saved" | Doc corrected. Lesson (state-lifecycle-save) |
| 3 | LOW | The known limitation said Auto-Assign "does not move hero-troops between formations". A hero-troop is a candidate, and vanilla's accept path removes it from its old formation first (`OrderOfBattleVM.cs:1267-1276`) | Doc contradicts the code | Written from the plan's intent ("captains only"), not from the candidate loop | Doc corrected. One-off |
| 4 | LOW | `IOOBCaptainAutoAssigner` justified exposing `OrderOfBattleVM` as a "sealed" type; it is `public class OrderOfBattleVM : ViewModel` and `SPOrderOfBattleVM` derives from it | Copied claim | Copied from `IOrderOfBattleVMTracker.cs:8`, which carries the same wrong word | Fixed in the new file; the old one is a follow-up. Existing lesson "Cloning a sibling clones its unverified claims" (data-content-cultures) covers it |
| 5 | LOW | The boundary's two early returns (null VM, player not general) had no test; the plan's "structurally untestable" waiver was applied to the whole class | Over-broad test waiver | The waiver is true from the first vanilla handler call on, and was never narrowed | `OOBCaptainAutoAssignerTests` (2). Lesson (testing-qa) |
| 6 | LOW | `ReflectionSiteBindingTests` and `reflection-sites.md` still labelled the `_isActive` / `_dataSource` lookups `OOBOverlayService.cs:57/58`; the new constructor parameter moved them to 60/61 | Line-number labels drift | The plan listed both files as out of scope, and a label only shows in a failure message | Labels updated. One-off: the Standards lens counted 8 of the other 36 labels already off (not re-counted here), so a line-number label is a known weak pointer |
| 7 | LOW | `companion-tactics.md` tree line, Key Files, Dependencies and Changelog sections not updated for the new class | Doc sweep incomplete | The doc pass added the Auto-Assign subsection and did not walk the file's other sections | Fixed. One-off |
| 8 | LOW | The planner's `score > 0` threshold was unpinned: `>= 100` passed all 17 tests, so the two 50-point fits (ranged hero to class 5, horse archer to class 6) were never exercised | Threshold without a boundary test | Every test's chosen pair scored 100 | Two planner tests. Covered by the lesson in row 5 |
| 9 | LOW | `OOBButtonsVMTests` asserted delegation only; `TextObject.ToString` swallows a localization failure into an "Error at id" string, so a broken message would pass | Test oracle checks dispatch, not output | The plan's Step 4 gate named delegation as the test | One DataRow test per result status, asserting the shown text. Lesson (testing-qa) |
| 10 | LOW | The overlay's DI graph gained `IOOBCaptainAutoAssigner` with no wiring test; the tick patch resolves it inside an empty catch every frame, so a broken registration would hide both buttons silently | New DI edge unpinned | The graph resolves today; no lens-free check exists for this feature | `CompanionTacticsWiringTests` (DryIoc `Validate`). One-off |
| 11 | LOW | The 36 seeded language rows ended in a bare LF inside `\r\r\n` files. `translate_with_claude.py sync_missing_ids` splits on one terminator, so a run of LF lines fuses with `</strings>` and the NEXT seeded row lands after it, where `LocalizedTextManager.LoadLanguage` never reads it | Mixed line endings defeat a line-based tool | The rows were placed by the tool, misplaced, and moved by hand; the terminator was not restored. `LanguageFileCoverageTests` counts rows anywhere, so no gate sees misplacement | Rows converted to `\r\r\n` (binary round-trip). Lesson (localization-ui); the tool and gate fixes are follow-ups |
| 12 | LOW | An extra blank line before `</strings>` in `taom_module_strings.xml` | Tool side effect | `harvest_literal_loc_keys.py insert_rows` adds a blank line on every run despite its comment | Removed; tool fix is a follow-up. One-off |
| 13 | LOW | CHANGELOG and doc said the button places "companions"; candidates are every non-player hero vanilla lists for the player's team (family, and possibly allied lords) | Wording narrower than the code | The plan's summary said companions while its candidate contract said `UnassignedHeroes` plus `HeroTroops` | Wording corrected. Whether to restrict the set is a question for Mike |

## Root-cause pattern

Rows 1, 2, 3 and 13 share one cause: **the plan's prose was treated as verified fact about the engine**. The plan
said the adapter matched the badge, that the action touched nothing save-backed, and that it placed companions.
Each was a design intention or an unchecked belief, and the implementation followed it faithfully. The lenses and
Codex caught all four by reading the engine path end to end (spawn, class selector, save configuration).

Rows 5, 8, 9 and 10 share a second cause: **a test plan that named what to test, and nothing checked what it left
out**. The waiver for the boundary, the delegation-only VM test and the 100-point planner cases were all what the
plan asked for.

## Why each agent missed these

The seven lenses ran on the finished diff; the question is why the build did not catch them first.

- **Implementation (plan 022 executor):** followed the plan's boundary snippet and test list verbatim. It had no
  reason to open `SandBoxSiegeMissionSpawnHandler`, because the plan framed the adapter call as a parity choice.
- **Standards (Agent 1):** caught rows 4, 5, 6 and 7. The siege defect is outside its checks (no rule says which
  equipment a mission decision reads).
- **Engine (Agent 2):** caught rows 1, 2, 3, 4 and 6. It did not flag rows 8 to 12 (tests and XML bytes are other
  lenses' scope).
- **Efficiency (Agent 3):** nothing here is a cost defect; it raised row 10 as a follow-up.
- **Completeness (Agent 4):** caught rows 5, 7, 8, 9 and 10. It did not trace engine behaviour, so row 1 was out of
  its reach.
- **Data flow (Agent 5):** caught rows 1, 2, 3, 6 and 13, the highest-value lens again.
- **Design (Agent 6):** caught rows 1 and 3 and the sealed wording.
- **XML (Agent 7):** caught rows 11 and 12; C# was out of scope.
- **Codex:** caught rows 1, 2 and 9. It did not flag the LOW test gaps in rows 5, 8 and 10, the line endings or the
  stale labels, and it read the greedy tie-break loss as conforming to the plan's contract (true; it is now a
  question for Mike).

## Codex adversarial review

| # | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|
| 1 (P2) | Siege horse owners never placed (row 1) | Assumed an API worked a certain way | The plan assumed campaign gear describes the mission agent; the siege spawn path was never read | Adapter overload plus boundary change, `HeroCombatAdapterTests`; lesson broadened in adapters-taleworlds-api |
| 2 (P3) | Doc claims nothing is save-backed (row 2) | Other: unverified premise copied from the plan | Didn't trace the full lifecycle (deployment end to `SaveConfiguration`) | Doc corrected; lesson in state-lifecycle-save |
| 3 (P3) | VM test cannot see a broken message (row 9) | Other: test oracle asserts dispatch only | The plan's gate named delegation; `TextObject.ToString` swallowing errors was not considered | Message assertions for all three statuses; lesson in testing-qa |

## Feedback memories to codify

None beyond the lessons entries. The siege finding is a scope extension of an existing lesson, recorded there.
