# RCA: The Lord's Gear Ladder (deep review, 2026-09-28)

## Top-line

`/deep-review` of the uncommitted lord's gear ladder (#693) on top of the armour acquisition branch
(worktree `E:/repos/taom-armour-acquisition`, branch `feat/armour-acquisition`, HEAD `2e5b4a78`) ran nine
lenses in three waves: Standards, Engine compatibility and Data flow split in two (the deeds and claim
flow; the lord's materials flow); then XML, Tooling, Completeness and Efficiency; then Design. Verdict
before fixes: **NEEDS FIXES**. One HIGH (the validator's item registry counts config and class-table rows
as item definitions, so the named-weapon check and 15 of the 32 weapon picks can never fail), five MEDIUM
in the ladder itself (a rung start blocked after a Player Switcher change, materials dropping after the
last rung, "The Deep Seam" paying the giver's material rather than the player's, a native read on a
possibly deleted hero agent in every removal, and "named weapons are never awarded" left in four texts
the weapon rung contradicts), one MEDIUM gate gap, two MEDIUM data findings (weak placeholder weapon
picks; the Deep Seam's unguarded third copy of the culture map), and a long LOW tail: config validation,
parsing, six wrong engine claims in the doc and the materials header, missing tests and doc gaps. Engine
compatibility checked 36 API uses: none incompatible. Efficiency found no hot-path cost. The Design lens
proposed eight simplifications. Every finding below was re-read at its source this session. Mike asked
for all of it to be fixed (2026-09-28). After the fixes every finding is fixed or recorded as a known
limitation, and all eight proposals are applied; row 24 was found while correcting the doc. The
convergence pass on the fixes found one MEDIUM and seven LOW gaps (C1 to C8 below), all fixed.

## Findings

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | HIGH | The validator's item registry (`taom_schema._scan`, `_ITEM_DEF_RE`) treats every `<Item id>` row in any XML as an item definition, so `armour_acquisition_config.xml`'s `<NamedWeapons>` rows and the 2,860-row `armour_classes.xml` define their own ids: the named-weapon check and 15 ladder weapon picks can never fail, and a troop wearing retired armour that the class table still lists passes `BROKEN_ITEM_REF` until the table is regenerated (Tooling) | Gate self-reference | The registry keys definitions on an element's shape, not on the document the engine loads as items; phase one added the first TAOM files whose rows share that shape, and the gate's own tests used a mocked registry | Lesson (build-tooling): a scanned registry keys definitions on the document kind the engine loads (an `<Items>` root), never on an element shape. Fixed with a test that builds the real registry over a fake Armory |
| 2 | MED | Taking up a rung checks for anyone's running quest on it (`LadderPresenter.Rows`, `LadderQuests.Find(step.QuestId, null)`), while crediting kills and the hand-in look only at the main hero's own quest. `QuestManager.OnPlayerCharacterChanged` cancels only non-special quests (QuestManager.cs:282-292) and a CareerQuest is special, so after a Player Switcher change the old hero's rung quest blocks the new hero's start for good (Data flow A) | Ownership scope | The one-quest Lord's Harness rule ("any running harness quest, whoever's: a second would share the first's quest id") was carried into a per-hero ladder without re-deriving it; the id-sharing premise was never checked (the quest list is a plain list; the engine's only StringId-keyed quest path is map markers) | Lesson (state-lifecycle): scope a running-quest check the way the state it guards is scoped. A repeat of the career-quest dedup owner filter (Codex P2, 2026-08-01, `CareerQuest.cs:60-63`) |
| 3 | MED | Lord's materials keep dropping after the last rung is claimed, when nothing can use them (`RollMaterialDrop` checks only the win and the culture) (Data flow A and B) | Producer without its consumer | The drop was designed from the producer side; no test asked what consumes a find once the ladder is done | Lesson (campaign-mechanics): a resource's producer stops, or says why not, when its only consumer is exhausted |
| 4 | MED | "The Deep Seam" pays the giver's culture's material (the issue offer filters on the giver's culture only, `LotrIssueService.GetEligibleIssues`), while the ladder counts only the player's: a Gondor hero at a Rohan village gets metal no rung of theirs accepts (Data flow B) | Key mismatch between producer and consumer | The rows were modelled on the Armourer's Commission, whose chest of the giver's people is useful to anyone; a material is useful only to its own culture's ladder, and nobody asked whose culture the reward must match | Lesson (data-content): a reward useful to one culture only is offered on the recipient's culture, not the giver's |
| 5 | MED | The kill counter evaluates native `Agent.IsEnemyOf` (`MBAPI.IMBAgent.IsEnemy` on both agents' pointers) for every human removal, whoever the killer, against a cached hero agent whose native struct may already be deleted (a crash is unverified) (Engine, Data flow A) | Native interop, eager evaluation | The enmity check was folded into a bool argument of a pure, testable rule, which forced eager evaluation; the lifetime of a cached Agent's native pointer after `Mission.OnAgentDeleted` was not considered | Lesson (adapters): in an agent callback, return on the managed identity check before any native agent read; a cached Agent outlives its native deletion |
| 6 | MED | "Named hero weapons never change hands" (the MCM master hint), "never sold, looted or awarded" (`ArmourAcquisitionConfig.NamedWeapons`, the config header) and the doc's named-items line stayed after the weapon rung began awarding them (Data flow A) | Doc drift | A decision reversed an invariant stated in four places; only the feature doc's table was rewritten | Repeat of RCA 2026-09-27 row 19. Lesson (misc): when a decision reverses an invariant, grep its old words across code comments, config headers and MCM hints |
| 7 | MED | The gate's rung-quest lookup (`quest_root.iter("CareerQuest")`) matches at any depth, while the game reads only the root's own children (`CareerQuestConfigProvider`, `root.Elements`): a nested quest passes the gate and never loads (Tooling) | Gate reads more than the engine | The gate was written to find the quests, not to read them the way the loader does | Fixed with `findall`; the tooling lens rule already says a gate reads what the engine reads |
| 8 | LOW | `base_chance` above `max_chance` is not refused (`ReadDrop`); the find then silently stays flat (Standards) | Config validation | The ordering check was written for the unit range only; csharp-architecture.md "Config Providers MUST Validate" item 2 was applied to one pair of the two | Fixed with a test; no new rule |
| 9 | LOW | An empty `<LordsMaterials>` falls back to the defaults while an empty or all-invalid `<LadderWeapons>` leaves every weapon rung unclaimable, and a hand-in can ready a rung with no reward choices (Data flow A and B, Completeness) | Config consistency, guard placement | Each reader was written on its own; nobody compared their empty-list rules, and the hand-in guard was placed on the cost, not on the reward | Fixed: every ladder list falls back to its defaults with a warning when it yields nothing; a rung with no choices offers no hand-in |
| 10 | LOW | `LadderSlotRules.TryParse` accepts comma lists: `Enum.TryParse` applies flags semantics to any enum, so `slot="legs,shoulders"` loads as a Head rung (Completeness, proven on the CLR) | Enum parsing | The numeral trap was known and guarded (first character a letter); the comma form was not | Lesson (testing-qa): parse enum names against `Enum.GetNames`, never `Enum.TryParse` alone. The career-quest objective parse has the same gap (follow-up) |
| 11 | LOW | `ArmourSlot` sits in `LordsLadder.cs` though it belongs to the item record; `LadderHandIn` sits in the service file outside `Domain` (Standards) | File layout | Types were added where they were first needed | Fixed |
| 12 | LOW | `ArmourAcquisitionState.Target`'s catch-all arm sends any future state kind to the visiting-armourer map (Standards) | Exhaustiveness | A default arm stood in for the last named case | Fixed: every kind named, the rest refused |
| 13 | LOW | The ladder's "deeds are known" message shows with the master switch off (`OnQuestCompleted` never checks the gate) (Data flow A) | Gate coverage | The completion handler writes state that must survive a toggle, so the gate was left off it entirely, message included | Fixed: the state write stays, the message waits for the gate |
| 14 | LOW | The take-up tip promises lord captures on the hands, legs and shoulders rungs, which have no such deed (Standards, XML) | Text accuracy | One tip was written for the harder rungs | Fixed with one tip that fits every rung |
| 15 | LOW | `|| PlayerEncounter.Current?.IsJoinedBattle == true` adds nothing: `JoinBattleInternal` sets `_mapEvent` in the same branch (PlayerEncounter.cs:909, 918), and `FinalizeBattleFromComponent` clears `_mapEvent` alone (PlayerEncounter.cs:1575), so the clause could only have added missions after a battle's end (Engine; corrected by the convergence pass) | Simplicity | Copied from `EncounterAdapter`'s ownership check | Fixed |
| 16 | LOW | Six wrong engine claims in the feature doc and the materials header: "no town eats them" (a zero-demand category gets 1% of prosperity as daily demand, DefaultSettlementEconomyModel), "ItemCategory cannot be declared in XML" (Game.cs:314 registers it; XML sets only the id), "the Trade component is what vanilla gives every trade good" (vanilla XML non-food goods carry only `Civilian`), "is_merchandise keeps them out of the hideout pool" (only the value does), "only a patch could reach the loot screen" (`RosterToReceiveLootItems` is public), and the kill counter's mission list (any mission the encounter's map event backs counts) (Engine, Data flow B) | Unverified engine claims | The "Engine facts" section and the header were written from the researchers' summaries with spot checks on the load-bearing lines only; the rest read as settled | Repeat of RCA 2026-09-27 rows 3, 6 and 8. Lesson (misc): every engine claim written into a doc or a data header cites a line read that session, or says UNVERIFIED |
| 17 | LOW | "Kept out of every economy" overstated: besides the sale, towns consume a sold stack, the vanilla Trade Proposal incident picks any Goods stack in the main party and pays two or three times its value, trade rumours list the materials, and QuickActions' sell sweep takes them at a high threshold (Data flow B, Engine) | Missing known limitations | Only the channels that create materials were traced; the channels that take them away were not | Documented as known limitations |
| 18 | LOW | The validator's `load()` catches only `ET.ParseError`: a bad encoding declaration raises `LookupError` and the commit hook reads the crash as a validation error with no findings (Tooling; pre-existing) | Tool robustness | Parse errors were the only failure imagined | Fixed with a test |
| 19 | LOW | An unparsable quest file adds one "defined nowhere" error per rung beside the true parse error (Tooling) | Gate noise | The per-rung check ran on an empty id set | Fixed: the rungs are not checked against a file that did not parse |
| 20 | LOW | Tests missing: the wiring test never builds the kill counter's `MissionBehaviorDecl` (the first production one; a factory throw fails open); the default-mirror test checks only the materials; nothing pins the materials' economy attributes, the Deep Seam's material map, or the XML element names the gate reads; the gate fixture cannot tell `id` from `career_id`; the service lacks edge cases (the last rung, a saved count above the rungs, a weapon rung with neither picks nor donor, an empty owner) (Completeness, Data flow B, Tooling) | Test coverage | The tests were written for the rules, not for the wiring and data they rely on | Fixed |
| 21 | LOW | Doc gaps: the in-game checklist never runs a rung by deeds, the fallbacks, a save and reload mid-ladder, the missions that must not count or a battle left early; Owed lacks Mike's ruling on the builder's defaults, the tuning and the two shields; the positional ladder state is not a known limitation; the career-quest header, the file catalogue, the validator docstring, `moduledata-validation.md` and `tools/README.md` miss the ladder (Completeness, Tooling) | Completeness | The docs were written before the review's questions | Fixed |
| 22 | MED | The weapon rung's placeholder picks included Dale's own starting sword (`dale_sword_c`: 6 career kits, 10 character-creation kits, 3 enlistment kits, 5 Dale troops) and roster weapons of Gundabad and Dol Guldur, while the rung's description said the finest weapons "are never sold" though every pick is merchandise (XML) | Reward value | The picks were chosen by culture tag and weapon class; nobody asked which kits already hand them out, or read the description against `is_merchandise` | Fixed: re-picked from weapons no troop, lord or player kit carries where the culture has any, and the description no longer says "never sold"; whether the picks join `<NamedWeapons>` is owed to Mike. Lesson (data-content): check a unique reward against every kit that already carries it |
| 23 | MED | "The Deep Seam" rows hand-copy the culture-to-material map a third time (beside `<LordsMaterials>` and `armour_from`), and nothing checks that the copies agree: a row paying another culture's material would pass every gate (XML) | Duplicated data without a parity check | The gate resolves ids and the provider test counts rows; agreement between the copies was nobody's check | Fixed with `DeepSeam_EveryRowPaysTheMaterialOfEveryCultureItServes_OnlyToAPlayerOfThem`; the compiled ladder defaults have the same kind of mirror test (`Config_TheCompiledLadderMirrorsTheShippedFile`) |
| 24 | LOW | The config comment written with row 22's fix said the picks are weapons no troop, lord or player kit carries where the culture has any, but Dol Guldur's two-handed mace is carried by a Dol Guldur troop (`troops_dolguldur.xml:2420`): the culture has one such weapon, its halberd (found while correcting the doc) | Claim wider than its evidence | The scan that chose the picks showed the mace's one carrier; the comment stated the rule for both picks when only one met it | Fixed: the comment and the doc say which picks are troop weapons. Repeat of row 16's pattern |

## Design proposals (Step 4)

The Design lens returned eight KEEP proposals, all on changed code, all applied after the defect fixes:

| # | Proposal | Proof |
|---|---|---|
| D1 | Delete `IArmouryPlayerAdapter.ReadEquippedItemIds` and its implementation, orphaned when the one-quest Harness went | The compile; no caller in `Main` or the tests |
| D2 | Keep each hero's claimed rungs as a mask of slots (pinned `LadderSlot` values), not a count of rungs, so a reordered or shortened ladder resumes every hero at their first unclaimed slot. The key was new in this change, so no save needed migrating | `CurrentStep_TheConfigDropsARung_TheHeroResumesAtTheirFirstUnclaimedSlot`, `Decode_ARungMaskOutsideTheSixSlots_IsSkipped` |
| D3 | One thread-safe accumulator: the counter adds each kill straight to `HeroKillTally`, with no buffer of its own and no `OnEndMission` | `Add_FromManyThreads_LosesNothing`; the wiring test builds the counter's decl |
| D4 | `HeroKillTally.Counts` keeps only the state rule, so the humanity and native enmity checks run last, for the hero's own kills only (the root of row 5) | `Counts_AKill_OrAKnockoutWhenTheLadderCountsThem` |
| D5 | Delete `IsLadderQuest`: `OnQuestEnded` alone decides which quest readies a rung | `OnQuestEnded_AFailureOrAnotherRungsQuest_ChangesNothing` |
| D6 | `GetPieces(cls, cultureId, ArmourSlot? slot = null)`: `ArmourSlot.None` keeps its one meaning, so a slot with no mapping lists nothing instead of every slot | `GetPieces_WithASlot_ListsOnlyThePiecesWornThere` |
| D7 | `LadderQuests.Find` takes the owner (the any-owner mode died with row 2), and two summaries name `LadderPresenter` | The compile |
| D8 | `reward_count` read and parsed once | `ParseIssues_RewardCount_IsRead_AndDefaultsToOne`, `ParseIssues_RewardCountOutOfRange_IsOne` |

The lens also left a question for Mike: `HeroKills` parses for any career quest, but only the current
rung's quest is fed, so a career quest listing it would never progress. A shipped-data assert now pins
`HeroKills` to the rung quests.

## Convergence pass (Step 4.6)

One `deep-reviewer` checked every row and D-row above for standards and behaviour parity. It confirmed the
save mask's range guard, the kill counter's order (no native read on a removal the hero did not cause) and
every cited engine line, and found these gaps, all fixed:

| # | Sev | Finding | Fix and proof |
|---|---|---|---|
| C1 | MED | No test built the kill counter's mission decl, though row 20, D3's proof and the feature doc said one did; the runner starts mission behaviors fail-open, so a factory throw would silently drop the counter | The wiring test builds it (`module.MissionBehaviors.Single().Create(null!, container)`). Lesson (testing-qa) |
| C2 | LOW | Row 6 incomplete: the doc's config table still said the named weapons are never awarded | Fixed |
| C3 | LOW | The Deep Seam's player filter ran only at the offer, and a new campaign creates its first issues before character creation, while the main hero is SandBox's `battania` placeholder: `lotr_deep_seam_khuzait` (khuzait, battania) could open at Rhûn and Khand villages for any player, and a hero switch left the same mismatch | `LotrIssueDefinition.OffersTo`, shared by the offer and all three templates' `IssueStayAliveConditions`, which the engine runs daily and when the player enters the settlement, and which drops only untaken offers (IssueManager.cs:262, 516); `OffersTo_ForPlayerCulture_OnlyAPlayerOfTheIssuesCultures`. Lesson (campaign-mechanics) |
| C4 | LOW | Row 3 incomplete: finds continued while the last rung waited for its claim | A find needs a rung neither claimed nor done: `RollMaterialDrop_TheLastRungWaitingToBeClaimed_FindsNothing` |
| C5 | LOW | D2 reached the claimed rungs only: readiness stayed one flag per hero, so a config edit that dropped or moved the ready rung handed its readiness to another | Readiness is a slot mask too, while no save holds it: `IsReady_TheReadyRungDroppedFromTheConfig_ReadiesNoOtherRung`, `IsReady_TheLadderReordered_FollowsTheSlotThatWasDone`, `Decode_AMaskOutsideTheSixSlots_IsSkipped` |
| C6 | LOW | Row 2's owner filter is exercised by no test or check (`LadderQuests` is an engine boundary) | An in-game checklist line: a Player Switcher change mid-rung |
| C7 | LOW | `for_player_culture` is not documented in `lotr-issues.md` | Documented |
| C8 | LOW | Two stale test comments (the Harness quest; the counter's check order) | Fixed |

**Why these got through:** C1 is the third claim without its evidence in this review (after rows 16 and 24):
the proof was written from the plan for the test, not from the test. C4 and C5 are fixes applied to the case
in view (a ladder fully climbed; the claimed flag) and not to its sibling (the last rung waiting; the ready
flag). C3 tested the offer with an adapter that always held the final player culture; nobody asked when the
first offers are made.

## Verification after the fixes

- `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=`, after the convergence fixes: 11,153
  passed, 1 failed, 2 skipped. The failure is `NoTranslatedString_MixesWritingSystems`: the new keys'
  English seeds in CN, JP, KO and RU, owed to the paid translator run. Each convergence fix went RED first
  (C4's drop test failed alone once C5 compiled; `OffersTo` failed to compile before C3).
- `python -m pytest tools/tests`: 2,916 passed, 3 failed, 8 skipped. None of the three reads a file this
  change touches: the heraldry specs against `spclans.xslt`'s line endings, the committed career kits
  against what the rule derives today from the troops and the live Armory, and graphify's default output
  folder, which differs in any worktree.
- `python tools/validate_moduledata.py`: 0 errors (after the convergence fixes too; its 207 tests pass).
  `python tools/generate_armour_classes.py --check`: the table is current. `python tools/lint_docs.py`: 0 dead links, 0 untracked targets, 0 new dashes.
  `python tools/harvest_literal_loc_keys.py --dry-run`: 0 unregistered.

## Root-cause patterns

- **A rule carried from the design it replaced (#2, #4).** The Harness's any-owner check and the
  Commission's giver-culture filter were right for what they came from and wrong for the ladder. Both came
  from reusing a sibling's shape without restating why it held.
- **Producers designed without their consumers (#3, #4, #9).** A find nobody can spend, a reward nobody can
  use, a hand-in for a piece nobody can claim: each flow was traced forward and never backward from its
  consumer.
- **Claims assumed, not read (#1, #16, #17).** The gate trusted a registry whose inputs changed; the doc
  trusted summaries. Both are repeats of the phase-one review's lead pattern.
- **Eager evaluation for testability (#5).** A pure rule is good; passing it arguments that cost a native
  call on every removal is not.

## Why the builder missed these, and which lens caught them

The ladder was built test-first and every service rule was green, but the defects sit in the seams: who
owns a quest after a hero switch, whose culture a reward follows, what happens after the last rung, and
what the tools treat as a definition. Data flow A caught the ownership gap and the stale texts; Data flow
B the Deep Seam, the drop lifecycle and the economy exits; Engine compatibility the native read and the
wrong engine claims; Standards the validation, layout and text slips; Completeness the parse gap and the
missing tests and checks; Tooling the self-referencing registry, which predates the ladder and would
have hidden a retired named weapon or a retired piece of armour at the next art drop; XML the weak
weapon picks and the unguarded third copy of the culture map. Efficiency confirmed the kill counter's
cost once gated, and Design turned a documented save trap (the count of rungs) into a non-issue before
any save held the state. Splitting Data flow in two again paid off: each half found
a MEDIUM the other did not look for.

## Feedback to codify

- Lessons appended: `build-tooling-workflow.md` (#1), `state-lifecycle-save.md` (#2, and the
  zombie-quest rule the research found: never delete a shipped CareerQuest definition),
  `campaign-mechanics.md` (#3), `data-content-cultures.md` (#4, #22), `adapters-taleworlds-api.md` (#5),
  `misc.md` (#6, #16; #24 is a repeat of #16), `testing-qa.md` (#10, C1), `campaign-mechanics.md` (C3).
- No new rule file: evidence-over-claims covers #16 and #17 and the tooling lens covers #1 and #7; each
  is recorded here as a repeat so the next review reads it.

## Follow-ups (pre-existing code, not this change)

- `CareerQuestConfigProvider` parses objective types with `Enum.TryParse` alone (numerals and comma
  lists pass), the same gap as #10.
- `CareerQuest.cs` is 314 lines against ADR-002's 150 (302 before this change).
- WinBattles and DefeatEnemyLords credit every running CareerQuest whatever its owner, so after a hero
  switch the old hero's quest counts the new hero's battles; the ladder inherits it (its kills stay
  owner-scoped).
- `EnlistmentMeritMissionBehavior` credits `IsMainAgent`, so a soldier the player takes control of after
  the hero falls earns the hero's merit; the ladder's cached agent avoids that.
