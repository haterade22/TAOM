# RCA: formation presets, adoption item 5 (deep review, 2026-10-09)

## Top-line

Item 5 of the yotthani adoption finishes TAOM's formation presets (#779): Save now captures the Order of Battle layout
(formation classes, captains, hero troops) and Load applies it through vanilla's public view-model flows. The
`/deep-review` ran seven lenses in two waves on the uncommitted worktree `E:\repos\taom-yotthani` (base `83ad1eab5`).
It found no CRITICAL or HIGH defect, and all 31 vanilla API usages resolve on the installed v1.5.4 binaries. It found
one apply defect (heroes placed into a formation whose saved class was blocked), two scope limits of the design
(presets store no troop shares or filters; a swap of single-class formations cannot apply), one reach gap (the overlay
in Custom Battle, where nothing persists) and one verification gap (no save-and-reload check of a populated preset),
plus LOW findings, most of them doc claims. Every finding was checked against the code, the v1.5.4 decompile or the
installed binaries before it was fixed or recorded. Three findings trace to the orchestrator's own build brief.

Review record: [adopt-yotthani-2026-10-08.md](adopt-yotthani-2026-10-08.md). Lenses: wave 1 Standards, Engine
compatibility, Data flow and XML; wave 2 Efficiency, Completeness and Design. Feature doc:
[companion-tactics.md](../features/companion-tactics.md).

## Findings and root causes

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | MED | `Apply` planned heroes for every formation with a class (`HasFormation`), so a class step that vanilla blocked (not `IsAdjustable`) still received the preset's captains and troops, into a formation of the wrong type (Data flow; Engine for the swap case) | A later stage used a weaker test than the plan's intent | The orchestrator's brief wrote the presence rule as "has a class", and the class pass returned counts only, so the set of applied steps was lost | `FormationPresetLayout.PlanLoad` plans heroes only for formations whose class step applied or already matched, from the pass loop's `Applied` list; tests `PlanLoad_ClassStepBlocked_SkipsItsHeroesAndCountsEverything`, `PlanLoad_ClassUnmappableInThisBattle_SkipsItsHeroesAndCountsEverything`, `PlanLoad_OneFormationReadyOneBlocked_PlansOnlyTheReadyOnesHeroes`. Lesson in `gamemodels-services.md` |
| 2 | MED | A preset stores no class weights (troop shares) and no filters. A class that another formation already has loads at 0 percent with only its heroes, and vanilla then stores that as the last layout. The doc said "vanilla moves the troops" (Data flow, Engine) | Port scope; a doc claim from a handler's role | The brief copied HoN's scope (classes, captains, troops) and called `OnFormationClassChanged` "vanilla's troop transfer" without its two branches; the builder turned that into the doc claim | Doc and class comment corrected (both branches); Known limitations and in-game checks; #787 for weights and filters (a save-format change: the maintainer's decision). Recurrence noted on `gamemodels-services.md` "Describe a vanilla model method from every branch" |
| 3 | MED | A swap or rotation between formations that are each the only formation of a class never applies (vanilla's `IsAdjustable` rule). While finding 1 was open, their heroes went into the wrong formations (Engine) | Port scope | The pass loop was designed for order dependencies, not for cycles | Documented, in-game check, #787; finding 1's fix skips those heroes and counts them |
| 4 | MED | The overlay attached in Custom Battle. The preset store is a singleton that only the campaign behavior persists (`SyncData`) or resets (a new game), so Save reported success for a preset that the next campaign load dropped; Auto-Assign finds no hero there, because Custom Battle agents are `BasicCharacterObject` (Data flow) | Reach across game types | Patch35 is applied in every game type; the brief never named the game types; the 2026-10-02 lesson on adapter branches that are dead in Custom Battle was not in it | `OOBOverlayService.OnTick` gates on `Campaign.Current`; in-game check (no overlay in Custom Battle). Recurrence noted on `adapters-taleworlds-api.md` |
| 5 | MED | This is the first build that writes data into saveable fields 4, 5 and 6, but nothing checked a populated preset through a campaign save and reload, and the doc claimed a "SaveableType round-trip" test that does not exist (Completeness) | Verification gap | The serialization guard checks field types against an allowlist; the old build saved empty containers, so the gap never showed | In-game checklist: save a preset, save the campaign, reload, load the preset. TAOM.Tests has no engine save harness; the type guard and v1.5.4's registration of the three containers (`SaveableBasicTypeDefiner.cs:117, 166, 167`) cover the types. Stale test claims removed |
| 6 | LOW | Save was not gated on `IsPlayerGeneral`; Load, Auto-Assign and vanilla's own `SaveConfiguration` are (Data flow) | Inconsistent gate | The brief left Save ungated on purpose and did not compare it with vanilla's save | The presets menu opens only for the general (`ExecuteManagePresets_PlayerIsNotTheGeneral_ShowsTheMessageAndNoMenu`); the unreachable `PresetApplyStatus` deleted (Design) |
| 7 | LOW | A player-typed preset name passes through `SetTextVariable`, and the text processor parses the string again as markup, so braces render empty (XML, Standards) | Untrusted input in a template | The old code interpolated the name raw; the localization pass moved it into the template | Braces removed at save (`Save_NameWithBraces_ReachesCaptureWithoutThem`). Lesson in `localization-ui.md` |
| 8 | LOW | The reflection-site labels for `OOBOverlayService` went stale again when its constructor grew (Standards, Engine). A repo-wide check then found six labels that pointed at the wrong code: three from item 3 (`NameplateCullAdapter.cs`, wrong since `ecaa67d26` added them), `SubModule.cs:503` (the patch moved to `ManualPatchApplicator.cs`), `CustomAttacksUtils.cs:55` and `XmlMergeEngineAdapter.cs:52`; the catalogue also lacked the `Mission._initialPlayerAgent` row | REPEAT of plan 022 RCA row 6 | Plan 022 recorded line labels as a known weak pointer with no gate; a label shows only in a failure message, and no convention said which line of a multi-line lookup to label | Two gates in `ReflectionSiteBindingTests`, no game needed: `EveryLineLabel_PointsAtALineThatNamesItsMember` (comments blanked; a label without a line must name an existing file) and `EveryDataRow_HasAMatchingRowInTheCatalogue`. The convention (label the line that spells the member) is written in the test and in `reflection-sites.md`; it moved eleven more labels off the first line of a multi-line lookup or a declaration and trimmed six item 1 labels of a shared helper line. Lesson in `testing-qa.md` |
| 9 | LOW | Dead or unread members: `FormationSnapshot.OfferedClasses` and `HasFormation`, `HoNFormationPreset.GetSummary` and `GetFormationClass`, and a production branch that existed only for unit tests (`VanillaText`) (Data flow, Design) | Dead code | Written for design steps that the final code did not take | Deleted; OK and Cancel use vanilla's own localization keys; the snapshot record renamed `PresetFormationSnapshot`, because a public `FormationSnapshot` already exists in CrashReport |
| 10 | LOW | Wrong engine wording: "drag" for vanilla's click path, "a change vanilla refuses is logged" (the `IsAdjustable` refusal was silent), an imprecise `IsAdjustable` rule, and stale test claims (missing-hero pruning, SaveableType round-trip, mission-end reset) (Engine, Data flow, Completeness) | Doc claims | The doc was written from the brief, and the old claims sat inside the edited block | Corrected; a stuck class step now gets a debug line |
| 11 | LOW | The harvester doubled the blank line before `</strings>` (XML) | Tool bug, #781 | Known since the items 1 to 4 review | One line deleted |
| 12 | LOW | Records: no credit line, Changelog bullet, Issue section, field meanings or co-op line in the feature doc; #782 did not list the 21 new keys; #117 duplicates #779; #779 lacked `triage-needs-ingame` (Completeness) | Records | Written after the code and before the review | Doc completed; #782, #117 and #779 updated |
| 13 | NIT | Three test names without the state segment, and two missing asserts (a null-preset count, the save prompt's texts) (Standards, Completeness) | Test hygiene | | Renamed; asserts added |

## Root-cause patterns

**A port inherits upstream's scope** (findings 2 and 3), the same pattern as finding 1 of the items 1 to 4 RCA.
yotthani's HoN preset stores classes, captains and hero troops, and the plan copied that scope. The review compared
the result with vanilla's own layout store, which also keeps weights and filters, and with vanilla's `IsAdjustable`
rule, which the plan did not model.

**The brief is a source of defects too** (findings 1, 2 and 4). The orchestrator wrote the presence rule ("has a
class"), the "troop transfer" description and a scope without game types into the build brief, and the builder
implemented them as written. A brief that states engine behaviour needs the same evidence as a doc: each branch read,
each game type named.

## Why each lens caught what it caught

- **Standards** found the stale anchors (8), the test names (13) and a gap in the standards docs: a DI-registered
  boundary class over a live TaleWorlds view model has no recorded exception under ADR-007 (see the decisions below).
- **Engine compatibility** verified all 31 API usages and found the two scope limits (2, 3), the wording (10) and the
  anchors (8). It named one UNVERIFIED path (a Load after the view model is finalized) and found no route to it: the
  presets menu is a modal inquiry.
- **Data flow** traced the chain from the screen to the campaign save and back, and found the hero plan defect (1),
  the shares (2), Custom Battle (4), the Save gate (6) and the dead members (9). Its one UNVERIFIED point, HoN's class
  encoding, is disproved: yotthani's `FormationPresetManager.cs:55-56` stores `(int)GetOrderOfBattleClass()`, and -1
  for unset, which are the values TAOM stores.
- **XML** ran six gates (one failure older than this change: the live modules' external localization coverage) and
  found the name markup (7) and the blank line (11).
- **Efficiency** found no issue at the real call rates, placed the Custom Battle check at the top of `OnTick`, and
  added the large-battle hitch check.
- **Completeness** found the verification gap (5) and the record gaps (12), and listed the tests that each planned fix
  needed.
- **Design** turned finding 1's fix into data that the pure layer already had (the pass loop's applied steps) and
  proposed five simplifications, all applied, plus one follow-up.

## Not applied, with reasons

- **Troop shares, filters and single-class swaps** (findings 2 and 3): #787. Weights and filters need new saveable
  fields on `HoNFormationPreset`, a save-format change outside the plan the maintainer approved, and their apply path
  (the slider's `DistributeWeights` with temporary locks) and the spare-formation swap have not run in game.
- **`string?` on the snapshot's captain id** (Standards): the CompanionTactics folder uses no nullable annotations.
- **A check that the tracked view model is still current before Load** (Engine, UNVERIFIED): no route found.
- **Removing `OOBButtonsVM`'s unused logger** (Design): code older than this change; a one-line clean-up for the next
  change that touches the view model.
- **The per-frame boxed read of `_isActive`** (Efficiency): older code, unmeasured, and the feature is off by default;
  no issue filed.

## Lessons appended

- `gamemodels-services.md`: "A later stage gets the exact set an earlier stage completed, never a weaker live test"
  (finding 1), and a recurrence on "Describe a vanilla model method from every branch" (finding 2).
- `adapters-taleworlds-api.md`: a recurrence on "An adapter branch on an engine subtype is dead in a game type that
  registers the base type" (finding 4).
- `localization-ui.md`: "Player-typed text never goes into a TextObject template with its braces" (finding 7).
- `testing-qa.md`: "A line-number label that drifted twice gets a gate" (finding 8).

## Codex review and convergence pass (2026-10-09)

Codex (gpt-6-astra at high, prompt `codex-adversarial-formation-presets-2026-10-09.prompt.md`) and one Claude
convergence lens (Data flow) read the fixed tree at the same time. Codex: 0 CRITICAL, HIGH or MEDIUM, 3 LOW. The lens
confirmed all nine fixes and found no runtime gap, only test and record drift. Every finding below was checked
against the code before it was fixed.

| # | Sev | Finding | Source | Fix |
|---|---|---|---|---|
| 14 | LOW | A preset name saved by an older build never passed through Save's brace removal, so the menu showed "Load: Guard " for "Guard {X}" (reproduced against the installed localization DLL) | Codex F1 | One `PlainName` helper cleans every stored name at display time and replaces Save's inline expression; `ExecuteManagePresets_StoredNameWithBraces_ShowsTheNameWithoutThem` |
| 15 | LOW | The doc and the applier's class comment said heroes outside the preset "stay where they are"; a saved captain displaces the current captain, as a manual pick does | Codex F2 | Both corrected |
| 16 | LOW | The doc's file table still described the deleted `PresetApplyStatus` and omitted `PlanLoad` | Codex F3, lens | Corrected |
| 17 | LOW | The applier binding test still pinned `IsControlledByPlayer`, which no TAOM code reads after the fixes | Lens | Assert deleted; `HasFormation` kept and marked as Auto-Assign's |
| 18 | LOW | The first label gate accepted a label on a comment or a declaration, skipped labels without a line, and moved seven correct labels without a written convention; RCA row 8 called those seven stale | Lens | Comments blanked, file existence checked, the convention written down, a catalogue check added, row 8 and the lesson corrected |
| 19 | NIT | "manual-drag" for vanilla's click path in the Auto-Assign doc and class comment; the tooltip patch's location in the doc and a test comment; "only `SyncData`" where a new game also resets the store | Lens | Corrected |

**One disagreement.** Codex asked for finding 14's fix; the lens judged it below the simplicity bar, because only a
name that a player typed with braces before this change is affected. It is applied, because the helper also replaces
the expression Save already had, so the cost is one wrapped argument at each of six display sites.

The lens's example for finding 18 (`MissionAttributionInstaller.cs:17` and `:28`, comment lines naming `OnTick`) was
not a real case: those lines were never labels, and the labels `:35,66` spell `"OnTick"` in code.

**Codex pass 2** (gpt-6-astra at high, on findings 14 to 19's fixes; prompt `...-pass2.prompt.md`): 0 CRITICAL, HIGH or
MEDIUM, 3 LOW. It confirmed every fix above, the four repointed labels and the new catalogue row.

| # | Sev | Finding | Disposition |
|---|---|---|---|
| 20 | LOW | The catalogue gate matched a label as a substring, so a wrong catalogue label such as `MonsterSizeCatalogAdapter.cs:230` passed for the DataRow's `:23` (reproduced in memory) | Fixed: `HasExactToken` matches a whole token only; `HasExactToken_LongerLineNumberOrLongerFileName_DoesNotMatch` pins `:23` against `:230`, `:35,66` against `:35,66,70`, and one file name inside another. Not sent to a third Codex pass: a test-only regex, proven by its own negative cases |
| 21 | LOW | `RepoPaths.StripComments` is not string-aware, so a `//` or `/*` inside a string literal blanks real code on a labelled line, and the gate would report a correct label as stale; no current label is affected | Documented in the gate's doc comment, with what to do. Not made string-aware: the failure is loud, never a silent pass, and a string-aware stripper costs more than the case it covers |
| 22 | LOW | `PlainName` can show two presets with one name: an older preset named `Guard {OLD}` and a new one named `Guard OLD` both read `Load: Guard OLD` | Not applied (simplicity criterion). The win: a player who kept a preset named with braces from the stub build and saves another with the same name without them could tell the two apart. The cost: a suffix rule in the menu or a second uniqueness rule. Load and Delete act by id, so nothing is lost or overwritten |

## Decisions owed to the maintainer

1. **#787**: extend the preset save format with troop shares and filters, and break single-class swaps through a
   spare formation, after the in-game check of #779.
2. **The boundary-class pattern** (Standards): record "a DI-registered boundary class over a live TaleWorlds view
   model" (`OOBCaptainAutoAssigner`, `OOBPresetApplier`) as an intentional pattern in `.ai/review-reference.md`, with
   its conditions (the decisions in a pure tested class, public flows only, a check after each step, the vanilla
   members pinned by binding tests), so that a literal ADR-007 pass does not file a CRITICAL. Changing the review
   policy is the maintainer's call, so it is not done here.
